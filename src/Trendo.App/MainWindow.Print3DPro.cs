using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Trendo.Core;
using Trendo.Core.Print3D;
using Path = System.IO.Path;

namespace Trendo.App;

/// <summary>Bambu Lab voll aus Trendo (Freigabe 05.11.2026): Drucker im Netz finden, Einrichtung samt WLAN, Steuerung (Licht, Tempo, Temperaturen, Lüfter),
/// Filamente im AMS, Fehlermeldungen in Klartext, Live-Kamera, Fertig-Meldung, Zeitraffer, Vorbereiten mit Bambu Studio, mehrere Drucker.</summary>
public partial class MainWindow
{
    List<BambuFound> _prFound = [];
    bool _prSearching, _prSetupOpen, _prLive;
    DispatcherTimer? _prLiveTimer;
    readonly Dictionary<string, BambuClient> _fleet = [];
    /// <summary>Für Tests: statt im Netz zu horchen.</summary>
    public Func<Task<List<BambuFound>>>? DiscoverOverride { get; set; }
    /// <summary>Für Tests: statt Bambu Studio (Modell, Ziel) → erfolgreich?</summary>
    public Func<string, string, Task<bool>>? SliceOverride { get; set; }
    public IReadOnlyList<BambuFound> PrintersFound => _prFound;
    public IReadOnlyList<BambuPrinter> PrinterList => BambuFleet.Load(PrintDir);
    public BambuDetails? PrinterDetails => _bambu?.Details;
    /// <summary>Für Tests: die Verbindung, als wäre ein Drucker verbunden.</summary>
    public BambuClient? PrinterClient => _bambu;

    /// <summary>Drucker im Heimnetz suchen (B1): einige Sekunden auf ihre Meldungen hören.</summary>
    public async Task FindPrinters()
    {
        if (_prSearching) return;
        _prSearching = true; _prMsg = "Suche Bambu-Drucker im Heimnetz …"; Show("3d-druck");
        try
        {
            _prFound = DiscoverOverride is { } fake ? await fake() : _opt.Isolated ? [] : await BambuNetPro.DiscoverAsync(TimeSpan.FromSeconds(6));
            _prMsg = _prFound.Count == 0 ? "Kein Drucker gefunden. Ist er eingeschaltet und im selben WLAN wie dieser PC? Unter „Einrichtung“ steht, wie er ins WLAN kommt." : $"{_prFound.Count} Drucker gefunden – „Übernehmen“ trägt IP und Seriennummer ein.";
        }
        catch (Exception ex) when (ex is System.Net.Sockets.SocketException or IOException) { _prMsg = "Suchen ging nicht: " + ex.Message + " (Port 2021 belegt oder von der Firewall gesperrt?)"; }
        finally { _prSearching = false; }
        Show("3d-druck");
    }

    /// <summary>Gefundenen Drucker übernehmen: Name, IP und Seriennummer – nur der Zugangscode fehlt dann noch.</summary>
    public void TakeFoundPrinter(BambuFound f)
    {
        SavePrinter(f.Name.Length > 0 ? f.Name : f.ModelName, f.Ip, f.Serial, null);
        _prMsg = $"„{Printer!.Name}“ ({f.ModelName}) übernommen. Jetzt den Zugangscode vom Druckerbildschirm eintragen und „Speichern“." + (f.Connect.Equals("lan", StringComparison.OrdinalIgnoreCase) ? "" : " Hinweis: Der Drucker ist noch im Cloud-Modus – für die Steuerung „Nur LAN“ und den Entwicklermodus einschalten (siehe Einrichtung).");
        Show("3d-druck");
    }

    public void SelectPrinter(string serial)
    {
        if (BambuFleet.Load(PrintDir).FirstOrDefault(p => p.Serial == serial) is not { } p) return;
        Printer = p; File.WriteAllText(Path.Combine(PrintDir, "drucker.json"), System.Text.Json.JsonSerializer.Serialize(p));
        _bambu = _fleet.GetValueOrDefault(serial); _prCamera = null;
        _prMsg = $"„{p.Name}“ ausgewählt."; Show("3d-druck");
    }
    public void RemovePrinter(string serial)
    {
        BambuFleet.Remove(PrintDir, serial); _core.Secrets.Delete(new BambuPrinter("", "", serial).SecretKey);
        if (_fleet.Remove(serial, out var c)) _ = c.DisposeAsync();
        if (Printer?.Serial == serial) { Printer = BambuFleet.Load(PrintDir).FirstOrDefault(); _bambu = Printer is { } np ? _fleet.GetValueOrDefault(np.Serial) : null; }
        _prMsg = "Drucker entfernt (auch sein Zugangscode)."; Show("3d-druck");
    }

    /// <summary>Verbindung des aktiven Druckers in die Druckfarm übernehmen und Fertig-Meldung anhängen.</summary>
    void AttachFleet(BambuClient c, BambuPrinter p)
    {
        _fleet[p.Serial] = c;
        c.Finished += (st, ok) => _ui.Post(() =>
        {
            var text = ok ? $"✓ „{p.Name}“: Druck fertig – {st.File}" : $"⚠ „{p.Name}“: Druck fehlgeschlagen – {st.File}";
            Toast(text); _core.Audit.Append(Principal.LocalOwner.Name, ok ? "3ddruck.fertig" : "3ddruck.fehler", p.Name);
            if (CurrentView == "3d-druck") { _prMsg = text; Show("3d-druck"); }
            _ = RaiseEvent("druck-fertig", text); // n8n/Node-RED-Ereignis (N3), nur an Webhooks auf diesem PC
        });
    }

    // ───────────── Steuerung ─────────────
    async Task PrinterDo(string what, Func<BambuClient, Task> run)
    {
        if (_bambu is not { } c) { _prMsg = "Erst mit dem Drucker verbinden."; Show("3d-druck"); return; }
        try { await run(c); _prMsg = what + " – gesendet. Ohne Entwicklermodus ignoriert der Drucker Steuerbefehle."; _core.Audit.Append(Principal.LocalOwner.Name, "3ddruck.steuerung", $"{Printer?.Name}: {what}"); }
        catch (Exception ex) when (ex is IOException or ArgumentException or ObjectDisposedException) { _prMsg = "Ging nicht: " + ex.Message; }
        Show("3d-druck");
    }
    public Task PrinterLight(bool on) => PrinterDo(on ? "Licht an" : "Licht aus", c => c.Light(on));
    public Task PrinterSpeed(int level) => PrinterDo("Tempo " + BambuDetails.SpeedName(level), c => c.Speed(level));
    public Task PrinterNozzle(int celsius) => PrinterDo($"Düse {celsius} °C", c => c.Nozzle(celsius));
    public Task PrinterBed(int celsius) => PrinterDo($"Bett {celsius} °C", c => c.Bed(celsius));
    public Task PrinterFan(int percent) => PrinterDo($"Lüfter {percent} %", c => c.Fan(percent));

    /// <summary>Live-Kamera: alle zwei Sekunden ein neues Bild, solange die Seite offen ist.</summary>
    public void PrinterLive(bool on)
    {
        _prLive = on; _prLiveTimer?.Stop(); _prLiveTimer = null;
        if (on && !_opt.Isolated)
        {
            _prLiveTimer = new DispatcherTimer(TimeSpan.FromSeconds(2), DispatcherPriority.Background, async (_, _) => { if (CurrentView != "3d-druck" || _bambu is null) { PrinterLive(false); return; } await CameraSnapshot(); });
            _prLiveTimer.Start();
        }
        _prMsg = on ? "Live-Kamera an." : "Live-Kamera aus."; Show("3d-druck");
    }

    async Task FetchTimelapse()
    {
        if (Printer is not { } p || _core.Secrets.Get(p.SecretKey) is not { } code || _opt.Isolated) { _prMsg = "Erst den Drucker verbinden."; Show("3d-druck"); return; }
        _prMsg = "Hole das neueste Zeitraffer-Video …"; Show("3d-druck");
        try
        {
            var f = await BambuNetPro.FetchNewestTimelapse(p.Ip, code, Path.Combine(PrintDir, "Zeitraffer"), () => Thumb(p), t => RememberThumb(p, t));
            _prMsg = f is null ? "Auf der Speicherkarte liegt noch kein Zeitraffer – beim Druckstart „Zeitraffer aufnehmen“ ankreuzen." : "Zeitraffer gespeichert: " + Path.GetFileName(f);
            if (f is not null) Launch(Path.GetDirectoryName(f)!);
        }
        catch (Exception ex) when (ex is IOException or System.Net.Sockets.SocketException or System.Security.Authentication.AuthenticationException) { _prMsg = "Zeitraffer holen ging nicht: " + ex.Message; }
        Show("3d-druck");
    }

    /// <summary>B3: Modell direkt in Trendo für den Druck vorbereiten (Bambu Studio im Hintergrund) – danach „Hochladen und drucken“.</summary>
    public async Task PrepareForPrint()
    {
        if (PrintFile is not { } f) { _prMsg = "Erst eine Datei wählen."; Show("3d-druck"); return; }
        var out3mf = Path.Combine(PrintDir, Path.GetFileNameWithoutExtension(f) + ".gcode.3mf");
        _prMsg = "Bambu Studio bereitet die Datei vor (aufstellen, anordnen, slicen) …"; Show("3d-druck");
        bool ok;
        if (SliceOverride is { } fake) ok = await fake(f, out3mf);
        else if (BambuStudioExe() is { } exe && !_opt.Isolated)
        {
            var r = await Task.Run(() => Cli.Run(exe, BambuStudioCli.SliceArgs(f, out3mf), Path.GetDirectoryName(f), 600_000));
            ok = r.Ok && File.Exists(out3mf);
            if (!ok) { _prMsg = "Bambu Studio konnte nicht vorbereiten: " + Short(r.Err.Length > 0 ? r.Err : r.Out) + " – „In Bambu Studio öffnen“ geht immer."; Show("3d-druck"); return; }
        }
        else { _prMsg = "Bambu Studio ist noch nicht eingerichtet (links: „Bambu Studio einrichten“)."; Show("3d-druck"); return; }
        if (!ok || !File.Exists(out3mf)) { _prMsg = "Vorbereiten fehlgeschlagen."; Show("3d-druck"); return; }
        _core.Audit.Append(Principal.LocalOwner.Name, "3ddruck.vorbereitet", Path.GetFileName(out3mf));
        PrintFile = out3mf; _prMsg = $"✓ Druckfertig: {Path.GetFileName(out3mf)} – jetzt „Hochladen und drucken“."; Show("3d-druck");
    }

    public bool PrintTimelapse { get; set; }

    // ───────────── Oberfläche ─────────────
    /// <summary>Karte „Drucker finden und einrichten“ (B1, B2, WLAN) – oberhalb der Eingabefelder.</summary>
    private Control PrinterFindCard()
    {
        var sp = new StackPanel { Spacing = 6 };
        var find = new Button { Name = "print-find", Content = _prSearching ? "Suche …" : "🔍 Drucker im Netz finden", Background = Accent, Foreground = Brushes.White, IsEnabled = !_prSearching };
        find.Click += (_, _) => _ = FindPrinters();
        var setup = new Button { Name = "print-setup", Content = _prSetupOpen ? "Einrichtung schließen" : "Einrichtung (WLAN, Nur LAN, Entwicklermodus)" };
        setup.Click += (_, _) => { _prSetupOpen = !_prSetupOpen; Show("3d-druck"); };
        sp.Children.Add(new WrapPanel { Children = { find, new Border { Width = 6 }, setup } });
        foreach (var (f, i) in _prFound.Select((f, i) => (f, i)))
        {
            var ff = f;
            var take = new Button { Name = $"print-found-{i}", Content = "Übernehmen", Padding = new Thickness(8, 2) }; take.Click += (_, _) => TakeFoundPrinter(ff);
            sp.Children.Add(new DockPanel { Children = { Dk(take, Avalonia.Controls.Dock.Right), P($"{f.ModelName} „{f.Name}“ · {f.Ip} · {(f.Connect.Equals("lan", StringComparison.OrdinalIgnoreCase) ? "LAN-Modus" : "Cloud-Modus")}", Text, 12.5) } });
        }
        if (_prSetupOpen)
        {
            var model = _prFound.FirstOrDefault(f => f.Serial == Printer?.Serial)?.ModelName ?? "";
            var steps = new StackPanel { Name = "print-setup-steps", Spacing = 4 };
            foreach (var (s, i) in BambuSetup.Steps(model).Select((s, i) => (s, i)))
                steps.Children.Add(new StackPanel { Spacing = 1, Children = { new TextBlock { Text = $"{i + 1}. {s.Title}", Foreground = Text, FontSize = 12.5, FontWeight = FontWeight.SemiBold }, P(s.How, Muted, 11.5) } });
            var ips = _opt.Isolated ? [] : BambuNetPro.OwnIps();
            var same = Printer is { } p0 && BambuDiscovery.SameNetwork(ips, p0.Ip);
            steps.Children.Add(P(Printer is null ? "Danach „Drucker im Netz finden“." : same ? $"✓ „{Printer.Name}“ ({Printer.Ip}) ist im selben Netz wie dieser PC." : $"⚠ „{Printer.Name}“ ({Printer.Ip}) scheint in einem anderen Netz zu sein als dieser PC – beide müssen im selben WLAN bzw. Router hängen.",
                same ? Brush.Parse("#2FBF71") : Brush.Parse("#F5A524"), 12));
            sp.Children.Add(new Border { CornerRadius = new CornerRadius(8), Background = Brush.Parse("#1A3D8BFF"), Padding = new Thickness(10), Child = steps });
        }
        return sp;
    }

    /// <summary>Liste aller Drucker (B5) mit Zustand.</summary>
    private Control? PrinterFleetCard()
    {
        var list = BambuFleet.Load(PrintDir); if (list.Count == 0) return null;
        var sp = new StackPanel { Name = "print-fleet", Spacing = 4, Children = { H("Meine Drucker", 14) } };
        foreach (var (p, i) in list.Select((p, i) => (p, i)))
        {
            var pp = p; var on = p.Serial == Printer?.Serial;
            var st = _fleet.GetValueOrDefault(p.Serial)?.Status;
            var state = st is null ? "nicht verbunden" : st.State == "RUNNING" ? $"druckt {st.Percent} % · noch {st.RemainingMin} min" : st.StateText;
            var sel = new Button { Name = $"print-fleet-sel-{i}", Content = (on ? "● " : "") + p.Name, FontWeight = on ? FontWeight.SemiBold : FontWeight.Normal, Padding = new Thickness(8, 2) }; sel.Click += (_, _) => SelectPrinter(pp.Serial);
            var del = new Button { Name = $"print-fleet-del-{i}", Content = "✕", Padding = new Thickness(6, 2) }; ToolTip.SetTip(del, "Drucker entfernen"); del.Click += (_, _) => RemovePrinter(pp.Serial);
            sp.Children.Add(new DockPanel { Children = { Dk(del, Avalonia.Controls.Dock.Right), Dk(sel, Avalonia.Controls.Dock.Left), new TextBlock { Text = "  " + state, Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center } } });
        }
        return sp;
    }

    /// <summary>Steuerung, Filamente und Meldungen des verbundenen Druckers (B4).</summary>
    private Control? PrinterControlCard()
    {
        if (_bambu is not { } c) return null;
        var d = c.Details;
        var sp = new StackPanel { Name = "print-control", Spacing = 6, Children = { H("Steuern", 14), P(d.WifiText, d.WifiDbm >= -67 || d.WifiDbm == int.MinValue ? Muted : Brush.Parse("#F5A524"), 12) } };
        var light = new Button { Name = "print-light", Content = d.LightOn == true ? "💡 Licht aus" : "💡 Licht an" }; light.Click += (_, _) => _ = PrinterLight(d.LightOn != true);
        var speed = new ComboBox { Name = "print-speed", ItemsSource = new[] { "Leise", "Normal", "Sport", "Maximal" }, SelectedIndex = d.SpeedLevel is >= 1 and <= 4 ? d.SpeedLevel - 1 : 1, Width = 110 };
        speed.SelectionChanged += (_, _) => { if (speed.SelectedIndex + 1 != d.SpeedLevel && speed.SelectedIndex >= 0) _ = PrinterSpeed(speed.SelectedIndex + 1); };
        sp.Children.Add(new WrapPanel { Children = { light, new Border { Width = 6 }, new TextBlock { Text = "Tempo", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) }, speed } });
        TextBox Num(string name, string placeholder) => new() { Name = name, PlaceholderText = placeholder, Width = 70 };
        var noz = Num("print-nozzle", "Düse °C"); var bed = Num("print-bed", "Bett °C"); var fan = Num("print-fan", "Lüfter %");
        Button Set(string name, TextBox box, Func<int, Task> go) { var b = new Button { Name = name, Content = "Setzen", Padding = new Thickness(8, 2) }; b.Click += (_, _) => { if (int.TryParse(box.Text, out var v)) _ = go(v); else { _prMsg = "Bitte eine ganze Zahl."; Show("3d-druck"); } }; return b; }
        sp.Children.Add(new WrapPanel { Children = { noz, Set("print-nozzle-set", noz, PrinterNozzle), new Border { Width = 8 }, bed, Set("print-bed-set", bed, PrinterBed), new Border { Width = 8 }, fan, Set("print-fan-set", fan, PrinterFan) } });
        if (d.Trays.Count > 0)
        {
            var ams = new WrapPanel { Name = "print-ams" };
            foreach (var t in d.Trays)
                ams.Children.Add(new Border { Margin = new Thickness(0, 0, 6, 6), Padding = new Thickness(8, 4), CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(t.Active ? 2 : 1), BorderBrush = t.Active ? Accent : Edge,
                    Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { new Border { Width = 14, Height = 14, CornerRadius = new CornerRadius(7), Background = t.ColorHex.Length == 7 ? Brush.Parse(t.ColorHex) : Brushes.Transparent, BorderBrush = Edge, BorderThickness = new Thickness(1) }, new TextBlock { Text = $"Fach {t.Ams}-{t.Slot}: {t.Label}", Foreground = Text, FontSize = 12 } } } });
            sp.Children.Add(H("Filamente (AMS)", 13)); sp.Children.Add(ams);
        }
        if (d.Hms.Count > 0)
        {
            sp.Children.Add(H("Meldungen des Druckers", 13));
            foreach (var (h, i) in d.Hms.Select((h, i) => (h, i)))
            {
                var hh = h;
                var look = new Button { Name = $"print-hms-{i}", Content = "Nachschlagen", Padding = new Thickness(8, 2) }; look.Click += (_, _) => Launch(hh.HelpUrl);
                sp.Children.Add(new DockPanel { Children = { Dk(look, Avalonia.Controls.Dock.Right), P("⚠ " + h.Text, Brush.Parse("#F5A524"), 12) } });
            }
        }
        var live = new Button { Name = "print-live", Content = _prLive ? "⏹ Live-Kamera aus" : "▶ Live-Kamera" }; live.Click += (_, _) => PrinterLive(!_prLive);
        var tl = new Button { Name = "print-timelapse", Content = "Zeitraffer holen" }; tl.Click += (_, _) => _ = FetchTimelapse();
        sp.Children.Add(new WrapPanel { Children = { live, new Border { Width = 6 }, tl } });
        return sp;
    }
}
