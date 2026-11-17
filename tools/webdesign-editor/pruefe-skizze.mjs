// Prüft Stufe 3, Teil 2 im Editor in echtem Chrome: HTML-Block zeigt nur erlaubte Elemente (K6/Z8), Skizze mit der Maus zeichnen und an Trendo geben (K3).
// Aufruf: node pruefe-skizze.mjs <chrome> <Ordner des Bündels>
import { spawn } from "node:child_process";
import { createServer } from "node:http";
import { mkdtempSync, readFileSync, existsSync, rmSync, statSync } from "node:fs";
import { tmpdir } from "node:os";
import { join, extname, normalize } from "node:path";

const [chrome, bundle] = process.argv.slice(2);
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const types = { ".html": "text/html; charset=utf-8", ".js": "text/javascript; charset=utf-8", ".css": "text/css; charset=utf-8" };
const server = createServer((req, res) => {
  const rel = normalize(decodeURIComponent(new URL(req.url, "http://x").pathname.slice(1)) || "index.html");
  const file = join(bundle, rel);
  if (rel.startsWith("..") || !existsSync(file) || statSync(file).isDirectory()) { res.writeHead(404); res.end(); return; }
  res.writeHead(200, { "content-type": types[extname(file)] ?? "application/octet-stream" }); res.end(readFileSync(file));
});
await new Promise((r) => server.listen(0, "127.0.0.1", r));
const url = `http://127.0.0.1:${server.address().port}/index.html?ohneRahmen`;
const profile = mkdtempSync(join(tmpdir(), "trendo-chrome-skizze-"));
const port = 20000 + Math.floor(Math.random() * 30000);
const browser = spawn(chrome, ["--headless=new", "--no-sandbox", "--no-first-run", "--disable-extensions", "--remote-allow-origins=*", `--remote-debugging-port=${port}`, `--user-data-dir=${profile}`, "--window-size=1280,900", "about:blank"], { stdio: "ignore" });
const page = { root: { props: { title: "Skizze" } }, content: [
  { type: "Ueberschrift", props: { id: "h", text: "Willkommen", level: "h2" } },
  { type: "Html", props: { id: "x", knoten: [{ tag: "table", attrs: {}, kinder: [{ tag: "tbody", attrs: {}, kinder: [{ tag: "tr", attrs: {}, kinder: [{ tag: "td", attrs: { colspan: "2", onclick: "alert(1)" }, kinder: [{ text: "Zelle" }] }] }] }] },
    { tag: "script", attrs: {}, kinder: [{ text: "window.__boese=1" }] }, { tag: "a", attrs: { href: "javascript:alert(2)" }, kinder: [{ text: "Link" }] }] } }], zones: {} };

const results = {}; const problems = []; let ws;
const check = (name, ok, detail) => { results[name] = ok; if (!ok) problems.push(name + (detail === undefined ? "" : ": " + JSON.stringify(detail))); };
try {
  let target;
  for (let i = 0; i < 200 && !target; i++) { try { target = (await (await fetch(`http://127.0.0.1:${port}/json/list`)).json()).find((t) => t.type === "page"); } catch { } if (!target) await sleep(150); }
  ws = new WebSocket(target.webSocketDebuggerUrl); await new Promise((r, e) => { ws.onopen = r; ws.onerror = e; });
  let id = 0; const waiting = new Map(); const exceptions = [];
  ws.onmessage = (m) => { const d = JSON.parse(m.data); if (d.id && waiting.has(d.id)) { waiting.get(d.id)(d); waiting.delete(d.id); } else if (d.method === "Runtime.exceptionThrown") exceptions.push(d.params.exceptionDetails.exception?.description ?? d.params.exceptionDetails.text); };
  const call = (method, params = {}) => new Promise((r) => { const n = ++id; waiting.set(n, r); ws.send(JSON.stringify({ id: n, method, params })); });
  const run = async (expression) => { const r = await call("Runtime.evaluate", { expression, awaitPromise: true, returnByValue: true }); if (r.result?.exceptionDetails) throw new Error(r.result.exceptionDetails.exception?.description ?? "Skriptfehler"); return r.result?.result?.value; };
  const until = async (expression, ms = 20000) => { for (let i = 0; i < ms / 50; i++) { if (await run(expression).catch(() => false)) return true; await sleep(50); } return false; };
  await call("Page.addScriptToEvaluateOnNewDocument", { source: "window.__m=[];window.invokeCSharpAction=(x)=>window.__m.push(JSON.parse(x));" });
  await call("Page.enable"); await call("Runtime.enable");
  await call("Page.navigate", { url });
  await until("window.__m && window.__m.some(x=>x.art==='bereit')");
  await run(`window.trendoLoad(${JSON.stringify(JSON.stringify(page))})`);
  await until(`[...document.querySelectorAll('button')].some(b=>b.textContent==='Skizze')`);
  check("HTML-Block: Tabelle wird gezeigt, colspan bleibt", await until(`!!document.querySelector('.t-html td[colspan="2"]')`));
  check("HTML-Block: kein onclick, kein Skript, kein javascript:-Link", await run(`!document.querySelector('.t-html [onclick]') && !document.querySelector('.t-html script') && !window.__boese && ![...document.querySelectorAll('.t-html a')].some(a=>(a.getAttribute('href')||'').startsWith('javascript'))`));
  await run(`[...document.querySelectorAll('button')].find(b=>b.textContent==='Skizze').click()`);
  check("Skizze: Zeichenfläche über der Seite", await until(`!!document.querySelector('.t-skizze-flaeche') && document.querySelector('.t-skizze-flaeche').width > 100`));
  const box = await run(`(()=>{const r=document.querySelector('.t-skizze-flaeche').getBoundingClientRect();return [r.left+40,r.top+40]})()`);
  await call("Input.dispatchMouseEvent", { type: "mouseMoved", x: box[0], y: box[1] });
  await call("Input.dispatchMouseEvent", { type: "mousePressed", x: box[0], y: box[1], button: "left", clickCount: 1 });
  for (let i = 1; i <= 10; i++) await call("Input.dispatchMouseEvent", { type: "mouseMoved", x: box[0] + i * 15, y: box[1] + i * 5, button: "left", buttons: 1 });
  await call("Input.dispatchMouseEvent", { type: "mouseReleased", x: box[0] + 150, y: box[1] + 50, button: "left", clickCount: 1 });
  check("Skizze: Striche liegen auf der Fläche", await run(`(()=>{const c=document.querySelector('.t-skizze-flaeche');const d=c.getContext('2d').getImageData(0,0,c.width,Math.min(c.height,200)).data;for(let i=3;i<d.length;i+=4) if(d[i]>0) return true;return false})()`));
  await run(`[...document.querySelectorAll('button')].find(b=>b.textContent==='An Trendo geben').click()`);
  check("Skizze: Bild geht als PNG an Trendo", await until(`window.__m.some(x=>x.art==='skizze' && x.daten.bild.startsWith('data:image/png;base64,') && x.daten.breite>100 && x.daten.hoehe>50)`));
  check("Skizze: zurück im Editor", await until(`[...document.querySelectorAll('button')].some(b=>b.textContent==='Skizze')`));
  check("keine Skriptfehler", exceptions.length === 0, exceptions.slice(0, 3));
} catch (e) { problems.push(String(e?.message ?? e)); }
finally {
  try { ws?.close(); } catch { }
  browser.kill(); server.close(); await sleep(300);
  try { rmSync(profile, { recursive: true, force: true, maxRetries: 25, retryDelay: 200 }) /* T-0729: Chrome-Hilfsprozesse geben Dateien erst nach Sekunden frei */; } catch { }
}
process.stdout.write(JSON.stringify({ results, problems }));
process.exit(problems.length ? 1 : 0);
