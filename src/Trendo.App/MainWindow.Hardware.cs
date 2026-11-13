using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Compat;

namespace Trendo.App;

/// <summary>Commercial Desktop Edition (Auftrag vom 13.11.2026): Hardware-Kompatibilität, Leistungsprofil und Eignung je Studio.
/// Nur lesend, nichts verlässt den PC. Die Internetprüfung läuft erst auf Knopfdruck. Vorschläge werden nur nach Zustimmung angewendet.</summary>
public partial class MainWindow
{
    private HardwareReport? _hw;
    private bool? _internet;
    private bool _hwBusy;
    private string _hwMsg = "";
    public HardwareReport? LastHardware => _hw;
    public Task? HardwareScanTask { get; private set; }

    private void InitHardwareCompat()
    {
        _showMap["hardware"] = HardwareView; PageModules["hardware"] = "SYS-13";
        ExtraPages.Add(("Hardware & Leistung", "hardware", ["hardware", "systemanforderungen", "anforderungen", "leistung", "grafikkarte", "arbeitsspeicher", "prozessor", "kompatibel", "profil", "langsam"]));
    }

    private void StartHardwareScan()
    {
        if (_hwBusy) return;
        _hwBusy = true;
        var root = _core.DataRoot;
        var b = Screens?.Primary?.Bounds;
        HardwareScanTask = Task.Run(() =>
        {
            HardwareReport? r = null;
            try { r = HardwareScan.Read(root, b?.Width ?? 0, b?.Height ?? 0); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException) { }
            _ui.Post(() => { _hw = r; _hwBusy = false; if (CurrentView is "hardware" or "lizenz") Show(CurrentView); });
        });
    }

    /// <summary>Erreichbarkeit des Update-Servers (GitHub), nur auf Knopfdruck. Es werden keine Gerätedaten gesendet. Liegt in der App, weil der Core netzfrei bleibt (QA-019).</summary>
    private static async Task<bool> InternetReachableAsync()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
            using var req = new HttpRequestMessage(HttpMethod.Head, "https://github.com/");
            using var res = await http.SendAsync(req);
            return (int)res.StatusCode < 500;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException) { return false; }
    }

    private bool HasProgram(string id) { try { return _deps.IsInstalled(DependencyCatalog.Get(id)); } catch (Exception e) when (e is InvalidOperationException or KeyNotFoundException or ArgumentException) { return false; } }
    private string ProgramName(string id) { try { return DependencyCatalog.Get(id).Name; } catch (Exception e) when (e is InvalidOperationException or KeyNotFoundException or ArgumentException) { return id; } }

    private List<StudioVerdict> StudioVerdicts(PerfProfile p) =>
        StudioCatalog.ById.Keys.Select(id => StudioNeeds.Verdict(id, p, HasProgram, ProgramName)).ToList();

    private static (string Icon, string Color) FitLook(StudioFit f) => f switch
    {
        StudioFit.Laeuft => ("✓", "#4ADE80"), StudioFit.Eingeschraenkt => ("•", "#FACC15"),
        StudioFit.BrauchtProgramm => ("+", "#93C5FD"), _ => ("!", "#F87171"),
    };

    private Control HardwareView()
    {
        var sp = new StackPanel { Spacing = 8 };
        if (_hw is null)
        {
            StartHardwareScan();
            sp.Children.Add(new TextBlock { Name = "hw-profile", Text = "Dein PC wird geprüft …", Foreground = Text, FontSize = 18 });
            return SysPage("Hardware & Leistung", sp);
        }
        var p = PerformanceProfiles.Classify(_hw);
        sp.Children.Add(WCard(new StackPanel { Spacing = 4, Children = {
            new TextBlock { Name = "hw-profile", Text = $"Leistungsprofil: {PerformanceProfiles.Label(p)}", Foreground = Text, FontSize = 20, FontWeight = FontWeight.Bold },
            P(p == PerfProfile.Unter ? "Dieser PC liegt unter dem Basis-Profil. Trendo startet, viele Studios werden aber langsam sein." : PerformanceProfiles.Spec(p).ForWhat + ".", Text, 13),
            P("Die Profilgrenzen sind vorläufig und noch nicht auf vielen PCs gemessen. Geprüft wird nur auf diesem PC, nichts wird gesendet.", null, 11) } }));

        var checks = new StackPanel { Name = "hw-checks", Spacing = 2 };
        foreach (var c in CompatibilityCheck.Items(_hw, HasProgram, _internet))
        {
            var (dot, col) = c.State switch { CompatState.Ok => ("✓", "#4ADE80"), CompatState.Warn => ("•", "#FACC15"), CompatState.Fail => ("!", "#F87171"), _ => ("?", "#9DB0CF") };
            checks.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("24,190,*"), Children = {
                new TextBlock { Text = dot, Foreground = Brush.Parse(col), FontWeight = FontWeight.Bold },
                Pos(new TextBlock { Text = c.Title, Foreground = Text, FontSize = 12 }, 1),
                Pos(new TextBlock { Text = $"{c.Value} – {c.Hint}", Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap }, 2) } });
        }
        var net = new Button { Name = "hw-internet", Content = "Internet prüfen", Margin = new Thickness(0, 6, 0, 0) };
        net.Click += async (_, _) => { net.IsEnabled = false; _internet = await InternetReachableAsync(); Show("hardware"); };
        var again = new Button { Name = "hw-rescan", Content = "Erneut prüfen", Margin = new Thickness(6, 6, 0, 0) };
        again.Click += (_, _) => { _hw = null; Show("hardware"); };
        sp.Children.Add(WCard(new StackPanel { Children = { new TextBlock { Text = "Systemcheck", Foreground = Text, FontSize = 15, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 0, 0, 4) }, checks,
            new StackPanel { Orientation = Orientation.Horizontal, Children = { net, again } } } }));

        var adv = new StackPanel { Name = "hw-advice", Spacing = 6 };
        adv.Children.Add(new TextBlock { Text = "Vorschläge für diesen PC", Foreground = Text, FontSize = 15, FontWeight = FontWeight.SemiBold });
        var advice = PerformanceAdvisor.Suggest(_hw, p);
        if (advice.Count == 0) adv.Children.Add(P("Keine Anpassung nötig.", Text, 12));
        foreach (var a in advice)
        {
            var row = new StackPanel { Spacing = 2, Children = { new TextBlock { Text = a.Title, Foreground = Text, FontSize = 13 }, P(a.Why, null, 12) } };
            if (a.Action == AdviceAction.SmallKiModel && _deps.HasModel(DependencyCatalog.LocalModel) && KiModelName != DependencyCatalog.LocalModel)
            {
                var b = new Button { Name = "hw-apply-" + a.Id, Content = "Übernehmen", HorizontalAlignment = HorizontalAlignment.Left };
                b.Click += (_, _) => { KiModel.Select(_core.DataRoot, DependencyCatalog.LocalModel, _core.Audit); _kiConnected = false; _hwMsg = "Kleines KI-Modell eingestellt. Das große bleibt installiert und ist jederzeit wieder wählbar."; Show("hardware"); };
                row.Children.Add(b);
            }
            else if (a.Action != AdviceAction.SmallKiModel)
                row.Children.Add(P("Hinweis – Trendo stellt das noch nicht automatisch um.", null, 11));
            adv.Children.Add(row);
        }
        if (_hwMsg.Length > 0) adv.Children.Add(new TextBlock { Name = "hw-msg", Text = _hwMsg, Foreground = Accent, FontSize = 12 });
        sp.Children.Add(WCard(adv));
        sp.Children.Add(WCard(StudioFitList(p, "hw-studios")));
        return SysPage("Hardware & Leistung", sp);
    }

    private StackPanel StudioFitList(PerfProfile p, string name)
    {
        var list = new StackPanel { Name = name, Spacing = 2 };
        var v = StudioVerdicts(p);
        list.Children.Add(new TextBlock { Text = "Studios auf diesem PC", Foreground = Text, FontSize = 15, FontWeight = FontWeight.SemiBold });
        list.Children.Add(P($"{v.Count(x => x.Fit == StudioFit.Laeuft)} laufen · {v.Count(x => x.Fit == StudioFit.Eingeschraenkt)} eingeschränkt · {v.Count(x => x.Fit == StudioFit.BrauchtProgramm)} brauchen ein Zusatzprogramm · {v.Count(x => x.Fit == StudioFit.ZuSchwach)} zu schwach", Text, 12));
        foreach (var x in v.OrderByDescending(x => x.Fit))
        {
            var (icon, col) = FitLook(x.Fit);
            list.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("24,220,*"), Children = {
                new TextBlock { Text = icon, Foreground = Brush.Parse(col), FontWeight = FontWeight.Bold },
                Pos(new TextBlock { Text = StudioCatalog.ById[x.StudioId].Name, Foreground = Text, FontSize = 12 }, 1),
                Pos(new TextBlock { Text = x.Text, Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap }, 2) } });
        }
        return list;
    }
}
