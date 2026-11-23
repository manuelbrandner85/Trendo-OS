using System.Net;
using System.Runtime.Versioning;
using System.Text;
using System.Text.RegularExpressions;

namespace Trendo.Core.Sys;

public enum Ampel { Gruen, Gelb, Rot }

/// <summary>Ein Eintrag, der beim Start oder als Programm auf diesem PC steht. <see cref="File"/>: die Datei, die tatsächlich startet (null = nicht bestimmbar).</summary>
public sealed record StartEntry(string Kind, string Name, string Where, string Command, string? File, string? DeclaredPublisher = null);

/// <summary>Bewertung eines Eintrags: Ampel, kurzer Grund, Erklärung in Alltagssprache und was die Person selbst tun kann.</summary>
public sealed record Finding(StartEntry Entry, SignatureInfo Signature, Ampel Light, string Reason, string Explanation, string Tip);

/// <summary>T-0755 SPEC-12 „System auf Gefahren prüfen“: liest NUR – Autostart-Ordner, Run-Schlüssel der Registrierung, geplante Aufgaben und
/// installierte Programme – und prüft je Eintrag die Datei samt Authenticode-Signatur. Trendo ändert nichts; die Hinweise sagen, was die Person selbst tun kann.</summary>
public static partial class SystemScan
{
    public const string KindStartup = "Autostart-Ordner", KindRun = "Run-Schlüssel", KindTask = "Geplante Aufgabe", KindProgram = "Installiertes Programm";

    /// <summary>Herausgeber, die Trendo als bekannte Firmen einstuft (Teil des Zertifikatsnamens). Gültig signiert + bekannt = grün.</summary>
    public static readonly string[] KnownPublishers =
    [
        "Microsoft", "Google", "Mozilla", "Apple", "Adobe", "NVIDIA", "Intel", "Advanced Micro Devices", "AMD", "Realtek", "Logitech", "Valve", "Epic Games",
        "Oracle", "Dropbox", "Zoom", "Spotify", "Discord", "Corsair", "Razer", "Dell", "HP Inc", "Hewlett", "Lenovo", "ASUSTeK", "Micro-Star", "Samsung",
        "Brave Software", "Opera", "Python Software Foundation", "GitHub", "Docker", "JetBrains", "Autodesk", "Blender Foundation", "Ubisoft", "Electronic Arts",
        "Riot Games", "TeamViewer", "Anthropic", "OpenJS Foundation", "Node.js", "VideoLAN", "Notepad++", "Elgato", "Steelseries", "Malwarebytes",
    ];

    /// <summary>Programme, die nur Skripte oder Bibliotheken starten – die eigentliche Wirkung steckt in den Argumenten.</summary>
    static readonly string[] ScriptHosts = ["cmd.exe", "powershell.exe", "pwsh.exe", "wscript.exe", "cscript.exe", "mshta.exe", "conhost.exe"];

    /// <summary>Ganzes Wort, nicht nur Teil davon: „Intel“ passt zu „Intel Corporation“, nicht zu „Intelligent Software“.</summary>
    public static bool KnownPublisher(string? publisher) => publisher is { Length: > 0 } p &&
        KnownPublishers.Any(k => Regex.IsMatch(p, @"(?<![\p{L}\p{N}])" + Regex.Escape(k) + @"(?![\p{L}\p{N}])", RegexOptions.IgnoreCase));

    /// <summary>Orte, an denen dauerhaft startende Programme normalerweise nichts zu suchen haben.</summary>
    public static string? SuspiciousPlace(string path)
    {
        string Norm(string p) { try { return Path.GetFullPath(p).TrimEnd('\\', '/') + Path.DirectorySeparatorChar; } catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException) { return p; } }
        var f = Norm(path);
        bool Under(string? dir) => dir is { Length: > 3 } && f.StartsWith(Norm(dir), StringComparison.OrdinalIgnoreCase);
        if (Under(Path.GetTempPath()) || Under(Environment.ExpandEnvironmentVariables("%SystemRoot%\\Temp"))) return "liegt im Ordner für temporäre Dateien (%TEMP%)";
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (home.Length > 0 && Under(Path.Combine(home, "Downloads"))) return "liegt im Download-Ordner";
        if (f.Contains("\\$Recycle.Bin\\", StringComparison.OrdinalIgnoreCase)) return "liegt im Papierkorb";
        if (Under(Environment.ExpandEnvironmentVariables("%PUBLIC%"))) return "liegt im öffentlichen Benutzerordner";
        return null;
    }

    [GeneratedRegex(@"-(e|ec|en|enc|encodedcommand)\s+[A-Za-z0-9+/=]{16,}", RegexOptions.IgnoreCase)] private static partial Regex Encoded();

    /// <summary>Welche Datei startet ein Befehl? Mit Anführungszeichen, Umgebungsvariablen, Leerzeichen im Pfad ohne Anführungszeichen,
    /// rundll32 (die Bibliothek zählt) und Programmen ohne Ordner (Suche im Windows-Ordner und im PATH).</summary>
    public static string? FileFromCommand(string command, Func<string, bool>? exists = null)
    {
        exists ??= System.IO.File.Exists;
        var c = Environment.ExpandEnvironmentVariables(command.Trim());
        if (c.Length == 0) return null;
        string first, rest;
        if (c[0] == '"') { var e = c.IndexOf('"', 1); first = e > 0 ? c[1..e] : c[1..]; rest = e > 0 ? c[(e + 1)..].Trim() : ""; }
        else
        {
            var parts = c.Split(' '); first = parts[0]; rest = string.Join(' ', parts.Skip(1)); var found = false;
            for (var i = 1; i <= parts.Length && !found; i++) // „C:\Program Files\X\x.exe -a“ ohne Anführungszeichen
            {
                var cand = string.Join(' ', parts.Take(i));
                if (exists(cand)) { first = cand; rest = string.Join(' ', parts.Skip(i)); found = true; }
                else if (!cand.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && exists(cand + ".exe")) { first = cand + ".exe"; rest = string.Join(' ', parts.Skip(i)); found = true; }
            }
            if (!found && c.IndexOf(".exe", StringComparison.OrdinalIgnoreCase) is var x and > 0) { first = c[..(x + 4)]; rest = c[(x + 4)..].Trim(); } // Datei fehlt: bis „.exe“
        }
        first = Resolve(first, exists);
        if (Path.GetFileName(first).Equals("rundll32.exe", StringComparison.OrdinalIgnoreCase) && rest.Length > 0)
        {
            while (rest.Length > 1 && rest[0] is '/' or '-') { var sp = rest.IndexOf(' '); rest = sp < 0 ? "" : rest[(sp + 1)..].TrimStart(); } // Schalter wie „/d“ überspringen
            var dll = rest.TrimStart('"'); var cut = dll.IndexOfAny([',', '"']); if (cut > 0) dll = dll[..cut];
            return Resolve(dll.Trim(), exists);
        }
        return first;
    }

    static string Resolve(string file, Func<string, bool> exists)
    {
        if (file.Length == 0 || Path.IsPathRooted(file)) return file;
        var name = Path.HasExtension(file) ? file : file + ".exe";
        var dirs = new[] { Environment.SystemDirectory, Environment.GetFolderPath(Environment.SpecialFolder.Windows), Environment.GetFolderPath(Environment.SpecialFolder.SystemX86) }
            .Concat((Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator)).Where(d => d.Length > 0);
        foreach (var d in dirs) { var p = Path.Combine(d, name); if (exists(p)) return p; }
        return file;
    }

    /// <summary>Bewertet einen Eintrag (reine Regel, ohne Systemzugriff – die Signatur kommt von außen).</summary>
    public static Finding Assess(StartEntry e, SignatureInfo s)
    {
        var who = s.Publisher ?? e.DeclaredPublisher;
        if (e.File is null)
            return new(e, s, Ampel.Gelb, "Datei nicht bestimmbar",
                e.Kind == KindProgram ? "Der Eintrag nennt keine Programmdatei. Trendo kann deshalb nicht prüfen, wer das Programm gemacht hat." : "Trendo kann aus dem Befehl keine Programmdatei ablesen und ihn deshalb nicht prüfen.",
                "Wenn du den Namen nicht kennst: im Internet nach dem Namen suchen oder in Windows unter Einstellungen → Apps nachsehen.");
        if (s.State == SigState.Fehlt || !System.IO.File.Exists(e.File))
            return new(e, s, Ampel.Rot, "Datei fehlt",
                "Der Eintrag zeigt auf eine Datei, die es auf diesem PC nicht gibt. Meist ist das ein Rest nach dem Deinstallieren – manchmal versteckt sich so auch ein Schadprogramm, das schon entfernt wurde.",
                e.Kind switch
                {
                    KindStartup => "Den Eintrag im Autostart-Ordner kannst du selbst löschen: Windows-Taste + R, „shell:startup“ eingeben, Verknüpfung löschen.",
                    KindRun => "In Windows unter Einstellungen → Apps → Autostart nachsehen und den Eintrag dort ausschalten. Trendo ändert die Registrierung nicht.",
                    KindTask => "In der Aufgabenplanung von Windows (taskschd.msc) die Aufgabe ansehen und, wenn du sie nicht kennst, deaktivieren.",
                    _ => "In Windows unter Einstellungen → Apps prüfen, ob das Programm noch da ist. Ein Rest-Eintrag lässt sich dort oft entfernen.",
                });
        var place = SuspiciousPlace(e.File);
        var file = Path.GetFileName(e.File);
        var fix = e.Kind switch
        {
            KindStartup => "Wenn du das nicht kennst: Windows-Taste + R, „shell:startup“ eingeben und die Verknüpfung löschen. Danach die Datei mit dem Virenschutz prüfen (Rechtsklick → Mit Microsoft Defender überprüfen).",
            KindRun => "Wenn du das nicht kennst: in Windows unter Einstellungen → Apps → Autostart ausschalten und die Datei mit dem Virenschutz prüfen. Trendo ändert nichts selbst.",
            KindTask => "Wenn du das nicht kennst: in der Aufgabenplanung (taskschd.msc) deaktivieren und die Datei mit dem Virenschutz prüfen.",
            _ => "Wenn du das Programm nicht kennst: in Windows unter Einstellungen → Apps deinstallieren und vorher mit dem Virenschutz prüfen.",
        };
        if (place is not null)
            return new(e, s, Ampel.Rot, "Verdächtiger Ort",
                $"„{file}“ {place}. Programme, die dauerhaft starten, liegen normalerweise unter „Programme“ oder im Windows-Ordner. Schadprogramme verstecken sich gern an solchen Orten.", fix);
        if (Encoded().IsMatch(e.Command))
            return new(e, s, Ampel.Rot, "Versteckter Befehl",
                "Der Befehl enthält einen verschlüsselten (Base64-)Befehl für PowerShell. So verstecken Schadprogramme, was sie tun.", fix);
        if (s.State == SigState.Ungueltig)
            return new(e, s, Ampel.Rot, "Signatur ungültig",
                $"„{file}“ trägt eine Signatur, die nicht stimmt: {s.Detail} Eine echte Firmen-Datei hat eine gültige Signatur.", fix);
        var host = ScriptHosts.Contains(file, StringComparer.OrdinalIgnoreCase);
        if (s.State == SigState.Gueltig && KnownPublisher(who) && !host)
            return new(e, s, Ampel.Gruen, "Gültig signiert",
                $"„{file}“ ist gültig signiert von {who}. Die Datei ist seit dem Signieren unverändert.", "Nichts zu tun. Wenn du das Programm beim Start nicht brauchst, kannst du es unter Einstellungen → Apps → Autostart ausschalten.");
        if (host)
            return new(e, s, Ampel.Gelb, "Startet ein Skript",
                $"„{file}“ ist ein Windows-Werkzeug, das Befehle oder Skripte ausführt. Was genau passiert, steht im Befehl – das kann Trendo nicht sicher bewerten.", "Befehl lesen: Kennst du das Skript oder den Ordner? Wenn nicht, " + fix[(fix.IndexOf(':') + 2)..]);
        if (s.State == SigState.Gueltig)
            return new(e, s, Ampel.Gelb, "Herausgeber unbekannt",
                $"„{file}“ ist gültig signiert von „{who ?? "unbekannt"}“. Diesen Herausgeber kennt Trendo nicht – das heißt nicht, dass er schlecht ist.", "Kennst du den Herausgeber? Wenn ja, ist alles in Ordnung. " + fix);
        return new(e, s, Ampel.Gelb, s.State == SigState.Unsigniert ? "Nicht signiert" : "Nicht prüfbar",
            s.State == SigState.Unsigniert ? $"„{file}“ ist nicht signiert. Viele kleine und freie Programme sind unsigniert – aber auch Schadprogramme. Trendo kann nicht sagen, wer die Datei gemacht hat." : $"„{file}“ ließ sich nicht prüfen: {s.Detail}",
            "Kennst du das Programm und hast es selbst installiert? Dann ist es wohl in Ordnung. " + fix);
    }

    // ---------- Quellen (nur lesen) ----------

    /// <summary>Autostart-Ordner des Benutzers und aller Benutzer.</summary>
    public static IReadOnlyList<string> StartupFolders() =>
    [
        Environment.GetFolderPath(Environment.SpecialFolder.Startup),
        Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup),
    ];

    /// <summary>Einträge in Autostart-Ordnern. Verknüpfungen (.lnk) werden gelesen, nicht gestartet.</summary>
    public static IEnumerable<StartEntry> FromStartupFolders(IEnumerable<string> folders)
    {
        foreach (var d in folders.Where(d => d.Length > 0 && Directory.Exists(d)))
        {
            string[] files; try { files = Directory.GetFiles(d); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { continue; }
            foreach (var f in files.Order())
            {
                var n = Path.GetFileName(f);
                if (n.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
                if (Path.GetExtension(f).Equals(".lnk", StringComparison.OrdinalIgnoreCase))
                {
                    var l = Shortcut.Read(f);
                    var target = l?.Target is { Length: > 0 } t ? Environment.ExpandEnvironmentVariables(t) : null;
                    yield return new(KindStartup, Path.GetFileNameWithoutExtension(f), f, target is null ? "(Verknüpfung ohne lesbares Ziel)" : $"\"{target}\" {l?.Arguments}".Trim(), target is null ? null : FileFromCommand($"\"{target}\" {l?.Arguments}"));
                }
                else yield return new(KindStartup, n, f, f, f);
            }
        }
    }

    public static readonly (string Hive, string Path)[] RunKeys =
    [
        ("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Run"), ("HKCU", @"Software\Microsoft\Windows\CurrentVersion\RunOnce"),
        ("HKLM", @"Software\Microsoft\Windows\CurrentVersion\Run"), ("HKLM", @"Software\Microsoft\Windows\CurrentVersion\RunOnce"),
        ("HKLM", @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run"), ("HKLM", @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\RunOnce"),
    ];

    /// <summary>Run-Schlüssel nur LESEN (Schlüssel werden schreibgeschützt geöffnet).</summary>
    [SupportedOSPlatform("windows")]
    public static IEnumerable<StartEntry> FromRunKeys()
    {
        var list = new List<StartEntry>();
        foreach (var (hive, path) in RunKeys)
        {
            try
            {
                var root = hive == "HKCU" ? Microsoft.Win32.Registry.CurrentUser : Microsoft.Win32.Registry.LocalMachine;
                using var k = root.OpenSubKey(path, writable: false);
                if (k is null) continue;
                foreach (var n in k.GetValueNames())
                    if (k.GetValue(n) is string cmd && cmd.Trim().Length > 0)
                        list.Add(new(KindRun, n.Length > 0 ? n : "(Standard)", $@"{hive}\{path}", cmd, FileFromCommand(cmd)));
            }
            catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or IOException) { }
        }
        return list;
    }

    /// <summary>Geplante Aufgaben nur LESEN (Aufgabenplanung über ihre Windows-Schnittstelle). Aufgaben ohne Programmstart (z. B. COM) werden gezählt, nicht bewertet.</summary>
    [SupportedOSPlatform("windows")]
    public static IReadOnlyList<StartEntry> FromScheduledTasks(out int skipped)
    {
        var list = new List<StartEntry>(); skipped = 0;
        try
        {
            var t = Type.GetTypeFromProgID("Schedule.Service"); if (t is null) return list;
            dynamic svc = Activator.CreateInstance(t)!; svc.Connect();
            var stack = new Stack<dynamic>(); stack.Push(svc.GetFolder("\\"));
            var n = 0;
            while (stack.Count > 0 && n < 5000)
            {
                var folder = stack.Pop();
                try { foreach (var sub in folder.GetFolders(0)) stack.Push(sub); } catch (System.Runtime.InteropServices.COMException) { } catch (UnauthorizedAccessException) { }
                dynamic tasks; try { tasks = folder.GetTasks(1); } catch (System.Runtime.InteropServices.COMException) { continue; } catch (UnauthorizedAccessException) { continue; }
                foreach (var task in tasks)
                {
                    n++;
                    try
                    {
                        string name = task.Path; bool enabled = task.Enabled; var any = false;
                        string? author = null; try { author = task.Definition.RegistrationInfo.Author; } catch (System.Runtime.InteropServices.COMException) { }
                        foreach (var a in task.Definition.Actions)
                        {
                            if ((int)a.Type != 0) continue; // 0 = Programm starten
                            string exe = a.Path ?? ""; string args = a.Arguments ?? ""; any = true;
                            var cmd = (exe.Contains(' ') && !exe.StartsWith('"') ? $"\"{exe}\"" : exe) + (args.Length > 0 ? " " + args : "");
                            list.Add(new(KindTask, name + (enabled ? "" : " (ausgeschaltet)"), "Aufgabenplanung", cmd, FileFromCommand(cmd), author is { Length: > 0 } && !author.StartsWith('$') ? author : null));
                        }
                        if (!any) skipped++;
                    }
                    catch (Exception e) when (e is System.Runtime.InteropServices.COMException or UnauthorizedAccessException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException) { skipped++; }
                }
            }
        }
        catch (Exception e) when (e is System.Runtime.InteropServices.COMException or UnauthorizedAccessException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException or InvalidCastException) { }
        return list;
    }

    static readonly (string Hive, string Path)[] UninstallKeys =
    [
        ("HKLM", @"Software\Microsoft\Windows\CurrentVersion\Uninstall"), ("HKLM", @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
        ("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Uninstall"),
    ];

    /// <summary>Installierte Programme aus den Deinstallations-Einträgen (nur lesen). Prüfdatei: das Programmsymbol (meist die Programmdatei), sonst keine.</summary>
    [SupportedOSPlatform("windows")]
    public static IEnumerable<StartEntry> FromInstalledPrograms()
    {
        var list = new List<StartEntry>(); var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (hive, path) in UninstallKeys)
        {
            try
            {
                var root = hive == "HKCU" ? Microsoft.Win32.Registry.CurrentUser : Microsoft.Win32.Registry.LocalMachine;
                using var k = root.OpenSubKey(path, writable: false); if (k is null) continue;
                foreach (var sub in k.GetSubKeyNames())
                {
                    using var s = k.OpenSubKey(sub, writable: false); if (s is null) continue;
                    if (s.GetValue("DisplayName") is not string name || name.Trim().Length == 0) continue;
                    if (s.GetValue("SystemComponent") is int sc && sc == 1 || s.GetValue("ParentKeyName") is string) continue;
                    var ver = s.GetValue("DisplayVersion") as string ?? "";
                    if (!seen.Add(name + "|" + ver)) continue;
                    var icon = (s.GetValue("DisplayIcon") as string ?? "").Trim();
                    string? file = null;
                    if (icon.Length > 0)
                    {
                        var p = icon.Trim('"'); var comma = p.LastIndexOf(','); if (comma > 2 && int.TryParse(p[(comma + 1)..].Trim(), out _)) p = p[..comma];
                        p = Environment.ExpandEnvironmentVariables(p.Trim().Trim('"'));
                        if (p.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && !Path.GetFileName(p).Equals("msiexec.exe", StringComparison.OrdinalIgnoreCase)) file = p;
                    }
                    list.Add(new(KindProgram, name.Trim() + (ver.Length > 0 ? " " + ver : ""), $@"{hive}\{path}\{sub}", icon.Length > 0 ? icon : (s.GetValue("InstallLocation") as string ?? ""), file, s.GetValue("Publisher") as string));
                }
            }
            catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or IOException) { }
        }
        return list.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Was geprüft wird. Tests setzen eigene Quellen ein (z. B. einen eigenen Autostart-Ordner).</summary>
    public sealed record Sources(IReadOnlyList<string> StartupFolders, bool RunKeys = true, bool Tasks = true, bool Programs = true)
    {
        public static Sources System => new(SystemScan.StartupFolders());
    }

    public sealed record Result(DateTimeOffset At, IReadOnlyList<Finding> Findings, int TasksWithoutProgram)
    {
        public int Count(Ampel a) => Findings.Count(f => f.Light == a);
    }

    /// <summary>Alles lesen und bewerten. Jede Datei wird nur einmal geprüft (Zwischenspeicher je Pfad).</summary>
    public static Result Run(Sources src, Func<string, SignatureInfo>? check = null, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        check ??= Authenticode.Check;
        var entries = new List<StartEntry>(); var skipped = 0;
        progress?.Report("Autostart-Ordner …"); entries.AddRange(FromStartupFolders(src.StartupFolders));
        if (OperatingSystem.IsWindows())
        {
            if (src.RunKeys) { progress?.Report("Run-Schlüssel …"); entries.AddRange(FromRunKeys()); }
            if (src.Tasks) { progress?.Report("Geplante Aufgaben …"); entries.AddRange(FromScheduledTasks(out skipped)); }
            if (src.Programs) { progress?.Report("Installierte Programme …"); entries.AddRange(FromInstalledPrograms()); }
        }
        var cache = new Dictionary<string, SignatureInfo>(StringComparer.OrdinalIgnoreCase);
        var res = new List<Finding>(); var i = 0;
        foreach (var e in entries)
        {
            ct.ThrowIfCancellationRequested();
            if (++i % 10 == 0) progress?.Report($"Signaturen prüfen … {i} von {entries.Count}");
            var sig = e.File is null ? new SignatureInfo(SigState.NichtPruefbar, null, "Keine Programmdatei angegeben.")
                : cache.TryGetValue(e.File, out var c) ? c : cache[e.File] = check(e.File);
            res.Add(Assess(e, sig));
        }
        return new(DateTimeOffset.Now, res.OrderBy(f => f.Light switch { Ampel.Rot => 0, Ampel.Gelb => 1, _ => 2 }).ThenBy(f => f.Entry.Kind).ThenBy(f => f.Entry.Name, StringComparer.OrdinalIgnoreCase).ToList(), skipped);
    }

    public static string LightText(Ampel a) => a switch { Ampel.Rot => "Rot", Ampel.Gelb => "Gelb", _ => "Grün" };
    static string SigText(SignatureInfo s) => s.State switch
    {
        SigState.Gueltig => "gültig" + (s.ViaCatalog ? " (Windows-Katalog)" : ""), SigState.Unsigniert => "nicht signiert", SigState.Ungueltig => "ungültig", SigState.Fehlt => "Datei fehlt", _ => "nicht prüfbar",
    };

    /// <summary>Bericht als eigenständige HTML-Seite (öffnet in jedem Browser, nichts wird nachgeladen).</summary>
    public static string ReportHtml(Result r)
    {
        static string H(string? s) => WebUtility.HtmlEncode(s ?? "");
        var sb = new StringBuilder("<!DOCTYPE html><html lang=\"de\"><head><meta charset=\"utf-8\"><title>Trendo – System auf Gefahren geprüft</title><style>")
            .Append("body{font-family:Segoe UI,Arial,sans-serif;background:#14110c;color:#eee;margin:24px}h1{color:#e8c26a}table{border-collapse:collapse;width:100%}td,th{border-bottom:1px solid #3a3326;padding:6px;text-align:left;vertical-align:top;font-size:13px}")
            .Append(".Rot{color:#f87171;font-weight:bold}.Gelb{color:#facc15;font-weight:bold}.Grün{color:#4ade80;font-weight:bold}small{color:#aaa}</style></head><body>")
            .Append($"<h1>System auf Gefahren geprüft</h1><p>{r.At:dd.MM.yyyy HH:mm} · {r.Findings.Count} Einträge · Rot {r.Count(Ampel.Rot)} · Gelb {r.Count(Ampel.Gelb)} · Grün {r.Count(Ampel.Gruen)}</p>")
            .Append("<p><small>Trendo hat nur gelesen und nichts geändert. Grün = gültig signiert von bekanntem Herausgeber. Gelb = nicht signiert oder Herausgeber unbekannt. Rot = Datei fehlt, Signatur ungültig oder verdächtiger Ort.</small></p>")
            .Append("<table><tr><th>Ampel</th><th>Art</th><th>Name</th><th>Datei</th><th>Herausgeber</th><th>Signatur</th><th>Erklärung</th><th>Was du tun kannst</th></tr>");
        foreach (var f in r.Findings)
        {
            var l = LightText(f.Light);
            sb.Append($"<tr><td class=\"{l}\">{l}<br><small>{H(f.Reason)}</small></td><td>{H(f.Entry.Kind)}</td><td>{H(f.Entry.Name)}<br><small>{H(f.Entry.Where)}</small></td>")
              .Append($"<td>{H(f.Entry.File ?? "–")}<br><small>{H(f.Entry.Command)}</small></td><td>{H(f.Signature.Publisher ?? f.Entry.DeclaredPublisher ?? "–")}</td><td>{H(SigText(f.Signature))}</td><td>{H(f.Explanation)}</td><td>{H(f.Tip)}</td></tr>");
        }
        if (r.TasksWithoutProgram > 0) sb.Append($"</table><p><small>{r.TasksWithoutProgram} geplante Aufgaben starten kein Programm (z. B. Windows-interne Dienste) und wurden nicht bewertet.</small></p>");
        else sb.Append("</table>");
        return sb.Append("</body></html>").ToString();
    }

    /// <summary>Bericht als CSV (Semikolon, für Tabellenprogramme).</summary>
    public static string ReportCsv(Result r)
    {
        static string C(string? s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";
        var sb = new StringBuilder("Ampel;Grund;Art;Name;Ort;Datei;Befehl;Herausgeber;Signatur;Erklärung;Tipp\r\n");
        foreach (var f in r.Findings)
            sb.Append(string.Join(';', C(LightText(f.Light)), C(f.Reason), C(f.Entry.Kind), C(f.Entry.Name), C(f.Entry.Where), C(f.Entry.File), C(f.Entry.Command), C(f.Signature.Publisher ?? f.Entry.DeclaredPublisher), C(SigText(f.Signature)), C(f.Explanation), C(f.Tip))).Append("\r\n");
        return sb.ToString();
    }
}

/// <summary>Liest das Ziel einer Windows-Verknüpfung (.lnk) direkt aus der Datei (Format MS-SHLLINK) – ohne sie zu öffnen oder zu starten.</summary>
public static class Shortcut
{
    public sealed record Link(string? Target, string? Arguments, string? WorkingDir);

    public static Link? Read(string lnk)
    {
        try { return Parse(System.IO.File.ReadAllBytes(lnk), Path.GetDirectoryName(lnk) ?? ""); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentOutOfRangeException or IndexOutOfRangeException) { return null; }
    }

    public static Link? Parse(byte[] b, string baseDir = "")
    {
        if (b.Length < 0x4C || BitConverter.ToUInt32(b, 0) != 0x4C) return null;
        var flags = BitConverter.ToUInt32(b, 20); var pos = 0x4C;
        bool F(uint m) => (flags & m) != 0;
        if (F(0x1)) pos += 2 + BitConverter.ToUInt16(b, pos);
        string? target = null;
        if (F(0x2))
        {
            var start = pos; var size = (int)BitConverter.ToUInt32(b, pos); var hdr = BitConverter.ToUInt32(b, pos + 4); var lif = BitConverter.ToUInt32(b, pos + 8);
            if ((lif & 1) != 0)
            {
                var basePath = hdr >= 0x24 && BitConverter.ToUInt32(b, pos + 28) is var uo and > 0 ? Uni(b, start + (int)uo) : Ansi(b, start + (int)BitConverter.ToUInt32(b, pos + 16));
                var suffix = hdr >= 0x24 && BitConverter.ToUInt32(b, pos + 32) is var us and > 0 ? Uni(b, start + (int)us) : Ansi(b, start + (int)BitConverter.ToUInt32(b, pos + 24));
                target = basePath + suffix;
            }
            pos = start + size;
        }
        var uni = F(0x80);
        string? Str(uint m) { if (!F(m)) return null; var n = BitConverter.ToUInt16(b, pos); pos += 2; var s = uni ? Encoding.Unicode.GetString(b, pos, n * 2) : Encoding.Latin1.GetString(b, pos, n); pos += uni ? n * 2 : n; return s; }
        Str(0x4); var rel = Str(0x8); var wd = Str(0x10); var args = Str(0x20); Str(0x40);
        // Zusatzblöcke: Umgebungsvariablen-Ziel (z. B. „%ProgramFiles%\…“)
        while (target is null && pos + 8 <= b.Length)
        {
            var size = (int)BitConverter.ToUInt32(b, pos); if (size < 8 || pos + size > b.Length) break;
            if (BitConverter.ToUInt32(b, pos + 4) == 0xA0000001 && size >= 8 + 260 + 520) { var t = Encoding.Unicode.GetString(b, pos + 8 + 260, 520); target = t[..Math.Max(0, t.IndexOf('\0') is var z and >= 0 ? z : t.Length)]; }
            pos += size;
        }
        if (target is null && rel is { Length: > 0 }) target = Path.GetFullPath(Path.Combine(baseDir, rel));
        return new(target, args, wd);
    }

    static string Ansi(byte[] b, int at) { var e = Array.IndexOf(b, (byte)0, at); return Encoding.Latin1.GetString(b, at, (e < 0 ? b.Length : e) - at); }
    static string Uni(byte[] b, int at) { var e = at; while (e + 1 < b.Length && (b[e] != 0 || b[e + 1] != 0)) e += 2; return Encoding.Unicode.GetString(b, at, e - at); }
}
