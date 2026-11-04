using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Trendo.App;
using Trendo.Core.Media;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>Audio-Editor (selbst nachgebaut statt Audacity, 04.11.2026): öffnen, mit der Maus wählen, schneiden, Effekte, anhören, rückgängig, exportieren.</summary>
public class AudioEditor_Ui
{
    sealed class FakeAudio : Trendo.App.IAudioOut
    {
        public readonly List<string> Loads = [];
        public event Action<double, double>? Progress { add { } remove { } }
        public event Action? Ended { add { } remove { } }
        public void Load(string file) => Loads.Add(file);
        public void SetPause(bool pause) { } public void Seek(double seconds) { } public void SetVolume(int percent) { } public void Stop() { } public void Dispose() { }
    }

    static string Tone(string dir, string name, double seconds, float amp = 0.3f)
    {
        const int rate = 8000; var n = (int)(seconds * rate);
        var d = Enumerable.Range(0, 2).Select(_ => Enumerable.Range(0, n).Select(i => amp * (float)Math.Sin(2 * Math.PI * 440 * i / rate)).ToArray()).ToArray();
        var p = Path.Combine(dir, name); AudioFile.WriteWav(new AudioBuf(rate, d), p); return p;
    }

    [AvaloniaFact(DisplayName = "Audio-Editor UI: WAV öffnen, Bereich mit der Maus ziehen, Löschen, Normalisieren, Einblenden, Kopieren/Einfügen, Abspielen, Rückgängig, Export WAV")]
    public void Edit_audio()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000; Directory.CreateDirectory(root);
        try
        {
            var fake = new FakeAudio(); w.AudioOverride = () => fake;
            var src = Tone(root, "aufnahme.wav", 6); var orig = File.ReadAllBytes(src);
            w.Show("ton"); Settle();
            RealClick(w, V<Button>(w, "prog-tpl-leer")); Settle();
            Assert.Empty(w.AuWaveForTests!.Peaks); // leere Fläche: noch kein Ton
            w.AuOpen(src); w.Show("ton"); Settle();
            var d = w.ProgramActive("ton")!;
            Assert.Equal("aufnahme", d.Title);
            Assert.Contains("6,00", w.Find<TextBlock>("au-time").Text);
            // Bereich 1 s – 3 s mit der Maus ziehen
            var wave = w.AuWaveForTests!; Settle();
            Point At(double t) => wave.TranslatePoint(new Point(wave.ToX(t), wave.Bounds.Height / 2), w)!.Value;
            w.MouseDown(At(1), MouseButton.Left); for (var i = 1; i <= 6; i++) { w.MouseMove(At(1 + 2 * i / 6.0)); Settle(); } w.MouseUp(At(3), MouseButton.Left); Settle();
            Assert.InRange(w.AuSel.A, 0.95, 1.05); Assert.InRange(w.AuSel.B, 2.95, 3.05);
            RealClick(w, V<Button>(w, "prog-cmd-au-del")); Settle();
            Assert.Equal(4.0, AudioEdit.Render(w.ProgramActive("ton")!.Data).Seconds, 1);
            Assert.Equal("Löschen", w.ProgramActive("ton")!.UndoLabel);
            // ohne Auswahl wirkt Normalisieren auf alles
            w.AuSel = default; RealClick(w, V<Button>(w, "prog-cmd-au-norm")); Settle();
            Assert.InRange(AudioEdit.PeakDb(AudioEdit.Render(w.ProgramActive("ton")!.Data)), -1.1, -0.9);
            RealClick(w, V<Button>(w, "prog-cmd-au-fadein")); Settle();
            Assert.True(AudioEdit.PeakDb(AudioEdit.Render(w.ProgramActive("ton")!.Data), 0, 0.05) < -20);
            // Kopieren und an der Marke einfügen
            w.AuSel = (3, 3.5); w.Show("ton"); Settle(); RealClick(w, V<Button>(w, "prog-cmd-au-copy")); Settle();
            w.AuSel = (0, 0); w.Show("ton"); Settle(); RealClick(w, V<Button>(w, "prog-cmd-au-paste")); Settle();
            Assert.Equal(4.5, AudioEdit.Render(w.ProgramActive("ton")!.Data).Seconds, 1);
            // Anhören lädt eine Datei in den Abspieler
            RealClick(w, V<Button>(w, "au-tp-play")); Settle();
            Assert.Single(fake.Loads); Assert.True(File.Exists(fake.Loads[0]));
            // Schritte-Liste und Rückgängig
            Assert.Contains(w.GetVisualDescendants().OfType<TextBlock>(), t => (t.Text ?? "").Contains("Schritte (4)"));
            w.ProgramActive("ton")!.Undo(); w.Show("ton"); Settle();
            Assert.Equal(4.0, AudioEdit.Render(w.ProgramActive("ton")!.Data).Seconds, 1);
            // ohne Auswahl meldet Löschen freundlich, statt alles zu löschen
            w.AuSel = default; w.Show("ton"); Settle(); RealClick(w, V<Button>(w, "prog-cmd-au-del")); Settle();
            Assert.Equal(4.0, AudioEdit.Render(w.ProgramActive("ton")!.Data).Seconds, 1);
            Assert.Contains("Bereich", w.ProgramMessage("ton"));
            // Export WAV; Original unverändert
            var outF = Path.Combine(root, "fertig.wav");
            MainWindow.Programs["ton"].Exports.First(e => e.Name == "wav").Write(w.ProgramActive("ton")!, outF);
            Assert.Equal(4.0, AudioFile.Read(outF).Seconds, 1);
            Assert.Equal(orig, File.ReadAllBytes(src));
            // falsches Format: deutsche Meldung
            var bad = Path.Combine(root, "x.aiff"); File.WriteAllText(bad, "x");
            Assert.Contains("WAV oder MP3", Assert.Throws<InvalidOperationException>(() => w.AuOpen(bad)).Message);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Audio-Editor Reparieren: ohne Profil freundlicher Hinweis, Rauschprofil merken, Rauschen entfernen, Brummen, Stille kürzen, Lautheit −16 LUFS, Spektrogramm, Aufnahme an der Marke")]
    public async Task Repair()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000; Directory.CreateDirectory(root);
        try
        {
            const int rate = 22050; var rnd = new Random(2); var a = AudioBuf.Silence(4, rate, 2);
            for (var i = 0; i < a.Length; i++) { var v = (float)((rnd.NextDouble() - 0.5) * 0.03 + (i > rate * 2 ? 0.3 * Math.Sin(2 * Math.PI * 440 * i / rate) : 0)); a.Data[0][i] = v; a.Data[1][i] = v; }
            var src = Path.Combine(root, "sprache.wav"); AudioFile.WriteWav(a, src);
            w.Show("ton"); Settle(); RealClick(w, V<Button>(w, "prog-tpl-leer")); Settle();
            w.AuOpen(src); w.Show("ton"); Settle();
            RealClick(w, V<Button>(w, "prog-cmd-au-denoise")); Settle();
            Assert.Contains("Rauschprofil", w.ProgramMessage("ton"));
            w.AuSel = (0.1, 1.5); w.Show("ton"); Settle(); RealClick(w, V<Button>(w, "prog-cmd-au-noise-profile")); Settle();
            w.AuSel = default; w.Show("ton"); Settle(); RealClick(w, V<Button>(w, "prog-cmd-au-denoise")); Settle();
            var d = w.ProgramActive("ton")!; Assert.Equal("Rauschen entfernen (Profil)", d.UndoLabel);
            var after = AudioEdit.Render(d.Data);
            Assert.True(AudioEdit.PeakDb(after, 0.3, 1.8) < AudioEdit.PeakDb(AudioFile.Read(src), 0.3, 1.8) - 8);
            RealClick(w, V<Button>(w, "prog-cmd-au-dehum")); Settle(); RealClick(w, V<Button>(w, "prog-cmd-au-loud-podcast")); Settle();
            Assert.InRange(AudioDsp.Lufs(AudioEdit.Render(w.ProgramActive("ton")!.Data)), -17, -15);
            RealClick(w, V<Button>(w, "prog-cmd-au-spectro")); Settle();
            Assert.NotNull(w.AuWaveForTests!.Spectro);
            w.MicRecordOverride = sec => { var f = Path.Combine(root, "mic.wav"); AudioFile.WriteWav(AudioBuf.Silence(sec, rate, 1), f); return Task.FromResult<string?>(f); };
            var len = AudioEdit.Render(w.ProgramActive("ton")!.Data).Seconds;
            await w.AuRecord(w.ProgramActive("ton")!, 2); Settle();
            Assert.Equal(len + 2, AudioEdit.Render(w.ProgramActive("ton")!.Data).Seconds, 1);
        }
        finally { w.Close(); }
    }
}
