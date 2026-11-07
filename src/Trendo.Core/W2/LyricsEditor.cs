using System.Text.Json;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>Trendo Musik V6 wie Suno (07.11.2026): Liedtext-Editor – Zeilen und Abschnitte bearbeiten, „Texter“ (eigene Beispieltexte als Schreibstil),
/// Anweisungen an die Text-KI für eine Zeile, für Zeilenvarianten oder den ganzen Text. Texter speichert nur Texte, die der Nutzer selbst eingibt (eigene Texte).</summary>
public static class LyricsEditor
{
    static readonly Regex Section = new(@"^\s*\[(?<n>[^\]]+)\]\s*$", RegexOptions.Compiled);

    /// <summary>Zeile, in der die Schreibmarke steht (0-basiert).</summary>
    public static int LineAt(string text, int caret)
    {
        caret = Math.Clamp(caret, 0, (text ?? "").Length);
        return (text ?? "")[..caret].Count(c => c == '\n');
    }

    public static string[] Lines(string text) => (text ?? "").Replace("\r\n", "\n").Split('\n');

    /// <summary>Eine Zeile ersetzen (Abschnittsmarken bleiben unangetastet).</summary>
    public static string ReplaceLine(string text, int line, string replacement)
    {
        var l = Lines(text);
        if (line < 0 || line >= l.Length) throw new ArgumentOutOfRangeException(nameof(line), "Diese Zeile gibt es nicht.");
        if (Section.IsMatch(l[line])) throw new ArgumentException("Das ist eine Abschnittsmarke – bitte eine Textzeile wählen.");
        l[line] = replacement.Replace("\r", "").Replace("\n", " ").Trim();
        return string.Join("\n", l);
    }

    /// <summary>Abschnitt (z. B. „Refrain“), in dem die Zeile steht – oder null.</summary>
    public static string? SectionOf(string text, int line)
    {
        var l = Lines(text);
        for (var i = Math.Min(line, l.Length - 1); i >= 0; i--) if (Section.Match(l[i]) is { Success: true } m) return m.Groups["n"].Value;
        return null;
    }

    /// <summary>Abschnittsmarke an der Schreibmarke einfügen (eigene Zeile).</summary>
    public static (string Text, int Caret) InsertSection(string text, int caret, string label)
    {
        text ??= ""; caret = Math.Clamp(caret, 0, text.Length);
        var before = text[..caret]; var after = text[caret..];
        var tag = (before.Length > 0 && !before.EndsWith('\n') ? "\n" : "") + $"[{label}]\n";
        return (before + tag + after, caret + tag.Length);
    }

    /// <summary>Statistik für die Kopfzeile: Zeilen, Wörter, Abschnitte.</summary>
    public static (int Lines, int Words, int Sections) Stats(string text)
    {
        var l = Lines(text).Where(x => x.Trim().Length > 0).ToList();
        var sections = l.Count(x => Section.IsMatch(x));
        var words = l.Where(x => !Section.IsMatch(x)).Sum(x => x.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);
        return (l.Count - sections, words, sections);
    }

    // ───────── Anweisungen an die Text-KI ─────────
    static string Style(Lyricist? who) => who is null || who.Examples.Count == 0 ? "" :
        $" Schreibe im Stil dieser eigenen Texte des Nutzers (Wortwahl, Bilder, Satzlänge, Reimart) – übernimm keine Zeilen daraus:\n---\n{string.Join("\n---\n", who.Examples.Take(4))}\n---\n";

    /// <summary>Eine Zeile nach Wunsch umschreiben (Silbenzahl und Reim auf die Nachbarzeilen erhalten).</summary>
    public static string LinePrompt(string text, int line, string wish, Lyricist? who)
    {
        var l = Lines(text); var cur = l[line]; var prev = line > 0 ? l[line - 1] : ""; var next = line + 1 < l.Length ? l[line + 1] : "";
        return $"Liedtext-Zeile umschreiben. Wunsch: {wish.Trim()}. Zeile: „{cur}“. Vorherige Zeile: „{prev}“. Nächste Zeile: „{next}“. Abschnitt: {SectionOf(text, line) ?? "unbekannt"}. "
            + $"Gleiche Sprache, ähnliche Silbenzahl ({SongTools.Syllables(cur)}), Reim zu den Nachbarzeilen erhalten." + Style(who) + " Gib nur die neue Zeile aus, ohne Anführungszeichen.";
    }

    /// <summary>Drei Varianten einer Zeile zur Auswahl.</summary>
    public static string VariantsPrompt(string text, int line, Lyricist? who)
    {
        var l = Lines(text);
        return $"Gib drei Varianten dieser Liedtext-Zeile: „{l[line]}“ (Abschnitt {SectionOf(text, line) ?? "unbekannt"}). Gleiche Sprache, ähnliche Silbenzahl ({SongTools.Syllables(l[line])}), passend zum Rest:\n{text}\n"
            + Style(who) + "Gib genau drei Zeilen aus, eine je Zeile, ohne Nummern und ohne Anführungszeichen.";
    }

    /// <summary>Den ganzen Text nach Wunsch überarbeiten (z. B. „Reimschema AABB“, „lustiger“) – Aufbau mit Abschnittsmarken erhalten.</summary>
    public static string WholePrompt(string text, string wish, Lyricist? who) =>
        $"Überarbeite diesen Liedtext. Wunsch: {wish.Trim()}. Behalte die Abschnittsmarken in eckigen Klammern und den Aufbau. " + LyricsCraft.Rules + Style(who)
        + $" Keine Namen echter Künstler, keine Zeilen aus bekannten Liedern. Gib nur den Liedtext aus.\n---\n{text}";

    /// <summary>Antwort der KI für Varianten → bis zu drei saubere Zeilen.</summary>
    public static List<string> ParseVariants(string answer) =>
        [.. (answer ?? "").Replace("\r", "").Split('\n').Select(x => Regex.Replace(x.Trim(), @"^(\d+[.)]|[-•*])\s*", "").Trim('"', '„', '“', ' ')).Where(x => x.Length > 1 && !x.StartsWith('[')).Distinct().Take(3)];

    public static string CleanLine(string answer) => ParseVariants(answer).FirstOrDefault() ?? throw new InvalidOperationException("Die KI hat keine Zeile geliefert.");
}

/// <summary>V6 „Texter“: eigene Beispieltexte als Schreibstil (wie Sunos „Lyricist“) – lokal in musik/texter.json.</summary>
public sealed class Lyricist
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "";
    public List<string> Examples { get; set; } = [];
}

public sealed class Lyricists(string dataRoot)
{
    static readonly JsonSerializerOptions Opt = new() { WriteIndented = true };
    string FilePath => Path.Combine(dataRoot, "musik", "texter.json");

    public List<Lyricist> All()
    {
        try { return File.Exists(FilePath) ? JsonSerializer.Deserialize<List<Lyricist>>(File.ReadAllText(FilePath), Opt) ?? [] : []; }
        catch (JsonException) { return []; }
    }

    void Save(List<Lyricist> l) { Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!); var t = FilePath + ".neu"; File.WriteAllText(t, JsonSerializer.Serialize(l, Opt)); SafeFile.Move(t, FilePath); }

    /// <summary>Beispieltext zu einem Texter hinzufügen (neu anlegen, wenn es den Namen nicht gibt). Mindestens 4 Zeilen, höchstens 8 Beispiele.</summary>
    public Lyricist AddExample(string name, string example)
    {
        var n = name.Trim(); if (n.Length == 0) throw new ArgumentException("Bitte dem Texter einen Namen geben.");
        var ex = example.Replace("\r", "").Trim();
        if (ex.Split('\n').Count(x => x.Trim().Length > 0) < 4) throw new ArgumentException("Ein Beispiel braucht mindestens 4 Zeilen eigenen Text.");
        var l = All(); var who = l.FirstOrDefault(x => x.Name.Equals(n, StringComparison.CurrentCultureIgnoreCase));
        if (who is null) { who = new Lyricist { Name = n }; l.Add(who); }
        if (!who.Examples.Contains(ex)) who.Examples.Add(ex);
        if (who.Examples.Count > 8) who.Examples.RemoveAt(0);
        Save(l); return who;
    }

    public Lyricist? Get(string id) => All().FirstOrDefault(x => x.Id == id);
    public void Delete(string id) { var l = All(); l.RemoveAll(x => x.Id == id); Save(l); }
}
