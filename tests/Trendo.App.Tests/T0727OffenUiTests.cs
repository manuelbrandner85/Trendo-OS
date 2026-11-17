using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Media;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0727 (offene Probleme, Gruppe B „Leistung und Ansicht“): 3D-Ansicht im niedrigen Programmfenster, Spuren-Mischer ohne Demucs.</summary>
public class T0727_Offen_Ui
{
    static T Get<T>(MainWindow w, string name) where T : Control { Settle(); return w.GetVisualDescendants().OfType<T>().Last(c => c.Name == name && c.IsEffectivelyVisible && c.Bounds.Width > 0); }
    static bool Has(MainWindow w, string name) { Settle(); return w.GetVisualDescendants().OfType<Control>().Any(c => c.Name == name && c.IsEffectivelyVisible); }
    static void Press(MainWindow w, string name) { var b = Get<Button>(w, name); b.BringIntoView(); Settle(); RealClick(w, Get<Button>(w, name)); Settle(); }

    [AvaloniaTheory(DisplayName = "T-0727 B3: 3D-Ansicht im Programmfenster wird nie unter ihre Mindesthöhe gedrückt – die Seite lässt sich stattdessen verschieben")]
    [InlineData(1366, 768)]
    [InlineData(1600, 900)]
    [InlineData(1600, 1000)]
    public void Viewport_keeps_min_height(int width, int height)
    {
        var (w, _, _) = Ui.Open(); w.Width = width; w.Height = height;
        try
        {
            w.Show("3d"); w.Studio3DAddShape("cube"); w.Show("3d"); Settle();
            var vp = Get<CadView>(w, "3d-viewport");
            var stage = Get<Border>(w, "3d-stage-drop");
            // vorher: Fläche der Ansicht (Rahmen „3d-stage-drop“) bei 1366 × 768 rund 150 px, bei 1600 × 900 rund 180 px hoch – der Rest abgeschnitten
            Assert.True(stage.Bounds.Height >= MainWindow.MinStage3D - 0.5, $"{width}×{height}: Ansicht nur {stage.Bounds.Height:0} px hoch (sichtbar mindestens {MainWindow.MinStage3D:0})");
            var sv = Get<ScrollViewer>(w, "page-minfill");
            if (sv.Extent.Height > sv.Viewport.Height + 1)
            {
                // niedriges Fenster: verschieben bringt den unteren Rand der Ansicht ins Bild
                sv.Offset = new Avalonia.Vector(0, sv.Extent.Height - sv.Viewport.Height); Settle();
                var bottom = stage.TranslatePoint(new Avalonia.Point(0, stage.Bounds.Height), sv)!.Value.Y;
                Assert.InRange(bottom, 0, sv.Viewport.Height + 1);
            }
        }
        finally { w.Close(); }
    }

    sealed class FakeAudio : IAudioOut
    {
        public event Action<double, double>? Progress { add { } remove { } }
        public event Action? Ended { add { } remove { } }
        public void Load(string file) { }
        public void SetPause(bool pause) { } public void Seek(double seconds) { } public void SetVolume(int percent) { } public void Stop() { } public void Dispose() { }
    }

    const int Rate = 44100;
    static float[] Tone(double hz, double amp) => Enumerable.Range(0, Rate).Select(i => (float)(amp * Math.Sin(2 * Math.PI * hz * i / Rate))).ToArray();
    static void Wav(string f, float[] x) => AudioFile.WriteWav(new AudioBuf(Rate, [x, (float[])x.Clone()]), f);

    [AvaloniaFact(DisplayName = "T-0727 B5: Schon getrenntes Lied zeigt ohne Demucs den Spuren-Mischer (nicht nur „Einrichten“) – Stumm wirkt auf die echten Spur-Dateien")]
    public async Task Mixer_without_demucs()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1100;
        try
        {
            var stems = new Dictionary<string, float[]> { ["gesang"] = Tone(440, 0.2), ["schlagzeug"] = Tone(80, 0.15), ["bass"] = Tone(110, 0.15), ["rest"] = Tone(660, 0.1) };
            var src = Path.Combine(root, "Mein Lied.wav"); Wav(src, Enumerable.Range(0, Rate).Select(i => stems.Values.Sum(s => s[i])).ToArray());
            // Trennen wie früher geschehen (Demucs ersetzt: schreibt die fünf Spuren als echte WAV-Dateien)
            w.StemSplitOverride = (file, dir) => { Directory.CreateDirectory(dir); foreach (var (n, x) in stems) Wav(Path.Combine(dir, n + ".wav"), x); Wav(Path.Combine(dir, "begleitung.wav"), Enumerable.Range(0, Rate).Select(i => stems["schlagzeug"][i] + stems["bass"][i] + stems["rest"][i]).ToArray()); return Task.FromResult(true); };
            w.AudioOverride = () => new FakeAudio();
            w.Show("musik-studio"); Settle();
            w.PickOverride = _ => Task.FromResult<string[]?>([src]);
            w.Find<Button>("ms-import").Press();
            for (var i = 0; i < 200 && w.Songs.All().Count < 3; i++) { await Task.Delay(20); Settle(); }
            var song = w.Songs.All().First(x => x.Kind == "eigen");
            Assert.NotNull(w.MsStemDir(song));

            w.StemSplitOverride = null; // jetzt ist Demucs nicht (mehr) eingerichtet – das Testfenster hat eigene, leere Werkzeuge
            w.MusicStudioSelect(song.Id); w.Show("musik-studio"); Press(w, "ms-dtab-spuren");
            Assert.True(Has(w, "ms-stems-setup"), "„Einrichten“ fürs Neu-Trennen fehlt");
            Assert.True(Has(w, "ms-stem-mixer"), "Mischer fehlt, obwohl die Spuren vorliegen");
            Assert.False(Has(w, "ms-stems"), "„Neu trennen“ ohne Demucs angeboten");
            var before = w.MsStemLink.Get()?.ToJsonString() ?? "-";
            Press(w, "ms-stem-mute-gesang");
            Assert.NotEqual(before, w.MsStemLink.Get()?.ToJsonString() ?? "-");
            var saved = w.MsStemSave(w.Songs.Get(song.Id)!)!; // Mischung aus den echten Spur-Dateien
            Assert.True(File.Exists(saved.File)); Assert.True(AudioFile.Read(saved.File).Length > 0);
        }
        finally { if (w.IsVisible) w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0727 B6: Testfenster ohne Modelle wählt wie ein frischer PC das kleine Standardmodell (gewollt); mit Ui.UseRealModels über den Weg der App das echte beste Modell (ECHT, nur wenn Ollama läuft)")]
    public void Test_helper_models()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            _ = w.InstalledModels;
            Assert.Equal("keine", w.LastModelScan!.Source); // isoliert: kein Ollama gefragt, leerer Werkzeugordner
            Assert.Equal(DependencyCatalog.LocalModel, w.KiModelChoice.Model);
            Assert.Contains("noch kein allgemeines Sprachmodell", w.KiModelChoice.Reason);
            var tags = Task.Run(() => new OllamaBackend().InstalledAsync()).GetAwaiter().GetResult();
            if (tags is not { Count: > 0 }) Assert.Skip("Ollama läuft nicht – Echtteil übersprungen.");
            Ui.UseRealModels(w);
            _ = w.InstalledModels;
            Assert.Equal("ollama-api", w.LastModelScan!.Source);
            var vram = SystemMetrics.GpuMemory()?.TotalGb ?? 0;
            var expected = KiModel.Choose(w.Core.DataRoot, GC.GetGCMemoryInfo().TotalAvailableMemoryBytes, vram, tags).Model;
            Assert.Equal(expected, w.KiModelChoice.Model);
            Console.WriteLine($"T-0727 B6 echt: {tags.Count} Modelle von Ollama, Grafikspeicher {vram:0.0} GB → Standard {w.KiModelChoice.Model}");
        }
        finally { w.OllamaTagsOverride = null; w.VramOverride = null; w.Close(); }
    }
}
