# T-0772: Vergleichs-Zeichnungen für alt/neu (gleicher G-Code, gleiche Meldungen). Aufruf: python korpus772.py <ordner> <anzahl>
import math, os, random, sys

out = sys.argv[1]; n = int(sys.argv[2]); os.makedirs(out, exist_ok=True)


def f(v):
    return f"{v:.4f}"


for k in range(n):
    r = random.Random(k); L = []

    def g(c, v): L.append(f"{c}\n{v}\n")

    def lw(pts, closed=True, layer="0"):
        g(0, "LWPOLYLINE"); g(8, layer); g(90, len(pts)); g(70, 1 if closed else 0)
        for x, y in pts: g(10, f(x)); g(20, f(y))

    def line(a, b, layer="0"):
        g(0, "LINE"); g(8, layer); g(10, f(a[0])); g(20, f(a[1])); g(30, 0); g(11, f(b[0])); g(21, f(b[1])); g(31, 0)

    def circle(x, y, rr): g(0, "CIRCLE"); g(8, "0"); g(10, f(x)); g(20, f(y)); g(30, 0); g(40, f(rr))

    W, H = r.choice([100, 200, 400]), r.choice([80, 150, 300])
    kind = k % 6
    if r.random() < 0.8: lw([(0, 0), (W, 0), (W, H), (0, H)])
    for _ in range(r.randint(0, 40)):  # Löcher
        circle(r.uniform(5, W - 5), r.uniform(5, H - 5), r.choice([1, 2, 3.5, 5, 8]))
    if kind in (1, 4):  # lange schräge Gravurlinien
        for _ in range(r.randint(1, 30)):
            line((r.uniform(0, W), 0 if r.random() < .5 else H), (r.uniform(0, W), H if r.random() < .5 else 0))
    if kind in (2, 4):  # Rechtecke aus Linien mit Spalten (auch gleich große)
        for _ in range(r.randint(1, 15)):
            x, y, w, h = r.uniform(5, W - 40), r.uniform(5, H - 40), r.uniform(10, 30), r.uniform(10, 30)
            gp = r.choice([0, 0.02, 0.05, 0.05, 0.08, 0.15])
            line((x + gp, y), (x + w, y)); line((x + w, y + gp), (x + w, y + h)); line((x + w - gp, y + h), (x, y + h)); line((x, y + h - gp), (x, y))
    if kind == 3:  # fast gleiche Umrisse (versteckte Kerbe)
        for i in range(r.randint(2, 80)):
            xx = 10 + 60 * r.random()
            lw([(0, 0), (xx - 0.0004, 0), (xx, r.choice([0.0005, 0.5])), (xx + 0.0004, 0), (W, 0), (W, H), (0, H)])
    if kind == 5:  # dichte Linien mit Spalten
        for _ in range(r.randint(10, 200)):
            y0 = H / 2 + r.uniform(0, 5); line((0, y0), (W, H / 2 + r.uniform(0, 5)))
    mode = r.choice(["auto", "auto", "auf", "aussen", "innen"])
    body = "0\nSECTION\n2\nHEADER\n9\n$ACADVER\n1\nAC1015\n9\n$INSUNITS\n70\n4\n0\nENDSEC\n0\nSECTION\n2\nENTITIES\n" + "".join(L) + "0\nENDSEC\n0\nEOF\n"
    name = f"k{k:04d}" + ("_gravur" if mode == "auf" else "")
    open(os.path.join(out, name + ".dxf"), "w", encoding="ascii", newline="\n").write(body)
print("fertig", n)
