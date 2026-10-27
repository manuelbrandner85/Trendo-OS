using System.Diagnostics;
using System.IO.Compression;
using Trendo.Core.Office;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0228 Stufe D: Diagramme (E2), PDF (E3).</summary>
public class SheetChartPdfTests
{
    static string Temp() { var d = Path.Combine(Path.GetTempPath(), "trendo-sheetd-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

    static SheetDoc WithCharts()
    {
        var d = SheetTemplates.Create("haushalt"); var s = d.Sheets[0];
        s.Charts.Add(new SheetChart(ChartKind.Balken, "A1:C7", "Betrag"));
        s.Charts.Add(new SheetChart(ChartKind.Kreis, "A1:C7", "Anteile"));
        s.Charts.Add(new SheetChart(ChartKind.Linie, "A1:C7", "Verlauf"));
        return d;
    }

    [Fact(DisplayName = "T-0228 E2: Diagrammdaten – Kopfzeile wird Reihenname, erste Spalte Beschriftung, Textspalten sind keine Reihe")]
    public void Chart_data()
    {
        var d = WithCharts(); var s = d.Sheets[0];
        var cd = ChartData.From(s, new SheetCalc(d), "A1:C7");
        Assert.Equal(["Gehalt", "Miete", "Strom", "Lebensmittel", "Handy", "Versicherungen"], cd.Labels);
        var only = Assert.Single(cd.Series);
        Assert.Equal("Betrag", only.Name);
        Assert.Equal([2450, 780, 65, 320, 15, 110], only.Values);
        Assert.Equal(500, ChartData.NiceStep(2450));
    }

    [Fact(DisplayName = "T-0228 E2: Diagramme werden als echte Excel-Diagramme in XLSX gespeichert und wieder gelesen")]
    public void Charts_roundtrip_xlsx()
    {
        var dir = Temp();
        try
        {
            var f = Path.Combine(dir, "mit-diagramm.xlsx");
            SheetFiles.Save(WithCharts(), f);
            using (var z = ZipFile.OpenRead(f))
            {
                Assert.NotNull(z.GetEntry("xl/drawings/drawing1.xml"));
                var chart = new StreamReader(z.GetEntry("xl/charts/chart1.xml")!.Open()).ReadToEnd();
                Assert.Contains("<c:barChart>", chart);
                Assert.Contains("'Haushalt'!$C$2:$C$7", chart);
                Assert.Contains("'Haushalt'!$A$2:$A$7", chart);
            }
            var back = SheetFiles.Open(f).Sheets[0].Charts;
            Assert.Equal([ChartKind.Balken, ChartKind.Kreis, ChartKind.Linie], back.Select(c => c.Kind));
            Assert.Equal(["Betrag", "Anteile", "Verlauf"], back.Select(c => c.Title));
            Assert.All(back, c => Assert.Equal("A1:C7", c.Range));
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }

    [Fact(DisplayName = "T-0228 E3: PDF – A4 hoch/quer, Seitenzahlen, lange Listen auf mehreren Seiten, kein Programmname im Dokument")]
    public void Pdf_export()
    {
        var dir = Temp();
        try
        {
            var d = WithCharts();
            var f = Path.Combine(dir, "haushalt.pdf");
            var pages = SheetPdf.Export(d, d.Sheets[0], f);
            Assert.True(pages >= 1);
            var bytes = File.ReadAllBytes(f);
            Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
            Assert.DoesNotContain("Trendo", System.Text.Encoding.Latin1.GetString(bytes));
            // lange Liste: mehrere Seiten, quer weniger als hoch
            var big = new SheetDoc(); var s = big.Sheets[0];
            for (var r = 0; r < 200; r++) { s.Set(r, 0, "Zeile " + (r + 1)); s.Set(r, 1, (r * 3).ToString()); }
            var hoch = SheetPdf.Export(big, s, Path.Combine(dir, "hoch.pdf"));
            var quer = SheetPdf.Export(big, s, Path.Combine(dir, "quer.pdf"), new SheetPdf.Options(Landscape: true));
            Assert.True(hoch >= 3, $"hoch: {hoch}"); Assert.True(quer > hoch, $"quer {quer} hoch {hoch}");
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }

    static string? Soffice()
    {
        foreach (var root in new[] { Environment.GetEnvironmentVariable("TRENDO_WERKZEUGE"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TrendoWerkzeuge") })
            if (root is not null && Path.Combine(root, "libreoffice", "App", "libreoffice", "program", "soffice.exe") is var p && File.Exists(p)) return p;
        return null;
    }

    [Fact(DisplayName = "T-0228 E2: LibreOffice Calc erkennt Trendos Diagramme (öffnet XLSX, speichert XLSX, Diagramme bleiben) (nur mit LibreOffice)")]
    public void Charts_in_libreoffice()
    {
        if (Soffice() is not { } soffice) return;
        var dir = Temp();
        try
        {
            var src = Path.Combine(dir, "diagramm.xlsx"); SheetFiles.Save(WithCharts(), src);
            var outDir = Path.Combine(dir, "out"); Directory.CreateDirectory(outDir);
            var psi = new ProcessStartInfo(soffice) { UseShellExecute = false, CreateNoWindow = true };
            foreach (var x in new[] { "-env:UserInstallation=" + new Uri(Path.Combine(dir, "profil")).AbsoluteUri, "--headless", "--norestore", "--convert-to", "xlsx:Calc MS Excel 2007 XML", "--outdir", outDir, src }) psi.ArgumentList.Add(x);
            using (var p = Process.Start(psi)!) Assert.True(p.WaitForExit(180_000), "LibreOffice antwortet nicht");
            var back = SheetFiles.Open(Path.Combine(outDir, "diagramm.xlsx")).Sheets[0].Charts;
            Assert.Equal([ChartKind.Balken, ChartKind.Kreis, ChartKind.Linie], back.Select(c => c.Kind));
            Assert.Contains(back, c => c.Title == "Betrag");
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }
}
