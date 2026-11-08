// Trendo – IFC-Dateien (BIM) in 3D ansehen. web-ifc (ThatOpen, MPL-2.0, unverändert) liest die Datei, three.js (MIT) zeichnet sie.
// Läuft unter file:// ohne Netz: die WASM-Datei steckt im Bündel, die IFC-Datei schickt Trendo über die Brücke (Base64).
import * as THREE from "three";
import { OrbitControls } from "three/examples/jsm/controls/OrbitControls.js";
import * as WebIFC from "web-ifc";
import wasmUrl from "web-ifc/web-ifc.wasm?url&inline";
import "./trendo.css";

const hinweis = document.getElementById("hinweis");
function senden(n) { const s = JSON.stringify(n); if (window.invokeCSharpAction) window.invokeCSharpAction(s); else if (window.chrome?.webview) window.chrome.webview.postMessage(s); }

// alpha: true – ohne Alpha-Kanal blieb die Fläche im Trendo-WebView auf dem Bildschirm weiß (Bild per CDP war richtig; echter Lauf 08.11.2026)
const renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true });
renderer.setPixelRatio(Math.min(devicePixelRatio, 2)); renderer.outputColorSpace = THREE.SRGBColorSpace; renderer.toneMapping = THREE.ACESFilmicToneMapping; renderer.toneMappingExposure = 0.75; // gedämpft: hellere WebView-Darstellung (HDR) soll Wände nicht weiß überstrahlen
document.getElementById("szene").appendChild(renderer.domElement);
// Dunkler Hintergrund (Trendo-Look): auf Bildschirmen, die den WebView heller darstellen (HDR), verschwanden helle Wände vor hellem Grund (echter Lauf 08.11.2026)
const scene = new THREE.Scene(); scene.background = new THREE.Color(0x1b1f27);
const camera = new THREE.PerspectiveCamera(45, 1, 0.05, 2000); camera.position.set(12, 10, 12);
const controls = new OrbitControls(camera, renderer.domElement); controls.enableDamping = true;
scene.add(new THREE.HemisphereLight(0xf4f7ff, 0x5a5348, 0.9));
const sun = new THREE.DirectionalLight(0xfff4e5, 1.5); sun.position.set(18, 30, 10); scene.add(sun);
const grid = new THREE.GridHelper(60, 60, 0x6d5a2e, 0x343a46); scene.add(grid);
let modell = new THREE.Group(); scene.add(modell);
function groesse() { const e = renderer.domElement.parentElement; const w = e.clientWidth || innerWidth, h = e.clientHeight || innerHeight; renderer.setSize(w, h); camera.aspect = w / h; camera.updateProjectionMatrix(); }
addEventListener("resize", groesse); groesse();
renderer.setAnimationLoop(() => { controls.update(); renderer.render(scene, camera); });

const api = new WebIFC.IfcAPI();
const bereit = api.Init((pfad) => pfad.endsWith(".wasm") ? wasmUrl : pfad, true);

function zuBytes(b64) { const s = atob(b64); const u = new Uint8Array(s.length); for (let i = 0; i < s.length; i++) u[i] = s.charCodeAt(i); return u; }

// Trendo ruft window.trendoIfc(base64, name) auf
window.trendoIfc = async (b64, name) => {
  try {
    await bereit; hinweis.textContent = "Lade " + name + " …";
    scene.remove(modell); modell.traverse((o) => { o.geometry?.dispose(); o.material?.dispose?.(); }); modell = new THREE.Group(); scene.add(modell);
    const id = api.OpenModel(zuBytes(b64), { COORDINATE_TO_ORIGIN: true });
    const mats = new Map(); let teile = 0;
    api.StreamAllMeshes(id, (mesh) => {
      if (api.GetLineType(id, mesh.expressID) === WebIFC.IFCSPACE) return; // Räume sind Volumen – sie würden die Wände verdecken
      const n = mesh.geometries.size();
      for (let i = 0; i < n; i++) {
        const pg = mesh.geometries.get(i); const g = api.GetGeometry(id, pg.geometryExpressID);
        const v = api.GetVertexArray(g.GetVertexData(), g.GetVertexDataSize()); const idx = api.GetIndexArray(g.GetIndexData(), g.GetIndexDataSize());
        const geo = new THREE.BufferGeometry(); const pos = new Float32Array(v.length / 2), nor = new Float32Array(v.length / 2);
        for (let k = 0, j = 0; k < v.length; k += 6, j += 3) { pos[j] = v[k]; pos[j + 1] = v[k + 1]; pos[j + 2] = v[k + 2]; nor[j] = v[k + 3]; nor[j + 1] = v[k + 4]; nor[j + 2] = v[k + 5]; }
        geo.setAttribute("position", new THREE.BufferAttribute(pos, 3)); geo.setAttribute("normal", new THREE.BufferAttribute(nor, 3)); geo.setIndex(new THREE.BufferAttribute(idx, 1));
        const c = pg.color; const key = [c.x, c.y, c.z, c.w].map((x) => x.toFixed(3)).join();
        if (!mats.has(key)) mats.set(key, new THREE.MeshStandardMaterial({ color: new THREE.Color(c.x, c.y, c.z), roughness: 0.85, metalness: 0, transparent: c.w < 0.999, opacity: c.w, side: THREE.DoubleSide }));
        const m = new THREE.Mesh(geo, mats.get(key)); m.matrix.fromArray(pg.flatTransformation); m.matrixAutoUpdate = false; modell.add(m); teile++;
        g.delete();
      }
    });
    const zaehl = (t) => api.GetLineIDsWithType(id, t).size();
    const info = { teile, waende: zaehl(WebIFC.IFCWALL) + zaehl(WebIFC.IFCWALLSTANDARDCASE), tueren: zaehl(WebIFC.IFCDOOR), fenster: zaehl(WebIFC.IFCWINDOW), raeume: zaehl(WebIFC.IFCSPACE) };
    api.CloseModel(id);
    const box = new THREE.Box3().setFromObject(modell); const mitte = box.getCenter(new THREE.Vector3()); const r = box.getSize(new THREE.Vector3()).length() || 10;
    controls.target.copy(mitte); camera.position.copy(mitte).add(new THREE.Vector3(r * 0.7, r * 0.6, r * 0.7)); camera.near = r / 500; camera.far = r * 50; camera.updateProjectionMatrix();
    grid.position.y = box.min.y - 0.001;
    hinweis.textContent = `${name}: ${info.waende} Wände, ${info.tueren} Türen, ${info.fenster} Fenster, ${info.raeume} Räume`;
    window.trendoIfcInfo = info; senden({ art: "geladen", ...info });
  } catch (e) { hinweis.textContent = "Fehler: " + (e?.message || e); senden({ art: "fehler", text: String(e?.message || e) }); }
};
bereit.then(() => { hinweis.textContent = ""; senden({ art: "bereit" }); }, (e) => { hinweis.textContent = "web-ifc startet nicht: " + e; senden({ art: "fehler", text: String(e) }); });
