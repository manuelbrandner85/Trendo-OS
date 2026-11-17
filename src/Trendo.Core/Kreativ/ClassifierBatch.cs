using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

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

    /// <param name="Total">So viele Zeilen bzw. Dateien hatte die Quelle (ohne Kopfzeile) – mehr als die bearbeiteten, wenn <see cref="MaxItems"/> griff.</param>
    /// <param name="Truncated">Die Quelle hatte mehr als <see cref="MaxItems"/> Einträge; nur die ersten wurden eingeordnet.</param>
    /// <param name="HasHeader">CSV: Die erste Zeile wurde als Überschrift gelesen.</param>
    public sealed record Result(IReadOnlyList<string> Header, IReadOnlyList<IReadOnlyList<string>> Rows, int Skipped, string Source, string Kind, int TextColumn = 0,
        int Total = 0, bool Truncated = false, bool HasHeader = false);

    static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");
    static readonly string[] TextNames = ["text", "satz", "beispiel", "inhalt", "nachricht", "betreff", "beschreibung", "message", "content"];

    /// <summary>Spalte mit dem Text finden: eine Kopfzeile mit „text“, „inhalt“ … gewinnt, sonst die Spalte mit den längsten Einträgen.
    /// Mit <paramref name="model"/>: Ein kurzes erstes Wort, das das Modell aus dem Training kennt (z. B. „Rechnung“), ist Inhalt, keine Überschrift.</summary>
    public static (bool HasHeader, int Column) DetectTextColumn(IReadOnlyList<IReadOnlyList<string>> rows, TextClassifier? model = null)
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
        // … aber ein Wort, das das Modell aus dem Training kennt, ist Inhalt (eine Liste ohne Kopf, die mit „Rechnung“ beginnt)
        if (looksHeader && model is not null && TextClassifier.Tokenize(head[best], model.RemoveStopwords).Count > 0 && model.Predict(head[best]).KnownWords > 0) looksHeader = false;
        return (looksHeader, best);
    }

    /// <summary>Jede Zeile der CSV-Datei einordnen. Alle Spalten bleiben erhalten, dazu kommen „Vorhersage“, „Sicherheit“ und „Bekannte Wörter“.</summary>
    /// <param name="header">null = selbst erkennen; true/false = „Erste Zeile ist Überschrift“, wie in der Oberfläche gewählt.</param>
    public static Result ApplyCsv(TextClassifier model, string csv, string source, IProgress<(int Done, int Total)>? progress = null, CancellationToken ct = default, bool? header = null)
    {
        var parsed = Trendo.Core.Sys.Csv.Parse((csv ?? "").TrimStart('﻿')).Select(r => (IReadOnlyList<string>)r).ToList();
        while (parsed.Count > 0 && parsed[^1].All(c => c.Length == 0)) parsed.RemoveAt(parsed.Count - 1);
        if (parsed.Count == 0) throw new InvalidDataException("Die CSV-Datei ist leer.");
        var (hasHeader, col) = DetectTextColumn(parsed, model);
        if (header is { } h) hasHeader = h;
        var cols = parsed.Max(r => r.Count);
        var head = hasHeader ? parsed[0].Select(x => x.Trim()).Concat(Enumerable.Repeat("", Math.Max(0, cols - parsed[0].Count))).ToList()
                             : Enumerable.Range(1, cols).Select(i => i - 1 == col ? "Text" : $"Spalte {i}").ToList();
        head.AddRange(["Vorhersage", "Sicherheit", "Bekannte Wörter"]);
        var available = parsed.Count - (hasHeader ? 1 : 0);
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
        return new(head, rows, skipped, source, "csv", col, available, available > MaxItems, hasHeader);
    }

    /// <summary>CSV-Datei lesen und einordnen: UTF-8 mit oder ohne Kennung, UTF-16 mit Kennung, sonst Windows-1252 (so speichert deutsches Excel „CSV (Trennzeichen-getrennt)“).</summary>
    public static Result ApplyCsvFile(TextClassifier model, string path, IProgress<(int Done, int Total)>? progress = null, CancellationToken ct = default, bool? header = null) =>
        ApplyCsv(model, Trendo.Core.Sys.TextFileCodec.Decode(File.ReadAllBytes(path)).Text, path, progress, ct, header);

    /// <summary>Alle Textdateien eines Ordners (mit Unterordnern) einordnen: je Datei eine Zeile.</summary>
    public static Result ApplyFolder(TextClassifier model, string dir, IProgress<(int Done, int Total)>? progress = null, CancellationToken ct = default)
    {
        if (!Directory.Exists(dir)) throw new DirectoryNotFoundException("Den Ordner gibt es nicht.");
        var all = Directory.EnumerateFiles(dir, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true })
            .Where(f => TextExtensions.Contains(Path.GetExtension(f).ToLowerInvariant())).Order(StringComparer.OrdinalIgnoreCase).ToList();
        var files = all.Take(MaxItems).ToList();
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
        return new(["Datei", "Vorhersage", "Sicherheit", "Bekannte Wörter", "Hinweis"], rows, skipped, dir, "ordner", 0, all.Count, all.Count > MaxItems);
    }

    /// <summary>Anfang einer Textdatei lesen; Kodierung wie beim Öffnen im Editor erkennen (UTF-8 mit/ohne Kennung, UTF-16 mit Kennung, sonst Windows-1252).</summary>
    static string ReadText(string f)
    {
        using var fs = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var buf = new byte[(int)Math.Min(fs.Length, MaxCharsPerFile * 4L)];
        var n = fs.ReadAtLeast(buf, buf.Length, throwOnEndOfStream: false);
        if (fs.Length > n) n = KiAttachments.WholeChars(buf, n); // nicht mitten in einem Zeichen aufhören (sonst hielte die Erkennung UTF-8 für Windows-1252)
        var text = Trendo.Core.Sys.TextFileCodec.Decode(n == buf.Length ? buf : buf[..n]).Text;
        return text.Length > MaxCharsPerFile ? text[..MaxCharsPerFile] : text;
    }

    static string[] Cells(TextClassifier model, string text)
    {
        if (TextClassifier.Tokenize(text, model.RemoveStopwords).Count == 0) return ["", "", "0"];
        var p = model.Predict(text);
        return [p.Label, (p.Confidence * 100).ToString("0", De) + " %", p.KnownWords.ToString(CultureInfo.InvariantCulture)];
    }

    /// <summary>Reine Zahl (auch mit Vorzeichen, Dezimalkomma oder -punkt, Exponent, Prozent) – die bleibt beim Entschärfen unverändert.</summary>
    static readonly Regex PlainNumber = new(@"^[+-]?(\d+([.,]\d+)*|[.,]\d+)([eE][+-]?\d+)?\s?%?$", RegexOptions.CultureInvariant);

    /// <summary>Formel-Einschleusung verhindern (OWASP „CSV Injection“): Zellen, die mit = + - @ Tab oder Wagenrücklauf beginnen, bekommen ein Hochkomma –
    /// außer reinen Zahlen wie -5 oder +3,2, die aus der Quelle durchgereicht werden.</summary>
    public static string Safe(string s) => s.Length > 0 && s[0] is '=' or '+' or '-' or '@' or '\t' or '\r' && !PlainNumber.IsMatch(s) ? "'" + s : s;

    /// <summary>Als CSV (Semikolon, Zeilenende LF wie der Beispiel-Export; die BOM setzt der Aufrufer beim Schreiben, damit Excel und LibreOffice Umlaute richtig zeigen).</summary>
    public static string ToCsv(Result r)
    {
        static string Q(string s) => s.IndexOfAny([';', '"', '\n', '\r']) >= 0 || s.StartsWith(' ') || s.EndsWith(' ') ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
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
