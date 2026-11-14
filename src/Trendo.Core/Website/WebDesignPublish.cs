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
        if (!Directory.Exists(exportDir)) return [];
        return Directory.EnumerateFiles(exportDir, "*", SearchOption.AllDirectories)
            .Select(f => (Rel: Path.GetRelativePath(exportDir, f).Replace('\\', '/'), Full: f))
            .Where(x => !x.Rel.StartsWith(".trendo/", StringComparison.Ordinal) && !x.Rel.StartsWith(".git/", StringComparison.Ordinal) && x.Rel != ".env")
            .OrderBy(x => x.Rel, StringComparer.Ordinal)
            .Select(x => new FileItem(x.Rel, new FileInfo(x.Full).Length)).ToList();
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
        foreach (var f in exportFindings) (f.Contains("nicht im HTML", StringComparison.Ordinal) ? notes : blockers).Add(f);
        if (Directory.Exists(dir))
            foreach (var s in SecurityCenter.ScanFolder(dir).Take(10)) blockers.Add($"Mögliches Geheimnis in {s.File} Zeile {s.Line} ({s.Kind}: {s.Masked}). Bitte entfernen, bevor etwas hochgeladen wird.");
        var files = Files(dir);
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
