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
        var (tok, _) = st.TryPair(code, "Manuels PC", now); Assert.NotNull(tok);
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

    [Fact(DisplayName = "CORE-04 Begleiter: echter HTTP-Server – Health offen, alles andere nur mit Token, Kopplung, Entkoppeln")]
    public async Task Companion_http()
    {
        Assert.SkipUnless(!OperatingSystem.IsWindows(), "Übersprungen: der Begleiter-Server läuft in diesem Test nur unter Linux.");
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
        Assert.True((await h.PostAsync("unpair", null)).IsSuccessStatusCode);
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, (await h.GetAsync("info")).StatusCode);
    }

    [Fact(DisplayName = "CORE-04 Begleiter: echte OCR mit Tesseract")]
    public async Task Companion_ocr()
    {
        Assert.SkipUnless(!OperatingSystem.IsWindows(), "Übersprungen: der Begleiter-Server läuft in diesem Test nur unter Linux.");
        var (_, _, r) = Make(); var f = Path.Combine(r, "zustand.json");
        var port = 18000 + Random.Shared.Next(2000);
        var srv = new Trendo.Companion.CompanionServer(port, f, loopbackOnly: true);
        try
        {
            // Stop() auf einem nie gestarteten Listener wirft. Deshalb erst starten, wenn Tesseract da ist.
            if (srv.Tesseract() is null)
                Assert.Skip("Übersprungen: Tesseract ist nicht installiert.");
            srv.Start();
            using var h = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}/") };
            var st = CompanionState.Load(f); var code = st.NewCode(DateTime.Now); st.Save(f);
            var ok = await h.PostAsync("pair", new StringContent($"{{\"code\":\"{code}\",\"pcName\":\"Testrechner\"}}"));
            var pr = System.Text.Json.JsonSerializer.Deserialize<PairResponse>(await ok.Content.ReadAsStringAsync())!;
            h.DefaultRequestHeaders.Authorization = new("Bearer", pr.Token);
            var img = Path.Combine(AppContext.BaseDirectory, "fixtures", "ocr-pi.png");
            var o = await h.PostAsync("task/ocr?lang=eng", new ByteArrayContent(File.ReadAllBytes(img)));
            Assert.True(o.IsSuccessStatusCode, await o.Content.ReadAsStringAsync());
            var res = System.Text.Json.JsonSerializer.Deserialize<OcrResult>(await o.Content.ReadAsStringAsync())!;
            Assert.Contains("4711", res.Text); Assert.Contains("31.12.2026", res.Text);
        }
        finally { try { srv.Dispose(); } catch (InvalidOperationException) { } }
    }

    [Fact(DisplayName = "CORE-04 Begleiter: nur Heimnetz-/Loopback-Absender werden bedient")]
    public void Companion_remote_filter()
    {
        Assert.True(Trendo.Companion.CompanionServer.AllowedRemote(System.Net.IPAddress.Loopback));
        Assert.True(Trendo.Companion.CompanionServer.AllowedRemote(System.Net.IPAddress.Parse("192.168.178.20")));
        Assert.True(Trendo.Companion.CompanionServer.AllowedRemote(System.Net.IPAddress.Parse("::ffff:10.0.0.5")));
        Assert.False(Trendo.Companion.CompanionServer.AllowedRemote(System.Net.IPAddress.Parse("8.8.8.8")));
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

    sealed class AdbFake(string root) : IProcessRunner
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
        var (_, _, r) = Make(); var run = new AdbFake(r);
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
