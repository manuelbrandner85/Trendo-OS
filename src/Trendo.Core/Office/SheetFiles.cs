using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;
using System.Xml.Linq;

namespace Trendo.Core.Office;

/// <summary>T-0228 Stufe B (C1): Trendo Tabelle öffnet und speichert XLSX (Excel/Calc), liest ODS (LibreOffice) und tauscht CSV aus.
/// Formeln werden in XLSX englisch gespeichert, wie es das Format verlangt, und beim Öffnen wieder deutsch angezeigt.</summary>
public static class SheetFiles
{
    public static readonly string[] Extensions = [".xlsx", ".ods", ".csv"];
    static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");
    static readonly XNamespace S = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    static readonly XNamespace Pr = "http://schemas.openxmlformats.org/package/2006/relationships";

    public static SheetDoc Open(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".xlsx" => ReadXlsx(path),
        ".ods" => ReadOds(path),
        ".csv" or ".txt" => ReadCsv(path),
        var e => throw new ArgumentException($"Das Format {e} kann Trendo Tabelle (noch) nicht öffnen."),
    };

    public static void Save(SheetDoc doc, string path)
    {
        var tmp = path + ".trendo-tmp";
        switch (Path.GetExtension(path).ToLowerInvariant())
        {
            case ".xlsx": WriteXlsx(doc, tmp); break;
            case ".csv": WriteCsv(doc, doc.Sheets[0], tmp); break;
            default: throw new ArgumentException("Speichern geht als .xlsx oder .csv (ODS kann Trendo Tabelle lesen).");
        }
        File.Move(tmp, path, true);
    }

    // ---------- XLSX schreiben ----------
    static string NumFmtCode(NumFmt f) => f switch
    {
        NumFmt.Euro => "#,##0.00\\ \"€\"", NumFmt.Datum => "DD.MM.YYYY", NumFmt.DatumZeit => "DD.MM.YYYY HH:MM", _ => "",
    };
    static int NumFmtId(NumFmt f) => f switch
    {
        NumFmt.Ganzzahl => 3, NumFmt.Zahl => 4, NumFmt.Prozent => 10, NumFmt.Text => 49, NumFmt.Euro => 164, NumFmt.Datum => 165, NumFmt.DatumZeit => 166, _ => 0,
    };

    public static void WriteXlsx(SheetDoc doc, string path)
    {
        var styles = new List<(NumFmt F, bool B, string? Fill, bool Border)> { (NumFmt.Standard, false, null, false) };
        var fills = new List<string>();
        var strings = new List<string>(); var stringIndex = new Dictionary<string, int>();
        int Style(Cell c)
        {
            var key = (c.Format, c.Bold, c.Fill?.ToUpperInvariant(), c.Border);
            var i = styles.IndexOf(key); if (i >= 0) return i;
            styles.Add(key); if (key.Item3 is { } f && !fills.Contains(f)) fills.Add(f); return styles.Count - 1;
        }
        int Str(string s) { if (!stringIndex.TryGetValue(s, out var i)) { stringIndex[s] = i = strings.Count; strings.Add(s); } return i; }

        var calc = new SheetCalc(doc);
        var sheetXml = new List<string>();
        foreach (var sh in doc.Sheets)
        {
            var sb = new StringBuilder();
            sb.Append($"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><worksheet xmlns=\"{S}\" xmlns:r=\"{R}\">");
            if (sh.MaxRow >= 0) sb.Append($"<dimension ref=\"A1:{Addr.Name(sh.MaxRow, Math.Max(0, sh.MaxCol))}\"/>");
            if (sh.ColWidths.Count > 0)
            {
                sb.Append("<cols>");
                foreach (var (c, w) in sh.ColWidths.OrderBy(x => x.Key)) sb.Append($"<col min=\"{c + 1}\" max=\"{c + 1}\" width=\"{w.ToString(CultureInfo.InvariantCulture)}\" customWidth=\"1\"/>");
                sb.Append("</cols>");
            }
            sb.Append("<sheetData>");
            foreach (var row in sh.Cells.Where(kv => !kv.Value.IsEmpty).GroupBy(kv => kv.Key.Row).OrderBy(g => g.Key))
            {
                sb.Append($"<row r=\"{row.Key + 1}\"{(sh.HiddenRows.Contains(row.Key) ? " hidden=\"1\"" : "")}>");
                foreach (var ((r, c), cell) in row.OrderBy(kv => kv.Key.Col))
                {
                    var s = Style(cell); var sAttr = s > 0 ? $" s=\"{s}\"" : ""; var a = Addr.Name(r, c);
                    var v = calc.Value(sh, r, c);
                    if (cell.IsFormula)
                    {
                        var f = SecurityElement.Escape(Formula.ToExcelSafe(cell.Raw[1..]));
                        sb.Append(v.Kind switch
                        {
                            CellValue.K.Number => $"<c r=\"{a}\"{sAttr}><f>{f}</f><v>{v.Number.ToString("R", CultureInfo.InvariantCulture)}</v></c>",
                            CellValue.K.Bool => $"<c r=\"{a}\"{sAttr} t=\"b\"><f>{f}</f><v>{(v.Number != 0 ? 1 : 0)}</v></c>",
                            CellValue.K.Error => $"<c r=\"{a}\"{sAttr} t=\"e\"><f>{f}</f><v>{SecurityElement.Escape(Formula.ErrorToExcel(v.Text))}</v></c>",
                            _ => $"<c r=\"{a}\"{sAttr} t=\"str\"><f>{f}</f><v>{SecurityElement.Escape(v.Text)}</v></c>",
                        });
                    }
                    else sb.Append(v.Kind switch
                    {
                        CellValue.K.Empty => $"<c r=\"{a}\"{sAttr}/>",
                        CellValue.K.Number => $"<c r=\"{a}\"{sAttr}><v>{v.Number.ToString("R", CultureInfo.InvariantCulture)}</v></c>",
                        CellValue.K.Bool => $"<c r=\"{a}\"{sAttr} t=\"b\"><v>{(v.Number != 0 ? 1 : 0)}</v></c>",
                        _ => $"<c r=\"{a}\"{sAttr} t=\"s\"><v>{Str(cell.Raw.StartsWith('\'') ? cell.Raw[1..] : cell.Raw)}</v></c>",
                    });
                }
                sb.Append("</row>");
            }
            sb.Append("</sheetData>");
            if (sh.HiddenRows.Count > 0 && sh.MaxRow >= 0) sb.Append($"<autoFilter ref=\"A1:{Addr.Name(sh.MaxRow, Math.Max(0, sh.MaxCol))}\"/>");
            sb.Append("<pageMargins left=\"0.7\" right=\"0.7\" top=\"0.75\" bottom=\"0.75\" header=\"0.3\" footer=\"0.3\"/></worksheet>");
            sheetXml.Add(sb.ToString());
        }

        var st = new StringBuilder($"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><styleSheet xmlns=\"{S}\">");
        var custom = styles.Select(x => x.F).Distinct().Where(f => NumFmtId(f) >= 164).ToList();
        if (custom.Count > 0) st.Append($"<numFmts count=\"{custom.Count}\">{string.Concat(custom.Select(f => $"<numFmt numFmtId=\"{NumFmtId(f)}\" formatCode=\"{SecurityElement.Escape(NumFmtCode(f))}\"/>"))}</numFmts>");
        st.Append("<fonts count=\"2\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font><font><b/><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts>");
        st.Append($"<fills count=\"{2 + fills.Count}\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill>");
        foreach (var f in fills) st.Append($"<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FF{f.TrimStart('#')}\"/><bgColor indexed=\"64\"/></patternFill></fill>");
        st.Append("</fills><borders count=\"2\"><border><left/><right/><top/><bottom/><diagonal/></border>");
        st.Append("<border><left style=\"thin\"><color auto=\"1\"/></left><right style=\"thin\"><color auto=\"1\"/></right><top style=\"thin\"><color auto=\"1\"/></top><bottom style=\"thin\"><color auto=\"1\"/></bottom><diagonal/></border></borders>");
        st.Append("<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>");
        st.Append($"<cellXfs count=\"{styles.Count}\">");
        foreach (var (f, b, fill, border) in styles)
        {
            var fillId = fill is null ? 0 : 2 + fills.IndexOf(fill);
            st.Append($"<xf numFmtId=\"{NumFmtId(f)}\" fontId=\"{(b ? 1 : 0)}\" fillId=\"{fillId}\" borderId=\"{(border ? 1 : 0)}\" xfId=\"0\"{(f != NumFmt.Standard ? " applyNumberFormat=\"1\"" : "")}{(b ? " applyFont=\"1\"" : "")}{(fill is null ? "" : " applyFill=\"1\"")}{(border ? " applyBorder=\"1\"" : "")}/>");
        }
        st.Append("</cellXfs><cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles></styleSheet>");

        using var fs = File.Create(path);
        using var z = new ZipArchive(fs, ZipArchiveMode.Create);
        void Add(string name, string content) { using var w = new StreamWriter(z.CreateEntry(name, CompressionLevel.Optimal).Open(), new UTF8Encoding(false)); w.Write(content); }
        Add("[Content_Types].xml", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
            "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
            "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
            string.Concat(doc.Sheets.Select((_, i) => $"<Override PartName=\"/xl/worksheets/sheet{i + 1}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>")) +
            "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" +
            "<Override PartName=\"/xl/sharedStrings.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml\"/>" +
            "<Override PartName=\"/docProps/core.xml\" ContentType=\"application/vnd.openxmlformats-package.core-properties+xml\"/></Types>");
        Add("_rels/.rels", $"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"{Pr}\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/><Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties\" Target=\"docProps/core.xml\"/></Relationships>");
        // Autor leer lassen: Trendo ist das Werkzeug, nicht der Verfasser (Vorgabe 2026-10-27)
        Add("docProps/core.xml", $"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><cp:coreProperties xmlns:cp=\"http://schemas.openxmlformats.org/package/2006/metadata/core-properties\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:dcterms=\"http://purl.org/dc/terms/\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\"><dc:creator></dc:creator><dcterms:created xsi:type=\"dcterms:W3CDTF\">{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}</dcterms:created></cp:coreProperties>");
        Add("xl/workbook.xml", $"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><workbook xmlns=\"{S}\" xmlns:r=\"{R}\"><sheets>" +
            string.Concat(doc.Sheets.Select((s, i) => $"<sheet name=\"{SecurityElement.Escape(s.Name)}\" sheetId=\"{i + 1}\" r:id=\"rId{i + 1}\"/>")) + "</sheets></workbook>");
        Add("xl/_rels/workbook.xml.rels", $"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"{Pr}\">" +
            string.Concat(doc.Sheets.Select((_, i) => $"<Relationship Id=\"rId{i + 1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet{i + 1}.xml\"/>")) +
            $"<Relationship Id=\"rId{doc.Sheets.Count + 1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>" +
            $"<Relationship Id=\"rId{doc.Sheets.Count + 2}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings\" Target=\"sharedStrings.xml\"/></Relationships>");
        for (var i = 0; i < sheetXml.Count; i++) Add($"xl/worksheets/sheet{i + 1}.xml", sheetXml[i]);
        Add("xl/styles.xml", st.ToString());
        Add("xl/sharedStrings.xml", $"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><sst xmlns=\"{S}\" count=\"{strings.Count}\" uniqueCount=\"{strings.Count}\">" +
            string.Concat(strings.Select(s => $"<si><t xml:space=\"preserve\">{SecurityElement.Escape(s)}</t></si>")) + "</sst>");
    }

    // ---------- XLSX lesen ----------
    static NumFmt FromNumFmt(int id, string? code)
    {
        switch (id)
        {
            case 0: return NumFmt.Standard;
            case 1 or 3 or 37 or 38: return NumFmt.Ganzzahl;
            case 2 or 4 or 39 or 40: return NumFmt.Zahl;
            case 9 or 10: return NumFmt.Prozent;
            case >= 14 and <= 17: return NumFmt.Datum;
            case 22: return NumFmt.DatumZeit;
            case 49: return NumFmt.Text;
        }
        if (code is null) return NumFmt.Standard;
        var c = code.ToUpperInvariant();
        if (c.Contains('€') || c.Contains("EUR")) return NumFmt.Euro;
        if (c.Contains('%')) return NumFmt.Prozent;
        if (c.Contains("YY") || c.Contains("JJ")) return c.Contains("HH") || c.Contains("H:") ? NumFmt.DatumZeit : NumFmt.Datum;
        if (c == "@") return NumFmt.Text;
        if (c.Contains("0.00") || c.Contains("0,00")) return NumFmt.Zahl;
        if (c.Contains("#,##0") || c == "0") return NumFmt.Ganzzahl;
        return NumFmt.Standard;
    }

    public static SheetDoc ReadXlsx(string path)
    {
        using var z = ZipFile.OpenRead(path);
        XDocument? Load(string name) { var e = z.GetEntry(name); if (e is null) return null; using var s = e.Open(); return XDocument.Load(s); }
        var wb = Load("xl/workbook.xml") ?? throw new InvalidDataException("Keine Excel-Mappe (workbook.xml fehlt).");
        var rels = Load("xl/_rels/workbook.xml.rels");
        var shared = Load("xl/sharedStrings.xml")?.Root?.Elements(S + "si").Select(si => string.Concat(si.Descendants(S + "t").Select(t => t.Value))).ToList() ?? [];
        var stDoc = Load("xl/styles.xml");
        var codes = stDoc?.Root?.Element(S + "numFmts")?.Elements(S + "numFmt").ToDictionary(n => (int)n.Attribute("numFmtId")!, n => (string?)n.Attribute("formatCode")) ?? [];
        var fontsBold = stDoc?.Root?.Element(S + "fonts")?.Elements(S + "font").Select(f => f.Element(S + "b") is { } b && (string?)b.Attribute("val") is null or "1" or "true").ToList() ?? [];
        var fillColors = stDoc?.Root?.Element(S + "fills")?.Elements(S + "fill").Select(f => f.Element(S + "patternFill") is { } p && (string?)p.Attribute("patternType") == "solid"
            && (string?)p.Element(S + "fgColor")?.Attribute("rgb") is { Length: >= 6 } rgb ? "#" + rgb[^6..].ToUpperInvariant() : null).ToList() ?? [];
        var borders = stDoc?.Root?.Element(S + "borders")?.Elements(S + "border").Select(b => b.Elements().Any(side => side.Attribute("style") is not null)).ToList() ?? [];
        var xfs = stDoc?.Root?.Element(S + "cellXfs")?.Elements(S + "xf").Select(x =>
        {
            int I(string a) => (int?)x.Attribute(a) ?? 0;
            var nf = I("numFmtId");
            return (Fmt: FromNumFmt(nf, codes.GetValueOrDefault(nf)), Bold: fontsBold.ElementAtOrDefault(I("fontId")), Fill: fillColors.ElementAtOrDefault(I("fillId")), Border: borders.ElementAtOrDefault(I("borderId")));
        }).ToList() ?? [];

        var doc = new SheetDoc(); doc.Sheets.Clear();
        foreach (var sheetEl in wb.Root!.Element(S + "sheets")!.Elements(S + "sheet"))
        {
            var name = (string)sheetEl.Attribute("name")!;
            var rid = (string?)sheetEl.Attribute(R + "id");
            var target = rels?.Root?.Elements(Pr + "Relationship").FirstOrDefault(r => (string?)r.Attribute("Id") == rid)?.Attribute("Target")?.Value ?? "worksheets/sheet1.xml";
            target = target.StartsWith('/') ? target[1..] : "xl/" + target;
            var sh = new Sheet(name); doc.Sheets.Add(sh);
            var ws = Load(target); if (ws is null) continue;
            foreach (var col in ws.Root!.Element(S + "cols")?.Elements(S + "col") ?? [])
            {
                var min = (int)col.Attribute("min")! - 1; var max = Math.Min((int)col.Attribute("max")! - 1, min + 200);
                if (double.TryParse((string?)col.Attribute("width"), NumberStyles.Float, CultureInfo.InvariantCulture, out var w) && (string?)col.Attribute("customWidth") is "1" or "true")
                    for (var c = min; c <= max; c++) sh.ColWidths[c] = Math.Round(w, 2);
            }
            foreach (var row in ws.Root.Element(S + "sheetData")?.Elements(S + "row") ?? [])
            {
                if ((string?)row.Attribute("hidden") is "1" or "true" && (int?)row.Attribute("r") is int hr) sh.HiddenRows.Add(hr - 1);
                foreach (var c in row.Elements(S + "c"))
                {
                    var (r, ci) = Addr.Parse((string)c.Attribute("r")!);
                    var t = (string?)c.Attribute("t") ?? "n"; var v = c.Element(S + "v")?.Value; var f = c.Element(S + "f")?.Value;
                    var style = (int?)c.Attribute("s") is int si && si < xfs.Count ? xfs[si] : default;
                    string raw;
                    if (!string.IsNullOrEmpty(f)) raw = "=" + Formula.FromExcelSafe(f);
                    else raw = t switch
                    {
                        "s" => int.TryParse(v, out var idx) && idx < shared.Count ? Escape(shared[idx]) : "",
                        "inlineStr" => Escape(string.Concat(c.Descendants(S + "t").Select(x => x.Value))),
                        "b" => v == "1" ? "WAHR" : "FALSCH",
                        "e" => "'" + (v ?? ""),
                        "str" => Escape(v ?? ""),
                        _ => v is null ? "" : NumberRaw(double.Parse(v, CultureInfo.InvariantCulture), style.Fmt),
                    };
                    if (raw.Length == 0 && !style.Bold && style.Fill is null && !style.Border && style.Fmt == NumFmt.Standard) continue;
                    var cell = sh.GetOrAdd(r, ci);
                    cell.Raw = raw; cell.Format = style.Fmt; cell.Bold = style.Bold; cell.Fill = style.Fill; cell.Border = style.Border;
                }
            }
        }
        if (doc.Sheets.Count == 0) doc.Sheets.Add(new Sheet("Tabelle1"));
        return doc;
    }

    /// <summary>Text, der wie eine Zahl, Formel oder ein Datum aussieht, bleibt Text („'“ davor, wie in Calc).</summary>
    static string Escape(string text) =>
        text.Length > 0 && (text[0] is '=' or '\'' || SheetCalc.Literal(text).Value.Kind != CellValue.K.Text) ? "'" + text : text;

    /// <summary>Zahl so ablegen, wie man sie eintippen würde: Datum als „27.10.2026“, sonst deutsch mit voller Genauigkeit.</summary>
    static string NumberRaw(double d, NumFmt f)
    {
        if (f is NumFmt.Datum or NumFmt.DatumZeit && d is > 0 and < 2958466)
        {
            var dt = SheetFormat.FromSerial(d);
            if (f == NumFmt.Datum && dt.TimeOfDay == TimeSpan.Zero) return dt.ToString("dd.MM.yyyy", De);
            if (f == NumFmt.DatumZeit && dt.Second == 0) return dt.ToString("dd.MM.yyyy HH:mm", De);
        }
        return d.ToString("R", De);
    }

    // ---------- ODS lesen ----------
    static readonly XNamespace Office = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";
    static readonly XNamespace Table = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";
    static readonly XNamespace TextNs = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";

    public static SheetDoc ReadOds(string path)
    {
        using var z = ZipFile.OpenRead(path);
        var e = z.GetEntry("content.xml") ?? throw new InvalidDataException("Keine ODS-Datei (content.xml fehlt).");
        XDocument x; using (var s = e.Open()) x = XDocument.Load(s);
        var doc = new SheetDoc(); doc.Sheets.Clear();
        foreach (var t in x.Descendants(Table + "table"))
        {
            var sh = new Sheet((string?)t.Attribute(Table + "name") ?? $"Tabelle{doc.Sheets.Count + 1}"); doc.Sheets.Add(sh);
            var r = 0;
            foreach (var row in t.Descendants(Table + "table-row"))
            {
                var rowRep = Math.Min((int?)row.Attribute(Table + "number-rows-repeated") ?? 1, 10_000);
                var cells = row.Elements().Where(c => c.Name == Table + "table-cell" || c.Name == Table + "covered-table-cell").ToList();
                var hasContent = cells.Any(c => c.Attribute(Office + "value-type") is not null || c.Attribute(Table + "formula") is not null);
                if (!hasContent) { r += rowRep; continue; }
                for (var rr = 0; rr < rowRep; rr++, r++)
                {
                    var col = 0;
                    foreach (var c in cells)
                    {
                        var rep = Math.Min((int?)c.Attribute(Table + "number-columns-repeated") ?? 1, 1024);
                        var raw = OdsRaw(c, out var fmt);
                        for (var k = 0; k < rep; k++, col++)
                            if (raw.Length > 0 && r < Addr.MaxRows && col < Addr.MaxCols) { var cell = sh.GetOrAdd(r, col); cell.Raw = raw; cell.Format = fmt; }
                    }
                }
            }
        }
        if (doc.Sheets.Count == 0) doc.Sheets.Add(new Sheet("Tabelle1"));
        return doc;
    }

    static string OdsRaw(XElement c, out NumFmt fmt)
    {
        fmt = NumFmt.Standard;
        var type = (string?)c.Attribute(Office + "value-type");
        if ((string?)c.Attribute(Table + "formula") is { } f && Formula.FromOds(f) is { } de) return "=" + de;
        switch (type)
        {
            case "float": return NumberRaw(double.Parse((string)c.Attribute(Office + "value")!, CultureInfo.InvariantCulture), fmt);
            case "currency": fmt = NumFmt.Euro; return NumberRaw(double.Parse((string)c.Attribute(Office + "value")!, CultureInfo.InvariantCulture), fmt);
            case "percentage": fmt = NumFmt.Prozent; return NumberRaw(double.Parse((string)c.Attribute(Office + "value")!, CultureInfo.InvariantCulture), fmt);
            case "boolean": return (string?)c.Attribute(Office + "boolean-value") == "true" ? "WAHR" : "FALSCH";
            case "date":
                var dv = (string)c.Attribute(Office + "date-value")!;
                if (DateTime.TryParse(dv, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) { fmt = d.TimeOfDay == TimeSpan.Zero ? NumFmt.Datum : NumFmt.DatumZeit; return NumberRaw(SheetFormat.ToSerial(d), fmt); }
                return dv;
            case null: return "";
            default: return Escape(string.Join("\n", c.Elements(TextNs + "p").Select(p => p.Value)));
        }
    }

    // ---------- CSV ----------
    public static SheetDoc ReadCsv(string path)
    {
        var text = File.ReadAllText(path, Encoding.UTF8);
        var first = text.Split('\n')[0];
        var sep = new[] { ';', '\t', ',' }.OrderByDescending(ch => first.Count(x => x == ch)).First();
        var doc = new SheetDoc(); var sh = doc.Sheets[0]; sh.Name = Path.GetFileNameWithoutExtension(path) is { Length: > 0 and <= 31 } n && n.IndexOfAny(['[', ']', '*', '?', '/', '\\', ':']) < 0 ? n : "Tabelle1";
        int r = 0, c = 0; var cell = new StringBuilder(); var quoted = false;
        void Put() { if (cell.Length > 0) sh.Set(r, c, cell.ToString().StartsWith('=') ? "'" + cell : cell.ToString()); cell.Clear(); }
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (quoted) { if (ch == '"') { if (i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; } else quoted = false; } else cell.Append(ch); continue; }
            if (ch == '"' && cell.Length == 0) { quoted = true; continue; }
            if (ch == sep) { Put(); c++; continue; }
            if (ch == '\r') continue;
            if (ch == '\n') { Put(); r++; c = 0; if (r >= Addr.MaxRows) break; continue; }
            cell.Append(ch);
        }
        Put();
        return doc;
    }

    /// <summary>CSV wie Excel auf Deutsch: Semikolon, UTF-8 mit BOM, angezeigte Werte (Formeln als Ergebnis).</summary>
    public static void WriteCsv(SheetDoc doc, Sheet sh, string path)
    {
        var calc = new SheetCalc(doc); var sb = new StringBuilder();
        for (var r = 0; r <= sh.MaxRow; r++)
        {
            var parts = new List<string>();
            for (var c = 0; c <= sh.MaxCol; c++)
            {
                var v = calc.Display(sh, r, c);
                parts.Add(v.IndexOfAny([';', '"', '\n', '\r']) >= 0 ? "\"" + v.Replace("\"", "\"\"") + "\"" : v);
            }
            sb.Append(string.Join(';', parts)).Append("\r\n");
        }
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
    }
}
