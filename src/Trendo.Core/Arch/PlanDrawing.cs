using System.Globalization;
using System.Text;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace Trendo.Core.Arch;

/// <summary>T-0256: Ein Grundriss wird einmal in Zeichenelemente übersetzt – daraus entstehen Bildschirm, SVG, DXF und PDF gleich aussehend.
/// Koordinaten in cm, Schriftgrößen in cm (Papier 1:50: 0,25 cm ≈ 3,5 pt).</summary>
public abstract record Shape(string Layer, string? Id);
public sealed record DPoly(IReadOnlyList<(double X, double Y)> Pts, string? Fill, string? Stroke, double Width, string Layer, string? Id = null, bool Closed = true) : Shape(Layer, Id);
public sealed record DLine(double X1, double Y1, double X2, double Y2, string Stroke, double Width, string Layer, bool Dashed = false) : Shape(Layer, null);
public sealed record DArc(double Cx, double Cy, double R, double StartDeg, double SweepDeg, string Stroke, double Width, string Layer) : Shape(Layer, null);
/// <summary>Line: −1 obere / +1 untere Zeile einer zweizeiligen Beschriftung um (X, Y) – der Abstand wächst mit der Schriftgröße.</summary>
public sealed record DText(double X, double Y, string Text, double Size, string Color, string Layer, bool Bold = false, double AngleDeg = 0, int Line = 0) : Shape(Layer, null)
{
    public double LineY(double size) => Y + Line * size * 0.62;
}

public sealed record PlanTheme(string Background, string Wall, string WallEdge, string Text, string Dim, string Furniture, string FurnitureEdge, string Door, string Window, IReadOnlyDictionary<string, string> Floors)
{
    public static readonly PlanTheme Screen = new("#0D1A2D", "#DCE6F4", "#9FB4D3", "#F2F6FC", "#F5A524", "#1F3150", "#8CC8FF", "#BFD8FF", "#7FD3FF",
        new Dictionary<string, string> { ["laminat"] = "#5A4A33", ["parkett"] = "#634528", ["fliesen"] = "#2E4A61", ["vinyl"] = "#3E4A3C", ["teppich"] = "#4A3F62" });
    public static readonly PlanTheme Paper = new("#FFFFFF", "#2B2B2B", "#2B2B2B", "#1A1A1A", "#7A5A00", "#FFFFFF", "#555555", "#444444", "#1E6FB8",
        new Dictionary<string, string> { ["laminat"] = "#F3E7D3", ["parkett"] = "#EBD8BE", ["fliesen"] = "#E1ECF4", ["vinyl"] = "#E7EEE3", ["teppich"] = "#ECE6F3" });
}

public static class PlanDrawing
{
    static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static List<Shape> Build(FloorPlan p, PlanTheme t, bool dims = true, bool furniture = true)
    {
        var s = new List<Shape>();
        // Räume (Boden)
        foreach (var r in p.Rooms)
            s.Add(new DPoly(r.Points.Select(q => (q.X, q.Y)).ToList(), t.Floors.GetValueOrDefault(r.Floor, t.Floors["laminat"]), null, 0, "RAEUME", r.Id));
        // Möbel unter den Wänden
        if (furniture)
            foreach (var i in p.Items)
            {
                s.Add(new DPoly(i.Corners(), i.Type == "teppich" ? null : t.Furniture, t.FurnitureEdge, i.Type == "teppich" ? 0.8 : 1.2, "MOEBEL", i.Id));
                Symbol(s, i, t);
            }
        // Wände: Umriss gefüllt, an Anschlüssen um die halbe Stärke verlängert (saubere Ecken)
        foreach (var w in p.Walls)
        {
            var (ux, uy) = w.Dir; var h = w.Thickness / 2;
            bool Joined(double x, double y) => p.Walls.Any(o => o != w && (Math.Abs(o.X1 - x) < 0.5 && Math.Abs(o.Y1 - y) < 0.5 || Math.Abs(o.X2 - x) < 0.5 && Math.Abs(o.Y2 - y) < 0.5));
            var e1 = Joined(w.X1, w.Y1) ? h : 0; var e2 = Joined(w.X2, w.Y2) ? h : 0;
            var ext = new Wall { X1 = w.X1 - ux * e1, Y1 = w.Y1 - uy * e1, X2 = w.X2 + ux * e2, Y2 = w.Y2 + uy * e2, Thickness = w.Thickness };
            s.Add(new DPoly(ext.Outline(), t.Wall, null, 0, "WAENDE", w.Id));
        }
        // Öffnungen: Lücke in der Wand, Tür mit Blatt und Schwenkbogen, Fenster mit Rahmen und Glas
        foreach (var o in p.Openings)
        {
            if (p.WallOf(o.WallId) is not { } w) continue;
            var (ux, uy) = w.Dir; var (nx, ny) = (-uy, ux); var h = w.Thickness / 2 + 0.6;
            var (cx, cy) = w.PointAt(o.At); var a = (X: cx - ux * o.Width / 2, Y: cy - uy * o.Width / 2); var b = (X: cx + ux * o.Width / 2, Y: cy + uy * o.Width / 2);
            s.Add(new DPoly([(a.X + nx * h, a.Y + ny * h), (b.X + nx * h, b.Y + ny * h), (b.X - nx * h, b.Y - ny * h), (a.X - nx * h, a.Y - ny * h)], t.Background, null, 0, "OEFFNUNGEN", o.Id));
            var hw = w.Thickness / 2;
            if (o.Kind == "fenster")
            {
                s.Add(new DPoly([(a.X + nx * hw, a.Y + ny * hw), (b.X + nx * hw, b.Y + ny * hw), (b.X - nx * hw, b.Y - ny * hw), (a.X - nx * hw, a.Y - ny * hw)], null, t.Window, 1.2, "OEFFNUNGEN", o.Id));
                s.Add(new DLine(a.X, a.Y, b.X, b.Y, t.Window, 1.2, "OEFFNUNGEN"));
                s.Add(new DLine(a.X + nx * hw / 3, a.Y + ny * hw / 3, b.X + nx * hw / 3, b.Y + ny * hw / 3, t.Window, 0.8, "OEFFNUNGEN"));
            }
            else
            {
                var side = o.Inward ? 1 : -1;
                var hinge = o.HingeLeft ? a : b; var other = o.HingeLeft ? b : a;
                var face = (X: hinge.X + nx * side * hw, Y: hinge.Y + ny * side * hw);
                var leaf = (X: face.X + nx * side * o.Width, Y: face.Y + ny * side * o.Width);
                s.Add(new DLine(face.X, face.Y, leaf.X, leaf.Y, t.Door, 1.6, "OEFFNUNGEN"));
                var start = Math.Atan2(leaf.Y - face.Y, leaf.X - face.X) * 180 / Math.PI;
                var end = Math.Atan2(other.Y + ny * side * hw - face.Y, other.X + nx * side * hw - face.X) * 180 / Math.PI;
                var sweep = end - start; while (sweep > 180) sweep -= 360; while (sweep < -180) sweep += 360;
                s.Add(new DArc(face.X, face.Y, o.Width, start, sweep, t.Door, 0.8, "OEFFNUNGEN"));
            }
        }
        // Beschriftung der Räume: Name und Fläche
        foreach (var r in p.Rooms)
        {
            var (cx, cy) = r.Center; var area = r.Area / 10000;
            var size = Math.Clamp(Math.Sqrt(r.Area) / 22, 9, 18);
            s.Add(new DText(cx, cy, r.Name, size, t.Text, "TEXT", true, 0, -1));
            s.Add(new DText(cx, cy, area.ToString("0.00", De) + " m²", size * 0.8, t.Text, "TEXT", false, 0, 1));
        }
        // T-0257: freie Bemaßungen
        foreach (var d in p.Dimensions) Dim(s, d.X1, d.Y1, d.X2, d.Y2, d.Offset, t);
        // T-0257: Texte
        foreach (var l in p.Labels) s.Add(new DText(l.X, l.Y, l.Text, l.Size, t.Text, "TEXT", false, l.Rot));
        // Bemaßung: Wandlängen außen neben der Wand
        if (dims && p.Bounds() is { } bb)
        {
            var mid = ((bb.MinX + bb.MaxX) / 2, (bb.MinY + bb.MaxY) / 2);
            bool InRoom(double x, double y) => p.Rooms.Any(r => Geo.Inside(r.Points.Select(q => (q.X, q.Y)).ToList(), x, y));
            foreach (var w in p.Walls.Where(w => w.Length >= 40))
            {
                var (ux, uy) = w.Dir; var (nx, ny) = (-uy, ux);
                var (mx, my) = w.PointAt(w.Length / 2);
                var probe = w.Thickness / 2 + 15;
                if (p.Rooms.Count > 0 && InRoom(mx + nx * probe, my + ny * probe) && InRoom(mx - nx * probe, my - ny * probe)) continue; // Innenwand: keine Außenmaßkette
                if ((mx - mid.Item1) * nx + (my - mid.Item2) * ny < 0) { nx = -nx; ny = -ny; } // nach außen
                var off = w.Thickness / 2 + 28;
                double ax = w.X1 + nx * off, ay = w.Y1 + ny * off, bx = w.X2 + nx * off, by = w.Y2 + ny * off;
                s.Add(new DLine(ax, ay, bx, by, t.Dim, 0.8, "MASSE"));
                foreach (var (x, y) in new[] { (ax, ay), (bx, by) })
                {
                    s.Add(new DLine(x - nx * 10, y - ny * 10, x + nx * 6, y + ny * 6, t.Dim, 0.8, "MASSE"));
                    s.Add(new DLine(x - (ux + nx) * 5, y - (uy + ny) * 5, x + (ux + nx) * 5, y + (uy + ny) * 5, t.Dim, 1.2, "MASSE"));
                }
                var ang = Math.Atan2(uy, ux) * 180 / Math.PI; if (ang > 90 || ang < -90) ang += 180;
                s.Add(new DText((ax + bx) / 2 + nx * 9, (ay + by) / 2 + ny * 9, (w.Length / 100).ToString("0.00", De), 10, t.Dim, "MASSE", false, ang));
            }
        }
        return s;
    }

    /// <summary>Maßkette zwischen zwei Punkten, um <paramref name="offset"/> versetzt: Hilfslinien, Schrägstriche, Länge in m.</summary>
    static void Dim(List<Shape> s, double x1, double y1, double x2, double y2, double offset, PlanTheme t)
    {
        var len = Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1)); if (len < 1) return;
        var ux = (x2 - x1) / len; var uy = (y2 - y1) / len; var nx = -uy; var ny = ux;
        double ax = x1 + nx * offset, ay = y1 + ny * offset, bx = x2 + nx * offset, by = y2 + ny * offset;
        var sg = offset >= 0 ? 1 : -1;
        s.Add(new DLine(ax, ay, bx, by, t.Dim, 0.8, "MASSE"));
        foreach (var (px, py, qx, qy) in new[] { (x1, y1, ax, ay), (x2, y2, bx, by) })
        {
            if (Math.Abs(offset) > 2) s.Add(new DLine(px + nx * sg * 4, py + ny * sg * 4, qx + nx * sg * 6, qy + ny * sg * 6, t.Dim, 0.6, "MASSE"));
            s.Add(new DLine(qx - (ux + nx) * 5, qy - (uy + ny) * 5, qx + (ux + nx) * 5, qy + (uy + ny) * 5, t.Dim, 1.2, "MASSE"));
        }
        var ang = Math.Atan2(uy, ux) * 180 / Math.PI; if (ang > 90 || ang < -90) ang += 180;
        s.Add(new DText((ax + bx) / 2 + nx * sg * 9, (ay + by) / 2 + ny * sg * 9, (len / 100).ToString("0.00", De), 10, t.Dim, "MASSE", false, ang));
    }

    /// <summary>Einfache Grundriss-Symbole, damit man Möbel erkennt (Kissen am Bett, Becken am WC …).</summary>
    static void Symbol(List<Shape> s, Item i, PlanTheme t)
    {
        var a = i.Rot * Math.PI / 180; var c = Math.Cos(a); var sn = Math.Sin(a); var m = i.Mirror ? -1 : 1;
        (double, double) P(double dx, double dy) { dx *= m; return (i.X + dx * c - dy * sn, i.Y + dx * sn + dy * c); }
        void L(double x1, double y1, double x2, double y2) { var p = P(x1, y1); var q = P(x2, y2); s.Add(new DLine(p.Item1, p.Item2, q.Item1, q.Item2, t.FurnitureEdge, 0.8, "MOEBEL")); }
        void R(double x1, double y1, double x2, double y2) => s.Add(new DPoly([P(x1, y1), P(x2, y1), P(x2, y2), P(x1, y2)], null, t.FurnitureEdge, 0.8, "MOEBEL"));
        double w = i.W / 2, d = i.D / 2;
        switch (i.Type)
        {
            case "bett": case "doppelbett":
                var pillows = i.Type == "bett" ? 1 : 2; var pw = (i.W - 20) / pillows;
                for (var k = 0; k < pillows; k++) R(-w + 10 + k * pw + 3, -d + 8, -w + 10 + (k + 1) * pw - 3, -d + 38);
                L(-w, -d + 50, w, -d + 50); break;
            case "sofa": R(-w, -d, w, -d + 20); R(-w, -d, -w + 18, d); R(w - 18, -d, w, d); L(-w + 18 + (i.W - 36) / 3, -d + 20, -w + 18 + (i.W - 36) / 3, d); L(w - 18 - (i.W - 36) / 3, -d + 20, w - 18 - (i.W - 36) / 3, d); break;
            case "sessel": R(-w, -d, w, -d + 18); R(-w, -d, -w + 14, d); R(w - 14, -d, w, d); break;
            case "wc": R(-w + 2, -d, w - 2, -d + 18); s.Add(new DPoly(Enumerable.Range(0, 24).Select(k => { var ang = k * Math.PI / 12; return P(Math.Cos(ang) * (w - 4), -d + 18 + (d * 2 - 22) / 2 + Math.Sin(ang) * (d * 2 - 22) / 2); }).ToList(), null, t.FurnitureEdge, 0.8, "MOEBEL")); break;
            case "waschbecken": case "spuele":
                s.Add(new DPoly(Enumerable.Range(0, 24).Select(k => { var ang = k * Math.PI / 12; return P(Math.Cos(ang) * (w - 8), Math.Sin(ang) * (d - 8) + 3); }).ToList(), null, t.FurnitureEdge, 0.8, "MOEBEL")); break;
            case "badewanne": R(-w + 6, -d + 6, w - 6, d - 6); break;
            case "dusche": L(-w, -d, w, d); L(w, -d, -w, d); break;
            case "herd": foreach (var (x, y) in new[] { (-w / 2, -d / 2), (w / 2, -d / 2), (-w / 2, d / 2), (w / 2, d / 2) }) s.Add(new DPoly(Enumerable.Range(0, 16).Select(k => P(x + Math.Cos(k * Math.PI / 8) * 9, y + Math.Sin(k * Math.PI / 8) * 9)).ToList(), null, t.FurnitureEdge, 0.8, "MOEBEL")); break;
            case "esstisch": foreach (var k in new[] { -0.25, 0.25 }) { R(i.W * k - 22, -d - 34, i.W * k + 22, -d - 4); R(i.W * k - 22, d + 4, i.W * k + 22, d + 34); } break;
            case "schrank": case "kuechenzeile": L(-w, d - 6, w, d - 6); break;
            case "treppe":
                var steps = Math.Max(2, (int)Math.Round(i.D / 26));
                for (var k = 1; k < steps; k++) L(-w, -d + i.D * k / steps, w, -d + i.D * k / steps);
                L(0, d - 10, 0, -d + 10); L(0, -d + 10, -8, -d + 24); L(0, -d + 10, 8, -d + 24); break;
            case "pflanze": s.Add(new DPoly(Enumerable.Range(0, 16).Select(k => P(Math.Cos(k * Math.PI / 8) * w * 0.8, Math.Sin(k * Math.PI / 8) * d * 0.8)).ToList(), null, t.FurnitureEdge, 0.8, "MOEBEL")); break;
        }
        if (i.W >= 60 && i.D >= 40 && i.Type is not ("teppich" or "wc" or "waschbecken"))
        {
            var (tx, ty) = P(0, i.Type is "bett" or "doppelbett" ? d * 0.35 : 0);
            s.Add(new DText(tx, ty, i.Name, 7.5, t.FurnitureEdge, "MOEBEL"));
        }
    }

    // ---------- SVG ----------
    public static string Svg(FloorPlan p)
    {
        var t = PlanTheme.Paper; var shapes = Build(p, t);
        var (minX, minY, maxX, maxY) = p.Bounds() ?? (0, 0, 100, 100);
        const double pad = 90; double x0 = minX - pad, y0 = minY - pad, w = maxX - minX + 2 * pad, h = maxY - minY + 2 * pad + 50;
        string N(double v) => v.ToString("0.##", Inv);
        var sb = new StringBuilder();
        sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"{N(x0)} {N(y0)} {N(w)} {N(h)}\" width=\"{N(w * 2)}\" height=\"{N(h * 2)}\" font-family=\"Segoe UI, Arial, sans-serif\">\n");
        sb.Append($"<rect x=\"{N(x0)}\" y=\"{N(y0)}\" width=\"{N(w)}\" height=\"{N(h)}\" fill=\"{t.Background}\"/>\n");
        foreach (var sh in shapes)
            switch (sh)
            {
                case DPoly q:
                    sb.Append($"<{(q.Closed ? "polygon" : "polyline")} points=\"{string.Join(' ', q.Pts.Select(v => N(v.X) + "," + N(v.Y)))}\" fill=\"{q.Fill ?? "none"}\" stroke=\"{q.Stroke ?? "none"}\" stroke-width=\"{N(q.Width)}\"/>\n"); break;
                case DLine l: sb.Append($"<line x1=\"{N(l.X1)}\" y1=\"{N(l.Y1)}\" x2=\"{N(l.X2)}\" y2=\"{N(l.Y2)}\" stroke=\"{l.Stroke}\" stroke-width=\"{N(l.Width)}\"{(l.Dashed ? " stroke-dasharray=\"6 4\"" : "")}/>\n"); break;
                case DArc a:
                    var (sx, sy) = (a.Cx + a.R * Math.Cos(a.StartDeg * Math.PI / 180), a.Cy + a.R * Math.Sin(a.StartDeg * Math.PI / 180));
                    var (ex, ey) = (a.Cx + a.R * Math.Cos((a.StartDeg + a.SweepDeg) * Math.PI / 180), a.Cy + a.R * Math.Sin((a.StartDeg + a.SweepDeg) * Math.PI / 180));
                    sb.Append($"<path d=\"M{N(sx)},{N(sy)} A{N(a.R)},{N(a.R)} 0 0 {(a.SweepDeg > 0 ? 1 : 0)} {N(ex)},{N(ey)}\" fill=\"none\" stroke=\"{a.Stroke}\" stroke-width=\"{N(a.Width)}\" stroke-dasharray=\"4 3\"/>\n"); break;
                case DText x:
                    sb.Append($"<text x=\"{N(x.X)}\" y=\"{N(x.LineY(x.Size))}\" font-size=\"{N(x.Size)}\" fill=\"{x.Color}\" text-anchor=\"middle\" dominant-baseline=\"middle\"{(x.Bold ? " font-weight=\"600\"" : "")}{(x.AngleDeg != 0 ? $" transform=\"rotate({N(x.AngleDeg)} {N(x.X)} {N(x.Y)})\"" : "")}>{System.Security.SecurityElement.Escape(x.Text)}</text>\n"); break;
            }
        // Plankopf: Name, Fläche, Maßstabsleiste 1 m
        var fy = maxY + pad - 10;
        sb.Append($"<text x=\"{N(minX)}\" y=\"{N(fy)}\" font-size=\"16\" font-weight=\"700\" fill=\"#1A1A1A\">{System.Security.SecurityElement.Escape(p.LevelName == "Erdgeschoss" ? p.Name : p.Name + " – " + p.LevelName)}</text>\n");
        sb.Append($"<text x=\"{N(minX)}\" y=\"{N(fy + 22)}\" font-size=\"11\" fill=\"#444\">Wohnfläche (WoFlV) {p.LivingArea().ToString("0.00", De)} m² · {p.Rooms.Count} Räume · Maße in m · {DateTime.Now.ToString("dd.MM.yyyy", De)} · Trendo Grundriss</text>\n");
        sb.Append($"<rect x=\"{N(maxX - 100)}\" y=\"{N(fy - 6)}\" width=\"50\" height=\"6\" fill=\"#1A1A1A\"/><rect x=\"{N(maxX - 50)}\" y=\"{N(fy - 6)}\" width=\"50\" height=\"6\" fill=\"#FFF\" stroke=\"#1A1A1A\"/>");
        sb.Append($"<text x=\"{N(maxX - 50)}\" y=\"{N(fy + 12)}\" font-size=\"10\" text-anchor=\"middle\" fill=\"#1A1A1A\">1 m</text>\n</svg>\n");
        return sb.ToString();
    }

    // ---------- DXF (R12, Einheiten cm, Ebenen je Bauteil) ----------
    public static string Dxf(FloorPlan p)
    {
        var shapes = Build(p, PlanTheme.Paper);
        var sb = new StringBuilder();
        void G(int code, object v) => sb.Append(code).Append('\n').Append(Convert.ToString(v, Inv)).Append('\n');
        string N(double v) => v.ToString("0.###", Inv);
        G(0, "SECTION"); G(2, "HEADER"); G(9, "$ACADVER"); G(1, "AC1009"); G(9, "$INSUNITS"); G(70, 5); G(0, "ENDSEC");
        var layers = new (string Name, int Color)[] { ("WAENDE", 7), ("OEFFNUNGEN", 5), ("RAEUME", 8), ("MOEBEL", 3), ("TEXT", 7), ("MASSE", 2) };
        G(0, "SECTION"); G(2, "TABLES"); G(0, "TABLE"); G(2, "LAYER"); G(70, layers.Length);
        foreach (var (n, c) in layers) { G(0, "LAYER"); G(2, n); G(70, 0); G(62, c); G(6, "CONTINUOUS"); }
        G(0, "ENDTAB"); G(0, "ENDSEC");
        G(0, "SECTION"); G(2, "ENTITIES");
        // y nach oben (CAD-Konvention): an der x-Achse spiegeln
        void Line(double x1, double y1, double x2, double y2, string layer) { G(0, "LINE"); G(8, layer); G(10, N(x1)); G(20, N(-y1)); G(30, 0); G(11, N(x2)); G(21, N(-y2)); G(31, 0); }
        foreach (var sh in shapes)
            switch (sh)
            {
                case DPoly q when q.Layer == "WAENDE" || q.Layer == "OEFFNUNGEN" && q.Stroke is null: break; // Wände kommen unten sauber unterbrochen
                case DPoly q:
                    for (var i = 0; i < q.Pts.Count - (q.Closed ? 0 : 1); i++) { var a = q.Pts[i]; var b = q.Pts[(i + 1) % q.Pts.Count]; Line(a.X, a.Y, b.X, b.Y, q.Layer); }
                    break;
                case DLine l: Line(l.X1, l.Y1, l.X2, l.Y2, l.Layer); break;
                case DArc a:
                    var s0 = -(a.StartDeg + a.SweepDeg); var s1 = -a.StartDeg; if (a.SweepDeg < 0) (s0, s1) = (-a.StartDeg, -(a.StartDeg + a.SweepDeg));
                    G(0, "ARC"); G(8, a.Layer); G(10, N(a.Cx)); G(20, N(-a.Cy)); G(30, 0); G(40, N(a.R)); G(50, N(s0)); G(51, N(s1)); break;
                case DText x: G(0, "TEXT"); G(8, x.Layer); G(10, N(x.X)); G(20, N(-x.LineY(x.Size))); G(30, 0); G(40, N(x.Size)); G(1, x.Text); G(50, N(-x.AngleDeg)); G(72, 1); G(11, N(x.X)); G(21, N(-x.LineY(x.Size))); G(31, 0); break;
            }
        // Wände: beide Kanten, an Türen und Fenstern unterbrochen, mit Leibungen; freie Enden geschlossen
        foreach (var w in p.Walls)
        {
            var len = w.Length; if (len < 1) continue;
            var (ux, uy) = w.Dir; var (nx, ny) = (-uy, ux); var h = w.Thickness / 2;
            bool Joined(double x, double y) => p.Walls.Any(o => o != w && (Math.Abs(o.X1 - x) < 0.5 && Math.Abs(o.Y1 - y) < 0.5 || Math.Abs(o.X2 - x) < 0.5 && Math.Abs(o.Y2 - y) < 0.5));
            var e1 = Joined(w.X1, w.Y1) ? h : 0; var e2 = Joined(w.X2, w.Y2) ? h : 0;
            var gaps = p.Openings.Where(o => o.WallId == w.Id).Select(o => (S: o.At - o.Width / 2, E: o.At + o.Width / 2)).OrderBy(g => g.S).ToList();
            var parts = new List<(double S, double E)>(); var pos = -e1;
            foreach (var (gs, ge) in gaps) { if (gs > pos) parts.Add((pos, gs)); pos = Math.Max(pos, ge); }
            if (len + e2 > pos) parts.Add((pos, len + e2));
            (double, double) At(double t, double side) => (w.X1 + ux * t + nx * side, w.Y1 + uy * t + ny * side);
            foreach (var (ps, pe) in parts) foreach (var side in new[] { h, -h }) { var a0 = At(ps, side); var a1 = At(pe, side); Line(a0.Item1, a0.Item2, a1.Item1, a1.Item2, "WAENDE"); }
            foreach (var (gs, ge) in gaps) foreach (var t0 in new[] { gs, ge }) { var a0 = At(t0, h); var a1 = At(t0, -h); Line(a0.Item1, a0.Item2, a1.Item1, a1.Item2, "WAENDE"); }
            if (e1 == 0) { var a0 = At(0, h); var a1 = At(0, -h); Line(a0.Item1, a0.Item2, a1.Item1, a1.Item2, "WAENDE"); }
            if (e2 == 0) { var a0 = At(len, h); var a1 = At(len, -h); Line(a0.Item1, a0.Item2, a1.Item1, a1.Item2, "WAENDE"); }
        }
        G(0, "ENDSEC"); G(0, "EOF");
        return sb.ToString();
    }

    // ---------- PDF (A4 oder A3 quer, Maßstab 1:50 oder 1:100) ----------
    public static string Pdf(FloorPlan p, string path) => Pdf([p], path);

    /// <summary>T-0257: eine Seite je Geschoss, gleicher Maßstab für alle.</summary>
    public static string Pdf(IReadOnlyList<FloorPlan> levels, string path)
    {
        if (levels.Count == 0) throw new ArgumentException("Es gibt kein Geschoss.");
        var p = levels[0];
        var all = levels.Select(l => l.Bounds()).Where(b => b is not null).Select(b => b!.Value).ToList();
        var (minX, minY, maxX, maxY) = all.Count == 0 ? (0.0, 0.0, 100.0, 100.0) : (all.Min(b => b.MinX), all.Min(b => b.MinY), all.Max(b => b.MaxX), all.Max(b => b.MaxY));
        var t = PlanTheme.Paper;
        const double mm = 72 / 25.4;
        var (pw, ph) = (297.0, 210.0); var area = (W: pw - 30, H: ph - 40);
        var scale = new[] { 50, 100, 200 }.First(sc => (maxX - minX + 160) * 10 / sc <= area.W && (maxY - minY + 160) * 10 / sc <= area.H || sc == 200);
        if ((maxX - minX + 160) * 10 / 100 > area.W || (maxY - minY + 160) * 10 / 100 > area.H) (pw, ph, area) = (420, 297, (390, 257));
        scale = new[] { 50, 100, 200 }.First(sc => (maxX - minX + 160) * 10 / sc <= area.W && (maxY - minY + 160) * 10 / sc <= area.H || sc == 200);
        var k = 10.0 / scale * mm; // cm → pt
        var ox = 15 * mm + (area.W * mm - (maxX - minX) * k) / 2 - minX * k; var oy = 15 * mm + (area.H * mm - (maxY - minY) * k) / 2 - minY * k;
        XPoint P(double x, double y) => new(ox + x * k, oy + y * k);
        static XColor C(string hex) { var c = hex.TrimStart('#'); return XColor.FromArgb(Convert.ToInt32(c[..2], 16), Convert.ToInt32(c[2..4], 16), Convert.ToInt32(c[4..6], 16)); }
        Trendo.Core.Sys.PdfTools.Fonts.Ensure();
        using var pdf = new PdfDocument(); pdf.Info.Title = p.Name; pdf.Info.Creator = "Trendo Grundriss";
        var living = levels.Sum(l => l.LivingArea());
        foreach (var level in levels)
        {
        p = level; var shapes = Build(p, t);
        var page = pdf.AddPage(); page.Width = PdfSharp.Drawing.XUnit.FromMillimeter(pw); page.Height = PdfSharp.Drawing.XUnit.FromMillimeter(ph);
        using (var g = XGraphics.FromPdfPage(page))
        {
            foreach (var sh in shapes)
                switch (sh)
                {
                    case DPoly q:
                        var pts = q.Pts.Select(v => P(v.X, v.Y)).ToArray();
                        if (q.Fill is { } f) g.DrawPolygon(new XSolidBrush(C(f)), pts, XFillMode.Winding);
                        if (q.Stroke is { } st) g.DrawPolygon(new XPen(C(st), Math.Max(0.25, q.Width * k * 0.4)), pts);
                        break;
                    case DLine l: g.DrawLine(new XPen(C(l.Stroke), Math.Max(0.25, l.Width * k * 0.4)), P(l.X1, l.Y1), P(l.X2, l.Y2)); break;
                    case DArc a:
                        var pen = new XPen(C(a.Stroke), 0.4) { DashStyle = XDashStyle.Dash };
                        var c0 = P(a.Cx - a.R, a.Cy - a.R); g.DrawArc(pen, c0.X, c0.Y, a.R * 2 * k, a.R * 2 * k, a.StartDeg, a.SweepDeg); break;
                    case DText x:
                        var font = new XFont("Arial", Math.Max(4, x.Size * k * 0.9), x.Bold ? XFontStyleEx.Bold : XFontStyleEx.Regular);
                        var at = P(x.X, x.LineY(x.Size));
                        var state = g.Save(); if (x.AngleDeg != 0) g.RotateAtTransform(x.AngleDeg, at);
                        g.DrawString(x.Text, font, new XSolidBrush(C(x.Color)), new XRect(at.X - 200, at.Y - 20, 400, 40), XStringFormats.Center);
                        g.Restore(state); break;
                }
            var foot = ph * mm - 14 * mm;
            g.DrawLine(new XPen(XColors.Black, 0.6), 15 * mm, foot - 4 * mm, (pw - 15) * mm, foot - 4 * mm);
            g.DrawString(levels.Count > 1 ? $"{p.Name} – {p.LevelName}" : p.Name, new XFont("Arial", 11, XFontStyleEx.Bold), XBrushes.Black, 15 * mm, foot + 1 * mm);
            g.DrawString($"Wohnfläche (WoFlV) {p.LivingArea().ToString("0.00", De)} m²{(levels.Count > 1 ? $" (gesamt {living.ToString("0.00", De)} m²)" : "")} · {p.Rooms.Count} Räume · Maßstab 1:{scale} · Maße in m · {DateTime.Now.ToString("dd.MM.yyyy", De)}", new XFont("Arial", 8), XBrushes.DimGray, 15 * mm, foot + 6 * mm);
            var bar = 100 * k; var bx = (pw - 15) * mm - bar;
            g.DrawRectangle(XBrushes.Black, bx, foot - 1 * mm, bar / 2, 1.5 * mm); g.DrawRectangle(new XPen(XColors.Black, 0.4), bx + bar / 2, foot - 1 * mm, bar / 2, 1.5 * mm);
            g.DrawString("1 m", new XFont("Arial", 7), XBrushes.Black, new XRect(bx, foot + 1 * mm, bar, 10), XStringFormats.TopCenter);
        }
        }
        pdf.Save(path);
        return path;
    }

    // ---------- OBJ (3D-Modell für Blender, SketchUp & Co.) ----------
    /// <summary>T-0257: alle Geschosse als OBJ mit Materialdatei (Farben); Einheit Meter, z nach oben wird y (OBJ-Konvention).</summary>
    public static (string Obj, string Mtl) Obj(IReadOnlyList<FloorPlan> levels, string mtlName)
    {
        var obj = new StringBuilder($"# Trendo Grundriss\nmtllib {mtlName}\n"); var mtl = new StringBuilder("# Trendo Grundriss\n");
        var colors = new Dictionary<string, string>(); var v = 0; var part = 0;
        foreach (var (color, solid, _) in Plan3D.BuildAll(levels))
        {
            if (!colors.TryGetValue(color, out var mat))
            {
                mat = "farbe_" + color.TrimStart('#'); colors[color] = mat;
                var c = color.TrimStart('#'); double R(int i) => Convert.ToInt32(c.Substring(i, 2), 16) / 255.0;
                mtl.Append($"newmtl {mat}\nKd {R(0).ToString("0.###", Inv)} {R(2).ToString("0.###", Inv)} {R(4).ToString("0.###", Inv)}\nd 1\n\n");
            }
            obj.Append($"o teil_{++part}\nusemtl {mat}\n");
            foreach (var poly in solid.Polys)
            {
                foreach (var q in poly.Verts) obj.Append($"v {(q.X / 100).ToString("0.####", Inv)} {(q.Z / 100).ToString("0.####", Inv)} {(-q.Y / 100).ToString("0.####", Inv)}\n");
                obj.Append('f'); for (var k = 1; k <= poly.Verts.Count; k++) obj.Append(' ').Append(v + k); obj.Append('\n');
                v += poly.Verts.Count;
            }
        }
        return (obj.ToString(), mtl.ToString());
    }

    // ---------- Raumbuch und Materialliste ----------
    public static string RoomBookCsv(FloorPlan p) => RoomBookCsv([p]);

    public static string RoomBookCsv(IReadOnlyList<FloorPlan> levels)
    {
        var sb = new StringBuilder("Raum;Art;Fläche m²;Umfang m;Höhe m;Wandfläche netto m²;Fenster m²;Türen;Fenster;Boden;Wände;Geschoss;Wohnfläche m²\n");
        foreach (var p in levels)
            foreach (var r in p.Rooms)
            {
                var f = p.Figures(r);
                sb.Append(string.Join(';', Csv(r.Name), KindName(r.Kind), F(f.Area), F(f.Perimeter), F(f.Height), F(f.WallNet), F(f.WindowArea), f.Doors, f.Windows, FloorName(r.Floor), FinishName(r.WallFinish), Csv(p.LevelName), F(p.RoomLivingArea(r)))).Append('\n');
            }
        sb.Append($"Summe;;{F(levels.Sum(p => p.Rooms.Sum(r => p.Figures(r).Area)))};;;;;;;;;;\nWohnfläche (WoFlV);;{F(levels.Sum(p => p.LivingArea()))};;;;;;;;;;\n");
        return sb.ToString();
        static string F(double v) => v.ToString("0.00", De);
        static string Csv(string s) => s.Contains(';') || s.Contains('"') ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
    }

    public static string MaterialList(FloorPlan p, double bucketPrice = 45, double packPrice = 25, double skirtingPerM = 3)
    {
        var n = p.Need(); var sb = new StringBuilder($"# Materialliste – {p.Name}\n\n| Raum | Boden | m² | Wände | m² |\n|---|---|---:|---|---:|\n");
        foreach (var r in p.Rooms.Where(r => r.Kind is not ("balkon" or "keller")))
        {
            var f = p.Figures(r); sb.Append($"| {r.Name} | {FloorName(r.Floor)} | {f.Area.ToString("0.00", De)} | {FinishName(r.WallFinish)} | {(f.WallNet + (r.WallFinish == "farbe" ? f.Area : 0)).ToString("0.00", De)} |\n");
        }
        var cost = n.PaintBuckets * bucketPrice + n.FloorPacks * packPrice + n.SkirtingMeters * skirtingPerM;
        sb.Append($"\n- Bodenbelag (ohne Fliesen): {n.Floor.ToString("0.00", De)} m² → **{n.FloorPacks} Pakete** à 2,2 m² (10 % Verschnitt)\n");
        sb.Append($"- Farbe für Wände und Decken: {n.Walls.ToString("0.00", De)} m², 2 Anstriche → **{n.PaintLiters.ToString("0.0", De)} l** = {n.PaintBuckets} Eimer à 10 l\n");
        sb.Append($"- Sockelleisten: **{n.SkirtingMeters.ToString("0.00", De)} m** (Türen abgezogen)\n");
        sb.Append($"- Geschätzte Kosten: **{cost.ToString("0.00", De)} €** (Farbe {bucketPrice} €/Eimer, Boden {packPrice} €/Paket, Leiste {skirtingPerM} €/m)\n");
        foreach (var r in p.Rooms.Where(r => r.Floor == "fliesen" && r.Kind != "keller"))
        {
            var (tiles, packs, grout) = Interior.Tiles(p.Figures(r).Area, 30, 60, 3, 10, 1.44);
            sb.Append($"- Fliesen {r.Name}: {tiles} Fliesen 30 × 60 cm = {packs} Pakete, Fugenmörtel ca. {grout.ToString("0.0", De)} kg\n");
        }
        return sb.ToString();
    }

    public static string KindName(string k) => k switch
    {
        "wohnen" => "Wohnen", "schlafen" => "Schlafen", "kind" => "Kinderzimmer", "arbeiten" => "Arbeiten", "kueche" => "Küche", "essen" => "Essen",
        "bad" => "Bad", "flur" => "Flur", "abstell" => "Abstellraum", "balkon" => "Balkon/Terrasse", "keller" => "Keller", _ => k,
    };
    public static string FloorName(string f) => f switch { "laminat" => "Laminat", "parkett" => "Parkett", "fliesen" => "Fliesen", "vinyl" => "Vinyl", "teppich" => "Teppich", _ => f };
    public static string FinishName(string f) => f switch { "farbe" => "Farbe", "tapete" => "Tapete", "fliesen" => "Fliesen", _ => f };
    public static readonly string[] Kinds = ["wohnen", "schlafen", "kind", "arbeiten", "kueche", "essen", "bad", "flur", "abstell", "balkon", "keller"];
    public static readonly string[] Floors = ["laminat", "parkett", "fliesen", "vinyl", "teppich"];
    public static readonly string[] Finishes = ["farbe", "tapete", "fliesen"];
}
