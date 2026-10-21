using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Trendo.Core;

public sealed record KnowledgeItem(string Id, string Title, string Text, List<string> Tags, string Source, DateTimeOffset Created);
public sealed record KnowledgeHit(KnowledgeItem Item, double Score);

/// <summary>CORE-10 Knowledge Memory & Self Improvement: eigenes Wissen nur auf diesem Gerät, Volltextsuche (BM25, Umlaute egal),
/// Lernen aus Rückmeldungen – eine Korrektur wird zu Wissen, das die KI künftig mitliest.</summary>
public sealed class KnowledgeStore
{
    private readonly string _file;
    private readonly AuditLog _audit;
    private List<KnowledgeItem> _items;
    public const int MaxImportBytes = 2 * 1024 * 1024;

    public KnowledgeStore(string dataRoot, AuditLog audit)
    {
        _audit = audit; _file = Path.Combine(dataRoot, "wissen", "wissen.json");
        Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
        try { _items = File.Exists(_file) ? JsonSerializer.Deserialize<List<KnowledgeItem>>(File.ReadAllText(_file)) ?? [] : []; } catch { _items = []; }
    }

    public IReadOnlyList<KnowledgeItem> All() => _items.OrderByDescending(i => i.Created).ToList();

    public KnowledgeItem Add(string title, string text, string source = "eigene Notiz", params string[] tags)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Leerer Text");
        var it = new KnowledgeItem(Guid.NewGuid().ToString("N")[..10], string.IsNullOrWhiteSpace(title) ? FirstLine(text) : title.Trim(), text.Trim(), tags.ToList(), source, DateTimeOffset.Now);
        _items.Add(it); Save();
        _audit.Append(Principal.LocalOwner.Name, "wissen.add", $"{it.Id} quelle={source} {it.Text.Length}Z");
        return it;
    }

    /// <summary>Liest .txt/.md/.csv-Dateien (einzeln oder ganzen Ordner) ein. Große und binäre Dateien werden übersprungen.</summary>
    public (int Added, int Skipped) Import(string path)
    {
        var files = Directory.Exists(path) ? Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories) : [path];
        int a = 0, s = 0;
        foreach (var f in files)
        {
            var ext = Path.GetExtension(f).ToLowerInvariant();
            var fi = new FileInfo(f);
            if (ext is not (".txt" or ".md" or ".csv") || !fi.Exists || fi.Length > MaxImportBytes || fi.Length == 0) { s++; continue; }
            var text = File.ReadAllText(f);
            if (text.Contains('\0')) { s++; continue; }
            if (_items.Any(i => i.Source == "Datei: " + fi.Name && i.Text == text.Trim())) { s++; continue; }
            Add(Path.GetFileNameWithoutExtension(f), text, "Datei: " + fi.Name); a++;
        }
        return (a, s);
    }

    public bool Remove(string id)
    {
        var n = _items.RemoveAll(i => i.Id == id); if (n > 0) { Save(); _audit.Append(Principal.LocalOwner.Name, "wissen.remove", id); }
        return n > 0;
    }

    /// <summary>Lernen: Eine Rückmeldung mit Korrektur wird als Wissen gespeichert, damit die nächste Antwort besser wird.</summary>
    public KnowledgeItem? Feedback(string question, string answer, bool good, string? correction = null)
    {
        var f = Path.Combine(Path.GetDirectoryName(_file)!, "rueckmeldungen.jsonl");
        File.AppendAllText(f, JsonSerializer.Serialize(new { at = DateTimeOffset.Now, good, q = question.Length, a = answer.Length, korrigiert = !string.IsNullOrWhiteSpace(correction) }) + "\n");
        _audit.Append(Principal.LocalOwner.Name, "wissen.feedback", good ? "gut" : "schlecht");
        return string.IsNullOrWhiteSpace(correction) ? null : Add("Korrektur: " + Shorten(question, 60), $"Frage: {question}\nRichtig ist: {correction.Trim()}", "Rückmeldung", "korrektur");
    }

    public (int Good, int Bad) FeedbackStats()
    {
        var f = Path.Combine(Path.GetDirectoryName(_file)!, "rueckmeldungen.jsonl");
        if (!File.Exists(f)) return (0, 0);
        var lines = File.ReadAllLines(f).Where(l => l.Length > 0).ToList();
        var g = lines.Count(l => l.Contains("\"good\":true"));
        return (g, lines.Count - g);
    }

    public IReadOnlyList<KnowledgeHit> Search(string query, int max = 5)
    {
        var q = Tokens(query).Distinct().ToList();
        if (q.Count == 0 || _items.Count == 0) return [];
        var docs = _items.Select(i => (i, t: Tokens(i.Title + " " + i.Title + " " + i.Text + " " + string.Join(' ', i.Tags)).ToList())).ToList();
        var avg = docs.Average(d => d.t.Count); var n = docs.Count;
        var df = q.ToDictionary(w => w, w => docs.Count(d => d.t.Contains(w)));
        const double k1 = 1.4, b = 0.75;
        return docs.Select(d =>
        {
            double s = 0;
            foreach (var w in q)
            {
                var tf = d.t.Count(x => x == w); if (tf == 0) continue;
                var idf = Math.Log(1 + (n - df[w] + 0.5) / (df[w] + 0.5));
                s += idf * tf * (k1 + 1) / (tf + k1 * (1 - b + b * d.t.Count / avg));
            }
            if (d.i.Tags.Contains("korrektur")) s *= 1.2;
            return new KnowledgeHit(d.i, s);
        }).Where(h => h.Score > 0).OrderByDescending(h => h.Score).Take(max).ToList();
    }

    public string ExportJson() => JsonSerializer.Serialize(_items, new JsonSerializerOptions { WriteIndented = true });

    private void Save() => File.WriteAllText(_file, JsonSerializer.Serialize(_items));

    private static readonly HashSet<string> Stop = ["der", "die", "das", "und", "oder", "ein", "eine", "einen", "ist", "sind", "was", "wie", "wo", "wer", "ich", "du", "mit", "von", "zu", "im", "in", "am", "an", "auf", "fuer", "den", "dem", "des", "es", "nicht", "the", "a", "of", "to"];

    public static IEnumerable<string> Tokens(string s)
    {
        var sb = new StringBuilder();
        foreach (var ch in s.ToLowerInvariant().Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue").Replace("ß", "ss").Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(ch)) sb.Append(ch);
            else if (sb.Length > 0) { var w = sb.ToString(); sb.Clear(); if (w.Length > 1 && !Stop.Contains(w)) yield return Stem(w); }
        }
        if (sb.Length > 1 && !Stop.Contains(sb.ToString())) yield return Stem(sb.ToString());
    }

    /// <summary>Sehr einfacher deutscher Wortstamm (Plural/Endungen), damit „Rechnungen“ auch „Rechnung“ findet.</summary>
    private static string Stem(string w)
    {
        foreach (var e in new[] { "ungen", "en", "er", "es", "e", "n", "s" })
            if (w.Length - e.Length >= 4 && w.EndsWith(e)) return w[..^e.Length] + (e == "ungen" ? "ung" : "");
        return w;
    }

    private static string FirstLine(string t) => Shorten(t.Trim().Split('\n')[0], 60);
    private static string Shorten(string t, int n) => t.Length <= n ? t : t[..n] + "…";
}
