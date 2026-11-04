using System.Text.Json.Nodes;

namespace Trendo.Core.Media;

/// <summary>Bildbearbeitung wie Photoshop/GIMP/Krita (selbst nachgebaut, 04.11.2026): Tonwerte und Kurven als Farbtabelle,
/// Auto-Tonwerte, Füllen (Farbeimer mit Toleranz), Reparieren (Fleck aus der Umgebung auffüllen), Klonen, Pinselmaske.
/// Arbeitet auf RGBA-Bytes (nicht vormultipliziert), damit es ohne Skia testbar ist.</summary>
public static class ImageOps
{
    /// <summary>Tonwerte (Schwarzpunkt, Weißpunkt, Gamma) und Kurve (Punkte x,y 0–255) als Tabelle 0–255.</summary>
    public static byte[] Lut(JsonObject? f)
    {
        double V(string k, double def) => f is null || f[k] is null ? def : ImageDoc.N(f[k], def);
        double b = Math.Clamp(V("black", 0), 0, 254), w = Math.Clamp(V("white", 255), b + 1, 255), g = Math.Clamp(V("gamma", 1), 0.1, 5);
        var curve = Curve(f?["curve"] as JsonArray);
        var t = new byte[256];
        for (var i = 0; i < 256; i++)
        {
            var x = Math.Clamp((i - b) / (w - b), 0, 1);
            x = Math.Pow(x, 1 / g) * 255;
            t[i] = (byte)Math.Round(Math.Clamp(curve is null ? x : curve(x), 0, 255));
        }
        return t;
    }

    /// <summary>true, wenn Tonwerte oder Kurve etwas ändern.</summary>
    public static bool HasLut(JsonObject? f)
    {
        var t = Lut(f); for (var i = 0; i < 256; i++) if (t[i] != i) return true; return false;
    }

    /// <summary>Monotone Kurve durch die Punkte (Fritsch–Carlson), damit keine Überschwinger entstehen wie in Photoshop-Kurven.</summary>
    static Func<double, double>? Curve(JsonArray? a)
    {
        if (a is null || a.Count < 4) return null;
        var pts = new List<(double X, double Y)>();
        for (var i = 0; i + 1 < a.Count; i += 2) pts.Add((Math.Clamp(ImageDoc.N(a[i]), 0, 255), Math.Clamp(ImageDoc.N(a[i + 1]), 0, 255)));
        pts = [.. pts.GroupBy(p => p.X).Select(g => g.First()).OrderBy(p => p.X)];
        if (pts.Count < 2) return null;
        var n = pts.Count; var d = new double[n - 1]; var m = new double[n];
        for (var i = 0; i < n - 1; i++) d[i] = (pts[i + 1].Y - pts[i].Y) / (pts[i + 1].X - pts[i].X);
        m[0] = d[0]; m[n - 1] = d[n - 2];
        for (var i = 1; i < n - 1; i++) m[i] = d[i - 1] * d[i] <= 0 ? 0 : (d[i - 1] + d[i]) / 2;
        for (var i = 0; i < n - 1; i++)
        {
            if (d[i] == 0) { m[i] = 0; m[i + 1] = 0; continue; }
            double al = m[i] / d[i], be = m[i + 1] / d[i], s = al * al + be * be;
            if (s > 9) { var k = 3 / Math.Sqrt(s); m[i] = k * al * d[i]; m[i + 1] = k * be * d[i]; }
        }
        return x =>
        {
            if (x <= pts[0].X) return pts[0].Y; if (x >= pts[^1].X) return pts[^1].Y;
            var i = 0; while (x > pts[i + 1].X) i++;
            double h = pts[i + 1].X - pts[i].X, t = (x - pts[i].X) / h, t2 = t * t, t3 = t2 * t;
            return (2 * t3 - 3 * t2 + 1) * pts[i].Y + (t3 - 2 * t2 + t) * h * m[i] + (-2 * t3 + 3 * t2) * pts[i + 1].Y + (t3 - t2) * h * m[i + 1];
        };
    }

    /// <summary>Fertige Kurven wie in den Vorbildern.</summary>
    public static readonly (string Id, string Label, double[] Points)[] CurvePresets =
    [
        ("none", "Keine Kurve", []),
        ("s", "S-Kurve (mehr Kontrast)", [0, 0, 64, 48, 192, 208, 255, 255]),
        ("hell", "Aufhellen", [0, 0, 128, 168, 255, 255]),
        ("dunkel", "Abdunkeln", [0, 0, 128, 92, 255, 255]),
        ("matt", "Matt (Film-Look)", [0, 28, 128, 128, 255, 236]),
    ];

    /// <summary>Auto-Tonwerte wie Photoshop „Auto-Tonwert“: 0,5 % der dunkelsten und hellsten Pixel werden Schwarz und Weiß.</summary>
    public static (int Black, int White) AutoLevels(byte[] rgba, double clip = 0.005)
    {
        var hist = new long[256]; long n = 0;
        for (var i = 0; i + 3 < rgba.Length; i += 4)
        {
            if (rgba[i + 3] < 8) continue;
            hist[(int)Math.Round(0.2126 * rgba[i] + 0.7152 * rgba[i + 1] + 0.0722 * rgba[i + 2])]++; n++;
        }
        if (n == 0) return (0, 255);
        var lim = (long)(n * clip); int lo = 0, hi = 255; long acc = 0;
        while (lo < 254 && (acc += hist[lo]) <= lim) lo++;
        acc = 0; while (hi > lo + 1 && (acc += hist[hi]) <= lim) hi--;
        return (lo, Math.Max(lo + 1, hi));
    }

    /// <summary>Farbeimer: zusammenhängende Fläche ab (x, y), deren Farbe höchstens <paramref name="tolerance"/> (0–255 je Kanal) abweicht.</summary>
    public static bool[] FloodMask(byte[] rgba, int w, int h, int x, int y, double tolerance)
    {
        var mask = new bool[w * h];
        if (x < 0 || y < 0 || x >= w || y >= h) return mask;
        var s = (y * w + x) * 4; var tol = Math.Clamp(tolerance, 0, 255);
        bool Near(int p) { var i = p * 4; return Math.Abs(rgba[i] - rgba[s]) <= tol && Math.Abs(rgba[i + 1] - rgba[s + 1]) <= tol && Math.Abs(rgba[i + 2] - rgba[s + 2]) <= tol && Math.Abs(rgba[i + 3] - rgba[s + 3]) <= tol; }
        var stack = new Stack<(int X, int Y)>(); stack.Push((x, y));
        while (stack.Count > 0)
        {
            var (px, py) = stack.Pop();
            if (mask[py * w + px] || !Near(py * w + px)) continue;
            var l = px; while (l > 0 && !mask[py * w + l - 1] && Near(py * w + l - 1)) l--;
            var r = px; while (r < w - 1 && !mask[py * w + r + 1] && Near(py * w + r + 1)) r++;
            for (var i = l; i <= r; i++) mask[py * w + i] = true;
            foreach (var ny in new[] { py - 1, py + 1 })
            {
                if (ny < 0 || ny >= h) continue;
                for (var i = l; i <= r; i++) if (!mask[ny * w + i] && Near(ny * w + i)) { stack.Push((i, ny)); while (i < r && !mask[ny * w + i + 1] && Near(ny * w + i + 1)) i++; }
            }
        }
        return mask;
    }

    /// <summary>Pinselmaske: Kreise mit Durchmesser <paramref name="size"/> entlang der Punkte (x,y,x,y …).</summary>
    public static bool[] StrokeMask(int w, int h, IReadOnlyList<double> pts, double size)
    {
        var mask = new bool[w * h]; var r = Math.Max(0.5, size / 2); var r2 = r * r;
        void Dot(double cx, double cy)
        {
            for (var yy = Math.Max(0, (int)(cy - r)); yy <= Math.Min(h - 1, (int)(cy + r)); yy++)
                for (var xx = Math.Max(0, (int)(cx - r)); xx <= Math.Min(w - 1, (int)(cx + r)); xx++)
                    if ((xx + 0.5 - cx) * (xx + 0.5 - cx) + (yy + 0.5 - cy) * (yy + 0.5 - cy) <= r2) mask[yy * w + xx] = true;
        }
        if (pts.Count < 2) return mask;
        Dot(pts[0], pts[1]);
        for (var i = 2; i + 1 < pts.Count; i += 2)
        {
            double x0 = pts[i - 2], y0 = pts[i - 1], x1 = pts[i], y1 = pts[i + 1]; var steps = Math.Max(1, (int)Math.Ceiling(Math.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0)) / Math.Max(0.5, r / 3)));
            for (var k = 1; k <= steps; k++) Dot(x0 + (x1 - x0) * k / steps, y0 + (y1 - y0) * k / steps);
        }
        return mask;
    }

    /// <summary>Reparieren (Bereichsreparatur): maskierte Pixel aus dem Rand nach innen glatt auffüllen (Laplace-Ausgleich) –
    /// Fleck, Kratzer oder Staub verschwinden, Farbverläufe der Umgebung laufen durch.</summary>
    public static void Heal(byte[] rgba, int w, int h, bool[] mask)
    {
        int x0 = w, y0 = h, x1 = -1, y1 = -1;
        for (var y = 0; y < h; y++) for (var x = 0; x < w; x++) if (mask[y * w + x]) { x0 = Math.Min(x0, x); x1 = Math.Max(x1, x); y0 = Math.Min(y0, y); y1 = Math.Max(y1, y); }
        if (x1 < 0) return;
        var idx = new List<int>();
        for (var y = y0; y <= y1; y++) for (var x = x0; x <= x1; x++) if (mask[y * w + x]) idx.Add(y * w + x);
        var v = new double[4][]; for (var c = 0; c < 4; c++) v[c] = new double[w * h];
        // Start: Mittelwert des Randes
        double[] mean = new double[4]; var nb = 0;
        for (var y = Math.Max(0, y0 - 1); y <= Math.Min(h - 1, y1 + 1); y++) for (var x = Math.Max(0, x0 - 1); x <= Math.Min(w - 1, x1 + 1); x++)
            if (!mask[y * w + x]) { for (var c = 0; c < 4; c++) mean[c] += rgba[(y * w + x) * 4 + c]; nb++; }
        for (var y = Math.Max(0, y0 - 1); y <= Math.Min(h - 1, y1 + 1); y++) for (var x = Math.Max(0, x0 - 1); x <= Math.Min(w - 1, x1 + 1); x++)
            for (var c = 0; c < 4; c++) v[c][y * w + x] = mask[y * w + x] ? (nb > 0 ? mean[c] / nb : 0) : rgba[(y * w + x) * 4 + c];
        var iters = Math.Clamp(Math.Max(x1 - x0, y1 - y0) * 6, 50, 1500);
        for (var it = 0; it < iters; it++)
            foreach (var p in idx)
            {
                int x = p % w, y = p / w;
                for (var c = 0; c < 4; c++)
                {
                    double s = 0; var k = 0;
                    if (x > 0) { s += v[c][p - 1]; k++; } if (x < w - 1) { s += v[c][p + 1]; k++; }
                    if (y > 0) { s += v[c][p - w]; k++; } if (y < h - 1) { s += v[c][p + w]; k++; }
                    v[c][p] = s / k;
                }
            }
        foreach (var p in idx) for (var c = 0; c < 4; c++) rgba[p * 4 + c] = (byte)Math.Round(Math.Clamp(v[c][p], 0, 255));
    }

    /// <summary>Kopierstempel: maskierte Pixel aus der um (dx, dy) versetzten Stelle von <paramref name="source"/> übernehmen.</summary>
    public static void Clone(byte[] rgba, byte[] source, int w, int h, bool[] mask, int dx, int dy)
    {
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                if (!mask[y * w + x]) continue;
                int sx = Math.Clamp(x + dx, 0, w - 1), sy = Math.Clamp(y + dy, 0, h - 1);
                Buffer.BlockCopy(source, (sy * w + sx) * 4, rgba, (y * w + x) * 4, 4);
            }
    }

    /// <summary>Farbe als RGBA-Bytes (#RRGGBB oder #AARRGGBB).</summary>
    public static (byte R, byte G, byte B, byte A) Rgba(string hex)
    {
        var s = (hex ?? "").TrimStart('#');
        try
        {
            return s.Length switch
            {
                6 => (Convert.ToByte(s[..2], 16), Convert.ToByte(s[2..4], 16), Convert.ToByte(s[4..6], 16), (byte)255),
                8 => (Convert.ToByte(s[2..4], 16), Convert.ToByte(s[4..6], 16), Convert.ToByte(s[6..8], 16), Convert.ToByte(s[..2], 16)),
                _ => (0, 0, 0, 255),
            };
        }
        catch (FormatException) { return (0, 0, 0, 255); }
    }
}
