import os, sys, statistics, subprocess, time
sys.path.insert(0, os.path.dirname(__file__))
import b4
pre = b4.pre
f960 = b4.filt.replace('setpts=PTS-STARTPTS[vlook]', 'setpts=PTS-STARTPTS,scale=960:-2[vlook]')
assert f960 != b4.filt
out = os.path.join(b4.M, 'p.png')
base = ['-map', '[vlook]', '-frames:v', '1', '-update', '1']
variants = {
    'png_standard': base + [out],
    'png_level1': base + ['-compression_level', '1', out],
    'png_level0': base + ['-compression_level', '0', out],
    'bmp_unkomprimiert': base + [out.replace('.png', '.bmp')],
}
res = {k: [] for k in variants}
sizes = {}
n = int(os.environ.get('N', '9'))
for rnd in range(n):
    for k, post in variants.items():
        args = ['ffmpeg'] + pre + ['-filter_complex', f960] + post
        t = time.perf_counter(); r = subprocess.run(args, capture_output=True, text=True); ms = (time.perf_counter() - t) * 1000
        if r.returncode:
            print(k, r.stderr[-300:]); continue
        res[k].append(ms); target = post[-1]; sizes[k] = os.path.getsize(target)
        os.replace(target, os.path.join(b4.M, f'png-{k}' + os.path.splitext(target)[1]))
for k, v in res.items():
    if v: print(f'{k:20s} Median {statistics.median(v):6.0f} ms  min {min(v):6.0f}  Datei {sizes[k] // 1024} KB')
