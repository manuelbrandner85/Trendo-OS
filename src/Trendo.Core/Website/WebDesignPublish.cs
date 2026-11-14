using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace Trendo.Core;

/// <summary>K8: Veröffentlichen als Paket oder Hochladen mit eigenem Konto. Erst nach bestandener Prüfung (Website-Prüfung, Prüfung auf dem Export, keine Geheimnisse in den Dateien)
/// und beim Hochladen nur nach Zustimmung im Einzelfall: Die Zustimmung gilt genau für die gezeigte Dateiliste (Fingerabdruck); ändert sich etwas, muss neu zugestimmt werden.
/// Trendo meldet danach, was hochgeladen wurde.</summary>
public static class WebDesignPublish
{
    public sealed record FileItem(string Rel, long Bytes);
    public sealed record Plan(IReadOnlyList<FileItem> Files, long Bytes, string Fingerprint, IReadOnlyList<string> Blockers, IReadOnlyList<string> Notes)
    {
        public bool Ok => Blockers.Count == 0;
    }

    /// <summary>Was zur Veröffentlichung gehört: der Export ohne interne Ordner (.trendo, .git) und ohne .env.</summary>
    public static IReadOnlyList<FileItem> Files(string exportDir)
    {
        if (!Directory.Exists(exportDir) || (File.GetAttributes(exportDir) & FileAttributes.ReparsePoint) != 0) return [];
        // Verknüpfungen (Junctions, Symlinks) werden nicht verfolgt: hochgeladen wird nur, was wirklich im Export-Ordner liegt.
        // Punkt-Dateien und -Ordner (.env, .git, .trendo …) und node_modules bleiben auf jeder Ebene draußen.
        return Directory.EnumerateFiles(exportDir, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = true })
            .Select(f => (Rel: Path.GetRelativePath(exportDir, f).Replace(Path.DirectorySeparatorChar, '/'), Full: f))
            .Where(x => !x.Rel.Split('/').Any(part => part.StartsWith('.') || part == "node_modules"))
            .OrderBy(x => x.Rel, StringComparer.Ordinal)
            .Select(x => new FileItem(x.Rel, new FileInfo(x.Full).Length)).ToList();
    }

    static readonly HashSet<string> TextTypes = [".html", ".htm", ".js", ".mjs", ".css", ".json", ".txt", ".md", ".xml", ".svg", ".map", ".csv", ".webmanifest", ".yml", ".yaml", ".ini", ".cfg", ".conf", ".env", ""];
    static readonly HashSet<string> BinaryTypes = [".png", ".jpg", ".jpeg", ".webp", ".gif", ".avif", ".ico", ".woff", ".woff2", ".ttf", ".otf", ".glb", ".gltf", ".mp4", ".webm", ".mp3", ".ogg", ".wav", ".pdf", ".zip", ".splat", ".spz", ".ksplat", ".ply"];

    /// <summary>Jede Datei prüfen: Textdateien vollständig (auch große, in Abschnitten), bekannte Binärformate nicht; alles andere sperrt.</summary>
    static IEnumerable<string> ScanAll(string exportDir, IReadOnlyList<FileItem> files)
    {
        foreach (var f in files)
        {
            var ext = Path.GetExtension(f.Rel).ToLowerInvariant();
            if (BinaryTypes.Contains(ext)) continue;
            if (!TextTypes.Contains(ext)) { yield return $"„{f.Rel}“ hat eine Dateiart, die Trendo nicht auf Geheimnisse prüfen kann. Bitte aus dem Export entfernen."; continue; }
            if (f.Bytes > 100_000_000) { yield return $"„{f.Rel}“ ist zu groß für die Prüfung (über 100 MB)."; continue; }
            if (f.Rel.StartsWith("assets/", StringComparison.Ordinal) && Path.GetFileName(f.Rel) is "editor.js" or "daten.js" or "LIZENZEN.txt") continue; // Programmteile von Trendo, unverändert aus dem Programm
            var text = File.ReadAllText(Path.Combine(exportDir, f.Rel.Replace('/', Path.DirectorySeparatorChar)));
            foreach (var s in SecurityCenter.ScanText(f.Rel, text).Take(3)) yield return $"Mögliches Geheimnis in {s.File} Zeile {s.Line} ({s.Kind}: {s.Masked}). Bitte entfernen, bevor etwas hochgeladen wird.";
            foreach (var k in WebDesignData.SecretKeys(text).Take(3)) yield return $"Geheimer Schlüssel in {f.Rel} ({k}). Geheime Schlüssel gehören nie in eine Website.";
        }
    }

    /// <summary>Fingerabdruck über Namen und Inhalt aller Dateien: dieselbe Zustimmung gilt nur für genau diese Dateien.</summary>
    public static string Fingerprint(string exportDir, IReadOnlyList<FileItem> files)
    {
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var f in files)
        {
            sha.AppendData(Encoding.UTF8.GetBytes(f.Rel + "\n"));
            sha.AppendData(SHA256.HashData(File.ReadAllBytes(Path.Combine(exportDir, f.Rel.Replace('/', Path.DirectorySeparatorChar)))));
        }
        return Convert.ToHexString(sha.GetHashAndReset());
    }

    /// <summary>Prüfen und Dateiliste zeigen. <paramref name="exportFindings"/>: Befunde der Prüfung auf dem Export (aus der App, nach dem Vorrendern).</summary>
    public static Plan Prepare(string projectDir, IReadOnlyList<string> exportFindings)
    {
        var dir = Path.Combine(projectDir, WebDesign.ExportDir);
        var blockers = new List<string>(); var notes = new List<string>();
        if (!File.Exists(Path.Combine(dir, "index.html"))) blockers.Add("Erst die Website exportieren.");
        foreach (var f in WebDesignSeo.Check(projectDir).Where(f => f.Ebene == "Fehler")) blockers.Add(f.Text);
        foreach (var f in exportFindings.Concat(WebDesignExport.CheckFolder(dir)).Distinct()) (f.Contains("nicht im HTML", StringComparison.Ordinal) ? notes : blockers).Add(f); // auch nach einem Neustart: Prüfung auf der Platte
        var files = Files(dir);
        blockers.AddRange(ScanAll(dir, files).Take(15));
        return new(files, files.Sum(f => f.Bytes), files.Count == 0 ? "" : Fingerprint(dir, files), blockers, notes);
    }

    /// <summary>Paket (ZIP) im Projekt anlegen, nur nach bestandener Prüfung. Es wird nichts hochgeladen.</summary>
    public static (string? Zip, string? Error) Package(string projectDir, Plan plan)
    {
        if (!plan.Ok) return (null, "Nicht verpackt, die Prüfung hat Fehler: " + plan.Blockers[0]);
        var dir = Path.Combine(projectDir, WebDesign.ExportDir);
        if (Fingerprint(dir, Files(dir)) != plan.Fingerprint) return (null, "Der Export hat sich seit der Prüfung geändert. Bitte neu prüfen.");
        var outDir = Path.Combine(projectDir, "pakete"); Directory.CreateDirectory(outDir);
        var zip = Path.Combine(outDir, $"website-{DateTime.Now:yyyyMMdd-HHmmss}.zip");
        using (var z = ZipFile.Open(zip, ZipArchiveMode.Create))
            foreach (var f in plan.Files) z.CreateEntryFromFile(Path.Combine(dir, f.Rel.Replace('/', Path.DirectorySeparatorChar)), f.Rel, CompressionLevel.Optimal);
        return (zip, null);
    }

    /// <summary>ZIP genau aus den gezeigten Dateien bauen und dabei den Fingerabdruck über dieselben Bytes prüfen (kein zweiter Durchlauf des Ordners, kein Wettlauf).</summary>
    public static (byte[]? Zip, string? Error) ZipExactly(string projectDir, Plan plan)
    {
        var dir = Path.Combine(projectDir, WebDesign.ExportDir);
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var ms = new MemoryStream();
        using (var z = new ZipArchive(ms, ZipArchiveMode.Create, true))
            foreach (var f in plan.Files)
            {
                var full = Path.Combine(dir, f.Rel.Replace('/', Path.DirectorySeparatorChar));
                if ((File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0) return (null, $"„{f.Rel}“ ist eine Verknüpfung und wird nicht hochgeladen.");
                var bytes = File.ReadAllBytes(full);
                sha.AppendData(Encoding.UTF8.GetBytes(f.Rel + "\n")); sha.AppendData(SHA256.HashData(bytes));
                var e = z.CreateEntry(f.Rel, CompressionLevel.Optimal);
                using var s = e.Open(); s.Write(bytes);
            }
        return Convert.ToHexString(sha.GetHashAndReset()) == plan.Fingerprint ? (ms.ToArray(), null) : (null, "Der Export hat sich nach deiner Zustimmung geändert. Bitte neu prüfen und zustimmen.");
    }

    /// <summary>Zustimmung prüfen: nur wenn genau dieser Fingerabdruck freigegeben wurde und die Dateien sich seitdem nicht geändert haben.</summary>
    public static string? CheckConsent(string projectDir, Plan plan, string? consentedFingerprint)
    {
        if (!plan.Ok) return "Nicht hochgeladen, die Prüfung hat Fehler: " + plan.Blockers[0];
        if (string.IsNullOrEmpty(consentedFingerprint) || consentedFingerprint != plan.Fingerprint) return "Nicht hochgeladen: Für genau diese Dateien fehlt deine Zustimmung.";
        var dir = Path.Combine(projectDir, WebDesign.ExportDir);
        if (Fingerprint(dir, Files(dir)) != plan.Fingerprint) return "Nicht hochgeladen: Der Export hat sich nach deiner Zustimmung geändert. Bitte neu prüfen und zustimmen.";
        return null;
    }

    /// <summary>Ehrlicher Bericht nach dem Hochladen.</summary>
    public static string Report(Plan plan, string target) =>
        $"Hochgeladen zu {target}: {plan.Files.Count} Dateien, {plan.Bytes / 1024.0:0.#} KB.\n" + string.Join("\n", plan.Files.Take(40).Select(f => $"• {f.Rel} ({f.Bytes / 1024.0:0.#} KB)")) + (plan.Files.Count > 40 ? $"\n• … und {plan.Files.Count - 40} weitere" : "");
}
