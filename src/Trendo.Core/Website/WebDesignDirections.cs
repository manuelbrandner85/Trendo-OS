using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>A8: Gestaltungsrichtung zuerst. Vor dem Bau schlägt die KI zwei bis drei deutlich verschiedene Richtungen vor (Stimmung, Schrift, Farben, Aufbau).
/// Gebaut wird erst nach der Wahl; die gewählten Farben und die Schrift setzt Trendo danach fest, damit die Seite der Wahl wirklich folgt.</summary>
public static partial class WebDesignDirections
{
    public sealed record Richtung(string Name, string Stimmung, string Schrift, string Akzent, string Text, string Hintergrund, IReadOnlyList<string> Aufbau);

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")] private static partial Regex Hex();

    public static string Instructions() =>
        "Du schlägst Gestaltungsrichtungen für eine Webseite vor, noch ohne sie zu bauen. Antworte ausschließlich mit JSON dieser Form: " +
        "{\"richtungen\":[{\"name\":\"kurzer Name\",\"stimmung\":\"ein Satz zur Wirkung\",\"schrift\":\"klar|serif|rund|mono\",\"akzent\":\"#RRGGBB\",\"text\":\"#RRGGBB\",\"hintergrund\":\"#RRGGBB\",\"aufbau\":[\"Baustein\", ...]}]}. " +
        "Gib zwei oder drei Richtungen, die sich deutlich unterscheiden: andere Schrift oder andere Farben, andere Stimmung, anderer Aufbau. Text auf Hintergrund und Hintergrund auf Akzent brauchen mindestens 4,5 : 1 Kontrast. " +
        "Bausteine für den Aufbau: " + string.Join(", ", WebDesignActions.ComponentNames.Where(n => n != "Komponente")) + ". Alles auf Deutsch.";

    public static string Prompt(string wish) => "Wunsch für die Seite: " + wish.Trim();

    /// <summary>Richtungen aus der KI-Antwort lesen und prüfen. Abgelehnt wird, was nicht 2 bis 3 gültige, deutlich verschiedene Richtungen mit genug Kontrast ergibt.</summary>
    public static (List<Richtung>? List, string? Error) Parse(string? answer)
    {
        if (string.IsNullOrWhiteSpace(answer)) return (null, "Die KI hat keine Richtungen geliefert.");
        var a = answer.IndexOf('{'); var b = answer.LastIndexOf('}');
        if (a < 0 || b <= a) return (null, "Die KI hat keine Richtungen geliefert.");
        JsonArray? arr;
        try { arr = JsonNode.Parse(answer[a..(b + 1)])?["richtungen"] as JsonArray; }
        catch (Exception e) when (e is JsonException or ArgumentException or InvalidOperationException) { return (null, "Die Richtungen der KI sind kein gültiges JSON."); }
        if (arr is null || arr.Count is < 2 or > 3) return (null, "Es braucht zwei oder drei Richtungen.");
        var list = new List<Richtung>();
        foreach (var n in arr)
        {
            if (n is not JsonObject o) return (null, "Eine Richtung ist unvollständig.");
            string? S(string k) => o[k] is JsonValue v && v.TryGetValue(out string? s) && s.Trim().Length > 0 ? s.Trim() : null;
            var (name, mood, font, acc, ink, paper) = (S("name"), S("stimmung"), S("schrift"), S("akzent"), S("text"), S("hintergrund"));
            if (name is null || mood is null || name.Length > 60 || mood.Length > 300) return (null, "Jede Richtung braucht einen Namen und einen Satz zur Stimmung.");
            if (font is null || !WebDesign.Fonts.Contains(font)) return (null, $"Richtung „{name}“: Schrift muss klar, serif, rund oder mono sein.");
            if (acc is null || ink is null || paper is null || !Hex().IsMatch(acc) || !Hex().IsMatch(ink) || !Hex().IsMatch(paper)) return (null, $"Richtung „{name}“: Farben als #RRGGBB.");
            var check = WebDesignTokens.Check(new JsonObject { ["accent"] = acc, ["ink"] = ink, ["paper"] = paper });
            if (check.Count > 0) return (null, $"Richtung „{name}“: {check[0].Text}");
            var layout = (o["aufbau"] as JsonArray ?? []).Select(x => x?.ToString() ?? "").ToList();
            if (layout.Count is 0 or > 12 || layout.Any(x => !WebDesignActions.ComponentNames.Contains(x) || x == "Komponente")) return (null, $"Richtung „{name}“: Aufbau nur aus bekannten Bausteinen (1 bis 12).");
            list.Add(new(name, mood, font, acc.ToLowerInvariant(), ink.ToLowerInvariant(), paper.ToLowerInvariant(), layout));
        }
        for (var i = 0; i < list.Count; i++)
            for (var j = i + 1; j < list.Count; j++)
                if (list[i].Schrift == list[j].Schrift && Similar(list[i].Akzent, list[j].Akzent) && Similar(list[i].Hintergrund, list[j].Hintergrund))
                    return (null, $"„{list[i].Name}“ und „{list[j].Name}“ unterscheiden sich weder in der Schrift noch deutlich in den Farben.");
        return (list, null);
    }

    /// <summary>Zwei Farben gelten als ähnlich, wenn sie sich in keinem Kanal um mehr als 48 von 255 unterscheiden.</summary>
    public static bool Similar(string a, string b)
    {
        static int C(string h, int i) => Convert.ToInt32(h.Substring(1 + i * 2, 2), 16);
        return Enumerable.Range(0, 3).All(i => Math.Abs(C(a, i) - C(b, i)) <= 48);
    }

    /// <summary>Auftrag zum Bau mit gewählter Richtung.</summary>
    public static string BuildPrompt(string wish, Richtung r) =>
        WebDesignKi.NewPagePrompt(wish) + $"\nGewählte Gestaltungsrichtung „{r.Name}“: {r.Stimmung} Schrift {r.Schrift}, Akzent {r.Akzent}, Text {r.Text}, Hintergrund {r.Hintergrund}. Aufbau in dieser Reihenfolge: {string.Join(", ", r.Aufbau)}.";

    /// <summary>Die gewählte Richtung fest einsetzen: Schrift und Farben als Gestaltungswerte, über die geprüften Aktionen.</summary>
    public static WebPlanResult Enforce(WebPlanResult built, Richtung r)
    {
        if (built.Json is null) return built;
        var plan = new JsonObject { ["aktionen"] = new JsonArray(
            new JsonObject { ["aktion"] = "gestaltungswert_setzen", ["name"] = "font", ["wert"] = r.Schrift },
            new JsonObject { ["aktion"] = "gestaltungswert_setzen", ["name"] = "accent", ["wert"] = r.Akzent },
            new JsonObject { ["aktion"] = "gestaltungswert_setzen", ["name"] = "ink", ["wert"] = r.Text },
            new JsonObject { ["aktion"] = "gestaltungswert_setzen", ["name"] = "paper", ["wert"] = r.Hintergrund }) };
        var after = WebDesignActions.Apply(built.Json, plan);
        return after.Json is null ? built : built with { Json = after.Json, Changes = [.. built.Changes, $"Gestaltungsrichtung „{r.Name}“ eingesetzt"] };
    }
}
