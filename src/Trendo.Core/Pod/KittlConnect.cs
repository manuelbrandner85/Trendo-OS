namespace Trendo.Core.Pod;

/// <summary>Anmeldung am eigenen Kittl-Konto über Claude Code. Die Zugangsdaten verwaltet Claude Code je Windows-Konto, Trendo speichert keine.
/// Die Anmeldung braucht ein echtes Konsolenfenster: Ohne Terminal bricht <c>claude mcp login</c> ab.</summary>
public static class KittlConnect
{
    public const string Server = StudioMcp.Prefix + "kittl";

    public enum State { Unbekannt, Verbunden, AnmeldungNoetig, NichtEingerichtet }

    public static IReadOnlyList<string> AddArgs { get; } = ["mcp", "add", "--transport", "http", Server, StudioMcp.KittlMcpUrl, "-s", "local"];
    public static IReadOnlyList<string> LoginArgs { get; } = ["mcp", "login", Server];
    public static IReadOnlyList<string> LogoutArgs { get; } = ["mcp", "logout", Server];
    public static IReadOnlyList<string> GetArgs { get; } = ["mcp", "get", Server];

    /// <summary>Liest die Ausgabe von <c>claude mcp get</c>.</summary>
    public static State Parse(string? output)
    {
        if (string.IsNullOrWhiteSpace(output)) return State.Unbekannt;
        var line = output.Split('\n').FirstOrDefault(l => l.TrimStart().StartsWith("Status:", StringComparison.OrdinalIgnoreCase));
        if (line is null) return output.Contains("No MCP server", StringComparison.OrdinalIgnoreCase) ? State.NichtEingerichtet : State.Unbekannt;
        if (line.Contains("Needs authentication", StringComparison.OrdinalIgnoreCase)) return State.AnmeldungNoetig;
        return line.Contains("Connected", StringComparison.OrdinalIgnoreCase) ? State.Verbunden : State.Unbekannt;
    }

    public static string Text(State s) => s switch
    {
        State.Verbunden => "✓ Verbunden mit deinem Kittl-Konto",
        State.AnmeldungNoetig or State.NichtEingerichtet => "Anmeldung nötig",
        _ => "Verbindung noch nicht geprüft",
    };
}
