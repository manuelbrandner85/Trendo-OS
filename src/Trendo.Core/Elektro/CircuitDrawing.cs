using System.Globalization;
using System.Text;
using Trendo.Core.Arch;

namespace Trendo.Core.Elektro;

/// <summary>T-0259: Schaltzeichen nach DIN EN 60617 (Widerstand als Rechteck usw.) als Zeichenelemente – Rasterpunkt = 20 Einheiten.
/// Mit Simulationsstand: Leitungen nach Spannung eingefärbt (grün +, grau 0, rot −), LEDs/Lampen leuchten, Werte an den Bauteilen.</summary>
public static class CircuitDrawing
{
    public const double Grid = 20;
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public sealed record Theme(string Bg, string Wire, string Part, string Text, string Pos, string Neg, string Zero);
    public static readonly Theme Screen = new("#0A1424", "#9FB4D3", "#E6EDF7", "#F2F6FC", "#4ADE80", "#F87171", "#8A99B3");
    public static readonly Theme Paper = new("#FFFFFF", "#1A1A1A", "#1A1A1A", "#1A1A1A", "#1A1A1A", "#1A1A1A", "#1A1A1A");

    public static string VoltColor(double v, double max, Theme t)
    {
        if (t == Paper || max < 1e-9) return t.Wire;
        var k = Math.Clamp(Math.Abs(v) / max, 0, 1);
        var (r0, g0, b0) = (0x8A, 0x99, 0xB3); var (r1, g1, b1) = v >= 0 ? (0x4A, 0xDE, 0x80) : (0xF8, 0x71, 0x71);
        int L(int a, int b) => (int)(a + (b - a) * k);
        return $"#{L(r0, r1):X2}{L(g0, g1):X2}{L(b0, b1):X2}";
    }

    public static List<Shape> Build(Circuit c, SimState? s, Theme t, bool values = true, string? selected = null)
    {
        var sh = new List<Shape>();
        var net = Netlist.Build(c);
        var maxV = s is null ? 0 : s.NodeV.Select(Math.Abs).DefaultIfEmpty(0).Max();
        string Col(int x, int y) => s is null ? t.Wire : VoltColor(s.NodeV[Math.Max(0, net.NodeOf(x, y))], maxV, t);
        foreach (var p in c.Parts)
        {
            double x1 = p.X1 * Grid, y1 = p.Y1 * Grid, x2 = p.X2 * Grid, y2 = p.Y2 * Grid;
            var len = Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1)); var ux = len > 0 ? (x2 - x1) / len : 1; var uy = len > 0 ? (y2 - y1) / len : 0; var nx = -uy; var ny = ux;
            (double, double) At(double along, double side) => (x1 + ux * (len / 2 + along) + nx * side, y1 + uy * (len / 2 + along) + ny * side);
            var c1 = Col(p.X1, p.Y1); var c2 = Col(p.X2, p.Y2);
            void Lead(double body) { var (ax, ay) = At(-body, 0); var (bx, by) = At(body, 0); sh.Add(new DLine(x1, y1, ax, ay, c1, 2.2, "DRAHT")); sh.Add(new DLine(bx, by, x2, y2, c2, 2.2, "DRAHT")); }
            void Poly(string fill, string stroke, double w, params (double A, double S)[] pts) => sh.Add(new DPoly(pts.Select(q => At(q.A, q.S)).ToList(), fill == "" ? null : fill, stroke, w, "BAUTEIL", p.Id));
            void Ln(double a1, double s1, double a2, double s2, string color, double w = 2) { var (ax, ay) = At(a1, s1); var (bx, by) = At(a2, s2); sh.Add(new DLine(ax, ay, bx, by, color, w, "BAUTEIL")); }
            var res = s is not null && s.Parts.TryGetValue(p.Id, out var r) ? r : ((double V, double I)?)null;
            switch (p.Kind)
            {
                case "draht":
                    if (c1 == c2) sh.Add(new DPoly([(x1, y1), (x2, y2)], null, c1, 2.2, "DRAHT", p.Id, false));
                    else { sh.Add(new DLine(x1, y1, (x1 + x2) / 2, (y1 + y2) / 2, c1, 2.2, "DRAHT")); sh.Add(new DLine((x1 + x2) / 2, (y1 + y2) / 2, x2, y2, c2, 2.2, "DRAHT")); sh.Add(new DPoly([(x1, y1), (x2, y2)], null, null, 0, "DRAHT", p.Id, false)); }
                    break;
                case "widerstand":
                    Lead(16); Poly(t.Bg, t.Part, 2, (-16, -6), (16, -6), (16, 6), (-16, 6)); break;
                case "lampe":
                    {
                        Lead(11); var glow = res is { } lr ? Math.Clamp(Math.Abs(lr.V * lr.I) / 2, 0, 1) : 0;
                        var (cx, cy) = At(0, 0); var circ = Enumerable.Range(0, 20).Select(k => (cx + Math.Cos(k * Math.PI / 10) * 11, cy + Math.Sin(k * Math.PI / 10) * 11)).ToList();
                        sh.Add(new DPoly(circ, glow > 0.02 ? "#FDE047" + ((int)(glow * 220 + 30)).ToString("X2") : t.Bg, t.Part, 2, "BAUTEIL", p.Id));
                        Ln(-7.8, -7.8, 7.8, 7.8, t.Part, 1.6); Ln(-7.8, 7.8, 7.8, -7.8, t.Part, 1.6); break;
                    }
                case "kondensator":
                    Lead(4); Ln(-4, -12, -4, 12, t.Part, 3); Ln(4, -12, 4, 12, t.Part, 3); break;
                case "spule":
                    Lead(18); Poly(t.Part, t.Part, 1, (-18, -5), (18, -5), (18, 5), (-18, 5)); break; // DIN: gefülltes Rechteck
                case "spannung":
                    Lead(4); Ln(-4, -14, -4, 14, t.Part, 2); Ln(4, -7, 4, 7, t.Part, 5);
                    { var (px, py) = At(14, -14); sh.Add(new DText(px, py, "+", 12, t.Text, "BAUTEIL", true)); } break;
                case "wechsel":
                    {
                        Lead(13); var (cx, cy) = At(0, 0);
                        sh.Add(new DPoly(Enumerable.Range(0, 24).Select(k => (cx + Math.Cos(k * Math.PI / 12) * 13, cy + Math.Sin(k * Math.PI / 12) * 13)).ToList(), t.Bg, t.Part, 2, "BAUTEIL", p.Id));
                        sh.Add(new DPoly(Enumerable.Range(0, 13).Select(k => { var a = -8 + k * 16.0 / 12; return At(a, -Math.Sin(a / 8 * Math.PI) * 5); }).ToList(), null, t.Part, 1.8, "BAUTEIL", null, false));
                        break;
                    }
                case "masse":
                    sh.Add(new DLine(x1, y1, x1, y1 + 8, c1, 2.2, "DRAHT"));
                    sh.Add(new DLine(x1 - 12, y1 + 8, x1 + 12, y1 + 8, t.Part, 3, "BAUTEIL"));
                    sh.Add(new DPoly([(x1 - 12, y1), (x1 + 12, y1), (x1 + 12, y1 + 12), (x1 - 12, y1 + 12)], null, null, 0, "BAUTEIL", p.Id));
                    break;
                case "schalter":
                    Lead(12); Ln(-12, 0, 12, p.Closed ? -2 : -14, t.Part, 2.4);
                    { var (ax, ay) = At(-12, 0); sh.Add(new DPoly(Enumerable.Range(0, 10).Select(k => (ax + Math.Cos(k * Math.PI / 5) * 2.5, ay + Math.Sin(k * Math.PI / 5) * 2.5)).ToList(), t.Part, null, 0, "BAUTEIL", p.Id)); }
                    break;
                case "diode": case "led":
                    {
                        Lead(9);
                        var on = res is { } dr ? Math.Clamp(dr.I / 0.02, 0, 1) : 0;
                        var col = p.Color switch { "gruen" => "#22C55E", "blau" => "#3B82F6", "weiss" => "#F8FAFC", "gelb" => "#FACC15", _ => "#EF4444" };
                        if (p.Kind == "led" && on > 0.03)
                        {
                            var (cx, cy) = At(0, 0);
                            sh.Add(new DPoly(Enumerable.Range(0, 24).Select(k => (cx + Math.Cos(k * Math.PI / 12) * 22, cy + Math.Sin(k * Math.PI / 12) * 22)).ToList(), col + ((int)(on * 120)).ToString("X2"), null, 0, "BAUTEIL"));
                        }
                        Poly(p.Kind == "led" ? (on > 0.03 ? col : t.Bg) : t.Part, t.Part, 2, (-9, -9), (9, 0), (-9, 9)); Ln(9, -9, 9, 9, t.Part, 2.4);
                        if (p.Kind == "led") { Ln(2, -12, 8, -20, t.Part, 1.4); Ln(6, -10, 12, -18, t.Part, 1.4); }
                        break;
                    }
            }
            // Beschriftung: waagrecht über dem Bauteil, senkrecht rechts daneben; Messwerte nur am ausgewählten Bauteil
            if (values && p.Kind is not ("draht" or "masse"))
            {
                var label = p.Name + (PartTypes.Format(p) is { Length: > 0 } f && p.Kind is not ("diode" or "led") ? " " + f : "");
                var vertical = Math.Abs(uy) > Math.Abs(ux);
                var (mx, my) = At(0, 0);
                var (tx, ty) = vertical ? (mx + 22 + label.Length * 3.1, my - 8) : (mx, my - 24);
                sh.Add(new DText(tx, ty, label, 11, t.Text, "TEXT", true));
                if (res is { } rr && s is not null && p.Kind is not "schalter" && p.Id == selected)
                {
                    var vt = $"{ElectronicsPlus.Fmt(Math.Abs(rr.V), "V")} · {ElectronicsPlus.Fmt(Math.Abs(rr.I), "A")}";
                    var (vx, vy) = vertical ? (mx + 22 + vt.Length * 2.9, my + 9) : (mx, my + 24);
                    sh.Add(new DText(vx, vy, vt, 10, t == Paper ? t.Text : "#7FD3FF", "WERTE"));
                }
            }
        }
        // Lötpunkte: wo drei oder mehr Anschlüsse zusammentreffen
        var ends = c.Parts.SelectMany(p => p.Kind == "masse" ? [(p.X1, p.Y1)] : new[] { (p.X1, p.Y1), (p.X2, p.Y2) }).GroupBy(q => q).Where(g => g.Count() >= 3);
        foreach (var g in ends) { var (x, y) = (g.Key.Item1 * Grid, g.Key.Item2 * Grid); sh.Add(new DPoly(Enumerable.Range(0, 10).Select(k => (x + Math.Cos(k * Math.PI / 5) * 3.5, y + Math.Sin(k * Math.PI / 5) * 3.5)).ToList(), Col(g.Key.Item1, g.Key.Item2), null, 0, "DRAHT")); }
        return sh;
    }

    public static string Svg(Circuit c)
    {
        var shapes = Build(c, null, Paper); var (x0, y0, x1, y1) = Trendo.Core.Statik.FrameDrawing.Extent(shapes); const double pad = 40;
        string N(double d) => d.ToString("0.##", Inv);
        var sb = new StringBuilder($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"{N(x0 - pad)} {N(y0 - pad)} {N(x1 - x0 + 2 * pad)} {N(y1 - y0 + 2 * pad + 30)}\" font-family=\"Segoe UI, Arial, sans-serif\">\n<rect x=\"{N(x0 - pad)}\" y=\"{N(y0 - pad)}\" width=\"{N(x1 - x0 + 2 * pad)}\" height=\"{N(y1 - y0 + 2 * pad + 30)}\" fill=\"#FFFFFF\"/>\n");
        foreach (var s in shapes)
            switch (s)
            {
                case DPoly p when p.Stroke is not null || p.Fill is not null:
                    sb.Append($"<{(p.Closed ? "polygon" : "polyline")} points=\"{string.Join(' ', p.Pts.Select(q => N(q.X) + "," + N(q.Y)))}\" fill=\"{(p.Fill is { } f ? f[..7] : "none")}\" stroke=\"{p.Stroke ?? "none"}\" stroke-width=\"{N(p.Width)}\"/>\n"); break;
                case DLine l: sb.Append($"<line x1=\"{N(l.X1)}\" y1=\"{N(l.Y1)}\" x2=\"{N(l.X2)}\" y2=\"{N(l.Y2)}\" stroke=\"{l.Stroke}\" stroke-width=\"{N(l.Width)}\" stroke-linecap=\"round\"/>\n"); break;
                case DText t: sb.Append($"<text x=\"{N(t.X)}\" y=\"{N(t.Y)}\" font-size=\"{N(t.Size)}\" text-anchor=\"middle\" dominant-baseline=\"middle\" fill=\"{t.Color}\"{(t.Bold ? " font-weight=\"600\"" : "")}>{System.Security.SecurityElement.Escape(t.Text)}</text>\n"); break;
            }
        sb.Append($"<text x=\"{N(x0)}\" y=\"{N(y1 + pad + 10)}\" font-size=\"14\" font-weight=\"700\">{System.Security.SecurityElement.Escape(c.Name)} – Trendo Schaltung</text>\n</svg>\n");
        return sb.ToString();
    }
}
