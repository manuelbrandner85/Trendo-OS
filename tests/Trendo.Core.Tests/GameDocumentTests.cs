using System.Text.Json.Nodes;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0610 D3/D4: Spielbibel als Dokument aus Abschnitten, Bilder, PDF, Chat mit Vorschlägen.</summary>
public class GameDocumentTests
{
    const string Png = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";

    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-gdoc-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }
    static JsonObject Bible() => GameDesign.Templates.First(t => t.Id == "topdown").Bible();
    static GameDocument.Section Sec(JsonObject d, string id) => GameDocument.Find(d, id)!;
    static List<string> Ids(JsonObject d) => GameDocument.Sections(d).Select(s => s.Id).ToList();
    static string Image(string dir) { var f = Path.Combine(dir, "bild.png"); File.WriteAllBytes(f, Convert.FromBase64String(Png)); return f; }

    [Fact(DisplayName = "Dokument: Standard-Markdown hat alle bisherigen Überschriften in der bisherigen Reihenfolge")]
    public void Default_markdown()
    {
        var md = GameDesign.BibleMarkdown(Bible());
        Assert.StartsWith("# Hüter des Hains\n\n**Umfang:**", md);
        var last = -1;
        foreach (var h in new[] { "## Idee", "## Kernschleife", "## Mechaniken", "## Steuerung", "## Stil", "## Figuren", "## Welt", "## Missionen", "## Systeme", "## Ton und Musik", "## Menüs", "## Meilensteine" })
        {
            var at = md.IndexOf(h, StringComparison.Ordinal);
            Assert.True(at > last, h); last = at;
        }
        Assert.Contains("- **Moosgeist** (Boss, 3 Phasen): Uralter Waldgeist", md);
        Assert.Contains("· braucht Laterne", md);
        Assert.Contains("- [ ] Konzept", md);
    }

    [Fact(DisplayName = "Dokument: verschobene und eigene Abschnitte bleiben nach Speichern und Laden")]
    public void Order_survives_save()
    {
        var root = Tmp();
        try
        {
            var doc = new ProgramDocument("CORE-06", "Test", Bible());
            string id = "";
            doc.Edit("Neu", x => id = GameDocument.AddCustom(x, "Wirtschaft"));
            doc.Edit("Text", x => GameDocument.SetText(x, id, "Münzen und Läden"));
            doc.Edit("Hoch", x => GameDocument.Move(x, id, -1));
            doc.Edit("Runter", x => GameDocument.Move(x, "idee", 1));
            var expected = Ids(doc.Data);
            Assert.Equal(["kern", "idee"], expected.Take(2));
            Assert.Equal(id, expected[^2]);
            var file = Path.Combine(root, "spiel.tdoc"); doc.Save(file);
            var loaded = ProgramDocument.Load(file);
            Assert.Equal(expected, Ids(loaded.Data));
            Assert.Equal("Münzen und Läden", GameDocument.Text(loaded.Data, Sec(loaded.Data, id)));
            var md = GameDocument.Markdown(loaded.Data);
            Assert.True(md.IndexOf("## Kernschleife", StringComparison.Ordinal) < md.IndexOf("## Idee", StringComparison.Ordinal));
            Assert.Contains("## Wirtschaft\n\nMünzen und Läden", md);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "Dokument: am Rand wird nicht verschoben; feste Abschnitte lassen sich nicht entfernen")]
    public void Edges()
    {
        var d = Bible();
        Assert.False(GameDocument.Move(d, "idee", -1));
        Assert.False(GameDocument.Move(d, "meilensteine", 1));
        Assert.False(GameDocument.Move(d, "gibt-es-nicht", 1));
        Assert.False(GameDocument.RemoveCustom(d, "idee"));
        Assert.Equal(12, Ids(d).Count);
    }

    [Fact(DisplayName = "Dokument: Bilder wandern zwischen Abschnitt und Ablage, beim Entfernen in die Ablage")]
    public void Images_and_vault()
    {
        var d = Bible();
        GameDocument.AddImage(d, "idee", "a.png"); GameDocument.AddImage(d, "idee", "a.png");
        Assert.Equal(["a.png"], GameDocument.Images(d, "idee"));
        GameDocument.ToVault(d, "idee", "a.png");
        Assert.Empty(GameDocument.Images(d, "idee")); Assert.Equal(["a.png"], GameDocument.Vault(d));
        GameDocument.FromVault(d, "stil", "a.png");
        Assert.Equal(["a.png"], GameDocument.Images(d, "stil")); Assert.Empty(GameDocument.Vault(d));
        var id = GameDocument.AddCustom(d, "Extra");
        GameDocument.AddImage(d, id, "b.png");
        Assert.True(GameDocument.RemoveCustom(d, id));
        Assert.Null(GameDocument.Find(d, id)); Assert.Equal(["b.png"], GameDocument.Vault(d));
    }

    [Fact(DisplayName = "Dokument: Stand wiederherstellen ergibt exakt den alten Inhalt")]
    public void Restore_exact()
    {
        var root = Tmp();
        try
        {
            var doc = new ProgramDocument("CORE-06", "Test", Bible());
            doc.Edit("Runter", x => GameDocument.Move(x, "idee", 1));
            var file = Path.Combine(root, "spiel.tdoc"); doc.Save(file);
            var old = doc.Data.ToJsonString();
            doc.Edit("Text", x => GameDocument.SetText(x, "stil", "Ganz anders"));
            doc.Save(file);
            var version = Assert.Single(ProgramDocument.Versions(file));
            doc.RestoreVersion(version.File);
            Assert.Equal(old, doc.Data.ToJsonString());
        }
        finally { Directory.Delete(root, true); }
    }

    static int ImagesIn(string pdf)
    {
        using var read = PdfReader.Open(pdf, PdfDocumentOpenMode.Import);
        return read.Pages.Cast<PdfPage>().Sum(p => p.Resources.Elements.GetDictionary("/XObject")?.Elements.Count ?? 0);
    }

    [Fact(DisplayName = "PDF: enthält Bilder der Abschnitte, nicht die Ablage; langer Text bricht um; fehlendes Bild stört nicht")]
    public void Pdf_with_images()
    {
        var root = Tmp();
        try
        {
            var d = Bible(); var img = Image(root);
            var plain = Path.Combine(root, "ohne.pdf");
            Assert.True(GameDocument.WritePdf(d, plain) >= 1);
            Assert.Equal(0, ImagesIn(plain));

            GameDocument.ToVault(d, "idee", img);
            var vault = Path.Combine(root, "ablage.pdf"); GameDocument.WritePdf(d, vault);
            Assert.Equal(0, ImagesIn(vault));

            GameDocument.FromVault(d, "idee", img);
            GameDocument.AddImage(d, "stil", Path.Combine(root, "fehlt.png"));
            var with = Path.Combine(root, "mit.pdf"); GameDocument.WritePdf(d, with);
            Assert.Equal(1, ImagesIn(with));

            var id = GameDocument.AddCustom(d, "Langer Text");
            GameDocument.SetText(d, id, string.Join(" ", Enumerable.Repeat("Ein längerer Satz über das Spiel und seine Welt.", 900)));
            var pages = GameDocument.WritePdf(d, Path.Combine(root, "lang.pdf"));
            Assert.True(pages > 3, $"Seiten: {pages}");
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "Chat: Vorschlag ändert nur nach dem Anwenden und nur den gewählten Abschnitt")]
    public void Suggestion_only_after_apply()
    {
        var d = Bible(); var s = Sec(d, "stil");
        var before = d.ToJsonString();
        var reply = GameDocument.ParseReply("Mein Rat.\n```abschnitt\nAquarell, ruhig\n```", s);
        Assert.Equal("Mein Rat.", reply.Text); Assert.Equal("Aquarell, ruhig", reply.Suggestion);
        Assert.Equal(["Aquarell, ruhig"], GameDocument.Preview(d, s, reply.Suggestion!));
        Assert.Equal(before, d.ToJsonString());
        GameDocument.Apply(d, s, reply.Suggestion!);
        Assert.Equal("Aquarell, ruhig", (string?)d["style"]);
        d["style"] = JsonNode.Parse(JsonNode.Parse(before)!["style"]!.ToJsonString());
        Assert.Equal(JsonNode.Parse(before)!.ToJsonString(), d.ToJsonString());
    }

    [Fact(DisplayName = "Chat: Listen-Vorschlag wird geprüft, ändert nur seine Liste; unlesbare Liste ist kein Vorschlag")]
    public void List_suggestion()
    {
        var d = Bible(); var s = Sec(d, "figuren");
        Assert.Null(GameDocument.ParseReply("Rat\n```json\n[{\"name\":\n```", s).Suggestion);
        Assert.Null(GameDocument.ParseReply("Rat\n```json\n{\"name\":\"x\"}\n```", s).Suggestion);
        var json = GameDocument.ParseReply("```json\n[{\"name\":\"Rabe\",\"role\":\"Quatsch\",\"text\":\"Bote\"}]\n```", s).Suggestion!;
        var preview = GameDocument.Preview(d, s, json);
        Assert.Contains("+ Figur „Rabe“", preview); Assert.Contains("− Figur „Lina“", preview);
        var quests = d["quests"]!.ToJsonString();
        GameDocument.Apply(d, s, json);
        var c = Assert.Single(d["characters"]!.AsArray());
        Assert.Equal("Rabe", (string?)c!["name"]); Assert.Equal("Nebenfigur", (string?)c["role"]);
        Assert.Equal(quests, d["quests"]!.ToJsonString());
    }

    [Fact(DisplayName = "Chat: Meilenstein-Vorschlag behält erledigte; Mechaniken mit Komma werden zur Liste")]
    public void Milestones_and_lines()
    {
        var d = Bible();
        d["milestones"]!.AsArray()[0]!["done"] = true;
        GameDocument.Apply(d, Sec(d, "meilensteine"), "[\"Konzept\", \"Neu\"]");
        var ms = d["milestones"]!.AsArray();
        Assert.Equal(2, ms.Count); Assert.True((bool)ms[0]!["done"]!); Assert.False((bool)ms[1]!["done"]!);
        GameDocument.Apply(d, Sec(d, "mechaniken"), "Rennen, Fliegen\nTauchen");
        Assert.Equal(["Rennen", "Fliegen", "Tauchen"], d["mechanics"]!.AsArray().Select(x => (string)x!));
    }

    [Fact(DisplayName = "Chat: Verlauf bleibt nach Speichern und Laden, ohne Rückgängig-Schritt und unberührt vom Wiederherstellen")]
    public void Chat_persists()
    {
        var root = Tmp();
        try
        {
            var doc = new ProgramDocument("CORE-06", "Test", Bible());
            GameDocument.AddChat(doc, "stil", "du", "Frage"); GameDocument.AddChat(doc, "stil", "KI", "Antwort");
            Assert.False(doc.CanUndo); Assert.True(doc.Dirty);
            var file = Path.Combine(root, "spiel.tdoc"); doc.Save(file);
            var loaded = ProgramDocument.Load(file);
            Assert.Equal([new GameDocument.ChatLine("du", "Frage"), new GameDocument.ChatLine("KI", "Antwort")], GameDocument.Chat(loaded, "stil"));
            Assert.Empty(GameDocument.Chat(loaded, "idee"));
            loaded.Edit("Text", x => GameDocument.SetText(x, "stil", "Neu")); loaded.Undo();
            Assert.Equal(2, GameDocument.Chat(loaded, "stil").Count);
            var autosave = loaded.Autosave(root) ?? ProgramDocument.AutosaveFile(file);
            Assert.Equal(2, GameDocument.Chat(ProgramDocument.Restore(autosave), "stil").Count);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "Dokument: Datei ohne Begleitdaten bleibt wie bisher")]
    public void No_meta_no_field() => Assert.DoesNotContain("\"meta\"", new ProgramDocument("CORE-06", "Test", Bible()).Serialize());

    [Fact(DisplayName = "Chat: Auftrag an die KI enthält Spiel, Abschnitt, Verlauf und Wunsch")]
    public void Prompt_content()
    {
        var d = Bible(); var s = Sec(d, "stil");
        var p = GameDocument.ChatPrompt(d, s, "Mehr Farbe", [new GameDocument.ChatLine("du", "Vorher")]);
        Assert.Contains("Hüter des Hains", p); Assert.Contains("Pixel-Art 32 px", p); Assert.Contains("du: Vorher", p); Assert.Contains("Wunsch: Mehr Farbe", p);
        Assert.Contains("```abschnitt", GameDocument.ChatInstructions(s));
        Assert.Contains("JSON-Array", GameDocument.ChatInstructions(Sec(d, "figuren")));
    }
}
