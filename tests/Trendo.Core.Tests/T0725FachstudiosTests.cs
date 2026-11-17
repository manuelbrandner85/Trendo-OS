using Trendo.Core;
using Trendo.Core.Kreativ;
using Trendo.Core.Office;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0725 (Audit D7): Kernlogik der übrigen Fachstudios – Buch ↔ Textdokument, fremder Brief in Felder, Kartenliste aus Datei, Wissen als Markdown,
/// Vorhaben als CSV/ICS. Die Wege über die Oberfläche und mit Fremdprogrammen prüfen T0725_Fachstudios_Ui und T0725_Echtlauf_Ui.</summary>
public class T0725_FachstudiosTests
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-t0725c-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }

    [Fact(DisplayName = "T-0725 Buch: Manuskript → Textdokument (Titelseite, je Kapitel neue Seite, fett/kursiv/Aufzählung) → DOCX → zurück ergibt dasselbe Manuskript")]
    public void Book_roundtrip()
    {
        const string ms = "# Ankunft\n\nDie Fähre kam **spät**.\n\n- Koffer\n- Schirm\n\n## Am Hafen\n\nEs regnete *leise*.\n\n# Abschied\n\nEnde.\n";
        var doc = BookKit.ToTextDoc("Sturmnacht", "Erika Muster", ms);
        Assert.Equal(ParaStyle.Title, doc.Paras.First().Style);
        Assert.Equal(2, doc.Blocks.Count(b => b is PageBreak));
        var f = Path.Combine(Tmp(), "b.docx"); DocxFormat.Write(doc, f);
        var (title, back) = BookKit.FromTextDoc(DocxFormat.Read(f));
        Assert.Equal("Sturmnacht", title);
        Assert.Equal(ms, back);
    }

    [Fact(DisplayName = "T-0725 Buch: Text ohne Überschriften bekommt „# Kapitel 1“, Markdown bleibt unverändert, unbekanntes Format wird abgelehnt")]
    public void Book_import_edges()
    {
        var d = Tmp();
        var txt = Path.Combine(d, "roh.txt"); File.WriteAllText(txt, "Erste Zeile.\nZweite Zeile.");
        var r = BookKit.Import(txt);
        Assert.StartsWith("# Kapitel 1\n\nErste Zeile.", r.Manuscript); Assert.Equal(1, r.Chapters);
        var md = Path.Combine(d, "m.md"); File.WriteAllText(md, "# A\n\nx\n");
        Assert.Equal("# A\n\nx\n", BookKit.Import(md).Manuscript);
        Assert.Throws<ArgumentException>(() => BookKit.Import(Path.Combine(d, "x.pdf")));
        Assert.Throws<InvalidOperationException>(() => BookKit.ToTextDoc("T", "", ""));
    }

    [Fact(DisplayName = "T-0725 Brief: Zeilen eines fremden Briefs werden in Anschrift, Ort/Datum, Betreff, Anrede, Text, Gruß und Anlagen geteilt; ohne Anrede bleibt der ganze Text")]
    public void Letter_split()
    {
        string[] lines = ["Erika Muster · Lindenweg 3 · 12345 Musterstadt", "", "Firma Beispiel GmbH", "Hauptstraße 1", "12345 Musterstadt", "", "Musterstadt, den 2.3.2026", "", "Betreff: Rechnung 17", "", "Sehr geehrter Herr Beispiel,", "", "Zeile eins.", "", "• Punkt", "", "Freundliche Grüße,", "", "Anlagen:", "Rechnung", "Lieferschein"];
        var (d, found) = LetterImport.Split(lines, new LetterDraft { SenderName = "X" });
        Assert.Equal(("Rechnung 17", "Firma Beispiel GmbH\nHauptstraße 1\n12345 Musterstadt", new DateTime(2026, 3, 2), "Musterstadt"), (d.Subject, d.Recipient, d.Date, d.Place));
        Assert.Equal(("Sehr geehrter Herr Beispiel,", "Zeile eins.\n\n– Punkt", "Freundliche Grüße", "Rechnung\nLieferschein"), (d.Salutation, d.Body, d.Closing, d.Attachments));
        Assert.Equal("X", d.SenderName);
        var (d2, _) = LetterImport.Split(["Nur ein Text.", "Noch einer."], new LetterDraft());
        Assert.Equal("Nur ein Text.\n\nNoch einer.", d2.Body);
        Assert.Throws<InvalidDataException>(() => LetterImport.Split(["", ""], new LetterDraft()));
    }

    [Fact(DisplayName = "T-0725 Karteikarten: Datei-Import (eigene CSV, Anki-Klartext) – Kopfzeilen, HTML, weitere Felder, Doppelte und leere Zeilen; Löschen und Zurücklegen behält den Lernstand")]
    public void Learn_file_import_and_restore()
    {
        var d = Tmp(); var box = new LearnBox(d);
        var f = Path.Combine(d, "k.txt"); File.WriteAllText(f, "#separator:tab\nHaus\tHouse\ttag\nBaum\t<b>tree</b>\nleer\t\nHaus\tHome\n");
        var (a, s) = box.ImportFile("Englisch", f);
        Assert.Equal((2, 2), (a, s));
        Assert.Equal("tree", box.Store.Cards.Single(c => c.Front == "Baum").Back);
        var csv = Path.Combine(d, "k.csv"); File.WriteAllText(csv, box.Export("Englisch"));
        var box2 = new LearnBox(Tmp()); Assert.Equal((2, 0), box2.ImportFile("Englisch", csv)); // eigene CSV wieder einlesbar
        var bin = Path.Combine(d, "x.apkg"); File.WriteAllBytes(bin, [80, 75, 0, 0, 1]);
        Assert.Throws<InvalidDataException>(() => box.ImportFile("E", bin));
        var c = box.Store.Cards.First(); box.Answer(c.Id, Grade.Gut); var reps = c.Reps;
        var gone = box.Take(c.Id)!; Assert.DoesNotContain(box.Store.Cards, x => x.Id == c.Id);
        box.Restore(gone); box.Restore(gone);
        Assert.Single(box.Store.Cards, x => x.Id == c.Id); Assert.Equal(reps, new LearnBox(d).Store.Cards.Single(x => x.Id == c.Id).Reps); // gespeichert
    }

    [Fact(DisplayName = "T-0725 Wissen: Export als Markdown (ein Dateiname je Titel, gleiche Titel mit „ (2)“, verbotene Zeichen ersetzt) und Wiedereinlesen")]
    public void Knowledge_markdown_roundtrip()
    {
        var d = Tmp(); var s = new KnowledgeStore(d, new AuditLog(Path.Combine(d, "a.jsonl")));
        s.Add("Termin: Arzt", "Dienstag 9 Uhr"); s.Add("Termin: Arzt", "Donnerstag 10 Uhr"); s.Add("Rezept", "Mehl, Eier, [[Termin: Arzt]]");
        var outDir = Path.Combine(d, "export");
        Assert.Equal(3, s.ExportMarkdown(outDir));
        Assert.Equal(["Rezept.md", "Termin_ Arzt (2).md", "Termin_ Arzt.md"], Directory.GetFiles(outDir).Select(Path.GetFileName).OrderBy(x => x, StringComparer.Ordinal).ToArray());
        var d2 = Tmp(); var s2 = new KnowledgeStore(d2, new AuditLog(Path.Combine(d2, "a.jsonl")));
        Assert.Equal((3, 0), s2.Import(outDir));
        Assert.Equal("Mehl, Eier, [[Termin: Arzt]]", s2.ByTitle("Rezept")!.Text);
    }

    [Fact(DisplayName = "T-0725 Vorhaben: CSV mit allen Schritten (Semikolon, Anführungszeichen) und ICS über die volle Dauer jedes Schritts; ohne Termine ehrliche Meldung")]
    public void Project_exports()
    {
        var dir = CreationSuite.Create(Tmp(), "video", "Urlaub; Film", new DateTime(2026, 12, 1));
        var steps = CreationSuite.Steps(dir); var bars = CreationSuite.Bars(steps);
        var csv = CreationSuite.ExportCsv(dir).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(steps.Count + 1, csv.Length);
        Assert.Contains($"{bars[0].Start:dd.MM.yyyy};{bars[0].End:dd.MM.yyyy};{bars[0].Days}", csv[bars[0].No]);
        var cal = Ical.Net.Calendar.Load(CreationSuite.ExportIcs(dir))!;
        Assert.Equal(bars.Count, cal.Events.Count);
        Assert.All(cal.Events, e => Assert.Contains(Path.GetFileName(dir), e.Summary));
        var empty = CreationSuite.Create(Tmp(), "video", "Leer", null);
        foreach (var st in CreationSuite.Steps(empty)) CreationSuite.SetDue(empty, st.Line, null);
        Assert.Throws<InvalidOperationException>(() => CreationSuite.ExportIcs(empty));
    }
}
