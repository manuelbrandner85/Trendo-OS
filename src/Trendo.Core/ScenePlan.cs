using System.Text.Json;

namespace Trendo.Core;

/// <summary>Liest einen Bauplan, den die lokale KI als JSON geliefert hat. Ohne KI passiert hier nichts.</summary>
public static class ScenePlan
{
    public static string Prompt(string sentence) =>
        "Antworte nur mit JSON, ohne Erklärung und ohne Markdown. " +
        "Baue ein einfaches 3D-Objekt aus höchstens 12 Teilen. Einheiten sind Zentimeter, Z zeigt nach oben. " +
        "x, y und z sind die Mitte des Teils. Die unterste Kante steht auf dem Boden, also z = Höhe / 2. Teile berühren sich. " +
        "Erlaubte Formen: cube, sphere, cylinder, cone, torus, plane. " +
        "Jedes Teil hat name, shape, sx, sy, sz, x, y, z, color. color ist #RRGGBB. " +
        "sx, sy, sz sind Breite, Tiefe und Höhe. Beim Torus ist sx der Außendurchmesser und sy die Ringdicke. " +
        "Setze mehrere Teile zu einem Gegenstand zusammen, zum Beispiel eine Platte und vier Beine. " +
        "Antworte genau so: {\"parts\":[...]}. Der Wunsch: " + sentence;

    public static bool TryRead(string text, out List<Part3D> parts, out string error)
    {
        parts = [];
        error = "";
        if (string.IsNullOrWhiteSpace(text)) { error = "Die KI hat nichts geantwortet."; return false; }
        var s = text.Trim();
        var fence = s.IndexOf("```", StringComparison.Ordinal);
        if (fence >= 0)
        {
            var nl = s.IndexOf('\n', fence);
            var end = s.IndexOf("```", fence + 3, StringComparison.Ordinal);
            if (nl > fence && end > nl) s = s[(nl + 1)..end];
        }
        var a = s.IndexOf('{');
        var b = s.LastIndexOf('}');
        if (a < 0 || b <= a) { error = "Die KI hat keinen Bauplan geliefert. Nenne das Objekt und die Maße in Zentimetern."; return false; }
        try
        {
            using var doc = JsonDocument.Parse(s[a..(b + 1)]);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("parts", out var arr)) root = arr;
            if (root.ValueKind != JsonValueKind.Array) { error = "Die KI hat keine Teile-Liste geliefert."; return false; }
            foreach (var el in root.EnumerateArray())
            {
                if (el.ValueKind != JsonValueKind.Object) continue;
                var shape = Str(el, "shape").Trim().ToLowerInvariant();
                if (!Scene3D.PartShapes.Any(x => x.Id == shape)) continue;
                var sx = Math.Clamp(Math.Abs(Num(el, "sx", 10)), 0.2, 2000);
                var sy = Math.Clamp(Math.Abs(Num(el, "sy", 10)), 0.2, 2000);
                var sz = Math.Clamp(Math.Abs(Num(el, "sz", 10)), 0.2, 2000);
                var name = Str(el, "name").Trim();
                if (name.Length == 0) name = Scene3D.ShapeLabel(shape);
                if (name.Length > 40) name = name[..40];
                var color = Str(el, "color").Trim();
                if (!Scene3D.ValidColor(color)) color = "#2F86F6";
                var z = el.TryGetProperty("z", out var zv) && zv.TryGetDouble(out var zn) ? zn : sz / 2;
                parts.Add(new Part3D
                {
                    Name = name, Shape = shape, Sx = sx, Sy = sy, Sz = sz,
                    X = Num(el, "x", 0), Y = Num(el, "y", 0), Z = z, Color = color,
                });
                if (parts.Count >= 12) break;
            }
        }
        catch (JsonException) { error = "Die KI hat keinen lesbaren Bauplan geliefert."; return false; }
        if (parts.Count == 0) { error = "Die KI hat keine brauchbaren Teile genannt."; return false; }
        return true;
    }

    private static string Str(JsonElement el, string name) =>
        el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    private static double Num(JsonElement el, string name, double fallback) =>
        el.TryGetProperty(name, out var v) && v.TryGetDouble(out var n) ? n : fallback;
}
