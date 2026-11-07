using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Trendo Musik wie Suno V10 (07.11.2026): Mashup und Sample.</summary>
public class MusicMashupTests
{
    [Fact(DisplayName = "Suno V10: Tonart als Dur-Tonklasse (Moll → Parallele), kürzester Weg, halbes/doppeltes Tempo; starke Änderung → gelb")]
    public void Match()
    {
        Assert.Equal(0, MusicMashup.MajorPc("A minor")); Assert.Equal(7, MusicMashup.MajorPc("G major")); Assert.Equal(1, MusicMashup.MajorPc("C# major")); Assert.Null(MusicMashup.MajorPc("H-Dur")); Assert.Null(MusicMashup.MajorPc(null));
        var p = MusicMashup.Match(100, "C major", 110, "D major");
        Assert.Equal(1.1, p.Tempo, 3); Assert.Equal(2, p.Semitones); Assert.Equal("gruen", p.Light);
        Assert.Equal(-5, MusicMashup.Match(120, "C major", 120, "G major").Semitones); // G ist 7 hoch = 5 tief
        Assert.Equal(1.0, MusicMashup.Match(70, null, 140, null).Tempo, 3);            // doppeltes Tempo passt
        Assert.Contains("Tonart unbekannt", MusicMashup.Match(70, null, 140, null).Text);
        Assert.Equal(0, MusicMashup.Match(100, "A minor", 100, "C major").Semitones);  // Parallele
        Assert.Equal(0.958, MusicMashup.Match(60, "C major", 115, "C major").Tempo, 3);  // 192 % → halbes Tempo 96 %
        var g = MusicMashup.Match(100, "C major", 140, "F# major"); Assert.Equal(1.4, g.Tempo, 3); Assert.Equal(-6, g.Semitones); Assert.Equal("gelb", g.Light);
        Assert.Throws<ArgumentException>(() => MusicMashup.Match(0, null, 120, null));
    }

    [Fact(DisplayName = "Suno V10: Mischbefehl mit Begleitung zuerst (deren Länge gilt); Sample-Ausschnitt 0,5–30 s im Lied")]
    public void Mix_and_sample()
    {
        var a = MusicMashup.MixArgs("v.wav", "b.wav", "o.wav", -3);
        Assert.Equal("b.wav", a[a.IndexOf("-i") + 1]); Assert.Contains("duration=first", string.Join(" ", a)); Assert.Contains("volume=0.708", string.Join(" ", a));
        Assert.Null(MusicMashup.SampleProblem(2, 8, 120));
        Assert.Contains("halbe Sekunde", MusicMashup.SampleProblem(2, 2.2, 120)); Assert.Contains("30 Sekunden", MusicMashup.SampleProblem(0, 40, 120)); Assert.Contains("außerhalb", MusicMashup.SampleProblem(100, 125, 120));
    }
}
