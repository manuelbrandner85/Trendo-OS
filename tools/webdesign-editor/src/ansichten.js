// W1/Z4: Geräte-Ansichten. Desktop ist die Grundlage; jede kleinere Ansicht hat eine Breite und gilt bis zu ihr.
// Anpassungen einer Ansicht gelten auch in allen kleineren, bis eine kleinere sie überschreibt (Reihenfolge der Regeln: breit vor schmal).
// Dieselbe Regel-Erzeugung steht in C# (WebDesignViews.Css); ein Test vergleicht beide.
export const BASIS = "Desktop";
export const STANDARD_ANSICHTEN = [{ name: "Tablet", breite: 1024 }, { name: "Handy", breite: 600 }];
const ID = /^[A-Za-z0-9_-]{1,80}$/;
const AUSRICHTUNG = ["left", "center", "right"];

export function ansichtenVon(root) {
  const a = root?.props?.ansichten;
  return Array.isArray(a) ? [...a].filter((v) => v && typeof v.name === "string" && Number.isInteger(v.breite)).sort((x, y) => y.breite - x.breite) : STANDARD_ANSICHTEN;
}

export function bloecke(data) {
  const out = [];
  const walk = (list) => {
    for (const b of list || []) {
      if (!b || typeof b !== "object") continue;
      out.push(b);
      for (const v of Object.values(b.props || {})) if (Array.isArray(v) && v.length && v.every((x) => x && typeof x === "object" && x.type)) walk(v);
    }
  };
  walk(data?.content);
  for (const z of Object.values(data?.zones || {})) walk(z);
  return out;
}

/** CSS-Regeln der Anpassungen. art "media": Bildschirmbreite (Export). art "container": Breite der Seite im Editor. */
export function anpassungenCss(data, art) {
  const teile = [];
  for (const v of ansichtenVon(data?.root)) {
    const regeln = [];
    for (const b of bloecke(data)) {
      const id = b.props?.id; const a = b.props?.anpassungen?.[v.name];
      if (!a || typeof a !== "object" || !ID.test(id || "")) continue;
      const d = [];
      if (a.ausblenden === true) d.push("display:none"); else if (a.ausblenden === false) d.push("display:block");
      if (Number.isInteger(a.schrift) && a.schrift >= 50 && a.schrift <= 200) d.push("font-size:" + a.schrift + "%", "--t-schrift:" + a.schrift / 100);
      if (d.length) regeln.push(`[data-b="${id}"]{${d.join(";")}}`);
      // Ausrichtung auch direkt am Inhalt, damit sie die Grundausrichtung des Bausteins schlägt
      if (AUSRICHTUNG.includes(a.ausrichtung)) regeln.push(`[data-b="${id}"],.t-page [data-b="${id}"]>*,[data-b="${id}"] .t-hero{text-align:${a.ausrichtung}}`);
      if (b.type === "Spalten" && (a.spalten === 1 || a.spalten === 2)) regeln.push(`[data-b="${id}"]>.t-cols-box>.t-cols{grid-template-columns:repeat(${a.spalten},minmax(0,1fr))}`);
    }
    if (regeln.length) teile.push(`${art === "container" ? "@container seite" : "@media"} (max-width: ${v.breite}px){${regeln.join("")}}`);
  }
  return teile.join("\n");
}

/** Wert eines Feldes in einer Ansicht: eigene Anpassung, sonst die der nächstgrößeren Ansicht, sonst undefined (Grundlage). */
export function geerbt(anpassungen, ansichten, name, feld) {
  let wert, von;
  for (const v of ansichten) {
    const w = anpassungen?.[v.name]?.[feld];
    if (w !== undefined) { wert = w; von = v.name; }
    if (v.name === name) break;
  }
  return { wert, von };
}
