using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Trendo.Core.Media;

/// <summary>Paket 01 – Studio 3, S3-13 (07.11.2026): KI-Video im Videoschnitt – Clip aus Beschreibung (und optional Startbild) mit einem lokal
/// eingerichteten freien Modell (Wan 2.2 TI2V-5B über stable-diffusion.cpp, Apache-2.0). Jeder Clip bekommt einen Herkunftsnachweis
/// (Beschreibung, Ausschluss, Zufallswert, Modell mit Lizenz und Dateien, Programm, Prüfsumme). Ohne Modell oder ohne Ergebnis gibt es nur
/// einen erklärten Fehler – Trendo erzeugt nie Ersatzbilder, die wie KI-Ergebnis aussehen.</summary>
public static class GenVideo
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    public const string Negative = "blurry, low quality, distorted, deformed hands, extra fingers, watermark, text, logo, flicker, jpeg artifacts";

    /// <summary>Qualität: Vorschau oder Endfassung (Wan 2.2 TI2V-5B: Kanten Vielfache von 32, 24 Bilder/s).
    /// Gemessen 07.11.2026 (RTX 5070, Seed 4711, 1 s): unter 1280 × 704 entstehen verschmierte, zerfallende Bilder (Modell ist auf 704p trainiert) –
    /// darum auch die Vorschau in voller Größe, nur mit 12 statt 30 Rechenschritten (Rechnen 43 s statt 120 s, Bild bereits glaubwürdig).</summary>
    public sealed record Quality(string Id, string Label, int Width, int Height, int Steps, int Fps);
    public static readonly Quality Preview = new("vorschau", "Vorschau (12 Schritte)", 1280, 704, 12, 24);
    public static readonly Quality Final = new("final", "Endfassung (30 Schritte)", 1280, 704, 30, 24);
    public static readonly IReadOnlyList<Quality> Qualities = [Preview, Final];

    public sealed record Request(string Prompt, long Seed, double Seconds, Quality Quality, string? StartImage = null, string? NegativePrompt = null);
    public sealed record Model(string Id, string Name, string License, string Diffusion, string TextEncoder, string Vae);

    /// <summary>Wan braucht 4·k+1 Bilder.</summary>
    public static int Frames(double seconds, int fps) { var n = (int)Math.Round(seconds * fps); return Math.Max(5, n / 4 * 4 + 1); }

    /// <summary>Neuer Zufallswert (0 … 2³²−1, wie stable-diffusion.cpp ihn nimmt).</summary>
    public static long NewSeed() => RandomNumberGenerator.GetInt32(int.MaxValue) * 2L + RandomNumberGenerator.GetInt32(2);

    /// <summary>Prüfung vor dem Start – null = in Ordnung.</summary>
    public static string? Problem(Request r, Model? m)
    {
        if (m is null) return "Kein Video-Modell eingerichtet – in der KI-Bibliothek unter „Videos erzeugen“ „Wan 2.2 – Filmszenen“ laden. Ohne Modell erzeugt Trendo keine Bilder.";
        if (string.IsNullOrWhiteSpace(r.Prompt)) return "Bitte beschreiben, was im Clip zu sehen sein soll.";
        if (r.Prompt.Length > 1500) return "Die Beschreibung ist zu lang (höchstens 1500 Zeichen).";
        if (r.Seed is < 0 or > uint.MaxValue) return "Der Zufallswert muss zwischen 0 und 4294967295 liegen.";
        if (r.Seconds is < 0.5 or > 5) return "Ein KI-Clip ist 0,5 bis 5 Sekunden lang.";
        if (r.Quality.Width % 32 != 0 || r.Quality.Height % 32 != 0) return "Bildgröße muss ein Vielfaches von 32 sein.";
        if (r.StartImage is { } s && !File.Exists(s)) return "Das Startbild fehlt: " + Path.GetFileName(s);
        foreach (var f in new[] { m.Diffusion, m.TextEncoder, m.Vae })
            if (!File.Exists(f)) return "Modelldatei fehlt: " + Path.GetFileName(f) + " – in der KI-Bibliothek neu laden.";
        return null;
    }

    /// <summary>Aufruf für sd-cli (stable-diffusion.cpp, „vid_gen“). Kacheln wie im KI-Musikvideo gemessen: hält das Dekodieren unter dem Zeitlimit des Treibers.</summary>
    public static List<string> Args(Request r, Model m, string output)
    {
        var q = r.Quality;
        var a = new List<string> { "-M", "vid_gen", "--diffusion-model", m.Diffusion, "--vae", m.Vae, "--t5xxl", m.TextEncoder,
            "-p", r.Prompt.Trim(), "-n", r.NegativePrompt ?? Negative, "-W", q.Width.ToString(Inv), "-H", q.Height.ToString(Inv),
            "--video-frames", Frames(r.Seconds, q.Fps).ToString(Inv), "--fps", q.Fps.ToString(Inv), "--steps", q.Steps.ToString(Inv),
            "--cfg-scale", "5.0", "--flow-shift", "5.0", "--sampling-method", "euler", "-s", r.Seed.ToString(Inv),
            "--offload-to-cpu", "--vae-tiling", "--vae-tile-size", "160", "--temporal-tiling", "--extra-tiling-args", "temporal_tile_frames=2", "-o", output };
        if (r.StartImage is { } s) { a.Add("-i"); a.Add(s); }
        return a;
    }

    public static string Sha256(string file) { using var f = File.OpenRead(file); return Convert.ToHexStringLower(SHA256.HashData(f)); }

    /// <summary>Herkunftsnachweis neben dem Clip („…​.herkunft.json“). Nur Dateinamen, keine Pfade (der Nachweis darf weitergegeben werden).</summary>
    public static JsonObject Provenance(Request r, Model m, string tool, string toolVersion, string clip, string? rawOutput, DateTime createdUtc)
    {
        var q = r.Quality;
        JsonObject F(string p) => new() { ["datei"] = Path.GetFileName(p), ["bytes"] = File.Exists(p) ? new FileInfo(p).Length : 0 };
        return new JsonObject
        {
            ["art"] = "KI-erzeugtes Video",
            ["hinweis"] = "Dieser Clip wurde vollständig von einer KI erzeugt und zeigt keine echte Aufnahme.",
            ["erstellt"] = createdUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", Inv),
            ["beschreibung"] = r.Prompt.Trim(),
            ["ausschluss"] = r.NegativePrompt ?? Negative,
            ["zufallswert"] = r.Seed,
            ["startbild"] = r.StartImage is { } s ? new JsonObject { ["datei"] = Path.GetFileName(s), ["sha256"] = Sha256(s) } : null,
            ["qualitaet"] = q.Id, ["breite"] = q.Width, ["hoehe"] = q.Height, ["bilder"] = Frames(r.Seconds, q.Fps), ["bilder_pro_s"] = q.Fps, ["schritte"] = q.Steps, ["sekunden"] = r.Seconds,
            ["modell"] = new JsonObject { ["id"] = m.Id, ["name"] = m.Name, ["lizenz"] = m.License, ["dateien"] = new JsonArray(F(m.Diffusion), F(m.TextEncoder), F(m.Vae)) },
            ["programm"] = new JsonObject { ["name"] = tool, ["fassung"] = toolVersion },
            ["roh_sha256"] = rawOutput is not null && File.Exists(rawOutput) ? Sha256(rawOutput) : null,
            ["clip"] = Path.GetFileName(clip),
            ["clip_sha256"] = Sha256(clip),
        };
    }

    public static string ProvenancePath(string clip) => Path.ChangeExtension(clip, null) + ".herkunft.json";

    public static void WriteProvenance(string clip, JsonObject p) =>
        File.WriteAllText(ProvenancePath(clip), p.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));

    /// <summary>Stimmt der Clip noch mit seinem Nachweis überein? (Prüfsumme) – null = ja, sonst der Grund.</summary>
    public static string? VerifyProvenance(string clip)
    {
        var pf = ProvenancePath(clip);
        if (!File.Exists(pf)) return "Kein Herkunftsnachweis gefunden.";
        try
        {
            var j = JsonNode.Parse(File.ReadAllText(pf))!;
            return (string?)j["clip_sha256"] == Sha256(clip) ? null : "Der Clip wurde nach dem Erzeugen verändert (Prüfsumme passt nicht zum Nachweis).";
        }
        catch (JsonException) { return "Der Herkunftsnachweis ist beschädigt."; }
    }

    /// <summary>Clip ins Projektformat (genaue Länge, Größe, Bildrate, ohne Ton).</summary>
    public static string[] NormalizeArgs(string input, double seconds, int w, int h, int fps, string output) =>
        ["-hide_banner", "-loglevel", "error", "-y", "-i", input, "-t", seconds.ToString("0.###", Inv), "-an",
         "-vf", $"scale={w}:{h}:force_original_aspect_ratio=increase,crop={w}:{h},fps={fps},tpad=stop_mode=clone:stop_duration={seconds.ToString("0.###", Inv)}",
         "-c:v", "libx264", "-preset", "medium", "-crf", "17", "-pix_fmt", "yuv420p", output];
}
