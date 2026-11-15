using System.IO.Compression;
using System.Text.Json.Nodes;
using Trendo.Core;
using Trendo.Core.Bibliothek;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0622 Teil 3: Befunde aus QA und Security-Prüfung zu A4, K7, Z3 und Export.</summary>
public class WebDesignStufe3Teil3QaTests
{
    static string Dir() => Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "trendo-web3d-" + Guid.NewGuid().ToString("N"))).FullName;
    static void Clean(string d) { try { foreach (var f in Directory.EnumerateFiles(d, "*", SearchOption.AllDirectories)) File.SetAttributes(f, FileAttributes.Normal); Directory.Delete(d, true); } catch (IOException) { } }
    static string Repo()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent) if (File.Exists(Path.Combine(d.FullName, "Trendo.sln"))) return d.FullName;
        throw new DirectoryNotFoundException();
    }
    static string Editor() => Path.Combine(Repo(), "src", "Trendo.App", "Assets", "webdesign");
    const string Page = """{"root":{"props":{"title":"Start","description":"Beschreibung der Startseite"}},"content":[{"type":"Text","props":{"id":"t","text":"Hallo","align":"left"}}],"zones":{}}""";

    static string Mod(string root, string vorlage = """{"name":"Angebot","inhalt":[{"type":"Ueberschrift","props":{"id":"a","text":"Unser Angebot","level":"h2"}}]}""")
    {
        var d = Directory.CreateDirectory(Path.Combine(root, "mod-" + Guid.NewGuid().ToString("N")[..6])).FullName;
        File.WriteAllText(Path.Combine(d, WebDesignMods.ManifestName), """{"id":"test.mod","name":"Test","version":"1.0.0","beschreibung":"Vorlagen für einen Test.","lizenz":"MIT","rechte":["vorlagen"]}""");
        Directory.CreateDirectory(Path.Combine(d, "vorlagen"));
        File.WriteAllText(Path.Combine(d, "vorlagen", "angebot.json"), vorlage);
        return d;
    }

    [Fact(DisplayName = "A4 (QA 11, Security M2): Html und Komponente sind auch in Slots einer Mod-Vorlage verboten")]
    public void Mod_nested_blocks_checked()
    {
        var root = Dir(); try
        {
            var nested = Mod(root, """{"name":"A","inhalt":[{"type":"Bereich","props":{"id":"b","tone":"hell","content":[{"type":"Html","props":{"id":"h","knoten":[]}}]}}]}""");
            Assert.Null(WebDesignMods.Check(nested).Mod);
            var nestedComp = Mod(root, """{"name":"A","inhalt":[{"type":"Bereich","props":{"id":"b","tone":"hell","content":[{"type":"Komponente","props":{"id":"k","komponente":"x"}}]}}]}""");
            Assert.Null(WebDesignMods.Check(nestedComp).Mod);
            var ok = Mod(root, """{"name":"A","inhalt":[{"type":"Bereich","props":{"id":"b","tone":"hell","content":[{"type":"Text","props":{"id":"t","text":"Hallo","align":"left"}}]}}]}""");
            Assert.NotNull(WebDesignMods.Check(ok).Mod);
        }
        finally { Clean(root); }
    }

    [Fact(DisplayName = "A4 (QA 5, 10): ZIP mit Hauptordner wird angenommen; Neu-Installieren aus dem installierten Ordner wird abgelehnt, die Mod bleibt erhalten")]
    public void Mod_zip_root_and_reinstall()
    {
        var root = Dir(); var proj = Dir(); try
        {
            var src = Mod(root);
            var zip = Path.Combine(root, "mod.zip");
            using (var z = ZipFile.Open(zip, ZipArchiveMode.Create))
                foreach (var f in Directory.GetFiles(src, "*", SearchOption.AllDirectories)) z.CreateEntryFromFile(f, "meine-mod/" + Path.GetRelativePath(src, f).Replace('\\', '/'));
            var prep = WebDesignMods.Prepare(zip);
            Assert.NotNull(prep.Mod);
            Assert.Null(WebDesignMods.Install(proj, prep, prep.Fingerprint));
            WebDesignMods.Cleanup(prep);
            var installed = Path.Combine(proj, WebDesignMods.Folder, "test.mod");
            var again = WebDesignMods.Prepare(installed);
            Assert.NotNull(again.Mod);
            Assert.Contains("schon im Projekt", WebDesignMods.Install(proj, again, again.Fingerprint));
            Assert.True(File.Exists(Path.Combine(installed, WebDesignMods.ManifestName))); // nichts verloren
            Assert.Single(WebDesignMods.Installed(proj));
            // Erneuern aus anderer Quelle tauscht vollständig aus
            var neu = WebDesignMods.Prepare(src);
            Assert.Null(WebDesignMods.Install(proj, neu, neu.Fingerprint));
            Assert.Single(WebDesignMods.Installed(proj));
            Assert.False(Directory.Exists(Path.Combine(proj, WebDesignMods.Folder, ".neu-test.mod")));
        }
        finally { Clean(root); Clean(proj); }
    }

    [Fact(DisplayName = "Export (QA 6, 7, 8): schreibgeschütztes .git stört nicht; Sicherung fremder Dateien bleibt über mehrere Exporte; eigene Dateien unter daten/ bleiben")]
    public void Export_git_backup_daten()
    {
        var d = Dir(); try
        {
            Assert.Null(WebDesign.Save(d, Page));
            var outDir = Path.Combine(d, WebDesign.ExportDir);
            var obj = Directory.CreateDirectory(Path.Combine(outDir, ".git", "objects", "ab")).FullName;
            var gitFile = Path.Combine(obj, "cdef"); File.WriteAllText(gitFile, "x"); File.SetAttributes(gitFile, FileAttributes.ReadOnly);
            File.WriteAllText(Path.Combine(outDir, "index.html"), "<html><body>Meine alte Seite</body></html>");
            File.WriteAllText(Path.Combine(outDir, "seite.js"), "window.TRENDO_SEITE={}"); // Spur eines alten Trendo-Exports ohne Liste
            Directory.CreateDirectory(Path.Combine(outDir, "daten"));
            File.WriteAllText(Path.Combine(outDir, "daten", "preisliste.pdf"), "%PDF-1.4 eigene Datei");
            var pdf = Path.Combine(outDir, "dokument.pdf"); File.WriteAllText(pdf, "%PDF-1.4"); File.SetAttributes(pdf, FileAttributes.ReadOnly); // fremd und schreibgeschützt (QA N1)
            for (var i = 0; i < 3; i++) Assert.Null(WebDesignExport.ExportSite(d, Editor()).Error);
            Assert.True(File.Exists(pdf));
            Assert.True(File.Exists(gitFile));
            Assert.True(File.Exists(Path.Combine(outDir, "daten", "preisliste.pdf")));
            var backup = Path.Combine(outDir, ".trendo", "vorher", "index.html");
            Assert.True(File.Exists(backup)); Assert.Contains("Meine alte Seite", File.ReadAllText(backup));
        }
        finally { Clean(d); }
    }

    [Fact(DisplayName = "K7 (QA 13, 14, Security 8): SQL ohne öffentliche Leseregel, mit Längengrenze; reservierte Feldnamen werden gemeldet")]
    public void Data_sql_and_reserved_fields()
    {
        var dl = JsonNode.Parse("""{"tabelle":"gaeste","liste":"alle","felder":[{"name":"name"},{"name":"id"},{"name":"erstellt"}]}""")!.AsObject();
        var sql = WebDesignData.SupabaseSql([dl]);
        Assert.DoesNotContain("for select", sql);
        Assert.Contains("length(name) <= 5000", sql);
        Assert.DoesNotContain("id text", sql); Assert.DoesNotContain("erstellt text", sql);
        var d = Dir(); try
        {
            Assert.Null(WebDesign.Save(d, """{"root":{"props":{"title":"Start","description":"Gästebuch der Seite"}},"content":[{"type":"Datenliste","props":{"id":"dl","titel":"G","tabelle":"gaeste","felder":[{"name":"erstellt","beschriftung":"E","art":"text","pflicht":"nein"}],"knopf":"Speichern","nurAngemeldet":"nein","liste":"alle"}}],"zones":{}}"""));
            Assert.Contains(WebDesignSeo.Check(d), f => f.Ebene == "Fehler" && f.Text.Contains("„erstellt“"));
        }
        finally { Clean(d); }
    }

    [Fact(DisplayName = "Z3 (Security 5): Blender lädt keine Skripte aus .blend und bettet für Websites nur Texturen aus dem Modellordner ein")]
    public void Blender_hardening()
    {
        var blend = ModelConverter.Script("/m/haus.blend", "/o/haus.glb", onlyLocalTextures: true);
        Assert.Contains("use_scripts=False", blend);
        Assert.Contains("commonpath", blend);
        Assert.DoesNotContain("commonpath", ModelConverter.Script("/m/haus.fbx", "/o/haus.glb")); // Bibliothek: wie bisher
    }

    /// <summary>Mod mit Aktion (Angabe „anzahl“), Bild und wählbarer Lizenz, installiert in <paramref name="proj"/>.</summary>
    static void InstallShopMod(string root, string proj, string lizenz, string autor)
    {
        var d = Directory.CreateDirectory(Path.Combine(root, "mod-" + Guid.NewGuid().ToString("N")[..6])).FullName;
        File.WriteAllText(Path.Combine(d, WebDesignMods.ManifestName), $$"""{"id":"laden.bausteine","name":"Laden","version":"1.0.0","beschreibung":"Bausteine für einen Laden.","lizenz":"{{lizenz}}","autor":"{{autor}}","rechte":["vorlagen","aktionen"]}""");
        foreach (var s in new[] { "vorlagen", "aktionen", "bilder" }) Directory.CreateDirectory(Path.Combine(d, s));
        File.WriteAllBytes(Path.Combine(d, "bilder", "brot.png"), [137, 80, 78, 71]);
        File.WriteAllText(Path.Combine(d, "vorlagen", "angebot.json"), """{"name":"Angebot","inhalt":[{"type":"Bild","props":{"id":"b","src":"bilder/brot.png","alt":"Brot"}}]}""");
        File.WriteAllText(Path.Combine(d, "aktionen", "vorrat.json"), """{"name":"vorrat","beschreibung":"Zeigt den Vorrat an.","parameter":["anzahl"],"schritte":[{"aktion":"baustein_einfuegen","typ":"Text","id":"vorrat","props":{"text":"Noch {{anzahl}} Stück","align":"left"}}]}""");
        var prep = WebDesignMods.Prepare(d);
        Assert.Null(prep.Error);
        Assert.Null(WebDesignMods.Install(proj, prep, prep.Fingerprint));
    }

    static string Text(string? plan) => JsonNode.Parse(plan!)!["aktionen"]![0]!["props"]!["text"]!.ToString();

    [Fact(DisplayName = "A4 (QA 20): Zahlen und Ja/Nein als Mod-Angaben werden als Text eingesetzt; Listen werden mit Meldung abgelehnt")]
    public void Mod_values_numbers()
    {
        var root = Dir(); var proj = Dir(); try
        {
            InstallShopMod(root, proj, "MIT", "Beispiel");
            var mods = WebDesignMods.Installed(proj);
            var (plan, err) = WebDesignMods.Expand("""{"aktionen":[{"aktion":"mod:laden.bausteine/vorrat","werte":{"anzahl":3}}]}""", mods);
            Assert.Null(err); Assert.Contains("Noch 3 Stück", Text(plan));
            Assert.Contains("Noch 2.5 Stück", Text(WebDesignMods.Expand("""{"aktionen":[{"aktion":"mod:laden.bausteine/vorrat","werte":{"anzahl":2.5}}]}""", mods).Plan));
            Assert.Contains("Noch ja Stück", Text(WebDesignMods.Expand("""{"aktionen":[{"aktion":"mod:laden.bausteine/vorrat","werte":{"anzahl":true}}]}""", mods).Plan));
            var bad = WebDesignMods.Expand("""{"aktionen":[{"aktion":"mod:laden.bausteine/vorrat","werte":{"anzahl":[1,2]}}]}""", mods);
            Assert.Null(bad.Plan); Assert.Contains("„anzahl“", bad.Error);
        }
        finally { Clean(root); Clean(proj); }
    }

    [Fact(DisplayName = "A4 (QA 22): Bilder einer Mod gelten mit der Lizenz der Mod; CC-BY ergibt eine Namensnennung, ohne Autor einen Fehler")]
    public void Mod_images_license()
    {
        var root = Dir(); var proj = Dir(); try
        {
            InstallShopMod(root, proj, "CC-BY-4.0", "Bäckerei Beispiel");
            var page = JsonNode.Parse("""{"root":{"props":{"title":"Start"}},"content":[{"type":"Bild","props":{"id":"b","src":"mods/laden.bausteine/bilder/brot.png","alt":"Brot"}}],"zones":{}}""");
            var f = WebDesignAssets.Check(proj, page);
            Assert.DoesNotContain(f, x => x.Code == "license-unknown");
            Assert.DoesNotContain(f, x => x.Level == "Fehler");
            var credit = Assert.Single(WebDesignAssets.AttributionFor(proj, page));
            Assert.Equal("Bäckerei Beispiel", credit.Urheber); Assert.Equal("CC-BY-4.0", credit.Lizenz);
            Assert.Contains(System.Net.WebUtility.HtmlEncode("Bäckerei Beispiel"), WebDesignAssets.AttributionHtml([credit]));
            Clean(proj); proj = Dir();
            InstallShopMod(root, proj, "CC-BY-4.0", "");
            Assert.Contains(WebDesignAssets.Check(proj, page), x => x.Code == "license-author");
        }
        finally { Clean(root); Clean(proj); }
    }

    [Fact(DisplayName = "Z6: Antworten denkender Modelle (<think>…</think> mit Klammern) ergeben den Plan dahinter; nur Überlegung ohne Plan wird klar gemeldet")]
    public void Plan_after_think_block()
    {
        const string plan = """{"aktionen":[{"aktion":"element_aendern","id":"t","props":{"text":"Hallo"}}]}""";
        var (p, err) = WebDesignActions.Parse("<think>Ich brauche {\"aktionen\": …} und vielleicht { id: t }.</think>\n" + plan);
        Assert.Null(err); Assert.Equal("element_aendern", p!["aktionen"]![0]!["aktion"]!.ToString());
        Assert.Null(WebDesignActions.Parse("<THINK>{x}</THINK>```json\n" + plan + "\n```").Error);
        Assert.Contains("nur überlegt", WebDesignActions.Parse("<think>{ abgebrochen mitten im Gedanken").Error);
        Assert.Null(WebDesignActions.Parse(plan).Error); // ohne Denkblock wie bisher
    }
}
