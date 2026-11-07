using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Trendo.Core.Programs;

/// <summary>Spiele-Prompt (Nutzerwunsch 07.11.2026, „im Spiele-Studio eine Prompt-Eingabe, spezialisiert auf Spieleentwicklung – alle Arten von Spielen, mit Claude alle
/// Fähigkeiten, sonst die lokalen KIs“): ein Satz wird zur geprüften Spielbibel (Spiel aus einem Satz), fehlende Angaben kommen als Rückfragen mit Knöpfen,
/// Änderungen per Satz zeigen vorher, was sich ändert (Rückgängig über das Dokument), und ein Team aus Profi-Rollen macht nummerierte Verbesserungsvorschläge.
/// Nutzt die vorhandene Spielbibel (<see cref="GameDesign"/>), die Team-Rollen (<see cref="GamePro"/>) und die Trendo-KI – kein eigener KI-Kern.</summary>
public static class GameDirector
{
    /// <summary>Das KI-Entwicklerteam: jede Rolle mit Aufgabe – die KI arbeitet alle Rollen nacheinander ab (Game Director führt).</summary>
    public static readonly (string Id, string Label, string Task)[] Team =
    [
        ("director", "Game Director", "Vision, Spielart, Umfang, Engine und Zielgruppe festlegen; Entscheidungen begründen"),
        ("design", "Game Design", "Kernschleife, Mechaniken, Regeln, Schwierigkeit, Belohnungen, Steuerung (Tastatur, Maus, Controller)"),
        ("story", "Story & Welt", "Figuren mit Rollen, Gegner und Bosse mit Phasen, Regionen, Missionen ohne Sackgassen"),
        ("level", "Level Design", "Level mit Lernkurve: erst üben, dann fordern; erreichbare Ziele, faire Sprünge"),
        ("grafik", "Grafik & Animation", "Stil (fotoreal bis Pixel), echte Figuren statt Formen, Bewegungen, Licht"),
        ("ton", "Ton & Musik", "Geräusche, Musik je Stimmung, Sprecher"),
        ("test", "Test (QA)", "Spielbarkeit, Balance, Fehler, Bildrate – mit Vorschlägen"),
        ("produktion", "Produktion", "Meilensteine, Umfang passend zur Engine, Veröffentlichen"),
    ];

    /// <summary>Spielarten, die der Spiele-Prompt kennt (Auswahl für Rückfragen; jede Beschreibung ist erlaubt).</summary>
    public static readonly string[] Genres =
    [
        "Jump'n'Run", "Plattformer", "Arcade", "Puzzle", "Top-Down-Abenteuer", "Action-Adventure", "Rollenspiel (RPG)", "Open World", "Shooter", "Horror",
        "Rennspiel", "Simulation", "Aufbau/Strategie", "Sport", "Kampfspiel", "Survival", "Stealth", "Lernspiel", "Visual Novel", "Party-Spiel",
    ];

    static readonly string[] Keys = ["title", "scale", "genre", "logline", "loop", "controls", "style", "audience", "platforms", "sound", "menus", "systems", "browser", "world3d",
        "mechanics", "characters", "world", "quests", "level", "weapon", "abilities", "violence"];

    /// <summary>Anweisungen für die KI (lokal oder Claude): Profi-Team, Liefer-Format (eine Spielbibel als JSON), Rückfragen und Vorschläge.</summary>
    public static string Instructions(bool claude)
    {
        var team = string.Join("\n", Team.Select(t => $"- {t.Label}: {t.Task}"));
        var scales = string.Join(", ", GameDesign.Scales.Select(s => $"{s.Id} ({s.Name}: {s.Hint})"));
        return "Du bist das Spiele-Entwicklerteam von Trendo OS – ein professionelles Studio in einer KI. Du entwickelst jede Art von Spiel " +
               "(von Retro bis AAA+, 2D und 3D, " + string.Join(", ", Genres.Take(12)) + " …) für Menschen ohne Fachwissen. Antworte auf Deutsch.\n" +
               "Dein Team:\n" + team + "\n" +
               "Größen (scale): " + scales + ". retro/indie2d laufen sofort im Browser (browser: jump, plattform, breakout oder snake); indie3d/aa/aaa entstehen in Unreal Engine 5 " +
               "mit echten Figuren (MetaHuman), fotorealer Welt und Physik (world3d: level oder openworld).\n" +
               "Liefere IMMER die vollständige Spielbibel als EIN JSON-Objekt in einem ```json-Block mit diesen Feldern: " +
               "title, scale, genre, logline (ein Satz), loop (Kernschleife), mechanics (Liste), controls, style, audience, platforms, sound, menus, systems, browser, world3d, " +
               "characters (Liste: name, role = Held|Begleiter|Gegner|Boss|Nebenfigur, text, phases = Zahl, Bosse 2–4), world (Liste: name, text), " +
               "quests (Liste: title, kind = Haupt|Neben, goal, needs, gives, giver = Name einer Figur), level (nur Browser: Zeilen aus # . o E ^ S Z F C K D G N), " +
               "weapon (schwert|werfen oder leer), abilities (doppelsprung, sprint …), " +
               "violence = keine (Kinder, ab 0/6) | normal (ab 12: Treffer, Blut am Boden, Körper fallen) | brutal (ab 18: harte Treffer, Blut, abgetrennte Körperteile) – passend zu Zielgruppe und Spielart.\n" +
               "Alles muss zur Welt passen: Fahrzeuge, Häuser und Gegenstände nach Ort und Zeit (Wald: Geländewagen, Hütte; Stadt: Autos, Hochhäuser; Mittelalter: Pferde, Burg).\n" +
               "Regeln: Missionen ohne Sackgassen (was eine Mission braucht, gibt eine andere), jeder Auftraggeber ist eine Figur, mindestens eine Hauptmission, " +
               "AAA+ mindestens drei Regionen, keine echten Personen, Marken oder fremde Spielfiguren nachbauen – alles eigene Ideen.\n" +
               "Fehlt etwas Wichtiges, entscheide sinnvoll selbst UND stelle höchstens drei Rückfragen als Zeilen „Frage: … [Antwort A | Antwort B | Antwort C]“.\n" +
               "Danach höchstens fünf Zeilen „Vorschlag: …“ – konkrete Verbesserungen aus Sicht deines Teams.\n" +
               (claude ? "Du hast alle Fähigkeiten von Claude: arbeite im Programm „Spiele bauen“ (Studio-Dokument), baue, teste und verbessere das Spiel, bis es läuft; " +
                         "nutze Unreal, Blender und die freien Asset-Quellen von Trendo, wenn es das Spiel besser macht.\n"
                       : "");
    }

    /// <summary>Auftrag an die KI: neues Spiel aus einem Satz oder Änderung am vorhandenen Spiel (dann wird die aktuelle Spielbibel mitgegeben).</summary>
    public static string Prompt(string wish, JsonObject? current)
    {
        wish = wish.Trim();
        if (current is null) return "Entwickle dieses Spiel: " + wish;
        var cur = new JsonObject(); foreach (var k in Keys) if (current[k] is { } v) cur[k] = v.DeepClone();
        return "Ändere das vorhandene Spiel nach diesem Wunsch: " + wish + "\nLiefere danach die vollständige, geänderte Spielbibel.\nAktuelle Spielbibel:\n```json\n" +
               cur.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "\n```";
    }

    public sealed record Question(string Text, IReadOnlyList<string> Options);
    public sealed record Result(JsonObject? Doc, IReadOnlyList<Question> Questions, IReadOnlyList<string> Suggestions, string? Error);

    /// <summary>KI-Antwort auswerten: Spielbibel (geprüft und vervollständigt), Rückfragen, Vorschläge. Bei einer Änderung bleiben Felder, die die KI weglässt, erhalten.</summary>
    public static Result Parse(string answer, JsonObject? current = null)
    {
        var qs = new List<Question>(); var sug = new List<string>();
        foreach (var raw in answer.Replace("\r", "").Split('\n'))
        {
            var l = raw.Trim().TrimStart('-', '*', ' ');
            var q = Regex.Match(l, @"^Frage:\s*(.+?)\s*\[(.+)\]\s*$");
            if (q.Success && qs.Count < 3) qs.Add(new Question(q.Groups[1].Value.Trim(), [.. q.Groups[2].Value.Split('|').Select(x => x.Trim()).Where(x => x.Length > 0).Take(4)]));
            else if (Regex.Match(l, @"^Vorschlag:\s*(.+)$") is { Success: true } s && sug.Count < 5) sug.Add(s.Groups[1].Value.Trim());
        }
        var json = Regex.Match(answer, @"```(?:json)?\s*(\{[\s\S]*?)\s*```");
        var text = json.Success ? json.Groups[1].Value : answer.IndexOf('{') is var a and >= 0 && answer.LastIndexOf('}') is var b && b > a ? answer[a..(b + 1)] : null;
        if (text is null) return new Result(null, qs, sug, qs.Count > 0 ? null : "Die KI hat keine Spielbibel geliefert.");
        JsonObject? raw2;
        try { raw2 = JsonNode.Parse(text) as JsonObject; }
        catch (JsonException e) { return new Result(null, qs, sug, "Die Spielbibel der KI war fehlerhaft: " + e.Message); }
        if (raw2 is null) return new Result(null, qs, sug, "Die KI hat keine Spielbibel geliefert.");
        var merged = current?.DeepClone().AsObject() ?? new JsonObject();
        foreach (var k in Keys) if (raw2[k] is { } v) merged[k] = v.DeepClone();
        return new Result(Normalize(merged), qs, sug, null);
    }

    static string S(JsonNode? n) => n?.GetValueKind() == JsonValueKind.String ? n.GetValue<string>().Trim() : n?.GetValueKind() == JsonValueKind.Number ? n.ToString() : "";
    static JsonArray Arr(JsonNode? n) => n as JsonArray ?? [];

    /// <summary>Spielbibel vervollständigen und absichern: gültige Größe und Spielart, Listen in der erwarteten Form, Meilensteine, Level für Browser-Spiele.</summary>
    public static JsonObject Normalize(JsonObject d)
    {
        var o = d.DeepClone().AsObject();
        var genre = S(o["genre"]).ToLowerInvariant();
        var scale = S(o["scale"]).ToLowerInvariant();
        if (GameDesign.Scales.All(s => s.Id != scale))
            scale = genre.Contains("open world") || genre.Contains("rpg") ? "aaa" : genre.Contains("horror") || genre.Contains("3d") ? "indie3d" : genre.Contains("action") ? "aa" : "retro";
        o["scale"] = scale;
        if (S(o["title"]).Length == 0) o["title"] = "Neues Spiel";
        foreach (var k in new[] { "genre", "logline", "loop", "controls", "style", "audience", "platforms", "sound", "menus", "systems" }) o[k] = S(o[k]);
        o["mechanics"] = new JsonArray([.. Arr(o["mechanics"]).Select(S).Where(x => x.Length > 0).Take(12).Select(x => (JsonNode)x)]);
        o["characters"] = new JsonArray([.. Arr(o["characters"]).OfType<JsonObject>().Take(20).Select(c => (JsonNode)new JsonObject
        {
            ["name"] = S(c["name"]) is { Length: > 0 } n ? n : "Figur", ["role"] = GameDesign.Roles.Contains(S(c["role"])) ? S(c["role"]) : "Nebenfigur",
            ["text"] = S(c["text"]), ["phases"] = Math.Clamp((int)ProgramDocument.ToNumber(c["phases"]), 0, 6),
        })]);
        o["world"] = new JsonArray([.. Arr(o["world"]).OfType<JsonObject>().Take(20).Select(w => (JsonNode)new JsonObject { ["name"] = S(w["name"]) is { Length: > 0 } n ? n : "Region", ["text"] = S(w["text"]) })]);
        o["quests"] = new JsonArray([.. Arr(o["quests"]).OfType<JsonObject>().Take(30).Select(q => (JsonNode)new JsonObject
        {
            ["title"] = S(q["title"]) is { Length: > 0 } t ? t : "Mission", ["kind"] = S(q["kind"]) == "Haupt" ? "Haupt" : "Neben",
            ["goal"] = S(q["goal"]), ["needs"] = S(q["needs"]), ["gives"] = S(q["gives"]), ["giver"] = S(q["giver"]),
        })]);
        var engine = GameDesign.ScaleOf(scale).Engine;
        var browser = S(o["browser"]).ToLowerInvariant();
        if (engine == "browser")
        {
            if (browser is not ("jump" or "plattform" or "breakout" or "snake"))
                browser = genre.Contains("snake") || genre.Contains("schlange") ? "snake" : genre.Contains("arcade") || genre.Contains("breakout") || genre.Contains("mauer") ? "breakout" : genre.Contains("jump") ? "jump" : "plattform";
            o["browser"] = browser;
            var rows = Arr(o["level"]).Select(S).Where(r => r.Length > 0).ToList();
            if (rows.Count == 0 || rows.Any(r => r.Any(ch => !GameStudio.Tiles.Contains(ch))))
                rows = browser == "jump" ? [.. GameStudio.DefaultJump] : browser == "breakout" ? [.. GameStudio.DefaultBreakout] : browser == "plattform" ? [.. GameStudio.DefaultPlatform] : [];
            o["level"] = new JsonArray([.. rows.Select(r => (JsonNode)r)]);
        }
        else
        {
            o["browser"] = "";
            o["world3d"] = S(o["world3d"]) is "level" or "openworld" ? S(o["world3d"]) : scale is "aaa" || genre.Contains("open world") ? "openworld" : "level";
        }
        o["weapon"] = S(o["weapon"]) is "schwert" or "werfen" ? S(o["weapon"]) : "";
        // Gewalt-Darstellung: ausdrücklich gewählt, sonst nach Zielgruppe (Kinder → keine)
        var aud = S(o["audience"]).ToLowerInvariant();
        o["violence"] = S(o["violence"]) is "keine" or "normal" or "brutal" ? S(o["violence"]) : aud.Contains("kind") || aud.Contains("ab 6") || aud.Contains("familie") ? "keine" : "normal";
        o["abilities"] = new JsonArray([.. Arr(o["abilities"]).Select(S).Where(a => GameStudio.Abilities.Any(x => x.Id == a)).Distinct().Select(a => (JsonNode)a)]);
        if (o["milestones"] is not JsonArray ms || ms.Count == 0)
            o["milestones"] = new JsonArray([.. GameDesign.Milestones(scale).Select(m => (JsonNode)new JsonObject { ["name"] = m, ["done"] = false })]);
        return o;
    }

    /// <summary>Was ändert sich? Kurze Zeilen für die Vorschau („Titel: alt → neu“, „+ Figur …“, „− Mission …“).</summary>
    public static List<string> Diff(JsonObject old, JsonObject neu)
    {
        var lines = new List<string>();
        var labels = new Dictionary<string, string> { ["title"] = "Titel", ["scale"] = "Umfang", ["genre"] = "Genre", ["logline"] = "Idee", ["loop"] = "Kernschleife", ["controls"] = "Steuerung",
            ["style"] = "Stil", ["sound"] = "Ton", ["browser"] = "Spielart", ["world3d"] = "Welt", ["weapon"] = "Waffe", ["violence"] = "Gewalt" };
        foreach (var (k, l) in labels)
            if (S(old[k]) != S(neu[k])) lines.Add($"{l}: {Short(S(old[k]))} → {Short(S(neu[k]))}");
        void Named(string key, string label, string nameKey)
        {
            var a = Arr(old[key]).OfType<JsonObject>().Select(x => S(x[nameKey])).ToList(); var b = Arr(neu[key]).OfType<JsonObject>().Select(x => S(x[nameKey])).ToList();
            foreach (var x in b.Except(a)) lines.Add($"+ {label} „{x}“");
            foreach (var x in a.Except(b)) lines.Add($"− {label} „{x}“");
            foreach (var x in a.Intersect(b))
                if (Arr(old[key]).OfType<JsonObject>().First(o => S(o[nameKey]) == x).ToJsonString() != Arr(neu[key]).OfType<JsonObject>().First(o => S(o[nameKey]) == x).ToJsonString())
                    lines.Add($"~ {label} „{x}“ geändert");
        }
        Named("characters", "Figur", "name"); Named("world", "Region", "name"); Named("quests", "Mission", "title");
        var ma = Arr(old["mechanics"]).Select(S).ToList(); var mb = Arr(neu["mechanics"]).Select(S).ToList();
        foreach (var x in mb.Except(ma)) lines.Add($"+ Mechanik „{x}“");
        foreach (var x in ma.Except(mb)) lines.Add($"− Mechanik „{x}“");
        if (Arr(old["level"]).ToJsonString() != Arr(neu["level"]).ToJsonString()) lines.Add("~ Level geändert");
        return lines;
        static string Short(string s) => s.Length == 0 ? "–" : s.Length > 60 ? s[..60] + " …" : s;
    }

    /// <summary>Verbesserungsvorschläge des Teams ohne KI (immer verfügbar): aus der Studio-Prüfung, der Vollständigkeit und dem letzten Prüfbericht (✗-Zeilen).</summary>
    public static List<string> Suggest(JsonObject d, string? report = null)
    {
        var s = new List<string>();
        foreach (var w in GameDesign.Check(d)) s.Add("Test (QA): " + w);
        foreach (var (k, v) in GameDesign.Completeness(d).Where(x => x.Value < 100).OrderBy(x => x.Value))
            s.Add(k switch
            {
                "Spielbibel" => "Game Design: Idee, Kernschleife, Mechaniken und Steuerung vervollständigen – sie tragen jede Entscheidung.",
                "Figuren" => "Story & Welt: mehr Figuren mit klaren Rollen (Held, Begleiter, Gegner, Boss) und Beschreibung.",
                "Welt" => "Level Design: weitere Regionen bzw. Level mit eigener Stimmung planen.",
                "Missionen" => "Story & Welt: Haupt- und Nebenmissionen mit klaren Zielen ergänzen.",
                "Systeme" => "Game Design: Systeme festlegen (z. B. Inventar, Fähigkeiten, Speichern).",
                "Ton" => "Ton & Musik: Geräusche und Musik je Stimmung beschreiben.",
                _ => $"Produktion: Bereich „{k}“ ist erst zu {v} % geplant.",
            });
        foreach (var l in (report ?? "").Split('\n').Select(x => x.Trim()).Where(x => x.StartsWith('✗') || x.StartsWith('•')))
            s.Add("Test (QA): " + l.TrimStart('✗', '•', ' ') + " – beheben und erneut prüfen.");
        var chars = Arr(d["characters"]).OfType<JsonObject>().ToList();
        if (GameDesign.ScaleOf(S(d["scale"])).Engine == "unreal" && !chars.Any(c => S(c["role"]) is "Gegner" or "Boss"))
            s.Add("Game Design: Gegner oder einen Boss einplanen – Spannung entsteht durch Widerstand.");
        if (Arr(d["mechanics"]).Count < 3) s.Add("Game Design: mindestens drei Mechaniken, die sich gegenseitig verstärken (z. B. Erkunden → Finden → Verbessern).");
        return [.. s.Distinct().Take(10)];
    }
}
