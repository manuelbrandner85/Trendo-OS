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
