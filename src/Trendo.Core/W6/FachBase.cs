using System.Globalization;
using System.Text.Json;

namespace Trendo.Core;

/// <summary>Gemeinsame Helfer der Fach-Studios (Welle 6): Zahlen in deutscher Schreibweise, Speichern pro Studio.</summary>
public static class Fach
{
    public static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>Liest „2,5“, „2.5“ oder „1.234,5“ als Zahl. Wirft eine verständliche Meldung bei Unsinn.</summary>
    public static double Num(string? s, string what = "Zahl")
    {
        var t = (s ?? "").Trim().Replace(" ", "");
        if (t.Contains(',') && t.Contains('.')) t = t.Replace(".", "");
        t = t.Replace(',', '.');
        if (double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && double.IsFinite(v)) return v;
        throw new FormatException($"Bitte bei „{what}“ eine Zahl eingeben.");
    }

    public static string F(double v, int digits = 2) => v.ToString("N" + digits, De);

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals };
    public static string StoreDir(string dataRoot, string studio) { var d = Path.Combine(dataRoot, "fach", studio); Directory.CreateDirectory(d); return d; }

    public static T Load<T>(string dataRoot, string studio, string name) where T : new()
    {
        var p = Path.Combine(StoreDir(dataRoot, studio), name + ".json");
        try { return File.Exists(p) ? JsonSerializer.Deserialize<T>(File.ReadAllText(p), Json) ?? new T() : new T(); } catch (JsonException) { return new T(); }
    }

    public static void Save<T>(string dataRoot, string studio, string name, T value)
    {
        var p = Path.Combine(StoreDir(dataRoot, studio), name + ".json");
        File.WriteAllText(p + ".tmp", JsonSerializer.Serialize(value, Json)); File.Move(p + ".tmp", p, true);
    }
}
