using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using Trendo.Core.Wissen;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0787 Gegenprüfung (Funde 2–10). Diese Klasse nutzt nur Schnittstellen, die es schon auf f3d579a88 gab – sie übersetzt dort und ist dort rot
/// (Rot-Lauf: reports/T-0787/rot-gegenpruefung.txt). Fälle mit neuen Schnittstellen stehen in <see cref="T0787_GegenpruefungNeu"/>.</summary>
public class T0787_Gegenpruefung
{
    static Rezept R(string id, string name, params string[] zutaten) => new(id, name, 2, [.. zutaten.Select(RezeptKit.LiesZutat)], ["Kochen."]);

    [Fact(DisplayName = "T-0787 G2: offene <script>-Marken (300 KB) – Import in linearer Zeit mit ehrlicher Meldung, kein quadratischer Regex")]
    public void Import_offene_script_marken_linear()
    {
        var sb = new StringBuilder("<html><head>");
        while (sb.Length < 300_000) sb.Append("<script type=\"application/ld+json\">{\"a\":1}");
        var sw = Stopwatch.StartNew();
        var (got, notes) = RezeptKit.Import(sb.ToString());
        sw.Stop();
        Assert.Empty(got); Assert.NotEmpty(notes);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5), $"Import brauchte {sw.Elapsed.TotalSeconds:0.0} s");
    }

    [Fact(DisplayName = "T-0787 G3: doppelte und leere Rezept-Kennungen – Einkaufsliste, Wochendruck und KiCheck werfen nicht, Kennungen danach eindeutig und stabil")]
    public void Doppelte_kennungen()
    {
        var d = RezeptKit.Neu();
        d["rezepte"] = new JsonArray(RezeptKit.RezeptJson(R("x", "Erstes", "2 Eier")), RezeptKit.RezeptJson(R("x", "Zweites", "100 g Mehl")), RezeptKit.RezeptJson(R("x", "Drittes", "1 Apfel")));
        d["plan"] = RezeptKit.PlanJson(new DateTime(2026, 11, 30), [new("p1", 0, "Mittag", "x", 2)]);
        var list = RezeptKit.Rezepte(d);
        Assert.Equal(3, list.Select(r => r.Id).Distinct().Count());
        Assert.Equal(list.Select(r => r.Id), RezeptKit.Rezepte(d).Select(r => r.Id)); // stabil über mehrere Lesevorgänge
        Assert.NotEmpty(RezeptKit.Einkaufsliste(d));
        Assert.NotNull(RezeptKit.WochenDruck(d));
        Assert.Contains("Ei", RezeptKit.EinkaufText(d));
        var vorher = RezeptKit.Neu(); vorher["rezepte"] = new JsonArray(RezeptKit.RezeptJson(R("x", "Erstes", "2 Eier")));
        Assert.NotNull(RezeptKit.KiCheck(vorher, d)); // doppelte Kennung von der KI: abgelehnt statt Absturz
        // leere Kennungen: fest je Stelle, nicht bei jedem Lesen neu
        var e = RezeptKit.Neu(); e["rezepte"] = new JsonArray(new JsonObject { ["name"] = "Ohne Kennung", ["portionen"] = 2 });
        Assert.Equal(RezeptKit.Rezepte(e)[0].Id, RezeptKit.Rezepte(e)[0].Id);
    }

    [Fact(DisplayName = "T-0787 G4/G7: Mengen – „ca./etwa 200 g“, Tausenderpunkt „1.000 g“, Aufzählungsstrich „–2 Eier“, „2 x 200 g“; „1e5 g“, „0 g“ und riesige Mengen nicht übernommen")]
    public void Mengen_parser()
    {
        var z = RezeptKit.LiesZutat("ca. 200 g Mehl"); Assert.Equal(200, z.Menge); Assert.Equal("g", z.Einheit); Assert.Equal("weizenmehl", z.Lm); Assert.Contains("ca.", z.Notiz);
        z = RezeptKit.LiesZutat("etwa 200 g Zucker"); Assert.Equal(200, z.Menge); Assert.Equal("zucker", z.Lm);
        z = RezeptKit.LiesZutat("1.000 g Mehl"); Assert.Equal(1000, z.Menge); Assert.Equal("g", z.Einheit);
        z = RezeptKit.LiesZutat("1.5 l Milch"); Assert.Equal(1.5, z.Menge); // Punkt mit einer Ziffer: Kommazahl
        z = RezeptKit.LiesZutat("–2 Eier"); Assert.Equal(2, z.Menge); Assert.Equal("ei", z.Lm);
        z = RezeptKit.LiesZutat("2 x 200 g Quark"); Assert.Equal(400, z.Menge); Assert.Equal("g", z.Einheit);
        Assert.Null(RezeptKit.LiesZutat("1e5 g Mehl").Menge);
        Assert.Null(RezeptKit.LiesZutat("1E+20 g Mehl").Menge);
        Assert.Null(RezeptKit.LiesZutat("0 g Mehl").Menge);
        Assert.Null(RezeptKit.LiesZutat("2000000 g Mehl").Menge);
        // was übernommen wird, bleibt beim Speichern und Neuladen gleich
        foreach (var line in new[] { "ca. 200 g Mehl", "1.000 g Mehl", "2 x 200 g Quark", "1e5 g Mehl", "0 g Mehl" })
        {
            var a = RezeptKit.LiesZutat(line); var b = RezeptKit.ZutatVon(RezeptKit.ZutatJson(a));
            Assert.Equal(a.Menge, b.Menge);
        }
    }

    [Fact(DisplayName = "T-0787 G5: Gesundheitsversprechen auch in Zutaten und Hinweisen; ganze Wörter statt Teilstring (keine Fehlalarme), weitere Wendungen erkannt")]
    public void Versprechen_genauer()
    {
        foreach (var ok in new[] { "Hilft beim Frischhalten der Kräuter.", "Er verbeugt sich vor dem Koch.", "Die Soße heilen lassen? Nein: ziehen lassen.".Replace("heilen", "abkühlen"), "Medium gebraten." })
            Assert.Empty(RezeptKit.Gesundheitsversprechen(ok));
        foreach (var bad in new[] { "Senkt Blutzucker", "wirkt entzündungshemmend", "heilt Entzündungen", "beugt Erkältungen vor", "lindert Halsschmerzen", "senkt den Blutdruck" })
            Assert.NotEmpty(RezeptKit.Gesundheitsversprechen(bad));
        var vorher = RezeptKit.Neu();
        var nachher = RezeptKit.Neu();
        nachher["rezepte"] = new JsonArray(RezeptKit.RezeptJson(new Rezept("k", "Goldene Milch", 1, [RezeptKit.LiesZutat("1 TL Kurkuma, heilt Entzündungen"), RezeptKit.LiesZutat("200 ml Milch")], ["Erwärmen."])));
        Assert.Contains("Gesundheitsversprechen", RezeptKit.KiCheck(vorher, nachher));
        nachher["rezepte"] = new JsonArray(RezeptKit.RezeptJson(new Rezept("k", "Tee", 1, [RezeptKit.LiesZutat("1 Beutel Tee")], ["Aufgießen."]) { Notizen = "Senkt Blutzucker." }));
        Assert.Contains("Gesundheitsversprechen", RezeptKit.KiCheck(vorher, nachher));
        Assert.Contains(RezeptKit.Pruefe(new Rezept("k", "Tee", 1, [RezeptKit.LiesZutat("1 TL Ingwer, entzündungshemmend")], ["Aufgießen."])), f => f.Contains("Gesundheitsversprechen"));
    }

    [Fact(DisplayName = "T-0787 G6: riesige Portionenzahl im Text – kein Überlauf, Portionen bleiben gültig")]
    public void AusText_ueberlauf()
    {
        var r = RezeptKit.AusText("Suppe\nFür 99999999999999999999 Portionen\nZutaten\n1 Zwiebel\nZubereitung\nKochen.");
        Assert.InRange(r.Portionen, 1, RezeptKit.MaxPortionen);
        r = RezeptKit.AusText("Suppe\nZutaten für 99999999999999999999 Personen\n1 Zwiebel\nZubereitung\nKochen.");
        Assert.InRange(r.Portionen, 1, RezeptKit.MaxPortionen);
    }

    [Fact(DisplayName = "T-0787 G8: Einkaufsliste – „1 Dose Tomaten“ ist Dosenware; Stück und Gramm derselben Zutat mit Stückgewicht zusammengelegt")]
    public void Einkauf_dose_und_stueck()
    {
        Assert.Equal("tomaten-dose", RezeptKit.LiesZutat("1 Dose Tomaten").Lm);
        Assert.Equal("tomate", RezeptKit.LiesZutat("2 Tomaten").Lm);
        Assert.Equal("kichererbsen", RezeptKit.LiesZutat("1 Dose Kichererbsen").Lm);
        var d = RezeptKit.Neu();
        d["rezepte"] = RezeptKit.RezepteJson([R("a", "A", "2 Zwiebeln"), R("b", "B", "100 g Zwiebeln")]);
        d["plan"] = RezeptKit.PlanJson(new DateTime(2026, 11, 30), [new("p1", 0, "Mittag", "a", 2), new("p2", 1, "Mittag", "b", 2)]);
        var zw = Assert.Single(RezeptKit.Einkaufsliste(d), p => p.Lm == "zwiebel");
        Assert.Equal("g", zw.Einheit); Assert.Equal(2 * NaehrwertTabelle.Find("zwiebel")!.Stueck + 100, zw.Menge);
    }

    [Fact(DisplayName = "T-0787 G9: Import-Grenzen – höchstens MaxRezepte über alle Blöcke, negative/riesige Zeiten begrenzt")]
    public void Import_grenzen()
    {
        static string Block(int n) => "<script type=\"application/ld+json\">[" + string.Join(",", Enumerable.Range(0, n).Select(i => $"{{\"@type\":\"Recipe\",\"name\":\"R{i}\",\"recipeIngredient\":[\"1 Ei\"],\"prepTime\":\"-5\",\"cookTime\":\"PT99999999H\"}}")) + "]</script>";
        var html = "<html>" + Block(1500) + Block(1500) + "</html>";
        var (got, notes) = RezeptKit.Import(html);
        Assert.Equal(RezeptKit.MaxRezepte, got.Count);
        Assert.NotEmpty(notes);
        Assert.All(got.Take(3), r => { Assert.InRange(r.Vorbereitung, 0, 100_000); Assert.InRange(r.Garzeit, 0, 100_000); });
    }

    [Fact(DisplayName = "T-0787 G10: Fett-Prozent in den Namen wie in den USDA-Daten (Vollmilch 3,25 %, Schlagsahne 36 %)")]
    public void Fettprozent_wie_usda()
    {
        Assert.Equal("Vollmilch (3,25 %)", NaehrwertTabelle.Find("vollmilch")!.Name);
        Assert.Equal("Schlagsahne (36 %)", NaehrwertTabelle.Find("sahne")!.Name);
        Assert.Equal("Fettarme Milch (1 %)", NaehrwertTabelle.Find("fettarme-milch")!.Name);
        Assert.Equal("Naturjoghurt (3,3 %)", NaehrwertTabelle.Find("joghurt")!.Name);
        Assert.InRange(NaehrwertTabelle.Find("sahne")!.Fett, 35.5, 36.5); Assert.InRange(NaehrwertTabelle.Find("vollmilch")!.Fett, 3.1, 3.3);
        Assert.Equal("vollmilch", RezeptKit.LiesZutat("500 ml Milch").Lm); Assert.Equal("sahne", RezeptKit.LiesZutat("200 ml Sahne").Lm);
    }
}

/// <summary>T-0787 Gegenprüfung: Fälle mit neuen Schnittstellen (übersetzen auf f3d579a88 nicht).</summary>
public class T0787_GegenpruefungNeu
{
    [Fact(DisplayName = "T-0787 G2: 20-MB-Rezeptseite mit offenen <script>-Marken und „<“-Flut in Namen – in wenigen Sekunden mit ehrlicher Meldung")]
    public void Import_20mb_boesartig()
    {
        var sb = new StringBuilder(21_000_000);
        sb.Append("<html><head>");
        while (sb.Length < 10_000_000) sb.Append("<script type=\"application/ld+json\">{\"a\":");
        var sw = Stopwatch.StartNew();
        var (got, notes) = RezeptKit.Import(sb.ToString());
        Assert.Empty(got); Assert.Contains(notes, n => n.Contains("nicht geschlossen"));
        // gültiges JSON-LD, aber Name und Schritte aus 5 MB „<“
        var flut = new string('<', 5_000_000);
        var json = new JsonObject { ["@context"] = "https://schema.org", ["@type"] = "Recipe", ["name"] = flut, ["recipeIngredient"] = new JsonArray("1 Ei", flut), ["recipeInstructions"] = flut }.ToJsonString();
        var (g2, _) = RezeptKit.Import(json);
        sw.Stop();
        Assert.Single(g2);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(20), $"brauchte {sw.Elapsed.TotalSeconds:0.0} s");
    }

    [Fact(DisplayName = "T-0787 G4/G7: Hinweis bei Mehrdeutigem und Unlesbarem (Tausenderpunkt, „1e5“, Menge außerhalb der Grenze); AusText meldet unlesbare Portionen")]
    public void Hinweise()
    {
        Assert.Contains("Tausender", RezeptKit.Lies("1.000 g Mehl").Hinweis);
        Assert.Contains("keine lesbare Menge", RezeptKit.Lies("1e5 g Mehl").Hinweis);
        Assert.Contains("außerhalb", RezeptKit.Lies("0 g Mehl").Hinweis);
        Assert.Null(RezeptKit.Lies("200 g Mehl").Hinweis);
        var h = new List<string>();
        RezeptKit.AusText("Suppe\nFür 99999999999999999999 Portionen\nZutaten\n1.000 g Kartoffeln\nZubereitung\nKochen.", h);
        Assert.Contains(h, x => x.Contains("Portionen")); Assert.Contains(h, x => x.Contains("Tausender"));
        Assert.Contains("Kurkuma", RezeptKit.AllerText(new Rezept("k", "x", 1, [RezeptKit.LiesZutat("1 TL Kurkuma, gemahlen")], [])));
    }
}
