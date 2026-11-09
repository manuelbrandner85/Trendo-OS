using Trendo.Core.Pod;
using Xunit;

namespace Trendo.Core.Tests;

public class KittlConnectTests
{
    [Theory(DisplayName = "Kittl: Status aus der Claude-Ausgabe")]
    [InlineData("trendo-kittl:\n  Status: ✔ Connected\n  Type: http", KittlConnect.State.Verbunden)]
    [InlineData("  Status: √ Connected", KittlConnect.State.Verbunden)]
    [InlineData("  Status: ! Needs authentication", KittlConnect.State.AnmeldungNoetig)]
    [InlineData("  Status: ⏸ Pending approval (run `claude` to approve)", KittlConnect.State.Unbekannt)]
    [InlineData("No MCP server found with name: trendo-kittl", KittlConnect.State.NichtEingerichtet)]
    [InlineData("", KittlConnect.State.Unbekannt)]
    public void Parse(string output, KittlConnect.State expected) => Assert.Equal(expected, KittlConnect.Parse(output));

    [Fact(DisplayName = "Kittl: Aufrufe nutzen den festen Servernamen und den offiziellen Zugang")]
    public void Args()
    {
        Assert.Equal("trendo-kittl", KittlConnect.Server);
        Assert.Contains(StudioMcp.KittlMcpUrl, KittlConnect.AddArgs);
        Assert.Equal(["mcp", "login", "trendo-kittl"], KittlConnect.LoginArgs);
    }
}
