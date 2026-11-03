"""Trendo: Bild -> 3D-Modell (GLB) mit TripoSR (VAST-AI-Research/TripoSR, MIT).

Eigenes Startskript von Trendo. TripoSR braucht fuer die Isoflaeche das Paket torchmcubes, das unter Windows
einen C++-Compiler verlangt. Trendo stellt dieselbe Funktion ueber scikit-image bereit (ohne Compiler).

Aufruf: python trendo_triposr.py <repo> <bild> <ziel.glb> [--res 256] [--texture] [--no-remove-bg]
"""
import argparse
import os
import sys
import types


def install_mcubes_shim():
    import numpy as np
    import torch
    from skimage import measure

    def marching_cubes(vol, thresh):
        v = vol.detach().cpu().numpy().astype(np.float32)
        if not (v.min() < thresh < v.max()):
            raise RuntimeError("Im Bild wurde kein Gegenstand erkannt (leere Form).")
        verts, faces, _, _ = measure.marching_cubes(v, level=thresh)
        # torchmcubes liefert die Achsen in umgekehrter Reihenfolge (x = letzte Achse)
        verts = verts[:, ::-1].copy()
        return torch.from_numpy(verts).float(), torch.from_numpy(faces.astype(np.int64))

    mod = types.ModuleType("torchmcubes")
    mod.marching_cubes = marching_cubes
    sys.modules["torchmcubes"] = mod


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("repo")
    ap.add_argument("image")
    ap.add_argument("out")
    ap.add_argument("--res", type=int, default=256)
    ap.add_argument("--texture", action="store_true")
    ap.add_argument("--no-remove-bg", action="store_true")
    a = ap.parse_args()

    sys.path.insert(0, os.path.abspath(a.repo))
    install_mcubes_shim()

    import numpy as np
    import torch
    from PIL import Image
    from tsr.system import TSR
    from tsr.utils import remove_background, resize_foreground

    device = "cuda:0" if torch.cuda.is_available() else "cpu"
    print(f"TRENDO geraet={device}", flush=True)
    model = TSR.from_pretrained("stabilityai/TripoSR", config_name="config.yaml", weight_name="model.ckpt")
    model.renderer.set_chunk_size(8192)
    model.to(device)

    img = Image.open(a.image)
    if a.no_remove_bg:
        image = img.convert("RGB")
    else:
        import rembg
        fg = resize_foreground(remove_background(img, rembg.new_session()), 0.85)
        arr = np.array(fg).astype(np.float32) / 255.0
        arr = arr[:, :, :3] * arr[:, :, 3:4] + (1 - arr[:, :, 3:4]) * 0.5
        image = Image.fromarray((arr * 255.0).astype(np.uint8))

    with torch.no_grad():
        codes = model([image], device=device)
    mesh = model.extract_mesh(codes, not a.texture, resolution=a.res)[0]
    mesh.fix_normals()  # Flaechen nach aussen ausrichten
    import math
    import trimesh
    # TripoSR liefert Z nach oben, GLB erwartet Y nach oben: erst nach dem Backen um -90 Grad um X drehen
    # (das Backen fragt die Farben an den Originalpunkten ab)
    up = trimesh.transformations.rotation_matrix(-math.pi / 2, [1, 0, 0])

    os.makedirs(os.path.dirname(os.path.abspath(a.out)), exist_ok=True)
    if a.texture:
        from tsr.bake_texture import bake_texture
        # Beim Backen fragt TripoSR Punkte auf dem Prozessor ab, das Modell liegt aber auf der Grafikkarte:
        # Punkte hin, Ergebnis zurueck (sonst bricht es mit „different devices“ ab)
        orig = model.renderer.query_triplane

        def query(decoder, positions, triplane):
            out = orig(decoder, positions.to(triplane.device), triplane)
            return {k: v.cpu() for k, v in out.items()} if positions.device.type == "cpu" else out

        model.renderer.query_triplane = query
        b = bake_texture(mesh, model, codes[0], 2048)
        tex = Image.fromarray((b["colors"] * 255.0).astype(np.uint8)).transpose(Image.FLIP_TOP_BOTTOM)
        mat = trimesh.visual.material.PBRMaterial(baseColorTexture=tex, roughnessFactor=0.8, metallicFactor=0.0)
        out = trimesh.Trimesh(vertices=mesh.vertices[b["vmapping"]], faces=b["indices"], process=False,
                              visual=trimesh.visual.TextureVisuals(uv=b["uvs"], material=mat))
        out.apply_transform(up)
        out.export(a.out)
    else:
        mesh.apply_transform(up)
        mesh.export(a.out)
    print(f"TRENDO fertig {len(mesh.vertices)} punkte {len(mesh.faces)} flaechen", flush=True)


if __name__ == "__main__":
    main()
