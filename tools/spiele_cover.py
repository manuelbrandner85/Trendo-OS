"""Eigene Cover für die Spiele-Bibliothek lokal erzeugen (ComfyUI auf 127.0.0.1, FLUX.2 klein). Keine Vorlagen, keine Logos, keine bestehenden Figuren.

Aufruf:  python tools/spiele_cover.py [--ids morvane ...]
Ausgabe: src/Trendo.App/Assets/bibliothek/<id>.jpg (480 × 672)
"""
import argparse, os, shutil, subprocess, sys, tempfile, time, urllib.request, uuid

sys.path.insert(0, os.path.dirname(__file__))
from studio_bilder import req, workflow  # noqa: E402

STYLE = ('cinematic key art for an original video game, vertical poster composition, dramatic believable lighting, rich detail, '
         'no text, no letters, no title, no logos, no watermark, no brand names')

COVERS = {
    'morvane': 'an invented rainy harbor city at night seen from a rooftop, container cranes, wet streets with car headlights, a lone courier in a hooded jacket beside a plain unbranded motorcycle',
    'moosgrund': 'a tiny glowing forest spirit made of moss leaping between giant mossy tree tops, shafts of warm sunlight, stylized early 2000s 3D game look',
    'ascheturm': 'an armored lone knight with a worn greatsword before an immense tower of grey ash and drifting embers, gloomy dusk',
    'kuestenrallye': 'two plain unbranded rally cars drifting around a sunny coastal cliff road, turquoise sea, dust clouds, early 2000s 3D racing game look',
    'eisfront': 'an abandoned arctic research station in a blizzard at night, one flickering window, a figure with a flashlight in a heavy parka',
    'klingenbund': 'four mismatched fantasy travelers standing on a hill looking over a fading kingdom, sunset, painterly early 2000s role playing game look',
    'funkstille': 'a silent agent in dark tactical clothing crouching in a concrete corridor, narrow beams of red light, deep shadows',
    'sternenwerft': 'a huge orbital shipyard above a blue planet, a small patched-together spaceship leaving the dock, laser sparks in the distance',
    'demo-platformer-3d': 'a small cheerful robot jumping across floating grass islands collecting golden coins, bright blue sky, stylized 3D',
    'demo-squash': 'a small round hero jumping onto colorful blob creatures on a simple arena floor, playful stylized 3D',
    'demo-truck': 'a sturdy plain unbranded truck driving through a tiny toy-like town on a hilly road, warm afternoon light',
    'demo-platformer-2d': 'a pixel art scene of a small adventurer running across green platforms in a cave, retro palette',
    'demo-dodge': 'a small character dodging swarms of little creatures on a dark blue field, simple playful 2D illustration',
    'hb-libbet': 'a small girl unrolling a magic glowing carpet tile by tile across a dark floor, four-color green handheld game palette, pixel art',
    'hb-ucity': 'a tiny pixel art city seen from above with roads, houses and a power plant, bright handheld game colors',
}

def render(cid, seed, out, ffmpeg):
    pid = req('/prompt', {'prompt': workflow(f'{COVERS[cid]}, {STYLE}', seed, w=768, h=1072), 'client_id': uuid.uuid4().hex})['prompt_id']
    for _ in range(600):
        h = req(f'/history/{pid}').get(pid)
        if h and h.get('outputs'):
            break
        time.sleep(0.5)
    else:
        raise TimeoutError(cid)
    im = [i for o in h['outputs'].values() for i in o.get('images', [])][0]
    png = req(f"/view?filename={urllib.request.quote(im['filename'])}&subfolder={urllib.request.quote(im.get('subfolder', ''))}&type={im.get('type', 'output')}", timeout=60)
    tmp = os.path.join(tempfile.gettempdir(), f'trendo-cover-{cid}.png')
    with open(tmp, 'wb') as f:
        f.write(png)
    subprocess.run([ffmpeg, '-y', '-loglevel', 'error', '-i', tmp, '-vf', 'scale=480:672', '-q:v', '3', os.path.join(out, f'{cid}.jpg')], check=True)
    os.remove(tmp)

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--ids', nargs='*')
    ap.add_argument('--seed', type=int, default=20261109)
    ap.add_argument('--out', default=os.path.join(os.path.dirname(__file__), '..', 'src', 'Trendo.App', 'Assets', 'bibliothek'))
    ap.add_argument('--ffmpeg', default=shutil.which('ffmpeg') or 'ffmpeg')
    a = ap.parse_args()
    out = os.path.abspath(a.out)
    os.makedirs(out, exist_ok=True)
    for i, cid in enumerate(a.ids or list(COVERS)):
        t = time.time()
        render(cid, a.seed + i, out, a.ffmpeg)
        print(f'COVER {cid} {time.time() - t:.1f}s', flush=True)

if __name__ == '__main__':
    main()
