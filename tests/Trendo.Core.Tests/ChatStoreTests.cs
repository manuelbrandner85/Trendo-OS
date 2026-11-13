using Trendo.Core;
using Xunit;
using K = Trendo.Core.ChatStore.Kind;

namespace Trendo.Core.Tests;

/// KI fragen als Chatfenster: Gespräche speichern/suchen/umbenennen/löschen, Titel aus der ersten Frage, Antworten in Text/Code/Tabelle zerlegen.
public class ChatStoreTests
{
    [Fact(DisplayName = "Chat: Gespräch anlegen, Titel aus der ersten Frage, suchen (Titel und Inhalt), umbenennen bleibt, löschen; ungültige Kennung abgelehnt")]
    public void Store()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-chat-" + Guid.NewGuid().ToString("N"));
        try
        {
            var s = new ChatStore(root);
            var c = s.Append(new ChatStore.Chat(), "Wie plane ich ein Sommerfest für den Verein mit 80 Gästen und kleinem Budget?", "Erstelle zuerst eine Liste …", "qwen2.5:7b");
            Assert.Equal("Wie plane ich ein Sommerfest für den Verein …", c.Title);
            s.Append(c, "Und die Getränke?", "Rechne mit …", "qwen2.5:7b");
            Assert.Equal("Wie plane ich ein Sommerfest für den Verein …", s.Get(c.Id)!.Title); // Titel bleibt
            var d = s.Append(new ChatStore.Chat(), "Was ist eine Firewall?", "Eine Firewall …", null);
            Assert.Equal([d.Id, c.Id], s.All().Select(x => x.Id));
            Assert.Equal([c.Id], s.All("getränke").Select(x => x.Id));
            s.Rename(c.Id, "Sommerfest");
            s.Append(s.Get(c.Id)!, "Noch was?", "Ja.", null);
            Assert.Equal("Sommerfest", s.Get(c.Id)!.Title); Assert.Equal(6, s.Get(c.Id)!.Messages.Count);
            Assert.Contains("**Du:** Und die Getränke?", ChatStore.ToMarkdown(s.Get(c.Id)!));
            Assert.True(s.Delete(d.Id)); Assert.Single(s.All());
            Assert.Throws<ArgumentException>(() => s.Rename("..\\x", "a"));
            Assert.Null(s.Get("../../etc"));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "Chat: Antwort zerlegen – Text, Code-Block mit Sprache, Markdown-Tabelle; Text ohne Besonderes bleibt ein Abschnitt")]
    public void Segments()
    {
        var s = ChatStore.Segments("So geht es:\n\n```python\nprint('hallo')\n```\n\n| Zutat | Menge |\n|---|---:|\n| Mehl | 500 g |\n| Zucker | 200 g |\n\nGuten Appetit!");
        Assert.Equal([K.Text, K.Code, K.Table, K.Text], s.Select(x => x.Kind));
        Assert.Equal(("python", "print('hallo')"), (s[1].Language, s[1].Content));
        Assert.Equal(["Zutat", "Menge"], s[2].Rows![0]); Assert.Equal(["Zucker", "200 g"], s[2].Rows![2]);
        Assert.Single(ChatStore.Segments("Nur ein Satz | mit Strich."));
        Assert.Equal(K.Code, Assert.Single(ChatStore.Segments("```\nnicht geschlossen")).Kind);
    }
}
