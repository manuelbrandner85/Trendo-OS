// Prüft Stufe 2, Teil 2 in einem echten Chrome: Komponenten (W5), Profi-Bausteine mit axe-core je Baustein und Tastatur (Z1), Bewegung mit „weniger Bewegung“ und Ersatzweg (W4), Zeitleiste (Z2).
// Aufruf: node pruefe-teil2.mjs <chrome> <Ordner des Bündels> <Seitendaten.json> <Export-Ordner> <axe.min.js oder leer>
// Ausgabe: JSON mit den Ergebnissen; Fehler ergeben Exitcode 1.
import { spawn } from "node:child_process";
import { createServer } from "node:http";
import { mkdtempSync, readFileSync, existsSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join, extname, normalize } from "node:path";

const [chrome, bundle, pageFile, exportDir, axeFile] = process.argv.slice(2);
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const types = { ".html": "text/html; charset=utf-8", ".js": "text/javascript; charset=utf-8", ".css": "text/css; charset=utf-8", ".png": "image/png" };
const serve = (root) => createServer((req, res) => {
  const rel = normalize(decodeURIComponent(new URL(req.url, "http://x").pathname.slice(1)) || "index.html");
  const file = join(root, rel);
  if (rel.startsWith("..") || !existsSync(file)) { res.writeHead(404); res.end(); return; }
  res.writeHead(200, { "content-type": types[extname(file)] ?? "application/octet-stream" }); res.end(readFileSync(file));
});
const servers = [serve(bundle), serve(exportDir)];
for (const s of servers) await new Promise((r) => s.listen(0, "127.0.0.1", r));
const editorUrl = `http://127.0.0.1:${servers[0].address().port}/index.html?ohneRahmen`;
const exportUrl = `http://127.0.0.1:${servers[1].address().port}/index.html`;
const profile = mkdtempSync(join(tmpdir(), "trendo-chrome-teil2-"));
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
  const key = async (name, code) => { await call("Input.dispatchKeyEvent", { type: "keyDown", key: name, code: name, windowsVirtualKeyCode: code, text: name === "Enter" ? "\r" : "" }); await call("Input.dispatchKeyEvent", { type: "keyUp", key: name, code: name, windowsVirtualKeyCode: code }); };
  const page = readFileSync(pageFile, "utf8");
  let script = "window.__m=[];window.invokeCSharpAction=(x)=>window.__m.push(JSON.parse(x));";
  let scriptId = (await call("Page.addScriptToEvaluateOnNewDocument", { source: script })).result.identifier;
  await call("Page.enable"); await call("Runtime.enable");
  const oeffne = async (url, ohneIO = false, wenigerBewegung = false) => {
    await call("Page.removeScriptToEvaluateOnNewDocument", { identifier: scriptId });
    scriptId = (await call("Page.addScriptToEvaluateOnNewDocument", { source: script + (ohneIO ? "delete window.IntersectionObserver;" : "") })).result.identifier;
    await call("Emulation.setEmulatedMedia", { features: [{ name: "prefers-reduced-motion", value: wenigerBewegung ? "reduce" : "no-preference" }] });
    await call("Page.navigate", { url });
    if (url === editorUrl) {
      await until("window.__m && window.__m.some(x=>x.art==='bereit')");
      await run(`window.trendoLoad(${JSON.stringify(page)})`);
      await until(`[...document.querySelectorAll('button')].some(b=>b.textContent==='Vorschau')`);
      await run(`[...document.querySelectorAll('button')].find(b=>b.textContent==='Vorschau').click()`);
    }
    await until(`!!document.querySelector('[data-b="fuss"]')`);
    await sleep(200);
  };

  for (const [wo, url] of [["Editor-Vorschau", editorUrl], ["Export", exportUrl]]) {
    await oeffne(url);
    // W5
    check(`${wo}: beide Verwendungen der Komponente gezeichnet`, await until(`!!document.querySelector('[data-b="k1--u"]') && !!document.querySelector('[data-b="k2--u"]')`));
    check(`${wo}: Original in Verwendung 1`, (await run(`document.querySelector('[data-b="k1--u"]').textContent`)) === "Paket Basis");
    check(`${wo}: Überschreibung nur in Verwendung 2`, (await run(`document.querySelector('[data-b="k2--u"]').textContent`)) === "Paket Profi" && (await run(`document.querySelector('[data-b="k2--t"]').textContent`)) === "Für den Anfang");
    // Z1: Bausteine folgen den Gestaltungswerten
    check(`${wo}: Preise nutzen die Akzentfarbe`, (await run(`getComputedStyle(document.querySelector('[data-b="preise"] .t-tarif li svg')).color`)) === "rgb(122, 31, 43)");
    await run(`document.querySelector('.t-page').style.setProperty('--t-accent', '#0055aa')`);
    check(`${wo}: Gestaltungswert ändern – Baustein folgt`, (await run(`getComputedStyle(document.querySelector('[data-b="preise"] .t-tarif li svg')).color`)) === "rgb(0, 85, 170)");
    await run(`document.querySelector('.t-page').style.setProperty('--t-accent', '#7a1f2b')`);
    // Z1: Tastatur
    await run(`document.querySelector('[data-b="fragen"] summary').focus()`);
    await key("Enter", 13);
    check(`${wo}: Häufige Fragen mit der Tastatur aufklappbar`, await until(`document.querySelector('[data-b="fragen"] details').open`));
    check(`${wo}: Formularfelder beschriftet`, await run(`[...document.querySelectorAll('[data-b="formular"] input, [data-b="formular"] textarea')].every(e=>e.labels && e.labels.length===1)`));
    // Z1: axe-core je Baustein (im echten Browser, mit Farbkontrast)
    if (axeFile) {
      await run(readFileSync(axeFile, "utf8") + ";true");
      for (const b of ["nav", "hero", "preise", "fragen", "galerie", "formular", "fuss"]) {
        // erst ganz zeigen und Bewegungen enden lassen: geprüft wird der ruhende Zustand
        await run(`document.querySelector('[data-b="${b}"]').scrollIntoView({block:"end"})`);
        await until(`document.querySelector('[data-b="${b}"]').getAnimations().every(a=>a.playState!=="running")`, 4000); await sleep(150);
        const v = await run(`axe.run(document.querySelector('[data-b="${b}"]'), { resultTypes: ['violations'] }).then(r => r.violations.filter(x => x.impact === 'critical' || x.impact === 'serious').map(x => x.id + ':' + x.nodes.length))`);
        check(`${wo}: axe ohne schweren Befund – ${b}`, Array.isArray(v) && v.length === 0, v);
      }
    }
  }

  // W4: Bewegung läuft, „weniger Bewegung“ stoppt sie, ohne IntersectionObserver bleibt alles sichtbar
  await oeffne(exportUrl);
  await run(`document.querySelector('[data-b="preise"]').scrollIntoView()`);
  check("Export: Erscheinen-Bewegung läuft", await until(`document.querySelector('[data-b="preise"]').getAnimations().length > 0`));
  check("Export: nach der Bewegung voll sichtbar", await until(`getComputedStyle(document.querySelector('[data-b="preise"]')).opacity === '1'`, 4000));
  await oeffne(exportUrl, false, true);
  await run(`window.scrollTo(0, document.body.scrollHeight)`); await sleep(400);
  check("Export: bei „weniger Bewegung“ läuft keine Animation", (await run(`document.getAnimations().length`)) === 0, await run(`document.getAnimations().length`));
  check("Export: bei „weniger Bewegung“ alles sichtbar", await run(`[...document.querySelectorAll('[data-b]')].every(e=>getComputedStyle(e).opacity==='1')`));
  check("Export: Ersatzweg im Stil – Darüberfahren nur ohne „weniger Bewegung“", await run(`[...document.styleSheets].some(s=>{try{return [...s.cssRules].some(r=>r.conditionText&&r.conditionText.includes('prefers-reduced-motion: no-preference')&&r.cssText.includes('t-hov-anheben'))}catch{return false}})`));
  await oeffne(exportUrl, true, false);
  await run(`window.scrollTo(0, document.body.scrollHeight)`); await sleep(300);
  check("Export: ohne IntersectionObserver bleibt der Inhalt sichtbar", await run(`getComputedStyle(document.querySelector('[data-b="preise"]')).opacity === '1'`));

  // Z2: Zeitleiste spielt im Export ohne Editor
  await oeffne(exportUrl);
  await run(`window.scrollTo(0,0)`);
  check("Export: Zeitleiste läuft", await run(`document.querySelector('[data-b="hero"]').getAnimations().length > 0`));
  check("Export: Zeitleiste erreicht das letzte Schlüsselbild (y = -20 px)", await until(`getComputedStyle(document.querySelector('[data-b="hero"]')).transform === 'matrix(1, 0, 0, 1, 0, -20)'`, 3000), await run(`getComputedStyle(document.querySelector('[data-b="hero"]')).transform`));
  const vorher = parseFloat(await run(`getComputedStyle(document.querySelector('[data-b="fuss"]')).opacity`));
  await run(`window.scrollTo(0, document.body.scrollHeight)`); await sleep(300);
  const nachher = parseFloat(await run(`getComputedStyle(document.querySelector('[data-b="fuss"]')).opacity`));
  check("Export: Zeitleiste folgt dem Scrollen", nachher > vorher && Math.abs(vorher - 0.2) < 0.21, [vorher, nachher]);
  check("keine Skriptfehler", exceptions.length === 0, exceptions.slice(0, 3));
} catch (e) { problems.push(String(e?.message ?? e)); }
finally {
  try { ws?.close(); } catch { /* schon zu */ }
  browser.kill(); for (const s of servers) s.close();
  await sleep(300);
  try { rmSync(profile, { recursive: true, force: true }); } catch { /* Chrome hält die Datei noch */ }
}
process.stdout.write(JSON.stringify({ results, problems }));
process.exit(problems.length ? 1 : 0);
