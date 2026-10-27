using System.Diagnostics;
using System.IO.Compression;
using Trendo.Core.Office;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0228 Stufe B (C1): XLSX hin und zurück, ODS lesen, CSV ein und aus, Abgleich mit LibreOffice Calc.</summary>
public class SheetFilesTests
{
    static string Temp() { var d = Path.Combine(Path.GetTempPath(), "trendo-sheet-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

    /// <summary>Beispielmappe mit allem, was Stufe 1 kann.</summary>
    public static SheetDoc Sample()
    {
        var d = new SheetDoc(); var s = d.Sheets[0]; s.Name = "Haushalt";
        s.Set("A1", "Posten"); s.Set("B1", "Betrag"); s.Set("C1", "Datum"); s.Set("D1", "Anteil");
        foreach (var c in new[] { "A1", "B1", "C1", "D1" }) { s.Get(c).Bold = true; s.Get(c).Fill = "#DDEBF7"; s.Get(c).Border = true; }
        (string, string, string)[] rows = [("Miete", "850", "01.10.2026"), ("Strom", "72,40", "05.10.2026"), ("Internet", "39,99", "12.10.2026"), ("Versicherung", "61,25", "15.10.2026")];
        for (var i = 0; i < rows.Length; i++)
        {
            var r = i + 1;
            s.Set(r, 0, rows[i].Item1); s.Set(r, 1, rows[i].Item2); s.GetOrAdd(r, 1).Format = NumFmt.Euro;
            s.Set(r, 2, rows[i].Item3); s.GetOrAdd(r, 2).Format = NumFmt.Datum;
            s.Set(r, 3, $"=B{r + 1}/B$6"); s.GetOrAdd(r, 3).Format = NumFmt.Prozent;
        }
        s.Set("A6", "Summe"); s.Get("A6").Bold = true; s.Set("B6", "=SUMME(B2:B5)"); s.Get("B6").Format = NumFmt.Euro; s.Get("B6").Bold = true;
        s.Set("A7", "Teuerster"); s.Set("B7", "=SVERWEIS(MAX(B2:B5);B2:B5;1;FALSCH)");
        s.Set("A8", "Hinweis"); s.Set("B8", "=WENN(B6>1000;\"zu hoch\";\"ok\")");
        s.Set("A9", "'0049 123"); s.Set("A10", "Text mit ; und \"Zitat\"");
        s.ColWidths[0] = 18; s.ColWidths[1] = 12.5;
        var t2 = d.AddSheet("Notizen"); t2.Set("A1", "=Haushalt!B6*12"); t2.Get("A1").Format = NumFmt.Euro; t2.Set("A2", "Jahreskosten");
        return d;
    }

    static void SameContent(SheetDoc a, SheetDoc b, bool styles = true)
    {
        Assert.Equal(a.Sheets.Select(s => s.Name), b.Sheets.Select(s => s.Name));
        var ca = new SheetCalc(a); var cb = new SheetCalc(b);
        foreach (var sa in a.Sheets)
        {
            var sb = b[sa.Name];
            foreach (var ((r, c), cell) in sa.Cells)
            {
                var name = $"{sa.Name}!{Addr.Name(r, c)}";
                if (styles) Assert.True(ca.Display(sa, r, c) == cb.Display(sb, r, c), $"{name}: „{ca.Display(sa, r, c)}“ ≠ „{cb.Display(sb, r, c)}“");
                else
                {
                    var (va, vb) = (ca.Value(sa, r, c), cb.Value(sb, r, c));
                    Assert.True(va.Kind == vb.Kind && (va.IsNumber ? Math.Abs(va.Number - vb.Number) < 1e-9 : va.Text == vb.Text), $"{name}: {va} ≠ {vb}");
                }
                var other = sb.Get(r, c) ?? new Cell();
                if (cell.IsFormula) Assert.Equal(cell.Raw, other.Raw);
                if (!styles) continue;
                Assert.True(cell.Bold == other.Bold, name + " fett");
                Assert.True(cell.Fill == other.Fill, name + " Füllfarbe");
                Assert.True(cell.Border == other.Border, name + " Rahmen");
                Assert.True(cell.Format == other.Format, $"{name} Format {cell.Format} ≠ {other.Format}");
            }
        }
    }

    [Fact(DisplayName = "Tabelle C1: XLSX speichern und wieder öffnen – Werte, Formeln, Formate, Fett, Farbe, Rahmen, Breiten, Blätter")]
    public void Xlsx_roundtrip()
    {
        var dir = Temp();
        try
        {
            var a = Sample(); a.Sheets[0].HiddenRows.Add(3);
            var f = Path.Combine(dir, "haushalt.xlsx");
            SheetFiles.Save(a, f);
            var b = SheetFiles.Open(f);
            SameContent(a, b);
            Assert.Equal(18, b.Sheets[0].ColWidth(0));
            Assert.Contains(3, b.Sheets[0].HiddenRows);
            Assert.Equal("1.023,64 €", new SheetCalc(b).Display(b.Sheets[0], 5, 1));
            // so steht es in der Datei: englische Formel, Ergebnis zwischengespeichert, kein Autor
            using var z = ZipFile.OpenRead(f);
            var sheet1 = new StreamReader(z.GetEntry("xl/worksheets/sheet1.xml")!.Open()).ReadToEnd();
            Assert.Contains("<f>SUM(B2:B5)</f><v>1023.64</v>", sheet1);
            Assert.Contains("<f>IF(B6&gt;1000,&quot;zu hoch&quot;,&quot;ok&quot;)</f>", sheet1);
            Assert.Contains("<dc:creator></dc:creator>", new StreamReader(z.GetEntry("docProps/core.xml")!.Open()).ReadToEnd());
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "Tabelle C1: CSV ein (Semikolon/Komma/Tab erkannt, Anführungszeichen) und aus (wie Excel deutsch)")]
    public void Csv()
    {
        var dir = Temp();
        try
        {
            var f = Path.Combine(dir, "liste.csv");
            File.WriteAllText(f, "Name;Betrag;Notiz\r\nMiete;850;\"mit ; Semikolon\"\r\nStrom;72,40;\"zwei\nZeilen\"\r\n=1+1;3;x\r\n");
            var d = SheetFiles.Open(f); var s = d.Sheets[0];
            Assert.Equal("mit ; Semikolon", s.Raw(1, 2));
            Assert.Equal("zwei\nZeilen", s.Raw(2, 2));
            Assert.Equal("'=1+1", s.Raw(3, 0)); // Formeln aus CSV werden nicht ausgeführt
            Assert.Equal(72.4, new SheetCalc(d).Value(s, 2, 1).Number);
            File.WriteAllText(f, "a,b\n1,2\n");
            Assert.Equal("2", SheetFiles.Open(f).Sheets[0].Raw(1, 1));
            var o = Path.Combine(dir, "aus.csv");
            SheetFiles.Save(Sample(), o);
            var text = File.ReadAllText(o);
            Assert.Contains("Summe;1.023,64 €", text);
            Assert.Contains("\"Text mit ; und \"\"Zitat\"\"\"", text);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "Tabelle C1: ODS-Formeln von LibreOffice verstehen")]
    public void Ods_formulas()
    {
        Assert.Equal("SUMME(A1:A3;Kosten!B2)", Formula.FromOds("of:=SUM([.A1:.A3];[Kosten.B2])"));
        Assert.Equal("WENN(B6>1000;\"zu; hoch\";2,5)", Formula.FromOds("of:=IF([.B6]>1000;\"zu; hoch\";2.5)"));
        Assert.Equal("'Mein Blatt'!$A$1*2", Formula.FromOds("of:=[$'Mein Blatt'.$A$1]*2"));
    }

    static string? Soffice()
    {
        foreach (var root in new[] { Environment.GetEnvironmentVariable("TRENDO_WERKZEUGE"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TrendoWerkzeuge") })
            if (root is not null && Path.Combine(root, "libreoffice", "App", "libreoffice", "program", "soffice.exe") is var p && File.Exists(p)) return p;
        return null;
    }

    [Fact(DisplayName = "Tabelle C1: Abgleich mit LibreOffice Calc – Calc öffnet Trendos XLSX, speichert ODS und XLSX, Trendo liest beides gleich (nur mit LibreOffice)")]
    public void LibreOffice_roundtrip()
    {
        if (Soffice() is not { } soffice) return; // ohne mitgeliefertes LibreOffice nicht prüfbar
        var dir = Temp();
        try
        {
            var a = Sample();
            var src = Path.Combine(dir, "trendo.xlsx"); SheetFiles.Save(a, src);
            foreach (var (ext, filter) in new[] { ("ods", "calc8"), ("xlsx", "Calc MS Excel 2007 XML") })
            {
                var outDir = Path.Combine(dir, ext); Directory.CreateDirectory(outDir);
                var profile = new Uri(Path.Combine(dir, "profil-" + ext)).AbsoluteUri;
                var psi = new ProcessStartInfo(soffice) { UseShellExecute = false, CreateNoWindow = true };
                foreach (var x in new[] { "-env:UserInstallation=" + profile, "--headless", "--norestore", "--convert-to", $"{ext}:{filter}", "--outdir", outDir, src }) psi.ArgumentList.Add(x);
                using (var p = Process.Start(psi)!) Assert.True(p.WaitForExit(180_000), "LibreOffice antwortet nicht");
                var back = SheetFiles.Open(Path.Combine(outDir, "trendo." + ext));
                // Calc speichert Zahlenformate eigener Art – Werte und Formeln müssen gleich sein, Fett/Farbe/Rahmen ebenso (bei ODS liest Trendo nur Werte)
                SameContent(a, back, styles: ext == "xlsx");
            }
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }
}
