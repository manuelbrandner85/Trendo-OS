using System.Net;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Trendo.Core;

namespace Trendo.App;

/// <summary>„Handy verbinden – ganz einfach“ (Vorgabe 27.10.2026): QR-Code mit der Handy-Kamera scannen, „Verbinden“ tippen, fertig.
/// Wunsch 27.10.2026: Schritt für Schritt – jeder Schritt wird geprüft, erst wenn er geklappt hat, kommt der nächste. Ohne Technikwissen,
/// zuhause über das WLAN, überall über Tailscale (Einrichtung aus Trendo heraus). Der alte Weg über die Entwickleroptionen bleibt als „Profi“ darunter.</summary>
public partial class MainWindow
{
    PhoneAccess? _phoneAccess;
    PhoneLinkServer? _phoneServer;
    string _phoneServerError = "";
    public PhoneAccess PhoneAccess => _phoneAccess ??= new PhoneAccess(_core.DataRoot);
    public string PhoneInbox => WorkFolder("Vom Handy");
    public string PhoneOutbox => WorkFolder("Zum Handy");
    /// <summary>Für Tests: feste Adressen statt der echten Netzwerkkarten.</summary>
    public Func<IReadOnlyList<IPAddress>>? PhoneAddressesOverride { get; set; }
    /// <summary>Für Tests: Windows-Freigabe nicht wirklich anlegen (Argumente für netsh).</summary>
    public Func<string[], bool>? PhoneFirewallOverride { get; set; }
    /// <summary>Für Tests: ist Tailscale am PC installiert?</summary>
    public Func<bool>? TailscaleInstalledOverride { get; set; }
    /// <summary>Für Tests: Tailscale-Anmeldung nicht wirklich starten.</summary>
    public Action? TailscaleLoginOverride { get; set; }
    public PhoneLinkServer? PhoneServer => _phoneServer;

    // Assistent: wo soll das Handy den PC erreichen, welcher Schritt läuft gerade
    string? _phoneWhere;            // "home" (WLAN) oder "away" (überall, Tailscale)
    int _phoneAddrIx;
    DateTime _phoneQrAt;
    /// <summary>Wann der aktuelle Code gezeigt wurde (für Tests: „30 Sekunden vergangen“ nachstellen).</summary>
    public DateTime PhoneQrAt { get => _phoneQrAt; set => _phoneQrAt = value; }
    string? _phoneNewDevice;
    bool _phoneRemoteAsked, _phoneTsPhoneDone;
    string _phoneMsg = "", _phoneSig = "";
    string? _phoneSsid;
    DispatcherTimer? _phoneTimer;
    PhoneRemoteBridge? _phoneRemote;

    /// <summary>Netzwerkkarten des PCs (Heimnetz und Tailscale) mit Angaben für die Adresswahl.</summary>
    public IReadOnlyList<PhoneAccess.Nic> PhoneNics()
    {
        if (PhoneAddressesOverride is not null) return PhoneAddressesOverride().Select(a => new PhoneAccess.Nic(a, "Test", true, false, false)).ToList();
        var list = new List<PhoneAccess.Nic>();
        foreach (var n in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (n.OperationalStatus != OperationalStatus.Up || n.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
            var props = n.GetIPProperties();
            var gw = props.GatewayAddresses.Any(g => !g.Address.Equals(IPAddress.Any) && g.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
            foreach (var u in props.UnicastAddresses)
                if (u.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && PhoneAccess.AllowedAddress(u.Address) && !IPAddress.IsLoopback(u.Address))
                    list.Add(new(u.Address, n.Name, gw, PhoneAccess.LooksVirtual(n.Description + " " + n.Name), n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211));
        }
        return list;
    }

    /// <summary>Eigene Adressen im Heimnetz und (falls Tailscale läuft) im Tailscale-Netz.</summary>
    public IReadOnlyList<IPAddress> PhoneAddresses() => PhoneNics().Select(n => n.Ip).Distinct().ToList();

    public IPAddress? TailscaleAddress() => PhoneAddresses().FirstOrDefault(PhoneAccess.IsTailscale);
    static string TailscaleExe => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Tailscale", "tailscale.exe");
    bool TailscaleInstalled() => TailscaleInstalledOverride?.Invoke() ?? File.Exists(TailscaleExe);

    public async Task<bool> EnsurePhoneServerAsync()
    {
        // Neue Adresse dazugekommen (z. B. Tailscale gerade angemeldet oder anderes WLAN)? Dann neu starten, damit das Handy sie erreicht.
        if (_phoneServer is { } running)
        {
            if (!PhoneAddresses().Except(running.Addresses).Any()) return true;
            _phoneServer = null;
            await running.DisposeAsync();
        }
        try
        {
            _phoneRemote ??= new PhoneRemoteBridge(this);
            _phoneServer = await PhoneLinkServer.StartAsync(PhoneAccess, PhoneAddresses(), PhoneAccess.DefaultPort, PhoneInbox, PhoneOutbox, _core.Audit,
                (dev, file) => Dispatcher.UIThread.Post(() => Toast($"Vom Handy ({dev}): {Path.GetFileName(file)}", ("Ordner öffnen", new Action(() => Launch(PhoneInbox))))),
                (dev, text) => Dispatcher.UIThread.Post(() =>
                {
                    if (text == "\u0001verbunden") { Toast($"✓ {dev} ist jetzt verbunden."); if (CurrentView == "handy") Show("handy"); return; }
                    _phoneTexts.Insert(0, (dev, text)); if (_phoneTexts.Count > 10) _phoneTexts.RemoveAt(10);
                    Toast($"Vom Handy ({dev}): {(text.Length > 80 ? text[..80] + " …" : text)}", ("Kopieren", new Action(() => { if (Clipboard is { } cb) _ = cb.SetTextAsync(text); })));
                    if (CurrentView == "handy") Show("handy");
                }), _phoneRemote);
            _phoneServerError = "";
            if (!_phoneCloseHooked) { _phoneCloseHooked = true; Closed += async (_, _) => { _phoneTimer?.Stop(); if (_phoneServer is { } s) await s.DisposeAsync(); }; }
            return true;
        }
        catch (Exception e) when (e is InvalidOperationException or IOException or System.Net.Sockets.SocketException)
        {
            _phoneServerError = e is IOException or System.Net.Sockets.SocketException ? "Der Handy-Zugang konnte nicht starten (Anschluss belegt?). Bitte Trendo neu starten." : e.Message;
            return false;
        }
    }
    bool _phoneCloseHooked;

    readonly List<(string Device, string Text)> _phoneTexts = [];

    /// <summary>Neuen QR-Code zeigen (startet den Handy-Zugang, falls nötig). Ohne Wahl gilt „zuhause im WLAN“.</summary>
    public async Task ShowPhoneQrAsync()
    {
        _phoneWhere ??= "home";
        if (!await EnsurePhoneServerAsync()) { Show("handy"); return; }
        PhoneAccess.NewOffer();
        _phoneQrAt = DateTime.UtcNow.AddSeconds(-1); _phoneNewDevice = null; _phoneRemoteAsked = false; _phoneMsg = "";
        _core.Audit.Append(Principal.LocalOwner.Name, "phonelink.offer", "QR-Code angezeigt");
        if (_phoneSsid is null && !_opt.Isolated) _ = Task.Run(() => { _phoneSsid = WlanName() ?? ""; Dispatcher.UIThread.Post(() => { if (CurrentView == "handy") Show("handy"); }); });
        Show("handy");
    }

    /// <summary>Name des WLANs, in dem der PC gerade ist (nur lesen).</summary>
    static string? WlanName()
    {
        if (!OperatingSystem.IsWindows()) return null;
        var r = Cli.Run("netsh", ["wlan", "show", "interfaces"], timeoutMs: 4000);
        return r.Out.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.StartsWith("SSID", StringComparison.Ordinal) && l.Contains(':'))?.Split(':', 2)[1].Trim() is { Length: > 0 } s ? s : null;
    }

    /// <summary>Adresse für den QR-Code: unterwegs Tailscale, zuhause die echte WLAN-/LAN-Karte (bei „Andere Adresse probieren“ die nächste).</summary>
    public IPAddress? PhoneQrHost()
    {
        if (_phoneWhere == "away" && TailscaleAddress() is { } ts) return ts;
        var home = PhoneAccess.HomeAddresses(PhoneNics());
        if (home.Count > 0) return home[_phoneAddrIx % home.Count];
        return TailscaleAddress() ?? _phoneServer?.Addresses.FirstOrDefault();
    }

    /// <summary>Welcher Schritt ist dran? Wird bei jedem Aufbau neu geprüft – nichts wird nur „angenommen“.</summary>
    public string PhoneStep()
    {
        if (_phoneWhere is null) return "where";
        if (_phoneWhere == "away")
        {
            if (!TailscaleInstalled()) return "ts-install";
            if (TailscaleAddress() is null) return "ts-login";
            if (!_phoneTsPhoneDone) return "ts-phone";
        }
        if (_phoneNewDevice is null && _phoneQrAt != default)
            _phoneNewDevice = PhoneAccess.Devices().Where(d => d.Paired >= _phoneQrAt).OrderByDescending(d => d.Paired).FirstOrDefault()?.Id;
        if (_phoneNewDevice is not null) return "done";
        if (PhoneAccess.CurrentOffer() is null || _phoneServer is null) return "qr-start";
        return PhoneAccess.LastReach is { } r && r.At >= _phoneQrAt ? "confirm" : "scan";
    }

    void PhoneWatch()
    {
        // Prüft jede Sekunde, ob der Schritt geklappt hat (Handy hat den PC erreicht, verbunden, Tailscale angemeldet …)
        if (_phoneTimer is null)
        {
            _phoneTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _phoneTimer.Tick += (_, _) =>
            {
                if (CurrentView != "handy") { _phoneTimer!.Stop(); return; }
                if (PhoneSignature() != _phoneSig) Show("handy");
            };
        }
        if (!_phoneTimer.IsEnabled) _phoneTimer.Start();
    }

    string PhoneSignature()
    {
        var step = PhoneStep();
        var slow = step == "scan" && DateTime.UtcNow - _phoneQrAt > TimeSpan.FromSeconds(30);
        return $"{step}|{slow}|{PhoneAccess.Devices().Count}|{TailscaleAddress()}|{_phoneSsid}";
    }

    static readonly string[] HomeSteps = ["Wo?", "Code scannen", "Verbinden", "Fertig"];
    static readonly string[] AwaySteps = ["Wo?", "Tailscale am PC", "Tailscale am Handy", "Code scannen", "Verbinden", "Fertig"];

    static int StepIndex(string step, bool away) => step switch
    {
        "where" => 0,
        "ts-install" or "ts-login" => 1,
        "ts-phone" => 2,
        "qr-start" or "scan" => away ? 3 : 1,
        "confirm" => away ? 4 : 2,
        _ => away ? 5 : 3,
    };

    Control PhoneStepper(string step)
    {
        var away = _phoneWhere == "away";
        var names = away ? AwaySteps : HomeSteps;
        var cur = StepIndex(step, away);
        var row = new WrapPanel { Name = "phone-steps" };
        for (var i = 0; i < names.Length; i++)
        {
            var done = i < cur || step == "done";
            var on = i == cur && step != "done";
            row.Children.Add(new Border
            {
                Margin = new Thickness(0, 0, 6, 6), Padding = new Thickness(10, 4), CornerRadius = new CornerRadius(14),
                Background = done ? Brush.Parse("#2216A34A") : on ? Brush.Parse("#333B82F6") : Brush.Parse("#14FFFFFF"),
                BorderBrush = done ? Brush.Parse("#664ADE80") : on ? Brush.Parse("#AAF5C451") : Brush.Parse("#22FFFFFF"), BorderThickness = new Thickness(1),
                Child = new TextBlock { Text = $"{(done ? "✓" : (i + 1).ToString())}  {names[i]}", Foreground = done ? Brush.Parse("#4ADE80") : on ? Text : Muted, FontSize = 12.5, FontWeight = on ? FontWeight.SemiBold : FontWeight.Normal },
            });
        }
        return row;
    }

    Button BigButton(string name, string text, Func<Task> click, bool primary = true)
    {
        var b = new Button { Name = name, Content = text, FontSize = 15, Padding = new Thickness(18, 10), Margin = new Thickness(0, 0, 8, 8), CornerRadius = new CornerRadius(10),
            Background = primary ? Accent : Brushes.Transparent, Foreground = primary ? Brushes.White : Text };
        b.Click += async (_, _) => { await click(); };
        return b;
    }

    static TextBlock Ok(string t) => new() { Text = "✓ " + t, Foreground = Brush.Parse("#4ADE80"), FontSize = 13, TextWrapping = TextWrapping.Wrap };

    private Control PhoneEasyCard()
    {
        var sp = new StackPanel { Name = "phone-easy", Spacing = 8 };
        sp.Children.Add(H("Handy verbinden – Schritt für Schritt", 17));
        sp.Children.Add(P("Für jedes Handy (Android und iPhone), ohne App aus dem Store und ohne Technikwissen. Jeder Schritt wird geprüft – erst wenn er geklappt hat, geht es weiter. Danach schickst du Fotos und Dateien hin und her und kannst Trendo vom Handy aus bedienen.", Text, 13));
        if (_phoneServerError.Length > 0) sp.Children.Add(P(_phoneServerError, Brush.Parse("#FACC15"), 13));
        var step = PhoneStep();
        _phoneSig = PhoneSignature();
        sp.Children.Add(PhoneStepper(step));
        var body = new StackPanel { Name = "phone-step-" + step, Spacing = 8 };
        switch (step)
        {
            case "where":
                body.Children.Add(H("Wo möchtest du das Handy nutzen?", 15));
                body.Children.Add(new WrapPanel { Children =
                {
                    BigButton("phone-where-home", "🏠  Zuhause (im selben WLAN)", async () => { _phoneWhere = "home"; await ShowPhoneQrAsync(); }),
                    BigButton("phone-where-away", "🌍  Überall – auch unterwegs", () => { _phoneWhere = "away"; _phoneTsPhoneDone = false; Show("handy"); return Task.CompletedTask; }, false),
                } });
                body.Children.Add(P("Zuhause ist am schnellsten eingerichtet. „Überall“ nutzt Tailscale – ein kostenloses, verschlüsseltes Privatnetz nur für deine Geräte. Trendo richtet es mit dir zusammen ein.", null, 12.5));
                break;

            case "ts-install":
                body.Children.Add(H("Tailscale auf diesem PC einrichten", 15));
                body.Children.Add(P("Trendo lädt das offizielle Tailscale-Programm von tailscale.com und startet die Installation. Windows fragt einmal nach – mit „Ja“ bestätigen. Kostenlos für Privatnutzer, kein Port im Router, nichts wird öffentlich.", Text, 13));
                body.Children.Add(new WrapPanel { Children =
                {
                    BigButton("phone-ts-install", "Tailscale laden & installieren", async () => { await TailscaleInstallAsync(); Show("handy"); }),
                    BigButton("phone-ts-recheck", "Neu prüfen", () => { Show("handy"); return Task.CompletedTask; }, false),
                } });
                break;

            case "ts-login":
                body.Children.Add(Ok("Tailscale ist auf dem PC installiert."));
                body.Children.Add(H("Bei Tailscale anmelden", 15));
                body.Children.Add(P("Im Browser öffnet sich die Anmeldung. Melde dich z. B. mit deinem Google-, Microsoft- oder Apple-Konto an. Merke dir, welches Konto du nimmst – am Handy brauchst du dasselbe. Trendo merkt automatisch, wenn es geklappt hat.", Text, 13));
                body.Children.Add(BigButton("phone-ts-login", "Jetzt anmelden", () => { TailscaleLogin(); return Task.CompletedTask; }));
                break;

            case "ts-phone":
                body.Children.Add(Ok($"Tailscale läuft auf dem PC ({TailscaleAddress()})."));
                body.Children.Add(H("Tailscale aufs Handy holen", 15));
                const string tsApp = "https://tailscale.com/download";
                body.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 22, Children =
                {
                    new Border { Name = "phone-ts-qr", Background = Brushes.White, Padding = new Thickness(12), CornerRadius = new CornerRadius(12), Child = QrView(tsApp) },
                    new StackPanel { Spacing = 6, VerticalAlignment = VerticalAlignment.Center, Children =
                    {
                        new TextBlock { Text = "1. Diesen Code mit der Handy-Kamera scannen", Foreground = Text, FontSize = 15, FontWeight = FontWeight.SemiBold },
                        new TextBlock { Text = "2. Tailscale-App installieren und öffnen", Foreground = Text, FontSize = 15, FontWeight = FontWeight.SemiBold },
                        new TextBlock { Text = "3. Mit demselben Konto wie am PC anmelden", Foreground = Text, FontSize = 15, FontWeight = FontWeight.SemiBold },
                        new TextBlock { Text = "4. In der App den Schalter auf „Verbunden“", Foreground = Text, FontSize = 15, FontWeight = FontWeight.SemiBold },
                    } },
                } });
                body.Children.Add(BigButton("phone-ts-phone-done", "Erledigt – weiter", async () => { _phoneTsPhoneDone = true; await ShowPhoneQrAsync(); }));
                body.Children.Add(P("Ob es wirklich geklappt hat, prüft der nächste Schritt: Dein Handy muss den PC über Tailscale erreichen.", null, 12));
                break;

            case "qr-start":
                body.Children.Add(P(PhoneAccess.LastReach is null ? "Gleich erscheint dein persönlicher Code." : "Der letzte Code ist abgelaufen (10 Minuten).", Text, 13));
                body.Children.Add(BigButton("phone-easy-start", "QR-Code zum Verbinden zeigen", ShowPhoneQrAsync));
                break;

            case "scan":
            case "confirm":
                PhoneScanStep(body, step);
                break;

            default:
                PhoneDoneStep(body);
                break;
        }
        if (_phoneMsg.Length > 0) body.Children.Add(P(_phoneMsg, Brush.Parse("#FACC15"), 12.5));
        if (step != "where")
        {
            var back = new Button { Name = "phone-wizard-restart", Content = step == "done" ? "Weiteres Handy verbinden" : "Von vorn", Padding = new Thickness(10, 4), Background = Brushes.Transparent };
            back.Click += (_, _) => { _phoneWhere = null; _phoneNewDevice = null; _phoneQrAt = default; _phoneMsg = ""; _phoneAddrIx = 0; Show("handy"); };
            body.Children.Add(back);
        }
        sp.Children.Add(WCard(body));

        PhoneDevices(sp);
        if (_phoneTexts.Count > 0)
        {
            sp.Children.Add(H("Zuletzt vom Handy", 14));
            foreach (var (dev, text) in _phoneTexts.Take(5)) sp.Children.Add(P($"{dev}: {text}", Text, 12.5));
        }
        sp.Children.Add(PhoneAwayCard());
        if (step != "done" && step != "where") PhoneWatch();
        return WCard(sp);
    }

    void PhoneScanStep(StackPanel body, string step)
    {
        var host = PhoneQrHost();
        var offer = PhoneAccess.CurrentOffer();
        if (host is null || offer is null || _phoneServer is null) return;
        var away = PhoneAccess.IsTailscale(host);
        body.Children.Add(Ok(away ? $"PC ist bereit – über Tailscale ({host})." : $"PC ist bereit – im Heimnetz{(_phoneSsid is { Length: > 0 } s ? $" „{s}“" : "")} ({host})."));
        var url = PhoneAccess.PairUrl(host, _phoneServer.Port, offer.Secret);
        if (step == "confirm")
        {
            body.Children.Add(Ok("Dein Handy hat den PC erreicht."));
            body.Children.Add(H("Jetzt auf dem Handy „Verbinden“ tippen", 16));
            body.Children.Add(P("Trendo wartet … sobald du getippt hast, geht es hier automatisch weiter.", Text, 13));
            return;
        }
        var qr = new Border { Name = "phone-easy-qr", Background = Brushes.White, Padding = new Thickness(14), CornerRadius = new CornerRadius(12), HorizontalAlignment = HorizontalAlignment.Left, Child = QrView(url) };
        var steps = new StackPanel { Spacing = 6, VerticalAlignment = VerticalAlignment.Center, Children =
        {
            new TextBlock { Text = "1. Handy-Kamera öffnen", Foreground = Text, FontSize = 16, FontWeight = FontWeight.SemiBold },
            new TextBlock { Text = "2. Auf diesen Code richten und den Link antippen", Foreground = Text, FontSize = 16, FontWeight = FontWeight.SemiBold },
            new TextBlock { Text = "3. Trendo wartet, bis dein Handy da ist …", Foreground = Muted, FontSize = 14 },
            P(away ? "Am Handy muss Tailscale an sein." : $"Das Handy muss im selben WLAN sein wie der PC{(_phoneSsid is { Length: > 0 } w ? $" („{w}“)" : "")} – mobile Daten dürfen an bleiben.", null, 12.5),
            new TextBlock { Name = "phone-easy-url", Text = url, Foreground = Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap },
        } };
        body.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 22, Children = { qr, steps } });

        // Hilfe, wenn das Handy nach 30 Sekunden noch nicht angekommen ist – mit Knöpfen statt Technik
        if (DateTime.UtcNow - _phoneQrAt > TimeSpan.FromSeconds(30))
        {
            var help = new StackPanel { Name = "phone-help", Spacing = 6 };
            help.Children.Add(H("Dein Handy ist noch nicht angekommen – das hilft meistens:", 14));
            help.Children.Add(P(away ? "• Am Handy die Tailscale-App öffnen: steht der Schalter auf „Verbunden“, mit demselben Konto wie am PC?"
                : $"• Ist das Handy im WLAN{(_phoneSsid is { Length: > 0 } w2 ? $" „{w2}“" : "")}? (Einstellungen → WLAN). Gäste-WLANs trennen Geräte oft voneinander.", Text, 12.5));
            help.Children.Add(P("• Windows blockt den Zugang oft, wenn das WLAN als „Öffentlich“ gilt. Mit dem Knopf erlaubst du ihn – nur für deine Heimnetz-Geräte und Tailscale, nur für diesen einen Anschluss.", Text, 12.5));
            var row = new WrapPanel();
            row.Children.Add(BigButton("phone-firewall", "Windows-Freigabe erlauben", async () => { await PhoneFirewallAsync(); Show("handy"); }));
            if (!away && PhoneAccess.HomeAddresses(PhoneNics()).Count > 1)
                row.Children.Add(BigButton("phone-next-address", "Andere Adresse probieren", () => { _phoneAddrIx++; _phoneQrAt = DateTime.UtcNow; Show("handy"); return Task.CompletedTask; }, false));
            if (!away) row.Children.Add(BigButton("phone-try-away", "Über Tailscale verbinden", () => { _phoneWhere = "away"; _phoneTsPhoneDone = false; Show("handy"); return Task.CompletedTask; }, false));
            row.Children.Add(BigButton("phone-new-code", "Neuen Code zeigen", ShowPhoneQrAsync, false));
            help.Children.Add(row);
            body.Children.Add(new Border { Background = Brush.Parse("#1AFACC15"), BorderBrush = Brush.Parse("#55FACC15"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 8), Child = help });
        }
    }

    void PhoneDoneStep(StackPanel body)
    {
        var dev = PhoneAccess.Devices().FirstOrDefault(d => d.Id == _phoneNewDevice);
        body.Children.Add(Ok($"{dev?.Name ?? "Dein Handy"} ist verbunden."));
        body.Children.Add(H("Fertig! 🎉", 17));
        if (dev is not null && !_phoneRemoteAsked && !dev.Remote)
        {
            body.Children.Add(H($"Darf „{dev.Name}“ Trendo komplett bedienen?", 15));
            body.Children.Add(P("Dann siehst du auf dem Handy den Trendo-Bildschirm und bedienst alles mit dem Finger – Programme, Studios, KI, Dateien. Nur dieses Handy, jederzeit hier wieder abschaltbar.", Text, 13));
            var id = dev.Id; var nm = dev.Name;
            body.Children.Add(new WrapPanel { Children =
            {
                BigButton("phone-remote-yes", "Ja, erlauben", () => { PhoneSetRemote(id, nm, true); _phoneRemoteAsked = true; Show("handy"); return Task.CompletedTask; }),
                BigButton("phone-remote-no", "Nein, nur Dateien & Texte", () => { _phoneRemoteAsked = true; Show("handy"); return Task.CompletedTask; }, false),
            } });
            return;
        }
        body.Children.Add(P(dev?.Remote == true
            ? "Auf dem Handy „Trendo öffnen“ antippen – dann bedienst du Trendo mit dem Finger. Fotos und Dateien schickst du über die Handy-Seite; sie landen im Ordner „Vom Handy“."
            : "Auf dem Handy kannst du jetzt Fotos, Dateien, Texte und Links an den PC schicken; sie landen im Ordner „Vom Handy“.", Text, 13));
        body.Children.Add(P("Tipp: Auf dem Handy im Browser-Menü „Zum Startbildschirm hinzufügen“ – dann ist Trendo wie eine App da.", null, 12.5));
        if (_phoneWhere == "home")
            body.Children.Add(P(TailscaleAddress() is not null ? "Für unterwegs: auf der Handy-Seite einmal „Unterwegs-Zugang einrichten“ tippen, solange du zuhause bist."
                : "Auch unterwegs nutzen? Unten bei „Auch unterwegs“ einrichten.", null, 12.5));
    }

    void PhoneSetRemote(string id, string name, bool on)
    {
        PhoneAccess.SetRemote(id, on);
        _core.Audit.Append(Principal.LocalOwner.Name, on ? "phonelink.remote.allow" : "phonelink.remote.deny", name);
        Toast(on ? $"„{name}“ darf Trendo jetzt bedienen." : $"„{name}“ darf Trendo nicht mehr bedienen.");
    }

    void PhoneDevices(StackPanel sp)
    {
        var devices = PhoneAccess.Devices();
        if (devices.Count == 0) return;
        sp.Children.Add(H($"Verbundene Handys ({devices.Count})", 14));
        foreach (var d in devices)
        {
            var id = d.Id; var nm = d.Name;
            var rm = new Button { Name = "phone-easy-remove-" + d.Id, Content = "Entfernen", Padding = new Thickness(10, 4) };
            rm.Click += (_, _) => { PhoneAccess.Remove(id); _core.Audit.Append(Principal.LocalOwner.Name, "phonelink.removed", nm); Toast($"{nm} ist nicht mehr verbunden."); Show("handy"); };
            var on = d.Remote;
            var remote = new Button { Name = "phone-easy-remote-" + d.Id, Content = on ? "Bedienen sperren" : "Trendo bedienen erlauben", Padding = new Thickness(10, 4), Background = on ? Brushes.Transparent : Accent };
            remote.Click += (_, _) => { PhoneSetRemote(id, nm, !on); Show("handy"); };
            sp.Children.Add(WCard(new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), Children = {
                new StackPanel { Children = { new TextBlock { Text = "📱 " + d.Name, Foreground = Text, FontSize = 14, FontWeight = FontWeight.SemiBold },
                    P($"Verbunden seit {d.Paired.ToLocalTime():dd.MM.yyyy} · zuletzt gesehen {d.LastSeen.ToLocalTime():dd.MM. HH:mm}{(on ? " · darf Trendo bedienen" : "")}", null, 12) } },
                Pos(Sp(remote), 1), Pos(Sp(rm), 2) } }));
        }
        var inbox = new Button { Name = "phone-easy-inbox", Content = "Ordner „Vom Handy“" }; inbox.Click += (_, _) => Launch(PhoneInbox);
        var outbox = new Button { Name = "phone-easy-outbox", Content = "Ordner „Zum Handy“ (hier hineinlegen)" }; outbox.Click += (_, _) => Launch(PhoneOutbox);
        sp.Children.Add(new WrapPanel { Children = { Sp(inbox), Sp(outbox) } });
    }

    Control PhoneAwayCard()
    {
        var ts = TailscaleAddress();
        var away = new StackPanel { Name = "phone-easy-away", Spacing = 6 };
        away.Children.Add(H("Auch unterwegs verbinden", 14));
        if (ts is not null)
            away.Children.Add(P($"✓ Bereit: Tailscale läuft auf dem PC ({ts}). Unterwegs erreicht dein Handy den PC, wenn dort Tailscale an ist.", Brush.Parse("#4ADE80"), 13));
        else
            away.Children.Add(P("Damit dein Handy den PC auch außerhalb des WLANs erreicht, nutzt Trendo Tailscale: ein privates, verschlüsseltes Netz nur für deine Geräte. Kostenlos für Privatnutzer, kein Port im Router, nichts ist öffentlich. Trendo führt dich Schritt für Schritt durch.", Text, 13));
        if (_phoneWhere != "away")
            away.Children.Add(new WrapPanel { Children = { BigButton("phone-easy-tailscale", ts is null ? "Unterwegs einrichten" : "Handy für unterwegs verbinden", () => { _phoneWhere = "away"; _phoneTsPhoneDone = false; _phoneNewDevice = null; Show("handy"); return Task.CompletedTask; }, false) } });
        return WCard(away);
    }

    /// <summary>Offizielles Tailscale-Setup laden und starten (Windows fragt nach Admin – das ist der Installer von Tailscale, nicht Trendo).</summary>
    async Task TailscaleInstallAsync()
    {
        if (_core.Offline) { _phoneMsg = "Trendo ist offline – zum Laden braucht es Internet."; return; }
        if (_depBusy) return;
        _depBusy = true; string? file = null;
        _core.Audit.Append(Principal.LocalOwner.Name, "phonelink.tailscale.install.start", "https://pkgs.tailscale.com/stable/tailscale-setup-latest.exe");
        BeginSetupOverlay("Tailscale wird eingerichtet");
        try
        {
            SetupOverlayProgress(0, 3, "Offizielles Tailscale-Setup wird geladen …");
            (file, _) = await KiDownload("https://pkgs.tailscale.com/stable/tailscale-setup-latest.exe", "tailscale-setup.exe", 0, 1, 3);
            SetupOverlayProgress(2, 3, "Installation läuft – bitte die Windows-Frage mit „Ja“ bestätigen …");
            if (KiInstallerRunOverride is { } run) await run(file);
            else
            {
                using var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(file) { UseShellExecute = true });
                if (p is not null) await p.WaitForExitAsync(SetupToken);
            }
            var ok = TailscaleInstalled();
            _core.Audit.Append(Principal.LocalOwner.Name, ok ? "phonelink.tailscale.install.ok" : "phonelink.tailscale.install.failed", "");
            _phoneMsg = ok ? "" : "Tailscale wurde nicht fertig installiert (abgebrochen?). Einfach noch einmal versuchen.";
            FinishSetupOverlay(ok ? "Tailscale ist installiert" : "Tailscale ist noch nicht installiert", ok ? "Weiter geht es mit der Anmeldung." : _phoneMsg, ok ? [] : [_phoneMsg]);
        }
        catch (OperationCanceledException) when (SetupCancelRequested) { _phoneMsg = "Abgebrochen."; }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            _phoneMsg = "Tailscale ließ sich nicht einrichten: " + ex.Message;
            FinishSetupOverlay("Einrichten nicht möglich", _phoneMsg, [ex.Message]);
        }
        finally { _depBusy = false; if (file is not null) TryDelete(file); }
    }

    /// <summary>Tailscale-Anmeldung: „tailscale up“ nennt eine Anmeldeseite – Trendo öffnet sie im Browser. Den Rest prüft der Assistent selbst.</summary>
    void TailscaleLogin()
    {
        _core.Audit.Append(Principal.LocalOwner.Name, "phonelink.tailscale.login", "");
        if (TailscaleLoginOverride is { } o) { o(); _phoneMsg = "Anmeldung geöffnet – bitte im Browser abschließen."; Show("handy"); return; }
        _phoneMsg = "Anmeldung wird geöffnet …"; Show("handy");
        _ = Task.Run(async () =>
        {
            string? url = null;
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo(TailscaleExe) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                psi.ArgumentList.Add("up");
                using var p = System.Diagnostics.Process.Start(psi)!;
                void Line(string? l)
                {
                    if (url is null && l is not null && Regex.Match(l, @"https://login\.tailscale\.com/\S+") is { Success: true } m)
                    {
                        url = m.Value;
                        Dispatcher.UIThread.Post(() => { Launch(url); _phoneMsg = "Anmeldung im Browser geöffnet – bitte dort abschließen. Trendo merkt es automatisch."; if (CurrentView == "handy") Show("handy"); });
                    }
                }
                p.OutputDataReceived += (_, e) => Line(e.Data); p.ErrorDataReceived += (_, e) => Line(e.Data);
                p.BeginOutputReadLine(); p.BeginErrorReadLine();
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
                await p.WaitForExitAsync(cts.Token);
            }
            catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or OperationCanceledException)
            {
                if (url is null) Dispatcher.UIThread.Post(() =>
                {
                    // Plan B: die Tailscale-App selbst öffnen, dort „Log in“
                    var gui = Path.Combine(Path.GetDirectoryName(TailscaleExe)!, "tailscale-ipn.exe");
                    if (File.Exists(gui)) Launch(gui);
                    _phoneMsg = "Bitte im Tailscale-Fenster (unten rechts beim Uhrsymbol) auf „Log in“ klicken.";
                    if (CurrentView == "handy") Show("handy");
                });
            }
        });
    }

    /// <summary>Windows-Freigabe für den Handy-Anschluss (eine Regel, Windows fragt nach Bestätigung). Nur auf Knopfdruck.</summary>
    async Task PhoneFirewallAsync()
    {
        var args = PhoneAccess.FirewallArgs(_phoneServer?.Port ?? PhoneAccess.DefaultPort);
        _core.Audit.Append(Principal.LocalOwner.Name, "phonelink.firewall", string.Join(' ', args));
        bool ok;
        if (PhoneFirewallOverride is { } o) ok = o(args);
        else
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo("netsh") { UseShellExecute = true, Verb = "runas", WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden };
                foreach (var a in args) psi.ArgumentList.Add(a);
                using var p = System.Diagnostics.Process.Start(psi);
                if (p is not null) await p.WaitForExitAsync();
                ok = p?.ExitCode == 0;
            }
            catch (System.ComponentModel.Win32Exception) { ok = false; } // Windows-Frage mit „Nein“ beantwortet
        }
        _phoneMsg = ok ? "✓ Windows-Freigabe ist eingerichtet. Jetzt den Code noch einmal scannen." : "Die Windows-Freigabe wurde nicht eingerichtet (Frage abgelehnt?).";
        _phoneQrAt = DateTime.UtcNow;
    }
}
