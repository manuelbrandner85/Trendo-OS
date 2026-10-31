using System.IO.Compression;
using System.Text;
using Trendo.Core;
using Trendo.Core.Sys;
using Xunit;
namespace Trendo.Core.Tests;

public class Sys10_Office
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-s10-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }
    static string? Lo => Cli.Find("soffice") ?? Cli.Find("libreoffice");
    static string ToPdf(string docx)
    {
        var prof = Path.Combine(Path.GetTempPath(), "trendo-lo-" + Guid.NewGuid().ToString("N"));
        var r = Cli.Run(Lo!, ["-env:UserInstallation=" + new Uri(prof).AbsoluteUri, "--headless", "--norestore", "--convert-to", "pdf", "--outdir", Path.GetDirectoryName(docx)!, docx], timeoutMs: 120000);
        return Path.ChangeExtension(docx, ".pdf");
    }

    /// <summary>Kleines klassisches PDF mit Formular (Textfeld „Name“, Kästchen „Zustimmung“).</summary>
    static string FormPdf(string dir)
    {
        var objs = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R /AcroForm 5 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595.28 841.89] /Annots [4 0 R 6 0 R] >>",
            "<< /Type /Annot /Subtype /Widget /FT /Tx /T (Name) /Rect [50 700 300 720] /F 4 >>",
            "<< /Fields [4 0 R 6 0 R] /DA (/Helv 0 Tf 0 g) >>",
            "<< /Type /Annot /Subtype /Widget /FT /Btn /T (Zustimmung) /V /Off /AS /Off /Rect [50 650 70 670] /F 4 /MK << /CA (4) >> >>",
        };
        var sb = new StringBuilder("%PDF-1.4\n"); var offs = new List<int>();
        for (var i = 0; i < objs.Length; i++) { offs.Add(Encoding.Latin1.GetByteCount(sb.ToString())); sb.Append($"{i + 1} 0 obj\n{objs[i]}\nendobj\n"); }
        var x = Encoding.Latin1.GetByteCount(sb.ToString());
        sb.Append($"xref\n0 {objs.Length + 1}\n0000000000 65535 f \n"); foreach (var o in offs) sb.Append($"{o:D10} 00000 n \n");
        sb.Append($"trailer\n<< /Size {objs.Length + 1} /Root 1 0 R >>\nstartxref\n{x}\n%%EOF\n");
        var f = Path.Combine(dir, "formular.pdf"); File.WriteAllBytes(f, Encoding.Latin1.GetBytes(sb.ToString())); return f;
    }

    [Fact(DisplayName = "SYS-10-P02/P11: Neues DOCX/XLSX; Tabelle ohne Office lesen (XLSX + CSV mit ; und Anführungszeichen)")]
    public void NewAndRead()
    {
        var d = Tmp(); var docx = Path.Combine(d, "a.docx"); Ooxml.NewDocx(docx, "Titel <&>", ["Absatz 1", "Absatz 2"]);
        Assert.Equal("Titel <&>\nAbsatz 1\nAbsatz 2", Ooxml.DocxText(docx));
        var xlsx = Path.Combine(d, "t.xlsx"); var rows = new List<IList<string>> { new[] { "Name", "Betrag" } }; for (var i = 0; i < 1200; i++) rows.Add(new[] { "Zeile " + i, i.ToString() });
        Ooxml.NewXlsx(xlsx, rows); var back = Ooxml.ReadXlsx(xlsx);
        Assert.Equal(1000, back.Count); Assert.Equal(["Name", "Betrag"], back[0]); Assert.Equal(["Zeile 998", "998"], back[999]);
        var csv = Csv.Parse("Name;Notiz\n\"Müller, Hans\";\"sagt \"\"Hallo\"\"\nzweite Zeile\"\nB;2\n");
        Assert.Equal(3, csv.Count); Assert.Equal("Müller, Hans", csv[1][0]); Assert.Equal("sagt \"Hallo\"\nzweite Zeile", csv[1][1]);
    }

    [Fact(DisplayName = "SYS-10-P04: Zuletzt verwendet – neueste zuerst")]
    public void Recent()
    {
        var d = Tmp(); for (var i = 0; i < 3; i++) { var f = Path.Combine(d, $"d{i}.docx"); File.WriteAllText(f, "x"); File.SetLastWriteTime(f, DateTime.Now.AddDays(-i)); }
        Assert.Equal(["d0.docx", "d1.docx", "d2.docx"], FileShelf.Recent([d], FileShelf.Office).Select(f => f.Name));
    }

    [NeedsFact("soffice|libreoffice", DisplayName = "SYS-10-P01 (T-0244 getrennt): LibreOffice öffnet ein von Trendo erzeugtes DOCX")]
    public void NewAndRead_real()
    {
        var d = Tmp(); var docx = Path.Combine(d, "a.docx"); Ooxml.NewDocx(docx, "Titel <&>", ["Absatz 1", "Absatz 2"]);
        Assert.True(File.Exists(ToPdf(docx))); // LibreOffice öffnet die Datei
    }

    [NeedsFact("soffice|libreoffice", DisplayName = "SYS-10-P05/P06: Seitenzahl und A4 eines echten LibreOffice-PDFs")]
    public void PdfPages()
    {
        if (Lo is null) return;
        var d = Tmp(); var a = Path.Combine(d, "a.docx"); Ooxml.NewDocx(a, "A", Enumerable.Range(0, 120).Select(i => "Zeile " + i)); var pa = ToPdf(a);
        var b = Path.Combine(d, "b.docx"); Ooxml.NewDocx(b, "B", ["x"]); var pb = ToPdf(b);
        var na = Pdf.Pages(pa); Assert.True(na >= 2); Assert.Equal(1, Pdf.Pages(pb)); Assert.True(Pdf.IsA4(Pdf.MediaBox(pa)));
    }

    [NeedsFact("soffice|libreoffice", "pdftotext", DisplayName = "SYS-10-P07: Text aus einem echten PDF")]
    public void Text()
    {
        if (Lo is null || Cli.Find("pdftotext") is null) return;
        var d = Tmp(); var a = Path.Combine(d, "a.docx"); Ooxml.NewDocx(a, "Rechnung Nummer 4711", ["Betrag 12,50 Euro"]); var pdf = ToPdf(a);
        var (ok, t, _) = PdfText.Extract(pdf, Cli.Find("pdftotext"), null, null); Assert.True(ok); Assert.Contains("4711", t);
    }

    [NeedsFact("soffice|libreoffice", "qpdf|pdfunite", DisplayName = "SYS-10-P17 (T-0244 getrennt): PDF zusammenfügen = Summe der Seiten, Teilen stimmt")]
    public void PdfMergeSplit()
    {
        var d = Tmp(); var a = Path.Combine(d, "a.docx"); Ooxml.NewDocx(a, "A", Enumerable.Range(0, 120).Select(i => "Zeile " + i)); var pa = ToPdf(a);
        var b = Path.Combine(d, "b.docx"); Ooxml.NewDocx(b, "B", ["x"]); var pb = ToPdf(b);
        var na = Pdf.Pages(pa);
        var unite = Cli.Find("pdfunite"); var sep = Cli.Find("pdfseparate"); var qpdf = Cli.Find("qpdf");
        var o = Path.Combine(d, "m.pdf"); var (exe, args) = Pdf.MergeCmd(qpdf, unite, [pa, pb], o); Cli.Run(exe, args);
        Assert.Equal(na + 1, Pdf.Pages(o));
        var s = Path.Combine(d, "s.pdf"); (exe, args) = Pdf.ExtractCmd(qpdf, sep, o, 2, 2, s); Cli.Run(exe, args);
        Assert.Equal(1, Pdf.Pages(qpdf is not null ? s : s.Replace(".pdf", "-2.pdf")));
        Assert.Throws<InvalidOperationException>(() => Pdf.MergeCmd(null, null, [pa], o)); Assert.Equal((2, 5), Pdf.ParseRange("2-5")); Assert.Throws<ArgumentException>(() => Pdf.ParseRange("x"));
    }

    [NeedsFact("soffice|libreoffice", "pdftotext", "tesseract", "pdftoppm", DisplayName = "SYS-10-P08 (T-0244 getrennt): Scan → durchsuchbares PDF mit Tesseract")]
    public void ScanOcr()
    {
        var d = Tmp(); var a = Path.Combine(d, "a.docx"); Ooxml.NewDocx(a, "Rechnung Nummer 4711", ["Betrag 12,50 Euro"]); var pdf = ToPdf(a);
        var tess = Cli.Find("tesseract")!; var ppm = Cli.Find("pdftoppm")!;
        Cli.Run(ppm, ["-r", "150", "-png", "-singlefile", pdf, Path.Combine(d, "scan")]);
        var ob = Path.Combine(d, "scan-durchsuchbar"); Cli.Run(tess, PdfText.ScanToPdfArgs(Path.Combine(d, "scan.png"), ob, "eng", null), timeoutMs: 120000);
        Assert.Contains("4711", PdfText.Extract(ob + ".pdf", Cli.Find("pdftotext"), null, null).Text);
    }

    [Fact(DisplayName = "SYS-10-P10: Serienbrief – N Zeilen = N Dateien, Platzhalter auch über Formatgrenzen")]
    public void Merge()
    {
        var d = Tmp(); var tpl = Path.Combine(d, "vorlage.docx");
        Ooxml.WriteDocxXml(tpl, "<w:p><w:r><w:t>Hallo {{</w:t></w:r><w:r><w:rPr><w:b/></w:rPr><w:t>Name</w:t></w:r><w:r><w:t>}}, Ihr Ort: {{Ort}}</w:t></w:r></w:p>");
        Assert.Equal(["Name", "Ort"], Ooxml.Placeholders(tpl));
        var rows = Csv.Records("Name;Ort\nAnna & Co;Berlin\nBernd;Köln\nCem;Ulm\n");
        var files = Ooxml.MailMerge(tpl, rows, Path.Combine(d, "out"), "Name");
        Assert.Equal(3, files.Count); Assert.Equal("Hallo Anna & Co, Ihr Ort: Berlin", Ooxml.DocxText(files[0])); Assert.Contains("Bernd", Path.GetFileName(files[1]));
        if (Lo is not null) Assert.True(File.Exists(ToPdf(files[0])));
    }

    [Fact(DisplayName = "SYS-10-P12/P21: Protokoll als DOCX + Aufgaben als .ics (VTODO mit Fälligkeit und Erinnerung)")]
    public void MinutesTasks()
    {
        var d = Tmp(); var tl = new TaskList(d);
        var t = tl.Add("Angebot schicken", new DateTime(2026, 11, 2, 9, 0, 0), "Erika"); tl.Add("Später", null);
        Assert.Single(tl.Overdue(new DateTime(2026, 11, 3))); tl.Toggle(t.Id); Assert.Empty(tl.Overdue(new DateTime(2026, 11, 3)));
        var ics = TaskList.ToIcs(tl.All());
        Assert.Contains("BEGIN:VTODO", ics); Assert.Contains("DUE:20261102T090000", ics); Assert.Contains("BEGIN:VALARM", ics); Assert.Contains("STATUS:COMPLETED", ics);
        var (docx, icsf) = TaskList.WriteMinutes(new("Projekt X", new DateTime(2026, 10, 21, 10, 0, 0), "A, B", "Punkt 1", tl.All()), d);
        Assert.Contains("Angebot schicken – Erika – bis 02.11.2026", Ooxml.DocxText(docx)); Assert.Equal(2, File.ReadAllText(icsf).Split("BEGIN:VTODO").Length - 1);
        TaskList.WriteMinutes(new("Projekt X", new DateTime(2026, 10, 21, 10, 0, 0), "A", "neu", []), d); Assert.Single(DocVersions.Versions(docx)); // P19
    }

    [Fact(DisplayName = "SYS-10-P13: Rechtschreibung mit Hunspell-Wörterbuch (Suffixe, Großschreibung, einfache Komposita)")]
    public void Spell()
    {
        var d = Tmp();
        File.WriteAllText(Path.Combine(d, "de.aff"), "SET UTF-8\nSFX N Y 2\nSFX N 0 en .\nSFX N 0 n e\nSFX E Y 1\nSFX E 0 e [^e]\n", new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(d, "de.dic"), "7\nHaus/N\nTür/N\nschön/E\nder\ndas\nist\nGarten/N\n", new UTF8Encoding(false));
        var s = new Speller(Path.Combine(d, "de.dic"), Path.Combine(d, "de.aff"), ["Trendo"]);
        Assert.Empty(s.Mistakes("Der Garten ist schöne. Trendo Gartenhaus Türen"));
        Assert.Equal(["Hauss", "schöon"], s.Mistakes("Das Hauss ist schöon."));
    }

    [Fact(DisplayName = "SYS-10-P14/P15: Wörter, Sätze, Lesbarkeit; Vergleich markiert Änderungen")]
    public void Writing_()
    {
        var st = Writing.Analyze("Das ist ein Satz. Hier kommt noch einer! Ist das leicht?");
        Assert.Equal(11, st.Words); Assert.Equal(3, st.Sentences); Assert.Equal("leicht", st.Level);
        Assert.Equal("sehr schwer", Writing.Analyze("Die Verwaltungsvorschriftenzusammenstellungsverordnung beinhaltet außerordentlich komplizierte Durchführungsbestimmungen bezüglich grundstücksverkehrsrechtlicher Genehmigungsverfahren.").Level);
        var diff = Writing.Diff("a\nb\nc", "a\nB\nc\nd");
        Assert.Equal([(' ', "a"), ('−', "b"), ('+', "B"), (' ', "c"), ('+', "d")], diff);
    }

    [Fact(DisplayName = "SYS-10-P16: Metadaten aus DOCX und PDF entfernen (PDF bleibt gültig)")]
    public void Meta()
    {
        var d = Tmp(); var docx = Path.Combine(d, "m.docx"); Ooxml.NewDocx(docx, "x", ["y"]);
        using (var z = ZipFile.Open(docx, ZipArchiveMode.Update)) { using var w = new StreamWriter(z.CreateEntry("docProps/core.xml").Open()); w.Write("<cp:coreProperties xmlns:cp=\"x\" xmlns:dc=\"y\"><dc:creator>Max Mustermann</dc:creator><cp:lastModifiedBy>Max</cp:lastModifiedBy></cp:coreProperties>"); using var w2 = new StreamWriter(z.CreateEntry("docProps/app.xml").Open()); w2.Write("<Properties><Company>Geheim GmbH</Company></Properties>"); }
        Assert.Equal("Max Mustermann", Ooxml.OfficeMeta(docx)["dc:creator"]);
        var o = Ooxml.StripOfficeMeta(docx); Assert.Empty(Ooxml.OfficeMeta(o)); Assert.Equal("x\ny", Ooxml.DocxText(o)); Assert.Equal("Max Mustermann", Ooxml.OfficeMeta(docx)["dc:creator"]);
        var pdf = Path.Combine(d, "p.pdf"); File.WriteAllBytes(pdf, File.ReadAllBytes(FormPdf(d)));
        var raw = File.ReadAllText(pdf, Encoding.Latin1).Replace("/Root 1 0 R >>", "/Root 1 0 R /Info 7 0 R >>").Replace("xref", "7 0 obj\n<< /Author (Max Mustermann) /Producer (Word) >>\nendobj\nxref");
        File.WriteAllText(pdf, raw, Encoding.Latin1);
        var po = Pdf.StripMeta(pdf); Assert.Equal("", Pdf.Info(po)["Author"]); Assert.Equal(new FileInfo(pdf).Length, new FileInfo(po).Length);
    }

    [Fact(DisplayName = "SYS-10-P19/P24/P09: Versionen vor dem Überschreiben, Export-Paket mit stimmender SHA-Liste, Vorlagen mit Version")]
    public void Versions()
    {
        var d = Tmp(); var f = Path.Combine(d, "brief.txt"); File.WriteAllText(f, "1");
        DocVersions.SafeWrite(f, x => File.WriteAllText(x, "2")); DocVersions.SafeWrite(f, x => File.WriteAllText(x, "3"));
        var v = DocVersions.Versions(f); Assert.Equal(2, v.Count); Assert.EndsWith("brief-v001.txt", v[0]); Assert.Equal("1", File.ReadAllText(v[0]));
        var att = Path.Combine(d, "anlage.pdf"); File.WriteAllText(att, "pdf");
        var z = DocVersions.ExportPackage(f, [att], Path.Combine(d, "p.zip")); Assert.True(DocVersions.VerifyPackage(z));
        using (var za = ZipFile.Open(z, ZipArchiveMode.Update)) { za.GetEntry("Anlagen/anlage.pdf")!.Delete(); using var w = new StreamWriter(za.CreateEntry("Anlagen/anlage.pdf").Open()); w.Write("manipuliert"); }
        Assert.False(DocVersions.VerifyPackage(z));
        DocVersions.AddTemplate(d, "Rechnung", f); DocVersions.AddTemplate(d, "Rechnung", f);
        var t = Assert.Single(DocVersions.Templates(d)); Assert.Equal(2, t.Version); Assert.EndsWith("Rechnung-v002.txt", t.File);
    }

    [NeedsFact("soffice|libreoffice", "pdfinfo", DisplayName = "SYS-10 (T-0244 getrennt): Metadaten aus einem echten LibreOffice-PDF entfernt")]
    public void StripMeta_real()
    {
        var d = Tmp();
        var real = Path.Combine(d, "r.docx"); Ooxml.NewDocx(real, "T", ["x"]); var rp = ToPdf(real); var ro = Pdf.StripMeta(rp);
        var info = Cli.Run(Cli.Find("pdfinfo")!, [ro]); Assert.True(info.Ok, info.Err); Assert.DoesNotContain("LibreOffice", info.Out);
    }

    [Fact(DisplayName = "SYS-10-P20: Suche in Dokumenten mit Fundstelle (DOCX, TXT, PDF)")]
    public void Search()
    {
        var d = Tmp(); Ooxml.NewDocx(Path.Combine(d, "a.docx"), "Vertrag", ["Die Kündigungsfrist beträgt drei Monate."]); File.WriteAllText(Path.Combine(d, "b.txt"), "nichts\nauch Kündigungsfrist hier");
        var h = Writing.Search(d, "kündigungsfrist"); Assert.Equal(2, h.Count); Assert.Contains(h, x => x.File.EndsWith("b.txt") && x.Line == 2);
        Assert.Contains("Kündigungsfrist", h[0].Snippet);
    }

    [Fact(DisplayName = "SYS-10-P22: Unterschrift als Bild in DOCX (Kopie, mit Hinweis „nicht rechtsverbindlich“)")]
    public void Signature()
    {
        var d = Tmp(); var docx = Path.Combine(d, "v.docx"); Ooxml.NewDocx(docx, "Vertrag", ["Text"]);
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");
        var o = Ooxml.InsertSignature(docx, png, 100, 40, "Bild-Unterschrift – nicht rechtsverbindlich");
        Assert.Contains("nicht rechtsverbindlich", Ooxml.DocxText(o)); Assert.DoesNotContain("rechtsverbindlich", Ooxml.DocxText(docx));
        using (var z = ZipFile.OpenRead(o)) { Assert.NotNull(z.GetEntry("word/media/trendo-unterschrift.png")); }
        if (Lo is not null) Assert.True(File.Exists(ToPdf(o)));
    }

    [Fact(DisplayName = "SYS-10-P23: PDF-Formular lesen und ausfüllen (Kopie, inkrementell, Original unverändert)")]
    public void Form()
    {
        var d = Tmp(); var f = FormPdf(d); var before = File.ReadAllBytes(f);
        var fields = PdfForm.Fields(f); Assert.Equal(["Name", "Zustimmung"], fields.Select(x => x.Name)); Assert.Equal("Off", fields[1].Value);
        var o = PdfForm.Fill(f, new() { ["Name"] = "Anna (Test)", ["Zustimmung"] = "ja" });
        var back = PdfForm.Fields(o); Assert.Equal("Anna (Test)", back[0].Value); Assert.Equal("Yes", back[1].Value);
        Assert.Equal(before, File.ReadAllBytes(f)); Assert.Contains("/NeedAppearances true", File.ReadAllText(o, Encoding.Latin1));
        if (Cli.Find("pdfinfo") is { } pi) { var r = Cli.Run(pi, [o]); Assert.True(r.Ok, r.Err); Assert.DoesNotContain("rror", r.Err); }
        Assert.Throws<ArgumentException>(() => PdfForm.Fill(f, new() { ["Gibtsnicht"] = "x" }));
    }

    [Fact(DisplayName = "SYS-10-P25: Barrierefreiheits-Bericht listet Mängel")]
    public void A11y()
    {
        var d = Tmp(); var l = Pdf.AccessibilityIssues(FormPdf(d));
        Assert.Contains(l, x => x.Contains("getaggt")); Assert.Contains(l, x => x.Contains("Dokumentsprache")); Assert.Contains(l, x => x.Contains("Dokumenttitel"));
        var ok = Path.Combine(d, "ok.pdf"); File.WriteAllText(ok, "%PDF-1.7\n<< /Type /Catalog /MarkInfo << /Marked true >> /StructTreeRoot 9 0 R /Lang (de-DE) /ViewerPreferences << /DisplayDocTitle true >> >>\n<< /Title (Bericht) >>\n<< /S /Figure /Alt (Logo) >>", Encoding.Latin1);
        Assert.Empty(Pdf.AccessibilityIssues(ok));
    }
}
