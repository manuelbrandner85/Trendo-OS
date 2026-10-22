using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

public class LiveStreamTests
{
    static StreamSource Cam(string id = "/dev/video0") => new("cam:" + id, StreamKind.Kamera, "Kamera", id, null);
    static StreamSource Mic() => new("mic:default", StreamKind.Mikrofon, "Mikrofon", "default", null);
    static StreamSource Screen() => new("screen:HDMI-1", StreamKind.Bildschirm, "Bildschirm HDMI-1", ":0.0", "1920x1080");

    [Fact(DisplayName = "Quellen: DirectShow, xrandr, Pulse und Fenster werden gelesen, nichts wird erfunden")]
    public void Parse_devices()
    {
        var dshow = """
            [dshow] DirectShow video devices
            [dshow]  "Integrated Camera"
            [dshow]     Alternative name "@device_pnp"
            [dshow] DirectShow audio devices
            [dshow]  "Microphone (Realtek)"
            [dshow] "USB-Cam" (video)
            """;
        var cams = LiveStream.ParseDirectShow(dshow);
        Assert.Equal(["Integrated Camera", "USB-Cam"], cams.Where(s => s.Kind == StreamKind.Kamera).Select(s => s.Label));
        Assert.Equal("Microphone (Realtek)", Assert.Single(cams.Where(s => s.Kind == StreamKind.Mikrofon)).Label);

        var screens = LiveStream.ParseXrandr("HDMI-1 connected primary 1920x1080+0+0\nDP-2 connected 1280x720+1920+0\n", ":0.0");
        Assert.Equal(":0.0", screens[0].Device);
        Assert.Equal("1920x1080", screens[0].Size);
        Assert.Equal(":0.0+1920,0", screens[1].Device);

        var pulse = LiveStream.ParsePulse("0\talsa_input.analog\tmodule\tRUNNING\n1\talsa_output.monitor\tmodule\tIDLE\n");
        Assert.Equal(StreamKind.Mikrofon, pulse[0].Kind);
        Assert.Equal("Ton vom PC", pulse[1].Label);

        var wins = LiveStream.ParseWindows("0x0120000a  0 rechner Editor – Trendo\n", windows: false);
        Assert.Equal("0x0120000a", wins[0].Device);
        var grab = LiveStream.ParseXwininfo("Width: 800\nHeight: 600\nAbsolute upper-left X: 12\nAbsolute upper-left Y: 40\n", ":0.0");
        Assert.Equal((":0.0+12,40", "800x600"), grab);

        var facts = new CaptureFacts { Videos = [("/dev/video2", "Logitech")], Pulse = "0\talso\tmod\tRUN\n", Xrandr = "eDP-1 connected 1366x768+0+0\n" };
        var all = LiveStream.Read(facts);
        Assert.Contains(all, s => s.Device == "/dev/video2" && s.Label == "Logitech");
        Assert.Contains(all, s => s.Kind == StreamKind.Bildschirm && s.Size == "1366x768");
        Assert.DoesNotContain(all, s => s.Id == "screen:default");
        Assert.Empty(LiveStream.Read(new CaptureFacts()));
    }

    [Fact(DisplayName = "FFmpeg: Vorschau und Aufnahme bleiben lokal, die Sendung nutzt die RTMP-Adresse")]
    public void Args_local_or_live()
    {
        var preview = LiveStream.Args([Screen(), Cam(), Mic()], LiveOut.Vorschau, "/tmp/v.png", windows: false);
        Assert.Contains("x11grab", preview);
        Assert.Contains("v4l2", preview);
        Assert.Contains("hstack=inputs=2", string.Join(" ", preview));
        Assert.DoesNotContain(preview, a => a.Contains("rtmp", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("/tmp/v.png", preview[^1]);

        var rec = LiveStream.Args([Mic()], LiveOut.Aufnahme, "/tmp/a.m4a", windows: false);
        Assert.Contains("pulse", rec);
        Assert.Equal("/tmp/a.m4a", rec[^1]);

        var url = LiveStream.PublishUrl("rtmp://a.rtmp.youtube.com/live2/", "geheim-schluessel");
        var live = LiveStream.Args([Cam()], LiveOut.Sendung, "", windows: true, url);
        Assert.Contains("dshow", live);
        Assert.Contains("video=" + Cam().Device, live);
        Assert.Equal(url, live[^1]);
        Assert.Contains("libx264", live);
        Assert.Throws<ArgumentException>(() => LiveStream.PublishUrl("https://example", "geheim-schluessel"));
        Assert.Throws<ArgumentException>(() => LiveStream.Args([Cam()], LiveOut.Vorschau, "/tmp/v.png", false, url));
    }

    [Fact(DisplayName = "Sendung: ohne Freigabe startet nichts, der Schlüssel steht nicht im Ziel")]
    public void Gate_blocks_until_approved()
    {
        var f = new Fixture(); using var _ = f;
        var key = "geheim-schluessel";
        var ids = new[] { "cam:/dev/video0" };
        var started = 0;
        Assert.Throws<ApprovalException>(() => LiveStream.Start(f.Core.Approvals, "keine", "ffmpeg", "YouTube", "rtmp://a.rtmp.youtube.com/live2", ids, key, () => started++));
        Assert.Equal(0, started);
        var req = LiveStream.Ask(f.Core.Approvals, Principal.LocalOwner, "ffmpeg", "YouTube", "rtmp://a.rtmp.youtube.com/live2", ids, key);
        Assert.DoesNotContain(key, req.Target);
        Assert.DoesNotContain(key, req.Description);
        Assert.Equal(0, started);
        Assert.Throws<ApprovalException>(() => LiveStream.Start(f.Core.Approvals, req.Id, "ffmpeg", "YouTube", "rtmp://a.rtmp.youtube.com/live2", ids, key, () => started++));
        Assert.Equal(0, started);
        f.Core.Approvals.Approve(req.Id, Principal.LocalOwner);
        Assert.Throws<ApprovalException>(() => LiveStream.Start(f.Core.Approvals, req.Id, "ffmpeg", "YouTube", "rtmp://a.rtmp.youtube.com/live2", ids, "anderer-schluessel", () => started++));
        Assert.Equal(0, started);
        LiveStream.Start(f.Core.Approvals, req.Id, "ffmpeg", "YouTube", "rtmp://a.rtmp.youtube.com/live2", ids, key, () => started++);
        Assert.Equal(1, started);
        Assert.Throws<ApprovalException>(() => LiveStream.Start(f.Core.Approvals, req.Id, "ffmpeg", "YouTube", "rtmp://a.rtmp.youtube.com/live2", ids, key, () => started++));
        Assert.DoesNotContain(f.Core.Audit.ReadAll(), e => e.Detail.Contains(key));
    }

    [Fact(DisplayName = "OBS: Schlüssel landet nur in der Dienstdatei, anderes bleibt stehen")]
    public void Obs_service_keeps_other_fields()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-obs-" + Guid.NewGuid().ToString("N"), "basic", "profiles", "Unbenannt");
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "service.json");
        File.WriteAllText(file, """{"type":"rtmp_common","settings":{"server":"auto","bwtest":false}}""");
        var root = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(dir)))!;
        Assert.Equal(file, LiveStream.NewestService(root));
        LiveStream.WriteService(file, "rtmp://live.twitch.tv/app", "twitch-schluessel");
        var text = File.ReadAllText(file);
        Assert.Contains("twitch-schluessel", text);
        Assert.Contains("rtmp://live.twitch.tv/app", text);
        Assert.Contains("bwtest", text);
        Assert.Contains("rtmp_custom", text);
        Assert.Null(LiveStream.NewestService(Path.Combine(Path.GetTempPath(), "fehlt-" + Guid.NewGuid().ToString("N"))));
    }
}
