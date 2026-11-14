using System.Text;
using System.Text.Json;

namespace Trendo.Core.Programs;

/// <summary>T-0660, M3 und M4: Blender ohne Fenster bereitet Mixamo-Dateien auf.
/// M3 „rigger“: eigene Figur (GLB, glTF, OBJ, FBX oder .blend) ohne Skelett, Transformationen angewendet, Füße auf dem Boden, als FBX für den Auto-Rigger.
/// M4 „prep“: Knochennamen auf „mixamorig:“ angleichen (mixamorig1:, mixamorig2: …), Maßstab auf Meter mit Objektmaßstab 1 (auch die Bewegungskurven),
/// eigener Wurzelknochen „root“, Laufen auf der Stelle oder Wurzelbewegung auf „root“, Übertragen auf die Spielfigur (Drehungen mit Ausgleich der Ruhehaltung,
/// Hüfthöhe nach Größe), Export je Aktion als FBX für Unreal (ein Wurzelknochen, ohne Blattknochen) und eine GLB mit allen Aktionen für Godot.
/// Bericht als JSON mit Messwerten; nichts wird geschätzt.</summary>
public static class MixamoBlender
{
    public const string InPlace = "inplace", RootMotion = "root";

    public static List<string> Args(string script, IEnumerable<string> rest) =>
        ["--background", "--factory-startup", "--python-exit-code", "1", "--python", script, "--", .. rest];

    public const string Script = """
import bpy, sys, os, re, json, math
from mathutils import Matrix, Vector, Quaternion

argv = sys.argv[sys.argv.index("--") + 1:]
mode = argv[0]

def clear():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.unit_settings.system = "METRIC"; bpy.context.scene.unit_settings.scale_length = 1.0

def load(path):
    ext = os.path.splitext(path)[1].lower()
    before = set(bpy.data.objects)
    if ext == ".fbx": bpy.ops.import_scene.fbx(filepath=path, automatic_bone_orientation=False, ignore_leaf_bones=False)  # Endknochen behalten: ohne sie fehlen Kopf und Füße, wenn eine Datei keine „_End“-Knochen hat
    elif ext in (".glb", ".gltf"): bpy.ops.import_scene.gltf(filepath=path)
    elif ext == ".obj": bpy.ops.wm.obj_import(filepath=path)
    elif ext == ".blend":
        with bpy.data.libraries.load(path, link=False) as (src, dst): dst.objects = list(src.objects)
        for o in dst.objects:
            if o is not None: bpy.context.scene.collection.objects.link(o)
    else: raise RuntimeError("Format nicht unterstützt: " + ext)
    return [o for o in bpy.data.objects if o not in before]

def height(objs):
    lo, hi = 1e9, -1e9
    bpy.context.view_layer.update()
    for o in objs:
        if o.type != "MESH": continue
        for c in o.bound_box:
            z = (o.matrix_world @ Vector(c)).z; lo = min(lo, z); hi = max(hi, z)
    return (hi - lo) if hi > lo else 0.0, lo

def select(objs):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs: o.select_set(True)
    if objs: bpy.context.view_layer.objects.active = objs[0]

# ───────────── M3: Figur für den Auto-Rigger ─────────────
if mode == "rigger":
    src, out, report = argv[1], argv[2], argv[3]
    clear(); objs = load(src)
    for o in [o for o in objs if o.type == "ARMATURE"]:
        for m in [m for m in o.children if m.type == "MESH"]:
            mat = m.matrix_world.copy(); m.parent = None; m.matrix_world = mat
        bpy.data.objects.remove(o)
    meshes = [o for o in bpy.data.objects if o.type == "MESH"]
    if not meshes: raise RuntimeError("Keine Figur (kein Mesh) in der Datei")
    for m in meshes:
        for md in [md for md in m.modifiers if md.type == "ARMATURE"]: m.modifiers.remove(md)
        for v in list(m.vertex_groups): m.vertex_groups.remove(v)
    select(meshes); bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    h, lo = height(meshes)
    # Füße auf den Boden, Mitte über den Ursprung (Mixamo erwartet eine stehende Figur)
    xs = [(m.matrix_world @ Vector(c)).x for m in meshes for c in m.bound_box]; ys = [(m.matrix_world @ Vector(c)).y for m in meshes for c in m.bound_box]
    off = Vector(((min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2, lo))
    for m in meshes: m.location -= off
    bpy.ops.object.transform_apply(location=True, rotation=False, scale=False)
    os.makedirs(os.path.dirname(out) or ".", exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=out, use_selection=True, object_types={"MESH"}, apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS", mesh_smooth_type="FACE", add_leaf_bones=False, path_mode="COPY", embed_textures=True, bake_anim=False)
    tris = sum(sum(len(p.vertices) - 2 for p in m.data.polygons) for m in meshes)
    with open(report, "w", encoding="utf-8") as f: json.dump({"mode": mode, "file": out, "meshes": len(meshes), "triangles": tris, "height": round(h, 4)}, f)
    print("TRENDO_MIXAMO ok"); sys.exit(0)

# ───────────── M4: Animationen aufbereiten ─────────────
figure, outdir, motion, report, anims = argv[1], argv[2], argv[3], argv[4], argv[5:]
PREFIX = re.compile(r"^mixamorig\d*[:_]")

def norm(name): return PREFIX.sub("mixamorig:", name)

def arm_of(objs):
    a = [o for o in objs if o.type == "ARMATURE"]
    if not a: raise RuntimeError("Kein Skelett in der Datei")
    return a[0]

def fix_names(arm):
    n = 0
    for b in arm.data.bones:
        new = norm(b.name)
        if new != b.name: b.name = new; n += 1   # Blender benennt die Kurven der Aktion mit um
    return n

def fcurves(act):
    # Blender 5: Kurven liegen in Ebenen/Streifen/Kanalbeuteln (Slotted Actions); ältere Blender haben act.fcurves
    if hasattr(act, "fcurves"): return list(act.fcurves)
    return [fc for l in act.layers for s in l.strips for cb in s.channelbags for fc in cb.fcurves]

def to_meters(arm, meshes):
    s = arm.scale.x
    act = arm.animation_data.action if arm.animation_data else None
    # Meshes erst in Weltlage festschreiben (Mixamo: Skelett 0,01, Mesh oft 100), dann das Skelett, dann wieder anhängen
    for m in meshes:
        w = m.matrix_world.copy(); m.parent = None; m.matrix_world = w
    if meshes:
        select(meshes); bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    select([arm]); bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    for m in meshes:
        m.parent = arm; m.matrix_parent_inverse = arm.matrix_world.inverted()
    if act and abs(s - 1.0) > 1e-6:
        for fc in fcurves(act):
            if fc.data_path.endswith(".location"):
                for k in fc.keyframe_points: k.co.y *= s; k.handle_left.y *= s; k.handle_right.y *= s
    return s

def hips_of(arm):
    for b in arm.pose.bones:
        if b.name == "mixamorig:Hips": return b
    return next(b for b in arm.pose.bones if b.parent is None)

def add_root(arm):
    bpy.context.view_layer.objects.active = arm; bpy.ops.object.mode_set(mode="EDIT")
    eb = arm.data.edit_bones; hips = eb.get("mixamorig:Hips") or next(b for b in eb if b.parent is None)
    r = eb.get("root") or eb.new("root"); r.head = (0, 0, 0); r.tail = (0, 0.25, 0); r.roll = 0
    hips.parent = r; hips.use_connect = False
    bpy.ops.object.mode_set(mode="OBJECT")

def frames(act): s, e = act.frame_range; return int(round(s)), int(round(e))

def bake_root(arm, act, keep_root):
    # Weg der Hüfte in der Ebene: bei „root“ auf den Wurzelknochen, bei „inplace“ entfernt; die Höhe bleibt immer an der Hüfte
    s, e = frames(act); sc = bpy.context.scene
    hips = hips_of(arm); world = []
    for f in range(s, e + 1): sc.frame_set(f); world.append((arm.matrix_world @ hips.matrix).copy())
    add_root(arm); hips = hips_of(arm); root = arm.pose.bones["root"]
    root.rotation_mode = "QUATERNION"
    p0 = world[0].translation; before = (world[-1].translation - p0).xy.length
    for i, f in enumerate(range(s, e + 1)):
        sc.frame_set(f)
        d = world[i].translation - p0; d = Vector((d.x, d.y, 0.0))
        root.matrix_basis = Matrix.Translation(d) if keep_root else Matrix.Identity(4)
        root.keyframe_insert("location", frame=f); root.keyframe_insert("rotation_quaternion", frame=f)
        bpy.context.view_layer.update()
        hips.matrix = arm.matrix_world.inverted() @ (world[i] if keep_root else Matrix.Translation(-d) @ world[i])
        hips.keyframe_insert("location", frame=f); hips.keyframe_insert("rotation_quaternion", frame=f)
    sc.frame_set(e); bpy.context.view_layer.update()
    drift = ((arm.matrix_world @ hips.matrix).translation - (arm.matrix_world @ root.matrix).translation - (world[0].translation - p0) - p0).xy.length
    return round(before, 4), round(drift, 4)

def retarget(src, dst, act):
    # Drehungen in Weltlage mit Ausgleich der Ruhehaltung; Hüfte nach Größenverhältnis; nur Knochen mit gleichem Namen
    s, e = frames(act); sc = bpy.context.scene
    names = [b.name for b in dst.data.bones if b.name in src.data.bones]
    rs = {n: (src.matrix_world @ src.data.bones[n].matrix_local).to_quaternion() for n in names}
    rt = {b.name: (dst.matrix_world @ b.matrix_local).to_quaternion() for b in dst.data.bones}
    hs, ht = src.data.bones.get("mixamorig:Hips"), dst.data.bones.get("mixamorig:Hips")
    ratio = (ht.head_local.z / hs.head_local.z) if hs and ht and hs.head_local.z > 1e-4 else 1.0
    new = bpy.data.actions.new(act.name); dst.animation_data_create(); dst.animation_data.action = new
    order = [b for b in dst.data.bones]  # Blender liefert Eltern vor Kindern
    for f in range(s, e + 1):
        sc.frame_set(f)
        q = {}
        for b in order:
            pb = dst.pose.bones[b.name]; pb.rotation_mode = "QUATERNION"
            if b.name in rs: q[b.name] = (src.matrix_world @ src.pose.bones[b.name].matrix).to_quaternion() @ rs[b.name].inverted() @ rt[b.name]
            else: q[b.name] = (q[b.parent.name] if b.parent else Quaternion()) @ (rt[b.parent.name].inverted() @ rt[b.name] if b.parent else rt[b.name])
            if b.parent: basis = rt[b.name].inverted() @ rt[b.parent.name] @ q[b.parent.name].inverted() @ q[b.name]
            else: basis = rt[b.name].inverted() @ q[b.name]
            pb.rotation_quaternion = basis; pb.keyframe_insert("rotation_quaternion", frame=f)
        for n in ("root", "mixamorig:Hips"):
            if n in dst.pose.bones and n in src.pose.bones:
                bpy.context.view_layer.update()
                w = src.matrix_world @ src.pose.bones[n].matrix
                pos = w.translation * ratio
                dst.pose.bones[n].matrix = dst.matrix_world.inverted() @ Matrix.LocRotScale(pos, (dst.matrix_world @ dst.pose.bones[n].matrix).to_quaternion(), None)
                dst.pose.bones[n].keyframe_insert("location", frame=f)
    return new, len(names), len(dst.data.bones), round(ratio, 4)

def export_fbx(path, objs, act):
    # Blender 5: nur ohne NLA-Spuren und mit Anfangs-/Endschlüssel landet die Aktion in der FBX; der Bildbereich muss genau die Aktion sein
    s, e = frames(act); bpy.context.scene.frame_start = s; bpy.context.scene.frame_end = e
    select(objs)
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"ARMATURE", "MESH"}, apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
        add_leaf_bones=False, primary_bone_axis="Y", secondary_bone_axis="X", armature_nodetype="NULL", bake_anim=True, bake_anim_use_all_actions=False,
        bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0, mesh_smooth_type="FACE", path_mode="COPY", embed_textures=True)

clear()
os.makedirs(outdir, exist_ok=True)
fig_arm = None; fig_meshes = []; fig_height = 0.0
if figure:
    objs = load(figure); fig_arm = arm_of(objs); fig_meshes = [o for o in objs if o.type == "MESH"]
    fix_names(fig_arm)
    if fig_arm.animation_data: fig_arm.animation_data.action = None
    to_meters(fig_arm, fig_meshes); add_root(fig_arm)
    fig_height, _ = height(fig_meshes)
results = []; actions = []
for spec in anims:
    action_id, path = spec.split("=", 1)
    objs = load(path); arm = arm_of(objs); meshes = [o for o in objs if o.type == "MESH"]
    renamed = fix_names(arm)
    act = arm.animation_data.action if arm.animation_data and arm.animation_data.action else None
    if act is None: raise RuntimeError("Keine Animation in " + os.path.basename(path))
    scale = to_meters(arm, meshes)
    fps = bpy.context.scene.render.fps
    travel, drift = bake_root(arm, act, motion == "root")
    act.name = action_id
    entry = {"action": action_id, "source": os.path.basename(path), "renamed": renamed, "scaleFromFile": round(scale, 6), "frames": frames(act)[1] - frames(act)[0] + 1, "fps": fps, "travel": travel, "drift": drift}
    if fig_arm is not None:
        new, matched, total, ratio = retarget(arm, fig_arm, act)
        new.name = action_id; new.use_fake_user = True; actions.append(new)
        entry.update({"matched": matched, "bones": total, "heightRatio": ratio})
        target = [fig_arm] + fig_meshes
    else:
        act.use_fake_user = True; actions.append(act)
        entry.update({"matched": len(arm.data.bones), "bones": len(arm.data.bones), "heightRatio": 1.0})
        target = [arm] + meshes
        h, _ = height(meshes); entry["height"] = round(h, 4)
    out = os.path.join(outdir, action_id + ".fbx"); export_fbx(out, target, actions[-1]); entry["fbx"] = out
    results.append(entry)
    if fig_arm is None and spec != anims[-1]:
        for o in objs: bpy.data.objects.remove(o)
# Godot: eine GLB mit allen Aktionen
glb = os.path.join(outdir, "mixamo-godot.glb")
holder = fig_arm if fig_arm is not None else arm_of(list(bpy.data.objects))
holder.animation_data_create()
for a in actions:
    tr = holder.animation_data.nla_tracks.new(); tr.name = a.name; tr.strips.new(a.name, int(a.frame_range[0]), a)
holder.animation_data.action = None
select([holder] + [o for o in bpy.data.objects if o.type == "MESH" and (o.parent == holder or o.find_armature() == holder)])
bpy.ops.export_scene.gltf(filepath=glb, export_format="GLB", use_selection=True, export_animations=True, export_animation_mode="NLA_TRACKS", export_skins=True, export_yup=True)
with open(report, "w", encoding="utf-8") as f:
    json.dump({"mode": mode, "motion": motion, "figureHeight": round(fig_height, 4), "glb": glb, "animations": results}, f)
print("TRENDO_MIXAMO ok")
""";

    public sealed record AnimResult(string Action, string Source, int Renamed, double ScaleFromFile, int Frames, int Fps, double Travel, double Drift, int Matched, int Bones, double HeightRatio, double Height, string Fbx);
    public sealed record PrepReport(string Motion, double FigureHeight, string Glb, IReadOnlyList<AnimResult> Animations);
    public sealed record RiggerReport(string File, int Meshes, int Triangles, double Height);

    public static PrepReport ParsePrep(string json)
    {
        using var d = JsonDocument.Parse(json); var r = d.RootElement;
        static double D(JsonElement e, string k) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : 0;
        static string S(JsonElement e, string k) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
        var list = r.TryGetProperty("animations", out var a) ? a.EnumerateArray().Select(x => new AnimResult(S(x, "action"), S(x, "source"), (int)D(x, "renamed"), D(x, "scaleFromFile"),
            (int)D(x, "frames"), (int)D(x, "fps"), D(x, "travel"), D(x, "drift"), (int)D(x, "matched"), (int)D(x, "bones"), D(x, "heightRatio"), D(x, "height"), S(x, "fbx"))).ToList() : [];
        return new(S(r, "motion"), D(r, "figureHeight"), S(r, "glb"), list);
    }

    public static RiggerReport ParseRigger(string json)
    {
        using var d = JsonDocument.Parse(json); var r = d.RootElement;
        return new(r.GetProperty("file").GetString() ?? "", r.GetProperty("meshes").GetInt32(), r.GetProperty("triangles").GetInt32(), r.GetProperty("height").GetDouble());
    }

    /// <summary>Prüfung des Ergebnisses: Größe eines Menschen, Knochen übertragen, auf der Stelle wirklich ohne Weg. Leer = in Ordnung.</summary>
    public static List<string> Check(PrepReport r)
    {
        var w = new List<string>();
        if (r.Animations.Count == 0) w.Add("Keine Animation aufbereitet.");
        var h = r.FigureHeight > 0 ? r.FigureHeight : r.Animations.Select(a => a.Height).DefaultIfEmpty(0).Max();
        if (h > 0 && (h < 0.5 || h > 3.0)) w.Add($"Figur ist {h:0.00} m groß – Maßstab prüfen (erwartet etwa 1,5 bis 2 m).");
        foreach (var a in r.Animations)
        {
            if (a.Bones > 0 && a.Matched < a.Bones * 0.9) w.Add($"{a.Action}: nur {a.Matched} von {a.Bones} Knochen übertragen.");
            if (r.Motion == InPlace && a.Drift > 0.01) w.Add($"{a.Action}: bewegt sich noch {a.Drift * 100:0.#} cm von der Stelle.");
            if (a.Frames < 2) w.Add($"{a.Action}: nur {a.Frames} Bild – keine Bewegung.");
        }
        return w;
    }

    static string WriteScript()
    {
        var s = Path.Combine(Path.GetTempPath(), "trendo-mixamo-" + Environment.ProcessId + ".py");
        File.WriteAllText(s, Script, new UTF8Encoding(false));
        return s;
    }

    static string Last(string s) => string.Join(" | ", s.Split('\n').Select(x => x.Trim()).Where(x => x.Length > 0).TakeLast(3));

    /// <summary>M3: Figur für den Auto-Rigger als FBX.</summary>
    public static RiggerReport Rigger(string blender, string source, string outFbx, int timeoutMs = 600_000)
    {
        if (!File.Exists(source)) throw new IOException($"„{Path.GetFileName(source)}“ gibt es nicht.");
        var report = Path.ChangeExtension(outFbx, ".bericht.json");
        var r = Cli.Run(blender, Args(WriteScript(), ["rigger", source, outFbx, report]), null, timeoutMs);
        if (!r.Ok || !File.Exists(report)) throw new InvalidOperationException("Blender konnte die Figur nicht vorbereiten: " + Last(r.Err.Length > 0 ? r.Err : r.Out));
        return ParseRigger(File.ReadAllText(report));
    }

    /// <summary>M4: Animationen aufbereiten. anims: Aktion → Mixamo-Datei. figure: Mixamo-Figur (oder leer).</summary>
    public static PrepReport Prep(string blender, string figure, IReadOnlyDictionary<string, string> anims, string outDir, string motion, int timeoutMs = 1_800_000)
    {
        if (anims.Count == 0) throw new InvalidOperationException("Ordne erst mindestens einer Spielaktion eine Mixamo-Animation zu.");
        if (motion is not (InPlace or RootMotion)) throw new ArgumentException("Unbekannte Bewegungsart.");
        Directory.CreateDirectory(outDir);
        var report = Path.Combine(outDir, "bericht.json"); if (File.Exists(report)) File.Delete(report);
        var rest = new List<string> { "prep", figure, outDir, motion, report };
        rest.AddRange(anims.Select(kv => kv.Key + "=" + kv.Value));
        var r = Cli.Run(blender, Args(WriteScript(), rest), null, timeoutMs);
        if (!r.Ok || !File.Exists(report)) throw new InvalidOperationException("Blender konnte die Animationen nicht aufbereiten: " + Last(r.Err.Length > 0 ? r.Err : r.Out));
        return ParsePrep(File.ReadAllText(report));
    }
}
