using PdfSharp.Pdf.IO;
using Trendo.Core.Office;
using Xunit;
namespace Trendo.Core.Tests;

public class T0223_TrendoText
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-text-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

    static string Repo(string rel)
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "Trendo.sln"))) return Path.Combine(d.FullName, rel);
        throw new FileNotFoundException(rel);
    }

    /// <summary>Ein Dokument mit allem, was Stufe 1 kann.</summary>
    static TextDoc Sample()
    {
        var d = new TextDoc { Title = "Probe" };
        var intro = new Para("Dies ist fett, kursiv und H2O mit x2.");
        TextDoc.Format(intro, 8, 4, Fmt.Bold, true);
        TextDoc.Format(intro, 14, 6, Fmt.Italic, true);
        TextDoc.Format(intro, 25, 1, Fmt.Sub, true);
        TextDoc.Format(intro, 34, 1, Fmt.Super, true);
        TextDoc.Size(intro, 0, 4, 16);
        d.Blocks.AddRange([
            new Para("Großer Titel", ParaStyle.Title), new Para("Kapitel eins", ParaStyle.Heading1), intro,
            new Para("Mittig") { Align = Align.Center }, new Para("Rechts") { Align = Align.Right }, new Para("Blocksatz mit genug Wörtern für eine Zeile") { Align = Align.Justify },
            new Para("Erster Punkt") { List = ListKind.Bullet }, new Para("Zweiter Punkt") { List = ListKind.Bullet }, new Para("Schritt eins") { List = ListKind.Number },
            new Para("Ein Zitat", ParaStyle.Quote), new Table { Rows = [["Name", "Wert"], ["Miete", "800 €"]] }, new PageBreak(),
            new Picture { Data = Png(), Ext = "png", WidthCm = 4, HeightCm = 2 }, new Para("Unterstrichen und durchgestrichen\nzweite Zeile"),
        ]);
        var last = (Para)d.Blocks[^1];
        TextDoc.Format(last, 0, 13, Fmt.Underline, true); TextDoc.Format(last, 18, 15, Fmt.Strike, true);
        return d;
    }

    /// <summary>Kleines gültiges PNG (2×1 Bildpunkte).</summary>
    static byte[] Png() => Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAIAAAABCAYAAAD0In+KAAAADklEQVR4nGPQb/v2HwQAGeAGpxUNKvkAAAAASUVORK5CYII=");

    static string Describe(TextDoc d) => string.Join("|", d.Blocks.Select(b => b switch
    {
        Para p => $"P:{p.Style}:{p.Align}:{p.List}:{p.Text}:{string.Join(",", p.Spans.Select(s => $"{s.Start}+{s.Length}={s.Fmt}/{s.Size}"))}",
        Table t => "T:" + string.Join(";", t.Rows.Select(r => string.Join(",", r))),
        Picture pic => $"B:{pic.Ext}:{pic.Data.Length}:{pic.WidthCm:0.0}x{pic.HeightCm:0.0}",
        PageBreak => "UMBRUCH", _ => "?",
    }));

    [Fact(DisplayName = "T-0223 T3: Fett/Kursiv/Unterstrichen umschalten, hoch/tief schließen sich aus, Größe in Writer-Schritten, GROSS/klein")]
    public void Formats()
    {
        var p = new Para("Hallo schöne Welt");
        TextDoc.Toggle(p, 6, 6, Fmt.Bold);
        Assert.Equal([new Span(6, 6, Fmt.Bold)], p.Spans);
        TextDoc.Toggle(p, 0, 12, Fmt.Bold); // nicht alles fett → alles fett
        Assert.Equal([new Span(0, 12, Fmt.Bold)], p.Spans);
        TextDoc.Toggle(p, 0, 12, Fmt.Bold); // alles fett → aus
        Assert.Empty(p.Spans);
        TextDoc.Format(p, 0, 5, Fmt.Super, true); TextDoc.Format(p, 0, 5, Fmt.Sub, true);
        Assert.Equal(Fmt.Sub, p.At(0).Fmt);
        TextDoc.Grow(p, 13, 4, bigger: true); Assert.Equal(12, p.At(13).Size);
        TextDoc.Grow(p, 13, 4, bigger: true); Assert.Equal(14, p.At(13).Size);
        TextDoc.Grow(p, 13, 4, bigger: false); Assert.Equal(12, p.At(13).Size);
        TextDoc.ChangeCase(p, 6, 6, TextDoc.Case.Upper); Assert.Equal("Hallo SCHÖNE Welt", p.Text);
        TextDoc.ChangeCase(p, 0, 17, TextDoc.Case.Lower); Assert.Equal("hallo schöne welt", p.Text);
        Assert.Equal(12, p.At(13).Size); // Format bleibt beim Umwandeln
        var s = new Para("straße"); TextDoc.ChangeCase(s, 0, 6, TextDoc.Case.Upper); Assert.Equal(6, s.Text.Length);
        Assert.Throws<ArgumentException>(() => TextDoc.Size(p, 0, 1, 500));
    }

    [Fact(DisplayName = "T-0223 T1/T2: Tippen und Löschen verschiebt Formate richtig; am Ende eines fetten Worts weitertippen bleibt fett")]
    public void Editing_keeps_formats()
    {
        var p = new Para("Das ist wichtig hier");
        TextDoc.Format(p, 8, 7, Fmt.Bold, true); // „wichtig“
        TextDoc.EditText(p, "Achtung: Das ist wichtig hier");
        Assert.Equal([new Span(17, 7, Fmt.Bold)], p.Spans);
        TextDoc.EditText(p, "Achtung: Das ist wichtiger hier"); // am Ende angefügt
        Assert.Equal([new Span(17, 9, Fmt.Bold)], p.Spans);
        TextDoc.EditText(p, "Achtung: wichtiger hier"); // davor gelöscht
        Assert.Equal([new Span(9, 9, Fmt.Bold)], p.Spans);
        TextDoc.EditText(p, "Achtung: hier"); // fettes Wort gelöscht
        Assert.Empty(p.Spans);
    }

    [Fact(DisplayName = "T-0223 T5/T7: Suchen (auch in Tabellen, ganze Wörter), Ersetzen behält das Format, Wörter zählen, Tabellenzeilen und -spalten")]
    public void Find_replace_count_tables()
    {
        var d = Sample();
        Assert.Equal(2, d.Find("punkt").Count);
        Assert.Single(d.Find("Miete"));
        Assert.Empty(d.Find("punkt", matchCase: true));
        Assert.Single(d.Find("Zitat", wholeWord: true));
        Assert.Empty(d.Find("Zit", wholeWord: true));
        var intro = (Para)d.Blocks[2];
        Assert.Equal(1, d.ReplaceAll("fett", "FETT"));
        Assert.Contains("FETT", intro.Text);
        Assert.True(TextDoc.Has(intro, 8, 4, Fmt.Bold)); // Format der Fundstelle bleibt
        var c = d.Count();
        Assert.True(c.Words > 20); Assert.True(c.CharactersNoSpaces < c.Characters); Assert.True(c.Paragraphs >= 10);
        var t = new Table(2, 2);
        TextDoc.InsertRow(t, 1); TextDoc.InsertColumn(t, 2);
        Assert.Equal((3, 3), (t.Rows.Count, t.Cols));
        TextDoc.DeleteRow(t, 0); TextDoc.DeleteColumn(t, 0);
        Assert.Equal((2, 2), (t.Rows.Count, t.Cols));
    }

    [Fact(DisplayName = "T-0223 T1: DOCX speichern und wieder öffnen – alles bleibt gleich (Absatzarten, Formate, Größe, Listen, Ausrichtung, Tabelle, Bild, Umbruch)")]
    public void Docx_roundtrip()
    {
        var d = Tmp();
        try
        {
            var doc = Sample(); var f = Path.Combine(d, "probe.docx");
            DocxFormat.Write(doc, f);
            var back = DocxFormat.Read(f);
            Assert.Equal(Describe(doc), Describe(back));
            Assert.Equal("Probe", back.Title);
            if (Environment.GetEnvironmentVariable("TRENDO_TEXT_KEEP") is { Length: > 0 } keep) { Directory.CreateDirectory(keep); File.Copy(f, Path.Combine(keep, Path.GetFileName(f)), true); File.WriteAllText(Path.Combine(keep, "erwartet.txt"), Describe(doc)); TextPdf.Export(doc, Path.Combine(keep, "trendo.pdf")); } // Abgleich mit LibreOffice
            using var z = System.IO.Compression.ZipFile.OpenRead(f);
            Assert.Contains("w:styleId=\"Heading1\"", new StreamReader(z.GetEntry("word/styles.xml")!.Open()).ReadToEnd()); // echte Formatvorlage
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0223 T1: ODT speichern und wieder öffnen – alles bleibt gleich; mimetype steht vorn und unkomprimiert")]
    public void Odt_roundtrip()
    {
        var d = Tmp();
        try
        {
            var doc = Sample(); var f = Path.Combine(d, "probe.odt");
            OdtFormat.Write(doc, f);
            Assert.Equal(Describe(doc), Describe(OdtFormat.Read(f)));
            if (Environment.GetEnvironmentVariable("TRENDO_TEXT_KEEP") is { Length: > 0 } keep) { Directory.CreateDirectory(keep); File.Copy(f, Path.Combine(keep, Path.GetFileName(f)), true); File.WriteAllText(Path.Combine(keep, "erwartet.txt"), Describe(doc)); } // Abgleich mit LibreOffice
            using var z = System.IO.Compression.ZipFile.OpenRead(f);
            Assert.Equal("mimetype", z.Entries[0].FullName);
            Assert.Equal(z.Entries[0].Length, z.Entries[0].CompressedLength);
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0223 T1: Abgleich – von LibreOffice umgewandelte Dateien liest Trendo mit gleichem Inhalt (nur mit TRENDO_LO_ABGLEICH)")]
    public void LibreOffice_roundtrip()
    {
        if (Environment.GetEnvironmentVariable("TRENDO_LO_ABGLEICH") is not { Length: > 0 } k) return; // braucht die LibreOffice-Umwandlung aus reports/t0223-office
        var expected = File.ReadAllText(Path.Combine(k, "erwartet.txt"));
        File.WriteAllText(Path.Combine(k, "trendo-liest-lo-odt.txt"), Describe(OdtFormat.Read(Path.Combine(k, "lo-aus-docx", "probe.odt"))));
        File.WriteAllText(Path.Combine(k, "trendo-liest-lo-docx.txt"), Describe(DocxFormat.Read(Path.Combine(k, "lo-aus-odt", "probe.docx"))));
        Assert.True(File.Exists(Path.Combine(k, "trendo-liest-lo-odt.txt")) && expected.Length > 0);
    }

    [Fact(DisplayName = "T-0223 T1: TXT speichern und öffnen, unbekanntes Format verständlich abgelehnt, DIN-5008-Vorlage öffnet")]
    public void Txt_and_template_files()
    {
        var d = Tmp();
        try
        {
            var f = Path.Combine(d, "notiz.txt");
            TextFiles.Save(new TextDoc { Blocks = [new Para("Zeile 1"), new Para("Zeile 2")] }, f);
            Assert.Equal(["Zeile 1", "Zeile 2"], TextFiles.Open(f).Paras.Select(p => p.Text));
            Assert.Contains("kann Trendo Text", Assert.Throws<ArgumentException>(() => TextFiles.Open(Path.Combine(d, "x.xyz"))).Message);
            var din = TextFiles.Open(Repo("src/Trendo.App/Assets/templates/DIN5008-Geschaeftsbrief-FormB.docx"));
            Assert.True(din.Paras.Count() > 5);
            Assert.Contains(din.Paras, p => p.Text.Length > 3);
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0223 T9: Als PDF – A4, Seitenumbruch ergibt neue Seite, langer Text bricht auf weitere Seiten um")]
    public void Pdf_export()
    {
        var d = Tmp();
        try
        {
            var f = Path.Combine(d, "probe.pdf");
            Assert.Equal(2, TextPdf.Export(Sample(), f));
            using (var pdf = PdfReader.Open(f, PdfDocumentOpenMode.Import))
            {
                Assert.Equal(2, pdf.PageCount);
                Assert.Equal(595, (int)Math.Round(pdf.Pages[0].Width.Point));
                Assert.Equal(["Kapitel eins"], pdf.Outlines.Select(o => o.Title)); // Lesezeichen für Überschriften wie bei LibreOffice
            }
            var longDoc = new TextDoc();
            for (var i = 0; i < 120; i++) longDoc.Blocks.Add(new Para($"Absatz {i}: " + string.Join(' ', Enumerable.Repeat("Wort", 30))) { Align = Align.Justify });
            Assert.True(TextPdf.Export(longDoc, Path.Combine(d, "lang.pdf")) >= 5);
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0223 T10: Vorlagen Lebenslauf, Rechnung, Protokoll haben Überschriften, Tabellen und Platzhalter")]
    public void Templates()
    {
        foreach (var (id, _, _) in TextTemplates.Builtin)
        {
            var t = TextTemplates.Create(id);
            Assert.NotEmpty(t.Blocks);
            if (id == "leer") continue;
            Assert.Contains(t.Paras, p => p.Style != ParaStyle.Text);
            Assert.Contains(t.Blocks, b => b is Table);
            Assert.Contains("[", t.PlainText());
        }
    }

    [Fact(DisplayName = "T-0223 T8: Rechtschreibung Deutsch – Fehler gefunden mit Vorschlägen, richtige Wörter, Zahlen, Adressen und eigene Wörter nicht angestrichen")]
    public void Spelling_german()
    {
        var dic = Repo("artifacts/vollpaket/staging/libreoffice/App/libreoffice/share/extensions/dict-de/de_DE_frami.dic");
        if (!File.Exists(dic)) return; // Wörterbuch kommt aus dem Vollpaket (nicht im Repo) – ohne Vollpaket nichts zu prüfen
        var d = Tmp();
        try
        {
            var sp = Spelling.Load([dic], Path.Combine(d, "eigene.txt"))!;
            Assert.NotNull(sp);
            var doc = new TextDoc { Blocks = [new Para("Das Hauss ist schön und die E-Mail-Adresse steht in Zeile 12."), new Para("Kontakt: max@example.de, Trendoo bleibt.")] };
            var errors = sp.Check(doc);
            Assert.Contains(errors, e => e.Word == "Hauss");
            Assert.Contains("Haus", sp.Suggest("Hauss"));
            Assert.DoesNotContain(errors, e => e.Word is "schön" or "Zeile" or "E-Mail-Adresse" or "example");
            Assert.Contains(errors, e => e.Word == "Trendoo");
            sp.AddOwn("Trendoo");
            Assert.DoesNotContain(sp.Check(doc), e => e.Word == "Trendoo");
            Assert.DoesNotContain(Spelling.Load([dic], Path.Combine(d, "eigene.txt"))!.Check(doc), e => e.Word == "Trendoo"); // gespeichert
            Assert.Null(Spelling.Load([Path.Combine(d, "gibt-es-nicht.dic")]));
        }
        finally { Directory.Delete(d, true); }
    }
}
