using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0767 (CORE-04/X-08): Datei senden in beide Richtungen, Entfernen sperrt sofort, „Lauf aufzeichnen“ –
/// gegen den echten Handy-Zugang (Kestrel) und einmal mit einem echten Browser (Chrome/Edge headless als „Handy“).</summary>
public class PhoneSendT0767Tests
{
    static string Temp() { var d = Path.Combine(Path.GetTempPath(), "trendo-t0767-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }
    static int FreePort() { var l = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0); l.Start(); var p = ((IPEndPoint)l.LocalEndpoint).Port; l.Stop(); return p; }
    static string Sha(byte[] b) => Convert.ToHexStringLower(SHA256.HashData(b));

    [Fact(DisplayName = "T-0767: Handy → PC mit Prüfsumme vom Handy, Größenlimit, PC → Handy nur für das gewählte Handy, Entfernen sperrt und wird protokolliert")]
    public async Task Server_flow_with_run_log()
    {
        var d = Temp(); var ct = TestContext.Current.CancellationToken;
        var access = new PhoneAccess(d); var run = new PhoneRunLog(); run.Start();
        var inbox = Path.Combine(d, "vom"); var outbox = Path.Combine(d, "zum");
        await using var srv = await PhoneLinkServer.StartAsync(access, [IPAddress.Loopback], FreePort(), inbox, outbox, new AuditLog(Path.Combine(d, "audit.jsonl")),
            (_, _) => { }, (_, _) => { }, null, run, maxFileBytes: 100_000);
        var baseUrl = new Uri($"http://127.0.0.1:{srv.Port}");
        async Task<HttpClient> Pair(string name)
        {
            var c = new HttpClient(new HttpClientHandler { CookieContainer = new CookieContainer() }) { BaseAddress = baseUrl };
            var o = access.NewOffer();
            var r = await c.PostAsync($"/p/{o.Secret}", new FormUrlEncodedContent(new Dictionary<string, string> { ["name"] = name }), ct);
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            return c;
        }
        using var a = await Pair("Handy A");
        using var b = await Pair("Handy B");
        var devA = access.Devices().Single(x => x.Name == "Handy A");

        var photo = RandomNumberGenerator.GetBytes(50_000);
        MultipartFormDataContent Form(byte[] data, string name, string? sha)
        {
            var f = new MultipartFormDataContent();
            if (sha is not null) f.Add(new StringContent(sha), "sha");
            var c = new ByteArrayContent(data); c.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            f.Add(c, "f", name);
            return f;
        }
        var ok = await a.PostAsync("/api/upload", Form(photo, "CON.jpg", Sha(photo)), ct);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Contains(Sha(photo), await ok.Content.ReadAsStringAsync(ct));
        Assert.Equal(photo, File.ReadAllBytes(Path.Combine(inbox, "_CON.jpg")));
        Assert.Contains(run.Entries, e => e.Step == PhoneRunLog.Steps.PhoneToPc && e.Ok && e.Sha256 == Sha(photo) && e.Note.StartsWith("Prüfsumme gleich"));

        // falsche Prüfsumme vom Handy → im Protokoll als Fehler
        Assert.Equal(HttpStatusCode.OK, (await a.PostAsync("/api/upload", Form(photo, "b.jpg", new string('0', 64)), ct)).StatusCode);
        Assert.Contains(run.Entries, e => e.Step == PhoneRunLog.Steps.PhoneToPc && !e.Ok && e.Note.Contains("ABWEICHEND"));

        // zu groß → 413 (oder Verbindung beendet), keine Datei, keine Teil-Datei
        try { var big = await a.PostAsync("/api/upload", Form(new byte[150_000], "gross.bin", null), ct); Assert.Equal((HttpStatusCode)413, big.StatusCode); }
        catch (HttpRequestException) { }
        Assert.False(File.Exists(Path.Combine(inbox, "gross.bin")));
        Assert.DoesNotContain(Directory.GetFiles(inbox), f => f.EndsWith(".teil"));

        // PC → Handy A: nur A sieht und lädt die Datei, Prüfsumme der gesendeten Bytes
        var doc = Path.Combine(d, "Rechnung.pdf"); var docBytes = RandomNumberGenerator.GetBytes(70_000); File.WriteAllBytes(doc, docBytes);
        var offered = await PhoneTransfer.OfferAsync(doc, outbox, devA.Id);
        run.Record(PhoneRunLog.Steps.Provided, devA.Id, null, bytes: offered.Bytes, sha: offered.Sha256);
        Assert.Contains("Rechnung.pdf", await a.GetStringAsync("/api/files", ct));
        Assert.DoesNotContain("Rechnung.pdf", await b.GetStringAsync("/api/files", ct));
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync("/f/Rechnung.pdf", ct)).StatusCode);
        Assert.Equal(docBytes, await a.GetByteArrayAsync("/f/Rechnung.pdf", ct));
        await Task.Delay(100, ct);
        Assert.Contains(run.Entries, e => e.Step == PhoneRunLog.Steps.PcToPhone && e.Ok && e.Sha256 == Sha(docBytes));

        // Entfernen: sofort gesperrt, Bereitgestelltes gelöscht, Abweisung im Protokoll
        access.Remove(devA.Id); PhoneTransfer.DropDevice(outbox, devA.Id); run.Record(PhoneRunLog.Steps.Removed, devA.Id, null);
        Assert.Equal(HttpStatusCode.Unauthorized, (await a.GetAsync("/api/files", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await a.PostAsync("/api/upload", Form(photo, "nach.jpg", null), ct)).StatusCode);
        Assert.False(File.Exists(Path.Combine(inbox, "nach.jpg")));
        Assert.Equal(HttpStatusCode.OK, (await b.GetAsync("/api/files", ct)).StatusCode); // anderes Handy bleibt verbunden
        Assert.All(PhoneRunLog.Evaluate(run.Entries), c => Assert.True(c.Ok, c.Name + ": " + c.Detail));
        Assert.DoesNotContain(run.Entries, e => e.Device.Contains("Handy") || e.Network.Contains("127."));
        Directory.Delete(d, true);
    }

    [AvaloniaFact(DisplayName = "T-0767 (UI): Lauf aufzeichnen → „Datei senden …“ an ein Handy → Entfernen → Aufzeichnung speichern; Kabel-Weg ehrlich")]
    public async Task Ui_send_remove_record()
    {
        var (w, _, _) = Ui.Open();
        w.PhoneAddressesOverride = () => [IPAddress.Loopback];
        w.TailscaleInstalledOverride = () => false;
        w.AdbLocator = () => null; // kein adb → Kabel-Weg nicht verfügbar
        var src = Path.Combine(Path.GetTempPath(), "trendo-t0767-ui-" + Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(src, "Einkaufsliste");
        w.PickOverride = _ => Task.FromResult<string[]?>([src]);
        w.Show("handy");
        w.Find<Button>("phone-run-start").Press();
        Assert.True(w.PhoneRun.Active);
        Assert.NotNull(w.TryFind<Button>("phone-run-stop"));
        var o = w.PhoneAccess.NewOffer();
        var dev = w.PhoneAccess.Redeem(o.Secret, "Testhandy")!.Value.Device;
        w.Show("handy");
        Assert.NotNull(w.TryFind<Border>("phone-device-" + dev.Id));
        Assert.NotNull(w.TryFind<Border>("phone-device-" + dev.Id)!.ContextMenu);
        w.Find<Button>("phone-send-" + dev.Id).Press();
        for (var i = 0; i < 200 && !w.PhoneRun.Entries.Any(e => e.Step == PhoneRunLog.Steps.Provided); i++) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(20); }
        var offered = Assert.Single(PhoneTransfer.Visible(w.PhoneOutbox, dev.Id, DateTime.UtcNow), x => x.Personal);
        Assert.Equal(PhoneTransfer.Sha256File(src), PhoneTransfer.Sha256File(offered.File.FullName));
        for (var i = 0; i < 200 && w.TryFind<TextBlock>("phone-cable-status")?.Text?.Contains("geprüft") != false; i++) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(20); }
        Assert.Contains("nicht verfügbar", w.Find<TextBlock>("phone-cable-status").Text);

        w.Find<Button>("phone-easy-remove-" + dev.Id).Press();
        Assert.Empty(w.PhoneAccess.Devices());
        Assert.False(Directory.Exists(PhoneTransfer.DeviceFolder(w.PhoneOutbox, dev.Id)));
        w.Find<Button>("phone-run-stop").Press();
        Assert.False(w.PhoneRun.Active);
        var md = File.ReadAllText(w.PhoneRunReport!);
        Assert.Contains("PC → Handy bereitgestellt", md);
        Assert.Contains("| Entfernt |", md);
        Assert.DoesNotContain("Testhandy", md);
        Assert.NotNull(w.TryFind<Button>("phone-run-folder"));
        if (w.PhoneServer is { } s) await s.DisposeAsync();
        File.Delete(src);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "T-0767 (UI): Kabel-Weg mit dem echten adb dieses PCs – Anzeige „möglich“, „Handy suchen“ läuft echt (ohne Handy: leere Liste)")]
    public async Task Ui_cable_real_adb()
    {
        var adb = MobileBridge.FindAdb(File.Exists, Environment.GetEnvironmentVariable("LOCALAPPDATA"), Environment.GetEnvironmentVariable("PATH"))
            ?? new[] { Path.Combine(Environment.GetEnvironmentVariable("LOCALAPPDATA") ?? "", "TrendoWerkzeuge", "scrcpy", "adb.exe") }.FirstOrDefault(File.Exists);
        if (adb is null) { Assert.Skip("BLOCKIERT – adb liegt nicht auf diesem PC (Trendo installiert hier nichts)."); return; }
        var before = Process.GetProcessesByName("adb").Length;
        var (w, _, _) = Ui.Open();
        w.PhoneAddressesOverride = () => [IPAddress.Loopback];
        w.TailscaleInstalledOverride = () => false;
        w.AdbLocator = () => adb;
        try
        {
            w.Show("handy");
            for (var i = 0; i < 200 && w.TryFind<TextBlock>("phone-cable-status")?.Text?.Contains("geprüft") != false; i++) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(20); }
            Assert.Contains("Kabel-Weg möglich", w.Find<TextBlock>("phone-cable-status").Text);
            var (ok, err) = await w.LoadPhonesAsync(); // echtes „adb devices -l“
            Assert.True(ok, err);
        }
        finally
        {
            // eigenen adb-Server dieses Tests wieder beenden – nur, wenn vorher keiner lief
            if (before == 0) using (var k = Process.Start(new ProcessStartInfo(adb, "kill-server") { UseShellExecute = false, CreateNoWindow = true })) k?.WaitForExit(10_000);
            w.Close();
        }
    }

    /// <summary>Private Heimnetz-Adresse dieses PCs (echte Karte mit Router), wie sie der QR-Code zeigen würde.</summary>
    static IPAddress? LanAddress()
    {
        var nics = new List<PhoneAccess.Nic>();
        foreach (var n in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (n.OperationalStatus != OperationalStatus.Up || n.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
            var p = n.GetIPProperties();
            var gw = p.GatewayAddresses.Any(g => g.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any));
            foreach (var u in p.UnicastAddresses)
                if (u.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && PhoneAccess.AllowedAddress(u.Address) && !IPAddress.IsLoopback(u.Address))
                    nics.Add(new(u.Address, n.Name, gw, PhoneAccess.LooksVirtual(n.Description + " " + n.Name), n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211));
        }
        return PhoneAccess.HomeAddresses(nics).FirstOrDefault(a => !PhoneAccess.IsTailscale(a));
    }

    [Fact(DisplayName = "T-0767 Ende-zu-Ende mit echtem Browser (Chrome/Edge headless als Handy): QR-Link → Verbinden → Datei hoch (SHA-256 am Handy) → Datei runter → Entfernen sperrt")]
    public async Task Real_browser_end_to_end()
    {
        var browser = WebsitePreview.FindBrowser();
        if (browser is null) { Assert.Skip("BLOCKIERT – kein Chrome/Edge auf diesem PC."); return; }
        var ct = TestContext.Current.CancellationToken;
        var d = Temp();
        var beleg = Environment.GetEnvironmentVariable("TRENDO_T0767_BELEG") is { Length: > 0 } b0 ? b0 : Path.Combine(d, "beleg");
        Directory.CreateDirectory(beleg);
        var log = new StringBuilder();
        var sw = Stopwatch.StartNew();
        var lan = LanAddress();
        var host = lan ?? IPAddress.Loopback;
        void L(string s) => log.AppendLine($"[{sw.Elapsed.TotalSeconds,7:0.000} s] {s}");
        L($"Browser: {Path.GetFileName(browser)} (headless, eigenes Profil) · Adresse: {(lan is null ? "127.0.0.1 (kein Heimnetz gefunden)" : "Heimnetz-Adresse dieses PCs (<lan-ip>)")}");

        var access = new PhoneAccess(d); var run = new PhoneRunLog(); run.Start();
        var inbox = Path.Combine(d, "Vom Handy"); var outbox = Path.Combine(d, "Zum Handy");
        var port = FreePort();
        await using var srv = await PhoneLinkServer.StartAsync(access, lan is null ? [IPAddress.Loopback] : [IPAddress.Loopback, lan], port, inbox, outbox,
            new AuditLog(Path.Combine(d, "audit.jsonl")), (_, _) => { }, (_, _) => { }, null, run);
        var baseUrl = $"http://{host}:{port}";
        var offer = access.NewOffer();
        run.Record(PhoneRunLog.Steps.Offer, null, null, note: "Einmal-Code, gilt 10 Minuten");
        L("Handy-Zugang läuft (Kestrel), Einmal-Code erzeugt");

        var profile = Path.Combine(d, "chrome-profil"); var downloads = Path.Combine(d, "Handy-Downloads");
        Directory.CreateDirectory(profile); Directory.CreateDirectory(downloads);
        var dbg = FreePort();
        var psi = new ProcessStartInfo(browser) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
        foreach (var arg in new[] { "--headless=new", "--no-first-run", "--no-default-browser-check", "--disable-extensions", "--disable-background-networking", "--remote-allow-origins=*",
            "--remote-debugging-port=" + dbg, "--user-data-dir=" + profile, "--window-size=412,915", "about:blank" }) psi.ArgumentList.Add(arg);
        using var proc = Process.Start(psi)!;
        _ = proc.StandardOutput.ReadToEndAsync(ct); _ = proc.StandardError.ReadToEndAsync(ct);
        L("Browser gestartet (eigener Prozess, wird am Ende per PID beendet)");
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            string? pageWs = null, browserWs = null;
            for (var i = 0; i < 100 && (pageWs is null || browserWs is null); i++)
            {
                try
                {
                    using var ver = JsonDocument.Parse(await http.GetStringAsync($"http://127.0.0.1:{dbg}/json/version", ct));
                    browserWs = ver.RootElement.GetProperty("webSocketDebuggerUrl").GetString();
                    using var list = JsonDocument.Parse(await http.GetStringAsync($"http://127.0.0.1:{dbg}/json/list", ct));
                    pageWs = list.RootElement.EnumerateArray().Where(x => x.GetProperty("type").GetString() == "page").Select(x => x.GetProperty("webSocketDebuggerUrl").GetString()).FirstOrDefault();
                }
                catch (Exception e) when (e is HttpRequestException or TaskCanceledException) { }
                if (pageWs is null) await Task.Delay(150, ct);
            }
            Assert.NotNull(pageWs); Assert.NotNull(browserWs);
            await using var page = await Cdp.Open(pageWs!, ct);
            await using var brow = await Cdp.Open(browserWs!, ct);
            await brow.Call("Browser.setDownloadBehavior", new { behavior = "allow", downloadPath = downloads, eventsEnabled = false }, ct);
            await page.Call("Page.enable", new { }, ct);

            async Task<string> Eval(string js) => (await page.Call("Runtime.evaluate", new { expression = js, returnByValue = true, awaitPromise = true }, ct))
                .GetProperty("result").TryGetProperty("value", out var v) ? v.ToString() : "";
            async Task Wait(string js, string what, int seconds = 20)
            {
                for (var i = 0; i < seconds * 10; i++) { if (await Eval(js) is "True" or "true") return; await Task.Delay(100, ct); }
                Assert.Fail("Zeitüberschreitung: " + what + " · Seite: " + await Eval("document.body ? document.body.innerText.slice(0,300) : ''"));
            }

            // 1. QR-Code-Link öffnen (wie nach dem Scan mit der Kamera) und „Verbinden“ tippen
            await page.Call("Page.navigate", new { url = PhoneAccess.PairUrl(host, port, offer.Secret) }, ct);
            await Wait("document.readyState==='complete' && !!document.getElementById('n')", "Kopplungsseite");
            await Eval("document.getElementById('n').value='Browser-Handy'; document.querySelector('form').submit(); true");
            await Wait("document.readyState==='complete' && document.body.innerText.indexOf('Verbunden mit')>=0", "Startseite nach dem Verbinden");
            var dev = Assert.Single(access.Devices());
            Assert.Null(access.CurrentOffer()); // Einmal-Code verbraucht
            L("Kopplung: Seite „Verbunden mit …“ erschienen, Gerät angelegt, Einmal-Code verbraucht");

            // 2. Handy → PC: Datei im Datei-Feld wählen (wie die Auswahl am Handy); die Seite bildet SHA-256 und schickt die Datei
            var upSrc = Path.Combine(d, "Handy-Foto.jpg"); var upBytes = RandomNumberGenerator.GetBytes(1_500_000); File.WriteAllBytes(upSrc, upBytes);
            var docNode = (await page.Call("DOM.getDocument", new { depth = 1 }, ct)).GetProperty("root").GetProperty("nodeId").GetInt32();
            var input = (await page.Call("DOM.querySelector", new { nodeId = docNode, selector = "#f" }, ct)).GetProperty("nodeId").GetInt32();
            await page.Call("DOM.setFileInputFiles", new { nodeId = input, files = new[] { upSrc } }, ct);
            await Wait("document.getElementById('st').textContent.indexOf('angekommen')>=0", "Upload", 60);
            var status = await Eval("document.getElementById('st').textContent");
            Assert.Contains("Prüfsumme gleich: 1", status);
            var arrived = Path.Combine(inbox, "Handy-Foto.jpg");
            Assert.Equal(Sha(upBytes), PhoneTransfer.Sha256File(arrived));
            L($"Handy → PC: {upBytes.Length} B, SHA-256 Quelle {Sha(upBytes)} = angekommen {PhoneTransfer.Sha256File(arrived)} · Seite meldet: {status}");

            // 3. PC → Handy: „Datei senden“ für genau dieses Handy, auf der Handy-Seite antippen → Download im Browser
            var downSrc = Path.Combine(d, "Rechnung November.pdf"); var downBytes = RandomNumberGenerator.GetBytes(900_000); File.WriteAllBytes(downSrc, downBytes);
            var offered = await PhoneTransfer.OfferAsync(downSrc, outbox, dev.Id);
            run.Record(PhoneRunLog.Steps.Provided, dev.Id, null, bytes: offered.Bytes, sha: offered.Sha256, note: "liegt 24 Stunden nur für dieses Handy bereit");
            await Eval("load(); true");
            await Wait("!!document.querySelector('#l a') && document.querySelector('#l').innerText.indexOf('nur für dieses Handy')>=0", "Liste „Vom PC für dich“");
            await Eval("document.querySelector('#l a').click(); true");
            var got = Path.Combine(downloads, "Rechnung November.pdf");
            for (var i = 0; i < 300 && !(File.Exists(got) && !Directory.EnumerateFiles(downloads, "*.crdownload").Any()); i++) await Task.Delay(100, ct);
            Assert.True(File.Exists(got), "Download fehlt: " + string.Join(", ", Directory.GetFiles(downloads).Select(Path.GetFileName)));
            Assert.Equal(Sha(downBytes), PhoneTransfer.Sha256File(got));
            for (var i = 0; i < 50 && !run.Entries.Any(e => e.Step == PhoneRunLog.Steps.PcToPhone); i++) await Task.Delay(100, ct);
            L($"PC → Handy: {downBytes.Length} B, SHA-256 bereitgestellt {offered.Sha256} = im Browser gespeichert {PhoneTransfer.Sha256File(got)}");

            // 4. Am PC entfernen → das Handy wird sofort abgewiesen (Liste und erneuter Upload)
            access.Remove(dev.Id); PhoneTransfer.DropDevice(outbox, dev.Id);
            run.Record(PhoneRunLog.Steps.Removed, dev.Id, null, note: "am PC entfernt, Schlüssel gelöscht");
            await Eval("load(); true");
            await Wait("document.getElementById('l').innerText.indexOf('nicht mehr mit dem PC verbunden')>=0", "Sperre in der Liste");
            await page.Call("DOM.setFileInputFiles", new { nodeId = input, files = new[] { upSrc } }, ct);
            await Wait("document.getElementById('st').textContent.indexOf('Nicht mehr verbunden')>=0", "Sperre beim Upload", 60);
            Assert.False(File.Exists(Path.Combine(inbox, "Handy-Foto (2).jpg")));
            L("Entfernen: Liste und erneuter Upload abgewiesen (401), keine zweite Datei angekommen");
        }
        finally
        {
            try { if (!proc.HasExited) proc.Kill(entireProcessTree: true); proc.WaitForExit(5000); } catch (InvalidOperationException) { }
            L("Browser-Prozess beendet (eigene PID); eigenes Profil wird danach gelöscht");
        }
        Assert.All(PhoneRunLog.Evaluate(run.Entries), c => Assert.True(c.Ok, c.Name + ": " + c.Detail));
        var (md, raw, sums) = run.Stop(beleg, CrashLog.AppVersion());
        var text = log.ToString();
        if (lan is not null) text = text.Replace(lan.ToString(), "<lan-ip>");
        File.WriteAllText(Path.Combine(beleg, "browser-lauf.log"), text);
        var all = File.ReadAllText(md) + File.ReadAllText(raw) + text;
        Assert.DoesNotContain("Browser-Handy", all);
        if (lan is not null) Assert.DoesNotContain(lan.ToString(), all);
        for (var i = 0; i < 20; i++) { try { Directory.Delete(profile, true); break; } catch (IOException) { await Task.Delay(250, ct); } catch (UnauthorizedAccessException) { await Task.Delay(250, ct); } }
        if (Environment.GetEnvironmentVariable("TRENDO_T0767_BELEG") is not { Length: > 0 }) try { Directory.Delete(d, true); } catch (IOException) { }
    }

    /// <summary>Kleiner DevTools-Kanal: Befehl schicken, auf die Antwort mit derselben Nummer warten, Ereignisse überspringen.</summary>
    sealed class Cdp : IAsyncDisposable
    {
        readonly ClientWebSocket _ws = new();
        int _id;
        public static async Task<Cdp> Open(string url, CancellationToken ct) { var c = new Cdp(); await c._ws.ConnectAsync(new Uri(url), ct); return c; }
        public async Task<JsonElement> Call(string method, object args, CancellationToken ct)
        {
            var id = ++_id;
            await _ws.SendAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { id, method, @params = args })), WebSocketMessageType.Text, true, ct);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(60));
            while (true)
            {
                using var ms = new MemoryStream(); var buf = new byte[65536]; WebSocketReceiveResult r;
                do { r = await _ws.ReceiveAsync(buf, timeout.Token); ms.Write(buf, 0, r.Count); } while (!r.EndOfMessage);
                using var doc = JsonDocument.Parse(ms.ToArray());
                if (!doc.RootElement.TryGetProperty("id", out var got) || got.GetInt32() != id) continue;
                if (doc.RootElement.TryGetProperty("error", out var err)) throw new InvalidOperationException(method + ": " + err);
                return doc.RootElement.GetProperty("result").Clone();
            }
        }
        public async ValueTask DisposeAsync() { try { await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None); } catch (WebSocketException) { } _ws.Dispose(); }
    }
}
