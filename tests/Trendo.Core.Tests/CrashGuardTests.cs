using System.Text.Json.Nodes;
using Trendo.Core;
using Trendo.Core.Statik;
using Xunit;

namespace Trendo.Core.Tests;

/// Rückmeldung 03.11.2026 „Trendo schließt sich einfach“: die Ursachen aus den Absturzprotokollen dürfen nie wieder abstürzen.
public class CrashGuardTests
{
    [Theory(DisplayName = "Claude-Zeilen in unerwarteter Form werden übersprungen statt Trendo zu beenden (Absturz 0.9.45–0.9.48)")]
    [InlineData("{\"type\":\"assistant\",\"message\":\"nur Text\"}")]
    [InlineData("{\"type\":\"assistant\",\"message\":{\"content\":\"Text statt Liste\"}}")]
    [InlineData("{\"type\":\"result\",\"subtype\":\"success\",\"usage\":5}")]
    [InlineData("{\"type\":\"result\",\"is_error\":\"nein\"}")]
    [InlineData("{\"type\":123,\"subtype\":[1]}")]
    [InlineData("{\"type\":\"system\",\"subtype\":\"init\",\"mcp_servers\":\"x\",\"skills\":5}")]
    [InlineData("{\"type\":\"user\",\"message\":{\"content\":[{\"type\":\"tool_result\",\"is_error\":\"ja\"}]}}")]
    [InlineData("[1,2,3]")]
    [InlineData("\"nur ein Text\"")]
    [InlineData("kein json {")]
    public void Claude_lines_never_throw(string line)
    {
        var ex = Record.Exception(() => ClaudeCode.Parse(line, Path.GetTempPath()).ToList());
        Assert.Null(ex);
    }

    [Fact(DisplayName = "Claude-Zeilen: gültige Antworten werden weiter erkannt")]
    public void Claude_valid_lines_still_parse()
    {
        var say = Assert.Single(ClaudeCode.Parse("{\"type\":\"assistant\",\"message\":{\"content\":[{\"type\":\"text\",\"text\":\" Hallo \"}]}}", Path.GetTempPath()));
        Assert.Equal("Hallo", Assert.IsType<ClaudeCode.Say>(say).Text);
        var done = Assert.IsType<ClaudeCode.Done>(Assert.Single(ClaudeCode.Parse("{\"type\":\"result\",\"subtype\":\"success\",\"result\":\"ok\",\"session_id\":\"s\",\"num_turns\":2}", Path.GetTempPath())));
        Assert.True(done.Ok); Assert.Equal(2, done.Turns);
    }

    [Fact(DisplayName = "Statik: Kennungen als Zahl (z. B. von einer KI) öffnen das Tragwerk statt die Seite zu sperren (14 Protokolle 0.9.45); Unlesbares öffnet leer")]
    public void Frame_numeric_ids()
    {
        var data = JsonNode.Parse("""{"frame":{"nodes":[{"id":1,"x":0,"y":0,"support":"fest"},{"id":2,"x":4,"y":0,"support":"rollen"}],"members":[{"id":10,"a":1,"b":2}],"loads":[{"kind":"strecke","target":10,"value":5}]}}""")!.AsObject();
        var m = FrameModel.From(data);
        Assert.Equal(["1", "2"], m.Nodes.Select(n => n.Id));
        var mem = Assert.Single(m.Members); Assert.Equal(("1", "2"), (mem.A, mem.B));
        Assert.Equal("10", Assert.Single(m.Loads).Target);
        Assert.Empty(FrameModel.From(JsonNode.Parse("""{"frame":{"nodes":"kaputt"}}""")!.AsObject()).Nodes);
    }
}
