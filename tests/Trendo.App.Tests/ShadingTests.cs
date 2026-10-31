using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0281: 3D-Vorschau – Licht, Kontaktverdunklung, Schattenwurf und Ausrichtung (Z oben) im Modell-Prüfer.</summary>
public class Shading_3D
{
    static double Lum(Color c) => 0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B;

    [Fact(DisplayName = "T-0281: Licht – zum Hauptlicht hell, abgewandt dunkler, Oberseite heller als Unterseite, Kontaktverdunklung am Boden")]
    public void Light()
    {
        var grey = Color.FromRgb(128, 128, 128); var view = new Vec3(0, -1, 0.4).Norm();
        var lit = Shading.Shade(grey, Shading.Key, view); var away = Shading.Shade(grey, Shading.Key * -1, view);
        Assert.True(Lum(lit) > Lum(away) + 40, $"{Lum(lit)} / {Lum(away)}");
        Assert.True(Lum(Shading.Shade(grey, new Vec3(0, 0, 1), view)) > Lum(Shading.Shade(grey, new Vec3(0, 0, -1), view)));
        Assert.True(Shading.GroundAo(0, 10) < 0.65 && Shading.GroundAo(20, 10) == 1);
        Assert.True(Lum(Shading.Shade(grey, Shading.Key, view, 0.6)) < Lum(lit));
        Assert.InRange(Lum(Shading.Shade(Color.FromRgb(255, 255, 255), Shading.Key, view)), 200, 255); // Weiß wird hell, aber nicht abgeschnitten-grell
    }

    [Fact(DisplayName = "T-0281: Schatten fällt vom Licht weg auf den Boden – Fußpunkt bleibt, höhere Punkte wandern weiter")]
    public void Shadow_projection()
    {
        var foot = Shading.OnGround(new Vec3(10, 20, 0), 0); Assert.Equal((10.0, 20.0, 0.0), (foot.X, foot.Y, foot.Z));
        var top = Shading.OnGround(new Vec3(10, 20, 100), 0);
        Assert.Equal(0, top.Z); Assert.True(Vec3.Dot(new Vec3(top.X - 10, top.Y - 20, 0), new Vec3(Shading.Key.X, Shading.Key.Y, 0)) < 0); // vom Licht weg
        Assert.InRange(new Vec3(top.X - 10, top.Y - 20, 0).Len, 60, 120);
    }

    [AvaloniaFact(DisplayName = "T-0281: Modell-Prüfer zeigt Z oben – eine hohe Säule steht hoch, Vorderansicht zeigt die Front (−Y)")]
    public void MeshView_z_up()
    {
        var m = new Mesh();
        Vec3[] c = [new(0, 0, 0), new(10, 0, 0), new(10, 10, 0), new(0, 10, 0), new(0, 0, 100), new(10, 0, 100), new(10, 10, 100), new(0, 10, 100)];
        foreach (var (a, b, d) in new[] { (0, 1, 5), (0, 5, 4), (1, 2, 6), (1, 6, 5), (2, 3, 7), (2, 7, 6), (3, 0, 4), (3, 4, 7), (4, 5, 6), (4, 6, 7), (0, 2, 1), (0, 3, 2) }) m.Add(c[a], c[b], c[d]);
        var v = new MeshView { Model = m }; v.Measure(new Size(400, 400)); v.Arrange(new Rect(0, 0, 400, 400)); v.SetView("vorne");
        Point Lo = v.Screen(new Vec3(5, 5, 0)), Hi = v.Screen(new Vec3(5, 5, 100)), Side = v.Screen(new Vec3(10, 5, 0));
        Assert.True(Lo.Y - Hi.Y > 150, $"Höhe auf dem Schirm {Lo.Y - Hi.Y:0}"); // oben ist oben
        Assert.True(Side.X > Lo.X); Assert.True(Math.Abs(Side.Y - Lo.Y) < 1);
        v.SetView("iso"); Assert.True(v.Screen(new Vec3(5, 5, 100)).Y < v.Screen(new Vec3(5, 5, 0)).Y);
        var img = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(200, 200)); img.Render(v); // zeichnet ohne Fehler (Licht, Schatten, Boden)
    }
}
