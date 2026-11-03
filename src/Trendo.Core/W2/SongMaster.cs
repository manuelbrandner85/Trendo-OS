using System.Globalization;
using System.Text.Json;

namespace Trendo.Core;

/// <summary>Trendo Musik – Mastering wie für Streamingdienste (Wunsch 03.11.2026 „hochprofessionell, nicht nach KI“): Rumpelfilter, leichter
/// Bus-Kompressor, Lautheit genau −14 LUFS und Spitzen höchstens −1 dBTP (EBU R128, zweistufig mit FFmpeg „loudnorm“).
/// Am 03.11.2026 echt geprüft: ein ACE-Step-Lied (−13,4 LUFS, −0,7 dBTP, LRA 15,7) kam auf −14,0 LUFS, −1,0 dBTP, LRA 10,7 – in 6 s für 3:26 Minuten.</summary>
public static class SongMaster
{
    public const double TargetLufs = -14, TruePeak = -1.2, Lra = 11;
    public const string Pre = "highpass=f=28:poles=2,acompressor=threshold=-20dB:ratio=1.6:attack=25:release=250:makeup=1";
    static string Norm => FormattableString.Invariant($"loudnorm=I={TargetLufs}:TP={TruePeak}:LRA={Lra}");

    /// <summary>Messlauf: FFmpeg schreibt die Messwerte als JSON auf stderr.</summary>
    public static string[] MeasureArgs(string input) => ["-hide_banner", "-nostats", "-i", input, "-af", Pre + "," + Norm + ":print_format=json", "-f", "null", "-"];

    public sealed record Measured(string I, string Tp, string Lra, string Thresh, string Offset);

    /// <summary>Messwerte aus der FFmpeg-Ausgabe (letzter JSON-Block) – oder null.</summary>
    public static Measured? ParseMeasure(string stderr)
    {
        var a = stderr.LastIndexOf('{'); var b = stderr.LastIndexOf('}');
        if (a < 0 || b < a) return null;
        try
        {
            using var d = JsonDocument.Parse(stderr[a..(b + 1)]);
            string G(string n) { var v = d.RootElement.GetProperty(n).GetString() ?? ""; return double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var x) && double.IsFinite(x) ? v : throw new FormatException(n); }
            return new(G("input_i"), G("input_tp"), G("input_lra"), G("input_thresh"), G("target_offset"));
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException or FormatException) { return null; }
    }

    /// <summary>Zweiter Lauf: mit den Messwerten linear auf das Ziel, Format nach der Endung (MP3 320 kbit/s, WAV 24 bit, FLAC).</summary>
    public static string[] ApplyArgs(string input, string output, Measured m)
    {
        var codec = Path.GetExtension(output).ToLowerInvariant() switch
        {
            ".wav" => new[] { "-c:a", "pcm_s24le" }, ".flac" => ["-c:a", "flac"], _ => ["-c:a", "libmp3lame", "-b:a", "320k"],
        };
        var af = $"{Pre},{Norm}:measured_I={m.I}:measured_TP={m.Tp}:measured_LRA={m.Lra}:measured_thresh={m.Thresh}:offset={m.Offset}:linear=true,aresample=48000";
        return ["-hide_banner", "-nostats", "-y", "-i", input, "-af", af, .. codec, output];
    }
}
