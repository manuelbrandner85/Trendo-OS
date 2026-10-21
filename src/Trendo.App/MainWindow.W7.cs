using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Welle 7: CORE-04 Verbund, X-08 Handy-Brücke, SYS-01 Betriebsmodus. Unter „System“ (2 Ebenen).</summary>
public partial class MainWindow
{
    public FusionStore Verbund => new(_core.DataRoot, _core.Audit);
    public PairCode? LastPairCode { get; private set; }
    public string W7Msg { get; private set; } = "";
    public IReadOnlyList<PhoneDevice> Phones { get; private set; } = [];
    /// <summary>Tests: Pfad zu adb vorgeben (sonst echte Suche).</summary>
    public string? AdbOverride { get; set; }
    public Func<Uri, Task<bool>>? HealthOverride { get; set; }

    private void InitW7()
    {
        _showMap["verbund"] = VerbundView; SysTabs.Add(("verbund", "Geräteverbund"));
        _showMap["handy"] = PhoneView; SysTabs.Add(("handy", "Handy"));
        _showMap["modus"] = ModeView; SysTabs.Add(("modus", "Betriebsmodus"));
        StudioPages["CORE-04"] = "verbund"; StudioPages["X-08"] = "handy"; StudioPages["SYS-01"] = "modus";
        ExtraPages.Add(("Geräteverbund (PC, Pi, Android)", "verbund", ["verbund", "pi", "raspberry", "koppeln", "netzwerk", "fusion"]));
        ExtraPages.Add(("Handy verbinden", "handy", ["handy", "android", "smartphone", "adb", "iphone", "telefon"]));
        ExtraPages.Add(("Betriebsmodus", "modus", ["modus", "linux", "usb-stick", "triple", "windows"]));
    }

    private void W7Say(string m, string view) { W7Msg = m; Show(view); }

    public List<string> SelfCaps()
    {
        var l = new List<string> { "office-lesen", "briefe" };
        if (ToolPath("libreoffice") is not null) l.Add("office");
        if (ToolPath("tesseract") is not null) l.Add("ocr");
        if (_kiConnected) l.Add("ki");
        if (ToolPath("ffmpeg") is not null) l.Add("video");
        return l;
    }

    public async Task CheckNodesAsync()
    {
        var f = Verbund;
        foreach (var n in f.Nodes())
        {
            bool ok;
            if (HealthOverride is not null) ok = await HealthOverride(FusionStore.HealthUri(n));
            else { try { using var h = new HttpClient { Timeout = TimeSpan.FromSeconds(3) }; ok = (await h.GetAsync(FusionStore.HealthUri(n))).IsSuccessStatusCode; } catch (Exception e) when (e is HttpRequestException or TaskCanceledException) { ok = false; } }
            f.MarkSeen(n.Id, ok, DateTime.Now);
        }
        W7Say($"{f.Nodes().Count(x => x.Online)} von {f.Nodes().Count} Geräten erreichbar.", "verbund");
    }

    private Control W7Msgs(StackPanel sp) { if (W7Msg.Length > 0) sp.Children.Add(WCard(new TextBlock { Name = "w7-msg", Text = W7Msg, Foreground = Text, TextWrapping = TextWrapping.Wrap })); return sp; }

    private Control VerbundView()
    {
        var sp = new StackPanel { Spacing = 8, Children = { P("Koppel deinen Raspberry Pi, ein Android-Handy oder einen zweiten PC im Heimnetz. Aufgaben laufen dort, wo das passende Werkzeug ist. Nur Heimnetz-Adressen, nie das Internet.") } };
        W7Msgs(sp);
        var code = LastPairCode is { } c && c.Expires > DateTime.Now ? $"Kopplungscode: {c.Code} (gültig bis {c.Expires:HH:mm})" : "Kein Kopplungscode aktiv.";
        sp.Children.Add(WCard(new StackPanel { Spacing = 6, Children = { new TextBlock { Name = "fus-code", Text = code, Foreground = Text, FontSize = 14 },
            Act("fus-newcode", "Neuen Code erzeugen", () => { LastPairCode = Verbund.NewCode(DateTime.Now); W7Say("Code am anderen Gerät eingeben (10 Minuten gültig).", "verbund"); }, true) } }));
        var name = new TextBox { Name = "fus-name", PlaceholderText = "Name, z. B. Pi Wohnzimmer", Width = 200 };
        var addr = new TextBox { Name = "fus-addr", PlaceholderText = "192.168.x.x oder name.local", Width = 180 };
        var port = new TextBox { Name = "fus-port", Text = "8787", Width = 70 };
        var kind = new ComboBox { Name = "fus-kind", ItemsSource = new[] { "Raspberry Pi", "Android", "PC" }, SelectedIndex = 0 };
        var caps = new TextBox { Name = "fus-caps", PlaceholderText = "Fähigkeiten: ki, ocr, video, sensor", Width = 230 };
        var pcode = new TextBox { Name = "fus-pcode", PlaceholderText = "Code", Width = 80 };
        sp.Children.Add(WCard(new StackPanel { Spacing = 6, Children = { new TextBlock { Text = "Gerät hinzufügen", Foreground = Text },
            new WrapPanel { Children = { name, kind, addr, port } }, new WrapPanel { Children = { caps, pcode,
            Act("fus-pair", "Koppeln", () => {
                var (n, m) = Verbund.Pair(pcode.Text ?? "", name.Text ?? "", kind.SelectedIndex switch { 1 => NodeKind.Android, 2 => NodeKind.Pc, _ => NodeKind.Pi }, (addr.Text ?? "").Trim(), int.TryParse(port.Text, out var p) ? p : 0, (caps.Text ?? "").Split(',', ' '), DateTime.Now);
                W7Say(m, "verbund"); }, true) } } } }));
        var nodes = Verbund.Nodes();
        sp.Children.Add(new TextBlock { Text = "Dieser PC: " + string.Join(", ", SelfCaps()), Foreground = Muted, FontSize = 11 });
        if (nodes.Count == 0) sp.Children.Add(P("Noch keine Geräte gekoppelt. Ohne zweites Gerät arbeitet Trendo allein auf diesem PC.", null, 11.5));
        var i = 0;
        foreach (var n in nodes)
        {
            var id = n.Id;
            sp.Children.Add(Row2(new StackPanel { Children = { new TextBlock { Name = $"fus-node-{i}", Text = $"{(n.Online ? "🟢" : "⚪")} {n.Name} · {n.Kind} · {n.Address}:{n.Port}", Foreground = Text },
                P($"Kann: {string.Join(", ", n.Caps)} · zuletzt erreichbar: {(n.LastSeen is { } s ? s.ToString("dd.MM. HH:mm") : "nie")}", null, 11) } },
                Act($"fus-del-{i}", "Entfernen", () => { Verbund.Remove(id); W7Say("Entfernt.", "verbund"); })));
            i++;
        }
        var task = new TextBox { Name = "fus-task", PlaceholderText = "Aufgabe braucht … (ki, ocr, video)", Width = 220 };
        sp.Children.Add(new WrapPanel { Children = { Act("fus-check", "Erreichbarkeit prüfen", () => _ = CheckNodesAsync()), task,
            Act("fus-route", "Wo läuft das?", () => { var (w, why) = FusionStore.Route(task.Text?.Trim() ?? "", SelfCaps(), Verbund.Nodes()); W7Say(w.Length > 0 ? $"→ {w}: {why}" : "Nicht möglich: " + why, "verbund"); }) } });
        return SysPage("Geräteverbund", sp);
    }

    public async Task ScanPhonesAsync()
    {
        var adb = AdbOverride ?? MobileBridge.FindAdb(File.Exists, Environment.GetEnvironmentVariable("LOCALAPPDATA"), Environment.GetEnvironmentVariable("PATH"));
        if (adb is null) { Phones = []; W7Say("Android-Werkzeug (adb) nicht gefunden – siehe Anleitung unten.", "handy"); return; }
        var (e, o) = await Runner.RunAsync(adb, "devices -l");
        Phones = e == 0 ? MobileBridge.ParseDevices(o) : [];
        W7Say(e != 0 ? $"adb meldet Fehler (Exit {e})." : Phones.Count == 0 ? "Kein Handy gefunden. Kabel und USB-Debugging prüfen." : $"{Phones.Count} Handy gefunden.", "handy");
    }

    public async Task PushToPhoneAsync(PhoneDevice d, string file, bool consent)
    {
        var (ok, args, m) = MobileBridge.PushArgs(d, file, consent);
        if (!ok) { W7Say(m, "handy"); return; }
        var adb = AdbOverride ?? MobileBridge.FindAdb(File.Exists, Environment.GetEnvironmentVariable("LOCALAPPDATA"), Environment.GetEnvironmentVariable("PATH"))!;
        var (e, _) = await Runner.RunAsync(adb, args);
        _core.Audit.Append("local-owner", "phone.push", $"{Path.GetFileName(file)} sha={LetterKit.Sha(file)[..16]} exit={e}");
        W7Say(e == 0 ? m + " ✓" : $"Übertragung fehlgeschlagen (Exit {e}).", "handy");
    }

    private Control PhoneView()
    {
        var sp = new StackPanel { Spacing = 8, Children = { P("Dateien aufs Android-Handy schicken und sehen, welches Handy verbunden ist. Trendo steuert das Handy nicht fern und liest keine privaten Daten.") } };
        W7Msgs(sp);
        sp.Children.Add(Act("phone-scan", "Handy suchen", () => _ = ScanPhonesAsync(), true));
        var i = 0;
        foreach (var d in Phones)
        {
            var dev = d; var file = new TextBox { Name = $"phone-file-{i}", PlaceholderText = "Datei, z. B. Brief als PDF", Width = 300 };
            var ok = new CheckBox { Name = $"phone-ok-{i}", Content = "Ich will diese Datei senden" };
            sp.Children.Add(WCard(new StackPanel { Spacing = 4, Children = { new TextBlock { Name = $"phone-dev-{i}", Text = $"📱 {d.Model} – {MobileBridge.StateText(d.State)}", Foreground = Text },
                new WrapPanel { Children = { file, ok, Act($"phone-push-{i}", "Senden", () => _ = PushToPhoneAsync(dev, file.Text?.Trim('"', ' ') ?? "", ok.IsChecked == true)) } } } }));
            i++;
        }
        var g = new StackPanel { Spacing = 2 }; foreach (var s in MobileBridge.Guide) g.Children.Add(P("• " + s, null, 11.5));
        sp.Children.Add(WCard(g));
        return SysPage("Handy", sp);
    }

    private Control ModeView()
    {
        var mode = TripleMode.DetectHere();
        var sp = new StackPanel { Spacing = 8, Children = { P("Trendo läuft als Windows-App, von einem Linux-USB-Stick oder auf dem Raspberry Pi. Hier steht ehrlich, was im aktuellen Modus geht."),
            new TextBlock { Name = "mode-now", Text = "Aktueller Modus: " + TripleMode.Label(mode), Foreground = Text, FontSize = 15 } } };
        bool Has(string t) => t switch { "soffice" => ToolPath("libreoffice") is not null, "winget" => ToolPath("winget") is not null || OperatingSystem.IsWindows(), "unreal" => ToolPath("unreal") is not null, "apt" => File.Exists("/usr/bin/apt"), _ => ToolPath(t) is not null };
        var i = 0;
        foreach (var c in TripleMode.Capabilities(mode, Has))
            sp.Children.Add(Row2(new TextBlock { Name = $"mode-cap-{i++}", Text = $"{(c.State switch { CapState.Ok => "✓", CapState.Fehlt => "○", _ => "✕" })} {c.Feature}", Foreground = Text },
                new TextBlock { Text = (c.State == CapState.Blockiert ? "BLOCKIERT – " : c.State == CapState.Fehlt ? "fehlt – " : "") + c.Detail, Foreground = c.State == CapState.Blockiert ? Brush.Parse("#F87171") : Muted, FontSize = 11, VerticalAlignment = VerticalAlignment.Center }));
        var plan = new StackPanel { Spacing = 2, Children = { new TextBlock { Text = "Linux-USB-Stick vorbereiten (Anleitung)", Foreground = Text } } };
        var removable = DriveInfo.GetDrives().Where(d => { try { return d.IsReady && d.DriveType == DriveType.Removable; } catch (IOException) { return false; } }).ToList();
        foreach (var s in TripleMode.UsbPlan(removable.Count > 0 ? removable.Max(d => d.TotalSize) : 0)) plan.Children.Add(P("• " + s, null, 11.5));
        if (removable.Count == 0) plan.Children.Add(P("Kein USB-Stick eingesteckt.", null, 11));
        sp.Children.Add(WCard(plan));
        return SysPage("Betriebsmodus", sp);
    }
}
