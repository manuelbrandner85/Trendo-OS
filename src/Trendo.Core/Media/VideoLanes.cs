using System.Globalization;
using System.Text.Json.Nodes;

namespace Trendo.Core.Media;

/// <summary>Pilot 4.0, V2: zusätzliche Bild- und Tonspuren. Alles ist Zusatz: Ein altes Projekt hat keinen Eintrag „lanes“ und keine „lane“-Angaben und bleibt beim Speichern und Exportieren unverändert.
/// Bildspur 0 der Einblendungen und Tonspur 0 sind die bisherigen Spuren. Höhere Bildspuren liegen im Bild weiter oben.</summary>
public static class VideoLanes
{
    public const int MaxLanes = 16;

    static JsonObject? Cfg(JsonObject p) => p["lanes"] as JsonObject;
    static JsonObject Ensure(JsonObject p) { if (p["lanes"] is JsonObject o) return o; var n = new JsonObject(); p["lanes"] = n; return n; }
    static int Stored(JsonObject p, string kind) => Cfg(p)?[kind] is JsonValue v && v.TryGetValue<int>(out var n) ? n : 1;

    public static int Video(JsonObject p) => Math.Max(Stored(p, "video"), VideoPro.Overlays(p).Select(o => o.Lane + 1).DefaultIfEmpty(1).Max());
    public static int Audio(JsonObject p) => Math.Max(Stored(p, "audio"), VideoEdit.Clips(p, "music").Select(m => m.Lane + 1).DefaultIfEmpty(1).Max());

    public static int Add(JsonObject p, string kind)
    {
        var n = (kind == "video" ? Video(p) : Audio(p)) + 1;
        if (n > MaxLanes) throw new InvalidOperationException($"Mehr als {MaxLanes} Spuren gehen nicht.");
        Ensure(p)[kind] = n;
        return n;
    }

    /// <summary>Letzte Spur entfernen, wenn sie leer ist. Spuren mit Inhalt werden nie gelöscht.</summary>
    public static bool RemoveLast(JsonObject p, string kind)
    {
        var n = kind == "video" ? Video(p) : Audio(p);
        if (n <= 1) return false;
        var used = kind == "video" ? VideoPro.Overlays(p).Any(o => o.Lane == n - 1) : VideoEdit.Clips(p, "music").Any(m => m.Lane == n - 1);
        if (used) return false;
        Ensure(p)[kind] = n - 1;
        return true;
    }

    static string Key(string kind, int lane) => (kind == "video" ? "v" : "a") + lane.ToString(CultureInfo.InvariantCulture);
    static List<string> Names(JsonObject p, string list) => (Cfg(p)?[list] as JsonArray ?? []).Select(x => x?.ToString() ?? "").ToList();
    static void Toggle(JsonObject p, string list, string key)
    {
        var l = Names(p, list);
        if (!l.Remove(key)) l.Add(key);
        Ensure(p)[list] = new JsonArray([.. l.Select(x => (JsonNode)x)]);
    }

    public static bool IsMuted(JsonObject p, string kind, int lane) => Names(p, "mute").Contains(Key(kind, lane));
    public static bool IsSolo(JsonObject p, string kind, int lane) => Names(p, "solo").Contains(Key(kind, lane));
    public static void ToggleMute(JsonObject p, string kind, int lane) => Toggle(p, "mute", Key(kind, lane));
    public static void ToggleSolo(JsonObject p, string kind, int lane) => Toggle(p, "solo", Key(kind, lane));

    /// <summary>Kommt diese Spur in die Ausgabe? Stumm schaltet aus; ist irgendeine Spur dieser Art auf Solo, zählen nur die Solo-Spuren.</summary>
    public static bool IsOn(JsonObject p, string kind, int lane)
    {
        if (IsMuted(p, kind, lane)) return false;
        var prefix = kind == "video" ? "v" : "a";
        var solos = Names(p, "solo").Where(x => x.StartsWith(prefix, StringComparison.Ordinal)).ToList();
        return solos.Count == 0 || solos.Contains(Key(kind, lane));
    }

    public static void SetMusicLane(JsonObject p, int index, int lane)
    {
        var l = VideoEdit.Clips(p, "music");
        if (index < 0 || index >= l.Count) return;
        l[index] = l[index] with { Lane = Math.Clamp(lane, 0, MaxLanes - 1) };
        VideoEdit.Store(p, l, "music");
    }

    public static void SetOverlayLane(JsonObject p, string id, int lane) => VideoPro.SetLane(p, id, Math.Clamp(lane, 0, MaxLanes - 1));
}

/// <summary>Pilot 4.0, V3 und V4: Bildraster und Schnitt-Werkzeuge wie in Schnittprogrammen.</summary>
public static class VideoTools
{
    public static int Fps(JsonObject p) => Math.Clamp(p["fps"] is JsonValue v && double.TryParse(v.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? (int)f : 30, 1, 240);

    /// <summary>Zeit auf das nächste Einzelbild runden.</summary>
    public static double Quantize(double seconds, int fps) => Math.Round(Math.Round(Math.Max(0, seconds) * fps) / fps, 4);

    /// <summary>Zeitcode Stunden:Minuten:Sekunden:Bilder.</summary>
    public static string TimeCode(double seconds, int fps)
    {
        var frames = (long)Math.Round(Math.Max(0, seconds) * fps);
        var s = frames / fps;
        return string.Create(CultureInfo.InvariantCulture, $"{s / 3600:00}:{s / 60 % 60:00}:{s % 60:00}:{frames % fps:00}");
    }

    /// <summary>Slip: Anderen Ausschnitt der Quelle zeigen, Länge und Lage des Stücks bleiben. Gibt die tatsächliche Verschiebung zurück.</summary>
    public static double Slip(JsonObject p, int index, double delta)
    {
        var l = VideoEdit.Clips(p);
        if (index < 0 || index >= l.Count || l[index].IsImage) return 0;
        var c = l[index];
        var d = Math.Clamp(delta, -c.In, Math.Max(0, c.SourceLength - c.Out));
        if (d == 0) return 0;
        l[index] = c with { In = c.In + d, Out = c.Out + d };
        VideoEdit.Store(p, l);
        return d;
    }

    /// <summary>Rolling: Den Schnitt zwischen Stück i und i+1 verschieben. Das eine wird länger, das andere kürzer, die Gesamtlänge bleibt. Gibt die tatsächliche Verschiebung zurück.</summary>
    public static double Roll(JsonObject p, int index, double delta)
    {
        var l = VideoEdit.Clips(p);
        if (index < 0 || index + 1 >= l.Count) return 0;
        var (a, b) = (l[index], l[index + 1]);
        var maxA = a.IsImage ? double.MaxValue : a.SourceLength - a.Out;
        var d = Math.Clamp(delta, Math.Min(0, -(a.Length - VideoEdit.MinLength)), Math.Max(0, Math.Min(maxA, b.Length - VideoEdit.MinLength)));
        d = Math.Max(d, b.IsImage ? d : -b.In);
        if (d == 0) return 0;
        l[index] = a with { Out = a.Out + d };
        l[index + 1] = b with { In = b.In + d };
        VideoEdit.Store(p, l);
        return d;
    }

    /// <summary>Ripple: Stück einer Tonspur löschen, spätere Stücke derselben Spur rücken um seine Länge auf.</summary>
    public static void RippleDeleteMusic(JsonObject p, int index)
    {
        var l = VideoEdit.Clips(p, "music");
        if (index < 0 || index >= l.Count) return;
        var gone = l[index];
        l.RemoveAt(index);
        for (var i = 0; i < l.Count; i++)
            if (l[i].Lane == gone.Lane && l[i].Start >= gone.Start + gone.Length - 1e-6) l[i] = l[i] with { Start = Math.Round(l[i].Start - gone.Length, 3) };
        VideoEdit.Store(p, l, "music");
    }

    /// <summary>Nächster Einrastpunkt zu <paramref name="t"/> innerhalb der Toleranz, sonst t.</summary>
    public static double Snap(double t, IEnumerable<double> points, double tolerance)
    {
        var best = t; var bestDist = tolerance;
        foreach (var x in points) { var d = Math.Abs(x - t); if (d < bestDist) { bestDist = d; best = x; } }
        return best;
    }
}
