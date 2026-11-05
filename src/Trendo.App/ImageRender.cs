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
            using var layerPaint = new SKPaint { Color = new SKColor(255, 255, 255, op), ColorFilter = LayerColor(f), ImageFilter = blur > 0 ? SKImageFilter.CreateBlur(blur, blur) : null };
            layerPaint.BlendMode = ImageDoc.S(l["blend"], "normal") switch // Mischmodi wie in Krita/GIMP
            {
                "multiply" => SKBlendMode.Multiply, "screen" => SKBlendMode.Screen, "overlay" => SKBlendMode.Overlay, "darken" => SKBlendMode.Darken,
                "lighten" => SKBlendMode.Lighten, "difference" => SKBlendMode.Difference, _ => SKBlendMode.SrcOver,
            };
            // Retusche wirkt auf alles darunter: bisheriges Ergebnis abgreifen, bevor die Ebene beginnt
            using var retouched = ImageDoc.S(l["kind"]) == "retouch" ? RetouchBitmap(c, bmp, l) : null;
            c.SaveLayer(layerPaint);
            switch (ImageDoc.S(l["kind"]))
            {
                case "retouch":
                    if (retouched is not null) c.DrawBitmap(retouched, 0, 0);
                    break;
                case "gradient":
                {
                    var c1 = Col(ImageDoc.S(l["c1"], "#000000"), SKColors.Black); var c2 = Col(ImageDoc.S(l["c2"], "#00FFFFFF"), SKColors.Transparent);
                    SKPoint p1 = new((float)ImageDoc.N(l["x1"]), (float)ImageDoc.N(l["y1"])), p2 = new((float)ImageDoc.N(l["x2"]), (float)ImageDoc.N(l["y2"]));
                    var len = Math.Max(1f, SKPoint.Distance(p1, p2));
                    using var sh = l["radial"]?.GetValue<bool>() == true ? SKShader.CreateRadialGradient(p1, len, [c1, c2], null, SKShaderTileMode.Clamp) : SKShader.CreateLinearGradient(p1, p2, [c1, c2], null, SKShaderTileMode.Clamp);
                    using var gp = new SKPaint { Shader = sh, IsAntialias = true, IsDither = true };
                    c.DrawRect(0, 0, w, h, gp);
                    break;
                }
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
            if (l["mask"] is JsonObject mk) ApplyMask(c, mk);
            c.Restore();
        }
        return bmp;
    }

    /// <summary>Farbmatrix (Helligkeit, Kontrast …) und danach Tonwerte/Kurve als Tabelle.</summary>
    static readonly byte[] Identity = [.. Enumerable.Range(0, 256).Select(i => (byte)i)];
    static SKColorFilter LayerColor(JsonObject? f)
    {
        var m = SKColorFilter.CreateColorMatrix(ImageDoc.ColorMatrix(f));
        if (!ImageOps.HasLut(f)) return m;
        var t = ImageOps.Lut(f);
        return SKColorFilter.CreateCompose(SKColorFilter.CreateTable(Identity, t, t, t), m);
    }

    /// <summary>Ebenenmaske: außerhalb (oder bei „umkehren“ innerhalb) der Form wird die Ebene ausgeblendet, mit weicher Kante.</summary>
    static void ApplyMask(SKCanvas c, JsonObject mk)
    {
        var r = new SKRect((float)ImageDoc.N(mk["x"]), (float)ImageDoc.N(mk["y"]), (float)(ImageDoc.N(mk["x"]) + ImageDoc.N(mk["w"])), (float)(ImageDoc.N(mk["y"]) + ImageDoc.N(mk["h"])));
        var shape = ImageDoc.S(mk["shape"]); var inv = mk["invert"]?.GetValue<bool>() == true; var fe = (float)ImageDoc.N(mk["feather"]);
        if (shape == "bitmap") { BitmapMask(c, mk, r, inv, fe); return; }
        using var path = new SKPath();
        if (shape == "ellipse") path.AddOval(r);
        else if (shape == "poly" && ImageDoc.MaskPoints(mk) is { Count: >= 3 } pts) path.AddPoly([.. pts.Select(q => new SKPoint((float)q.X, (float)q.Y))], true);
        else path.AddRect(r);
        if (!inv) path.FillType = SKPathFillType.InverseWinding;
        using var p = new SKPaint { BlendMode = SKBlendMode.DstOut, IsAntialias = true, Color = SKColors.Black, MaskFilter = fe > 0 ? SKMaskFilter.CreateBlur(SKBlurStyle.Normal, fe / 2) : null };
        c.DrawPath(path, p);
    }

    /// <summary>Zauberstab-Maske: Pixel außerhalb der Auswahl (umgekehrt: innerhalb) werden ausgestanzt, weiche Kante per Unschärfe.</summary>
    static void BitmapMask(SKCanvas c, JsonObject mk, SKRect r, bool inv, float fe)
    {
        var bits = ImageDoc.MaskBits(mk); int w = (int)ImageDoc.N(mk["w"]), h = (int)ImageDoc.N(mk["h"]); if (w < 1 || h < 1) return;
        var pad = (int)Math.Ceiling(fe) + 1; int bw = w + 2 * pad, bh = h + 2 * pad;
        using var a = new SKBitmap(new SKImageInfo(bw, bh, SKColorType.Alpha8, SKAlphaType.Premul));
        var px = new byte[bw * bh]; if (!inv) Array.Fill(px, (byte)255);
        for (var y = 0; y < h; y++) for (var x = 0; x < w; x++) if (bits[y * w + x]) px[(y + pad) * bw + x + pad] = (byte)(inv ? 255 : 0);
        System.Runtime.InteropServices.Marshal.Copy(px, 0, a.GetPixels(), px.Length);
        using var p = new SKPaint { BlendMode = SKBlendMode.DstOut, Color = SKColors.Black, ImageFilter = fe > 0 ? SKImageFilter.CreateBlur(fe / 2, fe / 2) : null };
        if (!inv) // alles außerhalb des Ausschnitts gehört nicht zur Auswahl
        {
            using var outside = new SKPath(); outside.AddRect(new SKRect(r.Left - pad, r.Top - pad, r.Left - pad + bw, r.Top - pad + bh)); outside.FillType = SKPathFillType.InverseWinding;
            using var op = new SKPaint { BlendMode = SKBlendMode.DstOut, Color = SKColors.Black }; c.DrawPath(outside, op);
        }
        c.DrawBitmap(a, r.Left - pad, r.Top - pad, p);
    }

    static byte[] Bytes(SKBitmap b)
    {
        using var u = new SKBitmap(new SKImageInfo(b.Width, b.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        using (var cv = new SKCanvas(u)) { cv.Clear(SKColors.Transparent); cv.DrawBitmap(b, 0, 0); }
        return u.Bytes;
    }

    /// <summary>Füllen, Reparieren und Klonen der Retusche-Ebene auf das bisherige Bild anwenden; nur geänderte Pixel sind sichtbar.</summary>
    static SKBitmap RetouchBitmap(SKCanvas c, SKBitmap below, JsonObject l)
    {
        c.Flush();
        int w = below.Width, h = below.Height; var px = Bytes(below); var changed = new bool[w * h];
        foreach (var o in (l["ops"] as JsonArray ?? []).OfType<JsonObject>())
        {
            var pts = (o["points"] as JsonArray ?? []).Select(n => ImageDoc.N(n)).ToList();
            bool[] mask;
            switch (ImageDoc.S(o["op"]))
            {
                case "fill":
                {
                    mask = ImageOps.FloodMask(px, w, h, (int)ImageDoc.N(o["px"]), (int)ImageDoc.N(o["py"]), ImageDoc.N(o["tolerance"], 32));
                    var (r, g, b, a) = ImageOps.Rgba(ImageDoc.S(o["color"], "#000000"));
                    for (var i = 0; i < mask.Length; i++) if (mask[i]) { px[i * 4] = r; px[i * 4 + 1] = g; px[i * 4 + 2] = b; px[i * 4 + 3] = a; }
                    break;
                }
                case "contentfill" when o["mask"] is JsonObject cm: mask = ImageOps.SelectionMask(cm, w, h); ImageOps.ContentFill(px, w, h, mask); break;
                case "heal": mask = ImageOps.StrokeMask(w, h, pts, ImageDoc.N(o["size"], 20)); ImageOps.Heal(px, w, h, mask); break;
                case "clone": mask = ImageOps.StrokeMask(w, h, pts, ImageDoc.N(o["size"], 20)); ImageOps.Clone(px, (byte[])px.Clone(), w, h, mask, (int)ImageDoc.N(o["dx"]), (int)ImageDoc.N(o["dy"])); break;
                default: continue;
            }
            for (var i = 0; i < mask.Length; i++) changed[i] |= mask[i];
        }
        for (var i = 0; i < changed.Length; i++) if (!changed[i]) px[i * 4 + 3] = 0;
        var outBmp = new SKBitmap(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        System.Runtime.InteropServices.Marshal.Copy(px, 0, outBmp.GetPixels(), px.Length);
        return outBmp;
    }

    /// <summary>Auswahl sichtbar machen: halbdurchsichtiges Blau auf ausgewählten Pixeln (Leinwandgröße).</summary>
    public static byte[] SelectionPng(bool[] bits, int w, int h)
    {
        using var b = new SKBitmap(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        var px = new byte[w * h * 4];
        for (var i = 0; i < bits.Length && i < w * h; i++) if (bits[i]) { px[i * 4] = 47; px[i * 4 + 1] = 134; px[i * 4 + 2] = 246; px[i * 4 + 3] = 90; }
        System.Runtime.InteropServices.Marshal.Copy(px, 0, b.GetPixels(), px.Length);
        using var img = SKImage.FromBitmap(b); using var data = img.Encode(SKEncodedImageFormat.Png, 100); return data.ToArray();
    }

    /// <summary>Gesamtbild als RGBA-Bytes (für den Zauberstab, der auf das sichtbare Bild schaut wie in Photoshop „Alle Ebenen aufnehmen“).</summary>
    public static byte[] CompositeRgba(JsonObject d) { using var b = Render(d); return Bytes(b); }

    /// <summary>Helligkeits-Histogramm (256 Stufen) einer Bilddatei für den Kurven-Editor; null ohne Bild.</summary>
    public static int[]? Histogram(string file)
    {
        if (Source(file) is not { } b) return null;
        var px = Bytes(b); var hist = new int[256]; var step = Math.Max(1, px.Length / 4 / 250_000);
        for (var i = 0; i < px.Length / 4; i += step) hist[(int)Math.Round(0.2126 * px[i * 4] + 0.7152 * px[i * 4 + 1] + 0.0722 * px[i * 4 + 2])]++;
        return hist;
    }

    /// <summary>Auto-Tonwerte für eine Bilddatei (Schwarz- und Weißpunkt).</summary>
    public static (int Black, int White) AutoLevels(string file) => Source(file) is { } b ? ImageOps.AutoLevels(Bytes(b)) : (0, 255);

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
