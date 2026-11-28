using System.Text.Json.Nodes;
using Trendo.Core.Wissen;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0787 dritte Prüfrunde. Diese Klasse nutzt nur Schnittstellen von b6ffe500c – sie übersetzt dort und ist dort rot
/// (Rot-Lauf: reports/T-0787/runde3/rot-runde3.txt). Fälle mit neuen Schnittstellen: <see cref="T0787_Runde3Neu"/>.</summary>
public class T0787_Runde3
{
    /// <summary>Normale Kochsätze aus allen Prüfrunden – dürfen nie gesperrt werden.</summary>
    public static readonly string[] Frei =
    [
        // Runde 3
        "Eine gesunde Portion Butter dazugeben.", "Den Topf zum Abnehmen vom Herd nehmen.", "Beim Abnehmen des Deckels Dampf entweichen lassen.", "Rühren, um das Risiko des Anbrennens zu senken.",
        "Den Teig gut gegen die Schüsselwand drücken.", "Zitronensaft wirkt gegen das Braunwerden der Äpfel.", "Ein Schuss Essig hilft gegen das Ausflocken.", "Etwas Milch hilft bei zu dickem Teig.",
        "Dem Anbrennen vorbeugend ab und zu rühren.", "Den Teig beugen und vor dem Ofen kurz ruhen lassen.", "Mit Heilwasser aufgießen.", "Gesundheit!",
        // Runden 1–2
        "Hilft beim Frischhalten der Kräuter.", "Er verbeugt sich vor dem Koch.", "Medium gebraten.", "Der Teig heilt nicht.", "Den Deckel abnehmen und rühren.", "Das Ei bleibt heil.",
        "Eine wärmende Suppe für kalte Tage.", "Die Soße abkühlen lassen.",
    ];
    /// <summary>Eindeutige Gesundheitsversprechen – werden gesperrt.</summary>
    public static readonly string[] Gesperrt =
    [
        "Diese Suppe heilt Erkältungen.", "Senkt den Blutdruck.", "Senkt Blutzucker.", "wirkt entzündungshemmend", "1 TL Kurkuma, heilt Entzündungen", "beugt Erkältungen vor", "lindert Halsschmerzen",
        "Ideal zum Abnehmen.", "Zum Abnehmen geeignet.", "Hilft beim Abnehmen.", "Schutz vor Krebs.", "gut gegen Husten", "wirkt gegen Müdigkeit", "fördert die Verdauung", "Blutdruck senkend",
        "antioxidativ", "stärkt die Knochen", "gut für das Herz", "ein echtes Superfood", "Detox-Smoothie", "entgiftet den Körper", "Ingwer, entzündungshemmend", "senkt das Risiko für Herzinfarkt",
    ];
    /// <summary>Weiche Wörter – nur „Bitte prüfen“.</summary>
    public static readonly string[] Hinweis =
    [
        "Ein gesundes Frühstück.", "Die gesündere Variante mit Vollkorn.", "Heil-Tee aus Kamille", "Heiltee", "wirkt lindernd", "senkt das Risiko", "gesünder als Pommes",
    ];

    [Fact(DisplayName = "T-0787 R3-1: Fehlalarm-Sätze aus allen Runden bleiben frei (keine Sperre)")]
    public void Fehlalarme_frei()
    {
        var falsch = Frei.Where(t => RezeptKit.Gesundheitsversprechen(t).Count > 0).Select(t => t + " → " + string.Join(", ", RezeptKit.Gesundheitsversprechen(t))).ToList();
        Assert.True(falsch.Count == 0, "gesperrt, obwohl Kochsatz: " + string.Join(" | ", falsch));
    }

    [Fact(DisplayName = "T-0787 R3-1: eindeutige Versprechen werden gesperrt – auch über KiCheck")]
    public void Versprechen_gesperrt()
    {
        var fehlt = Gesperrt.Where(t => RezeptKit.Gesundheitsversprechen(t).Count == 0).ToList();
        Assert.True(fehlt.Count == 0, "nicht gesperrt: " + string.Join(" | ", fehlt));
        foreach (var weich in Hinweis) Assert.True(RezeptKit.Gesundheitsversprechen(weich).Count == 0, "weiches Wort gesperrt: " + weich);
        var vorher = RezeptKit.Neu(); var nachher = RezeptKit.Neu();
        nachher["rezepte"] = RezeptKit.RezepteJson([new Rezept("k", "Brot", 1, [RezeptKit.LiesZutat("500 g Mehl")], ["Kneten.", "Die gesündere Variante mit Vollkorn."])]);
        Assert.Null(RezeptKit.KiCheck(vorher, nachher)); // weich: keine Ablehnung
        nachher["rezepte"] = RezeptKit.RezepteJson([new Rezept("k", "Brot", 1, [RezeptKit.LiesZutat("500 g Mehl")], ["Kneten.", "Den Topf zum Abnehmen vom Herd nehmen."])]);
        Assert.Null(RezeptKit.KiCheck(vorher, nachher));
        nachher["rezepte"] = RezeptKit.RezepteJson([new Rezept("k", "Brot", 1, [RezeptKit.LiesZutat("500 g Mehl")], ["Kneten.", "Zum Abnehmen geeignet."])]);
        Assert.NotNull(RezeptKit.KiCheck(vorher, nachher));
    }

    [Fact(DisplayName = "T-0787 R3-2: Bereich mit Tausenderpunkt „1.500-2.000 g“ = 1500 bis 2000 g; „1.5-2 kg“ bleibt 1,5 bis 2")]
    public void Bereich()
    {
        var z = RezeptKit.LiesZutat("1.500-2.000 g Mehl"); Assert.Equal(1500, z.Menge); Assert.Equal(2000, z.Bis); Assert.Equal("g", z.Einheit);
        z = RezeptKit.LiesZutat("1.500–2.000 kg Kartoffeln"); Assert.Equal(1.5, z.Menge); Assert.Equal(2, z.Bis);
        z = RezeptKit.LiesZutat("1,5-2 kg Kartoffeln"); Assert.Equal(1.5, z.Menge); Assert.Equal(2, z.Bis);
        z = RezeptKit.LiesZutat("2–3 Eier"); Assert.Equal(2, z.Menge); Assert.Equal(3, z.Bis);
        Assert.NotNull(RezeptKit.Lies("1.500-2.000 g Mehl").Hinweis);
    }

    [Fact(DisplayName = "T-0787 R3-3: englische Einheiten (cups, tbsp, tsp, oz, lb, fl oz, pint, quart) und häufige englische Zutatennamen – dieselben USDA-Zeilen")]
    public void Englisch()
    {
        var z = RezeptKit.LiesZutat("2 cups flour"); Assert.Equal("cup", z.Einheit); Assert.Equal("weizenmehl", z.Lm);
        Assert.Equal(2 * 236.588 * NaehrwertTabelle.Find("weizenmehl")!.Dichte, RezeptKit.Gramm(z)!.Value, 3);
        Assert.Equal("tbsp", RezeptKit.LiesZutat("1 tbsp sugar").Einheit); Assert.Equal("zucker", RezeptKit.LiesZutat("1 tbsp sugar").Lm);
        Assert.Equal("tsp", RezeptKit.LiesZutat("1/2 tsp salt").Einheit); Assert.Equal("salz", RezeptKit.LiesZutat("1/2 tsp salt").Lm);
        z = RezeptKit.LiesZutat("8 oz butter"); Assert.Equal("oz", z.Einheit); Assert.Equal(8 * 28.3495, RezeptKit.Gramm(z)!.Value, 3); Assert.Equal("butter", z.Lm);
        z = RezeptKit.LiesZutat("1 lb ground beef"); Assert.Equal("lb", z.Einheit); Assert.Equal("rinderhack", z.Lm);
        z = RezeptKit.LiesZutat("4 fl oz milk"); Assert.Equal("floz", z.Einheit); Assert.Equal("vollmilch", z.Lm);
        Assert.Equal("pint", RezeptKit.LiesZutat("1 pint heavy cream").Einheit); Assert.Equal("sahne", RezeptKit.LiesZutat("1 pint heavy cream").Lm);
        Assert.Equal("quart", RezeptKit.LiesZutat("1 quart water").Einheit);
        Assert.Equal("ei", RezeptKit.LiesZutat("3 eggs").Lm); Assert.Equal("olivenoel", RezeptKit.LiesZutat("2 tbsp olive oil").Lm);
        var (m, n) = RezeptKit.InMetrisch(new Rezept("x", "x", 1, [RezeptKit.LiesZutat("1 cup milk"), RezeptKit.LiesZutat("2 tbsp sugar")], []));
        Assert.Equal(2, n); Assert.Equal("ml", m.Zutaten[0].Einheit); Assert.Equal("g", m.Zutaten[1].Einheit);
        // Import: nicht zuordenbare englische Namen – ehrlicher Hinweis
        var json = new JsonObject { ["@type"] = "Recipe", ["name"] = "Pancakes", ["inLanguage"] = "en", ["recipeIngredient"] = new JsonArray("2 cups flour", "1 cup buttermilk", "2 tbsp molasses") }.ToJsonString();
        var (got, notes) = RezeptKit.Import(json);
        Assert.Single(got); Assert.Contains(notes, x => x.Contains("englische Zutatennamen nicht zugeordnet") && x.Contains("molasses"));
    }

    [Fact(DisplayName = "T-0787 R3-4: „1,000 g“ mit Hinweis „Menge prüfen“; „1,500 TL“ bleibt 1,5 TL; „1 Dose weiße Bohnen“ = Bohnen aus der Dose")]
    public void Kleinigkeiten()
    {
        var (z, h) = RezeptKit.Lies("1,000 g Mehl"); Assert.Equal(1000, z.Menge); Assert.Contains("prüfen", h);
        Assert.Equal(1.5, RezeptKit.LiesZutat("1,500 TL Salz").Menge);
        Assert.NotNull(RezeptKit.Lies("1.500 TL Salz").Hinweis);
        Assert.Equal("kidneybohnen", RezeptKit.LiesZutat("1 Dose weiße Bohnen").Lm);
    }
}

/// <summary>T-0787 dritte Prüfrunde: Fälle mit neuen Schnittstellen.</summary>
public class T0787_Runde3Neu
{
    [Fact(DisplayName = "T-0787 R3-1: weiche Wörter ergeben nur einen Hinweis (auch „gesündere Variante“); Kochsätze ergeben auch keinen Hinweis; Prüfung nennt „Bitte prüfen“")]
    public void Hinweise()
    {
        var fehlt = T0787_Runde3.Hinweis.Where(t => RezeptKit.Gesundheitshinweise(t).Count == 0).ToList();
        Assert.True(fehlt.Count == 0, "kein Hinweis: " + string.Join(" | ", fehlt));
        var falsch = T0787_Runde3.Frei.Where(t => RezeptKit.Gesundheitshinweise(t).Count > 0).Select(t => t + " → " + string.Join(", ", RezeptKit.Gesundheitshinweise(t))).ToList();
        Assert.True(falsch.Count == 0, "Hinweis bei Kochsatz: " + string.Join(" | ", falsch));
        Assert.Contains(RezeptKit.Pruefe(new Rezept("k", "Brot", 1, [RezeptKit.LiesZutat("500 g Mehl")], ["Die gesündere Variante."])), f => f.StartsWith("Bitte prüfen"));
        Assert.Contains("cup", RezeptKit.EnglischeEinheiten);
        Assert.Contains(NaehrwertTabelle.Englisch, e => e.En == "flour" && e.Id == "weizenmehl");
        Assert.All(NaehrwertTabelle.Englisch, e => Assert.NotNull(NaehrwertTabelle.Find(e.Id)));
    }
}
