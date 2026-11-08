# Trendo - 3D-Anprobe (SPEC-08): Schnittteile aus Trendo zusammennaehen und mit Blender-Stoffphysik auf einer Puppe nach Mass fallen lassen.
# Aufruf: blender --python trendo_anprobe.py -- auftrag.json (Fenster bleibt sichtbar, Blender schliesst sich am Ende selbst)
# Auftrag (JSON): kind, body{height,chest,waist,hip,women}, parts[], seams[], fabric{...}, out, frames, samples.
import json
import math
import sys

import bpy
import bmesh
from mathutils import Vector
from mathutils.geometry import delaunay_2d_cdt

H_CM = 1.25  # Maschenweite des Stoffnetzes in cm


def clear_scene():
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj)
    for block in (bpy.data.meshes, bpy.data.materials, bpy.data.lights, bpy.data.cameras, bpy.data.images):
        for item in list(block):
            block.remove(item)


def ellipse_axes(girth_cm, ratio):
    """Halbachsen in Metern so, dass der Umfang (Ramanujan) dem gemessenen Koerpermass entspricht."""
    a, b = 1.0, ratio
    h = ((a - b) / (a + b)) ** 2
    unit = math.pi * (a + b) * (1 + 3 * h / (10 + math.sqrt(4 - 3 * h)))
    a = girth_cm / unit / 100.0
    return a, a * ratio


def catmull(keys, t):
    """Weiche Interpolation durch die Stuetzstellen; keys = [(t, wert...)]."""
    if t <= keys[0][0]:
        return keys[0][1:]
    if t >= keys[-1][0]:
        return keys[-1][1:]
    for i in range(len(keys) - 1):
        if keys[i][0] <= t <= keys[i + 1][0]:
            p0 = keys[max(i - 1, 0)]
            p1 = keys[i]
            p2 = keys[i + 1]
            p3 = keys[min(i + 2, len(keys) - 1)]
            u = (t - p1[0]) / (p2[0] - p1[0])
            out = []
            for k in range(1, len(p1)):
                out.append(0.5 * (2 * p1[k] + (-p0[k] + p2[k]) * u + (2 * p0[k] - 5 * p1[k] + 4 * p2[k] - p3[k]) * u * u + (-p0[k] + 3 * p1[k] - 3 * p2[k] + p3[k]) * u * u * u))
            return tuple(out)
    return keys[-1][1:]


def loft(name, rings, segments=48):
    """rings: Liste von (mitte: Vector, x-Achse: Vector, y-Achse: Vector); Enden werden geschlossen."""
    verts = []
    faces = []
    for centre, ax, ay in rings:
        for s in range(segments):
            ang = 2 * math.pi * s / segments
            verts.append(centre + ax * math.cos(ang) + ay * math.sin(ang))
    for r in range(len(rings) - 1):
        for s in range(segments):
            a = r * segments + s
            b = r * segments + (s + 1) % segments
            c = (r + 1) * segments + (s + 1) % segments
            d = (r + 1) * segments + s
            faces.append((a, b, c, d))
    faces.append(tuple(range(segments - 1, -1, -1)))
    faces.append(tuple(range((len(rings) - 1) * segments, len(rings) * segments)))
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata([tuple(v) for v in verts], [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return obj


def body_frame(body):
    h = body["height"] / 100.0
    shoulder_half = (body["chest"] / 8.0 + 8.5) / 100.0
    return {
        "h": h,
        "shoulder_x": shoulder_half,
        "shoulder_z": 0.815 * h,
        "neck_z": 0.86 * h,
        "waist_z": 0.625 * h,
        "hip_z": 0.53 * h,
    }


def build_mannequin(body):
    """Schneiderpuppe aus gemessenen Umfaengen: Brust, Taille und Huefte stimmen mit den Eingaben ueberein."""
    f = body_frame(body)
    h = f["h"]
    chest, waist, hip = body["chest"], body["waist"], body["hip"]
    women = body.get("women", True)
    chest_ratio = 0.72 if women else 0.68
    a_c, b_c = ellipse_axes(chest, chest_ratio)
    a_w, b_w = ellipse_axes(waist, 0.76)
    a_h, b_h = ellipse_axes(hip, 0.74)
    a_s, b_s = f["shoulder_x"] * 0.86, b_c * 0.62
    a_n, b_n = ellipse_axes(body["chest"] * 0.36, 0.95)
    keys = [
        (0.455, a_h * 0.9, b_h * 0.92, 0.0),
        (0.53, a_h, b_h, 0.004),
        (0.58, a_h * 0.96 + a_w * 0.04, b_h * 0.94, 0.002),
        (0.625, a_w, b_w, 0.0),
        (0.72, a_c, b_c, 0.0),
        (0.79, a_s * 1.02, b_s * 1.12, 0.0),
        (0.815, a_s * 0.92, b_s * 0.98, 0.002),
        (0.835, a_s * 0.55, b_s * 0.8, 0.005),
        (0.85, a_n * 1.08, b_n * 1.05, 0.006),
        (0.895, a_n, b_n, 0.012),
    ]
    parts = []
    rings = []
    t = keys[0][0]
    while t <= keys[-1][0] + 1e-6:
        a, b, off = catmull(keys, t)
        rings.append((Vector((0, off, t * h)), Vector((max(a, 0.01), 0, 0)), Vector((0, max(b, 0.01), 0))))
        t += 0.004
    parts.append(loft("Rumpf", rings))
    # Beine
    for sign in (-1, 1):
        rings = []
        hip_r = hip / (2 * math.pi) / 100.0
        leg_keys = [
            (0.50, 0.58, sign * a_h * 0.48),
            (0.30, 0.37, sign * a_h * 0.46),
            (0.285, 0.36, sign * a_h * 0.45),
            (0.05, 0.22, sign * a_h * 0.42),
            (0.0, 0.20, sign * a_h * 0.42),
        ]
        keys_asc = sorted(leg_keys, key=lambda k: k[0])
        z = 0.0
        while z <= 0.50 + 1e-6:
            girth_f, x = catmull(keys_asc, z)
            radius = hip_r * 2 * math.pi * girth_f / (2 * math.pi)
            rings.append((Vector((x, 0, z * h)), Vector((radius, 0, 0)), Vector((0, radius, 0))))
            z += 0.01
        parts.append(loft("Bein", rings, 32))
    # Arme (A-Haltung, 32 Grad vom Koerper)
    for sign in (-1, 1):
        angle = math.radians(32)
        axis = Vector((sign * math.sin(angle), 0, -math.cos(angle)))
        side = Vector((sign * math.cos(angle), 0, math.sin(angle)))
        start = Vector((sign * f["shoulder_x"] * 0.8, 0, f["shoulder_z"] - 0.018))
        length = 0.36 * h
        arm_keys = [(0.0, 0.14), (0.035, 0.26), (0.09, 0.325), (0.16, 0.335), (0.26, 0.315), (0.5, 0.255), (0.78, 0.2), (0.96, 0.165), (1.0, 0.12)]
        rings = []
        t = 0.0
        while t <= 1.0 + 1e-6:
            girth_f = catmull(arm_keys, t)[0]
            radius = chest * girth_f / (2 * math.pi) / 100.0
            rings.append((start + axis * (length * t), side * radius, Vector((0, radius * 0.94, 0))))
            t += 0.02
        parts.append(loft("Arm", rings, 32))
    # Zu einem geschlossenen Koerper verschmelzen
    bpy.ops.object.select_all(action="DESELECT")
    for p in parts:
        p.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    obj.name = "Schneiderpuppe"
    obj.data.remesh_voxel_size = 0.005
    obj.data.use_remesh_preserve_volume = True
    bpy.ops.object.voxel_remesh()
    mod = obj.modifiers.new("Glatt", "SMOOTH")
    mod.factor = 0.9
    mod.iterations = 40
    bpy.ops.object.shade_smooth()
    return obj


def resample(points, count):
    """Gleichmaessige Teilung einer Polylinie in count Abschnitte."""
    seg = [(Vector(points[i + 1]) - Vector(points[i])).length for i in range(len(points) - 1)]
    total = sum(seg)
    out = [tuple(points[0])]
    walked = 0.0
    idx = 0
    for n in range(1, count):
        target = total * n / count
        while idx < len(seg) - 1 and walked + seg[idx] < target:
            walked += seg[idx]
            idx += 1
        u = (target - walked) / seg[idx] if seg[idx] > 0 else 0
        a = Vector(points[idx])
        b = Vector(points[idx + 1])
        p = a.lerp(b, u)
        out.append((p.x, p.y))
    out.append(tuple(points[-1]))
    return out


def edge_length(points):
    return sum((Vector(points[i + 1]) - Vector(points[i])).length for i in range(len(points) - 1))


def inside(poly, x, y):
    hit = False
    n = len(poly)
    for i in range(n):
        x1, y1 = poly[i]
        x2, y2 = poly[(i + 1) % n]
        if (y1 > y) != (y2 > y) and x < (x2 - x1) * (y - y1) / (y2 - y1) + x1:
            hit = not hit
    return hit


def dist_to_poly(poly, x, y):
    best = 1e9
    n = len(poly)
    for i in range(n):
        ax, ay = poly[i]
        bx, by = poly[(i + 1) % n]
        dx, dy = bx - ax, by - ay
        l2 = dx * dx + dy * dy
        u = 0.0 if l2 == 0 else max(0.0, min(1.0, ((x - ax) * dx + (y - ay) * dy) / l2))
        best = min(best, math.hypot(x - ax - u * dx, y - ay - u * dy))
    return best


def segment_counts(job):
    """Beide Kanten einer Naht bekommen gleich viele Abschnitte."""
    edges = {}
    for part in job["parts"]:
        for edge in part["edges"]:
            edges[(part["name"], edge["name"])] = edge["pts"]
    counts = {}
    for seam in job["seams"]:
        a = (seam["a"], seam["ea"])
        b = (seam["b"], seam["eb"])
        n = max(2, math.ceil(max(edge_length(edges[a]), edge_length(edges[b])) / H_CM))
        counts[a] = n
        counts[b] = n
    return counts


def wrap(role, x, y, part_top, body, half_width):
    """Startlage rund um den Koerper (in Metern); die Ruhelaenge bleibt der flache Schnitt."""
    f = body_frame(body)
    if role in ("front", "back"):
        radius = half_width * 2 / math.pi
        theta = x / radius
        sx, sy = 1.3, 1.22
        z = f["neck_z"] - 0.012 - (part_top - y) / 100.0
        px = radius * math.sin(theta) * sx / 100.0
        py = radius * math.cos(theta) * sy / 100.0
        return Vector((px, -py if role == "front" else py, z))
    if role in ("skirtFront", "skirtBack"):
        # Der Bund liegt genau auf der gemessenen Taille, darunter weitet sich die Startform bis ueber die Huefte.
        a_w, b_w = ellipse_axes(body["waist"], 0.76)
        a_h, b_h = ellipse_axes(body["hip"], 0.74)
        waist_half = (body["waist"] + 2) / 4
        depth = part_top - y
        u = min(depth / 19.0, 1.0)
        local_half = waist_half + (half_width - waist_half) * (depth / part_top)
        theta = max(-1.0, min(1.0, x / local_half)) * math.pi / 2
        ax = a_w + 0.004 + (a_h * 1.12 - a_w) * u + max(0.0, depth - 19.0) * 0.0012
        ay = b_w + 0.004 + (b_h * 1.14 - b_w) * u + max(0.0, depth - 19.0) * 0.0012
        z = f["waist_z"] - depth / 100.0
        py = ay * math.cos(theta)
        return Vector((ax * math.sin(theta), -py if role == "skirtFront" else py, z))
    sign = 1 if role == "sleeveR" else -1
    angle = math.radians(32)
    axis = Vector((sign * math.sin(angle), 0, -math.cos(angle)))
    out = Vector((sign * math.cos(angle), 0, math.sin(angle)))
    fwd = Vector((0, -1, 0))
    start = Vector((sign * f["shoulder_x"] * 0.93, 0, f["shoulder_z"] - 0.012))
    radius = half_width / math.pi
    theta = x / radius
    t = (part_top - y) / 100.0
    r = radius * 1.28 / 100.0
    return start + axis * t + out * (r * math.cos(theta)) + fwd * (r * math.sin(theta))


def build_garment(job):
    counts = segment_counts(job)
    all_flat = []
    all_start = []
    all_uv = []
    faces = []
    boundary = {}
    pinned = []
    for part in job["parts"]:
        offset = len(all_flat)
        outline = []
        spans = []
        for edge in part["edges"]:
            key = (part["name"], edge["name"])
            n = counts.get(key, max(1, math.ceil(edge_length(edge["pts"]) / H_CM)))
            pts = resample(edge["pts"], n)
            start = len(outline)
            outline.extend(pts[:-1])
            spans.append((edge["name"], start, n))
        loop = len(outline)
        xs = [p[0] for p in outline]
        ys = [p[1] for p in outline]
        interior = []
        row = 0
        y = min(ys) + H_CM * 0.5
        while y < max(ys):
            x = min(xs) + (H_CM * 0.5 if row % 2 else 0)
            while x < max(xs):
                if inside(outline, x, y) and dist_to_poly(outline, x, y) > H_CM * 0.65:
                    interior.append((x, y))
                x += H_CM
            y += H_CM * 0.866
            row += 1
        coords = [Vector(p) for p in outline + interior]
        cons = [(i, (i + 1) % loop) for i in range(loop)]
        vert_out, _edges, tris, orig_verts, _oe, _of = delaunay_2d_cdt(coords, cons, [], 1, 1e-6, True)
        remap = {}
        for new_index, sources in enumerate(orig_verts):
            for src in sources:
                remap[src] = new_index
        for name, start, n in spans:
            ids = [offset + remap[(start + i) % loop] for i in range(n + 1)]
            boundary[(part["name"], name)] = ids
        half_width = (max(xs) - min(xs)) / 2
        top = max(ys)
        for v in vert_out:
            all_flat.append((v.x / 100.0, v.y / 100.0))
            all_start.append(wrap(part["role"], v.x, v.y, top, job["body"], half_width))
            all_uv.append((v.x / 100.0, v.y / 100.0))
        for tri in tris:
            faces.append(tuple(offset + i for i in tri))
        if part["role"].startswith("skirt"):
            pinned.extend(boundary[(part["name"], "waist")])
    edges = []
    seam_pairs = []
    for seam in job["seams"]:
        a = boundary[(seam["a"], seam["ea"])]
        b = boundary[(seam["b"], seam["eb"])]
        if seam["rev"]:
            b = list(reversed(b))
        for i, j in zip(a, b):
            edges.append((i, j))
            seam_pairs.append((i, j))
    mesh = bpy.data.meshes.new("Kleidung")
    mesh.from_pydata([tuple(p) for p in all_start], edges, faces)
    mesh.update()
    obj = bpy.data.objects.new("Kleidung", mesh)
    bpy.context.collection.objects.link(obj)
    uv = mesh.uv_layers.new(name="Stoff")
    for loop in mesh.loops:
        uv.data[loop.index].uv = all_uv[loop.vertex_index]
    # Formschluessel: Start um den Koerper, Ruhelage = flacher Schnitt
    obj.shape_key_add(name="Start", from_mix=False)
    flat = obj.shape_key_add(name="Schnitt", from_mix=False)
    for i, p in enumerate(all_flat):
        flat.data[i].co = (p[0], 0.0, p[1])
    flat.value = 0.0  # nur Ruhelage der Stoffphysik, nicht sichtbar
    group = obj.vertex_groups.new(name="Bund")
    if pinned:
        group.add(pinned, 1.0, "REPLACE")
    obj["naehte"] = json.dumps(seam_pairs)
    return obj, flat


DEFAULT_FABRIC = {"mass": 0.3, "tension": 15, "compression": 15, "shear": 15, "bending": 0.5, "air": 1.0, "color": [0.8, 0.8, 0.78, 1]}


def add_cloth(garment, body, job, flat):
    fabric = dict(DEFAULT_FABRIC, **job.get("fabric", {}))
    col = body.modifiers.new("Kollision", "COLLISION")
    col.settings.thickness_outer = 0.004
    col.settings.thickness_inner = 0.01
    col.settings.cloth_friction = 6
    cloth = garment.modifiers.new("Stoff", "CLOTH")
    st = cloth.settings
    st.quality = 10
    st.mass = fabric["mass"]
    st.tension_stiffness = fabric["tension"]
    st.compression_stiffness = fabric["compression"]
    st.shear_stiffness = fabric["shear"]
    st.bending_stiffness = fabric["bending"]
    st.air_damping = fabric["air"]
    st.tension_damping = 5
    st.compression_damping = 5
    st.shear_damping = 5
    st.use_sewing_springs = True
    st.sewing_force_max = 6.0
    st.rest_shape_key = flat
    if garment.vertex_groups["Bund"].index is not None and job["kind"] == "rock":
        st.vertex_group_mass = "Bund"
        st.pin_stiffness = 1.0
    cc = cloth.collision_settings
    cc.collision_quality = 4
    cc.distance_min = 0.003
    cc.use_self_collision = True
    cc.self_distance_min = 0.0025
    cc.self_friction = 5
    return cloth


def build(job):
    clear_scene()
    scene = bpy.context.scene
    scene.frame_start = 1
    scene.frame_end = int(job.get("frames", 80))
    scene.render.fps = 24
    body = build_mannequin(job["body"])
    garment, flat = build_garment(job)
    cloth = add_cloth(garment, body, job, flat)
    cloth.point_cache.frame_start = 1
    cloth.point_cache.frame_end = scene.frame_end
    return body, garment


def simulate():
    scene = bpy.context.scene
    for frame in range(scene.frame_start, scene.frame_end + 1):
        scene.frame_set(frame)


def finish_garment(garment):
    """Stoffphysik uebernehmen, Naehte verschweissen, Oberflaeche glaetten."""
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated = garment.evaluated_get(depsgraph)
    mesh = bpy.data.meshes.new_from_object(evaluated, preserve_all_data_layers=True, depsgraph=depsgraph)
    pairs = json.loads(garment["naehte"])
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bm.verts.ensure_lookup_table()
    targetmap = {}
    for a, b in pairs:
        va, vb = bm.verts[a], bm.verts[b]
        if va is vb:
            continue
        mid = (va.co + vb.co) / 2
        va.co = mid
        vb.co = mid
        targetmap[vb] = va
    # Ketten aufloesen, falls eine Kante mehrfach vernaeht ist
    resolved = {}
    for src, dst in targetmap.items():
        seen = {src}
        while dst in targetmap and dst not in seen:
            seen.add(dst)
            dst = targetmap[dst]
        if src is not dst:
            resolved[src] = dst
    bmesh.ops.weld_verts(bm, targetmap=resolved)
    loose = [e for e in bm.edges if not e.link_faces]
    bmesh.ops.delete(bm, geom=loose, context="EDGES")
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    # Flaechen zeigen nach aussen (vom Koerper weg)
    centre = sum((v.co for v in bm.verts), Vector()) / len(bm.verts)
    outward = sum((f.normal.dot(f.calc_center_median() - Vector((0, 0, f.calc_center_median().z))) for f in bm.faces))
    if outward < 0:
        bmesh.ops.reverse_faces(bm, faces=bm.faces)
    bm.normal_update()
    # Saum und Ausschnitt nach innen umschlagen statt roher Schnittkante
    boundary = [e for e in bm.edges if e.is_boundary]
    rim = {v for e in boundary for v in e.verts}
    inward = {}
    for v in rim:
        faces = [f.calc_center_median() - v.co for f in v.link_faces]
        d = sum(faces, Vector())
        inward[v] = d.normalized() if d.length > 0 else Vector()
    made = bmesh.ops.extrude_edge_only(bm, edges=boundary)
    new_verts = [g for g in made["geom"] if isinstance(g, bmesh.types.BMVert)]
    for g in new_verts:
        link = [e.other_vert(g) for e in g.link_edges if e.other_vert(g) in rim]
        if link:
            src = link[0]
            g.co = src.co + inward[src] * 0.009 - src.normal * 0.0016
    bm.normal_update()
    bm.to_mesh(mesh)
    bm.free()
    final = bpy.data.objects.new("Kleidungsstueck", mesh)
    bpy.context.collection.objects.link(final)
    garment.hide_render = True
    garment.hide_viewport = True
    for poly in mesh.polygons:
        poly.use_smooth = True
    final.modifiers.new("Glatt", "SMOOTH").iterations = 3
    final.modifiers.new("Fein", "SUBSURF").levels = 1
    final.modifiers["Fein"].render_levels = 2
    thick = final.modifiers.new("Dicke", "SOLIDIFY")
    thick.thickness = 0.0007
    thick.offset = 1
    return final


def image_node(tree, path, non_color):
    node = tree.nodes.new("ShaderNodeTexImage")
    node.image = bpy.data.images.load(path)
    if non_color:
        node.image.colorspace_settings.name = "Non-Color"
    return node


def fabric_material(job):
    fabric = dict(DEFAULT_FABRIC, **job.get("fabric", {}))
    mat = bpy.data.materials.new("Stoff")
    mat.use_nodes = True
    tree = mat.node_tree
    bsdf = next(n for n in tree.nodes if n.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Base Color"].default_value = fabric["color"]
    bsdf.inputs["Roughness"].default_value = fabric.get("roughness", 0.85)
    bsdf.inputs["Sheen Weight"].default_value = fabric.get("sheen", 0.2)
    bsdf.inputs["Sheen Roughness"].default_value = 0.5
    tex = fabric.get("textures") or {}
    if tex.get("color"):
        coord = tree.nodes.new("ShaderNodeUVMap")
        coord.uv_map = "Stoff"
        mapping = tree.nodes.new("ShaderNodeMapping")
        scale = 1.0 / float(fabric.get("tile_m", 0.2))
        mapping.inputs["Scale"].default_value = (scale, scale, 1)
        tree.links.new(coord.outputs["UV"], mapping.inputs["Vector"])
        color = image_node(tree, tex["color"], False)
        tree.links.new(mapping.outputs["Vector"], color.inputs["Vector"])
        tint = tree.nodes.new("ShaderNodeMixRGB")
        tint.blend_type = "MULTIPLY"
        tint.inputs[0].default_value = 1.0
        tint.inputs[2].default_value = fabric["color"]
        tree.links.new(color.outputs["Color"], tint.inputs[1])
        tree.links.new(tint.outputs[0], bsdf.inputs["Base Color"])
        if tex.get("roughness"):
            rough = image_node(tree, tex["roughness"], True)
            tree.links.new(mapping.outputs["Vector"], rough.inputs["Vector"])
            tree.links.new(rough.outputs["Color"], bsdf.inputs["Roughness"])
        if tex.get("normal"):
            norm = image_node(tree, tex["normal"], True)
            tree.links.new(mapping.outputs["Vector"], norm.inputs["Vector"])
            nm = tree.nodes.new("ShaderNodeNormalMap")
            nm.uv_map = "Stoff"
            nm.inputs["Strength"].default_value = 0.8
            tree.links.new(norm.outputs["Color"], nm.inputs["Color"])
            tree.links.new(nm.outputs["Normal"], bsdf.inputs["Normal"])
    return mat


def studio(job, body):
    scene = bpy.context.scene
    h = job["body"]["height"] / 100.0
    # Puppe: matter, heller Glasfaser-Lack
    doll = bpy.data.materials.new("Puppe")
    doll.use_nodes = True
    d = next(n for n in doll.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    d.inputs["Base Color"].default_value = (0.74, 0.71, 0.66, 1)
    d.inputs["Roughness"].default_value = 0.46
    body.data.materials.append(doll)
    # Hohlkehle als Studiohintergrund, reale Masse: 6 m breit
    verts = []
    prof = []
    for i in range(0, 25):
        ang = math.radians(90 * i / 24)
        prof.append((3.0 + 1.0 * math.sin(ang), 1.0 - 1.0 * math.cos(ang)))
    prof = [(-4.0, 0.0)] + prof + [(4.0, 6.0)]
    for x in (-5, 5):
        for y, z in prof:
            verts.append((x, y, z))
    n = len(prof)
    faces = [(i, i + 1, n + i + 1, n + i) for i in range(n - 1)]
    mesh = bpy.data.meshes.new("Studio")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    wall = bpy.data.objects.new("Studio", mesh)
    bpy.context.collection.objects.link(wall)
    for poly in mesh.polygons:
        poly.use_smooth = True
    paper = bpy.data.materials.new("Hintergrund")
    paper.use_nodes = True
    w = next(x for x in paper.node_tree.nodes if x.type == "BSDF_PRINCIPLED")
    w.inputs["Base Color"].default_value = (0.42, 0.42, 0.42, 1)
    w.inputs["Roughness"].default_value = 0.9
    mesh.materials.append(paper)
    # Softboxen mit realer Groesse; Licht kommt von dort, wo ein Studio es haengt
    def softbox(name, loc, size, power, temp_rgb):
        light = bpy.data.lights.new(name, "AREA")
        light.shape = "RECTANGLE"
        light.size = size[0]
        light.size_y = size[1]
        light.energy = power
        light.color = temp_rgb
        obj = bpy.data.objects.new(name, light)
        obj.location = loc
        direction = Vector((0, 0, 0.62 * h)) - Vector(loc)
        obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
        bpy.context.collection.objects.link(obj)
        obj.visible_camera = False
    softbox("Hauptlicht", (-1.8, -2.4, 2.4), (1.2, 1.8), 150, (1.0, 0.96, 0.9))
    softbox("Aufheller", (2.4, -1.8, 1.4), (1.5, 2.0), 40, (0.92, 0.95, 1.0))
    softbox("Kante", (0.9, 2.3, 2.3), (0.8, 1.6), 110, (1.0, 0.98, 0.95))
    world = bpy.data.worlds.new("Welt")
    world.use_nodes = True
    bg = next((nd for nd in world.node_tree.nodes if nd.type == "BACKGROUND"), None)
    if bg is None:
        bg = world.node_tree.nodes.new("ShaderNodeBackground")
        out = next(nd for nd in world.node_tree.nodes if nd.type == "OUTPUT_WORLD")
        world.node_tree.links.new(bg.outputs[0], out.inputs[0])
    bg.inputs[0].default_value = (0.5, 0.5, 0.5, 1)
    bg.inputs[1].default_value = 0.08
    scene.world = world
    cam_data = bpy.data.cameras.new("Kamera")
    cam_data.lens = 85
    cam_data.sensor_width = 36
    camera = bpy.data.objects.new("Kamera", cam_data)
    bpy.context.collection.objects.link(camera)
    scene.camera = camera
    r = scene.render
    r.engine = "CYCLES"
    r.resolution_x = int(job.get("width", 1200))
    r.resolution_y = int(job.get("height", 1500))
    r.image_settings.file_format = "PNG"
    scene.cycles.samples = int(job.get("samples", 256))
    scene.cycles.use_denoising = True
    scene.view_settings.view_transform = "AgX"
    try:
        prefs = bpy.context.preferences.addons["cycles"].preferences
        for backend in ("OPTIX", "CUDA", "HIP", "ONEAPI", "METAL"):
            try:
                prefs.compute_device_type = backend
            except TypeError:
                continue
            prefs.get_devices()
            if any(dev.type == backend for dev in prefs.devices):
                for dev in prefs.devices:
                    dev.use = dev.type == backend
                scene.cycles.device = "GPU"
                break
    except (KeyError, AttributeError, RuntimeError):
        scene.cycles.device = "CPU"
    return camera


def aim(camera, job, degrees):
    """Kamera mit realer Portraetbrennweite; Mitte des Kleidungsstuecks im Bild."""
    h = job["body"]["height"] / 100.0
    centre = Vector((0, 0, 0.7 * h if job["kind"] == "tshirt" else 0.47 * h))
    distance = 2.9
    ang = math.radians(degrees)
    camera.location = centre + Vector((math.sin(ang) * distance, -math.cos(ang) * distance, 0.08))
    camera.rotation_euler = (centre - camera.location).to_track_quat("-Z", "Y").to_euler()


def main():
    argv = sys.argv
    job = json.load(open(argv[argv.index("--") + 1], encoding="utf-8"))
    body, garment = build(job)
    simulate()
    final = finish_garment(garment)
    final.data.materials.append(fabric_material(job))
    camera = studio(job, body)
    views = {"vorn": 0, "schraeg": 35, "hinten": 180}
    for name, degrees in views.items():
        aim(camera, job, degrees)
        bpy.context.scene.render.filepath = job["out"] + "-" + name + ".png"
        bpy.ops.render.render(write_still=True)
    print("TRENDO_FERTIG", flush=True)
    bpy.ops.wm.quit_blender()


if __name__ == "__main__" and "--" in sys.argv:
    main()
