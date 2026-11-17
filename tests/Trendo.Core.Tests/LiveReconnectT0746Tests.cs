using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0746: Sendeweg nach dem Echtlauf mit lokalem Empfänger – Fortschritt (Zeit, Bitrate), verständliche Meldung bei Verbindungsabbruch,
/// Neuverbinden (höchstens 3-mal, 2/4/8 s, eigene Kopie je Verbindung), Stopp beendet auch die Wiederholung.
/// Die FFmpeg-Zeilen stammen wörtlich aus dem Probelauf mit FFmpeg 9.0 auf 127.0.0.1 (Empfänger beendet bzw. nicht da).
/// Hier ist der Prozess ersetzt; den echten Lauf belegt <c>T0746_StreamTv_Echt_Ui</c>.</summary>
public class T0746_LiveReconnect
{
    const string Progress = "frame=  301 fps= 30 q=-1.0 size=    2048KiB time=00:00:10.03 bitrate=1672.4kbits/s speed=1.00x\r"
        + "frame=  362 fps= 30 q=-1.0 size=    2560KiB time=00:00:12.06 bitrate=1738.9kbits/s speed=1.00x";
    const string Dropped = "[vost#0:0/libx264 @ 000001f7b56fcac0] Error submitting a packet to the muxer: Error number -10054 occurred\n"
        + "[out#0/flv @ 000001f7b3856fc0] Error muxing a packet\n[out#0/flv @ 000001f7b3856fc0] Error writing trailer: Error number -10054 occurred";
    const string Refused = "[tcp @ 0000023e128cf940] Connection to tcp://127.0.0.1:19352?tcp_nodelay=0 failed: Error number -138 occurred\n"
        + "[rtmp @ 0000023e128cf400] Cannot open connection tcp://127.0.0.1:19352?tcp_nodelay=0";

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
        public ICaptureProcess Start(string ffmpeg, IReadOnlyList<string> arguments) { Args.Add(arguments.ToList()); var c = new Cap(); All.Add(c); return c; }
    }

    static CaptureChoice Screen() => new() { Os = CaptureOs.Windows, Video = VideoKind.Screen, Fps = 30, Width = 1280, Height = 720, VideoKbps = 2500, AudioKbps = 160 };

    static (LiveSession S, Factory F, Func<DateTime> Now, Action<double> Wait, Fixture Fx) Live(string? copyName)
    {
        var fx = new Fixture();
        var copy = copyName is null ? null : Path.Combine(fx.Root, copyName);
        var f = new Factory();
        var t = new DateTime(2026, 11, 17, 12, 0, 0, DateTimeKind.Utc);
        var s = new LiveSession(f) { Clock = () => t };
        StreamKeys.Save(fx.Core.Secrets, LivePlatform.Custom, "probe-schluessel");
        var req = LiveBroadcast.Ask(fx.Core.Approvals, Principal.LocalOwner, LivePlatform.Custom, "rtmp://127.0.0.1:19350/live", "Bildschirm");
        fx.Core.Approvals.Approve(req.Id, Principal.LocalOwner);
        LiveBroadcast.Go(fx.Core.Approvals, req.Id, s, LivePlatform.Custom, "rtmp://127.0.0.1:19350/live", fx.Core.Secrets, "ffmpeg", Screen(), Path.Combine(fx.Root, "v.jpg"), copy, false);
        return (s, f, () => t, sec => t = t.AddSeconds(sec), fx);
    }

    [Fact(DisplayName = "T-0746 Live: Zeit und Bitrate aus den Statuszeilen von FFmpeg")]
    public void Stats_from_ffmpeg()
    {
        Assert.Equal((TimeSpan.FromSeconds(12), 1739), LiveStats.Parse(Progress));
        Assert.Equal("0:12 · 1739 kbit/s", LiveStats.Line(Progress));
        Assert.Null(LiveStats.Line("Input #0, gdigrab, from 'desktop':"));
        Assert.Equal("1:02:03 · 2500 kbit/s", LiveStats.Line("time=01:02:03.50 bitrate=2500.0kbits/s"));
        Assert.True(LiveStats.ConnectionLost(Dropped));
        Assert.True(LiveStats.ConnectionLost(Refused));
        Assert.False(LiveStats.ConnectionLost("Could not find video device with name [Kamera] among source devices of type video."));
    }

    [Fact(DisplayName = "T-0746 Live: Status zeigt gesendete Zeit und Bitrate, ohne Schlüssel")]
    public void Status_shows_progress()
    {
        var (s, f, _, _, fx) = Live(null);
        using var _f = fx;
        f.All[0].Tail = Progress;
        Assert.Equal("Live. Mit Stopp beenden. – 0:12 · 1739 kbit/s", s.StatusLine());
        Assert.Equal("0:12 · 1739 kbit/s", s.Stats);
        s.Stop();
        Assert.Null(s.Stats);
    }

    [Fact(DisplayName = "T-0746 Live: Abbruch nach laufender Sendung → bis zu 3 Neuverbindungen (2/4/8 s), eigene Kopie je Verbindung, dann klare Meldung")]
    public void Reconnects_then_gives_up()
    {
        var (s, f, now, wait, fx) = Live("aufnahme-probe.mp4");
        using var _f = fx;
        for (var n = 1; n <= LiveSession.MaxRetries; n++)
        {
            wait(10); // Verbindung stand 10 s
            f.All[^1].Tail = Progress + "\n" + Dropped; f.All[^1].Running = false;
            s.Tick();
            Assert.True(s.Retrying);
            Assert.Equal(LiveMode.Broadcasting, s.Mode);
            Assert.Contains($"Neuer Versuch in {2 << (n - 1)} s ({n} von 3)", s.StatusLine());
            Assert.DoesNotContain("-10054", s.StatusLine());
            var before = f.Args.Count;
            wait((2 << (n - 1)) - 1); s.Tick();
            Assert.Equal(before, f.Args.Count); // noch nicht
            wait(1); s.Tick();
            Assert.Equal(before + 1, f.Args.Count);
            Assert.False(s.Retrying);
            Assert.Contains($"Wieder verbunden (Versuch {n} von 3)", s.StatusLine());
            Assert.Contains(f.Args[^1], a => a == "rtmp://127.0.0.1:19350/live/probe-schluessel"); // dasselbe freigegebene Ziel
            Assert.Contains(f.Args[^1], a => a.EndsWith($"aufnahme-probe-teil{n + 1}.mp4", StringComparison.Ordinal));
            Assert.DoesNotContain(f.Args[^1], a => a.EndsWith("aufnahme-probe.mp4", StringComparison.Ordinal)); // erste Kopie bleibt unberührt
        }
        Assert.Equal(4, s.CopyFiles.Count);
        foreach (var c in s.CopyFiles) File.WriteAllBytes(c, [1, 2, 3]); // FFmpeg hätte geschrieben
        wait(10);
        f.All[^1].Tail = Dropped; f.All[^1].Running = false;
        s.Tick();
        Assert.Equal(LiveMode.Idle, s.Mode);
        Assert.Contains("nach 3 Versuchen nicht wieder", s.Status);
        Assert.Contains("Kopie auf diesem PC", s.Status);
        Assert.Contains("-10054", s.LastLog);
        Assert.Equal(4, f.Args.Count);
    }

    /// <summary>Echtlauf 17.11.2026: Mit „Kopie auf diesem PC speichern“ startete FFmpeg nicht – „[vout]“ war Stream und Kopie zugeordnet.</summary>
    [Theory(DisplayName = "T-0746 Live: Stream mit Kopie – jeder Filterausgang genau einmal zugeordnet und im Filtergraph definiert (einfach, Szene, Tonmischer)")]
    [InlineData("einfach")]
    [InlineData("szene")]
    [InlineData("mischer")]
    public void Copy_uses_own_outputs(string kind)
    {
        var c = Screen();
        if (kind != "einfach") c = c with { Scene = new LiveScene("s1", "Szene 1", VideoKind.Screen, []) };
        if (kind == "mischer") c = c with { Microphone = true, Mic = "Tischmikrofon", Mix = new LiveMix(MicDb: -3) };
        foreach (var copy in new[] { null, @"C:\kopie.mp4" })
        {
            var a = LiveFfmpeg.Args(LiveOutput.Broadcast, c, "v.jpg", copy, "rtmp://127.0.0.1:1935/live/probe");
            var graph = a[a.IndexOf("-filter_complex") + 1];
            var mapped = a.Select((x, i) => (x, i)).Where(t => t.i > 0 && a[t.i - 1] == "-map" && t.x.StartsWith('[')).Select(t => t.x).ToList();
            Assert.Equal(mapped.Count, mapped.Distinct().Count());
            Assert.All(mapped, l => Assert.Contains(l, graph));
            Assert.Equal(copy is null ? 2 : 3, a.Count(x => x == "-c:v") + 1); // Vorschau ohne Encoder, Stream (+ Kopie) mit libx264
            if (kind == "mischer" && copy is not null) { Assert.Contains("[alive]", mapped); Assert.Contains("[acopy]", mapped); }
        }
    }

    [Fact(DisplayName = "T-0746 Live: Stream bricht ab, FFmpeg läuft wegen Vorschau und Kopie weiter → erkannt, beendet, neu verbunden")]
    public void Dropped_while_process_keeps_running()
    {
        var (s, f, _, wait, fx) = Live("aufnahme-probe.mp4");
        using var _f = fx;
        wait(10);
        f.All[0].Tail = Progress;
        s.Tick();
        Assert.False(f.All[0].Stopped); // läuft normal
        f.All[0].Tail = Progress + "\n" + Dropped; // Running bleibt true: FFmpeg schreibt Vorschau und Kopie weiter
        s.Tick();
        Assert.True(f.All[0].Stopped);
        Assert.True(s.Retrying);
        Assert.Contains("Neuer Versuch in 2 s", s.StatusLine());
        wait(2); s.Tick();
        Assert.Equal(2, f.Args.Count);
        Assert.Equal(LiveMode.Broadcasting, s.Mode);
    }

    [Fact(DisplayName = "T-0746 TV: Aufnahme ohne Programmführer heißt nicht „Sender – Sender“")]
    public void Tv_recording_name_without_guide()
    {
        var start = new DateTime(2026, 11, 17, 21, 10, 0, DateTimeKind.Utc);
        var plain = new Trendo.Core.Tv.TvRecording("r1", "q:0", "Trendo Teststream", "Trendo Teststream", start, start.AddHours(1));
        var f = Path.GetFileName(Trendo.Core.Tv.TvLibrary.RecordingFile(Path.GetTempPath(), plain));
        Assert.EndsWith(" Trendo Teststream.ts", f);
        Assert.DoesNotContain("–", f);
        var show = plain with { Title = "Abendnachrichten" };
        Assert.EndsWith(" Trendo Teststream – Abendnachrichten.ts", Path.GetFileName(Trendo.Core.Tv.TvLibrary.RecordingFile(Path.GetTempPath(), show)));
    }

    [Fact(DisplayName = "T-0746 Live: Server von Anfang an nicht erreichbar → kein Neuverbinden, verständliche Meldung")]
    public void Refused_at_start_no_retry()
    {
        var (s, f, _, wait, fx) = Live(null);
        using var _f = fx;
        wait(1);
        f.All[0].Tail = Refused; f.All[0].Running = false;
        s.Tick();
        Assert.Equal(LiveMode.Idle, s.Mode);
        Assert.False(s.Retrying);
        Assert.Single(f.Args);
        Assert.Contains("Der Sendeserver ist nicht erreichbar", s.Status);
        Assert.DoesNotContain("-138", s.Status);
    }

    [Fact(DisplayName = "T-0746 Live: Stopp während des Wartens beendet auch das Neuverbinden; Gerätefehler wird nicht wiederholt")]
    public void Stop_cancels_retry()
    {
        var (s, f, _, wait, fx) = Live(null);
        using var _f = fx;
        wait(20);
        f.All[0].Tail = Dropped; f.All[0].Running = false;
        s.Tick();
        Assert.True(s.Retrying);
        s.Stop();
        Assert.Equal("Gestoppt.", s.Status);
        Assert.Equal(LiveMode.Idle, s.Mode);
        wait(30); s.Tick();
        Assert.Single(f.Args);

        // Aufnahme (kein Stream) mit Gerätefehler: kein Neuverbinden, Rohzeile bleibt sichtbar wie bisher
        s.StartRecording("ffmpeg", Screen(), Path.Combine(fx.Root, "v.jpg"), Path.Combine(fx.Root, "a.mp4"));
        wait(20);
        f.All[^1].Tail = "Could not find video device"; f.All[^1].Running = false;
        s.Tick();
        Assert.Equal(LiveMode.Idle, s.Mode);
        Assert.Contains("FFmpeg hat aufgehört. Could not find video device", s.Status);
        Assert.Equal(2, f.Args.Count);
    }
}
