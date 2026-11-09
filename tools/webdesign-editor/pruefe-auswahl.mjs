// Prüft das gebaute Editor-Bündel in einem echten Chrome: Auswählen in der Vorschau, Text direkt ändern, KI-Vorschau.
// Aufruf: node pruefe-auswahl.mjs <chrome> <Ordner des Bündels> <Seitendaten.json mit den Bausteinen held, t1, b, kn>. Ausgabe: JSON mit den Ergebnissen; Fehler ergeben Exitcode 1.
import { spawn } from "node:child_process";
import { createServer } from "node:http";
import { mkdtempSync, readFileSync, existsSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join, extname, normalize } from "node:path";

const [chrome, bundle, pageFile] = process.argv.slice(2);
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
  const page = JSON.parse(readFileSync(pageFile, "utf8"));
  await run(`window.trendoLoad(${JSON.stringify(page)})`);
  check("KI-Daten öffnen sich im Editor", await until(`document.body.innerText.includes('Guter Kaffee')`));
  check("Knopf Auswählen da", await until(`[...document.querySelectorAll('button')].some(b=>b.textContent==='Auswählen')`));
  await run(`[...document.querySelectorAll('button')].find(b=>b.textContent==='Auswählen').click()`);
  check("Bausteine anklickbar", await until(`document.querySelectorAll('[data-tid]').length===4`));

  // Echte Mausereignisse: So bedient ein Mensch die Vorschau; ein künstliches dblclick verdeckt Fehler
  const center = async (sel) => run(`(()=>{const el=document.querySelector('${sel}');el.scrollIntoView({block:'center'});const r=el.getBoundingClientRect();return {x:r.left+r.width/2,y:r.top+r.height/2};})()`);
  const mouseClick = async (sel, count = 1) => {
    const p = await center(sel);
    await call("Input.dispatchMouseEvent", { type: "mouseMoved", x: p.x, y: p.y });
    for (let c = 1; c <= count; c++) {
      await call("Input.dispatchMouseEvent", { type: "mousePressed", x: p.x, y: p.y, button: "left", buttons: 1, clickCount: c });
      await call("Input.dispatchMouseEvent", { type: "mouseReleased", x: p.x, y: p.y, button: "left", buttons: 0, clickCount: c });
    }
  };
  const key = async (name, code) => { await call("Input.dispatchKeyEvent", { type: "rawKeyDown", key: name, code: name, windowsVirtualKeyCode: code }); await call("Input.dispatchKeyEvent", { type: "keyUp", key: name, code: name, windowsVirtualKeyCode: code }); };
  const lastIds = `JSON.stringify(window.__m.filter(x=>x.art==='auswahl').at(-1)?.daten.ids)`;
  const lastAction = `window.__m.filter(x=>x.art==='aktion').at(-1)?.daten.aktionen[0]`;

  await run(`window.__knoten=document.querySelector('[data-tid="t1"] p')`);
  await mouseClick('[data-tid="t1"] p');
  check("Auswahl gemeldet", await until(`${lastIds}==='["t1"]'`));
  check("Auswahl sichtbar", await until(`document.querySelector('[data-tid="t1"]').classList.contains('t-sel-on')`));
  check("Auswählen ersetzt die Elemente nicht", await run(`window.__knoten===document.querySelector('[data-tid="t1"] p')`));
  await mouseClick('[data-tid="kn"] a');
  check("Mehrfachauswahl, innerstes Element", await until(`${lastIds}==='["t1","kn"]'`));
  check("Link führt beim Auswählen nirgends hin", await run(`!location.hash`));
  await mouseClick('[data-tid="kn"] a');
  check("Abwählen", await until(`${lastIds}==='["t1"]'`));
  await mouseClick('[data-tid="kn"] a'); await mouseClick('[data-tid="held"] p');
  check("Zwei schnelle Klicks auf verschiedene Bausteine zählen beide", await until(`${lastIds}==='["t1","kn","held"]'`));
  await mouseClick('[data-tid="kn"] a'); await mouseClick('[data-tid="held"] p');
  check("Schnelles Abwählen", await until(`${lastIds}==='["t1"]'`));

  await mouseClick('[data-tid="t1"] p', 2);
  check("Doppelklick macht den Text bearbeitbar", await until(`document.querySelector('[data-tid="t1"] p').isContentEditable`));
  await sleep(320);
  check("Doppelklick ändert die Auswahl nicht", (await run(lastIds)) === '["t1"]');
  await call("Input.insertText", { text: "Neu" });
  await key("Enter", 13);
  check("Enter speichert als Aktion", await until(`(()=>{const a=${lastAction};return a&&a.aktion==='element_aendern'&&a.id==='t1'&&a.props.text.includes('Neu');})()`));
  await mouseClick('[data-tid="held"] h1', 2);
  check("Titel wird bearbeitbar", await until(`document.querySelector('[data-tid="held"] h1').isContentEditable`));
  await call("Input.insertText", { text: "Anders" });
  await mouseClick('.t-seltools span');
  check("Klick daneben speichert das richtige Feld", await until(`(${lastAction})?.props.title?.includes('Anders')`));
  const count = await run(`window.__m.filter(x=>x.art==='aktion').length`);
  const textBefore = await run(`document.querySelector('[data-tid="t1"] p').textContent`);
  await mouseClick('[data-tid="t1"] p', 2);
  await until(`document.querySelector('[data-tid="t1"] p').isContentEditable`);
  await call("Input.insertText", { text: "Weg" });
  await key("Escape", 27);
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
