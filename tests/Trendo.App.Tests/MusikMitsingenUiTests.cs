using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Trendo Musik S5/S6: Mitsingen über die Begleitung mit Treffer-Anzeige, Aufnahme mischen und mastern.</summary>
[Collection("Claude")]
public class MusikMitsingenUiTests
{
    static async Task Pump() { for (var i = 0; i < 8; i++) { Dispatcher.UIThread.RunJobs(); await Task.Delay(10); } }

    sealed class FakeAudio : IAudioOut
    {
        public readonly List<string> Loads = [];
        public event Action<double, double>? Progress; public event Action? Ended;
        public void Load(string file) => Loads.Add(file);
        public void SetPause(bool pause) { }
        public void Seek(double seconds) { }
        public void SetVolume(int percent) { }
        public void Stop() { }
        public void Dispose() { }
        public void Tick(double pos, double dur) => Progress?.Invoke(pos, dur);
        public void Quiet() => Ended?.Invoke();
    }

    static float[] Tone(double hz)
    {
        var x = new float[2048];
        for (var i = 0; i < x.Length; i++) { double v = 0; for (var k = 1; k <= 5; k++) v += Math.Sin(2 * Math.PI * hz * k * i / 16000.0) / k; x[i] = (float)(0.3 * v); }
        return x;
    }

    static (MusicSong Orig, MusicSong Acc, MusicSong Voc) Library(string root)
    {
        var lib = new SongLibrary(root);
        string F(string n) { var f = Path.Combine(root, n); File.WriteAllBytes(f, [1, 2, 3]); return f; }
        var o = lib.Add(new MusicSong { Title = "Sommer", Style = "pop", Lyrics = "[Refrain]\nla la", Seconds = 30, File = F("lied.mp3") });
        var a = lib.Add(new MusicSong { Title = "Sommer – Begleitung", Seconds = 30, File = F("begleitung.wav"), Kind = "begleitung", ParentId = o.Id });
        var v = lib.Add(new MusicSong { Title = "Sommer – Gesang", Seconds = 30, File = F("gesang.wav"), Kind = "gesang", ParentId = o.Id });
        return (o, a, v);
    }

    [AvaloniaFact(DisplayName = "S5: Mitsingen spielt die Begleitung, zeigt live Ziel/gesungener Ton und Treffer; S6: Mischen (Versatz, Hall) und Mastern ergeben „– mit meiner Stimme“")]
    public async Task Sing_and_mix()
    {
        var (w, _, root) = Ui.Open();
        var audio = new FakeAudio(); w.AudioOverride = () => audio;
        var (orig, acc, voc) = Library(root);
        w.MelodyOverride = f => Task.FromResult<string?>(f == voc.File ? """{"hop": 0.05, "midi": [60, 60, 60, 60, 60, 60, 60, 60, 60, 60]}""" : null);
        var started = new TaskCompletionSource<Action<float[]>>();
        w.MicStreamOverride = async (onBlock, ct) =>
        {
            started.SetResult(onBlock);
            try { await Task.Delay(Timeout.Infinite, ct); } catch (OperationCanceledException) { }
            var take = Path.Combine(root, "aufnahme.wav"); File.WriteAllBytes(take, [1, 2, 3]); return take;
        };
        var runs = new List<string[]>();
        w.MasterRunOverride = a =>
        {
            runs.Add(a);
            if (a.Contains("null")) return Task.FromResult(new Cli.Result(0, "", """{"input_i" : "-12.0", "input_tp" : "0.1", "input_lra" : "9.0", "input_thresh" : "-22.0", "target_offset" : "0.0"}"""));
            File.WriteAllBytes(a[^1], new byte[5000]); return Task.FromResult(new Cli.Result(0, "", ""));
        };
        try
        {
            w.MusicStudioSelect(orig.Id); w.Show("musik-studio"); await Pump();
            var sing = w.MsSingStart(orig.Id);
            var feed = await started.Task; await Pump();
            Assert.Equal(acc.File, audio.Loads.Last());
            audio.Tick(0.1, 30); await Pump();
            feed(Tone(261.6)); await Pump(); // C4 = Ziel → Treffer
            Assert.Contains("Ziel C4 · du C4  ✓ Treffer", w.Find<TextBlock>("ms-sing-live").Text);
            feed(Tone(233.1)); await Pump(); // B3 → höher
            Assert.Contains("etwas höher", w.Find<TextBlock>("ms-sing-live").Text);
            Assert.Contains("50 % getroffen", w.Find<TextBlock>("ms-sing-live").Text);
            w.Find<Button>("ms-sing-stop").Press();
            Assert.True(await sing); await Pump();
            Assert.Contains("50 % der gesungenen Töne getroffen", w.Find<TextBlock>("ms-msg").Text);
            w.Find<ComboBox>("ms-mix-reverb").SelectedIndex = 2; w.Find<NumericUpDown>("ms-mix-offset").Value = 100; await Pump();
            var mixed = await w.MsMixTake(); await Pump();
            Assert.NotNull(mixed);
            Assert.Equal(("Sommer – mit meiner Stimme", "aufnahme", orig.Id, true), (mixed!.Title, mixed.Kind, mixed.ParentId, mixed.Mastered));
            var mix = string.Join(" ", runs[0]);
            Assert.Contains(acc.File, mix); Assert.Contains("atrim=start=0.100", mix); Assert.Contains("aecho=0.8:0.75", mix);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "S5: ohne getrennte Spuren ehrlicher Hinweis „zuerst Spuren trennen“, kein Start")]
    public async Task Needs_stems()
    {
        var (w, _, root) = Ui.Open();
        var f = Path.Combine(root, "x.mp3"); File.WriteAllBytes(f, [1]);
        var s = new SongLibrary(root).Add(new MusicSong { Title = "Ohne", Seconds = 20, File = f });
        try
        {
            w.MusicStudioSelect(s.Id); w.Show("musik-studio"); await Pump();
            Assert.DoesNotContain(Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(w).OfType<Button>(), b => b.Name == "ms-sing");
            Assert.False(await w.MsSingStart(s.Id));
            Assert.Contains("zuerst „Spuren trennen“", w.Find<TextBlock>("ms-msg").Text);
        }
        finally { w.Close(); }
    }
}
