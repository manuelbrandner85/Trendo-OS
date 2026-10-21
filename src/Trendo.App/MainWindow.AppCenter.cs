using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>SYS-04: Programme suchen, installieren, aktualisieren, entfernen – über die Paketverwaltung des Systems, jede Änderung mit Freigabe.</summary>
public partial class MainWindow
{
    public IPackageBackend? AppBackendOverride { get; set; }
    private AppCenter? _appCenter;
    public AppCenter AppCenter => _appCenter ??= new AppCenter(AppBackendOverride ?? AppCenter.Default(new RealProcessRunner()), _core.Approvals, _core.Audit);
    private IReadOnlyList<PackageInfo> _appHits = [], _appInstalled = [], _appUpdates = [];
    private string _appQuery = "", _appMsg = ""; private bool _appBusy;

    private void InitSys04()
    {
        _showMap["appcenter"] = AppCenterView; SysTabs.Add(("appcenter", "Programme verwalten"));
        StudioPages["SYS-04"] = "appcenter";
        ExtraPages.Add(("Programme verwalten", "appcenter", ["installieren", "deinstallieren", "entfernen", "aktualisieren", "update", "winget", "flatpak"]));
    }

    public async Task AppActionAsync(string action, PackageInfo p)
    {
        _appBusy = true;
        try
        {
            var r = AppCenter.Plan(action, p);
            _core.Approvals.Approve(r.Id, Principal.LocalOwner);
            var (ok, _) = await AppCenter.ExecuteAsync(r);
            _appMsg = ok ? $"{p.Name}: {(action == "install" ? "installiert" : action == "upgrade" ? "aktualisiert" : "entfernt")}." : $"{p.Name}: hat nicht geklappt. Windows hat vielleicht nach Erlaubnis gefragt – versuch es noch einmal.";
            if (ok && action != "install") _appUpdates = _appUpdates.Where(x => x.Id != p.Id).ToList();
            if (ok && action == "uninstall") _appInstalled = _appInstalled.Where(x => x.Id != p.Id).ToList();
        }
        catch (Exception ex) { _appMsg = "Hat nicht geklappt: " + ex.Message; }
        _appBusy = false;
    }

    public async Task AppSearchAsync(string q) { _appQuery = q; _appHits = q.Trim().Length < 2 ? [] : await AppCenter.Backend.SearchAsync(q.Trim()); _appMsg = _appHits.Count == 0 ? $"Nichts gefunden für „{q}“." : $"{_appHits.Count} Treffer."; }
    public async Task AppLoadAsync() { _appInstalled = await AppCenter.Backend.InstalledAsync(); _appUpdates = await AppCenter.Backend.UpgradesAsync(); _appMsg = $"{_appInstalled.Count} Programme installiert, {_appUpdates.Count} Aktualisierung(en) verfügbar."; }

    private Control PkgRow(PackageInfo p, string prefix, int i, params (string Action, string Label)[] acts)
    {
        var btns = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        foreach (var (a, l) in acts)
        {
            var b = new Button { Name = $"{prefix}-{a}-{i}", Content = l, Padding = new Thickness(8, 2), IsEnabled = !_appBusy && !(_core.Offline && a != "uninstall") };
            b.Click += async (_, _) => { b.IsEnabled = false; b.Content = "Läuft …"; await AppActionAsync(a, p); Show("appcenter"); };
            btns.Children.Add(b);
        }
        return Row2(new StackPanel { Children = { new TextBlock { Text = p.Name, Foreground = Text, FontSize = 12.5 }, P($"{p.Id} · {p.Version}{(p.Available is { } v ? " → " + v : "")}", null, 10.5) } }, btns);
    }

    private Control AppCenterView()
    {
        var sp = new StackPanel { Spacing = 6, Children = { P($"Trendo nutzt die {AppCenter.Backend.Name}. Vor jeder Änderung fragt Trendo dich – und schreibt alles ins Protokoll.") } };
        var q = new TextBox { Name = "appc-query", PlaceholderText = "Programm suchen, z. B. VLC", Text = _appQuery, Width = 300 };
        var go = new Button { Name = "appc-search", Content = "Suchen", Background = Accent, IsEnabled = !_core.Offline };
        go.Click += async (_, _) => { go.IsEnabled = false; try { await AppSearchAsync(q.Text ?? ""); } catch (Exception ex) { _appMsg = $"Die Paketverwaltung antwortet nicht ({ex.Message})."; } Show("appcenter"); };
        var load = new Button { Name = "appc-load", Content = "Installierte & Updates anzeigen" };
        load.Click += async (_, _) => { load.IsEnabled = false; try { await AppLoadAsync(); } catch (Exception ex) { _appMsg = $"Die Paketverwaltung antwortet nicht ({ex.Message})."; } Show("appcenter"); };
        sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { q, go, load } });
        sp.Children.Add(new TextBlock { Name = "appc-msg", Text = _appMsg, Foreground = Text, TextWrapping = TextWrapping.Wrap });
        var i = 0;
        foreach (var p in _appHits.Take(25)) sp.Children.Add(PkgRow(p, "appc-hit", i++, ("install", "Installieren")));
        if (_appUpdates.Count > 0)
        {
            sp.Children.Add(H("Aktualisierungen", 14)); i = 0;
            foreach (var p in _appUpdates) sp.Children.Add(PkgRow(p, "appc-upd", i++, ("upgrade", "Aktualisieren")));
        }
        if (_appInstalled.Count > 0)
        {
            sp.Children.Add(H("Installiert", 14)); i = 0;
            foreach (var p in _appInstalled.Take(100)) sp.Children.Add(PkgRow(p, "appc-inst", i++, ("uninstall", "Entfernen")));
        }
        return SysPage("Programme verwalten", sp);
    }
}
