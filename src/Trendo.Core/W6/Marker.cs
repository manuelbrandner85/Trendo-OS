using System.Globalization;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace Trendo.Core;

/// <summary>Masterprompt 131: Schnittlagebild – Schnittteile (mit Nahtzugabe) auf der Stoffbahn. Breite = Stoffbreite (x), Länge = Verbrauch (y nach unten), alles in cm.
/// Automatisch legen per Skyline-Verfahren (jede Lage in beiden Drehungen probiert, tiefste Stelle zuerst), Prüfen auf Überlappung und Stoffrand, PDF zum Ausdrucken.</summary>
public static class Marker
{
    public sealed record Piece(string Name, double W, double H, int Qty);
    public sealed record Placed(int Piece, int Copy, double X, double Y, bool Rot);
    public const double Seam = 1.5;

    public static (double W, double H) Size(Piece p, bool rot) => rot ? (p.H + 2 * Seam, p.W + 2 * Seam) : (p.W + 2 * Seam, p.H + 2 * Seam);

    /// <summary>Skyline-Packen: Teile nach Fläche absteigend; für jedes die Stelle mit der kleinsten Unterkante (bei Gleichstand links), beide Drehungen.</summary>
    public static List<Placed> AutoLayout(IReadOnlyList<Piece> pieces, double fabricW)
    {
        var todo = pieces.SelectMany((p, i) => Enumerable.Range(0, Math.Max(0, p.Qty)).Select(c => (i, c))).OrderByDescending(t => (pieces[t.i].W + 2 * Seam) * (pieces[t.i].H + 2 * Seam)).ToList();
        var sky = new List<(double X, double W, double Y)> { (0, fabricW, 0) };
        var res = new List<Placed>();
        foreach (var (i, c) in todo)
        {
            (double X, double Y, bool Rot, double Bottom)? best = null;
            foreach (var rot in new[] { false, true })
            {
                var (w, h) = Size(pieces[i], rot); if (w > fabricW + 1e-9) continue;
                for (var s = 0; s < sky.Count; s++)
                {
                    var x = sky[s].X; if (x + w > fabricW + 1e-9) continue;
                    var y = sky.Where(k => k.X < x + w - 1e-9 && k.X + k.W > x + 1e-9).Max(k => k.Y);
                    if (best is null || y + h < best.Value.Bottom - 1e-9 || (Math.Abs(y + h - best.Value.Bottom) < 1e-9 && x < best.Value.X)) best = (x, y, rot, y + h);
                }
            }
            if (best is not { } b) throw new ArgumentException($"„{pieces[i].Name}“ ist breiter als der Stoff ({fabricW:0} cm).");
            var (bw, bh) = Size(pieces[i], b.Rot);
            res.Add(new Placed(i, c, b.X, b.Y, b.Rot));
            // Skyline aktualisieren
            var nx = b.X; var ne = b.X + bw; var news = new List<(double X, double W, double Y)>();
            foreach (var k in sky)
            {
                var kx2 = k.X + k.W;
                if (kx2 <= nx + 1e-9 || k.X >= ne - 1e-9) { news.Add(k); continue; }
                if (k.X < nx) news.Add((k.X, nx - k.X, k.Y));
                if (kx2 > ne) news.Add((ne, kx2 - ne, k.Y));
            }
            news.Add((nx, bw, b.Y + bh));
            sky = news.OrderBy(k => k.X).Aggregate(new List<(double X, double W, double Y)>(), (l, k) => { if (l.Count > 0 && Math.Abs(l[^1].Y - k.Y) < 1e-9 && Math.Abs(l[^1].X + l[^1].W - k.X) < 1e-9) l[^1] = (l[^1].X, l[^1].W + k.W, k.Y); else l.Add(k); return l; });
        }
        return res;
    }

    public static (double X, double Y, double W, double H) Box(IReadOnlyList<Piece> pieces, Placed p) { var (w, h) = Size(pieces[p.Piece], p.Rot); return (p.X, p.Y, w, h); }

    /// <summary>Stoffverbrauch in cm (Unterkante des tiefsten Teils).</summary>
    public static double Length(IReadOnlyList<Piece> pieces, IReadOnlyList<Placed> layout) => layout.Count == 0 ? 0 : layout.Max(p => Box(pieces, p) is var b ? b.Y + b.H : 0);

    /// <summary>Ausnutzung: Fläche der Teile ohne Nahtzugabe / (Stoffbreite × Verbrauch).</summary>
    public static double Efficiency(IReadOnlyList<Piece> pieces, IReadOnlyList<Placed> layout, double fabricW)
    {
        var len = Length(pieces, layout); if (len <= 0) return 0;
        return layout.Sum(p => pieces[p.Piece].W * pieces[p.Piece].H) / (fabricW * len);
    }

    /// <summary>Indizes der Teile, die sich überlappen oder über den Stoffrand ragen.</summary>
    public static HashSet<int> Problems(IReadOnlyList<Piece> pieces, IReadOnlyList<Placed> layout, double fabricW)
    {
        var bad = new HashSet<int>();
        for (var i = 0; i < layout.Count; i++)
        {
            var a = Box(pieces, layout[i]); if (a.X < -1e-6 || a.Y < -1e-6 || a.X + a.W > fabricW + 1e-6) bad.Add(i);
            for (var j = i + 1; j < layout.Count; j++)
            {
                var b = Box(pieces, layout[j]);
                if (a.X < b.X + b.W - 1e-6 && b.X < a.X + a.W - 1e-6 && a.Y < b.Y + b.H - 1e-6 && b.Y < a.Y + a.H - 1e-6) { bad.Add(i); bad.Add(j); }
            }
        }
        return bad;
    }

    /// <summary>Schnittlagebild als PDF (A4 hoch): Stoffbahn maßstäblich mit 10-cm-Raster, Teile mit Namen und Maßen, Kennzahlen und Teileliste.</summary>
    public static void Pdf(string path, string title, IReadOnlyList<Piece> pieces, IReadOnlyList<Placed> layout, double fabricW)
    {
        Sys.PdfTools.Fonts.Ensure();
        using var doc = new PdfDocument(); var page = doc.AddPage(); page.Width = XUnit.FromMillimeter(210); page.Height = XUnit.FromMillimeter(297);
        using var g = XGraphics.FromPdfPage(page);
        var de = CultureInfo.GetCultureInfo("de-DE"); var len = Math.Max(10, Length(pieces, layout));
        var head = new XFont("Arial", 14, XFontStyleEx.Bold); var small = new XFont("Arial", 8); var mid = new XFont("Arial", 9);
        g.DrawString(title + " – Schnittlagebild", head, XBrushes.Black, new XPoint(40, 50));
        g.DrawString($"Stoffbreite {fabricW:0} cm · Verbrauch {len / 100:0.00} m · Ausnutzung {Efficiency(pieces, layout, fabricW) * 100:0} % · Nahtzugabe {Seam.ToString("0.0", de)} cm", mid, XBrushes.Black, new XPoint(40, 68));
        double availW = page.Width.Point - 80, availH = page.Height.Point - 230; var k = Math.Min(availW / fabricW, availH / len);
        double ox = 40, oy = 85;
        g.DrawRectangle(new XPen(XColors.Gray, 0.8), new XSolidBrush(XColor.FromArgb(245, 245, 240)), ox, oy, fabricW * k, len * k);
        var grid = new XPen(XColor.FromArgb(210, 210, 210), 0.3);
        for (var x = 10.0; x < fabricW; x += 10) g.DrawLine(grid, ox + x * k, oy, ox + x * k, oy + len * k);
        for (var y = 10.0; y < len; y += 10) { g.DrawLine(grid, ox, oy + y * k, ox + fabricW * k, oy + y * k); if ((int)y % 50 == 0) g.DrawString($"{y:0}", small, XBrushes.Gray, new XPoint(ox - 18, oy + y * k + 3)); }
        XColor[] cols = [XColor.FromArgb(47, 134, 246), XColor.FromArgb(34, 179, 94), XColor.FromArgb(240, 138, 60), XColor.FromArgb(124, 77, 255), XColor.FromArgb(229, 72, 77)];
        foreach (var p in layout)
        {
            var b = Box(pieces, p); var c = cols[p.Piece % cols.Length];
            g.DrawRectangle(new XPen(c, 0.8), new XSolidBrush(XColor.FromArgb(70, c.R, c.G, c.B)), ox + b.X * k, oy + b.Y * k, b.W * k, b.H * k);
            g.DrawRectangle(new XPen(c, 0.4) { DashStyle = XDashStyle.Dash }, ox + (b.X + Seam) * k, oy + (b.Y + Seam) * k, (b.W - 2 * Seam) * k, (b.H - 2 * Seam) * k);
            g.DrawString(pieces[p.Piece].Name + (p.Rot ? " ↻" : ""), small, XBrushes.Black, new XPoint(ox + b.X * k + 3, oy + b.Y * k + 10));
        }
        var ly = oy + len * k + 24;
        g.DrawString("Teileliste (Maße ohne Nahtzugabe)", mid, XBrushes.Black, new XPoint(40, ly));
        foreach (var p in pieces) { ly += 12; g.DrawString($"{p.Name}: {p.W:0.#} × {p.H:0.#} cm · {p.Qty}×", small, XBrushes.Black, new XPoint(48, ly)); }
        g.DrawString("Gestrichelt: Nahtlinie · durchgezogen: Schnittkante · ↻ gedreht · Fadenlauf beachten", small, XBrushes.Gray, new XPoint(40, page.Height.Point - 30));
        doc.Save(path);
    }

    /// <summary>Größen ableiten (vereinfachte Gradierung): je Größenschritt von der Basisgröße wächst jedes Teil in der Breite um ein Viertel des Umfangsprungs
    /// (vier Teilbreiten ergeben den Umfang) und in der Länge um <paramref name="lengthStep"/> cm. Kleinste Maße 1 cm.</summary>
    public static List<Piece> Grade(IReadOnlyList<Piece> pieces, int steps, double girthStep, double lengthStep = 1) =>
        pieces.Select(p => p with { W = Math.Max(1, Math.Round(p.W + steps * girthStep / 4, 1)), H = Math.Max(1, Math.Round(p.H + steps * lengthStep, 1)) }).ToList();

    /// <summary>Stoffbedarf je Größe: Teile gradieren und automatisch legen. Basisgröße ist die mittlere (wie in der Größentabelle).</summary>
    public static List<(string Size, double Meters, double Efficiency)> SizeRun(IReadOnlyList<Piece> pieces, IReadOnlyList<string> sizes, double fabricW, double girthStep, double lengthStep = 1)
    {
        var res = new List<(string, double, double)>(); var b = sizes.Count / 2;
        for (var i = 0; i < sizes.Count; i++)
        {
            var g = Grade(pieces, i - b, girthStep, lengthStep);
            try { var l = AutoLayout(g, fabricW); res.Add((sizes[i], Math.Round(Length(g, l) / 100, 2), Efficiency(g, l, fabricW))); }
            catch (ArgumentException) { res.Add((sizes[i], double.NaN, 0)); }
        }
        return res;
    }

    /// <summary>Schnittlagebild als SVG im Maßstab 1:1 (Einheit cm) – für Plotter, Inkscape oder Zuschnittprogramme: Stoffbahn, 10-cm-Raster, Schnittkante, Nahtlinie, Namen.</summary>
    public static string Svg(string title, IReadOnlyList<Piece> pieces, IReadOnlyList<Placed> layout, double fabricW)
    {
        var ic = CultureInfo.InvariantCulture; string F(double v) => v.ToString("0.##", ic);
        static string E(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
        var len = Math.Max(10, Length(pieces, layout));
        var sb = new System.Text.StringBuilder();
        sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{F(fabricW)}cm\" height=\"{F(len)}cm\" viewBox=\"0 0 {F(fabricW)} {F(len)}\">\n");
        sb.Append($"<title>{E(title)} – Schnittlagebild</title>\n");
        sb.Append($"<rect x=\"0\" y=\"0\" width=\"{F(fabricW)}\" height=\"{F(len)}\" fill=\"#F5F5F0\" stroke=\"#888\" stroke-width=\"0.2\"/>\n<g stroke=\"#D2D2D2\" stroke-width=\"0.08\">");
        for (var x = 10.0; x < fabricW; x += 10) sb.Append($"<line x1=\"{F(x)}\" y1=\"0\" x2=\"{F(x)}\" y2=\"{F(len)}\"/>");
        for (var y = 10.0; y < len; y += 10) sb.Append($"<line x1=\"0\" y1=\"{F(y)}\" x2=\"{F(fabricW)}\" y2=\"{F(y)}\"/>");
        sb.Append("</g>\n");
        string[] cols = ["#2F86F6", "#22B35E", "#F08A3C", "#7C4DFF", "#E5484D"];
        foreach (var p in layout)
        {
            var b = Box(pieces, p); var c = cols[p.Piece % cols.Length]; var pc = pieces[p.Piece];
            sb.Append($"<g id=\"teil-{p.Piece}-{p.Copy}\"><rect x=\"{F(b.X)}\" y=\"{F(b.Y)}\" width=\"{F(b.W)}\" height=\"{F(b.H)}\" fill=\"{c}\" fill-opacity=\"0.25\" stroke=\"{c}\" stroke-width=\"0.2\"/>");
            sb.Append($"<rect x=\"{F(b.X + Seam)}\" y=\"{F(b.Y + Seam)}\" width=\"{F(b.W - 2 * Seam)}\" height=\"{F(b.H - 2 * Seam)}\" fill=\"none\" stroke=\"{c}\" stroke-width=\"0.12\" stroke-dasharray=\"0.8 0.5\"/>");
            sb.Append($"<text x=\"{F(b.X + 2)}\" y=\"{F(b.Y + 4)}\" font-family=\"Arial\" font-size=\"2.4\" fill=\"#1F2933\">{E(pc.Name + (p.Rot ? " ↻" : ""))}</text>");
            sb.Append($"<text x=\"{F(b.X + 2)}\" y=\"{F(b.Y + 7)}\" font-family=\"Arial\" font-size=\"1.8\" fill=\"#5C6B7A\">{F(pc.W)} × {F(pc.H)} cm</text></g>\n");
        }
        sb.Append("</svg>\n");
        return sb.ToString();
    }
}
