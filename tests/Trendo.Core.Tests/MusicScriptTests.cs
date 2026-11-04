using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>KI-Musikvideo B: Drehbuch mit Konzept, Figuren und Einstellungen; bearbeiten, löschen, neu würfeln.</summary>
public class MusicScriptTests
{
    static SongUnderstanding.Result Song() => new(120, 40, [],
        [new(0, 8, "Intro", 0.6), new(8, 24, "Strophe 1", 0.9), new(24, 40, "Refrain", 1.3)],
        [new(0, "w", "Alt"), new(1, "m", "Tenor")],
        [new("Ich warte am Meer", "Strophe 1", 9, 13, "Sängerin", true), new("Du kommst zurück", "Strophe 1", 16, 20, "Sänger", true), new("Wir zwei", "Refrain", 25, 30, "beide", true)],
        new("Sehnsucht", "Zwei treffen sich am Meer.", "warm", ["Strand", "Leuchtturm"], "Abend", ["Gold"]), "Test");

    [Fact(DisplayName = "Drehbuch: lückenlose Einstellungen ≤ 5 s, im Refrain schneller, Singende im Bild (nie in der Totale), Figuren erfunden, Ausschluss gegen Fehlbildungen")]
    public void Plan()
    {
        var sc = MusicScript.Plan(Song(), "pop, akustikgitarre");
        Assert.Equal(2, sc.Figures.Count);
        Assert.Equal(["Sängerin", "Sänger"], sc.Figures.Select(f => f.Role));
        Assert.All(sc.Figures, f => Assert.Contains("fictional", f.Look));
        Assert.Equal(0, sc.Shots[0].Start); Assert.Equal(40, sc.Shots[^1].End, 2);
        for (var i = 1; i < sc.Shots.Count; i++) Assert.Equal(sc.Shots[i - 1].End, sc.Shots[i].Start, 3); // keine Lücke
        Assert.All(sc.Shots, s => Assert.True(s.End - s.Start <= 5.01, $"Einstellung {s.Index} zu lang"));
        var refr = sc.Shots.Where(s => s.Part == "Refrain").Average(s => s.End - s.Start);
        var verse = sc.Shots.Where(s => s.Part == "Strophe 1").Average(s => s.End - s.Start);
        Assert.True(refr < verse);
        var sing = sc.Shots.Where(s => s.Singing).ToList();
        Assert.NotEmpty(sing);
        Assert.All(sing, s => Assert.DoesNotContain(s.Size, new[] { "Totale", "Halbtotale" }));
        Assert.Contains(sc.Shots, s => s.Singing && s.Who == "beide" && s.Prompt.Contains("singing together"));
        Assert.Contains(sc.Shots, s => !s.Singing && s.Part == "Intro");
        Assert.All(sc.Shots, s => Assert.Contains(s.Place, new[] { "Strand", "Leuchtturm" }));
        Assert.Contains("golden hour", sc.Shots[0].Light);
        Assert.Contains("extra arms", MusicScript.Negative); Assert.Contains("fused fingers", MusicScript.Negative);
        Assert.Equal("Zwei treffen sich am Meer.", sc.Logline);
    }

    [Fact(DisplayName = "Drehbuch bearbeiten: Größe ändern baut die Beschreibung neu, Löschen schließt die Lücke, Neu würfeln ändert Zufall und Kamera; Speichern/Laden")]
    public void Edit()
    {
        var u = Song(); var sc = MusicScript.Plan(u, "pop");
        var e = MusicScript.Edit(sc, 1, s => s with { Size = "Groß" }, "pop", u.Theme);
        Assert.Equal("Groß", e.Shots[1].Size); Assert.Contains("extreme close-up", e.Shots[1].Prompt);
        var own = MusicScript.Edit(sc, 1, s => s with { Prompt = "eigene Beschreibung" }, "pop", u.Theme);
        Assert.Equal("eigene Beschreibung", own.Shots[1].Prompt); // eigene Texte bleiben
        var r = MusicScript.Remove(sc, 2);
        Assert.Equal(sc.Shots.Count - 1, r.Shots.Count);
        Assert.Equal(sc.Shots[2].End, r.Shots[1].End, 3);
        Assert.Equal(Enumerable.Range(0, r.Shots.Count), r.Shots.Select(s => s.Index));
        var w = MusicScript.Reroll(sc, 0, "pop", u.Theme);
        Assert.NotEqual(sc.Shots[0].Seed, w.Shots[0].Seed); Assert.NotEqual(sc.Shots[0].Move, w.Shots[0].Move);
        Assert.Equal(sc.Shots.Count, MusicScript.Load(MusicScript.Save(sc))!.Shots.Count);
        Assert.Contains("2 Einstellungen", MusicScript.Summary(sc with { Shots = sc.Shots.Take(2).ToList() }));
    }

    [Fact(DisplayName = "Konzept „Geschichte erzählen“: überwiegend Szenen ohne Gesang")]
    public void Story_concept()
    {
        var sc = MusicScript.Plan(Song(), "pop", "geschichte");
        Assert.True(sc.Shots.Count(s => !s.Singing) > sc.Shots.Count / 2);
    }
}
