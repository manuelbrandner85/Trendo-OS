import subprocess, time, sys, os, re, statistics
M = os.path.join(os.environ['TEMP'], 'trendo-auftrag-T-0727-mess', 'b4src')
line = open(os.path.join(M, os.environ.get('B4CMD', 'cmd35.txt')), encoding='utf-8').read().strip()
pre, rest = line.split(' -filter_complex ', 1)
filt, post = rest.split(' -map ', 1)
pre = [os.path.join(M, os.path.basename(t.replace('\\', '/'))) if t.lower().endswith(('.mp4', '.png')) and '\\' in t else t for t in pre.split(' ')]
post = ['-map'] + post.split(' ')
post[-1] = os.path.join(M, 'out.png')


def run(f, p=None, n=7):
    args = ['ffmpeg'] + (p or pre) + ['-filter_complex', f] + post
    ts = []
    for _ in range(n):
        t = time.perf_counter(); r = subprocess.run(args, capture_output=True, text=True); ts.append((time.perf_counter() - t) * 1000)
        if r.returncode:
            print(r.stderr[-600:]); return None
    return round(statistics.median(ts)), round(min(ts))


if __name__ == '__main__':
    print('original', run(filt))
    dec = '[0:v]null[x0];[1:v]null[x1];[x0][x1]concat=n=2:v=1:a=0,trim=start=100[vlook]'
    print('nur_dekodieren(alle Bilder bis -t)', run(dec, n=5))
    print('ffmpeg leer (Start ohne Arbeit)', run('color=c=black:s=16x16:d=0.04[vlook]', p=['-hide_banner', '-loglevel', 'error', '-y'], n=5))
