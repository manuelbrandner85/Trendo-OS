/* Quelle für das lokale Bündel trendo-three.js. Wird nicht selbst in die Webseite kopiert. */
import * as THREE from 'three';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';

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

globalThis.TrendoWeb3 = { start };
