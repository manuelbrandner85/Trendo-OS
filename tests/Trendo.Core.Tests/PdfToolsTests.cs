using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using Trendo.Core;
using Trendo.Core.Sys;
using Xunit;
namespace Trendo.Core.Tests;

public class T0223_TrendoPdf
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-pdf-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

    /// <summary>PDF mit n Seiten; Seite k ist (200 + k) Punkte breit – so erkennt der Test jede Seite wieder.</summary>
    static string Make(string dir, string name, int n, int offset = 0)
    {
        var f = Path.Combine(dir, name);
        using var d = new PdfDocument();
        for (var k = 1; k <= n; k++) { var p = d.AddPage(); p.Width = PdfSharp.Drawing.XUnit.FromPoint(200 + offset + k); p.Height = PdfSharp.Drawing.XUnit.FromPoint(300); }
        d.Save(f);
        return f;
    }

    /// <summary>Für den Abgleich mit qpdf (reports/t0223-office): TRENDO_PDF_KEEP=Ordner behält die erzeugten Dateien.</summary>
    static void Keep(string d, string name)
    {
        if (Environment.GetEnvironmentVariable("TRENDO_PDF_KEEP") is not { Length: > 0 } keep) return;
        var target = Path.Combine(keep, name); Directory.CreateDirectory(target);
        foreach (var f in Directory.GetFiles(d, "*.pdf", SearchOption.AllDirectories)) File.Copy(f, Path.Combine(target, Path.GetFileName(f)), true);
    }

    static int[] Widths(string f) => PdfTools.Read(f).PageSizes.Select(s => (int)Math.Round(s.Width)).ToArray();

    [Fact(DisplayName = "T-0223 P3: Seitenangabe wie qpdf – 1-3,5,8-z, z, r1, rückwärts, Fehler verständlich")]
    public void Pages()
    {
        Assert.Equal([0, 1, 2, 4, 7, 8, 9], PdfTools.ParsePages("1-3,5,8-z", 10));
        Assert.Equal([9], PdfTools.ParsePages("z", 10));
        Assert.Equal([9, 8], PdfTools.ParsePages("r1,r2", 10));
        Assert.Equal([4, 3, 2], PdfTools.ParsePages("5-3", 10));
        Assert.Equal([6, 7, 8, 9], PdfTools.ParsePages("7-", 10));
        Assert.Equal(10, PdfTools.ParsePages("", 10).Count);
        Assert.Contains("gibt es nicht", Assert.Throws<ArgumentException>(() => PdfTools.ParsePages("11", 10)).Message);
        Assert.Contains("verstehe ich nicht", Assert.Throws<ArgumentException>(() => PdfTools.ParsePages("a-b", 10)).Message);
    }

    [Fact(DisplayName = "T-0223 P3: Zusammenfügen, Seiten auswählen, Teilen, Drehen – Seiten landen in der richtigen Reihenfolge, Vorlage bleibt")]
    public void Merge_select_split_rotate()
    {
        var d = Tmp();
        try
        {
            var a = Make(d, "a.pdf", 3); var b = Make(d, "b.pdf", 2, offset: 100);
            var before = File.ReadAllBytes(a);
            Assert.Equal(5, PdfTools.Merge([a, b], Path.Combine(d, "ab.pdf")));
            Assert.Equal([201, 202, 203, 301, 302], Widths(Path.Combine(d, "ab.pdf")));
            Assert.Equal(3, PdfTools.SelectPages(Path.Combine(d, "ab.pdf"), "z,1-2", Path.Combine(d, "auswahl.pdf")));
            Assert.Equal([302, 201, 202], Widths(Path.Combine(d, "auswahl.pdf")));
            var parts = PdfTools.Split(Path.Combine(d, "ab.pdf"), Path.Combine(d, "teile"), 2);
            Assert.Equal(["ab-01.pdf", "ab-02.pdf", "ab-03.pdf"], parts.Select(Path.GetFileName));
            Assert.Equal([203, 301], Widths(parts[1]));
            PdfTools.Rotate(a, Path.Combine(d, "gedreht.pdf"), 90, "2");
            using (var r = PdfReader.Open(Path.Combine(d, "gedreht.pdf"), PdfDocumentOpenMode.Import))
                Assert.Equal([0, 90, 0], r.Pages.Cast<PdfPage>().Select(p => p.Rotate));
            Assert.Equal(before, File.ReadAllBytes(a));
            Assert.Throws<ArgumentException>(() => PdfTools.Merge([a, b], a));          // nie die Vorlage überschreiben
            Assert.Throws<ArgumentException>(() => PdfTools.Rotate(a, Path.Combine(d, "x.pdf"), 45));
            Assert.Throws<ArgumentException>(() => PdfTools.Merge([a], Path.Combine(d, "y.pdf")));
            Keep(d, "werkzeuge");
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0223 P4: Passwort setzen und entfernen – ohne Passwort kein Zugriff, Info erkennt den Schutz, Passwort nie im Protokoll")]
    public void Password()
    {
        var d = Tmp();
        try
        {
            var a = Make(d, "a.pdf", 2);
            var audit = new AuditLog(Path.Combine(d, "audit.jsonl"));
            Assert.False(PdfTools.Read(a).Encrypted);
            var enc = Path.Combine(d, "geschuetzt.pdf");
            PdfTools.Encrypt(a, enc, "Geheim-123", audit: audit);
            var info = PdfTools.Read(enc);
            Assert.True(info.Encrypted); Assert.True(info.NeedsPassword); Assert.Equal(0, info.Pages);
            Assert.Equal(2, PdfTools.Read(enc, "Geheim-123").Pages);
            Assert.ThrowsAny<Exception>(() => PdfTools.Decrypt(enc, Path.Combine(d, "falsch.pdf"), "falsch"));
            var open = Path.Combine(d, "offen.pdf");
            PdfTools.Decrypt(enc, open, "Geheim-123", audit);
            Assert.False(PdfTools.Read(open).Encrypted);
            Assert.Equal([201, 202], Widths(open));
            Assert.DoesNotContain("Geheim-123", File.ReadAllText(Path.Combine(d, "audit.jsonl")));
            Keep(d, "passwort");
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0223 P5: Markieren, Unterstreichen, Durchstreichen und Notiz landen als Anmerkung auf der richtigen Seite")]
    public void Annotations()
    {
        var d = Tmp();
        try
        {
            var a = Make(d, "a.pdf", 2);
            var m1 = Path.Combine(d, "m1.pdf"); var m2 = Path.Combine(d, "m2.pdf"); var m3 = Path.Combine(d, "m3.pdf");
            PdfTools.AddMark(a, m1, 1, PdfTools.Mark.Highlight, 20, 200, 120, 14, "wichtig");
            PdfTools.AddMark(m1, m2, 1, PdfTools.Mark.StrikeOut, 20, 180, 80, 12);
            PdfTools.AddNote(m2, m3, 0, 30, 280, "Bitte prüfen\nbis Freitag");
            Assert.Empty(PdfTools.Annotations(a, 1));
            Assert.Equal([("Highlight", (string?)"wichtig"), ("StrikeOut", null)], PdfTools.Annotations(m3, 1));
            Assert.Equal([("FreeText", (string?)"Bitte prüfen\nbis Freitag")], PdfTools.Annotations(m3, 0));
            Assert.Throws<ArgumentException>(() => PdfTools.AddNote(a, Path.Combine(d, "leer.pdf"), 0, 1, 1, " "));
            Keep(d, "anmerkungen");
        }
        finally { Directory.Delete(d, true); }
    }

    /// <summary>PDF mit Formular: Textfeld „Name“, Ankreuzfeld „Zustimmung“.</summary>
    static string Form(string dir)
    {
        var f = Path.Combine(dir, "formular.pdf");
        using var d = new PdfDocument();
        var page = d.AddPage();
        PdfDictionary Widget(string type, string name, PdfItem value)
        {
            var w = new PdfDictionary(d);
            w.Elements["/Type"] = new PdfName("/Annot"); w.Elements["/Subtype"] = new PdfName("/Widget");
            w.Elements["/FT"] = new PdfName(type); w.Elements["/T"] = new PdfString(name); w.Elements["/V"] = value;
            var rect = new PdfArray(d); foreach (var v in new[] { 50.0, 700, 250, 720 }) rect.Elements.Add(new PdfReal(v));
            w.Elements["/Rect"] = rect; w.Elements["/P"] = page.Reference!;
            d.Internals.AddObject(w); return w;
        }
        var name = Widget("/Tx", "Name", new PdfString(""));
        var ok = Widget("/Btn", "Zustimmung", new PdfName("/Off"));
        var ap = new PdfDictionary(d); var n = new PdfDictionary(d); n.Elements["/Yes"] = new PdfDictionary(d); n.Elements["/Off"] = new PdfDictionary(d); ap.Elements["/N"] = n;
        ok.Elements["/AP"] = ap; ok.Elements["/AS"] = new PdfName("/Off");
        var annots = new PdfArray(d); annots.Elements.Add(name.Reference!); annots.Elements.Add(ok.Reference!); page.Elements["/Annots"] = annots;
        var form = new PdfDictionary(d); var fields = new PdfArray(d); fields.Elements.Add(name.Reference!); fields.Elements.Add(ok.Reference!);
        form.Elements["/Fields"] = fields; d.Internals.AddObject(form); d.Internals.Catalog.Elements["/AcroForm"] = form.Reference!;
        d.Save(f);
        return f;
    }

    [Fact(DisplayName = "T-0223 P6: Formular – Felder erkennen, ausfüllen, Ankreuzfeld setzen, unbekannte Felder melden")]
    public void Forms()
    {
        var d = Tmp();
        try
        {
            var f = Form(d);
            Assert.Equal([new PdfTools.Field("Name", "Text", ""), new PdfTools.Field("Zustimmung", "Ankreuzfeld", "nein")], PdfTools.Fields(f));
            var outF = Path.Combine(d, "ausgefuellt.pdf");
            var unknown = PdfTools.Fill(f, outF, new Dictionary<string, string> { ["Name"] = "Anna Müller", ["Zustimmung"] = "ja", ["Gibt-es-nicht"] = "x" });
            Assert.Equal(["Gibt-es-nicht"], unknown);
            Assert.Equal([new PdfTools.Field("Name", "Text", "Anna Müller"), new PdfTools.Field("Zustimmung", "Ankreuzfeld", "ja")], PdfTools.Fields(outF));
            Assert.Throws<InvalidOperationException>(() => PdfTools.Fill(Make(d, "ohne.pdf", 1), Path.Combine(d, "z.pdf"), new Dictionary<string, string>()));
            Assert.False(File.Exists(Path.Combine(d, "z.pdf"))); // abgebrochen: keine halbe Datei
            Keep(d, "formular");
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0223 P3: Neue leere PDF hat eine A4-Seite")]
    public void Blank()
    {
        var d = Tmp();
        try
        {
            var f = Path.Combine(d, "neu.pdf"); PdfTools.NewBlank(f);
            var i = PdfTools.Read(f);
            Assert.Equal(1, i.Pages); Assert.Equal(595, (int)Math.Round(i.PageSizes[0].Width)); Assert.Equal(842, (int)Math.Round(i.PageSizes[0].Height));
        }
        finally { Directory.Delete(d, true); }
    }
}
