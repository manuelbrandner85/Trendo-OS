using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core.Media;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>Paket 01 – Studio 4, S4-20 (06.11.2026): Prüfen und Freigeben im Audioeditor.</summary>
public class AudioQa_Ui
{
    static string Tone(string dir, string name, double seconds, double amp, bool clamp = false)
    {
        const int rate = 48000; var n = (int)(seconds * rate);
        var d = Enumerable.Range(0, 2).Select(_ => Enumerable.Range(0, n).Select(i => (float)Math.Clamp(amp * Math.Sin(2 * Math.PI * 440 * i / rate), clamp ? -1 : -9, clamp ? 1 : 9)).ToArray()).ToArray();
        var p = Path.Combine(dir, name); AudioFile.WriteWav(new AudioBuf(rate, d), p, 24); return p;
    }

    [AvaloniaFact(DisplayName = "S4-20 Audioeditor: ohne Lizenz und bei Übersteuerung keine Freigabe (Grund steht da, kein Ordner); mit Angabe Freigabe mit WAV 24 Bit und Prüfbericht")]
    public void Qa_blocks_then_releases()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000; Directory.CreateDirectory(root);
        try
        {
            w.Show("ton"); Settle(); RealClick(w, V<Button>(w, "prog-tpl-leer")); Settle();
            w.AuOpen(Tone(root, "laut.wav", 2, 1.6, clamp: true)); w.Show("ton"); Settle();
            w.Find<TextBox>("au-license").Text = "eigene Aufnahme";
            var rel = w.Find<Button>("au-release-run"); rel.BringIntoView(); Settle(); rel.Press(); Settle();
            Assert.True(w.AuQaLast!.Blocked); Assert.Contains("Übersteuert", w.ProgramMessage("ton"));
            Assert.Empty(Directory.GetDirectories(root, "Freigabe*", SearchOption.AllDirectories));
            // saubere Aufnahme, aber ohne Lizenz
            w.AuOpen(Tone(root, "sauber.wav", 3, 0.25)); w.Show("ton"); Settle();
            var d = w.ProgramActive("ton")!; d.Edit("leer", x => x["license"] = ""); w.Show("ton"); Settle();
            rel = w.Find<Button>("au-release-run"); rel.BringIntoView(); Settle(); rel.Press(); Settle();
            Assert.Contains("Lizenz fehlen", w.ProgramMessage("ton"));
            // KI-Stimme ohne Einwilligung
            w.Find<TextBox>("au-license").Text = "eigene Aufnahme"; w.Find<CheckBox>("au-voice").IsChecked = true;
            rel = w.Find<Button>("au-release-run"); rel.Press(); Settle();
            Assert.Contains("Einwilligung", w.ProgramMessage("ton"));
            w.Find<CheckBox>("au-consent").IsChecked = true; w.Find<ComboBox>("au-qa-target").SelectedIndex = 1;
            rel = w.Find<Button>("au-release-run"); rel.Press(); Settle();
            Assert.Contains("Freigegeben", w.ProgramMessage("ton"));
            var folder = Directory.GetDirectories(root, "Freigabe*", SearchOption.AllDirectories).Single();
            var wav = Directory.GetFiles(folder, "*.wav").Single();
            Assert.Equal(24, AudioQa.WavBits(File.ReadAllBytes(wav)));
            var rep = File.ReadAllText(Path.Combine(folder, "Prüfbericht.md"));
            Assert.Contains("Sprache / Podcast", rep); Assert.Contains(FilmDelivery.Sha256(wav), rep); Assert.Contains("Einwilligung liegt vor", rep);
            Assert.Equal("podcast", w.ProgramActive("ton")!.Text("qaTarget"));
        }
        finally { w.Close(); }
    }
}
