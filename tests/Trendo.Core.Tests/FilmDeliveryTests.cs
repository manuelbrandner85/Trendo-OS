using System.Text.Json.Nodes;
using Trendo.Core;
using Trendo.Core.Media;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Paket 01 – Studio 3 (06.11.2026): Film-Auslieferung – EDL/OTIO (S3-02), Stems (S3-14), Cue-Liste (S3-15), Export mit Hash (S3-18), Prüfbericht (S3-20).</summary>
public class FilmDeliveryTests
{
    static (JsonObject P, string Dir) Project(bool touch = true)
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-film-" + Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(dir);
        string F(string n) { var f = Path.Combine(dir, n); if (touch) File.WriteAllBytes(f, [0]); return f; }
        var p = VideoEdit.NewProject(320, 180); p["fps"] = 25;
        VideoEdit.Add(p, VideoEdit.NewClip(F("szene1.mp4"), 2, true)); VideoEdit.Add(p, VideoEdit.NewClip(F("szene2.mp4"), 2, true));
        VideoEdit.Add(p, VideoEdit.NewClip(F("thema.wav"), 3, true), "music"); VideoEdit.SetMusic(p, 0, 0.5, 0.8);
        return (p, dir);
    }

    [Fact(DisplayName = "S3-02/S3-15: EDL (CMX 3600) mit Timecodes, OTIO mit Bild- und Musikspur samt Lücke, Cue-Liste; Timecode HH:MM:SS:FF")]
    public void Interchange()
    {
        var (p, dir) = Project();
        try
        {
            Assert.Equal("00:00:02:12", FilmDelivery.Timecode(2.48, 25)); Assert.Equal("01:00:00:00", FilmDelivery.Timecode(3600, 25));
            var edl = FilmDelivery.Edl(p, "Unser Film");
            Assert.Contains("TITLE: Unser Film", edl); Assert.Contains("FCM: NON-DROP FRAME", edl);
            Assert.Contains("001  AX       AA/V  C         00:00:00:00 00:00:02:00 00:00:00:00 00:00:02:00", edl);
            Assert.Contains("002  AX       AA/V  C         00:00:00:00 00:00:02:00 00:00:02:00 00:00:04:00", edl);
            Assert.Contains("003  AX       A2    C         00:00:00:00 00:00:03:00 00:00:00:13 00:00:03:13", edl);
            var otio = JsonNode.Parse(FilmDelivery.Otio(p, "Unser Film"))!;
            Assert.Equal("Timeline.1", (string)otio["OTIO_SCHEMA"]!);
            var tracks = otio["tracks"]!["children"]!.AsArray();
            Assert.Equal(2, tracks[0]!["children"]!.AsArray().Count);
            Assert.Equal("Gap.1", (string)tracks[1]!["children"]![0]!["OTIO_SCHEMA"]!); Assert.Equal(13, (double)tracks[1]!["children"]![0]!["source_range"]!["duration"]!["value"]!);
            var cue = FilmDelivery.CueSheet(p);
            Assert.Contains("M01;thema;00:00:00:13;00:00:03:13;3.00;thema.wav;80 %", cue);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "S3-14: Stems – Dialog ohne Musik, Musik mit stummem Bildton, beide WAV 24 Bit; Prüfung meldet fehlende Medien und asynchrone Stems")]
    public void Stems_and_qc()
    {
        var (p, dir) = Project();
        try
        {
            Assert.Empty(VideoEdit.Clips(FilmDelivery.StemProject(p, "dialog"), "music"));
            Assert.All(VideoEdit.Clips(FilmDelivery.StemProject(p, "musik")), c => Assert.Equal(0, c.Volume));
            var a = FilmDelivery.StemArgs(p, "dialog", "d.wav");
            Assert.Equal("d.wav", a[^1]); Assert.Contains("pcm_s24le", a); Assert.Contains("-vn", a); Assert.DoesNotContain("aac", a);
            Assert.Throws<ArgumentException>(() => FilmDelivery.StemProject(p, "effekte"));
            var ok = new FilmDelivery.Measure(4, 0, -14.5, -2);
            var qc = FilmDelivery.Qc(p, ok, new Dictionary<string, FilmDelivery.Measure> { ["Dialog"] = ok with { }, ["Musik"] = ok with { Duration = 3.2 } });
            Assert.Contains(qc, c => c.Light == "rot" && c.Text.Contains("Musik") && c.Text.Contains("nicht synchron"));
            Assert.Contains(qc, c => c.Light == "gruen" && c.Text.Contains("Dialog"));
            File.Delete(Path.Combine(dir, "szene2.mp4"));
            Assert.Contains(FilmDelivery.Qc(p, ok, new Dictionary<string, FilmDelivery.Measure>()), c => c.Light == "rot" && c.Text.Contains("szene2.mp4"));
            Assert.Throws<FileNotFoundException>(() => FilmDelivery.Deliver(_ => new Cli.Result(0, "", ""), p, Path.Combine(dir, "aus"), "Film")); // fehlendes Medium nie als Schwarzbild
            var m = FilmDelivery.ParseMeasure("Duration: 00:00:04.00, start\n[blackdetect] black_start:0 black_end:1.5 black_duration:1.5\n  Integrated loudness:\n    I:         -16.2 LUFS\n  True peak:\n    Peak:       -1.4 dBFS");
            Assert.Equal((4.0, 1.5, -16.2, -1.4), (m.Duration, m.BlackSeconds, m.Lufs, m.TruePeak));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "Echt mit FFmpeg: ganze Auslieferung – Film, Stems gleich lang wie der Film, EDL, OTIO, Cue-Liste, Prüfbericht mit SHA-256")]
    public void Real_delivery()
    {
        var ff = Cli.Find("ffmpeg"); if (ff is null) return;
        var dir = Path.Combine(Path.GetTempPath(), "trendo-film-" + Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(dir);
        try
        {
            string Make(string name, params string[] args) { var f = Path.Combine(dir, name); var r = Cli.Run(ff, ["-hide_banner", "-loglevel", "error", "-y", .. args, f], dir, 60000); Assert.True(File.Exists(f), r.Err); return f; }
            var a = Make("szene1.mp4", "-f", "lavfi", "-i", "testsrc2=s=320x180:r=25:d=2", "-f", "lavfi", "-i", "sine=f=330:d=2", "-shortest", "-pix_fmt", "yuv420p");
            var b = Make("szene2.mp4", "-f", "lavfi", "-i", "color=c=blue:s=320x180:r=25:d=2", "-f", "lavfi", "-i", "sine=f=550:d=2", "-shortest", "-pix_fmt", "yuv420p");
            var mu = Make("thema.wav", "-f", "lavfi", "-i", "sine=f=220:d=3");
            var p = VideoEdit.NewProject(320, 180); p["fps"] = 25;
            VideoEdit.Add(p, VideoEdit.NewClip(a, 2, true)); VideoEdit.Add(p, VideoEdit.NewClip(b, 2, true));
            VideoEdit.Add(p, VideoEdit.NewClip(mu, 3, true), "music"); VideoEdit.SetMusic(p, 0, 0.5, 0.5);
            var res = FilmDelivery.Deliver(args => Cli.Run(ff, args, dir, 120000), p, Path.Combine(dir, "Auslieferung"), "Unser Film", "FFmpeg (Test)");
            Assert.True(File.Exists(res.Film));
            Assert.Equal(6, res.Files.Count); Assert.All(res.Files, f => Assert.True(File.Exists(f) && new FileInfo(f).Length > 10, f));
            Assert.DoesNotContain(res.Checks, c => c.Light == "rot"); // Medien da, Stems synchron
            Assert.Contains(res.Checks, c => c.Text.Contains("Stem „Musik“ gleich lang"));
            var report = File.ReadAllText(res.Report);
            Assert.Contains("| Unser Film.mp4 |", report); Assert.Contains(FilmDelivery.Sha256(res.Film), report);
            Assert.Contains("00:00:02:00 00:00:04:00", File.ReadAllText(Path.Combine(res.Folder, "Unser Film.edl")));
            if (Environment.GetEnvironmentVariable("TRENDO_FILM_BELEG") is { Length: > 0 } keep) // Beleg für reports/paket01: Eingaben und Auslieferung behalten
            {
                Directory.CreateDirectory(keep);
                foreach (var f in Directory.GetFiles(dir).Concat(Directory.GetFiles(res.Folder))) File.Copy(f, Path.Combine(keep, Path.GetFileName(f)), true);
            }
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }
}
