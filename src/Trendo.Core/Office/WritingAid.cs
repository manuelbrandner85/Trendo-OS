using System.Text.RegularExpressions;

namespace Trendo.Core.Office;

/// <summary>Ein Hinweis der Schreibhilfe an einer Stelle des Dokuments. <paramref name="Replacement"/> null = nur zeigen (z. B. langer Satz).</summary>
public sealed record WritingHint(string Kind, int Block, int Start, int Length, string Original, string? Replacement, string Title, string Detail)
{
    public string Key => $"text:{Kind}:{Original}:{Replacement}";
}

/// <summary>Masterprompt 131 / Schreibhilfe: lokale, nachvollziehbare Regeln – doppelte Wörter, Leerzeichen, Satzzeichen, Füllwörter,
/// einheitliche Schreibweisen (Duden), deutsche Anführungszeichen, zu lange Sätze; dazu die Rechtschreibung, wenn ein Wörterbuch da ist.
/// Nichts wird ohne „Ja“ geändert.</summary>
public static partial class WritingAid
{
    public const int LongSentenceWords = 30;

    static readonly (string Pattern, string With, string Why)[] Spellings =
    [
        (@"\bz\.\s?B\.(?<!z\. B\.)", "z. B.", "Abkürzung mit schmalem Abstand: „z. B.“"),
        (@"\bd\.h\.", "d. h.", "Abkürzung mit Abstand: „d. h.“"),
        (@"\bu\.a\.", "u. a.", "Abkürzung mit Abstand: „u. a.“"),
        (@"\bz\.T\.", "z. T.", "Abkürzung mit Abstand: „z. T.“"),
        (@"\b(?:Email|eMail|e-Mail|E-mail)\b", "E-Mail", "Duden-Schreibweise: „E-Mail“"),
        (@"\bOk\b|\bokay\b", "okay", "Einheitlich „okay“"),
        (@"\bbzgl\.(?=\S)", "bzgl. ", "Leerzeichen nach der Abkürzung"),
    ];
    static readonly string[] Fillers = ["eigentlich", "halt", "irgendwie", "quasi", "sozusagen", "gewissermaßen", "letztendlich", "ziemlich", "durchaus"];

    [GeneratedRegex(@"\b(\p{L}+)\s+\1\b", RegexOptions.IgnoreCase)] private static partial Regex DoubleWord();
    [GeneratedRegex(@"(?<=\S) {2,}(?=\S)")] private static partial Regex DoubleSpace();
    [GeneratedRegex(@" +([,.;:!?])")] private static partial Regex SpaceBeforePunct();
    [GeneratedRegex("\"([^\"\n]{1,200})\"")] private static partial Regex StraightQuotes();
    [GeneratedRegex(@"[^.!?]+[.!?]*")] private static partial Regex Sentence();

    public static List<WritingHint> Check(TextDoc doc, Spelling? spelling = null, int max = 200)
    {
        var res = new List<WritingHint>();
        for (var b = 0; b < doc.Blocks.Count && res.Count < max; b++)
        {
            if (doc.Blocks[b] is not Para p || p.Text.Length == 0) continue;
            var t = p.Text;
            foreach (Match m in DoubleWord().Matches(t))
                if (!string.Equals(m.Groups[1].Value, "das", StringComparison.OrdinalIgnoreCase) || !t[(m.Index + m.Length)..].TrimStart().StartsWith("ist")) // „das, das ist“ selten gewollt – trotzdem nur vorschlagen
                    res.Add(new("doppelt", b, m.Index, m.Length, m.Value, m.Groups[1].Value, $"Doppeltes Wort: „{m.Groups[1].Value} {m.Groups[1].Value}“", "Eines streichen?"));
            foreach (Match m in DoubleSpace().Matches(t)) res.Add(new("leer", b, m.Index, m.Length, m.Value, " ", "Doppeltes Leerzeichen", "Durch eines ersetzen?"));
            foreach (Match m in SpaceBeforePunct().Matches(t)) res.Add(new("satzzeichen", b, m.Index, m.Length, m.Value, m.Groups[1].Value, $"Leerzeichen vor „{m.Groups[1].Value}“", "Vor Satzzeichen steht kein Leerzeichen."));
            foreach (Match m in StraightQuotes().Matches(t)) res.Add(new("anfuehrung", b, m.Index, m.Length, m.Value, "„" + m.Groups[1].Value + "“", "Deutsche Anführungszeichen", $"„{Short(m.Groups[1].Value)}“ statt \"{Short(m.Groups[1].Value)}\"?"));
            foreach (var (pat, with, why) in Spellings)
                foreach (Match m in Regex.Matches(t, pat)) if (m.Value != with) res.Add(new("schreibweise", b, m.Index, m.Length, m.Value, with, $"„{m.Value}“ → „{with.Trim()}“", why));
            foreach (var f in Fillers)
                foreach (Match m in Regex.Matches(t, $@"\b{f}\b ?", RegexOptions.IgnoreCase))
                    res.Add(new("fuellwort", b, m.Index, m.Length, m.Value, "", $"Füllwort „{m.Value.Trim()}“", "Streichen macht den Satz klarer."));
            foreach (Match s in Sentence().Matches(t))
            {
                var words = s.Value.Split((char[])[' ', '\t'], StringSplitOptions.RemoveEmptyEntries).Length;
                if (words > LongSentenceWords) res.Add(new("lang", b, s.Index, s.Length, s.Value.Trim(), null, $"Langer Satz ({words} Wörter)", "Ab 30 Wörtern wird es schwer lesbar – in zwei Sätze teilen? „Ja“ markiert ihn."));
            }
        }
        if (spelling is not null)
            foreach (var m in spelling.Check(doc, 100))
                if (spelling.Suggest(m.Word).FirstOrDefault() is { } best)
                    res.Add(new("rechtschreibung", m.Block, m.Start, m.Length, m.Word, best, $"Rechtschreibung: „{m.Word}“ → „{best}“", "Vorschlag aus dem deutschen Wörterbuch."));
        // Überlappende Hinweise: der frühere gewinnt
        return res.OrderBy(h => h.Block).ThenBy(h => h.Start).Aggregate(new List<WritingHint>(), (l, h) => { if (!l.Any(x => x.Block == h.Block && h.Start < x.Start + x.Length && x.Start < h.Start + h.Length && x.Replacement is not null && h.Replacement is not null)) l.Add(h); return l; }).Take(max).ToList();
        static string Short(string s) => s.Length > 24 ? s[..24] + "…" : s;
    }

    /// <summary>Hinweis anwenden, wenn der Text an der Stelle noch stimmt. false = Stelle hat sich geändert.</summary>
    public static bool Apply(TextDoc doc, WritingHint h)
    {
        if (h.Replacement is null || h.Block >= doc.Blocks.Count || doc.Blocks[h.Block] is not Para p) return false;
        if (h.Start + h.Length > p.Text.Length || p.Text.Substring(h.Start, h.Length) != h.Original) return false;
        TextDoc.ReplaceAt(p, h.Start, h.Length, h.Replacement); return true;
    }
}
