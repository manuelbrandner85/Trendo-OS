using Trendo.Core;
using Trendo.Core.Cad;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>3D-Druck – Vorbild-Funktionen (PrusaSlicer, Cura, Bambu Studio; 04.11.2026): Umrisse, Füllung, Schätzung aus Bahnen, Material, Stützen, Skalieren, Anordnen.</summary>
public class SlicerTests
{
    static Mesh Box(double x0, double x1, double y0, double y1, double z0, double z1, Mesh? m = null) { m ??= new Mesh(); Mesh3D.AddBox(m, x0, x1, y0, y1, z0, z1); return m; }

    [Fact(DisplayName = "CORE-05: Umrisse: Würfelschnitt ergibt einen geschlossenen Umriss (Umfang 80 mm, Fläche 400 mm²); zwei Körper zwei Umrisse")]
    public void Loops()
    {
        var loops = Slicer.Loops(PrintPrep.Slice(Box(0, 20, 0, 20, 0, 10), 5));
        Assert.Single(loops); Assert.Equal(80, Slicer.Perimeter(loops[0]), 3); Assert.Equal(400, Slicer.Area(loops[0]), 3);
        var two = Box(40, 50, 0, 10, 0, 10, Box(0, 20, 0, 20, 0, 10));
        Assert.Equal(2, Slicer.Loops(PrintPrep.Slice(two, 5)).Count);
    }

    [Fact(DisplayName = "CORE-05: Füllung: 100 % deckt die Fläche (Länge ≈ Fläche / Linienbreite), läuft nicht in die Lücke zwischen zwei Körpern, Winkel wechselt")]
    public void Infill()
    {
        var loops = Slicer.Loops(PrintPrep.Slice(Box(0, 20, 0, 20, 0, 10), 5));
        var full = Slicer.Infill([.. loops], 0.42, 45);
        Assert.InRange(full.Sum(s => Math.Sqrt((s.X2 - s.X1) * (s.X2 - s.X1) + (s.Y2 - s.Y1) * (s.Y2 - s.Y1))), 400 / 0.42 * 0.95, 400 / 0.42 * 1.05);
        var two = Slicer.Loops(PrintPrep.Slice(Box(30, 40, 0, 20, 0, 10, Box(0, 20, 0, 20, 0, 10)), 5));
        var fill = Slicer.Infill([.. two], 1, 0);
        Assert.DoesNotContain(fill, s => Math.Min(s.X1, s.X2) < 25 && Math.Max(s.X1, s.X2) > 25); // keine Bahn über die Lücke 20–30 mm
        Assert.Empty(Slicer.Infill([], 1, 0));
        var st = new Slicer.Settings(Infill: 20);
        var (_, a) = Slicer.Layer(Box(0, 20, 0, 20, 0, 10), 10, 50, st); var (_, b) = Slicer.Layer(Box(0, 20, 0, 20, 0, 10), 11, 50, st);
        Assert.True(a.Count > 0 && b.Count > 0); Assert.NotEqual(Math.Sign((a[0].X2 - a[0].X1) * (a[0].Y2 - a[0].Y1)), Math.Sign((b[0].X2 - b[0].X1) * (b[0].Y2 - b[0].Y1)));
    }

    [Fact(DisplayName = "CORE-05: Schätzung aus Bahnen: massiver Würfel wiegt etwa Volumen × Dichte, 15 % Füllung deutlich weniger, PETG schwerer und teurer als PLA")]
    public void Estimate()
    {
        var cube = Box(0, 20, 0, 20, 0, 20);
        var solid = Slicer.Estimate(cube, new Slicer.Settings(Infill: 100, Walls: 2));
        Assert.Equal(100, solid.Layers);
        Assert.InRange(solid.Grams, 8000 / 1000.0 * 1.24 * 0.85, 8000 / 1000.0 * 1.24 * 1.35); // Wände überlappen die Füllung ein wenig
        var light = Slicer.Estimate(cube, new Slicer.Settings(Infill: 15));
        Assert.True(light.Grams < solid.Grams * 0.6 && light.Time < solid.Time, $"{light.Grams} g / {solid.Grams} g");
        var big = Box(0, 60, 0, 60, 0, 30); var pla = Slicer.Estimate(big, new Slicer.Settings(Infill: 15)); var petg = Slicer.Estimate(big, new Slicer.Settings(Infill: 15, Material: "petg"));
        Assert.True(petg.Grams > pla.Grams && petg.Euro > pla.Euro, $"PETG {petg.Grams} g {petg.Euro} € / PLA {pla.Grams} g {pla.Euro} €");
        Assert.Equal(Math.Round(light.Grams / 1000 * 20, 2), light.Euro, 2);
        Assert.Equal(0, light.SupportGrams);
        Assert.Equal(50, Slicer.Estimate(cube, new Slicer.Settings(Layer: 0.4)).Layers);
    }

    [Fact(DisplayName = "CORE-05: Stützen: Tisch-Form (Platte auf dünnem Fuß) braucht Stützen unter der Platte, Würfel nicht")]
    public void Supports()
    {
        var table = Box(0, 20, 0, 20, 10, 12, Box(8, 12, 8, 12, 0, 10));
        Assert.InRange(Slicer.SupportVolume(table), 3800, 4200); // 400 mm² × 10 mm Höhe
        Assert.Equal(0, Slicer.SupportVolume(Box(0, 10, 0, 10, 0, 10)));
        Assert.True(Slicer.Estimate(table, new Slicer.Settings()).SupportGrams > 0);
    }

    [Fact(DisplayName = "CORE-05: Skalieren, Anordnen in Reihen (mittig, mit Abstand), Kopien auf der Platte; zu viele Teile oder zu groß gibt eine klare Meldung")]
    public void Scale_arrange_copies()
    {
        var p = PrintPrep.BambuA1;
        var half = Slicer.Scale(Box(0, 40, 0, 20, 0, 10), 50); var (lo, hi) = PrintPrep.Bounds(half);
        Assert.Equal((20.0, 10.0, 5.0), (hi.X - lo.X, hi.Y - lo.Y, hi.Z - lo.Z));
        Assert.Throws<ArgumentException>(() => Slicer.Scale(half, 0));
        var pos = Slicer.Arrange([(100, 100), (100, 100), (100, 100), (100, 100)], p);
        Assert.Equal(4, pos.Distinct().Count());
        Assert.All(pos, q => Assert.True(q.X >= 0 && q.Y >= 0 && q.X + 100 <= p.W && q.Y + 100 <= p.D));
        Assert.Equal(p.W / 2, (pos.Min(q => q.X) + pos.Max(q => q.X) + 100) / 2, 1);
        Assert.Contains("höchstens", Assert.Throws<InvalidOperationException>(() => Slicer.Arrange([.. Enumerable.Repeat((100.0, 100.0), 9)], p)).Message);
        Assert.Throws<InvalidOperationException>(() => Slicer.Arrange([(300, 10)], p));
        var copies = Slicer.Copies(Box(0, 30, 0, 30, 0, 5), 6, p);
        Assert.Equal(12 * 6, copies.Triangles);
        var (clo, chi) = PrintPrep.Bounds(copies); Assert.True(clo.X >= 0 && clo.Y >= 0 && chi.X <= p.W && chi.Y <= p.D && clo.Z == 0);
        Assert.Equal(6 * 30 * 30 * 5, PrintPrep.Volume(copies), 0);
    }
}
