using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace Trendo.Core.Media;

/// <summary>Videoschnitt X1/X2/X11/X16 (freigegeben 04.11.2026): Export-Vorlagen, Grafikkarten-Encoder (NVENC), Lautheit −14 LUFS, Bereich,
/// nur Ton – und Übergabe an andere Schnittprogramme (EDL, FCPXML). Selbst gebaut statt Kdenlive.</summary>
public static class VideoExport
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static string F(double v) => v.ToString("0.###", Inv);

    /// <summary>W/H = 0: Größe des Projekts. MaxMb: Zielgröße (Bitrate wird daraus berechnet). AudioOnly: nur Ton (M4A).</summary>
    /// <param name="Sequence">Einzelbilder in einen Ordner (plus WAV) statt einer Filmdatei (Paket 01 S3-18).</param>
    public sealed record Preset(string Id, string Label, string Hint, int W, int H, string Codec, int Quality, string Ext = ".mp4", double MaxMb = 0, bool AudioOnly = false, bool Sequence = false);

    public static readonly Preset[] Presets =
    [
        new("projekt", "Wie das Projekt", "Größe und Bildrate des Projekts", 0, 0, "h264", 20),
        new("yt1080", "YouTube 1080p", "1920 × 1080, hohe Qualität", 1920, 1080, "h264", 18),
        new("yt4k", "YouTube 4K", "3840 × 2160, H.265", 3840, 2160, "hevc", 20),
        new("hoch", "Hochformat 9:16", "1080 × 1920 für Reels, Shorts, TikTok", 1080, 1920, "h264", 19),
        new("feed", "Feed 4:5", "1080 × 1350 für Instagram", 1080, 1350, "h264", 19),
        new("whatsapp", "WhatsApp / klein", "1280 × 720, höchstens 15 MB", 1280, 720, "h264", 0, ".mp4", 15),
        new("av1", "AV1 (sehr klein)", "Projektgröße, moderner Codec", 0, 0, "av1", 30),
        new("ton", "Nur Ton", "M4A (AAC), z. B. für einen Podcast", 0, 0, "aac", 0, ".m4a", 0, true),
        // Paket 01 S3-18: Profi-Ausgabe für Schnitt und VFX
        new("prores", "ProRes 422 HQ", "Für Resolve, Premiere, Final Cut – .mov, 10 Bit, Ton unkomprimiert", 0, 0, "prores", 0, ".mov"),
        new("png", "PNG-Bildsequenz", "Einzelbilder + WAV in einem Ordner, z. B. für VFX", 0, 0, "png", 0, ".png", 0, false, true),
        new("exr", "EXR-Bildsequenz", "OpenEXR (Fließkomma, ZIP) + WAV für Compositing", 0, 0, "exr", 0, ".exr", 0, false, true),
    ];

    /// <summary>Welche Encoder dieses FFmpeg enthält („ffmpeg -encoders“), zwischengespeichert.</summary>
    public static IReadOnlySet<string> Encoders(string ffmpeg)
    {
        lock (EncCache) if (EncCache.TryGetValue(ffmpeg, out var c)) return c;
        var set = ParseEncoders(Cli.Run(ffmpeg, ["-hide_banner", "-encoders"], null, 20000).Out);
        lock (EncCache) EncCache[ffmpeg] = set;
        return set;
    }
    static readonly Dictionary<string, IReadOnlySet<string>> EncCache = [];

    public static IReadOnlySet<string> ParseEncoders(string list) =>
        list.Split('\n').Select(l => System.Text.RegularExpressions.Regex.Match(l, @"^\s*[VAS][F.][S.][X.][B.][D.]\s+(\S+)")).Where(m => m.Success).Select(m => m.Groups[1].Value).ToHashSet();

    /// <summary>Software-Encoder, den eine Vorlage braucht (ohne Grafikkarte).</summary>
    public static string NeededEncoder(Preset p) => p.AudioOnly ? "aac" : Encoder(p, null, 60).Encoder;

    /// <summary>Paket 01 S3-18-N: fehlt der Encoder (nicht enthalten oder nicht lizenziert), nennt Trendo eine klare Alternative statt still etwas anderes zu liefern.</summary>
    public static (bool Ok, Preset? Alternative, string Text) Check(Preset p, IReadOnlySet<string> encoders)
    {
        var need = NeededEncoder(p);
        if (encoders.Contains(need)) return (true, null, "");
        Preset? alt = p.Codec switch { "exr" => Get("png"), "prores" => Get("yt1080"), "hevc" or "av1" => Get("projekt"), _ => null };
        if (alt is not null && !encoders.Contains(NeededEncoder(alt))) alt = null;
        var altText = alt is not null ? $" Alternative: „{alt.Label}“." : " Bitte ein FFmpeg mit diesem Encoder einrichten (Zusatzprogramme).";
        return (false, alt, $"„{p.Label}“ geht mit diesem FFmpeg nicht – Encoder „{need}“ fehlt.{altText}");
    }

    /// <summary>Ausgabe bei Bildsequenzen: Ordner mit „Name_00001.ext“ und „Name.wav“.</summary>
    public static (string Frames, string Wav) SequencePaths(string folder, Preset p)
    {
        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(folder)); if (name.Length == 0) name = "Film";
        return (Path.Combine(folder, name + "_%05d" + p.Ext), Path.Combine(folder, name + ".wav"));
    }

    /// <summary>Ist eine echte Ausgabe entstanden? Film: Datei mit Inhalt; Sequenz: mindestens ein Bild.</summary>
    public static bool Produced(string output, Preset p) => p.Sequence
        ? Directory.Exists(output) && Directory.EnumerateFiles(output, "*" + p.Ext).Any(f => new FileInfo(f).Length > 0)
        : File.Exists(output) && new FileInfo(output).Length > 0;

    public static Preset Get(string id) => Presets.FirstOrDefault(p => p.Id == id) ?? Presets[0];

    /// <summary>Export-Einstellungen: Vorlage, Encoder-Liste der Grafikkarte, Bereich (Sekunden), Lautheit angleichen.</summary>
    public sealed record Options(Preset Preset, IReadOnlySet<string>? Gpu = null, double From = 0, double To = 0, bool Loudness = false, bool Ducking = false, string? Subtitles = null);

    /// <summary>Encoder wählen: NVENC, wenn die Grafikkarte ihn wirklich kann (vorher gemessen), sonst die Software-Encoder.</summary>
    public static (string Encoder, List<string> Args) Encoder(Preset p, IReadOnlySet<string>? gpu, double seconds)
    {
        var nv = p.Codec switch { "hevc" => "hevc_nvenc", "av1" => "av1_nvenc", _ => "h264_nvenc" };
        var useGpu = gpu?.Contains(nv) == true;
        if (p.MaxMb > 0)
        {
            var kbps = Math.Max(300, (int)(p.MaxMb * 8192 * 0.95 / Math.Max(1, seconds)) - 96); // Ton 96 kbit/s, 5 % Reserve
            return useGpu ? (nv, ["-c:v", nv, "-preset", "p5", "-rc", "vbr", "-b:v", $"{kbps}k", "-maxrate", $"{kbps}k", "-bufsize", $"{kbps * 2}k"])
                          : ("libx264", ["-c:v", "libx264", "-preset", "medium", "-b:v", $"{kbps}k", "-maxrate", $"{kbps}k", "-bufsize", $"{kbps * 2}k"]);
        }
        var q = p.Quality.ToString(Inv);
        return (p.Codec, useGpu) switch
        {
            ("hevc", true) => (nv, ["-c:v", nv, "-preset", "p5", "-tune", "hq", "-rc", "vbr", "-cq", q, "-b:v", "0", "-tag:v", "hvc1"]),
            ("hevc", false) => ("libx265", ["-c:v", "libx265", "-preset", "medium", "-crf", q, "-tag:v", "hvc1"]),
            ("av1", true) => (nv, ["-c:v", nv, "-preset", "p5", "-cq", q, "-b:v", "0"]),
            ("av1", false) => ("libsvtav1", ["-c:v", "libsvtav1", "-preset", "8", "-crf", q]),
            ("prores", _) => ("prores_ks", ["-c:v", "prores_ks", "-profile:v", "3", "-vendor", "apl0", "-pix_fmt", "yuv422p10le"]),
            ("png", _) => ("png", ["-c:v", "png", "-pix_fmt", "rgb24"]),
            ("exr", _) => ("exr", ["-c:v", "exr", "-compression", "zip1", "-format", "half", "-pix_fmt", "gbrpf32le"]),
            (_, true) => (nv, ["-c:v", nv, "-preset", "p5", "-tune", "hq", "-rc", "vbr", "-cq", (p.Quality + 1).ToString(Inv), "-b:v", "0"]),
            _ => ("libx264", ["-c:v", "libx264", "-preset", "veryfast", "-crf", q]),
        };
    }

    /// <summary>Schluss der Filterkette: Bereich, Zielgröße (andere Seitenverhältnisse mit weichgezeichnetem Hintergrund statt schwarzer Balken), Lautheit.
    /// Liefert die neuen Ausgänge für Bild und Ton.</summary>
    public static (string V, string A) Tail(List<string> f, string v, string a, int w, int h, Options o)
    {
        if (o.To > o.From && o.To > 0)
        {
            f.Add($"[{v}]trim=start={F(o.From)}:end={F(o.To)},setpts=PTS-STARTPTS[vrange]"); v = "vrange";
            f.Add($"[{a}]atrim=start={F(o.From)}:end={F(o.To)},asetpts=PTS-STARTPTS[arange]"); a = "arange";
        }
        int W = o.Preset.W, H = o.Preset.H;
        if (!o.Preset.AudioOnly && W > 0 && H > 0 && (W != w || H != h))
        {
            if (Math.Abs(W / (double)H - w / (double)h) < 0.01) f.Add($"[{v}]scale={W}:{H}:flags=lanczos,setsar=1[vsize]");
            else
            {
                f.Add($"[{v}]split[vbg0][vfg0]");
                f.Add($"[vbg0]scale={W}:{H}:force_original_aspect_ratio=increase,crop={W}:{H},boxblur=24:2,eq=brightness=-0.06[vbg]");
                f.Add($"[vfg0]scale={W}:{H}:force_original_aspect_ratio=decrease:flags=lanczos[vfg]");
                f.Add("[vbg][vfg]overlay=(W-w)/2:(H-h)/2,setsar=1[vsize]");
            }
            v = "vsize";
        }
        if (!o.Preset.AudioOnly && o.Subtitles is { Length: > 0 } srt && File.Exists(srt))
        {
            // X10: Untertitel einbrennen – Schriftgröße passt sich der Bildhöhe an (Hochformat größer)
            var size = (W > 0 && H > W) || (W == 0 && h > w) ? 13 : 20;
            f.Add($"[{v}]subtitles=filename='{SubPath(srt)}':force_style='FontName=Arial,FontSize={size},Bold=1,Outline=2,Shadow=0,MarginV=36'[vsub]"); v = "vsub";
        }
        if (o.Loudness) { f.Add($"[{a}]loudnorm=I=-14:TP=-1:LRA=11,aresample=48000[aloud]"); a = "aloud"; }
        return (v, a);
    }

    /// <summary>Pfad für den FFmpeg-Filter „subtitles“: Schrägstriche, Doppelpunkt und Hochkomma maskiert (Windows-Laufwerk C: wird C\:).</summary>
    public static string SubPath(string path) => path.Replace('\\', '/').Replace(":", "\\:").Replace("'", "\\'");

    /// <summary>SRT lesen: (Start, Ende, Text) je Untertitel.</summary>
    public static List<(double Start, double End, string Text)> ReadSrt(string srt)
    {
        var list = new List<(double, double, string)>();
        foreach (var block in srt.Replace("\r\n", "\n").Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
        {
            var lines = block.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var ti = Array.FindIndex(lines, l => l.Contains("-->"));
            if (ti < 0) continue;
            var parts = lines[ti].Split("-->");
            if (parts.Length != 2 || !TryTime(parts[0], out var a) || !TryTime(parts[1], out var b)) continue;
            list.Add((a, b, string.Join(" ", lines.Skip(ti + 1)).Trim()));
        }
        return list;
    }

    static bool TryTime(string s, out double sec)
    {
        sec = 0; s = s.Trim().Replace(',', '.');
        var p = s.Split(':'); if (p.Length != 3) return false;
        if (!int.TryParse(p[0], out var h) || !int.TryParse(p[1], out var m) || !double.TryParse(p[2], NumberStyles.Float, Inv, out var x)) return false;
        sec = h * 3600 + m * 60 + x; return true;
    }

    /// <summary>Länge des Ergebnisses (mit Bereich).</summary>
    public static double Length(double total, Options o) => o.To > o.From && o.To > 0 ? Math.Min(total, o.To) - Math.Max(0, o.From) : total;

    /// <summary>NVENC wirklich prüfen (1 Bild kodieren) – „ffmpeg -encoders“ allein reicht nicht, die Grafikkarte muss es können.</summary>
    public static IReadOnlySet<string> GpuEncoders(string ffmpeg)
    {
        lock (Cache) if (Cache.TryGetValue(ffmpeg, out var c)) return c;
        var set = new HashSet<string>();
        foreach (var e in new[] { "h264_nvenc", "hevc_nvenc", "av1_nvenc" })
        {
            var r = Cli.Run(ffmpeg, ["-hide_banner", "-loglevel", "error", "-f", "lavfi", "-i", "color=c=black:s=256x144:d=0.1", "-frames:v", "2", "-c:v", e, "-f", "null", "-"], null, 20000);
            if (r.Ok) set.Add(e);
        }
        lock (Cache) Cache[ffmpeg] = set;
        return set;
    }
    static readonly Dictionary<string, IReadOnlySet<string>> Cache = [];

    // ───────────── X16: Übergabe an andere Schnittprogramme ─────────────

    static string Tc(double s, int fps) => FilmDelivery.Timecode(s, fps);

    /// <summary>EDL (CMX 3600) der Bildspur und der Musik (Spur A2) – lesen Resolve, Premiere, Avid. Überblendungen werden als Dissolve (D) geschrieben.</summary>
    public static string Edl(JsonObject p, string title)
    {
        var clips = VideoEdit.Clips(p); var fps = (int)Math.Max(1, ProgramFps(p)); var starts = VideoEdit.Starts(clips);
        var sb = new StringBuilder().Append("TITLE: ").AppendLine(title.Length > 0 ? title : "Trendo").AppendLine("FCM: NON-DROP FRAME").AppendLine();
        for (var i = 0; i < clips.Count; i++)
        {
            var c = clips[i]; var reel = "AX";
            var fadeIn = i > 0 ? VideoEdit.FadeOf(clips, i - 1) : 0;
            var kind = fadeIn > 0 ? $"D    {(int)Math.Round(fadeIn * fps):000}" : "C        ";
            sb.AppendLine($"{i + 1:000}  {reel,-8} AA/V  {kind} {Tc(c.In, fps)} {Tc(c.Out, fps)} {Tc(starts[i], fps)} {Tc(starts[i] + c.Length, fps)}");
            sb.AppendLine($"* FROM CLIP NAME: {Path.GetFileName(c.File)}");
            sb.AppendLine($"* SOURCE FILE: {c.File}");
            if (c.Title.Length > 0) sb.AppendLine($"* COMMENT: Titel „{c.Title}“");
            sb.AppendLine();
        }
        var ev = clips.Count + 1;
        foreach (var m in VideoEdit.Clips(p, "music").OrderBy(m => m.Start)) // Paket 01 S3-15: Musik mit Einsatz-Timecode
        {
            sb.AppendLine($"{ev++:000}  {"AX",-8} A2    C         {Tc(m.In, fps)} {Tc(m.Out, fps)} {Tc(m.Start, fps)} {Tc(m.Start + m.Length, fps)}");
            sb.AppendLine($"* FROM CLIP NAME: {Path.GetFileName(m.File)}");
            sb.AppendLine($"* SOURCE FILE: {m.File}");
            sb.AppendLine();
        }
        return sb.ToString();
    }

    static double ProgramFps(JsonObject p) => Num(p["fps"], 30);

    static string R(double s, int fps) => $"{(long)Math.Round(s * fps) * 100}/{fps * 100}s";

    /// <summary>FCPXML 1.10 – Final Cut Pro und DaVinci Resolve öffnen damit Bild- und Musikspur mit Schnittpunkten.</summary>
    public static string Fcpxml(JsonObject p, string title)
    {
        var clips = VideoEdit.Clips(p); var music = VideoEdit.Clips(p, "music"); var fps = (int)Math.Max(1, ProgramFps(p));
        int w = (int)Math.Max(16, Num(p["width"], 1280)), h = (int)Math.Max(16, Num(p["height"], 720));
        var all = clips.Concat(music).Select(c => c.File).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var res = new XElement("resources", new XElement("format", new XAttribute("id", "r0"), new XAttribute("name", $"FFVideoFormat{h}p{fps}"),
            new XAttribute("frameDuration", $"100/{fps * 100}s"), new XAttribute("width", w), new XAttribute("height", h)));
        string Asset(string file) => "a" + (all.IndexOf(file) + 1);
        foreach (var f in all)
        {
            var c = clips.Concat(music).First(x => x.File == f);
            res.Add(new XElement("asset", new XAttribute("id", Asset(f)), new XAttribute("name", Path.GetFileNameWithoutExtension(f)), new XAttribute("start", "0s"),
                new XAttribute("duration", R(Math.Max(c.SourceLength, c.Out), fps)), new XAttribute("hasVideo", c.IsImage || clips.Any(x => x.File == f) ? "1" : "0"),
                new XAttribute("hasAudio", c.HasAudio ? "1" : "0"), new XAttribute("format", "r0"),
                new XElement("media-rep", new XAttribute("kind", "original-media"), new XAttribute("src", new Uri(Path.GetFullPath(f)).AbsoluteUri))));
        }
        var spine = new XElement("spine"); var t = 0.0; var starts = VideoEdit.Starts(clips);
        for (var i = 0; i < clips.Count; i++)
        {
            var c = clips[i];
            var el = new XElement("asset-clip", new XAttribute("ref", Asset(c.File)), new XAttribute("name", Path.GetFileNameWithoutExtension(c.File)),
                new XAttribute("offset", R(starts[i], fps)), new XAttribute("start", R(c.In, fps)), new XAttribute("duration", R(c.Length, fps)), new XAttribute("format", "r0"));
            if (i == 0)
                foreach (var m in music)
                    el.Add(new XElement("asset-clip", new XAttribute("ref", Asset(m.File)), new XAttribute("lane", "-1"), new XAttribute("name", Path.GetFileNameWithoutExtension(m.File)),
                        new XAttribute("offset", R(m.Start + c.In, fps)), new XAttribute("start", R(m.In, fps)), new XAttribute("duration", R(m.Length, fps)), new XAttribute("audioRole", "music")));
            spine.Add(el); t = starts[i] + c.Length;
        }
        var doc = new XDocument(new XDeclaration("1.0", "UTF-8", null), new XElement("fcpxml", new XAttribute("version", "1.10"), res,
            new XElement("library", new XElement("event", new XAttribute("name", "Trendo"),
                new XElement("project", new XAttribute("name", title.Length > 0 ? title : "Trendo"),
                    new XElement("sequence", new XAttribute("format", "r0"), new XAttribute("duration", R(t, fps)), new XAttribute("tcStart", "0s"), spine))))));
        return "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<!DOCTYPE fcpxml>\n" + doc.Root!.ToString();
    }

    static double Num(JsonNode? n, double f) => n is JsonValue v && double.TryParse(v.GetValueKind() == System.Text.Json.JsonValueKind.String ? v.GetValue<string>() : v.ToJsonString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : f; // auch ganzzahlig gespeicherte Werte (fps = 25)
}
