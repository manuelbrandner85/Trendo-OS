using System.Text.Json.Nodes;

namespace Trendo.Core.Media;

/// <summary>Trendo Musik V2/V7 (07.11.2026): getrennte Spuren prüfen und mischen – Gesang, Schlagzeug, Bass, Rest. Die Summe der Spuren muss wieder das Original
/// ergeben (Rekonstruktion in dB); der Mischer mischt mit Pegel, Stumm und Solo je Spur, ohne das Original zu verändern.</summary>
public static class StemTools
{
    public static readonly (string Id, string Label)[] Stems = [("gesang", "Gesang"), ("schlagzeug", "Schlagzeug"), ("bass", "Bass"), ("gitarre", "Gitarre"), ("klavier", "Klavier"), ("rest", "Rest (Instrumente)")]; // Gitarre/Klavier nur mit 6-Spuren-Trennung (Suno V9)

    /// <summary>Vorhandene Spurdateien im Spuren-Ordner.</summary>
    public static Dictionary<string, string> Files(string dir) =>
        Stems.Select(s => (s.Id, Path.Combine(dir, s.Id + ".wav"))).Where(x => File.Exists(x.Item2)).ToDictionary(x => x.Id, x => x.Item2);

    /// <summary>Wie genau ergeben die Spuren zusammen wieder das Original? Verhältnis Original zu Rest-Unterschied in dB (über 20 dB = sauber getrennt, ohne Verlust).</summary>
    public static double Reconstruction(AudioBuf original, IReadOnlyList<AudioBuf> stems)
    {
        var n = Math.Min(original.Length, stems.Min(s => s.Length)); double sig = 0, err = 0;
        for (var c = 0; c < Math.Min(2, original.Channels); c++)
            for (var i = 0; i < n; i++)
            {
                double sum = 0; foreach (var s in stems) sum += s.Data[Math.Min(c, s.Channels - 1)][i];
                var o = original.Data[c][i]; sig += o * o; var d = o - sum; err += d * d;
            }
        return err <= 1e-12 ? 99 : Math.Round(10 * Math.Log10(sig / err), 1);
    }

    /// <summary>Wie stark spricht der Gesang in die Begleitung über? Korrelation der beiden (nahe 0 = sauber getrennt).</summary>
    public static double Bleed(AudioBuf vocal, AudioBuf accompaniment)
    {
        var n = Math.Min(vocal.Length, accompaniment.Length); double sv = 0, sa = 0, sva = 0;
        for (var i = 0; i < n; i++) { double v = vocal.Data[0][i], a = accompaniment.Data[0][i]; sv += v * v; sa += a * a; sva += v * a; }
        return sv < 1e-9 || sa < 1e-9 ? 0 : Math.Round(Math.Abs(sva / Math.Sqrt(sv * sa)), 3);
    }

    public sealed record Channel(double GainDb = 0, bool Mute = false, bool Solo = false);

    /// <summary>Suno V9 „Split from Mix“: eine Spur allein und die Summe aller anderen (zusammen wieder das Original).</summary>
    public static (AudioBuf Alone, AudioBuf Others) Isolate(IReadOnlyDictionary<string, string> files, string stemId)
    {
        if (!files.TryGetValue(stemId, out var f)) throw new InvalidOperationException("Diese Spur gibt es nicht.");
        var alone = AudioFile.Read(f); var ch = alone.Channels; var n = alone.Length;
        var rest = Enumerable.Range(0, ch).Select(_ => new float[n]).ToArray();
        foreach (var (id, file) in files)
        {
            if (id == stemId) continue;
            var b = AudioFile.Read(file);
            for (var c = 0; c < ch; c++) { var src = b.Data[Math.Min(c, b.Channels - 1)]; for (var i = 0; i < Math.Min(n, src.Length); i++) rest[c][i] += src[i]; }
        }
        return (alone, new AudioBuf(alone.Rate, rest));
    }

    /// <summary>Mischung der Spuren als Mehrspur-Projekt (für Abspielen, Speichern oder das Mischpult).</summary>
    public static JsonObject Project(IReadOnlyDictionary<string, string> files, IReadOnlyDictionary<string, Channel> mix)
    {
        var doc = AudioMulti.New();
        foreach (var (id, label) in Stems)
        {
            if (!files.TryGetValue(id, out var f)) continue;
            var t = AudioMulti.AddTrack(doc, f, 0, label);
            var ch = mix.TryGetValue(id, out var c) ? c : new Channel();
            AudioMulti.SetTrack(doc, t.Id, gainDb: ch.GainDb, mute: ch.Mute, solo: ch.Solo);
        }
        return doc;
    }
}
