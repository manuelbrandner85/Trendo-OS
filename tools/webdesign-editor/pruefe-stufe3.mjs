// Prüft Stufe 3, Teil 1 in einem echten Chrome am fertigen Export: Inhalt steht ohne Skript im HTML (W3), Seite lädt ohne Fehler,
// Liste aus der Sammlung (W2), Links bleiben in ihrer Sprache und Bilder laden auch in Unterordnern (Z5, <base>), Übergang zwischen Seiten (W4), axe-core.
// Aufruf: node pruefe-stufe3.mjs <chrome> <Export-Ordner> <axe.min.js oder leer>
// Ausgabe: JSON mit den Ergebnissen; Fehler ergeben Exitcode 1.
import { spawn } from "node:child_process";
import { createServer } from "node:http";
import { mkdtempSync, readFileSync, existsSync, rmSync, statSync } from "node:fs";
import { tmpdir } from "node:os";
import { join, extname, normalize } from "node:path";

const [chrome, exportDir, axeFile] = process.argv.slice(2);
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const types = { ".html": "text/html; charset=utf-8", ".js": "text/javascript; charset=utf-8", ".css": "text/css; charset=utf-8", ".png": "image/png", ".xml": "application/xml" };
const server = createServer((req, res) => {
  const rel = normalize(decodeURIComponent(new URL(req.url, "http://x").pathname.slice(1)) || "index.html");
  const file = join(exportDir, rel);
  if (rel.startsWith("..") || !existsSync(file) || statSync(file).isDirectory()) { res.writeHead(404); res.end(); return; }
  res.writeHead(200, { "content-type": types[extname(file)] ?? "application/octet-stream" }); res.end(readFileSync(file));
});
await new Promise((r) => server.listen(0, "127.0.0.1", r));
const base = `http://127.0.0.1:${server.address().port}/`;
const profile = mkdtempSync(join(tmpdir(), "trendo-chrome-stufe3-"));
const port = 20000 + Math.floor(Math.random() * 30000);
const browser = spawn(chrome, ["--headless=new", "--no-sandbox", "--no-first-run", "--disable-extensions", "--remote-allow-origins=*", `--remote-debugging-port=${port}`, `--user-data-dir=${profile}`, "--window-size=1280,900", "about:blank"], { stdio: "ignore" });

const results = {}; const problems = []; let ws;
const check = (name, ok, detail) => { results[name] = ok; if (!ok) problems.push(name + (detail === undefined ? "" : ": " + JSON.stringify(detail))); };
try {
  // W3: ohne Skript – rohes HTML direkt vom Server
  const raw = await (await fetch(base + "index.html")).text();
  check("W3: Text der Startseite steht im HTML ohne Skript", raw.includes("Willkommen in der Backstube") && !raw.includes('<div id="root"></div>'));
  const rawEntry = await (await fetch(base + "rezepte/brot.html")).text();
  check("W3: Eintragsseite hat ihren Text im HTML", rawEntry.includes("Bauernbrot"));
  check("W4: Übergang zwischen Seiten mit Ersatzweg im Kopf", raw.includes("@media (prefers-reduced-motion: no-preference) { @view-transition { navigation: auto; } }"));

  let target;
  for (let i = 0; i < 80 && !target; i++) {
    try { target = (await (await fetch(`http://127.0.0.1:${port}/json/list`)).json()).find((t) => t.type === "page"); } catch { /* Chrome startet noch */ }
    if (!target) await sleep(150);
  }
  if (!target) throw new Error("Chrome hat keinen Vorschau-Kanal geöffnet.");
  ws = new WebSocket(target.webSocketDebuggerUrl);
  await new Promise((r, e) => { ws.onopen = r; ws.onerror = e; });
  let id = 0; const waiting = new Map(); const exceptions = []; const failed = [];
  ws.onmessage = (m) => {
    const d = JSON.parse(m.data);
    if (d.id && waiting.has(d.id)) { waiting.get(d.id)(d); waiting.delete(d.id); }
    else if (d.method === "Runtime.exceptionThrown") exceptions.push(d.params.exceptionDetails.exception?.description ?? d.params.exceptionDetails.text);
    else if (d.method === "Runtime.consoleAPICalled" && d.params.type === "error") exceptions.push("Konsole: " + d.params.args.map((a) => a.value ?? a.description).join(" "));
    else if (d.method === "Network.responseReceived" && d.params.response.status >= 400 && !d.params.response.url.endsWith("favicon.ico")) failed.push(d.params.response.url.replace(base, ""));
  };
  const call = (method, params = {}) => new Promise((r) => { const n = ++id; waiting.set(n, r); ws.send(JSON.stringify({ id: n, method, params })); });
  const run = async (expression) => { const r = await call("Runtime.evaluate", { expression, awaitPromise: true, returnByValue: true }); if (r.result?.exceptionDetails) throw new Error(r.result.exceptionDetails.exception?.description ?? "Skriptfehler"); return r.result?.result?.value; };
  const until = async (expression, ms = 20000) => { for (let i = 0; i < ms / 50; i++) { if (await run(expression).catch(() => false)) return true; await sleep(50); } return false; };
  await call("Page.enable"); await call("Runtime.enable"); await call("Network.enable");
  const open = async (path) => { await call("Page.navigate", { url: base + path }); await until("document.documentElement.dataset.fertig === '1'"); await sleep(300); };

  // Startseite: lädt ohne Fehler, Liste aus der Sammlung
  await open("index.html");
  check("Startseite: Inhalt nach dem Laden sichtbar", await run(`document.body.innerText.includes("Willkommen in der Backstube")`));
  check("W2: Liste zeigt drei Einträge", (await run(`document.querySelectorAll('.t-sammlung-liste .t-karte').length`)) === 3, await run(`document.querySelectorAll('.t-sammlung-liste .t-karte').length`));
  check("W2: Bilder der Liste laden", await until(`[...document.querySelectorAll('.t-karte img')].every(i => i.complete && i.naturalWidth > 0)`));
  if (axeFile) {
    await run(readFileSync(axeFile, "utf8") + ";true");
    const v = await run(`axe.run(document.querySelector('.t-sammlung'), { resultTypes: ['violations'] }).then(r => r.violations.filter(x => x.impact === 'critical' || x.impact === 'serious').map(x => x.id + ':' + x.nodes.length))`);
    check("W2: axe ohne schweren Befund – Sammlung", Array.isArray(v) && v.length === 0, v);
  }
  // Klick auf einen Eintrag führt zur Eintragsseite (Unterordner mit <base>)
  await run(`document.querySelector('.t-sammlung-liste a').click()`);
  await until("location.pathname.startsWith('/rezepte/') && document.documentElement.dataset.fertig === '1'");
  check("W2: Link der Liste führt zur Eintragsseite", await run(`location.pathname`) === "/rezepte/zopf.html", await run(`location.pathname`));
  check("W2/Z5: Bild der Eintragsseite lädt im Unterordner", await until(`[...document.images].length > 0 && [...document.images].every(i => i.complete && i.naturalWidth > 0)`));
  // Englische Seite: Links bleiben in der Sprache
  await open("en/index.html");
  check("Z5: englische Seite trägt lang=en", await run(`document.documentElement.lang`) === "en");
  await run(`[...document.querySelectorAll('a')].find(a => a.textContent === 'About us').click()`);
  await until("location.pathname === '/en/ueber-uns.html' && document.documentElement.dataset.fertig === '1'");
  check("Z5: Link führt zur Seite in derselben Sprache", await run(`location.pathname`) === "/en/ueber-uns.html", await run(`location.pathname`));
  check("Z5: übersetzter Text erscheint", await run(`document.body.innerText.includes("Baking since 1990")`));
  check("Sprungmarke bleibt auf der Seite trotz <base>", (await run(`[...document.querySelectorAll('a')].find(a => a.textContent === 'Contact').getAttribute('href')`)) === "en/ueber-uns.html#kontakt");
  check("keine Skriptfehler", exceptions.length === 0, exceptions.slice(0, 3));
  check("keine fehlenden Dateien", failed.length === 0, failed.slice(0, 5));
} catch (e) { problems.push(String(e?.message ?? e)); }
finally {
  try { ws?.close(); } catch { /* schon zu */ }
  browser.kill(); server.close();
  await sleep(300);
  try { rmSync(profile, { recursive: true, force: true }); } catch { /* Chrome hält die Datei noch */ }
}
process.stdout.write(JSON.stringify({ results, problems }));
process.exit(problems.length ? 1 : 0);
