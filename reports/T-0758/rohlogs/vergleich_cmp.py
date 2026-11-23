import json, os, sys, hashlib
# cmp.py <alt> <neu>: G-Code (Bytes), Meldungen, ok/Fehler je Fall vergleichen; Zeiten ausgeben
a, b = sys.argv[1], sys.argv[2]

def bighash(path):
    h = hashlib.sha256(); size = os.path.getsize(path)
    with open(path, 'rb') as f:
        f.seek(max(0, size - 200)); tail = f.read(); cut = tail.rfind(b',"ms":')
        end = size - len(tail) + cut if cut >= 0 else size
        f.seek(0); left = end
        while left > 0:
            b = f.read(min(1 << 24, left)); h.update(b); left -= len(b)
    return h.hexdigest()
def gfile(p):
    if not os.path.exists(p): return None
    if os.path.getsize(p) > 200_000_000:
        h = hashlib.sha256()
        with open(p, 'rb') as f:
            while True:
                b = f.read(1 << 24)
                if not b: break
                h.update(b)
        return h.hexdigest().encode()
    return open(p, 'rb').read()
tot = same = 0; diffs = []; missing = []; times = []
only = set(open(os.environ["ONLY"]).read().split()) if os.environ.get("ONLY") else None
for d in sorted(os.listdir(a)):
    if only is not None and d not in only: continue
    pa = os.path.join(a, d); pb = os.path.join(b, d)
    if not os.path.isdir(pa): continue
    for fn in sorted(os.listdir(pa)):
        if not fn.endswith(".json"): continue
        n = fn[:-5]
        fa = os.path.join(pa, fn)
        if os.path.getsize(fa) > 300_000_000:
            fbig = os.path.join(pb, fn); tot += 1
            if not os.path.exists(fbig): missing.append(f"{d}/{n}"); continue
            ga2 = gfile(os.path.join(pa, n + ".nc")); gb2 = gfile(os.path.join(pb, n + ".nc"))
            if bighash(fa) == bighash(fbig) and ga2 == gb2: same += 1
            else: diffs.append(f"{d}/{n}: große Datei, Inhalt abweichend")
            continue
        try: ja = json.load(open(fa, encoding="utf-8"))
        except Exception: continue
        if not isinstance(ja, dict) or "name" not in ja: continue
        tot += 1
        fb = os.path.join(pb, fn)
        if not os.path.exists(fb): missing.append(f"{d}/{n}"); continue
        jb = json.load(open(fb, encoding="utf-8"))
        def g(p):
            return gfile(os.path.join(p, n + ".nc"))
        ga, gb = g(pa), g(pb)
        wa, wb = ja.get("warnings", ja.get("error")), jb.get("warnings", jb.get("error"))
        times.append((ja.get("ms", 0), jb.get("ms", 0), f"{d}/{n}"))
        if ga == gb and wa == wb and ja.get("ok") == jb.get("ok"): same += 1; continue
        why = []
        if ja.get("ok") != jb.get("ok"): why.append(f"ok alt={ja.get('ok')} neu={jb.get('ok')}")
        if ga != gb: why.append(f"G-Code alt={hashlib.sha256(ga or b'').hexdigest()[:12]} neu={hashlib.sha256(gb or b'').hexdigest()[:12]}")
        if wa != wb:
            sa = set(wa if isinstance(wa, list) else [wa]); sb = set(wb if isinstance(wb, list) else [wb])
            if sa == sb: why.append("Meldungen: gleiche Menge, andere Reihenfolge")
            else: why.append("Meldungen: nur alt=" + " || ".join(str(x)[:160] for x in list(sa - sb)[:3]) + f" (+{max(0,len(sa-sb)-3)})" + " ## nur neu=" + " || ".join(str(x)[:160] for x in list(sb - sa)[:3]) + f" (+{max(0,len(sb-sa)-3)})")
        diffs.append(f"{d}/{n} ({ja.get('ms',0):.0f} / {jb.get('ms',0):.0f} ms): " + "; ".join(why))
print(f"Fälle {tot}, gleich {same}, abweichend {len(diffs)}, fehlen in neu {len(missing)}")
for x in diffs: print("  " + x)
for x in missing[:50]: print("  fehlt: " + x)
if len(sys.argv) > 3:
    times.sort(key=lambda t: -t[0])
    print("Langsamste (alt ms / neu ms):")
    for t in times[:int(sys.argv[3])]: print(f"  {t[0]:9.0f} {t[1]:9.0f}  {t[2]}")
