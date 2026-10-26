using System.Text.Json;
using System.Text.Json.Serialization;

namespace Trendo.Core;

/// <summary>T-0222: Die Studio-Programme kommen mit Trendo OS – aus dem Ordner „Programme“ des Vollpakets (USB, ISO oder Ordner neben dem Setup).
/// Eingerichtet wird ohne Internet und ohne Admin in den Werkzeugordner; jede Datei nur mit passender Prüfsumme aus dem Manifest.</summary>
public static class Vollpaket
{
    public const string FolderName = "Programme";
    public const string ManifestName = "manifest.json";

    public sealed record Entry(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("version")] string Version,
        [property: JsonPropertyName("license")] string License,
        [property: JsonPropertyName("file")] string File,
        [property: JsonPropertyName("sha256")] string Sha256,
        [property: JsonPropertyName("bytes")] long Bytes,
        [property: JsonPropertyName("exe")] string Exe,
        [property: JsonPropertyName("source_url")] string? SourceUrl);

    sealed record ManifestFile([property: JsonPropertyName("programs")] List<Entry> Programs);

    /// <summary>Katalog-Id → (Paket im Vollpaket, Hauptprogramm relativ zum Werkzeugordner). adb steckt im scrcpy-Paket.</summary>
    public static readonly IReadOnlyDictionary<string, (string Package, string Exe)> Included = new Dictionary<string, (string, string)>
    {
        ["blender"] = ("blender", "blender/blender.exe"),
        ["libreoffice"] = ("libreoffice", "libreoffice/App/libreoffice/program/soffice.exe"),
        ["krita"] = ("krita", "krita/bin/krita.exe"),
        ["inkscape"] = ("inkscape", "inkscape/bin/inkscape.exe"),
        ["kdenlive"] = ("kdenlive", "kdenlive/bin/kdenlive.exe"),
        ["audacity"] = ("audacity", "audacity/bin/Audacity4.exe"),
        ["obs"] = ("obs", "obs/bin/64bit/obs64.exe"),
        ["ffmpeg"] = ("ffmpeg", "ffmpeg/bin/ffmpeg.exe"),
        ["mpv"] = ("mpv", "mpv/mpv.exe"),
        ["sumatra"] = ("sumatra", "sumatra/SumatraPDF.exe"),
        ["qpdf"] = ("qpdf", "qpdf/bin/qpdf.exe"),
        ["7zip"] = ("7zip", "7zip/7z.exe"),
        ["tesseract"] = ("tesseract", "tesseract/tesseract.exe"),
        ["colmap"] = ("colmap", "colmap/bin/colmap.exe"),
        ["git"] = ("git", "git/cmd/git.exe"),
        ["python"] = ("python", "python/python.exe"),
        ["vscode"] = ("vscode", "vscode/VSCodium.exe"),
        ["scrcpy"] = ("scrcpy", "scrcpy/scrcpy.exe"),
        ["adb"] = ("scrcpy", "scrcpy/adb.exe"),
        ["ollama"] = ("ollama", "ollama/ollama.exe"),
        ["model"] = ("model", "model/manifests/registry.ollama.ai/library/qwen2.5/0.5b"),
    };

    /// <summary>Pfad des Hauptprogramms im Werkzeugordner, falls die Komponente mitgeliefert wird.</summary>
    public static string? IncludedPath(string toolsRoot, string id) =>
        Included.TryGetValue(id, out var i) ? Path.Combine(toolsRoot, i.Exe.Replace('/', Path.DirectorySeparatorChar)) : null;

    /// <summary>Liest das Manifest; null, wenn es fehlt oder kaputt ist. Dateinamen mit Pfadanteil werden abgelehnt.</summary>
    public static IReadOnlyList<Entry>? Load(string programmeDir)
    {
        var f = Path.Combine(programmeDir, ManifestName);
        if (!File.Exists(f)) return null;
        try
        {
            var m = JsonSerializer.Deserialize<ManifestFile>(File.ReadAllText(f));
            if (m?.Programs is not { Count: > 0 } list) return null;
            return list.All(e => !string.IsNullOrWhiteSpace(e.Id) && e.File == Path.GetFileName(e.File) && e.Sha256.Length == 64) ? list : null;
        }
        catch (JsonException) { return null; }
    }

    /// <summary>Erster Kandidat, in dem ein gültiges Manifest liegt – der Ordner selbst oder sein Unterordner „Programme“.</summary>
    public static string? Locate(IEnumerable<string?> candidates)
    {
        foreach (var c in candidates)
        {
            if (string.IsNullOrWhiteSpace(c)) continue;
            foreach (var d in new[] { c, Path.Combine(c, FolderName) })
                try { if (Load(d) is not null) return Path.GetFullPath(d); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { }
        }
        return null;
    }

    /// <summary>Übliche Orte: TRENDO_VOLLPAKET, die Ordner der aufrufenden Programme (Setup.exe), Laufwerkswurzeln und „Downloads“ samt „TrendoOS*“-Unterordnern.</summary>
    public static IEnumerable<string?> Candidates(IEnumerable<string>? callerDirs = null)
    {
        yield return Environment.GetEnvironmentVariable("TRENDO_VOLLPAKET");
        foreach (var d in callerDirs ?? []) yield return d;
        var roots = new List<string>();
        try { roots.AddRange(DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType is DriveType.Removable or DriveType.CDRom or DriveType.Fixed).Select(d => d.RootDirectory.FullName)); }
        catch (IOException) { }
        var dl = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        roots.Add(dl);
        foreach (var r in roots)
        {
            yield return r;
            string[] subs;
            try { subs = Directory.Exists(r) ? Directory.GetDirectories(r, "TrendoOS*") : []; }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { subs = []; }
            foreach (var s in subs) yield return s;
        }
    }

    /// <summary>Richtet ein Paket ein: Prüfsumme muss stimmen, entpackt wird nur nach „Werkzeugordner/&lt;Paket&gt;“.</summary>
    public static string InstallPackage(string programmeDir, Entry e, string toolsRoot, AuditLog? audit = null)
    {
        var zip = Path.Combine(programmeDir, e.File);
        if (!File.Exists(zip)) throw new InvalidOperationException($"{e.Name}: Datei {e.File} fehlt im Vollpaket.");
        Directory.CreateDirectory(toolsRoot);
        var dest = Sys.Software.InstallPortable(zip, e.Sha256, toolsRoot, e.Id, audit);
        audit?.Append("app", "vollpaket.install", $"{e.Id} {e.Version}");
        return dest;
    }

    /// <summary>Alle fehlenden Pakete einrichten (beim ersten Start nach dem Setup). Vorhandene bleiben unverändert. Liefert die Zahl der eingerichteten Pakete.</summary>
    public static int InstallMissing(string programmeDir, string toolsRoot, AuditLog? audit = null, Action<string>? progress = null)
    {
        var list = Load(programmeDir) ?? throw new InvalidOperationException("Im Vollpaket fehlt ein gültiges manifest.json.");
        var n = 0;
        foreach (var e in list)
        {
            var exe = Path.Combine(toolsRoot, e.Id, e.Exe.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(exe) || Directory.Exists(exe)) { progress?.Invoke($"{e.Name}: schon eingerichtet"); continue; }
            progress?.Invoke($"{e.Name} {e.Version} wird eingerichtet …");
            InstallPackage(programmeDir, e, toolsRoot, audit);
            n++;
        }
        return n;
    }

    /// <summary>Ein mitgeliefertes Programm einzeln entfernen (Vorgabe 2026-10-26): löscht nur den Paketordner im Werkzeugordner.
    /// Über das Vollpaket lässt es sich jederzeit wieder einrichten. Unbekannte Namen werden abgelehnt.</summary>
    public static bool Uninstall(string toolsRoot, string package, AuditLog? audit = null)
    {
        if (!Included.Values.Any(i => i.Package == package)) return false;
        var dir = new DirectoryInfo(Path.Combine(toolsRoot, package));
        if (!dir.Exists) return false;
        if (dir.Attributes.HasFlag(FileAttributes.ReparsePoint)) dir.Delete();
        else
        {
            foreach (var f in dir.EnumerateFiles("*", SearchOption.AllDirectories)) if (f.IsReadOnly) f.IsReadOnly = false;
            dir.Delete(true);
        }
        audit?.Append("app", "vollpaket.uninstall", package);
        return true;
    }

    /// <summary>Ollama findet das mitgelieferte Modell nur über OLLAMA_MODELS; gesetzt nur, wenn der Nutzer selbst keins im eigenen Ollama-Ordner hat.</summary>
    public static void ConfigureOllama(System.Diagnostics.ProcessStartInfo psi, string toolsRoot)
    {
        var own = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ollama", "models", "manifests");
        var bundled = Path.Combine(toolsRoot, "model");
        if (!Directory.Exists(own) && Directory.Exists(Path.Combine(bundled, "manifests"))) psi.Environment["OLLAMA_MODELS"] = bundled;
    }
}
