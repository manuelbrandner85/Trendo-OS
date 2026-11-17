using System.IO.Compression;
using System.Xml.Linq;

namespace Trendo.Core.Office;

/// <summary>T-0682: Importprüfung für Trendo Text (DOCX, ODT). Gegenstück zu <see cref="DocxFormat.Read"/> und <see cref="OdtFormat.Read"/>:
/// Was dort übersprungen oder vereinfacht wird, wird hier gezählt.</summary>
public static partial class ImportCheck
{
    static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    static readonly XNamespace Rn = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    static readonly XNamespace An = "http://schemas.openxmlformats.org/drawingml/2006/main";
    static readonly XNamespace Mc = "http://schemas.openxmlformats.org/markup-compatibility/2006";
    static readonly XNamespace Vml = "urn:schemas-microsoft-com:vml";
    static readonly XNamespace Cn = "http://schemas.openxmlformats.org/drawingml/2006/chart";
    static readonly XNamespace Mth = "http://schemas.openxmlformats.org/officeDocument/2006/math";

    // ======================= DOCX =======================
    static readonly HashSet<string> DocxParaKnown = ["hyperlink", "ins", "del", "moveTo", "moveFrom", "smartTag", "fldSimple", "customXml", "sdt", "sdtContent", "commentRangeStart", "commentRangeEnd", "r", "pPr",
        "bookmarkStart", "bookmarkEnd", "proofErr", "permStart", "permEnd", "moveFromRangeStart", "moveFromRangeEnd", "moveToRangeStart", "moveToRangeEnd", "sdtPr", "sdtEndPr", "customXmlPr", "smartTagPr", "rPr"];
    static readonly HashSet<string> DocxRunKnown = ["t", "delText", "tab", "footnoteReference", "endnoteReference", "commentReference", "br", "cr", "drawing", "rPr",
        "lastRenderedPageBreak", "fldChar", "instrText", "delInstrText", "annotationRef", "footnoteRef", "endnoteRef", "separator", "continuationSeparator"];
    static readonly HashSet<string> DocxBodyKnown = ["p", "tbl", "sdt", "sectPr", "bookmarkStart", "bookmarkEnd", "proofErr", "permStart", "permEnd", "moveFromRangeStart", "moveFromRangeEnd", "moveToRangeStart", "moveToRangeEnd", "commentRangeStart", "commentRangeEnd"];
    static readonly HashSet<string> DocxRunFormatUnknown = ["caps", "smallCaps", "outline", "shadow", "emboss", "imprint", "vanish", "spacing", "w", "position", "em", "effect", "bdr"];
    static readonly HashSet<string> DocxBenignStyles = ["normal", "standard", "default", "default paragraph style", "text body", "body text", "textbody", "list paragraph", "no spacing", "caption", "header", "footer",
        "toc heading", "footnote text", "endnote text", "table contents", "table paragraph", "normal (web)", "annotation text", "commenttext", "footnotetext", "endnotetext", "listparagraph", "nospacing", "bodytext"];

    static bool IsDocxObject(XElement e) => e.Name == Mc + "AlternateContent" || e.Name == W + "pict" || e.Name == W + "object" || e.Name == W + "drawing";

    static void Docx(ZipArchive z, Losses l)
    {
        var main = X(z, "word/document.xml");
        var body = main?.Root?.Element(W + "body"); if (body is null) return;
        var rels = RelsOf(z, "word/document.xml");
        var stylesDoc = X(z, "word/styles.xml");
        var styleNames = new Dictionary<string, string>();
        foreach (var s in stylesDoc?.Root?.Elements(W + "style") ?? []) styleNames.TryAdd((string?)s.Attribute(W + "styleId") ?? "", (string?)s.Element(W + "name")?.Attribute(W + "val") ?? "");
        var numbering = X(z, "word/numbering.xml")?.Root;

        // Was der Leser erreicht: Absätze direkt im Text und in Inhaltssteuerelementen, darin die Läufe auf den bekannten Wegen.
        var reachedRuns = new HashSet<XElement>(); var reachedParas = new List<XElement>();
        void WalkP(XElement c)
        {
            foreach (var e in c.Elements())
            {
                if (e.Name.Namespace != W) { if (e.Name.Namespace == Mth) l.Add("docx.formel", "Formeln aus dem Formeleditor"); else if (!IsDocxObject(e)) l.Add("docx.unbekannt", "Bausteine im Text, die Trendo nicht kennt", 1, e.Name.LocalName); continue; }
                var n = e.Name.LocalName;
                if (n == "r") reachedRuns.Add(e);
                else if (n == "sdt") WalkP(e.Element(W + "sdtContent") ?? e);
                else if (n is "hyperlink" or "ins" or "del" or "moveTo" or "moveFrom" or "smartTag" or "fldSimple" or "customXml" or "sdtContent") WalkP(e);
                else if (!DocxParaKnown.Contains(n)) l.Add("docx.unbekannt", "Bausteine im Text, die Trendo nicht kennt", 1, n);
            }
        }
        var tableNo = 0;
        foreach (var el in body.Elements())
        {
            if (el.Name == W + "p") { reachedParas.Add(el); WalkP(el); }
            else if (el.Name == W + "sdt")
            {
                foreach (var p in el.Descendants(W + "p")) { reachedParas.Add(p); WalkP(p); }
                if (el.Descendants(W + "tbl").Any()) l.Add("docx.tabelle.sdt", "Tabellen in Inhaltssteuerelementen (werden zu einfachem Text)", el.Descendants(W + "tbl").Count());
            }
            else if (el.Name == W + "tbl") DocxTable(el, ++tableNo, l);
            else if (el.Name == W + "altChunk") l.Add("docx.fremdteil", "Eingefügte Fremddokumente");
            else if (el.Name.Namespace != W || !DocxBodyKnown.Contains(el.Name.LocalName)) l.Add("docx.unbekannt", "Bausteine im Text, die Trendo nicht kennt", 1, el.Name.LocalName);
        }

        // Textfelder, Formen, Diagramme, Objekte und Bilder
        foreach (var o in body.Outermost(IsDocxObject))
        {
            var inCell = o.Ancestors(W + "tc").Any();
            var blip = o.Descendants(An + "blip").FirstOrDefault();
            var reached = o.Name == W + "drawing" && o.Parent is { } run && reachedRuns.Contains(run) && (string?)blip?.Attribute(Rn + "embed") is { } rid && rels.TryGetValue(rid, out var rel) && !rel.External && z.GetEntry(rel.Target) is not null;
            if (reached)
            {
                if (o.Elements().Any(e => e.Name.LocalName == "anchor")) l.Add("docx.bild.frei", "Frei platzierte Bilder (stehen in Trendo in der Textzeile)");
                continue;
            }
            var text = Short(string.Concat(o.Descendants(W + "t").Select(t => t.Value)));
            if (o.Descendants(Cn + "chart").Any()) l.Add("docx.diagramm", "Diagramme");
            else if (o.Descendants().Any(e => e.Name.LocalName == "relIds" && e.Name.NamespaceName.EndsWith("/diagram", StringComparison.Ordinal))) l.Add("docx.smartart", "SmartArt-Grafiken");
            else if (o.Name == W + "object") l.Add("docx.objekt", "Eingebettete Objekte (z. B. eine Excel-Tabelle oder Formel)");
            else if (o.Descendants().Any(e => e.Name.LocalName is "txbx" or "textbox" or "txbxContent")) l.Add("docx.textfeld", "Textfelder", 1, text.Length > 0 ? $"„{text}“" : null);
            else if (blip is not null || o.Descendants(Vml + "imagedata").Any()) l.Add(inCell ? "docx.zelle.bild" : "docx.bild", inCell ? "Bilder in Tabellenzellen" : "Bilder, die Trendo nicht übernimmt (verknüpft oder in einem Sonderrahmen)");
            else l.Add("docx.form", "Formen und Zeichnungen", 1, text.Length > 0 ? $"„{text}“" : null);
        }

        // Läufe: Zeichen und Formate, die der Leser nicht kennt
        foreach (var r in reachedRuns)
        {
            foreach (var c in r.Elements())
            {
                if (IsDocxObject(c)) continue;
                var n = c.Name.LocalName;
                if (c.Name.Namespace != W) l.Add("docx.unbekannt", "Bausteine im Text, die Trendo nicht kennt", 1, n);
                else if (n == "sym") l.Add("docx.symbol", "Sonderzeichen aus Symbolschriften");
                else if (n is "noBreakHyphen" or "softHyphen") l.Add("docx.bindestrich", "Geschützte Bindestriche und weiche Trennstellen");
                else if (!DocxRunKnown.Contains(n)) l.Add("docx.unbekannt", "Bausteine im Text, die Trendo nicht kennt", 1, n);
            }
            if (r.Element(W + "rPr") is { } rpr && rpr.Elements().Any(e => e.Name.Namespace == W && DocxRunFormatUnknown.Contains(e.Name.LocalName) && Truthy((string?)e.Attribute(W + "val") ?? "1")))
                l.Add("docx.zeichenformat", "Zeichenformate, die Trendo nicht kennt (z. B. Kapitälchen, Sperrung, Schatten)");
        }

        // Absätze: Formatvorlagen, Rahmen, Tabulatoren, Nummerierungsarten
        foreach (var p in reachedParas)
        {
            var ppr = p.Element(W + "pPr"); if (ppr is null) continue;
            if (ppr.Element(W + "pBdr") is not null || (string?)ppr.Element(W + "shd")?.Attribute(W + "fill") is { } pf && pf != "auto") l.Add("docx.absatzrahmen", "Rahmen und Hintergrund von Absätzen");
            if (ppr.Element(W + "tabs")?.Elements(W + "tab").Any(t => (string?)t.Attribute(W + "val") != "clear") == true) l.Add("docx.tabstopp", "Eigene Tabulatoren");
            if (ppr.Element(W + "framePr") is not null) l.Add("docx.rahmen", "Positionsrahmen und Initialen");
            if ((string?)ppr.Element(W + "pStyle")?.Attribute(W + "val") is { } sid)
            {
                var name = styleNames.GetValueOrDefault(sid) is { Length: > 0 } sn ? sn : sid;
                var low = name.ToLowerInvariant();
                if (DocxMapsStyle(sid) || DocxMapsStyle(name) || DocxBenignStyles.Contains(low) || DocxBenignStyles.Contains(sid.ToLowerInvariant()) || low.StartsWith("toc", StringComparison.Ordinal) || low.StartsWith("list", StringComparison.Ordinal) || low.StartsWith("contents", StringComparison.Ordinal)) { }
                else l.Add("docx.vorlage", "Formatvorlagen, die Trendo nicht kennt (Absätze sehen danach wie Standardtext aus)", 1, name);
            }
            if (ppr.Element(W + "numPr") is { } np && numbering is not null && (string?)np.Element(W + "numId")?.Attribute(W + "val") is { } numId && numId != "0")
            {
                var lvl = (string?)np.Element(W + "ilvl")?.Attribute(W + "val") ?? "0";
                var num = numbering.Elements(W + "num").FirstOrDefault(n => (string?)n.Attribute(W + "numId") == numId);
                var abs = (string?)num?.Element(W + "abstractNumId")?.Attribute(W + "val");
                var lv = numbering.Elements(W + "abstractNum").FirstOrDefault(a => (string?)a.Attribute(W + "abstractNumId") == abs)?.Elements(W + "lvl").FirstOrDefault(x => (string?)x.Attribute(W + "ilvl") == lvl);
                var fmt = (string?)lv?.Element(W + "numFmt")?.Attribute(W + "val");
                var own = (int.TryParse(lvl, out var li) ? li % 3 : 0) switch { 0 => "decimal", 1 => "lowerLetter", _ => "lowerRoman" };
                if (fmt is not (null or "bullet") && fmt != own) l.Add("docx.nummerart", "Nummerierungsarten (z. B. a, b, c oder römische Zahlen)");
                if ((string?)lv?.Element(W + "start")?.Attribute(W + "val") is { } st && st != "1" && fmt != "bullet" || num?.Descendants(W + "startOverride").Any(s => (string?)s.Attribute(W + "val") != "1") == true)
                    l.Add("docx.nummerstart", "Nummerierungen, die nicht bei 1 beginnen");
            }
        }

        DocxFields(body, l);

        // Inhaltssteuerelemente und Formularfelder
        var controls = body.Descendants(W + "sdtPr").Count(pr => pr.Elements().Any(e => e.Name.LocalName is "dropDownList" or "comboBox" or "date" or "checkbox" or "picture")) + body.Descendants(W + "ffData").Count();
        l.Add("docx.formular", "Formularfelder (Auswahllisten, Kästchen, Datumsfelder)", controls);

        // Änderungsverfolgung: Formatänderungen und Namen
        l.Add("docx.formataenderung", "Nachverfolgte Formatänderungen", body.Descendants().Count(e => e.Name.Namespace == W && e.Name.LocalName.EndsWith("Change", StringComparison.Ordinal)));
        var authors = body.Descendants().Count(e => (e.Name == W + "ins" || e.Name == W + "del" || e.Name == W + "moveTo" || e.Name == W + "moveFrom") && (string?)e.Attribute(W + "author") is { Length: > 0 });
        if (rels.Values.FirstOrDefault(r => r.Type.EndsWith("/comments", StringComparison.Ordinal)) is { Target.Length: > 0 } cr)
            authors += X(z, cr.Target)?.Root?.Elements(W + "comment").Count(c => (string?)c.Attribute(W + "author") is { Length: > 0 }) ?? 0;
        l.Add("docx.verfasser", "Namen und Datum bei Kommentaren und Änderungen", authors);

        // Abschnitte, Papierformat, Kopf- und Fußzeilen
        var sects = body.Descendants(W + "sectPr").ToList();
        if (sects.Count > 1) l.Add("docx.abschnitte", "Mehrere Abschnitte (eigene Seitenformate, Spalten oder Kopfzeilen je Abschnitt)", sects.Count);
        var sect = body.Element(W + "sectPr") ?? sects.LastOrDefault();
        if (sect is not null)
        {
            var sz = sect.Element(W + "pgSz");
            if (double.TryParse((string?)sz?.Attribute(W + "w"), out var pw) && double.TryParse((string?)sz?.Attribute(W + "h"), out var ph))
            {
                var (a, b) = (Math.Min(pw, ph), Math.Max(pw, ph));
                if (Math.Abs(a - 11906) > 80 || Math.Abs(b - 16838) > 80) l.Add("docx.papier", "Papierformat (Trendo kennt nur A4)");
            }
            if (int.TryParse((string?)sect.Element(W + "cols")?.Attribute(W + "num"), out var cols) && cols > 3) l.Add("docx.spalten", "Mehr als drei Textspalten");
            foreach (var hr in sect.Elements(W + "headerReference").Concat(sect.Elements(W + "footerReference")))
            {
                var head = hr.Name == W + "headerReference"; var name = head ? "Kopfzeile" : "Fußzeile"; var key = head ? "docx.kopf" : "docx.fuss";
                if ((string?)hr.Attribute(W + "type") is "first" or "even") { l.Add(key + ".weitere", $"Eigene {name} für die erste Seite oder für gerade Seiten"); continue; }
                if ((string?)hr.Attribute(Rn + "id") is not { } rid || !rels.TryGetValue(rid, out var rel) || X(z, rel.Target)?.Root is not { } part) continue;
                l.Add(key + ".tabelle", $"Tabelle in der {name} (wird zu einer Textzeile)", part.Descendants(W + "tbl").Count());
                l.Add(key + ".bild", $"Bilder in der {name} (z. B. ein Logo)", part.Descendants(An + "blip").Count() + part.Descendants(Vml + "imagedata").Count());
                l.Add(key + ".textfeld", $"Textfelder und Formen in der {name}", part.Descendants().Count(e => e.Name.LocalName == "txbxContent" && e.Ancestors().All(a => a.Name != Mc + "Fallback")));
                bool PageNo(XElement p) => p.Descendants(W + "fldSimple").Any(f => ((string?)f.Attribute(W + "instr"))?.Contains("PAGE") == true) || p.Descendants(W + "instrText").Any(i => i.Value.Contains("PAGE"));
                var paras = part.Descendants(W + "p").Where(p => !p.Ancestors(W + "tbl").Any() && (head || !PageNo(p))).ToList();
                if (paras.Count(p => string.Concat(p.Descendants(W + "t").Select(t => t.Value)).Trim().Length > 0) > 1) l.Add(key + ".zeilen", $"{name} mit mehreren Zeilen (wird zu einer Zeile)");
                var fields = paras.Sum(p => p.Descendants(W + "fldSimple").Count() + p.Descendants(W + "fldChar").Count(f => (string?)f.Attribute(W + "fldCharType") == "begin"));
                l.Add(key + ".feld", $"Felder in der {name} (z. B. Seitenzahl oder Datum – es bleibt nur der feste Text)", fields);
            }
        }

        Parts(z, l, "docx", n => n.StartsWith("docProps/", StringComparison.Ordinal) || n.StartsWith("customXml/", StringComparison.Ordinal) || n.StartsWith("word/theme/", StringComparison.Ordinal) || n.StartsWith("word/media/", StringComparison.Ordinal) || n.StartsWith("word/glossary/", StringComparison.Ordinal)
                || n.StartsWith("word/header", StringComparison.Ordinal) || n.StartsWith("word/footer", StringComparison.Ordinal) || n.StartsWith("word/charts/", StringComparison.Ordinal) || n.StartsWith("word/diagrams/", StringComparison.Ordinal)
                || n is "word/document.xml" or "word/styles.xml" or "word/settings.xml" or "word/webSettings.xml" or "word/fontTable.xml" or "word/numbering.xml" or "word/footnotes.xml" or "word/endnotes.xml" or "word/comments.xml" or "word/stylesWithEffects.xml" or "word/people.xml" or "word/commentsIds.xml" or "word/commentsExtensible.xml",
            ("word/vbaProject", "docx.makro", "Makros"), ("word/vbaData", "", ""), ("word/embeddings/", "docx.einbettung", "Eingebettete Dateien (z. B. die Tabelle hinter einem Diagramm)"),
            ("word/commentsExtended", "docx.kommentarantwort", "Antworten und Erledigt-Status bei Kommentaren"), ("word/activeX/", "docx.formular", "Formularfelder (Auswahllisten, Kästchen, Datumsfelder)"), ("word/fonts/", "docx.schrift", "Eingebettete Schriften"));
    }

    static bool DocxMapsStyle(string id) => id.ToLowerInvariant().Replace("(user)", "").Replace("user", "").Replace(" ", "") is "title" or "titel" or "subtitle" or "untertitel" or "heading1" or "berschrift1" or "überschrift1"
        or "heading2" or "berschrift2" or "überschrift2" or "heading3" or "berschrift3" or "überschrift3" or "quote" or "quotations" or "zitat" or "blockzitat" or "intensequote";

    /// <summary>Tabelle im Text. Seit T-0692 übernimmt der Leser je Zelle Text mit Zeichenformaten, Schrift, Farbe, Links, Zeilenumbrüchen, die Ausrichtung,
    /// verbundene Zellen (gridSpan, vMerge) und Spaltenbreiten – gemeldet wird nur noch, was weiterhin fehlt.</summary>
    static void DocxTable(XElement tbl, int no, Losses l)
    {
        var wo = "Tabelle " + no;
        foreach (var c in tbl.Elements().Where(e => e.Name.LocalName is not ("tblPr" or "tblGrid" or "tr" or "bookmarkStart" or "bookmarkEnd" or "proofErr")))
            l.Add("docx.unbekannt", "Bausteine im Text, die Trendo nicht kennt", 1, c.Name.LocalName);
        if ((string?)tbl.Element(W + "tblPr")?.Element(W + "tblStyle")?.Attribute(W + "val") is { } ts && ts != "TableGrid") l.Add("docx.tabelle.vorlage", "Tabellenvorlagen (Farben und Rahmen der Tabelle)", 1, wo);
        foreach (var tc in tbl.Elements(W + "tr").Elements(W + "tc"))
        {
            var pr = tc.Element(W + "tcPr");
            if (pr?.Element(W + "hMerge") is { } hm && (string?)hm.Attribute(W + "val") == "restart") l.Add("docx.verbunden", "Verbundene Zellen in alter Schreibweise (hMerge)", 1, wo);
            l.Add("docx.tabelle.verschachtelt", "Tabellen in Tabellenzellen", tc.Elements(W + "tbl").Count(), wo);
            var own = tc.Elements(W + "p").ToList();
            var aligns = own.Select(p => (string?)p.Element(W + "pPr")?.Element(W + "jc")?.Attribute(W + "val") ?? "left").Distinct().Count();
            var formatted = (string?)pr?.Element(W + "shd")?.Attribute(W + "fill") is { } fill && fill is not ("auto" or "FFFFFF") || aligns > 1;
            if (formatted) l.Add("docx.zelle.format", "Hintergrundfarbe von Tabellenzellen und je Absatz eigene Ausrichtung in einer Zelle", 1, wo);
            if (own.Any(p => p.Element(W + "pPr")?.Element(W + "numPr") is not null)) l.Add("docx.zelle.liste", "Aufzählungen in Tabellenzellen", 1, wo);
            if (own.Descendants().Any(e => e.Name == W + "footnoteReference" || e.Name == W + "endnoteReference")) l.Add("docx.zelle.verweis", "Fußnoten in Tabellenzellen", 1, wo);
        }
    }

    /// <summary>Felder im Text außer dem Inhaltsverzeichnis: Der Leser behält nur den zuletzt angezeigten Text.</summary>
    static void DocxFields(XElement body, Losses l)
    {
        var stack = new List<System.Text.StringBuilder>();
        static string Word(string instr) => instr.Trim().Split(' ', '\\')[0].ToUpperInvariant();
        bool InToc() => stack.Any(s => Word(s.ToString()) == "TOC");
        void Count(string instr, XElement at)
        {
            var w = Word(instr);
            if (w is "TOC" or "") return;
            if (w == "SEQ" && at.AncestorsAndSelf(W + "p").FirstOrDefault()?.Element(W + "pPr")?.Element(W + "pStyle") is not null) return; // Bildbeschriftung – wird als Beschriftung gelesen
            if (w == "HYPERLINK") l.Add("docx.feldlink", "Links, die als Feld gespeichert sind");
            else l.Add("docx.feld", "Felder (z. B. Datum, Querverweis, Seriendruckfeld – es bleibt nur der feste Text)", 1, w);
        }
        foreach (var e in body.Descendants())
        {
            if (e.Name == W + "fldChar")
            {
                var t = (string?)e.Attribute(W + "fldCharType");
                if (t == "begin") stack.Add(new());
                else if (t == "end" && stack.Count > 0) { var s = stack[^1].ToString(); stack.RemoveAt(stack.Count - 1); if (!InToc()) Count(s, e); }
            }
            else if (e.Name == W + "instrText" && stack.Count > 0) stack[^1].Append(e.Value);
            else if (e.Name == W + "fldSimple" && !InToc()) Count((string?)e.Attribute(W + "instr") ?? "", e);
        }
    }

    // ======================= ODT =======================
    static readonly XNamespace OOffice = "urn:oasis:names:tc:opendocument:xmlns:office:1.0", OText = "urn:oasis:names:tc:opendocument:xmlns:text:1.0", OStyle = "urn:oasis:names:tc:opendocument:xmlns:style:1.0",
        OFo = "urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0", OTable = "urn:oasis:names:tc:opendocument:xmlns:table:1.0", ODraw = "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0",
        OXLink = "http://www.w3.org/1999/xlink", OPres = "urn:oasis:names:tc:opendocument:xmlns:presentation:1.0", OAnim = "urn:oasis:names:tc:opendocument:xmlns:animation:1.0", OChart = "urn:oasis:names:tc:opendocument:xmlns:chart:1.0";

    static readonly HashSet<string> OdtInlineKnown = ["span", "s", "tab", "line-break", "a", "note", "annotation", "annotation-end",
        "bookmark", "bookmark-start", "bookmark-end", "soft-page-break", "reference-mark", "reference-mark-start", "reference-mark-end", "toc-mark", "toc-mark-start", "toc-mark-end", "alphabetical-index-mark", "alphabetical-index-mark-start", "alphabetical-index-mark-end"];
    static readonly HashSet<string> OdtBodyKnown = ["p", "h", "list", "table", "section", "table-of-content", "sequence-decls", "variable-decls", "user-field-decls", "soft-page-break", "forms"];

    static void Odt(ZipArchive z, Losses l)
    {
        var content = X(z, "content.xml"); var stylesDoc = X(z, "styles.xml");
        var text = content?.Root?.Element(OOffice + "body")?.Element(OOffice + "text"); if (text is null) return;
        var tableNo = 0;

        void Frame(XElement f, bool inCell)
        {
            if (f.Name != ODraw + "frame") { l.Add("odt.form", "Formen und Zeichnungen", 1, Short(f.Value) is { Length: > 0 } t ? $"„{t}“" : null); return; }
            if (f.Element(ODraw + "text-box") is { } box) l.Add("odt.textfeld", "Textfelder", 1, Short(box.Value) is { Length: > 0 } t ? $"„{t}“" : null);
            else if (f.Element(ODraw + "object") is not null || f.Element(ODraw + "object-ole") is not null) l.Add("odt.objekt", "Eingebettete Objekte (z. B. Diagramm, Formel oder Tabelle)");
            else if (f.Element(ODraw + "image") is { } img)
            {
                if (inCell) l.Add("odt.zelle.bild", "Bilder in Tabellenzellen");
                else if ((string?)img.Attribute(OXLink + "href") is not { } href || z.GetEntry(href) is null) l.Add("odt.bild", "Bilder, die Trendo nicht übernimmt (verknüpft)");
            }
            else l.Add("odt.unbekannt", "Bausteine im Text, die Trendo nicht kennt", 1, f.Elements().FirstOrDefault()?.Name.LocalName ?? "frame");
        }
        void Inline(XElement e)
        {
            foreach (var c in e.Elements())
            {
                var n = c.Name.LocalName;
                if (c.Name.Namespace == ODraw) Frame(c, false);
                else if (c.Name == OText + "span" || c.Name == OText + "a") Inline(c);
                else if (c.Name == OText + "change" || c.Name == OText + "change-start" || c.Name == OText + "change-end") { if (n != "change-end") l.Add("odt.aenderung", "Nachverfolgte Änderungen"); }
                else if (c.Name.Namespace == OText && OdtInlineKnown.Contains(n) || c.Name == OOffice + "annotation" || c.Name == OOffice + "annotation-end") { }
                else if (c.Name.Namespace == OText) l.Add("odt.feld", "Felder (z. B. Datum, Seitenzahl, Querverweis – der Text fehlt in Trendo)", 1, n);
                else l.Add("odt.unbekannt", "Bausteine im Text, die Trendo nicht kennt", 1, n);
            }
        }
        void Block(XElement el)
        {
            if (el.Name == OText + "p" || el.Name == OText + "h") Inline(el);
            else if (el.Name == OText + "list") foreach (var item in el.Elements(OText + "list-item").Concat(el.Elements(OText + "list-header"))) foreach (var c in item.Elements()) Block(c);
            else if (el.Name == OText + "section") foreach (var c in el.Elements()) Block(c);
            else if (el.Name == OTable + "table") OdtTable(el, ++tableNo, l);
            else if (el.Name == OText + "tracked-changes") { }
            else if (el.Name.Namespace == ODraw) Frame(el, false);
            else if (el.Name.Namespace == OText && el.Name.LocalName.EndsWith("-index", StringComparison.Ordinal)) l.Add("odt.verzeichnis", "Weitere Verzeichnisse (z. B. Stichwort- oder Abbildungsverzeichnis)");
            else if (el.Name == OOffice + "forms") { if (el.HasElements) l.Add("odt.formular", "Formularfelder"); }
            else if (!(el.Name.Namespace == OText && OdtBodyKnown.Contains(el.Name.LocalName))) l.Add("odt.unbekannt", "Bausteine im Text, die Trendo nicht kennt", 1, el.Name.LocalName);
        }
        foreach (var el in text.Elements()) Block(el);

        // Formate, die der Leser nicht auswertet (nur in benutzten automatischen Stilen)
        var used = text.Descendants().Select(e => (string?)e.Attribute(OText + "style-name")).OfType<string>().ToHashSet();
        foreach (var st in content!.Descendants(OStyle + "style").Where(s => used.Contains((string?)s.Attribute(OStyle + "name") ?? "")))
        {
            var pp = st.Element(OStyle + "paragraph-properties"); var tp = st.Element(OStyle + "text-properties");
            if (pp is not null && (pp.Attributes().Any(a => a.Name.Namespace == OFo && a.Name.LocalName.StartsWith("border", StringComparison.Ordinal) && a.Value != "none") || (string?)pp.Attribute(OFo + "background-color") is { } bg && bg != "transparent"))
                l.Add("odt.absatzrahmen", "Rahmen und Hintergrund von Absätzen");
            if (pp?.Element(OStyle + "tab-stops")?.HasElements == true) l.Add("odt.tabstopp", "Eigene Tabulatoren");
            if (pp?.Element(OStyle + "drop-cap") is not null) l.Add("odt.rahmen", "Initialen");
            if (tp is not null && ((string?)tp.Attribute(OFo + "font-variant") == "small-caps" || (string?)tp.Attribute(OFo + "text-transform") is not (null or "none") || (string?)tp.Attribute(OFo + "text-shadow") is not (null or "none") || (string?)tp.Attribute(OFo + "letter-spacing") is not (null or "normal")))
                l.Add("odt.zeichenformat", "Zeichenformate, die Trendo nicht kennt (z. B. Kapitälchen, Sperrung, Schatten)");
        }

        // Seitenvorlagen, Kopf- und Fußzeile
        var masters = stylesDoc?.Descendants(OStyle + "master-page").ToList() ?? [];
        if (masters.Count > 1) l.Add("odt.abschnitte", "Mehrere Seitenvorlagen (eigene Seitenformate oder Kopfzeilen)", masters.Count);
        var master = masters.FirstOrDefault(m => (string?)m.Attribute(OStyle + "name") is "Standard" or "Default") ?? masters.FirstOrDefault();
        foreach (var hf in master?.Elements() ?? [])
        {
            var n = hf.Name.LocalName; if (hf.Name.Namespace != OStyle) continue;
            var head = n.StartsWith("header", StringComparison.Ordinal); var name = head ? "Kopfzeile" : "Fußzeile"; var key = head ? "odt.kopf" : "odt.fuss";
            if (n is "header-left" or "header-first" or "footer-left" or "footer-first") { l.Add(key + ".weitere", $"Eigene {name} für die erste Seite oder für gerade Seiten"); continue; }
            if (n is not ("header" or "footer")) continue;
            l.Add(key + ".tabelle", $"Tabelle in der {name} (fehlt in Trendo)", hf.Descendants(OTable + "table").Count());
            l.Add(key + ".bild", $"Bilder in der {name} (z. B. ein Logo)", hf.Descendants(ODraw + "image").Count());
            var paras = hf.Elements(OText + "p").Where(p => head || !p.Descendants(OText + "page-number").Any()).ToList();
            if (paras.Count(p => p.Value.Trim().Length > 0) > 1) l.Add(key + ".zeilen", $"{name} mit mehreren Zeilen (wird zu einer Zeile)");
            l.Add(key + ".feld", $"Felder in der {name} (z. B. Seitenzahl oder Datum – es bleibt nur der feste Text)", paras.Sum(p => p.Descendants().Count(e => e.Name.Namespace == OText && e.Name.LocalName is "page-number" or "page-count" or "date" or "time" or "title" or "author-name" or "file-name" or "chapter")));
        }
        var plName = (string?)master?.Attribute(OStyle + "page-layout-name");
        var pl = stylesDoc?.Descendants(OStyle + "page-layout").FirstOrDefault(x => (string?)x.Attribute(OStyle + "name") == plName)?.Element(OStyle + "page-layout-properties");
        if (pl is not null && OdfCm((string?)pl.Attribute(OFo + "page-width")) is { } w && OdfCm((string?)pl.Attribute(OFo + "page-height")) is { } h && (Math.Abs(Math.Min(w, h) - 21) > 0.15 || Math.Abs(Math.Max(w, h) - 29.7) > 0.15))
            l.Add("odt.papier", "Papierformat (Trendo kennt nur A4)");

        Parts(z, l, "odt", n => n is "content.xml" or "styles.xml" or "meta.xml" or "settings.xml" or "mimetype" or "manifest.rdf" or "layout-cache" || n.StartsWith("META-INF/", StringComparison.Ordinal) || n.StartsWith("Thumbnails/", StringComparison.Ordinal)
                || n.StartsWith("Pictures/", StringComparison.Ordinal) || n.StartsWith("Configurations2/", StringComparison.Ordinal) || n.StartsWith("Object", StringComparison.Ordinal),
            ("Basic/", "odt.makro", "Makros"), ("Scripts/", "odt.makro", "Makros"), ("Dialogs/", "", ""));
    }

    static double? OdfCm(string? v)
    {
        if (string.IsNullOrEmpty(v)) return null;
        var num = new string(v.TakeWhile(ch => char.IsDigit(ch) || ch is '.' or '-').ToArray()); var unit = v[num.Length..];
        if (!double.TryParse(num, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d)) return null;
        return unit switch { "cm" => d, "mm" => d / 10, "in" => d * 2.54, "pt" => d * 2.54 / 72, _ => null };
    }

    /// <summary>ODT-Tabelle. Seit T-0692 übernimmt der Leser je Zelle die direkten Absätze mit Zeichenformaten, verbundene Zellen und Spaltenbreiten.</summary>
    static void OdtTable(XElement tbl, int no, Losses l)
    {
        var wo = "Tabelle " + no;
        foreach (var cell in tbl.Descendants(OTable + "table-cell").Where(c => c.Ancestors(OTable + "table").First() == tbl))
        {
            l.Add("odt.tabelle.verschachtelt", "Tabellen in Tabellenzellen", cell.Elements(OTable + "table").Count(), wo);
            if (cell.Elements(OText + "list").Any()) l.Add("odt.zelle.liste", "Aufzählungen in Tabellenzellen (der Text fehlt in Trendo)", 1, wo);
            if (cell.Descendants(ODraw + "image").Any()) l.Add("odt.zelle.bild", "Bilder in Tabellenzellen", 1, wo);
            if (cell.Elements(OText + "p").Descendants().Any(e => e.Name == OText + "note")) l.Add("odt.zelle.verweis", "Fußnoten in Tabellenzellen", 1, wo);
        }
    }
}
