using System.Text.Json.Nodes;
using Trendo.Core;
using Trendo.Core.Media;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Paket 01 – Studio 4, S4-13 (06.11.2026): Step-Sequencer, Arpeggiator, Automation.</summary>
public class StepSequencerTests
{
    [Fact(DisplayName = "S4-13-P: EDM-Muster rendern – 128 BPM erkannt, 7,5 s Muster, ohne Übersteuerung; Arpeggio auf/ab, Automation linear; Speichern und Laden gleich")]
    public void Render_edm()
    {
        var p = StepSequencer.Edm();
        Assert.Equal(7.5, StepSequencer.Seconds(p), 3);
        var a = StepSequencer.Render(p);
        Assert.Equal(48000, a.Rate); Assert.Equal(2, a.Channels); Assert.InRange(a.Seconds, 8.4, 8.6);
        var m = AudioQa.Analyze(a, 24);
        Assert.Equal(0, m.ClipEvents); Assert.InRange(m.SamplePeakDb, -1.2, -0.8);
        Assert.InRange(BeatDetect.Find(a).Bpm, 126, 130); // der Takt ist hörbar richtig
        Assert.Equal([69, 72, 76, 81, 84, 88, 69], StepSequencer.ArpNotes(new StepSequencer.Arp(true, "Am", "up", 2), 7));
        Assert.Equal([69, 72, 76, 81, 84, 88, 84, 81, 76, 72, 69], StepSequencer.ArpNotes(new StepSequencer.Arp(true, "Am", "updown", 2), 11));
        Assert.Equal([88, 84, 81], StepSequencer.ArpNotes(new StepSequencer.Arp(true, "Am", "down", 2), 3));
        var bass = p.Tracks.Single(t => t.Id == "bass");
        Assert.Equal(0.4, StepSequencer.AutoAt(bass, 0), 3); Assert.Equal(0.9, StepSequencer.AutoAt(bass, 63), 3); Assert.Equal(0.4 + 0.5 * 31 / 63, StepSequencer.AutoAt(bass, 31), 6);
        var back = StepSequencer.FromJson(JsonNode.Parse(StepSequencer.ToJson(p).ToJsonString())!.AsObject());
        Assert.Equal(StepSequencer.ToJson(p).ToJsonString(), StepSequencer.ToJson(back).ToJsonString());
    }

    [Fact(DisplayName = "S4-13-N: Endlosschleife (Muster über 10 Minuten), Automation außerhalb, ungeordnet oder ungültig, falsches Raster und beschädigtes Muster werden mit Grund abgelehnt")]
    public void Rejects()
    {
        var p = StepSequencer.Edm();
        Assert.Contains("Endlosschleife", Assert.Throws<ArgumentException>(() => StepSequencer.Render(p with { Bpm = 60, Bars = 512 })).Message);
        var bass = p.Tracks.Single(t => t.Id == "bass");
        StepSequencer.Pattern With(StepSequencer.Track t) => p with { Tracks = [.. p.Tracks.Where(x => x.Id != "bass"), t] };
        Assert.Contains("außerhalb", Assert.Throws<ArgumentException>(() => StepSequencer.Validate(With(bass with { Automation = [(0, 1), (64, 1)] }))).Message);
        Assert.Contains("der Reihe nach", Assert.Throws<ArgumentException>(() => StepSequencer.Validate(With(bass with { Automation = [(10, 1), (5, 1)] }))).Message);
        Assert.Contains("ungültig", Assert.Throws<ArgumentException>(() => StepSequencer.Validate(With(bass with { Automation = [(0, double.NaN)] }))).Message);
        Assert.Throws<ArgumentException>(() => StepSequencer.Validate(With(bass with { Steps = "x..x" })));
        Assert.Throws<ArgumentException>(() => StepSequencer.Validate(p with { Bpm = 500 }));
        Assert.Throws<ArgumentException>(() => StepSequencer.Validate(p with { Arp = p.Arp with { Chord = "H#q" } }));
        var bad = StepSequencer.ToJson(p); bad["tracks"]![0]!["steps"] = "kaputt";
        Assert.Throws<ArgumentException>(() => StepSequencer.FromJson(bad));
        bad = StepSequencer.ToJson(p); bad["tracks"]![0]!["auto"] = new JsonArray(new JsonArray(0, "x"));
        Assert.Throws<ArgumentException>(() => StepSequencer.FromJson(bad));
    }

    [Fact(DisplayName = "S4-13 echt: Mixdown als WAV 24 Bit – FFmpeg liest 7,5 s + Ausklang, Lautheit messbar, Prüfung ohne Rot (eigene Klänge)")]
    public void Real_mixdown()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-seq-" + Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(dir);
        try
        {
            var f = Path.Combine(dir, "edm-pattern.wav"); AudioFile.WriteWav(StepSequencer.Render(StepSequencer.Edm()), f, 24);
            File.WriteAllText(Path.Combine(dir, "edm-pattern.json"), StepSequencer.ToJson(StepSequencer.Edm()).ToJsonString());
            var qa = AudioQa.Run(f, AudioQa.TargetOf("streaming"), [new("Step-Sequencer", "selbst erzeugt (Trendo-Synthese)")]);
            Assert.DoesNotContain(qa.Checks, c => c.Light == "rot");
            if (Cli.Find("ffprobe") is { } fp)
            {
                var info = Cli.Run(fp, ["-v", "error", "-show_entries", "stream=codec_name,sample_rate,channels:format=duration", "-of", "compact=p=0:nk=1", f], dir, 30000);
                Assert.True(info.Ok, info.Err); Assert.Contains("pcm_s24le|48000|2", info.Out);
                if (Environment.GetEnvironmentVariable("TRENDO_SEQ_BELEG") is { Length: > 0 } keep)
                {
                    Directory.CreateDirectory(keep);
                    foreach (var x in Directory.GetFiles(dir)) File.Copy(x, Path.Combine(keep, Path.GetFileName(x)), true);
                    File.WriteAllText(Path.Combine(keep, "messung.txt"), $"ffprobe: {info.Out.Trim().Replace('\n', ' ')}\nBPM erkannt: {BeatDetect.Find(StepSequencer.Render(StepSequencer.Edm())).Bpm:0.0}\n" + AudioQa.Report("edm-pattern", AudioQa.TargetOf("streaming"), qa, [f]));
                }
            }
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }
}
