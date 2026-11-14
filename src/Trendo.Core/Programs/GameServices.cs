using System.Text.Json.Nodes;

namespace Trendo.Core.Programs;

/// <summary>T-0650, E7: eine gemeinsame Auswahl für Bilder, 3D-Modelle, Geräusche, Musik und Sprache. Reihenfolge: eingerichtete Werkzeuge auf dem PC,
/// dann freie Quellen, dann noch nicht eingerichtete Werkzeuge. Bezahl-Dienste kommen nur vor, wenn die Person sie selbst eingerichtet hat.
/// Alles, was ins Spiel kommt, steht mit Herkunft und Lizenz in der Asset-Liste (<see cref="GameAssets"/>); ohne Lizenz wird nichts aufgenommen.</summary>
public static class GameServices
{
    public const string Local = "lokal", Free = "frei", Paid = "bezahlt";

    /// <summary>Tool: Werkzeugart für <c>KiToolRun</c> (bild, musik, vorlesen) oder leer, wenn der Dienst eine Seite oder ein Programm ist.</summary>
    public sealed record Provider(string Id, string Kind, string Name, string Type, string License, string How, string Tool = "", string Url = "");

    public static readonly (string Id, string Label)[] Kinds =
        [("bild", "Bilder"), ("3d", "3D-Modelle"), ("ton", "Geräusche"), ("musik", "Musik"), ("sprache", "Sprache")];

    public static readonly Provider[] All =
    [
        new("ki-bild", "bild", "Bild-KI auf dem PC", Local, "", "aus einer Beschreibung, in der KI-Bibliothek einrichten", "bild"),
        new("kenney-bild", "bild", "Kenney", Free, "CC0 1.0", "Spielgrafik und Kacheln über den Browser", "", "https://kenney.nl/assets?search="),
        new("oga-bild", "bild", "OpenGameArt", Free, "", "Grafik, Lizenz je Asset prüfen", "", "https://opengameart.org/art-search?keys="),
        new("blender", "3d", "Blender auf dem PC", Local, "eigene Arbeit", "eigene Modelle, Claude baut sie über die Blender-Verbindung"),
        new("polyhaven", "3d", "Poly Haven", Free, "CC0 1.0", "Modelle und Fotomaterialien über die offizielle Schnittstelle", "", "https://polyhaven.com/models"),
        new("quaternius", "3d", "Quaternius", Free, "CC0 1.0", "stilisierte Figuren und Umgebungen über den Browser", "", "https://quaternius.com/"),
        new("kenney-ton", "ton", "Kenney", Free, "CC0 1.0", "Geräusche-Pakete über den Browser", "", "https://kenney.nl/assets?search="),
        new("oga-ton", "ton", "OpenGameArt", Free, "", "Geräusche, Lizenz je Asset prüfen", "", "https://opengameart.org/art-search?keys="),
        new("ki-musik", "musik", "Musik-KI auf dem PC", Local, "", "aus einer Beschreibung, in der KI-Bibliothek einrichten", "musik"),
        new("oga-musik", "musik", "OpenGameArt", Free, "", "Musik, Lizenz je Stück prüfen", "", "https://opengameart.org/art-search?keys="),
        new("ki-sprache", "sprache", "Sprach-KI auf dem PC", Local, "", "Sätze vorlesen lassen, in der KI-Bibliothek einrichten", "vorlesen"),
    ];

    public sealed record Option(Provider Provider, bool Ready, string Note);

    /// <summary>Reihenfolge für eine Art. ready: ist ein Werkzeug auf dem PC eingerichtet (Id)? paidConfigured: Ids der Bezahl-Dienste, die die Person selbst eingerichtet hat.</summary>
    public static List<Option> Route(string kind, Func<string, bool> ready, IReadOnlySet<string>? paidConfigured = null, IEnumerable<Provider>? providers = null)
    {
        var paid = paidConfigured ?? new HashSet<string>();
        var list = (providers ?? All).Where(p => p.Kind == kind && (p.Type != Paid || paid.Contains(p.Id))).ToList();
        int Rank(Provider p) => p.Type == Local && ready(p.Id) ? 0 : p.Type == Free ? 1 : p.Type == Local ? 2 : 3;
        return [.. list.OrderBy(Rank).Select(p => new Option(p,
            p.Type != Local || ready(p.Id),
            p.Type == Local ? (ready(p.Id) ? "eingerichtet" : "noch nicht eingerichtet") : p.Type == Free ? "frei, sofort nutzbar" : "selbst eingerichtet"))];
    }

    /// <summary>Erste nutzbare Wahl, oder null.</summary>
    public static Option? Pick(string kind, Func<string, bool> ready, IReadOnlySet<string>? paidConfigured = null, IEnumerable<Provider>? providers = null) =>
        Route(kind, ready, paidConfigured, providers).FirstOrDefault(o => o.Ready);

    /// <summary>Erzeugtes oder geladenes Ergebnis ins Spiel aufnehmen: mit Herkunft und Lizenz, ohne Lizenz geht es nicht.</summary>
    public static JsonObject Add(JsonObject doc, string assetDir, string file, Provider p, string license, string detail, string url = "")
    {
        var lic = (license.Length > 0 ? license : p.License).Trim();
        if (lic.Length == 0) throw new ArgumentException($"Für „{Path.GetFileName(file)}“ fehlt die Lizenz – bitte von der Quelle übernehmen.");
        var source = detail.Length > 0 ? $"{p.Name} ({detail})" : p.Name;
        return GameAssets.Add(doc, assetDir, file, source, lic, p.Type == Local ? "erzeugt mit " + source : "", url, "datei");
    }

    /// <summary>Assets ohne Lizenzeintrag (sollte leer sein).</summary>
    public static List<string> MissingLicense(JsonObject doc) =>
        [.. (doc["assets"] as JsonArray ?? []).OfType<JsonObject>().Where(a => ((string?)a["license"] ?? "").Trim().Length == 0).Select(a => (string?)a["name"] ?? "")];
}
