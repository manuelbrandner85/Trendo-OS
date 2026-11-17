using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;

namespace Trendo.Core.Office;

public static partial class SlideFiles
{
    // ================= PPTX lesen =================
    sealed class Pkg(ZipArchive z)
    {
        public ZipArchive Zip => z;
        public XDocument? Load(string name) { var e = z.GetEntry(name); if (e is null) return null; return e.LoadXml(); }
        public byte[]? Bytes(string name) { var e = z.GetEntry(name); if (e is null) return null; return e.ReadBytes(); }
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
        using var z = OfficeZip.OpenRead(path);
        var pkg = new Pkg(z);
        var pres = pkg.Load("ppt/presentation.xml") ?? throw new InvalidDataException("Keine PowerPoint-Datei (presentation.xml fehlt).");
        var presRels = pkg.Rels("ppt/presentation.xml");
        var size = pres.Root!.Element(P + "sldSz");
        double sx = SlideDoc.WidthCm / (((long?)size?.Attribute("cx") ?? 12192000) / (double)Emu), sy = SlideDoc.HeightCm / (((long?)size?.Attribute("cy") ?? 6858000) / (double)Emu);
        var doc = new SlideDoc();
        // Design: Farben aus dem Thema, wenn es zu einem eigenen passt
        XDocument? themeDoc = null;
        if (presRels.Values.FirstOrDefault(r => r.Type.EndsWith("/theme", StringComparison.Ordinal)) is { Target.Length: > 0 } tr && pkg.Load(tr.Target) is { } theme)
        {
            themeDoc = theme;
            var name = (string?)theme.Root?.Attribute("name");
            if (SlideThemes.All.FirstOrDefault(t => t.Name == name) is { } known) doc.Theme = known;
        }
        // T-0701: fremdes Design (Folienmaster, Layouts, Theme) bleibt beim Speichern erhalten, statt durch Trendos Design ersetzt zu werden
        var colors = SchemeColors(themeDoc);
        if (themeDoc is null || SlideThemes.All.All(t => t.Name != (string?)themeDoc.Root?.Attribute("name")))
        {
            doc.Design = ReadDesign(pkg, pres.Root, presRels, themeDoc, colors);
            if (doc.Design is not null) doc.Theme = doc.Design.Theme;
        }
        var masterStyles = new Dictionary<string, MasterText>();
        foreach (var sid in pres.Root.Element(P + "sldIdLst")?.Elements(P + "sldId") ?? [])
        {
            var rid = (string?)sid.Attribute(R + "id");
            if (rid is null || !presRels.TryGetValue(rid, out var rel)) continue;
            var sx2 = pkg.Load(rel.Target); if (sx2?.Root is null) continue;
            var rels = pkg.Rels(rel.Target);
            var slide = new Slide { Layout = SlideLayout.Leer };
            var bgEl = sx2.Root.Element(P + "cSld")?.Element(P + "bg");
            if (bgEl?.Descendants(A + "srgbClr").FirstOrDefault()?.Attribute("val") is { } bgv) slide.Background = "#" + bgv.Value;
            if (bgEl is not null) { slide.SourceBg = Raw(bgEl); slide.SourceBgColor = slide.Background; }
            var layoutShapes = LayoutPlaceholders(pkg, rels);
            var layoutPart = rels.Values.FirstOrDefault(r => r.Type.EndsWith("/slideLayout", StringComparison.Ordinal)).Target;
            slide.SourceLayout = layoutPart;
            var masterPart = layoutPart is null ? null : pkg.Rels(layoutPart).Values.FirstOrDefault(r => r.Type.EndsWith("/slideMaster", StringComparison.Ordinal)).Target;
            if (!masterStyles.TryGetValue(masterPart ?? "", out var mt)) masterStyles[masterPart ?? ""] = mt = MasterTextOf(masterPart is null ? null : pkg.Load(masterPart));
            var idMap = new Dictionary<string, SlideShape>();
            foreach (var el in sx2.Root.Element(P + "cSld")?.Element(P + "spTree")?.Elements() ?? [])
            {
                var shape = el.Name.LocalName switch
                {
                    "sp" => ReadSp(el, layoutShapes, mt, colors),
                    "pic" => ReadPic(el, rels, pkg),
                    "graphicFrame" => ReadFrame(el, rels, pkg),
                    _ => null,
                };
                if (shape is null) continue;
                shape.X *= sx; shape.W *= sx; shape.Y *= sy; shape.H *= sy;
                slide.Shapes.Add(shape);
                if (el.Descendants(P + "cNvPr").FirstOrDefault()?.Attribute("id") is { } idAttr)
                {
                    idMap[idAttr.Value] = shape;
                    if (int.TryParse(idAttr.Value, out var nid) && nid > 1) shape.SourceId = nid;
                }
            }
            slide.Layout = slide.ByRole("sub") is not null ? SlideLayout.Titel : slide.ByRole("body2") is not null ? SlideLayout.ZweiSpalten : slide.ByRole("body") is not null ? SlideLayout.TitelInhalt : slide.ByRole("title") is not null ? SlideLayout.NurTitel : SlideLayout.Leer;
            var trEl = sx2.Root.Element(P + "transition");
            slide.Transition = trEl?.Elements().FirstOrDefault()?.Name.LocalName switch { "fade" => Transition.Ueberblenden, "push" or "cover" or "pull" => Transition.Schieben, "wipe" or "split" or "wheel" => Transition.Wischen, null => Transition.Keine, _ => Transition.Ueberblenden };
            // T-0701: Übergang und Animationen der Datei unverändert merken (auch neuere Übergänge in mc:AlternateContent)
            XNamespace mc = "http://schemas.openxmlformats.org/markup-compatibility/2006";
            var trRaw = sx2.Root.Elements(mc + "AlternateContent").FirstOrDefault(a => a.Descendants(P + "transition").Any()) ?? trEl;
            if (trRaw is not null)
            {
                if (trEl is null) slide.Transition = Transition.Ueberblenden;
                slide.SourceTransition = Raw(trRaw); slide.SourceTransitionKind = slide.Transition;
            }
            var timing = sx2.Root.Element(P + "timing");
            foreach (var ctn in timing?.Descendants(P + "cTn").Where(c => (string?)c.Attribute("presetClass") == "entr") ?? [])
            {
                var spid = ctn.Descendants(P + "spTgt").FirstOrDefault()?.Attribute("spid")?.Value;
                if (spid is not null && idMap.TryGetValue(spid, out var target))
                    target.Animation = (string?)ctn.Attribute("presetID") switch { "2" => Animation.Hereinfliegen, "23" or "53" => Animation.Zoomen, _ => Animation.Einblenden };
            }
            if (timing is not null) { slide.SourceTiming = Raw(timing); slide.SourceAnimSig = slide.AnimSignature(); }
            if (rels.Values.FirstOrDefault(r => r.Type.EndsWith("/notesSlide", StringComparison.Ordinal)) is { Target.Length: > 0 } nr && pkg.Load(nr.Target) is { } notes)
            {
                var body = notes.Descendants(P + "sp").FirstOrDefault(s => (string?)s.Descendants(P + "ph").FirstOrDefault()?.Attribute("type") == "body");
                if (body is not null) slide.Notes = string.Join('\n', body.Outermost(A + "p").Select(p => string.Concat(p.Outermost(A + "t").Select(t => t.Value)))).Trim('\n');
            }
            doc.Slides.Add(slide);
        }
        if (doc.Slides.Count == 0) doc.AddSlide(SlideLayout.Titel);
        return doc;
    }

    // ---------------- T-0701: fremdes Design ----------------

    /// <summary>Teil einer Datei unverändert als Text – mit allen Namensraum-Kürzeln der Datei (p:, a:, r:, p14: …), damit er an anderer Stelle gleich gelesen wird.</summary>
    static string Raw(XElement el)
    {
        var c = new XElement(el);
        foreach (var a in el.AncestorsAndSelf().Reverse().SelectMany(x => x.Attributes()).Where(a => a.IsNamespaceDeclaration)) c.SetAttributeValue(a.Name, a.Value);
        return c.ToString(SaveOptions.DisableFormatting);
    }

    /// <summary>Schriftgröße und Fett aus den Textformaten des Folienmasters (Titel, Inhalt, Sonstiges) – das erben Felder ohne eigene Angabe.</summary>
    sealed record MasterText(double? Title, double? Body, double? Other, bool? TitleBold, bool? BodyBold, bool? OtherBold);

    static MasterText MasterTextOf(XDocument? master)
    {
        var st = master?.Root?.Element(P + "txStyles");
        XElement? D(string n) => st?.Element(P + n)?.Element(A + "lvl1pPr")?.Element(A + "defRPr");
        double? Sz(XElement? d) => (int?)d?.Attribute("sz") is int v ? v / 100.0 : null;
        bool? B(XElement? d) => (string?)d?.Attribute("b") is { } b ? b is "1" or "true" : null;
        return new(Sz(D("titleStyle")), Sz(D("bodyStyle")), Sz(D("otherStyle")), B(D("titleStyle")), B(D("bodyStyle")), B(D("otherStyle")));
    }

    /// <summary>Farben des Designs nach Namen (dk1, lt1, accent1 …; tx1/bg1/tx2/bg2 wie in der Standardzuordnung).</summary>
    static Dictionary<string, string> SchemeColors(XDocument? theme)
    {
        var map = new Dictionary<string, string>();
        foreach (var e in theme?.Root?.Descendants(A + "clrScheme").FirstOrDefault()?.Elements() ?? [])
        {
            var c = e.Elements().FirstOrDefault();
            var v = (string?)c?.Attribute("lastClr") ?? (string?)c?.Attribute("val");
            if (v is { Length: 6 }) map[e.Name.LocalName] = "#" + v.ToUpperInvariant();
        }
        foreach (var (alias, name) in new[] { ("tx1", "dk1"), ("bg1", "lt1"), ("tx2", "dk2"), ("bg2", "lt2") }) if (map.TryGetValue(name, out var c)) map[alias] = c;
        return map;
    }

    /// <summary>Farbe einer Füllung als #RRGGBB: feste Farbe, Systemfarbe oder Designfarbe (mit Aufhellen/Abdunkeln über lumMod/lumOff).</summary>
    static string? FillColor(XElement? solidFill, Dictionary<string, string> scheme)
    {
        var c = solidFill?.Elements().FirstOrDefault(); if (c is null) return null;
        string? hex = c.Name.LocalName switch
        {
            "srgbClr" => (string?)c.Attribute("val") is { Length: 6 } v ? "#" + v.ToUpperInvariant() : null,
            "sysClr" => (string?)c.Attribute("lastClr") is { Length: 6 } l ? "#" + l.ToUpperInvariant() : null,
            "schemeClr" => scheme.GetValueOrDefault((string?)c.Attribute("val") ?? ""),
            _ => null,
        };
        if (hex is null) return null;
        var mod = (int?)c.Element(A + "lumMod")?.Attribute("val"); var off = (int?)c.Element(A + "lumOff")?.Attribute("val");
        if (mod is null && off is null) return hex;
        double r = Convert.ToInt32(hex[1..3], 16) / 255.0, g = Convert.ToInt32(hex[3..5], 16) / 255.0, b = Convert.ToInt32(hex[5..7], 16) / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), l2 = (max + min) / 2, h = 0, s = 0;
        if (max != min) { var d = max - min; s = l2 > 0.5 ? d / (2 - max - min) : d / (max + min); h = (max == r ? (g - b) / d + (g < b ? 6 : 0) : max == g ? (b - r) / d + 2 : (r - g) / d + 4) / 6; }
        l2 = Math.Clamp(l2 * (mod ?? 100000) / 100000.0 + (off ?? 0) / 100000.0, 0, 1);
        static double Hue(double p, double q, double t) { if (t < 0) t += 1; if (t > 1) t -= 1; return t < 1 / 6.0 ? p + (q - p) * 6 * t : t < 0.5 ? q : t < 2 / 3.0 ? p + (q - p) * (2 / 3.0 - t) * 6 : p; }
        if (s == 0) r = g = b = l2; else { var q = l2 < 0.5 ? l2 * (1 + s) : l2 + s - l2 * s; var p = 2 * l2 - q; r = Hue(p, q, h + 1 / 3.0); g = Hue(p, q, h); b = Hue(p, q, h - 1 / 3.0); }
        static string X(double v) => ((int)Math.Round(v * 255)).ToString("X2", CultureInfo.InvariantCulture);
        return "#" + X(r) + X(g) + X(b);
    }

    static readonly string[] NotDesign = ["/slide", "/notesSlide", "/notesMaster", "/handoutMaster", "/presProps", "/viewProps", "/tableStyles", "/comments", "/commentAuthors"];

    /// <summary>Folienmaster samt allem, worauf sie zeigen (Layouts, Theme, Bilder), unverändert einsammeln.</summary>
    static PptxDesign? ReadDesign(Pkg pkg, XElement pres, Dictionary<string, (string Type, string Target)> presRels, XDocument? theme, Dictionary<string, string> scheme)
    {
        var masters = new List<(string Id, string Part)>();
        foreach (var m in pres.Element(P + "sldMasterIdLst")?.Elements(P + "sldMasterId") ?? [])
            if ((string?)m.Attribute(R + "id") is { } rid && presRels.TryGetValue(rid, out var rel) && pkg.Zip.GetEntry(rel.Target) is not null)
                masters.Add(((string?)m.Attribute("id") ?? (2147483648L + masters.Count * 100).ToString(CultureInfo.InvariantCulture), rel.Target));
        if (masters.Count == 0) return null;
        var parts = new Dictionary<string, byte[]>(); var queue = new Queue<string>(masters.Select(m => m.Part));
        static string RelsOf(string part) { var i = part.LastIndexOf('/'); return $"{part[..Math.Max(0, i)]}/_rels/{part[(i + 1)..]}.rels"; }
        while (queue.Count > 0 && parts.Count < OfficeZip.MaxEntries)
        {
            var part = queue.Dequeue(); if (parts.ContainsKey(part) || pkg.Bytes(part) is not { } bytes) continue;
            parts[part] = bytes;
            if (pkg.Bytes(RelsOf(part)) is { } rb) parts[RelsOf(part)] = rb;
            foreach (var (type, target) in pkg.Rels(part).Values)
                if (!NotDesign.Any(n => type.EndsWith(n, StringComparison.Ordinal)) && !parts.ContainsKey(target) && pkg.Zip.GetEntry(target) is not null) queue.Enqueue(target);
        }
        var ct = pkg.Load("[Content_Types].xml")?.Root; XNamespace ctn = "http://schemas.openxmlformats.org/package/2006/content-types";
        var types = ct?.Elements(ctn + "Override").Where(o => o.Attribute("PartName") is not null && o.Attribute("ContentType") is not null)
            .GroupBy(o => ((string)o.Attribute("PartName")!).TrimStart('/')).ToDictionary(g => g.Key, g => (string)g.First().Attribute("ContentType")!) ?? [];
        var defaults = ct?.Elements(ctn + "Default").Where(o => o.Attribute("Extension") is not null && o.Attribute("ContentType") is not null)
            .GroupBy(o => ((string)o.Attribute("Extension")!).ToLowerInvariant()).ToDictionary(g => g.Key, g => (string)g.First().Attribute("ContentType")!) ?? [];
        var layouts = new Dictionary<string, string>(); string? first = null;
        foreach (var lid in pkg.Load(masters[0].Part)?.Root?.Element(P + "sldLayoutIdLst")?.Elements(P + "sldLayoutId") ?? [])
            if ((string?)lid.Attribute(R + "id") is { } r && pkg.Rels(masters[0].Part).TryGetValue(r, out var lr) && parts.ContainsKey(lr.Target)) { first ??= lr.Target; }
        foreach (var part in parts.Keys.Where(p => p.Contains("/slideLayouts/", StringComparison.Ordinal) && !p.Contains("/_rels/", StringComparison.Ordinal)).Order(StringComparer.Ordinal))
            if (pkg.Load(part)?.Root is { } lroot) layouts.TryAdd((string?)lroot.Attribute("type") ?? "cust", part);
        var themePart = presRels.Values.FirstOrDefault(r => r.Type.EndsWith("/theme", StringComparison.Ordinal)).Target is { } tp && parts.ContainsKey(tp) ? tp
            : pkg.Rels(masters[0].Part).Values.FirstOrDefault(r => r.Type.EndsWith("/theme", StringComparison.Ordinal)).Target;
        // Anzeige in Trendo: Hintergrund, Text- und Akzentfarbe sowie Schrift aus Theme und Master
        var master = pkg.Load(masters[0].Part)?.Root;
        var clrMap = master?.Element(P + "clrMap");
        string Map(string key, string dflt) => scheme.GetValueOrDefault((string?)clrMap?.Attribute(key) ?? dflt) ?? scheme.GetValueOrDefault(dflt) ?? (key.StartsWith("bg", StringComparison.Ordinal) ? "#FFFFFF" : "#000000");
        var bg = master?.Element(P + "cSld")?.Element(P + "bg")?.Element(P + "bgPr")?.Element(A + "solidFill") is { } bf ? FillColor(bf, scheme) : null;
        var font = (string?)theme?.Root?.Descendants(A + "minorFont").FirstOrDefault()?.Element(A + "latin")?.Attribute("typeface");
        var size = pres.Element(P + "sldSz");
        var derived = new SlideTheme("fremd", (string?)theme?.Root?.Attribute("name") is { Length: > 0 } n ? n : "Design der Datei", bg ?? Map("bg1", "lt1"), Map("tx1", "dk1"), Map("tx1", "dk1"),
            scheme.GetValueOrDefault("accent1") ?? "#4472C4", font is { Length: > 0 } && !font.StartsWith('+') ? font : "Calibri", Map("bg2", "lt2"));
        return new PptxDesign
        {
            Theme = derived, Parts = parts, ContentTypes = types, Defaults = defaults, Masters = masters, ThemePart = themePart,
            Cx = (long?)size?.Attribute("cx") ?? 12192000, Cy = (long?)size?.Attribute("cy") ?? 6858000, SizeType = (string?)size?.Attribute("type"),
            DefaultTextStyle = pres.Element(P + "defaultTextStyle") is { } dts ? Raw(dts) : null, LayoutByType = layouts, FirstLayout = first ?? layouts.Values.FirstOrDefault(),
        };
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

    static SlideShape ReadSp(XElement sp, Dictionary<string, (double X, double Y, double W, double H)> layout, MasterText? master = null, Dictionary<string, string>? scheme = null)
    {
        scheme ??= [];
        var nv = sp.Element(P + "nvSpPr"); var cnv = nv?.Element(P + "cNvPr");
        var ph = nv?.Element(P + "nvPr")?.Element(P + "ph");
        var phType = ph is null ? null : (string?)ph.Attribute("type") ?? "body";
        var phTypeRaw = ph is null ? null : (string?)ph.Attribute("type") ?? ""; var phIdx = (string?)ph?.Attribute("idx");
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
        s.PhType = phTypeRaw; s.PhIdx = phIdx;
        // T-0701: geerbte Schrift aus dem Folienmaster (Titel, Inhalt, Sonstiges)
        var isTitle = phType is "title" or "ctrTitle"; var isBody = phType is not null && !isTitle && phType is "body" or "obj" or "subTitle";
        var inheritSize = (isTitle ? master?.Title : isBody ? master?.Body : master?.Other) ?? (role == "title" ? 36 : role == "sub" ? 22 : 20);
        var inheritBold = (isTitle ? master?.TitleBold : isBody ? master?.BodyBold : master?.OtherBold) ?? (role == "title" && phType is not null && master?.Title is null);
        // Textabschnitte mit eigenem Format
        var runs = new List<TextRun>();
        TextRun Look(XElement? r)
        {
            var u = (string?)r?.Attribute("u");
            return new TextRun("", (int?)r?.Attribute("sz") is int z ? z / 100.0 : null, (string?)r?.Attribute("b") is { } b ? b is "1" or "true" : null, (string?)r?.Attribute("i") is { } i ? i is "1" or "true" : null,
                u is null or "none" ? null : u, FillColor(r?.Element(A + "solidFill"), scheme), (string?)r?.Element(A + "latin")?.Attribute("typeface"));
        }
        for (var pi = 0; pi < paras.Count; pi++)
        {
            if (pi > 0) runs.Add((runs.Count > 0 ? runs[^1] : new TextRun("")) with { Text = "\n" });
            var lvl = (int?)paras[pi].Element(A + "pPr")?.Attribute("lvl") ?? 0;
            var parts = paras[pi].Elements().Where(e => e.Name.LocalName is "r" or "fld").Select(r => Look(r.Element(A + "rPr")) with { Text = r.Element(A + "t")?.Value ?? "" }).Where(r => r.Text.Length > 0).ToList();
            if (lvl > 0) runs.Add((parts.FirstOrDefault() ?? new TextRun("")) with { Text = "\t" });
            runs.AddRange(parts);
        }
        var looks = runs.Where(r => r.Text is not ("\n" or "\t")).Select(r => r.Bare()).Distinct().ToList();
        var mixed = s.Kind != ShapeKind.Medien && (looks.Count > 1 || looks.Any(l => l.Italic is not null || l.Underline is not null || l.Font is not null)) && string.Concat(runs.Select(r => r.Text)) == s.Text;
        var rpr = sp.Descendants(A + "rPr").FirstOrDefault() ?? sp.Descendants(A + "endParaRPr").FirstOrDefault();
        if (mixed)
        {
            // Feld = geerbtes Format, Abschnitte = ihre eigenen Angaben
            s.FontSize = inheritSize; s.Bold = inheritBold; s.SizeInherited = true; s.BoldInherited = true;
            s.Runs = TextRuns.Normalize(runs);
        }
        else
        {
            if ((int?)rpr?.Attribute("sz") is int sz) s.FontSize = sz / 100.0; else { s.FontSize = inheritSize; s.SizeInherited = true; }
            if ((string?)rpr?.Attribute("b") is { } bv) s.Bold = bv is "1" or "true"; else { s.Bold = inheritBold; s.BoldInherited = true; }
            if (FillColor(rpr?.Element(A + "solidFill"), scheme) is { } col) s.Color = col;
        }
        return s;
    }

    static SlideShape? ReadPic(XElement pic, Dictionary<string, (string Type, string Target)> rels, Pkg pkg)
    {
        var embed = (string?)pic.Descendants(A + "blip").FirstOrDefault()?.Attribute(R + "embed");
        if (embed is null || !rels.TryGetValue(embed, out var r) || pkg.Bytes(r.Target) is not { } bytes) return null;
        var s = new SlideShape { Kind = ShapeKind.Bild, Image = bytes };
        if (pic.Element(P + "nvPicPr")?.Element(P + "nvPr")?.Element(P + "ph") is { } ph) { s.PhType = (string?)ph.Attribute("type") ?? ""; s.PhIdx = (string?)ph.Attribute("idx"); }
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
            s.Table = tbl.Elements(A + "tr").Select(tr => tr.Elements(A + "tc").Select(tc => string.Join('\n', tc.Outermost(A + "p").Select(p => string.Concat(p.Outermost(A + "t").Select(t => t.Value))))).ToList()).ToList();
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
            s.Text = string.Concat(chart.Root?.Element(C + "chart")?.Element(C + "title")?.Outermost(A + "t").Select(t => t.Value) ?? []);
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
        using var z = OfficeZip.OpenRead(path);
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
                    slide.Notes = string.Join('\n', el.Descendants(Dr + "text-box").SelectMany(b => b.Descendants(Tx + "p")).Select(p => p.Value).WithinTextBudget(z)).Trim();
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
                        s.Table = tbl.Descendants(Tb + "table-row").Select(r => r.Elements(Tb + "table-cell").Select(c => string.Join('\n', c.Descendants(Tx + "p").Select(p => p.Value).WithinTextBudget(z))).ToList()).ToList();
                    }
                    else if (el.Element(Dr + "object") is { } obj && ReadOdpChart(pkg, (string?)obj.Attribute(Xl + "href"), s)) { }
                    else if (el.Element(Dr + "image") is { } img && (string?)img.Attribute(Xl + "href") is { } href && pkg.Bytes(href.TrimStart('.', '/')) is { } bytes) { s.Kind = ShapeKind.Bild; s.Image = bytes; }
                    else if (el.Element(Dr + "text-box") is { } box) FillText(s, box, cls, z);
                    else continue;
                }
                else if (el.Name.LocalName is "custom-shape" or "rect" or "ellipse")
                {
                    var type = ((string?)el.Descendants(Dr + "enhanced-geometry").FirstOrDefault()?.Attribute(Dr + "type") ?? "").ToLowerInvariant();
                    var gp = Props((string?)el.Attribute(Dr + "style-name"), "graphic-properties");
                    var filled = (string?)gp?.Attribute(Dr + "fill") is "solid" or null && gp?.Attribute(Dr + "fill-color") is not null;
                    s.Role ??= RoleOf((string?)el.Attribute(Dr + "name"));
                    FillText(s, el, s.Role switch { "title" => "title", "sub" => "subtitle", "body" or "body2" => "outline", _ => null }, z);
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
        s.Text = chart.Element(Ch + "title") is { } title ? string.Concat(title.Descendants(Tx + "p").Select(p => p.Value).WithinTextBudget(pkg.Zip)) : "";
        var rows = chart.Descendants(Tb + "table-rows").FirstOrDefault()?.Elements(Tb + "table-row").ToList() ?? [];
        s.ChartLabels = rows.Select(r => r.Elements(Tb + "table-cell").FirstOrDefault()?.Descendants(Tx + "p").FirstOrDefault()?.Value ?? "").ToList();
        s.ChartValues = rows.Select(r => double.TryParse((string?)r.Elements(Tb + "table-cell").Skip(1).FirstOrDefault()?.Attribute(Od + "value"), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0).ToList();
        return true;
    }

    static void FillText(SlideShape s, XElement box, string? cls, ZipArchive z)
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
                    foreach (var p in item.Elements(Tx + "p")) lines.Add((depth > 1 ? "\t" : "") + z.Charge(p.Value));
                }
            }
            else if (el.Name == Tx + "p" || el.Name == Tx + "h") lines.Add(z.Charge(el.Value));
        }
        s.Kind = ShapeKind.Text; s.Text = string.Join('\n', lines); s.Bullets = bullets || cls == "outline";
        s.FontSize = cls switch { "title" => 36, "subtitle" => 22, _ => 20 }; s.Bold = cls == "title";
        if (cls == "subtitle" || cls == "title" && s.Y > 4) s.Align = Align.Center;
    }
}
