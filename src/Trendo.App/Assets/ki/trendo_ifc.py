# Trendo – Grundriss als IFC4 (BIM) mit IfcOpenShell (LGPL-3.0, eigener Prozess) und IFC-Dateien prüfen.
# Aufruf: python trendo_ifc.py <repo (unbenutzt)> export <plan.json> <ausgabe.ifc>
#         python trendo_ifc.py <repo (unbenutzt)> info <eingabe.ifc> <ausgabe.json>
# plan.json kommt von Trendo (IfcExchange.ToJson): Maße in Metern, y nach oben.
import json
import sys
from pathlib import Path


def export(plan_file: Path, out: Path) -> int:
    import numpy as np
    import ifcopenshell
    import ifcopenshell.api as api
    import ifcopenshell.guid

    plan = json.loads(plan_file.read_text(encoding="utf-8"))
    f = api.run("project.create_file", version="IFC4")
    project = api.run("root.create_entity", f, ifc_class="IfcProject", name=plan.get("projekt") or "Trendo")
    api.run("unit.assign_unit", f)
    model = api.run("context.add_context", f, context_type="Model")
    body = api.run("context.add_context", f, context_type="Model", context_identifier="Body", target_view="MODEL_VIEW", parent=model)
    axis = api.run("context.add_context", f, context_type="Model", context_identifier="Axis", target_view="GRAPH_VIEW", parent=model)
    site = api.run("root.create_entity", f, ifc_class="IfcSite", name="Grundstück")
    building = api.run("root.create_entity", f, ifc_class="IfcBuilding", name=plan.get("projekt") or "Gebäude")
    storey = api.run("root.create_entity", f, ifc_class="IfcBuildingStorey", name=plan.get("geschoss") or "Erdgeschoss")
    storey.Elevation = float(plan.get("hoehenlage", 0.0))
    api.run("aggregate.assign_object", f, products=[site], relating_object=project)
    api.run("aggregate.assign_object", f, products=[building], relating_object=site)
    api.run("aggregate.assign_object", f, products=[storey], relating_object=building)

    walls = []
    for i, w in enumerate(plan.get("waende", [])):
        wall = api.run("root.create_entity", f, ifc_class="IfcWall", name=f"Wand {i + 1}")
        rep = api.run("geometry.create_2pt_wall", f, element=wall, context=body, p1=(w["x1"], w["y1"]), p2=(w["x2"], w["y2"]),
                      elevation=0.0, height=w["hoehe"], thickness=w["staerke"])
        api.run("geometry.assign_representation", f, product=wall, representation=rep)
        api.run("spatial.assign_container", f, products=[wall], relating_structure=storey)
        walls.append((wall, w))

    for o in plan.get("oeffnungen", []):
        if not (0 <= o["wand"] < len(walls)):
            continue
        wall, w = walls[o["wand"]]
        dx, dy = w["x2"] - w["x1"], w["y2"] - w["y1"]
        length = (dx * dx + dy * dy) ** 0.5 or 1.0
        ux, uy = dx / length, dy / length
        x, y = w["x1"] + ux * o["bei"], w["y1"] + uy * o["bei"]
        # create_2pt_wall legt die Wand links der Achse an (Normale -uy, ux); die Öffnung beginnt auf der Achse und geht durch die ganze Dicke (nachgemessen 08.11.2026)
        m = np.array([[ux, -uy, 0, x], [uy, ux, 0, y], [0, 0, 1, o["bruestung"]], [0, 0, 0, 1]], dtype=float)
        opening = api.run("root.create_entity", f, ifc_class="IfcOpeningElement", name="Öffnung")
        orep = api.run("geometry.add_wall_representation", f, context=body, length=o["breite"], height=o["hoehe"], thickness=w["staerke"])
        api.run("geometry.assign_representation", f, product=opening, representation=orep)
        api.run("geometry.edit_object_placement", f, product=opening, matrix=m)
        api.run("feature.add_feature", f, feature=opening, element=wall)
        kind = "IfcDoor" if o["art"] == "tuer" else "IfcWindow"
        fill = api.run("root.create_entity", f, ifc_class=kind, name="Tür" if kind == "IfcDoor" else "Fenster")
        fill.OverallWidth, fill.OverallHeight = float(o["breite"]), float(o["hoehe"])
        api.run("geometry.edit_object_placement", f, product=fill, matrix=m)
        api.run("spatial.assign_container", f, products=[fill], relating_structure=storey)
        api.run("feature.add_filling", f, opening=opening, element=fill)

    for r in plan.get("raeume", []):
        pts = [tuple(p) for p in r["umriss"]]
        if len(pts) < 3:
            continue
        space = api.run("root.create_entity", f, ifc_class="IfcSpace", name=r["name"])
        srep = api.run("geometry.add_slab_representation", f, context=body, depth=r["hoehe"], polyline=pts)
        api.run("geometry.assign_representation", f, product=space, representation=srep)
        api.run("aggregate.assign_object", f, products=[space], relating_object=storey)

    out.parent.mkdir(parents=True, exist_ok=True)
    f.write(str(out))
    print(f"IFC geschrieben: {len(walls)} Wände, {len(plan.get('oeffnungen', []))} Öffnungen, {len(plan.get('raeume', []))} Räume")
    return 0


def info(src: Path, out: Path) -> int:
    import ifcopenshell

    f = ifcopenshell.open(str(src))
    count = {c: len(f.by_type(c)) for c in ("IfcWall", "IfcDoor", "IfcWindow", "IfcSpace", "IfcBuildingStorey", "IfcSlab", "IfcFurnishingElement")}
    proj = f.by_type("IfcProject")
    data = {"schema": f.schema, "projekt": proj[0].Name if proj else None, "anzahl": count,
            "geschosse": [s.Name for s in f.by_type("IfcBuildingStorey")], "raeume": [s.Name for s in f.by_type("IfcSpace")]}
    out.write_text(json.dumps(data, ensure_ascii=False), encoding="utf-8")
    print(json.dumps(data, ensure_ascii=False))
    return 0


def main() -> int:
    if len(sys.argv) != 5 or sys.argv[2] not in ("export", "info"):
        print("Aufruf: trendo_ifc.py <repo> export <plan.json> <ausgabe.ifc> | info <eingabe.ifc> <ausgabe.json>", file=sys.stderr)
        return 2
    src, out = Path(sys.argv[3]), Path(sys.argv[4])
    if not src.is_file():
        print(f"Datei fehlt: {src.name}", file=sys.stderr)
        return 2
    return export(src, out) if sys.argv[2] == "export" else info(src, out)


if __name__ == "__main__":
    sys.exit(main())
