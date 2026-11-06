using System.Globalization;
using System.Text.Json.Nodes;

namespace Trendo.Core.Media;

/// <summary>Videoschnitt wie Premiere/DaVinci (Profi 23, 05.11.2026): Farbkorrektur je Stück, Tonpegel je Stück, Übergangsarten,
/// Titel mit Lage und Größe, zweite Bildspur für Bild im Bild, Logos und Einblendungen. Bestehende Projekte bleiben gültig:
/// Zusatzwerte je Stück liegen unter „fx“ (nach Stück-Kennung), die zweite Bildspur unter „overlay“.</summary>
public static class VideoPro
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static string F(double v) => v.ToString("0.###", Inv);

    public sealed record ClipFx(double Brightness = 0, double Contrast = 1, double Saturation = 1, double Warmth = 0, string Transition = "fade", string TitlePos = "unten", double TitleSize = 1)
    {
        public bool HasColor => Math.Abs(Brightness) > 0.001 || Math.Abs(Contrast - 1) > 0.001 || Math.Abs(Saturation - 1) > 0.001 || Math.Abs(Warmth) > 0.001;
    }

    public static readonly (string Id, string Label)[] Transitions =
        [("fade", "Überblenden"), ("fadeblack", "Über Schwarz"), ("dissolve", "Auflösen"), ("wipeleft", "Wischen"), ("slideleft", "Schieben"), ("circleopen", "Kreis öffnen")];

    /// <summary>Farb-Vorlagen wie die LUT-Auswahl in Schnittprogrammen.</summary>
    public static readonly (string Id, string Label, ClipFx Fx)[] Looks =
    [
        ("natur", "Natürlich", new ClipFx()),
        ("warm", "Warm (Sonnenuntergang)", new ClipFx(0.02, 1.05, 1.1, 0.35)),
        ("kuehl", "Kühl (Winter, Technik)", new ClipFx(0, 1.05, 0.95, -0.35)),
        ("kontrast", "Kontrastreich", new ClipFx(0, 1.25, 1.15, 0)),
        ("film", "Film-Look (matt)", new ClipFx(0.03, 0.9, 0.85, 0.1)),
        ("sw", "Schwarzweiß", new ClipFx(0, 1.1, 0, 0)),
    ];

    public static ClipFx Fx(JsonObject p, string clipId)
    {
        if ((p["fx"] as JsonObject)?[clipId] is not JsonObject o) return new ClipFx();
        double D(string k, double d) => o[k] is { } n && double.TryParse(n.ToJsonString(), NumberStyles.Float, Inv, out var v) ? v : d;
        string S(string k, string d) => o[k]?.GetValue<string>() is { Length: > 0 } s ? s : d;
        return new ClipFx(D("b", 0), D("c", 1), D("s", 1), D("w", 0), S("t", "fade"), S("tp", "unten"), D("ts", 1));
    }

    public static void SetFx(JsonObject p, string clipId, ClipFx fx)
    {
        var all = p["fx"] as JsonObject ?? (JsonObject)(p["fx"] = new JsonObject());
        if (!Transitions.Any(t => t.Id == fx.Transition)) throw new ArgumentException("Unbekannter Übergang: " + fx.Transition);
        all[clipId] = new JsonObject
        {
            ["b"] = Math.Round(Math.Clamp(fx.Brightness, -0.5, 0.5), 3), ["c"] = Math.Round(Math.Clamp(fx.Contrast, 0.3, 2), 3), ["s"] = Math.Round(Math.Clamp(fx.Saturation, 0, 3), 3),
            ["w"] = Math.Round(Math.Clamp(fx.Warmth, -1, 1), 3), ["t"] = fx.Transition, ["tp"] = fx.TitlePos is "oben" or "mitte" ? fx.TitlePos : "unten", ["ts"] = Math.Round(Math.Clamp(fx.TitleSize, 0.5, 3), 2),
        };
    }

    /// <summary>Tonpegel eines Stücks der Bildspur (0 = stumm, 1 = Original, 2 = doppelt).</summary>
    public static void SetClipVolume(JsonObject p, int index, double volume)
    {
        var l = VideoEdit.Clips(p); if (index < 0 || index >= l.Count) return;
        l[index] = l[index] with { Volume = Math.Round(Math.Clamp(volume, 0, 2), 2) }; VideoEdit.Store(p, l);
    }

    /// <summary>FFmpeg-Farbfilter (Helligkeit, Kontrast, Sättigung, Wärme über die Farbbalance).</summary>
    public static string ColorFilter(ClipFx fx)
    {
        if (!fx.HasColor) return "";
        var s = $",eq=brightness={F(fx.Brightness)}:contrast={F(fx.Contrast)}:saturation={F(fx.Saturation)}";
        if (Math.Abs(fx.Warmth) > 0.001) { var k = fx.Warmth * 0.25; s += $",colorbalance=rs={F(k)}:gs={F(k * 0.2)}:bs={F(-k)}:rm={F(k * 0.6)}:bm={F(-k * 0.6)}"; }
        return s;
    }

    /// <summary>Senkrechte Lage der Texteinblendung.</summary>
    public static string TitleY(string pos) => pos switch { "oben" => "h/10", "mitte" => "(h-text_h)/2", _ => "h-text_h-h/10" };

    // ---------- zweite Bildspur ----------
    /// <param name="Key">Farbe für Greenscreen als „#RRGGBB“, leer = aus (Paket 01 S3-05).</param>
    /// <param name="Mask">Maske (Ausschnitt der Einblendung) in Anteilen 0…1: X, Y, Breite, Höhe – außerhalb bleibt durchsichtig.</param>
    public sealed record Overlay(string Id, string File, double Start, double Length, double X, double Y, double Width, double Opacity,
        string Key = "", double KeySimilarity = 0.15, double KeyBlend = 0.05, (double X, double Y, double W, double H)? Mask = null);

    public static List<Overlay> Overlays(JsonObject p) => (p["overlay"] as JsonArray ?? []).OfType<JsonObject>().Select(o =>
    {
        double D(string k, double d) => o[k] is { } n && double.TryParse(n.ToJsonString(), NumberStyles.Float, Inv, out var v) ? v : d;
        (double, double, double, double)? mask = o["mask"] is JsonArray m && m.Count == 4 && m.All(n => n is JsonValue)
            ? (Num(m[0]), Num(m[1]), Num(m[2]), Num(m[3])) : null;
        return new Overlay((string?)o["id"] ?? "", (string?)o["file"] ?? "", D("start", 0), D("len", 5), D("x", 0.68), D("y", 0.05), D("w", 0.28), D("op", 1),
            (string?)o["key"] ?? "", D("ksim", 0.15), D("kblend", 0.05), mask);
        static double Num(JsonNode? n) => n is not null && double.TryParse(n.ToJsonString(), NumberStyles.Float, Inv, out var v) ? v : 0;
    }).ToList();

    static void StoreOverlays(JsonObject p, IEnumerable<Overlay> l) => p["overlay"] = new JsonArray([.. l.Select(o => (JsonNode)new JsonObject
    {
        ["id"] = o.Id, ["file"] = o.File, ["start"] = Math.Round(o.Start, 3), ["len"] = Math.Round(o.Length, 3), ["x"] = Math.Round(o.X, 3), ["y"] = Math.Round(o.Y, 3), ["w"] = Math.Round(o.Width, 3), ["op"] = Math.Round(o.Opacity, 2),
        ["key"] = o.Key, ["ksim"] = Math.Round(o.KeySimilarity, 3), ["kblend"] = Math.Round(o.KeyBlend, 3),
        ["mask"] = o.Mask is { } m ? new JsonArray(Math.Round(m.X, 4), Math.Round(m.Y, 4), Math.Round(m.W, 4), Math.Round(m.H, 4)) : null,
    })]);

    /// <summary>Greenscreen für eine Einblendung: Farbe „#RRGGBB“ (leer = aus), Ähnlichkeit 0,01–0,6, weiche Kante 0–0,5.</summary>
    public static void SetKey(JsonObject p, string id, string color, double similarity = 0.15, double blend = 0.05)
    {
        color = (color ?? "").Trim();
        if (color.Length > 0 && !System.Text.RegularExpressions.Regex.IsMatch(color, "^#[0-9A-Fa-f]{6}$")) throw new ArgumentException("Farbe als #RRGGBB angeben, z. B. #00FF00 für Grün.");
        var l = Overlays(p); var i = l.FindIndex(o => o.Id == id); if (i < 0) return;
        l[i] = l[i] with { Key = color.ToUpperInvariant(), KeySimilarity = Math.Clamp(similarity, 0.01, 0.6), KeyBlend = Math.Clamp(blend, 0, 0.5) };
        StoreOverlays(p, l);
    }

    /// <summary>Maske setzen (null = ganze Einblendung). Eine Maske außerhalb des Bildes wird abgelehnt, nicht still beschnitten.</summary>
    public static void SetMask(JsonObject p, string id, (double X, double Y, double W, double H)? mask)
    {
        if (mask is { } m && (m.X < 0 || m.Y < 0 || m.W < 0.02 || m.H < 0.02 || m.X + m.W > 1 + 1e-6 || m.Y + m.H > 1 + 1e-6 || double.IsNaN(m.X + m.Y + m.W + m.H)))
            throw new ArgumentOutOfRangeException(nameof(mask), "Die Maske liegt außerhalb des Bildes – X+Breite und Y+Höhe dürfen höchstens 100 % sein.");
        var l = Overlays(p); var i = l.FindIndex(o => o.Id == id); if (i < 0) return;
        l[i] = l[i] with { Mask = mask }; StoreOverlays(p, l);
    }

    /// <summary>FFmpeg-Filter vor dem Skalieren: Greenscreen (chromakey) und Maske (Ausschnitt, Rest durchsichtig, Größe bleibt).</summary>
    public static string KeyAndMask(Overlay o)
    {
        var f = new List<string>();
        if (o.Key.Length == 7 || o.Mask is not null) f.Add("format=yuva444p");
        if (o.Key.Length == 7) f.Add($"chromakey=0x{o.Key[1..]}:{F(o.KeySimilarity)}:{F(o.KeyBlend)}");
        if (o.Mask is { } m) f.Add($"crop=iw*{F(m.W)}:ih*{F(m.H)}:iw*{F(m.X)}:ih*{F(m.Y)},pad=iw/{F(m.W)}:ih/{F(m.H)}:iw/{F(m.W)}*{F(m.X)}:ih/{F(m.H)}*{F(m.Y)}:color=black@0");
        return f.Count == 0 ? "" : string.Join(",", f) + ",";
    }

    /// <summary>Bild oder Video auf die zweite Spur legen – Standard: oben rechts, ein Viertel breit (Bild im Bild).</summary>
    public static Overlay AddOverlay(JsonObject p, string file, double start, double length)
    {
        var l = Overlays(p); var o = new Overlay(Guid.NewGuid().ToString("N")[..8], file, Math.Max(0, start), Math.Max(0.2, length), 0.68, 0.05, 0.28, 1);
        l.Add(o); StoreOverlays(p, l); return o;
    }

    public static void SetOverlay(JsonObject p, string id, double start, double length, double x, double y, double width, double opacity)
    {
        var l = Overlays(p); var i = l.FindIndex(o => o.Id == id); if (i < 0) return;
        l[i] = l[i] with { Start = Math.Max(0, start), Length = Math.Max(0.2, length), X = Math.Clamp(x, 0, 1), Y = Math.Clamp(y, 0, 1), Width = Math.Clamp(width, 0.05, 1), Opacity = Math.Clamp(opacity, 0.05, 1) };
        StoreOverlays(p, l);
    }

    public static void RemoveOverlay(JsonObject p, string id) => StoreOverlays(p, Overlays(p).Where(o => o.Id != id));

    /// <summary>Lage in festen Werten (Vorlagen wie in Schnittprogrammen).</summary>
    public static (double X, double Y) Corner(string corner, double width, double aspect = 9.0 / 16) => corner switch
    {
        "oben-links" => (0.03, 0.04), "unten-links" => (0.03, 0.96 - width * aspect * (16.0 / 9)), "unten-rechts" => (0.97 - width, 0.96 - width * aspect * (16.0 / 9)), "mitte" => ((1 - width) / 2, (1 - width * aspect * (16.0 / 9)) / 2),
        _ => (0.97 - width, 0.04),
    };

    static bool IsImage(string f) => Path.GetExtension(f).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg" or ".webp" or ".bmp" or ".gif";

    /// <summary>Zweite Bildspur in den Filtergraph legen: aus „vin“ wird „vout“. Liefert die zusätzlichen Eingaben.</summary>
    public static List<string> OverlayGraph(JsonObject p, List<string> graph, string vin, string vout, ref int input, int w, int h, int fps)
    {
        var args = new List<string>(); var cur = vin; var ov = Overlays(p);
        for (var i = 0; i < ov.Count; i++)
        {
            var o = ov[i];
            if (!File.Exists(o.File)) throw new FileNotFoundException("Datei fehlt: " + Path.GetFileName(o.File));
            if (IsImage(o.File)) args.AddRange(["-loop", "1", "-framerate", fps.ToString(Inv), "-t", F(o.Start + o.Length), "-i", o.File]);
            else args.AddRange(["-t", F(o.Length), "-i", o.File]);
            var idx = input++; var ow = Math.Max(16, (int)Math.Round(w * o.Width / 2) * 2);
            graph.Add($"[{idx}:v]{KeyAndMask(o)}scale={ow}:-2,format=rgba,colorchannelmixer=aa={F(o.Opacity)},setpts=PTS-STARTPTS+{F(IsImage(o.File) ? 0 : o.Start)}/TB[ov{i}]");
            var next = i == ov.Count - 1 ? vout : $"ovc{i}";
            graph.Add($"[{cur}][ov{i}]overlay=x={F(o.X)}*W:y={F(o.Y)}*H:enable='between(t,{F(o.Start)},{F(o.Start + o.Length)})':eof_action=pass[{next}]");
            cur = next;
        }
        if (ov.Count == 0) graph.Add($"[{vin}]null[{vout}]");
        return args;
    }
}
