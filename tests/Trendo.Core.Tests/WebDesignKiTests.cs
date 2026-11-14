using System.Text.Json.Nodes;
using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0620: KI-Grundlage von „Webseite bauen“ – je Prüfung aus <c>docs/nachbau/web-ki/anforderungen.md</c> mindestens ein Test.</summary>
public class WebDesignKiTests
{
    const string Page = """{"root":{"props":{"title":"Bäckerei Muster","description":"Frisches Brot"}},"content":[{"type":"Kopfbereich","props":{"id":"k1","title":"Willkommen","sub":"Text","cta":"Los","href":"#","motion":"keine","seconds":1}},{"type":"Text","props":{"id":"t1","text":"Alt","align":"left"}},{"type":"Text","props":{"id":"t2","text":"Bleibt","align":"left"}},{"type":"Bereich","props":{"id":"b1","tone":"hell","content":[{"type":"Knopf","props":{"id":"kn1","label":"Innen","href":"#"}}]}}],"zones":{}}""";
    static string Dir() => Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "trendo-webki-" + Guid.NewGuid().ToString("N"))).FullName;
    static JsonNode Prop(string json, int i) => JsonNode.Parse(json)!["content"]![i]!["props"]!;

    static string Repo()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent) if (File.Exists(Path.Combine(d.FullName, "Trendo.sln"))) return d.FullName;
        throw new DirectoryNotFoundException();
    }

    // ---------- A1 ----------
    const string SamplePlan = """{"aktionen":[{"aktion":"baustein_einfuegen","typ":"Ueberschrift","props":{"text":"Sortiment"}},{"aktion":"element_aendern","id":"t1","props":{"text":"Neu"}}]}""";

    [Fact(DisplayName = "A1: Gleicher Plan ergibt das gleiche Ergebnis, egal wie die KI ihn verpackt")]
    public void Same_plan_same_result()
    {
        var a = WebDesignActions.Apply(Page, SamplePlan);
        var b = WebDesignActions.Apply(Page, "Gerne, hier ist der Plan:\n```json\n" + SamplePlan + "\n```\nViel Erfolg!");
        var c = WebDesignActions.Apply(Page, "  " + SamplePlan + "  ");
        Assert.Null(a.Error); Assert.Equal(a.Json, b.Json); Assert.Equal(a.Json, c.Json);
        Assert.Equal("Neu", Prop(a.Json!, 1)["text"]!.ToString());
        Assert.Equal("Ueberschrift-1", Prop(a.Json!, 4)["id"]!.ToString()); // feste, vom Plan abhängige Nummer
        Assert.Equal("h2", Prop(a.Json!, 4)["level"]!.ToString()); // Standardwerte wie im Editor, sonst kann der Baustein nicht zeichnen
    }

    [Fact(DisplayName = "A1: Alle Aktionen des Editors aus Stufe 1 funktionieren")]
    public void All_actions()
    {
        Assert.Equal(26, WebDesignActions.Catalog.Count);
        var r = WebDesignActions.Apply(Page, """
            {"aktionen":[
             {"aktion":"seite_beschreiben","titel":"Neu","beschreibung":"Text"},
             {"aktion":"baustein_einfuegen","typ":"Bild","id":"bild1","in":"b1","position":0},
             {"aktion":"baustein_einfuegen","typ":"Spalten","id":"sp1"},
             {"aktion":"baustein_einfuegen","typ":"Text","id":"li","in":"sp1","slot":"left"},
             {"aktion":"bild_setzen","id":"bild1","src":"bilder/foto.jpg","alt":"Foto vom Laden"},
             {"aktion":"element_aendern","id":"kn1","props":{"label":"Kontakt","href":"https://example.org"}},
             {"aktion":"element_verschieben","id":"t2","position":0},
             {"aktion":"element_entfernen","id":"t1"},
             {"aktion":"animation_setzen","id":"k1","bewegung":"schweben","dauer":2},
             {"aktion":"gestaltungswert_setzen","name":"accent","wert":"#AA3300"},
             {"aktion":"gestaltungswert_setzen","name":"font","wert":"serif"},
             {"aktion":"gestaltungswert_setzen","name":"space","wert":32},
             {"aktion":"ansicht_anpassen","ansicht":"handy"},
             {"aktion":"stand_speichern","name":"Fertig"}
            ]}
            """);
        Assert.Null(r.Error);
        var doc = JsonNode.Parse(r.Json!)!; var root = doc["root"]!["props"]!;
        Assert.Equal("Neu", root["title"]!.ToString()); Assert.Equal("#AA3300", root["accent"]!.ToString()); Assert.Equal("serif", root["font"]!.ToString()); Assert.Equal(32, root["space"]!.GetValue<double>()); Assert.Equal("handy", root["ansicht"]!.ToString());
        Assert.Equal("t2", Prop(r.Json!, 0)["id"]!.ToString()); Assert.DoesNotContain("\"t1\"", r.Json!);
        Assert.Equal("schweben", Prop(r.Json!, 1)["motion"]!.ToString());
        var inner = Prop(r.Json!, 2)["content"]!.AsArray();
        Assert.Equal("bilder/foto.jpg", inner[0]!["props"]!["src"]!.ToString()); Assert.Equal("Kontakt", inner[1]!["props"]!["label"]!.ToString());
        Assert.Equal("li", Prop(r.Json!, 3)["left"]![0]!["props"]!["id"]!.ToString());
        Assert.Equal(["Fertig"], r.States);
        Assert.Null(WebDesignActions.Apply(WebDesign.Empty(), """{"aktionen":[{"aktion":"seite_anlegen","titel":"Frisch","beschreibung":"Kurz"}]}""").Error);
    }

    [Theory(DisplayName = "A1: Unbekannte oder ungültige Aktion wird abgelehnt")]
    [InlineData("""{"aktionen":[{"aktion":"datei_loeschen","id":"k1"}]}""", "unbekannte Aktion")]
    [InlineData("""{"aktionen":[{"aktion":"element_aendern","id":"t1","props":{"text":"x"},"extra":1}]}""", "unbekannte Angabe")]
    [InlineData("""{"aktionen":[{"aktion":"element_aendern","id":"t1"}]}""", "fehlt")]
    [InlineData("""{"aktionen":[{"aktion":"element_aendern","id":"gibtsnicht","props":{"text":"x"}}]}""", "gibt es nicht")]
    [InlineData("""{"aktionen":[{"aktion":"element_aendern","id":"t1","props":{"farbe":"x"}}]}""", "kein Feld")]
    [InlineData("""{"aktionen":[{"aktion":"element_aendern","id":"t1","props":{"align":"schief"}}]}""", "einer von")]
    [InlineData("""{"aktionen":[{"aktion":"element_aendern","id":"kn1","props":{"href":"javascript:alert(1)"}}]}""", "nicht erlaubt")]
    [InlineData("""{"aktionen":[{"aktion":"baustein_einfuegen","typ":"Skript"}]}""", "unbekannter Baustein")]
    [InlineData("""{"aktionen":[{"aktion":"baustein_einfuegen","typ":"Text","id":"t1"}]}""", "schon")]
    [InlineData("""{"aktionen":[{"aktion":"baustein_einfuegen","typ":"Text","position":99}]}""", "position")]
    [InlineData("""{"aktionen":[{"aktion":"baustein_einfuegen","typ":"Text","in":"t1"}]}""", "kann nichts liegen")]
    [InlineData("""{"aktionen":[{"aktion":"gestaltungswert_setzen","name":"accent","wert":"red;x:y"}]}""", "#RRGGBB")]
    [InlineData("""{"aktionen":[{"aktion":"gestaltungswert_setzen","name":"radius","wert":99}]}""", "Zahl")]
    [InlineData("""{"aktionen":[{"aktion":"gestaltungswert_setzen","name":"css","wert":"x"}]}""", "gibt es nicht")]
    [InlineData("""{"aktionen":[{"aktion":"animation_setzen","id":"t1","bewegung":"schweben"}]}""", "keine Bewegung")]
    [InlineData("""{"aktionen":[{"aktion":"bild_setzen","id":"t1","src":"a.png","alt":"x"}]}""", "kein Bild")]
    [InlineData("""{"aktionen":[{"aktion":"ansicht_anpassen","ansicht":"uhr"}]}""", "handy")]
    [InlineData("""{"aktionen":[{"aktion":"stand_speichern","name":"x"},{"aktion":"seite_anlegen","titel":"y"}]}""", "erste Aktion")]
    [InlineData("""{"aktionen":[{"aktion":"element_verschieben","id":"b1","position":0,"in":"kn1"}]}""", "in sich selbst")]
    [InlineData("""{"aktionen":[]}""", "keine Aktionen")]
    [InlineData("""{"aktionen":[{"aktion":"stand_speichern","name":"x"}],"code":"<script>"}""", "Unbekannte Angabe")]
    [InlineData("<html><body>fertige Seite</body></html>", "keinen Aktionsplan")]
    [InlineData("{kein json}", "kein gültiges JSON")]
    public void Rejects(string plan, string expected)
    {
        var r = WebDesignActions.Apply(Page, plan);
        Assert.Null(r.Json); Assert.Contains(expected, r.Error);
    }

    [Theory(DisplayName = "A1: Bilder von außerhalb des Projekts werden abgelehnt")]
    [InlineData("https://fremd.example/a.png")] [InlineData("../geheim.png")] [InlineData("/etc/passwd")] [InlineData("data:image/png;base64,AA")]
    public void Image_only_from_project(string src)
    {
        var plan = new JsonObject { ["aktionen"] = new JsonArray(new JsonObject { ["aktion"] = "baustein_einfuegen", ["typ"] = "Bild", ["props"] = new JsonObject { ["src"] = src } }) };
        Assert.Contains("im Projekt", WebDesignActions.Apply(Page, plan).Error);
    }

    [Theory(DisplayName = "A1 Sicherheit: Nur Bilddateien aus dem Projekt, keine versteckten oder vom Export belegten Pfade")]
    [InlineData(".env")] [InlineData(".trendo-staende/x.json")] [InlineData("seite.trendo.json")] [InlineData("assets/editor.js")] [InlineData("veroeffentlichen/foto.png")] [InlineData("ordner/.geheim/foto.png")] [InlineData("foto.svg")] [InlineData("C:\\Windows\\foto.png")]
    public void Image_only_safe(string src)
    {
        var plan = new JsonObject { ["aktionen"] = new JsonArray(new JsonObject { ["aktion"] = "baustein_einfuegen", ["typ"] = "Bild", ["id"] = "f" }, new JsonObject { ["aktion"] = "bild_setzen", ["id"] = "f", ["src"] = src, ["alt"] = "x" }) };
        Assert.Contains("Bilddatei", WebDesignActions.Apply(Page, plan).Error);
    }

    [Fact(DisplayName = "A1 Sicherheit: Fehlerhafte Pläne werden abgelehnt statt abzustürzen; Schemaprüfung lässt sich nicht mit Steuerzeichen umgehen")]
    public void Malformed_plans_and_schemes()
    {
        Assert.Contains("fehlerhaft", WebDesignActions.Apply(Page, """{"aktionen":[{"aktion":"stand_speichern","name":"a","name":"b"}]}""").Error);
        Assert.Contains("fehlerhaft", WebDesignActions.Apply(Page, """{"aktionen":[],"aktionen":[]}""").Error);
        foreach (var bad in new[] { "java\tscript:alert(1)", "\u0001javascript:alert(1)", "ms-\tmsdt:x", "file:///c:/x", "vbscript:x" })
        {
            var plan = new JsonObject { ["aktionen"] = new JsonArray(new JsonObject { ["aktion"] = "element_aendern", ["id"] = "kn1", ["props"] = new JsonObject { ["href"] = bad } }) };
            Assert.Contains("nicht erlaubt", WebDesignActions.Apply(Page, plan).Error);
            var page = new JsonObject { ["root"] = new JsonObject(), ["content"] = new JsonArray(new JsonObject { ["type"] = "Knopf", ["props"] = new JsonObject { ["href"] = bad } }) };
            Assert.Equal("#", JsonNode.Parse(WebDesign.Validate(page.ToJsonString()).Json!)!["content"]![0]!["props"]!["href"]!.ToString());
        }
        Assert.False(WebDesignCheck.IsLocalFile(Path.GetTempPath(), "a\u0000.png")); Assert.False(WebDesignCheck.IsLocalFile(Path.GetTempPath(), "C:\\Windows\\win.ini"));
    }

    [Fact(DisplayName = "A6 Sicherheit: Export kopiert nur Bilder; fremde Dateien bleiben draußen, das Bündel lässt sich nicht ersetzen")]
    public void Export_ignores_unsafe_files()
    {
        var d = Dir(); try
        {
            File.WriteAllText(Path.Combine(d, ".env"), "GEHEIM=1");
            Directory.CreateDirectory(Path.Combine(d, "assets")); File.WriteAllText(Path.Combine(d, "assets", "editor.js"), "/*BOESE*/");
            var page = """{"root":{"props":{"title":"x"}},"content":[{"type":"Bild","props":{"id":"a","src":".env","alt":"x"}},{"type":"Bild","props":{"id":"b","src":"assets/editor.js","alt":"x"}}],"zones":{}}""";
            WebDesign.Save(d, page);
            var editor = Path.Combine(Repo(), "src", "Trendo.App", "Assets", "webdesign");
            var (outDir, err) = WebDesign.Export(d, editor); Assert.Null(err);
            Assert.False(File.Exists(Path.Combine(outDir!, ".env")));
            Assert.Equal(File.ReadAllBytes(Path.Combine(editor, "assets", "editor.js")), File.ReadAllBytes(Path.Combine(outDir!, "assets", "editor.js")));
            Assert.Equal(2, WebDesignCheck.CheckData(page, d).Count(f => f.Code == "image-unsafe"));
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "K5: Stände und Varianten gehören nicht in ein weitergegebenes Paket; doppelte Schlüssel in Seitendaten werden abgewiesen")]
    public void Pack_without_states()
    {
        var root = Dir(); try
        {
            var built = WebsiteStudio.Build(root, WebsiteStudio.Example("mikro"));
            WebDesign.Save(built.Directory, Page); WebDesignStates.Save(built.Directory, "Geheimer Stand"); WebDesignStates.CreateVariant(built.Directory, "A");
            var audit = new AuditLog(Path.Combine(root, "audit.jsonl")); var gate = new ApprovalGate(audit);
            var pending = WebsiteStudio.RequestPack(gate, Principal.LocalOwner, built.Directory); gate.Approve(pending.Id, Principal.LocalOwner);
            using var archive = System.IO.Compression.ZipFile.OpenRead(WebsiteStudio.Pack(gate, pending.Id, built.Directory));
            Assert.Contains(archive.Entries, e => e.Name == "index.html");
            Assert.DoesNotContain(archive.Entries, e => e.FullName.StartsWith(".trendo-", StringComparison.Ordinal));
            Assert.Contains("JSON", WebDesign.Validate("""{"root":{},"root":{},"content":[]}""").Error);
            Assert.Null(WebDesign.ParseMessage("""{"art":"a","art":"b"}"""));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "K5 Sicherheit: Manipulierte Variantendatei gilt als nicht vorhanden und wird nie als Skript gelesen")]
    public void K5_manipulated_variant()
    {
        var d = Dir(); try
        {
            WebDesign.Save(d, Page);
            var (v, _) = WebDesignStates.CreateVariant(d, "A");
            var f = Path.Combine(d, WebDesignStates.VariantsDir, v!.Id + ".json");
            File.WriteAllText(f, new JsonObject { ["name"] = "A", ["zeit"] = "2026-11-09T00:00:00+01:00", ["seite"] = "alert(document.domain)" }.ToJsonString());
            Assert.Null(WebDesignStates.VariantContent(d, v.Id));
            Assert.DoesNotContain("alert", WebDesignStates.Read(d, v.Id));
            File.WriteAllText(f, "[]"); // keine Objektform: gilt ebenso als nicht vorhanden, statt eine Ausnahme zu werfen
            Assert.Null(WebDesignStates.VariantContent(d, v.Id));
            File.WriteAllText(f, new JsonObject { ["name"] = "A", ["seite"] = "alert(document.domain)" }.ToJsonString());
            Assert.NotNull(WebDesignStates.AdoptVariant(d, v.Id)); Assert.Contains("Willkommen", WebDesign.Load(d));
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "A1: Ein ungültiger Schritt verwirft den ganzen Plan, nichts wird geschrieben")]
    public void Nothing_written_on_rejection()
    {
        var d = Dir(); try
        {
            WebDesign.Save(d, Page); var before = File.ReadAllText(Path.Combine(d, WebDesign.PageFile));
            var r = WebDesignActions.Apply(Page, """{"aktionen":[{"aktion":"element_aendern","id":"t1","props":{"text":"Neu"}},{"aktion":"gibtsnicht"}]}""");
            Assert.Null(r.Json); Assert.Empty(r.Changes);
            var (auto, err) = WebDesignActions.Commit(d, r, Page, "Test");
            Assert.Null(auto); Assert.NotNull(err);
            Assert.Equal(before, File.ReadAllText(Path.Combine(d, WebDesign.PageFile))); Assert.Empty(WebDesignStates.List(d));
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "A1: Die Bausteine der Aktionen sind genau die Bausteine des Editors")]
    public void Components_match_editor()
    {
        var src = File.ReadAllText(Path.Combine(Repo(), "tools", "webdesign-editor", "src", "main.jsx"));
        Assert.Equal(17, WebDesignActions.ComponentNames.Count);
        var names = WebDesignActions.ComponentNames.ToList();
        foreach (var c in names)
        {
            var start = src.IndexOf($"    {c}: {{", StringComparison.Ordinal); Assert.True(start >= 0, c);
            var next = names.Select(n => src.IndexOf($"    {n}: {{", start + 1, StringComparison.Ordinal)).Where(i => i > start).DefaultIfEmpty(src.Length).Min();
            foreach (var field in WebDesignActions.ComponentFields(c).Where(f => f is not ("motion" or "seconds"))) Assert.Contains(field + ":", src[start..next]); // gleiche Felder wie im Editor
        }
    }

    // ---------- A7 ----------
    [Fact(DisplayName = "A7: Jede Aktion nennt ihren Bedarf; das Plan-Ergebnis sagt, was gebraucht wurde")]
    public void Needs_named()
    {
        Assert.All(WebDesignActions.Catalog, a => Assert.True(Enum.IsDefined(a.Bedarf)));
        Assert.Equal(KiBedarf.Bild, WebDesignActions.Catalog.Single(a => a.Name == "bild_setzen").Bedarf);
        Assert.Equal(KiBedarf.Bild, WebDesignKi.Tasks["seite_aus_bild"]); Assert.Equal(KiBedarf.Text, WebDesignKi.Tasks["seite_aus_text"]);
        Assert.Equal([KiBedarf.Text], WebDesignActions.Apply(Page, SamplePlan).Needs);
    }

    [Fact(DisplayName = "A7: Claude zuerst, sonst lokal; Bildaufgabe ohne bildfähige KI ergibt einen klaren Hinweis")]
    public void Choose_by_need()
    {
        Assert.Equal(WebDesignKi.ClaudeLabel, WebDesignKi.Choose(KiBedarf.Text, true, true, false).Who);
        Assert.Equal(WebDesignKi.LocalLabel, WebDesignKi.Choose(KiBedarf.Text, false, true, false).Who);
        Assert.Equal(WebDesignKi.LocalLabel, WebDesignKi.Choose(KiBedarf.Code, false, true, false).Who);
        Assert.Equal(WebDesignKi.LocalLabel, WebDesignKi.Choose(KiBedarf.Bild, false, true, true).Who);
        var none = WebDesignKi.Choose(KiBedarf.Bild, false, true, false);
        Assert.False(none.Ok); Assert.Contains("Bilder sehen", none.Note);
        Assert.False(WebDesignKi.Choose(KiBedarf.Text, false, false, false).Ok);
    }

    sealed class Fake(string name, bool up, string answer) : IKiBackend
    {
        public int Calls;
        public string Name => name; public string BaseUrl => "http://127.0.0.1";
        public Task<bool> IsUpAsync(CancellationToken ct = default) => Task.FromResult(up);
        public Task<IReadOnlyList<string>> ModelsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>(["m"]);
        public Task<string> ChatAsync(string model, IReadOnlyList<KiMessage> messages, CancellationToken ct = default) { Calls++; return Task.FromResult(answer); }
    }
    sealed class FakeClaude(bool up) : IMainKiBackend
    {
        public string Name => "Claude"; public string BaseUrl => "claude-code";
        public Task<bool> IsUpAsync(CancellationToken ct = default) => Task.FromResult(up);
        public Task<IReadOnlyList<string>> ModelsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>(["claude"]);
        public Task<string> ChatAsync(string model, IReadOnlyList<KiMessage> messages, CancellationToken ct = default) => Task.FromResult(SamplePlan);
    }
    sealed class FakeApi : IRemoteKiBackend
    {
        public int Calls;
        public string Name => "Bezahl-API"; public string BaseUrl => "https://api.example";
        public Task<bool> IsUpAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<IReadOnlyList<string>> ModelsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>(["x"]);
        public Task<string> ChatAsync(string model, IReadOnlyList<KiMessage> messages, CancellationToken ct = default) { Calls++; return Task.FromResult("nein"); }
    }

    [Fact(DisplayName = "A7: Ohne Claude läuft die Textaufgabe lokal, ein KI-Dienst mit API-Schlüssel wird nie benutzt")]
    public async Task Text_runs_locally()
    {
        var d = Dir(); try
        {
            var local = new Fake("Ollama", true, SamplePlan); var api = new FakeApi();
            IKiBackend[] all = [new FakeClaude(false), api, local];
            var allowed = WebDesignKi.Allowed(all);
            Assert.DoesNotContain(api, allowed); Assert.Equal(2, allowed.Count);
            var core = new IntelligenceCore(d, new AuditLog(Path.Combine(d, "audit")), () => allowed);
            var a = await core.AskAsync(new KiRequest("Auftrag", WebDesignKi.Instructions(null), UseKnowledge: false, AllowRemote: true));
            Assert.True(a.Ok); Assert.Equal("Ollama", a.Backend); Assert.Equal(1, local.Calls); Assert.Equal(0, api.Calls);
            Assert.Null(WebDesignActions.Apply(Page, a.Text).Error);
        }
        finally { Directory.Delete(d, true); }
    }

    // ---------- A3 ----------
    static SkillLibrary Lib(out string root)
    {
        root = Dir(); var audit = new AuditLog(Path.Combine(root, "audit"));
        return new SkillLibrary(root, new PromptLibrary(root, audit), audit);
    }

    [Theory(DisplayName = "A3: Aufgabe lädt das passende Fachgebiet")]
    [InlineData("Mach die Seite barrierefrei für Screenreader", "webdesign-barrierefreiheit")]
    [InlineData("Passe die Seite fürs Handy an", "webdesign-responsiv")]
    [InlineData("Lass den Kopfbereich sanft einblenden", "webdesign-bewegung")]
    [InlineData("Bessere Auffindbarkeit bei Google", "webdesign-seo")]
    [InlineData("Die Ladezeit ist zu lang", "webdesign-leistung")]
    [InlineData("Ein sicheres Anmeldeformular mit Passwort", "webdesign-sicherheit")]
    [InlineData("Ändere die Schrift und die Abstände", "webdesign-layout")]
    public void Task_loads_topic(string task, string id)
    {
        var lib = Lib(out var root); try
        {
            var skills = lib.ForTask(null, "CORE-07", task);
            Assert.Equal(id, skills.First(s => s.Keywords.Count > 0).Id);
            Assert.Contains(skills, s => s.Id == "webdesign"); // Grundregeln bleiben dabei
            Assert.DoesNotContain(skills, s => s.Id == "brief-din5008");
            Assert.Contains(skills.First(s => s.Keywords.Count > 0).Name, lib.PromptText(null, "CORE-07", task));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "A3: Grenze für lokale KI wird eingehalten, gekürzt wird an ganzen Zeilen, Checkliste entfällt")]
    public void Local_limit()
    {
        var lib = Lib(out var root); try
        {
            const string many = "barrierefrei handy animation seo ladezeit sicherheit layout schrift";
            var local = lib.PromptText(null, "CORE-07", many, SkillLibrary.MaxPromptCharsLocal)!;
            Assert.True(local.Length <= SkillLibrary.MaxPromptCharsLocal, local.Length.ToString());
            Assert.DoesNotContain("## Checkliste", local);
            var allLines = SkillLibrary.BuiltIn.SelectMany(s => s.Body.Split('\n')).Select(l => l.Trim()).ToHashSet();
            Assert.All(local.Split('\n').Where(l => l.Length > 0 && !l.StartsWith("### Skill:")), l => Assert.Contains(l.Trim(), allLines));
            var full = lib.PromptText(null, "CORE-07", many)!;
            Assert.True(full.Length <= SkillLibrary.MaxPromptChars); Assert.True(full.Length > local.Length);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "A3: Fachgebiete tauchen ohne passende Aufgabe nicht auf, Stichworte treffen nur am Wortanfang")]
    public void Topics_only_when_relevant()
    {
        var lib = Lib(out var root); try
        {
            Assert.DoesNotContain(lib.ForTask(null, "CORE-07", "Eine Variante mit Text"), s => s.Id == "webdesign-barrierefreiheit"); // „aria“ steckt in „Variante“
            Assert.DoesNotContain(lib.For(null, "CORE-07"), s => s.Keywords.Count > 0);
            Assert.Null(lib.PromptText(null, "CORE-02", "barrierefrei"));
        }
        finally { Directory.Delete(root, true); }
    }

    // ---------- K4 ----------
    [Fact(DisplayName = "K4: Ungültige KI-Antwort wird abgelehnt und gelangt nicht in die Seite")]
    public void K4_invalid_rejected()
    {
        var r = WebDesignActions.Apply(WebDesign.Empty(), "<html><script>alert(1)</script></html>");
        Assert.Null(r.Json); Assert.NotNull(r.Error);
        Assert.Null(WebDesignActions.Apply(WebDesign.Empty(), """{"root":{},"content":[]}""").Json); // fertige Seitendaten statt Plan reichen nicht
    }

    [Fact(DisplayName = "K4: Ergebnis sind Editor-Daten, die sich öffnen und weiter ändern lassen")]
    public void K4_editable()
    {
        var d = Dir(); try
        {
            var r = WebDesignActions.Apply(WebDesign.Empty(), """{"aktionen":[{"aktion":"seite_anlegen","titel":"Café Muster","beschreibung":"Kaffee"},{"aktion":"baustein_einfuegen","typ":"Kopfbereich","id":"held","props":{"title":"Guter Kaffee"}},{"aktion":"baustein_einfuegen","typ":"Bereich","id":"b"},{"aktion":"baustein_einfuegen","typ":"Text","in":"b","props":{"text":"Täglich frisch"}}]}""");
            Assert.Null(r.Error); Assert.Null(WebDesign.Save(d, r.Json!));
            var loaded = WebDesign.Load(d); var doc = JsonNode.Parse(loaded)!;
            Assert.Equal("Café Muster", doc["root"]!["props"]!["title"]!.ToString());
            Assert.Equal("Guter Kaffee", Prop(loaded, 0)["title"]!.ToString());
            Assert.Equal("einblenden", Prop(loaded, 0)["motion"]!.ToString()); // Standardwerte des Editors sind da
            Assert.Equal("Text-1", Prop(loaded, 1)["content"]![0]!["props"]!["id"]!.ToString());
            var change = WebDesignActions.Apply(loaded, """{"aktionen":[{"aktion":"element_aendern","id":"Text-1","props":{"text":"Geändert"}}]}""");
            Assert.Equal("Geändert", Prop(change.Json!, 1)["content"]![0]!["props"]!["text"]!.ToString());
        }
        finally { Directory.Delete(d, true); }
    }

    // ---------- K1 ----------
    const string SelectionPlan = """{"aktionen":[{"aktion":"element_aendern","id":"t1","props":{"text":"Neu"}}]}""";

    [Fact(DisplayName = "K1: Auftrag ändert nur ausgewählte Bausteine")]
    public void K1_only_selection()
    {
        var r = WebDesignActions.Apply(Page, SelectionPlan, new WebPlanScope(new HashSet<string> { "t1" }));
        Assert.Null(r.Error);
        var before = JsonNode.Parse(Page)!["content"]!.AsArray(); var after = JsonNode.Parse(r.Json!)!["content"]!.AsArray();
        Assert.Equal(before.Count, after.Count);
        for (var i = 0; i < before.Count; i++) if (i != 1) Assert.True(JsonNode.DeepEquals(before[i], after[i]));
        Assert.Equal("Neu", after[1]!["props"]!["text"]!.ToString());
    }

    [Theory(DisplayName = "K1: Alles außerhalb der Auswahl wird abgelehnt")]
    [InlineData("""{"aktionen":[{"aktion":"element_aendern","id":"t2","props":{"text":"x"}}]}""", "nicht zur Auswahl")]
    [InlineData("""{"aktionen":[{"aktion":"element_entfernen","id":"t1"}]}""", "mehr als die Auswahl")]
    [InlineData("""{"aktionen":[{"aktion":"baustein_einfuegen","typ":"Text"}]}""", "mehr als die Auswahl")]
    [InlineData("""{"aktionen":[{"aktion":"gestaltungswert_setzen","name":"accent","wert":"#112233"}]}""", "mehr als die Auswahl")]
    public void K1_outside_rejected(string plan, string expected) =>
        Assert.Contains(expected, WebDesignActions.Apply(Page, plan, new WebPlanScope(new HashSet<string> { "t1" })).Error);

    [Fact(DisplayName = "K1: Ein ausgewählter Bereich schließt seinen Inhalt ein; Auftrag zeigt der KI nur die Auswahl")]
    public void K1_section_and_prompt()
    {
        Assert.Null(WebDesignActions.Apply(Page, """{"aktionen":[{"aktion":"element_aendern","id":"kn1","props":{"label":"x"}}]}""", new WebPlanScope(new HashSet<string> { "b1" })).Error);
        var p = WebDesignKi.SelectionPrompt("kürzer", Page, ["t1"]);
        Assert.Contains("\"t1\"", p); Assert.DoesNotContain("Bleibt", p); Assert.DoesNotContain("Willkommen", p);
        var r = WebDesignKi.RepairPrompt(Page, [new("Links", "Fehler", "t1 kaputt", "x")], ["t1"]);
        Assert.Contains("t1 kaputt", r); Assert.DoesNotContain("Bleibt", r); // auch beim Nachbessern nur die Auswahl
    }

    // ---------- K5 ----------
    [Fact(DisplayName = "K5: Wiederherstellen ergibt exakt den alten Inhalt, vorher entsteht ein automatischer Stand")]
    public void K5_restore_exact()
    {
        var d = Dir(); try
        {
            WebDesign.Save(d, Page); var original = File.ReadAllBytes(Path.Combine(d, WebDesign.PageFile));
            var (s, err) = WebDesignStates.Save(d, "Erster Stand"); Assert.Null(err);
            WebDesign.Save(d, Page.Replace("Willkommen", "Hallo"));
            Assert.Null(WebDesignStates.Restore(d, s!.Id));
            Assert.Equal(original, File.ReadAllBytes(Path.Combine(d, WebDesign.PageFile)));
            var list = WebDesignStates.List(d);
            Assert.Equal(2, list.Count); Assert.True(list[0].Auto); Assert.Equal("Erster Stand", list[1].Name); Assert.Contains("Hallo", WebDesignStates.Content(d, list[0].Id));
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "K5: Vor jeder KI-Änderung entsteht automatisch ein Stand; benannte Stände aus dem Plan werden gespeichert")]
    public void K5_auto_before_ai()
    {
        var d = Dir(); try
        {
            WebDesign.Save(d, Page);
            var r = WebDesignActions.Apply(Page, """{"aktionen":[{"aktion":"element_aendern","id":"t1","props":{"text":"Neu"}},{"aktion":"stand_speichern","name":"Nach KI"}]}""");
            var (auto, err) = WebDesignActions.Commit(d, r, File.ReadAllText(Path.Combine(d, WebDesign.PageFile)), "Text ändern"); Assert.Null(err);
            var list = WebDesignStates.List(d);
            Assert.Contains(list, s => s.Id == auto && s.Auto && s.Name == "Vor KI: Text ändern");
            Assert.Contains(list, s => s.Name == "Nach KI" && !s.Auto);
            Assert.DoesNotContain("Neu", WebDesignStates.Content(d, auto!)); Assert.Contains("Neu", WebDesign.Load(d));
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "K5: Variante ändert das Original nicht; übernehmen ersetzt die Seite und sichert sie vorher")]
    public void K5_variant()
    {
        var d = Dir(); try
        {
            WebDesign.Save(d, Page); var original = File.ReadAllText(Path.Combine(d, WebDesign.PageFile));
            var (v, _) = WebDesignStates.CreateVariant(d, "Dunkel");
            Assert.Null(WebDesignStates.SaveVariant(d, v!.Id, Page.Replace("Willkommen", "Variante")));
            Assert.Equal(original, File.ReadAllText(Path.Combine(d, WebDesign.PageFile)));
            Assert.Contains("Variante", WebDesignStates.VariantContent(d, v.Id));
            Assert.Equal("Dunkel", WebDesignStates.Variants(d).Single().Name);
            Assert.NotNull(WebDesignStates.SaveVariant(d, v.Id, "kaputt"));
            Assert.Null(WebDesignStates.AdoptVariant(d, v.Id));
            Assert.Contains("Variante", WebDesign.Load(d));
            Assert.Contains("Willkommen", WebDesignStates.Content(d, WebDesignStates.List(d).Single().Id));
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "K5: Ein Stand einer Variante kehrt in die Variante zurück, nicht in die Seite")]
    public void K5_state_of_variant()
    {
        var d = Dir(); try
        {
            WebDesign.Save(d, Page);
            var (v, _) = WebDesignStates.CreateVariant(d, "A");
            var (s, _) = WebDesignStates.Save(d, "Vor Änderung", v!.Id);
            WebDesignStates.SaveVariant(d, v.Id, Page.Replace("Willkommen", "Geändert"));
            var page = File.ReadAllText(Path.Combine(d, WebDesign.PageFile));
            Assert.Null(WebDesignStates.Restore(d, s!.Id));
            Assert.DoesNotContain("Geändert", WebDesignStates.VariantContent(d, v.Id)); Assert.Equal(page, File.ReadAllText(Path.Combine(d, WebDesign.PageFile)));
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "K5: Fremde Kennungen kommen nicht aus dem Ordner; alte automatische Stände werden begrenzt, benannte bleiben")]
    public void K5_limits()
    {
        var d = Dir(); try
        {
            WebDesign.Save(d, Page); File.WriteAllText(Path.Combine(d, "geheim.json"), "{}");
            Assert.NotNull(WebDesignStates.Restore(d, "..\\..\\geheim")); Assert.Null(WebDesignStates.Content(d, "../geheim")); Assert.False(WebDesignStates.Delete(d, "../geheim")); Assert.False(WebDesignStates.DeleteVariant(d, "../geheim"));
            Assert.NotNull(WebDesignStates.Save(d, " ").Error);
            WebDesignStates.Save(d, "Benannt");
            for (var i = 0; i < WebDesignStates.MaxAuto + 5; i++) WebDesignStates.SaveContent(d, "Auto", Page, true);
            var list = WebDesignStates.List(d);
            Assert.Equal(WebDesignStates.MaxAuto, list.Count(s => s.Auto)); Assert.Contains(list, s => s.Name == "Benannt");
        }
        finally { Directory.Delete(d, true); }
    }

    // ---------- A6 ----------
    static string Fehlerseite => Page.Replace("\"href\":\"#\",\"motion\"", "\"href\":\"#gibtsnicht\",\"motion\"");

    [Fact(DisplayName = "A6: Eingebauter Fehler wird gefunden und gemeldet")]
    public void A6_finds_errors()
    {
        var d = Dir(); try
        {
            var f = WebDesignCheck.CheckData(Fehlerseite, d);
            Assert.Contains(f, x => x.Level == "Fehler" && x.Code == "anchor" && x.Text.Contains("#gibtsnicht"));
            var withImage = WebDesignActions.Apply(Page, """{"aktionen":[{"aktion":"baustein_einfuegen","typ":"Bild","id":"foto","props":{"src":"fehlt.png","alt":""}},{"aktion":"baustein_einfuegen","typ":"Kopfbereich"},{"aktion":"gestaltungswert_setzen","name":"ink","wert":"#EEEEEE"},{"aktion":"gestaltungswert_setzen","name":"paper","wert":"#FFFFFF"},{"aktion":"baustein_einfuegen","typ":"Ueberschrift","props":{"level":"h3"}}]}""");
            var g = WebDesignCheck.CheckData(withImage.Json!, d);
            foreach (var code in new[] { "missing", "alt", "h1", "contrast", "heading-order" }) Assert.Contains(g, x => x.Code == code && x.Level == "Fehler");
            Assert.DoesNotContain(WebDesignCheck.CheckData(Page, d), x => x.Level == "Fehler");
            Assert.DoesNotContain(WebDesignCheck.CheckData(Page.Replace("\"href\":\"#\",\"motion\"", "\"href\":\"tel:+4912345\",\"motion\""), d), x => x.Level == "Fehler"); // Telefon-Links sind erlaubt
            Assert.True(WebDesign.IsSafeImagePath("assets/poster.jpg")); // Webseiten von Trendo legen Bilder dort ab
        }
        finally { Directory.Delete(d, true); }
    }

    static Task<IReadOnlyList<WebsiteFinding>> Always(string _) => Task.FromResult<IReadOnlyList<WebsiteFinding>>([new("Links", "Fehler", "immer kaputt", "x")]);

    [Fact(DisplayName = "A6: Obergrenze an Runden wird eingehalten")]
    public async Task A6_limit()
    {
        var checks = 0; var repairs = 0; var n = 0;
        var r = await WebDesignCheck.RunAsync(Page, j => { checks++; return Always(j); }, (_, _) => { repairs++; return Task.FromResult<string?>("""{"aktionen":[{"aktion":"element_aendern","id":"t1","props":{"text":"Versuch """ + ++n + """
            "}}]}
            """); });
        Assert.Equal(WebDesignCheck.MaxRepairs, repairs); Assert.Equal(WebDesignCheck.MaxRepairs + 1, checks);
        Assert.False(r.Clean); Assert.True(r.LimitReached); Assert.Equal(WebDesignCheck.MaxRepairs, r.Repairs);
        Assert.Contains("Obergrenze", r.Rounds[^1].Note);
    }

    [Fact(DisplayName = "A6: Gezielte Nachbesserung behebt den Fehler und endet sofort")]
    public async Task A6_repair_fixes()
    {
        var d = Dir(); try
        {
            string? asked = null;
            var r = await WebDesignCheck.RunAsync(Fehlerseite, j => Task.FromResult(WebDesignCheck.CheckData(j, d)), (doc, errors) =>
            {
                asked = WebDesignKi.RepairPrompt(doc, errors);
                return Task.FromResult<string?>("""{"aktionen":[{"aktion":"element_aendern","id":"k1","props":{"href":"#"}}]}""");
            });
            Assert.True(r.Clean); Assert.Equal(1, r.Repairs); Assert.Contains("#gibtsnicht", asked); Assert.DoesNotContain("gibtsnicht", r.Json);
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "A6: Ungültige Nachbesserung wird verworfen, die Seite bleibt gültig; Nachbessern bleibt in der Auswahl")]
    public async Task A6_bad_repair()
    {
        var r = await WebDesignCheck.RunAsync(Page, Always, (_, _) => Task.FromResult<string?>("<html>Code</html>"));
        Assert.False(r.Clean); Assert.Equal(Page, r.Json); Assert.Contains("verworfen", r.Rounds[0].Note);
        var s = await WebDesignCheck.RunAsync(Page, Always, (_, _) => Task.FromResult<string?>("""{"aktionen":[{"aktion":"element_aendern","id":"t2","props":{"text":"x"}}]}"""), new WebPlanScope(new HashSet<string> { "t1" }));
        Assert.Contains("nicht zur Auswahl", s.Rounds[0].Note);
    }

    [Fact(DisplayName = "A6: Export nimmt Bilder aus dem Projekt mit und nennt die Beschreibung; Stände gehören nicht in den Export")]
    public void A6_export_complete()
    {
        var d = Dir(); try
        {
            Directory.CreateDirectory(Path.Combine(d, "bilder")); File.WriteAllBytes(Path.Combine(d, "bilder", "foto.png"), [1, 2, 3]);
            var r = WebDesignActions.Apply(Page, """{"aktionen":[{"aktion":"baustein_einfuegen","typ":"Bild","id":"f","props":{"src":"bilder/foto.png","alt":"Foto"}}]}""");
            WebDesign.Save(d, r.Json!); WebDesignStates.Save(d, "S");
            var editor = Path.Combine(Repo(), "src", "Trendo.App", "Assets", "webdesign");
            var (outDir, err) = WebDesign.Export(d, editor); Assert.Null(err);
            Assert.True(File.Exists(Path.Combine(outDir!, "bilder", "foto.png")));
            Assert.Contains("name=\"description\" content=\"Frisches Brot\"", File.ReadAllText(Path.Combine(outDir!, "index.html")));
            Assert.Empty(Directory.GetDirectories(outDir!, ".trendo-*"));
            Assert.Contains("prefers-reduced-motion", File.ReadAllText(Path.Combine(editor, "assets", "style.css")));
            Assert.Contains("focus-visible", File.ReadAllText(Path.Combine(editor, "assets", "style.css")));
            Assert.Contains("dataset.fertig", File.ReadAllText(Path.Combine(editor, "assets", "editor.js")));
        }
        finally { Directory.Delete(d, true); }
    }
}
