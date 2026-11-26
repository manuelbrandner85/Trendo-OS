"""Fotorealistische Studio-Bilder für Trendo OS lokal erzeugen (Wunsch 02.11.2026: alle Bilder fotorealistisch, auch für künftige Studios).

Einziger Weg: eine lokal laufende ComfyUI (nur 127.0.0.1) mit FLUX.2 [klein] 4B (flux-2-klein-4b, qwen_3_4b, flux2-vae) – kein Clouddienst.
Lizenz (T-0783, geprüft 26.11.2026 an der Modellkarte): FLUX.2 [klein] 4B steht unter Apache-2.0 – Ausgabebilder dürfen ohne
Registrierung, Umsatzgrenze oder Hinweispflicht verkauft werden. NICHT verwenden: SDXL Turbo / SD Turbo (Stability AI Community
License: Registrierung, Umsatzgrenze, „Powered by Stability AI“) und FLUX.2 [klein] 9B (FLUX Non-Commercial License).
Nachweis: THIRD_PARTY.md, Abschnitt „Studio-Bilder“; Herkunft je Bild in docs/design/STUDIOBILDER.json (schreibt dieses Skript).
Neues Studio: Zeile in PROMPTS ergänzen, dann  python tools/studio_bilder.py --ids NEU-01  (ComfyUI muss laufen).
Ausgabe: src/Trendo.App/Assets/studios/<ID>.jpg (768 × 432, über ffmpeg aus dem PNG umgewandelt, ohne Metadaten).
"""
import argparse, datetime, hashlib, json, os, shutil, subprocess, sys, tempfile, time, urllib.parse, urllib.request, uuid

HOST = 'http://127.0.0.1:8188'
# Modellordner der ComfyUI (Prüfsummen-Abgleich ist Pflicht). Überschreibbar mit --modelle oder TRENDO_COMFY_MODELLE.
MODELLORDNER = os.environ.get('TRENDO_COMFY_MODELLE') or os.path.join(
    os.path.expanduser('~'), 'Desktop', 'Studio-Werkzeuge', 'GAMEFORGE_GODMODE_v2_0', 'engines', 'ComfyUI_windows_portable', 'ComfyUI', 'models')
# Erlaubtes Modell (Apache-2.0). SHA-256 wie auf Hugging Face (black-forest-labs/FLUX.2-klein-4B, Comfy-Org/flux2-klein-4B).
MODELL = {
    'unet': 'flux-2-klein-4b.safetensors',
    'clip': 'qwen_3_4b.safetensors',
    'vae': 'flux2-vae.safetensors',
    'sha256': {
        'flux-2-klein-4b.safetensors': 'ec3d4e733a771f61c052fb4856c48b336c55eaf2c65487c2a1faeb9bbda7a343',
        'qwen_3_4b.safetensors': '6c671498573ac2f7a5501502ccce8d2b08ea6ca2f661c458e708f36b36edfc5a',
        'flux2-vae.safetensors': '868fe7b343cc8f3a19dbcfcafbc3d5f888802be3f89bd81b65b3621a066ce8f3',
    },
    'name': 'FLUX.2 [klein] 4B (flux-2-klein-4b, qwen_3_4b, flux2-vae), 4 Schritte, euler, cfg 1.0, ComfyUI lokal',
    'lizenz': 'Apache-2.0 (https://huggingface.co/black-forest-labs/FLUX.2-klein-4B), Ausgabe kommerziell frei, keine Hinweispflicht',
}
VERBOTEN = ('sdxl', 'sd_xl', 'sd-turbo', 'sd_turbo', 'klein-9b', 'klein_9b')
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
    'STUDIO-FILM-01': 'a film director chair and a wooden clapperboard next to a cinema camera on a tripod on a dark film set, warm golden spotlight, soft haze',
    'STUDIO-POD-01': 'a screen printing workbench in a dark workshop: a freshly printed plain t-shirt, a ceramic mug and rolled posters with abstract colorful artwork, a squeegee and ink tins, warm lamp light',
    'STUDIO-CONSOLE-01': 'a retro video game console with a disc tray open and a modern handheld game device and a laptop side by side on a dark workbench, cables, warm lamp light, no brand names',
    'STUDIO-BOOK-01': 'an open handwritten manuscript notebook with a fountain pen, a stack of hardcover books and a cup of tea on a dark wooden writing desk, warm lamp light, no readable text',
    'STUDIO-REFERAT-01': 'a student desk at evening with a printed thesis manuscript held by a binder clip, an open notebook with a hand-drawn outline, library books with colored sticky tabs, a fountain pen and a laptop with its lid half closed, warm golden lamp light, no readable text',
    'STUDIO-TIMELINE-01': 'a long paper timeline scroll unrolled across a dark wooden table with small pinned photographs, an antique coin, a pocket watch and a magnifying glass, colored thread connecting points along the scroll, warm golden lamp light, no readable text',
    'STUDIO-ART-01': 'an artist desk at night with a graphics pen tablet and stylus, an open sketchbook with a pencil portrait sketch, watercolor paint pans, brushes in a glass jar and a wooden artist mannequin, warm golden lamp light, no readable text',
    'STUDIO-ANIM-01': 'a graphics tablet with a stylus next to a small flipbook showing a bouncing ball in motion blur on a dark desk, soft colorful rim light, no text',
    'STUDIO-PHOTOS-01': 'neatly arranged printed landscape photographs of mountains, lakes and forests in plain wooden frames and wooden boxes next to a vintage camera without any nameplate on a dark table, warm light, only landscapes, no people, no faces, no readable text',
    'STUDIO-IMAGEAI-01': 'an artist easel with a canvas where a glowing landscape painting seems to emerge from soft light particles, dark studio, golden accent light, no text',
    'STUDIO-VIDEOAI-01': 'a small film camera on a tripod in front of a large monitor showing a cinematic landscape frame, film strip and storyboard cards on a dark desk, warm light, no readable text, no faces',
    'STUDIO-AGENTS-01': 'a tidy workbench with several small brass robot figurines connected by thin glowing threads to a notebook with a flow diagram, dark room, golden accent light, no readable text',
    'STUDIO-MODELS-01': 'three identical vintage scales side by side on a dark wooden table, each weighing a different glass sphere with soft inner light, warm light, no text',
    'STUDIO-TRAIN-01': 'a teacher desk with stacks of labeled index cards sorted into small wooden trays and a laptop showing a simple chart, dark study room, warm desk lamp, no readable text',
    'STUDIO-CODEAI-01': 'a mechanical keyboard and a wide frameless monitor with plain bezels showing blurred warm amber and gold source code lines on a pure black background, full screen editor without any taskbar or status bar, in a dark room, a coffee cup and a small brass lamp, warm accent light, no blue, no badge, no readable text',
    'STUDIO-VISION-01': 'a magnifying glass and a camera lens lying on a printed photograph of a forest on a dark desk, light reflections in the glass, warm light, no readable text, no faces',
    'STUDIO-CITE-01': 'open old books with colorful paper bookmarks and a card catalog drawer in a library reading room, dark wood, warm reading lamp, no readable text',
    'STUDIO-DECKDESIGN-01': 'a laptop on a dark desk showing an elegant presentation slide with a large golden number and a simple timeline, a slide clicker and printed slide thumbnails beside it, warm golden accent light, no readable text',
    'STUDIO-DOCSCAN-01': 'a smartphone held above a sheet of paper on a dark wooden desk, the phone screen shows the page outlined with corner markers, a stapled letter and a receipt nearby, soft cool daylight, no readable text',
    'STUDIO-DAYPLAN-01': 'a paper day planner with time blocks colored by soft markers next to a brass alarm clock and a cup of tea on a dark desk at morning light, no readable text',
    'STUDIO-CV-01': 'a printed resume page with a small portrait photo placeholder and neat sections on a dark wooden desk next to a fountain pen, a leather folder and a cup of coffee, warm lamp light, no readable text, no faces',
    'STUDIO-CARDS-01': 'handmade folded greeting cards and an elegant invitation card with gold foil ornaments and a wax sealed envelope on a dark table with dried flowers and ribbon, warm golden light, no readable text',
    'STUDIO-CONTRACTS-01': 'a tidy ring binder with colored tabs and contract papers, a calendar with a circled date and a reading glasses on a dark desk, warm lamp light, no readable text, no logos',
    'STUDIO-PDFTOOLS-01': 'a tidy office desk with a neat stack of printed pages held by a black binder clip, a paper cutter, a rubber stamp and an ink pad, a few pages fanned out with a black redaction bar on one line, cool daylight from a window, no readable text',
    'STUDIO-FORMS-01': 'a printed application form on a clipboard with check boxes partly ticked, a fountain pen resting on the signature line, reading glasses and a cup of coffee on a light wooden desk, soft morning light, no readable text',
    'STUDIO-SUMMARY-01': 'a thick stack of printed documents next to a single neat index card with a few highlighted lines and a yellow highlighter on a dark wooden desk, warm desk lamp light',
    'STUDIO-LOGO-01': 'a designer desk with color swatches, a sketchbook with simple geometric emblem sketches, pencils and a pantone-like fan deck, soft daylight, no text, no brand names',
    'STUDIO-PODCAST-01': 'a broadcast microphone on a boom arm with a pop filter, studio headphones and a small audio mixer on a dark desk with acoustic foam panels behind, warm light',
    'STUDIO-COMIC-01': 'an artist drawing table with a blank comic page layout of empty panels in pencil, ink pens, brushes and a desk lamp, dark studio, no text',
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
    # T-0782 (Wissen & Lernen)
    'STUDIO-TUTOR-01': 'a school exercise book with handwritten fractions and arithmetic, a pencil, an eraser and a small desk lamp on a dark wooden desk, warm evening light, no readable text, no faces',
    'STUDIO-CODELEARN-01': 'a laptop with abstract colorful program code lines out of focus on a dark desk, a paper notebook with hand drawn flowchart boxes and a pencil beside it, warm light, no readable text, no faces',
}

def req(path, data=None, timeout=30):
    body = json.dumps(data).encode() if data is not None else None
    r = urllib.request.Request(HOST + path, data=body, headers={'Content-Type': 'application/json'} if body else {})
    with urllib.request.urlopen(r, timeout=timeout) as resp:
        raw = resp.read()
        return json.loads(raw) if resp.headers.get('Content-Type', '').startswith('application/json') else raw

def workflow(prompt, seed, w=1024, h=576):
    return {
        '1': {'class_type': 'UNETLoader', 'inputs': {'unet_name': MODELL['unet'], 'weight_dtype': 'default'}},
        '2': {'class_type': 'CLIPLoader', 'inputs': {'clip_name': MODELL['clip'], 'type': 'flux2', 'device': 'default'}},
        '3': {'class_type': 'VAELoader', 'inputs': {'vae_name': MODELL['vae']}},
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

def modell_pruefen(models_dir):
    """Bricht ab, wenn ComfyUI das erlaubte Modell nicht hat oder eine Modelldatei nicht den SHA-256 der Apache-2.0-Datei von Hugging Face trägt (Pflicht)."""
    if not models_dir or not os.path.isdir(models_dir): sys.exit(f'Modellordner fehlt ({models_dir}) – mit --modelle angeben; ohne Prüfsummen-Abgleich wird nichts erzeugt.')
    for ordner, name in (('diffusion_models', MODELL['unet']), ('text_encoders', MODELL['clip']), ('vae', MODELL['vae'])):
        vorhanden = req(f'/models/{ordner}')
        if name not in vorhanden: sys.exit(f'ComfyUI hat {name} nicht im Ordner {ordner} – nur FLUX.2 [klein] 4B (Apache-2.0) ist erlaubt.')
        datei = os.path.join(models_dir, ordner, name)
        if not os.path.isfile(datei): sys.exit(f'{datei} fehlt – Prüfsumme nicht prüfbar.')
        h = hashlib.sha256()
        with open(datei, 'rb') as f:
            for block in iter(lambda: f.read(1 << 24), b''): h.update(block)
        if h.hexdigest() != MODELL['sha256'][name]: sys.exit(f'{name}: Prüfsumme weicht ab – nicht die Apache-2.0-Datei von Hugging Face.')
        print(f'MODELL {name} sha256 ok', flush=True)

def render(sid, seed, out_dir, ffmpeg):
    wf = workflow(f'{PROMPTS[sid]}, {STYLE}', seed)
    if any(v in json.dumps(wf).lower() for v in VERBOTEN): sys.exit('Ablauf nutzt ein nicht erlaubtes Modell.')
    pid = req('/prompt', {'prompt': wf, 'client_id': uuid.uuid4().hex})['prompt_id']
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
    # -map_metadata -1 und bitexact: keine Metadaten, kein Encoder-Kommentar, keine Pfade im Bild.
    subprocess.run([ffmpeg, '-y', '-loglevel', 'error', '-i', tmp, '-vf', 'scale=768:432', '-q:v', '3',
                    '-map_metadata', '-1', '-fflags', '+bitexact', '-flags:v', '+bitexact', target], check=True)
    os.remove(tmp)
    return target

def herkunft_schreiben(path, sid, seed, jpg):
    """Herkunft je Bild in docs/design/STUDIOBILDER.json (Prompt, Startwert, Modell, Lizenz, Datum, SHA-256)."""
    reg = json.load(open(path, encoding='utf-8')) if os.path.exists(path) else {}
    reg[sid] = {
        'datei': f'src/Trendo.App/Assets/studios/{sid}.jpg',
        'seed': seed,
        'prompt': f'{PROMPTS[sid]}, {STYLE}',
        'modell': MODELL['name'],
        'erzeugt': datetime.date.today().isoformat(),
        'lizenz': 'eigene KI-Erzeugung für Trendo OS, keine Fremdfotos; Modell ' + MODELL['lizenz'],
        'sha256': hashlib.sha256(open(jpg, 'rb').read()).hexdigest(),
    }
    with open(path, 'w', encoding='utf-8', newline='\n') as f:
        json.dump(reg, f, ensure_ascii=False, indent=2); f.write('\n')

def host_ok(url):
    """Nur http://127.0.0.1:<port> – geprüft mit urllib.parse (kein userinfo wie 127.0.0.1@fremd, kein anderer Rechner)."""
    if url != url.strip(): return False
    try:
        u = urllib.parse.urlsplit(url)
        port = u.port
    except ValueError:
        return False
    return (u.scheme == 'http' and u.hostname == '127.0.0.1' and u.username is None and u.password is None
            and '@' not in u.netloc and port is not None and u.path in ('', '/') and not u.query and not u.fragment)

def main():
    global HOST
    root = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..')
    ap = argparse.ArgumentParser()
    ap.add_argument('--ids', nargs='*', help='nur diese Studios (Standard: alle)')
    ap.add_argument('--seed', type=int, default=20261102)
    ap.add_argument('--host', default=HOST, help='ComfyUI, nur 127.0.0.1 (Standard %(default)s)')
    ap.add_argument('--modelle', default=MODELLORDNER, help='ComfyUI-Modellordner für den Pflicht-Abgleich der Prüfsummen (Standard: %(default)s)')
    ap.add_argument('--out', default=os.path.join(root, 'src', 'Trendo.App', 'Assets', 'studios'))
    ap.add_argument('--herkunft', default=os.path.join(root, 'docs', 'design', 'STUDIOBILDER.json'))
    ap.add_argument('--ffmpeg', default=shutil.which('ffmpeg') or 'ffmpeg')
    a = ap.parse_args()
    if not host_ok(a.host): sys.exit('Nur eine ComfyUI auf diesem PC ist erlaubt: http://127.0.0.1:<port>, ohne Benutzerangaben, Pfad oder Abfrage.')
    HOST = a.host
    try: req('/system_stats', timeout=3)
    except OSError: sys.exit(f'ComfyUI läuft nicht auf {HOST} – bitte zuerst starten.')
    modell_pruefen(a.modelle)
    ids = a.ids or list(PROMPTS)
    for i, sid in enumerate(ids):
        if sid not in PROMPTS: print(f'FEHLT Bildbeschreibung für {sid}'); continue
        t = time.time(); path = render(sid, a.seed + i, os.path.abspath(a.out), a.ffmpeg)
        herkunft_schreiben(os.path.abspath(a.herkunft), sid, a.seed + i, path)
        print(f'BILD {sid} {time.time() - t:.1f}s -> {os.path.basename(path)}', flush=True)

if __name__ == '__main__':
    main()
