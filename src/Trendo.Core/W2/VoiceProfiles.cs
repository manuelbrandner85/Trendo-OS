using System.Text.Json;
using System.Text.Json.Serialization;

namespace Trendo.Core;

/// <summary>Trendo Musik S1/S4 (freigegeben 03.11.2026): Stimmprofile – die eigene Stimme (und die anderer Personen, jede mit eigener Einwilligung)
/// mit ihrer Analyse: Tonumfang, bequemer Bereich, Stimmlage, Klangfarbe, Hauchanteil, passende Tonarten. Liegt nur auf diesem PC.</summary>
public sealed record VoiceAnalysis(
    [property: JsonPropertyName("low")] string Low, [property: JsonPropertyName("high")] string High,
    [property: JsonPropertyName("comfy_low")] string ComfyLow, [property: JsonPropertyName("comfy_high")] string ComfyHigh,
    [property: JsonPropertyName("semitones")] int Semitones, [property: JsonPropertyName("voice")] string Voice,
    [property: JsonPropertyName("color")] string Color, [property: JsonPropertyName("breath")] string Breath,
    [property: JsonPropertyName("keys")] List<string> Keys, [property: JsonPropertyName("tags")] string Tags,
    [property: JsonPropertyName("seconds")] double Seconds)
{
    /// <summary>Ergebnis des Analyseskripts („stimme“) – oder null mit Grund.</summary>
    public static (VoiceAnalysis? Result, string Reason) Parse(string json)
    {
        try
        {
            using var d = JsonDocument.Parse(json);
            if (!d.RootElement.TryGetProperty("stimme", out var s) || s.ValueKind != JsonValueKind.Object)
                return (null, d.RootElement.TryGetProperty("grund", out var g) ? g.GetString() ?? "" : "keine Stimme erkannt");
            var a = s.Deserialize<VoiceAnalysis>();
            return a is null || a.Keys is null || a.Voice.Length == 0 ? (null, "keine Stimme erkannt") : (a, "");
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException) { return (null, "Analyse unlesbar"); }
    }

    /// <summary>Tonlage für den Erstellen-Bereich (hoch/mittel/tief) aus der Stimmlage.</summary>
    public string Range => Voice switch { "Bass" or "Alt" => "tief", "Sopran" or "Tenor" => "hoch", _ => "mittel" };
    /// <summary>Männer- oder Frauenstimme (für die Auswahl „Stimme“).</summary>
    public bool Female => Voice is "Alt" or "Mezzosopran" or "Sopran";
    public string KeysDe => string.Join(", ", Keys.Select(k => SongTools.KeyDe(k + " major").Replace("-Dur", "")));
    public string Summary => $"{Voice} · Umfang {Low}–{High} ({Semitones} Halbtöne), bequem {ComfyLow}–{ComfyHigh} · Klang {Color}, {Breath} (Schätzung) · passende Tonarten {KeysDe}";
}

public sealed class VoiceProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..10];
    public string Name { get; set; } = "";
    public VoiceAnalysis? Analysis { get; set; }
    /// <summary>S2/S4: Zeitpunkt und gesprochener Satz der Einwilligung (fürs Stimmmodell); leer = nur Analyse.</summary>
    public DateTimeOffset? ConsentAt { get; set; }
    public string ConsentText { get; set; } = "";
    /// <summary>S2: trainiertes Stimmmodell (leer = keins).</summary>
    public string ModelFile { get; set; } = "";
    public DateTimeOffset Created { get; set; } = DateTimeOffset.Now;
}

public sealed class VoiceProfiles(string dataRoot)
{
    readonly string _file = Path.Combine(dataRoot, "musik", "stimmen.json");
    static readonly JsonSerializerOptions Opt = new() { WriteIndented = true };

    public List<VoiceProfile> All()
    {
        try { return File.Exists(_file) ? JsonSerializer.Deserialize<List<VoiceProfile>>(File.ReadAllText(_file)) ?? [] : []; }
        catch (JsonException) { return []; }
    }

    void Save(List<VoiceProfile> l) { Directory.CreateDirectory(Path.GetDirectoryName(_file)!); var tmp = _file + ".tmp"; File.WriteAllText(tmp, JsonSerializer.Serialize(l, Opt)); File.Move(tmp, _file, true); }

    /// <summary>Profil mit diesem Namen anlegen oder seine Analyse ersetzen.</summary>
    public VoiceProfile Upsert(string name, VoiceAnalysis a)
    {
        var l = All(); var n = name.Trim().Length == 0 ? "Ich" : name.Trim();
        var p = l.FirstOrDefault(x => string.Equals(x.Name, n, StringComparison.OrdinalIgnoreCase));
        if (p is null) { p = new VoiceProfile { Name = n }; l.Add(p); }
        p.Analysis = a; Save(l); return p;
    }

    public void Update(string id, Action<VoiceProfile> change) { var l = All(); if (l.FirstOrDefault(x => x.Id == id) is { } p) { change(p); Save(l); } }

    /// <summary>Profil löschen – samt Stimmmodell und Aufnahmen (Rückgabe: die gelöschten Dateien/Ordner zum Entsorgen).</summary>
    public VoiceProfile? Remove(string id) { var l = All(); var p = l.FirstOrDefault(x => x.Id == id); if (p is not null) { l.Remove(p); Save(l); } return p; }
}
