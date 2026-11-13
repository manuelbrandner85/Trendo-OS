using System.Text.Json;
using System.Text.Json.Nodes;

namespace Trendo.Core.Programs;

/// <summary>T-0640 E3/E4: Ablauf bis zur Abnahme. Aus der Spielbibel entstehen Epics und Stories mit Abnahmekriterien auf dem vorhandenen Aufgaben-Board
/// (<see cref="GameBoard"/>); eine Story endet als fertig, fertig mit Hinweisen oder blockiert. Phasentore (<see cref="GamePro"/>) urteilen bestanden, Bedenken,
/// nicht geprüft oder durchgefallen. Die Prüfstufe (schlank, normal, streng) legt fest, welche Prüfungen laufen; schlank entspricht der bisherigen Abnahme.</summary>
public static class GameFlow
{
    public sealed record Rigor(string Id, string Label, string Hint);
    public static readonly Rigor[] Rigors =
    [
        new("schlank", "Schlank", "Standard: Abnahme nach den Phasen-Kriterien wie bisher, keine zusätzlichen Tore"),
        new("normal", "Normal", "Tor an jedem Phasenübergang: Kriterien und Stories der Phase werden beurteilt, Bedenken nur mit festgehaltenem Risiko"),
        new("streng", "Streng", "Alle Pflichtpunkte: jede Story vollständig geprüft, keine Abnahme mit Bedenken, keine Story mit Hinweisen"),
    ];

    public const string Passed = "bestanden", Concerns = "bedenken", NotAssessed = "nicht geprüft", Failed = "durchgefallen";
    public const string Done = "fertig", DoneWithNotes = "hinweise", Blocked = "blockiert";
    public const string Open = "offen", Yes = "ja", No = "nein";

    static string S(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s.Trim() : "";

    public static string RigorOf(JsonObject d) => S(d["rigor"]) is var r && Rigors.Any(x => x.Id == r) ? r : "schlank";

    /// <summary>Prüfstufe wechseln: nur dieses Feld ändert sich, alle Inhalte bleiben.</summary>
    public static void SetRigor(JsonObject d, string id)
    {
        if (Rigors.All(r => r.Id != id)) throw new ArgumentException("Unbekannte Prüfstufe.");
        d["rigor"] = id;
    }

    /// <summary>Welche Prüfungen bei der Abnahme einer Phase laufen.</summary>
    public static IReadOnlyList<string> Checks(string rigor) => rigor switch
    {
        "normal" => ["Phasen-Kriterien", "Stories der Phase beurteilt", "Bedenken nur mit festgehaltenem Risiko"],
        "streng" => ["Phasen-Kriterien", "Stories der Phase beurteilt", "Alle Abnahmekriterien geprüft und erfüllt", "Keine Abnahme mit Bedenken", "Keine Story mit Hinweisen"],
        _ => ["Phasen-Kriterien"],
    };

    // ───────────── Stories ─────────────

    public static bool IsStory(JsonObject t) => t["criteria"] is JsonArray;
    public static string ResultOf(JsonObject t) => S(t["result"]);
    public static string PhaseOf(JsonObject t) => S(t["phase"]);

    /// <summary>Kriterien in derselben Reihenfolge wie im Dokument; ein Text statt eines Eintrags (z. B. von einer KI geschrieben) gilt als offenes Kriterium.</summary>
    public static List<(string Text, string State)> Criteria(JsonObject t) =>
        [.. (t["criteria"] as JsonArray ?? []).Select(c => c switch
        {
            JsonObject o => (S(o["text"]) is { Length: > 0 } x ? x : "(ohne Text)", S(o["state"]) is var s && s is Yes or No ? s : Open),
            _ => (S(c) is { Length: > 0 } x ? x : "(unlesbar)", Open),
        })];

    sealed record Story(string Epic, string Phase, string Title, string[] Criteria);

    static IEnumerable<Story> Plan(JsonObject d)
    {
        yield return new("Kernspiel", "konzept", "Kernspiel beschreiben", ["Idee in einem Satz steht", "Kernschleife nennt, was man immer wieder tut", "Mindestens drei Mechaniken, die zusammenpassen"]);
        foreach (var c in (d["characters"] as JsonArray ?? []).OfType<JsonObject>().Where(c => S(c["name"]).Length > 0))
        {
            var crit = new List<string> { "Rolle und Beschreibung passen zur Spielbibel", "Figur ist im Spiel zu sehen und bewegt sich" };
            if (S(c["role"]) == "Boss") crit.Add("Boss hat 2 bis 4 erkennbare Phasen");
            yield return new("Figuren", "vslice", $"Figur „{S(c["name"])}“ umsetzen", [.. crit]);
        }
        foreach (var r in (d["world"] as JsonArray ?? []).OfType<JsonObject>().Where(r => S(r["name"]).Length > 0))
            yield return new("Welt", "vslice", $"Region „{S(r["name"])}“ bauen", ["Beschreibung ist umgesetzt", "Region ist im Spiel erreichbar"]);
        foreach (var q in (d["quests"] as JsonArray ?? []).OfType<JsonObject>().Where(q => S(q["title"]).Length > 0))
            yield return new("Missionen", "alpha", $"Mission „{S(q["title"])}“ spielbar machen", ["Ziel ist erreichbar", "Belohnung oder Folge tritt ein", "Auftraggeber vergibt die Mission"]);
        var levels = GamePro.Levels(d).Count(l => l.Length > 0);
        for (var i = 1; i <= levels; i++)
            yield return new("Level", "alpha", $"Level {i} fertigstellen", ["Start und Ziel vorhanden", "Ziel ist ohne Glück erreichbar"]);
        yield return new("Ton und Menüs", "beta", "Ton und Menüs fertigstellen", ["Geräusche und Musik sind eingebaut", "Menüs sind vollständig bedienbar"]);
    }

    /// <summary>Epics und Stories aus der Spielbibel aufs Board legen (vorhandene gleichen Namens bleiben unberührt). Liefert die Zahl neuer Stories.</summary>
    public static int Generate(JsonObject d)
    {
        var n = 0;
        foreach (var s in Plan(d))
        {
            // auch fertige Stories gleichen Namens zählen – erneutes Erzeugen legt nichts doppelt an
            if (GameBoard.Tasks(d).Any(t => IsStory(t) && string.Equals(S(t["title"]), s.Title, StringComparison.OrdinalIgnoreCase))) continue;
            if (!GameBoard.Add(d, s.Title, "Story · " + s.Epic)) continue;
            var t = GameBoard.Tasks(d)[^1];
            t["epic"] = s.Epic; t["phase"] = s.Phase;
            t["criteria"] = new JsonArray([.. s.Criteria.Select(c => (JsonNode)new JsonObject { ["text"] = c, ["state"] = Open })]);
            n++;
        }
        return n;
    }

    static JsonObject Task(JsonObject d, int index) => GameBoard.At(d, index) ?? throw new ArgumentException("Diese Aufgabe gibt es nicht.");

    /// <summary>Ein Abnahmekriterium prüfen: erfüllt (true), nicht erfüllt (false) oder wieder offen (null). Ändert sich etwas, ist die Story wieder offen.</summary>
    public static void Check(JsonObject d, int index, int criterion, bool? ok)
    {
        var t = Task(d, index);
        if (t["criteria"] is not JsonArray list || criterion < 0 || criterion >= list.Count) throw new ArgumentException("Dieses Kriterium gibt es nicht.");
        if (list[criterion] is not JsonObject c) { c = new JsonObject { ["text"] = Criteria(t)[criterion].Text }; list[criterion] = c; } // Text in einen Eintrag umwandeln
        c["state"] = ok is null ? Open : ok.Value ? Yes : No;
        if (ResultOf(t).Length > 0) { t.Remove("result"); if (GameBoard.StateOf(t) == "Fertig") t["state"] = "In Arbeit"; }
    }

    /// <summary>Story abschließen. Fertig nur mit allen Kriterien erfüllt; mit Hinweisen nur geprüft und mit Notiz (nicht in „streng“); blockiert immer mit Notiz.</summary>
    public static void Complete(JsonObject d, int index, string result, string note = "")
    {
        var t = Task(d, index);
        if (!IsStory(t)) throw new InvalidOperationException("Nur Stories mit Abnahmekriterien werden so abgeschlossen.");
        var crit = Criteria(t); note = (note ?? "").Trim();
        if (crit.Count == 0 && result != Blocked) throw new InvalidOperationException("Diese Story hat keine Abnahmekriterien – erst Kriterien ergänzen.");
        switch (result)
        {
            case Done:
                if (crit.Any(c => c.State == Open)) throw new InvalidOperationException("Erst alle Abnahmekriterien prüfen.");
                if (crit.Any(c => c.State == No)) throw new InvalidOperationException("Nicht alle Kriterien sind erfüllt – „Mit Hinweisen“ oder „Blockiert“ wählen.");
                t["state"] = "Fertig"; break;
            case DoneWithNotes:
                if (RigorOf(d) == "streng") throw new InvalidOperationException("In der Prüfstufe „Streng“ gibt es kein Fertig mit Hinweisen.");
                if (crit.Any(c => c.State == Open)) throw new InvalidOperationException("Erst alle Abnahmekriterien prüfen.");
                if (note.Length == 0) throw new InvalidOperationException("Bitte die Hinweise kurz festhalten.");
                t["state"] = "Fertig"; break;
            case Blocked:
                if (note.Length == 0) throw new InvalidOperationException("Bitte festhalten, was die Story blockiert.");
                t["state"] = "In Arbeit"; break;
            default: throw new ArgumentException("Unbekanntes Ergebnis.");
        }
        t["result"] = result;
        if (note.Length > 0) t["note"] = note.Length > 300 ? note[..300] : note; else t.Remove("note");
    }

    // ───────────── Phasentore ─────────────

    public sealed record Gate(string Verdict, IReadOnlyList<string> Reasons);

    /// <summary>Urteil über eine Phase nach der Prüfstufe des Projekts.</summary>
    public static Gate Judge(JsonObject d, string phase)
    {
        var rigor = RigorOf(d);
        var reasons = new List<string>();
        var open = GamePro.Criteria(d, phase).Where(c => !c.Ok).Select(c => "Phasen-Kriterium offen: " + c.Text).ToList();
        if (open.Count > 0) return new(Failed, open);
        if (rigor == "schlank") return new(Passed, []);
        var stories = GameBoard.Tasks(d).Where(t => IsStory(t) && PhaseOf(t) == phase).ToList();
        string Title(JsonObject t) => S(t["title"]);
        var blocked = stories.Where(t => ResultOf(t) == Blocked).Select(t => $"Blockiert: {Title(t)}{(S(t["note"]) is { Length: > 0 } n ? " – " + n : "")}").ToList();
        if (blocked.Count > 0) return new(Failed, blocked);
        var unjudged = stories.Where(t => ResultOf(t).Length == 0).Select(t => "Noch nicht beurteilt: " + Title(t)).ToList();
        if (unjudged.Count > 0) return new(NotAssessed, unjudged);
        var notes = stories.Where(t => ResultOf(t) == DoneWithNotes).Select(t => $"Mit Hinweisen: {Title(t)} – {S(t["note"])}").ToList();
        if (rigor == "streng" && notes.Count > 0) return new(Failed, notes);
        return notes.Count > 0 ? new(Concerns, notes) : new(Passed, reasons);
    }

    /// <summary>Festgehaltene Risiko-Annahmen je Phase (Phase → Notiz).</summary>
    public static Dictionary<string, string> Risks(JsonObject d) =>
        (d["risks"] as JsonObject ?? []).Where(kv => S(kv.Value).Length > 0).ToDictionary(kv => kv.Key, kv => S(kv.Value));

    /// <summary>Abnahme zurücknehmen (auch alle späteren Phasen) und deren Risiko-Notizen entfernen.</summary>
    public static void Reopen(JsonObject d, string phase)
    {
        var i = Array.FindIndex(GamePro.Phases, p => p.Id == phase);
        if (i < 0) return;
        GamePro.Reopen(d, phase);
        if (d["risks"] is JsonObject risks) foreach (var p in GamePro.Phases.Skip(i)) risks.Remove(p.Id);
    }

    /// <summary>Phase abnehmen: bestanden rückt vor; Bedenken nur mit festgehaltener Risiko-Annahme (nicht in „streng“); nicht geprüft und durchgefallen nie.</summary>
    public static void Accept(JsonObject d, string phase, string riskNote = "")
    {
        var i = Array.FindIndex(GamePro.Phases, p => p.Id == phase);
        if (i < 0) throw new ArgumentException("Unbekannte Phase.");
        if (i > 0 && !GamePro.Accepted(d, GamePro.Phases[i - 1].Id)) throw new InvalidOperationException($"Erst „{GamePro.Phases[i - 1].Label}“ abnehmen.");
        var g = Judge(d, phase); riskNote = (riskNote ?? "").Trim();
        switch (g.Verdict)
        {
            case Failed: throw new InvalidOperationException("Durchgefallen – " + string.Join(" · ", g.Reasons));
            case NotAssessed: throw new InvalidOperationException("Nicht geprüft – " + string.Join(" · ", g.Reasons));
            case Concerns when riskNote.Length == 0: throw new InvalidOperationException("Bedenken – bitte festhalten, warum das Risiko angenommen wird.");
        }
        GamePro.Accept(d, phase);
        if (g.Verdict != Concerns) (d["risks"] as JsonObject)?.Remove(phase);
        if (g.Verdict == Concerns)
        {
            if (d["risks"] is not JsonObject risks) d["risks"] = risks = [];
            risks[phase] = riskNote.Length > 300 ? riskNote[..300] : riskNote;
        }
    }
}
