"""T-0745: SVG aus Trendo mit dem SVG-Import von Blender (io_curve_svg) einlesen und die Masse der Schnittkanten messen.
Aufruf: blender -b --factory-startup --python svg_blender_T-0745.py -- <schnitt.svg> <erwartet.json>"""
import bpy, json, sys
from mathutils import Vector
svg, exp = sys.argv[-2], sys.argv[-1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_curve.svg(filepath=svg)
expect = json.load(open(exp, encoding="utf-8"))
curves = [o for o in bpy.context.scene.objects if o.type == "CURVE"]
print("BLENDER", bpy.app.version_string, "Kurvenobjekte:", len(curves))
# Groesste geschlossene Kurven = Schnittkanten (je Teil eine), in der Reihenfolge der Datei (nach x)
def dims(o):
    bb = [o.matrix_world @ Vector(c) for c in o.bound_box]
    xs = [v.x for v in bb]; ys = [v.y for v in bb]
    return (max(xs) - min(xs)) * 100, (max(ys) - min(ys)) * 100, min(xs)
cuts = []
for o in curves:
    for s in o.data.splines:
        pts = [p.co for p in s.points] or [p.co for p in s.bezier_points]
        if len(pts) < 3: continue
        xs = [o.matrix_world @ Vector((p[0], p[1], 0)) for p in pts]
        w = (max(v.x for v in xs) - min(v.x for v in xs)) * 100; h = (max(v.y for v in xs) - min(v.y for v in xs)) * 100
        cuts.append((min(v.x for v in xs), w, h, o.name))
ok = True
for part in expect["parts"]:
    best = min(cuts, key=lambda c: abs(c[1] - part["cut_w_cm"]) + abs(c[2] - part["cut_h_cm"]))
    good = abs(best[1] - part["cut_w_cm"]) < 0.05 and abs(best[2] - part["cut_h_cm"]) < 0.05
    ok &= good
    print(f"{part['name']}: Blender {best[1]:.2f} x {best[2]:.2f} cm (Trendo {part['cut_w_cm']:.2f} x {part['cut_h_cm']:.2f}) -> {'OK' if good else 'ABWEICHUNG'}")
print("ERGEBNIS", "OK" if ok else "FEHLER")
