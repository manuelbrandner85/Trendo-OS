using System.Globalization;
using System.Text.Json.Nodes;

namespace Trendo.Core.Media;

/// <summary>Ein Stück auf der Zeitleiste: Ausschnitt <see cref="In"/>–<see cref="Out"/> (Sekunden) aus einer Datei.
/// Bildspur: lückenlos hintereinander (Reihenfolge = Liste). Tonspur (Musik): eigene Startzeit und Lautstärke.</summary>
public sealed record EditClip(string Id, string File, double In, double Out, double SourceLength, bool HasAudio = true, double Start = 0, double Volume = 1)
{
    public double Length => Math.Max(0, Out - In);
    public bool IsImage => MediaStudio.IsImage(File);
}

/// <summary>CORE-08 (Masterprompt 131): Videoschnitt-Projekt – Bildspur und Musikspur, Schneiden, Kürzen, Teilen, Verschieben, Export über FFmpeg.
/// Die Daten liegen als JSON im Programmdokument (.tdoc), damit Rückgängig, Autosave und Wiederherstellen der Programm-Hülle greifen.</summary>
public static class VideoEdit
{
    public const double ImageSeconds = 5, MinLength = 0.2;
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static JsonObject NewProject(int width = 1280, int height = 720) => new() { ["clips"] = new JsonArray(), ["music"] = new JsonArray(), ["playhead"] = 0, ["width"] = width, ["height"] = height, ["fps"] = 30 };

    public static List<EditClip> Clips(JsonObject p, string track = "clips") => (p[track] as JsonArray ?? []).OfType<JsonObject>().Select(Read).ToList();
    static EditClip Read(JsonObject o) => new((string?)o["id"] ?? Guid.NewGuid().ToString("N")[..8], (string?)o["file"] ?? "", D(o["in"]), D(o["out"]), D(o["len"]), (bool?)o["audio"] ?? true, D(o["start"]), o["vol"] is null ? 1 : D(o["vol"]));
    static double D(JsonNode? n) => n is null ? 0 : double.TryParse(n.GetValueKind() == System.Text.Json.JsonValueKind.String ? n.GetValue<string>() : n.ToJsonString(), NumberStyles.Float, Inv, out var v) ? v : 0;
    static JsonObject Write(EditClip c) => new() { ["id"] = c.Id, ["file"] = c.File, ["in"] = Math.Round(c.In, 3), ["out"] = Math.Round(c.Out, 3), ["len"] = Math.Round(c.SourceLength, 3), ["audio"] = c.HasAudio, ["start"] = Math.Round(c.Start, 3), ["vol"] = Math.Round(c.Volume, 2) };
    public static void Store(JsonObject p, List<EditClip> clips, string track = "clips") => p[track] = new JsonArray([.. clips.Select(c => (JsonNode)Write(c))]);

    /// <summary>Gesamtlänge: Bildspur, mindestens bis zum Ende der Musik.</summary>
    public static double Duration(JsonObject p) => Math.Max(Clips(p).Sum(c => c.Length), Clips(p, "music").Select(m => m.Start + m.Length).DefaultIfEmpty(0).Max());
    /// <summary>Startzeit jedes Bildspur-Stücks auf der Zeitleiste.</summary>
    public static List<double> Starts(List<EditClip> clips) { var t = 0.0; var l = new List<double>(); foreach (var c in clips) { l.Add(t); t += c.Length; } return l; }

    /// <summary>Stück unter einer Zeitleisten-Zeit (Bildspur) und die Zeit innerhalb der Quelldatei.</summary>
    public static (int Index, double SourceTime)? At(JsonObject p, double t)
    {
        var clips = Clips(p); var s = Starts(clips);
        for (var i = 0; i < clips.Count; i++) if (t >= s[i] && t < s[i] + clips[i].Length) return (i, clips[i].In + (t - s[i]));
        return null;
    }

    public static EditClip NewClip(string file, double sourceLength, bool hasAudio) =>
        MediaStudio.IsImage(file) ? new(Guid.NewGuid().ToString("N")[..8], file, 0, ImageSeconds, 3600, false) : new(Guid.NewGuid().ToString("N")[..8], file, 0, sourceLength, sourceLength, hasAudio);

    public static void Add(JsonObject p, EditClip c, string track = "clips")
    {
        if (track == "music") c = c with { Start = Duration(p) > 0 && Clips(p, "music").Count > 0 ? Clips(p, "music").Max(m => m.Start + m.Length) : 0 };
        var l = Clips(p, track); l.Add(c); Store(p, l, track);
    }

    /// <summary>Am Zeitpunkt der Zeitleiste in zwei Stücke teilen.</summary>
    public static bool Split(JsonObject p, double t)
    {
        var clips = Clips(p); var s = Starts(clips);
        for (var i = 0; i < clips.Count; i++)
        {
            var local = t - s[i];
            if (local <= MinLength || local >= clips[i].Length - MinLength) continue;
            var c = clips[i]; var cut = c.In + local;
            clips[i] = c with { Out = cut }; clips.Insert(i + 1, c with { Id = Guid.NewGuid().ToString("N")[..8], In = cut });
            Store(p, clips); return true;
        }
        return false;
    }

    /// <summary>Anfang oder Ende eines Stücks verschieben (Sekunden), innerhalb der Quelle und mit Mindestlänge.</summary>
    public static void Trim(JsonObject p, int index, double newIn, double newOut, string track = "clips")
    {
        var l = Clips(p, track); if (index < 0 || index >= l.Count) return;
        var c = l[index];
        var max = c.IsImage ? 3600 : c.SourceLength;
        newIn = Math.Clamp(newIn, 0, max - MinLength); newOut = Math.Clamp(newOut, newIn + MinLength, max);
        l[index] = c with { In = newIn, Out = newOut }; Store(p, l, track);
    }

    public static void Move(JsonObject p, int from, int to)
    {
        var l = Clips(p); if (from < 0 || from >= l.Count) return;
        to = Math.Clamp(to, 0, l.Count - 1); var c = l[from]; l.RemoveAt(from); l.Insert(to, c); Store(p, l);
    }

    public static void Remove(JsonObject p, int index, string track = "clips") { var l = Clips(p, track); if (index >= 0 && index < l.Count) { l.RemoveAt(index); Store(p, l, track); } }

    public static void SetMusic(JsonObject p, int index, double start, double volume)
    {
        var l = Clips(p, "music"); if (index < 0 || index >= l.Count) return;
        l[index] = l[index] with { Start = Math.Max(0, start), Volume = Math.Clamp(volume, 0, 2) }; Store(p, l, "music");
    }

    static string F(double v) => v.ToString("0.###", Inv);

    /// <summary>FFmpeg-Aufruf für den Export: jedes Stück zugeschnitten, auf gleiche Größe und Bildrate gebracht, aneinandergehängt;
    /// Stücke ohne Ton bekommen Stille; Musik mit Startzeit und Lautstärke darübergemischt. H.264 + AAC, MP4.</summary>
    public static List<string> ExportArgs(JsonObject p, string output)
    {
        var clips = Clips(p); var music = Clips(p, "music");
        if (clips.Count == 0) throw new InvalidOperationException("Die Bildspur ist leer – zuerst Videos oder Bilder hinzufügen.");
        if (clips.FirstOrDefault(c => !System.IO.File.Exists(c.File)) is { } miss) throw new FileNotFoundException("Datei fehlt: " + Path.GetFileName(miss.File));
        int w = (int)D(p["width"]), h = (int)D(p["height"]), fps = (int)Math.Max(1, D(p["fps"])); if (w < 16) w = 1280; if (h < 16) h = 720;
        var a = new List<string> { "-hide_banner", "-loglevel", "error", "-y" };
        var f = new List<string>(); var input = 0; var concat = "";
        foreach (var c in clips)
        {
            if (c.IsImage) a.AddRange(["-loop", "1", "-framerate", fps.ToString(Inv), "-t", F(c.Length), "-i", c.File]);
            else a.AddRange(["-ss", F(c.In), "-t", F(c.Length), "-i", c.File]);
            var vi = input++;
            f.Add($"[{vi}:v]scale={w}:{h}:force_original_aspect_ratio=decrease,pad={w}:{h}:(ow-iw)/2:(oh-ih)/2,setsar=1,fps={fps},format=yuv420p[v{vi}]");
            string ai;
            if (c.HasAudio && !c.IsImage) { ai = $"{vi}:a"; }
            else { a.AddRange(["-f", "lavfi", "-t", F(c.Length), "-i", "anullsrc=r=48000:cl=stereo"]); ai = $"{input++}:a"; }
            f.Add($"[{ai}]aresample=48000,aformat=channel_layouts=stereo[a{vi}]");
            concat += $"[v{vi}][a{vi}]";
        }
        f.Add($"{concat}concat=n={clips.Count}:v=1:a=1[vout][abase]");
        var mix = "[abase]"; var n = 1;
        foreach (var m in music)
        {
            if (!System.IO.File.Exists(m.File)) throw new FileNotFoundException("Datei fehlt: " + Path.GetFileName(m.File));
            a.AddRange(["-ss", F(m.In), "-t", F(m.Length), "-i", m.File]);
            var ms = (long)Math.Round(m.Start * 1000);
            f.Add($"[{input++}:a]aresample=48000,aformat=channel_layouts=stereo,volume={F(m.Volume)},adelay={ms}|{ms}[m{n}]");
            mix += $"[m{n}]"; n++;
        }
        var total = clips.Sum(c => c.Length);
        f.Add(n > 1 ? $"{mix}amix=inputs={n}:duration=first:normalize=0[aout]" : "[abase]anull[aout]");
        a.AddRange(["-filter_complex", string.Join(";", f), "-map", "[vout]", "-map", "[aout]", "-t", F(total),
            "-c:v", "libx264", "-preset", "veryfast", "-crf", "20", "-pix_fmt", "yuv420p", "-c:a", "aac", "-b:a", "192k", "-movflags", "+faststart", output]);
        return a;
    }

    /// <summary>Export ausführen; prüft danach, dass eine Datei mit Inhalt entstanden ist.</summary>
    public static string Export(string ffmpeg, JsonObject p, string output, int timeoutMs = 1800000)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        var r = Cli.Run(ffmpeg, ExportArgs(p, output), Path.GetDirectoryName(Path.GetFullPath(output)), timeoutMs);
        if (!r.Ok || !System.IO.File.Exists(output) || new FileInfo(output).Length == 0)
            throw new InvalidOperationException("Export hat nicht geklappt: " + (r.Err.Length > 0 ? r.Err.Trim().Split('\n')[^1] : $"FFmpeg-Code {r.Exit}"));
        return output;
    }

    /// <summary>Hat die Datei eine Tonspur? (ffprobe neben ffmpeg)</summary>
    public static bool HasAudioStream(string ffmpeg, string file)
    {
        var probe = Path.Combine(Path.GetDirectoryName(ffmpeg) ?? "", Path.GetFileName(ffmpeg).Replace("ffmpeg", "ffprobe"));
        var r = Cli.Run(System.IO.File.Exists(probe) ? probe : "ffprobe", ["-v", "error", "-select_streams", "a", "-show_entries", "stream=index", "-of", "csv=p=0", file], null, 30000);
        return r.Ok && r.Out.Trim().Length > 0;
    }
}
