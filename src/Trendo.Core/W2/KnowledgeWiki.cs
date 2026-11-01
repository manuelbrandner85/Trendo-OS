using System.Text.Json;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>Wissen als Notiz-Wiki (Masterprompt 131, Runde 7): Seiten bearbeiten, Verlinkungen mit [[Titel]] oder [[Titel|Anzeige]],
/// Rückverweise, fehlende Seiten; Löschen geht in den Papierkorb des Wissens und lässt sich wiederherstellen.</summary>
public sealed partial class KnowledgeStore
{
    string TrashFile => Path.Combine(Path.GetDirectoryName(_file)!, "papierkorb.json");

    [GeneratedRegex(@"\[\[([^\[\]|]+)(?:\|([^\[\]]+))?\]\]")] private static partial Regex LinkRx();

    /// <summary>Verlinkte Titel einer Seite (ohne doppelte, Reihenfolge des Textes).</summary>
    public static IReadOnlyList<string> Links(string text) =>
        LinkRx().Matches(text).Select(m => m.Groups[1].Value.Trim()).Where(t => t.Length > 0).Distinct(StringComparer.CurrentCultureIgnoreCase).ToList();

    /// <summary>Text in Stücke zerlegen: normaler Text und Verweise (Titel, Anzeige) – für die Leseansicht.</summary>
    public static IReadOnlyList<(string Text, string? Link)> Segments(string text)
    {
        var res = new List<(string, string?)>(); var at = 0;
        foreach (Match m in LinkRx().Matches(text))
        {
            if (m.Index > at) res.Add((text[at..m.Index], null));
            res.Add((m.Groups[2].Success ? m.Groups[2].Value.Trim() : m.Groups[1].Value.Trim(), m.Groups[1].Value.Trim()));
            at = m.Index + m.Length;
        }
        if (at < text.Length) res.Add((text[at..], null));
        return res;
    }

    public KnowledgeItem? Get(string id) => _items.FirstOrDefault(i => i.Id == id);
    public KnowledgeItem? ByTitle(string title) => _items.FirstOrDefault(i => string.Equals(i.Title.Trim(), title.Trim(), StringComparison.CurrentCultureIgnoreCase));

    /// <summary>Seiten, die auf diese Seite verweisen.</summary>
    public IReadOnlyList<KnowledgeItem> Backlinks(string title) =>
        _items.Where(i => !string.Equals(i.Title, title, StringComparison.CurrentCultureIgnoreCase) && Links(i.Text).Contains(title.Trim(), StringComparer.CurrentCultureIgnoreCase)).OrderBy(i => i.Title, StringComparer.CurrentCultureIgnoreCase).ToList();

    /// <summary>Verweise einer Seite, zu denen es noch keine Seite gibt.</summary>
    public IReadOnlyList<string> Missing(KnowledgeItem item) => Links(item.Text).Where(t => ByTitle(t) is null).ToList();

    /// <summary>Seite ändern. Wird der Titel geändert, passen sich die Verweise anderer Seiten mit an.</summary>
    public KnowledgeItem Update(string id, string title, string text)
    {
        var i = _items.FindIndex(x => x.Id == id); if (i < 0) throw new KeyNotFoundException("Diese Seite gibt es nicht mehr.");
        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Leerer Text");
        var old = _items[i]; var newTitle = string.IsNullOrWhiteSpace(title) ? FirstLine(text) : title.Trim();
        if (!string.Equals(newTitle, old.Title, StringComparison.CurrentCultureIgnoreCase) && ByTitle(newTitle) is not null) throw new ArgumentException($"Eine Seite „{newTitle}“ gibt es schon.");
        _items[i] = old with { Title = newTitle, Text = text.Trim() };
        if (newTitle != old.Title)
        {
            var rx = new Regex(@"\[\[\s*" + Regex.Escape(old.Title) + @"\s*(\|[^\[\]]+)?\]\]", RegexOptions.IgnoreCase);
            for (var k = 0; k < _items.Count; k++) if (k != i && rx.IsMatch(_items[k].Text)) _items[k] = _items[k] with { Text = rx.Replace(_items[k].Text, m => "[[" + newTitle + m.Groups[1].Value + "]]") };
        }
        Save(); _audit.Append(Principal.LocalOwner.Name, "wissen.update", $"{id} {_items[i].Text.Length}Z");
        return _items[i];
    }

    public IReadOnlyList<KnowledgeItem> Trashed()
    {
        try { return File.Exists(TrashFile) ? JsonSerializer.Deserialize<List<KnowledgeItem>>(File.ReadAllText(TrashFile)) ?? [] : []; }
        catch (JsonException) { return []; }
    }

    /// <summary>In den Papierkorb des Wissens (wiederherstellbar) statt endgültig löschen.</summary>
    public bool Trash(string id)
    {
        var it = Get(id); if (it is null) return false;
        var t = Trashed().ToList(); t.Add(it); File.WriteAllText(TrashFile, JsonSerializer.Serialize(t));
        _items.Remove(it); Save(); _audit.Append(Principal.LocalOwner.Name, "wissen.trash", id);
        return true;
    }

    public bool Restore(string id)
    {
        var t = Trashed().ToList(); var it = t.FirstOrDefault(x => x.Id == id); if (it is null) return false;
        t.Remove(it); File.WriteAllText(TrashFile, JsonSerializer.Serialize(t));
        _items.Add(it); Save(); _audit.Append(Principal.LocalOwner.Name, "wissen.restore", id);
        return true;
    }
}
