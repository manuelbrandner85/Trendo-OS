using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// Claude-Chat im Kern: Rückfragen erkennen, Startideen je Studio, Kontingent-Meldung, Token-Zählung, Projekte mit vorhandenem Ordner und voller Freigabe.
public class ClaudeChatCoreTests
{
    [Fact(DisplayName = "Claude-Chat: Rückfrage „Frage: … [A | B]“ wird zu Knöpfen, normaler Text bleibt; Startideen je Studio")]
    public void Question_and_ideas()
    {
        var (t, q, o) = ClaudeChats.SplitQuestion("Ich habe zwei Ideen.\nFrage: Welche Farbe? [Blau | Grün | Gold]");
        Assert.Equal("Ich habe zwei Ideen.", t); Assert.Equal("Welche Farbe?", q); Assert.Equal(["Blau", "Grün", "Gold"], o);
        Assert.Null(ClaudeChats.SplitQuestion("Fertig. Frage am Rande: alles gut?").Question);
        Assert.Null(ClaudeChats.SplitQuestion("Frage: Leer? []").Question);
        Assert.Contains("Jump-and-Run", string.Join("|", ClaudeChats.Ideas("CORE-06", "spiele")));
        Assert.Contains("Haushaltsbuch", string.Join("|", ClaudeChats.Ideas("SYS-10", "tabelle")));
        Assert.Equal(4, ClaudeChats.Ideas(null, null).Count);
        Assert.Contains("Frage:", ClaudeChats.System("Spiele bauen", "x", []));
    }

    [Fact(DisplayName = "Claude-Chat: Token-Verbrauch aus dem Ergebnis, Kontingent-Ende verständlich erkannt")]
    public void Usage_and_limit()
    {
        var done = ClaudeCode.Parse("""{"type":"result","subtype":"success","is_error":false,"result":"ok","session_id":"s","num_turns":1,"usage":{"input_tokens":100,"output_tokens":50,"cache_creation_input_tokens":20,"cache_read_input_tokens":30}}""", ".").OfType<ClaudeCode.Done>().Single();
        Assert.Equal(200, done.Tokens);
        Assert.True(ClaudeCode.IsLimit("Claude AI usage limit reached|1730000000")); Assert.True(ClaudeCode.IsLimit("You've hit your 5-hour limit"));
        Assert.False(ClaudeCode.IsLimit("Datei nicht gefunden")); Assert.False(ClaudeCode.IsLimit(null));
    }

    [Fact(DisplayName = "Claude-Chat: Projekt im vorhandenen Studio-Ordner anlegen; volle Funktionen je Projekt merken")]
    public void Existing_folder_and_full()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-chatcore-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        var site = Path.Combine(root, "meine-seite"); Directory.CreateDirectory(site);
        var chats = new ClaudeChats(root);
        var c = chats.Create("CORE-07", "Seite", root, site);
        Assert.Equal(site, c.Folder); Assert.False(c.Full);
        Assert.True(chats.SetFull("CORE-07", c.Id, true).Full); Assert.True(chats.Get("CORE-07", c.Id)!.Full);
        Directory.Delete(root, true);
    }
}
