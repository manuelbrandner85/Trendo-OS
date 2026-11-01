using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Trendo.Core;

/// <summary>Markierung im Lesetext: Stelle (Zeichen), markierter Text, Farbe, Notiz – gehört zur Quelle (URL).</summary>
public sealed record Highlight(string Id, string Url, string Title, int Start, int Length, string Text, string Color, string Note, DateTimeOffset At);

/// <summary>Recherche mit Markierungen (Masterprompt 131): Textmarker in Farben mit Notiz, Zitatsammlung je Quelle, Literaturliste (einfach/DIN-nah oder APA).
/// Bleibt wie die Quellenliste flüchtig in der Sitzung; der Bericht nimmt Markierungen und Literaturliste mit.</summary>
public sealed partial class ResearchDesk
{
    public static readonly string[] Colors = ["gelb", "grün", "blau", "rot"];
    static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");
    string MarksFile => Path.Combine(_dir, "markierungen.json");

    public IReadOnlyList<Highlight> Highlights(string? url = null)
    {
        List<Highlight> all; try { all = File.Exists(MarksFile) ? JsonSerializer.Deserialize<List<Highlight>>(File.ReadAllText(MarksFile)) ?? [] : []; } catch (JsonException) { all = []; }
        return all.Where(h => url is null || h.Url == url).OrderBy(h => h.Url == url ? 0 : 1).ThenBy(h => h.Url).ThenBy(h => h.Start).ToList();
    }
    void SaveMarks(IEnumerable<Highlight> l) { Directory.CreateDirectory(_dir); File.WriteAllText(MarksFile, JsonSerializer.Serialize(l.ToList())); }

    /// <summary>Stelle markieren. Leerzeichen am Rand werden abgeschnitten; dieselbe Stelle zweimal ersetzt Farbe/Notiz statt doppelt zu markieren.</summary>
    public Highlight AddHighlight(string url, string title, string fullText, int start, int length, string color = "gelb", string note = "")
    {
        if (!Colors.Contains(color)) throw new ArgumentException("Unbekannte Farbe.");
        if (start < 0 || length <= 0 || start + length > fullText.Length) throw new ArgumentException("Bitte zuerst im Text eine Stelle auswählen.");
        while (length > 0 && char.IsWhiteSpace(fullText[start])) { start++; length--; }
        while (length > 0 && char.IsWhiteSpace(fullText[start + length - 1])) length--;
        if (length == 0) throw new ArgumentException("Bitte zuerst im Text eine Stelle auswählen.");
        var all = Highlights().ToList(); var same = all.FindIndex(h => h.Url == url && h.Start == start && h.Length == length);
        var h = new Highlight(same >= 0 ? all[same].Id : Guid.NewGuid().ToString("N")[..8], url, title, start, length, fullText.Substring(start, length), color, note.Trim(), DateTimeOffset.Now);
        if (same >= 0) all[same] = h; else all.Add(h);
        SaveMarks(all); _audit.Append(Principal.LocalOwner.Name, "research.highlight", $"{new Uri(url).Host} {length}Z");
        return h;
    }
    public void RemoveHighlight(string id) => SaveMarks(Highlights().Where(h => h.Id != id));
    public void SetHighlight(string id, string? color = null, string? note = null) =>
        SaveMarks(Highlights().Select(h => h.Id != id ? h : h with { Color = color is not null && Colors.Contains(color) ? color : h.Color, Note = note?.Trim() ?? h.Note }));

    /// <summary>Abschnitte für die Anzeige: Text in Stücke zerlegt, jedes mit der Farbe der (zuletzt angelegten) Markierung darüber oder null.</summary>
    public static List<(string Text, Highlight? Mark)> Segments(string text, IEnumerable<Highlight> marks)
    {
        var owner = new Highlight?[text.Length];
        foreach (var h in marks.OrderBy(h => h.At)) for (var i = h.Start; i < Math.Min(text.Length, h.Start + h.Length); i++) owner[i] = h;
        var res = new List<(string, Highlight?)>(); var from = 0;
        for (var i = 1; i <= text.Length; i++)
            if (i == text.Length || !ReferenceEquals(owner[i], owner[from])) { res.Add((text[from..i], owner[from])); from = i; }
        return res;
    }

    /// <summary>Quellenangabe: „einfach“ (DIN-nah) oder „apa“; Seitenname aus der Adresse, Abrufdatum aus der Sitzung.</summary>
    public static string Cite(ResearchSource s, string style = "einfach")
    {
        var u = new Uri(s.Url); var site = u.Host.StartsWith("www.") ? u.Host[4..] : u.Host;
        return style == "apa"
            ? $"{site}. (o. J.). {s.Title}. Abgerufen am {s.At.ToString("d. MMMM yyyy", De)}, von {s.Url}"
            : $"{s.Title}. In: {site}. Online unter: {s.Url} (Abruf: {s.At:dd.MM.yyyy}).";
    }

    /// <summary>Literaturliste: alle gelesenen Quellen (oder nur die mit Markierungen), alphabetisch, ohne Doppelte.</summary>
    public List<string> Bibliography(string style = "einfach", bool onlyMarked = false)
    {
        var marked = Highlights().Select(h => h.Url).ToHashSet();
        return SessionSources().GroupBy(s => s.Url).Select(g => g.First()).Where(s => !onlyMarked || marked.Contains(s.Url))
            .Select(s => Cite(s, style)).OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    /// <summary>Zitat mit Quelle zum Einfügen.</summary>
    public string Quote(Highlight h, string style = "einfach")
    {
        var src = SessionSources().FirstOrDefault(s => s.Url == h.Url) ?? new ResearchSource(h.Url, h.Title, h.At, "", 0);
        return $"„{h.Text}“ ({Cite(src, style)})" + (h.Note.Length > 0 ? $"\nNotiz: {h.Note}" : "");
    }

    /// <summary>Markdown-Teil für den Bericht: Zitate je Quelle mit Farbe und Notiz, danach die Literaturliste.</summary>
    public string MarksMarkdown(string style = "einfach")
    {
        var sb = new StringBuilder(); var marks = Highlights();
        if (marks.Count > 0)
        {
            sb.Append("\n## Zitate und Markierungen\n");
            foreach (var g in marks.GroupBy(h => h.Url))
            {
                sb.Append($"\n### {g.First().Title}\n");
                foreach (var h in g) sb.Append($"\n> „{h.Text.Replace("\n", " ")}“ ({h.Color})\n").Append(h.Note.Length > 0 ? $"\n{h.Note}\n" : "");
            }
        }
        var bib = Bibliography(style);
        if (bib.Count > 0) { sb.Append("\n## Literaturliste\n\n"); foreach (var b in bib) sb.Append("- ").Append(b).Append('\n'); }
        return sb.ToString();
    }
}
