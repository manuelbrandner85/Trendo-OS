using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Trendo.Core;

/// <summary>Ein Feld der verlangten Antwortform: Name und Art („text“, „zahl“, „liste“ = Liste von Texten, „janein“).</summary>
public sealed record AnswerField(string Name, string Kind, string Hint = "");

/// <summary>Alle Studios, Runde 3 – CORE-03 KI-Team: geprüfte, typisierte Antworten (Konzept aus pydantic-ai, eigener Code im AgentCenter).
/// Eine Rolle verlangt eine Antwortform; die Antwort wird als JSON geprüft, bei Fehlern einmal mit den konkreten Fehlern nachgefragt
/// und als lesbarer Text weitergegeben. Kein zweites Agenten-Laufzeitsystem.</summary>
public static class AgentAnswer
{
    public static readonly string[] Kinds = ["text", "zahl", "liste", "janein"];
    public const int MaxListItems = 50, MaxText = 4000;

    /// <summary>Zusatz zum Systemtext: genau ein JSON-Objekt mit diesen Feldern, sonst nichts.</summary>
    public static string Instruction(IReadOnlyList<AnswerField> shape)
    {
        var sb = new StringBuilder(" Antworte NUR mit einem JSON-Objekt, ohne Text davor oder danach, mit genau diesen Feldern: ");
        sb.Append(string.Join(", ", shape.Select(f => $"\"{f.Name}\" ({KindText(f.Kind)}{(f.Hint.Length > 0 ? ", " + f.Hint : "")})")));
        return sb.Append('.').ToString();
    }

    static string KindText(string kind) => kind switch { "zahl" => "Zahl", "liste" => "Liste von kurzen Texten", "janein" => "true oder false", _ => "Text" };

    /// <summary>Das JSON-Objekt aus der Antwort holen – auch wenn die KI es in ```json … ``` gepackt oder einen Satz davor geschrieben hat.</summary>
    public static string? ExtractJson(string answer)
    {
        var s = answer.Trim();
        var fence = s.IndexOf("```", StringComparison.Ordinal);
        if (fence >= 0) { var start = s.IndexOf('\n', fence); var end = start < 0 ? -1 : s.IndexOf("```", start, StringComparison.Ordinal); if (start >= 0 && end > start) s = s[(start + 1)..end].Trim(); }
        var a = s.IndexOf('{'); var b = s.LastIndexOf('}');
        return a >= 0 && b > a ? s[a..(b + 1)] : null;
    }

    /// <summary>Antwort gegen die Form prüfen. Gültig → bereinigtes Objekt (nur die verlangten Felder); sonst die Fehler in verständlichen Sätzen.</summary>
    public static (JsonObject? Data, IReadOnlyList<string> Errors) Check(string answer, IReadOnlyList<AnswerField> shape)
    {
        var json = ExtractJson(answer);
        if (json is null) return (null, ["Die Antwort enthält kein JSON-Objekt."]);
        JsonObject o;
        try { o = JsonNode.Parse(json) as JsonObject ?? throw new JsonException(); }
        catch (JsonException) { return (null, ["Das JSON ist nicht gültig."]); }
        var errors = new List<string>(); var clean = new JsonObject();
        foreach (var f in shape)
        {
            if (!o.TryGetPropertyValue(f.Name, out var v) || v is null) { errors.Add($"Feld „{f.Name}“ fehlt."); continue; }
            switch (f.Kind)
            {
                case "zahl":
                    if (v is JsonValue nv && nv.TryGetValue<double>(out var d) && double.IsFinite(d)) clean[f.Name] = d;
                    else if (v is JsonValue sv && sv.TryGetValue<string>(out var str) && double.TryParse(str.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var d2) && double.IsFinite(d2)) clean[f.Name] = d2;
                    else errors.Add($"Feld „{f.Name}“ muss eine Zahl sein.");
                    break;
                case "janein":
                    if (v is JsonValue bv && bv.TryGetValue<bool>(out var bo)) clean[f.Name] = bo; else errors.Add($"Feld „{f.Name}“ muss true oder false sein.");
                    break;
                case "liste":
                    if (v is not JsonArray arr || arr.Count == 0) { errors.Add($"Feld „{f.Name}“ muss eine nicht leere Liste sein."); break; }
                    var items = arr.Select(x => x is JsonValue xv && xv.TryGetValue<string>(out var t) ? t.Trim() : x?.ToJsonString()).ToList();
                    if (items.Any(x => string.IsNullOrWhiteSpace(x))) { errors.Add($"Feld „{f.Name}“ enthält leere Einträge."); break; }
                    if (items.Count > MaxListItems) { errors.Add($"Feld „{f.Name}“ hat mehr als {MaxListItems} Einträge."); break; }
                    clean[f.Name] = new JsonArray([.. items.Select(x => (JsonNode)JsonValue.Create(Cut(x!))!)]);
                    break;
                default:
                    if (v is JsonValue tv && tv.TryGetValue<string>(out var text) && text.Trim().Length > 0) clean[f.Name] = Cut(text.Trim());
                    else errors.Add($"Feld „{f.Name}“ muss ein nicht leerer Text sein.");
                    break;
            }
        }
        return errors.Count == 0 ? (clean, []) : (null, errors);
    }

    static string Cut(string s) => s.Length > MaxText ? s[..MaxText] + " …" : s;

    /// <summary>Nachfrage nach einer ungültigen Antwort: die konkreten Fehler und die Bitte um genau die verlangte Form.</summary>
    public static string Correction(IReadOnlyList<string> errors, IReadOnlyList<AnswerField> shape) =>
        "Deine Antwort hatte nicht die verlangte Form: " + string.Join(" ", errors) + Instruction(shape);

    /// <summary>Geprüfte Daten als lesbarer Text (für Anzeige und als Eingabe der nächsten Rolle).</summary>
    public static string Render(JsonObject data, IReadOnlyList<AnswerField> shape)
    {
        var sb = new StringBuilder();
        foreach (var f in shape)
        {
            var label = char.ToUpperInvariant(f.Name[0]) + f.Name[1..];
            switch (data[f.Name])
            {
                case JsonArray arr:
                    sb.Append(label).Append(":\n"); var i = 1;
                    foreach (var x in arr) sb.Append(i++).Append(". ").Append(x!.GetValue<string>()).Append('\n');
                    break;
                case JsonValue v when v.TryGetValue<bool>(out var b): sb.Append(label).Append(": ").Append(b ? "ja" : "nein").Append('\n'); break;
                case JsonValue v when v.TryGetValue<double>(out var d): sb.Append(label).Append(": ").Append(d.ToString("0.##", CultureInfo.GetCultureInfo("de-DE"))).Append('\n'); break;
                case JsonValue v: sb.Append(label).Append(": ").Append(v.GetValue<string>()).Append('\n'); break;
            }
        }
        return sb.ToString().TrimEnd();
    }
}
