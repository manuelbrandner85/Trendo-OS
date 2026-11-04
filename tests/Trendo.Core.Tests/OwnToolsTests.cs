using Trendo.Core;
using Trendo.Core.Sys;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Selbst nachgebaut statt fremder Programme (04.11.2026): PDF zusammenfügen/teilen ohne qpdf, .7z entpacken ohne 7-Zip.</summary>
public class OwnToolsTests
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-eigen-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

    static string MakePdf(string dir, string name, int pages)
    {
        var f = Path.Combine(dir, name);
        using var d = new PdfSharp.Pdf.PdfDocument();
        for (var i = 0; i < pages; i++) d.AddPage();
        d.Save(f);
        return f;
    }

    [Fact(DisplayName = "PDF zusammenfügen und Seiten herausholen ohne qpdf – Seitenzahlen stimmen, falscher Bereich wird erklärt")]
    public void Pdf_merge_extract()
    {
        var d = Tmp();
        try
        {
            var a = MakePdf(d, "a.pdf", 2); var b = MakePdf(d, "b.pdf", 3);
            var m = Path.Combine(d, "m.pdf"); Pdf.Merge([a, b], m);
            Assert.Equal(5, Pdf.Pages(m));
            var x = Path.Combine(d, "x.pdf"); Pdf.Extract(m, 2, 4, x);
            Assert.Equal(3, Pdf.Pages(x));
            Assert.Contains("nur 5 Seiten", Assert.Throws<ArgumentException>(() => Pdf.Extract(m, 2, 9, Path.Combine(d, "y.pdf"))).Message);
            Assert.Throws<ArgumentException>(() => Pdf.Merge([a], m));
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = ".7z entpacken ohne 7-Zip – Dateien und Unterordner kommen an")]
    public void Seven_zip()
    {
        var d = Tmp();
        try
        {
            var f = Path.Combine(d, "t.7z"); File.WriteAllBytes(f, Convert.FromBase64String("N3q8ryccAASmIu8WgwAAAAAAAAAhAAAAAAAAAEuwesMBABdIYWxsbyBUcmVuZG96d2VpdGUgRGF0ZWkAAACBMweuD8/Hr2gP6+qcvzY9/nXb5jZNUqQuvHsB4WTonC//agTPUm22PAHpLIgKNAtSxcYlPP2PotCY1i6bfgvrtzfuT2VTd5YbhDXnHHkz1HaG7cEfpiMbaY66eGTuZlz2WrC3ABcGHAEJZwAHCwEAASMDAQEFXQAQAAAMgJIKATRHa0sAAA=="));
            Assert.Equal(2, Archive7z.Extract(f, Path.Combine(d, "aus")));
            Assert.Equal("Hallo Trendo", File.ReadAllText(Path.Combine(d, "aus", "a.txt")));
            Assert.Equal("zweite Datei", File.ReadAllText(Path.Combine(d, "aus", "unter", "b.txt")));
        }
        finally { Directory.Delete(d, true); }
    }
}
