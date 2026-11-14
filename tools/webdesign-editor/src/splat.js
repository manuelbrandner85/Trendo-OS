// Z3: Gaussian Splats über Spark (sparkjsdev/spark, MIT) mit WebGL 2 – Spark rendert mit dem WebGL-Renderer von three.js.
// Dieses Bündel lädt die Seite nur, wenn sie einen 3D-Baustein mit Splat hat. Die Datei kommt eingebettet, nicht aus dem Netz.
import * as THREE from "three";
import { SplatMesh, SparkRenderer } from "@sparkjsdev/spark";
import { starte } from "./szene.js";

// Echte Aufnahmen haben Punkte bis weit in den Hintergrund (Himmel, Ausreißer). Der Bildausschnitt richtet sich darum nach dem dichten Kern:
// je Achse 30.–70. Perzentil der gut sichtbaren Punkte (Stichprobe), in Weltkoordinaten. Kleine Objekte ohne Hintergrund bleiben praktisch gleich.
function kernBox(splat) {
  const n = splat.packedSplats ? splat.packedSplats.numSplats : 0;
  const schritt = Math.max(1, Math.floor(n / 60000));
  const xs = [], ys = [], zs = [], v = new THREE.Vector3();
  splat.forEachSplat((i, center, scales, quat, opacity) => {
    if (i % schritt !== 0 || opacity < 0.25) return;
    v.copy(center).applyMatrix4(splat.matrixWorld); xs.push(v.x); ys.push(v.y); zs.push(v.z);
  });
  if (xs.length < 50) return splat.getBoundingBox(true).clone().applyMatrix4(splat.matrixWorld);
  const p = (a, q) => { a.sort((x, y) => x - y); return a[Math.min(a.length - 1, Math.floor(q * a.length))]; };
  return new THREE.Box3(new THREE.Vector3(p(xs, 0.3), p(ys, 0.3), p(zs, 0.3)), new THREE.Vector3(p(xs, 0.7), p(ys, 0.7), p(zs, 0.7)));
}

window.TrendoSplat = {
  async mount(el, props, datei) {
    const bytes = await datei(props.datei);
    if (!bytes) throw new Error("Splat fehlt");
    const renderer = new THREE.WebGLRenderer({ antialias: false, alpha: true });
    // Licht und Schatten sind in einer Aufnahme schon enthalten: kein zusätzlicher Schattenboden
    return starte({ renderer, el, props, datei, gebacken: true, backend: () => "webgl", inhalt: async (scene) => {
      scene.add(new SparkRenderer({ renderer }));
      const splat = new SplatMesh({ fileBytes: bytes, fileName: props.datei });
      await splat.initialized;
      splat.quaternion.set(1, 0, 0, 0); // übliche Aufnahmen stehen kopf (y nach unten)
      scene.add(splat);
      splat.updateMatrixWorld(true);
      return { objekt: splat, box: kernBox(splat) };
    } });
  },
};
window.dispatchEvent(new Event("trendo-splat"));
