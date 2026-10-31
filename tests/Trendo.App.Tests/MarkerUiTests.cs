using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Trendo.App;
using Trendo.Core;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>Schnittlagebild: Packen, Prüfungen, PDF und Bedienung mit der Maus.</summary>
public class Marker_Ui
{
    static readonly List<Marker.Piece> Shirt = [new("Vorderteil", 55, 75, 1), new("Rückenteil", 55, 75, 1), new("Ärmel", 40, 25, 2)];

    [Fact(DisplayName = "Schnittlage-Kern: automatisch ohne Überlappung im Stoff, mindestens so sparsam wie das alte Reihenlegen, Ausnutzung, zu breit = Fehler, PDF")]
    public void Core()
    {
        var l = Marker.AutoLayout(Shirt, 140);
        Assert.Equal(4, l.Count); Assert.Empty(Marker.Problems(Shirt, l, 140));
        var oldMeters = Fashion.Fabric(Shirt.Select(p => (p.W, p.H, p.Qty)), 140).Meters;
        Assert.True(Marker.Length(Shirt, l) / 100 <= oldMeters + 1e-9, $"{Marker.Length(Shirt, l)} cm vs. {oldMeters} m");
        var eff = Marker.Efficiency(Shirt, l, 140); Assert.InRange(eff, 0.5, 1);
        var moved = l.ToList(); moved[1] = moved[1] with { X = moved[0].X, Y = moved[0].Y }; Assert.Contains(0, Marker.Problems(Shirt, moved, 140)); Assert.Contains(1, Marker.Problems(Shirt, moved, 140));
        Assert.Throws<ArgumentException>(() => Marker.AutoLayout([new("Riesig", 200, 300, 1)], 140));
        var f = Path.Combine(Path.GetTempPath(), $"trendo-marker-{Guid.NewGuid():N}.pdf");
        try { Marker.Pdf(f, "T-Shirt", Shirt, l, 140); Assert.True(new FileInfo(f).Length > 1000); Assert.StartsWith("%PDF", File.ReadAllText(f)[..4]); }
        finally { File.Delete(f); }
    }

    [AvaloniaFact(DisplayName = "Schnittlage UI: Teil ziehen speichert die Lage (Rückgängig), Doppelklick dreht, Überlappung wird gemeldet, automatisch legen räumt auf, PDF")]
    public void Ui_marker()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1100;
        w.Show("mode-programm"); Settle(); RealClick(w, V<Button>(w, "prog-tpl-tshirt")); Settle();
        var c = w.MarkerForTests!; c.BringIntoView(); Settle();
        var d = w.ProgramActive("mode-programm")!;
        // Teil 0 um 20 cm nach unten ziehen (Maßstab 3 px/cm)
        var a = c.TranslatePoint(c.CenterOf(0), w)!.Value; var b = c.TranslatePoint(c.CenterOf(0) + new Point(0, 60), w)!.Value; // 60 px der Fläche = 20 cm (Oberfläche kann skaliert sein)
        w.MouseDown(a, MouseButton.Left); for (var i = 1; i <= 4; i++) { w.MouseMove(a + (b - a) * (i / 4.0)); Settle(); } w.MouseUp(b, MouseButton.Left); Settle();
        var lay = MainWindow.MarkerLayout(d, 140); Assert.Equal("Teil verschieben", d.UndoLabel);
        var y0 = Marker.AutoLayout(Shirt, 140)[0].Y;
        Assert.Equal(y0 + 20, lay[0].Y, 0);
        // Doppelklick dreht
        c = w.MarkerForTests!; c.BringIntoView(); Settle(); var p = c.TranslatePoint(c.CenterOf(3), w)!.Value;
        w.MouseDown(p, MouseButton.Left, RawInputModifiers.None); w.MouseUp(p, MouseButton.Left); w.MouseDown(p, MouseButton.Left); w.MouseUp(p, MouseButton.Left); Settle();
        Assert.Equal("Teil drehen", d.UndoLabel); Assert.NotEqual(Marker.AutoLayout(Shirt, 140)[3].Rot, MainWindow.MarkerLayout(d, 140)[3].Rot);
        // Überlappung erzeugen (Teil 1 auf Teil 0) → Meldung
        var l2 = MainWindow.MarkerLayout(d, 140); l2[1] = l2[1] with { X = l2[0].X, Y = l2[0].Y };
        d.Set("layout", new System.Text.Json.Nodes.JsonArray([.. l2.Select(x => (System.Text.Json.Nodes.JsonNode)new System.Text.Json.Nodes.JsonObject { ["p"] = x.Piece, ["c"] = x.Copy, ["x"] = x.X, ["y"] = x.Y, ["r"] = x.Rot })]), "Test"); w.Show("mode-programm"); Settle();
        Assert.Contains("überlappen", w.Find<TextBlock>("mode-marker-facts").Text);
        var auto = V<Button>(w, "mode-marker-auto"); auto.BringIntoView(); Settle(); RealClick(w, V<Button>(w, "mode-marker-auto"));
        Assert.DoesNotContain("überlappen", w.Find<TextBlock>("mode-marker-facts").Text);
        var pdf = V<Button>(w, "mode-marker-pdf"); pdf.BringIntoView(); Settle(); RealClick(w, V<Button>(w, "mode-marker-pdf"));
        Assert.Single(Directory.GetFiles(root, "*Schnittlage.pdf", SearchOption.AllDirectories));
    }
}
