using System.Diagnostics;
using System.IO.Compression;
using System.Xml.Linq;
using Trendo.Core.Office;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0701 D1b: Trendo Tabelle und Trendo Präsentation – fremde Dateien ohne Verlust (gemeinsame Formeln, verbundene Zellen, Formatcodes,
/// Zellaussehen, Zeilenhöhen, Diagramm-Einzelheiten; Textformate je Feld, fremdes Design, Animationen und Übergänge).</summary>
public class T0701_TabellePraesentation
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-t0701-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

    static string Fremd(string name)
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "Trendo.sln"))) return Path.Combine(d.FullName, "tests", "Daten", "office", name);
        throw new FileNotFoundException(name);
    }

    static string Part(string file, string part) { using var z = ZipFile.OpenRead(file); using var s = new StreamReader(z.GetEntry(part)!.Open()); return s.ReadToEnd(); }

    static void Edit(string zip, string part, Func<string, string> change)
    {
        using var z = ZipFile.Open(zip, ZipArchiveMode.Update);
        var e = z.GetEntry(part)!; string text; using (var s = new StreamReader(e.Open())) text = s.ReadToEnd();
        var neu = change(text); Assert.NotEqual(text, neu);
        e.Delete(); using var w = new StreamWriter(z.CreateEntry(part).Open()); w.Write(neu);
    }

    /// <summary>Jeder XML-Teil des Pakets ist wohlgeformt, jede Beziehung zeigt auf einen vorhandenen Teil, jeder Teil hat eine Inhaltsart.</summary>
    static void PackageOk(string file)
    {
        using var z = ZipFile.OpenRead(file);
        var names = z.Entries.Select(e => e.FullName).ToHashSet();
        var ct = XDocument.Load(z.GetEntry("[Content_Types].xml")!.Open()).Root!; XNamespace c = "http://schemas.openxmlformats.org/package/2006/content-types";
        var overrides = ct.Elements(c + "Override").Select(o => ((string)o.Attribute("PartName")!).TrimStart('/')).ToHashSet();
        var defaults = ct.Elements(c + "Default").Select(o => ((string)o.Attribute("Extension")!).ToLowerInvariant()).ToHashSet();
        foreach (var e in z.Entries)
        {
            if (e.FullName.EndsWith(".xml") || e.FullName.EndsWith(".rels")) { using var s = e.Open(); XDocument.Load(s); }
            if (e.FullName != "[Content_Types].xml") Assert.True(overrides.Contains(e.FullName) || defaults.Contains(Path.GetExtension(e.FullName).TrimStart('.').ToLowerInvariant()), "ohne Inhaltsart: " + e.FullName);
            if (!e.FullName.EndsWith(".rels")) continue;
            var dir = e.FullName.Replace("_rels/", "")[..^5]; dir = dir.Contains('/') ? dir[..dir.LastIndexOf('/')] : "";
            using var rs = e.Open();
            foreach (var rel in XDocument.Load(rs).Root!.Elements())
            {
                if ((string?)rel.Attribute("TargetMode") == "External") continue;
                var t = (string)rel.Attribute("Target")!;
                var full = t.StartsWith('/') ? t[1..] : Norm(dir.Length == 0 ? t : dir + "/" + t);
                Assert.True(names.Contains(full), $"{e.FullName}: Ziel fehlt {t}");
            }
        }
        static string Norm(string p) { var parts = new List<string>(); foreach (var s in p.Split('/')) { if (s == "..") parts.RemoveAt(parts.Count - 1); else if (s != ".") parts.Add(s); } return string.Join('/', parts); }
    }

    // =================== Tabelle ===================

    [Fact(DisplayName = "T-0701: gemeinsame Formeln (shared formulas) – jede Folgezelle hat nach dem Öffnen ihre eigene, relativ verschobene Formel ($ bleibt fest), rechnet richtig und steht nach dem Speichern als Formel in der Datei")]
    public void Gemeinsame_Formeln()
    {
        var d = Tmp();
        try
        {
            var f = Path.Combine(d, "gemeinsam.xlsx"); File.Copy(Fremd("fremd-tabelle.xlsx"), f);
            // so legt Excel „=B3*C3“ ab, wenn man sie nach unten zieht: Formel nur in D3, D4/D5 nur mit der Nummer si
            Edit(f, "xl/worksheets/sheet1.xml", x => x
                .Replace("<c r=\"C3\" s=\"1\"><v>10.5</v></c>", "<c r=\"C3\" s=\"1\"><v>10.5</v></c><c r=\"D3\"><f t=\"shared\" ref=\"D3:D5\" si=\"0\">B3*C3</f><v>12.96225</v></c><c r=\"E3\"><f t=\"shared\" ref=\"E3:E5\" si=\"1\">B3*$C$3+Tabelle1!A$1</f><v>0</v></c>")
                .Replace("<c r=\"C4\" s=\"1\"><v>11.25</v></c>", "<c r=\"C4\" s=\"1\"><v>11.25</v></c><c r=\"D4\"><f t=\"shared\" si=\"0\"/><v>28.125</v></c><c r=\"E4\"><f t=\"shared\" si=\"1\"/><v>0</v></c>")
                .Replace("<c r=\"C5\" s=\"1\"><v>12</v></c>", "<c r=\"C5\" s=\"1\"><v>12</v></c><c r=\"D5\"><f t=\"shared\" si=\"0\"/><v>37.5</v></c><c r=\"E5\"><f t=\"shared\" si=\"1\"/><v>0</v></c>"));
            var doc = SheetFiles.Open(f); var sh = doc.Sheets[0];
            Assert.Equal("=B3*C3", sh.Get("D3").Raw); Assert.Equal("=B4*C4", sh.Get("D4").Raw); Assert.Equal("=B5*C5", sh.Get("D5").Raw);
            Assert.Equal("=B5*$C$3+Tabelle1!A$1", sh.Get("E5").Raw); // absolute Teile bleiben stehen, auch mit Blattnamen
            Assert.False(doc.Import!.Has("xlsx.formel.gemeinsam"));
            var calc = new SheetCalc(doc);
            Assert.Equal(28.125, calc.Value(sh, 3, 3).Number, 9); Assert.Equal(37.5, calc.Value(sh, 4, 3).Number, 9);
            sh.Set("B5", "4"); calc.Invalidate();
            Assert.Equal(48, calc.Value(sh, 4, 3).Number, 9); // die Folgezelle rechnet wirklich (früher: fester Wert 37,5)

            var copy = Path.Combine(d, "kopie.xlsx"); SheetFiles.Save(doc, copy);
            var xml = Part(copy, "xl/worksheets/sheet1.xml");
            Assert.Contains("<c r=\"D4\"><f>B4*C4</f><v>28.125</v></c>", xml);
            Assert.Contains("<c r=\"D5\"><f>B5*C5</f><v>48</v></c>", xml);
            Assert.Contains("<f>B5*$C$3+Tabelle1!A$1</f>", xml);
            var back = SheetFiles.Open(copy);
            Assert.Equal("=B4*C4", back.Sheets[0].Get("D4").Raw);
            PackageOk(copy);
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0701: Formel relativ verschieben wie beim Kopieren – Zeilen, Spalten, $-Bezüge, Bereiche, fremde Blätter, vor A1 = #BEZUG!")]
    public void Formel_kopieren()
    {
        Assert.Equal("=SUMME(B2:C3)*$A$1+D$1", Formula.CopyRefs("=SUMME(A1:B2)*$A$1+C$1", 1, 1));
        Assert.Equal("='Mein Blatt'!B5+$B4", Formula.CopyRefs("='Mein Blatt'!A3+$B2", 2, 1));
        Assert.Equal("=#BEZUG!+1", Formula.CopyRefs("=A1+1", -1, 0));
        Assert.Equal("=A1", Formula.CopyRefs("=A1", 0, 0));
    }

    [Fact(DisplayName = "T-0701: Zahlenformat-Codes wie Excel anzeigen – Einheiten, Abschnitte mit Farbe, Prozent, Tausender, Skalierung, wissenschaftlich, Bruch, Datum/Zeit, Text")]
    public void Formatcodes()
    {
        string F(double x, string code) => SheetNumberFormat.Format(x, code, out _);
        Assert.Equal("1,235 kg", F(1.2345, "0.000\" kg\""));
        Assert.Equal("1.234.567,89 €", F(1234567.891, "#,##0.00\\ \"€\""));
        Assert.Equal("-12,50 €", F(-12.5, "#,##0.00 €;[Red]-#,##0.00 €"));
        SheetNumberFormat.Format(-12.5, "#,##0.00 €;[Red]-#,##0.00 €", out var red); Assert.Equal("#FF0000", red);
        Assert.Equal("(1.235)", F(-1234.6, "#,##0 ;(#,##0)"));
        Assert.Equal("–", F(0, "0.00;-0.00;\"–\""));
        Assert.Equal("12,35%", F(0.12345, "0.00%"));
        Assert.Equal("83,04 %", F(0.8304, "0.00\\ %")); // LibreOffice: geschütztes Leerzeichen
        Assert.Equal("1.235 T€", F(1234567, "#,##0, \"T€\""));
        Assert.Equal("1,23E+05", F(123456, "0.00E+00"));
        Assert.Equal("3 1/4", F(3.25, "# ?/?"));
        Assert.Equal("007", F(7, "000"));
        Assert.Equal("27.10.2026", F(SheetFormat.ToSerial(new DateTime(2026, 10, 27)), "dd/mm/yyyy"));
        Assert.Equal("27.10.2026", F(SheetFormat.ToSerial(new DateTime(2026, 10, 27)), "mm-dd-yy")); // Excels Kurzdatum Nr. 14 folgt dem Land
        Assert.Equal("Dienstag, 27. Oktober 2026", F(SheetFormat.ToSerial(new DateTime(2026, 10, 27)), "dddd, d. mmmm yyyy"));
        Assert.Equal("Okt 26", F(SheetFormat.ToSerial(new DateTime(2026, 10, 27)), "mmm yy"));
        Assert.Equal("14:05", F(SheetFormat.ToSerial(new DateTime(2026, 10, 27, 14, 5, 0)), "hh:mm"));
        Assert.Equal("2:05 PM", F(SheetFormat.ToSerial(new DateTime(2026, 10, 27, 14, 5, 0)), "h:mm AM/PM"));
        Assert.Equal("27:30:00", F(1 + 3.5 / 24, "[h]:mm:ss"));
        Assert.Equal("27.10.2026", F(SheetFormat.ToSerial(new DateTime(2026, 10, 27)), "TT.MM.JJJJ")); // deutsche Codes
        Assert.Equal("Nr. 5", F(5, "\"Nr. \"0"));
        Assert.Equal("1234,5", F(1234.5, "General"));
        Assert.Equal("groß", F(150, "[>=100]\"groß\";[<0]\"minus\";0"));
        Assert.Equal("Name: Erika", SheetNumberFormat.FormatText("Erika", "0;-0;0;\"Name: \"@"));
        Assert.True(SheetNumberFormat.IsDate("dd.mm.yyyy")); Assert.False(SheetNumberFormat.IsDate("0.00 \"m\""));
    }

    [Fact(DisplayName = "T-0701: Zellaussehen hin und zurück – Schrift (kursiv, doppelt unterstrichen, durchgestrichen, Farbe, Größe, Art), Ausrichtung, Umbruch, Einzug, Drehung, Rahmen je Seite, Zeilenhöhen, verbundene Zellen, fixierte Zeilen, eigener Formatcode")]
    public void Zellaussehen_rundreise()
    {
        var d = Tmp();
        try
        {
            var doc = new SheetDoc { FontName = "Arial", FontSize = 10 }; var s = doc.Sheets[0];
            var a = s.Get("A1"); a.Raw = "Kopf"; a.Italic = true; a.Underline = "double"; a.Strike = true; a.FontColor = "#C00000"; a.FontSize = 14; a.FontName = "Georgia"; a.HAlign = "center"; a.VAlign = "top"; a.Wrap = true;
            var b = s.Get("B2"); b.Raw = "12,5"; b.NumCode = "0.0\" m\""; b.Indent = 2; b.Rotation = 45; b.Shrink = true; b.Borders = new CellBorders(new BorderLine("medium", "#FF0000"), null, new BorderLine("dashed"), new BorderLine("double", "#0070C0"));
            var c = s.Get("C3"); c.Raw = "x"; c.Border = true; c.Bold = true; c.Fill = "#FFFF99";
            s.Merge(0, 0, 0, 2); s.RowHeights[0] = 30; s.RowHeights[5] = 8.25; s.FreezeRows = 1;
            var f = Path.Combine(d, "aussehen.xlsx"); SheetFiles.Save(doc, f);
            PackageOk(f);
            var r = SheetFiles.Open(f); var t = r.Sheets[0];
            Assert.False(r.Import!.HasLoss, r.Import.Lang());
            Assert.Equal(("Arial", 10.0), (r.FontName, r.FontSize));
            var ra = t.Get("A1");
            Assert.Equal((true, "double", true, "#C00000", (double?)14, "Georgia", "center", "top", true), (ra.Italic, ra.Underline, ra.Strike, ra.FontColor, ra.FontSize, ra.FontName, ra.HAlign, ra.VAlign, ra.Wrap));
            var rb = t.Get("B2");
            Assert.Equal(("0.0\" m\"", 2, 45, true), (rb.NumCode, rb.Indent, rb.Rotation, rb.Shrink)); Assert.Equal(b.Borders, rb.Borders);
            Assert.Equal("12,5 m", new SheetCalc(r).Display(t, 1, 1));
            var rc = t.Get("C3"); Assert.True(rc.Border); Assert.Null(rc.Borders); Assert.True(rc.Bold); Assert.Equal("#FFFF99", rc.Fill);
            Assert.Equal(new MergeRange(0, 0, 0, 2), Assert.Single(t.Merges));
            Assert.Equal(30, t.RowHeights[0]); Assert.Equal(8.25, t.RowHeights[5]); Assert.Equal(1, t.FreezeRows);
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0701: Farben aus dem Design der Mappe (theme + tint) und der alten Palette (indexed) werden feste Farben – Füllung, Schrift, Rahmen")]
    public void Designfarben()
    {
        var d = Tmp();
        try
        {
            var f = Path.Combine(d, "design.xlsx"); File.Copy(Fremd("fremd-tabelle.xlsx"), f);
            Edit(f, "xl/styles.xml", x => x
                .Replace("<fonts count=\"1\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts>", "<fonts count=\"2\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font><font><sz val=\"11\"/><color theme=\"4\"/><name val=\"Calibri\"/></font></fonts>")
                .Replace("<fill><patternFill patternType=\"gray125\"/></fill></fills>", "<fill><patternFill patternType=\"gray125\"/></fill><fill><patternFill patternType=\"solid\"><fgColor theme=\"4\" tint=\"0.79998168889431442\"/></patternFill></fill></fills>")
                .Replace("<borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders>", "<borders count=\"2\"><border><left/><right/><top/><bottom/><diagonal/></border><border><left/><right/><top/><bottom style=\"thick\"><color indexed=\"10\"/></bottom><diagonal/></border></borders>")
                .Replace("</cellXfs>", "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"1\" xfId=\"0\"/></cellXfs>").Replace("cellXfs count=\"2\"", "cellXfs count=\"3\""));
            Edit(f, "xl/worksheets/sheet1.xml", x => x.Replace("<c r=\"A2\" t=\"s\">", "<c r=\"A2\" s=\"2\" t=\"s\">"));
            var doc = SheetFiles.Open(f); var a2 = doc.Sheets[0].Get("A2");
            Assert.Equal("#4472C4", a2.FontColor); // accent1 des Office-Standarddesigns (die Datei bringt keins mit)
            Assert.Equal("#DAE3F3", a2.Fill);      // accent1, um 80 % aufgehellt – so zeigt Excel es
            Assert.Equal(new CellBorders(null, null, null, new BorderLine("thick", "#FF0000")), a2.Borders);
            Assert.False(doc.Import!.HasLoss, doc.Import.Lang());
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0701: verbundene Zellen – verbinden leert die übrigen Zellen, Überschneidungen verschmelzen, Zeilen/Spalten einfügen und löschen verschieben den Bereich, aufheben")]
    public void Verbinden()
    {
        var s = new SheetDoc().Sheets[0];
        s.Set("A1", "Titel"); s.Set("B1", "weg"); s.Set("A3", "bleibt");
        var m = s.Merge(0, 0, 0, 2)!;
        Assert.Equal("A1:C1", m.Name); Assert.Equal("", s.Raw(0, 1)); Assert.Equal("Titel", s.Raw(0, 0));
        Assert.Equal(new MergeRange(0, 0, 1, 3), s.Merge(0, 2, 1, 3)); Assert.Single(s.Merges);
        s.InsertRows(0); Assert.Equal(new MergeRange(1, 0, 2, 3), s.Merges[0]);
        s.DeleteCols(1, 2); Assert.Equal(new MergeRange(1, 0, 2, 1), s.Merges[0]);
        s.DeleteRows(2); Assert.Equal(new MergeRange(1, 0, 1, 1), s.Merges[0]);
        s.DeleteCols(1); Assert.Empty(s.Merges); // nur noch eine Zelle – kein Verbund mehr
        s.Merge(0, 0, 1, 1); Assert.Equal(1, s.Unmerge(1, 1, 1, 1)); Assert.Empty(s.Merges);
        Assert.Equal("bleibt", s.Raw(2, 0)); Assert.Null(s.MergeAt(0, 0));
    }

    [Fact(DisplayName = "T-0701: eigene Diagramme mit Achsentiteln, Reihenfarben und Legende rechts – hin und zurück ohne Verlust")]
    public void Diagramm_rundreise()
    {
        var d = Tmp();
        try
        {
            var doc = new SheetDoc(); var s = doc.Sheets[0];
            s.Set("A1", "Monat"); s.Set("B1", "Umsatz"); s.Set("C1", "Kosten"); s.Set("A2", "Jan"); s.Set("B2", "10"); s.Set("C2", "4"); s.Set("A3", "Feb"); s.Set("B3", "12"); s.Set("C3", "5");
            var ch = new SheetChart(ChartKind.Balken, "A1:C3", "Ergebnis", "Monat", "Euro", ["#C0504D", null], "r");
            s.Charts.Add(ch);
            var f = Path.Combine(d, "diagramm.xlsx"); SheetFiles.Save(doc, f); PackageOk(f);
            var back = Assert.Single(SheetFiles.Open(f).Sheets[0].Charts);
            Assert.Equal((ch.Kind, ch.Range, ch.Title, ch.XTitle, ch.YTitle, ch.Legend), (back.Kind, back.Range, back.Title, back.XTitle, back.YTitle, back.Legend));
            Assert.Equal(["#C0504D", null], back.Colors!);
            Assert.False(ImportCheck.For(f).HasLoss);
        }
        finally { Directory.Delete(d, true); }
    }

    // =================== Präsentation ===================

    [Fact(DisplayName = "T-0701: mehrere Textformate in einem Feld – Text ändern behält die Formate der übrigen Stellen, ein Wort kursiv/größer setzen, ganzes Feld fett setzt alles")]
    public void Textlaeufe_bearbeiten()
    {
        var s = new SlideShape { Text = "Der Umsatz steigt." };
        s.FormatRange(4, 6, r => r with { Bold = true, Color = "#C00000" });
        Assert.Equal(["Der ", "Umsatz", " steigt."], s.Runs!.Select(r => r.Text));
        s.Text = "Der Umsatz steigt stark.";                    // hinten etwas angehängt
        Assert.Equal(["Der ", "Umsatz", " steigt stark."], s.Runs!.Select(r => r.Text));
        s.Text = "Der Gesamtumsatz steigt stark.";              // im fetten Wort eingefügt: bekommt dessen Format
        Assert.Equal(["Der ", "Gesamtumsatz", " steigt stark."], s.Runs!.Select(r => r.Text));
        Assert.Equal(true, s.Runs![1].Bold);
        s.FormatRange(0, 3, r => r with { Italic = true, Size = 30 });
        Assert.Equal(new TextRun("Der", 30, Italic: true), s.Runs![0]);
        Assert.Equal(true, s.RunAt(6).Bold);
        s.Bold = true;                                           // ganzes Feld: eigene Fett-Angaben fallen weg
        Assert.All(s.Runs!, r => Assert.Null(r.Bold));
        s.FontSize = 20;
        Assert.All(s.Runs!, r => Assert.Null(r.Size));
        Assert.Equal("Der Gesamtumsatz steigt stark.", string.Concat(s.Runs!.Select(r => r.Text)));
        var c = s.Clone(); c.Text = "Neu"; Assert.Equal("Der Gesamtumsatz steigt stark.", string.Concat(s.Runs!.Select(r => r.Text))); // Kopie teilt nichts
    }

    [Fact(DisplayName = "T-0701: fremde PPTX bearbeiten und speichern – Folienmaster, Layout und Theme der Datei bleiben byte-gleich, Platzhalter bleiben Platzhalter, Animation und Textformate bleiben; mit Trendo-Design wird ersetzt")]
    public void Fremdes_Design_bleibt()
    {
        var d = Tmp();
        try
        {
            var src = Fremd("fremd-praesentation.pptx");
            var doc = SlideFiles.Open(src);
            var box = doc.Slides[0].Shapes.Single(s => s.Text.Contains("12 Prozent"));
            box.Text = "Der Umsatz ist um 12 Prozent gestiegen – ein Rekord.";
            box.X += 1; box.W -= 1;                                   // verschoben und schmaler: Animation zeigt weiter auf dasselbe Objekt
            var title = doc.Slides[0].Shapes.Single(s => s.Role == "title");
            Assert.Equal(44, title.FontSize); Assert.True(title.SizeInherited); // Titelgröße aus dem Folienmaster der Datei
            doc.AddSlide(SlideLayout.NurTitel).ByRole("title")!.Text = "Neue Folie";
            var f = Path.Combine(d, "bearbeitet.pptx"); SlideFiles.Save(doc, f);
            PackageOk(f);
            foreach (var p in new[] { "ppt/slideMasters/slideMaster1.xml", "ppt/slideLayouts/slideLayout1.xml", "ppt/theme/theme1.xml", "ppt/slideMasters/_rels/slideMaster1.xml.rels" })
                Assert.Equal(Part(src, p), Part(f, p));
            var slide = Part(f, "ppt/slides/slide1.xml");
            Assert.Contains("<p:ph type=\"title\"/>", slide);                   // Titel bleibt Platzhalter der Vorlage
            Assert.DoesNotContain("<p:bg>", slide);                              // Hintergrund kommt aus dem Master
            Assert.Contains("animRot", slide); Assert.Contains("spid=\"3\"", slide); Assert.Contains("<p:cNvPr id=\"3\"", slide);
            Assert.Contains("ein Rekord.</a:t>", slide); Assert.Contains("sz=\"2800\" b=\"1\"", slide);
            Assert.Contains("Target=\"/ppt/slideLayouts/slideLayout1.xml\"", Part(f, "ppt/slides/_rels/slide1.xml.rels"));
            Assert.Contains("Target=\"/ppt/slideLayouts/slideLayout1.xml\"", Part(f, "ppt/slides/_rels/slide2.xml.rels")); // neue „Nur Titel“-Folie nutzt das Layout der Vorlage
            var back = SlideFiles.Open(f);
            Assert.False(back.Import!.HasLoss, back.Import.Lang());
            Assert.Equal("Office Theme", back.Theme.Name);
            Assert.Equal(new TextRun("12 Prozent", 28, true, Color: "#C00000"), back.Slides[0].Shapes.Single(s => s.Text.Contains("Rekord")).Runs![1]);

            // PDF: die Textabschnitte werden mit ihrem eigenen Format gesetzt (kein Absturz, echte Seite)
            var pdf = Path.Combine(d, "folien.pdf"); SlideFiles.Save(doc, pdf);
            Assert.StartsWith("%PDF", File.ReadAllText(pdf)[..4]); Assert.True(new FileInfo(pdf).Length > 1000);

            // Design wechseln = bewusst ersetzen
            doc.Theme = SlideThemes.Get("wald"); Assert.False(doc.KeepsDesign);
            var g = Path.Combine(d, "wald.pptx"); SlideFiles.Save(doc, g); PackageOk(g);
            using (var gz = ZipFile.OpenRead(g)) Assert.DoesNotContain("Office Theme", string.Concat(gz.Entries.Where(e => e.FullName.EndsWith(".xml")).Select(e => { using var r = new StreamReader(e.Open()); return r.ReadToEnd(); })));
            Assert.Equal("Wald", SlideFiles.Open(g).Theme.Name);
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0701: Animationen der Datei – bleiben unverändert, solange in Trendo nichts daran geändert wird; neue Animation oder gelöschtes Ziel-Objekt: Trendo schreibt seine eigenen")]
    public void Animationen_durchreichen()
    {
        var d = Tmp();
        try
        {
            var doc = SlideFiles.Open(Fremd("fremd-praesentation.pptx"));
            var a = Path.Combine(d, "a.pptx"); SlideFiles.Save(doc, a);
            Assert.Contains("animRot", Part(a, "ppt/slides/slide1.xml"));

            var changed = doc.Clone(); changed.Slides[0].Shapes.Single(s => s.Role == "title").Animation = Animation.Einblenden;
            var b = Path.Combine(d, "b.pptx"); SlideFiles.Save(changed, b);
            var xb = Part(b, "ppt/slides/slide1.xml");
            Assert.DoesNotContain("animRot", xb); Assert.Contains("presetID=\"10\"", xb); Assert.Contains("spid=\"2\"", xb);

            var gone = doc.Clone(); gone.Slides[0].Shapes.RemoveAll(s => s.Text.Contains("12 Prozent"));
            var c = Path.Combine(d, "c.pptx"); SlideFiles.Save(gone, c); PackageOk(c);
            Assert.DoesNotContain("<p:timing>", Part(c, "ppt/slides/slide1.xml")); // keine Animation, die ins Leere zeigt

            var dup = doc.Clone(); var box = dup.Slides[0].Shapes.Single(s => s.Text.Contains("12 Prozent")); dup.Slides[0].Shapes.Add(box.Clone());
            var e = Path.Combine(d, "e.pptx"); SlideFiles.Save(dup, e); PackageOk(e);
            var ids = System.Text.RegularExpressions.Regex.Matches(Part(e, "ppt/slides/slide1.xml"), "<p:cNvPr id=\"(\\d+)\"").Select(m => m.Groups[1].Value).ToList();
            Assert.Equal(ids.Count, ids.Distinct().Count()); // Duplikat bekommt eine eigene Nummer
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0701: Übergang und Hintergrundbild der Datei bleiben unverändert, solange Trendo sie nicht ändert")]
    public void Uebergang_und_Hintergrund()
    {
        var d = Tmp();
        try
        {
            var f = Path.Combine(d, "fremd.pptx"); File.Copy(Fremd("fremd-praesentation.pptx"), f);
            Edit(f, "ppt/slides/slide1.xml", x => x.Replace("<p:cSld><p:spTree>", "<p:cSld><p:bg><p:bgPr><a:gradFill><a:gsLst><a:gs pos=\"0\"><a:srgbClr val=\"FFFFFF\"/></a:gs><a:gs pos=\"100000\"><a:schemeClr val=\"accent1\"/></a:gs></a:gsLst></a:gradFill><a:effectLst/></p:bgPr></p:bg><p:spTree>")
                .Replace("</p:clrMapOvr><p:timing>", "</p:clrMapOvr><p:transition spd=\"slow\" advTm=\"3000\"><p:dissolve/></p:transition><p:timing>"));
            var doc = SlideFiles.Open(f);
            Assert.False(doc.Import!.HasLoss, doc.Import.Lang());
            var g = Path.Combine(d, "kopie.pptx"); SlideFiles.Save(doc, g); PackageOk(g);
            var xml = Part(g, "ppt/slides/slide1.xml");
            Assert.Contains("<a:gradFill>", xml); Assert.Contains("<p:dissolve />", xml.Replace("<p:dissolve/>", "<p:dissolve />")); Assert.Contains("advTm=\"3000\"", xml);
            doc.Slides[0].Transition = Transition.Wischen; doc.Slides[0].Background = "#FFEEDD";
            SlideFiles.Save(doc, g); xml = Part(g, "ppt/slides/slide1.xml");
            Assert.Contains("<p:wipe dir=\"r\"/>", xml); Assert.DoesNotContain("dissolve", xml); Assert.Contains("FFEEDD", xml); Assert.DoesNotContain("gradFill", xml);
        }
        finally { Directory.Delete(d, true); }
    }

    // =================== Fremdprogramm ===================

    static string? Soffice()
    {
        foreach (var root in new[] { Environment.GetEnvironmentVariable("TRENDO_WERKZEUGE"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TrendoWerkzeuge") })
            if (root is not null && Path.Combine(root, "libreoffice", "App", "libreoffice", "program", "soffice.exe") is var p && File.Exists(p)) return p;
        return null;
    }

    static string Convert(string soffice, string src, string dir, string target)
    {
        // kurze Namen: LibreOffice legt im Profil tiefe Unterordner an und scheitert still an langen Pfaden (seit T-0729 liegt Temp eine Ebene tiefer)
        var outDir = Path.Combine(dir, "lo-" + target.Split(':')[0] + "-aus-" + Path.GetExtension(src).TrimStart('.')); Directory.CreateDirectory(outDir);
        var psi = new ProcessStartInfo(soffice) { UseShellExecute = false, CreateNoWindow = true };
        foreach (var x in new[] { "-env:UserInstallation=" + new Uri(Path.Combine(dir, "p" + Guid.NewGuid().ToString("N")[..8])).AbsoluteUri, "--headless", "--norestore", "--convert-to", target, "--outdir", outDir, src }) psi.ArgumentList.Add(x);
        using (var p = Process.Start(psi)!) Assert.True(p.WaitForExit(180_000), "LibreOffice antwortet nicht");
        var res = Directory.GetFiles(outDir).Single();
        Assert.True(new FileInfo(res).Length > 0);
        return res;
    }

    [Fact(DisplayName = "T-0701: echter Abgleich mit LibreOffice – Trendos gespeicherte Fremd-Tabelle (gemeinsame Formeln, verbundene Zellen, Formatcode) liest Calc gleich zurück; Fremd-Präsentation öffnet Impress und macht ein PDF (nur mit LibreOffice)")]
    public void LibreOffice_rundreise()
    {
        if (Soffice() is not { } soffice) { Ueberspringen.Weil("LibreOffice fehlt auf diesem PC."); return; }
        var d = Tmp();
        try
        {
            var f = Path.Combine(d, "gemeinsam.xlsx"); File.Copy(Fremd("fremd-tabelle.xlsx"), f);
            Edit(f, "xl/worksheets/sheet1.xml", x => x
                .Replace("<c r=\"C3\" s=\"1\"><v>10.5</v></c>", "<c r=\"C3\" s=\"1\"><v>10.5</v></c><c r=\"D3\"><f t=\"shared\" ref=\"D3:D5\" si=\"0\">B3*C3</f><v>12.96225</v></c>")
                .Replace("<c r=\"C4\" s=\"1\"><v>11.25</v></c>", "<c r=\"C4\" s=\"1\"><v>11.25</v></c><c r=\"D4\"><f t=\"shared\" si=\"0\"/><v>28.125</v></c>")
                .Replace("<c r=\"C5\" s=\"1\"><v>12</v></c>", "<c r=\"C5\" s=\"1\"><v>12</v></c><c r=\"D5\"><f t=\"shared\" si=\"0\"/><v>37.5</v></c>"));
            var doc = SheetFiles.Open(f); doc.Sheets[0].Set("B5", "4");
            var trendo = Path.Combine(d, "trendo.xlsx"); SheetFiles.Save(doc, trendo);
            var lo = SheetFiles.Open(Convert(soffice, trendo, d, "xlsx:Calc MS Excel 2007 XML"));
            var ls = lo.Sheets[0]; var calc = new SheetCalc(lo);
            Assert.Equal("=B5*C5", ls.Get("D5").Raw); Assert.Equal(48, calc.Value(ls, 4, 3).Number, 9);
            Assert.Equal(new MergeRange(0, 0, 0, 2), Assert.Single(ls.Merges));
            Assert.Equal("1,235 kg", calc.Display(ls, 2, 1));
            Assert.Equal("Messwerte je Probe", Assert.Single(ls.Charts).Title);
            Convert(soffice, trendo, d, "pdf");

            var deck = SlideFiles.Open(Fremd("fremd-praesentation.pptx"));
            var p = Path.Combine(d, "trendo.pptx"); SlideFiles.Save(deck, p);
            var odp = SlideFiles.Open(Convert(soffice, p, d, "odp"));
            Assert.Contains(odp.Slides[0].Shapes, s => s.Text.Contains("12 Prozent gestiegen"));
            Assert.EndsWith(".pdf", Convert(soffice, p, d, "pdf"));
            // Beleg: Mit TRENDO_T0701_BELEG bleiben Trendos Dateien und die LibreOffice-Ergebnisse dort liegen (reports/T-0701)
            if (Environment.GetEnvironmentVariable("TRENDO_T0701_BELEG") is { Length: > 0 } keep)
            {
                Directory.CreateDirectory(keep);
                foreach (var file in Directory.GetFiles(d, "*", SearchOption.AllDirectories).Where(x => !x.Contains("profil-") && Path.GetExtension(x) is ".xlsx" or ".pptx" or ".pdf" or ".odp"))
                    File.Copy(file, Path.Combine(keep, (Path.GetFileName(Path.GetDirectoryName(file)!) is var sub && sub.StartsWith("lo-") ? sub + "_" : "") + Path.GetFileName(file)), true);
            }
        }
        finally { try { Directory.Delete(d, true); } catch (IOException) { } }
    }
}
