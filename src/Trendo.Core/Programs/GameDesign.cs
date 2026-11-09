using System.Text;
using System.Text.Json.Nodes;

namespace Trendo.Core.Programs;

/// <summary>T-0231 / Wunsch 27.10.2026: Spiele-Studio als Programm – vom Retro-Spiel bis AAA+. Ein Spielprojekt mit Spielbibel, Figuren und Bossen,
/// Welt und Level, Missionen, Produktion (Meilensteine) und Bauen (Browser oder Unreal). Prüfungen wie in einem Spielestudio:
/// Vollständigkeit je Bereich, Bosse ohne Phasen, Missions-Sackgassen, Umfang passend zur Engine.</summary>
public static class GameDesign
{
    public sealed record Scale(string Id, string Name, string Engine, string Hint);
    public static readonly Scale[] Scales =
    [
        new("retro", "Retro (2D-Pixel)", "browser", "Kleines Spiel, sofort im Browser spielbar"),
        new("indie2d", "Indie 2D", "browser", "Mehrere Level, eigene Grafik und Musik"),
        new("indie3d", "Indie 3D", "unreal", "3D-Welt in Unreal Engine 5, kleines Team"),
        new("aa", "AA", "unreal", "Größere 3D-Welt, Story, Kampf, eigene Figuren"),
        new("aaa", "AAA+", "unreal", "Unreal Engine 5 mit Lumen, Nanite, World Partition und Cinematics"),
    ];
    public static Scale ScaleOf(string id) => Scales.FirstOrDefault(s => s.Id == id) ?? Scales[0];

    public static readonly string[] Roles = ["Held", "Begleiter", "Gegner", "Boss", "Nebenfigur"];
    public static readonly string[] Sections = ["Spielbibel", "Figuren", "Welt", "Missionen", "Systeme", "Ton", "Menüs"];

    /// <summary>Meilensteine je Umfang (mit Abnahme-Tor wie in der Spieleproduktion).</summary>
    public static string[] Milestones(string scale) => scale switch
    {
        "retro" => ["Idee in einem Satz", "Spielbarer Prototyp", "Drei Level", "Feinschliff: Ton, Punkte, Menü", "Veröffentlichen"],
        "indie2d" => ["Konzept", "Prototyp", "Vertical Slice (ein fertiges Level)", "Alle Level", "Beta-Test", "Veröffentlichen"],
        "indie3d" => ["Konzept", "Graubox-Prototyp in Unreal", "Vertical Slice", "Alpha (alles spielbar)", "Beta (alles fertig)", "Release"],
        _ => ["Konzept und Spielbibel", "Graubox-Prototyp", "Vertical Slice in Endqualität", "Pre-Production abgeschlossen", "Alpha", "Beta", "Gold Master", "Release", "DLC-Plan"],
    };

    public sealed record Template(string Id, string Name, string Hint, string Scale, string Genre, string BrowserTemplate, Func<JsonObject> Bible);

    static JsonObject Doc(string title, string scale, string genre, string logline, string loop, string[] mechanics, string controls, string style,
        (string Name, string Role, string Text, int Phases)[] chars, (string Name, string Text)[] world, (string Title, string Kind, string Goal, string Needs, string Gives, string Giver)[] quests, string browser = "")
    {
        var d = new JsonObject
        {
            ["title"] = title, ["scale"] = scale, ["genre"] = genre, ["logline"] = logline, ["loop"] = loop, ["controls"] = controls, ["style"] = style,
            ["audience"] = "", ["platforms"] = scale is "retro" or "indie2d" ? "Browser, PC" : "PC, Konsole", ["sound"] = "", ["menus"] = "Startmenü, Pause, Einstellungen, Spielstand",
            ["systems"] = scale is "retro" ? "Punkte, Leben" : "Inventar, Fähigkeiten, Speichern",
            ["mechanics"] = new JsonArray([.. mechanics.Select(m => (JsonNode)m)]),
            ["characters"] = new JsonArray([.. chars.Select(c => (JsonNode)new JsonObject { ["name"] = c.Name, ["role"] = c.Role, ["text"] = c.Text, ["phases"] = c.Phases })]),
            ["world"] = new JsonArray([.. world.Select(w => (JsonNode)new JsonObject { ["name"] = w.Name, ["text"] = w.Text })]),
            ["quests"] = new JsonArray([.. quests.Select(q => (JsonNode)new JsonObject { ["title"] = q.Title, ["kind"] = q.Kind, ["goal"] = q.Goal, ["needs"] = q.Needs, ["gives"] = q.Gives, ["giver"] = q.Giver })]),
            ["milestones"] = new JsonArray([.. Milestones(scale).Select(m => (JsonNode)new JsonObject { ["name"] = m, ["done"] = false })]),
            ["browser"] = browser,
            ["level"] = new JsonArray([.. (browser == "jump" ? GameStudio.DefaultJump : browser == "breakout" ? GameStudio.DefaultBreakout : browser == "plattform" ? GameStudio.DefaultPlatform : []).Select(r => (JsonNode)r)]),
        };
        return d;
    }

    public static readonly Template[] Templates =
    [
        new("leer", "Leeres Spiel", "Alles selbst festlegen", "retro", "", "jump", () => Doc("Neues Spiel", "retro", "", "", "", [], "", "", [], [], [], "jump")),
        new("retro-jump", "Retro-Jump'n'Run", "8-Bit-Hüpfspiel, Level im Raster bauen", "retro", "Plattformer", "jump", () => Doc("Pixel-Sprung", "retro", "Plattformer",
            "Ein mutiger Pixelheld springt über Hindernisse, um den Schatz am Ende der Welt zu finden.", "Laufen → Springen → Ausweichen → Punkte sammeln → nächstes Level",
            ["Springen", "Ausweichen", "Punkte sammeln"], "Leertaste: Springen", "Pixel-Grafik, 16 Farben, Chiptune",
            [("Pixel", "Held", "Kleiner Abenteurer mit roter Mütze", 0)], [("Wiesenwelt", "Grüne Hügel, einfache Hindernisse")], [("Ins Ziel", "Haupt", "Erreiche das Ende des Levels", "", "", "")], "jump")),
        new("retro-plattform", "Retro-Plattformer", "Level selbst malen: Boden, Münzen, Gegner, Stacheln, Ziel", "retro", "Plattformer", "plattform", () => Doc("Münzjäger", "retro", "Plattformer",
            "Ein flinker Held sammelt Münzen über Abgründe hinweg und erreicht die Flagge.", "Laufen → Springen → Münzen sammeln → Gegnern ausweichen → Ziel",
            ["Laufen", "Springen", "Sammeln", "Gegner besiegen"], "Pfeiltasten laufen, Leertaste springt, auf Gegner springen besiegt sie", "Pixel-Grafik, kräftige Farben",
            [("Flink", "Held", "Schneller Läufer mit Sprungschuhen", 0)], [("Grasland", "Hügel, Abgründe, Stachelgruben")], [("Zur Flagge", "Haupt", "Erreiche die Flagge am Ende", "", "", "")], "plattform")),
        new("retro-breakout", "Retro-Mauerbrecher", "Klassiker mit eigenem Steinmuster", "retro", "Arcade", "breakout", () => Doc("Steinbrecher", "retro", "Arcade",
            "Zerstöre mit Ball und Schläger jede Mauer.", "Ball abprallen lassen → Steine treffen → Mauer leeren", ["Abprallen", "Zielen"], "Maus oder Pfeiltasten", "Neon-Pixel",
            [], [("Mauerhalle", "Jede Halle hat ein neues Steinmuster")], [("Halle leeren", "Haupt", "Alle Steine zerstören", "", "", "")], "breakout")),
        new("retro-snake", "Retro-Schlange", "Schnell, einfach, süchtig machend", "retro", "Arcade", "snake", () => Doc("Pixelschlange", "retro", "Arcade",
            "Friss Punkte, werde länger, beiß dich nicht selbst.", "Bewegen → Fressen → Wachsen", ["Lenken", "Wachsen"], "Pfeiltasten", "Klassisch grün auf schwarz",
            [], [("Spielfeld", "Ein Raster mit Rand")], [("Rekord", "Haupt", "Erreiche 20 Punkte", "", "", "")], "snake")),
        new("topdown", "Top-Down-Abenteuer", "Zelda-artig: Erkunden, Rätsel, Dungeons", "indie2d", "Action-Adventure", "", () => Doc("Hüter des Hains", "indie2d", "Action-Adventure",
            "Eine junge Hüterin muss drei Kristalle finden, um den Wald zu heilen.", "Erkunden → Rätsel lösen → Gegenstand finden → neuen Bereich öffnen",
            ["Erkunden", "Rätsel", "Nahkampf", "Gegenstände"], "WASD bewegen, Leertaste Schwert, E benutzen", "Pixel-Art 32 px, warme Farben",
            [("Lina", "Held", "Junge Hüterin mit Laterne", 0), ("Moosgeist", "Boss", "Uralter Waldgeist", 3)],
            [("Hain", "Startgebiet"), ("Dunkler Wald", "Erster Dungeon")],
            [("Der erste Kristall", "Haupt", "Finde den grünen Kristall", "Laterne", "Grüner Kristall", "Lina"), ("Die Laterne", "Neben", "Hol die Laterne beim Händler", "", "Laterne", "Lina")])),
        new("action3d", "3D-Action (Unreal)", "Third-Person mit Kampf und Bossen", "aa", "Action", "", () => Doc("Ascheklinge", "aa", "Action-RPG",
            "Ein Söldner kämpft sich durch eine zerfallende Festung, um seinen Bruder zu retten.", "Erkunden → Kämpfen → Beute → Verbessern → Boss",
            ["Nahkampf mit Ausweichen", "Fähigkeiten", "Beute", "Verbesserungen"], "Gamepad oder Maus/Tastatur", "Dunkle Fantasy, realistisch",
            [("Kael", "Held", "Söldner mit Zweihänder", 0), ("Der Aschekönig", "Boss", "Herrscher der Festung", 3)],
            [("Vorhof", "Tutorial-Bereich"), ("Thronsaal", "Bossarena")],
            [("Zum Thron", "Haupt", "Besiege den Aschekönig", "Schlüssel der Festung", "", "Kael"), ("Der Schlüssel", "Neben", "Finde den Schlüssel im Vorhof", "", "Schlüssel der Festung", "Kael")])),
        new("openworld", "Open-World AAA+", "Unreal 5: Lumen, Nanite, World Partition", "aaa", "Open-World-RPG", "", () => Doc("Erbe der Sterne", "aaa", "Open-World-RPG",
            "In einer sterbenden Welt sucht eine Kartografin die letzten Sternenkarten, bevor der Himmel erlischt.", "Erkunden → Entdecken → Quest → Kampf/Dialog → Belohnung → Fortschritt der Welt",
            ["Freie Erkundung", "Reiten", "Kampf", "Dialoge mit Entscheidungen", "Crafting", "Tag/Nacht"], "Gamepad zuerst, Maus/Tastatur", "Fotorealistisch, filmische Beleuchtung",
            [("Aria", "Held", "Kartografin", 0), ("Vesh", "Begleiter", "Ehemaliger Söldner", 0), ("Der Sternfresser", "Boss", "Uraltes Wesen am Himmel", 4)],
            [("Küstenland", "Startregion"), ("Glasöde", "Wüste aus Sternenglas"), ("Himmelsturm", "Finale")],
            [("Die erste Karte", "Haupt", "Finde die Sternenkarte im Leuchtturm", "Leuchtturmschlüssel", "Sternenkarte I", "Aria"), ("Licht im Turm", "Neben", "Hilf dem Wärter", "", "Leuchtturmschlüssel", "Vesh")])),
        new("horror", "Horror (Unreal)", "Atmosphäre, Licht und Ton", "indie3d", "Horror", "", () => Doc("Das stille Haus", "indie3d", "Horror",
            "Eine Nacht in einem Haus, das sich jede Stunde verändert.", "Erkunden → Hinweise → Rätsel → Flucht", ["Schleichen", "Taschenlampe", "Rätsel"], "WASD, Maus, F Lampe", "Realistisch, dunkel, wenig Licht",
            [("Mara", "Held", "Studentin", 0)], [("Erdgeschoss", "Beginn"), ("Keller", "Finale")], [("Der Ausgang", "Haupt", "Finde den Kellerschlüssel und fliehe", "Kellerschlüssel", "", "Mara"), ("Spuren", "Neben", "Finde den Schlüssel im Arbeitszimmer", "", "Kellerschlüssel", "Mara")])),
    ];

    static IEnumerable<JsonObject> Objs(JsonObject d, string key) => (d[key] as JsonArray ?? []).OfType<JsonObject>();
    static string S(JsonNode? n) => n?.GetValueKind() == System.Text.Json.JsonValueKind.String ? n.GetValue<string>() : "";

    /// <summary>Vollständigkeit je Bereich (0–100).</summary>
    public static Dictionary<string, int> Completeness(JsonObject d)
    {
        int Of(params bool[] parts) => parts.Length == 0 ? 0 : parts.Count(x => x) * 100 / parts.Length;
        var chars = Objs(d, "characters").ToList(); var quests = Objs(d, "quests").ToList();
        return new()
        {
            ["Spielbibel"] = Of(S(d["logline"]).Length > 10, S(d["loop"]).Length > 5, (d["mechanics"] as JsonArray)?.Count > 0, S(d["controls"]).Length > 2, S(d["style"]).Length > 2, S(d["genre"]).Length > 2),
            ["Figuren"] = Of(chars.Any(c => S(c["role"]) == "Held"), chars.Count >= (S(d["scale"]) is "retro" ? 1 : 3), chars.All(c => S(c["text"]).Length > 3)),
            ["Welt"] = Of(Objs(d, "world").Any(), Objs(d, "world").Count() >= (S(d["scale"]) is "retro" ? 1 : 2), S(d["scale"]) != "retro" || (d["level"] as JsonArray)?.Count > 0),
            ["Missionen"] = Of(quests.Any(q => S(q["kind"]) == "Haupt"), quests.Count >= (S(d["scale"]) is "retro" ? 1 : 2), quests.All(q => S(q["goal"]).Length > 3)),
            ["Systeme"] = Of(S(d["systems"]).Length > 3),
            ["Ton"] = Of(S(d["sound"]).Length > 3),
            ["Menüs"] = Of(S(d["menus"]).Length > 3),
        };
    }

    public static int Overall(JsonObject d) { var c = Completeness(d); return c.Values.Sum() / c.Count; }

    /// <summary>Prüfungen wie in einem Spielestudio: Bosse ohne Phasen, Missions-Sackgassen, unbekannte Auftraggeber, Umfang passend zur Engine.</summary>
    public static List<string> Check(JsonObject d)
    {
        var w = new List<string>();
        var chars = Objs(d, "characters").ToList(); var names = chars.Select(c => S(c["name"])).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var b in chars.Where(c => S(c["role"]) == "Boss" && ProgramDocument.ToNumber(c["phases"]) < 2)) w.Add($"Boss „{S(b["name"])}“ hat weniger als zwei Phasen – Bosse wirken mit 2–4 Phasen stärker.");
        var quests = Objs(d, "quests").ToList();
        var given = quests.Select(q => S(q["gives"])).Where(x => x.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var q in quests.Where(q => S(q["needs"]).Length > 0 && !given.Contains(S(q["needs"]))))
            w.Add($"Mission „{S(q["title"])}“ braucht „{S(q["needs"])}“, aber keine Mission gibt es – Sackgasse.");
        foreach (var q in quests.Where(q => S(q["giver"]).Length > 0 && !names.Contains(S(q["giver"]))))
            w.Add($"Mission „{S(q["title"])}“: Auftraggeber „{S(q["giver"])}“ gibt es nicht unter den Figuren.");
        if (!quests.Any(q => S(q["kind"]) == "Haupt")) w.Add("Es gibt noch keine Hauptmission.");
        var scale = ScaleOf(S(d["scale"]));
        if (scale.Engine == "browser" && S(d["browser"]).Length == 0) w.Add("Für ein Browser-Spiel bitte eine Spielart wählen (Hüpfspiel, Mauerbrecher, Schlange oder Plattformer) – oder Unreal-Umfang wählen.");
        if (scale.Id == "aaa" && Objs(d, "world").Count() < 3) w.Add("AAA+: Plane mindestens drei Regionen – die Welt trägt das Spiel.");
        if (d["level"] is JsonArray lv && lv.Select(x => S(x)).Any(r => r.Any(ch => !GameStudio.Tiles.Contains(ch)))) w.Add("Im Level nur # . o E ^ S Z F C K D G N.");
        if (S(d["browser"]) == "plattform") w.AddRange(GameStudio.CheckPlatform((d["level"] as JsonArray ?? []).Select(S).ToList()));
        return w;
    }

    /// <summary>Spielbibel als Markdown (für das Team, das Unreal-Projekt oder zum Ausdrucken).</summary>
    public static string BibleMarkdown(JsonObject d) => GameDocument.Markdown(d);

    /// <summary>Ereignis-Regeln aus dem Projekt („rules“: [{when, n, do}]).</summary>
    /// <summary>Waffe der Spielfigur aus dem Dokument („weapon“), unbekannt = keine.</summary>
    public static string Weapon(JsonObject d) => S(d["weapon"]) is var w && GameStudio.Weapons.Any(x => x.Id == w) ? w : "";

    /// <summary>Landschaft des Spiels aus Titel, Idee, Stil, Genre und Regionen: „savanne“, „hof“ oder „wald“ (gemäßigt, Standard).
    /// Bestimmt z. B., welche Tiere dort leben – Hyänen in der Savanne, Hirsch, Fuchs und Wolf im Wald, Schweine auf dem Hof.</summary>
    public static string Biome(JsonObject d)
    {
        if (S(d["biome"]) is "savanne" or "wald" or "hof") return S(d["biome"]); // ausdrücklich gewählt
        var t = string.Join(" ", new[] { S(d["title"]), S(d["logline"]), S(d["style"]), S(d["genre"]) }.Concat(Objs(d, "world").Select(w => S(w["name"]) + " " + S(w["text"])))).ToLowerInvariant();
        int Hits(params string[] k) => k.Count(t.Contains);
        var sav = Hits("savanne", "steppe", "afrika", "safari", "wüste", "serengeti", "trockenland", "dürre");
        var hof = Hits("bauernhof", "farm", "hof ", "dorf", "weide", "stall", "acker");
        var wald = Hits("wald", "forst", "förster", "gebirge", "berg", "tanne", "see", "wiese", "jäger");
        return sav > wald && sav >= hof ? "savanne" : hof > wald && hof > sav ? "hof" : "wald";
    }

    /// <summary>Objekt-Texte (Gegenstand, sprechende Figur) aus dem Dokument („texts“).</summary>
    public static Dictionary<string, string> Texts(JsonObject d) => (d["texts"] as JsonObject ?? []).Where(kv => S(kv.Value).Length > 0).ToDictionary(kv => kv.Key, kv => S(kv.Value));

    /// <summary>Fähigkeiten der Spielfigur aus dem Dokument („abilities“).</summary>
    public static List<string> Abilities(JsonObject d) => [.. (d["abilities"] as JsonArray ?? []).Select(S).Where(a => GameStudio.Abilities.Any(x => x.Id == a)).Distinct()];

    public static List<GameStudio.GameRule> Rules(JsonObject d) =>
        [.. (d["rules"] as JsonArray ?? []).OfType<JsonObject>().Select(r => new GameStudio.GameRule(S(r["when"]), int.TryParse(r["n"]?.ToString(), out var n) ? n : 0, S(r["do"]), S(r["title"]) is { Length: > 0 } t ? t : null)).Where(r => r.When.Length > 0 && r.Do.Length > 0)];

    /// <summary>Bauen: Retro/Indie 2D als spielbares Browser-Spiel (mit Level aus dem Projekt).</summary>
    static string[] Lines(JsonNode? n) => (n as JsonArray ?? []).Select(S).Where(r => r.Length > 0).ToArray();

    public static string BuildBrowser(JsonObject d, string parent)
    {
        var t = S(d["browser"]); if (t.Length == 0) throw new InvalidOperationException("Bitte zuerst die Spielart wählen (Hüpfspiel, Mauerbrecher, Schlange oder Plattformer).");
        var level = (d["level"] as JsonArray ?? []).Select(S).Where(r => r.Length > 0).ToArray();
        var dir = Path.Combine(parent, DevStudio.SafeName(S(d["title"])));
        static double? N(JsonNode? n) => n is JsonValue && double.TryParse(n.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : null;
        var props = (d["objects"] as JsonObject ?? []).Where(kv => N(kv.Value) is not null).ToDictionary(kv => kv.Key, kv => N(kv.Value)!.Value);
        var more = (d["levels"] as JsonArray ?? []).OfType<JsonObject>().Where(o => Lines(o["rows"]).Length > 0).ToList();
        // Animationen je Zustand: „anims“: {laufen: {file, frames, fps}} – Bildstreifen aus dem Projekt
        var anims = (d["anims"] as JsonObject ?? []).Where(kv => kv.Value is JsonObject o && S(o["file"]).Length > 0)
            .ToDictionary(kv => kv.Key, kv => (File: S(kv.Value!["file"]), Frames: (int)(N(kv.Value!["frames"]) ?? 1), Fps: (int)(N(kv.Value!["fps"]) ?? 8)));
        var settings = GameStudio.Validate(new GameStudio.GameSettings { Level = level, Props = props, Speed = (int)(N(d["speed"]) ?? 5), Lives = (int)(N(d["lives"]) ?? 3),
            TimeLimit = (int)(N(d["timeLimit"]) ?? 0), Rules = Rules(d), Saves = d["saves"]?.GetValueKind() != System.Text.Json.JsonValueKind.False, Abilities = Abilities(d), Texts = Texts(d), Weapon = Weapon(d), Menu = d["menu"]?.GetValueKind() != System.Text.Json.JsonValueKind.False,
            MoreLevels = [.. more.Select(o => Lines(o["rows"]))], Decor = [Lines(d["decor"]), .. more.Select(o => Lines(o["decor"]))],
            LevelNames = [S(d["levelName"]) is { Length: > 0 } n0 ? n0 : "Level 1", .. more.Select((o, i) => S(o["name"]) is { Length: > 0 } n ? n : $"Level {i + 2}")],
            Anims = anims.ToDictionary(kv => kv.Key, kv => new GameStudio.Anim("figur-" + kv.Key + Path.GetExtension(kv.Value.File), kv.Value.Frames, kv.Value.Fps)) });
        var files = anims.Where(kv => File.Exists(kv.Value.File)).ToDictionary(kv => kv.Key, kv => kv.Value.File);
        var sounds = (d["sounds"] as JsonObject ?? []).Where(kv => S(kv.Value).Length > 0 && File.Exists(S(kv.Value))).ToDictionary(kv => kv.Key, kv => S(kv.Value));
        settings = settings with { Sounds = sounds.ToDictionary(kv => kv.Key, kv => "ton-" + kv.Key + Path.GetExtension(kv.Value)) };
        // Bilder aus der Asset-Liste (Figur, Hintergrund) und der Abspann mit allen Lizenzen
        var player = S(d["playerImage"]) is { Length: > 0 } pi && File.Exists(pi) ? pi : null;
        var back = S(d["backgroundImage"]) is { Length: > 0 } bi && File.Exists(bi) ? bi : null;
        string Finish(string game)
        {
            if (files.Count > 0 || sounds.Count > 0 || player is not null || back is not null) GameStudio.Save(game, settings, player, back, files, sounds);
            var credits = GameAssets.Credits(d);
            if (credits.Length > 0) File.WriteAllText(Path.Combine(game, "CREDITS.txt"), credits);
            return game;
        }
        if (Directory.Exists(dir)) { GameStudio.Save(dir, settings); return Finish(dir); }
        return Finish(GameStudio.CreateBrowserGame(parent, t, S(d["title"]), settings));
    }

    /// <summary>Bauen: 3D/AA/AAA als Unreal-Projekt; Spielbibel liegt im Projekt; AAA+ bekommt Lumen, Nanite-taugliche Einstellungen und World Partition.</summary>
    /// <summary>Spieldaten aus dem Dokument (Level, Regeln, Fähigkeiten, Waffe, Texte, Menü) – für Unreal ohne Bilder und Töne.</summary>
    public static GameStudio.GameSettings SettingsOf(JsonObject d)
    {
        static double? N(JsonNode? n) => n is JsonValue && double.TryParse(n.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : null;
        var level = (d["level"] as JsonArray ?? []).Select(S).Where(r => r.Length > 0).ToArray();
        var more = (d["levels"] as JsonArray ?? []).OfType<JsonObject>().Where(o => Lines(o["rows"]).Length > 0).ToList();
        if (level.Any(r => r.Any(ch => !GameStudio.Tiles.Contains(ch)))) level = [];
        return GameStudio.Validate(new GameStudio.GameSettings
        {
            Level = level, Props = (d["objects"] as JsonObject ?? []).Where(kv => N(kv.Value) is not null).ToDictionary(kv => kv.Key, kv => N(kv.Value)!.Value),
            Speed = (int)(N(d["speed"]) ?? 5), Lives = (int)(N(d["lives"]) ?? 3), TimeLimit = (int)(N(d["timeLimit"]) ?? 0), Rules = Rules(d),
            MoreLevels = [.. more.Select(o => Lines(o["rows"]))],
            LevelNames = [S(d["levelName"]) is { Length: > 0 } n0 ? n0 : "Level 1", .. more.Select((o, i) => S(o["name"]) is { Length: > 0 } n ? n : $"Level {i + 2}")],
            Abilities = Abilities(d), Texts = Texts(d), Weapon = Weapon(d), Menu = d["menu"]?.GetValueKind() != System.Text.Json.JsonValueKind.False,
        });
    }

    public static string BuildUnreal(JsonObject d, string parent, string engineVersion)
    {
        var dir = GameStudio.CreateUnrealProject(parent, S(d["title"]), engineVersion);
        Directory.CreateDirectory(Path.Combine(dir, "Design"));
        File.WriteAllText(Path.Combine(dir, "Design", "Spielbibel.md"), BibleMarkdown(d));
        // Paket 01: spielbares C++-Spiel mit Welt aus Trendo (3D-Level oder Open World)
        var world = UnrealGame.WorldOf(d);
        var level = JsonNode.Parse(UnrealGame.LevelJson(S(d["title"]) is { Length: > 0 } t ? t : "Spiel", SettingsOf(d), world))!.AsObject();
        level["violence"] = S(d["violence"]) is "keine" or "brutal" ? S(d["violence"]) : "normal"; // Gewalt-Darstellung passend zur Altersfreigabe
        level["biome"] = Biome(d); // Landschaft: welche Tiere dort leben (alles muss zusammenpassen)
        UnrealGame.Write(dir, level.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }), world);
        if (S(d["scale"]) is "aa" or "aaa")
            File.AppendAllText(Path.Combine(dir, "Config", "DefaultEngine.ini"),
                "\n[/Script/Engine.RendererSettings]\nr.DynamicGlobalIlluminationMethod=1\nr.ReflectionMethod=1\nr.Shadow.Virtual.Enable=1\nr.AllowStaticLighting=False\nr.GenerateMeshDistanceFields=True\n");
        return dir;
    }
}
