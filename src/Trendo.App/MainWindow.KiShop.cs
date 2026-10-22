using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Trendo.Core;

namespace Trendo.App;

/// <summary>KI-Laden und kostenlose Online-Dienste. Nutzt den vorhandenen KI-Dienst, keinen zweiten.</summary>
public sealed partial class MainWindow
{
    public Func<string, IProgress<FetchTick>, CancellationToken, Task<bool>>? ModelPullOverride { get; set; }
    public Func<string, Task<bool>>? ModelRemoveOverride { get; set; }
    public Func<IReadOnlyList<string>>? InstalledModelsOverride { get; set; }
    public Func<HardwareInfo>? HardwareOverride { get; set; }
    public Func<long>? FreeBytesOverride { get; set; }

    private CancellationTokenSource? _fetchCts;
    private string _shopMsg = "";
    private bool _shopBusy;

    private static readonly (string Id, string Name, string KeyUrl, string ValidateUrl, string Note)[] ExtraKeyServices =
    [
        ("xai", "xAI / Grok (optional, kostet Geld)", "https://console.x.ai", "https://api.x.ai/v1/models", "Optional. Die KI auf diesem PC läuft ohne diesen Schlüssel. Die automatische Wahl nutzt ihn nicht."),
        ("github", "GitHub (optional)", "https://github.com/settings/tokens", "https://api.github.com/user", "Für Web & App Studio. GitHub bietet zusätzlich ein offizielles Geräte-Login (OAuth Device Flow) – noch nicht eingebaut."),
    ];

    private void InitKiShop()
    {
        _showMap["ki-laden"] = ShopView;
        if (KiTabs.All(t => t.Key != "ki-laden")) KiTabs.Add(("ki-laden", "KI-Laden"));
        if (ExtraPages.All(p => p.Key != "ki-laden")) ExtraPages.Add(("KI-Laden", "ki-laden", ["modell", "laden", "qwen", "llama", "herunterladen"]));
    }

    private HardwareInfo ThisHardware() => HardwareOverride?.Invoke() ?? HardwareProbe.Read();

    private long FreeBytes()
    {
        if (FreeBytesOverride is { } f) return f();
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(_core.DataRoot));
            return string.IsNullOrEmpty(root) ? 0 : new DriveInfo(root).AvailableFreeSpace;
        }
        catch (Exception) { return 0; }
    }

    private IReadOnlyList<string> ModelsHere()
    {
        if (InstalledModelsOverride is { } f) return f();
        if (LastScan.TryGetValue("ollama", out var ol) && ol.Models.Count > 0) return ol.Models;
        return KiCatalog.Models.Select(m => m.Tag).Where(_deps.HasModel).ToList();
    }

    private bool ModelHere(string tag) => ModelsHere().Any(have => SameModel(have, tag));

    private static bool SameModel(string have, string tag) =>
        have.Equals(tag, StringComparison.OrdinalIgnoreCase) || have.StartsWith(tag + ":", StringComparison.OrdinalIgnoreCase);

    private string ActiveNow()
    {
        var have = ModelsHere();
        var text = KiCatalog.Models.Where(m => m.Job == KiJob.Text && m.FreeToUse && have.Any(h => SameModel(h, m.Tag)))
            .OrderByDescending(m => m.SizeGb).FirstOrDefault();
        return text is null
            ? "Gerade aktiv: noch kein Modell auf diesem PC."
            : LocalAiPlan.ActiveLine("Ollama", text.Name, true, KiJob.Text);
    }

    private static string FetchLine(string tag, FetchTick t)
    {
        var name = KiCatalog.Models.FirstOrDefault(m => m.Tag == tag)?.Name ?? tag;
        var pct = (int)Math.Clamp(t.Fraction * 100, 0, 100);
        var raw = t.Status.ToLowerInvariant();
        var what = raw.Contains("verif") ? "Prüfe die Datei" : raw.Contains("manifest") ? "Prüfe das Modell" : raw.Contains("success") ? "Fertig" : "Lädt";
        return pct > 0 ? $"{name}: {what}, {pct} %." : $"{name}: {what}.";
    }

    private async Task<bool> PullModel(string tag, IProgress<FetchTick> progress, CancellationToken ct)
    {
        if (ModelPullOverride is { } fake) return await fake(tag, progress, ct);
        if (_core.Offline) return false;
        var local = new OllamaBackend();
        if (!await local.IsUpAsync(ct)) await EnsureOllamaRunning();
        if (!await local.IsUpAsync(ct)) return false;
        return await local.PullAsync(tag, progress, ct);
    }

    private async Task<string> FetchTags(IReadOnlyList<string>? only, Action<string> status, Action<double>? bar)
    {
        _fetchCts?.Cancel();
        _fetchCts?.Dispose();
        _fetchCts = new CancellationTokenSource();
        var msg = await ModelFetch.Run(_core.DataRoot, ThisHardware(), FreeBytes(), ModelHere, (tag, progress, ct) =>
            PullModel(tag, new InlineProgress(t =>
            {
                progress.Report(t);
                var line = FetchLine(tag, t);
                if (Dispatcher.UIThread.CheckAccess()) { status(line); bar?.Invoke(t.Fraction); }
                else Dispatcher.UIThread.Post(() => { status(line); bar?.Invoke(t.Fraction); });
            }), ct), _fetchCts.Token, only);
        _shopMsg = msg;
        _core.Audit.Append(Principal.LocalOwner.Name, "ki.fetch", msg);
        return msg;
    }

    private Control ShopView()
    {
        var hw = ThisHardware();
        var have = ModelsHere();
        var disk = LocalAiPlan.CheckDisk(hw, FreeBytes(), ModelHere);
        var book = DownloadBook.Load(_core.DataRoot);
        var rec = LocalAiPlan.Recommend(hw).Select(m => m.Tag).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var sp = new StackPanel { Spacing = 8 };
        sp.Children.Add(P("Kostenlose Modelle auf diesem PC. Danach funktioniert die KI ohne Internet. Der Dienst bleibt auf diesem PC und ist im Netzwerk nicht erreichbar."));
        sp.Children.Add(P($"Liste geprüft am {KiCatalog.CatalogDate}. Größen sind ungefähre Angaben. Es stehen nur frei nutzbare Lizenzen drin.", null, 11));
        sp.Children.Add(new TextBlock { Name = "laden-plan", Text = LocalAiPlan.Sentence(hw) + " " + LocalAiPlan.TierOf(hw).Why, Foreground = Text, TextWrapping = TextWrapping.Wrap });
        sp.Children.Add(new TextBlock { Name = "laden-platz", Text = disk.Message, Foreground = disk.Ok ? Muted : Brush.Parse("#FACC15"), TextWrapping = TextWrapping.Wrap, FontSize = 12 });
        var status = new TextBlock { Name = "laden-status", Text = _shopMsg.Length > 0 ? _shopMsg : book.Items.Count > 0 ? book.Summary() : "Noch nichts geladen.", Foreground = Text, TextWrapping = TextWrapping.Wrap };
        var bar = new ProgressBar { Name = "laden-balken", Minimum = 0, Maximum = 100, Height = 8, IsVisible = _shopBusy };
        var stop = new Button { Name = "laden-abbrechen", Content = "Anhalten", IsEnabled = _shopBusy };
        stop.Click += (_, _) => _fetchCts?.Cancel();
        var go = new Button { Name = "laden-holen-empfohlen", Content = "Empfohlene holen", Background = Accent, IsEnabled = !_shopBusy && !_core.Offline && disk.Ok };
        go.Click += async (_, _) => await RunFetch(null, status, bar, stop);
        var again = new Button { Name = "laden-weiter", Content = "Weitermachen", IsEnabled = !_shopBusy && book.Items.Any(i => i.Phase is "pause" or "fehler" or "geplant") };
        again.Click += async (_, _) => await RunFetch(null, status, bar, stop);
        sp.Children.Add(status);
        sp.Children.Add(bar);
        sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { go, again, stop } });
        if (_core.Offline) sp.Children.Add(P("Ohne Internet kannst du nichts Neues holen. Was schon da ist, bleibt nutzbar.", Brush.Parse("#FACC15"), 11));
        var head = false; var more = false;
        foreach (var m in LocalAiPlan.Recommend(hw).Concat(KiCatalog.Models.Where(m => !rec.Contains(m.Tag))))
        {
            if (rec.Contains(m.Tag)) { if (!head) { sp.Children.Add(H("Empfohlen für diesen PC", 14)); head = true; } }
            else if (!more) { sp.Children.Add(H("Weitere freie Modelle", 14)); more = true; }
            sp.Children.Add(ModelCard(m, rec.Contains(m.Tag), hw, have, status, bar, stop));
        }
        sp.Children.Add(P("Ein llama.cpp-Server auf diesem PC wird mitgenutzt, wenn du ihn selbst gestartet hast. Trendo lädt ihn nicht von allein.", null, 11));
        var online = new Button { Name = "laden-online", Content = "Kostenlose Online-Dienste" };
        online.Click += (_, _) => Show("keys");
        sp.Children.Add(online);
        return KiPage("KI-Laden", sp);
    }

    private async Task RunFetch(IReadOnlyList<string>? only, TextBlock status, ProgressBar bar, Button stop)
    {
        if (_shopBusy) return;
        _shopBusy = true; stop.IsEnabled = true; bar.IsVisible = true; status.Text = "Lädt …";
        try { await FetchTags(only, t => status.Text = t, f => { bar.Value = Math.Clamp(f, 0, 1) * 100; bar.IsVisible = true; }); }
        catch (Exception ex) { _shopMsg = "Hat nicht geklappt: " + ex.Message; }
        finally { _shopBusy = false; Show("ki-laden"); }
    }

    private Control ModelCard(FreeModel m, bool recommended, HardwareInfo hw, IReadOnlyList<string> have, TextBlock status, ProgressBar bar, Button stop)
    {
        var id = LocalAiPlan.SafeId(m.Tag);
        var got = have.Any(h => SameModel(h, m.Tag));
        var fit = LocalAiPlan.FitsPc(m, hw);
        var body = new StackPanel { Spacing = 3 };
        body.Children.Add(new TextBlock { Text = m.Name + (recommended ? " · Empfohlen" : ""), Foreground = Text, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
        if (recommended) body.Children.Add(new TextBlock { Name = "laden-tip-" + id, Text = "Empfohlen", Foreground = Brush.Parse("#4ADE80"), FontSize = 11 });
        body.Children.Add(new TextBlock { Name = "laden-fit-" + id, Text = $"{m.Zweck} · ca. {m.SizeGb:0.#} GB · Lizenz {m.License} · {LocalAiPlan.FitText(m, hw)}", Foreground = fit ? Muted : Brush.Parse("#FACC15"), FontSize = 11, TextWrapping = TextWrapping.Wrap });
        var phase = DownloadBook.Load(_core.DataRoot).Items.FirstOrDefault(i => i.Tag == m.Tag);
        if (phase is not null && phase.Phase is not "fertig") body.Children.Add(P(phase.Message, null, 11));
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        var get = new Button { Name = "laden-holen-" + id, Content = got ? "Ist da" : "Holen", IsEnabled = !got && fit && !_shopBusy && !_core.Offline, Padding = new Thickness(8, 2) };
        get.Click += async (_, _) => await RunFetch([m.Tag], status, bar, stop);
        var del = new Button { Name = "laden-weg-" + id, Content = "Entfernen", IsEnabled = got && !_shopBusy, Padding = new Thickness(8, 2) };
        del.Click += async (_, _) =>
        {
            del.IsEnabled = false;
            var ok = ModelRemoveOverride is { } fake ? await fake(m.Tag) : await new OllamaBackend().DeleteAsync(m.Tag);
            _shopMsg = ok ? $"{m.Name} ist entfernt." : $"{m.Name} ließ sich nicht entfernen.";
            _core.Audit.Append(Principal.LocalOwner.Name, ok ? "ki.remove" : "ki.remove.fail", m.Tag);
            Show("ki-laden");
        };
        row.Children.Add(get); row.Children.Add(del); body.Children.Add(row);
        return WCard(body);
    }

    private Control KeysPage()
    {
        var sp = new StackPanel { Spacing = 8, Children = {
            P("Die KI auf diesem PC braucht keinen Schlüssel. Kostenlose Online-Dienste sind ein Zusatz. Ohne beide Häkchen bleibt jede Frage hier. Trendo legt kein Konto an und kauft nichts."),
            OnlineEditors(false),
            H("Weitere Dienste", 14),
            P("Diese Dienste nutzt die automatische Wahl nicht.", null, 11),
        } };
        foreach (var k in ExtraKeyServices) sp.Children.Add(LegacyKeyCard(k));
        return Page("Dienste & Schlüssel", sp);
    }

    private Control WizardAiBlock()
    {
        var hw = ThisHardware();
        var disk = LocalAiPlan.CheckDisk(hw, FreeBytes(), ModelHere);
        var sp = new StackPanel { Spacing = 6 };
        sp.Children.Add(new TextBlock { Name = "wiz-ki-plan", Text = LocalAiPlan.Sentence(hw), Foreground = Text, TextWrapping = TextWrapping.Wrap });
        sp.Children.Add(new TextBlock { Name = "wiz-ki-disk", Text = disk.Message, Foreground = disk.Ok ? Muted : Brush.Parse("#FACC15"), TextWrapping = TextWrapping.Wrap, FontSize = 12 });
        sp.Children.Add(P("Kostenlose Online-Dienste sind aus, bis du sie einschaltest. Die Frage geht dann ins Internet. Es wird nichts gekauft und kein Konto angelegt."));
        sp.Children.Add(OnlineEditors(true));
        var stop = new Button { Name = "wiz-ki-stop", Content = "Laden anhalten" };
        stop.Click += (_, _) => _fetchCts?.Cancel();
        sp.Children.Add(stop);
        return sp;
    }

    private Control OnlineEditors(bool setup)
    {
        var opt = OnlineOptIn.Load(_core.DataRoot);
        var sp = new StackPanel { Spacing = 6 };
        var master = new CheckBox
        {
            Name = setup ? "wiz-opt-master" : "opt-master",
            Foreground = Text,
            Content = "Ich erlaube kostenlose Online-Dienste, wenn auf diesem PC keins reicht.",
        };
        master.IsChecked = opt.Allow;
        master.IsCheckedChanged += (_, _) => { opt.Allow = master.IsChecked == true; opt.Save(_core.DataRoot); };
        sp.Children.Add(master);
        foreach (var p in FreeOnline.Providers)
        {
            var row = new StackPanel { Spacing = 4 };
            var on = new CheckBox { Name = (setup ? "wiz-opt-" : "opt-") + p.Id, Foreground = Text, Content = p.Name };
            on.IsChecked = opt.On.Contains(p.Id);
            on.IsCheckedChanged += (_, _) => { opt.Set(p.Id, on.IsChecked == true); opt.Save(_core.DataRoot); };
            row.Children.Add(on);
            row.Children.Add(P(p.Note + " " + p.Limits, null, 11));
            var get = new Button { Name = (setup ? "wiz-getkey-" : "btn-getkey-") + p.Id, Content = "Schlüssel holen", IsEnabled = !_core.Offline };
            get.Click += (_, _) => Launch(p.KeyUrl);
            var has = _core.Secrets.Get(FreeOnline.SecretKey(p.Id)) is not null;
            var box = new TextBox { Name = (setup ? "wiz-txt-" : "txt-key-") + p.Id, PasswordChar = '•', PlaceholderText = "Schlüssel hier einfügen", Width = 260 };
            var state = new TextBlock { Name = (setup ? "wiz-key-state-" : "key-state-") + p.Id, Text = has ? "gespeichert ✓" : "kein Schlüssel", Foreground = Muted, FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
            var save = new Button { Name = (setup ? "wiz-savekey-" : "btn-savekey-") + p.Id, Content = "Prüfen & speichern", IsEnabled = !_core.Offline };
            save.Click += async (_, _) => await SaveCheckedKey(p.Id, box.Text, p.CheckUrl, state, box);
            row.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { get, box, save, state } });
            sp.Children.Add(WCard(row));
        }
        foreach (var (name, why) in FreeOnline.LeftOut) sp.Children.Add(P($"{name}: {why}", null, 11));
        return sp;
    }

    private Control LegacyKeyCard((string Id, string Name, string KeyUrl, string ValidateUrl, string Note) k)
    {
        var has = _core.Secrets.Get(FreeOnline.SecretKey(k.Id)) is not null;
        var box = new TextBox { Name = "txt-key-" + k.Id, PasswordChar = '•', PlaceholderText = "Schlüssel hier einfügen", Width = 280 };
        var state = new TextBlock { Name = "key-state-" + k.Id, Text = has ? "gespeichert ✓" : "kein Schlüssel", Foreground = Muted, FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
        var get = new Button { Name = "btn-getkey-" + k.Id, Content = "Schlüssel holen", IsEnabled = !_core.Offline };
        get.Click += (_, _) => Launch(k.KeyUrl);
        var save = new Button { Name = "btn-savekey-" + k.Id, Content = "Prüfen & speichern", IsEnabled = !_core.Offline };
        save.Click += async (_, _) => await SaveCheckedKey(k.Id, box.Text, k.ValidateUrl, state, box);
        return WCard(new StackPanel { Spacing = 6, Children = {
            new TextBlock { Text = k.Name, Foreground = Text, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap },
            P(k.Note, null, 11),
            new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { get, box, save, state } },
        } });
    }

    private async Task SaveCheckedKey(string id, string? raw, string validateUrl, TextBlock state, TextBox box)
    {
        var key = raw?.Trim() ?? "";
        if (key.Length == 0) return;
        state.Text = "Prüfe …";
        var ok = KeyValidatorOverride is { } fake ? await fake(key) : await ValidateKey(validateUrl, key);
        if (ok) { _core.Secrets.Set(FreeOnline.SecretKey(id), key); box.Text = ""; }
        _core.Audit.Append(Principal.LocalOwner.Name, ok ? "key.saved" : "key.invalid", id);
        state.Text = ok ? "gültig und gespeichert ✓" : "ungültig – nicht gespeichert";
    }
}
