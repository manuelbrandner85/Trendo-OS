using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Trendo.App;
using Trendo.Core.Media;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>Audio Profi 25 (05.11.2026): Mehrspur-Ansicht wie Audition, spektrales Retuschieren wie iZotope RX – in der echten Oberfläche.</summary>
public class AudioPro_Ui
{
    static void Max(MainWindow w) { if (w.Windows.Active is { Maximized: false } pw) w.Windows.ToggleMaximize(pw.Id); Settle(); }
    static void Click(MainWindow w, string name) { var b = V<Button>(w, name); b.BringIntoView(); Settle(); RealClick(w, V<Button>(w, name)); }

    static string Wav(string dir, string name, double sec, params (double Hz, float Amp)[] tones)
    {
        const int rate = 44100; var n = (int)(sec * rate);
        var d = Enumerable.Range(0, n).Select(i => tones.Sum(t => t.Amp * (float)Math.Sin(2 * Math.PI * t.Hz * i / rate))).ToArray();
        var p = Path.Combine(dir, name); AudioFile.WriteWav(new AudioBuf(rate, [d, (float[])d.Clone()]), p); return p;
    }

    [AvaloniaFact(DisplayName = "Audio Mehrspur: Projekt anlegen, zwei Spuren, Clip mit der Maus verschieben, Stumm und Solo per Knopf, Abmischen öffnet eine Spur")]
    public void Multitrack()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000; Directory.CreateDirectory(root);
        try
        {
            var voice = Wav(root, "stimme.wav", 2, (300, 0.3f)); var music = Wav(root, "musik.wav", 2, (1000, 0.3f));
            w.Show("ton"); Settle(); Max(w); RealClick(w, V<Button>(w, "prog-tpl-mehrspur")); Settle();
            Assert.True(AudioMulti.IsMulti(w.ProgramActive("ton")!.Data));
            w.Find<TextBox>("au-file").Text = voice; Click(w, "au-add-track"); Settle();
            w.Find<TextBox>("au-file").Text = music; Click(w, "au-add-track"); Settle();
            var d = w.ProgramActive("ton")!; Assert.Equal(2, AudioMulti.Tracks(d.Data).Count);
            Assert.Equal("Spur hinzufügen", d.UndoLabel);
            // Clip 2 um etwa 1 s nach rechts ziehen
            var lane = w.Find<ClipLane>("au-lane-1"); Settle();
            Point At(double t) => lane.TranslatePoint(new Point(lane.ToX(t), lane.Bounds.Height / 2), w)!.Value;
            w.MouseDown(At(0.5), MouseButton.Left); for (var i = 1; i <= 5; i++) { w.MouseMove(At(0.5 + i / 5.0)); Settle(); } w.MouseUp(At(1.5), MouseButton.Left); Settle();
            var t2 = AudioMulti.Tracks(w.ProgramActive("ton")!.Data)[1]; Assert.InRange(t2.Start, 0.9, 1.1);
            Assert.Equal(3.0, AudioEdit.Render(w.ProgramActive("ton")!.Data).Seconds, 1);
            // Stumm auf Spur 1, dann Solo auf Spur 2
            Click(w, "au-track-mute-0"); Settle();
            Assert.True(AudioMulti.Tracks(w.ProgramActive("ton")!.Data)[0].Mute);
            Click(w, "au-track-mute-0"); Settle(); Click(w, "au-track-solo-1"); Settle();
            var ts = AudioMulti.Tracks(w.ProgramActive("ton")!.Data); Assert.False(ts[0].Mute); Assert.True(ts[1].Solo);
            Assert.False(AudioMulti.Audible(ts, ts[0]));
            // Abmischen: neue einzelne Spur mit der Summe
            Click(w, "au-mixdown"); Settle();
            var single = w.ProgramActive("ton")!;
            Assert.False(AudioMulti.IsMulti(single.Data)); Assert.True(File.Exists(single.Text("source")));
            Assert.Equal(3.0, AudioEdit.Render(single.Data).Seconds, 1);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Audio spektral retuschieren: Spektrogramm an, Rechteck um den Pfeifton ziehen, Bereich absenken – Pfeifton weg, Stimme bleibt, rückgängig")]
    public void Spectral_repair()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000; Directory.CreateDirectory(root);
        try
        {
            var src = Wav(root, "interview.wav", 4, (300, 0.3f), (3000, 0.2f));
            w.Show("ton"); Settle(); Max(w); RealClick(w, V<Button>(w, "prog-tpl-leer")); Settle();
            w.AuOpen(src); w.AuSpectro = true; w.Show("ton"); Settle();
            var wave = w.AuWaveForTests!; Assert.NotNull(wave.Spectro);
            Point At(double t, double hz) => wave.TranslatePoint(new Point(wave.ToX(t), wave.HzToY(hz)), w)!.Value;
            w.MouseDown(At(1, 4000), MouseButton.Left); for (var i = 1; i <= 6; i++) { w.MouseMove(At(1 + 2 * i / 6.0, 4000 - 1700 * i / 6.0)); Settle(); } w.MouseUp(At(3, 2300), MouseButton.Left); Settle();
            Assert.InRange(w.AuSel.A, 0.95, 1.05); Assert.InRange(w.AuSel.B, 2.95, 3.05);
            Assert.InRange(w.AuFreq.Lo, 2100, 2500); Assert.InRange(w.AuFreq.Hi, 3700, 4300);
            Assert.Contains("Bereich absenken", w.ProgramMessage("ton"));
            w.Find<Slider>("au-spectral-db").Value = -40;
            Click(w, "au-spectral-apply"); Settle();
            var d = w.ProgramActive("ton")!; Assert.Equal("Spektral absenken", d.UndoLabel);
            var a = AudioEdit.Render(d.Data).Data[0];
            double Tone(double hz, int s0, int s1) { double om = 2 * Math.PI * hz / 44100, c = 2 * Math.Cos(om), q1 = 0, q2 = 0; for (var i = s0; i < s1; i++) { var q0 = a[i] + c * q1 - q2; q2 = q1; q1 = q0; } return 2 * Math.Sqrt(Math.Max(0, q1 * q1 + q2 * q2 - c * q1 * q2)) / (s1 - s0); }
            Assert.True(Tone(3000, 60000, 120000) < 0.01, $"Pfeifton: {Tone(3000, 60000, 120000)}");
            Assert.InRange(Tone(300, 60000, 120000), 0.27, 0.33);
            Assert.InRange(Tone(3000, 150000, 170000), 0.18, 0.22); // nach der Auswahl unverändert
            // Klick ohne senkrechtes Ziehen: normale Zeitauswahl, Frequenz-Auswahl weg
            wave = w.AuWaveForTests!; Settle();
            w.MouseDown(At(0.5, 1000), MouseButton.Left); w.MouseMove(At(1.5, 1000)); Settle(); w.MouseUp(At(1.5, 1000), MouseButton.Left); Settle();
            Assert.True(w.AuFreq.Hi <= w.AuFreq.Lo);
            Click(w, "prog-cmd-au-spectral"); Settle();
            Assert.Contains("Rechteck", w.ProgramMessage("ton"));
            w.ProgramActive("ton")!.Undo(); Assert.Empty(w.ProgramActive("ton")!.List("ops"));
        }
        finally { w.Close(); }
    }
}
