using System.Globalization;
using System.Text.Json;

namespace Trendo.Core;

/// <summary>KI-Musikvideo, Baustein C und G (freigegeben 04.11.2026): echte Filmszenen mit freien Video-KIs über stable-diffusion.cpp.
/// C1 Wan 2.2 TI2V-5B (Szene aus Text und Figurenbild), C2 Wan 2.2 S2V-14B (Figur singt zur Gesangsspur), dazu ein Figurenbild je Figur (B3),
/// damit die Person in jeder Einstellung gleich aussieht. G: jede Einstellung wird geprüft (trendo_pruefung.py) und bei Fehlern bis zu
/// <see cref="MaxTries"/>-mal neu erzeugt – mit neuem Zufallswert, strengerer Beschreibung und ruhigerer Kamera; danach rot markiert, nie still übernommen.</summary>
public static class MusicVideoRender
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    public const int MaxTries = 3;

    /// <summary>Qualität: Vorschau (F1, klein und schnell) oder Endfassung.</summary>
    public sealed record Quality(string Id, int Width, int Height, int Steps, int Fps);
    public static readonly Quality Preview = new("vorschau", 480, 272, 12, 12);
    public static readonly Quality Final = new("final", 1280, 704, 30, 24);
    public static readonly Quality SingFinal = new("final", 832, 480, 30, 16); // S2V: 480p, 16 Bilder/s (wie das Modell trainiert ist)
    public static readonly Quality SingPreview = new("vorschau", 448, 256, 12, 16);

    public sealed record SceneModels(string Diffusion, string Vae, string TextEncoder);
    public sealed record SingModels(string Diffusion, string Vae, string TextEncoder, string AudioEncoder);

    static string N(double v) => v.ToString("0.###", Inv);
    // Befund 04.11.2026 (RTX 5070, Vulkan): das Dekodieren des Videos brach auf der Grafikkarte mit „DeviceLost“ ab (Zeitlimit des Treibers) –
    // auf dem Prozessor dauerte es 48 min für 2 s. Lösung (gemessen): kleine Kacheln räumlich (160 px) und zeitlich (2 Bilder) – jede Rechnung
    // bleibt unter dem Zeitlimit; 2 s Video in rund 11 min (Bilder 72 s, Dekodieren ~10 min) auf der RTX 5070.

    /// <summary>Bilder einer Einstellung: Wan braucht 4·k+1 Bilder.</summary>
    public static int Frames(double seconds, int fps) { var n = (int)Math.Round(seconds * fps); return Math.Max(5, n / 4 * 4 + 1); }

    /// <summary>B3: Figurenblatt – neutrales, gut ausgeleuchtetes Ganzkörper-Bild der erfundenen Figur (Bezug für jede Einstellung).</summary>
    /// <summary>Befund 04.11.2026 (erster echter Lauf): ein Hochkant-Porträt als Startbild wurde im Querformat mitten durch die Augen beschnitten –
    /// darum Figurenblatt im Format des Videos, von der Hüfte aufwärts, Kopf ganz im Bild.</summary>
    public static string FigurePrompt(MusicScript.Figure f, string style) =>
        $"photorealistic photo of {f.Look}, medium shot from the waist up, centered, whole head and hair fully in frame with space above, facing the camera, neutral relaxed expression, soft even studio light, plain light grey background, sharp focus, natural skin texture, correct anatomy, {style.Trim().TrimEnd(',')} artist look";

    /// <summary>Startbild einer Szene (wie ein Storyboard-Bild): Figur am richtigen Ort, in Einstellungsgröße und Licht des Drehbuchs.
    /// Befund 04.11.2026: Wan 2.2 folgt dem Startbild sehr eng – mit dem Studio-Porträt als Start fehlte der Drehort ganz.</summary>
    public static string KeyframePrompt(MusicScript.Shot s, string shotPrompt, int people = 1) =>
        shotPrompt.Replace("cinematic music video, ", "cinematic film still from a music video, " + (people == 1 ? "a single person alone, " : people == 2 ? "exactly two people, " : ""))
        + ", single frame, no motion blur";

    /// <summary>Ausschluss fürs Startbild: bei einer Person zusätzlich „zwei Menschen, Doppelgänger“ (gemessen: SDXL Turbo verdoppelte die Figur).</summary>
    public static string KeyframeNegative(int people) => (people == 1 ? "two people, duplicate person, twins, crowd, " : "") + MusicScript.Negative;

    /// <summary>Bildgröße fürs Startbild. Gemessen 04.11.2026: SDXL Turbo verdoppelt Personen bei 1344 × 768 – bei 768 × 448 (nahe der
    /// Trainingsgröße) bleibt es eine Person. Das Video skaliert ohnehin auf 832 × 480.</summary>
    public static (int W, int H) KeyframeSize(bool portrait) => portrait ? (448, 768) : (768, 448);

    /// <summary>C1: Szene (Text + optional Figurenbild als Startbild).</summary>
    public static List<string> SceneArgs(SceneModels m, MusicScript.Shot s, string prompt, string? figureImage, string output, Quality q, int seed)
    {
        var a = new List<string> { "-M", "vid_gen", "--diffusion-model", m.Diffusion, "--vae", m.Vae, "--t5xxl", m.TextEncoder,
            "-p", prompt, "-n", MusicScript.Negative, "-W", q.Width.ToString(Inv), "-H", q.Height.ToString(Inv),
            "--video-frames", Frames(s.End - s.Start, q.Fps).ToString(Inv), "--fps", q.Fps.ToString(Inv), "--steps", q.Steps.ToString(Inv),
            "--cfg-scale", "5.0", "--flow-shift", "5.0", "--sampling-method", "euler", "-s", seed.ToString(Inv),
            "--offload-to-cpu", "--vae-tiling", "--vae-tile-size", "160", "--temporal-tiling", "--extra-tiling-args", "temporal_tile_frames=2", "-o", output };
        if (figureImage is not null) { a.Add("-i"); a.Add(figureImage); }
        return a;
    }

    /// <summary>C2: singende Figur – Figurenbild + Gesangsausschnitt; Mund, Mimik und Körper folgen der Stimme.</summary>
    public static List<string> SingArgs(SingModels m, MusicScript.Shot s, string prompt, string figureImage, string vocalClip, string output, Quality q, int seed) =>
    [
        "-M", "vid_gen", "--diffusion-model", m.Diffusion, "--vae", m.Vae, "--t5xxl", m.TextEncoder, "--audio-encoder", m.AudioEncoder,
        "--audio", vocalClip, "-i", figureImage, "-p", prompt, "-n", MusicScript.Negative,
        "-W", q.Width.ToString(Inv), "-H", q.Height.ToString(Inv), "--video-frames", Frames(s.End - s.Start, q.Fps).ToString(Inv), "--fps", q.Fps.ToString(Inv),
        "--steps", q.Steps.ToString(Inv), "--cfg-scale", "4.5", "--flow-shift", "3.0", "--sampling-method", "euler", "-s", seed.ToString(Inv),
        "--offload-to-cpu", "--vae-tiling", "--vae-tile-size", "160", "--temporal-tiling", "--extra-tiling-args", "temporal_tile_frames=2", "-o", output,
    ];

    /// <summary>Gesangsausschnitt für eine Einstellung (16 kHz mono WAV, wie wav2vec2 ihn erwartet).</summary>
    public static string[] VocalClipArgs(string vocal, MusicScript.Shot s, string wav) =>
        ["-hide_banner", "-loglevel", "error", "-y", "-ss", N(s.Start), "-t", N(s.End - s.Start), "-i", vocal, "-ar", "16000", "-ac", "1", "-c:a", "pcm_s16le", wav];

    /// <summary>Clip auf die genaue Länge, Bildgröße und Bildrate des Projekts bringen (ohne Ton – der Ton kommt vom Lied).</summary>
    public static string[] NormalizeArgs(string input, double seconds, int w, int h, int fps, string output) =>
        ["-hide_banner", "-loglevel", "error", "-y", "-i", input, "-t", N(seconds), "-an",
         "-vf", $"scale={w}:{h}:force_original_aspect_ratio=increase,crop={w}:{h},fps={fps},tpad=stop_mode=clone:stop_duration={N(seconds)}",
         "-c:v", "libx264", "-preset", "medium", "-crf", "17", "-pix_fmt", "yuv420p", output];

    // ───────────── G: Prüfen und verbessern ─────────────
    public sealed record Issue(string Code, string Text, double At);
    public sealed record Check(bool Ok, IReadOnlyList<Issue> Issues);

    public static Check ParseCheck(string json)
    {
        using var d = JsonDocument.Parse(json); var r = d.RootElement;
        var issues = r.GetProperty("issues").EnumerateArray().Select(x => new Issue(x.GetProperty("code").GetString() ?? "", x.GetProperty("text").GetString() ?? "",
            x.TryGetProperty("at", out var a) && a.ValueKind == JsonValueKind.Number ? a.GetDouble() : 0)).ToList();
        return new Check(r.GetProperty("ok").GetBoolean() && issues.Count == 0, issues);
    }

    /// <summary>G2: Frage an die freie Bildverstehens-KI (Qwen2.5-VL) zu drei Standbildern einer Einstellung.</summary>
    public static string VisionQuestion(int people) =>
        $"These are three still frames from one AI-generated video shot. Expected number of people: {people}. " +
        "Check carefully for generation errors: extra or missing arms, hands or fingers, fused or deformed fingers, twisted limbs, two heads, merged or distorted faces, " +
        "body parts floating or disconnected, people appearing or disappearing, garbled text. " +
        "Answer with exactly one line: OK – or – ERROR: <short reason in German>.";

    /// <summary>Antwort der Bild-KI lesen: „OK“ = in Ordnung, „ERROR: …“ = Fehler mit Grund; Unklares zählt nicht als Fehler.</summary>
    public static Issue? ParseVision(string? answer)
    {
        if (string.IsNullOrWhiteSpace(answer)) return null;
        var a = answer.Trim();
        var i = a.IndexOf("ERROR", StringComparison.OrdinalIgnoreCase);
        if (i < 0 || (a.StartsWith("OK", StringComparison.OrdinalIgnoreCase) && i > 2)) return null;
        var why = a[(i + 5)..].TrimStart(':', ' ', '-', '–').Split('\n')[0].Trim();
        return new Issue("bild-ki", "Bild-KI sieht einen Fehler: " + (why.Length > 0 ? why : "Fehlbildung"), 0);
    }

    /// <summary>Wie viele Personen das Prüfprogramm erwarten soll.</summary>
    public static int ExpectedPeople(MusicScript.Shot s, IReadOnlyList<MusicScript.Figure> figs) =>
        s.Who == "beide" ? Math.Min(2, figs.Count) : figs.Any(f => f.Role == s.Who || s.Who.StartsWith(f.Role)) ? 1 : s.Singing ? 1 : 0;

    /// <summary>G6: nächster Versuch nach einem Fehler – neuer Zufall, strengere Beschreibung, ruhigere Kamera, Hände aus dem Bild.</summary>
    public static (MusicScript.Shot Shot, string Prompt) Retry(MusicScript.Shot s, string prompt, Check failed, int attempt)
    {
        var p = prompt;
        var codes = failed.Issues.Select(i => i.Code).ToHashSet();
        if (codes.Contains("haende") || codes.Contains("gesichter") || codes.Contains("personen"))
            p += ", exactly " + (s.Who == "beide" ? "two people" : "one person") + ", anatomically correct, two arms and two hands per person, hands relaxed and partly out of frame";
        if (codes.Contains("person")) p += ", same person as in the reference image, identical face and hairstyle";
        if (codes.Contains("bildsprung") || codes.Contains("gesicht-flackert")) p += ", smooth continuous motion, stable lighting, no cuts";
        if (codes.Contains("lippen")) p += ", face clearly visible, mouth clearly articulating the words";
        var move = codes.Overlaps(["bildsprung", "haende", "gesicht-flackert"]) ? "statisch" : s.Move;
        var size = codes.Contains("haende") && s.Size is "Halbtotale" or "Totale" or "Halbnah" ? "Nah" : s.Size;
        return (s with { Seed = s.Seed + 7919 * attempt, Move = move, Size = size }, p);
    }

    /// <summary>G8: Prüfbericht einer Einstellung in einem Satz.</summary>
    public static string Report(IReadOnlyList<Check> tries)
    {
        if (tries.Count == 0) return "nicht geprüft";
        var last = tries[^1];
        if (last.Ok) return tries.Count == 1 ? "✓ geprüft, in Ordnung" : $"✓ in Ordnung nach {tries.Count} Versuchen (vorher: {string.Join("; ", tries[0].Issues.Select(i => i.Text))})";
        return $"✗ nach {tries.Count} Versuchen noch fehlerhaft: {string.Join("; ", last.Issues.Select(i => i.Text))}";
    }
}
