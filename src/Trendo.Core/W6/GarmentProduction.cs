using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace Trendo.Core;

/// <summary>Stoff mit echten Kennwerten und Physik für die Anprobe. Die Oberflächen stammen von ambientCG (CC0), die Physikwerte folgen den Stoff-Voreinstellungen von Blender.</summary>
public sealed record FabricInfo(string Id, string Name, string Note, double Gsm, double WidthCm, double StretchPct, double PricePerM, string TextureId, double TileM, double Mass, double Stiffness, double Bending, double Sheen);

public static class Fabrics
{
    public static readonly FabricInfo[] All =
    [
        new("jersey", "Baumwoll-Jersey", "Weich und dehnbar, für T-Shirts", 160, 150, 25, 9, "Fabric019", 0.12, 0.3, 15, 0.5, 0.2),
        new("popeline", "Baumwoll-Popeline", "Glatt und fest, für Hemden und Blusen", 120, 145, 0, 10, "Fabric019", 0.06, 0.25, 20, 1, 0.15),
        new("denim", "Denim (Annäherung)", "Fest und schwer. Es gibt keine freie Denim-Textur, die Oberfläche ist angenähert", 340, 150, 2, 16, "Fabric019", 0.09, 1, 40, 10, 0.1),
        new("leinen", "Leinen", "Kühl, knittert leicht", 190, 140, 0, 14, "Fabric031", 0.1, 0.35, 25, 2, 0.1),
        new("wolle", "Wollstoff", "Warm und fallend", 300, 150, 3, 22, "Fabric031", 0.15, 0.5, 20, 2, 0.35),
        new("seide", "Seide", "Leicht, fließend, glänzt", 60, 140, 0, 30, "Fabric019", 0.04, 0.15, 5, 0.05, 0.8),
        new("leder", "Leder", "Steif und schwer, kommt als Haut statt als Bahn", 1200, 120, 2, 45, "Leather039", 0.35, 0.4, 80, 150, 0),
    ];

    public static FabricInfo Of(string? id) => All.FirstOrDefault(f => f.Id == id) ?? All[0];

    static readonly Dictionary<string, string> Colors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Schwarz"] = "#1F1F1F", ["Weiß"] = "#F2F0EA", ["Weiss"] = "#F2F0EA", ["Navy"] = "#1E2F5C", ["Grau"] = "#8A8F98", ["Bordeaux"] = "#6E1F33", ["Natur"] = "#E6DCC6", ["Grün"] = "#2F6B47", ["Rot"] = "#A8232D", ["Blau"] = "#2C5AA0", ["Beige"] = "#CDBB9B",
    };

    /// <summary>Lineare Blender-Farbe aus Farbname oder #RRGGBB; unbekannt wird Grau.</summary>
    public static double[] Linear(string? color)
    {
        var hex = color is null ? "#8A8F98" : Colors.TryGetValue(color.Trim(), out var h) ? h : color.Trim();
        if (hex.Length != 7 || hex[0] != '#' || !int.TryParse(hex[1..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v)) hex = "#8A8F98";
        v = int.Parse(hex[1..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        static double L(int c) { var s = c / 255.0; return s <= 0.04045 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4); }
        return [Math.Round(L(v >> 16 & 255), 4), Math.Round(L(v >> 8 & 255), 4), Math.Round(L(v & 255), 4), 1];
    }

    public static JsonObject ToJob(FabricInfo f, string? color, string? textureDir)
    {
        var c = Linear(color);
        var o = new JsonObject
        {
            ["mass"] = f.Mass, ["tension"] = f.Stiffness, ["compression"] = f.Stiffness, ["shear"] = f.Stiffness, ["bending"] = f.Bending, ["air"] = 1.0, ["sheen"] = f.Sheen, ["tile_m"] = f.TileM,
            ["color"] = new JsonArray(c[0], c[1], c[2], c[3]),
        };
        if (textureDir is not null)
        {
            string P(string map) => Path.Combine(textureDir, $"{f.TextureId}_1K-JPG_{map}.jpg");
            o["textures"] = new JsonObject { ["color"] = P("Color"), ["roughness"] = P("Roughness"), ["normal"] = P("NormalGL") };
        }
        return o;
    }
}

public sealed record NestPlaced(int Part, int Copy, double X, double Y, bool Rotated);
public sealed record NestResult(IReadOnlyList<NestPlaced> Placed, double LengthCm, double Efficiency);

/// <summary>SPEC-08: Nahtzugabe, Zuschnitt mit echten Formen und Produktionsdateien. Der Fadenlauf bleibt erhalten: Teile drehen nur um 180°.</summary>
public static class GarmentProduction
{
    public const double SeamCm = 1.2, HemCm = 2.5, CellCm = 0.5;

    /// <summary>Außenkontur mit Nahtzugabe; am Saum breiter, weil er umgeschlagen wird.</summary>
    public static List<PatPt> WithAllowance(PatPart part)
    {
        var pts = new List<(PatPt P, double Off)>();
        foreach (var e in part.Edges)
        {
            var off = e.Name == "hem" ? HemCm : SeamCm;
            pts.AddRange(e.Points.Take(e.Points.Count - 1).Select(p => (p, off)));
        }
        var n = pts.Count; var res = new List<PatPt>(n);
        (double Dx, double Dy, double Nx, double Ny) Seg(int k)
        {
            var q = pts[k].P; var r = pts[(k + 1) % n].P; double dx = r.X - q.X, dy = r.Y - q.Y, len = Math.Max(1e-9, Math.Sqrt(dx * dx + dy * dy));
            return (dx / len, dy / len, dy / len, -dx / len); // Außenseite bei Lauf gegen den Uhrzeigersinn
        }
        for (var i = 0; i < n; i++)
        {
            var p = pts[i].P; var prev = (i + n - 1) % n; var (d1x, d1y, n1x, n1y) = Seg(prev); var (d2x, d2y, n2x, n2y) = Seg(i);
            double o1 = pts[prev].Off, o2 = pts[i].Off, denom = d1x * d2y - d1y * d2x;
            double ax = p.X + n1x * o1, ay = p.Y + n1y * o1, bx = p.X + n2x * o2, by = p.Y + n2y * o2;
            if (Math.Abs(denom) < 1e-6) { res.Add(new(bx, by)); continue; }
            var t = ((bx - ax) * d2y - (by - ay) * d2x) / denom;
            double x = ax + d1x * t, y = ay + d1y * t, limit = 4 * Math.Max(o1, o2); // Gehrung begrenzen, sonst laufen spitze Ecken weit hinaus
            if (Math.Sqrt((x - p.X) * (x - p.X) + (y - p.Y) * (y - p.Y)) > limit)
            {
                double mx = n1x + n2x, my = n1y + n2y, ml = Math.Max(1e-9, Math.Sqrt(mx * mx + my * my));
                x = p.X + mx / ml * limit; y = p.Y + my / ml * limit;
            }
            res.Add(new(x, y));
        }
        return res;
    }

    static bool Inside(IReadOnlyList<PatPt> poly, double x, double y)
    {
        var hit = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            if (poly[i].Y > y != poly[j].Y > y && x < (poly[j].X - poly[i].X) * (y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X) hit = !hit;
        return hit;
    }

    sealed record Shape(int Part, bool[,] Cells, int W, int H, double MinX, double MinY, bool Rotated);

    static Shape Rasterize(int part, IReadOnlyList<PatPt> outline, bool rotated)
    {
        var pts = rotated ? outline.Select(p => new PatPt(-p.X, -p.Y)).ToList() : outline.ToList();
        double minX = pts.Min(p => p.X), minY = pts.Min(p => p.Y), maxX = pts.Max(p => p.X), maxY = pts.Max(p => p.Y);
        int w = (int)Math.Ceiling((maxX - minX) / CellCm) + 2, h = (int)Math.Ceiling((maxY - minY) / CellCm) + 2;
        var cells = new bool[w, h];
        for (var x = 0; x < w; x++)
            for (var y = 0; y < h; y++)
            {
                var hit = false; // eine Zelle ist belegt, sobald eine Ecke oder ihre Mitte in der Form liegt: nichts überlappt
                foreach (var (fx, fy) in new[] { (0.0, 0.0), (1.0, 0.0), (0.0, 1.0), (1.0, 1.0), (0.5, 0.5) })
                    if (Inside(pts, minX + (x - 0.5 + fx) * CellCm, minY + (y - 0.5 + fy) * CellCm)) { hit = true; break; }
                cells[x, y] = hit;
            }
        return new(part, cells, w, h, minX, minY, rotated);
    }

    /// <summary>Legt alle Teile mit Nahtzugabe unten-links auf die Stoffbahn. Rasterweite 5 mm.</summary>
    public static NestResult Nest(Garment g, double fabricWidthCm)
    {
        var cols = (int)Math.Floor(fabricWidthCm / CellCm);
        var outlines = g.Parts.Select(WithAllowance).ToList();
        var items = g.Parts.SelectMany((p, i) => Enumerable.Range(0, p.Qty).Select(c => (Part: i, Copy: c, Area: p.Area))).OrderByDescending(x => x.Area).ToList();
        var variants = outlines.Select((o, i) => new[] { Rasterize(i, o, false), Rasterize(i, o, true) }).ToList();
        if (variants.Any(v => v[0].W > cols)) throw new ArgumentException("Ein Schnittteil ist breiter als die Stoffbahn. Bitte eine breitere Bahn wählen.");
        const int rows = 4000;
        var occupied = new bool[cols, rows];
        var placed = new List<NestPlaced>(); var maxRow = 0;
        foreach (var (part, copy, _) in items)
        {
            (Shape S, int X, int Y)? best = null;
            foreach (var shape in variants[part])
                for (var y = 0; y < rows - shape.H && (best is null || y < best.Value.Y + 1); y++)
                {
                    var found = false;
                    for (var x = 0; x <= cols - shape.W; x++)
                    {
                        var free = true;
                        for (var i = 0; i < shape.W && free; i++)
                            for (var j = 0; j < shape.H && free; j++)
                                if (shape.Cells[i, j] && occupied[x + i, y + j]) free = false;
                        if (!free) continue;
                        if (best is null || y < best.Value.Y) best = (shape, x, y);
                        found = true; break;
                    }
                    if (found) break;
                }
            if (best is not { } b) throw new InvalidOperationException("Die Teile passen nicht auf die Stoffbahn.");
            for (var i = 0; i < b.S.W; i++)
                for (var j = 0; j < b.S.H; j++)
                    if (b.S.Cells[i, j]) occupied[b.X + i, b.Y + j] = true;
            maxRow = Math.Max(maxRow, b.Y + b.S.H);
            placed.Add(new(part, copy, b.X * CellCm - b.S.MinX, b.Y * CellCm - b.S.MinY, b.S.Rotated));
        }
        var length = maxRow * CellCm;
        var area = g.Parts.Sum(p => outlineArea(WithAllowance(p)) * p.Qty);
        return new(placed, length, Math.Min(1, area / (fabricWidthCm * length)));
    }

    static double outlineArea(IReadOnlyList<PatPt> o)
    {
        var a = 0.0;
        for (var i = 0; i < o.Count; i++) { var p = o[i]; var q = o[(i + 1) % o.Count]; a += p.X * q.Y - q.X * p.Y; }
        return Math.Abs(a) / 2;
    }

    /// <summary>Punkt eines Teils in Stoffbahn-Koordinaten.</summary>
    public static PatPt Place(PatPt p, NestPlaced n) => n.Rotated ? new(-p.X + n.X, -p.Y + n.Y) : new(p.X + n.X, p.Y + n.Y);

    static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>DXF (R12) in Millimetern: Schnittkante auf Ebene SCHNITT, Nahtlinie auf NAHT, Fadenlauf auf FADENLAUF. Für Plotter und Zuschnitt-Software.</summary>
    public static string Dxf(Garment g)
    {
        var sb = new StringBuilder("0\nSECTION\n2\nENTITIES\n");
        void Poly(string layer, IReadOnlyList<PatPt> pts, double ox, double oy, bool closed)
        {
            sb.Append($"0\nPOLYLINE\n8\n{layer}\n66\n1\n70\n{(closed ? 1 : 0)}\n");
            foreach (var p in pts) sb.Append($"0\nVERTEX\n8\n{layer}\n10\n{F((p.X + ox) * 10)}\n20\n{F((p.Y + oy) * 10)}\n");
            sb.Append("0\nSEQEND\n");
        }
        var x = 0.0;
        foreach (var p in g.Parts)
        {
            var cut = WithAllowance(p); var ox = x - cut.Min(q => q.X) + 5; var oy = -cut.Min(q => q.Y) + 5;
            Poly("SCHNITT", cut, ox, oy, true); Poly("NAHT", p.Outline, ox, oy, true);
            Poly("FADENLAUF", [new PatPt(0, p.Outline.Min(q => q.Y) + 6), new PatPt(0, p.Outline.Max(q => q.Y) - 6)], ox, oy, false);
            x += cut.Max(q => q.X) - cut.Min(q => q.X) + 5;
        }
        sb.Append("0\nENDSEC\n0\nEOF\n");
        return sb.ToString();
    }

    /// <summary>Schnittbogen 1:1 auf A4 zum Zusammenkleben: 10 mm Überlappung, Klebemarken, Kontrollquadrat 5 × 5 cm. Rückgabe: Seitenzahl.</summary>
    public static int PdfTiled(string path, Garment g, string title)
    {
        Sys.PdfTools.Fonts.Ensure();
        const double pageW = 210, pageH = 297, margin = 10, overlap = 10;
        double usableW = pageW - 2 * margin, usableH = pageH - 2 * margin;
        // Teile nebeneinander anordnen (mm)
        var boxes = new List<(PatPart P, List<PatPt> Cut, double Ox, double Oy)>(); var x = 80.0; var height = 60.0;
        foreach (var p in g.Parts)
        {
            var c = WithAllowance(p); double w = (c.Max(q => q.X) - c.Min(q => q.X)) * 10, h = (c.Max(q => q.Y) - c.Min(q => q.Y)) * 10;
            boxes.Add((p, c, x - c.Min(q => q.X) * 10, 10 + c.Max(q => q.Y) * 10)); x += w + 15; height = Math.Max(height, h + 20);
        }
        var stepX = usableW - overlap; var stepY = usableH - overlap;
        int cols = (int)Math.Ceiling(Math.Max(1, (x - overlap) / stepX)), rows = (int)Math.Ceiling(Math.Max(1, (height - overlap) / stepY));
        using var doc = new PdfDocument();
        XPoint Pt(double mx, double my) => new(XUnit.FromMillimeter(mx).Point, XUnit.FromMillimeter(my).Point);
        var small = new XFont("Arial", 8); var label = new XFont("Arial", 11, XFontStyleEx.Bold);
        var cutPen = new XPen(XColors.Black, 0.8); var seamPen = new XPen(XColors.Gray, 0.5) { DashStyle = XDashStyle.Dash }; var thin = new XPen(XColors.Gray, 0.3);
        for (var r = 0; r < rows; r++)
            for (var c = 0; c < cols; c++)
            {
                var page = doc.AddPage(); page.Width = XUnit.FromMillimeter(pageW); page.Height = XUnit.FromMillimeter(pageH);
                using var gfx = XGraphics.FromPdfPage(page);
                double dx = margin - c * stepX, dy = margin - r * stepY;
                gfx.Save();
                gfx.IntersectClip(new XRect(Pt(margin, margin), Pt(pageW - margin, pageH - margin)));
                XPoint[] Map(IEnumerable<PatPt> pts, double ox, double oy) => pts.Select(q => Pt(q.X * 10 + ox + dx, oy - q.Y * 10 + dy)).ToArray();
                foreach (var (p, cut, ox, oy) in boxes)
                {
                    gfx.DrawPolygon(cutPen, Map(cut, ox, oy)); gfx.DrawPolygon(seamPen, Map(p.Outline, ox, oy));
                    var line = Map([new PatPt(0, p.Outline.Min(q => q.Y) + 6), new PatPt(0, p.Outline.Max(q => q.Y) - 6)], ox, oy);
                    gfx.DrawLine(thin, line[0], line[1]);
                    gfx.DrawString($"{p.Name} · {p.Qty}× zuschneiden · Fadenlauf", label, XBrushes.Black, Pt(ox + dx + 10, oy - (p.Outline.Max(q => q.Y) + p.Outline.Min(q => q.Y)) * 5 + dy));
                }
                if (r == 0 && c == 0)
                {
                    gfx.DrawRectangle(new XPen(XColors.Black, 0.6), new XRect(Pt(15 + margin, 15 + margin), Pt(65 + margin, 65 + margin)));
                    gfx.DrawString("Kontrollquadrat 5 × 5 cm", small, XBrushes.Black, Pt(17 + margin, 12 + margin));
                    gfx.DrawString("Mit 100 % drucken", small, XBrushes.Black, Pt(17 + margin, 72 + margin));
                }
                gfx.Restore();
                gfx.DrawRectangle(thin, new XRect(Pt(margin, margin), Pt(pageW - margin, pageH - margin)));
                gfx.DrawString($"{title} · Blatt Zeile {r + 1}, Spalte {c + 1} von {rows} × {cols}", small, XBrushes.Gray, Pt(margin, 7));
            }
        var count = doc.PageCount;
        doc.Save(path);
        return count;
    }

    /// <summary>Stoffbedarf und Materialkosten je Größe.</summary>
    public static List<(string Size, double Meters, double Cost, double Efficiency)> SizeRun(string kind, BodyMeasures basis, IReadOnlyList<string> sizes, FabricInfo fabric, double girthStep = 4)
    {
        var baseIdx = sizes.Count / 2; var res = new List<(string, double, double, double)>();
        for (var i = 0; i < sizes.Count; i++)
        {
            var k = i - baseIdx;
            var nest = Nest(GarmentBlocks.Build(kind, basis with { Chest = basis.Chest + girthStep * k, Waist = basis.Waist + girthStep * k, Hip = basis.Hip + girthStep * k }), fabric.WidthCm);
            var m = Math.Round(nest.LengthCm / 100, 2);
            res.Add((sizes[i], m, Math.Round(m * fabric.PricePerM, 2), nest.Efficiency));
        }
        return res;
    }

    /// <summary>Technisches Datenblatt: Maße, Teile, Nähte, Stückliste, Verbrauch je Größe.</summary>
    public static string TechPack(string name, Garment g, BodyMeasures b, FabricInfo f, string color, IReadOnlyList<string> sizes)
    {
        var de = CultureInfo.GetCultureInfo("de-DE"); var sb = new StringBuilder();
        sb.AppendLine($"# Technisches Datenblatt: {name}").AppendLine();
        sb.AppendLine($"**Schnitt:** {g.Label}  ").AppendLine($"**Stoff:** {f.Name}, {f.Gsm:0} g/m², Bahnbreite {f.WidthCm:0} cm, Dehnung {f.StretchPct:0} %  ").AppendLine($"**Farbe:** {color}").AppendLine();
        sb.AppendLine("## Körpermaße der Grundgröße (cm)").AppendLine().AppendLine("| Größe | Brust | Taille | Hüfte |").AppendLine("|---|---:|---:|---:|").AppendLine($"| Körper | {b.Chest:0} | {b.Waist:0} | {b.Hip:0} | ").AppendLine();
        sb.AppendLine("## Schnittteile").AppendLine().AppendLine("| Teil | Anzahl | Fläche (cm²) | Nahtzugabe |").AppendLine("|---|---:|---:|---|");
        foreach (var p in g.Parts) sb.AppendLine($"| {p.Name} | {p.Qty} | {p.Area.ToString("0", de)} | {SeamCm.ToString("0.0", de)} cm, Saum {HemCm.ToString("0.0", de)} cm |");
        sb.AppendLine().AppendLine("## Nähte").AppendLine();
        foreach (var s in g.Seams) sb.AppendLine($"- {s.PartA} ({s.EdgeA}) an {s.PartB} ({s.EdgeB})");
        sb.AppendLine().AppendLine("## Stoffbedarf und Materialkosten").AppendLine().AppendLine("| Größe | Meter | Kosten Stoff | Ausnutzung |").AppendLine("|---|---:|---:|---:|");
        foreach (var (size, m, cost, eff) in SizeRun(g.Kind, b, sizes, f)) sb.AppendLine($"| {size} | {m.ToString("0.00", de)} | {cost.ToString("0.00", de)} € | {(eff * 100).ToString("0", de)} % |");
        return sb.ToString();
    }
}
