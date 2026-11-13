using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0621 Teil 1 in der Oberfläche: Gestaltungsfeld mit Kontrast beim Wählen (W6), Ansichten anlegen und entfernen (W1, Z4).</summary>
public class WebDesignStufe2_Ui
{
    static void Pump() { for (var i = 0; i < 8; i++) Dispatcher.UIThread.RunJobs(); }
    const string Page = """{"root":{"props":{"title":"Seite","description":"Kurz"}},"content":[{"type":"Text","props":{"id":"t1","text":"Absatz","align":"left"}}],"zones":{}}""";

    [AvaloniaFact(DisplayName = "W6: Kontrast wird schon beim Wählen gemeldet; Übernehmen gilt für Seite und Variante")]
    public void Tokens_pane()
    {
        var (w, _, _) = Ui.Open();
        w.WebDesignMessage("{\"art\":\"speichern\",\"daten\":" + Page + "}"); Pump();
        w.WebDesignVariantCreate("B"); Pump();
        w.Show("dev"); Pump();
        Assert.StartsWith("✓", w.Find<TextBlock>("web-tok-check").Text);
        w.Find<TextBox>("web-tok-ink").Text = "#eeeeee"; Pump();
        Assert.Contains("Text auf dem Hintergrund ist zu schwach", w.Find<TextBlock>("web-tok-check").Text); // ohne Speichern
        w.Find<TextBox>("web-tok-paper").Text = "kein"; Pump();
        Assert.Contains("Hintergrund: bitte als #RRGGBB", w.Find<TextBlock>("web-tok-check").Text);
        w.Find<Button>("web-tok-apply").Press(); Pump();
        Assert.Contains("ungültig", w.WebDesignNote); Assert.Null(WebDesignTokens.Load(w.WebDesignDir)); // ungültig: nichts übernommen
        w.Find<TextBox>("web-tok-paper").Text = "#ffffff"; w.Find<TextBox>("web-tok-ink").Text = "#123456"; w.Find<TextBox>("web-tok-accent").Text = "#7A1F2B";
        w.Find<CheckBox>("web-tok-dunkel").IsChecked = true; w.Find<TextBox>("web-tok-inkDunkel").Text = "#202020"; Pump();
        Assert.Contains("(dunkel)", w.Find<TextBlock>("web-tok-check").Text); // dunkler Satz wird mitgeprüft
        w.Find<TextBox>("web-tok-inkDunkel").Text = "#f2f2f2"; Pump();
        Assert.StartsWith("✓", w.Find<TextBlock>("web-tok-check").Text);
        w.Find<Button>("web-tok-apply").Press(); Pump();
        Assert.Equal("#7A1F2B", WebDesignTokens.Load(w.WebDesignDir)!["accent"]!.ToString());
        var variant = WebDesignStates.Variants(w.WebDesignDir).Single().Id;
        var props = JsonNode.Parse(WebDesignStates.Read(w.WebDesignDir, variant))!["root"]!["props"]!;
        Assert.Equal(("#7A1F2B", "#123456", true), (props["accent"]!.ToString(), props["ink"]!.ToString(), props["dunkel"]!.GetValue<bool>()));
    }

    [AvaloniaFact(DisplayName = "W1/Z4: Ansicht mit eigener Breite anlegen, Breite ändern und entfernen, ungültige Breite wird gemeldet")]
    public void Views_pane()
    {
        var (w, _, _) = Ui.Open();
        w.WebDesignMessage("{\"art\":\"speichern\",\"daten\":" + Page + "}"); Pump();
        w.Show("dev"); Pump();
        Assert.Contains(w.GetLogicalTexts(), t => t == "Tablet · bis 1024 px");
        w.Find<TextBox>("web-ansicht-name").Text = "Großes Handy"; w.Find<TextBox>("web-ansicht-breite").Text = "480";
        w.Find<Button>("web-ansicht-setzen").Press(); Pump();
        Assert.Contains(w.GetLogicalTexts(), t => t == "Großes Handy · bis 480 px");
        w.Find<TextBox>("web-ansicht-name").Text = "Großes Handy"; w.Find<TextBox>("web-ansicht-breite").Text = "5000";
        w.Find<Button>("web-ansicht-setzen").Press(); Pump();
        Assert.Contains("zwischen", w.WebDesignNote);
        w.Find<Button>("web-ansicht-weg-0").Press(); Pump(); // Tablet ist die breiteste
        var views = WebDesignViews.Of(JsonNode.Parse(WebDesign.Load(w.WebDesignDir))!["root"]!["props"]!.AsObject());
        Assert.Equal(["Handy", "Großes Handy"], views.Select(v => v.Name));
    }
}

/// <summary>T-0621 Teil 1 im echten Browser und mit Node: Editor-Bündel und Export, gleiche Regeln in Editor und Export.</summary>
public class WebDesignStufe2_Browser
{
    static string Repo() => WebDesignKi_Ui.Repo();
    static string PagePlan() => File.ReadAllText(Path.Combine(Repo(), "tests", "Trendo.App.Tests", "Daten", "web-stufe2-teil1.json"));

    [Fact(DisplayName = "Z4/W1: Editor (ansichten.js) und Export (C#) erzeugen dieselben Regeln (übersprungen ohne Node)")]
    public void Same_rules_in_editor_and_export()
    {
        if (Cli.Find("node") is not { } node) { Assert.Skip("Übersprungen – Node.js fehlt."); return; }
        var j = WebDesignActions.Apply(WebDesign.Empty(), PagePlan());
        Assert.Null(j.Error);
        var extra = WebDesignActions.Apply(j.Json!, """{"aktionen":[{"aktion":"ansicht_setzen","name":"Klein","breite":360},{"aktion":"anpassung_setzen","id":"aussen","ansicht":"Klein","werte":{"spalten":1,"schrift":90,"ausblenden":false}}]}""");
        var file = Path.Combine(Path.GetTempPath(), "trendo-web2-" + Guid.NewGuid().ToString("N") + ".json"); File.WriteAllText(file, extra.Json!);
        try
        {
            var module = new Uri(Path.Combine(Repo(), "tools", "webdesign-editor", "src", "ansichten.js")).AbsoluteUri;
            var r = Cli.Run(node, ["--input-type=module", "-e", $"import {{ anpassungenCss }} from '{module}'; import {{ readFileSync }} from 'node:fs'; const d = JSON.parse(readFileSync(process.argv[1], 'utf8')); process.stdout.write(anpassungenCss(d, 'media') + '\\u0001' + anpassungenCss(d, 'container'));", file], timeoutMs: 30_000);
            Assert.True(r.Ok, r.Err);
            var doc = JsonNode.Parse(extra.Json!)!.AsObject();
            Assert.Equal(WebDesignViews.Css(doc) + "\u0001" + WebDesignViews.Css(doc, container: true), r.Out);
            Assert.Contains("@media (max-width: 360px)", r.Out);
        }
        finally { File.Delete(file); }
    }

    [Fact(DisplayName = "W1, Z4, W6, A5 in echtem Chrome: Ansichten wirken nach unten, Spalten nach eigener Breite, dunkler Satz, Stapel im Rahmen – im Editor und im Export (übersprungen ohne Chrome oder Node)")]
    public void Real_browser()
    {
        if (WebsitePreview.FindBrowser() is not { } chrome || Cli.Find("node") is not { } node) { Assert.Skip("Übersprungen – Chrome/Edge oder Node.js fehlt auf diesem PC."); return; }
        var editor = Path.Combine(Repo(), "src", "Trendo.App", "Assets", "webdesign");
        var d = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "trendo-web2-real-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            var r = WebDesignActions.Apply(WebDesign.Empty(), PagePlan()); Assert.Null(r.Error);
            Assert.Null(WebDesignStates.Write(d, r.Json!)); // wie in der App: Gestaltungswerte gehen ins Projekt
            var (outDir, err) = WebDesign.Export(d, editor); Assert.Null(err);
            var page = Path.Combine(d, "seite.json"); File.WriteAllText(page, WebDesignStates.Read(d));
            var run = Cli.Run(node, [Path.Combine(Repo(), "tools", "webdesign-editor", "pruefe-gestaltung.mjs"), chrome, editor, page, outDir!], timeoutMs: 120_000);
            Assert.True(run.Ok, run.Out + run.Err);
            Assert.Contains("\"problems\":[]", run.Out);
        }
        finally { try { Directory.Delete(d, true); } catch (IOException) { } }
    }
}
