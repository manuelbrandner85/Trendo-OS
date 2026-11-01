using System.Text.Json;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>KI fragen als Chatfenster (Masterprompt 131, Runde 7): Gespräche speichern, wiederfinden, umbenennen, löschen;
/// Antworten in Abschnitte zerlegen (Text, Code, Tabelle) für eine saubere Darstellung. Ablage: Datenordner/ki/gespraeche/&lt;id&gt;.json.</summary>
public sealed partial class ChatStore(string dataRoot)
{
    public sealed record Message(string Role, string Content, DateTimeOffset At, string? Model = null);
    public sealed class Chat
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..10];
        public string Title { get; set; } = "Neues Gespräch";
        public List<Message> Messages { get; set; } = [];
        public DateTimeOffset Updated { get; set; } = DateTimeOffset.Now;
        public bool TitleSet { get; set; }
    }

    string Dir => Path.Combine(dataRoot, "ki", "gespraeche");
    [GeneratedRegex("^[a-f0-9]{10}$")] private static partial Regex IdRx();
    string FileOf(string id) => IdRx().IsMatch(id) ? Path.Combine(Dir, id + ".json") : throw new ArgumentException("Ungültiges Gespräch.");

    /// <summary>Alle Gespräche, neuestes zuerst; optional gefiltert (Titel oder Inhalt).</summary>
    public IReadOnlyList<Chat> All(string? search = null)
    {
        if (!Directory.Exists(Dir)) return [];
        var list = new List<Chat>();
        foreach (var f in Directory.GetFiles(Dir, "*.json"))
            try { if (JsonSerializer.Deserialize<Chat>(File.ReadAllText(f)) is { } c) list.Add(c); } catch (Exception e) when (e is JsonException or IOException) { }
        var q = search?.Trim() ?? "";
        return list.Where(c => q.Length == 0 || c.Title.Contains(q, StringComparison.CurrentCultureIgnoreCase) || c.Messages.Any(m => m.Content.Contains(q, StringComparison.CurrentCultureIgnoreCase)))
            .OrderByDescending(c => c.Updated).ToList();
    }

    public Chat? Get(string id) { try { var f = FileOf(id); return File.Exists(f) ? JsonSerializer.Deserialize<Chat>(File.ReadAllText(f)) : null; } catch (Exception e) when (e is JsonException or IOException or ArgumentException) { return null; } }

    public void Save(Chat c)
    {
        Directory.CreateDirectory(Dir); c.Updated = DateTimeOffset.Now;
        var f = FileOf(c.Id); File.WriteAllText(f + ".tmp", JsonSerializer.Serialize(c)); SafeFile.Move(f + ".tmp", f);
    }

    /// <summary>Frage und Antwort anhängen; Titel automatisch aus der ersten Frage (bis 48 Zeichen), solange nicht umbenannt.</summary>
    public Chat Append(Chat c, string question, string answer, string? model)
    {
        c.Messages.Add(new("user", question, DateTimeOffset.Now)); c.Messages.Add(new("assistant", answer, DateTimeOffset.Now, model));
        if (!c.TitleSet && c.Messages.Count == 2) c.Title = TitleFrom(question);
        Save(c); return c;
    }

    public static string TitleFrom(string question)
    {
        var t = Regex.Replace(question.Trim(), @"\s+", " ");
        if (t.Length == 0) return "Neues Gespräch";
        if (t.Length <= 48) return t;
        var cut = t.LastIndexOf(' ', 45); // an einer Wortgrenze kürzen
        return (cut > 20 ? t[..cut] : t[..45]).TrimEnd(' ', ',', ';', ':') + " …";
    }

    public void Rename(string id, string title)
    {
        var c = Get(id) ?? throw new ArgumentException("Dieses Gespräch gibt es nicht mehr.");
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Bitte einen Namen.");
        c.Title = title.Trim(); c.TitleSet = true; Save(c);
    }

    public bool Delete(string id) { var f = FileOf(id); if (!File.Exists(f)) return false; File.Delete(f); return true; }

    /// <summary>Gespräch als Markdown-Dokument (zum Speichern oder Weitergeben).</summary>
    public static string ToMarkdown(Chat c) =>
        $"# {c.Title}\n\n" + string.Join("\n\n", c.Messages.Select(m => (m.Role == "user" ? "**Du:** " : "**Trendo KI" + (m.Model is { Length: > 0 } md ? $" ({md})" : "") + ":** ") + m.Content)) + "\n";

    // ───────────── Darstellung ─────────────

    public enum Kind { Text, Code, Table }
    public sealed record Segment(Kind Kind, string Content, string Language = "", IReadOnlyList<IReadOnlyList<string>>? Rows = null);

    /// <summary>Antwort in Abschnitte zerlegen: ```Code-Blöcke``` (mit Sprache), Markdown-Tabellen (| a | b |) und normalen Text.</summary>
    public static IReadOnlyList<Segment> Segments(string text)
    {
        var list = new List<Segment>(); var lines = text.Replace("\r", "").Split('\n'); var buf = new List<string>();
        void Flush() { var t = string.Join("\n", buf).Trim('\n'); if (t.Trim().Length > 0) list.Add(new(Kind.Text, t)); buf.Clear(); }
        for (var i = 0; i < lines.Length; i++)
        {
            var l = lines[i];
            if (l.TrimStart().StartsWith("```"))
            {
                Flush(); var lang = l.Trim()[3..].Trim(); var code = new List<string>();
                for (i++; i < lines.Length && !lines[i].TrimStart().StartsWith("```"); i++) code.Add(lines[i]);
                list.Add(new(Kind.Code, string.Join("\n", code), lang)); continue;
            }
            if (IsRow(l) && i + 1 < lines.Length && Regex.IsMatch(lines[i + 1].Trim(), @"^\|?\s*:?-{2,}:?\s*(\|\s*:?-{2,}:?\s*)*\|?$"))
            {
                Flush(); var rows = new List<IReadOnlyList<string>> { Cells(l) };
                for (i += 2; i < lines.Length && IsRow(lines[i]); i++) rows.Add(Cells(lines[i]));
                i--; list.Add(new(Kind.Table, "", "", rows)); continue;
            }
            buf.Add(l);
        }
        Flush(); return list;
    }

    static bool IsRow(string l) => l.Trim().StartsWith('|') && l.Trim().Count(c => c == '|') >= 2;
    static IReadOnlyList<string> Cells(string l) => l.Trim().Trim('|').Split('|').Select(c => c.Trim()).ToList();
}
