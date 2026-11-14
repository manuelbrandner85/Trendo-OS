// Prüft Stufe 2, Teil 1 in einem echten Chrome: Anpassungen je Ansicht (W1), Spalten nach eigener Breite (Z4), dunkler Satz (W6), Stapel (A5),
// im Editor-Bündel und im Export von Trendo. Aufruf: node pruefe-gestaltung.mjs <chrome> <Ordner des Bündels> <Seitendaten.json> <Export-Ordner>
// Ausgabe: JSON mit den Ergebnissen; Fehler ergeben Exitcode 1.
import { spawn } from "node:child_process";
import { createServer } from "node:http";
import { mkdtempSync, readFileSync, existsSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join, extname, normalize } from "node:path";

const [chrome, bundle, pageFile, exportDir] = process.argv.slice(2);
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
const profile = mkdtempSync(join(tmpdir(), "trendo-chrome-gestaltung-"));
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
  const width = async (w) => { await call("Emulation.setDeviceMetricsOverride", { width: w, height: 900, deviceScaleFactor: 1, mobile: false }); await sleep(150); };
  const style = (sel, prop) => `(()=>{const e=document.querySelector('${sel}');return e?getComputedStyle(e).getPropertyValue('${prop}').trim():null;})()`;
  const cols = (sel) => `(()=>{const e=document.querySelector('${sel}');return e?getComputedStyle(e).gridTemplateColumns.split(' ').length:0;})()`;
  const overflow = `Math.max(document.documentElement.scrollWidth, document.body.scrollWidth) - window.innerWidth`;

  await call("Page.enable"); await call("Runtime.enable");
  await call("Page.addScriptToEvaluateOnNewDocument", { source: "window.__m=[];window.invokeCSharpAction=(x)=>window.__m.push(JSON.parse(x));window.__sichtbar=(s)=>[...document.querySelectorAll(s)].find(e=>e.getClientRects().length>0);" });

  // Beide Wege prüfen: Vorschau im Editor (Container-Regeln) und Export (Medienregeln von Trendo)
  const pruefe = async (wo, url, vorbereiten) => {
    await width(1440);
    await call("Page.navigate", { url });
    await vorbereiten();
    check(`${wo}: Seite gezeichnet`, await until(`!!document.querySelector('[data-b="t1"] p')`));
    check(`${wo}: Kopfbereich auf Desktop mittig`, (await run(style('[data-b="held"] .t-hero', "text-align"))) === "center");
    check(`${wo}: Desktop bleibt links`, ["left", "start"].includes(await run(style('[data-b="t1"] p', "text-align"))));
    const base = parseFloat(await run(style('[data-b="t1"] p', "font-size")));
    await width(900);
    check(`${wo}: Tablet-Anpassung wirkt`, (await run(style('[data-b="t1"] p', "text-align"))) === "center");
    check(`${wo}: Ausrichtung wirkt auch im Kopfbereich`, ["left", "start"].includes(await run(style('[data-b="held"] .t-hero', "text-align"))), await run(style('[data-b="held"] .t-hero', "text-align")));
    check(`${wo}: Tablet behält die Grundschrift`, parseFloat(await run(style('[data-b="t1"] p', "font-size"))) === base);
    await width(500);
    check(`${wo}: Handy erbt von Tablet`, (await run(style('[data-b="t1"] p', "text-align"))) === "center");
    check(`${wo}: Handy-Schrift 150 %`, Math.abs(parseFloat(await run(style('[data-b="t1"] p', "font-size"))) - base * 1.5) < 0.6, [base, await run(style('[data-b="t1"] p', "font-size"))]);
    await width(1440);
    check(`${wo}: zurück auf Desktop wirkt nichts nach oben`, ["left", "start"].includes(await run(style('[data-b="t1"] p', "text-align"))));
    check(`${wo}: äußere Spalten nebeneinander`, (await run(cols('[data-b="aussen"]>.t-cols-box>.t-cols'))) === 2);
    check(`${wo}: mittlere Spalten nebeneinander`, (await run(cols('[data-b="mitte"]>.t-cols-box>.t-cols'))) === 2);
    check(`${wo}: innere Spalten stehen in ihrer schmalen Spalte untereinander`, (await run(cols('[data-b="innen"]>.t-cols-box>.t-cols'))) === 1);
    check(`${wo}: Stapel-Abstand aus dem Gestaltungswert (24 px × 0,5)`, (await run(style('[data-b="stapel"] .t-stack', "column-gap"))) === "12px");
    await width(390);
    check(`${wo}: Stapel bleibt bei 390 px im Rahmen`, (await run(overflow)) <= 1, await run(overflow));
    check(`${wo}: Stapel-Box liegt innerhalb der Seite`, await run(`(()=>{const s=document.querySelector('[data-b="stapel"] .t-stack').getBoundingClientRect(),p=document.querySelector('.t-page').getBoundingClientRect();return s.left>=p.left-0.5&&s.right<=p.right+0.5;})()`));
    await call("Emulation.setEmulatedMedia", { features: [{ name: "prefers-color-scheme", value: "dark" }] });
    check(`${wo}: dunkler Satz bei dunklem Gerät`, (await run(style(".t-page", "background-color"))) === "rgb(18, 18, 18)", await run(style(".t-page", "background-color")));
    await call("Emulation.setEmulatedMedia", { features: [{ name: "prefers-color-scheme", value: "light" }] });
    check(`${wo}: heller Satz bei hellem Gerät`, (await run(style(".t-page", "background-color"))) === "rgb(255, 255, 255)");
  };

  const page = readFileSync(pageFile, "utf8");
  await pruefe("Editor-Vorschau", editorUrl, async () => {
    await until("window.__m && window.__m.some(x=>x.art==='bereit')");
    await run(`window.trendoLoad(${JSON.stringify(page)})`);
    await until(`[...document.querySelectorAll('button')].some(b=>b.textContent==='Vorschau')`);
    await run(`[...document.querySelectorAll('button')].find(b=>b.textContent==='Vorschau').click()`);
  });
  await pruefe("Export", exportUrl, async () => { await until("document.documentElement.dataset.fertig==='1'"); });
  check("Export hat Medienregeln", await run(`(document.getElementById('t-anpassungen')?.textContent||'').includes('@media (max-width: 1024px)')`));

  // Editor: Vorschau je Ansicht ohne Rahmen (wie in Trendo) und das Feld „Anpassungen je Ansicht“ mit echter Maus
  const center = async (sel) => run(`(()=>{const el=window.__sichtbar('${sel}');el.scrollIntoView({block:'center'});const r=el.getBoundingClientRect();return {x:r.left+r.width/2,y:r.top+r.height/2};})()`);
  const mouseClick = async (sel) => {
    const p = await center(sel);
    await call("Input.dispatchMouseEvent", { type: "mouseMoved", x: p.x, y: p.y });
    await call("Input.dispatchMouseEvent", { type: "mousePressed", x: p.x, y: p.y, button: "left", buttons: 1, clickCount: 1 });
    await call("Input.dispatchMouseEvent", { type: "mouseReleased", x: p.x, y: p.y, button: "left", buttons: 0, clickCount: 1 });
  };
  const knopf = (text) => `[...document.querySelectorAll('button')].find(b=>b.textContent===${JSON.stringify(text)})`;
  const waehle = (wert) => run(`(()=>{const s=window.__sichtbar('select.t-anp-ansicht');const set=Object.getOwnPropertyDescriptor(HTMLSelectElement.prototype,'value').set;set.call(s,${JSON.stringify(wert)});s.dispatchEvent(new Event('change',{bubbles:true}));})()`);
  await width(1440);
  await call("Page.navigate", { url: editorUrl });
  await until("window.__m && window.__m.some(x=>x.art==='bereit')");
  await run(`window.trendoLoad(${JSON.stringify(page)})`);
  await until(`!!${knopf("Vorschau")}`);
  await run(`${knopf("Vorschau")}.click()`);
  check("Vorschau: Knöpfe je Ansicht", await until(`['Desktop','Tablet','Handy'].every(n=>[...document.querySelectorAll('button')].some(b=>b.textContent===n))`));
  await run(`${knopf("Tablet")}.click()`);
  check("Vorschau: Tablet-Breite zeigt Tablet-Anpassung", await until(`getComputedStyle(document.querySelector('[data-b="t1"] p')).textAlign==='center'`));
  await run(`${knopf("Desktop")}.click()`);
  check("Vorschau: Desktop-Breite ohne Anpassung", await until(`['left','start'].includes(getComputedStyle(document.querySelector('[data-b="t1"] p')).textAlign)`));
  await run(`${knopf("← Zurück zum Editor")}.click()`);
  await until(`!!document.querySelector('[data-b="t1"] p')`);
  await mouseClick('[data-b="t1"] p');
  check("Editor: Feld Anpassungen erscheint", await until(`!!window.__sichtbar('.t-anp') && window.__sichtbar('.t-anp').innerText.includes('Anpassungen je Ansicht')`));
  check("Editor: Desktop als Grundlage mit Liste der Überschreibungen", await until(`document.body.innerText.includes('ist die Grundlage') && document.body.innerText.includes('Überschrieben: Tablet: ausrichtung center')`));
  await waehle("Tablet");
  check("Editor: Tablet zeigt die Überschreibung mit Zurücksetzen", await until(`!!window.__sichtbar('.t-anp-ueber') && window.__sichtbar('.t-anp').querySelectorAll('.t-anp-reset').length===1`));
  await waehle("Handy");
  check("Editor: Handy zeigt geerbten Wert und eigene Schrift", await until(`window.__sichtbar('.t-anp').innerText.includes('geerbt aus Tablet: center') && window.__sichtbar('.t-anp').querySelectorAll('.t-anp-reset').length===1`), await run(`window.__sichtbar('.t-anp')?.innerText`));
  await waehle("Tablet");
  await until(`window.__sichtbar('.t-anp').querySelectorAll('.t-anp-reset').length===1`);
  await mouseClick('.t-anp-reset');
  check("Editor: Zurücksetzen entfernt nur die Tablet-Anpassung", await until(`(()=>{const e=window.__m.filter(x=>x.art==='entwurf').at(-1);if(!e)return false;const b=e.daten.content.find(x=>x.props.id==='t1');const t=JSON.stringify(b.props.anpassungen||{});return !t.includes('"Tablet"') && t.includes('"Handy":{"schrift":150}');})()`, 4000), await run(`[[...document.querySelectorAll('.t-anp')].map(e=>(e.closest('[class*="Field"],[class*="field"]')?.className||'?')+' :: '+e.innerText.slice(0,60)), window.__m.filter(x=>x.art==='entwurf').length, JSON.stringify(window.__m.filter(x=>x.art==='entwurf').at(-1)?.daten?.content?.map(b=>[b.props.id,b.props.anpassungen]))]`));
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
