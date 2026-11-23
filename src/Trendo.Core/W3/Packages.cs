using System.Globalization;
using System.Text.RegularExpressions;

namespace Trendo.Core;

public sealed record PackageInfo(string Name, string Id, string Version, string? Available = null, string Source = "");

/// <summary>Ein Paketverwalter des Betriebssystems (winget unter Windows, flatpak unter Linux).</summary>
public interface IPackageBackend
{
    string Name { get; }
    string Exe { get; }
    Task<IReadOnlyList<PackageInfo>> SearchAsync(string q, CancellationToken ct = default);
    Task<IReadOnlyList<PackageInfo>> InstalledAsync(CancellationToken ct = default);
    Task<IReadOnlyList<PackageInfo>> UpgradesAsync(CancellationToken ct = default);
    Task<(bool Ok, string Log)> InstallAsync(string id, Action<string>? line = null, CancellationToken ct = default);
    Task<(bool Ok, string Log)> UpgradeAsync(string id, Action<string>? line = null, CancellationToken ct = default);
    Task<(bool Ok, string Log)> UninstallAsync(string id, Action<string>? line = null, CancellationToken ct = default);
    /// <summary>T-0755: der genaue Befehl, den Trendo für diese Änderung startet – wird vorher und während des Laufs angezeigt.</summary>
    string CommandLine(string action, string id) => $"{Exe} {action} {id}";
    /// <summary>T-0755: Nachprüfung nach dem Lauf – steht das Paket in der Liste der installierten Programme? null = nicht prüfbar.</summary>
    async Task<bool?> IsInstalledAsync(string id, CancellationToken ct = default) =>
        (await InstalledAsync(ct)).Any(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
    /// <summary>T-0755 (Gegenprüfung): installierte Version nach dem Lauf – null = nicht installiert oder nicht prüfbar.</summary>
    async Task<string?> InstalledVersionAsync(string id, CancellationToken ct = default) =>
        (await InstalledAsync(ct)).FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase))?.Version;
}

/// <summary>T-0755: Paketverwaltung, die den Rückgabewert (Exitcode) des letzten Laufs nennt.</summary>
public interface IPackageExitCode { int? LastExitCode { get; } }

public static class PackageParsers
{
    /// <summary>winget-Tabellen (search/list/upgrade): Spalten über die Kopfzeile, Trennlinie aus ----.</summary>
    public static IReadOnlyList<PackageInfo> ParseWinget(string output)
    {
        var lines = output.Replace("\r", "").Split('\n').Select(l => l.Contains('\b') ? l[(l.LastIndexOf('\b') + 1)..] : l).ToList();
        var sep = lines.FindIndex(l => l.Trim().Length > 10 && l.Trim().All(c => c == '-'));
        if (sep < 1) return [];
        var head = lines[sep - 1];
        int Col(params string[] names) { foreach (var n in names) { var i = head.IndexOf(n, StringComparison.Ordinal); if (i >= 0) return i; } return -1; }
        var cName = 0; var cId = Col("Id", "ID"); var cVer = Col("Version"); var cAvail = Col("Available", "Verfügbar"); var cSrc = Col("Source", "Quelle");
        if (cId < 0 || cVer < 0) return [];
        string Cut(string l, int a, int b) => a < 0 || a >= l.Length ? "" : l[a..Math.Min(l.Length, b < 0 ? l.Length : b)].Trim();
        var res = new List<PackageInfo>();
        foreach (var l in lines.Skip(sep + 1))
        {
            if (l.Trim().Length == 0 || l.Length < cVer || char.IsDigit(l.TrimStart()[0]) && l.Contains("upgrades available")) continue;
            if (!l.Contains(' ') || l.TrimStart().StartsWith("The following") ) continue;
            var next = new[] { cAvail, cSrc }.Where(x => x > cVer).DefaultIfEmpty(-1).Min();
            var p = new PackageInfo(Cut(l, cName, cId), Cut(l, cId, cVer), Cut(l, cVer, next), cAvail > 0 ? NullIfEmpty(Cut(l, cAvail, cSrc > cAvail ? cSrc : -1)) : null, cSrc > 0 ? Cut(l, cSrc, -1) : "");
            if (p.Id.Length > 0 && !p.Id.Contains(' ')) res.Add(p);
        }
        return res;
    }

    /// <summary>flatpak mit --columns=name,application,version(,branch): tabulatorgetrennt.</summary>
    public static IReadOnlyList<PackageInfo> ParseFlatpak(string output) =>
        output.Replace("\r", "").Split('\n').Where(l => l.Contains('\t')).Select(l => l.Split('\t'))
            .Where(c => c.Length >= 2 && c[1].Contains('.')).Select(c => new PackageInfo(c[0].Trim(), c[1].Trim(), c.Length > 2 ? c[2].Trim() : "", null, "flathub")).ToList();

    private static string? NullIfEmpty(string s) => s.Length == 0 ? null : s;

    /// <summary>T-0755: Steht die Paket-ID in einer winget-Liste (Ausgabe von „winget list --id … -e“)? „Nichts gefunden“ → false.</summary>
    public static bool ListedIn(string wingetListOutput, string id) =>
        ParseWinget(wingetListOutput).Any(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>T-0755: Fortschritt aus einer Ausgabezeile von winget (0–1) – „12.0 MB / 40.0 MB“ oder „45%“. Zeilen mit \r: das letzte Stück zählt.</summary>
    public static double? Progress(string line)
    {
        var l = CleanLine(line);
        var m = Regex.Match(l, @"(\d+(?:[.,]\d+)?)\s*(B|KB|MB|GB)\s*/\s*(\d+(?:[.,]\d+)?)\s*(B|KB|MB|GB)", RegexOptions.IgnoreCase);
        static double Bytes(string n, string u) => double.Parse(n.Replace(',', '.'), CultureInfo.InvariantCulture) * u.ToUpperInvariant() switch { "KB" => 1e3, "MB" => 1e6, "GB" => 1e9, _ => 1 };
        if (m.Success) { var t = Bytes(m.Groups[3].Value, m.Groups[4].Value); return t <= 0 ? null : Math.Clamp(Bytes(m.Groups[1].Value, m.Groups[2].Value) / t, 0, 1); }
        var p = Regex.Match(l, @"(?<![\d.,])(\d{1,3})\s*%");
        return p.Success && int.Parse(p.Groups[1].Value, CultureInfo.InvariantCulture) is var v and <= 100 ? v / 100.0 : null;
    }

    /// <summary>T-0755: Ausgabezeile lesbar machen – nur das Stück nach dem letzten \r, ohne Rückschritt-Zeichen (Drehsymbol).</summary>
    public static string CleanLine(string line)
    {
        var l = line.TrimEnd('\r', '\n');
        var r = l.LastIndexOf('\r'); if (r >= 0) l = l[(r + 1)..];
        var b = l.LastIndexOf('\b'); if (b >= 0) l = l[(b + 1)..];
        return l.TrimEnd();
    }

    /// <summary>T-0755: bekannte Rückgabewerte von winget in Alltagssprache (sonst nur die Zahl).</summary>
    public static string ExitText(int code) => code switch
    {
        0 => "0 – fertig ohne Fehler",
        unchecked((int)0x8A150014) => "0x8A150014 – kein Paket mit dieser Kennung gefunden",
        unchecked((int)0x8A15002B) => "0x8A15002B – keine passende Aktualisierung gefunden",
        unchecked((int)0x8A150011) => "0x8A150011 – die Prüfsumme des Installationsprogramms stimmt nicht",
        _ => $"{code} (0x{code:X8})",
    };

    /// <summary>Nur sichere Paket-IDs (keine Leerzeichen/Shell-Zeichen).</summary>
    /// <remarks>T-0755: Das erste Zeichen muss Buchstabe oder Ziffer sein – sonst würde „--override“ als Schalter gelesen.</remarks>
    public static bool SafeId(string id) => id.Length is > 1 and < 200 && char.IsLetterOrDigit(id[0]) && id.All(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_' or '+');

    /// <summary>T-0755: Versionen gleich (ohne Leerzeichen, „v“ vorne, nachgestellte „.0“)?</summary>
    public static bool SameVersion(string? a, string? b)
    {
        static string N(string v) { v = v.Trim().TrimStart('v', 'V'); while (v.EndsWith(".0") && v.Count(c => c == '.') > 0) v = v[..^2]; return v; }
        static bool Vague(string v) => v.Contains('<') || v.Contains('>') || v.Contains("unknown", StringComparison.OrdinalIgnoreCase); // T-0755 Fix2: „< 2.0“, „Unknown“ = nicht bestätigt
        return a is { Length: > 0 } && b is { Length: > 0 } && !Vague(a) && !Vague(b) && string.Equals(N(a), N(b), StringComparison.OrdinalIgnoreCase);
    }
}

/// <param name="exe">T-0755: Programmdatei, Standard „winget“. Tests setzen hier ein eigenes Testprogramm ein, das winget-Ausgaben nachahmt (ersetzt, kein echter Lauf).</param>
public sealed class WingetBackend(IProcessRunner run, string exe = "winget") : IPackageBackend, IPackageExitCode
{
    public string Name => "Windows-Paketverwaltung (winget)";
    public string Exe => exe;
    public int? LastExitCode { get; private set; }
    const string Agree = "--accept-source-agreements --disable-interactivity";
    public async Task<IReadOnlyList<PackageInfo>> SearchAsync(string q, CancellationToken ct = default) => PackageParsers.ParseWinget((await run.RunAsync(Exe, $"search \"{q.Replace("\"", "")}\" {Agree}", null, ct)).Output);
    public async Task<IReadOnlyList<PackageInfo>> InstalledAsync(CancellationToken ct = default) => PackageParsers.ParseWinget((await run.RunAsync(Exe, $"list {Agree}", null, ct)).Output);
    public async Task<IReadOnlyList<PackageInfo>> UpgradesAsync(CancellationToken ct = default) => PackageParsers.ParseWinget((await run.RunAsync(Exe, $"upgrade {Agree}", null, ct)).Output).Where(p => p.Available is not null).ToList();
    public Task<(bool, string)> InstallAsync(string id, Action<string>? l = null, CancellationToken ct = default) => Do("install", id, l, ct);
    public Task<(bool, string)> UpgradeAsync(string id, Action<string>? l = null, CancellationToken ct = default) => Do("upgrade", id, l, ct);
    public Task<(bool, string)> UninstallAsync(string id, Action<string>? l = null, CancellationToken ct = default) => Do("uninstall", id, l, ct);

    /// <summary>T-0755: Argumente je Vorgang – dieselben für Anzeige und Lauf.</summary>
    public static string Args(string action, string id) => action switch
    {
        "install" => $"install --id {id} -e --scope user --accept-package-agreements {Agree}",
        "upgrade" => $"upgrade --id {id} -e --accept-package-agreements {Agree}",
        "uninstall" => $"uninstall --id {id} -e {Agree}",
        _ => throw new ArgumentException(action),
    };
    /// <summary>Genau das, was gestartet wird: Programmdatei (bei einem Ersatz-Testprogramm dessen Pfad) und Argumente.</summary>
    public string CommandLine(string action, string id) => $"{(Exe.Contains(' ') ? $"\"{Exe}\"" : Exe)} {Args(action, id)}";

    /// <summary>T-0755: nur lesend „winget list --id … -e“ – steht das Paket danach in der Liste?</summary>
    public async Task<bool?> IsInstalledAsync(string id, CancellationToken ct = default)
    {
        if (!PackageParsers.SafeId(id)) return null;
        var (e, o) = await run.RunAsync(Exe, $"list --id {id} -e {Agree}", null, ct);
        // T-0755 (Gegenprüfung): nur Exit 0 (Liste) oder „nichts gefunden“ (0x8A150014) sind eine Antwort – jeder andere Fehler (z. B. Quelle kaputt) = nicht prüfbar
        return e == 0 ? PackageParsers.ListedIn(o, id) : e == NotFound ? false : null;
    }

    public const int NotFound = unchecked((int)0x8A150014);

    /// <summary>T-0755: installierte Version laut „winget list --id … -e“; null, wenn nicht gelistet oder nicht prüfbar.</summary>
    public async Task<string?> InstalledVersionAsync(string id, CancellationToken ct = default)
    {
        if (!PackageParsers.SafeId(id)) return null;
        var (e, o) = await run.RunAsync(Exe, $"list --id {id} -e {Agree}", null, ct);
        return e == 0 ? PackageParsers.ParseWinget(o).FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase))?.Version : null;
    }

    private async Task<(bool, string)> Do(string action, string id, Action<string>? l, CancellationToken ct)
    {
        LastExitCode = null;
        if (!PackageParsers.SafeId(id)) return (false, "Ungültige Paket-ID");
        var (e, o) = await run.RunAsync(Exe, Args(action, id), l, ct); LastExitCode = e; return (e == 0, o);
    }
}

public sealed class FlatpakBackend(IProcessRunner run) : IPackageBackend, IPackageExitCode
{
    public string Name => "Flatpak (Flathub)";
    public string Exe => "flatpak";
    public int? LastExitCode { get; private set; }
    public async Task<IReadOnlyList<PackageInfo>> SearchAsync(string q, CancellationToken ct = default) => PackageParsers.ParseFlatpak((await run.RunAsync(Exe, $"search --columns=name,application,version \"{q.Replace("\"", "")}\"", null, ct)).Output);
    public async Task<IReadOnlyList<PackageInfo>> InstalledAsync(CancellationToken ct = default) => PackageParsers.ParseFlatpak((await run.RunAsync(Exe, "list --app --columns=name,application,version", null, ct)).Output);
    public async Task<IReadOnlyList<PackageInfo>> UpgradesAsync(CancellationToken ct = default) => PackageParsers.ParseFlatpak((await run.RunAsync(Exe, "remote-ls --updates --app --columns=name,application,version", null, ct)).Output);
    public Task<(bool, string)> InstallAsync(string id, Action<string>? l = null, CancellationToken ct = default) => Do(Args("install", id), id, l, ct);
    public Task<(bool, string)> UpgradeAsync(string id, Action<string>? l = null, CancellationToken ct = default) => Do(Args("upgrade", id), id, l, ct);
    public Task<(bool, string)> UninstallAsync(string id, Action<string>? l = null, CancellationToken ct = default) => Do(Args("uninstall", id), id, l, ct);
    static string Args(string action, string id) => action switch
    {
        "install" => $"install --user -y --noninteractive flathub {id}",
        "upgrade" => $"update --user -y --noninteractive {id}",
        _ => $"uninstall --user -y --noninteractive {id}",
    };
    public string CommandLine(string action, string id) => $"flatpak {Args(action, id)}";
    private async Task<(bool, string)> Do(string args, string id, Action<string>? l, CancellationToken ct)
    {
        LastExitCode = null;
        if (!PackageParsers.SafeId(id)) return (false, "Ungültige Paket-ID");
        var (e, o) = await run.RunAsync(Exe, args, l, ct); LastExitCode = e; return (e == 0, o);
    }
}

/// <summary>SYS-04 App Installation & System Control Center: suchen, installieren, aktualisieren, entfernen – jede Änderung nur nach Freigabe, alles protokolliert.</summary>
public sealed class AppCenter(IPackageBackend pm, ApprovalGate gate, AuditLog audit)
{
    public IPackageBackend Backend => pm;
    public static IPackageBackend Default(IProcessRunner r) => OperatingSystem.IsWindows() ? new WingetBackend(r) : new FlatpakBackend(r);

    /// <param name="by">T-0713: angemeldete Person (Rolle aus dem Profil); ohne Angabe der lokale Besitzer (Kommandozeile, Tests).</param>
    public ApprovalRequest Plan(string action, PackageInfo p, Principal? by = null) =>
        gate.Request(by ?? Principal.LocalOwner, "app." + action, $"{p.Name} {(action == "install" ? "installieren" : action == "upgrade" ? "aktualisieren" : "entfernen")}", p.Id);

    public async Task<(bool Ok, string Log)> ExecuteAsync(ApprovalRequest r, Action<string>? line = null, CancellationToken ct = default)
    {
        var action = r.Action["app.".Length..];
        Func<Task<(bool, string)>> job = action switch
        {
            "install" => () => pm.InstallAsync(r.Target, line, ct), "upgrade" => () => pm.UpgradeAsync(r.Target, line, ct), "uninstall" => () => pm.UninstallAsync(r.Target, line, ct),
            _ => throw new ArgumentException(action),
        };
        var res = await gate.Execute(r.Id, r.Action, r.Target, job);
        audit.Append(Principal.LocalOwner.Name, res.Item1 ? "app.done" : "app.failed", $"{action} {r.Target}");
        return res;
    }

    /// <summary>T-0755: Ergebnis eines Laufs mit Nachprüfung. <see cref="Installed"/>: danach in der Liste (null = nicht prüfbar).</summary>
    public sealed record RunResult(string Action, string Id, string Command, bool ExitOk, int? ExitCode, bool Cancelled, bool? Installed, string LogFile, string Output,
        string? ExpectedVersion = null, string? VersionAfter = null)
    {
        /// <summary>Der gewünschte Zustand ist nachgeprüft erreicht: installiert, entfernt – beim Aktualisieren steht danach die erwartete Version da.
        /// Ohne bekannte Zielversion gilt eine Aktualisierung als „nicht bestätigt“ (T-0755, Gegenprüfung).</summary>
        public bool Verified => Action switch
        {
            "uninstall" => Installed == false,
            "upgrade" => Installed == true && PackageParsers.SameVersion(VersionAfter, ExpectedVersion),
            _ => Installed == true,
        };
    }

    /// <summary>T-0755: ein freigegebener Lauf mit Protokolldatei – Befehl, jede Ausgabezeile, Rückgabewert, Abbruch und die Nachprüfung,
    /// ob das Programm danach installiert ist. Das Protokoll wird Zeile für Zeile geschrieben und bleibt auch nach einem Absturz lesbar.</summary>
    /// <param name="expectVersion">Beim Aktualisieren: die angekündigte neue Version (winget „Available“).</param>
    public async Task<RunResult> RunAsync(ApprovalRequest r, string logDir, Action<string>? line = null, CancellationToken ct = default, string? expectVersion = null)
    {
        var action = r.Action["app.".Length..]; var id = r.Target;
        var cmd = pm.CommandLine(action, id);
        Directory.CreateDirectory(logDir);
        var log = Path.Combine(logDir, $"{DateTime.Now:yyyyMMdd-HHmmss-fff}-{action}-{(PackageParsers.SafeId(id) ? id : "ungueltig")}.log");
        var sb = new System.Text.StringBuilder(); var lk = new object();
        await using var w = new StreamWriter(log, append: false, new System.Text.UTF8Encoding(false)) { AutoFlush = true };
        void W(string s) { lock (lk) w.WriteLine(s); }
        W($"Trendo – Programme verwalten · {DateTime.Now:dd.MM.yyyy HH:mm:ss}");
        W($"Vorgang: {action} · Paket: {id} · Paketverwaltung: {pm.Name}");
        W($"Befehl: {cmd}");
        W("--- Ausgabe ---");
        bool ok = false, cancelled = false; var output = "";
        try
        {
            (ok, output) = await ExecuteAsync(r, s => { var c = PackageParsers.CleanLine(s); lock (lk) { sb.AppendLine(c); w.WriteLine(c); } line?.Invoke(s); }, ct);
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
            audit.Append(Principal.LocalOwner.Name, "app.abgebrochen", $"{action} {id}");
        }
        var exit = cancelled ? null : (pm as IPackageExitCode)?.LastExitCode;
        W("--- Ende der Ausgabe ---");
        W(cancelled ? "Ergebnis: abgebrochen – Trendo hat den Lauf beendet." : $"Rückgabewert: {(exit is { } e ? PackageParsers.ExitText(e) : ok ? "erfolgreich" : "Fehler")}");
        bool? installed; string? after = null;
        try
        {
            installed = await pm.IsInstalledAsync(id, CancellationToken.None);
            if (action == "upgrade" && installed == true) after = await pm.InstalledVersionAsync(id, CancellationToken.None);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or System.ComponentModel.Win32Exception) { installed = null; W("Nachprüfung nicht möglich: " + ex.Message); }
        W($"Nachprüfung: {(installed is null ? "nicht prüfbar" : installed.Value ? "steht in der Liste der installierten Programme" : "steht nicht in der Liste der installierten Programme")}");
        if (action == "upgrade") W($"Version danach: {after ?? "unbekannt"} · erwartet: {expectVersion ?? "unbekannt"}");
        string text; lock (lk) text = sb.ToString();
        var res = new RunResult(action, id, cmd, ok && !cancelled, exit, cancelled, installed, log, text.Length > 0 ? text : output, expectVersion, after);
        W($"Ziel erreicht: {(res.Verified ? "ja" : "nein")}");
        audit.Append(Principal.LocalOwner.Name, "app.pruefung", $"{action} {id} {(res.Verified ? "bestätigt" : "nicht bestätigt")}");
        return res;
    }
}
