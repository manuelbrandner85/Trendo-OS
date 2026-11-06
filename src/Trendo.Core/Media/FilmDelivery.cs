using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Trendo.Core.Media;

/// <summary>Paket 01 – Studio 3 (Film, Video &amp; VFX), 06.11.2026: Film-Auslieferung wie in der Postproduktion.
/// Aus dem Schnittprojekt entstehen: Filmdatei, Ton-Stems (Dialog/Originalton und Musik, gleich lang wie der Film), Schnittliste als EDL (CMX 3600)
/// und OpenTimelineIO, Musik-Cue-Liste und ein Prüfbericht (fehlende Medien, Schwarzbild, Ton-Bild-Länge, Lautheit, SHA-256 je Datei).
/// Abdeckung: S3-02 (EDL/OTIO), S3-14 (Mix mit Stems), S3-15 (Cue-Liste), S3-18 (Export mit Codec-Info, Log, Hash), S3-20 (QC-Report).</summary>
public static class FilmDelivery
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static double D(JsonNode? n, double f = 0) => n is JsonValue v && double.TryParse(v.GetValueKind() == System.Text.Json.JsonValueKind.String ? v.GetValue<string>() : v.ToJsonString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : f; // auch ganzzahlige Werte (fps = 25)

    /// <summary>Timecode HH:MM:SS:FF bei ganzzahliger Bildrate (kein Drop-Frame).</summary>
    public static string Timecode(double sec, int fps)
    {
        fps = Math.Max(1, fps); var frames = (long)Math.Round(Math.Max(0, sec) * fps, MidpointRounding.AwayFromZero);
        return $"{frames / (3600L * fps):00}:{frames / (60L * fps) % 60:00}:{frames / fps % 60:00}:{frames % fps:00}";
    }

    static int Fps(JsonObject p) => (int)Math.Max(1, D(p["fps"], 30));

    /// <summary>Schnittliste im Format CMX 3600: je Stück ein Ereignis (V = Bild mit Ton, A2 = Musik), Quell- und Programm-Timecode.</summary>
    public static string Edl(JsonObject p, string title)
    {
        var fps = Fps(p); var clips = VideoEdit.Clips(p); var starts = VideoEdit.Starts(clips);
        var sb = new StringBuilder($"TITLE: {Regex.Replace(title, @"[^\p{L}\p{N} _\-]", "").Trim()}\nFCM: NON-DROP FRAME\n\n");
        var ev = 1;
        string Reel(string f) { var r = Regex.Replace(Path.GetFileNameWithoutExtension(f).ToUpperInvariant(), "[^A-Z0-9]", ""); return (r.Length == 0 ? "AX" : r.Length > 8 ? r[..8] : r).PadRight(8); }
        for (var i = 0; i < clips.Count; i++)
        {
            var c = clips[i];
            sb.Append($"{ev++:000}  {Reel(c.File)} {(c.HasAudio && !c.IsImage ? "AA/V " : "V    ")} C        {Timecode(c.In, fps)} {Timecode(c.Out, fps)} {Timecode(starts[i], fps)} {Timecode(starts[i] + c.Length, fps)}\n");
            sb.Append($"* FROM CLIP NAME: {Path.GetFileName(c.File)}\n");
            if (c.Title.Length > 0) sb.Append($"* COMMENT: TITEL {c.Title}\n");
        }
        foreach (var m in VideoEdit.Clips(p, "music"))
        {
            sb.Append($"{ev++:000}  {Reel(m.File)} A2    C        {Timecode(m.In, fps)} {Timecode(m.Out, fps)} {Timecode(m.Start, fps)} {Timecode(m.Start + m.Length, fps)}\n");
            sb.Append($"* FROM CLIP NAME: {Path.GetFileName(m.File)}\n");
        }
        return sb.ToString();
    }

    /// <summary>OpenTimelineIO (JSON, Schema Timeline.1): Spur „Bild“ (mit Lücken-freier Folge), Spur „Musik“ (mit Gap bis zum Einsatz), Medien als ExternalReference.</summary>
    public static string Otio(JsonObject p, string title)
    {
        var fps = Fps(p);
        JsonObject Rt(double sec) => new() { ["OTIO_SCHEMA"] = "RationalTime.1", ["rate"] = fps, ["value"] = Math.Round(sec * fps, MidpointRounding.AwayFromZero) };
        JsonObject Range(double start, double dur) => new() { ["OTIO_SCHEMA"] = "TimeRange.1", ["start_time"] = Rt(start), ["duration"] = Rt(dur) };
        JsonObject Clip(EditClip c) => new()
        {
            ["OTIO_SCHEMA"] = "Clip.2", ["name"] = Path.GetFileName(c.File), ["source_range"] = Range(c.In, c.Length),
            ["media_references"] = new JsonObject { ["DEFAULT_MEDIA"] = new JsonObject { ["OTIO_SCHEMA"] = "ExternalReference.1", ["target_url"] = new Uri(Path.GetFullPath(c.File)).AbsoluteUri } },
            ["active_media_reference_key"] = "DEFAULT_MEDIA", ["metadata"] = new JsonObject { ["trendo"] = new JsonObject { ["id"] = c.Id, ["title"] = c.Title, ["volume"] = c.Volume } },
        };
        var video = new JsonArray(); foreach (var c in VideoEdit.Clips(p)) video.Add(Clip(c));
        var audio = new JsonArray(); var t = 0.0;
        foreach (var m in VideoEdit.Clips(p, "music").OrderBy(m => m.Start))
        {
            if (m.Start > t + 1e-6) audio.Add(new JsonObject { ["OTIO_SCHEMA"] = "Gap.1", ["source_range"] = Range(0, m.Start - t) });
            audio.Add(Clip(m)); t = m.Start + m.Length;
        }
        JsonObject Track(string name, string kind, JsonArray children) => new() { ["OTIO_SCHEMA"] = "Track.1", ["name"] = name, ["kind"] = kind, ["children"] = children };
        var tl = new JsonObject
        {
            ["OTIO_SCHEMA"] = "Timeline.1", ["name"] = title, ["global_start_time"] = Rt(0),
            ["tracks"] = new JsonObject { ["OTIO_SCHEMA"] = "Stack.1", ["name"] = "Spuren", ["children"] = new JsonArray(Track("Bild", "Video", video), Track("Musik", "Audio", audio)) },
        };
        return tl.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>Musik-Cue-Liste (wie für Verwertungsgesellschaften/Filmmusik): Nr, Titel, Ein, Aus, Dauer, Datei, Pegel.</summary>
    public static string CueSheet(JsonObject p)
    {
        var fps = Fps(p); var sb = new StringBuilder("Cue;Titel;Ein (TC);Aus (TC);Dauer (s);Datei;Pegel\n");
        var i = 1;
        foreach (var m in VideoEdit.Clips(p, "music").OrderBy(m => m.Start))
            sb.Append($"M{i++:00};{Path.GetFileNameWithoutExtension(m.File).Replace(';', ',')};{Timecode(m.Start, fps)};{Timecode(m.Start + m.Length, fps)};{m.Length.ToString("0.00", Inv)};{Path.GetFileName(m.File).Replace(';', ',')};{(m.Volume * 100).ToString("0", Inv)} %\n");
        return sb.ToString();
    }

    /// <summary>Projekt für einen Stem: „dialog“ = Originalton der Bildspur ohne Musik, „musik“ = nur Musik (Bildton stumm) – beide gleich lang wie der Film.</summary>
    public static JsonObject StemProject(JsonObject p, string stem)
    {
        var c = (JsonObject)p.DeepClone();
        if (stem == "dialog") c["music"] = new JsonArray();
        else if (stem == "musik") VideoEdit.Store(c, [.. VideoEdit.Clips(c).Select(x => x with { Volume = 0 })]);
        else throw new ArgumentException("Unbekannter Stem: " + stem);
        return c;
    }

    /// <summary>FFmpeg-Befehl für einen Stem als WAV (24 Bit, 48 kHz).</summary>
    public static List<string> StemArgs(JsonObject p, string stem, string output)
    {
        var a = VideoEdit.ExportArgs(StemProject(p, stem), output, new VideoExport.Options(VideoExport.Presets.First(x => x.AudioOnly)));
        var i = a.IndexOf("-c:a"); if (i < 0) throw new InvalidOperationException("Unerwarteter Exportbefehl.");
        a.RemoveRange(i, a.Count - 1 - i); // Codec, Bitrate, faststart weg – Ausgabe bleibt das letzte Argument
        a.InsertRange(a.Count - 1, ["-c:a", "pcm_s24le", "-ar", "48000"]);
        return a;
    }

    /// <summary>Messbefehl für den Prüfbericht: Schwarzbild, Lautheit (EBU R128), Länge.</summary>
    public static List<string> MeasureArgs(string file) => ["-hide_banner", "-nostats", "-i", file, "-vf", "blackdetect=d=0.5:pix_th=0.08", "-af", "ebur128=peak=true", "-f", "null", "-"];

    public sealed record Measure(double Duration, double BlackSeconds, double Lufs, double TruePeak);

    /// <summary>Messwerte aus der FFmpeg-Ausgabe lesen (Zusammenfassung von ebur128 am Ende).</summary>
    public static Measure ParseMeasure(string stderr)
    {
        double R(string pattern) { var m = Regex.Matches(stderr, pattern); return m.Count == 0 ? double.NaN : double.Parse(m[^1].Groups[1].Value, Inv); }
        var dm = Regex.Match(stderr, @"Duration:\s*(\d+):(\d+):(\d+(?:\.\d+)?)");
        var dur = dm.Success ? int.Parse(dm.Groups[1].Value) * 3600 + int.Parse(dm.Groups[2].Value) * 60 + double.Parse(dm.Groups[3].Value, Inv) : double.NaN;
        var black = Regex.Matches(stderr, @"black_duration:\s*([0-9.]+)").Sum(m => double.Parse(m.Groups[1].Value, Inv));
        return new(dur, black, R(@"I:\s*(-?[0-9.]+)\s*LUFS"), R(@"Peak:\s*(-?[0-9.]+)\s*dBFS"));
    }

    public static string Sha256(string file) { using var s = File.OpenRead(file); return Convert.ToHexString(SHA256.HashData(s)).ToLowerInvariant(); }

    public sealed record Check(string Light, string Text);

    /// <summary>Prüfung wie ein Release Director: fehlende Medien, Schwarzbild, Länge Film ↔ Stems, Lautheit (Ziel −23 LUFS Rundfunk bzw. −14 Streaming), Spitzenpegel.</summary>
    public static List<Check> Qc(JsonObject p, Measure film, IReadOnlyDictionary<string, Measure> stems, double targetLufs = -14)
    {
        var r = new List<Check>();
        var missing = VideoEdit.Missing(p);
        r.Add(missing.Count == 0 ? new("gruen", "Alle Quellmedien vorhanden.") : new("rot", "Fehlende Medien: " + string.Join(", ", missing.Select(Path.GetFileName))));
        var expected = VideoEdit.VideoLength(VideoEdit.Clips(p));
        r.Add(double.IsNaN(film.Duration) ? new("rot", "Filmlänge nicht messbar.") : Math.Abs(film.Duration - expected) <= 0.2 ? new("gruen", $"Filmlänge {film.Duration:0.00} s wie im Schnitt.") : new("gelb", $"Filmlänge {film.Duration:0.00} s, Schnitt {expected:0.00} s."));
        r.Add(film.BlackSeconds >= Math.Max(1, film.Duration * 0.5) ? new("rot", $"{film.BlackSeconds:0.0} s Schwarzbild – Quelle prüfen.") : film.BlackSeconds > 0.5 ? new("gelb", $"{film.BlackSeconds:0.0} s Schwarzbild (gewollte Blende?).") : new("gruen", "Kein Schwarzbild."));
        foreach (var (name, m) in stems)
            r.Add(Math.Abs(m.Duration - film.Duration) <= 0.1 ? new("gruen", $"Stem „{name}“ gleich lang wie der Film – synchron.") : new("rot", $"Stem „{name}“ {m.Duration:0.00} s statt {film.Duration:0.00} s – nicht synchron."));
        if (double.IsNaN(film.Lufs)) r.Add(new("gelb", "Lautheit nicht messbar (kein Ton?)."));
        else r.Add(Math.Abs(film.Lufs - targetLufs) <= 2 ? new("gruen", $"Lautheit {film.Lufs:0.0} LUFS (Ziel {targetLufs:0}).") : new("gelb", $"Lautheit {film.Lufs:0.0} LUFS – Ziel {targetLufs:0} LUFS (im Export „Lautheit angleichen“)."));
        if (!double.IsNaN(film.TruePeak)) r.Add(film.TruePeak > -1 ? new("gelb", $"Spitze {film.TruePeak:0.0} dBFS – über −1, kann beim Streamen verzerren.") : new("gruen", $"Spitze {film.TruePeak:0.0} dBFS."));
        return r;
    }

    public sealed record Result(string Folder, string Film, IReadOnlyList<string> Files, IReadOnlyList<Check> Checks, string Report);

    /// <summary>Ganze Auslieferung in einen neuen Ordner: Film (MP4), Stems Dialog und Musik (WAV), EDL, OTIO, Cue-Liste, Messung und Prüfbericht.
    /// <paramref name="run"/> führt FFmpeg aus (Argumente → Ergebnis), damit Tests ohne FFmpeg laufen.</summary>
    public static Result Deliver(Func<IReadOnlyList<string>, Cli.Result> run, JsonObject p, string folder, string title, string ffmpegVersion = "FFmpeg")
    {
        var missing = VideoEdit.Missing(p);
        if (missing.Count > 0) throw new FileNotFoundException("Fehlende Medien – erst neu verknüpfen: " + string.Join(", ", missing.Select(Path.GetFileName))); // nie als schwarzer Clip verschleiern
        Directory.CreateDirectory(folder);
        var safe = Regex.Replace(title, @"[^\p{L}\p{N} _\-]", "").Trim(); if (safe.Length == 0) safe = "Film";
        string F(string n) => Path.Combine(folder, n);
        var film = F(safe + ".mp4");
        var r = run(VideoEdit.ExportArgs(p, film)); if (!r.Ok || !File.Exists(film)) throw new InvalidOperationException("Film-Export ging nicht: " + Last(r.Err));
        var stems = new Dictionary<string, string> { ["Dialog"] = F(safe + " – Stem Dialog.wav") };
        if (VideoEdit.Clips(p, "music").Count > 0) stems["Musik"] = F(safe + " – Stem Musik.wav");
        foreach (var (name, file) in stems)
        {
            var rs = run(StemArgs(p, name == "Dialog" ? "dialog" : "musik", file));
            if (!rs.Ok || !File.Exists(file)) throw new InvalidOperationException($"Stem „{name}“ ging nicht: " + Last(rs.Err));
        }
        File.WriteAllText(F(safe + ".edl"), Edl(p, safe));
        File.WriteAllText(F(safe + ".otio"), Otio(p, safe));
        File.WriteAllText(F(safe + " – Cue-Liste.csv"), CueSheet(p), new UTF8Encoding(true));
        var fm = ParseMeasure(run(MeasureArgs(film)).Err);
        var sm = stems.ToDictionary(x => x.Key, x => ParseMeasure(run(MeasureArgs(x.Value)).Err));
        var checks = Qc(p, fm, sm);
        var files = new List<string> { film }; files.AddRange(stems.Values); files.AddRange([F(safe + ".edl"), F(safe + ".otio"), F(safe + " – Cue-Liste.csv")]);
        var report = F("Prüfbericht.md");
        File.WriteAllText(report, Report(title, checks, files, ffmpegVersion));
        return new Result(folder, film, files, checks, report);
        static string Last(string e) => e.Split('\n', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "";
    }

    /// <summary>Prüfbericht als Markdown mit Dateien, Größe und SHA-256.</summary>
    public static string Report(string title, IEnumerable<Check> checks, IEnumerable<string> files, string ffmpegVersion)
    {
        var sb = new StringBuilder($"# Auslieferung „{title}“\n\nErstellt: {DateTime.Now:dd.MM.yyyy HH:mm} · Werkzeug: {ffmpegVersion}\n\n## Prüfung\n");
        foreach (var c in checks) sb.Append($"- {(c.Light switch { "rot" => "🔴", "gelb" => "🟡", _ => "🟢" })} {c.Text}\n");
        sb.Append("\n## Dateien\n| Datei | Größe | SHA-256 |\n|---|---|---|\n");
        foreach (var f in files.Where(File.Exists)) sb.Append($"| {Path.GetFileName(f)} | {new FileInfo(f).Length:N0} Bytes | `{Sha256(f)}` |\n");
        return sb.ToString();
    }
}
