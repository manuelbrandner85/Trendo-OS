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

    public static (Cli.Result R, string Out) Convert(string blender, string input, string format)
    {
        if (!Imports.Contains(Path.GetExtension(input).ToLowerInvariant())) return (new(-1, "", "Dieses Format kann Trendo nicht öffnen."), "");
        var outp = Free(Path.ChangeExtension(input, format));
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
}
