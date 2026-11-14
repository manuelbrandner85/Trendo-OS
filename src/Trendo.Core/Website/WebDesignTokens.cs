using System.Text.Json;
using System.Text.Json.Nodes;

namespace Trendo.Core;

/// <summary>W6: zentrale Gestaltungswerte. Farben (hell und dunkel), Schrift, Rundung und Abstand gelten für das ganze Projekt: die Seite und alle Varianten.
/// Gespeichert in <c>gestaltung.trendo.json</c>. Wer sie auf einer Seite ändert (Editor, KI oder Gestaltungsfeld), ändert sie für alle; beim Laden und im Export setzt Trendo sie ein.</summary>
public static class WebDesignTokens
{
    public const string FileName = "gestaltung.trendo.json";
    public static readonly IReadOnlyList<string> Keys = ["accent", "ink", "paper", "font", "radius", "space", "dunkel", "accentDunkel", "inkDunkel", "paperDunkel"];
    /// <summary>Lesbare Namen der Farbwerte.</summary>
    public static readonly IReadOnlyDictionary<string, string> Names = new Dictionary<string, string>
    {
        ["accent"] = "Akzent", ["ink"] = "Text", ["paper"] = "Hintergrund", ["accentDunkel"] = "Akzent dunkel", ["inkDunkel"] = "Text dunkel", ["paperDunkel"] = "Hintergrund dunkel",
    };
    public static readonly IReadOnlyDictionary<string, string> Defaults = new Dictionary<string, string>
    {
        ["accent"] = "#1d1d1f", ["ink"] = "#1d1d1f", ["paper"] = "#ffffff", ["accentDunkel"] = "#f0c75e", ["inkDunkel"] = "#f2f2f2", ["paperDunkel"] = "#121212",
    };

    static string File(string dir) => Path.Combine(dir, FileName);

    /// <summary>Gestaltungswerte des Projekts oder null, solange es noch keine gibt. Ungültige Werte werden wie beim Speichern bereinigt.</summary>
    public static JsonObject? Load(string dir)
    {
        if (!System.IO.File.Exists(File(dir))) return null;
        try
        {
            if (JsonNode.Parse(System.IO.File.ReadAllText(File(dir))) is not JsonObject o) return null;
            var t = new JsonObject();
            foreach (var k in Keys) if (o[k] is { } v) t[k] = v.DeepClone();
            WebDesign.SanitizeTokens(t);
            return t;
        }
        catch (Exception e) when (e is JsonException or ArgumentException or InvalidOperationException or IOException) { return null; }
    }

    public static void Save(string dir, JsonObject tokens)
    {
        var t = new JsonObject();
        foreach (var k in Keys) if (tokens[k] is { } v) t[k] = v.DeepClone();
        WebDesign.SanitizeTokens(t);
        Directory.CreateDirectory(dir);
        System.IO.File.WriteAllText(File(dir) + ".neu", t.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        System.IO.File.Move(File(dir) + ".neu", File(dir), true);
    }

    /// <summary>Gestaltungswerte des Projekts in Seitendaten einsetzen. Ohne Projektwerte bleiben die Daten unverändert.</summary>
    public static string Inject(string json, JsonObject? tokens)
    {
        if (tokens is null || tokens.Count == 0) return json;
        try
        {
            if (JsonNode.Parse(json) is not JsonObject doc || doc["root"] is not JsonObject root) return json;
            if (root["props"] is not JsonObject props) root["props"] = props = [];
            foreach (var kv in tokens) props[kv.Key] = kv.Value?.DeepClone();
            return doc.ToJsonString();
        }
        catch (Exception e) when (e is JsonException or ArgumentException) { return json; }
    }

    /// <summary>Gestaltungswerte, die in Seitendaten stehen.</summary>
    public static JsonObject Extract(string json)
    {
        var t = new JsonObject();
        try { if (JsonNode.Parse(json)?["root"]?["props"] is JsonObject p) foreach (var k in Keys) if (p[k] is { } v) t[k] = v.DeepClone(); }
        catch (Exception e) when (e is JsonException or ArgumentException) { }
        return t;
    }

    /// <summary>Nach dem Speichern einer Seite oder Variante: geänderte Gestaltungswerte gelten ab jetzt für das ganze Projekt. Liefert true, wenn sich etwas geändert hat.</summary>
    public static bool Lift(string dir, string json)
    {
        var page = Extract(json); if (page.Count == 0) return false;
        var current = Load(dir) ?? new JsonObject();
        var merged = (JsonObject)current.DeepClone();
        foreach (var kv in page) merged[kv.Key] = kv.Value?.DeepClone();
        WebDesign.SanitizeTokens(merged);
        if (JsonNode.DeepEquals(merged, current) && System.IO.File.Exists(File(dir))) return false;
        Save(dir, merged);
        return true;
    }

    static string Color(JsonObject t, string key) => t[key] is JsonValue v && v.TryGetValue(out string? s) && s.Length == 7 ? s : Defaults[key];

    /// <summary>Kontrast prüfen: Text auf Hintergrund und Knopftext (Hintergrundfarbe) auf Akzent, im hellen Satz und, wenn eingeschaltet, im dunklen. Mindestens 4,5 : 1.</summary>
    public static IReadOnlyList<WebsiteFinding> Check(JsonObject t)
    {
        var f = new List<WebsiteFinding>();
        void Pair(string fg, string bg, string what)
        {
            var r = WebsiteColor.Contrast(Color(t, fg), Color(t, bg));
            if (r < 4.5) f.Add(new("Barrierefreiheit", "Fehler", $"{what} ist zu schwach ({r.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)}:1, nötig 4,5:1).", "contrast"));
        }
        Pair("ink", "paper", "Text auf dem Hintergrund");
        Pair("paper", "accent", "Knopftext auf der Akzentfarbe");
        if (t["dunkel"] is JsonValue d && d.TryGetValue(out bool on) && on)
        {
            Pair("inkDunkel", "paperDunkel", "Text auf dem Hintergrund (dunkel)");
            Pair("paperDunkel", "accentDunkel", "Knopftext auf der Akzentfarbe (dunkel)");
        }
        return f;
    }
}
