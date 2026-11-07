using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Trendo.Core.Programs;

/// <summary>U3 (Freigabe 07.11.2026): Ablaufplaner für große Spiele-Aufträge. Aus dem Auftrag entsteht ein Plan aus Strategien und Schritten,
/// jeder Schritt mit Fachrolle (<see cref="GameExperts"/>) und Abnahme. Ausgeführt wird über eine übergebene Frage-Funktion
/// (Trendos KI bzw. Claude über den eigenen Login) – kein eigener KI-Kern. Jeder Lauf wird in <c>.trendo-lauf/&lt;id&gt;/</c> protokolliert.
/// Strategie-Idee nach UltraGameStudio (MIT-Lizenz, © 2026 wellingfeng); für Trendo neu umgesetzt.</summary>
public static class GameHarness
{
    public static readonly (string Id, string Label, string What)[] Strategies =
    [
        ("einordnen", "Einordnen und handeln", "Auftrag in Teile sortieren, jedes Teil von der passenden Fachrolle erledigen lassen"),
        ("auffaechern", "Auffächern und zusammenführen", "mehrere Fachleute arbeiten unabhängig, danach wird zu einem Ergebnis zusammengeführt"),
        ("gegenpruefung", "Gegenprüfung", "eine zweite Rolle sucht streng nach Fehlern und Lücken, bevor etwas gilt"),
        ("aussieben", "Erzeugen und aussieben", "mehrere Varianten erzeugen, schwache aussortieren"),
        ("wettbewerb", "Wettbewerb", "Varianten gegeneinander bewerten, die beste gewinnt"),
        ("bis-fertig", "Wiederholen bis fertig", "so lange nachbessern, bis die Abnahme erfüllt ist (höchstens 3 Runden)"),
    ];

    public sealed record Step(int No, string Stage, string ExpertId, string Instruction, string Acceptance);
    public sealed record Plan(string Id, string Request, string Engine, IReadOnlyList<string> Strategies, IReadOnlyList<Step> Steps);

    /// <summary>Strategien aus dem Auftrag: Fehler/Absturz → bis fertig + Gegenprüfung; Prüfen/Sicherheit → auffächern + Gegenprüfung;
    /// Ideen/Design/Namen → aussieben + Wettbewerb; Sortieren/Rangfolge → einordnen + Wettbewerb; sonst auffächern + Gegenprüfung.</summary>
    public static IReadOnlyList<string> InferStrategies(string request)
    {
        var t = (request ?? "").ToLowerInvariant(); var s = new List<string>();
        void Add(params string[] ids) { foreach (var i in ids) if (!s.Contains(i)) s.Add(i); }
        if (Regex.IsMatch(t, "fehler|bug|absturz|stürzt|hängt|kaputt|test|log")) Add("bis-fertig", "gegenpruefung");
        if (Regex.IsMatch(t, "prüf|sicherheit|überprüf|kontroll|review|audit")) Add("auffaechern", "gegenpruefung");
        if (Regex.IsMatch(t, "idee|ideen|entwurf|design|name|namen|varianten|vorschläge")) Add("aussieben", "wettbewerb");
        if (Regex.IsMatch(t, "sortier|rangfolge|einordnen|priorisier|ordne")) Add("einordnen", "wettbewerb");
        if (s.Count == 0) Add("auffaechern", "gegenpruefung");
        return s;
    }

    /// <summary>Baut den Plan: Fachleute über den Rat-Modus, je Strategie die passenden Schritte, am Ende immer eine Abnahme durch die QA-Leitung.</summary>
    public static Plan Build(string request, string? engine = null, string mode = "normal")
    {
        var strategies = InferStrategies(request); var experts = GameExperts.Pick(request, engine, mode);
        var steps = new List<Step>(); int no = 0;
        void S(string stage, string expert, string what, string ok) => steps.Add(new(++no, stage, expert, what, ok));
        var lead = experts[0].Id;
        if (strategies.Contains("einordnen")) S("einordnen", "producer", "Teile den Auftrag in Teilaufgaben und ordne jede einer Fachrolle zu", "jede Teilaufgabe hat genau eine Rolle");
        if (strategies.Contains("aussieben") || strategies.Contains("wettbewerb"))
        {
            foreach (var e in experts.Take(3)) S("varianten", e.Id, "Erzeuge eine eigenständige Variante aus deiner Fachsicht", "Variante ist vollständig und umsetzbar");
            S(strategies.Contains("wettbewerb") ? "wettbewerb" : "aussieben", "creative-director", "Bewerte die Varianten mit Punkten 0–100 und wähle die beste (Begründung)", "eine Variante gewählt, Punkte begründet");
        }
        else foreach (var e in experts) S("arbeit", e.Id, "Bearbeite den Auftrag aus deiner Fachsicht", "konkrete, umsetzbare Antwort");
        if (strategies.Contains("auffaechern") && experts.Count > 1) S("zusammenfuehren", lead, "Führe die Beiträge zu einem widerspruchsfreien Ergebnis zusammen", "keine Widersprüche, alles Wichtige drin");
        if (strategies.Contains("gegenpruefung")) S("gegenpruefung", "qa-tester", "Suche streng nach Fehlern, Lücken und Widersprüchen; antworte als JSON {\"ok\":true|false,\"maengel\":[…]}", "ok = true");
        S("abnahme", "qa-lead", "Entscheide Ja/Nein: ist das Ergebnis fertig? Antworte als JSON {\"ok\":true|false,\"maengel\":[…]}", "ok = true");
        return new(DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..6], request, engine ?? "", strategies, steps);
    }

    /// <summary>Liest ein Prüf-Urteil {"ok":…, "maengel":[…]} aus einer KI-Antwort (auch mitten im Text). Ohne lesbares Urteil: nicht bestanden.</summary>
    public static (bool Ok, List<string> Defects) ParseCheck(string? answer)
    {
        var o = FirstJson(answer); if (o is null) return (false, ["kein lesbares Urteil"]);
        var ok = o["ok"]?.GetValueKind() == JsonValueKind.True;
        var d = (o["maengel"] as JsonArray)?.Select(x => x?.ToString() ?? "").Where(x => x.Length > 0).ToList() ?? [];
        return (ok, d);
    }

    internal static JsonObject? FirstJson(string? text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        for (int i = text.IndexOf('{'); i >= 0; i = text.IndexOf('{', i + 1))
            for (int j = text.LastIndexOf('}'); j > i; j = text.LastIndexOf('}', j - 1))
                try { if (JsonNode.Parse(text[i..(j + 1)]) is JsonObject o) return o; } catch (JsonException) { }
        return null;
    }

    /// <summary>Führt den Plan aus. <paramref name="ask"/> bekommt (Rollen-Anweisung, Frage) und liefert die Antwort.
    /// Bei „bis fertig“ wird nach einer nicht bestandenen Abnahme mit den Mängeln bis zu 3 Runden nachgebessert. Protokoll: ledger.jsonl + ergebnis.md.</summary>
    public static async Task<(bool Ok, string Result, string LogDir)> Run(Plan plan, Func<string, string, CancellationToken, Task<string>> ask, string projectDir, CancellationToken ct = default)
    {
        var dir = Path.Combine(projectDir, ".trendo-lauf", plan.Id); Directory.CreateDirectory(dir);
        var ledger = Path.Combine(dir, "ledger.jsonl");
        void Log(object e) => File.AppendAllText(ledger, JsonSerializer.Serialize(e) + "\n");
        Log(new { ereignis = "plan", plan.Request, plan.Engine, strategien = plan.Strategies, schritte = plan.Steps.Count });
        var rounds = plan.Strategies.Contains("bis-fertig") ? 3 : 1; string result = ""; bool ok = false; var defects = new List<string>();
        for (int round = 1; round <= rounds && !ok; round++)
        {
            var notes = new List<string>();
            foreach (var st in plan.Steps)
            {
                var ex = GameExperts.Find(st.ExpertId) ?? GameExperts.Find("game-designer")!;
                var q = $"Auftrag: {plan.Request}\nSchritt {st.No} ({st.Stage}): {st.Instruction}\nAbnahme: {st.Acceptance}"
                    + (notes.Count > 0 ? "\n\nBisherige Beiträge:\n" + string.Join("\n---\n", notes) : "")
                    + (defects.Count > 0 ? "\n\nMängel aus der letzten Runde, bitte beheben:\n- " + string.Join("\n- ", defects) : "");
                var a = await ask(ex.Prompt, q, ct);
                Log(new { ereignis = "schritt", runde = round, st.No, st.Stage, rolle = ex.Name, antwort = a.Length > 4000 ? a[..4000] : a });
                if (st.Stage is "gegenpruefung" or "abnahme")
                {
                    var (k, d) = ParseCheck(a); defects = d;
                    if (st.Stage == "abnahme") ok = k && (plan.Strategies.Contains("gegenpruefung") ? defects.Count == 0 : true);
                }
                else { notes.Add($"{ex.Name}: {a}"); result = st.Stage is "zusammenfuehren" or "wettbewerb" or "aussieben" ? a : string.Join("\n\n", notes); }
            }
            Log(new { ereignis = "runde", runde = round, bestanden = ok, maengel = defects });
        }
        File.WriteAllText(Path.Combine(dir, "ergebnis.md"), $"# Ergebnis\n\nAuftrag: {plan.Request}\n\nStrategien: {string.Join(", ", plan.Strategies)}\n\nAbnahme: {(ok ? "bestanden" : "nicht bestanden")}\n"
            + (defects.Count > 0 ? "\nOffene Mängel:\n- " + string.Join("\n- ", defects) + "\n" : "") + "\n" + result + "\n");
        return (ok, result, dir);
    }
}

/// <summary>U4: Prüfschleife für erzeugte Grafiken – erzeugen, ansehen, bewerten (0–100 und Mängel), Prompt nachbessern, neu erzeugen,
/// bis bestanden oder das Budget aufgebraucht ist. Ohne Bewerter (kein Bildmodell erreichbar) gilt das erste Bild, ehrlich als „nicht geprüft“ markiert.</summary>
public static class AssetCheck
{
    public sealed record Verdict(bool Pass, int Score, IReadOnlyList<string> Defects, string? PromptPatch);
    public sealed record Attempt(int No, string Prompt, string File, Verdict? Verdict);

    public const string JudgeInstruction = "Bewerte das Bild streng gegen die Beschreibung. Antworte nur als JSON: {\"pass\":true|false,\"score\":0-100,\"maengel\":[\"…\"],\"prompt_zusatz\":\"…\"}. "
        + "Mängel konkret (Anatomie, Proportionen, Licht, Kanten, Wiederholung, Text im Bild, Stilbruch).";

    /// <summary>Liest das Urteil; bestanden nur mit pass=true UND Punktzahl ≥ Schwelle.</summary>
    public static Verdict? Parse(string? answer, int threshold = 75)
    {
        var o = GameHarness.FirstJson(answer); if (o is null) return null;
        var score = o["score"] is JsonValue v && v.TryGetValue(out double d) ? (int)Math.Clamp(Math.Round(d), 0, 100) : 0;
        var defects = (o["maengel"] as JsonArray)?.Select(x => x?.ToString() ?? "").Where(x => x.Length > 0).ToList() ?? [];
        var patch = o["prompt_zusatz"]?.ToString(); if (string.IsNullOrWhiteSpace(patch)) patch = null;
        return new(o["pass"]?.GetValueKind() == JsonValueKind.True && score >= threshold, score, defects, patch);
    }

    /// <summary>Nächster Prompt: Ausgangsbeschreibung plus Zusatz des Bewerters und die Mängel als „vermeiden“.</summary>
    public static string NextPrompt(string basePrompt, Verdict v) =>
        basePrompt + (v.PromptPatch is null ? "" : ", " + v.PromptPatch) + (v.Defects.Count == 0 ? "" : ". Vermeiden: " + string.Join("; ", v.Defects));

    public static async Task<IReadOnlyList<Attempt>> Loop(string prompt, Func<string, int, CancellationToken, Task<string>> generate, Func<string, string, CancellationToken, Task<string>>? judge,
        int budget = 3, int threshold = 75, CancellationToken ct = default)
    {
        var tries = new List<Attempt>(); var p = prompt;
        for (int i = 1; i <= Math.Clamp(budget, 1, 6); i++)
        {
            var file = await generate(p, i, ct);
            if (judge is null) { tries.Add(new(i, p, file, null)); break; }
            var v = Parse(await judge(file, JudgeInstruction + "\nBeschreibung: " + prompt, ct), threshold) ?? new Verdict(false, 0, ["kein lesbares Urteil"], null);
            tries.Add(new(i, p, file, v));
            if (v.Pass) break;
            p = NextPrompt(prompt, v);
        }
        return tries;
    }
}

/// <summary>U5: Sprites, Spritesheets und 2D-Animation. Raster aus Bildzahl, Zellengröße, Bildpositionen; Prompt für die lokale Bild-KI (ComfyUI).</summary>
public static class SpriteSheet
{
    public static readonly (string Id, string Label, int Frames)[] Moves =
        [("stehen", "Stehen (atmen)", 4), ("gehen", "Gehen", 8), ("laufen", "Laufen", 8), ("springen", "Springen", 6), ("angriff", "Angriff", 6), ("treffer", "Getroffen", 4), ("sterben", "Sterben", 6)];

    /// <summary>Raster möglichst quadratisch: Spalten = ⌈√n⌉, Zeilen = ⌈n / Spalten⌉ (oder fest vorgegebene Spalten, z. B. 1 Zeile für Engines, die Streifen erwarten).</summary>
    public static (int Cols, int Rows) Grid(int frames, int? cols = null)
    {
        frames = Math.Clamp(frames, 1, 64); var c = Math.Clamp(cols ?? (int)Math.Ceiling(Math.Sqrt(frames)), 1, frames);
        return (c, (int)Math.Ceiling(frames / (double)c));
    }

    /// <summary>Pixel-Rechteck jedes Bildes in einem Blatt.</summary>
    public static IReadOnlyList<(int X, int Y, int W, int H)> Cells(int frames, int cell, int? cols = null)
    {
        var (c, _) = Grid(frames, cols); return Enumerable.Range(0, Math.Clamp(frames, 1, 64)).Select(i => (i % c * cell, i / c * cell, cell, cell)).ToList();
    }

    /// <summary>Prompt für ein Spritesheet: gleiche Figur in jedem Feld, feste Kamera, neutraler Hintergrund zum Freistellen.</summary>
    public static string Prompt(string figure, string move, int frames, string style = "pixel art")
    {
        var (c, r) = Grid(frames); var m = Moves.FirstOrDefault(x => x.Id == move).Label ?? move;
        return $"sprite sheet, {c} columns x {r} rows, {frames} frames of the same character: {figure}, animation: {m}, consistent proportions and colors in every frame, "
            + $"side view, fixed camera, evenly spaced cells, plain flat background, no text, {style}";
    }
}

/// <summary>U6: Befehle im Spiele-Prompt. „/bild …“ erzeugt sofort; „/bild-start“ … „/bild-ende“ schaltet einen Modus, in dem jede Eingabe dorthin geht.</summary>
public static class GameCommands
{
    public static readonly (string Cmd, string Kind, string Label, string Tool)[] All =
    [
        ("/bild", "bild", "Bild (Konzept, Textur, Symbol, Hintergrund)", "ComfyUI"),
        ("/sprite", "sprite", "2D-Sprite und Spritesheet", "ComfyUI"),
        ("/modell", "modell", "3D-Modell (Requisit, Figur, Kulisse)", "Blender"),
        ("/musik", "musik", "Musik (Hintergrund, Stimmung)", "ACE-Step"),
        ("/sprache", "sprache", "Sprache (Sprecher, Erzählung)", "Piper"),
        ("/video", "video", "Video (Zwischensequenz, Bewegung)", "Video-Studio"),
    ];

    public sealed record Parsed(string Kind, string Text, string? Mode);

    /// <summary>Erkennt einen Befehl. Mode = "start"/"ende" für Modus-Schalter, sonst null. Kein Befehl → null.</summary>
    public static Parsed? Parse(string? input)
    {
        var t = (input ?? "").Trim(); if (!t.StartsWith('/')) return null;
        var head = t.Split(' ', 2)[0].ToLowerInvariant(); var rest = t.Length > head.Length ? t[head.Length..].Trim() : "";
        foreach (var c in All)
        {
            if (head == c.Cmd) return new(c.Kind, rest, null);
            if (head == c.Cmd + "-start") return new(c.Kind, rest, "start");
            if (head == c.Cmd + "-ende") return new(c.Kind, rest, "ende");
        }
        return null;
    }
}
