using System.Text.Json.Nodes;
using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0621 Teil 1: A5 (Stapel), W1 mit Z4 (Ansichten, freie Breiten), W6 (zentrale Gestaltungswerte). Je Prüfung aus der Anforderungsliste mindestens ein Test.</summary>
public class WebDesignStufe2Tests
{
    const string Page = """{"root":{"props":{"title":"Seite","description":"Kurz"}},"content":[{"type":"Kopfbereich","props":{"id":"k1","title":"Titel","sub":"Text","cta":"Los","href":"#","motion":"keine","seconds":1}},{"type":"Text","props":{"id":"t1","text":"Absatz","align":"left"}},{"type":"Spalten","props":{"id":"sp","left":[],"right":[]}}],"zones":{}}""";
    static string Dir() => Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "trendo-web2-" + Guid.NewGuid().ToString("N"))).FullName;
    static string Plan(params string[] actions) => "{\"aktionen\":[" + string.Join(",", actions) + "]}";
    static string Ok(string page, params string[] actions) { var r = WebDesignActions.Apply(page, Plan(actions)); Assert.True(r.Error is null, r.Error); return r.Json!; }
    static JsonObject Doc(string json) => JsonNode.Parse(json)!.AsObject();
    static string Repo()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent) if (File.Exists(Path.Combine(d.FullName, "Trendo.sln"))) return d.FullName;
        throw new DirectoryNotFoundException();
    }

    // ---------- W1 ----------
    [Fact(DisplayName = "W1: Wert auf Tablet ändern lässt Desktop gleich und wirkt auf Handy; Handy kann wieder überschreiben")]
    public void W1_cascade()
    {
        var j = Ok(Page, """{"aktion":"anpassung_setzen","id":"t1","ansicht":"Tablet","werte":{"ausrichtung":"center","schrift":120}}""");
        var d = Doc(j);
        Assert.Null(WebDesignViews.Effective(d, "t1", "Desktop", "ausrichtung").Value);
        Assert.Equal("left", Prop(j, 1)["align"]!.ToString()); // Grundlage unverändert
        Assert.Equal(("center", "Tablet"), (WebDesignViews.Effective(d, "t1", "Tablet", "ausrichtung").Value!.ToString(), WebDesignViews.Effective(d, "t1", "Tablet", "ausrichtung").From));
        Assert.Equal(("center", "Tablet"), (WebDesignViews.Effective(d, "t1", "Handy", "ausrichtung").Value!.ToString(), WebDesignViews.Effective(d, "t1", "Handy", "ausrichtung").From)); // geerbt
        var j2 = Ok(j, """{"aktion":"anpassung_setzen","id":"t1","ansicht":"Handy","werte":{"ausrichtung":"right"}}""");
        Assert.Equal("right", WebDesignViews.Effective(Doc(j2), "t1", "Handy", "ausrichtung").Value!.ToString());
        Assert.Equal("center", WebDesignViews.Effective(Doc(j2), "t1", "Tablet", "ausrichtung").Value!.ToString()); // wirkt nicht nach oben
    }

    static JsonNode Prop(string json, int i) => JsonNode.Parse(json)!["content"]![i]!["props"]!;

    [Fact(DisplayName = "W1: Export erzeugt passende Medienregeln, breite Ansicht vor schmaler, nur für angepasste Bausteine")]
    public void W1_export_media_rules()
    {
        var j = Ok(Page, """{"aktion":"anpassung_setzen","id":"t1","ansicht":"Handy","werte":{"ausblenden":true}}""", """{"aktion":"anpassung_setzen","id":"t1","ansicht":"Tablet","werte":{"ausrichtung":"center"}}""",
            """{"aktion":"anpassung_setzen","id":"sp","ansicht":"Handy","werte":{"spalten":2}}""");
        Assert.Equal("@media (max-width: 1024px){[data-b=\"t1\"],.t-page [data-b=\"t1\"]>*,[data-b=\"t1\"] .t-hero{text-align:center}}\n@media (max-width: 600px){[data-b=\"t1\"]{display:none}[data-b=\"sp\"]>.t-cols-box>.t-cols{grid-template-columns:repeat(2,minmax(0,1fr))}}", WebDesignViews.Css(Doc(j)));
        Assert.StartsWith("@container seite (max-width: 1024px)", WebDesignViews.Css(Doc(j), container: true));
        var d = Dir(); try
        {
            WebDesign.Save(d, j);
            var (outDir, err) = WebDesign.Export(d, Path.Combine(Repo(), "src", "Trendo.App", "Assets", "webdesign")); Assert.Null(err);
            var html = File.ReadAllText(Path.Combine(outDir!, "index.html"));
            Assert.Contains("<style id=\"t-anpassungen\">", html); Assert.Contains("@media (max-width: 600px)", html);
            Assert.True(html.IndexOf("1024px", StringComparison.Ordinal) < html.IndexOf("600px", StringComparison.Ordinal));
        }
        finally { Directory.Delete(d, true); }
        Assert.Equal("", WebDesignViews.Css(Doc(Page)));
    }

    [Fact(DisplayName = "W1: Überschriebene Werte lassen sich einzeln oder ganz zurücksetzen")]
    public void W1_reset()
    {
        var j = Ok(Page, """{"aktion":"anpassung_setzen","id":"t1","ansicht":"Tablet","werte":{"ausrichtung":"center","schrift":120}}""");
        var one = Ok(j, """{"aktion":"anpassung_zuruecksetzen","id":"t1","ansicht":"Tablet","felder":["schrift"]}""");
        Assert.Equal("center", WebDesignViews.Effective(Doc(one), "t1", "Tablet", "ausrichtung").Value!.ToString());
        Assert.Null(WebDesignViews.Effective(Doc(one), "t1", "Tablet", "schrift").Value);
        var all = Ok(one, """{"aktion":"anpassung_zuruecksetzen","id":"t1","ansicht":"Tablet"}""");
        Assert.Null(Prop(all, 1)["anpassungen"]); // nichts mehr überschrieben, keine leeren Reste
    }

    [Theory(DisplayName = "W1: Ungültige Anpassungen werden abgelehnt")]
    [InlineData("""{"aktion":"anpassung_setzen","id":"t1","ansicht":"Desktop","werte":{"ausrichtung":"center"}}""", "Grundlage")]
    [InlineData("""{"aktion":"anpassung_setzen","id":"t1","ansicht":"Uhr","werte":{"ausrichtung":"center"}}""", "gibt es nicht")]
    [InlineData("""{"aktion":"anpassung_setzen","id":"t1","ansicht":"Handy","werte":{"spalten":2}}""", "nur bei Spalten")]
    [InlineData("""{"aktion":"anpassung_setzen","id":"t1","ansicht":"Handy","werte":{"schrift":300}}""", "50 bis 200")]
    [InlineData("""{"aktion":"anpassung_setzen","id":"t1","ansicht":"Handy","werte":{"farbe":"#000000"}}""", "lässt sich je Ansicht nicht")]
    [InlineData("""{"aktion":"anpassung_setzen","id":"t1","ansicht":"Handy","werte":{}}""", "nicht leeres")]
    [InlineData("""{"aktion":"element_aendern","id":"t1","props":{"anpassungen":{"Handy":{"ausrichtung":"center"}}}}""", "kein Feld")]
    public void W1_rejects(string action, string expected) => Assert.Contains(expected, WebDesignActions.Apply(Page, Plan(action)).Error);

    [Fact(DisplayName = "W1: Daten aus dem Editor werden bereinigt: unbekannte Ansicht, falsche Werte und Einschleusung fallen weg")]
    public void W1_sanitize()
    {
        var raw = Page.Replace("\"align\":\"left\"}", "\"align\":\"left\",\"anpassungen\":{\"Handy\":{\"ausrichtung\":\"center;}body{display:none\",\"schrift\":120},\"Uhr\":{\"schrift\":120},\"Tablet\":{\"spalten\":2}}}");
        var ok = WebDesign.Validate(raw).Json!;
        Assert.Equal("""{"Handy":{"schrift":120}}""", Prop(ok, 1)["anpassungen"]!.ToJsonString());
        var bad = """{"root":{"props":{"ansichten":[{"name":"Desktop","breite":900},{"name":"A","breite":5000},{"name":"B","breite":700},{"name":"C","breite":700},{"name":"</style>","breite":500}]}},"content":[],"zones":{}}""";
        Assert.Equal([("B", 700)], WebDesignViews.Of(Doc(WebDesign.Validate(bad).Json!)["root"]!["props"]!.AsObject()).Select(v => (v.Name, v.Breite)));
        var evil = Page.Replace("\"id\":\"t1\"", "\"id\":\"x\\\"]{}</style><script>\"").Replace("\"align\":\"left\"}", "\"align\":\"left\",\"anpassungen\":{\"Handy\":{\"schrift\":120}}}");
        Assert.Equal("", WebDesignViews.Css(Doc(WebDesign.Validate(evil).Json!))); // ungültige id: keine Regel
    }

    // ---------- Z4 ----------
    [Fact(DisplayName = "Z4: Eigene Ansicht mit Breite anlegen; die Anpassung gilt nur dort und darunter")]
    public void Z4_custom_view()
    {
        var j = Ok(Page, """{"aktion":"ansicht_setzen","name":"Großes Handy","breite":480}""", """{"aktion":"anpassung_setzen","id":"t1","ansicht":"Großes Handy","werte":{"ausrichtung":"center"}}""");
        var d = Doc(j);
        Assert.Equal(["Tablet", "Handy", "Großes Handy"], WebDesignViews.Of(d["root"]!["props"]!.AsObject()).Select(v => v.Name));
        Assert.Null(WebDesignViews.Effective(d, "t1", "Handy", "ausrichtung").Value); // breiter als 480: gilt nicht
        Assert.Equal("center", WebDesignViews.Effective(d, "t1", "Großes Handy", "ausrichtung").Value!.ToString());
        var below = Ok(j, """{"aktion":"ansicht_setzen","name":"Klein","breite":320}""");
        Assert.Equal(("center", "Großes Handy"), (WebDesignViews.Effective(Doc(below), "t1", "Klein", "ausrichtung").Value!.ToString(), WebDesignViews.Effective(Doc(below), "t1", "Klein", "ausrichtung").From)); // gilt auch darunter
        Assert.Contains("@media (max-width: 480px)", WebDesignViews.Css(d));
        var wider = Ok(j, """{"aktion":"ansicht_setzen","name":"großes handy","breite":500}""");
        Assert.Equal(500, WebDesignViews.Of(Doc(wider)["root"]!["props"]!.AsObject()).Single(v => v.Name == "Großes Handy").Breite); // Breite wählbar, Name bleibt
        var removed = Ok(wider, """{"aktion":"ansicht_entfernen","name":"Großes Handy"}""");
        Assert.DoesNotContain("anpassungen", removed); // Anpassungen der Ansicht gehen mit
        Assert.Contains("eine dieser Ansichten", WebDesignActions.Apply(removed, Plan("""{"aktion":"ansicht_anpassen","ansicht":"großes handy"}""")).Error);
    }

    [Theory(DisplayName = "Z4: Ungültige Ansichten werden abgelehnt")]
    [InlineData("""{"aktion":"ansicht_setzen","name":"Desktop","breite":900}""", "Grundlage")]
    [InlineData("""{"aktion":"ansicht_setzen","name":"Riesig","breite":5000}""", "zwischen")]
    [InlineData("""{"aktion":"ansicht_setzen","name":"Gleich","breite":600}""", "schon die Breite")]
    [InlineData("""{"aktion":"ansicht_setzen","name":"<b>","breite":500}""", "Name der Ansicht")]
    [InlineData("""{"aktion":"ansicht_setzen","name":"Halb","breite":500.5}""", "ganze Zahl")]
    [InlineData("""{"aktion":"ansicht_entfernen","name":"Uhr"}""", "gibt es nicht")]
    public void Z4_rejects(string action, string expected) => Assert.Contains(expected, WebDesignActions.Apply(Page, Plan(action)).Error);

    [Fact(DisplayName = "Z4: Bündel enthält die Container-Regel, nach der Spalten auf ihre eigene Breite reagieren")]
    public void Z4_container_and_parity()
    {
        var css = File.ReadAllText(Path.Combine(Repo(), "src", "Trendo.App", "Assets", "webdesign", "assets", "style.css"));
        Assert.Contains(".t-cols-box{container-type:inline-size}", css.Replace(" ", ""));
        Assert.Contains("@container(max-width:560px){.t-cols{grid-template-columns:1fr}}", css.Replace(" ", ""));
    }

    // ---------- W6 ----------
    [Fact(DisplayName = "W6: Wert ändern wirkt projektweit (Seite, neue Varianten, Export); eine bestehende Variante behält ihre eigene Gestaltung, bis sie übernommen wird (K5)")]
    public void W6_project_wide()
    {
        var d = Dir(); try
        {
            WebDesign.Save(d, Page);
            var (v, _) = WebDesignStates.CreateVariant(d, "B");
            var changed = Ok(WebDesignStates.Read(d), """{"aktion":"gestaltungswert_setzen","name":"accent","wert":"#0055AA"}""", """{"aktion":"gestaltungswert_setzen","name":"font","wert":"serif"}""");
            Assert.Null(WebDesignStates.Write(d, changed));
            Assert.Equal("#0055AA", WebDesignTokens.Load(d)!["accent"]!.ToString());
            Assert.DoesNotContain("0055AA", WebDesignStates.Read(d, v!.Id)); // bestehende Variante bleibt, wie sie war
            var (v2, _) = WebDesignStates.CreateVariant(d, "C");
            Assert.Equal(("#0055AA", "serif"), (Doc(WebDesignStates.Read(d, v2!.Id))["root"]!["props"]!["accent"]!.ToString(), Doc(WebDesignStates.Read(d, v2.Id))["root"]!["props"]!["font"]!.ToString())); // neue Variante übernimmt die Projektwerte
            var (outDir, err) = WebDesign.Export(d, Path.Combine(Repo(), "src", "Trendo.App", "Assets", "webdesign")); Assert.Null(err);
            Assert.Contains("#0055AA", File.ReadAllText(Path.Combine(outDir!, "seite.js")));
            var fromVariant = Ok(WebDesignStates.Read(d, v.Id), """{"aktion":"gestaltungswert_setzen","name":"accent","wert":"#226600"}""");
            Assert.Null(WebDesignStates.Write(d, fromVariant, v.Id));
            Assert.Equal("#0055AA", Doc(WebDesignStates.Read(d))["root"]!["props"]!["accent"]!.ToString()); // Variante ändert die Seite nicht (K5)
            Assert.Null(WebDesignStates.AdoptVariant(d, v.Id));
            Assert.Equal("#226600", WebDesignTokens.Load(d)!["accent"]!.ToString()); // übernommen: jetzt gilt ihre Gestaltung fürs Projekt
            var before = WebDesignStates.List(d).First(s => s.Name == "Vor Übernahme einer Variante");
            Assert.Null(WebDesignStates.Restore(d, before.Id));
            Assert.Equal("#0055AA", WebDesignTokens.Load(d)!["accent"]!.ToString()); // Wiederherstellen setzt auch die Gestaltung zurück
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "W6/K5: Rückgängig nach einer KI-Änderung setzt auch Gestaltungswerte exakt zurück")]
    public void W6_undo_restores_tokens()
    {
        var d = Dir(); try
        {
            var start = Ok(Page, """{"aktion":"gestaltungswert_setzen","name":"accent","wert":"#111111"}""");
            Assert.Null(WebDesignStates.Write(d, start));
            var r = WebDesignActions.Apply(WebDesignStates.Read(d), Plan("""{"aktion":"gestaltungswert_setzen","name":"accent","wert":"#ff0000"}"""));
            var (auto, err) = WebDesignActions.Commit(d, r, WebDesignStates.Read(d), "rot"); Assert.Null(err);
            Assert.Equal("#ff0000", WebDesignTokens.Load(d)!["accent"]!.ToString());
            Assert.Null(WebDesignStates.Restore(d, auto!));
            Assert.Equal("#111111", WebDesignTokens.Load(d)!["accent"]!.ToString());
            Assert.Equal("#111111", Doc(WebDesignStates.Read(d))["root"]!["props"]!["accent"]!.ToString());
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "W6: Benannte Werte als heller und dunkler Satz; zu geringer Kontrast wird gemeldet, auch im dunklen Satz")]
    public void W6_contrast()
    {
        Assert.Empty(WebDesignTokens.Check(new JsonObject()));
        var light = WebDesignTokens.Check(new JsonObject { ["ink"] = "#cccccc", ["paper"] = "#ffffff" });
        Assert.Contains(light, f => f.Code == "contrast" && f.Text.StartsWith("Text auf dem Hintergrund", StringComparison.Ordinal));
        var dark = new JsonObject { ["dunkel"] = true, ["inkDunkel"] = "#333333", ["paperDunkel"] = "#121212", ["accentDunkel"] = "#f0c75e" };
        Assert.Contains(WebDesignTokens.Check(dark), f => f.Text.Contains("(dunkel)"));
        dark["dunkel"] = false; Assert.Empty(WebDesignTokens.Check(dark)); // ausgeschalteter Satz zählt nicht
        Assert.Contains(WebDesignTokens.Check(new JsonObject { ["accent"] = "#ffff00" }), f => f.Text.StartsWith("Knopftext", StringComparison.Ordinal));
        var j = Ok(Page, """{"aktion":"gestaltungswert_setzen","name":"dunkel","wert":true}""", """{"aktion":"gestaltungswert_setzen","name":"inkDunkel","wert":"#222222"}""");
        Assert.Contains(WebDesignCheck.CheckData(j, Path.GetTempPath()), f => f.Code == "contrast" && f.Text.Contains("(dunkel)"));
        Assert.Equal(["Akzent", "Text", "Hintergrund"], new[] { "accent", "ink", "paper" }.Select(k => WebDesignTokens.Names[k]));
    }

    [Fact(DisplayName = "W6: Gestaltungsdatei mit ungültigen Werten wird bereinigt; keine Einschleusung über dunkle Farben")]
    public void W6_sanitized()
    {
        var d = Dir(); try
        {
            File.WriteAllText(Path.Combine(d, WebDesignTokens.FileName), """{"accent":"red;}body{","inkDunkel":"expression(1)","dunkel":"ja","radius":999,"fremd":"x"}""");
            var t = WebDesignTokens.Load(d)!;
            Assert.Equal(("#1d1d1f", "#f2f2f2", false, 40.0), (t["accent"]!.ToString(), t["inkDunkel"]!.ToString(), t["dunkel"]!.GetValue<bool>(), t["radius"]!.GetValue<double>()));
            Assert.Null(t["fremd"]);
            File.WriteAllText(Path.Combine(d, WebDesignTokens.FileName), "[]");
            Assert.Null(WebDesignTokens.Load(d));
            Assert.Contains("wahr oder falsch", WebDesignActions.Apply(Page, Plan("""{"aktion":"gestaltungswert_setzen","name":"dunkel","wert":"ja"}""")).Error);
        }
        finally { Directory.Delete(d, true); }
    }

    // ---------- A5 ----------
    [Fact(DisplayName = "A5: Abstände stammen aus den Gestaltungswerten: Stapel kennt nur Stufen, Pixel werden abgelehnt")]
    public void A5_spacing_from_tokens()
    {
        var j = Ok(Page, """{"aktion":"baustein_einfuegen","typ":"Stapel","id":"s","props":{"richtung":"waagerecht","abstand":3,"breite":"fuellt"}}""", """{"aktion":"baustein_einfuegen","typ":"Knopf","in":"s"}""");
        Assert.Equal(3, Prop(j, 3)["abstand"]!.GetValue<int>()); Assert.Single(Prop(j, 3)["inhalt"]!.AsArray());
        foreach (var bad in new[] { "24", "\"24px\"", "1.5", "9" })
            Assert.Contains("keine Pixel", WebDesignActions.Apply(Page, Plan("{\"aktion\":\"baustein_einfuegen\",\"typ\":\"Stapel\",\"props\":{\"abstand\":" + bad + "}}")).Error);
        Assert.Contains("keine Pixel", WebDesignActions.ComponentsText()); // die KI erfährt die Regel
        var bundle = File.ReadAllText(Path.Combine(Repo(), "src", "Trendo.App", "Assets", "webdesign", "assets", "editor.js"));
        Assert.Contains("calc(var(--t-space) * ", bundle);
        var css = File.ReadAllText(Path.Combine(Repo(), "src", "Trendo.App", "Assets", "webdesign", "assets", "style.css")).Replace(" ", "");
        Assert.Contains(".t-stack{display:flex;flex-wrap:wrap", css); Assert.Contains(".t-stack>*{min-width:0;max-width:100%}", css); // bricht um statt überzulaufen
    }

    [Fact(DisplayName = "A1 bleibt vollständig: Neue Editor-Funktionen sind geprüfte Aktionen mit Bedarf")]
    public void New_actions_in_catalog()
    {
        foreach (var n in new[] { "ansicht_setzen", "ansicht_entfernen", "anpassung_setzen", "anpassung_zuruecksetzen" }) Assert.Contains(WebDesignActions.Catalog, a => a.Name == n && a.Bedarf == KiBedarf.Text);
        Assert.True(WebDesignActions.Catalog.Single(a => a.Name == "anpassung_setzen").FuerAuswahl);
        Assert.Contains("nicht zur Auswahl", WebDesignActions.Apply(Page, Plan("""{"aktion":"anpassung_setzen","id":"t1","ansicht":"Handy","werte":{"schrift":120}}"""), new WebPlanScope(new HashSet<string> { "k1" })).Error);
        Assert.Contains("mehr als die Auswahl", WebDesignActions.Apply(Page, Plan("""{"aktion":"ansicht_setzen","name":"X","breite":300}"""), new WebPlanScope(new HashSet<string> { "k1" })).Error);
    }
}
