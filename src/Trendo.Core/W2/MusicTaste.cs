namespace Trendo.Core;

/// <summary>Trendo Musik V13 wie Suno (07.11.2026): „Mein Geschmack“ – lernt nur auf dem eigenen PC aus den ★-Favoriten, welche Stil-Wörter man mag
/// (Favorit zählt 3, ein daraus abgeleitetes Lied 1), ohne KI und nachvollziehbar. Daraus: Vorschlag für ein neues Lied und Reihenfolge „Für dich“.</summary>
public static class MusicTaste
{
    public sealed record Word(string Text, double Weight);

    /// <summary>Lied-Arten, deren Stil kein Geschmack ist (Spuren, Sounds, Sprache, eigene Dateien ohne Stil).</summary>
    static readonly string[] Skip = ["gesang", "begleitung", "sound", "sprache", "mischung"];

    /// <summary>Stil-Text → einzelne Wörter (klein, ohne Zusätze wie „Sample aus …“, „Mashup: …“, Prozentangaben).</summary>
    public static IEnumerable<string> Words(string? style)
    {
        if (string.IsNullOrWhiteSpace(style)) yield break;
        foreach (var raw in style.Split([',', '·', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var w = raw.Trim().Trim('.', '„', '“', '"').ToLowerInvariant();
            if (w.Length is < 2 or > 32 || w.Contains('%') || w.Contains('„') || w.StartsWith("sample aus") || w.StartsWith("mashup") || w.Any(char.IsDigit)) continue;
            yield return w;
        }
    }

    /// <summary>Geschmacksprofil: gewichtete Wörter, stärkste zuerst (höchstens <paramref name="max"/>). Leer bei weniger als zwei Favoriten.</summary>
    public static List<Word> Profile(IEnumerable<MusicSong> songs, int max = 12)
    {
        var list = songs.Where(s => !Skip.Contains(s.Kind) && !s.Kind.StartsWith("spur-") && !s.Kind.StartsWith("ohne-")).ToList();
        var favs = list.Where(s => s.Favorite).ToList();
        if (favs.Count < 2) return [];
        var favIds = favs.Select(s => s.Id).ToHashSet();
        var w = new Dictionary<string, double>();
        foreach (var s in list)
        {
            var weight = s.Favorite ? 3.0 : s.ParentId is { } p && favIds.Contains(p) ? 1.0 : 0;
            if (weight == 0) continue;
            foreach (var word in Words(s.Style).Distinct()) w[word] = w.GetValueOrDefault(word) + weight;
        }
        return [.. w.Where(kv => kv.Value >= 3).OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).Take(max).Select(kv => new Word(kv.Key, kv.Value))];
    }

    /// <summary>Vorschlag für ein neues Lied: die drei stärksten Wörter, ein weiteres aus dem Profil und ein Thema.</summary>
    public static string Suggest(IReadOnlyList<Word> profile, int seed)
    {
        if (profile.Count == 0) return MusicSuno.Idea(seed);
        var r = new Random(seed);
        var pick = profile.Take(3).Select(x => x.Text).ToList();
        var rest = profile.Skip(3).ToList();
        if (rest.Count > 0) pick.Add(rest[r.Next(rest.Count)].Text);
        return string.Join(", ", pick) + ", " + MusicSuno.Topic(r.Next());
    }

    /// <summary>Wie gut passt ein Lied zum Geschmack (0–1): Anteil der Profil-Gewichte, die im Stil vorkommen.</summary>
    public static double Score(MusicSong s, IReadOnlyList<Word> profile)
    {
        if (profile.Count == 0) return 0;
        var words = Words(s.Style).ToHashSet();
        return profile.Where(p => words.Contains(p.Text)).Sum(p => p.Weight) / profile.Sum(p => p.Weight);
    }
}
