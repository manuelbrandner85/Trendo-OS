using System.Text.Json.Nodes;

namespace Trendo.Core.Programs;

/// <summary>Spiele bauen wie ein Profi-Studio (Profi-Ausbau 1, 05.11.2026): Rollen im Team, Phasen mit Abnahme (Konzept → Prototyp → Vertical Slice → Alpha → Beta → Release),
/// Spielbarkeits-Prüfung je Level (erreichbares Ziel, knappe Sprünge, Fallen) und Test-Notizen. Vorbild: Produktionsablauf in Spielestudios, Playtest-Protokolle – nur Funktionsideen.</summary>
public static class GamePro
{
    public sealed record Role(string Id, string Label, string Task, string Section);
    public static readonly Role[] Roles =
    [
        new("design", "Game Design", "Idee, Regeln, Zielgruppe", "Spielbibel"),
        new("level", "Level", "Level und Ebenen bauen", "Level"),
        new("grafik", "Grafik & Animation", "Figuren, Bildstreifen, Stil", "Figuren"),
        new("programm", "Programmierung", "Regeln „Wenn … dann“, Spielregeln", "Level"),
        new("ton", "Ton & Musik", "Geräusche und Musik", "Level"),
        new("test", "Test", "Durchspielen, Fehler und Notizen", "Test"),
        new("produktion", "Produktion", "Phasen, Abnahmen, Veröffentlichen", "Produktion"),
    ];

    public sealed record Phase(string Id, string Label, string Goal);
    public static readonly Phase[] Phases =
    [
        new("konzept", "Konzept", "Die Idee steht: ein Satz, Kernschleife, Zielgruppe, Mechaniken."),
        new("prototyp", "Prototyp", "Ein Level ist spielbar und einmal gebaut."),
        new("vslice", "Vertical Slice", "Ein Level in Endqualität: fair, mit Figur-Animation und Ton."),
        new("alpha", "Alpha", "Alle Level spielbar, jedes Ziel erreichbar."),
        new("beta", "Beta", "Durchgetestet: Notizen erfasst, keine offenen Fehler."),
        new("release", "Release", "Gebaut und Veröffentlichungsziele gewählt."),
    ];

    static string S(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : "";
    static string[] Lines(JsonNode? n) => (n as JsonArray ?? []).Select(S).Where(r => r.Length > 0).ToArray();

    /// <summary>Alle Level des Dokuments (Level 1 aus „level“, weitere aus „levels“).</summary>
    public static List<string[]> Levels(JsonObject d) => [Lines(d["level"]), .. (d["levels"] as JsonArray ?? []).OfType<JsonObject>().Select(o => Lines(o["rows"])).Where(l => l.Length > 0)];

    /// <summary>Abnahme-Kriterien einer Phase mit Erfüllt/offen – aus dem Dokument berechnet.</summary>
    public static List<(string Text, bool Ok)> Criteria(JsonObject d, string phase)
    {
        var levels = Levels(d).Where(l => l.Length > 0).ToList();
        var plat = S(d["browser"]) == "plattform";
        bool Playable(string[] l) => !plat ? l.Length > 0 : GameStudio.CheckPlatform(l).All(x => !x.StartsWith("Kein")) && Fairness(l, (int)ProgramDocument.ToNumber(d["speed"] ?? 5)).GoalReachable;
        var notes = (d["testnotes"] as JsonArray ?? []).OfType<JsonObject>().ToList();
        return phase switch
        {
            "konzept" => [("Idee in einem Satz", S(d["logline"]).Length > 5), ("Kernschleife", S(d["loop"]).Length > 3), ("Zielgruppe", S(d["audience"]).Length > 1), ("Mindestens eine Mechanik", (d["mechanics"] as JsonArray)?.Count > 0)],
            "prototyp" => [("Ein Level mit Start und Ziel", levels.Count > 0 && (!plat || GameStudio.CheckPlatform(levels[0]).All(x => !x.StartsWith("Kein")))), ("Einmal gebaut", S(d["built"]).Length > 0)],
            "vslice" => [("Erstes Level fair (Ziel erreichbar)", levels.Count > 0 && Playable(levels[0])), ("Figur mit Animation oder Stil festgelegt", (d["anims"] as JsonObject)?.Count > 0 || S(d["style"]).Length > 3),
                ("Ton: Geräusche oder Musik", (d["sounds"] as JsonObject)?.Count > 0 || S(d["sound"]).Length > 3)],
            "alpha" => [("Mindestens zwei Level", levels.Count >= 2 || !plat), ("Jedes Level spielbar", levels.Count > 0 && levels.All(Playable))],
            "beta" => [("Test-Notizen erfasst", notes.Count > 0), ("Keine offenen Fehler", notes.All(n => S(n["kind"]) != "fehler" || n["done"]?.GetValueKind() == System.Text.Json.JsonValueKind.True))],
            "release" => [("Gebaut", S(d["built"]).Length > 0), ("Veröffentlichungsziele gewählt", GameRelease.TargetsOf(d).Count > 0)],
            _ => [],
        };
    }

    public static bool Accepted(JsonObject d, string phase) => d["phases"]?[phase]?.GetValueKind() == System.Text.Json.JsonValueKind.True;
    /// <summary>Aktuelle Phase: die erste noch nicht abgenommene (nach Release: Release).</summary>
    public static Phase Current(JsonObject d) => Phases.FirstOrDefault(p => !Accepted(d, p.Id)) ?? Phases[^1];

    /// <summary>Phase abnehmen – nur der Reihe nach und nur, wenn alle Kriterien erfüllt sind (wie ein Meilenstein-Tor im Studio).</summary>
    public static void Accept(JsonObject d, string phase)
    {
        var i = Array.FindIndex(Phases, p => p.Id == phase);
        if (i < 0) throw new ArgumentException("Unbekannte Phase.");
        if (i > 0 && !Accepted(d, Phases[i - 1].Id)) throw new InvalidOperationException($"Erst „{Phases[i - 1].Label}“ abnehmen.");
        if (Criteria(d, phase).FirstOrDefault(c => !c.Ok) is { Text: { } open } && open is not null) throw new InvalidOperationException($"Noch offen: {open}.");
        var o = d["phases"] as JsonObject ?? []; o[phase] = true; d["phases"] = o;
    }
    /// <summary>Abnahme zurücknehmen – auch alle späteren Phasen gelten dann wieder als offen.</summary>
    public static void Reopen(JsonObject d, string phase)
    {
        var i = Array.FindIndex(Phases, p => p.Id == phase);
        if (i < 0 || d["phases"] is not JsonObject o) return;
        foreach (var p in Phases.Skip(i)) o.Remove(p.Id);
    }

    // ───────────── Spielbarkeits-Prüfung ─────────────
    public sealed record Finding(int X, int Y, string Text, bool Severe);
    public sealed record Report(bool GoalReachable, IReadOnlyList<Finding> Findings, int Jumps);

    /// <summary>Wie weit die Figur springen kann (in Kacheln), aus denselben Werten wie das Spiel: Sprung 11,5, Schwerkraft 0,6, Tempo 2,2 + 0,35 × Tempo.</summary>
    public static int MaxDx(int dyUp, int speed, double jump = 11.5) => Reach(dyUp, speed, jump) is var r && r < 0 ? -1 : (int)Math.Floor((r + 54) / 32); // physikalisch gerade noch möglich

    /// <summary>Wie weit (Pixel) die Figur in der Luft vorankommt, bis sie auf der Höhe dyUp (Kacheln über dem Absprung, negativ = tiefer) landet; −1 = so hoch kommt sie nicht.</summary>
    public static double Reach(int dyUp, int speed, double jump = 11.5)
    {
        const double g = 0.6, T = 32; var v = 2.2 + Math.Clamp(speed, 1, 10) * 0.35;
        var j = jump - g; // das Spiel zieht die Schwerkraft schon im ersten Bild ab
        var disc = j * j - 2 * g * dyUp * T; if (disc < 0) return -1;
        return (j + Math.Sqrt(disc)) / g * v;
    }
    /// <summary>Benötigter Weg für einen Sprung über dx Kacheln: von der äußersten Kante (Figur 24 breit) bis zur ersten Berührung der Landekachel.</summary>
    static double Need(int dx) => dx * 32 - 54;

    /// <summary>Spielt das Level als Prüfer durch (Laufen, Springen, Fallen, Sprungfedern; Türen öffnen, wenn alle Schlüssel erreichbar sind) und meldet unfaire Stellen.</summary>
    public static Report Fairness(IReadOnlyList<string> level, int speed = 5)
    {
        var R = level.Count; var C = level.Count == 0 ? 0 : level.Max(r => r.Length);
        char At(int x, int y) => y < 0 || x < 0 || x >= C ? '#' : y >= R ? '.' : x < level[y].Length ? level[y][x] : '.';
        var findings = new List<Finding>();
        (int X, int Y)? start = null, goal = null;
        for (var y = 0; y < R; y++) for (var x = 0; x < level[y].Length; x++) { if (level[y][x] == 'S') start ??= (x, y); if (level[y][x] == 'Z') goal ??= (x, y); }
        if (start is null || goal is null) return new(false, [new(start?.X ?? 0, start?.Y ?? 0, start is null ? "Kein Start (S)." : "Kein Ziel (Z).", true)], 0);

        (HashSet<(int, int)> Seen, Dictionary<(int, int), ((int, int) From, int Dx, int Dy)> Par) Search(bool doorsOpen)
        {
            bool Solid(int x, int y) { var ch = At(x, y); return ch == '#' || ch == 'F' || (ch == 'D' && !doorsOpen); }
            bool Free(int x, int y) => !Solid(x, y) && At(x, y) != '^';
            // Wo landet man, wenn man in Spalte x ab Zeile y fällt? (null = Abgrund oder Stacheln)
            (int, int)? Fall(int x, int y)
            {
                for (var yy = y; yy < R + 1; yy++) { if (yy >= R) return null; if (At(x, yy) == '^') return null; if (Solid(x, yy)) return null; if (Solid(x, yy + 1)) return (x, yy); }
                return null;
            }
            // fairster Weg zuerst (wie ein vorsichtiger Spieler): knappe Sprünge kosten viel, bequeme wenig
            var seen = new HashSet<(int, int)>(); var par = new Dictionary<(int, int), ((int, int), int, int)>(); var dist = new Dictionary<(int, int), int>();
            var q = new PriorityQueue<(int X, int Y), int>();
            if (Fall(start.Value.X, start.Value.Y) is { } s0) { dist[s0] = 0; q.Enqueue(s0, 0); }
            while (q.TryDequeue(out var node, out var cost))
            {
                if (cost > dist[node] || !seen.Add(node)) continue; // veralteter Eintrag oder schon besucht
                var (x, y) = node;
                var spring = At(x, y + 1) == 'F';
                var up = spring ? 7 : 3; var jv = spring ? 17.0 : 11.5;
                // Decke über dem Kopf kürzt den Sprung (wie im Spiel: Kopf stößt an, die Figur fällt sofort)
                var room = 0; while (room < 8 && Free(x, y - room - 1)) room++;
                var natural = jv * jv / (2 * 0.6);
                if (room * 32 + 2 < natural) { jv = Math.Sqrt(2 * 0.6 * (room * 32 + 2)); up = Math.Min(up, room); }
                void Try((int, int)? t, int dx, int dy)
                {
                    if (t is not { } tt) return;
                    var tight = Math.Abs(dx) > 2 && Need(Math.Abs(dx)) > Reach(-dy, speed, jv) - 24;
                    var c = cost + 1 + (tight ? 50 : 0);
                    if (c < dist.GetValueOrDefault(tt, int.MaxValue)) { dist[tt] = c; par[tt] = ((x, y), dx, dy); q.Enqueue(tt, c); }
                }
                for (var dir = -1; dir <= 1; dir += 2)
                {
                    if (Free(x + dir, y)) Try(Fall(x + dir, y), dir, 0); // laufen oder hinunterfallen
                    // springen: Bogen in Höhe h (Kopffreiheit), Landung auf gleicher Höhe, höher oder tiefer
                    for (var h = 1; h <= Math.Max(1, up); h++)
                    {
                        if (!Free(x, y - h) && h > 1) break;
                        var reachFlat = MaxDx(0, speed, jv);
                        for (var dx = 1; dx <= Math.Max(reachFlat + 2, 1); dx++)
                        {
                            var tx = x + dir * dx; var ok = true;
                            for (var k = 1; k < dx && ok; k++) ok = Free(x + dir * k, y - h) || Free(x + dir * k, y - h + 1);
                            if (!ok) break;
                            for (var yy = y - h; yy <= y; yy++)
                                if (yy >= 0 && Free(tx, yy) && Solid(tx, yy + 1) && dx <= MaxDx(y - yy, speed, jv)) Try((tx, yy), dir * dx, yy - y);
                            if (Free(tx, y) && !Solid(tx, y + 1) && dx <= MaxDx(-1, speed, jv)) Try(Fall(tx, y), dir * dx, 1); // über eine Lücke springen und tiefer landen
                        }
                    }
                }
            }
            return (seen, par);
        }

        bool Touch(HashSet<(int, int)> seen, int gx, int gy) => seen.Any(p => p.Item1 == gx && Math.Abs(p.Item2 - gy) <= 1);
        // Münzen und Schlüssel streift man auch im Sprung oder beim Herunterfallen: daneben stehen oder bis zu 3 Kacheln darunter
        bool Grab(HashSet<(int, int)> seen, int gx, int gy) => seen.Any(p => Math.Abs(p.Item1 - gx) <= 1 && p.Item2 - gy is >= -1 and <= 3);
        var keys = new List<(int, int)>();
        for (var y = 0; y < R; y++) for (var x = 0; x < level[y].Length; x++) if (level[y][x] == 'K') keys.Add((x, y));
        var (seenA, parA) = Search(false);
        var open = keys.Count > 0 && keys.All(k => Grab(seenA, k.Item1, k.Item2));
        var (seen, par) = open ? Search(true) : (seenA, parA);
        if (keys.Count > 0 && !open) findings.Add(new(keys.First(k => !Grab(seenA, k.Item1, k.Item2)).Item1, keys.First(k => !Grab(seenA, k.Item1, k.Item2)).Item2, "Schlüssel nicht erreichbar – die Türen bleiben zu.", true));
        var reached = seen.FirstOrDefault(p => p.Item1 == goal.Value.X && Math.Abs(p.Item2 - goal.Value.Y) <= 1);
        var goalOk = Touch(seen, goal.Value.X, goal.Value.Y);
        if (!goalOk) findings.Add(new(goal.Value.X, goal.Value.Y, "Ziel nicht erreichbar – Lücke zu weit, Wand zu hoch oder Weg durch Stacheln.", true));
        var jumps = 0;
        if (goalOk) // Weg zum Ziel zurückverfolgen: knappe Sprünge und Landungen neben Stacheln
        {
            var cur = reached;
            while (par.TryGetValue(cur, out var p))
            {
                var (dx, dy) = (p.Dx, p.Dy); var (fx, fy) = p.From;
                if (Math.Abs(dx) > 1 || dy < 0) jumps++;
                var reach = Reach(-dy, speed, At(fx, fy + 1) == 'F' ? 17 : 11.5);
                if (Math.Abs(dx) > 2 && Need(Math.Abs(dx)) > reach - 24) findings.Add(new(cur.Item1, cur.Item2, $"Sehr knapper Sprung ({Math.Abs(dx)} Spalten weit) – nur mit perfektem Timing, kein fairerer Weg.", false));
                if (new[] { (-1, 0), (1, 0), (-1, 1), (1, 1) }.Any(o => At(cur.Item1 + o.Item1, cur.Item2 + o.Item2) == '^')) findings.Add(new(cur.Item1, cur.Item2, "Landung direkt neben Stacheln – kaum Platz für Fehler.", false));
                cur = p.From;
            }
        }
        for (var y = 0; y < R; y++) for (var x = 0; x < level[y].Length; x++)
        {
            if (level[y][x] == 'E' && Math.Abs(y - start.Value.Y) <= 1 && Math.Abs(x - start.Value.X) <= 3) findings.Add(new(x, y, "Gegner direkt am Start – Treffer, bevor man reagieren kann.", false));
            if (level[y][x] == 'o' && !Grab(seen, x, y)) findings.Add(new(x, y, "Münze nicht erreichbar.", false));
        }
        return new(goalOk, findings.DistinctBy(f => (f.X, f.Y, f.Text)).ToList(), jumps);
    }

    /// <summary>Kurzbericht der Prüfung (für Claude und die Liste im Programm).</summary>
    public static string Describe(Report r, int levelNo) =>
        $"Level {levelNo}: " + (r.GoalReachable ? $"Ziel erreichbar ({r.Jumps} Sprünge auf dem Weg)" : "Ziel NICHT erreichbar") +
        (r.Findings.Count == 0 ? ", keine unfairen Stellen." : ". " + string.Join(" ", r.Findings.Select(f => $"[Spalte {f.X + 1}, Zeile {f.Y + 1}] {f.Text}")));

    // ───────────── Test-Notizen ─────────────
    /// <summary>Notiz oder Fehler aus dem Testmodus festhalten (Level, Stelle, Text).</summary>
    public static void AddNote(JsonObject d, int level, int x, int y, string text, bool bug)
    {
        text = text.Trim(); if (text.Length == 0) throw new ArgumentException("Bitte kurz beschreiben, was auffällt.");
        if (text.Length > 300) text = text[..300];
        var a = d["testnotes"] as JsonArray ?? [];
        if (a.Count >= 200) throw new InvalidOperationException("Höchstens 200 Notizen – bitte erledigte löschen.");
        a.Add(new JsonObject { ["level"] = level, ["x"] = x, ["y"] = y, ["text"] = text, ["kind"] = bug ? "fehler" : "notiz", ["done"] = false });
        d["testnotes"] = a;
    }
}
