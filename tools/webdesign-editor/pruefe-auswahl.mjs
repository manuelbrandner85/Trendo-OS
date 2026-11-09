// Prüft das gebaute Editor-Bündel in einem echten Chrome: Auswählen in der Vorschau, Text direkt ändern, KI-Vorschau.
// Aufruf: node pruefe-auswahl.mjs <chrome> <Ordner des Bündels>. Ausgabe: JSON mit den Ergebnissen; Fehler ergeben Exitcode 1.
import { spawn } from "node:child_process";
import { createServer } from "node:http";
import { mkdtempSync, readFileSync, existsSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join, extname, normalize } from "node:path";

const [chrome, bundle] = process.argv.slice(2);
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const types = { ".html": "text/html; charset=utf-8", ".js": "text/javascript; charset=utf-8", ".css": "text/css; charset=utf-8" };
const server = createServer((req, res) => {
  const rel = normalize(new URL(req.url, "http://x").pathname.slice(1) || "index.html");
  const file = join(bundle, rel);
  if (rel.startsWith("..") || !existsSync(file)) { res.writeHead(404); res.end(); return; }
  res.writeHead(200, { "content-type": types[extname(file)] ?? "application/octet-stream" }); res.end(readFileSync(file));
});
await new Promise((r) => server.listen(0, "127.0.0.1", r));
const site = `http://127.0.0.1:${server.address().port}/index.html?ohneRahmen`;
const profile = mkdtempSync(join(tmpdir(), "trendo-chrome-auswahl-"));
const port = 20000 + Math.floor(Math.random() * 30000);
const browser = spawn(chrome, ["--headless=new", "--no-sandbox", "--no-first-run", "--disable-extensions", "--remote-allow-origins=*", `--remote-debugging-port=${port}`, `--user-data-dir=${profile}`, "--window-size=1440,900", "about:blank"], { stdio: "ignore" });

const results = {}; const problems = []; let ws;
const check = (name, ok, detail) => { results[name] = ok; if (!ok) problems.push(name + (detail === undefined ? "" : ": " + JSON.stringify(detail))); };
try {
  let target;
  for (let i = 0; i < 80 && !target; i++) {
    try { target = (await (await fetch(`http://127.0.0.1:${port}/json/list`)).json()).find((t) => t.type === "page"); } catch { /* Chrome startet noch */ }
    if (!target) await sleep(150);
  }
  if (!target) throw new Error("Chrome hat keinen Vorschau-Kanal geöffnet.");
  ws = new WebSocket(target.webSocketDebuggerUrl);
  await new Promise((r, e) => { ws.onopen = r; ws.onerror = e; });
  let id = 0; const waiting = new Map(); const exceptions = [];
  ws.onmessage = (m) => {
    const d = JSON.parse(m.data);
    if (d.id && waiting.has(d.id)) { waiting.get(d.id)(d); waiting.delete(d.id); }
    else if (d.method === "Runtime.exceptionThrown") exceptions.push(d.params.exceptionDetails.exception?.description ?? d.params.exceptionDetails.text);
  };
  const call = (method, params = {}) => new Promise((r) => { const n = ++id; waiting.set(n, r); ws.send(JSON.stringify({ id: n, method, params })); });
  const run = async (expression) => { const r = await call("Runtime.evaluate", { expression, awaitPromise: true, returnByValue: true }); if (r.result?.exceptionDetails) throw new Error(r.result.exceptionDetails.exception?.description ?? "Skriptfehler"); return r.result?.result?.value; };
  const until = async (expression, ms = 8000) => { for (let i = 0; i < ms / 50; i++) { if (await run(expression)) return true; await sleep(50); } return false; };

  await call("Page.enable"); await call("Runtime.enable"); await call("Emulation.setFocusEmulationEnabled", { enabled: true });
  await call("Page.addScriptToEvaluateOnNewDocument", { source: "window.__m=[];window.invokeCSharpAction=(x)=>window.__m.push(JSON.parse(x));" });
  await call("Page.navigate", { url: site });
  check("bereit gemeldet", await until("window.__m && window.__m.some(x=>x.art==='bereit')"));
  const page = { root: { props: { title: "Café", description: "x", ansicht: "handy" } }, content: [
    { type: "Kopfbereich", props: { id: "held", title: "Guter Kaffee", sub: "Täglich frisch", cta: "Los", href: "#", motion: "keine", seconds: 1 } },
    { type: "Text", props: { id: "t1", text: "Unsere Bohnen", align: "left" } },
    { type: "Bereich", props: { id: "b", tone: "hell", content: [{ type: "Knopf", props: { id: "kn", label: "Innen", href: "#" } }] } }], zones: {} };
  await run(`window.trendoLoad(${JSON.stringify(page)})`);
  check("KI-Daten öffnen sich im Editor", await until(`document.body.innerText.includes('Guter Kaffee')`));
  check("Knopf Auswählen da", await until(`[...document.querySelectorAll('button')].some(b=>b.textContent==='Auswählen')`));
  await run(`[...document.querySelectorAll('button')].find(b=>b.textContent==='Auswählen').click()`);
  check("Bausteine anklickbar", await until(`document.querySelectorAll('[data-tid]').length===4`));

  await run(`document.querySelector('[data-tid="t1"]').click()`);
  check("Auswahl gemeldet", await until(`JSON.stringify(window.__m.filter(x=>x.art==='auswahl').at(-1)?.daten.ids)==='["t1"]'`));
  check("Auswahl sichtbar", await until(`document.querySelector('[data-tid="t1"]').classList.contains('t-sel-on')`));
  await run(`document.querySelector('[data-tid="kn"] a').click()`);
  check("Mehrfachauswahl, innerstes Element", await until(`JSON.stringify(window.__m.filter(x=>x.art==='auswahl').at(-1)?.daten.ids)==='["t1","kn"]'`));
  check("Link führt beim Auswählen nirgends hin", await run(`!location.hash`));
  await run(`document.querySelector('[data-tid="kn"] a').click()`);
  check("Abwählen", await until(`JSON.stringify(window.__m.filter(x=>x.art==='auswahl').at(-1)?.daten.ids)==='["t1"]'`));

  const edit = (sel, text) => `(()=>{const el=document.querySelector('${sel}');el.dispatchEvent(new MouseEvent('dblclick',{bubbles:true}));if(!el.isContentEditable)return false;el.textContent=${JSON.stringify(text)};el.blur();return true;})()`;
  check("Absatz wird direkt bearbeitbar", await run(edit('[data-tid="t1"] p', "Beste Bohnen")));
  check("Text geht als Aktion an Trendo", await until(`(()=>{const a=window.__m.filter(x=>x.art==='aktion').at(-1)?.daten.aktionen[0];return a&&a.aktion==='element_aendern'&&a.id==='t1'&&a.props.text==='Beste Bohnen';})()`));
  check("Titel im Kopfbereich trifft das richtige Feld", await run(edit('[data-tid="held"] h1', "Neu")) && await until(`window.__m.filter(x=>x.art==='aktion').at(-1)?.daten.aktionen[0].props.title==='Neu'`));
  const count = await run(`window.__m.filter(x=>x.art==='aktion').length`);
  const textBefore = await run(`document.querySelector('[data-tid="t1"] p').textContent`);
  await run(`(()=>{const el=document.querySelector('[data-tid="t1"] p');el.dispatchEvent(new MouseEvent('dblclick',{bubbles:true}));el.textContent='Weg';el.dispatchEvent(new KeyboardEvent('keydown',{key:'Escape',bubbles:true}));})()`);
  await sleep(100);
  check("Escape verwirft ohne Aktion", (await run(`window.__m.filter(x=>x.art==='aktion').length`)) === count && (await run(`document.querySelector('[data-tid="t1"] p').textContent`)) === textBefore);

  page.content[1].props.text = "Bestätigt";
  await run(`window.trendoLoad(${JSON.stringify(page)})`);
  check("Nach dem Speichern steht der neue Text da", await until(`document.querySelector('[data-tid="t1"] p')?.textContent==='Bestätigt'`));
  await run(`window.trendoVorschau(${JSON.stringify({ ...page, content: [{ type: "Text", props: { id: "z", text: "Vorschlag der KI", align: "left" } }] })})`);
  check("KI-Vorschau wird angezeigt", await until(`document.body.innerText.includes('Vorschlag der KI') && document.body.innerText.includes('noch nicht übernommen')`));
  await run(`window.trendoVorschau(null)`);
  check("Verwerfen zeigt wieder die Seite", await until(`document.body.innerText.includes('Bestätigt') && !document.body.innerText.includes('Vorschlag der KI')`));
  await run(`[...document.querySelectorAll('button')].find(b=>b.textContent.includes('Zurück zum Editor')).click()`);
  check("Zurück in den Editor", await until(`[...document.querySelectorAll('button')].some(b=>b.textContent==='Auswählen')`));
  check("keine Skriptfehler", exceptions.length === 0, exceptions.slice(0, 3));
} catch (e) { problems.push(String(e?.message ?? e)); }
finally {
  try { ws?.close(); } catch { /* schon zu */ }
  browser.kill(); server.close();
  await sleep(300);
  try { rmSync(profile, { recursive: true, force: true }); } catch { /* Chrome hält die Datei noch */ }
}
process.stdout.write(JSON.stringify({ results, problems }));
process.exit(problems.length ? 1 : 0);
