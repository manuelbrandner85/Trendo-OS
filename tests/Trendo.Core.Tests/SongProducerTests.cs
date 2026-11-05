using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Trendo Musik Claude 26 (05.11.2026): Produzenten-Prüfung (Aufbau, Silben, Reime, Tempo), Mastering-Ziele, Claudes Vorschläge lesen.</summary>
public class SongProducerTests
{
    [Fact(DisplayName = "Produzent: Ausreißer-Zeile im Rhythmus erkannt, Reimschema aabb, fehlender Refrain gemeldet; gleichmäßiger Text ohne Hinweise")]
    public void Check()
    {
        var bad = "[Strophe]\nDie Straße glänzt im Licht\nwir halten heute nicht\nund fahren immer weiter durch die ganze lange Nacht bis morgen früh\nbis der Morgen glüht";
        var r = SongProducer.Check(bad, 100, "4/4");
        Assert.Single(r.Sections); Assert.Contains(r.Sections[0].Lines, l => l.Hint is { } h && h.Contains("zu viel"));
        Assert.Contains(r.Notes, n => n.Contains("Kein Refrain"));
        Assert.Equal("aa", string.Concat(r.Sections[0].Lines.Take(2).Select(l => l.Rhyme)));
        var good = "[Strophe]\nDie Straße glänzt im Licht\nwir halten heute nicht\n[Refrain]\nSommer nimm uns mit\nweit im gleichen Schritt\n[Strophe]\nDer Wind ist warm und weich\nwir sind so frei und reich\n[Refrain]\nSommer nimm uns mit\nweit im gleichen Schritt";
        var g = SongProducer.Check(good, 100, "4/4");
        Assert.Equal(0, g.Issues); Assert.Equal(["Strophe", "Refrain", "Strophe", "Refrain"], g.Structure);
        Assert.Contains("Rhythmus gleichmäßig", SongProducer.Summary(g)); Assert.Contains("Reime aa", SongProducer.Summary(g));
        // sehr schnelles Tempo verträgt weniger Silben je Zeile
        var dense = SongProducer.Check("[Refrain]\nwir fahren und fahren und fahren und fahren und fahren und fahren und fahren und fahren", 200, "4/4");
        Assert.Contains(dense.Sections[0].Lines, l => l.Hint is { } h && h.Contains("zu dicht"));
    }

    [Fact(DisplayName = "Mastering-Ziele: Streaming −14, Laut −9, Podcast −16, Dynamisch −18; FFmpeg-Befehl trägt das Ziel; Standard bleibt −14 wie bisher")]
    public void Targets()
    {
        Assert.Equal(-14, SongProducer.TargetOf(null).Lufs); Assert.Equal(-9, SongProducer.TargetOf("laut").Lufs); Assert.Equal(-14, SongProducer.TargetOf("quatsch").Lufs);
        Assert.Contains("loudnorm=I=-14:TP=-1.2:LRA=11", string.Join(" ", SongMaster.MeasureArgs("a.mp3")));
        Assert.Contains("loudnorm=I=-9:TP=-0.8:LRA=7", string.Join(" ", SongMaster.MeasureArgs("a.mp3", -9, -0.8)));
        var m = new SongMaster.Measured("-13.4", "-0.7", "15.7", "-24", "0.2");
        Assert.Contains("I=-18:TP=-1:LRA=15", string.Join(" ", SongMaster.ApplyArgs("a.mp3", "b.mp3", m, -18, -1)));
    }

    [Fact(DisplayName = "Claudes Vorschläge lesen: „ / “ wird Zeilenwechsel, Marken auf eigene Zeilen, ohne Marken oder zu lang abgelehnt; Briefing enthält Prüfung und Feldnamen")]
    public void Parse_and_briefing()
    {
        var (t, e) = SongProducer.ParseLyrics("{Strophe} / Die Straße glänzt / wir fahren los / [Refrain] / Sommer nimm uns mit");
        Assert.Null(e); Assert.Equal("[Strophe]\nDie Straße glänzt\nwir fahren los\n\n[Refrain]\nSommer nimm uns mit".Replace("\n\n", "\n"), t!.Replace("\n\n", "\n"));
        Assert.NotNull(SongProducer.ParseLyrics("nur eine Zeile ohne Marke").Error);
        Assert.NotNull(SongProducer.ParseLyrics("[A] " + new string('x', 4100)).Error);
        Assert.Equal("Pop, Gitarre", SongProducer.ParseStyle("  Pop,   Gitarre, ").Text);
        var s = new MusicSong { Title = "Roadtrip", Style = "Pop", Lyrics = "[Strophe]\nDie Straße glänzt\nwir fahren los", Seconds = 120, Bpm = 110, Key = "A minor" };
        var b = SongProducer.Briefing(s, [s]);
        Assert.Contains("Produzent", b); Assert.Contains("110 BPM", b); Assert.Contains("A-Moll", b);
        Assert.Contains("ms-prod-lyrics", b); Assert.Contains("ms-prod-style", b); Assert.Contains("ms-prod-master", b);
        Assert.Contains("· Die Straße glänzt", b); Assert.Contains("Keine Namen echter Künstler", b);
    }
}
