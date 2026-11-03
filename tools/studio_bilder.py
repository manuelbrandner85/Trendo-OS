"""Fotorealistische Studio-Bilder für Trendo OS lokal erzeugen (Wunsch 02.11.2026: alle Bilder fotorealistisch, auch für künftige Studios).

Nutzt eine lokal laufende ComfyUI (nur 127.0.0.1) mit FLUX.2 klein (flux-2-klein-4b, qwen_3_4b, flux2-vae) – kein Clouddienst.
Neues Studio: Zeile in PROMPTS ergänzen, dann  python tools/studio_bilder.py --ids NEU-01  (ComfyUI muss laufen).
Ausgabe: src/Trendo.App/Assets/studios/<ID>.jpg (768 × 432, über ffmpeg aus dem PNG umgewandelt).
"""
import argparse, json, os, shutil, subprocess, sys, tempfile, time, urllib.request, uuid

HOST = 'http://127.0.0.1:8188'
STYLE = ('photorealistic photograph, shot on a full-frame camera with a 35mm lens at f/2.8, natural soft light with warm golden accent light, '
         'dark charcoal and black surroundings with subtle real gold details, shallow depth of field, realistic materials and textures, '
         'cinematic but believable, high detail, no text, no letters, no logos, no watermark, no screens with readable text')

# Je Studio eine echte Szene, die zeigt, was man dort tut – passend zum Dunkel-Gold-Look von Trendo.
PROMPTS = {
    'STUDIO-LETTER-01': 'a fountain pen resting on a handwritten letter and an envelope with a wax seal on a dark walnut desk, brass desk lamp',
    'SYS-10': 'an open laptop next to printed documents, a spreadsheet printout and a notebook on a dark oak desk in a quiet home office at evening',
    'SYS-09': 'a smartphone showing a calendar beside a paper planner, envelopes and a cup of coffee on a dark wooden table',
    'X-11': 'an empty notebook, pencils, a camera and a small potted plant arranged on a dark desk at dawn, ready to start a new project',
    'SYS-02': 'a calculator, sticky notes, wireless headphones and a small speaker on a dark desk with warm lamp light',
    'STUDIO-ONLINE-BUSINESS-01': 'small business owner packing a parcel at a dark wooden workbench with shipping boxes, product jars and a card terminal',
    'SPEC-11': 'stacks of coins, a calculator, receipts and a leather wallet on a dark desk with a ledger book',
    'CORE-08': 'professional camera, video editing controller and color grading panel on a dark studio desk with monitors glowing softly',
    'SPEC-15': 'a streaming setup with studio microphone on a boom arm, ring light and camera in a dark room with warm accent lights',
    'STUDIO-MUSIK-01': 'a home music production studio at night: studio monitor speakers, a large-diaphragm condenser microphone with pop filter, a MIDI keyboard and an acoustic guitar on a stand, warm golden lamp light, dark acoustic panels',
    'STUDIO-TV-01': 'a cozy dark living room with a large wall-mounted television glowing, sofa and a remote control on a side table',
    'SPEC-08': 'tailor workbench with fabric swatches, measuring tape, scissors and a dress form in a dark atelier',
    'X-07': 'a photographer and sound recordist kit: camera, field recorder and headphones laid out on a dark table',
    'CORE-12': 'a person writing careful notes in a notebook next to a laptop, warm desk lamp, dark study room',
    'CORE-05': 'a 3D artist workstation with a graphics tablet and a small detailed sculpted dragon figurine on a dark desk',
    'CORE-06': 'a game developer desk with a game controller, mechanical keyboard and concept art sketches in a dark room',
    'SPEC-04': 'a virtual reality headset and controllers resting on a dark wooden table with soft golden rim light',
    'SPEC-09': 'a handheld 3D scanner scanning a ceramic vase on a turntable in a dark workshop',
    'SPEC-01': 'an architect desk with rolled floor plans, scale ruler and a white architectural model of an apartment, dark room, warm light',
    'SPEC-02': 'an electronics workbench with a breadboard, LEDs, resistors, a multimeter and a soldering iron',
    'SPEC-03': 'steel beams and a structural engineering drawing with a calculator and hard hat on a dark table',
    'SPEC-10': 'industrial sensors and a small control panel with analog gauges in a dark technical room',
    'SPEC-13': 'a hiking map, compass and GPS device on a dark wooden table next to hiking boots',
    'SPEC-14': 'a CNC milling machine cutting an aluminum part with metal chips, dark workshop, warm work light',
    'SPEC-16': 'interior of a modern car at dusk, dashboard and a plain steering wheel without any emblem or brand logo, car keys on the seat',
    'CORE-09': 'an industrial robot arm next to a camera on a tripod in a dark laboratory',
    'SPEC-05': 'laboratory measuring instruments and printed graphs on a dark lab bench',
    'SPEC-06': 'handwritten flash cards, a vocabulary book and highlighters on a dark desk',
    'SPEC-07': 'two open dictionaries in different languages and a notebook on a dark desk',
    'CORE-10': 'a library shelf with old books and a leather notebook in warm light, dark reading room',
    'SYS-08': 'a researcher reading documents with a magnifying glass at a dark archive table with folders',
    'CORE-02': 'a person asking a question to a small smart assistant device on a dark desk, warm ambient light',
    'CORE-03': 'a team of colleagues collaborating around a dark meeting table with laptops and notes, warm lighting',
    'X-05': 'a computer workstation with a powerful graphics card visible through the side glass, dark room',
    'SYS-05': 'gears of a precision mechanical clockwork in gold and steel, dark background',
    'X-01': 'a large production planning board with cards and a team in a dark studio space',
    'CORE-07': 'a web designer workspace with two monitors, keyboard and color swatches, dark room with warm light',
    'X-03': 'colorful building blocks being assembled into an app-like structure on a dark desk',
    'X-06': 'a developer laptop with code and a coffee mug on a dark desk at night',
    'X-04': 'a toolbox with precision screwdrivers and electronic modules on a dark workbench',
    'X-10': 'a disassembled mechanical device with neatly arranged parts and blueprints on a dark workbench',
    'CORE-01': 'a clean modern desk with a large monitor and keyboard in a dark room with warm accent lighting',
    'SYS-01': 'a laptop opening up on a dark desk with a warm glow',
    'CORE-11': 'a heavy steel safe with a combination lock in a dark room, warm light',
    'SPEC-12': 'a security professional examining a hard drive with a flashlight on a dark desk',
    'SYS-03': 'a laptop with a browser window and a download progress on a dark desk',
    'SYS-04': 'software installation boxes and a USB stick on a dark desk',
    'SYS-06': 'a home network router and external hard drive on a dark shelf with soft light',
    'SYS-07': 'a printer, webcam and wireless mouse arranged on a dark desk',
    'SYS-11': 'a laptop showing app tiles on a dark desk with a coffee cup',
    'SYS-12': 'reading glasses and a large keyboard on a dark desk with warm light',
    'SYS-13': 'a quality inspector checking a circuit board with a magnifier in a dark lab',
    'X-02': 'a technician repairing a computer with tools on a dark workbench',
    'X-08': 'a smartphone connected by cable to a laptop on a dark desk',
    'CORE-04': 'a Raspberry Pi board, a smartphone and a laptop connected on a dark desk',
    'X-09': 'computer RAM modules and a CPU on a dark anti-static mat',
}

def req(path, data=None, timeout=30):
    body = json.dumps(data).encode() if data is not None else None
    r = urllib.request.Request(HOST + path, data=body, headers={'Content-Type': 'application/json'} if body else {})
    with urllib.request.urlopen(r, timeout=timeout) as resp:
        raw = resp.read()
        return json.loads(raw) if resp.headers.get('Content-Type', '').startswith('application/json') else raw

def workflow(prompt, seed, w=1024, h=576):
    return {
        '1': {'class_type': 'UNETLoader', 'inputs': {'unet_name': 'flux-2-klein-4b.safetensors', 'weight_dtype': 'default'}},
        '2': {'class_type': 'CLIPLoader', 'inputs': {'clip_name': 'qwen_3_4b.safetensors', 'type': 'flux2', 'device': 'default'}},
        '3': {'class_type': 'VAELoader', 'inputs': {'vae_name': 'flux2-vae.safetensors'}},
        '4': {'class_type': 'CLIPTextEncode', 'inputs': {'text': prompt, 'clip': ['2', 0]}},
        '5': {'class_type': 'EmptyFlux2LatentImage', 'inputs': {'width': w, 'height': h, 'batch_size': 1}},
        '6': {'class_type': 'Flux2Scheduler', 'inputs': {'steps': 4, 'width': w, 'height': h}},
        '7': {'class_type': 'RandomNoise', 'inputs': {'noise_seed': seed}},
        '8': {'class_type': 'KSamplerSelect', 'inputs': {'sampler_name': 'euler'}},
        '9': {'class_type': 'CFGGuider', 'inputs': {'model': ['1', 0], 'positive': ['4', 0], 'negative': ['10', 0], 'cfg': 1.0}},
        '10': {'class_type': 'ConditioningZeroOut', 'inputs': {'conditioning': ['4', 0]}},
        '11': {'class_type': 'SamplerCustomAdvanced', 'inputs': {'noise': ['7', 0], 'guider': ['9', 0], 'sampler': ['8', 0], 'sigmas': ['6', 0], 'latent_image': ['5', 0]}},
        '12': {'class_type': 'VAEDecode', 'inputs': {'samples': ['11', 0], 'vae': ['3', 0]}},
        '30': {'class_type': 'SaveImage', 'inputs': {'images': ['12', 0], 'filename_prefix': 'trendo-studio'}},
    }

def render(sid, seed, out_dir, ffmpeg):
    pid = req('/prompt', {'prompt': workflow(f'{PROMPTS[sid]}, {STYLE}', seed), 'client_id': uuid.uuid4().hex})['prompt_id']
    for _ in range(600):
        h = req(f'/history/{pid}').get(pid)
        if h and h.get('outputs'): break
        time.sleep(0.5)
    else: raise TimeoutError(sid)
    im = [i for o in h['outputs'].values() for i in o.get('images', [])][0]
    png = req(f"/view?filename={urllib.request.quote(im['filename'])}&subfolder={urllib.request.quote(im.get('subfolder', ''))}&type={im.get('type', 'output')}", timeout=60)
    tmp = os.path.join(tempfile.gettempdir(), f'trendo-studio-{sid}.png')
    open(tmp, 'wb').write(png)
    target = os.path.join(out_dir, f'{sid}.jpg')
    subprocess.run([ffmpeg, '-y', '-loglevel', 'error', '-i', tmp, '-vf', 'scale=768:432', '-q:v', '3', target], check=True)
    os.remove(tmp)
    return target

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--ids', nargs='*', help='nur diese Studios (Standard: alle)')
    ap.add_argument('--seed', type=int, default=20261102)
    ap.add_argument('--out', default=os.path.join(os.path.dirname(__file__), '..', 'src', 'Trendo.App', 'Assets', 'studios'))
    ap.add_argument('--ffmpeg', default=shutil.which('ffmpeg') or 'ffmpeg')
    a = ap.parse_args()
    try: req('/system_stats', timeout=3)
    except OSError: sys.exit('ComfyUI läuft nicht auf 127.0.0.1:8188 – bitte zuerst starten.')
    ids = a.ids or list(PROMPTS)
    for i, sid in enumerate(ids):
        if sid not in PROMPTS: print(f'FEHLT Bildbeschreibung für {sid}'); continue
        t = time.time(); path = render(sid, a.seed + i, os.path.abspath(a.out), a.ffmpeg)
        print(f'BILD {sid} {time.time() - t:.1f}s -> {os.path.basename(path)}', flush=True)

if __name__ == '__main__':
    main()
