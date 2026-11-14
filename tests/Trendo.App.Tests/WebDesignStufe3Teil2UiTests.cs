using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0622 Teil 2 in der Oberfläche: Code neben dem Design (K6), Import (Z8), Skizze (K3), Veröffentlichen mit Zustimmung (K8).</summary>
public class WebDesignStufe3Teil2_Ui
{
    static void Pump() { for (var i = 0; i < 8; i++) Dispatcher.UIThread.RunJobs(); }
    const string Page = """{"root":{"props":{"title":"Start","description":"Kurz"}},"content":[{"type":"Ueberschrift","props":{"id":"h","text":"Willkommen","level":"h2"}},{"type":"Text","props":{"id":"t","text":"Absatz","align":"left"}}],"zones":{}}""";
    static JsonObject E(string tag, JsonObject attrs, params JsonNode[] kinder) => new() { ["tag"] = tag, ["attrs"] = attrs, ["kinder"] = new JsonArray(kinder) };
    static JsonObject T(string t) => new() { ["text"] = t };
    static string Png(int w, int h)
    {
        using var bmp = new SkiaSharp.SKBitmap(w, h); using var c = new SkiaSharp.SKCanvas(bmp);
        c.Clear(SkiaSharp.SKColors.Transparent); using var paint = new SkiaSharp.SKPaint { Color = SkiaSharp.SKColors.Red, StrokeWidth = 4 }; c.DrawLine(10, 10, w - 10, h - 10, paint);
        using var img = SkiaSharp.SKImage.FromBitmap(bmp); using var data = img.Encode(SkiaSharp.SKEncodedImageFormat.Png, 90);
        return "data:image/png;base64," + Convert.ToBase64String(data.ToArray());
    }

    [AvaloniaFact(DisplayName = "K6: Code zeigen, Klick auf einen Baustein springt zur Zeile, Änderung im Code landet nach „Code übernehmen“ in der Seite (vorher ein Stand)")]
    public async Task K6_code_pane()
    {
        // Ersatz für den GrapesJS-Kern: liefert den Baum zum geänderten Code
        MainWindow.WebDesignParseOverride = html => Task.FromResult<(string?, string?)>((new JsonObject { ["knoten"] = new JsonArray(
            E("section", new JsonObject { ["data-typ"] = "Ueberschrift", ["data-b"] = "h", ["data-p-level"] = "h3" }, E("p", new JsonObject { ["data-feld"] = "text" }, T(html.Contains("Hallo Code") ? "Hallo Code" : "Willkommen"))),
            E("blockquote", new JsonObject(), T("Ein Zitat"))) }.ToJsonString(), null));
        try
        {
            var (w, _, _) = Ui.Open();
            w.WebDesignMessage("{\"art\":\"speichern\",\"daten\":" + Page + "}"); Pump();
            w.Show("dev"); Pump();
            w.Find<Button>("web-code-zeigen").Press(); Pump();
            Assert.Contains("data-b=\"t\"", w.WebDesignCodeText);
            w.WebDesignMessage("""{"art":"auswahl","daten":{"ids":["t"]}}"""); Pump();
            Assert.Equal(WebDesignHtml.LineOf(w.WebDesignCodeText!, "t"), w.WebDesignCodeLine);
            await w.WebDesignCodeApply(w.WebDesignCodeText!.Replace("Willkommen", "Hallo Code")); Pump();
            var saved = WebDesignStates.Read(w.WebDesignDir);
            Assert.Contains("Hallo Code", saved); Assert.Contains("\"level\":\"h3\"", saved);
            Assert.Contains("Ein Zitat", System.Text.RegularExpressions.Regex.Unescape(saved)); // nicht abbildbar → HTML-Block, nichts verloren
            Assert.Contains("HTML-Block", w.WebDesignCodeNote);
            Assert.Contains(WebDesignStates.List(w.WebDesignDir), s => s.Name == "Vor Code-Übernahme");
        }
        finally { MainWindow.WebDesignParseOverride = null; }
    }

    [AvaloniaFact(DisplayName = "Z8: Internet-Adresse abgelehnt, ohne dass etwas gelesen wird; eigene Datei wird zur neuen Seite mit Meldeliste")]
    public async Task Z8_import_pane()
    {
        var calls = 0;
        MainWindow.WebDesignParseOverride = _ => { calls++; return Task.FromResult<(string?, string?)>((new JsonObject { ["knoten"] = new JsonArray(E("h2", new JsonObject(), T("Aus der Datei")), E("table", new JsonObject(), E("tr", new JsonObject(), E("td", new JsonObject(), T("Zelle"))))), ["css"] = 0 }.ToJsonString(), null)); };
        var src = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "trendo-imp-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            var (w, _, _) = Ui.Open();
            w.WebDesignMessage("{\"art\":\"speichern\",\"daten\":" + Page + "}"); Pump();
            w.Show("dev"); Pump();
            w.Find<TextBox>("web-import-pfad").Text = "https://fremde-seite.example/index.html";
            w.Find<Button>("web-import-start").Press(); Pump();
            Assert.Contains("Internet-Adresse", w.WebDesignImportNote); Assert.Equal(0, calls);
            File.WriteAllText(Path.Combine(src, "angebot.html"), "<title>Angebot</title><h2>Aus der Datei</h2>");
            await w.WebDesignImportHtml(Path.Combine(src, "angebot.html")); Pump();
            Assert.Equal(1, calls);
            Assert.Contains("angebot", WebDesignSite.Pages(w.WebDesignDir));
            Assert.Equal("angebot", WebDesignSite.Active(w.WebDesignDir).Adresse);
            Assert.Contains("Meldeliste", w.WebDesignImportNote); Assert.Contains("<table>", w.WebDesignImportNote);
        }
        finally { MainWindow.WebDesignParseOverride = null; Directory.Delete(src, true); }
    }

    sealed class FakeFigma : HttpMessageHandler
    {
        public readonly List<(string Url, bool Token)> Calls = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        {
            Calls.Add((r.RequestUri!.AbsoluteUri, r.Headers.Contains("X-Figma-Token")));
            var path = r.RequestUri.AbsolutePath;
            if (path.StartsWith("/v1/files/", StringComparison.Ordinal))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"name":"Landing","document":{"children":[{"type":"CANVAS","name":"S","children":[{"type":"FRAME","absoluteBoundingBox":{"y":0},"children":[{"type":"TEXT","id":"1:2","characters":"Titel aus Figma","style":{"fontSize":40},"absoluteBoundingBox":{"y":0}},{"type":"RECTANGLE","id":"1:3","name":"Foto","fills":[{"type":"IMAGE"}],"absoluteBoundingBox":{"y":50}}]}]}]}}""", Encoding.UTF8, "application/json") });
            if (path.StartsWith("/v1/images/", StringComparison.Ordinal))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"images":{"1:3":"https://s3-alpha.figma.com/img/x.png"}}""", Encoding.UTF8, "application/json") });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0]) });
        }
    }

    [AvaloniaFact(DisplayName = "Z8 mit Figma: Schlüssel geht nur an api.figma.com, nicht an den Bild-Speicher, steht nirgends im Projekt; Seite mit Bild entsteht")]
    public async Task Z8_figma()
    {
        var fake = new FakeFigma(); MainWindow.FigmaHandlerOverride = fake;
        const string token = "figd_geheimer-schluessel-1234567890";
        try
        {
            var (w, _, _) = Ui.Open();
            w.WebDesignMessage("{\"art\":\"speichern\",\"daten\":" + Page + "}"); Pump();
            await w.WebDesignImportFigma("https://www.figma.com/design/AbCdEf123456/Landing", token); Pump();
            Assert.Contains("nicht gespeichert", w.WebDesignImportNote);
            Assert.All(fake.Calls.Where(c => c.Url.Contains("api.figma.com")), c => Assert.True(c.Token));
            Assert.All(fake.Calls.Where(c => !c.Url.Contains("api.figma.com")), c => Assert.False(c.Token)); // Bild-Speicher bekommt den Schlüssel nicht
            Assert.True(File.Exists(Path.Combine(w.WebDesignDir, "bilder", "figma", "figma-13.png")));
            Assert.Contains("Titel aus Figma", WebDesignStates.Read(w.WebDesignDir));
            foreach (var f in Directory.EnumerateFiles(w.Core.DataRoot, "*", SearchOption.AllDirectories).Where(f => new FileInfo(f).Length < 5_000_000))
                Assert.DoesNotContain(token, File.ReadAllText(f)); // nirgends gespeichert, auch nicht im Protokoll
        }
        finally { MainWindow.FigmaHandlerOverride = null; }
    }

    [AvaloniaFact(DisplayName = "K3: Skizze aus dem Editor kommt an; ohne bildfähige KI ein Hinweis statt Fehler; mit bildfähiger KI enthält der Auftrag das Bild (Seite plus Striche)")]
    public async Task K3_sketch()
    {
        try
        {
            var (w, _, _) = Ui.Open();
            w.WebDesignMessage("{\"art\":\"speichern\",\"daten\":" + Page + "}"); Pump();
            w.WebDesignMessage(new JsonObject { ["art"] = "skizze", ["daten"] = new JsonObject { ["bild"] = Png(300, 200), ["breite"] = 300, ["hoehe"] = 200 } }.ToJsonString()); Pump();
            Assert.True(w.WebDesignHasSketch);
            w.Show("dev"); Pump();
            Assert.NotNull(w.Find<Button>("web-ki-skizze"));
            MainWindow.WebDesignKiStateOverride = (false, true); // nur Text-KI auf dem PC
            await w.WebDesignAiSketch("Knopf größer"); Pump();
            Assert.Contains("Bilder sehen", w.WebDesignNote);
            string? seen = null; long size = 0; (int W, int H) dims = default;
            MainWindow.WebDesignVisionOverride = (image, _) =>
            {
                seen = image; size = new FileInfo(image).Length;
                using var b = SkiaSharp.SKBitmap.Decode(image); dims = (b.Width, b.Height);
                return Task.FromResult<string?>("Rote Linie quer über die Überschrift");
            };
            string? prompt = null;
            MainWindow.WebDesignKiOverride = (_, p) => { prompt ??= p; return Task.FromResult("""{"aktionen":[{"aktion":"element_aendern","id":"h","props":{"text":"Größer"}}]}"""); };
            await w.WebDesignAiSketch("Überschrift ändern"); Pump();
            Assert.NotNull(seen); Assert.True(size > 0); Assert.Equal((300, 200), dims);
            Assert.Contains("Rote Linie quer über die Überschrift", prompt); Assert.Contains("Überschrift ändern", prompt); Assert.Contains("\"h\"", prompt);
            Assert.False(File.Exists(seen)); // Bild für die KI bleibt nicht liegen
            Assert.False(w.WebDesignHasSketch);
        }
        finally { MainWindow.WebDesignKiStateOverride = null; MainWindow.WebDesignVisionOverride = null; MainWindow.WebDesignKiOverride = null; }
    }

    sealed class FakeNetlify : HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        {
            Calls++;
            var json = r.RequestUri!.AbsolutePath.EndsWith("/sites", StringComparison.Ordinal) ? """{"id":"site-1"}""" : """{"id":"d1","ssl_url":"https://meine-seite.netlify.app"}""";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
        }
    }

    [AvaloniaFact(DisplayName = "K8: ohne Zustimmung kein Hochladen; nach Zustimmung genau einmal, mit ehrlichem Bericht der Dateien; Zustimmung gilt nicht für den nächsten Export")]
    public async Task K8_publish()
    {
        var fake = new FakeNetlify(); MainWindow.NetlifyHandlerOverride = fake;
        MainWindow.WebDesignEditorDirOverride = Path.Combine(WebDesignKi_Ui.Repo(), "src", "Trendo.App", "Assets", "webdesign");
        try
        {
            var (w, _, _) = Ui.Open();
            w.Core.Secrets.Set(NetlifyLink.SecretKey, "nfp_0123456789abcdefghijklmnop");
            w.WebDesignMessage("{\"art\":\"speichern\",\"daten\":" + Page + "}"); Pump();
            Assert.Null(Trendo.Core.WebDesignExport.ExportSite(w.WebDesignDir, MainWindow.WebDesignEditorDir).Error);
            w.Show("dev"); Pump();
            w.Find<Button>("web-pub-pruefen").Press(); Pump();
            Assert.Contains("Prüfung bestanden", w.WebDesignPublishNote);
            await w.WebDesignPublishUpload(); Pump();
            Assert.Equal(0, fake.Calls); Assert.Contains("Zustimmung", w.WebDesignPublishNote);
            w.Find<Button>("web-pub-pruefen").Press(); Pump();
            w.Find<Button>("web-pub-hochladen").Press(); Pump();
            Assert.Equal(0, fake.Calls); // erst fragen
            w.Find<Button>("web-pub-ja").Press();
            for (var i = 0; i < 100 && !w.WebDesignPublishNote.StartsWith("Hochgeladen", StringComparison.Ordinal); i++) { await Task.Delay(20); Pump(); }
            Assert.True(fake.Calls > 0);
            Assert.StartsWith("Hochgeladen zu https://meine-seite.netlify.app", w.WebDesignPublishNote);
            Assert.Contains("index.html", w.WebDesignPublishNote);
            var after = fake.Calls;
            await w.WebDesignPublishUpload(); Pump(); // Zustimmung ist verbraucht
            Assert.Equal(after, fake.Calls);
        }
        finally { MainWindow.NetlifyHandlerOverride = null; MainWindow.WebDesignEditorDirOverride = null; }
    }
}

/// <summary>T-0622 Teil 2 im echten Browser: GrapesJS-Kern liest den Code der Seite verlustfrei zurück und eine fremde HTML-Seite ohne Skripte.</summary>
public class WebDesignStufe3Teil2_Browser
{
    const string Page = """{"root":{"props":{"title":"Seite"}},"content":[{"type":"Hero","props":{"id":"hero","kicker":"Neu","titel":"Brot <frisch> & gut","text":"Zeile 1\nZeile 2","knopf":"Los","knopfZiel":"kontakt","knopf2":"","knopf2Ziel":"#","bild":"","bildAlt":"","bewegung":{"erscheinen":"einblenden"},"anpassungen":{"Handy":{"ausrichtung":"center"}}}},{"type":"Preise","props":{"id":"preise","titel":"Preise","tarife":[{"name":"Basis","preis":"9 €","zeitraum":"im Monat","merkmale":"A\nB","knopf":"Wählen","ziel":"#","hervorgehoben":"ja"}]}},{"type":"Stapel","props":{"id":"st","richtung":"waagerecht","abstand":3,"breite":"fuellt","ausrichtung":"mitte","inhalt":[{"type":"Text","props":{"id":"t1","text":"Innen","align":"center"}}]}},{"type":"Html","props":{"id":"html","knoten":[{"tag":"table","attrs":{},"kinder":[{"tag":"tbody","attrs":{},"kinder":[{"tag":"tr","attrs":{},"kinder":[{"tag":"td","attrs":{"colspan":"2"},"kinder":[{"text":"Zelle"}]}]}]}]}]}}],"zones":{}}""";

    static string Editor() => Path.Combine(WebDesignKi_Ui.Repo(), "src", "Trendo.App", "Assets", "webdesign");
    static JsonNode? Content(string json) => JsonNode.Parse(WebDesign.Validate(json).Json!)!["content"];

    [Fact(DisplayName = "K3/K6 im Editor in echtem Chrome: Skizze mit der Maus zeichnen und als PNG übergeben; HTML-Block zeigt nur erlaubte Elemente (übersprungen ohne Chrome/Node)")]
    public void Real_editor_sketch_and_html_block()
    {
        if (WebsitePreview.FindBrowser() is not { } chrome || Cli.Find("node") is not { } node) { Assert.Skip("Übersprungen – Chrome/Edge oder Node.js fehlt auf diesem PC."); return; }
        var run = Cli.Run(node, [Path.Combine(WebDesignKi_Ui.Repo(), "tools", "webdesign-editor", "pruefe-skizze.mjs"), chrome, Editor()], timeoutMs: 120_000);
        Assert.True(run.Ok, run.Out + run.Err);
    }

    [Fact(DisplayName = "K6 in echtem Chrome mit GrapesJS: Seite → Code → Seite ist gleich; fremde Seite: Skript und Ereignisse weg, Text vollständig (übersprungen ohne Chrome/Edge)")]
    public async Task Real_parser_round_trip()
    {
        if (WebsitePreview.FindBrowser() is null) { Assert.Skip("Übersprungen – Chrome oder Edge fehlt auf diesem PC."); return; }
        var proj = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "trendo-web3p-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            var html = WebDesignHtml.ToHtml(JsonNode.Parse(Page)!.AsObject());
            var (json, err) = await WebsitePreview.ParseHtmlAsync(Editor(), html);
            Assert.Null(err);
            var r = WebDesignHtml.FromTree((JsonNode.Parse(json!)!["knoten"] as JsonArray)!, new WebDesignHtml.Context(proj, null));
            var back = new JsonObject { ["root"] = new JsonObject { ["props"] = new JsonObject { ["title"] = "Seite" } }, ["content"] = r.Content, ["zones"] = new JsonObject() }.ToJsonString();
            Assert.True(JsonNode.DeepEquals(Content(Page), Content(back)), Content(back)!.ToJsonString());

            var foreign = "<header><h1>Bäckerei</h1><nav><a href=\"ueber.html\">Über uns</a></nav></header><p onclick=\"steal()\">Frisch <em>jeden</em> Tag</p><script>alert(1)</script><a href=\"javascript:alert(2)\">Klick</a><ul><li>Roggen</li><li>Dinkel</li></ul><style>p{color:red}</style>";
            var (fj, ferr) = await WebsitePreview.ParseHtmlAsync(Editor(), foreign);
            Assert.Null(ferr);
            var fr = WebDesignHtml.FromTree((JsonNode.Parse(fj!)!["knoten"] as JsonArray)!, new WebDesignHtml.Context(proj, null));
            var text = System.Text.RegularExpressions.Regex.Unescape(fr.Content.ToJsonString());
            foreach (var t in new[] { "Bäckerei", "Über uns", "Frisch", "jeden", "Tag", "Klick", "Roggen", "Dinkel" }) Assert.Contains(t, text);
            Assert.DoesNotContain("alert", text); Assert.DoesNotContain("steal", text); Assert.DoesNotContain("javascript", text); Assert.DoesNotContain("color:red", text);
            Assert.Equal(1, JsonNode.Parse(fj!)!["css"]!.GetValue<int>());
        }
        finally { Directory.Delete(proj, true); }
    }
}
