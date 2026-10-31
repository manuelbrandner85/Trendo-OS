# Trendo OS – Foto-Ansicht des Grundrisses (T-0280). Aufruf: blender -b --factory-startup --python trendo_foto.py -- szene.json
# Baut die Wohnung aus der Szene von PlanPhoto.Scene (Meter, Z oben) mit echten Materialien, Sockelleisten, Fenstern mit Glas,
# Möbeln aus trendo_moebel.py in Render-Qualität (oder Modellen aus der 3D-Bibliothek), physikalischem Himmel und Sonne,
# misst die Belichtung an einer kleinen Proberechnung und rendert mit Cycles (GPU, wenn vorhanden). Eigene Arbeit, keine fremden Assets.
import bpy, bmesh, sys, os, json, math, random
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import trendo_moebel as tm

def fail(msg):
    print('TRENDO_FEHLER', msg); sys.stdout.flush(); sys.exit(2)

def step(v):
    print('TRENDO_FORTSCHRITT', round(v, 3)); sys.stdout.flush()

try:
    S = json.load(open(sys.argv[sys.argv.index('--') + 1], encoding='utf-8'))
except Exception as e:
    fail(f'Szene nicht lesbar: {e}')

bpy.ops.wm.read_homefile(use_empty=True)
sc = bpy.context.scene
sc.unit_settings.system = 'METRIC'; sc.unit_settings.scale_length = 1.0
sc.render.engine = 'CYCLES'
col = sc.collection
MODE = S.get('mode', 'vogel')

# ---------------------------------------------------------------- Gerät: GPU (OptiX → CUDA → HIP → Metal → oneAPI), sonst CPU
def device():
    pr = bpy.context.preferences.addons['cycles'].preferences
    for t in ('OPTIX', 'CUDA', 'HIP', 'METAL', 'ONEAPI'):
        try:
            pr.compute_device_type = t; pr.get_devices()
            gpus = [d for d in pr.devices if d.type == t]
            if gpus:
                for d in pr.devices: d.use = d.type == t
                sc.cycles.device = 'GPU'; return f'{t}: {gpus[0].name}'
        except Exception:
            pass
    sc.cycles.device = 'CPU'; return 'CPU'
DEV = device()

# ---------------------------------------------------------------- Werkstoffe
mats = tm.M()

def node_mat(name):
    m = bpy.data.materials.new(name); m.use_nodes = True
    return m, m.node_tree, tm._bsdf(m)

def plaster(name='Putz weiß', col=(0.80, 0.79, 0.76)):
    """Wandfarbe auf Putz: matt, Helligkeit ±3 % fleckig (Rollspuren), feine Körnung als Bump."""
    m, nt, b = node_mat(name)
    tc = nt.nodes.new('ShaderNodeTexCoord'); nz = nt.nodes.new('ShaderNodeTexNoise'); nz.inputs['Scale'].default_value = 3; nz.inputs['Detail'].default_value = 6
    mr = nt.nodes.new('ShaderNodeMapRange'); mr.inputs['To Min'].default_value = 0.97; mr.inputs['To Max'].default_value = 1.03
    hsv = nt.nodes.new('ShaderNodeHueSaturation'); hsv.inputs['Color'].default_value = (*col, 1)
    nt.links.new(tc.outputs['Object'], nz.inputs['Vector']); nt.links.new(nz.outputs['Fac'], mr.inputs['Value']); nt.links.new(mr.outputs['Result'], hsv.inputs['Value'])
    nt.links.new(hsv.outputs['Color'], b.inputs['Base Color'])
    tm._set(b, **{'Roughness': 0.92, 'Specular IOR Level': 0.3})
    tm._bump(m, scale=400, strength=0.04, detail=4)
    return m

def planks(name, c1, c2, rough, plank=(1.2, 0.19), coat=0.0):
    """Dielen/Laminat: Brettverband (jedes Brett eigener Farbton), Maserung längs, Fugen als feine Vertiefung, Rauheit leicht variiert."""
    m, nt, b = node_mat(name)
    tc = nt.nodes.new('ShaderNodeTexCoord')
    br = nt.nodes.new('ShaderNodeTexBrick'); br.offset = 0.37; br.squash = 1.0
    br.inputs['Scale'].default_value = 1.0; br.inputs['Brick Width'].default_value = plank[0]; br.inputs['Row Height'].default_value = plank[1]
    br.inputs['Mortar Size'].default_value = 0.0015; br.inputs['Mortar Smooth'].default_value = 0.3; br.inputs['Bias'].default_value = 0.0
    br.inputs['Color1'].default_value = (*c1, 1); br.inputs['Color2'].default_value = (*c2, 1); br.inputs['Mortar'].default_value = (0.05, 0.035, 0.02, 1)
    mp = nt.nodes.new('ShaderNodeMapping'); mp.inputs['Scale'].default_value = (0.6, 14.0, 1.0)
    wv = nt.nodes.new('ShaderNodeTexWave'); wv.wave_type = 'BANDS'; wv.inputs['Scale'].default_value = 2.0; wv.inputs['Distortion'].default_value = 8.0; wv.inputs['Detail'].default_value = 4.0
    mix = nt.nodes.new('ShaderNodeMix'); mix.data_type = 'RGBA'; mix.blend_type = 'MULTIPLY'; mix.inputs['Factor'].default_value = 0.35
    nt.links.new(tc.outputs['Object'], br.inputs['Vector']); nt.links.new(tc.outputs['Object'], mp.inputs['Vector']); nt.links.new(mp.outputs['Vector'], wv.inputs['Vector'])
    nt.links.new(br.outputs['Color'], mix.inputs['A'])
    ramp = nt.nodes.new('ShaderNodeValToRGB'); ramp.color_ramp.elements[0].color = (0.75, 0.75, 0.75, 1); ramp.color_ramp.elements[1].color = (1, 1, 1, 1)
    nt.links.new(wv.outputs['Fac'], ramp.inputs['Fac']); nt.links.new(ramp.outputs['Color'], mix.inputs['B'])
    nt.links.new(mix.outputs['Result'], b.inputs['Base Color'])
    rr = nt.nodes.new('ShaderNodeMapRange'); rr.inputs['To Min'].default_value = rough - 0.06; rr.inputs['To Max'].default_value = rough + 0.06
    nt.links.new(wv.outputs['Fac'], rr.inputs['Value']); nt.links.new(rr.outputs['Result'], b.inputs['Roughness'])
    inv = nt.nodes.new('ShaderNodeMath'); inv.operation = 'SUBTRACT'; inv.inputs[0].default_value = 1.0
    nt.links.new(br.outputs['Fac'], inv.inputs[1])
    bu = nt.nodes.new('ShaderNodeBump'); bu.inputs['Strength'].default_value = 0.25; bu.inputs['Distance'].default_value = 0.001
    nt.links.new(inv.outputs['Value'], bu.inputs['Height']); nt.links.new(bu.outputs['Normal'], b.inputs['Normal'])
    tm._set(b, **{'Coat Weight': coat, 'Coat Roughness': 0.2})
    return m

def tiles(name, col, grout=(0.42, 0.41, 0.39), size=0.6, rough=(0.18, 0.85), vary=1.04):
    """Feinsteinzeug: quadratische Platten, glasiert (geringe Rauheit), Fugen matt und vertieft, Platten minimal unterschiedlich hell."""
    m, nt, b = node_mat(name)
    tc = nt.nodes.new('ShaderNodeTexCoord')
    br = nt.nodes.new('ShaderNodeTexBrick'); br.offset = 0.0; br.offset_frequency = 1
    br.inputs['Scale'].default_value = 1.0; br.inputs['Brick Width'].default_value = size; br.inputs['Row Height'].default_value = size
    br.inputs['Mortar Size'].default_value = 0.003; br.inputs['Bias'].default_value = 0.0
    c2 = tuple(min(1, x * vary) for x in col)
    br.inputs['Color1'].default_value = (*col, 1); br.inputs['Color2'].default_value = (*c2, 1); br.inputs['Mortar'].default_value = (*grout, 1)
    nt.links.new(tc.outputs['Object'], br.inputs['Vector']); nt.links.new(br.outputs['Color'], b.inputs['Base Color'])
    rr = nt.nodes.new('ShaderNodeMapRange'); rr.inputs['From Min'].default_value = 0; rr.inputs['From Max'].default_value = 1; rr.inputs['To Min'].default_value = rough[0]; rr.inputs['To Max'].default_value = rough[1]
    nt.links.new(br.outputs['Fac'], rr.inputs['Value']); nt.links.new(rr.outputs['Result'], b.inputs['Roughness'])
    inv = nt.nodes.new('ShaderNodeMath'); inv.operation = 'SUBTRACT'; inv.inputs[0].default_value = 1.0; nt.links.new(br.outputs['Fac'], inv.inputs[1])
    bu = nt.nodes.new('ShaderNodeBump'); bu.inputs['Strength'].default_value = 0.4; bu.inputs['Distance'].default_value = 0.002
    nt.links.new(inv.outputs['Value'], bu.inputs['Height']); nt.links.new(bu.outputs['Normal'], b.inputs['Normal'])
    return m

M_WALL = plaster()
M_CEIL = plaster('Decke weiß', (0.83, 0.83, 0.81))
M_CUT = tm.solid('Schnittfläche', (0.045, 0.047, 0.05), 0.8)
M_SKIRT = tm.solid('Sockelleiste Lack', (0.78, 0.78, 0.76), 0.35, coat=0.1)
M_FRAME = tm.solid('Fensterrahmen Kunststoff', (0.80, 0.80, 0.79), 0.3)
def window_glass():
    """Floatglas, leicht grünlich: sichtbar spiegelnd/brechend, für Schatten- und diffuse Strahlen durchlässig –
    sonst sperrt echtes Glas Sonne und Himmel aus (ohne Kaustiken), und der Raum bleibt ~13 Blenden zu dunkel."""
    m, nt, b = node_mat('Fensterglas')
    tm._set(b, **{'Base Color': (0.93, 0.97, 0.95, 1), 'Roughness': 0.0, 'Transmission Weight': 1.0, 'IOR': 1.52})
    out = next(n for n in nt.nodes if n.type == 'OUTPUT_MATERIAL')
    tr = nt.nodes.new('ShaderNodeBsdfTransparent'); tr.inputs['Color'].default_value = (0.93, 0.97, 0.95, 1)
    lp = nt.nodes.new('ShaderNodeLightPath'); mx = nt.nodes.new('ShaderNodeMath'); mx.operation = 'MAXIMUM'
    nt.links.new(lp.outputs['Is Shadow Ray'], mx.inputs[0]); nt.links.new(lp.outputs['Is Diffuse Ray'], mx.inputs[1])
    mix = nt.nodes.new('ShaderNodeMixShader')
    nt.links.new(mx.outputs['Value'], mix.inputs['Fac']); nt.links.new(b.outputs['BSDF'], mix.inputs[1]); nt.links.new(tr.outputs['BSDF'], mix.inputs[2])
    nt.links.new(mix.outputs['Shader'], out.inputs['Surface'])
    return m
M_GLASS = window_glass()
M_SILL = tm.solid('Fensterbank Stein', (0.55, 0.54, 0.52), 0.25, bump=0.02)
M_GROUND = tiles('Betonpflaster', (0.24, 0.235, 0.22), (0.12, 0.115, 0.10), 0.4, (0.8, 0.95), 1.12)
M_PLINTH = tm.solid('Sockel Naturstein', (0.36, 0.35, 0.33), 0.6, bump=0.08)
FLOORS = {
    'parkett': planks('Parkett Eiche', (0.33, 0.20, 0.09), (0.42, 0.27, 0.13), 0.38, (1.8, 0.19), 0.08),
    'laminat': planks('Laminat Eiche hell', (0.47, 0.35, 0.21), (0.55, 0.42, 0.27), 0.42, (1.29, 0.19)),
    'vinyl': planks('Vinyl Grau', (0.30, 0.29, 0.27), (0.36, 0.35, 0.33), 0.5, (1.22, 0.18)),
    'fliesen': tiles('Fliesen Feinsteinzeug', (0.62, 0.61, 0.58)),
    'teppich': tm.fabric('Teppichboden', (0.22, 0.21, 0.20), 0.95, 0.5, 600),
}

# ---------------------------------------------------------------- Geometrie-Helfer
def mesh_obj(name, verts, faces, mat, smooth=False):
    me = bpy.data.meshes.new(name); me.from_pydata(verts, [], faces)
    b = bmesh.new(); b.from_mesh(me); bmesh.ops.recalc_face_normals(b, faces=b.faces); b.to_mesh(me); b.free(); me.update()
    if mat: me.materials.append(mat)
    if smooth:
        for p in me.polygons: p.use_smooth = True
    o = bpy.data.objects.new(name, me); col.objects.link(o); return o

def box(name, cx, cy, z0, w, d, h, ang, mat):
    """Quader w × d × h, Mitte (cx, cy), Unterkante z0, um Z gedreht."""
    c, s = math.cos(ang), math.sin(ang); vs = []
    for dz in (0, h):
        for dx, dy in ((-w / 2, -d / 2), (w / 2, -d / 2), (w / 2, d / 2), (-w / 2, d / 2)):
            vs.append((cx + dx * c - dy * s, cy + dx * s + dy * c, z0 + dz))
    fs = [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]
    return mesh_obj(name, vs, fs, mat)

def signed_area(pts):
    return sum(pts[i][0] * pts[(i + 1) % len(pts)][1] - pts[(i + 1) % len(pts)][0] * pts[i][1] for i in range(len(pts))) / 2

# ---------------------------------------------------------------- Wände (aus Plan3D, mit Aussparungen)
step(0.02)
bm = bmesh.new()
for solid in S['walls']:  # jeder Wandkörper für sich geschlossen – verschweißt man alles, entstehen Ritzen, durch die Licht fällt
    own = []
    for poly in solid:
        vs = [bm.verts.new(v) for v in poly]; own += vs
        try: bm.faces.new(vs)
        except ValueError: pass
    bmesh.ops.remove_doubles(bm, verts=own, dist=0.0005)
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
wall_me = bpy.data.meshes.new('Wände'); bm.to_mesh(wall_me); bm.free()
wall_me.materials.append(M_WALL); wall_me.materials.append(M_CUT)
top = max((v.co.z for v in wall_me.vertices), default=2.5)
if MODE == 'vogel':  # Schnittflächen oben dunkel – wie in Architektur-Ansichten
    for p in wall_me.polygons:
        if p.normal.z > 0.9 and p.center.z > top - 0.01: p.material_index = 1
wall_ob = bpy.data.objects.new('Wände', wall_me); col.objects.link(wall_ob)
wb = wall_ob.modifiers.new('Kante', 'BEVEL'); wb.width = 0.004; wb.segments = 2; wb.limit_method = 'ANGLE'; wb.harden_normals = False

# ---------------------------------------------------------------- Räume: Boden, Sockelleiste, Decke, Leuchte
doors = [o for o in S['openings'] if o['kind'] == 'tuer']
xs = [v[0] for sol in S['walls'] for p in sol for v in p] or [0, 1]; ys = [v[1] for sol in S['walls'] for p in sol for v in p] or [0, 1]
bx0, bx1, by0, by1 = min(xs), max(xs), min(ys), max(ys); bcx, bcy = (bx0 + bx1) / 2, (by0 + by1) / 2

for r in S['rooms']:
    pts = [tuple(p) for p in r['points']]
    if signed_area(pts) < 0: pts = pts[::-1]
    z = r['z']; n = len(pts)
    fm = FLOORS.get(r['floor'], FLOORS['laminat'])
    mesh_obj(f"Boden {r['name']}", [(x, y, z) for x, y in pts] + [(x, y, z - 0.03) for x, y in pts],
             [tuple(range(n)), tuple(range(2 * n - 1, n - 1, -1))] + [(i, n + i, n + (i + 1) % n, (i + 1) % n)[::-1] for i in range(n)], fm)
    if r['floor'] != 'fliesen':  # Sockelleisten 6 cm, an Türen unterbrochen
        for i in range(n):
            (ax, ay), (bx, by) = pts[i], pts[(i + 1) % n]; L = math.hypot(bx - ax, by - ay)
            if L < 0.05: continue
            ux, uy = (bx - ax) / L, (by - ay) / L; nx, ny = -uy, ux  # links = innen (gegen den Uhrzeigersinn)
            cuts = []
            for dd in doors:
                t = (dd['x'] - ax) * ux + (dd['y'] - ay) * uy; off = abs((dd['x'] - ax) * nx + (dd['y'] - ay) * ny)
                if -0.5 < t < L + 0.5 and off < 0.3: cuts.append((t - dd['width'] / 2, t + dd['width'] / 2))
            segs, pos = [], 0.0
            for a, b in sorted(cuts):
                if a > pos: segs.append((pos, min(a, L)))
                pos = max(pos, b)
            if pos < L: segs.append((pos, L))
            for a, b in segs:
                if b - a < 0.03: continue
                m0 = (a + b) / 2
                box('Sockelleiste', ax + ux * m0 + nx * 0.006, ay + uy * m0 + ny * 0.006, z, b - a, 0.012, 0.06, math.atan2(uy, ux), M_SKIRT)
    if MODE == 'raum':
        h = r['height']
        mesh_obj(f"Decke {r['name']}", [(x, y, z + h) for x, y in pts] + [(x, y, z + h + 0.2) for x, y in pts],
                 [tuple(range(n))[::-1], tuple(range(n, 2 * n))] + [(i, (i + 1) % n, n + (i + 1) % n, n + i)[::-1] for i in range(n)], M_CEIL)
        cx = sum(p[0] for p in pts) / n; cy = sum(p[1] for p in pts) / n  # Leuchte (aus – Tageslichtfoto): Baldachin und Schirm an der Decke
        tm.cyl('Deckenleuchte', cx, cy, z + h - 0.09, 0.16, 0.09, M_SKIRT, 48)

# Durchgehende Bodenplatte (füllt Türdurchgänge, kein Blick ins Freie unter den Wänden) und – in der Raumansicht – Rohdecke über allem
z_floor = min((r['z'] for r in S['rooms']), default=0.0)
box('Bodenplatte', bcx, bcy, z_floor - 0.25, bx1 - bx0, by1 - by0, 0.249, 0, M_PLINTH)  # außen sichtbar als Sockel, in Türdurchgängen als Schwelle
if MODE == 'raum':
    box('Rohdecke', bcx, bcy, top, bx1 - bx0 + 0.02, by1 - by0 + 0.02, 0.22, 0, M_CEIL)

# ---------------------------------------------------------------- Fenster (Rahmen, Glas, Fensterbank) und Türzargen
step(0.08)
for o in S['openings']:
    a = o['angle']; c, s = math.cos(a), math.sin(a); w, h, t = o['width'], o['height'], o['thickness']; x, y, z = o['x'], o['y'], o['z']
    if o['kind'] == 'fenster':
        fw = 0.065
        box('Fensterrahmen', x, y, z, w, 0.07, fw, a, M_FRAME); box('Fensterrahmen', x, y, z + h - fw, w, 0.07, fw, a, M_FRAME)
        for k in (-1, 1): box('Fensterrahmen', x + c * k * (w / 2 - fw / 2), y + s * k * (w / 2 - fw / 2), z, fw, 0.07, h, a, M_FRAME)
        if w > 1.2: box('Fensterpfosten', x, y, z, fw, 0.07, h, a, M_FRAME)
        box('Glas', x, y, z + fw, w - 2 * fw, 0.008, h - 2 * fw, a, M_GLASS)
        nx, ny = -s, c
        for k in (-1, 1):  # Fensterbank innen und außen
            box('Fensterbank', x + nx * k * (t / 2 + 0.04), y + ny * k * (t / 2 + 0.04), z - 0.02, w + 0.06, 0.2, 0.02, a, M_SILL)
    else:
        for k in (-1, 1): box('Zarge', x + c * k * (w / 2 + 0.01), y + s * k * (w / 2 + 0.01), z, 0.02, t + 0.02, h, a, M_SKIRT)
        box('Zarge', x, y, z + h, w + 0.04, t + 0.02, 0.02, a, M_SKIRT)

# ---------------------------------------------------------------- Möbel
step(0.12)
tm.QUALITY = 'render'
for i, it in enumerate(S['items']):
    try:
        if 'file' in it:
            before = set(bpy.data.objects)
            bpy.ops.import_scene.gltf(filepath=it['file'])
            new = [o for o in bpy.data.objects if o not in before]
            meshes = [o for o in new if o.type == 'MESH']
            if not meshes: continue
            bpy.context.view_layer.update()
            pts = [o.matrix_world @ Vector(cn) for o in meshes for cn in o.bound_box]
            x0, x1 = min(p.x for p in pts), max(p.x for p in pts); y0, y1 = min(p.y for p in pts), max(p.y for p in pts); z0 = min(p.z for p in pts)
            root = bpy.data.objects.new(f"Modell {it['name']}", None); col.objects.link(root)
            root.location = ((x0 + x1) / 2, (y0 + y1) / 2, z0)
            for o in new:
                if o.parent is None: mw = o.matrix_world.copy(); o.parent = root; o.matrix_parent_inverse = root.matrix_world.inverted(); o.matrix_world = mw
            sx, sy = it['w'] / max(1e-6, x1 - x0), it['d'] / max(1e-6, y1 - y0)
            root.scale = (sx, sy, (sx + sy) / 2)
        else:
            root = tm.build(it['type'], it['w'], it['d'], it['h'], mats)
        root.location = (it['x'], it['y'], it['z'])
        root.rotation_euler = (0, 0, math.radians(it['rot']))
        if it.get('mirror'): root.scale = (-root.scale[0], root.scale[1], root.scale[2])
    except Exception as e:
        print('TRENDO_HINWEIS Möbel übersprungen:', it.get('name'), e)
    step(0.12 + 0.2 * (i + 1) / max(1, len(S['items'])))

# ---------------------------------------------------------------- Umgebung: Gelände (Vogel), Himmel und Sonne
if True:  # Gelände – auch durchs Fenster sichtbar
    gz = min((r['z'] for r in S['rooms']), default=0) - 0.12; e = 60
    mesh_obj('Gelände', [(bcx - e, bcy - e, gz), (bcx + e, bcy - e, gz), (bcx + e, bcy + e, gz), (bcx - e, bcy + e, gz)], [(0, 1, 2, 3)], M_GROUND)

# Sonne so, dass sie durch die meisten Fenster scheint (Fensterseite nach außen gemittelt)
fx = fy = 0.0
for o in S['openings']:
    if o['kind'] != 'fenster': continue
    nx, ny = -math.sin(o['angle']), math.cos(o['angle'])
    if (o['x'] - bcx) * nx + (o['y'] - bcy) * ny < 0: nx, ny = -nx, -ny
    fx += nx * o['width']; fy += ny * o['width']
az = math.atan2(fy, fx) if (fx or fy) else math.radians(-60)
az += math.radians(35)  # nicht genau frontal – schräges Licht zeichnet Formen
EL = math.radians(32 if MODE == 'vogel' else 24)

w = bpy.data.worlds.new('Himmel'); w.use_nodes = True; sc.world = w; nt = w.node_tree
sky = nt.nodes.new('ShaderNodeTexSky')
try: sky.sky_type = 'MULTIPLE_SCATTERING'
except TypeError: sky.sky_type = 'NISHITA'
sky.sun_disc = False; sky.sun_elevation = EL; sky.sun_rotation = 0.0; sky.altitude = 200
nt.links.new(sky.outputs['Color'], nt.nodes['Background'].inputs['Color'])
sun_l = bpy.data.lights.new('Sonne', 'SUN'); sun_l.angle = math.radians(0.53)
sun = bpy.data.objects.new('Sonne', sun_l); col.objects.link(sun)
d = Vector((math.cos(EL) * math.cos(az), math.cos(EL) * math.sin(az), math.sin(EL)))  # Richtung zur Sonne
sun.rotation_euler = d.to_track_quat('Z', 'Y').to_euler()
# Himmel auf die Sonnenrichtung drehen: Sonnenrichtung des Himmels bei Drehung 0 messen wir nicht – wir nutzen sun_direction, wenn vorhanden
try:
    sky.sun_rotation = (math.pi / 2 - az) % (2 * math.pi)
except Exception:
    pass

# Sonnenstärke kalibrieren: Sonnenscheibe des Himmels gegen Lampe (gleiche Höhe) – gemessen auf einer weißen Fläche
def calibrate():
    tmp = bpy.data.scenes.new('Kalibrierung'); tmp.render.engine = 'CYCLES'; tmp.cycles.device = sc.cycles.device
    tmp.cycles.samples = 32; tmp.cycles.use_denoising = False; tmp.render.resolution_x = tmp.render.resolution_y = 16
    tmp.view_settings.view_transform = 'Standard'
    me = bpy.data.meshes.new('k'); me.from_pydata([(-50, -50, 0), (50, -50, 0), (50, 50, 0), (-50, 50, 0)], [], [(0, 1, 2, 3)])
    m = bpy.data.materials.new('k'); m.use_nodes = True; b = tm._bsdf(m); tm._set(b, **{'Base Color': (1, 1, 1, 1), 'Roughness': 1.0, 'Specular IOR Level': 0.0}); me.materials.append(m)
    ob = bpy.data.objects.new('k', me); tmp.collection.objects.link(ob)
    cam = bpy.data.cameras.new('k'); cam.type = 'ORTHO'; cam.ortho_scale = 1; co = bpy.data.objects.new('kc', cam); co.location = (0, 0, 5); tmp.collection.objects.link(co); tmp.camera = co
    kw = bpy.data.worlds.new('k'); kw.use_nodes = True; tmp.world = kw; kn = kw.node_tree
    ks = kn.nodes.new('ShaderNodeTexSky'); ks.sky_type = sky.sky_type; ks.sun_elevation = EL; ks.altitude = sky.altitude
    kn.links.new(ks.outputs['Color'], kn.nodes['Background'].inputs['Color'])
    path = os.path.join(bpy.app.tempdir, 'trendo_kal.exr'); tmp.render.image_settings.file_format = 'OPEN_EXR'; tmp.render.filepath = path
    def lum():
        bpy.ops.render.render(write_still=True, scene=tmp.name)
        im = bpy.data.images.load(path, check_existing=False); px = im.pixels[:]; bpy.data.images.remove(im); n = len(px) // 4
        return sum(0.2126 * px[4 * i] + 0.7152 * px[4 * i + 1] + 0.0722 * px[4 * i + 2] for i in range(n)) / n
    ks.sun_disc = True; with_disc = lum(); ks.sun_disc = False; without = lum()
    bpy.data.scenes.remove(tmp)
    per_watt = math.sin(EL) / math.pi  # weiße Lambert-Fläche unter einer Sonnenlampe der Stärke 1
    return max(0.5, (with_disc - without) / per_watt)
try:
    sun_l.energy = calibrate()
except Exception as e:
    print('TRENDO_HINWEIS Kalibrierung:', e); sun_l.energy = 120.0
step(0.36)

# ---------------------------------------------------------------- Kamera
cam_d = bpy.data.cameras.new('Kamera'); cam_d.sensor_width = 36; cam_d.clip_start = 0.05; cam_d.clip_end = 500
cam = bpy.data.objects.new('Kamera', cam_d); col.objects.link(cam); sc.camera = cam
W, H = int(S.get('width', 1600)), int(S.get('height', 1000))
sc.render.resolution_x, sc.render.resolution_y = W, H; sc.render.resolution_percentage = 100
if MODE == 'raum':
    rooms = S['rooms']; room = next((r for r in rooms if r['id'] == S.get('room')), None)
    if room is None and rooms:
        room = max(rooms, key=lambda r: abs(signed_area([tuple(p) for p in r['points']])))
    if room is None: fail('Kein Raum im Plan.')
    pts = [tuple(p) for p in room['points']]; cx = sum(p[0] for p in pts) / len(pts); cy = sum(p[1] for p in pts) / len(pts)
    corner = max(pts, key=lambda p: math.hypot(p[0] - cx, p[1] - cy))
    vx, vy = cx - corner[0], cy - corner[1]; L = math.hypot(vx, vy) or 1
    cam.location = (corner[0] + vx / L * 0.35, corner[1] + vy / L * 0.35, room['z'] + 1.45)
    cam.rotation_euler = (math.radians(90), 0, math.atan2(vy, vx) - math.pi / 2)  # waagrecht – senkrechte Linien bleiben senkrecht
    cam_d.lens = 18; cam_d.shift_y = -0.06
else:
    size = max(bx1 - bx0, by1 - by0, 4)
    yaw, pitch = math.radians(-125), math.radians(48)  # von Südwesten, schräg von oben
    cam_d.lens = 35
    from bpy_extras.object_utils import world_to_camera_view
    cz = top * 0.3; target = Vector((bcx, bcy, cz))
    corners = [Vector((x, y, z)) for x in (bx0, bx1) for y in (by0, by1) for z in (0.0, top)]
    def place(dist):
        cam.location = (bcx + dist * math.cos(pitch) * math.cos(yaw), bcy + dist * math.cos(pitch) * math.sin(yaw), cz + dist * math.sin(pitch))
        cam.rotation_euler = (target - Vector(cam.location)).to_track_quat('-Z', 'Y').to_euler()
        bpy.context.view_layer.update()
        return all(0.04 <= p.x <= 0.96 and 0.04 <= p.y <= 0.96 and p.z > 0 for p in (world_to_camera_view(sc, cam, c) for c in corners))
    lo, hi = 1.0, size * 8
    for _ in range(24):  # kleinste Entfernung, bei der die ganze Wohnung mit Rand ins Bild passt
        mid = (lo + hi) / 2
        if place(mid): hi = mid
        else: lo = mid
    place(hi)

# ---------------------------------------------------------------- Render-Einstellungen
cy = sc.cycles
cy.use_adaptive_sampling = True; cy.adaptive_threshold = 0.008
cy.max_bounces = 12; cy.diffuse_bounces = 6; cy.glossy_bounces = 6; cy.transmission_bounces = 12; cy.transparent_max_bounces = 8
cy.caustics_reflective = False; cy.caustics_refractive = False; cy.sample_clamp_indirect = 8.0; cy.blur_glossy = 0.5
cy.use_denoising = True
try: cy.denoiser = 'OPENIMAGEDENOISE'
except TypeError: pass
for k, v in (('view_transform', 'AgX'), ('look', 'AgX - Base Contrast')):
    try: setattr(sc.view_settings, k, v)
    except TypeError: pass
sc.render.film_transparent = False

# Belichtung messen: kleine Proberechnung, geometrisches Mittel der Leuchtdichte auf Mittelgrau (18 %) legen
def meter():
    sc.render.resolution_percentage = 15; cy.samples = 64; cy.use_denoising = True  # entrauscht – sonst ziehen schwarze Rauschpixel das Mittel ins Bodenlose
    path = os.path.join(bpy.app.tempdir, 'trendo_mess.exr'); sc.render.image_settings.file_format = 'OPEN_EXR'; sc.render.filepath = path
    old = sc.view_settings.view_transform
    bpy.ops.render.render(write_still=True)
    im = bpy.data.images.load(path, check_existing=False); px = im.pixels[:]; bpy.data.images.remove(im); n = len(px) // 4
    lums = sorted(0.2126 * px[4 * i] + 0.7152 * px[4 * i + 1] + 0.0722 * px[4 * i + 2] for i in range(n))
    lums = lums[int(n * 0.02): int(n * 0.98)] or lums  # Ausreißer (Sonnenflecken, tiefe Schatten) nicht mitzählen
    g = math.exp(sum(math.log(max(1e-6, v)) for v in lums) / len(lums))
    print('TRENDO_MESSUNG geometrisches Mittel', g, 'Median', lums[len(lums) // 2], 'schwarz', sum(1 for v in lums if v < 1e-5) / len(lums))
    sc.render.resolution_percentage = 100
    return g
try:
    g = meter(); key = 0.16 if MODE == 'vogel' else 0.20
    sc.view_settings.exposure = max(-12, min(12, math.log2(key / g)))
except Exception as e:
    print('TRENDO_HINWEIS Belichtung:', e)
step(0.42)

cy.samples = int(S.get('samples', 256))
sc.render.image_settings.file_format = 'PNG'; sc.render.image_settings.color_mode = 'RGB'; sc.render.image_settings.color_depth = '8'
sc.render.filepath = S['out']
print('TRENDO_PHASE final', DEV); sys.stdout.flush()
bpy.ops.render.render(write_still=True)
if not os.path.exists(S['out']): fail('Kein Bild geschrieben.')
print('TRENDO_OK', S['out'], 'Belichtung', round(sc.view_settings.exposure, 2), 'Sonne', round(sun_l.energy, 1))
