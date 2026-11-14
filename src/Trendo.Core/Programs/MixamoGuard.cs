using System.IO.Compression;

namespace Trendo.Core.Programs;

/// <summary>T-0660, M6: Lizenzwächter für Mixamo. Rohdateien mit Mixamo-Skelett (FBX, glTF, BVH, DAE, auch in ZIP-Archiven) dürfen nur in den fertigen Spiel-Build,
/// nie in einen Export, ein Asset-Paket oder eine Weitergabe. Erkannt wird am Inhalt (Knochen „mixamorig…“), damit auch umbenannte oder in Blender
/// überarbeitete Dateien auffallen. In gekochten Engine-Paketen (.pak, .pck) stecken keine Rohdateien; sie werden nicht geprüft.</summary>
public static class MixamoGuard
{
    public const string Build = "build", Export = "export", Package = "asset-paket", Share = "weitergabe";

    static readonly string[] Raw = [".fbx", ".glb", ".gltf", ".dae", ".bvh"];

    /// <summary>Mixamo-Rohdateien in den Dateien oder Ordnern (bei ZIP: Einträge darin, als „archiv.zip › eintrag“).</summary>
    public static List<string> Find(IEnumerable<string> paths)
    {
        var hits = new List<string>();
        foreach (var p in paths)
        {
            if (Directory.Exists(p))
            {
                IEnumerable<string> files;
                try { files = Directory.EnumerateFiles(p, "*", SearchOption.AllDirectories).ToList(); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { continue; }
                hits.AddRange(Find(files));
            }
            else if (File.Exists(p))
            {
                var ext = Path.GetExtension(p).ToLowerInvariant();
                if (Raw.Contains(ext) && Mixamo.Detect(p).IsMixamo) hits.Add(p);
                else if (ext == ".zip") hits.AddRange(InZip(p));
            }
        }
        return hits;
    }

    static List<string> InZip(string zip)
    {
        var hits = new List<string>();
        try
        {
            using var z = ZipFile.OpenRead(zip);
            foreach (var e in z.Entries.Where(e => Raw.Contains(Path.GetExtension(e.Name).ToLowerInvariant())))
            {
                var tmp = Path.Combine(Path.GetTempPath(), "trendo-mixamo-" + Guid.NewGuid().ToString("N") + Path.GetExtension(e.Name));
                try { e.ExtractToFile(tmp); if (Mixamo.Detect(tmp).IsMixamo) hits.Add($"{Path.GetFileName(zip)} › {e.FullName}"); }
                finally { try { File.Delete(tmp); } catch (IOException) { } }
            }
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException) { }
        return hits;
    }

    /// <summary>Bricht mit verständlicher Meldung ab, wenn Mixamo-Rohdateien in etwas anderes als den fertigen Spiel-Build sollen.</summary>
    public static void Ensure(string purpose, IEnumerable<string> paths)
    {
        if (purpose == Build) return;
        var hits = Find(paths);
        if (hits.Count == 0) return;
        var what = purpose switch { Package => "ein Asset-Paket", Share => "eine Weitergabe", _ => "einen Export" };
        throw new MixamoLicenseException($"Abgebrochen: {hits.Count} Mixamo-Rohdatei(en) dürfen nicht in {what} ({string.Join(", ", hits.Take(3).Select(Path.GetFileName))}{(hits.Count > 3 ? " …" : "")}). " +
            "Mixamo erlaubt sie nur im fertigen Spiel. Nimm sie aus dem Ordner oder baue das Spiel mit der Engine.", hits);
    }
}

/// <summary>Verstoß gegen die Mixamo-Bedingungen (Rohdateien in Export, Paket oder Weitergabe).</summary>
public sealed class MixamoLicenseException(string message, IReadOnlyList<string> files) : InvalidOperationException(message)
{
    public IReadOnlyList<string> Files { get; } = files;
}
