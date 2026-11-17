using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Media;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0695: Videoschnitt – die Vorschau zeigt das fertige Bild (gleicher Filtergraph wie der Export), Vollbild, Ablegen von Dateien, eine goldene Hauptaktion.</summary>
public class VideoLook_Ui
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    static async Task Until(Func<bool> ok, int seconds = 60)
    {
        var sw = Stopwatch.StartNew();
        while (!ok() && sw.Elapsed.TotalSeconds < seconds) { Dispatcher.UIThread.RunJobs(); await Task.Delay(20); }
        Settle();
    }

    static async Task LookDone(MainWindow w)
    {
        for (var i = 0; i < 400 && w.EditLookTask is { IsCompleted: false } task; i++) { Dispatcher.UIThread.RunJobs(); await Task.WhenAny(task, Task.Delay(25)); }
        Settle();
    }

    /// <summary>Anteil farbiger Pixel in RGB-Rohdaten (Kanäle weichen um mehr als 12 voneinander ab).</summary>
    static double Colored(byte[] px) { var n = 0; for (var i = 0; i < px.Length; i += 3) if (Math.Max(Math.Abs(px[i] - px[i + 1]), Math.Abs(px[i + 1] - px[i + 2])) > 12) n++; return n / (px.Length / 3.0); }

    [AvaloniaFact(DisplayName = "T-0695 mit echtem FFmpeg, vom Ablegen bis zur Datei: Schwarzweiß-Look und Titel sind in der Vorschau zu sehen, beim Scrubben kommt die letzte Stelle, F zeigt Vollbild, die goldene Hauptaktion exportiert – Vorschau gleich dem Bild im MP4")]
    public async Task Look_click_to_file_real()
    {
        var ff = Cli.Find("ffmpeg"); var probe = Cli.Find("ffprobe");
        if (ff is null || probe is null) { Assert.Skip("Übersprungen – FFmpeg oder ffprobe fehlt auf diesem PC."); return; }
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000;
        var log = new StringBuilder(); void Log(string s) => log.AppendLine(s);
        var beleg = Environment.GetEnvironmentVariable("TRENDO_T0695_BELEG");
        try
        {
            var media = Path.Combine(root, "clips"); Directory.CreateDirectory(media);
            string P(string n) => Path.Combine(media, n);
            Assert.True(Cli.Run(ff, ["-hide_banner", "-loglevel", "error", "-y", "-f", "lavfi", "-i", "testsrc2=size=1920x1080:rate=30:duration=4", "-f", "lavfi", "-i", "sine=frequency=440:duration=4", "-shortest", "-c:v", "libx264", "-pix_fmt", "yuv420p", "-c:a", "aac", P("strand.mp4")], media, 120000).Ok);
            Assert.True(Cli.Run(ff, ["-hide_banner", "-loglevel", "error", "-y", "-f", "lavfi", "-i", "smptehdbars=size=1920x1080", "-frames:v", "1", P("tafel.png")], media, 60000).Ok);
            w.EditFfmpegPath = ff; // echtes FFmpeg statt der Sperre des abgeschotteten Testmodus – kein Ersatz für das Werkzeug

            w.Show("schnitt"); Settle(); RealClick(w, w.Find<Button>("prog-tpl-fullhd")); Settle();
            // Ablegen an der Abwurfstelle (derselbe Weg wie Ziehen aus dem Explorer): Länge und Ton liest ffprobe
            Assert.Equal(2, w.EditDropFiles([P("strand.mp4"), P("tafel.png")], 0, false));
            var d = w.ProgramActive("schnitt")!;
            var clips = VideoEdit.Clips(d.Data);
            Assert.Equal(["strand.mp4", "tafel.png"], clips.Select(c => Path.GetFileName(c.File)));
            Assert.InRange(clips[0].Length, 3.9, 4.1); Assert.True(clips[0].HasAudio);
            d.Edit("Titel", x => VideoEdit.SetClip(x, 0, 0.5, "Hallo Welt"));
            d.Edit("Look", x => VideoPro.SetFx(x, clips[0].Id, VideoPro.Looks.First(l => l.Id == "sw").Fx));

            // Abspielkopf auf 1 s: das fertige Bild kommt im Hintergrund
            var sw = Stopwatch.StartNew();
            d.Set("playhead", 1.0, "Abspielkopf"); w.Show("schnitt"); Settle();
            await LookDone(w);
            Log($"Standbild 1,0 s (1080p-Projekt, 960 px): {w.EditLookMs} ms bis zum Bild (Messung in Trendo vom Wunsch bis zum fertigen Bild), Test gesamt {sw.ElapsedMilliseconds} ms");
            Assert.True(w.EditLookError.Length == 0, w.EditLookError);
            Assert.False(w.Find<TextBlock>("ve-look-busy").IsVisible);
            Assert.False(w.Find<TextBlock>("ve-title-overlay").IsVisible); // der Titel steckt im fertigen Bild, nicht als Text darüber
            // Die Oberfläche zeigt das Bild aus dem Zwischenspeicher (Schlüssel = Projekt + Stelle); im Testmodus ohne Grafik wird es nicht dekodiert, also die Datei prüfen
            Assert.IsType<Bitmap>(w.EditPreviewImage);
            byte[] Raw(string png) { var raw = png + ".rgb"; Assert.True(Cli.Run(ff, ["-hide_banner", "-loglevel", "error", "-y", "-i", png, "-f", "rawvideo", "-pix_fmt", "rgb24", raw], root, 60000).Ok); var bytes = File.ReadAllBytes(raw); File.Delete(raw); return bytes; }
            var look1 = Directory.GetFiles(root, VideoLook.Key(d.Data, 1.0, 960, w.EditExportOptions(d)) + ".png", SearchOption.AllDirectories).Single();
            var px = Raw(look1); Assert.Equal(960 * 540 * 3, px.Length);
            var colored = Colored(px);
            Log($"Farbige Pixel in der Vorschau bei Schwarzweiß-Look: {colored:P2}");
            Assert.True(colored < 0.01, $"Schwarzweiß-Look fehlt in der Vorschau: {colored:P1} farbig");

            // Scrubben: viele Stellen schnell hintereinander – nur die letzte wird fertig gerechnet und gezeigt
            var step = Stopwatch.StartNew(); var steps = new List<long>();
            foreach (var t in new[] { 0.3, 0.6, 0.9, 1.5, 2.2 }) { step.Restart(); d.Set("playhead", t, "Abspielkopf"); w.Show("schnitt"); Dispatcher.UIThread.RunJobs(); steps.Add(step.ElapsedMilliseconds); }
            Log("Seitenaufbau je Schritt im Testmodus: " + string.Join("/", steps) + " ms");
            await LookDone(w);
            var file22 = Directory.GetFiles(root, VideoLook.Key(d.Data, 2.2, 960, w.EditExportOptions(d)) + ".png", SearchOption.AllDirectories).Single();
            var between = new[] { 0.3, 0.6, 0.9, 1.5 }.Count(t => File.Exists(Path.Combine(Path.GetDirectoryName(file22)!, VideoLook.Key(d.Data, t, 960, w.EditExportOptions(d)) + ".png")));
            Assert.Equal("", w.EditLookError);
            Log($"Scrubben über 5 Stellen: letzte Stelle nach {w.EditLookMs} ms fertig, {4 - between} von 4 Zwischenstellen abgebrochen – im Testmodus dauert jeder Seitenaufbau länger als ein Bild, Abbrechen prüft der Kerntest");

            // Abspielen: fertige Bilder (Look, Titel, Blende) aus demselben Graphen als Strom, 640×360
            d.Set("playhead", 0.0, "Abspielkopf"); w.Show("schnitt"); Settle(); await LookDone(w);
            w.EditTogglePlay(d); Assert.True(w.EditPlaying);
            var play = Stopwatch.StartNew();
            while (w.PreviewFramesShown < 15 && play.Elapsed.TotalSeconds < 20) { w.EditSmoothTickForTests(d); Dispatcher.UIThread.RunJobs(); await Task.Delay(30); }
            Log($"Abspielen mit Effekten: {w.PreviewFramesShown} fertige Bilder nach {play.ElapsedMilliseconds} ms gezeigt");
            Assert.True(w.PreviewFramesShown >= 15, $"nur {w.PreviewFramesShown} Bilder beim Abspielen");
            Assert.IsType<WriteableBitmap>(w.EditPreviewImage);
            Assert.False(w.Find<TextBlock>("ve-title-overlay").IsVisible);
            w.EditTogglePlay(d); Settle(); Assert.False(w.EditPlaying);

            // Vollbild mit F (Taste auf der Zeitleiste), F im Vollbild schließt
            var tl = w.Find<EditTimeline>("ve-timeline");
            tl.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.F, Source = tl });
            Settle();
            Assert.True(w.EditFullscreenOpen);
            Assert.Same(w.EditPreviewImage, w.EditFullscreenWindow!.GetLogicalDescendants().OfType<Image>().Single(i => i.Name == "ve-full-image").Source);
            w.EditFullscreenWindow.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.F, Source = w.EditFullscreenWindow });
            Settle();
            Assert.False(w.EditFullscreenOpen);

            // Zurück auf 1 s und exportieren über die eine goldene Hauptaktion
            d.Set("playhead", 1.0, "Abspielkopf"); w.Show("schnitt"); Settle(); await LookDone(w);
            var export = w.Find<Button>("prog-cmd-ve-export-bg");
            Assert.True(MainWindow.IsGold(export)); Assert.False(MainWindow.IsGold(w.Find<Button>("prog-cmd-ve-split")));
            RealClick(w, export);
            await Until(() => !w.EditExporting && w.EditQueueResults.Count > 0, 180);
            var mp4 = w.EditQueueResults[^1];
            Assert.True(File.Exists(mp4), mp4 + " · " + w.ProgramMessage("schnitt"));
            string Probe(params string[] a) { var r = Cli.Run(probe, ["-v", "error", .. a], root, 60000); Assert.True(r.Ok, r.Err); return r.Out.Trim(); }
            var dur = double.Parse(Probe("-show_entries", "format=duration", "-of", "csv=p=0", mp4), Inv);
            var streams = Probe("-show_entries", "stream=codec_type,width,height", "-of", "csv=p=0", mp4);
            Log($"Export über „Video exportieren“: {Path.GetFileName(mp4)}, Dauer {dur.ToString("0.000", Inv)} s, Spuren {streams.Replace("\r", "").Replace("\n", " | ")}, SHA-256 {Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(mp4))).ToLowerInvariant()}");
            Assert.InRange(dur, VideoEdit.VideoLength(VideoEdit.Clips(d.Data)) - 0.2, VideoEdit.VideoLength(VideoEdit.Clips(d.Data)) + 0.2);
            Assert.Contains("1920,1080", streams); Assert.Contains("audio", streams);

            // Vorschaubild (das gezeigte, aus dem Zwischenspeicher) gegen das Bild im MP4 an derselben Stelle, beide 960 px
            var shown = Directory.GetFiles(root, VideoLook.Key(d.Data, 1.0, 960, w.EditExportOptions(d)) + ".png", SearchOption.AllDirectories).Single();
            var exp = Path.Combine(root, "export-1s.png");
            Assert.True(Cli.Run(ff, ["-hide_banner", "-loglevel", "error", "-y", "-ss", "1", "-i", mp4, "-frames:v", "1", "-vf", "scale=960:-2", exp], root, 60000).Ok);
            var a = Raw(shown); var b = Raw(exp); Assert.Equal(a.Length, b.Length);
            long sum = 0; for (var i = 0; i < a.Length; i++) sum += Math.Abs(a[i] - b[i]);
            var mad = sum / (double)a.Length;
            Log($"Vorschau (gezeigt) ↔ Export bei 1,0 s, 960 px: mittlere Abweichung {mad.ToString("0.000", Inv)} von 255");
            Assert.True(mad < 2.5, $"Vorschau weicht vom Export ab: {mad:0.000}");

            if (beleg is { Length: > 0 })
            {
                Directory.CreateDirectory(beleg);
                File.Copy(shown, Path.Combine(beleg, "ui-vorschau-1.0s.png"), true); File.Copy(exp, Path.Combine(beleg, "ui-export-1.0s.png"), true);
                File.WriteAllText(Path.Combine(beleg, "ui-rohlog.txt"), log.ToString());
            }
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0695: Dateien ablegen – Video an der Abwurfstelle in die Bildspur, Ton auf die Musikspur ab der Stelle, ein Schritt für Rückgängig; eine goldene Hauptaktion „Video exportieren“; Vollbild-Knopf")]
    public void Drop_and_gold()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000;
        string F(string n) { var f = Path.Combine(root, n); File.WriteAllBytes(f, [1, 2, 3]); return f; }
        w.Show("schnitt"); Settle(); RealClick(w, w.Find<Button>("prog-tpl-leer")); Settle();
        w.EditAdd(F("eins.mp4"), length: 4, audio: true); w.EditAdd(F("zwei.mp4"), length: 4, audio: true); Settle();
        var d = w.ProgramActive("schnitt")!;
        // bei 5 s liegt „zwei“ (4–8 s), dessen Mitte (6 s) dahinter: das neue Stück kommt davor
        Assert.Equal(1, w.EditDropFiles([F("neu.mp4")], 5, false, lengthForTests: 3, audioForTests: false));
        Assert.Equal(["eins.mp4", "neu.mp4", "zwei.mp4"], VideoEdit.Clips(d.Data).Select(c => Path.GetFileName(c.File)));
        Assert.Equal(("clips", 1), w.EditSelected);
        // Ton unten abgelegt: Musikspur ab der Stelle
        w.EditDropFiles([F("lied.mp3")], 2.5, true, lengthForTests: 30);
        var m = Assert.Single(VideoEdit.Clips(d.Data, "music")); Assert.Equal(2.5, m.Start, 3);
        // Foto ganz hinten
        w.EditDropFiles([F("foto.png")], 99, false);
        Assert.Equal("foto.png", Path.GetFileName(VideoEdit.Clips(d.Data)[^1].File));
        Assert.Throws<InvalidOperationException>(() => w.EditDropFiles([F("notiz.txt")], 0, false));
        // ein Schritt für Rückgängig
        Assert.True(d.Undo()); Assert.Equal(3, VideoEdit.Clips(d.Data).Count);
        Settle(); w.Show("schnitt"); Settle();
        Assert.True(MainWindow.IsGold(w.Find<Button>("prog-cmd-ve-export-bg")));
        Assert.False(MainWindow.IsGold(w.Find<Button>("prog-cmd-ve-split")));
        Assert.NotNull(w.Find<Button>("ve-tp-full"));
        Assert.True(DragDrop.GetAllowDrop(w.Find<EditTimeline>("ve-timeline")));
        Assert.Contains("Dateien hierher ziehen", string.Join(" ", w.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text)));
        w.Close();
    }
}
