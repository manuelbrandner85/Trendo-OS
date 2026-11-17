using SkiaSharp;
using Trendo.Core.Kreativ;

namespace Trendo.App;

/// <summary>T-0696: zeichnet ein Vektor-Dokument direkt mit SkiaSharp in ein Bild – für PNG, Favicon und die Bilder des Videos.
/// Unabhängig vom Fenster (läuft auch im Hintergrund und ohne Bildschirm), gleiche Geometrie wie SVG und PDF (<see cref="VectorKit"/>).</summary>
public static class VectorSkia
{
    static SKColor C(string hex, double alpha = 1) { var (r, g, b) = VectorKit.Rgb(hex); return new SKColor((byte)r, (byte)g, (byte)b, (byte)Math.Round(Math.Clamp(alpha, 0, 1) * 255)); }

    static SKPath Path(VShape s)
    {
        var (n, closed) = VectorKit.ToNodes(s);
        var p = new SKPath();
        if (n.Count == 0) return p;
        p.MoveTo((float)n[0].X, (float)n[0].Y);
        var segs = closed ? n.Count : n.Count - 1;
        for (var i = 0; i < segs; i++)
        {
            var a = n[i]; var b = n[(i + 1) % n.Count];
            if (a.OutX == a.X && a.OutY == a.Y && b.InX == b.X && b.InY == b.Y) p.LineTo((float)b.X, (float)b.Y);
            else p.CubicTo((float)a.OutX, (float)a.OutY, (float)b.InX, (float)b.InY, (float)b.X, (float)b.Y);
        }
        if (closed) p.Close();
        return p;
    }

    static SKPaint Fill(VShape s, SKRect box)
    {
        var paint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = C(s.Fill) };
        if (s.Fill2.Length > 0 && VectorKit.IsColor(s.Fill2))
        {
            var (x1, y1, x2, y2) = VectorKit.GradientLine(s.GradAngle);
            paint.Shader = SKShader.CreateLinearGradient(new SKPoint(box.Left + (float)x1 * box.Width, box.Top + (float)y1 * box.Height), new SKPoint(box.Left + (float)x2 * box.Width, box.Top + (float)y2 * box.Height),
                [C(s.Fill), C(s.Fill2)], [0, 1], SKShaderTileMode.Clamp);
        }
        return paint;
    }

    /// <summary>Zeichnet alle Formen in <paramref name="c"/> (Dokument-Koordinaten).</summary>
    public static void Draw(SKCanvas c, VDoc d, string? background = null)
    {
        var bg = background ?? d.Background;
        if (VectorKit.IsColor(bg)) c.Clear(C(bg)); else c.Clear(SKColors.Transparent);
        foreach (var s in d.Shapes)
        {
            c.Save();
            c.Translate((float)s.Cx, (float)s.Cy); c.RotateDegrees((float)s.Rot); c.Scale((float)s.Scale); c.Translate((float)(-s.W / 2), (float)(-s.H / 2));
            var layered = s.Opacity < 1;
            if (layered) c.SaveLayer(new SKPaint { Color = new SKColor(0, 0, 0, (byte)Math.Round(Math.Clamp(s.Opacity, 0, 1) * 255)) });
            switch (s.Kind)
            {
                case "text":
                {
                    if (!VectorKit.IsColor(s.Fill)) break;
                    using var tf = SKTypeface.FromFamilyName(s.Font, s.Bold ? SKFontStyle.Bold : SKFontStyle.Normal) ?? SKTypeface.Default;
                    using var font = new SKFont(tf, (float)s.FontSize) { Subpixel = true, Edging = SKFontEdging.Antialias };
                    var lines = VectorKit.Lines(s);
                    var widths = lines.Select(l => font.MeasureText(l)).ToList();
                    var box = new SKRect(0, 0, (float)s.W, (float)s.H);
                    using var paint = Fill(s, box);
                    for (var i = 0; i < lines.Length; i++)
                    {
                        var x = s.Anchor == "middle" ? s.W / 2 - widths[i] / 2 : s.Anchor == "end" ? s.W - widths[i] : 0;
                        c.DrawText(lines[i], (float)x, (float)(s.FontSize * (VectorKit.TextBase + i * VectorKit.TextLine)), font, paint);
                    }
                    break;
                }
                case "image":
                    if (VectorKit.DataUri(s.Image) is { } bytes)
                    {
                        using var data = SKData.CreateCopy(bytes);
                        using var img = SKImage.FromEncodedData(data);
                        if (img is not null) c.DrawImage(img, new SKRect(0, 0, (float)s.W, (float)s.H), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
                    }
                    break;
                default:
                {
                    using var p = Path(s);
                    var closed = s.Kind != "line" && (s.Kind != "path" || s.Closed);
                    if (closed && s.Fill.Length > 0 && VectorKit.IsColor(s.Fill)) { using var f = Fill(s, p.Bounds); c.DrawPath(p, f); }
                    if (s.Stroke.Length > 0 && s.StrokeW > 0 && VectorKit.IsColor(s.Stroke))
                    {
                        using var st = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = (float)s.StrokeW, Color = C(s.Stroke), StrokeJoin = SKStrokeJoin.Round, StrokeCap = SKStrokeCap.Round };
                        c.DrawPath(p, st);
                    }
                    break;
                }
            }
            if (layered) c.Restore();
            c.Restore();
        }
    }

    /// <summary>Bild in <paramref name="pixelWidth"/> × <paramref name="pixelHeight"/> Pixel (Vektoren werden in dieser Größe gezeichnet, nicht hochgerechnet).</summary>
    public static SKBitmap Render(VDoc d, int pixelWidth, string? background = null, int? pixelHeight = null)
    {
        var ph = pixelHeight ?? (int)Math.Round(d.H * pixelWidth / d.W);
        var bmp = new SKBitmap(new SKImageInfo(pixelWidth, ph, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var c = new SKCanvas(bmp);
        c.Scale((float)(pixelWidth / d.W), (float)(ph / d.H));
        Draw(c, d, background);
        c.Flush();
        return bmp;
    }

    public static byte[] Png(VDoc d, int pixelWidth, string? background = null, int? pixelHeight = null)
    {
        using var bmp = Render(d, pixelWidth, background, pixelHeight);
        using var img = SKImage.FromBitmap(bmp);
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
