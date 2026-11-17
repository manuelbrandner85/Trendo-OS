using System.Text.Json.Nodes;

namespace Trendo.Core.Programs;

/// <summary>T-0717 (B6 „Programm-Hülle für alle“, Etappe E5): Rückgängig und Wiederholen der Hülle für Programme, die jede Änderung sofort selbst speichern
/// (Aufgaben, Kalender, Vorhaben, Abläufe, Produktion) – statt eines eigenen kleinen Stapels je Programm. Kein zweites System: Der Verlauf liegt in einem
/// <see cref="ProgramLink"/> (100 Schritte, <see cref="ProgramDocument.MaxUndo"/>).
/// Gemerkt werden nur die Elemente, die eine Änderung berührt (Kennung → Stand als JSON, <c>null</c> = gibt es nicht) – nicht die ganze Sammlung. Rückgängig und
/// Wiederholen liefern genau die Elemente, deren Stand sich dabei ändert; was sonst geändert wurde (andere Elemente, Läufe im Hintergrund, ein zweites Fenster), bleibt unberührt.
/// Eine Notsicherung entsteht nicht – alles ist schon gespeichert; das Dokument gilt nach jedem Schritt als gespeichert.</summary>
public sealed class ItemUndo
{
    public ProgramLink Link { get; }
    /// <summary>Stand, den das Programm zuletzt übernommen hat (für den Vergleich bei Rückgängig/Wiederholen).</summary>
    JsonObject _applied = [];

    public ItemUndo(string studio, string format) => Link = new ProgramLink(studio, format, new JsonObject(), format);

    JsonObject Now => Link.State as JsonObject ?? [];
    public bool CanUndo => Link.CanUndo;
    public bool CanRedo => Link.CanRedo;
    public string? UndoLabel => Link.UndoLabel;

    /// <summary>Eine Änderung festhalten: Stand der berührten Elemente vorher und nachher. Gleicher Stand: kein Schritt (false).</summary>
    public bool Record(string label, IEnumerable<KeyValuePair<string, JsonNode?>> before, IEnumerable<KeyValuePair<string, JsonNode?>> after)
    {
        var s = (JsonObject)Now.DeepClone();
        foreach (var (k, v) in before) s[k] = v?.DeepClone();
        Link.Track(s); // Stand vor der Änderung ohne eigenen Schritt (er kann z. B. außerhalb entstanden sein)
        var a = (JsonObject)s.DeepClone();
        foreach (var (k, v) in after) a[k] = v?.DeepClone();
        var changed = Link.Commit(label, a);
        Link.Doc.MarkClean();
        _applied = (JsonObject)Now.DeepClone();
        return changed;
    }

    /// <summary>Wie <see cref="Record(string, IEnumerable{KeyValuePair{string, JsonNode?}}, IEnumerable{KeyValuePair{string, JsonNode?}})"/> für ein Element.</summary>
    public bool Record(string label, string key, JsonNode? before, JsonNode? after) => Record(label, [new(key, before)], [new(key, after)]);

    /// <summary>Elemente, die das Programm übernehmen muss, um von seinem letzten Stand auf <paramref name="state"/> zu kommen (Kennung → Stand, null = löschen).
    /// Danach gilt <paramref name="state"/> als übernommen und das Dokument als gespeichert.</summary>
    public IReadOnlyList<(string Key, JsonNode? Value)> Changes(JsonNode? state)
    {
        var now = state as JsonObject ?? [];
        var l = now.Where(kv => !_applied.TryGetPropertyValue(kv.Key, out var old) || !JsonNode.DeepEquals(old, kv.Value))
            .Select(kv => (kv.Key, kv.Value?.DeepClone())).ToList();
        _applied = (JsonObject)now.DeepClone();
        Link.Doc.MarkClean();
        return l;
    }

    /// <summary>Einen Schritt zurück: die zu übernehmenden Elemente (null: nichts zurückzunehmen).</summary>
    public IReadOnlyList<(string Key, JsonNode? Value)>? Undo() => Link.Undo() is { } st ? Changes(st) : null;
    public IReadOnlyList<(string Key, JsonNode? Value)>? Redo() => Link.Redo() is { } st ? Changes(st) : null;
}
