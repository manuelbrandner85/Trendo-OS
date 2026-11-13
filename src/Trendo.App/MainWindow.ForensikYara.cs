using Avalonia.Controls;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Sys;

namespace Trendo.App;

/// <summary>Alle Studios, Runde 8 – SPEC-12: Werkzeug-Karte „Auf Schadsoftware-Merkmale prüfen“ mit yara-x (BSD-3) und den ReversingLabs-Regeln (MIT).
/// Nur lesen: Befunde zeigen, nichts löschen oder verschieben; Quarantäne bleibt eine bewusste Entscheidung unter „Sicher prüfen“.</summary>
public partial class MainWindow
{
    /// <summary>Für Tests: Werkzeugordner und Lauf nachbilden.</summary>
    public static string? YaraDirOverride { get; set; }
    public static Func<string, IReadOnlyList<string>, int, Cli.Result>? YaraRunOverride { get; set; }
    string YaraDir => YaraDirOverride ?? Path.Combine(_deps.ToolsRoot, "yara-x");
    bool YaraReady => YaraDirOverride is not null || YaraScan.Ready(YaraDir);
    string _yaraMsg = ""; public string YaraMessage => _yaraMsg;
    public List<YaraScan.Finding>? YaraLast { get; private set; }

    /// <summary>Datei oder Ordner prüfen (öffentlich für Tests).</summary>
    public async Task ForensicsYara(string target)
    {
        target = target.Trim().Trim('"');
        _yaraMsg = "Wird geprüft …"; Show("forensik");
        var dir = YaraDir; var run = YaraRunOverride;
        var (f, msg) = await Task.Run(() => YaraScan.Scan(dir, target, run: run));
        YaraLast = f; _yaraMsg = (f is { Count: > 0 } ? "" : f is null ? "• " : "") + msg;
        _core.Audit.Append(Principal.LocalOwner.Name, "security.yara", f is null ? "fehler" : $"{f.Count} Treffer");
        Show("forensik");
    }

    async Task ForensicsYaraSetup()
    {
        if (_opt.Isolated) { _yaraMsg = "Testmodus: nichts installiert."; Show("forensik"); return; }
        _yaraMsg = "yara-x und die Regeln werden aus den offiziellen Quellen geladen (feste Stände, mit Prüfsumme) …"; Show("forensik");
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        var (exe, m1) = await YaraScan.FetchAsync(http.GetStreamAsync, YaraScan.ZipUrl, YaraScan.ZipSha256);
        var (rules, m2) = exe is null ? (null, m1) : await YaraScan.FetchAsync(http.GetStreamAsync, YaraScan.RulesUrl, YaraScan.RulesSha256);
        var ok = exe is not null && rules is not null && YaraScan.ExtractExe(exe, YaraDir) && YaraScan.ExtractRules(new MemoryStream(rules), YaraDir) > 0;
        _core.Audit.Append(Principal.LocalOwner.Name, "deps.download", $"yara-x {YaraScan.Version} + ReversingLabs-Regeln {YaraScan.RulesCommit[..8]}" + (ok ? "" : " (fehlgeschlagen)"));
        _yaraMsg = ok ? "Regel-Prüfung ist eingerichtet (310 Regeln)." : "Einrichten ging nicht: " + (exe is null ? m1 : m2);
        Show("forensik");
    }

    Control ForensicsYaraCard()
    {
        var target = new TextBox { Name = "for-yara-target", PlaceholderText = "Datei oder Ordner, z. B. Downloads", Width = 320, Text = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) is { Length: > 0 } h ? Path.Combine(h, "Downloads") : "" };
        Control action = YaraReady
            ? new WrapPanel { Children = { MSp(target), MSp(Act("for-yara-run", "Prüfen", () => _ = ForensicsYara(target.Text ?? ""), true)) } }
            : Act("for-yara-setup", "Regel-Prüfung einrichten (yara-x, kostenlos)", () => _ = ForensicsYaraSetup());
        var msg = new SelectableTextBlock { Name = "for-yara-msg", Text = _yaraMsg, TextWrapping = TextWrapping.Wrap, Foreground = _yaraMsg.StartsWith('⚠') ? Brush.Parse("#F97316") : Text };
        return ToolCard("for-card-yara", Mdi.Magnify, "#FCA5A5", "#B91C1C", "Auf Schadsoftware-Merkmale prüfen",
            "Prüft Dateien mit 310 geprüften Regeln (ReversingLabs) auf bekannte Erpressersoftware, Trojaner und Hackerwerkzeuge. Nur lesen – nichts wird gelöscht oder verschoben.", action, msg);
    }
}
