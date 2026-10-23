using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace Trendo.Core;

/// <summary>Eigenes Browser-Spiel: Regeln, Level-Raster, Ton und Figur. Die drei Vorlagen in GameStudio bleiben unverändert.</summary>
public sealed class SpielEntwurf
{
    public string Name { get; set; } = "Mein Level";
    public string Art { get; set; } = "snake";
    public int Tempo { get; set; } = 5;
    public int Leben { get; set; } = 3;
    public int Sieg { get; set; } = 10;
    public string Figur { get; set; } = "#4FB3FF";
    public string Hintergrund { get; set; } = "#10203f";
    public string FigurDatei { get; set; } = "";
    public bool Ton { get; set; }
    public string TonDatei { get; set; } = "";
    public int Breite { get; set; } = 16;
    public int Hoehe { get; set; } = 12;
    public List<bool> Raster { get; set; } = [];
}

public static class SpielBaukasten
{
    public static readonly (string Id, string Label)[] Arten =
    [
        ("snake", "Schlange"),
        ("breakout", "Mauerbrecher"),
        ("jump", "Hüpfspiel"),
    ];

    public static int ClampTempo(int v) => Math.Clamp(v, 1, 10);
    public static int ClampLeben(int v) => Math.Clamp(v, 1, 9);
    public static int ClampSieg(int v) => Math.Clamp(v, 1, 999);
    public static int ClampBreite(int v) => Math.Clamp(v, 8, 24);
    public static int ClampHoehe(int v) => Math.Clamp(v, 6, 16);

    /// <summary>Schreibt index.html, game.js und kopiert Figur und Ton in den Ordner.</summary>
    public static string Bauen(string parent, SpielEntwurf e)
    {
        if (Arten.All(a => a.Id != e.Art)) throw new ArgumentException("Unbekannte Spielart.");
        var dir = Path.Combine(parent, DevStudio.SafeName(string.IsNullOrWhiteSpace(e.Name) ? "Mein Level" : e.Name));
        if (Directory.Exists(dir)) throw new IOException($"Den Ordner „{Path.GetFileName(dir)}“ gibt es schon.");
        Directory.CreateDirectory(dir);
        var figur = Kopie(e.FigurDatei, dir, "figur");
        var ton = Kopie(e.TonDatei, dir, "ton");
        int breite = ClampBreite(e.Breite), hoehe = ClampHoehe(e.Hoehe);
        var raster = new int[breite * hoehe];
        for (var i = 0; i < raster.Length && i < e.Raster.Count; i++) raster[i] = e.Raster[i] ? 1 : 0;
        var regeln = new
        {
            art = e.Art,
            tempo = ClampTempo(e.Tempo),
            leben = ClampLeben(e.Leben),
            sieg = ClampSieg(e.Sieg),
            figur = SicherFarbe(e.Figur, "#4FB3FF"),
            hintergrund = SicherFarbe(e.Hintergrund, "#10203f"),
            figurBild = figur,
            ton = e.Ton,
            tonDatei = ton,
            breite,
            hoehe,
            raster,
        };
        var json = JsonSerializer.Serialize(regeln).Replace("<", "\\u003c").Replace(">", "\\u003e").Replace("&", "\\u0026");
        var title = System.Net.WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(e.Name) ? "Mein Level" : e.Name.Trim());
        var html = $$"""
<!doctype html><html lang="de"><head><meta charset="utf-8"><title>{{title}}</title>
<style>html,body{margin:0;background:#0b1730;color:#eef;font-family:system-ui,sans-serif;text-align:center}canvas{background:{{SicherFarbe(e.Hintergrund, "#10203f")}};border-radius:12px;margin-top:16px}button{margin:8px;padding:8px 14px}</style></head>
<body><h2>{{title}}</h2><p>Pfeiltasten, Maus oder Leertaste. Regeln stehen in der Seite.</p>
<canvas id="c" width="480" height="360"></canvas><p id="s">Punkte: 0</p>
<button id="neu" type="button">Nochmal</button>
<script>window.REGELN = {{json}};</script>
<script src="game.js"></script></body></html>
""";
        File.WriteAllText(Path.Combine(dir, "index.html"), html, new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(dir, "game.js"), Js, new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(dir, "README.md"), $"# {e.Name}\n\nEigenes Spiel aus Trendo OS. Öffne index.html im Browser.\n");
        return dir;
    }

    public static string Zip(string spielOrdner)
    {
        if (!File.Exists(Path.Combine(spielOrdner, "index.html"))) throw new IOException("In diesem Ordner liegt kein Spiel.");
        var zip = Path.Combine(Path.GetDirectoryName(spielOrdner)!, Path.GetFileName(spielOrdner) + ".zip");
        if (File.Exists(zip)) File.Delete(zip);
        ZipFile.CreateFromDirectory(spielOrdner, zip);
        return zip;
    }

    static string Kopie(string? quelle, string dir, string basis)
    {
        if (string.IsNullOrWhiteSpace(quelle) || !File.Exists(quelle)) return "";
        var ext = Path.GetExtension(quelle).ToLowerInvariant();
        if (ext.Length is < 2 or > 8) ext = "";
        var name = basis + ext;
        File.Copy(quelle, Path.Combine(dir, name), true);
        return name;
    }

    public static string SicherFarbe(string? farbe, string ersatz)
    {
        var s = (farbe ?? "").Trim();
        if (s.Length == 7 && s[0] == '#' && s[1..].All(Uri.IsHexDigit)) return s;
        return ersatz;
    }

    public const string Js = """
const R = (typeof window !== "undefined" && window.REGELN) || {art:"snake",tempo:5,leben:3,sieg:10,figur:"#4FB3FF",hintergrund:"#10203f",figurBild:"",ton:false,tonDatei:"",raster:[],breite:16,hoehe:12};
const c = document.getElementById("c");
const x = c.getContext("2d");
const cols = Math.max(8, R.breite | 0);
const rows = Math.max(6, R.hoehe | 0);
const cw = c.width / cols;
const ch = c.height / rows;
let leben = Math.max(1, R.leben | 0);
let punkte = 0;
let vorbei = false;
let gewonnen = false;
const bild = R.figurBild ? Object.assign(new Image(), {src: R.figurBild}) : null;
function ton() {
  if (!R.ton) return;
  try {
    if (R.tonDatei) { const a = new Audio(R.tonDatei); a.play(); return; }
    const AC = window.AudioContext || window.webkitAudioContext;
    if (!AC) return;
    const ac = ton.ac || (ton.ac = new AC());
    const o = ac.createOscillator();
    const g = ac.createGain();
    o.frequency.value = 660;
    g.gain.value = 0.05;
    o.connect(g); g.connect(ac.destination);
    o.start();
    o.stop(ac.currentTime + 0.08);
  } catch (e) {}
}
function zeile() {
  const s = document.getElementById("s");
  if (gewonnen) s.textContent = "Gewonnen. Punkte: " + punkte;
  else if (leben <= 0) s.textContent = "Verloren. Punkte: " + punkte;
  else s.textContent = "Punkte: " + punkte + "   Leben: " + leben;
}
function sieg() { if (punkte >= Math.max(1, R.sieg | 0)) { gewonnen = true; vorbei = true; } }
function treffer() { punkte++; ton(); sieg(); zeile(); }
function tot() { leben--; ton(); if (leben <= 0) vorbei = true; zeile(); }
function feld(i) { return R.raster && R.raster.length === cols * rows && R.raster[i] ? 1 : 0; }
function grund() { x.fillStyle = R.hintergrund || "#10203f"; x.fillRect(0, 0, c.width, c.height); }
function figur(px, py, w, h) {
  if (bild && bild.complete && bild.naturalWidth) x.drawImage(bild, px, py, w, h);
  else { x.fillStyle = R.figur || "#4FB3FF"; x.fillRect(px, py, w, h); }
}
const taste = {};
addEventListener("keydown", function (e) { taste[e.key] = 1; if (e.code === "Space") e.preventDefault(); });
addEventListener("keyup", function (e) { taste[e.key] = 0; });
document.getElementById("neu").onclick = function () { location.reload(); };

if (R.art === "breakout") {
  let px = c.width / 2 - 40;
  const speed = 2 + Math.max(1, Math.min(10, R.tempo | 0)) * 0.35;
  let ball = {x: 240, y: 260, dx: speed, dy: -speed};
  const bricks = [];
  const hatRaster = R.raster && R.raster.length === cols * rows;
  for (let r = 0; r < rows; r++) for (let i = 0; i < cols; i++) {
    if (hatRaster ? feld(r * cols + i) : r < 3) bricks.push({x: i * cw + 2, y: r * ch + 2, w: cw - 4, h: ch - 4, a: 1});
  }
  c.addEventListener("mousemove", function (e) { px = e.offsetX - 40; });
  function step() {
    if (vorbei) { zeile(); return; }
    if (taste.ArrowLeft) px -= 6; if (taste.ArrowRight) px += 6;
    px = Math.max(0, Math.min(c.width - 80, px));
    ball.x += ball.dx; ball.y += ball.dy;
    if (ball.x < 8 || ball.x > c.width - 8) ball.dx *= -1;
    if (ball.y < 8) ball.dy *= -1;
    if (ball.y > c.height - 18 && ball.x > px && ball.x < px + 80) ball.dy = -Math.abs(ball.dy);
    if (ball.y > c.height) { tot(); ball = {x: 240, y: 260, dx: speed, dy: -speed}; bricks.forEach(function (q) { q.a = 1; }); punkte = gewonnen ? punkte : 0; }
    bricks.forEach(function (q) {
      if (q.a && ball.x > q.x && ball.x < q.x + q.w && ball.y > q.y && ball.y < q.y + q.h) { q.a = 0; ball.dy *= -1; treffer(); }
    });
    if (bricks.length && bricks.every(function (q) { return !q.a; })) { punkte = Math.max(punkte, R.sieg | 0); sieg(); }
    grund();
    x.fillStyle = "#eef"; x.fillRect(px, c.height - 14, 80, 8);
    figur(ball.x - 6, ball.y - 6, 12, 12);
    bricks.forEach(function (q) { if (q.a) { x.fillStyle = "#7C4DFF"; x.fillRect(q.x, q.y, q.w, q.h); } });
    zeile();
    if (!vorbei) requestAnimationFrame(step);
  }
  step();
} else if (R.art === "jump") {
  let y = c.height - 70, v = 0, t = 0;
  const hüpfen = 11 + Math.max(1, Math.min(10, R.tempo | 0)) * 0.3;
  const obstacles = [];
  const boden = [];
  for (let i = 0; i < cols; i++) if (feld((rows - 1) * cols + i)) boden.push(i);
  function step() {
    if (vorbei) { zeile(); return; }
    t++;
    if ((taste[" "] || taste.ArrowUp) && y >= c.height - 70) v = -hüpfen;
    v += 0.55; y = Math.min(c.height - 70, y + v);
    const tempo = 3 + Math.max(1, Math.min(10, R.tempo | 0)) * 0.4;
    if (t % Math.max(20, 90 - R.tempo * 6) === 0) {
      const spalte = boden.length ? boden[t % boden.length] : cols - 1;
      obstacles.push({x: spalte * cw});
    }
    obstacles.forEach(function (q) { q.x -= tempo; });
    const noch = obstacles.filter(function (q) { return q.x > -cw; });
    obstacles.length = 0; noch.forEach(function (q) { obstacles.push(q); });
    if (obstacles.some(function (q) { return q.x < 70 && q.x > 30 && y > c.height - 80; })) { tot(); obstacles.length = 0; y = c.height - 70; v = 0; }
    else if (t % 8 === 0) treffer();
    grund();
    x.fillStyle = "#334"; x.fillRect(0, c.height - 28, c.width, 4);
    figur(40, y, 28, 28);
    x.fillStyle = "#FB8C00";
    obstacles.forEach(function (q) { x.fillRect(q.x, c.height - 58, 18, 30); });
    zeile();
    if (!vorbei) requestAnimationFrame(step);
  }
  step();
} else {
  const s = [{x: 3, y: 3}];
  let d = {x: 1, y: 0};
  let f = {x: Math.min(cols - 2, 8), y: Math.min(rows - 2, 6)};
  addEventListener("keydown", function (e) {
    const m = {ArrowUp: [0, -1], ArrowDown: [0, 1], ArrowLeft: [-1, 0], ArrowRight: [1, 0]}[e.key];
    if (m && (m[0] !== -d.x || m[1] !== -d.y)) d = {x: m[0], y: m[1]};
  });
  function step() {
    if (vorbei) { zeile(); return; }
    const h = {x: (s[0].x + d.x + cols) % cols, y: (s[0].y + d.y + rows) % rows};
    if (s.some(function (q) { return q.x === h.x && q.y === h.y; })) { tot(); s.length = 0; s.push({x: 3, y: 3}); }
    s.unshift(h);
    if (h.x === f.x && h.y === f.y) { treffer(); f = {x: Math.floor(Math.random() * cols), y: Math.floor(Math.random() * rows)}; }
    else s.pop();
    grund();
    x.fillStyle = "#FACC15"; x.fillRect(f.x * cw, f.y * ch, cw - 2, ch - 2);
    s.forEach(function (q, i) { if (i === 0) figur(q.x * cw, q.y * ch, cw - 2, ch - 2); else { x.fillStyle = R.figur || "#4FB3FF"; x.fillRect(q.x * cw, q.y * ch, cw - 2, ch - 2); } });
    zeile();
  }
  const ms = Math.max(40, 220 - Math.max(1, Math.min(10, R.tempo | 0)) * 16);
  setInterval(step, ms);
}
""";
}
