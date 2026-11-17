"""T-0745: DXF (R12) aus Trendo mit eigenem, unabhaengigem Leser (nur Python-Standardbibliothek) einlesen.
Gibt je Polylinie Ebene, Punktzahl, Umfang und Flaeche in cm aus und vergleicht die Nahtlinien mit erwartet.json."""
import json, math, sys

sys.stdout.reconfigure(encoding="utf-8")

def read(path):
    lines = open(path, encoding="utf-8").read().split("\n")
    pairs = [(lines[i].strip(), lines[i + 1].strip()) for i in range(0, len(lines) - 1, 2)]
    polys, cur, layer, pt = [], None, None, {}
    for code, val in pairs:
        if code == "0":
            if pt and cur is not None: cur["pts"].append((pt["10"], pt["20"])); pt = {}
            if val == "POLYLINE": cur = {"layer": None, "closed": False, "pts": []}; polys.append(cur); mode = "poly"
            elif val == "VERTEX": pt = {}; mode = "vertex"
            elif val == "SEQEND": cur = None; mode = None
            continue
        if cur is None: continue
        if code == "8" and cur["layer"] is None: cur["layer"] = val
        elif code == "70" and not cur["pts"] and not pt: cur["closed"] = int(val) & 1 == 1
        elif code in ("10", "20"): pt[code] = float(val) / 10.0  # mm -> cm
    return polys

def measure(pts, closed):
    n = len(pts); per = sum(math.dist(pts[i], pts[(i + 1) % n]) for i in range(n if closed else n - 1))
    area = abs(sum(pts[i][0] * pts[(i + 1) % n][1] - pts[(i + 1) % n][0] * pts[i][1] for i in range(n))) / 2 if closed else 0
    ys = [p[1] for p in pts]
    return per, area, max(ys) - min(ys)

polys = read(sys.argv[1]); expect = json.load(open(sys.argv[2], encoding="utf-8"))
print(f"Polylinien: {len(polys)}  Ebenen: {sorted(set(p['layer'] for p in polys))}")
seams = [p for p in polys if p["layer"] == "NAHT"]; cuts = [p for p in polys if p["layer"] == "SCHNITT"]
ok = len(seams) == len(expect["parts"]) == len(cuts)
for i, part in enumerate(expect["parts"]):
    per, area, _ = measure(seams[i]["pts"], seams[i]["closed"]); _, _, h = measure(cuts[i]["pts"], True)
    good = abs(per - part["perimeter_cm"]) < 0.05 and abs(area - part["area_cm2"]) < 0.5 and abs(h - part["cut_h_cm"]) < 0.05
    ok &= good
    print(f"{part['name']}: Naht-Umfang {per:.2f} cm (Trendo {part['perimeter_cm']:.2f}) | Flaeche {area:.1f} cm2 (Trendo {part['area_cm2']:.1f}) | Schnitthoehe {h:.2f} cm (Trendo {part['cut_h_cm']:.2f}) -> {'OK' if good else 'ABWEICHUNG'}")
t = expect["template_front"]; per0, area0, _ = measure(seams[0]["pts"], True)
print(f"Vorderteil gegenueber Vorlage: Flaeche {area0 - t['area_cm2']:+.1f} cm2, Umfang {per0 - t['perimeter_cm']:+.2f} cm, Schnitthoehe {measure(cuts[0]['pts'], True)[2] - t['cut_h_cm']:+.2f} cm")
print("ERGEBNIS", "OK" if ok else "FEHLER")
sys.exit(0 if ok else 1)
