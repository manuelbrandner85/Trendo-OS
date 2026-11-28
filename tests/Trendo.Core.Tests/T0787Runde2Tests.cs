using System.Text.Json.Nodes;
using Trendo.Core.Wissen;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0787 zweite Prüfrunde. Diese Klasse nutzt nur Schnittstellen von 3496aede4 – sie übersetzt dort und ist dort rot
/// (Rot-Lauf: reports/T-0787/runde2/rot-runde2.txt). Fälle mit neuen Schnittstellen: <see cref="T0787_Runde2Neu"/>.</summary>
public class T0787_Runde2
{
    static Rezept R(string id, string name, params string[] zutaten) => new(id, name, 2, [.. zutaten.Select(RezeptKit.LiesZutat)], ["Kochen."]);

    [Fact(DisplayName = "T-0787 R2-1: Punkt mit drei Ziffern – führende 0 ist Dezimalpunkt, bei kg/l Dezimalpunkt, bei g/ml Tausenderpunkt; immer mit Hinweis außer „0.250“")]
    public void Punktregel()
    {
        Assert.Equal(0.25, RezeptKit.LiesZutat("0.250 kg Mehl").Menge); Assert.Equal("kg", RezeptKit.LiesZutat("0.250 kg Mehl").Einheit);
        Assert.Equal(1.5, RezeptKit.LiesZutat("1.500 kg Kartoffeln").Menge);
        Assert.Equal(3.125, RezeptKit.LiesZutat("3.125 l Wasser").Menge);
        Assert.Equal(1500, RezeptKit.LiesZutat("1.500 g Mehl").Menge);
        Assert.Equal(1000, RezeptKit.LiesZutat("1.000 ml Milch").Menge);
        Assert.NotNull(RezeptKit.Lies("1.500 kg Kartoffeln").Hinweis); Assert.NotNull(RezeptKit.Lies("1.500 g Mehl").Hinweis);
        Assert.Null(RezeptKit.Lies("0.250 kg Mehl").Hinweis);
    }

    [Fact(DisplayName = "T-0787 R2-1: JSON-LD-Import meldet mehrdeutige Mengen und markiert die Zutat mit „Menge prüfen“ – nichts still übernommen")]
    public void Import_meldet_mehrdeutig()
    {
        var json = new JsonObject { ["@context"] = "https://schema.org", ["@type"] = "Recipe", ["name"] = "Brot", ["recipeIngredient"] = new JsonArray("1.500 kg Mehl", "200 g Wasser") }.ToJsonString();
        var (got, notes) = RezeptKit.Import(json);
        var r = Assert.Single(got);
        Assert.Contains(notes, n => n.Contains("mehrdeutig"));
        Assert.Contains("Menge prüfen", r.Zutaten[0].Notiz);
        Assert.Equal("", r.Zutaten[1].Notiz);
    }

    [Fact(DisplayName = "T-0787 R2-3: weitere Gesundheitsversprechen nach Wortfamilien erkannt; „Der Teig heilt nicht“ und die Fehlalarme aus Runde 1 bleiben frei")]
    public void Versprechen_wortfamilien()
    {
        foreach (var bad in new[] { "Ein gesundes Frühstück", "Ideal zum Abnehmen", "Schutz vor Krebs", "gut gegen Husten", "wirkt gegen Müdigkeit", "fördert die Verdauung", "senkt das Risiko",
                                    "Blutdruck senkend", "wirkt lindernd", "antioxidativ", "stärkt die Knochen", "gut für das Herz", "ein echtes Superfood", "Heil-Tee aus Kamille", "Heiltee" })
            Assert.True(RezeptKit.Gesundheitsversprechen(bad).Count > 0 || RezeptKit.Gesundheitshinweise(bad).Count > 0, "nicht erkannt: " + bad); // 3. Runde: gesperrt ODER „Bitte prüfen“
        foreach (var ok in new[] { "Der Teig heilt nicht", "Hilft beim Frischhalten der Kräuter.", "Er verbeugt sich vor dem Koch.", "Medium gebraten.", "Den Deckel abnehmen und rühren.", "Das Ei bleibt heil." })
            Assert.True(RezeptKit.Gesundheitsversprechen(ok).Count == 0, ok + ": " + string.Join(", ", RezeptKit.Gesundheitsversprechen(ok)));
    }

    [Fact(DisplayName = "T-0787 R2-4: deutsche Schreibweise „1.000,5 g“ und „1.250,75 ml“")]
    public void Deutsche_schreibweise()
    {
        Assert.Equal(1000.5, RezeptKit.LiesZutat("1.000,5 g Mehl").Menge);
        Assert.Equal(1250.75, RezeptKit.LiesZutat("1.250,75 ml Milch").Menge);
        Assert.Null(RezeptKit.Lies("1.250,75 ml Milch").Hinweis);
    }

    [Fact(DisplayName = "T-0787 R2-5: doppelte Kennung – das zuerst vorhandene Rezept behält seine, das Duplikat bekommt eine freie (auch wenn „x-2“ schon echt vergeben ist); Plan bleibt beim ersten")]
    public void Duplikat_bekommt_freie_kennung()
    {
        var d = RezeptKit.Neu();
        d["rezepte"] = new JsonArray(RezeptKit.RezeptJson(R("x", "Erstes", "1 Ei")), RezeptKit.RezeptJson(R("x", "Duplikat", "1 Apfel")), RezeptKit.RezeptJson(R("x-2", "Echtes x-2", "1 Banane")));
        d["plan"] = RezeptKit.PlanJson(new DateTime(2026, 11, 30), [new("p1", 0, "Mittag", "x", 2), new("p2", 1, "Mittag", "x-2", 2)]);
        var l = RezeptKit.Rezepte(d);
        Assert.Equal("x", l[0].Id); Assert.Equal("x-2", l[2].Id); Assert.Equal("Echtes x-2", l[2].Name);
        Assert.NotEqual("x", l[1].Id); Assert.NotEqual("x-2", l[1].Id); Assert.Equal(3, l.Select(r => r.Id).Distinct().Count());
        var plan = RezeptKit.Plan(d);
        Assert.Equal("Erstes", l.Single(r => r.Id == plan[0].Rezept).Name);
        Assert.Equal("Echtes x-2", l.Single(r => r.Id == plan[1].Rezept).Name);
    }

    [Fact(DisplayName = "T-0787 R2-8: „10 x 10 x 10 g“, „3 x Eier“, „-1 g“ gemeldet, „1 Dose Bohnen“ = Dosenbohnen, Bund + Gramm zusammengelegt, Zeiten höchstens 7 Tage")]
    public void Kleinigkeiten()
    {
        var z = RezeptKit.LiesZutat("10 x 10 x 10 g Zucker"); Assert.Equal(1000, z.Menge); Assert.Equal("Zucker", z.Name);
        z = RezeptKit.LiesZutat("3 x Eier"); Assert.Equal(3, z.Menge); Assert.Equal("Eier", z.Name);
        var (neg, h) = RezeptKit.Lies("-1 g Salz"); Assert.Null(neg.Menge); Assert.Contains("Negative", h);
        Assert.Equal(2, RezeptKit.LiesZutat("- 2 Eier").Menge); // Aufzählungsstrich mit Leerzeichen
        Assert.Equal("kidneybohnen", RezeptKit.LiesZutat("1 Dose Bohnen").Lm);
        var d = RezeptKit.Neu();
        d["rezepte"] = RezeptKit.RezepteJson([R("a", "A", "1 Bund Petersilie"), R("b", "B", "10 g Petersilie")]);
        d["plan"] = RezeptKit.PlanJson(new DateTime(2026, 11, 30), [new("p1", 0, "Mittag", "a", 2), new("p2", 1, "Mittag", "b", 2)]);
        var pe = Assert.Single(RezeptKit.Einkaufsliste(d), p => p.Lm == "petersilie");
        Assert.Equal("g", pe.Einheit); Assert.Equal(40, pe.Menge);
        var json = new JsonObject { ["@type"] = "Recipe", ["name"] = "Sauerteig", ["recipeIngredient"] = new JsonArray("500 g Mehl"), ["totalTime"] = "P400D", ["prepTime"] = "P30D" }.ToJsonString();
        var r = Assert.Single(RezeptKit.Import(json).Rezepte);
        Assert.InRange(r.Vorbereitung, 0, 7 * 24 * 60); Assert.InRange(r.Garzeit, 0, 7 * 24 * 60);
    }
}

/// <summary>T-0787 zweite Prüfrunde: Fälle mit neuen Schnittstellen.</summary>
public class T0787_Runde2Neu
{
    [Fact(DisplayName = "T-0787 R2-7: Zeitüberschreitung der Versprechen-Prüfung wird als Hinweis gemeldet statt abzustürzen (KiCheck lehnt ab)")]
    public void Zeitgrenze_ehrlich()
    {
        var text = string.Concat(Enumerable.Repeat("beugt aaaa bbbb cccc dddd ", 6000));
        var hits = RezeptKit.Gesundheitsversprechen(text, TimeSpan.FromTicks(1)); // eigene Zeitgrenze als Argument – stört keine parallelen Tests
        Assert.StartsWith(RezeptKit.Zeitgrenze, Assert.Single(hits));
        var vorher = RezeptKit.Neu(); var nachher = RezeptKit.Neu();
        nachher["rezepte"] = RezeptKit.RezepteJson([new Rezept("k", "Tee", 1, [RezeptKit.LiesZutat("1 Beutel Tee")], [text])]);
        Assert.StartsWith(RezeptKit.Zeitgrenze, RezeptKit.KiCheck(vorher, nachher, TimeSpan.FromTicks(1)));
        Assert.Empty(RezeptKit.Gesundheitsversprechen("Eine Suppe."));
    }

    [Fact(DisplayName = "T-0787 R2-1: Zeilen ohne Rückfrage (Einlesen, KI) bekommen „Menge prüfen“; eindeutige bleiben unverändert")]
    public void Pruefvermerk()
    {
        var h = new List<string>();
        Assert.Contains("Menge prüfen", RezeptKit.LiesMitPruefvermerk("1.500 g Mehl, gesiebt", h).Notiz);
        Assert.Contains("gesiebt", RezeptKit.LiesMitPruefvermerk("1.500 g Mehl, gesiebt").Notiz);
        Assert.Equal("", RezeptKit.LiesMitPruefvermerk("1.500,5 g Mehl").Notiz);
        Assert.Single(h);
        Assert.Equal(7 * 24 * 60, RezeptKit.MaxMinutenImport);
    }
}
