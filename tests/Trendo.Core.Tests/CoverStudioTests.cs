using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Cover-Werkstatt M1–M8 (04.11.2026): Suche, Bauplan, Auftrag, Vergleich mit Ampel, Selbstverbesserung, Epochen-Stimmen und -Klang.</summary>
public class CoverStudioTests
{
    const string Mb = """
    {"recordings":[
      {"id":"r2","title":"Mein Lied","length":185000,"first-release-date":"1999-01-01","artist-credit":[{"name":"Erika Muster"}],"releases":[{"title":"Best of"}]},
      {"id":"r1","title":"Mein Lied","length":184000,"first-release-date":"1961-05-02","artist-credit":[{"name":"Erika Muster"}],"releases":[{"title":"Erste Platte"}]},
      {"id":"r3","title":"Anderes","artist-credit":[]}
    ]}
    """;

    [Fact(DisplayName = "M1 Suche: Künstler und Titel getrennt gesucht, Wiederveröffentlichungen zusammengefasst (älteste bleibt), Album, Jahr, Länge, Seite – kein Liedtext")]
    public void Search()
    {
        Assert.Contains("artist%3A%22Erika%20Muster%22", CoverStudio.SearchUri("Erika Muster – Mein Lied").AbsoluteUri);
        var h = CoverStudio.ParseHits(Mb);
        Assert.Equal(2, h.Count);
        var first = h.First(x => x.Title == "Mein Lied");
        Assert.Equal((1961, "Erste Platte", 184.0), (first.Year, first.Album, first.Seconds));
        Assert.Equal("https://musicbrainz.org/recording/r1", first.Page);
        Assert.Empty(CoverStudio.ParseHits("kaputt"));
        Assert.Contains("fügst", CoverStudio.LyricsHint.Replace("Füge", "fügst"));
    }

    static CoverStudio.Blueprint Bp(double bpm, string key, string meter, double dur, int sections, Func<int, double?> melody) =>
        new(dur, bpm, key, meter, ["Am", "F"], "A3–E5 (Alt)", Enumerable.Range(0, sections).Select(i => new CoverStudio.Section(i * 10, i * 10 + 10, 0.5)).ToList(), 0.05,
            Enumerable.Range(0, (int)(dur / 0.05)).Select(melody).ToList());

    static double? Tune(int i) => i % 40 < 30 ? 60 + 5 * Math.Sin(i / 15.0) : null;

    [Fact(DisplayName = "M2 Bauplan: Grunddaten, Abschnitte und Melodie aus den Analysen; Tonname deutsch")]
    public void Blueprint()
    {
        var a = new SongAnalysis(180, 120, "A minor", 0.8, "4/4", ["Am", "F", "C", "G"], new SongRange("A3", "E5", 19, "Alt"));
        var b = CoverStudio.Build(a, """{"bpm":120,"duration":180,"beats":[0.5,1.0],"sections":[{"start":0,"end":30,"energy":0.4},{"start":30,"end":60,"energy":0.9}]}""",
            """{"hop":0.05,"midi":[60.0,null,62.5,64.0]}""");
        Assert.Equal("A-Moll", b.KeyDe); Assert.Equal(2, b.Sections.Count); Assert.Equal(4, b.Melody.Count); Assert.Null(b.Melody[1]);
        Assert.Equal(60, b.MelodyLow); Assert.Equal(64, b.MelodyHigh);
        Assert.Equal("C4", CoverStudio.Note(60)); Assert.Equal("A4", CoverStudio.Note(69)); Assert.Equal("H3", CoverStudio.Note(59));
        var none = CoverStudio.Build(a, null, "kaputt"); Assert.Empty(none.Sections); Assert.Empty(none.Melody);
    }

    [Fact(DisplayName = "M3/M5: Stil enthält Tempo, Tonart, Takt, Genre und erfundene Stimme – nie einen echten Sängernamen; Nähe → Stärke 0,5–0,9")]
    public void Style()
    {
        var b = Bp(118, "C major", "4/4", 60, 3, Tune);
        var s = CoverStudio.CoverStyle(b, "Rockabilly", CoverStudio.Voice("rocknroll56"));
        Assert.Contains("118 BPM", s); Assert.Contains("key of C major", s); Assert.Contains("4/4 time", s); Assert.Contains("Rockabilly", s); Assert.Contains("1956 style", s);
        Assert.Contains("fictional singer", s);
        Assert.All(CoverStudio.Voices, v => Assert.DoesNotContain("Elvis", v.Prompt + v.Label + v.Hint));
        Assert.Equal(0.5, CoverStudio.Strength(0)); Assert.Equal(0.9, CoverStudio.Strength(1)); Assert.Equal(0.7, CoverStudio.Strength(0.5));
        Assert.Equal("frei", CoverStudio.Voice("gibtsnicht").Id);
    }

    [Fact(DisplayName = "M4 Vergleich: gleiches Lied (transponiert, doppeltes Tempo gemessen) grün; fremde Melodie und anderes Tempo rot; Selbstverbesserung erhöht die Nähe")]
    public void Compare()
    {
        var o = Bp(120, "A minor", "4/4", 60, 4, Tune);
        var same = Bp(240, "A minor", "4/4", 61, 4, i => Tune(i) + 3); // 3 Halbtöne höher gesungen? → anderer Tonart-Name, darum gleiche Tonart hier
        var r = CoverStudio.Compare(o, same);
        Assert.Equal("gruen", r.Light);
        Assert.Equal("gruen", r.Points.First(p => p.Name == "Tempo").Light);
        Assert.Null(CoverStudio.NextTry(r, 0.7, 1));
        var other = Bp(92, "F# major", "3/4", 45, 2, i => 60 + (i * 7 % 13));
        var bad = CoverStudio.Compare(o, other);
        Assert.Equal("rot", bad.Light);
        Assert.Equal("rot", bad.Points.First(p => p.Name == "Melodie").Light);
        var next = CoverStudio.NextTry(bad, 0.7, 1)!.Value;
        Assert.True(next.Strength > 0.7); Assert.Contains("Melodie", next.Why);
        Assert.Null(CoverStudio.NextTry(bad, 0.7, 3));
        Assert.Equal("gelb", CoverStudio.KeyLight("A minor", "C major")); Assert.Equal("gelb", CoverStudio.KeyLight("C major", "G major")); Assert.Equal("rot", CoverStudio.KeyLight("C major", "F# major"));
    }

    [Fact(DisplayName = "M8 Klang: 1956 ist Mono mit Echo, alle Epochen enden mit Lautheit −14 LUFS, MP3-Ausgabe mit LAME")]
    public void Sound()
    {
        var a = string.Join(" ", CoverStudio.SoundArgs("in.wav", "out.mp3", "1956"));
        Assert.Contains("pan=mono", a); Assert.Contains("aecho", a); Assert.Contains("loudnorm=I=-14", a); Assert.Contains("libmp3lame", a);
        Assert.DoesNotContain("libmp3lame", string.Join(" ", CoverStudio.SoundArgs("in.wav", "out.wav", "heute")));
        Assert.All(CoverStudio.Voices, v => Assert.Contains(CoverStudio.Sounds, s => s.Id == v.Sound));
    }
}
