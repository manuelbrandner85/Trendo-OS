import os, sys, statistics, subprocess, time
sys.path.insert(0, os.path.dirname(__file__))
import b4
pre = b4.pre
plain = ("[0:v]setpts=PTS+3/TB,fps=30,select='eq(n\\,0)+gte(t\\,3.491667)',format=yuv420p[v0];"
         "[1:v]fps=30,select='eq(n\\,0)+gte(t\\,0.491667)',format=yuv420p[v1];"
         "[v0][v1]xfade=transition=slideleft:duration=1:offset=0[vbase];[vbase]format=yuv420p,trim=start=3.491667,setpts=PTS-STARTPTS[vlook]")
only_a = "[0:v]setpts=PTS+3/TB,fps=30,select='eq(n\\,0)+gte(t\\,3.491667)',format=yuv420p,trim=start=3.491667,setpts=PTS-STARTPTS[vlook]"
only_b = "[1:v]fps=30,select='eq(n\\,0)+gte(t\\,0.491667)',format=yuv420p,trim=start=0.491667,setpts=PTS-STARTPTS[vlook]"
pre_noaudio = [t for t in pre]
variants = {'original': b4.filt, 'ohne_effekte_nur_xfade': plain, 'nur_a_bis_stelle': only_a, 'nur_b_bis_stelle': only_b}
res = {k: [] for k in variants}
for rnd in range(7):
    for k, f in variants.items():
        args = ['ffmpeg'] + pre + ['-filter_complex', f] + b4.post
        t = time.perf_counter(); r = subprocess.run(args, capture_output=True, text=True); ms = (time.perf_counter() - t) * 1000
        if r.returncode:
            print(k, r.stderr[-400:]); res[k].append(None); continue
        res[k].append(ms)
for k, v in res.items():
    v = [x for x in v if x is not None]
    if v: print(f'{k:26s} Median {statistics.median(v):6.0f} ms  min {min(v):6.0f}')

import re as _re
def reorder(f):
    # scale/pad/setsar hinter fps+select ziehen (nur Stücke mit select)
    pat = _re.compile(r"(scale=1920:1080:force_original_aspect_ratio=decrease,pad=1920:1080:\(ow-iw\)/2:\(oh-ih\)/2,setsar=1),(fps=30,select='[^']*')")
    return pat.sub(lambda m: m.group(2) + ',' + m.group(1), f)
r = reorder(b4.filt)
print('umgestellt:', r != b4.filt)
res2 = {'original': [], 'select_vor_scale': []}
for rnd in range(9):
    for k, f in (('original', b4.filt), ('select_vor_scale', r)):
        args = ['ffmpeg'] + pre + ['-filter_complex', f] + b4.post
        t = time.perf_counter(); rr = subprocess.run(args, capture_output=True, text=True); res2[k].append((time.perf_counter() - t) * 1000)
        os.replace(os.path.join(b4.M, 'out.png'), os.path.join(b4.M, f'out2-{k}.png'))
for k, v in res2.items(): print(f'{k:26s} Median {statistics.median(v):6.0f} ms  min {min(v):6.0f}')
