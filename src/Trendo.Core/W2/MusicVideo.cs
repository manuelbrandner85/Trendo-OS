using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Trendo.Core;

/// <summary>Trendo Musik – Musikvideo (V1/V4/V8, freigegeben 03.11.2026): Shotliste aus der Song-Analyse (Abschnitte, Energie, Takt), Schnitte genau auf
/// Taktgrenzen, Kamerafahrten je nach Abschnitt, Film-Looks als selbst erzeugte 3D-LUT, feines Korn, Breitbild-Balken, 16:9 und 9:16.
/// Clips kommen aus eigenen Videos, freiem Filmmaterial oder der Video-KI – es werden keine Standbilder animiert.</summary>
public static class MusicVideo
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static string F(double v) => v.ToString("0.###", Inv);

    public sealed record Section(double Start, double End, double Energy);
    public sealed record Analysis(double Bpm, double Duration, List<Section> Sections);
    public sealed record Shot(int Index, double Start, double Length, int Section, string Part, string Camera, string Prompt);

    /// <summary>Ergebnis des Analyseskripts („--abschnitte“) – oder null.</summary>
    public static Analysis? Parse(string json)
    {
        try
        {
            using var d = JsonDocument.Parse(json);
            var r = d.RootElement;
            var secs = r.GetProperty("sections").EnumerateArray().Select(s => new Section(s.GetProperty("start").GetDouble(), s.GetProperty("end").GetDouble(), s.GetProperty("energy").GetDouble())).ToList();
            var a = new Analysis(r.GetProperty("bpm").GetDouble(), r.GetProperty("duration").GetDouble(), secs);
            return a.Bpm > 0 && a.Duration > 0 && secs.Count > 0 ? a : null;
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException) { return null; }
    }

    /// <summary>Teil eines Lieds nach Lage und Energie: ruhiger Anfang = Intro, kraftvoll = Refrain, Rest Strophe; letzter ruhiger Teil = Outro.</summary>
    public static string PartOf(Analysis a, int i)
    {
        var s = a.Sections[i];
        if (i == 0 && s.Energy < 0.8) return "intro";
        if (i == a.Sections.Count - 1 && s.Energy < 0.9) return "outro";
        if (s.Energy >= 1.2) return "refrain";
        return s.Energy < 0.75 && i > 0 ? "bridge" : "strophe";
    }

    static readonly Dictionary<string, string[]> Cameras = new()
    {
        ["intro"] = ["slow aerial drone establishing shot", "slow push in through the scene", "static wide shot with gentle parallax"],
        ["strophe"] = ["slow dolly in", "handheld medium close-up", "slow lateral tracking shot", "slow pan across the scene"],
        ["refrain"] = ["fast tracking shot following the subject", "orbit around the subject", "crane shot rising up", "dynamic handheld close-up"],
        ["bridge"] = ["slow motion close-up", "slow dolly out", "static shot with shallow depth of field"],
        ["outro"] = ["slow pull back to a wide shot", "slow crane down", "static wide shot as the light fades"],
    };

    /// <summary>Shotliste: je Abschnitt Einstellungen von ganzen Takten (höchstens <paramref name="maxShot"/> s – so lang wie ein Video-Clip),
    /// im Refrain kürzer (schnellere Schnitte), Kamerafahrt wechselnd nach Abschnitt.</summary>
    public static List<Shot> Plan(Analysis a, string style, int beatsPerBar = 4, double maxShot = 5.0)
    {
        var bar = beatsPerBar * 60 / a.Bpm;
        var shots = new List<Shot>();
        for (var i = 0; i < a.Sections.Count; i++)
        {
            var s = a.Sections[i]; var part = PartOf(a, i);
            var fit = Math.Max(1, (int)Math.Floor(maxShot / bar)); // so viele Takte passen in einen Clip
            var calm = Math.Min(part is "intro" or "outro" ? 4 : 3, fit);
            var bars = part == "refrain" ? Math.Max(1, (int)Math.Round(calm * 0.6)) : calm; // Refrain schneller geschnitten
            var len = bars * bar;
            var cams = Cameras[part];
            var t = s.Start; var k = 0;
            while (t < s.End - 0.05)
            {
                var l = Math.Min(len, s.End - t);
                if (l < bar * 0.75 && shots.Count > 0 && shots[^1].Section == i) { shots[^1] = shots[^1] with { Length = shots[^1].Length + l }; break; } // kein Mini-Schnitt am Ende
                var cam = cams[k % cams.Length];
                shots.Add(new(shots.Count, Math.Round(t, 3), Math.Round(l, 3), i, part, cam, PromptFor(style, part, cam, s.Energy)));
                t += l; k++;
            }
        }
        return shots;
    }

    /// <summary>Bildbeschreibung für die Video-KI (englisch, wie es die Modelle am besten verstehen) – ohne Namen echter Personen.</summary>
    public static string PromptFor(string style, string part, string camera, double energy) =>
        $"cinematic music video scene, {style.Trim().TrimEnd(',')}, {camera}, " +
        (energy >= 1.2 ? "energetic, dynamic movement, dramatic lighting" : energy < 0.75 ? "calm, intimate, soft natural light" : "atmospheric, natural light") +
        ", shot on 35mm film, shallow depth of field, professional color grading, photorealistic";

    // ───────────── V4: Film-Looks ─────────────

    public static readonly (string Id, string Name)[] Looks = [("klar", "Klar"), ("warm", "Warm Vintage"), ("kuehl", "Kühl Nacht"), ("kino", "Kino-Kontrast"), ("sw", "Schwarzweiß-Film")];

    /// <summary>3D-LUT (.cube, 33³) für einen Look – selbst berechnet, keine fremden LUT-Dateien.</summary>
    public static string Cube(string look, int n = 33)
    {
        var sb = new StringBuilder($"TITLE \"Trendo {look}\"\nLUT_3D_SIZE {n}\n");
        static double S(double x, double k) => x + k * (x - 0.5) * (1 - Math.Abs(2 * x - 1)); // sanfte S-Kurve
        for (var bi = 0; bi < n; bi++)
            for (var gi = 0; gi < n; gi++)
                for (var ri = 0; ri < n; ri++)
                {
                    double r = ri / (n - 1.0), g = gi / (n - 1.0), b = bi / (n - 1.0);
                    var y = 0.2126 * r + 0.7152 * g + 0.0722 * b;
                    switch (look)
                    {
                        case "warm": // angehobene Schwärzen, warme Mitten, etwas weniger Sättigung
                            (r, g, b) = (Mix(y, r, 0.85), Mix(y, g, 0.85), Mix(y, b, 0.85));
                            (r, g, b) = (0.04 + 0.94 * r * 1.04, 0.035 + 0.94 * g, 0.03 + 0.92 * b * 0.93); break;
                        case "kuehl": // blaue Schatten, mehr Kontrast
                            (r, g, b) = (S(r, 0.25) * 0.95, S(g, 0.25) * 0.98, Math.Min(1, S(b, 0.2) * 1.02 + 0.04 * (1 - y))); break;
                        case "kino": // Kontrast, Schatten leicht blaugrün, Lichter leicht warm – dezent
                            (r, g, b) = (S(r, 0.3) + 0.03 * y - 0.015, S(g, 0.3) + 0.01 * (1 - y), S(b, 0.3) + 0.04 * (1 - y) - 0.03 * y); break;
                        case "sw": { var v = S(y, 0.35); (r, g, b) = (v, v, v); break; }
                        default: (r, g, b) = (S(r, 0.12), S(g, 0.12), S(b, 0.12)); break;
                    }
                    sb.Append(F(Math.Clamp(r, 0, 1))).Append(' ').Append(F(Math.Clamp(g, 0, 1))).Append(' ').Append(F(Math.Clamp(b, 0, 1))).Append('\n');
                }
        return sb.ToString();
        static double Mix(double y, double c, double k) => y + k * (c - y);
    }

    /// <summary>Bildfilter fürs fertige Video: Format füllen, Look (LUT), feines Korn, dezente Vignette, optional Breitbild-Balken (2,39:1).</summary>
    public static string Filter(int w, int h, string lutFile, bool letterbox, int grain = 5)
    {
        var lut = lutFile.Replace("\\", "/").Replace(":", "\\:");
        var f = $"scale={w}:{h}:force_original_aspect_ratio=increase,crop={w}:{h},setsar=1,fps=30,lut3d='{lut}',noise=alls={grain}:allf=t,vignette=angle=PI/6";
        if (letterbox && w > h) { var bar = (int)Math.Round((h - w / 2.39) / 2); f += $",drawbox=x=0:y=0:w=iw:h={bar}:color=black:t=fill,drawbox=x=0:y=ih-{bar}:w=iw:h={bar}:color=black:t=fill"; }
        return f;
    }

    /// <summary>Ein Clip für eine Einstellung: Ausschnitt ab <paramref name="inPoint"/> genau so lang wie die Einstellung, im Format, mit Look.</summary>
    public static string[] ClipArgs(string src, double inPoint, double length, string filter, string output) =>
        ["-hide_banner", "-nostats", "-y", "-ss", F(inPoint), "-i", src, "-t", F(length), "-an", "-vf", filter, "-c:v", "libx264", "-preset", "medium", "-crf", "18", "-pix_fmt", "yuv420p", output];

    /// <summary>Alle Clips hart aneinander (Schnitt auf dem Takt) und darunter das Lied.</summary>
    public static string[] JoinArgs(string listFile, string song, string output, string? subtitles = null)
    {
        var a = new List<string> { "-hide_banner", "-nostats", "-y", "-f", "concat", "-safe", "0", "-i", listFile, "-i", song };
        if (subtitles is not null) { a.AddRange(["-vf", $"subtitles='{subtitles.Replace("\\", "/").Replace(":", "\\:")}'", "-c:v", "libx264", "-preset", "medium", "-crf", "18", "-pix_fmt", "yuv420p"]); }
        else a.AddRange(["-c:v", "copy"]);
        a.AddRange(["-map", "0:v", "-map", "1:a", "-c:a", "aac", "-b:a", "320k", "-shortest", "-movflags", "+faststart", output]);
        return [.. a];
    }

    // ───────────── V3: freies Filmmaterial von Pexels (Lizenz: kostenlos nutzbar, Nennung erwünscht) ─────────────
    public sealed record StockClip(long Id, string Url, int Width, int Height, double Seconds, string Author, string Page);

    public static Uri PexelsSearch(string query, bool portrait, int perPage = 15) =>
        new($"https://api.pexels.com/videos/search?per_page={perPage}&size=medium&orientation={(portrait ? "portrait" : "landscape")}&query=" + Uri.EscapeDataString(query.Trim()));

    /// <summary>Treffer der Pexels-Videosuche: je Video die MP4-Datei, die am besten zum Zielformat passt (mindestens <paramref name="minWidth"/>, sonst die größte), nur Clips ab 4 s.</summary>
    public static List<StockClip> ParsePexels(string json, int minWidth = 1920)
    {
        try
        {
            using var d = JsonDocument.Parse(json);
            var res = new List<StockClip>();
            foreach (var v in d.RootElement.GetProperty("videos").EnumerateArray())
            {
                var dur = v.TryGetProperty("duration", out var du) ? du.GetDouble() : 0;
                if (dur < 4) continue;
                var files = v.GetProperty("video_files").EnumerateArray()
                    .Where(f => f.TryGetProperty("file_type", out var ft) && ft.GetString() == "video/mp4" && f.TryGetProperty("width", out var w) && w.ValueKind == JsonValueKind.Number)
                    .Select(f => (Url: f.GetProperty("link").GetString() ?? "", W: f.GetProperty("width").GetInt32(), H: f.GetProperty("height").GetInt32())).ToList();
                if (files.Count == 0) continue;
                var pick = files.Where(f => Math.Max(f.W, f.H) >= minWidth).OrderBy(f => f.W * f.H).FirstOrDefault();
                if (pick.Url is null or "") pick = files.MaxBy(f => f.W * f.H);
                if (!Uri.TryCreate(pick.Url, UriKind.Absolute, out var u) || u.Scheme != "https") continue;
                res.Add(new(v.GetProperty("id").GetInt64(), pick.Url, pick.W, pick.H, dur, v.TryGetProperty("user", out var us) && us.TryGetProperty("name", out var un) ? un.GetString() ?? "" : "",
                    v.TryGetProperty("url", out var pg) ? pg.GetString() ?? "" : ""));
            }
            return res;
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException) { return []; }
    }

    /// <summary>Suchbegriffe fürs Filmmaterial aus dem Stil (Genre-/Stimm-Wörter weglassen, Bildwörter behalten).</summary>
    public static string StockQuery(string style)
    {
        var skip = new[] { "vocals", "vocal", "voice", "bpm", "male", "female", "duet", "choir", "rap", "pop", "rock", "metal", "house", "techno", "edm", "hip-hop", "trap", "jazz", "blues", "soul", "funk", "schlager", "clear", "breathy", "warm voice", "dark voice", "bright voice" };
        var words = style.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Where(w => !skip.Any(k => w.Contains(k, StringComparison.OrdinalIgnoreCase))).Take(3).ToList();
        return words.Count > 0 ? string.Join(" ", words) : "cinematic city night";
    }

    public static string ConcatList(IEnumerable<string> files) => string.Concat(files.Select(f => $"file '{f.Replace("\\", "/").Replace("'", "'\\''")}'\n"));
}
