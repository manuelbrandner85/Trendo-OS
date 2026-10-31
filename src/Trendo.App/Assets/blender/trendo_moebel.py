# Trendo OS – Möbel und Einrichtung für Blender (T-0278).
# Baut jedes Möbel des Grundriss-Katalogs in echten Maßen (Meter, Ursprung Bodenmitte, Vorderseite zeigt nach −Y),
# mit Fasen an allen Kanten, Polstern und physikalisch plausiblen Materialien (Cycles). Eigene Arbeit, keine fremden Assets.
import bpy, bmesh, math, random
from mathutils import Vector

# ---------------------------------------------------------------- Materialien
def _mat(name):
    m = bpy.data.materials.get(name)
    if m: return m, False
    m = bpy.data.materials.new(name); m.use_nodes = True
    return m, True

def _bsdf(m):
    return next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')

def _set(b, **kw):
    for k, v in kw.items():
        if k in b.inputs: b.inputs[k].default_value = v

def _bump(m, scale=40.0, strength=0.08, detail=8.0, coords='Object'):
    nt = m.node_tree; b = _bsdf(m)
    tc = nt.nodes.new('ShaderNodeTexCoord'); n = nt.nodes.new('ShaderNodeTexNoise'); bu = nt.nodes.new('ShaderNodeBump')
    n.inputs['Scale'].default_value = scale; n.inputs['Detail'].default_value = detail
    nt.links.new(tc.outputs[coords], n.inputs['Vector']); nt.links.new(n.outputs['Fac'], bu.inputs['Height'])
    bu.inputs['Strength'].default_value = strength; nt.links.new(bu.outputs['Normal'], b.inputs['Normal'])
    return n

def wood(name, c1, c2, rough=0.45, coat=0.0, vertical=False):
    """Holz: Jahresringe (Wellen gestreckt entlang X) + Faser-Rauschen, Rauheit mit Maserung variiert, feine Poren als Bump."""
    m, new = _mat(name)
    if not new: return m
    nt = m.node_tree; b = _bsdf(m)
    tc = nt.nodes.new('ShaderNodeTexCoord'); mp = nt.nodes.new('ShaderNodeMapping'); mp.inputs['Scale'].default_value = (9.0, 9.0, 1.0) if vertical else (1.0, 9.0, 9.0) # Faser längs zum Bauteil
    wv = nt.nodes.new('ShaderNodeTexWave'); wv.wave_type = 'RINGS'; wv.inputs['Scale'].default_value = 3.0; wv.inputs['Distortion'].default_value = 6.0; wv.inputs['Detail'].default_value = 3.0
    nz = nt.nodes.new('ShaderNodeTexNoise'); nz.inputs['Scale'].default_value = 60; nz.inputs['Detail'].default_value = 10
    mix = nt.nodes.new('ShaderNodeMix'); mix.data_type = 'RGBA'; mix.inputs['Factor'].default_value = 0.25
    ramp = nt.nodes.new('ShaderNodeValToRGB'); ramp.color_ramp.elements[0].color = (*c1, 1); ramp.color_ramp.elements[1].color = (*c2, 1)
    nt.links.new(tc.outputs['Object'], mp.inputs['Vector']); nt.links.new(mp.outputs['Vector'], wv.inputs['Vector']); nt.links.new(mp.outputs['Vector'], nz.inputs['Vector'])
    nt.links.new(wv.outputs['Fac'], mix.inputs['A']); nt.links.new(nz.outputs['Fac'], mix.inputs['B']); nt.links.new(mix.outputs['Result'], ramp.inputs['Fac'])
    nt.links.new(ramp.outputs['Color'], b.inputs['Base Color'])
    rr = nt.nodes.new('ShaderNodeMapRange'); rr.inputs['To Min'].default_value = rough - 0.08; rr.inputs['To Max'].default_value = rough + 0.08
    nt.links.new(wv.outputs['Fac'], rr.inputs['Value']); nt.links.new(rr.outputs['Result'], b.inputs['Roughness'])
    _set(b, **{'Coat Weight': coat, 'Coat Roughness': 0.15})
    bu = nt.nodes.new('ShaderNodeBump'); bu.inputs['Strength'].default_value = 0.05
    nt.links.new(nz.outputs['Fac'], bu.inputs['Height']); nt.links.new(bu.outputs['Normal'], b.inputs['Normal'])
    m.diffuse_color = (*[(a + b2) / 2 for a, b2 in zip(c1, c2)], 1)
    return m

def fabric(name, col, rough=0.85, sheen=0.4, scale=900):
    """Stoff: Webstruktur als sehr feiner Bump, Sheen für den Samtschimmer an der Kante, Farbe leicht variiert."""
    m, new = _mat(name)
    if not new: return m
    b = _bsdf(m); _set(b, **{'Base Color': (*col, 1), 'Roughness': rough, 'Sheen Weight': sheen, 'Sheen Roughness': 0.4})
    _bump(m, scale=scale, strength=0.12, detail=2)
    # Makro-Variation: Stoff hat Flecken unterschiedlicher Sättigung/Helligkeit (Abrieb, Faserlage) – ±7 %
    nt = m.node_tree; tc = nt.nodes.new('ShaderNodeTexCoord'); nz = nt.nodes.new('ShaderNodeTexNoise'); nz.inputs['Scale'].default_value = 6; nz.inputs['Detail'].default_value = 4
    hsv = nt.nodes.new('ShaderNodeHueSaturation'); hsv.inputs['Color'].default_value = (*col, 1)
    mr = nt.nodes.new('ShaderNodeMapRange'); mr.inputs['To Min'].default_value = 0.93; mr.inputs['To Max'].default_value = 1.07
    nt.links.new(tc.outputs['Object'], nz.inputs['Vector']); nt.links.new(nz.outputs['Fac'], mr.inputs['Value']); nt.links.new(mr.outputs['Result'], hsv.inputs['Value'])
    nt.links.new(hsv.outputs['Color'], b.inputs['Base Color'])
    m.diffuse_color = (*col, 1); return m

def solid(name, col, rough=0.5, metal=0.0, coat=0.0, spec=0.5, bump=0.0, transmission=0.0, ior=1.45):
    m, new = _mat(name)
    if not new: return m
    b = _bsdf(m); _set(b, **{'Base Color': (*col, 1), 'Roughness': rough, 'Metallic': metal, 'Coat Weight': coat, 'Coat Roughness': 0.05,
                            'Specular IOR Level': spec, 'Transmission Weight': transmission, 'IOR': ior})
    if bump: _bump(m, scale=200, strength=bump, detail=6)
    m.diffuse_color = (*col, 1); return m

def leaf(name):
    m, new = _mat(name)
    if not new: return m
    b = _bsdf(m); _set(b, **{'Base Color': (0.06, 0.22, 0.05, 1), 'Roughness': 0.45, 'Subsurface Weight': 0.25, 'Subsurface Radius': (0.1, 0.25, 0.05), 'Transmission Weight': 0.1, 'Coat Weight': 0.2})
    _bump(m, scale=80, strength=0.05)
    m.diffuse_color = (0.08, 0.25, 0.07, 1); return m

def M():
    """Alle Werkstoffe (sRGB-Farben linear umgerechnet, Albedo im realen Bereich 0,03–0,85)."""
    return dict(
        eiche=wood("Eiche geölt", (0.30, 0.17, 0.07), (0.55, 0.36, 0.18), 0.42),
        eiche_stab=wood("Eiche geölt (stehend)", (0.30, 0.17, 0.07), (0.55, 0.36, 0.18), 0.42, vertical=True),
        buche=wood("Buche hell", (0.52, 0.33, 0.18), (0.72, 0.52, 0.32), 0.38, 0.1),
        nuss=wood("Nussbaum", (0.10, 0.05, 0.025), (0.24, 0.13, 0.06), 0.35, 0.2),
        stoff_grau=fabric("Stoff Anthrazit", (0.07, 0.075, 0.08)),
        stoff_blau=fabric("Stoff Petrol", (0.03, 0.09, 0.11)),
        stoff_beige=fabric("Leinen Beige", (0.50, 0.43, 0.33), 0.9, 0.2, 1400),
        matratze=fabric("Bezug Weiß", (0.75, 0.75, 0.72), 0.95, 0.3, 1600),
        decke=fabric("Bettdecke Grau", (0.32, 0.33, 0.34), 0.95, 0.5, 700),
        teppich=fabric("Wollteppich", (0.22, 0.20, 0.17), 1.0, 0.6, 400),
        lack_weiss=solid("Lack Weiß matt", (0.80, 0.80, 0.78), 0.35, bump=0.003),
        lack_grau=solid("Lack Grau", (0.25, 0.26, 0.27), 0.4),
        chrom=solid("Chrom", (0.95, 0.93, 0.88), 0.08, 1.0),
        edelstahl=solid("Edelstahl gebürstet", (0.80, 0.78, 0.75), 0.28, 1.0, bump=0.02),
        metall_schwarz=solid("Stahl pulverbeschichtet", (0.025, 0.025, 0.025), 0.55, 0.0, bump=0.01),
        keramik=solid("Sanitärkeramik", (0.85, 0.85, 0.83), 0.06, coat=0.6),
        glas=solid("Glas", (0.95, 0.98, 0.97), 0.0, transmission=1.0, ior=1.5),
        kunststoff=solid("Kunststoff Weiß", (0.78, 0.78, 0.76), 0.45),
        kunststoff_schwarz=solid("Kunststoff Schwarz", (0.02, 0.02, 0.022), 0.4),
        arbeitsplatte=solid("Arbeitsplatte Stein", (0.18, 0.17, 0.16), 0.3, coat=0.3, bump=0.01),
        bildschirm=solid("Bildschirm", (0.004, 0.004, 0.005), 0.05, coat=1.0),
        erde=solid("Blumenerde", (0.05, 0.035, 0.02), 0.95, bump=0.3),
        topf=solid("Keramiktopf", (0.62, 0.58, 0.52), 0.55, bump=0.01),
        blatt=leaf("Blatt"),
        stamm=solid("Stamm", (0.12, 0.08, 0.04), 0.8, bump=0.2),
        gummi=solid("Gummi", (0.03, 0.03, 0.03), 0.8),
        fliese=solid("Duschtasse", (0.88, 0.88, 0.86), 0.12, coat=0.4),
    )

# ---------------------------------------------------------------- Bauteile
_parts = []
# "render" = volle Qualität für Cycles; "preview" = leichtere Netze für die 3D-Vorschau in Trendo (gleiche Form, weniger Unterteilung)
QUALITY = "render"
def _q(render, preview): return preview if QUALITY == "preview" else render

def _obj(name, bm, loc, mat, smooth=True):
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me); o.location = loc
    (bpy.context.scene.collection if bpy.context.scene else bpy.data.scenes[0].collection).objects.link(o)
    me.materials.append(mat)
    if smooth:
        for p in me.polygons: p.use_smooth = True
    _parts.append(o); return o

def _mods(o, bevel, seg, subsurf, minside):
    if bevel > 0:
        md = o.modifiers.new("Fase", 'BEVEL'); md.width = min(bevel, minside / 2.1); md.segments = _q(seg, min(seg, 2)); md.limit_method = 'ANGLE'; md.harden_normals = False
    if subsurf:
        lv = _q(subsurf, 1); sd = o.modifiers.new("Glätten", 'SUBSURF'); sd.levels = lv; sd.render_levels = lv

def box(name, x, y, z, w, d, h, mat, bevel=0.004, seg=3, subsurf=0, rot=0.0):
    """Quader mit Fase (Mitte unten bei x, y, z; w/d/h in Metern). Objektursprung = Quadermitte."""
    bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts: v.co = Vector((v.co.x * w, v.co.y * d, v.co.z * h))
    o = _obj(name, bm, (x, y, z + h / 2), mat, smooth=subsurf > 0); o.rotation_euler[2] = rot
    _mods(o, bevel, seg, subsurf, min(w, d, h)); return o

def cushion(name, x, y, z, w, d, h, mat, puff=0.35, axis='z'):
    """Polster: abgerundeter Block, die Sichtfläche leicht gewölbt wie ein gefülltes Kissen (axis 'z' = Sitzkissen oben, 'y' = Rückenkissen vorne),
    Kanten etwas eingezogen (Keder-Naht), dazu eine kaum sichtbare Unregelmäßigkeit – echte Polster sind nie ganz gleich."""
    rnd = random.Random(hash(name) & 0xffff)
    bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=_q(6, 3), use_grid_fill=True)
    for v in bm.verts:
        c = Vector((v.co.x * w, v.co.y * d, v.co.z * h))
        if axis == 'z' and v.co.z > 0.2:
            fx = 1 - min(1, abs(v.co.x) * 2) ** 2; fy = 1 - min(1, abs(v.co.y) * 2) ** 2; c.z += h * 0.35 * puff * fx * fy
        if axis == 'y' and v.co.y < -0.2:
            fx = 1 - min(1, abs(v.co.x) * 2) ** 2; fz = 1 - min(1, abs(v.co.z) * 2) ** 2; c.y -= d * 0.35 * puff * fx * fz
        c += Vector((rnd.uniform(-1, 1), rnd.uniform(-1, 1), rnd.uniform(-1, 1))) * 0.0015
        v.co = c
    o = _obj(name, bm, (x, y, z + h / 2), mat)
    _mods(o, min(w, d, h) * 0.3, 4, 2, min(w, d, h)); return o

def cyl(name, x, y, z, r, h, mat, verts=32, bevel=0.002, r2=None):
    bm = bmesh.new(); bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=_q(verts, max(12, verts // 2)), radius1=r, radius2=r if r2 is None else r2, depth=h)
    o = _obj(name, bm, (x, y, z + h / 2), mat)
    _mods(o, bevel, 2, 0, r); return o

def ellipsoid(name, x, y, z, rx, ry, rz, mat, cut_top=False):
    bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=_q(48, 16), v_segments=_q(24, 8), radius=1.0)
    for v in bm.verts: v.co = Vector((v.co.x * rx, v.co.y * ry, v.co.z * rz))
    return _obj(name, bm, (x, y, z), mat)

def legs(prefix, w, d, inset, h, r, mat, tapered=True):
    for i, (sx, sy) in enumerate([(-1, -1), (1, -1), (-1, 1), (1, 1)]):
        cyl(f"{prefix}Bein{i}", sx * (w / 2 - inset), sy * (d / 2 - inset), 0, r, h, mat, r2=r * 0.7 if tapered else None)

def basin(name, x, y, z, w, d, depth, wall, mat):
    """Becken (Wanne, Waschbecken): Körper mit eingesenkter, abgerundeter Mulde (Boolesche Differenz)."""
    outer = box(name, x, y, z, w, d, depth, mat, bevel=wall * 0.9, seg=4)
    inner = box(name + "Mulde", x, y, z + wall, w - 2 * wall, d - 2 * wall, depth, mat, bevel=min(w, d) * 0.18, seg=6)
    bo = outer.modifiers.new("Mulde", 'BOOLEAN'); bo.object = inner; bo.operation = 'DIFFERENCE'
    inner.hide_render = True; inner.hide_viewport = True; _parts.remove(inner); inner["trendo_helper"] = True
    return outer

def cutout(target, x, y, z, w, d, h):
    """Öffnung aus einem Bauteil schneiden (z. B. Becken in der Arbeitsplatte)."""
    c = box(target.name + "Ausschnitt", x, y, z, w, d, h, target.data.materials[0], bevel=0.02, seg=3)
    bo = target.modifiers.new("Ausschnitt", 'BOOLEAN'); bo.object = c; bo.operation = 'DIFFERENCE'
    c.hide_render = True; c.hide_viewport = True; _parts.remove(c); c["trendo_helper"] = True

# ---------------------------------------------------------------- Möbel (Ursprung: Bodenmitte, Front −Y)
def build(kind, W, D, H, mats=None):
    """Baut ein Möbel des Katalogs in W × D × H (Meter) und gibt das übergeordnete Leerobjekt zurück."""
    global _parts; _parts = []; m = mats or M(); rnd = random.Random(hash(kind) & 0xffff)
    k = kind
    if k == "sofa":
        sh, seat = 0.12, 0.42
        legs("", W, D, 0.07, sh, 0.018, m['metall_schwarz'])
        box("Gestell", 0, 0, sh, W, D, 0.22, m['stoff_grau'], 0.03, 4, 1)
        for s in (-1, 1): cushion(f"Armlehne{s}", s * (W / 2 - 0.09), 0.0, sh, 0.18, D, 0.62 - sh, m['stoff_grau'], 0.1)
        inner = W - 0.36; n = 3
        for i in range(n): cushion(f"Sitzkissen{i}", -inner / 2 + inner / n * (i + 0.5), -0.06, sh + 0.22, inner / n - 0.01, D - 0.24, seat - sh - 0.22 + 0.06, m['stoff_grau'])
        for i in range(n): cushion(f"Rückenkissen{i}", -inner / 2 + inner / n * (i + 0.5), D / 2 - 0.15, seat - 0.02, inner / n - 0.015, 0.18, H - seat - 0.02, m['stoff_grau'], 0.5, 'y').rotation_euler[0] = math.radians(-9)
        box("Rücken", 0, D / 2 - 0.05, sh, W - 0.02, 0.1, H - 0.08 - sh, m['stoff_grau'], 0.03, 4, 1)
        cushion("Zierkissen", -inner / 2 + 0.25, D / 2 - 0.32, seat, 0.42, 0.12, 0.4, m['stoff_beige'], 0.9, 'y').rotation_euler = (math.radians(-12), 0, math.radians(8))
    elif k == "sessel":
        legs("", W, D, 0.06, 0.18, 0.016, m['eiche_stab'])
        box("Korpus", 0, 0, 0.18, W, D, 0.2, m['stoff_blau'], 0.03, 4, 1)
        for s in (-1, 1): cushion(f"Armlehne{s}", s * (W / 2 - 0.07), 0.02, 0.18, 0.14, D - 0.04, 0.42, m['stoff_blau'], 0.1)
        cushion("Sitz", 0, -0.05, 0.38, W - 0.28, D - 0.22, 0.1, m['stoff_blau'])
        cushion("Lehne", 0, D / 2 - 0.12, 0.38, W - 0.28, 0.16, H - 0.4, m['stoff_blau'], 0.4, 'y').rotation_euler[0] = math.radians(-10)
    elif k == "couchtisch":
        box("Platte", 0, 0, H - 0.035, W, D, 0.035, m['nuss'], 0.004)
        box("Ablage", 0, 0, 0.1, W - 0.12, D - 0.12, 0.02, m['nuss'], 0.003)
        legs("", W, D, 0.04, H - 0.035, 0.016, m['metall_schwarz'], False)
    elif k == "tv":
        legs("", W, D, 0.05, 0.12, 0.012, m['metall_schwarz'], False)
        box("Korpus", 0, 0, 0.12, W, D, H - 0.12, m['eiche'], 0.004)
        for i in range(3): box(f"Front{i}", -W / 3 + W / 3 * i, -D / 2 - 0.002, 0.14, W / 3 - 0.008, 0.01, H - 0.16, m['lack_weiss'], 0.002)
        box("Fernseher", 0, 0.03, H + 0.06, 1.45, 0.04, 0.83, m['bildschirm'], 0.004)
        box("Fuß", 0, 0.05, H, 0.35, 0.22, 0.06, m['metall_schwarz'], 0.004)
    elif k == "regal":
        for s in (-1, 1): box(f"Seite{s}", s * (W / 2 - 0.0095), 0, 0, 0.019, D, H, m['buche'], 0.002)
        for i in range(6): box(f"Boden{i}", 0, 0, 0.02 + i * (H - 0.04) / 5, W - 0.038, D - 0.01, 0.019, m['buche'], 0.002)
        box("Rückwand", 0, D / 2 - 0.004, 0, W - 0.02, 0.006, H, m['lack_weiss'], 0.0)
        for i in range(5):  # Bücher in leicht unterschiedlicher Höhe und Farbe
            x = -W / 2 + 0.05
            while x < W / 2 - 0.12:
                bw = rnd.uniform(0.02, 0.045); bh = rnd.uniform(0.2, 0.3)
                col = solid(f"Buch{rnd.randint(0, 5)}", [(0.35, 0.05, 0.04), (0.04, 0.12, 0.25), (0.6, 0.55, 0.45), (0.05, 0.2, 0.1), (0.15, 0.15, 0.16), (0.55, 0.4, 0.1)][rnd.randint(0, 5)], 0.6)
                box("Buch", x + bw / 2, 0, 0.04 + i * (H - 0.04) / 5, bw, D * 0.75, min(bh, (H - 0.04) / 5 - 0.04), col, 0.002)
                x += bw + rnd.uniform(0.0, 0.006)
                if rnd.random() < 0.08: x += 0.08
    elif k == "pflanze":
        cyl("Topf", 0, 0, 0, W * 0.38, 0.32, m['topf'], 48, 0.005, W * 0.3)
        cyl("Erde", 0, 0, 0.29, W * 0.35, 0.02, m['erde'], 48, 0)
        for i in range(3): cyl(f"Stamm{i}", rnd.uniform(-0.03, 0.03), rnd.uniform(-0.03, 0.03), 0.3, 0.012, H * rnd.uniform(0.55, 0.75), m['stamm'], 8, 0)
        for i in range(70):  # Blätter: gewölbte, schräg stehende Ellipsen mit zufälliger Größe
            a = rnd.uniform(0, 2 * math.pi); r = rnd.uniform(0.04, W * 0.5); z = rnd.uniform(0.55, H - 0.05)
            lf = ellipsoid("Blatt", math.cos(a) * r * (z / H), math.sin(a) * r * (z / H), z, rnd.uniform(0.05, 0.09), 0.025, 0.004, m['blatt'])
            lf.rotation_euler = (rnd.uniform(-0.6, 0.6), rnd.uniform(-0.4, 0.4), a)
    elif k == "teppich":
        box("Teppich", 0, 0, 0, W, D, 0.012, m['teppich'], 0.005, 2)
    elif k in ("bett", "doppelbett"):
        box("Rahmen", 0, 0.0, 0.12, W + 0.06, D + 0.04, 0.2, m['eiche'], 0.006)
        legs("", W + 0.06, D + 0.04, 0.06, 0.12, 0.025, m['eiche_stab'], False)
        box("Kopfteil", 0, D / 2 + 0.03, 0.12, W + 0.06, 0.06, 0.9, m['stoff_beige'], 0.02, 4, 1)
        cushion("Matratze", 0, 0, 0.3, W, D, 0.2, m['matratze'], 0.05)
        cushion("Decke", 0, -0.12, 0.49, W - 0.02, D * 0.72, 0.06, m['decke'], 0.6)
        for i in range(2 if k == "doppelbett" else 1):
            cushion(f"Kissen{i}", (-W / 4 + i * W / 2) if k == "doppelbett" else 0, D / 2 - 0.25, 0.5, min(0.7, W / 2 - 0.1) if k == "doppelbett" else 0.7, 0.4, 0.14, m['matratze'], 0.9)
    elif k == "schrank":
        box("Korpus", 0, 0, 0, W, D, H, m['lack_weiss'], 0.003)
        n = max(2, round(W / 0.5))
        for i in range(n):
            box(f"Tür{i}", -W / 2 + W / n * (i + 0.5), -D / 2 - 0.009, 0.01, W / n - 0.004, 0.019, H - 0.02, m['lack_weiss'], 0.002)
            box(f"Griff{i}", -W / 2 + W / n * (i + 0.5) + (W / n / 2 - 0.05) * (1 if i % 2 == 0 else -1), -D / 2 - 0.03, H * 0.45, 0.012, 0.02, 0.3, m['edelstahl'], 0.002)
    elif k in ("kommode", "nachttisch"):
        legs("", W, D, 0.04, 0.1, 0.012, m['metall_schwarz'], False)
        box("Korpus", 0, 0, 0.1, W, D, H - 0.1, m['eiche'], 0.004)
        n = 3 if k == "kommode" else 2; fh = (H - 0.14) / n
        for i in range(n):
            box(f"Lade{i}", 0, -D / 2 - 0.004, 0.12 + i * fh, W - 0.02, 0.012, fh - 0.006, m['eiche'], 0.002)
            box(f"Griff{i}", 0, -D / 2 - 0.016, 0.12 + i * fh + fh / 2 - 0.006, min(0.16, W * 0.4), 0.014, 0.012, m['metall_schwarz'], 0.003)
    elif k == "kuechenzeile":
        box("Sockel", 0, 0.03, 0, W, D - 0.06, 0.1, m['kunststoff_schwarz'], 0.002)
        box("Unterschränke", 0, 0, 0.1, W, D - 0.04, 0.76, m['lack_grau'], 0.002)
        n = max(2, round(W / 0.6))
        for i in range(n):
            box(f"Front{i}", -W / 2 + W / n * (i + 0.5), -D / 2 + 0.012, 0.11, W / n - 0.004, 0.019, 0.74, m['lack_grau'], 0.002)
            box(f"Griffleiste{i}", -W / 2 + W / n * (i + 0.5), -D / 2 + 0.0, 0.78, W / n - 0.08, 0.012, 0.012, m['edelstahl'], 0.002)
        pl = box("Arbeitsplatte", 0, 0, 0.86, W, D, 0.04, m['arbeitsplatte'], 0.003)
        cutout(pl, W / 2 - 0.45, -0.02, 0.84, 0.48, 0.4, 0.1)
        basin("Spüle", W / 2 - 0.45, -0.02, 0.72, 0.5, 0.42, 0.18, 0.012, m['edelstahl'])  # eingelassen
        cyl("Armatur", W / 2 - 0.45, 0.22, 0.9, 0.022, 0.26, m['chrom'], 32)
        box("Kochfeld", -W / 2 + 0.45, 0, 0.9, 0.58, 0.51, 0.006, m['bildschirm'], 0.002)
    elif k == "kuehlschrank":
        box("Korpus", 0, 0, 0, W, D, H, m['edelstahl'], 0.008)
        box("Fuge", 0, -D / 2 - 0.0005, H * 0.62, W - 0.01, 0.002, 0.006, m['kunststoff_schwarz'], 0)
        for zz, hh in ((H * 0.1, 0.25), (H * 0.66, 0.3)): box("Griff", W / 2 - 0.06, -D / 2 - 0.025, zz, 0.018, 0.03, hh, m['edelstahl'], 0.004)
    elif k == "herd":
        box("Korpus", 0, 0, 0, W, D, H - 0.01, m['edelstahl'], 0.004)
        box("Kochfeld", 0, 0, H - 0.01, W - 0.01, D - 0.01, 0.006, m['bildschirm'], 0.002)
        box("Backofentür", 0, -D / 2 - 0.006, 0.12, W - 0.02, 0.012, 0.55, m['bildschirm'], 0.004)
        box("Griff", 0, -D / 2 - 0.04, 0.6, W - 0.12, 0.02, 0.02, m['edelstahl'], 0.006)
    elif k == "spuele":
        box("Unterschrank", 0, 0, 0.1, W, D - 0.04, 0.76, m['lack_weiss'], 0.002)
        box("Sockel", 0, 0.03, 0, W, D - 0.06, 0.1, m['kunststoff_schwarz'], 0.002)
        pl = box("Arbeitsplatte", 0, 0, 0.86, W, D, 0.04, m['arbeitsplatte'], 0.003)
        basin("Spülbecken", 0, -0.02, 0.72, W - 0.14, D - 0.2, 0.18, 0.012, m['edelstahl'])  # eingelassen, Rand bündig mit der Platte (90 cm)
        cutout(pl, 0, -0.02, 0.84, W - 0.16, D - 0.22, 0.1)
        cyl("Armatur", 0, D / 2 - 0.08, 0.9, 0.022, 0.26, m['chrom'], 32)
    elif k == "esstisch":
        box("Platte", 0, 0, H - 0.04, W, D, 0.04, m['eiche'], 0.006)
        box("Zarge", 0, 0, H - 0.12, W - 0.2, D - 0.2, 0.08, m['eiche'], 0.003)
        legs("", W, D, 0.08, H - 0.04, 0.03, m['eiche_stab'], False)
    elif k == "stuhl":
        legs("", W, D, 0.03, 0.45, 0.014, m['eiche_stab'])
        box("Sitz", 0, 0, 0.45, W, D, 0.03, m['eiche'], 0.006)
        cushion("Polster", 0, -0.01, 0.48, W - 0.04, D - 0.06, 0.03, m['stoff_beige'], 0.4)
        for s in (-1, 1): cyl(f"Lehnenstab{s}", s * (W / 2 - 0.03), D / 2 - 0.03, 0.45, 0.014, H - 0.45, m['eiche_stab'], 16)
        box("Lehne", 0, D / 2 - 0.03, H - 0.22, W, 0.025, 0.2, m['eiche'], 0.006).rotation_euler[0] = math.radians(-6)
    elif k == "badewanne":
        basin("Wanne", 0, 0, 0, W, D, H, 0.06, m['keramik'])
        cyl("Ablauf", -W / 2 + 0.2, 0, 0.065, 0.03, 0.004, m['chrom'], 32, 0)
        cyl("Armatur", -W / 2 + 0.05, 0, H, 0.02, 0.18, m['chrom'], 32)
    elif k == "dusche":
        box("Duschtasse", 0, 0, 0, W, D, 0.04, m['fliese'], 0.008)
        for s, (gx, gy, gw, gd) in enumerate([(0, -D / 2 + 0.004, W - 0.01, 0.008), (-W / 2 + 0.004, 0, 0.008, D - 0.01)]):
            box(f"Glas{s}", gx, gy, 0.04, gw, gd, H - 0.04, m['glas'], 0.002)
        box("Profil", 0, -D / 2 + 0.004, H - 0.02, W - 0.01, 0.012, 0.02, m['chrom'], 0.002)
        cyl("Stange", W / 2 - 0.08, D / 2 - 0.05, 1.0, 0.012, 1.0, m['chrom'], 24)
        ellipsoid("Brause", W / 2 - 0.08, D / 2 - 0.12, H - 0.05, 0.11, 0.11, 0.012, m['chrom'])
    elif k == "wc":
        box("Spülkasten", 0, D / 2 - 0.09, 0.0, W - 0.02, 0.18, H, m['keramik'], 0.03, 4)
        bowl = ellipsoid("Schüssel", 0, -0.04, 0.29, W / 2, D / 2 - 0.08, 0.13, m['keramik'])
        cyl("Fuß", 0, 0.0, 0.0, 0.13, 0.3, m['keramik'], 48, 0.01, 0.16)
        ellipsoid("Deckel", 0, -0.04, 0.41, W / 2 - 0.01, D / 2 - 0.09, 0.015, m['kunststoff'])
        box("Drücker", 0, D / 2 - 0.09, H, 0.18, 0.1, 0.006, m['chrom'], 0.002)
    elif k == "waschbecken":
        box("Unterschrank", 0, 0.03, 0.3, W, D - 0.06, 0.42, m['eiche'], 0.004)
        basin("Becken", 0, 0, 0.72, W, D, 0.13, 0.015, m['keramik'])
        cyl("Armatur", 0, D / 2 - 0.06, 0.85, 0.018, 0.18, m['chrom'], 32)
    elif k == "waschmaschine":
        box("Gehäuse", 0, 0, 0, W, D, H, m['kunststoff'], 0.012)
        cyl("Bullauge", 0, -D / 2 - 0.02, 0.0, 0.2, 0.04, m['chrom'], 64).rotation_euler[0] = math.radians(90)
        _parts[-1].location = (0, -D / 2 + 0.005, H * 0.42)
        cyl("Glas", 0, -D / 2 - 0.02, 0.0, 0.16, 0.03, m['glas'], 64).rotation_euler[0] = math.radians(90)
        _parts[-1].location = (0, -D / 2, H * 0.42)
        box("Bedienfeld", 0, -D / 2 - 0.002, H - 0.12, W - 0.06, 0.006, 0.08, m['kunststoff'], 0.002)
    elif k == "schreibtisch":
        box("Platte", 0, 0, H - 0.025, W, D, 0.025, m['eiche'], 0.004)
        for s in (-1, 1): box(f"Wange{s}", s * (W / 2 - 0.05), 0, 0, 0.05, D - 0.06, H - 0.025, m['metall_schwarz'], 0.004)
        box("Bildschirm", 0, D / 2 - 0.15, H + 0.12, 0.62, 0.02, 0.36, m['bildschirm'], 0.004)
        box("Monitorfuß", 0, D / 2 - 0.13, H, 0.2, 0.16, 0.012, m['metall_schwarz'], 0.003)
        box("Tastatur", 0, -0.12, H, 0.44, 0.13, 0.015, m['kunststoff_schwarz'], 0.003)
    elif k == "buerostuhl":
        for i in range(5):
            a = i * 2 * math.pi / 5; box(f"Fußstern{i}", math.cos(a) * 0.16, math.sin(a) * 0.16, 0.06, 0.32, 0.04, 0.03, m['kunststoff_schwarz'], 0.008, rot=a)
            ellipsoid(f"Rolle{i}", math.cos(a) * 0.3, math.sin(a) * 0.3, 0.03, 0.025, 0.02, 0.03, m['kunststoff_schwarz'])
        cyl("Gaslift", 0, 0, 0.08, 0.025, 0.36, m['chrom'], 32)
        cushion("Sitz", 0, 0, 0.44, W - 0.08, D - 0.1, 0.07, m['stoff_grau'], 0.3)
        cushion("Lehne", 0, D / 2 - 0.08, 0.6, W - 0.12, 0.06, H - 0.6, m['stoff_grau'], 0.4, 'y').rotation_euler[0] = math.radians(-10)
    elif k == "treppe":
        n = max(3, round(H / 0.18)); rise = H / n; run = D / n
        for i in range(n): box(f"Stufe{i}", 0, -D / 2 + run * (i + 0.5), rise * (i + 1) - 0.04, W, run + 0.03, 0.04, m['eiche'], 0.004)
        for s in (-1, 1):  # Wangen entlang der Schräge (Länge = Hypotenuse), unter den Stufenkanten
            w = box(f"Wange{s}", s * (W / 2 + 0.02), 0, 0, 0.04, math.hypot(D, H), 0.3, m['lack_weiss'], 0.003)
            w.rotation_euler[0] = math.atan2(H, D); w.location = (s * (W / 2 + 0.02), 0, H / 2 - 0.05)
    else:
        box(k, 0, 0, 0, W, D, H, m['eiche'], 0.005)
    root = bpy.data.objects.new(f"Moebel_{k}", None); bpy.context.scene.collection.objects.link(root)
    for p in _parts + [o for o in bpy.data.objects if o.get("trendo_helper") and o.parent is None]:
        if p.parent is None: p.parent = root
    root["trendo_type"] = k
    return root

def catalog():
    """Katalog wie in FurnitureCatalog (C#): Typ, Breite, Tiefe, Höhe in cm."""
    return [("sofa", 220, 90, 85), ("sessel", 80, 85, 90), ("couchtisch", 110, 60, 45), ("tv", 180, 45, 50), ("regal", 80, 35, 190), ("pflanze", 40, 40, 120),
            ("teppich", 200, 140, 1), ("bett", 90, 200, 45), ("doppelbett", 180, 200, 50), ("schrank", 200, 60, 220), ("kommode", 120, 45, 85), ("nachttisch", 45, 40, 50),
            ("kuechenzeile", 270, 60, 90), ("kuehlschrank", 60, 65, 185), ("herd", 60, 60, 90), ("spuele", 80, 60, 90), ("esstisch", 160, 90, 75), ("stuhl", 45, 50, 90),
            ("badewanne", 170, 75, 60), ("dusche", 90, 90, 200), ("wc", 40, 65, 80), ("waschbecken", 60, 45, 85), ("waschmaschine", 60, 60, 85),
            ("schreibtisch", 140, 70, 75), ("buerostuhl", 60, 60, 110), ("treppe", 100, 280, 275)]

def export_previews(out_dir):
    """Vorschau-Modelle (GLB, leichte Netze, mittlere Materialfarben) für die 3D-Ansicht in Trendo schreiben.
    Aufruf aus Blender: import trendo_moebel as tm; tm.export_previews(r"…/Assets/moebel")."""
    import os, io, contextlib
    global QUALITY
    old, QUALITY = QUALITY, "preview"
    try:
        view = bpy.context.view_layer; sizes = {}
        mats = M()
        for mt in bpy.data.materials:  # glTF übernimmt keine prozeduralen Muster: mittlere Farbe einsetzen
            b = next((n for n in mt.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None) if mt.use_nodes else None
            if b and b.inputs['Base Color'].is_linked:
                for l in list(b.inputs['Base Color'].links): mt.node_tree.links.remove(l)
                b.inputs['Base Color'].default_value = mt.diffuse_color
        for k, w, d, h in catalog():
            root = build(k, w / 100, d / 100, h / 100, mats)
            objs = [o for o in [root] + list(root.children_recursive) if not o.hide_render]
            for o in view.objects: o.select_set(False)
            for o in objs: o.select_set(True)
            view.objects.active = root
            path = os.path.join(out_dir, f"{k}.glb")
            with contextlib.redirect_stdout(io.StringIO()):
                bpy.ops.export_scene.gltf(filepath=path, use_selection=True, export_apply=True, export_yup=True, export_materials='EXPORT')
            sizes[k] = os.path.getsize(path)
            for n in [o.name for o in [root] + list(root.children_recursive)] + [o.name for o in bpy.data.objects if o.get("trendo_helper")]:
                ob = bpy.data.objects.get(n)
                if ob: bpy.data.objects.remove(ob, do_unlink=True)
            for me in [x for x in bpy.data.meshes if x.users == 0]: bpy.data.meshes.remove(me)
        for mt in [x for x in bpy.data.materials if x.users == 0]: bpy.data.materials.remove(mt)
        return sizes
    finally:
        QUALITY = old
