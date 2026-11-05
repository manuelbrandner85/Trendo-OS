using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Trendo Musik Profi 26 (05.11.2026): mitlaufender Liedtext, Tonart- und Tempo-Raster, Fassungen.</summary>
public class SongFollowTests
{
    [Fact(DisplayName = "Mitlaufender Liedtext: ohne Messung geschätzt (Vorspiel frei, Reihenfolge, Abschnitte), gemessene Zeiten haben Vorrang; laufende Zeile zur Zeit")]
    public void Follow()
    {
        var lyr = "[Strophe]\nDie Straße glänzt im Abendlicht\nwir fahren los\n\n[Refrain]\nSommer, nimm uns mit\nSommer, nimm uns mit";
        var p = SongFollow.Plan(lyr, 100);
        Assert.Equal(4, p.Count); Assert.All(p, l => Assert.False(l.Timed));
        Assert.True(p[0].Start >= 7.9 && p[^1].End <= 94.1, $"{p[0].Start} … {p[^1].End}");
        for (var i = 1; i < p.Count; i++) Assert.True(p[i].Start >= p[i - 1].End, "überlappt");
        Assert.Equal(("Strophe", "Refrain"), (p[0].Part, p[2].Part));
        Assert.True(p[0].End - p[0].Start > p[1].End - p[1].Start); // längere Zeile = mehr Zeit
        Assert.Equal(-1, SongFollow.At(p, 2)); Assert.Equal(0, SongFollow.At(p, p[0].Start + 0.1)); Assert.Equal(3, SongFollow.At(p, 99));
        Assert.Empty(SongFollow.Plan("", 100)); Assert.Empty(SongFollow.Plan("[Instrumental]", 100));
        var u = new SongUnderstanding.Result(120, 100, [], [], [], [new("Gemessen eins", "Strophe", 12, 15, "", true), new("ohne Zeit", "Strophe", 0, 0, "", false), new("Gemessen zwei", "Refrain", 20, 23, "", true)], null, "test");
        var m = SongFollow.Plan(lyr, 100, u);
        Assert.Equal(2, m.Count); Assert.All(m, l => Assert.True(l.Timed)); Assert.Equal(20, m[1].Start);
    }

    [Fact(DisplayName = "Raster: Takte aus dem Tempo (120 BPM, 4/4 = 2 s je Takt) oder aus gemessenen Schlägen (3/4); Tonleitern deutsch mit H; Paralleltonart")]
    public void Grid()
    {
        var b = SongFollow.Bars(120, "4/4", 10);
        Assert.Equal(5, b.Count); Assert.Equal((1, 0.0, 2.0), (b[0].Number, b[0].Start, b[0].End)); Assert.Equal(8, b[4].Start);
        var beats = Enumerable.Range(0, 12).Select(i => 1 + i * 0.5).ToList();
        var w = SongFollow.Bars(0, "3/4", 8, beats);
        Assert.Equal(4, w.Count); Assert.Equal(1, w[0].Start); Assert.Equal(2.5, w[1].Start);
        Assert.Empty(SongFollow.Bars(0, "4/4", 10));
        Assert.Equal(["A", "H", "C", "D", "E", "F", "G"], SongFollow.Scale("A minor"));
        Assert.Equal(["Es", "F", "G", "As", "B", "C", "D"], SongFollow.Scale("Eb major"));
        Assert.Empty(SongFollow.Scale("Quatsch")); Assert.Empty(SongFollow.Scale(null));
        Assert.Equal("C major", SongFollow.Relative("A minor")); Assert.Equal("A minor", SongFollow.Relative("C major"));
    }

    [Fact(DisplayName = "Fassungen: Ausgangslied zuerst, Cover und Abschnitt (auch vom Cover abgeleitet) gehören dazu, getrennte Spuren und fremde Lieder nicht")]
    public void Versions()
    {
        var t = DateTimeOffset.Now;
        var o = new MusicSong { Id = "o", Kind = "neu", Created = t };
        var c = new MusicSong { Id = "c", Kind = "cover", ParentId = "o", Created = t.AddMinutes(1) };
        var a = new MusicSong { Id = "a", Kind = "abschnitt", ParentId = "c", Created = t.AddMinutes(2) };
        var st = new MusicSong { Id = "s", Kind = "begleitung", ParentId = "o", Created = t.AddMinutes(3) };
        var x = new MusicSong { Id = "x", Kind = "neu", Created = t.AddMinutes(4) };
        var v = SongFollow.Versions([a, x, st, c, o], a);
        Assert.Equal(["o", "c", "a"], v.Select(s => s.Id));
        Assert.Single(SongFollow.Versions([a, x, st, c, o], x));
        Assert.Equal("Cover", SongFollow.KindLabel("cover")); Assert.Equal("Mit meiner Stimme", SongFollow.KindLabel("aufnahme"));
    }
}
