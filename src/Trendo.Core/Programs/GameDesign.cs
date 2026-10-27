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
            ["level"] = new JsonArray([.. (browser == "jump" ? GameStudio.DefaultJump : browser == "breakout" ? GameStudio.DefaultBreakout : []).Select(r => (JsonNode)r)]),
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
        if (scale.Engine == "browser" && S(d["browser"]).Length == 0) w.Add("Für ein Browser-Spiel bitte eine Spielart wählen (Hüpfspiel, Mauerbrecher oder Schlange) – oder Unreal-Umfang wählen.");
        if (scale.Id == "aaa" && Objs(d, "world").Count() < 3) w.Add("AAA+: Plane mindestens drei Regionen – die Welt trägt das Spiel.");
        if (d["level"] is JsonArray lv && lv.Select(x => S(x)).Any(r => r.Any(ch => ch is not ('#' or '.')))) w.Add("Im Level nur # (Stein/Hindernis) und . (frei).");
        return w;
    }

    /// <summary>Spielbibel als Markdown (für das Team, das Unreal-Projekt oder zum Ausdrucken).</summary>
    public static string BibleMarkdown(JsonObject d)
    {
        var sb = new StringBuilder($"# {S(d["title"])}\n\n**Umfang:** {ScaleOf(S(d["scale"])).Name} · **Genre:** {S(d["genre"])} · **Plattformen:** {S(d["platforms"])}\n\n");
        sb.Append($"## Idee\n\n{S(d["logline"])}\n\n## Kernschleife\n\n{S(d["loop"])}\n\n## Mechaniken\n\n");
        foreach (var m in (d["mechanics"] as JsonArray ?? [])) sb.Append($"- {S(m)}\n");
        sb.Append($"\n## Steuerung\n\n{S(d["controls"])}\n\n## Stil\n\n{S(d["style"])}\n\n## Figuren\n\n");
        foreach (var c in Objs(d, "characters")) sb.Append($"- **{S(c["name"])}** ({S(c["role"])}{(S(c["role"]) == "Boss" ? $", {ProgramDocument.ToNumber(c["phases"]):0} Phasen" : "")}): {S(c["text"])}\n");
        sb.Append("\n## Welt\n\n");
        foreach (var r in Objs(d, "world")) sb.Append($"- **{S(r["name"])}**: {S(r["text"])}\n");
        sb.Append("\n## Missionen\n\n");
        foreach (var q in Objs(d, "quests")) sb.Append($"- **{S(q["title"])}** ({S(q["kind"])}): {S(q["goal"])}{(S(q["needs"]).Length > 0 ? $" · braucht {S(q["needs"])}" : "")}{(S(q["gives"]).Length > 0 ? $" · gibt {S(q["gives"])}" : "")}\n");
        sb.Append($"\n## Systeme\n\n{S(d["systems"])}\n\n## Ton und Musik\n\n{S(d["sound"])}\n\n## Menüs\n\n{S(d["menus"])}\n\n## Meilensteine\n\n");
        foreach (var m in Objs(d, "milestones")) sb.Append($"- [{(m["done"]?.GetValueKind() == System.Text.Json.JsonValueKind.True ? "x" : " ")}] {S(m["name"])}\n");
        return sb.ToString();
    }

    /// <summary>Bauen: Retro/Indie 2D als spielbares Browser-Spiel (mit Level aus dem Projekt).</summary>
    public static string BuildBrowser(JsonObject d, string parent)
    {
        var t = S(d["browser"]); if (t.Length == 0) throw new InvalidOperationException("Bitte zuerst die Spielart wählen (Hüpfspiel, Mauerbrecher oder Schlange).");
        var level = (d["level"] as JsonArray ?? []).Select(S).Where(r => r.Length > 0).ToArray();
        var dir = Path.Combine(parent, DevStudio.SafeName(S(d["title"])));
        var settings = GameStudio.Validate(new GameStudio.GameSettings { Level = level });
        if (Directory.Exists(dir)) { GameStudio.Save(dir, settings); return dir; }
        return GameStudio.CreateBrowserGame(parent, t, S(d["title"]), settings);
    }

    /// <summary>Bauen: 3D/AA/AAA als Unreal-Projekt; Spielbibel liegt im Projekt; AAA+ bekommt Lumen, Nanite-taugliche Einstellungen und World Partition.</summary>
    public static string BuildUnreal(JsonObject d, string parent, string engineVersion)
    {
        var dir = GameStudio.CreateUnrealProject(parent, S(d["title"]), engineVersion);
        Directory.CreateDirectory(Path.Combine(dir, "Design"));
        File.WriteAllText(Path.Combine(dir, "Design", "Spielbibel.md"), BibleMarkdown(d));
        if (S(d["scale"]) is "aa" or "aaa")
            File.AppendAllText(Path.Combine(dir, "Config", "DefaultEngine.ini"),
                "\n[/Script/Engine.RendererSettings]\nr.DynamicGlobalIlluminationMethod=1\nr.ReflectionMethod=1\nr.Shadow.Virtual.Enable=1\nr.AllowStaticLighting=False\nr.GenerateMeshDistanceFields=True\n");
        return dir;
    }
}
