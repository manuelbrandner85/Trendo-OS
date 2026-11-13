using Avalonia.Controls;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Alle Studios, Runde 7 – CORE-07: „Barrierefreiheit genau prüfen (axe-core)“ in der Qualitätsprüfung von „Webseite bauen“.
/// axe-core (MPL-2.0, unverändert) + jsdom laufen als getrennter Node-Prozess; Seiten-Skripte laufen nicht, nichts geht ins Netz.</summary>
public partial class MainWindow
{
    /// <summary>Für Tests: Werkzeugordner und Lauf nachbilden.</summary>
    public static string? AxeDirOverride { get; set; }
    public static Func<string, IReadOnlyList<string>, int, Cli.Result>? AxeRunOverride { get; set; }
    string AxeDir => AxeDirOverride ?? Path.Combine(_deps.ToolsRoot, "axe");
    bool AxeReady => AxeDirOverride is not null || AxeCheck.Ready(AxeDir);
    string _axeMsg = ""; public string AxeMessage => _axeMsg;
    public AxeCheck.Result? AxeLast { get; private set; }

    /// <summary>Seite mit axe-core prüfen (öffentlich für Tests). Befunde erscheinen unter der Note der eigenen Prüfung.</summary>
    public void WebAxeCheck()
    {
        if (_webDir is null) { _axeMsg = "Erst eine Seite anlegen oder öffnen."; Show("dev"); return; }
        var node = AxeRunOverride is not null ? "node" : Cli.Find("node");
        if (node is null) { _axeMsg = "Node.js fehlt – zuerst unter Programme „Node.js“ einrichten."; Show("dev"); return; }
        var (r, msg) = AxeCheck.Run(node, AxeDir, _webDir, run: AxeRunOverride);
        AxeLast = r;
        _axeMsg = r is null ? "" + msg
            : msg + (r.Findings.Count == 0 ? " Keine Probleme gefunden (Farbkontrast prüft die eigene Prüfung)." : "\n" + string.Join("\n", r.Findings.Take(12).Select(f => (f.Level == "Fehler" ? "" : "• ") + f.Text)) + (r.Findings.Count > 12 ? $"\n… und {r.Findings.Count - 12} weitere." : ""));
        _core.Audit.Append(Principal.LocalOwner.Name, "web.axe", r is null ? "fehler" : $"{r.Findings.Count} Befunde");
        Show("dev");
    }

    async Task WebAxeSetup()
    {
        var npm = Cli.Find("npm") ?? Cli.Find("npm.cmd");
        if (_opt.Isolated) _axeMsg = "Testmodus: nichts installiert.";
        else if (npm is null) _axeMsg = "Node.js mit npm fehlt – zuerst unter Programme „Node.js“ einrichten.";
        else
        {
            _axeMsg = "axe-core und jsdom werden aus der offiziellen npm-Quelle geladen (feste Fassungen, ohne Installationsskripte) …"; Show("dev");
            Directory.CreateDirectory(AxeDir);
            var r = await Task.Run(() => Cli.Run(npm, AxeCheck.InstallArgs(AxeDir), AxeDir, 600_000));
            var ok = r.Ok && AxeCheck.Ready(AxeDir);
            _core.Audit.Append(Principal.LocalOwner.Name, "deps.npm", $"axe-core@{AxeCheck.AxeVersion} jsdom@{AxeCheck.JsdomVersion}" + (ok ? "" : " (fehlgeschlagen)"));
            _axeMsg = ok ? "axe-core ist eingerichtet." : "Einrichten ging nicht: " + (r.Err.Length > 0 ? r.Err : r.Out).Trim().Split('\n')[0];
        }
        Show("dev");
    }

    Control WebAxePanel()
    {
        var sp = new StackPanel { Name = "web-axe", Spacing = 4 };
        sp.Children.Add(AxeReady
            ? Act("web-axe-run", "Barrierefreiheit genau prüfen (axe-core)", WebAxeCheck)
            : Act("web-axe-setup", "Genaue Barrierefreiheitsprüfung einrichten (axe-core, kostenlos)", () => _ = WebAxeSetup()));
        if (_axeMsg.Length > 0) sp.Children.Add(new TextBlock { Name = "web-axe-msg", Text = _axeMsg, FontSize = 11.5, Foreground = Muted, TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        return sp;
    }
}
