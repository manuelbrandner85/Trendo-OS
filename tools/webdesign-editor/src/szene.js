// Z3: gemeinsamer Szenenaufbau für 3D-Modelle (Bündel drei.js, WebGPU mit WebGL-Rückfall) und Gaussian Splats (Bündel splat.js, WebGL mit Spark).
// Ziel ist ein Bild wie mit einer echten Kamera: filmische Tonwertkurve (AgX), sRGB-Ausgabe, Spiegelungen aus einer Umgebung, eine Sonne mit echten Schatten
// und Kontaktschatten auf dem Boden, Kamera mit Brennweite auf Vollformat. Kein Bloom, keine Aufhelllichter ohne Quelle.
import * as THREE from "three";
import { OrbitControls } from "three/addons/controls/OrbitControls.js";
import { RoomEnvironment } from "three/addons/environments/RoomEnvironment.js";
import { HDRLoader } from "three/addons/loaders/HDRLoader.js";

const rad = (d) => (d * Math.PI) / 180;
const zahl = (v, d, lo, hi) => (Number.isFinite(+v) ? Math.min(hi, Math.max(lo, +v)) : d);
/** Vertikaler Bildwinkel aus der Brennweite auf Vollformat (36 × 24 mm). */
export const fovAusBrennweite = (mm) => (2 * Math.atan(24 / (2 * zahl(mm, 50, 18, 200))) * 180) / Math.PI;
const wenigerBewegung = () => typeof matchMedia === "function" && matchMedia("(prefers-reduced-motion: reduce)").matches;

/**
 * Szene aufbauen. inhalt(scene) lädt Modell oder Splat und liefert { objekt, box } (Bounding-Box in Weltkoordinaten).
 * Liefert eine Steuerung: aktualisieren(props), standbild(), fps(), entfernen().
 */
export async function starte({ renderer, el, props, datei, inhalt, backend }) {
  const scene = new THREE.Scene();
  renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
  renderer.outputColorSpace = THREE.SRGBColorSpace;
  renderer.toneMapping = THREE.AgXToneMapping;
  renderer.shadowMap.enabled = true;
  renderer.shadowMap.type = THREE.VSMShadowMap; // weiche Schattenkante wie bei einer echten Sonne (0,5° Winkeldurchmesser), nicht messerscharf
  el.appendChild(renderer.domElement);
  renderer.domElement.style.display = "block"; renderer.domElement.style.width = "100%"; renderer.domElement.style.height = "100%";

  const camera = new THREE.PerspectiveCamera(fovAusBrennweite(props.brennweite), 1, 0.01, 1000);
  const controls = new OrbitControls(camera, renderer.domElement);
  controls.enableDamping = true;

  // Inhalt laden und auf den Boden stellen; scheitert das, Renderer und Zeichenfläche wieder entfernen
  let geladen;
  try { geladen = await inhalt(scene); }
  catch (e) { controls.dispose(); renderer.setAnimationLoop(null); renderer.dispose(); renderer.domElement.remove(); throw e; }
  const { objekt, box } = geladen;
  const center = box.getCenter(new THREE.Vector3());
  const size = box.getSize(new THREE.Vector3());
  const radius = Math.max(size.length() / 2, 0.001);
  objekt.position.x -= center.x; objekt.position.z -= center.z; objekt.position.y -= box.min.y;
  const ziel = new THREE.Vector3(0, size.y / 2, 0);

  // Sonne mit Schatten; Schattenkamera eng um das Modell, damit Kontaktschatten scharf bleiben
  const sonne = new THREE.DirectionalLight(0xfff1e0, 3); // tageslichtnah (ca. 5500 K)
  sonne.castShadow = true;
  sonne.shadow.mapSize.set(2048, 2048);
  sonne.shadow.bias = -0.0002; sonne.shadow.normalBias = 0.02; sonne.shadow.radius = 3; sonne.shadow.blurSamples = 16;
  const sc = sonne.shadow.camera; sc.left = -radius * 2.6; sc.right = radius * 2.6; sc.top = radius * 2.6; sc.bottom = -radius * 2.6; sc.near = radius * 0.05; sc.far = radius * 12;
  scene.add(sonne); scene.add(sonne.target); sonne.target.position.copy(ziel);

  // Boden fängt nur den Schatten auf (die Seite bleibt sichtbar)
  const SchattenMaterial = THREE.ShadowNodeMaterial || THREE.ShadowMaterial;
  // Boden nur so groß wie der Bereich, den die Schattenkarte abdeckt – sonst zeichnet ihr Rand eine Linie auf den Boden
  const boden = new THREE.Mesh(new THREE.CircleGeometry(radius * 2.4, 96), new SchattenMaterial({ opacity: 0.38 }));
  boden.rotation.x = -Math.PI / 2; boden.receiveShadow = true;
  scene.add(boden);
  // Kontaktverschattung: wo das Objekt aufliegt, kommt kaum Himmelslicht hin – dunkle, weich auslaufende Zone unter der Grundfläche (Reality Check: „klebt nicht drauf“)
  const ao = document.createElement("canvas"); ao.width = ao.height = 128;
  const g = ao.getContext("2d"); const grd = g.createRadialGradient(64, 64, 0, 64, 64, 64);
  grd.addColorStop(0, "rgba(0,0,0,0.5)"); grd.addColorStop(0.45, "rgba(0,0,0,0.3)"); grd.addColorStop(1, "rgba(0,0,0,0)");
  g.fillStyle = grd; g.fillRect(0, 0, 128, 128);
  const aoTex = new THREE.CanvasTexture(ao);
  const kontakt = new THREE.Mesh(new THREE.PlaneGeometry(Math.max(size.x, 0.001) * 1.35, Math.max(size.z, 0.001) * 1.35), new THREE.MeshBasicMaterial({ map: aoTex, transparent: true, depthWrite: false, toneMapped: false }));
  kontakt.rotation.x = -Math.PI / 2; kontakt.position.y = radius * 0.0015; kontakt.renderOrder = 1;
  scene.add(kontakt);

  // Umgebung für Spiegelungen: Studio (ohne Datei) oder eigenes HDR-Bild
  const pmrem = new THREE.PMREMGenerator(renderer);
  let envTex = null, envKey = "";
  async function umgebung(p) {
    const key = p.umgebung === "hdri" && p.hdri ? "hdri:" + p.hdri : p.umgebung || "studio";
    if (key === envKey) return; envKey = key;
    if (envTex) { envTex.dispose(); envTex = null; }
    if (key.startsWith("hdri:")) {
      const bytes = await datei(p.hdri);
      if (bytes) { const tex = new HDRLoader().parse(bytes); const t = new THREE.DataTexture(tex.data, tex.width, tex.height, THREE.RGBAFormat, tex.type); t.colorSpace = THREE.LinearSRGBColorSpace; t.mapping = THREE.EquirectangularReflectionMapping; t.flipY = true; t.needsUpdate = true; envTex = pmrem.fromEquirectangular(t).texture; t.dispose(); }
    }
    if (!envTex && key !== "keine") envTex = pmrem.fromScene(new RoomEnvironment(), 0.04).texture;
    scene.environment = envTex;
  }

  // Material des Modells wahlweise ersetzen (Metall binär, Rauheit wie eingestellt)
  const original = new Map();
  objekt.traverse((o) => { if (o.isMesh) { o.castShadow = true; o.receiveShadow = true; original.set(o, o.material); } });
  function material(p) {
    for (const [mesh, mat] of original) {
      if (p.material === "eigen") {
        if (!mesh.userData.eigen) mesh.userData.eigen = new THREE.MeshStandardMaterial();
        const m = mesh.userData.eigen;
        m.color.set(/^#[0-9a-fA-F]{6}$/.test(p.farbe || "") ? p.farbe : "#8a8a8a"); m.roughness = zahl(p.rauheit, 0.5, 0, 1); m.metalness = p.metall === "ja" ? 1 : 0;
        if (mat && mat.normalMap) { m.normalMap = mat.normalMap; } // Oberflächenstruktur des Modells bleibt
        mesh.material = m;
      } else mesh.material = mat;
    }
  }

  let props0 = props;
  async function aktualisieren(p) {
    props0 = p;
    renderer.toneMappingExposure = zahl(p.belichtung, 1, 0.25, 4);
    camera.fov = fovAusBrennweite(p.brennweite);
    // Kamera so weit weg, dass das Modell bei dieser Brennweite ganz ins Bild passt
    const w = el.clientWidth || 800, h = el.clientHeight || 480;
    camera.aspect = w / h;
    const vfov = rad(camera.fov), hfov = 2 * Math.atan(Math.tan(vfov / 2) * camera.aspect);
    // Abstand aus den echten Maßen statt aus der Hüllkugel: Objekt füllt das Bild, mit etwas Luft (15 %)
    const halbH = size.y / 2, halbB = Math.max(size.x, size.z) / 2, halbT = Math.min(size.x, size.z) / 2;
    const abstand = Math.max(halbH / Math.tan(vfov / 2), halbB / Math.tan(hfov / 2)) * 1.15 + halbT;
    const az = rad(zahl(p.winkel, 30, -180, 180)), el2 = rad(zahl(p.neigung, 15, -10, 80));
    camera.position.set(Math.sin(az) * Math.cos(el2) * abstand, ziel.y + Math.sin(el2) * abstand, Math.cos(az) * Math.cos(el2) * abstand);
    camera.near = Math.max(abstand / 1000, 0.001); camera.far = abstand * 50;
    camera.updateProjectionMatrix();
    controls.target.copy(ziel); controls.enabled = p.bedienen !== "nein";
    controls.autoRotate = zahl(p.drehen, 0, 0, 2) > 0 && !wenigerBewegung(); controls.autoRotateSpeed = zahl(p.drehen, 0, 0, 2) * 2;
    controls.update();
    sonne.intensity = zahl(p.sonne, 3, 0, 10);
    const sa = rad(zahl(p.sonnenWinkel, -40, -180, 180)), sh = rad(zahl(p.sonnenHoehe, 40, 5, 90));
    sonne.position.set(Math.sin(sa) * Math.cos(sh) * radius * 6, ziel.y + Math.sin(sh) * radius * 6, Math.cos(sa) * Math.cos(sh) * radius * 6);
    sonne.castShadow = p.schatten !== "nein"; boden.visible = p.schatten !== "nein"; kontakt.visible = p.schatten !== "nein";
    material(p);
    await umgebung(p);
  }

  function groesse() { const w = el.clientWidth || 800, h = el.clientHeight || 480; renderer.setSize(w, h, false); camera.aspect = w / h; camera.updateProjectionMatrix(); }
  const ro = new ResizeObserver(groesse); ro.observe(el); groesse();
  await aktualisieren(props);

  // Bildrate messen (für den PC-Lauf und die Prüfung): Bilder je Sekunde über ein gleitendes Fenster
  let frames = 0, seit = performance.now(), fps = 0;
  renderer.setAnimationLoop(() => {
    controls.update();
    renderer.render(scene, camera);
    frames++; const jetzt = performance.now();
    if (jetzt - seit >= 1000) { fps = (frames * 1000) / (jetzt - seit); frames = 0; seit = jetzt; el.dataset.fps = fps.toFixed(1); }
  });
  el.dataset.backend = backend(); el.dataset.bereit = "1";

  return {
    aktualisieren,
    fps: () => fps,
    backend,
    standbild() { renderer.render(scene, camera); return renderer.domElement.toDataURL("image/png"); },
    entfernen() { aoTex.dispose(); ro.disconnect(); renderer.setAnimationLoop(null); controls.dispose(); pmrem.dispose(); if (envTex) envTex.dispose(); renderer.dispose(); renderer.domElement.remove(); },
    get props() { return props0; },
  };
}
