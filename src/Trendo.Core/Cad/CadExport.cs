using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace Trendo.Core.Cad;

/// <summary>N23: Ausgabe von Trendo CAD – STL und 3MF (3D-Druck, Bambu Studio), STEP (andere CAD-Programme) und technische Zeichnung (PDF).
/// Alle Maße in Millimetern. Kein Programm- oder Autorname in den Dateien.</summary>
public static class CadExport
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static string F(double v) => v.ToString("0.######", Inv);

    /// <summary>Geschlossenes Dreiecksnetz für den 3D-Druck: gleiche Koordinaten werden ein Punkt, Punkte anderer Flächen, die auf einer Kante liegen
    /// („T-Stellen“ aus den booleschen Operationen), werden in die Kante eingefügt, und jedes (konvexe) Polygon wird von seiner Mitte aus zerlegt.</summary>
    public static (List<Vec3> V, List<(int A, int B, int C)> T) Indexed(Solid s)
    {
        var map = new Dictionary<(long, long, long), int>(); var v = new List<Vec3>(); var t = new List<(int, int, int)>();
        static (long, long, long) K(Vec3 p) => ((long)Math.Round(p.X * 1e4), (long)Math.Round(p.Y * 1e4), (long)Math.Round(p.Z * 1e4));
        int Id(Vec3 p) { var k = K(p); if (!map.TryGetValue(k, out var i)) { i = v.Count; v.Add(p); map[k] = i; } return i; }
        var loops = s.Polys.Select(p => p.Verts.Select(Id).ToList()).ToList();
        // Raster für die Suche nach Punkten auf Kanten
        var (min, max) = s.Bounds(); var cell = Math.Max(0.5, (max - min).Len / 64);
        var grid = new Dictionary<(int, int, int), List<int>>();
        (int, int, int) C(Vec3 p) => ((int)Math.Floor((p.X - min.X) / cell), (int)Math.Floor((p.Y - min.Y) / cell), (int)Math.Floor((p.Z - min.Z) / cell));
        for (var i = 0; i < v.Count; i++) { var c = C(v[i]); if (!grid.TryGetValue(c, out var l)) grid[c] = l = []; l.Add(i); }
        List<int> OnEdge(int a, int b)
        {
            var pa = v[a]; var pb = v[b]; var d = pb - pa; var len2 = Vec3.Dot(d, d); if (len2 < 1e-12) return [];
            var ca = C(pa); var cb = C(pb); var hits = new List<(double T, int I)>();
            for (var x = Math.Min(ca.Item1, cb.Item1); x <= Math.Max(ca.Item1, cb.Item1); x++)
                for (var y = Math.Min(ca.Item2, cb.Item2); y <= Math.Max(ca.Item2, cb.Item2); y++)
                    for (var z = Math.Min(ca.Item3, cb.Item3); z <= Math.Max(ca.Item3, cb.Item3); z++)
                        if (grid.TryGetValue((x, y, z), out var list))
                            foreach (var i in list)
                            {
                                if (i == a || i == b) continue;
                                var tt = Vec3.Dot(v[i] - pa, d) / len2; if (tt <= 1e-7 || tt >= 1 - 1e-7) continue;
                                var off = pa + d * tt - v[i]; if (Vec3.Dot(off, off) < 1e-8) hits.Add((tt, i));
                            }
            return hits.OrderBy(h => h.T).Select(h => h.I).Distinct().ToList();
        }
        foreach (var loop in loops)
        {
            var full = new List<int>();
            for (var i = 0; i < loop.Count; i++) { var a = loop[i]; var b = loop[(i + 1) % loop.Count]; if (a == b) continue; full.Add(a); full.AddRange(OnEdge(a, b)); }
            // doppelte Nachbarn entfernen
            var clean = new List<int>(); foreach (var i in full) if (clean.Count == 0 || clean[^1] != i) clean.Add(i);
            if (clean.Count > 1 && clean[0] == clean[^1]) clean.RemoveAt(clean.Count - 1);
            if (clean.Count < 3) continue;
            if (clean.Count == 3) { t.Add((clean[0], clean[1], clean[2])); continue; }
            var cen = clean.Aggregate(new Vec3(0, 0, 0), (acc, i) => acc + v[i]) * (1.0 / clean.Count);
            var ci = Id(cen);
            for (var i = 0; i < clean.Count; i++) { var a = clean[i]; var b = clean[(i + 1) % clean.Count]; if (Vec3.Cross(v[a] - cen, v[b] - cen).Len > 1e-12) t.Add((ci, a, b)); }
        }
        return (v, t);
    }

    /// <summary>Dreiecke des geschlossenen Netzes als Koordinaten.</summary>
    public static IEnumerable<(Vec3 A, Vec3 B, Vec3 C)> PrintTriangles(Solid s) { var (v, t) = Indexed(s); return t.Select(x => (v[x.A], v[x.B], v[x.C])); }

    public static void WriteStl(IEnumerable<Solid> solids, string path)
    {
        var tris = solids.SelectMany(PrintTriangles).ToList();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var w = new BinaryWriter(File.Create(path));
        w.Write(Encoding.ASCII.GetBytes("binary STL, units: mm".PadRight(80)[..80]));
        w.Write(tris.Count);
        foreach (var (a, b, c) in tris)
        {
            var n = Vec3.Cross(b - a, c - a).Norm();
            foreach (var v in new[] { n, a, b, c }) { w.Write((float)v.X); w.Write((float)v.Y); w.Write((float)v.Z); }
            w.Write((ushort)0);
        }
    }

    /// <summary>3MF (3D Manufacturing Format) – je Körper ein Objekt mit Name und Farbe, wie Bambu Studio/Prusa/Cura es öffnen.</summary>
    public static void Write3mf(IReadOnlyList<(string Name, string Color, Solid Solid)> parts, string path)
    {
        var model = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\"?><model unit=\"millimeter\" xml:lang=\"de-DE\" xmlns=\"http://schemas.microsoft.com/3dmanufacturing/core/2015/02\"><resources>");
        model.Append("<basematerials id=\"1\">");
        foreach (var p in parts) model.Append($"<base name=\"{SecurityElement.Escape(p.Name)}\" displaycolor=\"{Hex(p.Color)}FF\"/>");
        model.Append("</basematerials>");
        var build = new StringBuilder("<build>");
        for (var k = 0; k < parts.Count; k++)
        {
            var (v, t) = Indexed(parts[k].Solid); if (t.Count == 0) continue;
            var id = k + 2;
            model.Append($"<object id=\"{id}\" type=\"model\" name=\"{SecurityElement.Escape(parts[k].Name)}\" pid=\"1\" pindex=\"{k}\"><mesh><vertices>");
            foreach (var p in v) model.Append($"<vertex x=\"{F(p.X)}\" y=\"{F(p.Y)}\" z=\"{F(p.Z)}\"/>");
            model.Append("</vertices><triangles>");
            foreach (var (a, b, c) in t) model.Append($"<triangle v1=\"{a}\" v2=\"{b}\" v3=\"{c}\"/>");
            model.Append("</triangles></mesh></object>");
            build.Append($"<item objectid=\"{id}\"/>");
        }
        model.Append("</resources>").Append(build).Append("</build></model>");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var z = new ZipArchive(File.Create(path), ZipArchiveMode.Create);
        void Add(string n, string c) { using var w = new StreamWriter(z.CreateEntry(n).Open(), new UTF8Encoding(false)); w.Write(c); }
        Add("[Content_Types].xml", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"model\" ContentType=\"application/vnd.ms-package.3dmanufacturing-3dmodel+xml\"/></Types>");
        Add("_rels/.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Target=\"/3D/3dmodel.model\" Id=\"rel0\" Type=\"http://schemas.microsoft.com/3dmanufacturing/2013/01/3dmodel\"/></Relationships>");
        Add("3D/3dmodel.model", model.ToString());
    }

    static string Hex(string c) { var h = c.TrimStart('#').ToUpperInvariant(); return "#" + (h.Length == 8 ? h[2..] : h); }

    /// <summary>STEP (ISO 10303-21, AP214) als facettiertes Volumen – lässt sich in FreeCAD, Fusion, SolidWorks usw. öffnen.</summary>
    public static void WriteStep(IReadOnlyList<(string Name, Solid Solid)> parts, string path)
    {
        var sb = new StringBuilder(); var n = 0;
        int E(string s) { sb.Append($"#{++n}={s};\n"); return n; }
        var ctxUnit = E("(LENGTH_UNIT()NAMED_UNIT(*)SI_UNIT(.MILLI.,.METRE.))");
        var angle = E("(NAMED_UNIT(*)PLANE_ANGLE_UNIT()SI_UNIT($,.RADIAN.))");
        var solidAngle = E("(NAMED_UNIT(*)SI_UNIT($,.STERADIAN.)SOLID_ANGLE_UNIT())");
        var unc = E($"UNCERTAINTY_MEASURE_WITH_UNIT(LENGTH_MEASURE(1.E-05),#{ctxUnit},'distance_accuracy_value','confusion accuracy')");
        var ctx = E($"(GEOMETRIC_REPRESENTATION_CONTEXT(3)GLOBAL_UNCERTAINTY_ASSIGNED_CONTEXT((#{unc}))GLOBAL_UNIT_ASSIGNED_CONTEXT((#{ctxUnit},#{angle},#{solidAngle}))REPRESENTATION_CONTEXT('',''))");
        var appCtx = E("APPLICATION_CONTEXT('automotive design')");
        E($"APPLICATION_PROTOCOL_DEFINITION('international standard','automotive_design',2000,#{appCtx})");
        var prodCtx = E($"PRODUCT_CONTEXT('',#{appCtx},'mechanical')");
        var defCtx = E($"PRODUCT_DEFINITION_CONTEXT('part definition',#{appCtx},'design')");
        foreach (var (name, solid) in parts)
        {
            var (v, t) = Indexed(solid); if (t.Count == 0) continue;
            var nm = name.Replace("'", "");
            var prod = E($"PRODUCT('{nm}','{nm}','',(#{prodCtx}))");
            var form = E($"PRODUCT_DEFINITION_FORMATION('','',#{prod})");
            var def = E($"PRODUCT_DEFINITION('design','',#{form},#{defCtx})");
            var shapeDef = E($"PRODUCT_DEFINITION_SHAPE('','',#{def})");
            var pts = v.Select(p => E($"CARTESIAN_POINT('',({F(p.X)},{F(p.Y)},{F(p.Z)}))")).ToList();
            var faces = new List<int>();
            foreach (var (a, b, c) in t)
            {
                var loop = E($"POLY_LOOP('',(#{pts[a]},#{pts[b]},#{pts[c]}))");
                var bound = E($"FACE_OUTER_BOUND('',#{loop},.T.)");
                faces.Add(E($"FACE('',(#{bound}))"));
            }
            var shell = E($"CLOSED_SHELL('',({string.Join(",", faces.Select(f => "#" + f))}))");
            var brep = E($"FACETED_BREP('{nm}',#{shell})");
            var origin = E("CARTESIAN_POINT('',(0.,0.,0.))"); var ax = E("DIRECTION('',(0.,0.,1.))"); var rx = E("DIRECTION('',(1.,0.,0.))");
            var place = E($"AXIS2_PLACEMENT_3D('',#{origin},#{ax},#{rx})");
            var rep = E($"FACETED_BREP_SHAPE_REPRESENTATION('{nm}',(#{place},#{brep}),#{ctx})");
            E($"SHAPE_DEFINITION_REPRESENTATION(#{shapeDef},#{rep})");
        }
        var file = new StringBuilder("ISO-10303-21;\nHEADER;\nFILE_DESCRIPTION(('facetted solid'),'2;1');\n");
        file.Append($"FILE_NAME('{Path.GetFileName(path).Replace("'", "")}','{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss}',(''),(''),'','','');\nFILE_SCHEMA(('AUTOMOTIVE_DESIGN {{ 1 0 10303 214 1 1 1 1 }}'));\nENDSEC;\nDATA;\n");
        file.Append(sb).Append("ENDSEC;\nEND-ISO-10303-21;\n");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, file.ToString(), new UTF8Encoding(false));
    }

    /// <summary>Kanten für Zeichnungen: Polygonränder, außer zwischen Flächen derselben Ebene (sonst sieht man jedes Dreieck).</summary>
    public static List<(Vec3 A, Vec3 B)> FeatureEdges(Solid s)
    {
        var edges = new Dictionary<((long, long, long), (long, long, long)), (Vec3 A, Vec3 B, List<Vec3> Normals)>();
        static (long, long, long) K(Vec3 p) => ((long)Math.Round(p.X * 1e4), (long)Math.Round(p.Y * 1e4), (long)Math.Round(p.Z * 1e4));
        foreach (var p in s.Polys)
            for (var i = 0; i < p.Verts.Count; i++)
            {
                var a = p.Verts[i]; var b = p.Verts[(i + 1) % p.Verts.Count];
                var ka = K(a); var kb = K(b); if (ka == kb) continue;
                var key = ka.CompareTo(kb) < 0 ? (ka, kb) : (kb, ka);
                if (!edges.TryGetValue(key, out var e)) edges[key] = e = (a, b, []);
                e.Normals.Add(p.Plane.Normal);
            }
        return edges.Values.Where(e => e.Normals.Count == 1 || e.Normals.Any(n => Vec3.Dot(n, e.Normals[0]) < 0.97)).Select(e => (e.A, e.B)).ToList();
    }

    /// <summary>Technische Zeichnung A4 quer: Vorderansicht, Draufsicht, Seitenansicht und Schrägbild, Hauptmaße, Schriftfeld.</summary>
    public static int WriteDrawing(IReadOnlyList<(string Name, Solid Solid)> parts, string title, string path)
    {
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(Sys.PdfTools).TypeHandle);
        using var pdf = new PdfDocument(); pdf.Info.Title = title;
        var page = pdf.AddPage(); page.Width = XUnit.FromMillimeter(297); page.Height = XUnit.FromMillimeter(210);
        using var g = XGraphics.FromPdfPage(page);
        const double Mm = 72 / 25.4;
        var all = parts.Where(p => p.Solid.Polys.Count > 0).ToList();
        var edges = all.SelectMany(p => FeatureEdges(p.Solid)).ToList();
        var mins = all.Select(p => p.Solid.Bounds()).ToList();
        var min = new Vec3(mins.Count == 0 ? 0 : mins.Min(b => b.Min.X), mins.Count == 0 ? 0 : mins.Min(b => b.Min.Y), mins.Count == 0 ? 0 : mins.Min(b => b.Min.Z));
        var max = new Vec3(mins.Count == 0 ? 1 : mins.Max(b => b.Max.X), mins.Count == 0 ? 1 : mins.Max(b => b.Max.Y), mins.Count == 0 ? 1 : mins.Max(b => b.Max.Z));
        double sx = Math.Max(1e-6, max.X - min.X), sy = Math.Max(1e-6, max.Y - min.Y), sz = Math.Max(1e-6, max.Z - min.Z);
        // Maßstab: Vorderansicht (X×Z) links oben, Seitenansicht (Y×Z) rechts daneben, Draufsicht (X×Y) darunter
        var availW = 180.0; var availH = 140.0;
        var scale = Math.Min(availW / (sx + sy + 30), availH / (sz + sy + 30));
        scale = new[] { 10, 5, 2, 1, 0.5, 0.2, 0.1, 0.05, 0.02, 0.01 }.First(s => s <= scale);
        var pen = new XPen(XColors.Black, 0.6); var thin = new XPen(XColors.Black, 0.25); var font = new XFont("Arial", 8); var big = new XFont("Arial", 11, XFontStyleEx.Bold);
        double ox = 25, oz = 20 + sz * scale; // Vorderansicht: Ursprung links unten
        void View(Func<Vec3, (double U, double V)> proj, double x0, double y0, string label)
        {
            foreach (var (a, b) in edges)
            {
                var (ua, va) = proj(a); var (ub, vb) = proj(b);
                g.DrawLine(pen, (x0 + ua * scale) * Mm, (y0 - va * scale) * Mm, (x0 + ub * scale) * Mm, (y0 - vb * scale) * Mm);
            }
            g.DrawString(label, font, XBrushes.DimGray, x0 * Mm, (y0 + 7) * Mm);
        }
        void Dim(double x1, double y1, double x2, double y2, string text, bool vertical)
        {
            g.DrawLine(thin, x1 * Mm, y1 * Mm, x2 * Mm, y2 * Mm);
            foreach (var (px, py, dx, dy) in new[] { (x1, y1, x2 - x1, y2 - y1), (x2, y2, x1 - x2, y1 - y2) })
            {
                var l = Math.Sqrt(dx * dx + dy * dy); if (l < 1e-9) continue; var ux = dx / l * 2; var uy = dy / l * 2;
                g.DrawLine(thin, px * Mm, py * Mm, (px + ux - uy * 0.4) * Mm, (py + uy + ux * 0.4) * Mm);
                g.DrawLine(thin, px * Mm, py * Mm, (px + ux + uy * 0.4) * Mm, (py + uy - ux * 0.4) * Mm);
            }
            var st = g.Save();
            g.TranslateTransform((x1 + x2) / 2 * Mm, (y1 + y2) / 2 * Mm);
            if (vertical) g.RotateTransform(-90);
            g.DrawString(text, font, XBrushes.Black, new XRect(-40, -11, 80, 10), XStringFormats.BottomCenter);
            g.Restore(st);
        }
        string L(double v) => v.ToString("0.##", CultureInfo.GetCultureInfo("de-DE"));
        View(p => (p.X - min.X, p.Z - min.Z), ox, oz, "Vorderansicht");
        var sideX = ox + sx * scale + 15;
        View(p => (max.Y - p.Y, p.Z - min.Z), sideX, oz, "Seitenansicht von links");
        var topY = oz + 15 + sy * scale;
        View(p => (p.X - min.X, max.Y - p.Y), ox, topY, "Draufsicht");
        Dim(ox, oz + 4, ox + sx * scale, oz + 4, L(sx), false);
        Dim(ox - 5, oz, ox - 5, oz - sz * scale, L(sz), true);
        Dim(sideX, oz + 4, sideX + sy * scale, oz + 4, L(sy), false);
        // Schrägbild (isometrisch) rechts oben
        var isoS = Math.Min(60 / (sx + sy), 50 / (sz + (sx + sy) * 0.5)) ;
        (double, double) Iso(Vec3 p) { var x = p.X - min.X; var y = p.Y - min.Y; var z = p.Z - min.Z; return ((x - y) * 0.866, z + (x + y) * 0.5); }
        var ix0 = 215.0; var iy0 = 75.0;
        foreach (var (a, b) in edges) { var (ua, va) = Iso(a); var (ub, vb) = Iso(b); g.DrawLine(thin, (ix0 + ua * isoS) * Mm, (iy0 - va * isoS) * Mm, (ix0 + ub * isoS) * Mm, (iy0 - vb * isoS) * Mm); }
        g.DrawString("Schrägbild", font, XBrushes.DimGray, (ix0 - 30) * Mm, (iy0 + 8) * Mm);
        // Rahmen und Schriftfeld
        g.DrawRectangle(new XPen(XColors.Black, 1), 10 * Mm, 10 * Mm, 277 * Mm, 190 * Mm);
        var bx = 187.0; var by = 172.0;
        g.DrawRectangle(new XPen(XColors.Black, 0.8), bx * Mm, by * Mm, 100 * Mm, 28 * Mm);
        g.DrawLine(thin, bx * Mm, (by + 12) * Mm, (bx + 100) * Mm, (by + 12) * Mm); g.DrawLine(thin, (bx + 50) * Mm, (by + 12) * Mm, (bx + 50) * Mm, (by + 28) * Mm);
        g.DrawString(title, big, XBrushes.Black, (bx + 3) * Mm, (by + 8) * Mm);
        g.DrawString($"Maßstab {(scale >= 1 ? $"{L(scale)} : 1" : $"1 : {L(1 / scale)}")}", font, XBrushes.Black, (bx + 3) * Mm, (by + 18) * Mm);
        g.DrawString("Maße in mm", font, XBrushes.Black, (bx + 3) * Mm, (by + 24) * Mm);
        g.DrawString(DateTime.Today.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture), font, XBrushes.Black, (bx + 53) * Mm, (by + 18) * Mm);
        g.DrawString($"{L(sx)} × {L(sy)} × {L(sz)}", font, XBrushes.Black, (bx + 53) * Mm, (by + 24) * Mm);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        pdf.Save(path);
        return 1;
    }

    /// <summary>Netz für die Anzeige (Zentimeter wie im 3D-Studio, Farbe je Körper).</summary>
    public static Mesh ToMesh(IEnumerable<(string Color, Solid Solid)> parts)
    {
        var m = new Mesh();
        foreach (var (color, s) in parts)
        {
            var (r, g, b) = Mesh3D.Rgb(color); m.CR = r; m.CG = g; m.CB = b;
            foreach (var (a, bb, c) in s.Triangles()) m.Add(a * 0.1, bb * 0.1, c * 0.1);
        }
        return m;
    }
}
