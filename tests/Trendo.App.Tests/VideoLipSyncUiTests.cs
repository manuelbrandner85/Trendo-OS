using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Media;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>Paket 01 – Studio 3, S3-09 (07.11.2026): Lippensynchron im Videoschnitt.</summary>
public class VideoLipSync_Ui
{
    [AvaloniaFact(DisplayName = "S3-09 Videoschnitt: Sprachaufnahme + Wortzeiten → Mundbilder (JSON, Moho), Prüfung OK, Vergleichsvorschau (echtes FFmpeg) in die Zeitleiste; Gesichtsvideo 150 ms versetzt → FAIL, nichts eingefügt; fehlende Aufnahme → FAIL")]
    public async Task Lipsync_track()
    {
        var ff = Cli.Find("ffmpeg"); if (ff is null) return;
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000; Directory.CreateDirectory(root);
        try
        {
            LipSync.Word[] words = [new("Heute", 0.3, 0.7), new("Abend", 0.75, 1.15), new("tanzen", 1.4, 1.85), new("wir", 1.9, 2.1), new("am", 2.15, 2.3), new("Fluss", 2.35, 2.9)];
            var tr = LipSync.Track(words); var curve = LipSync.OpennessCurve(tr, 3.2); const int rate = 16000;
            var x = new float[(int)(3.2 * rate)];
            for (var i = 0; i < x.Length; i++) { var k = Math.Min(curve.Length - 1, i * LipSync.Rate / rate); x[i] = (float)(curve[k] * (0.5 * Math.Sin(2 * Math.PI * 140 * i / rate) + 0.25 * Math.Sin(2 * Math.PI * 280 * i / rate))); }
            var wav = Path.Combine(root, "sprecherin.wav"); AudioFile.WriteWav(new AudioBuf(rate, [x]), wav);
            var face = Path.Combine(root, "gesicht.mp4");
            Assert.True(Cli.Run(ff, ["-hide_banner", "-loglevel", "error", "-y", "-f", "lavfi", "-i", "color=c=gray:s=320x240:r=25:d=3.2", "-pix_fmt", "yuv420p", face], root, 60000).Ok);
            var json = "{\"transcription\":[" + string.Join(",", words.Select(v => $"{{\"offsets\":{{\"from\":{(int)(v.Start * 1000)},\"to\":{(int)(v.End * 1000)}}},\"text\":\" {v.Text}\"}}")) + "]}";
            w.EditLipWordsOverride = _ => Task.FromResult<string?>(json);
            var lag = 0.15;
            w.EditLipMouthOverride = _ => Task.FromResult<(double, double?[])?>((25.0, Enumerable.Range(0, 80).Select(f => { var k = (int)((f / 25.0 - lag) * 100); return (double?)(k < 0 ? 0 : curve[Math.Min(curve.Length - 1, k)]); }).ToArray()));
            w.FfmpegRunOverride = args => Cli.Run(ff, args, root, 60000);
            w.Show("schnitt"); Settle(); RealClick(w, V<Button>(w, "prog-tpl-leer")); w.EditTab = "medien"; w.Show("schnitt"); Settle();
            var d = w.ProgramActive("schnitt")!;
            // Gesicht 150 ms zu spät → FAIL, nichts in der Zeitleiste
            Assert.False(await w.EditLipSync(d, wav, "", face));
            Assert.True(w.EditLipLast!.Ok, w.EditLipLast.Text); Assert.False(w.EditLipVideoLast!.Ok); Assert.Contains("versetzt", w.EditLipVideoLast.Text);
            Assert.Empty(VideoEdit.Clips(d.Data)); Assert.Contains("Nichts in die Zeitleiste", w.ProgramMessage("schnitt"));
            // über die Oberfläche, passendes Gesicht → OK
            lag = 0; w.Show("schnitt"); Settle();
            w.Find<TextBox>("ve-lip-audio").Text = wav; w.Find<TextBox>("ve-lip-video").Text = face;
            var b = w.Find<Button>("ve-lip-run"); b.BringIntoView(); Settle(); b.Press();
            for (var i = 0; i < 300 && VideoEdit.Clips(d.Data).Count == 0; i++) { await Task.Delay(20); Settle(); }
            Assert.True(w.EditLipVideoLast!.Ok, w.EditLipVideoLast.Text);
            var c = Assert.Single(VideoEdit.Clips(d.Data)); Assert.StartsWith("Lippensynchron", c.Title); Assert.True(c.HasAudio); Assert.True(File.Exists(c.File));
            Assert.Contains("Duration: 00:00:03.2", Cli.Run(ff, ["-hide_banner", "-i", c.File], root, 30000).Err);
            Assert.Contains("\"mund\": \"MBP\"", File.ReadAllText(Path.Combine(w.EditLipFolder!, "mundbilder.json")));
            Assert.StartsWith("MohoSwitch1", File.ReadAllText(Path.Combine(w.EditLipFolder!, "mundbilder-30fps.dat")));
            Assert.Equal("Lippensynchron", d.UndoLabel);
            w.Show("schnitt"); Settle(); Assert.Contains(w.GetLogicalDescendantsOfType<TextBlock>(), t => (t.Text ?? "").StartsWith("OK: Versatz"));
            // fehlende Aufnahme
            Assert.False(await w.EditLipSync(d, Path.Combine(root, "gibtsnicht.wav"), "Hallo"));
            Assert.Contains("FAIL: Sprachaufnahme fehlt", w.ProgramMessage("schnitt"));
        }
        finally { w.Close(); }
    }
}
