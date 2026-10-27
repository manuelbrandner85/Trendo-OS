using System.Diagnostics;
using Trendo.Core.Office;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0228 E5–E9: bedingte Formatierung, Auswahllisten, Als Tabelle formatieren, Duplikate, Text in Spalten, Blitzvorschau, Notizen, Blattschutz.</summary>
public class SheetExtrasTests
{
    static string Temp() { var d = Path.Combine(Path.GetTempPath(), "trendo-sheete-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

    static SheetDoc Sample()
    {
        var d = SheetTemplates.Create("haushalt"); var s = d.Sheets[0];
        s.CondRules.Add(new CondRule("C2:C7", CondKind.Regel, CondOp.Groesser, 500, Fill: "#FFFDE2E2"));
        s.CondRules.Add(new CondRule("C2:C7", CondKind.Regel, CondOp.Zwischen, 10, 100, "#FFD1FAE5"));
        s.CondRules.Add(new CondRule("B2:B7", CondKind.Regel, CondOp.Enthaelt, Fill: "#FFFFF3B0", Text: "Einnahme"));
        s.CondRules.Add(new CondRule("C9:C11", CondKind.Farbskala));
        s.CondRules.Add(new CondRule("C2:C7", CondKind.Datenbalken, Fill: "#FF63A0F0"));
        s.Validations.Add(new ValidationRule("B2:B20", ["Einnahme", "Ausgabe"]));
        s.Get("A2").Note = "Netto, ohne Weihnachtsgeld";
        s.Get("C2").Unlocked = true;
        s.Protected = true;
        return d;
    }

    [Fact(DisplayName = "T-0228 E5: Regeln färben passende Zellen, Farbskala Rot→Grün, Datenbalken anteilig")]
    public void Conditional()
    {
        var d = Sample(); var s = d.Sheets[0]; var c = new SheetCalc(d);
        Assert.Equal("#FFFDE2E2", SheetExtras.Look(s, c, 1, 2).Fill);   // 2450 > 500
        Assert.Equal("#FFD1FAE5", SheetExtras.Look(s, c, 3, 2).Fill);   // 65 zwischen 10 und 100
        Assert.Null(SheetExtras.Look(s, c, 4, 2).Fill);                 // 320
        Assert.Equal("#FFFFF3B0", SheetExtras.Look(s, c, 1, 1).Fill);   // „Einnahme“
        Assert.Equal(1, SheetExtras.Look(s, c, 1, 2).Bar);              // größter Wert = voller Balken
        Assert.True(SheetExtras.Look(s, c, 5, 2).Bar < 0.01);
        Assert.Equal("#FF63BE7B", SheetExtras.Look(s, c, 8, 2).Fill);   // Einnahmen = größter Wert → grün
        Assert.Equal("#FFF8696B", SheetExtras.Scale(0));
    }

    [Fact(DisplayName = "T-0228 E6/E7/E8: Auswahlliste prüft Eingaben; Als Tabelle mit Summenzeile; Duplikate; Text in Spalten; Blitzvorschau")]
    public void Operations()
    {
        var d = Sample(); var s = d.Sheets[0]; var c = new SheetCalc(d);
        var rule = SheetExtras.ValidationFor(s, 3, 1);
        Assert.True(SheetExtras.Allowed(rule, "ausgabe")); Assert.False(SheetExtras.Allowed(rule, "Sonstiges")); Assert.True(SheetExtras.Allowed(rule, ""));
        Assert.Null(SheetExtras.ValidationFor(s, 3, 0));

        var t = new SheetDoc(); var ts = t.Sheets[0]; var tc = new SheetCalc(t);
        string[][] rows = [["Name", "Wert"], ["A", "1"], ["B", "2"], ["A", "1"], ["C", "3"]];
        for (var r = 0; r < rows.Length; r++) for (var k = 0; k < 2; k++) ts.Set(r, k, rows[r][k]);
        var total = SheetExtras.FormatAsTable(ts, tc, 0, 0, 4, 1, totals: true);
        Assert.Equal(5, total); Assert.Equal("=SUMME(B2:B5)", ts.Raw(5, 1)); Assert.Equal("Summe", ts.Raw(5, 0));
        Assert.True(ts.Get(0, 0)!.Bold); Assert.NotNull(ts.Get(2, 0)!.Fill); Assert.Null(ts.Get(1, 0)!.Fill);
        Assert.Equal(1, SheetExtras.RemoveDuplicates(ts, new SheetCalc(t), 1, 0, 4, 1));
        Assert.Equal(["A", "B", "C", ""], Enumerable.Range(1, 4).Select(r => ts.Raw(r, 0)));

        var x = new SheetDoc(); var xs = x.Sheets[0];
        xs.Set("A1", "Erika Muster; Hauptstraße 1; 10115 Berlin");
        Assert.Equal(3, SheetExtras.TextToColumns(xs, 0, 0, 0, ';'));
        Assert.Equal(["Erika Muster", "Hauptstraße 1", "10115 Berlin"], new[] { xs.Raw(0, 0), xs.Raw(0, 1), xs.Raw(0, 2) });

        var f = new SheetDoc(); var fs = f.Sheets[0];
        string[] names = ["Erika Muster", "Max Mustermann", "Lena Beispiel", "Tom Probe"];
        for (var r = 0; r < names.Length; r++) fs.Set(r, 0, names[r]);
        fs.Set("B1", "Muster");
        Assert.Equal(3, SheetExtras.FlashFill(fs, new SheetCalc(f), 0, 3, 1));
        Assert.Equal(["Muster", "Mustermann", "Beispiel", "Probe"], Enumerable.Range(0, 4).Select(r => fs.Raw(r, 1)));
        fs.Set("C1", "EM");
        SheetExtras.FlashFill(fs, new SheetCalc(f), 0, 3, 2);
        Assert.Equal("LB", fs.Raw(2, 2));
    }

    [Fact(DisplayName = "T-0228 E5/E6/E9: Regeln, Auswahllisten, Notizen und Blattschutz bleiben in XLSX erhalten; Notiz ohne Autorname")]
    public void Xlsx_roundtrip()
    {
        var dir = Temp();
        try
        {
            var f = Path.Combine(dir, "extras.xlsx"); SheetFiles.Save(Sample(), f);
            using (var z = System.IO.Compression.ZipFile.OpenRead(f))
            {
                var cm = new StreamReader(z.GetEntry("xl/comments1.xml")!.Open()).ReadToEnd();
                Assert.Contains("<author></author>", cm); Assert.DoesNotContain("Trendo", cm);
                Assert.NotNull(z.GetEntry("xl/drawings/vmlDrawing1.vml"));
            }
            Check(SheetFiles.Open(f).Sheets[0]);
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }

    static void Check(Sheet b)
    {
        Assert.True(b.Protected);
        Assert.True(b.Get(1, 2)!.Unlocked); Assert.False(b.Get(1, 1)?.Unlocked ?? false);
        Assert.Equal("Netto, ohne Weihnachtsgeld", b.Get(1, 0)!.Note);
        var v = Assert.Single(b.Validations); Assert.Equal(["Einnahme", "Ausgabe"], v.Items);
        Assert.Contains(b.CondRules, r => r.Kind == CondKind.Regel && r.Op == CondOp.Groesser && r.V1 == 500 && r.Range == "C2:C7");
        Assert.Contains(b.CondRules, r => r.Op == CondOp.Zwischen && r.V1 == 10 && r.V2 == 100);
        Assert.Contains(b.CondRules, r => r.Op == CondOp.Enthaelt && r.Text == "Einnahme");
        Assert.Contains(b.CondRules, r => r.Kind == CondKind.Farbskala && r.Range == "C9:C11");
        Assert.Contains(b.CondRules, r => r.Kind == CondKind.Datenbalken);
    }

    static string? Soffice()
    {
        foreach (var root in new[] { Environment.GetEnvironmentVariable("TRENDO_WERKZEUGE"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TrendoWerkzeuge") })
            if (root is not null && Path.Combine(root, "libreoffice", "App", "libreoffice", "program", "soffice.exe") is var p && File.Exists(p)) return p;
        return null;
    }

    [Fact(DisplayName = "T-0228 E5/E6/E9: LibreOffice Calc übernimmt Regeln, Auswahllisten, Notizen und Blattschutz (öffnet XLSX, speichert XLSX) (nur mit LibreOffice)")]
    public void Libreoffice_keeps_extras()
    {
        if (Soffice() is not { } soffice) return;
        var dir = Temp();
        try
        {
            var src = Path.Combine(dir, "extras.xlsx"); SheetFiles.Save(Sample(), src);
            var outDir = Path.Combine(dir, "out"); Directory.CreateDirectory(outDir);
            var psi = new ProcessStartInfo(soffice) { UseShellExecute = false, CreateNoWindow = true };
            foreach (var x in new[] { "-env:UserInstallation=" + new Uri(Path.Combine(dir, "profil")).AbsoluteUri, "--headless", "--norestore", "--convert-to", "xlsx:Calc MS Excel 2007 XML", "--outdir", outDir, src }) psi.ArgumentList.Add(x);
            using (var p = Process.Start(psi)!) Assert.True(p.WaitForExit(180_000), "LibreOffice antwortet nicht");
            Check(SheetFiles.Open(Path.Combine(outDir, "extras.xlsx")).Sheets[0]);
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }
}
