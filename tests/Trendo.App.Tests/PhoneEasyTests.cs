using System.Net;
using System.Net.Http.Headers;
using Avalonia.Controls;
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

    [AvaloniaFact(DisplayName = "Handy (UI): „QR-Code zum Verbinden zeigen“ zeigt Code und Schritte; Unterwegs-Karte erklärt Tailscale")]
    public async Task Ui_shows_qr()
    {
        var (w, _, _) = Ui.Open();
        w.PhoneAddressesOverride = () => [IPAddress.Loopback];
        w.Show("handy");
        Assert.NotNull(w.Find<Button>("phone-easy-start"));
        Assert.NotNull(w.TryFind<Control>("phone-easy-away"));
        await w.ShowPhoneQrAsync();
        Assert.NotNull(w.TryFind<Border>("phone-easy-qr"));
        Assert.Contains("/p/", w.Find<TextBlock>("phone-easy-url").Text);
        if (w.PhoneServer is { } s) await s.DisposeAsync();
        w.Close();
    }
}
