namespace Trendo.Core.Kreativ;

/// <summary>T-0733 (STUDIO-ART-01): Auswahl im Künstlerstudio wie in Krita/Procreate – Rechteck oder Lasso, dann den Inhalt der Ebene
/// verschieben und skalieren. Die Auswahl ist ein Vieleck in Leinwand-Pixeln; die Pixel werden beim Umsetzen ausgeschnitten,
/// bilinear auf die neue Größe gerechnet und über die Ebene gelegt.</summary>
public static class PaintSelection
{
    /// <summary>Vieleck eines Rechtecks (zwei beliebige Ecken).</summary>
    public static List<(double X, double Y)> Rect(double x0, double y0, double x1, double y1)
    {
        double l = Math.Min(x0, x1), r = Math.Max(x0, x1), t = Math.Min(y0, y1), b = Math.Max(y0, y1);
        return [(l, t), (r, t), (r, b), (l, b)];
    }

    /// <summary>Umschließendes Rechteck (X, Y, Breite, Höhe).</summary>
    public static (double X, double Y, double W, double H) Bounds(IReadOnlyList<(double X, double Y)> poly)
    {
        if (poly.Count == 0) return (0, 0, 0, 0);
        double x0 = poly.Min(p => p.X), y0 = poly.Min(p => p.Y);
        return (x0, y0, poly.Max(p => p.X) - x0, poly.Max(p => p.Y) - y0);
    }

    /// <summary>Maske der Leinwand: true, wo die Pixelmitte im Vieleck liegt (gerade-ungerade-Regel, auch für überkreuzte Lasso-Linien).</summary>
    public static bool[] Mask(int w, int h, IReadOnlyList<(double X, double Y)> poly)
    {
        var m = new bool[w * h];
        if (poly.Count < 3) return m;
        var xs = new List<double>();
        var (bx, by, bw, bh) = Bounds(poly);
        int yA = Math.Max(0, (int)Math.Floor(by)), yB = Math.Min(h - 1, (int)Math.Ceiling(by + bh));
        for (var y = yA; y <= yB; y++)
        {
            var cy = y + 0.5; xs.Clear();
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                var (ax, ay) = poly[i]; var (qx, qy) = poly[j];
                if ((ay > cy) != (qy > cy)) xs.Add(ax + (cy - ay) * (qx - ax) / (qy - ay));
            }
            xs.Sort();
            for (var k = 0; k + 1 < xs.Count; k += 2)
            {
                int xa = Math.Max(0, (int)Math.Ceiling(xs[k] - 0.5)), xb = Math.Min(w - 1, (int)Math.Floor(xs[k + 1] - 0.5));
                for (var x = xa; x <= xb; x++) m[y * w + x] = true;
            }
        }
        return m;
    }

    /// <summary>Anzahl ausgewählter Pixel.</summary>
    public static int Count(bool[] mask) => mask.Count(b => b);

    /// <summary>Ausgewählten Inhalt der Ebene verschieben und skalieren: Ausschneiden, um (dx, dy) versetzen und um (sx, sy) vergrößern –
    /// Anker ist die linke obere Ecke des umschließenden Rechtecks –, dann darüberlegen (normal). Gibt das umgesetzte Vieleck zurück. Die Ebene wird an Ort und Stelle geändert.</summary>
    public static List<(double X, double Y)> Transform(Pixmap layer, IReadOnlyList<(double X, double Y)> poly, double dx, double dy, double sx = 1, double sy = 1)
    {
        if (poly.Count < 3) throw new InvalidOperationException("Erst einen Bereich auswählen – Rechteck aufziehen oder mit dem Lasso umfahren.");
        if (!(sx > 0.01 && sy > 0.01 && sx < 100 && sy < 100)) throw new ArgumentException("Die Größe muss zwischen 1 % und 10 000 % liegen.");
        int w = layer.W, h = layer.H;
        var mask = Mask(w, h, poly);
        var (bx, by, bw, bh) = Bounds(poly);
        int x0 = Math.Max(0, (int)Math.Floor(bx)), y0 = Math.Max(0, (int)Math.Floor(by));
        int x1 = Math.Min(w - 1, (int)Math.Ceiling(bx + bw)), y1 = Math.Min(h - 1, (int)Math.Ceiling(by + bh));
        if (x1 < x0 || y1 < y0) throw new InvalidOperationException("Die Auswahl liegt außerhalb der Leinwand.");
        // 1. Ausschneiden (unvormultipliziert RGBA wie die Ebene)
        int pw = x1 - x0 + 1, ph = y1 - y0 + 1; var piece = new Pixmap(pw, ph); var any = false;
        for (var y = y0; y <= y1; y++)
            for (var x = x0; x <= x1; x++)
            {
                var i = y * w + x; if (!mask[i]) continue;
                var s = i * 4; var t = ((y - y0) * pw + (x - x0)) * 4;
                if (layer.Px[s + 3] != 0) any = true;
                for (var c = 0; c < 4; c++) { piece.Px[t + c] = layer.Px[s + c]; layer.Px[s + c] = 0; }
            }
        // 2. Abbildung q = Ecke + Verschiebung + (p − Ecke) · Faktor, Ecke = oben links der Auswahl (wie die Griffe in der Oberfläche)
        double ox = bx + dx, oy = by + dy;
        if (any)
        {
            double qx0 = ox + (x0 - bx) * sx, qy0 = oy + (y0 - by) * sy, qx1 = ox + (x1 + 1 - bx) * sx, qy1 = oy + (y1 + 1 - by) * sy;
            int ax = Math.Max(0, (int)Math.Floor(qx0)), ay = Math.Max(0, (int)Math.Floor(qy0));
            int bx2 = Math.Min(w - 1, (int)Math.Ceiling(qx1) - 1), by2 = Math.Min(h - 1, (int)Math.Ceiling(qy1) - 1);
            for (var y = ay; y <= by2; y++)
                for (var x = ax; x <= bx2; x++)
                {
                    // Pixelmitte zurück in die Quelle (Koordinaten im ausgeschnittenen Stück)
                    var u = bx + (x + 0.5 - ox) / sx - x0 - 0.5; var v = by + (y + 0.5 - oy) / sy - y0 - 0.5;
                    if (u < -0.5 || v < -0.5 || u > pw - 0.5 || v > ph - 0.5) continue;
                    var (r, g, b, a) = Sample(piece, u, v);
                    if (a <= 0) continue;
                    var o = (y * w + x) * 4; double da = layer.Px[o + 3] / 255.0, sa = a / 255.0, oa = sa + da * (1 - sa);
                    layer.Px[o] = (byte)Math.Round((r * sa + layer.Px[o] * da * (1 - sa)) / oa);
                    layer.Px[o + 1] = (byte)Math.Round((g * sa + layer.Px[o + 1] * da * (1 - sa)) / oa);
                    layer.Px[o + 2] = (byte)Math.Round((b * sa + layer.Px[o + 2] * da * (1 - sa)) / oa);
                    layer.Px[o + 3] = (byte)Math.Round(oa * 255);
                }
        }
        return [.. poly.Select(p => (Math.Round(ox + (p.X - bx) * sx, 2), Math.Round(oy + (p.Y - by) * sy, 2)))];
    }

    /// <summary>Inhalt der Auswahl auf der Ebene löschen (Entf).</summary>
    public static int Clear(Pixmap layer, IReadOnlyList<(double X, double Y)> poly)
    {
        var mask = Mask(layer.W, layer.H, poly); var n = 0;
        for (var i = 0; i < mask.Length; i++) if (mask[i] && layer.Px[i * 4 + 3] != 0) { layer.Px[i * 4] = layer.Px[i * 4 + 1] = layer.Px[i * 4 + 2] = layer.Px[i * 4 + 3] = 0; n++; }
        return n;
    }

    /// <summary>Bilinear abtasten, mit vormultiplizierter Deckkraft (keine dunklen Ränder an durchsichtigen Kanten).</summary>
    static (double R, double G, double B, double A) Sample(Pixmap p, double u, double v)
    {
        int ix = (int)Math.Floor(u), iy = (int)Math.Floor(v); double fx = u - ix, fy = v - iy;
        double r = 0, g = 0, b = 0, a = 0;
        for (var k = 0; k < 4; k++)
        {
            int x = Math.Clamp(ix + (k & 1), 0, p.W - 1), y = Math.Clamp(iy + (k >> 1), 0, p.H - 1);
            var wgt = ((k & 1) == 1 ? fx : 1 - fx) * ((k >> 1) == 1 ? fy : 1 - fy); if (wgt == 0) continue;
            var i = (y * p.W + x) * 4; var pa = p.Px[i + 3] / 255.0 * wgt;
            r += p.Px[i] * pa; g += p.Px[i + 1] * pa; b += p.Px[i + 2] * pa; a += pa;
        }
        return a <= 1e-9 ? (0, 0, 0, 0) : (r / a, g / a, b / a, a * 255);
    }
}
