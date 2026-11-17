using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using Trendo.Core.Media;
using Trendo.Core.Sys;

namespace Trendo.Core.Kreativ;

/// <summary>T-0696: Punkt eines Vektorpfads – Anker und zwei Griffe (Bézier), in Koordinaten der Form (0..W, 0..H). Griff = Anker heißt „Ecke“.</summary>
public sealed record VNode(double X, double Y, double InX, double InY, double OutX, double OutY)
{
    public static VNode Corner(double x, double y) => new(x, y, x, y, x, y);
    public bool IsCorner => Math.Abs(InX - X) + Math.Abs(InY - Y) + Math.Abs(OutX - X) + Math.Abs(OutY - Y) < 1e-9;
    public VNode Offset(double dx, double dy) => new(X + dx, Y + dy, InX + dx, InY + dy, OutX + dx, OutY + dy);
    public VNode Scale(double sx, double sy) => new(X * sx, Y * sy, InX * sx, InY * sy, OutX * sx, OutY * sy);
}

/// <summary>T-0696: eine Form im gemeinsamen Vektor-Editor (Logos, Animation). Kasten X/Y/W/H in Dokument-Koordinaten, Drehung um die Mitte.
/// Für die Animation trägt jede Form ihre Schlüsselbilder (<see cref="Tracks"/>; Farbe als gepackter RGB-Wert).</summary>
public sealed class VShape
{
    public string Id { get; set; } = "";
    public string Kind { get; set; } = "rect"; // rect, ellipse, polygon, star, line, path, text, image
    public string Name { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    public double W { get; set; } = 100;
    public double H { get; set; } = 100;
    public double Rot { get; set; }
    public double Scale { get; set; } = 1;
    public double Opacity { get; set; } = 1;
    public string Fill { get; set; } = "#D4A017";
    /// <summary>Zweite Farbe des Verlaufs – leer = einfarbig.</summary>
    public string Fill2 { get; set; } = "";
    public double GradAngle { get; set; } = 90;
    public string Stroke { get; set; } = "";
    public double StrokeW { get; set; }
    public double Radius { get; set; }
    public int Sides { get; set; } = 5;
    public double Inner { get; set; } = 0.5;
    public bool Closed { get; set; } = true;
    public List<VNode> Nodes { get; set; } = [];
    public string Text { get; set; } = "";
    public string Font { get; set; } = "Segoe UI";
    public double FontSize { get; set; } = 48;
    public bool Bold { get; set; }
    /// <summary>Textausrichtung im Kasten: start, middle, end.</summary>
    public string Anchor { get; set; } = "start";
    /// <summary>Eingebettetes Bild als data:-Adresse (PNG/JPEG).</summary>
    public string Image { get; set; } = "";
    public string Group { get; set; } = "";
    public Dictionary<string, List<AnimKit.Key>> Tracks { get; set; } = [];

    public double Cx => X + W / 2;
    public double Cy => Y + H / 2;

    public VShape Clone()
    {
        var c = (VShape)MemberwiseClone();
        c.Nodes = [.. Nodes];
        c.Tracks = Tracks.ToDictionary(kv => kv.Key, kv => kv.Value.ToList());
        return c;
    }
}

/// <summary>T-0696: Vektor-Dokument – Zeichenfläche und Formen (unten nach oben).</summary>
public sealed class VDoc
{
    public double W { get; set; } = 640;
    public double H { get; set; } = 360;
    /// <summary>Hintergrund als #RRGGBB oder „transparent“.</summary>
    public string Background { get; set; } = "transparent";
    public List<VShape> Shapes { get; set; } = [];
    public VDoc Clone() => new() { W = W, H = H, Background = Background, Shapes = Shapes.Select(s => s.Clone()).ToList() };
    public VShape? Find(string id) => Shapes.FirstOrDefault(s => s.Id == id);
}

/// <summary>T-0696 (Clean-Room, Vorbild: Vektor-Editoren wie Inkscape/Illustrator in ihrer Grundbedienung): gemeinsamer Vektor-Kern für Logos &amp; Marken und Animation &amp; Motion.
/// Formen und Bézier-Pfade, Treffer, Ausrichten, Gruppen, Ebenen-Reihenfolge, Raster; SVG schreiben/lesen (mit verlustfreier Rundreise über Metadaten),
/// fremdes SVG über <see cref="ImageVector.FromSvg"/>, PDF (Vektor, PDFsharp), Favicon (ICO), Markenpaket aus dem echten Logo.</summary>
public static class VectorKit
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    public const double K = 0.5522847498; // Kreis aus vier Bézierkurven
    /// <summary>Grundlinie der ersten Textzeile unter der Oberkante (Anteil der Schriftgröße) – gleich in Vorschau, SVG, PDF.</summary>
    public const double TextBase = 0.9;
    public const double TextLine = 1.2;
    public const string MetaNs = "urn:trendo:vektor";

    public static readonly IReadOnlyList<(string Id, string Label)> Kinds = [("rect", "Rechteck"), ("ellipse", "Ellipse"), ("polygon", "Vieleck"), ("star", "Stern"), ("line", "Linie"), ("path", "Pfad"), ("text", "Text"), ("image", "Bild")];
    public static string Label(string kind) => Kinds.FirstOrDefault(k => k.Id == kind).Label ?? kind;

    static string F(double v) => Math.Round(v, 3).ToString("0.###", Inv);

    // ───────────── Formen anlegen ─────────────
    public static VShape New(string kind, double x, double y, double w, double h, string id, string fill = "#D4A017")
    {
        var s = new VShape { Id = id, Kind = kind, X = x, Y = y, W = Math.Max(1, w), H = Math.Max(1, h), Fill = fill };
        switch (kind)
        {
            case "line": s.Fill = ""; s.Stroke = fill; s.StrokeW = 6; s.Closed = false; s.Nodes = [VNode.Corner(0, 0), VNode.Corner(s.W, s.H)]; break;
            case "star": s.Sides = 5; s.Inner = 0.45; break;
            case "polygon": s.Sides = 6; break;
            case "text": s.Text = "Text"; s.FontSize = Math.Max(8, h / TextLine); s.H = s.FontSize * TextLine; s.W = EstimateTextWidth(s); break;
        }
        return s;
    }

    public static string NextId(VDoc d, string kind) { var i = d.Shapes.Count + 1; while (d.Shapes.Any(s => s.Id == $"{kind}-{i}")) i++; return $"{kind}-{i}"; }

    public static double EstimateTextWidth(VShape s) => Math.Max(10, s.Text.Split('\n').Max(l => l.Length) * s.FontSize * (s.Bold ? 0.6 : 0.55));
    public static string[] Lines(VShape s) => (s.Text ?? "").Replace("\r", "").Split('\n');

    // ───────────── Geometrie ─────────────
    /// <summary>Pfadpunkte jeder Form (Rechteck, Ellipse, Vieleck, Stern als Pfad) in Koordinaten der Form.</summary>
    public static (List<VNode> Nodes, bool Closed) ToNodes(VShape s)
    {
        double w = s.W, h = s.H;
        switch (s.Kind)
        {
            case "rect":
            {
                var r = Math.Clamp(s.Radius, 0, Math.Min(w, h) / 2);
                if (r <= 0.01) return ([VNode.Corner(0, 0), VNode.Corner(w, 0), VNode.Corner(w, h), VNode.Corner(0, h)], true);
                var k = r * (1 - K);
                return ([
                    new(r, 0, k, 0, r, 0), new(w - r, 0, w - r, 0, w - k, 0),
                    new(w, r, w, k, w, r), new(w, h - r, w, h - r, w, h - k),
                    new(w - r, h, w - k, h, w - r, h), new(r, h, r, h, k, h),
                    new(0, h - r, 0, h - k, 0, h - r), new(0, r, 0, r, 0, k)], true);
            }
            case "ellipse":
            {
                double rx = w / 2, ry = h / 2, kx = rx * K, ky = ry * K;
                return ([
                    new(rx, 0, rx - kx, 0, rx + kx, 0), new(w, ry, w, ry - ky, w, ry + ky),
                    new(rx, h, rx + kx, h, rx - kx, h), new(0, ry, 0, ry + ky, 0, ry - ky)], true);
            }
            case "polygon" or "star":
            {
                var n = Math.Clamp(s.Sides, 3, 64); var star = s.Kind == "star"; var count = star ? n * 2 : n;
                var list = new List<VNode>();
                for (var i = 0; i < count; i++)
                {
                    var a = -Math.PI / 2 + i * 2 * Math.PI / count; var f = star && i % 2 == 1 ? Math.Clamp(s.Inner, 0.05, 1) : 1;
                    list.Add(VNode.Corner(w / 2 + w / 2 * f * Math.Cos(a), h / 2 + h / 2 * f * Math.Sin(a)));
                }
                return (list, true);
            }
            case "line" or "path": return (s.Nodes.ToList(), s.Kind != "line" && s.Closed);
            default: return ([VNode.Corner(0, 0), VNode.Corner(w, 0), VNode.Corner(w, h), VNode.Corner(0, h)], true);
        }
    }

    /// <summary>SVG-Pfaddaten nur aus M, L, C, Z (versteht jedes Programm, auch die Vorschau und PDF).</summary>
    public static string PathData(IReadOnlyList<VNode> n, bool closed, double dx = 0, double dy = 0)
    {
        if (n.Count == 0) return "";
        var sb = new StringBuilder($"M{F(n[0].X + dx)},{F(n[0].Y + dy)}");
        var segs = closed ? n.Count : n.Count - 1;
        for (var i = 0; i < segs; i++)
        {
            var a = n[i]; var b = n[(i + 1) % n.Count];
            if (Same(a.OutX, a.OutY, a.X, a.Y) && Same(b.InX, b.InY, b.X, b.Y)) sb.Append($" L{F(b.X + dx)},{F(b.Y + dy)}");
            else sb.Append($" C{F(a.OutX + dx)},{F(a.OutY + dy)} {F(b.InX + dx)},{F(b.InY + dy)} {F(b.X + dx)},{F(b.Y + dy)}");
        }
        if (closed) sb.Append(" Z");
        return sb.ToString();
    }

    static bool Same(double ax, double ay, double bx, double by) => Math.Abs(ax - bx) < 1e-9 && Math.Abs(ay - by) < 1e-9;

    /// <summary>Umriss als Punktfolge (Kurven abgetastet) in Koordinaten der Form.</summary>
    public static List<(double X, double Y)> Sample(IReadOnlyList<VNode> n, bool closed, int steps = 16)
    {
        var pts = new List<(double, double)>();
        if (n.Count == 0) return pts;
        pts.Add((n[0].X, n[0].Y));
        var segs = closed ? n.Count : n.Count - 1;
        for (var i = 0; i < segs; i++)
        {
            var a = n[i]; var b = n[(i + 1) % n.Count];
            for (var k = 1; k <= steps; k++)
            {
                var t = (double)k / steps; var u = 1 - t;
                pts.Add((u * u * u * a.X + 3 * u * u * t * a.OutX + 3 * u * t * t * b.InX + t * t * t * b.X, u * u * u * a.Y + 3 * u * u * t * a.OutY + 3 * u * t * t * b.InY + t * t * t * b.Y));
            }
        }
        return pts;
    }

    /// <summary>Punkt der Form (lokal) in Dokument-Koordinaten (Drehung und Skalierung um die Mitte).</summary>
    public static (double X, double Y) ToDoc(VShape s, double lx, double ly)
    {
        var a = s.Rot * Math.PI / 180; double c = Math.Cos(a), si = Math.Sin(a);
        double px = (lx - s.W / 2) * s.Scale, py = (ly - s.H / 2) * s.Scale;
        return (s.Cx + px * c - py * si, s.Cy + px * si + py * c);
    }

    public static (double X, double Y) ToLocal(VShape s, double x, double y)
    {
        var a = -s.Rot * Math.PI / 180; double c = Math.Cos(a), si = Math.Sin(a);
        double px = x - s.Cx, py = y - s.Cy;
        var sc = Math.Abs(s.Scale) < 1e-6 ? 1 : s.Scale;
        return ((px * c - py * si) / sc + s.W / 2, (px * si + py * c) / sc + s.H / 2);
    }

    /// <summary>Achsenparalleler Rahmen um die (gedrehte) Form in Dokument-Koordinaten.</summary>
    public static (double X, double Y, double W, double H) Bounds(VShape s)
    {
        var c = new[] { ToDoc(s, 0, 0), ToDoc(s, s.W, 0), ToDoc(s, s.W, s.H), ToDoc(s, 0, s.H) };
        double x0 = c.Min(p => p.X), y0 = c.Min(p => p.Y), x1 = c.Max(p => p.X), y1 = c.Max(p => p.Y);
        return (x0, y0, x1 - x0, y1 - y0);
    }

    public static (double X, double Y, double W, double H) Bounds(IEnumerable<VShape> shapes)
    {
        var b = shapes.Select(Bounds).ToList();
        if (b.Count == 0) return (0, 0, 0, 0);
        double x0 = b.Min(r => r.X), y0 = b.Min(r => r.Y), x1 = b.Max(r => r.X + r.W), y1 = b.Max(r => r.Y + r.H);
        return (x0, y0, x1 - x0, y1 - y0);
    }

    static bool InPolygon(List<(double X, double Y)> p, double x, double y)
    {
        var inside = false;
        for (int i = 0, j = p.Count - 1; i < p.Count; j = i++)
            if ((p[i].Y > y) != (p[j].Y > y) && x < (p[j].X - p[i].X) * (y - p[i].Y) / (p[j].Y - p[i].Y + 1e-12) + p[i].X) inside = !inside;
        return inside;
    }

    static double SegDist(double px, double py, (double X, double Y) a, (double X, double Y) b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y, l = dx * dx + dy * dy;
        var t = l < 1e-12 ? 0 : Math.Clamp(((px - a.X) * dx + (py - a.Y) * dy) / l, 0, 1);
        return Math.Sqrt(Math.Pow(px - a.X - t * dx, 2) + Math.Pow(py - a.Y - t * dy, 2));
    }

    /// <summary>Trifft der Punkt (Dokument) die Form? <paramref name="tol"/> in Dokument-Einheiten (für Linien und offene Pfade).</summary>
    public static bool Hit(VShape s, double x, double y, double tol = 4)
    {
        var (lx, ly) = ToLocal(s, x, y);
        var t = tol / Math.Max(0.01, Math.Abs(s.Scale));
        if (s.Kind is "text" or "image") return lx >= -t && ly >= -t && lx <= s.W + t && ly <= s.H + t;
        var (nodes, closed) = ToNodes(s);
        var pts = Sample(nodes, closed);
        if (closed && (s.Fill.Length > 0 || s.Fill2.Length > 0) && InPolygon(pts, lx, ly)) return true;
        var reach = Math.Max(t, s.StrokeW / 2 + t * 0.5);
        if (closed && s.Fill.Length == 0 && s.Stroke.Length == 0) reach = t; // unsichtbar: nur am Rand greifen
        for (var i = 0; i + 1 < pts.Count; i++) if (SegDist(lx, ly, pts[i], pts[i + 1]) <= reach) return true;
        return false;
    }

    /// <summary>Oberste getroffene Form (Index) oder -1.</summary>
    public static int HitTop(VDoc d, double x, double y, double tol = 4)
    {
        for (var i = d.Shapes.Count - 1; i >= 0; i--) if (Hit(d.Shapes[i], x, y, tol)) return i;
        return -1;
    }

    /// <summary>Kasten neu setzen (Größe ändern): Pfadpunkte und Schriftgröße wandern mit.</summary>
    public static void Resize(VShape s, double x, double y, double w, double h)
    {
        w = Math.Max(1, w); h = Math.Max(1, h);
        double sx = s.W > 1e-6 ? w / s.W : 1, sy = s.H > 1e-6 ? h / s.H : 1;
        if (s.Kind is "line" or "path") s.Nodes = s.Nodes.Select(n => n.Scale(sx, sy)).ToList();
        if (s.Kind == "text") s.FontSize = Math.Max(4, s.FontSize * sy);
        s.X = x; s.Y = y; s.W = w; s.H = h;
    }

    /// <summary>Nach dem Bearbeiten von Pfadpunkten: Kasten an die Punkte anpassen, ohne dass sich etwas sichtbar verschiebt (auch gedreht).</summary>
    public static void Normalize(VShape s)
    {
        if (s.Kind is not ("line" or "path") || s.Nodes.Count == 0) return;
        var pts = Sample(s.Nodes, s.Closed);
        double x0 = pts.Min(p => p.X), y0 = pts.Min(p => p.Y), x1 = pts.Max(p => p.X), y1 = pts.Max(p => p.Y);
        double nw = Math.Max(1, x1 - x0), nh = Math.Max(1, y1 - y0);
        var (cx, cy) = ToDoc(s, x0 + nw / 2, y0 + nh / 2); // neue Mitte bleibt, wo sie sichtbar war
        s.Nodes = s.Nodes.Select(n => n.Offset(-x0, -y0)).ToList();
        s.W = nw; s.H = nh; s.X = cx - nw / 2; s.Y = cy - nh / 2;
    }

    /// <summary>Formen in einen Pfad umwandeln (dann lassen sich die Punkte einzeln ziehen).</summary>
    public static void ToPath(VShape s)
    {
        if (s.Kind is "path" or "text" or "image") return;
        var (n, closed) = ToNodes(s);
        s.Nodes = n; s.Closed = closed; s.Kind = "path";
    }

    /// <summary>Eckpunkt ↔ weicher Punkt (Griffe entlang der Nachbarn, ein Drittel des Abstands).</summary>
    public static VNode ToggleSmooth(IReadOnlyList<VNode> n, int i, bool closed)
    {
        var p = n[i];
        if (!p.IsCorner) return VNode.Corner(p.X, p.Y);
        var prev = i > 0 ? n[i - 1] : closed ? n[^1] : p; var next = i < n.Count - 1 ? n[i + 1] : closed ? n[0] : p;
        double dx = next.X - prev.X, dy = next.Y - prev.Y, len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1e-6) return p;
        double ux = dx / len, uy = dy / len;
        double a = Math.Sqrt(Math.Pow(p.X - prev.X, 2) + Math.Pow(p.Y - prev.Y, 2)) / 3, b = Math.Sqrt(Math.Pow(next.X - p.X, 2) + Math.Pow(next.Y - p.Y, 2)) / 3;
        return new(p.X, p.Y, p.X - ux * a, p.Y - uy * a, p.X + ux * b, p.Y + uy * b);
    }

    public static double Snap(double v, double grid) => grid <= 0 ? v : Math.Round(v / grid) * grid;

    // ───────────── Anordnen ─────────────
    public static readonly IReadOnlyList<(string Id, string Label)> AlignModes = [("links", "Links"), ("mitte-h", "Mitte waagrecht"), ("rechts", "Rechts"), ("oben", "Oben"), ("mitte-v", "Mitte senkrecht"), ("unten", "Unten")];

    /// <summary>Ausrichten an der gemeinsamen Auswahl, bei einer einzelnen Form (oder Gruppe) an der Zeichenfläche.</summary>
    public static void Align(VDoc d, IReadOnlyCollection<string> ids, string mode)
    {
        var sel = d.Shapes.Where(s => ids.Contains(s.Id)).ToList();
        if (sel.Count == 0) return;
        var single = sel.Count == 1 || sel.Select(s => s.Group).Distinct().Count() == 1 && sel[0].Group.Length > 0;
        var frame = single ? (0.0, 0.0, d.W, d.H) : Bounds(sel);
        var units = single ? [sel] : sel.GroupBy(s => s.Group.Length > 0 ? s.Group : s.Id).Select(g => g.ToList()).ToList();
        foreach (var u in units)
        {
            var b = Bounds(u);
            double dx = mode switch { "links" => frame.Item1 - b.X, "mitte-h" => frame.Item1 + frame.Item3 / 2 - (b.X + b.W / 2), "rechts" => frame.Item1 + frame.Item3 - (b.X + b.W), _ => 0 };
            double dy = mode switch { "oben" => frame.Item2 - b.Y, "mitte-v" => frame.Item2 + frame.Item4 / 2 - (b.Y + b.H / 2), "unten" => frame.Item2 + frame.Item4 - (b.Y + b.H), _ => 0 };
            foreach (var s in u) { s.X += dx; s.Y += dy; }
        }
    }

    /// <summary>Gleichmäßig verteilen (waagrecht „h“ oder senkrecht „v“), ab drei Formen.</summary>
    public static void Distribute(VDoc d, IReadOnlyCollection<string> ids, string axis)
    {
        var sel = d.Shapes.Where(s => ids.Contains(s.Id)).ToList();
        if (sel.Count < 3) return;
        var h = axis == "h";
        var ordered = sel.OrderBy(s => h ? Bounds(s).X : Bounds(s).Y).ToList();
        var all = Bounds(sel); var total = ordered.Sum(s => h ? Bounds(s).W : Bounds(s).H);
        var gap = ((h ? all.W : all.H) - total) / (ordered.Count - 1); var pos = h ? all.X : all.Y;
        foreach (var s in ordered)
        {
            var b = Bounds(s);
            if (h) s.X += pos - b.X; else s.Y += pos - b.Y;
            pos += (h ? b.W : b.H) + gap;
        }
    }

    public static string Group(VDoc d, IReadOnlyCollection<string> ids)
    {
        var sel = d.Shapes.Where(s => ids.Contains(s.Id)).ToList();
        if (sel.Count < 2) throw new InvalidOperationException("Zum Gruppieren mindestens zwei Formen wählen.");
        var g = "gruppe-" + Guid.NewGuid().ToString("N")[..6];
        foreach (var s in sel) s.Group = g;
        // Gruppe liegt zusammen in der Reihenfolge (an der Stelle der obersten Form)
        var top = d.Shapes.FindLastIndex(s => ids.Contains(s.Id));
        var rest = d.Shapes.Where(s => !ids.Contains(s.Id)).ToList();
        var insertAt = rest.Count(s => d.Shapes.IndexOf(s) < top);
        rest.InsertRange(insertAt, sel); d.Shapes = rest;
        return g;
    }

    public static void Ungroup(VDoc d, IReadOnlyCollection<string> ids) { foreach (var s in d.Shapes.Where(s => ids.Contains(s.Id))) s.Group = ""; }

    /// <summary>Alle Formen der Gruppen der gewählten Formen.</summary>
    public static HashSet<string> WithGroups(VDoc d, IEnumerable<string> ids)
    {
        var set = ids.ToHashSet();
        var groups = d.Shapes.Where(s => set.Contains(s.Id) && s.Group.Length > 0).Select(s => s.Group).ToHashSet();
        foreach (var s in d.Shapes) if (groups.Contains(s.Group)) set.Add(s.Id);
        return set;
    }

    public static readonly IReadOnlyList<(string Id, string Label)> OrderModes = [("ganz-vorne", "Ganz nach vorne"), ("vorne", "Eine Ebene nach vorne"), ("hinten", "Eine Ebene nach hinten"), ("ganz-hinten", "Ganz nach hinten")];

    public static void Order(VDoc d, IReadOnlyCollection<string> ids, string mode)
    {
        var sel = d.Shapes.Where(s => ids.Contains(s.Id)).ToList();
        if (sel.Count == 0) return;
        var rest = d.Shapes.Where(s => !ids.Contains(s.Id)).ToList();
        int first = d.Shapes.IndexOf(sel[0]), last = d.Shapes.IndexOf(sel[^1]);
        int at = mode switch
        {
            "ganz-vorne" => rest.Count,
            "ganz-hinten" => 0,
            "vorne" => Math.Min(rest.Count, rest.Count(s => d.Shapes.IndexOf(s) < last) + 1),
            _ => Math.Max(0, rest.Count(s => d.Shapes.IndexOf(s) < first) - 1),
        };
        rest.InsertRange(at, sel); d.Shapes = rest;
    }

    /// <summary>Kopien mit neuen Kennungen (Gruppen bleiben als neue Gruppe zusammen), um <paramref name="dx"/>/<paramref name="dy"/> versetzt, oben angelegt.</summary>
    public static List<string> Duplicate(VDoc d, IReadOnlyCollection<VShape> src, double dx = 20, double dy = 20)
    {
        var map = new Dictionary<string, string>(); var ids = new List<string>();
        foreach (var s in src)
        {
            var c = s.Clone(); c.Id = NextId(d, c.Kind); c.X += dx; c.Y += dy;
            if (c.Group.Length > 0) { if (!map.TryGetValue(c.Group, out var g)) map[c.Group] = g = "gruppe-" + Guid.NewGuid().ToString("N")[..6]; c.Group = g; }
            d.Shapes.Add(c); ids.Add(c.Id);
        }
        return ids;
    }

    // ───────────── Farben ─────────────
    public static bool IsColor(string c) => LogoKit.IsHex(c);

    public static (int R, int G, int B) Rgb(string hex) => (Convert.ToInt32(hex.Substring(1, 2), 16), Convert.ToInt32(hex.Substring(3, 2), 16), Convert.ToInt32(hex.Substring(5, 2), 16));
    public static string Hex(int r, int g, int b) => $"#{Math.Clamp(r, 0, 255):X2}{Math.Clamp(g, 0, 255):X2}{Math.Clamp(b, 0, 255):X2}";
    public static double Pack(string hex) { var (r, g, b) = Rgb(hex); return r * 65536 + g * 256 + b; }
    public static string Unpack(double v) { var i = (int)Math.Clamp(Math.Round(v), 0, 0xFFFFFF); return Hex(i >> 16, (i >> 8) & 255, i & 255); }
    public static string Mix(string a, string b, double t)
    {
        var (r1, g1, b1) = Rgb(a); var (r2, g2, b2) = Rgb(b);
        return Hex((int)Math.Round(r1 + (r2 - r1) * t), (int)Math.Round(g1 + (g2 - g1) * t), (int)Math.Round(b1 + (b2 - b1) * t));
    }

    /// <summary>Verlauf: Start- und Endpunkt (relativ 0..1) aus dem Winkel (90° = oben nach unten).</summary>
    public static (double X1, double Y1, double X2, double Y2) GradientLine(double angle)
    {
        var a = angle * Math.PI / 180; double c = Math.Cos(a) / 2, s = Math.Sin(a) / 2;
        return (0.5 - c, 0.5 - s, 0.5 + c, 0.5 + s);
    }

    // ───────────── JSON (im Programm-Dokument) ─────────────
    static double N(JsonNode? n, double f) => ProgramDocNum(n, f);
    static double ProgramDocNum(JsonNode? n, double f) => n is JsonValue v && v.TryGetValue<double>(out var x) && double.IsFinite(x) ? x : n is JsonValue s && s.TryGetValue<string>(out var t) && double.TryParse(t, NumberStyles.Float, Inv, out var y) && double.IsFinite(y) ? y : f;
    static string S(JsonNode? n, string f = "") => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : f;
    static bool B(JsonNode? n, bool f) => n is JsonValue v && v.TryGetValue<bool>(out var b) ? b : f;
    static string Col(string c, string f) => c.Length == 0 ? "" : IsColor(c) ? c.ToUpperInvariant() : f;

    public static JsonObject ShapeJson(VShape s)
    {
        var o = new JsonObject
        {
            ["id"] = s.Id, ["kind"] = s.Kind, ["x"] = R(s.X), ["y"] = R(s.Y), ["w"] = R(s.W), ["h"] = R(s.H), ["fill"] = s.Fill,
        };
        if (s.Name.Length > 0) o["name"] = s.Name;
        if (Math.Abs(s.Rot) > 1e-9) o["rot"] = R(s.Rot);
        if (Math.Abs(s.Scale - 1) > 1e-9) o["scale"] = R(s.Scale);
        if (s.Opacity < 1) o["opacity"] = R(s.Opacity);
        if (s.Fill2.Length > 0) { o["fill2"] = s.Fill2; o["gradAngle"] = R(s.GradAngle); }
        if (s.Stroke.Length > 0) { o["stroke"] = s.Stroke; o["strokeW"] = R(s.StrokeW); }
        if (s.Kind == "rect" && s.Radius > 0) o["radius"] = R(s.Radius);
        if (s.Kind is "polygon" or "star") o["sides"] = s.Sides;
        if (s.Kind == "star") o["inner"] = R(s.Inner);
        if (s.Kind is "path" or "line")
        {
            o["closed"] = s.Closed;
            o["nodes"] = new JsonArray(s.Nodes.Select(n => (JsonNode)new JsonArray(R(n.X), R(n.Y), R(n.InX), R(n.InY), R(n.OutX), R(n.OutY))).ToArray());
        }
        if (s.Kind == "text") { o["text"] = s.Text; o["font"] = s.Font; o["size"] = R(s.FontSize); if (s.Bold) o["bold"] = true; if (s.Anchor != "start") o["anchor"] = s.Anchor; }
        if (s.Kind == "image") o["image"] = s.Image;
        if (s.Group.Length > 0) o["group"] = s.Group;
        if (s.Tracks.Count > 0)
            o["tracks"] = new JsonObject(s.Tracks.Where(kv => kv.Value.Count > 0).Select(kv => new KeyValuePair<string, JsonNode?>(kv.Key,
                new JsonArray(kv.Value.Select(k => (JsonNode)(kv.Key == "farbe" ? new JsonObject { ["t"] = R(k.T), ["c"] = Unpack(k.V), ["e"] = k.Ease } : new JsonObject { ["t"] = R(k.T), ["v"] = R(k.V), ["e"] = k.Ease })).ToArray()))));
        return o;
    }

    static double R(double v) => Math.Round(v, 3);

    public static VShape ShapeFrom(JsonObject o)
    {
        var kind = S(o["kind"], "rect"); if (!Kinds.Any(k => k.Id == kind)) kind = "rect";
        var s = new VShape
        {
            Id = S(o["id"], "form"), Kind = kind, Name = S(o["name"]),
            X = Math.Clamp(N(o["x"], 0), -1e5, 1e5), Y = Math.Clamp(N(o["y"], 0), -1e5, 1e5), W = Math.Clamp(N(o["w"], 100), 0.5, 1e5), H = Math.Clamp(N(o["h"], 100), 0.5, 1e5),
            Rot = N(o["rot"], 0) % 360, Scale = Math.Clamp(N(o["scale"], 1), 0.001, 1000), Opacity = Math.Clamp(N(o["opacity"], 1), 0, 1),
            Fill = Col(S(o["fill"], "#D4A017"), "#D4A017"), Fill2 = Col(S(o["fill2"]), ""), GradAngle = N(o["gradAngle"], 90),
            Stroke = Col(S(o["stroke"]), "#000000"), StrokeW = Math.Clamp(N(o["strokeW"], 0), 0, 1000), Radius = Math.Max(0, N(o["radius"], 0)),
            Sides = (int)Math.Clamp(N(o["sides"], 5), 3, 64), Inner = Math.Clamp(N(o["inner"], 0.5), 0.05, 1), Closed = B(o["closed"], true),
            Text = S(o["text"]), Font = S(o["font"], "Segoe UI"), FontSize = Math.Clamp(N(o["size"], 48), 2, 2000), Bold = B(o["bold"], false), Anchor = S(o["anchor"], "start") is "middle" or "end" ? S(o["anchor"]) : "start",
            Image = S(o["image"]), Group = S(o["group"]),
        };
        if (o["nodes"] is JsonArray na)
            s.Nodes = na.OfType<JsonArray>().Where(a => a.Count >= 2).Select(a =>
            {
                double V(int i, int fb) => N(a.Count > i ? a[i] : null, N(a[fb], 0));
                return new VNode(V(0, 0), V(1, 1), V(2, 0), V(3, 1), V(4, 0), V(5, 1));
            }).ToList();
        if (s.Kind is "line" or "path" && s.Nodes.Count < 2) s.Nodes = [VNode.Corner(0, 0), VNode.Corner(s.W, s.H)];
        if (s.Image.Length > 0 && !s.Image.StartsWith("data:image/", StringComparison.Ordinal)) s.Image = ""; // nur eingebettete Bilder – keine Pfade nach außen
        if (o["tracks"] is JsonObject tr)
            foreach (var (prop, arr) in tr)
                if (MotionKit.Props.Contains(prop) && arr is JsonArray a)
                    s.Tracks[prop] = a.OfType<JsonObject>().Select(k => new AnimKit.Key(Math.Max(0, N(k["t"], 0)),
                        prop == "farbe" ? (IsColor(S(k["c"])) ? Pack(S(k["c"])) : N(k["v"], 0)) : N(k["v"], MotionKit.Base(s, prop)),
                        S(k["e"]) is { Length: > 0 } e && AnimKit.Eases.Any(x => x.Id == e) ? e : "ein-aus")).OrderBy(k => k.T).ToList();
        return s;
    }

    public static JsonObject ToJson(VDoc d) => new()
    {
        ["w"] = R(d.W), ["h"] = R(d.H), ["background"] = d.Background,
        ["shapes"] = new JsonArray(d.Shapes.Select(s => (JsonNode)ShapeJson(s)).ToArray()),
    };

    public static VDoc FromJson(JsonNode? n, double w = 640, double h = 360, string bg = "transparent")
    {
        if (n is not JsonObject o) return new VDoc { W = w, H = h, Background = bg };
        var bgv = S(o["background"], bg);
        var d = new VDoc { W = Math.Clamp(N(o["w"], w), 16, 8192), H = Math.Clamp(N(o["h"], h), 16, 8192), Background = bgv == "transparent" || IsColor(bgv) ? bgv : bg };
        var used = new HashSet<string>();
        foreach (var so in (o["shapes"] as JsonArray ?? []).OfType<JsonObject>())
        {
            var s = ShapeFrom(so);
            if (!used.Add(s.Id)) { s.Id = NextId(d, s.Kind); used.Add(s.Id); }
            d.Shapes.Add(s);
        }
        return d;
    }

    // ───────────── SVG schreiben ─────────────
    static string X(string s) => KreativSafe.Xml(s);

    /// <summary>Gefüllte Fläche/Kontur als SVG-Attribute; legt bei Verlauf ein linearGradient in <paramref name="defs"/> an.</summary>
    static string PaintAttrs(VShape s, StringBuilder defs, string gid, string? fillOverride = null)
    {
        var fill = fillOverride ?? s.Fill;
        string f;
        if (fill.Length > 0 && s.Fill2.Length > 0 && fillOverride is null)
        {
            var (x1, y1, x2, y2) = GradientLine(s.GradAngle);
            defs.Append($"<linearGradient id=\"{gid}\" x1=\"{F(x1)}\" y1=\"{F(y1)}\" x2=\"{F(x2)}\" y2=\"{F(y2)}\"><stop offset=\"0\" stop-color=\"{fill}\"/><stop offset=\"1\" stop-color=\"{s.Fill2}\"/></linearGradient>");
            f = $"url(#{gid})";
        }
        else f = fill.Length > 0 ? fill : "none";
        var st = s.Stroke.Length > 0 && s.StrokeW > 0 ? $" stroke=\"{s.Stroke}\" stroke-width=\"{F(s.StrokeW)}\" stroke-linejoin=\"round\" stroke-linecap=\"round\"" : "";
        return $"fill=\"{f}\"{st}";
    }

    /// <summary>Die Form selbst in Koordinaten der Form (0..W, 0..H), ohne Drehung.</summary>
    public static string ShapeBody(VShape s, StringBuilder defs, string gid, string? fillOverride = null)
    {
        switch (s.Kind)
        {
            case "text":
            {
                var lines = Lines(s); var ax = s.Anchor == "middle" ? s.W / 2 : s.Anchor == "end" ? s.W : 0;
                var sb = new StringBuilder($"<text font-family=\"{X(s.Font)}\" font-size=\"{F(s.FontSize)}\"{(s.Bold ? " font-weight=\"700\"" : "")}{(s.Anchor != "start" ? $" text-anchor=\"{s.Anchor}\"" : "")} {PaintAttrs(s, defs, gid, fillOverride)} xml:space=\"preserve\">");
                for (var i = 0; i < lines.Length; i++) sb.Append($"<tspan x=\"{F(ax)}\" y=\"{F(s.FontSize * (TextBase + i * TextLine))}\">{X(lines[i])}</tspan>");
                return sb.Append("</text>").ToString();
            }
            case "image":
                return s.Image.Length == 0 ? "" : $"<image x=\"0\" y=\"0\" width=\"{F(s.W)}\" height=\"{F(s.H)}\" preserveAspectRatio=\"none\" href=\"{X(s.Image)}\" xlink:href=\"{X(s.Image)}\"/>";
            default:
            {
                var (n, closed) = ToNodes(s);
                return $"<path d=\"{PathData(n, closed)}\" {PaintAttrs(s, defs, gid, fillOverride)}/>";
            }
        }
    }

    /// <summary>Ganzes Dokument als SVG 1.1. Mit <paramref name="meta"/> liegt das bearbeitbare Dokument zusätzlich in den Metadaten (verlustfreie Rundreise in Trendo).</summary>
    public static string ToSvg(VDoc d, bool meta = true, string? background = null)
    {
        var defs = new StringBuilder(); var body = new StringBuilder();
        var bg = background ?? d.Background;
        if (bg != "transparent" && IsColor(bg)) body.Append($"<rect width=\"{F(d.W)}\" height=\"{F(d.H)}\" fill=\"{bg}\"/>");
        var i = 0;
        foreach (var s in d.Shapes)
        {
            i++;
            var shape = ShapeBody(s, defs, "v" + i);
            if (shape.Length == 0) continue;
            var tf = $"translate({F(s.X)} {F(s.Y)})";
            if (Math.Abs(s.Rot) > 1e-9) tf = $"rotate({F(s.Rot)} {F(s.Cx)} {F(s.Cy)}) " + tf;
            if (Math.Abs(s.Scale - 1) > 1e-9) tf = $"translate({F(s.Cx)} {F(s.Cy)}) scale({F(s.Scale)}) translate({F(-s.Cx)} {F(-s.Cy)}) " + tf;
            body.Append($"<g id=\"{X(s.Id)}\" transform=\"{tf}\"{(s.Opacity < 1 ? $" opacity=\"{F(s.Opacity)}\"" : "")}>{shape}</g>");
        }
        var sb = new StringBuilder($"<svg xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" width=\"{F(d.W)}\" height=\"{F(d.H)}\" viewBox=\"0 0 {F(d.W)} {F(d.H)}\">");
        if (meta) sb.Append($"<metadata><trendo:vektor xmlns:trendo=\"{MetaNs}\">{Convert.ToBase64String(Encoding.UTF8.GetBytes(ToJson(d).ToJsonString()))}</trendo:vektor></metadata>");
        if (defs.Length > 0) sb.Append("<defs>").Append(defs).Append("</defs>");
        return sb.Append(body).Append("</svg>").ToString();
    }

    // ───────────── SVG lesen ─────────────
    /// <summary>SVG in ein Vektor-Dokument. Eigene Exporte kommen verlustfrei zurück (Metadaten), fremde Dateien über den Leser der Bildbearbeitung
    /// (Rechtecke, Kreise, Ellipsen, Linien, Vielecke, Pfade, Text; Kurven als Punkte). Skipped = übersprungene Elemente.</summary>
    public static (VDoc Doc, int Skipped) FromSvg(string svg)
    {
        XDocument x;
        try { x = XDocument.Parse(svg, LoadOptions.None); }
        catch (System.Xml.XmlException e) { throw new InvalidDataException("Das ist keine lesbare SVG-Datei: " + e.Message, e); }
        var metaEl = x.Descendants(XName.Get("vektor", MetaNs)).FirstOrDefault();
        if (metaEl is not null)
        {
            try { return (FromJson(JsonNode.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(metaEl.Value.Trim())))), 0); }
            catch (Exception e) when (e is FormatException or System.Text.Json.JsonException) { /* beschädigt: wie fremdes SVG lesen */ }
        }
        var (img, skipped) = ImageVector.FromSvg(svg);
        var d = new VDoc { W = ImageDoc.N(img["width"], 800), H = ImageDoc.N(img["height"], 600) };
        var bg = ImageDoc.S(img["background"], "transparent"); d.Background = IsColor(bg) ? bg.ToUpperInvariant() : "transparent";
        foreach (var l in ImageDoc.Layers(img).OfType<JsonObject>())
        {
            double lx = ImageDoc.N(l["x"]), ly = ImageDoc.N(l["y"]), lw = ImageDoc.N(l["w"]), lh = ImageDoc.N(l["h"]);
            string Paint(string v) => IsColor(v) ? v.ToUpperInvariant() : NamedColor(v);
            switch (ImageDoc.S(l["kind"]))
            {
                case "shape":
                {
                    var s = New(ImageDoc.S(l["shape"]) == "ellipse" ? "ellipse" : "rect", lx, ly, lw, lh, NextId(d, "form"), Paint(ImageDoc.S(l["fill"], "#000000")));
                    s.Stroke = Paint(ImageDoc.S(l["stroke"])); s.StrokeW = s.Stroke.Length > 0 ? ImageDoc.N(l["strokeWidth"]) : 0;
                    d.Shapes.Add(s); break;
                }
                case "text":
                {
                    var size = ImageDoc.N(l["size"], 24);
                    var s = New("text", lx, ly, 10, size * TextLine, NextId(d, "text"), Paint(ImageDoc.S(l["color"], "#000000")));
                    s.Text = ImageDoc.S(l["text"]); s.FontSize = size; s.H = size * TextLine * Lines(s).Length; s.W = EstimateTextWidth(s);
                    s.Y = ly + size - size * TextBase; // Bildbearbeitung: y = Grundlinie − Größe
                    d.Shapes.Add(s); break;
                }
                case "path":
                {
                    var pts = ImageVector.Points(l); if (pts.Count < 2) { skipped++; break; }
                    var s = new VShape { Id = NextId(d, "pfad"), Kind = "path", Closed = l["closed"]?.GetValue<bool>() == true, Fill = Paint(ImageDoc.S(l["fill"])), Stroke = Paint(ImageDoc.S(l["stroke"])), StrokeW = ImageDoc.N(l["strokeWidth"], 1) };
                    s.Nodes = pts.Select(p => VNode.Corner(p.X, p.Y)).ToList();
                    if (s.Fill.Length == 0 && s.Stroke.Length == 0) s.Stroke = "#000000";
                    if (!s.Closed && s.Stroke.Length == 0) s.Stroke = "#000000";
                    s.X = 0; s.Y = 0; s.W = 1; s.H = 1; Normalize(s);
                    d.Shapes.Add(s); break;
                }
                default: skipped++; break;
            }
        }
        return (d, skipped);
    }

    static string NamedColor(string v) => (v ?? "").Trim().ToLowerInvariant() switch
    {
        "black" => "#000000", "white" => "#FFFFFF", "red" => "#FF0000", "green" => "#008000", "blue" => "#0000FF", "yellow" => "#FFFF00", "orange" => "#FFA500",
        "gray" or "grey" => "#808080", "purple" => "#800080", "navy" => "#000080", "gold" => "#FFD700", "silver" => "#C0C0C0", "teal" => "#008080", "maroon" => "#800000",
        var s when s.Length == 4 && s[0] == '#' && s.Skip(1).All(Uri.IsHexDigit) => ("#" + s[1] + s[1] + s[2] + s[2] + s[3] + s[3]).ToUpperInvariant(),
        _ => "",
    };

    /// <summary>Fremdes Dokument (SVG, Logo) als Gruppe einsetzen: eingepasst in <paramref name="maxW"/>×<paramref name="maxH"/>, Mitte bei (<paramref name="cx"/>, <paramref name="cy"/>).</summary>
    public static List<string> Place(VDoc target, VDoc src, double cx, double cy, double maxW, double maxH)
    {
        if (src.Shapes.Count == 0) throw new InvalidDataException("In der Datei war keine Form, die Trendo lesen kann.");
        var b = Bounds(src.Shapes);
        var k = Math.Min(1, Math.Min(maxW / Math.Max(1, b.W), maxH / Math.Max(1, b.H)));
        var g = src.Shapes.Count > 1 ? "gruppe-" + Guid.NewGuid().ToString("N")[..6] : "";
        var ids = new List<string>();
        foreach (var s0 in src.Shapes)
        {
            var s = s0.Clone(); s.Id = NextId(target, s.Kind); s.Group = g;
            var ncx = cx + (s.Cx - (b.X + b.W / 2)) * k; var ncy = cy + (s.Cy - (b.Y + b.H / 2)) * k;
            Resize(s, 0, 0, s.W * k, s.H * k); s.X = ncx - s.W / 2; s.Y = ncy - s.H / 2; s.StrokeW *= k;
            target.Shapes.Add(s); ids.Add(s.Id);
        }
        return ids;
    }

    /// <summary>Eingesetztes SVG umfärben: jede Füllung/Kontur der Auswahl bekommt die neue Farbe (Verlauf wird einfarbig).</summary>
    public static void Recolor(IEnumerable<VShape> shapes, string color)
    {
        if (!IsColor(color)) throw new ArgumentException("Farbe als #RRGGBB angeben.");
        foreach (var s in shapes)
        {
            if (s.Fill.Length > 0) { s.Fill = color; s.Fill2 = ""; }
            if (s.Stroke.Length > 0) s.Stroke = color;
            s.Tracks.Remove("farbe");
        }
    }

    // ───────────── Logos ─────────────
    /// <summary>Logo-Vorschlag aus dem Formular (Name, Untertitel, Farben, Form, Schrift) als bearbeitbares Vektor-Dokument – gleiche Geometrie wie <see cref="LogoKit.Svg"/>.</summary>
    public static VDoc FromLogo(LogoKit.Spec s)
    {
        var d = new VDoc { W = 640, H = 180, Background = "transparent" };
        var font = LogoKit.Fonts.Contains(s.Font) ? s.Font : LogoKit.Fonts[0];
        var on = LogoKit.TextOn(s.Primary);
        VShape Txt(string id, string text, double size, string color, bool bold, double left, double baseline, bool center = false, double boxW = 0)
        {
            var t = new VShape { Id = id, Kind = "text", Name = id, Text = text, FontSize = size, Fill = color, Bold = bold, Font = font, H = size * TextLine };
            t.W = EstimateTextWidth(t); t.X = center ? left + boxW / 2 - t.W / 2 : left; if (center) t.Anchor = "middle"; t.Y = baseline - size * TextBase;
            return t;
        }
        var ini = LogoKit.Initials(s.Name);
        var g = "zeichen";
        void Mark(VShape m) { m.Group = g; d.Shapes.Add(m); }
        switch (s.Layout)
        {
            case "schild":
            {
                var p = new VShape { Id = "zeichen-form", Kind = "path", Fill = s.Primary, Closed = true, X = 40, Y = 20, W = 100, H = 140 };
                // M40,20 H140 V90 Q140,140 90,160 Q40,140 40,90 Z (quadratische Kurven als kubische)
                p.Nodes = [VNode.Corner(0, 0), VNode.Corner(100, 0), new(100, 70, 100, 70, 100, 70 + 2.0 / 3 * 50), new(50, 140, 50 + 2.0 / 3 * 50, 140 - 2.0 / 3 * 20, 50 - 2.0 / 3 * 50, 140 - 2.0 / 3 * 20), new(0, 70, 0, 70 + 2.0 / 3 * 50, 0, 70)];
                Mark(p); Mark(Txt("zeichen-text", ini, 52, on, true, 0, 102, true, 180)); break;
            }
            case "quadrat":
            {
                var r = New("rect", 30, 30, 120, 120, "zeichen-form", s.Primary); r.Radius = 26; Mark(r);
                Mark(Txt("zeichen-text", ini, 56, on, true, 0, 108, true, 180)); break;
            }
            case "wort": break;
            case "linie":
                Mark(New("rect", 30, 118, 120, 8, "zeichen-form", s.Accent));
                Mark(Txt("zeichen-text", ini, 60, s.Primary, true, 0, 104, true, 180)); break;
            default:
            {
                var c = New("ellipse", 28, 28, 124, 124, "zeichen-form", s.Primary); c.Stroke = s.Accent; c.StrokeW = 6; Mark(c);
                Mark(Txt("zeichen-text", ini, 52, on, true, 0, 108, true, 180)); break;
            }
        }
        var tx = s.Layout == "wort" ? 30 : 180;
        if (!string.IsNullOrWhiteSpace(s.Name)) d.Shapes.Add(Txt("name", s.Name.Trim(), 44, s.Primary, true, tx, 92));
        if (!string.IsNullOrWhiteSpace(s.Tagline)) d.Shapes.Add(Txt("untertitel", s.Tagline.Trim(), 20, s.Accent, false, tx, 126));
        if (d.Shapes.Count(x => x.Group == g) == 1) d.Shapes.First(x => x.Group == g).Group = "";
        return d;
    }

    /// <summary>Farben des echten Logos, nach Fläche geordnet (größte zuerst).</summary>
    public static List<string> Palette(VDoc d)
    {
        var area = new Dictionary<string, double>();
        foreach (var s in d.Shapes)
        {
            var a = s.W * s.H * s.Scale * s.Scale;
            void Add(string c, double w) { if (IsColor(c)) area[c.ToUpperInvariant()] = area.GetValueOrDefault(c.ToUpperInvariant()) + w; }
            Add(s.Fill, s.Kind == "text" ? a * 0.4 : a); Add(s.Fill2, a * 0.5); Add(s.Stroke, Math.Max(1, s.StrokeW) * (s.W + s.H) * 2);
        }
        return area.OrderByDescending(kv => kv.Value).Select(kv => kv.Key).ToList();
    }

    public static List<string> FontsOf(VDoc d) => d.Shapes.Where(s => s.Kind == "text").Select(s => s.Font).Distinct().ToList();

    /// <summary>Einfarbige Fassung (z. B. Stempel, Gravur).</summary>
    public static VDoc Mono(VDoc d, string color) { var c = d.Clone(); Recolor(c.Shapes, color); return c; }

    /// <summary>Fassung für dunklen Grund: Farben mit zu wenig Kontrast zum Grund werden hell (Weiß), der Rest bleibt.</summary>
    public static VDoc ForBackground(VDoc d, string background)
    {
        var c = d.Clone(); c.Background = background;
        var target = LogoKit.TextOn(background);
        // zu schwache Farben in Richtung Weiß bzw. Schwarz schieben, bis 3:1 erreicht ist – der Farbton bleibt erkennbar
        string Fix(string col)
        {
            if (!IsColor(col) || LogoKit.Contrast(col, background) >= 3) return col;
            for (var t = 0.1; t < 1; t += 0.05) { var m = Mix(col, target, t); if (LogoKit.Contrast(m, background) >= 3) return m; }
            return target;
        }
        var fills = new Dictionary<string, string>();
        foreach (var s in c.Shapes.Where(s => s.Kind != "text")) { s.Fill = Fix(s.Fill); if (s.Fill2.Length > 0) s.Fill2 = Fix(s.Fill2); s.Stroke = Fix(s.Stroke); if (s.Group.Length > 0 && s.Fill.Length > 0) fills.TryAdd(s.Group, s.Fill); }
        foreach (var s in c.Shapes.Where(s => s.Kind == "text"))
        {
            // Text auf einer eigenen Fläche des Logos (Monogramm): lesbar auf dieser Fläche, nicht auf dem Grund
            if (s.Group.Length > 0 && fills.TryGetValue(s.Group, out var under))
            {
                if (IsColor(s.Fill) && LogoKit.Contrast(s.Fill, under) < 3) s.Fill = LogoKit.TextOn(under);
                continue;
            }
            s.Fill = Fix(s.Fill); if (s.Fill2.Length > 0) s.Fill2 = Fix(s.Fill2);
        }
        return c;
    }

    /// <summary>Markenpaket als Markdown aus dem echten Logo: Farben (mit Kontrast), Schriften, Dateien.</summary>
    public static string BrandMarkdown(VDoc d, string name)
    {
        var sb = new StringBuilder($"# Markenpaket: {(string.IsNullOrWhiteSpace(name) ? "Logo" : name.Trim())}\n\n## Farben\n\n| Farbe | Wert | RGB | Kontrast zu Weiß | Kontrast zu Schwarz |\n|---|---|---|---|---|\n");
        var pal = Palette(d);
        for (var i = 0; i < pal.Count; i++)
        {
            var (r, g, b) = Rgb(pal[i]);
            sb.Append(CultureInfo.InvariantCulture, $"| {(i == 0 ? "Hauptfarbe" : i == 1 ? "Akzent" : $"Farbe {i + 1}")} | {pal[i]} | {r}, {g}, {b} | {LogoKit.Contrast(pal[i], "#FFFFFF")}:1 | {LogoKit.Contrast(pal[i], "#000000")}:1 |\n");
        }
        var fonts = FontsOf(d);
        sb.Append($"\n## Schriften\n\n{(fonts.Count == 0 ? "- (keine Schrift im Logo)" : string.Join("\n", fonts.Select(f => "- " + f)))}\n");
        sb.Append(CultureInfo.InvariantCulture, $"\n## Größe\n\nZeichenfläche {F(d.W)} × {F(d.H)} Pixel, Seitenverhältnis {(d.W / d.H).ToString("0.00", CultureInfo.GetCultureInfo("de-DE"))}:1. Schutzraum: rundherum mindestens die Höhe des Anfangsbuchstabens frei lassen.\n");
        sb.Append("\n## Dateien\n\n- logo.svg – Original zum Weiterbearbeiten\n- logo-hell.svg / logo-dunkel.svg – für hellen und dunklen Grund\n- logo-einfarbig.svg / logo-negativ.svg – einfarbig schwarz und weiß\n- logo-512.png, logo-1024.png, logo-2048.png – Bilder mit durchsichtigem Grund\n- logo.pdf – für die Druckerei\n- favicon.ico – Symbol für Webseiten (16 bis 256 Pixel)\n");
        sb.Append("\nFür Fließtext braucht es mindestens 4,5:1 Kontrast (WCAG AA), für große Schrift 3:1. Vor der Anmeldung als Marke beim Amt prüfen, ob es den Namen oder ein ähnliches Zeichen schon gibt.\n");
        return sb.ToString();
    }

    // ───────────── PDF und ICO ─────────────
    /// <summary>Vektor-PDF (PDFsharp): Seite so groß wie die Zeichenfläche (1 Pixel = 0,75 pt), Formen als echte Pfade, Text als Text.</summary>
    public static byte[] Pdf(VDoc d)
    {
        PdfTools.EnsureFonts();
        using var doc = new PdfDocument();
        doc.Info.Title = "Logo"; doc.Info.Creator = "Trendo";
        var page = doc.AddPage(); const double pt = 0.75;
        page.Width = XUnit.FromPoint(d.W * pt); page.Height = XUnit.FromPoint(d.H * pt);
        using (var g = XGraphics.FromPdfPage(page))
        {
            g.ScaleTransform(pt);
            if (IsColor(d.Background)) g.DrawRectangle(new XSolidBrush(XC(d.Background, 1)), 0, 0, d.W, d.H);
            foreach (var s in d.Shapes)
            {
                var state = g.Save();
                g.TranslateTransform(s.Cx, s.Cy); g.RotateTransform(s.Rot); g.ScaleTransform(s.Scale); g.TranslateTransform(-s.W / 2, -s.H / 2);
                XBrush? brush = s.Fill.Length == 0 ? null : s.Fill2.Length > 0
                    ? Grad(s, s.Opacity)
                    : new XSolidBrush(XC(s.Fill, s.Opacity));
                switch (s.Kind)
                {
                    case "text":
                    {
                        var font = new XFont(s.Font, s.FontSize, s.Bold ? XFontStyleEx.Bold : XFontStyleEx.Regular);
                        var lines = Lines(s);
                        for (var i = 0; i < lines.Length; i++) g.DrawString(lines[i], font, brush ?? XBrushes.Black, s.Anchor == "middle" ? s.W / 2 : s.Anchor == "end" ? s.W : 0, s.FontSize * (TextBase + i * TextLine), s.Anchor == "middle" ? XStringFormats.BaseLineCenter : s.Anchor == "end" ? XStringFormats.BaseLineRight : XStringFormats.BaseLineLeft);
                        break;
                    }
                    case "image":
                        if (DataUri(s.Image) is { } bytes) { using var ms = new MemoryStream(bytes); using var im = XImage.FromStream(ms); g.DrawImage(im, 0, 0, s.W, s.H); }
                        break;
                    default:
                    {
                        var (n, closed) = ToNodes(s);
                        var path = new XGraphicsPath();
                        var segs = closed ? n.Count : n.Count - 1;
                        for (var i = 0; i < segs; i++)
                        {
                            var a = n[i]; var b = n[(i + 1) % n.Count];
                            if (Same(a.OutX, a.OutY, a.X, a.Y) && Same(b.InX, b.InY, b.X, b.Y)) path.AddLine(a.X, a.Y, b.X, b.Y);
                            else path.AddBezier(a.X, a.Y, a.OutX, a.OutY, b.InX, b.InY, b.X, b.Y);
                        }
                        if (closed) path.CloseFigure();
                        var pen = s.Stroke.Length > 0 && s.StrokeW > 0 ? new XPen(XC(s.Stroke, s.Opacity), s.StrokeW) { LineJoin = XLineJoin.Round, LineCap = XLineCap.Round } : null;
                        if (closed && brush is not null && pen is not null) g.DrawPath(pen, brush, path);
                        else if (closed && brush is not null) g.DrawPath(brush, path);
                        else if (pen is not null) g.DrawPath(pen, path);
                        break;
                    }
                }
                g.Restore(state);
            }
        }
        using var outMs = new MemoryStream();
        doc.Save(outMs, false);
        return outMs.ToArray();
    }

    static XColor XC(string hex, double alpha) { var (r, g, b) = Rgb(hex); return XColor.FromArgb((int)Math.Round(Math.Clamp(alpha, 0, 1) * 255), r, g, b); }

    static XLinearGradientBrush Grad(VShape s, double alpha)
    {
        var (x1, y1, x2, y2) = GradientLine(s.GradAngle);
        return new XLinearGradientBrush(new XPoint(x1 * s.W, y1 * s.H), new XPoint(x2 * s.W + 0.001, y2 * s.H + 0.001), XC(s.Fill, alpha), XC(s.Fill2, alpha));
    }

    /// <summary>Bytes einer data:-Adresse (Base64), sonst null.</summary>
    public static byte[]? DataUri(string uri)
    {
        var i = uri.IndexOf(";base64,", StringComparison.Ordinal);
        if (!uri.StartsWith("data:image/", StringComparison.Ordinal) || i < 0) return null;
        try { return Convert.FromBase64String(uri[(i + 8)..]); } catch (FormatException) { return null; }
    }

    public static string ToDataUri(byte[] bytes, string file)
    {
        var ext = Path.GetExtension(file).ToLowerInvariant();
        var mime = ext is ".jpg" or ".jpeg" ? "image/jpeg" : ext == ".png" ? "image/png" : throw new InvalidDataException("Als Bild gehen PNG und JPEG.");
        if (bytes.Length > KreativSafe.MaxImageBytes / 4) throw new InvalidDataException("Das Bild ist zu groß (höchstens 6 MB).");
        return $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
    }

    /// <summary>Favicon: mehrere PNG-Bilder (quadratisch, 16 bis 256 Pixel) in einer ICO-Datei (PNG-Einträge, ab Windows Vista üblich).</summary>
    public static byte[] Ico(IReadOnlyList<(int Size, byte[] Png)> images)
    {
        if (images.Count == 0) throw new ArgumentException("Mindestens ein Bild.");
        using var ms = new MemoryStream(); using var w = new BinaryWriter(ms);
        w.Write((short)0); w.Write((short)1); w.Write((short)images.Count);
        var offset = 6 + 16 * images.Count;
        foreach (var (size, png) in images)
        {
            if (size is < 1 or > 256) throw new ArgumentException("ICO-Bilder gehen bis 256 Pixel.");
            w.Write((byte)(size == 256 ? 0 : size)); w.Write((byte)(size == 256 ? 0 : size)); w.Write((byte)0); w.Write((byte)0);
            w.Write((short)1); w.Write((short)32); w.Write(png.Length); w.Write(offset);
            offset += png.Length;
        }
        foreach (var (_, png) in images) w.Write(png);
        return ms.ToArray();
    }

    /// <summary>Liest die Bildgrößen aus einer ICO-Datei (Prüfung).</summary>
    public static List<int> IcoSizes(byte[] ico)
    {
        if (ico.Length < 6 || BitConverter.ToInt16(ico, 2) != 1) throw new InvalidDataException("Keine ICO-Datei.");
        var n = BitConverter.ToInt16(ico, 4); var list = new List<int>();
        for (var i = 0; i < n; i++) { var b = ico[6 + 16 * i]; list.Add(b == 0 ? 256 : b); }
        return list;
    }
}

/// <summary>T-0696: Bewegung für Formen des Vektor-Editors – Schlüsselbilder je Eigenschaft (Mitte x/y, Größe, Drehung, Deckkraft, Farbe),
/// Übergänge aus <see cref="AnimKit.Eases"/>, Abtasten, animiertes SVG, und das alte Ebenen-Format von <see cref="AnimKit"/> lesen.</summary>
public static class MotionKit
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    public static readonly IReadOnlyList<string> Props = ["x", "y", "skalierung", "drehung", "deckkraft", "farbe"];
    public static readonly IReadOnlyList<(string Id, string Label, string[] Props)> Rows = [("position", "Position", ["x", "y"]), ("skalierung", "Größe", ["skalierung"]), ("drehung", "Drehung", ["drehung"]), ("deckkraft", "Deckkraft", ["deckkraft"]), ("farbe", "Farbe", ["farbe"])];
    public static readonly IReadOnlyList<(string W, string H, string Label)> Formats = [("1280", "720", "Querformat 1280 × 720"), ("1920", "1080", "Full HD 1920 × 1080"), ("1080", "1920", "Hochformat 1080 × 1920"), ("1080", "1080", "Quadrat 1080 × 1080")];

    /// <summary>Wert ohne Schlüssel: aus der Form selbst.</summary>
    public static double Base(VShape s, string prop) => prop switch
    {
        "x" => s.Cx, "y" => s.Cy, "skalierung" => s.Scale, "drehung" => s.Rot, "deckkraft" => s.Opacity,
        "farbe" => VectorKit.IsColor(s.Fill) ? VectorKit.Pack(s.Fill) : 0, _ => 0,
    };

    public static double Value(VShape s, string prop, double t)
    {
        var keys = s.Tracks.GetValueOrDefault(prop);
        return keys is null || keys.Count == 0 ? Base(s, prop) : AnimKit.Value(keys, prop, t);
    }

    /// <summary>Farbe zur Zeit t: je Farbkanal mit dem Übergang des früheren Schlüssels.</summary>
    public static string Color(VShape s, double t)
    {
        var k = s.Tracks.GetValueOrDefault("farbe");
        if (k is null || k.Count == 0) return s.Fill;
        if (t <= k[0].T) return VectorKit.Unpack(k[0].V);
        if (t >= k[^1].T) return VectorKit.Unpack(k[^1].V);
        for (var i = 0; i < k.Count - 1; i++)
            if (t <= k[i + 1].T)
            {
                var span = k[i + 1].T - k[i].T; var p = span <= 0 ? 1 : (t - k[i].T) / span;
                return VectorKit.Mix(VectorKit.Unpack(k[i].V), VectorKit.Unpack(k[i + 1].V), Math.Clamp(AnimKit.Ease(k[i].Ease, p), 0, 1));
            }
        return VectorKit.Unpack(k[^1].V);
    }

    /// <summary>Form zur Zeit t (Kopie mit Lage, Größe, Drehung, Deckkraft und Farbe aus den Schlüsseln).</summary>
    public static VShape Pose(VShape s, double t)
    {
        var p = s.Clone();
        var cx = Value(s, "x", t); var cy = Value(s, "y", t);
        p.X = cx - s.W / 2; p.Y = cy - s.H / 2; p.Scale = Value(s, "skalierung", t); p.Rot = Value(s, "drehung", t);
        p.Opacity = Math.Clamp(Value(s, "deckkraft", t), 0, 1);
        if (s.Tracks.ContainsKey("farbe")) { p.Fill = Color(s, t); p.Fill2 = ""; }
        return p;
    }

    public static VDoc PoseDoc(VDoc d, double t) => new() { W = d.W, H = d.H, Background = d.Background, Shapes = d.Shapes.Select(s => Pose(s, t)).ToList() };

    /// <summary>Setzt oder ersetzt einen Schlüssel (gleiche Zeit auf 1/100 s).</summary>
    public static void SetKey(VShape s, string prop, double t, double v, string ease = "ein-aus")
    {
        if (!Props.Contains(prop)) throw new ArgumentException("Unbekannte Eigenschaft: " + prop);
        if (!s.Tracks.TryGetValue(prop, out var list)) s.Tracks[prop] = list = [];
        var keep = list.FirstOrDefault(k => Math.Abs(k.T - t) < 0.005)?.Ease;
        list.RemoveAll(k => Math.Abs(k.T - t) < 0.005);
        list.Add(new(Math.Round(Math.Max(0, t), 2), v, AnimKit.Eases.Any(e => e.Id == ease) ? ease : keep ?? "ein-aus"));
        list.Sort((a, b) => a.T.CompareTo(b.T));
    }

    /// <summary>Schlüssel einer Zeile (alle Eigenschaften der Zeile) von <paramref name="from"/> nach <paramref name="to"/> verschieben. Liegt dort schon einer, wird er ersetzt.</summary>
    public static void MoveKeys(VShape s, IEnumerable<string> props, double from, double to)
    {
        to = Math.Round(Math.Max(0, to), 2);
        foreach (var p in props)
        {
            if (!s.Tracks.TryGetValue(p, out var list)) continue;
            var k = list.FirstOrDefault(x => Math.Abs(x.T - from) < 0.005); if (k is null) continue;
            list.Remove(k); list.RemoveAll(x => Math.Abs(x.T - to) < 0.005); list.Add(k with { T = to }); list.Sort((a, b) => a.T.CompareTo(b.T));
        }
    }

    public static void DeleteKeys(VShape s, IEnumerable<string> props, double t)
    {
        foreach (var p in props) if (s.Tracks.TryGetValue(p, out var list)) { list.RemoveAll(x => Math.Abs(x.T - t) < 0.005); if (list.Count == 0) s.Tracks.Remove(p); }
    }

    public static void SetEase(VShape s, IEnumerable<string> props, double t, string ease)
    {
        if (!AnimKit.Eases.Any(e => e.Id == ease)) throw new ArgumentException("Unbekannte Kurve.");
        foreach (var p in props) if (s.Tracks.TryGetValue(p, out var list)) for (var i = 0; i < list.Count; i++) if (Math.Abs(list[i].T - t) < 0.005) list[i] = list[i] with { Ease = ease };
    }

    /// <summary>Zeitpunkte mit Schlüsseln je Zeile.</summary>
    public static List<double> KeyTimes(VShape s, IEnumerable<string> props) => props.SelectMany(p => s.Tracks.GetValueOrDefault(p) ?? []).Select(k => k.T).Distinct().OrderBy(t => t).ToList();

    public static double Duration(VDoc d) => Math.Max(1, d.Shapes.SelectMany(s => s.Tracks.Values).SelectMany(k => k).Select(k => k.T).DefaultIfEmpty(0).Max());

    /// <summary>Altes Ebenen-Format (rechteck/kreis/text mit Mitte x/y) in Formen umwandeln – alte Animationen öffnen sich weiter.</summary>
    public static VShape FromLayer(AnimKit.Layer l)
    {
        VShape s;
        if (l.Shape == "text")
        {
            s = new VShape { Id = l.Id, Kind = "text", Text = l.Text, FontSize = Math.Max(4, l.H), Bold = true, Fill = l.Color, Anchor = "middle" };
            s.H = s.FontSize * VectorKit.TextLine * VectorKit.Lines(s).Length; s.W = VectorKit.EstimateTextWidth(s);
        }
        else
        {
            s = VectorKit.New(l.Shape == "kreis" ? "ellipse" : "rect", 0, 0, l.W, l.H, l.Id, l.Color);
            if (l.Shape != "kreis") s.Radius = 12;
        }
        s.X = AnimKit.StageW / 2 - s.W / 2; s.Y = AnimKit.StageH / 2 - s.H / 2;
        foreach (var (prop, keys) in l.Tracks) if (Props.Contains(prop)) s.Tracks[prop] = keys.ToList();
        // ohne Schlüssel bleibt die Mitte dort, wo AnimKit sie hatte (Bühnenmitte)
        return s;
    }

    static string F(double v) => Math.Round(v, 3).ToString("0.###", Inv);

    /// <summary>Animiertes SVG (SMIL), abgetastet mit <paramref name="fps"/> Bildern je Sekunde, Schleife. Läuft in jedem aktuellen Browser.</summary>
    public static string Svg(VDoc d, int fps = 30)
    {
        var dur = Duration(d); var n = Math.Clamp((int)Math.Ceiling(dur * fps), 2, 3000);
        var times = Enumerable.Range(0, n + 1).Select(i => dur * i / n).ToList();
        var keyTimes = string.Join(";", times.Select(t => (t / dur).ToString("0.######", Inv)));
        string Values(Func<double, string> f) => string.Join(";", times.Select(f));
        var common = $"dur=\"{F(dur)}s\" repeatCount=\"indefinite\" calcMode=\"linear\" keyTimes=\"{keyTimes}\"";
        var defs = new StringBuilder(); var body = new StringBuilder();
        if (VectorKit.IsColor(d.Background)) body.Append($"<rect width=\"{F(d.W)}\" height=\"{F(d.H)}\" fill=\"{d.Background}\"/>");
        var i = 0;
        foreach (var s in d.Shapes)
        {
            i++;
            var animColor = s.Tracks.ContainsKey("farbe");
            var shape = VectorKit.ShapeBody(s, defs, "m" + i, animColor ? Color(s, 0) : null);
            if (shape.Length == 0) continue;
            if (animColor)
            {
                var anim = $"<animate attributeName=\"fill\" {common} values=\"{Values(t => Color(s, t))}\"/>";
                var close = shape.EndsWith("</text>", StringComparison.Ordinal) ? shape.IndexOf('>') + 1 : shape.Length - 2;
                shape = shape.EndsWith("/>", StringComparison.Ordinal) ? shape[..close] + ">" + anim + "</path>" : shape.Insert(close, anim);
            }
            body.Append("<g>");
            body.Append($"<animateTransform attributeName=\"transform\" type=\"translate\" {common} values=\"{Values(t => F(Value(s, "x", t)) + " " + F(Value(s, "y", t)))}\"/>");
            body.Append("<g>");
            body.Append($"<animateTransform attributeName=\"transform\" type=\"rotate\" {common} values=\"{Values(t => F(Value(s, "drehung", t)))}\"/>");
            body.Append($"<animateTransform attributeName=\"transform\" type=\"scale\" additive=\"sum\" {common} values=\"{Values(t => F(Value(s, "skalierung", t)))}\"/>");
            body.Append($"<g><animate attributeName=\"opacity\" {common} values=\"{Values(t => F(Math.Clamp(Value(s, "deckkraft", t), 0, 1)))}\"/>");
            body.Append($"<g transform=\"translate({F(-s.W / 2)} {F(-s.H / 2)})\">{shape}</g></g>");
            body.Append("</g></g>");
        }
        var sb = new StringBuilder($"<svg xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" width=\"{F(d.W)}\" height=\"{F(d.H)}\" viewBox=\"0 0 {F(d.W)} {F(d.H)}\">");
        if (defs.Length > 0) sb.Append("<defs>").Append(defs).Append("</defs>");
        return sb.Append(body).Append("</svg>").ToString();
    }

    /// <summary>Bildfolge zu GIF (eigene Farbpalette, Schleife).</summary>
    public static IReadOnlyList<string> GifArgs(string framePattern, int fps, string output) =>
        ["-y", "-framerate", fps.ToString(Inv), "-i", framePattern, "-vf", "split[a][b];[a]palettegen=stats_mode=diff[p];[b][p]paletteuse=dither=bayer:bayer_scale=4", "-loop", "0", output];
}
