using System.Collections.Concurrent;
using System.IO.Enumeration;
using System.Text;
using System.Text.Json;

namespace Trendo.Core;

/// <summary>T-0762 L1: schnelle Start-Prüfung. Statt bei jedem Start den ganzen Ordner zu lesen:
/// Index über alle Dateien (relativer Pfad, Größe, Änderungs- und Erstellzeit – nur Verzeichnisdaten, kein Lesen) plus volle SHA-256 nur der ausführbaren Inhalte.
/// Signaturergebnisse werden je Datei-SHA-256 nur im Speicher zwischengespeichert (ein Cache auf der Platte könnte „signiert“ vortäuschen).</summary>
public static partial class GitHubApps
{
    /// <summary>Fingerabdruck eines Programmordners. <see cref="Key"/> ist das, woran die Start-Erlaubnis gebunden wird.</summary>
    public sealed record StartPrint(string Index, string ExecHash, IReadOnlyDictionary<string, string> ExecSha, int FileCount, int LinkCount)
    {
        public string Key => $"v2:{Index}:{ExecHash}";
    }

    /// <summary>Index über alle Dateien des Ordners aus den Verzeichnisdaten (ohne die Dateien zu lesen) und alle Verknüpfungen (Pfad + Ziel).</summary>
    public static (string Index, int Files, int Links) FolderIndex(string dir, CancellationToken ct = default)
    {
        var rows = new List<string>();
        if (Directory.Exists(dir))
        {
            var root = Path.GetFullPath(dir);
            var en = new FileSystemEnumerable<string>(root, (ref FileSystemEntry e) =>
                    $"{Path.GetRelativePath(root, e.ToFullPath()).Replace('\\', '/').ToLowerInvariant()}\t{e.Length}\t{e.LastWriteTimeUtc.UtcTicks}\t{e.CreationTimeUtc.UtcTicks}\t{(int)e.Attributes}",
                new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint })
            { ShouldIncludePredicate = (ref FileSystemEntry e) => !e.IsDirectory };
            foreach (var r in en) { ct.ThrowIfCancellationRequested(); rows.Add(r); }
        }
        rows.Sort(StringComparer.Ordinal);
        var links = ReparsePoints(dir);
        var sb = new StringBuilder();
        foreach (var r in rows) sb.Append(r).Append('\n');
        foreach (var (rel, target) in links) sb.Append("link\t").Append(rel.ToLowerInvariant()).Append('\t').Append(target).Append('\n');
        return (Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()))), rows.Count, links.Count);
    }

    /// <summary>L3: package.json mit „main“ (Node/Electron lädt die genannte Datei als Programm).</summary>
    public static List<string> PackageMains(string dir) =>
        Directory.Exists(dir)
            ? Directory.EnumerateFiles(dir, "package.json", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint })
                .Take(200).Where(f =>
                {
                    try { using var d = JsonDocument.Parse(File.ReadAllText(f)); return d.RootElement.ValueKind == JsonValueKind.Object && d.RootElement.TryGetProperty("main", out _); }
                    catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException) { return true; } // nicht lesbar: lieber nennen
                }).ToList()
            : [];

    /// <summary>Fingerabdruck: Index (alle Dateien, nur Verzeichnisdaten) + SHA-256 der ausführbaren Inhalte (diese werden ohnehin gesperrt).
    /// <paramref name="progress"/> meldet (fertig, gesamt) für die gelesenen ausführbaren Dateien.</summary>
    public static StartPrint Fingerprint(string dir, IReadOnlyList<string> exec, CancellationToken ct = default, IProgress<(int Done, int Total)>? progress = null)
    {
        var (index, files, links) = FolderIndex(dir, ct);
        var sha = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var sb = new StringBuilder();
        var i = 0;
        foreach (var f in exec.OrderBy(f => Path.GetRelativePath(dir, f), StringComparer.OrdinalIgnoreCase))
        {
            ct.ThrowIfCancellationRequested();
            using var fs = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 1 << 16, FileOptions.SequentialScan);
            var h = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(fs));
            sha[Path.GetFullPath(f)] = h;
            sb.Append(Path.GetRelativePath(dir, f).Replace('\\', '/').ToLowerInvariant()).Append('\t').Append(h).Append('\n');
            progress?.Report((++i, exec.Count));
        }
        return new(index, Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()))), sha, files, links);
    }

    /// <summary>L1c: Signaturergebnis je Datei-SHA-256, nur im Speicher dieses Trendo-Laufs.</summary>
    static readonly ConcurrentDictionary<string, Sys.SignatureInfo> SigCache = new(StringComparer.Ordinal);
    public static int SignatureCacheCount => SigCache.Count;

    /// <summary>Signatur prüfen, gemerkt je Inhalt (SHA-256). Mit Ersatzprüfung (Tests) wird nichts gemerkt.</summary>
    static Sys.SignatureInfo SigOf(string file, string? sha, Func<string, Sys.SignatureInfo>? check)
    {
        if (check is not null || sha is null) return (check ?? Sys.Authenticode.Check)(file);
        if (SigCache.TryGetValue(sha, out var s)) return s;
        s = Sys.Authenticode.Check(file);
        if (s.State is Sys.SigState.Gueltig or Sys.SigState.Unsigniert or Sys.SigState.Ungueltig) SigCache[sha] = s;
        return s;
    }

    /// <summary>L1: Ordnerprüfung mit vorhandenem Fingerabdruck (kein zweites Lesen). <see cref="InstallerCheck.Sha256"/> = <see cref="StartPrint.Key"/>.</summary>
    public static InstallerCheck CheckProgramFolder(string exe, string dir, StartPrint print, Func<string, Sys.SignatureInfo>? check = null, CancellationToken ct = default, IProgress<(int Done, int Total)>? progress = null)
    {
        var exeSha = print.ExecSha.TryGetValue(Path.GetFullPath(exe), out var es) ? es : null;
        var msig = SigOf(exe, exeSha, check);
        var mainText = $"„{Path.GetFileName(exe)}“ ist ein fremdes Programm aus der Veröffentlichung auf GitHub. Beim Start läuft es mit deinen Rechten (ohne Admin).";
        var main = msig.State switch
        {
            Sys.SigState.Gueltig => new InstallerCheck(Ampel.Gruen, $"Signiert von {msig.Publisher ?? "einem unbekannten Herausgeber"}", mainText, msig.Publisher, print.Key, false, false),
            Sys.SigState.NichtPruefbar => new InstallerCheck(Ampel.Gelb, "Signatur nicht prüfbar", $"{mainText} {msig.Detail} Nur ausführen, wenn du dem Projekt vertraust.", null, print.Key, true, false),
            Sys.SigState.Fehlt => new InstallerCheck(Ampel.Rot, "Programm fehlt", "Die Datei ist nicht mehr da – es gibt nichts auszuführen.", null, print.Key, true, true),
            Sys.SigState.Unsigniert => new InstallerCheck(Ampel.Rot, "Nicht signiert", $"{mainText} Die Datei trägt keine Signatur: Niemand bestätigt, wer sie gebaut hat oder dass sie unverändert ist. Nur ausführen, wenn du dem Projekt ausdrücklich vertraust.", null, print.Key, true, false),
            _ => new InstallerCheck(Ampel.Rot, "Signatur ungültig", $"{mainText} Die Signatur ist ungültig: {msig.Detail} Trendo rät davon ab, die Datei auszuführen.", msig.Publisher, print.Key, true, false),
        };
        var files = print.ExecSha.Keys.ToList();
        bool IsExe(string f) => string.Equals(f, Path.GetFullPath(exe), StringComparison.OrdinalIgnoreCase);
        var others = files.Where(f => !IsExe(f)).OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
        string Rel(string f) => Path.GetRelativePath(dir, f).Replace('\\', '/');
        static string Names(IEnumerable<string> n) { var l = n.ToList(); return string.Join(", ", l.Take(5)) + (l.Count > 5 ? $" und {l.Count - 5} weitere" : ""); }
        var scope = $" Geprüft wird der ganze Programmordner ({print.FileCount} Datei(en), davon {files.Count} ausführbar); die Start-Erlaubnis gilt nur, solange sich keine Datei im Ordner ändert.";
        if (main.Ampel != Ampel.Gruen) return main with { Text = main.Text + scope };
        if (files.Count > MaxExecutables)
            return main with { Ampel = Ampel.Gelb, Title = "Startdatei signiert, Ordner zu groß zum Prüfen", Text = main.Text + scope + $" Es sind mehr als {MaxExecutables} – Trendo prüft nicht alle und kann den Ordner deshalb nicht bestätigen.", NeedsRiskConsent = true };
        var signable = others.Where(f => SignableExt.Contains(Path.GetExtension(f).ToLowerInvariant())).ToList();
        var unsigned = new List<string>(); var k = 0;
        foreach (var f in signable)
        {
            ct.ThrowIfCancellationRequested();
            if (SigOf(f, print.ExecSha[f], check).State != Sys.SigState.Gueltig) unsigned.Add(f);
            progress?.Report((++k, signable.Count));
        }
        var unsignable = others.Where(f => !SignableExt.Contains(Path.GetExtension(f).ToLowerInvariant())).Concat(PackageMains(dir)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var links = ReparsePoints(dir);
        var problems = new List<string>();
        if (unsigned.Count > 0) problems.Add($"Nicht gültig signiert: {Names(unsigned.Select(Rel))}.");
        if (unsignable.Count > 0) problems.Add($"{unsignable.Count} Datei(en) nicht signierbar/prüfbar (Skripte, Konfiguration, Pakete, die das Programm ausführen oder laden kann): {Names(unsignable.Select(Rel))}.");
        if (links.Count > 0) problems.Add($"Verknüpfung(en) im Ordner, die woandershin zeigen (die Erlaubnis wird deshalb nicht gespeichert – Trendo fragt jedes Mal): {Names(links.Select(l => $"{l.Rel} → {l.Target}"))}.");
        var signer = main.Publisher ?? "einem unbekannten Herausgeber";
        if (problems.Count == 0)
            return main with { Text = $"„{Path.GetFileName(exe)}“ ist ein fremdes Programm von GitHub und läuft mit deinen Rechten (ohne Admin). Geprüft: Programme und Bibliotheken sind signiert von {signer} ({files.Count} Datei(en)). Daten- und Skriptdateien kann eine Signatur nicht abdecken. Sperrlisten fragt Trendo nicht ab." + scope };
        var n = unsigned.Count + unsignable.Count + links.Count;
        return main with { Ampel = Ampel.Gelb, Title = $"Startdatei signiert, {n} weitere Datei(en) nicht bestätigt", NeedsRiskConsent = true,
            Text = $"„{Path.GetFileName(exe)}“ ist signiert von {signer}. " + string.Join(" ", problems) + " Das Programm kann diese beim Start laden oder ausführen – die Signatur der Startdatei deckt sie nicht ab." + scope };
    }

    /// <summary>Alle ausführbaren Inhalte (inkl. Startdatei), die vor dem Start gesperrt und gehasht werden.</summary>
    public static List<string> ExecSet(string dir, string exe)
    {
        var l = ExecutableFiles(dir).Select(Path.GetFullPath).ToList();
        if (!l.Contains(Path.GetFullPath(exe), StringComparer.OrdinalIgnoreCase)) l.Add(Path.GetFullPath(exe));
        return l;
    }
}
