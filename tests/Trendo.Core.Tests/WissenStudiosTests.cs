using System.Text;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Trendo.Core;
using Trendo.Core.Kreativ;
using Trendo.Core.Office;
using Trendo.Core.PromptAction;
using Trendo.Core.Wissen;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0780 (Etappe 0.9.113 „Wissen &amp; Lernen“): Fachkerne der Studios „Referate &amp; Hausarbeiten“ (<see cref="ReferatKit"/>, ODP-Schreiben)
/// und „Zeitleisten“ (<see cref="HistDate"/>, <see cref="TimelineKit"/>) – mit echten Dateien, die mit Trendos eigenen Lesern zurückgelesen werden.</summary>
public class WissenStudiosTests
{
    static string Temp() { var d = Path.Combine(Path.GetTempPath(), "trendo-t0780-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }

    // ---------------- Datum ----------------

    [Theory(DisplayName = "Zeitleiste: Daten lesen und gleich wieder schreiben – auch v. Chr., ungefähr, Jahrzehnt, Jahrhundert, Jahrtausend")]
    [InlineData("1789", "1789", 1789, DatePrecision.Year, false)]
    [InlineData("14.07.1789", "14. Juli 1789", 1789, DatePrecision.Day, false)]
    [InlineData("14. Juli 1789", "14. Juli 1789", 1789, DatePrecision.Day, false)]
    [InlineData("Juli 1789", "Juli 1789", 1789, DatePrecision.Month, false)]
    [InlineData("07.1789", "Juli 1789", 1789, DatePrecision.Month, false)]
    [InlineData("1789-07-14", "14. Juli 1789", 1789, DatePrecision.Day, false)]
    [InlineData("44 v. Chr.", "44 v. Chr.", -44, DatePrecision.Year, false)]
    [InlineData("15.03.44 v. Chr.", "15. März 44 v. Chr.", -44, DatePrecision.Day, false)]
    [InlineData("-753", "753 v. Chr.", -753, DatePrecision.Year, false)]
    [InlineData("500 BC", "500 v. Chr.", -500, DatePrecision.Year, false)]
    [InlineData("ca. 1200", "ca. 1200", 1200, DatePrecision.Year, true)]
    [InlineData("um 800", "ca. 800", 800, DatePrecision.Year, true)]
    [InlineData("~1500", "ca. 1500", 1500, DatePrecision.Year, true)]
    [InlineData("1990er", "1990er", 1990, DatePrecision.Decade, false)]
    [InlineData("1990er Jahre", "1990er", 1990, DatePrecision.Decade, false)]
    [InlineData("12. Jahrhundert", "12. Jh.", 1101, DatePrecision.Century, false)]
    [InlineData("3. Jh. v. Chr.", "3. Jh. v. Chr.", -300, DatePrecision.Century, false)]
    [InlineData("2. Jt. v. Chr.", "2. Jt. v. Chr.", -2000, DatePrecision.Millennium, false)]
    [InlineData("1 n. Chr.", "1", 1, DatePrecision.Year, false)]
    public void Dates(string input, string shown, int year, DatePrecision precision, bool approx)
    {
        var d = HistDate.Parse(input);
        Assert.NotNull(d);
        Assert.Equal((year, precision, approx), (d!.Value.Year, d.Value.Precision, d.Value.Approx));
        Assert.Equal(shown, d.Value.ToString());
        Assert.Equal(d, HistDate.Parse(d.Value.ToString())); // Rundreise
    }

    [Theory(DisplayName = "Zeitleiste: ungültige Daten werden abgelehnt (kein Jahr 0, kein 30. Februar, kein Text)")]
    [InlineData("0")]
    [InlineData("30.02.2001")]
    [InlineData("29.02.1900")]
    [InlineData("13.2020")]
    [InlineData("irgendwann")]
    [InlineData("")]
    [InlineData("1985er")]
    public void Bad_dates(string input) => Assert.Null(HistDate.Parse(input));

    [Fact(DisplayName = "Zeitleiste: Lage auf der Achse ohne Jahr 0 (1 v. Chr. direkt vor 1 n. Chr.), Jahrhundert in der Mitte, Schaltjahr 2000 gültig")]
    public void Positions()
    {
        Assert.Equal(-43, HistDate.Parse("44 v. Chr.")!.Value.Position);
        Assert.Equal(0, HistDate.Parse("1 v. Chr.")!.Value.Position);
        Assert.Equal(1, HistDate.Parse("1 n. Chr.")!.Value.Position);
        Assert.Equal(1, HistDate.Parse("1 n. Chr.")!.Value.Position - HistDate.Parse("1 v. Chr.")!.Value.Position);
        var c = HistDate.Parse("3. Jh. v. Chr.")!.Value; // 300–201 v. Chr. = astronomisch −299 … −200
        Assert.Equal(-299, c.Position); Assert.Equal(-199, c.PositionEnd); Assert.Equal(-249, c.Center);
        Assert.NotNull(HistDate.Parse("29.02.2000"));
        Assert.True(HistDate.Parse("15.03.44 v. Chr.")!.Value.Position < HistDate.Parse("44 v. Chr.")!.Value.PositionEnd);
    }

    [Fact(DisplayName = "Zeitleiste: Zeilen „Datum | Titel | Spur | Beschreibung“ und Zeiträume „500–300 v. Chr.“ lesen; Unlesbares wird genannt")]
    public void Lines()
    {
        var (items, bad) = TimelineKit.ParseLines("1789 | Französische Revolution | Politik | Sturm auf die Bastille\n500–300 v. Chr. | Klassik\n1618 – 1648 | Dreißigjähriger Krieg\n- 44 v. Chr.: Tod Caesars\nirgendwann | Nichts\nnur Text");
        Assert.Equal(4, items.Count);
        Assert.Equal(("Französische Revolution", "Politik", "Sturm auf die Bastille"), (items[0].Title, items[0].Track, items[0].Text));
        Assert.Equal((-500, -300), (items[1].Start.Year, items[1].End!.Value.Year));
        Assert.Equal((1618, 1648), (items[2].Start.Year, items[2].End!.Value.Year));
        Assert.Equal((-44, "Tod Caesars"), (items[3].Start.Year, items[3].Title));
        Assert.Equal(2, bad.Count);
        Assert.Contains("irgendwann", bad[0]);
        var t = new Timeline(); t.Tracks.Add(new("t-allg", "Allgemein", "#D4A017"));
        Assert.Equal("t-allg", TimelineKit.TrackFor(t, "t-allg")); Assert.Equal("t-allg", TimelineKit.TrackFor(t, "allgemein")); Assert.Single(t.Tracks); // Spur auch über ihre Kennung (Befund Echtlauf)
    }

    static Timeline Sample()
    {
        var t = new Timeline { Title = "Probe" };
        var a = TimelineKit.TrackFor(t, "Politik"); var b = TimelineKit.TrackFor(t, "Kultur");
        foreach (var (s, e, title, tr) in new[] { ("753 v. Chr.", "", "Gründung Roms", a), ("44 v. Chr.", "", "Tod Caesars", a), ("27 v. Chr.", "476", "Kaiserzeit", a), ("ca. 1200", "", "Nibelungenlied", b), ("3. Jh. v. Chr.", "", "Hellenismus", b), ("1450", "", "Buchdruck", b) })
        {
            var (hs, he) = TimelineKit.CheckDates(s, e);
            t.Items.Add(new(TimelineKit.NewId("e"), title, hs, he, tr, "Text zu " + title));
        }
        return t;
    }

    [Fact(DisplayName = "Zeitleiste: CSV (Semikolon, BOM-fähig) und JSON hin und zurück – gleiche Einträge, Spuren und Daten")]
    public void Csv_json_roundtrip()
    {
        var t = Sample();
        var (fromCsv, p1) = TimelineKit.FromCsv(TimelineKit.ToCsv(t));
        Assert.Empty(p1);
        Assert.Equal(t.Items.Select(i => (i.Title, i.Start, i.End, t.TrackName(i.Track))).OrderBy(x => x.Title), fromCsv.Items.Select(i => (i.Title, i.Start, i.End, fromCsv.TrackName(i.Track))).OrderBy(x => x.Title));
        var (fromJson, p2) = TimelineKit.FromExchangeJson(TimelineKit.ToExchangeJson(t));
        Assert.Empty(p2);
        Assert.Equal(t.Title, fromJson.Title);
        Assert.Equal(t.Items.Select(i => (i.Id, i.Title, i.Start, i.End, i.Text)), fromJson.Items.Select(i => (i.Id, i.Title, i.Start, i.End, i.Text)));
    }

    [Fact(DisplayName = "Zeitleiste: Tabelle und JSON im Format von TimelineJS einlesen (Year/Month/Day/Headline/Group, negative Jahre = v. Chr.)")]
    public void TimelineJs_import()
    {
        const string csv = "Year,Month,Day,Time,End Year,End Month,End Day,End Time,Display Date,Headline,Text,Media,Media Credit,Media Caption,Media Thumbnail,Type,Group,Background\n"
                         + ",,,,,,,,,Römische Geschichte,Überblick,,,,,title,,\n"
                         + "-44,3,15,,,,,,,Tod Caesars,Iden des März,,,,,,Politik,\n"
                         + "-27,,,,476,,,,,Kaiserzeit,,,,,,,Politik,\n"
                         + "1450,,,,,,,,,\"Buchdruck, beweglich\",,,,,,,Technik,\n";
        var (t, probs) = TimelineKit.FromCsv(csv);
        Assert.Empty(probs);
        Assert.Equal("Römische Geschichte", t.Title);
        Assert.Equal(3, t.Items.Count);
        Assert.Equal("15. März 44 v. Chr.", t.Items[0].Start.ToString());
        Assert.Equal((-27, 476), (t.Items[1].Start.Year, t.Items[1].End!.Value.Year));
        Assert.Equal("Buchdruck, beweglich", t.Items[2].Title);
        Assert.Equal(["Politik", "Technik"], t.Tracks.Select(x => x.Name));
        const string json = """{"title":{"text":{"headline":"Probe"}},"events":[{"start_date":{"year":"-490"},"text":{"headline":"Marathon","text":"<p>Schlacht</p>"},"group":"Krieg"},{"start_date":{"year":"1066","month":"10","day":"14"},"text":{"headline":"Hastings"}}]}""";
        var (j, p2) = TimelineKit.FromExchangeJson(json);
        Assert.Empty(p2);
        Assert.Equal(("Probe", 2), (j.Title, j.Items.Count));
        Assert.Equal(("490 v. Chr.", "Schlacht"), (j.Items[0].Start.ToString(), j.Items[0].Text));
        Assert.Equal("14. Oktober 1066", j.Items[1].Start.ToString());
    }

    [Fact(DisplayName = "Zeitleiste: Bild der Zeitleiste – Achse mit v. Chr., keine überlappenden Beschriftungen je Spur, Rand rechts wird nicht überschritten")]
    public void Layout_no_overlap()
    {
        var t = Sample();
        for (var i = 0; i < 30; i++) t.Items.Add(new(TimelineKit.NewId("e"), "Ereignis mit langem Titel " + i, HistDate.Parse((1400 + i * 3).ToString())!.Value, null, t.Tracks[1].Id));
        var (f, to) = TimelineKit.Fit(t);
        var s = TimelineKit.Layout(t, f, to, 1400);
        Assert.Contains(s.Prims.OfType<TimelineKit.PText>(), p => p.Text.EndsWith("v. Chr.") && p.Y < TimelineKit.AxisTop + 1);
        foreach (var lane in t.Tracks)
        {
            var boxes = t.Items.Where(i => i.Track == lane.Id && !i.IsPeriod).Select(i => s.Hits[i.Id]).ToList();
            for (var a = 0; a < boxes.Count; a++)
                for (var b = a + 1; b < boxes.Count; b++)
                {
                    var (x1, y1, w1, h1) = boxes[a]; var (x2, y2, w2, h2) = boxes[b];
                    Assert.False(x1 < x2 + w2 && x2 < x1 + w1 && y1 < y2 + h2 && y2 < y1 + h1, $"Überlappung {a}/{b}");
                }
        }
        Assert.All(s.Hits.Values, h => Assert.True(h.X + h.W <= s.Width + 1, $"rechts über den Rand: {h.X + h.W} > {s.Width}"));
    }

    [Fact(DisplayName = "Zeitleiste: SVG ist gültiges XML mit allen Titeln, PDF hat eine Seite und echten Text")]
    public void Svg_pdf()
    {
        var root = Temp();
        try
        {
            var t = Sample(); var (f, to) = TimelineKit.Fit(t);
            var scene = TimelineKit.Layout(t, f, to, 1400, paper: true);
            var svg = TimelineKit.ToSvg(scene);
            var x = XDocument.Parse(svg);
            Assert.Equal("svg", x.Root!.Name.LocalName);
            var texts = x.Descendants().Where(e => e.Name.LocalName == "text").Select(e => e.Value).ToList();
            foreach (var i in t.Items) Assert.Contains(i.Title, texts.Select(s => s.Replace("ca. ", "")));
            Assert.Contains("753 v. Chr.", texts);
            var pdf = Path.Combine(root, "z.pdf"); TimelineKit.ToPdf(scene, pdf);
            using var doc = PdfSharp.Pdf.IO.PdfReader.Open(pdf, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import);
            Assert.Equal(1, doc.PageCount);
            Assert.True(new FileInfo(pdf).Length > 2000);
        }
        finally { try { Directory.Delete(root, true); } catch (IOException) { } }
    }

    // ---------------- Referate ----------------

    static JsonObject RefSample(string extraText = "")
    {
        var o = new JsonObject
        {
            ["title"] = "Wasser sparen im Garten", ["subtitle"] = "Ein Referat", ["kind"] = "referat", ["author"] = "Erika Muster", ["school"] = "Musterschule", ["course"] = "Biologie", ["teacher"] = "Max Beispiel",
            ["style"] = "apa", ["theme"] = "klar",
            ["sources"] = new JsonArray(
                ReferatKit.SourceJson(new CiteKit.Source { Id = "q-buch", Kind = "buch", Authors = "Muster, Erika; Beispiel, Max", Title = "Gärten für Einsteiger", Year = "2024", Publisher = "Grünverlag", Place = "Musterstadt" }),
                ReferatKit.SourceJson(new CiteKit.Source { Id = "q-web", Kind = "webseite", Authors = "{Gartenverband Musterstadt}", Title = "Wasser sparen", Year = "2025", Journal = "example.org", Url = "https://example.org/w", Accessed = "2026-11-03" })),
            ["sections"] = ReferatKit.SectionsJson(
            [
                new("s1", 1, "Einleitung", "Gärten brauchen viel Wasser [@q-web]. Hier geht es ums Sparen.\n\nZweiter Absatz." + extraText),
                new("s2", 1, "Wege", ""),
                new("s3", 2, "Regentonne", "Regenwasser ist weich [@q-buch, S. 12]."),
                new("s4", 1, "Fazit", "Es lohnt sich.", "Wenig Aufwand\nViel Wirkung"),
            ]),
        };
        return o;
    }

    [Fact(DisplayName = "Referat: Gliederung aus Zeilen (1 / 1.1 / ## / Einrückung), Nummern, Verweise im Text und Kurzbelege nach Stil")]
    public void Outline_and_citations()
    {
        var o = ReferatKit.ParseOutline("1 Einleitung\n1.1 Fragestellung\n\n2. Hauptteil\n## Unterpunkt\n  eingerückt\n    tiefer\n3 Fazit:");
        Assert.Equal([(1, "Einleitung"), (2, "Fragestellung"), (1, "Hauptteil"), (2, "Unterpunkt"), (2, "eingerückt"), (3, "tiefer"), (1, "Fazit")], o);
        var n = ReferatKit.Numbered(ReferatKit.Sections(RefSample()));
        Assert.Equal(["1", "2", "2.1", "3"], n.Select(x => x.Number));
        var src = ReferatKit.Sources(RefSample());
        Assert.Equal("Wasser [Quelle fehlt: x] und (Muster & Beispiel, 2024, S. 12).", ReferatKit.Resolve("Wasser [@x] und [@q-buch, S. 12].", src, "apa"));
        Assert.Equal("(MUSTER und BEISPIEL 2024, S. 12)", ReferatKit.Resolve("[@q-buch S. 12]", src, "din"));
        Assert.Equal(["x"], ReferatKit.UnknownCitations("a [@x] b [@q-web]", src));
    }

    [Fact(DisplayName = "Referat: Prüfung meldet Verweis ohne Quelle als blockierend – die Ausgabe verweigert dann (keine erfundenen Quellen)")]
    public void Unknown_source_blocks_export()
    {
        var o = RefSample(" Laut Studie [@erfunden-2023] spart man 90 %.");
        var f = ReferatKit.Check(o);
        Assert.Contains(f, x => x.Blocks && x.Text.Contains("[@erfunden-2023]"));
        var e = Assert.Throws<InvalidOperationException>(() => ReferatKit.BuildText(o));
        Assert.Contains("erfunden-2023", e.Message);
        Assert.Throws<InvalidOperationException>(() => ReferatKit.BuildSlides(o));
        Assert.DoesNotContain(ReferatKit.Check(RefSample()), x => x.Blocks);
    }

    [Fact(DisplayName = "Referat: DOCX und ODT mit Deckblatt, Inhaltsverzeichnis, nummerierten Kapiteln, Kurzbelegen, Literaturverzeichnis und Seitenzahlen – mit Trendos Lesern zurückgelesen; PDF mit mehreren Seiten")]
    public void Text_outputs()
    {
        var root = Temp();
        try
        {
            var doc = ReferatKit.BuildText(RefSample());
            var docx = Path.Combine(root, "r.docx"); DocxFormat.Write(doc, docx);
            var odt = Path.Combine(root, "r.odt"); OdtFormat.Write(doc, odt);
            foreach (var back in new[] { DocxFormat.Read(docx), OdtFormat.Read(odt) })
            {
                Assert.True(back.Page.PageNumbers);
                Assert.Single(back.Blocks.OfType<TocBlock>());
                var paras = back.Paras.ToList();
                Assert.Contains(paras, p => p.Style == ParaStyle.Title && p.Text == "Wasser sparen im Garten");
                Assert.Contains(paras, p => p.Text == "vorgelegt von Erika Muster");
                Assert.Equal(["1 Einleitung", "2 Wege", "3 Fazit", "Literaturverzeichnis"], paras.Where(p => p.Style == ParaStyle.Heading1).Select(p => p.Text));
                Assert.Contains(paras, p => p.Style == ParaStyle.Heading2 && p.Text == "2.1 Regentonne");
                Assert.Contains(paras, p => p.Text.Contains("(Gartenverband Musterstadt, 2025)"));
                Assert.Contains(paras, p => p.Text.Contains("(Muster & Beispiel, 2024, S. 12)"));
                var lit = paras.SkipWhile(p => p.Text != "Literaturverzeichnis").Skip(1).ToList();
                Assert.Equal(2, lit.Count);
                Assert.StartsWith("Gartenverband Musterstadt", lit[0].Text); Assert.StartsWith("Muster, E., & Beispiel, M.", lit[1].Text);
                Assert.Contains(lit[1].Spans, s => s.Fmt.HasFlag(Fmt.Italic)); // Titel kursiv
            }
            var pdf = Path.Combine(root, "r.pdf");
            var pages = TextPdf.Export(doc, pdf);
            Assert.True(pages >= 3, $"nur {pages} Seiten");
        }
        finally { try { Directory.Delete(root, true); } catch (IOException) { } }
    }

    [Fact(DisplayName = "Referat: Folien – Titelfolie, Gliederung, je Kapitel eine Folie mit Punkten und Sprechernotiz, Quellenfolie; PPTX und ODP mit Trendos Lesern zurückgelesen")]
    public void Slides()
    {
        var root = Temp();
        try
        {
            var deck = ReferatKit.BuildSlides(RefSample());
            Assert.Equal(["Wasser sparen im Garten", "Gliederung", "1 Einleitung", "2 Wege", "3 Fazit", "Quellen"], deck.Slides.Select(s => s.Title));
            Assert.Equal("Regentonne", deck.Slides[3].ByRole("body")!.Text); // Unterabschnitte als Punkte
            Assert.Equal("Wenig Aufwand\nViel Wirkung", deck.Slides[4].ByRole("body")!.Text); // eigene Folienpunkte
            Assert.Contains("(Gartenverband Musterstadt, 2025)", deck.Slides[2].Notes);
            var pptx = Path.Combine(root, "r.pptx"); SlideFiles.WritePptx(deck, pptx);
            var odp = Path.Combine(root, "r.odp"); SlideFiles.WriteOdp(deck, odp);
            foreach (var back in new[] { SlideFiles.Open(pptx), SlideFiles.Open(odp) })
            {
                Assert.Equal(deck.Slides.Select(s => s.Title), back.Slides.Select(s => s.Title));
                Assert.Contains("Regenwasser ist weich", back.Slides[3].Notes);
                Assert.Equal(["Regentonne"], back.Slides[3].ByRole("body")!.Text.Split('\n').Select(x => x.Trim()));
            }
            using var z = System.IO.Compression.ZipFile.OpenRead(odp);
            Assert.Equal("mimetype", z.Entries[0].FullName);
            using (var r = new StreamReader(z.Entries[0].Open())) Assert.Equal("application/vnd.oasis.opendocument.presentation", r.ReadToEnd());
            foreach (var part in new[] { "content.xml", "styles.xml", "meta.xml", "META-INF/manifest.xml" }) { using var s = z.GetEntry(part)!.Open(); XDocument.Load(s); }
        }
        finally { try { Directory.Delete(root, true); } catch (IOException) { } }
    }

    [Fact(DisplayName = "KI-Plan (T-0780, Befund Echtlauf): Liste von Texten für einen Text-Wert wird zu Zeilen; lange Texte gelten bis zur Grenze der Aktion (statt fest 2000 Zeichen)")]
    public void Planner_list_and_long_text()
    {
        var ev = new Programs.AssistAction("zeitleiste.ereignisse", "Ereignisse", ["ereignisse"], [new Programs.AssistParam("ereignisse", "Ereignisse", Max: 50_000)]);
        var plan = Programs.AssistChain.FromKi("Ereignisse einfügen", """{"steps":[{"action":"zeitleiste.ereignisse","args":{"ereignisse":["44 v. Chr. | Tod Caesars", "800 | Kaiserkrönung"]}}]}""", [ev]);
        Assert.True(plan.Understood);
        Assert.Equal("44 v. Chr. | Tod Caesars\n800 | Kaiserkrönung", plan.Steps.Single().Args["ereignisse"]);
        var longText = new string('a', 5000);
        var sec = new Programs.AssistAction("referat.abschnitt", "Abschnitt", ["abschnitt"], [new Programs.AssistParam("text", "Text", Max: ReferatKit.MaxSectionChars)]);
        var p2 = Programs.AssistChain.FromKi("Abschnitt", new JsonObject { ["steps"] = new JsonArray(new JsonObject { ["action"] = "referat.abschnitt", ["args"] = new JsonObject { ["text"] = longText } }) }.ToJsonString(), [sec]);
        Assert.Equal(5000, p2.Steps.Single().Args["text"].Length);
        var plain = new Programs.AssistAction("x.y", "X", ["x"], [new Programs.AssistParam("text", "Text")]); // ohne Angabe bleibt die alte Grenze
        var p3 = Programs.AssistChain.FromKi("X", new JsonObject { ["steps"] = new JsonArray(new JsonObject { ["action"] = "x.y", ["args"] = new JsonObject { ["text"] = longText } }) }.ToJsonString(), [plain]);
        Assert.False(p3.Steps.SingleOrDefault()?.Args.ContainsKey("text") ?? false);
    }

    // ---------------- Adapter ----------------

    [Fact(DisplayName = "Wissen-Adapter: Gliederungs-Gerüst und Zeitleisten-Tabelle ohne Nebenwirkung, mit Pflichtfeldern und echten Dateien im Projekt")]
    public void Adapter()
    {
        var all = PromptActions.CreateDefault();
        foreach (var id in new[] { "STUDIO-REFERAT-01", "STUDIO-TIMELINE-01" })
        {
            var ad = all.Single(a => a.StudioId == id);
            Assert.False(ad.CanPerformSideEffect);
            Assert.Equal(SideEffectClass.None, ad.Effect);
            Assert.NotEmpty(ad.Missing(new Dictionary<string, string>()));
            Assert.Equal(CapabilityLevel.Partial, CapabilityCatalog.Level(id, all));
        }
        Assert.NotEmpty(all.Single(a => a.StudioId == "STUDIO-TIMELINE-01").Validate(new Dictionary<string, string> { ["ereignisse"] = "irgendwann | nichts" }));
        var root = Temp();
        var core = new TrendoCore(Path.Combine(root, "daten"), new InMemorySecretStore(), offline: true);
        var project = core.Projects.Create("probe", "Probe").Id;
        var wf = new StudioWorkflow(core, all);
        var ctx = new StudioExecutionContext(project, Principal.LocalOwner, CancellationToken.None, ResourceBudget.Default);
        string Run(string studio, string prompt, string file, string id)
        {
            var req = new StudioPromptRequest(id, project, studio, prompt, [], "de", null, PrivacyMode.Local, new Provenance("test", null, null));
            var done = wf.Los(project, wf.Start(req, ctx).RunId, ctx);
            Assert.Equal(RunStatus.Verified, done.Status);
            return File.ReadAllText(core.Projects.ResolveInProject(project, done.Artifacts.Single(a => a.RelativePath.EndsWith(file)).RelativePath));
        }
        try
        {
            var g = Run("STUDIO-REFERAT-01", "Thema: Wasser sparen\nPunkte: Regentonne; Mulchen", "gliederung.md", "ref-1");
            Assert.Contains("2.1 Regentonne", g); Assert.Contains("2.2 Mulchen", g); Assert.Contains("3 Fazit", g);
            var csv = Run("STUDIO-TIMELINE-01", "Ereignisse: 44 v. Chr. | Tod Caesars | Politik", "zeitleiste.csv", "tl-1");
            Assert.Contains("Ereignis;Tod Caesars;44 v. Chr.;;Politik", csv);
        }
        finally { try { Directory.Delete(root, true); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { } }
    }
}
