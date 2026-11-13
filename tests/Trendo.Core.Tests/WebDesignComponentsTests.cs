using System.Text.Json.Nodes;
using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0621 Teil 2: W5 wiederverwendbare Komponenten.</summary>
public class WebDesignComponentsTests
{
    const string Page = """{"root":{"props":{"title":"Seite","description":"Kurz"}},"content":[{"type":"Ueberschrift","props":{"id":"u","text":"Paket Basis","level":"h2"}},{"type":"Text","props":{"id":"t","text":"Für den Anfang","align":"left"}},{"type":"Bereich","props":{"id":"b","tone":"hell","content":[]}}],"zones":{}}""";
    static string Plan(params string[] actions) => "{\"aktionen\":[" + string.Join(",", actions) + "]}";
    static string Ok(string page, params string[] actions) { var r = WebDesignActions.Apply(page, Plan(actions)); Assert.True(r.Error is null, r.Error); return r.Json!; }
    static string Dir() => Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "trendo-web2k-" + Guid.NewGuid().ToString("N"))).FullName;
    static JsonArray Expanded(string json) => WebDesignComponents.Expand(JsonNode.Parse(json)!.AsObject())["content"]!.AsArray();
    static string Text(JsonArray c, string id) => c.OfType<JsonObject>().Single(b => b["props"]!["id"]!.ToString() == id)["props"]!["text"]!.ToString();

    static string WithTwoUses() => Ok(Page,
        """{"aktion":"komponente_erstellen","name":"Preiskarte","ids":["t","u"],"id":"k1"}""",
        """{"aktion":"komponente_einfuegen","komponente":"K-1","id":"k2"}""",
        """{"aktion":"komponente_ueberschreiben","id":"k2","element":"u","props":{"text":"Paket Profi"}}""");

    [Fact(DisplayName = "W5: Bausteingruppe wird zur Komponente, an ihrer Stelle steht eine Verwendung")]
    public void Group_becomes_component()
    {
        var j = WithTwoUses();
        var doc = JsonNode.Parse(j)!;
        Assert.Equal(["Komponente", "Bereich", "Komponente"], doc["content"]!.AsArray().Select(b => b!["type"]!.ToString()));
        var def = WebDesignComponents.Read(doc["root"]!["props"]!["komponenten"]).Single();
        Assert.Equal(("K-1", "Preiskarte"), (def.Id, def.Name));
        Assert.Equal(["u", "t"], def.Blocks.Select(b => b!["props"]!["id"]!.ToString())); // Reihenfolge der Seite, nicht der Angabe
    }

    [Fact(DisplayName = "W5: Original ändern ändert beide Verwendungen, außer überschriebene Felder")]
    public void Original_changes_all_uses_except_overrides()
    {
        var j = Ok(WithTwoUses(), """{"aktion":"komponente_aendern","komponente":"K-1","element":"u","props":{"text":"Tarif Basis"}}""",
            """{"aktion":"komponente_aendern","komponente":"K-1","element":"t","props":{"text":"Monatlich kündbar"}}""");
        var c = Expanded(j);
        Assert.Equal(("Tarif Basis", "Monatlich kündbar"), (Text(c, "k1--u"), Text(c, "k1--t")));
        Assert.Equal(("Paket Profi", "Monatlich kündbar"), (Text(c, "k2--u"), Text(c, "k2--t"))); // überschrieben bleibt, der Rest folgt dem Original
        var reset = Ok(j, """{"aktion":"komponente_zuruecksetzen","id":"k2","element":"u"}""");
        Assert.Equal("Tarif Basis", Text(Expanded(reset), "k2--u"));
        Assert.Null(JsonNode.Parse(reset)!["content"]![2]!["props"]!["ueberschreibungen"]);
    }

    [Fact(DisplayName = "W5: Komponenten gelten projektweit; eine Variante behält ihre eigene Fassung, bis sie übernommen wird")]
    public void Project_wide()
    {
        var d = Dir(); try
        {
            Assert.Null(WebDesignStates.Write(d, WithTwoUses()));
            Assert.Single(WebDesignComponents.Load(d));
            File.WriteAllText(Path.Combine(d, WebDesign.PageFile), WebDesign.Validate(WithTwoUses().Replace("\"komponenten\"", "\"x\""))!.Json!); // Seite ohne eingebettete Komponenten
            Assert.Contains("Preiskarte", WebDesignStates.Read(d)); // beim Laden aus dem Projekt eingesetzt
            var (outDir, err) = WebDesign.Export(d, Path.Combine(WebDesignKiTestsRepo(), "src", "Trendo.App", "Assets", "webdesign")); Assert.Null(err);
            Assert.Contains("Preiskarte", File.ReadAllText(Path.Combine(outDir!, "seite.js")));
            var (v, _) = WebDesignStates.CreateVariant(d, "B");
            var changed = Ok(WebDesignStates.Read(d, v!.Id), """{"aktion":"komponente_aendern","komponente":"K-1","element":"u","props":{"text":"Nur in der Variante"}}""");
            Assert.Null(WebDesignStates.Write(d, changed, v.Id));
            Assert.DoesNotContain("Nur in der Variante", WebDesignStates.Read(d));
        }
        finally { Directory.Delete(d, true); }
    }

    static string WebDesignKiTestsRepo()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent) if (File.Exists(Path.Combine(dir.FullName, "Trendo.sln"))) return dir.FullName;
        throw new DirectoryNotFoundException();
    }

    [Fact(DisplayName = "W5: Eigene Vorlage speichern und in ein anderes Projekt holen")]
    public void Templates()
    {
        var d = Dir(); try
        {
            var def = WebDesignComponents.Read(JsonNode.Parse(WithTwoUses())!["root"]!["props"]!["komponenten"]).Single();
            var (file, replaced) = WebDesignComponents.SaveTemplate(Path.Combine(d, "vorlagen"), def); Assert.False(replaced);
            Assert.EndsWith("preiskarte.json", file);
            var t = WebDesignComponents.Templates(Path.Combine(d, "vorlagen")).Single();
            var other = Ok(WebDesign.Empty(), new JsonObject { ["aktion"] = "komponente_definieren", ["name"] = t.Name, ["komponente"] = "V-1", ["bausteine"] = WebDesignComponents.TemplateBlocks(t.Blocks) }.ToJsonString(),
                """{"aktion":"komponente_einfuegen","komponente":"V-1","id":"x"}""");
            Assert.Equal("Paket Basis", Text(Expanded(other), "x--u"));
            File.WriteAllText(Path.Combine(d, "vorlagen", "kaputt.json"), "[]");
            Assert.Single(WebDesignComponents.Templates(Path.Combine(d, "vorlagen")));
        }
        finally { Directory.Delete(d, true); }
    }

    [Theory(DisplayName = "W5: Ungültige Komponenten-Aktionen werden abgelehnt")]
    [InlineData("""{"aktion":"komponente_erstellen","name":"X","ids":["u","gibtsnicht"]}""", "gibt es nicht")]
    [InlineData("""{"aktion":"komponente_erstellen","name":"<b>","ids":["u"]}""", "Name der Komponente")]
    [InlineData("""{"aktion":"komponente_erstellen","name":"X","ids":[]}""", "Liste")]
    [InlineData("""{"aktion":"komponente_einfuegen","komponente":"K-9"}""", "gibt es nicht")]
    [InlineData("""{"aktion":"baustein_einfuegen","typ":"Komponente"}""", "komponente_einfuegen")]
    [InlineData("""{"aktion":"komponente_definieren","name":"Y","bausteine":[{"typ":"Komponente"}]}""", "keine Verwendung")]
    [InlineData("""{"aktion":"komponente_definieren","name":"Y","bausteine":[{"typ":"Text","props":{"farbe":"x"}}]}""", "kein Feld")]
    [InlineData("""{"aktion":"komponente_definieren","name":"Y","bausteine":[{"typ":"Text","id":"a"},{"typ":"Text","id":"a"}]}""", "doppelt")]
    [InlineData("""{"aktion":"komponente_definieren","name":"Y","bausteine":[{"typ":"Text","skript":"x"}]}""", "unbekannte Angabe")]
    public void Rejects(string action, string expected) => Assert.Contains(expected, WebDesignActions.Apply(Page, Plan(action)).Error);

    [Fact(DisplayName = "W5: Gleiche Namen, falsche Ziele und Bereiche beim Überschreiben werden abgelehnt; Bausteine müssen in derselben Liste liegen")]
    public void Rejects_in_context()
    {
        var j = WithTwoUses();
        Assert.Contains("gibt es schon", WebDesignActions.Apply(j, Plan("""{"aktion":"komponente_definieren","name":"preiskarte","bausteine":[{"typ":"Text"}]}""")).Error);
        Assert.Contains("gibt es in der Komponente nicht", WebDesignActions.Apply(j, Plan("""{"aktion":"komponente_ueberschreiben","id":"k1","element":"zz","props":{"text":"x"}}""")).Error);
        Assert.Contains("keine Verwendung", WebDesignActions.Apply(j, Plan("""{"aktion":"komponente_ueberschreiben","id":"b","element":"u","props":{"text":"x"}}""")).Error);
        var nested = Ok(Page, """{"aktion":"baustein_einfuegen","typ":"Text","id":"innen","in":"b"}""");
        Assert.Contains("derselben Liste", WebDesignActions.Apply(nested, Plan("""{"aktion":"komponente_erstellen","name":"X","ids":["u","innen"]}""")).Error);
        Assert.Contains("nicht zur Auswahl", WebDesignActions.Apply(j, Plan("""{"aktion":"komponente_ueberschreiben","id":"k2","element":"u","props":{"text":"x"}}"""), new WebPlanScope(new HashSet<string> { "k1" })).Error);
        Assert.Contains("mehr als die Auswahl", WebDesignActions.Apply(j, Plan("""{"aktion":"komponente_aendern","komponente":"K-1","element":"u","props":{"text":"x"}}"""), new WebPlanScope(new HashSet<string> { "k1" })).Error);
    }

    [Fact(DisplayName = "W5: Prüfung sieht in die Komponente hinein; ungültige Komponenten aus dem Editor fallen beim Speichern weg")]
    public void Check_and_sanitize()
    {
        var j = Ok(Page, """{"aktion":"komponente_definieren","name":"Bildkarte","bausteine":[{"typ":"Bild","id":"bild","props":{"src":"fehlt.png","alt":""}}]}""", """{"aktion":"komponente_einfuegen","komponente":"K-1","id":"bk"}""");
        Assert.Contains(WebDesignCheck.CheckData(j, Path.GetTempPath()), f => f.Code == "alt" && f.Text.Contains("bk--bild"));
        var junk = Page.Replace("\"description\":\"Kurz\"", "\"description\":\"Kurz\",\"komponenten\":[{\"id\":\"../x\",\"name\":\"A\",\"bausteine\":[{\"type\":\"Text\"}]},{\"id\":\"ok\",\"name\":\"B\",\"bausteine\":[{\"type\":\"Komponente\"}]},{\"id\":\"gut\",\"name\":\"C\",\"bausteine\":[{\"type\":\"Text\",\"props\":{\"href\":\"javascript:x\"}}]}]");
        var defs = WebDesignComponents.Read(JsonNode.Parse(WebDesign.Validate(junk).Json!)!["root"]!["props"]!["komponenten"]);
        Assert.Equal(["gut"], defs.Select(c => c.Id));
        Assert.Equal("#", defs[0].Blocks[0]!["props"]!["href"]!.ToString()); // gefährliche Links auch im Original entschärft
    }
}
