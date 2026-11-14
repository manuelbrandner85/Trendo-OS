using System.Text.Json.Nodes;
using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0622 Teil 2: K6 (Code und Design), Z8 (Import, Figma), K8 (Veröffentlichen). Die Bäume entsprechen dem, was der GrapesJS-Kern liefert ({tag, attrs, kinder} / {text});
/// den echten Parser prüft der Browser-Test in der App.</summary>
public class WebDesignStufe3Teil2Tests
{
    static string Dir() => Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "trendo-web3b-" + Guid.NewGuid().ToString("N"))).FullName;
    static void Clean(string d) { try { Directory.Delete(d, true); } catch (IOException) { } }
    static JsonObject Doc(string json) => JsonNode.Parse(json)!.AsObject();
    static JsonObject T(string text) => new() { ["text"] = text };
    static JsonObject E(string tag, JsonObject? attrs = null, params JsonNode[] kinder) => new() { ["tag"] = tag, ["attrs"] = attrs ?? new JsonObject(), ["kinder"] = new JsonArray(kinder) };
    static JsonObject A(params (string K, string V)[] a) => new(a.Select(x => KeyValuePair.Create(x.K, (JsonNode?)x.V)));

    const string Page = """{"root":{"props":{"title":"Seite"}},"content":[{"type":"Hero","props":{"id":"hero","kicker":"Neu","titel":"Brot <frisch>","text":"Zeile 1\nZeile 2","knopf":"Los","knopfZiel":"kontakt","knopf2":"","knopf2Ziel":"#","bild":"","bildAlt":"","bewegung":{"erscheinen":"einblenden"},"anpassungen":{"Handy":{"ausrichtung":"center"}}}},{"type":"Preise","props":{"id":"preise","titel":"Preise","tarife":[{"name":"Basis","preis":"9 €","zeitraum":"im Monat","merkmale":"A\nB","knopf":"Wählen","ziel":"#","hervorgehoben":"ja"}]}},{"type":"Stapel","props":{"id":"st","richtung":"waagerecht","abstand":3,"breite":"fuellt","ausrichtung":"mitte","inhalt":[{"type":"Text","props":{"id":"t1","text":"Innen","align":"center"}}]}}],"zones":{}}""";

    /// <summary>HTML so in einen Baum lesen, wie GrapesJS ihn liefert – nur für die Form dieser Tests (eigener, kleiner Leser für wohlgeformtes XHTML).</summary>
    static JsonArray Parse(string html)
    {
        var x = System.Xml.Linq.XElement.Parse("<r>" + html.Replace("<br>", "<br/>").Replace("alt=\"\">", "alt=\"\"/>") + "</r>", System.Xml.Linq.LoadOptions.PreserveWhitespace);
        JsonNode Node(System.Xml.Linq.XNode n) => n is System.Xml.Linq.XText t ? T(t.Value)
            : n is System.Xml.Linq.XElement e ? E(e.Name.LocalName, new JsonObject(e.Attributes().Select(a => KeyValuePair.Create(a.Name.LocalName, (JsonNode?)a.Value))), e.Nodes().Select(Node).Where(k => k is not JsonObject o || o["text"] is null || o["text"]!.ToString().Trim().Length > 0).ToArray()) : T("");
        return new JsonArray(x.Nodes().Select(Node).Where(k => k is not JsonObject o || o["text"] is null || o["text"]!.ToString().Trim().Length > 0).ToArray());
    }

    // ---------- K6 ----------

    [Fact(DisplayName = "K6: Seite → Code → Seite ist verlustfrei (Texte mit Zeilenumbruch, Listen, Bereiche, Bewegung, Anpassungen)")]
    public void K6_round_trip()
    {
        var html = WebDesignHtml.ToHtml(Doc(Page));
        Assert.Contains("data-b=\"hero\"", html); Assert.Contains("Brot &lt;frisch&gt;", html);
        var r = WebDesignHtml.FromTree(Parse(html), new WebDesignHtml.Context(Dir(), null));
        Assert.Equal(0, r.HtmlBlocks);
        Assert.True(JsonNode.DeepEquals(Doc(WebDesign.Validate(new JsonObject { ["root"] = new JsonObject { ["props"] = new JsonObject() }, ["content"] = Doc(Page)["content"]!.DeepClone(), ["zones"] = new JsonObject() }.ToJsonString()).Json!)["content"],
            Doc(WebDesign.Validate(new JsonObject { ["root"] = new JsonObject { ["props"] = new JsonObject() }, ["content"] = r.Content, ["zones"] = new JsonObject() }.ToJsonString()).Json!)["content"]), r.Content.ToJsonString());
    }

    [Fact(DisplayName = "K6: Änderung im Code erscheint im Editor und umgekehrt; nicht abgleichbarer Code bleibt als HTML-Block erhalten")]
    public void K6_both_directions()
    {
        var html = WebDesignHtml.ToHtml(Doc(Page)).Replace("Innen", "Im Code geändert").Replace("</section>\n<section data-typ=\"Preise\"", "</section>\n<table><tr><td>Öffnungszeiten</td><td>7–18 Uhr</td></tr></table>\n<section data-typ=\"Preise\"");
        var r = WebDesignHtml.FromTree(Parse(html), new WebDesignHtml.Context(Dir(), null));
        var html2 = System.Text.RegularExpressions.Regex.Unescape(r.Content.ToJsonString());
        Assert.Contains("Im Code geändert", html2); // Code → Editor
        Assert.Equal(1, r.HtmlBlocks);
        Assert.Contains("7–18 Uhr", html2); // nichts verloren
        Assert.Contains(r.Report, x => x.Contains("HTML-Block"));
        var changed = Doc(Page); changed["content"]![0]!["props"]!["titel"] = "Im Editor geändert";
        Assert.Contains("Im Editor geändert", WebDesignHtml.ToHtml(changed)); // Editor → Code
        Assert.Equal(1, WebDesignHtml.LineOf(WebDesignHtml.ToHtml(changed), "hero"));
        Assert.True(WebDesignHtml.LineOf(WebDesignHtml.ToHtml(changed), "t1") > 1); // Klick springt zur Stelle
    }

    [Fact(DisplayName = "K6/Z8: HTML-Block nur aus erlaubten Elementen: Skript, Ereignisse, javascript: und fremde Bilder fallen weg, auch in einer veränderten Datei")]
    public void Html_block_sanitized()
    {
        var report = new List<string>();
        var clean = WebDesignHtml.Sanitize(new JsonArray(
            E("p", A(("onclick", "x()"), ("style", "color:red")), T("Hallo "), E("a", A(("href", "javascript:alert(1)")), T("Klick")), E("script", null, T("alert(2)"))),
            E("form", null, E("label", null, T("Name")), E("input", A(("name", "n")))),
            E("img", A(("src", "https://fremd.example/x.png"))), E("iframe", A(("src", "https://fremd.example")))), report);
        var s = clean.ToJsonString();
        Assert.DoesNotContain("onclick", s); Assert.DoesNotContain("style", s); Assert.DoesNotContain("javascript", s); Assert.DoesNotContain("alert(2)", s);
        Assert.DoesNotContain("iframe", s); Assert.DoesNotContain("fremd.example", s); Assert.DoesNotContain("input", s);
        Assert.Contains("Hallo", s); Assert.Contains("Klick", s); Assert.Contains("Name", s); // Text bleibt
        Assert.Contains(report, r => r.Contains("script")); Assert.Contains(report, r => r.Contains("form"));
        var page = new JsonObject { ["root"] = new JsonObject { ["props"] = new JsonObject() }, ["content"] = new JsonArray(new JsonObject { ["type"] = "Html", ["props"] = new JsonObject { ["id"] = "h", ["knoten"] = new JsonArray(E("script", null, T("böse")), E("p", A(("onmouseover", "x")), T("ok"))) } }), ["zones"] = new JsonObject() };
        var saved = WebDesign.Validate(page.ToJsonString()).Json!;
        Assert.DoesNotContain("böse", saved); Assert.DoesNotContain("onmouseover", saved); Assert.Contains("ok", saved);
    }

    // ---------- Z8 ----------

    [Fact(DisplayName = "Z8: Beispielseite ergibt Bausteine plus Meldeliste; nichts geht verloren; Bilder aus dem Ordner der Seite reisen mit, fremde nicht")]
    public void Z8_import_page()
    {
        var src = Dir(); var proj = Dir(); try
        {
            File.WriteAllBytes(Path.Combine(src, "brot.png"), [137, 80, 78, 71, 1]);
            File.WriteAllText(Path.Combine(src, "seite.html"), "<title>Unsere Bäckerei</title>");
            var tree = new JsonArray(
                E("header", null, E("h1", null, T("Bäckerei am Platz")), E("nav", null, E("a", A(("href", "ueber.html")), T("Über uns")), E("a", A(("href", "kontakt.html")), T("Kontakt")))),
                E("section", null, E("h2", null, T("Unser Brot")), E("p", null, T("Jeden Morgen frisch.")), E("img", A(("src", "brot.png"), ("alt", "Brot"))), E("img", A(("src", "https://cdn.example/x.jpg"))),
                    E("p", null, T("Mit "), E("strong", null, T("Sauerteig")), T(" gebacken."))),
                E("ul", null, E("li", null, T("Roggen")), E("li", null, T("Dinkel"))),
                E("a", A(("href", "bestellen.html")), T("Jetzt bestellen")));
            var json = new JsonObject { ["knoten"] = tree, ["css"] = 3 }.ToJsonString();
            WebDesign.Save(proj, WebDesign.Empty());
            var o = WebDesignImport.CreatePage(proj, Path.Combine(src, "seite.html"), json);
            Assert.Null(o.Error);
            Assert.Equal("seite", o.Adresse);
            var page = File.ReadAllText(Path.Combine(proj, "seiten", "seite.trendo.json"));
            foreach (var text in new[] { "Bäckerei am Platz", "Über uns", "Kontakt", "Unser Brot", "Jeden Morgen frisch.", "Sauerteig", "Roggen", "Dinkel", "Jetzt bestellen", "Unsere Bäckerei" })
                Assert.Contains(text, System.Text.RegularExpressions.Regex.Unescape(page)); // nichts verloren
            var doc = Doc(page);
            var types = string.Join(",", doc["content"]!.AsArray().Select(b => b!["type"]!.ToString()));
            Assert.Contains("Navigation", page); Assert.Contains("\"type\":\"Ueberschrift\"", page); Assert.Contains("\"type\":\"Bild\"", page); Assert.Contains("\"type\":\"Html\"", page); Assert.Contains("Knopf", types);
            Assert.True(File.Exists(Path.Combine(proj, "bilder", "import", "brot.png")));
            Assert.Contains("bilder/import/brot.png", page); Assert.DoesNotContain("cdn.example", page);
            Assert.Contains(o.Report, r => r.Contains("cdn.example")); Assert.Contains(o.Report, r => r.Contains("Stilregel")); Assert.Contains(o.Report, r => r.Contains("<ul>"));
            Assert.True(o.Mapped >= 6); Assert.True(o.HtmlBlocks >= 2);
        }
        finally { Clean(src); Clean(proj); }
    }

    [Theory(DisplayName = "Z8: Internet-Adresse oder fremde Quelle wird abgelehnt")]
    [InlineData("https://www.beispiel.de/index.html")] [InlineData("http://x.de")] [InlineData("www.beispiel.de")] [InlineData("//server/freigabe/a.html")] [InlineData(@"\\server\freigabe\a.html")] [InlineData("file:///C:/a.html")] [InlineData("relativ/a.html")]
    public void Z8_url_rejected(string source) => Assert.NotNull(WebDesignImport.CheckSource(source));

    [Fact(DisplayName = "Z8: nur HTML-Dateien, keine Verknüpfungen, Bilder nur aus dem Ordner der Seite (kein Weg nach oben)")]
    public void Z8_source_rules()
    {
        var d = Dir(); var proj = Dir(); try
        {
            File.WriteAllText(Path.Combine(d, "a.txt"), "x");
            Assert.NotNull(WebDesignImport.CheckSource(Path.Combine(d, "a.txt")));
            var sub = Directory.CreateDirectory(Path.Combine(d, "seite")).FullName;
            File.WriteAllText(Path.Combine(sub, "s.html"), "<p>x</p>");
            File.WriteAllBytes(Path.Combine(d, "geheim.png"), [137, 80, 78, 71]);
            Assert.Null(WebDesignImport.CheckSource(Path.Combine(sub, "s.html")));
            WebDesign.Save(proj, WebDesign.Empty());
            var o = WebDesignImport.CreatePage(proj, Path.Combine(sub, "s.html"), new JsonObject { ["knoten"] = new JsonArray(E("img", A(("src", "../geheim.png"))), E("p", null, T("Text"))) }.ToJsonString());
            Assert.Null(o.Error);
            Assert.False(Directory.Exists(Path.Combine(proj, "bilder", "import")) && Directory.GetFiles(Path.Combine(proj, "bilder", "import")).Length > 0);
        }
        finally { Clean(d); Clean(proj); }
    }

    [Fact(DisplayName = "Z8 mit Figma: nur eigener figma.com-Link; Rahmen werden Bereiche, Texte nach Größe Überschrift oder Absatz, Bildflächen Bilder; Schlüssel nur geprüft, nie gespeichert")]
    public void Z8_figma_map()
    {
        Assert.Equal("AbCdEf123456", WebDesignFigma.KeyOf("https://www.figma.com/design/AbCdEf123456/Meine-Seite?node-id=1-2").Key);
        Assert.Null(WebDesignFigma.KeyOf("https://evil.example/design/AbCdEf123456/x").Key);
        Assert.Null(WebDesignFigma.KeyOf("http://www.figma.com/design/AbCdEf123456/x").Key);
        Assert.NotNull(WebDesignFigma.CheckToken("kurz"));
        Assert.False(WebDesignFigma.IsImageUrl("https://evil.example/a.png")); Assert.True(WebDesignFigma.IsImageUrl("https://s3-alpha.figma.com/img/a.png"));
        var file = JsonNode.Parse("""
            {"name":"Landing","document":{"children":[{"type":"CANVAS","name":"Seite 1","children":[
              {"type":"FRAME","name":"Hero","absoluteBoundingBox":{"x":0,"y":0},"children":[
                {"type":"TEXT","id":"1:2","characters":"Großer Titel","style":{"fontSize":48},"absoluteBoundingBox":{"x":0,"y":10}},
                {"type":"GROUP","children":[{"type":"TEXT","id":"1:3","characters":"Kleiner Text","style":{"fontSize":16,"textAlignHorizontal":"CENTER"},"absoluteBoundingBox":{"x":0,"y":80}}]},
                {"type":"RECTANGLE","id":"1:4","name":"Foto","fills":[{"type":"IMAGE"}],"absoluteBoundingBox":{"x":0,"y":40}},
                {"type":"TEXT","id":"1:5","characters":"versteckt","visible":false,"style":{"fontSize":16}}]}]}]}}
            """)!.AsObject();
        var m = WebDesignFigma.Map(file);
        Assert.Equal("Landing", m.Title);
        Assert.Equal(["1:4"], m.ImageNodes);
        var inner = m.Content[0]!["props"]!["content"]!.AsArray();
        Assert.Equal(["Ueberschrift", "Bild", "Text"], inner.Select(b => b!["type"]!.ToString())); // nach y sortiert
        Assert.Equal("center", inner[2]!["props"]!["align"]!.ToString());
        Assert.DoesNotContain("versteckt", m.Content.ToJsonString());
        WebDesignFigma.SetImages(m.Content, new Dictionary<string, string> { ["1:4"] = "bilder/figma/figma-14.png" });
        Assert.Equal("bilder/figma/figma-14.png", inner[1]!["props"]!["src"]!.ToString());
        Assert.DoesNotContain("\"figma\"", m.Content.ToJsonString());
    }

    // ---------- K8 ----------

    [Fact(DisplayName = "K8: ohne Zustimmung kein Hochladen; Zustimmung gilt nur für genau diese Dateien; Prüffehler und Geheimnisse sperren; Paket ohne Hochladen")]
    public void K8_consent()
    {
        var d = Dir(); try
        {
            WebDesign.Save(d, """{"root":{"props":{"title":"Start","description":"Kurz"}},"content":[{"type":"Text","props":{"id":"t","text":"Hallo","align":"left"}}],"zones":{}}""");
            Assert.False(WebDesignPublish.Prepare(d, []).Ok); // noch kein Export
            var editor = Path.Combine(Repo(), "src", "Trendo.App", "Assets", "webdesign");
            Assert.Null(WebDesignExport.ExportSite(d, editor).Error);
            var plan = WebDesignPublish.Prepare(d, []);
            Assert.True(plan.Ok, string.Join(" | ", plan.Blockers));
            Assert.Contains(plan.Files, f => f.Rel == "index.html");
            Assert.DoesNotContain(plan.Files, f => f.Rel.StartsWith(".trendo/", StringComparison.Ordinal));
            Assert.Contains("Zustimmung", WebDesignPublish.CheckConsent(d, plan, null));
            Assert.Contains("Zustimmung", WebDesignPublish.CheckConsent(d, plan, "falsch"));
            Assert.Null(WebDesignPublish.CheckConsent(d, plan, plan.Fingerprint));
            File.AppendAllText(Path.Combine(d, WebDesign.ExportDir, "index.html"), "<!-- geändert -->");
            Assert.Contains("geändert", WebDesignPublish.CheckConsent(d, plan, plan.Fingerprint)); // Zustimmung galt dem alten Stand
            var (zip, err) = WebDesignPublish.Package(d, plan);
            Assert.Null(zip); Assert.Contains("geändert", err);
            var fresh = WebDesignPublish.Prepare(d, []);
            var (zip2, err2) = WebDesignPublish.Package(d, fresh);
            Assert.Null(err2); Assert.True(File.Exists(zip2));
            File.WriteAllText(Path.Combine(d, WebDesign.ExportDir, "config.js"), "const api_key = \"sk-1234567890abcdef1234567890abcdef\";");
            var leak = WebDesignPublish.Prepare(d, []);
            Assert.False(leak.Ok); Assert.Contains(leak.Blockers, b => b.Contains("Geheimnis"));
            Assert.Contains("Prüfung", WebDesignPublish.CheckConsent(d, leak, leak.Fingerprint));
            Assert.False(WebDesignPublish.Prepare(d, ["/index.html: kein eigener Seitentitel."]).Ok);
            Assert.Contains("Hochgeladen zu https://x.netlify.app", WebDesignPublish.Report(fresh, "https://x.netlify.app"));
        }
        finally { Clean(d); }
    }

    static string Repo()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent) if (File.Exists(Path.Combine(d.FullName, "Trendo.sln"))) return d.FullName;
        throw new DirectoryNotFoundException();
    }

    // ---------- K3 ----------

    [Fact(DisplayName = "K3: Skizze ist eine Bild-Aufgabe; ohne bildfähige KI ein klarer Hinweis, kein Fehler; der Auftrag nennt Bild, Wunsch und Seite")]
    public void K3_needs_vision()
    {
        Assert.Equal(KiBedarf.Bild, WebDesignKi.Tasks["skizze"]);
        var c = WebDesignKi.Choose(KiBedarf.Bild, false, true, false);
        Assert.False(c.Ok); Assert.Contains("Bilder sehen", c.Note);
        var p = WebDesignKi.SketchPrompt("Knopf größer", Page, "Rote Markierung um den Knopf");
        Assert.Contains("Rote Markierung um den Knopf", p); Assert.Contains("Knopf größer", p); Assert.Contains("\"hero\"", p);
    }
}
