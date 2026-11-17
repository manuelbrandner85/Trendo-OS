using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Trendo.Core.Cad;

/// <summary>T-0747: SVG als Zeichnung lesen – für Konturen beim Fräsen (Laserschnitt-, Plotter- und Inkscape-Dateien). Gelesen werden path (alle Befehle
/// über <see cref="Media.ImageVector.ParseSubpaths"/>), rect (auch mit Rundung), circle, ellipse, line, polyline und polygon, jeweils mit transform
/// (matrix, translate, scale, rotate, skewX/Y) auch aus Gruppen. Ergebnis in Millimetern, y nach oben wie in DXF. Maßstab: width/height mit Einheit
/// (mm, cm, in, pt, pc, px) gegen die viewBox; ohne Einheit gilt 96 Pixel je Zoll (wie Inkscape und Browser). Gruppen-Namen werden zu Ebenen.</summary>
public static class CadSvg
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    const double MmPerPx = 25.4 / 96;

    public sealed record ReadResult(CadDrawing Drawing, Dictionary<string, int> Skipped, double MmPerUnit);

    public static ReadResult Read(string path)
    {
        if (new FileInfo(path).Length > 64L * 1024 * 1024) throw new CadError("Die SVG-Datei ist zu groß (über 64 MB).");
        return Parse(File.ReadAllText(path));
    }

    public static ReadResult Parse(string text)
    {
        XDocument doc;
        try { doc = XDocument.Parse(text, LoadOptions.None); }
        catch (XmlException e) { throw new CadError("Die SVG-Datei ist kein gültiges XML: " + e.Message); }
        var root = doc.Root ?? throw new CadError("Die SVG-Datei ist leer.");
        if (root.Name.LocalName != "svg") throw new CadError("Das ist keine SVG-Datei (kein <svg>-Element).");
        // Maßstab: Benutzereinheit → mm
        var vb = Nums(root.Attribute("viewBox")?.Value ?? "");
        double kx = Length(root.Attribute("width")?.Value) is { } w && vb.Count == 4 && vb[2] > 0 ? w / vb[2] : MmPerPx;
        double ky = Length(root.Attribute("height")?.Value) is { } h && vb.Count == 4 && vb[3] > 0 ? h / vb[3] : kx;
        double ox = vb.Count == 4 ? vb[0] : 0, oy = vb.Count == 4 ? vb[1] : 0;
        // Gesamtabbildung: Benutzereinheit → mm, y gespiegelt (SVG y nach unten, Zeichnung y nach oben)
        var rootM = new M(kx, 0, 0, -ky, -ox * kx, oy * ky);
        var d = new CadDrawing(); var skipped = new Dictionary<string, int>();
        Walk(root, rootM, "0", d, skipped);
        foreach (var name in d.Entities.Select(e => e.Layer).Distinct().ToList()) d.Layer(name);
        if (d.Entities.Count == 0) throw new CadError("In der SVG-Datei sind keine Formen (Pfade, Rechtecke, Kreise, Linien).");
        return new ReadResult(d, skipped, (kx + ky) / 2);
    }

    static void Walk(XElement el, M m, string layer, CadDrawing d, Dictionary<string, int> skipped)
    {
        foreach (var e in el.Elements())
        {
            var me = m * Transform(e.Attribute("transform")?.Value);
            var name = e.Name.LocalName;
            if (Style(e, "display") == "none") continue;
            switch (name)
            {
                case "g" or "a" or "switch":
                {
                    var label = e.Attributes().FirstOrDefault(a => a.Name.LocalName == "label")?.Value ?? (e.Attribute("id")?.Value is { Length: > 0 } id && !Regex.IsMatch(id, @"^(g|layer)\d+$") ? id : null);
                    Walk(e, me, label ?? layer, d, skipped); break;
                }
                case "path":
                    foreach (var (pts, closed) in Media.ImageVector.ParseSubpaths(e.Attribute("d")?.Value ?? "", 24))
                        if (pts.Count >= 2) Poly(d, layer, pts.Select(me.Apply).ToList(), closed);
                    break;
                case "rect":
                {
                    double x = N(e, "x"), y = N(e, "y"), w = N(e, "width"), h = N(e, "height");
                    if (w <= 0 || h <= 0) { Count(skipped, "leeres Rechteck"); break; }
                    double rx = N(e, "rx", -1), ry = N(e, "ry", -1); if (rx < 0) rx = Math.Max(0, ry); if (ry < 0) ry = rx; rx = Math.Min(rx, w / 2); ry = Math.Min(ry, h / 2);
                    var pts = new List<(double X, double Y)>();
                    if (rx <= 0) pts.AddRange([(x, y), (x + w, y), (x + w, y + h), (x, y + h)]);
                    else
                    {
                        void Corner(double cx, double cy, double a0) { for (var k = 0; k <= 36; k++) { var a = (a0 + 90.0 * k / 36) * Math.PI / 180; pts.Add((cx + rx * Math.Cos(a), cy + ry * Math.Sin(a))); } }
                        Corner(x + w - rx, y + ry, 270); Corner(x + w - rx, y + h - ry, 0); Corner(x + rx, y + h - ry, 90); Corner(x + rx, y + ry, 180);
                    }
                    Poly(d, layer, pts.Select(me.Apply).ToList(), true); break;
                }
                case "circle" or "ellipse":
                {
                    double cx = N(e, "cx"), cy = N(e, "cy"), rx = name == "circle" ? N(e, "r") : N(e, "rx"), ry = name == "circle" ? rx : N(e, "ry");
                    if (rx <= 0 || ry <= 0) { Count(skipped, "leerer Kreis"); break; }
                    if (Math.Abs(rx - ry) < 1e-12 && me.IsSimilarity(out var scale))
                    {
                        var (px, py) = me.Apply((cx, cy));
                        d.Entities.Add(new CadEntity { Kind = "kreis", Layer = layer, Cx = px, Cy = py, R = rx * scale });
                    }
                    else
                    {
                        var n = Math.Clamp((int)Math.Ceiling(Math.Max(rx, ry) * Math.Max(Math.Abs(me.A), Math.Abs(me.D)) * 8), 72, 720);
                        Poly(d, layer, Enumerable.Range(0, n).Select(k => me.Apply((cx + rx * Math.Cos(2 * Math.PI * k / n), cy + ry * Math.Sin(2 * Math.PI * k / n)))).ToList(), true);
                    }
                    break;
                }
                case "line":
                {
                    var (x1, y1) = me.Apply((N(e, "x1"), N(e, "y1"))); var (x2, y2) = me.Apply((N(e, "x2"), N(e, "y2")));
                    d.Entities.Add(new CadEntity { Kind = "linie", Layer = layer, Points = [[x1, y1, 0], [x2, y2, 0]] }); break;
                }
                case "polyline" or "polygon":
                {
                    var n = Nums(e.Attribute("points")?.Value ?? ""); var pts = new List<(double X, double Y)>();
                    for (var k = 0; k + 1 < n.Count; k += 2) pts.Add(me.Apply((n[k], n[k + 1])));
                    if (pts.Count >= 2) Poly(d, layer, pts, name == "polygon"); else Count(skipped, name);
                    break;
                }
                case "defs" or "title" or "desc" or "metadata" or "style" or "namedview" or "sodipodi:namedview": break;
                case "text" or "image" or "use" or "symbol" or "pattern" or "clipPath" or "mask" or "linearGradient" or "radialGradient" or "filter": Count(skipped, name); break;
                default: if (el.Name.Namespace == e.Name.Namespace) Count(skipped, name); break;
            }
        }
    }

    static void Poly(CadDrawing d, string layer, List<(double X, double Y)> pts, bool closed)
    {
        if (pts.Count == 2 && !closed) { d.Entities.Add(new CadEntity { Kind = "linie", Layer = layer, Points = [[pts[0].X, pts[0].Y, 0], [pts[1].X, pts[1].Y, 0]] }); return; }
        d.Entities.Add(new CadEntity { Kind = "polylinie", Layer = layer, Closed = closed, Points = pts.Select(p => new[] { p.X, p.Y, 0 }).ToList() });
    }

    static void Count(Dictionary<string, int> s, string k) => s[k] = s.GetValueOrDefault(k) + 1;

    static string? Style(XElement e, string prop)
    {
        if (e.Attribute(prop)?.Value is { } v) return v.Trim();
        var m = Regex.Match(e.Attribute("style")?.Value ?? "", @"(?:^|;)\s*" + Regex.Escape(prop) + @"\s*:\s*([^;]+)");
        return m.Success ? m.Groups[1].Value.Trim() : null;
    }

    static double N(XElement e, string a, double def = 0) => e.Attribute(a)?.Value is { } v && Regex.Match(v, @"^\s*([-+]?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?)") is { Success: true } m ? double.Parse(m.Groups[1].Value, Inv) : def;
    static List<double> Nums(string s) => Regex.Matches(s, @"[-+]?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?").Select(m => double.Parse(m.Value, Inv)).ToList();

    /// <summary>Länge mit Einheit in mm (ohne Einheit/px: 96 dpi). Prozent → null.</summary>
    static double? Length(string? v)
    {
        if (string.IsNullOrWhiteSpace(v)) return null;
        var m = Regex.Match(v.Trim(), @"^([-+]?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?)\s*([a-z%]*)$", RegexOptions.IgnoreCase);
        if (!m.Success) return null;
        var x = double.Parse(m.Groups[1].Value, Inv);
        return m.Groups[2].Value.ToLowerInvariant() switch
        {
            "mm" => x, "cm" => x * 10, "in" => x * 25.4, "pt" => x * 25.4 / 72, "pc" => x * 25.4 / 6, "" or "px" => x * MmPerPx, "q" => x / 4, _ => null,
        };
    }

    /// <summary>SVG-transform in eine Matrix (Reihenfolge wie in SVG: links zuerst angewandt auf das Elternsystem).</summary>
    static M Transform(string? t)
    {
        var res = M.Id;
        if (string.IsNullOrWhiteSpace(t)) return res;
        foreach (Match f in Regex.Matches(t, @"(matrix|translate|scale|rotate|skewX|skewY)\s*\(([^)]*)\)"))
        {
            var a = Nums(f.Groups[2].Value);
            M m = f.Groups[1].Value switch
            {
                "matrix" when a.Count == 6 => new M(a[0], a[1], a[2], a[3], a[4], a[5]),
                "translate" when a.Count >= 1 => new M(1, 0, 0, 1, a[0], a.Count > 1 ? a[1] : 0),
                "scale" when a.Count >= 1 => new M(a[0], 0, 0, a.Count > 1 ? a[1] : a[0], 0, 0),
                "rotate" when a.Count >= 1 => Rot(a[0], a.Count >= 3 ? a[1] : 0, a.Count >= 3 ? a[2] : 0),
                "skewX" when a.Count >= 1 => new M(1, 0, Math.Tan(a[0] * Math.PI / 180), 1, 0, 0),
                "skewY" when a.Count >= 1 => new M(1, Math.Tan(a[0] * Math.PI / 180), 0, 1, 0, 0),
                _ => M.Id,
            };
            res *= m;
        }
        return res;
        static M Rot(double deg, double cx, double cy)
        {
            var r = deg * Math.PI / 180; double c = Math.Cos(r), s = Math.Sin(r);
            return new M(1, 0, 0, 1, cx, cy) * new M(c, s, -s, c, 0, 0) * new M(1, 0, 0, 1, -cx, -cy);
        }
    }

    /// <summary>Affine 2D-Matrix wie in SVG: x' = A·x + C·y + E, y' = B·x + D·y + F.</summary>
    readonly record struct M(double A, double B, double C, double D, double E, double F)
    {
        public static readonly M Id = new(1, 0, 0, 1, 0, 0);
        public static M operator *(M p, M q) => new(p.A * q.A + p.C * q.B, p.B * q.A + p.D * q.B, p.A * q.C + p.C * q.D, p.B * q.C + p.D * q.D, p.A * q.E + p.C * q.F + p.E, p.B * q.E + p.D * q.F + p.F);
        public (double X, double Y) Apply((double X, double Y) p) => (A * p.X + C * p.Y + E, B * p.X + D * p.Y + F);
        /// <summary>Nur Drehung, Spiegelung und gleichmäßige Größe? Dann bleibt ein Kreis ein Kreis.</summary>
        public bool IsSimilarity(out double scale)
        {
            double sx = Math.Sqrt(A * A + B * B), sy = Math.Sqrt(C * C + D * D); scale = sx;
            return Math.Abs(sx - sy) < 1e-9 * Math.Max(1, sx) && Math.Abs(A * C + B * D) < 1e-9 * Math.Max(1, sx * sy);
        }
    }
}
