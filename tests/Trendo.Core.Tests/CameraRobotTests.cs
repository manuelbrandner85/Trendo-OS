using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

public class CameraRobotTests
{
    [Fact(DisplayName = "Kameras: Webcams und RTSP werden gelesen, http ist keine RTSP-Adresse")]
    public void Cameras_parse()
    {
        var text = """
            [dshow] DirectShow video devices
            [dshow]  "Integrierte Kamera"
            [dshow]     Alternative name "@device"
            [dshow] DirectShow audio devices
            [dshow]  "Mikrofon"
            """;
        var cams = CameraCatalog.ParseDirectShow(text);
        Assert.Equal("Integrierte Kamera", Assert.Single(cams).Name);
        var local = CameraCatalog.Local(new CameraFacts { Videos = [("/dev/video0", "Logitech")] });
        Assert.Equal("/dev/video0", Assert.Single(local).Address);
        var rtsp = CameraCatalog.Rtsp("Hof", "rtsp://user:geheim@10.0.0.5/live");
        Assert.Equal("rtsp://…@10.0.0.5/live", CameraCatalog.Redact(rtsp.Address));
        Assert.Throws<ArgumentException>(() => CameraCatalog.Rtsp("Hof", "http://10.0.0.5/live"));
        var snap = CameraCatalog.Snapshot(local[0], "/tmp/a.png", windows: false);
        Assert.Contains("v4l2", snap);
        Assert.Equal("/tmp/a.png", snap[^1]);
        Assert.DoesNotContain("rtmp", string.Join(" ", snap));
        var rec = CameraCatalog.Record(rtsp, "/tmp/a.mp4", 10, windows: false);
        Assert.Contains("-rtsp_transport", rec);
        Assert.Contains("10", rec);
        Assert.Contains(rtsp.Address, rec);
    }

    [Fact(DisplayName = "ONVIF: Suche liest Adressen, die Kette liefert die RTSP-Adresse, das Kennwort steht im Umschlag")]
    public async Task Onvif_chain()
    {
        var xml = """<d:XAddrs>http://192.168.0.20/onvif/device_service http://192.168.0.20/onvif/device_service</d:XAddrs>""";
        Assert.Equal(["http://192.168.0.20/onvif/device_service"], Onvif.XAddrs(xml));
        Assert.Contains("NetworkVideoTransmitter", System.Text.Encoding.UTF8.GetString(Onvif.Probe()));
        var urls = new List<string>();
        var rtsp = await Onvif.Resolve(async (url, body, _) =>
        {
            urls.Add(url);
            await Task.Yield();
            if (body.Contains("GetCapabilities", StringComparison.Ordinal)) return "<tt:Media><tt:XAddr>http://cam/onvif/media</tt:XAddr></tt:Media>";
            if (body.Contains("GetProfiles", StringComparison.Ordinal)) return """<trt:Profiles token="P1" fixed="true"></trt:Profiles>""";
            Assert.Contains("P1", body);
            Assert.Contains("anna", body);
            Assert.Contains("geheim", body);
            return "<tt:Uri>rtsp://192.168.0.20/stream</tt:Uri>";
        }, "192.168.0.20", "anna", "geheim");
        Assert.Equal("rtsp://192.168.0.20/stream", rtsp);
        Assert.Contains("http://192.168.0.20/onvif/device_service", urls[0]);
        Assert.Equal("http://cam/onvif/media", urls[1]);
        Assert.Throws<ArgumentException>(() => Onvif.DeviceService("ftp://kamera"));
    }

    [Fact(DisplayName = "Roboter: Probelauf sendet nichts, echt nur nach Freigabe, Not-Aus blockiert")]
    public void Motion_needs_approval_and_respects_estop()
    {
        var f = new Fixture(); using var _ = f;
        var link = new RobotLink();
        var order = new MotionOrder("Arm", 0, 45, -30, 2, "halten");
        var sent = new List<string>();
        Assert.Contains("Es wurde nichts gesendet", link.Dry(order));
        Assert.Contains("150,0", link.Simulate(new ArmConfig(), 150, 50, 40));
        Assert.Empty(sent);
        Assert.Throws<ApprovalException>(() => link.Send(f.Core.Approvals, "keine", order, sent.Add));
        Assert.Empty(sent);
        var req = link.Ask(f.Core.Approvals, Principal.LocalOwner, order);
        Assert.Equal(order.Target, req.Target);
        Assert.Empty(sent);
        link.Estop();
        f.Core.Approvals.Approve(req.Id, Principal.LocalOwner);
        Assert.Throws<InvalidOperationException>(() => link.Send(f.Core.Approvals, req.Id, order, sent.Add));
        Assert.Empty(sent);
        link.Release();
        var again = link.Ask(f.Core.Approvals, Principal.LocalOwner, order);
        Assert.Throws<ApprovalException>(() => link.Send(f.Core.Approvals, again.Id, order, sent.Add));
        f.Core.Approvals.Approve(again.Id, Principal.LocalOwner);
        Assert.Contains("Gesendet", link.Send(f.Core.Approvals, again.Id, order, sent.Add));
        Assert.Equal([order.Line], sent);
        Assert.Throws<ApprovalException>(() => link.Send(f.Core.Approvals, again.Id, order, sent.Add));
        Assert.Single(sent);
    }

    [Fact(DisplayName = "Roboter: der Anschluss bekommt genau die Zeile, ein fehlender Anschluss sendet nichts")]
    public void Wire_writes_only_when_the_port_exists()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-port-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var port = Path.Combine(dir, "tty");
        File.WriteAllText(port, "");
        RobotWire.Send(port, "MOVE Arm 0 45 -30 2 halten");
        Assert.Equal("MOVE Arm 0 45 -30 2 halten\n", File.ReadAllText(port));
        Assert.Throws<IOException>(() => RobotWire.Send(Path.Combine(dir, "fehlt"), "MOVE Arm 1 0 0 1 halten"));
        Assert.Throws<InvalidOperationException>(() => RobotWire.Send("  ", "MOVE Arm 1 0 0 1 halten"));
    }
}
