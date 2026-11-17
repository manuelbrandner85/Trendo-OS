using Trendo.Core.Kreativ;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0733: Auswahl im Künstlerstudio – Maske aus Rechteck und Lasso, Inhalt verschieben, skalieren und löschen.</summary>
public class KuenstlerAuswahlTests
{
    static Pixmap Square(int w, int h, int x0, int y0, int size)
    {
        var p = new Pixmap(w, h);
        for (var y = y0; y < y0 + size; y++) for (var x = x0; x < x0 + size; x++) { var i = (y * w + x) * 4; p.Px[i] = 200; p.Px[i + 1] = 10; p.Px[i + 2] = 20; p.Px[i + 3] = 255; }
        return p;
    }
    static byte A(Pixmap p, int x, int y) => p.Px[(y * p.W + x) * 4 + 3];

    [Fact(DisplayName = "Auswahl: Rechteck-Maske zählt genau die Pixel, Lasso-Dreieck etwa die Hälfte, außerhalb der Leinwand nichts")]
    public void Masks()
    {
        Assert.Equal(40 * 30, PaintSelection.Count(PaintSelection.Mask(100, 100, PaintSelection.Rect(60, 70, 20, 40))));
        var tri = PaintSelection.Count(PaintSelection.Mask(100, 100, [(0, 0), (80, 0), (0, 80)]));
        Assert.InRange(tri, 80 * 80 / 2 - 80, 80 * 80 / 2 + 80);
        Assert.Equal(0, PaintSelection.Count(PaintSelection.Mask(100, 100, PaintSelection.Rect(200, 200, 300, 300))));
        Assert.Equal((10.0, 20.0, 30.0, 5.0), PaintSelection.Bounds(PaintSelection.Rect(40, 25, 10, 20)));
    }

    [Fact(DisplayName = "Auswahl verschieben: Inhalt wandert, alte Stelle wird durchsichtig, Vieleck zieht mit; Rest der Ebene bleibt")]
    public void Move()
    {
        var p = Square(200, 200, 20, 20, 20); p.Px[(150 * 200 + 150) * 4 + 3] = 255; // Punkt außerhalb der Auswahl
        var moved = PaintSelection.Transform(p, PaintSelection.Rect(20, 20, 40, 40), 100, 50);
        Assert.Equal(0, A(p, 30, 30)); Assert.Equal(255, A(p, 130, 80)); Assert.Equal(200, p.Px[(80 * 200 + 130) * 4]);
        Assert.Equal(255, A(p, 150, 150));
        Assert.Equal((120.0, 70.0, 20.0, 20.0), PaintSelection.Bounds(moved));
    }

    [Fact(DisplayName = "Auswahl skalieren: 20-px-Quadrat auf 200 % wird 40 px groß (Anker oben links), danach auf 50 % wieder 20 px")]
    public void Scale()
    {
        var p = Square(200, 200, 20, 20, 20);
        var big = PaintSelection.Transform(p, PaintSelection.Rect(20, 20, 40, 40), 0, 0, 2, 2);
        Assert.Equal(255, A(p, 21, 21)); Assert.Equal(255, A(p, 58, 58)); Assert.Equal(0, A(p, 61, 61));
        Assert.Equal((20.0, 20.0, 40.0, 40.0), PaintSelection.Bounds(big));
        var small = PaintSelection.Transform(p, big, 0, 0, 0.5, 0.5);
        Assert.Equal(255, A(p, 35, 35)); Assert.Equal(0, A(p, 45, 45));
        Assert.Equal((20.0, 20.0, 20.0, 20.0), PaintSelection.Bounds(small));
        Assert.Throws<ArgumentException>(() => PaintSelection.Transform(p, small, 0, 0, 0, 1));
        Assert.Throws<InvalidOperationException>(() => PaintSelection.Transform(p, [(1, 1), (2, 2)], 1, 1));
    }

    [Fact(DisplayName = "Lasso-Auswahl löschen entfernt nur den umfahrenen Teil")]
    public void Clear_lasso()
    {
        var p = Square(100, 100, 0, 0, 100);
        var n = PaintSelection.Clear(p, [(0, 0), (60, 0), (0, 60)]);
        Assert.InRange(n, 1700, 1900); Assert.Equal(0, A(p, 5, 5)); Assert.Equal(255, A(p, 90, 90));
    }
}
