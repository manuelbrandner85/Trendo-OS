using System.IO.Compression;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Trendo.Core.Kreativ;
using Trendo.Core.Office;
using Trendo.Core.Programs;
using Trendo.Core.Wissen;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0780, Gegenprüfung (nicht freigabefähig): Regressionstests je Befund H1–H5, M1–M4 und der kleinen Punkte. Jeder Test schlägt auf 11c40362d fehl.</summary>
public class T0780GegenpruefungTests
{
    static string Temp() { var d = Path.Combine(Path.GetTempPath(), "trendo-t0780g-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }

    static readonly List<CiteKit.Source> Src =
    [
        new() { Id = "q-buch", Kind = "buch", Authors = "Muster, Erika", Title = "Gärten", Year = "2024", Publisher = "Grünverlag", Place = "Musterstadt" },
        new() { Id = "q-art", Kind = "artikel", Authors = "Beispiel, Max", Title = "Boden", Year = "2023", Journal = "Gartenbau", Volume = "1", Pages = "45–67" },
    ];

    // ---------------- H1/H2: freie Kurzbelege ----------------

    // Testtabelle aus beiden Gegenprüfungen (2. Gegenprüfung M1): gesperrt – nur Hinweis – gar nichts, je mit Gegenprobe

    [Theory(DisplayName = "T-0780 M1 (Tabelle): eindeutige Belegformen werden gesperrt")]
    [InlineData("Mulch spart Wasser (vgl. Erfunden 2021).")]
    [InlineData("Mulch spart Wasser (Erfunden 2021: 5).")]
    [InlineData("Mulch spart Wasser (Erfunden 2021, 5).")]
    [InlineData("Mulch spart Wasser (Erfunden, 2021, p. 3).")]
    [InlineData("Das ist belegt (Erfunden, 2021; Ander, 2019).")]
    [InlineData("Das ist belegt (van Erfunden, 2021).")]
    [InlineData("Das ist belegt (A, B & C, 2021).")]
    [InlineData("Das ist belegt (Erfunden et al., 2021, S. 12).")]
    [InlineData("Mulch hilft.\n¹ Erfunden, Max: Gartenbuch. Musterstadt 2021, S. 5.")]
    [InlineData("Siehe dazu Erfunden, Gartenbuch, Musterstadt: Grünverlag, 2021.")]
    [InlineData("Das ist belegt (Erfunden o. J.).")]
    [InlineData("Das ist belegt (n.d.).")]
    [InlineData("Das ist belegt, vgl. Erfunden, 2021, S. 4.")]
    [InlineData("Das ist belegt (Erfunden 2021 S. 5).")]
    [InlineData("Das ist belegt (Erfunden [2021]).")]
    [InlineData("Das ist belegt (Platon, 380 v. Chr.).")]
    [InlineData("Das ist belegt (Erfunden, 1200).")]
    public void Blocked(string text)
    {
        Assert.NotEmpty(ReferatKit.FreeCitations(text));
        Assert.NotNull(ReferatKit.KiTextProblem(text, Src));
    }

    [Theory(DisplayName = "T-0780 M1 (Tabelle): erzählende Formen und Klammern ohne Komma/Seite sind nur ein Hinweis – keine Ablehnung")]
    [InlineData("Wie Erfunden (2021) zeigt, spart Mulch Wasser.")]
    [InlineData("Angela Merkel (2005) wurde Bundeskanzlerin.")]
    [InlineData("1) Die Wiedervereinigung 1990 veränderte vieles.")]
    [InlineData("Das ist belegt (erfunden 2021).")]
    [InlineData("Die Französische Revolution (1789) veränderte Europa.")]
    [InlineData("Nach dem Mauerfall (1989) änderte sich viel.")]
    public void Hint_only(string text)
    {
        Assert.Empty(ReferatKit.FreeCitations(text));
        Assert.Null(ReferatKit.KiTextProblem(text, Src));
        Assert.Contains(ReferatKit.CitationHits(text), h => !h.Blocks);
    }

    [Theory(DisplayName = "T-0780 M1 (Tabelle, Gegenproben): normaler Text mit Zahlen und Jahren – weder Sperre noch Hinweis")]
    [InlineData("Im Jahr 2021 war der Sommer sehr trocken.")]
    [InlineData("Die Regentonne (Stand 2021) fasst 300 Liter.")]
    [InlineData("Im Juli (2021) regnete es kaum.")]
    [InlineData("Ein guter Boden braucht Zeit [@q-buch, S. 12].")]
    [InlineData("Seit 2019 sammeln wir Regenwasser, 2021 kam eine zweite Tonne dazu.")]
    [InlineData("Mehr dazu (vgl. Abschnitt 2).")]
    [InlineData("Die Tabelle (siehe Seite 4) zeigt es.")]
    [InlineData("Das kostet (rund 300 Euro) im Jahr.")]
    [InlineData("Ergebnis: Deutschland gewann die WM, 2014 war das.")]
    [InlineData("Hinweis: Große Ernte, 2021 im Herbst war sie gut.")]
    public void No_false_alarm(string text)
    {
        Assert.Empty(ReferatKit.CitationHits(text));
        Assert.Null(ReferatKit.KiTextProblem(text, Src));
    }

    [Theory(DisplayName = "T-0780 M (3. Gegenprüfung): eindeutige Kurzbelege ohne Komma (et al., &/und, 2021a, Seite), „(Name 2021)“ ohne passende Quelle und „ebd./a. a. O.“ ohne Verweis davor werden in KI-Text gesperrt")]
    [InlineData("Das ist belegt (Erfunden & Ander 2021).")]
    [InlineData("Das ist belegt (Erfunden und Ander 2021).")]
    [InlineData("Das ist belegt (Erfunden et al. 2021).")]
    [InlineData("Das ist belegt (Erfunden 2021a).")]
    [InlineData("Das ist belegt (Erfunden 2021: 5).")]
    [InlineData("Wie Erfunden (2021: 5) zeigt, ist das so.")]
    [InlineData("Wie Erfunden et al. (2021) zeigen, ist das so.")]
    [InlineData("Wie Erfunden (2021b) zeigt, ist das so.")]
    [InlineData("Das ist belegt (ebd.).")]
    [InlineData("Das ist belegt (ebd., S. 4).")]
    [InlineData("Das ist belegt (a. a. O., S. 5).")]
    public void Blocked_round3(string text) => Assert.NotNull(ReferatKit.KiTextProblem(text, Src));

    // ---------------- 4. Gegenprüfung: eine Regel für streng und lose ----------------

    [Theory(DisplayName = "T-0780 M (4. Gegenprüfung): strenge Formen sind immer gesperrt – auch mit Zeit-, Orts- oder Ereigniswort und auch in der Fußnote mit Ort")]
    [InlineData("Das ist belegt (Sommer, 2019, S. 4).")]
    [InlineData("Das ist belegt (Winter 2020: 5).")]
    [InlineData("Das ist belegt (Wahl et al. 2020).")]
    [InlineData("Das ist belegt (Stand, 2021).")]
    [InlineData("Die Zahl stieg deutlich (Deutschland, 2023).")]
    [InlineData("Das ist belegt (Berlin & Wien 2020).")]
    [InlineData("Das ist belegt (Juli und August 2021).")]
    [InlineData("Das ist belegt (Mai 2021a).")]
    [InlineData("Das ist belegt (vgl. Juli 2021).")]
    [InlineData("Das ist belegt, vgl. Sommer, 2019, S. 4.")]
    [InlineData("Wie Olympia (2024: 5) zeigt, ist das so.")]
    [InlineData("Mulch hilft.\n¹ Erfunden, Hans: Titel. Berlin 2021, S. 3.")]
    [InlineData("Siehe Erfunden, Titel, Wien: Grünverlag, 2021.")]
    [InlineData("Das ist belegt (ebd.).")]
    [InlineData("Das ist belegt (a. a. O., S. 5).")]
    public void Strict_always_blocked(string text)
    {
        Assert.Contains(ReferatKit.CitationHits(text), h => h.Blocks);
        Assert.Contains(ReferatKit.CitationHits(text, Src), h => h.Blocks);
        Assert.NotNull(ReferatKit.KiTextProblem(text, Src));
    }

    [Theory(DisplayName = "T-0780 M (4. Gegenprüfung): lose Formen mit Ort oder Ereignis sind nur ein Hinweis – 14 Fälle plus „(Berlin 2019)“, keine Sperre")]
    [InlineData("Danach war alles anders (Mauerfall 1989).")]
    [InlineData("Danach war alles anders (Brexit 2016).")]
    [InlineData("Danach war alles anders (Wiedervereinigung 1990).")]
    [InlineData("Die Spiele (Olympia 2024) fanden in Paris statt.")]
    [InlineData("Viele blieben zu Hause (Corona 2020).")]
    [InlineData("Es regnete wenig (Deutschland 2023).")]
    [InlineData("Europa wurde neu geordnet (Wien 1815).")]
    [InlineData("Die Stadt trauerte (Paris 2015).")]
    [InlineData("Das Unglück veränderte alles (Tschernobyl 1986).")]
    [InlineData("Das Unglück veränderte alles (Fukushima 2011).")]
    [InlineData("Das Wasser stieg hoch (Hamburg 1962).")]
    [InlineData("Die EU wuchs stark (Osterweiterung 2004).")]
    [InlineData("Die Zahl wurde neu erhoben (Zensus 2011).")]
    [InlineData("Viele gingen wählen (Bundestagswahl 2021).")]
    [InlineData("Die Mauer stand noch (Berlin 2019).")]
    [InlineData("In Deutschland (2023) regnete es wenig.")]
    public void Loose_place_or_event_hint(string text)
    {
        var hits = ReferatKit.CitationHits(text, Src);
        Assert.NotEmpty(hits); Assert.All(hits, h => Assert.False(h.Blocks));
        Assert.Null(ReferatKit.KiTextProblem(text, Src));
    }

    [Theory(DisplayName = "T-0780 M (4. Gegenprüfung): Quelle „Muster, Erika 2024“ – nur der ganze Nachname mit falschem Jahr sperrt; Teil des Namens oder Vorname ist ein Hinweis")]
    [InlineData("Gute Erde ist wichtig (Muster 1999).", true)]
    [InlineData("Wie Muster (1999) zeigt, ist Erde wichtig.", true)]
    [InlineData("Gute Erde ist wichtig (Muster 2024).", false)]
    [InlineData("Wie Muster (2024) zeigt, ist Erde wichtig.", false)]
    [InlineData("Gute Erde ist wichtig (Mus 2021).", false)]
    [InlineData("Gute Erde ist wichtig (Erika 2021).", false)]
    [InlineData("Gute Erde ist wichtig (Erfunden 2021).", false)]
    [InlineData("Guter Boden (Beispiel 2020).", true)]
    public void Loose_surname_rule(string text, bool blocks)
    {
        var hits = ReferatKit.CitationHits(text, Src);
        Assert.Single(hits); Assert.Equal(blocks, hits[0].Blocks);
        Assert.Equal(blocks, ReferatKit.KiTextProblem(text, Src) is not null);
        Assert.False(ReferatKit.CitationHits(text)[0].Blocks); // ohne Quellenliste: nie gesperrt
    }

    [Theory(DisplayName = "T-0780 M (4. Gegenprüfung): Zeitwörter unterdrücken nur den Hinweis bei losen Formen")]
    [InlineData("Die Regentonne (Stand 2021) fasst 300 Liter.")]
    [InlineData("Es war heiß (Juli 2021).")]
    [InlineData("Im Sommer (2019) war es trocken.")]
    public void Time_words_no_hint(string text)
    {
        Assert.Empty(ReferatKit.CitationHits(text, Src));
        Assert.Null(ReferatKit.KiTextProblem(text, Src));
    }

    [Fact(DisplayName = "T-0780 M (3. Gegenprüfung): „(Name 2021)“ mit passender Quelle und „ebd.“ nach echtem Verweis – nur Hinweis bzw. gar nichts")]
    public void Allowed_round3()
    {
        const string a = "Gute Erde ist wichtig (Muster 2024).";
        Assert.Null(ReferatKit.KiTextProblem(a, Src));
        Assert.Contains(ReferatKit.CitationHits(a, Src), h => !h.Blocks);
        const string b = "Gute Erde ist wichtig [@q-buch, S. 12]. Das gilt auch für Laub (ebd., S. 13).";
        Assert.Null(ReferatKit.KiTextProblem(b, Src));
        Assert.Empty(ReferatKit.CitationHits(b, Src));
    }

    [Fact(DisplayName = "T-0780 (2. Gegenprüfung, niedrig): Belege, die die Person schon geschrieben hat, sperren eine KI-Ergänzung nicht – neue schon")]
    public void Only_new_text_checked()
    {
        var before = Doc("Alter Text (Müller, 2019, S. 3) und [@alt].");
        Assert.Null(ReferatKit.KiCheck(before, Doc("Alter Text (Müller, 2019, S. 3) und [@alt]. Neu von der KI [@q-buch].")));
        Assert.NotNull(ReferatKit.KiCheck(before, Doc("Alter Text (Müller, 2019, S. 3) und [@alt]. Neu (Erfunden, 2021).")));
        Assert.NotNull(ReferatKit.KiCheck(before, Doc("Alter Text (Müller, 2019, S. 3) und [@alt]. Neu [@neu].")));
    }

    [Fact(DisplayName = "T-0780 M3 (2. Gegenprüfung): 2020a/2020b nur bei gleicher ganzer Personenliste – „Muster“ und „Muster; Beispiel“ im gleichen Jahr bleiben ohne Buchstaben")]
    public void Year_suffix_full_list()
    {
        var o = ReferatKit.ForOutput(
        [
            new() { Id = "a", Kind = "buch", Authors = "Muster, Erika", Title = "Eins", Year = "2020", Publisher = "V" },
            new() { Id = "b", Kind = "buch", Authors = "Muster, Erika; Beispiel, Max", Title = "Zwei", Year = "2020", Publisher = "V" },
            new() { Id = "c", Kind = "buch", Authors = "Muster, Erika; Beispiel, Max", Title = "Drei", Year = "2020", Publisher = "V" },
        ]);
        Assert.Equal(["2020", "2020b", "2020a"], o.Select(s => s.Year));
    }

    static JsonObject Doc(string text) => new()
    {
        ["sources"] = new JsonArray([.. Src.Select(s => (JsonNode)ReferatKit.SourceJson(s))]),
        ["sections"] = ReferatKit.SectionsJson([new("s1", 1, "Einleitung", text)]),
    };

    [Fact(DisplayName = "T-0780 H1/H2: gemeinsame Prüfung jeder KI-Änderung – geänderter Abschnitt mit freiem Beleg, unbekanntem Verweis oder neuer Quelle wird abgelehnt, unveränderter Alttext nicht")]
    public void Ki_check_after_change()
    {
        var before = Doc("Alter Text der Person (Müller, 2019).");
        Assert.Null(ReferatKit.KiCheck(before, before.DeepClone().AsObject())); // Text der Person bleibt unbeanstandet, solange die KI ihn nicht ändert
        var after = Doc("Neuer Text (Erfunden, 2021).");
        Assert.Contains("Erfunden, 2021", ReferatKit.KiCheck(before, after));
        Assert.Contains("[@neu]", ReferatKit.KiCheck(before, Doc("Neuer Text [@neu].")));
        var title = before.DeepClone().AsObject(); title["sections"] = ReferatKit.SectionsJson([new("s1", 1, "Einleitung (vgl. Erfunden 2021)", "Alter Text der Person (Müller, 2019).")]);
        Assert.NotNull(ReferatKit.KiCheck(before, title)); // auch Überschriften (Gliederung)
        var src = before.DeepClone().AsObject(); ((JsonArray)src["sources"]!).Add(ReferatKit.SourceJson(new CiteKit.Source { Id = "q-x", Title = "Erfunden", Year = "2021" }));
        Assert.Contains("Quellen", ReferatKit.KiCheck(before, src));
        Assert.Null(ReferatKit.KiCheck(before, Doc("Neuer Text [@q-buch, S. 3].")));
    }

    [Fact(DisplayName = "T-0780 H2: Seitenangabe außerhalb der bekannten Seiten einer echten Quelle wird gemeldet (Warnung, nicht blockierend)")]
    public void Page_plausibility()
    {
        Assert.Equal(99, ReferatKit.PageOutside("99", "45–67"));
        Assert.Null(ReferatKit.PageOutside("50", "45–67"));
        Assert.Equal(12, ReferatKit.PageOutside("12–14", "45-67"));
        Assert.Null(ReferatKit.PageOutside("12", ""));
        var f = ReferatKit.Check(Doc("Text [@q-art, S. 99]."));
        Assert.Contains(f, x => !x.Blocks && x.Text.Contains("Seite 99"));
    }

    // ---------------- M3: Kennungen mit Umlauten und Leerzeichen ----------------

    [Fact(DisplayName = "T-0780 M3: Verweise mit Umlaut-Kennung und Leerzeichen („[@müller2020]“, „[ @neu]“) werden erkannt, unbekannte blockieren die Ausgabe")]
    public void Citation_ids_unicode()
    {
        Assert.Equal(["müller2020", "neu", "q.a:1"], ReferatKit.Citations("A [@müller2020] B [ @neu] C [@q.a:1, S. 3]").Select(c => c.Id));
        Assert.Equal(["müller2020", "neu"], ReferatKit.UnknownCitations("A [@müller2020] B [ @neu] [@q-buch]", Src));
        var o = Doc("Text [ @neu].");
        Assert.Contains(ReferatKit.Check(o), x => x.Blocks);
        Assert.Throws<InvalidOperationException>(() => ReferatKit.BuildText(o));
    }

    // ---------------- M4: 2020a/2020b ----------------

    [Fact(DisplayName = "T-0780 M4: gleiche Erstautorin im gleichen Jahr wird 2020a/2020b – im Kurzbeleg und im Literaturverzeichnis")]
    public void Year_suffix()
    {
        var two = new List<CiteKit.Source>
        {
            new() { Id = "a", Kind = "buch", Authors = "Muster, Erika", Title = "Zweites Buch", Year = "2020", Publisher = "V" },
            new() { Id = "b", Kind = "buch", Authors = "Muster, Erika", Title = "Anderes Buch", Year = "2020", Publisher = "V" },
            new() { Id = "c", Kind = "buch", Authors = "Beispiel, Max", Title = "Drittes", Year = "2020", Publisher = "V" },
        };
        var o = ReferatKit.ForOutput(two);
        Assert.Equal(["2020b", "2020a", "2020"], o.Select(s => s.Year));
        Assert.Equal("(Muster, 2020a)", ReferatKit.Resolve("[@b]", o, "apa"));
        var doc = new JsonObject { ["title"] = "T", ["sources"] = new JsonArray([.. two.Select(s => (JsonNode)ReferatKit.SourceJson(s))]), ["sections"] = ReferatKit.SectionsJson([new("s1", 1, "E", "x [@a] y [@b]")]) };
        var text = ReferatKit.BuildText(doc);
        Assert.Contains(text.Paras, p => p.Text.Contains("(Muster, 2020b)") && p.Text.Contains("(Muster, 2020a)"));
        Assert.Contains(text.Paras, p => p.Text.StartsWith("Muster, E. (2020a). Anderes Buch"));
    }

    // ---------------- H5: Rundreise aller Datumsarten ----------------

    [Theory(DisplayName = "T-0780 H5: jede Datumsart (auch Jahrzehnte v. Chr. und „0er“) wird so geschrieben, dass Parse sie gleich wieder liest")]
    [InlineData("50er v. Chr.", -59, DatePrecision.Decade)]
    [InlineData("ca. 1990er", 1990, DatePrecision.Decade)]
    [InlineData("0er", 1, DatePrecision.Decade)]
    [InlineData("0er v. Chr.", -9, DatePrecision.Decade)]
    [InlineData("1. Jh. v. Chr.", -100, DatePrecision.Century)]
    [InlineData("1. Jh.", 1, DatePrecision.Century)]
    [InlineData("1. Jt. v. Chr.", -1000, DatePrecision.Millennium)]
    [InlineData("3. März 44 v. Chr.", -44, DatePrecision.Day)]
    [InlineData("Februar 1 v. Chr.", -1, DatePrecision.Month)]
    [InlineData("ca. 753 v. Chr.", -753, DatePrecision.Year)]
    [InlineData("29. Februar 2000", 2000, DatePrecision.Day)]
    public void Roundtrip_all_kinds(string text, int year, DatePrecision p)
    {
        var d = HistDate.Parse(text)!.Value;
        Assert.Equal((year, p), (d.Year, d.Precision));
        Assert.Equal(d, HistDate.Parse(d.ToString()));
        Assert.Equal(d, HistDate.Parse(d.Short()));
        Assert.True(d.PositionEnd > d.Position);
    }

    [Fact(DisplayName = "T-0780 H5: alle Jahrzehnte von 990 v. Chr. bis 2020 n. Chr. überstehen Schreiben und Lesen")]
    public void Roundtrip_decades()
    {
        for (var dec = 0; dec <= 990; dec += 10) { var d = HistDate.Parse($"{dec}er v. Chr.")!.Value; Assert.Equal(d, HistDate.Parse(d.ToString())); Assert.Equal($"{dec}er v. Chr.", d.ToString()); }
        for (var dec = 0; dec <= 2020; dec += 10) { var d = HistDate.Parse($"{dec}er")!.Value; Assert.Equal(d, HistDate.Parse(d.ToString())); Assert.Equal($"{dec}er", d.ToString()); }
        Assert.True(HistDate.Parse("50er v. Chr.")!.Value.Position < HistDate.Parse("40er v. Chr.")!.Value.Position);
    }

    [Fact(DisplayName = "T-0780 (niedrig): ISO-Jahre zählen astronomisch – „-0044“ ist 45 v. Chr., „0000“ ist 1 v. Chr.; „-44“ bleibt 44 v. Chr. (TimelineJS)")]
    public void Iso_years()
    {
        Assert.Equal(-45, HistDate.Parse("-0044")!.Value.Year);
        Assert.Equal(-45, HistDate.Parse("-0044-03-15")!.Value.Year);
        Assert.Equal(-1, HistDate.Parse("0000")!.Value.Year);
        Assert.Equal(-44, HistDate.Parse("-44")!.Value.Year);
        Assert.Equal(1789, HistDate.Parse("1789-07-14")!.Value.Year);
    }

    [Fact(DisplayName = "T-0780 H5: FromJson meldet verworfene Einträge (unlesbares Datum, Ende, Farbe, Bild) statt sie still fallen zu lassen")]
    public void From_json_reports()
    {
        var o = JsonNode.Parse("""{"title":"x","tracks":[{"id":"t1","name":"A","color":"rot"}],"items":[{"id":"e1","title":"Gut","start":"1789","track":"t1"},{"id":"e2","title":"Kaputt","start":"irgendwann","track":"t1"},{"id":"e3","title":"Ende","start":"1800","end":"bald","track":"t1"}]}""")!.AsObject();
        var t = TimelineKit.FromJson(o);
        Assert.Equal(2, t.Items.Count);
        Assert.Contains(t.Problems, p => p.Contains("Kaputt") && p.Contains("ausgelassen"));
        Assert.Contains(t.Problems, p => p.Contains("Ende") && p.Contains("bald"));
        Assert.Contains(t.Problems, p => p.Contains("rot"));
    }

    // ---------------- H4: Farben ----------------

    [Fact(DisplayName = "T-0780 H4: ungültige Farben aus Dokument und CSV werden ersetzt – kein Script oder fremdes Attribut im SVG, ToPdf bricht nicht ab")]
    public void Colors_sanitized()
    {
        Assert.True(TimelineKit.ValidColor("#D4A017")); Assert.True(TimelineKit.ValidColor("#80D4A017"));
        Assert.False(TimelineKit.ValidColor("#D4A01")); Assert.False(TimelineKit.ValidColor("red")); Assert.False(TimelineKit.ValidColor("#GGGGGG"));
        var evil = "#fff\" onload=\"alert(1)\"><script>alert(1)</script><x a=\"";
        var o = new JsonObject
        {
            ["title"] = "x",
            ["tracks"] = new JsonArray(new JsonObject { ["id"] = "t1", ["name"] = "A", ["color"] = evil }),
            ["items"] = new JsonArray(new JsonObject { ["id"] = "e1", ["title"] = "Ereignis", ["start"] = "1789", ["track"] = "t1", ["color"] = evil },
                                      new JsonObject { ["id"] = "e2", ["title"] = "Zeit", ["start"] = "1700", ["end"] = "1750", ["track"] = "t1", ["color"] = "javascript:x" }),
        };
        var t = TimelineKit.FromJson(o);
        Assert.All(t.Tracks, x => Assert.True(TimelineKit.ValidColor(x.Color)));
        Assert.All(t.Items, x => Assert.True(x.Color.Length == 0 || TimelineKit.ValidColor(x.Color)));
        // auch wenn eine ungültige Farbe am Lesen vorbei ins Modell kommt: das Bild bleibt sauber
        t.Items[0] = t.Items[0] with { Color = evil };
        var svg = TimelineKit.ToSvg(TimelineKit.Layout(t, 1650, 1850, 1200));
        XDocument.Parse(svg);
        Assert.DoesNotContain("script", svg, StringComparison.OrdinalIgnoreCase); Assert.DoesNotContain("onload", svg, StringComparison.OrdinalIgnoreCase);
        var root = Temp();
        try { TimelineKit.ToPdf(TimelineKit.Layout(t, 1650, 1850, 1200), Path.Combine(root, "z.pdf")); Assert.True(File.Exists(Path.Combine(root, "z.pdf"))); }
        finally { Directory.Delete(root, true); }
        var (csv, _) = TimelineKit.FromCsv("Art;Titel;Beginn;Ende;Spur;Beschreibung;Farbe\nEreignis;X;1789;;A;;\"#fff\"\" onload=\"\"x\"\nEreignis;Y;1790;;A;;#00FF00\n");
        Assert.Equal(["", "#00FF00"], csv.Items.Select(i => i.Color));
    }

    // ---------------- M1: Bilder ----------------

    [Fact(DisplayName = "T-0780 M1: Bilder aus Dokument/JSON nur als echtes PNG/JPEG bis zur Höchstgröße – SVG/HTML/zu große Daten werden verworfen und gemeldet")]
    public void Images_checked()
    {
        var png = PngCodec.Encode(new Pixmap(4, 4, Enumerable.Repeat((byte)200, 64).ToArray()));
        Assert.True(TimelineKit.ValidImage(png));
        Assert.False(TimelineKit.ValidImage(System.Text.Encoding.UTF8.GetBytes("<svg onload=alert(1)>")));
        Assert.False(TimelineKit.ValidImage(new byte[TimelineKit.MaxImageBytes + 10]));
        var big = new byte[TimelineKit.MaxImageBytes + 100]; png.CopyTo(big, 0);
        var o = new JsonObject
        {
            ["items"] = new JsonArray(
                new JsonObject { ["id"] = "e1", ["title"] = "Gut", ["start"] = "1789", ["image"] = Convert.ToBase64String(png) },
                new JsonObject { ["id"] = "e2", ["title"] = "Svg", ["start"] = "1790", ["image"] = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("<svg onload=alert(1)>")) },
                new JsonObject { ["id"] = "e3", ["title"] = "Riesig", ["start"] = "1791", ["image"] = Convert.ToBase64String(big) }),
        };
        var t = TimelineKit.FromJson(o);
        Assert.Equal([true, false, false], t.Items.Select(i => i.Image is not null));
        Assert.Equal(2, t.Problems.Count(p => p.Contains("Bild")));
        Assert.DoesNotContain("<svg onload", TimelineKit.ToSvg(TimelineKit.Layout(t, 1780, 1800, 1200)));
    }

    // ---------------- M2: Steuerzeichen ----------------

    [Fact(DisplayName = "T-0780 M2: Steuerzeichen im Text ergeben trotzdem gültiges XML in DOCX, ODT, PPTX, ODP und SVG")]
    public void Control_chars_valid_xml()
    {
        var bad = "Titel\u0001mit\u0008Steuer\u000Bzeichen￾";
        Assert.Equal("TitelmitSteuerzeichen", XmlSafe.Clean(bad));
        Assert.Equal("😀 ok\t", XmlSafe.Clean("😀 ok\t\u0000"));
        var root = Temp();
        try
        {
            var o = new JsonObject
            {
                ["title"] = bad, ["author"] = bad, ["style"] = "apa",
                ["sources"] = new JsonArray(ReferatKit.SourceJson(new CiteKit.Source { Id = "q", Kind = "buch", Authors = "Muster, Erika", Title = bad, Year = "2024", Publisher = "V" })),
                ["sections"] = ReferatKit.SectionsJson([new("s1", 1, bad, bad + " [@q]")]),
            };
            var text = ReferatKit.BuildText(o); var deck = ReferatKit.BuildSlides(o);
            var files = new Dictionary<string, string[]>
            {
                [Path.Combine(root, "r.docx")] = ["word/document.xml"], [Path.Combine(root, "r.odt")] = ["content.xml", "meta.xml"],
                [Path.Combine(root, "r.pptx")] = ["ppt/slides/slide1.xml"], [Path.Combine(root, "r.odp")] = ["content.xml"],
            };
            DocxFormat.Write(text, Path.Combine(root, "r.docx")); OdtFormat.Write(text, Path.Combine(root, "r.odt"));
            SlideFiles.WritePptx(deck, Path.Combine(root, "r.pptx")); SlideFiles.WriteOdp(deck, Path.Combine(root, "r.odp"));
            foreach (var (f, parts) in files)
            {
                using var z = ZipFile.OpenRead(f);
                foreach (var p in parts) { using var st = z.GetEntry(p)!.Open(); XDocument.Load(st); }
            }
            var t = new Timeline { Title = bad }; t.Tracks.Add(new("t", bad, "#D4A017")); t.Items.Add(new("e", bad, HistDate.Parse("1789")!.Value, null, "t", bad));
            XDocument.Parse(TimelineKit.ToSvg(TimelineKit.Layout(t, 1700, 1900, 1200)));
        }
        finally { Directory.Delete(root, true); }
    }

    // ---------------- niedrig ----------------

    [Fact(DisplayName = "T-0780 (niedrig): „o. J.“ ohne doppelten Punkt, „*“ im Titel bleibt Text, Tab in ODP als <text:tab/>, Liste→Zeilen nur bei mehrzeiligen Parametern")]
    public void Small_points()
    {
        var noYear = new CiteKit.Source { Id = "a", Kind = "buch", Authors = "Muster, Erika", Title = "Ohne Jahr", Publisher = "V" };
        foreach (var st in new[] { "apa", "harvard", "din" }) Assert.DoesNotContain("o. J..", CiteKit.Format(noYear, st));
        var star = ReferatKit.ForOutput([new CiteKit.Source { Id = "s", Kind = "buch", Authors = "Muster, Erika", Title = "Sterne *hell*", Year = "2020", Publisher = "V" }]);
        var doc = new JsonObject { ["title"] = "T", ["sources"] = new JsonArray(ReferatKit.SourceJson(star[0] with { Title = "Sterne *hell*" })), ["sections"] = ReferatKit.SectionsJson([new("s1", 1, "E", "x [@s]")]) };
        Assert.Contains(ReferatKit.BuildText(doc).Paras, p => p.Text.Contains("Sterne ∗hell∗"));
        var root = Temp();
        try
        {
            var deck = new SlideDoc(); var s = deck.AddSlide(SlideLayout.TitelInhalt); s.ByRole("title")!.Text = "A\tB"; s.Notes = "x\ty";
            var f = Path.Combine(root, "t.odp"); SlideFiles.WriteOdp(deck, f);
            using var z = ZipFile.OpenRead(f); using var r = new StreamReader(z.GetEntry("content.xml")!.Open());
            var xml = r.ReadToEnd();
            Assert.Contains("A<text:tab/>B", xml); Assert.Contains("x<text:tab/>y", xml);
        }
        finally { Directory.Delete(root, true); }
        var single = new AssistAction("x.y", "X", ["x"], [new AssistParam("titel", "Titel")]);
        Assert.False(AssistChain.FromKi("x", """{"steps":[{"action":"x.y","args":{"titel":["a","b"]}}]}""", [single]).Steps.SingleOrDefault()?.Args.ContainsKey("titel") ?? false);
        var multi = new AssistAction("x.y", "X", ["x"], [new AssistParam("zeilen", "Zeilen", Multiline: true)]);
        Assert.Equal("a\nb", AssistChain.FromKi("x", """{"steps":[{"action":"x.y","args":{"zeilen":["a","b"]}}]}""", [multi]).Steps.Single().Args["zeilen"]);
    }
}
