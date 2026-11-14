using System.Text.Json.Nodes;
using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0622 Teil 1: mehrere Seiten, W3 (SEO je Seite, Sitemap, robots, Weiterleitungen), W2 (CMS), Z5 (Mehrsprachigkeit), W4 (Übergang zwischen Seiten).</summary>
public class WebDesignStufe3Tests
{
    static string Dir() => Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "trendo-web3-" + Guid.NewGuid().ToString("N"))).FullName;
    static string Repo()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent) if (File.Exists(Path.Combine(d.FullName, "Trendo.sln"))) return d.FullName;
        throw new DirectoryNotFoundException();
    }
    static string Editor() => Path.Combine(Repo(), "src", "Trendo.App", "Assets", "webdesign");
    static string PageWith(string title, string text, string description = "Kurz") =>
        $$$"""{"root":{"props":{"title":"{{{title}}}","description":"{{{description}}}"}},"content":[{"type":"Ueberschrift","props":{"id":"h","text":"{{{title}}}","level":"h2"}},{"type":"Text","props":{"id":"t","text":"{{{text}}}","align":"left"}},{"type":"Knopf","props":{"id":"k","label":"Mehr","href":"ueber-uns"}}],"zones":{}}""";
    static JsonObject Doc(string json) => JsonNode.Parse(json)!.AsObject();
    /// <summary>Seitendaten aus einer Export-Datei (window.TRENDO_SEITE = …;), als lesbarer Text.</summary>
    static string Data(string file) => JsonNode.Parse(File.ReadAllText(file)["window.TRENDO_SEITE = ".Length..].TrimEnd().TrimEnd(';'))!.ToJsonString(new System.Text.Json.JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    static void Clean(string d) { try { Directory.Delete(d, true); } catch (IOException) { } }

    // ---------- Mehrere Seiten ----------

    [Fact(DisplayName = "Seiten: anlegen, wählen, speichern; Editor, Stände und Varianten arbeiten auf der aktiven Seite")]
    public void Pages_active_page()
    {
        var d = Dir(); try
        {
            Assert.Null(WebDesign.Save(d, PageWith("Start", "Willkommen")));
            Assert.Null(WebDesignSite.Create(d, "ueber-uns", "Über uns"));
            Assert.Equal(["index", "ueber-uns"], WebDesignSite.Pages(d));
            Assert.Equal("ueber-uns", WebDesignSite.Active(d).Adresse); // neue Seite ist gleich aktiv
            Assert.Null(WebDesign.Save(d, PageWith("Über uns", "Wir backen seit 1990")));
            Assert.True(File.Exists(Path.Combine(d, "seiten", "ueber-uns.trendo.json")));
            Assert.Contains("Willkommen", File.ReadAllText(Path.Combine(d, WebDesign.PageFile))); // Startseite unberührt
            var (stand, _) = WebDesignStates.Save(d, "Über uns v1");
            Assert.Equal("ueber-uns", stand!.Seite);
            Assert.Null(WebDesign.Save(d, PageWith("Über uns", "Geändert")));
            Assert.Null(WebDesignSite.Activate(d, "index"));
            Assert.Null(WebDesignStates.Restore(d, stand.Id)); // springt auf die Seite des Stands zurück
            Assert.Equal("ueber-uns", WebDesignSite.Active(d).Adresse);
            Assert.Contains("Wir backen seit 1990", WebDesign.Load(d));
            Assert.Contains("Willkommen", File.ReadAllText(Path.Combine(d, WebDesign.PageFile)));
            var (v, _) = WebDesignStates.CreateVariant(d, "B");
            Assert.Equal("ueber-uns", v!.Seite);
            Assert.Null(WebDesignSite.Activate(d, "index"));
            Assert.Null(WebDesignStates.AdoptVariant(d, v.Id)); // Variante landet auf ihrer Seite, nicht auf der Startseite
            Assert.Contains("Willkommen", File.ReadAllText(Path.Combine(d, WebDesign.PageFile)));
        }
        finally { Clean(d); }
    }

    [Theory(DisplayName = "Seiten: ungültige oder reservierte Adressen werden abgelehnt")]
    [InlineData("Über uns")] [InlineData("ueber_uns")] [InlineData("assets")] [InlineData("en")] [InlineData("-x")] [InlineData("")]
    public void Pages_bad_slug(string slug) => Assert.NotNull(WebDesignSite.CheckSlug(slug));

    [Fact(DisplayName = "Seiten: doppelte Adresse, Startseite löschen und Weiterleitung über eine Seite werden abgelehnt")]
    public void Pages_duplicates()
    {
        var d = Dir(); try
        {
            WebDesign.Save(d, PageWith("Start", "x"));
            Assert.Null(WebDesignSite.Create(d, "kontakt", "Kontakt"));
            Assert.Contains("gibt es schon", WebDesignSite.Create(d, "kontakt", "Noch mal"));
            Assert.NotNull(WebDesignSite.Delete(d, "index"));
            Assert.Contains("bestehende Seite", WebDesignSite.AddRedirect(d, "/kontakt", "index"));
            Assert.Null(WebDesignCms.CreateCollection(d, "rezepte", "Rezepte", "titel:text*"));
            Assert.Contains("Sammlung", WebDesignSite.Create(d, "rezepte", "Rezepte"));
            Assert.Null(WebDesignSite.Delete(d, "kontakt"));
            Assert.Equal(["index"], WebDesignSite.Pages(d));
            Assert.Single(Directory.GetDirectories(Path.Combine(d, ".trendo-papierkorb"))); // gelöscht heißt: im Papierkorb des Projekts
        }
        finally { Clean(d); }
    }

    // ---------- W3 ----------

    [Fact(DisplayName = "W3: Export mit Titel, Beschreibung, kanonischer Adresse, Vorschaubild je Seite; Sitemap listet alle Seiten, robots.txt verweist darauf")]
    public void W3_export_seo()
    {
        var d = Dir(); try
        {
            WebDesign.Save(d, PageWith("Bäckerei Muster", "Willkommen"));
            Directory.CreateDirectory(Path.Combine(d, "bilder")); File.WriteAllBytes(Path.Combine(d, "bilder", "teilen.png"), [137, 80, 78, 71]);
            WebDesignSite.Create(d, "ueber-uns", "Über uns");
            var doc = Doc(PageWith("Über uns", "Seit 1990")); doc["root"]!["props"]!["vorschaubild"] = "bilder/teilen.png";
            WebDesign.Save(d, doc.ToJsonString());
            WebDesignSite.Create(d, "intern", "Intern");
            var hidden = Doc(PageWith("Intern", "nur für uns")); hidden["root"]!["props"]!["indexieren"] = false;
            WebDesign.Save(d, hidden.ToJsonString());
            Assert.Null(WebDesignSite.SetBase(d, "https://www.baeckerei-muster.de/"));
            var r = WebDesignExport.ExportSite(d, Editor());
            Assert.Null(r.Error);
            Assert.Equal(["index.html", "ueber-uns.html", "intern.html"], r.Pages.Select(p => p.RelPath));
            var about = File.ReadAllText(Path.Combine(r.Dir!, "ueber-uns.html"));
            Assert.Contains("<title>Über uns</title>", about);
            Assert.Contains("<meta name=\"description\" content=\"Kurz\" />", about);
            Assert.Contains("<link rel=\"canonical\" href=\"https://www.baeckerei-muster.de/ueber-uns.html\" />", about);
            Assert.Contains("og:image\" content=\"https://www.baeckerei-muster.de/bilder/teilen.png\"", about);
            Assert.True(File.Exists(Path.Combine(r.Dir!, "bilder", "teilen.png")));
            Assert.Contains("noindex", File.ReadAllText(Path.Combine(r.Dir!, "intern.html")));
            var sitemap = File.ReadAllText(Path.Combine(r.Dir!, "sitemap.xml"));
            Assert.Contains("<loc>https://www.baeckerei-muster.de/</loc>", sitemap);
            Assert.Contains("<loc>https://www.baeckerei-muster.de/ueber-uns.html</loc>", sitemap);
            Assert.DoesNotContain("intern.html", sitemap); // nicht indexieren heißt auch: nicht in die Sitemap
            Assert.Contains("Sitemap: https://www.baeckerei-muster.de/sitemap.xml", File.ReadAllText(Path.Combine(r.Dir!, "robots.txt")));
            Assert.Contains("\"href\":\"ueber-uns.html\"", File.ReadAllText(Path.Combine(r.Dir!, "seite.js"))); // interner Link zeigt auf die Datei
            Assert.DoesNotContain(WebDesignExport.CheckExport(r.Dir!, r.Pages), x => !x.Contains("nicht im HTML")); // nur das Vorrendern fehlt hier (braucht den Browser der App)
        }
        finally { Clean(d); }
    }

    [Fact(DisplayName = "W3: doppelte Adresse wird gemeldet (Seite gegen Sammlung, Weiterleitung auf unbekannte Seite)")]
    public void W3_duplicate_address_reported()
    {
        var d = Dir(); try
        {
            WebDesign.Save(d, PageWith("Start", "x"));
            Assert.Null(WebDesignCms.CreateCollection(d, "rezepte", "Rezepte", "titel:text*"));
            Directory.CreateDirectory(Path.Combine(d, "seiten")); File.WriteAllText(Path.Combine(d, "seiten", "rezepte.trendo.json"), PageWith("Rezepte", "von Hand angelegt")); // an Trendo vorbei
            Assert.Null(WebDesignSite.AddRedirect(d, "/alt", "gibt-es-nicht"));
            var f = WebDesignSeo.Check(d);
            Assert.Contains(f, x => x.Text.Contains("„rezepte“") && x.Text.Contains("doppelt") && x.Ebene == "Fehler");
            Assert.Contains(f, x => x.Text.Contains("/alt") && x.Ebene == "Fehler");
        }
        finally { Clean(d); }
    }

    [Fact(DisplayName = "W3: Weiterleitungen als Seite mit Sprungziel und als _redirects; ohne Website-Adresse gibt es einen Hinweis statt einer Sitemap")]
    public void W3_redirects()
    {
        var d = Dir(); try
        {
            WebDesign.Save(d, PageWith("Start", "x"));
            WebDesignSite.Create(d, "kontakt", "Kontakt"); WebDesign.Save(d, PageWith("Kontakt", "Ruf an"));
            Assert.Null(WebDesignSite.AddRedirect(d, "/alt-kontakt.html", "kontakt"));
            Assert.NotNull(WebDesignSite.AddRedirect(d, "/x", "javascript:alert(1)"));
            var r = WebDesignExport.ExportSite(d, Editor());
            Assert.Contains("url=kontakt.html", File.ReadAllText(Path.Combine(r.Dir!, "alt-kontakt.html")));
            Assert.Contains("/alt-kontakt /kontakt 301", File.ReadAllText(Path.Combine(r.Dir!, "_redirects")));
            Assert.False(File.Exists(Path.Combine(r.Dir!, "sitemap.xml")));
            Assert.Contains(r.Notes, n => n.Contains("Ohne Adresse der Website"));
        }
        finally { Clean(d); }
    }

    // ---------- W4 ----------

    [Fact(DisplayName = "W4: Übergang zwischen Seiten nur bei erlaubter Bewegung; ohne Unterstützung wechselt die Seite einfach")]
    public void W4_page_transition()
    {
        var css = WebDesignExport.TransitionCss("schieben");
        Assert.StartsWith("\n@media (prefers-reduced-motion: no-preference) { @view-transition { navigation: auto; }", css);
        Assert.Equal("", WebDesignExport.TransitionCss("keiner"));
        var d = Dir(); try
        {
            WebDesign.Save(d, PageWith("Start", "x"));
            Assert.Null(WebDesignSite.SetTransition(d, "einblenden"));
            var r = WebDesignExport.ExportSite(d, Editor());
            Assert.Contains("@view-transition { navigation: auto; }", File.ReadAllText(Path.Combine(r.Dir!, "index.html")));
        }
        finally { Clean(d); }
    }

    // ---------- W2 ----------

    static string Template() =>
        """{"root":{"props":{"title":"Rezept","description":"{{titel}} aus unserer Backstube","vorlage":"rezepte"}},"content":[{"type":"Ueberschrift","props":{"id":"h","text":"{{titel}}","level":"h2"}},{"type":"Text","props":{"id":"t","text":"{{beschreibung}} – von {{autor}} am {{datum}}","align":"left"}},{"type":"Bild","props":{"id":"b","src":"{{bild}}","alt":"{{titel}}"}}],"zones":{}}""";

    static void FillCms(string d)
    {
        Directory.CreateDirectory(Path.Combine(d, "bilder")); foreach (var n in new[] { "brot", "zopf", "semmel" }) File.WriteAllBytes(Path.Combine(d, "bilder", n + ".png"), [137, 80, 78, 71]);
        Assert.Null(WebDesignCms.CreateCollection(d, "autoren", "Autoren", "name:text*"));
        Assert.Null(WebDesignCms.CreateCollection(d, "rezepte", "Rezepte", "titel:text*, beschreibung:langtext, bild:bild, datum:datum, autor:verweis>autoren"));
        Assert.Null(WebDesignCms.SetEntry(d, "autoren", "anna", new Dictionary<string, string> { ["name"] = "Anna" }));
        Assert.Null(WebDesignCms.SetEntry(d, "rezepte", "brot", new Dictionary<string, string> { ["titel"] = "Bauernbrot", ["beschreibung"] = "Kräftig", ["bild"] = "bilder/brot.png", ["datum"] = "2026-11-01", ["autor"] = "anna" }));
        Assert.Null(WebDesignCms.SetEntry(d, "rezepte", "zopf", new Dictionary<string, string> { ["titel"] = "Hefezopf", ["beschreibung"] = "Süß", ["bild"] = "bilder/zopf.png", ["datum"] = "2026-11-12", ["autor"] = "anna" }));
        Assert.Null(WebDesignCms.SetEntry(d, "rezepte", "semmel", new Dictionary<string, string> { ["titel"] = "Semmel", ["beschreibung"] = "Knusprig", ["bild"] = "bilder/semmel.png", ["datum"] = "2026-10-20", ["autor"] = "anna" }));
    }

    [Fact(DisplayName = "W2: 3 Einträge ergeben 3 Seiten aus der Vorlage und eine Liste; Platzhalter, Datum und Verweis werden eingesetzt")]
    public void W2_three_entries_three_pages()
    {
        var d = Dir(); try
        {
            var start = Doc(PageWith("Start", "Unsere Rezepte"));
            start["content"]!.AsArray().Add(new JsonObject { ["type"] = "Sammlung", ["props"] = new JsonObject { ["id"] = "liste", ["titel"] = "Rezepte", ["sammlung"] = "rezepte", ["anzahl"] = 10, ["sortierung"] = "neueste" } });
            WebDesign.Save(d, start.ToJsonString());
            FillCms(d);
            WebDesignSite.Create(d, "rezept-vorlage", "Rezept"); WebDesign.Save(d, Template());
            Assert.Equal("rezept-vorlage", WebDesignCms.TemplateOf(d, "rezepte"));
            var r = WebDesignExport.ExportSite(d, Editor());
            Assert.Null(r.Error);
            Assert.Equal(3, r.Pages.Count(p => p.RelPath.StartsWith("rezepte/", StringComparison.Ordinal)));
            Assert.DoesNotContain(r.Pages, p => p.RelPath == "rezept-vorlage.html"); // die Vorlage selbst ist keine Seite
            var brotData = Data(Path.Combine(r.Dir!, "daten", "rezepte", "brot.js"));
            Assert.Contains("Bauernbrot", brotData); Assert.Contains("Kräftig – von Anna am 01.11.2026", brotData); Assert.Contains("bilder/brot.png", brotData);
            Assert.Contains("<base href=\"../\" />", File.ReadAllText(Path.Combine(r.Dir!, "rezepte", "brot.html")));
            Assert.Contains("<title>Bauernbrot – Rezept</title>", File.ReadAllText(Path.Combine(r.Dir!, "rezepte", "brot.html")));
            var list = Doc(Data(Path.Combine(r.Dir!, "seite.js")))["content"]![3]!["props"]!["eintraege"]!.AsArray();
            Assert.Equal(["Hefezopf", "Bauernbrot", "Semmel"], list.Select(x => x!["titel"]!.ToString())); // „neueste“ zuerst
            Assert.Equal("rezepte/zopf.html", list[0]!["href"]!.ToString());
            Assert.True(File.Exists(Path.Combine(r.Dir!, "bilder", "zopf.png")));
            Assert.DoesNotContain("eintraege", File.ReadAllText(Path.Combine(d, WebDesign.PageFile))); // Liste wird nicht gespeichert, nur angezeigt
            Assert.Null(WebDesignSite.Activate(d, "index"));
            Assert.Contains("Hefezopf", WebDesignStates.Read(d)); // Editor zeigt die Liste
        }
        finally { Clean(d); }
    }

    [Fact(DisplayName = "W2: leeres Pflichtfeld und Verweis ins Leere werden gemeldet; ungültige Werte werden abgelehnt")]
    public void W2_required_field_reported()
    {
        var d = Dir(); try
        {
            WebDesign.Save(d, PageWith("Start", "x"));
            Assert.Null(WebDesignCms.CreateCollection(d, "autoren", "Autoren", "name:text*"));
            Assert.Null(WebDesignCms.CreateCollection(d, "rezepte", "Rezepte", "titel:text*, datum:datum, autor:verweis>autoren"));
            Assert.Null(WebDesignCms.SetEntry(d, "rezepte", "ohne-titel", new Dictionary<string, string> { ["autor"] = "niemand" }));
            var f = WebDesignCms.Check(d);
            Assert.Contains(f, x => x.Text.Contains("Pflichtfeld „titel“ ist leer") && x.Ebene == "Fehler");
            Assert.Contains(f, x => x.Text.Contains("„autor“ verweist auf einen Eintrag, den es nicht gibt"));
            Assert.Contains("JJJJ-MM-TT", WebDesignCms.SetEntry(d, "rezepte", "x", new Dictionary<string, string> { ["titel"] = "X", ["datum"] = "1.11.2026" }));
            Assert.NotNull(WebDesignCms.ParseFields("titel:farbe").Error);
            Assert.NotNull(WebDesignCms.ParseFields("autor:verweis").Error); // Verweis ohne Ziel
            Assert.Contains("gibt es nicht", WebDesignCms.CreateCollection(d, "news", "News", "titel:text, x:verweis>fehlt"));
        }
        finally { Clean(d); }
    }

    // ---------- Z5 ----------

    [Fact(DisplayName = "Z5: Zwei Sprachen ergeben zwei Seiten mit lang, eigener Adresse und gegenseitigen hreflang-Verweisen")]
    public void Z5_two_languages()
    {
        var d = Dir(); try
        {
            WebDesign.Save(d, PageWith("Start", "Willkommen"));
            WebDesignSite.Create(d, "ueber-uns", "Über uns"); WebDesign.Save(d, PageWith("Über uns", "Seit 1990"));
            Assert.Null(WebDesignSite.SetBase(d, "https://beispiel.de"));
            Assert.Null(WebDesignSite.AddLanguage(d, "en"));
            Assert.Null(WebDesignSite.Activate(d, "ueber-uns", "en")); // Übersetzung entsteht als Kopie
            var (doc, n) = WebDesignI18n.Apply(Doc(WebDesign.Load(d)), new Dictionary<string, string> { ["root/title"] = "About us", ["h/text"] = "About us", ["t/text"] = "Since 1990" });
            Assert.Equal(3, n);
            Assert.Null(WebDesign.Save(d, doc.ToJsonString()));
            var r = WebDesignExport.ExportSite(d, Editor());
            var en = File.ReadAllText(Path.Combine(r.Dir!, "en", "ueber-uns.html"));
            Assert.Contains("<html lang=\"en\">", en);
            Assert.Contains("<link rel=\"alternate\" hreflang=\"de\" href=\"https://beispiel.de/ueber-uns.html\" />", en);
            Assert.Contains("<link rel=\"alternate\" hreflang=\"en\" href=\"https://beispiel.de/en/ueber-uns.html\" />", en);
            Assert.Contains("<link rel=\"canonical\" href=\"https://beispiel.de/en/ueber-uns.html\" />", en);
            var de = File.ReadAllText(Path.Combine(r.Dir!, "ueber-uns.html"));
            Assert.Contains("<html lang=\"de\">", de);
            Assert.Contains("hreflang=\"en\" href=\"https://beispiel.de/en/ueber-uns.html\"", de);
            Assert.Contains("Since 1990", File.ReadAllText(Path.Combine(r.Dir!, "daten", "en", "ueber-uns.js")));
            Assert.Contains("\"href\":\"en/ueber-uns.html\"", File.ReadAllText(Path.Combine(r.Dir!, "daten", "en", "index.js"))); // Links bleiben in der Sprache
            Assert.Contains("https://beispiel.de/en/ueber-uns.html", File.ReadAllText(Path.Combine(r.Dir!, "sitemap.xml")));
        }
        finally { Clean(d); }
    }

    [Fact(DisplayName = "Z5: fehlende Übersetzung wird gemeldet; der Export zeigt dort das Original ohne falsche Sprachverweise")]
    public void Z5_missing_translation_reported()
    {
        var d = Dir(); try
        {
            WebDesign.Save(d, PageWith("Start", "Willkommen"));
            WebDesignSite.AddLanguage(d, "fr");
            Assert.Contains(WebDesignI18n.Report(d), s => s.Sprache == "fr" && s.Adresse == "index" && s.Fehlt);
            WebDesignSite.Activate(d, "index", "fr"); // nur Kopie, nichts übersetzt
            var st = WebDesignI18n.Report(d).Single();
            Assert.False(st.Fehlt); Assert.Equal(st.Gesamt, st.Offen);
            Assert.Contains(WebDesignSeo.Check(d), x => x.Text.Contains("Französisch") && x.Text.Contains("nicht übersetzt"));
            var r = WebDesignExport.ExportSite(d, Editor());
            Assert.Contains(r.Notes, n => n.Contains("nicht übersetzt"));
            var fr = File.ReadAllText(Path.Combine(r.Dir!, "fr", "index.html"));
            Assert.Contains("<html lang=\"de\">", fr); // ehrlich: der Inhalt ist deutsch
            Assert.DoesNotContain("hreflang", fr);
        }
        finally { Clean(d); }
    }

    [Fact(DisplayName = "Z5: KI-Übersetzung ändert erst nach Übernahme; Antwort mit fremden Schlüsseln oder ohne JSON wird abgelehnt")]
    public void Z5_proposal_only_after_adopt()
    {
        var d = Dir(); try
        {
            WebDesign.Save(d, PageWith("Start", "Willkommen"));
            WebDesignSite.AddLanguage(d, "en");
            var texts = WebDesignI18n.Texts(Doc(WebDesign.Load(d)));
            Assert.Contains(texts, t => t.Key == "t/text" && t.Text == "Willkommen");
            Assert.Contains("\"t/text\": \"Willkommen\"", WebDesignI18n.Prompt(texts, "de", "en"));
            Assert.NotNull(WebDesignI18n.ParseAnswer("Gern geschehen!", texts).Error);
            Assert.NotNull(WebDesignI18n.ParseAnswer("""{"fremd/text":"x"}""", texts).Error);
            var (map, err) = WebDesignI18n.ParseAnswer("Hier: {\"t/text\":\"Welcome\",\"root/title\":\"Home\",\"boese/href\":\"javascript:x\"}", texts);
            Assert.Null(err); Assert.Equal(2, map!.Count);
            Assert.Null(WebDesignI18n.SaveProposal(d, "index", "en", map));
            Assert.False(File.Exists(Path.Combine(d, "sprachen", "en", "index.trendo.json"))); // Vorschlag ändert nichts
            Assert.Null(WebDesignI18n.AdoptProposal(d, "index", "en"));
            Assert.Contains("Welcome", File.ReadAllText(Path.Combine(d, "sprachen", "en", "index.trendo.json")));
            Assert.Contains("Willkommen", File.ReadAllText(Path.Combine(d, WebDesign.PageFile)));
            Assert.Null(WebDesignI18n.Proposal(d, "index", "en")); // erledigt
            Assert.Contains(WebDesignStates.List(d), s => s.Name.StartsWith("Vor Übersetzung", StringComparison.Ordinal));
        }
        finally { Clean(d); }
    }

    [Fact(DisplayName = "Z5: CMS-Einträge je Sprache; fehlende Übersetzung im CMS wird gemeldet")]
    public void Z5_cms_languages()
    {
        var d = Dir(); try
        {
            WebDesign.Save(d, PageWith("Start", "x"));
            WebDesignSite.AddLanguage(d, "en");
            FillCms(d);
            WebDesignSite.Create(d, "rezept-vorlage", "Rezept"); WebDesign.Save(d, Template());
            Assert.Null(WebDesignCms.SetEntry(d, "rezepte", "brot", new Dictionary<string, string> { ["titel"] = "Farmhouse bread", ["beschreibung"] = "Hearty" }, "en"));
            var f = WebDesignCms.Check(d);
            Assert.Contains(f, x => x.Text.Contains("zopf") && x.Text.Contains("Übersetzung (en) fehlt"));
            Assert.DoesNotContain(f, x => x.Text.Contains("› brot:") && x.Text.Contains("Übersetzung"));
            var r = WebDesignExport.ExportSite(d, Editor());
            Assert.Contains("Farmhouse bread", Data(Path.Combine(r.Dir!, "daten", "en", "rezepte", "brot.js")));
            Assert.Contains("<base href=\"../../\" />", File.ReadAllText(Path.Combine(r.Dir!, "en", "rezepte", "brot.html")));
        }
        finally { Clean(d); }
    }
}
