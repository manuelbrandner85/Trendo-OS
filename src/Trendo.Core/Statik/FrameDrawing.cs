using System.Globalization;
using System.Text;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using Trendo.Core.Arch;

namespace Trendo.Core.Statik;

/// <summary>Welche Ergebnisse gezeichnet werden.</summary>
public sealed record FrameView(bool Deformed = true, bool Moment = true, bool Shear = false, bool Normal = false, bool Reactions = true, bool Values = true);

/// <summary>T-0258: Zeichnung des Tragwerks als Zeichenelemente (wie beim Grundriss) – Koordinaten in cm, y nach unten (Bildschirm):
/// Stäbe (grün/rot nach Nachweis), Lager, Gelenke, Lasten, Schnittkraftlinien, Biegelinie, Reaktionen.</summary>
public static class FrameDrawing
{
    static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    public static (double X, double Y) P(double x, double y) => (x * 100, -y * 100);

    public sealed record Theme(string Bg, string Member, string Ok, string Bad, string Node, string Support, string Load, string Moment, string Shear, string Normal, string Deformed, string Reaction, string Text);
    public static readonly Theme Screen = new("#0D1A2D", "#DCE6F4", "#4ADE80", "#F87171", "#FFFFFF", "#9FB4D3", "#F5A524", "#F87171", "#34D399", "#60A5FA", "#C4B5FD", "#E879F9", "#F2F6FC");
    public static readonly Theme Paper = new("#FFFFFF", "#1A1A1A", "#15803D", "#B91C1C", "#1A1A1A", "#333333", "#B45309", "#DC2626", "#059669", "#2563EB", "#7C3AED", "#A21CAF", "#1A1A1A");

    public static List<Shape> Build(FrameModel f, FrameResult? r, FrameView v, Theme t)
    {
        var s = new List<Shape>();
        var size = f.Bounds() is { } b ? Math.Max(1, Math.Max(b.MaxX - b.MinX, b.MaxY - b.MinY)) * 100 : 500;
        var unit = Math.Clamp(size / 40, 6, 30); // Symbolgröße in cm
        // Ergebnisse unter den Stäben
        if (r is not null)
        {
            void Diagram(Func<MemberResult, double[]> pick, string color, string unitText, bool flip)
            {
                var max = r.Members.SelectMany(pick).Select(Math.Abs).DefaultIfEmpty(0).Max(); if (max < 1e-9) return;
                var k = size * 0.12 / max;
                foreach (var m in r.Members)
                {
                    var mem = f.Member(m.Id); if (mem is null) continue;
                    var a = f.Node(mem.A)!; var bb = f.Node(mem.B)!; var c = (bb.X - a.X) / m.L; var sn = (bb.Y - a.Y) / m.L;
                    var vals = pick(m); var pts = new List<(double, double)>();
                    for (var i = 0; i < m.Xs.Length; i++)
                    {
                        var (bx, by) = P(a.X + c * m.Xs[i], a.Y + sn * m.Xs[i]);
                        // Normale im Bildschirm (y nach unten): positive Momente auf der Zugseite unten zeichnen
                        var o = vals[i] * k * (flip ? 1 : -1); var nx = -sn; var ny = -c; // Bildschirm-Normale „unten“ für einen waagrechten Stab
                        pts.Add((bx + nx * -o, by + ny * -o));
                    }
                    var (s0x, s0y) = P(a.X, a.Y); var (s1x, s1y) = P(bb.X, bb.Y);
                    var poly = new List<(double, double)> { (s0x, s0y) }; poly.AddRange(pts); poly.Add((s1x, s1y));
                    s.Add(new DPoly(poly, color + "55", color, 0.8, "ERGEBNIS"));
                    if (v.Values)
                    {
                        var iMax = Array.IndexOf(vals, vals.MaxBy(Math.Abs));
                        if (Math.Abs(vals[iMax]) > max * 0.05)
                            s.Add(new DText(pts[iMax].Item1, pts[iMax].Item2 + (vals[iMax] * (flip ? 1 : -1) >= 0 ? unit * 0.9 : -unit * 0.9), vals[iMax].ToString("0.##", De) + unitText, unit * 0.9, color, "ERGEBNIS", true));
                        foreach (var end in new[] { 0, vals.Length - 1 })
                            if (end != iMax && Math.Abs(vals[end]) > max * 0.15) s.Add(new DText(pts[end].Item1, pts[end].Item2 + (vals[end] * (flip ? 1 : -1) >= 0 ? unit * 0.9 : -unit * 0.9), vals[end].ToString("0.##", De), unit * 0.75, color, "ERGEBNIS"));
                    }
                }
            }
            if (v.Normal) Diagram(m => m.N, t.Normal, " kN", false);
            if (v.Shear) Diagram(m => m.V, t.Shear, " kN", false);
            if (v.Moment) Diagram(m => m.M, t.Moment, " kNm", true);
            if (v.Deformed)
            {
                var maxD = r.Members.SelectMany(m => m.Dx.Zip(m.Dy, (x, y) => Math.Sqrt(x * x + y * y))).DefaultIfEmpty(0).Max();
                if (maxD > 1e-12)
                {
                    var k = size * 0.08 / maxD / 100; // in m
                    foreach (var m in r.Members)
                    {
                        var mem = f.Member(m.Id); if (mem is null) continue;
                        var a = f.Node(mem.A)!; var bb = f.Node(mem.B)!; var c = (bb.X - a.X) / m.L; var sn = (bb.Y - a.Y) / m.L;
                        for (var i = 1; i < m.Xs.Length; i++)
                        {
                            var (x0, y0) = P(a.X + c * m.Xs[i - 1] + m.Dx[i - 1] * k, a.Y + sn * m.Xs[i - 1] + m.Dy[i - 1] * k);
                            var (x1, y1) = P(a.X + c * m.Xs[i] + m.Dx[i] * k, a.Y + sn * m.Xs[i] + m.Dy[i] * k);
                            s.Add(new DLine(x0, y0, x1, y1, t.Deformed, 1.4, "ERGEBNIS", true));
                        }
                    }
                }
            }
        }
        // Stäbe
        foreach (var m in f.Members)
        {
            var a = f.Node(m.A)!; var bb = f.Node(m.B)!; var (x0, y0) = P(a.X, a.Y); var (x1, y1) = P(bb.X, bb.Y);
            var res = r?.Of(m.Id);
            s.Add(new DPoly([(x0, y0), (x1, y1)], null, res is null ? t.Member : res.Ok ? t.Ok : t.Bad, 4, "STAEBE", m.Id, false));
            var len = Math.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0)); var ux = (x1 - x0) / len; var uy = (y1 - y0) / len;
            foreach (var (h, hx, hy, dir) in new[] { (m.HingeA, x0, y0, 1), (m.HingeB, x1, y1, -1) })
                if (h) s.Add(new DPoly(Circle(hx + ux * dir * unit * 0.6, hy + uy * dir * unit * 0.6, unit * 0.32), t.Bg, t.Member, 1.4, "STAEBE"));
        }
        // Knoten und Lager
        foreach (var n in f.Nodes)
        {
            var (x, y) = P(n.X, n.Y);
            s.Add(new DPoly(Circle(x, y, unit * 0.18), t.Node, null, 0, "KNOTEN", n.Id));
            switch (n.Support)
            {
                case "fest": case "los":
                    s.Add(new DPoly([(x, y), (x - unit * 0.7, y + unit * 1.1), (x + unit * 0.7, y + unit * 1.1)], null, t.Support, 1.6, "LAGER"));
                    var gy = y + unit * (n.Support == "los" ? 1.45 : 1.1);
                    if (n.Support == "los") s.Add(new DLine(x - unit * 0.9, y + unit * 1.25, x + unit * 0.9, y + unit * 1.25, t.Support, 1.2, "LAGER"));
                    s.Add(new DLine(x - unit, gy, x + unit, gy, t.Support, 1.6, "LAGER"));
                    for (var k = -2; k <= 2; k++) s.Add(new DLine(x + k * unit * 0.4, gy, x + k * unit * 0.4 - unit * 0.3, gy + unit * 0.35, t.Support, 0.8, "LAGER"));
                    break;
                case "einspannung":
                    s.Add(new DLine(x - unit, y, x + unit, y, t.Support, 2.4, "LAGER"));
                    for (var k = -2; k <= 2; k++) s.Add(new DLine(x + k * unit * 0.45, y, x + k * unit * 0.45 - unit * 0.35, y + unit * 0.45, t.Support, 0.8, "LAGER"));
                    break;
            }
        }
        // Lasten
        foreach (var l in f.Loads)
        {
            if (l.Kind == "knoten" && f.Node(l.Target) is { } n)
            {
                var (x, y) = P(n.X, n.Y);
                if (Math.Abs(l.Value) > 1e-9) { Arrow(s, x, y - unit * (l.Value > 0 ? 3.2 : -3.2), x, y - unit * (l.Value > 0 ? 0.3 : -0.3), t.Load, unit, l.Id); s.Add(new DText(x + unit * 0.8, y - unit * 3.4, Fmt(Math.Abs(l.Value)) + " kN", unit * 0.85, t.Load, "LASTEN")); }
                if (Math.Abs(l.Fx) > 1e-9) { Arrow(s, x - unit * (l.Fx > 0 ? 3.2 : -3.2), y, x - unit * (l.Fx > 0 ? 0.3 : -0.3), y, t.Load, unit, l.Id); s.Add(new DText(x - unit * 2, y - unit * 0.8, Fmt(Math.Abs(l.Fx)) + " kN", unit * 0.85, t.Load, "LASTEN")); }
                if (Math.Abs(l.M) > 1e-9) { s.Add(new DArc(x, y, unit * 1.6, 200, l.M > 0 ? -220 : 220, t.Load, 1.6, "LASTEN")); s.Add(new DText(x + unit * 2.2, y - unit * 1.6, Fmt(Math.Abs(l.M)) + " kNm", unit * 0.85, t.Load, "LASTEN")); }
            }
            else if (f.Member(l.Target) is { } m)
            {
                var a = f.Node(m.A)!; var bb = f.Node(m.B)!; var (x0, y0) = P(a.X, a.Y); var (x1, y1) = P(bb.X, bb.Y);
                if (l.Kind == "strecke")
                {
                    var h = unit * 2.2; var cnt = Math.Max(3, (int)(Math.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0)) / (unit * 2.5)));
                    s.Add(new DPoly([(x0, y0 - h), (x1, y1 - h)], null, t.Load, 1.4, "LASTEN", l.Id, false));
                    for (var k = 0; k <= cnt; k++) { var px = x0 + (x1 - x0) * k / cnt; var py = y0 + (y1 - y0) * k / cnt; Arrow(s, px, py - h, px, py - unit * 0.3, t.Load, unit * 0.7, l.Id); }
                    s.Add(new DText((x0 + x1) / 2, (y0 + y1) / 2 - h - unit * 0.8, Fmt(l.Value) + " kN/m", unit * 0.85, t.Load, "LASTEN", true));
                }
                else
                {
                    var px = x0 + (x1 - x0) * l.At; var py = y0 + (y1 - y0) * l.At;
                    Arrow(s, px, py - unit * 3.2, px, py - unit * 0.3, t.Load, unit, l.Id);
                    s.Add(new DText(px + unit * 0.8, py - unit * 3.4, Fmt(l.Value) + " kN", unit * 0.85, t.Load, "LASTEN"));
                }
            }
        }
        // Lagerreaktionen
        if (r is not null && v.Reactions)
            foreach (var (id, (rx, ry, rm)) in r.Reactions)
            {
                if (f.Node(id) is not { } n) continue; var (x, y) = P(n.X, n.Y); var below = unit * (n.Support == "einspannung" ? 1.2 : 2.2);
                if (Math.Abs(ry) > 1e-6) { Arrow(s, x, y + below + unit * 2.6 * (ry > 0 ? 1 : 0), x, y + below + unit * 2.6 * (ry > 0 ? 0 : 1), t.Reaction, unit * 0.9, null); s.Add(new DText(x + unit * 1.1, y + below + unit * 1.6, Fmt(ry) + " kN", unit * 0.8, t.Reaction, "REAKTION", true)); }
                if (Math.Abs(rx) > 1e-6) { Arrow(s, x - unit * (rx > 0 ? 3.4 : -3.4), y + unit * 0.6, x - unit * (rx > 0 ? 0.9 : -0.9), y + unit * 0.6, t.Reaction, unit * 0.9, null); s.Add(new DText(x - unit * 2.4, y + unit * 1.5, Fmt(rx) + " kN", unit * 0.8, t.Reaction, "REAKTION", true)); }
                if (Math.Abs(rm) > 1e-6) s.Add(new DText(x - unit * 2.4, y - unit * 1.2, Fmt(rm) + " kNm", unit * 0.8, t.Reaction, "REAKTION", true));
            }
        return s;
    }

    static string Fmt(double v) => v.ToString("0.##", De);
    static List<(double X, double Y)> Circle(double x, double y, double r) => Enumerable.Range(0, 16).Select(k => (x + Math.Cos(k * Math.PI / 8) * r, y + Math.Sin(k * Math.PI / 8) * r)).ToList();
    static void Arrow(List<Shape> s, double x0, double y0, double x1, double y1, string color, double unit, string? id)
    {
        s.Add(new DLine(x0, y0, x1, y1, color, 1.4, "LASTEN"));
        var l = Math.Max(1e-9, Math.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0))); var ux = (x1 - x0) / l; var uy = (y1 - y0) / l; var h = unit * 0.5;
        s.Add(new DPoly([(x1, y1), (x1 - ux * h - uy * h * 0.5, y1 - uy * h + ux * h * 0.5), (x1 - ux * h + uy * h * 0.5, y1 - uy * h - ux * h * 0.5)], color, null, 0, "LASTEN", id));
    }

    public static (double MinX, double MinY, double MaxX, double MaxY) Extent(List<Shape> shapes)
    {
        var pts = shapes.SelectMany(sh => sh switch
        {
            DPoly p => p.Pts, DLine l => [(l.X1, l.Y1), (l.X2, l.Y2)], DText t => [(t.X, t.Y)], DArc a => [(a.Cx - a.R, a.Cy - a.R), (a.Cx + a.R, a.Cy + a.R)], _ => [],
        }).ToList();
        return pts.Count == 0 ? (0, 0, 100, 100) : (pts.Min(p => p.X), pts.Min(p => p.Y), pts.Max(p => p.X), pts.Max(p => p.Y));
    }

    // ---------- SVG ----------
    public static string Svg(FrameModel f, FrameResult? r, FrameView v)
    {
        var shapes = Build(f, r, v, Paper); var (x0, y0, x1, y1) = Extent(shapes); const double pad = 60;
        string N(double d) => d.ToString("0.##", Inv);
        var sb = new StringBuilder($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"{N(x0 - pad)} {N(y0 - pad)} {N(x1 - x0 + 2 * pad)} {N(y1 - y0 + 2 * pad + 40)}\" font-family=\"Segoe UI, Arial, sans-serif\">\n");
        sb.Append($"<rect x=\"{N(x0 - pad)}\" y=\"{N(y0 - pad)}\" width=\"{N(x1 - x0 + 2 * pad)}\" height=\"{N(y1 - y0 + 2 * pad + 40)}\" fill=\"#FFFFFF\"/>\n");
        foreach (var sh in shapes)
            switch (sh)
            {
                case DPoly p: sb.Append($"<{(p.Closed ? "polygon" : "polyline")} points=\"{string.Join(' ', p.Pts.Select(q => N(q.X) + "," + N(q.Y)))}\" fill=\"{Color(p.Fill) ?? "none"}\" fill-opacity=\"{Alpha(p.Fill)}\" stroke=\"{Color(p.Stroke) ?? "none"}\" stroke-width=\"{N(p.Width)}\"/>\n"); break;
                case DLine l: sb.Append($"<line x1=\"{N(l.X1)}\" y1=\"{N(l.Y1)}\" x2=\"{N(l.X2)}\" y2=\"{N(l.Y2)}\" stroke=\"{Color(l.Stroke)}\" stroke-width=\"{N(l.Width)}\"{(l.Dashed ? " stroke-dasharray=\"6 4\"" : "")}/>\n"); break;
                case DArc a:
                    var (sx, sy) = (a.Cx + a.R * Math.Cos(a.StartDeg * Math.PI / 180), a.Cy + a.R * Math.Sin(a.StartDeg * Math.PI / 180));
                    var (ex, ey) = (a.Cx + a.R * Math.Cos((a.StartDeg + a.SweepDeg) * Math.PI / 180), a.Cy + a.R * Math.Sin((a.StartDeg + a.SweepDeg) * Math.PI / 180));
                    sb.Append($"<path d=\"M{N(sx)},{N(sy)} A{N(a.R)},{N(a.R)} 0 1 {(a.SweepDeg > 0 ? 1 : 0)} {N(ex)},{N(ey)}\" fill=\"none\" stroke=\"{Color(a.Stroke)}\" stroke-width=\"{N(a.Width)}\"/>\n"); break;
                case DText t: sb.Append($"<text x=\"{N(t.X)}\" y=\"{N(t.Y)}\" font-size=\"{N(t.Size)}\" fill=\"{Color(t.Color)}\" text-anchor=\"middle\" dominant-baseline=\"middle\"{(t.Bold ? " font-weight=\"600\"" : "")}>{System.Security.SecurityElement.Escape(t.Text)}</text>\n"); break;
            }
        sb.Append($"<text x=\"{N(x0)}\" y=\"{N(y1 + pad + 20)}\" font-size=\"14\" font-weight=\"700\" fill=\"#1A1A1A\">{System.Security.SecurityElement.Escape(f.Name)} – Trendo Tragwerk</text>\n</svg>\n");
        return sb.ToString();
    }

    static string? Color(string? c) => c is null ? null : c.Length == 9 ? c[..7] : c;
    static string Alpha(string? c) => c is { Length: 9 } ? (Convert.ToInt32(c[7..], 16) / 255.0).ToString("0.##", Inv) : "1";

    // ---------- CSV ----------
    public static string Csv(FrameModel f, FrameResult r)
    {
        var sb = new StringBuilder("Stab;Länge m;Querschnitt;Werkstoff;max N kN;max V kN;max M kNm;Spannung N/mm²;Sicherheit;Knicksicherheit;Durchbiegung mm;Ergebnis\n");
        var i = 0;
        foreach (var m in f.Members)
        {
            var x = r.Of(m.Id)!; i++;
            sb.Append(string.Join(';', $"S{i}", F(x.L), m.Section, m.Material, F(x.MaxN), F(x.MaxV), F(x.MaxM), F(x.Stress_MPa), F(x.Safety), x.BuckleSafety is { } b ? F(b) : "–", F(x.MaxDefl_mm), x.Ok ? "hält" : "nicht ausreichend")).Append('\n');
        }
        sb.Append("\nLager;x m;y m;Art;Rx kN;Ry kN;M kNm\n");
        foreach (var n in f.Nodes.Where(n => n.Support != ""))
        {
            var (rx, ry, rm) = r.Reactions[n.Id];
            sb.Append(string.Join(';', $"K{f.Nodes.IndexOf(n) + 1}", F(n.X), F(n.Y), SupportName(n.Support), F(rx), F(ry), F(rm))).Append('\n');
        }
        return sb.ToString();
        static string F(double v) => v.ToString("0.###", De);
    }

    public static string SupportName(string s) => s switch { "fest" => "Festlager", "los" => "Loslager", "einspannung" => "Einspannung", _ => "frei" };

    // ---------- PDF-Kurzbericht ----------
    public static string Pdf(FrameModel f, FrameResult r, string path)
    {
        Trendo.Core.Sys.PdfTools.Fonts.Ensure();
        const double mm = 72 / 25.4;
        using var pdf = new PdfDocument(); pdf.Info.Title = f.Name; pdf.Info.Creator = "Trendo Tragwerk";
        var page = pdf.AddPage(); page.Width = XUnit.FromMillimeter(210); page.Height = XUnit.FromMillimeter(297);
        using var g = XGraphics.FromPdfPage(page);
        g.DrawString(f.Name, new XFont("Arial", 16, XFontStyleEx.Bold), XBrushes.Black, 20 * mm, 22 * mm);
        g.DrawString($"Statische Vorbemessung · {DateTime.Now.ToString("dd.MM.yyyy", De)} · Trendo Tragwerk – ersetzt keine geprüfte Statik", new XFont("Arial", 8), XBrushes.DimGray, 20 * mm, 28 * mm);
        // Bild: Momentenlinie, Biegelinie, Reaktionen
        var shapes = Build(f, r, new FrameView(true, true, false, false, true, true), Paper); var (x0, y0, x1, y1) = Extent(shapes);
        var box = new XRect(20 * mm, 34 * mm, 170 * mm, 100 * mm);
        var k = Math.Min(box.Width / Math.Max(1, x1 - x0), box.Height / Math.Max(1, y1 - y0));
        XPoint Pt(double x, double y) => new(box.X + (x - x0) * k + (box.Width - (x1 - x0) * k) / 2, box.Y + (y - y0) * k + (box.Height - (y1 - y0) * k) / 2);
        static XColor C(string hex) { var c = hex.TrimStart('#'); var col = XColor.FromArgb(Convert.ToInt32(c[..2], 16), Convert.ToInt32(c[2..4], 16), Convert.ToInt32(c[4..6], 16)); return c.Length == 8 ? XColor.FromArgb(Convert.ToInt32(c[6..], 16), col) : col; }
        foreach (var sh in shapes)
            switch (sh)
            {
                case DPoly p:
                    var pts = p.Pts.Select(q => Pt(q.X, q.Y)).ToArray();
                    if (p.Closed && p.Fill is { } fl) g.DrawPolygon(new XSolidBrush(C(fl)), pts, XFillMode.Winding);
                    if (p.Stroke is { } st) { var pen = new XPen(C(st), Math.Max(0.3, p.Width * 0.35)); if (p.Closed) g.DrawPolygon(pen, pts); else g.DrawLines(pen, pts); }
                    break;
                case DLine l: g.DrawLine(new XPen(C(l.Stroke), Math.Max(0.3, l.Width * 0.35)) { DashStyle = l.Dashed ? XDashStyle.Dash : XDashStyle.Solid }, Pt(l.X1, l.Y1), Pt(l.X2, l.Y2)); break;
                case DText t:
                    var at = Pt(t.X, t.Y);
                    g.DrawString(t.Text, new XFont("Arial", Math.Clamp(t.Size * k * 0.9, 5, 9), t.Bold ? XFontStyleEx.Bold : XFontStyleEx.Regular), new XSolidBrush(C(t.Color)), new XRect(at.X - 60, at.Y - 8, 120, 16), XStringFormats.Center);
                    break;
            }
        // Tabellen
        var y = 142 * mm; var font = new XFont("Arial", 8); var bold = new XFont("Arial", 8, XFontStyleEx.Bold);
        void Row(XFont fnt, params string[] cells) { double[] w = [14, 16, 30, 32, 16, 16, 18, 18, 16]; var x = 20 * mm; for (var i = 0; i < cells.Length && i < w.Length; i++) { g.DrawString(cells[i], fnt, XBrushes.Black, x, y); x += w[i] * mm; } y += 5 * mm; }
        g.DrawString("Stäbe", new XFont("Arial", 10, XFontStyleEx.Bold), XBrushes.Black, 20 * mm, y); y += 6 * mm;
        Row(bold, "Stab", "Länge", "Querschnitt", "Werkstoff", "max M", "max N", "σ N/mm²", "Sicherheit", "f mm");
        var i2 = 0;
        foreach (var m in f.Members) { var x = r.Of(m.Id)!; i2++; Row(font, $"S{i2}", $"{x.L:0.00} m", m.Section, m.Material, $"{x.MaxM:0.##}", $"{x.MaxN:0.##}", $"{x.Stress_MPa:0.#}", $"{x.Safety:0.##}", $"{x.MaxDefl_mm:0.#}"); if (y > 270 * mm) break; }
        y += 3 * mm; g.DrawString("Lager", new XFont("Arial", 10, XFontStyleEx.Bold), XBrushes.Black, 20 * mm, y); y += 6 * mm;
        Row(bold, "Knoten", "x", "y", "Art", "Rx kN", "Ry kN", "M kNm");
        foreach (var n in f.Nodes.Where(n => n.Support != "")) { var (rx, ry, rm) = r.Reactions[n.Id]; Row(font, $"K{f.Nodes.IndexOf(n) + 1}", $"{n.X:0.##}", $"{n.Y:0.##}", SupportName(n.Support), $"{rx:0.##}", $"{ry:0.##}", $"{rm:0.##}"); if (y > 275 * mm) break; }
        y += 3 * mm;
        g.DrawString(r.Ok ? "Ergebnis: Alle Stäbe halten (Spannung ≥ 1,5-fache Sicherheit, Durchbiegung ≤ L/300, Knicken ≥ 2)." : "Ergebnis: Mindestens ein Stab reicht nicht aus – siehe Tabelle.", bold, r.Ok ? XBrushes.DarkGreen : XBrushes.DarkRed, 20 * mm, y);
        pdf.Save(path); return path;
    }
}
