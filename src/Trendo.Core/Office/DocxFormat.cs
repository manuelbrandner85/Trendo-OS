using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;
using System.Xml.Linq;

namespace Trendo.Core.Office;

/// <summary>Word-Dateien (.docx, Office Open XML) lesen und schreiben – ohne Fremdbibliothek. Absatzarten werden als echte Formatvorlagen
/// (Title, Subtitle, Heading1–3, Quote) gespeichert, damit Word und LibreOffice sie als Überschriften erkennen.</summary>
public static class DocxFormat
{
    static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";
    static readonly XNamespace Pic = "http://schemas.openxmlformats.org/drawingml/2006/picture";
    static readonly XNamespace WP = "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing";
    const long EmuPerCm = 360000;

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

    // ---------- Schreiben ----------
    public static void Write(TextDoc doc, string path)
    {
        var body = new StringBuilder(); var rels = new StringBuilder(); var media = new List<(string Name, byte[] Data)>();
        var picId = 0; var breakBefore = false;
        foreach (var b in doc.Blocks)
        {
            if (b is PageBreak) { if (breakBefore) body.Append("<w:p><w:r><w:br w:type=\"page\"/></w:r></w:p>"); breakBefore = true; continue; }
            // Seitenumbruch wie in Word: als Eigenschaft des nächsten Absatzes (vor Tabellen als eigener Umbruch)
            if (breakBefore && b is Table) { body.Append("<w:p><w:r><w:br w:type=\"page\"/></w:r></w:p>"); breakBefore = false; }
            var pb = breakBefore ? "<w:pageBreakBefore/>" : ""; breakBefore = false;
            switch (b)
            {
                case Para p: body.Append(ParaXml(p, pb)); break;
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
                    picId++;
                    var name = $"bild{picId}.{pic.Ext}"; media.Add((name, pic.Data));
                    rels.Append($"<Relationship Id=\"rIdImg{picId}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/image\" Target=\"media/{name}\"/>");
                    long cx = (long)(pic.WidthCm * EmuPerCm), cy = (long)(pic.HeightCm * EmuPerCm);
                    body.Append($"<w:p>{(pb.Length > 0 ? $"<w:pPr>{pb}</w:pPr>" : "")}<w:r><w:drawing><wp:inline><wp:extent cx=\"{cx}\" cy=\"{cy}\"/><wp:docPr id=\"{picId}\" name=\"Bild {picId}\"/>"
                        + "<a:graphic><a:graphicData uri=\"http://schemas.openxmlformats.org/drawingml/2006/picture\"><pic:pic>"
                        + $"<pic:nvPicPr><pic:cNvPr id=\"{picId}\" name=\"{name}\"/><pic:cNvPicPr/></pic:nvPicPr>"
                        + $"<pic:blipFill><a:blip r:embed=\"rIdImg{picId}\"/><a:stretch><a:fillRect/></a:stretch></pic:blipFill>"
                        + $"<pic:spPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"{cx}\" cy=\"{cy}\"/></a:xfrm><a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom></pic:spPr>"
                        + "</pic:pic></a:graphicData></a:graphic></wp:inline></w:drawing></w:r></w:p>");
                    break;
            }
        }
        if (breakBefore) body.Append("<w:p><w:r><w:br w:type=\"page\"/></w:r></w:p>");
        var docXml = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
            + "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\" xmlns:wp=\"http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing\" xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" xmlns:pic=\"http://schemas.openxmlformats.org/drawingml/2006/picture\"><w:body>"
            + body + "<w:sectPr><w:pgSz w:w=\"11906\" w:h=\"16838\"/><w:pgMar w:top=\"1417\" w:right=\"1417\" w:bottom=\"1134\" w:left=\"1417\" w:header=\"708\" w:footer=\"708\" w:gutter=\"0\"/></w:sectPr></w:body></w:document>";
        var exts = media.Select(m => Path.GetExtension(m.Name).TrimStart('.')).Distinct()
            .Select(e => $"<Default Extension=\"{e}\" ContentType=\"image/{(e == "jpg" ? "jpeg" : e)}\"/>");
        var tmp = path + ".tmp";
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using (var z = ZipFile.Open(tmp, ZipArchiveMode.Create))
        {
            Add(z, "[Content_Types].xml", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/>"
                + string.Concat(exts)
                + "<Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/><Override PartName=\"/word/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml\"/><Override PartName=\"/word/numbering.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.numbering+xml\"/><Override PartName=\"/docProps/core.xml\" ContentType=\"application/vnd.openxmlformats-package.core-properties+xml\"/></Types>");
            Add(z, "_rels/.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/><Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties\" Target=\"docProps/core.xml\"/></Relationships>");
            Add(z, "word/_rels/document.xml.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rIdStyles\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/><Relationship Id=\"rIdNum\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/numbering\" Target=\"numbering.xml\"/>" + rels + "</Relationships>");
            Add(z, "word/document.xml", docXml);
            Add(z, "word/styles.xml", Styles);
            Add(z, "word/numbering.xml", Numbering);
            Add(z, "docProps/core.xml", $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><cp:coreProperties xmlns:cp=\"http://schemas.openxmlformats.org/package/2006/metadata/core-properties\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\"><dc:title>{Esc(doc.Title ?? "")}</dc:title><dc:creator>Trendo Text</dc:creator></cp:coreProperties>");
            foreach (var (n, data) in media) { using var s = z.CreateEntry("word/media/" + n).Open(); s.Write(data); }
        }
        File.Move(tmp, path, true);
    }

    static string ParaXml(Para p, string extra = "")
    {
        var sb = new StringBuilder("<w:p><w:pPr>");
        if (p.Style != ParaStyle.Text) sb.Append($"<w:pStyle w:val=\"{StyleId(p.Style)}\"/>");
        if (p.List != ListKind.None) sb.Append($"<w:numPr><w:ilvl w:val=\"0\"/><w:numId w:val=\"{(p.List == ListKind.Bullet ? 1 : 2)}\"/></w:numPr>");
        sb.Append(extra);
        if (p.Align != Align.Left) sb.Append($"<w:jc w:val=\"{p.Align switch { Align.Center => "center", Align.Right => "right", _ => "both" }}\"/>");
        sb.Append("</w:pPr>");
        foreach (var (t, f, size) in p.Runs())
        {
            sb.Append("<w:r>");
            if (f != Fmt.None || size is not null)
            {
                sb.Append("<w:rPr>");
                if (f.HasFlag(Fmt.Bold)) sb.Append("<w:b/>");
                if (f.HasFlag(Fmt.Italic)) sb.Append("<w:i/>");
                if (f.HasFlag(Fmt.Strike)) sb.Append("<w:strike/>");
                if (f.HasFlag(Fmt.Underline)) sb.Append("<w:u w:val=\"single\"/>");
                if (f.HasFlag(Fmt.Super)) sb.Append("<w:vertAlign w:val=\"superscript\"/>");
                if (f.HasFlag(Fmt.Sub)) sb.Append("<w:vertAlign w:val=\"subscript\"/>");
                if (size is { } s) sb.Append($"<w:sz w:val=\"{(int)Math.Round(s * 2)}\"/>");
                sb.Append("</w:rPr>");
            }
            var lines = t.Split('\n'); // weicher Zeilenumbruch im Absatz
            for (var l = 0; l < lines.Length; l++)
            {
                if (l > 0) sb.Append("<w:br/>");
                var parts = lines[l].Split('\t');
                for (var i = 0; i < parts.Length; i++)
                {
                    if (i > 0) sb.Append("<w:tab/>");
                    if (parts[i].Length > 0) sb.Append($"<w:t xml:space=\"preserve\">{Esc(parts[i])}</w:t>");
                }
            }
            sb.Append("</w:r>");
        }
        return sb.Append("</w:p>").ToString();
    }

    static string Esc(string s) => SecurityElement.Escape(s) ?? "";
    static void Add(ZipArchive z, string n, string c) { using var w = new StreamWriter(z.CreateEntry(n).Open(), new UTF8Encoding(false)); w.Write(c); }

    static string StyleDef(string id, string name, int halfPt, bool bold, string? color = null, bool italic = false, int outline = -1) =>
        $"<w:style w:type=\"paragraph\" w:styleId=\"{id}\"><w:name w:val=\"{name}\"/><w:basedOn w:val=\"Normal\"/><w:next w:val=\"Normal\"/><w:qFormat/>"
        + $"<w:pPr><w:spacing w:before=\"{(outline >= 0 ? 240 : 0)}\" w:after=\"120\"/>{(outline >= 0 ? $"<w:outlineLvl w:val=\"{outline}\"/>" : "")}</w:pPr>"
        + $"<w:rPr>{(bold ? "<w:b/>" : "")}{(italic ? "<w:i/>" : "")}{(color is null ? "" : $"<w:color w:val=\"{color}\"/>")}<w:sz w:val=\"{halfPt}\"/></w:rPr></w:style>";

    static readonly string Styles = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><w:styles xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\">"
        + "<w:docDefaults><w:rPrDefault><w:rPr><w:rFonts w:ascii=\"Arial\" w:hAnsi=\"Arial\" w:cs=\"Arial\"/><w:sz w:val=\"22\"/><w:lang w:val=\"de-DE\"/></w:rPr></w:rPrDefault><w:pPrDefault><w:pPr><w:spacing w:after=\"120\" w:line=\"276\" w:lineRule=\"auto\"/></w:pPr></w:pPrDefault></w:docDefaults>"
        + "<w:style w:type=\"paragraph\" w:default=\"1\" w:styleId=\"Normal\"><w:name w:val=\"Normal\"/><w:qFormat/></w:style>"
        + StyleDef("Title", "Title", 52, true) + StyleDef("Subtitle", "Subtitle", 32, false, "595959")
        + StyleDef("Heading1", "heading 1", 36, true, "1F3864", outline: 0) + StyleDef("Heading2", "heading 2", 30, true, "2F5496", outline: 1) + StyleDef("Heading3", "heading 3", 26, true, "2F5496", outline: 2)
        + StyleDef("Quote", "Quote", 22, false, "404040", italic: true)
        + "<w:style w:type=\"table\" w:styleId=\"TableGrid\"><w:name w:val=\"Table Grid\"/></w:style></w:styles>";

    static readonly string Numbering = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><w:numbering xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\">"
        + "<w:abstractNum w:abstractNumId=\"0\"><w:lvl w:ilvl=\"0\"><w:start w:val=\"1\"/><w:numFmt w:val=\"bullet\"/><w:lvlText w:val=\"•\"/><w:lvlJc w:val=\"left\"/><w:pPr><w:ind w:left=\"720\" w:hanging=\"360\"/></w:pPr></w:lvl></w:abstractNum>"
        + "<w:abstractNum w:abstractNumId=\"1\"><w:lvl w:ilvl=\"0\"><w:start w:val=\"1\"/><w:numFmt w:val=\"decimal\"/><w:lvlText w:val=\"%1.\"/><w:lvlJc w:val=\"left\"/><w:pPr><w:ind w:left=\"720\" w:hanging=\"360\"/></w:pPr></w:lvl></w:abstractNum>"
        + "<w:num w:numId=\"1\"><w:abstractNumId w:val=\"0\"/></w:num><w:num w:numId=\"2\"><w:abstractNumId w:val=\"1\"/></w:num></w:numbering>";

    // ---------- Lesen ----------
    /// <summary>Liest .docx (auch von Word oder LibreOffice). Unbekanntes (Kopfzeilen, Fußnoten, Felder) wird als Text übernommen oder ausgelassen.</summary>
    public static TextDoc Read(string path)
    {
        using var z = ZipFile.OpenRead(path);
        XDocument Load(string n) { var e = z.GetEntry(n); if (e is null) return new XDocument(); using var s = e.Open(); return XDocument.Load(s); }
        var main = Load("word/document.xml");
        var rels = Load("word/_rels/document.xml.rels").Root?.Elements().ToDictionary(e => (string?)e.Attribute("Id") ?? "", e => (string?)e.Attribute("Target") ?? "") ?? [];
        var styleNames = Load("word/styles.xml").Root?.Elements(W + "style").ToDictionary(s => (string?)s.Attribute(W + "styleId") ?? "", s => (string?)s.Element(W + "name")?.Attribute(W + "val") ?? "") ?? [];
        var numbering = Load("word/numbering.xml").Root;
        ListKind NumKind(string? numId)
        {
            if (numId is null || numId == "0" || numbering is null) return numId is null || numId == "0" ? ListKind.None : ListKind.Bullet;
            var abs = numbering.Elements(W + "num").FirstOrDefault(n => (string?)n.Attribute(W + "numId") == numId)?.Element(W + "abstractNumId")?.Attribute(W + "val")?.Value;
            var fmt = numbering.Elements(W + "abstractNum").FirstOrDefault(a => (string?)a.Attribute(W + "abstractNumId") == abs)?.Elements(W + "lvl").FirstOrDefault()?.Element(W + "numFmt")?.Attribute(W + "val")?.Value;
            return fmt == "bullet" || fmt is null ? ListKind.Bullet : ListKind.Number;
        }
        var doc = new TextDoc();
        var body = main.Root?.Element(W + "body"); if (body is null) return TextDoc.Empty();
        foreach (var el in body.Elements())
        {
            if (el.Name == W + "p") ReadPara(el, doc, rels, z, styleNames, NumKind);
            else if (el.Name == W + "tbl")
            {
                var t = new Table();
                foreach (var tr in el.Elements(W + "tr"))
                    t.Rows.Add(tr.Elements(W + "tc").Select(tc => string.Join("\n", tc.Elements(W + "p").Select(p => string.Concat(p.Descendants(W + "t").Select(x => x.Value))))).ToList());
                if (t.Rows.Count > 0) doc.Blocks.Add(t);
            }
            else if (el.Name == W + "sdt") foreach (var p in el.Descendants(W + "p")) ReadPara(p, doc, rels, z, styleNames, NumKind);
        }
        doc.Title = Load("docProps/core.xml").Root?.Elements().FirstOrDefault(e => e.Name.LocalName == "title")?.Value is { Length: > 0 } title ? title : null;
        if (doc.Blocks.Count == 0) doc.Blocks.Add(new Para());
        return doc;
    }

    static void ReadPara(XElement p, TextDoc doc, Dictionary<string, string> rels, ZipArchive z, Dictionary<string, string> styleNames, Func<string?, ListKind> numKind)
    {
        var ppr = p.Element(W + "pPr");
        if (ppr?.Element(W + "pageBreakBefore") is { } pbb && (string?)pbb.Attribute(W + "val") is not ("0" or "false")) doc.Blocks.Add(new PageBreak());
        var sid = (string?)ppr?.Element(W + "pStyle")?.Attribute(W + "val");
        var style = FromStyleId(sid);
        if (style == ParaStyle.Text && sid is not null && styleNames.TryGetValue(sid, out var sname)) style = FromStyleId(sname);
        var para = new Para
        {
            Style = style,
            Align = ((string?)ppr?.Element(W + "jc")?.Attribute(W + "val")) switch { "center" => Align.Center, "right" or "end" => Align.Right, "both" or "distribute" => Align.Justify, _ => Align.Left },
            List = ppr?.Element(W + "numPr") is { } np ? numKind((string?)np.Element(W + "numId")?.Attribute(W + "val")) : ListKind.None,
        };
        var runs = new List<(string, Fmt, double?)>();
        foreach (var r in p.Elements().SelectMany(e => e.Name == W + "r" ? [e] : e.Name == W + "hyperlink" || e.Name == W + "ins" || e.Name == W + "smartTag" ? e.Elements(W + "r") : Enumerable.Empty<XElement>()))
        {
            var rpr = r.Element(W + "rPr");
            bool On(string n) => rpr?.Element(W + n) is { } x && (string?)x.Attribute(W + "val") is not ("0" or "false" or "none");
            var f = Fmt.None;
            if (On("b")) f |= Fmt.Bold; if (On("i")) f |= Fmt.Italic; if (On("strike") || On("dstrike")) f |= Fmt.Strike; if (On("u")) f |= Fmt.Underline;
            var va = (string?)rpr?.Element(W + "vertAlign")?.Attribute(W + "val");
            if (va == "superscript") f |= Fmt.Super; else if (va == "subscript") f |= Fmt.Sub;
            double? size = double.TryParse((string?)rpr?.Element(W + "sz")?.Attribute(W + "val"), NumberStyles.Float, CultureInfo.InvariantCulture, out var hp) ? hp / 2 : null;
            if (size is { } sz && Math.Abs(sz - TextDoc.StyleSize(para.Style)) < 0.01) size = null;
            foreach (var c in r.Elements())
            {
                if (c.Name == W + "t") runs.Add((c.Value, f, size));
                else if (c.Name == W + "tab") runs.Add(("\t", f, size));
                else if (c.Name == W + "br" && (string?)c.Attribute(W + "type") == "page") { para.SetRuns(runs); if (para.Text.Length > 0) doc.Blocks.Add(para); runs = []; doc.Blocks.Add(new PageBreak()); para = new Para { Style = para.Style, Align = para.Align }; }
                else if (c.Name == W + "br" || c.Name == W + "cr") runs.Add(("\n", f, size)); // weicher Zeilenumbruch bleibt im Absatz
                else if (c.Name == W + "drawing" && c.Descendants(A + "blip").FirstOrDefault()?.Attribute(R + "embed")?.Value is { } rid && rels.TryGetValue(rid, out var target))
                {
                    var entry = z.GetEntry("word/" + target.TrimStart('/').Replace("../", ""));
                    if (entry is null) continue;
                    using var ms = new MemoryStream(); using (var s = entry.Open()) s.CopyTo(ms);
                    var ext = c.Descendants(WP + "extent").FirstOrDefault();
                    para.SetRuns(runs); if (para.Text.Length > 0) doc.Blocks.Add(para); runs = [];
                    doc.Blocks.Add(new Picture
                    {
                        Data = ms.ToArray(), Ext = Path.GetExtension(target).TrimStart('.').ToLowerInvariant() is { Length: > 0 } e ? e.Replace("jpeg", "jpg") : "png",
                        WidthCm = long.TryParse((string?)ext?.Attribute("cx"), out var cx) ? cx / (double)EmuPerCm : 12,
                        HeightCm = long.TryParse((string?)ext?.Attribute("cy"), out var cy) ? cy / (double)EmuPerCm : 8,
                    });
                    para = new Para { Style = para.Style, Align = para.Align };
                }
            }
        }
        para.SetRuns(runs);
        // Absatz, der nur einen Seitenumbruch oder ein Bild trug, bleibt nicht als leerer Absatz zurück
        if (para.Text.Length > 0 || doc.Blocks.Count == 0 || doc.Blocks[^1] is not (PageBreak or Picture) || !p.Descendants(W + "br").Concat(p.Descendants(W + "drawing")).Any()) doc.Blocks.Add(para);
    }
}
