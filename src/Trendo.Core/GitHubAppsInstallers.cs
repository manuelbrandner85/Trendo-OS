using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>Befund 27.10.2026: Viele GitHub-Programme (Electron) liefern als „.exe“ einen Installer, kein Programm – „Starten“ öffnete dann nur den Installer.
/// Jetzt: Installer erkennen; NSIS-Installer und das reine Programmpaket (*.nsis.7z) mit 7-Zip in den Werkzeugordner auspacken;
/// andere Installer still für den aktuellen Benutzer ausführen (in der App) – seit T-0762 erst nach Signaturprüfung mit Ampel und eigenem Bestätigungsklick. Dazu: Einleitung und Funktionen aus der README für die Beschreibung.</summary>
public static partial class GitHubApps
{
    public enum InstallerKind { None, Nsis, Inno, Squirrel }

    /// <summary>Die Datei ist ein Installer, den Trendo nicht selbst auspacken kann – er wird still für diesen Benutzer ausgeführt.</summary>
    public sealed class InstallerOnlyException(InstallerKind kind, string file)
        : InvalidOperationException($"„{Path.GetFileName(file)}“ ist ein {kind}-Installer.")
    {
        public InstallerKind Kind { get; } = kind;
        public string File { get; } = file;
    }

    /// <summary>Erkennt gängige Windows-Installer an ihren Kennungen in den ersten Megabytes (Manifest/Ressourcen) und am Dateiende.</summary>
    public static InstallerKind DetectInstaller(string file)
    {
        var len = new FileInfo(file).Length;
        if (len < 2) return InstallerKind.None;
        using var fs = File.OpenRead(file);
        var head = new byte[(int)Math.Min(len, 6 << 20)]; fs.ReadExactly(head);
        var tail = Array.Empty<byte>();
        if (len > head.Length) { var n = (int)Math.Min(len - head.Length, 2 << 20); fs.Seek(-n, SeekOrigin.End); tail = new byte[n]; fs.ReadExactly(tail); }
        if (head[0] != 'M' || head[1] != 'Z') return InstallerKind.None;
        var text = Encoding.Latin1.GetString(head) + Encoding.Latin1.GetString(tail);
        if (text.Contains("Nullsoft.NSIS", StringComparison.Ordinal) || text.Contains("NullsoftInst", StringComparison.Ordinal)) return InstallerKind.Nsis;
        if (text.Contains("Inno Setup", StringComparison.Ordinal)) return InstallerKind.Inno;
        if (text.Contains("SquirrelSetup", StringComparison.Ordinal) || text.Contains("Squirrel.Windows", StringComparison.Ordinal)) return InstallerKind.Squirrel;
        return InstallerKind.None;
    }

    /// <summary>Stille Installation für den aktuellen Benutzer (ohne Admin, wo der Installer das anbietet).</summary>
    public static string[] SilentArgs(InstallerKind k) => k switch
    {
        InstallerKind.Nsis => ["/S", "/currentuser"],
        InstallerKind.Inno => ["/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/CURRENTUSER", "/SP-"],
        _ => [],
    };

    /// <summary>T-0762 (X-06): Ampel vor einem Installer-Lauf. Grün = gültig signiert, Gelb = Signatur nicht prüfbar, Rot = unsigniert oder ungültig.</summary>
    public enum Ampel { Gruen, Gelb, Rot }

    /// <summary>Ergebnis der Prüfung vor dem Installer-Lauf. <see cref="NeedsRiskConsent"/>: Gelb/Rot brauchen eine eigene ausdrückliche Bestätigung;
    /// <see cref="Blocked"/>: Datei fehlt – dann gibt es nichts auszuführen.</summary>
    public sealed record InstallerCheck(Ampel Ampel, string Title, string Text, string? Publisher, string Sha256, bool NeedsRiskConsent, bool Blocked);

    /// <summary>T-0762 (X-06): Installer vor dem Lauf prüfen – Authenticode-Signatur (Windows WinVerifyTrust, ohne Sperrlisten-Abfrage) und SHA-256.
    /// Der Installer läuft danach mit den Rechten der Person; Trendo führt ihn nie im selben Klick wie „Installieren“ aus.
    /// Mit <paramref name="locked"/> (aus <see cref="OpenLocked"/>) wird die Prüfsumme aus genau der gesperrten Datei gelesen (kein Austausch bis zum Start).</summary>
    public static InstallerCheck CheckInstaller(string file, InstallerKind kind, Func<string, Sys.SignatureInfo>? check = null, FileStream? locked = null) =>
        CheckFile(file, $"„{Path.GetFileName(file)}“ ist ein {kind}-Installer. Trendo kann ihn nicht selbst auspacken – er müsste als fremdes Programm mit deinen Rechten laufen und richtet das Programm außerhalb von Trendo ein (nur für dich, ohne Admin).", "Installer", check, locked);

    /// <summary>T-0762 (B2): fertiges Programm (aus ZIP oder einzelne .exe) vor dem ersten Start prüfen – gleiche Ampel wie beim Installer.</summary>
    public static InstallerCheck CheckProgram(string exe, Func<string, Sys.SignatureInfo>? check = null, FileStream? locked = null) =>
        CheckFile(exe, $"„{Path.GetFileName(exe)}“ ist ein fremdes Programm aus der Veröffentlichung auf GitHub. Beim Start läuft es mit deinen Rechten (ohne Admin).", "Programm", check, locked);

    /// <summary>N2/Z1: Dateiendungen, die ein Programm beim Start ausführen oder laden kann (Programme, Bibliotheken, Electron-Pakete, Skripte, Konfiguration, die Code nachlädt).</summary>
    public static readonly string[] ExecutableExt = [".exe", ".dll", ".sys", ".ocx", ".cpl", ".com", ".scr", ".msi", ".asar", ".node", ".ps1", ".psm1", ".bat", ".cmd", ".vbs", ".vbe", ".js", ".jse", ".wsf", ".hta", ".py", ".pyd", ".jar", ".lnk",
        ".ahk", ".config", ".manifest", ".local", ".mjs", ".cjs", ".pyc", ".pyw", ".pyz", "._pth", ".pth", ".zip", ".lua", ".class", ".wasm", ".tcl", ".rb", ".pl"];
    /// <summary>Z1: Endungen, die eine Authenticode-Signatur tragen können (Windows-Programme und -Bibliotheken, PowerShell, Installer). Alles andere aus <see cref="ExecutableExt"/> kann keine Signatur abdecken.</summary>
    public static readonly string[] SignableExt = [".exe", ".dll", ".sys", ".ocx", ".cpl", ".com", ".scr", ".msi", ".node", ".pyd", ".ps1", ".psm1"];
    /// <summary>Mehr ausführbare Dateien prüft Trendo nicht einzeln (dann höchstens Gelb).</summary>
    public const int MaxExecutables = 1500;

    /// <summary>N2: alle ausführbaren Inhalte eines Programmordners (ohne Verknüpfungen zu betreten), sortiert nach relativem Pfad.</summary>
    public static List<string> ExecutableFiles(string dir) =>
        Directory.Exists(dir)
            ? Directory.EnumerateFiles(dir, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint })
                .Where(f => ExecutableExt.Contains(Path.GetExtension(f).ToLowerInvariant())).OrderBy(f => Path.GetRelativePath(dir, f), StringComparer.OrdinalIgnoreCase).Take(MaxExecutables + 1).ToList()
            : [];

    /// <summary>Z2: Verknüpfungen (Junction, Symlink) im Programmordner – relativer Pfad und Ziel. Sie werden nie betreten.</summary>
    public static List<(string Rel, string Target)> ReparsePoints(string dir)
    {
        var res = new List<(string, string)>();
        if (!Directory.Exists(dir)) return res;
        var todo = new Stack<DirectoryInfo>(); todo.Push(new DirectoryInfo(dir));
        while (todo.Count > 0)
            foreach (var e in todo.Pop().EnumerateFileSystemInfos("*", new EnumerationOptions { IgnoreInaccessible = true, AttributesToSkip = 0 }))
            {
                if (e.Attributes.HasFlag(FileAttributes.ReparsePoint)) res.Add((Path.GetRelativePath(dir, e.FullName).Replace('\\', '/'), e.LinkTarget ?? "?"));
                else if (e is DirectoryInfo d) todo.Push(d);
            }
        return res.OrderBy(r => r.Item1, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>N2: Prüfsumme über die genannten Dateien (relativer Pfad + SHA-256 je Datei).</summary>
    public static string FolderHash(string dir, IReadOnlyList<string> files)
    {
        var sb = new StringBuilder();
        foreach (var f in files)
        {
            using var fs = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            sb.Append(Path.GetRelativePath(dir, f).Replace('\\', '/').ToLowerInvariant()).Append('\t').Append(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(fs))).Append('\n');
        }
        return Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
    }

    /// <summary>Z1a/Z2: Prüfsumme über ALLE Dateien des Programmordners (relativer Pfad + Inhalt) und alle Verknüpfungen (Pfad + Ziel).
    /// Jede Änderung, jede neue oder gelöschte Datei ändert sie – daran ist die Start-Erlaubnis gebunden.</summary>
    public static string FolderHashAll(string dir)
    {
        var files = Directory.Exists(dir)
            ? Directory.EnumerateFiles(dir, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint })
                .OrderBy(f => Path.GetRelativePath(dir, f), StringComparer.OrdinalIgnoreCase).ToList()
            : [];
        var sb = new StringBuilder(FolderHash(dir, files)).Append('\n');
        foreach (var (rel, target) in ReparsePoints(dir)) sb.Append("link\t").Append(rel.ToLowerInvariant()).Append('\t').Append(target).Append('\n');
        return Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
    }

    /// <summary>Z3c: Dateien zum Starten sperren (andere dürfen lesen, nicht schreiben/löschen/umbenennen). Freigabe mit Dispose.</summary>
    public sealed class FileLocks : IDisposable
    {
        readonly List<FileStream> _s = [];
        public FileLocks(IEnumerable<string> files) { try { foreach (var f in files) _s.Add(OpenLocked(f)); } catch { Dispose(); throw; } }
        public int Count => _s.Count;
        public void Dispose() { foreach (var s in _s) s.Dispose(); _s.Clear(); }
    }

    /// <summary>N2/Z1: Programm mit seinem ganzen Ordner prüfen. Grün nur, wenn die Startdatei und alle signierbaren Programme/Bibliotheken gültig signiert sind,
    /// es keine nicht signierbaren ausführbaren Inhalte (Skripte, Konfiguration, Pakete) und keine Verknüpfungen gibt. Sonst Gelb mit Namen; Startdatei unsigniert = Rot.
    /// <see cref="InstallerCheck.Sha256"/> ist die Prüfsumme über alle Dateien des Ordners (<see cref="FolderHashAll"/>).</summary>
    public static InstallerCheck CheckProgramFolder(string exe, string dir, Func<string, Sys.SignatureInfo>? check = null, FileStream? locked = null) =>
        CheckProgramFolder(exe, dir, Fingerprint(dir, ExecSet(dir, exe)), check); // L1: Ergebnis-Prüfsumme = Fingerabdruck (Index + ausführbare Inhalte)

    /// <summary>Z3a: Start-Erlaubnis für Programme, die nicht in der GitHub-Liste stehen (z. B. KI-Programme vom Hersteller), gebunden an die Ordner-Prüfsumme.</summary>
    public const string ExternalApprovalsFile = "start-freigaben.json";
    public static string? ExternalApproval(string toolsRoot, string key)
    {
        var f = Path.Combine(toolsRoot, ExternalApprovalsFile);
        try { return File.Exists(f) && JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(f)) is { } d && d.TryGetValue(key, out var h) ? h : null; }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException) { return null; }
    }
    public static void ApproveExternal(string toolsRoot, string key, string hash)
    {
        Directory.CreateDirectory(toolsRoot);
        var f = Path.Combine(toolsRoot, ExternalApprovalsFile);
        Dictionary<string, string> d;
        try { d = File.Exists(f) ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(f)) ?? [] : []; } catch (JsonException) { d = []; }
        d[key] = hash;
        AtomicFile.WriteAllText(f, JsonSerializer.Serialize(d, new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>Z3b: Installer eines Herstellers (KI-Programme) vor dem Start prüfen – gleiche Ampel.</summary>
    public static InstallerCheck CheckVendorInstaller(string file, string name, Func<string, Sys.SignatureInfo>? check = null, FileStream? locked = null) =>
        CheckFile(file, $"„{Path.GetFileName(file)}“ ist der Installer des Herstellers für {name}. Er läuft mit deinen Rechten und zeigt danach seine eigenen Fenster.", "Installer", check, locked);

    /// <summary>Datei zum Prüfen und Starten sperren: andere dürfen lesen, aber nicht schreiben, löschen oder umbenennen.</summary>
    public static FileStream OpenLocked(string file) => new(file, FileMode.Open, FileAccess.Read, FileShare.Read);

    /// <summary>SHA-256 aus einem offenen (gesperrten) Datenstrom.</summary>
    public static string Sha256Of(FileStream fs) { fs.Position = 0; var h = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(fs)); fs.Position = 0; return h; }

    static InstallerCheck CheckFile(string file, string what, string noun, Func<string, Sys.SignatureInfo>? check, FileStream? locked)
    {
        string sha;
        if (locked is not null) sha = Sha256Of(locked);
        else if (File.Exists(file)) { using var fs = OpenLocked(file); sha = Sha256Of(fs); }
        else sha = "";
        var sig = (check ?? Sys.Authenticode.Check)(file);
        return sig.State switch
        {
            Sys.SigState.Gueltig => new(Ampel.Gruen, $"Signiert von {sig.Publisher ?? "einem unbekannten Herausgeber"}",
                $"{what} Die Signatur ist gültig ({sig.Detail}). Das sagt, wer die Datei gebaut hat und dass sie seitdem nicht verändert wurde – nicht, dass sie harmlos ist. Sperrlisten fragt Trendo nicht ab.", sig.Publisher, sha, false, false),
            Sys.SigState.NichtPruefbar => new(Ampel.Gelb, "Signatur nicht prüfbar", $"{what} {sig.Detail} Nur ausführen, wenn du dem Projekt vertraust.", null, sha, true, false),
            Sys.SigState.Fehlt => new(Ampel.Rot, $"{noun} fehlt", "Die Datei ist nicht mehr da – es gibt nichts auszuführen.", null, sha, true, true),
            Sys.SigState.Unsigniert => new(Ampel.Rot, "Nicht signiert", $"{what} Die Datei trägt keine Signatur: Niemand bestätigt, wer sie gebaut hat oder dass sie unverändert ist. Nur ausführen, wenn du dem Projekt ausdrücklich vertraust.", null, sha, true, false),
            _ => new(Ampel.Rot, "Signatur ungültig", $"{what} Die Signatur ist ungültig: {sig.Detail} Trendo rät davon ab, die Datei auszuführen.", sig.Publisher, sha, true, false),
        };
    }

    /// <summary>T-0762 (B2): Erlaubnis für den ersten Start merken – gebunden an die SHA-256 der Programmdatei (ändert sie sich, fragt Trendo erneut).</summary>
    public static bool ApproveStart(string toolsRoot, string id, string sha)
    {
        var list = List(toolsRoot);
        if (list.FirstOrDefault(i => i.Id == id) is not { } e) return false;
        Save(toolsRoot, list.Select(i => i.Id == id ? e with { StartApproved = sha } : i));
        return true;
    }

    /// <summary>Electron-NSIS-Installer enthalten das Programm als „app-64.7z“ (bzw. arm64/32) – das passende Paket im ausgepackten Ordner.</summary>
    public static string? NsisPayload(string extractedDir, bool arm64 = false)
    {
        var all = Directory.EnumerateFiles(extractedDir, "app-*.7z", SearchOption.AllDirectories).ToList();
        string? Find(string n) => all.FirstOrDefault(f => Path.GetFileName(f).Equals(n, StringComparison.OrdinalIgnoreCase));
        return arm64 ? Find("app-arm64.7z") ?? Find("app-64.7z") : Find("app-64.7z") ?? Find("app-32.7z") ?? all.FirstOrDefault();
    }

    /// <summary>Ein per Installer eingerichtetes Programm in die Liste eintragen (Programmpfad außerhalb des Werkzeugordners).</summary>
    public static Installed Register(Principal by, string toolsRoot, string repo, Release release, Asset asset, string exe, string sha, string license, string description, AuditLog? audit = null)
    {
        Rbac.DemandSoftware(by, "github.install " + repo, audit); // T-0764
        if (!ValidRepo(repo)) throw new ArgumentException("Ungültiger Projektname.", nameof(repo));
        var entry = new Installed(IdFor(repo), repo, release.Tag, Path.GetFullPath(exe), license, asset.Name, sha, description, DateTimeOffset.Now);
        Save(toolsRoot, List(toolsRoot).Where(i => i.Id != entry.Id).Append(entry));
        audit?.Append(by.Name, "github.install.installer", $"{repo} {release.Tag} {asset.Name} sha256={sha} → {Path.GetFileName(exe)}");
        return entry;
    }

    /// <summary>Programmpfad eines Eintrags (im Werkzeugordner oder – bei Installern – absolut).</summary>
    public static string ExePath(string toolsRoot, Installed e) => Path.IsPathRooted(e.Exe) ? e.Exe : Path.Combine(toolsRoot, e.Id, e.Exe.Replace('/', Path.DirectorySeparatorChar));

    // ---------- README: Einleitung und Funktionen ----------
    static readonly Regex Badge = new(@"!\[[^\]]*\]\([^)]*\)|\[!\[[^\]]*\]\([^)]*\)\]\([^)]*\)", RegexOptions.Compiled);
    static readonly Regex Html = new(@"<[^>]+>", RegexOptions.Compiled);
    static readonly Regex Link = new(@"\[([^\]]+)\]\([^)]*\)", RegexOptions.Compiled);

    static string Clean(string s) => Regex.Replace(Link.Replace(Html.Replace(Badge.Replace(s, ""), ""), "$1").Replace("**", "").Replace("__", "").Replace("`", ""), @"\s+", " ").Trim();

    /// <summary>Die ersten aussagekräftigen Absätze der README (ohne Abzeichen, Bilder, HTML, Überschriften), höchstens <paramref name="max"/> Zeichen.</summary>
    public static string ReadmeIntro(string markdown, int max = 700)
    {
        var paras = Regex.Split(markdown.Replace("\r", ""), @"\n\s*\n").Select(p => p.Trim())
            .Where(p => p.Length > 0 && !p.StartsWith('#') && !p.StartsWith("```", StringComparison.Ordinal) && !p.StartsWith('|') && !p.StartsWith("- ") && !p.StartsWith("* "))
            .Select(Clean).Where(p => p.Length >= 50 && p.Count(char.IsLetter) > p.Length / 2).Take(2).ToList();
        var s = string.Join(" ", paras);
        return s.Length <= max ? s : s[..s.LastIndexOf(' ', max)] + " …";
    }

    /// <summary>Stichpunkte unter einer Überschrift wie „Features“ / „Funktionen“ (höchstens <paramref name="max"/>).</summary>
    public static List<string> ReadmeFeatures(string markdown, int max = 10)
    {
        var lines = markdown.Replace("\r", "").Split('\n');
        var res = new List<string>(); var inside = false;
        foreach (var raw in lines)
        {
            var l = raw.Trim();
            if (l.StartsWith('#')) { if (inside && res.Count > 0) break; inside = Regex.IsMatch(l, @"feature|funktion|highlights|what.*can", RegexOptions.IgnoreCase); continue; }
            if (inside && (l.StartsWith("- ") || l.StartsWith("* ") || Regex.IsMatch(l, @"^\d+\. ")))
            {
                var t = Clean(Regex.Replace(l, @"^(-|\*|\d+\.)\s+", "")); if (t.Length > 2) res.Add(t.Length > 160 ? t[..157] + "…" : t);
                if (res.Count >= max) break;
            }
        }
        return res;
    }

    public static string ReadmeUrl(string repo) => ValidRepo(repo) ? $"https://api.github.com/repos/{repo}/readme" : throw new ArgumentException("Ungültiger Projektname.", nameof(repo));
}
