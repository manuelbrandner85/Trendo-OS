using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Media.Imaging;
using Trendo.App;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Glas-Unschärfe wird vorab als Bitmap berechnet, nicht mehr live per BlurEffect.</summary>
public class GlassBlurTests
{
    [AvaloniaFact(DisplayName = "Glas: kein Live-BlurEffect, vorab weichgezeichnete Bitmap in kleiner Größe")]
    public void Backdrop_uses_prebaked_bitmap()
    {
        var (w, _, _) = Ui.Open();
        var b = w.GetLogicalDescendants().OfType<Border>().FirstOrDefault(x => x.Name == "backdrop-blur");
        if (w.LowPower) { Assert.Null(b); return; } // Sparprofil: kein Glas
        Assert.NotNull(b);
        var img = Assert.IsType<Image>(b!.Child);
        Assert.Null(img.Effect);
        var bmp = Assert.IsType<Bitmap>(img.Source);
        Assert.True(bmp.PixelSize.Width <= GlassBlur.TargetWidth, $"Breite {bmp.PixelSize.Width}");
    }

    [AvaloniaFact(DisplayName = "Glas: gleiche Wallpaper wird nur einmal weichgezeichnet (Cache)")]
    public void Cache_computes_once()
    {
        var calls = 0;
        byte[] png; using (var sk = new SkiaSharp.SKBitmap(64, 36)) { sk.Erase(SkiaSharp.SKColors.SteelBlue); using var d = sk.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100); png = d.ToArray(); }
        var a = GlassBlur.Get("test-cache-x", () => { calls++; return new System.IO.MemoryStream(png); });
        var c = GlassBlur.Get("test-cache-x", () => { calls++; return new System.IO.MemoryStream(png); });
        Assert.Equal(1, calls);
        Assert.Same(a, c);
        Assert.NotNull(a);
    }

    [Fact(DisplayName = "Glas: großes Bild wird auf 480 px verkleinert und wirklich weichgezeichnet")]
    public void Blur_downscales_and_softens()
    {
        byte[] png; using (var sk = new SkiaSharp.SKBitmap(1920, 1080)) { sk.Erase(SkiaSharp.SKColors.Black); using (var cv = new SkiaSharp.SKCanvas(sk)) cv.DrawRect(0, 0, 960, 1080, new SkiaSharp.SKPaint { Color = SkiaSharp.SKColors.White }); using var d = sk.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100); png = d.ToArray(); }
        using var outBmp = SkiaSharp.SKBitmap.Decode(GlassBlur.BlurToPng(new System.IO.MemoryStream(png)));
        Assert.Equal(480, outBmp.Width); Assert.Equal(270, outBmp.Height);
        var edge = outBmp.GetPixel(240, 135).Red; // an der harten Kante: Zwischenton statt Schwarz/Weiß
        Assert.InRange(edge, 40, 215);
    }

    [AvaloniaFact(DisplayName = "Leistungsprofil: TRENDO_PROFILE=pi erzwingt sparsam, voll erzwingt Glas")]
    public void Profile_override()
    {
        var (w, _, _) = Ui.Open();
        var old = Environment.GetEnvironmentVariable("TRENDO_PROFILE");
        try
        {
            Environment.SetEnvironmentVariable("TRENDO_PROFILE", "pi"); Assert.True(w.LowPower);
            Environment.SetEnvironmentVariable("TRENDO_PROFILE", "voll"); Assert.False(w.LowPower);
        }
        finally { Environment.SetEnvironmentVariable("TRENDO_PROFILE", old); w.Close(); }
    }
}
