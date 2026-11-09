using System.Globalization;
using System.Text.Json.Nodes;

namespace Trendo.Core.Media;

/// <summary>Pilot 4.0, V7: Farbräder je Stück für Schatten, Mitten und Lichter. Jedes Rad ist eine Lage im Einheitskreis: Richtung ist der Farbton, Abstand die Stärke.</summary>
public static class VideoGrade
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    public static readonly (string Id, string Label, string Key)[] Wheels = [("lift", "Schatten", "s"), ("gamma", "Mitten", "m"), ("gain", "Lichter", "h")];

    static double Num(JsonNode? n) => n is not null && double.TryParse(n.ToJsonString(), NumberStyles.Float, Inv, out var v) ? v : 0;
    static JsonObject? Node(JsonObject p, string id) => (p["grade"] as JsonObject)?[id] as JsonObject;

    public static (double X, double Y) Get(JsonObject p, string id, string wheel) => Node(p, id)?[wheel] is JsonArray { Count: 2 } a ? (Num(a[0]), Num(a[1])) : (0, 0);

    /// <summary>Rad setzen; Werte außerhalb des Kreises rücken auf den Rand, ein Rad in der Mitte wird nicht gespeichert.</summary>
    public static void Set(JsonObject p, string id, string wheel, double x, double y)
    {
        if (!Wheels.Any(w => w.Id == wheel)) throw new ArgumentException("Unbekanntes Rad: " + wheel);
        var len = Math.Sqrt(x * x + y * y);
        if (len > 1) { x /= len; y /= len; }
        var all = p["grade"] as JsonObject ?? (JsonObject)(p["grade"] = new JsonObject());
        var node = all[id] as JsonObject ?? (JsonObject)(all[id] = new JsonObject());
        if (len < 0.005) node.Remove(wheel); else node[wheel] = new JsonArray(Math.Round(x, 3), Math.Round(y, 3));
        if (node.Count == 0) all.Remove(id);
        if (all.Count == 0) p.Remove("grade");
    }

    public static void Reset(JsonObject p, string id) { if (p["grade"] is JsonObject all) { all.Remove(id); if (all.Count == 0) p.Remove("grade"); } }

    /// <summary>Anteile für Rot, Grün und Blau aus der Lage im Kreis.</summary>
    public static (double R, double G, double B) Rgb(double x, double y)
    {
        double C(double deg) { var a = deg * Math.PI / 180; return (x * Math.Cos(a) + y * Math.Sin(a)) * 0.5; }
        return (C(0), C(120), C(240));
    }

    /// <summary>colorbalance-Filter mit führendem Komma, leer ohne Einstellung.</summary>
    public static string Filter(JsonObject p, string id)
    {
        if (Node(p, id) is null) return "";
        var parts = new List<string>();
        foreach (var (w, _, key) in Wheels)
        {
            var (x, y) = Get(p, id, w);
            if (x == 0 && y == 0) continue;
            var (r, g, b) = Rgb(x, y);
            parts.Add(string.Create(Inv, $"r{key}={r:0.###}:g{key}={g:0.###}:b{key}={b:0.###}"));
        }
        return parts.Count == 0 ? "" : ",colorbalance=" + string.Join(":", parts);
    }
}
