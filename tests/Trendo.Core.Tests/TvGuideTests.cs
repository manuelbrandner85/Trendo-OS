using Trendo.Core.Tv;
using Xunit;

namespace Trendo.Core.Tests;

/// T-0300: Programmführer als Raster – Sparten, Zeitfenster, Suche, Überschneidungen, Erinnerungen.
public class TvGuideTests
{
    static readonly DateTime N = new(2026, 11, 1, 12, 10, 0, DateTimeKind.Utc);
    static readonly TvChannel Eins = new("q:1", "Eins", "u1", "D", null, "eins.de", 1), Zwei = new("q:2", "Zwei", "u2", "D", null, "zwei.de", 2), Ohne = new("q:3", "Ohne", "u3", "D", null, null, 3);
    static TvProgramme P(string ch, int fromMin, int toMin, string title, string cat = "", string desc = "") => new(ch, N.AddMinutes(fromMin), N.AddMinutes(toMin), title, desc, cat);

    [Fact(DisplayName = "T-0300: Sparten aus Kategorie oder Titel (deutsch und englisch)")]
    public void Kinds()
    {
        Assert.Equal("Nachrichten", TvGuide.Kind(P("e", 0, 1, "Tagesschau")));
        Assert.Equal("Sport", TvGuide.Kind(P("e", 0, 1, "Live", "Sports")));
        Assert.Equal("Film", TvGuide.Kind(P("e", 0, 1, "Der Pate", "Movie")));
        Assert.Equal("Serie", TvGuide.Kind(P("e", 0, 1, "Tatort", "Krimi")));
        Assert.Equal("Kinder", TvGuide.Kind(P("e", 0, 1, "Sandmann", "Kinder")));
        Assert.Equal("Doku", TvGuide.Kind(P("e", 0, 1, "Terra X", "Dokumentation")));
        Assert.Equal("Show", TvGuide.Kind(P("e", 0, 1, "Wer wird Millionär", "Quiz")));
        Assert.Equal("Sonstiges", TvGuide.Kind(P("e", 0, 1, "Lorem")));
        Assert.All(TvGuide.Kinds, k => Assert.StartsWith("#", TvGuide.Color(k)));
    }

    [Fact(DisplayName = "T-0300: Zeitfenster – heute ab der halben Stunde, andere Tage ab 6 Uhr, 20:15-Sprung")]
    public void Window()
    {
        var local = N.ToLocalTime();
        var today = TvGuide.Start(N, 0).ToLocalTime();
        Assert.Equal(local.Date, today.Date); Assert.Equal(local.Hour, today.Hour); Assert.Equal(local.Minute < 30 ? 0 : 30, today.Minute);
        Assert.Equal(local.Date.AddDays(1).AddHours(6), TvGuide.Start(N, 1).ToLocalTime());
        Assert.Equal(local.Date.AddDays(2).AddHours(19.5), TvGuide.Start(N, 2, 19.5).ToLocalTime());
        Assert.Equal(DateTimeKind.Utc, TvGuide.Start(N, 3).Kind);
    }

    [Fact(DisplayName = "T-0300: Zeile, Suche (Titel, Beschreibung, Kategorie, nur Kommendes), Sender ohne Programminfo leer")]
    public void Row_and_search()
    {
        var all = new List<TvProgramme> { P("eins.de", -60, -30, "Tatort", "Krimi"), P("eins.de", -10, 20, "Tagesschau"), P("eins.de", 20, 110, "Tatort", "Krimi"), P("zwei.de", 30, 90, "Abendshow", "", "mit Tatort-Kommissar"), P("eins.de", 400, 500, "Später") };
        var row = TvGuide.Row(all, Eins, N, N.AddHours(6));
        Assert.Equal(["Tagesschau", "Tatort"], row.Select(p => p.Title));
        Assert.Empty(TvGuide.Row(all, Ohne, N, N.AddHours(6)));
        var hits = TvGuide.Search(all, [Eins, Zwei], "tatort", N);
        Assert.Equal(2, hits.Count); Assert.Equal("Eins", hits[0].Channel.Name); Assert.Equal("Abendshow", hits[1].Show.Title);
        Assert.Single(TvGuide.Search(all, [Eins, Zwei], "Krimi", N));
        Assert.Empty(TvGuide.Search(all, [Eins], "t", N));
    }

    [Fact(DisplayName = "T-0300: Überschneidende Aufnahmen und Merker „geplant“")]
    public void Overlaps()
    {
        var recs = new List<TvRecording> { new("a", "q:1", "Eins", "Tatort", N.AddMinutes(20), N.AddMinutes(110)), new("b", "q:2", "Zwei", "Alt", N.AddMinutes(-90), N.AddMinutes(-60), "fertig") };
        Assert.Single(TvGuide.Overlaps(recs, N.AddMinutes(60), N.AddMinutes(150)));
        Assert.Empty(TvGuide.Overlaps(recs, N.AddMinutes(110), N.AddMinutes(150)));
        Assert.Empty(TvGuide.Overlaps(recs, N.AddMinutes(20), N.AddMinutes(110), "a"));
        Assert.Empty(TvGuide.Overlaps(recs, N.AddMinutes(-80), N.AddMinutes(-70)));
        Assert.True(TvGuide.Planned(recs, Eins, P("eins.de", 20, 110, "Tatort")));
        Assert.False(TvGuide.Planned(recs, Zwei, P("zwei.de", 20, 110, "Tatort")));
    }

    [Fact(DisplayName = "T-0300: Erinnerung an/aus wird gespeichert, ist kurz vor Beginn fällig und nur einmal")]
    public void Reminders()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-tvguide-" + Guid.NewGuid().ToString("N"));
        try
        {
            var lib = new TvLibrary(dir); var p = new TvProgramme("eins.de", DateTime.UtcNow.AddMinutes(2), DateTime.UtcNow.AddMinutes(30), "Sandmann");
            Assert.True(lib.ToggleReminder(Eins, p)); Assert.True(lib.HasReminder("q:1", p.Start));
            Assert.Single(new TvLibrary(dir).Reminders);
            Assert.Single(TvGuide.Due(lib.Reminders, DateTime.UtcNow, TimeSpan.FromMinutes(3)));
            Assert.Empty(TvGuide.Due(lib.Reminders, DateTime.UtcNow, TimeSpan.FromMinutes(1)));
            lib.ReminderShown("q:1", p.Start);
            Assert.Empty(TvGuide.Due(lib.Reminders, DateTime.UtcNow, TimeSpan.FromMinutes(3)));
            Assert.False(lib.ToggleReminder(Eins, p)); Assert.Empty(lib.Reminders);
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }
}
