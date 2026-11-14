using System.Globalization;
using System.Text.Json.Nodes;

namespace Trendo.Core;

/// <summary>Z2: Zeitleiste mit Schlüsselbildern. Wertberechnung wie im Editor und im Export (<c>tools/webdesign-editor/src/bewegung.js</c>):
/// vor dem ersten Schlüsselbild gilt dessen Wert, nach dem letzten der letzte, dazwischen linear; ohne Schlüsselbild der Grundwert.</summary>
public static class WebDesignMotion
{
    /// <summary>Eigenschaften mit Grundwert und erlaubtem Bereich.</summary>
    public static readonly IReadOnlyDictionary<string, (double Default, double Min, double Max)> Properties = new Dictionary<string, (double, double, double)>
    {
        ["x"] = (0, -2000, 2000), ["y"] = (0, -2000, 2000), ["skalierung"] = (1, 0, 5), ["drehung"] = (0, -720, 720), ["deckkraft"] = (1, 0, 1),
    };

    public static double ValueAt(JsonArray? points, string property, double t)
    {
        var p = (points ?? []).OfType<JsonObject>()
            .Where(k => k["eigenschaft"]?.ToString() == property && WebDesign.Number(k["zeit"]) is not null && WebDesign.Number(k["wert"]) is not null)
            .Select(k => (Zeit: WebDesign.Number(k["zeit"])!.Value, Wert: WebDesign.Number(k["wert"])!.Value)).OrderBy(k => k.Zeit).ToList();
        if (p.Count == 0) return Properties[property].Default;
        if (t <= p[0].Zeit) return p[0].Wert;
        for (var i = 1; i < p.Count; i++)
            if (t <= p[i].Zeit) { var (a, b) = (p[i - 1], p[i]); return b.Zeit == a.Zeit ? b.Wert : a.Wert + (b.Wert - a.Wert) * ((t - a.Zeit) / (b.Zeit - a.Zeit)); }
        return p[^1].Wert;
    }

    /// <summary>Prüft eine Zeitleiste: Schlüsselbilder innerhalb der Dauer und im Bereich ihrer Eigenschaft. Null heißt gültig.</summary>
    public static string? CheckTimeline(JsonObject z)
    {
        var dauer = WebDesign.Number(z["dauer"]) ?? 2;
        foreach (var p in (z["punkte"] as JsonArray ?? []).OfType<JsonObject>())
        {
            var prop = p["eigenschaft"]?.ToString() ?? "";
            if (!Properties.TryGetValue(prop, out var r)) return $"Eigenschaft „{prop}“ gibt es nicht.";
            var zeit = WebDesign.Number(p["zeit"]); var wert = WebDesign.Number(p["wert"]);
            if (zeit is null || wert is null) return "Jedes Schlüsselbild braucht zeit und wert.";
            if (zeit > dauer) return $"Schlüsselbild bei {zeit.Value.ToString(CultureInfo.InvariantCulture)} s liegt hinter der Dauer von {dauer.ToString(CultureInfo.InvariantCulture)} s.";
            if (wert < r.Min || wert > r.Max) return $"„{prop}“ muss zwischen {r.Min.ToString(CultureInfo.InvariantCulture)} und {r.Max.ToString(CultureInfo.InvariantCulture)} liegen.";
        }
        return null;
    }
}
