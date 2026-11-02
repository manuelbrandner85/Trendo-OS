using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// 0.9.37: schöne Antworten (Markdown, Code-Farben), Projekt-Gedächtnis, Zeitreise, Gutachter-Note, echt testen (Befunde → Nachricht an Claude).
public class ClaudeExtrasTests
{
    static string Temp() { var d = Path.Combine(Path.GetTempPath(), "trendo-extras-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

    [Fact(DisplayName = "Markdown: Überschriften, Absätze, Listen, Tabellen, Zitate, Trennlinie und Code mit Sprache")]
    public void Markdown_blocks()
    {
        var b = ChatMarkdown.Parse("# Titel\n\nEin **fetter** Satz\nmit Umbruch.\n\n- eins\n- zwei\n\n3. drei\n4. vier\n\n| Name | Preis |\n|---|---:|\n| Brot | 2,50 € |\n| Milch | 1,20 € |\n\n> Zitat\n\n---\n\n```js\nconst a = 1;\n```");
        Assert.Collection(b,
            x => Assert.Equal(new ChatMarkdown.Heading(1, "Titel"), x),
            x => Assert.Equal("Ein **fetter** Satz mit Umbruch.", Assert.IsType<ChatMarkdown.Para>(x).Text),
            x => { var l = Assert.IsType<ChatMarkdown.ListBlock>(x); Assert.False(l.Ordered); Assert.Equal(["eins", "zwei"], l.Items); },
            x => { var l = Assert.IsType<ChatMarkdown.ListBlock>(x); Assert.True(l.Ordered); Assert.Equal(3, l.Start); Assert.Equal(2, l.Items.Count); },
            x => { var t = Assert.IsType<ChatMarkdown.Table>(x); Assert.Equal(["Name", "Preis"], t.Head); Assert.Equal(2, t.Rows.Count); Assert.Equal("1,20 €", t.Rows[1][1]); },
            x => Assert.Equal("Zitat", Assert.IsType<ChatMarkdown.Quote>(x).Text),
            x => Assert.IsType<ChatMarkdown.Rule>(x),
            x => Assert.Equal(new ChatMarkdown.Code("js", "const a = 1;"), x));
        // während Claude noch schreibt: offener Codeblock gilt bis zum Ende
        Assert.IsType<ChatMarkdown.Code>(ChatMarkdown.Parse("Hier:\n```py\nprint(1)").Last());
        // Zeilen mit Doppelpunkt am Ende (Gutachten: „Stärken:“) stehen für sich
        Assert.Equal(["Note: 8/10", "Stärken:"], ChatMarkdown.Parse("Note: 8/10\nStärken:\n- klar").OfType<ChatMarkdown.Para>().Select(x => x.Text));
    }

    [Fact(DisplayName = "Markdown im Text: Fett, Kursiv, Inline-Code, Links und nackte Adressen; Klartext zum Vorlesen")]
    public void Markdown_inline()
    {
        var s = ChatMarkdown.Inline("Das ist **wichtig**, *leise*, `code` und [Doku](https://example.org/a) oder https://example.org/b.");
        Assert.Contains(s, x => x is { Kind: ChatMarkdown.Kind.Bold, Text: "wichtig" });
        Assert.Contains(s, x => x is { Kind: ChatMarkdown.Kind.Italic, Text: "leise" });
        Assert.Contains(s, x => x is { Kind: ChatMarkdown.Kind.Code, Text: "code" });
        Assert.Contains(s, x => x is { Kind: ChatMarkdown.Kind.Link, Text: "Doku", Url: "https://example.org/a" });
        Assert.Contains(s, x => x is { Kind: ChatMarkdown.Kind.Link, Url: "https://example.org/b" });
        Assert.Equal("Ein Satz mit 5 * 3 = 15", string.Concat(ChatMarkdown.Inline("Ein Satz mit 5 * 3 = 15").Select(x => x.Text))); // Rechenzeichen bleiben
        Assert.Equal("Titel\nfett und eins", ChatMarkdown.Plain("# Titel\n**fett** und eins\n\n```\ncode\n```"));
    }

    [Fact(DisplayName = "Code-Farben: Schlüsselwörter, Texte, Kommentare, Zahlen; # nur in Python/Shell ein Kommentar, Tags nur in HTML")]
    public void Code_colors()
    {
        var t = CodeColors.Tokens("js", "const x = \"hi\"; // gruß\nreturn 42;");
        Assert.Contains(t, k => k is { Kind: CodeColors.Kind.Keyword, Text: "const" });
        Assert.Contains(t, k => k is { Kind: CodeColors.Kind.String, Text: "\"hi\"" });
        Assert.Contains(t, k => k is { Kind: CodeColors.Kind.Comment, Text: "// gruß" });
        Assert.Contains(t, k => k is { Kind: CodeColors.Kind.Number, Text: "42" });
        Assert.Equal("const x = \"hi\"; // gruß\nreturn 42;", string.Concat(t.Select(k => k.Text))); // nichts geht verloren
        Assert.Contains(CodeColors.Tokens("py", "# Notiz\nx = 1"), k => k.Kind == CodeColors.Kind.Comment);
        Assert.DoesNotContain(CodeColors.Tokens("css", "#kopf { color: red }"), k => k.Kind == CodeColors.Kind.Comment);
        Assert.Contains(CodeColors.Tokens("html", "<div class=\"a\">x</div>"), k => k is { Kind: CodeColors.Kind.Tag, Text: "<div" });
    }

    [Fact(DisplayName = "Projekt-Gedächtnis: „Merken:“-Zeilen erkennen und entfernen, ohne Doppelte, löschen, geht in die Anweisungen mit")]
    public void Memory()
    {
        var root = Temp();
        try
        {
            var mem = new ClaudeMemory(root);
            var answer = "Fertig.\nMerken: Farben sind Dunkelblau und Gold\n- **Merken:** Kundin heißt Erika Muster\nSchönen Tag!";
            Assert.Equal(["Farben sind Dunkelblau und Gold", "Kundin heißt Erika Muster"], ClaudeMemory.Extract(answer));
            Assert.Equal("Fertig.\n\nSchönen Tag!", ClaudeMemory.Strip(answer));
            foreach (var x in ClaudeMemory.Extract(answer)) Assert.True(mem.Add("c1", x));
            Assert.False(mem.Add("c1", "farben sind dunkelblau und gold")); // schon gemerkt
            Assert.Contains("- Kundin heißt Erika Muster", mem.Prompt("c1"));
            Assert.Empty(mem.Items("c2")); // je Projekt getrennt
            mem.Remove("c1", 0); Assert.Equal(["Kundin heißt Erika Muster"], mem.Items("c1"));
            for (var i = 0; i < ClaudeMemory.Max + 5; i++) mem.Add("c1", "Punkt " + i);
            Assert.Equal(ClaudeMemory.Max, mem.Items("c1").Count);
            mem.Delete("c1"); Assert.Empty(mem.Items("c1"));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "Zeitreise: Versionspunkte je Antwort, zurück zu einem älteren Stand und wieder vor")]
    public void Timeline()
    {
        var root = Temp(); var folder = Path.Combine(root, "projekt"); Directory.CreateDirectory(folder);
        try
        {
            var tl = new ClaudeTimeline(root);
            var f = Path.Combine(folder, "index.html");
            File.WriteAllText(f, "v1");
            var p1 = tl.NewStore("c1"); var s1 = FolderSnapshot.Take(folder, p1); File.WriteAllText(f, "v2"); File.WriteAllText(Path.Combine(folder, "neu.css"), "x"); tl.Keep("c1", p1, "Mach es blau", s1.Changes().Count);
            Thread.Sleep(5);
            var p2 = tl.NewStore("c1"); var s2 = FolderSnapshot.Take(folder, p2); tl.Keep("c1", p2, "Nichts geändert", s2.Changes().Count); // ohne Änderung kein Punkt
            Thread.Sleep(5);
            var p3 = tl.NewStore("c1"); var s3 = FolderSnapshot.Take(folder, p3); File.WriteAllText(f, "v3"); tl.Keep("c1", p3, "Mach es rot", s3.Changes().Count);
            var pts = tl.Points("c1");
            Assert.Equal(["Mach es rot", "Mach es blau"], pts.Select(p => p.Label));
            tl.Travel("c1", pts[1].Id, folder); // vor „Mach es blau“
            Assert.Equal("v1", File.ReadAllText(f)); Assert.False(File.Exists(Path.Combine(folder, "neu.css")));
            var back = tl.Points("c1")[0]; Assert.Equal("Vor der Zeitreise", back.Label);
            tl.Travel("c1", back.Id, folder); // wieder vor
            Assert.Equal("v3", File.ReadAllText(f)); Assert.True(File.Exists(Path.Combine(folder, "neu.css")));
            Assert.Throws<InvalidOperationException>(() => tl.Travel("c1", "gibt-es-nicht", folder));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "Gutachter: Note 1–10 erkennen, Auftrag ohne Änderungen, Verbesserung als Nachricht")]
    public void Review()
    {
        Assert.Equal(7, ClaudeReview.Grade("Note: 7/10\nStärken: …"));
        Assert.Equal(10, ClaudeReview.Grade("**Note:** 10 von 10"));
        Assert.Equal(8, ClaudeReview.Grade("note 8,5/10"));
        Assert.Null(ClaudeReview.Grade("Sehr schön."));
        Assert.Contains("ändere NICHTS", ClaudeReview.Rule);
        Assert.Contains("„Baue eine Startseite“", ClaudeReview.Prompt("Webseiten", "Baue eine Startseite", ["index.html"]));
        Assert.StartsWith("Ein Gutachter hat", ClaudeReview.Improve("Note: 6/10"));
    }

    [Fact(DisplayName = "Echt testen: Befunde und Bildschirmfoto werden zur Nachricht an Claude; ohne Befund nur Hinschauen; JSON wird geprüft")]
    public void Probe()
    {
        var shot = new ClaudeProbe.Result(["Skriptfehler in app.js Zeile 3: x is not defined"], ClaudeProbe.Shot);
        var fb = ClaudeProbe.Feedback(["Die Seite hat kein <title>."], shot, look: true)!;
        Assert.Contains("echt gestartet", fb); Assert.Contains("- Skriptfehler in app.js", fb); Assert.Contains("- Die Seite hat kein <title>.", fb); Assert.Contains(ClaudeProbe.Shot, fb);
        Assert.Contains("„Passt.“", ClaudeProbe.Feedback([], new([], ClaudeProbe.Shot), look: true));
        Assert.Null(ClaudeProbe.Feedback([], new([], ClaudeProbe.Shot), look: false));
        Assert.Null(ClaudeProbe.Feedback([], new([], null), look: true));
        var root = Temp();
        try
        {
            File.WriteAllText(Path.Combine(root, "gut.json"), "{\"a\":1}"); File.WriteAllText(Path.Combine(root, "kaputt.json"), "{\"a\":");
            Assert.Equal(["kaputt.json"], ClaudeProbe.JsonErrors(root, ["gut.json", "kaputt.json", "index.html"]).Select(e => e.Split(':')[0]));
        }
        finally { Directory.Delete(root, true); }
    }
}

/// 0.9.38: Vorschläge abtrennen, Projekte über alle Studios, Abläufe in der Prompt-Bibliothek.
public class ClaudeProgramsCoreTests
{
    [Fact(DisplayName = "Vorschläge: Zeile „Vorschläge: A | B“ wird zu Knöpfen und verschwindet aus dem Text; Frage bleibt erkennbar")]
    public void Suggestions()
    {
        var (text, s) = ClaudeChats.SplitSuggestions("Fertig.\n**Vorschläge:** Kontaktformular | „Farben anpassen“ | x\nFrage: Hell oder dunkel? [Hell | Dunkel]");
        Assert.Equal(["Kontaktformular", "Farben anpassen"], s);
        Assert.Equal("Hell oder dunkel?", ClaudeChats.SplitQuestion(text).Question);
        Assert.Empty(ClaudeChats.SplitSuggestions("Nur Text.").Suggestions);
        Assert.Contains(ClaudeChats.SuggestMark, ClaudeChats.SuggestRule);
    }

    [Fact(DisplayName = "Projekt-Fortsetzung: zuletzt benutzte Projekte über alle Studios, neueste zuerst, leere ausgelassen")]
    public void Recent()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-recent-" + Guid.NewGuid().ToString("N"));
        try
        {
            var c = new ClaudeChats(root);
            var a = c.Create("CORE-07", "Webseite", Path.Combine(root, "w")); c.Add("CORE-07", a.Id, new ClaudeChats.Msg("user", "x", [], DateTimeOffset.Now));
            Thread.Sleep(10);
            var b = c.Create("CORE-06", "Spiel", Path.Combine(root, "s")); c.Add("CORE-06", b.Id, new ClaudeChats.Msg("user", "y", [], DateTimeOffset.Now));
            c.Create("CORE-05", "Leer", Path.Combine(root, "l"));
            Assert.Equal(["Spiel", "Webseite"], c.Recent().Select(x => x.Title));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "Abläufe: als Vorlage in der Prompt-Bibliothek je Studio, gleicher Auftrag nur einmal, Wiederholungen gezählt")]
    public void Flows()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-flows-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var lib = new PromptLibrary(root, new AuditLog(Path.Combine(root, "audit.jsonl")));
            var t = ClaudeFlows.Save(lib, "Webseite bauen", "Prüfe alle Links");
            Assert.Equal(t.Id, ClaudeFlows.Save(lib, "Webseite bauen", "prüfe alle links ").Id);
            Assert.Single(ClaudeFlows.For(lib, "Webseite bauen")); Assert.Empty(ClaudeFlows.For(lib, "Spiele"));
            var chats = new[] { new ClaudeChats.Chat("1", "s", "t", "f", null, DateTimeOffset.Now, DateTimeOffset.Now, [new("user", "Prüfe alle Links", [], DateTimeOffset.Now), new("user", "prüfe alle links", [], DateTimeOffset.Now), new("assistant", "Prüfe alle Links", [], DateTimeOffset.Now)]) };
            Assert.Equal(2, ClaudeFlows.Repeats(chats, "Prüfe alle Links"));
        }
        finally { Directory.Delete(root, true); }
    }
}
