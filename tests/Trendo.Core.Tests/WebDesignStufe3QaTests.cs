using System.Diagnostics;
using System.Text.Json.Nodes;
using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0622: K7 (Web-Apps mit Daten) und Gegenproben zu den Befunden der QA- und Security-Prüfung von Teil 2.</summary>
public class WebDesignStufe3QaTests
{
    static string Dir() => Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "trendo-web3q-" + Guid.NewGuid().ToString("N"))).FullName;
    static void Clean(string d) { try { foreach (var f in Directory.EnumerateFiles(d, "*", SearchOption.AllDirectories)) File.SetAttributes(f, FileAttributes.Normal); Directory.Delete(d, true); } catch (IOException) { } }
    static string Repo()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent) if (File.Exists(Path.Combine(d.FullName, "Trendo.sln"))) return d.FullName;
        throw new DirectoryNotFoundException();
    }
    static string Editor() => Path.Combine(Repo(), "src", "Trendo.App", "Assets", "webdesign");
    static JsonObject T(string t) => new() { ["text"] = t };
    static JsonObject E(string tag, JsonObject? attrs = null, params JsonNode[] kinder) => new() { ["tag"] = tag, ["attrs"] = attrs ?? new JsonObject(), ["kinder"] = new JsonArray(kinder) };
    static string Jwt(string role) => "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9." + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($$"""{"iss":"supabase","ref":"abcdefgh","role":"{{role}}"}""")).TrimEnd('=').Replace('+', '-').Replace('/', '_') + ".c2lnbmF0dXJlX2Zha2VfMTIzNDU2";
    static bool Junction(string link, string target)
    {
        var p = Process.Start(new ProcessStartInfo("cmd.exe", ["/c", "mklink", "/J", link, target]) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true });
        p!.WaitForExit(10_000);
        return Directory.Exists(link);
    }

    // ---------- K7 ----------

    const string AppPage = """{"root":{"props":{"title":"Gästebuch","description":"Schreib uns"}},"content":[{"type":"Anmeldung","props":{"id":"an","titel":"Anmelden"}},{"type":"Datenliste","props":{"id":"dl","titel":"Einträge","tabelle":"gaeste","felder":[{"name":"name","beschriftung":"Name","art":"text","pflicht":"ja"},{"name":"nachricht","beschriftung":"Nachricht","art":"langtext","pflicht":"ja"}],"knopf":"Speichern","nurAngemeldet":"ja","liste":"alle"}}],"zones":{}}""";

    [Fact(DisplayName = "K7: Supabase nur mit eigenem Projekt und öffentlichem Schlüssel; geheime Schlüssel werden abgelehnt")]
    public void K7_supabase_keys()
    {
        Assert.Null(WebDesignData.CheckSupabase("https://meinprojekt.supabase.co", Jwt("anon")));
        Assert.Null(WebDesignData.CheckSupabase("https://meinprojekt.supabase.co", "sb_publishable_abcdefghijklmnop1234"));
        Assert.Contains("geheimer Schlüssel", WebDesignData.CheckSupabase("https://meinprojekt.supabase.co", Jwt("service_role")));
        Assert.Contains("geheimer Schlüssel", WebDesignData.CheckSupabase("https://meinprojekt.supabase.co", "sb_secret_abcdefghijklmnop1234"));
        Assert.NotNull(WebDesignData.CheckSupabase("http://meinprojekt.supabase.co", Jwt("anon")));
        Assert.NotNull(WebDesignData.CheckSupabase("https://evil.example", Jwt("anon")));
        var d = Dir(); try
        {
            Assert.NotNull(WebDesignData.SetSupabase(d, "https://meinprojekt.supabase.co", Jwt("service_role")));
            Assert.False(File.Exists(Path.Combine(d, WebDesignData.FileName)));
        }
        finally { Clean(d); }
    }

    [Fact(DisplayName = "K7: Geheimnis im Code wird gemeldet (Seitendaten und Export) und sperrt das Veröffentlichen")]
    public void K7_secret_in_code_reported()
    {
        var d = Dir(); try
        {
            var page = JsonNode.Parse(AppPage)!.AsObject();
            page["content"]!.AsArray().Add(new JsonObject { ["type"] = "Text", ["props"] = new JsonObject { ["id"] = "t", ["text"] = "Schlüssel: " + Jwt("service_role"), ["align"] = "left" } });
            Assert.Null(WebDesign.Save(d, page.ToJsonString()));
            Assert.Contains(WebDesignSeo.Check(d), f => f.Ebene == "Fehler" && f.Text.Contains("service_role"));
            Assert.Null(WebDesignExport.ExportSite(d, Editor()).Error);
            var plan = WebDesignPublish.Prepare(d, []);
            Assert.False(plan.Ok); Assert.Contains(plan.Blockers, b => b.Contains("Geheim"));
            Assert.DoesNotContain(Jwt("service_role"), string.Join(" ", plan.Blockers)); // nur maskiert
        }
        finally { Clean(d); }
    }

    [Fact(DisplayName = "K7: Datenbank-Bündel nur auf Seiten mit Daten-Bausteinen; Einstellungen ohne Geheimnis; Prüfung findet fehlende Anmeldung und ungültige Tabelle")]
    public void K7_export_and_checks()
    {
        var d = Dir(); try
        {
            Assert.Null(WebDesign.Save(d, AppPage));
            WebDesignSite.Create(d, "info", "Info");
            Assert.Null(WebDesign.Save(d, """{"root":{"props":{"title":"Info","description":"x"}},"content":[{"type":"Text","props":{"id":"t","text":"Nur Text","align":"left"}}],"zones":{}}"""));
            Assert.Null(WebDesignData.SetSupabase(d, "https://meinprojekt.supabase.co", Jwt("anon")));
            var r = WebDesignExport.ExportSite(d, Editor());
            Assert.Null(r.Error);
            var index = File.ReadAllText(Path.Combine(r.Dir!, "index.html"));
            Assert.Contains("<script defer src=\"assets/daten.js\"></script>", index);
            Assert.Contains("window.TRENDO_DATEN", index); Assert.Contains("meinprojekt.supabase.co", index);
            Assert.DoesNotContain("assets/daten.js", File.ReadAllText(Path.Combine(r.Dir!, "info.html")));
            Assert.True(File.Exists(Path.Combine(r.Dir!, "assets", "daten.js")));
            Assert.Contains("create policy \"gaeste_einfuegen\"", WebDesignData.SupabaseSql([JsonNode.Parse(AppPage)!["content"]![1]!["props"]!.AsObject()]));
            var broken = JsonNode.Parse(AppPage)!.AsObject(); broken["content"]!.AsArray().RemoveAt(0); broken["content"]![0]!["props"]!["tabelle"] = "Gäste Tabelle";
            WebDesignSite.Activate(d, "index"); Assert.Null(WebDesign.Save(d, broken.ToJsonString()));
            var f = WebDesignSeo.Check(d);
            Assert.Contains(f, x => x.Text.Contains("Tabelle aus Kleinbuchstaben"));
            Assert.Contains(f, x => x.Text.Contains("keine Seite hat den Baustein „Anmeldung“"));
        }
        finally { Clean(d); }
    }

    // ---------- Befunde der QA zu Teil 2 ----------

    [Fact(DisplayName = "QA T2-B1: index.html, de.html und sitemap.html lassen sich importieren")]
    public void Qa_import_reserved_file_names()
    {
        var src = Dir(); var proj = Dir(); try
        {
            WebDesign.Save(proj, WebDesign.Empty());
            foreach (var n in new[] { "index", "de", "sitemap" })
            {
                File.WriteAllText(Path.Combine(src, n + ".html"), "<p>x</p>");
                var o = WebDesignImport.CreatePage(proj, Path.Combine(src, n + ".html"), new JsonObject { ["knoten"] = new JsonArray(E("p", null, T("Hallo"))) }.ToJsonString());
                Assert.Null(o.Error); Assert.StartsWith("importierte-seite", o.Adresse);
            }
        }
        finally { Clean(src); Clean(proj); }
    }

    [Fact(DisplayName = "QA T2-B3/B4/B6: doppelte id wird umbenannt und gemeldet; %20 im Bildpfad gefunden; Text bleibt genau; leerer Link bleibt leer")]
    public void Qa_round_trip_details()
    {
        var src = Dir(); var proj = Dir(); try
        {
            var tree = new JsonArray(
                E("section", new JsonObject { ["data-typ"] = "Text", ["data-b"] = "t" }, E("p", new JsonObject { ["data-feld"] = "text" }, T("  Rand  und  a  b  "))),
                E("section", new JsonObject { ["data-typ"] = "Text", ["data-b"] = "t" }, E("p", new JsonObject { ["data-feld"] = "text" }, T("Kopie"))),
                E("section", new JsonObject { ["data-typ"] = "Knopf", ["data-b"] = "k" }, E("p", new JsonObject { ["data-feld"] = "label" }, T("Los")), E("a", new JsonObject { ["data-feld"] = "href", ["href"] = "" })),
                E("section", new JsonObject { ["data-typ"] = "Szene3D", ["data-b"] = "s", ["data-p-spin"] = "abc" }));
            var r = WebDesignHtml.FromTree(tree, new WebDesignHtml.Context(proj, null));
            Assert.Equal("  Rand  und  a  b  ", r.Content[0]!["props"]!["text"]!.ToString());
            Assert.NotEqual("t", r.Content[1]!["props"]!["id"]!.ToString());
            Assert.Contains(r.Report, x => x.Contains("doppelt"));
            Assert.Equal("", r.Content[2]!["props"]!["href"]!.ToString());
            Assert.Contains(r.Report, x => x.Contains("keine Zahl"));
            File.WriteAllBytes(Path.Combine(src, "foto bild.png"), [137, 80, 78, 71]);
            var imp = WebDesignHtml.FromTree(new JsonArray(E("img", new JsonObject { ["src"] = "foto%20bild.png", ["alt"] = "x" })), new WebDesignHtml.Context(proj, src));
            Assert.Contains("bilder/import/foto bild.png", imp.Content.ToJsonString());
        }
        finally { Clean(src); Clean(proj); }
    }

    [Fact(DisplayName = "QA T2-A1/A2/A3: alter Export ohne Liste wird erkannt; eigene robots.txt bleibt; fremde Datei gesichert; Schreibschutz bricht vor dem Löschen ab")]
    public void Qa_export_legacy_and_foreign()
    {
        var d = Dir(); try
        {
            WebDesign.Save(d, """{"root":{"props":{"title":"Start","description":"x"}},"content":[],"zones":{}}""");
            WebDesignSite.Create(d, "alt", "Alt"); WebDesign.Save(d, """{"root":{"props":{"title":"Alt","description":"x"}},"content":[],"zones":{}}""");
            var outDir = Path.Combine(d, WebDesign.ExportDir);
            Assert.Null(WebDesignExport.ExportSite(d, Editor()).Error);
            File.Delete(Path.Combine(outDir, ".trendo", "export-dateien.txt")); // wie ein Export von früher
            Assert.Null(WebDesignSite.Delete(d, "alt"));
            File.WriteAllText(Path.Combine(outDir, "robots.txt"), "USER-ROBOTS");
            File.WriteAllText(Path.Combine(outDir, "eigene.html"), "<p>meine Seite</p>");
            var r = WebDesignExport.ExportSite(d, Editor());
            Assert.Null(r.Error);
            Assert.False(File.Exists(Path.Combine(outDir, "alt.html"))); // alte Trendo-Seite erkannt und entfernt
            Assert.True(File.Exists(Path.Combine(outDir, "eigene.html")));
            Assert.Equal("USER-ROBOTS", File.ReadAllText(Path.Combine(outDir, "robots.txt")));
            Assert.Contains(r.Notes, n => n.Contains("robots.txt bleibt"));
            Assert.Equal("USER-ROBOTS", File.ReadAllText(Path.Combine(outDir, ".trendo", "vorher", "robots.txt")));
            File.SetAttributes(Path.Combine(outDir, "eigene.html"), FileAttributes.ReadOnly);
            var blocked = WebDesignExport.ExportSite(d, Editor());
            Assert.Contains("schreibgeschützt", blocked.Error);
            Assert.True(File.Exists(Path.Combine(outDir, "index.html"))); // nichts gelöscht
        }
        finally { Clean(d); }
    }

    // ---------- Befunde der Security-Prüfung ----------

    [Theory(DisplayName = "Sicherheit 2: Netzwerkpfade werden auch in versteckter Form abgelehnt")]
    [InlineData(@"/\host\share\a.html")] [InlineData(@"\/host/share/a.html")] [InlineData(@"\??\UNC\host\share\a.html")] [InlineData(@"\\host\share\a.html")] [InlineData(@"\\?\UNC\host\share\a.html")]
    public void Sec_network_paths(string p)
    {
        Assert.NotNull(WebDesignImport.CheckSource(p));
        Assert.NotNull(WebDesignImport.LocalPathError(p) ?? WebDesignImport.CheckSource(p));
        Assert.NotNull(WebDesignMods.Prepare(p).Error);
    }

    [Fact(DisplayName = "Sicherheit 4: Veröffentlichen folgt keiner Verknüpfung, lässt Punkt-Dateien auf jeder Ebene weg, prüft HTML und große Dateien, ZIP genau aus der Liste")]
    public void Sec_publish_files()
    {
        var d = Dir(); var outside = Dir(); try
        {
            WebDesign.Save(d, """{"root":{"props":{"title":"Start","description":"x"}},"content":[],"zones":{}}""");
            Assert.Null(WebDesignExport.ExportSite(d, Editor()).Error);
            var outDir = Path.Combine(d, WebDesign.ExportDir);
            File.WriteAllText(Path.Combine(outside, "geheim.txt"), "nichts");
            var linked = Junction(Path.Combine(outDir, "ext"), outside);
            Directory.CreateDirectory(Path.Combine(outDir, "sub", ".git")); File.WriteAllText(Path.Combine(outDir, "sub", ".git", "config"), "x"); File.WriteAllText(Path.Combine(outDir, "sub", ".env.local"), "x");
            var plan = WebDesignPublish.Prepare(d, []);
            Assert.True(plan.Ok, string.Join(" | ", plan.Blockers));
            if (linked) Assert.DoesNotContain(plan.Files, f => f.Rel.StartsWith("ext/", StringComparison.Ordinal));
            Assert.DoesNotContain(plan.Files, f => f.Rel.Contains("/."));
            var (zip, err) = WebDesignPublish.ZipExactly(d, plan);
            Assert.Null(err);
            using (var z = new System.IO.Compression.ZipArchive(new MemoryStream(zip!))) Assert.Equal(plan.Files.Select(f => f.Rel), z.Entries.Select(e => e.FullName));
            File.WriteAllText(Path.Combine(outDir, "info.html"), "<p>api_key = \"sk-1234567890abcdefghij1234567890ab\"</p>");
            File.WriteAllText(Path.Combine(outDir, "gross.js"), new string('x', 1_200_000) + "\nconst token = \"ghp_123456789012345678901234567890123456\";");
            File.WriteAllText(Path.Combine(outDir, "unbekannt.bin2"), "x");
            var bad = WebDesignPublish.Prepare(d, []);
            Assert.Contains(bad.Blockers, b => b.Contains("info.html"));
            Assert.Contains(bad.Blockers, b => b.Contains("gross.js"));
            Assert.Contains(bad.Blockers, b => b.Contains("unbekannt.bin2"));
            File.AppendAllText(Path.Combine(outDir, "index.html"), " ");
            Assert.NotNull(WebDesignPublish.ZipExactly(d, plan).Error); // nach Änderung kein ZIP mit alter Zustimmung
        }
        finally { try { Directory.Delete(Path.Combine(d, WebDesign.ExportDir, "ext")); } catch (IOException) { } Clean(d); Clean(outside); }
    }

    [Fact(DisplayName = "Sicherheit 5: Editor bekommt eine veränderte Seiten- oder Variantendatei nur bereinigt")]
    public void Sec_editor_gets_sanitized()
    {
        var d = Dir(); try
        {
            WebDesign.Save(d, """{"root":{"props":{"title":"S"}},"content":[],"zones":{}}""");
            File.WriteAllText(Path.Combine(d, WebDesign.PageFile), """{"root":{"props":{"title":"S"}},"content":[{"type":"Knopf","props":{"id":"k","label":"x","href":"javascript:alert(1)"}},{"type":"Html","props":{"id":"h","knoten":[{"tag":"script","attrs":{},"kinder":[{"text":"alert(2)"}]}]}}],"zones":{}}""");
            var read = WebDesignStates.Read(d);
            Assert.DoesNotContain("javascript:", read); Assert.DoesNotContain("alert(2)", read);
        }
        finally { Clean(d); }
    }

    [Fact(DisplayName = "Sicherheit 6/7/8: Figma-Bildnamen eindeutig; nur Figma-Speicher; falscher Typ bei „visible“ ohne Absturz")]
    public void Sec_figma_details()
    {
        Assert.NotEqual(WebDesignFigma.ImageName("AbCdEf123456", "1:23"), WebDesignFigma.ImageName("AbCdEf123456", "12:3"));
        Assert.NotEqual(WebDesignFigma.ImageName("AbCdEf123456", "1:2"), WebDesignFigma.ImageName("ZyXwVu987654", "1:2"));
        Assert.True(WebDesignFigma.IsImageUrl("https://figma-alpha-api.s3.us-west-2.amazonaws.com/images/x"));
        Assert.False(WebDesignFigma.IsImageUrl("https://fremder-bucket.s3.amazonaws.com/x.png"));
        Assert.False(WebDesignFigma.IsImageUrl("https://s3-alpha.figma.com:8443/x.png"));
        var file = JsonNode.Parse("""{"name":"X","document":{"children":[{"type":"CANVAS","children":[{"type":"FRAME","children":[{"type":"TEXT","characters":"sichtbar?","visible":"nein","style":{"fontSize":12}},{"type":"RECTANGLE","id":"1:2","fills":[{"type":"IMAGE","visible":"ja"}]},{"type":"TEXT","characters":"Bleibt","style":{"fontSize":12}}]}]}]}}""")!.AsObject();
        var m = WebDesignFigma.Map(file);
        Assert.Contains("Bleibt", m.Content.ToJsonString()); Assert.DoesNotContain("sichtbar?", m.Content.ToJsonString()); Assert.Empty(m.ImageNodes);
    }

    [Fact(DisplayName = "Sicherheit 9: Bild hinter einer Verknüpfung im Quellordner wird beim Import nicht kopiert")]
    public void Sec_import_image_through_junction()
    {
        var src = Dir(); var outside = Dir(); var proj = Dir(); try
        {
            File.WriteAllBytes(Path.Combine(outside, "fremd.png"), [137, 80, 78, 71]);
            if (!Junction(Path.Combine(src, "verlinkt"), outside)) return; // ohne mklink (kein Windows) nicht prüfbar
            var r = WebDesignHtml.FromTree(new JsonArray(E("img", new JsonObject { ["src"] = "verlinkt/fremd.png" })), new WebDesignHtml.Context(proj, src));
            Assert.False(Directory.Exists(Path.Combine(proj, "bilder", "import")) && Directory.GetFiles(Path.Combine(proj, "bilder", "import")).Length > 0);
            Assert.Contains(r.Report, x => x.Contains("fremd.png"));
        }
        finally { try { Directory.Delete(Path.Combine(src, "verlinkt")); } catch (IOException) { } Clean(src); Clean(outside); Clean(proj); }
    }
}
