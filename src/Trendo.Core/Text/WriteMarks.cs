namespace Trendo.Core.Text;

/// <summary>V-12 (T-0700): Schreibzeichen per Knopf statt Tippen. Die Person markiert Text (oder setzt nur die Marke) und drückt „Überschrift“, „Fett“, „Liste“ –
/// Trendo setzt die Zeichen (#, **, -, [[ ]], {{ }}) selbst. Das Dateiformat bleibt Markdown, damit vorhandene Dokumente lesbar bleiben.
/// Ergebnis: neuer Text und die neue Markierung (Start, Ende) – Markiertes bleibt markiert.</summary>
public static class WriteMarks
{
    public enum Kind { Heading, Bold, Italic, List, Link, Placeholder }

    public readonly record struct Result(string Text, int SelStart, int SelEnd);

    /// <summary>Wendet die Auszeichnung auf die Markierung [start, end) an (start == end: nur die Marke).</summary>
    public static Result Apply(string text, int start, int end, Kind kind, string? placeholder = null)
    {
        text ??= "";
        start = Math.Clamp(start, 0, text.Length); end = Math.Clamp(end, 0, text.Length);
        if (end < start) (start, end) = (end, start);
        return kind switch
        {
            Kind.Heading => LinePrefix(text, start, end, "# "),
            Kind.List => LinePrefix(text, start, end, "- "),
            Kind.Bold => Wrap(text, start, end, "**", "**", "fetter Text"),
            Kind.Italic => Wrap(text, start, end, "*", "*", "schräger Text"),
            Kind.Link => Wrap(text, start, end, "[[", "]]", placeholder ?? "Seitentitel"),
            Kind.Placeholder => Wrap(text, start, end, "{{", "}}", placeholder ?? "Thema"),
            _ => new(text, start, end),
        };
    }

    /// <summary>Zeichen um die Markierung; ohne Markierung ein Beispielwort, das gleich markiert ist (einfach drüberschreiben). Schon umschlossen: Zeichen wieder weg.</summary>
    static Result Wrap(string text, int start, int end, string open, string close, string sample)
    {
        if (start >= open.Length && end + close.Length <= text.Length && text.AsSpan(start - open.Length, open.Length).SequenceEqual(open) && text.AsSpan(end, close.Length).SequenceEqual(close)
            && !(open == "*" && start >= 2 && text[start - 2] == '*')) // „*“ in „**fett**“ ist nicht kursiv
            return new(text.Remove(end, close.Length).Remove(start - open.Length, open.Length), start - open.Length, end - open.Length);
        var inner = start == end ? sample : text[start..end];
        var neu = text[..start] + open + inner + close + text[end..];
        return new(neu, start + open.Length, start + open.Length + inner.Length);
    }

    /// <summary>Zeichen an den Anfang jeder berührten Zeile (eine vorhandene Überschrift oder Aufzählung wird ersetzt); stehen sie schon überall, werden sie entfernt.</summary>
    static Result LinePrefix(string text, int start, int end, string prefix)
    {
        var lineStart = start == 0 ? 0 : text.LastIndexOf('\n', start - 1) + 1;
        var lastEnd = end > start && text[end - 1] == '\n' ? end - 1 : end;
        var lineEnd = text.IndexOf('\n', lastEnd); if (lineEnd < 0) lineEnd = text.Length;
        var lines = text[lineStart..lineEnd].Split('\n');
        var all = lines.All(l => l.StartsWith(prefix, StringComparison.Ordinal));
        static string Bare(string l) => l.StartsWith("- ", StringComparison.Ordinal) ? l[2..] : l.StartsWith('#') ? l.TrimStart('#').TrimStart(' ') : l;
        var block = string.Join('\n', lines.Select(l => all ? l[prefix.Length..] : prefix + Bare(l)));
        var neu = text[..lineStart] + block + text[lineEnd..];
        var delta = block.Length - (lineEnd - lineStart);
        if (start != end) return new(neu, lineStart, lineEnd + delta);
        var caret = Math.Clamp(start + delta, lineStart, lineStart + block.Length);
        return new(neu, caret, caret);
    }
}
