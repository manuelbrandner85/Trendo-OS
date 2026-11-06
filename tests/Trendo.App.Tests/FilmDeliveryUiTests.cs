using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Media;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>Paket 01 – Studio 3 (06.11.2026): „Film ausliefern“ im Videoschnitt – ein Ordner mit Film, Stems, EDL, OTIO, Cue-Liste und Prüfbericht.</summary>
public class FilmDelivery_Ui
{
    [AvaloniaFact(DisplayName = "S3-18/S3-20 Videoschnitt: „Film ausliefern“ legt einen Ordner mit Film, Stems, EDL, OTIO, Cue-Liste und Prüfbericht an; fehlendes Medium wird gemeldet statt verschleiert")]
    public void Deliver_from_ribbon()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000; Directory.CreateDirectory(root);
        try
        {
            string F(string n) { var f = Path.Combine(root, n); File.WriteAllBytes(f, [1, 2, 3]); return f; }
            var calls = new List<IReadOnlyList<string>>();
            w.FfmpegRunOverride = args =>
            {
                calls.Add(args); var o = args[^1];
                if (o.EndsWith(".mp4") || o.EndsWith(".wav")) File.WriteAllBytes(o, new byte[64]); // FFmpeg-Ersatz schreibt die Zieldatei
                return new Cli.Result(0, "", "Duration: 00:00:04.00, start\n  Integrated loudness:\n    I:         -14.0 LUFS\n  True peak:\n    Peak:       -2.0 dBFS");
            };
            w.Show("schnitt"); Settle(); RealClick(w, V<Button>(w, "prog-tpl-leer"));
            w.EditAdd(F("a.mp4"), length: 2, audio: true); w.EditAdd(F("b.mp4"), length: 2, audio: true);
            var d = w.ProgramActive("schnitt")!;
            var b = w.Find<Button>("prog-cmd-ve-deliver"); b.BringIntoView(); Settle(); b.Press();
            for (var i = 0; i < 200 && w.FilmLastDelivery is null; i++) { Thread.Sleep(20); Settle(); }
            var r = w.FilmLastDelivery ?? throw new Xunit.Sdk.XunitException("keine Auslieferung: " + w.ProgramMessage("schnitt"));
            Assert.True(Directory.Exists(r.Folder)); Assert.StartsWith("Auslieferung", Path.GetFileName(r.Folder));
            Assert.All(r.Files, f => Assert.True(File.Exists(f), f));
            Assert.Contains(r.Files, f => f.EndsWith(".edl")); Assert.Contains(r.Files, f => f.EndsWith(".otio")); Assert.Contains(r.Files, f => f.EndsWith("Cue-Liste.csv"));
            Assert.Contains(r.Files, f => f.Contains("Stem Dialog"));
            Assert.Contains(FilmDelivery.Sha256(r.Film), File.ReadAllText(r.Report));
            Assert.Contains("Ausgeliefert", w.ProgramMessage("schnitt"));
            Assert.Contains(calls, a => a.Contains("pcm_s24le")); // Stem in 24 Bit
            // fehlendes Medium: nie als Schwarzbild ausliefern
            var before = r; File.Delete(Path.Combine(root, "b.mp4"));
            _ = w.FilmDeliver(d);
            for (var i = 0; i < 200 && !w.ProgramMessage("schnitt").Contains("Fehlende Medien"); i++) { Thread.Sleep(20); Settle(); }
            Assert.Contains("b.mp4", w.ProgramMessage("schnitt"));
            Assert.Same(before, w.FilmLastDelivery);
        }
        finally { w.Close(); }
    }
}
