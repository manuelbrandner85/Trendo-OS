using System.Text.Json.Nodes;
using SkiaSharp;
using Trendo.Core.Media;

namespace Trendo.App;

/// <summary>Zeichnet ein <see cref="ImageDoc"/> mit Skia: Hintergrund, dann Ebene für Ebene (Deckkraft, Filter, Unschärfe);
/// Mal-Ebenen bekommen eine eigene Fläche, damit der Radierer nur dort löscht.</summary>
public static class ImageRender
{
    static readonly Dictionary<string, SKBitmap> Cache = [];

    static SKBitmap? Source(string file)
    {
        if (Cache.TryGetValue(file, out var b)) return b;
        if (!File.Exists(file)) return null;
        var bmp = SKBitmap.Decode(file); if (bmp is null) return null;
        if (Cache.Count > 24) { foreach (var x in Cache.Values) x.Dispose(); Cache.Clear(); }
        return Cache[file] = bmp;
    }

    static SKColor Col(string hex, SKColor fallback) => SKColor.TryParse(hex, out var c) ? c : fallback;

    public static SKBitmap Render(JsonObject d, bool transparentBackground = false)
    {
        int w = Math.Max(1, (int)ImageDoc.N(d["width"], 800)), h = Math.Max(1, (int)ImageDoc.N(d["height"], 600));
        var bmp = new SKBitmap(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var c = new SKCanvas(bmp);
        c.Clear(transparentBackground ? SKColors.Transparent : Col(ImageDoc.S(d["background"], "#FFFFFF"), SKColors.White));
        foreach (var l in ImageDoc.Layers(d).OfType<JsonObject>())
        {
            if (l["visible"]?.GetValue<bool>() == false) continue;
            var op = (byte)Math.Clamp(ImageDoc.N(l["opacity"], 1) * 255, 0, 255);
            float x = (float)ImageDoc.N(l["x"]), y = (float)ImageDoc.N(l["y"]), lw = (float)ImageDoc.N(l["w"]), lh = (float)ImageDoc.N(l["h"]);
            var f = l["filter"] as JsonObject; var blur = (float)(f is null ? 0 : ImageDoc.N(f["blur"]));
            using var layerPaint = new SKPaint { Color = new SKColor(255, 255, 255, op), ColorFilter = SKColorFilter.CreateColorMatrix(ImageDoc.ColorMatrix(f)), ImageFilter = blur > 0 ? SKImageFilter.CreateBlur(blur, blur) : null };
            layerPaint.BlendMode = ImageDoc.S(l["blend"], "normal") switch // Mischmodi wie in Krita/GIMP
            {
                "multiply" => SKBlendMode.Multiply, "screen" => SKBlendMode.Screen, "overlay" => SKBlendMode.Overlay, "darken" => SKBlendMode.Darken,
                "lighten" => SKBlendMode.Lighten, "difference" => SKBlendMode.Difference, _ => SKBlendMode.SrcOver,
            };
            c.SaveLayer(layerPaint);
            switch (ImageDoc.S(l["kind"]))
            {
                case "image":
                    if (Source(ImageDoc.S(l["file"])) is { } src) c.DrawBitmap(src, new SKRect(x, y, x + lw, y + lh), new SKPaint { IsAntialias = true, FilterQuality = SKFilterQuality.High });
                    break;
                case "text":
                {
                    var size = (float)ImageDoc.N(l["size"], 64);
                    using var font = new SKFont(SKTypeface.FromFamilyName("Inter") ?? SKTypeface.Default, size);
                    using var shadow = new SKPaint { Color = new SKColor(0, 0, 0, 140), IsAntialias = true, ImageFilter = SKImageFilter.CreateBlur(size / 18, size / 18) };
                    using var ink = new SKPaint { Color = Col(ImageDoc.S(l["color"], "#FFFFFF"), SKColors.White), IsAntialias = true };
                    var lines = ImageDoc.S(l["text"]).Split('\n'); var yy = y + size;
                    foreach (var line in lines) { c.DrawText(line, x + size * 0.04f, yy + size * 0.05f, font, shadow); c.DrawText(line, x, yy, font, ink); yy += size * 1.2f; }
                    break;
                }
                case "shape":
                {
                    var r = new SKRect(x, y, x + lw, y + lh);
                    using var fill = new SKPaint { Color = Col(ImageDoc.S(l["fill"], "#2F86F6"), SKColors.Transparent), IsAntialias = true, Style = SKPaintStyle.Fill };
                    var sw = (float)ImageDoc.N(l["strokeWidth"]);
                    using var stroke = new SKPaint { Color = Col(ImageDoc.S(l["stroke"]), SKColors.Transparent), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = sw };
                    if (ImageDoc.S(l["shape"]) == "ellipse") { c.DrawOval(r, fill); if (sw > 0) c.DrawOval(r, stroke); }
                    else { c.DrawRoundRect(r, Math.Min(lw, lh) * 0.04f, Math.Min(lw, lh) * 0.04f, fill); if (sw > 0) c.DrawRoundRect(r, Math.Min(lw, lh) * 0.04f, Math.Min(lw, lh) * 0.04f, stroke); }
                    break;
                }
                case "path":
                {
                    var pts = Trendo.Core.Media.ImageVector.Points(l); if (pts.Count < 2) break;
                    using var p = SKPath.ParseSvgPathData(Trendo.Core.Media.ImageVector.PathData(pts, l["closed"]?.GetValue<bool>() == true, l["smooth"]?.GetValue<bool>() != false));
                    if (ImageDoc.S(l["fill"]) is { Length: > 0 } pf) { using var fp = new SKPaint { Color = Col(pf, SKColors.Transparent), IsAntialias = true, Style = SKPaintStyle.Fill }; c.DrawPath(p, fp); }
                    if (ImageDoc.S(l["stroke"]) is { Length: > 0 } ps && ImageDoc.N(l["strokeWidth"], 4) > 0)
                    { using var sp = new SKPaint { Color = Col(ps, SKColors.Black), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = (float)ImageDoc.N(l["strokeWidth"], 4), StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round }; c.DrawPath(p, sp); }
                    break;
                }
                case "paint":
                    foreach (var st in (l["strokes"] as JsonArray ?? []).OfType<JsonObject>())
                    {
                        var pts = (st["points"] as JsonArray ?? []).Select(n => (float)ImageDoc.N(n)).ToArray(); if (pts.Length < 2) continue;
                        var erase = st["erase"]?.GetValue<bool>() == true;
                        var ink = Col(ImageDoc.S(st["color"], "#000000"), SKColors.Black); ink = ink.WithAlpha((byte)Math.Clamp(ImageDoc.N(st["alpha"], 1) * 255, 0, 255));
                        using var pen = new SKPaint { Color = erase ? SKColors.Transparent : ink, BlendMode = erase ? SKBlendMode.Clear : SKBlendMode.SrcOver,
                            StrokeWidth = (float)ImageDoc.N(st["size"], 8), StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round, Style = SKPaintStyle.Stroke, IsAntialias = true };
                        using var path = new SKPath(); path.MoveTo(pts[0], pts[1]);
                        if (pts.Length == 2) path.LineTo(pts[0] + 0.01f, pts[1]);
                        for (var i = 2; i + 1 < pts.Length; i += 2) path.LineTo(pts[i], pts[i + 1]);
                        c.DrawPath(path, pen);
                    }
                    break;
            }
            c.Restore();
        }
        return bmp;
    }

    public static byte[] Png(JsonObject d)
    {
        using var b = Render(d); using var img = SKImage.FromBitmap(b); using var data = img.Encode(SKEncodedImageFormat.Png, 100); return data.ToArray();
    }

    /// <summary>Als PNG (mit Transparenz, wenn der Hintergrund „transparent“ ist) oder JPG speichern.</summary>
    public static string Export(JsonObject d, string path)
    {
        var jpg = Path.GetExtension(path).ToLowerInvariant() is ".jpg" or ".jpeg";
        using var b = Render(d, !jpg && ImageDoc.S(d["background"]) == "transparent");
        using var img = SKImage.FromBitmap(b); using var data = img.Encode(jpg ? SKEncodedImageFormat.Jpeg : SKEncodedImageFormat.Png, jpg ? 92 : 100);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using (var fs = File.Create(path)) data.SaveTo(fs);
        return path;
    }
}
