using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Xunit;
namespace Trendo.App.Tests;

/// TV & Live auf Highend (02.11.2026) über die Oberfläche: Startseite mit Reihen, Film-Infos mit Trailer, Weiterschauen, nächste Folge,
/// Kinder-Profil, Bild im Bild, Bild & Ton, Aufnahme-Regel, Handy-Fernbedienung, Qualität bei doppelten Sendern.
public class TvPlus_Ui
{
    static void Pump() { for (var i = 0; i < 5; i++) Avalonia.Threading.Dispatcher.UIThread.RunJobs(); }

    [AvaloniaFact(DisplayName = "TV Highend: Startseite, Film-Infos/Trailer, Weiterschauen, nächste Folge, Kinder-Profil, Bild im Bild, Bild & Ton, Regeln, Fernbedienung, Qualität")]
    public async Task Highend()
    {
        var (w, _, _) = Ui.Open();
        var played = new List<string>();
        w.TvPlayOverride = (s, u) => played.Add(u);
        w.TvFetchOverride = u => Task.FromResult(Encoding.UTF8.GetBytes(
            u.Contains("player_api") && !u.Contains("action") ? """{"user_info":{"auth":1,"status":"Active"}}"""
            : u.Contains("get_live_categories") ? """[{"category_id":"9","category_name":"DE | Sender"},{"category_id":"8","category_name":"DE | Kinder"}]"""
            : u.Contains("get_live_streams") ? """[{"num":1,"name":"ZDF HD","stream_id":1,"category_id":"9"},{"num":2,"name":"ZDF FHD","stream_id":2,"category_id":"9"},{"num":3,"name":"KiKA","stream_id":3,"category_id":"8"}]"""
            : u.Contains("get_vod_categories") ? """[{"category_id":"1","category_name":"DE • Krimi"}]"""
            : u.Contains("get_vod_streams") ? """[{"stream_id":7,"name":"Tatort Spezial","category_id":"1","container_extension":"mkv","rating":"8.2","stream_icon":"http://img.example/7.jpg"},{"stream_id":8,"name":"Kommissar Fall","category_id":"1","rating":"7"}]"""
            : u.Contains("get_vod_info") ? """{"info":{"plot":"Ein Fall","rating":"8.2","director":"Erika Muster","youtube_trailer":"abcdefghijk"}}"""
            : u.Contains("get_series_categories") ? """[{"category_id":"5","category_name":"DE • Serien"}]"""
            : u.Contains("get_series_info") ? """{"episodes":{"1":[{"id":"101","episode_num":1,"title":"Anfang","container_extension":"mp4"},{"id":"102","episode_num":2,"title":"Weiter","container_extension":"mp4"}]}}"""
            : u.Contains("get_series") ? """[{"series_id":11,"name":"Kommissarin","category_id":"5"}]"""
            : u.Contains("xmltv") ? "<tv/>" : ""));
        try
        {
            await w.TvAddSource("", "url", "http://srv.example:8080/get.php?username=u&password=p&type=m3u_plus&output=ts");
            // doppelte Sender: ZDF HD + FHD → einer, mit Qualität ▾
            w.Show("tv"); Pump();
            Assert.DoesNotContain(w.GetLogicalTexts(), t => t == "ZDF HD");
            Assert.Contains(w.GetLogicalTexts(), t => t == "ZDF FHD");
            w.TvPlay(w.Tv.Channels().First(c => c.Name == "ZDF FHD").Id); w.Show("tv"); Pump();
            Assert.NotNull(w.TryFind<Button>("tv-quality"));
            Assert.NotNull(w.TryFind<Button>("tv-live-pip")); Assert.NotNull(w.TryFind<Button>("tv-live-tune"));
            // Startseite mit großem Bild und Reihen
            w.Find<Button>("tv-tab-film").Press(); Pump();
            Assert.NotNull(w.TryFind<Border>("tv-film-hero"));
            Assert.NotNull(w.TryFind<StackPanel>("tv-row-beliebt"));
            // Film-Infos mit Trailer
            w.Find<Button>("tv-hero-info").Press(); Pump();
            for (var i = 0; i < 50 && w.TryFind<StackPanel>("tv-film-info") is null; i++) { await Task.Delay(20); Pump(); }
            Assert.NotNull(w.TryFind<Button>("tv-film-trailer"));
            Assert.Equal("Erika Muster", w.TvVodInfoForTests!.Director);
            // abspielen: Film läuft im Bereich, Bild im Bild und Bild & Ton da
            w.Find<Button>("tv-film-play").Press(); Pump();
            Assert.Equal("film", w.CurrentTvTab);
            Assert.NotNull(w.TryFind<Button>("tv-vod-pip")); Assert.NotNull(w.TryFind<Button>("tv-vod-tune"));
            // Weiterschauen: gemerkte Stelle erscheint als Reihe und am Knopf
            var url = played[^1];
            w.Tv.SaveResume(new Trendo.Core.Tv.TvResume(Trendo.Core.Tv.TvPlus.ResumeKey(w.TvVodList().First(v => v.Name == "Tatort Spezial").Url), "Tatort Spezial", w.TvVodList().First(v => v.Name == "Tatort Spezial").Url, 1234, 5400, DateTime.UtcNow));
            w.Find<Button>("tv-vod-stop").Press(); Pump();
            Assert.StartsWith("Weiter ab 20:34", w.Find<Button>("tv-film-play").Content as string);
            w.Find<Button>("tv-film-close").Press(); Pump();
            Assert.NotNull(w.TryFind<StackPanel>("tv-continue-film"));
            // Serie: Folge 1 → „Nächste Folge“ spielt Folge 2
            w.Find<Button>("tv-tab-serie").Press();
            w.TryFind<Button>("tv-serie-item-")!.Press();
            for (var i = 0; i < 50 && w.TryFind<Button>("tv-ep-1-1") is null; i++) { await Task.Delay(20); Pump(); }
            w.Find<Button>("tv-ep-1-1").Press(); Pump();
            Assert.EndsWith("/101.mp4", played[^1]);
            Assert.True(w.TvPlayNextEpisode());
            Assert.EndsWith("/102.mp4", played[^1]);
            Assert.Equal("serie", w.CurrentTvTab);
            // Handy-Fernbedienung
            Assert.Equal("⏯", w.TvRemote("pause"));
            // Bild im Bild
            w.TvPip(true); Assert.True(w.TvPipOpen); w.TvPip(false); Assert.False(w.TvPipOpen);
            // Kinder-Profil: nur Kindersender
            w.Find<Button>("tv-kids").Press(); Pump();
            Assert.True(w.Tv.KidsMode);
            w.Find<Button>("tv-tab-live").Press(); Pump();
            Assert.Contains(w.GetLogicalTexts(), t => t == "KiKA");
            Assert.DoesNotContain(w.GetLogicalTexts(), t => t == "ZDF FHD");
            w.Find<Button>("tv-kids").Press(); Pump(); // ohne eingerichtete PIN direkt aus
            Assert.False(w.Tv.KidsMode);
            // Aufnahme-Regel über „Quellen“
            w.Find<Button>("tv-tab-quellen").Press(); Pump();
            w.Find<TextBox>("tv-rule-text").Text = "FC Bayern";
            w.Find<Button>("tv-rule-add").Press(); Pump();
            Assert.Single(w.Tv.RecordRules);
            Assert.NotNull(w.TryFind<ComboBox>("tv-language"));
        }
        finally { w.Close(); }
    }
}
