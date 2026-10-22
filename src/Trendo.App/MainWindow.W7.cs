using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Welle 7: CORE-04 Verbund, X-08 Handy-Brücke, SYS-01 Betriebsmodus. Unter „System“ (2 Ebenen).</summary>
public partial class MainWindow
{
    public FusionStore Verbund => new(_core.DataRoot, _core.Audit);
    public string W7Msg { get; private set; } = "";
    public IReadOnlyList<PhoneDevice> Phones { get; private set; } = [];
    /// <summary>Tests: Pfad zu adb vorgeben (sonst echte Suche).</summary>
    public string? AdbOverride { get; set; }
    public Func<Uri, Task<bool>>? HealthOverride { get; set; }
    /// <summary>Tests: eigener HTTP-Handler (z. B. echter Begleiter auf 127.0.0.1).</summary>
    public HttpMessageHandler? VerbundHttp { get; set; }
    public bool AllowLoopbackForTests { get; set; }
    private HttpClient VClient() => VerbundHttp is null ? new HttpClient { Timeout = TimeSpan.FromSeconds(5) } : new HttpClient(VerbundHttp, false) { Timeout = TimeSpan.FromSeconds(5) };
    public string LastNodeInfo { get; private set; } = "";

    /// <summary>Koppeln: Code steht auf dem Pi („trendo-companion pair“), Trendo schickt ihn an das Gerät und bekommt ein Token.</summary>
    public async Task PairRemoteAsync(string address, int port, string code)
    {
        address = address.Trim();
        if (!(FusionStore.IsLanAddress(address) || (AllowLoopbackForTests && address == "127.0.0.1"))) { W7Say("Nur Adressen im Heimnetz sind erlaubt.", "verbund"); return; }
        try
        {
            using var h = VClient();
            var r = await h.PostAsync(new Uri($"http://{address}:{port}/pair"), new StringContent(System.Text.Json.JsonSerializer.Serialize(new PairRequest(code.Trim(), Environment.MachineName)), System.Text.Encoding.UTF8, "application/json"));
            var body = await r.Content.ReadAsStringAsync();
            if (!r.IsSuccessStatusCode) { W7Say("Gerät lehnt ab: " + (System.Text.Json.JsonDocument.Parse(body).RootElement.TryGetProperty("error", out var e) ? e.GetString() : r.StatusCode.ToString()), "verbund"); return; }
            var pr = System.Text.Json.JsonSerializer.Deserialize<PairResponse>(body)!;
            var (n, m) = Verbund.AddPaired(pr, address, port, DateTime.Now, AllowLoopbackForTests);
            if (n is not null) _core.Secrets.Set(FusionStore.TokenKey(n.Id), pr.Token);
            W7Say(m, "verbund");
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException) { W7Say("Gerät nicht erreichbar: läuft „trendo-companion serve“?", "verbund"); }
    }

    public async Task NodeInfoAsync(FusionNode n)
    {
        var tok = _core.Secrets.Get(FusionStore.TokenKey(n.Id));
        if (tok is null) { W7Say("Kein Token gespeichert – bitte neu koppeln.", "verbund"); return; }
        try
        {
            using var h = VClient(); h.DefaultRequestHeaders.Authorization = new("Bearer", tok);
            var r = await h.GetAsync(FusionStore.ApiUri(n, "info"));
            if (!r.IsSuccessStatusCode) { W7Say($"„{n.Name}“ antwortet {(int)r.StatusCode} – Kopplung ungültig?", "verbund"); return; }
            var i = System.Text.Json.JsonSerializer.Deserialize<CompanionInfo>(await r.Content.ReadAsStringAsync())!;
            LastNodeInfo = $"{i.Model} · {i.Cpus} Kerne · RAM {i.MemFreeMb} von {i.MemTotalMb} MB frei" + (i.TempC is { } t ? $" · {t:0.0} °C" : "") + $" · kann: {string.Join(", ", i.Caps)}";
            W7Say($"„{n.Name}“: {LastNodeInfo}", "verbund");
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException) { W7Say($"„{n.Name}“ nicht erreichbar.", "verbund"); }
    }

    private void InitW7()
    {
        _showMap["verbund"] = VerbundView; SysTabs.Add(("verbund", "Geräteverbund"));
        _showMap["handy"] = PhoneView; SysTabs.Add(("handy", "Handy"));
        _showMap["modus"] = ModeView; SysTabs.Add(("modus", "Betriebsmodus"));
        StudioPages["CORE-04"] = "verbund"; StudioPages["X-08"] = "handy"; StudioPages["SYS-01"] = "modus";
        ExtraPages.Add(("Geräteverbund (PC, Pi, Android)", "verbund", ["verbund", "pi", "raspberry", "koppeln", "netzwerk", "fusion"]));
        ExtraPages.Add(("Handy verbinden", "handy", ["handy", "android", "smartphone", "adb", "iphone", "telefon", "wlan", "bluetooth", "qr"]));
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
            else { try { using var h = VClient(); ok = (await h.GetAsync(FusionStore.HealthUri(n))).IsSuccessStatusCode; } catch (Exception e) when (e is HttpRequestException or TaskCanceledException) { ok = false; } }
            f.MarkSeen(n.Id, ok, DateTime.Now);
        }
        W7Say($"{f.Nodes().Count(x => x.Online)} von {f.Nodes().Count} Geräten erreichbar.", "verbund");
    }

    private Control W7Msgs(StackPanel sp) { if (W7Msg.Length > 0) sp.Children.Add(WCard(new TextBlock { Name = "w7-msg", Text = W7Msg, Foreground = Text, TextWrapping = TextWrapping.Wrap })); return sp; }

    private Control VerbundView()
    {
        var sp = new StackPanel { Spacing = 8, Children = { P("Koppel deinen Raspberry Pi, ein Android-Handy oder einen zweiten PC im Heimnetz. Aufgaben laufen dort, wo das passende Werkzeug ist. Nur Heimnetz-Adressen, nie das Internet.") } };
        W7Msgs(sp);
        var addr = new TextBox { Name = "fus-addr", PlaceholderText = "192.168.x.x oder name.local", Width = 200 };
        var port = new TextBox { Name = "fus-port", Text = "8787", Width = 70 };
        var pcode = new TextBox { Name = "fus-pcode", PlaceholderText = "Code vom Gerät", Width = 120 };
        sp.Children.Add(WCard(new StackPanel { Spacing = 6, Children = { new TextBlock { Text = "Gerät koppeln", Foreground = Text },
            P("Auf dem Pi/Linux-Gerät einmalig „trendo-companion netz an“ (nur das eigene Heimnetz), dann „trendo-companion pair“ und den 6-stelligen Code hier eingeben.", null, 11),
            new WrapPanel { Children = { addr, port, pcode, Act("fus-pair", "Koppeln", () => _ = PairRemoteAsync(addr.Text ?? "", int.TryParse(port.Text, out var p) ? p : 0, pcode.Text ?? ""), true) } } } }));
        var nodes = Verbund.Nodes();
        sp.Children.Add(new TextBlock { Text = "Dieser PC: " + string.Join(", ", SelfCaps()), Foreground = Muted, FontSize = 11 });
        if (nodes.Count == 0) sp.Children.Add(P("Noch keine Geräte gekoppelt. Ohne zweites Gerät arbeitet Trendo allein auf diesem PC.", null, 11.5));
        var i = 0;
        foreach (var n in nodes)
        {
            var id = n.Id;
            sp.Children.Add(Row2(new StackPanel { Children = { new TextBlock { Name = $"fus-node-{i}", Text = $"{(n.Online ? "🟢" : "⚪")} {n.Name} · {n.Kind} · {n.Address}:{n.Port}", Foreground = Text },
                P($"Kann: {string.Join(", ", n.Caps)} · zuletzt erreichbar: {(n.LastSeen is { } s ? s.ToString("dd.MM. HH:mm") : "nie")}", null, 11) } },
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { Act($"fus-info-{i}", "Details", () => _ = NodeInfoAsync(n)),
                Act($"fus-del-{i}", "Entfernen", () => { Verbund.Remove(id); _core.Secrets.Delete(FusionStore.TokenKey(id)); W7Say("Entfernt.", "verbund"); }) } }));
            i++;
        }
        var task = new TextBox { Name = "fus-task", PlaceholderText = "Aufgabe braucht … (ki, ocr, video)", Width = 220 };
        sp.Children.Add(new WrapPanel { Children = { Act("fus-check", "Erreichbarkeit prüfen", () => _ = CheckNodesAsync()), task,
            Act("fus-route", "Wo läuft das?", () => { var (w, why) = FusionStore.Route(task.Text?.Trim() ?? "", SelfCaps(), Verbund.Nodes()); W7Say(w.Length > 0 ? $"→ {w}: {why}" : "Nicht möglich: " + why, "verbund"); }) } });
        return SysPage("Geräteverbund", sp);
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
