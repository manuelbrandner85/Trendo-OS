using System.IO.Compression;
using System.Text.RegularExpressions;
using Trendo.Core;
using Trendo.Core.PromptAction;
using Trendo.Core.Sys;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0765 (X-03 „Apps zusammenklicken“ als echter App-Baukasten): mehrere Seiten mit Navigation, Datensammlungen mit Formular, Liste und
/// Auswertung, Speichern im Browser über ein eingebettetes Skript (ohne Netz, ohne fremde Skripte, ohne eval), Rundreise über die exportierte ZIP,
/// alte seite.json bleibt lesbar, KI-Weg „seite.schreiben“ baut Seiten und Daten und lässt vorhandene Seiten stehen.</summary>
public class T0765_AppBaukastenTests
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-t0765-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }

    [Fact(DisplayName = "T-0765 Vorlage Einkaufsliste: zwei verlinkte Seiten, Sammlung mit Pflichtfeld, Liste, Anzahl, Summe – Fassung 2 hin und zurück")]
    public void Vorlage_einkaufsliste()
    {
        var p = SitePage.Template("einkaufsliste")!;
        Assert.True(p.IsApp);
        Assert.Equal(["liste", "neu"], p.Pages.Select(s => s.Id));
        var c = Assert.Single(p.Collections);
        Assert.Equal("einkaeufe", c.Id);
        Assert.Equal(["artikel", "menge", "preis"], c.Fields.Select(f => f.Id));
        Assert.True(c.Fields[0].Required); Assert.Equal("zahl", c.Fields[2].Type);
        Assert.Equal(12, p.AppId.Length);
        Assert.Contains(p.Pages[0].Blocks, b => b.Kind == "button" && b.Url == "seite:neu");
        Assert.Contains(p.Pages[1].Blocks, b => b.Kind == "button" && b.Url == "seite:liste");

        var json = p.ToJson();
        Assert.StartsWith("{\n  \"Version\": 2,", json.ReplaceLineEndings("\n"));
        var back = SitePage.FromJson(json);
        Assert.Equal(p.ToJson(), back.ToJson());
        Assert.Equal(p.AppId, back.AppId);
    }

    [Fact(DisplayName = "T-0765 HTML der App: Navigation, Seiten als Abschnitte, Formular mit required, nur das eingebettete Skript (CSP-Hash), kein eval/innerHTML/fremde Quelle, Inhalt maskiert")]
    public void App_html_sicher()
    {
        var p = SitePage.Template("einkaufsliste")!;
        p.Title = "</script><script>alert(1)</script>";
        p.Pages[0].Blocks.Add(new Block("p", "<img src=x onerror=alert(2)>"));
        var html = p.ToHtml();
        Assert.Contains("<nav class=\"t-nav\" aria-label=\"Seiten\"><a href=\"#seite-liste\">Liste</a><a href=\"#seite-neu\">Hinzuf&#252;gen</a></nav>", html);
        Assert.Contains("<section id=\"seite-liste\" data-seite=\"liste\"", html);
        Assert.Contains("<section id=\"seite-neu\" data-seite=\"neu\"", html);
        Assert.Contains("<a class=\"btn\" href=\"#seite-neu\">", html);
        Assert.Contains("<input name=\"artikel\" type=\"text\" required>", html);
        Assert.Contains("<input name=\"preis\" type=\"number\" step=\"any\" inputmode=\"decimal\">", html);
        Assert.Contains("data-mass=\"summe:preis\"", html);
        // genau zwei Skript-Elemente: die eingebetteten Daten und das eigene Skript – der Titel bricht nicht aus
        Assert.Equal(2, Regex.Matches(html, "<script", RegexOptions.IgnoreCase).Count);
        Assert.Equal(2, Regex.Matches(html, "</script>", RegexOptions.IgnoreCase).Count);
        Assert.DoesNotContain("<img src=x", html);
        Assert.DoesNotContain("src=\"http", html); Assert.DoesNotContain("<script src", html); Assert.DoesNotContain("<link", html);
        Assert.Contains($"script-src '{SitePage.ScriptHash}'", html);
        Assert.DoesNotContain("eval(", SitePage.AppScript); Assert.DoesNotContain("innerHTML", SitePage.AppScript); Assert.DoesNotContain("new Function", SitePage.AppScript);
        Assert.Contains("localStorage", SitePage.AppScript);
        Assert.DoesNotContain("\r", SitePage.AppScript); // sonst passt der Hash nicht zum Text, den der Browser liest
        // das Skript steht genau so (Hash-gleich) in der Seite
        var body = Regex.Match(html, "<script>(.*?)</script>", RegexOptions.Singleline).Groups[1].Value;
        Assert.Equal(SitePage.AppScript, body);
    }

    [Fact(DisplayName = "T-0765 Einfache Seite bleibt Fassung 1 und ohne Skript; alte seite.json ohne Version und Pages wird die erste Seite")]
    public void Einfache_seite_bleibt()
    {
        var old = "{\n  \"Title\": \"Alt\",\n  \"Accent\": \"#22B35E\",\n  \"Blocks\": [ { \"Kind\": \"h1\", \"Text\": \"Hallo\", \"Url\": \"\" }, { \"Kind\": \"button\", \"Text\": \"Mehr\", \"Url\": \"https://example.org\" } ]\n}";
        var p = SitePage.FromJson(old);
        Assert.False(p.IsApp);
        var s = Assert.Single(p.Pages); Assert.Equal("start", s.Id);
        Assert.Equal(["h1", "button"], p.Blocks.Select(b => b.Kind));
        var json = p.ToJson().ReplaceLineEndings("\n");
        Assert.StartsWith("{\n  \"Version\": 1,", json);
        Assert.Contains("\"Blocks\": [", json); Assert.DoesNotContain("Pages", json); Assert.DoesNotContain("\"Data\"", json);
        var html = p.ToHtml();
        Assert.DoesNotContain("<script>", html); Assert.DoesNotContain("<nav", html);
        Assert.Contains("<h1>Hallo</h1>", html);
        // die neuere Fassung 3 wird abgelehnt
        Assert.Throws<InvalidDataException>(() => SitePage.FromJson(json.Replace("\"Version\": 1", "\"Version\": 3")));
    }

    [Fact(DisplayName = "T-0765 Seiten anlegen, umbenennen (Link hält), sortieren, löschen (Links zeigen dann auf die erste Seite)")]
    public void Seiten_verwalten()
    {
        var p = new SitePage { Blocks = [new("h1", "Start")] };
        var a = p.AddPage("Über uns"); Assert.Equal("ueber-uns", a.Id);
        var b = p.AddPage("Über uns"); Assert.Equal("ueber-uns-2", b.Id);
        p.Blocks.Add(new Block("button", "Mehr", "seite:" + a.Id));
        Assert.True(p.IsApp);
        p.RenamePage(1, "Team");
        Assert.Equal("Team", p.Pages[1].Name); Assert.Contains("href=\"#seite-ueber-uns\"", p.ToHtml());
        p.MovePage(1, 1); Assert.Equal(["start", "ueber-uns-2", "ueber-uns"], p.Pages.Select(s => s.Id));
        Assert.True(p.DeletePage(2));
        Assert.Equal("seite:start", p.Blocks[1].Url);
        Assert.Equal("#", p.LinkHref("seite:gibtsnicht"));
        Assert.True(p.DeletePage(1)); Assert.False(p.DeletePage(0)); // die letzte Seite bleibt
        Assert.Equal(["Artikel", "Menge", "Preis"], SitePage.ParseFields("Artikel*, Menge (Zahl), Preis (Zahl)").Select(f => f.Label));
        Assert.Equal([true, false, false], SitePage.ParseFields("Artikel*, Menge (Zahl), Preis (Zahl)").Select(f => f.Required));
        Assert.Equal(["text", "zahl", "zahl"], SitePage.ParseFields("Artikel*, Menge (Zahl), Preis (Zahl)").Select(f => f.Type));
    }

    [Fact(DisplayName = "T-0765 Rundreise: veröffentlichen, als ZIP packen, ZIP wieder öffnen – gleiche App samt Bild")]
    public void Rundreise_zip()
    {
        var dir = Tmp();
        try
        {
            var assets = Path.Combine(dir, "baukasten"); Directory.CreateDirectory(Path.Combine(assets, "bilder"));
            File.WriteAllBytes(Path.Combine(assets, "bilder", "logo.png"), [0x89, 0x50, 0x4E, 0x47, 1, 2, 3]);
            var p = SitePage.Template("einkaufsliste")!;
            p.Pages[0].Blocks.Insert(1, new Block("img", "Logo", "bilder/logo.png"));
            var html = p.Publish(Path.Combine(dir, "fertig"), assets);
            var zip = Path.Combine(dir, "app.zip"); ZipFile.CreateFromDirectory(Path.GetDirectoryName(html)!, zip);
            var target = Path.Combine(dir, "neu"); Directory.CreateDirectory(target);
            var back = SitePage.FromExport(zip, target);
            Assert.Equal(p.ToJson(), back.ToJson());
            Assert.Equal(File.ReadAllBytes(Path.Combine(assets, "bilder", "logo.png")), File.ReadAllBytes(Path.Combine(target, "bilder", "logo.png")));
            Assert.Equal(p.ToJson(), SitePage.FromExport(Path.GetDirectoryName(html)!).ToJson()); // auch der Ordner und …
            Assert.Equal(p.ToJson(), SitePage.FromExport(html).ToJson());                         // … die index.html selbst
            var foreign = Path.Combine(dir, "fremd.html"); File.WriteAllText(foreign, "<!doctype html><title>x</title>");
            Assert.Contains("nicht aus dem Baukasten", Assert.Throws<InvalidDataException>(() => SitePage.FromExport(foreign)).Message);
        }
        finally { Directory.Delete(dir, true); }
    }

    sealed class Fx : IDisposable
    {
        public readonly string Root = Path.Combine(Path.GetTempPath(), "trendo-t0765-ki-" + Guid.NewGuid().ToString("N")[..8]);
        public readonly TrendoCore Core; public readonly string Project; public readonly StudioWorkflow Wf;
        public Fx() { Core = new TrendoCore(Root, new InMemorySecretStore(), offline: true); Project = Core.Projects.Create("probe", "Probe").Id; Wf = new StudioWorkflow(Core, PromptActions.CreateDefault()); }
        public StudioExecutionContext Ctx() => new(Project, Principal.LocalOwner, CancellationToken.None, ResourceBudget.Default);
        public StudioPromptRequest Req(string prompt, string id) => new(id, Project, "X-03", prompt, [], "de", null, PrivacyMode.Local, new Provenance("test", null, null));
        public StudioExecutionResult Run(string prompt, string id) => Wf.Los(Project, Wf.Start(Req(prompt, id), Ctx()).RunId, Ctx());
        public SitePage Json() => SitePage.Load(Core.Projects.ResolveInProject(Project, "baukasten/seite.json"));
        public void Dispose()
        {
            try { foreach (var f in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories)) File.SetAttributes(f, FileAttributes.Normal); Directory.Delete(Root, true); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
    }

    [Fact(DisplayName = "T-0765 KI „seite.schreiben“: Daten und weitere Seite werden eine App; ein späterer einfacher Text ersetzt nur die erste Seite, Seiten, Daten und App-Kennung bleiben")]
    public void Ki_baut_app_und_laesst_seiten_stehen()
    {
        using var fx = new Fx();
        var r = fx.Run("Titel: Einkauf\nText: Was wir brauchen\nDaten: Einkäufe: Artikel*, Menge (Zahl), Preis (Zahl)\nSeite: Über uns", "ki-app");
        Assert.Equal(RunStatus.Verified, r.Status);
        var app = fx.Json();
        Assert.True(app.IsApp);
        Assert.Equal(["start", "einkaeufe-eintragen", "ueber-uns"], app.Pages.Select(s => s.Id));
        Assert.Equal(["artikel", "menge", "preis"], Assert.Single(app.Collections).Fields.Select(f => f.Id));
        Assert.Contains(app.Pages[0].Blocks, b => b.Kind == "stat" && b.Url == "summe:preis");
        Assert.Contains(app.Pages[0].Blocks, b => b.Kind == "datalist" && b.Data == "einkaeufe");
        Assert.Contains(app.Pages[1].Blocks, b => b.Kind == "form" && b.Data == "einkaeufe");
        Assert.Contains(app.Pages[0].Blocks, b => b.Kind == "button" && b.Url == "seite:ueber-uns");
        var html = File.ReadAllText(fx.Core.Projects.ResolveInProject(fx.Project, r.Artifacts.Single(a => a.RelativePath.EndsWith("seite.html", StringComparison.Ordinal)).RelativePath));
        Assert.Contains("<form class=\"t-form\" data-sammlung=\"einkaeufe\"", html);

        var r2 = fx.Run("Titel: Einkauf neu\nText: Nur die Startseite ändert sich", "ki-text");
        Assert.Equal(RunStatus.Verified, r2.Status);
        var after = fx.Json();
        Assert.Equal(app.AppId, after.AppId);
        Assert.Equal(["start", "einkaeufe-eintragen", "ueber-uns"], after.Pages.Select(s => s.Id));
        Assert.Single(after.Collections);
        Assert.Equal("Einkauf neu", after.Title);
        Assert.Contains(after.Blocks, b => b.Text == "Nur die Startseite ändert sich");

        var r3 = fx.Run("Titel: Einkauf 3\nText: Neu gebaut\nDaten: Einkäufe: Artikel*", "ki-app-2");
        Assert.Equal(app.AppId, fx.Json().AppId); // neu gebaute App: im Browser gespeicherte Einträge bleiben erreichbar
        Assert.Equal(RunStatus.Verified, r3.Status);
    }
}
