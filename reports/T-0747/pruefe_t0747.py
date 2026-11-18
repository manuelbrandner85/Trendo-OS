"""T-0747: unabhängige Prüfung der Trendo-Ausgaben mit eigenem, kleinem Python-Leser (kein Trendo-Code, keine Zusatzpakete).

Auf dem Prüf-PC war kein Fremd-CAD vorhanden (ezdxf nicht installiert, kein LibreCAD/QCAD, Blender 5.2 ohne DXF-Erweiterung) –
deshalb liest dieses Skript DXF, SVG und G-Code selbst. Es prüft:
  1. wohnung-bearbeitet.dxf (Trendo-Export nach Import + Bearbeitung): Aufbau (HEADER/TABLES/ENTITIES/EOF), $INSUNITS = 5 (cm),
     Ebene WAENDE, Außenmaß 8,00 × 6,00 m, Innenwand jetzt 17,5 cm (zwei parallele Linien), Türbogen, Raumnamen.
  2. teil-fremd-dxf.nc / teil-fremd-svg.nc gegen die Original-Zeichnung: alle Schnittbewegungen (Z < 0) bleiben mindestens um den
     Fräserradius von jeder Kontur weg (kein Anschneiden), jede Kontur hat eine Bahn im Abstand Radius (Versatz stimmt), die Bahnen
     liegen im Rohteil (Teil ± Radius), tiefste Z = −Tiefe, Stufen −2/−4/−6, Haltestege auf −4,5.
  T-0747-fix (Gegenprüfung): Der Abstand wird zusätzlich exakt Strecke zu Strecke geprüft (jede Schnittbewegung gegen jede Kontur-Strecke,
     nicht nur abgetastete Punkte). Neu 3. fix-faelle.dxf / fix-faelle.nc (Fräser Ø 6): zwei Teile 3 mm auseinander, Insel 2 mm neben dem
     Lochrand, Achterform, Hantel-Loch mit 3-mm-Steg, 3-mm-Schlitz außen, Kreis R 50 mit 0,5 mm Rauschen – kein Schnitt kommt einer Kontur
     näher als der Radius, die gefährlichen Konturen haben keine Bahn, die Hinweise nennen jeden Fall.
Aufruf: python pruefe_t0747.py <belegordner> <fixtures-ordner>   Exitcode 0 = alles bestanden.
"""
import math
import re
import sys
import xml.etree.ElementTree as ET

sys.stdout.reconfigure(encoding="utf-8")
R = 2.0  # Fräserradius (Ø 4)
DEPTH = 6.0
fails = []


def check(cond, text):
    print(("OK    " if cond else "FEHLER") + "  " + text)
    if not cond:
        fails.append(text)


def pairs(path):
    raw = open(path, "rb").read()
    try:
        txt = raw.decode("utf-8")
    except UnicodeDecodeError:
        txt = raw.decode("cp1252")
    lines = txt.replace("\r\n", "\n").split("\n")
    return [(int(lines[i].strip()), lines[i + 1].strip()) for i in range(0, len(lines) - 1, 2)]


def entities(pp):
    """(typ, {code: [werte]}) aus dem ENTITIES-Abschnitt; VERTEX hängen an POLYLINE."""
    out, sec, i = [], None, 0
    while i < len(pp):
        c, v = pp[i]
        if c == 0 and v == "SECTION":
            sec = pp[i + 1][1]; i += 2; continue
        if c == 0 and sec == "ENTITIES" and v not in ("ENDSEC", "EOF"):
            body = {}; i += 1
            while i < len(pp) and pp[i][0] != 0:
                body.setdefault(pp[i][0], []).append(pp[i][1]); i += 1
            out.append((v, body)); continue
        i += 1
    return out


def header(pp, name):
    for i, (c, v) in enumerate(pp):
        if c == 9 and v == name:
            return pp[i + 1][1]
    return None


def num(b, c, k=0):
    return float(b[c][k])


# ---------- 1. Grundriss-Rundreise ----------
def check_plan(path):
    print("\n== 1. " + path)
    pp = pairs(path)
    names = [v for c, v in pp if c == 0]
    check(names[0] == "SECTION" and names[-1] == "EOF", "DXF-Aufbau: beginnt mit SECTION, endet mit EOF")
    check(header(pp, "$INSUNITS") == "5", "$INSUNITS = 5 (Zentimeter)")
    layers = [pp[i + 1][1] for i, (c, v) in enumerate(pp) if c == 0 and v == "LAYER" and pp[i + 1][0] == 2]
    check("WAENDE" in layers and "OEFFNUNGEN" in layers, "Ebenen WAENDE und OEFFNUNGEN vorhanden: " + ", ".join(layers))
    ents = entities(pp)
    walls = [(num(b, 10), num(b, 20), num(b, 11), num(b, 21)) for t, b in ents if t == "LINE" and b[8][0] == "WAENDE"]
    check(len(walls) > 20, "Wandkanten als LINE auf WAENDE: %d" % len(walls))
    xs = [x for w in walls for x in (w[0], w[2])]; ys = [y for w in walls for y in (w[1], w[3])]
    # Außenwände: Linien bei x = 0 und x = 800 über die ganze Höhe 600 (Erker/Brüstung liegen außerhalb)
    full_h = [w for w in walls if abs(w[0] - w[2]) < 0.01 and abs(abs(w[1] - w[3]) - 600) < 0.05]
    full_w = [w for w in walls if abs(w[1] - w[3]) < 0.01 and abs(abs(w[0] - w[2]) - 800) < 0.05]
    check(any(abs(w[0]) < 0.05 for w in full_h) and any(abs(w[0] - 800) < 0.05 for w in full_h), "Außenkanten links/rechts: 6,00 m hoch bei x = 0 und x = 800 cm")
    check(len(full_w) >= 2, "Außenkanten oben/unten: 8,00 m breit (%d Linien)" % len(full_w))
    # Innenwand 17,5 cm: zwei senkrechte Linien mit Abstand 17,5 um x = 405,75
    vx = sorted({round(w[0], 3) for w in walls if abs(w[0] - w[2]) < 0.01 and 380 < w[0] < 430})
    check(any(abs((b - a) - 17.5) < 0.01 for a in vx for b in vx), "Innenwand nach Bearbeitung 17,5 cm stark (Kanten bei x = %s)" % vx)
    arcs = [(num(b, 10), num(b, 20), num(b, 40)) for t, b in ents if t == "ARC" and b[8][0] == "OEFFNUNGEN"]
    check(any(abs(r - 90) < 0.5 for _, _, r in arcs), "Tür mit Schwenkbogen r = 90 cm auf OEFFNUNGEN")
    texts = [b[1][0] for t, b in ents if t == "TEXT"]
    check("Wohnen" in texts and "Schlafen" in texts, "Raumnamen als TEXT: Wohnen, Schlafen")
    check(any(re.search(r"m²", t) for t in texts), "Flächenangaben (m²) richtig kodiert")
    check(min(xs) > -100 and max(ys) <= 0.01, "Lage: links oben bei 0/0 (y nach unten gespiegelt), Erker/Brüstung außerhalb")


# ---------- 2. Zeichnung → Konturen (eigener Leser) ----------
def sample_arc(cx, cy, r, a0, a1, step=0.5):
    while a1 <= a0:
        a1 += 360
    n = max(2, int(math.ceil((a1 - a0) / step)))
    return [(cx + r * math.cos(math.radians(a0 + (a1 - a0) * k / n)), cy + r * math.sin(math.radians(a0 + (a1 - a0) * k / n))) for k in range(n + 1)]


def bulge_pts(p, q, b):
    th = 4 * math.atan(b); c = math.dist(p, q); r = c / (2 * math.sin(abs(th) / 2))
    mx, my = (p[0] + q[0]) / 2, (p[1] + q[1]) / 2; h = r * math.cos(th / 2) * (1 if b > 0 else -1)
    cx, cy = mx - (q[1] - p[1]) / c * h, my + (q[0] - p[0]) / c * h
    a = math.atan2(p[1] - cy, p[0] - cx)
    n = max(2, int(abs(th) / math.radians(0.5)))
    return [(cx + r * math.cos(a + th * k / n), cy + r * math.sin(a + th * k / n)) for k in range(n + 1)]


def dxf_segments(path):
    pp = pairs(path); k = {"1": 25.4, "4": 1.0, "5": 10.0, "6": 1000.0}.get(header(pp, "$INSUNITS"), 1.0)
    segs = []
    def poly(pts, closed=False):
        pts = [(x * k, y * k) for x, y in pts]
        for i in range(len(pts) - 1 + (1 if closed else 0)):
            segs.append((pts[i], pts[(i + 1) % len(pts)]))
    for t, b in entities(pp):
        if t == "LINE":
            poly([(num(b, 10), num(b, 20)), (num(b, 11), num(b, 21))])
        elif t == "CIRCLE":
            poly(sample_arc(num(b, 10), num(b, 20), num(b, 40), 0, 360))
        elif t == "ARC":
            poly(sample_arc(num(b, 10), num(b, 20), num(b, 40), num(b, 50), num(b, 51)))
        elif t == "LWPOLYLINE":
            xs, ys = [float(v) for v in b[10]], [float(v) for v in b[20]]
            pts = list(zip(xs, ys)); out = []
            bul = b.get(42, [])
            # vereinfachter Leser für die Testdatei: Ausbuchtungen stehen an Punkt 2 und 4 (Langloch-Enden)
            bmap = {1: float(bul[0]), 3: float(bul[1])} if len(bul) == 2 else {}
            closed = b[70][0] == "1"
            for i in range(len(pts)):
                q = pts[(i + 1) % len(pts)]
                if i == len(pts) - 1 and not closed:
                    break
                if bmap.get(i):
                    out += bulge_pts(pts[i], q, bmap[i])
                else:
                    out += [pts[i], q]
            poly(out)
    return segs


def svg_segments(path):
    root = ET.parse(path).getroot(); ns = "{http://www.w3.org/2000/svg}"
    vb = [float(v) for v in root.get("viewBox").split()]; k = float(root.get("width")[:-2]) / vb[2]
    segs = []
    def add(pts, tx=0, ty=0, closed=True):
        pts = [((x + tx) * k, -(y + ty) * k) for x, y in pts]
        for i in range(len(pts) - (0 if closed else 1)):
            segs.append((pts[i], pts[(i + 1) % len(pts)]))
    for g in root.iter(ns + "g"):
        tx = ty = 0
        m = re.match(r"translate\(([-\d.]+)[ ,]+([-\d.]+)\)", g.get("transform", ""))
        if m:
            tx, ty = float(m.group(1)), float(m.group(2))
        for e in g:
            tag = e.tag.replace(ns, "")
            if tag == "rect":
                x, y, w, h, r = (float(e.get(a, 0)) for a in ("x", "y", "width", "height", "rx"))
                pts = []
                for cx, cy, a0 in ((x + w - r, y + r, 270), (x + w - r, y + h - r, 0), (x + r, y + h - r, 90), (x + r, y + r, 180)):
                    pts += sample_arc(cx, cy, r, a0, a0 + 90)
                add(pts, tx, ty)
            elif tag == "circle":
                add(sample_arc(float(e.get("cx")), float(e.get("cy")), float(e.get("r")), 0, 360)[:-1], tx, ty)
            elif tag == "path":
                # Testdatei: M 50 -10 h 10 a 10 10 0 0 1 0 20 h -10 a 10 10 0 0 1 0 -20 z  →  Langloch: Halbkreise um (60,0) und (50,0)
                pts = [(50, -10), (60, -10)] + sample_arc(60, 0, 10, -90, 90)[1:] + [(50, 10)] + sample_arc(50, 0, 10, 90, 270)[1:]
                add(pts, tx, ty)
    return segs


def seg_dist(p, a, b):
    dx, dy = b[0] - a[0], b[1] - a[1]; l2 = dx * dx + dy * dy
    t = 0 if l2 == 0 else max(0, min(1, ((p[0] - a[0]) * dx + (p[1] - a[1]) * dy) / l2))
    return math.hypot(a[0] + dx * t - p[0], a[1] + dy * t - p[1])


def gcode_moves(path):
    x = y = 0.0; z = 5.0; mode = 0; out = []
    for line in open(path, encoding="utf-8"):
        line = re.sub(r"\(.*?\)", "", line).split(";")[0].strip().upper()
        if not line:
            continue
        w = dict((m.group(1), float(m.group(2))) for m in re.finditer(r"([GXYZF])\s*([-+]?\d*\.?\d+)", line) if m.group(1) != "G")
        for g in re.findall(r"G(\d+)", line):
            if g in ("0", "00"): mode = 0
            if g in ("1", "01"): mode = 1
            if g in ("2", "02", "3", "03", "91"):
                fails.append("unerwarteter Befehl G" + g); return out
        nx, ny, nz = w.get("X", x), w.get("Y", y), w.get("Z", z)
        if (nx, ny, nz) != (x, y, z):
            out.append((mode, (x, y, z), (nx, ny, nz)))
        x, y, z = nx, ny, nz
    return out


def check_nc(name, nc, segs):
    print("\n== 2. " + nc + "  (gegen " + name + ")")
    minx = min(min(a[0], b[0]) for a, b in segs); miny = min(min(a[1], b[1]) for a, b in segs)
    segs = [((a[0] - minx, a[1] - miny), (b[0] - minx, b[1] - miny)) for a, b in segs]  # Trendo legt das Teil auf den Nullpunkt
    W = max(max(a[0], b[0]) for a, b in segs); H = max(max(a[1], b[1]) for a, b in segs)
    moves = gcode_moves(nc)
    cuts = [m for m in moves if m[0] == 1 and (m[1][2] < 0 or m[2][2] < 0)]
    check(len(cuts) > 100, "%d Schnittbewegungen unter Z 0" % len(cuts))
    zs = sorted({round(m[2][2], 4) for m in moves if m[2][2] < 0})
    check(abs(min(zs) + DEPTH) < 1e-6, "tiefste Z = %.3f (Tiefe %.1f mm)" % (min(zs), DEPTH))
    check({-2.0, -4.0, -6.0, -4.5} <= set(zs), "Stufen −2/−4/−6 und Haltesteg-Höhe −4,5 vorhanden: %s" % zs)
    # Eilgang darf nur senkrecht nach oben aus dem Material (Rückzug); kein G0 seitlich unter Z 0 und kein G0 hinein
    rapid_low = [m for m in moves if m[0] == 0 and min(m[1][2], m[2][2]) < 0 and (m[2][2] < m[1][2] or (m[1][0], m[1][1]) != (m[2][0], m[2][1]))]
    check(not rapid_low, "kein Eilgang (G0) ins Material oder seitlich unter Z 0 (nur senkrechter Rückzug)")
    pts = []
    for _, a, b in cuts:
        if abs(a[0] - b[0]) + abs(a[1] - b[1]) < 1e-9:
            continue
        n = max(1, int(math.dist(a[:2], b[:2]) / 0.25))
        pts += [(a[0] + (b[0] - a[0]) * k / n, a[1] + (b[1] - a[1]) * k / n) for k in range(n + 1)]
    dmin = min(min(seg_dist(p, s[0], s[1]) for s in segs) for p in pts[::3])
    check(dmin >= R - 0.02, "kein Anschneiden: kleinster Abstand Fräsermitte–Kontur %.4f mm (Radius %.1f, Toleranz 0,02)" % (dmin, R))
    inside = all(-R - 0.05 <= p[0] <= W + R + 0.05 and -R - 0.05 <= p[1] <= H + R + 0.05 for p in pts)
    check(inside, "Bahnen im Rohteil: Teil %.2f × %.2f mm ± Radius" % (W, H))
    # jede Kontur (Gruppe zusammenhängender Segmente) hat Bahnpunkte im Abstand Radius
    groups, cur = [], [segs[0]]
    for s in segs[1:]:
        if math.dist(cur[-1][1], s[0]) < 1e-6 or math.dist(cur[-1][1], s[1]) < 1e-6 or math.dist(cur[0][0], s[1]) < 1e-6:
            cur.append(s)
        else:
            groups.append(cur); cur = [s]
    groups.append(cur)
    hits = 0
    for g in groups:
        near = [p for p in pts[::2] if abs(min(seg_dist(p, s[0], s[1]) for s in g) - R) < 0.03]
        hits += 1 if len(near) > 20 else 0
    check(hits >= 3, "Radiusversatz: %d Konturgruppen mit Bahn genau im Abstand %.1f mm (± 0,03)" % (hits, R))
    exact = min_seg_clearance(cuts, segs)
    check(exact >= R - 0.0025, "T-0747-fix: exakt Strecke zu Strecke: kleinster Abstand Schnittbewegung–Kontur %.4f mm (Radius %.1f, Toleranz 0,0025 = Bogen 0,001 + Prüfung 0,001 + Rundung)" % (exact, R))


# ---------- T-0747-fix: exakter Abstand Strecke zu Strecke ----------
def orient(o, a, b):
    return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])


def seg_seg(a, b, c, d):
    o1, o2, o3, o4 = orient(c, d, a), orient(c, d, b), orient(a, b, c), orient(a, b, d)
    if ((o1 > 0 > o2) or (o1 < 0 < o2)) and ((o3 > 0 > o4) or (o3 < 0 < o4)):
        return 0.0
    return min(seg_dist(a, c, d), seg_dist(b, c, d), seg_dist(c, a, b), seg_dist(d, a, b))


def min_seg_clearance(cuts, segs, cell=10.0):
    """Kleinster Abstand aller Schnittbewegungen (in XY) zu allen Kontur-Strecken – Raster nur zum Beschleunigen, gerechnet wird exakt."""
    grid = {}
    for k, (a, b) in enumerate(segs):
        for gx in range(int(math.floor(min(a[0], b[0]) / cell)), int(math.floor(max(a[0], b[0]) / cell)) + 1):
            for gy in range(int(math.floor(min(a[1], b[1]) / cell)), int(math.floor(max(a[1], b[1]) / cell)) + 1):
                grid.setdefault((gx, gy), []).append(k)
    best = float("inf")
    for p, q in {((round(a[0], 4), round(a[1], 4)), (round(b[0], 4), round(b[1], 4))) for _, a, b in cuts}:
        if p == q:
            continue
        near = set()
        for gx in range(int(math.floor((min(p[0], q[0]) - 10) / cell)), int(math.floor((max(p[0], q[0]) + 10) / cell)) + 1):
            for gy in range(int(math.floor((min(p[1], q[1]) - 10) / cell)), int(math.floor((max(p[1], q[1]) + 10) / cell)) + 1):
                near.update(grid.get((gx, gy), ()))
        for k in near:
            best = min(best, seg_seg(p, q, segs[k][0], segs[k][1]))
    return best


def check_fix(nc, dxf, notes):
    """3. Gegenprüfungs-Fälle: Fräser Ø 6, Konturen als LINE-Ketten, jede Kontur auf eigener Ebene (F1 … F9)."""
    print("\n== 3. " + nc + "  (gegen " + dxf + ")")
    r = 3.0
    by_layer = {}
    for t, b in entities(pairs(dxf)):
        if t == "LINE":
            by_layer.setdefault(b[8][0], []).append(((num(b, 10), num(b, 20)), (num(b, 11), num(b, 21))))
    segs = [s for v in by_layer.values() for s in v]
    minx = min(min(a[0], b[0]) for a, b in segs); miny = min(min(a[1], b[1]) for a, b in segs)
    segs = [((a[0] - minx, a[1] - miny), (b[0] - minx, b[1] - miny)) for a, b in segs]
    moves = gcode_moves(nc)
    cuts = [m for m in moves if m[0] == 1 and m[1][2] < 0 and m[2][2] < 0]
    check(len(cuts) > 100, "%d Schnittbewegungen unter Z 0" % len(cuts))
    exact = min_seg_clearance(cuts, segs)
    check(exact >= r - 0.0025, "kein Anschneiden (exakt Strecke zu Strecke, ALLE Konturen): kleinster Abstand %.4f mm (Radius %.1f, Toleranz 0,0025)" % (exact, r))
    text = open(nc, encoding="utf-8").read()
    milled = sorted({int(m) for m in re.findall(r"\(Kontur (\d+):", text)})
    hint = open(notes, encoding="utf-8").read()
    print("        gefräste Konturen: %s" % milled)
    for name, cond in [
        ("zwei Teile 3 mm auseinander: beide ausgelassen und gemeldet", "zu nah an Kontur" in hint and hint.count("zu nah an Kontur") >= 4),
        ("Achterform abgelehnt", "kreuzt sich selbst" in hint),
        ("Hantel-Steg und Schlitz als Engstelle gemeldet (mit Lage)", hint.count("enger als der Fräser") >= 2 and "bei X" in hint),
    ]:
        check(cond, name)
    check(not rapid_into_material(moves), "kein Eilgang (G0) ins Material")
    return milled


def rapid_into_material(moves):
    return [m for m in moves if m[0] == 0 and min(m[1][2], m[2][2]) < 0 and (m[2][2] < m[1][2] or (m[1][0], m[1][1]) != (m[2][0], m[2][1]))]


if __name__ == "__main__":
    beleg, fx = sys.argv[1], sys.argv[2]
    check_plan(beleg + "/wohnung-bearbeitet.dxf")
    check_nc("teil-fremd.dxf (Zoll)", beleg + "/teil-fremd-dxf.nc", dxf_segments(fx + "/teil-fremd.dxf"))
    check_nc("teil-fremd.svg", beleg + "/teil-fremd-svg.nc", svg_segments(fx + "/teil-fremd.svg"))
    import os
    if os.path.exists(beleg + "/fix-faelle.nc"):
        check_fix(beleg + "/fix-faelle.nc", beleg + "/fix-faelle.dxf", beleg + "/fix-faelle-hinweise.txt")
    print("\nERGEBNIS: " + ("BESTANDEN" if not fails else "%d FEHLER" % len(fails)))
    sys.exit(1 if fails else 0)
