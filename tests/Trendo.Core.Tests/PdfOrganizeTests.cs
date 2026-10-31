using PdfSharp.Pdf;
using Trendo.Core.Sys;
using Xunit;

namespace Trendo.Core.Tests;

/// T-0303: PDF – Seiten ordnen (Reihenfolge, Drehen, leere Seite, Seiten aus einer anderen PDF), Vorlage bleibt unverändert.
public class PdfOrganizeTests
{
    /// <summary>PDF mit Seiten der angegebenen Breiten (Punkte) – so lässt sich jede Seite wiedererkennen.</summary>
    public static void Make(string file, params double[] widths)
    {
        using var d = new PdfDocument();
        foreach (var w in widths) { var p = d.AddPage(); p.Width = PdfSharp.Drawing.XUnit.FromPoint(w); p.Height = PdfSharp.Drawing.XUnit.FromPoint(600); }
        d.Save(file);
    }

    [Fact(DisplayName = "T-0303: Neue Reihenfolge mit Drehung, leerer Seite (Größe der Seite davor) und Seite aus zweiter PDF; Vorlagen unverändert; Fehlerfälle")]
    public void Rearrange()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-pdforg-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        try
        {
            var a = Path.Combine(dir, "a.pdf"); var b = Path.Combine(dir, "b.pdf"); Make(a, 200, 300, 400); Make(b, 500);
            var before = File.ReadAllBytes(a); var o = Path.Combine(dir, "neu.pdf");
            var n = PdfTools.Rearrange([new(a, 2, 90), new(a, 0), new("", -1), new(b, 0), new(a, 1, 270)], o);
            Assert.Equal(5, n);
            var info = PdfTools.Read(o);
            Assert.Equal([400.0, 200, 200, 500, 300], info.PageSizes.Select(s => Math.Round(Math.Min(s.Width, s.Height)))); // kürzere Kante = gesetzte Breite (Höhe ist überall 600), gleich ob gedreht
            Assert.Equal(90, PdfTools.PageRotation(o, 0)); Assert.Equal(0, PdfTools.PageRotation(o, 1)); Assert.Equal(270, PdfTools.PageRotation(o, 4));
            Assert.Equal(before, File.ReadAllBytes(a));
            Assert.Throws<ArgumentException>(() => PdfTools.Rearrange([new(a, 0)], a));
            Assert.Throws<ArgumentException>(() => PdfTools.Rearrange([], Path.Combine(dir, "x.pdf")));
            Assert.Throws<ArgumentException>(() => PdfTools.Rearrange([new(a, 7)], Path.Combine(dir, "y.pdf")));
            Assert.Throws<ArgumentException>(() => PdfTools.Rearrange([new(a, 0, 45)], Path.Combine(dir, "z.pdf")));
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }
}
