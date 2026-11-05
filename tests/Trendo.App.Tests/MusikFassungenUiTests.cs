using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Trendo Musik Profi 26 (05.11.2026): mitlaufender Liedtext, Tonart- und Tempo-Raster, Fassungen nebeneinander mit A/B an derselben Stelle.</summary>
public class MusikProfi26_Ui
{
    static void Pump() { for (var i = 0; i < 5; i++) Dispatcher.UIThread.RunJobs(); }

    sealed class FakeAudio : IAudioOut
    {
        public readonly List<string> Loads = []; public readonly List<double> Seeks = [];
        public event Action<double, double>? Progress { add { } remove { } }
        public event Action? Ended { add { } remove { } }
        public void Load(string file) => Loads.Add(file);
        public void Seek(double seconds) => Seeks.Add(seconds);
        public void SetPause(bool pause) { } public void SetVolume(int percent) { } public void Stop() { } public void Dispose() { }
    }

    [AvaloniaFact(DisplayName = "Trendo Musik Profi: Liedtext läuft mit (laufende Zeile gold), Zeile anklicken springt dorthin; Raster mit Tonart, Tonleiter, Takten; Fassungen A/B an gleicher Stelle")]
    public void Follow_grid_compare()
    {
        var (w, _, root) = Ui.Open();
        var audio = new FakeAudio(); w.AudioOverride = () => audio;
        try
        {
            var lib = new SongLibrary(w.Core.DataRoot);
            string F(string n) { var f = Path.Combine(root, n); File.WriteAllText(f, "ID3"); return f; }
            var orig = lib.Add(new MusicSong { Title = "Roadtrip", Style = "Pop, Gitarre", Seconds = 100, Bpm = 120, Key = "A minor", TimeSignature = "4/4", File = F("roadtrip.mp3"),
                Lyrics = "[Strophe]\nDie Straße glänzt im Abendlicht\nwir fahren los\n[Refrain]\nSommer, nimm uns mit\nSommer, nimm uns mit" });
            var cover = lib.Add(new MusicSong { Title = "Roadtrip – Cover", Style = "Jazz, Klavier", Seconds = 100, Kind = "cover", ParentId = orig.Id, File = F("roadtrip-cover.mp3"), Lyrics = orig.Lyrics, Created = DateTimeOffset.Now.AddMinutes(1) });
            w.MusicStudioSelect(orig.Id); w.Show("musik-studio"); Pump();
            // Liedtext mit Zeiten (geschätzt), noch nichts hervorgehoben
            Assert.Equal(4, w.MsFollowPlan.Count); Assert.NotNull(w.TryFind<TextBlock>("ms-follow-line-0"));
            Assert.Contains(w.GetLogicalDescendantsText(), t => t == "Zeiten geschätzt");
            // Abspielen und Fortschritt: Zeile 3 (Refrain) wird gold
            w.MsPlayAt(orig, 0); Pump(); Assert.Equal(orig.File, audio.Loads[^1]);
            w.Show("musik-studio"); Pump();
            var at = w.MsFollowPlan[2].Start + 0.5; w.MusicProgressForPreview(at, 100); Pump();
            Assert.Equal(2, w.MsFollowIndex);
            var line = w.Find<TextBlock>("ms-follow-line-2"); Assert.Equal(FontWeight.Bold, line.FontWeight);
            Assert.Equal(FontWeight.Normal, w.Find<TextBlock>("ms-follow-line-0").FontWeight);
            // Zeile anklicken: gleiches Lied läuft → nur springen
            w.MsPlayAt(orig, w.MsFollowPlan[1].Start); Assert.Equal(w.MsFollowPlan[1].Start, audio.Seeks[^1], 2); Assert.Single(audio.Loads);
            // Raster
            Assert.Equal("A-Moll", ((TextBlock)((StackPanel)w.Find<Border>("ms-grid-key").Child!).Children[1]).Text);
            Assert.Contains("A · H · C · D · E · F · G", w.Find<TextBlock>("ms-grid-scale").Text); Assert.Contains("C-Dur", w.Find<TextBlock>("ms-grid-scale").Text);
            var grid = w.Find<BeatGrid>("ms-grid"); Assert.Equal(50, grid.Bars.Count); // 100 s bei 120 BPM 4/4 = 2 s je Takt
            Assert.Equal(w.MsFollowPlan[1].Start, grid.Playhead, 1); // nach dem Sprung steht die Marke in Zeile 2
            // Fassungen: Original A, Cover B; B an derselben Stelle weiterhören
            Assert.NotNull(w.TryFind<Border>("ms-compare-item-1"));
            w.MusicProgressForPreview(42, 100); Pump();
            w.Find<Button>("ms-compare-play-1").Press(); Pump();
            Assert.Equal(cover.File, audio.Loads[^1]);
            w.MusicProgressForPreview(0, 100); Pump(); // neue Datei geladen, Länge bekannt → Sprung an die alte Stelle
            Assert.Equal(42, audio.Seeks[^1], 1);
            Assert.Contains("gleiche Stelle", w.TryFind<TextBlock>("ms-msg")?.Text ?? "");
            // ohne zweite Fassung: Hinweis
            var solo = lib.Add(new MusicSong { Title = "Allein", Seconds = 30, File = F("allein.mp3") });
            w.MusicStudioSelect(solo.Id); w.Show("musik-studio"); Pump();
            Assert.Contains(w.GetLogicalDescendantsText(), t => t.StartsWith("Noch keine zweite Fassung"));
            Assert.Null(w.TryFind<Border>("ms-follow")); // kein Text → kein Mitlauf
            Assert.Contains(w.GetLogicalDescendantsText(), t => t.StartsWith("Noch kein Tempo"));
        }
        finally { w.Close(); }
    }
}

static class TextExt
{
    public static List<string> GetLogicalDescendantsText(this Window w) =>
        [.. Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(w).OfType<TextBlock>().Select(t => t.Text ?? "")];
}
