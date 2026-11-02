using PdfSharp.Pdf.IO;
using Trendo.Core.Office;
using Xunit;
namespace Trendo.Core.Tests;

/// Pilot 0.9.47 (Vorbild Word/Writer): Schriftart, Schriftfarbe, Hervorhebung, Zeilenabstand, Abstand vor, Erstzeileneinzug – im Modell, beim Tippen und im Rundweg DOCX/ODT/PDF.
public class Pilot_TextLook
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-textlook-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

    static TextDoc Sample()
    {
        var p = new Para("Rote Überschrift in Georgia und gelb markiert") { LineSpacing = 1.5, SpaceBeforePt = 12, SpaceAfterPt = 6, FirstLineCm = 1.25 };
        TextDoc.SetInk(p, 0, 4, MarkKind.Color, "#c00000");
        TextDoc.SetInk(p, 5, 11, MarkKind.Font, "Georgia");
        TextDoc.SetInk(p, 30, 4, MarkKind.Highlight, "#FFF59D");
        TextDoc.Format(p, 0, 4, Fmt.Bold, true);
        return new TextDoc { Blocks = [p, new Para("Zweiter Absatz") { FirstLineCm = -1 }] };
    }

    [Fact(DisplayName = "Pilot Text: Schrift, Farbe, Hervorhebung setzen, überschreiben, entfernen; ungültige Farbe abgewiesen")]
    public void Ink_model()
    {
        var p = new Para("Hallo schöne Welt");
        TextDoc.SetInk(p, 0, 17, MarkKind.Color, "#1971c2");
        TextDoc.SetInk(p, 6, 6, MarkKind.Color, "#E03131"); // Mitte umfärben → links/rechts bleiben blau
        Assert.Equal("#1971C2", TextDoc.InkAt(p, 0, MarkKind.Color));
        Assert.Equal("#E03131", TextDoc.InkAt(p, 7, MarkKind.Color));
        Assert.Equal("#1971C2", TextDoc.InkAt(p, 14, MarkKind.Color));
        TextDoc.SetInk(p, 0, 17, MarkKind.Color, null);
        Assert.DoesNotContain(p.Marks, m => m.Kind == MarkKind.Color);
        Assert.Throws<ArgumentException>(() => TextDoc.SetInk(p, 0, 3, MarkKind.Color, "rot"));
        Assert.Throws<ArgumentException>(() => TextDoc.SetInk(p, 0, 3, MarkKind.Font, " "));
        TextDoc.SetInk(p, 0, 5, MarkKind.Font, "Verdana"); TextDoc.SetInk(p, 5, 3, MarkKind.Font, "Verdana");
        Assert.Single(p.Marks, m => m.Kind == MarkKind.Font); // gleiche Schrift nebeneinander = ein Bereich
    }

    [Fact(DisplayName = "Pilot Text: Weitertippen behält Farbe und Schrift, Enter nimmt sie und die Absatzabstände in den neuen Absatz mit")]
    public void Ink_follows_typing()
    {
        var d = new TextDoc { Blocks = [new Para("Gold") { LineSpacing = 1.15, SpaceBeforePt = 6 }] };
        var p = (Para)d.Blocks[0];
        TextDoc.SetInk(p, 0, 4, MarkKind.Color, "#B8860B");
        TextDoc.EditText(p, "Goldglanz");
        Assert.Equal("#B8860B", TextDoc.InkAt(p, 8, MarkKind.Color));
        var next = d.SplitAt(0, 4);
        Assert.Equal("glanz", next.Text); Assert.Equal("#B8860B", TextDoc.InkAt(next, 0, MarkKind.Color));
        Assert.Equal(1.15, next.LineSpacing); Assert.Equal(6, next.SpaceBeforePt);
        d.MergeWithPrevious(1);
        Assert.Equal("#B8860B", TextDoc.InkAt((Para)d.Blocks[0], 8, MarkKind.Color));
        var copy = (Para)p.Copy(); Assert.Equal(p.LineSpacing, copy.LineSpacing); Assert.Equal(p.Marks, copy.Marks);
    }

    [Fact(DisplayName = "Pilot Text: DOCX-Rundweg behält Schrift, Farbe, Hervorhebung, Zeilenabstand, Abstände und Erstzeileneinzug")]
    public void Docx_roundtrip()
    {
        var dir = Tmp();
        try
        {
            var f = Path.Combine(dir, "probe.docx");
            DocxFormat.Write(Sample(), f);
            var back = DocxFormat.Read(f); var p = back.Paras.First();
            Assert.Equal("#C00000", TextDoc.InkAt(p, 1, MarkKind.Color));
            Assert.True(TextDoc.Has(p, 0, 4, Fmt.Bold));
            Assert.Equal("Georgia", TextDoc.InkAt(p, 6, MarkKind.Font));
            Assert.Equal("#FFF59D", TextDoc.InkAt(p, 31, MarkKind.Highlight));
            Assert.Null(TextDoc.InkAt(p, 20, MarkKind.Font));
            Assert.Equal(1.5, p.LineSpacing); Assert.Equal(12, p.SpaceBeforePt); Assert.Equal(6, p.SpaceAfterPt); Assert.Equal(1.25, p.FirstLineCm);
            Assert.Equal(-1, back.Paras.Last().FirstLineCm);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "Pilot Text: ODT-Rundweg behält Schrift, Farbe, Hervorhebung, Zeilenabstand, Abstände und Erstzeileneinzug")]
    public void Odt_roundtrip()
    {
        var dir = Tmp();
        try
        {
            var f = Path.Combine(dir, "probe.odt");
            OdtFormat.Write(Sample(), f);
            var back = OdtFormat.Read(f); var p = back.Paras.First();
            Assert.Equal("#C00000", TextDoc.InkAt(p, 1, MarkKind.Color));
            Assert.True(TextDoc.Has(p, 0, 4, Fmt.Bold));
            Assert.Equal("Georgia", TextDoc.InkAt(p, 6, MarkKind.Font));
            Assert.Equal("#FFF59D", TextDoc.InkAt(p, 31, MarkKind.Highlight));
            Assert.Equal(1.5, p.LineSpacing); Assert.Equal(12, p.SpaceBeforePt); Assert.Equal(1.25, p.FirstLineCm);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "Pilot Text: PDF mit Farben, fremder Schrift und doppeltem Zeilenabstand entsteht; mehr Zeilenabstand braucht mehr Seiten")]
    public void Pdf_export()
    {
        var dir = Tmp();
        try
        {
            TextDoc Long(double ls) => new() { Blocks = Enumerable.Range(0, 60).Select(i => (Trendo.Core.Office.Block)new Para($"Zeile {i} mit etwas Text für die Seite") { LineSpacing = ls }).ToList() };
            var a = Path.Combine(dir, "eng.pdf"); var b = Path.Combine(dir, "weit.pdf"); var c = Path.Combine(dir, "probe.pdf");
            var n1 = TextPdf.Export(Long(1), a); var n2 = TextPdf.Export(Long(2), b);
            Assert.True(n2 > n1, $"doppelter Zeilenabstand: {n2} Seiten, einfach: {n1}");
            Assert.True(TextPdf.Export(Sample(), c) >= 1);
            using var pdf = PdfReader.Open(c, PdfDocumentOpenMode.Import); Assert.True(pdf.PageCount >= 1);
        }
        finally { Directory.Delete(dir, true); }
    }
}
