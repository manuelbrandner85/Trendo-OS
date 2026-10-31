using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;
using System.Xml.Linq;

namespace Trendo.Core.Office;

/// <summary>OpenDocument-Text (.odt, LibreOffice) und reiner Text (.txt) lesen und schreiben – ohne Fremdbibliothek.</summary>
public static class OdtFormat
{
    static readonly XNamespace Office = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";
    static readonly XNamespace Text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
    static readonly XNamespace Style = "urn:oasis:names:tc:opendocument:xmlns:style:1.0";
    static readonly XNamespace Fo = "urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0";
    static readonly XNamespace TableNs = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";
    static readonly XNamespace Draw = "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0";
    static readonly XNamespace Svg = "urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0";
    static readonly XNamespace XLink = "http://www.w3.org/1999/xlink";

    static string StyleName(ParaStyle s) => s switch
    {
        ParaStyle.Title => "Title", ParaStyle.Subtitle => "Subtitle", ParaStyle.Heading1 => "Heading_20_1", ParaStyle.Heading2 => "Heading_20_2", ParaStyle.Heading3 => "Heading_20_3", ParaStyle.Quote => "Quotations", _ => "Standard",
    };

    static string Esc(string s) => SecurityElement.Escape(s) ?? "";

    // ---------- Schreiben ----------
    public static void Write(TextDoc doc, string path)
    {
        var auto = new StringBuilder(); var body = new StringBuilder(); var pics = new List<(string Name, byte[] Data, string Ext)>();
        var spanStyles = new Dictionary<(Fmt, double?), string>(); var paraStyles = new Dictionary<(ParaStyle, Align, double, double?), string>();
        int notes = 0, endnotes = 0, comments = 0, figures = 0; var page = doc.Page;
        string SpanStyle(Fmt f, double? size)
        {
            if (spanStyles.TryGetValue((f, size), out var n)) return n;
            n = "T" + (spanStyles.Count + 1); spanStyles[(f, size)] = n;
            auto.Append($"<style:style style:name=\"{n}\" style:family=\"text\"><style:text-properties");
            if (f.HasFlag(Fmt.Bold)) auto.Append(" fo:font-weight=\"bold\"");
            if (f.HasFlag(Fmt.Italic)) auto.Append(" fo:font-style=\"italic\"");
            if (f.HasFlag(Fmt.Underline)) auto.Append(" style:text-underline-style=\"solid\" style:text-underline-width=\"auto\" style:text-underline-color=\"font-color\"");
            if (f.HasFlag(Fmt.Strike)) auto.Append(" style:text-line-through-style=\"solid\"");
            if (f.HasFlag(Fmt.Super)) auto.Append(" style:text-position=\"super 58%\"");
            if (f.HasFlag(Fmt.Sub)) auto.Append(" style:text-position=\"sub 58%\"");
            if (size is { } s) auto.Append($" fo:font-size=\"{s.ToString(CultureInfo.InvariantCulture)}pt\"");
            auto.Append("/></style:style>");
            return n;
        }
        string ParaStyleFor(Para p)
        {
            if (p.Align == Align.Left && p.IndentCm == 0 && p.SpaceAfterPt is null) return StyleName(p.Style);
            var key = (p.Style, p.Align, p.IndentCm, p.SpaceAfterPt);
            if (paraStyles.TryGetValue(key, out var n)) return n;
            n = "P" + (paraStyles.Count + 1); paraStyles[key] = n;
            auto.Append($"<style:style style:name=\"{n}\" style:family=\"paragraph\" style:parent-style-name=\"{StyleName(p.Style)}\"><style:paragraph-properties");
            if (p.Align != Align.Left) auto.Append($" fo:text-align=\"{p.Align switch { Align.Center => "center", Align.Right => "end", _ => "justify" }}\"");
            if (p.IndentCm > 0) auto.Append($" fo:margin-left=\"{p.IndentCm.ToString("0.###", CultureInfo.InvariantCulture)}cm\"");
            if (p.SpaceAfterPt is { } sa) auto.Append($" fo:margin-bottom=\"{(sa * 2.54 / 72).ToString("0.###", CultureInfo.InvariantCulture)}cm\"");
            auto.Append("/></style:style>");
            return n;
        }
        string Inline(Para p)
        {
            var sb = new StringBuilder();
            var prevSpace = true; // ODF fasst Leerzeichen zusammen: führende und doppelte als <text:s/>
            // Abschnitte nach Formaten und Markierungen; gelöschter Text (Änderungen) wird in ODT als angenommen gespeichert
            var cuts = new SortedSet<int> { 0, p.Text.Length };
            foreach (var s in p.Spans) { cuts.Add(Math.Clamp(s.Start, 0, p.Text.Length)); cuts.Add(Math.Clamp(s.End, 0, p.Text.Length)); }
            foreach (var m in p.Marks) { cuts.Add(Math.Clamp(m.Start, 0, p.Text.Length)); cuts.Add(Math.Clamp(m.End, 0, p.Text.Length)); }
            var pts = cuts.ToList(); var openComments = new Dictionary<Mark, string>();
            void Points(int at)
            {
                foreach (var m in p.Marks.Where(m => m.Length == 0 && m.Start == at))
                    if (m.Kind is MarkKind.Footnote or MarkKind.Endnote)
                    {
                        var foot = m.Kind == MarkKind.Footnote; var no = foot ? ++notes : ++endnotes;
                        sb.Append($"<text:note text:id=\"{(foot ? "ftn" : "edn")}{no}\" text:note-class=\"{(foot ? "footnote" : "endnote")}\"><text:note-citation>{no}</text:note-citation><text:note-body><text:p text:style-name=\"{(foot ? "Footnote" : "Endnote")}\">{Esc(m.Value)}</text:p></text:note-body></text:note>");
                    }
                    else if (m.Kind == MarkKind.Comment) sb.Append($"<office:annotation><dc:creator></dc:creator><text:p>{Esc(m.Value)}</text:p></office:annotation>");
            }
            Points(0);
            for (var k = 0; k + 1 < pts.Count; k++)
            {
                int x = pts[k], y = pts[k + 1]; if (y <= x) continue;
                foreach (var m in p.Marks.Where(m => m.Kind == MarkKind.Comment && m.Length > 0 && m.Start == x)) { var nm = $"c{++comments}"; openComments[m] = nm; sb.Append($"<office:annotation office:name=\"{nm}\"><dc:creator></dc:creator><text:p>{Esc(m.Value)}</text:p></office:annotation>"); }
                if (p.Marks.Any(m => m.Kind == MarkKind.Delete && m.Start <= x && m.End >= y)) { Points(y); continue; }
                var link = p.Marks.FirstOrDefault(m => m.Kind == MarkKind.Link && m.Start <= x && m.End >= y)?.Value;
                var (f, size) = p.At(x); var t = p.Text[x..y];
                if (link is not null) sb.Append($"<text:a xlink:type=\"simple\" xlink:href=\"{Esc(link)}\">");
                var content = new StringBuilder();
                foreach (var ch in t)
                {
                    if (ch == ' ') { content.Append(prevSpace ? "<text:s/>" : " "); prevSpace = true; continue; }
                    prevSpace = ch is '\t' or '\n';
                    content.Append(ch switch { '\t' => "<text:tab/>", '\n' => "<text:line-break/>", _ => Esc(ch.ToString()) });
                }
                sb.Append(f == Fmt.None && size is null ? content.ToString() : $"<text:span text:style-name=\"{SpanStyle(f, size)}\">{content}</text:span>");
                if (link is not null) sb.Append("</text:a>");
                foreach (var m in p.Marks.Where(m => m.Kind == MarkKind.Comment && m.Length > 0 && m.End == y && openComments.ContainsKey(m))) sb.Append($"<office:annotation-end office:name=\"{openComments[m]}\"/>");
                Points(y);
            }
            return sb.ToString();
        }
        string ParaXml(Para p)
        {
            var name = ParaStyleFor(p);
            return p.Style is ParaStyle.Heading1 or ParaStyle.Heading2 or ParaStyle.Heading3
                ? $"<text:h text:style-name=\"{name}\" text:outline-level=\"{(int)p.Style - (int)ParaStyle.Heading1 + 1}\">{Inline(p)}</text:h>"
                : $"<text:p text:style-name=\"{name}\">{Inline(p)}</text:p>";
        }
        auto.Append("<style:style style:name=\"PB\" style:family=\"paragraph\" style:parent-style-name=\"Standard\"><style:paragraph-properties fo:break-before=\"page\"/></style:style>");
        auto.Append("<text:list-style style:name=\"LBullet\"><text:list-level-style-bullet text:level=\"1\" text:bullet-char=\"•\"><style:list-level-properties text:list-level-position-and-space-mode=\"label-alignment\"><style:list-level-label-alignment text:label-followed-by=\"listtab\" fo:text-indent=\"-0.635cm\" fo:margin-left=\"1.27cm\"/></style:list-level-properties></text:list-level-style-bullet></text:list-style>");
        auto.Append("<text:list-style style:name=\"LNumber\"><text:list-level-style-number text:level=\"1\" style:num-suffix=\".\" style:num-format=\"1\"><style:list-level-properties text:list-level-position-and-space-mode=\"label-alignment\"><style:list-level-label-alignment text:label-followed-by=\"listtab\" fo:text-indent=\"-0.635cm\" fo:margin-left=\"1.27cm\"/></style:list-level-properties></text:list-level-style-number></text:list-style>");
        var pageBreakNext = false; var tableNo = 0; var picNo = 0;
        for (var i = 0; i < doc.Blocks.Count; i++)
        {
            var b = doc.Blocks[i];
            if (b is PageBreak) { pageBreakNext = true; continue; }
            if (pageBreakNext) { body.Append("<text:p text:style-name=\"PB\"/>"); pageBreakNext = false; }
            switch (b)
            {
                case Para p when p.List != ListKind.None:
                    // aufeinanderfolgende Listenpunkte gleicher Art bilden eine Liste
                    body.Append($"<text:list text:style-name=\"{(p.List == ListKind.Bullet ? "LBullet" : "LNumber")}\">");
                    var kind = p.List;
                    for (; i < doc.Blocks.Count && doc.Blocks[i] is Para lp && lp.List == kind; i++) body.Append($"<text:list-item>{ParaXml(lp)}</text:list-item>");
                    i--;
                    body.Append("</text:list>");
                    break;
                case Para p: body.Append(ParaXml(p)); break;
                case TocBlock toc:
                    body.Append($"<text:table-of-content text:name=\"Inhaltsverzeichnis1\"><text:table-of-content-source text:outline-level=\"3\"><text:index-title-template>{Esc(toc.Title)}</text:index-title-template></text:table-of-content-source><text:index-body><text:index-title text:name=\"Inhaltsverzeichnis1_Head\"><text:p text:style-name=\"Contents_20_Heading\">{Esc(toc.Title)}</text:p></text:index-title>");
                    foreach (var e in TextTools.Toc(doc)) body.Append($"<text:p text:style-name=\"Contents_20_{e.Level}\">{Esc(e.Text)}</text:p>");
                    body.Append("</text:index-body></text:table-of-content>");
                    break;
                case Table t:
                    tableNo++;
                    body.Append($"<table:table table:name=\"Tabelle{tableNo}\"><table:table-column table:number-columns-repeated=\"{Math.Max(1, t.Cols)}\"/>");
                    foreach (var row in t.Rows)
                    {
                        body.Append("<table:table-row>");
                        for (var c = 0; c < t.Cols; c++)
                            body.Append($"<table:table-cell office:value-type=\"string\"><text:p>{Esc(c < row.Count ? row[c] : "")}</text:p></table:table-cell>");
                        body.Append("</table:table-row>");
                    }
                    body.Append("</table:table>");
                    break;
                case Picture pic:
                    picNo++;
                    var name = $"Pictures/bild{picNo}.{pic.Ext}"; pics.Add((name, pic.Data, pic.Ext));
                    body.Append($"<text:p><draw:frame draw:name=\"Bild{picNo}\" text:anchor-type=\"as-char\" svg:width=\"{pic.WidthCm.ToString("0.###", CultureInfo.InvariantCulture)}cm\" svg:height=\"{pic.HeightCm.ToString("0.###", CultureInfo.InvariantCulture)}cm\"><draw:image xlink:href=\"{name}\" xlink:type=\"simple\" xlink:show=\"embed\" xlink:actuate=\"onLoad\"/>{(pic.Alt.Length > 0 ? $"<svg:desc>{Esc(pic.Alt)}</svg:desc>" : "")}</draw:frame></text:p>");
                    if (pic.Caption.Trim().Length > 0) body.Append($"<text:p text:style-name=\"Caption\">Abbildung {++figures}: {Esc(pic.Caption.Trim())}</text:p>");
                    break;
            }
        }
        const string Ns = "xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" xmlns:fo=\"urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" xmlns:svg=\"urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:meta=\"urn:oasis:names:tc:opendocument:xmlns:meta:1.0\" office:version=\"1.3\"";
        var content = $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><office:document-content {Ns}><office:automatic-styles>{auto}</office:automatic-styles><office:body><office:text>{body}</office:text></office:body></office:document-content>";
        var styles = $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><office:document-styles {Ns}><office:styles>"
            + $"<style:default-style style:family=\"paragraph\"><style:paragraph-properties fo:margin-bottom=\"0.21cm\"/><style:text-properties style:font-name=\"{Esc(page.Font)}\" fo:font-family=\"{Esc(page.Font)}\" fo:font-size=\"11pt\" fo:language=\"de\" fo:country=\"DE\"{(page.Hyphenate ? " fo:hyphenate=\"true\"" : "")}/></style:default-style>"
            + "<style:style style:name=\"Standard\" style:family=\"paragraph\" style:class=\"text\"/>"
            + OdfStyle("Title", "Title", 26, true, null) + OdfStyle("Subtitle", "Subtitle", 16, false, "#595959")
            + OdfStyle("Heading_20_1", "Heading 1", 18, true, page.HeadingColor.ToLowerInvariant(), 1) + OdfStyle("Heading_20_2", "Heading 2", 15, true, page.HeadingColor.ToLowerInvariant(), 2) + OdfStyle("Heading_20_3", "Heading 3", 13, true, page.HeadingColor.ToLowerInvariant(), 3)
            + OdfStyle("Caption", "Caption", 9, false, "#595959") + OdfStyle("Footnote", "Footnote", 9, false, null) + OdfStyle("Endnote", "Endnote", 9, false, null)
            + OdfStyle("Contents_20_Heading", "Contents Heading", 16, true, page.HeadingColor.ToLowerInvariant()) + OdfStyle("Contents_20_1", "Contents 1", 11, false, null) + OdfStyle("Contents_20_2", "Contents 2", 11, false, null) + OdfStyle("Contents_20_3", "Contents 3", 11, false, null)
            + "<style:style style:name=\"Quotations\" style:display-name=\"Quotations\" style:family=\"paragraph\" style:parent-style-name=\"Standard\"><style:paragraph-properties fo:margin-left=\"1cm\" fo:margin-right=\"1cm\"/><style:text-properties fo:font-style=\"italic\" fo:color=\"#404040\"/></style:style>"
            + "</office:styles><office:automatic-styles>" + PageLayout(page) + "</office:automatic-styles>"
            + "<office:master-styles><style:master-page style:name=\"Standard\" style:page-layout-name=\"pm1\">" + HeaderFooter(page) + "</style:master-page></office:master-styles></office:document-styles>";
        var meta = $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><office:document-meta {Ns}><office:meta><dc:title>{Esc(doc.Title ?? "")}</dc:title></office:meta></office:document-meta>";
        var manifest = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\"?><manifest:manifest xmlns:manifest=\"urn:oasis:names:tc:opendocument:xmlns:manifest:1.0\" manifest:version=\"1.3\"><manifest:file-entry manifest:full-path=\"/\" manifest:media-type=\"application/vnd.oasis.opendocument.text\"/><manifest:file-entry manifest:full-path=\"content.xml\" manifest:media-type=\"text/xml\"/><manifest:file-entry manifest:full-path=\"styles.xml\" manifest:media-type=\"text/xml\"/><manifest:file-entry manifest:full-path=\"meta.xml\" manifest:media-type=\"text/xml\"/>");
        foreach (var (n, _, ext) in pics) manifest.Append($"<manifest:file-entry manifest:full-path=\"{n}\" manifest:media-type=\"image/{(ext == "jpg" ? "jpeg" : ext)}\"/>");
        manifest.Append("</manifest:manifest>");
        var tmp = path + ".tmp";
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using (var z = ZipFile.Open(tmp, ZipArchiveMode.Create))
        {
            var mt = z.CreateEntry("mimetype", CompressionLevel.NoCompression); // muss zuerst und unkomprimiert stehen
            using (var w = new StreamWriter(mt.Open(), new UTF8Encoding(false))) w.Write("application/vnd.oasis.opendocument.text");
            Add(z, "content.xml", content); Add(z, "styles.xml", styles); Add(z, "meta.xml", meta); Add(z, "META-INF/manifest.xml", manifest.ToString());
            foreach (var (n, data, _) in pics) { using var s = z.CreateEntry(n).Open(); s.Write(data); }
        }
        File.Move(tmp, path, true);
    }

    static string Cm(double v) => v.ToString("0.###", CultureInfo.InvariantCulture) + "cm";

    /// <summary>W3/W4: Seitenformat, Ränder, Spalten, Seitenrand-Linie.</summary>
    static string PageLayout(PageSetup page)
    {
        double w = 21, h = 29.7; if (page.Landscape) (w, h) = (h, w);
        var sb = new StringBuilder($"<style:page-layout style:name=\"pm1\"><style:page-layout-properties fo:page-width=\"{Cm(w)}\" fo:page-height=\"{Cm(h)}\" style:print-orientation=\"{(page.Landscape ? "landscape" : "portrait")}\" fo:margin-top=\"{Cm(page.Top)}\" fo:margin-bottom=\"{Cm(page.Bottom)}\" fo:margin-left=\"{Cm(page.Left)}\" fo:margin-right=\"{Cm(page.Right)}\"");
        if (page.PageBorder) sb.Append(" fo:border=\"0.06pt solid #808080\" fo:padding=\"0.3cm\"");
        sb.Append(">");
        if (page.Columns > 1) sb.Append($"<style:columns fo:column-count=\"{page.Columns}\" fo:column-gap=\"1.25cm\"/>");
        sb.Append("</style:page-layout-properties>");
        if (page.Header.Length > 0) sb.Append("<style:header-style><style:header-footer-properties fo:min-height=\"0cm\" fo:margin-bottom=\"0.5cm\"/></style:header-style>");
        if (page.Footer.Length > 0 || page.PageNumbers) sb.Append("<style:footer-style><style:header-footer-properties fo:min-height=\"0cm\" fo:margin-top=\"0.5cm\"/></style:footer-style>");
        return sb.Append("</style:page-layout>").ToString();
    }

    static string HeaderFooter(PageSetup page)
    {
        var sb = new StringBuilder();
        if (page.Header.Length > 0) sb.Append($"<style:header><text:p>{Esc(page.Header)}</text:p></style:header>");
        if (page.Footer.Length > 0 || page.PageNumbers)
        {
            sb.Append("<style:footer>");
            if (page.Footer.Length > 0) sb.Append($"<text:p>{Esc(page.Footer)}</text:p>");
            if (page.PageNumbers) sb.Append("<text:p>Seite <text:page-number text:select-page=\"current\">1</text:page-number> von <text:page-count>1</text:page-count></text:p>");
            sb.Append("</style:footer>");
        }
        return sb.ToString();
    }

    static string OdfStyle(string name, string display, int pt, bool bold, string? color, int outline = 0) =>
        $"<style:style style:name=\"{name}\" style:display-name=\"{display}\" style:family=\"paragraph\" style:parent-style-name=\"Standard\" style:next-style-name=\"Standard\" style:class=\"text\"{(outline > 0 ? $" style:default-outline-level=\"{outline}\"" : "")}>"
        + $"<style:paragraph-properties fo:margin-top=\"{(outline > 0 ? "0.42cm" : "0cm")}\" fo:margin-bottom=\"0.21cm\"/><style:text-properties fo:font-size=\"{pt}pt\"{(bold ? " fo:font-weight=\"bold\"" : "")}{(color is null ? "" : $" fo:color=\"{color}\"")}/></style:style>";

    static void Add(ZipArchive z, string n, string c) { using var w = new StreamWriter(z.CreateEntry(n).Open(), new UTF8Encoding(false)); w.Write(c); }

    // ---------- Lesen ----------
    public static TextDoc Read(string path)
    {
        using var z = OfficeZip.OpenRead(path);
        XDocument Load(string n) { var e = z.GetEntry(n); if (e is null) return new XDocument(); return e.LoadXml(LoadOptions.PreserveWhitespace); }
        var content = Load("content.xml"); var stylesDoc = Load("styles.xml");
        // Stilnamen → Absatzart/Ausrichtung/Zeichenformat (automatische und benannte Stile)
        var all = content.Descendants(Style + "style").Concat(stylesDoc.Descendants(Style + "style")).GroupBy(s => (string?)s.Attribute(Style + "name") ?? "").ToDictionary(g => g.Key, g => g.First());
        ParaStyle PStyle(string? name)
        {
            for (var guard = 0; name is not null && guard < 8; guard++)
            {
                var key = name.Replace("_20_", " ").ToLowerInvariant();
                if (key is "title" or "titel") return ParaStyle.Title;
                if (key is "subtitle" or "untertitel") return ParaStyle.Subtitle;
                if (key is "heading 1" or "überschrift 1") return ParaStyle.Heading1;
                if (key is "heading 2" or "überschrift 2") return ParaStyle.Heading2;
                if (key is "heading 3" or "überschrift 3") return ParaStyle.Heading3;
                if (key is "quotations" or "zitat" or "quote") return ParaStyle.Quote;
                name = all.TryGetValue(name, out var st) ? (string?)st.Attribute(Style + "parent-style-name") : null;
            }
            return ParaStyle.Text;
        }
        Align PAlign(string? name) => (name is not null && all.TryGetValue(name, out var st) ? (string?)st.Element(Style + "paragraph-properties")?.Attribute(Fo + "text-align") : null) switch
        { "center" => Align.Center, "end" or "right" => Align.Right, "justify" => Align.Justify, _ => Align.Left };
        string? PStyleName(string? name)
        {
            for (var guard = 0; name is not null && guard < 8; guard++)
            {
                var key = name.Replace("_20_", " ").ToLowerInvariant();
                if (key is "caption" or "illustration" or "figure" or "abbildung") return key;
                name = all.TryGetValue(name, out var st) ? (string?)st.Attribute(Style + "parent-style-name") : null;
            }
            return null;
        }
        double PIndent(string? name) => name is not null && all.TryGetValue(name, out var st) && (string?)st.Element(Style + "paragraph-properties")?.Attribute(Fo + "margin-left") is { } ml && ml.EndsWith("cm", StringComparison.Ordinal) && double.TryParse(ml[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v > 0.01 && !(st.Attribute(Style + "family")?.Value == "paragraph" && PStyle(name) == ParaStyle.Quote) ? Math.Round(v, 2) : 0;
        double? PSpace(string? name) => name is not null && all.TryGetValue(name, out var st) && (string?)st.Attribute(Style + "parent-style-name") is not null && (string?)st.Element(Style + "paragraph-properties")?.Attribute(Fo + "margin-bottom") is { } mb && mb.EndsWith("cm", StringComparison.Ordinal) && double.TryParse(mb[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && content.Descendants(Style + "style").Contains(st) ? Math.Round(v * 72 / 2.54) : null;
        bool BreakBefore(string? name) => name is not null && all.TryGetValue(name, out var st) && (string?)st.Element(Style + "paragraph-properties")?.Attribute(Fo + "break-before") == "page";
        (Fmt, double?) SFmt(string? name)
        {
            if (name is null || !all.TryGetValue(name, out var st) || st.Element(Style + "text-properties") is not { } tp) return (Fmt.None, null);
            var f = Fmt.None;
            if ((string?)tp.Attribute(Fo + "font-weight") == "bold") f |= Fmt.Bold;
            if ((string?)tp.Attribute(Fo + "font-style") == "italic") f |= Fmt.Italic;
            if ((string?)tp.Attribute(Style + "text-underline-style") is { } u && u != "none") f |= Fmt.Underline;
            if ((string?)tp.Attribute(Style + "text-line-through-style") is { } l && l != "none") f |= Fmt.Strike;
            var pos = (string?)tp.Attribute(Style + "text-position") ?? "";
            if (pos.StartsWith("super", StringComparison.Ordinal) || (pos.Length > 0 && char.IsDigit(pos[0]) && !pos.StartsWith('0'))) f |= Fmt.Super;
            if (pos.StartsWith("sub", StringComparison.Ordinal) || pos.StartsWith('-')) f |= Fmt.Sub;
            double? size = double.TryParse(((string?)tp.Attribute(Fo + "font-size"))?.Replace("pt", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var pt) ? pt : null;
            return (f, size);
        }
        var doc = new TextDoc();
        var text = content.Root?.Element(Office + "body")?.Element(Office + "text");
        if (text is null) return TextDoc.Empty();
        void ParaFrom(XElement el, ListKind list)
        {
            var styleName = (string?)el.Attribute(Text + "style-name");
            if (BreakBefore(styleName)) doc.Blocks.Add(new PageBreak());
            var style = el.Name == Text + "h" ? (int.TryParse((string?)el.Attribute(Text + "outline-level"), out var lvl) ? lvl switch { 1 => ParaStyle.Heading1, 2 => ParaStyle.Heading2, _ => ParaStyle.Heading3 } : ParaStyle.Heading1) : PStyle(styleName);
            if (el.Name == Text + "h" && PStyle(styleName) is var named && named != ParaStyle.Text) style = named;
            var p = new Para { Style = style, Align = PAlign(styleName), List = list, IndentCm = PIndent(styleName), SpaceAfterPt = PSpace(styleName) };
            var runs = new List<(string, Fmt, double?)>(); var marks = new List<Mark>(); var openAnn = new Dictionary<string, (int Start, string Text)>();
            int Len() => runs.Sum(r => r.Item1.Length);
            void Walk(XElement e, Fmt f, double? size)
            {
                foreach (var n in e.Nodes())
                    switch (n)
                    {
                        case XText t: runs.Add((System.Text.RegularExpressions.Regex.Replace(t.Value, "[ \t\r\n]+", " "), f, size)); break; // ODF: Leerraum im Text zählt als ein Leerzeichen
                        case XElement c when c.Name == Text + "span": { var (sf, ss) = SFmt((string?)c.Attribute(Text + "style-name")); Walk(c, f | sf, ss ?? size); break; }
                        case XElement c when c.Name == Text + "s": runs.Add((new string(' ', int.TryParse((string?)c.Attribute(Text + "c"), out var k) ? k : 1), f, size)); break;
                        case XElement c when c.Name == Text + "tab": runs.Add(("\t", f, size)); break;
                        case XElement c when c.Name == Text + "line-break": runs.Add(("\n", f, size)); break;
                        case XElement c when c.Name == Text + "a":
                        {
                            var start = Len(); Walk(c, f, size);
                            if ((string?)c.Attribute(XLink + "href") is { Length: > 0 } linkHref && Len() > start) marks.Add(new Mark(start, Len() - start, MarkKind.Link, linkHref));
                            break;
                        }
                        case XElement c when c.Name == Text + "note":
                            marks.Add(new Mark(Len(), 0, (string?)c.Attribute(Text + "note-class") == "endnote" ? MarkKind.Endnote : MarkKind.Footnote, string.Join("\n", c.Element(Text + "note-body")?.Elements().Select(e => e.Value) ?? []).Trim()));
                            break;
                        case XElement c when c.Name == Office + "annotation":
                        {
                            var body = string.Join("\n", c.Elements(Text + "p").Select(e => e.Value)).Trim();
                            if ((string?)c.Attribute(Office + "name") is { } an) openAnn[an] = (Len(), body); else marks.Add(new Mark(Len(), 0, MarkKind.Comment, body));
                            break;
                        }
                        case XElement c when c.Name == Office + "annotation-end":
                            if ((string?)c.Attribute(Office + "name") is { } en && openAnn.Remove(en, out var a)) marks.Add(new Mark(a.Start, Len() - a.Start, MarkKind.Comment, a.Text));
                            break;
                        case XElement c when c.Name == Draw + "frame" && c.Element(Draw + "image") is { } img:
                            p.SetRuns(runs); if (p.Text.Length > 0) doc.Blocks.Add(p); runs = []; p = new Para { Style = style, Align = p.Align };
                            var href = (string?)img.Attribute(XLink + "href");
                            if (href is not null && z.GetEntry(href) is { } entry)
                            {
                                using var ms = new MemoryStream(entry.ReadBytes());
                                static double Cm(string? v) => v is null ? 0 : v.EndsWith("cm", StringComparison.Ordinal) ? double.Parse(v[..^2], CultureInfo.InvariantCulture) : v.EndsWith("in", StringComparison.Ordinal) ? double.Parse(v[..^2], CultureInfo.InvariantCulture) * 2.54 : v.EndsWith("mm", StringComparison.Ordinal) ? double.Parse(v[..^2], CultureInfo.InvariantCulture) / 10 : 0;
                                doc.Blocks.Add(new Picture { Data = ms.ToArray(), Ext = Path.GetExtension(href).TrimStart('.').ToLowerInvariant().Replace("jpeg", "jpg"), WidthCm = Cm((string?)c.Attribute(Svg + "width")) is > 0 and var w ? w : 12, HeightCm = Cm((string?)c.Attribute(Svg + "height")) is > 0 and var h ? h : 8, Alt = c.Element(Svg + "desc")?.Value ?? c.Element(Svg + "title")?.Value ?? "" });
                            }
                            break;
                    }
            }
            // Bildbeschriftung („Abbildung 1: …“) direkt unter einem Bild gehört zum Bild
            if (PStyleName(styleName) is "caption" or "illustration" or "figure" && doc.Blocks.Count > 0 && doc.Blocks[^1] is Picture capPic)
            {
                var cap = el.Value; var colon = cap.IndexOf(':');
                capPic.Caption = colon >= 0 && cap.StartsWith("Abb", StringComparison.OrdinalIgnoreCase) ? cap[(colon + 1)..].Trim() : cap.Trim();
                return;
            }
            Walk(el, Fmt.None, null);
            p.SetRuns(runs); p.Marks = marks;
            // leerer Träger eines Seitenumbruchs oder eines Bildes ist kein eigener Absatz
            var carrier = p.Text.Length == 0 && (el.Descendants(Draw + "frame").Any() || (BreakBefore(styleName) && !el.Nodes().Any()));
            if (!carrier || p.Marks.Count > 0) doc.Blocks.Add(p);
        }
        void Block(XElement el, ListKind list)
        {
            if (el.Name == Text + "p" || el.Name == Text + "h") ParaFrom(el, list);
            else if (el.Name == Text + "list")
            {
                var ls = (string?)el.Attribute(Text + "style-name");
                var kind = ls is not null && content.Descendants(Text + "list-style").Concat(stylesDoc.Descendants(Text + "list-style")).FirstOrDefault(s => (string?)s.Attribute(Style + "name") == ls)?.Elements().FirstOrDefault()?.Name == Text + "list-level-style-number" ? ListKind.Number : ListKind.Bullet;
                foreach (var item in el.Elements(Text + "list-item")) foreach (var c in item.Elements()) Block(c, list == ListKind.None ? kind : list);
            }
            else if (el.Name == TableNs + "table")
            {
                var t = new Table();
                foreach (var row in el.Descendants(TableNs + "table-row"))
                {
                    var cells = new List<string>();
                    foreach (var cell in row.Elements(TableNs + "table-cell"))
                    {
                        var v = string.Join("\n", cell.Elements(Text + "p").Select(p => p.Value));
                        var rep = int.TryParse((string?)cell.Attribute(TableNs + "number-columns-repeated"), out var n) ? Math.Min(n, 64) : 1;
                        for (var k = 0; k < rep; k++) cells.Add(v);
                    }
                    t.Rows.Add(cells);
                }
                if (t.Rows.Count > 0) doc.Blocks.Add(t);
            }
            else if (el.Name == Text + "section") foreach (var c in el.Elements()) Block(c, list);
            else if (el.Name == Text + "table-of-content") doc.Blocks.Add(new TocBlock { Title = el.Descendants(Text + "index-title-template").FirstOrDefault()?.Value is { Length: > 0 } tt ? tt : "Inhalt" });
        }
        foreach (var el in text.Elements()) Block(el, ListKind.None);
        // Seite einrichten aus der Standard-Seitenvorlage
        var master = stylesDoc.Descendants(Style + "master-page").FirstOrDefault(m => (string?)m.Attribute(Style + "name") is "Standard" or "Default") ?? stylesDoc.Descendants(Style + "master-page").FirstOrDefault();
        var plName = (string?)master?.Attribute(Style + "page-layout-name");
        var pl = stylesDoc.Descendants(Style + "page-layout").FirstOrDefault(l => (string?)l.Attribute(Style + "name") == plName)?.Element(Style + "page-layout-properties");
        if (pl is not null)
        {
            static double C(string? v, double def) => v is null ? def : v.EndsWith("cm", StringComparison.Ordinal) && double.TryParse(v[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? Math.Round(d, 2) : v.EndsWith("mm", StringComparison.Ordinal) && double.TryParse(v[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var mm) ? Math.Round(mm / 10, 2) : v.EndsWith("in", StringComparison.Ordinal) && double.TryParse(v[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var inch) ? Math.Round(inch * 2.54, 2) : def;
            var pg = doc.Page;
            pg.Landscape = (string?)pl.Attribute(Style + "print-orientation") == "landscape" || C((string?)pl.Attribute(Fo + "page-width"), 21) > C((string?)pl.Attribute(Fo + "page-height"), 29.7);
            pg.Top = C((string?)pl.Attribute(Fo + "margin-top"), pg.Top); pg.Bottom = C((string?)pl.Attribute(Fo + "margin-bottom"), pg.Bottom);
            pg.Left = C((string?)pl.Attribute(Fo + "margin-left"), pg.Left); pg.Right = C((string?)pl.Attribute(Fo + "margin-right"), pg.Right);
            pg.Columns = int.TryParse((string?)pl.Element(Style + "columns")?.Attribute(Fo + "column-count"), out var cc) ? Math.Clamp(cc, 1, 3) : 1;
            pg.PageBorder = (string?)pl.Attribute(Fo + "border") is { } bd && bd != "none";
            if (master?.Element(Style + "header") is { } hd) pg.Header = string.Join(" ", hd.Elements(Text + "p").Select(e => e.Value).Where(s => s.Trim().Length > 0)).Trim();
            if (master?.Element(Style + "footer") is { } ft)
            {
                pg.PageNumbers = ft.Descendants(Text + "page-number").Any();
                pg.Footer = string.Join(" ", ft.Elements(Text + "p").Where(e => !e.Descendants(Text + "page-number").Any()).Select(e => e.Value).Where(s => s.Trim().Length > 0)).Trim();
            }
        }
        if (stylesDoc.Descendants(Style + "default-style").FirstOrDefault(d => (string?)d.Attribute(Style + "family") == "paragraph")?.Element(Style + "text-properties") is { } dtp)
        {
            if ((string?)dtp.Attribute(Fo + "hyphenate") == "true") doc.Page.Hyphenate = true;
            var font = (string?)dtp.Attribute(Style + "font-name") ?? (string?)dtp.Attribute(Fo + "font-family");
            if (PageSetup.Designs.FirstOrDefault(d => d.Font == font) is { Id: { } did }) doc.Page.Design = did;
        }
        doc.Title = Load("meta.xml").Descendants().FirstOrDefault(e => e.Name.LocalName == "title")?.Value is { Length: > 0 } title ? title : null;
        if (doc.Blocks.Count == 0) doc.Blocks.Add(new Para());
        return doc;
    }

    // ---------- Reiner Text ----------
    public static TextDoc ReadTxt(string path)
    {
        var doc = new TextDoc();
        foreach (var line in File.ReadAllText(path).Replace("\r\n", "\n").Split('\n'))
        {
            if (line.Contains('\f')) { doc.Blocks.Add(new PageBreak()); continue; }
            doc.Blocks.Add(new Para(line));
        }
        if (doc.Blocks.Count == 0) doc.Blocks.Add(new Para());
        return doc;
    }

    public static void WriteTxt(TextDoc doc, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, doc.PlainText().Replace("\n", Environment.NewLine), new UTF8Encoding(true));
    }
}

/// <summary>Öffnen und Speichern nach Dateiendung (.docx, .odt, .txt).</summary>
public static class TextFiles
{
    public static readonly string[] Extensions = ["docx", "odt", "txt"];

    public static TextDoc Open(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".docx" => DocxFormat.Read(path), ".odt" => OdtFormat.Read(path), ".txt" or ".md" => OdtFormat.ReadTxt(path),
        var e => throw new ArgumentException($"Das Format {e} kann Trendo Text (noch) nicht öffnen."),
    };

    public static void Save(TextDoc doc, string path)
    {
        switch (Path.GetExtension(path).ToLowerInvariant())
        {
            case ".docx": DocxFormat.Write(doc, path); break;
            case ".odt": OdtFormat.Write(doc, path); break;
            case ".txt": OdtFormat.WriteTxt(doc, path); break;
            case ".pdf": TextPdf.Export(doc, path); break;
            default: throw new ArgumentException("Bitte als .docx, .odt, .txt oder .pdf speichern.");
        }
    }
}
