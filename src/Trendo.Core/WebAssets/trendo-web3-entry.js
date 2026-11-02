/* Quelle für das lokale Bündel trendo-three.js. Wird nicht selbst in die Webseite kopiert. */
import * as THREE from 'three';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';
import { RoomEnvironment } from 'three/addons/environments/RoomEnvironment.js';

function farbe(hex) {
  const h = String(hex || '#2f6fed').replace('#', '');
  if (!/^[0-9a-fA-F]{6}$/.test(h)) return new THREE.Color('#2f6fed');
  return new THREE.Color(parseInt(h.slice(0, 2), 16) / 255, parseInt(h.slice(2, 4), 16) / 255, parseInt(h.slice(4, 6), 16) / 255);
}

function start(canvas, opts) {
  opts = opts || {};
  const reduce = window.matchMedia && matchMedia('(prefers-reduced-motion: reduce)').matches;
  let renderer;
  try {
    renderer = new THREE.WebGLRenderer({ canvas, antialias: true, alpha: false, preserveDrawingBuffer: true });
  } catch (e) {
    hinweis('WebGL fehlt auf diesem Gerät. Der Text der Seite bleibt nutzbar.');
    if (opts.fertig) opts.fertig();
    return;
  }
  renderer.setClearColor(farbe(opts.hintergrund || '#10141c'), 1);
  renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
  const scene = new THREE.Scene();
  const camera = new THREE.PerspectiveCamera(32, 1, 0.1, 100);
  camera.position.set(2.4, 1.45, 3.8);
  camera.lookAt(0, 0.05, 0);
  scene.add(new THREE.AmbientLight(0xffffff, 0.7));
  const key = new THREE.DirectionalLight(0xffffff, 1.6);
  key.position.set(2.5, 3.2, 4);
  scene.add(key);
  const fill = new THREE.DirectionalLight(0xb9d4ff, 0.4);
  fill.position.set(-3, 1, -1);
  scene.add(fill);
  const root = new THREE.Group();
  scene.add(root);
  const boden = new THREE.Mesh(
    new THREE.CircleGeometry(1.4, 48),
    new THREE.MeshStandardMaterial({ color: 0x1a1f2b, roughness: 0.9, metalness: 0 })
  );
  boden.rotation.x = -Math.PI / 2;
  boden.position.y = -0.72;
  scene.add(boden);

  let spin = 0.4;
  let bereit = false;
  function knopf(el, delta) {
    if (!el) return;
    el.addEventListener('click', function () { spin += delta; zeichnen(); });
  }
  knopf(opts.links, -Math.PI / 10);
  knopf(opts.rechts, Math.PI / 10);

  let ziehen = false;
  let lastX = 0;
  canvas.addEventListener('pointerdown', function (e) {
    ziehen = true; lastX = e.clientX;
    try { canvas.setPointerCapture(e.pointerId); } catch (err) { /* ältere Ansicht */ }
  });
  canvas.addEventListener('pointerup', function () { ziehen = false; });
  canvas.addEventListener('pointercancel', function () { ziehen = false; });
  canvas.addEventListener('pointermove', function (e) {
    if (!ziehen) return;
    spin += (e.clientX - lastX) * 0.01;
    lastX = e.clientX;
    zeichnen();
  });

  function groesse() {
    const w = canvas.clientWidth || canvas.width || 800;
    const h = canvas.clientHeight || canvas.height || 480;
    renderer.setSize(w, h, false);
    camera.aspect = w / Math.max(1, h);
    camera.updateProjectionMatrix();
  }

  function platzieren(objekt) {
    const box = new THREE.Box3().setFromObject(objekt);
    const size = box.getSize(new THREE.Vector3());
    const center = box.getCenter(new THREE.Vector3());
    objekt.position.sub(center);
    const max = Math.max(size.x, size.y, size.z) || 1;
    objekt.scale.multiplyScalar(1.05 / max);
    objekt.position.y += 0.05;
    root.add(objekt);
  }

  function ersatz() {
    while (root.children.length) root.remove(root.children[0]);
    const mat = new THREE.MeshStandardMaterial({ color: farbe(opts.farbe), roughness: 0.32, metalness: 0.18 });
    platzieren(new THREE.Mesh(new THREE.TorusKnotGeometry(0.48, 0.15, 120, 16), mat));
    bereit = true;
    zeichnen();
    if (opts.fertig) opts.fertig();
  }

  function zeichnen() {
    groesse();
    const max = document.documentElement.scrollHeight - window.innerHeight;
    const scroll = opts.bewegung === 'scroll' && max > 0 ? Math.min(1, window.scrollY / max) : null;
    root.rotation.y = spin + (scroll === null ? 0 : scroll * Math.PI * 2);
    root.rotation.x = scroll === null ? 0.15 : 0.1 + scroll * 0.5;
    renderer.render(scene, camera);
  }

  const b64 = (opts.modell || '').replace(/\s+/g, '');
  const loader = new GLTFLoader();
  function zeige(gltf) {
    platzieren(gltf.scene);
    bereit = true;
    zeichnen();
    if (opts.fertig) opts.fertig();
  }
  function kaputt() {
    hinweis('Das Modell konnte nicht gelesen werden. Gezeigt wird eine Ersatzform.');
    ersatz();
  }
  if (b64) {
    try {
      const bin = Uint8Array.from(atob(b64), function (c) { return c.charCodeAt(0); });
      loader.parse(bin.buffer, '', zeige, kaputt);
    } catch (e) { kaputt(); }
  } else if (opts.url) {
    loader.load(opts.url, zeige, undefined, kaputt);
  } else ersatz();

  function schleife() {
    if (bereit && !reduce && opts.bewegung !== 'scroll' && !ziehen) spin += 0.008;
    if (bereit) zeichnen();
    requestAnimationFrame(schleife);
  }
  requestAnimationFrame(schleife);
  window.addEventListener('resize', zeichnen);
  window.addEventListener('scroll', function () { if (opts.bewegung === 'scroll') zeichnen(); }, { passive: true });
}

function hinweis(text) {
  const el = document.getElementById('modell-hinweis');
  if (el) el.textContent = text;
}

/* 0.9.47 – „Immersiv“: fotorealistische Szene, die sich beim Scrollen bewegt (Kamerafahrt über die Kapitel).
   Physikalisch korrekte Materialien (Gold, Glas), Umgebungsspiegelung, weiche Schatten, filmische Tonwerte. */
function immersiv(canvas, opts) {
  opts = opts || {};
  const reduce = window.matchMedia && matchMedia('(prefers-reduced-motion: reduce)').matches;
  let renderer;
  try {
    renderer = new THREE.WebGLRenderer({ canvas, antialias: true, alpha: false, preserveDrawingBuffer: true, powerPreference: 'high-performance' });
  } catch (e) {
    hinweis('WebGL fehlt auf diesem Gerät. Der Text der Seite bleibt nutzbar.');
    if (opts.fertig) opts.fertig();
    return;
  }
  renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
  renderer.outputColorSpace = THREE.SRGBColorSpace;
  renderer.toneMapping = THREE.ACESFilmicToneMapping;
  renderer.toneMappingExposure = 1.0;
  renderer.shadowMap.enabled = true;
  renderer.shadowMap.type = THREE.PCFSoftShadowMap;
  const grund = new THREE.Color(); grund.setStyle(opts.hintergrund || '#0b0b0c', THREE.SRGBColorSpace); // sRGB, sonst wird Fast-Schwarz zu Grau
  const scene = new THREE.Scene();
  scene.background = grund;
  scene.fog = new THREE.Fog(grund, 6, 16);
  const pmrem = new THREE.PMREMGenerator(renderer);
  scene.environment = pmrem.fromScene(new RoomEnvironment(), 0.04).texture;
  const camera = new THREE.PerspectiveCamera(35, 1, 0.1, 60);

  // Licht mit Herkunft: warmes Hauptlicht (Fenster), kühles Kantenlicht, wenig Himmel
  const key = new THREE.DirectionalLight(0xfff1d6, 2.4);
  key.position.set(3, 5, 4);
  key.castShadow = true;
  key.shadow.mapSize.set(2048, 2048);
  key.shadow.camera.near = 0.5; key.shadow.camera.far = 20;
  key.shadow.camera.left = -4; key.shadow.camera.right = 4; key.shadow.camera.top = 4; key.shadow.camera.bottom = -4;
  key.shadow.radius = 5; key.shadow.bias = -0.0004;
  const rim = new THREE.DirectionalLight(0xbfd4ff, 0.9);
  rim.position.set(-4, 2.5, -3);
  scene.add(key, rim, new THREE.HemisphereLight(0xffffff, 0x1a1a1a, 0.25));

  const boden = new THREE.Mesh(new THREE.CircleGeometry(30, 128), new THREE.MeshStandardMaterial({ color: 0x0e0e0e, roughness: 0.6, metalness: 0, envMapIntensity: 0.35 }));
  boden.rotation.x = -Math.PI / 2;
  boden.position.y = -1;
  boden.receiveShadow = true;
  scene.add(boden);

  const root = new THREE.Group();
  scene.add(root);
  // Gold mit gemessenem Reflexionswert (F0 linear ≈ 1,00 / 0,71 / 0,29), leicht poliert
  const gold = new THREE.MeshPhysicalMaterial({ color: new THREE.Color().setRGB(1.0, 0.71, 0.29, THREE.LinearSRGBColorSpace), metalness: 1, roughness: 0.2, clearcoat: 0.4, clearcoatRoughness: 0.12, envMapIntensity: 1.0 });

  function schatten(o) { o.traverse(function (m) { if (m.isMesh) { m.castShadow = true; m.receiveShadow = true; } }); }
  function platzieren(objekt) {
    const box = new THREE.Box3().setFromObject(objekt);
    const size = box.getSize(new THREE.Vector3());
    const center = box.getCenter(new THREE.Vector3());
    objekt.position.sub(center);
    const max = Math.max(size.x, size.y, size.z) || 1;
    objekt.scale.multiplyScalar(1.9 / max);
    const box2 = new THREE.Box3().setFromObject(objekt);
    objekt.position.y += -1 - box2.min.y; // steht auf dem Boden
    schatten(objekt);
    root.add(objekt);
  }
  function ersatz() {
    while (root.children.length) root.remove(root.children[0]);
    const knoten = new THREE.Mesh(new THREE.TorusKnotGeometry(0.62, 0.2, 360, 56), gold);
    knoten.position.y = 0.05;
    const glas = new THREE.Mesh(new THREE.SphereGeometry(0.3, 96, 96), new THREE.MeshPhysicalMaterial({ color: 0xffffff, metalness: 0, roughness: 0.04, transmission: 1, thickness: 0.6, ior: 1.5 }));
    glas.position.set(1.35, -0.7, 0.55);
    const sockel = new THREE.Mesh(new THREE.CylinderGeometry(0.95, 1.0, 0.12, 128), new THREE.MeshStandardMaterial({ color: 0x1c1c1c, roughness: 0.35, metalness: 0.2 }));
    sockel.position.y = -0.94;
    [knoten, glas, sockel].forEach(function (m) { schatten(m); root.add(m); });
  }

  const b64 = (opts.modell || '').replace(/\s+/g, '');
  const loader = new GLTFLoader();
  function kaputt() { hinweis('Das Modell konnte nicht gelesen werden. Gezeigt wird eine Ersatzform.'); ersatz(); }
  if (b64) {
    try {
      const bin = Uint8Array.from(atob(b64), function (c) { return c.charCodeAt(0); });
      loader.parse(bin.buffer, '', function (g) { platzieren(g.scene); }, kaputt);
    } catch (e) { kaputt(); }
  } else if (opts.url) loader.load(opts.url, function (g) { platzieren(g.scene); }, undefined, kaputt);
  else ersatz();

  // Kamerafahrt: eine Station je Kapitel, weich übergeblendet
  const wege = [[0, 0.35, 5.4], [2.7, 1.1, 3.6], [-2.5, 0.7, 3.4], [0.2, 2.8, 3.1], [0, 0.3, 4.4]];
  // Blickziel je Kapitel: Text abwechselnd links/rechts, das Objekt steht auf der freien Seite
  const blicke = [-1.1, 1.0, -1.0, 1.0, -0.9];
  function fortschritt() {
    const max = document.documentElement.scrollHeight - window.innerHeight;
    return max > 0 ? Math.min(1, Math.max(0, window.scrollY / max)) : 0;
  }
  function station(t) {
    const n = wege.length - 1, f = t * n, i = Math.min(n - 1, Math.floor(f)), u = f - i, e = u * u * (3 - 2 * u);
    const a = wege[i], b = wege[i + 1];
    return [a[0] + (b[0] - a[0]) * e, a[1] + (b[1] - a[1]) * e, a[2] + (b[2] - a[2]) * e];
  }
  function groesse() {
    const w = canvas.clientWidth || window.innerWidth || 1280;
    const h = canvas.clientHeight || window.innerHeight || 720;
    renderer.setSize(w, h, false);
    camera.aspect = w / Math.max(1, h);
    camera.updateProjectionMatrix();
  }
  let jetzt = fortschritt(), erstes = true;
  function bild(zeit) {
    const ziel = fortschritt();
    jetzt += (ziel - jetzt) * (reduce ? 1 : 0.07);
    const p = station(jetzt);
    camera.position.set(p[0], p[1], p[2]);
    const nb = blicke.length - 1, fb = jetzt * nb, ib = Math.min(nb - 1, Math.floor(fb)), ub = fb - ib, eb = ub * ub * (3 - 2 * ub);
    camera.lookAt(blicke[ib] + (blicke[ib + 1] - blicke[ib]) * eb, -0.15, 0);
    root.rotation.y = jetzt * Math.PI * 1.4 + (reduce ? 0 : zeit * 0.00012);
    renderer.render(scene, camera);
    if (erstes) { erstes = false; document.documentElement.dataset.szene = '1'; if (opts.fertig) opts.fertig(); }
    requestAnimationFrame(bild);
  }
  groesse();
  window.addEventListener('resize', groesse);
  requestAnimationFrame(bild);
}

globalThis.TrendoWeb3 = { start, immersiv };
