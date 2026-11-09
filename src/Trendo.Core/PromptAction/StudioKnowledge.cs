using System.Text.Json.Nodes;

namespace Trendo.Core.PromptAction;

/// <summary>Eine typische Aufgabe in einem Studio. <paramref name="Starter"/> kommt in die Eingabe, die Person ergänzt den Rest.</summary>
public sealed record StudioTask(string Id, string Label, string Hint, string Starter, IReadOnlyList<string> Keywords, IReadOnlyList<string> Needs);

/// <summary>Fachwissen eines Studios. Ergänzt <see cref="CapabilityCatalog"/> (Reife und Grenzen) um Zweck, Aufgaben, Formate und Qualität; keine zweite Registry.</summary>
public sealed record StudioKnowledge(
    string StudioId,
    string Purpose,
    IReadOnlyList<StudioTask> Tasks,
    IReadOnlyList<string> Programs,
    IReadOnlyList<string> Inputs,
    IReadOnlyList<string> Outputs,
    IReadOnlyList<string> Formats,
    IReadOnlyList<string> Workflows,
    IReadOnlyList<string> Quality,
    IReadOnlyList<string> Helpers,
    bool Detailed);

public static class StudioKnowledgeBase
{
    static StudioTask T(string id, string label, string hint, string starter, string[] keywords, params string[] needs) => new(id, label, hint, starter, keywords, needs);

    static readonly Dictionary<string, StudioKnowledge> Detailed = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CORE-06"] = new("CORE-06",
            "Eigene Spiele planen und bauen: vom kleinen Browser-Spiel bis zum Unreal-Projekt, mit Spielbibel, Figuren, Welt, Missionen, Leveln und Test.",
            [
                T("spiel-neu", "Neues Spiel erfinden", "Ein Satz genügt, der Rest entsteht von selbst", "Baue mir ein fertiges Spiel über ", ["spiel", "game", "erstell", "bau", "jump", "run", "plattformer", "rpg", "shooter", "rennspiel", "puzzle"], "Thema oder Spielidee"),
                T("level", "Level bauen", "Neue Level mit Münzen, Gegnern und Zielen", "Baue für mein Spiel ein neues Level mit ", ["level", "karte", "abschnitt", "welt", "schwier"], "offenes Spiel"),
                T("figur", "Figur oder Gegner entwerfen", "Held, Gegner, Boss mit Phasen", "Entwirf für mein Spiel eine Figur: ", ["figur", "charakter", "held", "gegner", "boss", "npc"], "offenes Spiel"),
                T("mission", "Geschichte und Missionen", "Haupt- und Nebenmissionen, die zusammenpassen", "Schreibe für mein Spiel Missionen über ", ["mission", "quest", "geschichte", "story", "aufgabe", "dialog"], "offenes Spiel"),
                T("pruefen", "Spiel prüfen und verbessern", "Sackgassen, unfaire Stellen, offene Punkte", "Prüfe mein Spiel und behebe alles, was ", ["prüf", "test", "fehler", "verbesser", "unfair", "sackgasse", "bug"], "offenes Spiel"),
            ],
            ["unreal", "unity", "godot", "blender"],
            ["Spielidee in einem Satz", "optional eigene Bilder, Töne und 3D-Modelle"],
            ["spielbares Browser-Spiel", "Unreal-, Unity- oder Godot-Projekt", "Spielbibel", "Level als Tiled-Karten"],
            [".tdoc", ".html", ".uproject", ".md", ".tmx"],
            ["Idee → Spielbibel → Level → Prüfen → Bauen → Spielen → Weitergeben"],
            ["Jedes Level hat Start und erreichbares Ziel", "Keine unfairen Sprünge oder Fallen am Start", "Spiel lässt sich bauen und starten", "Keine Platzhalter im fertigen Spiel"],
            ["CORE-05", "X-07", "STUDIO-MUSIK-01", "CORE-08"],
            true),
    };

    /// <summary>Wissen zu einem Studio. Ohne ausführliche Beschreibung wird es ehrlich aus dem Fachprofil abgeleitet und ist dann als nicht ausführlich markiert.</summary>
    public static StudioKnowledge? For(string? studioId)
    {
        if (studioId is null) return null;
        if (Detailed.TryGetValue(studioId, out var k)) return k;
        if (CapabilityCatalog.Find(studioId) is not { } p) return null;
        return new StudioKnowledge(p.Id, p.Title, [], [], [p.Inputs], [p.Outputs], [], [p.PlannedAction], [], [], false);
    }

    /// <summary>Passende Aufgaben zu einem Auftrag, beste zuerst. Die erste Aufgabe eines Studios ist die allgemeine und zählt nur, wenn keine speziellere passt.</summary>
    public static IReadOnlyList<StudioTask> Match(StudioKnowledge knowledge, string request)
    {
        var text = request.ToLowerInvariant();
        return knowledge.Tasks.Select((t, i) => (Task: t, General: i == 0, Score: t.Keywords.Count(text.Contains))).Where(x => x.Score > 0)
            .OrderBy(x => x.General).ThenByDescending(x => x.Score).Select(x => x.Task).ToList();
    }

    public static JsonObject ToJson(StudioKnowledge k, string? request = null)
    {
        static JsonArray A(IEnumerable<string> v) => new(v.Select(x => (JsonNode)x).ToArray());
        var json = new JsonObject
        {
            ["studio"] = k.StudioId, ["zweck"] = k.Purpose, ["ausfuehrlich"] = k.Detailed,
            ["grenzen"] = CapabilityCatalog.Find(k.StudioId)?.Blocker,
            ["aufgaben"] = new JsonArray(k.Tasks.Select(t => (JsonNode)new JsonObject { ["aufgabe"] = t.Id, ["text"] = t.Label, ["braucht"] = A(t.Needs) }).ToArray()),
            ["programme"] = A(k.Programs), ["eingaben"] = A(k.Inputs), ["ergebnisse"] = A(k.Outputs), ["formate"] = A(k.Formats),
            ["ablaeufe"] = A(k.Workflows), ["qualitaet"] = A(k.Quality), ["helfer_studios"] = A(k.Helpers),
        };
        if (request is { Length: > 0 }) json["passend"] = A(Match(k, request).Select(t => t.Id));
        return json;
    }
}

/// <summary>Ehrlicher Stand eines Fachprogramms auf diesem PC. <paramref name="Starts"/> ist null, wenn sich das ohne Editorstart nicht prüfen lässt.</summary>
public sealed record ProgramCheck(string Id, string Name, bool Installed, bool? Starts, string Interface, IReadOnlyList<string> Actions, string Rights, string Note)
{
    public static readonly IReadOnlyDictionary<string, string[]> KnownActions = new Dictionary<string, string[]>
    {
        ["unreal"] = ["Projekt mit Spielbibel anlegen", "Karten erzeugen", "Projekt bauen"],
        ["unity"] = ["Projekt anlegen", "Tests über die Kommandozeile"],
        ["godot"] = ["Projekt anlegen", "Headless-Prüfung"],
        ["blender"] = ["3D-Modelle erzeugen", "Rendern", "Export"],
    };

    /// <summary>Art der Schnittstelle, nur aus tatsächlichem Befund. <paramref name="mcpConnected"/>: im letzten Claude-Lauf gemeldete Verbindungen.</summary>
    public static string InterfaceOf(string id, bool installed, bool? starts, IReadOnlyCollection<string> mcpConnected)
    {
        if (!installed) return "keine, Programm fehlt";
        var mcp = mcpConnected.Any(m => m.Equals(id, StringComparison.OrdinalIgnoreCase));
        var cli = starts == true ? "Kommandozeile geprüft" : starts == false ? "Kommandozeile startet nicht" : "Kommandozeile nicht geprüft";
        return mcp ? "MCP verbunden im letzten Claude-Lauf, " + cli : cli + ", MCP nicht bestätigt";
    }
}
