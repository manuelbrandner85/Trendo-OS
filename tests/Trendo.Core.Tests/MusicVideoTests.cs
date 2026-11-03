using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Trendo Musik – Musikvideo (V1/V4/V6/V8): Shotliste, Looks, Filter, Liedtext-Zeiten.</summary>
public class MusicVideoTests
{
    const string Abschnitte = """{"bpm": 120.0, "duration": 60.0, "sections": [{"start": 0.0, "end": 16.0, "energy": 0.4}, {"start": 16.0, "end": 32.0, "energy": 1.0}, {"start": 32.0, "end": 48.0, "energy": 1.5}, {"start": 48.0, "end": 60.0, "energy": 0.6}]}""";

    [Fact(DisplayName = "V1 Shotliste: deckt das ganze Lied ab, Schnitte auf Taktgrenzen, Refrain schneller geschnitten, Kamera passend zum Teil, keine Mini-Schnitte")]
    public void Plan_shots()
    {
        var a = MusicVideo.Parse(Abschnitte)!;
        Assert.Equal(["intro", "strophe", "refrain", "outro"], Enumerable.Range(0, 4).Select(i => MusicVideo.PartOf(a, i)));
        var shots = MusicVideo.Plan(a, "summer pop, beach");
        Assert.Equal(60, shots.Sum(s => s.Length), 3);
        Assert.All(shots, s => Assert.True(s.Length <= 5.0 + 1e-9 || s.Length <= 6.1, $"{s.Length}")); // Taktlänge 2 s
        Assert.All(shots, s => Assert.Equal(0, Math.Round(s.Start / 2.0 % 1, 6) % 1)); // Start auf Taktgrenze (2 s bei 120 BPM)
        var refrain = shots.Where(s => s.Part == "refrain").ToList(); var strophe = shots.Where(s => s.Part == "strophe").ToList();
        Assert.True(refrain.Average(s => s.Length) < strophe.Average(s => s.Length));
        Assert.Contains("aerial", shots[0].Camera);
        Assert.Contains("energetic", refrain[0].Prompt); Assert.Contains("summer pop, beach", refrain[0].Prompt);
        Assert.DoesNotContain(shots, s => s.Length < 1.5);
        Assert.Null(MusicVideo.Parse("kaputt"));
    }

    [Fact(DisplayName = "V4 Looks: gültige 33³-LUT je Look (Schwarzweiß ohne Farbe, Werte 0–1), Filter mit Look, Korn, Vignette und Breitbild-Balken nur im Querformat")]
    public void Looks_and_filter()
    {
        foreach (var (id, _) in MusicVideo.Looks)
        {
            var lines = MusicVideo.Cube(id).Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal("LUT_3D_SIZE 33", lines[1]);
            var vals = lines[2..];
            Assert.Equal(33 * 33 * 33, vals.Length);
            Assert.All(vals.Take(500), v => Assert.All(v.Split(' '), x => Assert.InRange(double.Parse(x, System.Globalization.CultureInfo.InvariantCulture), 0, 1)));
        }
        var sw = MusicVideo.Cube("sw").Split('\n')[2 + 33 * 33 * 33 / 2].Split(' ');
        Assert.Equal(sw[0], sw[1]); Assert.Equal(sw[1], sw[2]);
        var f = MusicVideo.Filter(1920, 1080, @"C:\t\warm.cube", letterbox: true);
        Assert.Contains("lut3d='C\\:/t/warm.cube'", f); Assert.Contains("noise=alls=5", f); Assert.Contains("vignette", f); Assert.Contains("drawbox=x=0:y=0:w=iw:h=138", f);
        Assert.DoesNotContain("drawbox", MusicVideo.Filter(1080, 1920, "x.cube", letterbox: true)); // 9:16 ohne Balken
        Assert.Contains("crop=1080:1920", MusicVideo.Filter(1080, 1920, "x.cube", letterbox: false));
        var j = MusicVideo.JoinArgs("l.txt", "song.mp3", "o.mp4");
        Assert.Contains("copy", j); Assert.Contains("-shortest", j);
        Assert.Equal("file 'C:/a b/it'\\''s.mp4'\n", MusicVideo.ConcatList([@"C:\a b\it's.mp4"]));
    }

    [Fact(DisplayName = "V6 Liedtext: Phrasen aus der Gesangsmelodie, Zeilen ohne Marken, gleich viele/zu viele/zu wenige Phrasen sauber zugeordnet, ASS mit Ein-/Ausblenden")]
    public void Lyrics_timing()
    {
        var m = new SingAlong.Melody(0.15, [null, 60, 60, 61, 62, 62, null, null, null, null, 64, 64, 65, null, 67, null, null, null, null, 60, 60, 60, 60, 60]);
        var ph = LyricTiming.Phrases(m);
        Assert.Equal(3, ph.Count); // die kurze Lücke bei 1,95 s trennt nicht, der einzelne Ton bei 2,1 s gehört zur zweiten Phrase
        var lines = LyricTiming.Lines("[Strophe]\nErste Zeile\n\nZweite Zeile\n[Refrain]\nDritte Zeile");
        Assert.Equal(["Erste Zeile", "Zweite Zeile", "Dritte Zeile"], lines);
        var al = LyricTiming.Align(lines, ph);
        Assert.Equal([0.15, 1.5, 2.85], al.Select(l => l.Start));
        Assert.Equal(2, LyricTiming.Align(lines.Take(2).ToList(), ph).Count); // mehr Phrasen als Zeilen → verbunden
        var more = LyricTiming.Align([.. lines, "Vierte", "Fünfte"], ph);
        Assert.Equal(5, more.Count); Assert.True(more.Zip(more.Skip(1)).All(p => p.First.Start < p.Second.Start));
        var ass = LyricTiming.Ass(al, 1920, 1080);
        Assert.Contains("PlayResX: 1920", ass); Assert.Contains("Dialogue: 0,0:00:00.15,0:00:01.15,Lied,,0,0,0,,{\\fad(180,220)}Erste Zeile", ass);
    }
}
