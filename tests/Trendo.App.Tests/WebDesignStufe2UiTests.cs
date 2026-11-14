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

    [AvaloniaFact(DisplayName = "W6: Kontrast wird schon beim Wählen gemeldet; Übernehmen gilt fürs Projekt, eine bestehende Variante bleibt unverändert")]
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
        w.Find<TextBox>("web-tok-paper").Text = "#ffffff"; w.Find<TextBox>("web-tok-ink").Text = "#eeeeee"; Pump(); // gültig, aber Text #eeeeee zu schwach (Live-Test 13.11.2026: wurde trotzdem übernommen)
        w.Find<Button>("web-tok-apply").Press(); Pump();
        Assert.Contains("Kontrast reicht nicht", w.WebDesignNote); Assert.Null(WebDesignTokens.Load(w.WebDesignDir));
        Assert.Contains("Kontrast reicht nicht", w.Find<TextBlock>("web-tok-note").Text); // sichtbar direkt am Knopf
        w.Find<TextBox>("web-tok-ink").Text = "#123456"; w.Find<TextBox>("web-tok-accent").Text = "#7A1F2B";
        w.Find<CheckBox>("web-tok-dunkel").IsChecked = true; w.Find<TextBox>("web-tok-inkDunkel").Text = "#202020"; Pump();
        Assert.Contains("(dunkel)", w.Find<TextBlock>("web-tok-check").Text); // dunkler Satz wird mitgeprüft
        w.Find<TextBox>("web-tok-inkDunkel").Text = "#f2f2f2"; Pump();
        Assert.StartsWith("✓", w.Find<TextBlock>("web-tok-check").Text);
        w.Find<Button>("web-tok-apply").Press(); Pump();
        Assert.Equal("#7A1F2B", WebDesignTokens.Load(w.WebDesignDir)!["accent"]!.ToString());
        var props = JsonNode.Parse(WebDesignStates.Read(w.WebDesignDir))!["root"]!["props"]!;
        Assert.Equal(("#7A1F2B", "#123456", true), (props["accent"]!.ToString(), props["ink"]!.ToString(), props["dunkel"]!.GetValue<bool>()));
        var variant = WebDesignStates.Variants(w.WebDesignDir).Single().Id;
        Assert.DoesNotContain("7A1F2B", WebDesignStates.Read(w.WebDesignDir, variant)); // K5: Variante ändert sich nicht mit
    }

    [AvaloniaFact(DisplayName = "W5: Auswahl zur Komponente machen, einfügen, als Vorlage speichern und in ein anderes Projekt holen")]
    public void Components_pane()
    {
        var templates = Path.Combine(Path.GetTempPath(), "trendo-web2-vorlagen-" + Guid.NewGuid().ToString("N"));
        MainWindow.WebDesignTemplatesOverride = templates;
        try
        {
            var (w, _, _) = Ui.Open();
            w.WebDesignMessage("""{"art":"speichern","daten":{"root":{"props":{"title":"Seite"}},"content":[{"type":"Ueberschrift","props":{"id":"u","text":"Paket","level":"h2"}},{"type":"Text","props":{"id":"t","text":"Preis","align":"left"}}],"zones":{}}}"""); Pump();
            w.Show("dev"); Pump();
            Assert.False(w.Find<Button>("web-komp-erstellen").IsEnabled); // ohne Auswahl nicht möglich
            w.WebDesignMessage("""{"art":"auswahl","daten":{"ids":["u","t"]}}"""); Pump();
            w.Find<TextBox>("web-komp-name").Text = "Preiskarte";
            w.Find<Button>("web-komp-erstellen").Press(); Pump();
            var def = WebDesignComponents.Load(w.WebDesignDir).Single();
            Assert.Equal("Preiskarte", def.Name); Assert.Empty(w.WebDesignSelection);
            w.Find<Button>("web-komp-einfuegen-0").Press(); Pump();
            Assert.Equal(2, JsonNode.Parse(WebDesign.Load(w.WebDesignDir))!["content"]!.AsArray().Count(b => b!["type"]!.ToString() == "Komponente"));
            Assert.Contains(w.GetLogicalTexts(), t => t.Contains("Preiskarte · 2 Baustein(e) · 2 Verwendung(en)"));
            w.Find<Button>("web-komp-vorlage-0").Press(); Pump();
            Assert.Single(WebDesignComponents.Templates(templates));

            var (w2, _, _) = Ui.Open(); // anderes Projekt
            w2.Show("dev"); Pump();
            w2.Find<Button>("web-vorlage-0").Press(); Pump();
            var expanded = WebDesignComponents.Expand(JsonNode.Parse(WebDesignStates.Read(w2.WebDesignDir))!.AsObject());
            Assert.Contains("Paket", expanded["content"]!.ToJsonString());
        }
        finally { MainWindow.WebDesignTemplatesOverride = null; if (Directory.Exists(templates)) Directory.Delete(templates, true); }
    }

    [AvaloniaFact(DisplayName = "A8: Ohne Wahl wird nichts gebaut; abgelehnte Richtungen werden gemeldet; die gewählte Richtung prägt die Seite")]
    public async Task Directions_first()
    {
        var (w, _, _) = Ui.Open(); var builds = 0; var answer = "kaputt";
        const string dirs = """{"richtungen":[{"name":"Ruhig","stimmung":"Klar.","schrift":"klar","akzent":"#1f4e79","text":"#1d1d1f","hintergrund":"#ffffff","aufbau":["Hero","Text"]},{"name":"Warm","stimmung":"Herzlich.","schrift":"serif","akzent":"#7a1f2b","text":"#2b1d14","hintergrund":"#fbf6ee","aufbau":["Hero","Galerie"]}]}""";
        MainWindow.WebDesignKiOverride = (s, _) => s.Contains("Gestaltungsrichtungen") ? Task.FromResult(answer) : Task.FromResult(new string('x', builds++ * 0) + """{"aktionen":[{"aktion":"seite_anlegen","titel":"Café"},{"aktion":"baustein_einfuegen","typ":"Hero"}]}""");
        try
        {
            w.Show("dev"); Pump();
            await w.WebDesignAiDirections("Café"); Pump();
            Assert.Contains("abgelehnt", w.WebDesignNote); Assert.Null(w.WebDesignDirectionsShown);
            answer = dirs;
            await w.WebDesignAiDirections("Café"); Pump();
            Assert.Equal(2, w.WebDesignDirectionsShown!.Count); Assert.NotNull(w.Find<WrapPanel>("web-ki-richtungen"));
            Assert.Equal(0, builds); Assert.Null(w.WebDesignPending); Assert.False(File.Exists(Path.Combine(w.WebDesignDir, WebDesign.PageFile)));
            await w.WebDesignChooseDirection(1); Pump();
            Assert.Equal(1, builds);
            var p = JsonNode.Parse(w.WebDesignPending!.Json!)!["root"]!["props"]!;
            Assert.Equal(("serif", "#7a1f2b"), (p["font"]!.ToString(), p["accent"]!.ToString()));
            Assert.Null(w.WebDesignDirectionsShown);
        }
        finally { MainWindow.WebDesignKiOverride = null; }
    }

    [AvaloniaFact(DisplayName = "Z7: Medien ins Projekt holen – Bilder werden verkleinert, Lizenz fehlt wird angezeigt, Suche, Schrift von Fontsource mit Herkunft und Lizenz")]
    public async Task Media_pane()
    {
        var (w, _, root) = Ui.Open();
        var big = Path.Combine(root, "gross.png");
        using (var bmp = new SkiaSharp.SKBitmap(3000, 1500)) using (var img = SkiaSharp.SKImage.FromBitmap(bmp)) using (var data = img.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100)) using (var fs = File.Create(big)) data.SaveTo(fs);
        w.Show("dev"); Pump();
        w.WebDesignAssetImport(big, "bild", "Großes Bild", "eigenes Foto", ""); Pump();
        var a = WebDesignAssets.Load(w.WebDesignDir).Single();
        using (var stored = SkiaSharp.SKBitmap.Decode(Path.Combine(w.WebDesignDir, a.Datei))) Assert.Equal((1920, 960), (stored.Width, stored.Height)); // für das Web verkleinert
        Assert.Contains("Lizenz eintragen", w.WebDesignNote);
        Assert.Contains(w.GetLogicalTexts(), t => t.Contains("⚠ Lizenz fehlt"));
        w.Find<TextBox>("web-medium-suche").Text = "nichtda"; Pump(); w.Find<Button>("web-medium-suchen").Press(); Pump();
        Assert.DoesNotContain(w.GetLogicalTexts(), t => t.StartsWith("bild: Großes Bild", StringComparison.Ordinal));
        Assert.Contains("medien", WebDesignStates.Read(w.WebDesignDir)); // Liste für die Auswahl im Editor

        WebDesignFontsourceHandler = new FakeFontsource();
        try
        {
            await w.WebDesignFontFetch("inter"); Pump();
            Assert.True(WebDesignAssets.Load(w.WebDesignDir).Any(x => x.Art == "schrift"), w.WebDesignNote);
            var font = WebDesignAssets.Load(w.WebDesignDir).Single(x => x.Art == "schrift");
            Assert.Equal(("Inter", "OFL-1.1", "Fontsource"), (font.Familie, font.Lizenz, font.Herkunft));
            w.Find<TextBox>("web-medium-suche").Text = ""; Pump(); w.Show("dev"); Pump();
            w.Find<Button>("web-medium-schrift-1").Press(); Pump(); // zweites Medium in der Liste
            Assert.Equal("eigene:Inter", JsonNode.Parse(WebDesign.Load(w.WebDesignDir))!["root"]!["props"]!["font"]!.ToString());
            await w.WebDesignFontFetch("../böse"); Pump();
            Assert.Contains("Kennung", w.WebDesignNote);
        }
        finally { WebDesignFontsourceHandler = null; }
    }

    static HttpMessageHandler? WebDesignFontsourceHandler { set => MainWindow.WebDesignFontsourceHandler = value; }

    sealed class FakeFontsource : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        {
            var u = r.RequestUri!.ToString();
            if (u == "https://api.fontsource.org/v1/fonts/inter")
                return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent("""{"id":"inter","family":"Inter","license":"OFL-1.1","defSubset":"latin","variants":{"400":{"normal":{"latin":{"url":{"woff2":"https://cdn.jsdelivr.net/fontsource/fonts/inter@5.3.0/latin-400-normal.woff2"}}}}}}""") });
            if (u.StartsWith("https://cdn.jsdelivr.net/fontsource/", StringComparison.Ordinal))
                return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new ByteArrayContent([0x77, 0x4F, 0x46, 0x32]) });
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
        }
    }

    sealed class BenchKi : IKiBackend
    {
        public string Name => "Ollama"; public string BaseUrl => "http://127.0.0.1:11434";
        public Task<bool> IsUpAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<IReadOnlyList<string>> ModelsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>(["qwen3:4b", "qwen2.5:0.5b"]);
        public Task<string> ChatAsync(string model, IReadOnlyList<KiMessage> messages, CancellationToken ct = default)
        {
            if (model == "qwen2.5:0.5b") return Task.FromResult("<html>fertig</html>");
            var q = messages[^1].Content;
            return Task.FromResult(q.Contains("Bäckerei") ? """{"aktionen":[{"aktion":"seite_anlegen","titel":"B"},{"aktion":"baustein_einfuegen","typ":"Hero"},{"aktion":"baustein_einfuegen","typ":"Text"},{"aktion":"baustein_einfuegen","typ":"Formular"}]}"""
                : q.Contains("#weg") ? """{"aktionen":[{"aktion":"element_aendern","id":"held","props":{"href":"#"}}]}""" : """{"aktionen":[{"aktion":"element_aendern","id":"t1","props":{"text":"Wir freuen uns auf dich."}}]}""");
        }
    }

    [AvaloniaFact(DisplayName = "Z6: Prüflauf vergleicht die lokalen Modelle, speichert das Ergebnis und empfiehlt nur das bestandene")]
    public async Task Model_bench()
    {
        var (w, _, _) = Ui.Open();
        MainWindow.WebDesignBenchBackendOverride = new BenchKi(); MainWindow.WebDesignHardwareOverride = new HardwareInfo(8L << 30, null, 0);
        try
        {
            w.Show("dev"); Pump();
            Assert.Contains("Noch kein Prüflauf", w.Find<TextBlock>("web-modell-empfehlung").Text);
            await w.WebDesignBenchRun(); Pump();
            var results = WebDesignModelBench.Load(Path.Combine(w.WebDesignDir, "..", ".."));
            Assert.Contains("qwen3:4b", w.Find<TextBlock>("web-modell-empfehlung").Text);
            Assert.Contains(w.GetLogicalTexts(), t => t.StartsWith("✗ qwen2.5:0.5b: 0 von 3"));
            Assert.Contains(w.GetLogicalTexts(), t => t.StartsWith("✓ qwen3:4b: 3 von 3"));
        }
        finally { MainWindow.WebDesignBenchBackendOverride = null; MainWindow.WebDesignHardwareOverride = null; }
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

    /// <summary>Kleinstes gültiges PNG (1 × 1 Pixel), damit Galerie und Hero echte Dateien haben.</summary>
    static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");
    static string? AxeFile() { var f = Path.Combine(Path.GetTempPath(), "trendo-axe", "node_modules", "axe-core", "axe.min.js"); return File.Exists(f) ? f : null; }

    [Fact(DisplayName = "W5, Z1, W4, Z2 in echtem Chrome: Komponenten mit Überschreibung, Profi-Bausteine folgen den Gestaltungswerten, Tastatur und axe-core je Baustein, Bewegung mit „weniger Bewegung“ und Ersatzweg, Zeitleiste im Export (übersprungen ohne Chrome, Node oder axe-core in %TEMP%\\trendo-axe)")]
    public void Real_browser_teil2()
    {
        if (WebsitePreview.FindBrowser() is not { } chrome || Cli.Find("node") is not { } node) { Assert.Skip("Übersprungen – Chrome/Edge oder Node.js fehlt auf diesem PC."); return; }
        if (AxeFile() is not { } axe) { Assert.Skip("Übersprungen – axe-core ist nicht in %TEMP%\\trendo-axe eingerichtet (npm install axe-core@4.14.0 --prefix %TEMP%\\trendo-axe)."); return; }
        var editor = Path.Combine(Repo(), "src", "Trendo.App", "Assets", "webdesign");
        var d = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "trendo-web2-teil2-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(d, "bilder"));
            foreach (var n in new[] { "held", "a", "b", "c" }) File.WriteAllBytes(Path.Combine(d, "bilder", n + ".png"), Png);
            var r = WebDesignActions.Apply(WebDesign.Empty(), File.ReadAllText(Path.Combine(Repo(), "tests", "Trendo.App.Tests", "Daten", "web-stufe2-teil2.json")));
            Assert.Null(r.Error);
            Assert.Null(WebDesignStates.Write(d, r.Json!));
            Assert.DoesNotContain(WebDesignCheck.CheckData(WebDesignStates.Read(d), d), f => f.Level == "Fehler"); // die Prüfseite selbst ist sauber
            var (outDir, err) = WebDesign.Export(d, editor); Assert.Null(err);
            Assert.True(File.Exists(Path.Combine(outDir!, "bilder", "a.png"))); Assert.True(File.Exists(Path.Combine(outDir!, "bilder", "held.png")));
            Assert.Contains("lucide-react", File.ReadAllText(Path.Combine(outDir!, "assets", "LIZENZEN.txt"))); // Lizenzhinweis reist mit
            var page = Path.Combine(d, "seite.json"); File.WriteAllText(page, WebDesignStates.Read(d));
            var run = Cli.Run(node, [Path.Combine(Repo(), "tools", "webdesign-editor", "pruefe-teil2.mjs"), chrome, editor, page, outDir!, axe], timeoutMs: 180_000);
            Assert.True(run.Ok, run.Out + run.Err);
            Assert.Contains("\"problems\":[]", run.Out);
        }
        finally { try { Directory.Delete(d, true); } catch (IOException) { } }
    }

    [Fact(DisplayName = "Z6 PC-Lauf: Prüflauf mit echten lokalen Modellen über Ollama, Protokoll unter reports/ (nur mit TRENDO_PC_LAUF=1)")]
    public async Task Z6_pc_run()
    {
        if (Environment.GetEnvironmentVariable("TRENDO_PC_LAUF") != "1") { Assert.Skip("Übersprungen – PC-Lauf nur mit TRENDO_PC_LAUF=1 (braucht Ollama und Zeit)."); return; }
        var ollama = new OllamaBackend();
        Assert.True(await ollama.IsUpAsync(), "Ollama läuft nicht.");
        var installed = await ollama.ModelsAsync();
        var models = (Environment.GetEnvironmentVariable("TRENDO_PC_MODELLE") ?? "qwen2.5:3b,qwen2.5-coder:7b,mistral:7b").Split(',').Where(installed.Contains).ToList();
        Assert.True(models.Count >= 2, "Mindestens zwei Modelle nötig, vorhanden: " + string.Join(", ", installed));
        var results = new List<WebDesignModelBench.ModelResult>();
        foreach (var m in models) results.Add(await WebDesignModelBench.RunAsync(ollama, m));
        var hw = (HardwareInfo)typeof(MainWindow).Assembly.GetType("Trendo.App.HardwareProbe")!.GetMethod("Read")!.Invoke(null, null)!;
        var rec = WebDesignModelBench.Recommend(hw, results);
        var log = System.Text.Json.JsonSerializer.Serialize(new { Datum = DateTimeOffset.Now, Hardware = new { RamGb = hw.RamBytes >> 30, hw.Gpu, VramGb = hw.VramBytes >> 30 }, Ergebnisse = results, Empfehlung = rec.Model, Hinweis = rec.Note }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        var dir = Path.Combine(Repo(), "reports"); Directory.CreateDirectory(dir);
        var raw = Path.Combine(dir, $"web-modell-pruefung-{DateTime.Now:yyyy-MM-dd}.json");
        File.WriteAllText(raw, log);
        var sha = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(raw))).ToLowerInvariant();
        var md = new System.Text.StringBuilder($"# Z6 – Prüflauf lokaler Modelle für Webdesign\n\nDatum: {DateTime.Now:dd.MM.yyyy HH:mm}. Rohdaten: `{Path.GetFileName(raw)}`, SHA-256 `{sha}`.\n\n| Modell | Bestanden | Zeit | Befunde |\n|---|---|---|---|\n");
        foreach (var r in results) md.Append($"| {r.Model} | {r.Tasks.Count(t => t.Passed)} von {r.Tasks.Count} | {r.Seconds:0} s | {string.Join("; ", r.Tasks.Where(t => !t.Passed).Select(t => t.Task + ": " + t.Note.Replace("|", "/")))} |\n");
        md.Append($"\nEmpfehlung: {rec.Note}\n");
        File.WriteAllText(Path.ChangeExtension(raw, ".md"), md.ToString());
        Assert.Equal(models.Count, results.Count);
    }

    [Fact(DisplayName = "Z2: Editor und Export (bewegung.js) berechnen Schlüsselbilder gleich wie C# (übersprungen ohne Node)")]
    public void Same_timeline_values()
    {
        if (Cli.Find("node") is not { } node) { Assert.Skip("Übersprungen – Node.js fehlt."); return; }
        var points = """[{"eigenschaft":"y","zeit":0.5,"wert":-10},{"eigenschaft":"y","zeit":2,"wert":30},{"eigenschaft":"deckkraft","zeit":1,"wert":0.4},{"eigenschaft":"y","zeit":1,"wert":0}]""";
        var times = new[] { 0.0, 0.25, 0.5, 0.75, 1, 1.5, 2, 3 };
        var module = new Uri(Path.Combine(Repo(), "tools", "webdesign-editor", "src", "bewegung.js")).AbsoluteUri;
        var r = Cli.Run(node, ["--input-type=module", "-e", $"import {{ wertZurZeit }} from '{module}'; const p = {points}; const out = []; for (const e of ['x','y','skalierung','drehung','deckkraft']) for (const t of [{string.Join(",", times.Select(t => t.ToString(System.Globalization.CultureInfo.InvariantCulture)))}]) out.push(wertZurZeit(p, e, t)); process.stdout.write(JSON.stringify(out));"], timeoutMs: 30_000);
        Assert.True(r.Ok, r.Err);
        var js = JsonNode.Parse(r.Out)!.AsArray().Select(x => x!.GetValue<double>()).ToList();
        var arr = JsonNode.Parse(points)!.AsArray();
        var cs = new[] { "x", "y", "skalierung", "drehung", "deckkraft" }.SelectMany(e => times.Select(t => WebDesignMotion.ValueAt(arr, e, t))).ToList();
        Assert.Equal(cs.Count, js.Count);
        for (var i = 0; i < cs.Count; i++) Assert.Equal(cs[i], js[i], 9);
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
