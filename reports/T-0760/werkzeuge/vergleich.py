"""T-0760: Lese-Ergebnis main gegen neu je Datei vergleichen. Unterschied erlaubt nur, wo main etwas „nicht gelesen“ meldete,
das T-0760 jetzt liest (INSERT, SPLINE, MLINE, Element in Blockdefinition)."""
import glob, json, sys, collections
S = sys.argv[1]; pre = sys.argv[2] if len(sys.argv) > 2 else 'lese'
def load(v):
    d = {}
    for f in glob.glob(f'{S}/{pre}-{v}-*.jsonl'):
        for l in open(f, encoding='utf-8'):
            x = json.loads(l); x.pop('ms', None); d[x['file']] = x
    return d
a, b = load('main'), load('new')
NEW = {'INSERT', 'SPLINE', 'MLINE', 'Element in Blockdefinition'}
same = diff_ok = 0; bad = []; cats = collections.Counter(); rows = []
for f in sorted(a):
    x, y = a[f], b.get(f)
    if y is None: bad.append((f, 'fehlt in neu')); continue
    if x == y: same += 1; continue
    old_unread = set((x.get('skipped') or {}).keys()) & NEW
    if old_unread:
        diff_ok += 1
        for k in old_unread: cats[k] += 1
        rows.append((f, sorted(old_unread), x.get('skipped'), y.get('skipped'), x.get('kinds'), y.get('kinds'), x.get('read'), y.get('read')))
    else:
        keys = [k for k in set(x) | set(y) if x.get(k) != y.get(k)]
        bad.append((f, keys, {k: (x.get(k), y.get(k)) for k in keys}))
print(f'Dateien: {len(a)}; gleich: {same}; anders, wo vorher nicht gelesen: {diff_ok} {dict(cats)}; anders OHNE Grund: {len(bad)}')
for r in bad[:30]: print('UNERKLÄRT', json.dumps(r, ensure_ascii=False)[:600])
if len(sys.argv) > 3:
    with open(sys.argv[3], 'w', encoding='utf-8') as o:
        for r in rows: o.write(json.dumps(r, ensure_ascii=False) + '\n')
        for r in bad: o.write('UNERKLÄRT ' + json.dumps(r, ensure_ascii=False) + '\n')
