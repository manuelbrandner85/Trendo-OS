# T-0760 Fremdbeleg: Abstand Trendo-Geometrie ↔ FreeCAD-Geometrie (beide fein abgetastet, Punkte auf Strecken), je FreeCAD-Objekt und je Trendo-Element
import json, math, sys, collections
fc = json.load(open(sys.argv[1])); tr = json.load(open(sys.argv[2], encoding='utf-8'))
CELL = 2.0
def grid(polys):
    g = collections.defaultdict(list)
    for pi, pts in enumerate(polys):
        for i in range(len(pts) - 1):
            (ax, ay), (bx, by) = pts[i], pts[i + 1]
            for gx in range(int(math.floor(min(ax, bx) / CELL)), int(math.floor(max(ax, bx) / CELL)) + 1):
                for gy in range(int(math.floor(min(ay, by) / CELL)), int(math.floor(max(ay, by) / CELL)) + 1):
                    g[(gx, gy)].append((pi, i))
    return g
def seg(p, a, b):
    dx, dy = b[0] - a[0], b[1] - a[1]; l2 = dx * dx + dy * dy
    t = 0 if l2 == 0 else max(0, min(1, ((p[0] - a[0]) * dx + (p[1] - a[1]) * dy) / l2))
    return math.hypot(p[0] - a[0] - t * dx, p[1] - a[1] - t * dy)
def dist(p, polys, g):
    gx, gy = int(math.floor(p[0] / CELL)), int(math.floor(p[1] / CELL)); best = 1e9
    for i in range(gx - 1, gx + 2):
        for j in range(gy - 1, gy + 2):
            for pi, k in g.get((i, j), []): best = min(best, seg(p, polys[pi][k], polys[pi][k + 1]))
    return best
tpolys = [e['Pts'] + ([e['Pts'][0]] if e['Closed'] or e['Kind'] == 'kreis' else []) for e in tr['Ents']]
tg = grid(tpolys)
byobj = collections.defaultdict(list)
for e in fc['edges']: byobj[e['obj']].append(e['pts'])
print('FreeCAD', fc['log'])
print('Trendo: nicht gelesen', tr['Skipped'], tr['Unread'])
matched = []
for o, ps in sorted(byobj.items()):
    d = max(dist(p, tpolys, tg) for pts in ps for p in pts)
    tag = 'deckt sich' if d < 0.01 else 'Blockdefinition (nicht gezeichnet)' if o.startswith('BLOCK_') or o in ('Shape', 'Shape001', 'Shape002') else 'ABWEICHUNG'
    print(f'  FreeCAD {o:16s} {len(ps):3d} Kanten: größter Abstand zu Trendo {d:.6f} mm → {tag}')
    if d < 0.01: matched.extend(ps)
fg = grid(matched)
worst = 0
for i, e in enumerate(tr['Ents']):
    d = max(dist(p, matched, fg) for p in e['Pts']); worst = max(worst, d)
    print(f'  Trendo {i:2d} {e["Kind"]:9s} {e["Source"]:7s} {len(e["Pts"]):5d} Punkte: größter Abstand zu FreeCAD {d:.6f} mm')
print(f'ERGEBNIS: größter Abstand Trendo → FreeCAD {worst:.6f} mm')
