namespace Trendo.Core;

/// <summary>F3 Briefe (Vorbild-Analyse, nächster Schritt 1): Unterschrift mit Maus oder Stift zeichnen. Die Striche werden hier zu einem PNG gerastert –
/// geglättete Kanten, dunkle Tinte auf Weiß, auf die Striche zugeschnitten. Keine qualifizierte elektronische Signatur.</summary>
public static class LetterSignature
{
    /// <summary>Striche (Punkte in Pixeln des Zeichenfelds) als PNG; null, wenn zu wenig gezeichnet wurde.</summary>
    public static byte[]? ToPng(IReadOnlyList<IReadOnlyList<(double X, double Y)>> strokes, double scale = 3, double pen = 2.4)
    {
        var pts = strokes.Where(s => s.Count > 0).SelectMany(s => s).ToList();
        if (pts.Count < 2) return null;
        double minX = pts.Min(p => p.X), maxX = pts.Max(p => p.X), minY = pts.Min(p => p.Y), maxY = pts.Max(p => p.Y);
        if (maxX - minX < 8 && maxY - minY < 8) return null; // ein Tupfer ist keine Unterschrift
        const double Pad = 6;
        var w = (int)Math.Ceiling((maxX - minX + 2 * Pad) * scale); var h = (int)Math.Ceiling((maxY - minY + 2 * Pad) * scale);
        var cover = new float[w * h];
        var r = pen * scale / 2;
        (double, double) P((double X, double Y) p) => ((p.X - minX + Pad) * scale, (p.Y - minY + Pad) * scale);
        foreach (var s in strokes.Where(s => s.Count > 0))
        {
            if (s.Count == 1) { Segment(cover, w, h, P(s[0]), P(s[0]), r); continue; }
            for (var i = 1; i < s.Count; i++) Segment(cover, w, h, P(s[i - 1]), P(s[i]), r);
        }
        // Tinte #1B2A4A auf Weiß
        var rgb = new byte[w * h * 3];
        for (var i = 0; i < cover.Length; i++)
        {
            var a = Math.Clamp(cover[i], 0f, 1f);
            rgb[i * 3] = (byte)(255 - a * (255 - 0x1B)); rgb[i * 3 + 1] = (byte)(255 - a * (255 - 0x2A)); rgb[i * 3 + 2] = (byte)(255 - a * (255 - 0x4A));
        }
        return Mesh3D.Png(w, h, rgb);
    }

    /// <summary>Strich als Kapsel: Abdeckung aus dem Abstand zur Strecke, 1 Pixel weiche Kante.</summary>
    static void Segment(float[] cover, int w, int h, (double X, double Y) a, (double X, double Y) b, double r)
    {
        int x0 = Math.Max(0, (int)Math.Floor(Math.Min(a.X, b.X) - r - 1)), x1 = Math.Min(w - 1, (int)Math.Ceiling(Math.Max(a.X, b.X) + r + 1));
        int y0 = Math.Max(0, (int)Math.Floor(Math.Min(a.Y, b.Y) - r - 1)), y1 = Math.Min(h - 1, (int)Math.Ceiling(Math.Max(a.Y, b.Y) + r + 1));
        double dx = b.X - a.X, dy = b.Y - a.Y, len2 = dx * dx + dy * dy;
        for (var y = y0; y <= y1; y++)
            for (var x = x0; x <= x1; x++)
            {
                double px = x + 0.5 - a.X, py = y + 0.5 - a.Y;
                var t = len2 == 0 ? 0 : Math.Clamp((px * dx + py * dy) / len2, 0, 1);
                double ex = px - t * dx, ey = py - t * dy;
                var c = (float)Math.Clamp(r + 0.5 - Math.Sqrt(ex * ex + ey * ey), 0, 1);
                if (c > cover[y * w + x]) cover[y * w + x] = c;
            }
    }
}
