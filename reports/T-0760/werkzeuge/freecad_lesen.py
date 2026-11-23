# T-0760 Fremdbeleg: FreeCAD liest die DXF mit seinem eigenen Importer und schreibt alle Kanten fein abgetastet (JSON).
# Aufruf: freecadcmd.exe freecad_lesen.py  (Pfade über Umgebungsvariablen T0760_IN / T0760_OUT)
import os, json, sys
import FreeCAD
src = os.environ["T0760_IN"]; dst = os.environ["T0760_OUT"]
doc = FreeCAD.newDocument("t0760")
log = {"freecad": FreeCAD.Version()[:3], "importer": None}
try:
    import Import
    Import.readDXF(src, doc.Name)
    log["importer"] = "Import.readDXF (C++)"
except Exception as e:
    log["fehler_cpp"] = repr(e)
    import importDXF
    importDXF.insert(src, doc.Name)
    log["importer"] = "importDXF (Python)"
doc.recompute()
edges = []
import Part
for o in doc.Objects:
    try:
        sh = Part.getShape(o, "", needSubElement=False, transform=True)  # mit Lage (Placement) des Verweises
    except Exception:
        sh = getattr(o, "Shape", None)
    if sh is None or sh.isNull():
        continue
    for e in sh.Edges:
        n = max(8, int(e.Length / 0.05))
        pts = [(p.x, p.y) for p in e.discretize(Number=min(n, 20000))]
        edges.append({"obj": o.Name, "typ": type(e.Curve).__name__, "pts": pts, "sichtbar": bool(getattr(o, "Visibility", True))})
log["objekte"] = len(doc.Objects); log["kanten"] = len(edges)
json.dump({"log": log, "edges": edges}, open(dst, "w"))
print(json.dumps(log))
