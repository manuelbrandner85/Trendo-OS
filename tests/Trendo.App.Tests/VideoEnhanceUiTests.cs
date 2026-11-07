using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Media;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>Paket 01 – Studio 3, S3-12 (06.11.2026): „Bild verbessern“ je Stück – messen, erst auf Wunsch übernehmen, rückgängig machbar; Artefakte nie übernehmen.</summary>
public class VideoEnhance_Ui
{
    [AvaloniaFact(DisplayName = "S3-12 Videoschnitt: Bild verbessern berechnet eine Fassung mit Messwerten; „verwenden“ tauscht die Datei, Rückgängig holt das Original; Hochskalieren mit Artefakten bietet kein Übernehmen an")]
    public void Enhance_measure_apply_undo()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000; Directory.CreateDirectory(root);
        try
        {
            string F(string n) { var f = Path.Combine(root, n); File.WriteAllBytes(f, [1, 2, 3]); return f; }
            var ssim = "0.95"; var calls = new List<IReadOnlyList<string>>();
            w.FfmpegRunOverride = args =>
            {
                calls.Add(args); var o = args[^1];
                if (args.Count == 3) return new(1, "", "Stream #0:0: Video: h264, yuv420p, 320x180, 25 fps"); // Größe abfragen
                if (o.EndsWith(".mp4")) { File.WriteAllBytes(o, new byte[64]); return new(0, "", ""); }
                return new(0, "", $"SSIM Y:0.9 All:{ssim} (13)\nPSNR y:30 average:33.5 min:30");
            };
            w.Show("schnitt"); Settle(); RealClick(w, V<Button>(w, "prog-tpl-leer"));
            var src = F("strand.mp4"); w.EditAdd(src, length: 4, audio: true);
            w.EditSelected = ("clips", 0); w.EditTab = "auswahl"; w.Show("schnitt"); Settle();
            w.Find<Slider>("ve-enh-denoise").Value = 0.6;
            var b = w.Find<Button>("ve-enh-run"); b.BringIntoView(); Settle(); b.Press();
            for (var i = 0; i < 200 && w.EditEnhanceLast is null; i++) { Thread.Sleep(20); Settle(); }
            var last = w.EditEnhanceLast ?? throw new Xunit.Sdk.XunitException("kein Ergebnis: " + w.ProgramMessage("schnitt"));
            Assert.True(last.Result.Verdict.MayApply); Assert.Contains("SSIM 0.950", last.Result.Verdict.Text.Replace(',', '.'));
            Assert.Contains(calls, a => a.Contains("-vf") && a.ToList()[a.ToList().IndexOf("-vf") + 1].StartsWith("hqdn3d="));
            Settle(); var apply = w.Find<Button>("ve-enh-apply"); apply.BringIntoView(); Settle(); apply.Press(); Settle();
            var d = w.ProgramActive("schnitt")!;
            Assert.EndsWith("strand – verbessert.mp4", VideoEdit.Clips(d.Data)[0].File);
            Assert.Equal("Bild verbessert", d.UndoLabel);
            d.Undo(); Assert.Equal(src, VideoEdit.Clips(w.ProgramActive("schnitt")!.Data)[0].File);
            // Hochskalieren mit Artefakten: rot, kein Übernehmen
            ssim = "0.90"; w.EditSelected = ("clips", 0); w.EditTab = "auswahl"; w.Show("schnitt"); Settle();
            w.Find<ComboBox>("ve-enh-up").SelectedIndex = 1;
            b = w.Find<Button>("ve-enh-run"); b.BringIntoView(); Settle(); b.Press();
            for (var i = 0; i < 200 && w.EditEnhanceLast is null; i++) { Thread.Sleep(20); Settle(); }
            Assert.False(w.EditEnhanceLast!.Value.Result.Verdict.MayApply); Assert.Contains("Artefakte", w.ProgramMessage("schnitt"));
            Settle(); Assert.DoesNotContain(w.GetLogicalDescendantsOfType<Button>(), x => x.Name == "ve-enh-apply");
        }
        finally { w.Close(); }
    }
}

static class LogicalExt
{
    public static IEnumerable<T> GetLogicalDescendantsOfType<T>(this Avalonia.Controls.Window w) where T : Avalonia.LogicalTree.ILogical =>
        Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(w).OfType<T>();
}

/// <summary>Paket 01 – Studio 3, S3-17 (07.11.2026): Abspann im Videoschnitt.</summary>
public class VideoCredits_Ui
{
    [AvaloniaFact(DisplayName = "S3-17 Videoschnitt: Abspann aus „Rolle: Name“ als Clip ans Ende (rückgängig machbar); Schrift mit Einbett-Verbot wird vor der Freigabe abgewiesen")]
    public async Task Credits_to_timeline()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000; Directory.CreateDirectory(root);
        try
        {
            string F(string n) { var f = Path.Combine(root, n); File.WriteAllBytes(f, [1, 2, 3]); return f; }
            var runs = new List<IReadOnlyList<string>>();
            w.FfmpegRunOverride = a => { runs.Add(a); File.WriteAllBytes(a[^1], new byte[128]); return new Cli.Result(0, "", ""); };
            w.Show("schnitt"); Settle(); RealClick(w, V<Button>(w, "prog-tpl-leer"));
            w.EditAdd(F("a.mp4"), length: 4, audio: true);
            w.EditTab = "export"; w.Show("schnitt"); Settle();
            w.Find<TextBox>("ve-credits-text").Text = "Regie: Erika Muster\nMusik: Max Beispiel, Trendo";
            w.Find<NumericUpDown>("ve-credits-len").Value = 8;
            var b = w.Find<Button>("ve-credits-add"); b.BringIntoView(); Settle(); b.Press();
            for (var i = 0; i < 100 && VideoEdit.Clips(w.ProgramActive("schnitt")!.Data).Count < 2; i++) { await Task.Delay(20); Settle(); }
            var d = w.ProgramActive("schnitt")!; var clips = VideoEdit.Clips(d.Data);
            Assert.Equal(2, clips.Count); Assert.Contains("-abspann-", clips[1].File); Assert.Equal(8, clips[1].Length, 1);
            Assert.Equal("Abspann", d.UndoLabel); Assert.Contains("Regie: Erika Muster", d.Text("credits"));
            Assert.Contains(runs, a => string.Join(" ", a).Contains("Erika Muster") && string.Join(" ", a).Contains("drawtext"));
            d.Undo(); Assert.Single(VideoEdit.Clips(w.ProgramActive("schnitt")!.Data));
            // Schrift mit Einbett-Verbot (fsType 2)
            var bad = Path.Combine(root, "gesperrt.ttf");
            var os2 = new byte[10]; os2[9] = 2;
            byte[] head = [0, 1, 0, 0, 0, 1, 0, 16, 0, 0, 0, 0, (byte)'O', (byte)'S', (byte)'/', (byte)'2', 0, 0, 0, 0, 0, 0, 0, 28, 0, 0, 0, 10];
            File.WriteAllBytes(bad, [.. head, .. os2]);
            w.CreditsFontOverride = bad; runs.Clear();
            await w.EditCreditsAdd(w.ProgramActive("schnitt")!, "Regie: Erika Muster", 6);
            Assert.Empty(runs); Assert.Contains("verbietet", w.ProgramMessage("schnitt"));
            Assert.Single(VideoEdit.Clips(w.ProgramActive("schnitt")!.Data));
        }
        finally { w.Close(); }
    }
}

/// <summary>Paket 01 – Studio 3, S3-18 (07.11.2026): Render-Warteschlange im Videoschnitt.</summary>
public class VideoQueue_Ui
{
    [AvaloniaFact(DisplayName = "S3-18 Videoschnitt: drei Fassungen in die Warteschlange (YouTube, Hochformat, ProRes) laufen nacheinander; entfernte Fassung läuft nicht; ein Fehler hält die Schlange nicht an")]
    public async Task Queue()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000; Directory.CreateDirectory(root);
        try
        {
            string F(string n) { var f = Path.Combine(root, n); File.WriteAllBytes(f, [1, 2, 3]); return f; }
            var gate = new TaskCompletionSource(); var order = new List<string>();
            w.EncodersOverride = new HashSet<string> { "libx264", "aac", "png", "libx265" }; // ProRes fehlt → Fehler, Schlange läuft weiter
            w.ExportAsyncOverride = async (p, f, prog, ct) => { order.Add(Path.GetFileName(f)); if (order.Count == 1) await gate.Task; File.WriteAllText(f, "mp4"); return f; };
            w.Show("schnitt"); Settle(); RealClick(w, V<Button>(w, "prog-tpl-leer"));
            w.EditAdd(F("a.mp4"), length: 4, audio: true);
            var d = w.ProgramActive("schnitt")!;
            w.EditPreset = "yt1080"; var first = w.EditQueueAdd(d); Settle(); // startet sofort, wartet am Tor
            w.EditPreset = "hoch"; await w.EditQueueAdd(d);
            w.EditPreset = "png"; await w.EditQueueAdd(d);
            w.EditPreset = "prores"; await w.EditQueueAdd(d);
            w.EditPreset = "av1"; await w.EditQueueAdd(d);
            Assert.Equal(4, w.EditQueue.Count);
            w.EditQueueRemove(3); // AV1 doch nicht
            w.EditTab = "export"; w.Show("schnitt"); Settle();
            Assert.NotNull(w.Find<StackPanel>("ve-queue"));
            gate.SetResult();
            await first;
            for (var i = 0; i < 100 && (w.EditExporting || w.EditQueue.Count > 0); i++) { await Task.Delay(10); Settle(); }
            Assert.Equal(["Unbenannt-yt1080.mp4", "Unbenannt-hoch.mp4", "Unbenannt-png"], order);
            Assert.Equal(4, w.EditQueueResults.Count); Assert.Contains(w.EditQueueResults, r => r.Contains("prores_ks"));
            Assert.DoesNotContain(order, o => o.Contains("av1"));
        }
        finally { w.Close(); }
    }
}

/// <summary>Paket 01 – Studio 3, S3-02 (07.11.2026): Proxy-Dateien im Videoschnitt.</summary>
public class VideoProxy_Ui
{
    [AvaloniaFact(DisplayName = "S3-02 Videoschnitt: Proxys erstellen (echtes FFmpeg) – Vorschau nutzt den Proxy, Export bleibt beim Original; ausschalten geht")]
    public async Task Proxies()
    {
        var ff = Cli.Find("ffmpeg"); if (ff is null) return;
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000; Directory.CreateDirectory(root);
        try
        {
            var orig = Path.Combine(root, "kamera.mp4");
            Assert.True(Cli.Run(ff, ["-hide_banner", "-loglevel", "error", "-y", "-f", "lavfi", "-i", "testsrc2=s=1280x720:r=25:d=2", "-pix_fmt", "yuv420p", orig], root, 60000).Ok);
            w.FfmpegRunOverride = a => Cli.Run(ff, a, root, 120000);
            w.Show("schnitt"); Settle(); RealClick(w, V<Button>(w, "prog-tpl-leer"));
            w.EditAdd(orig, length: 2, audio: false); w.EditTab = "medien"; w.Show("schnitt"); Settle();
            var b = w.Find<Button>("ve-proxy-make"); b.BringIntoView(); Settle(); b.Press();
            for (var i = 0; i < 300 && VideoProxy.Map(w.ProgramActive("schnitt")!.Data).Count == 0; i++) { await Task.Delay(20); Settle(); }
            var d = w.ProgramActive("schnitt")!;
            var px = VideoProxy.Map(d.Data)[orig];
            Assert.True(File.Exists(px)); Assert.Equal(px, VideoProxy.ForPreview(d.Data, orig));
            Assert.Contains("1 Proxy(s) bereit", w.ProgramMessage("schnitt"));
            Assert.DoesNotContain(".proxy.mp4", string.Join(" ", VideoEdit.ExportArgs(d.Data, Path.Combine(root, "film.mp4"))));
            w.Show("schnitt"); Settle(); w.Find<CheckBox>("ve-proxy-use").IsChecked = false; Settle();
            Assert.Equal(orig, VideoProxy.ForPreview(w.ProgramActive("schnitt")!.Data, orig));
        }
        finally { w.Close(); }
    }
}

/// <summary>Paket 01 – Studio 3, S3-01/S3-03 (07.11.2026): Drehbuch → Shotliste und Storyboard im Videoschnitt.</summary>
public class VideoShotPlan_Ui
{
    [AvaloniaFact(DisplayName = "S3-01/S3-03 Videoschnitt: Shotliste aus dem Drehbuch (CSV), Storyboard-Tafeln mit Shot-IDs in die Zeitleiste; nicht erzeugbare Tafel steht im Fehlprotokoll, der Rest wird übernommen")]
    public async Task Script_to_storyboard()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000; Directory.CreateDirectory(root);
        try
        {
            w.FfmpegRunOverride = a => { if (a[^1].EndsWith("SZ01-E02.mp4")) return new Cli.Result(1, "", "Fehler: Schrift defekt"); File.WriteAllBytes(a[^1], new byte[64]); return new Cli.Result(0, "", ""); };
            w.Show("schnitt"); Settle(); RealClick(w, V<Button>(w, "prog-tpl-leer")); w.EditTab = "medien"; w.Show("schnitt"); Settle();
            w.Find<TextBox>("ve-script").Text = "1 INNEN. KÜCHE – MORGEN\n- Totale: Erika kommt (4 s)\n- Nah: Kaffee (3 s)\n\n2 AUSSEN. STRASSE – TAG\n- Halbtotale: Erika geht (5 s)";
            var b = w.Find<Button>("ve-shotlist"); b.BringIntoView(); Settle(); b.Press(); Settle();
            Assert.Equal(3, w.EditShotPlanLast!.AllShots.Count());
            Assert.Single(Directory.GetFiles(root, "*Shotliste.csv", SearchOption.AllDirectories));
            Assert.Contains("Shotliste: 2 Szenen, 3 Einstellungen", w.ProgramMessage("schnitt"));
            w.Show("schnitt"); Settle(); w.Find<Button>("ve-storyboard").Press();
            for (var i = 0; i < 100 && VideoEdit.Clips(w.ProgramActive("schnitt")!.Data).Count < 2; i++) { await Task.Delay(20); Settle(); }
            await Task.Delay(100); Settle();
            var d = w.ProgramActive("schnitt")!; var clips = VideoEdit.Clips(d.Data);
            Assert.Equal(["SZ01-E01", "SZ02-E01"], clips.Select(c => c.Title)); Assert.Equal([4.0, 5.0], clips.Select(c => c.Length));
            Assert.Contains(w.EditStoryboardLog, l => l.StartsWith("SZ01-E02") && l.Contains("Schrift defekt"));
            Assert.Contains("Fehlprotokoll", w.ProgramMessage("schnitt"));
            Assert.Equal("Storyboard", d.UndoLabel);
        }
        finally { w.Close(); }
    }
}

/// <summary>Paket 01 – Studio 3, S3-02 (07.11.2026): Multicam im Videoschnitt.</summary>
public class VideoMulticam_Ui
{
    [AvaloniaFact(DisplayName = "S3-02 Multicam: zwei echte Kameradateien über den Ton synchronisieren (echtes FFmpeg), Wechsel 0:1, 3:2, 6:1 → drei Bild-Abschnitte mit richtigen Einstiegspunkten, Ton von Kamera 1 durchgehend; Unsinn bei den Wechseln wird gemeldet")]
    public async Task Multicam_cut()
    {
        var ff = Cli.Find("ffmpeg"); if (ff is null) return;
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000; Directory.CreateDirectory(root);
        try
        {
            const int rate = 48000; var rng = new Random(5); var scene = new float[rate * 14]; var t = 0.3;
            while (t < 13.5) { var at = (int)(t * rate); for (var i = 0; i < rate / 20 && at + i < scene.Length; i++) scene[at + i] = (float)(0.6 * Math.Sin(2 * Math.PI * 300 * i / rate) * Math.Exp(-i / (rate * 0.015))); t += 0.15 + rng.NextDouble() * 0.6; }
            string Cam(string name, float[] audio, string color)
            {
                var wav = Path.Combine(root, name + ".wav"); AudioFile.WriteWav(new AudioBuf(rate, [audio, (float[])audio.Clone()]), wav);
                var mp4 = Path.Combine(root, name + ".mp4");
                Assert.True(Cli.Run(ff, ["-hide_banner", "-loglevel", "error", "-y", "-f", "lavfi", "-i", $"color=c={color}:s=320x180:r=25", "-i", wav, "-shortest", "-pix_fmt", "yuv420p", "-c:a", "aac", mp4], root, 60000).Ok); return mp4;
            }
            var a = Cam("a", scene[..(rate * 12)], "red"); var b = Cam("b", scene[(rate * 2)..], "blue"); // B startete 2 s später
            w.FfmpegRunOverride = args => Cli.Run(ff, args, root, 60000);
            w.Show("schnitt"); Settle(); RealClick(w, V<Button>(w, "prog-tpl-leer")); w.EditTab = "medien"; w.Show("schnitt"); Settle();
            w.Find<TextBox>("ve-mc-files").Text = a + "\n" + b;
            Assert.True(await w.EditMulticamSync(a + "\n" + b)); w.Show("schnitt"); Settle();
            Assert.InRange(w.EditMulticamAngles![1].Offset, 1.97, 2.03);
            w.Find<TextBox>("ve-mc-switches").Text = "0:1, 3:2, 6:1";
            var cut = w.Find<Button>("ve-mc-cut"); cut.BringIntoView(); Settle(); cut.Press(); Settle();
            var d = w.ProgramActive("schnitt")!; var clips = VideoEdit.Clips(d.Data);
            Assert.Equal([a, b, a], clips.Select(c => c.File)); Assert.InRange(clips[1].In, 0.97, 1.03); Assert.Equal(3, clips[1].Length, 2);
            Assert.All(clips, c => Assert.False(c.HasAudio));
            Assert.Equal(a, VideoEdit.Clips(d.Data, "music").Single().File);
            Assert.Equal("Multicam-Schnitt", d.UndoLabel);
            w.Show("schnitt"); Settle(); w.Find<TextBox>("ve-mc-switches").Text = "drei Kameras bitte"; w.Find<Button>("ve-mc-cut").Press(); Settle();
            Assert.Contains("verstehe ich nicht", w.ProgramMessage("schnitt"));
        }
        finally { w.Close(); }
    }
}
