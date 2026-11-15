// Z3: reine Rechnungen für den Bildausschnitt von Splats und die Nahgrenze der Kamera – ohne three.js, damit sie ohne Browser prüfbar sind
// (tools/webdesign-editor/pruefe-kern.mjs).

const perzentil = (sortiert, q) => sortiert[Math.min(sortiert.length - 1, Math.max(0, Math.floor(q * sortiert.length)))];

/**
 * Ausschnitt aus Punktlisten (je Achse): Ein einzelnes Objekt behält seine volle Ausdehnung (2.–98. Perzentil, Ausreißer ab).
 * Eine Aufnahme mit Landschaft rundherum (dichter Kern viel kleiner als die volle Ausdehnung) wird auf den Kern (30.–70. Perzentil) begrenzt.
 * Liefert { min: [x,y,z], max: [x,y,z], kern: boolean }.
 */
export function ausschnitt(xs, ys, zs) {
  const achsen = [xs, ys, zs].map((a) => Float64Array.from(a).sort());
  const voll = achsen.map((a) => [perzentil(a, 0.02), perzentil(a, 0.98)]);
  const kern = achsen.map((a) => [perzentil(a, 0.3), perzentil(a, 0.7)]);
  // Bei gleichmäßig gefüllten Objekten ist der Kern auf jeder Achse etwa 40 % der vollen Ausdehnung; bei Aufnahmen mit Landschaft
  // ist er in mindestens einer Richtung (meist waagerecht) weit kleiner. Senkrecht ist auch eine Landschaft flach, darum zählt die kleinste Achse.
  const anteil = Math.min(...kern.map((k, i) => (k[1] - k[0]) / Math.max(voll[i][1] - voll[i][0], 1e-9)));
  const nimmKern = anteil < 0.25;
  const b = nimmKern ? kern : voll;
  return { min: b.map((x) => x[0]), max: b.map((x) => x[1]), kern: nimmKern };
}

/**
 * Nahgrenze für Aufnahmen: Was näher an der Kamera liegt als 95 % des Abstands zur Vorderkante des Motivs (Tiefe entlang der Blickachse, darum exakt), wird ausgeblendet
 * (Pfosten, Ausreißer vor der Linse). r ist die Tiefe des Motivs in Richtung Kamera (tiefeZurKamera); das Motiv bleibt aus jeder Richtung und bei jedem Zoom ganz.
 */
export function nahgrenze(abstand, r) {
  return Math.max((abstand - r) * 0.95, abstand / 1000, 0.001);
}

/** Wie weit reicht ein achsparalleler Kasten (halbe Kantenlängen) von seiner Mitte aus in Blickrichtung (Einheitsvektor) – die Vorderkante aus Sicht der Kamera. */
export function tiefeZurKamera(halb, richtung) {
  return Math.abs(halb[0] * richtung[0]) + Math.abs(halb[1] * richtung[1]) + Math.abs(halb[2] * richtung[2]);
}
