using System.Diagnostics;

namespace Trendo.Core.Bibliothek;

/// <summary>T-0279: Modelle aus anderen Programmen übernehmen – das mitgelieferte Blender öffnet FBX, OBJ, USD/USDZ, Alembic, PLY, STL, glTF
/// oder eine .blend-Datei im Hintergrund und schreibt ein GLB (Meter, Y oben, Materialien und Texturen eingebettet).</summary>
public static class ModelConverter
{
    public static readonly string[] Extensions = [".fbx", ".obj", ".usd", ".usda", ".usdc", ".usdz", ".abc", ".ply", ".stl", ".blend", ".gltf", ".glb"];

    /// <summary>Blender suchen: Umgebungsvariable TRENDO_BLENDER, sonst die übliche Installation.</summary>
    public static string? FindBlender()
    {
        if (Environment.GetEnvironmentVariable("TRENDO_BLENDER") is { Length: > 0 } e && File.Exists(e)) return e;
        foreach (var root in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "/usr/bin", "/opt", "/Applications" })
        {
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) continue;
            var bf = Path.Combine(root, "Blender Foundation");
            if (Directory.Exists(bf)) { var hit = Directory.GetDirectories(bf).OrderByDescending(d => d).Select(d => Path.Combine(d, "blender.exe")).FirstOrDefault(File.Exists); if (hit is not null) return hit; }
            var unix = Path.Combine(root, "blender"); if (File.Exists(unix)) return unix;
            var mac = Path.Combine(root, "Blender.app", "Contents", "MacOS", "Blender"); if (File.Exists(mac)) return mac;
        }
        return null;
    }

    /// <summary>Python-Skript für Blender: Szene leeren, Datei importieren (bzw. öffnen), alles als GLB schreiben.</summary>
    public static string Script(string input, string output)
    {
        var ext = Path.GetExtension(input).ToLowerInvariant();
        if (!Extensions.Contains(ext)) throw new ArgumentException($"Das Format {ext} kann Trendo nicht übernehmen. Möglich: {string.Join(", ", Extensions)}.");
        static string Py(string s) => "r'" + s.Replace("'", "") + "'";
        var import = ext switch
        {
            ".fbx" => $"bpy.ops.import_scene.fbx(filepath={Py(input)})",
            ".obj" => $"bpy.ops.wm.obj_import(filepath={Py(input)})",
            ".usd" or ".usda" or ".usdc" or ".usdz" => $"bpy.ops.wm.usd_import(filepath={Py(input)})",
            ".abc" => $"bpy.ops.wm.alembic_import(filepath={Py(input)})",
            ".ply" => $"bpy.ops.wm.ply_import(filepath={Py(input)})",
            ".stl" => $"bpy.ops.wm.stl_import(filepath={Py(input)})",
            ".gltf" or ".glb" => $"bpy.ops.import_scene.gltf(filepath={Py(input)})",
            _ => $"bpy.ops.wm.open_mainfile(filepath={Py(input)})",
        };
        return "import bpy, sys\n"
            + (ext == ".blend" ? "" : "bpy.ops.wm.read_homefile(use_empty=True)\n")
            + $"try:\n    {import}\nexcept Exception as e:\n    print('TRENDO_FEHLER', e); sys.exit(2)\n"
            + "meshes = [o for o in bpy.context.scene.objects if o.type in ('MESH', 'CURVE', 'SURFACE', 'META', 'FONT')]\n"
            + "if not meshes:\n    print('TRENDO_FEHLER keine Netze'); sys.exit(3)\n"
            + $"bpy.ops.export_scene.gltf(filepath={Py(output)}, export_format='GLB', use_selection=False, export_apply=True, export_yup=True, export_materials='EXPORT', export_image_format='AUTO')\n"
            + "print('TRENDO_OK', len(meshes))\n";
    }

    /// <summary>Umwandeln (Blender im Hintergrund, ohne Fenster, höchstens <paramref name="timeout"/>).</summary>
    public static async Task<string> ToGlb(string input, string output, string? blender = null, TimeSpan? timeout = null, CancellationToken ct = default)
    {
        if (!File.Exists(input)) throw new FileNotFoundException("Diese Datei gibt es nicht.");
        blender ??= FindBlender() ?? throw new InvalidOperationException("Blender wurde nicht gefunden – es gehört zum Trendo-Vollpaket und lässt sich unter Programme einrichten.");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        var script = Path.Combine(Path.GetTempPath(), $"trendo-convert-{Guid.NewGuid():N}.py");
        await File.WriteAllTextAsync(script, Script(Path.GetFullPath(input), Path.GetFullPath(output)), ct);
        try
        {
            var psi = new ProcessStartInfo(blender) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
            foreach (var a in new[] { "-b", "--factory-startup", "--python", script }) psi.ArgumentList.Add(a);
            using var p = Process.Start(psi) ?? throw new InvalidOperationException("Blender ließ sich nicht starten.");
            var outTask = p.StandardOutput.ReadToEndAsync(ct); var errTask = p.StandardError.ReadToEndAsync(ct);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct); cts.CancelAfter(timeout ?? TimeSpan.FromMinutes(3));
            try { await p.WaitForExitAsync(cts.Token); }
            catch (OperationCanceledException) { try { p.Kill(true); } catch (InvalidOperationException) { } throw new TimeoutException("Die Umwandlung dauert zu lange und wurde abgebrochen."); }
            var stdout = await outTask; await errTask;
            if (stdout.Split('\n').FirstOrDefault(l => l.StartsWith("TRENDO_FEHLER")) is { } err) throw new InvalidDataException("Blender konnte die Datei nicht lesen: " + err["TRENDO_FEHLER".Length..].Trim());
            if (!File.Exists(output)) throw new InvalidDataException("Blender hat kein Modell geschrieben.");
            return output;
        }
        finally { try { File.Delete(script); } catch (IOException) { } }
    }
}
