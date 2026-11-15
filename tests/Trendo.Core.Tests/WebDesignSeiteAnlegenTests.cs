using System.Text.Json.Nodes;
using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0662: A1-Aktion „seite_anlegen“ mit ganzer Seite in einem Schritt (bausteine) und als weitere Seite der Website (adresse).</summary>
public class WebDesignSeiteAnlegenTests
{
    static string Dir() => Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "trendo-seite-" + Guid.NewGuid().ToString("N"))).FullName;
    const string Page = """{"root":{"props":{"title":"Start","description":"Beschreibung der Startseite"}},"content":[{"type":"Text","props":{"id":"t","text":"Hallo","align":"left"}}],"zones":{}}""";
    static string Plan(string action) => "{\"aktionen\":[" + action + "]}";
    static JsonArray Content(string json) => JsonNode.Parse(json)!["content"]!.AsArray();

    [Fact(DisplayName = "seite_anlegen: bausteine bauen die ganze Seite in einer Aktion, der Reihe nach, mit Standardwerten")]
    public void Builds_whole_page()
    {
        var r = WebDesignActions.Apply(Page, Plan("""
            {"aktion":"seite_anlegen","titel":"Bäckerei","beschreibung":"Frisch","bausteine":[
              {"typ":"Navigation","id":"nav","props":{"marke":"Bäckerei Korn"}},
              {"typ":"Kopfbereich","id":"held","props":{"title":"Frisches Brot","sub":"Jeden Morgen ab sechs"}},
              {"typ":"Ueberschrift","props":{"text":"Öffnungszeiten"}},
              {"typ":"Fusszeile"}]}
            """));
        Assert.Null(r.Error); Assert.Null(r.NeueSeite);
        var c = Content(r.Json!);
        Assert.Equal(["Navigation", "Kopfbereich", "Ueberschrift", "Fusszeile"], c.Select(b => b!["type"]!.ToString()));
        Assert.Equal("Bäckerei Korn", c[0]!["props"]!["marke"]!.ToString());
        Assert.False(string.IsNullOrEmpty(c[2]!["props"]!["id"]?.ToString())); // eigene id auch ohne Angabe
        Assert.Equal("h2", c[2]!["props"]!["level"]!.ToString()); // Standardwert
        Assert.Equal("Bäckerei", JsonNode.Parse(r.Json!)!["root"]!["props"]!["title"]!.ToString());
        Assert.DoesNotContain(c, b => b!["props"]!["id"]?.ToString() == "t"); // alte Seite ersetzt
    }

    [Fact(DisplayName = "seite_anlegen: fehlerhafte bausteine verwerfen den ganzen Plan mit Nummer des Bausteins")]
    public void Rejects_bad_blocks()
    {
        static string? Err(string bausteine) => WebDesignActions.Apply(Page, Plan("{\"aktion\":\"seite_anlegen\",\"titel\":\"X\",\"bausteine\":" + bausteine + "}")).Error;
        Assert.Contains("Baustein 2", Err("""[{"typ":"Text"},{"typ":"Gibtsnicht"}]"""));
        Assert.Contains("„in“", Err("""[{"typ":"Bereich","id":"b"},{"typ":"Text","in":"b"}]""")); // in bausteine nur oberste Ebene
        Assert.Contains("schon", Err("""[{"typ":"Text","id":"a"},{"typ":"Text","id":"a"}]"""));
        Assert.Contains("Liste", Err("""{"typ":"Text"}"""));
        Assert.Contains("kein Objekt", Err("""["Text"]"""));
        Assert.Contains("zu viele", Err("[" + string.Join(",", Enumerable.Repeat("""{"typ":"Text"}""", WebDesignActions.MaxActions)) + "]"));
        Assert.Contains("muss Text sein", Err("""[{"typ":"Text","props":{"text":[1, 2]}}]"""));
        Assert.Contains("muss Text sein", Err("""[{"typ":"Text","props":{"text":[]}}]"""));
        Assert.Contains("doppelt", Err("""[{"typ":"Text","text":"a","props":{"text":"b"}}]"""));
        Assert.Contains("unbekannte Angabe „farbe“", Err("""[{"typ":"Text","farbe":"rot"}]"""));
    }

    [Fact(DisplayName = "seite_anlegen: Felder direkt neben typ gelten wie in props, eine Liste aus Texten als Zeilen (häufige Fehler lokaler Modelle)")]
    public void Tolerates_common_slips()
    {
        var r = WebDesignActions.Apply(Page, Plan("""
            {"aktion":"seite_anlegen","titel":"X","bausteine":[
              {"typ":"Navigation","marke":"Korn","links":[{"label":"Brot","href":"#"}],"props":{"knopf":"Bestellen"}},
              {"typ":"Preise","props":{"tarife":[{"name":"Klein","preis":"3 €","zeitraum":"je Stück","merkmale":["Roggen","Sauerteig"],"knopf":"Nehmen","ziel":"#","hervorgehoben":"nein"}]}}]}
            """));
        Assert.Null(r.Error);
        var c = Content(r.Json!);
        Assert.Equal("Korn", c[0]!["props"]!["marke"]!.ToString()); Assert.Equal("Bestellen", c[0]!["props"]!["knopf"]!.ToString());
        Assert.Equal("Brot", c[0]!["props"]!["links"]![0]!["label"]!.ToString());
        Assert.Equal("Roggen\nSauerteig", c[1]!["props"]!["tarife"]![0]!["merkmale"]!.ToString());
    }

    [Fact(DisplayName = "seite_anlegen: Ja/Nein als true/false, Angaben in props verpackt, erfundenes Bild bleibt leer und wird genannt")]
    public void Tolerates_more_slips()
    {
        var r = WebDesignActions.Apply(Page, Plan("""
            {"aktion":"seite_anlegen","props":{"titel":"Korn","bausteine":[
              {"typ":"Hero","props":{"titel":"Brot","bild":"https://example.com/brot.jpg","bildAlt":"Brot"}},
              {"typ":"Preise","props":{"tarife":[{"name":"A","preis":"1 €","zeitraum":"x","merkmale":"y","knopf":"k","ziel":"#","hervorgehoben":true}]}}]}}
            """));
        Assert.Null(r.Error);
        var c = Content(r.Json!);
        Assert.Equal("Korn", JsonNode.Parse(r.Json!)!["root"]!["props"]!["title"]!.ToString());
        Assert.Equal("", c[0]!["props"]!["bild"]!.ToString()); // Standardwert statt erfundener Adresse
        Assert.Contains(r.Changes, x => x.Contains("leer gelassen"));
        Assert.Equal("ja", c[1]!["props"]!["tarife"]![0]!["hervorgehoben"]!.ToString());
        // doppelt neben und in props: abgelehnt; true bei einer Wahl ohne ja/nein: abgelehnt
        Assert.Contains("doppelt", WebDesignActions.Apply(Page, Plan("""{"aktion":"seite_anlegen","titel":"A","props":{"titel":"B"}}""")).Error);
        Assert.NotNull(WebDesignActions.Apply(Page, Plan("""{"aktion":"seite_anlegen","titel":"A","bausteine":[{"typ":"Text","props":{"align":true}}]}""")).Error);
        // außerhalb von seite_anlegen bleibt ein ungültiges Bild ein Fehler
        Assert.NotNull(WebDesignActions.Apply(Page, Plan("""{"aktion":"baustein_einfuegen","typ":"Bild","props":{"src":"https://example.com/x.jpg"}}""")).Error);
    }

    [Fact(DisplayName = "Z6/T-0662: Qwen 2.5 Coder 14B steht in der KI-Bibliothek und wird nach bestandenem Prüflauf empfohlen, wenn er zum PC passt")]
    public void Recommends_coder_14b()
    {
        var m = Assert.Single(KiLibrary.Models, x => x.Tag == "qwen2.5-coder:14b");
        Assert.Equal("Apache-2.0", m.License);
        WebDesignModelBench.ModelResult R(string model, bool ok) => new(model, [new("Seite aus einem Satz", ok, ok ? "bestanden" : "nein", 150)], DateTimeOffset.Now);
        var results = new[] { R("qwen2.5-coder:14b", true), R("qwen2.5-coder:7b", false) };
        Assert.Equal("qwen2.5-coder:14b", WebDesignModelBench.Recommend(new HardwareInfo(32L << 30, "RTX 5070", 12L << 30), results).Model); // dieser PC
        Assert.Null(WebDesignModelBench.Recommend(new HardwareInfo(16L << 30, "GPU", 6L << 30), results).Model); // passt nicht: zu wenig Grafikspeicher
    }

    [Fact(DisplayName = "Nachfrage bei abgelehntem Plan: nennt Meldung und eigenen Plan, kürzt sehr lange Antworten")]
    public void Fix_prompt()
    {
        var p = WebDesignKi.FixPlanPrompt("{\"aktionen\":[]}", "Der Plan enthält keine Aktionen.");
        Assert.Contains("Der Plan enthält keine Aktionen.", p); Assert.Contains("{\"aktionen\":[]}", p); Assert.Contains("ganzen Plan", p);
        Assert.True(WebDesignKi.FixPlanPrompt(new string('x', 50000), "f").Length < WebDesignKi.MaxFixAnswerChars + 500);
    }

    [Fact(DisplayName = "seite_anlegen mit adresse: weitere Seite der Website entsteht, die bisherige Seite bleibt unberührt")]
    public void Adds_site_page()
    {
        var d = Dir(); try
        {
            Assert.Null(WebDesignStates.Write(d, Page));
            var before = File.ReadAllText(WebDesignSite.ActiveFile(d));
            var r = WebDesignActions.Apply(Page, Plan("""{"aktion":"seite_anlegen","titel":"Kontakt","adresse":"kontakt","bausteine":[{"typ":"Kopfbereich","id":"k","props":{"title":"Schreib uns","sub":"Wir antworten schnell"}},{"typ":"Formular"}]}"""));
            Assert.Null(r.Error); Assert.Equal(new WebNeueSeite("kontakt", "Kontakt"), r.NeueSeite);
            var (auto, err) = WebDesignActions.Commit(d, r, Page, "Kontaktseite");
            Assert.Null(err); Assert.Equal("", auto); // kein Stand nötig: die bisherige Seite wurde nicht verändert
            Assert.Equal(before, File.ReadAllText(WebDesignSite.FileOf(d, WebDesignSite.Index, WebDesignSite.Load(d).Standardsprache, WebDesignSite.Load(d).Standardsprache)));
            Assert.Contains("kontakt", WebDesignSite.Pages(d));
            Assert.Equal("kontakt", WebDesignSite.Active(d).Adresse);
            Assert.Equal(["Kopfbereich", "Formular"], Content(WebDesignStates.Read(d)).Select(b => b!["type"]!.ToString()));

            // dieselbe Adresse noch einmal: abgelehnt, nichts geschrieben
            var again = WebDesignActions.Commit(d, r, Page, "nochmal");
            Assert.Null(again.AutoId); Assert.Contains("gibt es schon", again.Error);
            // in einer Variante nicht erlaubt
            Assert.Contains("Variante", WebDesignActions.Commit(d, WebDesignActions.Apply(Page, Plan("""{"aktion":"seite_anlegen","titel":"B","adresse":"b"}""")), Page, "v", "test").Error);
            Assert.DoesNotContain("b", WebDesignSite.Pages(d));
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "seite_anlegen mit adresse: ungültige oder reservierte Adressen werden schon beim Prüfen abgelehnt")]
    public void Rejects_bad_address()
    {
        static string? Err(string adresse) => WebDesignActions.Apply(Page, Plan("{\"aktion\":\"seite_anlegen\",\"titel\":\"X\",\"adresse\":" + adresse + "}")).Error;
        Assert.NotNull(Err("\"../weg\""));
        Assert.NotNull(Err("\"Kontakt Seite\""));
        Assert.NotNull(Err("\"assets\"")); // reserviert
        Assert.NotNull(Err("\"\""));
        Assert.NotNull(Err("3"));
        Assert.NotNull(Err("\"en\"")); Assert.NotNull(Err("\"en-gb\"")); Assert.NotNull(Err("\"con\"")); // Sprachcode, Windows-Gerätename
        Assert.NotNull(Err("\"" + new string('a', 61) + "\""));
        Assert.Null(Err("\"ueber-uns\""));
        Assert.NotNull(WebDesignActions.Apply(Page, Plan("""{"aktion":"seite_anlegen","titel":"   ","adresse":"x"}""")).Error); // Titel nur aus Leerzeichen
        // Grenze: Bausteine zählen mit den Aktionen
        Assert.Null(WebDesignActions.Apply(Page, Plan("{\"aktion\":\"seite_anlegen\",\"titel\":\"X\",\"bausteine\":[" + string.Join(",", Enumerable.Repeat("""{"typ":"Text"}""", WebDesignActions.MaxActions - 1)) + "]}")).Error);
    }

    [Fact(DisplayName = "seite_anlegen mit adresse: Vorprüfung vor dem Schreiben; Rückgängig nur, solange die Seite offen und unverändert ist, und mit Gestaltungswerten und voriger Seite")]
    public void Undo_new_page()
    {
        var d = Dir(); try
        {
            Assert.Null(WebDesignStates.Write(d, Page));
            Assert.Null(WebDesignSite.Create(d, "preise", "Preise")); // vorher bearbeitet: /preise
            var tokensBefore = WebDesignTokens.Load(d)?.ToJsonString();
            var plan = Plan("""{"aktion":"seite_anlegen","titel":"Kontakt","adresse":"kontakt","bausteine":[{"typ":"Kopfbereich","id":"k","props":{"title":"Schreib uns","sub":"Gern"}}]},{"aktion":"gestaltungswert_setzen","name":"accent","wert":"#aa0000"}""");
            var r = WebDesignActions.Apply(Page, plan);
            Assert.Null(r.Error);
            Assert.Contains("Variante", WebDesignActions.CheckNewPage(d, r, "v1"));
            var (stand, err) = WebDesignActions.CommitNewPage(d, r); Assert.Null(err);
            Assert.Equal("#aa0000", WebDesignTokens.Load(d)!["accent"]!.ToString()); // Schreiben hebt die Werte aufs Projekt
            Assert.Contains("gibt es schon", WebDesignActions.CheckNewPage(d, r, null));

            Assert.Null(WebDesignActions.UndoNewPage(d, stand!));
            Assert.DoesNotContain("kontakt", WebDesignSite.Pages(d));
            Assert.Equal("preise", WebDesignSite.Active(d).Adresse); // zurück zur vorigen Seite
            Assert.Equal(tokensBefore, WebDesignTokens.Load(d)?.ToJsonString()); // Gestaltungswerte wie vorher
            Assert.Contains("gibt es nicht mehr", WebDesignActions.UndoNewPage(d, stand!));

            // nach einer Änderung an der neuen Seite: kein Rückgängig, nichts wird gelöscht
            var (s2, _) = WebDesignActions.CommitNewPage(d, r);
            var changed = WebDesignActions.Apply(WebDesignStates.Read(d), Plan("""{"aktion":"element_aendern","id":"k","props":{"title":"Neu"}}"""));
            Assert.Null(WebDesignStates.Write(d, changed.Json!));
            Assert.Contains("geändert", WebDesignActions.UndoNewPage(d, s2!));
            Assert.Contains("kontakt", WebDesignSite.Pages(d));
            // eine andere Seite offen: kein Rückgängig
            Assert.Null(WebDesignSite.Activate(d, "preise"));
            Assert.Contains("nicht mehr geöffnet", WebDesignActions.UndoNewPage(d, s2!));
            Assert.Contains("kontakt", WebDesignSite.Pages(d));
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "Prüfkreislauf: Eine Nachbesserung, die eine weitere Seite anlegen will, wird verworfen")]
    public async Task Repair_cannot_add_page()
    {
        var r = await WebDesignCheck.RunAsync(Page, _ => Task.FromResult<IReadOnlyList<WebsiteFinding>>([new("SEO", "Fehler", "Test", "h1")]),
            (_, _) => Task.FromResult<string?>(Plan("""{"aktion":"seite_anlegen","titel":"X","adresse":"x"}""")), null);
        Assert.Equal(Page, r.Json);
        Assert.Contains(r.Rounds, x => x.Note?.Contains("weitere Seite") == true);
    }

    [Fact(DisplayName = "Neue-Seite-Auftrag: KI soll die ganze Seite über bausteine ohne „in“ liefern")]
    public void Prompt_guides_models()
    {
        var p = WebDesignKi.NewPagePrompt("Eine Seite für eine Bäckerei.");
        Assert.Contains("bausteine", p); Assert.Contains("ohne „in“", p); Assert.Contains("Fusszeile", p);
        Assert.Contains("adresse", WebDesignKi.Instructions(null));
    }
}
