using System.Globalization;
using System.Text;

namespace Trendo.Core.Kreativ;

/// <summary>T-0740 (STUDIO-TRAIN-01): Das trainierte Modell im Stapel anwenden – auf jede Zeile einer CSV-Datei oder auf jede Textdatei eines Ordners.
/// Ergebnis ist eine Tabelle mit Vorhersage und Sicherheit je Zeile bzw. je Datei; Fortschritt wird gemeldet, Abbrechen wirft <see cref="OperationCanceledException"/>.</summary>
public static class ClassifierBatch
{
    /// <summary>Höchstens so viele Zeilen bzw. Dateien je Lauf (wie beim CSV-Import der Beispiele).</summary>
    public const int MaxItems = 50000;
    /// <summary>Höchstens so viele Zeichen je Datei werden gelesen.</summary>
    public const int MaxCharsPerFile = 200_000;
    /// <summary>Dateiarten, die als Text gelesen werden.</summary>
    public static readonly string[] TextExtensions = [".txt", ".md", ".eml", ".csv", ".log", ".html", ".htm", ".json", ".xml"];

    public sealed record Result(IReadOnlyList<string> Header, IReadOnlyList<IReadOnlyList<string>> Rows, int Skipped, string Source, string Kind, int TextColumn = 0);

    static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");
    static readonly string[] TextNames = ["text", "satz", "beispiel", "inhalt", "nachricht", "betreff", "beschreibung", "message", "content"];

    /// <summary>Spalte mit dem Text finden: eine Kopfzeile mit „text“, „inhalt“ … gewinnt, sonst die Spalte mit den längsten Einträgen.</summary>
    public static (bool HasHeader, int Column) DetectTextColumn(IReadOnlyList<IReadOnlyList<string>> rows)
    {
        if (rows.Count == 0) return (false, 0);
        var head = rows[0];
        for (var i = 0; i < head.Count; i++)
            if (TextNames.Contains(head[i].Trim().ToLowerInvariant())) return (true, i);
        var cols = rows.Max(r => r.Count);
        var best = 0; var bestLen = -1.0;
        for (var c = 0; c < cols; c++)
        {
            var avg = rows.Skip(1).Take(200).Select(r => c < r.Count ? r[c].Length : 0).DefaultIfEmpty(0).Average();
            if (avg > bestLen) { bestLen = avg; best = c; }
        }
        // Kopfzeile: erste Zeile hat in der Textspalte etwas Kurzes ohne Leerzeichen, der Rest ist länger
        var looksHeader = rows.Count > 1 && best < head.Count && head[best].Trim().Length is > 0 and < 30 && !head[best].Trim().Contains(' ') && bestLen > head[best].Length;
        return (looksHeader, best);
    }

    /// <summary>Jede Zeile der CSV-Datei einordnen. Alle Spalten bleiben erhalten, dazu kommen „Vorhersage“, „Sicherheit“ und „Bekannte Wörter“.</summary>
    public static Result ApplyCsv(TextClassifier model, string csv, string source, IProgress<(int Done, int Total)>? progress = null, CancellationToken ct = default)
    {
        var parsed = Trendo.Core.Sys.Csv.Parse((csv ?? "").TrimStart('﻿'), MaxItems + 1).Select(r => (IReadOnlyList<string>)r).ToList();
        while (parsed.Count > 0 && parsed[^1].All(c => c.Length == 0)) parsed.RemoveAt(parsed.Count - 1);
        if (parsed.Count == 0) throw new InvalidDataException("Die CSV-Datei ist leer.");
        var (hasHeader, col) = DetectTextColumn(parsed);
        var cols = parsed.Max(r => r.Count);
        var header = hasHeader ? parsed[0].Select(h => h.Trim()).Concat(Enumerable.Repeat("", Math.Max(0, cols - parsed[0].Count))).ToList()
                               : Enumerable.Range(1, cols).Select(i => i - 1 == col ? "Text" : $"Spalte {i}").ToList();
        header.AddRange(["Vorhersage", "Sicherheit", "Bekannte Wörter"]);
        var data = parsed.Skip(hasHeader ? 1 : 0).Take(MaxItems).ToList();
        var rows = new List<IReadOnlyList<string>>(data.Count); var skipped = 0;
        for (var i = 0; i < data.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var r = data[i];
            var cells = r.Concat(Enumerable.Repeat("", Math.Max(0, cols - r.Count))).ToList();
            var text = col < r.Count ? r[col] : "";
            if (r.All(c => c.Trim().Length == 0)) { skipped++; continue; }
            cells.AddRange(Cells(model, text));
            rows.Add(cells);
            if ((i & 63) == 0 || i == data.Count - 1) progress?.Report((i + 1, data.Count));
        }
        return new(header, rows, skipped, source, "csv", col);
    }

    /// <summary>Alle Textdateien eines Ordners (mit Unterordnern) einordnen: je Datei eine Zeile.</summary>
    public static Result ApplyFolder(TextClassifier model, string dir, IProgress<(int Done, int Total)>? progress = null, CancellationToken ct = default)
    {
        if (!Directory.Exists(dir)) throw new DirectoryNotFoundException("Den Ordner gibt es nicht.");
        var files = Directory.EnumerateFiles(dir, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true })
            .Where(f => TextExtensions.Contains(Path.GetExtension(f).ToLowerInvariant())).Order(StringComparer.OrdinalIgnoreCase).Take(MaxItems).ToList();
        if (files.Count == 0) throw new InvalidDataException("In diesem Ordner liegen keine Textdateien (" + string.Join(", ", TextExtensions) + ").");
        var rows = new List<IReadOnlyList<string>>(files.Count); var skipped = 0;
        for (var i = 0; i < files.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var f = files[i];
            string text;
            try { text = ReadText(f); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { skipped++; rows.Add([Path.GetRelativePath(dir, f), "", "", "", "nicht lesbar"]); continue; }
            rows.Add([Path.GetRelativePath(dir, f), .. Cells(model, text), ""]);
            progress?.Report((i + 1, files.Count));
        }
        return new(["Datei", "Vorhersage", "Sicherheit", "Bekannte Wörter", "Hinweis"], rows, skipped, dir, "ordner");
    }

    static string ReadText(string f)
    {
        using var r = new StreamReader(f, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var buf = new char[MaxCharsPerFile]; var n = r.ReadBlock(buf, 0, buf.Length);
        return new string(buf, 0, n);
    }

    static string[] Cells(TextClassifier model, string text)
    {
        if (TextClassifier.Tokenize(text, model.RemoveStopwords).Count == 0) return ["", "", "0"];
        var p = model.Predict(text);
        return [p.Label, (p.Confidence * 100).ToString("0", De) + " %", p.KnownWords.ToString(CultureInfo.InvariantCulture)];
    }

    /// <summary>Als CSV (Semikolon, Zeilenende LF wie der Beispiel-Export; die BOM setzt der Aufrufer beim Schreiben, damit Excel und LibreOffice Umlaute richtig zeigen).</summary>
    public static string ToCsv(Result r)
    {
        static string Q(string s) => s.IndexOfAny([';', '"', '\n', '\r']) >= 0 || s.StartsWith(' ') || s.EndsWith(' ') ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
        // Formel-Einschleusung verhindern: Zellen, die wie eine Formel beginnen, bekommen ein Hochkomma
        static string Safe(string s) => s.Length > 0 && s[0] is '=' or '+' or '-' or '@' ? "'" + s : s;
        var sb = new StringBuilder();
        sb.Append(string.Join(";", r.Header.Select(h => Q(Safe(h))))).Append('\n');
        foreach (var row in r.Rows) sb.Append(string.Join(";", row.Select(c => Q(Safe(c))))).Append('\n');
        return sb.ToString();
    }

    /// <summary>Wie viele Zeilen je Kategorie – für die Kurzübersicht.</summary>
    public static IReadOnlyList<(string Label, int Count)> Summary(Result r)
    {
        var idx = r.Kind == "ordner" ? 1 : r.Header.Count - 3;
        return r.Rows.Select(x => idx < x.Count ? x[idx] : "").GroupBy(l => l.Length == 0 ? "(nicht einzuordnen)" : l).Select(g => (g.Key, g.Count())).OrderByDescending(x => x.Item2).ThenBy(x => x.Key, StringComparer.Ordinal).ToList();
    }
}
