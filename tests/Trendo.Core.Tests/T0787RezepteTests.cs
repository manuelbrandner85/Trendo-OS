using System.Text.Json.Nodes;
using Trendo.Core.Office;
using Trendo.Core.Wissen;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0787: Fachkern „Rezepte &amp; Ernährung“ (STUDIO-RECIPE-01) – Zutaten lesen, Portionen und Einheiten, Nährwerte aus USDA SR Legacy (CC0), Wochenplan,
/// Einkaufsliste, schema.org/Recipe (JSON-LD) hin und zurück, Druckfassung, keine Gesundheitsversprechen.</summary>
public class T0787_Rezepte
{
    static Rezept Pfannkuchen() => new("r1", "Pfannkuchen", 4,
        [.. new[] { "250 g Weizenmehl", "500 ml Milch", "3 Eier", "1 Prise Salz", "2 EL Butter, zerlassen", "Zucker nach Geschmack" }.Select(RezeptKit.LiesZutat)],
        ["Mehl, Milch, Eier und Salz glatt rühren.", "In der Pfanne mit etwas Butter ausbacken."]);

    [Fact(DisplayName = "T-0787 Zutaten lesen: Mengen (Bruch, Komma, Bereich), Einheiten, Hinweis nach Komma, ohne Menge, Zwischenüberschrift; Zuordnung zur Tabelle")]
    public void Zutaten_lesen()
    {
        var z = RezeptKit.LiesZutat("200 g Weizenmehl (Type 405)");
        Assert.Equal(200, z.Menge); Assert.Equal("g", z.Einheit); Assert.Equal("weizenmehl", z.Lm);
        z = RezeptKit.LiesZutat("1/2 TL Salz"); Assert.Equal(0.5, z.Menge); Assert.Equal("tl", z.Einheit); Assert.Equal("salz", z.Lm);
        z = RezeptKit.LiesZutat("1 ½ EL Olivenöl"); Assert.Equal(1.5, z.Menge); Assert.Equal("el", z.Einheit); Assert.Equal("olivenoel", z.Lm);
        z = RezeptKit.LiesZutat("2–3 Zwiebeln, fein gewürfelt"); Assert.Equal(2, z.Menge); Assert.Equal(3, z.Bis); Assert.Equal("", z.Einheit); Assert.Equal("Zwiebeln", z.Name); Assert.Equal("fein gewürfelt", z.Notiz); Assert.Equal("zwiebel", z.Lm);
        z = RezeptKit.LiesZutat("0,75 l Gemüsebrühe"); Assert.Equal(0.75, z.Menge); Assert.Equal("l", z.Einheit); Assert.Equal("haehnchenbruehe", z.Lm);
        z = RezeptKit.LiesZutat("200g Rinderhackfleisch"); Assert.Equal(200, z.Menge); Assert.Equal("g", z.Einheit); Assert.Equal("rinderhack", z.Lm);
        z = RezeptKit.LiesZutat("2 Knoblauchzehen"); Assert.Equal("knoblauch", z.Lm);
        z = RezeptKit.LiesZutat("Pfeffer nach Geschmack"); Assert.Null(z.Menge); Assert.Equal("Pfeffer", z.Name); Assert.Equal("nach Geschmack", z.Notiz); Assert.Equal("pfeffer", z.Lm);
        z = RezeptKit.LiesZutat("Für den Teig:"); Assert.True(z.Gruppe); Assert.Equal("Für den Teig", z.Name);
        z = RezeptKit.LiesZutat("1 Dose Tomaten, stückig"); Assert.Equal("dose", z.Einheit); Assert.Equal("tomaten-dose", RezeptKit.LiesZutat("1 Dose gehackte Tomaten").Lm);
        Assert.Equal("Bio-Weizenmehl", RezeptKit.LiesZutat("100 g Bio-Weizenmehl").Name); Assert.Equal("weizenmehl", RezeptKit.LiesZutat("100 g Bio-Weizenmehl").Lm);
        Assert.Equal("", RezeptKit.LiesZutat("1 Stange Zimtblüten-Fantasie").Lm == "zimt" ? "x" : ""); // keine gewagte Zuordnung über Wortanfänge unter 5 Zeichen
        Assert.Equal("200 g Weizenmehl (Type 405)", RezeptKit.Zeile(RezeptKit.LiesZutat("200 g Weizenmehl (Type 405)")));
    }

    [Fact(DisplayName = "T-0787 Portionen umrechnen: Mengen mal neu/alt, Brüche bei Löffel und Stück, Einheiten werden glatt (g→kg, TL→EL)")]
    public void Portionen_und_einheiten()
    {
        var r = RezeptKit.Skaliere(Pfannkuchen(), 8);
        Assert.Equal(8, r.Portionen);
        Assert.Equal("500 g Weizenmehl", RezeptKit.Zeile(r.Zutaten[0]));
        Assert.Equal("1 l Milch", RezeptKit.Zeile(r.Zutaten[1]));
        Assert.Equal("6 Eier", RezeptKit.Zeile(r.Zutaten[2]));
        Assert.Equal("4 EL Butter, zerlassen", RezeptKit.Zeile(r.Zutaten[4]));
        Assert.Equal("Zucker, nach Geschmack", RezeptKit.Zeile(r.Zutaten[5]));
        var half = RezeptKit.Skaliere(Pfannkuchen(), 2);
        Assert.Equal("1 ½ Eier", RezeptKit.Zeile(half.Zutaten[2]));
        Assert.Equal("1 EL Butter, zerlassen", RezeptKit.Zeile(half.Zutaten[4]));
        Assert.Equal("½ Prise Salz", RezeptKit.Zeile(half.Zutaten[3]));
        // 3 TL → 1 EL, 1500 g → 1,5 kg
        Assert.Equal("el", RezeptKit.Glaette(new Zutat(3, null, "tl", "Zucker")).Einheit);
        Assert.Equal("1,5 kg Kartoffeln", RezeptKit.Zeile(RezeptKit.Glaette(new Zutat(1500, null, "g", "Kartoffeln"))));
        // Löffel in Gramm (feste Zutat mit Dichte) bzw. Milliliter (Flüssigkeit)
        var (m, n) = RezeptKit.InMetrisch(new Rezept("x", "x", 2, [RezeptKit.LiesZutat("2 EL Zucker"), RezeptKit.LiesZutat("3 EL Olivenöl"), RezeptKit.LiesZutat("1 Tasse Milch")], []));
        Assert.Equal(3, n);
        Assert.Equal("g", m.Zutaten[0].Einheit); Assert.InRange(m.Zutaten[0].Menge!.Value, 25, 26.5); // 30 ml × 0,85 g/ml
        Assert.Equal("ml", m.Zutaten[1].Einheit); Assert.Equal(45, m.Zutaten[1].Menge);
        Assert.Equal("ml", m.Zutaten[2].Einheit); Assert.Equal(250, m.Zutaten[2].Menge);
    }

    [Fact(DisplayName = "T-0787 Nährwerte je Portion aus USDA SR Legacy: Rechnung nachvollziehbar, Stückgewicht, Dichte, Fehlendes und Ohne-Menge benannt")]
    public void Naehrwerte()
    {
        var p = Pfannkuchen();
        var mehl = NaehrwertTabelle.Find("weizenmehl")!; var milch = NaehrwertTabelle.Find("vollmilch")!; var ei = NaehrwertTabelle.Find("ei")!; var butter = NaehrwertTabelle.Find("butter")!; var salz = NaehrwertTabelle.Find("salz")!;
        Assert.Equal(168936, mehl.FdcId); Assert.Equal(364, mehl.Kcal); Assert.Equal(171287, ei.FdcId); Assert.Equal(50, ei.Stueck);
        var erwartet = 2.5 * mehl.Kcal + 5 * milch.Dichte * milch.Kcal + 1.5 * ei.Kcal + 0.005 * salz.Kcal + 0.30 * butter.Dichte * butter.Kcal; // 250 g, 500 ml, 3 × 50 g, 0,5 g, 30 ml
        var n = RezeptKit.Berechne(p);
        Assert.Equal(erwartet, n.Kcal, 3);
        Assert.Equal(5, n.Erfasst); Assert.Empty(n.Fehlt); Assert.Equal(["Zucker"], n.OhneMenge);
        var je = RezeptKit.JePortion(p); Assert.Equal(erwartet / 4, je.Kcal, 3);
        Assert.Equal(n.Salz / 4, je.Salz, 6);
        // nicht bestimmbar: Bund ohne Gewicht, unbekannte Zutat
        var r = new Rezept("x", "x", 1, [RezeptKit.LiesZutat("1 Bund Koriander"), RezeptKit.LiesZutat("100 g Drachenfrucht-Pulver")], []);
        var nn = RezeptKit.Berechne(r);
        Assert.Equal(0, nn.Erfasst); Assert.Equal(2, nn.Fehlt.Count);
        Assert.Contains("Richtwerte", NaehrwertTabelle.Hinweis); Assert.Contains("CC0", NaehrwertTabelle.Lizenz);
        Assert.True(NaehrwertTabelle.Alle.Length >= 150);
        Assert.All(NaehrwertTabelle.Alle, l => { Assert.True(l.FdcId > 100000); Assert.InRange(l.Kcal, 0, 900); Assert.Contains(l.Abteilung, RezeptKit.Abteilungen); });
        Assert.Equal(NaehrwertTabelle.Alle.Length, NaehrwertTabelle.Alle.Select(l => l.Id).Distinct().Count());
    }

    [Fact(DisplayName = "T-0787 Einkaufsliste: gleiche Zutaten aus mehreren Rezepten zusammengefasst, nach Portionen im Plan umgerechnet, nach Abteilung sortiert; CSV mit Schutz vor Formeln")]
    public void Einkaufsliste()
    {
        var doc = RezeptKit.Neu();
        var a = Pfannkuchen();
        var b = new Rezept("r2", "Rührei", 2, [RezeptKit.LiesZutat("4 Eier"), RezeptKit.LiesZutat("100 ml Milch"), RezeptKit.LiesZutat("1 Prise Salz"), RezeptKit.LiesZutat("=1+1 Testzutat")], ["Verquirlen und stocken lassen."]);
        doc["rezepte"] = RezeptKit.RezepteJson([a, b]);
        doc["plan"] = RezeptKit.PlanJson(new DateTime(2026, 11, 30), [new("p1", 0, "Mittag", "r1", 4), new("p2", 2, "Frühstück", "r2", 4), new("p3", 3, "Abend", "gibt-es-nicht", 2)]);
        var list = RezeptKit.Einkaufsliste(doc);
        var eier = list.Single(p => p.Lm == "ei"); Assert.True(eier.Menge == 3 + 8, string.Join(" | ", list.Select(p => p.Key + "=" + p.Text))); Assert.Equal("", eier.Einheit); Assert.Equal(["Pfannkuchen", "Rührei"], eier.Fuer);
        var milch = list.Single(p => p.Lm == "vollmilch"); Assert.Equal(700, milch.Menge); Assert.Equal("ml", milch.Einheit);
        Assert.Equal("Zucker", list.Single(p => p.Name == "Zucker").Name); Assert.Null(list.Single(p => p.Name == "Zucker").Menge);
        Assert.True(list.FindIndex(p => p.Lm == "weizenmehl") < list.FindIndex(p => p.Lm == "ei")); // Backzutaten vor Milch & Eiern (Gang durch den Laden)
        var csv = RezeptKit.EinkaufCsv(doc);
        Assert.StartsWith("Abteilung;Menge;Einheit;Zutat;Für;Erledigt\n", csv);
        Assert.Contains(";'=1+1 Testzutat;", csv);
        Assert.Equal(2, RezeptKit.Plan(doc).Count); // Eintrag mit unbekanntem Rezept fällt weg
        var txt = RezeptKit.EinkaufText(doc); Assert.Contains("Montag, 30. November 2026", txt); Assert.Contains("[ ] 11 Ei (Hühnerei)", txt);
    }

    [Fact(DisplayName = "T-0787 schema.org/Recipe: Export als JSON-LD und Rezeptseite, Import zurück (auch @graph, HowToSection, ISO-Dauer, Ertrag als Liste) ohne Verlust")]
    public void JsonLd_hin_und_zurueck()
    {
        var r = Pfannkuchen() with { Beschreibung = "Dünne Pfannkuchen", Vorbereitung = 10, Garzeit = 20, Kategorie = "Hauptgericht", Kueche = "Deutsch", Stichwoerter = "schnell, süß" };
        var json = RezeptKit.JsonLd([r]);
        var o = JsonNode.Parse(json)!.AsObject();
        Assert.Equal("https://schema.org", o["@context"]!.GetValue<string>()); Assert.Equal("Recipe", o["@type"]!.GetValue<string>());
        Assert.Equal("PT10M", o["prepTime"]!.GetValue<string>()); Assert.Equal("PT30M", o["totalTime"]!.GetValue<string>());
        Assert.Equal(6, o["recipeIngredient"]!.AsArray().Count); Assert.Equal("HowToStep", o["recipeInstructions"]![0]!["@type"]!.GetValue<string>());
        Assert.EndsWith(" kcal", o["nutrition"]!["calories"]!.GetValue<string>()); Assert.Contains("Richtwert", o["nutrition"]!["description"]!.GetValue<string>());
        var (back, notes) = RezeptKit.Import(json);
        Assert.Empty(notes);
        var b = Assert.Single(back);
        Assert.Equal(r.Name, b.Name); Assert.Equal(4, b.Portionen); Assert.Equal(10, b.Vorbereitung); Assert.Equal(20, b.Garzeit); Assert.Equal("Hauptgericht", b.Kategorie);
        Assert.Equal(r.Zutaten.Select(z => RezeptKit.Zeile(z)), b.Zutaten.Select(z => RezeptKit.Zeile(z)));
        Assert.Equal(r.Schritte, b.Schritte);
        Assert.Equal(RezeptKit.JePortion(r).Kcal, RezeptKit.JePortion(b).Kcal, 6);
        // Rezeptseite: JSON-LD eingebettet, Import aus HTML
        var html = RezeptKit.Html([r, Pfannkuchen() with { Id = "r9", Name = "Zweites </script> Rezept" }]);
        Assert.Contains("<script type=\"application/ld+json\">", html); Assert.DoesNotContain("Zweites </script>", html);
        var (fromHtml, _) = RezeptKit.Import(html); Assert.Equal(2, fromHtml.Count); Assert.Equal("Zweites Rezept", fromHtml[1].Name); // HTML-Marken im Namen fallen beim Lesen weg (wie bei Rezeptseiten üblich)
        // Fremde Struktur nach der Google-Doku für Rezepte: @graph mit WebPage, Recipe mit @type-Liste, HowToSection, Ertrag als Liste, Zeiten in Stunden
        const string fremd = """
        <html><head><script type="application/ld+json">{"@context":"https://schema.org","@graph":[{"@type":"WebPage","name":"Seite"},
        {"@type":["Recipe","NewsArticle"],"name":"Linsensuppe","recipeYield":["6","6 Teller"],"prepTime":"PT15M","totalTime":"PT1H15M","recipeCategory":["Suppe","Eintopf"],
         "keywords":["vegan","winter"],"recipeIngredient":["250 g Linsen","2 Karotten","1 l Gemüsebrühe","Salz"],
         "recipeInstructions":[{"@type":"HowToSection","name":"Vorbereiten","itemListElement":[{"@type":"HowToStep","text":"Gemüse <b>schälen</b>."}]},{"@type":"HowToStep","text":"Alles 45 Minuten kochen."}]}]}</script></head><body></body></html>
        """;
        var (f, fn) = RezeptKit.Import(fremd); Assert.Empty(fn);
        var l = Assert.Single(f);
        Assert.Equal("Linsensuppe", l.Name); Assert.Equal(6, l.Portionen); Assert.Equal("Teller", l.PortionWort); Assert.Equal(15, l.Vorbereitung); Assert.Equal(60, l.Garzeit);
        Assert.Equal("Suppe, Eintopf", l.Kategorie); Assert.Equal("vegan, winter", l.Stichwoerter);
        Assert.Equal(["Vorbereiten: Gemüse schälen .", "Alles 45 Minuten kochen."], l.Schritte.Select(s => s.Replace("schälen.", "schälen .")));
        Assert.Equal("linsen", l.Zutaten[0].Lm); Assert.Equal("karotte", l.Zutaten[1].Lm);
        Assert.Equal(90, RezeptKit.Minuten("PT1H30M")); Assert.Equal("PT1H30M", RezeptKit.Iso(90)); Assert.Equal("PT45M", RezeptKit.Iso(45)); Assert.Equal("PT2H", RezeptKit.Iso(120));
        Assert.Single(RezeptKit.Import("<html>kein rezept</html>").Hinweise);
        Assert.Single(RezeptKit.Import("{ kaputt").Hinweise);
    }

    [Fact(DisplayName = "T-0787 Freitext → Rezept: Name, Portionen, Zutaten und nummerierte Schritte aus eingefügtem Text")]
    public void Aus_text()
    {
        var r = RezeptKit.AusText("Tomatensoße\nFür 4 Portionen\nZutaten\n1 Zwiebel\n2 EL Olivenöl\n1 Dose Tomaten\nSalz und Pfeffer\n\nZubereitung\n1. Zwiebel würfeln und im Öl andünsten.\n2. Tomaten dazugeben und 20 Minuten köcheln.\nAbschmecken.");
        Assert.Equal("Tomatensoße", r.Name); Assert.Equal(4, r.Portionen);
        Assert.Equal(4, r.Zutaten.Count); Assert.Equal("olivenoel", r.Zutaten[1].Lm);
        Assert.Equal(["Zwiebel würfeln und im Öl andünsten.", "Tomaten dazugeben und 20 Minuten köcheln.", "Abschmecken."], r.Schritte);
    }

    [Fact(DisplayName = "T-0787 Keine Gesundheitsversprechen: KI-Text mit Heilversprechen wird abgelehnt, Plan und Liste ändert nur die Person; Prüfung nennt fehlende Nährwerte")]
    public void Keine_versprechen()
    {
        Assert.NotEmpty(RezeptKit.Gesundheitsversprechen("Diese Suppe heilt Erkältungen."));
        Assert.NotEmpty(RezeptKit.Gesundheitsversprechen("Senkt den Blutdruck"));
        Assert.Empty(RezeptKit.Gesundheitsversprechen("Eine wärmende Suppe für kalte Tage."));
        var vorher = RezeptKit.Neu(); vorher["rezepte"] = RezeptKit.RezepteJson([Pfannkuchen()]);
        var nachher = (JsonObject)vorher.DeepClone();
        nachher["rezepte"] = RezeptKit.RezepteJson([Pfannkuchen(), new Rezept("r2", "Detox-Smoothie", 1, [RezeptKit.LiesZutat("1 Apfel")], ["Mixen – entgiftet den Körper."])]);
        Assert.Contains("Gesundheitsversprechen", RezeptKit.KiCheck(vorher, nachher));
        nachher["rezepte"] = RezeptKit.RezepteJson([Pfannkuchen(), new Rezept("r2", "Apfel-Smoothie", 1, [RezeptKit.LiesZutat("1 Apfel")], ["Mixen."])]);
        Assert.Null(RezeptKit.KiCheck(vorher, nachher));
        nachher["plan"] = RezeptKit.PlanJson(DateTime.Today, [new("p", 0, "Mittag", "r1", 2)]);
        Assert.Contains("Wochenplan", RezeptKit.KiCheck(vorher, nachher));
        var f = RezeptKit.Pruefe(new Rezept("x", "", 2, [RezeptKit.LiesZutat("1 Bund Koriander")], []));
        Assert.Contains(f, x => x.Contains("Namen")); Assert.Contains(f, x => x.Contains("Arbeitsschritte")); Assert.Contains(f, x => x.Contains("Koriander"));
    }

    [Fact(DisplayName = "T-0787 Druck: Rezept, Kochbuch und Wochenplan als PDF/DOCX/ODT – Inhalt mit Richtwert-Hinweis und Quelle der Nährwerte")]
    public void Druck_dateien()
    {
        var dir = Directory.CreateTempSubdirectory("trendo-T0787-druck-").FullName;
        try
        {
            var r = Pfannkuchen();
            var pdf = Path.Combine(dir, "rezept.pdf"); TextPdf.Export(RezeptKit.Druck(r), pdf);
            Assert.True(Seiten(pdf) >= 1); // Text im PDF prüft der App-Test (PDFium) und der Fremdlauf (pdftotext)
            var druck = RezeptKit.Druck(r).PlainText();
            Assert.Contains("Zutaten", druck); Assert.Contains("Richtwerte", druck); Assert.Contains("FoodData Central", druck); Assert.Contains("250 g", druck);
            var docx = Path.Combine(dir, "rezept.docx"); DocxFormat.Write(RezeptKit.Druck(r), docx);
            Assert.Contains("Weizenmehl", DocxFormat.Read(docx).PlainText());
            var odt = Path.Combine(dir, "rezept.odt"); OdtFormat.Write(RezeptKit.Kochbuch([r, r with { Id = "r2", Name = "Crêpes" }], "Unser Kochbuch"), odt);
            Assert.Contains("Crêpes", OdtFormat.Read(odt).PlainText());
            var d = RezeptKit.Neu(); d["rezepte"] = RezeptKit.RezepteJson([r]); d["plan"] = RezeptKit.PlanJson(new DateTime(2026, 11, 30), [new("p", 4, "Abend", "r1", 2)]);
            var w = Path.Combine(dir, "woche.pdf"); TextPdf.Export(RezeptKit.WochenDruck(d), w);
            Assert.True(Seiten(w) >= 1);
            var wt = RezeptKit.WochenDruck(d).PlainText(); Assert.True(wt.Contains("Freitag"), wt); Assert.Contains("Einkaufsliste", wt);
            Assert.Throws<InvalidOperationException>(() => RezeptKit.WochenDruck(RezeptKit.Neu()));
        }
        finally { Directory.Delete(dir, true); }
    }

    static int Seiten(string pdf)
    {
        Assert.Equal("%PDF", File.ReadAllText(pdf)[..4]);
        using var doc = PdfSharp.Pdf.IO.PdfReader.Open(pdf, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import);
        return doc.PageCount;
    }
}
