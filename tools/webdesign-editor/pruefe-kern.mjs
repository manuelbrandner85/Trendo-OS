// Prüft die reinen Rechnungen für Splats ohne Browser: Bildausschnitt (einzelnes Objekt behält seine Größe, Aufnahme mit Landschaft wird auf den Kern
// begrenzt) und Nahgrenze (Motiv bleibt aus jeder Richtung und bei jedem Zoom ganz). Aufruf: node pruefe-kern.mjs – Ausgabe JSON, Rückgabe 1 bei Fehler.
import { ausschnitt, nahgrenze, tiefeZurKamera } from "./src/kern.js";

const problems = [];
const check = (name, ok, detail) => { if (!ok) problems.push(name + (detail === undefined ? "" : ": " + JSON.stringify(detail))); };
let seed = 7; const zufall = () => ((seed = (seed * 1103515245 + 12345) % 2147483648) / 2147483648);

// 1) Kugel (r = 0,5) ohne Hintergrund, gleichmäßig auf der Oberfläche wie im Ersatz-Splat der Tests
const k = { xs: [], ys: [], zs: [] };
for (let i = 0; i < 20000; i++) { const th = zufall() * Math.PI * 2, ph = Math.acos(2 * zufall() - 1); k.xs.push(Math.sin(ph) * Math.cos(th) * 0.5); k.ys.push(Math.cos(ph) * 0.5 + 0.5); k.zs.push(Math.sin(ph) * Math.sin(th) * 0.5); }
const a = ausschnitt(k.xs, k.ys, k.zs);
check("Kugel: kein Kern-Ausschnitt", !a.kern, a);
for (let i = 0; i < 3; i++) check(`Kugel: Achse ${i} deckt mindestens 90 % des Durchmessers`, a.max[i] - a.min[i] >= 0.9, a);
check("Kugel: Unterkante nahe am Boden", a.min[1] < 0.05, a.min[1]);

// 2) Aufnahme: dichtes Motiv (Lok 10 × 4 × 3) mitten in weiter Landschaft (Boden, Hügel, Himmel bis 80 Einheiten)
const s = { xs: [], ys: [], zs: [] };
for (let i = 0; i < 30000; i++) { s.xs.push((zufall() - 0.5) * 10); s.ys.push(zufall() * 4); s.zs.push((zufall() - 0.5) * 3); }
for (let i = 0; i < 30000; i++) { s.xs.push((zufall() - 0.5) * 160); s.ys.push(zufall() * 40 - 2); s.zs.push((zufall() - 0.5) * 160); }
const b = ausschnitt(s.xs, s.ys, s.zs);
check("Aufnahme: Kern-Ausschnitt", b.kern, b);
check("Aufnahme: Ausschnitt deutlich kleiner als die Landschaft", b.max[0] - b.min[0] < 40 && b.max[2] - b.min[2] < 40, b);
check("Aufnahme: Motiv liegt im Ausschnitt", b.min[0] < 0 && b.max[0] > 0 && b.min[2] < 0 && b.max[2] > 0, b);

// 3) Nahgrenze: Vorderkante des Motivs (Abstand − r) liegt immer hinter der Nahgrenze, beim Zoomen bis nah an das Motiv
for (const r of [0.5, 5, 50]) for (const f of [1.05, 1.5, 3, 10]) { const d = r * f; check(`Nahgrenze r=${r} Abstand=${d}`, nahgrenze(d, r) < d - r || d - r < 1e-3, { n: nahgrenze(d, r), front: d - r }); }
check("Nahgrenze nie null", nahgrenze(0.5, 0.5) > 0);

// 4) Längliches Motiv (10 × 1 × 1) aus jeder Richtung, auch genau entlang der langen Achse: Vorderkante bleibt hinter der Nahgrenze
const halb = [5, 0.5, 0.5];
check("Tiefe entlang der langen Achse", Math.abs(tiefeZurKamera(halb, [1, 0, 0]) - 5) < 1e-9);
check("Tiefe quer", Math.abs(tiefeZurKamera(halb, [0, 0, 1]) - 0.5) < 1e-9);
for (let i = 0; i < 2000; i++) {
  let v = [zufall() - 0.5, zufall() - 0.5, zufall() - 0.5]; const l = Math.hypot(...v); v = v.map((x) => x / l);
  const d = 6 + zufall() * 20, t = tiefeZurKamera(halb, v);
  // Ecke des Kastens, die der Kamera am nächsten liegt
  const ecke = halb.map((h, k) => Math.sign(v[k]) * h); const kam = v.map((x) => x * d);
  const naechste = Math.hypot(kam[0] - ecke[0], kam[1] - ecke[1], kam[2] - ecke[2]);
  if (!(nahgrenze(d, t) < naechste)) { check("Längliches Motiv wird angeschnitten", false, { v, d, near: nahgrenze(d, t), naechste }); break; }
}

process.stdout.write(JSON.stringify({ kugel: a, aufnahme: b, problems }));
process.exit(problems.length ? 1 : 0);
