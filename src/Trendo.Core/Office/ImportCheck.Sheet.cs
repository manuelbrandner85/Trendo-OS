using System.Globalization;
using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace Trendo.Core.Office;

/// <summary>T-0682: Importprüfung für Trendo Tabelle (XLSX, ODS). Gegenstück zu <see cref="SheetFiles.ReadXlsx"/> und <see cref="SheetFiles.ReadOds"/>.</summary>
public static partial class ImportCheck
{
    static readonly XNamespace Sn = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    static readonly XNamespace Xdr = "http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing";

    static readonly HashSet<string> XlsxSheetKnown = ["sheetData", "sheetViews", "cols", "sheetProtection", "conditionalFormatting", "dataValidations", "drawing",
        "dimension", "sheetPr", "sheetFormatPr", "pageMargins", "pageSetup", "printOptions", "phoneticPr", "legacyDrawing", "legacyDrawingHF", "sheetCalcPr", "ignoredErrors", "sortState", "customSheetViews"];

    /// <summary>Zahlenformate, die Trendo unverändert zurückschreibt: Standard, „#.##0“, „#.##0,00“, „0,00 %“, Text.</summary>
    static readonly HashSet<int> XlsxNumFmtKept = [0, 3, 4, 10, 49];
    static readonly HashSet<string> XlsxNumCodeKept = ["GENERAL", "#,##0", "#,##0.00", "0.00%", "@", "#,##0.00 €", "DD.MM.YYYY", "DD.MM.YYYY HH:MM"];

    static string NormCode(string code) => code.Replace("\\", "").Replace("\"", "").Replace("[$-407]", "").Replace("[$€-407]", "€").ToUpperInvariant().Trim();

    /// <summary>Bausteine eines Diagramms als „Eltern>Kind“: So fällt z. B. ein Achsentitel auf, obwohl es den Diagrammtitel auch bei Trendo gibt.</summary>
    static IEnumerable<string> ChartPairs(XElement root) => root.Descendants().Select(e => $"{e.Parent?.Name.LocalName}>{e.Name.LocalName}");

    /// <summary>Diagrammarten, die Trendo genauso zurückschreibt: Säulen nebeneinander, Linie, Kreis. Alles andere wird nur ähnlich.</summary>
    static bool ChartKindKept(XElement plot) => plot.Elements().Where(e => e.Name.LocalName.EndsWith("Chart", StringComparison.Ordinal)).All(e => e.Name.LocalName switch
    {
        "barChart" => (string?)e.Element(Cn + "barDir")?.Attribute("val") is null or "col" && (string?)e.Element(Cn + "grouping")?.Attribute("val") is null or "clustered",
        "lineChart" => (string?)e.Element(Cn + "grouping")?.Attribute("val") is null or "standard",
        "pieChart" => true,
        _ => false,
    });

    /// <summary>Bausteine, die Trendos eigener Diagrammschreiber verwendet – alles andere in einem fremden Diagramm sind Einzelheiten, die verloren gehen.</summary>
    static readonly Lazy<HashSet<string>> OwnSheetChartNames = new(() =>
    {
        var names = new HashSet<string>();
        var doc = new SheetDoc(); var sh = doc.Sheets[0];
        sh.Get("A1").Raw = "Monat"; sh.Get("B1").Raw = "Wert"; sh.Get("A2").Raw = "Jan"; sh.Get("B2").Raw = "1"; sh.Get("A3").Raw = "Feb"; sh.Get("B3").Raw = "2";
        var calc = new SheetCalc(doc);
        foreach (var k in Enum.GetValues<ChartKind>())
            foreach (var pair in ChartPairs(XDocument.Parse(SheetFiles.ChartXml(sh, calc, new SheetChart(k, "A1:B3", "Titel"))).Root!)) names.Add(pair);
        return names;
    });

    static void Xlsx(ZipArchive z, Losses l)
    {
        var wb = X(z, "xl/workbook.xml"); if (wb?.Root is null) return;
        var wbRels = RelsOf(z, "xl/workbook.xml");

        // Formate der Mappe: je Zellformat, was Trendo davon nicht hält
        var st = X(z, "xl/styles.xml")?.Root;
        var codes = new Dictionary<int, string>();
        foreach (var n in st?.Element(Sn + "numFmts")?.Elements(Sn + "numFmt") ?? []) if (int.TryParse((string?)n.Attribute("numFmtId"), out var id)) codes[id] = (string?)n.Attribute("formatCode") ?? "";
        var fonts = st?.Element(Sn + "fonts")?.Elements(Sn + "font").ToList() ?? [];
        var fills = st?.Element(Sn + "fills")?.Elements(Sn + "fill").ToList() ?? [];
        var borders = st?.Element(Sn + "borders")?.Elements(Sn + "border").ToList() ?? [];
        string? V(XElement? font, string n) => (string?)font?.Element(Sn + n)?.Attribute("val");
        bool FontLoss(XElement f)
        {
            bool On(string n) => f.Element(Sn + n) is { } x && Truthy((string?)x.Attribute("val") ?? "1");
            var col = f.Element(Sn + "color");
            var colored = col is not null && !((string?)col.Attribute("theme") == "1" || (string?)col.Attribute("auto") is "1" or "true" || (string?)col.Attribute("indexed") is "8" or "64" || ((string?)col.Attribute("rgb"))?.EndsWith("000000", StringComparison.Ordinal) == true);
            return On("i") || On("u") || On("strike") || colored || fonts.Count > 0 && (V(f, "sz") != V(fonts[0], "sz") || V(f, "name") != V(fonts[0], "name"));
        }
        bool FillLoss(XElement f) => f.Element(Sn + "gradientFill") is not null || f.Element(Sn + "patternFill") is { } p && (string?)p.Attribute("patternType") is { } pt && pt != "none"
            && !(pt == "solid" && (string?)p.Element(Sn + "fgColor")?.Attribute("rgb") is { Length: >= 6 });
        bool BorderLoss(XElement b)
        {
            var sides = new[] { "left", "right", "top", "bottom" }.Select(s => b.Element(Sn + s) ?? b.Element(Sn + (s == "left" ? "start" : s == "right" ? "end" : s))).Select(e => (string?)e?.Attribute("style")).ToList();
            var diag = (string?)b.Element(Sn + "diagonal")?.Attribute("style") is not null;
            if (sides.All(s => s is null) && !diag) return false;
            var colored = b.Elements().Select(e => e.Element(Sn + "color")).Any(c => c is not null && !((string?)c.Attribute("auto") is "1" or "true" || (string?)c.Attribute("indexed") == "64" || ((string?)c.Attribute("rgb"))?.EndsWith("000000", StringComparison.Ordinal) == true));
            return diag || colored || !sides.All(s => s == "thin");
        }
        var xfLoss = new List<(bool Num, string Code, bool Font, bool Fill, bool Border, bool Align)>();
        foreach (var xf in st?.Element(Sn + "cellXfs")?.Elements(Sn + "xf") ?? [])
        {
            int I(string a) => int.TryParse((string?)xf.Attribute(a), out var v) ? v : 0;
            var nf = I("numFmtId"); var code = codes.GetValueOrDefault(nf);
            var numLoss = code is not null ? !XlsxNumCodeKept.Contains(NormCode(code)) : !XlsxNumFmtKept.Contains(nf);
            var al = xf.Element(Sn + "alignment");
            var alignLoss = al is not null && ((string?)al.Attribute("horizontal") is not (null or "general") || (string?)al.Attribute("vertical") is not (null or "bottom") || Truthy((string?)al.Attribute("wrapText")) || Truthy((string?)al.Attribute("textRotation")) || Truthy((string?)al.Attribute("indent")) || Truthy((string?)al.Attribute("shrinkToFit")));
            xfLoss.Add((numLoss, code ?? "Nr. " + nf, fonts.ElementAtOrDefault(I("fontId")) is { } f && FontLoss(f), fills.ElementAtOrDefault(I("fillId")) is { } fl && FillLoss(fl), borders.ElementAtOrDefault(I("borderId")) is { } bd && BorderLoss(bd), alignLoss));
        }

        var hidden = 0;
        foreach (var sheetEl in wb.Root.Element(Sn + "sheets")?.Elements(Sn + "sheet") ?? [])
        {
            var name = (string?)sheetEl.Attribute("name") ?? "";
            if ((string?)sheetEl.Attribute("state") is "hidden" or "veryHidden") hidden++;
            if ((string?)sheetEl.Attribute(Rn + "id") is not { } rid || !wbRels.TryGetValue(rid, out var rel)) continue;
            if (!rel.Type.EndsWith("/worksheet", StringComparison.Ordinal)) { l.Add("xlsx.sonderblatt", "Diagramm- und Sonderblätter (bleiben in Trendo leer)", 1, name); continue; }
            if (z.GetEntry(rel.Target) is { } part) XlsxSheet(z, part, rel.Target, name, xfLoss, l);
        }
        l.Add("xlsx.blatt.versteckt", "Ausgeblendete Blätter (sind in Trendo sichtbar)", hidden);
        var names = wb.Root.Element(Sn + "definedNames")?.Elements(Sn + "definedName").ToList() ?? [];
        l.Add("xlsx.name", "Benannte Bereiche (Formeln mit diesen Namen rechnen nicht mehr)", names.Count(n => ((string?)n.Attribute("name"))?.StartsWith("_xlnm.", StringComparison.Ordinal) != true), string.Join(", ", names.Select(n => (string?)n.Attribute("name")).Where(n => n?.StartsWith("_xlnm.", StringComparison.Ordinal) == false).Take(3)));
        l.Add("xlsx.druck", "Druckbereiche und Wiederholungszeilen", names.Count(n => (string?)n.Attribute("name") is "_xlnm.Print_Area" or "_xlnm.Print_Titles"));
        if (wb.Root.Element(Sn + "workbookProtection") is not null) l.Add("xlsx.mappenschutz", "Schutz der Mappe");

        // Unterschiedliche Formate innerhalb einer Zelle (Text mit mehreren Schriften)
        if (z.GetEntry("xl/sharedStrings.xml") is { } ss)
        {
            var rich = 0;
            ss.ReadXml(x => { while (x.Read()) if (x.NodeType == XmlNodeType.Element && x.LocalName == "rPr" && x.Depth == 3) rich++; });
            l.Add("xlsx.zelle.mischformat", "Unterschiedliche Formate innerhalb einer Zelle", rich > 0 ? 1 : 0);
        }

        Parts(z, l, "xlsx", n => n.StartsWith("docProps/", StringComparison.Ordinal) || n.StartsWith("customXml/", StringComparison.Ordinal) || n.StartsWith("xl/theme/", StringComparison.Ordinal) || n.StartsWith("xl/worksheets/", StringComparison.Ordinal)
                || n.StartsWith("xl/drawings/", StringComparison.Ordinal) || n.StartsWith("xl/charts/", StringComparison.Ordinal) || n.StartsWith("xl/media/", StringComparison.Ordinal) || n.StartsWith("xl/printerSettings/", StringComparison.Ordinal)
                || n.StartsWith("xl/comments", StringComparison.Ordinal) || n.StartsWith("xl/tables/", StringComparison.Ordinal) || n.StartsWith("xl/chartsheets/", StringComparison.Ordinal) || n.StartsWith("xl/persons/", StringComparison.Ordinal)
                || n is "xl/workbook.xml" or "xl/styles.xml" or "xl/sharedStrings.xml" or "xl/calcChain.xml" or "xl/metadata.xml",
            ("xl/vbaProject", "xlsx.makro", "Makros"), ("xl/pivotTables/", "xlsx.pivot", "Pivot-Tabellen"), ("xl/pivotCache/", "", ""), ("xl/embeddings/", "xlsx.einbettung", "Eingebettete Dateien und Objekte"),
            ("xl/externalLinks/", "xlsx.extern", "Verknüpfungen zu anderen Dateien"), ("xl/slicer", "xlsx.datenschnitt", "Datenschnitte und Zeitachsen"), ("xl/timeline", "xlsx.datenschnitt", "Datenschnitte und Zeitachsen"),
            ("xl/threadedComments/", "xlsx.kommentarantwort", "Unterhaltungen in Kommentaren (es bleibt die Notiz)"), ("xl/queryTables/", "xlsx.verbindung", "Datenverbindungen und Abfragen"), ("xl/connections", "xlsx.verbindung", "Datenverbindungen und Abfragen"),
            ("xl/ctrlProps/", "xlsx.formular", "Schaltflächen und Formularelemente"), ("xl/activeX/", "xlsx.formular", "Schaltflächen und Formularelemente"), ("xl/macrosheets/", "xlsx.makro", "Makros"), ("xl/dialogsheets/", "xlsx.makro", "Makros"));
    }

    static void XlsxSheet(ZipArchive z, ZipArchiveEntry part, string path, string name, List<(bool Num, string Code, bool Font, bool Fill, bool Border, bool Align)> xfLoss, Losses l)
    {
        var used = new Dictionary<int, int>(); int shared = 0, array = 0, rowHeights = 0, groups = 0;
        XElement? ws = null;
        part.ReadXml(x =>
        {
            while (x.NodeType != XmlNodeType.Element && x.Read()) { }
            var root = ws = new XElement(XName.Get(x.LocalName, x.NamespaceURI));
            var limited = new OfficeZip.ShallowReader(x, OfficeZip.NodesOf(part.Archive));
            if (x.IsEmptyElement) return;
            var depth = x.Depth; x.Read();
            while (x.Depth > depth)
            {
                if (x.NodeType != XmlNodeType.Element) { x.Read(); continue; }
                if (!(x.LocalName == "sheetData" && x.NamespaceURI == Sn.NamespaceName)) { root.Add(XNode.ReadFrom(limited)); continue; }
                root.Add(new XElement(Sn + "sheetData"));
                if (x.IsEmptyElement) { x.Read(); continue; }
                var d2 = x.Depth; x.Read();
                while (x.Depth > d2)
                {
                    if (x.NodeType == XmlNodeType.Element && x.NamespaceURI == Sn.NamespaceName)
                    {
                        if (x.LocalName == "row")
                        {
                            if (x.GetAttribute("customHeight") is "1" or "true") rowHeights++;
                            if (Truthy(x.GetAttribute("outlineLevel"))) groups++;
                        }
                        else if (x.LocalName == "c") { if (int.TryParse(x.GetAttribute("s"), out var si)) used[si] = used.GetValueOrDefault(si) + 1; }
                        else if (x.LocalName == "f")
                        {
                            var t = x.GetAttribute("t");
                            if (t == "shared" && x.GetAttribute("ref") is null) shared++; // Folgezelle einer gemeinsamen Formel: Trendo liest dort nur den Wert
                            else if (t is "array" or "dataTable") array++;
                        }
                    }
                    x.Read();
                }
                x.Read();
            }
        });
        if (ws is null) return;

        void Cells(string id, string was, Func<(bool Num, string Code, bool Font, bool Fill, bool Border, bool Align), bool> pick, bool code = false)
        {
            var hit = used.Where(kv => kv.Key < xfLoss.Count && pick(xfLoss[kv.Key])).ToList();
            l.Add(id, was, hit.Sum(kv => kv.Value), name + (code && hit.Count > 0 ? ": " + string.Join(", ", hit.Select(kv => xfLoss[kv.Key].Code).Distinct().Take(3)) : ""));
        }
        Cells("xlsx.zahlenformat", "Zahlenformate, die Trendo nicht genau kennt (die Zahl bleibt, die Anzeige ändert sich)", s => s.Num, true);
        Cells("xlsx.schrift", "Schrift in Zellen (kursiv, unterstrichen, Farbe, Größe, Schriftart)", s => s.Font);
        Cells("xlsx.fuellung", "Füllmuster und Farben aus dem Design", s => s.Fill);
        Cells("xlsx.rahmen", "Rahmenarten (Trendo kennt nur einen dünnen Rahmen rundum)", s => s.Border);
        Cells("xlsx.ausrichtung", "Ausrichtung und Zeilenumbruch in Zellen", s => s.Align);
        l.Add("xlsx.formel.gemeinsam", "Formeln, die Trendo nur als festen Wert übernimmt (gemeinsame Formeln)", shared, name);
        l.Add("xlsx.formel.matrix", "Matrixformeln und Datentabellen", array, name);
        l.Add("xlsx.zeilenhoehe", "Eigene Zeilenhöhen", rowHeights, name);

        foreach (var el in ws.Elements())
        {
            var n = el.Name.LocalName;
            if (el.Name.Namespace != Sn) { l.Add("xlsx.unbekannt", "Angaben im Blatt, die Trendo nicht kennt", 1, $"{name}: {n}"); continue; }
            switch (n)
            {
                case "mergeCells": l.Add("xlsx.verbunden", "Verbundene Zellen", el.Elements(Sn + "mergeCell").Count(), name + ": " + string.Join(", ", el.Elements(Sn + "mergeCell").Take(3).Select(m => (string?)m.Attribute("ref")))); break;
                case "autoFilter": l.Add("xlsx.filter", "Filtereinstellungen", el.Elements(Sn + "filterColumn").Count(), name); break;
                case "hyperlinks": l.Add("xlsx.link", "Links in Zellen", el.Elements(Sn + "hyperlink").Count(), name); break;
                case "tableParts": l.Add("xlsx.tabellenbereich", "Als Tabelle formatierte Bereiche", el.Elements(Sn + "tablePart").Count(), name); break;
                case "headerFooter": l.Add("xlsx.druckkopf", "Kopf- und Fußzeilen für den Druck", el.Elements().Any(e => e.Value.Trim().Length > 0) ? 1 : 0, name); break;
                case "rowBreaks" or "colBreaks": l.Add("xlsx.umbruch", "Seitenumbrüche", el.Elements(Sn + "brk").Count(), name); break;
                case "oleObjects": l.Add("xlsx.einbettung", "Eingebettete Dateien und Objekte", 1, name); break;
                case "controls": l.Add("xlsx.formular", "Schaltflächen und Formularelemente", 1, name); break;
                case "picture": l.Add("xlsx.hintergrund", "Hintergrundbild des Blatts", 1, name); break;
                case "extLst":
                    foreach (var x in el.Descendants().Where(e => e.Name.LocalName is "sparklineGroup" or "cfRule" or "dataValidation" or "slicer"))
                        l.Add(x.Name.LocalName switch { "sparklineGroup" => "xlsx.sparkline", "cfRule" => "xlsx.bedingt", "dataValidation" => "xlsx.eingaberegel", _ => "xlsx.datenschnitt" },
                            x.Name.LocalName switch { "sparklineGroup" => "Sparklines (kleine Diagramme in Zellen)", "cfRule" => "Bedingte Formate, die Trendo nicht kennt (z. B. Symbole, Formeln, Rangfolgen)", "dataValidation" => "Eingaberegeln außer festen Auswahllisten (z. B. Liste aus Zellen, Zahlengrenzen)", _ => "Datenschnitte und Zeitachsen" }, 1, name);
                    break;
                case "cols":
                    l.Add("xlsx.spalte.versteckt", "Ausgeblendete Spalten (sind in Trendo sichtbar)", el.Elements(Sn + "col").Count(c => Truthy((string?)c.Attribute("hidden"))), name);
                    groups += el.Elements(Sn + "col").Count(c => Truthy((string?)c.Attribute("outlineLevel")));
                    break;
                case "conditionalFormatting":
                    if (((string?)el.Attribute("sqref") ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > 1) l.Add("xlsx.bedingt.bereich", "Bedingte Formate für mehrere Bereiche (Trendo behält den ersten)", 1, name);
                    foreach (var rule in el.Elements(Sn + "cfRule"))
                    {
                        var type = (string?)rule.Attribute("type");
                        var numeric = double.TryParse(rule.Element(Sn + "formula")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
                        var ok = type is "colorScale" or "dataBar" or "containsText" || type == "cellIs" && numeric && (string?)rule.Attribute("operator") is "greaterThan" or "lessThan" or "between" or "equal";
                        if (!ok) l.Add("xlsx.bedingt", "Bedingte Formate, die Trendo nicht kennt (z. B. Symbole, Formeln, Rangfolgen)", 1, name);
                    }
                    break;
                case "dataValidations":
                    l.Add("xlsx.eingaberegel", "Eingaberegeln außer festen Auswahllisten (z. B. Liste aus Zellen, Zahlengrenzen)", el.Elements(Sn + "dataValidation").Count(dv => !((string?)dv.Attribute("type") == "list" && dv.Element(Sn + "formula1")?.Value is { Length: >= 2 } f1 && f1[0] == '"')), name);
                    break;
                case "drawing": XlsxDrawing(z, path, el, name, l); break;
                default: if (!XlsxSheetKnown.Contains(n)) l.Add("xlsx.unbekannt", "Angaben im Blatt, die Trendo nicht kennt", 1, $"{name}: {n}"); break;
            }
        }
        l.Add("xlsx.gliederung", "Gruppierte Zeilen und Spalten (Gliederung)", groups, name);
    }

    static void XlsxDrawing(ZipArchive z, string sheetPath, XElement drawingEl, string name, Losses l)
    {
        var rels = RelsOf(z, sheetPath);
        if ((string?)drawingEl.Attribute(Rn + "id") is not { } did || !rels.TryGetValue(did, out var dr) || X(z, dr.Target)?.Root is not { } drawing) return;
        var drRels = RelsOf(z, dr.Target);
        foreach (var anchor in drawing.Elements())
            foreach (var o in anchor.Elements().Where(e => e.Name.Namespace == Xdr || e.Name == Mc + "AlternateContent"))
                switch (o.Name.LocalName)
                {
                    case "pic": l.Add("xlsx.bild", "Bilder", 1, name); break;
                    case "sp" or "cxnSp" or "grpSp": l.Add("xlsx.form", "Formen und Textfelder", 1, name + (Short(string.Concat(o.Descendants(An + "t").Select(t => t.Value))) is { Length: > 0 } t ? $": „{t}“" : "")); break;
                    case "graphicFrame":
                        if ((string?)o.Descendants(Cn + "chart").FirstOrDefault()?.Attribute(Rn + "id") is { } cid && drRels.TryGetValue(cid, out var cr) && X(z, cr.Target)?.Root is { } chart) XlsxChart(chart, name, l);
                        else l.Add("xlsx.grafik", "Grafiken, die Trendo nicht kennt (z. B. SmartArt)", 1, name);
                        break;
                    case "AlternateContent": l.Add("xlsx.grafik", "Grafiken, die Trendo nicht kennt (z. B. SmartArt)", 1, name); break;
                }
    }

    static void XlsxChart(XElement chart, string sheet, Losses l)
    {
        var title = Short(string.Concat(chart.Element(Cn + "chart")?.Element(Cn + "title")?.Descendants(An + "t").Select(t => t.Value) ?? []));
        var wo = sheet + (title.Length > 0 ? $": „{title}“" : "");
        var plot = chart.Descendants(Cn + "plotArea").FirstOrDefault();
        if (plot is null || !plot.Descendants(Cn + "f").Any()) { l.Add("xlsx.diagramm.fehlt", "Diagramme, die Trendo nicht lesen kann", 1, wo); return; }
        var types = plot.Elements().Count(e => e.Name.LocalName.EndsWith("Chart", StringComparison.Ordinal));
        var own = OwnSheetChartNames.Value;
        if (!ChartKindKept(plot)) l.Add("xlsx.diagramm.art", "Diagrammarten, die Trendo nur ähnlich kennt (z. B. gestapelt, liegend, Fläche, Punkte)", 1, wo);
        if (types > 1 || ChartPairs(chart).Any(p => !own.Contains(p))) l.Add("xlsx.diagramm", "Diagramm-Einzelheiten (Trendo behält Art, Datenbereich und Titel – nicht z. B. Achsentitel, Farben, Legende)", 1, wo);
    }

    // ======================= ODS =======================
    static void Ods(ZipArchive z, Losses l)
    {
        var content = X(z, "content.xml"); if (content?.Root is null) return;
        var styles = new Dictionary<string, XElement>();
        foreach (var s in content.Descendants(OStyle + "style")) styles.TryAdd((string?)s.Attribute(OStyle + "name") ?? "", s);
        bool Formatted(string? style)
        {
            if (style is null || !styles.TryGetValue(style, out var s)) return false;
            return s.Element(OStyle + "text-properties")?.HasAttributes == true || s.Element(OStyle + "table-cell-properties")?.Attributes().Any(a => a.Name.LocalName is "background-color" or "border" or "border-left" or "border-top" or "border-bottom" or "border-right" or "wrap-option" or "rotation-angle" && a.Value is not ("none" or "transparent" or "no-wrap"))
                == true || (string?)s.Element(OStyle + "paragraph-properties")?.Attribute(OFo + "text-align") is not (null or "start");
        }
        bool NumberStyle(string? style) => style is not null && styles.TryGetValue(style, out var s) && (string?)s.Attribute(OStyle + "data-style-name") is { Length: > 0 };
        foreach (var t in content.Descendants(OTable + "table").Where(t => !t.Ancestors(OTable + "table").Any()))
        {
            var name = (string?)t.Attribute(OTable + "name") ?? "";
            int merged = 0, fmt = 0, num = 0, notes = 0, links = 0;
            foreach (var c in t.Descendants(OTable + "table-cell"))
            {
                if (int.TryParse((string?)c.Attribute(OTable + "number-columns-spanned"), out var cs) && cs > 1 || int.TryParse((string?)c.Attribute(OTable + "number-rows-spanned"), out var rs) && rs > 1) merged++;
                var filled = c.Attribute(OOffice + "value-type") is not null || c.Attribute(OTable + "formula") is not null;
                var sn = (string?)c.Attribute(OTable + "style-name");
                if (filled && Formatted(sn)) fmt++;
                if (filled && NumberStyle(sn) && (string?)c.Attribute(OOffice + "value-type") is "float") num++;
                if (c.Element(OOffice + "annotation") is not null) notes++;
                if (c.Descendants(OText + "a").Any()) links++;
            }
            l.Add("ods.verbunden", "Verbundene Zellen", merged, name);
            l.Add("ods.format", "Aussehen der Zellen (fett, Farben, Rahmen, Ausrichtung – Trendo liest aus ODS nur die Inhalte)", fmt, name);
            l.Add("ods.zahlenformat", "Zahlenformate (z. B. Nachkommastellen – die Zahl bleibt, die Anzeige ändert sich)", num, name);
            l.Add("ods.notiz", "Notizen an Zellen", notes, name);
            l.Add("ods.link", "Links in Zellen", links, name);
            l.Add("ods.objekt", "Diagramme und eingebettete Objekte", t.Descendants(ODraw + "object").Count(), name);
            l.Add("ods.bild", "Bilder", t.Descendants(ODraw + "image").Count(i => i.Parent?.Element(ODraw + "object") is null), name);
            l.Add("ods.form", "Formen und Textfelder", t.Descendants().Count(e => e.Name.Namespace == ODraw && e.Name.LocalName is "custom-shape" or "rect" or "ellipse" or "line" or "connector" or "text-box" or "path" or "polygon"), name);
            l.Add("ods.versteckt", "Ausgeblendete Zeilen und Spalten", t.Descendants().Count(e => (e.Name == OTable + "table-row" || e.Name == OTable + "table-column") && (string?)e.Attribute(OTable + "visibility") is "collapse" or "filter"), name);
            if ((string?)t.Attribute(OTable + "protected") == "true") l.Add("ods.schutz", "Blattschutz", 1, name);
        }
        l.Add("ods.eingaberegel", "Eingaberegeln und Auswahllisten", content.Descendants(OTable + "content-validation").Count());
        l.Add("ods.name", "Benannte Bereiche", content.Descendants(OTable + "named-range").Count() + content.Descendants(OTable + "named-expression").Count());
        l.Add("ods.filter", "Filter- und Datenbereiche", content.Descendants(OTable + "database-range").Count());
        l.Add("ods.bedingt", "Bedingte Formate", content.Descendants().Count(e => e.Name.LocalName == "conditional-format") + content.Descendants(OStyle + "map").Count());
        l.Add("ods.pivot", "Pivot-Tabellen", content.Descendants(OTable + "data-pilot-table").Count());
        Parts(z, l, "ods", n => n is "content.xml" or "styles.xml" or "meta.xml" or "settings.xml" or "mimetype" or "manifest.rdf" || n.StartsWith("META-INF/", StringComparison.Ordinal) || n.StartsWith("Thumbnails/", StringComparison.Ordinal)
                || n.StartsWith("Pictures/", StringComparison.Ordinal) || n.StartsWith("Configurations2/", StringComparison.Ordinal) || n.StartsWith("Object", StringComparison.Ordinal),
            ("Basic/", "ods.makro", "Makros"), ("Scripts/", "ods.makro", "Makros"), ("Dialogs/", "", ""));
    }
}
