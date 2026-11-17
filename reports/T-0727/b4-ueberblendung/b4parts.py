import os, sys, statistics, subprocess, time, re
sys.path.insert(0, os.path.dirname(__file__))
import b4
pre, filt = b4.pre, b4.filt
parts = filt.split(';')
# Teile: [0:v]...[v0] ; [0:a]...[a0] ; [1:v]...[vt1] ; color...[v1bg] ; [vt1]scale...[v1fg] ; [v1bg][v1fg]overlay...[v1] ; [2:a]...[a1] ; xfade ; acrossfade ; null ; anullsink ; vlook
def without_b_fx(ps):
    out = []
    for p in ps:
        if p.startswith('[1:v]'):
            out.append(p.replace(',vignette=angle=0.46[vt1]', '[v1]'))
        elif p.startswith('color=') or p.startswith('[vt1]') or p.startswith('[v1bg]'):
            continue
        else:
            out.append(p)
    return ';'.join(out)
def without_a_fx(ps):
    return ';'.join(re.sub(r',eq=[^\[]*\[v0\]', '[v0]', p) if p.startswith('[0:v]') else p for p in ps)
def without_audio(ps):
    out = []
    for p in ps:
        if p.startswith('[0:a]') or p.startswith('[2:a]') or 'acrossfade' in p or p.startswith('[abase]'):
            continue
        out.append(p)
    return ';'.join(out)
variants = {'original': filt, 'ohne_b_effekte': without_b_fx(parts), 'ohne_a_effekte': without_a_fx(parts), 'ohne_ton': without_audio(parts)}
res = {k: [] for k in variants}
for rnd in range(int(os.environ.get('N', '7'))):
    for k, f in variants.items():
        args = ['ffmpeg'] + pre + ['-filter_complex', f] + b4.post
        t = time.perf_counter(); r = subprocess.run(args, capture_output=True, text=True); ms = (time.perf_counter() - t) * 1000
        if r.returncode:
            print(k, r.stderr[-300:]); break
        res[k].append(ms)
for k, v in res.items():
    if v: print(f'{k:20s} Median {statistics.median(v):6.0f} ms  min {min(v):6.0f}')
