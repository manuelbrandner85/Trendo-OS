// K7: Daten für fertige Websites – lokal zuerst. SQLite (sql.js, MIT) läuft im Browser der Besucherin oder des Besuchers, die Datenbank liegt auf deren Gerät (IndexedDB).
// Anmeldung lokal: Passwörter nur als PBKDF2-Hash (WebCrypto) mit eigenem Salz. Optional zusätzlich Supabase mit dem eigenen Projekt und nur dem öffentlichen Schlüssel.
// Diese Datei kommt nur in Seiten, die Daten-Bausteine nutzen. Sie lädt nichts aus dem Netz (WebAssembly ist eingebettet).
import initSqlJs from "sql.js/dist/sql-wasm.js";
import wasmUrl from "virtual:sql-wasm";

const NAME = /^[a-z][a-z0-9_]{0,29}$/;
const cfg = () => (window.TRENDO_DATEN && typeof window.TRENDO_DATEN === "object" ? window.TRENDO_DATEN : {});
let dbPromise = null;

function bytesFromDataUrl(u) { const b = atob(u.slice(u.indexOf(",") + 1)); const a = new Uint8Array(b.length); for (let i = 0; i < b.length; i++) a[i] = b.charCodeAt(i); return a; }

// IndexedDB: eine Datenbankdatei je Website (Schlüssel aus der Konfiguration)
function idb() {
  return new Promise((res, rej) => { const r = indexedDB.open("trendo-daten", 1); r.onupgradeneeded = () => r.result.createObjectStore("db"); r.onsuccess = () => res(r.result); r.onerror = () => rej(r.error); });
}
async function laden() { const d = await idb(); return new Promise((res) => { const t = d.transaction("db").objectStore("db").get(cfg().schluessel || "website"); t.onsuccess = () => res(t.result || null); t.onerror = () => res(null); }); }
async function sichern(db) { const d = await idb(); const bytes = db.export(); return new Promise((res, rej) => { const t = d.transaction("db", "readwrite"); t.objectStore("db").put(bytes, cfg().schluessel || "website"); t.oncomplete = res; t.onerror = () => rej(t.error); }); }

function open() {
  if (!dbPromise) dbPromise = (async () => {
    const SQL = await initSqlJs({ wasmBinary: bytesFromDataUrl(wasmUrl) });
    const alt = await laden().catch(() => null);
    const db = alt ? new SQL.Database(alt) : new SQL.Database();
    db.run("CREATE TABLE IF NOT EXISTS eintraege (id INTEGER PRIMARY KEY AUTOINCREMENT, tabelle TEXT NOT NULL, erstellt TEXT NOT NULL, nutzer TEXT, daten TEXT NOT NULL)");
    db.run("CREATE TABLE IF NOT EXISTS nutzer (name TEXT PRIMARY KEY, salz TEXT NOT NULL, hash TEXT NOT NULL, erstellt TEXT NOT NULL)");
    return db;
  })();
  return dbPromise;
}

const hex = (buf) => [...new Uint8Array(buf)].map((b) => b.toString(16).padStart(2, "0")).join("");
async function hash(passwort, salzHex) {
  const salz = new Uint8Array(salzHex.match(/../g).map((h) => parseInt(h, 16)));
  const key = await crypto.subtle.importKey("raw", new TextEncoder().encode(passwort), "PBKDF2", false, ["deriveBits"]);
  return hex(await crypto.subtle.deriveBits({ name: "PBKDF2", hash: "SHA-256", salt: salz, iterations: 210000 }, key, 256));
}

// Supabase nur mit eigenem Projekt: https://<projekt>.supabase.co und öffentlicher Schlüssel. Ohne Angaben bleibt alles auf dem Gerät.
function supabase() { const s = cfg().supabase; return s && /^https:\/\/[a-z0-9-]+\.supabase\.co$/.test(s.url || "") && typeof s.schluessel === "string" ? s : null; }
async function nachSupabase(tabelle, row) {
  const s = supabase(); if (!s) return;
  try { await fetch(`${s.url}/rest/v1/${encodeURIComponent(tabelle)}`, { method: "POST", headers: { apikey: s.schluessel, Authorization: `Bearer ${s.schluessel}`, "Content-Type": "application/json", Prefer: "return=minimal" }, body: JSON.stringify(row) }); }
  catch { /* offline: der Eintrag bleibt lokal gespeichert */ }
}

const listeners = new Set();
const melden = () => listeners.forEach((f) => { try { f(); } catch { } });

window.TrendoDaten = {
  bereit: true,
  beobachten(f) { listeners.add(f); return () => listeners.delete(f); },
  aktuell() { try { return sessionStorage.getItem("trendo-nutzer") || ""; } catch { return ""; } },
  async registrieren(name, passwort) {
    name = String(name || "").trim();
    if (!/^[\p{L}0-9._-]{3,40}$/u.test(name)) return "Name bitte mit 3 bis 40 Zeichen (Buchstaben, Ziffern, Punkt, Bindestrich).";
    if (String(passwort || "").length < 10) return "Das Passwort braucht mindestens 10 Zeichen.";
    const db = await open();
    const r = db.exec("SELECT 1 FROM nutzer WHERE name = ?", [name]); if (r.length) return "Diesen Namen gibt es auf diesem Gerät schon.";
    const salz = hex(crypto.getRandomValues(new Uint8Array(16)));
    db.run("INSERT INTO nutzer (name, salz, hash, erstellt) VALUES (?, ?, ?, ?)", [name, salz, await hash(passwort, salz), new Date().toISOString()]);
    await sichern(db);
    try { sessionStorage.setItem("trendo-nutzer", name); } catch { }
    melden(); return null;
  },
  async anmelden(name, passwort) {
    const db = await open();
    const r = db.exec("SELECT salz, hash FROM nutzer WHERE name = ?", [String(name || "").trim()]);
    if (!r.length || (await hash(String(passwort || ""), r[0].values[0][0])) !== r[0].values[0][1]) return "Name oder Passwort stimmt nicht.";
    try { sessionStorage.setItem("trendo-nutzer", String(name).trim()); } catch { }
    melden(); return null;
  },
  abmelden() { try { sessionStorage.removeItem("trendo-nutzer"); } catch { } melden(); },
  async speichern(tabelle, werte) {
    if (!NAME.test(tabelle)) return "Ungültige Tabelle.";
    const db = await open();
    const row = {}; for (const [k, v] of Object.entries(werte || {})) if (NAME.test(k)) row[k] = String(v).slice(0, 5000);
    db.run("INSERT INTO eintraege (tabelle, erstellt, nutzer, daten) VALUES (?, ?, ?, ?)", [tabelle, new Date().toISOString(), this.aktuell(), JSON.stringify(row)]);
    await sichern(db);
    nachSupabase(tabelle, row);
    melden(); return null;
  },
  async liste(tabelle, nurEigene) {
    if (!NAME.test(tabelle)) return [];
    const db = await open();
    const r = nurEigene ? db.exec("SELECT erstellt, nutzer, daten FROM eintraege WHERE tabelle = ? AND nutzer = ? ORDER BY id DESC LIMIT 200", [tabelle, this.aktuell()])
      : db.exec("SELECT erstellt, nutzer, daten FROM eintraege WHERE tabelle = ? ORDER BY id DESC LIMIT 200", [tabelle]);
    return r.length ? r[0].values.map(([erstellt, nutzer, daten]) => ({ erstellt, nutzer, ...JSON.parse(daten) })) : [];
  },
};
window.dispatchEvent(new Event("trendo-daten"));
