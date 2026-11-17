using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Trendo.Core.Media;

namespace Trendo.Core.Kreativ;

/// <summary>T-0697 (D4b): Schnitt einer Podcast-Folge – mehrere Sprecherspuren (gleichzeitig ab 0 s), Stellen löschen (Kapitel rücken mit),
/// Pausen kürzen, Intro/Outro-Musik, Lautheit −16 LUFS in zwei Durchgängen (FFmpeg „loudnorm“) und MP3 mit ID3-Tags und Kapiteln.
/// Lesen, Mischen und Schneiden macht Trendo selbst (Audio-Kern von „Audio bearbeiten“); FFmpeg nur für Lautheit und MP3.</summary>
public static class PodcastEdit
{
    public const int Rate = 44100;
    public const double TargetLufs = -16, TargetTp = -1.5, TargetLra = 11;

    public sealed record Track(string Id, string Name, string File, double GainDb, bool Muted);
    public sealed record Cut(double A, double B);
    public sealed record Tags(string Title, string Artist, string Album, string Year, string Comment, string Episode);
    /// <summary>Messwerte des ersten loudnorm-Durchgangs.</summary>
    public sealed record Loudness(double I, double Tp, double Lra, double Thresh, double Offset);

    static string S(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : "";
    static double N(JsonNode? n, double d) => Programs.ProgramDocument.ToNumber(n, d);

    /// <summary>Spuren der Folge. Ältere Dokumente mit nur einer geladenen Aufnahme („audio“) zählen als eine Spur.</summary>
    public static List<Track> Tracks(JsonObject d)
    {
        var list = (d["tracks"] as JsonArray)?.OfType<JsonObject>().Select(o => new Track(S(o["id"]), S(o["name"]), S(o["file"]), N(o["gain"], 0), o["muted"]?.GetValueKind() == JsonValueKind.True)).Where(t => t.File.Length > 0).ToList() ?? [];
        if (list.Count == 0 && S(d["audio"]) is { Length: > 0 } a) list.Add(new("t0", "Aufnahme", a, 0, false));
        return list;
    }

    public static List<Cut> Cuts(JsonObject d) =>
        (d["cuts"] as JsonArray)?.OfType<JsonObject>().Select(o => new Cut(N(o["a"], 0), N(o["b"], 0))).Where(c => c.B > c.A).ToList() ?? [];

    public static string NextSpeaker(JsonObject d) => $"Sprecher {Tracks(d).Count + 1}";

    /// <summary>Spuren als Liste im Dokument (eine alte Einzelaufnahme wird dabei Spur 1).</summary>
    static JsonArray Materialize(JsonObject d)
    {
        if (d["tracks"] is JsonArray arr && arr.Count > 0) return arr;
        arr = new JsonArray([.. Tracks(d).Select(t => (JsonNode)new JsonObject { ["id"] = t.Id, ["name"] = t.Name, ["file"] = t.File, ["gain"] = 0, ["muted"] = false })]);
        d["tracks"] = arr; return arr;
    }

    /// <summary>Spur anhängen.</summary>
    public static string AddTrack(JsonObject d, string name, string file)
    {
        var arr = Materialize(d); var id = "t" + Guid.NewGuid().ToString("N")[..6];
        arr.Add(new JsonObject { ["id"] = id, ["name"] = name, ["file"] = file, ["gain"] = 0, ["muted"] = false });
        return id;
    }

    public static void SetTrack(JsonObject d, string id, Action<JsonObject> change) =>
        change(Materialize(d).OfType<JsonObject>().FirstOrDefault(x => S(x["id"]) == id) ?? throw new InvalidOperationException("Diese Spur gibt es nicht mehr."));

    public static void RemoveTrack(JsonObject d, string id)
    {
        SetTrack(d, id, _ => { });
        var arr = (JsonArray)d["tracks"]!; arr.Remove(arr.OfType<JsonObject>().First(x => S(x["id"]) == id));
        if (arr.Count == 0) d["audio"] = "";
    }

    /// <summary>Stelle a–b (Sekunden in der jetzigen Folge) löschen. Kapitel dahinter rücken um die Länge nach vorn, Kapitel in der Stelle an ihren Anfang.</summary>
    public static void ApplyCut(JsonObject d, double a, double b)
    {
        if (b - a < 0.01) throw new InvalidOperationException("Erst an der Welle eine Stelle markieren (mit der Maus ziehen).");
        if (d["cuts"] is not JsonArray arr) d["cuts"] = arr = [];
        arr.Add(new JsonObject { ["a"] = Math.Round(a, 4), ["b"] = Math.Round(b, 4) });
        d["chapters"] = ShiftChapters(S(d["chapters"]), a, b);
    }

    public static string ShiftChapters(string text, double a, double b)
    {
        var len = b - a; var lines = new List<string>();
        foreach (var raw in (text ?? "").Replace("\r", "").Split('\n'))
        {
            var (ch, _) = PodcastKit.ParseChapters(raw);
            if (ch.Count != 1) { lines.Add(raw); continue; }
            var t = ch[0].Start.TotalSeconds;
            var nt = t >= b ? t - len : t > a ? a : t;
            lines.Add(Clock(TimeSpan.FromSeconds(Math.Round(nt))) + " " + ch[0].Title);
        }
        return string.Join("\n", lines);
    }

    public static string Clock(TimeSpan t) => t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture) : t.ToString(@"mm\:ss", CultureInfo.InvariantCulture);

    /// <summary>Spur in einheitliche Form bringen (44,1 kHz, mono) und Lautstärke anwenden.</summary>
    static AudioBuf Prepare(AudioBuf a, double gainDb)
    {
        var m = AudioEdit.Match(a, Rate, 1);
        if (Math.Abs(gainDb) > 0.01) { var g = (float)Math.Pow(10, gainDb / 20); var c = m.Data[0]; for (var i = 0; i < c.Length; i++) c[i] *= g; }
        return m;
    }

    /// <summary>Alle hörbaren Spuren mischen (beginnen gleichzeitig), dann die Schnitte der Reihe nach anwenden.</summary>
    public static AudioBuf Render(JsonObject d, Func<string, AudioBuf>? read = null, string? onlyTrack = null)
    {
        read ??= AudioFile.Read;
        var tracks = Tracks(d).Where(t => onlyTrack is null ? !t.Muted : t.Id == onlyTrack).ToList();
        var bufs = tracks.Select(t => Prepare(read(t.File), t.GainDb)).ToList();
        var len = bufs.Count == 0 ? 0 : bufs.Max(b => b.Length);
        var mix = new float[len];
        foreach (var b in bufs) { var c = b.Data[0]; for (var i = 0; i < c.Length; i++) mix[i] += c[i]; }
        if (bufs.Count > 1) for (var i = 0; i < mix.Length; i++) mix[i] = Math.Clamp(mix[i], -1f, 1f);
        var a = new AudioBuf(Rate, [mix]);
        foreach (var c in Cuts(d)) AudioEdit.Apply(a, AudioEdit.Op("cut", c.A, c.B));
        return a;
    }

    /// <summary>Pausen finden (leiser als <paramref name="thresholdDb"/>, länger als <paramref name="minSec"/>); von jeder Pause bleiben <paramref name="keepSec"/> stehen.
    /// Ergebnis: zu löschende Stellen, aufsteigend.</summary>
    public static List<Cut> FindPauses(AudioBuf a, double thresholdDb = -40, double minSec = 1.0, double keepSec = 0.5)
    {
        var th = Math.Pow(10, thresholdDb / 20); var win = Math.Max(1, a.Rate / 100); var n = (a.Length + win - 1) / win;
        var quiet = new bool[n];
        for (var k = 0; k < n; k++)
        {
            double sum = 0; var cnt = 0;
            foreach (var c in a.Data) for (var i = k * win; i < Math.Min(a.Length, (k + 1) * win); i++) { sum += c[i] * c[i]; cnt++; }
            quiet[k] = Math.Sqrt(sum / Math.Max(1, cnt)) < th;
        }
        var res = new List<Cut>(); var j = 0;
        while (j < n)
        {
            if (!quiet[j]) { j++; continue; }
            var e = j; while (e < n && quiet[e]) e++;
            double s0 = j * (double)win / a.Rate, s1 = Math.Min(a.Seconds, e * (double)win / a.Rate);
            if (s1 - s0 >= minSec && s1 - s0 > keepSec + 0.05) res.Add(new(Math.Round(s0 + keepSec / 2, 3), Math.Round(s1 - keepSec / 2, 3)));
            j = e;
        }
        return res;
    }

    /// <summary>Pausen kürzen: alle gefundenen Stellen als Schnitte eintragen (von hinten, damit die Zeiten stimmen), Kapitel rücken mit. Liefert Anzahl und gesparte Sekunden.</summary>
    public static (int Count, double Seconds) TrimPauses(JsonObject d, AudioBuf rendered, double thresholdDb = -40, double minSec = 1.0, double keepSec = 0.5)
    {
        var p = FindPauses(rendered, thresholdDb, minSec, keepSec);
        foreach (var c in Enumerable.Reverse(p)) ApplyCut(d, c.A, c.B);
        return (p.Count, p.Sum(c => c.B - c.A));
    }

    /// <summary>Intro vorn und Outro hinten anfügen (Musik mit kurzer Blende). Liefert die fertige Spur und die Länge des Intros.</summary>
    public static (AudioBuf Audio, double IntroSeconds) WithMusic(AudioBuf speech, AudioBuf? intro, AudioBuf? outro)
    {
        static float[] Faded(AudioBuf m)
        {
            var c = AudioEdit.Match(m, Rate, 1).Data[0]; var f = Math.Min(c.Length / 4, Rate / 2);
            for (var i = 0; i < f; i++) { var g = i / (float)f; c[i] *= g; c[c.Length - 1 - i] *= g; }
            return c;
        }
        float[] a = intro is null ? [] : Faded(intro), z = outro is null ? [] : Faded(outro);
        var all = a.Concat(speech.Data[0]).Concat(z).ToArray();
        return (new AudioBuf(Rate, [all]), a.Length / (double)Rate);
    }

    /// <summary>Kapiteldatei (FFMETADATA) für die fertige Folge: Kapitel nach dem Intro rücken um dessen Länge (das erste bei 0 bleibt und umfasst das Intro).</summary>
    public static string Metadata(string title, IReadOnlyList<PodcastKit.Chapter> chapters, TimeSpan total, double introSeconds)
    {
        var shifted = chapters.Select(c => c.Start == TimeSpan.Zero ? c : c with { Start = c.Start + TimeSpan.FromSeconds(introSeconds) }).ToList();
        return PodcastKit.FfMetadata(title, shifted, total);
    }

    public static IReadOnlyList<string> MeasureArgs(string input) =>
        ["-hide_banner", "-nostats", "-i", input, "-af", $"loudnorm=I={Inv(TargetLufs)}:TP={Inv(TargetTp)}:LRA={Inv(TargetLra)}:print_format=json", "-f", "null", "-"];

    static string Inv(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>JSON-Block der loudnorm-Messung aus der FFmpeg-Ausgabe lesen; null, wenn keiner da ist.</summary>
    public static Loudness? ParseMeasure(string stderr)
    {
        var end = stderr.LastIndexOf('}'); if (end < 0) return null;
        var start = stderr.LastIndexOf('{', end); if (start < 0) return null;
        try
        {
            var o = JsonNode.Parse(stderr[start..(end + 1)]) as JsonObject;
            double G(string k) => double.TryParse(S(o?[k]), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : double.NaN;
            var r = new Loudness(G("input_i"), G("input_tp"), G("input_lra"), G("input_thresh"), G("target_offset"));
            return double.IsFinite(r.I) && double.IsFinite(r.Tp) ? r : null;
        }
        catch (JsonException) { return null; }
    }

    /// <summary>Zweiter Durchgang: Lautheit mit den Messwerten linear angleichen, MP3 128 kbit/s mit ID3v2.3-Tags und Kapiteln (CHAP) schreiben.</summary>
    public static IReadOnlyList<string> ExportArgs(string input, string metaFile, string output, Loudness? m, Tags tags)
    {
        var filter = $"loudnorm=I={Inv(TargetLufs)}:TP={Inv(TargetTp)}:LRA={Inv(TargetLra)}";
        if (m is not null && double.IsFinite(m.I) && m.I > -70)
            filter += $":measured_I={Inv(m.I)}:measured_TP={Inv(m.Tp)}:measured_LRA={Inv(double.IsFinite(m.Lra) ? m.Lra : 0)}:measured_thresh={Inv(double.IsFinite(m.Thresh) ? m.Thresh : -70)}:offset={Inv(double.IsFinite(m.Offset) ? m.Offset : 0)}:linear=true";
        var a = new List<string> { "-y", "-hide_banner", "-nostats", "-i", input, "-i", metaFile, "-map", "0:a", "-map_metadata", "1", "-map_chapters", "1", "-af", filter,
            "-ar", Rate.ToString(CultureInfo.InvariantCulture), "-c:a", "libmp3lame", "-b:a", "128k", "-id3v2_version", "3" };
        void Tag(string k, string v) { if (v.Trim().Length > 0) { a.Add("-metadata"); a.Add($"{k}={v.Trim()}"); } }
        Tag("title", tags.Title); Tag("artist", tags.Artist); Tag("album_artist", tags.Artist); Tag("album", tags.Album); Tag("date", tags.Year); Tag("comment", tags.Comment); Tag("track", tags.Episode);
        a.Add("-metadata"); a.Add("genre=Podcast");
        a.Add(output);
        return a;
    }
}
