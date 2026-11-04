using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Cad;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>3D-Druck-Vorschau: Druckplatte, Aufstellen, Schichten, Prüfungen, Schätzung – Kern und Oberfläche.</summary>
public class PrintPreview_Ui
{
    static Mesh T()
    {
        var m = new Mesh(); Mesh3D.AddBox(m, -10, 10, -10, 10, 0, 60); Mesh3D.AddBox(m, -45, 45, -10, 10, 60, 75); return m; // mm
    }

    [Fact(DisplayName = "Druck-Kern: Würfel-Volumen, Schnitt ist ein Quadrat, T hat Überhang, Aufstellen beseitigt ihn, Platte-Prüfung, 3MF lesen in mm")]
    public void Core()
    {
        var cube = new Mesh(); Mesh3D.AddBox(cube, 0, 20, 0, 20, 0, 20);
        Assert.Equal(8000, PrintPrep.Volume(cube), 1);
        var seg = PrintPrep.Slice(cube, 10); Assert.Equal(80, seg.Sum(s => Math.Sqrt(Math.Pow(s.X2 - s.X1, 2) + Math.Pow(s.Y2 - s.Y1, 2))), 1); // Umfang 4 × 20
        Assert.Equal(0, PrintPrep.OverhangArea(cube), 3);
        var t = T(); Assert.Equal(90 * 20, PrintPrep.OverhangArea(t), 1); // ganze Unterseite des Querbalkens (zwei getrennte Quader)
        var (best, label) = PrintPrep.AutoOrient(t); Assert.Equal(0, PrintPrep.OverhangArea(best), 1); Assert.Contains("gedreht", label);
        var placed = PrintPrep.Place(best, PrintPrep.BambuA1); var r = PrintPrep.Analyze(placed, PrintPrep.BambuA1);
        Assert.True(r.Fits); Assert.Equal(20, r.SizeZ, 1); Assert.Equal(100, r.Layers); Assert.InRange(r.FilamentG, 5, 60); Assert.True(r.Time.TotalMinutes > 5);
        Assert.Equal(0, PrintPrep.Bounds(placed).Min.Z, 6); Assert.Equal(128, (PrintPrep.Bounds(placed).Min.X + PrintPrep.Bounds(placed).Max.X) / 2, 3);
        var big = new Mesh(); Mesh3D.AddBox(big, 0, 300, 0, 10, 0, 10); Assert.False(PrintPrep.Analyze(PrintPrep.Place(big, PrintPrep.BambuA1), PrintPrep.BambuA1).Fits);
        var f = Path.Combine(Path.GetTempPath(), $"trendo-3mf-{Guid.NewGuid():N}.3mf");
        try
        {
            var solid = new Solid(); for (var i = 0; i + 2 < cube.I.Count; i += 3) if (Poly.TryCreate([cube.V[cube.I[i]], cube.V[cube.I[i + 1]], cube.V[cube.I[i + 2]]]) is { } p) solid.Polys.Add(p);
            CadExport.Write3mf([("Würfel", "#F59E0B", solid)], f);
            var back = PrintPrep.Load(f); Assert.Equal(8000, PrintPrep.Volume(back), 1);
        }
        finally { File.Delete(f); }
    }

    [AvaloniaFact(DisplayName = "3D-Druck UI: STL zeigt Platte, Schichten und Warnungen; Automatisch aufstellen beseitigt den Überhang; Schicht-Regler wählt die Schicht")]
    public void Ui_preview()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1100; Directory.CreateDirectory(root);
        var stl = Path.Combine(root, "t.stl"); var m = new Mesh(); Mesh3D.AddBox(m, -1, 1, -1, 1, 0, 6); Mesh3D.AddBox(m, -4.5, 4.5, -1, 1, 6, 7.5); Mesh3D.WriteStl(m, stl); // cm → STL in mm
        w.Open3dPrint(stl); Settle();
        Assert.NotNull(w.TryFind<CadView>("print-3d"));
        Assert.Equal(75, w.PrintReport!.SizeZ, 1);
        Assert.Contains("Stützen", string.Join(" ", w.Find<StackPanel>("print-warnings").Children.OfType<TextBlock>().Select(t => t.Text)));
        var b = V<Button>(w, "print-orient"); b.BringIntoView(); Settle(); RealClick(w, V<Button>(w, "print-orient"));
        Assert.Equal(20, w.PrintReport!.SizeZ, 1); Assert.True(w.PrintReport.OverhangCm2 < 0.1);
        Assert.Contains("✓", string.Join(" ", w.Find<StackPanel>("print-warnings").Children.OfType<TextBlock>().Select(t => t.Text)));
        w.Find<Slider>("print-layer").Value = 10; Settle();
        Assert.Equal(10, w.PrintLayer); Assert.Contains("10 / 100", w.Find<TextBlock>("print-layer-label").Text);
        // STEP ohne Vorschau, aber ohne Fehler
        var step = Path.Combine(root, "teil.step"); File.WriteAllText(step, "ISO-10303-21;"); w.Open3dPrint(step); Settle();
        Assert.Null(w.TryFind<StackPanel>("print-preview")); // keine Schichten/Eigenschaften
        Assert.Contains("keine Vorschau für dieses Format", w.Find<TextBlock>("print-empty-title").Text); // leere Platte mit ehrlichem Hinweis (wie im Slicer)
    }
    [AvaloniaFact(DisplayName = "3D-Druck wie PrusaSlicer/Cura: Schichtvorschau mit Wänden und Füllung, Material und Kosten, Füllung, Schichthöhe, Skalieren, Kopien anordnen")]
    public void Slicer_settings()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1100; Directory.CreateDirectory(root);
        try
        {
            var stl = Path.Combine(root, "wuerfel.stl"); var m = new Mesh(); Mesh3D.AddBox(m, 0, 3, 0, 3, 0, 3); Mesh3D.WriteStl(m, stl); // 30-mm-Würfel
            w.Open3dPrint(stl); Settle();
            var e0 = w.PrintEstimate!; Assert.Equal(150, e0.Layers);
            Assert.Contains("Füllbahnen", w.Find<TextBlock>("print-layer-label").Text);
            Assert.Contains("PLA", w.Find<TextBlock>("print-facts").Text); Assert.Contains("€", w.Find<TextBlock>("print-facts").Text);
            // Material PETG: schwerer, teurer, andere Temperaturen
            w.Find<ComboBox>("print-material").SelectedIndex = 1; Settle();
            Assert.Equal("petg", w.PrintSettings.Material); Assert.True(w.PrintEstimate!.Grams > e0.Grams);
            Assert.Contains("Düse 245 °C", w.Find<TextBlock>("print-facts").Text);
            // Füllung 50 %: mehr Material
            var g = w.PrintEstimate.Grams; w.Find<Slider>("print-infill").Value = 50; Settle();
            Assert.Equal(50, w.PrintSettings.Infill); Assert.True(w.PrintEstimate!.Grams > g * 1.3);
            // Schichthöhe 0,28 mm: weniger Schichten, schneller
            var t = w.PrintEstimate.Time; w.Find<ComboBox>("print-layer-height").SelectedIndex = 4; Settle();
            Assert.Equal(108, w.PrintEstimate!.Layers); Assert.True(w.PrintEstimate.Time < t);
            // Skalieren auf 50 %
            w.Find<TextBox>("print-scale").Text = "50"; w.Find<Button>("print-scale-apply").Press(); Settle();
            Assert.Equal(15, w.PrintReport!.SizeX, 1);
            // 4 Kopien anordnen: alle auf der Platte, viermal so viel Material
            var one = w.PrintEstimate!.Grams; w.Find<TextBox>("print-copies").Text = "4"; w.Find<Button>("print-arrange").Press(); Settle();
            Assert.Equal(4 * 12, w.PrintMesh!.Triangles); Assert.True(w.PrintReport!.Fits);
            Assert.InRange(w.PrintEstimate!.Grams, one * 3.6, one * 4.4);
            Assert.Contains("4 Kopien", w.Find<TextBlock>("print-msg").Text);
            // zu viele Kopien: klare Meldung statt Absturz
            w.Find<TextBox>("print-copies").Text = "500"; w.Find<Button>("print-arrange").Press(); Settle();
            Assert.Contains("1 bis 100", w.Find<TextBlock>("print-msg").Text);
        }
        finally { w.Close(); }
    }
}
