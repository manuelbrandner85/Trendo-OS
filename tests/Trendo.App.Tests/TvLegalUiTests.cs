using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Trendo.App;
using Trendo.Core.Tv;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>STUDIO-TV-01 – „Mediatheken & Streaming“ (Wunsch 05.11.2026): legale Quellen kategorisiert, Mediathek in Trendo suchen und abspielen.</summary>
public class TvLegal_Ui
{
    static void Pump() { for (var i = 0; i < 6; i++) Avalonia.Threading.Dispatcher.UIThread.RunJobs(); }
    static bool Visible(MainWindow w, string name) => w.GetVisualDescendants().OfType<Control>().Any(c => c.Name == name && c.IsEffectivelyVisible);

    static List<LegalSources.Video> Fake(string q, string cat, string? ch) =>
    [
        new("ARD", "Filme in der ARD", q.Length > 0 ? q : "Werk ohne Autor", "Ein Kinofilm", new DateTime(2026, 11, 4), TimeSpan.FromMinutes(175), "https://cdn.example/a.mp4", "https://cdn.example/a_hd.mp4", "https://www.ardmediathek.de/a"),
        new("ARTE.DE", "Kino", "La Chimera", "Ein zweiter Film", new DateTime(2026, 11, 3), TimeSpan.FromMinutes(123), "https://cdn.example/b.mp4", null, "https://www.arte.tv/b"),
    ];

    [AvaloniaFact(DisplayName = "STUDIO-TV-01 Mediatheken & Streaming: ohne Senderliste erreichbar, Arten und Kategorien, Mediathek suchen und in Trendo abspielen, Abo-Kachel öffnet nur die offizielle Seite")]
    public async Task Legal_sources()
    {
        var (w, _, _) = Ui.Open(); w.Width = 1600; w.Height = 1000;
        try
        {
            var played = new List<(string Slot, string Url)>(); var opened = new List<string>(); var asked = new List<(string, string, string?)>();
            w.TvPlayOverride = (s, u) => played.Add((s, u));
            w.TvLegalOpenOverride = u => opened.Add(u);
            w.TvLegalSearchOverride = (q, c, ch) => { asked.Add((q, c, ch)); return Task.FromResult(Fake(q, c, ch)); };
            w.Show("tv"); Pump();
            Assert.True(Visible(w, "tv-start-mediathek")); // ohne eigene Senderliste: direkt zu den Mediatheken
            w.Find<Button>("tv-start-mediathek").Press(); Pump();
            Assert.True(Visible(w, "tv-legal")); Assert.True(Visible(w, "tv-legal-kind-mediathek")); Assert.True(Visible(w, "tv-legal-cat-filme"));
            Assert.True(Visible(w, "tv-legal-provider-ard")); Assert.False(Visible(w, "tv-legal-provider-netflix"));
            // Kategorie „Filme“ sucht sofort in den Mediatheken
            w.Find<Button>("tv-legal-cat-filme").Press(); Pump(); await Task.Delay(50); Pump();
            Assert.Equal(("", "Filme", (string?)null), asked[^1]);
            Assert.True(Visible(w, "tv-legal-item-0")); Assert.Contains("2 Sendungen", w.TvLegalStatus);
            // eigener Suchbegriff
            w.Find<TextBox>("tv-legal-search").Text = "Tatort"; w.Find<Button>("tv-legal-go").Press(); Pump(); await Task.Delay(50); Pump();
            Assert.Equal("Tatort", asked[^1].Item1); Assert.Equal("Tatort", w.TvLegalResults![0].Title);
            // Abspielen in Trendo (HD bevorzugt), Player oben im Reiter
            w.Find<Button>("tv-legal-play-0").Press(); Pump();
            Assert.Equal(("main", "https://cdn.example/a_hd.mp4"), played[^1]); Assert.True(Visible(w, "tv-vod-stage"));
            w.Find<Button>("tv-legal-web-1").Press(); Assert.Equal("https://www.arte.tv/b", opened[^1]);
            // Abo-Dienste: Kachel öffnet die offizielle Seite; Kategorie Sport filtert
            w.TvLegalSelect(LegalSources.Kind.Abo, "Sport"); Pump();
            Assert.True(Visible(w, "tv-legal-provider-dazn")); Assert.False(Visible(w, "tv-legal-provider-netflix"));
            w.Find<Button>("tv-legal-provider-dazn").Press(); Assert.Equal("https://www.dazn.com/de-DE", opened[^1]);
            w.TvLegalSelect(LegalSources.Kind.Kostenlos); Pump();
            Assert.True(Visible(w, "tv-legal-provider-pluto")); Assert.False(Visible(w, "tv-legal-search"));
            // Fehler der Suche: verständliche Meldung statt Absturz
            w.TvLegalSearchOverride = (_, _, _) => throw new InvalidOperationException("Mediathek-Suche: kaputt");
            w.TvLegalSelect(LegalSources.Kind.Mediathek); await w.TvLegalSearchAsync("x", "Alle", null); Pump();
            Assert.Contains("kaputt", w.TvLegalStatus); Assert.Empty(w.TvLegalResults!);
        }
        finally { w.Close(); }
    }
}
