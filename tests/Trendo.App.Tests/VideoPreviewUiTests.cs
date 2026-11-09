using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core.Media;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>Pilot 4.0, V1: flüssige Vorschau mit Ton; alte Einzelbild-Wiedergabe bleibt für andere Tempi.</summary>
public class VideoPreview_Ui
{
    sealed class FakeSource : IPreviewSource
    {
        public bool Failed => false; public bool Disposed; public int Copies;
        public bool TryCopy(double seconds, Span<byte> target, int stride) { Copies++; target.Fill(200); return true; }
        public void Dispose() => Disposed = true;
    }
    sealed class FakeAudio : IAudioOut
    {
        public string? Loaded; public List<double> Seeks = []; public int Stops; public bool Paused = true;
        public void Load(string file) { Loaded = file; Progress?.Invoke(0, 10); }
        public void SetPause(bool pause) => Paused = pause;
        public void Seek(double seconds) => Seeks.Add(seconds);
        public void SetVolume(int percent) { }
        public void Stop() => Stops++;
        public event Action<double, double>? Progress;
        public event Action? Ended { add { } remove { } }
        public void Dispose() { }
    }

    static void Ticks(MainWindow w, Trendo.Core.Programs.ProgramDocument d, int n) { for (var i = 0; i < n; i++) { w.EditSmoothTickForTests(d); Dispatcher.UIThread.RunJobs(); Thread.Sleep(20); } }

    [AvaloniaFact(DisplayName = "Vorschau V1: Abspielen nimmt die Bildquelle, zeigt Bilder, spielt die Ton-Mischung; Anhalten beendet Quelle und Ton und setzt den Abspielkopf")]
    public void Plays_with_sound_and_stops()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000;
        var source = new FakeSource(); var audio = new FakeAudio(); EditClip? asked = null;
        w.PreviewSourceOverride = (c, start, len, fps) => { asked = c; return source; };
        w.PreviewAudioOverride = () => audio; w.PreviewMixOverride = _ => Path.Combine(root, "mix.m4a");
        var f = Path.Combine(root, "film.mp4"); File.WriteAllBytes(f, [1, 2, 3]);
        w.Show("schnitt"); Settle(); RealClick(w, w.Find<Button>("prog-tpl-leer")); Settle();
        w.EditAdd(f, length: 10, audio: true); Settle();
        var d = w.ProgramActive("schnitt")!;
        w.EditTogglePlay(d);
        Assert.True(w.EditPlaying);
        Ticks(w, d, 8);
        Assert.Equal(f, asked!.File);
        Assert.True(w.PreviewFramesShown >= 3, $"nur {w.PreviewFramesShown} Bilder");
        Assert.IsType<WriteableBitmap>(w.Find<Image>("ve-play-image").Source);
        Assert.Equal(Path.Combine(root, "mix.m4a"), audio.Loaded);
        Assert.False(audio.Paused); Assert.Single(audio.Seeks);
        w.EditTogglePlay(d); Settle();
        Assert.False(w.EditPlaying); Assert.True(source.Disposed); Assert.Equal(1, audio.Stops);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Vorschau V1: Ohne Bildquelle (Tests, Abschottung) bleibt die bisherige Wiedergabe; schnelleres Tempo wechselt dorthin")]
    public void Falls_back_to_frames()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000;
        var calls = 0; w.PlayFramesOverride = _ => { calls++; return []; };
        var f = Path.Combine(root, "film.mp4"); File.WriteAllBytes(f, [1, 2, 3]);
        w.Show("schnitt"); Settle(); RealClick(w, w.Find<Button>("prog-tpl-leer")); Settle();
        w.EditAdd(f, length: 10, audio: true); Settle();
        var d = w.ProgramActive("schnitt")!;
        w.EditTogglePlay(d); Assert.True(w.EditPlaying);
        w.EditShowFrame(d, 0.5);
        Assert.True(calls > 0); Assert.Equal(0, w.PreviewFramesShown);
        w.EditTogglePlay(d); w.PlayFramesOverride = null;

        var source = new FakeSource(); w.PreviewSourceOverride = (_, _, _, _) => source;
        w.EditTogglePlay(d); Ticks(w, d, 3);
        Assert.True(source.Copies > 0);
        w.PlayFramesOverride = _ => []; // ab hier: L für doppeltes Tempo
        w.EditKey(d, Avalonia.Input.Key.L); w.EditSmoothTickForTests(d); Dispatcher.UIThread.RunJobs();
        Assert.True(source.Disposed); Assert.True(w.EditPlaying); Assert.Equal(2, w.EditRate);
        w.EditKey(d, Avalonia.Input.Key.K); Settle();
        w.Close();
    }
}
