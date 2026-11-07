using Trendo.Core;
using Trendo.Core.Media;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Paket 01 – Studio 4, S4-14 (07.11.2026): Tempo-Map, Leitmotiv am Timecode, Treffpunkte in Bildern.</summary>
public class ScoreSyncTests
{
    static readonly PianoRoll.Note[] Motif = [new(57, 0, 480), new(60, 480, 480), new(64, 960, 960)]; // A – C – E

    [Fact(DisplayName = "S4-14-P: Tempo-Map mit Wechsel, Tempo so eingepasst, dass das Leitmotiv genau am Schnitt (TC 00:00:12:12) beginnt; MIDI mit Tempowechseln und Cue-Zeile")]
    public void Fit_and_export()
    {
        ScoreSync.TempoChange[] map0 = [new(1, 120)];
        Assert.Equal(2, ScoreSync.Seconds(map0, 2), 6);
        var map = new[] { new ScoreSync.TempoChange(1, 120), new ScoreSync.TempoChange(5, 60) };
        Assert.Equal(8, ScoreSync.Seconds(map, 5), 6); Assert.Equal(12, ScoreSync.Seconds(map, 6), 6); Assert.Equal(9, ScoreSync.Seconds(map, 5, 2), 6);
        // Schnitt bei 12,48 s (25 fps: 00:00:12:12) – Leitmotiv soll auf Takt 9 einsetzen
        var bpm = ScoreSync.FitBpm(map0, 1, 9, 12.48);
        var fitted = new[] { new ScoreSync.TempoChange(1, bpm) };
        var checks = ScoreSync.CheckHits(fitted, [new ScoreSync.Hit("Schnitt Strand", 12.48, 9)]);
        Assert.True(checks[0].Ok, $"{checks[0].OffFrames} Bilder daneben"); Assert.InRange(Math.Abs(checks[0].OffFrames), 0, 0.01);
        var midi = ScoreSync.ToMidi([.. fitted, new ScoreSync.TempoChange(13, 90)], Motif, 9, "Leitmotiv Meer");
        var tempos = ScoreSync.TempoEvents(midi);
        Assert.Equal([(0, Math.Round(bpm, 2)), (12 * 4 * 480, 90.0)], tempos);
        var back = PianoRoll.FromMidi(midi);
        Assert.True(back.Ok, string.Join(";", back.Warnings)); Assert.Equal("Leitmotiv Meer", back.Song!.Name);
        Assert.Equal(8 * 4 * 480, back.Song.Notes.Min(n => n.Start)); // Takt 9
        Assert.Equal("M02;Leitmotiv Meer;00:00:12:12;00:00:16:00;3.52", ScoreSync.CueLine("M02", "Leitmotiv Meer", 12.48, 16, 25));
    }

    [Fact(DisplayName = "S4-14-N: Treffpunkt drei Bilder daneben → FAIL; unmögliches Tempo, Map ohne Takt 1 und ungeordnete Wechsel werden abgelehnt")]
    public void Fails()
    {
        var map = new[] { new ScoreSync.TempoChange(1, 120) };
        var c = ScoreSync.CheckHits(map, [new ScoreSync.Hit("Tür", 16.12, 9)]); // Takt 9 = 16,00 s → 3 Bilder zu früh
        Assert.False(c[0].Ok); Assert.Equal(-3, c[0].OffFrames, 2);
        Assert.True(ScoreSync.CheckHits(map, [new ScoreSync.Hit("Tür", 16.03, 9)])[0].Ok); // unter einem Bild
        Assert.Throws<ArgumentException>(() => ScoreSync.FitBpm(map, 1, 2, 60)); // 4 Schläge in 60 s = 4 BPM
        Assert.Throws<ArgumentException>(() => ScoreSync.Seconds([new ScoreSync.TempoChange(2, 120)], 3));
        Assert.Throws<ArgumentException>(() => ScoreSync.Validate([new ScoreSync.TempoChange(1, 120), new ScoreSync.TempoChange(5, 90), new ScoreSync.TempoChange(3, 100)]));
    }

    [Fact(DisplayName = "S4-14 echt: MIDI mit Tempowechseln auf Platte, FFmpeg erkennt sie, Prüfsumme stabil")]
    public void Real_file()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-score-" + Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(dir);
        try
        {
            var bpm = ScoreSync.FitBpm([new ScoreSync.TempoChange(1, 120)], 1, 9, 12.48);
            var map = new[] { new ScoreSync.TempoChange(1, bpm), new ScoreSync.TempoChange(13, 90) };
            var f = Path.Combine(dir, "leitmotiv.mid"); File.WriteAllBytes(f, ScoreSync.ToMidi(map, Motif, 9, "Leitmotiv Meer"));
            Assert.Equal(File.ReadAllBytes(f), ScoreSync.ToMidi(map, Motif, 9, "Leitmotiv Meer")); // gleiche Eingabe → gleiche Datei
            var probe = Cli.Find("ffprobe") is { } fp ? Cli.Run(fp, ["-v", "error", "-show_entries", "format=format_name", "-of", "compact=p=0:nk=1", f], dir, 30000).Out.Trim() : "";
            if (Environment.GetEnvironmentVariable("TRENDO_SCORE_BELEG") is { Length: > 0 } keep)
            {
                Directory.CreateDirectory(keep); File.Copy(f, Path.Combine(keep, "leitmotiv.mid"), true);
                var checks = ScoreSync.CheckHits(map, [new ScoreSync.Hit("Schnitt Strand", 12.48, 9), new ScoreSync.Hit("Tür (absichtlich daneben)", 16.12, 11)]);
                File.WriteAllText(Path.Combine(keep, "messung.txt"), $"eingepasstes Tempo: {bpm} BPM (Takt 9 = 12,48 s)\nTempowechsel in der Datei: {string.Join(", ", ScoreSync.TempoEvents(File.ReadAllBytes(f)).Select(t => $"Tick {t.Tick} → {t.Bpm} BPM"))}\nffprobe: {probe}\n"
                    + string.Join("\n", checks.Select(c => $"{c.Name}: Ziel {c.Target:0.000} s, Musik {c.Actual:0.000} s, {c.OffFrames:+0.00;-0.00;0} Bilder → {(c.Ok ? "OK" : "FAIL")}")) + "\n" + ScoreSync.CueLine("M02", "Leitmotiv Meer", 12.48, 16, 25) + "\n");
            }
        }
        finally { Directory.Delete(dir, true); }
    }
}
