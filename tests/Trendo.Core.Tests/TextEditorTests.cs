using System.IO.Compression;
using Trendo.Core.Office;
using Trendo.Core.Sys;
using Xunit;

namespace Trendo.Core.Tests;

/// T-0692 D1a: Trendo Text als durchgehender Texteditor – Seitensatz (ein Umbruch für Bildschirm und PDF), Bearbeiten über Absatzgrenzen,
/// Tabellen mit formatierten und verbundenen Zellen, Rundreise DOCX/ODT.
public class T0692_TextEditorTests
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-t0692-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }
    static string Fremd(string name) { for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent) { var f = Path.Combine(d.FullName, "tests", "Daten", "office", name); if (File.Exists(f)) return f; } throw new FileNotFoundException(name); }
    static string Xml(string file, string part) { using var z = ZipFile.OpenRead(file); using var s = new StreamReader(z.GetEntry(part)!.Open()); return s.ReadToEnd(); }

    static TextDoc Long(int paras)
    {
        var doc = new TextDoc();
        for (var i = 0; i < paras; i++)
        {
            var p = new Para($"Absatz {i}: Trendo Text setzt Schrift, Farbe und Abstände wie ein Schreibprogramm und bricht lange Zeilen am Wortende um, damit jede Seite sauber gefüllt ist.");
            TextDoc.Format(p, 0, 8, Fmt.Bold, true); TextDoc.SetInk(p, 10, 6, MarkKind.Color, "#B8860B");
            doc.Blocks.Add(p);
        }
        return doc;
    }

    [Fact(DisplayName = "T-0692 Seitensatz: Bildschirm und PDF haben denselben Umbruch – gleiche Seitenzahl wie die geschriebene PDF-Datei, jede Zeile liegt im Satzspiegel, „Seite x von y“ auf jeder Seite")]
    public void Flow_matches_pdf()
    {
        var doc = Long(120); doc.Page.PageNumbers = true; doc.Page.Header = "Bericht";
        var flow = FlowDoc.Build(doc);
        Assert.True(flow.Pages.Count >= 4, $"nur {flow.Pages.Count} Seiten");
        var d = Tmp();
        try
        {
            var pdf = Path.Combine(d, "a.pdf");
            Assert.Equal(flow.Pages.Count, TextPdf.Export(doc, pdf));
            Assert.Equal(flow.Pages.Count, PdfTools.Read(pdf).Pages); // die Datei selbst gezählt, nicht nur der Rückgabewert
        }
        finally { Directory.Delete(d, true); }
        const double cm = 72 / 2.54;
        foreach (var l in flow.Lines)
        {
            Assert.InRange(l.Y, doc.Page.Top * cm - 0.5, flow.PageH - doc.Page.Bottom * cm);
            Assert.True(l.Y + l.H <= flow.PageH - doc.Page.Bottom * cm + 0.5, $"Zeile ragt über den unteren Rand: Seite {l.Page + 1}");
            Assert.True(l.X >= doc.Page.Left * cm - 0.5);
        }
        for (var i = 0; i < flow.Pages.Count; i++)
        {
            Assert.Contains(flow.Pages[i].Items.OfType<FlowAligned>(), a => a.Text == $"Seite {i + 1} von {flow.Pages.Count}");
            Assert.Contains(flow.Pages[i].Items.OfType<FlowAligned>(), a => a.Text == "Bericht");
        }
        // jeder Absatz ist vollständig gesetzt: die Zeilen decken den Text lückenlos ab
        for (var b = 0; b < doc.Blocks.Count; b++)
        {
            var lines = flow.LinesOf(new TextPos(b, 0)); var p = (Para)doc.Blocks[b];
            Assert.Equal(0, lines[0].Start); Assert.Equal(p.Text.Length, lines[^1].End);
            for (var k = 1; k < lines.Count; k++) Assert.Equal(lines[k - 1].End, lines[k].Start);
            Assert.Equal(p.Text.Length, lines.Sum(l => l.Segs.Sum(s => s.Length)));
        }
    }

    [Fact(DisplayName = "T-0692 Seitensatz: Cursor und Klick passen zusammen – jede Stelle eines Absatzes mit gemischten Formaten, Tabulator und Zeilenwechsel wird per Klick wieder getroffen; Pfeil hoch/runter hält die Spalte")]
    public void Caret_and_hit()
    {
        var doc = new TextDoc();
        var p = new Para("Ein längerer Absatz mit fetten, kursiven und großen Wörtern,\tmit Tabulator und\nZeilenwechsel, der über mehrere Zeilen läuft und dabei am Wortende umbricht, damit die Prüfung auch weiche Umbrüche trifft.");
        TextDoc.Format(p, 4, 8, Fmt.Bold, true); TextDoc.Format(p, 30, 9, Fmt.Italic, true); TextDoc.Size(p, 44, 6, 20); TextDoc.SetInk(p, 60, 9, MarkKind.Font, "Georgia");
        doc.Blocks.Add(new Para("Davor")); doc.Blocks.Add(p); doc.Blocks.Add(new Para("Danach"));
        var flow = FlowDoc.Build(doc);
        var lines = flow.LinesOf(new TextPos(1, 0));
        Assert.True(lines.Count >= 3);
        for (var o = 0; o <= p.Text.Length; o++)
        {
            var c = flow.Caret(new TextPos(1, o))!.Value;
            var hit = flow.Hit(c.Page, c.X + 0.05, c.Y + c.H / 2)!.Value;
            var softEnd = lines.Take(lines.Count - 1).Any(l => l.End == o && p.Text[o - 1] != '\n'); // am weichen Umbruch steht der Cursor am Anfang der nächsten Zeile
            if (!softEnd) Assert.True(hit == new TextPos(1, o), $"Stelle {o} („{p.Text[Math.Max(0, o - 3)..Math.Min(p.Text.Length, o + 3)]}“) → {hit.Offset}");
        }
        // hoch/runter: aus der Mitte der zweiten Zeile in die erste und zurück in dieselbe Zeile
        var mid = new TextPos(1, lines[1].Start + 10); var x = flow.Caret(mid)!.Value.X;
        var up = flow.Vertical(mid, -1, x)!.Value; Assert.Equal(1, up.Block); Assert.InRange(up.Offset, lines[0].Start, lines[0].End);
        var down = flow.Vertical(up, 1, x)!.Value; Assert.Equal(mid, down);
        Assert.Equal(0, flow.Vertical(new TextPos(1, 3), -1, flow.Caret(new TextPos(1, 3))!.Value.X)!.Value.Block); // über die Absatzgrenze
        Assert.Equal(2, flow.Vertical(new TextPos(1, p.Text.Length), 1, 0)!.Value.Block);
        Assert.Null(flow.Vertical(new TextPos(0, 0), -1, 0)); // oben ist Schluss
    }

    [Fact(DisplayName = "T-0692 Bearbeiten: Bereich über drei Absätze fett setzen, löschen (Absätze verbinden sich, Formate bleiben), kopieren und mit Format wieder einfügen; reiner Text mit Zeilenwechseln teilt Absätze")]
    public void Edit_across_paragraphs()
    {
        var doc = new TextDoc { Blocks = [new Para("Erster Absatz"), new Para("Zweiter Absatz") { Align = Align.Center }, new Para("Dritter Absatz")] };
        TextDoc.Format((Para)doc.Blocks[2], 8, 6, Fmt.Italic, true);
        var a = new TextPos(0, 7); var b = new TextPos(2, 7);
        Assert.Equal(3, TextEdit.Apply(doc, a, b, (p, s, l) => TextDoc.Format(p, s, l, Fmt.Bold, true)));
        Assert.True(TextDoc.Has((Para)doc.Blocks[0], 7, 6, Fmt.Bold)); Assert.False(TextDoc.Has((Para)doc.Blocks[0], 0, 7, Fmt.Bold));
        Assert.True(TextDoc.Has((Para)doc.Blocks[1], 0, 14, Fmt.Bold)); Assert.True(TextDoc.Has((Para)doc.Blocks[2], 0, 7, Fmt.Bold)); Assert.False(TextDoc.Has((Para)doc.Blocks[2], 7, 1, Fmt.Bold));

        var copy = TextEdit.Copy(doc, a, b);
        Assert.Equal("Absatz\nZweiter Absatz\nDritter", TextEdit.Plain(copy));
        Assert.Equal(Align.Center, ((Para)copy[1]).Align); Assert.True(TextDoc.Has((Para)copy[0], 0, 6, Fmt.Bold));

        var at = TextEdit.Delete(doc, a, b);
        Assert.Equal(a, at); Assert.Single(doc.Blocks);
        var one = (Para)doc.Blocks[0];
        Assert.Equal("Erster  Absatz", one.Text); Assert.True(TextDoc.Has(one, 8, 6, Fmt.Italic)); // der Rest des dritten Absatzes behält kursiv

        var end = TextEdit.Paste(doc, at, copy);
        Assert.Equal(["Erster Absatz", "Zweiter Absatz", "Dritter Absatz"], doc.Paras.Select(p => p.Text));
        Assert.Equal(new TextPos(2, 7), end);
        Assert.True(TextDoc.Has((Para)doc.Blocks[0], 7, 6, Fmt.Bold)); Assert.Equal(Align.Center, ((Para)doc.Blocks[1]).Align); Assert.True(TextDoc.Has((Para)doc.Blocks[2], 0, 7, Fmt.Bold)); Assert.True(TextDoc.Has((Para)doc.Blocks[2], 8, 6, Fmt.Italic));

        var after = TextEdit.InsertText(doc, new TextPos(1, 7), " neuer\nund zweiter");
        Assert.Equal(["Erster Absatz", "Zweiter neuer", "und zweiter Absatz", "Dritter Absatz"], doc.Paras.Select(p => p.Text));
        Assert.Equal(new TextPos(2, 11), after); Assert.True(TextDoc.Has((Para)doc.Blocks[1], 8, 5, Fmt.Bold)); // Getipptes erbt das Format davor

        // Rücktaste am Absatzanfang verbindet, Entf am Absatzende holt den nächsten Absatz heran, Schritt über die Absatzgrenze
        Assert.Equal(new TextPos(1, 13), TextEdit.Backspace(doc, new TextPos(2, 0))); Assert.Equal(3, doc.Blocks.Count);
        TextEdit.DeleteForward(doc, new TextPos(0, 13)); Assert.Equal(2, doc.Blocks.Count);
        Assert.Equal(new TextPos(1, 0), TextEdit.Step(doc, new TextPos(0, ((Para)doc.Blocks[0]).Text.Length), 1));
        Assert.Equal((new TextPos(1, 0), new TextPos(1, 7)), TextEdit.WordAt(doc, new TextPos(1, 3)));
        Assert.Equal(new TextPos(1, 8), TextEdit.WordStep(doc, new TextPos(1, 2), 1)); Assert.Equal(new TextPos(1, 0), TextEdit.WordStep(doc, new TextPos(1, 5), -1));
    }

    [Fact(DisplayName = "T-0692 Tippen: Eingefügtes erbt das Format des Zeichens davor; Links, Schrift und Farbe laufen weiter; eingefügter Absatzteil behält sein eigenes Format")]
    public void Insert_keeps_formats()
    {
        var p = new Para("fett normal");
        TextDoc.Format(p, 0, 4, Fmt.Bold, true); TextDoc.SetInk(p, 0, 4, MarkKind.Color, "#C00000");
        TextDoc.InsertAt(p, 4, "er");
        Assert.Equal("fetter normal", p.Text); Assert.True(TextDoc.Has(p, 0, 6, Fmt.Bold)); Assert.Equal("#C00000", TextDoc.InkAt(p, 5, MarkKind.Color)); Assert.False(TextDoc.Has(p, 6, 1, Fmt.Bold));
        TextDoc.InsertAt(p, 7, "ganz ");
        Assert.Equal("fetter ganz normal", p.Text); Assert.False(TextDoc.Has(p, 7, 5, Fmt.Bold));
        TextDoc.InsertAt(p, 0, "Sehr ");
        Assert.True(TextDoc.Has(p, 0, 11, Fmt.Bold)); // am Absatzanfang: Format des ersten Zeichens
        var piece = new Para("kursiv"); TextDoc.Format(piece, 0, 6, Fmt.Italic, true);
        TextDoc.InsertPara(p, 7, piece);
        Assert.Equal("Sehr fekursivtter ganz normal", p.Text);
        Assert.True(TextDoc.Has(p, 7, 6, Fmt.Italic)); Assert.False(TextDoc.Has(p, 7, 6, Fmt.Bold)); Assert.True(TextDoc.Has(p, 13, 4, Fmt.Bold)); Assert.Null(TextDoc.InkAt(p, 8, MarkKind.Color)); Assert.Equal("#C00000", TextDoc.InkAt(p, 14, MarkKind.Color));
        Assert.Equal("kursiv", TextDoc.Slice(p, 7, 6).Text); Assert.True(TextDoc.Has(TextDoc.Slice(p, 5, 10), 2, 6, Fmt.Italic));
    }

    [Fact(DisplayName = "T-0692 Tabelle: Zellen mit Zeichenformaten, verbinden und lösen, Zeilen/Spalten einfügen und löschen mit Verbindungen, Spaltenbreiten; Suchen und Ersetzen behält das Zellformat")]
    public void Table_model()
    {
        var t = new Table(3, 3);
        t.Rows[0][0] = "Kopf"; t.Rows[0][1] = "B"; t.Rows[1][0] = "Nord"; t.Rows[2][2] = "Ende";
        var cell = t.CellPara(0, 0); TextDoc.Format(cell, 0, 4, Fmt.Bold, true); cell.Align = Align.Center; t.SetCell(0, 0, cell);
        Assert.True(TextDoc.Has(t.CellPara(0, 0), 0, 4, Fmt.Bold)); Assert.Equal(Align.Center, t.CellPara(0, 0).Align);
        Assert.True(t.Merge(0, 0, 3, 1));
        Assert.Equal("Kopf\nB", t.Rows[0][0]); Assert.Equal("", t.Rows[0][1]); Assert.True(t.IsCovered(0, 2)); Assert.Equal((0, 0), t.Anchor(0, 1)); Assert.True(TextDoc.Has(t.CellPara(0, 0), 0, 4, Fmt.Bold));
        Assert.True(t.Merge(1, 0, 1, 2)); Assert.False(t.Merge(1, 0, 2, 1)); // ragt in keine fremde Verbindung, aber die eigene Zelle ist schon verbunden: erst lösen
        Assert.False(t.Merge(0, 1, 1, 2)); // überdeckte Zelle kann nicht Anfang sein
        t.InsertRow(2); // mitten in die senkrechte Verbindung: sie wächst
        Assert.Equal((1, 3), t.SpanOf(1, 0)); Assert.Equal(4, t.Rows.Count); Assert.Equal("Ende", t.Rows[3][2]);
        t.InsertColumn(1); // mitten in die waagerechte Verbindung
        Assert.Equal((4, 1), t.SpanOf(0, 0)); Assert.Equal(4, t.Cols); Assert.Equal("Ende", t.Rows[3][3]);
        t.DeleteColumn(0); // die erste Spalte fällt weg: die Kopfzelle rückt nach rechts und bleibt verbunden
        Assert.Equal((3, 1), t.SpanOf(0, 0)); Assert.Equal("Kopf\nB", t.Rows[0][0]); Assert.Equal(3, t.Cols);
        t.DeleteRow(0); Assert.Equal(3, t.Rows.Count); Assert.DoesNotContain(t.Cells.Values, f => f.ColSpan > 1);
        Assert.True(t.Unmerge(0, 0) || t.Cells.Count == 0);
        t.SetWidths([1, 2, 1]); Assert.Equal([0.25, 0.5, 0.25], t.Fractions()); t.SetWidths([1, 1, 1]); Assert.Empty(t.Widths);

        var doc = new TextDoc { Blocks = [new Para("Leistung"), new Table(1, 2)] };
        var tb = (Table)doc.Blocks[1]; var c2 = new Para("Die Leistung zählt"); TextDoc.Format(c2, 4, 8, Fmt.Bold, true); TextDoc.SetInk(c2, 13, 5, MarkKind.Color, "#1971C2"); tb.SetCell(0, 1, c2);
        Assert.Equal(2, doc.ReplaceAll("Leistung", "Arbeit"));
        Assert.Equal("Die Arbeit zählt", tb.Rows[0][1]); Assert.True(TextDoc.Has(tb.CellPara(0, 1), 4, 6, Fmt.Bold)); Assert.Equal("#1971C2", TextDoc.InkAt(tb.CellPara(0, 1), 12, MarkKind.Color));
        // Bereich über die Tabelle hinweg nimmt alle Zellen mit; Schritt vom Absatz in die erste Zelle und Tabulator bis zur neuen Zeile
        doc.Blocks.Add(new Para("Ende"));
        Assert.Equal(4, TextEdit.Parts(doc, new TextPos(0, 0), new TextPos(2, 4)).Count);
        Assert.Equal(new TextPos(1, 0, 0, 0), TextEdit.Step(doc, new TextPos(0, 6), 1));
        var next = TextEdit.NextCell(doc, new TextPos(1, 0, 0, 1), 1); Assert.Equal(2, tb.Rows.Count); Assert.Equal(new TextPos(1, 0, 1, 0), next);
    }

    static TextDoc Mixed()
    {
        var doc = new TextDoc();
        var p = new Para("Gemischt: fett, kursiv, groß und rot in einem Absatz.");
        TextDoc.Format(p, 10, 4, Fmt.Bold, true); TextDoc.Format(p, 16, 6, Fmt.Italic | Fmt.Underline, true); TextDoc.Size(p, 24, 4, 18); TextDoc.SetInk(p, 33, 3, MarkKind.Color, "#C00000"); TextDoc.SetInk(p, 10, 4, MarkKind.Font, "Georgia");
        doc.Blocks.Add(p);
        var t = new Table(3, 3);
        string[][] rows = [["Umsatz nach Quartal", "", ""], ["Nord", "Q1", "1200"], ["", "Q2", "1350"]];
        for (var r = 0; r < 3; r++) for (var c = 0; c < 3; c++) t.Rows[r][c] = rows[r][c];
        var head = t.CellPara(0, 0); TextDoc.Format(head, 0, 6, Fmt.Bold, true); TextDoc.SetInk(head, 12, 7, MarkKind.Color, "#1971C2"); head.Align = Align.Center; t.SetCell(0, 0, head);
        var num = t.CellPara(1, 2); TextDoc.Format(num, 0, 4, Fmt.Italic, true); num.Align = Align.Right; t.SetCell(1, 2, num);
        var two = new Para("Zeile eins\nZeile zwei"); TextDoc.AddMark(two, 0, 5, MarkKind.Link, "https://example.org/"); t.SetCell(2, 1, two);
        Assert.True(t.Merge(0, 0, 3, 1)); Assert.True(t.Merge(1, 0, 1, 2));
        t.SetWidths([2, 1, 1]);
        doc.Blocks.Add(t); doc.Blocks.Add(new Para("Schluss"));
        return doc;
    }

    static void CheckMixed(TextDoc back)
    {
        var p = back.Paras.First();
        Assert.Equal("Gemischt: fett, kursiv, groß und rot in einem Absatz.", p.Text);
        Assert.True(TextDoc.Has(p, 10, 4, Fmt.Bold)); Assert.False(TextDoc.Has(p, 9, 1, Fmt.Bold)); Assert.False(TextDoc.Has(p, 14, 1, Fmt.Bold));
        Assert.True(TextDoc.Has(p, 16, 6, Fmt.Italic)); Assert.True(TextDoc.Has(p, 16, 6, Fmt.Underline)); Assert.Equal(18, p.At(25).Size); Assert.Null(p.At(29).Size);
        Assert.Equal("#C00000", TextDoc.InkAt(p, 34, MarkKind.Color)); Assert.Null(TextDoc.InkAt(p, 37, MarkKind.Color)); Assert.Equal("Georgia", TextDoc.InkAt(p, 11, MarkKind.Font));
        var t = back.Blocks.OfType<Table>().Single();
        Assert.Equal(3, t.Rows.Count); Assert.Equal(3, t.Cols);
        Assert.Equal((3, 1), t.SpanOf(0, 0)); Assert.Equal((1, 2), t.SpanOf(1, 0)); Assert.True(t.IsCovered(0, 1)); Assert.True(t.IsCovered(0, 2)); Assert.True(t.IsCovered(2, 0));
        Assert.Equal("Umsatz nach Quartal", t.Rows[0][0]); Assert.Equal("Nord", t.Rows[1][0]); Assert.Equal("1350", t.Rows[2][2]);
        var head = t.CellPara(0, 0);
        Assert.True(TextDoc.Has(head, 0, 6, Fmt.Bold)); Assert.False(TextDoc.Has(head, 7, 4, Fmt.Bold)); Assert.Equal("#1971C2", TextDoc.InkAt(head, 13, MarkKind.Color)); Assert.Equal(Align.Center, head.Align);
        Assert.True(TextDoc.Has(t.CellPara(1, 2), 0, 4, Fmt.Italic)); Assert.Equal(Align.Right, t.CellPara(1, 2).Align);
        Assert.Equal("Zeile eins\nZeile zwei", t.Rows[2][1]);
        Assert.Contains(t.CellPara(2, 1).Marks, m => m.Kind == MarkKind.Link && m.Start == 0 && m.Length == 5 && m.Value == "https://example.org/");
        var fr = t.Fractions(); Assert.InRange(fr[0], 0.49, 0.51); Assert.InRange(fr[1], 0.24, 0.26);
    }

    [Fact(DisplayName = "T-0692 Rundreise DOCX: gemischte Zeichenformate im Absatz, Zellen mit Format, Ausrichtung, Link und Zeilenwechsel, verbundene Zellen (gridSpan, vMerge) und Spaltenbreiten bleiben über Speichern und Öffnen erhalten")]
    public void Roundtrip_docx()
    {
        var d = Tmp();
        try
        {
            var f = Path.Combine(d, "rund.docx"); TextFiles.Save(Mixed(), f);
            var xml = Xml(f, "word/document.xml");
            Assert.Contains("<w:gridSpan w:val=\"3\"/>", xml); Assert.Contains("<w:vMerge w:val=\"restart\"/>", xml); Assert.Contains("<w:vMerge/>", xml); Assert.Contains("<w:gridCol w:w=\"4500\"/>", xml);
            var back = TextFiles.Open(f); CheckMixed(back);
            Assert.False(back.Import!.HasLoss, back.Import.Lang()); // eigene Datei: nichts zu melden
            var f2 = Path.Combine(d, "rund2.docx"); TextFiles.Save(back, f2); CheckMixed(TextFiles.Open(f2)); // zweite Runde bleibt stabil
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0692 Rundreise ODT: gemischte Zeichenformate, Zellen mit Format, verbundene Zellen (spanned, covered-table-cell) und Spaltenbreiten bleiben erhalten")]
    public void Roundtrip_odt()
    {
        var d = Tmp();
        try
        {
            var f = Path.Combine(d, "rund.odt"); TextFiles.Save(Mixed(), f);
            var xml = Xml(f, "content.xml");
            Assert.Contains("table:number-columns-spanned=\"3\"", xml); Assert.Contains("table:number-rows-spanned=\"2\"", xml); Assert.Contains("<table:covered-table-cell/>", xml); Assert.Contains("style:rel-column-width=\"32768*\"", xml);
            var back = TextFiles.Open(f); CheckMixed(back);
            Assert.False(back.Import!.HasLoss, back.Import.Lang());
            var f2 = Path.Combine(d, "rund2.odt"); TextFiles.Save(back, f2); CheckMixed(TextFiles.Open(f2));
            var f3 = Path.Combine(d, "quer.docx"); TextFiles.Save(back, f3); CheckMixed(TextFiles.Open(f3)); // ODT → DOCX
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0692 Fremddateien: fremd-text.docx und fremd-text.odt behalten ihre verbundenen Zellen über Öffnen, Speichern und erneutes Öffnen; der Importbericht ist um genau diesen Punkt kürzer")]
    public void Fremd_keeps_merged_cells()
    {
        var d = Tmp();
        try
        {
            var docx = TextFiles.Open(Fremd("fremd-text.docx"));
            Assert.DoesNotContain(docx.Import!.Verluste, v => v.Id == "docx.verbunden"); Assert.Contains(docx.Import.Verluste, v => v.Id == "docx.textfeld"); // das Textfeld fehlt weiterhin – und steht weiter im Bericht
            var t = docx.Blocks.OfType<Table>().Single();
            Assert.Equal((3, 1), t.SpanOf(0, 0)); Assert.Equal((1, 2), t.SpanOf(1, 0));
            var copy = Path.Combine(d, "kopie.docx"); TextFiles.Save(docx, copy);
            var t2 = TextFiles.Open(copy).Blocks.OfType<Table>().Single();
            Assert.Equal((3, 1), t2.SpanOf(0, 0)); Assert.Equal((1, 2), t2.SpanOf(1, 0)); Assert.Equal(t.Rows.Select(r => string.Join("|", r)), t2.Rows.Select(r => string.Join("|", r)));

            var odt = TextFiles.Open(Fremd("fremd-text.odt"));
            Assert.DoesNotContain(odt.Import!.Verluste, v => v.Id == "odt.verbunden"); Assert.Contains(odt.Import.Verluste, v => v.Id == "odt.textfeld");
            var o = odt.Blocks.OfType<Table>().Single(); Assert.Equal((2, 1), o.SpanOf(0, 0));
            var copy2 = Path.Combine(d, "kopie.odt"); TextFiles.Save(odt, copy2);
            Assert.Equal((2, 1), TextFiles.Open(copy2).Blocks.OfType<Table>().Single().SpanOf(0, 0));
            // der Seitensatz zeichnet die verbundene Zelle über die ganze Tabellenbreite
            var flow = FlowDoc.Build(docx); var cells = flow.Boxes.Where(b => b.Kind == FlowBoxKind.Cell).ToList();
            var head = cells.Single(b => b.Row == 0 && b.Col == 0); var q1 = cells.Single(b => b.Row == 1 && b.Col == 1); var nord = cells.Single(b => b.Row == 1 && b.Col == 0); var q2 = cells.Single(b => b.Row == 2 && b.Col == 1);
            Assert.Equal(6, cells.Count); Assert.InRange(head.W, q1.W * 2.9, q1.W * 3.1); Assert.InRange(nord.H, q1.H + q2.H - 0.5, q1.H + q2.H + 0.5);
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0692 Importbericht bleibt ehrlich: Zellhintergrund, Aufzählung, Fußnote und alte hMerge-Verbindung in Tabellenzellen werden weiter gemeldet; fett, Farbe, Zeilenwechsel und Link in Zellen nicht mehr")]
    public void Import_report_cells()
    {
        var d = Tmp();
        try
        {
            var f = Path.Combine(d, "a.docx"); File.Copy(Fremd("fremd-text.docx"), f);
            string cellOld = "<w:tc><w:tcPr><w:tcW w:w=\"3000\" w:type=\"dxa\"/></w:tcPr><w:p><w:r><w:t xml:space=\"preserve\">Q1</w:t></w:r></w:p></w:tc>";
            using (var z = ZipFile.Open(f, ZipArchiveMode.Update))
            {
                var e = z.GetEntry("word/document.xml")!; string xml; using (var r = new StreamReader(e.Open())) xml = r.ReadToEnd();
                Assert.Contains(cellOld, xml);
                xml = xml.Replace(cellOld, "<w:tc><w:tcPr><w:tcW w:w=\"3000\" w:type=\"dxa\"/></w:tcPr><w:p><w:pPr><w:jc w:val=\"center\"/></w:pPr><w:r><w:rPr><w:b/><w:color w:val=\"C00000\"/></w:rPr><w:t xml:space=\"preserve\">Q1</w:t></w:r><w:r><w:br/><w:t>neu</w:t></w:r></w:p></w:tc>");
                e.Delete(); using var w = new StreamWriter(z.CreateEntry("word/document.xml").Open()); w.Write(xml);
            }
            var doc = TextFiles.Open(f);
            Assert.DoesNotContain(doc.Import!.Verluste, v => v.Id.StartsWith("docx.zelle") || v.Id == "docx.verbunden"); // fett, Farbe, Ausrichtung, Zeilenwechsel: alles im Dokument
            var cell = doc.Blocks.OfType<Table>().Single().CellPara(1, 1);
            Assert.Equal("Q1\nneu", cell.Text); Assert.True(TextDoc.Has(cell, 0, 2, Fmt.Bold)); Assert.Equal("#C00000", TextDoc.InkAt(cell, 0, MarkKind.Color)); Assert.Equal(Align.Center, cell.Align);

            using (var z = ZipFile.Open(f, ZipArchiveMode.Update))
            {
                var e = z.GetEntry("word/document.xml")!; string xml; using (var r = new StreamReader(e.Open())) xml = r.ReadToEnd();
                xml = xml.Replace("<w:tc><w:tcPr><w:tcW w:w=\"3000\" w:type=\"dxa\"/></w:tcPr><w:p><w:r><w:t xml:space=\"preserve\">1200</w:t></w:r></w:p></w:tc>",
                    "<w:tc><w:tcPr><w:tcW w:w=\"3000\" w:type=\"dxa\"/><w:shd w:val=\"clear\" w:fill=\"FFF2CC\"/><w:hMerge w:val=\"restart\"/></w:tcPr><w:p><w:pPr><w:numPr><w:ilvl w:val=\"0\"/><w:numId w:val=\"1\"/></w:numPr></w:pPr><w:r><w:t>1200</w:t></w:r><w:r><w:footnoteReference w:id=\"5\"/></w:r></w:p></w:tc>");
                e.Delete(); using var w = new StreamWriter(z.CreateEntry("word/document.xml").Open()); w.Write(xml);
            }
            var ids = ImportCheck.For(f).Verluste.Select(v => v.Id).ToList();
            foreach (var id in new[] { "docx.zelle.format", "docx.zelle.liste", "docx.zelle.verweis", "docx.verbunden" }) Assert.Contains(id, ids);
        }
        finally { Directory.Delete(d, true); }
    }
}
