using System.Net;
using System.Net.Http.Headers;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Handy verbinden – ganz einfach: echter Durchlauf mit nachgebildetem Handy (HTTP gegen den Handy-Zugang auf 127.0.0.1).</summary>
public class PhoneEasyTests
{
    static string Temp() { var d = Path.Combine(Path.GetTempPath(), "trendo-phonesrv-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

    [Fact(DisplayName = "Handy: QR öffnen → verbinden → Foto senden → Text senden → Datei vom PC laden; Fremde und Ungekoppelte abgewiesen")]
    public async Task Full_flow()
    {
        var d = Temp(); var ct = TestContext.Current.CancellationToken;
        var access = new PhoneAccess(d);
        var files = new List<string>(); var texts = new List<string>();
        var outbox = Path.Combine(d, "zum"); Directory.CreateDirectory(outbox); File.WriteAllText(Path.Combine(outbox, "brief.pdf"), "PDF");
        await using var srv = await PhoneLinkServer.StartAsync(access, [IPAddress.Loopback], 0 + FreePort(), Path.Combine(d, "vom"), outbox, new AuditLog(Path.Combine(d, "audit.jsonl")),
            (_, f) => files.Add(f), (_, t) => texts.Add(t));
        var baseUrl = $"http://127.0.0.1:{srv.Port}";
        var jar = new CookieContainer();
        using var phone = new HttpClient(new HttpClientHandler { CookieContainer = jar, AllowAutoRedirect = true }) { BaseAddress = new Uri(baseUrl) };

        Assert.Equal(HttpStatusCode.Unauthorized, (await phone.GetAsync("/", ct)).StatusCode);
        var offer = access.NewOffer();
        var page = await phone.GetStringAsync($"/p/{offer.Secret}", ct);
        Assert.Contains("Verbinden", page);
        var pair = await phone.PostAsync($"/p/{offer.Secret}", new FormUrlEncodedContent(new Dictionary<string, string> { ["name"] = "Testhandy" }), ct);
        Assert.Equal(HttpStatusCode.OK, pair.StatusCode);
        Assert.Contains("Verbunden mit", await pair.Content.ReadAsStringAsync(ct));
        Assert.Contains("\u0001verbunden", texts);
        Assert.Single(access.Devices());

        using (var form = new MultipartFormDataContent())
        {
            var img = new ByteArrayContent([0xFF, 0xD8, 0xFF]); img.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            form.Add(img, "f", "../../urlaub.jpg"); // Pfadteile werden entfernt
            Assert.Equal(HttpStatusCode.OK, (await phone.PostAsync("/api/upload", form, ct)).StatusCode);
        }
        Assert.True(File.Exists(Path.Combine(d, "vom", "urlaub.jpg")));
        Assert.Single(files);
        Assert.Equal(HttpStatusCode.OK, (await phone.PostAsync("/api/text", new FormUrlEncodedContent(new Dictionary<string, string> { ["text"] = "https://example.org" }), ct)).StatusCode);
        Assert.Contains("https://example.org", texts);
        Assert.Contains("brief.pdf", await phone.GetStringAsync("/api/files", ct));
        Assert.Equal("PDF", await phone.GetStringAsync("/f/brief.pdf", ct));
        Assert.Equal(HttpStatusCode.NotFound, (await phone.GetAsync("/f/..%2Faudit.jsonl", ct)).StatusCode);

        // Zweites Einlösen geht nicht, fremder Host-Kopf (DNS-Rebinding) und Fremde ohne Schlüssel werden abgewiesen
        using var stranger = new HttpClient { BaseAddress = new Uri(baseUrl) };
        Assert.Equal(HttpStatusCode.Gone, (await stranger.PostAsync($"/p/{offer.Secret}", new FormUrlEncodedContent(new Dictionary<string, string> { ["name"] = "x" }), ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await stranger.PostAsync("/api/text", new FormUrlEncodedContent(new Dictionary<string, string> { ["text"] = "x" }), ct)).StatusCode);
        var rebind = new HttpRequestMessage(HttpMethod.Get, "/"); rebind.Headers.Host = "evil.example";
        Assert.Equal((HttpStatusCode)421, (await stranger.SendAsync(rebind, ct)).StatusCode);

        // Entfernen sperrt das Handy sofort
        access.Remove(access.Devices()[0].Id);
        Assert.Equal(HttpStatusCode.Unauthorized, (await phone.GetAsync("/api/files", ct)).StatusCode);
        Directory.Delete(d, true);
    }

    static int FreePort() { var l = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0); l.Start(); var p = ((IPEndPoint)l.LocalEndpoint).Port; l.Stop(); return p; }

    sealed class FakeRemote : IPhoneRemote
    {
        public readonly List<string> Calls = [];
        public Task<byte[]?> ScreenAsync(int maxWidth) { Calls.Add("screen " + maxWidth); return Task.FromResult<byte[]?>([0x89, 0x50, 0x4E, 0x47]); }
        public Task<string> TapAsync(double fx, double fy) { Calls.Add(FormattableString.Invariant($"tap {fx:0.00} {fy:0.00}")); return Task.FromResult("Knopf"); }
        public Task TypeAsync(string text) { Calls.Add("type " + text); return Task.CompletedTask; }
        public Task KeyAsync(string key) { Calls.Add("key " + key); return Task.CompletedTask; }
        public Task ScrollAsync(double fx, double fy, double dy) { Calls.Add(FormattableString.Invariant($"scroll {dy:0.0}")); return Task.CompletedTask; }
        public void Started(string device) => Calls.Add("start " + device);
    }

    static FormUrlEncodedContent Form(params (string K, string V)[] kv) => new(kv.ToDictionary(x => x.K, x => x.V));

    [Fact(DisplayName = "Handy: Trendo bedienen nur mit Erlaubnis am PC – dann Bildschirm, Tippen, Schreiben, Tasten, Scrollen; Scan wird gemeldet; Unterwegs-Link meldet an")]
    public async Task Remote_and_transfer()
    {
        var d = Temp(); var ct = TestContext.Current.CancellationToken;
        var access = new PhoneAccess(d); var remote = new FakeRemote();
        await using var srv = await PhoneLinkServer.StartAsync(access, [IPAddress.Loopback], FreePort(), Path.Combine(d, "vom"), Path.Combine(d, "zum"), new AuditLog(Path.Combine(d, "audit.jsonl")),
            (_, _) => { }, (_, _) => { }, remote);
        var baseUrl = $"http://127.0.0.1:{srv.Port}";
        using var phone = new HttpClient(new HttpClientHandler { CookieContainer = new CookieContainer() }) { BaseAddress = new Uri(baseUrl) };
        var offer = access.NewOffer();
        Assert.Null(access.LastReach);
        await phone.GetStringAsync($"/p/{offer.Secret}", ct);
        Assert.NotNull(access.LastReach); // Assistent sieht: Handy hat den PC erreicht
        var home = await (await phone.PostAsync($"/p/{offer.Secret}", Form(("name", "Testhandy")), ct)).Content.ReadAsStringAsync(ct);
        Assert.Contains("Trendo bedienen erlauben", home); // noch nicht erlaubt

        Assert.Equal(HttpStatusCode.Forbidden, (await phone.GetAsync("/fern", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await phone.PostAsync("/api/tap", Form(("x", "0.5"), ("y", "0.5")), ct)).StatusCode);
        Assert.Empty(remote.Calls);

        var dev = Assert.Single(access.Devices());
        access.SetRemote(dev.Id, true);
        Assert.Contains("/fern", await phone.GetStringAsync("/", ct));
        Assert.Contains("Trendo-Bildschirm", await phone.GetStringAsync("/fern", ct));
        var png = await phone.GetAsync("/api/screen?w=800", ct);
        Assert.Equal("image/png", png.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Knopf", await (await phone.PostAsync("/api/tap", Form(("x", "0.25"), ("y", "0.75")), ct)).Content.ReadAsStringAsync(ct));
        Assert.Equal(HttpStatusCode.BadRequest, (await phone.PostAsync("/api/tap", Form(("x", "2"), ("y", "0.5")), ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await phone.PostAsync("/api/type", Form(("text", "Hallo")), ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await phone.PostAsync("/api/key", Form(("key", "enter")), ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await phone.PostAsync("/api/key", Form(("key", "alt+f4")), ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await phone.PostAsync("/api/scroll", Form(("x", "0.5"), ("y", "0.5"), ("dy", "9")), ct)).StatusCode);
        Assert.Equal(["start Testhandy", "screen 800", "tap 0.25 0.75", "type Hallo", "key enter", "scroll 3.0"], remote.Calls);

        // Ohne Tailscale-Adresse gibt es keinen Unterwegs-Link; der Übergabe-Link selbst meldet ein zweites Mal an
        Assert.Equal(HttpStatusCode.NotFound, (await phone.PostAsync("/api/away", null, ct)).StatusCode);
        using var other = new HttpClient(new HttpClientHandler { CookieContainer = new CookieContainer() }) { BaseAddress = new Uri(baseUrl) };
        var code = access.NewTransfer(dev.Id);
        Assert.Contains("Verbunden mit", await other.GetStringAsync($"/t/{code}", ct));
        Assert.Equal(HttpStatusCode.Gone, (await other.GetAsync($"/t/{code}", ct)).StatusCode);
        access.SetRemote(dev.Id, false);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync("/api/screen", ct)).StatusCode);
        Directory.Delete(d, true);
    }

    static async Task Until(MainWindow w, Func<bool> ok)
    {
        for (var i = 0; i < 200 && !ok(); i++) { await Task.Delay(25); Avalonia.Threading.Dispatcher.UIThread.RunJobs(); }
        Assert.True(ok());
    }

    [AvaloniaFact(DisplayName = "Handy (UI): Schritt für Schritt – Ort wählen → Code → Handy erreicht → verbunden → Bedienen erlauben; Hilfe nach 30 s mit Windows-Freigabe")]
    public async Task Wizard_home()
    {
        var (w, _, _) = Ui.Open();
        w.PhoneAddressesOverride = () => [IPAddress.Loopback];
        string[]? fw = null; w.PhoneFirewallOverride = a => { fw = a; return true; };
        w.Show("handy");
        Assert.NotNull(w.TryFind<Control>("phone-step-where"));
        Assert.NotNull(w.TryFind<Control>("phone-steps"));
        Assert.NotNull(w.TryFind<Control>("phone-easy-away"));
        w.Find<Button>("phone-where-home").Press();
        await Until(w, () => w.TryFind<Border>("phone-easy-qr") is not null);
        Assert.Contains("/p/", w.Find<TextBlock>("phone-easy-url").Text);
        Assert.Null(w.TryFind<Control>("phone-help"));

        // 30 Sekunden ohne Handy: Hilfe mit Knöpfen
        w.PhoneQrAt = DateTime.UtcNow.AddSeconds(-40); w.Show("handy");
        w.Find<Button>("phone-firewall").Press();
        await Until(w, () => fw is not null);
        Assert.Contains("localport=" + PhoneAccess.DefaultPort, fw!);

        // Handy öffnet den Code → „Verbinden tippen“; Handy tippt → Fertig mit Frage nach dem Bedienen
        w.PhoneAccess.MarkReached(IPAddress.Loopback); w.Show("handy");
        Assert.NotNull(w.TryFind<Control>("phone-step-confirm"));
        w.PhoneAccess.Redeem(w.PhoneAccess.CurrentOffer()!.Secret, "Testhandy"); w.Show("handy");
        Assert.NotNull(w.TryFind<Control>("phone-step-done"));
        w.Find<Button>("phone-remote-yes").Press();
        Assert.True(Assert.Single(w.PhoneAccess.Devices()).Remote);
        Assert.Contains(w.GetLogicalTexts(), t => t.Contains("Trendo öffnen"));
        Assert.NotNull(w.TryFind<Button>("phone-easy-remote-"));
        if (w.PhoneServer is { } s) await s.DisposeAsync();
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Handy (UI): Überall/Tailscale – installieren → anmelden → App aufs Handy; jeder Schritt erst, wenn der vorige geprüft ist")]
    public void Wizard_tailscale()
    {
        var (w, _, _) = Ui.Open();
        var installed = false; var login = 0; IReadOnlyList<IPAddress> addrs = [IPAddress.Loopback];
        w.TailscaleInstalledOverride = () => installed;
        w.TailscaleLoginOverride = () => login++;
        w.PhoneAddressesOverride = () => addrs;
        w.Show("handy");
        w.Find<Button>("phone-where-away").Press();
        Assert.NotNull(w.TryFind<Control>("phone-step-ts-install"));
        w.Find<Button>("phone-ts-recheck").Press();
        Assert.NotNull(w.TryFind<Control>("phone-step-ts-install")); // noch nicht installiert → bleibt
        installed = true; w.Show("handy");
        Assert.NotNull(w.TryFind<Control>("phone-step-ts-login"));
        w.Find<Button>("phone-ts-login").Press();
        Assert.Equal(1, login);
        addrs = [IPAddress.Loopback, IPAddress.Parse("100.101.102.103")]; w.Show("handy");
        Assert.NotNull(w.TryFind<Control>("phone-step-ts-phone"));
        Assert.NotNull(w.TryFind<Border>("phone-ts-qr"));
        Assert.Equal(IPAddress.Parse("100.101.102.103"), w.PhoneQrHost());
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Handy bedient Trendo: Bildschirmbild, Knopf antippen, Feld antippen und schreiben, Löschen")]
    public void Remote_drives_ui()
    {
        var (w, _, _) = Ui.Open();
        w.TailscaleInstalledOverride = () => false;
        w.Show("handy");
        Assert.NotNull(w.PhoneScreen(400)); // ohne Grafikkarte (Testlauf) ist das Bild leer, in Trendo ein PNG
        Point Center(Control c)
        {
            Avalonia.Threading.Dispatcher.UIThread.RunJobs(); w.UpdateLayout();
            c.BringIntoView(); w.UpdateLayout();
            Avalonia.Headless.HeadlessWindowExtensions.CaptureRenderedFrame(w); // Trefferprüfung nutzt die zuletzt gezeichnete Szene
            return c.TranslatePoint(new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), w) ?? throw new InvalidOperationException($"{c.Name}: nicht im Fenster (Wurzel {TopLevel.GetTopLevel(c)?.GetType().Name ?? "keine"})");
        }
        var p = Center(w.Find<Button>("phone-where-away"));
        Assert.Equal("Knopf", w.PhoneTap(p.X / w.Bounds.Width, p.Y / w.Bounds.Height)); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.True(w.TryFind<Control>("phone-step-ts-install") is not null, "getroffen: " + w.PhoneLastHit + " bei " + p);
        w.Show("oss"); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var box = w.GetVisualDescendants().OfType<TextBox>().First(t => t.IsEffectivelyVisible && t.IsEffectivelyEnabled && !t.IsReadOnly);
        var b = Center(box);
        Assert.Equal("Eingabefeld", w.PhoneTap(b.X / w.Bounds.Width, b.Y / w.Bounds.Height));
        w.PhoneType("Musik!"); w.PhoneKey("back");
        Assert.EndsWith("Musik", box.Text);
        w.PhoneScroll(0.5, 0.5, 1);
        w.Close();
    }
}
