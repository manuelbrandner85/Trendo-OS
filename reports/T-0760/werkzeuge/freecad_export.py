# T-0760 Fremdbeleg Rundreise: FreeCAD erzeugt eine Freiformkurve (B-Spline durch Punkte) und Kreise und schreibt sie mit seinem eigenen
# DXF-Export. Die Kurve wird zusätzlich fein abgetastet als JSON gespeichert (Wahrheit aus FreeCAD).
import os, json
import FreeCAD, Part
from FreeCAD import Vector as V
dst = os.environ["T0760_OUT"]; truth = os.environ["T0760_TRUTH"]
doc = FreeCAD.newDocument("rund")
pts = [V(0, 0, 0), V(40, 60, 0), V(90, 20, 0), V(140, 80, 0), V(200, 10, 0)]
bs = Part.BSplineCurve(); bs.interpolate(pts)
closed = Part.BSplineCurve(); closed.interpolate([V(300, 0, 0), V(360, 40, 0), V(330, 90, 0), V(270, 60, 0)], PeriodicFlag=True)
objs = []
for name, sh in (("Kurve", bs.toShape()), ("Ring", closed.toShape()), ("Kreis", Part.makeCircle(15, V(100, -50, 0)))):
    o = doc.addObject("Part::Feature", name); o.Shape = sh; objs.append(o)
doc.recompute()
log = {"freecad": FreeCAD.Version()[:3]}
try:
    import Import
    Import.writeDXFObject(objs, dst)
    log["export"] = "Import.writeDXFObject (C++)"
except Exception as e:
    log["fehler_cpp"] = repr(e)
    import importDXF
    importDXF.export(objs, dst)
    log["export"] = "importDXF.export (Python)"
edges = []
for o in objs:
    for e in o.Shape.Edges:
        edges.append({"obj": o.Name, "typ": type(e.Curve).__name__, "pts": [(p.x, p.y) for p in e.discretize(Number=20000)]})
json.dump({"log": log, "edges": edges}, open(truth, "w"))
print(json.dumps(log))
