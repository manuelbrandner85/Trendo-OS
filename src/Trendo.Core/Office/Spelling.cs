using System.Text.RegularExpressions;
using WeCantSpell.Hunspell;

namespace Trendo.Core.Office;

/// <summary>T8: Rechtschreibprüfung Deutsch, offline. Hunspell-Nachbau WeCantSpell.Hunspell 7.0.1 (MPL 1.1) mit dem deutschen Wörterbuch „frami“
/// (GPL 2/3) aus dem mitgelieferten LibreOffice-Paket – das Wörterbuch liegt nicht in Trendo selbst, sondern im Vollpaket. Eigene Wörter im Datenordner.</summary>
public sealed partial class Spelling
{
    public sealed record Mistake(int Block, int Start, int Length, string Word);

    private readonly WordList _list;
    private readonly HashSet<string> _own;
    private readonly string? _ownFile;
    public string Source { get; }

    private Spelling(WordList list, string source, string? ownFile)
    {
        _list = list; Source = source; _ownFile = ownFile;
        _own = ownFile is not null && File.Exists(ownFile) ? File.ReadAllLines(ownFile).Where(l => l.Trim().Length > 0).Select(l => l.Trim()).ToHashSet(StringComparer.Ordinal) : [];
    }

    /// <summary>Bekannte Orte des deutschen Wörterbuchs: Vollpaket, installiertes LibreOffice, Linux-Paket hunspell-de.</summary>
    public static IEnumerable<string> Candidates(string toolsRoot) =>
    [
        Path.Combine(toolsRoot, "libreoffice", "App", "libreoffice", "share", "extensions", "dict-de", "de_DE_frami.dic"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "LibreOffice", "share", "extensions", "dict-de", "de_DE_frami.dic"),
        "/usr/share/hunspell/de_DE.dic", "/usr/share/hunspell/de_DE_frami.dic", "/usr/share/myspell/de_DE.dic",
    ];

    /// <summary>Lädt das erste vorhandene Wörterbuch; null, wenn keins da ist (dann sagt die Oberfläche, wie man es bekommt).</summary>
    public static Spelling? Load(IEnumerable<string> dicCandidates, string? ownWordsFile = null)
    {
        foreach (var dic in dicCandidates)
        {
            var aff = Path.ChangeExtension(dic, ".aff");
            if (!File.Exists(dic) || !File.Exists(aff)) continue;
            return new Spelling(WordList.CreateFromFiles(dic, aff), dic, ownWordsFile);
        }
        return null;
    }

    [GeneratedRegex(@"[\p{L}][\p{L}\p{M}'’\-]*[\p{L}]|[\p{L}]")]
    private static partial Regex WordRx();

    public bool Correct(string word)
    {
        var w = word.Trim('\'', '’', '-');
        if (w.Length <= 1 || w.Any(char.IsDigit) || _own.Contains(w)) return true;
        if (w.Length <= 4 && w.All(char.IsUpper)) return true; // Abkürzungen wie GmbH-artige Kürzel, USB
        if (_list.Check(w)) return true;
        // Bindestrich-Wörter: jedes Teil prüfen (z. B. „E-Mail-Adresse“)
        return w.Contains('-') && w.Split('-', StringSplitOptions.RemoveEmptyEntries).All(part => part.Length <= 1 || _list.Check(part) || _own.Contains(part));
    }

    public IReadOnlyList<string> Suggest(string word) => _list.Suggest(word).Take(6).ToList();

    /// <summary>Alle unbekannten Wörter in Absätzen (Tabellen und Bilder werden übersprungen).</summary>
    public List<Mistake> Check(TextDoc doc, int max = 500)
    {
        var res = new List<Mistake>();
        for (var b = 0; b < doc.Blocks.Count && res.Count < max; b++)
        {
            if (doc.Blocks[b] is not Para p) continue;
            var text = p.Text;
            foreach (Match m in WordRx().Matches(text))
            {
                // Adressen und Webseiten nicht prüfen
                var left = m.Index > 0 ? text[m.Index - 1] : ' ';
                if (left is '@' or '/' or '.' && m.Index > 1 && !char.IsWhiteSpace(text[m.Index - 2])) continue;
                if (!Correct(m.Value)) res.Add(new Mistake(b, m.Index, m.Length, m.Value));
                if (res.Count >= max) break;
            }
        }
        return res;
    }

    /// <summary>Wort ins eigene Wörterbuch aufnehmen (wird danach nicht mehr angestrichen).</summary>
    public void AddOwn(string word)
    {
        var w = word.Trim(); if (w.Length == 0 || !_own.Add(w)) return;
        if (_ownFile is null) return;
        Directory.CreateDirectory(Path.GetDirectoryName(_ownFile)!);
        File.AppendAllLines(_ownFile, [w]);
    }
}
