using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>Trendo Musik M5: Ergebnis der Song-Analyse (siehe Assets/ki/trendo_analyse.py).</summary>
public sealed record SongAnalysis(
    [property: JsonPropertyName("duration")] double Duration,
    [property: JsonPropertyName("bpm")] double Bpm,
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("key_confidence")] double KeyConfidence,
    [property: JsonPropertyName("meter")] string Meter,
    [property: JsonPropertyName("chords")] List<string> Chords,
    [property: JsonPropertyName("range")] SongRange? Range)
{
    public static SongAnalysis? Parse(string json)
    {
        try { return JsonSerializer.Deserialize<SongAnalysis>(json); } catch (JsonException) { return null; }
    }

    /// <summary>Tonart deutsch, z. B. „A-Moll“.</summary>
    public string KeyDe => SongTools.KeyDe(Key);
    /// <summary>Takt als ACE-Step-Wert („3“ oder „4“).</summary>
    public string MeterValue => Meter.Split('/')[0];
}

public sealed record SongRange([property: JsonPropertyName("low")] string Low, [property: JsonPropertyName("high")] string High,
    [property: JsonPropertyName("semitones")] int Semitones, [property: JsonPropertyName("voice")] string Voice);

/// <summary>Trendo Musik M4/M7/M9: Liedtext gliedern, Silben zählen, Reime finden, Refrain wiederholen; Tonart/Tempo bekannter Songs aus freien Daten.</summary>
public static partial class SongTools
{
    public static string KeyDe(string k) => k.Replace(" major", "-Dur").Replace(" minor", "-Moll").Replace("Eb", "Es").Replace("Ab", "As").Replace("Bb", "B").Replace("C#", "Cis").Replace("F#", "Fis");

    // ───────────── M4: eingefügten Text gliedern ─────────────
    /// <summary>Text ohne Abschnitts-Marken in [Strophe]/[Refrain]/[Bridge] gliedern: Absätze (Leerzeile) sind Abschnitte; ein Absatz, der mehrfach vorkommt, ist der Refrain.
    /// Schon gegliederte Texte bleiben, wie sie sind.</summary>
    public static string Structure(string text)
    {
        var t = text.Replace("\r", "").Trim();
        if (t.Length == 0 || t.Contains('[')) return t;
        var blocks = Regex.Split(t, @"\n\s*\n").Select(b => b.Trim()).Where(b => b.Length > 0).ToList();
        static string Norm(string b) => Regex.Replace(b.ToLowerInvariant(), @"[^\p{L}]+", "");
        var counts = blocks.GroupBy(Norm).ToDictionary(g => g.Key, g => g.Count());
        var sb = new List<string>(); var verses = 0; var lastWasChorus = false;
        for (var i = 0; i < blocks.Count; i++)
        {
            var chorus = counts[Norm(blocks[i])] > 1;
            var bridge = !chorus && blocks.Count >= 5 && i == blocks.Count - 2 && lastWasChorus;
            sb.Add(chorus ? "[Refrain]" : bridge ? "[Bridge]" : "[Strophe]");
            if (!chorus && !bridge) verses++;
            sb.Add(blocks[i]); sb.Add("");
            lastWasChorus = chorus;
        }
        return string.Join("\n", sb).Trim();
    }

    // ───────────── M9: Silben, Reime, Refrain ─────────────
    [GeneratedRegex(@"(?:äu|eu|ei|ai|au|ie|ey|ay|[aeiouyäöü])+", RegexOptions.IgnoreCase)] private static partial Regex VowelGroups();

    /// <summary>Silben einer Zeile (deutsche Faustregel: Vokalgruppen; Diphthonge zählen einfach). Abschnitts-Marken zählen nicht.</summary>
    public static int Syllables(string line)
    {
        var l = line.Trim();
        if (l.StartsWith('[') && l.EndsWith(']')) return 0;
        return Regex.Matches(l, @"\p{L}+").Sum(w => Math.Max(1, VowelGroups().Matches(w.Value).Count));
    }

    /// <summary>Silben je Zeile – zum Prüfen, ob Zeilen auf denselben Rhythmus passen.</summary>
    public static List<(string Line, int Syllables)> SyllablesPerLine(string text) =>
        [.. text.Replace("\r", "").Split('\n').Where(l => l.Trim().Length > 0 && !(l.Trim().StartsWith('[') && l.Trim().EndsWith(']'))).Select(l => (l.Trim(), Syllables(l)))];

    /// <summary>Reimendung: ab dem letzten Vokal (samt Vokalgruppe) bis zum Ende, klein, ohne Satzzeichen.</summary>
    public static string RhymeKey(string word)
    {
        var w = Regex.Replace(word.ToLowerInvariant(), @"[^\p{L}]", "");
        var m = VowelGroups().Matches(w);
        return m.Count == 0 ? w : w[m[^1].Index..];
    }

    /// <summary>Reimwörter aus einer Wortliste (z. B. dem Rechtschreib-Wörterbuch): gleiche Reimendung, anderes Wort, kurze zuerst.</summary>
    public static List<string> Rhymes(string word, IEnumerable<string> words, int max = 24)
    {
        var key = RhymeKey(word); var self = word.Trim().ToLowerInvariant();
        if (key.Length < 2) return [];
        return words.Select(w => w.Split('/')[0].Trim()).Where(w => w.Length > 1 && w.All(char.IsLetter) && !w.Equals(self, StringComparison.OrdinalIgnoreCase) && RhymeKey(w) == key)
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(w => w.Length).ThenBy(w => w, StringComparer.OrdinalIgnoreCase).Take(max).ToList();
    }

    /// <summary>Den letzten Refrain am Ende noch einmal anhängen.</summary>
    public static string RepeatChorus(string lyrics)
    {
        var t = lyrics.Replace("\r", "").TrimEnd();
        var m = Regex.Matches(t, @"\[(Refrain|Chorus)\][^\[]*", RegexOptions.IgnoreCase);
        if (m.Count == 0) throw new ArgumentException("Im Text gibt es noch keinen [Refrain].");
        return t + "\n\n" + m[^1].Value.Trim();
    }

    // ───────────── M7: Tonart und Tempo bekannter Songs (freie Daten) ─────────────
    public sealed record Recording(string Mbid, string Title, string Artist, int? Year);
    public sealed record SongFacts(string Key, double Bpm, string Source);

    /// <summary>Treffer aus der MusicBrainz-Suche (JSON der Webservice-Version 2).</summary>
    public static List<Recording> ParseMusicBrainz(string json)
    {
        try
        {
            using var d = JsonDocument.Parse(json);
            if (!d.RootElement.TryGetProperty("recordings", out var recs)) return [];
            return [.. recs.EnumerateArray().Select(r => new Recording(
                r.GetProperty("id").GetString() ?? "", r.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "",
                r.TryGetProperty("artist-credit", out var ac) && ac.GetArrayLength() > 0 && ac[0].TryGetProperty("name", out var an) ? an.GetString() ?? "" : "",
                r.TryGetProperty("first-release-date", out var fr) && fr.GetString() is { Length: >= 4 } fd && int.TryParse(fd[..4], out var y) ? y : null))
                .Where(r => r.Mbid.Length > 0)];
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException) { return []; }
    }

    /// <summary>Tonart und Tempo aus AcousticBrainz (low-level, CC0) – oder null, wenn es keine Messung gibt.</summary>
    public static SongFacts? ParseAcousticBrainz(string json)
    {
        try
        {
            using var d = JsonDocument.Parse(json);
            var r = d.RootElement;
            var key = r.GetProperty("tonal").GetProperty("key_key").GetString(); var scale = r.GetProperty("tonal").GetProperty("key_scale").GetString();
            var bpm = r.GetProperty("rhythm").GetProperty("bpm").GetDouble();
            if (key is null || scale is null) return null;
            key = key switch { "D#" => "Eb", "G#" => "Ab", "A#" => "Bb", "Db" => "C#", "Gb" => "F#", _ => key }; // Schreibweise wie bei ACE-Step
            return new($"{key} {scale}", Math.Round(bpm, 1), "AcousticBrainz (CC0) · MusicBrainz");
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException) { return null; }
    }
}
