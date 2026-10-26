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
        var spanStyles = new Dictionary<(Fmt, double?), string>(); var paraStyles = new Dictionary<(ParaStyle, Align), string>();
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
            if (p.Align == Align.Left) return StyleName(p.Style);
            if (paraStyles.TryGetValue((p.Style, p.Align), out var n)) return n;
            n = "P" + (paraStyles.Count + 1); paraStyles[(p.Style, p.Align)] = n;
            auto.Append($"<style:style style:name=\"{n}\" style:family=\"paragraph\" style:parent-style-name=\"{StyleName(p.Style)}\"><style:paragraph-properties fo:text-align=\"{p.Align switch { Align.Center => "center", Align.Right => "right", _ => "justify" }}\"/></style:style>");
            return n;
        }
        string Inline(Para p)
        {
            var sb = new StringBuilder();
            var prevSpace = true; // ODF fasst Leerzeichen zusammen: führende und doppelte als <text:s/>
            foreach (var (t, f, size) in p.Runs())
            {
                var content = new StringBuilder();
                foreach (var ch in t)
                {
                    if (ch == ' ') { content.Append(prevSpace ? "<text:s/>" : " "); prevSpace = true; continue; }
                    prevSpace = ch is '\t' or '\n';
                    content.Append(ch switch { '\t' => "<text:tab/>", '\n' => "<text:line-break/>", _ => Esc(ch.ToString()) });
                }
                sb.Append(f == Fmt.None && size is null ? content.ToString() : $"<text:span text:style-name=\"{SpanStyle(f, size)}\">{content}</text:span>");
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
                    body.Append($"<text:p><draw:frame draw:name=\"Bild{picNo}\" text:anchor-type=\"as-char\" svg:width=\"{pic.WidthCm.ToString("0.###", CultureInfo.InvariantCulture)}cm\" svg:height=\"{pic.HeightCm.ToString("0.###", CultureInfo.InvariantCulture)}cm\"><draw:image xlink:href=\"{name}\" xlink:type=\"simple\" xlink:show=\"embed\" xlink:actuate=\"onLoad\"/></draw:frame></text:p>");
                    break;
            }
        }
        const string Ns = "xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" xmlns:fo=\"urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" xmlns:svg=\"urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:meta=\"urn:oasis:names:tc:opendocument:xmlns:meta:1.0\" office:version=\"1.3\"";
        var content = $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><office:document-content {Ns}><office:automatic-styles>{auto}</office:automatic-styles><office:body><office:text>{body}</office:text></office:body></office:document-content>";
        var styles = $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><office:document-styles {Ns}><office:styles>"
            + "<style:default-style style:family=\"paragraph\"><style:paragraph-properties fo:margin-bottom=\"0.21cm\"/><style:text-properties style:font-name=\"Arial\" fo:font-family=\"Arial\" fo:font-size=\"11pt\" fo:language=\"de\" fo:country=\"DE\"/></style:default-style>"
            + "<style:style style:name=\"Standard\" style:family=\"paragraph\" style:class=\"text\"/>"
            + OdfStyle("Title", "Title", 26, true, null) + OdfStyle("Subtitle", "Subtitle", 16, false, "#595959")
            + OdfStyle("Heading_20_1", "Heading 1", 18, true, "#1f3864", 1) + OdfStyle("Heading_20_2", "Heading 2", 15, true, "#2f5496", 2) + OdfStyle("Heading_20_3", "Heading 3", 13, true, "#2f5496", 3)
            + "<style:style style:name=\"Quotations\" style:display-name=\"Quotations\" style:family=\"paragraph\" style:parent-style-name=\"Standard\"><style:paragraph-properties fo:margin-left=\"1cm\" fo:margin-right=\"1cm\"/><style:text-properties fo:font-style=\"italic\" fo:color=\"#404040\"/></style:style>"
            + "</office:styles><office:automatic-styles><style:page-layout style:name=\"pm1\"><style:page-layout-properties fo:page-width=\"21cm\" fo:page-height=\"29.7cm\" fo:margin-top=\"2.5cm\" fo:margin-bottom=\"2cm\" fo:margin-left=\"2.5cm\" fo:margin-right=\"2.5cm\"/></style:page-layout></office:automatic-styles>"
            + "<office:master-styles><style:master-page style:name=\"Standard\" style:page-layout-name=\"pm1\"/></office:master-styles></office:document-styles>";
        var meta = $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><office:document-meta {Ns}><office:meta><meta:generator>Trendo Text</meta:generator><dc:title>{Esc(doc.Title ?? "")}</dc:title></office:meta></office:document-meta>";
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

    static string OdfStyle(string name, string display, int pt, bool bold, string? color, int outline = 0) =>
        $"<style:style style:name=\"{name}\" style:display-name=\"{display}\" style:family=\"paragraph\" style:parent-style-name=\"Standard\" style:next-style-name=\"Standard\" style:class=\"text\"{(outline > 0 ? $" style:default-outline-level=\"{outline}\"" : "")}>"
        + $"<style:paragraph-properties fo:margin-top=\"{(outline > 0 ? "0.42cm" : "0cm")}\" fo:margin-bottom=\"0.21cm\"/><style:text-properties fo:font-size=\"{pt}pt\"{(bold ? " fo:font-weight=\"bold\"" : "")}{(color is null ? "" : $" fo:color=\"{color}\"")}/></style:style>";

    static void Add(ZipArchive z, string n, string c) { using var w = new StreamWriter(z.CreateEntry(n).Open(), new UTF8Encoding(false)); w.Write(c); }

    // ---------- Lesen ----------
    public static TextDoc Read(string path)
    {
        using var z = ZipFile.OpenRead(path);
        XDocument Load(string n) { var e = z.GetEntry(n); if (e is null) return new XDocument(); using var s = e.Open(); return XDocument.Load(s, LoadOptions.PreserveWhitespace); }
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
            var p = new Para { Style = style, Align = PAlign(styleName), List = list };
            var runs = new List<(string, Fmt, double?)>();
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
                        case XElement c when c.Name == Text + "a": Walk(c, f, size); break;
                        case XElement c when c.Name == Draw + "frame" && c.Element(Draw + "image") is { } img:
                            p.SetRuns(runs); if (p.Text.Length > 0) doc.Blocks.Add(p); runs = []; p = new Para { Style = style, Align = p.Align };
                            var href = (string?)img.Attribute(XLink + "href");
                            if (href is not null && z.GetEntry(href) is { } entry)
                            {
                                using var ms = new MemoryStream(); using (var s = entry.Open()) s.CopyTo(ms);
                                static double Cm(string? v) => v is null ? 0 : v.EndsWith("cm", StringComparison.Ordinal) ? double.Parse(v[..^2], CultureInfo.InvariantCulture) : v.EndsWith("in", StringComparison.Ordinal) ? double.Parse(v[..^2], CultureInfo.InvariantCulture) * 2.54 : v.EndsWith("mm", StringComparison.Ordinal) ? double.Parse(v[..^2], CultureInfo.InvariantCulture) / 10 : 0;
                                doc.Blocks.Add(new Picture { Data = ms.ToArray(), Ext = Path.GetExtension(href).TrimStart('.').ToLowerInvariant().Replace("jpeg", "jpg"), WidthCm = Cm((string?)c.Attribute(Svg + "width")) is > 0 and var w ? w : 12, HeightCm = Cm((string?)c.Attribute(Svg + "height")) is > 0 and var h ? h : 8 });
                            }
                            break;
                    }
            }
            Walk(el, Fmt.None, null);
            p.SetRuns(runs);
            // leerer Träger eines Seitenumbruchs oder eines Bildes ist kein eigener Absatz
            var carrier = p.Text.Length == 0 && (el.Descendants(Draw + "frame").Any() || (BreakBefore(styleName) && !el.Nodes().Any()));
            if (!carrier) doc.Blocks.Add(p);
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
        }
        foreach (var el in text.Elements()) Block(el, ListKind.None);
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
