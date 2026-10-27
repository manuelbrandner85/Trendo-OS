using System.Net;
using System.Net.NetworkInformation;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Trendo.Core;

namespace Trendo.App;

/// <summary>„Handy verbinden – ganz einfach“ (Vorgabe 27.10.2026): QR-Code mit der Handy-Kamera scannen, „Verbinden“ tippen, fertig.
/// Zuhause über das Heimnetz, unterwegs über Tailscale. Der alte Weg über die Entwickleroptionen bleibt als „Profi“ darunter.</summary>
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
    public PhoneLinkServer? PhoneServer => _phoneServer;

    /// <summary>Eigene Adressen im Heimnetz und (falls Tailscale läuft) im Tailscale-Netz.</summary>
    public IReadOnlyList<IPAddress> PhoneAddresses()
    {
        if (PhoneAddressesOverride is not null) return PhoneAddressesOverride();
        return NetworkInterface.GetAllNetworkInterfaces().Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .SelectMany(n => n.GetIPProperties().UnicastAddresses.Select(u => u.Address))
            .Where(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && PhoneAccess.AllowedAddress(a) && !IPAddress.IsLoopback(a)).Distinct().ToList();
    }

    public IPAddress? TailscaleAddress() => PhoneAddresses().FirstOrDefault(PhoneAccess.IsTailscale);
    static bool TailscaleInstalled() => File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Tailscale", "tailscale.exe"));

    public async Task<bool> EnsurePhoneServerAsync()
    {
        if (_phoneServer is not null) return true;
        try
        {
            _phoneServer = await PhoneLinkServer.StartAsync(PhoneAccess, PhoneAddresses(), PhoneAccess.DefaultPort, PhoneInbox, PhoneOutbox, _core.Audit,
                (dev, file) => Dispatcher.UIThread.Post(() => Toast($"Vom Handy ({dev}): {Path.GetFileName(file)}", ("Ordner öffnen", new Action(() => Launch(PhoneInbox))))),
                (dev, text) => Dispatcher.UIThread.Post(() =>
                {
                    if (text == "\u0001verbunden") { Toast($"✓ {dev} ist jetzt verbunden."); if (CurrentView == "handy") Show("handy"); return; }
                    _phoneTexts.Insert(0, (dev, text)); if (_phoneTexts.Count > 10) _phoneTexts.RemoveAt(10);
                    Toast($"Vom Handy ({dev}): {(text.Length > 80 ? text[..80] + " …" : text)}", ("Kopieren", new Action(() => { if (Clipboard is { } cb) _ = cb.SetTextAsync(text); })));
                    if (CurrentView == "handy") Show("handy");
                }));
            _phoneServerError = "";
            Closed += async (_, _) => { if (_phoneServer is { } s) await s.DisposeAsync(); };
            return true;
        }
        catch (Exception e) when (e is InvalidOperationException or IOException or System.Net.Sockets.SocketException)
        {
            _phoneServerError = e is IOException or System.Net.Sockets.SocketException ? "Der Handy-Zugang konnte nicht starten (Anschluss belegt?). Bitte Trendo neu starten." : e.Message;
            return false;
        }
    }

    readonly List<(string Device, string Text)> _phoneTexts = [];

    public async Task ShowPhoneQrAsync()
    {
        if (!await EnsurePhoneServerAsync()) { Show("handy"); return; }
        PhoneAccess.NewOffer();
        _core.Audit.Append(Principal.LocalOwner.Name, "phonelink.offer", "QR-Code angezeigt");
        Show("handy");
    }

    private Control PhoneEasyCard()
    {
        var sp = new StackPanel { Name = "phone-easy", Spacing = 8 };
        sp.Children.Add(H("Handy verbinden – ganz einfach", 17));
        sp.Children.Add(P("Funktioniert mit jedem Handy (Android und iPhone), ohne App und ohne Entwicklermodus. Danach schickst du Fotos, Dateien, Texte und Links zwischen Handy und PC hin und her.", Text, 13));
        if (_phoneServerError.Length > 0) sp.Children.Add(P(_phoneServerError, Brush.Parse("#FACC15"), 13));
        var offer = PhoneAccess.CurrentOffer();
        var host = TailscaleAddress() ?? _phoneServer?.Addresses.FirstOrDefault(a => !IPAddress.IsLoopback(a)) ?? _phoneServer?.Addresses.FirstOrDefault();
        if (offer is not null && _phoneServer is not null && host is not null)
        {
            var url = PhoneAccess.PairUrl(host, _phoneServer.Port, offer.Secret);
            var qr = new Border { Background = Brushes.White, Padding = new Thickness(14), CornerRadius = new CornerRadius(12), HorizontalAlignment = HorizontalAlignment.Left, Child = QrView(url) };
            qr.Name = "phone-easy-qr";
            var steps = new StackPanel { Spacing = 6, VerticalAlignment = VerticalAlignment.Center, Children =
            {
                new TextBlock { Text = "1. Handy-Kamera öffnen", Foreground = Text, FontSize = 16, FontWeight = FontWeight.SemiBold },
                new TextBlock { Text = "2. Auf diesen Code richten und den Link antippen", Foreground = Text, FontSize = 16, FontWeight = FontWeight.SemiBold },
                new TextBlock { Text = "3. „Verbinden“ tippen – fertig", Foreground = Text, FontSize = 16, FontWeight = FontWeight.SemiBold },
                P($"Der Code gilt 10 Minuten und nur einmal. Handy und PC müssen dafür im selben WLAN sein{(PhoneAccess.IsTailscale(host) ? " – oder beide in Tailscale (dann geht es auch unterwegs)" : "")}.", null, 12),
                P("Beim ersten Mal fragt Windows, ob Trendo im Netzwerk erreichbar sein darf: „Private Netzwerke“ erlauben.", null, 12),
                new TextBlock { Name = "phone-easy-url", Text = url, Foreground = Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap },
            } };
            sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 22, Children = { qr, steps } });
        }
        else
        {
            var go = new Button { Name = "phone-easy-start", Content = "QR-Code zum Verbinden zeigen", Background = Accent, Foreground = Brushes.White, Padding = new Thickness(18, 10), FontSize = 15 };
            go.Click += async (_, _) => await ShowPhoneQrAsync();
            sp.Children.Add(go);
        }

        var devices = PhoneAccess.Devices();
        if (devices.Count > 0)
        {
            sp.Children.Add(H($"Verbundene Handys ({devices.Count})", 14));
            foreach (var d in devices)
            {
                var rm = new Button { Name = "phone-easy-remove-" + d.Id, Content = "Entfernen", Padding = new Thickness(10, 4) };
                var id = d.Id; var nm = d.Name;
                rm.Click += (_, _) => { PhoneAccess.Remove(id); _core.Audit.Append(Principal.LocalOwner.Name, "phonelink.removed", nm); Toast($"{nm} ist nicht mehr verbunden."); Show("handy"); };
                sp.Children.Add(WCard(new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = {
                    new StackPanel { Children = { new TextBlock { Text = "📱 " + d.Name, Foreground = Text, FontSize = 14, FontWeight = FontWeight.SemiBold }, P($"Verbunden seit {d.Paired.ToLocalTime():dd.MM.yyyy} · zuletzt gesehen {d.LastSeen.ToLocalTime():dd.MM. HH:mm}", null, 12) } },
                    Pos(rm, 1) } }));
            }
            var inbox = new Button { Name = "phone-easy-inbox", Content = "Ordner „Vom Handy“" }; inbox.Click += (_, _) => Launch(PhoneInbox);
            var outbox = new Button { Name = "phone-easy-outbox", Content = "Ordner „Zum Handy“ (hier hineinlegen)" }; outbox.Click += (_, _) => Launch(PhoneOutbox);
            sp.Children.Add(new WrapPanel { Children = { Sp(inbox), Sp(outbox) } });
        }
        if (_phoneTexts.Count > 0)
        {
            sp.Children.Add(H("Zuletzt vom Handy", 14));
            foreach (var (dev, text) in _phoneTexts.Take(5)) sp.Children.Add(P($"{dev}: {text}", Text, 12.5));
        }

        // Unterwegs: Tailscale
        var ts = TailscaleAddress();
        var away = new StackPanel { Name = "phone-easy-away", Spacing = 6 };
        away.Children.Add(H("Auch unterwegs verbinden", 14));
        if (ts is not null)
            away.Children.Add(P($"✓ Bereit: Tailscale läuft ({ts}). Der QR-Code nutzt diese Adresse – dein Handy erreicht den PC damit zuhause und unterwegs, wenn auf dem Handy Tailscale an ist.", Brush.Parse("#4ADE80"), 13));
        else
        {
            away.Children.Add(P("Damit dein Handy den PC auch außerhalb des WLANs erreicht, nutzt Trendo Tailscale: ein privates, verschlüsseltes Netz nur für deine Geräte. Kostenlos für Privatnutzer, kein Port im Router, nichts ist öffentlich.", Text, 13));
            away.Children.Add(P(TailscaleInstalled() ? "Tailscale ist installiert, aber nicht angemeldet: Tailscale öffnen und anmelden." :
                "1. Tailscale auf dem PC installieren (offizielle Seite) und anmelden · 2. Tailscale-App aufs Handy, mit demselben Konto anmelden · 3. Hier „Neu prüfen“, dann QR-Code zeigen.", null, 12.5));
            var get = new Button { Name = "phone-easy-tailscale", Content = "Tailscale-Seite öffnen", IsEnabled = !_core.Offline };
            get.Click += (_, _) => { _core.Audit.Append(Principal.LocalOwner.Name, "phonelink.tailscale.link", "https://tailscale.com/download"); Launch("https://tailscale.com/download"); };
            var again = new Button { Name = "phone-easy-recheck", Content = "Neu prüfen" }; again.Click += (_, _) => Show("handy");
            away.Children.Add(new WrapPanel { Children = { Sp(get), Sp(again) } });
        }
        sp.Children.Add(WCard(away));
        return WCard(sp);
    }
}
