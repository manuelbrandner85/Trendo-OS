// Prüft K7 in einem echten Chrome am fertigen Export: lokale Anmeldung, Eintrag speichern, nach dem Neuladen noch da (SQLite in IndexedDB),
// „nur nach Anmeldung“ sperrt das Formular, Supabase bekommt den Eintrag mit dem öffentlichen Schlüssel (Anfrage abgefangen, nichts geht ins Netz).
// Aufruf: node pruefe-daten.mjs <chrome> <Export-Ordner>
import { spawn } from "node:child_process";
import { createServer } from "node:http";
import { mkdtempSync, readFileSync, existsSync, rmSync, statSync } from "node:fs";
import { tmpdir } from "node:os";
import { join, extname, normalize } from "node:path";

const [chrome, exportDir] = process.argv.slice(2);
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const types = { ".html": "text/html; charset=utf-8", ".js": "text/javascript; charset=utf-8", ".css": "text/css; charset=utf-8" };
const server = createServer((req, res) => {
  const rel = normalize(decodeURIComponent(new URL(req.url, "http://x").pathname.slice(1)) || "index.html");
  const file = join(exportDir, rel);
  if (rel.startsWith("..") || !existsSync(file) || statSync(file).isDirectory()) { res.writeHead(404); res.end(); return; }
  res.writeHead(200, { "content-type": types[extname(file)] ?? "application/octet-stream" }); res.end(readFileSync(file));
});
await new Promise((r) => server.listen(0, "127.0.0.1", r));
const base = `http://127.0.0.1:${server.address().port}/`;
const profile = mkdtempSync(join(tmpdir(), "trendo-chrome-daten-"));
const port = 20000 + Math.floor(Math.random() * 30000);
const browser = spawn(chrome, ["--headless=new", "--no-sandbox", "--no-first-run", "--disable-extensions", "--remote-allow-origins=*", `--remote-debugging-port=${port}`, `--user-data-dir=${profile}`, "--window-size=1280,900", "about:blank"], { stdio: "ignore" });
const results = {}; const problems = []; let ws;
const check = (name, ok, detail) => { results[name] = ok; if (!ok) problems.push(name + (detail === undefined ? "" : ": " + JSON.stringify(detail))); };
try {
  let target;
  for (let i = 0; i < 200 && !target; i++) { try { target = (await (await fetch(`http://127.0.0.1:${port}/json/list`)).json()).find((t) => t.type === "page"); } catch { } if (!target) await sleep(150); }
  ws = new WebSocket(target.webSocketDebuggerUrl); await new Promise((r, e) => { ws.onopen = r; ws.onerror = e; });
  let id = 0; const waiting = new Map(); const exceptions = []; const supa = []; const fremd = [];
  const call = (method, params = {}) => new Promise((r) => { const n = ++id; waiting.set(n, r); ws.send(JSON.stringify({ id: n, method, params })); });
  ws.onmessage = (m) => {
    const d = JSON.parse(m.data);
    if (d.id && waiting.has(d.id)) { waiting.get(d.id)(d); waiting.delete(d.id); return; }
    if (d.method === "Runtime.exceptionThrown") exceptions.push(d.params.exceptionDetails.exception?.description ?? d.params.exceptionDetails.text);
    if (d.method === "Fetch.requestPaused") {
      const u = d.params.request.url;
      if (u.startsWith(base)) { call("Fetch.continueRequest", { requestId: d.params.requestId }); return; }
      if (u.includes(".supabase.co/")) supa.push({ url: u, method: d.params.request.method, apikey: d.params.request.headers.apikey || d.params.request.headers.Apikey, body: d.params.request.postData });
      else fremd.push(u);
      call("Fetch.fulfillRequest", { requestId: d.params.requestId, responseCode: 201, responseHeaders: [{ name: "Access-Control-Allow-Origin", value: "*" }, { name: "Access-Control-Allow-Headers", value: "*" }], body: "" });
    }
  };
  const run = async (expression) => { const r = await call("Runtime.evaluate", { expression, awaitPromise: true, returnByValue: true }); if (r.result?.exceptionDetails) throw new Error(r.result.exceptionDetails.exception?.description ?? "Skriptfehler"); return r.result?.result?.value; };
  const until = async (expression, ms = 20000) => { for (let i = 0; i < ms / 50; i++) { if (await run(expression).catch(() => false)) return true; await sleep(50); } return false; };
  await call("Page.enable"); await call("Runtime.enable"); await call("Fetch.enable", { patterns: [{ urlPattern: "*" }] });
  const open = async () => { await call("Page.navigate", { url: base + "index.html" }); await until("document.documentElement.dataset.fertig === '1' && !!window.TrendoDaten"); await sleep(300); };
  const type = (sel, text) => run(`(()=>{const e=document.querySelector(${JSON.stringify(sel)});const set=Object.getOwnPropertyDescriptor(e.tagName==='TEXTAREA'?HTMLTextAreaElement.prototype:HTMLInputElement.prototype,'value').set;set.call(e,${JSON.stringify(text)});e.dispatchEvent(new Event('input',{bubbles:true}));return true})()`);

  await open();
  check("Datenbank-Bündel geladen", await run("!!window.TrendoDaten && window.TrendoDaten.bereit === true"));
  check("Nur nach Anmeldung: Formular gesperrt", await until(`document.body.innerText.includes('Bitte zuerst anmelden')`));
  // Konto anlegen (lokal)
  await run(`[...document.querySelectorAll('.t-anmeldung button')].find(b=>b.textContent==='Neues Konto').click()`);
  await sleep(100);
  await type(".t-anmeldung input[name=name]", "gast"); await type(".t-anmeldung input[name=passwort]", "kurz");
  await run(`document.querySelector('.t-anmeldung form').requestSubmit()`);
  check("Zu kurzes Passwort abgelehnt", await until(`document.body.innerText.includes('mindestens 10 Zeichen')`));
  await type(".t-anmeldung input[name=passwort]", "ein-langes-passwort");
  await run(`document.querySelector('.t-anmeldung form').requestSubmit()`);
  check("Konto angelegt, angemeldet", await until(`document.body.innerText.includes('Angemeldet als')`));
  check("Passwort nicht im Klartext gespeichert", await run(`(async()=>{const d=await new Promise(r=>{const q=indexedDB.open('trendo-daten',1);q.onsuccess=()=>r(q.result)});const all=await new Promise(r=>{const t=d.transaction('db').objectStore('db').getAll();t.onsuccess=()=>r(t.result)});return all.length>0 && !new TextDecoder().decode(all[0]).includes('ein-langes-passwort')})()`));
  // Eintrag speichern
  await until(`!!document.querySelector('.t-daten:not(.t-anmeldung) form')`);
  await type(".t-daten:not(.t-anmeldung) input[name=name]", "Erika");
  await type(".t-daten:not(.t-anmeldung) textarea[name=nachricht]", "Schöne Seite!");
  await run(`document.querySelector('.t-daten:not(.t-anmeldung) form').requestSubmit()`);
  check("Eintrag gespeichert und in der Liste", await until(`[...document.querySelectorAll('.t-daten-liste li')].some(li=>li.innerText.includes('Schöne Seite!'))`));
  check("Pflichtfeld geprüft", await (async () => { await run(`document.querySelector('.t-daten:not(.t-anmeldung) form').requestSubmit()`); return until(`document.body.innerText.includes('Bitte „Name“ ausfüllen')`); })());
  check("Supabase bekam den Eintrag mit dem öffentlichen Schlüssel", await (async () => { for (let i = 0; i < 60 && !supa.length; i++) await sleep(50); const posts = supa.filter((x) => x.method === "POST"); return posts.length === 1 && posts[0].method === "POST" && posts[0].url.endsWith("/rest/v1/gaeste") && (posts[0].apikey || "").startsWith("eyJ") && JSON.parse(posts[0].body).nachricht === "Schöne Seite!"; })(), supa);
  // Neu laden: Daten bleiben (lokal), Anmeldung gilt in der Sitzung
  await open();
  check("Nach dem Neuladen noch da (SQLite auf dem Gerät)", await until(`[...document.querySelectorAll('.t-daten-liste li')].some(li=>li.innerText.includes('Schöne Seite!'))`));
  await run(`[...document.querySelectorAll('.t-anmeldung button')].find(b=>b.textContent==='Abmelden').click()`);
  check("Abgemeldet: Formular wieder gesperrt", await until(`document.body.innerText.includes('Bitte zuerst anmelden')`));
  await type(".t-anmeldung input[name=name]", "gast"); await type(".t-anmeldung input[name=passwort]", "falsches-passwort");
  await run(`document.querySelector('.t-anmeldung form').requestSubmit()`);
  check("Falsches Passwort abgelehnt", await until(`document.body.innerText.includes('Name oder Passwort stimmt nicht')`));
  check("keine Anfragen an fremde Adressen", fremd.length === 0, fremd);
  check("keine Skriptfehler", exceptions.length === 0, exceptions.slice(0, 3));
} catch (e) { problems.push(String(e?.message ?? e)); }
finally {
  try { ws?.close(); } catch { }
  browser.kill(); server.close(); await sleep(300);
  try { rmSync(profile, { recursive: true, force: true, maxRetries: 25, retryDelay: 200 }) /* T-0729: Chrome-Hilfsprozesse geben Dateien erst nach Sekunden frei */; } catch { }
}
process.stdout.write(JSON.stringify({ results, problems }));
process.exit(problems.length ? 1 : 0);
