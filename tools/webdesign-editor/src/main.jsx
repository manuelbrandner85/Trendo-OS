// Trendo Web Design Studio – Editor-Kern (Puck, MIT) mit 3D-Block (React Three Fiber, MIT) und Bewegungen (Web Animations API).
// Läuft im eingebetteten WebView von Trendo. Laden: Trendo ruft window.trendoLoad(json); Speichern: invokeCSharpAction({art:"speichern", daten}).
import React, { useEffect, useMemo, useRef, useState } from "react";
import { createRoot } from "react-dom/client";
import { Puck, Render, createUsePuck } from "@puckeditor/core";
import "@puckeditor/core/puck.css";
import { Canvas, useFrame } from "@react-three/fiber";
import "./trendo.css";
import { de } from "./de.js";

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
const FONTS = { klar: '"Segoe UI", Helvetica, Arial, sans-serif', serif: 'Georgia, "Times New Roman", serif', rund: '"Trebuchet MS", "Segoe UI", sans-serif', mono: 'Consolas, "Courier New", monospace' };
const hex = (v, d) => (typeof v === "string" && /^#[0-9a-fA-F]{6}$/.test(v) ? v : d);
const num = (v, d, lo, hi) => (Number.isFinite(+v) ? Math.min(hi, Math.max(lo, +v)) : d);
export function tokens(t) {
  return { "--t-accent": hex(t.accent, "#1d1d1f"), "--t-ink": hex(t.ink, "#1d1d1f"), "--t-paper": hex(t.paper, "#ffffff"), "--t-font": FONTS[t.font] || FONTS.klar,
    "--t-radius": num(t.radius, 12, 0, 40) + "px", "--t-space": num(t.space, 24, 4, 48) + "px" };
}

// ---------- Bausteine ----------
export const config = {
  categories: {
    aufbau: { title: "Aufbau", components: ["Bereich", "Spalten"] },
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
    },
    defaultProps: { title: "Neue Seite", description: "", accent: "#1d1d1f", ink: "#1d1d1f", paper: "#ffffff", font: "klar", radius: 12, space: 24 },
    render: ({ children, accent, ink, paper, font, radius, space }) => <div className="t-page" style={tokens({ accent, ink, paper, font, radius, space })}>{children}</div>,
  },
  components: {
    Kopfbereich: {
      label: "Kopfbereich",
      fields: { title: { type: "text", label: "Titel" }, sub: { type: "textarea", label: "Untertitel" }, cta: { type: "text", label: "Knopf-Text" }, href: { type: "text", label: "Knopf-Ziel" }, ...motionFields },
      defaultProps: { title: "Willkommen", sub: "Eine Seite, gebaut mit Trendo.", cta: "Mehr erfahren", href: "#", motion: "einblenden", seconds: 0.9 },
      render: ({ title, sub, cta, href, motion, seconds, id }) => (
        <Animated motion={motion} seconds={seconds} id={id}><header className="t-hero"><h1>{title}</h1><p>{sub}</p>{cta && <a className="t-btn" href={href}>{cta}</a>}</header></Animated>),
    },
    Ueberschrift: { label: "Überschrift", fields: { text: { type: "text", label: "Text" }, level: { type: "select", label: "Ebene", options: [{ label: "H2", value: "h2" }, { label: "H3", value: "h3" }] } },
      defaultProps: { text: "Überschrift", level: "h2" }, render: ({ text, level }) => React.createElement(level, { className: "t-h" }, text) },
    Text: { label: "Text", fields: { text: { type: "textarea", label: "Text" }, align: { type: "radio", label: "Ausrichtung", options: [{ label: "Links", value: "left" }, { label: "Mitte", value: "center" }] } },
      defaultProps: { text: "Hier steht dein Text.", align: "left" }, render: ({ text, align }) => <p className="t-text" style={{ textAlign: align }}>{text}</p> },
    Bild: { label: "Bild", fields: { src: { type: "text", label: "Bilddatei (im Projekt)" }, alt: { type: "text", label: "Beschreibung (Pflicht für Barrierefreiheit)" } },
      defaultProps: { src: "", alt: "" }, render: ({ src, alt }) => src ? <img className="t-img" src={src} alt={alt} loading="lazy" /> : <div className="t-img t-empty">Bild wählen</div> },
    Knopf: { label: "Knopf", fields: { label: { type: "text", label: "Text" }, href: { type: "text", label: "Ziel" } },
      defaultProps: { label: "Kontakt", href: "#kontakt" }, render: ({ label, href }) => <a className="t-btn" href={href}>{label}</a> },
    Bereich: { label: "Bereich", fields: { content: { type: "slot" }, tone: { type: "select", label: "Hintergrund", options: [{ label: "Hell", value: "hell" }, { label: "Dunkel", value: "dunkel" }] } },
      defaultProps: { tone: "hell" }, render: ({ content: Content, tone }) => <section className={`t-section t-${tone}`}><Content /></section> },
    Spalten: { label: "Spalten", fields: { left: { type: "slot" }, right: { type: "slot" } },
      render: ({ left: Left, right: Right }) => <div className="t-cols"><div><Left /></div><div><Right /></div></div> },
    Szene3D: { label: "3D-Szene", fields: { form: { type: "select", label: "Form", options: [{ label: "Würfel", value: "wuerfel" }, { label: "Kugel", value: "kugel" }, { label: "Knoten", value: "ring" }] },
        color: { type: "text", label: "Farbe" }, spin: { type: "number", label: "Drehung", min: 0, max: 3, step: 0.1 }, height: { type: "number", label: "Höhe (px)", min: 160, max: 900 } },
      defaultProps: { form: "ring", color: "#d4a73a", spin: 0.6, height: 380 }, render: (p) => <Scene3D {...p} /> },
  },
};

// Baustein per Doppelklick oder Eingabetaste am Seitenende einfügen (Bedienung ohne Ziehen, Tastatur, Barrierefreiheit)
const usePuckSel = createUsePuck();
function DrawerItem({ children, name }) {
  const dispatch = usePuckSel((s) => s.dispatch); const count = usePuckSel((s) => s.appState.data.content.length);
  const add = () => dispatch({ type: "insert", componentType: name, destinationIndex: count, destinationZone: "root:default-zone" });
  return <div className="t-drawer-item" data-baustein={name} tabIndex={0} title="Ziehen oder doppelklicken zum Einfügen" onDoubleClick={add} onKeyDown={(e) => { if (e.key === "Enter") add(); }}>{children}</div>;
}

const empty = { root: { props: { title: "Neue Seite", description: "" } }, content: [], zones: {} };
const viewports = [{ width: 390, height: "auto", label: "Handy", icon: "Smartphone" }, { width: 820, height: "auto", label: "Tablet", icon: "Tablet" }, { width: 1440, height: "auto", label: "Desktop", icon: "Monitor" }];

// Fertige Seite (Export): kein Editor, nur die Seite aus window.TRENDO_SEITE (seite.js neben index.html)
function Viewer() {
  const data = window.TRENDO_SEITE || empty;
  useEffect(() => { document.title = data.root?.props?.title || "Webseite"; const d = data.root?.props?.description; if (d) { let m = document.querySelector('meta[name="description"]'); if (!m) { m = document.createElement("meta"); m.name = "description"; document.head.appendChild(m); } m.content = d; } }, [data]);
  return <Render config={config} data={data} />;
}

function App() {
  const [data, setData] = useState(() => { try { return JSON.parse(localStorage.getItem("trendo-entwurf") || "null") || empty; } catch { return empty; } });
  const [key, setKey] = useState(0); const [preview, setPreview] = useState(false);
  const dataRef = useRef(data); dataRef.current = data;
  useEffect(() => {
    // nur einmal: Trendo antwortet auf „bereit“ mit der gespeicherten Seite (sonst Endlosschleife Laden → bereit → Laden)
    window.trendoLoad = (json) => { const d = typeof json === "string" ? JSON.parse(json) : json; setData(d || empty); setKey((k) => k + 1); return true; };
    window.trendoData = () => JSON.stringify(dataRef.current);
    send({ art: "bereit" });
  }, []);
  if (preview) return <div className="t-preview"><button className="t-back" onClick={() => setPreview(false)}>← Zurück zum Editor</button><Render config={config} data={data} /></div>;
  return (
    <Puck key={key} config={config} data={data} viewports={viewports} iframe={{ enabled: location.protocol !== "file:" && !location.search.includes("ohneRahmen") }} dictionary={de}
      onChange={(d) => { setData(d); try { localStorage.setItem("trendo-entwurf", JSON.stringify(d)); } catch { } send({ art: "geaendert" }); }}
      onPublish={(d) => send({ art: "speichern", daten: d })}
      overrides={{ headerActions: ({ children }) => <>{children}<button className="t-tool" onClick={() => setPreview(true)}>Vorschau</button></>, drawerItem: DrawerItem }} />
  );
}

createRoot(document.getElementById("root")).render(window.TRENDO_SEITE ? <Viewer /> : <App />);
