using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;
using System.Xml.Linq;

namespace Trendo.Core.Office;

/// <summary>Word-Dateien (.docx, Office Open XML) lesen und schreiben – ohne Fremdbibliothek. Absatzarten werden als echte Formatvorlagen
/// (Title, Subtitle, Heading1–3, Quote) gespeichert, damit Word und LibreOffice sie als Überschriften erkennen.
/// T-0230: dazu Seite einrichten (Ränder, Hoch/Quer, Spalten, Seitenrand-Linie), Kopf-/Fußzeile mit Seitenzahl, Wasserzeichen, Design,
/// Links, Kommentare, Fuß- und Endnoten, nachverfolgte Änderungen (w:ins/w:del), Inhaltsverzeichnis, Bildbeschriftung und Alternativtext.
/// Kein Autorname in der Datei (Kommentare und Änderungen ohne Verfasser).</summary>
public static class DocxFormat
{
    static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";
    static readonly XNamespace Pic = "http://schemas.openxmlformats.org/drawingml/2006/picture";
    static readonly XNamespace WP = "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing";
    static readonly XNamespace Vml = "urn:schemas-microsoft-com:vml";
    static readonly XNamespace Pr = "http://schemas.openxmlformats.org/package/2006/relationships";
    const long EmuPerCm = 360000;
    const double TwipsPerCm = 566.93;
    const string RelT = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/";
    const string Ns = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\" xmlns:wp=\"http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing\" xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" xmlns:pic=\"http://schemas.openxmlformats.org/drawingml/2006/picture\" xmlns:v=\"urn:schemas-microsoft-com:vml\" xmlns:o=\"urn:schemas-microsoft-com:office:office\"";
    const string Xml = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>";
    const string Stamp = "2026-01-01T00:00:00Z";

    static string StyleId(ParaStyle s) => s switch
    {
        ParaStyle.Title => "Title", ParaStyle.Subtitle => "Subtitle", ParaStyle.Heading1 => "Heading1", ParaStyle.Heading2 => "Heading2", ParaStyle.Heading3 => "Heading3", ParaStyle.Quote => "Quote", _ => "Normal",
    };

    static ParaStyle FromStyleId(string? id) => (id ?? "").ToLowerInvariant().Replace("(user)", "").Replace("user", "").Replace(" ", "") switch
    {
        "title" or "titel" => ParaStyle.Title, "subtitle" or "untertitel" => ParaStyle.Subtitle,
        "heading1" or "berschrift1" or "überschrift1" => ParaStyle.Heading1, "heading2" or "berschrift2" or "überschrift2" => ParaStyle.Heading2,
        "heading3" or "berschrift3" or "überschrift3" => ParaStyle.Heading3, "quote" or "quotations" or "zitat" or "blockzitat" or "intensequote" => ParaStyle.Quote, _ => ParaStyle.Text,
    };

    // ======================= Schreiben =======================
    sealed class Ctx
    {
        public readonly StringBuilder Rels = new();
        public readonly List<(string Name, byte[] Data)> Media = [];
        public readonly List<(int Id, string Text)> Comments = [], Footnotes = [], Endnotes = [];
        public int Pic, Link, Rev = 1, Figure;
    }

    public static void Write(TextDoc doc, string path)
    {
        var ctx = new Ctx(); var body = new StringBuilder(); var breakBefore = false;
        var page = doc.Page;
        for (var bi = 0; bi < doc.Blocks.Count; bi++)
        {
            var b = doc.Blocks[bi];
            if (b is PageBreak) { if (breakBefore) body.Append("<w:p><w:r><w:br w:type=\"page\"/></w:r></w:p>"); breakBefore = true; continue; }
            // Seitenumbruch wie in Word: als Eigenschaft des nächsten Absatzes (vor Tabellen als eigener Umbruch)
            if (breakBefore && b is Table or TocBlock) { body.Append("<w:p><w:r><w:br w:type=\"page\"/></w:r></w:p>"); breakBefore = false; }
            var pb = breakBefore ? "<w:pageBreakBefore/>" : ""; breakBefore = false;
            switch (b)
            {
                case Para p: body.Append(ParaXml(p, pb, ctx)); break;
                case TocBlock toc: body.Append(TocXml(doc, toc)); break;
                case Table t:
                    body.Append("<w:tbl><w:tblPr><w:tblStyle w:val=\"TableGrid\"/><w:tblW w:w=\"5000\" w:type=\"pct\"/><w:tblBorders>");
                    foreach (var side in new[] { "top", "left", "bottom", "right", "insideH", "insideV" }) body.Append($"<w:{side} w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"808080\"/>");
                    body.Append("</w:tblBorders></w:tblPr><w:tblGrid>");
                    for (var c = 0; c < t.Cols; c++) body.Append($"<w:gridCol w:w=\"{9000 / Math.Max(1, t.Cols)}\"/>");
                    body.Append("</w:tblGrid>");
                    foreach (var row in t.Rows)
                    {
                        body.Append("<w:tr>");
                        for (var c = 0; c < t.Cols; c++) body.Append($"<w:tc><w:tcPr><w:tcW w:w=\"{9000 / Math.Max(1, t.Cols)}\" w:type=\"dxa\"/></w:tcPr><w:p><w:r><w:t xml:space=\"preserve\">{Esc(c < row.Count ? row[c] : "")}</w:t></w:r></w:p></w:tc>");
                        body.Append("</w:tr>");
                    }
                    body.Append("</w:tbl>");
                    break;
                case Picture pic:
                    var id = ++ctx.Pic;
                    var name = $"bild{id}.{pic.Ext}"; ctx.Media.Add((name, pic.Data));
                    ctx.Rels.Append($"<Relationship Id=\"rIdImg{id}\" Type=\"{RelT}image\" Target=\"media/{name}\"/>");
                    long cx = (long)(pic.WidthCm * EmuPerCm), cy = (long)(pic.HeightCm * EmuPerCm);
                    body.Append($"<w:p>{(pb.Length > 0 ? $"<w:pPr>{pb}</w:pPr>" : "")}<w:r><w:drawing><wp:inline><wp:extent cx=\"{cx}\" cy=\"{cy}\"/><wp:docPr id=\"{id}\" name=\"Bild {id}\"{(pic.Alt.Length > 0 ? $" descr=\"{Esc(pic.Alt)}\"" : "")}/>"
                        + "<a:graphic><a:graphicData uri=\"http://schemas.openxmlformats.org/drawingml/2006/picture\"><pic:pic>"
                        + $"<pic:nvPicPr><pic:cNvPr id=\"{id}\" name=\"{name}\"{(pic.Alt.Length > 0 ? $" descr=\"{Esc(pic.Alt)}\"" : "")}/><pic:cNvPicPr/></pic:nvPicPr>"
                        + $"<pic:blipFill><a:blip r:embed=\"rIdImg{id}\"/><a:stretch><a:fillRect/></a:stretch></pic:blipFill>"
                        + $"<pic:spPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"{cx}\" cy=\"{cy}\"/></a:xfrm><a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom></pic:spPr>"
                        + "</pic:pic></a:graphicData></a:graphic></wp:inline></w:drawing></w:r></w:p>");
                    if (pic.Caption.Trim().Length > 0)
                    {
                        var n = ++ctx.Figure;
                        body.Append($"<w:p><w:pPr><w:pStyle w:val=\"Caption\"/></w:pPr><w:r><w:t xml:space=\"preserve\">Abbildung </w:t></w:r><w:fldSimple w:instr=\" SEQ Abbildung \\* ARABIC \"><w:r><w:t>{n}</w:t></w:r></w:fldSimple><w:r><w:t xml:space=\"preserve\">: {Esc(pic.Caption.Trim())}</w:t></w:r></w:p>");
                    }
                    break;
            }
        }
        if (breakBefore) body.Append("<w:p><w:r><w:br w:type=\"page\"/></w:r></w:p>");

        var hasHeader = page.Header.Length > 0 || page.Watermark.Length > 0; var hasFooter = page.Footer.Length > 0 || page.PageNumbers;
        double w = 21, h = 29.7; if (page.Landscape) (w, h) = (h, w);
        var sect = new StringBuilder("<w:sectPr>");
        if (hasHeader) sect.Append("<w:headerReference w:type=\"default\" r:id=\"rIdHdr\"/>");
        if (hasFooter) sect.Append("<w:footerReference w:type=\"default\" r:id=\"rIdFtr\"/>");
        sect.Append($"<w:pgSz w:w=\"{Tw(w)}\" w:h=\"{Tw(h)}\"{(page.Landscape ? " w:orient=\"landscape\"" : "")}/>");
        sect.Append($"<w:pgMar w:top=\"{Tw(page.Top)}\" w:right=\"{Tw(page.Right)}\" w:bottom=\"{Tw(page.Bottom)}\" w:left=\"{Tw(page.Left)}\" w:header=\"708\" w:footer=\"708\" w:gutter=\"0\"/>");
        if (page.PageBorder) sect.Append("<w:pgBorders w:offsetFrom=\"page\">" + string.Concat(new[] { "top", "left", "bottom", "right" }.Select(s => $"<w:{s} w:val=\"single\" w:sz=\"8\" w:space=\"24\" w:color=\"808080\"/>")) + "</w:pgBorders>");
        if (page.Columns > 1) sect.Append($"<w:cols w:num=\"{page.Columns}\" w:space=\"708\"/>");
        sect.Append("</w:sectPr>");
        var docXml = $"{Xml}<w:document {Ns}><w:body>{body}{sect}</w:body></w:document>";

        var exts = ctx.Media.Select(m => Path.GetExtension(m.Name).TrimStart('.')).Distinct().Select(e => $"<Default Extension=\"{e}\" ContentType=\"image/{(e == "jpg" ? "jpeg" : e)}\"/>");
        const string Wml = "application/vnd.openxmlformats-officedocument.wordprocessingml.";
        var ov = new StringBuilder($"<Override PartName=\"/word/document.xml\" ContentType=\"{Wml}document.main+xml\"/><Override PartName=\"/word/styles.xml\" ContentType=\"{Wml}styles+xml\"/><Override PartName=\"/word/numbering.xml\" ContentType=\"{Wml}numbering+xml\"/><Override PartName=\"/word/settings.xml\" ContentType=\"{Wml}settings+xml\"/><Override PartName=\"/docProps/core.xml\" ContentType=\"application/vnd.openxmlformats-package.core-properties+xml\"/>");
        var rels = new StringBuilder($"<Relationship Id=\"rIdStyles\" Type=\"{RelT}styles\" Target=\"styles.xml\"/><Relationship Id=\"rIdNum\" Type=\"{RelT}numbering\" Target=\"numbering.xml\"/><Relationship Id=\"rIdSet\" Type=\"{RelT}settings\" Target=\"settings.xml\"/>").Append(ctx.Rels);
        var parts = new List<(string Name, string Content)>();
        if (hasHeader) { parts.Add(("word/header1.xml", HeaderXml(page))); ov.Append($"<Override PartName=\"/word/header1.xml\" ContentType=\"{Wml}header+xml\"/>"); rels.Append($"<Relationship Id=\"rIdHdr\" Type=\"{RelT}header\" Target=\"header1.xml\"/>"); }
        if (hasFooter) { parts.Add(("word/footer1.xml", FooterXml(page))); ov.Append($"<Override PartName=\"/word/footer1.xml\" ContentType=\"{Wml}footer+xml\"/>"); rels.Append($"<Relationship Id=\"rIdFtr\" Type=\"{RelT}footer\" Target=\"footer1.xml\"/>"); }
        if (ctx.Comments.Count > 0)
        {
            parts.Add(("word/comments.xml", $"{Xml}<w:comments {Ns}>" + string.Concat(ctx.Comments.Select(c => $"<w:comment w:id=\"{c.Id}\" w:author=\"\" w:initials=\"\"><w:p><w:pPr><w:pStyle w:val=\"CommentText\"/></w:pPr><w:r><w:t xml:space=\"preserve\">{Esc(c.Text)}</w:t></w:r></w:p></w:comment>")) + "</w:comments>"));
            ov.Append($"<Override PartName=\"/word/comments.xml\" ContentType=\"{Wml}comments+xml\"/>"); rels.Append($"<Relationship Id=\"rIdCom\" Type=\"{RelT}comments\" Target=\"comments.xml\"/>");
        }
        string Notes(string tag, string style, List<(int Id, string Text)> list) =>
            $"{Xml}<w:{tag}s {Ns}><w:{tag} w:type=\"separator\" w:id=\"-1\"><w:p><w:r><w:separator/></w:r></w:p></w:{tag}><w:{tag} w:type=\"continuationSeparator\" w:id=\"0\"><w:p><w:r><w:continuationSeparator/></w:r></w:p></w:{tag}>"
            + string.Concat(list.Select(n => $"<w:{tag} w:id=\"{n.Id}\"><w:p><w:pPr><w:pStyle w:val=\"{style}Text\"/></w:pPr><w:r><w:rPr><w:rStyle w:val=\"{style}Reference\"/></w:rPr><w:{tag}Ref/></w:r><w:r><w:t xml:space=\"preserve\"> {Esc(n.Text)}</w:t></w:r></w:p></w:{tag}>")) + $"</w:{tag}s>";
        if (ctx.Footnotes.Count > 0) { parts.Add(("word/footnotes.xml", Notes("footnote", "Footnote", ctx.Footnotes))); ov.Append($"<Override PartName=\"/word/footnotes.xml\" ContentType=\"{Wml}footnotes+xml\"/>"); rels.Append($"<Relationship Id=\"rIdFn\" Type=\"{RelT}footnotes\" Target=\"footnotes.xml\"/>"); }
        if (ctx.Endnotes.Count > 0) { parts.Add(("word/endnotes.xml", Notes("endnote", "Endnote", ctx.Endnotes))); ov.Append($"<Override PartName=\"/word/endnotes.xml\" ContentType=\"{Wml}endnotes+xml\"/>"); rels.Append($"<Relationship Id=\"rIdEn\" Type=\"{RelT}endnotes\" Target=\"endnotes.xml\"/>"); }
        var settings = new StringBuilder($"{Xml}<w:settings {Ns}>");
        if (page.Hyphenate) settings.Append("<w:autoHyphenation/>");
        if (doc.Blocks.OfType<TocBlock>().Any()) settings.Append("<w:updateFields w:val=\"true\"/>");
        if (ctx.Footnotes.Count > 0) settings.Append("<w:footnotePr><w:footnote w:id=\"-1\"/><w:footnote w:id=\"0\"/></w:footnotePr>");
        if (ctx.Endnotes.Count > 0) settings.Append("<w:endnotePr><w:endnote w:id=\"-1\"/><w:endnote w:id=\"0\"/></w:endnotePr>");
        settings.Append("<w:defaultTabStop w:val=\"708\"/><w:compat><w:compatSetting w:name=\"compatibilityMode\" w:uri=\"http://schemas.microsoft.com/office/word\" w:val=\"15\"/></w:compat></w:settings>");

        var tmp = path + ".tmp";
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using (var z = ZipFile.Open(tmp, ZipArchiveMode.Create))
        {
            Add(z, "[Content_Types].xml", $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/>{string.Concat(exts)}{ov}</Types>");
            Add(z, "_rels/.rels", $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"{Pr}\"><Relationship Id=\"rId1\" Type=\"{RelT}officeDocument\" Target=\"word/document.xml\"/><Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties\" Target=\"docProps/core.xml\"/></Relationships>");
            Add(z, "word/_rels/document.xml.rels", $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"{Pr}\">{rels}</Relationships>");
            Add(z, "word/document.xml", docXml);
            Add(z, "word/styles.xml", Styles(page));
            Add(z, "word/numbering.xml", Numbering);
            Add(z, "word/settings.xml", settings.ToString());
            foreach (var (n, c) in parts) Add(z, n, c);
            Add(z, "docProps/core.xml", $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><cp:coreProperties xmlns:cp=\"http://schemas.openxmlformats.org/package/2006/metadata/core-properties\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\"><dc:title>{Esc(doc.Title ?? "")}</dc:title><dc:creator></dc:creator></cp:coreProperties>");
            foreach (var (n, data) in ctx.Media) { using var s = z.CreateEntry("word/media/" + n).Open(); s.Write(data); }
        }
        File.Move(tmp, path, true);
    }

    static int Tw(double cm) => (int)Math.Round(cm * TwipsPerCm);

    static string HeaderXml(PageSetup page)
    {
        var sb = new StringBuilder($"{Xml}<w:hdr {Ns}><w:p><w:pPr><w:pStyle w:val=\"Header\"/><w:jc w:val=\"right\"/></w:pPr>");
        if (page.Watermark.Length > 0)
            sb.Append($"<w:r><w:pict><v:shapetype id=\"_x0000_t136\" coordsize=\"21600,21600\" o:spt=\"136\" adj=\"10800\" path=\"m@7,l@8,m@5,21600l@6,21600e\"><v:path textpathok=\"t\" o:connecttype=\"custom\"/><v:textpath on=\"t\" fitshape=\"t\"/></v:shapetype>"
                + $"<v:shape id=\"Wasserzeichen\" o:spid=\"_x0000_s2049\" type=\"#_x0000_t136\" style=\"position:absolute;margin-left:0;margin-top:0;width:440pt;height:110pt;rotation:315;z-index:-251657216;mso-position-horizontal:center;mso-position-horizontal-relative:margin;mso-position-vertical:center;mso-position-vertical-relative:margin\" o:allowincell=\"f\" fillcolor=\"silver\" stroked=\"f\"><v:fill opacity=\".5\"/><v:textpath style=\"font-family:&quot;Arial&quot;;font-size:1pt\" string=\"{Esc(page.Watermark)}\"/></v:shape></w:pict></w:r>");
        if (page.Header.Length > 0) sb.Append($"<w:r><w:t xml:space=\"preserve\">{Esc(page.Header)}</w:t></w:r>");
        return sb.Append("</w:p></w:hdr>").ToString();
    }

    static string FooterXml(PageSetup page)
    {
        var sb = new StringBuilder($"{Xml}<w:ftr {Ns}>");
        if (page.Footer.Length > 0) sb.Append($"<w:p><w:pPr><w:pStyle w:val=\"Footer\"/></w:pPr><w:r><w:t xml:space=\"preserve\">{Esc(page.Footer)}</w:t></w:r></w:p>");
        if (page.PageNumbers) sb.Append("<w:p><w:pPr><w:pStyle w:val=\"Footer\"/><w:jc w:val=\"center\"/></w:pPr><w:r><w:t xml:space=\"preserve\">Seite </w:t></w:r><w:fldSimple w:instr=\" PAGE \"><w:r><w:t>1</w:t></w:r></w:fldSimple><w:r><w:t xml:space=\"preserve\"> von </w:t></w:r><w:fldSimple w:instr=\" NUMPAGES \"><w:r><w:t>1</w:t></w:r></w:fldSimple></w:p>");
        return sb.Append("</w:ftr>").ToString();
    }

    /// <summary>W5: Inhaltsverzeichnis als echtes Word-Feld (TOC), die Einträge stehen schon drin; Word aktualisiert beim Öffnen.</summary>
    static string TocXml(TextDoc doc, TocBlock toc)
    {
        var entries = TextTools.Toc(doc);
        var sb = new StringBuilder($"<w:p><w:pPr><w:pStyle w:val=\"TOCHeading\"/></w:pPr><w:r><w:t xml:space=\"preserve\">{Esc(toc.Title)}</w:t></w:r></w:p>");
        sb.Append("<w:p><w:pPr><w:pStyle w:val=\"TOC1\"/></w:pPr><w:r><w:fldChar w:fldCharType=\"begin\" w:dirty=\"true\"/></w:r><w:r><w:instrText xml:space=\"preserve\"> TOC \\o \"1-3\" \\h \\z \\u </w:instrText></w:r><w:r><w:fldChar w:fldCharType=\"separate\"/></w:r>");
        if (entries.Count == 0) sb.Append("<w:r><w:t>Keine Überschriften gefunden.</w:t></w:r></w:p>");
        for (var i = 0; i < entries.Count; i++)
        {
            if (i > 0) sb.Append($"<w:p><w:pPr><w:pStyle w:val=\"TOC{entries[i].Level}\"/></w:pPr>");
            sb.Append($"<w:r><w:t xml:space=\"preserve\">{Esc(entries[i].Text)}</w:t></w:r></w:p>");
        }
        sb.Append("<w:p><w:r><w:fldChar w:fldCharType=\"end\"/></w:r></w:p>");
        return sb.ToString();
    }

    static string ParaXml(Para p, string extra, Ctx ctx)
    {
        var sb = new StringBuilder("<w:p><w:pPr>");
        if (p.Style != ParaStyle.Text) sb.Append($"<w:pStyle w:val=\"{StyleId(p.Style)}\"/>");
        if (p.List != ListKind.None) sb.Append($"<w:numPr><w:ilvl w:val=\"0\"/><w:numId w:val=\"{(p.List == ListKind.Bullet ? 1 : 2)}\"/></w:numPr>");
        sb.Append(extra);
        if (p.SpaceAfterPt is { } sa) sb.Append($"<w:spacing w:after=\"{(int)Math.Round(sa * 20)}\"/>");
        if (p.IndentCm > 0) sb.Append($"<w:ind w:left=\"{Tw(p.IndentCm)}\"/>");
        if (p.Align != Align.Left) sb.Append($"<w:jc w:val=\"{p.Align switch { Align.Center => "center", Align.Right => "right", _ => "both" }}\"/>");
        sb.Append("</w:pPr>");
        // Abschnitte gleicher Formate und Markierungen
        var cuts = new SortedSet<int> { 0, p.Text.Length };
        foreach (var s in p.Spans) { cuts.Add(Math.Clamp(s.Start, 0, p.Text.Length)); cuts.Add(Math.Clamp(s.End, 0, p.Text.Length)); }
        foreach (var m in p.Marks) { cuts.Add(Math.Clamp(m.Start, 0, p.Text.Length)); cuts.Add(Math.Clamp(m.End, 0, p.Text.Length)); }
        var points = cuts.ToList(); string? openLink = null;
        var commentIds = new Dictionary<Mark, int>();
        void Notes(int at)
        {
            foreach (var m in p.Marks.Where(m => m.Length == 0 && m.Start == at))
            {
                if (m.Kind == MarkKind.Footnote) { var id = ctx.Footnotes.Count + 1; ctx.Footnotes.Add((id, m.Value)); sb.Append($"<w:r><w:rPr><w:rStyle w:val=\"FootnoteReference\"/></w:rPr><w:footnoteReference w:id=\"{id}\"/></w:r>"); }
                else if (m.Kind == MarkKind.Endnote) { var id = ctx.Endnotes.Count + 1; ctx.Endnotes.Add((id, m.Value)); sb.Append($"<w:r><w:rPr><w:rStyle w:val=\"EndnoteReference\"/></w:rPr><w:endnoteReference w:id=\"{id}\"/></w:r>"); }
                else if (m.Kind == MarkKind.Comment) { var id = ctx.Comments.Count; ctx.Comments.Add((id, m.Value)); sb.Append($"<w:commentRangeStart w:id=\"{id}\"/><w:commentRangeEnd w:id=\"{id}\"/><w:r><w:rPr><w:rStyle w:val=\"CommentReference\"/></w:rPr><w:commentReference w:id=\"{id}\"/></w:r>"); }
            }
        }
        Notes(0);
        for (var k = 0; k + 1 < points.Count; k++)
        {
            int x = points[k], y = points[k + 1]; if (y <= x) continue;
            foreach (var m in p.Marks.Where(m => m.Kind == MarkKind.Comment && m.Length > 0 && m.Start == x)) { var id = ctx.Comments.Count; ctx.Comments.Add((id, m.Value)); commentIds[m] = id; sb.Append($"<w:commentRangeStart w:id=\"{id}\"/>"); }
            var link = p.Marks.FirstOrDefault(m => m.Kind == MarkKind.Link && m.Start <= x && m.End >= y)?.Value;
            if (link != openLink)
            {
                if (openLink is not null) sb.Append("</w:hyperlink>");
                if (link is not null) { var rid = $"rIdLink{++ctx.Link}"; ctx.Rels.Append($"<Relationship Id=\"{rid}\" Type=\"{RelT}hyperlink\" Target=\"{Esc(link)}\" TargetMode=\"External\"/>"); sb.Append($"<w:hyperlink r:id=\"{rid}\" w:history=\"1\">"); }
                openLink = link;
            }
            var (f, size) = p.At(x);
            var ins = p.Marks.Any(m => m.Kind == MarkKind.Insert && m.Start <= x && m.End >= y);
            var del = p.Marks.Any(m => m.Kind == MarkKind.Delete && m.Start <= x && m.End >= y);
            if (ins) sb.Append($"<w:ins w:id=\"{ctx.Rev++}\" w:author=\"\" w:date=\"{Stamp}\">");
            if (del) sb.Append($"<w:del w:id=\"{ctx.Rev++}\" w:author=\"\" w:date=\"{Stamp}\">");
            sb.Append(RunXml(p.Text[x..y], f, size, link is not null, del));
            if (del) sb.Append("</w:del>");
            if (ins) sb.Append("</w:ins>");
            foreach (var m in p.Marks.Where(m => m.Kind == MarkKind.Comment && m.Length > 0 && m.End == y && commentIds.ContainsKey(m)))
                sb.Append($"<w:commentRangeEnd w:id=\"{commentIds[m]}\"/><w:r><w:rPr><w:rStyle w:val=\"CommentReference\"/></w:rPr><w:commentReference w:id=\"{commentIds[m]}\"/></w:r>");
            Notes(y);
        }
        if (openLink is not null) sb.Append("</w:hyperlink>");
        return sb.Append("</w:p>").ToString();
    }

    static string RunXml(string t, Fmt f, double? size, bool link, bool deleted)
    {
        var sb = new StringBuilder("<w:r>");
        if (f != Fmt.None || size is not null || link)
        {
            sb.Append("<w:rPr>");
            if (link) sb.Append("<w:rStyle w:val=\"Hyperlink\"/>");
            if (f.HasFlag(Fmt.Bold)) sb.Append("<w:b/>");
            if (f.HasFlag(Fmt.Italic)) sb.Append("<w:i/>");
            if (f.HasFlag(Fmt.Strike)) sb.Append("<w:strike/>");
            if (f.HasFlag(Fmt.Underline)) sb.Append("<w:u w:val=\"single\"/>");
            if (f.HasFlag(Fmt.Super)) sb.Append("<w:vertAlign w:val=\"superscript\"/>");
            if (f.HasFlag(Fmt.Sub)) sb.Append("<w:vertAlign w:val=\"subscript\"/>");
            if (size is { } s) sb.Append($"<w:sz w:val=\"{(int)Math.Round(s * 2)}\"/>");
            sb.Append("</w:rPr>");
        }
        var tag = deleted ? "delText" : "t";
        var lines = t.Split('\n'); // weicher Zeilenumbruch im Absatz
        for (var l = 0; l < lines.Length; l++)
        {
            if (l > 0) sb.Append("<w:br/>");
            var parts = lines[l].Split('\t');
            for (var i = 0; i < parts.Length; i++)
            {
                if (i > 0) sb.Append("<w:tab/>");
                if (parts[i].Length > 0) sb.Append($"<w:{tag} xml:space=\"preserve\">{Esc(parts[i])}</w:{tag}>");
            }
        }
        return sb.Append("</w:r>").ToString();
    }

    static string Esc(string s) => SecurityElement.Escape(s) ?? "";
    static void Add(ZipArchive z, string n, string c) { using var w = new StreamWriter(z.CreateEntry(n).Open(), new UTF8Encoding(false)); w.Write(c); }

    static string StyleDef(string id, string name, int halfPt, bool bold, string? color = null, bool italic = false, int outline = -1) =>
        $"<w:style w:type=\"paragraph\" w:styleId=\"{id}\"><w:name w:val=\"{name}\"/><w:basedOn w:val=\"Normal\"/><w:next w:val=\"Normal\"/><w:qFormat/>"
        + $"<w:pPr><w:spacing w:before=\"{(outline >= 0 ? 240 : 0)}\" w:after=\"120\"/>{(outline >= 0 ? $"<w:outlineLvl w:val=\"{outline}\"/>" : "")}</w:pPr>"
        + $"<w:rPr>{(bold ? "<w:b/>" : "")}{(italic ? "<w:i/>" : "")}{(color is null ? "" : $"<w:color w:val=\"{color}\"/>")}<w:sz w:val=\"{halfPt}\"/></w:rPr></w:style>";

    static string Styles(PageSetup page)
    {
        var font = Esc(page.Font); var hc = page.HeadingColor.TrimStart('#');
        return $"{Xml}<w:styles xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\">"
            + $"<w:docDefaults><w:rPrDefault><w:rPr><w:rFonts w:ascii=\"{font}\" w:hAnsi=\"{font}\" w:cs=\"{font}\"/><w:sz w:val=\"22\"/><w:lang w:val=\"de-DE\"/></w:rPr></w:rPrDefault><w:pPrDefault><w:pPr><w:spacing w:after=\"120\" w:line=\"276\" w:lineRule=\"auto\"/></w:pPr></w:pPrDefault></w:docDefaults>"
            + "<w:style w:type=\"paragraph\" w:default=\"1\" w:styleId=\"Normal\"><w:name w:val=\"Normal\"/><w:qFormat/></w:style>"
            + StyleDef("Title", "Title", 52, true, hc) + StyleDef("Subtitle", "Subtitle", 32, false, "595959")
            + StyleDef("Heading1", "heading 1", 36, true, hc, outline: 0) + StyleDef("Heading2", "heading 2", 30, true, hc, outline: 1) + StyleDef("Heading3", "heading 3", 26, true, hc, outline: 2)
            + StyleDef("Quote", "Quote", 22, false, "404040", italic: true)
            + StyleDef("Caption", "caption", 18, false, "595959", italic: true) + StyleDef("TOCHeading", "TOC Heading", 32, true, hc)
            + StyleDef("TOC1", "toc 1", 22, false) + "<w:style w:type=\"paragraph\" w:styleId=\"TOC2\"><w:name w:val=\"toc 2\"/><w:basedOn w:val=\"Normal\"/><w:pPr><w:ind w:left=\"220\"/></w:pPr></w:style><w:style w:type=\"paragraph\" w:styleId=\"TOC3\"><w:name w:val=\"toc 3\"/><w:basedOn w:val=\"Normal\"/><w:pPr><w:ind w:left=\"440\"/></w:pPr></w:style>"
            + "<w:style w:type=\"paragraph\" w:styleId=\"Header\"><w:name w:val=\"header\"/><w:basedOn w:val=\"Normal\"/></w:style><w:style w:type=\"paragraph\" w:styleId=\"Footer\"><w:name w:val=\"footer\"/><w:basedOn w:val=\"Normal\"/></w:style>"
            + "<w:style w:type=\"paragraph\" w:styleId=\"FootnoteText\"><w:name w:val=\"footnote text\"/><w:basedOn w:val=\"Normal\"/><w:rPr><w:sz w:val=\"18\"/></w:rPr></w:style><w:style w:type=\"paragraph\" w:styleId=\"EndnoteText\"><w:name w:val=\"endnote text\"/><w:basedOn w:val=\"Normal\"/><w:rPr><w:sz w:val=\"18\"/></w:rPr></w:style><w:style w:type=\"paragraph\" w:styleId=\"CommentText\"><w:name w:val=\"annotation text\"/><w:basedOn w:val=\"Normal\"/></w:style>"
            + "<w:style w:type=\"character\" w:styleId=\"Hyperlink\"><w:name w:val=\"Hyperlink\"/><w:rPr><w:color w:val=\"0563C1\"/><w:u w:val=\"single\"/></w:rPr></w:style>"
            + "<w:style w:type=\"character\" w:styleId=\"FootnoteReference\"><w:name w:val=\"footnote reference\"/><w:rPr><w:vertAlign w:val=\"superscript\"/></w:rPr></w:style><w:style w:type=\"character\" w:styleId=\"EndnoteReference\"><w:name w:val=\"endnote reference\"/><w:rPr><w:vertAlign w:val=\"superscript\"/></w:rPr></w:style><w:style w:type=\"character\" w:styleId=\"CommentReference\"><w:name w:val=\"annotation reference\"/><w:rPr><w:sz w:val=\"16\"/></w:rPr></w:style>"
            + "<w:style w:type=\"table\" w:styleId=\"TableGrid\"><w:name w:val=\"Table Grid\"/></w:style></w:styles>";
    }

    static readonly string Numbering = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><w:numbering xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\">"
        + "<w:abstractNum w:abstractNumId=\"0\"><w:lvl w:ilvl=\"0\"><w:start w:val=\"1\"/><w:numFmt w:val=\"bullet\"/><w:lvlText w:val=\"•\"/><w:lvlJc w:val=\"left\"/><w:pPr><w:ind w:left=\"720\" w:hanging=\"360\"/></w:pPr></w:lvl></w:abstractNum>"
        + "<w:abstractNum w:abstractNumId=\"1\"><w:lvl w:ilvl=\"0\"><w:start w:val=\"1\"/><w:numFmt w:val=\"decimal\"/><w:lvlText w:val=\"%1.\"/><w:lvlJc w:val=\"left\"/><w:pPr><w:ind w:left=\"720\" w:hanging=\"360\"/></w:pPr></w:lvl></w:abstractNum>"
        + "<w:num w:numId=\"1\"><w:abstractNumId w:val=\"0\"/></w:num><w:num w:numId=\"2\"><w:abstractNumId w:val=\"1\"/></w:num></w:numbering>";

    // ======================= Lesen =======================
    sealed class RCtx
    {
        public required ZipArchive Z;
        public required Dictionary<string, string> Rels, ExternalRels;
        public required Dictionary<string, string> StyleNames;
        public required Func<string?, ListKind> NumKind;
        public Dictionary<string, string> Comments = [], Footnotes = [], Endnotes = [];
    }

    /// <summary>Liest .docx (auch von Word oder LibreOffice) mit Seite einrichten, Kopf-/Fußzeile, Links, Kommentaren, Fuß-/Endnoten,
    /// Änderungen, Inhaltsverzeichnis, Bildbeschriftungen und Alternativtext.</summary>
    public static TextDoc Read(string path)
    {
        using var z = OfficeZip.OpenRead(path);
        XDocument Load(string n) { var e = z.GetEntry(n); if (e is null) return new XDocument(); return e.LoadXml(); }
        var main = Load("word/document.xml");
        var relEls = Load("word/_rels/document.xml.rels").Root?.Elements().ToList() ?? [];
        var rels = relEls.ToDictionary(e => (string?)e.Attribute("Id") ?? "", e => (string?)e.Attribute("Target") ?? "");
        string? RelOfType(string type) => relEls.FirstOrDefault(e => ((string?)e.Attribute("Type"))?.EndsWith("/" + type, StringComparison.Ordinal) == true)?.Attribute("Target")?.Value;
        var stylesDoc = Load("word/styles.xml");
        var styleNames = stylesDoc.Root?.Elements(W + "style").ToDictionary(s => (string?)s.Attribute(W + "styleId") ?? "", s => (string?)s.Element(W + "name")?.Attribute(W + "val") ?? "") ?? [];
        var numbering = Load("word/numbering.xml").Root;
        ListKind NumKind(string? numId)
        {
            if (numId is null || numId == "0" || numbering is null) return numId is null || numId == "0" ? ListKind.None : ListKind.Bullet;
            var abs = numbering.Elements(W + "num").FirstOrDefault(n => (string?)n.Attribute(W + "numId") == numId)?.Element(W + "abstractNumId")?.Attribute(W + "val")?.Value;
            var fmt = numbering.Elements(W + "abstractNum").FirstOrDefault(a => (string?)a.Attribute(W + "abstractNumId") == abs)?.Elements(W + "lvl").FirstOrDefault()?.Element(W + "numFmt")?.Attribute(W + "val")?.Value;
            return fmt == "bullet" || fmt is null ? ListKind.Bullet : ListKind.Number;
        }
        static string PText(XElement p) => string.Concat(p.Descendants().Where(e => e.Name == W + "t" || e.Name == W + "tab").Select(e => e.Name == W + "tab" ? "\t" : e.Value));
        Dictionary<string, string> NotesOf(string? target, string tag) =>
            target is null ? [] : Load("word/" + target).Root?.Elements(W + tag).Where(n => (string?)n.Attribute(W + "type") is null or "normal")
                .ToDictionary(n => (string?)n.Attribute(W + "id") ?? "", n => string.Join("\n", n.Elements(W + "p").Select(PText)).Trim()) ?? [];
        var ctx = new RCtx
        {
            Z = z, Rels = rels, StyleNames = styleNames, NumKind = NumKind,
            ExternalRels = relEls.Where(e => ((string?)e.Attribute("Type"))?.EndsWith("/hyperlink", StringComparison.Ordinal) == true).ToDictionary(e => (string?)e.Attribute("Id") ?? "", e => (string?)e.Attribute("Target") ?? ""),
            Comments = RelOfType("comments") is { } ct ? Load("word/" + ct).Root?.Elements(W + "comment").ToDictionary(c => (string?)c.Attribute(W + "id") ?? "", c => string.Join("\n", c.Elements(W + "p").Select(PText)).Trim()) ?? [] : [],
            Footnotes = NotesOf(RelOfType("footnotes"), "footnote"), Endnotes = NotesOf(RelOfType("endnotes"), "endnote"),
        };
        var doc = new TextDoc();
        var body = main.Root?.Element(W + "body"); if (body is null) return TextDoc.Empty();
        var tocDepth = 0;
        foreach (var el in body.Elements())
        {
            if (el.Name == W + "p")
            {
                // Inhaltsverzeichnis-Feld (TOC): Absätze bis zum Feldende werden ein Verzeichnis-Baustein
                var instr = string.Concat(el.Descendants(W + "instrText").Select(x => x.Value)).Trim();
                if (tocDepth == 0 && instr.StartsWith("TOC", StringComparison.Ordinal))
                {
                    if (doc.Blocks.Count > 0 && doc.Blocks[^1] is Para { Text: var tt } prevTitle && prevTitle.Style == ParaStyle.Text && tt.Trim() is "Inhalt" or "Inhaltsverzeichnis" or "Contents" or "Table of Contents") doc.Blocks.RemoveAt(doc.Blocks.Count - 1);
                    doc.Blocks.Add(new TocBlock());
                }
                if (tocDepth > 0 || instr.StartsWith("TOC", StringComparison.Ordinal))
                {
                    foreach (var fc in el.Descendants(W + "fldChar")) tocDepth += (string?)fc.Attribute(W + "fldCharType") switch { "begin" => 1, "end" => -1, _ => 0 };
                    continue;
                }
                var sid = (string?)el.Element(W + "pPr")?.Element(W + "pStyle")?.Attribute(W + "val");
                var sname = sid is not null && styleNames.TryGetValue(sid, out var sn) ? sn : sid;
                if (sname?.Equals("TOC Heading", StringComparison.OrdinalIgnoreCase) == true) continue;
                if (sname?.Equals("caption", StringComparison.OrdinalIgnoreCase) == true && doc.Blocks.Count > 0 && doc.Blocks[^1] is Picture pic)
                {
                    var cap = PText(el); var colon = cap.IndexOf(':');
                    pic.Caption = colon >= 0 && cap.StartsWith("Abb", StringComparison.OrdinalIgnoreCase) ? cap[(colon + 1)..].Trim() : cap.Trim();
                    continue;
                }
                ReadPara(el, doc, ctx);
            }
            else if (el.Name == W + "tbl")
            {
                var t = new Table();
                foreach (var tr in el.Elements(W + "tr"))
                    t.Rows.Add(tr.Elements(W + "tc").Select(tc => string.Join("\n", tc.Elements(W + "p").Select(p => string.Concat(p.Descendants(W + "t").Select(x => x.Value))))).ToList());
                if (t.Rows.Count > 0) doc.Blocks.Add(t);
            }
            else if (el.Name == W + "sdt")
            {
                var gallery = (string?)el.Descendants(W + "docPartGallery").FirstOrDefault()?.Attribute(W + "val");
                if (gallery?.Contains("Contents", StringComparison.OrdinalIgnoreCase) == true || gallery?.Contains("Inhalt", StringComparison.OrdinalIgnoreCase) == true) { doc.Blocks.Add(new TocBlock()); continue; }
                foreach (var p in el.Descendants(W + "p")) ReadPara(p, doc, ctx);
            }
        }
        // Seite einrichten (letzter Abschnitt), Kopf- und Fußzeile
        var sect = body.Element(W + "sectPr") ?? body.Descendants(W + "sectPr").LastOrDefault();
        if (sect is not null)
        {
            var pg = doc.Page;
            double Cm(XElement? e, string a, double def) => double.TryParse((string?)e?.Attribute(W + a), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? Math.Round(v / TwipsPerCm, 2) : def;
            var sz = sect.Element(W + "pgSz");
            pg.Landscape = (string?)sz?.Attribute(W + "orient") == "landscape" || Cm(sz, "w", 21) > Cm(sz, "h", 29.7);
            var mar = sect.Element(W + "pgMar");
            pg.Top = Cm(mar, "top", pg.Top); pg.Bottom = Cm(mar, "bottom", pg.Bottom); pg.Left = Cm(mar, "left", pg.Left); pg.Right = Cm(mar, "right", pg.Right);
            pg.Columns = int.TryParse((string?)sect.Element(W + "cols")?.Attribute(W + "num"), out var cols) ? Math.Clamp(cols, 1, 3) : 1;
            pg.PageBorder = sect.Element(W + "pgBorders") is not null;
            foreach (var hr in sect.Elements(W + "headerReference").Concat(sect.Elements(W + "footerReference")))
            {
                if ((string?)hr.Attribute(W + "type") is not (null or "default")) continue;
                var rid = (string?)hr.Attribute(R + "id"); if (rid is null || !rels.TryGetValue(rid, out var target)) continue;
                var part = Load("word/" + target).Root; if (part is null) continue;
                if (hr.Name == W + "headerReference")
                {
                    if (part.Descendants(Vml + "textpath").Select(tp => tp.Attribute("string")).FirstOrDefault(a => a is not null) is { } wm) pg.Watermark = wm.Value;
                    pg.Header = string.Join(" ", part.Descendants(W + "p").Select(PText).Where(s => s.Trim().Length > 0)).Trim();
                }
                else
                {
                    var paras = part.Descendants(W + "p").ToList();
                    bool IsPageNo(XElement p) => p.Descendants(W + "fldSimple").Any(f => ((string?)f.Attribute(W + "instr"))?.Contains("PAGE") == true) || p.Descendants(W + "instrText").Any(i => i.Value.Contains("PAGE"));
                    pg.PageNumbers = paras.Any(IsPageNo);
                    pg.Footer = string.Join(" ", paras.Where(p => !IsPageNo(p)).Select(PText).Where(s => s.Trim().Length > 0)).Trim();
                }
            }
        }
        if (Load("word/settings.xml").Root?.Element(W + "autoHyphenation") is { } ah && (string?)ah.Attribute(W + "val") is not ("0" or "false")) doc.Page.Hyphenate = true;
        var defFont = (string?)stylesDoc.Root?.Element(W + "docDefaults")?.Descendants(W + "rFonts").FirstOrDefault()?.Attribute(W + "ascii");
        if (PageSetup.Designs.FirstOrDefault(d => d.Font == defFont) is { Id: { } did } && did != "standard")
        {
            var h1 = (string?)stylesDoc.Root?.Elements(W + "style").FirstOrDefault(s => (string?)s.Attribute(W + "styleId") == "Heading1")?.Descendants(W + "color").FirstOrDefault()?.Attribute(W + "val");
            if (PageSetup.Designs.FirstOrDefault(d => d.Font == defFont && d.Color.TrimStart('#').Equals(h1, StringComparison.OrdinalIgnoreCase)) is { Id: { } exact }) doc.Page.Design = exact;
        }
        doc.Title = Load("docProps/core.xml").Root?.Elements().FirstOrDefault(e => e.Name.LocalName == "title")?.Value is { Length: > 0 } title ? title : null;
        if (doc.Blocks.Count == 0) doc.Blocks.Add(new Para());
        return doc;
    }

    static void ReadPara(XElement p, TextDoc doc, RCtx ctx)
    {
        var ppr = p.Element(W + "pPr");
        if (ppr?.Element(W + "pageBreakBefore") is { } pbb && (string?)pbb.Attribute(W + "val") is not ("0" or "false")) doc.Blocks.Add(new PageBreak());
        var sid = (string?)ppr?.Element(W + "pStyle")?.Attribute(W + "val");
        var style = FromStyleId(sid);
        if (style == ParaStyle.Text && sid is not null && ctx.StyleNames.TryGetValue(sid, out var sname)) style = FromStyleId(sname);
        Para NewPara() => new()
        {
            Style = style,
            Align = ((string?)ppr?.Element(W + "jc")?.Attribute(W + "val")) switch { "center" => Align.Center, "right" or "end" => Align.Right, "both" or "distribute" => Align.Justify, _ => Align.Left },
            List = ppr?.Element(W + "numPr") is { } np ? ctx.NumKind((string?)np.Element(W + "numId")?.Attribute(W + "val")) : ListKind.None,
            IndentCm = double.TryParse((string?)(ppr?.Element(W + "ind")?.Attribute(W + "left") ?? ppr?.Element(W + "ind")?.Attribute(W + "start")), NumberStyles.Float, CultureInfo.InvariantCulture, out var ind) && ppr?.Element(W + "numPr") is null ? Math.Round(ind / TwipsPerCm, 2) : 0,
            SpaceAfterPt = double.TryParse((string?)ppr?.Element(W + "spacing")?.Attribute(W + "after"), NumberStyles.Float, CultureInfo.InvariantCulture, out var sa) ? sa / 20 : null,
        };
        var para = NewPara();
        var runs = new List<(string, Fmt, double?)>(); var marks = new List<Mark>(); var len = 0;
        var commentStart = new Dictionary<string, int>();
        void Text(string s, Fmt f, double? size) { runs.Add((s, f, size)); len += s.Length; }
        void Flush(bool keepEmpty = false) { para.SetRuns(runs); para.Marks = marks; if (para.Text.Length > 0 || keepEmpty || para.Marks.Count > 0) doc.Blocks.Add(para); runs = []; marks = []; len = 0; para = NewPara(); }
        void Walk(XElement container, string? link, MarkKind? rev)
        {
            foreach (var e in container.Elements())
            {
                if (e.Name == W + "hyperlink")
                {
                    var url = (string?)e.Attribute(R + "id") is { } rid && ctx.ExternalRels.TryGetValue(rid, out var u) ? u : (string?)e.Attribute(W + "anchor") is { } anc ? "#" + anc : null;
                    var start = len; Walk(e, url ?? link, rev);
                    if (url is not null && len > start) marks.Add(new Mark(start, len - start, MarkKind.Link, url));
                }
                else if (e.Name == W + "ins" || e.Name == W + "del" || e.Name == W + "moveTo" || e.Name == W + "moveFrom")
                {
                    var kind = e.Name == W + "ins" || e.Name == W + "moveTo" ? MarkKind.Insert : MarkKind.Delete;
                    var start = len; Walk(e, link, kind);
                    if (len > start) marks.Add(new Mark(start, len - start, kind));
                }
                else if (e.Name == W + "smartTag" || e.Name == W + "fldSimple" || e.Name == W + "customXml" || e.Name == W + "sdt" || e.Name == W + "sdtContent") Walk(e.Name == W + "sdt" ? e.Element(W + "sdtContent") ?? e : e, link, rev);
                else if (e.Name == W + "commentRangeStart") commentStart[(string?)e.Attribute(W + "id") ?? ""] = len;
                else if (e.Name == W + "commentRangeEnd")
                {
                    var id = (string?)e.Attribute(W + "id") ?? "";
                    if (ctx.Comments.TryGetValue(id, out var ct)) { var st = commentStart.GetValueOrDefault(id, len); marks.Add(new Mark(st, len - st, MarkKind.Comment, ct)); ctx.Comments.Remove(id); }
                }
                else if (e.Name == W + "r") Run(e);
            }
        }
        void Run(XElement r)
        {
            var rpr = r.Element(W + "rPr");
            bool On(string n) => rpr?.Element(W + n) is { } x && (string?)x.Attribute(W + "val") is not ("0" or "false" or "none");
            var f = Fmt.None;
            if (On("b")) f |= Fmt.Bold; if (On("i")) f |= Fmt.Italic; if (On("strike") || On("dstrike")) f |= Fmt.Strike; if (On("u")) f |= Fmt.Underline;
            var va = (string?)rpr?.Element(W + "vertAlign")?.Attribute(W + "val");
            var rstyle = (string?)rpr?.Element(W + "rStyle")?.Attribute(W + "val");
            if (rstyle is "Hyperlink" or "InternetLink") f &= ~Fmt.Underline;
            if (va == "superscript" && rstyle is not ("FootnoteReference" or "EndnoteReference")) f |= Fmt.Super; else if (va == "subscript") f |= Fmt.Sub;
            double? size = double.TryParse((string?)rpr?.Element(W + "sz")?.Attribute(W + "val"), NumberStyles.Float, CultureInfo.InvariantCulture, out var hp) ? hp / 2 : null;
            if (size is { } sz && Math.Abs(sz - TextDoc.StyleSize(para.Style)) < 0.01) size = null;
            foreach (var c in r.Elements())
            {
                if (c.Name == W + "t" || c.Name == W + "delText") Text(c.Value, f, size);
                else if (c.Name == W + "tab") Text("\t", f, size);
                else if (c.Name == W + "footnoteReference" && ctx.Footnotes.TryGetValue((string?)c.Attribute(W + "id") ?? "", out var fn)) marks.Add(new Mark(len, 0, MarkKind.Footnote, fn));
                else if (c.Name == W + "endnoteReference" && ctx.Endnotes.TryGetValue((string?)c.Attribute(W + "id") ?? "", out var en)) marks.Add(new Mark(len, 0, MarkKind.Endnote, en));
                else if (c.Name == W + "commentReference" && ctx.Comments.TryGetValue((string?)c.Attribute(W + "id") ?? "", out var cm)) { marks.Add(new Mark(len, 0, MarkKind.Comment, cm)); ctx.Comments.Remove((string?)c.Attribute(W + "id") ?? ""); }
                else if (c.Name == W + "br" && (string?)c.Attribute(W + "type") == "page") { Flush(); doc.Blocks.Add(new PageBreak()); }
                else if (c.Name == W + "br" || c.Name == W + "cr") Text("\n", f, size); // weicher Zeilenumbruch bleibt im Absatz
                else if (c.Name == W + "drawing" && c.Descendants(A + "blip").FirstOrDefault()?.Attribute(R + "embed")?.Value is { } rid && ctx.Rels.TryGetValue(rid, out var target))
                {
                    var entry = ctx.Z.GetEntry("word/" + target.TrimStart('/').Replace("../", ""));
                    if (entry is null) continue;
                    using var ms = new MemoryStream(entry.ReadBytes());
                    var ext = c.Descendants(WP + "extent").FirstOrDefault();
                    Flush();
                    doc.Blocks.Add(new Picture
                    {
                        Data = ms.ToArray(), Ext = Path.GetExtension(target).TrimStart('.').ToLowerInvariant() is { Length: > 0 } e ? e.Replace("jpeg", "jpg") : "png",
                        WidthCm = long.TryParse((string?)ext?.Attribute("cx"), out var cx) ? cx / (double)EmuPerCm : 12,
                        HeightCm = long.TryParse((string?)ext?.Attribute("cy"), out var cy) ? cy / (double)EmuPerCm : 8,
                        Alt = (string?)c.Descendants(WP + "docPr").FirstOrDefault()?.Attribute("descr") ?? "",
                    });
                }
            }
        }
        Walk(p, null, null);
        var hadBreakOrPic = p.Descendants(W + "br").Any(b => (string?)b.Attribute(W + "type") == "page") || p.Descendants(W + "drawing").Any();
        // Absatz, der nur einen Seitenumbruch oder ein Bild trug, bleibt nicht als leerer Absatz zurück
        para.SetRuns(runs); para.Marks = marks;
        if (para.Text.Length > 0 || para.Marks.Count > 0 || !hadBreakOrPic) doc.Blocks.Add(para);
    }
}
