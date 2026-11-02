using System.Text.Json.Nodes;

namespace Trendo.Core;

/// <summary>Live mitverfolgen (Wunsch 02.11.2026): ändert Claude ein Studio-Dokument mit vielen neuen Teilen auf einmal (z. B. alle Wände einer 2-Raum-Wohnung),
/// zeigt Trendo sie trotzdem nacheinander – Zwischenstände, in denen jeweils ein neues Element mehr dazukommt. Neue Einträge werden in allen Listen gesucht,
/// auch verschachtelt (z. B. plan → walls, rooms, openings, items). Geänderte Werte und neu geschriebene Listen gelten sofort.</summary>
public static class LiveReplay
{
    /// <summary>Zwischenstände von <paramref name="before"/> zu <paramref name="after"/> (ohne den Endstand). Leer, wenn nichts hinzukam.
    /// Höchstens <paramref name="max"/> Stände – bei mehr neuen Elementen kommen mehrere je Stand dazu.</summary>
    public static IReadOnlyList<JsonObject> Steps(JsonObject before, JsonObject after, int max = 60)
    {
        var start = (JsonObject)after.DeepClone();
        var growth = new List<(JsonArray Target, List<JsonNode?> Extra)>();
        Walk(before, start, growth);
        var total = growth.Sum(g => g.Extra.Count);
        if (total == 0) return [];
        var per = Math.Max(1, (int)Math.Ceiling(total / (double)Math.Max(1, max)));
        var steps = new List<JsonObject>(); var n = 0;
        foreach (var (target, extra) in growth)
            foreach (var item in extra)
            {
                target.Add(item?.DeepClone());
                if (++n % per == 0 && n < total) steps.Add((JsonObject)start.DeepClone());
            }
        return steps;
    }

    /// <summary>Listen im neuen Stand, die den alten Stand nur verlängern, auf den alten Stand kürzen und die neuen Elemente merken.</summary>
    static void Walk(JsonNode? old, JsonNode? now, List<(JsonArray, List<JsonNode?>)> growth)
    {
        switch (now)
        {
            case JsonObject no when old is JsonObject or null: // auch neu angelegte Bereiche (z. B. „plan“ in einem leeren Dokument)
                foreach (var (k, v) in no.ToList()) Walk((old as JsonObject)?[k], v, growth);
                break;
            case JsonArray na when old is null && na.Count > 0: // neu angelegte Liste: alle Einträge nacheinander
                Walk(new JsonArray(), na, growth);
                break;
            case JsonArray na when old is JsonArray oa && na.Count > oa.Count && Prefix(oa, na):
                var extra = new List<JsonNode?>();
                while (na.Count > oa.Count) { var last = na[^1]; na.RemoveAt(na.Count - 1); extra.Insert(0, last); }
                growth.Add((na, extra));
                break;
            case JsonArray na2 when old is JsonArray oa2 && na2.Count == oa2.Count:
                for (var i = 0; i < na2.Count; i++) Walk(oa2[i], na2[i], growth); // gleiche Länge: in den Elementen weitersuchen (z. B. Geschosse mit Wänden)
                break;
        }
    }

    static bool Prefix(JsonArray a, JsonArray b)
    {
        for (var i = 0; i < a.Count; i++) if (!JsonNode.DeepEquals(a[i], b[i])) return false;
        return true;
    }

    public const string Rule = "\nArbeite sichtbar in Schritten: Der Nutzer sieht jede Änderung sofort live im Programm. Speichere darum nach jedem sinnvollen Teilschritt " +
        "(z. B. jede Wand, jeder Raum, jede Figur, jede Szene, jeder Absatz, jede Tabellenzeile) statt alles in einem einzigen großen Schreibvorgang.";
}
