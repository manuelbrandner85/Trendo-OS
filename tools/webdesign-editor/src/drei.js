// Z3: 3D-Modelle (GLB) im Browser – WebGPU, wenn der Browser es kann, sonst WebGL 2 (der WebGPU-Renderer von three.js wechselt selbst).
// Dieses Bündel lädt die Seite nur, wenn sie einen 3D-Baustein mit Modell hat. Die Modelldatei kommt eingebettet (window.TRENDO_DATEIEN), nicht aus dem Netz.
import * as THREE from "three";
import { GLTFLoader } from "three/addons/loaders/GLTFLoader.js";
import { MeshoptDecoder } from "three/addons/libs/meshopt_decoder.module.js";
import { starte } from "./szene.js";

function parse(bytes) {
  return new Promise((res, rej) => {
    const loader = new GLTFLoader(); loader.setMeshoptDecoder(MeshoptDecoder);
    loader.parse(bytes, "", (gltf) => res(gltf.scene), (e) => rej(e));
  });
}

window.Trendo3D = {
  async mount(el, props, datei) {
    const bytes = await datei(props.datei);
    if (!bytes) throw new Error("Modell fehlt");
    const renderer = new THREE.WebGPURenderer({ antialias: true, alpha: true, forceWebGL: props.renderer === "webgl" });
    await renderer.init();
    const backend = () => (renderer.backend && renderer.backend.isWebGPUBackend ? "webgpu" : "webgl");
    return starte({ renderer, el, props, datei, backend, inhalt: async (scene) => {
      const objekt = await parse(bytes);
      scene.add(objekt);
      objekt.updateMatrixWorld(true);
      return { objekt, box: new THREE.Box3().setFromObject(objekt) };
    } });
  },
};
window.dispatchEvent(new Event("trendo-3d"));
