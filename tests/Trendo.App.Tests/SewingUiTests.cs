using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.Core;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>Alle Studios, Runde 7 – SPEC-08: „Schnittmuster nach Maß (FreeSewing)“ im Programm „Kleidung &amp; Produkte“ (FreeSewing nachgebildet; Echtlauf erst nach Freigabe).</summary>
public class Sewing_Ui
{
    const string SK = "mode-programm";

    [AvaloniaFact(DisplayName = "Schnittmuster: ohne Einrichtung Einrichten-Knopf; mit FreeSewing → T-Shirt-Schnitt als SVG mit Maßen in mm, Herrengröße, ergänzte Maße auf Deutsch genannt")]
    public void Draft()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000;
        try
        {
            MainWindow.SewingDirOverride = null; MainWindow.SewingRunOverride = null;
            w.Show(SK); Settle(); RealClick(w, w.Find<Button>("prog-tpl-tshirt")); Settle();
            Assert.NotNull(w.Find<Button>("mode-sew-setup"));
            var d = w.ProgramActive(SK)!; d.Set("fs-neck", 39, "Hals");
            IReadOnlyList<string>? seen = null; string? measures = null;
            MainWindow.SewingDirOverride = Path.Combine(root, "fs-fake");
            MainWindow.SewingRunOverride = (_, a, _) =>
            {
                seen = a; measures = File.ReadAllText(a[2]); File.WriteAllText(a[5], "<svg width=\"800mm\" height=\"900mm\"/>");
                return new Cli.Result(0, """{"schnitt":"teagan","groesse":"cisMaleAdult38","ergaenzt":["biceps","shoulderSlope"],"pflicht":["biceps","chest","neck"],"fehler":[],"breite_mm":800,"hoehe_mm":900}""", "");
            };
            w.Show(SK); Settle();
            Assert.NotNull(w.Find<ComboBox>("mode-sew-design")); Assert.NotNull(w.Find<TextBox>("mode-sew-neck"));
            var svg = w.SewingDraft(d, "teagan", 10);
            Assert.NotNull(svg); Assert.True(File.Exists(svg)); Assert.EndsWith("-teagan.svg", svg);
            Assert.Equal(["teagan"], seen!.Skip(1).Take(1)); Assert.Equal("10", seen[4]);
            Assert.Equal(d.Flag("women") ? "frau" : "mann", seen[3]);
            Assert.Contains("\"neck\":390", measures); Assert.Contains("\"chest\":" + (d.Number("chest") * 10).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture), measures);
            Assert.StartsWith("✓ T-Shirt: Schnittbogen 80 × 90 cm – aus Standardgröße ergänzt: Oberarmumfang, Schulterneigung (Grad).", w.SewingMessage);
            Assert.Contains("1:1 drucken", w.SewingMessage);
            w.Show(SK); Settle(); Assert.NotNull(w.Find<Button>("mode-sew-open"));
            Assert.Null(w.SewingDraft(d, "gibtsnicht")); Assert.Equal("⚠ Unbekannter Schnitt.", w.SewingMessage);
        }
        finally { MainWindow.SewingDirOverride = null; MainWindow.SewingRunOverride = null; w.Close(); }
    }
}
