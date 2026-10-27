using System.Diagnostics;
using System.IO.Compression;
using Trendo.Core.Office;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0230: Ausbau Trendo Text (W1–W9) – Kern und Dateien.</summary>
public class TextProTests
{
    static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
    static string Temp() { var d = Path.Combine(Path.GetTempPath(), "trendo-textpro-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

    static TextDoc Sample()
    {
        var d = new TextDoc();
        d.Blocks.Add(new Para("Jahresbericht", ParaStyle.Title));
        d.Blocks.Add(new TocBlock());
        d.Blocks.Add(new Para("Einleitung", ParaStyle.Heading1));
        var p = new Para("Mehr Infos auf der Seite der Stadtwerke. Wichtiger Satz.") { IndentCm = 1.5, SpaceAfterPt = 12 };
        TextDoc.AddMark(p, 21, 18, MarkKind.Link, "https://example.org/stadtwerke");
        TextDoc.AddMark(p, 41, 16, MarkKind.Comment, "Bitte prüfen");
        TextDoc.AddMark(p, 0, 4, MarkKind.Footnote, "Stand Oktober");
        TextDoc.AddMark(p, 0, 15, MarkKind.Endnote, "Quelle: eigene Erhebung");
        d.Blocks.Add(p);
        d.Blocks.Add(new Para("Ergebnisse", ParaStyle.Heading2));
        d.Blocks.Add(new Picture { Data = Png, WidthCm = 4, HeightCm = 4, Caption = "Umsatz", Alt = "Balken steigen von links nach rechts" });
        var chg = new Para("Alt und neu");
        chg.Marks.Add(new Mark(0, 4, MarkKind.Delete)); chg.Marks.Add(new Mark(8, 3, MarkKind.Insert));
        d.Blocks.Add(chg);
        d.Page = new PageSetup { Landscape = true, Columns = 2, Header = "Erika Muster GmbH", Footer = "Vertraulich", PageNumbers = true, Watermark = "ENTWURF", PageBorder = true, Design = "klassisch", Hyphenate = true };
        d.Page.SetMargins("schmal");
        return d;
    }

    [Fact(DisplayName = "T-0230 W2/W5/W7: Markierungen wandern beim Tippen mit, Teilen und Verbinden von Absätzen behält sie")]
    public void Marks_follow_edits()
    {
        var p = new Para("Hallo Welt");
        TextDoc.AddMark(p, 6, 4, MarkKind.Link, "https://example.org");
        TextDoc.AddMark(p, 0, 5, MarkKind.Footnote, "Gruß");
        TextDoc.EditText(p, "Ein Hallo Welt");
        Assert.Equal((10, 4), (p.Marks.Single(m => m.Kind == MarkKind.Link).Start, p.Marks.Single(m => m.Kind == MarkKind.Link).Length));
        Assert.Equal(9, p.Marks.Single(m => m.Kind == MarkKind.Footnote).Start);
        var d = new TextDoc { Blocks = [p] };
        var next = d.SplitAt(0, 9);
        Assert.Single(p.Marks); Assert.Equal(MarkKind.Footnote, p.Marks[0].Kind);
        Assert.Equal((1, 4), (next.Marks[0].Start, next.Marks[0].Length));
        d.MergeWithPrevious(1);
        Assert.Equal(2, p.Marks.Count); Assert.Equal(10, p.Marks.Single(m => m.Kind == MarkKind.Link).Start);
    }

    [Fact(DisplayName = "T-0230: DOCX speichern und lesen – Seite einrichten, Kopf-/Fußzeile, Wasserzeichen, Design, Links, Kommentare, Fuß-/Endnoten, Änderungen, Verzeichnis, Beschriftung, Alternativtext, Einzug")]
    public void Docx_roundtrip()
    {
        var dir = Temp();
        try
        {
            var f = Path.Combine(dir, "bericht.docx"); DocxFormat.Write(Sample(), f);
            using (var z = ZipFile.OpenRead(f))
            {
                var all = string.Concat(z.Entries.Where(e => e.Name.EndsWith(".xml", StringComparison.Ordinal)).Select(e => new StreamReader(e.Open()).ReadToEnd()));
                Assert.DoesNotContain("Trendo", all);
                Assert.Contains("w:author=\"\"", all);
                Assert.NotNull(z.GetEntry("word/footnotes.xml")); Assert.NotNull(z.GetEntry("word/comments.xml")); Assert.NotNull(z.GetEntry("word/header1.xml"));
            }
            Check(DocxFormat.Read(f), full: true);
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }

    static void Check(TextDoc b, bool full)
    {
        var pg = b.Page;
        Assert.True(pg.Landscape); Assert.Equal(2, pg.Columns); Assert.InRange(pg.Left, 1.2, 1.35);
        Assert.Equal("Erika Muster GmbH", pg.Header); Assert.True(pg.PageNumbers); Assert.Contains("Vertraulich", pg.Footer);
        Assert.Single(b.Blocks.OfType<TocBlock>());
        var p = b.Paras.First(x => x.Text.StartsWith("Mehr Infos", StringComparison.Ordinal));
        Assert.Equal("https://example.org/stadtwerke", p.Marks.Single(m => m.Kind == MarkKind.Link).Value);
        Assert.Equal("Bitte prüfen", p.Marks.Single(m => m.Kind == MarkKind.Comment).Value);
        Assert.Equal("Stand Oktober", p.Marks.Single(m => m.Kind == MarkKind.Footnote).Value);
        Assert.Equal("Quelle: eigene Erhebung", p.Marks.Single(m => m.Kind == MarkKind.Endnote).Value);
        var pic = b.Blocks.OfType<Picture>().Single();
        Assert.Equal("Balken steigen von links nach rechts", pic.Alt); Assert.Equal("Umsatz", pic.Caption);
        var chg = b.Paras.First(x => x.Text.Contains("neu"));
        Assert.Contains(chg.Marks, m => m.Kind == MarkKind.Delete); Assert.Contains(chg.Marks, m => m.Kind == MarkKind.Insert);
        if (!full) return;
        Assert.Equal("ENTWURF", pg.Watermark); Assert.True(pg.PageBorder); Assert.Equal("klassisch", pg.Design); Assert.True(pg.Hyphenate);
        Assert.Equal(1.5, p.IndentCm, 1); Assert.Equal(12, p.SpaceAfterPt);
    }

    [Fact(DisplayName = "T-0230 W5/W6/W7: Verzeichnis, Serienbrief, Vergleichen mit Annehmen/Ablehnen, Barrierefreiheit, Etiketten und Umschläge")]
    public void Tools()
    {
        var d = Sample();
        Assert.Equal([(1, "Einleitung"), (2, "Ergebnisse")], TextTools.Toc(d).Select(e => (e.Level, e.Text)));
        // Serienbrief
        var t = new TextDoc { Blocks = [new Para("Hallo «Vorname» {Nachname},"), new Para("Ihr Termin: «Termin»")] };
        Assert.Equal(["Vorname", "Nachname", "Termin"], TextTools.Fields(t));
        var m = TextTools.MailMerge(t, ["Vorname", "Nachname", "Termin"], [["Erika", "Muster", "Mo 10 Uhr"], ["Max", "Beispiel", "Di 9 Uhr"]]);
        Assert.Equal(5, m.Blocks.Count); Assert.IsType<PageBreak>(m.Blocks[2]);
        Assert.Equal("Hallo Max Beispiel,", ((Para)m.Blocks[3]).Text);
        // Vergleichen
        var a = new TextDoc { Blocks = [new Para("Erster Absatz bleibt."), new Para("Der Hund läuft schnell."), new Para("Fällt weg.")] };
        var bnew = new TextDoc { Blocks = [new Para("Erster Absatz bleibt."), new Para("Der Hund rennt schnell."), new Para("Ganz neu.")] };
        var cmp = TextTools.Compare(a, bnew);
        Assert.True(TextTools.HasChanges(cmp));
        var accepted = cmp.Copy(); TextTools.Resolve(accepted, accept: true);
        Assert.Equal(bnew.Paras.Select(p => p.Text), accepted.Paras.Select(p => p.Text));
        var rejected = cmp.Copy(); TextTools.Resolve(rejected, accept: false);
        Assert.Equal(a.Paras.Select(p => p.Text), rejected.Paras.Select(p => p.Text));
        Assert.False(TextTools.HasChanges(accepted));
        // Barrierefreiheit
        var acc = new TextDoc { Blocks = [new Para("Titel", ParaStyle.Heading1), new Para("Unterpunkt", ParaStyle.Heading3), new Picture { Data = Png }] };
        var link = new Para("Mehr hier"); TextDoc.AddMark(link, 5, 4, MarkKind.Link, "https://example.org"); acc.Blocks.Add(link);
        var issues = TextTools.Accessibility(acc).Select(i => i.Text).ToList();
        Assert.Contains(issues, i => i.Contains("Alternativtext")); Assert.Contains(issues, i => i.Contains("Überschrift 3 folgt auf Überschrift 1")); Assert.Contains(issues, i => i.Contains("Linktext"));
        Assert.Empty(TextTools.Accessibility(new TextDoc { Blocks = [new Para("Titel", ParaStyle.Heading1), new Para("Text")] }));
        // Etiketten und Umschläge
        var dir = Temp();
        try
        {
            Assert.Equal(2, TextTools.Labels(Enumerable.Range(1, 30).Select(i => $"Erika Muster {i}\nHauptstraße {i}\n10115 Berlin"), Path.Combine(dir, "etiketten.pdf")));
            Assert.Equal(2, TextTools.Envelopes("Max Beispiel\nRingweg 2\n20095 Hamburg", ["Erika Muster\nHauptstraße 1\n10115 Berlin", "Lena Probe\nWeg 3\n80331 München"], Path.Combine(dir, "umschlag.pdf")));
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }

    static void CheckOdt(TextDoc b)
    {
        var pg = b.Page;
        Assert.True(pg.Landscape); Assert.Equal(2, pg.Columns); Assert.InRange(pg.Left, 1.2, 1.35); Assert.True(pg.PageBorder);
        Assert.Equal("Erika Muster GmbH", pg.Header); Assert.True(pg.PageNumbers); Assert.Contains("Vertraulich", pg.Footer);
        Assert.Single(b.Blocks.OfType<TocBlock>());
        var p = b.Paras.First(x => x.Text.StartsWith("Mehr Infos", StringComparison.Ordinal));
        Assert.Equal("https://example.org/stadtwerke", p.Marks.Single(m => m.Kind == MarkKind.Link).Value);
        Assert.Equal("Bitte prüfen", p.Marks.Single(m => m.Kind == MarkKind.Comment).Value);
        Assert.Equal("Stand Oktober", p.Marks.Single(m => m.Kind == MarkKind.Footnote).Value);
        Assert.Equal("Quelle: eigene Erhebung", p.Marks.Single(m => m.Kind == MarkKind.Endnote).Value);
        Assert.DoesNotContain("Stand Oktober", p.Text);
        var pic = b.Blocks.OfType<Picture>().Single(); Assert.Equal("Balken steigen von links nach rechts", pic.Alt); Assert.Equal("Umsatz", pic.Caption);
        Assert.Contains(b.Paras, x => x.Text == "und neu"); // Änderungen werden in ODT als angenommen gespeichert
    }

    [Fact(DisplayName = "T-0230: ODT speichern und lesen, auch nach LibreOffice Writer – Seite, Kopf-/Fußzeile, Links, Kommentare, Fuß-/Endnoten, Verzeichnis, Beschriftung, Alternativtext")]
    public void Odt_roundtrip()
    {
        var dir = Temp();
        try
        {
            var f = Path.Combine(dir, "bericht.odt"); OdtFormat.Write(Sample(), f);
            using (var z = ZipFile.OpenRead(f)) Assert.DoesNotContain("Trendo", string.Concat(z.Entries.Where(e => e.Name.EndsWith(".xml", StringComparison.Ordinal)).Select(e => new StreamReader(e.Open()).ReadToEnd())));
            CheckOdt(OdtFormat.Read(f));
            if (Soffice() is not { } soffice) return;
            var outDir = Path.Combine(dir, "out"); Directory.CreateDirectory(outDir);
            var psi = new ProcessStartInfo(soffice) { UseShellExecute = false, CreateNoWindow = true };
            foreach (var x in new[] { "-env:UserInstallation=" + new Uri(Path.Combine(dir, "profil")).AbsoluteUri, "--headless", "--norestore", "--convert-to", "odt", "--outdir", outDir, f }) psi.ArgumentList.Add(x);
            using (var p = Process.Start(psi)!) Assert.True(p.WaitForExit(180_000), "LibreOffice antwortet nicht");
            CheckOdt(OdtFormat.Read(Path.Combine(outDir, "bericht.odt")));
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }

    static string? Soffice()
    {
        foreach (var root in new[] { Environment.GetEnvironmentVariable("TRENDO_WERKZEUGE"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TrendoWerkzeuge") })
            if (root is not null && Path.Combine(root, "libreoffice", "App", "libreoffice", "program", "soffice.exe") is var p && File.Exists(p)) return p;
        return null;
    }

    [Fact(DisplayName = "T-0230: Abgleich mit LibreOffice Writer – Writer öffnet Trendos DOCX und speichert DOCX, Trendo liest Seite, Kopf-/Fußzeile, Links, Kommentare, Fußnoten, Änderungen, Verzeichnis (nur mit LibreOffice)")]
    public void Writer_roundtrip()
    {
        if (Soffice() is not { } soffice) return;
        var dir = Temp();
        try
        {
            var src = Path.Combine(dir, "bericht.docx"); DocxFormat.Write(Sample(), src);
            var outDir = Path.Combine(dir, "out"); Directory.CreateDirectory(outDir);
            var psi = new ProcessStartInfo(soffice) { UseShellExecute = false, CreateNoWindow = true };
            foreach (var x in new[] { "-env:UserInstallation=" + new Uri(Path.Combine(dir, "profil")).AbsoluteUri, "--headless", "--norestore", "--convert-to", "docx:MS Word 2007 XML", "--outdir", outDir, src }) psi.ArgumentList.Add(x);
            using (var p = Process.Start(psi)!) Assert.True(p.WaitForExit(180_000), "LibreOffice antwortet nicht");
            Check(DocxFormat.Read(Path.Combine(outDir, "bericht.docx")), full: false);
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }
}
