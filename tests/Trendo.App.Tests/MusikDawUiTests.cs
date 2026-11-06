using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Trendo Musik im DAW-Stil (Wunsch 06.11.2026): Transportleiste mit Takt, Tempo, Takt-Art und Tonart, Arrangement mit Spuren, „Im Mischpult öffnen“.</summary>
public class MusikDaw_Ui
{
    static void Pump() { for (var i = 0; i < 5; i++) Dispatcher.UIThread.RunJobs(); }
    sealed class FakeAudio : IAudioOut
    {
        public readonly List<double> Seeks = [];
        public event Action<double, double>? Progress { add { } remove { } }
        public event Action? Ended { add { } remove { } }
        public void Load(string file) { } public void Seek(double seconds) => Seeks.Add(seconds);
        public void SetPause(bool pause) { } public void SetVolume(int percent) { } public void Stop() { } public void Dispose() { }
    }

    [AvaloniaFact(DisplayName = "Trendo Musik DAW: Transportleiste zeigt Takt 1.1 und Tempo, Arrangement mit Abschnitten, Text und Gesang/Begleitung; Fortschritt bewegt Takt-Anzeige; Mischpult legt Mehrspur-Projekt mit den Spuren an")]
    public void Transport_arrange_mixer()
    {
        var (w, _, root) = Ui.Open(); var audio = new FakeAudio(); w.AudioOverride = () => audio;
        try
        {
            var lib = new SongLibrary(w.Core.DataRoot);
            string Wav(string n) { var f = Path.Combine(root, n); Directory.CreateDirectory(root); Trendo.Core.Media.AudioFile.WriteWav(new Trendo.Core.Media.AudioBuf(8000, [new float[8000], new float[8000]]), f); return f; }
            var s = lib.Add(new MusicSong { Title = "Roadtrip", Seconds = 100, Bpm = 120, Key = "A minor", TimeSignature = "4/4", File = Wav("roadtrip.wav"), Lyrics = "[Strophe]\nDie Straße glänzt\n[Refrain]\nSommer nimm uns mit" });
            lib.Add(new MusicSong { Title = "Roadtrip – Gesang", Kind = "gesang", ParentId = s.Id, File = Wav("gesang.wav") });
            lib.Add(new MusicSong { Title = "Roadtrip – Begleitung", Kind = "begleitung", ParentId = s.Id, File = Wav("begleitung.wav") });
            w.MusicStudioSelect(s.Id); w.Show("musik-studio"); Pump();
            Assert.Equal("120,00", w.Find<TextBlock>("ms-transport-bpm-v").Text.Replace('.', ','));
            Assert.Equal("A-Moll", w.Find<TextBlock>("ms-transport-key-v").Text);
            var arr = w.Find<ArrangeView>("ms-arrange");
            Assert.Equal(["Abschnitte", "Text (geschätzt)", "Gesang", "Begleitung", "Mix"], arr.Lanes.Select(l => l.Name));
            Assert.Equal(50, arr.Bars.Count);
            w.MsPlayAt(s, 0); Pump(); w.MusicProgressForPreview(9, 100); Pump();
            Assert.Equal("5.3", w.Find<TextBlock>("ms-transport-bars").Text); // 9 s bei 120 BPM = Schlag 18 → Takt 5, Schlag 3
            Assert.Equal(9, w.Find<ArrangeView>("ms-arrange").Playhead, 1);
            w.Find<Button>("ms-to-mixer").Press(); Pump();
            Assert.Equal("ton", w.CurrentView);
            var d = w.ProgramActive("ton")!;
            Assert.True(Trendo.Core.Media.AudioMulti.IsMulti(d.Data));
            Assert.Equal(["gesang", "begleitung"], Trendo.Core.Media.AudioMulti.Tracks(d.Data).Select(t => Path.GetFileNameWithoutExtension(t.File)));
        }
        finally { w.Close(); }
    }
}
