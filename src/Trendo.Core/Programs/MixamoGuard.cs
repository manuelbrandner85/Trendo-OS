using System.IO.Compression;

namespace Trendo.Core.Programs;

/// <summary>T-0660, M6: Lizenzwächter für Mixamo. Rohdateien mit Mixamo-Skelett dürfen nur in den fertigen Spiel-Build, nie in einen Export, ein Asset-Paket
/// oder eine Weitergabe. Erkannt wird am Inhalt, unabhängig von der Endung: jede Datei, die wie ein 3D-Format beginnt (FBX, glTF, BVH, Collada), wird auf
/// Knochen „mixamorig…“ geprüft – auch umbenannt, in Blender überarbeitet oder in ZIP-Archiven (auch verschachtelt). ZIP-Einträge werden als Datenstrom mit
/// Größengrenze geprüft, nichts wird entpackt. Was sich nicht prüfen lässt (defektes Archiv, gesperrter Ordner, zu groß), gilt als nicht freigegeben.
/// Gekochte Engine-Pakete (.pak, .pck) beginnen nicht wie ein 3D-Format und enthalten keine Rohdateien.</summary>
public static class MixamoGuard
{
    public const string Build = "build", Export = "export", Package = "asset-paket", Share = "weitergabe";
    public const long MaxEntry = 2L << 30; // 2 GB je Eintrag
    const int MaxDepth = 3;

    public sealed record Result(List<string> Hits, List<string> Unchecked);

    /// <summary>Mixamo-Rohdateien in Dateien oder Ordnern (bei ZIP: „archiv.zip › eintrag“).</summary>
    public static List<string> Find(IEnumerable<string> paths) => Scan(paths).Hits;

    public static Result Scan(IEnumerable<string> paths)
    {
        var r = new Result([], []);
        foreach (var p in paths)
        {
            if (Directory.Exists(p))
            {
                List<string> files;
                try { files = [.. Directory.EnumerateFiles(p, "*", SearchOption.AllDirectories)]; }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { r.Unchecked.Add(p); continue; }
                var sub = Scan(files); r.Hits.AddRange(sub.Hits); r.Unchecked.AddRange(sub.Unchecked);
            }
            else if (File.Exists(p))
            {
                try
                {
                    using var f = new FileStream(p, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete); // auch Dateien, an denen gerade jemand schreibt
                    var head = new byte[64]; var n = f.Read(head, 0, head.Length); f.Position = 0;
                    if (n >= 4 && head[0] == 'P' && head[1] == 'K' && head[2] == 3 && head[3] == 4) InZip(f, Path.GetFileName(p), 0, r);
                    else if (Mixamo.LooksLikeModel(head.AsSpan(0, n)) && Mixamo.Detect(f, Path.GetExtension(p).ToLowerInvariant(), long.MaxValue).IsMixamo) r.Hits.Add(p);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException) { r.Unchecked.Add(p); }
            }
        }
        return r;
    }

    static void InZip(Stream s, string label, int depth, Result r)
    {
        if (depth >= MaxDepth) { r.Unchecked.Add(label + " (zu tief verschachtelt)"); return; }
        try
        {
            using var z = new ZipArchive(s, ZipArchiveMode.Read, leaveOpen: true);
            foreach (var e in z.Entries.Where(e => e.Name.Length > 0))
            {
                var name = $"{label} › {e.FullName}";
                if (e.Length > MaxEntry) { r.Unchecked.Add(name + " (zu groß)"); continue; }
                try
                {
                    var head = new byte[64]; int n;
                    using (var es = e.Open()) n = ReadFull(es, head);
                    if (n >= 4 && head[0] == 'P' && head[1] == 'K' && head[2] == 3 && head[3] == 4)
                    {
                        using var inner = new MemoryStream(); using (var es = e.Open()) CopyMax(es, inner, 256L << 20); // verschachteltes ZIP: höchstens 256 MB in den Speicher
                        inner.Position = 0; InZip(inner, name, depth + 1, r);
                    }
                    else if (Mixamo.LooksLikeModel(head.AsSpan(0, n)))
                    {
                        using var es = e.Open();
                        if (Mixamo.Detect(es, Path.GetExtension(e.Name).ToLowerInvariant(), MaxEntry).IsMixamo) r.Hits.Add(name);
                    }
                }
                catch (Exception ex) when (ex is IOException or InvalidDataException) { r.Unchecked.Add(name); }
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException) { r.Unchecked.Add(label + " (Archiv nicht lesbar)"); }
    }

    static int ReadFull(Stream s, byte[] b) { int t = 0, n; while (t < b.Length && (n = s.Read(b, t, b.Length - t)) > 0) t += n; return t; }
    static void CopyMax(Stream from, Stream to, long max) { var buf = new byte[1 << 16]; long t = 0; int n; while ((n = from.Read(buf, 0, buf.Length)) > 0) { t += n; if (t > max) throw new InvalidDataException("zu groß"); to.Write(buf, 0, n); } }

    /// <summary>Bricht mit verständlicher Meldung ab, wenn Mixamo-Rohdateien in etwas anderes als den fertigen Spiel-Build sollen oder sich nicht prüfen ließen.</summary>
    public static void Ensure(string purpose, IEnumerable<string> paths)
    {
        if (purpose == Build) return;
        var r = Scan(paths);
        var what = purpose switch { Package => "ein Asset-Paket", Share => "eine Weitergabe", _ => "einen Export" };
        if (r.Hits.Count > 0)
            throw new MixamoLicenseException($"Abgebrochen: {r.Hits.Count} Mixamo-Rohdatei(en) dürfen nicht in {what} ({string.Join(", ", r.Hits.Take(3).Select(Short))}{(r.Hits.Count > 3 ? " …" : "")}). " +
                "Mixamo erlaubt sie nur im fertigen Spiel. Nimm sie aus dem Ordner oder baue das Spiel mit der Engine.", r.Hits);
        if (r.Unchecked.Count > 0)
            throw new MixamoLicenseException($"Abgebrochen: {r.Unchecked.Count} Datei(en) ließen sich nicht auf Mixamo-Dateien prüfen ({string.Join(", ", r.Unchecked.Take(3).Select(Short))}). Ohne Prüfung geht nichts in {what}.", r.Unchecked);
    }

    static string Short(string p) => p.Contains(" › ", StringComparison.Ordinal) ? p : Path.GetFileName(p);
}

/// <summary>Verstoß gegen die Mixamo-Bedingungen (Rohdateien in Export, Paket oder Weitergabe) oder nicht prüfbarer Inhalt.</summary>
public sealed class MixamoLicenseException(string message, IReadOnlyList<string> files) : InvalidOperationException(message)
{
    public IReadOnlyList<string> Files { get; } = files;
}
