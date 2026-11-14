using System.Text.Json.Nodes;
using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0621 Teil 2: Z1 Profi-Bausteine, W4 Bewegung, Z2 Zeitleiste.</summary>
public class WebDesignTeil2Tests
{
    const string Page = """{"root":{"props":{"title":"Seite","description":"Kurz"}},"content":[{"type":"Text","props":{"id":"t","text":"Absatz","align":"left"}}],"zones":{}}""";
    static string Plan(params string[] actions) => "{\"aktionen\":[" + string.Join(",", actions) + "]}";
    static string Ok(string page, params string[] actions) { var r = WebDesignActions.Apply(page, Plan(actions)); Assert.True(r.Error is null, r.Error); return r.Json!; }
    static JsonNode Props(string json, string id) => JsonNode.Parse(json)!["content"]!.AsArray().Single(b => b!["props"]!["id"]!.ToString() == id)!["props"]!;

    // ---------- Z1 ----------
    [Theory(DisplayName = "Z1: Jeder Profi-Baustein lässt sich einfügen und bringt sinnvolle Inhalte mit")]
    [InlineData("Navigation", "links")] [InlineData("Hero", "titel")] [InlineData("Preise", "tarife")] [InlineData("Fragen", "fragen")]
    [InlineData("Galerie", "spalten")] [InlineData("Formular", "empfaenger")] [InlineData("Fusszeile", "impressum")]
    public void Z1_insert(string type, string field)
    {
        var j = Ok(Page, $$"""{"aktion":"baustein_einfuegen","typ":"{{type}}","id":"x"}""");
        Assert.NotNull(Props(j, "x")[field]);
    }

    [Theory(DisplayName = "Z1: Ungültige Inhalte der Profi-Bausteine werden abgelehnt")]
    [InlineData("""{"aktion":"baustein_einfuegen","typ":"Navigation","props":{"links":[{"label":"A","href":"javascript:alert(1)"}]}}""", "nicht erlaubt")]
    [InlineData("""{"aktion":"baustein_einfuegen","typ":"Navigation","props":{"links":[{"label":"A","ziel":"#"}]}}""", "kein Feld")]
    [InlineData("""{"aktion":"baustein_einfuegen","typ":"Formular","props":{"empfaenger":"keine-adresse"}}""", "E-Mail")]
    [InlineData("""{"aktion":"baustein_einfuegen","typ":"Formular","props":{"empfaenger":"a@b.de?subject=x"}}""", "E-Mail")]
    [InlineData("""{"aktion":"baustein_einfuegen","typ":"Galerie","props":{"bilder":[{"src":"../x.png","alt":"x"}]}}""", "Bilddatei")]
    [InlineData("""{"aktion":"baustein_einfuegen","typ":"Galerie","props":{"spalten":7}}""", "Stufe")]
    [InlineData("""{"aktion":"baustein_einfuegen","typ":"Preise","props":{"tarife":[{"hervorgehoben":"vielleicht"}]}}""", "einer von")]
    [InlineData("""{"aktion":"baustein_einfuegen","typ":"Preise","props":{"tarife":"Basis"}}""", "Liste")]
    public void Z1_rejects(string action, string expected) => Assert.Contains(expected, WebDesignActions.Apply(Page, Plan(action)).Error);

    [Fact(DisplayName = "Z1: Höchstzahl an Einträgen, Prüfung der Galerie- und Hero-Bilder, Hero zählt als Hauptüberschrift, Bilder reisen in den Export")]
    public void Z1_check_and_export()
    {
        var many = new JsonArray(Enumerable.Range(0, 13).Select(i => (JsonNode)new JsonObject { ["label"] = "L" + i, ["href"] = "#" }).ToArray());
        Assert.Contains("höchstens 12", WebDesignActions.Apply(Page, Plan(new JsonObject { ["aktion"] = "baustein_einfuegen", ["typ"] = "Navigation", ["props"] = new JsonObject { ["links"] = many } }.ToJsonString())).Error);
        var d = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "trendo-web2z1-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(d, "bilder")); File.WriteAllBytes(Path.Combine(d, "bilder", "a.png"), [1]);
            var j = Ok(Page, """{"aktion":"baustein_einfuegen","typ":"Hero","id":"h","props":{"bild":"bilder/a.png","bildAlt":""}}""",
                """{"aktion":"baustein_einfuegen","typ":"Galerie","id":"g","props":{"bilder":[{"src":"bilder/a.png","alt":"A"},{"src":"bilder/fehlt.png","alt":"B"}]}}""");
            var f = WebDesignCheck.CheckData(j, d);
            Assert.Contains(f, x => x.Code == "alt" && x.Text.Contains("„h“"));
            Assert.Contains(f, x => x.Code == "missing" && x.Text.Contains("g, Bild 2"));
            Assert.DoesNotContain(f, x => x.Code == "h1"); // Hero trägt die Hauptüberschrift
            WebDesign.Save(d, j);
            var editor = Path.Combine(Repo(), "src", "Trendo.App", "Assets", "webdesign");
            var (outDir, err) = WebDesign.Export(d, editor); Assert.Null(err);
            Assert.True(File.Exists(Path.Combine(outDir!, "bilder", "a.png")));
            Assert.Contains("lucide-react", File.ReadAllText(Path.Combine(outDir!, "assets", "LIZENZEN.txt")));
            Assert.Contains("(ISC)", File.ReadAllText(Path.Combine(editor, "LIZENZEN.txt")));
        }
        finally { Directory.Delete(d, true); }
    }

    static string Repo()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent) if (File.Exists(Path.Combine(dir.FullName, "Trendo.sln"))) return dir.FullName;
        throw new DirectoryNotFoundException();
    }

    // ---------- W4 ----------
    [Fact(DisplayName = "W4: Bewegung je Baustein (Erscheinen, Darüberfahren, Dauer, Verzögerung) und Seitenübergang als geprüfte Aktionen")]
    public void W4_actions()
    {
        var j = Ok(Page, """{"aktion":"bewegung_setzen","id":"t","erscheinen":"hochgleiten","darueber":"anheben","dauer":0.5}""", """{"aktion":"bewegung_setzen","id":"t","verzoegerung":0.2}""",
            """{"aktion":"seitenuebergang_setzen","art":"einblenden"}""");
        Assert.Equal("""{"erscheinen":"hochgleiten","darueber":"anheben","dauer":0.5,"verzoegerung":0.2}""", Props(j, "t")["bewegung"]!.ToJsonString());
        Assert.Equal("einblenden", JsonNode.Parse(j)!["root"]!["props"]!["uebergang"]!.ToString());
        Assert.Contains("einer von", WebDesignActions.Apply(Page, Plan("""{"aktion":"bewegung_setzen","id":"t","erscheinen":"wackeln"}""")).Error);
        Assert.Contains("Keine Einstellung", WebDesignActions.Apply(Page, Plan("""{"aktion":"bewegung_setzen","id":"t"}""")).Error);
        Assert.Contains("Zahl von 0,2", WebDesignActions.Apply(Page, Plan("""{"aktion":"bewegung_setzen","id":"t","dauer":9}""")).Error.Replace("0.2", "0,2"));
    }

    [Fact(DisplayName = "W4: Ersatzweg im Bündel – ohne Bewegung erlaubt bleibt alles ruhig; kein Baustein ist per Stil versteckt")]
    public void W4_fallback_in_bundle()
    {
        var css = File.ReadAllText(Path.Combine(Repo(), "src", "Trendo.App", "Assets", "webdesign", "assets", "style.css")).Replace(" ", "");
        Assert.Contains("@media(prefers-reduced-motion:no-preference){.t-hov-anheben", css);
        Assert.Contains("@media(prefers-reduced-motion:reduce)", css);
        Assert.DoesNotContain("[data-erscheinen]{opacity:0", css); // Inhalt nie per CSS versteckt: ohne Skript bleibt er sichtbar
        var js = File.ReadAllText(Path.Combine(Repo(), "src", "Trendo.App", "Assets", "webdesign", "assets", "editor.js"));
        Assert.Contains("prefers-reduced-motion: reduce", js);
    }

    // ---------- Z2 ----------
    [Fact(DisplayName = "Z2: Schlüsselbild setzen – Wert zur Zeit t stimmt (davor, dazwischen, danach, ohne Schlüsselbild)")]
    public void Z2_value_at_time()
    {
        var j = Ok(Page, """{"aktion":"schluesselbild_setzen","id":"t","eigenschaft":"y","zeit":0.5,"wert":-10}""", """{"aktion":"schluesselbild_setzen","id":"t","eigenschaft":"y","zeit":2,"wert":30}""",
            """{"aktion":"schluesselbild_setzen","id":"t","eigenschaft":"y","zeit":2,"wert":40}""", """{"aktion":"zeitleiste_setzen","id":"t","dauer":3,"ausloeser":"scroll"}""");
        var z = Props(j, "t")["zeitleiste"]!.AsObject(); var p = z["punkte"]!.AsArray();
        Assert.Equal(2, p.Count); // gleiche Zeit ersetzt
        Assert.Equal(-10, WebDesignMotion.ValueAt(p, "y", 0));
        Assert.Equal(15, WebDesignMotion.ValueAt(p, "y", 1.25), 9);
        Assert.Equal(40, WebDesignMotion.ValueAt(p, "y", 2.5));
        Assert.Equal(1, WebDesignMotion.ValueAt(p, "skalierung", 1));
        Assert.Equal(("scroll", 3.0), (z["ausloeser"]!.ToString(), z["dauer"]!.GetValue<double>()));
        var removed = Ok(j, """{"aktion":"schluesselbild_entfernen","id":"t","eigenschaft":"y","zeit":0.5}""");
        Assert.Single(Props(removed, "t")["zeitleiste"]!["punkte"]!.AsArray());
    }

    [Theory(DisplayName = "Z2: Ungültige Schlüsselbilder werden abgelehnt")]
    [InlineData("""{"aktion":"schluesselbild_setzen","id":"t","eigenschaft":"farbe","zeit":0,"wert":1}""", "einer von")]
    [InlineData("""{"aktion":"schluesselbild_setzen","id":"t","eigenschaft":"deckkraft","zeit":0,"wert":3}""", "zwischen 0 und 1")]
    [InlineData("""{"aktion":"schluesselbild_setzen","id":"t","eigenschaft":"y","zeit":5,"wert":1}""", "hinter der Dauer")]
    [InlineData("""{"aktion":"schluesselbild_entfernen","id":"t","eigenschaft":"y","zeit":1}""", "kein Schlüsselbild")]
    [InlineData("""{"aktion":"zeitleiste_setzen","id":"t","ausloeser":"klick"}""", "einer von")]
    [InlineData("""{"aktion":"element_aendern","id":"t","props":{"zeitleiste":{"punkte":[{"zeit":0,"eigenschaft":"y","wert":1,"js":"x"}]}}}""", "kein Feld")]
    public void Z2_rejects(string action, string expected) => Assert.Contains(expected, WebDesignActions.Apply(Page, Plan(action)).Error);
}
