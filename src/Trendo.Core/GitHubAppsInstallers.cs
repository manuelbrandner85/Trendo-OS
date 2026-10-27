using System.Text;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>Befund 27.10.2026: Viele GitHub-Programme (Electron) liefern als „.exe“ einen Installer, kein Programm – „Starten“ öffnete dann nur den Installer.
/// Jetzt: Installer erkennen; NSIS-Installer und das reine Programmpaket (*.nsis.7z) mit 7-Zip in den Werkzeugordner auspacken;
/// andere Installer still für den aktuellen Benutzer ausführen (in der App). Dazu: Einleitung und Funktionen aus der README für die Beschreibung.</summary>
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

    /// <summary>Electron-NSIS-Installer enthalten das Programm als „app-64.7z“ (bzw. arm64/32) – das passende Paket im ausgepackten Ordner.</summary>
    public static string? NsisPayload(string extractedDir, bool arm64 = false)
    {
        var all = Directory.EnumerateFiles(extractedDir, "app-*.7z", SearchOption.AllDirectories).ToList();
        string? Find(string n) => all.FirstOrDefault(f => Path.GetFileName(f).Equals(n, StringComparison.OrdinalIgnoreCase));
        return arm64 ? Find("app-arm64.7z") ?? Find("app-64.7z") : Find("app-64.7z") ?? Find("app-32.7z") ?? all.FirstOrDefault();
    }

    /// <summary>Ein per Installer eingerichtetes Programm in die Liste eintragen (Programmpfad außerhalb des Werkzeugordners).</summary>
    public static Installed Register(string toolsRoot, string repo, Release release, Asset asset, string exe, string sha, string license, string description, AuditLog? audit = null)
    {
        if (!ValidRepo(repo)) throw new ArgumentException("Ungültiger Projektname.", nameof(repo));
        var entry = new Installed(IdFor(repo), repo, release.Tag, Path.GetFullPath(exe), license, asset.Name, sha, description, DateTimeOffset.Now);
        Save(toolsRoot, List(toolsRoot).Where(i => i.Id != entry.Id).Append(entry));
        audit?.Append("app", "github.install.installer", $"{repo} {release.Tag} {asset.Name} sha256={sha} → {Path.GetFileName(exe)}");
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
