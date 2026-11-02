# Trendo OS – Kreatur-Baukasten für Blender (0.9.47, Wunsch: komplexe Figuren wie ein Drache).
# Baut eine Kreatur in echten Maßen (Meter, Z oben, Kopf zeigt nach −Y) aus einem Skelett mit organischer Haut (Skin + Unterteilung),
# Schuppenrelief, Flughaut, Hörnern und Augen, physikalisch plausiblen Materialien und einem Knochengerüst (automatisch gewichtet).
# Ausgabe: .blend, .glb (mit Rig) und auf Wunsch ein Vorschaubild (Cycles, AgX). Eigene Arbeit, keine fremden Assets.
# Aufruf:  blender [-b] --factory-startup --python trendo_kreatur.py -- '{"art":"drache","ausgabe":"C:/…/drache","bild":true}'
import bpy, json, math, sys, os
from mathutils import Vector

ARGS = json.loads(sys.argv[sys.argv.index('--') + 1]) if '--' in sys.argv else {}
ART = ARGS.get('art', 'drache')
OUT = ARGS.get('ausgabe') or os.path.join(os.path.expanduser('~'), 'trendo-kreatur')
FARBE = ARGS.get('farbe', 'bronze')  # bronze | gruen | rot | schwarz
BILD = bool(ARGS.get('bild', False))
SICHTBAR = not bpy.app.background

# ---------------------------------------------------------------- Baupläne (Punkte in Metern, Radius der Haut)
def drache():
    S = {}  # Name -> (x, y, z, Radius)
    spine = [('schwanz6', 0, 4.10, 0.22, 0.02), ('schwanz5', 0, 3.50, 0.34, 0.05), ('schwanz4', 0, 2.90, 0.52, 0.10), ('schwanz3', 0, 2.30, 0.78, 0.17), ('schwanz2', 0, 1.75, 0.98, 0.27),
             ('huefte', 0, 1.15, 1.12, 0.48), ('bauch', 0, 0.45, 1.20, 0.62), ('brust', 0, -0.35, 1.32, 0.58), ('hals1', 0, -0.88, 1.58, 0.38),
             ('hals2', 0, -1.15, 1.92, 0.28), ('hals3', 0, -1.32, 2.20, 0.23), ('kopf', 0, -1.58, 2.34, 0.27), ('schnauze', 0, -1.98, 2.27, 0.17), ('nase', 0, -2.24, 2.21, 0.10)]
    for n, x, y, z, r in spine: S[n] = (x, y, z, r)
    E = [(spine[i][0], spine[i + 1][0]) for i in range(len(spine) - 1)]
    for side, sx in (('l', 1), ('r', -1)):
        legs = [('vschulter', 0.40, -0.38, 1.12, 0.25, 'brust'), ('vellbogen', 0.52, -0.30, 0.64, 0.16, None), ('vhand', 0.52, -0.52, 0.16, 0.11, None), ('vfuss', 0.52, -0.78, 0.06, 0.09, None),
                ('hhuefte', 0.46, 1.00, 1.00, 0.36, 'huefte'), ('hknie', 0.58, 0.62, 0.58, 0.23, None), ('hferse', 0.58, 1.05, 0.22, 0.13, None), ('hfuss', 0.58, 0.80, 0.06, 0.10, None),
                ('fschulter', 0.34, -0.52, 1.62, 0.13, 'brust'), ('fellbogen', 1.20, -0.22, 2.30, 0.075, None), ('fhand', 2.00, 0.10, 2.62, 0.05, None),
                ('ffinger1', 3.40, 0.40, 2.12, 0.022, 'fhand'), ('ffinger2', 3.05, 1.00, 1.42, 0.022, 'fhand'), ('ffinger3', 2.40, 1.32, 1.00, 0.022, 'fhand')]
        prev = None
        for n, x, y, z, r, parent in legs:
            name = n + side; S[name] = (x * sx, y, z, r)
            src = (parent + side if parent and parent.startswith('f') and parent != 'fschulter' and n.startswith('ffinger') else parent) if parent else prev
            if parent in ('brust', 'huefte'): src = parent
            if n.startswith('ffinger'): src = 'fhand' + side
            E.append((src, name)); prev = name
    return S, E

PLAENE = {'drache': drache}
if ART not in PLAENE: raise SystemExit('Unbekannte Kreatur: ' + ART)
SKEL, EDGES = PLAENE[ART]()

# ---------------------------------------------------------------- Szene leeren
bpy.ops.wm.read_factory_settings(use_empty=True) if bpy.app.background else None
for o in list(bpy.data.objects): bpy.data.objects.remove(o, do_unlink=True)
scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'; scene.unit_settings.scale_length = 1.0
col = bpy.data.collections.new('Kreatur'); scene.collection.children.link(col)

def link(o): col.objects.link(o); return o
def activate(o):
    for x in bpy.context.view_layer.objects: x.select_set(False)
    o.select_set(True); bpy.context.view_layer.objects.active = o

# ---------------------------------------------------------------- Materialien
def nodes(m): return m.node_tree.nodes, m.node_tree.links
def principled(m): return next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
def setin(b, **kw):
    for k, v in kw.items():
        if k in b.inputs: b.inputs[k].default_value = v

PAL = {'bronze': ((0.035, 0.028, 0.016), (0.16, 0.10, 0.035), (0.36, 0.28, 0.17)), 'gruen': ((0.03, 0.07, 0.03), (0.10, 0.20, 0.07), (0.45, 0.42, 0.28)),
       'rot': ((0.10, 0.02, 0.015), (0.35, 0.06, 0.03), (0.60, 0.40, 0.25)), 'schwarz': ((0.012, 0.012, 0.014), (0.05, 0.05, 0.055), (0.25, 0.22, 0.18))}

def schuppen():
    """Schuppenhaut: Voronoi-Zellen als Schuppen (Farbe dunkel in den Fugen), heller Bauch über die Höhe, Rauheit variiert, Relief als Bump, wenig Subsurface."""
    dunkel, mitte, bauch = PAL.get(FARBE, PAL['bronze'])
    m = bpy.data.materials.new('Schuppenhaut'); m.use_nodes = True
    N, L = nodes(m); b = principled(m)
    tc = N.new('ShaderNodeTexCoord'); vor = N.new('ShaderNodeTexVoronoi'); vor.feature = 'DISTANCE_TO_EDGE'; vor.inputs['Scale'].default_value = 20.0
    vor2 = N.new('ShaderNodeTexVoronoi'); vor2.inputs['Scale'].default_value = 20.0
    noise = N.new('ShaderNodeTexNoise'); noise.inputs['Scale'].default_value = 4.0; noise.inputs['Detail'].default_value = 6.0
    fuge = N.new('ShaderNodeMapRange'); fuge.inputs['From Min'].default_value = 0.0; fuge.inputs['From Max'].default_value = 0.08
    L.new(tc.outputs['Object'], vor.inputs['Vector']); L.new(tc.outputs['Object'], vor2.inputs['Vector']); L.new(tc.outputs['Object'], noise.inputs['Vector'])
    L.new(vor.outputs['Distance'], fuge.inputs['Value'])
    ramp = N.new('ShaderNodeValToRGB'); ramp.color_ramp.elements[0].color = (*dunkel, 1); ramp.color_ramp.elements[1].color = (*mitte, 1)
    mixv = N.new('ShaderNodeMath'); mixv.operation = 'MULTIPLY'
    L.new(fuge.outputs['Result'], mixv.inputs[0]); L.new(noise.outputs['Fac'], mixv.inputs[1]); L.new(mixv.outputs['Value'], ramp.inputs['Fac'])
    # Bauch heller: Höhe (Z) und Unterseite über die Normale
    sep = N.new('ShaderNodeSeparateXYZ'); L.new(tc.outputs['Normal'], sep.inputs['Vector'])
    bauchmaske = N.new('ShaderNodeMapRange'); bauchmaske.interpolation_type = 'SMOOTHSTEP'; bauchmaske.inputs['From Min'].default_value = -0.2; bauchmaske.inputs['From Max'].default_value = -0.75
    L.new(sep.outputs['Z'], bauchmaske.inputs['Value'])
    mixc = N.new('ShaderNodeMix'); mixc.data_type = 'RGBA'; mixc.inputs['B'].default_value = (*bauch, 1)
    L.new(bauchmaske.outputs['Result'], mixc.inputs['Factor']); L.new(ramp.outputs['Color'], mixc.inputs['A']); L.new(mixc.outputs['Result'], b.inputs['Base Color'])
    rough = N.new('ShaderNodeMapRange'); rough.inputs['To Min'].default_value = 0.32; rough.inputs['To Max'].default_value = 0.62
    L.new(noise.outputs['Fac'], rough.inputs['Value']); L.new(rough.outputs['Result'], b.inputs['Roughness'])
    bump = N.new('ShaderNodeBump'); bump.inputs['Strength'].default_value = 0.55; bump.inputs['Distance'].default_value = 0.004
    L.new(fuge.outputs['Result'], bump.inputs['Height']); L.new(bump.outputs['Normal'], b.inputs['Normal'])
    setin(b, **{'Subsurface Weight': 0.04, 'Subsurface Radius': (0.004, 0.002, 0.0015), 'Specular IOR Level': 0.45, 'Coat Weight': 0.15, 'Coat Roughness': 0.25})
    return m

def flughaut():
    dunkel, mitte, _ = PAL.get(FARBE, PAL['bronze'])
    m = bpy.data.materials.new('Flughaut'); m.use_nodes = True
    N, L = nodes(m); b = principled(m)
    tc = N.new('ShaderNodeTexCoord'); noise = N.new('ShaderNodeTexNoise'); noise.inputs['Scale'].default_value = 9.0; noise.inputs['Detail'].default_value = 8.0
    ramp = N.new('ShaderNodeValToRGB'); ramp.color_ramp.elements[0].color = (*[c * 0.7 for c in dunkel], 1); ramp.color_ramp.elements[1].color = (*[min(1, c * 1.4) for c in mitte], 1)
    L.new(tc.outputs['Object'], noise.inputs['Vector']); L.new(noise.outputs['Fac'], ramp.inputs['Fac']); L.new(ramp.outputs['Color'], b.inputs['Base Color'])
    setin(b, **{'Roughness': 0.55, 'Subsurface Weight': 0.35, 'Subsurface Radius': (0.03, 0.012, 0.008), 'Transmission Weight': 0.08})
    m.use_backface_culling = False
    return m

def horn():
    m = bpy.data.materials.new('Horn'); m.use_nodes = True; b = principled(m)
    setin(b, **{'Base Color': (0.42, 0.36, 0.27, 1), 'Roughness': 0.38, 'Coat Weight': 0.3})
    return m

def auge():
    m = bpy.data.materials.new('Auge'); m.use_nodes = True; b = principled(m)
    setin(b, **{'Base Color': (0.55, 0.30, 0.02, 1), 'Roughness': 0.05, 'Coat Weight': 1.0, 'Coat Roughness': 0.02, 'Emission Color': (0.9, 0.45, 0.05, 1), 'Emission Strength': 0.15})
    return m

# ---------------------------------------------------------------- Körper: Skelett-Netz mit Skin-Modifier
names = list(SKEL)
me = bpy.data.meshes.new('Koerper'); me.from_pydata([Vector(SKEL[n][:3]) for n in names], [(names.index(a), names.index(b)) for a, b in EDGES], [])
body = link(bpy.data.objects.new('Koerper', me))
skin = body.modifiers.new('Haut', 'SKIN'); skin.use_smooth_shade = True; skin.branch_smoothing = 0.6
for i, n in enumerate(names):
    r = SKEL[n][3]; v = me.skin_vertices[0].data[i]
    v.radius = (r, r * (0.82 if n in ('bauch', 'brust', 'huefte') else 1.0))
    v.use_root = (n == 'brust')
sub = body.modifiers.new('Rund', 'SUBSURF'); sub.levels = 2; sub.render_levels = 2
activate(body)
for mod in ('Haut', 'Rund'): bpy.ops.object.modifier_apply(modifier=mod)
sub2 = body.modifiers.new('Fein', 'SUBSURF'); sub2.levels = 1; sub2.render_levels = 1
bpy.ops.object.modifier_apply(modifier='Fein')
tex = bpy.data.textures.new('Schuppenrelief', 'VORONOI'); tex.noise_scale = 0.07; tex.distance_metric = 'DISTANCE'
disp = body.modifiers.new('Relief', 'DISPLACE'); disp.texture = tex; disp.strength = 0.012; disp.texture_coords = 'OBJECT'
bpy.ops.object.modifier_apply(modifier='Relief')  # Relief fest, bevor Hörner und Augen dazukommen
bpy.ops.object.shade_smooth()
body.data.materials.append(schuppen())

# ---------------------------------------------------------------- Flughaut zwischen Fingern, Arm und Flanke
haut = flughaut(); wings = []
for side, sx in (('l', 1), ('r', -1)):
    P = lambda n: Vector(SKEL[n + side][:3])
    flank = Vector((0.42 * sx, 0.75, 1.32))
    verts = [P('fschulter'), P('fellbogen'), P('fhand'), P('ffinger1'), P('ffinger2'), P('ffinger3'), flank]
    # Zwischenpunkte am freien Rand leicht eingebuchtet (wie echte Flughaut)
    def bucht(a, b, t=0.5, tiefe=0.28):
        m = a.lerp(b, t); d = (m - Vector((0, m.y, 2.2))).normalized() * 0; return m + Vector((0, 0, -tiefe * 0.0)) + (Vector((0, 0.18, -0.12)) * tiefe / 0.28)
    verts += [bucht(verts[3], verts[4]), bucht(verts[4], verts[5]), bucht(verts[5], verts[6], 0.5, 0.4)]
    faces = [(2, 3, 7), (2, 7, 4), (2, 4, 8), (2, 8, 5), (1, 2, 5, 9), (0, 1, 9, 6)]
    wm = bpy.data.meshes.new('Fluegel_' + side); wm.from_pydata(verts, [], faces)
    w = link(bpy.data.objects.new('Fluegel_' + side, wm))
    activate(w)
    s = w.modifiers.new('Weich', 'SUBSURF'); s.levels = 3; s.render_levels = 3
    so = w.modifiers.new('Dicke', 'SOLIDIFY'); so.thickness = 0.012
    bpy.ops.object.modifier_apply(modifier='Weich'); bpy.ops.object.modifier_apply(modifier='Dicke')
    bpy.ops.object.shade_smooth(); w.data.materials.append(haut); wings.append(w)

# ---------------------------------------------------------------- Hörner, Augen, Rückenstacheln
hm, am = horn(), auge(); extras = []
def kegel(name, pos, rot, r1, laenge):
    bpy.ops.mesh.primitive_cone_add(vertices=24, radius1=r1, radius2=0.004, depth=laenge, location=pos, rotation=rot)
    o = bpy.context.active_object; o.name = name; col.objects.link(o) if o.name not in col.objects else None
    for c in list(o.users_collection):
        if c != col: c.objects.unlink(o)
    bpy.ops.object.shade_smooth(); o.data.materials.append(hm); extras.append(o); return o
for side, sx in (('l', 1), ('r', -1)):
    kegel('Horn_' + side, (0.13 * sx, -1.40, 2.46), (math.radians(-58), math.radians(16 * sx), 0), 0.05, 0.36)
    bpy.ops.mesh.primitive_uv_sphere_add(segments=32, ring_count=16, radius=0.034, location=(0.165 * sx, -1.70, 2.36))
    e = bpy.context.active_object; e.name = 'Auge_' + side
    for c in list(e.users_collection):
        if c != col: c.objects.unlink(e)
    if e.name not in col.objects: col.objects.link(e)
    bpy.ops.object.shade_smooth(); e.data.materials.append(am); extras.append(e)
for side, sx in (('l', 1), ('r', -1)):
    for foot in ('vfuss', 'hfuss'):
        f = Vector(SKEL[foot + side][:3])
        for k, dx in enumerate((-0.06, 0.0, 0.06)):
            kegel(f'Kralle_{foot}{side}{k}', (f.x + dx * sx, f.y - 0.07, 0.045), (math.radians(-105), 0, math.radians(dx * 120 * sx)), 0.024, 0.11)
for i, (n1, n2) in enumerate([('hals1', 'brust'), ('brust', 'bauch'), ('bauch', 'huefte'), ('huefte', 'schwanz2'), ('schwanz2', 'schwanz3'), ('schwanz3', 'schwanz4')]):
    a, b = Vector(SKEL[n1][:3]), Vector(SKEL[n2][:3])
    for t in (0.25, 0.75):
        p = a.lerp(b, t); r = SKEL[n1][3] * (1 - t) + SKEL[n2][3] * t
        kegel(f'Stachel_{i}_{t}', (p.x, p.y, p.z + r * 0.92), (math.radians(-20), 0, 0), max(0.02, r * 0.18), max(0.05, r * 0.55))

# ---------------------------------------------------------------- Knochengerüst (Bones entlang der Skelettkanten), automatisch gewichtet
arm_data = bpy.data.armatures.new('Rig'); rig = link(bpy.data.objects.new('Rig', arm_data))
activate(rig); bpy.ops.object.mode_set(mode='EDIT')
bones = {}
for a, b in EDGES:
    bn = arm_data.edit_bones.new(b); bn.head = Vector(SKEL[a][:3]); bn.tail = Vector(SKEL[b][:3]); bones[b] = bn
for a, b in EDGES:
    if a in bones: bones[b].parent = bones[a]; bones[b].use_connect = (bones[a].tail - bones[b].head).length < 1e-4
bpy.ops.object.mode_set(mode='OBJECT')
for x in bpy.context.view_layer.objects: x.select_set(False)
for o in extras: o.select_set(True)
body.select_set(True); bpy.context.view_layer.objects.active = body
bpy.ops.object.join(); extras = []
for o in [body] + wings:
    for x in bpy.context.view_layer.objects: x.select_set(False)
    o.select_set(True); rig.select_set(True); bpy.context.view_layer.objects.active = rig
    try: bpy.ops.object.parent_set(type='ARMATURE_AUTO')
    except RuntimeError: bpy.ops.object.parent_set(type='ARMATURE_ENVELOPE')

# ---------------------------------------------------------------- Bühne: Boden, Licht mit Herkunft, Kamera mit echter Brennweite
bpy.ops.mesh.primitive_plane_add(size=40, location=(0, 0, 0)); ground = bpy.context.active_object; ground.name = 'Boden'
gm = bpy.data.materials.new('Boden'); gm.use_nodes = True; gb = principled(gm)
N, L = nodes(gm); gn = N.new('ShaderNodeTexNoise'); gn.inputs['Scale'].default_value = 1.5; gn.inputs['Detail'].default_value = 12
gr = N.new('ShaderNodeValToRGB'); gr.color_ramp.elements[0].color = (0.025, 0.022, 0.02, 1); gr.color_ramp.elements[1].color = (0.06, 0.052, 0.045, 1)
L.new(gn.outputs['Fac'], gr.inputs['Fac']); L.new(gr.outputs['Color'], gb.inputs['Base Color']); setin(gb, Roughness=0.85)
ground.data.materials.append(gm)
world = bpy.data.worlds.new('Welt'); scene.world = world; world.use_nodes = True
WN, WL = world.node_tree.nodes, world.node_tree.links
for n in list(WN): WN.remove(n)
bg = WN.new('ShaderNodeBackground'); out = WN.new('ShaderNodeOutputWorld'); sky = WN.new('ShaderNodeTexGradient'); wtc = WN.new('ShaderNodeTexCoord')
wr = WN.new('ShaderNodeValToRGB'); wr.color_ramp.elements[0].color = (0.006, 0.006, 0.008, 1); wr.color_ramp.elements[1].color = (0.05, 0.05, 0.06, 1)
WL.new(wtc.outputs['Generated'], sky.inputs['Vector']); WL.new(sky.outputs['Fac'], wr.inputs['Fac']); WL.new(wr.outputs['Color'], bg.inputs['Color']); WL.new(bg.outputs['Background'], out.inputs['Surface'])
bg.inputs['Strength'].default_value = 1.0
def area(name, loc, energy, size, color, target=(0, 0, 1.3)):
    d = bpy.data.lights.new(name, 'AREA'); d.energy = energy; d.size = size; d.color = color
    o = link(bpy.data.objects.new(name, d)); o.location = loc
    dirv = Vector(target) - Vector(loc); o.rotation_euler = dirv.to_track_quat('-Z', 'Y').to_euler()
    try: o.visible_camera = False
    except AttributeError: pass
    return o
area('Hauptlicht', (-4.5, -5.5, 6.0), 900, 3.0, (1.0, 0.93, 0.82))       # warmes Fensterlicht von vorn links oben
area('Kantenlicht', (5.0, 4.5, 4.0), 700, 2.0, (0.80, 0.88, 1.0))        # kühles Licht von hinten für die Silhouette
area('Aufhellung', (5.5, -4.0, 1.5), 90, 4.0, (1.0, 0.97, 0.92))
cam_d = bpy.data.cameras.new('Kamera'); cam_d.lens = 50; cam_d.sensor_width = 36
cam_d.dof.use_dof = True; cam_d.dof.aperture_fstop = 5.6
cam = link(bpy.data.objects.new('Kamera', cam_d)); cam.location = (5.6, -6.4, 2.1)
target = Vector((0, 0.3, 1.25)); cam.rotation_euler = (target - cam.location).to_track_quat('-Z', 'Y').to_euler()
cam_d.dof.focus_distance = (target - cam.location).length
scene.camera = cam

# ---------------------------------------------------------------- Farbmanagement und Speichern
scene.view_settings.view_transform = 'AgX'
for look in ('AgX - Medium High Contrast', 'Medium High Contrast', 'None'):
    try: scene.view_settings.look = look; break
    except TypeError: continue
os.makedirs(os.path.dirname(OUT) or '.', exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=OUT + '.blend')
for x in bpy.context.view_layer.objects: x.select_set(x.name not in ('Boden', 'Kamera') and x.type != 'LIGHT')
bpy.ops.export_scene.gltf(filepath=OUT + '.glb', export_format='GLB', use_selection=True, export_apply=True)
stats = {'art': ART, 'dreiecke': sum(len(o.data.polygons) for o in [body] + wings + extras), 'knochen': len(arm_data.bones), 'glb': OUT + '.glb', 'blend': OUT + '.blend'}

if BILD:
    scene.render.engine = 'CYCLES'
    prefs = bpy.context.preferences.addons['cycles'].preferences
    gpu = False
    for t in ('OPTIX', 'CUDA', 'HIP', 'METAL'):
        try:
            prefs.compute_device_type = t; prefs.get_devices()
            if any(d.type != 'CPU' for d in prefs.devices):
                for d in prefs.devices: d.use = d.type != 'CPU'
                gpu = True; break
        except TypeError: continue
    scene.cycles.device = 'GPU' if gpu else 'CPU'
    scene.cycles.samples = int(ARGS.get('samples', 96)); scene.cycles.use_denoising = True
    scene.render.resolution_x, scene.render.resolution_y = int(ARGS.get('breite', 1280)), int(ARGS.get('hoehe', 720))
    scene.render.filepath = OUT + '.png'; scene.render.image_settings.file_format = 'PNG'
    bpy.ops.render.render(write_still=True)
    stats['bild'] = OUT + '.png'; stats['geraet'] = scene.cycles.device

print('TRENDO_KREATUR ' + json.dumps(stats))
