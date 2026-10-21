using SkiaSharp;

namespace Trendo.App;

/// <summary>SYS-02 Bildwerkzeuge (Skia): Drehen, Verkleinern, Schwärzen, Farbe an einer Stelle. Das Original bleibt immer unverändert – es entsteht eine Kopie.</summary>
public static class ImageTools
{
    static string CopyName(string file, string suffix, string ext = ".png") => Path.Combine(Path.GetDirectoryName(file)!, Path.GetFileNameWithoutExtension(file) + "-" + suffix + ext);

    static void Save(SKBitmap bmp, string path)
    {
        using var img = SKImage.FromBitmap(bmp); using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        using var fs = File.Create(path); data.SaveTo(fs);
    }

    static SKBitmap Load(string file) => SKBitmap.Decode(file) ?? throw new InvalidOperationException("Das Bild lässt sich nicht öffnen.");

    public static string Rotate(string file, int degrees)
    {
        using var src = Load(file);
        var swap = degrees % 180 != 0;
        using var dst = new SKBitmap(swap ? src.Height : src.Width, swap ? src.Width : src.Height);
        using (var c = new SKCanvas(dst)) { c.Translate(dst.Width / 2f, dst.Height / 2f); c.RotateDegrees(degrees); c.Translate(-src.Width / 2f, -src.Height / 2f); c.DrawBitmap(src, 0, 0); }
        var o = CopyName(file, "gedreht"); Save(dst, o); return o;
    }

    public static string Resize(string file, int maxSide)
    {
        if (maxSide < 16) throw new ArgumentException("Bitte mindestens 16 Pixel.");
        using var src = Load(file);
        var k = Math.Min(1.0, maxSide / (double)Math.Max(src.Width, src.Height));
        using var dst = src.Resize(new SKImageInfo(Math.Max(1, (int)(src.Width * k)), Math.Max(1, (int)(src.Height * k))), new SKSamplingOptions(SKCubicResampler.Mitchell)) ?? throw new InvalidOperationException("Verkleinern hat nicht geklappt.");
        var o = CopyName(file, $"{dst.Width}x{dst.Height}"); Save(dst, o); return o;
    }

    /// <summary>Schwärzt Bereiche (x,y,b,h in Pixeln) dauerhaft: Die Pixel werden überschrieben, die Kopie enthält keine Ebenen.</summary>
    public static string Redact(string file, IEnumerable<(int X, int Y, int W, int H)> areas)
    {
        using var bmp = Load(file);
        using (var c = new SKCanvas(bmp)) using (var p = new SKPaint { Color = SKColors.Black, Style = SKPaintStyle.Fill })
            foreach (var a in areas) c.DrawRect(a.X, a.Y, a.W, a.H, p);
        var o = CopyName(file, "geschwaerzt"); Save(bmp, o); return o;
    }

    public static (byte R, byte G, byte B) ColorAt(string file, int x, int y)
    {
        using var bmp = Load(file);
        if (x < 0 || y < 0 || x >= bmp.Width || y >= bmp.Height) throw new ArgumentException($"Stelle liegt außerhalb des Bildes ({bmp.Width}×{bmp.Height}).");
        var c = bmp.GetPixel(x, y); return (c.Red, c.Green, c.Blue);
    }

    public static (int W, int H) Size(string file) { using var b = Load(file); return (b.Width, b.Height); }
}
