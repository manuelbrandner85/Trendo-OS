using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Media;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0732 „Audio bearbeiten“: Aufnahme bis Stopp mit Pegel (echtes FFmpeg; ersetzt ist nur das Mikrofon durch eine FFmpeg-Tonquelle in Echtzeit),
/// Leertaste startet/hält die Wiedergabe (Abspieler ersetzt – mpv-Lauf belegt <c>MpvWindowTests</c>), Effekte mit einstellbaren Werten.</summary>
public class TonT0732_Ui
{
    sealed class FakeAudio : IAudioOut
    {
        public readonly List<string> Loads = []; public int Stops;
        public event Action<double, double>? Progress { add { } remove { } }
        public event Action? Ended { add { } remove { } }
        public void Load(string file) => Loads.Add(file);
        public void SetPause(bool pause) { } public void Seek(double seconds) { } public void SetVolume(int percent) { } public void Stop() => Stops++; public void Dispose() { }
    }

    static string Tone(string dir, string name, double seconds, float amp = 0.25f)
    {
        const int rate = 44100; var n = (int)(seconds * rate);
        var d = Enumerable.Range(0, 1).Select(_ => Enumerable.Range(0, n).Select(i => amp * (float)Math.Sin(2 * Math.PI * 440 * i / rate)).ToArray()).ToArray();
        var p = Path.Combine(dir, name); AudioFile.WriteWav(new AudioBuf(rate, d), p); return p;
    }

    static void Key1(MainWindow w, Key k, PhysicalKey ph) { w.KeyPress(k, RawInputModifiers.None, ph, null); w.KeyRelease(k, RawInputModifiers.None, ph, null); Settle(); }

    [AvaloniaFact(DisplayName = "T-0732 Ton (UI): „Aufnehmen“ läuft bis „Stopp“ (länger als je), Pegel und Zeit sichtbar, Aufnahme an der Marke eingefügt – echtes FFmpeg")]
    public async Task Record_until_stop()
    {
        if (Cli.Find("ffmpeg") is null) { Assert.Skip("Übersprungen – FFmpeg fehlt auf diesem PC."); return; }
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000; Directory.CreateDirectory(root);
        try
        {
            w.MicInputOverride = () => ["-re", "-f", "lavfi", "-i", "sine=frequency=440:sample_rate=44100:duration=600"];
            var src = Tone(root, "sprache.wav", 2);
            w.Show("ton"); Settle();
            RealClick(w, V<Button>(w, "prog-tpl-leer")); Settle();
            w.AuOpen(src); w.AuSel = (2, 2); w.Show("ton"); Settle();
            var d = w.ProgramActive("ton")!;
            w.AuRecordStart(d); w.Show("ton"); Settle();
            Assert.True(w.AuRecording);
            var t = System.Diagnostics.Stopwatch.StartNew();
            while (w.AuRecSeconds < 3.2 && t.Elapsed < TimeSpan.FromSeconds(30)) { await Task.Delay(50); Settle(); }
            Assert.True(w.AuRecording, "läuft weiter, bis Stopp gedrückt wird");
            Assert.InRange(w.AuRecLevel, 0.05, 1);
            Assert.Contains("0:03", V<TextBlock>(w, "au-rec-time").Text); // Zeit läuft sichtbar mit
            Assert.True(V<Border>(w, "au-rec-meter").Width > 10, "Pegelbalken ausgeschlagen");
            RealClick(w, V<Button>(w, "au-rec-stop"));
            t.Restart(); while (w.AuRecording || w.ProgramActive("ton")!.List("ops").Count == 0) { if (t.Elapsed > TimeSpan.FromSeconds(30)) break; await Task.Delay(50); Settle(); }
            Assert.False(w.AuRecording);
            var after = AudioEdit.Render(w.ProgramActive("ton")!.Data);
            Assert.InRange(after.Seconds, 2 + 3.0, 2 + 6.0); // Original 2 s + Aufnahme > 3 s (nicht fest 10 s)
            Assert.Equal("Datei einfügen", w.ProgramActive("ton")!.UndoLabel);
            var rec = Directory.GetFiles(Path.Combine(w.MediaFolder, "Aufnahmen"), "*.wav").Single();
            Assert.InRange(AudioFile.Read(rec).Seconds, 3.0, 6.0);
            Assert.Throws<InvalidOperationException>(() => { w.MicInputOverride = null; w.AuRecordStart(d); }); // Testmodus ohne Quelle: ehrliche Meldung statt stiller Simulation
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0732 Ton (UI): Leertaste startet und hält die Wiedergabe an – auf der Welle, nicht beim Tippen in Feldern")]
    public void Space_plays_and_stops()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000; Directory.CreateDirectory(root);
        try
        {
            var fake = new FakeAudio(); w.AudioOverride = () => fake;
            var src = Tone(root, "musik.wav", 4);
            w.Show("ton"); Settle(); RealClick(w, V<Button>(w, "prog-tpl-leer")); Settle();
            w.AuOpen(src); w.Show("ton"); Settle();
            var wave = w.AuWaveForTests!;
            var p = wave.TranslatePoint(new Point(wave.ToX(1), wave.Bounds.Height / 2), w)!.Value;
            w.MouseDown(p, MouseButton.Left); w.MouseUp(p, MouseButton.Left); Settle(); // Marke bei 1 s
            Assert.Same(w.AuWaveForTests, TopLevel.GetTopLevel(w)!.FocusManager!.GetFocusedElement()); // nach dem Neuaufbau hat die Welle den Fokus
            Key1(w, Key.Space, PhysicalKey.Space);
            Assert.Single(fake.Loads); Assert.True(w.AuPlaying);
            Assert.InRange(AudioFile.Read(fake.Loads[0]).Seconds, 2.9, 3.1); // ab der Marke bis zum Ende
            var stops = fake.Stops;
            Key1(w, Key.Space, PhysicalKey.Space);
            Assert.False(w.AuPlaying); Assert.True(fake.Stops > stops);
            Key1(w, Key.Space, PhysicalKey.Space);
            Assert.Equal(2, fake.Loads.Count);
            // beim Tippen in einem Zahlenfeld gehört die Leertaste dem Feld
            Key1(w, Key.Space, PhysicalKey.Space); // anhalten
            var n = V<NumericUpDown>(w, "au-fx-gain"); n.Focus(); Settle();
            Key1(w, Key.Space, PhysicalKey.Space);
            Assert.Equal(2, fake.Loads.Count);
            // derselbe Eintrag steht in der Menüleiste „Wiedergabe“ mit dem Kürzel
            Assert.Contains(MainWindow.Programs["ton"].Menus!(w.ProgramActive("ton")!).SelectMany(m => m.Items), c => c.Id == "au-key-play" && c.Shortcut == "Leertaste");
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0732 Ton (UI): Effekt-Werte einstellbar – „Lauter“ um 6 dB, Echo-Abstand, Tiefen weg bei 200 Hz, Stille 2,5 s; Rückgängig")]
    public void Effects_with_values()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000; Directory.CreateDirectory(root);
        try
        {
            var src = Tone(root, "ton.wav", 3, 0.1f);
            w.Show("ton"); Settle(); RealClick(w, V<Button>(w, "prog-tpl-leer")); Settle();
            w.AuOpen(src); w.Show("ton"); Settle();
            double Peak() => AudioEdit.PeakDb(AudioEdit.Render(w.ProgramActive("ton")!.Data));
            var before = Peak();
            V<NumericUpDown>(w, "au-fx-gain").Value = 6; Settle();
            Assert.Equal(6, w.AuFx.GainDb);
            RealClick(w, V<Button>(w, "prog-cmd-au-louder")); Settle();
            Assert.InRange(Peak() - before, 5.9, 6.1);
            var ops = w.ProgramActive("ton")!.List("ops");
            Assert.Equal(6, ProgramDocument_Number(ops[^1]!["v"]), 3);
            w.ProgramActive("ton")!.Undo(); w.Show("ton"); Settle();
            Assert.InRange(Peak() - before, -0.1, 0.1);
            // Stille einfügen mit eingestellter Länge
            V<NumericUpDown>(w, "au-fx-sil").Value = 2.5m; Settle();
            w.AuSel = (1, 1); MainWindow.Programs["ton"].Ribbon.SelectMany(g => g.Cmds).First(c => c.Name == "au-sil1").Run(w.ProgramActive("ton")!); Settle();
            Assert.Equal(5.5, AudioEdit.Render(w.ProgramActive("ton")!.Data).Seconds, 2);
            // Tiefen weg bei 200 Hz und Echo-Abstand landen als Wert im Schritt
            V<NumericUpDown>(w, "au-fx-hp").Value = 200; V<NumericUpDown>(w, "au-fx-echo").Value = 0.5m; Settle();
            w.AuSel = default;
            MainWindow.Programs["ton"].Ribbon.SelectMany(g => g.Cmds).First(c => c.Name == "au-hp").Run(w.ProgramActive("ton")!);
            MainWindow.Programs["ton"].Ribbon.SelectMany(g => g.Cmds).First(c => c.Name == "au-echo").Run(w.ProgramActive("ton")!); Settle();
            var o2 = w.ProgramActive("ton")!.List("ops");
            Assert.Equal(200, ProgramDocument_Number(o2[^2]!["v"]), 3); Assert.Equal(0.5, ProgramDocument_Number(o2[^1]!["v"]), 3);
        }
        finally { w.Close(); }
    }

    static double ProgramDocument_Number(System.Text.Json.Nodes.JsonNode? n) => Trendo.Core.Programs.ProgramDocument.ToNumber(n);
}

/// <summary>T-0732 „Medien bearbeiten“: Stapel – Dateien aus dem Explorer auf die Werkbank ziehen, Aufgabe wählen, Start, Fortschritt je Datei,
/// Ergebnisse im Ausgabeordner (echtes FFmpeg, ersetzt ist nichts außer der Ordnerauswahl über <see cref="MainWindow.PickOverride"/>).</summary>
public class MedienStapelT0732_Ui
{
    static string Video(string dir, string name, double seconds, string size = "320x240")
    {
        var p = Path.Combine(dir, name);
        Assert.True(Cli.Run("ffmpeg", ["-hide_banner", "-loglevel", "error", "-f", "lavfi", "-i", $"testsrc=duration={seconds}:size={size}:rate=25", "-f", "lavfi", "-i", $"sine=frequency=440:duration={seconds}",
            "-c:v", "libx264", "-preset", "ultrafast", "-c:a", "aac", "-shortest", p], dir, 120000).Ok);
        return p;
    }

    static async Task Wait(MainWindow w, Func<bool> done, int seconds = 120)
    {
        var t = System.Diagnostics.Stopwatch.StartNew();
        while (!done() && t.Elapsed < TimeSpan.FromSeconds(seconds)) { await Task.Delay(50); Settle(); }
        Settle();
    }

    [AvaloniaFact(DisplayName = "T-0732 Medien (UI): drei Videos und ein Bild auf die Werkbank ziehen, „Ton herausholen“, Ausgabeordner wählen, Start – Fortschritt je Datei, MP3s im Ordner")]
    public async Task Batch_drop_and_run()
    {
        if (Cli.Find("ffmpeg") is null) { Assert.Skip("Übersprungen – FFmpeg fehlt auf diesem PC."); return; }
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000; Directory.CreateDirectory(root);
        try
        {
            var src = Path.Combine(root, "quelle"); Directory.CreateDirectory(src);
            var files = new[] { Video(src, "urlaub 1.mp4", 2), Video(src, "urlaub 2.mp4", 3), Video(src, "urlaub 3.mp4", 2) };
            var img = Path.Combine(src, "foto.png"); Assert.True(Cli.Run("ffmpeg", ["-loglevel", "error", "-f", "lavfi", "-i", "testsrc=size=64x64", "-frames:v", "1", img], src).Ok);
            var outDir = Path.Combine(root, "fertig");
            w.PickOverride = r => Task.FromResult<string[]?>(r.Field == "media-batch-out" ? [outDir] : null);
            w.Show("kreativ-medien"); Settle();
            BueroAusbau_Ui.Drop(w, "media-batch-drop", [.. files, img]);
            Assert.Equal(4, w.MediaBatchItems.Count);
            Assert.Equal(4, w.GetVisualDescendants().OfType<Grid>().Count(g => g.Name?.StartsWith("media-batch-item-", StringComparison.Ordinal) == true));
            V<ComboBox>(w, "media-batch-job").SelectedIndex = MediaStudio.Jobs.ToList().FindIndex(j => j.Id == "mp3"); Settle();
            BueroAusbau_Ui.Click(w, "media-batch-outpick"); await Wait(w, () => w.MediaBatchOut == outDir, 5);
            Assert.Contains(outDir, V<TextBlock>(w, "media-batch-out").Text);
            BueroAusbau_Ui.Click(w, "media-batch-start"); Settle();
            Assert.True(w.MediaBatchRunning || w.MediaBatchItems.All(i => i.State != MediaBatchState.Wartet));
            await Wait(w, () => !w.MediaBatchRunning);
            await Wait(w, () => V<TextBlock>(w, "media-batch-state-0").Text == "fertig", 5);
            Assert.Equal(3, w.MediaBatchItems.Count(i => i.State == MediaBatchState.Fertig));
            Assert.Equal(MediaBatchState.Uebersprungen, w.MediaBatchItems[3].State);
            var mp3 = Directory.GetFiles(outDir, "*.mp3").OrderBy(x => x).ToArray();
            Assert.Equal(3, mp3.Length);
            Assert.InRange(MediaStudio.Duration("ffmpeg", mp3[1]) ?? 0, 2.8, 3.3);
            Assert.Equal(100, V<ProgressBar>(w, "media-batch-bar-0").Value);
            Assert.Contains("übersprungen", V<TextBlock>(w, "media-batch-state-3").Text);
            Assert.Contains("Stapel fertig: 3 von 4", ((TextBlock)w.Find<Border>("w45-msg").Child!).Text);
            Assert.All(files, f => Assert.True(File.Exists(f)));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0732 Medien (UI): „Abbrechen“ hält den Stapel mitten in einer Datei an – keine halbe Datei im Ausgabeordner")]
    public async Task Batch_cancel()
    {
        if (Cli.Find("ffmpeg") is null) { Assert.Skip("Übersprungen – FFmpeg fehlt auf diesem PC."); return; }
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000; Directory.CreateDirectory(root);
        try
        {
            var src = Path.Combine(root, "quelle"); Directory.CreateDirectory(src);
            var files = new[] { Video(src, "lang1.mp4", 40, "1280x720"), Video(src, "lang2.mp4", 40, "1280x720") };
            var outDir = Path.Combine(root, "fertig"); w.MediaBatchOut = outDir;
            w.Show("kreativ-medien"); Settle();
            w.MediaBatchAdd(files); w.MediaBatchJob = "small"; Settle();
            BueroAusbau_Ui.Click(w, "media-batch-start");
            await Wait(w, () => w.MediaBatchItems[0].Fraction > 0.02, 60);
            Assert.True(w.MediaBatchRunning);
            Assert.InRange(V<ProgressBar>(w, "media-batch-bar-0").Value, 1, 99); // Balken der ersten Datei bewegt sich
            BueroAusbau_Ui.Click(w, "media-batch-cancel");
            await Wait(w, () => !w.MediaBatchRunning, 30);
            Assert.All(w.MediaBatchItems, i => Assert.Equal(MediaBatchState.Abgebrochen, i.State));
            await Task.Delay(300);
            Assert.Empty(Directory.Exists(outDir) ? Directory.GetFiles(outDir) : []);
            Assert.Contains("abgebrochen", ((TextBlock)w.Find<Border>("w45-msg").Child!).Text);
        }
        finally { w.Close(); }
    }
}
