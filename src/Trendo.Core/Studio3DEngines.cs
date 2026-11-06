using System.Text;
using System.Text.Json;

namespace Trendo.Core;

/// <summary>Ziel-Engine für den 3D-Export mit Format und Begründung (Paket 01 / S2-17 „Blender–Unreal–Web Fusion“, Unity zusätzlich bestätigt).</summary>
public sealed record EngineProfile(string Id, string Label, string Ext, string Why);

/// <summary>Messwerte einer Szene (Quelle in Blender bzw. wieder eingelesener Export).</summary>
public sealed record SceneStats(int Meshes, long Vertices, long Triangles, int Materials, int MeshesWithUv, int Bones, int Animations, int ShapeKeys, double SizeX, double SizeY, double SizeZ);

/// <summary>Eine Abweichung im Vergleich: „Fehler“ (geht verloren) oder „Hinweis“ (verändert, meist gewollt).</summary>
public sealed record ExportFinding(bool Error, string Text);

/// <summary>Ergebnis des Rundlaufs: exportierte Datei, Quelle, Wieder-Import und Abweichungen.</summary>
public sealed record EngineExportReport(string Target, string File, SceneStats Source, SceneStats Result, IReadOnlyList<ExportFinding> Findings)
{
    public bool Ok => Findings.All(f => !f.Error);
}

/// <summary>S2-17: Export für Unity, Unreal, Godot und Web über Blender – mit Rundlauf-Prüfung: Blender exportiert, liest die Datei sofort wieder ein
/// und vergleicht Dreiecke, Materialien, UV, Knochen, Animationen, Formschlüssel und Maße. Kein verlustfreies Versprechen: jede Abweichung steht im Bericht.</summary>
public static class Studio3DEngines
{
    public static readonly EngineProfile[] Profiles =
    [
        new("unity", "Unity", ".fbx", "FBX ist Unitys eingebautes Format; Maßstab 1 m, Y oben, ohne Blattknochen."),
        new("unreal", "Unreal Engine", ".fbx", "FBX mit Kanten-Glättung je Fläche und ohne Blattknochen, wie Epic es für Blender empfiehlt."),
        new("godot", "Godot", ".glb", "glTF/GLB ist Godots bevorzugtes Format (Materialien, Knochen, Animationen)."),
        new("web", "Web", ".glb", "GLB für Webseiten (three.js, model-viewer) – eine Datei mit Texturen."),
    ];

    public static EngineProfile Profile(string id) => Profiles.FirstOrDefault(p => p.Id == id) ?? throw new ArgumentException($"Unbekanntes Ziel „{id}“.");

    // Parameter kommen über sys.argv nach "--", nie in den Code eingesetzt.
    public const string Script = """
import bpy, sys, os, json
a = sys.argv[sys.argv.index("--") + 1:]
src, target, out, report = a[0], a[1], a[2], a[3]
def load(p):
    e = os.path.splitext(p)[1].lower()
    if e == ".blend": bpy.ops.wm.open_mainfile(filepath=p)
    else:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        if e in (".glb", ".gltf"): bpy.ops.import_scene.gltf(filepath=p)
        elif e == ".fbx": bpy.ops.import_scene.fbx(filepath=p)
        elif e == ".obj": bpy.ops.wm.obj_import(filepath=p)
        elif e == ".stl": bpy.ops.wm.stl_import(filepath=p)
        else: raise SystemExit("format")
def stats():
    dg = bpy.context.evaluated_depsgraph_get()
    shapes = {pb.custom_shape for o in bpy.context.scene.objects if o.type == "ARMATURE" for pb in o.pose.bones if pb.custom_shape}
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH" and o not in shapes]  # Knochen-Formen (glTF-Import) zählen nicht
    v = t = uv = sk = 0
    mats = set()
    lo = [1e9, 1e9, 1e9]; hi = [-1e9, -1e9, -1e9]
    for o in meshes:
        ev = o.evaluated_get(dg); m = ev.to_mesh()
        m.calc_loop_triangles()
        v += len(m.vertices); t += len(m.loop_triangles)
        if len(m.uv_layers) > 0: uv += 1
        for s in o.material_slots:
            if s.material: mats.add(s.material.name.split(".")[0])
        if o.data.shape_keys: sk += max(0, len(o.data.shape_keys.key_blocks) - 1)
        for c in o.bound_box:
            w = o.matrix_world @ __import__("mathutils").Vector(c)
            for i in range(3): lo[i] = min(lo[i], w[i]); hi[i] = max(hi[i], w[i])
        ev.to_mesh_clear()
    bones = sum(len(o.data.bones) for o in bpy.context.scene.objects if o.type == "ARMATURE")
    anims = len([x for x in bpy.data.actions if x.users > 0 or x.use_fake_user])
    size = [round(hi[i] - lo[i], 4) if meshes else 0 for i in range(3)]
    return {"meshes": len(meshes), "vertices": v, "triangles": t, "materials": len(mats), "meshesWithUv": uv, "bones": bones, "animations": anims, "shapeKeys": sk, "sizeX": size[0], "sizeY": size[1], "sizeZ": size[2]}
load(src)
before = stats()
os.makedirs(os.path.dirname(out) or ".", exist_ok=True)
if target == "unity":
    bpy.ops.export_scene.fbx(filepath=out, apply_scale_options="FBX_SCALE_UNITS", apply_unit_scale=True, bake_space_transform=True, axis_forward="-Z", axis_up="Y", mesh_smooth_type="FACE", add_leaf_bones=False, use_mesh_modifiers=True, bake_anim=True, path_mode="COPY", embed_textures=True)
elif target == "unreal":
    bpy.ops.export_scene.fbx(filepath=out, apply_unit_scale=True, mesh_smooth_type="FACE", use_tspace=True, add_leaf_bones=False, use_mesh_modifiers=True, bake_anim=True, path_mode="COPY", embed_textures=True)
else:
    bpy.ops.export_scene.gltf(filepath=out, export_format="GLB", export_apply=True, export_yup=True, export_animations=True, export_morph=True, export_skins=True, export_materials="EXPORT", export_image_format="AUTO")
load(out)
after = stats()
with open(report, "w", encoding="utf-8") as f: json.dump({"target": target, "file": out, "source": before, "result": after}, f)
print("TRENDO_ENGINE_EXPORT ok")
""";

    /// <summary>Blender-Argumente (ohne Fenster, Werkseinstellungen, Fehler im Skript ergibt Exitcode 1).</summary>
    public static List<string> Args(string script, string src, string target, string output, string report) =>
        ["--background", "--factory-startup", "--python-exit-code", "1", "--python", script, "--", src, target, output, report];

    public static EngineExportReport Run(string blender, string source, string target, string outDir, int timeoutMs = 900_000)
    {
        var p = Profile(target);
        if (!File.Exists(source)) throw new IOException($"„{Path.GetFileName(source)}“ gibt es nicht.");
        Directory.CreateDirectory(outDir);
        var output = Path.Combine(outDir, Path.GetFileNameWithoutExtension(source) + "-" + target + p.Ext);
        var report = Path.Combine(outDir, Path.GetFileNameWithoutExtension(source) + "-" + target + "-bericht.json");
        var script = Path.Combine(Path.GetTempPath(), "trendo-engine-export-" + Environment.ProcessId + ".py");
        File.WriteAllText(script, Script, new UTF8Encoding(false));
        var r = Cli.Run(blender, Args(script, source, target, output, report), null, timeoutMs);
        if (!r.Ok || !File.Exists(report)) throw new InvalidOperationException("Blender konnte nicht exportieren: " + Last(r.Err.Length > 0 ? r.Err : r.Out));
        return Parse(File.ReadAllText(report));
    }

    static string Last(string s) { var l = s.Split('\n').Select(x => x.Trim()).Where(x => x.Length > 0).TakeLast(3); return string.Join(" | ", l); }

    public static SceneStats ReadStats(JsonElement e)
    {
        int I(string k) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? (int)v.GetDouble() : 0;
        long L(string k) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? (long)v.GetDouble() : 0;
        double D(string k) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : 0;
        return new(I("meshes"), L("vertices"), L("triangles"), I("materials"), I("meshesWithUv"), I("bones"), I("animations"), I("shapeKeys"), D("sizeX"), D("sizeY"), D("sizeZ"));
    }

    /// <summary>Bericht aus Blenders JSON lesen und vergleichen.</summary>
    public static EngineExportReport Parse(string json)
    {
        using var d = JsonDocument.Parse(json);
        var r = d.RootElement;
        var s = ReadStats(r.GetProperty("source")); var t = ReadStats(r.GetProperty("result"));
        var target = r.GetProperty("target").GetString() ?? "";
        return new(target, r.GetProperty("file").GetString() ?? "", s, t, Compare(s, t));
    }

    /// <summary>Vergleich Quelle ↔ Wieder-Import. Fehler: Teile, Materialien, UV, Knochen, Animationen oder Formschlüssel gehen verloren, Geometrie fehlt, Größe stimmt nicht.
    /// Hinweise: Dreiecke durch Triangulieren/Modifikatoren verändert, mehr Knochen (Wurzelknochen), gedrehte Achsen.</summary>
    public static List<ExportFinding> Compare(SceneStats s, SceneStats t)
    {
        var f = new List<ExportFinding>();
        if (s.Meshes > 0 && t.Meshes == 0) { f.Add(new(true, "Im Export ist keine Geometrie angekommen.")); return f; }
        if (t.Meshes < s.Meshes) f.Add(new(true, $"Teile verloren: {s.Meshes} → {t.Meshes}."));
        if (s.Triangles > 0)
        {
            var diff = Math.Abs(t.Triangles - s.Triangles) / (double)s.Triangles;
            if (diff > 0.25) f.Add(new(true, $"Dreiecke stark verändert: {s.Triangles} → {t.Triangles}."));
            else if (diff > 0.001) f.Add(new(false, $"Dreiecke leicht verändert: {s.Triangles} → {t.Triangles} (Modifikatoren angewendet)."));
        }
        if (t.Materials < s.Materials) f.Add(new(true, $"Materialien verloren: {s.Materials} → {t.Materials}."));
        if (t.MeshesWithUv < s.MeshesWithUv) f.Add(new(true, $"UV-Koordinaten verloren: {s.MeshesWithUv} → {t.MeshesWithUv} Teile mit UV – Texturen sitzen dann falsch."));
        if (s.Bones > 0 && t.Bones < s.Bones) f.Add(new(true, $"Knochen verloren: {s.Bones} → {t.Bones}."));
        else if (t.Bones > s.Bones && s.Bones > 0) f.Add(new(false, $"Knochen: {s.Bones} → {t.Bones} (Wurzelknochen der Engine)."));
        if (t.Animations < s.Animations) f.Add(new(true, $"Animationen verloren: {s.Animations} → {t.Animations}."));
        if (t.ShapeKeys < s.ShapeKeys) f.Add(new(true, $"Formschlüssel (Gesichtsausdrücke) verloren: {s.ShapeKeys} → {t.ShapeKeys}."));
        // Größe: Kanten unabhängig von der Achsen-Reihenfolge vergleichen (Y-oben ↔ Z-oben)
        var a = new[] { s.SizeX, s.SizeY, s.SizeZ }.OrderBy(x => x).ToArray(); var b = new[] { t.SizeX, t.SizeY, t.SizeZ }.OrderBy(x => x).ToArray();
        for (var i = 0; i < 3; i++)
            if (a[i] > 1e-4 && Math.Abs(b[i] - a[i]) / a[i] > 0.01)
            {
                var k = b[i] / a[i];
                f.Add(new(true, $"Größe stimmt nicht: {Math.Round(a[i], 3)} m → {Math.Round(b[i], 3)} m (Faktor {Math.Round(k, 3)}).")); break;
            }
        if (s.SizeY > 0 && Math.Abs(t.SizeZ - s.SizeY) < 1e-3 && Math.Abs(t.SizeY - s.SizeZ) < 1e-3 && Math.Abs(s.SizeY - s.SizeZ) > 1e-3) f.Add(new(false, "Achsen getauscht (Y oben ↔ Z oben) – so erwartet es die Engine."));
        return f;
    }

    /// <summary>Bericht als Text für die Anzeige und die Berichtsdatei.</summary>
    public static string Text(EngineExportReport r)
    {
        var p = Profiles.FirstOrDefault(x => x.Id == r.Target);
        var sb = new StringBuilder();
        sb.AppendLine($"{(r.Ok ? "✓" : "⚠")} {p?.Label ?? r.Target}: {Path.GetFileName(r.File)}");
        sb.AppendLine($"Quelle: {r.Source.Meshes} Teile, {r.Source.Triangles} Dreiecke, {r.Source.Materials} Materialien, {r.Source.Bones} Knochen, {r.Source.Animations} Animationen");
        sb.AppendLine($"Export: {r.Result.Meshes} Teile, {r.Result.Triangles} Dreiecke, {r.Result.Materials} Materialien, {r.Result.Bones} Knochen, {r.Result.Animations} Animationen");
        foreach (var f in r.Findings) sb.AppendLine((f.Error ? "  ✗ " : "  · ") + f.Text);
        if (r.Findings.Count == 0) sb.AppendLine("  Keine Abweichung.");
        return sb.ToString().TrimEnd();
    }
}
