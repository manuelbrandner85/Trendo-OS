using System.IO.Compression;
using Trendo.Core.Office;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0682: Importbericht – was fremde Office-Dateien enthalten, das Trendo nicht übernimmt.
/// Die Fremddateien unter tests/Daten/office stammen nicht aus Trendos Schreibern (handgeschriebenes Roh-OOXML/-ODF, siehe erzeugen.py).</summary>
public class T0682_ImportBericht
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-t0682-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

    static string Fremd(string name)
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "Trendo.sln"))) return Path.Combine(d.FullName, "tests", "Daten", "office", name);
        throw new FileNotFoundException(name);
    }

    static string[] Ids(ImportReport r) => r.Verluste.Select(v => v.Id).Order().ToArray();
    static ImportLoss One(ImportReport r, string id) => r.Verluste.Single(v => v.Id == id);
    static string Part(string file, string part) { using var z = ZipFile.OpenRead(file); using var s = new StreamReader(z.GetEntry(part)!.Open()); return s.ReadToEnd(); }
    static string AllXml(string file) { using var z = ZipFile.OpenRead(file); return string.Concat(z.Entries.Where(e => e.FullName.EndsWith(".xml") || e.FullName.EndsWith(".rels")).Select(e => { using var s = new StreamReader(e.Open()); return s.ReadToEnd(); })); }
    static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    [Fact(DisplayName = "T-0682: fremde DOCX – Bericht nennt genau verbundene Zellen, Textfeld und die Besonderheiten der Kopfzeile; beim Speichern gehen sie wirklich verloren")]
    public void Docx_fremd()
    {
        var f = Fremd("fremd-text.docx");
        var doc = TextFiles.Open(f);
        var r = doc.Import!;
        Assert.Equal(["docx.kopf.weitere", "docx.kopf.zeilen", "docx.textfeld", "docx.verbunden"], Ids(r));
        Assert.Equal(2, One(r, "docx.verbunden").Anzahl); Assert.Equal("Tabelle 1", One(r, "docx.verbunden").Wo); // eine Zelle über drei Spalten, eine über zwei Zeilen
        Assert.Equal(1, One(r, "docx.textfeld").Anzahl); Assert.Contains("Hinweis im Textfeld", One(r, "docx.textfeld").Wo); // Word legt das Feld doppelt ab (neu + Rückfall) – gezählt wird es einmal
        Assert.Contains("Kopfzeile", One(r, "docx.kopf.zeilen").Was); Assert.Contains("erste Seite", One(r, "docx.kopf.weitere").Was);
        Assert.True(r.HasLoss); Assert.Contains("Verbundene Zellen", r.Kurz()); Assert.Contains("Tabelle 1", r.Lang());

        // Der Bericht behauptet nichts: Genau das fehlt im gelesenen Dokument und in der von Trendo geschriebenen Datei.
        Assert.Contains("Umsatz nach Quartal", doc.PlainText());          // Tabellentext ist da …
        Assert.DoesNotContain("Hinweis im Textfeld", doc.PlainText());     // … das Textfeld nicht
        Assert.Equal("Musterfirma GmbH Beispielweg 1, 12345 Musterstadt", doc.Page.Header); // zwei Zeilen wurden eine
        var d = Tmp();
        try
        {
            var copy = Path.Combine(d, "kopie.docx"); TextFiles.Save(doc, copy);
            Assert.Contains("gridSpan", Part(f, "word/document.xml")); Assert.Contains("txbxContent", Part(f, "word/document.xml"));
            var xml = AllXml(copy);
            Assert.DoesNotContain("gridSpan", xml); Assert.DoesNotContain("vMerge", xml); Assert.DoesNotContain("txbxContent", xml); Assert.DoesNotContain("Deckblatt", xml);
            Assert.False(ImportCheck.For(copy).HasLoss); // Trendos eigene Datei: nichts mehr zu verlieren
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0682: fremde XLSX – Bericht nennt genau Zahlenformat, verbundene Zellen und Diagramm-Einzelheiten; beim Speichern gehen sie wirklich verloren")]
    public void Xlsx_fremd()
    {
        var f = Fremd("fremd-tabelle.xlsx");
        var doc = SheetFiles.Open(f);
        var r = doc.Import!;
        Assert.Equal(["xlsx.diagramm", "xlsx.verbunden", "xlsx.zahlenformat"], Ids(r));
        Assert.Equal(1, One(r, "xlsx.verbunden").Anzahl); Assert.Contains("Tabelle1: A1:C1", One(r, "xlsx.verbunden").Wo);
        Assert.Equal(6, One(r, "xlsx.zahlenformat").Anzahl); Assert.Contains("0.000", One(r, "xlsx.zahlenformat").Wo); // sechs Zahlenzellen mit dem Format
        Assert.Contains("Messwerte je Probe", One(r, "xlsx.diagramm").Wo);

        Assert.Equal("1,2345", doc.Sheets[0].Get("B3").Raw); Assert.Equal(NumFmt.Zahl, doc.Sheets[0].Get("B3").Format); // die Zahl bleibt; aus „0,000 kg“ wird Trendos „Zahl“ mit zwei Nachkommastellen ohne Einheit
        Assert.Single(doc.Sheets[0].Charts); // das Diagramm selbst bleibt (Art, Bereich, Titel)
        var d = Tmp();
        try
        {
            var copy = Path.Combine(d, "kopie.xlsx"); SheetFiles.Save(doc, copy);
            var xml = AllXml(copy);
            Assert.DoesNotContain("mergeCell", xml); Assert.DoesNotContain("0.000", xml); Assert.DoesNotContain("Messwert<", xml); Assert.DoesNotContain("C0504D", xml); // Achsentitel und Reihenfarben sind weg
            Assert.Contains("Messwerte je Probe", xml);
            Assert.False(ImportCheck.For(copy).HasLoss);
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0682: fremde PPTX – Bericht nennt genau zwei Textformate in einem Feld, die Animation und das fremde Design; beim Speichern gehen sie wirklich verloren")]
    public void Pptx_fremd()
    {
        var f = Fremd("fremd-praesentation.pptx");
        var doc = SlideFiles.Open(f);
        var r = doc.Import!;
        Assert.Equal(["pptx.animation", "pptx.design", "pptx.textformat"], Ids(r));
        Assert.Equal(1, One(r, "pptx.animation").Anzahl); Assert.Equal("Folie 1", One(r, "pptx.animation").Wo);
        Assert.Equal(1, One(r, "pptx.textformat").Anzahl); Assert.Contains("Der Umsatz ist um 12 Prozent gestiegen.", One(r, "pptx.textformat").Wo);

        var box = doc.Slides[0].Shapes.Single(s => s.Text.Contains("12 Prozent"));
        Assert.Equal(18, box.FontSize); Assert.False(box.Bold); Assert.Equal(Animation.Keine, box.Animation); // ein Format für das ganze Feld, keine Animation
        var d = Tmp();
        try
        {
            var copy = Path.Combine(d, "kopie.pptx"); SlideFiles.Save(doc, copy);
            var xml = AllXml(copy);
            Assert.Contains("2800", Part(f, "ppt/slides/slide1.xml")); Assert.Contains("animRot", Part(f, "ppt/slides/slide1.xml"));
            Assert.DoesNotContain("C00000", xml); Assert.DoesNotContain("animRot", xml); Assert.DoesNotContain("Office Theme", xml);
            Assert.False(ImportCheck.For(copy).HasLoss);
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0682: fremde ODT, ODS und ODP – Bericht nennt verbundene Zellen, Textrahmen, Feld, Zellformat, Notiz, Textformat und Animation")]
    public void Odf_fremd()
    {
        var t = TextFiles.Open(Fremd("fremd-text.odt"));
        Assert.Equal(["odt.feld", "odt.textfeld", "odt.verbunden"], Ids(t.Import!));
        Assert.Contains("date", One(t.Import!, "odt.feld").Wo); Assert.Contains("Notiz im Textrahmen", One(t.Import!, "odt.textfeld").Wo);
        Assert.DoesNotContain("Notiz im Textrahmen", t.PlainText()); Assert.DoesNotContain("01.01.2026", t.PlainText()); // beides fehlt wirklich
        Assert.Equal("Musterverein e. V.", t.Page.Header); // die einfache Kopfzeile kommt an und steht deshalb nicht im Bericht

        var s = SheetFiles.Open(Fremd("fremd-tabelle.ods"));
        Assert.Equal(["ods.format", "ods.notiz", "ods.verbunden"], Ids(s.Import!));
        Assert.Equal("Fahrten", One(s.Import!, "ods.verbunden").Wo);
        Assert.Equal("42,5", s.Sheets[0].Get("B2").Raw); Assert.Null(s.Sheets[0].Get("B2").Note); Assert.False(s.Sheets[0].Get("A1").Bold); // Wert da, Notiz und Fett nicht

        var p = SlideFiles.Open(Fremd("fremd-praesentation.odp"));
        Assert.Equal(["odp.animation", "odp.design", "odp.textformat"], Ids(p.Import!));
        Assert.Contains("Beginn um 15 Uhr", One(p.Import!, "odp.textformat").Wo);
        Assert.All(p.Slides[0].Shapes, x => Assert.Equal(Animation.Keine, x.Animation));
    }

    [Fact(DisplayName = "T-0682: eigene Dateien bleiben ohne Hinweis – alle Vorlagen und alle Funktionen von Text, Tabelle und Präsentation ergeben einen leeren Bericht")]
    public void Eigene_Dateien_ohne_Verlust()
    {
        var d = Tmp();
        try
        {
            void Clean(string file) { var r = ImportCheck.For(file); Assert.True(!r.HasLoss, Path.GetFileName(file) + ": " + r.Lang()); }

            // Text: jede Vorlage und ein Dokument mit allem, als DOCX und ODT
            var docs = TextTemplates.Builtin.Select(t => (t.Id, Doc: TextTemplates.Create(t.Id))).Append(("alles", TextAlles())).ToList();
            foreach (var (id, doc) in docs)
                foreach (var ext in new[] { ".docx", ".odt" }) { var f = Path.Combine(d, $"text-{id}{ext}"); TextFiles.Save(doc, f); Clean(f); Assert.False(TextFiles.Open(f).Import!.HasLoss); }

            // Tabelle: jede Vorlage und eine Mappe mit allem
            foreach (var (id, doc) in SheetTemplates.Builtin.Select(t => (t.Id, SheetTemplates.Create(t.Id))).Append(("alles", SheetAlles())))
            { var f = Path.Combine(d, $"tabelle-{id}.xlsx"); SheetFiles.Save(doc, f); Clean(f); Assert.False(SheetFiles.Open(f).Import!.HasLoss); }

            // Präsentation: jedes Design mit allen Layouts, Objekten, Übergängen und Animationen
            foreach (var th in SlideThemes.All)
            { var f = Path.Combine(d, $"folien-{th.Id}.pptx"); SlideFiles.Save(SlideAlles(th), f); Clean(f); Assert.False(SlideFiles.Open(f).Import!.HasLoss); }

            // Formate ohne Paket: kein Bericht, kein Fehler
            var txt = Path.Combine(d, "a.txt"); File.WriteAllText(txt, "Hallo"); Assert.False(TextFiles.Open(txt).Import!.HasLoss);
            var csv = Path.Combine(d, "a.csv"); File.WriteAllText(csv, "a;b\n1;2"); Assert.False(SheetFiles.Open(csv).Import!.HasLoss);
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0682: echte LibreOffice-Dateien (26.2.4, aus reports/t0223-office) und die Word-Briefvorlagen – nur Inhalte, die Trendo kennt: kein falscher Alarm")]
    public void Fremdprogramm_ohne_Verlust_kein_Alarm()
    {
        // Von LibreOffice aus Trendo-Dokumenten umgewandelt (Beleg reports/t0223-office/text-abgleich/ERGEBNIS.txt) bzw. mit Word für Mac geschrieben (docProps/app.xml der Vorlagen).
        foreach (var rel in new[] { "reports/t0223-office/text-abgleich/lo-aus-docx/probe.odt", "reports/t0223-office/text-abgleich/lo-aus-odt/probe.docx",
            "src/Trendo.App/Assets/templates/DIN5008-Geschaeftsbrief-FormB.docx", "src/Trendo.App/Assets/templates/DIN5008-Privatbrief-FormB.docx" })
        {
            var f = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Fremd("x"))!)!)!)!, rel);
            Assert.True(File.Exists(f), rel);
            var r = ImportCheck.For(f);
            Assert.True(!r.HasLoss, rel + ": " + r.Lang());
        }
    }

    [Fact(DisplayName = "T-0682: weitere Verluste werden einzeln erkannt – Makros, eingebettete Dateien, gemeinsame Formeln, benannte Bereiche, gruppierte Objekte, unbekannte Bausteine")]
    public void Einzelne_Verluste()
    {
        var d = Tmp();
        try
        {
            // DOCX: Formatvorlage, Feld, Kommentar mit Verfasser, Makro-Teil, unbekannter Baustein
            var docx = Path.Combine(d, "a.docx"); File.Copy(Fremd("fremd-text.docx"), docx);
            Edit(docx, "word/document.xml", x => x.Replace("<w:p><w:r><w:t xml:space=\"preserve\">Schlussabsatz nach dem Textfeld.</w:t></w:r></w:p>",
                "<w:p><w:pPr><w:pStyle w:val=\"Firmenstil\"/></w:pPr><w:r><w:t>Stand </w:t></w:r><w:fldSimple w:instr=\" DATE \"><w:r><w:t>01.01.2026</w:t></w:r></w:fldSimple><w:ins w:id=\"1\" w:author=\"Erika Muster\"><w:r><w:rPr><w:smallCaps/></w:rPr><w:t>neu</w:t></w:r></w:ins><w:neuesElement/></w:p>"),
                ("word/vbaProject.bin", "x"), ("word/embeddings/Tabelle1.xlsx", "x"), ("word/sonderteil.xml", "<a/>"));
            var r = ImportCheck.For(docx);
            foreach (var id in new[] { "docx.vorlage", "docx.feld", "docx.verfasser", "docx.zeichenformat", "docx.makro", "docx.einbettung", "docx.unbekannt", "docx.teile" }) Assert.True(r.Has(id), id + " fehlt: " + r.Lang());
            Assert.Contains("Firmenstil", One(r, "docx.vorlage").Wo); Assert.Contains("DATE", One(r, "docx.feld").Wo); Assert.Contains("neuesElement", One(r, "docx.unbekannt").Wo); Assert.Contains("sonderteil.xml", One(r, "docx.teile").Wo);

            // XLSX: gemeinsame Formel, benannter Bereich, kursive Schrift, ausgeblendetes Blatt, Pivot-Teil
            var xlsx = Path.Combine(d, "a.xlsx"); File.Copy(Fremd("fremd-tabelle.xlsx"), xlsx);
            Edit(xlsx, "xl/worksheets/sheet1.xml", x => x.Replace("<c r=\"C5\" s=\"1\"><v>12</v></c>", "<c r=\"C5\" s=\"1\"><f t=\"shared\" si=\"0\"/><v>12</v></c><c r=\"D5\" s=\"2\"><v>1</v></c>"), ("xl/pivotTables/pivotTable1.xml", "<a/>"));
            Edit(xlsx, "xl/workbook.xml", x => x.Replace("sheetId=\"1\"", "sheetId=\"1\" state=\"hidden\"").Replace("</sheets>", "</sheets><definedNames><definedName name=\"Steuersatz\">Tabelle1!$B$3</definedName></definedNames>"));
            Edit(xlsx, "xl/styles.xml", x => x.Replace("<fonts count=\"1\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts>", "<fonts count=\"2\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font><font><i/><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts>")
                .Replace("</cellXfs>", "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"0\" borderId=\"0\" xfId=\"0\"><alignment horizontal=\"center\" wrapText=\"1\"/></xf></cellXfs>"));
            r = ImportCheck.For(xlsx);
            foreach (var id in new[] { "xlsx.formel.gemeinsam", "xlsx.name", "xlsx.schrift", "xlsx.ausrichtung", "xlsx.blatt.versteckt", "xlsx.pivot" }) Assert.True(r.Has(id), id + " fehlt: " + r.Lang());
            Assert.Contains("Steuersatz", One(r, "xlsx.name").Wo);

            // PPTX: Gruppe, Form mit Verlauf, eingebettete Datei
            var pptx = Path.Combine(d, "a.pptx"); File.Copy(Fremd("fremd-praesentation.pptx"), pptx);
            Edit(pptx, "ppt/slides/slide1.xml", x => x.Replace("</p:spTree>", "<p:grpSp><p:nvGrpSpPr><p:cNvPr id=\"9\" name=\"Gruppe\"/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr><p:grpSpPr/></p:grpSp>"
                + "<p:sp><p:nvSpPr><p:cNvPr id=\"10\" name=\"Stern\"/><p:cNvSpPr/><p:nvPr/></p:nvSpPr><p:spPr><a:prstGeom prst=\"star5\"><a:avLst/></a:prstGeom><a:gradFill/></p:spPr></p:sp></p:spTree>"), ("ppt/embeddings/Mappe1.xlsx", "x"));
            r = ImportCheck.For(pptx);
            foreach (var id in new[] { "pptx.gruppe", "pptx.form", "pptx.effekt", "pptx.einbettung" }) Assert.True(r.Has(id), id + " fehlt: " + r.Lang());

            // Kaputte Datei: Der Bericht sagt, dass er nicht prüfen konnte, statt „alles in Ordnung“ zu melden
            var bad = Path.Combine(d, "kaputt.docx"); File.WriteAllText(bad, "kein zip");
            Assert.True(ImportCheck.For(bad).Has("pruefung"));
        }
        finally { Directory.Delete(d, true); }
    }

    static void Edit(string zip, string part, Func<string, string> change, params (string Name, string Content)[] add)
    {
        using var z = ZipFile.Open(zip, ZipArchiveMode.Update);
        var e = z.GetEntry(part)!; string text; using (var s = new StreamReader(e.Open())) text = s.ReadToEnd();
        var neu = change(text); Assert.NotEqual(text, neu);
        e.Delete(); using (var w = new StreamWriter(z.CreateEntry(part).Open())) w.Write(neu);
        foreach (var (n, c) in add) { using var w = new StreamWriter(z.CreateEntry(n).Open()); w.Write(c); }
    }

    static TextDoc TextAlles()
    {
        var d = new TextDoc { Title = "Alles" };
        d.Page.Header = "Kopf"; d.Page.Footer = "Fuß"; d.Page.PageNumbers = true; d.Page.Watermark = "ENTWURF"; d.Page.Columns = 2; d.Page.PageBorder = true; d.Page.Landscape = true; d.Page.Hyphenate = true;
        var p = new Para("Fett, kursiv, unterstrichen, durchgestrichen, hoch und tief mit Link, Kommentar, Fußnote und Endnote.");
        TextDoc.Format(p, 0, 4, Fmt.Bold, true); TextDoc.Format(p, 6, 6, Fmt.Italic, true); TextDoc.Format(p, 14, 13, Fmt.Underline, true); TextDoc.Format(p, 29, 15, Fmt.Strike, true);
        TextDoc.Format(p, 46, 4, Fmt.Super, true); TextDoc.Format(p, 55, 4, Fmt.Sub, true); TextDoc.Size(p, 0, 4, 18);
        p.Marks = [new Mark(64, 4, MarkKind.Link, "https://example.org"), new Mark(70, 9, MarkKind.Comment, "Bitte prüfen"), new Mark(81, 0, MarkKind.Footnote, "Eine Fußnote"), new Mark(94, 0, MarkKind.Endnote, "Eine Endnote"),
            new Mark(0, 4, MarkKind.Font, "Georgia"), new Mark(6, 6, MarkKind.Color, "#C00000"), new Mark(14, 13, MarkKind.Highlight, "#FFFF00")];
        var rev = new Para("Alt neu bleibt") { Marks = [new Mark(0, 3, MarkKind.Delete), new Mark(4, 3, MarkKind.Insert)] };
        d.Blocks.AddRange([
            new Para("Titel", ParaStyle.Title), new Para("Untertitel", ParaStyle.Subtitle), new TocBlock(), new Para("Eins", ParaStyle.Heading1), new Para("Zwei", ParaStyle.Heading2), new Para("Drei", ParaStyle.Heading3), p, rev,
            new Para("Mitte") { Align = Align.Center, SpaceAfterPt = 12, SpaceBeforePt = 6, LineSpacing = 1.5, FirstLineCm = 1 }, new Para("Rechts") { Align = Align.Right, IndentCm = 2 }, new Para("Block") { Align = Align.Justify },
            new Para("Punkt") { List = ListKind.Bullet }, new Para("Unterpunkt") { List = ListKind.Bullet, ListLevel = 1 }, new Para("Schritt") { List = ListKind.Number }, new Para("Teilschritt") { List = ListKind.Number, ListLevel = 1 }, new Para("Teilteil") { List = ListKind.Number, ListLevel = 2 },
            new Para("Zitat", ParaStyle.Quote), new Table { Rows = [["Name", "Wert"], ["Miete\nwarm", "800 €"]] }, new PageBreak(),
            new Picture { Data = Png, Ext = "png", WidthCm = 4, HeightCm = 2, Caption = "Ein Bild", Alt = "Beschreibung" }, new Para("Zeile eins\nZeile zwei\tmit Tab"),
        ]);
        return d;
    }

    static SheetDoc SheetAlles()
    {
        var d = new SheetDoc(); var s = d.Sheets[0];
        s.Get("A1").Raw = "Posten"; s.Get("B1").Raw = "Betrag"; s.Get("A1").Bold = true; s.Get("B1").Bold = true; s.Get("A1").Fill = "#BDD7EE"; s.Get("B1").Border = true;
        var formats = Enum.GetValues<NumFmt>();
        for (var i = 0; i < formats.Length; i++) { s.Get("A" + (i + 2)).Raw = "Zeile " + i; var c = s.Get("B" + (i + 2)); c.Raw = formats[i] == NumFmt.Text ? "Text" : (i + 1.5).ToString(System.Globalization.CultureInfo.GetCultureInfo("de-DE")); c.Format = formats[i]; }
        s.Get("C2").Raw = "=SUMME(B2:B5)"; s.Get("C3").Raw = "=WENN(B2>1;\"ja\";\"nein\")"; s.Get("C4").Raw = "WAHR"; s.Get("A2").Note = "Eine Notiz"; s.Get("B2").Unlocked = true;
        s.ColWidths[0] = 24; s.HiddenRows.Add(4); s.FreezeRows = 1; s.FreezeCols = 1; s.Protected = true;
        foreach (var k in Enum.GetValues<ChartKind>()) s.Charts.Add(new SheetChart(k, "A1:B5", "Diagramm " + k));
        s.CondRules.AddRange([new CondRule("B2:B9", CondKind.Farbskala), new CondRule("B2:B9", CondKind.Datenbalken), new CondRule("B2:B9", CondKind.Regel, CondOp.Groesser, 3), new CondRule("B2:B9", CondKind.Regel, CondOp.Zwischen, 1, 4),
            new CondRule("B2:B9", CondKind.Regel, CondOp.Kleiner, 2), new CondRule("B2:B9", CondKind.Regel, CondOp.Gleich, 2), new CondRule("A2:A9", CondKind.Regel, CondOp.Enthaelt, Text: "Zeile")]);
        s.Validations.Add(new ValidationRule("D2:D9", ["ja", "nein"]));
        var two = d.AddSheet("Zweites Blatt"); two.Get("A1").Raw = "=Tabelle1!B2*2";
        return d;
    }

    static SlideDoc SlideAlles(SlideTheme th)
    {
        var d = new SlideDoc { Theme = th };
        foreach (var (layout, _) in SlideLayouts.Names) { var s = d.AddSlide(layout); foreach (var sh in s.Shapes.Where(x => x.Kind == ShapeKind.Text)) sh.Text = "Text\n\tUnterpunkt\n\nNoch einer"; s.Notes = "Notiz eins\nNotiz zwei"; }
        var all = d.AddSlide(SlideLayout.Leer); all.Background = "#102030";
        all.Shapes.Add(new SlideShape { Kind = ShapeKind.Text, X = 1, Y = 1, W = 10, H = 3, Text = "Punkte\n\tUnterpunkt", Bullets = true, Bold = true, Color = "#FF0000", Align = Align.Center, FontSize = 30, Animation = Animation.Zoomen });
        all.Shapes.Add(new SlideShape { Kind = ShapeKind.Tabelle, X = 2, Y = 5, W = 14, H = 6, FontSize = 16, Table = [["Posten", "Betrag"], ["Getränke", "120"]], Animation = Animation.Einblenden });
        foreach (var k in Enum.GetValues<ChartKind>()) all.Shapes.Add(new SlideShape { Kind = ShapeKind.Diagramm, X = 17, Y = 5, W = 14, H = 10, Text = "Kosten", ChartKind = k, ChartLabels = ["A", "B"], ChartValues = [120, 40] });
        all.Shapes.Add(new SlideShape { Kind = ShapeKind.Bild, X = 2, Y = 12, W = 4, H = 4, Image = Png, Animation = Animation.Einblenden });
        all.Shapes.Add(new SlideShape { Kind = ShapeKind.Ellipse, X = 8, Y = 12, W = 4, H = 4, Fill = "#F59E0B", Text = "Neu!", Align = Align.Right, Animation = Animation.Hereinfliegen });
        all.Shapes.Add(new SlideShape { Kind = ShapeKind.Rechteck, X = 12, Y = 12, W = 4, H = 4, Fill = "#00AA00", Text = "Kasten" });
        all.Shapes.Add(new SlideShape { Kind = ShapeKind.Pfeil, X = 16, Y = 12, W = 4, H = 2, Fill = "#0000AA" });
        all.Shapes.Add(new SlideShape { Kind = ShapeKind.Medien, X = 20, Y = 12, W = 6, H = 4, MediaPath = "film.mp4" });
        var tr = Enum.GetValues<Transition>();
        for (var i = 0; i < d.Slides.Count; i++) d.Slides[i].Transition = tr[i % tr.Length];
        return d;
    }
}
