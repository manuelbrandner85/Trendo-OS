using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Media;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>Paket 01 – Studio 3, S3-11 (06.11.2026): LUT je Stück und Scopes mit Farbvergleich im Videoschnitt.</summary>
public class ColorScopes_Ui
{
    [AvaloniaFact(DisplayName = "S3-11 Videoschnitt: LUT setzen (beschädigte abgelehnt, Stück unverändert), Scopes mit Messwerten und Vergleich mit einem anderen Stück")]
    public void Lut_and_scopes()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000; Directory.CreateDirectory(root);
        try
        {
            string F(string n) { var f = Path.Combine(root, n); File.WriteAllBytes(f, [1, 2, 3]); return f; }
            var calls = new List<IReadOnlyList<string>>(); var yavg = "120";
            w.FfmpegRunOverride = args =>
            {
                calls.Add(args); var o = args[^1];
                if (o.EndsWith(".png")) { File.WriteAllBytes(o, Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==")); return new(0, "", ""); }
                if (args.Contains("-lavfi")) return new(0, "", "SSIM Y:0.5 All:0.47 (2)\nPSNR y:8 average:8.6 min:8");
                var cur = yavg; yavg = "150"; // erstes Stück 120, Vergleichsstück 150
                return new(0, "", $"lavfi.signalstats.YMIN=16\nlavfi.signalstats.YAVG={cur}\nlavfi.signalstats.YMAX=235\nlavfi.signalstats.SATAVG=30\nlavfi.signalstats.HUEAVG=200");
            };
            w.Show("schnitt"); Settle(); RealClick(w, V<Button>(w, "prog-tpl-leer"));
            w.EditAdd(F("a.mp4"), length: 4, audio: true); w.EditAdd(F("b.mp4"), length: 4, audio: true);
            var broken = Path.Combine(root, "kaputt.cube"); File.WriteAllText(broken, "LUT_3D_SIZE 2\n0 0 0\n");
            var good = Path.Combine(root, "look.cube"); File.WriteAllText(good, "LUT_3D_SIZE 2\n0 0 1\n1 0 0\n0 1 0\n1 1 0\n0 0 1\n1 0 1\n0 1 1\n1 1 1\n");
            w.EditSelected = ("clips", 0); w.EditTab = "auswahl"; w.Show("schnitt"); Settle();
            w.Find<TextBox>("ve-lut").Text = broken; var apply = w.Find<Button>("ve-pro-apply"); apply.BringIntoView(); Settle(); apply.Press(); Settle();
            var d = w.ProgramActive("schnitt")!; var id = VideoEdit.Clips(d.Data)[0].Id;
            Assert.Equal("", VideoPro.Fx(d.Data, id).Lut); Assert.Contains("unvollständig", w.ProgramMessage("schnitt"));
            w.Find<TextBox>("ve-lut").Text = good; apply = w.Find<Button>("ve-pro-apply"); apply.Press(); Settle();
            Assert.Equal(good, VideoPro.Fx(w.ProgramActive("schnitt")!.Data, id).Lut);
            w.EditSelected = ("clips", 0); w.EditTab = "auswahl"; w.Show("schnitt"); Settle();
            Assert.Equal(good, w.Find<TextBox>("ve-lut").Text); // Feld zeigt die gesetzte LUT
            w.Find<ComboBox>("ve-scopes-ref").SelectedIndex = 1;
            var run = w.Find<Button>("ve-scopes-run"); run.BringIntoView(); Settle(); run.Press();
            for (var i = 0; i < 200 && w.EditScopesLast is null; i++) { Thread.Sleep(20); Settle(); }
            var last = w.EditScopesLast ?? throw new Xunit.Sdk.XunitException("keine Scopes: " + w.ProgramMessage("schnitt"));
            Assert.Equal(120, last.Stats.YAvg); Assert.False(last.Match!.Equivalent); Assert.Contains("Helligkeit", last.Match.Text);
            Assert.False(last.Effect!.Equivalent); // LUT verändert das Bild deutlich
            Assert.Contains(calls, a => string.Join(" ", a).Contains("lut3d=file='")); // Scopes mit der LUT des Stücks
            Settle(); Assert.NotNull(w.Find<Image>("ve-scopes-image"));
        }
        finally { w.Close(); }
    }
}
