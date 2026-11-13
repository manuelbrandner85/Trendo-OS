using System.Text.Json.Nodes;

namespace Trendo.Core.Programs;

/// <summary>S2 (Modernisierung, freigegeben 07.11.2026): Spiele-Projekt-Dashboard – Aufgaben-Board (Offen / In Arbeit / Fertig) im Spielprojekt
/// und Spielvorschau aus dem letzten echten Prüflauf des gebauten Spiels. Aufgaben liegen im Projekt unter „tasks“ ({title, state, source}).</summary>
public static class GameBoard
{
    public static readonly string[] States = ["Offen", "In Arbeit", "Fertig"];

    public static List<JsonObject> Tasks(JsonObject d) => [.. (d["tasks"] as JsonArray ?? []).OfType<JsonObject>()];

    static JsonArray Arr(JsonObject d) { if (d["tasks"] is not JsonArray a) { a = []; d["tasks"] = a; } return a; }
    static string S(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : "";
    public static string StateOf(JsonObject t) => S(t["state"]) is var s && States.Contains(s) ? s : States[0];

    /// <summary>Neue Aufgabe; gleichlautende offene oder laufende Aufgaben werden nicht doppelt angelegt. Liefert false, wenn nichts hinzukam.</summary>
    public static bool Add(JsonObject d, string title, string source = "")
    {
        title = (title ?? "").Trim(); if (title.Length == 0) return false;
        if (Tasks(d).Any(t => string.Equals(S(t["title"]), title, StringComparison.OrdinalIgnoreCase) && StateOf(t) != "Fertig")) return false;
        Arr(d).Add(new JsonObject { ["title"] = title, ["state"] = States[0], ["source"] = source });
        return true;
    }

    /// <summary>Aufgabe eine Spalte weiter (+1) oder zurück (−1); bleibt an den Rändern stehen.</summary>
    public static void Move(JsonObject d, int index, int step)
    {
        var a = Arr(d); if (index < 0 || index >= a.Count || a[index] is not JsonObject t) return;
        var i = Math.Clamp(Array.IndexOf(States, StateOf(t)) + step, 0, States.Length - 1);
        // T-0640: Stories mit Abnahmekriterien werden nur über die Abnahme fertig; wer eine fertige Story zurückschiebt, öffnet sie wieder
        if (GameFlow.IsStory(t) && States[i] == "Fertig" && StateOf(t) != "Fertig" && GameFlow.ResultOf(t) is not (GameFlow.Done or GameFlow.DoneWithNotes))
            throw new InvalidOperationException("Diese Story wird über ihre Abnahmekriterien abgeschlossen.");
        if (GameFlow.IsStory(t) && step < 0 && StateOf(t) == "Fertig") t.Remove("result");
        t["state"] = States[i];
    }

    public static void Remove(JsonObject d, int index) { var a = Arr(d); if (index >= 0 && index < a.Count) a.RemoveAt(index); }

    /// <summary>Hinweise der Spielprüfung und offene Meilensteine als Aufgaben übernehmen (ohne Doppelte). Liefert die Zahl neuer Aufgaben.</summary>
    public static int Seed(JsonObject d)
    {
        var n = 0;
        foreach (var w in GameDesign.Check(d)) if (Add(d, w, "Prüfung")) n++;
        foreach (var m in (d["milestones"] as JsonArray ?? []).OfType<JsonObject>())
            if (m["done"]?.GetValueKind() != System.Text.Json.JsonValueKind.True && Add(d, S(m["name"]), "Meilenstein")) n++;
        return n;
    }

    /// <summary>Fortschritt in Prozent (fertige Aufgaben / alle); ohne Aufgaben 0.</summary>
    public static int Progress(JsonObject d) { var t = Tasks(d); return t.Count == 0 ? 0 : 100 * t.Count(x => StateOf(x) == "Fertig") / t.Count; }

    /// <summary>Spielvorschau: das Bild des letzten echten Prüflaufs (Unreal: Saved/Screenshots, bevorzugt trendo-start.png) – sonst null.</summary>
    public static string? Preview(string built)
    {
        if (string.IsNullOrEmpty(built) || !Directory.Exists(built)) return null;
        var shots = Path.Combine(built, "Saved", "Screenshots");
        if (!Directory.Exists(shots)) return null;
        var start = Path.Combine(shots, "trendo-start.png");
        if (File.Exists(start)) return start;
        return new DirectoryInfo(shots).GetFiles("*.png").OrderByDescending(f => f.LastWriteTimeUtc).FirstOrDefault()?.FullName;
    }
}
