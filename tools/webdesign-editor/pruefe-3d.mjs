// Prüft Z3 in einem echten Chrome am fertigen Export: Modell erscheint (WebGPU, wenn möglich), ohne WebGPU läuft WebGL, ohne 3D erscheint das Standbild,
// Gaussian Splat erscheint; misst die Bildrate und speichert Bilder. Aufruf:
// node pruefe-3d.mjs <chrome> <Export-Ordner> <Bilder-Ordner oder leer> <gpu|software>
// gpu: echte Grafikkarte (PC-Lauf), software: SwiftShader (Tests auf jedem Rechner).
import { spawn } from "node:child_process";
import { createServer } from "node:http";
import { mkdtempSync, readFileSync, existsSync, rmSync, statSync, writeFileSync, mkdirSync } from "node:fs";
import { tmpdir } from "node:os";
import { join, extname, normalize } from "node:path";

const [chrome, exportDir, shotDir, modus] = process.argv.slice(2);
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const types = { ".html": "text/html; charset=utf-8", ".js": "text/javascript; charset=utf-8", ".css": "text/css; charset=utf-8", ".png": "image/png" };
const server = createServer((req, res) => {
  const rel = normalize(decodeURIComponent(new URL(req.url, "http://x").pathname.slice(1)) || "index.html");
  const file = join(exportDir, rel);
  if (rel.startsWith("..") || !existsSync(file) || statSync(file).isDirectory()) { res.writeHead(404); res.end(); return; }
  res.writeHead(200, { "content-type": types[extname(file)] ?? "application/octet-stream" }); res.end(readFileSync(file));
});
await new Promise((r) => server.listen(0, "127.0.0.1", r));
const base = `http://127.0.0.1:${server.address().port}/`;
const gpu = modus === "gpu";
const flags = gpu ? ["--enable-unsafe-webgpu", "--enable-features=Vulkan,WebGPU", "--ignore-gpu-blocklist", "--use-angle=d3d11", "--enable-gpu-rasterization"] : ["--use-angle=swiftshader", "--enable-unsafe-swiftshader"];
const profile = mkdtempSync(join(tmpdir(), "trendo-chrome-3d-"));
const port = 20000 + Math.floor(Math.random() * 30000);
const browser = spawn(chrome, ["--headless=new", "--no-first-run", "--disable-extensions", "--remote-allow-origins=*", `--remote-debugging-port=${port}`, `--user-data-dir=${profile}`, "--window-size=1440,900", ...flags, "about:blank"], { stdio: "ignore" });
const results = {}; const messung = {}; const problems = []; let ws;
const check = (name, ok, detail) => { results[name] = ok; if (!ok) problems.push(name + (detail === undefined ? "" : ": " + JSON.stringify(detail))); };
try {
  let target;
  for (let i = 0; i < 80 && !target; i++) { try { target = (await (await fetch(`http://127.0.0.1:${port}/json/list`)).json()).find((t) => t.type === "page"); } catch { } if (!target) await sleep(150); }
  ws = new WebSocket(target.webSocketDebuggerUrl); await new Promise((r, e) => { ws.onopen = r; ws.onerror = e; });
  let id = 0; const waiting = new Map(); const exceptions = [];
  ws.onmessage = (m) => { const d = JSON.parse(m.data); if (d.id && waiting.has(d.id)) { waiting.get(d.id)(d); waiting.delete(d.id); } else if (d.method === "Runtime.exceptionThrown") exceptions.push(d.params.exceptionDetails.exception?.description ?? d.params.exceptionDetails.text); };
  const call = (method, params = {}) => new Promise((r) => { const n = ++id; waiting.set(n, r); ws.send(JSON.stringify({ id: n, method, params })); });
  const run = async (expression) => { const r = await call("Runtime.evaluate", { expression, awaitPromise: true, returnByValue: true }); if (r.result?.exceptionDetails) throw new Error(r.result.exceptionDetails.exception?.description ?? "Skriptfehler"); return r.result?.result?.value; };
  const until = async (expression, ms = 30000) => { for (let i = 0; i < ms / 100; i++) { if (await run(expression).catch(() => false)) return true; await sleep(100); } return false; };
  await call("Page.enable"); await call("Runtime.enable");
  let skript = null;
  const oeffne = async (page, vorher) => {
    if (skript) await call("Page.removeScriptToEvaluateOnNewDocument", { identifier: skript });
    skript = vorher ? (await call("Page.addScriptToEvaluateOnNewDocument", { source: vorher })).result.identifier : null;
    await call("Page.navigate", { url: base + page }); await until("document.documentElement.dataset.fertig === '1'", 15000); await sleep(200);
  };
  // Inhalt im Bild: Anteil der Pixel, die sich vom Hintergrund unterscheiden (Kopie der Zeichenfläche)
  const inhalt = `(async()=>{const c=document.querySelector('.t-modell-flaeche canvas');if(!c)return 0;await new Promise(r=>requestAnimationFrame(()=>requestAnimationFrame(r)));const k=document.createElement('canvas');k.width=200;k.height=120;const g=k.getContext('2d');g.drawImage(c,0,0,200,120);const d=g.getImageData(0,0,200,120).data;let n=0;for(let i=0;i<d.length;i+=4){if(d[i+3]>8&&(Math.abs(d[i]-d[0])+Math.abs(d[i+1]-d[1])+Math.abs(d[i+2]-d[2])>24||d[i+3]-d[3]>40))n++}return n/(200*120)})()`;
  const bild = async (name) => {
    if (!shotDir) return;
    const r = await run(`(()=>{const b=document.querySelector('.t-modell').getBoundingClientRect();return [b.x,b.y+scrollY,b.width,b.height]})()`);
    const s = await call("Page.captureScreenshot", { format: "png", clip: { x: r[0], y: r[1], width: r[2], height: r[3], scale: 1 } });
    mkdirSync(shotDir, { recursive: true }); writeFileSync(join(shotDir, name + ".png"), Buffer.from(s.result.data, "base64"));
  };
  const fps = async () => { await sleep(5000); return parseFloat(await run(`document.querySelector('.t-modell-flaeche').dataset.fps||'0'`)); };

  // 1) normal: WebGPU, wenn der Browser es kann
  await oeffne("index.html");
  check("Modell erscheint", await until(`document.querySelector('.t-modell-flaeche')?.dataset.bereit==='1'`), await run(`document.querySelector('.t-modell')?.dataset.zustand`));
  messung.backendNormal = await run(`document.querySelector('.t-modell-flaeche').dataset.backend`);
  messung.webgpuVerfuegbar = await run(`(async()=>!!navigator.gpu && !!(await navigator.gpu.requestAdapter().catch(()=>null)))()`);
  messung.gpu = await run(`(()=>{try{const c=document.createElement('canvas').getContext('webgl2');const e=c.getExtension('WEBGL_debug_renderer_info');return e?c.getParameter(e.UNMASKED_RENDERER_WEBGL):c.getParameter(c.RENDERER)}catch{return ''}})()`);
  check("WebGPU genutzt, wenn verfügbar", !messung.webgpuVerfuegbar || messung.backendNormal === "webgpu", messung);
  messung.fpsNormal = await fps();
  messung.inhaltNormal = await run(inhalt);
  check("Bild hat Inhalt (Modell gezeichnet)", messung.inhaltNormal > 0.02, messung.inhaltNormal);
  await bild("1-modell-" + messung.backendNormal);

  // 2) ohne WebGPU: WebGL
  await oeffne("index.html", "try{Object.defineProperty(Navigator.prototype,'gpu',{get(){return undefined}})}catch{}");
  check("Ohne WebGPU erscheint das Modell", await until(`document.querySelector('.t-modell-flaeche')?.dataset.bereit==='1'`));
  messung.backendOhneWebgpu = await run(`document.querySelector('.t-modell-flaeche').dataset.backend`);
  check("Ohne WebGPU läuft WebGL", messung.backendOhneWebgpu === "webgl", messung.backendOhneWebgpu);
  messung.fpsWebgl = await fps();
  messung.inhaltWebgl = await run(inhalt);
  check("WebGL-Bild hat Inhalt", messung.inhaltWebgl > 0.02, messung.inhaltWebgl);
  await bild("2-modell-webgl");

  // 3) ohne 3D: Standbild
  await oeffne("index.html", "try{Object.defineProperty(Navigator.prototype,'gpu',{get(){return undefined}})}catch{};const g=HTMLCanvasElement.prototype.getContext;HTMLCanvasElement.prototype.getContext=function(t,...a){return /webgl|webgpu/.test(t)?null:g.call(this,t,...a)}");
  check("Ohne 3D erscheint das Standbild", await until(`(()=>{const i=document.querySelector('.t-modell-standbild');return !!i&&i.complete&&i.naturalWidth>0})()`, 10000));
  check("Ohne 3D keine Zeichenfläche", await run(`!document.querySelector('.t-modell-flaeche canvas')`));
  await bild("3-ohne-3d-standbild");

  // 4) Gaussian Splat (Spark, WebGL)
  if (existsSync(join(exportDir, "splat.html"))) {
    await oeffne("splat.html");
    check("Splat erscheint", await until(`document.querySelector('.t-modell-flaeche')?.dataset.bereit==='1'`), await run(`document.querySelector('.t-modell')?.dataset.zustand`));
    messung.fpsSplat = await fps();
    messung.inhaltSplat = await run(inhalt);
    check("Splat-Bild hat Inhalt", messung.inhaltSplat > 0.005, messung.inhaltSplat);
    await bild("4-splat");
  }
  check("keine Skriptfehler", exceptions.length === 0, exceptions.slice(0, 3));
} catch (e) { problems.push(String(e?.message ?? e)); }
finally {
  try { ws?.close(); } catch { }
  browser.kill(); server.close(); await sleep(300);
  try { rmSync(profile, { recursive: true, force: true }); } catch { }
}
process.stdout.write(JSON.stringify({ results, messung, problems }));
process.exit(problems.length ? 1 : 0);
