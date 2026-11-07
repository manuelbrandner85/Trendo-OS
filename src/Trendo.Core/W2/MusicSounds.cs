using System.Globalization;
using Trendo.Core.Media;

namespace Trendo.Core;

/// <summary>Trendo Musik V7/V8 wie Suno (07.11.2026): „Sounds“ – Einzelklang (One-Shot) oder Loop mit Tonart und Tempo aus einem kurzen,
/// lokal erzeugten Instrumentalstück; „Sprache“ – gesprochener Text (Piper) mit passender Musik darunter, die beim Sprechen leiser wird.</summary>
public static class MusicSounds
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Hüllkurve: RMS je 10 ms über alle Kanäle.</summary>
    static double[] Env(AudioBuf a)
    {
        var step = Math.Max(1, a.Rate / 100); var n = a.Length / step; var e = new double[n];
        for (var i = 0; i < n; i++) { double s = 0; foreach (var c in a.Data) for (var k = 0; k < step; k++) { var v = c[i * step + k]; s += v * v; } e[i] = Math.Sqrt(s / (step * a.Data.Length)); }
        return e;
    }

    /// <summary>One-Shot: stärkster Einsatz in den ersten 70 %, ab 10 ms davor bis der Klang unter −50 dB (bezogen auf die Spitze) abgeklungen ist
    /// (höchstens <paramref name="maxSeconds"/>), 30 ms Ausblende, Spitze auf −1 dBFS. Ohne deutlichen Einsatz: Fehler statt Stille.</summary>
    public static AudioBuf OneShot(AudioBuf a, double maxSeconds = 4)
    {
        var e = Env(a); if (e.Length < 10) throw new InvalidOperationException("Das Stück ist zu kurz für einen Einzelklang.");
        var peak = e.Max(); if (peak < 1e-4) throw new InvalidOperationException("Im Stück ist nichts zu hören.");
        int onset = -1; double best = 0;
        for (var i = 1; i < (int)(e.Length * 0.7); i++) { var jump = e[i] - e[i - 1] * 1.0; if (jump > best && e[i] > peak * 0.25) { best = jump; onset = i; } }
        if (onset < 0) throw new InvalidOperationException("Kein deutlicher Einsatz gefunden – bitte die Beschreibung schärfer machen (z. B. „einzelner Schlag“).");
        var startS = Math.Max(0, (onset - 1) * a.Rate / 100);
        var localPeak = e.Skip(onset).Take(50).Max(); var end = onset;
        for (var i = onset; i < Math.Min(e.Length, onset + (int)(maxSeconds * 100)); i++)
        {
            end = i;
            if (i > onset + 5 && e.Skip(i).Take(10).All(v => v < localPeak * 0.00316)) break; // −50 dB, 100 ms lang
        }
        var endS = Math.Min(a.Length, (end + 1) * a.Rate / 100); var len = Math.Max(1, endS - startS);
        var data = a.Data.Select(c => c[startS..(startS + len)]).ToArray();
        var fade = Math.Min(len, a.Rate * 3 / 100);
        foreach (var c in data) for (var i = 0; i < fade; i++) c[len - 1 - i] *= i / (float)fade;
        var p = data.Max(c => c.Length == 0 ? 0 : c.Max(Math.Abs)); var g = p > 0 ? (float)(0.891 / p) : 1f;
        foreach (var c in data) for (var i = 0; i < c.Length; i++) c[i] *= g;
        return new AudioBuf(a.Rate, data);
    }

    /// <summary>Länge von <paramref name="bars"/> Takten in Sekunden.</summary>
    public static double BarsSeconds(double bpm, int bars, int beatsPerBar = 4) => bars * beatsPerBar * 60.0 / Math.Clamp(bpm, 30, 300);

    /// <summary>Loop: Takte im erkannten Tempo (gemessen, falls die Musik-KI vom Wunsch abweicht – nachgemessen bis 2,5 %), ab dem ersten Schlag,
    /// nahtlos überblendet (<see cref="MusicPro.MakeLoop"/>).</summary>
    public static (AudioBuf Loop, double Bpm) Loop(AudioBuf a, double wantBpm, int bars, int beatsPerBar = 4)
    {
        var found = BeatDetect.Find(a);
        var bpm = found.Bpm > 0 && Math.Abs(found.Bpm - wantBpm) / wantBpm < 0.08 ? found.Bpm : wantBpm; // halbe/doppelte Erkennung verwerfen
        var e = Env(a); var first = 0; var peak = e.Max();
        for (var i = 0; i < e.Length; i++) if (e[i] > peak * 0.3) { first = i; break; }
        var from = first * a.Rate / 100;
        var tail = new AudioBuf(a.Rate, a.Data.Select(c => c[from..]).ToArray());
        return (MusicPro.MakeLoop(tail, bpm, beatsPerBar, bars), bpm);
    }

    /// <summary>Beschreibung für die Musik-KI (englische Fachwörter versteht sie am besten).</summary>
    public static string SoundPrompt(string description, bool loop, int bpm, string? key) =>
        loop ? $"{description.Trim()}, seamless loop, {bpm} bpm" + (key is { Length: > 0 } ? $", {key}" : "") + ", instrumental, no vocals"
             : $"{description.Trim()}, single isolated sound effect, one shot, short, dry, instrumental, no vocals, no music";

    public static string SpeechMusicPrompt(string style) => $"{(style.Trim().Length > 0 ? style.Trim() : "calm ambient background music")}, instrumental, background music for spoken word, no vocals, soft, steady";

    /// <summary>FFmpeg: Sprache nach <paramref name="intro"/> Sekunden über die Musik; die Musik wird beim Sprechen abgesenkt (Sidechain), danach Ausklang und Ausblende.</summary>
    public static List<string> SpeechMixArgs(string voice, string music, string output, double voiceSeconds, double intro = 2.0, double outro = 3.0)
    {
        string F(double v) => v.ToString("0.###", Inv);
        var total = intro + voiceSeconds + outro; var delayMs = (int)(intro * 1000);
        return ["-hide_banner", "-loglevel", "error", "-y", "-i", voice, "-i", music, "-filter_complex",
            $"[0:a]aformat=sample_rates=44100:channel_layouts=stereo,adelay={delayMs}|{delayMs},apad,atrim=0:{F(total)},asplit=2[v][sc];" +
            $"[1:a]aformat=sample_rates=44100:channel_layouts=stereo,volume=0.55,atrim=0:{F(total)},afade=t=out:st={F(total - 2)}:d=2[m];" +
            "[m][sc]sidechaincompress=threshold=0.03:ratio=8:attack=25:release=600[duck];" +
            "[duck][v]amix=inputs=2:duration=first:normalize=0,alimiter=limit=0.89[o]",
            "-map", "[o]", "-ar", "44100", "-c:a", "pcm_s16le", output];
    }
}
