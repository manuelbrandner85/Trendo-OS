using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0622 Teil 1 in der Oberfläche: Seiten, Sprachen mit KI-Vorschlag, Inhalte (CMS), Prüfung der Website.</summary>
public class WebDesignStufe3_Ui
{
    static void Pump() { for (var i = 0; i < 8; i++) Dispatcher.UIThread.RunJobs(); }
    const string Start = """{"root":{"props":{"title":"Start","description":"Kurz"}},"content":[{"type":"Text","props":{"id":"t","text":"Willkommen","align":"left"}}],"zones":{}}""";

    [AvaloniaFact(DisplayName = "Seiten: anlegen über die Felder, wechseln, löschen erst nach Nachfrage; Prüfung zeigt Befunde")]
    public void Pages_pane()
    {
        var (w, _, _) = Ui.Open();
        w.WebDesignMessage("{\"art\":\"speichern\",\"daten\":" + Start + "}"); Pump();
        w.Show("dev"); Pump();
        w.Find<TextBox>("web-seite-titel").Text = "Über uns";
        w.Find<Button>("web-seite-anlegen").Press(); Pump();
        Assert.Equal(["index", "ueber-uns"], WebDesignSite.Pages(w.WebDesignDir));
        Assert.Equal("ueber-uns", WebDesignSite.Active(w.WebDesignDir).Adresse);
        Assert.Contains("/ueber-uns", w.WebDesignSiteNote);
        w.Find<Button>("web-seite-oeffnen-0").Press(); Pump(); // zurück zur Startseite
        Assert.Equal("index", WebDesignSite.Active(w.WebDesignDir).Adresse);
        w.Find<Button>("web-seite-loeschen-1").Press(); Pump();
        Assert.Equal(2, WebDesignSite.Pages(w.WebDesignDir).Count); // erst nachfragen
        Assert.Contains("Noch einmal", w.WebDesignSiteNote);
        w.Find<Button>("web-seite-loeschen-1").Press(); Pump();
        Assert.Equal(["index"], WebDesignSite.Pages(w.WebDesignDir));
        w.Find<TextBox>("web-basis").Text = "http://unsicher.de";
        w.Find<Button>("web-basis-speichern").Press(); Pump();
        Assert.Contains("https://", w.WebDesignSiteNote);
        w.Find<Button>("web-pruefen").Press(); Pump();
        Assert.Contains(w.GetLogicalTexts(), t => t.Contains("Adresse der Website fehlt"));
    }

    [AvaloniaFact(DisplayName = "Z5: Sprache hinzufügen, KI-Vorschlag ändert erst nach „Übernehmen“, fehlende Übersetzung steht in der Liste")]
    public async Task Languages_pane()
    {
        MainWindow.WebDesignKiOverride = (_, prompt) => Task.FromResult(prompt.Contains("\"t/text\": \"Willkommen\"") ? """{"t/text":"Welcome","root/title":"Home"}""" : "{}");
        try
        {
            var (w, _, _) = Ui.Open();
            w.WebDesignMessage("{\"art\":\"speichern\",\"daten\":" + Start + "}"); Pump();
            w.Show("dev"); Pump();
            w.Find<TextBox>("web-sprache-code").Text = "en";
            w.Find<Button>("web-sprache-anlegen").Press(); Pump();
            Assert.Contains(w.GetLogicalTexts(), t => t.Contains("Startseite: fehlt"));
            await w.WebDesignTranslateAi("index", "en"); Pump();
            Assert.Contains("Übernehmen", w.WebDesignLangNote);
            Assert.False(File.Exists(Path.Combine(w.WebDesignDir, "sprachen", "en", "index.trendo.json"))); // Vorschlag ändert nichts
            w.Find<Button>("web-uebersetzung-ok-0-0").Press(); Pump();
            Assert.Contains("Welcome", File.ReadAllText(Path.Combine(w.WebDesignDir, "sprachen", "en", "index.trendo.json")));
            Assert.Contains("Willkommen", File.ReadAllText(Path.Combine(w.WebDesignDir, WebDesign.PageFile)));
            Assert.Equal("en", WebDesignSite.Active(w.WebDesignDir).Sprache); // bearbeitet wird jetzt die Übersetzung
            Assert.Contains(w.GetLogicalTexts(), t => t.Contains("Startseite: 1 von 3 Texten offen")); // die Beschreibung „Kurz“ hat die KI nicht übersetzt – ehrlich gezählt
        }
        finally { MainWindow.WebDesignKiOverride = null; }
    }

    [AvaloniaFact(DisplayName = "W2: Sammlung anlegen, Eintrag über das Formular, Pflichtfeld leer wird gemeldet, Vorlage-Seite mit Platzhaltern")]
    public void Cms_pane()
    {
        var (w, _, _) = Ui.Open();
        w.WebDesignMessage("{\"art\":\"speichern\",\"daten\":" + Start + "}"); Pump();
        w.Show("dev"); Pump();
        w.Find<TextBox>("web-cms-name").Text = "Rezepte";
        w.Find<TextBox>("web-cms-felder").Text = "titel:text*, datum:datum, text:langtext";
        w.Find<Button>("web-cms-anlegen").Press(); Pump();
        Assert.Equal("rezepte", WebDesignCms.Load(w.WebDesignDir).Single().Id);
        w.Find<TextBox>("web-cms-adresse").Text = "brot";
        w.Find<TextBox>("web-cms-feld-datum").Text = "2026-11-14";
        w.Find<Button>("web-cms-speichern").Press(); Pump();
        Assert.Contains(w.GetLogicalTexts(), t => t.Contains("Pflichtfeld „titel“ ist leer")); // gespeichert, aber gemeldet
        w.Find<TextBox>("web-cms-feld-titel").Text = "Bauernbrot";
        w.Find<Button>("web-cms-speichern").Press(); Pump();
        Assert.Equal("Bauernbrot", WebDesignCms.Load(w.WebDesignDir).Single().Eintraege.Single().Werte["titel"]);
        Assert.DoesNotContain(w.GetLogicalTexts(), t => t.Contains("Pflichtfeld"));
        w.Find<Button>("web-cms-vorlage-0").Press(); Pump();
        Assert.Equal("rezepte-vorlage", WebDesignCms.TemplateOf(w.WebDesignDir, "rezepte"));
        var tpl = File.ReadAllText(Path.Combine(w.WebDesignDir, "seiten", "rezepte-vorlage.trendo.json"));
        Assert.Contains("{{titel}}", tpl); Assert.Contains("{{datum}}", tpl);
        Assert.Contains(w.GetLogicalTexts(), t => t.Contains("Vorlage für „Rezepte“"));
    }
}

/// <summary>T-0622 Teil 1 im echten Browser: Export mit Vorrendern, Liste, Sprachen, Unterordner.</summary>
public class WebDesignStufe3_Browser
{
    static string Repo() => WebDesignKi_Ui.Repo();
    static string? AxeFile() { var f = Path.Combine(Path.GetTempPath(), "trendo-axe", "node_modules", "axe-core", "axe.min.js"); return File.Exists(f) ? f : null; }
    static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    /// <summary>Website mit Startseite (Liste), „Über uns“ (de/en), Sammlung mit Vorlage und drei Einträgen.</summary>
    static void Build(string d)
    {
        Directory.CreateDirectory(Path.Combine(d, "bilder"));
        foreach (var n in new[] { "brot", "zopf", "semmel" }) File.WriteAllBytes(Path.Combine(d, "bilder", n + ".png"), Png);
        Assert.Null(WebDesign.Save(d, """{"root":{"props":{"title":"Backstube","description":"Brot vom Lindenplatz"}},"content":[{"type":"Navigation","props":{"id":"nav","marke":"Backstube","links":[{"label":"Über uns","href":"ueber-uns"}],"knopf":"","knopfZiel":"#"}},{"type":"Text","props":{"id":"t","text":"Willkommen in der Backstube","align":"left"}},{"type":"Sammlung","props":{"id":"liste","titel":"Unsere Brote","sammlung":"rezepte","anzahl":10,"sortierung":"neueste"}}],"zones":{}}"""));
        Assert.Null(WebDesignCms.CreateCollection(d, "rezepte", "Rezepte", "titel:text*, beschreibung:langtext, bild:bild, datum:datum"));
        foreach (var (a, t, img, date) in new[] { ("brot", "Bauernbrot", "brot", "2026-11-01"), ("zopf", "Hefezopf", "zopf", "2026-11-12"), ("semmel", "Semmel", "semmel", "2026-10-20") })
            Assert.Null(WebDesignCms.SetEntry(d, "rezepte", a, new Dictionary<string, string> { ["titel"] = t, ["beschreibung"] = "Frisch aus dem Ofen", ["bild"] = $"bilder/{img}.png", ["datum"] = date }));
        Assert.Null(WebDesignSite.Create(d, "rezept-vorlage", "Rezept"));
        Assert.Null(WebDesign.Save(d, """{"root":{"props":{"title":"Rezept","description":"{{titel}} aus der Backstube","vorlage":"rezepte"}},"content":[{"type":"Ueberschrift","props":{"id":"h","text":"{{titel}}","level":"h2"}},{"type":"Bild","props":{"id":"b","src":"{{bild}}","alt":"{{titel}}"}},{"type":"Text","props":{"id":"x","text":"{{beschreibung}}","align":"left"}}],"zones":{}}"""));
        Assert.Null(WebDesignSite.Create(d, "ueber-uns", "Über uns"));
        Assert.Null(WebDesign.Save(d, """{"root":{"props":{"title":"Über uns","description":"Seit 1990"}},"content":[{"type":"Text","props":{"id":"t","text":"Wir backen seit 1990","align":"left"}},{"type":"Knopf","props":{"id":"k","label":"Kontakt","href":"#kontakt"}}],"zones":{}}"""));
        Assert.Null(WebDesignSite.AddLanguage(d, "en"));
        Assert.Null(WebDesignSite.Activate(d, "index", "en"));
        var (en, _) = WebDesignI18n.Apply(JsonNode.Parse(WebDesign.Load(d))!.AsObject(), new Dictionary<string, string> { ["root/title"] = "Bakery", ["t/text"] = "Welcome to the bakery", ["liste/titel"] = "Our breads", ["nav/marke"] = "Bakery", ["nav/links/0/label"] = "About us" });
        Assert.Null(WebDesign.Save(d, en.ToJsonString()));
        Assert.Null(WebDesignSite.Activate(d, "ueber-uns", "en"));
        var (about, _) = WebDesignI18n.Apply(JsonNode.Parse(WebDesign.Load(d))!.AsObject(), new Dictionary<string, string> { ["root/title"] = "About us", ["root/description"] = "Since 1990", ["t/text"] = "Baking since 1990", ["k/label"] = "Contact" });
        Assert.Null(WebDesign.Save(d, about.ToJsonString()));
        Assert.Null(WebDesignSite.SetTransition(d, "einblenden"));
        Assert.Null(WebDesignSite.SetBase(d, "https://backstube.example"));
    }

    [AvaloniaFact(DisplayName = "W3, W2, Z5, W4 in echtem Chrome: Inhalt vorgerendert im HTML, Export ohne Befund, Liste, Links je Sprache, Bilder im Unterordner, Übergang (übersprungen ohne Chrome/Node)")]
    public async Task Real_browser_stufe3()
    {
        if (WebsitePreview.FindBrowser() is not { } chrome || Cli.Find("node") is not { } node) { Assert.Skip("Übersprungen – Chrome/Edge oder Node.js fehlt auf diesem PC."); return; }
        MainWindow.WebDesignEditorDirOverride = Path.Combine(Repo(), "src", "Trendo.App", "Assets", "webdesign");
        try
        {
            var (w, _, _) = Ui.Open();
            Build(w.WebDesignDir);
            await w.WebDesignExportSiteAsync();
            var outDir = Path.Combine(w.WebDesignDir, WebDesign.ExportDir);
            Assert.DoesNotContain("<div id=\"root\"></div>", File.ReadAllText(Path.Combine(outDir, "index.html"))); // vorgerendert
            Assert.DoesNotContain("<div id=\"root\"></div>", File.ReadAllText(Path.Combine(outDir, "en", "rezepte", "brot.html")));
            Assert.DoesNotContain("Inhalt steht nicht im HTML", w.WebDesignNote);
            Assert.DoesNotContain("fehlt", w.WebDesignNote);
            var run = Cli.Run(node, [Path.Combine(Repo(), "tools", "webdesign-editor", "pruefe-stufe3.mjs"), chrome, outDir, AxeFile() ?? ""], timeoutMs: 180_000);
            Assert.True(run.Ok, run.Out + run.Err);
        }
        finally { MainWindow.WebDesignEditorDirOverride = null; }
    }
}
