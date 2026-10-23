using System.Text;

namespace Trendo.Core;

/// <summary>CORE-05: 3D-Studio – steuert Blender im Hintergrund (ohne Fenster): Modell anlegen, Vorschau rendern, Formate umwandeln.</summary>
public static class Studio3D
{
    public static readonly (string Id, string Label)[] Shapes = [("cube", "Würfel"), ("sphere", "Kugel"), ("monkey", "Affenkopf"), ("text", "3D-Schrift")];
    public static readonly (string Ext, string Label)[] Formats = [(".glb", "glTF (Web, Spiele)"), (".obj", "OBJ"), (".stl", "STL (3D-Druck)"), (".fbx", "FBX"), (".blend", "Blender-Datei")];
    public static readonly string[] Imports = [".glb", ".gltf", ".obj", ".stl", ".fbx", ".blend"];

    // Das Skript liest seine Parameter aus sys.argv nach "--"; Werte werden nie in den Code eingesetzt.
    public const string Script = """
import bpy, sys, os, math
a = sys.argv[sys.argv.index("--") + 1:]
mode, src, out, extra, color = a[0], a[1], a[2], a[3], a[4]
def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
def load(p):
    e = os.path.splitext(p)[1].lower()
    if e == ".blend": bpy.ops.wm.open_mainfile(filepath=p)
    elif e in (".glb", ".gltf"): bpy.ops.import_scene.gltf(filepath=p)
    elif e == ".obj": bpy.ops.wm.obj_import(filepath=p)
    elif e == ".stl": bpy.ops.wm.stl_import(filepath=p)
    elif e == ".fbx": bpy.ops.import_scene.fbx(filepath=p)
    else: raise SystemExit("format")
def save(p):
    e = os.path.splitext(p)[1].lower()
    if e == ".glb": bpy.ops.export_scene.gltf(filepath=p, export_format="GLB")
    elif e == ".obj": bpy.ops.wm.obj_export(filepath=p)
    elif e == ".stl": bpy.ops.wm.stl_export(filepath=p)
    elif e == ".fbx": bpy.ops.export_scene.fbx(filepath=p)
    elif e == ".blend": bpy.ops.wm.save_as_mainfile(filepath=p)
    else: raise SystemExit("format")
def preview(p):
    sc = bpy.context.scene
    objs = [o for o in sc.objects if o.type == "MESH"]
    if not any(o.type == "CAMERA" for o in sc.objects):
        bpy.ops.object.camera_add(location=(6, -6, 4.5)); cam = bpy.context.object
        bpy.ops.object.select_all(action="DESELECT")
        for o in objs: o.select_set(True)
        sc.camera = cam
        c = cam.constraints.new("TRACK_TO")
        bpy.ops.object.empty_add(location=(0, 0, 0)); c.target = bpy.context.object
        if objs: bpy.ops.view3d.camera_to_view_selected() if bpy.context.area else None
    sc.render.engine = "BLENDER_WORKBENCH"
    sc.display.shading.light = "STUDIO"; sc.display.shading.color_type = "OBJECT"
    sc.render.resolution_x, sc.render.resolution_y, sc.render.resolution_percentage = 640, 480, 100
    sc.render.film_transparent = False
    sc.render.filepath = p; sc.render.image_settings.file_format = "PNG"
    bpy.ops.render.render(write_still=True)
if mode == "new":
    reset()
    if extra == "cube": bpy.ops.mesh.primitive_cube_add(size=2)
    elif extra == "sphere": bpy.ops.mesh.primitive_uv_sphere_add(radius=1.3, segments=48, ring_count=24); bpy.ops.object.shade_smooth()
    elif extra == "monkey": bpy.ops.mesh.primitive_monkey_add(size=2); bpy.ops.object.shade_smooth()
    else:
        bpy.ops.object.text_add(); t = bpy.context.object; t.data.body = src or "Trendo"; t.data.extrude = 0.15; t.data.align_x = "CENTER"
        t.rotation_euler = (math.radians(90), 0, 0); bpy.ops.object.convert(target="MESH")
    o = bpy.context.object
    r, g, b = (int(color[i:i+2], 16) / 255 for i in (1, 3, 5)); o.color = (r, g, b, 1)
    bpy.ops.object.light_add(type="SUN", location=(4, -4, 8))
    save(out)
elif mode == "convert":
    reset(); load(src); save(out)
elif mode == "preview":
    reset(); load(src); preview(out)
elif mode == "info":
    reset(); load(src)
    m = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    print("TRENDO_INFO objects=%d vertices=%d faces=%d" % (len(m), sum(len(o.data.vertices) for o in m), sum(len(o.data.polygons) for o in m)))
""";

    public sealed record Info(int Objects, int Vertices, int Faces);

    private static Cli.Result Blender(string blender, string mode, string src, string outp, string extra = "", string color = "#2F86F6")
    {
        var script = Path.Combine(Path.GetTempPath(), "trendo-3d-" + Environment.ProcessId + ".py");
        File.WriteAllText(script, Script, new UTF8Encoding(false));
        return Cli.Run(blender, ["--background", "--factory-startup", "--python-exit-code", "1", "--python", script, "--", mode, src, outp, extra, color], null, 600000);
    }

    private static string Free(string p)
    {
        var d = Path.GetDirectoryName(p)!; var b = Path.GetFileNameWithoutExtension(p); var e = Path.GetExtension(p);
        for (var i = 2; File.Exists(p); i++) p = Path.Combine(d, $"{b}-{i}{e}");
        return p;
    }

    public static (Cli.Result R, string Out) Create(string blender, string folder, string shape, string name, string format = ".blend", string color = "#2F86F6", string text = "Trendo")
    {
        if (!Shapes.Any(s => s.Id == shape)) throw new ArgumentException("Unbekannte Form");
        if (!System.Text.RegularExpressions.Regex.IsMatch(color, "^#[0-9A-Fa-f]{6}$")) color = "#2F86F6";
        var outp = Free(Path.Combine(folder, DevStudio.SafeName(name) + format));
        return (Blender(blender, "new", text, outp, shape, color), outp);
    }

    public static (Cli.Result R, string Out) Convert(string blender, string input, string format, string? output = null)
    {
        if (!Imports.Contains(Path.GetExtension(input).ToLowerInvariant())) return (new(-1, "", "Dieses Format kann Trendo nicht öffnen."), "");
        if (!Formats.Any(f => f.Ext.Equals(format, StringComparison.OrdinalIgnoreCase))) return (new(-1, "", "In dieses Format kann Trendo nicht speichern."), "");
        var outp = string.IsNullOrWhiteSpace(output) ? Free(Path.ChangeExtension(input, format)) : output;
        var dir = Path.GetDirectoryName(outp);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        return (Blender(blender, "convert", input, outp), outp);
    }

    public static (Cli.Result R, string Out) Preview(string blender, string input)
    {
        var outp = Free(Path.Combine(Path.GetDirectoryName(input)!, Path.GetFileNameWithoutExtension(input) + "-vorschau.png"));
        return (Blender(blender, "preview", input, outp), outp);
    }

    public static Info? Inspect(string blender, string input)
    {
        var r = Blender(blender, "info", input, "");
        var line = r.Out.Split('\n').FirstOrDefault(l => l.StartsWith("TRENDO_INFO"));
        if (line is null) return null;
        int V(string k) => int.Parse(line.Split(' ').First(x => x.StartsWith(k + "="))[(k.Length + 1)..]);
        return new(V("objects"), V("vertices"), V("faces"));
    }

    /// <summary>Baut die aktuelle Szene in Blender nach und speichert sie als .blend. Werte stehen nur in der JSON-Datei.</summary>
    public const string SceneScript = """
import bpy, json, sys, os, math
a = sys.argv[sys.argv.index("--") + 1:]
spec_path, out_blend = a[0], a[1]
with open(spec_path, encoding="utf-8") as f:
    spec = json.load(f)
bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
sc.unit_settings.system = "METRIC"
sc.unit_settings.scale_length = 0.01
sc.unit_settings.length_unit = "CENTIMETERS"

def activate(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj

def color_of(h):
    h = (h or "#2F86F6").lstrip("#")
    if len(h) < 6: h = "2F86F6"
    return tuple(int(h[i:i+2], 16) / 255 for i in (0, 2, 4)) + (1.0,)

def paint(obj, h):
    r, g, b, a = color_of(h)
    mat = bpy.data.materials.new(obj.name[:50] or "Farbe")
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf: bsdf.inputs["Base Color"].default_value = (r, g, b, 1)
    obj.data.materials.append(mat)
    obj.color = (r, g, b, 1)

for part in spec.get("parts", []):
    shape = part.get("shape") or "cube"
    obj = None
    if shape == "mesh":
        p = part.get("file") or ""
        if not p or not os.path.isfile(p): continue
        before = set(bpy.data.objects)
        e = os.path.splitext(p)[1].lower()
        if e == ".obj": bpy.ops.wm.obj_import(filepath=p)
        elif e == ".stl": bpy.ops.wm.stl_import(filepath=p)
        elif e in (".glb", ".gltf"): bpy.ops.import_scene.gltf(filepath=p)
        elif e == ".fbx": bpy.ops.import_scene.fbx(filepath=p)
        else: continue
        new = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
        if not new: continue
        activate(new[0])
        for o in new: o.select_set(True)
        if len(new) > 1: bpy.ops.object.join()
        obj = bpy.context.view_layer.objects.active
        bx = float(part.get("baseX") or part.get("sx") or 1) or 1
        by = float(part.get("baseY") or part.get("sy") or 1) or 1
        bz = float(part.get("baseZ") or part.get("sz") or 1) or 1
        obj.scale = (float(part.get("sx") or 1) / bx, float(part.get("sy") or 1) / by, float(part.get("sz") or 1) / bz)
        activate(obj)
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    elif shape == "text":
        bpy.ops.object.text_add()
        obj = bpy.context.object
        obj.data.body = part.get("text") or "Text"
        obj.data.align_x = "CENTER"
        obj.data.extrude = max(0.05, float(part.get("sy") or 1) / 2)
        obj.data.size = max(0.05, float(part.get("sz") or 1) / 7)
        activate(obj)
        bpy.ops.object.convert(target="MESH")
    else:
        if shape == "cube": bpy.ops.mesh.primitive_cube_add(size=1)
        elif shape == "sphere": bpy.ops.mesh.primitive_uv_sphere_add(radius=0.5, segments=32, ring_count=16)
        elif shape == "cylinder": bpy.ops.mesh.primitive_cylinder_add(radius=0.5, depth=1, vertices=32)
        elif shape == "cone": bpy.ops.mesh.primitive_cone_add(radius1=0.5, depth=1, vertices=32)
        elif shape == "torus": bpy.ops.mesh.primitive_torus_add(major_radius=0.35, minor_radius=0.12)
        elif shape == "plane": bpy.ops.mesh.primitive_plane_add(size=1)
        elif shape == "monkey": bpy.ops.mesh.primitive_monkey_add(size=1)
        else: continue
        obj = bpy.context.object
        obj.dimensions = (float(part.get("sx") or 1), float(part.get("sy") or 1), float(part.get("sz") or 1))
        activate(obj)
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        if shape in ("sphere", "cylinder", "cone", "torus"):
            try: bpy.ops.object.shade_smooth()
            except Exception: pass
    if obj is None or obj.type != "MESH": continue
    obj.name = (part.get("name") or "Teil")[:60]
    paint(obj, part.get("color"))
    for op in part.get("ops") or []:
        kind = op.get("kind")
        if kind == "bevel":
            mod = obj.modifiers.new("Abrunden", "BEVEL")
            mod.width = float(op.get("amount") or 0.3); mod.segments = 3
        elif kind == "smooth":
            mod = obj.modifiers.new("Glätten", "SUBSURF")
            mod.levels = max(1, int(float(op.get("amount") or 1)))
        elif kind == "mirror":
            mod = obj.modifiers.new("Spiegel", "MIRROR")
            ax = op.get("axis") or "x"
            mod.use_axis[0] = ax == "x"; mod.use_axis[1] = ax == "y"; mod.use_axis[2] = ax == "z"
        elif kind == "shell":
            mod = obj.modifiers.new("Aushöhlen", "SOLIDIFY")
            mod.thickness = float(op.get("amount") or 0.4)
        elif kind == "hole":
            depth = max(float(part.get("sx") or 1), float(part.get("sy") or 1), float(part.get("sz") or 1)) * 4 + 1
            bpy.ops.mesh.primitive_cylinder_add(radius=max(0.05, float(op.get("diameter") or 1) / 2), depth=depth, vertices=24,
                location=(float(op.get("atX") or 0), float(op.get("atY") or 0), float(op.get("atZ") or 0)))
            cut = bpy.context.object
            ax = op.get("axis") or "z"
            if ax == "x": cut.rotation_euler = (0, math.radians(90), 0)
            elif ax == "y": cut.rotation_euler = (math.radians(90), 0, 0)
            cut.parent = obj
            cut.display_type = "WIRE"
            cut.hide_render = True
            mod = obj.modifiers.new("Loch", "BOOLEAN")
            mod.operation = "DIFFERENCE"
            mod.object = cut
            try: mod.solver = "EXACT"
            except Exception: pass
    obj.location = (float(part.get("x") or 0), float(part.get("y") or 0), float(part.get("z") or 0))
    obj.rotation_euler = tuple(math.radians(float(part.get(k) or 0)) for k in ("rx", "ry", "rz"))

bpy.ops.object.light_add(type="SUN", location=(80, -120, 150))
if len(a) > 2 and a[2].lower().endswith(".fbx"):
    bpy.ops.export_scene.fbx(filepath=a[2])
bpy.ops.wm.save_as_mainfile(filepath=out_blend)
print("TRENDO_BLEND ok")
""";

    public static (Cli.Result R, string Out) ExportBlend(string blender, string specPath, string outBlend, string? alsoFbx = null)
    {
        var script = Path.Combine(Path.GetTempPath(), "trendo-3d-scene-" + Environment.ProcessId + ".py");
        File.WriteAllText(script, SceneScript, new UTF8Encoding(false));
        var args = new List<string> { "--background", "--factory-startup", "--python-exit-code", "1", "--python", script, "--", specPath, outBlend };
        if (alsoFbx is not null) args.Add(alsoFbx);
        var r = Cli.Run(blender, args, null, 600000);
        return (r, outBlend);
    }
}
