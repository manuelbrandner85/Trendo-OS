using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>KI-Musikvideo A: Trendo versteht das Lied (Abschnitte, Zeilen mit Zeit, wer singt, Thema).</summary>
public class SongUnderstandingTests
{
    const string Lyrics = "[Strophe 1 – Sie]\nDie Straße glänzt im Abendlicht\nwir fahren los\n[Refrain – beide]\nSommer nimm uns mit\n[Strophe 2]\n(Er) Ich halte deine Hand\n[Instrumental]";

    [Fact(DisplayName = "Liedtext: Abschnitte und ausdrückliche Stimmen („– Sie“, „– beide“, „(Er)“) werden erkannt")]
    public void Lines_and_tags()
    {
        var l = SongUnderstanding.LyricLines(Lyrics);
        Assert.Equal(4, l.Count);
        Assert.Equal(("Die Straße glänzt im Abendlicht", "Strophe 1", "w"), l[0]);
        Assert.Equal("Refrain", l[2].Part); Assert.Equal("beide", l[2].Who);
        Assert.Equal(("Ich halte deine Hand", "Strophe 2", "m"), l[3]);
        Assert.Null(SongUnderstanding.WhoFromTag("Bridge"));
    }

    [Fact(DisplayName = "Zeilen bekommen Zeiten aus den Wörtern (Whisper), Stimmen aus den Phrasen; Duett und „beide“ werden benannt")]
    public void Build_with_words_and_singers()
    {
        var sections = """{"bpm":120,"duration":40,"sections":[{"start":0,"end":4,"energy":0.5},{"start":4,"end":20,"energy":0.9},{"start":20,"end":40,"energy":1.2}],"beats":[0.5,1,1.5]}""";
        var singers = """{"singers":[{"id":0,"gender":"w","voice":"Alt"},{"id":1,"gender":"m","voice":"Bariton"}],"phrases":[{"start":5,"end":9,"singer":0,"both":false},{"start":10,"end":12,"singer":0,"both":false},{"start":21,"end":24,"singer":1,"both":true},{"start":30,"end":33,"singer":1,"both":false}]}""";
        static string W(string t, int a, int b) => $$"""{"offsets":{"from":{{a}},"to":{{b}}},"text":" {{t}}"}""";
        var whisper = "{\"transcription\":[" + string.Join(",", W("[Musik]", 0, 4000), W("Die", 5000, 5400), W("Strasse", 5400, 6000), W("glänzt", 6000, 6600), W("im", 6600, 6800), W("Abendlicht", 6800, 8800),
            W("wir", 10000, 10400), W("fahren", 10400, 11000), W("los", 11000, 12000), W("Sommer", 21000, 21800), W("nimm", 21800, 22400), W("uns", 22400, 22800), W("mit", 22800, 24000),
            W("ich", 30000, 30500), W("halte", 30500, 31200), W("deine", 31200, 31800), W("Hand", 31800, 33000)) + "]}";
        var r = SongUnderstanding.Build("[Strophe 1]\nDie Straße glänzt im Abendlicht\nwir fahren los\n[Refrain]\nSommer nimm uns mit\n[Strophe 2]\nIch halte deine Hand", sections, singers, whisper, null);
        Assert.Equal(120, r.Bpm); Assert.Equal(3, r.Beats.Count);
        Assert.All(r.Lines, l => Assert.True(l.Timed));
        Assert.Equal(5.0, r.Lines[0].Start); Assert.Equal(8.8, r.Lines[0].End);
        Assert.Equal("Sängerin", r.Lines[0].Who);
        Assert.Equal("beide", r.Lines[2].Who); // Phrase mit zwei gleichzeitigen Stimmen
        Assert.Equal("Sänger", r.Lines[3].Who);
        Assert.Equal(["Intro", "Strophe 1", "Refrain"], r.Sections.Select(s => s.Part));
        Assert.Equal("Wortzeiten (Whisper)", r.Source);
        Assert.Contains("Duett: Sängerin und Sänger", SongUnderstanding.Summary(r));
        Assert.Equal(r.Lines.Count, SongUnderstanding.Load(SongUnderstanding.Save(r))!.Lines.Count);
    }

    [Fact(DisplayName = "Ohne Whisper: Zeiten aus den gesungenen Phrasen; eine Stimme heißt „Sängerin“")]
    public void Build_from_phrases()
    {
        var sections = """{"bpm":90,"duration":20,"sections":[{"start":0,"end":20,"energy":1}]}""";
        var singers = """{"singers":[{"id":0,"gender":"w","voice":"Sopran"}],"phrases":[{"start":1,"end":4,"singer":0},{"start":6,"end":9,"singer":0}]}""";
        var r = SongUnderstanding.Build("eins zwei\ndrei vier", sections, singers, null, null);
        Assert.Equal("gesungene Phrasen", r.Source);
        Assert.Equal(1.0, r.Lines[0].Start); Assert.Equal(6.0, r.Lines[1].Start);
        Assert.All(r.Lines, l => Assert.Equal("Sängerin", l.Who));
    }

    [Fact(DisplayName = "Thema: JSON-Antwort der KI wird gelesen, auch mit Text drumherum; Unbrauchbares ergibt null")]
    public void Theme()
    {
        var t = SongUnderstanding.ParseTheme("Gern! {\"thema\":\"Aufbruch\",\"geschichte\":\"Zwei fahren ans Meer.\",\"stimmung\":\"sehnsüchtig\",\"orte\":[\"Landstraße\",\"Strand\"],\"tageszeit\":\"Abend\",\"farben\":[\"Gold\"]} Viel Spaß");
        Assert.NotNull(t); Assert.Equal("Aufbruch", t!.Topic); Assert.Equal(["Landstraße", "Strand"], t.Places);
        Assert.Null(SongUnderstanding.ParseTheme("keine Ahnung"));
        Assert.Contains("Keine echten oder bekannten Personen", SongUnderstanding.ThemePrompt("T", "pop", "la"));
    }
}
