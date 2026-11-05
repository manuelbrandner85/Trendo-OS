using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>Wunsch 05.11.2026: Treffer der Cover-Suche in Trendo Musik anklicken übernimmt alle Einstellungen für die Generierung – statt nur die MusicBrainz-Seite zu öffnen.</summary>
public class MusikCoverVorlage_Ui
{
    const string Mb = """{"recordings":[{"id":"r1","title":"Mein Lied","length":184000,"first-release-date":"1961-05-02","artist-credit":[{"name":"Erika Muster"}],"releases":[{"title":"Erste Platte"}]}]}""";
    const string Ab = """{"tonal":{"key_key":"A","key_scale":"minor"},"rhythm":{"bpm":118.4}}""";

    [AvaloniaFact(DisplayName = "Trendo Musik: Treffer anklicken übernimmt Titel, Länge, Stil ohne Namen, Tonart und Tempo ins Profi-Formular; ohne Messung freundlicher Hinweis; MusicBrainz-Seite öffnet sich dabei nicht")]
    public async Task Hit_click_takes_settings()
    {
        var (w, _, _) = Ui.Open(); w.Width = 1600; w.Height = 1100;
        var asked = new List<string>(); var launched = new List<string>();
        w.SongFactsFetchOverride = u => { asked.Add(u.Host); return Task.FromResult<string?>(u.Host.Contains("acousticbrainz") ? Ab : u.AbsolutePath.Contains("recording") ? Mb : null); };
        w.MusicLyricsOverride = _ => Task.FromResult("rock and roll, 1960s, electric guitar, Erika Muster, upbeat");
        w.CoverGenre = "Akustik";
        try
        {
            w.Show("musik-studio"); Settle();
            Assert.Single(await w.MsCoverSearch("Erika Muster – Mein Lied"));
            w.Show("musik-studio"); Settle();
            var hit = w.Find<Button>("cw-hit-0"); hit.BringIntoView(); Settle();
            hit.Press();
            for (var i = 0; i < 200 && w.MsCoverTaken.Count == 0; i++) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(10); }
            Assert.Contains(asked, h => h.Contains("acousticbrainz"));
            w.Show("musik-studio"); Settle();
            // Profi-Formular ausgefüllt
            Assert.Equal("Mein Lied (Cover)", w.Find<TextBox>("ms-title").Text);
            var style = w.Find<TextBox>("ms-style").Text ?? "";
            Assert.StartsWith("Akustik", style); Assert.Contains("electric guitar", style); Assert.DoesNotContain("Erika", style); // keine Namen im Stil
            Assert.Contains(w.MsCoverTaken, t => t == "118 BPM"); Assert.Contains(w.MsCoverTaken, t => t == "Tonart A-Moll"); Assert.Contains(w.MsCoverTaken, t => t.StartsWith("Länge 3:04"));
            Assert.Contains("Liedtext (fügst du selbst ein)", w.TryFind<TextBlock>("ms-msg")?.Text ?? w.CoverMessage);
            Assert.NotNull(w.TryFind<TextBlock>("cw-taken"));
            Assert.Equal("Infos bei MusicBrainz ansehen", w.Find<Button>("cw-page").Content);
            // ohne Messung bei AcousticBrainz: Hinweis, Tempo und Tonart leer
            w.SongFactsFetchOverride = u => Task.FromResult<string?>(u.Host.Contains("acousticbrainz") ? null : u.AbsolutePath.Contains("recording") ? Mb : null);
            await w.MsCoverSearch("Erika Muster – Mein Lied"); await w.MsCoverUse(0);
            Assert.Contains("keine Messung", w.CoverMessage);
            Assert.DoesNotContain(w.MsCoverTaken, t => t.EndsWith("BPM"));
        }
        finally { w.Close(); }
    }
}
