using System.Diagnostics;
using System.IO.Compression;
using System.Text.RegularExpressions;
using Trendo.Core.Office;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0228 E4: weitere Funktionen (Mathematik, Statistik, Text, Nachschlagen, Datum, Logik, Info, Finanzen).</summary>
public class SheetFunctionTests
{
    /// <summary>Kleine Beispieltabelle: A1:C6 mit Kopf, Namen, Abteilungen, Beträgen; E1:E5 Zahlen; Datumswerte in G1:G2.</summary>
    static (SheetDoc D, Sheet S, SheetCalc C) Data()
    {
        var d = new SheetDoc(); var s = d.Sheets[0];
        string[][] rows = [["Name", "Abteilung", "Betrag"], ["Anna", "Verkauf", "120"], ["Ben", "Technik", "80"], ["Cem", "Verkauf", "200"], ["Dora", "Technik", "50"], ["Eva", "Verwaltung", "150"]];
        for (var r = 0; r < rows.Length; r++) for (var c = 0; c < 3; c++) s.Set(r, c, rows[r][c]);
        foreach (var (i, v) in new[] { (0, "2"), (1, "4"), (2, "4"), (3, "5"), (4, "9") }) s.Set(i, 4, v);
        s.Set("G1", "15.01.2026"); s.Set("G2", "10.03.2027"); s.Set("G3", "06.04.2026");
        foreach (var (i, v) in new[] { (0, "-1000"), (1, "300"), (2, "400"), (3, "500") }) s.Set(i, 8, v); // I1:I4 Zahlungsreihe
        return (d, s, new SheetCalc(d) { Now = new DateTime(2026, 10, 27) });
    }

    /// <summary>Formel → erwartete Anzeige (Standardformat, deutsch).</summary>
    public static readonly (string Formula, string Expected)[] Cases =
    [
        // Mathematik
        ("=SUMMEWENNS(C2:C6;B2:B6;\"Verkauf\";C2:C6;\">100\")", "320"),
        ("=ZÄHLENWENNS(B2:B6;\"Technik\";C2:C6;\"<60\")", "1"),
        ("=MITTELWERTWENNS(C2:C6;B2:B6;\"Technik\")", "65"),
        ("=MAXWENNS(C2:C6;B2:B6;\"Verkauf\")", "200"),
        ("=MINWENNS(C2:C6;B2:B6;\"Verkauf\")", "120"),
        ("=SUMMENPRODUKT(E1:E3;E3:E5)", "64"),
        ("=QUADRATESUMME(3;4)", "25"),
        ("=OBERGRENZE(4,3;0,5)", "4,5"), ("=UNTERGRENZE(4,7;0,5)", "4,5"), ("=VRUNDEN(10;3)", "9"),
        ("=KÜRZEN(-4,7)", "-4"), ("=KÜRZEN(3,14159;2)", "3,14"), ("=VORZEICHEN(-3)", "-1"),
        ("=RUNDEN(EXP(1);4)", "2,7183"), ("=LN(1)", "0"), ("=LOG(8;2)", "3"), ("=LOG10(1000)", "3"),
        ("=GERADE(3)", "4"), ("=UNGERADE(4)", "5"), ("=FAKULTÄT(5)", "120"), ("=GGT(12;18)", "6"), ("=KGV(4;6)", "12"),
        ("=QUOTIENT(7;2)", "3"), ("=RUNDEN(SIN(PI()/2);6)", "1"), ("=RUNDEN(GRAD(PI());6)", "180"), ("=RÖMISCH(2026)", "MMXXVI"), ("=KOMBINATIONEN(5;2)", "10"),
        // Statistik
        ("=KGRÖSSTE(C2:C6;2)", "150"), ("=KKLEINSTE(C2:C6;1)", "50"), ("=RANG(150;C2:C6)", "2"), ("=RANG.GLEICH(50;C2:C6;1)", "1"),
        ("=STABW(E1:E5)", "2,588435821"), ("=STABW.N(E1:E5)", "2,315167381"), ("=VARIANZ(E1:E5)", "6,7"), ("=VAR.P(E1:E5)", "5,36"),
        ("=MODALWERT(E1:E5)", "4"), ("=QUANTIL(E1:E5;0,25)", "4"), ("=QUARTILE(E1:E5;3)", "5"), ("=MITTELABW(E1:E5)", "1,76"),
        ("=RUNDEN(GEOMITTEL(E1:E5);6)", "4,282255"), ("=KORREL(E1:E5;E1:E5)", "1"), ("=STEIGUNG(E1:E5;E1:E5)", "1"), ("=ACHSENABSCHNITT(E1:E5;E1:E5)", "0"),
        ("=MAXA(C1:C6)", "200"), ("=MITTELWERTA(E1:E5)", "4,8"),
        // Text
        ("=TEXT(1234,5;\"#.##0,00\")", "1.234,50"), ("=TEXT(G1;\"TT.MM.JJJJ\")", "15.01.2026"), ("=TEXT(G1;\"TTTT\")", "Donnerstag"), ("=TEXT(0,25;\"0%\")", "25%"),
        ("=TEXTKETTE(A2;\"-\";B2)", "Anna-Verkauf"), ("=TEXTVERKETTEN(\", \";WAHR;A2:A4)", "Anna, Ben, Cem"),
        ("=WECHSELN(\"a-b-c\";\"-\";\"+\")", "a+b+c"), ("=WECHSELN(\"a-b-c\";\"-\";\"+\";2)", "a-b+c"), ("=ERSETZEN(\"Hallo\";1;1;\"B\")", "Ballo"),
        ("=FINDEN(\"l\";\"Hallo\")", "3"), ("=SUCHEN(\"L*O\";\"Hallo\")", "3"), ("=WIEDERHOLEN(\"ab\";3)", "ababab"), ("=GROSS2(\"erika muster\")", "Erika Muster"),
        ("=WERT(\"1.234,5\")", "1234,5"), ("=IDENTISCH(\"a\";\"A\")", "FALSCH"), ("=ZEICHEN(65)", "A"), ("=CODE(\"A\")", "65"), ("=FEST(1234,567;1)", "1.234,6"),
        ("=ZAHLENWERT(\"2.500,5\";\",\";\".\")", "2500,5"),
        // Nachschlagen
        ("=INDEX(A1:C6;3;1)", "Ben"), ("=VERGLEICH(\"Cem\";A1:A6;0)", "4"), ("=XVERWEIS(\"Dora\";A2:A6;C2:C6)", "50"),
        ("=XVERWEIS(\"Zoe\";A2:A6;C2:C6;\"fehlt\")", "fehlt"), ("=XVERGLEICH(\"Eva\";A2:A6)", "5"), ("=WVERWEIS(\"Betrag\";A1:C6;2;FALSCH)", "120"),
        ("=VERWEIS(4,5;E1:E5)", "4"), ("=WAHL(2;\"a\";\"b\";\"c\")", "b"), ("=ZEILE(C5)", "5"), ("=SPALTE(C5)", "3"), ("=ZEILEN(A1:C6)", "6"), ("=SPALTEN(A1:C6)", "3"),
        ("=INDIREKT(\"C4\")", "200"), ("=ADRESSE(2;3)", "$C$2"), ("=ADRESSE(2;3;4)", "C2"),
        // Datum
        ("=DATEDIF(G1;G2;\"Y\")", "1"), ("=DATEDIF(G1;G2;\"M\")", "13"), ("=DATEDIF(G1;G2;\"MD\")", "23"),
        ("=TEXT(ARBEITSTAG(G1;5);\"TT.MM.JJJJ\")", "22.01.2026"), ("=NETTOARBEITSTAGE(G1;G3)", "58"),
        ("=TEXT(EDATUM(G1;1);\"TT.MM.JJJJ\")", "15.02.2026"), ("=TEXT(MONATSENDE(G1;1);\"TT.MM.JJJJ\")", "28.02.2026"),
        ("=KALENDERWOCHE(G1;2)", "3"), ("=ISOKALENDERWOCHE(G1)", "3"), ("=STUNDE(0,75)", "18"), ("=MINUTE(ZEIT(9;41;0))", "41"),
        ("=TAGE(G2;G1)", "419"), ("=TAGE360(G1;G3)", "81"), ("=RUNDEN(BRTEILJAHRE(G1;G2);6)", "1,152778"),
        // Logik und Info
        ("=WENNS(C2>100;\"hoch\";WAHR;\"niedrig\")", "hoch"), ("=ERSTERWERT(B3;\"Verkauf\";1;\"Technik\";2;0)", "2"), ("=XODER(WAHR;FALSCH)", "WAHR"),
        ("=WENNNV(SVERWEIS(\"x\";A1:C6;2;FALSCH);\"nicht da\")", "nicht da"), ("=ISTTEXT(A2)", "WAHR"), ("=ISTNV(NV())", "WAHR"), ("=ISTGERADE(4)", "WAHR"),
        ("=TYP(A2)", "2"), ("=N(WAHR)", "1"),
        // Finanzen
        ("=RUNDEN(RMZ(0,05/12;60;20000);2)", "-377,42"), ("=RUNDEN(ZW(0,03;10;-1000);2)", "11463,88"), ("=RUNDEN(BW(0,04;5;-1000);2)", "4451,82"),
        ("=RUNDEN(ZZR(0,01;-100;1000);4)", "10,5886"), ("=RUNDEN(ZINS(60;-377,42;20000)*12;4)", "0,05"), ("=RUNDEN(NBW(0,1;I2:I4)+I1;2)", "-21,04"),
        ("=RUNDEN(IKV(I1:I4);6)", "0,088963"), ("=RUNDEN(ZINSZ(0,05/12;1;60;20000);2)", "-83,33"), ("=RUNDEN(KAPZ(0,05/12;1;60;20000);2)", "-294,09"),
        ("=RUNDEN(EFFEKTIV(0,05;12);6)", "0,051162"), ("=LIA(10000;1000;5)", "1800"), ("=DIA(10000;1000;5;1)", "3000"), ("=GDA(10000;1000;5;1)", "4000"),
    ];

    [Fact(DisplayName = "T-0228 E4: über 100 weitere Funktionen rechnen wie Excel/Calc")]
    public void All_cases()
    {
        var fails = new List<string>();
        foreach (var (f, expected) in Cases)
        {
            var (_, s, c) = Data(); s.Set("K1", f);
            var got = c.Display(s, 0, 10);
            if (got != expected) fails.Add($"{f} → {got} (erwartet {expected})");
        }
        Assert.True(fails.Count == 0, string.Join("\n", fails));
    }

    [Fact(DisplayName = "T-0228 E4: Funktionsumfang – mindestens 150 Funktionen, deutsche Namen eindeutig; neue Excel-Funktionen mit _xlfn. gespeichert")]
    public void Names_and_xlsx()
    {
        Assert.True(Formula.German.Count >= 150, "nur " + Formula.German.Count);
        Assert.Equal(Formula.German.Count, Formula.German.Values.Distinct().Count());
        Assert.Equal("_xlfn.XLOOKUP(A1,B1:B3,C1:C3)", Formula.ToExcel("XVERWEIS(A1;B1:B3;C1:C3)"));
        Assert.Equal("XVERWEIS(A1;B1:B3;C1:C3)", Formula.FromExcel("_xlfn.XLOOKUP(A1,B1:B3,C1:C3)"));
        Assert.Equal("SUMIFS(C2:C6,B2:B6,\"x\")", Formula.ToExcel("SUMMEWENNS(C2:C6;B2:B6;\"x\")"));
    }

    /// <summary>Die von Calc berechneten Ergebnisse einer Spalte direkt aus content.xml (Zeile → Wert).</summary>
    static Dictionary<int, CellValue> OdsResults(string ods, int col)
    {
        System.Xml.Linq.XNamespace T = "urn:oasis:names:tc:opendocument:xmlns:table:1.0", O = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";
        using var z = ZipFile.OpenRead(ods);
        var doc = System.Xml.Linq.XDocument.Load(z.GetEntry("content.xml")!.Open());
        var res = new Dictionary<int, CellValue>(); var row = 0;
        foreach (var tr in doc.Descendants(T + "table").First().Descendants(T + "table-row"))
        {
            var rep = (int?)tr.Attribute(T + "number-rows-repeated") ?? 1; var c = 0;
            foreach (var tc in tr.Elements().Where(e => e.Name == T + "table-cell" || e.Name == T + "covered-table-cell"))
            {
                var cr = (int?)tc.Attribute(T + "number-columns-repeated") ?? 1;
                if (col >= c && col < c + cr && tc.Attribute(T + "formula") is not null)
                {
                    var type = (string?)tc.Attribute(O + "value-type");
                    res[row] = type switch
                    {
                        "float" or "percentage" or "currency" => CellValue.Num(double.Parse((string)tc.Attribute(O + "value")!, System.Globalization.CultureInfo.InvariantCulture)),
                        "date" => CellValue.Num(SheetFormat.ToSerial(DateTime.Parse((string)tc.Attribute(O + "date-value")!, System.Globalization.CultureInfo.InvariantCulture))),
                        "boolean" => CellValue.Bool((string?)tc.Attribute(O + "boolean-value") == "true"),
                        _ => CellValue.Str((string?)tc.Attribute(O + "string-value") ?? string.Concat(tc.Descendants().Where(e => e.Name.LocalName == "p").Select(p => p.Value))),
                    };
                }
                c += cr;
            }
            row += rep;
        }
        return res;
    }

    static string? Soffice()
    {
        foreach (var root in new[] { Environment.GetEnvironmentVariable("TRENDO_WERKZEUGE"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TrendoWerkzeuge") })
            if (root is not null && Path.Combine(root, "libreoffice", "App", "libreoffice", "program", "soffice.exe") is var p && File.Exists(p)) return p;
        return null;
    }

    [Fact(DisplayName = "T-0228 E4: Abgleich mit LibreOffice Calc – Calc rechnet alle Formeln selbst nach (ohne gespeicherte Ergebnisse) (nur mit LibreOffice)")]
    public void Same_as_libreoffice()
    {
        if (Soffice() is not { } soffice) return;
        var dir = Path.Combine(Path.GetTempPath(), "trendo-fn-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        try
        {
            // Zufall und Bezüge auf die eigene Zelle auslassen; alle übrigen Formeln je Zeile in Spalte K
            var cases = Cases.Where(x => !x.Formula.Contains("ZUFALL")).ToList();
            var (d, s, c) = Data();
            for (var i = 0; i < cases.Count; i++) s.Set(10 + i, 10, cases[i].Formula);
            var src = Path.Combine(dir, "fn.xlsx"); SheetFiles.Save(d, src);
            // gespeicherte Ergebnisse entfernen, damit Calc selbst rechnen muss
            using (var z = ZipFile.Open(src, ZipArchiveMode.Update))
            {
                var e = z.GetEntry("xl/worksheets/sheet1.xml")!; string xml; using (var r = new StreamReader(e.Open())) xml = r.ReadToEnd();
                xml = Regex.Replace(xml, "(</f>)<v>[^<]*</v>", "$1"); xml = xml.Replace(" t=\"str\"", "").Replace(" t=\"b\"><f>", "><f>");
                e.Delete(); using var w = new StreamWriter(z.CreateEntry("xl/worksheets/sheet1.xml").Open()); w.Write(xml);
            }
            var outDir = Path.Combine(dir, "out"); Directory.CreateDirectory(outDir);
            var psi = new ProcessStartInfo(soffice) { UseShellExecute = false, CreateNoWindow = true };
            foreach (var x in new[] { "-env:UserInstallation=" + new Uri(Path.Combine(dir, "profil")).AbsoluteUri, "--headless", "--norestore", "--convert-to", "ods", "--outdir", outDir, src }) psi.ArgumentList.Add(x);
            using (var p = Process.Start(psi)!) Assert.True(p.WaitForExit(180_000), "LibreOffice antwortet nicht");
            var calcValues = OdsResults(Path.Combine(outDir, "fn.ods"), 10);
            var fails = new List<string>();
            for (var i = 0; i < cases.Count; i++)
            {
                var lo = calcValues.GetValueOrDefault(10 + i, CellValue.Empty); var ours = c.Value(s, 10 + i, 10);
                if (ours.IsError && lo.Kind == CellValue.K.Text && lo.Text.StartsWith('#') || ours.IsError && lo.IsEmpty) continue; // Fehlerwerte stehen in ODS nur als Text
                var same = lo.Kind == ours.Kind && (lo.IsNumber ? Math.Abs(lo.Number - ours.Number) <= 1e-6 * Math.Max(1, Math.Abs(ours.Number)) : lo.ToString() == ours.ToString())
                    || lo.IsNumber && ours.Kind == CellValue.K.Bool && lo.Number == ours.Number;
                if (!same) fails.Add($"{cases[i].Formula}: Calc {lo} ({lo.Kind}) · Trendo {ours} ({ours.Kind})");
            }
            Assert.True(fails.Count == 0, $"{fails.Count} Abweichungen:\n" + string.Join("\n", fails));
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }
}
