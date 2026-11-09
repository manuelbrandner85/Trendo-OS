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
