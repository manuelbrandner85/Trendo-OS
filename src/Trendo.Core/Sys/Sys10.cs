using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;

namespace Trendo.Core.Sys;

/// <summary>SYS-10 (Katalog freigegeben 21.10.2026): Office & Dokumente. Reine Dateiarbeit in C#; optionale Werkzeuge (qpdf, Poppler, Tesseract, LibreOffice, SumatraPDF) nur wenn vorhanden.</summary>
public static class Ooxml
{
    static void Add(ZipArchive z, string n, string c) { using var w = new StreamWriter(z.CreateEntry(n).Open(), new UTF8Encoding(false)); w.Write(c); }
    const string Rels = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"{0}\"/></Relationships>";

    /// <summary>P02: Einfaches Word-Dokument (Titel + Absätze).</summary>
    public static void NewDocx(string path, string title, IEnumerable<string> paragraphs)
    {
        var body = new StringBuilder();
        body.Append($"<w:p><w:pPr><w:pStyle w:val=\"Title\"/></w:pPr><w:r><w:rPr><w:b/><w:sz w:val=\"36\"/></w:rPr><w:t xml:space=\"preserve\">{SecurityElement.Escape(title)}</w:t></w:r></w:p>");
        foreach (var p in paragraphs) body.Append($"<w:p><w:r><w:t xml:space=\"preserve\">{SecurityElement.Escape(p)}</w:t></w:r></w:p>");
        WriteDocxXml(path, body.ToString());
    }
    public static void WriteDocxXml(string path, string bodyXml)
    {
        var doc = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\" xmlns:wp=\"http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing\" xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" xmlns:pic=\"http://schemas.openxmlformats.org/drawingml/2006/picture\"><w:body>"
            + bodyXml + "<w:sectPr><w:pgSz w:w=\"11906\" w:h=\"16838\"/><w:pgMar w:top=\"1417\" w:right=\"1417\" w:bottom=\"1134\" w:left=\"1417\" w:header=\"708\" w:footer=\"708\" w:gutter=\"0\"/></w:sectPr></w:body></w:document>";
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var z = ZipFile.Open(path + ".tmp", ZipArchiveMode.Create);
        Add(z, "[Content_Types].xml", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Default Extension=\"png\" ContentType=\"image/png\"/><Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/></Types>");
        Add(z, "_rels/.rels", string.Format(Rels, "word/document.xml"));
        Add(z, "word/document.xml", doc);
        z.Dispose(); File.Move(path + ".tmp", path, true);
    }

    /// <summary>P02: Einfache Tabelle (erste Zeile = Überschriften).</summary>
    public static void NewXlsx(string path, IList<IList<string>> rows)
    {
        static string Col(int i) { var s = ""; for (i++; i > 0; i = (i - 1) / 26) s = (char)('A' + (i - 1) % 26) + s; return s; }
        var sb = new StringBuilder();
        for (var r = 0; r < rows.Count; r++)
        {
            sb.Append($"<row r=\"{r + 1}\">");
            for (var c = 0; c < rows[r].Count; c++)
            {
                var v = rows[r][c]; var cell = $"{Col(c)}{r + 1}";
                if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out _) && v.Length > 0) sb.Append($"<c r=\"{cell}\"><v>{v}</v></c>");
                else sb.Append($"<c r=\"{cell}\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{SecurityElement.Escape(v)}</t></is></c>");
            }
            sb.Append("</row>");
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using (var z = ZipFile.Open(path + ".tmp", ZipArchiveMode.Create))
        {
            Add(z, "[Content_Types].xml", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/></Types>");
            Add(z, "_rels/.rels", string.Format(Rels, "xl/workbook.xml"));
            Add(z, "xl/workbook.xml", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"Tabelle1\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
            Add(z, "xl/_rels/workbook.xml.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/></Relationships>");
            Add(z, "xl/worksheets/sheet1.xml", $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>{sb}</sheetData></worksheet>");
        }
        File.Move(path + ".tmp", path, true);
    }

    static string Entry(string file, string name) { using var z = ZipFile.OpenRead(file); var e = z.GetEntry(name); if (e is null) return ""; using var r = new StreamReader(e.Open()); return r.ReadToEnd(); }
    static string XmlText(string xml, string paraEnd) => System.Net.WebUtility.HtmlDecode(Regex.Replace(Regex.Replace(Regex.Replace(xml, paraEnd, "\n"), @"<w:tab/>|<text:tab/>", "\t"), "<[^>]+>", "")).Trim();
    public static string DocxText(string f) => XmlText(Entry(f, "word/document.xml"), "</w:p>");
    public static string OdtText(string f) => XmlText(Entry(f, "content.xml"), "</text:p>|</text:h>");

    /// <summary>P11: Tabelle lesen ohne Office (erste Tabelle, höchstens „max“ Zeilen).</summary>
    public static List<List<string>> ReadXlsx(string f, int max = 1000)
    {
        var shared = Regex.Matches(Entry(f, "xl/sharedStrings.xml"), @"<si>(.*?)</si>", RegexOptions.Singleline).Select(m => System.Net.WebUtility.HtmlDecode(Regex.Replace(m.Groups[1].Value, "<[^>]+>", ""))).ToList();
        var sheet = Entry(f, "xl/worksheets/sheet1.xml"); var res = new List<List<string>>();
        foreach (Match row in Regex.Matches(sheet, @"<row\b[^>]*>(.*?)</row>", RegexOptions.Singleline))
        {
            var cells = new List<string>();
            foreach (Match c in Regex.Matches(row.Groups[1].Value, @"<c\b([^>]*?)(?:/>|>(.*?)</c>)", RegexOptions.Singleline))
            {
                var attrs = c.Groups[1].Value; var inner = c.Groups[2].Value;
                var col = Regex.Match(attrs, @"r=""([A-Z]+)\d+""").Groups[1].Value; var idx = col.Aggregate(0, (a, ch) => a * 26 + ch - 'A' + 1) - 1;
                while (cells.Count < idx) cells.Add("");
                var v = Regex.Match(inner, @"<v>(.*?)</v>").Groups[1].Value;
                var t = Regex.Match(attrs, @"t=""(\w+)""").Groups[1].Value;
                cells.Add(t == "s" && int.TryParse(v, out var si) && si < shared.Count ? shared[si] : t == "inlineStr" ? System.Net.WebUtility.HtmlDecode(Regex.Replace(inner, "<[^>]+>", "")) : System.Net.WebUtility.HtmlDecode(v));
            }
            res.Add(cells); if (res.Count >= max) break;
        }
        return res;
    }

    /// <summary>P16: Autor/Firma/Bearbeiter aus DOCX/XLSX/PPTX entfernen – in eine Kopie.</summary>
    public static string StripOfficeMeta(string file)
    {
        var o = Path.Combine(Path.GetDirectoryName(file)!, Path.GetFileNameWithoutExtension(file) + "-ohne-metadaten" + Path.GetExtension(file));
        File.Copy(file, o, true);
        using var z = ZipFile.Open(o, ZipArchiveMode.Update);
        foreach (var (name, tags) in new[] { ("docProps/core.xml", new[] { "dc:creator", "cp:lastModifiedBy", "dc:title", "dc:subject", "cp:keywords", "dc:description", "cp:lastPrinted" }), ("docProps/app.xml", new[] { "Company", "Manager", "Template" }), ("docProps/custom.xml", Array.Empty<string>()) })
        {
            var e = z.GetEntry(name); if (e is null) continue;
            string x; using (var r = new StreamReader(e.Open())) x = r.ReadToEnd();
            if (name.EndsWith("custom.xml")) x = Regex.Replace(x, @"<property\b.*?</property>", "", RegexOptions.Singleline);
            foreach (var t in tags) x = Regex.Replace(x, $@"<{t}\b([^>]*)>.*?</{t}>", $"<{t}$1></{t}>", RegexOptions.Singleline);
            e.Delete(); using var w = new StreamWriter(z.CreateEntry(name).Open(), new UTF8Encoding(false)); w.Write(x);
        }
        return o;
    }
    public static Dictionary<string, string> OfficeMeta(string file)
    {
        var x = Entry(file, "docProps/core.xml") + Entry(file, "docProps/app.xml");
        return Regex.Matches(x, @"<((?:dc|cp):\w+|Company|Manager)\b[^>]*>([^<]+)</\1>").ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value);
    }

    /// <summary>P10: Serienbrief – Platzhalter {{Feld}} (auch über Formatierungsgrenzen hinweg) je CSV-Zeile ersetzen.</summary>
    public static List<string> MailMerge(string templateDocx, List<Dictionary<string, string>> rows, string outDir, string nameField = "")
    {
        string xml; using (var z = ZipFile.OpenRead(templateDocx)) using (var r = new StreamReader(z.GetEntry("word/document.xml")!.Open())) xml = r.ReadToEnd();
        xml = Regex.Replace(xml, @"\{(?:<[^>]+>)*\{((?:[^}<]|<[^>]+>)*?)\}(?:<[^>]+>)*\}", m => "{{" + Regex.Replace(m.Groups[1].Value, "<[^>]+>", "").Trim() + "}}");
        Directory.CreateDirectory(outDir); var res = new List<string>(); var i = 0;
        foreach (var row in rows)
        {
            i++;
            var filled = Regex.Replace(xml, @"\{\{([^}]+)\}\}", m => row.TryGetValue(m.Groups[1].Value.Trim(), out var v) ? SecurityElement.Escape(v) : m.Value);
            var stem = nameField.Length > 0 && row.TryGetValue(nameField, out var nm) && nm.Trim().Length > 0 ? string.Concat(nm.Where(c => !Path.GetInvalidFileNameChars().Contains(c))) : $"{i:000}";
            var o = Path.Combine(outDir, $"{Path.GetFileNameWithoutExtension(templateDocx)}-{i:000}-{stem}.docx");
            File.Copy(templateDocx, o, true);
            using (var z = ZipFile.Open(o, ZipArchiveMode.Update)) { z.GetEntry("word/document.xml")!.Delete(); using var w = new StreamWriter(z.CreateEntry("word/document.xml").Open(), new UTF8Encoding(false)); w.Write(filled); }
            res.Add(o);
        }
        return res;
    }
    public static List<string> Placeholders(string docx) => Regex.Matches(Regex.Replace(Entry(docx, "word/document.xml"), "<[^>]+>", ""), @"\{\{([^}]+)\}\}").Select(m => m.Groups[1].Value.Trim()).Distinct().ToList();

    /// <summary>P22: Unterschrift als Bild ans Ende einfügen (Kopie). Ausdrücklich keine qualifizierte elektronische Signatur.</summary>
    public static string InsertSignature(string docx, byte[] png, int widthPx, int heightPx, string caption)
    {
        var o = Path.Combine(Path.GetDirectoryName(docx)!, Path.GetFileNameWithoutExtension(docx) + "-unterschrieben.docx"); File.Copy(docx, o, true);
        long cx = widthPx * 9525L, cy = heightPx * 9525L; const string rid = "rIdTrendoSig";
        var drawing = $"<w:p><w:r><w:drawing><wp:inline><wp:extent cx=\"{cx}\" cy=\"{cy}\"/><wp:docPr id=\"9001\" name=\"Unterschrift\" descr=\"Unterschrift (Bild)\"/><a:graphic><a:graphicData uri=\"http://schemas.openxmlformats.org/drawingml/2006/picture\"><pic:pic><pic:nvPicPr><pic:cNvPr id=\"9001\" name=\"unterschrift.png\"/><pic:cNvPicPr/></pic:nvPicPr><pic:blipFill><a:blip r:embed=\"{rid}\"/><a:stretch><a:fillRect/></a:stretch></pic:blipFill><pic:spPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"{cx}\" cy=\"{cy}\"/></a:xfrm><a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom></pic:spPr></pic:pic></a:graphicData></a:graphic></wp:inline></w:drawing></w:r></w:p>"
            + $"<w:p><w:r><w:rPr><w:sz w:val=\"16\"/></w:rPr><w:t xml:space=\"preserve\">{SecurityElement.Escape(caption)}</w:t></w:r></w:p>";
        using var z = ZipFile.Open(o, ZipArchiveMode.Update);
        string Read(string n) { var e = z.GetEntry(n); if (e is null) return ""; using var r = new StreamReader(e.Open()); return r.ReadToEnd(); }
        void Write(string n, string c) { z.GetEntry(n)?.Delete(); using var w = new StreamWriter(z.CreateEntry(n).Open(), new UTF8Encoding(false)); w.Write(c); }
        var doc = Read("word/document.xml");
        foreach (var (p, ns) in new[] { ("r", "http://schemas.openxmlformats.org/officeDocument/2006/relationships"), ("wp", "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing"), ("a", "http://schemas.openxmlformats.org/drawingml/2006/main"), ("pic", "http://schemas.openxmlformats.org/drawingml/2006/picture") })
            if (!doc.Contains($"xmlns:{p}=")) doc = Regex.Replace(doc, @"<w:document\b", $"<w:document xmlns:{p}=\"{ns}\"", RegexOptions.None, TimeSpan.FromSeconds(1));
        var sect = doc.LastIndexOf("<w:sectPr", StringComparison.Ordinal); var end = doc.LastIndexOf("</w:body>", StringComparison.Ordinal);
        doc = doc.Insert(sect > 0 ? sect : end, drawing); Write("word/document.xml", doc);
        var rels = Read("word/_rels/document.xml.rels"); if (rels.Length == 0) rels = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"></Relationships>";
        Write("word/_rels/document.xml.rels", rels.Replace("</Relationships>", $"<Relationship Id=\"{rid}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/image\" Target=\"media/trendo-unterschrift.png\"/></Relationships>"));
        var ct = Read("[Content_Types].xml"); if (!Regex.IsMatch(ct, @"Extension=""png""", RegexOptions.IgnoreCase)) Write("[Content_Types].xml", ct.Replace("</Types>", "<Default Extension=\"png\" ContentType=\"image/png\"/></Types>"));
        z.GetEntry("word/media/trendo-unterschrift.png")?.Delete(); using (var s = z.CreateEntry("word/media/trendo-unterschrift.png").Open()) s.Write(png);
        return o;
    }
}

public static class Csv
{
    /// <summary>RFC 4180 mit automatischer Trennzeichen-Erkennung (; , Tab).</summary>
    public static List<List<string>> Parse(string text, int max = int.MaxValue)
    {
        var firstLine = text.Split('\n')[0]; var sep = new[] { ';', ',', '\t' }.OrderByDescending(c => firstLine.Count(x => x == c)).First();
        var rows = new List<List<string>>(); var row = new List<string>(); var cell = new StringBuilder(); var q = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (q) { if (c == '"') { if (i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; } else q = false; } else cell.Append(c); }
            else if (c == '"') q = true;
            else if (c == sep) { row.Add(cell.ToString()); cell.Clear(); }
            else if (c == '\n' || c == '\r') { if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++; row.Add(cell.ToString()); cell.Clear(); rows.Add(row); row = []; if (rows.Count >= max) return rows; }
            else cell.Append(c);
        }
        if (cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString()); rows.Add(row); }
        return rows;
    }
    public static List<Dictionary<string, string>> Records(string text)
    {
        var r = Parse(text.TrimStart('\uFEFF')); if (r.Count == 0) return [];
        var h = r[0].Select(x => x.Trim()).ToList();
        return r.Skip(1).Where(x => x.Any(c => c.Length > 0)).Select(x => h.Select((k, i) => (k, v: i < x.Count ? x[i] : "")).ToDictionary(p => p.k, p => p.v)).ToList();
    }
}

public static class Pdf
{
    /// <summary>Seitenzahl ohne Zusatzprogramm (klassische und komprimierte Seitenbäume über /Count im Wurzelknoten).</summary>
    public static int Pages(string file)
    {
        var s = Encoding.Latin1.GetString(File.ReadAllBytes(file));
        var counts = Regex.Matches(s, @"/Type\s*/Pages\b[^>]*?/Count\s+(\d+)|/Count\s+(\d+)[^>]*?/Type\s*/Pages\b", RegexOptions.Singleline).Select(m => int.Parse(m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value)).ToList();
        return counts.Count > 0 ? counts.Max() : Regex.Matches(s, @"/Type\s*/Page(?!s)").Count;
    }
    /// <summary>Seitengröße aus der ersten /MediaBox (Punkte).</summary>
    public static (double W, double H)? MediaBox(string file)
    {
        var m = Regex.Match(Encoding.Latin1.GetString(File.ReadAllBytes(file)), @"/MediaBox\s*\[\s*([-\d.]+)\s+([-\d.]+)\s+([-\d.]+)\s+([-\d.]+)\s*\]");
        if (!m.Success) return null;
        double D(int i) => double.Parse(m.Groups[i].Value, CultureInfo.InvariantCulture);
        return (Math.Round(D(3) - D(1), 1), Math.Round(D(4) - D(2), 1));
    }
    public static bool IsA4((double W, double H)? b) => b is { } x && Math.Abs(Math.Min(x.W, x.H) - 595.3) < 3 && Math.Abs(Math.Max(x.W, x.H) - 841.9) < 3;

    public static (string Exe, List<string> Args) MergeCmd(string? qpdf, string? pdfunite, IList<string> inputs, string output) =>
        qpdf is not null ? (qpdf, ["--empty", "--pages", .. inputs, "--", output]) : pdfunite is not null ? (pdfunite, [.. inputs, output]) : throw new InvalidOperationException("Zum Zusammenfügen fehlt das kostenlose Programm qpdf (winget install qpdf.qpdf).");
    public static (string Exe, List<string> Args) ExtractCmd(string? qpdf, string? pdfseparate, string input, int from, int to, string output)
    {
        if (from < 1 || to < from) throw new ArgumentException("Seitenbereich bitte wie 2-5.");
        return qpdf is not null ? (qpdf, [input, "--pages", input, $"{from}-{to}", "--", output]) : pdfseparate is not null ? (pdfseparate, ["-f", from.ToString(), "-l", to.ToString(), input, output.Replace(".pdf", "-%d.pdf")]) : throw new InvalidOperationException("Zum Teilen fehlt das kostenlose Programm qpdf (winget install qpdf.qpdf).");
    }
    public static (int From, int To) ParseRange(string s) { var m = Regex.Match(s.Trim(), @"^(\d+)\s*(?:-\s*(\d+))?$"); if (!m.Success) throw new ArgumentException("Seitenbereich bitte wie 2-5 oder 3."); var a = int.Parse(m.Groups[1].Value); return (a, m.Groups[2].Success ? int.Parse(m.Groups[2].Value) : a); }

    /// <summary>P16: Autor/Ersteller/Programm im Info-Bereich und in XMP mit gleich vielen Leerzeichen überschreiben – Datei bleibt gültig (Positionen ändern sich nicht).</summary>
    public static string StripMeta(string file)
    {
        var b = File.ReadAllBytes(file); var s = Encoding.Latin1.GetString(b);
        var o = Path.Combine(Path.GetDirectoryName(file)!, Path.GetFileNameWithoutExtension(file) + "-ohne-metadaten.pdf");
        void Blank(int start, int len) { for (var i = start; i < start + len; i++) b[i] = (byte)' '; }
        foreach (Match m in Regex.Matches(s, @"/(Author|Creator|Producer|Title|Subject|Keywords)\s*(\((?:\\.|[^\\)])*\)|<[0-9A-Fa-f\s]*>)")) { var v = m.Groups[2]; if (v.Value.StartsWith('(')) Blank(v.Index + 1, v.Length - 2); else { for (var i = v.Index + 1; i < v.Index + v.Length - 1; i++) b[i] = (byte)'0'; } }
        foreach (Match m in Regex.Matches(s, @"<(dc:creator|dc:title|dc:description|pdf:Producer|pdf:Author|xmp:CreatorTool|pdf:Keywords)\b[^>]*>(.*?)</\1>", RegexOptions.Singleline)) Blank(m.Groups[2].Index, m.Groups[2].Length);
        File.WriteAllBytes(o, b); return o;
    }
    public static Dictionary<string, string> Info(string file) =>
        Regex.Matches(Encoding.Latin1.GetString(File.ReadAllBytes(file)), @"/(Author|Creator|Producer|Title)\s*\(((?:\\.|[^\\)])*)\)").GroupBy(m => m.Groups[1].Value).ToDictionary(g => g.Key, g => g.First().Groups[2].Value.Trim());

    /// <summary>P25: Barrierefreiheits-Hinweise (Tags, Sprache, Titel, Alternativtexte).</summary>
    public static List<string> AccessibilityIssues(string file)
    {
        var s = Encoding.Latin1.GetString(File.ReadAllBytes(file)); var l = new List<string>();
        var compressedOnly = !s.Contains("/StructTreeRoot") && s.Contains("/ObjStm");
        if (!Regex.IsMatch(s, @"/Marked\s+true")) l.Add("Nicht als „getaggt“ markiert – Bildschirmleser finden keine Struktur.");
        if (!s.Contains("/StructTreeRoot")) l.Add("Keine Strukturbaum (Überschriften, Absätze, Tabellen fehlen für Bildschirmleser).");
        if (!Regex.IsMatch(s, @"/Lang\s*\(")) l.Add("Keine Dokumentsprache angegeben (z. B. de-DE).");
        if (!Regex.IsMatch(s, @"/Title\s*\(\s*[^)\s]") && !Regex.IsMatch(s, @"<dc:title>.*?\S.*?</dc:title>", RegexOptions.Singleline)) l.Add("Kein Dokumenttitel.");
        if (!Regex.IsMatch(s, @"/DisplayDocTitle\s+true")) l.Add("Fenstertitel zeigt den Dateinamen statt des Titels.");
        var figures = Regex.Matches(s, @"/S\s*/Figure\b[^>]*").Count; var alts = Regex.Matches(s, @"/S\s*/Figure\b[^>]*/Alt\s*\(").Count;
        if (figures > alts) l.Add($"{figures - alts} Bilder ohne Alternativtext.");
        if (compressedOnly) l.Add("Hinweis: Teile des PDFs sind komprimiert – die Prüfung ist dort nur grob.");
        return l;
    }
}

/// <summary>P23: PDF-Formularfelder lesen und ausfüllen (klassische PDFs, Textfelder und Kästchen) per inkrementeller Aktualisierung – das Original bleibt erhalten.</summary>
public static class PdfForm
{
    public sealed record Field(int Obj, int Gen, string Name, string Type, string Value);
    static string Unesc(string s) => Regex.Replace(s, @"\\([()\\])", "$1");
    static string Esc(string s) => s.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
    static List<(int Obj, int Gen, string Body)> Objects(string s) =>
        Regex.Matches(s, @"(\d+)\s+(\d+)\s+obj\b(.*?)endobj", RegexOptions.Singleline).Select(m => (int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), m.Groups[3].Value)).GroupBy(o => o.Item1).Select(g => g.Last()).ToList();
    public static List<Field> Fields(string file)
    {
        var s = Encoding.Latin1.GetString(File.ReadAllBytes(file));
        return Objects(s).Where(o => Regex.IsMatch(o.Body, @"/FT\s*/(Tx|Btn)") && Regex.IsMatch(o.Body, @"/T\s*\("))
            .Select(o => new Field(o.Obj, o.Gen, Unesc(Regex.Match(o.Body, @"/T\s*\(((?:\\.|[^\\)])*)\)").Groups[1].Value), Regex.Match(o.Body, @"/FT\s*/(\w+)").Groups[1].Value,
                Regex.Match(o.Body, @"/V\s*\(((?:\\.|[^\\)])*)\)") is { Success: true } v ? Unesc(v.Groups[1].Value) : Regex.Match(o.Body, @"/V\s*/(\w+)").Groups[1].Value)).ToList();
    }
    public static string Fill(string file, Dictionary<string, string> values)
    {
        var bytes = File.ReadAllBytes(file); var s = Encoding.Latin1.GetString(bytes);
        if (!Regex.IsMatch(s, @"\bxref\s")) throw new InvalidOperationException("Dieses PDF nutzt eine moderne Struktur, die Trendo nicht ausfüllen kann. Bitte im PDF-Programm ausfüllen.");
        var objs = Objects(s); var startxref = Regex.Matches(s, @"startxref\s+(\d+)").Last().Groups[1].Value;
        var trailer = Regex.Matches(s, @"trailer\s*<<(.*?)>>\s*startxref", RegexOptions.Singleline).Last().Groups[1].Value;
        var size = int.Parse(Regex.Match(trailer, @"/Size\s+(\d+)").Groups[1].Value); var root = Regex.Match(trailer, @"/Root\s+(\d+\s+\d+\s+R)").Groups[1].Value;
        var sb = new StringBuilder(); var offsets = new SortedDictionary<int, (long Off, int Gen)>(); long pos = bytes.Length + 1;
        void Emit(int obj, int gen, string body) { var t = $"{obj} {gen} obj{body}endobj\n"; offsets[obj] = (pos, gen); sb.Append(t); pos += Encoding.Latin1.GetByteCount(t); }
        foreach (var f in Fields(file).Where(f => values.ContainsKey(f.Name)))
        {
            var body = objs.First(o => o.Obj == f.Obj).Body; var v = values[f.Name];
            body = Regex.Replace(body, @"/V\s*(\((?:\\.|[^\\)])*\)|/\w+)", "");
            var val = f.Type == "Btn" ? (v is "ja" or "Yes" or "On" or "true" ? "/Yes" : "/Off") : $"({Esc(v)})";
            var lt = body.IndexOf("<<", StringComparison.Ordinal); body = body[..(lt + 2)] + " /V " + val + (f.Type == "Btn" ? " /AS " + val : "") + " " + body[(lt + 2)..];
            Emit(f.Obj, f.Gen, body);
        }
        if (offsets.Count == 0) throw new ArgumentException("Keines der Felder gibt es in diesem PDF.");
        var acro = objs.FirstOrDefault(o => o.Body.Contains("/Fields") && !o.Body.Contains("/Type /Catalog") && !o.Body.Contains("/Type/Catalog"));
        if (acro.Body is not null) { var ab = Regex.Replace(acro.Body, @"/NeedAppearances\s+\w+", ""); var k = ab.IndexOf("<<", StringComparison.Ordinal); Emit(acro.Obj, acro.Gen, ab[..(k + 2)] + " /NeedAppearances true " + ab[(k + 2)..]); }
        else
        {
            var cat = objs.First(o => Regex.IsMatch(o.Body, @"/Type\s*/Catalog"));
            Emit(cat.Obj, cat.Gen, Regex.Replace(cat.Body, @"(/AcroForm\s*<<)", "$1 /NeedAppearances true "));
        }
        var xref = new StringBuilder("xref\n");
        foreach (var (k, (off, gen)) in offsets) xref.Append($"{k} 1\n{off:D10} {gen:D5} n \n");
        var xrefPos = pos;
        var tail = $"{xref}trailer\n<< /Size {size} /Root {root} /Prev {startxref} >>\nstartxref\n{xrefPos}\n%%EOF\n";
        var o2 = Path.Combine(Path.GetDirectoryName(file)!, Path.GetFileNameWithoutExtension(file) + "-ausgefuellt.pdf");
        using (var fs = File.Create(o2)) { fs.Write(bytes); fs.Write(Encoding.Latin1.GetBytes("\n" + sb + tail)); }
        return o2;
    }
}

public static class Writing
{
    /// <summary>P14: Wörter, Sätze, Silben und Lesbarkeit (Flesch nach Amstad für Deutsch: 0–100, höher = leichter).</summary>
    public sealed record Stats(int Words, int Sentences, int Chars, double Flesch, string Level);
    /// <summary>Silben grob über Vokalgruppen (au, ei, eu, ie, äu zählen als eine).</summary>
    public static int Syllables(string w) => Math.Max(1, Regex.Matches(w.ToLowerInvariant(), "[aeiouyäöü]+").Count);
    public static Stats Analyze(string text)
    {
        var words = Regex.Matches(text, @"[\p{L}\p{N}][\p{L}\p{N}\-']*").Select(m => m.Value).ToList();
        var sentences = Math.Max(1, Regex.Matches(text, @"[^.!?]+[.!?]+(\s|$)").Count + (Regex.IsMatch(text.TrimEnd(), @"[.!?]$") ? 0 : 1));
        if (words.Count == 0) return new(0, 0, text.Length, 0, "—");
        var syl = words.Sum(Syllables);
        var f = Math.Round(Math.Clamp(180 - (double)words.Count / sentences - 58.5 * syl / words.Count, 0, 100), 1);
        return new(words.Count, sentences, text.Length, f, f >= 70 ? "leicht" : f >= 50 ? "mittel" : f >= 30 ? "schwer" : "sehr schwer");
    }

    /// <summary>P15: Unterschiede Zeile für Zeile (LCS). „+“ neu, „−“ entfernt, „ “ gleich.</summary>
    public static List<(char Op, string Line)> Diff(string a, string b)
    {
        var x = a.Replace("\r", "").Split('\n'); var y = b.Replace("\r", "").Split('\n');
        var L = new int[x.Length + 1, y.Length + 1];
        for (var i = x.Length - 1; i >= 0; i--) for (var j = y.Length - 1; j >= 0; j--) L[i, j] = x[i] == y[j] ? L[i + 1, j + 1] + 1 : Math.Max(L[i + 1, j], L[i, j + 1]);
        var res = new List<(char, string)>(); int p = 0, q = 0;
        while (p < x.Length && q < y.Length) { if (x[p] == y[q]) { res.Add((' ', x[p])); p++; q++; } else if (L[p + 1, q] >= L[p, q + 1]) res.Add(('−', x[p++])); else res.Add(('+', y[q++])); }
        while (p < x.Length) res.Add(('−', x[p++])); while (q < y.Length) res.Add(('+', y[q++]));
        return res;
    }

    /// <summary>Text aus TXT/MD/DOCX/ODT; PDF über pdftotext (wenn vorhanden).</summary>
    public static string TextOf(string file, string? pdftotext = null)
    {
        switch (Path.GetExtension(file).ToLowerInvariant())
        {
            case ".docx": return Ooxml.DocxText(file);
            case ".odt": return Ooxml.OdtText(file);
            case ".pdf":
                if (pdftotext is null) return "";
                var r = Cli.Run(pdftotext, ["-layout", "-enc", "UTF-8", file, "-"], timeoutMs: 60000); return r.Ok ? r.Out : "";
            case ".txt" or ".md" or ".csv": return File.ReadAllText(file);
            default: return "";
        }
    }

    /// <summary>P20: Volltextsuche mit Fundstelle.</summary>
    public sealed record Hit(string File, int Line, string Snippet);
    public static List<Hit> Search(string folder, string query, string? pdftotext = null, int max = 100)
    {
        var res = new List<Hit>(); if (query.Trim().Length < 2) return res;
        foreach (var f in Directory.EnumerateFiles(folder, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true }).Where(f => Path.GetExtension(f).ToLowerInvariant() is ".docx" or ".odt" or ".pdf" or ".txt" or ".md"))
        {
            string t; try { t = TextOf(f, pdftotext); } catch (Exception e) when (e is IOException or InvalidDataException) { continue; }
            var lines = t.Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                var k = lines[i].IndexOf(query, StringComparison.OrdinalIgnoreCase); if (k < 0) continue;
                var s = Math.Max(0, k - 40); res.Add(new(f, i + 1, (s > 0 ? "…" : "") + lines[i].Substring(s, Math.Min(lines[i].Length - s, query.Length + 80)).Trim()));
                if (res.Count >= max) return res;
            }
        }
        return res;
    }
}

/// <summary>P13: Rechtschreibprüfung mit Hunspell-Wörterbüchern (.dic/.aff) – kleiner eigener Leser für Präfixe/Suffixe. Zusammensetzungen (Komposita) werden nur einfach geprüft.</summary>
public sealed class Speller
{
    readonly HashSet<string> _words = new(StringComparer.Ordinal); readonly Dictionary<string, string> _flags = new(StringComparer.Ordinal);
    readonly List<(bool Suffix, char Flag, string Strip, string Add, Regex Cond)> _rules = [];
    readonly HashSet<string> _user = new(StringComparer.OrdinalIgnoreCase);
    public static readonly string[] KnownPaths = [@"%ProgramFiles%\LibreOffice\share\extensions\dict-de\de_DE_frami.dic", "/usr/share/hunspell/de_DE.dic", "/usr/share/hunspell/de_DE_frami.dic"];
    public Speller(string dic, string aff, IEnumerable<string>? userWords = null)
    {
        var enc = Encoding.UTF8;
        var a = File.ReadAllLines(aff, Encoding.Latin1); var set = a.FirstOrDefault(l => l.StartsWith("SET "))?[4..].Trim();
        if (set is not null && !set.Equals("UTF-8", StringComparison.OrdinalIgnoreCase)) enc = Encoding.Latin1; // ISO8859-1/-15: für deutsche Umlaute gleich
        foreach (var l in File.ReadAllLines(aff, enc))
        {
            var p = l.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (p.Length >= 5 && p[0] is "SFX" or "PFX")
                rules(p[0] == "SFX", p[1][0], p[2] == "0" ? "" : p[2], p[3].Split('/')[0] == "0" ? "" : p[3].Split('/')[0], p[4]);
        }
        foreach (var l in File.ReadLines(dic, enc).Skip(1))
        {
            var t = l.Split('\t')[0].Trim(); if (t.Length == 0) continue;
            var i = t.IndexOf('/'); var w = i < 0 ? t : t[..i]; _words.Add(w); if (i >= 0) _flags[w] = (_flags.GetValueOrDefault(w) ?? "") + t[(i + 1)..];
        }
        foreach (var u in userWords ?? []) _user.Add(u);
        void rules(bool sfx, char flag, string strip, string add, string cond) =>
            _rules.Add((sfx, flag, strip, add, new Regex(cond == "." ? "" : sfx ? cond + "$" : "^" + cond, RegexOptions.CultureInvariant)));
    }
    bool Base(string w) => _words.Contains(w) || (w.Length > 0 && char.IsUpper(w[0]) && _words.Contains(char.ToLowerInvariant(w[0]) + w[1..])) || (w.Length > 0 && _words.Contains(char.ToUpperInvariant(w[0]) + w[1..]));
    bool HasFlag(string w, char f) => (_flags.GetValueOrDefault(w) ?? _flags.GetValueOrDefault(char.ToUpperInvariant(w[0]) + w[1..]) ?? _flags.GetValueOrDefault(char.ToLowerInvariant(w[0]) + w[1..]) ?? "").Contains(f);
    public bool Correct(string word)
    {
        if (word.Length < 2 || word.Any(char.IsDigit) || _user.Contains(word) || Base(word)) return true;
        foreach (var r in _rules)
        {
            if (r.Suffix && word.EndsWith(r.Add, StringComparison.Ordinal)) { var b = word[..^r.Add.Length] + r.Strip; if (b.Length > 0 && r.Cond.IsMatch(b) && Base(b) && HasFlag(b, r.Flag)) return true; }
            if (!r.Suffix && word.StartsWith(r.Add, StringComparison.Ordinal)) { var b = r.Strip + word[r.Add.Length..]; if (b.Length > 0 && r.Cond.IsMatch(b) && Base(b) && HasFlag(b, r.Flag)) return true; }
        }
        for (var i = 3; i <= word.Length - 3; i++) if (Base(word[..i]) && (Base(word[i..]) || Base(char.ToUpperInvariant(word[i]) + word[(i + 1)..]))) return true; // einfache Komposita
        return false;
    }
    public List<string> Mistakes(string text) => Regex.Matches(text, @"\p{L}[\p{L}\-']*").Select(m => m.Value).Where(w => !Correct(w)).Distinct().ToList();
}

/// <summary>P09 Vorlagen mit Version im Namen, P19 Versionen vor dem Überschreiben, P24 Export-Paket.</summary>
public static class DocVersions
{
    public static string Backup(string file)
    {
        var dir = Path.Combine(Path.GetDirectoryName(file)!, ".versionen"); Directory.CreateDirectory(dir);
        var stem = Path.GetFileNameWithoutExtension(file); var ext = Path.GetExtension(file); var v = 1;
        while (File.Exists(Path.Combine(dir, $"{stem}-v{v:000}{ext}"))) v++;
        var o = Path.Combine(dir, $"{stem}-v{v:000}{ext}"); File.Copy(file, o); return o;
    }
    /// <summary>Schreibt sicher: vorhandene Datei wird vorher als v###-Kopie gesichert.</summary>
    public static void SafeWrite(string file, Action<string> write) { if (File.Exists(file)) Backup(file); write(file); }
    public static List<string> Versions(string file) { var dir = Path.Combine(Path.GetDirectoryName(file)!, ".versionen"); return Directory.Exists(dir) ? Directory.GetFiles(dir, Path.GetFileNameWithoutExtension(file) + "-v*" + Path.GetExtension(file)).OrderBy(x => x).ToList() : []; }

    public static string TemplateDir(string dataRoot) => Path.Combine(dataRoot, "vorlagen");
    public static string AddTemplate(string dataRoot, string name, string source)
    {
        var d = TemplateDir(dataRoot); Directory.CreateDirectory(d);
        var stem = string.Concat(name.Where(c => !Path.GetInvalidFileNameChars().Contains(c))).Trim(); if (stem.Length == 0) throw new ArgumentException("Bitte einen Namen.");
        var v = 1; while (Directory.GetFiles(d, $"{stem}-v{v:000}.*").Length > 0) v++;
        var o = Path.Combine(d, $"{stem}-v{v:000}{Path.GetExtension(source)}"); File.Copy(source, o); return o;
    }
    public static List<(string Name, int Version, string File)> Templates(string dataRoot) =>
        Directory.Exists(TemplateDir(dataRoot)) ? Directory.GetFiles(TemplateDir(dataRoot)).Select(f => Regex.Match(Path.GetFileNameWithoutExtension(f), @"^(.*)-v(\d{3})$") is { Success: true } m ? (m.Groups[1].Value, int.Parse(m.Groups[2].Value), f) : (Path.GetFileNameWithoutExtension(f), 0, f))
            .GroupBy(t => t.Item1).Select(g => g.OrderByDescending(t => t.Item2).First()).OrderBy(t => t.Item1).ToList() : [];

    public static string ExportPackage(string mainDoc, IEnumerable<string> attachments, string zip)
    {
        if (File.Exists(zip)) File.Delete(zip);
        var files = new[] { mainDoc }.Concat(attachments).Where(File.Exists).Distinct().ToList(); var sums = new StringBuilder();
        using (var z = ZipFile.Open(zip, ZipArchiveMode.Create))
            foreach (var (f, i) in files.Select((f, i) => (f, i)))
            {
                var name = (i == 0 ? "" : "Anlagen/") + Path.GetFileName(f); z.CreateEntryFromFile(f, name); sums.Append($"{J.Sha(f)}  {name}\n");
            }
        using (var z = ZipFile.Open(zip, ZipArchiveMode.Update)) using (var w = new StreamWriter(z.CreateEntry("SHA256SUMS.txt").Open(), new UTF8Encoding(false))) w.Write(sums.ToString());
        return zip;
    }
    public static bool VerifyPackage(string zip)
    {
        using var z = ZipFile.OpenRead(zip); var e = z.GetEntry("SHA256SUMS.txt"); if (e is null) return false;
        using var r = new StreamReader(e.Open());
        foreach (var l in r.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var sha = l[..64]; var name = l[66..]; var x = z.GetEntry(name); if (x is null) return false;
            using var s = x.Open(); if (Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(s)).ToLowerInvariant() != sha) return false;
        }
        return true;
    }
}

/// <summary>P12 Besprechungsprotokoll, P21 Aufgaben – beides mit .ics-Export (VTODO).</summary>
public sealed record TaskItem(string Id, string Title, DateTime? Due, string Who, bool Done);
public sealed class TaskList(string dataRoot)
{
    string F => Path.Combine(dataRoot, "werkzeuge", "aufgaben.json");
    public List<TaskItem> All() => J.Load<TaskItem>(F).OrderBy(t => t.Done).ThenBy(t => t.Due ?? DateTime.MaxValue).ToList();
    public TaskItem Add(string title, DateTime? due, string who = "") { var l = J.Load<TaskItem>(F); var t = new TaskItem(Guid.NewGuid().ToString("N")[..8], title.Trim(), due, who.Trim(), false); l.Add(t); J.Save(F, l); return t; }
    public void Toggle(string id) { var l = J.Load<TaskItem>(F); J.Save(F, l.Select(t => t.Id == id ? t with { Done = !t.Done } : t).ToList()); }
    public void Delete(string id) { var l = J.Load<TaskItem>(F); l.RemoveAll(t => t.Id == id); J.Save(F, l); }
    public List<TaskItem> Overdue(DateTime now) => All().Where(t => !t.Done && t.Due < now).ToList();

    /// <summary>VTODO mit Fälligkeit und Erinnerung (VALARM) zur Fälligkeit.</summary>
    public static string ToIcs(IEnumerable<TaskItem> tasks)
    {
        var sb = new StringBuilder("BEGIN:VCALENDAR\r\nVERSION:2.0\r\nPRODID:-//Trendo OS//DE\r\n");
        foreach (var t in tasks)
        {
            sb.Append($"BEGIN:VTODO\r\nUID:{t.Id}@trendo\r\nDTSTAMP:{DateTime.UtcNow:yyyyMMdd'T'HHmmss'Z'}\r\nSUMMARY:{t.Title.Replace(",", "\\,").Replace(";", "\\;")}\r\n");
            if (t.Due is { } d) sb.Append($"DUE:{d:yyyyMMdd'T'HHmmss}\r\nBEGIN:VALARM\r\nACTION:DISPLAY\r\nDESCRIPTION:{t.Title.Replace(",", "\\,")}\r\nTRIGGER:-PT0M\r\nEND:VALARM\r\n");
            if (t.Who.Length > 0) sb.Append($"DESCRIPTION:Zuständig: {t.Who}\r\n");
            sb.Append($"STATUS:{(t.Done ? "COMPLETED" : "NEEDS-ACTION")}\r\nEND:VTODO\r\n");
        }
        return sb.Append("END:VCALENDAR\r\n").ToString();
    }

    public sealed record Minutes(string Title, DateTime Date, string Attendees, string Notes, List<TaskItem> Tasks);
    /// <summary>Protokoll als DOCX und Aufgaben als .ics daneben.</summary>
    public static (string Docx, string Ics) WriteMinutes(Minutes m, string folder)
    {
        Directory.CreateDirectory(folder);
        var stem = $"{m.Date:yyyy-MM-dd}-Protokoll-{string.Concat(m.Title.Where(char.IsLetterOrDigit))}";
        var docx = Path.Combine(folder, stem + ".docx"); var ics = Path.Combine(folder, stem + "-aufgaben.ics");
        var paras = new List<string> { $"Datum: {m.Date:dd.MM.yyyy HH:mm}", $"Teilnehmende: {m.Attendees}", "", "Besprochen:" };
        paras.AddRange(m.Notes.Replace("\r", "").Split('\n')); paras.Add(""); paras.Add("Aufgaben:");
        paras.AddRange(m.Tasks.Select(t => $"☐ {t.Title}{(t.Who.Length > 0 ? " – " + t.Who : "")}{(t.Due is { } d ? $" – bis {d:dd.MM.yyyy}" : "")}"));
        DocVersions.SafeWrite(docx, f => Ooxml.NewDocx(f, "Protokoll: " + m.Title, paras));
        File.WriteAllText(ics, ToIcs(m.Tasks)); return (docx, ics);
    }
}

/// <summary>P07/P08: PDF-Text (pdftotext, sonst Texterkennung), Scan zu durchsuchbarem PDF (Tesseract).</summary>
public static class PdfText
{
    public static List<string> ScanToPdfArgs(string image, string outBase, string lang, string? tessdata) => [.. tessdata is null ? Array.Empty<string>() : ["--tessdata-dir", tessdata], image, outBase, "-l", lang, "pdf"];
    public static (bool Ok, string Text, string How) Extract(string pdf, string? pdftotext, string? pdftoppm, string? tesseract, string lang = "deu", string? tessdata = null)
    {
        if (pdftotext is not null) { var r = Cli.Run(pdftotext, ["-layout", "-enc", "UTF-8", pdf, "-"], timeoutMs: 60000); if (r.Ok && r.Out.Trim().Length > 0) return (true, r.Out, "Text aus dem PDF"); }
        if (pdftoppm is null || tesseract is null) return (false, "", pdftotext is null ? "Dafür fehlt Poppler (pdftotext)." : "Das PDF enthält keinen Text (Scan). Für Texterkennung fehlen Poppler oder Tesseract.");
        var tmp = Path.Combine(Path.GetTempPath(), "trendo-pdfocr-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(tmp);
        try
        {
            Cli.Run(pdftoppm, ["-r", "200", "-png", pdf, Path.Combine(tmp, "s")], timeoutMs: 120000);
            var sb = new StringBuilder();
            foreach (var img in Directory.GetFiles(tmp, "*.png").OrderBy(x => x))
            {
                var r = Cli.Run(tesseract, [.. tessdata is null ? Array.Empty<string>() : ["--tessdata-dir", tessdata], img, "stdout", "-l", lang], timeoutMs: 120000);
                if (r.Ok) sb.AppendLine(r.Out);
            }
            return (sb.Length > 0, sb.ToString(), "Texterkennung (Scan)");
        }
        finally { Directory.Delete(tmp, true); }
    }
}
