"""Fotorealistische Symbole für die Leiste (Dock) von Trendo OS lokal erzeugen (Wunsch 03.11.2026: Leiste viel fotorealistischer und schöner).

Nutzt dieselbe lokale ComfyUI mit FLUX.2 klein wie tools/studio_bilder.py (nur 127.0.0.1) – kein Clouddienst.
Je Symbol ein echtes Objekt (Startknopf bleibt Logo C; Blender/Unreal behalten ihr Originalzeichen) aus echtem Material, frei auf dunklem Grund; Trendo legt es auf eine Glas-Kachel.
Keine Schrift, keine Logos – fremde Markenzeichen (WhatsApp, Telegram …) kommen nie aus der Bild-KI, sondern als Original-Symbol des Dienstes.
Aufruf:  python tools/dock_bilder.py [--ids files,browser] [--seed 7]   (ComfyUI muss laufen)
Ausgabe: src/Trendo.App/Assets/dock/<id>.png (256 × 256, über ffmpeg verkleinert).
"""
import argparse, os, shutil, subprocess, sys, tempfile, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import studio_bilder as sb  # gleiche ComfyUI-Anbindung und derselbe Ablauf

STYLE = ('hyper-realistic close-up product photograph of a single object, centered and filling about three quarters of the square frame, '
         'isolated on a seamless pure black background, soft studio key light from the top left, subtle warm rim light on the object edges only, '
         'realistic materials, fine surface detail and reflections, crisp focus, gentle contact shadow, no visible light source, no lens flare, no glow in the background, '
         'premium app icon look like a real physical miniature, no text, no letters, no numbers, no logos, no watermark')

PROMPTS = {
    'files': 'a classic manila paper file folder slightly open with a few white paper sheets inside',
    'browser': 'a small glossy glass globe of planet earth with blue oceans on a thin brass stand',
    'mail': 'a closed cream white paper envelope with a red wax seal',
    'ki': 'a glowing four-pointed golden star crystal floating, sparkling light inside',
    'media': 'a vintage brass film camera lens lying next to a curled strip of 35mm film, compact still life, no people',
    'settings': 'two interlocking polished steel gears, machined metal',
    'components': 'a small open cardboard box with a golden bow, gift box',
    'code': 'a small wedge of a dark mechanical keyboard with a few keys glowing warm golden from below, no symbols on the keys',
    'office': 'a small stack of white paper documents with a silver paper clip and a fountain pen',
    'security': 'a solid brushed steel padlock with a golden shackle',
    'trash': 'a small brushed stainless steel pedal waste bin with a closed rounded lid and a foot pedal',
}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--ids', default=','.join(PROMPTS))
    ap.add_argument('--seed', type=int, default=7)
    ap.add_argument('--ffmpeg', default=shutil.which('ffmpeg') or 'ffmpeg')
    a = ap.parse_args()
    out = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), 'src', 'Trendo.App', 'Assets', 'dock')
    os.makedirs(out, exist_ok=True)
    for i in [x.strip() for x in a.ids.split(',') if x.strip()]:
        prompt = f'{PROMPTS[i]}, {STYLE}'
        pid = sb.req('/prompt', {'prompt': sb.workflow(prompt, a.seed, 1024, 1024), 'client_id': 'trendo-dock'})['prompt_id']
        for _ in range(600):
            h = sb.req(f'/history/{pid}').get(pid)
            if h and h.get('outputs'): break
            time.sleep(0.5)
        else: raise TimeoutError(i)
        im = [x for o in h['outputs'].values() for x in o.get('images', [])][0]
        q = sb.urllib.request.quote
        data = sb.req(f"/view?filename={q(im['filename'])}&subfolder={q(im.get('subfolder', ''))}&type={im.get('type', 'output')}", timeout=60)
        tmp = os.path.join(tempfile.gettempdir(), f'trendo-dock-{i}.png')
        open(tmp, 'wb').write(data)
        target = os.path.join(out, i + '.png')
        subprocess.run([a.ffmpeg, '-y', '-loglevel', 'error', '-i', tmp, '-vf', 'scale=256:256', target], check=True)
        os.remove(tmp)
        print('OK', i, target)


if __name__ == '__main__':
    main()
