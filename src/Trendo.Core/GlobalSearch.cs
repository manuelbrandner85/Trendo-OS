using System.Globalization;
using System.Text;

namespace Trendo.Core;

/// <summary>CORE-01: Eine Suche für alles – Seiten, Studios, Projekte, Einstellungen und Dateien im Projektordner.</summary>
public sealed record SearchHit(string Kind, string Title, string Target, int Score);

public sealed class GlobalSearch
{
    private readonly List<(string Kind, string Title, string Target, string[] Words)> _items = [];

    public void Add(string kind, string title, string target, params string[] keywords) =>
        _items.Add((kind, title, target, keywords.Append(title).Select(Norm).ToArray()));

    /// <summary>Kleinbuchstaben, ohne Akzente, ä→ae usw., damit „Brief“, „briefe“ und „Bríef“ gleich gefunden werden.</summary>
    public static string Norm(string s)
    {
        s = s.ToLowerInvariant().Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue").Replace("ß", "ss");
        var sb = new StringBuilder();
        foreach (var c in s.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString();
    }

    public IReadOnlyList<SearchHit> Find(string query, int max = 30)
    {
        var terms = Norm(query).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (terms.Length == 0) return [];
        var hits = new List<SearchHit>();
        foreach (var it in _items)
        {
            var score = 0;
            foreach (var t in terms)
            {
                var best = 0;
                foreach (var w in it.Words)
                    best = Math.Max(best, w == t ? 100 : w.StartsWith(t) ? 60 : w.Contains(t) ? 30 : 0);
                if (best == 0) { score = 0; break; }
                score += best;
            }
            if (score > 0) hits.Add(new(it.Kind, it.Title, it.Target, score + (it.Kind == "Seite" ? 5 : 0)));
        }
        return hits.OrderByDescending(h => h.Score).ThenBy(h => h.Title, StringComparer.CurrentCulture).Take(max).ToList();
    }

    /// <summary>Dateinamen (nicht Inhalte) bis zu einer Tiefe von 3 und max. 2000 Einträgen – schnell und ohne den PC zu durchwühlen.</summary>
    public void AddFiles(string folder, int maxDepth = 3, int maxFiles = 2000)
    {
        if (!Directory.Exists(folder)) return;
        var n = 0; var q = new Queue<(string, int)>(); q.Enqueue((folder, 0));
        while (q.Count > 0 && n < maxFiles)
        {
            var (d, depth) = q.Dequeue();
            try
            {
                foreach (var f in Directory.EnumerateFiles(d)) { if (n++ >= maxFiles) break; Add("Datei", Path.GetFileName(f), f, Path.GetFileNameWithoutExtension(f).Split('-', '_', ' ', '.')); }
                if (depth < maxDepth) foreach (var s in Directory.EnumerateDirectories(d)) q.Enqueue((s, depth + 1));
            }
            catch (UnauthorizedAccessException) { } catch (IOException) { }
        }
    }
}
