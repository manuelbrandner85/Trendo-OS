import os, sys, statistics, subprocess, time
sys.path.insert(0, os.path.dirname(__file__))
import b4
pre, filt = b4.pre, b4.filt


def with_input_opts(opts):
    out = []
    for i, t in enumerate(pre):
        if t == '-ss' and (i == 0 or pre[i - 1] != '-i'):
            out += opts
        out.append(t)
    return out


variants = {
    'original': (filt, pre),
    'filter_threads1': (filt, ['-filter_complex_threads', '1'] + pre),
    'hw_d3d11va': (filt, with_input_opts(['-hwaccel', 'd3d11va'])),
    'hw_cuda': (filt, with_input_opts(['-hwaccel', 'cuda'])),
    'dec_threads_auto_frame': (filt, with_input_opts(['-thread_type', 'frame'])),
}
res = {k: [] for k in variants}
for rnd in range(int(sys.argv[1]) if len(sys.argv) > 1 else 5):
    for k, (f, p) in variants.items():
        args = ['ffmpeg'] + p + ['-filter_complex', f] + b4.post
        t = time.perf_counter(); r = subprocess.run(args, capture_output=True, text=True); ms = (time.perf_counter() - t) * 1000
        if r.returncode:
            res[k].append(None); print(k, r.stderr[-300:]); continue
        res[k].append(ms)
        os.replace(os.path.join(b4.M, 'out.png'), os.path.join(b4.M, f'out-{k}.png'))
for k, v in res.items():
    v = [x for x in v if x is not None]
    if v: print(f'{k:28s} Median {statistics.median(v):6.0f} ms  min {min(v):6.0f}  ({"/".join(str(round(x)) for x in v)})')
