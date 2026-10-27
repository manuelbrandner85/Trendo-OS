using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;

namespace Trendo.Core.Office;

public static partial class SlideFiles
{
    // ================= PPTX lesen =================
    sealed class Pkg(ZipArchive z)
    {
        public XDocument? Load(string name) { var e = z.GetEntry(name); if (e is null) return null; using var s = e.Open(); return XDocument.Load(s); }
        public byte[]? Bytes(string name) { var e = z.GetEntry(name); if (e is null) return null; using var s = e.Open(); using var m = new MemoryStream(); s.CopyTo(m); return m.ToArray(); }
        static string Dir(string f) { var i = f.LastIndexOf('/'); return i < 0 ? "" : f[..i]; }
        public static string Resolve(string baseFile, string target)
        {
            if (target.StartsWith('/')) return target[1..];
            var parts = Dir(baseFile).Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();
            foreach (var seg in target.Split('/')) { if (seg == "..") { if (parts.Count > 0) parts.RemoveAt(parts.Count - 1); } else if (seg != ".") parts.Add(seg); }
            return string.Join('/', parts);
        }
        public Dictionary<string, (string Type, string Target)> Rels(string part)
        {
            var d = Load($"{Dir(part)}/_rels/{part[(part.LastIndexOf('/') + 1)..]}.rels");
            return d?.Root?.Elements(Pr + "Relationship").ToDictionary(r => (string)r.Attribute("Id")!, r => ((string?)r.Attribute("Type") ?? "", Resolve(part, (string?)r.Attribute("Target") ?? ""))) ?? [];
        }
    }

    public static SlideDoc ReadPptx(string path)
    {
        using var z = ZipFile.OpenRead(path);
        var pkg = new Pkg(z);
        var pres = pkg.Load("ppt/presentation.xml") ?? throw new InvalidDataException("Keine PowerPoint-Datei (presentation.xml fehlt).");
        var presRels = pkg.Rels("ppt/presentation.xml");
        var size = pres.Root!.Element(P + "sldSz");
        double sx = SlideDoc.WidthCm / (((long?)size?.Attribute("cx") ?? 12192000) / (double)Emu), sy = SlideDoc.HeightCm / (((long?)size?.Attribute("cy") ?? 6858000) / (double)Emu);
        var doc = new SlideDoc();
        // Design: Farben aus dem Thema, wenn es zu einem eigenen passt
        if (presRels.Values.FirstOrDefault(r => r.Type.EndsWith("/theme", StringComparison.Ordinal)) is { Target.Length: > 0 } tr && pkg.Load(tr.Target) is { } theme)
        {
            var name = (string?)theme.Root?.Attribute("name");
            if (SlideThemes.All.FirstOrDefault(t => t.Name == name) is { } known) doc.Theme = known;
        }
        foreach (var sid in pres.Root.Element(P + "sldIdLst")?.Elements(P + "sldId") ?? [])
        {
            var rid = (string?)sid.Attribute(R + "id");
            if (rid is null || !presRels.TryGetValue(rid, out var rel)) continue;
            var sx2 = pkg.Load(rel.Target); if (sx2?.Root is null) continue;
            var rels = pkg.Rels(rel.Target);
            var slide = new Slide { Layout = SlideLayout.Leer };
            if (sx2.Root.Element(P + "cSld")?.Element(P + "bg")?.Descendants(A + "srgbClr").FirstOrDefault()?.Attribute("val") is { } bgv) slide.Background = "#" + bgv.Value;
            var layoutShapes = LayoutPlaceholders(pkg, rels);
            var idMap = new Dictionary<string, SlideShape>();
            foreach (var el in sx2.Root.Element(P + "cSld")?.Element(P + "spTree")?.Elements() ?? [])
            {
                var shape = el.Name.LocalName switch
                {
                    "sp" => ReadSp(el, layoutShapes),
                    "pic" => ReadPic(el, rels, pkg),
                    "graphicFrame" => ReadFrame(el, rels, pkg),
                    _ => null,
                };
                if (shape is null) continue;
                shape.X *= sx; shape.W *= sx; shape.Y *= sy; shape.H *= sy;
                slide.Shapes.Add(shape);
                if (el.Descendants(P + "cNvPr").FirstOrDefault()?.Attribute("id") is { } idAttr) idMap[idAttr.Value] = shape;
            }
            slide.Layout = slide.ByRole("sub") is not null ? SlideLayout.Titel : slide.ByRole("body2") is not null ? SlideLayout.ZweiSpalten : slide.ByRole("body") is not null ? SlideLayout.TitelInhalt : slide.ByRole("title") is not null ? SlideLayout.NurTitel : SlideLayout.Leer;
            var trEl = sx2.Root.Element(P + "transition");
            slide.Transition = trEl?.Elements().FirstOrDefault()?.Name.LocalName switch { "fade" => Transition.Ueberblenden, "push" or "cover" or "pull" => Transition.Schieben, "wipe" or "split" or "wheel" => Transition.Wischen, null => Transition.Keine, _ => Transition.Ueberblenden };
            foreach (var ctn in sx2.Root.Element(P + "timing")?.Descendants(P + "cTn").Where(c => (string?)c.Attribute("presetClass") == "entr") ?? [])
            {
                var spid = ctn.Descendants(P + "spTgt").FirstOrDefault()?.Attribute("spid")?.Value;
                if (spid is not null && idMap.TryGetValue(spid, out var target))
                    target.Animation = (string?)ctn.Attribute("presetID") switch { "2" => Animation.Hereinfliegen, "23" or "53" => Animation.Zoomen, _ => Animation.Einblenden };
            }
            if (rels.Values.FirstOrDefault(r => r.Type.EndsWith("/notesSlide", StringComparison.Ordinal)) is { Target.Length: > 0 } nr && pkg.Load(nr.Target) is { } notes)
            {
                var body = notes.Descendants(P + "sp").FirstOrDefault(s => (string?)s.Descendants(P + "ph").FirstOrDefault()?.Attribute("type") == "body");
                if (body is not null) slide.Notes = string.Join('\n', body.Descendants(A + "p").Select(p => string.Concat(p.Descendants(A + "t").Select(t => t.Value)))).Trim('\n');
            }
            doc.Slides.Add(slide);
        }
        if (doc.Slides.Count == 0) doc.AddSlide(SlideLayout.Titel);
        return doc;
    }

    /// <summary>Lage der Platzhalter aus dem Folienlayout (für Dateien anderer Programme, deren Folien keine eigene Lage angeben).</summary>
    static Dictionary<string, (double X, double Y, double W, double H)> LayoutPlaceholders(Pkg pkg, Dictionary<string, (string Type, string Target)> rels)
    {
        var res = new Dictionary<string, (double, double, double, double)>();
        var layout = rels.Values.FirstOrDefault(r => r.Type.EndsWith("/slideLayout", StringComparison.Ordinal)).Target;
        if (layout is null) return res;
        void Take(XDocument? d)
        {
            foreach (var sp in d?.Descendants(P + "sp") ?? [])
            {
                var ph = sp.Descendants(P + "ph").FirstOrDefault(); if (ph is null) continue;
                var key = (string?)ph.Attribute("type") ?? "body";
                if (key is "ctrTitle") key = "title"; if (key is "subTitle") key = "sub"; if (key is "obj") key = "body";
                if (Xfrm(sp.Element(P + "spPr")) is { } x && !res.ContainsKey(key)) res[key] = x;
            }
        }
        Take(pkg.Load(layout));
        var master = pkg.Rels(layout).Values.FirstOrDefault(r => r.Type.EndsWith("/slideMaster", StringComparison.Ordinal)).Target;
        if (master is not null) Take(pkg.Load(master));
        return res;
    }

    static (double X, double Y, double W, double H)? Xfrm(XElement? spPr)
    {
        var x = spPr?.Element(A + "xfrm"); if (x is null) return null;
        var off = x.Element(A + "off"); var ext = x.Element(A + "ext");
        double V(XElement? e, string a) => ((long?)e?.Attribute(a) ?? 0) / (double)Emu;
        return (V(off, "x"), V(off, "y"), V(ext, "cx"), V(ext, "cy"));
    }

    static SlideShape ReadSp(XElement sp, Dictionary<string, (double X, double Y, double W, double H)> layout)
    {
        var nv = sp.Element(P + "nvSpPr"); var cnv = nv?.Element(P + "cNvPr");
        var ph = nv?.Element(P + "nvPr")?.Element(P + "ph");
        var phType = ph is null ? null : (string?)ph.Attribute("type") ?? "body";
        var role = RoleOf((string?)cnv?.Attribute("name")) ?? phType switch { "title" or "ctrTitle" => "title", "subTitle" => "sub", "body" or "obj" => "body", _ => null };
        var spPr = sp.Element(P + "spPr");
        var geom = (string?)spPr?.Element(A + "prstGeom")?.Attribute("prst");
        var descr = (string?)cnv?.Attribute("descr");
        var s = new SlideShape
        {
            Role = role,
            Kind = descr?.StartsWith("media:", StringComparison.Ordinal) == true ? ShapeKind.Medien : geom switch { "ellipse" => ShapeKind.Ellipse, "rightArrow" or "leftArrow" or "upArrow" or "downArrow" => ShapeKind.Pfeil, _ => ShapeKind.Text },
        };
        if (s.Kind == ShapeKind.Medien) s.MediaPath = descr![6..];
        // gefüllte Form ohne Textfeld-Kennung und ohne Platzhalter = Rechteck (bzw. andere Grundform); sonst Textfeld
        if (spPr?.Element(A + "solidFill")?.Element(A + "srgbClr")?.Attribute("val") is { } fill)
        {
            s.Fill = "#" + fill.Value;
            if (s.Kind == ShapeKind.Text && role is null && ph is null && nv?.Element(P + "cNvSpPr")?.Attribute("txBox") is null) s.Kind = ShapeKind.Rechteck;
        }
        var pos = Xfrm(spPr) ?? (phType is not null && layout.TryGetValue(phType is "ctrTitle" ? "title" : phType is "subTitle" ? "sub" : phType is "obj" ? "body" : phType, out var lp) ? lp : (2.3, 5, 29.2, 12));
        (s.X, s.Y, s.W, s.H) = pos;
        var paras = sp.Element(P + "txBody")?.Elements(A + "p").ToList() ?? [];
        var lines = new List<string>(); var bullets = false;
        foreach (var p in paras)
        {
            var ppr = p.Element(A + "pPr"); var lvl = (int?)ppr?.Attribute("lvl") ?? 0;
            if (ppr?.Element(A + "buChar") is not null || ppr?.Element(A + "buAutoNum") is not null || (role is "body" or "body2" && ppr?.Element(A + "buNone") is null)) bullets = true;
            lines.Add((lvl > 0 ? "\t" : "") + string.Concat(p.Elements().Where(e => e.Name.LocalName is "r" or "fld").Select(r => r.Element(A + "t")?.Value ?? "")) + (p.Elements(A + "br").Any() ? "" : ""));
            if (s.Align == Align.Left && (string?)ppr?.Attribute("algn") is { } algn) s.Align = algn switch { "ctr" => Align.Center, "r" => Align.Right, "just" => Align.Justify, _ => Align.Left };
        }
        s.Text = s.Kind == ShapeKind.Medien ? "" : string.Join('\n', lines);
        s.Bullets = bullets && s.Kind == ShapeKind.Text;
        var rpr = sp.Descendants(A + "rPr").FirstOrDefault() ?? sp.Descendants(A + "endParaRPr").FirstOrDefault();
        if ((int?)rpr?.Attribute("sz") is int sz) s.FontSize = sz / 100.0; else s.FontSize = role == "title" ? 36 : role == "sub" ? 22 : 20;
        s.Bold = (string?)rpr?.Attribute("b") is "1" or "true" || (role == "title" && rpr?.Attribute("b") is null && phType is not null);
        if (rpr?.Element(A + "solidFill")?.Element(A + "srgbClr")?.Attribute("val") is { } col) s.Color = "#" + col.Value;
        return s;
    }

    static SlideShape? ReadPic(XElement pic, Dictionary<string, (string Type, string Target)> rels, Pkg pkg)
    {
        var embed = (string?)pic.Descendants(A + "blip").FirstOrDefault()?.Attribute(R + "embed");
        if (embed is null || !rels.TryGetValue(embed, out var r) || pkg.Bytes(r.Target) is not { } bytes) return null;
        var s = new SlideShape { Kind = ShapeKind.Bild, Image = bytes };
        if (Xfrm(pic.Element(P + "spPr")) is { } x) (s.X, s.Y, s.W, s.H) = x;
        return s;
    }

    static SlideShape? ReadFrame(XElement gf, Dictionary<string, (string Type, string Target)> rels, Pkg pkg)
    {
        var x = gf.Element(P + "xfrm"); var off = x?.Element(A + "off"); var ext = x?.Element(A + "ext");
        double V(XElement? e, string a) => ((long?)e?.Attribute(a) ?? 0) / (double)Emu;
        var s = new SlideShape { X = V(off, "x"), Y = V(off, "y"), W = V(ext, "cx"), H = V(ext, "cy") };
        if (gf.Descendants(A + "tbl").FirstOrDefault() is { } tbl)
        {
            s.Kind = ShapeKind.Tabelle;
            s.Table = tbl.Elements(A + "tr").Select(tr => tr.Elements(A + "tc").Select(tc => string.Join('\n', tc.Descendants(A + "p").Select(p => string.Concat(p.Descendants(A + "t").Select(t => t.Value))))).ToList()).ToList();
            if ((int?)tbl.Descendants(A + "rPr").FirstOrDefault()?.Attribute("sz") is int sz) s.FontSize = sz / 100.0; else s.FontSize = 16;
            return s;
        }
        var cid = (string?)gf.Descendants(C + "chart").FirstOrDefault()?.Attribute(R + "id");
        if (cid is not null && rels.TryGetValue(cid, out var r) && pkg.Load(r.Target) is { } chart)
        {
            s.Kind = ShapeKind.Diagramm;
            var plot = chart.Descendants(C + "plotArea").FirstOrDefault();
            s.ChartKind = plot?.Elements().Any(e => e.Name.LocalName is "pieChart" or "pie3DChart" or "doughnutChart") == true ? ChartKind.Kreis : plot?.Elements().Any(e => e.Name.LocalName is "lineChart" or "areaChart" or "scatterChart") == true ? ChartKind.Linie : ChartKind.Balken;
            var ser = chart.Descendants(C + "ser").FirstOrDefault();
            s.ChartLabels = ser?.Element(C + "cat")?.Descendants(C + "pt").Select(p => p.Element(C + "v")?.Value ?? "").ToList() ?? [];
            s.ChartValues = ser?.Element(C + "val")?.Descendants(C + "pt").Select(p => double.TryParse(p.Element(C + "v")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0).ToList() ?? [];
            s.Text = string.Concat(chart.Root?.Element(C + "chart")?.Element(C + "title")?.Descendants(A + "t").Select(t => t.Value) ?? []);
            return s;
        }
        return null;
    }

    // ================= ODP lesen =================
    static readonly XNamespace Od = "urn:oasis:names:tc:opendocument:xmlns:office:1.0", Dr = "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0",
        Svg = "urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0", Tx = "urn:oasis:names:tc:opendocument:xmlns:text:1.0",
        Pres = "urn:oasis:names:tc:opendocument:xmlns:presentation:1.0", Tb = "urn:oasis:names:tc:opendocument:xmlns:table:1.0", Xl = "http://www.w3.org/1999/xlink",
        St = "urn:oasis:names:tc:opendocument:xmlns:style:1.0", Fo = "urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0", Smil = "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0";

    static double Cm(string? v)
    {
        if (string.IsNullOrEmpty(v)) return 0;
        var num = new string(v.TakeWhile(ch => char.IsDigit(ch) || ch is '.' or '-').ToArray()); var unit = v[num.Length..];
        var d = double.TryParse(num, NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ? x : 0;
        return unit switch { "mm" => d / 10, "in" => d * 2.54, "pt" => d * 2.54 / 72, _ => d };
    }

    public static SlideDoc ReadOdp(string path)
    {
        using var z = ZipFile.OpenRead(path);
        var pkg = new Pkg(z);
        var content = pkg.Load("content.xml") ?? throw new InvalidDataException("Keine Impress-Datei (content.xml fehlt).");
        var doc = new SlideDoc();
        // automatische Stile: Füllung von Formen und Folien, Übergänge, Schrift von Textstellen
        var styles = content.Root?.Element(Od + "automatic-styles")?.Elements(St + "style").Where(s => s.Attribute(St + "name") is not null)
            .ToDictionary(s => (string)s.Attribute(St + "name")!, s => s) ?? [];
        XElement? Props(string? style, string kind) => style is not null && styles.TryGetValue(style, out var s) ? s.Element(St + kind) : null;
        foreach (var page in content.Descendants(Dr + "page"))
        {
            var slide = new Slide { Layout = SlideLayout.Leer };
            var dp = Props((string?)page.Attribute(Dr + "style-name"), "drawing-page-properties");
            if ((string?)dp?.Attribute(Dr + "fill") == "solid" && (string?)dp.Attribute(Dr + "fill-color") is { } bgc) slide.Background = bgc.ToUpperInvariant();
            slide.Transition = (string?)dp?.Attribute(Smil + "type") switch { null => Transition.Keine, "fade" => Transition.Ueberblenden, "pushWipe" or "slideWipe" => Transition.Schieben, _ => Transition.Wischen };
            foreach (var el in page.Elements())
            {
                if (el.Name == Pres + "notes")
                {
                    slide.Notes = string.Join('\n', el.Descendants(Dr + "text-box").SelectMany(b => b.Descendants(Tx + "p")).Select(p => p.Value)).Trim();
                    continue;
                }
                var s = new SlideShape { X = Cm((string?)el.Attribute(Svg + "x")), Y = Cm((string?)el.Attribute(Svg + "y")), W = Cm((string?)el.Attribute(Svg + "width")), H = Cm((string?)el.Attribute(Svg + "height")) };
                var cls = (string?)el.Attribute(Pres + "class");
                s.Role = cls switch { "title" => "title", "subtitle" => "sub", "outline" => slide.ByRole("body") is null ? "body" : "body2", _ => null };
                if (el.Name == Dr + "frame")
                {
                    // Reihenfolge wichtig: Tabellen und Diagramme haben in Impress zusätzlich ein Ersatzbild
                    if (el.Element(Tb + "table") is { } tbl)
                    {
                        s.Kind = ShapeKind.Tabelle; s.FontSize = 16;
                        s.Table = tbl.Descendants(Tb + "table-row").Select(r => r.Elements(Tb + "table-cell").Select(c => string.Join('\n', c.Descendants(Tx + "p").Select(p => p.Value))).ToList()).ToList();
                    }
                    else if (el.Element(Dr + "object") is { } obj && ReadOdpChart(pkg, (string?)obj.Attribute(Xl + "href"), s)) { }
                    else if (el.Element(Dr + "image") is { } img && (string?)img.Attribute(Xl + "href") is { } href && pkg.Bytes(href.TrimStart('.', '/')) is { } bytes) { s.Kind = ShapeKind.Bild; s.Image = bytes; }
                    else if (el.Element(Dr + "text-box") is { } box) FillText(s, box, cls);
                    else continue;
                }
                else if (el.Name.LocalName is "custom-shape" or "rect" or "ellipse")
                {
                    var type = ((string?)el.Descendants(Dr + "enhanced-geometry").FirstOrDefault()?.Attribute(Dr + "type") ?? "").ToLowerInvariant();
                    var gp = Props((string?)el.Attribute(Dr + "style-name"), "graphic-properties");
                    var filled = (string?)gp?.Attribute(Dr + "fill") is "solid" or null && gp?.Attribute(Dr + "fill-color") is not null;
                    s.Role ??= RoleOf((string?)el.Attribute(Dr + "name"));
                    FillText(s, el, s.Role switch { "title" => "title", "sub" => "subtitle", "body" or "body2" => "outline", _ => null });
                    s.Kind = el.Name.LocalName == "ellipse" || type.Contains("ellipse") ? ShapeKind.Ellipse : type.Contains("arrow") ? ShapeKind.Pfeil : filled && s.Role is null ? ShapeKind.Rechteck : ShapeKind.Text;
                    if (filled) s.Fill = ((string)gp!.Attribute(Dr + "fill-color")!).ToUpperInvariant();
                    if (s.Kind != ShapeKind.Text) { s.Bullets = false; if (s.Role is null) s.Align = Align.Center; }
                    // Schrift der ersten Textstelle
                    var span = el.Descendants(Tx + "span").FirstOrDefault();
                    var tp = Props((string?)span?.Attribute(Tx + "style-name"), "text-properties");
                    if ((string?)tp?.Attribute(Fo + "font-size") is { } fsz && fsz.EndsWith("pt", StringComparison.Ordinal) && double.TryParse(fsz[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var pt)) s.FontSize = pt;
                    if ((string?)tp?.Attribute(Fo + "font-weight") is { } fw) s.Bold = fw == "bold";
                    if ((string?)tp?.Attribute(Fo + "color") is { } fc) s.Color = fc.ToUpperInvariant();
                    var pp = Props((string?)el.Elements(Tx + "p").FirstOrDefault()?.Attribute(Tx + "style-name"), "paragraph-properties");
                    if ((string?)pp?.Attribute(Fo + "text-align") is { } ta) s.Align = ta switch { "center" => Align.Center, "end" or "right" => Align.Right, "justify" => Align.Justify, _ => Align.Left };
                }
                else continue;
                slide.Shapes.Add(s);
            }
            slide.Layout = slide.ByRole("sub") is not null ? SlideLayout.Titel : slide.ByRole("body2") is not null ? SlideLayout.ZweiSpalten : slide.ByRole("body") is not null ? SlideLayout.TitelInhalt : slide.ByRole("title") is not null ? SlideLayout.NurTitel : SlideLayout.Leer;
            doc.Slides.Add(slide);
        }
        if (doc.Slides.Count == 0) doc.AddSlide(SlideLayout.Titel);
        return doc;
    }

    static readonly XNamespace Ch = "urn:oasis:names:tc:opendocument:xmlns:chart:1.0";

    /// <summary>Eingebettetes Impress-Diagramm: Art und die Werte der ersten Reihe aus der Datentabelle des Objekts.</summary>
    static bool ReadOdpChart(Pkg pkg, string? href, SlideShape s)
    {
        if (href is null) return false;
        var dir = href.TrimStart('.', '/').TrimEnd('/');
        if (pkg.Load(dir + "/content.xml") is not { } c || c.Descendants(Ch + "chart").FirstOrDefault() is not { } chart) return false;
        s.Kind = ShapeKind.Diagramm;
        s.ChartKind = ((string?)chart.Attribute(Ch + "class") ?? "") switch { "chart:circle" or "chart:ring" => ChartKind.Kreis, "chart:line" or "chart:area" or "chart:scatter" => ChartKind.Linie, _ => ChartKind.Balken };
        s.Text = string.Concat(chart.Element(Ch + "title")?.Descendants(Tx + "p").Select(p => p.Value) ?? []);
        var rows = chart.Descendants(Tb + "table-rows").FirstOrDefault()?.Elements(Tb + "table-row").ToList() ?? [];
        s.ChartLabels = rows.Select(r => r.Elements(Tb + "table-cell").FirstOrDefault()?.Descendants(Tx + "p").FirstOrDefault()?.Value ?? "").ToList();
        s.ChartValues = rows.Select(r => double.TryParse((string?)r.Elements(Tb + "table-cell").Skip(1).FirstOrDefault()?.Attribute(Od + "value"), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0).ToList();
        return true;
    }

    static void FillText(SlideShape s, XElement box, string? cls)
    {
        var lines = new List<string>(); var bullets = false;
        foreach (var el in box.Elements())
        {
            if (el.Name == Tx + "list")
            {
                bullets = true;
                foreach (var item in el.Descendants(Tx + "list-item"))
                {
                    var depth = item.Ancestors(Tx + "list").TakeWhile(a => a != box).Count();
                    foreach (var p in item.Elements(Tx + "p")) lines.Add((depth > 1 ? "\t" : "") + p.Value);
                }
            }
            else if (el.Name == Tx + "p" || el.Name == Tx + "h") lines.Add(el.Value);
        }
        s.Kind = ShapeKind.Text; s.Text = string.Join('\n', lines); s.Bullets = bullets || cls == "outline";
        s.FontSize = cls switch { "title" => 36, "subtitle" => 22, _ => 20 }; s.Bold = cls == "title";
        if (cls == "subtitle" || cls == "title" && s.Y > 4) s.Align = Align.Center;
    }
}
