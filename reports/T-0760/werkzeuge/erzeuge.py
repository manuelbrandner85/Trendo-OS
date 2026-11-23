# T-0760 Fremdbeleg: Test-DXF (ASCII, AC1015) mit Blockverweisen (gedreht, skaliert, gespiegelt, verschachtelt) und SPLINE (NURBS-Kreis, kubisch)
import math, sys
out = sys.argv[1]; base = float(sys.argv[2]) if len(sys.argv) > 2 else 10  # Basispunkt des Blocks PLATTE
L = []
def g(*pairs):
    for c, v in pairs: L.append(f"{c}\n{v}\n")
W = math.sqrt(0.5)
def spline(deg, ctrl, knots, weights=None, flags=8, layer="0"):
    g((0, "SPLINE"), (8, layer), (100, "AcDbEntity"), (100, "AcDbSpline"), (210, 0), (220, 0), (230, 1), (70, flags | (4 if weights else 0)), (71, deg), (72, len(knots)), (73, len(ctrl)), (74, 0), (42, 1e-10), (43, 1e-10))
    for k in knots: g((40, k))
    for w in (weights or []): g((41, w))
    for x, y in ctrl: g((10, x), (20, y), (30, 0))
def lw(pts, closed=True, layer="0"):
    g((0, "LWPOLYLINE"), (8, layer), (100, "AcDbEntity"), (100, "AcDbPolyline"), (90, len(pts)), (70, 1 if closed else 0))
    for x, y in pts: g((10, x), (20, y))
def circle(x, y, r, layer="0"): g((0, "CIRCLE"), (8, layer), (100, "AcDbEntity"), (100, "AcDbCircle"), (10, x), (20, y), (30, 0), (40, r))
def arc(x, y, r, a0, a1): g((0, "ARC"), (8, "0"), (100, "AcDbEntity"), (100, "AcDbCircle"), (10, x), (20, y), (30, 0), (40, r), (100, "AcDbArc"), (50, a0), (51, a1))
def insert(name, x, y, sx=1, sy=1, rot=0, layer="0"):
    g((0, "INSERT"), (8, layer), (100, "AcDbEntity"), (100, "AcDbBlockReference"), (2, name), (10, x), (20, y), (30, 0), (41, sx), (42, sy), (43, 1), (50, rot))
g((0, "SECTION"), (2, "HEADER"), (9, "$ACADVER"), (1, "AC1015"), (9, "$INSUNITS"), (70, 4), (0, "ENDSEC"))
g((0, "SECTION"), (2, "BLOCKS"))
# Block PLATTE: Platte 100 × 60 mit Ecke-Bogen, Loch r 8, Basispunkt (10/10)
g((0, "BLOCK"), (8, "0"), (100, "AcDbEntity"), (100, "AcDbBlockBegin"), (2, "PLATTE"), (70, 0), (10, base), (20, base), (30, 0), (3, "PLATTE"))
lw([(10, 10), (110, 10), (110, 70), (10, 70)])
circle(40, 40, 8)
arc(80, 40, 10, 0, 180)
g((0, "ENDBLK"), (8, "0"), (100, "AcDbEntity"), (100, "AcDbBlockEnd"))
# Block DOPPEL: verschachtelt – zwei PLATTE-Verweise
g((0, "BLOCK"), (8, "0"), (100, "AcDbEntity"), (100, "AcDbBlockBegin"), (2, "DOPPEL"), (70, 0), (10, 0), (20, 0), (30, 0), (3, "DOPPEL"))
insert("PLATTE", 0, 0)
insert("PLATTE", 0, 100, rot=15)
g((0, "ENDBLK"), (8, "0"), (100, "AcDbEntity"), (100, "AcDbBlockEnd"))
g((0, "ENDSEC"))
g((0, "SECTION"), (2, "ENTITIES"))
insert("PLATTE", 300, 0, rot=30)
insert("PLATTE", 500, 0, sx=1.5, sy=1.5)
insert("PLATTE", 800, 0, sx=-1, sy=1)
insert("DOPPEL", 0, 300, sx=0.5, sy=0.5, rot=-20)
spline(2, [(1050, 50), (1050, 90), (1010, 90), (970, 90), (970, 50), (970, 10), (1010, 10), (1050, 10), (1050, 50)],
       [0, 0, 0, .25, .25, .5, .5, .75, .75, 1, 1, 1], [1, W, 1, W, 1, W, 1, W, 1], flags=1 | 8)
spline(3, [(1100, 0), (1130, 80), (1170, -20), (1200, 0), (1230, 60), (1260, 10)], [0, 0, 0, 0, 1, 2, 3, 3, 3, 3])
g((0, "ENDSEC"), (0, "EOF"))
open(out, "w", newline="\n").write("".join(L))
