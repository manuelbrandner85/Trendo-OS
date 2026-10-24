using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Media.Imaging;
using SkiaSharp;

namespace Trendo.App;

/// <summary>Design 41 Glas: Wallpaper wird einmal verkleinert weichgezeichnet statt live per BlurEffect (GPU-Last). Ob Glas gezeigt wird, entscheidet das Leistungsprofil (MainWindow.LowPower).</summary>
public static class GlassBlur
{
    /// <summary>Breite der vorab berechneten Bitmap; wird per UniformToFill hochskaliert (Unschärfe verdeckt das).</summary>
    public const int TargetWidth = 480;
    /// <summary>Entspricht BlurEffect Radius 36 (Sigma ≈ 0,577·r+0,5) bei ~1440 px Breite, umgerechnet auf TargetWidth.</summary>
    public const float SigmaAt1440 = 21.3f;
    private static readonly Dictionary<string, Bitmap?> Cache = [];

    public static Bitmap? Get(string key, Func<Stream?> open)
    {
        lock (Cache)
        {
            if (Cache.TryGetValue(key, out var b)) return b;
            Bitmap? r = null;
            try { using var st = open(); if (st is not null) r = Blur(st); } catch { r = null; }
            return Cache[key] = r;
        }
    }

    public static int CachedCount { get { lock (Cache) return Cache.Count; } }

    public static Bitmap Blur(Stream encoded) { using var ms = new MemoryStream(BlurToPng(encoded)); return new Bitmap(ms); }

    /// <summary>Verkleinert auf höchstens TargetWidth und zeichnet weich; Ergebnis als PNG.</summary>
    public static byte[] BlurToPng(Stream encoded)
    {
        using var full = SKBitmap.Decode(encoded) ?? throw new InvalidDataException("Wallpaper nicht lesbar");
        var w = Math.Min(TargetWidth, full.Width); var h = Math.Max(1, (int)Math.Round(full.Height * (double)w / full.Width));
        var sigma = SigmaAt1440 * w / 1440f;
        using var surface = SKSurface.Create(new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Premul));
        using (var paint = new SKPaint { ImageFilter = SKImageFilter.CreateBlur(sigma, sigma, SKShaderTileMode.Clamp) })
        using (var img = SKImage.FromBitmap(full))
            surface.Canvas.DrawImage(img, new SKRect(0, 0, w, h), new SKSamplingOptions(SKFilterMode.Linear), paint);
        using var snap = surface.Snapshot(); using var data = snap.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
