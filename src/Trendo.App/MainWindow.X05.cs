using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>X-05: freie KI-Quellen auf diesem PC finden, Reihenfolge festlegen, freie Modelle holen.</summary>
public partial class MainWindow
{
    public Func<KiSourceInfo, IKiBackend>? KiBackendFactory { get; set; }
    public Dictionary<string, (bool Up, IReadOnlyList<string> Models)> LastScan { get; } = new();
    private string _srcMsg = "";

    private IKiBackend MakeBackend(KiSourceInfo s) => KiBackendFactory?.Invoke(s) ?? (s.Protocol == KiProtocol.Ollama ? new OllamaBackend(s.BaseUrl) : new OpenAiCompatBackend(s.Name, s.BaseUrl));

    private void InitX05()
    {
        _showMap["ki-quellen"] = SourcesView; KiTabs.Add(("ki-quellen", "KI-Quellen"));
        StudioPages["X-05"] = "ki-quellen";
        ExtraPages.Add(("KI-Quellen", "ki-quellen", ["lm studio", "llama", "jan", "modell", "kostenlos", "quelle"]));
    }

    public async Task ScanSourcesAsync()
    {
        LastScan.Clear();
        await Task.WhenAll(KiCatalog.Sources.Select(async s =>
        {
            var b = MakeBackend(s);
            var up = await b.IsUpAsync();
            var models = up ? await b.ModelsAsync() : [];
            lock (LastScan) LastScan[s.Id] = (up, models);
        }));
        _core.Audit.Append(Principal.LocalOwner.Name, "ki.scan", string.Join(", ", LastScan.Select(kv => $"{kv.Key}={(kv.Value.Up ? kv.Value.Models.Count + "M" : "aus")}")));
        _srcMsg = LastScan.Values.Any(v => v.Up) ? $"{LastScan.Values.Count(v => v.Up)} KI-Quelle(n) auf diesem PC gefunden." : "Keine KI-Quelle gefunden. Richte Ollama unter „Zusatzprogramme“ ein – kostenlos.";
    }

    private Control SourcesView()
    {
        var set = KiSourceSettings.Load(_core.DataRoot);
        var sp = new StackPanel { Spacing = 6, Children = { P("Trendo nutzt nur kostenlose KI auf diesem PC. Ist die erste Quelle aus, fragt Trendo automatisch die nächste.") } };
        var scan = new Button { Name = "src-scan", Content = "Jetzt suchen", Background = Accent };
        var srcMsg = new TextBlock { Name = "src-msg", Text = _srcMsg, Foreground = Text, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
        scan.Click += async (_, _) =>
        {
            scan.IsEnabled = false; _srcMsg = "Suche läuft …"; srcMsg.Text = _srcMsg;
            try { await ScanSourcesAsync(); }
            catch (Exception ex) { _srcMsg = "Suche hat nicht geklappt: " + ex.Message; }
            Show("ki-quellen");
        };
        sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { scan, srcMsg } });
        sp.Children.Add(H("Reihenfolge", 14));
        var n = 0;
        foreach (var id in set.Order)
        {
            var s = KiCatalog.Sources.First(x => x.Id == id); var pos = n++;
            var off = set.Disabled.Contains(id);
            var state = LastScan.TryGetValue(id, out var r) ? r.Up ? $"✓ läuft · {r.Models.Count} Modell(e){(r.Models.Count > 0 ? ": " + string.Join(", ", r.Models.Take(3)) : "")}" : "nicht gefunden" : "noch nicht gesucht";
            var up = new Button { Name = "src-up-" + id, Content = "▲", IsEnabled = pos > 0, Padding = new Thickness(8, 2) };
            up.Click += (_, _) => { set.Move(id, -1); set.Save(_core.DataRoot); Show("ki-quellen"); };
            var tg = new Button { Name = "src-toggle-" + id, Content = off ? "Einschalten" : "Ausschalten", Padding = new Thickness(8, 2) };
            tg.Click += (_, _) => { set.Toggle(id); set.Save(_core.DataRoot); Show("ki-quellen"); };
            var web = new Button { Name = "src-web-" + id, Content = "Herstellerseite", Padding = new Thickness(8, 2), IsEnabled = !_core.Offline };
            web.Click += (_, _) => Launch(s.OfficialUrl);
            sp.Children.Add(WCard(new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = {
                new StackPanel { Children = { new TextBlock { Text = $"{pos + 1}. {s.Name}{(off ? " (aus)" : "")}", Foreground = Text, FontSize = 13 }, P($"{state} · Lizenz: {s.License}", null, 10.5), P(s.Hint, null, 10.5) } },
                Pos(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center, Children = { up, tg, web } }, 1) } }));
        }
        sp.Children.Add(H("Freie Modelle", 14));
        var hw = new HardwareInfo(TotalRam, null, 0);
        var have = LastScan.TryGetValue("ollama", out var ol) && ol.Up ? ol.Models : [];
        foreach (var m in KiCatalog.Models)
        {
            var fits = KiCatalog.Fits(m, hw); var got = have.Contains(m.Tag);
            var b = new Button { Name = "model-pull-" + m.Tag.Replace(':', '-').Replace('.', '-'), Content = got ? "✓ vorhanden" : "Holen (Zustimmung)", IsEnabled = !got && fits && ol.Up && !_core.Offline, Padding = new Thickness(8, 2) };
            b.Click += async (_, _) =>
            {
                var req = _core.Approvals.Request(Principal.LocalOwner, "ki.pull", $"KI-Modell {m.Name} ({m.SizeGb:0.0} GB) laden", m.Tag);
                _core.Approvals.Approve(req.Id, Principal.LocalOwner);
                b.IsEnabled = false; b.Content = "Lädt …";
                var ok = await _core.Approvals.Execute(req.Id, "ki.pull", m.Tag, () => new OllamaBackend().PullAsync(m.Tag));
                _srcMsg = ok ? $"{m.Name} ist jetzt da." : $"{m.Name} konnte nicht geladen werden – Internet prüfen und erneut versuchen.";
                await ScanSourcesAsync(); Show("ki-quellen");
            };
            sp.Children.Add(WCard(new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = {
                new StackPanel { Children = { new TextBlock { Text = m.Name, Foreground = Text, FontSize = 13 }, P($"{m.GoodFor} · {m.SizeGb:0.0} GB · {(fits ? "passt zu deinem PC" : $"braucht {m.MinRamGb} GB Arbeitsspeicher")} · Lizenz: {m.License}", fits ? null : Brush.Parse("#FACC15"), 10.5) } },
                Pos(b, 1) } }));
        }
        return KiPage("KI-Quellen", sp);
    }
}
