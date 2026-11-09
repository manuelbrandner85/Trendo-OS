using System.Text.Json.Nodes;

namespace Trendo.Core.PromptAction;

/// <summary>Eine typische Aufgabe in einem Studio. <paramref name="Starter"/> kommt in die Eingabe, die Person ergänzt den Rest.</summary>
public sealed record StudioTask(string Id, string Label, string Hint, string Starter, IReadOnlyList<string> Keywords, IReadOnlyList<string> Needs, string? Program = null);

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
        ["CORE-08"] = new("CORE-08",
            "Videos schneiden, Bilder gestalten, Ton bearbeiten und Medien umwandeln.",
            [
                T("medien", "Medien umwandeln", "Datei in ein anderes Format bringen", "Wandle diese Datei um: ", ["medien", "umwandel", "konvert", "format", "datei"], "Mediendatei"),
                new("schnitt", "Video schneiden", "Clips anordnen, kürzen, Musik dazu", "Schneide aus meinen Clips ein Video über ", ["video", "schneid", "schnitt", "clip", "film ab"], ["Videodateien"], "schnitt"),
                new("bild", "Bild oder Plakat gestalten", "Ebenen, Text, Formen, Export", "Gestalte ein Bild: ", ["bild", "foto", "plakat", "poster", "retusch", "logo", "zeichne"], [], "bild"),
                new("ton", "Ton bearbeiten", "Schneiden, lauter, sauberer", "Bearbeite diese Tonaufnahme: ", ["ton", "audio", "podcast", "rausch", "aufnahme"], ["Tondatei"], "ton"),
            ],
            ["ffmpeg"],
            ["Videos, Bilder oder Tondateien", "Beschreibung des Ziels"],
            ["fertiges Video", "fertiges Bild", "bearbeitete Tondatei"],
            [".tdoc", ".mp4", ".png", ".jpg", ".svg", ".wav"],
            ["Medien holen → Anordnen → Vorschau prüfen → Exportieren"],
            ["Export lässt sich öffnen und hat die gewünschte Länge oder Größe", "Keine fremden Inhalte ohne passende Lizenz"],
            ["STUDIO-MUSIK-01", "X-07", "STUDIO-FILM-01"],
            true),
        ["STUDIO-FILM-01"] = new("STUDIO-FILM-01",
            "Kurzfilme und Trailer planen: Drehbuch, Figuren, Orte, Einstellungen und Abgabe.",
            [
                T("drehbuch", "Drehbuch schreiben", "Vom Einfall zur fertigen Szene", "Schreibe ein Drehbuch über ", ["drehbuch", "film", "kurzfilm", "szene", "geschichte"], "Idee oder Thema"),
                T("shots", "Einstellungen planen", "Shotliste aus dem Drehbuch", "Plane die Einstellungen für ", ["einstellung", "shot", "kamera", "storyboard"], "Drehbuch"),
                T("trailer", "Trailer vorbereiten", "Die stärksten Momente auswählen", "Plane einen Trailer aus ", ["trailer", "teaser", "vorschau"], "Drehbuch oder Szenen"),
                T("auszug", "Figuren und Orte auflisten", "Drehbuchauszug für die Planung", "Erstelle den Drehbuchauszug für ", ["figur", "ort", "auszug", "drehplan", "besetzung"], "Drehbuch"),
            ],
            ["ffmpeg", "blender"],
            ["Idee oder vorhandenes Drehbuch", "optional Szenen als Video"],
            ["Drehbuch", "Shotliste", "Drehbuchauszug"],
            [".tdoc", ".fountain", ".pdf", ".csv", ".md"],
            ["Idee → Drehbuch → Auszug → Einstellungen → Schnitt"],
            ["Jede Szene hat einen Szenenkopf", "Figuren und Orte sind einheitlich benannt"],
            ["CORE-08", "STUDIO-MUSIK-01", "CORE-05"],
            true),
        ["STUDIO-MUSIK-01"] = new("STUDIO-MUSIK-01",
            "Lieder mit Gesang aus einer Beschreibung erzeugen, Spuren trennen, abmischen und Cover gestalten.",
            [
                T("lied", "Lied erzeugen", "Stil, Text, Sprache, Länge", "Erzeuge ein Lied über ", ["lied", "song", "musik", "melodie", "rap", "ballade"], "Thema oder Liedtext"),
                T("instrumental", "Instrumental erzeugen", "Musik ohne Gesang", "Erzeuge ein Instrumental im Stil ", ["instrumental", "ohne gesang", "hintergrundmusik"], "Stil"),
                T("trennen", "Spuren trennen", "Gesang und Begleitung getrennt", "Trenne die Spuren von ", ["trenn", "spur", "karaoke", "stem"], "Tondatei"),
                T("cover", "Cover gestalten", "Bild zum Lied", "Gestalte ein Cover für ", ["cover", "albumbild", "plattencover"], "Titel oder Stimmung"),
            ],
            ["ffmpeg"],
            ["Beschreibung, Stil, Sprache", "optional Liedtext oder Tondatei"],
            ["Lied als Audiodatei", "getrennte Spuren", "Cover"],
            [".mp3", ".wav", ".flac", ".png"],
            ["Beschreibung → Erzeugen → Anhören → Trennen oder Abmischen → Export"],
            ["Keine Stimmen bekannter Personen", "Liedtexte nur vom Nutzer oder selbst erfunden"],
            ["CORE-08", "STUDIO-FILM-01"],
            true),
        ["CORE-05"] = new("CORE-05",
            "3D-Modelle bauen und fotorealistisch rendern, für Spiele, Filme und den 3D-Druck.",
            [
                T("modell", "Modell bauen", "Aus einem Satz, mit echten Maßen", "Modelliere ", ["modell", "3d", "objekt", "bau", "burg", "haus", "tisch", "stuhl"], "Beschreibung oder Foto"),
                T("render", "Foto rendern", "Echte Materialien und Licht", "Rendere ein realistisches Bild von ", ["render", "foto", "bild", "material"], "Modell"),
                T("druck", "Für den 3D-Druck vorbereiten", "Prüfen und als STL ausgeben", "Bereite für den 3D-Druck vor: ", ["druck", "stl", "drucker", "bambu"], "Modell"),
                T("export", "Für andere Programme ausgeben", "GLB, FBX, OBJ", "Exportiere für ", ["export", "glb", "fbx", "obj", "unreal"], "Modell"),
            ],
            ["blender", "f3d"],
            ["Beschreibung, Maße oder Foto"],
            ["3D-Modell", "gerendertes Bild", "Druckdatei"],
            [".blend", ".glb", ".fbx", ".obj", ".stl", ".png"],
            ["Beschreiben → Modellieren → Materialien → Licht und Kamera → Rendern → Export"],
            ["Reale Maße und Proportionen", "Echte Materialien statt Einheitsfarbe", "Kontaktschatten, nichts schwebt"],
            ["CORE-06", "SPEC-08", "X-07"],
            true),
        ["CORE-07"] = new("CORE-07",
            "Webseiten und kleine Web-Anwendungen als echtes Projekt mit Quelltext bauen, testen und ansehen.",
            [
                T("seite", "Webseite bauen", "Ein Satz genügt", "Baue mir eine Webseite für ", ["webseite", "website", "homepage", "seite", "landing"], "Thema oder Firma"),
                T("funktion", "Funktion hinzufügen", "Formular, Galerie, Menü", "Füge meiner Webseite hinzu: ", ["formular", "galerie", "menü", "funktion", "einbauen", "kontakt"], "bestehendes Projekt"),
                T("design", "Design verbessern", "Schöner, klarer, schneller", "Verbessere das Design: ", ["design", "schöner", "farbe", "layout"], "bestehendes Projekt"),
                T("pruefen", "Webseite prüfen", "Fehler, Links, Handyansicht", "Prüfe meine Webseite auf ", ["prüf", "test", "fehler", "kaputt", "link", "handy"], "bestehendes Projekt"),
            ],
            ["git"],
            ["Thema oder bestehendes Projekt"],
            ["Quelltext der Webseite", "lokale Vorschau"],
            [".html", ".css", ".js"],
            ["Anforderung → Bauen → Starten → Prüfen → Verbessern"],
            ["Keine Fehler in der Konsole", "Funktioniert auf dem Handy", "Veröffentlichen nur nach Zustimmung"],
            ["CORE-08", "X-07", "CORE-05"],
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
        ["ffmpeg"] = ["Video und Ton umwandeln", "Schneiden und Exportieren"],
        ["f3d"] = ["3D-Dateien ansehen"],
        ["git"] = ["Versionen des Projekts"],
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
