using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Sys;

namespace Trendo.App;

/// <summary>Alle Studios, Runde 8 – CORE-11: Karte „Verschlüsselte Sicherung mit Versionen (restic)“ in „Daten schützen“.
/// Passwort nur im Arbeitsspeicher dieses Fensters – nie gespeichert; Wiederherstellen nur in einen neuen Ordner.</summary>
public partial class MainWindow
{
    /// <summary>Für Tests: Werkzeugordner und Lauf nachbilden.</summary>
    public static string? ResticDirOverride { get; set; }
    public static Func<string, IReadOnlyList<string>, IDictionary<string, string>, Cli.Result>? ResticRunOverride { get; set; }
    string ResticDir => ResticDirOverride ?? Path.Combine(_deps.ToolsRoot, "restic");
    bool ResticReady => ResticDirOverride is not null || ResticBackup.Ready(ResticDir);
    string _resticMsg = "", _resticRepo = "", _resticSources = ""; public string ResticMessage => _resticMsg;
    public List<ResticBackup.Snapshot>? ResticSnapshots { get; private set; }

    /// <summary>Sichern (öffentlich für Tests). Quellen mit „;“ getrennt.</summary>
    public async Task SchutzResticBackup(string sources, string repo, string password)
    {
        _resticSources = sources; _resticRepo = repo.Trim().Trim('"');
        var paths = sources.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(p => p.Trim('"')).ToList();
        _resticMsg = "Wird verschlüsselt gesichert …"; Show("schutz");
        var dir = ResticDir; var run = ResticRunOverride; var r = _resticRepo;
        var (s, msg) = await Task.Run(() => ResticBackup.Backup(dir, r, password, paths, run));
        _resticMsg = (s is null ? "• " : "") + msg + (s is not null ? " Passwort gut aufbewahren – ohne Passwort lässt sich die Sicherung nicht öffnen." : "");
        _core.Audit.Append(Principal.LocalOwner.Name, "backup.restic", s is null ? "fehler" : $"Stand {s.SnapshotId[..Math.Min(8, s.SnapshotId.Length)]}");
        if (s is not null) ResticSnapshots = null;
        Show("schutz");
    }

    /// <summary>Stände anzeigen (öffentlich für Tests).</summary>
    public async Task SchutzResticList(string repo, string password)
    {
        _resticRepo = repo.Trim().Trim('"'); var dir = ResticDir; var run = ResticRunOverride; var r = _resticRepo;
        var (l, msg) = await Task.Run(() => ResticBackup.Snapshots(dir, r, password, run));
        ResticSnapshots = l; _resticMsg = (l is null ? "• " : "") + msg; Show("schutz");
    }

    /// <summary>Stand in einen neuen Ordner „Wiederhergestellt/&lt;Datum&gt;“ zurückholen (öffentlich für Tests). Rückgabe: Zielordner oder null.</summary>
    public async Task<string?> SchutzResticRestore(ResticBackup.Snapshot snap, string password)
    {
        var target = Path.Combine(WorkFolder("Wiederhergestellt"), $"{snap.Time.LocalDateTime:yyyy-MM-dd HHmm} ({snap.ShortId})");
        var dir = ResticDir; var run = ResticRunOverride; var repo = _resticRepo;
        var (ok, msg) = await Task.Run(() => ResticBackup.Restore(dir, repo, password, snap.Id, target, run, snap.Paths));
        _resticMsg = (ok ? "• " : "") + msg; _core.Audit.Append(Principal.LocalOwner.Name, "backup.restic.restore", ok ? snap.ShortId : "fehler");
        Show("schutz"); return ok ? target : null;
    }

    async Task SchutzResticSetup()
    {
        if (_opt.Isolated) { _resticMsg = "Testmodus: nichts installiert."; Show("schutz"); return; }
        _resticMsg = "restic wird von der offiziellen Seite geladen (feste Fassung, mit Prüfsumme) …"; Show("schutz");
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        var (zip, m) = await YaraScan.FetchAsync(http.GetStreamAsync, ResticBackup.ZipUrl, ResticBackup.ZipSha256);
        var ok = zip is not null && ResticBackup.ExtractExe(zip, ResticDir);
        _core.Audit.Append(Principal.LocalOwner.Name, "deps.download", "restic " + ResticBackup.Version + (ok ? "" : " (fehlgeschlagen)"));
        _resticMsg = ok ? "restic ist eingerichtet." : "Einrichten ging nicht: " + m;
        Show("schutz");
    }

    Control SchutzResticCard()
    {
        var sp = new StackPanel { Name = "sec-restic", Spacing = 6 };
        sp.Children.Add(H("Verschlüsselte Sicherung mit Versionen (restic)", 14));
        sp.Children.Add(P("Sichert Ordner verschlüsselt auf eine USB-Platte oder einen anderen Ort – jedes Mal nur, was sich geändert hat, und mit allen früheren Ständen. Kostenlos, BSD-2.", null, 12));
        if (!ResticReady) { sp.Children.Add(Act("sec-restic-setup", "restic einrichten (ca. 11 MB)", () => _ = SchutzResticSetup())); }
        else
        {
            var src = new TextBox { Name = "sec-restic-src", Text = _resticSources.Length > 0 ? _resticSources : (string.IsNullOrEmpty(_settings.ProjectsFolder) ? UserSettings.DefaultProjectsFolder() : _settings.ProjectsFolder), Width = 420, PlaceholderText = "Ordner (mehrere mit ;)" };
            var repo = new TextBox { Name = "sec-restic-repo", Text = _resticRepo, Width = 260, PlaceholderText = "Sicherungsort, z. B. E:\\Trendo-Sicherung" };
            var pw = new TextBox { Name = "sec-restic-pw", PasswordChar = '•', Width = 200, PlaceholderText = "Passwort (mind. 8 Zeichen)" };
            sp.Children.Add(new WrapPanel { Children = { MSp(src) } });
            sp.Children.Add(new WrapPanel { Children = { MSp(repo), MSp(pw),
                MSp(Act("sec-restic-run", "Jetzt sichern", () => _ = SchutzResticBackup(src.Text ?? "", repo.Text ?? "", pw.Text ?? ""), true)),
                MSp(Act("sec-restic-list", "Stände anzeigen", () => _ = SchutzResticList(repo.Text ?? "", pw.Text ?? ""))) } });
            var i = 0;
            foreach (var s in ResticSnapshots ?? [])
            {
                var snap = s;
                sp.Children.Add(Row(P($"{s.Time.LocalDateTime:dd.MM.yyyy HH:mm} · {s.ShortId} · {string.Join(", ", s.Paths.Select(Path.GetFileName))}", Text, 12),
                    Act($"sec-restic-restore-{i++}", "In neuen Ordner zurückholen", () => _ = SchutzResticRestore(snap, pw.Text ?? ""))));
            }
        }
        if (_resticMsg.Length > 0) sp.Children.Add(new TextBlock { Name = "sec-restic-msg", Text = _resticMsg, TextWrapping = TextWrapping.Wrap, Foreground = _resticMsg.StartsWith('⚠') ? Brush.Parse("#F97316") : Text, FontSize = 12 });
        return WCard(sp);
    }
}
