using System.Text.Json;
using System.Text.RegularExpressions;

namespace Trendo.Core;

public sealed record PromptVersion(int Version, string Text, DateTimeOffset At, string Note);
public sealed class PromptTemplate
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "Allgemein";
    public bool BuiltIn { get; set; }
    public List<PromptVersion> Versions { get; set; } = [];
    public string Text => Versions.Count == 0 ? "" : Versions[^1].Text;
    public int Version => Versions.Count == 0 ? 0 : Versions[^1].Version;
}

public sealed record PromptHint(string Level, string Text);

/// <summary>CORE-12 Prompt & Masterprompt Studio: Vorlagen mit {{Platzhaltern}}, Versionen, Qualitätsprüfung und Masterprompt-Baukasten.</summary>
public sealed partial class PromptLibrary
{
    [GeneratedRegex(@"\{\{\s*([\p{L}\p{N}_ -]+?)\s*(?:\|([^}]*))?\}\}")] private static partial Regex VarRx();
    private readonly string _file; private readonly AuditLog _audit;
    private readonly List<PromptTemplate> _items;

    public static readonly (string Id, string Name, string Category, string Text)[] Defaults =
    [
        ("einfach-erklaeren", "Einfach erklären", "Alltag", "Erkläre mir {{Thema}} so, dass es ein {{Alter|12}}-jähriges Kind versteht. Nutze höchstens 5 kurze Sätze und ein Beispiel aus dem Alltag."),
        ("zusammenfassen", "Text zusammenfassen", "Texte", "Fasse den folgenden Text in {{Anzahl|3}} Stichpunkten auf Deutsch zusammen. Nenne zuerst das Wichtigste.\n\nText:\n{{Text}}"),
        ("brief-formulieren", "Brief formulieren", "Briefe", "Schreibe einen höflichen, klaren Brief an {{Empfänger}}. Anliegen: {{Anliegen}}. Ton: {{Ton|sachlich und freundlich}}. Format: Betreff, Anrede, 3 kurze Absätze, Gruß. Erfinde keine Fakten; markiere fehlende Angaben mit [bitte ergänzen]."),
        ("mail-umschreiben", "E-Mail freundlicher machen", "Texte", "Schreibe diese Nachricht freundlicher und kürzer, ohne den Inhalt zu ändern:\n\n{{Nachricht}}"),
        ("ideen", "Ideen sammeln", "Kreativ", "Sammle {{Anzahl|10}} unterschiedliche Ideen für {{Thema}}. Gib jede Idee als eine Zeile mit kurzem Nutzen aus."),
        ("fehler-pruefen", "Rechtschreibung prüfen", "Texte", "Prüfe den Text auf Rechtschreibung und Grammatik. Gib zuerst den korrigierten Text aus, danach eine Liste der Änderungen.\n\n{{Text}}"),
    ];

    public PromptLibrary(string dataRoot, AuditLog audit)
    {
        _audit = audit; _file = Path.Combine(dataRoot, "prompts", "vorlagen.json");
        Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
        try { _items = File.Exists(_file) ? JsonSerializer.Deserialize<List<PromptTemplate>>(File.ReadAllText(_file)) ?? [] : []; } catch { _items = []; }
        foreach (var d in Defaults.Where(d => _items.All(i => i.Id != d.Id)))
            _items.Add(new PromptTemplate { Id = d.Id, Name = d.Name, Category = d.Category, BuiltIn = true, Versions = [new(1, d.Text, DateTimeOffset.Now, "mitgeliefert")] });
    }

    public IReadOnlyList<PromptTemplate> All() => _items.OrderBy(i => i.Category).ThenBy(i => i.Name).ToList();
    public PromptTemplate? Get(string id) => _items.FirstOrDefault(i => i.Id == id);

    public PromptTemplate Save(string? id, string name, string category, string text, string note = "")
    {
        var t = id is null ? null : Get(id);
        if (t is null)
        {
            var baseId = Slug(name); var nid = baseId; var n = 2;
            while (Get(nid) is not null) nid = $"{baseId}-{n++}";
            t = new PromptTemplate { Id = nid }; _items.Add(t);
        }
        t.Name = string.IsNullOrWhiteSpace(name) ? t.Name : name.Trim(); t.Category = string.IsNullOrWhiteSpace(category) ? "Allgemein" : category.Trim();
        if (t.Text != text) t.Versions.Add(new(t.Version + 1, text, DateTimeOffset.Now, note));
        Persist(); _audit.Append(Principal.LocalOwner.Name, "prompt.save", $"{t.Id} v{t.Version}");
        return t;
    }

    public PromptTemplate Restore(string id, int version)
    {
        var t = Get(id) ?? throw new KeyNotFoundException(id);
        var v = t.Versions.First(x => x.Version == version);
        return Save(id, t.Name, t.Category, v.Text, $"wiederhergestellt aus v{version}");
    }

    public bool Delete(string id)
    {
        var t = Get(id); if (t is null || t.BuiltIn) return false;
        _items.Remove(t); Persist(); _audit.Append(Principal.LocalOwner.Name, "prompt.delete", id); return true;
    }

    /// <summary>Platzhalter in Reihenfolge, mit Standardwert.</summary>
    public static IReadOnlyList<(string Name, string? Default)> Variables(string text) =>
        VarRx().Matches(text).Select(m => (m.Groups[1].Value.Trim(), m.Groups[2].Success ? m.Groups[2].Value : null)).DistinctBy(v => v.Item1).ToList();

    public static (string Text, IReadOnlyList<string> Missing) Fill(string text, IReadOnlyDictionary<string, string> values)
    {
        var missing = new List<string>();
        var r = VarRx().Replace(text, m =>
        {
            var k = m.Groups[1].Value.Trim();
            if (values.TryGetValue(k, out var v) && !string.IsNullOrWhiteSpace(v)) return v;
            if (m.Groups[2].Success) return m.Groups[2].Value;
            if (!missing.Contains(k)) missing.Add(k);
            return m.Value;
        });
        return (r, missing);
    }

    /// <summary>Einfache Qualitätsprüfung in verständlicher Sprache.</summary>
    public static IReadOnlyList<PromptHint> Check(string text)
    {
        var h = new List<PromptHint>();
        var t = text.Trim(); var lower = t.ToLowerInvariant();
        if (t.Length < 20) h.Add(new("warn", "Sehr kurz. Schreib dazu, was genau du willst."));
        if (!Regex.IsMatch(lower, @"\b(schreib|erklär|erklaer|fass|sammle|prüf|pruef|erstelle|übersetz|gib|nenne|liste|plane|analysier|formulier|beschreib|vergleich)")) h.Add(new("tipp", "Beginne mit einer klaren Aufgabe, z. B. „Schreibe …“ oder „Erkläre …“."));
        if (!Regex.IsMatch(lower, @"(stichpunkt|sätze|saetze|absätz|absaetz|tabelle|liste|format|zeile|wörter|woerter|kurz)")) h.Add(new("tipp", "Sag, wie die Antwort aussehen soll (z. B. „in 3 Stichpunkten“)."));
        if (t.Length > 6000) h.Add(new("warn", "Sehr lang. Kleine Modelle auf dem PC lesen nur einen Teil."));
        var open = Regex.Matches(t, @"\{\{").Count; var close = Regex.Matches(t, @"\}\}").Count;
        if (open != close) h.Add(new("warn", "Ein Platzhalter ist nicht geschlossen: {{ und }} müssen paarweise stehen."));
        if (Regex.IsMatch(t, @"(?i)(passwort|password|api[-_ ]?key|iban)\s*[:=]\s*\S+")) h.Add(new("warn", "Enthält vermutlich ein Passwort oder einen Schlüssel. Bitte entfernen."));
        if (h.Count == 0) h.Add(new("ok", "Gut formuliert: klare Aufgabe und Format."));
        return h;
    }

    /// <summary>Masterprompt-Baukasten: setzt Rolle, Ziel, Hintergrund, Format und Regeln zu einem klaren Auftrag zusammen.</summary>
    public static string Master(string role, string goal, string context, string format, string rules)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(role)) parts.Add($"## Rolle\nDu bist {role.Trim()}.");
        if (!string.IsNullOrWhiteSpace(goal)) parts.Add($"## Ziel\n{goal.Trim()}");
        if (!string.IsNullOrWhiteSpace(context)) parts.Add($"## Hintergrund\n{context.Trim()}");
        if (!string.IsNullOrWhiteSpace(format)) parts.Add($"## Form der Antwort\n{format.Trim()}");
        var r = (rules ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        r.Add("Erfinde keine Fakten. Wenn etwas fehlt, frag nach oder markiere es mit [bitte ergänzen].");
        parts.Add("## Regeln\n" + string.Join("\n", r.Select(x => "- " + x.TrimStart('-', ' '))));
        return string.Join("\n\n", parts);
    }

    private void Persist() => File.WriteAllText(_file, JsonSerializer.Serialize(_items));
    private static string Slug(string s)
    {
        var t = new string(s.ToLowerInvariant().Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue").Replace("ß", "ss").Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');
        while (t.Contains("--")) t = t.Replace("--", "-");
        return t == "" ? "vorlage" : t;
    }
}
