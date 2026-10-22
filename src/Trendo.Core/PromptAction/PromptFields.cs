using System.Text;

namespace Trendo.Core.PromptAction;

/// <summary>Liest „Feld: Wert“-Zeilen. Fortsetzungszeilen gehören zum letzten Feld. Anhänge werden hier nicht eingemischt.</summary>
public static class PromptFields
{
    public static Dictionary<string, string> Read(string? prompt)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? current = null;
        foreach (var raw in (prompt ?? "").Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.TrimEnd();
            if (line.Trim().Length == 0) { current = null; continue; }
            var idx = line.IndexOf(':');
            var key = idx > 0 ? Norm(line[..idx]) : "";
            if (idx > 1 && idx < 48 && key.Length >= 2 && key.All(c => char.IsLetterOrDigit(c) || c is '-' or '_'))
            {
                current = key;
                var value = line[(idx + 1)..].Trim();
                map[current] = map.TryGetValue(current, out var old) && old.Length > 0 ? old + "\n" + value : value;
            }
            else if (current is not null)
            {
                map[current] = map[current].Length == 0 ? line.Trim() : map[current] + "\n" + line.Trim();
            }
        }
        return map;
    }

    public static string Norm(string key)
    {
        var s = key.Trim().ToLowerInvariant().Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue").Replace("ß", "ss");
        var b = new StringBuilder(s.Length);
        foreach (var c in s)
            if (char.IsLetterOrDigit(c)) b.Append(c);
            else if (c is ' ' or '-' or '_') b.Append('-');
        return b.ToString().Trim('-');
    }
}
