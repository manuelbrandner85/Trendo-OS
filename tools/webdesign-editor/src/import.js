// Z8/K6: HTML in eine Baumstruktur übersetzen – mit dem Kern von GrapesJS (BSD-3-Clause), ohne Editor-Oberfläche.
// Trendo lädt diese Datei nur zum Einlesen (assets/import.html), nicht in fertige Websites. Skripte, on…-Attribute und javascript:-Adressen
// entfernt schon der Parser von GrapesJS; Trendo prüft den Baum danach noch einmal mit einer festen Liste erlaubter Elemente.
import grapesjs from "grapesjs";

let editor = null;
function parser() {
  if (!editor) editor = grapesjs.init({ headless: true, storageManager: false, undoManager: false, parser: { optionsHtml: { allowScripts: false, allowUnsafeAttr: false, allowUnsafeAttrValue: false } } });
  return editor.Parser;
}

// Ergebnis von GrapesJS in einen schlichten Baum: { tag, attrs, kinder } oder { text }
function plain(def) {
  if (def == null) return null;
  if (typeof def === "string") return { text: def };
  if (def.type === "textnode") return { text: String(def.content ?? "") };
  if (def.type === "comment") return null;
  const tag = String(def.tagName || (def.type === "image" ? "img" : def.type === "link" ? "a" : "div")).toLowerCase();
  const attrs = { ...(def.attributes || {}) };
  if (def.src && tag === "img") attrs.src = def.src;
  if (def.classes) attrs.class = def.classes.map((c) => (typeof c === "string" ? c : c.name)).join(" ");
  const kinder = [];
  if (typeof def.content === "string" && def.content.length) kinder.push({ text: def.content });
  // „components“ ist je nach Element eine Liste, ein einzelnes Element oder schon Text
  const comps = Array.isArray(def.components) ? def.components : typeof def.components === "string" ? [{ type: "textnode", content: def.components }] : def.components ? [def.components] : [];
  for (const c of comps) { const k = plain(c); if (k) kinder.push(k); }
  return { tag, attrs, kinder };
}

window.trendoParseHtml = (html) => {
  const res = parser().parseHtml(String(html || ""), { keepEmptyTextNodes: false });
  const list = Array.isArray(res.html) ? res.html : res.html ? [res.html] : [];
  return JSON.stringify({ knoten: list.map(plain).filter(Boolean), css: Array.isArray(res.css) ? res.css.length : 0 });
};
document.documentElement.dataset.fertig = "1";
