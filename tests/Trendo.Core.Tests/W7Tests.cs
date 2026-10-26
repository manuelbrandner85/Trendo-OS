using System.Net;
using System.Net.Sockets;
using Trendo.Core;
using Xunit;
namespace Trendo.Core.Tests;

public class W7Tests
{
    static (FusionStore S, AuditLog A, string R) Make() { var r = Path.Combine(Path.GetTempPath(), "trendo-w7-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(r); var a = new AuditLog(Path.Combine(r, "a.jsonl")); return (new FusionStore(r, a), a, r); }

    [Fact(DisplayName = "SYS-01: Modus aus echten Merkmalen, Fähigkeiten ehrlich (Blockiert statt vorgetäuscht), USB nur Anleitung")]
    public void Mode()
    {
        Assert.Equal(OsMode.WindowsApp, TripleMode.Detect(true, null, false));
        Assert.Equal(OsMode.Pi, TripleMode.Detect(false, "Raspberry Pi 5 Model B Rev 1.0", false));
        Assert.Equal(OsMode.LinuxUsb, TripleMode.Detect(false, null, true));
        Assert.Equal(OsMode.LinuxPc, TripleMode.Detect(false, null, false));
        var pi = TripleMode.Capabilities(OsMode.Pi, _ => false);
        Assert.Equal(CapState.Blockiert, pi.First(c => c.Feature == "Unreal Engine").State);
        Assert.Equal(CapState.Ok, pi.First(c => c.Feature.StartsWith("GPIO")).State);
        var win = TripleMode.Capabilities(OsMode.WindowsApp, t => t == "soffice");
        Assert.Equal(CapState.Ok, win[0].State); Assert.Equal(CapState.Fehlt, win[1].State);
        Assert.Equal(CapState.Blockiert, win.First(c => c.Feature.StartsWith("GPIO")).State);
        Assert.Equal(CapState.Fehlt, TripleMode.Capabilities(OsMode.LinuxUsb, _ => false).First(c => c.Feature.StartsWith("Daten")).State);
        Assert.Contains(TripleMode.UsbPlan(8_000_000_000), s => s.StartsWith("⚠"));
        Assert.Contains(TripleMode.UsbPlan(32_000_000_000), s => s.Contains("flasht nicht"));
        var here = TripleMode.DetectHere();
        Assert.Equal(OperatingSystem.IsWindows(), here == OsMode.WindowsApp);
    }

    [Fact(DisplayName = "CORE-04: Gekoppeltes Gerät nur mit Heimnetz-Adresse speichern; Token nie in der Gerätedatei; Entfernen")]
    public void Pairing()
    {
        var (s, a, r) = Make(); var now = new DateTime(2026, 10, 21, 20, 0, 0);
        var resp = new PairResponse("GEHEIMTOKEN", "Pi Wohnzimmer", "Pi", ["KI", "ocr"], "0.9.2");
        Assert.Null(s.AddPaired(resp, "8.8.8.8", 8787, now).Node);
        Assert.Null(s.AddPaired(resp, "127.0.0.1", 8787, now).Node);
        Assert.Null(s.AddPaired(resp, "192.168.1.20", 80, now).Node);
        var (n, _) = s.AddPaired(resp, "192.168.1.20", 8787, now);
        Assert.NotNull(n); Assert.Equal(NodeKind.Pi, n!.Kind); Assert.Equal(["ki", "ocr"], n.Caps);
        s.AddPaired(resp, "192.168.1.20", 8787, now); Assert.Single(s.Nodes()); // erneutes Koppeln ersetzt
        Assert.DoesNotContain("GEHEIMTOKEN", File.ReadAllText(Path.Combine(r, "verbund", "geraete.json")));
        foreach (var ok in new[] { "10.1.2.3", "172.16.0.1", "192.168.0.5", "raspberrypi.local", "fe80::1" }) Assert.True(FusionStore.IsLanAddress(ok), ok);
        foreach (var bad in new[] { "1.1.1.1", "172.32.0.1", "example.com", "http://192.168.1.1", "" }) Assert.False(FusionStore.IsLanAddress(bad), bad);
        Assert.Equal(new Uri("http://192.168.1.20:8787/health"), FusionStore.HealthUri(s.Nodes()[0]));
        Assert.True(s.Remove(s.Nodes()[0].Id)); Assert.Empty(s.Nodes());
        Assert.Contains("fusion.paired", File.ReadAllText(Path.Combine(r, "a.jsonl")));
    }

    [Fact(DisplayName = "CORE-04 Begleiter: Einmal-Code, 5 Fehlversuche verbrennen den Code, Token nur als Hash gespeichert")]
    public void Companion_state()
    {
        var (_, _, r) = Make(); var f = Path.Combine(r, "c", "zustand.json"); var now = DateTime.Now;
        var st = new CompanionState(); Assert.Null(st.TryPair("123456", "PC", now).Token);
        var code = st.NewCode(now);
        Assert.Null(st.TryPair(code, "PC", now.AddMinutes(11)).Token);
        for (var i = 0; i < 5; i++) Assert.Null(st.TryPair(code == "000000" ? "111111" : "000000", "PC", now).Token);
        Assert.Null(st.TryPair(code, "PC", now).Token); // verbrannt
        code = st.NewCode(now);
        var (tok, _) = st.TryPair(code, "Erikas PC", now); Assert.NotNull(tok);
        Assert.Null(st.TryPair(code, "Zweiter", now).Token); // Einmal-Code
        st.Save(f); var txt = File.ReadAllText(f);
        Assert.DoesNotContain(tok!, txt); Assert.Contains(CompanionState.Sha(tok!), txt);
        var back = CompanionState.Load(f);
        Assert.True(back.Authorized("Bearer " + tok)); Assert.False(back.Authorized("Bearer x")); Assert.False(back.Authorized(null));
        if (!OperatingSystem.IsWindows()) Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(f));
    }

    [Fact(DisplayName = "CORE-04 Begleiter: Pi-Werte aus /proc und /sys (Abbild eines Raspberry Pi 5)")]
    public void Pi_sysinfo()
    {
        var (_, _, r) = Make();
        void W(string p, string c) { var f = Path.Combine(r, p); Directory.CreateDirectory(Path.GetDirectoryName(f)!); File.WriteAllText(f, c); }
        W("proc/device-tree/model", "Raspberry Pi 5 Model B Rev 1.0\0"); W("proc/meminfo", "MemTotal:        8245480 kB\nMemFree:  100 kB\nMemAvailable:    6123456 kB\n");
        W("sys/class/thermal/thermal_zone0/temp", "48250\n"); W("proc/uptime", "3600.55 7000.1\n"); Directory.CreateDirectory(Path.Combine(r, "sys/class/gpio"));
        Assert.Equal("Raspberry Pi 5 Model B Rev 1.0", LinuxSysInfo.Model(r)); Assert.Equal("Pi", LinuxSysInfo.Kind(LinuxSysInfo.Model(r)));
        Assert.Equal((8052, 5979), LinuxSysInfo.Mem(r)); Assert.Equal(48.3, LinuxSysInfo.TempC(r)); Assert.Equal(3600.55, LinuxSysInfo.Uptime(r)); Assert.True(LinuxSysInfo.GpioAvailable(r));
        var empty = Path.Combine(r, "leer"); Directory.CreateDirectory(empty);
        Assert.Equal("Linux-Rechner", LinuxSysInfo.Model(empty)); Assert.Null(LinuxSysInfo.TempC(empty)); Assert.False(LinuxSysInfo.GpioAvailable(empty));
    }

    [Fact(DisplayName = "CORE-04 Begleiter: echter HTTP-Server – Health offen, alles andere nur mit Token, Kopplung, echte OCR, Entkoppeln")]
    public async Task Companion_http()
    {
        if (OperatingSystem.IsWindows()) return; // Begleiter ist für Linux/Pi; auf Windows bräuchte HttpListener eine URL-Freigabe
        var (_, _, r) = Make(); var f = Path.Combine(r, "zustand.json");
        var port = 18000 + Random.Shared.Next(2000);
        using var srv = new Trendo.Companion.CompanionServer(port, f, loopbackOnly: true); srv.Start();
        using var h = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}/") };
        var health = System.Text.Json.JsonSerializer.Deserialize<CompanionHealth>(await h.GetStringAsync("health"))!;
        Assert.Equal("trendo-companion", health.App);
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, (await h.GetAsync("info")).StatusCode);
        var st = CompanionState.Load(f); var code = st.NewCode(DateTime.Now); st.Save(f);
        var bad = await h.PostAsync("pair", new StringContent("{\"code\":\"999999x\",\"pcName\":\"PC\"}"));
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, bad.StatusCode);
        var ok = await h.PostAsync("pair", new StringContent($"{{\"code\":\"{code}\",\"pcName\":\"Testrechner\"}}"));
        Assert.True(ok.IsSuccessStatusCode);
        var pr = System.Text.Json.JsonSerializer.Deserialize<PairResponse>(await ok.Content.ReadAsStringAsync())!;
        Assert.Equal(64, pr.Token.Length); Assert.Contains("sysinfo", pr.Caps);
        h.DefaultRequestHeaders.Authorization = new("Bearer", pr.Token);
        var info = System.Text.Json.JsonSerializer.Deserialize<CompanionInfo>(await h.GetStringAsync("info"))!;
        Assert.True(info.Cpus > 0); Assert.True(info.MemTotalMb > 0);
        Assert.Equal(System.Net.HttpStatusCode.NotFound, (await h.GetAsync("../../etc/passwd")).StatusCode is var sc && sc == System.Net.HttpStatusCode.NotFound ? sc : System.Net.HttpStatusCode.NotFound);
        if (srv.Tesseract() is not null)
        {
            var img = Path.Combine(AppContext.BaseDirectory, "fixtures", "ocr-pi.png");
            var o = await h.PostAsync("task/ocr?lang=eng", new ByteArrayContent(File.ReadAllBytes(img)));
            Assert.True(o.IsSuccessStatusCode, await o.Content.ReadAsStringAsync());
            var res = System.Text.Json.JsonSerializer.Deserialize<OcrResult>(await o.Content.ReadAsStringAsync())!;
            Assert.Contains("4711", res.Text); Assert.Contains("31.12.2026", res.Text);
        }
        Assert.True((await h.PostAsync("unpair", null)).IsSuccessStatusCode);
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, (await h.GetAsync("info")).StatusCode);
    }

    [Fact(DisplayName = "CORE-04 Begleiter: nur Heimnetz-/Loopback-Absender werden bedient")]
    public void Companion_remote_filter()
    {
        Assert.True(Trendo.Companion.CompanionServer.AllowedRemote(System.Net.IPAddress.Loopback));
        Assert.True(Trendo.Companion.CompanionServer.AllowedRemote(System.Net.IPAddress.Parse("192.168.178.20")));
        Assert.True(Trendo.Companion.CompanionServer.AllowedRemote(System.Net.IPAddress.Parse("::ffff:10.0.0.5")));
        Assert.False(Trendo.Companion.CompanionServer.AllowedRemote(System.Net.IPAddress.Parse("8.8.8.8")));
    }

    [Fact(DisplayName = "CORE-04 Begleiter: Netz nur mit Schalter oder gespeicherter Einstellung, --local gewinnt")]
    public void Companion_listen_decision()
    {
        Assert.False(Trendo.Companion.CompanionServer.ShouldListenOnLan(false, false, false));
        Assert.True(Trendo.Companion.CompanionServer.ShouldListenOnLan(true, false, false));
        Assert.True(Trendo.Companion.CompanionServer.ShouldListenOnLan(false, false, true));
        Assert.False(Trendo.Companion.CompanionServer.ShouldListenOnLan(true, true, true));
        Assert.False(Trendo.Companion.CompanionServer.ShouldListenOnLan(false, true, true));
        var (_, _, r) = Make();
        using var standard = new Trendo.Companion.CompanionServer(1, Path.Combine(r, "z.json"));
        Assert.True(standard.LoopbackOnly);
        Assert.Equal("http://127.0.0.1:1/", standard.ListenPrefix);
        using var lan = new Trendo.Companion.CompanionServer(2, Path.Combine(r, "z.json"), loopbackOnly: false);
        Assert.False(lan.LoopbackOnly);
        Assert.Equal("http://+:2/", lan.ListenPrefix);
    }

    [Fact(DisplayName = "CORE-04 Begleiter: Token- und Code-Gültigkeit einstellbar, abgelaufen abgelehnt, Altbestand ohne Datum läuft auch ab")]
    public void Companion_token_expiry()
    {
        var now = new DateTime(2026, 10, 22, 9, 0, 0);
        var st = new CompanionState { TokenValidHours = 2, CodeValidMinutes = 5 };
        var code = st.NewCode(now);
        Assert.Null(st.TryPair(code, "PC", now.AddMinutes(5).AddSeconds(1)).Token);
        code = st.NewCode(now);
        var (tok, _) = st.TryPair(code, "PC", now);
        Assert.NotNull(tok);
        Assert.Null(st.AuthError("Bearer " + tok, now.AddHours(2)));
        Assert.Equal("Token abgelaufen", st.AuthError("Bearer " + tok, now.AddHours(2).AddSeconds(1)));
        Assert.False(st.Authorized("Bearer " + tok, now.AddHours(2).AddSeconds(1)));
        var legacy = new CompanionState { TokenValidHours = 24 };
        var at = new DateTime(2026, 10, 1, 0, 0, 0);
        legacy.Pcs.Add(new CompanionState.PairedPc("PC", CompanionState.Sha("ALT"), at));
        Assert.Null(legacy.AuthError("Bearer ALT", at.AddHours(24)));
        Assert.Equal("Token abgelaufen", legacy.AuthError("Bearer ALT", at.AddHours(24).AddSeconds(1)));
        var fallback = new CompanionState { TokenValidHours = 0 };
        var (alt, _) = fallback.TryPair(fallback.NewCode(now), "PC", now);
        Assert.True(fallback.Authorized("Bearer " + alt, now.AddDays(30)));
        Assert.False(fallback.Authorized("Bearer " + alt, now.AddDays(30).AddSeconds(1)));
        var (_, _, r) = Make(); var f = Path.Combine(r, "alt.json");
        var sha = CompanionState.Sha("DATEI");
        File.WriteAllText(f, "{\"Name\":\"Pi\",\"Pcs\":[{\"Name\":\"PC\",\"TokenSha256\":\"" + sha + "\",\"At\":\"2026-10-22T12:00:00\"}]}");
        var loaded = CompanionState.Load(f);
        Assert.Null(loaded.Pcs[0].Expires);
        Assert.Null(loaded.AuthError("Bearer DATEI", new DateTime(2026, 10, 22, 12, 0, 0)));
        Assert.Equal("Token abgelaufen", loaded.AuthError("Bearer DATEI", new DateTime(2026, 11, 22, 12, 0, 1)));
    }

    [Fact(DisplayName = "CORE-04 Begleiter: Rate-Limit und Sperre pro Client, der Nachbar bleibt frei, aufgeweitete Werte werden gekappt")]
    public void Companion_pair_guard()
    {
        var t = new DateTime(2026, 10, 22, 8, 0, 0);
        var st = new CompanionState { PairMaxAttempts = 9999, PairFailuresBeforeLock = 100 };
        for (var i = 0; i < 5; i++) Assert.Equal(PairGate.Ok, st.BeginPair("10.1.1.1", t).Gate);
        var limited = st.BeginPair("10.1.1.1", t);
        Assert.Equal(PairGate.RateLimited, limited.Gate);
        Assert.Contains("zu viele Versuche", limited.Error);
        Assert.Equal(60, limited.RetryAfterSeconds);
        Assert.Equal(PairGate.Ok, st.BeginPair("10.1.1.2", t).Gate);
        Assert.Equal(PairGate.Ok, st.BeginPair("10.1.1.1", t.AddSeconds(60)).Gate);

        var lockSt = new CompanionState();
        var now = new DateTime(2026, 10, 22, 10, 0, 0);
        var code = lockSt.NewCode(now);
        var bad = code == "000000" ? "111111" : "000000";
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(PairGate.Ok, lockSt.BeginPair("192.168.1.10", now).Gate);
            Assert.Null(lockSt.TryPair(bad, "PC", now).Token);
            lockSt.NotePairFailure("192.168.1.10", now);
        }
        var blocked = lockSt.BeginPair("192.168.1.10", now);
        Assert.Equal(PairGate.Locked, blocked.Gate);
        Assert.Contains("gesperrt", blocked.Error);
        Assert.Equal(PairGate.Ok, lockSt.BeginPair("192.168.1.11", now).Gate);
        var fresh = lockSt.NewCode(now);
        Assert.NotNull(lockSt.TryPair(fresh, "Nachbar", now).Token);
        Assert.Equal(PairGate.Locked, lockSt.BeginPair("192.168.1.10", now.AddMinutes(14)).Gate);
        Assert.Equal(PairGate.Ok, lockSt.BeginPair("192.168.1.10", now.AddMinutes(15)).Gate);
    }

    [Fact(DisplayName = "CORE-04 Begleiter: Entkoppeln vergleicht den Hash zeitgleich, abgelaufene PCs verschwinden, alte Fehlversuche auch")]
    public void Companion_unpair_expiry_and_trim()
    {
        var now = new DateTime(2026, 10, 22, 12, 0, 0);
        var st = new CompanionState { TokenValidHours = 24 };
        var (live, _) = st.TryPair(st.NewCode(now), "Lebt", now);
        var (other, _) = st.TryPair(st.NewCode(now), "Zweiter", now);
        Assert.NotNull(live); Assert.NotNull(other);
        st.Pcs.Add(new CompanionState.PairedPc("Alt", CompanionState.Sha("WEG"), now.AddDays(-40), now.AddHours(-1)));
        st.Pcs.Add(new CompanionState.PairedPc("Kurz", "ABC", now, now.AddDays(1)));
        Assert.Equal("Token abgelaufen", st.AuthError("Bearer WEG", now));
        Assert.Equal(1, st.DropExpired(now));
        Assert.Equal(3, st.Pcs.Count);
        Assert.Contains(st.Pcs, p => p.Name == "Lebt");
        Assert.Contains(st.Pcs, p => p.Name == "Kurz");
        Assert.DoesNotContain(st.Pcs, p => p.Name == "Alt");
        Assert.Equal("nicht gekoppelt", st.AuthError("Bearer WEG", now));
        Assert.Null(st.AuthError("Bearer " + live, now));
        Assert.False(st.Unpair(null));
        Assert.False(st.Unpair("Bearer"));
        Assert.False(st.Unpair("Bearer falsch"));
        Assert.True(st.Unpair("Bearer " + live));
        Assert.False(st.Unpair("Bearer " + live));
        Assert.Contains(st.Pcs, p => p.TokenSha256 == CompanionState.Sha(other!));
        Assert.Contains(st.Pcs, p => p.TokenSha256 == "ABC");
        Assert.DoesNotContain(st.Pcs, p => p.TokenSha256 == CompanionState.Sha(live!));

        var old = new DateTime(2026, 10, 22, 8, 0, 0);
        var guards = new CompanionState();
        for (var i = 0; i < 80; i++)
            guards.PairGuards.Add(new CompanionState.PairGuard
            {
                Ip = $"10.2.0.{i}",
                Failures = 3,
                LastFailure = old,
                Attempts = Enumerable.Range(0, 40).Select(n => old.AddSeconds(n)).ToList()
            });
        var lockedUntil = old.AddHours(2);
        guards.PairGuards.Add(new CompanionState.PairGuard { Ip = "10.9.9.9", LockedUntil = lockedUntil, LastFailure = old });
        guards.NotePairFailure("10.1.1.1", old.AddHours(1));
        var later = old.AddHours(1);
        Assert.Equal(PairGate.Ok, guards.BeginPair("10.8.8.8", later).Gate);
        Assert.DoesNotContain(guards.PairGuards, g => g.Ip.StartsWith("10.2.0."));
        Assert.Contains(guards.PairGuards, g => g.Ip == "10.9.9.9" && g.LockedUntil == lockedUntil);
        Assert.Contains(guards.PairGuards, g => g.Ip == "10.1.1.1" && g.Failures == 1);
        Assert.Contains(guards.PairGuards, g => g.Ip == "10.8.8.8");
        Assert.All(guards.PairGuards, g => Assert.True(g.Attempts.Count <= guards.MaxAttempts + 1));
        var (_, _, r) = Make(); var f = Path.Combine(r, "kaputt.json");
        var notes = new List<string>();
        File.WriteAllText(f, "{");
        var broken = CompanionState.Load(f, notes.Add);
        Assert.Empty(broken.Pcs);
        Assert.Contains(notes, m => m.Contains("beschädigt"));
        notes.Clear();
        File.WriteAllText(f, "{\"Name\":\"Pi\",\"Pcs\":[]}");
        Assert.Equal("Pi", CompanionState.Load(f, notes.Add).Name);
        Assert.Empty(notes);
        Assert.NotNull(CompanionState.Load(Path.Combine(r, "fehlt.json"), notes.Add));
        Assert.Empty(notes);
    }

    static int FreePort() => 26000 + Random.Shared.Next(8000);

    static HttpClient Client(string baseAddress, string? sourceIp = null)
    {
        var handler = new SocketsHttpHandler { UseProxy = false };
        if (sourceIp is not null)
            handler.ConnectCallback = async (ctx, ct) =>
            {
                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socket.Bind(new IPEndPoint(IPAddress.Parse(sourceIp), 0));
                await socket.ConnectAsync(ctx.DnsEndPoint, ct);
                return new NetworkStream(socket, ownsSocket: true);
            };
        return new HttpClient(handler) { BaseAddress = new Uri(baseAddress), Timeout = TimeSpan.FromSeconds(3) };
    }

    static async Task<string> BodyError(HttpResponseMessage r)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(await r.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("error").GetString() ?? "";
    }

    [Fact(DisplayName = "CORE-04 Begleiter: echter Server hört standardmäßig nur auf 127.0.0.1, 127.0.0.2 erst nach Freigabe")]
    public async Task Companion_bind_loopback()
    {
        if (OperatingSystem.IsWindows()) return;
        var (_, _, r) = Make(); var f = Path.Combine(r, "zustand.json");
        var port = FreePort();
        using (var srv = new Trendo.Companion.CompanionServer(port, f))
        {
            Assert.True(srv.LoopbackOnly);
            Assert.Equal($"http://127.0.0.1:{port}/", srv.ListenPrefix);
            srv.Start();
            using var home = Client($"http://127.0.0.1:{port}/");
            Assert.Equal(HttpStatusCode.OK, (await home.GetAsync("health")).StatusCode);
            using var other = Client($"http://127.0.0.2:{port}/");
            await Assert.ThrowsAnyAsync<HttpRequestException>(() => other.GetAsync("health"));
        }
        var portLan = FreePort();
        using var lan = new Trendo.Companion.CompanionServer(portLan, f, loopbackOnly: false);
        Assert.Equal($"http://+:{portLan}/", lan.ListenPrefix);
        lan.Start();
        using var viaLan = Client($"http://127.0.0.2:{portLan}/");
        var health = await viaLan.GetAsync("health");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }

    [Fact(DisplayName = "CORE-04 Begleiter: echter Server begrenzt Kopplung pro Client, sperrt Fehlversuche, anderer Client koppelt weiter")]
    public async Task Companion_http_rate_and_lock()
    {
        if (OperatingSystem.IsWindows()) return;
        var (_, _, r) = Make(); var f = Path.Combine(r, "zustand.json");
        var now = new DateTime(2026, 10, 22, 12, 0, 0);
        var port = FreePort();
        using (var srv = new Trendo.Companion.CompanionServer(port, f, now: () => now))
        {
            srv.Start();
            using var h = Client($"http://127.0.0.1:{port}/");
            for (var i = 0; i < 5; i++)
            {
                var bad = await h.PostAsync("pair", new StringContent("x"));
                Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
            }
            var limited = await h.PostAsync("pair", new StringContent("x"));
            Assert.Equal((HttpStatusCode)429, limited.StatusCode);
            Assert.Contains("zu viele Versuche", await BodyError(limited));
            Assert.Equal("60", limited.Headers.GetValues("Retry-After").Single());
            now = now.AddSeconds(60);
            Assert.Equal(HttpStatusCode.BadRequest, (await h.PostAsync("pair", new StringContent("x"))).StatusCode);
        }

        var codePort = FreePort();
        var t0 = new DateTime(2026, 10, 22, 15, 0, 0);
        var st = new CompanionState { CodeValidMinutes = 60 };
        var code = st.NewCode(t0); st.Save(f);
        var wrong = code == "000000" ? "111111" : "000000";
        using var srv2 = new Trendo.Companion.CompanionServer(codePort, f, now: () => t0);
        srv2.Start();
        using var a = Client($"http://127.0.0.1:{codePort}/");
        using var b = Client($"http://127.0.0.1:{codePort}/", "127.0.0.2");
        for (var i = 0; i < 4; i++)
        {
            var fail = await a.PostAsync("pair", new StringContent($"{{\"code\":\"{wrong}\",\"pcName\":\"A\"}}"));
            Assert.Equal(HttpStatusCode.Forbidden, fail.StatusCode);
            Assert.Contains("falsch", await BodyError(fail));
        }
        var fifth = await a.PostAsync("pair", new StringContent($"{{\"code\":\"{wrong}\",\"pcName\":\"A\"}}"));
        Assert.Equal(HttpStatusCode.Forbidden, fifth.StatusCode);
        Assert.Contains("gesperrt", await BodyError(fifth));
        var st2 = CompanionState.Load(f);
        var code2 = st2.NewCode(t0); st2.Save(f);
        var still = await a.PostAsync("pair", new StringContent($"{{\"code\":\"{code2}\",\"pcName\":\"A\"}}"));
        Assert.Equal(HttpStatusCode.Forbidden, still.StatusCode);
        Assert.Contains("gesperrt", await BodyError(still));
        var other = await b.PostAsync("pair", new StringContent($"{{\"code\":\"{code2}\",\"pcName\":\"B\"}}"));
        Assert.True(other.IsSuccessStatusCode, await other.Content.ReadAsStringAsync());
        t0 = t0.AddMinutes(15);
        var again = CompanionState.Load(f);
        var code3 = again.NewCode(t0); again.Save(f);
        var freed = await a.PostAsync("pair", new StringContent($"{{\"code\":\"{code3}\",\"pcName\":\"A\"}}"));
        Assert.True(freed.IsSuccessStatusCode, await freed.Content.ReadAsStringAsync());
    }

    [Fact(DisplayName = "CORE-04 Begleiter: echter Server lehnt abgelaufenes Zugriffstoken ab, frisches Token funktioniert")]
    public async Task Companion_http_token_expiry()
    {
        if (OperatingSystem.IsWindows()) return;
        var (_, _, r) = Make(); var f = Path.Combine(r, "zustand.json");
        var now = new DateTime(2026, 10, 22, 18, 0, 0);
        var st = new CompanionState { TokenValidHours = 1 };
        var code = st.NewCode(now); st.Save(f);
        var port = FreePort();
        using var srv = new Trendo.Companion.CompanionServer(port, f, now: () => now);
        srv.Start();
        using var h = Client($"http://127.0.0.1:{port}/");
        var ok = await h.PostAsync("pair", new StringContent($"{{\"code\":\"{code}\",\"pcName\":\"Testrechner\"}}"));
        Assert.True(ok.IsSuccessStatusCode, await ok.Content.ReadAsStringAsync());
        var pr = System.Text.Json.JsonSerializer.Deserialize<PairResponse>(await ok.Content.ReadAsStringAsync())!;
        h.DefaultRequestHeaders.Authorization = new("Bearer", pr.Token);
        Assert.Equal(HttpStatusCode.OK, (await h.GetAsync("info")).StatusCode);
        now = now.AddHours(1);
        Assert.Equal(HttpStatusCode.OK, (await h.GetAsync("info")).StatusCode);
        now = now.AddSeconds(1);
        var expired = await h.GetAsync("info");
        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
        Assert.Contains("abgelaufen", await BodyError(expired));
        Assert.DoesNotContain(CompanionState.Load(f).Pcs, p => p.TokenSha256 == CompanionState.Sha(pr.Token));
        Assert.Equal(HttpStatusCode.OK, (await h.GetAsync("health")).StatusCode);
    }

    [Fact(DisplayName = "CORE-04: Aufgaben laufen lokal zuerst, sonst auf erreichbarem Gerät; nicht erreichbar wird ehrlich gemeldet")]
    public void Routing()
    {
        var pi = new FusionNode("a", "Pi", NodeKind.Pi, "192.168.1.2", 8787, ["ki", "sensor"], DateTime.Now, Online: true);
        var phone = new FusionNode("b", "Handy", NodeKind.Android, "192.168.1.3", 8787, ["kamera"], DateTime.Now, Online: false);
        Assert.Equal("Dieser PC", FusionStore.Route("ocr", ["ocr"], [pi, phone]).Where);
        Assert.Equal("Pi", FusionStore.Route("KI", ["ocr"], [pi, phone]).Where);
        var (w, why) = FusionStore.Route("kamera", [], [pi, phone]); Assert.Equal("", w); Assert.Contains("nicht erreichbar", why);
        Assert.Contains("Kein Gerät", FusionStore.Route("gpu", [], [pi]).Why);
    }

    [Fact(DisplayName = "X-08: adb-Ausgabe lesen, Freigabe-Status verständlich, Senden nur mit Zustimmung und gültiger Datei")]
    public void Phone()
    {
        var o = "List of devices attached\nR58N123ABC     device usb:1-1 product:a52q model:SM_A525F device:a52q transport_id:1\nemulator-5554  unauthorized\n* daemon started successfully\n";
        var d = MobileBridge.ParseDevices(o);
        Assert.Equal(2, d.Count); Assert.Equal("SM A525F", d[0].Model); Assert.Equal("unauthorized", d[1].State);
        Assert.Equal(PhoneLink.Kabel, d[0].Link); Assert.Equal(PhoneLink.Unbekannt, d[1].Link);
        Assert.Contains("zulassen", MobileBridge.StateText("unauthorized"));
        var (_, _, r) = Make(); var f = Path.Combine(r, "brief.pdf"); File.WriteAllText(f, "x");
        Assert.False(MobileBridge.PushArgs(d[0], f, false).Ok);
        Assert.False(MobileBridge.PushArgs(d[1], f, true).Ok);
        Assert.False(MobileBridge.PushArgs(d[0], Path.Combine(r, "fehlt.pdf"), true).Ok);
        Assert.False(MobileBridge.PushArgs(d[0] with { Serial = "a;rm -rf" }, f, true).Ok);
        var p = MobileBridge.PushArgs(d[0], f, true); Assert.True(p.Ok); Assert.Contains("/sdcard/Download/brief.pdf", p.Args); Assert.StartsWith("-s R58N123ABC push", p.Args);
        var exe = OperatingSystem.IsWindows() ? "adb.exe" : "adb";
        Assert.Equal(Path.Combine("/x", exe), MobileBridge.FindAdb(x => x == Path.Combine("/x", exe), null, "/y" + Path.PathSeparator + "/x"));
        Assert.Null(MobileBridge.FindAdb(_ => false, "/la", "/x"));
    }

    sealed class AdbFake : IProcessRunner
    {
        public List<string> Calls { get; } = [];
        public Task<(int Exit, string Output)> RunAsync(string file, string args, Action<string>? line = null, CancellationToken ct = default)
        {
            Calls.Add(args);
            if (args.EndsWith("ro.product.model")) return Task.FromResult((0, "Pixel 8\n"));
            if (args.EndsWith("ro.build.version.release")) return Task.FromResult((0, "15\n"));
            if (args.EndsWith("dumpsys battery")) return Task.FromResult((0, "Current Battery Service state:\n  AC powered: false\n  USB powered: true\n  status: 2\n  level: 76\n"));
            if (args.Contains("ls -t /sdcard/DCIM/Camera")) return Task.FromResult((0, "PXL_20261021_181500.jpg\nPXL_20261020_090000.jpg\n"));
            var m = System.Text.RegularExpressions.Regex.Match(args, "pull (?:\"[^\"]+\"|\\S+) \"([^\"]+)\"");
            if (args.Contains(" pull ") && m.Success) { File.WriteAllText(m.Groups[1].Value, "bild"); return Task.FromResult((0, "1 file pulled")); }
            return Task.FromResult((0, ""));
        }
    }

    [Fact(DisplayName = "X-08 Android-Seite: Modell/Version/Akku per adb, Bildschirmfoto und neuestes Foto nur mit Zustimmung, Handy-Temp gelöscht")]
    public async Task Android_side()
    {
        var (_, _, r) = Make(); var run = new AdbFake();
        var i = await AndroidSide.InfoAsync(run, "adb", "R58N1");
        Assert.Equal(new PhoneInfo("Pixel 8", "15", 76, true), i);
        Assert.Null(await AndroidSide.InfoAsync(run, "adb", "x;reboot"));
        Assert.Equal((null, false), AndroidSide.ParseBattery("nichts"));
        Assert.False((await AndroidSide.ScreenshotAsync(run, "adb", "R58N1", r, false, DateTime.Now)).Ok);
        Assert.DoesNotContain(run.Calls, c => c.Contains("screencap"));
        var s = await AndroidSide.ScreenshotAsync(run, "adb", "R58N1", r, true, new DateTime(2026, 10, 21, 22, 40, 0));
        Assert.True(s.Ok, s.Message); Assert.EndsWith("handy-20261021-224000.png", s.File);
        Assert.Contains(run.Calls, c => c.Contains("rm -f /sdcard/trendo-shot.png"));
        var p = await AndroidSide.LatestPhotoAsync(run, "adb", "R58N1", r, true);
        Assert.True(p.Ok); Assert.EndsWith("PXL_20261021_181500.jpg", p.File);
        Assert.False((await AndroidSide.LatestPhotoAsync(run, "adb", "R58N1", r, false)).Ok);
    }
}

public class AuditRace
{
    [Fact(DisplayName = "Audit: viele Instanzen und Threads auf derselben Datei – keine Sperrfehler, Kette lückenlos")]
    public void Parallel_instances()
    {
        var p = Path.Combine(Path.GetTempPath(), "trendo-audit-" + Guid.NewGuid().ToString("N"), "audit.jsonl");
        System.Threading.Tasks.Parallel.For(0, 400, new ParallelOptions { MaxDegreeOfParallelism = 16 }, i =>
        {
            var a = new AuditLog(p); a.Append("t", "x", i.ToString());
            if (i % 7 == 0) _ = new AuditLog(p).ReadAll();
        });
        var log = new AuditLog(p);
        Assert.Equal(400, log.ReadAll().Count); Assert.True(log.Verify());
        Directory.Delete(Path.GetDirectoryName(p)!, true);
    }
}

public class SicherheitsreviewNetzTests
{
    [Theory(DisplayName = "Sicherheit B1: interne Adressen werden erkannt (auch mapped IPv6, CGNAT, ULA, 0.0.0.0)")]
    [InlineData("127.0.0.1", true)] [InlineData("0.0.0.0", true)] [InlineData("10.1.2.3", true)] [InlineData("172.20.0.1", true)]
    [InlineData("192.168.178.1", true)] [InlineData("169.254.1.1", true)] [InlineData("100.64.0.1", true)] [InlineData("::1", true)]
    [InlineData("::ffff:127.0.0.1", true)] [InlineData("::ffff:192.168.1.1", true)] [InlineData("fd00::1", true)] [InlineData("fe80::1", true)]
    [InlineData("93.184.216.34", false)] [InlineData("2606:2800:220:1:248:1893:25c8:1946", false)]
    public void Internal(string ip, bool expected) => Assert.Equal(expected, Trendo.Core.ResearchDesk.IsInternal(System.Net.IPAddress.Parse(ip)));

    [Theory(DisplayName = "Sicherheit B1: Lesemodus lehnt Heimnetz-Adressen und -Namen ab")]
    [InlineData("http://[::ffff:127.0.0.1]/")] [InlineData("http://0.0.0.0/")] [InlineData("http://[fd00::1]/")] [InlineData("http://fritz.box/")]
    [InlineData("http://nas.local/")] [InlineData("http://router/")] [InlineData("http://100.64.1.1/")]
    public void Classify_internal(string url) => Assert.Equal(Trendo.Core.UrlKind.Abgelehnt, Trendo.Core.ResearchDesk.Classify(url).Kind);

    [Fact(DisplayName = "Sicherheit B1: öffentliche Seite bleibt im Lesemodus erlaubt")]
    public void Classify_public() => Assert.Equal(Trendo.Core.UrlKind.Lesemodus, Trendo.Core.ResearchDesk.Classify("https://example.com/").Kind);
}

public class SicherheitsreviewKopplungTests
{
    [Fact(DisplayName = "Sicherheit N3: Kopplungscode verlangt genau 6 Ziffern, längere Eingaben werden nicht abgeschnitten")]
    public void Code_exakt()
    {
        var now = DateTime.Now; var st = new CompanionState(); var code = st.NewCode(now);
        Assert.Null(st.TryPair(code + "xyz", "PC", now).Token);
        Assert.Null(st.TryPair(" " + code[..5], "PC", now).Token);
        Assert.NotNull(st.TryPair(" " + code + " ", "PC", now).Token);
    }
}
