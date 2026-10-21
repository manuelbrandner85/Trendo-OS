using System.IO.Compression;
using System.Text;

namespace Trendo.Core;

public sealed record Exposure(double MeanPct, double DarkPct, double ClippedPct, string Verdict);
public sealed record MotionResult(double ChangedPct, int MinX, int MinY, int MaxX, int MaxY, bool Moved);
public sealed record MoveProfile(double AccelTime, double CruiseTime, double Total, double PeakSpeed, bool Triangle);

/// <summary>CORE-09 Ausbau: Bildprüfung (Objekte zählen, Belichtung, Schärfe, Bewegung, Maske), Kalibrierung, Greifbahn, Reichweite, Servo, Fahrprofil.</summary>
public static class VisionPlus
{
    public static bool[] Mask(RgbImage img, string color, double minSat = 0.4, double minVal = 0.25)
    {
        if (!Vision.Hues.TryGetValue(color.Trim().Replace("gruen", "grün"), out var hr)) throw new ArgumentException("Farben: " + string.Join(", ", Vision.Hues.Keys));
        var m = new bool[img.Width * img.Height];
        for (var i = 0; i < m.Length; i++) { var (H, S, V) = Vision.Hsv(img.Rgb[i * 3], img.Rgb[i * 3 + 1], img.Rgb[i * 3 + 2]); m[i] = S >= minSat && V >= minVal && (hr.H0 <= hr.H1 ? H >= hr.H0 && H < hr.H1 : H >= hr.H0 || H < hr.H1); }
        return m;
    }

    /// <summary>F11: alle Objekte einer Farbe ab <paramref name="minPixels"/> Pixeln, größtes zuerst.</summary>
    public static List<Blob> FindAll(RgbImage img, string color, int minPixels = 20)
    {
        int w = img.Width, h = img.Height; var mask = Mask(img, color); var seen = new bool[w * h]; var res = new List<Blob>(); var q = new Stack<int>();
        for (var s = 0; s < w * h; s++)
        {
            if (!mask[s] || seen[s]) continue;
            long n = 0, sx = 0, sy = 0; int x0 = w, y0 = h, x1 = 0, y1 = 0; q.Push(s); seen[s] = true;
            while (q.Count > 0)
            {
                var p = q.Pop(); int x = p % w, y = p / w; n++; sx += x; sy += y; x0 = Math.Min(x0, x); x1 = Math.Max(x1, x); y0 = Math.Min(y0, y); y1 = Math.Max(y1, y);
                if (x > 0 && mask[p - 1] && !seen[p - 1]) { seen[p - 1] = true; q.Push(p - 1); }
                if (x < w - 1 && mask[p + 1] && !seen[p + 1]) { seen[p + 1] = true; q.Push(p + 1); }
                if (y > 0 && mask[p - w] && !seen[p - w]) { seen[p - w] = true; q.Push(p - w); }
                if (y < h - 1 && mask[p + w] && !seen[p + w]) { seen[p + w] = true; q.Push(p + w); }
            }
            if (n >= minPixels) res.Add(new((int)n, (double)sx / n, (double)sy / n, x0, y0, x1, y1, Math.Round(100.0 * n / (w * h), 2)));
        }
        return res.OrderByDescending(b => b.Pixels).ToList();
    }

    static double[] Gray(RgbImage img) { var g = new double[img.Width * img.Height]; for (var i = 0; i < g.Length; i++) g[i] = 0.299 * img.Rgb[i * 3] + 0.587 * img.Rgb[i * 3 + 1] + 0.114 * img.Rgb[i * 3 + 2]; return g; }

    /// <summary>F12</summary>
    public static Exposure CheckExposure(RgbImage img)
    {
        var g = Gray(img); var mean = g.Average() / 255 * 100; var dark = 100.0 * g.Count(v => v < 16) / g.Length; var clip = 100.0 * g.Count(v => v > 250) / g.Length;
        var verdict = clip > 5 ? "Überstrahlt – Belichtung verringern oder Licht dämpfen." : dark > 30 || mean < 20 ? "Zu dunkel – mehr Licht oder längere Belichtung." : mean > 85 ? "Sehr hell – etwas weniger Belichtung." : "Belichtung in Ordnung.";
        return new(Math.Round(mean, 1), Math.Round(dark, 1), Math.Round(clip, 1), verdict);
    }

    /// <summary>F13: Varianz des Laplace-Filters (höher = schärfer; unter ca. 100 meist unscharf).</summary>
    public static double Sharpness(RgbImage img)
    {
        int w = img.Width, h = img.Height; if (w < 3 || h < 3) throw new ArgumentException("Das Bild ist zu klein.");
        var g = Gray(img); var l = new List<double>((w - 2) * (h - 2));
        for (var y = 1; y < h - 1; y++) for (var x = 1; x < w - 1; x++) { var i = y * w + x; l.Add(g[i - 1] + g[i + 1] + g[i - w] + g[i + w] - 4 * g[i]); }
        var m = l.Average(); return Math.Round(l.Sum(v => (v - m) * (v - m)) / l.Count, 2);
    }

    /// <summary>F14: Anteil geänderter Pixel (Grauwert-Differenz &gt; <paramref name="threshold"/>) und Rahmen der Änderung.</summary>
    public static MotionResult Motion(RgbImage a, RgbImage b, double threshold = 30, double minPct = 0.5)
    {
        if (a.Width != b.Width || a.Height != b.Height) throw new ArgumentException("Beide Bilder müssen gleich groß sein.");
        var ga = Gray(a); var gb = Gray(b); int n = 0, x0 = a.Width, y0 = a.Height, x1 = -1, y1 = -1;
        for (var i = 0; i < ga.Length; i++) if (Math.Abs(ga[i] - gb[i]) > threshold) { n++; int x = i % a.Width, y = i / a.Width; x0 = Math.Min(x0, x); y0 = Math.Min(y0, y); x1 = Math.Max(x1, x); y1 = Math.Max(y1, y); }
        var pct = Math.Round(100.0 * n / ga.Length, 2); return n == 0 ? new(0, 0, 0, 0, 0, false) : new(pct, x0, y0, x1, y1, pct >= minPct);
    }

    static readonly uint[] CrcTable = Enumerable.Range(0, 256).Select(n => { var c = (uint)n; for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1; return c; }).ToArray();
    static uint Crc(byte[] d, int off, int len) { var c = 0xFFFFFFFF; for (var i = off; i < off + len; i++) c = CrcTable[(c ^ d[i]) & 0xFF] ^ (c >> 8); return c ^ 0xFFFFFFFF; }
    static void Chunk(Stream s, string type, byte[] data)
    {
        var buf = new byte[data.Length + 4]; Encoding.ASCII.GetBytes(type).CopyTo(buf, 0); data.CopyTo(buf, 4);
        void U32(uint v) { s.WriteByte((byte)(v >> 24)); s.WriteByte((byte)(v >> 16)); s.WriteByte((byte)(v >> 8)); s.WriteByte((byte)v); }
        U32((uint)data.Length); s.Write(buf); U32(Crc(buf, 0, buf.Length));
    }

    /// <summary>F15: RGB-Bild als PNG (8 Bit, Filter 0) speichern.</summary>
    public static byte[] EncodePng(RgbImage img)
    {
        using var ms = new MemoryStream(); ms.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        var ihdr = new byte[13]; void Be(int o, int v) { ihdr[o] = (byte)(v >> 24); ihdr[o + 1] = (byte)(v >> 16); ihdr[o + 2] = (byte)(v >> 8); ihdr[o + 3] = (byte)v; }
        Be(0, img.Width); Be(4, img.Height); ihdr[8] = 8; ihdr[9] = 2; Chunk(ms, "IHDR", ihdr);
        using var raw = new MemoryStream(); using (var z = new ZLibStream(raw, CompressionLevel.Optimal, true)) for (var y = 0; y < img.Height; y++) { z.WriteByte(0); z.Write(img.Rgb, y * img.Width * 3, img.Width * 3); }
        Chunk(ms, "IDAT", raw.ToArray()); Chunk(ms, "IEND", []); return ms.ToArray();
    }

    /// <summary>Maske einer Farbe als Schwarz-Weiß-Bild (weiß = erkannt).</summary>
    public static RgbImage MaskImage(RgbImage img, string color) { var m = Mask(img, color); var rgb = new byte[m.Length * 3]; for (var i = 0; i < m.Length; i++) if (m[i]) rgb[i * 3] = rgb[i * 3 + 1] = rgb[i * 3 + 2] = 255; return new(img.Width, img.Height, rgb); }

    /// <summary>F16: Homographie aus 4 Punktpaaren (Bild → Tisch). Ergebnis als 3×3-Matrix (h33 = 1).</summary>
    public static double[] Homography((double X, double Y)[] src, (double X, double Y)[] dst)
    {
        if (src.Length != 4 || dst.Length != 4) throw new ArgumentException("Genau 4 Punktpaare nötig.");
        var a = new double[8, 9];
        for (var i = 0; i < 4; i++)
        {
            var (x, y) = src[i]; var (u, v) = dst[i];
            double[] r1 = [x, y, 1, 0, 0, 0, -u * x, -u * y, u], r2 = [0, 0, 0, x, y, 1, -v * x, -v * y, v];
            for (var k = 0; k < 9; k++) { a[2 * i, k] = r1[k]; a[2 * i + 1, k] = r2[k]; }
        }
        for (var c = 0; c < 8; c++)
        {
            var p = c; for (var r = c + 1; r < 8; r++) if (Math.Abs(a[r, c]) > Math.Abs(a[p, c])) p = r;
            if (Math.Abs(a[p, c]) < 1e-12) throw new ArgumentException("Die Punkte liegen auf einer Linie – bitte 4 Ecken wählen.");
            for (var k = 0; k < 9; k++) (a[c, k], a[p, k]) = (a[p, k], a[c, k]);
            for (var r = 0; r < 8; r++) { if (r == c) continue; var f = a[r, c] / a[c, c]; for (var k = c; k < 9; k++) a[r, k] -= f * a[c, k]; }
        }
        var h = new double[9]; for (var i = 0; i < 8; i++) h[i] = a[i, 8] / a[i, i]; h[8] = 1; return h;
    }
    public static (double X, double Y) Apply(double[] h, double x, double y) { var w = h[6] * x + h[7] * y + h[8]; return (Math.Round((h[0] * x + h[1] * y + h[2]) / w, 3), Math.Round((h[3] * x + h[4] * y + h[5]) / w, 3)); }
}

public static class ArmPlus
{
    /// <summary>F17: Pick & Place – anfahren über dem Teil, absenken, greifen, anheben, rüber, absenken, loslassen, anheben.</summary>
    public static List<(double X, double Y, double Z, string Action)> PickPlace((double X, double Y, double Z) pick, (double X, double Y, double Z) place, double lift = 50)
    {
        if (lift <= 0) throw new ArgumentException("Die Anfahrhöhe muss größer als 0 sein.");
        return [(pick.X, pick.Y, pick.Z + lift, "anfahren"), (pick.X, pick.Y, pick.Z, "greifen"), (pick.X, pick.Y, pick.Z + lift, "anheben"),
                (place.X, place.Y, place.Z + lift, "rüberfahren"), (place.X, place.Y, place.Z, "loslassen"), (place.X, place.Y, place.Z + lift, "zurückziehen")];
    }
    public static string ProgramText(IEnumerable<(double X, double Y, double Z, string Action)> p) => string.Join("\n", p.Select(s => FormattableString.Invariant($"{s.X:0.###} {s.Y:0.###} {s.Z:0.###}")));

    /// <summary>F18: Reichweiten-Karte in Höhe z (Raster in mm, # = erreichbar, Blick von oben, +x nach rechts, +y nach oben).</summary>
    public static (string Map, double ReachablePct, double MaxRadius) Reach(ArmConfig c, double z, double step = 20)
    {
        if (step <= 0) throw new ArgumentException("Rastergröße > 0.");
        var r = c.L1 + c.L2; var n = (int)Math.Ceiling(r / step); var sb = new StringBuilder(); int ok = 0, all = 0; var maxR = 0.0;
        for (var j = n; j >= -n; j--)
        {
            for (var i = -n; i <= n; i++)
            {
                double x = i * step, y = j * step; all++; var hit = true; try { RobotArm.Inverse(c, x, y, z); } catch (ArgumentException) { hit = false; }
                if (hit) { ok++; maxR = Math.Max(maxR, Math.Sqrt(x * x + y * y)); }
                sb.Append(i == 0 && j == 0 ? 'O' : hit ? '#' : '·');
            }
            sb.Append('\n');
        }
        return (sb.ToString(), Math.Round(100.0 * ok / all, 1), maxR);
    }

    /// <summary>F19: Servo-Winkel → Pulsbreite (Standard 500–2500 µs für 0–180°).</summary>
    public static int ServoPulse(double angle, double minAngle = 0, double maxAngle = 180, int minUs = 500, int maxUs = 2500)
    {
        if (maxAngle <= minAngle || maxUs <= minUs) throw new ArgumentException("Bereiche ungültig.");
        if (angle < minAngle || angle > maxAngle) throw new ArgumentException($"Winkel {angle:0.#}° liegt außerhalb {minAngle:0}–{maxAngle:0}°.");
        return (int)Math.Round(minUs + (angle - minAngle) / (maxAngle - minAngle) * (maxUs - minUs));
    }

    /// <summary>F20: Trapez-Fahrprofil (Weg, Höchstgeschwindigkeit, Beschleunigung). Wird die Höchstgeschwindigkeit nicht erreicht: Dreieck.</summary>
    public static MoveProfile Profile(double distance, double vmax, double accel)
    {
        if (distance < 0 || vmax <= 0 || accel <= 0) throw new ArgumentException("Weg ≥ 0, Geschwindigkeit und Beschleunigung > 0.");
        var ta = vmax / accel; var da = accel * ta * ta / 2;
        if (2 * da >= distance) { var t = Math.Sqrt(distance / accel); return new(Math.Round(t, 4), 0, Math.Round(2 * t, 4), Math.Round(accel * t, 4), true); }
        var tc = (distance - 2 * da) / vmax; return new(Math.Round(ta, 4), Math.Round(tc, 4), Math.Round(2 * ta + tc, 4), vmax, false);
    }
}
