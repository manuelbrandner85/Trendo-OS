using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Trendo.App;
using Trendo.Core.Tv;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>STUDIO-TV-01 – „Video-Plattformen“ (Wunsch 05.11.2026): YouTube und ähnliche Dienste als eigene Kategorie in Trendo TV.</summary>
public class TvVideos_Ui
{
    static void Pump() { for (var i = 0; i < 6; i++) Avalonia.Threading.Dispatcher.UIThread.RunJobs(); }
    static bool Visible(MainWindow w, string name) => w.GetVisualDescendants().OfType<Control>().Any(c => c.Name == name && c.IsEffectivelyVisible);
    static async Task Wait() { Pump(); await Task.Delay(40); Pump(); }

    static List<VideoPlatforms.Video> Fake(string pf, string q, string cat) =>
    [
        new(pf, "a", q.Length > 0 ? q : "Video zu " + cat, "Erika Muster", "", TimeSpan.FromSeconds(337), new DateTime(2026, 11, 4), 8696, null, $"https://tube.example.org/w/a", ResolveUrl: "https://tube.example.org/api/v1/videos/a"),
        new(pf, "b", "Zweites Video", "Max Beispiel", "", TimeSpan.Zero, null, 0, null, "https://tube.example.org/w/b"),
    ];

    [AvaloniaFact(DisplayName = "STUDIO-TV-01 Video-Plattformen: ohne Senderliste erreichbar, Kategorien wie bei YouTube, PeerTube in Trendo abspielen, Dailymotion auf der Original-Seite, YouTube-Suche und -Link öffnen den offiziellen Player")]
    public async Task Platforms()
    {
        var (w, _, _) = Ui.Open(); w.Width = 1600; w.Height = 1000;
        try
        {
            var played = new List<string>(); var opened = new List<string>(); var asked = new List<(string Pf, string Q, string Cat)>();
            w.TvPlayOverride = (_, u) => played.Add(u);
            w.TvLegalOpenOverride = u => opened.Add(u);
            w.TvVideoSearchOverride = (pf, q, c) => { asked.Add((pf, q, c)); return Task.FromResult(Fake(pf, q, c)); };
            w.TvVideoResolveOverride = v => Task.FromResult<string?>(v.ResolveUrl is null ? null : "https://tube.example.org/v-720.mp4");
            w.Show("tv"); Pump();
            Assert.True(Visible(w, "tv-start-videos"));
            w.Find<Button>("tv-start-videos").Press(); Pump();
            Assert.True(Visible(w, "tv-videos")); Assert.True(Visible(w, "tv-vid-platform-youtube")); Assert.True(Visible(w, "tv-vid-platform-peertube")); Assert.True(Visible(w, "tv-vid-cat-musik"));
            // PeerTube: Kategorie Musik sucht sofort, Kachel mit Kanal und Aufrufen, Abspielen im Trendo-Player
            w.Find<Button>("tv-vid-cat-musik").Press(); await Wait();
            Assert.Equal(("peertube", "", "musik"), asked[^1]); Assert.True(Visible(w, "tv-vid-item-0")); Assert.Contains("2 Videos auf PeerTube", w.TvVideoStatus);
            w.Find<Button>("tv-vid-play-0").Press(); await Wait();
            Assert.Equal("https://tube.example.org/v-720.mp4", played[^1]); Assert.True(Visible(w, "tv-vod-stage"));
            // ohne abspielbare Datei: Hinweis statt Absturz
            w.Find<Button>("tv-vid-play-1").Press(); await Wait();
            Assert.Contains("keine abspielbare Datei", w.TvVideoStatus); Assert.Single(played);
            // Dailymotion: Suche in Trendo, Ansehen auf der offiziellen Seite
            w.Find<Button>("tv-vid-platform-dailymotion").Press(); await Wait();
            Assert.Equal("dailymotion", asked[^1].Pf);
            w.Find<Button>("tv-vid-play-0").Press(); await Wait(); Assert.Equal("https://tube.example.org/w/a", opened[^1]); Assert.Single(played);
            // YouTube: Suche und Kategorie öffnen die offizielle Seite
            w.Find<Button>("tv-vid-platform-youtube").Press(); Pump();
            Assert.False(Visible(w, "tv-vid-results"));
            w.Find<TextBox>("tv-vid-search").Text = "Terra X"; w.Find<Button>("tv-vid-go").Press(); await Wait();
            Assert.Equal("https://www.youtube.com/results?search_query=Terra%20X", opened[^1]);
            w.Find<Button>("tv-vid-cat-kochen").Press(); await Wait(); Assert.Equal("https://www.youtube.com/results?search_query=rezept", opened[^1]);
            // eingefügter Link: offizieller Einbett-Player
            w.Find<TextBox>("tv-vid-search").Text = "https://youtu.be/dQw4w9WgXcQ"; w.Find<Button>("tv-vid-go").Press(); await Wait();
            Assert.StartsWith("https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ", opened[^1]);
            Assert.DoesNotContain(asked, a => a.Pf == "youtube"); // YouTube wird nie über Dritte abgefragt
            // Fehler der Suche: verständliche Meldung
            w.TvVideoSearchOverride = (_, _, _) => throw new InvalidOperationException("Die PeerTube-Suche nicht erreichbar (503).");
            w.TvVideoSelect("peertube"); await Wait();
            Assert.Contains("503", w.TvVideoStatus); Assert.Empty(w.TvVideoResults!);
        }
        finally { w.Close(); }
    }
}
