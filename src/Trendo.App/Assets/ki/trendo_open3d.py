# Trendo – Scan-Netze glätten und vereinfachen mit Open3D (MIT), eigener Prozess.
# Aufruf: python trendo_open3d.py <repo (unbenutzt)> glaetten <ein.obj> <aus.obj> <durchgaenge 1-50>
#         python trendo_open3d.py <repo (unbenutzt)> vereinfachen <ein.obj> <aus.obj> <ziel-dreiecke 100-5000000>
# Ausgabe (stdout, eine Zeile JSON): Dreiecke vorher/nachher, Abmessungen vorher/nachher.
import json
import sys
from pathlib import Path


def main() -> int:
    if len(sys.argv) != 6 or sys.argv[2] not in ("glaetten", "vereinfachen"):
        print("Aufruf: trendo_open3d.py <repo> glaetten|vereinfachen <ein> <aus> <zahl>", file=sys.stderr)
        return 2
    mode, src, out = sys.argv[2], Path(sys.argv[3]), Path(sys.argv[4])
    try:
        n = int(sys.argv[5])
    except ValueError:
        print("Zahl erwartet.", file=sys.stderr)
        return 2
    if mode == "glaetten" and not 1 <= n <= 50 or mode == "vereinfachen" and not 100 <= n <= 5_000_000:
        print("Zahl außerhalb des erlaubten Bereichs.", file=sys.stderr)
        return 2
    if not src.is_file():
        print(f"Datei fehlt: {src.name}", file=sys.stderr)
        return 2
    import open3d as o3d

    mesh = o3d.io.read_triangle_mesh(str(src))
    before = len(mesh.triangles)
    if before == 0:
        print("Das Netz hat keine Dreiecke.", file=sys.stderr)
        return 3
    ext0 = mesh.get_axis_aligned_bounding_box().get_extent().tolist()
    mesh.remove_duplicated_vertices()
    mesh.remove_degenerate_triangles()
    if mode == "glaetten":
        # Taubin: glättet Rauschen, ohne dass das Objekt schrumpft (anders als einfaches Laplace-Glätten)
        mesh = mesh.filter_smooth_taubin(number_of_iterations=n)
    elif n < len(mesh.triangles):
        mesh = mesh.simplify_quadric_decimation(target_number_of_triangles=n)
    mesh.remove_unreferenced_vertices()
    mesh.compute_vertex_normals()
    out.parent.mkdir(parents=True, exist_ok=True)
    if not o3d.io.write_triangle_mesh(str(out), mesh, write_triangle_uvs=False):
        print("Schreiben fehlgeschlagen.", file=sys.stderr)
        return 4
    ext1 = mesh.get_axis_aligned_bounding_box().get_extent().tolist()
    print(json.dumps({"vorher": before, "nachher": len(mesh.triangles), "groesse_vorher": [round(v, 4) for v in ext0], "groesse_nachher": [round(v, 4) for v in ext1]}))
    return 0


if __name__ == "__main__":
    sys.exit(main())
