using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// Anzeige wie in Claude: was Claude aus dem eigenen Claude mitbringt (Skills, Connectoren, Plugins, Agenten) und welcher Skill/Agent/Connector gerade arbeitet.
public class ClaudeCapsTests
{
    [Fact(DisplayName = "Claude-Anzeige: Start meldet Skills, verbundene Connectoren, Plugins, Agenten; Schritte zeigen Skill, Agent, Connector, Befehl, Internet")]
    public void Init_and_tools()
    {
        var init = ClaudeCode.Parse("""{"type":"system","subtype":"init","session_id":"s","model":"m","skills":["gameforge:gameforge-studio","pdf"],"mcp_servers":[{"name":"claude.ai Gmail","status":"connected"},{"name":"kaputt","status":"failed"}],"plugins":[{"name":"gameforge","path":"x"}],"agents":["Explore","gameforge:blender-3d"]}""", ".").OfType<ClaudeCode.Init>().Single();
        Assert.Equal(["gameforge:gameforge-studio", "pdf"], init.Skills); Assert.Equal(["claude.ai Gmail"], init.Connectors);
        Assert.Equal(["gameforge"], init.Plugins); Assert.Equal(2, init.Agents!.Count);
        ClaudeCode.Tool T(string name, string input) => ClaudeCode.Parse("{\"type\":\"assistant\",\"message\":{\"content\":[{\"type\":\"tool_use\",\"name\":\"" + name + "\",\"input\":" + input + "}]}}", ".").OfType<ClaudeCode.Tool>().Single();
        var sk = T("Skill", """{"skill":"gameforge:blender-asset"}"""); Assert.Equal(("Skill", "gameforge:blender-asset"), (sk.Badge, sk.Detail)); Assert.Contains("„gameforge:blender-asset“", sk.Text);
        var ag = T("Task", """{"subagent_type":"gameforge:blender-3d","description":"Modell bauen"}"""); Assert.Equal("Agent", ag.Badge); Assert.Contains("Modell bauen", ag.Text);
        var mc = T("mcp__claude_ai_Gmail__search_threads", "{}"); Assert.Equal(("Connector", "Gmail"), (mc.Badge, mc.Detail)); Assert.Equal("Connector Gmail: search threads", mc.Text);
        Assert.Equal("Befehl", T("Bash", """{"command":"npm test","description":"Tests laufen lassen"}""").Badge);
        Assert.Contains("„Godot Tilemap“", T("WebSearch", """{"query":"Godot Tilemap"}""").Text);
    }
}
