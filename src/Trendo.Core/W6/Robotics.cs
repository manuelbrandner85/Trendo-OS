using System.IO.Compression;
using System.Text;

namespace Trendo.Core;

public sealed record RgbImage(int Width, int Height, byte[] Rgb);
public sealed record Blob(int Pixels, double CenterX, double CenterY, int MinX, int MinY, int MaxX, int MaxY, double SharePct);
public sealed record ArmConfig(double BaseHeight = 60, double L1 = 120, double L2 = 120, double MinShoulder = -10, double MaxShoulder = 190, double MinElbow = -150, double MaxElbow = 150, double MaxDegPerSec = 90);
public sealed record Joints(double Base, double Shoulder, double Elbow);
public sealed record ProgramStep(int Index, double X, double Y, double Z, Joints J, double Seconds);

/// <summary>CORE-09: Sehen & Roboter – Bilder lesen (PNG/BMP), Farbobjekte finden, Roboterarm (3 Achsen) rechnen und Programme erzeugen.</summary>
public static class Vision
{
    public static RgbImage Load(string path)
    {
        if (!File.Exists(path)) throw new IOException("Diese Datei gibt es nicht.");
        var b = File.ReadAllBytes(path);
        if (b.Length > 8 && b[0] == 0x89 && b[1] == (byte)'P') return Png(b);
        if (b.Length > 54 && b[0] == (byte)'B' && b[1] == (byte)'M') return Bmp(b);
        throw new InvalidOperationException("Unterstützt werden PNG und BMP (z. B. Kamerabild als PNG speichern).");
    }

    private static int Be(byte[] b, int o) => b[o] << 24 | b[o + 1] << 16 | b[o + 2] << 8 | b[o + 3];

    private static RgbImage Png(byte[] b)
    {
        int w = 0, h = 0, depth = 0, ct = 0, inter = 0; byte[]? pal = null; var idat = new MemoryStream(); var o = 8;
        while (o + 8 <= b.Length)
        {
            var len = Be(b, o); var type = Encoding.ASCII.GetString(b, o + 4, 4); var d = o + 8;
            if (len < 0 || d + len > b.Length) throw new InvalidOperationException("Die PNG-Datei ist beschädigt.");
            if (type == "IHDR") { w = Be(b, d); h = Be(b, d + 4); depth = b[d + 8]; ct = b[d + 9]; inter = b[d + 12]; }
            else if (type == "PLTE") pal = b[d..(d + len)];
            else if (type == "IDAT") idat.Write(b, d, len);
            else if (type == "IEND") break;
            o = d + len + 4;
        }
        if (depth != 8 || inter != 0 || ct is not (0 or 2 or 3 or 4 or 6)) throw new InvalidOperationException("Nur PNG mit 8 Bit pro Kanal ohne Interlacing wird unterstützt.");
        if (w <= 0 || h <= 0 || (long)w * h > 50_000_000) throw new InvalidOperationException("Bildgröße ungültig oder zu groß.");
        var ch = ct switch { 0 => 1, 2 => 3, 3 => 1, 4 => 2, _ => 4 }; var stride = w * ch;
        idat.Position = 0; using var z = new ZLibStream(idat, CompressionMode.Decompress); var raw = new MemoryStream(); z.CopyTo(raw); var data = raw.ToArray();
        if (data.Length < (stride + 1) * h) throw new InvalidOperationException("Die PNG-Daten sind unvollständig.");
        var cur = new byte[stride]; var prev = new byte[stride]; var rgb = new byte[w * h * 3];
        for (var y = 0; y < h; y++)
        {
            var f = data[y * (stride + 1)]; Array.Copy(data, y * (stride + 1) + 1, cur, 0, stride);
            for (var i = 0; i < stride; i++)
            {
                int a = i >= ch ? cur[i - ch] : 0, up = prev[i], c = i >= ch ? prev[i - ch] : 0;
                cur[i] = (byte)(cur[i] + f switch { 1 => a, 2 => up, 3 => (a + up) / 2, 4 => Paeth(a, up, c), _ => 0 });
            }
            for (var x = 0; x < w; x++)
            {
                var p = (y * w + x) * 3; var s = x * ch;
                switch (ct)
                {
                    case 0: case 4: rgb[p] = rgb[p + 1] = rgb[p + 2] = cur[s]; break;
                    case 3: var k = cur[s] * 3; if (pal is null || k + 2 >= pal.Length) throw new InvalidOperationException("PNG-Palette fehlt."); rgb[p] = pal[k]; rgb[p + 1] = pal[k + 1]; rgb[p + 2] = pal[k + 2]; break;
                    default: rgb[p] = cur[s]; rgb[p + 1] = cur[s + 1]; rgb[p + 2] = cur[s + 2]; break;
                }
            }
            (cur, prev) = (prev, cur);
        }
        return new(w, h, rgb);
    }

    private static int Paeth(int a, int b, int c) { var p = a + b - c; int pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c); return pa <= pb && pa <= pc ? a : pb <= pc ? b : c; }

    private static RgbImage Bmp(byte[] b)
    {
        var off = BitConverter.ToInt32(b, 10); var w = BitConverter.ToInt32(b, 18); var hRaw = BitConverter.ToInt32(b, 22); var bpp = BitConverter.ToInt16(b, 28); var comp = BitConverter.ToInt32(b, 30);
        if (bpp is not (24 or 32) || comp is not (0 or 3)) throw new InvalidOperationException("Nur unkomprimierte BMP mit 24 oder 32 Bit.");
        var h = Math.Abs(hRaw); var bytes = bpp / 8; var stride = (w * bytes + 3) & ~3; var rgb = new byte[w * h * 3];
        if (off + (long)stride * h > b.Length) throw new InvalidOperationException("Die BMP-Datei ist unvollständig.");
        for (var y = 0; y < h; y++) { var row = off + (hRaw > 0 ? h - 1 - y : y) * stride; for (var x = 0; x < w; x++) { var s = row + x * bytes; var p = (y * w + x) * 3; rgb[p] = b[s + 2]; rgb[p + 1] = b[s + 1]; rgb[p + 2] = b[s]; } }
        return new(w, h, rgb);
    }

    public static (double H, double S, double V) Hsv(byte r, byte g, byte b)
    {
        double R = r / 255.0, G = g / 255.0, B = b / 255.0, max = Math.Max(R, Math.Max(G, B)), min = Math.Min(R, Math.Min(G, B)), d = max - min, h = 0;
        if (d > 0) h = max == R ? 60 * ((G - B) / d % 6) : max == G ? 60 * ((B - R) / d + 2) : 60 * ((R - G) / d + 4);
        return (h < 0 ? h + 360 : h, max == 0 ? 0 : d / max, max);
    }

    public static readonly IReadOnlyDictionary<string, (double H0, double H1)> Hues = new Dictionary<string, (double, double)>(StringComparer.OrdinalIgnoreCase)
    { ["rot"] = (340, 20), ["orange"] = (20, 40), ["gelb"] = (40, 70), ["grün"] = (70, 170), ["blau"] = (190, 260), ["lila"] = (260, 320) };

    /// <summary>Größtes zusammenhängendes Objekt einer Farbe (4er-Nachbarschaft), mit Mittelpunkt und Rahmen.</summary>
    public static Blob? Find(RgbImage img, string color, double minSat = 0.4, double minVal = 0.25)
    {
        if (!Hues.TryGetValue(color.Trim().Replace("gruen", "grün"), out var hr)) throw new ArgumentException("Farben: " + string.Join(", ", Hues.Keys));
        int w = img.Width, h = img.Height; var mask = new bool[w * h];
        for (var i = 0; i < w * h; i++) { var (H, S, V) = Hsv(img.Rgb[i * 3], img.Rgb[i * 3 + 1], img.Rgb[i * 3 + 2]); mask[i] = S >= minSat && V >= minVal && (hr.H0 <= hr.H1 ? H >= hr.H0 && H < hr.H1 : H >= hr.H0 || H < hr.H1); }
        var seen = new bool[w * h]; Blob? best = null; var q = new Stack<int>();
        for (var s = 0; s < w * h; s++)
        {
            if (!mask[s] || seen[s]) continue;
            long n = 0, sx = 0, sy = 0; int x0 = w, y0 = h, x1 = 0, y1 = 0; q.Push(s); seen[s] = true;
            while (q.Count > 0)
            {
                var p = q.Pop(); int x = p % w, y = p / w; n++; sx += x; sy += y; x0 = Math.Min(x0, x); x1 = Math.Max(x1, x); y0 = Math.Min(y0, y); y1 = Math.Max(y1, y);
                foreach (var nb in new[] { x > 0 ? p - 1 : -1, x < w - 1 ? p + 1 : -1, y > 0 ? p - w : -1, y < h - 1 ? p + w : -1 }) if (nb >= 0 && mask[nb] && !seen[nb]) { seen[nb] = true; q.Push(nb); }
            }
            if (best is null || n > best.Pixels) best = new((int)n, (double)sx / n, (double)sy / n, x0, y0, x1, y1, Math.Round(100.0 * n / (w * h), 2));
        }
        return best;
    }

    /// <summary>Bildpunkt → Tischkoordinate (mm) über zwei bekannte Ecken (Kamera senkrecht von oben).</summary>
    /// <summary>Pixel → Tisch-mm. Annahme (Kalibrierung): das Bild zeigt genau die Tischfläche, Kamera senkrecht. T-0251 (V109): Ohne
    /// gültige Kalibrierung keine mm-Werte – Maße müssen positiv sein und das Seitenverhältnis von Bild und Tisch darf höchstens 10 % abweichen.</summary>
    public static (double X, double Y) PixelToTable(double px, double py, int w, int h, double tableW, double tableH)
    {
        if (CalibrationProblem(w, h, tableW, tableH) is { } why) throw new ArgumentException(why);
        return (Math.Round(px / w * tableW, 1), Math.Round((1 - py / h) * tableH, 1));
    }

    /// <summary>Grund, warum die Kalibrierung nicht passt – oder null.</summary>
    public static string? CalibrationProblem(int w, int h, double tableW, double tableH)
    {
        if (w <= 0 || h <= 0) return "Das Bild hat keine gültige Größe.";
        if (tableW <= 0 || tableH <= 0) return "Bitte die Tischmaße (mm) größer als 0 angeben – ohne Kalibrierung gibt es keine mm-Werte.";
        var ri = (double)w / h; var rt = tableW / tableH;
        return Math.Abs(ri - rt) / rt > 0.10
            ? $"Kalibrierung passt nicht: Bild {w} × {h} Pixel (Seitenverhältnis {ri:0.00}), Tisch {tableW:0} × {tableH:0} mm ({rt:0.00}). Zeigt das Bild genau die Tischfläche, von oben?"
            : null;
    }
}

public static class RobotArm
{
    private const double Deg = 180 / Math.PI;

    public static (double X, double Y, double Z) Forward(ArmConfig c, Joints j)
    {
        double s = j.Shoulder / Deg, e = (j.Shoulder + j.Elbow) / Deg, r = c.L1 * Math.Cos(s) + c.L2 * Math.Cos(e), z = c.BaseHeight + c.L1 * Math.Sin(s) + c.L2 * Math.Sin(e);
        return (r * Math.Cos(j.Base / Deg), r * Math.Sin(j.Base / Deg), z);
    }

    /// <summary>Inverse Kinematik (Ellbogen oben). Wirft, wenn der Punkt nicht erreichbar ist oder Achsgrenzen verletzt.</summary>
    public static Joints Inverse(ArmConfig c, double x, double y, double z)
    {
        var r = Math.Sqrt(x * x + y * y); var h = z - c.BaseHeight; var d2 = r * r + h * h;
        var cosE = (d2 - c.L1 * c.L1 - c.L2 * c.L2) / (2 * c.L1 * c.L2);
        if (cosE > 1 + 1e-9 || cosE < -1 - 1e-9) throw new ArgumentException($"Punkt ({x:0}, {y:0}, {z:0}) ist außer Reichweite (max. {c.L1 + c.L2:0} mm vom Schultergelenk).");
        var e = -Math.Acos(Math.Clamp(cosE, -1, 1));
        var s = Math.Atan2(h, r) - Math.Atan2(c.L2 * Math.Sin(e), c.L1 + c.L2 * Math.Cos(e));
        var j = new Joints(Math.Round(Math.Atan2(y, x) * Deg, 3), Math.Round(s * Deg, 3), Math.Round(e * Deg, 3));
        if (j.Shoulder < c.MinShoulder || j.Shoulder > c.MaxShoulder) throw new ArgumentException($"Schulter müsste auf {j.Shoulder:0}° – erlaubt {c.MinShoulder:0}° bis {c.MaxShoulder:0}°.");
        if (j.Elbow < c.MinElbow || j.Elbow > c.MaxElbow) throw new ArgumentException($"Ellbogen müsste auf {j.Elbow:0}° – erlaubt {c.MinElbow:0}° bis {c.MaxElbow:0}°.");
        return j;
    }

    /// <summary>Programm aus Zeilen „x y z“ (mm). Zeit je Schritt nach der langsamsten Achse.</summary>
    public static IReadOnlyList<ProgramStep> Program(ArmConfig c, string text)
    {
        var res = new List<ProgramStep>(); Joints? last = null; var n = 0;
        foreach (var raw in text.Replace("\r", "").Split('\n'))
        {
            n++; var l = raw.Trim(); if (l == "" || l.StartsWith('#')) continue;
            var p = l.Split([' ', ';', '\t'], StringSplitOptions.RemoveEmptyEntries);
            if (p.Length < 3) throw new FormatException($"Zeile {n}: bitte „x y z“ in mm.");
            double x = Fach.Num(p[0], "x"), y = Fach.Num(p[1], "y"), z = Fach.Num(p[2], "z");
            Joints j; try { j = Inverse(c, x, y, z); } catch (ArgumentException ex) { throw new ArgumentException($"Zeile {n}: {ex.Message}"); }
            var secs = last is null ? 0 : new[] { Math.Abs(j.Base - last.Base), Math.Abs(j.Shoulder - last.Shoulder), Math.Abs(j.Elbow - last.Elbow) }.Max() / c.MaxDegPerSec;
            res.Add(new(res.Count + 1, x, y, z, j, Math.Round(secs, 3))); last = j;
        }
        if (res.Count == 0) throw new FormatException("Keine Wegpunkte.");
        return res;
    }

    public static string Csv(IEnumerable<ProgramStep> p)
    {
        var sb = new StringBuilder("Schritt;x_mm;y_mm;z_mm;Basis_Grad;Schulter_Grad;Ellbogen_Grad;Dauer_s\n");
        foreach (var s in p) sb.Append(FormattableString.Invariant($"{s.Index};{s.X:0.##};{s.Y:0.##};{s.Z:0.##};{s.J.Base:0.##};{s.J.Shoulder:0.##};{s.J.Elbow:0.##};{s.Seconds:0.###}\n"));
        return sb.ToString();
    }
}
