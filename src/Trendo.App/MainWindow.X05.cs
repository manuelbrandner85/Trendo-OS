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
        InitKiLibrary();
        StudioPages["X-05"] = "ki-quellen";
        ExtraPages.Add(("KI-Quellen", "ki-quellen", ["lm studio", "llama", "jan", "modell", "kostenlos", "quelle"]));
    }

    /// <summary>Vorgabe 2026-10-25: freie Modelle (Apache-2.0/MIT, passend zum Arbeitsspeicher) nach dem Start ohne Klick laden, eins nach dem anderen.
    /// Modelle mit eigenen Lizenzbedingungen (z. B. Llama, Gemma, Qwen Research) brauchen weiter eine Bestätigung.</summary>
    public async Task<List<string>> AutoInstallModelsAsync(Func<Task<IReadOnlyList<string>>> have, Func<string, Task<bool>> pull)
    {
        var loaded = new List<string>();
        var set = KiSourceSettings.Load(_core.DataRoot);
        if (!set.AutoModels || _core.Offline) return loaded;
        var todo = KiCatalog.AutoModels(new HardwareInfo(TotalRam, null, 0), await have()).Where(m => set.AutoFailed.GetValueOrDefault(m.Tag) < 3).ToList();
        foreach (var m in todo)
        {
            var free = FreeBytesForModels?.Invoke() ?? FreeBytes(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            if (free < (long)((m.SizeGb + 5) * 1024 * 1024 * 1024)) { _srcMsg = $"Zu wenig Speicherplatz für {m.Name} ({m.SizeGb:0.0} GB + 5 GB Reserve) – übersprungen."; _core.Audit.Append("app", "ki.model.auto.skip", $"{m.Tag} Speicher"); continue; }
            var req = _core.Approvals.Request(Principal.LocalOwner, "ki.pull", $"KI-Modell {m.Name} ({m.SizeGb:0.0} GB, {m.License}) automatisch laden", m.Tag);
            _core.Approvals.Approve(req.Id, Principal.LocalOwner); // Vorgabe des Besitzers: freie Modelle automatisch
            _core.Audit.Append("app", "ki.model.auto.start", $"{m.Tag} {m.SizeGb:0.0} GB {m.License}");
            _srcMsg = $"Lade KI-Modell {m.Name} ({m.SizeGb:0.0} GB) …";
            var ok = await _core.Approvals.Execute(req.Id, "ki.pull", m.Tag, () => pull(m.Tag));
            _core.Audit.Append("app", ok ? "ki.model.auto.ok" : "ki.model.auto.fail", m.Tag);
            if (ok) loaded.Add(m.Tag);
            KiSourceSettings.Update(_core.DataRoot, cur => { if (ok) cur.AutoFailed.Remove(m.Tag); else cur.AutoFailed[m.Tag] = cur.AutoFailed.GetValueOrDefault(m.Tag) + 1; });
            _srcMsg = ok ? $"KI-Modell {m.Name} ist da." : $"{m.Name} konnte nicht geladen werden – Trendo versucht es beim nächsten Start erneut.";
        }
        return loaded;
    }

    /// <summary>Befund 27.10.2026: Ohne laufendes Ollama waren alle Lade-Knöpfe grau, ohne Erklärung. Jetzt: Grund und der nächste Schritt.</summary>
    private Control OllamaHint(string page)
    {
        var msg = new TextBlock { Name = "ollama-hint-msg", Foreground = Text, FontSize = 12, TextWrapping = TextWrapping.Wrap };
        var start = new Button { Name = "ollama-start", Content = "Ollama jetzt starten", Background = Accent, IsEnabled = !_depBusy };
        start.Click += async (_, _) =>
        {
            start.IsEnabled = false; msg.Text = "Ollama wird gestartet …";
            await EnsureOllamaRunning(); await ScanSourcesAsync();
            msg.Text = LastScan.TryGetValue("ollama", out var r) && r.Up ? "Ollama läuft – die Modelle lassen sich jetzt laden." : "Ollama ist nicht eingerichtet. Richte es über das Vollpaket ein (Knopf daneben).";
            Show(page);
        };
        var setup = new Button { Name = "ollama-setup", Content = "Programme einrichten …" };
        setup.Click += (_, _) => Show("components");
        return new Border { Name = "ollama-hint", Background = Brush.Parse("#33F59E0B"), CornerRadius = new CornerRadius(12), Padding = new Thickness(12, 10),
            Child = new StackPanel { Spacing = 6, Children = {
                new TextBlock { Text = "Zum Laden der Modelle muss Ollama laufen", Foreground = Text, FontSize = 14, FontWeight = FontWeight.SemiBold },
                P(_core.Offline ? "Trendo ist im Offline-Modus – Modelle werden dann nicht geladen. Schalte den Offline-Modus in den Einstellungen aus."
                    : "Ollama ist das freie Programm, das die KI-Modelle auf deinem PC ausführt. Es kommt mit dem Vollpaket. Läuft es nicht, sind die Lade-Knöpfe gesperrt.", null, 12),
                new WrapPanel { Children = { start, new Border { Width = 6 }, setup } }, msg } } };
    }

    /// <summary>Für Tests: freier Speicherplatz in Bytes.</summary>
    public Func<long>? FreeBytesForModels { get; set; }
    private static long FreeBytes(string path) { try { return new DriveInfo(Path.GetPathRoot(Path.GetFullPath(path))!).AvailableFreeSpace; } catch (Exception e) when (e is IOException or ArgumentException or UnauthorizedAccessException) { return 0; } }

    private async Task AutoModelsAfter(Task ollamaStart)
    {
        try { await ollamaStart; } catch (Exception ex) when (ex is not OutOfMemoryException) { return; }
        var o = new OllamaBackend();
        if (!await o.IsUpAsync()) return;
        await AutoInstallModelsAsync(() => o.ModelsAsync(), tag => o.PullAsync(tag));
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
        var sp = new StackPanel { Spacing = 6, Children = { P("Trendo nutzt zuerst die kostenlose KI auf diesem PC. Ist die erste Quelle aus, fragt Trendo automatisch die nächste. Freie Modelle lädt Trendo selbst; KI-Dienste mit Schlüssel trägst du unter „Dienste & Schlüssel“ ein.") } };
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
            up.Click += (_, _) => { KiSourceSettings.Update(_core.DataRoot, s => s.Move(id, -1)); Show("ki-quellen"); };
            var tg = new Button { Name = "src-toggle-" + id, Content = off ? "Einschalten" : "Ausschalten", Padding = new Thickness(8, 2) };
            tg.Click += (_, _) => { KiSourceSettings.Update(_core.DataRoot, s => s.Toggle(id)); Show("ki-quellen"); };
            var web = new Button { Name = "src-web-" + id, Content = "Herstellerseite", Padding = new Thickness(8, 2), IsEnabled = !_core.Offline };
            web.Click += (_, _) => Launch(s.OfficialUrl);
            sp.Children.Add(WCard(new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = {
                new StackPanel { Children = { new TextBlock { Text = $"{pos + 1}. {s.Name}{(off ? " (aus)" : "")}", Foreground = Text, FontSize = 13 }, P($"{state} · Lizenz: {s.License}", null, 10.5), P(s.Hint, null, 10.5), new Border { Margin = new Thickness(0, 4, 0, 0), Child = KiSourceButtons(s) } } },
                Pos(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center, Children = { up, tg, web } }, 1) } }));
        }
        sp.Children.Add(H("Freie Modelle", 14));
        var olUp = LastScan.TryGetValue("ollama", out var ol0) && ol0.Up;
        if (!olUp) sp.Children.Add(OllamaHint("ki-quellen"));
        var auto = new CheckBox { Name = "ki-auto-models", Content = "Freie Modelle (Apache-2.0, MIT) automatisch laden, passend zu diesem PC", IsChecked = set.AutoModels, Foreground = Text };
        auto.IsCheckedChanged += (_, _) => { var cur = KiSourceSettings.Update(_core.DataRoot, s => s.AutoModels = auto.IsChecked == true); _core.Audit.Append(Principal.LocalOwner.Name, cur.AutoModels ? "ki.auto.on" : "ki.auto.off", ""); };
        sp.Children.Add(auto);
        var hw = new HardwareInfo(TotalRam, null, 0);
        var have = LastScan.TryGetValue("ollama", out var ol) && ol.Up ? ol.Models : [];
        foreach (var m in KiCatalog.Models)
        {
            var fits = KiCatalog.Fits(m, hw); var got = have.Contains(m.Tag);
            var b = new Button { Name = "model-pull-" + m.Tag.Replace(':', '-').Replace('.', '-'), Content = got ? "✓ vorhanden" : KiCatalog.AutoLicense(m) ? (fits ? "Jetzt laden (sonst automatisch)" : "passt nicht") : $"Lizenz „{m.License}“ bestätigen & laden", IsEnabled = !got && fits && ol.Up && !_core.Offline, Padding = new Thickness(8, 2) };
            // Befund 27.10.2026: gesperrte Knöpfe sagen, warum
            if (!b.IsEnabled && !got) ToolTip.SetTip(b, !fits ? $"Braucht {m.MinRamGb} GB Arbeitsspeicher." : _core.Offline ? "Im Offline-Modus wird nichts geladen." : "Zuerst muss Ollama laufen – siehe Hinweis oben.");
            b.Click += async (_, _) =>
            {
                var req = _core.Approvals.Request(Principal.LocalOwner, "ki.pull", $"KI-Modell {m.Name} ({m.SizeGb:0.0} GB) laden", m.Tag);
                _core.Approvals.Approve(req.Id, Principal.LocalOwner);
                b.IsEnabled = false; b.Content = "Lädt …";
                var ok = await _core.Approvals.Execute(req.Id, "ki.pull", m.Tag, () => new OllamaBackend().PullAsync(m.Tag));
                _srcMsg = ok ? $"{m.Name} ist jetzt da." : $"{m.Name} konnte nicht geladen werden – Internet prüfen und erneut versuchen.";
                await ScanSourcesAsync(); Show("ki-quellen");
            };
            Control right = b;
            if (got)
            {
                var tag = m.Tag;
                right = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { b, ConfirmRemoveButton("ollama:" + tag, "model-remove-" + tag.Replace(':', '-').Replace('.', '-'), "ki-quellen", async () =>
                {
                    var ok = await new OllamaBackend().DeleteAsync(tag);
                    _core.Audit.Append(Principal.LocalOwner.Name, ok ? "ki.model.remove" : "ki.model.remove.failed", tag);
                    _srcMsg = ok ? $"{m.Name} wurde entfernt – der Speicherplatz ist frei." : $"{m.Name} ließ sich nicht entfernen (läuft Ollama?).";
                    await ScanSourcesAsync();
                }) } };
            }
            sp.Children.Add(WCard(new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = {
                new StackPanel { Children = { new TextBlock { Text = m.Name, Foreground = Text, FontSize = 13 }, P($"{m.GoodFor} · {m.SizeGb:0.0} GB · {(fits ? "passt zu deinem PC" : $"braucht {m.MinRamGb} GB Arbeitsspeicher")} · Lizenz: {m.License}", fits ? null : Brush.Parse("#FACC15"), 10.5) } },
                Pos(right, 1) } }));
        }
        return KiPage("KI-Quellen", sp);
    }
}
