"""T-0747: erzeugt die Testdateien selbst (keine fremden Pläne).

wohnung-fremd.dxf  – Grundriss wie aus einem fremden CAD (mm, $INSUNITS 4, AC1015): Außenwände als zwei geschlossene LWPOLYLINE,
                     Innenwand als Doppellinie mit Türlücke, Türbogen auf eigener Ebene, Erker als ARC, Brüstung als alte POLYLINE,
                     Räume als LWPOLYLINE mit Namen (TEXT), Möbel und eine Bemaßung (DIMENSION) auf eigenen Ebenen.
teil-fremd.dxf     – Frästeil in Zoll ($INSUNITS 1): Umriss aus losen LINEs mit Rundung (ARC), zwei Bohrungen (CIRCLE), Langloch (LWPOLYLINE mit Ausbuchtung).
teil-fremd.svg     – Frästeil als SVG (width/height in mm, viewBox): Rechteck mit Rundung, Kreis, Pfad mit Bogen (A) und Gruppe mit transform.

Aufruf: python make_fixtures.py <zielordner>
"""
import math
import os
import sys


def dxf(entities, units, layers):
    out = ["0", "SECTION", "2", "HEADER", "9", "$ACADVER", "1", "AC1015", "9", "$INSUNITS", "70", str(units), "0", "ENDSEC",
           "0", "SECTION", "2", "TABLES", "0", "TABLE", "2", "LAYER", "70", str(len(layers))]
    for name, color in layers:
        out += ["0", "LAYER", "2", name, "70", "0", "62", str(color), "6", "CONTINUOUS"]
    out += ["0", "ENDTAB", "0", "ENDSEC", "0", "SECTION", "2", "ENTITIES"]
    for e in entities:
        out += e
    out += ["0", "ENDSEC", "0", "EOF"]
    return "\n".join(out) + "\n"


def f(v):
    return ("%.6f" % v).rstrip("0").rstrip(".")


def line(layer, x1, y1, x2, y2):
    return ["0", "LINE", "8", layer, "10", f(x1), "20", f(y1), "30", "0", "11", f(x2), "21", f(y2), "31", "0"]


def lwpoly(layer, pts, closed, bulges=None):
    e = ["0", "LWPOLYLINE", "8", layer, "90", str(len(pts)), "70", "1" if closed else "0"]
    for i, (x, y) in enumerate(pts):
        e += ["10", f(x), "20", f(y)]
        if bulges and bulges[i]:
            e += ["42", f(bulges[i])]
    return e


def polyline(layer, pts):
    e = ["0", "POLYLINE", "8", layer, "66", "1", "10", "0", "20", "0", "30", "0", "70", "0"]
    for x, y in pts:
        e += ["0", "VERTEX", "8", layer, "10", f(x), "20", f(y), "30", "0"]
    return e + ["0", "SEQEND", "8", layer]


def arc(layer, cx, cy, r, a0, a1):
    return ["0", "ARC", "8", layer, "10", f(cx), "20", f(cy), "30", "0", "40", f(r), "50", f(a0), "51", f(a1)]


def circle(layer, cx, cy, r):
    return ["0", "CIRCLE", "8", layer, "10", f(cx), "20", f(cy), "30", "0", "40", f(r)]


def text(layer, x, y, h, s):
    return ["0", "TEXT", "8", layer, "10", f(x), "20", f(y), "30", "0", "40", f(h), "1", s]


def wohnung():
    W = "A-WALL"
    e = [
        lwpoly(W, [(0, 0), (8000, 0), (8000, 6000), (0, 6000)], True),
        lwpoly(W, [(240, 240), (7760, 240), (7760, 5760), (240, 5760)], True),
        # Innenwand 11,5 cm bei x = 4000 … 4115, Tür 900 mm von y = 2000 bis 2900, Leibungen als Querstriche
        line(W, 4000, 240, 4000, 2000), line(W, 4115, 240, 4115, 2000), line(W, 4000, 2000, 4115, 2000),
        line(W, 4000, 2900, 4000, 5760), line(W, 4115, 2900, 4115, 5760), line(W, 4000, 2900, 4115, 2900),
        arc("A-DOOR", 4000, 2000, 900, 90, 180), line("A-DOOR", 4000, 2000, 3100, 2000),
        # Erker als Halbkreis vor der rechten Außenwand, Brüstung als alte POLYLINE
        arc(W, 8000, 3000, 1000, -90, 90),
        polyline(W, [(0, 0), (0, -1500), (2500, -1500)]),
        lwpoly("A-ROOM", [(240, 240), (4000, 240), (4000, 5760), (240, 5760)], True),
        lwpoly("A-ROOM", [(4115, 240), (7760, 240), (7760, 5760), (4115, 5760)], True),
        text("A-TEXT", 1800, 3000, 200, "Wohnen"), text("A-TEXT", 1800, 2600, 150, "20,77 m²"),
        text("A-TEXT", 5600, 3000, 200, "Schlafen"),
        text("A-TEXT", 500, -1000, 150, "Balkon Süd"),
        circle("A-FURN", 2000, 4000, 600), line("A-FURN", 5000, 1000, 6500, 1000),
        ["0", "DIMENSION", "8", "A-DIMS", "10", "0", "20", "-500", "30", "0", "1", "8,00"],
    ]
    return dxf(e, 4, [("A-WALL", 7), ("A-DOOR", 5), ("A-ROOM", 8), ("A-TEXT", 7), ("A-FURN", 3), ("A-DIMS", 2)])


def teil():
    # Umriss 4 × 2,5 Zoll, rechte obere Ecke mit Radius 0,5 Zoll – lose Linien in gemischter Richtung
    L = "KONTUR"
    e = [
        line(L, 0, 0, 4, 0), line(L, 4, 2.0, 4, 0),  # umgedreht gezeichnet
        arc(L, 3.5, 2.0, 0.5, 0, 90), line(L, 3.5, 2.5, 0, 2.5), line(L, 0, 2.5, 0, 0),
        circle("BOHRUNG", 1, 1.25, 0.25), circle("BOHRUNG", 3, 1.25, 0.25),
        # Langloch 1,0 × 0,3 Zoll mit Halbkreisenden (Ausbuchtung 1 = Halbkreis)
        lwpoly("LANGLOCH", [(1.5, 0.5), (2.5, 0.5), (2.5, 0.8), (1.5, 0.8)], True, [0, 1, 0, 1]),
    ]
    return dxf(e, 1, [("KONTUR", 7), ("BOHRUNG", 1), ("LANGLOCH", 3)])


def svg():
    return """<?xml version="1.0" encoding="UTF-8"?>
<svg xmlns="http://www.w3.org/2000/svg" width="120mm" height="80mm" viewBox="0 0 120 80">
  <title>T-0747 Testteil</title>
  <g id="umriss">
    <rect x="10" y="10" width="100" height="60" rx="8"/>
  </g>
  <g id="loecher" transform="translate(30 40)">
    <circle cx="0" cy="0" r="6"/>
    <path d="M 50 -10 h 10 a 10 10 0 0 1 0 20 h -10 a 10 10 0 0 1 0 -20 z"/>
  </g>
  <text x="5" y="5">nicht fräsen</text>
</svg>
"""


if __name__ == "__main__":
    out = sys.argv[1] if len(sys.argv) > 1 else os.path.dirname(os.path.abspath(__file__))
    for name, content in [("wohnung-fremd.dxf", wohnung()), ("teil-fremd.dxf", teil()), ("teil-fremd.svg", svg())]:
        # DXF bis AC1018 steht in der Windows-Codepage (hier cp1252), SVG in UTF-8
        with open(os.path.join(out, name), "w", encoding="cp1252" if name.endswith(".dxf") else "utf-8", newline="\n") as fh:
            fh.write(content)
        print(name)
