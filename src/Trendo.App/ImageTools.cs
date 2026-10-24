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

    /// <summary>T-0205: Bearbeitung in einem Schritt. Zuschnitt in Pixeln (null = ganzes Bild), Drehung 0/90/180/270,
    /// Helligkeit und Kontrast −100…100, optionaler Text unten links. Ergebnis als Kopie (.jpg oder .png).</summary>
    public sealed record Edit(int? CropX = null, int? CropY = null, int? CropW = null, int? CropH = null, int Rotate = 0,
        int Brightness = 0, int Contrast = 0, string? Text = null, int TextSize = 48, string Format = ".jpg");

    public static string Apply(string file, Edit e, string? output = null)
    {
        if (e.Rotate % 90 != 0) throw new ArgumentException("Drehen geht nur in 90-Grad-Schritten.");
        if (e.Format is not (".jpg" or ".png")) throw new ArgumentException("Bitte JPG oder PNG.");
        using var src = Load(file);
        var rect = new SKRectI(e.CropX ?? 0, e.CropY ?? 0, (e.CropX ?? 0) + (e.CropW ?? src.Width), (e.CropY ?? 0) + (e.CropH ?? src.Height));
        if (rect.Left < 0 || rect.Top < 0 || rect.Width < 1 || rect.Height < 1 || rect.Right > src.Width || rect.Bottom > src.Height)
            throw new ArgumentException($"Zuschnitt liegt außerhalb des Bildes ({src.Width}×{src.Height}).");
        using var cut = new SKBitmap(rect.Width, rect.Height);
        using (var c = new SKCanvas(cut))
        {
            // Helligkeit/Kontrast als Farbmatrix: Kontrast um die Mitte, Helligkeit als Verschiebung
            float k = 1 + Math.Clamp(e.Contrast, -100, 100) / 100f, b = Math.Clamp(e.Brightness, -100, 100) / 100f * 255f, off = 128 * (1 - k) + b;
            using var p = new SKPaint { ColorFilter = SKColorFilter.CreateColorMatrix([k, 0, 0, 0, off, 0, k, 0, 0, off, 0, 0, k, 0, off, 0, 0, 0, 1, 0]) };
            c.DrawBitmap(src, rect, new SKRect(0, 0, rect.Width, rect.Height), p);
        }
        var deg = ((e.Rotate % 360) + 360) % 360; var swap = deg % 180 != 0;
        using var dst = new SKBitmap(swap ? cut.Height : cut.Width, swap ? cut.Width : cut.Height);
        using (var c = new SKCanvas(dst))
        {
            c.Translate(dst.Width / 2f, dst.Height / 2f); c.RotateDegrees(deg); c.Translate(-cut.Width / 2f, -cut.Height / 2f); c.DrawBitmap(cut, 0, 0);
            if (!string.IsNullOrWhiteSpace(e.Text))
            {
                c.ResetMatrix();
                using var font = new SKFont(SKTypeface.Default, Math.Clamp(e.TextSize, 8, 400));
                using var shadow = new SKPaint { Color = new SKColor(0, 0, 0, 170), IsAntialias = true };
                using var ink = new SKPaint { Color = SKColors.White, IsAntialias = true };
                var y = dst.Height - font.Size * 0.6f;
                c.DrawText(e.Text, font.Size * 0.5f + 2, y + 2, font, shadow);
                c.DrawText(e.Text, font.Size * 0.5f, y, font, ink);
            }
        }
        var o = output ?? CopyName(file, "bearbeitet", e.Format);
        for (var n = 2; output is null && File.Exists(o); n++) o = CopyName(file, $"bearbeitet-{n}", e.Format);
        using var img = SKImage.FromBitmap(dst);
        using var data = img.Encode(e.Format == ".png" ? SKEncodedImageFormat.Png : SKEncodedImageFormat.Jpeg, 92);
        using (var fs = File.Create(o)) data.SaveTo(fs);
        return o;
    }
}
