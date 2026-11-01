using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// Vorstellung nach einem Update: Neuerungen als Folien, Update sofort mit Countdown, häufigere Suche.
public class NewsShowTests
{
    const string Md = "## 0.9.29\n\n- **Neues Programm „Karten & Touren“** ersetzt die Formularseite:\n  - Echte Karte zum Zoomen\n  - GPX laden\n" +
                      "- **Posteingang als Mailprogramm:** Ordner „Entwürfe“ und **Gesendet**\n- Rückgängig wirkt jetzt zuverlässig: auch nach dem Eintippen\n" +
                      "- Einige Knöpfe reagierten nur auf die Schrift\n\n## 0.9.28\n\n- Alt\n";

    [Fact(DisplayName = "Neuerungen: eine Folie je Punkt – fetter Titel, Text, eingerückte Unterpunkte; ohne Titel bis zum Doppelpunkt oder erste Wörter; nur die gewählte Version")]
    public void Slides()
    {
        var s = NewsShow.Slides(Md, "0.9.29");
        Assert.Equal(4, s.Count);
        Assert.Equal("Neues Programm „Karten & Touren“", s[0].Title); Assert.Equal("Ersetzt die Formularseite:", s[0].Text);
        Assert.Equal(["Echte Karte zum Zoomen", "GPX laden"], s[0].Points); Assert.Equal("🗺️", s[0].Icon);
        Assert.Equal("Posteingang als Mailprogramm", s[1].Title); Assert.Equal("Ordner „Entwürfe“ und Gesendet", s[1].Text); Assert.Equal("✉️", s[1].Icon);
        Assert.Equal("Rückgängig wirkt jetzt zuverlässig", s[2].Title); Assert.Equal("Auch nach dem Eintippen", s[2].Text);
        Assert.Equal("Einige Knöpfe reagierten nur auf die Schrift", s[3].Title); Assert.Equal("", s[3].Text); Assert.Equal("✦", s[3].Icon);
        Assert.Equal(["Alt"], NewsShow.Slides(Md, "0.9.28").Select(x => x.Title));
        Assert.Equal(4, NewsShow.Slides("## 0.9.29\n- a b c d e f g\n- **X:** y\n- z: w\n- q\n").Count);
        Assert.InRange(s[0].Duration.TotalSeconds, 5, 12);
        Assert.Equal(12, new NewsSlide("T", new string('x', 2000), [], "✦").Duration.TotalSeconds);
    }

    [Fact(DisplayName = "Neuerungen: echte Notizen 0.9.28 ergeben je Punkt eine Folie mit Titel; Updates sofort, Suche alle 30 Minuten")]
    public void Real_notes_and_policy()
    {
        var dir = AppContext.BaseDirectory; while (dir is not null && !File.Exists(Path.Combine(dir, "Trendo.sln"))) dir = Path.GetDirectoryName(dir);
        var md = File.ReadAllText(Path.Combine(dir!, "installer", "neuerungen", "0.9.28.md"));
        var s = NewsShow.Slides(md, "0.9.28");
        Assert.Equal(md.Replace("\r", "").Split('\n').Count(l => l.StartsWith("- ")), s.Count);
        Assert.All(s, x => Assert.False(string.IsNullOrWhiteSpace(x.Title)));
        Assert.DoesNotContain(s, x => x.Title.Contains("**") || x.Text.Contains("**"));
        Assert.Equal(TimeSpan.FromMinutes(30), UpdateSchedule.CheckEvery);
        Assert.Equal("sofort", new UserSettings().UpdateWhen);
    }
}
