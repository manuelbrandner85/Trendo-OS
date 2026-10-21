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
}

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

    /// <summary>Nur sichere Paket-IDs (keine Leerzeichen/Shell-Zeichen).</summary>
    public static bool SafeId(string id) => id.Length is > 1 and < 200 && id.All(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_' or '+');
}

public sealed class WingetBackend(IProcessRunner run) : IPackageBackend
{
    public string Name => "Windows-Paketverwaltung (winget)";
    public string Exe => "winget";
    const string Agree = "--accept-source-agreements --disable-interactivity";
    public async Task<IReadOnlyList<PackageInfo>> SearchAsync(string q, CancellationToken ct = default) => PackageParsers.ParseWinget((await run.RunAsync(Exe, $"search \"{q.Replace("\"", "")}\" {Agree}", null, ct)).Output);
    public async Task<IReadOnlyList<PackageInfo>> InstalledAsync(CancellationToken ct = default) => PackageParsers.ParseWinget((await run.RunAsync(Exe, $"list {Agree}", null, ct)).Output);
    public async Task<IReadOnlyList<PackageInfo>> UpgradesAsync(CancellationToken ct = default) => PackageParsers.ParseWinget((await run.RunAsync(Exe, $"upgrade {Agree}", null, ct)).Output).Where(p => p.Available is not null).ToList();
    public Task<(bool, string)> InstallAsync(string id, Action<string>? l = null, CancellationToken ct = default) => Do($"install --id {id} -e --scope user --accept-package-agreements {Agree}", id, l, ct);
    public Task<(bool, string)> UpgradeAsync(string id, Action<string>? l = null, CancellationToken ct = default) => Do($"upgrade --id {id} -e --accept-package-agreements {Agree}", id, l, ct);
    public Task<(bool, string)> UninstallAsync(string id, Action<string>? l = null, CancellationToken ct = default) => Do($"uninstall --id {id} -e {Agree}", id, l, ct);
    private async Task<(bool, string)> Do(string args, string id, Action<string>? l, CancellationToken ct)
    {
        if (!PackageParsers.SafeId(id)) return (false, "Ungültige Paket-ID");
        var (e, o) = await run.RunAsync(Exe, args, l, ct); return (e == 0, o);
    }
}

public sealed class FlatpakBackend(IProcessRunner run) : IPackageBackend
{
    public string Name => "Flatpak (Flathub)";
    public string Exe => "flatpak";
    public async Task<IReadOnlyList<PackageInfo>> SearchAsync(string q, CancellationToken ct = default) => PackageParsers.ParseFlatpak((await run.RunAsync(Exe, $"search --columns=name,application,version \"{q.Replace("\"", "")}\"", null, ct)).Output);
    public async Task<IReadOnlyList<PackageInfo>> InstalledAsync(CancellationToken ct = default) => PackageParsers.ParseFlatpak((await run.RunAsync(Exe, "list --app --columns=name,application,version", null, ct)).Output);
    public async Task<IReadOnlyList<PackageInfo>> UpgradesAsync(CancellationToken ct = default) => PackageParsers.ParseFlatpak((await run.RunAsync(Exe, "remote-ls --updates --app --columns=name,application,version", null, ct)).Output);
    public Task<(bool, string)> InstallAsync(string id, Action<string>? l = null, CancellationToken ct = default) => Do($"install --user -y --noninteractive flathub {id}", id, l, ct);
    public Task<(bool, string)> UpgradeAsync(string id, Action<string>? l = null, CancellationToken ct = default) => Do($"update --user -y --noninteractive {id}", id, l, ct);
    public Task<(bool, string)> UninstallAsync(string id, Action<string>? l = null, CancellationToken ct = default) => Do($"uninstall --user -y --noninteractive {id}", id, l, ct);
    private async Task<(bool, string)> Do(string args, string id, Action<string>? l, CancellationToken ct)
    {
        if (!PackageParsers.SafeId(id)) return (false, "Ungültige Paket-ID");
        var (e, o) = await run.RunAsync(Exe, args, l, ct); return (e == 0, o);
    }
}

/// <summary>SYS-04 App Installation & System Control Center: suchen, installieren, aktualisieren, entfernen – jede Änderung nur nach Freigabe, alles protokolliert.</summary>
public sealed class AppCenter(IPackageBackend pm, ApprovalGate gate, AuditLog audit)
{
    public IPackageBackend Backend => pm;
    public static IPackageBackend Default(IProcessRunner r) => OperatingSystem.IsWindows() ? new WingetBackend(r) : new FlatpakBackend(r);

    public ApprovalRequest Plan(string action, PackageInfo p) =>
        gate.Request(Principal.LocalOwner, "app." + action, $"{p.Name} {(action == "install" ? "installieren" : action == "upgrade" ? "aktualisieren" : "entfernen")}", p.Id);

    public async Task<(bool Ok, string Log)> ExecuteAsync(ApprovalRequest r, Action<string>? line = null)
    {
        var action = r.Action["app.".Length..];
        Func<Task<(bool, string)>> job = action switch
        {
            "install" => () => pm.InstallAsync(r.Target, line), "upgrade" => () => pm.UpgradeAsync(r.Target, line), "uninstall" => () => pm.UninstallAsync(r.Target, line),
            _ => throw new ArgumentException(action),
        };
        var res = await gate.Execute(r.Id, r.Action, r.Target, job);
        audit.Append(Principal.LocalOwner.Name, res.Item1 ? "app.done" : "app.failed", $"{action} {r.Target}");
        return res;
    }
}
