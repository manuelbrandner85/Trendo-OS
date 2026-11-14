// Z3: Gaussian Splats über Spark (sparkjsdev/spark, MIT) mit WebGL 2 – Spark rendert mit dem WebGL-Renderer von three.js.
// Dieses Bündel lädt die Seite nur, wenn sie einen 3D-Baustein mit Splat hat. Die Datei kommt eingebettet, nicht aus dem Netz.
import * as THREE from "three";
import { SplatMesh, SparkRenderer } from "@sparkjsdev/spark";
import { starte } from "./szene.js";

window.TrendoSplat = {
  async mount(el, props, datei) {
    const bytes = await datei(props.datei);
    if (!bytes) throw new Error("Splat fehlt");
    const renderer = new THREE.WebGLRenderer({ antialias: false, alpha: true });
    return starte({ renderer, el, props, datei, backend: () => "webgl", inhalt: async (scene) => {
      scene.add(new SparkRenderer({ renderer }));
      const splat = new SplatMesh({ fileBytes: bytes, fileName: props.datei });
      await splat.initialized;
      splat.quaternion.set(1, 0, 0, 0); // übliche Aufnahmen stehen kopf (y nach unten)
      scene.add(splat);
      splat.updateMatrixWorld(true);
      const box = splat.getBoundingBox(true).clone().applyMatrix4(splat.matrixWorld);
      return { objekt: splat, box };
    } });
  },
};
window.dispatchEvent(new Event("trendo-splat"));
