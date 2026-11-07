using System.Globalization;
using Trendo.Core.Media;

namespace Trendo.Core;

/// <summary>Trendo Musik V10 wie Suno (07.11.2026): Mashup – Gesang von Lied A über die Begleitung von Lied B, in Tempo und Tonart angeglichen
/// (Rubber Band über <see cref="TimePitch"/>; zu starke Änderung wird abgelehnt statt still verbogen). Sample – ein Ausschnitt wird Klangvorbild für ein neues Lied.</summary>
public static class MusicMashup
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static readonly string[] Notes = ["C", "C#", "D", "Eb", "E", "F", "F#", "G", "Ab", "A", "Bb", "B"];

    /// <summary>Tonart „A minor“ → Tonklasse der parallelen Dur-Tonart (A-Moll → C), damit Dur und Moll zusammenpassen; null = unbekannt.</summary>
    public static int? MajorPc(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        var p = key.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var pc = Array.IndexOf(Notes, p[0] switch { "Db" => "C#", "D#" => "Eb", "Gb" => "F#", "G#" => "Ab", "A#" => "Bb", var x => x });
        if (pc < 0) return null;
        return p.Length > 1 && p[1].StartsWith("min", StringComparison.OrdinalIgnoreCase) ? (pc + 3) % 12 : pc;
    }

    public sealed record Plan(double Tempo, int Semitones, string Light, string Text);

    /// <summary>Wie muss der Gesang (A) geändert werden, damit er auf die Begleitung (B) passt? Tempo = B/A (halb/doppelt, wenn näher an 1),
    /// Tonart: kürzester Weg zur Tonklasse von B (−6…+5 Halbtöne). Ampel wie bei Tempo/Tonhöhe.</summary>
    public static Plan Match(double bpmVocal, string? keyVocal, double bpmBacking, string? keyBacking)
    {
        if (bpmVocal <= 0 || bpmBacking <= 0) throw new ArgumentException("Für ein Mashup braucht es das Tempo beider Lieder (erst „Analysieren“).");
        var t = bpmBacking / bpmVocal;
        foreach (var f in new[] { 2.0, 0.5 }) if (Math.Abs(Math.Log(t * f)) < Math.Abs(Math.Log(t))) t *= f;
        var a = MajorPc(keyVocal); var b = MajorPc(keyBacking);
        var semis = a is int x && b is int y ? ((y - x) % 12 + 18) % 12 - 6 : 0;
        var c = TimePitch.Assess(t, semis);
        var text = $"Gesang {t * 100:0} % Tempo, {semis:+0;-0;0} Halbtöne" + (a is null || b is null ? " (Tonart unbekannt – nur Tempo angeglichen)" : "") + " – " + c.Text;
        return new(Math.Round(t, 4), semis, c.Light, text);
    }

    /// <summary>FFmpeg: angeglichenen Gesang (A) und Begleitung (B) mischen, Länge der Begleitung, Spitze begrenzt.</summary>
    public static List<string> MixArgs(string vocal, string backing, string output, double vocalGainDb = 0) =>
        ["-hide_banner", "-loglevel", "error", "-y", "-i", backing, "-i", vocal, "-filter_complex",
         $"[1:a]aformat=sample_rates=44100:channel_layouts=stereo,volume={Math.Pow(10, vocalGainDb / 20).ToString("0.###", Inv)}[v];[0:a]aformat=sample_rates=44100:channel_layouts=stereo[b];[b][v]amix=inputs=2:duration=first:normalize=0,alimiter=limit=0.89[o]",
         "-map", "[o]", "-c:a", "pcm_s16le", output];

    /// <summary>V10 Sample: Ausschnitt prüfen (0,5–30 s, im Lied).</summary>
    public static string? SampleProblem(double from, double to, double songSeconds)
    {
        if (to - from < 0.5) return "Der Ausschnitt muss mindestens eine halbe Sekunde lang sein.";
        if (to - from > 30) return "Ein Sample ist höchstens 30 Sekunden lang.";
        if (from < 0 || to > songSeconds + 0.5) return $"Der Ausschnitt liegt außerhalb des Lieds (0–{songSeconds:0} s).";
        return null;
    }
}
