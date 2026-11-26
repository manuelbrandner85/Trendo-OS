# T-0776: FreeCAD (freecadcmd) liest eine von Trendo geschriebene STEP- oder DXF-Datei und schreibt, was es darin findet, als JSON.
# Aufruf: freecadcmd freecad_pruefe.py  –  Eingaben über Umgebungsvariablen (freecadcmd deutet weitere Argumente als Dateien):
#   T0776_ART = step | dxf,  T0776_IN = Eingabedatei,  T0776_OUT = JSON-Ausgabe
# Nur Lesen: es wird nichts gespeichert außer der JSON-Datei.
import json
import os
import sys

import FreeCAD as App

art = os.environ["T0776_ART"]
src = os.environ["T0776_IN"]
out = os.environ["T0776_OUT"]
res = {"freecad": list(App.Version()[:3]), "art": art}


def bbox(b):
    return [round(b.XMin, 4), round(b.YMin, 4), round(b.ZMin, 4), round(b.XMax, 4), round(b.YMax, 4), round(b.ZMax, 4)]


try:
    if art == "step":
        import Part
        import Import

        s = Part.Shape()
        s.read(src)
        res["gueltig"] = s.isValid()
        res["volumen"] = round(s.Volume, 4)
        res["flaeche"] = round(s.Area, 4)
        res["koerper"] = len(s.Solids)
        res["schalen_geschlossen"] = all(sh.isClosed() for sh in s.Shells)
        res["flaechen"] = len(s.Faces)
        res["umriss"] = bbox(s.BoundBox)
        res["koerper_volumen"] = [round(x.Volume, 4) for x in s.Solids]
        doc = App.newDocument("t0776")
        Import.insert(src, doc.Name)
        res["namen"] = sorted(o.Label for o in doc.Objects if hasattr(o, "Shape") and not o.Shape.isNull() and o.Shape.Solids)
    elif art == "dxf":
        import Import

        # Texte mit einlesen (Einstellung „Texte und Bemaßungen importieren“ – liegt im eigenen Profil aus FREECAD_USER_HOME)
        App.ParamGet("User parameter:BaseApp/Preferences/Draft").SetBool("dxftext", True)
        doc = App.newDocument("t0776")
        Import.readDXF(src)
        doc.recompute()
        objs = []
        edges = 0
        for o in doc.Objects:
            e = {"name": o.Label, "typ": o.TypeId}
            if hasattr(o, "Shape") and not o.Shape.isNull():
                e["kanten"] = len(o.Shape.Edges)
                edges += len(o.Shape.Edges)
                if o.Shape.Edges:
                    e["umriss"] = bbox(o.Shape.BoundBox)
            for prop in ("Text", "LabelText", "String"):
                if hasattr(o, prop):
                    v = getattr(o, prop)
                    e["text"] = list(v) if isinstance(v, (list, tuple)) else str(v)
            if o.TypeId == "App::DocumentObjectGroup" or hasattr(o, "Group"):
                e["enthaelt"] = len(getattr(o, "Group", []))
            objs.append(e)
        res["importer"] = "Import.readDXF (C++)"
        res["objekte"] = len(doc.Objects)
        res["kanten"] = edges
        withshape = [o for o in doc.Objects if hasattr(o, "Shape") and not o.Shape.isNull() and o.Shape.Edges]
        if withshape:
            b = withshape[0].Shape.BoundBox
            for o in withshape[1:]:
                b.add(o.Shape.BoundBox)
            res["umriss"] = bbox(b)
        res["liste"] = objs
    else:
        res["fehler"] = "unbekannte Art " + art
except Exception as ex:  # Fehler ehrlich melden statt still
    res["fehler"] = "%s: %s" % (type(ex).__name__, ex)

with open(out, "w", encoding="utf-8") as f:
    json.dump(res, f, ensure_ascii=False, indent=1)
print(json.dumps({k: v for k, v in res.items() if k != "liste"}, ensure_ascii=False))
sys.stdout.flush()
