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

/// <summary>T-0622: K7 im echten Browser, A4 in der Oberfläche und Gegenproben zu QA- und Security-Befunden in der App.</summary>
public class WebDesignStufe3Qa_Ui
{
    static void Pump() { for (var i = 0; i < 8; i++) Dispatcher.UIThread.RunJobs(); }
    const string Page = """{"root":{"props":{"title":"Start","description":"Kurz"}},"content":[{"type":"Ueberschrift","props":{"id":"h","text":"Willkommen","level":"h2"}}],"zones":{}}""";

    [AvaloniaFact(DisplayName = "QA T2-B2: „Code übernehmen“ verweigert veralteten Code (Editor geändert, andere Seite) statt Änderungen zu überschreiben")]
    public async Task Qa_stale_code_refused()
    {
        var calls = 0;
        MainWindow.WebDesignParseOverride = _ => { calls++; return Task.FromResult<(string?, string?)>(("{\"knoten\":[]}", null)); };
        try
        {
            var (w, _, _) = Ui.Open();
            w.WebDesignMessage("{\"art\":\"speichern\",\"daten\":" + Page + "}"); Pump();
            w.WebDesignCodeShow(); Pump();
            w.WebDesignMessage("{\"art\":\"speichern\",\"daten\":" + Page.Replace("Willkommen", "Neu im Editor") + "}"); Pump();
            await w.WebDesignCodeApply(); Pump();
            Assert.Contains("geändert", w.WebDesignCodeNote); Assert.Equal(0, calls);
            Assert.Contains("Neu im Editor", WebDesignStates.Read(w.WebDesignDir));
            w.WebDesignCodeShow(); Pump();
            w.WebDesignCreatePage("Über uns", "ueber-uns", false); Pump(); // andere Seite
            await w.WebDesignCodeApply("<p>x</p>"); Pump();
            Assert.Equal(0, calls);
            Assert.Contains("Neu im Editor", File.ReadAllText(Path.Combine(w.WebDesignDir, WebDesign.PageFile)));
            w.WebDesignCodeShow(); Pump();
            await w.WebDesignCodeApply("   "); Pump();
            Assert.Contains("leer", w.WebDesignCodeNote); Assert.Equal(0, calls);
        }
        finally { MainWindow.WebDesignParseOverride = null; }
    }

    sealed class Redirecting : HttpMessageHandler
    {
        public readonly List<string> Calls = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        {
            Calls.Add(r.RequestUri!.Host);
            var m = new HttpResponseMessage(HttpStatusCode.Found); m.Headers.Location = new Uri("https://sammler.example/abgreifen");
            return Task.FromResult(m);
        }
    }

    [AvaloniaFact(DisplayName = "Sicherheit 1: Figma-Weiterleitung wird nicht verfolgt; der Schlüssel geht nur an api.figma.com")]
    public async Task Sec_figma_redirect()
    {
        var fake = new Redirecting(); MainWindow.FigmaHandlerOverride = fake;
        try
        {
            var (w, _, _) = Ui.Open();
            await w.WebDesignImportFigma("https://www.figma.com/design/AbCdEf123456/X", "figd_schluessel-1234567890abcdef");
            Assert.Contains("umgeleitet", w.WebDesignImportNote);
            Assert.Equal(["api.figma.com"], fake.Calls);
        }
        finally { MainWindow.FigmaHandlerOverride = null; }
    }

    [AvaloniaFact(DisplayName = "A4: Mod prüfen zeigt Beschreibung, Rechte und Lizenz; erst „Ja“ installiert; Vorlage einfügen")]
    public void A4_mods_pane()
    {
        var src = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "trendo-mod-ui-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            File.WriteAllText(Path.Combine(src, WebDesignMods.ManifestName), """{"id":"test.mod","name":"Testmod","version":"1.0.0","beschreibung":"Eine Vorlage zum Ausprobieren.","lizenz":"MIT","rechte":["vorlagen"]}""");
            Directory.CreateDirectory(Path.Combine(src, "vorlagen"));
            File.WriteAllText(Path.Combine(src, "vorlagen", "a.json"), """{"name":"Hinweis","inhalt":[{"type":"Text","props":{"id":"x","text":"Aus der Mod","align":"left"}}]}""");
            var (w, _, _) = Ui.Open();
            w.WebDesignMessage("{\"art\":\"speichern\",\"daten\":" + Page + "}"); Pump();
            w.Show("dev"); Pump();
            w.Find<TextBox>("web-mod-quelle").Text = src;
            w.Find<Button>("web-mod-pruefen").Press(); Pump();
            Assert.Contains(w.GetLogicalTexts(), t => t.Contains("Lizenz MIT"));
            Assert.Contains(w.GetLogicalTexts(), t => t.Contains("bringt Vorlagen"));
            Assert.Empty(WebDesignMods.Installed(w.WebDesignDir)); // nur geprüft
            w.Find<Button>("web-mod-ja").Press(); Pump();
            Assert.Single(WebDesignMods.Installed(w.WebDesignDir));
            w.Find<Button>("web-mod-vorlage-0-0").Press(); Pump();
            Assert.True(WebDesignStates.Read(w.WebDesignDir).Contains("Aus der Mod"), w.WebDesignModNote);
            Assert.True(File.Exists(Path.Combine(src, WebDesignMods.ManifestName))); // der Mod-Ordner der Person bleibt
        }
        finally { Directory.Delete(src, true); }
    }
}

/// <summary>K7 und Import-Sicherheit im echten Browser.</summary>
public class WebDesignStufe3Qa_Browser
{
    static string Editor() => Path.Combine(WebDesignKi_Ui.Repo(), "src", "Trendo.App", "Assets", "webdesign");
    static string Anon() => "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9." + Convert.ToBase64String(Encoding.UTF8.GetBytes("""{"iss":"supabase","ref":"abcdefgh","role":"anon"}""")).TrimEnd('=').Replace('+', '-').Replace('/', '_') + ".c2lnbmF0dXJlX2Zha2VfMTIzNDU2";

    [Fact(DisplayName = "K7 in echtem Chrome: lokal anmelden, Eintrag speichern, nach dem Neuladen noch da, „nur nach Anmeldung“ sperrt, Supabase mit öffentlichem Schlüssel (übersprungen ohne Chrome/Node)")]
    public void Real_browser_web_app()
    {
        if (WebsitePreview.FindBrowser() is not { } chrome || Cli.Find("node") is not { } node) { Assert.Skip("Übersprungen – Chrome/Edge oder Node.js fehlt auf diesem PC."); return; }
        var d = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "trendo-web3-k7-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            Assert.Null(WebDesign.Save(d, """{"root":{"props":{"title":"Gästebuch","description":"Schreib uns"}},"content":[{"type":"Anmeldung","props":{"id":"an","titel":"Anmelden"}},{"type":"Datenliste","props":{"id":"dl","titel":"Einträge","tabelle":"gaeste","felder":[{"name":"name","beschriftung":"Name","art":"text","pflicht":"ja"},{"name":"nachricht","beschriftung":"Nachricht","art":"langtext","pflicht":"ja"}],"knopf":"Speichern","nurAngemeldet":"ja","liste":"alle"}}],"zones":{}}"""));
            Assert.Null(WebDesignData.SetSupabase(d, "https://meinprojekt.supabase.co", Anon()));
            var r = Trendo.Core.WebDesignExport.ExportSite(d, Editor()); Assert.Null(r.Error);
            var run = Cli.Run(node, [Path.Combine(WebDesignKi_Ui.Repo(), "tools", "webdesign-editor", "pruefe-daten.mjs"), chrome, r.Dir!], timeoutMs: 180_000);
            Assert.True(run.Ok, run.Out + run.Err);
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "Sicherheit 3: Einlesen von HTML lädt nichts aus dem Netz, auch kein @import in CSS (übersprungen ohne Chrome/Edge)")]
    public async Task Sec_parse_no_network()
    {
        if (WebsitePreview.FindBrowser() is null) { Assert.Skip("Übersprungen – Chrome oder Edge fehlt auf diesem PC."); return; }
        var hits = new List<string>();
        var listener = new HttpListener(); var port = WebsitePreview.FreePort(); listener.Prefixes.Add($"http://127.0.0.1:{port}/"); listener.Start();
        var loop = Task.Run(async () => { try { while (true) { var c = await listener.GetContextAsync(); lock (hits) hits.Add(c.Request.Url!.AbsolutePath); c.Response.StatusCode = 200; c.Response.Close(); } } catch (Exception e) when (e is HttpListenerException or ObjectDisposedException) { } });
        try
        {
            var html = $"<style>@import url(http://127.0.0.1:{port}/css-import); body{{background:url(http://127.0.0.1:{port}/bg)}}</style><p>Text</p><img src=\"http://127.0.0.1:{port}/img\" onerror=\"fetch('http://127.0.0.1:{port}/js')\"><link rel=\"stylesheet\" href=\"http://127.0.0.1:{port}/link\">";
            var (json, err) = await WebsitePreview.ParseHtmlAsync(Editor(), html);
            Assert.Null(err); Assert.Contains("Text", json);
            await Task.Delay(500);
            lock (hits) Assert.Empty(hits);
        }
        finally { listener.Stop(); listener.Close(); await loop; }
    }
}
