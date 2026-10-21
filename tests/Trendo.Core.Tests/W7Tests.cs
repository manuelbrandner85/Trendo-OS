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

    [Fact(DisplayName = "CORE-04: Kopplung nur mit gültigem Einmal-Code und Heimnetz-Adresse; Entfernen; Audit")]
    public void Pairing()
    {
        var (s, a, r) = Make(); var now = new DateTime(2026, 10, 21, 20, 0, 0);
        var c = s.NewCode(now); Assert.Matches("^\\d{6}$", c.Code);
        Assert.Null(s.Pair("000000" == c.Code ? "111111" : "000000", "Pi", NodeKind.Pi, "192.168.1.20", 8787, ["ki"], now).Node);
        Assert.Null(s.Pair(c.Code, "Pi", NodeKind.Pi, "8.8.8.8", 8787, ["ki"], now).Node);
        Assert.Null(s.Pair(c.Code, "Pi", NodeKind.Pi, "192.168.1.20", 8787, ["ki"], now.AddMinutes(11)).Node);
        var (n, m) = s.Pair(c.Code, "Pi Wohnzimmer", NodeKind.Pi, "192.168.1.20", 8787, ["KI", " ocr", ""], now);
        Assert.NotNull(n); Assert.Equal(["ki", "ocr"], n!.Caps);
        Assert.Null(s.Pair(c.Code, "Noch einer", NodeKind.Pc, "10.0.0.2", 8787, [], now).Node); // Einmal-Code verbraucht
        Assert.Single(s.Nodes());
        foreach (var ok in new[] { "10.1.2.3", "172.16.0.1", "192.168.0.5", "raspberrypi.local", "fe80::1" }) Assert.True(FusionStore.IsLanAddress(ok), ok);
        foreach (var bad in new[] { "1.1.1.1", "172.32.0.1", "example.com", "http://192.168.1.1", "" }) Assert.False(FusionStore.IsLanAddress(bad), bad);
        Assert.Equal(new Uri("http://192.168.1.20:8787/health"), FusionStore.HealthUri(n));
        Assert.True(s.Remove(n.Id)); Assert.Empty(s.Nodes());
        var log = File.ReadAllText(Path.Combine(r, "a.jsonl"));
        Assert.Contains("fusion.paired", log); Assert.Contains("fusion.pair.rejected", log); Assert.Contains("fusion.removed", log);
        Assert.DoesNotContain(c.Code, log);
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
}
