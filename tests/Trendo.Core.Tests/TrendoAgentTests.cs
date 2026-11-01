using Trendo.Core.Programs;
using Xunit;

namespace Trendo.Core.Tests;

/// Trendo-Agent wie Claude Code: Plan lesen, Änderungen nur sicher im Projekt, Vorher/Nachher, Prüfen für Nachbessern.
public class TrendoAgentTests
{
    static string Proj()
    {
        var d = Path.Combine(Path.GetTempPath(), "trendo-agent-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(Path.Combine(d, "node_modules", "x"));
        File.WriteAllText(Path.Combine(d, "index.html"), "<!doctype html>\n<html>\n<body>\n<h1>Hallo</h1>\n</body>\n</html>\n");
        File.WriteAllText(Path.Combine(d, "node_modules", "x", "a.js"), "x"); File.WriteAllBytes(Path.Combine(d, "bild.png"), [1, 2, 3]);
        return d;
    }

    [Fact(DisplayName = "Agent: Plan aus JSON (auch in Codeblock, mit Fragen und Optionen) oder aus nummerierten Zeilen")]
    public void Plan()
    {
        var p = TrendoAgent.ParsePlan("Gern!\n```json\n{\"zusammenfassung\":\"Kontaktseite\",\"plan\":[\"Formular bauen\",\"Seite verlinken\"],\"fragen\":[{\"frage\":\"Welche Farbe?\",\"optionen\":[\"Blau\",\"Grün\"]},{\"frage\":\"Mit Karte?\"}]}\n```");
        Assert.Equal(["Formular bauen", "Seite verlinken"], p.Steps); Assert.Equal("Kontaktseite", p.Summary);
        Assert.Equal(["Blau", "Grün"], p.Questions[0].Options); Assert.Equal(["Ja", "Nein"], p.Questions[1].Options);
        var q = TrendoAgent.ParsePlan("Plan:\n1. Kopf bauen\n2) Text schreiben\n- Bild einfügen");
        Assert.Equal(["Kopf bauen", "Text schreiben", "Bild einfügen"], q.Steps); Assert.Empty(q.Questions);
    }

    [Fact(DisplayName = "Agent: Änderungen nur als Textdatei im Projekt – „..“, Laufwerk, node_modules, Bilder verworfen; Vorher/Nachher und Zeilenzahlen")]
    public void Changes()
    {
        var d = Proj(); var rej = new List<string>();
        var json = "{\"dateien\":[{\"pfad\":\"index.html\",\"inhalt\":\"<!doctype html>\\n<html lang=\\\"de\\\">\\n<body>\\n<h1>Willkommen</h1>\\n</body>\\n</html>\\n\"}," +
                   "{\"pfad\":\"css/stil.css\",\"inhalt\":\"h1{color:gold}\"},{\"pfad\":\"../boese.txt\",\"inhalt\":\"x\"},{\"pfad\":\"C:/Windows/x.txt\",\"inhalt\":\"x\"}," +
                   "{\"pfad\":\"node_modules/y.js\",\"inhalt\":\"x\"},{\"pfad\":\"bild.png\",\"inhalt\":\"x\"}]}";
        var c = TrendoAgent.ParseChanges(json, d, rej);
        Assert.Equal(["index.html", "css/stil.css"], c.Select(x => x.Path)); Assert.Equal(4, rej.Count);
        Assert.False(c[0].IsNew); Assert.True(c[1].IsNew);
        Assert.Equal((2, 2), (c[0].Added, c[0].Removed)); // <html> und <h1> ersetzt
        var diff = TrendoAgent.Diff(c[0].Before!, c[0].After);
        Assert.Contains("- <h1>Hallo</h1>", diff); Assert.Contains("+ <h1>Willkommen</h1>", diff);
        TrendoAgent.Apply(d, c);
        Assert.Equal("h1{color:gold}", File.ReadAllText(Path.Combine(d, "css", "stil.css")));
        Assert.Empty(TrendoAgent.ParseChanges(json.Replace("Willkommen", "Willkommen"), d).Where(x => x.Path == "index.html")); // unverändert → keine Änderung
        Assert.Contains("keine Dateien", Assert.Single(Run(() => { var r = new List<string>(); TrendoAgent.ParseChanges("Mache ich!", d, r); return r; })));
        Assert.DoesNotContain("node_modules/x/a.js", TrendoAgent.ProjectFiles(d)); Assert.DoesNotContain("bild.png", TrendoAgent.ProjectFiles(d));
        Assert.Contains("=== index.html ===", TrendoAgent.Context(d));
    }

    static List<string> Run(Func<List<string>> f) => f();

    [Fact(DisplayName = "Agent: Prüfen findet fehlenden Titel, Sprache, viewport, kaputte Verweise, Bilder ohne alt, fehlerhaftes JSON und offene Klammern – Rückmeldung zum Nachbessern")]
    public void Checks()
    {
        var d = Proj();
        File.WriteAllText(Path.Combine(d, "index.html"), "<html><head></head><body><a href=\"fehlt.html\">x</a><img src=\"bild.png\"><a href=\"https://x.de\">y</a></body></html>");
        File.WriteAllText(Path.Combine(d, "a.json"), "{\"x\":");
        File.WriteAllText(Path.Combine(d, "a.js"), "function f(){ if (x) { return '}'; }");
        var c = TrendoAgent.Check(d, ["index.html", "a.json", "a.js"]);
        string[] expect = ["kein <title>", "lang=", "viewport", "„fehlt.html“", "alt-Text", "JSON fehlerhaft", "Klammern {}"];
        Assert.All(expect, e => Assert.Contains(c, x => !x.Ok && x.Text.Contains(e)));
        Assert.DoesNotContain(c, x => x.Text.Contains("x.de") || x.Text.Contains("bild.png"));
        var fb = TrendoAgent.Feedback(c); Assert.StartsWith("- ", fb);
        File.WriteAllText(Path.Combine(d, "ok.html"), "<!doctype html><html lang=\"de\"><head><meta name=\"viewport\" content=\"width=device-width\"><title>Gut</title></head><body></body></html>");
        Assert.All(TrendoAgent.Check(d, ["ok.html"]), x => Assert.True(x.Ok));
    }
}
