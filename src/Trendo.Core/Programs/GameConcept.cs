using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Trendo.Core.Programs;

/// <summary>T-0610 D1/D2: Spielideen finden. Aus Stichworten, Genre, Plattform, Stil und Perspektive entstehen Ideen, die sich bewerten und neu erzeugen lassen.
/// Mit KI frei formuliert, ohne KI aus einem eigenen Baukasten, damit die Funktion immer arbeitet. Der geführte Weg kommt nur mit Knöpfen zu einer Idee.</summary>
public static class GameConcept
{
    public sealed record Options(string Keywords = "", string Genre = "", string Platform = "", string Style = "", string Perspective = "", string Mood = "", string Scale = "");
    public sealed record Idea(string Title, string Pitch, string Loop, string Genre, string Platform, string Style, string Perspective, string Scale, string Theme);
    public sealed record PathStep(string Id, string Question, (string Label, string Value)[] Options);

    public static readonly string[] Platforms = ["PC", "Konsole", "Browser", "Smartphone"];
    public static readonly string[] Styles = ["Pixel-Art", "Comic", "Handgezeichnet", "Stilisiert 3D", "Fotorealistisch"];
    public static readonly string[] Perspectives = ["Von der Seite", "Von oben", "Isometrisch", "Schulterblick", "Ego-Perspektive"];

    public static readonly PathStep[] Path =
    [
        new("stimmung", "Wie soll sich dein Spiel anfühlen?", [("Entspannt", "entspannt"), ("Spannend", "spannend"), ("Lustig", "lustig"), ("Düster", "düster")]),
        new("spielart", "Was machst du im Spiel am liebsten?", [("Erkunden", "Action-Adventure"), ("Kämpfen", "Kampfspiel"), ("Knobeln", "Puzzle"), ("Aufbauen", "Aufbau/Strategie"), ("Springen und rennen", "Plattformer")]),
        new("aufwand", "Wie groß soll es werden?", [("Klein und schnell spielbar", "retro"), ("Mittelgroß in 3D", "indie3d"), ("Riesige Welt", "aaa")]),
    ];

    /// <summary>Antworten des geführten Wegs in Einstellungen für die Ideen übersetzen.</summary>
    public static Options PathOptions(IReadOnlyDictionary<string, string> answers)
    {
        var scale = answers.GetValueOrDefault("aufwand", "retro");
        var (platform, style, perspective) = scale switch
        {
            "aaa" => ("PC", "Fotorealistisch", "Schulterblick"),
            "indie3d" => ("PC", "Stilisiert 3D", "Schulterblick"),
            _ => ("Browser", "Pixel-Art", "Von der Seite"),
        };
        return new Options("", answers.GetValueOrDefault("spielart", ""), platform, style, perspective, answers.GetValueOrDefault("stimmung", ""), scale);
    }

    sealed record Theme(string Id, string Mood, string Title, string Hero, string Place, string Goal);

    static readonly Theme[] Themes =
    [
        new("insel", "entspannt", "Salz und Sprossen", "Eine junge Gärtnerin", "auf einer windigen Insel", "die verlassenen Beete wieder zum Blühen bringen"),
        new("post", "entspannt", "Letzte Post am Leuchtturm", "Ein alter Postbote", "an einer stillen Küste", "die vergessenen Briefe an ihre Empfänger bringen"),
        new("baeckerei", "entspannt", "Ofenwarm", "Ein Bäckerlehrling", "in einem Bergdorf", "die alte Backstube zum Treffpunkt aller Nachbarn machen"),
        new("tiefsee", "spannend", "Druckausgleich", "Eine Taucherin", "in einer versunkenen Stadt", "die letzte Schleuse öffnen, bevor das Wasser steigt"),
        new("eiszug", "spannend", "Kaltstart", "Eine Zugführerin", "auf einer Fahrt durch die Eiswüste", "den Zug am Laufen halten, bis die Rettung kommt"),
        new("mond", "spannend", "Funkstille", "Ein Techniker", "auf einer Station am Mond", "herausfinden, warum niemand mehr antwortet"),
        new("putzroboter", "lustig", "Staubsauger-Revolte", "Ein Putzroboter", "in einem chaotischen Bürohaus", "die Kollegen vom Streik überzeugen oder ihn anführen"),
        new("katzen", "lustig", "Kommissar Schnurr", "Eine Katzen-Detektivin", "in einer Kleinstadt voller Fischdiebe", "den größten Thunfisch-Raub aller Zeiten aufklären"),
        new("imbiss", "lustig", "Monster-Imbiss", "Ein überforderter Koch", "in einem Imbiss für Monster", "jeden noch so seltsamen Wunsch rechtzeitig erfüllen"),
        new("moor", "düster", "Nebelmoor", "Eine Totengräberin", "in einem Dorf am Moor", "herausfinden, wer die Gräber nachts öffnet"),
        new("uhren", "düster", "Die letzte Stunde", "Ein Uhrmacher", "in einer Stadt ohne Morgen", "die Nacht beenden, ohne seine Erinnerungen zu verlieren"),
        new("ubahn", "düster", "Endstation", "Eine Pendlerin", "in einem endlosen U-Bahn-Netz", "den Ausgang finden, bevor das Licht ausgeht"),
    ];

    static string LoopOf(string genre)
    {
        var g = genre.ToLowerInvariant();
        if (g.Contains("puzzle") || g.Contains("lern")) return "Rätsel ansehen → Idee ausprobieren → Lösung finden → schwereres Rätsel";
        if (g.Contains("aufbau") || g.Contains("simulation")) return "Sammeln → Bauen → Erweitern → Neues freischalten";
        if (g.Contains("kampf") || g.Contains("shooter")) return "Gegner lesen → Angreifen und Ausweichen → Beute → Stärker werden";
        if (g.Contains("rennen") || g.Contains("rennspiel")) return "Fahren → Bestzeit jagen → Aufrüsten → Neue Strecke";
        if (g.Contains("plattform") || g.Contains("jump")) return "Laufen → Springen → Sammeln → Nächster Abschnitt";
        return "Erkunden → Herausforderung meistern → Belohnung → Neues freischalten";
    }

    static string ScaleFor(string scale, string genre, string platform, string style, string perspective)
    {
        if (GameDesign.Scales.Any(s => s.Id == scale)) return scale;
        if (platform == "Browser" || style == "Pixel-Art") return "retro";
        if (style == "Fotorealistisch") return genre.Contains("Open World") || genre.Contains("RPG") ? "aaa" : "aa";
        if (style == "Stilisiert 3D" || perspective is "Schulterblick" or "Ego-Perspektive") return "indie3d";
        return "indie2d";
    }

    /// <summary>Ideen ohne KI aus dem Baukasten. Abgelehnte Themen fallen weg, gemochte kommen zuerst.</summary>
    public static List<Idea> Local(Options o, int seed, IReadOnlyCollection<string>? liked = null, IReadOnlyCollection<string>? disliked = null, int count = 5)
    {
        var rng = new Random(seed);
        bool Rejected(Theme t) => disliked?.Contains(t.Id) == true;
        var pool = Themes.Where(t => !Rejected(t) && (o.Mood.Length == 0 || t.Mood == o.Mood)).ToList();
        if (pool.Count < 3) pool.AddRange(Themes.Where(t => !Rejected(t) && !pool.Contains(t)));
        var chosen = pool.Select(t => (Theme: t, Order: rng.Next())).OrderBy(x => liked?.Contains(x.Theme.Id) == true ? 0 : 1).ThenBy(x => x.Order).Take(count).Select(x => x.Theme);
        string Pick(string wanted, IReadOnlyList<string> from) => wanted.Length > 0 ? wanted : from[rng.Next(from.Count)];
        return [.. chosen.Select(t =>
        {
            var genre = Pick(o.Genre, GameDirector.Genres); var platform = Pick(o.Platform, Platforms); var style = Pick(o.Style, Styles); var perspective = Pick(o.Perspective, Perspectives);
            var pitch = $"{t.Hero} muss {t.Place} {t.Goal}." + (o.Keywords.Trim().Length > 0 ? $" Im Mittelpunkt: {o.Keywords.Trim()}." : "");
            return new Idea(t.Title, pitch, LoopOf(genre), genre, platform, style, perspective, ScaleFor(o.Scale, genre, platform, style, perspective), t.Id);
        })];
    }

    public static string Instructions() =>
        "Du erfindest Spielideen für Menschen ohne Fachwissen. Antworte auf Deutsch. Liefere genau fünf eigenständige Ideen als JSON-Array in einem ```json-Block. " +
        "Felder je Idee: title (kurzer, eigener Titel), pitch (ein bis zwei Sätze: wer, wo, welches Ziel), loop (was man immer wieder tut, mit Pfeilen →), theme (ein Stichwort für das Thema). " +
        "Keine echten Personen, Marken, bekannten Spieltitel oder fremden Spielfiguren. Jede Idee soll sich deutlich von den anderen unterscheiden.";

    public static string Prompt(Options o, IEnumerable<string>? liked = null, IEnumerable<string>? disliked = null, IEnumerable<string>? seen = null)
    {
        var parts = new List<string> { "Finde Spielideen." };
        void Add(string label, string value) { if (value.Trim().Length > 0) parts.Add($"{label}: {value.Trim()}."); }
        Add("Stichworte", o.Keywords); Add("Genre", o.Genre); Add("Plattform", o.Platform); Add("Stil", o.Style); Add("Perspektive", o.Perspective); Add("Stimmung", o.Mood);
        Add("Gefallen hat", string.Join(", ", liked ?? [])); Add("Nicht gefallen hat", string.Join(", ", disliked ?? [])); Add("Bereits gezeigt, nicht wiederholen", string.Join(", ", seen ?? []));
        return string.Join(" ", parts);
    }

    static string S(JsonNode? n) => n?.GetValueKind() == JsonValueKind.String ? n.GetValue<string>().Trim() : "";

    /// <summary>KI-Antwort auswerten. Gewählte Einstellungen gelten für jede Idee, abgelehnte Themen und unbrauchbare Einträge fallen weg.</summary>
    public static List<Idea> Parse(string answer, Options o, IReadOnlyCollection<string>? disliked = null, int seed = 0)
    {
        var fenced = Regex.Match(answer, @"```(?:json)?\s*(\[[\s\S]*?\])\s*```");
        var text = fenced.Success ? fenced.Groups[1].Value : answer.IndexOf('[') is var a and >= 0 && answer.LastIndexOf(']') is var b && b > a ? answer[a..(b + 1)] : null;
        if (text is null) return [];
        JsonArray? list;
        try { list = JsonNode.Parse(text) as JsonArray; }
        catch (JsonException) { return []; }
        var rng = new Random(seed);
        string Pick(string wanted, IReadOnlyList<string> from) => wanted.Length > 0 ? wanted : from[rng.Next(from.Count)];
        var ideas = new List<Idea>();
        foreach (var item in (list ?? []).OfType<JsonObject>())
        {
            var (title, pitch) = (S(item["title"]), S(item["pitch"]));
            var theme = S(item["theme"]) is { Length: > 0 } t ? t : title;
            if (title.Length == 0 || pitch.Length == 0 || ideas.Any(i => i.Title == title) || disliked?.Contains(theme) == true) continue;
            var genre = Pick(o.Genre, GameDirector.Genres); var platform = Pick(o.Platform, Platforms); var style = Pick(o.Style, Styles); var perspective = Pick(o.Perspective, Perspectives);
            ideas.Add(new Idea(title, pitch, S(item["loop"]) is { Length: > 0 } l ? l : LoopOf(genre), genre, platform, style, perspective, ScaleFor(o.Scale, genre, platform, style, perspective), theme));
        }
        return [.. ideas.Take(6)];
    }

    /// <summary>Beschreibung für die lokale Bild-KI.</summary>
    public static string ImagePrompt(Idea i) => $"Konzeptbild für ein Videospiel: {i.Pitch} Genre {i.Genre}, Stil {i.Style}, Perspektive {i.Perspective}. Keine Schrift, keine Logos.";

    /// <summary>Aus der Idee wird der Anfang einer Spielbibel, in derselben Form wie beim Spiele-Prompt.</summary>
    public static JsonObject ToBible(Idea i) => GameDirector.Normalize(new JsonObject
    {
        ["title"] = i.Title, ["scale"] = i.Scale, ["genre"] = i.Genre, ["logline"] = i.Pitch, ["loop"] = i.Loop,
        ["style"] = $"{i.Style}, {i.Perspective}", ["platforms"] = i.Platform,
    });
}
