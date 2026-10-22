using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

public class CORE09_Cameras
{
    [Fact(DisplayName = "CORE-09: Webcams aus sysfs, DirectShow nur Video, RTSP-Adresse")]
    public void Discover()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-v4l-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "video0"));
        Directory.CreateDirectory(Path.Combine(root, "video1"));
        File.WriteAllText(Path.Combine(root, "video0", "name"), "Labor-Kamera\n");
        File.WriteAllText(Path.Combine(root, "video1", "name"), "Labor-Kamera metadata\n");
        var cams = WebcamScan.ReadVideo4Linux(root);
        var one = Assert.Single(cams);
        Assert.Equal("/dev/video0", one.Source);
        Assert.Equal("Labor-Kamera", one.Name);
        Directory.Delete(root, true);

        var dshow = WebcamScan.FromDshow("[dshow] \"USB-Kamera\" (video)\n[dshow] \"Mikrofon\" (audio)\n");
        var video = Assert.Single(dshow);
        Assert.Equal("USB-Kamera", video.Name);
        Assert.Equal(CameraKind.Webcam, video.Kind);

        var ip = WebcamScan.FromRtsp("rtsp://10.0.0.8/live");
        Assert.Equal("rtsp://10.0.0.8/live", ip.Source);
        Assert.True(ip.HasPicture);
        Assert.Contains("rtsp://", Assert.Throws<ArgumentException>(() => WebcamScan.FromRtsp("http://10.0.0.8/x")).Message);
    }

    [Fact(DisplayName = "CORE-09: FFmpeg-Argumente für Webcam, RTSP, Aufnahme")]
    public void Args()
    {
        var web = new CameraDevice("/dev/video0", "Cam", CameraKind.Webcam, "/dev/video0");
        var snap = CameraFfmpeg.SnapshotArgs(web, "bild.jpg", windows: false);
        Assert.Contains("v4l2", snap);
        Assert.Contains("-frames:v", snap);
        Assert.Equal("bild.jpg", snap[^1]);

        var win = new CameraDevice("dshow:USB", "USB", CameraKind.Webcam, "USB-Kamera");
        Assert.Contains("video=USB-Kamera", CameraFfmpeg.InputArgs(win, windows: true));

        var rtsp = WebcamScan.FromRtsp("rtsp://10.1.1.5:554/stream");
        var rec = CameraFfmpeg.RecordArgs(rtsp, "film.mp4", windows: false);
        Assert.Contains("-rtsp_transport", rec);
        Assert.Contains("tcp", rec);
        Assert.Equal("film.mp4", rec[^1]);
        Assert.DoesNotContain("-t", rec);
        Assert.Contains("libx264", rec);

        var blind = new CameraDevice("onvif:x", "Netz", CameraKind.Onvif, "");
        Assert.False(blind.HasPicture);
        Assert.Throws<InvalidOperationException>(() => CameraFfmpeg.InputArgs(blind, false));
    }

    [Fact(DisplayName = "CORE-09: Bild holen liest die Datei, die FFmpeg geschrieben hat")]
    public async Task Grab()
    {
        IReadOnlyList<string> seen = [];
        var src = new FfmpegFrameSource(() => "/usr/bin/ffmpeg", (_, args, _) =>
        {
            seen = args.ToList();
            File.WriteAllBytes(args[^1], Enumerable.Repeat((byte)255, 40).ToArray());
            return new Cli.Result(0, "", "");
        });
        var bytes = await src.GrabAsync(new CameraDevice("/dev/video2", "Cam", CameraKind.Webcam, "/dev/video2"), CancellationToken.None);
        Assert.Equal(40, bytes.Length);
        Assert.Contains("/dev/video2", seen);
        Assert.False(File.Exists(seen[^1]));

        var fail = new FfmpegFrameSource(() => null);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fail.GrabAsync(new CameraDevice("d", "d", CameraKind.Webcam, "/dev/video0"), CancellationToken.None));
    }

    [Fact(DisplayName = "CORE-09: Aufnahme startet einen Prozess und schreibt die Datei beim Stoppen")]
    public void RecordStop()
    {
        var args = CameraFfmpeg.RecordArgs(new CameraDevice("d", "Cam", CameraKind.Webcam, "/dev/video0"), "out.mp4", false);
        Assert.Equal("out.mp4", args[^1]);
        if (!OperatingSystem.IsLinux()) return;
        var dir = Path.Combine(Path.GetTempPath(), "trendo-rec-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var script = Path.Combine(dir, "fake-ffmpeg");
        var file = Path.Combine(dir, "film.mp4");
        File.WriteAllText(script, "#!/bin/sh\nout=\"\"\nfor a in \"$@\"; do out=\"$a\"; done\nread line\nprintf aufnahme > \"$out\"\n");
        File.SetUnixFileMode(script, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var rec = new CameraRecorder();
        rec.Start(script, ["-y", file], file);
        Assert.True(rec.Running);
        rec.Stop();
        Assert.False(rec.Running);
        Assert.Equal("aufnahme", File.ReadAllText(file));
        Directory.Delete(dir, true);
    }

    [Fact(DisplayName = "CORE-09: ONVIF-Probe, Profile und Stream-Adresse")]
    public async Task Onvif()
    {
        Assert.Contains("NetworkVideoTransmitter", OnvifXml.Probe());
        var xml = "<d:XAddrs>http://10.0.0.5/onvif/device_service http://10.0.0.9/onvif/device_service</d:XAddrs>";
        Assert.Equal(2, OnvifXml.XAddrs(xml).Count);
        Assert.Equal("P1", OnvifXml.ProfileToken("<trt:Profiles token=\"P1\" fixed=\"true\">"));
        Assert.Contains("P1", OnvifXml.GetStreamUriBody("P1"));
        Assert.Contains("a&lt;b", OnvifXml.GetStreamUriBody("a<b"));
        Assert.DoesNotContain("a<b", OnvifXml.GetStreamUriBody("a<b"));
        Assert.Equal("rtsp://10.0.0.5:554/live", OnvifXml.StreamUri("<tt:Uri>rtsp://10.0.0.5:554/live</tt:Uri>"));

        var fake = new FakeOnvif();
        var cams = await OnvifCameras.DiscoverAsync(fake);
        var cam = Assert.Single(cams);
        Assert.Equal(CameraKind.Onvif, cam.Kind);
        Assert.Equal("rtsp://10.0.0.5:554/live", cam.Source);
        Assert.Equal("10.0.0.5", cam.Name.Split(' ')[^1]);
        Assert.Equal(2, fake.Posts);
    }

    sealed class FakeOnvif : IOnvifTransport
    {
        public int Posts;
        public Task<IReadOnlyList<string>> ProbeAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<string>>(["<d:XAddrs>http://10.0.0.5/onvif/device_service</d:XAddrs>"]);
        public Task<string> PostAsync(string url, string soapAction, string body, CancellationToken ct)
        {
            Posts++;
            Assert.Equal("http://10.0.0.5/onvif/device_service", url);
            if (soapAction.EndsWith("GetProfiles", StringComparison.Ordinal)) return Task.FromResult("<trt:Profiles token=\"P1\"></trt:Profiles>");
            Assert.Contains("P1", body);
            return Task.FromResult("<tt:Uri>rtsp://10.0.0.5:554/live</tt:Uri>");
        }
    }
}

public class CORE09_RobotDrive
{
    [Fact(DisplayName = "CORE-09: Simulation und Trockenlauf senden nichts, Echt braucht Freigabe")]
    public void Modes()
    {
        using var f = new Fixture();
        var link = new MemoryMotionLink();
        var drive = new RobotDrive { Link = link };
        var pose = new Joints(10, 20, 30);
        var cmd = MotionText.Move(pose);
        var sim = drive.Execute(cmd, pose, null, null);
        Assert.False(sim.Sent);
        Assert.Contains("Simulation", sim.Detail);
        Assert.Equal(pose, drive.Pose);
        Assert.Empty(link.Sent);

        drive.Mode = DriveMode.DryRun;
        var dry = drive.Execute(cmd, null, null, null);
        Assert.False(dry.Sent);
        Assert.Contains("nicht gesendet", dry.Detail);
        Assert.Contains(cmd, dry.Detail);
        Assert.Empty(link.Sent);

        drive.Mode = DriveMode.Live;
        Assert.Throws<InvalidOperationException>(() => new RobotDrive { Mode = DriveMode.Live }.Execute(cmd, pose, f.Core.Approvals, "x"));
        drive.Link = link;
        Assert.Throws<ApprovalException>(() => drive.Execute(cmd, pose, null, null));
        Assert.Empty(link.Sent);
        var req = f.Core.Approvals.Request(Principal.LocalOwner, MotionText.Action, "Arm", cmd);
        Assert.Throws<ApprovalException>(() => drive.Execute(cmd, pose, f.Core.Approvals, req.Id));
        f.Core.Approvals.Approve(req.Id, Principal.LocalOwner);
        var sent = drive.Execute(cmd, pose, f.Core.Approvals, req.Id);
        Assert.True(sent.Sent);
        Assert.Equal(cmd, Assert.Single(link.Sent));

        var other = f.Core.Approvals.Request(Principal.LocalOwner, MotionText.Action, "Arm", "MOVE 1 2 3");
        f.Core.Approvals.Approve(other.Id, Principal.LocalOwner);
        Assert.Throws<ApprovalException>(() => drive.Execute(cmd, pose, f.Core.Approvals, other.Id));
        Assert.Single(link.Sent);
    }

    [Fact(DisplayName = "CORE-09: Not-Aus sendet STOP und blockiert die nächste Fahrt")]
    public void Estop()
    {
        using var f = new Fixture();
        var link = new MemoryMotionLink();
        var drive = new RobotDrive { Mode = DriveMode.Live, Link = link };
        drive.EmergencyStop();
        Assert.Equal(MotionText.Stop, Assert.Single(link.Sent));
        var req = f.Core.Approvals.Request(Principal.LocalOwner, MotionText.Action, "Arm", "MOVE 1 2 3");
        f.Core.Approvals.Approve(req.Id, Principal.LocalOwner);
        Assert.Throws<InvalidOperationException>(() => drive.Execute("MOVE 1 2 3", null, f.Core.Approvals, req.Id));
        Assert.Single(link.Sent);
        drive.ReleaseEmergency();
        Assert.False(drive.Emergency);
        Assert.Throws<InvalidOperationException>(() => drive.ReleaseEmergency());
        var sim = new RobotDrive();
        sim.EmergencyStop();
        Assert.Empty(new MemoryMotionLink().Sent);
        Assert.Throws<InvalidOperationException>(() => sim.Execute("MOVE 0 0 0", null, null, null));
    }

    [Fact(DisplayName = "CORE-09: Geräteadresse ist Rechner und Port")]
    public void Endpoint()
    {
        Assert.False(MotionEndpoint.TryParse("keine-port", out _, out _));
        Assert.False(MotionEndpoint.TryParse("rtsp://10.0.0.1/x", out _, out _));
        Assert.True(MotionEndpoint.TryParse("127.0.0.1:9", out var host, out var port));
        Assert.Equal(("127.0.0.1", 9), (host, port));
    }

    [Fact(DisplayName = "CORE-09: Mehrere Zeilen gehen nach einer Freigabe an das Gerät")]
    public void ProgramLines()
    {
        using var f = new Fixture();
        var link = new MemoryMotionLink();
        var drive = new RobotDrive { Mode = DriveMode.Live, Link = link };
        var cmd = "MOVE 1 2 3\nGRIP CLOSE";
        var req = f.Core.Approvals.Request(Principal.LocalOwner, MotionText.Action, "Weg", cmd);
        f.Core.Approvals.Approve(req.Id, Principal.LocalOwner);
        var report = drive.Execute(cmd, new Joints(1, 2, 3), f.Core.Approvals, req.Id);
        Assert.True(report.Sent);
        Assert.Equal(["MOVE 1 2 3", "GRIP CLOSE"], link.Sent);
    }
}
