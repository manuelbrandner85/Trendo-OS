using System.Text.Json;

namespace Trendo.Core;

/// <summary>Wunsch 27.10.2026 (C1): kostenlose KIs von OpenRouter. Trendo lädt die öffentliche Modell-Liste (ohne Schlüssel) und hält sie aktuell.
/// Hier nur das Auslesen und Ablegen – das Laden aus dem Netz macht die App (QA-019: kein Netz im Kern).</summary>
public static class OpenRouterFree
{
    public sealed record Model(string Id, string Name, string Description, IReadOnlyList<string> Input, IReadOnlyList<string> Output, int Context)
    {
        /// <summary>Wofür das Modell taugt – in Alltagswörtern.</summary>
        public IReadOnlyList<string> Areas =>
        [
            .. Output.Contains("text") ? new[] { Text } : [],
            .. Input.Contains("image") ? new[] { Images } : [],
            .. Input.Contains("video") ? new[] { Video } : [],
            .. Input.Contains("audio") ? new[] { Audio } : [],
            .. Output.Contains("audio") ? new[] { Speech } : [],
            .. Output.Contains("image") ? new[] { Draw } : [],
        ];
    }

    public const string Text = "Texte & Chat", Images = "Bilder verstehen", Video = "Videos verstehen", Audio = "Audio verstehen", Speech = "Sprechen", Draw = "Bilder erzeugen";
    public static readonly string[] AllAreas = [Text, Images, Video, Audio, Speech, Draw];
    public const string ModelsUrl = "https://openrouter.ai/api/v1/models";
    public const string SignupUrl = "https://openrouter.ai/sign-up";
    public const string KeysUrl = "https://openrouter.ai/settings/keys";
    /// <summary>Ungefähre Tagesgrenze der kostenlosen Modelle ohne Guthaben (laut OpenRouter, Stand 2026).</summary>
    public const int DailyFreeLimit = 50;

    static bool Zero(JsonElement pricing, string key) =>
        !pricing.TryGetProperty(key, out var v) || (v.ValueKind == JsonValueKind.String ? decimal.TryParse(v.GetString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d) && d == 0 : v.ValueKind == JsonValueKind.Number && v.GetDecimal() == 0);

    /// <summary>Nur wirklich kostenlose Modelle (Eingabe und Ausgabe kosten 0).</summary>
    public static List<Model> Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var list = new List<Model>();
        foreach (var m in doc.RootElement.GetProperty("data").EnumerateArray())
        {
            if (!m.TryGetProperty("pricing", out var p) || !Zero(p, "prompt") || !Zero(p, "completion") || !Zero(p, "request") || !Zero(p, "image")) continue;
            var id = m.GetProperty("id").GetString() ?? "";
            if (id.Length == 0) continue;
            IReadOnlyList<string> Modes(string key) => m.TryGetProperty("architecture", out var a) && a.TryGetProperty(key, out var arr) && arr.ValueKind == JsonValueKind.Array
                ? arr.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => x.Length > 0).ToList() : ["text"];
            var name = m.TryGetProperty("name", out var n) ? n.GetString() ?? id : id;
            var desc = m.TryGetProperty("description", out var de) ? de.GetString() ?? "" : "";
            if (desc.Length > 300) desc = desc[..300].TrimEnd() + " …";
            var ctx = m.TryGetProperty("context_length", out var c) && c.ValueKind == JsonValueKind.Number ? c.GetInt32() : 0;
            list.Add(new(id, name.Replace(" (free)", "", StringComparison.OrdinalIgnoreCase), desc, Modes("input_modalities"), Modes("output_modalities"), ctx));
        }
        return list.OrderByDescending(x => x.Context).ToList();
    }

    sealed record Cache(DateTime Fetched, List<Model> Models);
    static string FileOf(string root) => Path.Combine(root, "ki", "openrouter-kostenlos.json");

    public static void Save(string root, IReadOnlyList<Model> models, DateTime? now = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FileOf(root))!);
        File.WriteAllText(FileOf(root), JsonSerializer.Serialize(new Cache(now ?? DateTime.UtcNow, [.. models])));
    }

    public static (DateTime Fetched, IReadOnlyList<Model> Models) Load(string root)
    {
        try { var c = JsonSerializer.Deserialize<Cache>(File.ReadAllText(FileOf(root))); return c is null ? (default, []) : (c.Fetched, c.Models); }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { return (default, []); }
    }

    /// <summary>Liste älter als einen Tag (oder noch nie geladen)?</summary>
    public static bool Stale(string root, DateTime? now = null) => (now ?? DateTime.UtcNow) - Load(root).Fetched > TimeSpan.FromHours(24);

    /// <summary>Bestes Modell für einen Bereich: das gewählte, sonst das mit dem größten Kontext.</summary>
    public static Model? Best(IReadOnlyList<Model> models, string area, string? chosen) =>
        models.FirstOrDefault(m => m.Id == chosen && m.Areas.Contains(area)) ?? models.FirstOrDefault(m => m.Areas.Contains(area));
}

/// <summary>C4: zählt Anfragen, die heute ins Internet gingen (nur Anzahl, nie Inhalte).</summary>
public sealed class RemoteKiCounter(string dataRoot)
{
    sealed record Day(string Date, int Count);
    string FileOf => Path.Combine(dataRoot, "ki", "internet-heute.json");
    static readonly object Lock = new();

    public int Today(DateTime? now = null)
    {
        lock (Lock)
        {
            try { var d = JsonSerializer.Deserialize<Day>(File.ReadAllText(FileOf)); return d?.Date == Key(now) ? d.Count : 0; }
            catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { return 0; }
        }
    }

    public int Add(DateTime? now = null)
    {
        lock (Lock)
        {
            var n = Today(now) + 1;
            Directory.CreateDirectory(Path.GetDirectoryName(FileOf)!);
            File.WriteAllText(FileOf, JsonSerializer.Serialize(new Day(Key(now), n)));
            return n;
        }
    }

    static string Key(DateTime? now) => (now ?? DateTime.Now).ToString("yyyy-MM-dd");
}
