using System.Text.Json.Nodes;
using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Claude bedient Trendo selbst: Protokoll, Stellen im Dokument, wann der Nutzer gefragt wird.</summary>
public class TrendoMcpTests
{
    static TrendoMcp Server(Func<JsonObject, Task<McpResult>>? run = null) => new([new McpTool("eins", "Beschreibung", TrendoMcp.Schema(("ziel", "string", "Ziel", true), ("frei", "any", "Frei", false)), run ?? (_ => Task.FromResult(new McpResult("gut"))))]);
    static JsonNode Req(string method, JsonObject? p = null) => new JsonObject { ["jsonrpc"] = "2.0", ["id"] = 7, ["method"] = method, ["params"] = p };

    [Fact(DisplayName = "Claude-Zugang: Begrüßung nennt Version und Fähigkeiten, Benachrichtigung bleibt ohne Antwort, Unbekanntes meldet Fehler")]
    public async Task Handshake()
    {
        var s = Server();
        var r = await s.HandleAsync(Req("initialize", new JsonObject { ["protocolVersion"] = "2025-06-18" }));
        Assert.Equal("2025-06-18", (string)r!["result"]!["protocolVersion"]!);
        Assert.Equal("trendo", (string)r["result"]!["serverInfo"]!["name"]!);
        Assert.NotNull(r["result"]!["capabilities"]!["tools"]);
        Assert.Equal(7, (int)r["id"]!);
        Assert.Null(await s.HandleAsync(new JsonObject { ["jsonrpc"] = "2.0", ["method"] = "notifications/initialized" }));
        Assert.Equal(-32601, (int)(await s.HandleAsync(Req("gibtsnicht")))!["error"]!["code"]!);
    }

    [Fact(DisplayName = "Claude-Zugang: Liste zeigt Werkzeuge mit Schema, Aufruf liefert Text und Bild, Fehler kommen als Hinweis statt Absturz")]
    public async Task Tools()
    {
        var s = Server();
        var list = (await s.HandleAsync(Req("tools/list")))!["result"]!["tools"]!.AsArray();
        Assert.Equal("eins", (string)list[0]!["name"]!);
        Assert.Equal("ziel", (string)list[0]!["inputSchema"]!["required"]![0]!);
        Assert.Null(list[0]!["inputSchema"]!["properties"]!["frei"]!["type"]);

        var withImage = Server(_ => Task.FromResult(new McpResult("bild", [1, 2, 3])));
        var c = (await withImage.HandleAsync(Req("tools/call", new JsonObject { ["name"] = "eins", ["arguments"] = new JsonObject() })))!["result"]!;
        Assert.Equal("image", (string)c["content"]![1]!["type"]!);
        Assert.Equal("AQID", (string)c["content"]![1]!["data"]!);
        Assert.False((bool)c["isError"]!);

        var broken = Server(_ => throw new ArgumentException("kaputt"));
        var e = (await broken.HandleAsync(Req("tools/call", new JsonObject { ["name"] = "eins" })))!["result"]!;
        Assert.True((bool)e["isError"]!); Assert.Equal("kaputt", (string)e["content"]![0]!["text"]!);
        Assert.NotNull((await s.HandleAsync(Req("tools/call", new JsonObject { ["name"] = "nein" })))!["error"]);
    }

    [Fact(DisplayName = "Dokument-Stellen: lesen, tief setzen, an Listen anhängen, entfernen; Unmögliches wird abgelehnt")]
    public void Paths()
    {
        var d = JsonNode.Parse("""{"levels":[{"name":"A"}],"zahl":1}""")!.AsObject();
        Assert.Equal("A", (string)DocPath.Get(d, "levels/0/name")!);
        Assert.Same(d, DocPath.Get(d, ""));
        Assert.Null(DocPath.Get(d, "levels/9"));
        DocPath.Set(d, "levels/0/name", "B"); Assert.Equal("B", (string)d["levels"]![0]!["name"]!);
        DocPath.Set(d, "levels/1", new JsonObject { ["name"] = "C" }); Assert.Equal(2, d["levels"]!.AsArray().Count);
        DocPath.Set(d, "neu/tief/wert", 5); Assert.Equal(5, (int)d["neu"]!["tief"]!["wert"]!);
        Assert.True(DocPath.Remove(d, "levels/0")); Assert.Equal("C", (string)d["levels"]![0]!["name"]!);
        Assert.Throws<ArgumentException>(() => DocPath.Set(d, "levels/7", 1));
        Assert.Throws<ArgumentException>(() => DocPath.Set(d, "zahl/x", 1));
        Assert.Throws<ArgumentException>(() => DocPath.Set(d, "", 1));
    }

    [Theory(DisplayName = "Nachfrage: Senden, Kaufen, Veröffentlichen und ganze Dateien löschen fragen; Bearbeiten im Dokument läuft ohne Klick")]
    [InlineData("Brief senden", "x", true, true)]
    [InlineData("Jetzt kaufen", "x", true, true)]
    [InlineData("Veröffentlichen", "x", true, true)]
    [InlineData("3D-Druck starten", "druck starten", true, true)]
    [InlineData("Projekt löschen", "x", true, true)]
    [InlineData("Datei löschen", "x", true, true)]
    [InlineData("Ebene löschen", "x", true, false)]
    [InlineData("Ebene löschen", "x", false, true)]
    [InlineData("Rendern starten", "x", true, false)]
    [InlineData("Kennzahlen", "x", true, false)]
    [InlineData("Level hinzufügen", "x", false, false)]
    public void Consent(string label, string name, bool undoable, bool asks) => Assert.Equal(asks, ClaudeActions.NeedsConsent(label, name, undoable));

    [Theory(DisplayName = "Dokument-Stellen: JSON in Text wird ausgepackt, normaler Text bleibt")]
    [InlineData("\"#FF0000\"", "\"#FF0000\"".Length - 2, "string")]
    [InlineData("[1,2]", 2, "array")]
    [InlineData("[kein json", 10, "string")]
    [InlineData("Hallo", 5, "string")]
    public void Unwrap(string input, int size, string kind)
    {
        var r = DocPath.Unwrap(JsonValue.Create(input));
        if (kind == "array") Assert.Equal(size, r!.AsArray().Count); else Assert.Equal(size, ((string)r!).Length);
    }

    [Fact(DisplayName = "Fertig-Zeichen: wird erkannt und erscheint nicht in der Antwort")]
    public void Finish()
    {
        Assert.True(TrendoMcp.IsFinished("Alles da.\nTRENDO_FERTIG"));
        Assert.False(TrendoMcp.IsFinished("Es fehlt noch etwas."));
        Assert.Equal("Alles da.", TrendoMcp.StripFinish("Alles da.\nTRENDO_FERTIG"));
    }
}
