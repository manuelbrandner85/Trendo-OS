using System.Security.Cryptography;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Kreativ;
using Trendo.Core.Messdaten;
using Trendo.Core.Media;
using Trendo.Core.Office;
using Trendo.Core.Programs;
using Trendo.Core.Sys;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0725 (Audit D7 „Übrige Fachstudios“): je Programm die wichtigste Lücke – vom leeren Zustand über echte Klicks, Tasten und Ablegen bis zur echten Datei.
/// Ersetzt ist nur die Dateiauswahl des Systems (<see cref="MainWindow.PickOverride"/> / <see cref="MainWindow.FolderPickOverride"/> liefern echte Dateien und Ordner).
/// Die Fremdprogramm-Gegenprobe (LibreOffice, qpdf, Ical.Net) steht in <see cref="T0725_Echtlauf_Ui"/>.</summary>
[Collection("Claude")]
public class T0725_Fachstudios_Ui
{
    static string Tmp(string name) { var d = Path.Combine(Path.GetTempPath(), "trendo-t0725-" + name + "-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }
    static string Sha(string f) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f)));
    static void Pick(MainWindow w, string file) => w.PickOverride = _ => Task.FromResult<string[]?>([file]);
    static void Wait(Func<bool> done, int ms = 15000) { var sw = System.Diagnostics.Stopwatch.StartNew(); while (!done() && sw.ElapsedMilliseconds < ms) { Thread.Sleep(20); Settle(); } Settle(); }
    /// <summary>Programm in „Einfach“ öffnen und die Vorlage per Mausklick wählen.</summary>
    static ProgramDocument Start(MainWindow w, string key, string tpl)
    {
        w.Width = 1600; w.Height = 1200;
        w.ProgramSetLevel(UserLevel.Einfach); w.Show(key); Settle();
        RealClick(w, w.Find<Button>("prog-tpl-" + tpl)); Settle();
        return w.ProgramActive(key)!;
    }
    static T? TryV<T>(MainWindow w, string name) where T : Control { Settle(); return w.GetVisualDescendants().OfType<T>().FirstOrDefault(c => c.Name == name && c.IsEffectivelyVisible); }

    /// <summary>Fremde Word-Datei mit Titel, zwei Kapiteln (Überschrift 1), fett und kursiv – geschrieben mit dem DOCX-Schreiber.</summary>
    internal static string BookDocx(string dir)
    {
        var doc = new TextDoc { Blocks = [new Para("Sturmnacht", ParaStyle.Title), new Para("Erika Muster", ParaStyle.Subtitle), new Para("Ankunft", ParaStyle.Heading1)] };
        var p = new Para("Die Fähre legte spät an. ");
        doc.Blocks.Add(p); TextDoc.Format(p, 0, 0, Fmt.Bold, true);
        var q = new Para("Niemand wartete am Steg."); doc.Blocks.Add(q); TextDoc.Format(q, 0, 7, Fmt.Bold, true);
        doc.Blocks.Add(new Para("Der Leuchtturm", ParaStyle.Heading1));
        var r = new Para("Oben brannte noch Licht."); doc.Blocks.Add(r); TextDoc.Format(r, 5, 7, Fmt.Italic, true);
        var f = Path.Combine(dir, "Sturmnacht.docx"); DocxFormat.Write(doc, f); return f;
    }

    [AvaloniaFact(DisplayName = "T-0725 Bücher: leeres Buch → „Manuskript öffnen …“ übernimmt eine Word-Datei (Kapitel, fett, kursiv), Rückgängig holt den alten Stand, Ablegen einer .md; Export Word, Druck-PDF und OpenDocument enthalten die Kapitel")]
    public void Book_open_and_export()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            const string K = "buch-programm";
            var dir = Tmp("buch"); var src = BookDocx(dir); var sha = Sha(src);
            var d = Start(w, K, "leer");
            Pick(w, src);
            BueroAusbau_Ui.Cmd(w, "prog-cmd-book-open");
            var text = d.Text("text");
            Assert.Equal("Sturmnacht", d.Text("title"));
            Assert.Contains("# Ankunft", text); Assert.Contains("# Der Leuchtturm", text);
            Assert.Contains("**Niemand**", text); Assert.Contains("*brannte*", text);
            Assert.Equal(2, BookKit.Chapters(text).Count);
            Assert.Contains("übernommen: 2 Kapitel", w.ProgramMessage(K));
            Assert.Equal(sha, Sha(src)); // Original unverändert
            // Rückgängig (Knopf der Hülle) holt das leere Buch zurück, Wiederholen wieder das Manuskript
            BueroAusbau_Ui.Cmd(w, "prog-undo");
            Assert.StartsWith("# Kapitel 1", d.Text("text"));
            BueroAusbau_Ui.Cmd(w, "prog-redo");
            Assert.Contains("# Der Leuchtturm", d.Text("text"));
            // Ablegen einer Markdown-Datei aus dem Explorer aufs Manuskript
            var md = Path.Combine(dir, "Notizen.md"); File.WriteAllText(md, "# Prolog\n\nEs war einmal.\n");
            BueroAusbau_Ui.Drop(w, "book-text", md);
            Assert.StartsWith("# Prolog", d.Text("text"));
            BueroAusbau_Ui.Cmd(w, "prog-undo");
            Assert.Contains("# Ankunft", d.Text("text"));
            // Export (Word, Druck-PDF, OpenDocument stehen auch in „Einfach“ im Menüband)
            BueroAusbau_Ui.Cmd(w, "prog-export-docx");
            var docx = BueroAusbau_Ui.Exported(w, K);
            var back = DocxFormat.Read(docx);
            Assert.Equal(["Ankunft", "Der Leuchtturm"], back.Paras.Where(x => x.Style == ParaStyle.Heading1).Select(x => x.Text).ToArray());
            Assert.True(TextDoc.Has(back.Paras.First(x => x.Text.StartsWith("Niemand")), 0, 7, Fmt.Bold));
            Assert.Contains(back.Blocks, b => b is PageBreak);
            BueroAusbau_Ui.Cmd(w, "prog-export-pdf");
            var pdf = BueroAusbau_Ui.Exported(w, K);
            var pages = PdfView.PageCount(pdf);
            Assert.True(pages >= 3, "Titelseite + je Kapitel eine Seite, waren " + pages);
            var all = string.Concat(Enumerable.Range(0, pages).Select(i => PdfView.Text(pdf, i)));
            Assert.Contains("Der Leuchtturm", all); Assert.Contains("Niemand wartete", all);
            BueroAusbau_Ui.Cmd(w, "prog-export-odt");
            var odt = BueroAusbau_Ui.Exported(w, K);
            Assert.Contains("Oben brannte noch Licht.", OdtFormat.Read(odt).PlainText());
        }
        finally { w.Close(); }
    }

    static string Png(string dir, string name, byte shade)
    {
        var f = Path.Combine(dir, name);
        using var bmp = new SkiaSharp.SKBitmap(320, 240);
        using (var c = new SkiaSharp.SKCanvas(bmp)) { c.Clear(new SkiaSharp.SKColor(shade, (byte)(255 - shade), 90)); c.DrawCircle(160, 120, 40 + shade % 50, new SkiaSharp.SKPaint { Color = SkiaSharp.SKColors.White }); }
        using var img = SkiaSharp.SKImage.FromBitmap(bmp); using var data = img.Encode(SkiaSharp.SKEncodedImageFormat.Png, 90);
        File.WriteAllBytes(f, data.ToArray()); return f;
    }

    [AvaloniaFact(DisplayName = "T-0725 Fotos verwalten: Ordner wählen, Probelauf → Raster mit echten Vorschaubildern (Doppeltes markiert), Klick zeigt das Foto groß, „In Bildbearbeitung öffnen“ übergibt es")]
    public void Photos_grid_with_thumbnails()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            const string K = "fotos-verwalten-programm";
            var src = Tmp("fotos"); var dst = Tmp("ziel");
            for (var i = 0; i < 4; i++) Png(src, $"bild{i}.png", (byte)(40 + i * 50));
            File.Copy(Path.Combine(src, "bild2.png"), Path.Combine(src, "bild2 - Kopie.png"));
            var folders = new Queue<string>([src, dst]);
            MainWindow.FolderPickOverride = _ => folders.Dequeue();
            Start(w, K, "leer");
            BueroAusbau_Ui.Click(w, "photos-source");
            BueroAusbau_Ui.Click(w, "photos-target");
            BueroAusbau_Ui.Click(w, "photos-scan");
            Wait(() => TryV<WrapPanel>(w, "photos-grid") is not null);
            var grid = TryV<WrapPanel>(w, "photos-grid") ?? throw new Xunit.Sdk.XunitException("kein Raster nach dem Probelauf: " + w.ProgramMessage(K));
            Assert.Equal(5, grid.Children.Count);
            // echte Vorschaubilder: jedes Bild ist geladen (verkleinert dekodiert)
            Wait(() => grid.GetVisualDescendants().OfType<Image>().All(i => i.Source is Bitmap));
            Assert.All(grid.GetVisualDescendants().OfType<Image>(), i => Assert.IsAssignableFrom<Bitmap>(i.Source));
            var dupTiles = grid.Children.OfType<Border>().Where(b => b.BorderThickness.Left > 0).ToList();
            Assert.Single(dupTiles); // genau eine Kopie ist doppelt
            // Klick auf eine Kachel → groß mit Angaben
            var tile0 = grid.Children.OfType<Border>().First(b => b.BorderThickness.Left == 0);
            var name0 = ToolTip.GetTip(tile0) as string;
            BueroAusbau_Ui.Click(w, tile0.Name!);
            Assert.Equal(name0, TryV<TextBlock>(w, "photos-detail-name")?.Text);
            Wait(() => TryV<Image>(w, "photos-big")?.Source is Bitmap);
            Assert.IsAssignableFrom<Bitmap>(TryV<Image>(w, "photos-big")!.Source);
            // Rechtsklick-Menü auf einer Kachel, dann in die Bildbearbeitung
            BueroAusbau_Ui.Menu(w, grid.Children.OfType<Border>().Last().Name!, "photos-m-show");
            BueroAusbau_Ui.Click(w, "photos-edit");
            Assert.Equal("bild", w.CurrentView);
            Assert.Single(ImageDoc.Layers(w.ProgramActive("bild")!.Data));
            Assert.Empty(Directory.GetFiles(dst, "*", SearchOption.AllDirectories)); // Ansehen verändert nichts
        }
        finally { MainWindow.FolderPickOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0725 Texte zusammenfassen: „Datei öffnen …“ liest Word und PDF ein, Ablegen einer Textdatei; ein Scan-PDF ohne Text wird ehrlich gemeldet")]
    public void Summary_open_files()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            const string K = "zusammenfassen-programm";
            var dir = Tmp("sum");
            var sentences = new[] { "Die Stadt baut einen neuen Radweg entlang des Flusses.", "Er soll im Frühjahr fertig sein und kostet zwei Millionen Euro.", "Anwohner haben sich lange dafür eingesetzt.", "Während der Bauzeit wird die Uferstraße gesperrt.", "Der Radweg verbindet zwei Stadtteile direkt miteinander.", "Die Stadt rechnet mit vielen Pendlern auf dem Rad." };
            var doc = new TextDoc { Blocks = sentences.Select(s => (Trendo.Core.Office.Block)new Para(s)).ToList() };
            var docx = Path.Combine(dir, "Meldung.docx"); DocxFormat.Write(doc, docx);
            var pdf = Path.Combine(dir, "Meldung.pdf"); TextPdf.Export(doc, pdf);
            var d = Start(w, K, "leer");
            Pick(w, docx);
            BueroAusbau_Ui.Cmd(w, "prog-cmd-sum-open");
            Assert.Contains("neuen Radweg", d.Text("text")); Assert.Equal("Meldung", d.Text("title"));
            Assert.Contains("eingelesen", w.ProgramMessage(K));
            Assert.NotEmpty(w.GetVisualDescendants().OfType<StackPanel>().First(s => s.Name == "sum-result").Children.OfType<Grid>()); // Kernsätze rechts
            Pick(w, pdf);
            BueroAusbau_Ui.Cmd(w, "prog-cmd-sum-open");
            Assert.Contains("Uferstraße gesperrt", d.Text("text").Replace("\n", " "));
            var txt = Path.Combine(dir, "Notiz.txt"); File.WriteAllText(txt, "Morgen ist Markt. Der Markt beginnt um acht Uhr.");
            BueroAusbau_Ui.Drop(w, "sum-text", txt);
            Assert.StartsWith("Morgen ist Markt", d.Text("text"));
            // Scan-PDF ohne Textebene: ehrliche Meldung, Text bleibt
            var scan = Path.Combine(dir, "Scan.pdf"); PdfTools.NewBlank(scan);
            Pick(w, scan);
            BueroAusbau_Ui.Cmd(w, "prog-cmd-sum-open");
            Assert.Contains("keinen Text", w.ProgramMessage(K));
            Assert.StartsWith("Morgen ist Markt", d.Text("text"));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0725 Wissen merken: „Als Markdown sichern …“ schreibt je Seite eine .md, „Einlesen“ in einem frischen Trendo holt alle Seiten mit Verweisen zurück")]
    public void Knowledge_export_and_reimport()
    {
        var (w, _, _) = Ui.Open();
        var (w2, _, _) = Ui.Open();
        try
        {
            w.Wissen.Add("Müllabfuhr", "Gelbe Tonne alle zwei Wochen am Dienstag. Siehe [[Wertstoffhof]].");
            w.Wissen.Add("Wertstoffhof", "Samstags 9 bis 13 Uhr geöffnet.");
            w.Wissen.Add("Kundennummer: Stadtwerke", "K-4711");
            var to = Tmp("wissen");
            MainWindow.FolderPickOverride = _ => to;
            w.Show("ki-wissen"); Settle();
            BueroAusbau_Ui.Click(w, "wissen-export");
            Wait(() => w.WissenLastExport is not null);
            var outDir = w.WissenLastExport!;
            var files = Directory.GetFiles(outDir, "*.md").Select(Path.GetFileName).OrderBy(x => x).ToArray();
            Assert.Equal(["Kundennummer_ Stadtwerke.md", "Müllabfuhr.md", "Wertstoffhof.md"], files);
            Assert.Contains("[[Wertstoffhof]]", File.ReadAllText(Path.Combine(outDir, "Müllabfuhr.md")));
            // in einem zweiten, leeren Trendo über die Oberfläche einlesen
            w2.Show("ki-wissen"); Settle();
            w2.PickOverride = _ => Task.FromResult<string[]?>([outDir]);
            BueroAusbau_Ui.Click(w2, "wissen-folder-pickdir");
            if (w2.Wissen.All().Count == 0) BueroAusbau_Ui.Click(w2, "wissen-import");
            Assert.Equal(3, w2.Wissen.All().Count);
            Assert.Equal("Gelbe Tonne alle zwei Wochen am Dienstag. Siehe [[Wertstoffhof]].", w2.Wissen.ByTitle("Müllabfuhr")!.Text);
            Assert.Contains(w2.Wissen.Backlinks("Wertstoffhof"), x => x.Title == "Müllabfuhr");
        }
        finally { MainWindow.FolderPickOverride = null; w.Close(); w2.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0725 Messwerte: leere Messreihe → „CSV öffnen …“ (Dateiauswahl) und CSV auf die Tabelle ziehen, Rückgängig; Diagramm als PNG-Bild")]
    public void Measure_csv_open_and_png()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            const string K = "wissenschaft";
            var dir = Tmp("mess");
            var csv = Path.Combine(dir, "Pendel.csv"); File.WriteAllText(csv, "Länge [m];Dauer [s]\n0,2;0,90\n0,4;1,27\n0,6;1,55\n0,8;1,79\n1,0;2,01\n");
            var csv2 = Path.Combine(dir, "Temperatur.csv"); File.WriteAllText(csv2, "Minute;Grad\n0;80\n5;66\n10;55\n15;47\n");
            w.Width = 1600; w.Height = 1100;
            w.Show(K); Settle();
            RealClick(w, w.Find<Button>("prog-tpl-leer")); Settle();
            var d = w.ProgramActive(K)!;
            Pick(w, csv);
            BueroAusbau_Ui.Cmd(w, "prog-cmd-ms-open");
            var s = MeasureSheet.From(d.Data);
            Assert.Equal(5, s.Rows); Assert.Equal(2, s.Columns.Count);
            Assert.Contains("Pendel.csv", w.ProgramMessage(K));
            BueroAusbau_Ui.Drop(w, "ms-grid", csv2);
            Assert.Equal(4, MeasureSheet.From(d.Data).Rows);
            BueroAusbau_Ui.Cmd(w, "prog-undo");
            Assert.Equal(5, MeasureSheet.From(d.Data).Rows);
            BueroAusbau_Ui.Cmd(w, "prog-export-png");
            var png = BueroAusbau_Ui.Exported(w, K);
            using var bmp = SkiaSharp.SKBitmap.Decode(png);
            Assert.Equal((1600, 1000), (bmp.Width, bmp.Height));
            var colors = new HashSet<uint>(); for (var y = 0; y < bmp.Height; y += 7) for (var x = 0; x < bmp.Width; x += 7) colors.Add((uint)bmp.GetPixel(x, y));
            Assert.True(colors.Count > 3, "das Bild ist leer");
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0725 Karteikarten: Anki-Klartext über „Datei öffnen …“ (Kopfzeilen, HTML, Schlagwörter), Karte löschen und mit Lernstand zurückholen")]
    public void Flashcards_file_import_and_undo_delete()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            var dir = Tmp("lernen");
            var anki = Path.Combine(dir, "Hauptstaedte.txt");
            File.WriteAllText(anki, "#separator:tab\n#html:true\n#tags column:3\nFrankreich\tParis\tgeo europa\nItalien\tRom<br>(Lazio)\tgeo\nSpanien\tMadrid&nbsp;\t\nnur eine Spalte\n");
            w.Show("lernen"); Settle();
            BueroAusbau_Ui.Click(w, "learn-mode-karten");
            BueroAusbau_Ui.TypeInto(w, "learn-new-deck", "Erdkunde");
            var exp = TryV<Expander>(w, "learn-import-box")!; exp.IsExpanded = true; Settle();
            Pick(w, anki);
            BueroAusbau_Ui.Click(w, "learn-import-file");
            var cards = w.Learn.Store.Cards.Where(c => c.Deck == "Erdkunde").OrderBy(c => c.Front).ToList();
            Assert.Equal(["Frankreich", "Italien", "Spanien"], cards.Select(c => c.Front).ToArray());
            Assert.Equal("Rom (Lazio)", cards[1].Back); Assert.Equal("Madrid", cards[2].Back); Assert.Equal("Paris", cards[0].Back);
            Assert.Contains("3 Karte(n)", TryV<TextBlock>(w, "learn-msg")!.Text);
            // gelernt: Lernstand der Karte merken, löschen, zurückholen
            var fr = cards[0]; w.Learn.Answer(fr.Id, Grade.Gut);
            var (reps, due) = (fr.Reps, fr.Due);
            var row = w.GetVisualDescendants().OfType<TextBox>().First(t => t.Name!.StartsWith("learn-f-") && t.Text == "Frankreich").Name!.Replace("learn-f-", "");
            BueroAusbau_Ui.Click(w, "learn-del-" + row);
            Assert.DoesNotContain(w.Learn.Store.Cards, c => c.Front == "Frankreich");
            BueroAusbau_Ui.Click(w, "learn-undo-del");
            var again = w.Learn.Store.Cards.Single(c => c.Front == "Frankreich");
            Assert.Equal((fr.Id, reps, due), (again.Id, again.Reps, again.Due));
            Assert.Null(TryV<Button>(w, "learn-undo-del"));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0725 Neues Projekt: Beispiel-Vorhaben → „Schritte als Tabelle (.csv)“ und „Zeitplan als Kalenderdatei (.ics)“ landen in der Mappe; Ical.Net liest jeden Schritt mit Zeitraum")]
    public void Project_export_csv_ics()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.Show("vorhaben"); Settle();
            BueroAusbau_Ui.Click(w, "suite-demo");
            var dir = w.SuiteOpenDir!;
            var steps = CreationSuite.Steps(dir); var bars = CreationSuite.Bars(steps);
            Assert.True(bars.Count >= 3, "Beispiel ohne Zeitplan");
            BueroAusbau_Ui.Click(w, "suite-export-csv");
            var csv = w.SuiteLastExport!;
            Assert.Equal(dir, Path.GetDirectoryName(csv));
            var lines = File.ReadAllLines(csv, System.Text.Encoding.UTF8);
            Assert.StartsWith("Nr;Schritt;Status;Beginn;Ende", lines[0]);
            Assert.Equal(steps.Count + 1, lines.Length);
            Assert.Contains(bars[0].Start.ToString("dd.MM.yyyy"), lines[bars[0].No]);
            BueroAusbau_Ui.Click(w, "suite-export-ics");
            var ics = w.SuiteLastExport!;
            var cal = Ical.Net.Calendar.Load(File.ReadAllText(ics))!;
            Assert.Equal(bars.Count, cal.Events.Count);
            var first = cal.Events.Single(e => e.Summary!.EndsWith(bars[0].Step.Text));
            Assert.Equal(bars[0].Start.Date, first.DtStart!.Value.Date);
            Assert.Equal(bars[0].End.Date.AddDays(1), first.DtEnd!.Value.Date);
            Assert.Contains("Gespeichert:", w.GetLogicalTexts().FirstOrDefault(t => t.StartsWith("Gespeichert:")) ?? "Gespeichert:");
        }
        finally { w.Close(); }
    }

    /// <summary>Fremder Brief, wie ihn Word-Nutzer schreiben: Absenderzeile, Anschrift, Ort/Datum, Betreff, Anrede, zwei Absätze, Gruß, Anlagen.</summary>
    internal static string[] LetterLines =>
    [
        "Erika Muster · Lindenweg 3 · 12345 Musterstadt", "",
        "Stadtwerke Musterstadt", "Kundenservice", "Hauptstraße 1", "12345 Musterstadt", "",
        "Musterstadt, 12.03.2026", "",
        "Kündigung meines Stromvertrags 4711", "",
        "Sehr geehrte Damen und Herren,", "",
        "hiermit kündige ich meinen Stromvertrag fristgerecht zum 30.06.2026.", "",
        "Bitte bestätigen Sie mir die Kündigung schriftlich.", "",
        "Mit freundlichen Grüßen", "", "Erika Muster", "",
        "Anlage: Kopie des Vertrags",
    ];

    [AvaloniaFact(DisplayName = "T-0725 Briefe: „Brief öffnen (Word …)“ übernimmt einen fremden Brief in die Felder (Anschrift, Datum, Betreff, Anrede, Text, Gruß, Anlage), Speichern legt einen neuen Brief an, das Original bleibt; Ablegen geht auch")]
    public async Task Letter_open_foreign()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            var dir = Tmp("brief");
            var doc = new TextDoc { Blocks = LetterLines.Select(l => (Trendo.Core.Office.Block)new Para(l)).ToList() };
            var src = Path.Combine(dir, "Kuendigung.docx"); DocxFormat.Write(doc, src); var sha = Sha(src);
            await Ui.OpenLetter(w); Settle();
            Pick(w, src);
            BueroAusbau_Ui.Click(w, "letter-open-foreign");
            var l = w.Letter;
            Assert.Equal("Kündigung meines Stromvertrags 4711", l.Subject);
            Assert.Equal("Stadtwerke Musterstadt\nKundenservice\nHauptstraße 1\n12345 Musterstadt", l.Recipient);
            Assert.Equal(new DateTime(2026, 3, 12), l.Date); Assert.Equal("Musterstadt", l.Place);
            Assert.Equal("Sehr geehrte Damen und Herren,", l.Salutation);
            Assert.Equal("hiermit kündige ich meinen Stromvertrag fristgerecht zum 30.06.2026.\n\nBitte bestätigen Sie mir die Kündigung schriftlich.", l.Body);
            Assert.Equal("Mit freundlichen Grüßen", l.Closing); Assert.Equal("Kopie des Vertrags", l.Attachments);
            Assert.Contains("erkannt:", TryV<TextBlock>(w, "letter-msg")!.Text);
            // Text ändern (Tastatur) und als neuen Brief speichern
            BueroAusbau_Ui.TypeInto(w, "letter-body", l.Body + "\n\nVielen Dank.");
            // Absender steht in einem frischen Trendo noch nicht in den Einstellungen – wie eine Person in Schritt 2 eintragen
            BueroAusbau_Ui.Click(w, "letter-wiz-tab-2");
            BueroAusbau_Ui.TypeInto(w, "letter-sender-name", "Erika Muster");
            BueroAusbau_Ui.TypeInto(w, "letter-sender-street", "Lindenweg 3");
            BueroAusbau_Ui.TypeInto(w, "letter-sender-city", "12345 Musterstadt");
            BueroAusbau_Ui.Click(w, "letter-save");
            Wait(() => new LetterArchive(w.ProjectsRootForTests).List().Count > 0);
            Assert.True(new LetterArchive(w.ProjectsRootForTests).List().Count > 0, "nicht gespeichert: " + TryV<TextBlock>(w, "letter-msg")?.Text);
            var saved = new LetterArchive(w.ProjectsRootForTests).List().First();
            Assert.Contains("Vielen Dank.", Ooxml.DocxText(saved)); Assert.Contains("Kündigung meines Stromvertrags 4711", Ooxml.DocxText(saved));
            Assert.Equal(sha, Sha(src));
            // Ablegen einer Textdatei auf das Formular
            var txt = Path.Combine(dir, "Anfrage.txt"); File.WriteAllLines(txt, ["Betreff: Anfrage Termin", "", "Hallo Frau Muster,", "", "passt Ihnen Montag?", "", "Viele Grüße"]);


            BueroAusbau_Ui.Drop(w, "letter-form-scroll", txt);
            Assert.Equal(("Anfrage Termin", "Hallo Frau Muster,", "passt Ihnen Montag?", "Viele Grüße"), (w.Letter.Subject, w.Letter.Salutation, w.Letter.Body, w.Letter.Closing));
        }
        finally { w.Close(); }
    }
}
