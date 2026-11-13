using System.Text.Json.Nodes;
using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0621: Regressionen aus der QA zu Teil 2 und Teil 3 (A2 Technik-Bibliothek, A8 Gestaltungsrichtung, Z6 lokales Modell, Z7 Medien).</summary>
public class WebDesignTeil3Tests
{
    const string Page = """{"root":{"props":{"title":"Seite","description":"Kurz"}},"content":[{"type":"Ueberschrift","props":{"id":"u","text":"Paket","level":"h2"}},{"type":"Text","props":{"id":"t","text":"Preis","align":"left"}},{"type":"Bereich","props":{"id":"b","tone":"hell","content":[]}}],"zones":{}}""";
    static string Plan(params string[] actions) => "{\"aktionen\":[" + string.Join(",", actions) + "]}";
    static string Ok(string page, params string[] actions) { var r = WebDesignActions.Apply(page, Plan(actions)); Assert.True(r.Error is null, r.Error); return r.Json!; }
    static string Dir() => Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "trendo-web3-" + Guid.NewGuid().ToString("N"))).FullName;

    // ---------- Regressionen aus der QA zu Teil 2 ----------
    [Fact(DisplayName = "QA Teil 2: Komponente mit zu vielen Bausteinen wird abgelehnt statt still zu verschwinden; Bausteine müssen direkt hintereinander liegen")]
    public void Component_limits()
    {
        var many = new JsonArray(Enumerable.Range(0, 61).Select(i => (JsonNode)new JsonObject { ["type"] = "Text", ["props"] = new JsonObject { ["id"] = "x" + i, ["text"] = "T", ["align"] = "left" } }).ToArray());
        var page = new JsonObject { ["root"] = new JsonObject { ["props"] = new JsonObject() }, ["content"] = many, ["zones"] = new JsonObject() }.ToJsonString();
        var ids = new JsonArray(Enumerable.Range(0, 61).Select(i => (JsonNode)("x" + i)).ToArray());
        Assert.Contains("höchstens 60", WebDesignActions.Apply(page, new JsonObject { ["aktionen"] = new JsonArray(new JsonObject { ["aktion"] = "komponente_erstellen", ["name"] = "Groß", ["ids"] = ids }) }).Error);
        Assert.Contains("direkt hintereinander", WebDesignActions.Apply(Page, Plan("""{"aktion":"komponente_erstellen","name":"X","ids":["u","b"]}""")).Error);
    }

    [Fact(DisplayName = "QA Teil 2: Keine Verwendung in einer Komponente, auch nicht in einem Bereich; Prüfung sieht in Bereiche eingesetzter Bausteine")]
    public void No_nested_usage()
    {
        var j = Ok(Page, """{"aktion":"komponente_definieren","name":"Bild","bausteine":[{"typ":"Bild","id":"f","props":{"src":"","alt":""}}]}""",
            """{"aktion":"komponente_einfuegen","komponente":"K-1","id":"inner","in":"b"}""");
        Assert.Contains("auch nicht in einem Bereich", WebDesignActions.Apply(j, Plan("""{"aktion":"komponente_erstellen","name":"Aussen","ids":["b"]}""")).Error);
        var withSection = Ok(Page, """{"aktion":"komponente_definieren","name":"Mit Bereich","bausteine":[{"typ":"Bereich","id":"sec"}]}""", """{"aktion":"komponente_einfuegen","komponente":"K-1","id":"v"}""");
        var doc = JsonNode.Parse(withSection)!.AsObject();
        doc["root"]!["props"]!["komponenten"]![0]!["bausteine"]![0]!["props"]!["content"] = new JsonArray(new JsonObject { ["type"] = "Bild", ["props"] = new JsonObject { ["id"] = "pic", ["src"] = "fehlt.png", ["alt"] = "" } });
        Assert.Contains(WebDesignCheck.CheckData(doc.ToJsonString(), Path.GetTempPath()), f => f.Code == "alt" && f.Text.Contains("pic")); // Bereiche in Komponenten werden mit geprüft
        var cyclic = """{"id":"A","name":"A","bausteine":[{"type":"Bereich","props":{"id":"s","content":[{"type":"Komponente","props":{"id":"z","komponente":"A"}}]}}]}""";
        Assert.Empty(WebDesignComponents.Read(JsonNode.Parse("[" + cyclic + "]"))); // Zyklen aus Dateien werden verworfen
    }

    [Fact(DisplayName = "QA Teil 2: Anpassungen je Ansicht wirken auch in Komponenten, je Verwendung")]
    public void Views_in_components()
    {
        var j = Ok(Page, """{"aktion":"anpassung_setzen","id":"t","ansicht":"Handy","werte":{"ausblenden":true}}""", """{"aktion":"komponente_erstellen","name":"Karte","ids":["u","t"],"id":"k1"}""",
            """{"aktion":"komponente_einfuegen","komponente":"K-1","id":"k2"}""");
        var css = WebDesignViews.Css(JsonNode.Parse(j)!.AsObject());
        Assert.Contains("[data-b=\"k1--t\"]{display:none}", css); Assert.Contains("[data-b=\"k2--t\"]{display:none}", css);
    }

    [Fact(DisplayName = "QA Teil 2: Vorlagen behalten Listen und Bewegung und überschreiben sich nicht still")]
    public void Templates_keep_content()
    {
        var d = Dir(); try
        {
            var j = Ok(WebDesign.Empty(), """{"aktion":"baustein_einfuegen","typ":"Navigation","id":"n","props":{"links":[{"label":"Eigen","href":"eigen.html"}]}}""",
                """{"aktion":"bewegung_setzen","id":"n","erscheinen":"zoomen"}""", """{"aktion":"komponente_erstellen","name":"Kopf","ids":["n"]}""");
            var def = WebDesignComponents.Read(JsonNode.Parse(j)!["root"]!["props"]!["komponenten"]).Single();
            var dir = Path.Combine(d, "v");
            var (f1, r1) = WebDesignComponents.SaveTemplate(dir, def);
            var (f2, r2) = WebDesignComponents.SaveTemplate(dir, def with { Name = "kopf" }); // anderer Name, gleiche Datei-Kennung
            var (f3, r3) = WebDesignComponents.SaveTemplate(dir, def);
            Assert.False(r1); Assert.False(r2); Assert.NotEqual(f1, f2); Assert.True(r3); Assert.Equal(f1, f3);
            Assert.Equal(2, WebDesignComponents.Templates(dir).Count);
            var t = WebDesignComponents.Templates(dir).First(x => x.Name == "Kopf");
            var other = Ok(WebDesign.Empty(), new JsonObject { ["aktion"] = "komponente_definieren", ["name"] = "Kopf", ["komponente"] = "V-1", ["bausteine"] = WebDesignComponents.TemplateBlocks(t.Blocks) }.ToJsonString());
            var block = WebDesignComponents.Read(JsonNode.Parse(other)!["root"]!["props"]!["komponenten"]).Single().Blocks[0]!;
            Assert.Equal("Eigen", block["props"]!["links"]![0]!["label"]!.ToString()); Assert.Equal("zoomen", block["props"]!["bewegung"]!["erscheinen"]!.ToString());
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "QA Teil 2: Link-, Bild- und Mail-Felder aller Bausteine werden auch auf dem Editor-Weg entschärft, auch in Listen")]
    public void Sanitize_all_fields()
    {
        var raw = """{"root":{"props":{}},"content":[{"type":"Hero","props":{"id":"h","knopfZiel":"javascript:alert(1)","knopf2Ziel":"data:text/html,x","bild":"../geheim.png"}},{"type":"Fusszeile","props":{"id":"f","impressum":"javascript:alert(2)","datenschutz":"datenschutz.html"}},{"type":"Preise","props":{"id":"p","tarife":[{"name":"A","ziel":"javascript:alert(3)"}]}},{"type":"Formular","props":{"id":"m","empfaenger":"a@b.de?bcc=evil@x.org"}}],"zones":{}}""";
        var ok = WebDesign.Validate(raw).Json!;
        Assert.DoesNotContain("javascript", ok); Assert.DoesNotContain("data:text", ok); Assert.DoesNotContain("geheim", ok); Assert.DoesNotContain("evil", ok);
        Assert.Contains("datenschutz.html", ok); // erlaubte Ziele bleiben
    }

    // ---------- A2 ----------
    [Fact(DisplayName = "A2: Jeder Eintrag hat Stand, Quelle und wo nötig einen Ersatzweg; die geforderten Techniken sind da")]
    public void A2_directory()
    {
        Assert.All(WebDesignTechniques.All, t =>
        {
            Assert.False(string.IsNullOrWhiteSpace(t.Stand)); Assert.StartsWith("https://developer.mozilla.org/", t.Quelle); Assert.False(string.IsNullOrWhiteSpace(t.Beispiel));
            Assert.Contains(t.Nutzung, new[] { "direkt", "mit Ersatzweg" });
            if (t.Nutzung == "mit Ersatzweg" || !t.Stand.Contains("weitgehend")) Assert.False(string.IsNullOrWhiteSpace(t.Ersatzweg), t.Id);
        });
        foreach (var id in new[] { "container-queries", "subgrid", "view-transitions", "has", "clamp" }) Assert.Equal("direkt", WebDesignTechniques.All.Single(t => t.Id == id).Nutzung);
        foreach (var id in new[] { "scroll-animationen", "anchor-positioning", "seitenuebergaenge" }) Assert.Equal("mit Ersatzweg", WebDesignTechniques.All.Single(t => t.Id == id).Nutzung);
    }

    [Fact(DisplayName = "A2: Die KI bekommt passende Techniken aus dem Verzeichnis, gekürzt auf ganze Einträge")]
    public void A2_prompt()
    {
        Assert.Equal(["scroll-animationen"], WebDesignTechniques.ForTask("Bewegung beim Scrollen").Select(t => t.Id).Where(id => id == "scroll-animationen"));
        Assert.Empty(WebDesignTechniques.ForTask("Bäckerei"));
        var text = WebDesignTechniques.PromptText(WebDesignTechniques.All, 300);
        Assert.True(text.Length <= 300); Assert.EndsWith(")", text.Split('\n')[0].Split(':')[0]);
    }

    // ---------- A8 ----------
    const string Directions = """{"richtungen":[{"name":"Ruhig","stimmung":"Klar und hell.","schrift":"klar","akzent":"#1f4e79","text":"#1d1d1f","hintergrund":"#ffffff","aufbau":["Hero","Text","Fusszeile"]},{"name":"Warm","stimmung":"Handwerklich und herzlich.","schrift":"serif","akzent":"#7a1f2b","text":"#2b1d14","hintergrund":"#fbf6ee","aufbau":["Navigation","Hero","Galerie","Formular"]}]}""";

    [Fact(DisplayName = "A8: Zwei bis drei gültige Richtungen werden gelesen; sie unterscheiden sich in Schrift oder Farbe")]
    public void A8_parse()
    {
        var (list, err) = WebDesignDirections.Parse("Hier sind Vorschläge: " + Directions);
        Assert.Null(err); Assert.Equal(2, list!.Count);
        Assert.True(list[0].Schrift != list[1].Schrift || !WebDesignDirections.Similar(list[0].Akzent, list[1].Akzent));
    }

    [Theory(DisplayName = "A8: Zu wenige, gleiche oder kontrastarme Richtungen werden abgelehnt")]
    [InlineData("""{"richtungen":[{"name":"A","stimmung":"x","schrift":"klar","akzent":"#1f4e79","text":"#1d1d1f","hintergrund":"#ffffff","aufbau":["Hero"]}]}""", "zwei oder drei")]
    [InlineData("""{"richtungen":[{"name":"A","stimmung":"x","schrift":"klar","akzent":"#1f4e79","text":"#1d1d1f","hintergrund":"#ffffff","aufbau":["Hero"]},{"name":"B","stimmung":"y","schrift":"klar","akzent":"#204f7a","text":"#111111","hintergrund":"#fafafa","aufbau":["Text"]}]}""", "unterscheiden sich")]
    [InlineData("""{"richtungen":[{"name":"A","stimmung":"x","schrift":"klar","akzent":"#1f4e79","text":"#eeeeee","hintergrund":"#ffffff","aufbau":["Hero"]},{"name":"B","stimmung":"y","schrift":"serif","akzent":"#7a1f2b","text":"#111111","hintergrund":"#ffffff","aufbau":["Text"]}]}""", "zu schwach")]
    [InlineData("""{"richtungen":[{"name":"A","stimmung":"x","schrift":"comic","akzent":"#1f4e79","text":"#111111","hintergrund":"#ffffff","aufbau":["Hero"]},{"name":"B","stimmung":"y","schrift":"serif","akzent":"#7a1f2b","text":"#111111","hintergrund":"#ffffff","aufbau":["Text"]}]}""", "klar, serif")]
    [InlineData("""{"richtungen":[{"name":"A","stimmung":"x","schrift":"klar","akzent":"#1f4e79","text":"#111111","hintergrund":"#ffffff","aufbau":["Skript"]},{"name":"B","stimmung":"y","schrift":"serif","akzent":"#7a1f2b","text":"#111111","hintergrund":"#ffffff","aufbau":["Text"]}]}""", "bekannten Bausteinen")]
    [InlineData("kein json", "keine Richtungen")]
    public void A8_rejects(string answer, string expected) => Assert.Contains(expected, WebDesignDirections.Parse(answer).Error);

    [Fact(DisplayName = "A8: Die gewählte Richtung wird fest eingesetzt (Schrift und Farben)")]
    public void A8_enforce()
    {
        var r = WebDesignDirections.Parse(Directions).List![1];
        var built = WebDesignActions.Apply(WebDesign.Empty(), Plan("""{"aktion":"seite_anlegen","titel":"T"}""", """{"aktion":"gestaltungswert_setzen","name":"accent","wert":"#00ff00"}"""));
        var p = JsonNode.Parse(WebDesignDirections.Enforce(built, r).Json!)!["root"]!["props"]!;
        Assert.Equal(("serif", "#7a1f2b", "#2b1d14", "#fbf6ee"), (p["font"]!.ToString(), p["accent"]!.ToString(), p["ink"]!.ToString(), p["paper"]!.ToString()));
        Assert.Contains("Warm", WebDesignDirections.BuildPrompt("Bäckerei", r));
    }

    // ---------- Z6 ----------
    [Fact(DisplayName = "Z6: Prüflauf bewertet gültige und ungültige Editor-Daten")]
    public void Z6_score()
    {
        var t = WebDesignModelBench.Tasks;
        Assert.False(WebDesignModelBench.Score(t[0], "<html>fertige Seite</html>").Passed);
        Assert.False(WebDesignModelBench.Score(t[0], """{"aktionen":[{"aktion":"seite_anlegen","titel":"B"},{"aktion":"baustein_einfuegen","typ":"Text"}]}""").Passed); // zu wenig, keine Hauptüberschrift
        Assert.True(WebDesignModelBench.Score(t[0], """{"aktionen":[{"aktion":"seite_anlegen","titel":"B"},{"aktion":"baustein_einfuegen","typ":"Kopfbereich"},{"aktion":"baustein_einfuegen","typ":"Text"},{"aktion":"baustein_einfuegen","typ":"Formular"}]}""").Passed);
        Assert.False(WebDesignModelBench.Score(t[1], """{"aktionen":[{"aktion":"element_aendern","id":"held","props":{"title":"x"}}]}""").Passed); // außerhalb der Auswahl
        Assert.True(WebDesignModelBench.Score(t[1], """{"aktionen":[{"aktion":"element_aendern","id":"t1","props":{"text":"Wir freuen uns jeden Tag auf dich."}}]}""").Passed);
        Assert.True(WebDesignModelBench.Score(t[2], """{"aktionen":[{"aktion":"element_aendern","id":"held","props":{"href":"#"}}]}""").Passed);
        Assert.False(WebDesignModelBench.Score(t[2], """{"aktionen":[{"aktion":"element_aendern","id":"t1","props":{"text":"x"}}]}""").Passed);
    }

    [Fact(DisplayName = "Z6: Empfehlung passt zur vorgegebenen Hardware und nennt nur, was den Prüflauf bestanden hat; ohne Eignung sagt Trendo es klar")]
    public void Z6_recommend()
    {
        static WebDesignModelBench.ModelResult R(string m, bool ok, double s) => new(m, [new("a", ok, "", s), new("b", true, "", s), new("c", true, "", s)], DateTimeOffset.Now);
        var small = new HardwareInfo(8L << 30, null, 0); var big = new HardwareInfo(32L << 30, "GPU", 12L << 30);
        Assert.DoesNotContain(WebDesignModelBench.Candidates(small), m => m.MinRamGb > 8);
        Assert.Contains(WebDesignModelBench.Candidates(big), m => m.Tag == "qwen2.5-coder:7b");
        var results = new[] { R("qwen2.5-coder:7b", true, 30), R("qwen2.5:0.5b", false, 5), R("qwen3:4b", true, 60) };
        Assert.Equal("qwen3:4b", WebDesignModelBench.Recommend(small, results).Model); // 7B passt nicht in 8 GB, 0.5B hat nicht bestanden
        Assert.Equal("qwen2.5-coder:7b", WebDesignModelBench.Recommend(big, results).Model); // schnellstes bestandenes
        Assert.Contains("Kein geprüftes Modell", WebDesignModelBench.Recommend(small, [R("qwen2.5:0.5b", false, 5)]).Note);
        Assert.Contains("Noch kein Prüflauf", WebDesignModelBench.Recommend(small, []).Note);
        Assert.Null(WebDesignModelBench.Recommend(new HardwareInfo(2L << 30, null, 0), results).Model);
        Assert.Contains("kein Modell", WebDesignModelBench.Recommend(new HardwareInfo(2L << 30, null, 0), results).Note);
    }

    // ---------- Z7 ----------
    static (string Dir, string File) Project(string name, byte[]? bytes = null)
    {
        var d = Dir(); var f = Path.Combine(d, "quelle"); Directory.CreateDirectory(f);
        var src = Path.Combine(f, name); File.WriteAllBytes(src, bytes ?? [1, 2, 3]);
        return (d, src);
    }

    [Fact(DisplayName = "Z7: Medium ohne Lizenzeintrag wird beim Export gemeldet; nicht verzeichnete Dateien als Hinweis")]
    public void Z7_license_reported()
    {
        var (d, src) = Project("laden.png"); try
        {
            var (a, err) = WebDesignAssets.Import(d, src, "bild", "Laden", "eigenes Foto", ""); Assert.Null(err);
            Assert.Equal("medien/bild/laden.png", a!.Datei);
            var j = Ok(Page, $$$"""{"aktion":"baustein_einfuegen","typ":"Bild","props":{"src":"{{{a.Datei}}}","alt":"Laden"}}""");
            Assert.Contains(WebDesignCheck.CheckData(j, d), f => f.Code == "license" && f.Level == "Fehler");
            Assert.Null(WebDesignAssets.Update(d, a.Datei, "eigenes Foto", "CC0-1.0", "", ""));
            Assert.DoesNotContain(WebDesignCheck.CheckData(j, d), f => f.Area == "Lizenzen");
            File.WriteAllBytes(Path.Combine(d, "frei.png"), [1]);
            var free = Ok(Page, """{"aktion":"baustein_einfuegen","typ":"Bild","props":{"src":"frei.png","alt":"x"}}""");
            Assert.Contains(WebDesignCheck.CheckData(free, d), f => f.Code == "license-unknown" && f.Level == "Hinweis");
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "Z7: CC-BY erscheint in der Namensnennung im Export; ohne Urheber wird es gemeldet")]
    public void Z7_attribution()
    {
        var (d, src) = Project("berg.jpg"); try
        {
            var (a, _) = WebDesignAssets.Import(d, src, "bild", "Berg im Nebel", "Wikimedia Commons", "CC-BY-4.0", "Erika Muster", "https://commons.wikimedia.org/wiki/File:Berg.jpg", "berg landschaft");
            var j = Ok(Page, $$$"""{"aktion":"baustein_einfuegen","typ":"Galerie","props":{"bilder":[{"src":"{{{a!.Datei}}}","alt":"Berg"}]}}""");
            WebDesign.Save(d, j);
            var (outDir, err) = WebDesign.Export(d, Path.Combine(Repo(), "src", "Trendo.App", "Assets", "webdesign")); Assert.Null(err);
            var credits = File.ReadAllText(Path.Combine(outDir!, "namensnennung.html"));
            Assert.Contains("„Berg im Nebel“ von Erika Muster, Lizenz CC-BY-4.0", credits); Assert.Contains("commons.wikimedia.org", credits);
            Assert.True(File.Exists(Path.Combine(outDir!, "medien", "bild", "berg.jpg")));
            WebDesignAssets.Update(d, a.Datei, "Wikimedia Commons", "CC-BY-4.0", "", "");
            Assert.Contains(WebDesignCheck.CheckData(j, d), f => f.Code == "license-author");
            Assert.Single(WebDesignAssets.Search(d, "landschaft")); Assert.Empty(WebDesignAssets.Search(d, "meer")); Assert.Single(WebDesignAssets.Search(d, null, "bild"));
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "Z7: Eigene Schrift liegt lokal im Export, der Export lädt keine Schrift aus dem Netz")]
    public void Z7_font_local()
    {
        var (d, src) = Project("inter-400.woff2", [0x77, 0x4F, 0x46, 0x32]); try
        {
            var (a, err) = WebDesignAssets.Import(d, src, "schrift", "Inter", "Fontsource", "OFL-1.1", "", "https://fontsource.org/fonts/inter", "", "Inter"); Assert.Null(err);
            var j = Ok(Page, """{"aktion":"gestaltungswert_setzen","name":"font","wert":"eigene:Inter"}""");
            WebDesign.Save(d, j);
            var (outDir, _) = WebDesign.Export(d, Path.Combine(Repo(), "src", "Trendo.App", "Assets", "webdesign"));
            var html = File.ReadAllText(Path.Combine(outDir!, "index.html"));
            Assert.Contains("@font-face{font-family:\"Inter\";src:url(\"medien/schrift/inter-400.woff2\") format(\"woff2\")", html);
            Assert.True(File.Exists(Path.Combine(outDir!, "medien", "schrift", "inter-400.woff2")));
            Assert.DoesNotContain("fonts.googleapis", html); Assert.DoesNotContain("url(\"http", html);
            Assert.DoesNotContain("fonts.googleapis", File.ReadAllText(Path.Combine(outDir!, "assets", "style.css")));
            Assert.Contains("eine Schrift braucht", WebDesignAssets.Import(d, src, "schrift", "X", "", "").Error, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("nur", WebDesignAssets.Import(d, src, "bild", "X", "", "").Error);
            Assert.Equal("klar", JsonNode.Parse(WebDesign.Validate(Page.Replace("\"description\":\"Kurz\"", "\"description\":\"Kurz\",\"font\":\"eigene:x\\\"}body{\""))!.Json!)!["root"]!["props"]!["font"]!.ToString());
        }
        finally { Directory.Delete(d, true); }
    }

    static string Repo()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent) if (File.Exists(Path.Combine(dir.FullName, "Trendo.sln"))) return dir.FullName;
        throw new DirectoryNotFoundException();
    }
}
