using System.IO.Compression;
using System.Text;

namespace Trendo.Core;

public sealed record RgbImage(int Width, int Height, byte[] Rgb);

/// <summary>Bilder lesen (PNG/BMP) als RGB – genutzt von der Spielbild-Prüfung (GameShotCheck).
/// T-0720: aus dem entfernten Studio „Kameras &amp; Roboter“ (CORE-09, früher Robotics.cs) unverändert übernommen; Farbsuche, Kalibrierung und Roboterarm sind gelöscht.</summary>
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
}

/// <summary>PNG schreiben und Homographie aus 4 Punktpaaren – genutzt von „Dokumente scannen“ (Entzerren) und als Bildquelle in Tests.
/// T-0720: aus dem entfernten Studio „Kameras &amp; Roboter“ (CORE-09, früher Robotics2.cs) unverändert übernommen; Objekte zählen, Belichtung, Schärfe, Bewegung, Maske und Arm-Rechnungen sind gelöscht.</summary>
public static class VisionPlus
{
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
