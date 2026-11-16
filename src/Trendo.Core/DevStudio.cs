using System.Text;

namespace Trendo.Core;

/// <summary>CORE-07: Software-Studio – Projektvorlagen anlegen, Git-Stand lesen, bauen/testen mit vorhandenen Werkzeugen.</summary>
public sealed record DevTemplate(string Id, string Name, string Tool, string Description);
public sealed record GitState(bool IsRepo, string Branch, int Changed, IReadOnlyList<string> Files, string LastCommit);

public static class DevStudio
{
    public static readonly IReadOnlyList<DevTemplate> Templates =
    [
        new("web", "Web-App (HTML, CSS, JavaScript)", "", "Eine Einkaufsliste, die sofort im Browser läuft – mit Test und kleinem Server, wenn Node eingerichtet ist."),
        new("python", "Python-Programm", "python3", "Wertet Zahlen aus (Summe, Mittelwert, Median) – mit Tests."),
        new("dotnet-console", "C#-Programm für die Konsole (.NET)", "dotnet", "Ein Konsolenprogramm mit Rechenkern und eigenem Testprojekt."),
        new("dotnet-desktop", "C#-Programm mit Fenster (.NET, Avalonia)", "dotnet", "Ein Einheiten-Umrechner mit Fenster und eigenem Testprojekt."),
        new("node", "JavaScript-Programm (Node)", "node", "Zählt Wörter und Zeichen – mit Tests."),
    ];

    public static string SafeName(string name)
    {
        var s = new string(name.Trim().Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '-').ToArray()).Trim('-');
        return string.IsNullOrEmpty(s) ? "projekt" : s.Length > 60 ? s[..60] : s;
    }

    /// <summary>Legt das Projekt aus der Vorlage an. Überschreibt nie einen vorhandenen Ordner.</summary>
    public static string Create(string parent, string templateId, string name)
    {
        var t = Templates.FirstOrDefault(x => x.Id == templateId) ?? throw new ArgumentException("Unbekannte Vorlage: " + templateId);
        var safe = SafeName(name);
        var dir = Path.Combine(parent, safe);
        if (Directory.Exists(dir)) throw new IOException($"Den Ordner „{safe}“ gibt es schon.");
        Directory.CreateDirectory(dir);
        void W(string rel, string text) { var p = Path.Combine(dir, rel); Directory.CreateDirectory(Path.GetDirectoryName(p)!); File.WriteAllText(p, text.Replace("\r\n", "\n"), new UTF8Encoding(false)); }
        // T-0688: echte Vorlagen mit kleiner Funktion und Test (Inhalte in DevTemplates)
        var lower = safe.ToLowerInvariant();
        foreach (var (rel, text) in DevTemplates.Files(t.Id))
            W(rel.Replace("__SAFE__", safe), text.Replace("__HTML__", System.Net.WebUtility.HtmlEncode(name)).Replace("__NAME__", name.Replace("\"", "'").Replace("\\", "/")).Replace("__SAFE__", safe).Replace("__LOWER__", lower));
        return dir;
    }

    /// <summary>Was „Testen“ für den Projektordner bedeutet (erkannt an den Dateien). T-0688: bei .NET ein echter Testlauf (dotnet test) statt nur Bauen;
    /// ohne Testprojekt bzw. ohne Tests gibt es keinen Befehl.</summary>
    public static (string Exe, string[] Args)? TestCommand(string dir) => DevBuild.Test(DevBuild.Detect(dir)).Cmd is { } c ? (c.Exe, c.Args) : null;

    public static Cli.Result RunTests(string dir, int timeoutMs = 180000)
    {
        var plan = DevBuild.Test(DevBuild.Detect(dir));
        return plan.Cmd is { } c ? Cli.Run(c.Exe, c.Args, c.Cwd, timeoutMs, c.Env, c.Utf8 ? new UTF8Encoding(false) : null) : new(-1, "", plan.Blocked ?? "Für dieses Projekt kennt Trendo keinen Test.");
    }

    public static Cli.Result GitInit(string dir)
    {
        var r = Cli.Run("git", ["init", "-q", "-b", "main"], dir);
        if (!r.Ok) return r;
        Cli.Run("git", ["add", "-A"], dir);
        return Cli.Run("git", ["-c", "user.name=Trendo", "-c", "user.email=trendo@localhost", "commit", "-q", "-m", "Erster Stand"], dir);
    }

    public static GitState Git(string dir)
    {
        var top = Cli.Run("git", ["rev-parse", "--is-inside-work-tree"], dir, 10000);
        if (!top.Ok || top.Out.Trim() != "true") return new(false, "", 0, [], "");
        var br = Cli.Run("git", ["branch", "--show-current"], dir, 10000).Out.Trim();
        var st = Cli.Run("git", ["status", "--porcelain"], dir, 10000).Out.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.Length > 3 ? l[3..] : l).ToList();
        var last = Cli.Run("git", ["log", "-1", "--format=%s (%cr)"], dir, 10000);
        return new(true, br, st.Count, st, last.Ok ? last.Out.Trim() : "noch kein Stand");
    }

    public static Cli.Result Commit(string dir, string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return new(-1, "", "Bitte eine kurze Beschreibung eingeben.");
        Cli.Run("git", ["add", "-A"], dir);
        return Cli.Run("git", ["-c", "user.name=Trendo", "-c", "user.email=trendo@localhost", "commit", "-q", "-m", message], dir);
    }
}
