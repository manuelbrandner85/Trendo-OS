using System.Reflection;
using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

public class SPEC15_LiveStream
{
    sealed class Cap : ICaptureProcess
    {
        public bool Running { get; set; } = true;
        public string Tail { get; set; } = "";
        public bool Stopped { get; private set; }
        public void Stop() { Stopped = true; Running = false; }
    }

    sealed class Factory : ICaptureProcessFactory
    {
        public List<IReadOnlyList<string>> Args { get; } = [];
        public List<Cap> All { get; } = [];
        public ICaptureProcess Start(string ffmpeg, IReadOnlyList<string> arguments)
        {
            Args.Add(arguments.ToList());
            var c = new Cap();
            All.Add(c);
            return c;
        }
    }

    sealed class Runner : IProcessRunner
    {
        public List<(string F, string A)> Calls { get; } = [];
        public Task<(int Exit, string Output)> RunAsync(string file, string args, Action<string>? line = null, CancellationToken ct = default)
        {
            Calls.Add((file, args));
            return Task.FromResult((0, "ffmpeg version 6.1\n"));
        }
    }

    static CaptureChoice Screen(CaptureOs os, bool mic = false) => new()
    {
        Os = os, Video = VideoKind.Screen, Microphone = mic, Mic = mic ? "Tischmikrofon" : null,
        Fps = 30, Width = 1280, Height = 720, VideoKbps = 2750, AudioKbps = 160, Display = ":0"
    };

    [Fact(DisplayName = "SPEC-15 Live: neue Sitzung ist ruhig und startet nichts")]
    public void Idle()
    {
        var s = new LiveSession(new Factory());
        Assert.Equal(LiveMode.Idle, s.Mode);
        s.Stop();
        Assert.Equal("Es läuft nichts.", s.Status);
        var method = typeof(LiveSession).GetMethod("BeginBroadcast", BindingFlags.Instance | BindingFlags.Public);
        Assert.Null(method);
        Assert.NotNull(typeof(LiveSession).GetMethod("BeginBroadcast", BindingFlags.Instance | BindingFlags.NonPublic));
    }

    [Fact(DisplayName = "SPEC-15 Live: Vorschau und Aufnahme bauen echte ffmpeg-Befehle, ohne RTMP")]
    public void Preview_and_record_commands()
    {
        var win = LiveFfmpeg.Args(LiveOutput.Preview, Screen(CaptureOs.Windows), @"C:\vorschau.jpg", null, null);
        Assert.Contains("gdigrab", win);
        Assert.Contains("desktop", win);
        Assert.Equal(@"C:\vorschau.jpg", win[^1]);
        Assert.Contains("-update", win);
        Assert.DoesNotContain(win, a => a.Contains("rtmp", StringComparison.OrdinalIgnoreCase));

        var linux = LiveFfmpeg.Args(LiveOutput.Record, Screen(CaptureOs.Linux, mic: true), "/tmp/v.jpg", "/tmp/aufnahme.mp4", null);
        Assert.Contains("x11grab", linux);
        Assert.Contains(":0.0", linux);
        Assert.Contains("pulse", linux);
        Assert.Contains("Tischmikrofon", linux);
        Assert.Contains("libx264", linux);
        Assert.Equal("/tmp/aufnahme.mp4", linux[^1]);
        Assert.DoesNotContain(linux, a => a.StartsWith("rtmp", StringComparison.OrdinalIgnoreCase));

        var cam = LiveFfmpeg.Args(LiveOutput.Preview, new CaptureChoice { Os = CaptureOs.Windows, Video = VideoKind.Camera, Device = "Integrierte Kamera", Fps = 30, Width = 1280, Height = 720, VideoKbps = 2750 }, "v.jpg", null, null);
        Assert.Contains("video=Integrierte Kamera", cam);
        var window = LiveFfmpeg.Args(LiveOutput.Preview, new CaptureChoice { Os = CaptureOs.Linux, Video = VideoKind.Window, Device = "Editor", WinX = 10, WinY = 20, WinW = 800, WinH = 600, Fps = 30, Width = 1280, Height = 720, VideoKbps = 2750, Display = ":0" }, "v.jpg", null, null);
        Assert.Contains("800x600", window);
        Assert.Contains(":0.0+10,20", window);
        Assert.Contains("title=Editor", LiveFfmpeg.Args(LiveOutput.Preview, new CaptureChoice { Os = CaptureOs.Windows, Video = VideoKind.Window, Device = "Editor", Fps = 30, Width = 1280, Height = 720, VideoKbps = 2750 }, "v.jpg", null, null));
    }

    [Fact(DisplayName = "SPEC-15 Live: Mikrofon nur bei Aufnahme und Sendung, nie in der Vorschau")]
    public void Mic_only_when_recording_or_streaming()
    {
        var withMic = Screen(CaptureOs.Linux, mic: true);
        var preview = LiveFfmpeg.Args(LiveOutput.Preview, withMic, "/tmp/v.jpg", null, null);
        Assert.DoesNotContain("pulse", preview);
        Assert.DoesNotContain("Tischmikrofon", preview);
        Assert.Contains("-an", preview);

        var record = LiveFfmpeg.Args(LiveOutput.Record, withMic, "/tmp/v.jpg", "/tmp/aufnahme.mp4", null);
        Assert.Contains("pulse", record);
        Assert.Contains("Tischmikrofon", record);

        var live = LiveFfmpeg.Args(LiveOutput.Broadcast, withMic, "/tmp/v.jpg", null, "rtmp://a.rtmp.youtube.com/live2/yt-test-key-1234");
        Assert.Contains("pulse", live);
        Assert.Contains("Tischmikrofon", live);
        Assert.DoesNotContain(live, a => a.Contains("anullsrc", StringComparison.Ordinal));

        var silent = LiveFfmpeg.Args(LiveOutput.Broadcast, Screen(CaptureOs.Windows), @"C:\v.jpg", null, "rtmp://live.twitch.tv/app/live-key-99");
        Assert.Contains("anullsrc=channel_layout=stereo:sample_rate=44100", silent);
        Assert.DoesNotContain(silent, a => a.StartsWith("audio=", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "SPEC-15 Live: Stream-Befehl enthält Plattformadresse, Schlüssel und Stopp-fähige Ausgabe")]
    public void Broadcast_command()
    {
        var rates = LiveFfmpeg.Rates(LivePlatform.YouTube, "720p30");
        var choice = Screen(CaptureOs.Windows, mic: true) with { VideoKbps = rates.VideoKbps, AudioKbps = rates.AudioKbps, Width = rates.W, Height = rates.H, Fps = rates.Fps };
        var url = Ingest.Url(LivePlatform.YouTube, null, "yt-test-key-1234");
        var args = LiveFfmpeg.Args(LiveOutput.Broadcast, choice, @"C:\v.jpg", @"C:\kopie.mp4", url);
        Assert.Contains("rtmp://a.rtmp.youtube.com/live2/yt-test-key-1234", args);
        Assert.Contains("audio=Tischmikrofon", args);
        Assert.Contains("libx264", args);
        Assert.Contains("zerolatency", args);
        Assert.Contains(rates.VideoKbps + "k", args);
        Assert.Equal(@"C:\kopie.mp4", args[^1]);
        Assert.Equal("rtmp://live.twitch.tv/app/live-key-99", Ingest.Url(LivePlatform.Twitch, null, "live-key-99"));
        Assert.Equal("rtmps://example.com/live/custom-key-99", Ingest.Url(LivePlatform.Custom, "rtmps://example.com/live", "custom-key-99"));
        Assert.Throws<ArgumentException>(() => Ingest.ParseBase("http://example.com/live"));
        Assert.Throws<ArgumentException>(() => Ingest.ParseBase("rtmp://secret@example.com/live"));
        Assert.Equal("youtube", Ingest.Target(LivePlatform.YouTube, null));
        Assert.Equal("rtmp:example.com", Ingest.Target(LivePlatform.Custom, "rtmp://example.com/live"));
    }

    [Fact(DisplayName = "SPEC-15 Live: Senden nur nach Freigabe, genau einmal, Schlüssel nicht im Protokoll")]
    public void Gate()
    {
        using var f = new Fixture();
        var factory = new Factory();
        var session = new LiveSession(factory);
        const string key = "yt-test-key-1234";
        StreamKeys.Save(f.Core.Secrets, LivePlatform.YouTube, key);
        StreamKeys.Save(f.Core.Secrets, LivePlatform.Twitch, "twitch-other-key");
        var choice = Screen(CaptureOs.Windows);
        Assert.Throws<ApprovalException>(() => LiveBroadcast.Go(f.Core.Approvals, "fehlt", session, LivePlatform.YouTube, null, f.Core.Secrets, "ffmpeg", choice, "v.jpg", null, false));
        var pending = LiveBroadcast.Ask(f.Core.Approvals, Principal.LocalOwner, LivePlatform.YouTube, null, "Bildschirm");
        Assert.Throws<ApprovalException>(() => LiveBroadcast.Go(f.Core.Approvals, pending.Id, session, LivePlatform.YouTube, null, f.Core.Secrets, "ffmpeg", choice, "v.jpg", null, false));
        f.Core.Approvals.Deny(pending.Id, Principal.LocalOwner);
        Assert.Throws<ApprovalException>(() => LiveBroadcast.Go(f.Core.Approvals, pending.Id, session, LivePlatform.YouTube, null, f.Core.Secrets, "ffmpeg", choice, "v.jpg", null, false));
        Assert.Empty(factory.Args);

        var ok = LiveBroadcast.Ask(f.Core.Approvals, Principal.LocalOwner, LivePlatform.YouTube, null, "Bildschirm");
        f.Core.Approvals.Approve(ok.Id, Principal.LocalOwner);
        Assert.Throws<OfflineException>(() => LiveBroadcast.Go(f.Core.Approvals, ok.Id, session, LivePlatform.YouTube, null, f.Core.Secrets, "ffmpeg", choice, "v.jpg", null, true));
        Assert.Throws<ApprovalException>(() => LiveBroadcast.Go(f.Core.Approvals, ok.Id, session, LivePlatform.Twitch, null, f.Core.Secrets, "ffmpeg", choice, "v.jpg", null, false));
        Assert.Empty(factory.Args);
        LiveBroadcast.Go(f.Core.Approvals, ok.Id, session, LivePlatform.YouTube, null, f.Core.Secrets, "ffmpeg", choice, Path.Combine(f.Root, "v.jpg"), null, false);
        Assert.Equal(LiveMode.Broadcasting, session.Mode);
        Assert.Contains(factory.Args[0], a => a.Contains(key, StringComparison.Ordinal));
        Assert.Throws<InvalidOperationException>(() => LiveBroadcast.Go(f.Core.Approvals, ok.Id, session, LivePlatform.YouTube, null, f.Core.Secrets, "ffmpeg", choice, "v.jpg", null, false));
        Assert.Single(factory.Args);
        var log = string.Join("\n", f.Core.Audit.ReadAll().Select(e => e.Actor + " " + e.Action + " " + e.Detail));
        Assert.DoesNotContain(key, log);
        Assert.DoesNotContain("twitch-other-key", log);
        Assert.Contains("live.start", log);
        factory.All[0].Tail = "failed " + key;
        factory.All[0].Running = false;
        session.NoteExit();
        Assert.DoesNotContain(key, session.Status);
        Assert.Contains("••••", session.Status);
        Assert.Equal(LiveMode.Idle, session.Mode);
        Assert.Throws<ApprovalException>(() => LiveBroadcast.Go(f.Core.Approvals, ok.Id, session, LivePlatform.YouTube, null, f.Core.Secrets, "ffmpeg", choice, "v.jpg", null, false));
        Assert.Single(factory.Args);
    }

    [Fact(DisplayName = "SPEC-15 Live: Schlüssel nur im Speicher, Aufnahme stoppt den Prozess")]
    public void Key_and_stop()
    {
        var store = new InMemorySecretStore();
        Assert.Throws<ArgumentException>(() => StreamKeys.Save(store, LivePlatform.YouTube, "ab"));
        Assert.Throws<ArgumentException>(() => StreamKeys.Save(store, LivePlatform.YouTube, "hat leer zeichen"));
        StreamKeys.Save(store, LivePlatform.YouTube, "yt-test-key-1234");
        Assert.True(StreamKeys.Saved(store, LivePlatform.YouTube));
        Assert.False(StreamKeys.Saved(store, LivePlatform.Twitch));
        var factory = new Factory();
        var session = new LiveSession(factory);
        var file = Path.Combine(Path.GetTempPath(), "trendo-live-" + Guid.NewGuid().ToString("N") + ".mp4");
        session.StartRecording("ffmpeg", Screen(CaptureOs.Linux), Path.Combine(Path.GetTempPath(), "v.jpg"), file);
        Assert.Equal(LiveMode.Recording, session.Mode);
        session.Stop();
        Assert.True(factory.All[0].Stopped);
        Assert.Equal(LiveMode.Idle, session.Status is "Gestoppt." ? LiveMode.Idle : session.Mode);
        Assert.Equal("Gestoppt.", session.Status);
        try { if (File.Exists(file)) File.Delete(file); } catch { /* Testdatei */ }
    }

    [Fact(DisplayName = "SPEC-15 Live: Gerätelisten aus ffmpeg-Text")]
    public void Device_lists()
    {
        var dshow = LiveDevices.ParseDshow("""
            [dshow @ 1] DirectShow video devices
            [dshow @ 1]  "Integrated Camera"
            [dshow @ 1]     Alternative name "@device_pnp"
            [dshow @ 1] DirectShow audio devices
            [dshow @ 1]  "Microphone (Realtek)"
            [dshow @ 1] "USB Cam" (video)
            """);
        Assert.Equal(["Integrated Camera", "USB Cam"], dshow.Cameras.Select(c => c.Id).ToArray());
        Assert.Equal(["Microphone (Realtek)"], dshow.Mics.Select(m => m.Id).ToArray());
        Assert.Equal("alsa_input.pci", Assert.Single(LiveDevices.ParsePulse("  alsa_input.pci [Eingang]\n  alsa_output.pci.monitor [Monitor of Speakers]\n")).Id);
        Assert.Equal("alsa_input.pci", Assert.Single(LiveDevices.ParsePactl("0\talsa_output.pci.monitor\tmod\tfmt\tIDLE\n1\talsa_input.pci\tmod\tfmt\tRUNNING\n")).Id);
        var av = LiveDevices.ParseAvfoundation("[AVFoundation indev @ 0x7f] AVFoundation video devices:\n[AVFoundation indev @ 0x7f] [0] FaceTime HD Camera\n[AVFoundation indev @ 0x7f] AVFoundation audio devices:\n[AVFoundation indev @ 0x7f] [0] MacBook Pro Microphone\n");
        Assert.Equal("0", av.Cameras[0].Id);
        Assert.Contains("FaceTime", av.Cameras[0].Label);
        Assert.Contains("MacBook", av.Mics[0].Label);
        var win = Assert.Single(LiveDevices.ParseWmctrl("0x0120000b  0 100 200 800 600 myhost Editor Titel\n"));
        Assert.Equal(("Editor Titel", 100, 200, 800, 600), (win.Title, win.X, win.Y, win.Width, win.Height));
        var xw = Assert.Single(LiveDevices.ParseXwininfo("     0x1400007 \"Rechner\": (\"gnome-calculator\" \"Gnome-calculator\")  320x400+100+80  +100+80\n"));
        Assert.Equal(("Rechner", 100, 80, 320, 400), (xw.Title, xw.X, xw.Y, xw.Width, xw.Height));
    }

    [Fact(DisplayName = "SPEC-15 Live: FFmpeg über die bestehende Prüfung, ohne selbst zu installieren")]
    public async Task Tool_check()
    {
        using var f = new Fixture();
        var runner = new Runner();
        var detect = Path.Combine(f.Root, "ffmpeg.exe");
        File.WriteAllText(detect, "x");
        var deps = new DependencyManager(f.Core.Audit, runner, p => p.Contains("WinGet", StringComparison.Ordinal) ? detect : p, path => path == detect)
        { ToolsFile = Path.Combine(f.Root, "tools.json") };
        var called = false;
        var ok = await FfmpegReady.Resolve(deps, _ => { called = true; return null; });
        Assert.False(called);
        Assert.Equal(ToolState.Working, ok.State);
        Assert.Equal(detect, ok.Path);

        var before = runner.Calls.Count;
        var missDeps = new DependencyManager(f.Core.Audit, runner, p => p, _ => false);
        var miss = await FfmpegReady.Resolve(missDeps, _ => null);
        Assert.Equal(ToolState.Missing, miss.State);
        Assert.Equal(before, runner.Calls.Count);

        var found = Path.Combine(f.Root, "ffmpeg-bin");
        File.WriteAllText(found, "x");
        var viaPath = await FfmpegReady.Resolve(missDeps, name => name == "ffmpeg" ? found : null);
        Assert.Equal(ToolState.Working, viaPath.State);
        Assert.Equal("-version", runner.Calls[^1].A);
        Assert.DoesNotContain(f.Core.Audit.ReadAll(), e => e.Action.Contains("install", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "SPEC-15 Live: fehlendes FFmpeg startet keinen Prozess")]
    public void Missing_ffmpeg()
    {
        var factory = new FfmpegCaptureFactory();
        var missing = Path.Combine(Path.GetTempPath(), "trendo-kein-ffmpeg-" + Guid.NewGuid().ToString("N"));
        Assert.Throws<FileNotFoundException>(() => factory.Start(missing, ["-version"]));
    }
}
