using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Media;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>Trendo Musik V1/V2/V7 (07.11.2026): Hochladen → gleich trennen, Spuren-Mischer, Modellwahl und Qualitätsmessung.</summary>
public class MusikStems_Ui
{
    sealed class FakeAudio : IAudioOut
    {
        public readonly List<string> Loads = [];
        public event Action<double, double>? Progress { add { } remove { } }
        public event Action? Ended { add { } remove { } }
        public void Load(string file) => Loads.Add(file);
        public void SetPause(bool pause) { } public void Seek(double seconds) { } public void SetVolume(int percent) { } public void Stop() { } public void Dispose() { }
    }

    const int Rate = 44100;
    static float[] Tone(double hz, double amp) => Enumerable.Range(0, Rate * 2).Select(i => (float)(amp * Math.Sin(2 * Math.PI * hz * i / Rate))).ToArray();
    static void Wav(string f, float[] x) => AudioFile.WriteWav(new AudioBuf(Rate, [x, (float[])x.Clone()]), f);
    static double Rms(string f) { var a = AudioFile.Read(f); return Math.Sqrt(a.Data[0].Sum(v => v * (double)v) / a.Length); }

    [AvaloniaFact(DisplayName = "V1/V2/V7 Trendo Musik: hochgeladenes Lied wird gleich getrennt, Qualität gemessen (sauber), Spuren-Mischer spielt in Trendo (Gesang stumm leiser), Mischung als Lied; Modellwahl „Genau“; ohne Auto-Trennen nichts getrennt")]
    public async Task Upload_split_mix()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000; Directory.CreateDirectory(root);
        try
        {
            var stems = new Dictionary<string, float[]> { ["gesang"] = Tone(440, 0.2), ["schlagzeug"] = Tone(80, 0.15), ["bass"] = Tone(110, 0.15), ["rest"] = Tone(660, 0.1) };
            var mixData = Enumerable.Range(0, Rate * 2).Select(i => stems.Values.Sum(s => s[i])).ToArray();
            var src = Path.Combine(root, "Mein Lied.wav"); Wav(src, mixData);
            w.StemSplitOverride = (file, dir) => { Directory.CreateDirectory(dir); foreach (var (n, x) in stems) Wav(Path.Combine(dir, n + ".wav"), x); Wav(Path.Combine(dir, "begleitung.wav"), Enumerable.Range(0, Rate * 2).Select(i => stems["schlagzeug"][i] + stems["bass"][i] + stems["rest"][i]).ToArray()); return Task.FromResult(true); };
            var fake = new FakeAudio(); w.AudioOverride = () => fake;
            w.Show("musik-studio"); Settle();
            w.PickOverride = _ => Task.FromResult<string[]?>([src]);
            w.Find<Button>("ms-import").Press(); // „＋ Eigene Aufnahme“ → gleich trennen
            for (var i = 0; i < 100 && w.Songs.All().Count == 0; i++) { await Task.Delay(20); Settle(); }
            var s = w.Songs.All().First(x => x.Kind == "eigen");
            for (var i = 0; i < 100 && w.Songs.All().Count < 3; i++) { await Task.Delay(20); Settle(); }
            var all = w.Songs.All();
            Assert.Contains(all, x => x.Kind == "begleitung"); Assert.Contains(all, x => x.Kind == "gesang"); // V1: gleich getrennt
            Assert.True(w.MsStemQuality!.Value.Reconstruction >= 40, $"{w.MsStemQuality}"); // V7: Spuren ergeben das Original
            Assert.True(w.MsStemQuality.Value.Bleed < 0.05);
            // V2: Mischer – Gesang stumm
            w.Show("musik-studio"); Settle();
            var full = w.MsStemPlay(s!)!; Assert.Equal(full, fake.Loads[^1]); // spielt in Trendo
            w.Find<Button>("ms-stem-mute-gesang").Press(); Settle();
            var noVocal = w.MsStemPlay(s!)!;
            Assert.True(Rms(noVocal) < Rms(full) * 0.85, $"{Rms(noVocal)} vs {Rms(full)}");
            var saved = w.MsStemSave(s!)!; Assert.Equal("mischung", saved.Kind); Assert.True(File.Exists(saved.File));
            // V8 Karaoke: ohne Text freundlicher Hinweis; mit Text LRC mit vier Zeilen aus vier gesungenen Phrasen, Begleitung läuft
            Assert.Null(await w.MsKaraoke(s!));
            w.Songs.Update(s!.Id, x => x.Lyrics = "[Strophe]\nErste Zeile hier\nZweite Zeile dort\n\n[Refrain]\nDritte Zeile laut\nVierte Zeile leis");
            w.MelodyOverride = _ => Task.FromResult<string?>("{\"hop\":0.05,\"midi\":[" + string.Join(",", Enumerable.Range(0, 400).Select(i => i % 100 < 70 ? "62.0" : "null")) + "]}");
            var lrc = await w.MsKaraoke(w.Songs.Get(s.Id)!);
            Assert.NotNull(lrc); var lines = TimedText.ReadLrc(File.ReadAllText(lrc!));
            Assert.Equal(["Erste Zeile hier", "Zweite Zeile dort", "Dritte Zeile laut", "Vierte Zeile leis"], lines.Take(4).Select(x => x.Text));
            Assert.Equal([0.0, 5.0, 10.0, 15.0], lines.Take(4).Select(x => x.Time));
            Assert.EndsWith("begleitung.wav", fake.Loads[^1]);
            // V7: Modellwahl
            w.Show("musik-studio"); Settle();
            w.Find<ComboBox>("ms-stem-model").SelectedIndex = 1; Assert.Equal("htdemucs_ft", w.MsStemModel);
            // ohne Auto-Trennen
            w.MsAutoSplit = false; var count = w.Songs.All().Count;
            var src2 = Path.Combine(root, "Zweites.wav"); Wav(src2, mixData);
            w.PickOverride = _ => Task.FromResult<string[]?>([src2]); w.Show("musik-studio"); Settle(); w.Find<Button>("ms-import").Press();
            for (var i = 0; i < 50 && w.Songs.All().Count == count; i++) { await Task.Delay(20); Settle(); }
            await Task.Delay(100); Settle();
            Assert.Equal(count + 1, w.Songs.All().Count);
        }
        finally { w.Close(); }
    }
}
