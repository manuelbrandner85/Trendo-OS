// Trendo Web Design Studio – Editor-Kern (Puck, MIT) mit 3D-Block (React Three Fiber, MIT) und Bewegungen (Web Animations API).
// Läuft im eingebetteten WebView von Trendo. Laden: Trendo ruft window.trendoLoad(json); Speichern: invokeCSharpAction({art:"speichern", daten}).
import React, { useEffect, useMemo, useRef, useState } from "react";
import { createRoot } from "react-dom/client";
import { Puck, Render, createUsePuck } from "@puckeditor/core";
import "@puckeditor/core/puck.css";
import { Canvas, useFrame } from "@react-three/fiber";
import "./trendo.css";
import { de } from "./de.js";
import { ansichtenVon, anpassungenCss, geerbt, BASIS } from "./ansichten.js";

const send = (msg) => { try { if (typeof window.invokeCSharpAction === "function") window.invokeCSharpAction(JSON.stringify(msg)); } catch { /* ohne Trendo (Test im Browser) */ } };

// ---------- 3D-Block: echtes WebGL über React Three Fiber ----------
function Shape({ form, color, spin }) {
  const ref = useRef();
  useFrame((_, dt) => { if (ref.current && spin) ref.current.rotation.y += dt * spin; });
  return (
    <mesh ref={ref} castShadow>
      {form === "kugel" ? <sphereGeometry args={[1, 64, 64]} /> : form === "ring" ? <torusKnotGeometry args={[0.7, 0.25, 200, 32]} /> : <boxGeometry args={[1.4, 1.4, 1.4]} />}
      <meshStandardMaterial color={color} roughness={0.35} metalness={0.2} />
    </mesh>
  );
}
function Scene3D({ form, color, spin, height }) {
  return (
    <div className="t-scene" style={{ height }}>
      <Canvas shadows dpr={[1, 2]} camera={{ position: [0, 1.2, 4], fov: 40 }} gl={{ antialias: true }} resize={{ offsetSize: true }}>
        <ambientLight intensity={0.35} />
        <directionalLight position={[3, 5, 2]} intensity={2.2} castShadow />
        <Shape form={form} color={color} spin={spin} />
        <mesh rotation={[-Math.PI / 2, 0, 0]} position={[0, -1.2, 0]} receiveShadow><planeGeometry args={[20, 20]} /><shadowMaterial opacity={0.25} /></mesh>
      </Canvas>
    </div>
  );
}

// ---------- Bewegung: Web Animations API des Browsers (keine Fremdbibliothek). Theatre.js folgt mit dem Zeitleisten-Modul 09. ----------
// Wer im System „weniger Bewegung“ eingestellt hat, bekommt keine Animation (Barrierefreiheit).
function Animated({ children, motion, seconds }) {
  const ref = useRef();
  useEffect(() => {
    const el = ref.current; if (!el || !motion || motion === "keine" || el.ownerDocument.defaultView?.matchMedia?.("(prefers-reduced-motion: reduce)").matches) return;
    const a = motion === "einblenden"
      ? el.animate([{ opacity: 0, transform: "translateY(24px)" }, { opacity: 1, transform: "none" }], { duration: seconds * 1000, easing: "cubic-bezier(.2,.7,.2,1)", fill: "both" })
      : el.animate([{ transform: "translateY(0)" }, { transform: "translateY(-8px)" }, { transform: "translateY(0)" }], { duration: Math.max(1, seconds) * 2000, iterations: Infinity, easing: "ease-in-out" });
    return () => a.cancel();
  }, [motion, seconds]);
  return <div ref={ref}>{children}</div>;
}
const motionFields = {
  motion: { type: "select", label: "Bewegung", options: [{ label: "Keine", value: "keine" }, { label: "Einblenden", value: "einblenden" }, { label: "Schweben", value: "schweben" }] },
  seconds: { type: "number", label: "Dauer (s)", min: 0.2, max: 5, step: 0.1 },
};

// ---------- Design-Tokens (Modul 05) ----------
const usePuckSel = createUsePuck();
const FONTS = { klar: '"Segoe UI", Helvetica, Arial, sans-serif', serif: 'Georgia, "Times New Roman", serif', rund: '"Trebuchet MS", "Segoe UI", sans-serif', mono: 'Consolas, "Courier New", monospace' };
const hex = (v, d) => (typeof v === "string" && /^#[0-9a-fA-F]{6}$/.test(v) ? v : d);
const num = (v, d, lo, hi) => (Number.isFinite(+v) ? Math.min(hi, Math.max(lo, +v)) : d);
export function tokens(t) {
  const v = { "--t-accent": hex(t.accent, "#1d1d1f"), "--t-ink": hex(t.ink, "#1d1d1f"), "--t-paper": hex(t.paper, "#ffffff"), "--t-font": FONTS[t.font] || FONTS.klar,
    "--t-radius": num(t.radius, 12, 0, 40) + "px", "--t-space": num(t.space, 24, 4, 48) + "px" };
  // W6: dunkler Satz, wird über „prefers-color-scheme: dark“ aktiv (Regel in trendo.css)
  if (t.dunkel) Object.assign(v, { "--t-accent-d": hex(t.accentDunkel, "#f0c75e"), "--t-ink-d": hex(t.inkDunkel, "#f2f2f2"), "--t-paper-d": hex(t.paperDunkel, "#121212") });
  return v;
}

// W1: Daten der ganzen Seite, damit die Seite die Anpassungen je Ansicht als Container-Regeln zeichnen kann (nur Editor und Vorschau; der Export bekommt Medienregeln von Trendo)
const DataCtx = React.createContext(null);
function PageRoot({ children, p }) {
  const data = React.useContext(DataCtx);
  return (
    <main className={"t-page" + (p.dunkel ? " t-hat-dunkel" : "")} style={tokens(p)}>
      {data && <style>{anpassungenCss(data, "container")}</style>}
      {children}
    </main>);
}

// W1: Anpassungen je Ansicht im Eigenschaftenfeld. Zeigt, was überschrieben ist und woher ein Wert kommt; „Zurücksetzen“ nimmt die Überschreibung weg.
function AnpassungenFeld({ value, onChange, spalten }) {
  // Eigene Wahl der Ansicht: Die Ansichtsumschaltung des Editors gibt es nur mit Rahmen (iframe), in Trendo läuft der Editor ohne
  const width = usePuckSel((s) => s.appState.ui.viewports.current.width);
  const root = usePuckSel((s) => s.appState.data.root);
  const ansichten = ansichtenVon(root);
  const [gewaehlt, setGewaehlt] = useState(null);
  const ansicht = gewaehlt === BASIS ? undefined : ansichten.find((v) => v.name === gewaehlt) ?? (gewaehlt ? undefined : ansichten.find((v) => v.breite === width));
  const alle = value && typeof value === "object" ? value : {};
  const liste = Object.entries(alle).filter(([, a]) => a && Object.keys(a).length).map(([n, a]) => `${n}: ${Object.entries(a).map(([k, w]) => `${k} ${w}`).join(", ")}`);
  const wahl = (
    <select className="t-anp-ansicht" aria-label="Ansicht" value={ansicht?.name ?? BASIS} onChange={(e) => setGewaehlt(e.target.value)}>
      <option value={BASIS}>{BASIS} (Grundlage)</option>
      {ansichten.map((v) => <option key={v.name} value={v.name}>{v.name} – bis {v.breite} px</option>)}
    </select>);
  if (!ansicht) return <div className="t-anp"><b>Anpassungen je Ansicht</b>{wahl}<p>{BASIS} ist die Grundlage. Wähle eine kleinere Ansicht, um dort abzuweichen.</p>{liste.length > 0 && <p className="t-anp-liste">Überschrieben: {liste.join(" · ")}</p>}</div>;
  const eigen = alle[ansicht.name] || {};
  const setze = (feld, wert) => {
    const n = { ...eigen }; if (wert === undefined || wert === "" || Number.isNaN(wert)) delete n[feld]; else n[feld] = wert;
    const neu = { ...alle }; if (Object.keys(n).length) neu[ansicht.name] = n; else delete neu[ansicht.name];
    onChange(neu);
  };
  const zeile = (feld, label, eingabe) => {
    const { wert, von } = geerbt(alle, ansichten, ansicht.name, feld);
    const ueber = eigen[feld] !== undefined;
    return (
      <div className={"t-anp-zeile" + (ueber ? " t-anp-ueber" : "")} key={feld}>
        <label>{label}</label>{eingabe}
        {ueber ? <button type="button" className="t-anp-reset" onClick={() => setze(feld, undefined)}>Zurücksetzen</button>
          : <span className="t-anp-erbe">{von ? `geerbt aus ${von}: ${wert}` : `wie ${BASIS}`}</span>}
      </div>);
  };
  return (
    <div className="t-anp"><b>Anpassungen je Ansicht</b>
      {wahl}
      <p>Gilt für <b>{ansicht.name}</b> (bis {ansicht.breite} px) und alle kleineren Ansichten.</p>
      {zeile("ausblenden", "Sichtbarkeit", <select value={eigen.ausblenden === true ? "aus" : eigen.ausblenden === false ? "an" : ""} onChange={(e) => setze("ausblenden", e.target.value === "aus" ? true : e.target.value === "an" ? false : undefined)}><option value="">–</option><option value="aus">Ausblenden</option><option value="an">Wieder einblenden</option></select>)}
      {zeile("ausrichtung", "Ausrichtung", <select value={eigen.ausrichtung || ""} onChange={(e) => setze("ausrichtung", e.target.value)}><option value="">–</option><option value="left">Links</option><option value="center">Mitte</option><option value="right">Rechts</option></select>)}
      {zeile("schrift", "Schriftgröße %", <input type="number" min={50} max={200} step={5} value={eigen.schrift ?? ""} onChange={(e) => setze("schrift", e.target.value === "" ? undefined : Math.round(+e.target.value))} />)}
      {spalten && zeile("spalten", "Spalten", <select value={eigen.spalten ?? ""} onChange={(e) => setze("spalten", e.target.value === "" ? undefined : +e.target.value)}><option value="">–</option><option value="1">1</option><option value="2">2</option></select>)}
    </div>);
}
// ---------- W5: Komponenten ----------
// Original im Projekt (root.props.komponenten), Verwendung mit eigenen Überschreibungen; gleiche Regel wie in C# (WebDesignComponents.Instance): ids „verwendung--baustein“
export function instanz(def, verwendung, ueber) {
  return (def?.bausteine || []).map((b) => {
    const innen = b.props?.id || "b";
    return { ...b, props: { ...(b.props || {}), ...((ueber || {})[innen] || {}), id: `${verwendung}--${innen}` } };
  });
}
const komponentenVon = (data) => (Array.isArray(data?.root?.props?.komponenten) ? data.root.props.komponenten : []);
function KomponenteAnzeige({ komponente, ueberschreibungen, id }) {
  const data = React.useContext(DataCtx) || window.TRENDO_SEITE;
  const def = komponentenVon(data).find((k) => k.id === komponente);
  if (!def) return <div className="t-img t-empty">Komponente fehlt</div>;
  return <Render config={innerConfig} data={{ root: { props: {} }, content: instanz(def, id, ueberschreibungen), zones: {} }} />;
}
function KomponenteWahl({ value, onChange }) {
  const defs = usePuckSel((s) => komponentenVon(s.appState.data));
  return <select className="t-anp-ansicht" value={value || ""} onChange={(e) => onChange(e.target.value)}><option value="">– wählen –</option>{defs.map((k) => <option key={k.id} value={k.id}>{k.name}</option>)}</select>;
}
// Felder der Komponente: „Nur hier“ überschreibt in dieser Verwendung, „Original“ ändert alle Verwendungen
function UeberschreibungenFeld({ value, onChange }) {
  const data = usePuckSel((s) => s.appState.data);
  const sel = usePuckSel((s) => s.selectedItem?.props);
  const dispatch = usePuckSel((s) => s.dispatch);
  const [original, setOriginal] = useState(false);
  const defs = komponentenVon(data);
  const def = defs.find((k) => k.id === sel?.komponente);
  if (!def) return <div className="t-anp">Erst eine Komponente wählen.</div>;
  const ueber = value && typeof value === "object" ? value : {};
  const setzeHier = (innen, feld, wert) => {
    const e = { ...(ueber[innen] || {}) }; if (wert === undefined) delete e[feld]; else e[feld] = wert;
    const neu = { ...ueber }; if (Object.keys(e).length) neu[innen] = e; else delete neu[innen];
    onChange(neu);
  };
  const setzeOriginal = (innen, feld, wert) => {
    const kompo = defs.map((k) => k.id !== def.id ? k : { ...k, bausteine: k.bausteine.map((b) => b.props?.id !== innen ? b : { ...b, props: { ...b.props, [feld]: wert } }) });
    dispatch({ type: "setData", data: { ...data, root: { ...data.root, props: { ...data.root.props, komponenten: kompo } } } });
  };
  return (
    <div className="t-anp"><b>{def.name}</b>
      <label className="t-anp-zeile"><span>Original ändern (alle Verwendungen)</span><input type="checkbox" checked={original} onChange={(e) => setOriginal(e.target.checked)} /></label>
      {def.bausteine.map((b) => {
        const felder = Object.entries(baseConfig.components[b.type]?.fields || {}).filter(([, f]) => f.type === "text" || f.type === "textarea");
        const innen = b.props?.id;
        return felder.map(([feld, f]) => {
          const hier = ueber[innen]?.[feld];
          return (
            <div className={"t-anp-zeile" + (hier !== undefined ? " t-anp-ueber" : "")} key={innen + feld} data-feld={`${innen}.${feld}`}>
              <label>{(baseConfig.components[b.type]?.label || b.type) + " – " + (f.label || feld)}</label>
              <input type="text" value={original ? (b.props?.[feld] ?? "") : (hier ?? b.props?.[feld] ?? "")} onChange={(e) => (original ? setzeOriginal(innen, feld, e.target.value) : setzeHier(innen, feld, e.target.value))} />
              {!original && (hier !== undefined ? <button type="button" className="t-anp-reset" onClick={() => setzeHier(innen, feld, undefined)}>Zurücksetzen</button> : <span className="t-anp-erbe">wie im Original</span>)}
            </div>);
        });
      })}
    </div>);
}

const anpassungenFeld = (spalten = false) => ({ anpassungen: { type: "custom", label: "Anpassungen je Ansicht", render: ({ value, onChange }) => <AnpassungenFeld value={value} onChange={onChange} spalten={spalten} /> } });

// ---------- Bausteine ----------
const baseConfig = {
  categories: {
    aufbau: { title: "Aufbau", components: ["Bereich", "Spalten", "Stapel", "Komponente"] },
    inhalt: { title: "Inhalt", components: ["Kopfbereich", "Ueberschrift", "Text", "Bild", "Knopf"] },
    effekte: { title: "3D & Bewegung", components: ["Szene3D"] },
  },
  // Modul 05 Design-System: seitenweite Werte als CSS-Variablen – jeder Baustein nutzt sie; Trendo prüft die Werte beim Speichern (nur #Farben, sichere Schriftnamen)
  root: {
    fields: {
      title: { type: "text", label: "Seitentitel" }, description: { type: "textarea", label: "Beschreibung (Suchmaschinen)" },
      accent: { type: "text", label: "Akzentfarbe (#RRGGBB)" }, ink: { type: "text", label: "Textfarbe (#RRGGBB)" }, paper: { type: "text", label: "Hintergrund (#RRGGBB)" },
      font: { type: "select", label: "Schrift", options: [{ label: "Klar (Segoe/Helvetica)", value: "klar" }, { label: "Serif (Georgia)", value: "serif" }, { label: "Rund (Trebuchet)", value: "rund" }, { label: "Technisch (Consolas)", value: "mono" }] },
      radius: { type: "number", label: "Rundung (px)", min: 0, max: 40 }, space: { type: "number", label: "Abstand-Grundwert (px)", min: 4, max: 48 },
      dunkel: { type: "radio", label: "Dunkler Satz", options: [{ label: "Aus", value: false }, { label: "An", value: true }] },
      accentDunkel: { type: "text", label: "Akzent dunkel (#RRGGBB)" }, inkDunkel: { type: "text", label: "Text dunkel (#RRGGBB)" }, paperDunkel: { type: "text", label: "Hintergrund dunkel (#RRGGBB)" },
    },
    defaultProps: { title: "Neue Seite", description: "", accent: "#1d1d1f", ink: "#1d1d1f", paper: "#ffffff", font: "klar", radius: 12, space: 24, dunkel: false, accentDunkel: "#f0c75e", inkDunkel: "#f2f2f2", paperDunkel: "#121212" },
    render: ({ children, ...p }) => <PageRoot p={p}>{children}</PageRoot>,
  },
  components: {
    Kopfbereich: {
      label: "Kopfbereich",
      fields: { title: { type: "text", label: "Titel" }, sub: { type: "textarea", label: "Untertitel" }, cta: { type: "text", label: "Knopf-Text" }, href: { type: "text", label: "Knopf-Ziel" }, ...motionFields, ...anpassungenFeld() },
      defaultProps: { title: "Willkommen", sub: "Eine Seite, gebaut mit Trendo.", cta: "Mehr erfahren", href: "#", motion: "einblenden", seconds: 0.9 },
      render: ({ title, sub, cta, href, motion, seconds, id }) => (
        <Animated motion={motion} seconds={seconds} id={id}><header className="t-hero"><h1>{title}</h1><p>{sub}</p>{cta && <a className="t-btn" href={href}>{cta}</a>}</header></Animated>),
    },
    Ueberschrift: { label: "Überschrift", fields: { text: { type: "text", label: "Text" }, level: { type: "select", label: "Ebene", options: [{ label: "H2", value: "h2" }, { label: "H3", value: "h3" }] }, ...anpassungenFeld() },
      defaultProps: { text: "Überschrift", level: "h2" }, render: ({ text, level }) => React.createElement(level, { className: "t-h" }, text) },
    Text: { label: "Text", fields: { text: { type: "textarea", label: "Text" }, align: { type: "radio", label: "Ausrichtung", options: [{ label: "Links", value: "left" }, { label: "Mitte", value: "center" }] }, ...anpassungenFeld() },
      defaultProps: { text: "Hier steht dein Text.", align: "left" }, render: ({ text, align }) => <p className={`t-text t-a-${align === "center" ? "center" : "left"}`}>{text}</p> },
    Bild: { label: "Bild", fields: { src: { type: "text", label: "Bilddatei (im Projekt)" }, alt: { type: "text", label: "Beschreibung (Pflicht für Barrierefreiheit)" }, ...anpassungenFeld() },
      defaultProps: { src: "", alt: "" }, render: ({ src, alt }) => src ? <img className="t-img" src={src} alt={alt} loading="lazy" /> : <div className="t-img t-empty">Bild wählen</div> },
    Knopf: { label: "Knopf", fields: { label: { type: "text", label: "Text" }, href: { type: "text", label: "Ziel" }, ...anpassungenFeld() },
      defaultProps: { label: "Kontakt", href: "#kontakt" }, render: ({ label, href }) => <a className="t-btn" href={href}>{label}</a> },
    Bereich: { label: "Bereich", fields: { content: { type: "slot" }, tone: { type: "select", label: "Hintergrund", options: [{ label: "Hell", value: "hell" }, { label: "Dunkel", value: "dunkel" }] }, ...anpassungenFeld() },
      defaultProps: { tone: "hell" }, render: ({ content: Content, tone }) => <section className={`t-section t-${tone}`}><Content /></section> },
    Spalten: { label: "Spalten", fields: { left: { type: "slot" }, right: { type: "slot" }, ...anpassungenFeld(true) },
      render: ({ left: Left, right: Right }) => <div className="t-cols-box"><div className="t-cols"><div><Left /></div><div><Right /></div></div></div> },
    Komponente: { label: "Komponente", fields: {
        komponente: { type: "custom", label: "Komponente", render: ({ value, onChange }) => <KomponenteWahl value={value} onChange={onChange} /> },
        ueberschreibungen: { type: "custom", label: "Felder", render: ({ value, onChange }) => <UeberschreibungenFeld value={value} onChange={onChange} /> }, ...anpassungenFeld() },
      render: ({ komponente, ueberschreibungen, id }) => <KomponenteAnzeige komponente={komponente} ueberschreibungen={ueberschreibungen} id={id} /> },
    // A5: Stapel mit Abstand aus den Gestaltungswerten (Stufen statt Pixel), „passt sich an“ oder „füllt aus“, bricht bei schmaler Breite um
    Stapel: { label: "Stapel", fields: { inhalt: { type: "slot" },
        richtung: { type: "radio", label: "Richtung", options: [{ label: "Untereinander", value: "senkrecht" }, { label: "Nebeneinander", value: "waagerecht" }] },
        abstand: { type: "select", label: "Abstand", options: [0, 1, 2, 3, 4].map((n) => ({ label: ["Keiner", "Klein", "Mittel", "Groß", "Sehr groß"][n], value: n })) },
        breite: { type: "radio", label: "Breite der Teile", options: [{ label: "Passt sich an", value: "passt" }, { label: "Füllt aus", value: "fuellt" }] },
        ausrichtung: { type: "select", label: "Ausrichtung", options: [{ label: "Anfang", value: "start" }, { label: "Mitte", value: "mitte" }, { label: "Ende", value: "ende" }] }, ...anpassungenFeld() },
      defaultProps: { richtung: "senkrecht", abstand: 2, breite: "passt", ausrichtung: "start" },
      render: ({ inhalt: Inhalt, richtung, abstand, breite, ausrichtung }) => <Inhalt className={`t-stack t-stack-${richtung} t-stack-${breite} t-stack-a-${ausrichtung}`} style={{ gap: `calc(var(--t-space) * ${[0, 0.25, 0.5, 1, 1.5][abstand] ?? 0.5})` }} /> },
    Szene3D: { label: "3D-Szene", fields: { form: { type: "select", label: "Form", options: [{ label: "Würfel", value: "wuerfel" }, { label: "Kugel", value: "kugel" }, { label: "Knoten", value: "ring" }] },
        color: { type: "text", label: "Farbe" }, spin: { type: "number", label: "Drehung", min: 0, max: 3, step: 0.1 }, height: { type: "number", label: "Höhe (px)", min: 160, max: 900 }, ...anpassungenFeld() },
      defaultProps: { form: "ring", color: "#d4a73a", spin: 0.6, height: 380 }, render: (p) => <Scene3D {...p} /> },
  },
};

// Jeder Baustein steckt in einem Rahmen mit seiner id: daran hängen die Anpassungen je Ansicht (Editor, Vorschau und Export)
export const config = { ...baseConfig, components: Object.fromEntries(Object.entries(baseConfig.components).map(([name, c]) =>
  [name, { ...c, render: (p) => <div className="t-b" data-b={p.id}>{c.render(p)}</div> }])) };

// Bausteine einer Komponente: ohne eigene Seitenhülle
const innerConfig = { ...config, root: { render: ({ children }) => <>{children}</> } };

// Baustein per Doppelklick oder Eingabetaste am Seitenende einfügen (Bedienung ohne Ziehen, Tastatur, Barrierefreiheit)
function DrawerItem({ children, name }) {
  const dispatch = usePuckSel((s) => s.dispatch); const count = usePuckSel((s) => s.appState.data.content.length);
  const add = () => dispatch({ type: "insert", componentType: name, destinationIndex: count, destinationZone: "root:default-zone" });
  return <div className="t-drawer-item" data-baustein={name} tabIndex={0} title="Ziehen oder doppelklicken zum Einfügen" onDoubleClick={add} onKeyDown={(e) => { if (e.key === "Enter") add(); }}>{children}</div>;
}

const empty = { root: { props: { title: "Neue Seite", description: "" } }, content: [], zones: {} };
// Editor-Ansichten aus den Ansichten der Seite: Desktop als Grundlage, jede kleinere in ihrer Breite
const viewportsVon = (root) => [{ width: 1440, height: "auto", label: BASIS, icon: "Monitor" }, ...ansichtenVon(root).map((v) => ({ width: v.breite, height: "auto", label: v.name, icon: v.breite < 700 ? "Smartphone" : "Tablet" }))];

// Fertige Seite (Export): kein Editor, nur die Seite aus window.TRENDO_SEITE (seite.js neben index.html)
function Viewer() {
  const data = window.TRENDO_SEITE || empty;
  useEffect(() => { document.title = data.root?.props?.title || "Webseite"; const d = data.root?.props?.description; if (d) { let m = document.querySelector('meta[name="description"]'); if (!m) { m = document.createElement("meta"); m.name = "description"; document.head.appendChild(m); } m.content = d; } document.documentElement.dataset.fertig = "1"; }, [data]);
  return <Render config={config} data={data} />;
}

// ---------- K1/K2: Auswählen in der Vorschau, Text direkt ändern ----------
// Welche Stelle eines Bausteins welches Feld ist – Text wird über dieselbe Aktion „element_aendern“ gespeichert, die auch die KI nutzt.
const TEXT_FELDER = { Kopfbereich: { H1: "title", P: "sub", A: "cta" }, Ueberschrift: { H2: "text", H3: "text" }, Text: { P: "text" }, Knopf: { A: "label" } };

// Die Auswahl kommt über den Kontext: Die Baustein-Konfiguration bleibt dieselbe, sonst ersetzt React bei jedem Klick die Elemente und ein Doppelklick träfe ins Leere.
const SelCtx = React.createContext({ selected: [], toggle: () => {} });
const pendingToggles = new Map(); // Je Baustein: Ein Einzelklick wählt erst aus, wenn kein zweiter Klick folgt

function SelBox({ id, typ, children }) {
  const { selected, toggle } = React.useContext(SelCtx);
  const onClick = (e) => {
    e.preventDefault(); e.stopPropagation();
    if (e.target.isContentEditable || e.detail > 1) return;
    clearTimeout(pendingToggles.get(id));
    pendingToggles.set(id, setTimeout(() => { pendingToggles.delete(id); toggle(id); }, 240));
  };
  return <div className={"t-sel" + (selected.includes(id) ? " t-sel-on" : "")} data-tid={id} data-typ={typ} onClick={onClick}>{children}</div>;
}

const selectableConfig = { ...config, components: Object.fromEntries(Object.entries(config.components).map(([name, c]) =>
  [name, { ...c, render: (p) => <SelBox id={p.id} typ={name}>{c.render(p)}</SelBox> }])) };

function startTextEdit(e) {
  const el = e.target; const host = el.closest?.("[data-tid]"); if (!host) return;
  clearTimeout(pendingToggles.get(host.dataset.tid)); pendingToggles.delete(host.dataset.tid);
  const feld = TEXT_FELDER[host.dataset.typ]?.[el.tagName]; if (!feld) return;
  e.preventDefault(); e.stopPropagation();
  const before = el.textContent; el.contentEditable = "plaintext-only"; el.focus();
  const end = (save) => {
    el.removeEventListener("blur", onBlur); el.removeEventListener("keydown", onKey); el.contentEditable = "false";
    if (save && el.textContent !== before) send({ art: "aktion", daten: { aktionen: [{ aktion: "element_aendern", id: host.dataset.tid, props: { [feld]: el.textContent } }] } });
    else el.textContent = before;
  };
  const onBlur = () => end(true);
  const onKey = (k) => { if (k.key === "Escape") end(false); else if (k.key === "Enter" && !k.shiftKey) { k.preventDefault(); end(true); } };
  el.addEventListener("blur", onBlur); el.addEventListener("keydown", onKey);
}

function Selectable({ data, selected, setSelected, onBack }) {
  const toggle = (id) => setSelected((now) => now.includes(id) ? now.filter((x) => x !== id) : [...now, id]);
  const ctx = useMemo(() => ({ selected, toggle }), [selected]);
  return (
    <div className="t-preview" onDoubleClick={startTextEdit}>
      <div className="t-seltools"><span>{selected.length ? `${selected.length} ausgewählt` : "Klicken wählt aus · Doppelklick ändert Text"}</span>
        {selected.length > 0 && <button className="t-tool" onClick={() => setSelected([])}>Auswahl aufheben</button>}<button className="t-tool" onClick={onBack}>← Zurück zum Editor</button></div>
      <SelCtx.Provider value={ctx}><Render config={selectableConfig} data={data} /></SelCtx.Provider>
    </div>
  );
}

function App() {
  const [data, setData] = useState(() => { try { return JSON.parse(localStorage.getItem("trendo-entwurf") || "null") || empty; } catch { return empty; } });
  const [key, setKey] = useState(0); const [mode, setMode] = useState("editor"); // editor, vorschau, auswahl
  const [kiPreview, setKiPreview] = useState(null); // KI-Ergebnis, das noch nicht übernommen ist
  const [selected, setSelected] = useState([]);
  const [vorschauBreite, setVorschauBreite] = useState(null); // Breite der Vorschau je Ansicht (Container-Regeln greifen an der Seitenbreite)
  const dataRef = useRef(data); dataRef.current = data;
  const draftTimer = useRef(0);
  useEffect(() => {
    // nur einmal: Trendo antwortet auf „bereit“ mit der gespeicherten Seite (sonst Endlosschleife Laden → bereit → Laden)
    window.trendoLoad = (json) => { clearTimeout(draftTimer.current); const d = typeof json === "string" ? JSON.parse(json) : json; setData(d || empty); setKey((k) => k + 1); return true; };
    window.trendoData = () => JSON.stringify(dataRef.current);
    window.trendoVorschau = (json) => { setKiPreview(json ? (typeof json === "string" ? JSON.parse(json) : json) : null); return true; };
    window.trendoAuswahl = (ids) => { setSelected(ids || []); return true; };
    send({ art: "bereit" });
  }, []);
  useEffect(() => { send({ art: "auswahl", daten: { ids: selected } }); }, [selected]);
  const viewports = useMemo(() => viewportsVon(data.root), [key]);
  const ui = useMemo(() => { const v = viewports.find((x) => x.label.toLowerCase() === data.root?.props?.ansicht); return v ? { viewports: { current: { width: v.width, height: "auto" } } } : undefined; }, [key]);
  if (kiPreview) return <DataCtx.Provider value={kiPreview}><div className="t-preview"><div className="t-seltools"><span>Vorschau des KI-Vorschlags – noch nicht übernommen</span></div><Render config={config} data={kiPreview} /></div></DataCtx.Provider>;
  if (mode === "vorschau") return (
    <DataCtx.Provider value={data}><div className="t-preview">
      <div className="t-seltools"><span>Vorschau</span>
        {[{ name: BASIS, breite: null }, ...ansichtenVon(data.root)].map((v) => <button key={v.name} className={"t-tool" + (vorschauBreite === v.breite ? " t-tool-an" : "")} onClick={() => setVorschauBreite(v.breite)}>{v.name}</button>)}
        <button className="t-tool" onClick={() => setMode("editor")}>← Zurück zum Editor</button></div>
      <div className="t-vorschau-rahmen" style={vorschauBreite ? { maxWidth: vorschauBreite + "px" } : undefined}><Render config={config} data={data} /></div>
    </div></DataCtx.Provider>);
  if (mode === "auswahl") return <DataCtx.Provider value={data}><Selectable key={key} data={data} selected={selected} setSelected={setSelected} onBack={() => setMode("editor")} /></DataCtx.Provider>;
  return (
    <DataCtx.Provider value={data}><Puck key={key} config={config} data={data} viewports={viewports} ui={ui} iframe={{ enabled: location.protocol !== "file:" && !location.search.includes("ohneRahmen") }} dictionary={de}
      onChange={(d) => { setData(d); try { localStorage.setItem("trendo-entwurf", JSON.stringify(d)); } catch { } send({ art: "geaendert" }); clearTimeout(draftTimer.current); draftTimer.current = setTimeout(() => send({ art: "entwurf", daten: d }), 400); }}
      onPublish={(d) => send({ art: "speichern", daten: d })}
      overrides={{ headerActions: ({ children }) => <>{children}<button className="t-tool" onClick={() => setMode("auswahl")}>Auswählen</button><button className="t-tool" onClick={() => setMode("vorschau")}>Vorschau</button></>, drawerItem: DrawerItem }} /></DataCtx.Provider>
  );
}

createRoot(document.getElementById("root")).render(window.TRENDO_SEITE ? <Viewer /> : <App />);
