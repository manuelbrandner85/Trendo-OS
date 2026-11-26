# T-0772: Nachbau der Prüfdateien k5_dicht, b2, e0, z1, p1 (die Originale der Prüfrunden T-0747 liegen nicht mehr auf dem PC).
# Die Formen folgen den Namen und Meldungen in reports/T-0758 (Platte 1000 x 1000, Linien von X 0 nach X 1000 dicht um Y 500 usw.).
# Aufruf: python erzeuge772.py <zielordner>
import math, os, random, sys

out = sys.argv[1]
os.makedirs(out, exist_ok=True)


class Dxf:
    def __init__(self):
        self.L = []
        self.layers = []

    def g(self, c, v):
        self.L.append(f"{c}\n{v}\n")

    def lw(self, pts, closed=True, layer="0"):
        self.g(0, "LWPOLYLINE"); self.g(8, layer); self.g(90, len(pts)); self.g(70, 1 if closed else 0)
        for x, y in pts:
            self.g(10, f"{x:.6f}"); self.g(20, f"{y:.6f}")

    def line(self, x0, y0, x1, y1, layer="0"):
        self.g(0, "LINE"); self.g(8, layer); self.g(10, f"{x0:.6f}"); self.g(20, f"{y0:.6f}"); self.g(30, 0); self.g(11, f"{x1:.6f}"); self.g(21, f"{y1:.6f}"); self.g(31, 0)

    def circle(self, x, y, r, layer="0"):
        self.g(0, "CIRCLE"); self.g(8, layer); self.g(10, f"{x:.6f}"); self.g(20, f"{y:.6f}"); self.g(30, 0); self.g(40, f"{r:.6f}")

    def save(self, name):
        head = ["0\nSECTION\n2\nHEADER\n9\n$ACADVER\n1\nAC1015\n9\n$INSUNITS\n70\n4\n0\nENDSEC\n"]
        if self.layers:
            t = "0\nSECTION\n2\nTABLES\n0\nTABLE\n2\nLAYER\n"
            for n, color in self.layers:
                t += f"0\nLAYER\n2\n{n}\n70\n0\n62\n{color}\n6\nCONTINUOUS\n"
            t += "0\nENDTAB\n0\nENDSEC\n"
            head.append(t)
        body = "0\nSECTION\n2\nENTITIES\n" + "".join(self.L) + "0\nENDSEC\n0\nEOF\n"
        with open(os.path.join(out, name + ".dxf"), "w", encoding="ascii", newline="\n") as f:
            f.write("".join(head) + body)


def plate(d, w=1000, h=1000):
    d.lw([(0, 0), (w, 0), (w, h), (0, h)])


def k5(a, b):
    # Platte, ein kleines dichtes Vieleck (a Ecken, r 2 mm, zu klein für den 6-mm-Fräser) und b Linien von X 0 nach X 1000 dicht um Y 500 (Band 3 mm)
    rnd = random.Random(5000 + a + b); d = Dxf(); plate(d)
    if a > 0:
        d.lw([(100 + 2 * math.cos(2 * math.pi * i / a), 100 + 2 * math.sin(2 * math.pi * i / a)) for i in range(a)])
    for _ in range(b):
        d.line(0, 500 + 3 * rnd.random(), 1000, 500 + 3 * rnd.random())
    d.save(f"k5_dicht_{a}_{b}")


def b2(n):
    # n fast gleiche Umrisse: Quadrat 100 x 100 mit einer versteckten Kerbe nach innen (0,5 mm tief, 0,0008 mm breit) an wechselnder Stelle – gleicher Rahmen, fast gleiche Fläche
    d = Dxf()
    for i in range(n):
        x = 1 + 98 * (i + 0.5) / n
        d.lw([(0, 0), (x - 0.0004, 0), (x, 0.5), (x + 0.0004, 0), (100, 0), (100, 100), (0, 100)])
    d.save(f"b2_gleicher_rahmen_versteckt_{n}")


def e0(n):
    # n Kreise r 1 mm, gleichmäßig verteilt (Kontrolle zum „Eimer“), keine Platte
    d = Dxf(); k = int(math.ceil(math.sqrt(n)))
    for i in range(n):
        d.circle(10 * (i % k), 10 * (i // k), 1)
    d.save(f"e0_kontrolle_verteilt_{n}")


def z1(n, holes):
    # geschlossener Zickzack mit n senkrechten Zähnen (Teilung 2 mm, Höhe 200 mm) und holes kleinen Löchern im Steg darunter
    d = Dxf(); pts = [(0, -50)]
    for i in range(n):
        x = 2 * i
        pts += [(x, 0), (x + 1, 200)]
    pts += [(2 * n, 0), (2 * n, -50)]
    d.lw(pts)
    for i in range(holes):
        d.circle(1 + (2 * n - 2) * (i + 0.5) / holes, -25, 4)
    d.save(f"z1_zickzack_senkrecht_{n}_versteckt_{holes}")


def p1(lines, circles):
    # Platte, lines schräge Linien durch einen Punkt (Gravur, Fräsart „auf der Linie“) und circles Löcher r 1,5 mm
    d = Dxf(); plate(d); rnd = random.Random(77 + lines)
    for i in range(lines):
        t = math.pi * (i + 0.5) / lines
        d.line(500 - 450 * math.cos(t), 500 - 450 * math.sin(t), 500 + 450 * math.cos(t), 500 + 450 * math.sin(t))
    k = int(math.ceil(math.sqrt(circles)))
    for i in range(circles):
        d.circle(20 + 960 * (i % k + 0.5) / k, 20 + 960 * (i // k + 0.5) / k, 1.5)
    d.save(f"p1_diag_gravur_{lines}_{circles}")


def k5b(a, b, spacing):
    # wie k5, aber Band = b * spacing mm hoch (mittlerer Abstand spacing): eindeutige und mehrdeutige Spalten gemischt, viele Verkettungen
    rnd = random.Random(7000 + a + b); d = Dxf(); plate(d)
    if a > 0:
        d.lw([(100 + 2 * math.cos(2 * math.pi * i / a), 100 + 2 * math.sin(2 * math.pi * i / a)) for i in range(a)])
    hb = b * spacing
    for _ in range(b):
        d.line(0, 500 + hb * rnd.random(), 1000, 500 + hb * rnd.random())
    d.save(f"k5b_dicht_{a}_{b}_{int(spacing*1000)}")


for a, b, sp in [(2000, 3000, 0.05), (2000, 30000, 0.05), (500, 60000, 0.05), (2000, 3000, 0.2), (2000, 30000, 0.2)]:
    k5b(a, b, sp)
for a, b in [(2000, 30000), (500, 60000), (20000, 3000), (2000, 3000), (20000, 300)]:
    k5(a, b)
b2(20000); b2(2000)
e0(100000); e0(10000)
z1(100000, 2000); z1(10000, 200)
p1(1000, 10000); p1(200, 10000)
print("fertig")
