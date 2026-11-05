using System.Text.Json.Nodes;

namespace Trendo.Core.Media;

/// <summary>Mehrspur-Projekt wie Audition „Multitrack“ (Profi 25, 05.11.2026): mehrere Tondateien auf eigenen Spuren, je Spur Startzeit,
/// Lautstärke (dB), Panorama, Stumm und Solo. Abgemischt wird zu einer Stereo-Summe; die Bearbeitungsschritte des Editors wirken danach auf die Summe.
/// Die Originaldateien bleiben unverändert.</summary>
public static class AudioMulti
{
    public sealed record Track(string Id, string Name, string File, double Start, double GainDb, double Pan, bool Mute, bool Solo);

    public static JsonObject New() => new() { ["source"] = "", ["length"] = 0, ["multi"] = true, ["tracks"] = new JsonArray(), ["ops"] = new JsonArray() };

    public static bool IsMulti(JsonObject doc) => doc["multi"] is JsonValue v && v.TryGetValue<bool>(out var b) && b;

    static double N(JsonNode? n, double f = 0) => n is JsonValue v && v.TryGetValue<double>(out var d) ? d : f;
    static bool B(JsonNode? n) => n is JsonValue v && v.TryGetValue<bool>(out var b) && b;
    static string S(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : "";

    public static List<Track> Tracks(JsonObject doc) =>
        [.. (doc["tracks"] as JsonArray ?? []).OfType<JsonObject>().Select(t => new Track(S(t["id"]), S(t["name"]), S(t["file"]), N(t["start"]), N(t["gain"]), N(t["pan"]), B(t["mute"]), B(t["solo"])))];

    static JsonObject Find(JsonObject doc, string id) => (doc["tracks"] as JsonArray ?? []).OfType<JsonObject>().FirstOrDefault(t => S(t["id"]) == id) ?? throw new InvalidOperationException("Diese Spur gibt es nicht mehr.");

    /// <summary>Neue Spur mit einer Tondatei, beginnend bei <paramref name="start"/> Sekunden (wie in Audition an der Marke).</summary>
    public static Track AddTrack(JsonObject doc, string file, double start = 0, string? name = null)
    {
        if (!File.Exists(file)) throw new FileNotFoundException("Datei nicht gefunden: " + Path.GetFileName(file));
        AudioFile.Read(file); // prüft das Format mit deutscher Meldung
        doc["multi"] = true;
        var arr = doc["tracks"] as JsonArray ?? (JsonArray)(doc["tracks"] = new JsonArray());
        if (arr.Count >= 16) throw new InvalidOperationException("Höchstens 16 Spuren je Projekt.");
        var id = "t" + Guid.NewGuid().ToString("N")[..6];
        arr.Add(new JsonObject { ["id"] = id, ["name"] = name ?? Path.GetFileNameWithoutExtension(file), ["file"] = Path.GetFullPath(file), ["start"] = Math.Round(Math.Max(0, start), 3), ["gain"] = 0.0, ["pan"] = 0.0, ["mute"] = false, ["solo"] = false });
        return Tracks(doc).Last();
    }

    public static void SetTrack(JsonObject doc, string id, double? start = null, double? gainDb = null, double? pan = null, bool? mute = null, bool? solo = null, string? name = null)
    {
        var t = Find(doc, id);
        if (start is { } s) t["start"] = Math.Round(Math.Max(0, s), 3);
        if (gainDb is { } g) t["gain"] = Math.Round(Math.Clamp(g, -60, 12), 1);
        if (pan is { } p) t["pan"] = Math.Round(Math.Clamp(p, -1, 1), 2);
        if (mute is { } m) t["mute"] = m;
        if (solo is { } so) t["solo"] = so;
        if (name is { Length: > 0 } n) t["name"] = n;
    }

    public static void RemoveTrack(JsonObject doc, string id)
    {
        var arr = doc["tracks"] as JsonArray ?? []; var t = Find(doc, id); arr.Remove(t);
    }

    /// <summary>Hörbar ist eine Spur, wenn sie nicht stumm ist und – sobald irgendeine Spur Solo hat – selbst Solo hat.</summary>
    public static bool Audible(IReadOnlyList<Track> all, Track t) => !t.Mute && (!all.Any(x => x.Solo) || t.Solo);

    /// <summary>Abmischen: Stereo 44,1 kHz, Panorama mit gleicher Leistung (−3 dB in der Mitte ausgeglichen), Spitzen über 0 dBFS werden weich begrenzt.</summary>
    public static AudioBuf Mix(JsonObject doc, int rate = 44100)
    {
        var all = Tracks(doc);
        var parts = all.Select(t => (T: t, A: AudioEdit.Match(AudioFile.Read(t.File), rate, 2))).ToList();
        var len = parts.Count == 0 ? 0 : parts.Max(p => (int)Math.Round(p.T.Start * rate) + p.A.Length);
        var outL = new float[len]; var outR = new float[len];
        foreach (var (t, a) in parts)
        {
            if (!Audible(all, t)) continue;
            var g = Math.Pow(10, t.GainDb / 20); var ang = (t.Pan + 1) * Math.PI / 4; // 0 … π/2
            float gl = (float)(g * Math.Cos(ang) * Math.Sqrt(2)), gr = (float)(g * Math.Sin(ang) * Math.Sqrt(2));
            var off = (int)Math.Round(t.Start * rate);
            for (var i = 0; i < a.Length; i++) { outL[off + i] += a.Data[0][i] * gl; outR[off + i] += a.Data[1][i] * gr; }
        }
        static float Soft(float v) => Math.Abs(v) <= 0.95f ? v : Math.Sign(v) * (0.95f + 0.05f * (float)Math.Tanh((Math.Abs(v) - 0.95f) / 0.05f));
        for (var i = 0; i < len; i++) { outL[i] = Soft(outL[i]); outR[i] = Soft(outR[i]); }
        return new AudioBuf(rate, [outL, outR]);
    }
}
