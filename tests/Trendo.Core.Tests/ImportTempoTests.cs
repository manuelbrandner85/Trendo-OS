using System.IO.Compression;
using System.Text;
using Trendo.Core.Office;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0502: Die schnellen, gestreamten Leser für Excel und OBJ liefern dasselbe wie vorher und halten die Schutzregeln ein.</summary>
public class ImportTempoTests
{
    const string S = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    const string R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    const string Dtd = """<?xml version="1.0"?><!DOCTYPE x [<!ENTITY a "b">]>""";

    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-import-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

    static readonly string Workbook = $"""<workbook xmlns="{S}" xmlns:r="{R}"><sheets><sheet name="Blatt" r:id="rId1"/></sheets></workbook>""";
    const string Rels = """<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Target="worksheets/sheet1.xml"/></Relationships>""";

    static string Xlsx(string dir, string sheet, string? shared = null)
    {
        var path = Path.Combine(dir, "a.xlsx");
        using var z = ZipFile.Open(path, ZipArchiveMode.Create);
        void Add(string name, string content) { using var w = new StreamWriter(z.CreateEntry(name).Open(), new UTF8Encoding(false)); w.Write(content); }
        Add("xl/workbook.xml", Workbook);
        Add("xl/_rels/workbook.xml.rels", Rels);
        Add("xl/worksheets/sheet1.xml", sheet);
        if (shared is not null) Add("xl/sharedStrings.xml", shared);
        return path;
    }

    [Fact(DisplayName = "T-0502: Excel – gemeinsame Texte, Rich-Text, Inline-Text und Formeln")]
    public void Xlsx_cell_kinds()
    {
        var d = Tmp();
        try
        {
            var f = Xlsx(d, $"""
                <worksheet xmlns="{S}"><sheetData>
                <row r="1"><c r="A1" t="s"><v>0</v></c><c r="B1" t="s"><v>1</v></c><c r="C1" t="s"><v>2</v></c><c r="D1" t="s"><v>0</v></c></row>
                <row r="2"><c r="A2" t="inlineStr"><is><r><t>In</t></r><r><t>line</t></r></is></c><c r="B2"><f>SUM(A1:A2)</f><v>3</v></c><c r="C2" t="b"><v>1</v></c><c r="D2"><v>1.5</v></c></row>
                </sheetData></worksheet>
                """,
                $"""<sst xmlns="{S}"><si><t>Haus</t></si><si><r><t>Fett </t></r><r><t xml:space="preserve"> dünn</t></r></si><si><t>123</t></si></sst>""");
            var s = SheetFiles.ReadXlsx(f).Sheets[0];
            Assert.Equal("Haus", s.Raw(0, 0));
            Assert.Equal("Fett  dünn", s.Raw(0, 1));
            Assert.Equal("'123", s.Raw(0, 2)); // Text, der wie eine Zahl aussieht, bleibt Text
            Assert.Equal("Haus", s.Raw(0, 3));
            Assert.Equal("Inline", s.Raw(1, 0));
            Assert.Equal("=SUMME(A1:A2)", s.Raw(1, 1));
            Assert.Equal("WAHR", s.Raw(1, 2));
            Assert.Equal("1,5", s.Raw(1, 3));
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0502: Excel – Abschnitte vor und nach den Zeilen bleiben erhalten")]
    public void Xlsx_sections_around_sheetdata()
    {
        var d = Tmp();
        try
        {
            var f = Xlsx(d, $"""
                <worksheet xmlns="{S}">
                <sheetViews><sheetView><pane xSplit="1" ySplit="2" state="frozen"/></sheetView></sheetViews>
                <cols><col min="1" max="1" width="20" customWidth="1"/></cols>
                <sheetData><row r="1"><c r="A1"><v>1</v></c></row><row r="3" hidden="1"/><fremd/></sheetData>
                <sheetProtection sheet="1"/>
                <dataValidations><dataValidation type="list" sqref="B1"><formula1>"Ja,Nein"</formula1></dataValidation></dataValidations>
                <sheetData><row r="9"><c r="A9"><v>9</v></c></row></sheetData>
                </worksheet>
                """);
            var s = SheetFiles.ReadXlsx(f).Sheets[0];
            Assert.Equal((2, 1), (s.FreezeRows, s.FreezeCols));
            Assert.Equal(20, s.ColWidths[0]);
            Assert.Contains(2, s.HiddenRows);
            Assert.True(s.Protected);
            Assert.Equal(["Ja", "Nein"], s.Validations.Single().Items);
            Assert.Equal("1", s.Raw(0, 0));
            Assert.Equal("", s.Raw(8, 0)); // nur der erste Datenabschnitt zählt
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0502: Excel – Namensraum-Präfix und leeres Blatt")]
    public void Xlsx_prefix_and_empty()
    {
        var d = Tmp();
        try
        {
            var s = SheetFiles.ReadXlsx(Xlsx(d, $"""<x:worksheet xmlns:x="{S}"><x:sheetData><x:row r="1"><x:c r="B1"><x:v>2</x:v></x:c></x:row></x:sheetData></x:worksheet>""")).Sheets[0];
            Assert.Equal("2", s.Raw(0, 1));
            File.Delete(Path.Combine(d, "a.xlsx"));
            Assert.Empty(SheetFiles.ReadXlsx(Xlsx(d, $"""<worksheet xmlns="{S}"/>""")).Sheets[0].Cells);
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0502: Excel – DTD wird auch beim Streamen abgelehnt (Blatt und gemeinsame Texte)")]
    public void Xlsx_dtd_rejected_when_streaming()
    {
        var d = Tmp();
        try
        {
            var sheet = Xlsx(d, Dtd + $"""<worksheet xmlns="{S}"><sheetData/></worksheet>""");
            Assert.Contains("Sicherheitsgründen", Assert.Throws<InvalidDataException>(() => SheetFiles.ReadXlsx(sheet)).Message);
            File.Delete(sheet);
            var shared = Xlsx(d, $"""<worksheet xmlns="{S}"><sheetData/></worksheet>""", Dtd + $"""<sst xmlns="{S}"/>""");
            Assert.Contains("Sicherheitsgründen", Assert.Throws<InvalidDataException>(() => SheetFiles.ReadXlsx(shared)).Message);
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0502: OBJ – Vielecke, negative Indizes, Schrägstriche und Leerraum")]
    public void Obj_plain()
    {
        var d = Tmp();
        try
        {
            var f = Path.Combine(d, "a.obj");
            File.WriteAllText(f, "# Kommentar\nv 0 0 0\r\n  v   1  0   0\t\nv 1 1 0\nv 0 1.5E0 0\n\nf 1 2 3 4\nf -4/1 -3/2/5 -2//3\nf 1 2\nvn 0 0 1\n");
            var m = Mesh3D.ReadMeshFile(f);
            Assert.Equal(3, m.Triangles); // Viereck = 2 Dreiecke, dazu eins mit negativen Indizes; „f 1 2“ zählt nicht
            Assert.Equal(1.5, m.V[5].Y, 6); // vierter Punkt, zweites Dreieck des Vierecks
            Assert.Equal(1, m.V[^1].Y, 6);  // „-2“ zeigt auf den dritten Punkt
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0502: Excel – Hunderttausende Textstücke brauchen keine quadratische Zeit")]
    public void Xlsx_many_text_pieces_stay_linear()
    {
        const int N = 500_000; const int Expected = N * 4;
        static string Repeat(string piece) => new StringBuilder(piece.Length * N).Insert(0, piece, N).ToString();
        var d = Tmp();
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            string Fresh(string sheet, string? shared = null) { File.Delete(Path.Combine(d, "a.xlsx")); return Xlsx(d, sheet, shared); }
            var oneShared = $"""<worksheet xmlns="{S}"><sheetData><row r="1"><c r="A1" t="s"><v>0</v></c></row></sheetData></worksheet>""";
            Assert.Equal(Expected, SheetFiles.ReadXlsx(Fresh(oneShared, $"""<sst xmlns="{S}"><si><t>{Repeat("aaaa<b/>")}</t></si></sst>""")).Sheets[0].Raw(0, 0).Length);
            Assert.Equal(Expected, SheetFiles.ReadXlsx(Fresh(oneShared, $"""<sst xmlns="{S}"><si>{Repeat("<t>aaaa</t>")}</si></sst>""")).Sheets[0].Raw(0, 0).Length);
            Assert.Equal(Expected, SheetFiles.ReadXlsx(Fresh($"""<worksheet xmlns="{S}"><sheetData><row r="1"><c r="A1" t="inlineStr"><is>{Repeat("<t>aaaa</t>")}</is></c></row></sheetData></worksheet>""")).Sheets[0].Raw(0, 0).Length);
            Assert.Equal(Expected, SheetFiles.ReadXlsx(Fresh($"""<worksheet xmlns="{S}"><sheetData><row r="1"><c r="A1" t="str"><v>{Repeat("aaaa<b/>")}</v></c></row></sheetData></worksheet>""")).Sheets[0].Raw(0, 0).Length);
            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(30), $"Dauer: {sw.Elapsed}");
        }
        finally { Directory.Delete(d, true); }
    }

    [Theory(DisplayName = "T-0502: Excel – leeres oder falsch beendetes XML bleibt ein XML-Fehler")]
    [InlineData("")]
    [InlineData("<!-- nur ein Kommentar -->")]
    [InlineData("<worksheet><sheetData/></worksheet><zweite/>")]
    [InlineData("<worksheet><sheetData><row>")]
    public void Xlsx_broken_xml(string broken)
    {
        var d = Tmp();
        try
        {
            var sheet = Xlsx(d, broken);
            Assert.Throws<System.Xml.XmlException>(() => SheetFiles.ReadXlsx(sheet));
            File.Delete(sheet);
            var shared = Xlsx(d, $"""<worksheet xmlns="{S}"><sheetData/></worksheet>""", broken);
            Assert.Throws<System.Xml.XmlException>(() => SheetFiles.ReadXlsx(shared));
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0502: Excel – leere Zellen, Kommentare, Fehlerwerte, zweiter Wert, CDATA")]
    public void Xlsx_cell_edge_cases()
    {
        var d = Tmp();
        try
        {
            var f = Xlsx(d, $"""
                <worksheet xmlns="{S}"><sheetData>
                <!-- vor der Zeile -->
                <row r="1"><c r="A1"/><?pi test?><c r="B1" t="e"><v>#DIV/0!</v></c><c r="C1"><v>1</v><v>2</v></c><c r="D1"><v>4<!-- mitten -->2</v></c></row>
                <row r="2"><c r="A2" t="str"><v><![CDATA[<x>]]> &amp; y</v></c><c r="B2" t="str"><v/></c></row>
                </sheetData></worksheet>
                """);
            var s = SheetFiles.ReadXlsx(f).Sheets[0];
            Assert.Null(s.Get(0, 0));
            Assert.Equal("'#DIV/0!", s.Raw(0, 1));
            Assert.Equal("1", s.Raw(0, 2));
            Assert.Equal("42", s.Raw(0, 3));
            Assert.Equal("<x> & y", s.Raw(1, 0));
            Assert.Null(s.Get(1, 1));
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0502: OBJ – kaputte Zeilen verhalten sich wie bisher")]
    public void Obj_plain_errors()
    {
        var d = Tmp();
        try
        {
            var f = Path.Combine(d, "a.obj");
            File.WriteAllText(f, "v 0 0 0\nv 1 0 0\nv 1 1 0\nv 1 2\nf a b\nf 1 2 3\n");
            Assert.Equal(1, Mesh3D.ReadMeshFile(f).Triangles); // zu kurze „v“- und „f“-Zeilen zählen nicht
            File.WriteAllText(f, "v a b c\n");
            Assert.Throws<FormatException>(() => Mesh3D.ReadMeshFile(f));
            File.WriteAllText(f, "v 0 0 0\nv 1 0 0\nv 1 1 0\nf 1 2 x\n");
            Assert.Throws<FormatException>(() => Mesh3D.ReadMeshFile(f));
            File.WriteAllText(f, "v 0 0 0\nv 1 0 0\nv 1 1 0\nf 1 2 9\n");
            Assert.Throws<ArgumentOutOfRangeException>(() => Mesh3D.ReadMeshFile(f));
        }
        finally { Directory.Delete(d, true); }
    }

    const string RelNs = "http://schemas.openxmlformats.org/package/2006/relationships";

    static string Zip(string dir, params (string Name, string Content)[] entries)
    {
        var path = Path.Combine(dir, Guid.NewGuid().ToString("N") + ".xlsx");
        using var z = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (name, content) in entries) { using var w = new StreamWriter(z.CreateEntry(name).Open(), new UTF8Encoding(false)); w.Write(content); }
        return path;
    }

    static string SheetsXml(int count) => $"""<workbook xmlns="{S}" xmlns:r="{R}"><sheets>{string.Concat(Enumerable.Range(0, count).Select(i => $"<sheet name=\"B{i}\" r:id=\"rId1\"/>"))}</sheets></workbook>""";

    static readonly (string, string) SheetRels = ("xl/worksheets/_rels/sheet1.xml.rels", $"""<Relationships xmlns="{RelNs}"><Relationship Id="c1" Type="http://x/comments" Target="../comments1.xml"/></Relationships>""");
    static readonly string OneCell = $"""<worksheet xmlns="{S}"><sheetData><row r="1"><c r="A1"><v>1</v></c></row></sheetData></worksheet>""";

    [Fact(DisplayName = "T-0503: Excel-Notizen – tief verschachtelte Texte zählen einmal und bleiben schnell")]
    public void Notes_nested_text_once()
    {
        const int Depth = 100_000;
        var d = Tmp();
        try
        {
            var nested = string.Concat(Enumerable.Repeat("<t>", Depth)) + "x" + string.Concat(Enumerable.Repeat("</t>", Depth));
            var f = Zip(d, ("xl/workbook.xml", SheetsXml(1)), ("xl/_rels/workbook.xml.rels", Rels), ("xl/worksheets/sheet1.xml", OneCell), SheetRels,
                ("xl/comments1.xml", $"""<comments xmlns="{S}"><commentList><comment ref="A1"><text>{nested}</text></comment><comment ref="B2"><text><r><t>Teil </t></r><r><t>zwei</t></r></text></comment><comment ref="kaputt"><text><t>weg</t></text></comment></commentList></comments>"""));
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var s = SheetFiles.ReadXlsx(f).Sheets[0];
            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(30), $"Dauer: {sw.Elapsed}");
            Assert.Equal("x", s.Get(0, 0)!.Note);
            Assert.Equal("Teil zwei", s.Get(1, 1)!.Note);
            Assert.Equal(2, s.Cells.Count(c => c.Value.Note is not null));
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0503: Excel-Notizen – kaputte Notizdatei kostet nur die Notizen, nicht die Zellen")]
    public void Notes_broken_file_keeps_cells()
    {
        var d = Tmp();
        try
        {
            var f = Zip(d, ("xl/workbook.xml", SheetsXml(1)), ("xl/_rels/workbook.xml.rels", Rels), ("xl/worksheets/sheet1.xml", OneCell), SheetRels,
                ("xl/comments1.xml", $"""<comments xmlns="{S}"><commentList><comment ref="A1"><text><t>schon gelesen</t></text></comment><comment ref="B2">"""));
            var s = SheetFiles.ReadXlsx(f).Sheets[0];
            Assert.Equal("1", s.Raw(0, 0));
            Assert.All(s.Cells, c => Assert.Null(c.Value.Note));
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0503: Excel – zu viele Blätter werden abgelehnt, die Grenze selbst geht noch")]
    public void Too_many_sheets()
    {
        var d = Tmp();
        try
        {
            var ok = Zip(d, ("xl/workbook.xml", SheetsXml(OfficeZip.MaxEntries)), ("xl/_rels/workbook.xml.rels", Rels), ("xl/worksheets/sheet1.xml", OneCell));
            Assert.Equal(OfficeZip.MaxEntries, SheetFiles.ReadXlsx(ok).Sheets.Count);
            var bad = Zip(d, ("xl/workbook.xml", SheetsXml(OfficeZip.MaxEntries + 1)), ("xl/_rels/workbook.xml.rels", Rels), ("xl/worksheets/sheet1.xml", OneCell));
            Assert.Contains("Sicherheitsgründen", Assert.Throws<InvalidDataException>(() => SheetFiles.ReadXlsx(bad)).Message);
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0503: Excel – ein Blatt-Eintrag kann nicht beliebig oft gelesen werden")]
    public void Repeated_sheet_part_is_budgeted()
    {
        var d = Tmp();
        try
        {
            var big = $"""<worksheet xmlns="{S}"><!--{new string('a', 1024 * 1024)}--><sheetData/></worksheet>""";
            var f = Zip(d, ("xl/workbook.xml", SheetsXml(600)), ("xl/_rels/workbook.xml.rels", Rels), ("xl/worksheets/sheet1.xml", big));
            Assert.Contains("Sicherheitsgründen", Assert.Throws<InvalidDataException>(() => SheetFiles.ReadXlsx(f)).Message);
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0503: Excel – Mehrfach-Verweis liest Zellen je Blatt, Notizen nur beim ersten")]
    public void Repeated_sheet_part_extras_once()
    {
        var d = Tmp();
        try
        {
            var f = Zip(d, ("xl/workbook.xml", SheetsXml(2)), ("xl/_rels/workbook.xml.rels", Rels), ("xl/worksheets/sheet1.xml", OneCell), SheetRels,
                ("xl/comments1.xml", $"""<comments xmlns="{S}"><commentList><comment ref="A1"><text><t>Notiz</t></text></comment></commentList></comments>"""));
            var doc = SheetFiles.ReadXlsx(f);
            Assert.Equal(["1", "1"], doc.Sheets.Select(s => s.Raw(0, 0)));
            Assert.Equal("Notiz", doc.Sheets[0].Get(0, 0)!.Note);
            Assert.Null(doc.Sheets[1].Get(0, 0)!.Note);
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0503: Excel – Blatt-Zuordnung: erste Beziehung gewinnt, fehlendes Ziel, fehlende Id")]
    public void Sheet_relationship_mapping()
    {
        var d = Tmp();
        try
        {
            static string Value(int n) => $"""<worksheet xmlns="{S}"><sheetData><row r="1"><c r="A1"><v>{n}</v></c></row></sheetData></worksheet>""";
            var f = Zip(d,
                ("xl/workbook.xml", $"""<workbook xmlns="{S}" xmlns:r="{R}"><sheets><sheet name="A" r:id="rId1"/><sheet name="B" r:id="rId2"/><sheet name="C"/><sheet name="D" r:id="gibtsnicht"/></sheets></workbook>"""),
                ("xl/_rels/workbook.xml.rels", $"""<Relationships xmlns="{RelNs}"><Relationship Id="rId1" Target="worksheets/sheet2.xml"/><Relationship Id="rId1" Target="worksheets/sheet3.xml"/><Relationship Id="rId2"/><Relationship Target="/xl/worksheets/sheet3.xml"/></Relationships>"""),
                ("xl/worksheets/sheet1.xml", Value(1)), ("xl/worksheets/sheet2.xml", Value(2)), ("xl/worksheets/sheet3.xml", Value(3)));
            Assert.Equal(["2", "1", "3", "1"], SheetFiles.ReadXlsx(f).Sheets.Select(s => s.Raw(0, 0)));
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0503: Excel-Notizen – zweite Liste, doppelter Bezug, fehlender Bezug")]
    public void Notes_list_edge_cases()
    {
        var d = Tmp();
        try
        {
            var f = Zip(d, ("xl/workbook.xml", SheetsXml(1)), ("xl/_rels/workbook.xml.rels", Rels), ("xl/worksheets/sheet1.xml", OneCell), SheetRels,
                ("xl/comments1.xml", $"""<comments xmlns="{S}"><commentList><comment ref="A1"><text><t>erste</t></text></comment><comment ref="A1"><text><t>zweite</t></text></comment><comment><text><t>ohne</t></text></comment><fremd ref="C3"/></commentList><commentList><comment ref="D4"><text><t>spaeter</t></text></comment></commentList></comments>"""));
            var s = SheetFiles.ReadXlsx(f).Sheets[0];
            Assert.Equal("zweite", s.Get(0, 0)!.Note);
            Assert.Single(s.Cells, c => c.Value.Note is not null);
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0503: OBJ – Obergrenze für Dreiecke und Punkte, genau an der Grenze geht es noch")]
    public void Obj_limit()
    {
        var d = Tmp();
        try
        {
            var f = Path.Combine(d, "a.obj");
            File.WriteAllText(f, "v 0 0 0\nv 1 0 0\nv 1 1 0\nv 0 1 0\nf 1 2 3\nf 1 3 4\n");
            Assert.Equal(2, Mesh3D.ReadMeshFile(f, 2).Triangles);
            Assert.Throws<InvalidDataException>(() => Mesh3D.ReadMeshFile(f, 1));
            File.WriteAllText(f, "v 0 0 0\nv 1 0 0\nv 1 1 0\nv 0 1 0\nf 1 2 3 4\n"); // Vieleck zählt je Dreieck
            Assert.Throws<InvalidDataException>(() => Mesh3D.ReadMeshFile(f, 1));
            File.WriteAllText(f, string.Concat(Enumerable.Repeat("v 0 0 0\n", 7)));
            Assert.Throws<InvalidDataException>(() => Mesh3D.ReadMeshFile(f, 2));
            Assert.True(Mesh3D.MaxFileTriangles >= 5_000_000); // große Scans müssen weiter gehen
        }
        finally { Directory.Delete(d, true); }
    }

    const string CNs = "http://schemas.openxmlformats.org/drawingml/2006/chart";
    const string ANs = "http://schemas.openxmlformats.org/drawingml/2006/main";

    static string Rel(string id, string target) => $"""<Relationship Id="{id}" Target="{target}"/>""";
    static string RelList(IEnumerable<string> rels) => $"""<Relationships xmlns="{RelNs}">{string.Concat(rels)}</Relationships>""";
    static string DrawingOf(IEnumerable<string> ids) => $"""<wsDr xmlns:c="{CNs}" xmlns:r="{R}">{string.Concat(ids.Select(id => $"<c:chart r:id=\"{id}\"/>"))}</wsDr>""";
    static string ChartXml(string title, string formula, string padding = "") => $"""<c:chartSpace xmlns:c="{CNs}" xmlns:a="{ANs}">{padding}<c:chart><c:title><c:tx><c:rich>{title}</c:rich></c:tx></c:title><c:plotArea><c:barChart><c:ser><c:val><c:numRef><c:f>{formula}</c:f></c:numRef></c:val></c:ser></c:barChart></c:plotArea></c:chart></c:chartSpace>""";
    static readonly string SheetWithDrawing = $"""<worksheet xmlns="{S}" xmlns:r="{R}"><sheetData><row r="1"><c r="A1"><v>1</v></c></row></sheetData><drawing r:id="d1"/></worksheet>""";
    static readonly (string, string) DrawingSheetRels = ("xl/worksheets/_rels/sheet1.xml.rels", RelList([Rel("d1", "../drawings/drawing1.xml")]));
    static string Comment(int bytes) => new('a', bytes);

    /// <summary>Mappe mit einer Zeichnung auf einem Blatt.</summary>
    static string WithDrawing(string dir, string drawing, string drawingRels, params (string, string)[] more) =>
        Zip(dir, [("xl/workbook.xml", SheetsXml(1)), ("xl/_rels/workbook.xml.rels", Rels), ("xl/worksheets/sheet1.xml", SheetWithDrawing), DrawingSheetRels, ("xl/drawings/drawing1.xml", drawing), ("xl/drawings/_rels/drawing1.xml.rels", drawingRels), .. more]);

    /// <summary>Viele eigene Blätter, die alle dieselbe Nebendatei benutzen.</summary>
    static string ManySheets(string dir, int count, string sheet, Func<int, string> sheetRels, params (string, string)[] more)
    {
        var entries = new List<(string, string)>
        {
            ("xl/workbook.xml", $"""<workbook xmlns="{S}" xmlns:r="{R}"><sheets>{string.Concat(Enumerable.Range(1, count).Select(i => $"<sheet name=\"B{i}\" r:id=\"s{i}\"/>"))}</sheets></workbook>"""),
            ("xl/_rels/workbook.xml.rels", RelList(Enumerable.Range(1, count).Select(i => Rel($"s{i}", $"worksheets/sheet{i}.xml")))),
        };
        for (var i = 1; i <= count; i++) { entries.Add(($"xl/worksheets/sheet{i}.xml", sheet)); entries.Add(($"xl/worksheets/_rels/sheet{i}.xml.rels", sheetRels(i))); }
        entries.AddRange(more);
        return Zip(dir, [.. entries]);
    }

    static void AssertRejected(string file) => Assert.Contains("Sicherheitsgründen", Assert.Throws<InvalidDataException>(() => SheetFiles.ReadXlsx(file)).Message);

    [Fact(DisplayName = "T-0504: Diagramme – normale Datei: Art, Bereich, Titel und mehrere Blätter wie bisher")]
    public void Charts_normal_roundtrip()
    {
        var d = Tmp();
        try
        {
            var doc = new SheetDoc();
            var sh = doc.Sheets[0];
            sh.Set(0, 0, "Monat"); sh.Set(0, 1, "Wert"); sh.Set(1, 0, "Jan"); sh.Set(1, 1, "4"); sh.Set(2, 0, "Feb"); sh.Set(2, 1, "5");
            sh.Charts.Add(new SheetChart(ChartKind.Linie, "A1:B3", "Verlauf & mehr"));
            sh.Charts.Add(new SheetChart(ChartKind.Kreis, "A1:B3", "Anteile"));
            var f = Path.Combine(d, "normal.xlsx");
            SheetFiles.WriteXlsx(doc, f);
            var back = SheetFiles.ReadXlsx(f).Sheets[0].Charts;
            Assert.Equal([new SheetChart(ChartKind.Linie, "A1:B3", "Verlauf & mehr"), new SheetChart(ChartKind.Kreis, "A1:B3", "Anteile")], back);
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0504: Diagramme – mehrteiliger Titel, fehlender Titel, unbekannte Beziehung")]
    public void Charts_edge_cases()
    {
        var d = Tmp();
        try
        {
            var f = WithDrawing(d, DrawingOf(["c1", "gibtsnicht", "c2"]), RelList([Rel("c1", "../charts/chart1.xml"), Rel("c1", "../charts/chart2.xml"), Rel("c2", "../charts/chart2.xml")]),
                ("xl/charts/chart1.xml", ChartXml("<a:p><a:r><a:t>Teil </a:t></a:r><a:r><a:t>zwei</a:t></a:r></a:p>", "'Blatt'!$C$2:$D$9")),
                ("xl/charts/chart2.xml", ChartXml("", "$A$1")));
            Assert.Equal([new SheetChart(ChartKind.Balken, "C2:D9", "Teil zwei"), new SheetChart(ChartKind.Balken, "A1:A1", "Diagramm")], SheetFiles.ReadXlsx(f).Sheets[0].Charts);
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0504: Diagramme – viele Anker auf dasselbe Ziel laden es nur einmal")]
    public void Charts_same_target_loaded_once()
    {
        const int Anchors = 600; // 600 × 1 MB läge über dem Budget, wenn jeder Anker neu lesen würde
        var d = Tmp();
        try
        {
            var f = WithDrawing(d, DrawingOf(Enumerable.Repeat("c1", Anchors)), RelList([Rel("c1", "../charts/chart1.xml")]),
                ("xl/charts/chart1.xml", ChartXml("<a:t>T</a:t>", "$A$1", $"<!--{Comment(1024 * 1024)}-->")));
            var charts = SheetFiles.ReadXlsx(f).Sheets[0].Charts;
            Assert.Equal(Anchors, charts.Count);
            Assert.All(charts, c => Assert.Equal("T", c.Title));
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0504: Diagramme – riesige Beziehungsliste wird nicht je Anker durchsucht")]
    public void Charts_relationship_lookup_is_fast()
    {
        const int Relations = 500_000; const int Anchors = OfficeZip.MaxEntries;
        var d = Tmp();
        try
        {
            var ids = Enumerable.Range(0, Relations).Select(i => $"r{i}").ToList();
            var f = WithDrawing(d, DrawingOf(ids[^Anchors..]), RelList(ids.Select(id => Rel(id, "../charts/chart1.xml"))), ("xl/charts/chart1.xml", ChartXml("<a:t>T</a:t>", "$A$1")));
            var sw = System.Diagnostics.Stopwatch.StartNew();
            Assert.Equal(Anchors, SheetFiles.ReadXlsx(f).Sheets[0].Charts.Count);
            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(30), $"Dauer: {sw.Elapsed}");
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0504: Diagramme – Anzahl der Anker ist je Mappe begrenzt, die Grenze selbst geht noch")]
    public void Charts_anchor_limit()
    {
        var d = Tmp();
        try
        {
            string Make(int n) => WithDrawing(d, DrawingOf(Enumerable.Repeat("c1", n)), RelList([Rel("c1", "../charts/chart1.xml")]), ("xl/charts/chart1.xml", ChartXml("<a:t>T</a:t>", "$A$1")));
            Assert.Equal(OfficeZip.MaxEntries, SheetFiles.ReadXlsx(Make(OfficeZip.MaxEntries)).Sheets[0].Charts.Count);
            AssertRejected(Make(OfficeZip.MaxEntries + 1));
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0504: Diagramme – Anker zählen über alle Blätter zusammen")]
    public void Charts_anchor_limit_across_sheets()
    {
        var d = Tmp();
        try
        {
            var f = ManySheets(d, 2, SheetWithDrawing, _ => RelList([Rel("d1", "../drawings/drawing1.xml")]),
                ("xl/drawings/drawing1.xml", DrawingOf(Enumerable.Repeat("c1", OfficeZip.MaxEntries / 2 + 1))),
                ("xl/drawings/_rels/drawing1.xml.rels", RelList([Rel("c1", "../charts/chart1.xml")])),
                ("xl/charts/chart1.xml", ChartXml("<a:t>T</a:t>", "$A$1")));
            AssertRejected(f);
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0504: Budget – viele Blätter lesen dieselbe Notizdatei nicht beliebig oft")]
    public void Budget_repeated_notes()
    {
        var d = Tmp();
        try
        {
            var f = ManySheets(d, 600, OneCell, _ => RelList([$"""<Relationship Id="c" Type="http://x/comments" Target="../comments1.xml"/>"""]),
                ("xl/comments1.xml", $"""<comments xmlns="{S}"><!--{Comment(1024 * 1024)}--><commentList/></comments>"""));
            AssertRejected(f);
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0504: Budget – viele Blätter lesen dieselbe Zeichnung nicht beliebig oft")]
    public void Budget_repeated_drawing()
    {
        var d = Tmp();
        try
        {
            var f = ManySheets(d, 600, SheetWithDrawing, _ => RelList([Rel("d1", "../drawings/drawing1.xml")]),
                ("xl/drawings/drawing1.xml", $"<wsDr><!--{Comment(1024 * 1024)}--></wsDr>"));
            AssertRejected(f);
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0504: Budget – mehrere Blätter mit derselben Notizdatei gehen unterhalb der Grenze weiter")]
    public void Budget_shared_notes_below_limit()
    {
        var d = Tmp();
        try
        {
            var f = ManySheets(d, 3, OneCell, _ => RelList([$"""<Relationship Id="c" Type="http://x/comments" Target="../comments1.xml"/>"""]),
                ("xl/comments1.xml", $"""<comments xmlns="{S}"><commentList><comment ref="A1"><text><t>Notiz</t></text></comment></commentList></comments>"""));
            Assert.All(SheetFiles.ReadXlsx(f).Sheets, s => Assert.Equal("Notiz", s.Get(0, 0)!.Note));
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0504: Diagramme – kaputte Zeichnung kostet nur die Diagramme, nicht die Zellen")]
    public void Charts_broken_drawing_keeps_cells()
    {
        var d = Tmp();
        try
        {
            var f = WithDrawing(d, "<wsDr><kaputt>", RelList([]));
            var s = SheetFiles.ReadXlsx(f).Sheets[0];
            Assert.Equal("1", s.Raw(0, 0));
            Assert.Empty(s.Charts);
        }
        finally { Directory.Delete(d, true); }
    }

    static string Nest(int depth, string open, string close, string inner) => string.Concat(Enumerable.Repeat(open, depth)) + inner + string.Concat(Enumerable.Repeat(close, depth));

    [Fact(DisplayName = "T-0504: Diagramme – verschachtelte Titel und Bezüge zählen einmal")]
    public void Charts_nested_text_once()
    {
        const int Depth = 200;
        var d = Tmp();
        try
        {
            var f = WithDrawing(d, DrawingOf(["c1"]), RelList([Rel("c1", "../charts/chart1.xml")]),
                ("xl/charts/chart1.xml", ChartXml(Nest(Depth, "<a:t>", "</a:t>", "Titel"), Nest(Depth, "<c:f>", "</c:f>", "$B$2:$C$4"))));
            Assert.Equal([new SheetChart(ChartKind.Balken, "B2:C4", "Titel")], SheetFiles.ReadXlsx(f).Sheets[0].Charts);
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0504: Diagramme – extrem tiefes XML beendet das Programm nicht und kostet nur die Diagramme")]
    public void Charts_extreme_depth_does_not_crash()
    {
        const int Depth = 1_000_000;
        var d = Tmp();
        try
        {
            var f = WithDrawing(d, DrawingOf(["c1"]), RelList([Rel("c1", "../charts/chart1.xml")]),
                ("xl/charts/chart1.xml", ChartXml(Nest(Depth, "<a:t>", "</a:t>", "Titel"), "$A$1")));
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var s = SheetFiles.ReadXlsx(f).Sheets[0];
            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(30), $"Dauer: {sw.Elapsed}");
            Assert.Equal("1", s.Raw(0, 0));
            Assert.Empty(s.Charts);
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0504: Excel – tief verschachtelte Blatt-Abschnitte werden abgelehnt, übliche Tiefe geht weiter")]
    public void Worksheet_section_depth_limit()
    {
        static string Sheet(int depth) => $"""<worksheet xmlns="{S}"><sheetData/><conditionalFormatting sqref="A1"><cfRule type="cellIs" operator="equal"><formula>{Nest(depth, "<x>", "</x>", "7")}</formula></cfRule></conditionalFormatting></worksheet>""";
        var d = Tmp();
        try
        {
            var ok = Xlsx(d, Sheet(100));
            Assert.Equal(7, SheetFiles.ReadXlsx(ok).Sheets[0].CondRules.Single().V1);
            File.Delete(ok);
            var bad = Xlsx(d, Sheet(1_000_000));
            var sw = System.Diagnostics.Stopwatch.StartNew();
            AssertRejected(bad);
            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(30), $"Dauer: {sw.Elapsed}");
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0504: Excel – überlange Beziehungsziele werden nicht je Blatt oder Anker verarbeitet")]
    public void Oversized_relationship_targets_are_ignored()
    {
        var huge = new string('x', 9 * 1024 * 1024); // unter der Schwelle der ZIP-Bomben-Prüfung
        var d = Tmp();
        try
        {
            var f = Zip(d,
                ("xl/workbook.xml", $"""<workbook xmlns="{S}" xmlns:r="{R}"><sheets>{string.Concat(Enumerable.Range(0, OfficeZip.MaxEntries).Select(i => $"<sheet name=\"B{i}\" r:id=\"lang\"/>"))}</sheets></workbook>"""),
                ("xl/_rels/workbook.xml.rels", RelList([Rel("lang", huge)])),
                ("xl/worksheets/sheet1.xml", OneCell));
            var sw = System.Diagnostics.Stopwatch.StartNew();
            Assert.Equal(OfficeZip.MaxEntries, SheetFiles.ReadXlsx(f).Sheets.Count);
            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(30), $"Dauer: {sw.Elapsed}");
        }
        finally { Directory.Delete(d, true); }
    }

    const string Tri = "v 0 0 0\nv 1 0 0\nv 1 1 0\n";

    [Fact(DisplayName = "T-0504: OBJ-Teile – Gesamtgrenze über alle Teile, genau an der Grenze geht es noch")]
    public void Obj_parts_total_limit()
    {
        var d = Tmp();
        try
        {
            var f = Path.Combine(d, "a.obj");
            File.WriteAllText(f, Tri + "o A\nf 1 2 3\no B\nf 1 2 3\no C\nf 1 2 3\n");
            Assert.Equal(3, Mesh3D.ImportParts(f, "cm", 3).Count);
            Assert.Throws<InvalidOperationException>(() => Mesh3D.ImportParts(f, "cm", 2));
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0504: OBJ-Teile – die Grenze greift beim Einlesen, nicht erst am Dateiende")]
    public void Obj_parts_limit_stops_reading()
    {
        var d = Tmp();
        try
        {
            var f = Path.Combine(d, "a.obj");
            // Die Zeile „v a b c“ hinter der Grenze wäre ein Zahlenfehler: Er darf nicht mehr erreicht werden.
            File.WriteAllText(f, Tri + "o A\nf 1 2 3\no B\nf 1 2 3\nv a b c\n");
            Assert.Throws<InvalidOperationException>(() => Mesh3D.ImportParts(f, "cm", 1));
            File.WriteAllText(f, string.Concat(Enumerable.Repeat("v 0 0 0\n", 7)) + "v a b c\n");
            Assert.Throws<InvalidOperationException>(() => Mesh3D.ImportParts(f, "cm", 2)); // höchstens 3 Punkte je erlaubtes Dreieck
            var sb = new StringBuilder(Tri);
            for (var i = 0; i <= Mesh3D.MaxTriangles; i++) sb.Append("f 1 2 3\n");
            File.WriteAllText(f, sb.Append("v a b c\n").ToString());
            Assert.Throws<InvalidOperationException>(() => Mesh3D.ImportParts(f, "cm")); // Grenze je Teil ebenfalls beim Einlesen
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0504: OBJ-Teile – ein riesiges Vieleck zählt beim Einlesen je Dreieck")]
    public void Obj_parts_big_polygon()
    {
        var d = Tmp();
        try
        {
            var f = Path.Combine(d, "a.obj");
            File.WriteAllText(f, "v 0 0 0\nv 1 0 0\nv 1 1 0\nv 0 1 0\nf 1 2 3 4\nv a b c\n");
            Assert.Throws<InvalidOperationException>(() => Mesh3D.ImportParts(f, "cm", 1));
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0504: OBJ-Teile – Anzahl der Teile ist begrenzt, auch über Farben")]
    public void Obj_parts_count_limit()
    {
        var d = Tmp();
        try
        {
            var f = Path.Combine(d, "a.obj");
            var sb = new StringBuilder(Tri);
            for (var i = 0; i <= Mesh3D.MaxImportParts + 1; i++) sb.Append("o T\nf 1 2 3\n"); // ein Teil zählt, sobald das nächste beginnt
            File.WriteAllText(f, sb.Append("v a b c\n").ToString());
            Assert.Throws<InvalidOperationException>(() => Mesh3D.ImportParts(f));

            File.WriteAllText(Path.Combine(d, "c.mtl"), string.Concat(Enumerable.Range(0, 256).SelectMany(r => Enumerable.Range(0, 256).Select(g => $"newmtl m{r}_{g}\nKd {r / 255.0:0.####} {g / 255.0:0.####} 0\n".Replace(',', '.')))));
            var colors = new StringBuilder("mtllib c.mtl\n" + Tri);
            for (var part = 0; part < 2; part++) // zwei Teile mit je 65.536 Farben = 131.072 Einzelteile
            {
                colors.Append("o T\n");
                for (var r = 0; r < 256; r++) for (var g = 0; g < 256; g++) colors.Append($"usemtl m{r}_{g}\nf 1 2 3\n");
            }
            File.WriteAllText(f, colors.ToString());
            Assert.Throws<InvalidOperationException>(() => Mesh3D.ImportParts(f));
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0504: OBJ-Teile – Materialdatei darf auch hinter den ersten Flächen genannt sein")]
    public void Obj_parts_late_mtllib()
    {
        var d = Tmp();
        try
        {
            File.WriteAllText(Path.Combine(d, "a.mtl"), "newmtl rot\nKd 1 0 0\n");
            var f = Path.Combine(d, "a.obj");
            File.WriteAllText(f, Tri + "usemtl rot\nf 1 2 3\nmtllib a.mtl\n");
            Assert.Equal("#FF0000", Mesh3D.ImportParts(f, "mm").Single().Color);
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0502: OBJ – Teile, Materialien und kaputte Flächen")]
    public void Obj_parts()
    {
        var d = Tmp();
        try
        {
            File.WriteAllText(Path.Combine(d, "a.mtl"), "newmtl rot\nKd 1 0 0\n");
            var f = Path.Combine(d, "a.obj");
            File.WriteAllText(f, "mtllib a.mtl\nv 0 0 0\nv 1 0 0\nv 1 1 0\nv 0 1 0\no   Erstes Teil  \nusemtl rot\nf 1 2 3\ng\nf 1 3 4\nf a b c\nf 1 2 999\n");
            var parts = Mesh3D.ImportParts(f, "mm");
            Assert.Equal(["Erstes Teil", "Teil"], parts.Select(p => p.Name));
            Assert.All(parts, p => Assert.Equal("#FF0000", p.Color));
        }
        finally { Directory.Delete(d, true); }
    }
}
