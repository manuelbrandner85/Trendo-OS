using System.Globalization;
using System.Text;

namespace Trendo.Core.Kreativ;

/// <summary>T-0739 (STUDIO-MODELS-01): Fragenreihe in einem Durchgang gegen mehrere Modelle. Die eigentliche Frage stellt die App über den
/// gemeinsamen KI-Kern (<c>IntelligenceCore.AskAsync</c>) – dieser Baustein liest die Fragen, läuft Frage für Frage und Modell für Modell,
/// meldet den Fortschritt, hört auf „Abbrechen“ und schreibt Tabelle, CSV und Markdown. Kein eigener KI-Kern.</summary>
public static class ModelBatch
{
    public const int MaxQuestions = 200, MaxQuestionChars = 4000;
    /// <summary>Größte Fragen-Datei, die Trendo liest (1 MB reicht für 200 lange Fragen).</summary>
    public const long MaxFileBytes = 1 << 20;

    /// <summary>Ergebnis einer Frage an ein Modell. <paramref name="Question"/> zählt ab 1. Zeichen und Wörter zählt Trendo selbst an der Antwort;
    /// Tokens meldet der gemeinsame KI-Kern nicht – darum steht hier ehrlich die Zeichenzahl.</summary>
    public sealed record Result(int Question, string Text, string Source, bool Ok, string Answer, double Seconds, string Error)
    {
        public int Chars => Ok ? Answer.Length : 0;
        public int Words => Ok ? ModelCompare.Words(Answer) : 0;
        public double CharsPerSecond => Ok && Seconds > 0 ? Math.Round(Chars / Seconds, 1) : 0;
    }

    /// <summary>Stand des Durchgangs: <paramref name="Done"/> von <paramref name="Total"/> Einzelfragen erledigt, gerade an der Reihe: Frage <paramref name="Question"/> an <paramref name="Source"/>.</summary>
    public sealed record Progress(int Done, int Total, int Question, int Questions, string Source)
    {
        public int Percent => Total == 0 ? 0 : (int)Math.Round(100.0 * Done / Total);
    }

    /// <summary>Zusammenfassung je Modell.</summary>
    public sealed record Summary(string Source, int Asked, int Ok, int Failed, double AvgSeconds, double AvgChars, double CharsPerSecond);

    /// <summary>Antwort der Quelle: ob geklappt, Text (bei Fehler die Meldung), Dauer in Sekunden.</summary>
    public delegate Task<(bool Ok, string Text, double Seconds)> AskFunc(string question, string source, CancellationToken ct);

    /// <summary>Fragen aus einer Liste (eine je Zeile) oder einer Datei lesen. CSV (Endung .csv): Spalte „frage“/„question“/„prompt“, sonst die erste Spalte;
    /// Trennzeichen Semikolon, Komma oder Tab, Anführungszeichen erlaubt. TXT/Liste: jede nicht leere Zeile, Zeilen mit „#“ am Anfang sind Notizen.
    /// Doppelte Fragen bleiben (bewusst wiederholt), leere fallen weg. Mehr als <see cref="MaxQuestions"/> → Fehler mit klarer Meldung.</summary>
    public static IReadOnlyList<string> ParseQuestions(string text, string? fileName = null)
    {
        text = (text ?? "").TrimStart('\uFEFF');
        var csv = fileName is not null && fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);
        var list = new List<string>();
        if (csv)
        {
            var rows = CsvRows(text, DetectSeparator(text));
            if (rows.Count == 0) return list;
            var col = 0; var start = 0;
            var head = rows[0].Select(h => h.Trim().ToLowerInvariant()).ToList();
            var named = head.FindIndex(h => h is "frage" or "fragen" or "question" or "prompt" or "aufgabe");
            if (named >= 0) { col = named; start = 1; }
            for (var i = start; i < rows.Count; i++)
                if (col < rows[i].Count && rows[i][col].Trim() is { Length: > 0 } q) list.Add(q);
        }
        else
        {
            foreach (var raw in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
            {
                var q = raw.Trim();
                if (q.Length == 0 || q.StartsWith('#')) continue;
                list.Add(q);
            }
        }
        if (list.Count > MaxQuestions) throw new InvalidOperationException($"Zu viele Fragen: {list.Count}. Höchstens {MaxQuestions} je Durchgang – teile die Liste auf.");
        var tooLong = list.FindIndex(q => q.Length > MaxQuestionChars);
        if (tooLong >= 0) throw new InvalidOperationException($"Frage {tooLong + 1} ist länger als {MaxQuestionChars} Zeichen.");
        return list;
    }

    static char DetectSeparator(string text)
    {
        var first = text.Split('\n')[0];
        int semi = first.Count(c => c == ';'), comma = first.Count(c => c == ','), tab = first.Count(c => c == '\t');
        return tab > semi && tab > comma ? '\t' : semi >= comma && semi > 0 ? ';' : comma > 0 ? ',' : ';';
    }

    /// <summary>CSV nach RFC 4180 (Anführungszeichen, verdoppelte Anführungszeichen, Zeilenumbrüche in Feldern).</summary>
    static List<List<string>> CsvRows(string text, char sep)
    {
        var rows = new List<List<string>>(); var row = new List<string>(); var cell = new StringBuilder(); var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quoted)
            {
                if (c == '"') { if (i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; } else quoted = false; }
                else cell.Append(c);
            }
            else if (c == '"' && cell.Length == 0) quoted = true;
            else if (c == sep) { row.Add(cell.ToString()); cell.Clear(); }
            else if (c is '\n' or '\r')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                row.Add(cell.ToString()); cell.Clear();
                if (row.Any(x => x.Length > 0)) rows.Add(row);
                row = [];
            }
            else cell.Append(c);
        }
        row.Add(cell.ToString());
        if (row.Any(x => x.Length > 0)) rows.Add(row);
        return rows;
    }

    /// <summary>Der Durchgang: jede Frage an jedes Modell, der Reihe nach (Modelle auf einem PC teilen sich die Grafikkarte – nacheinander ist fair und
    /// misst die Zeit ohne Störung). <paramref name="done"/> sind schon vorhandene Ergebnisse (Fortsetzen nach Abbruch) – die werden übersprungen.
    /// Fehler einer Quelle stehen als Fehlerzeile in der Tabelle, der Rest läuft weiter. „Abbrechen“ wirkt sofort, auch mitten in einer
    /// laufenden Antwort (die zählt dann nicht); alle bis dahin erhaltenen Antworten bleiben.</summary>
    public static async Task<(IReadOnlyList<Result> Results, bool Cancelled)> RunAsync(IReadOnlyList<string> questions, IReadOnlyList<string> sources, AskFunc ask,
        Action<Progress, Result?>? progress = null, IReadOnlyList<Result>? done = null, CancellationToken ct = default)
    {
        if (questions.Count == 0) throw new InvalidOperationException("Die Fragenreihe ist leer.");
        if (sources.Count == 0) throw new InvalidOperationException("Wähle zuerst mindestens ein Modell aus.");
        var results = new List<Result>(done ?? []);
        var have = results.Select(r => (r.Question, r.Source)).ToHashSet();
        var total = questions.Count * sources.Count; var count = results.Count(r => r.Question >= 1 && r.Question <= questions.Count && sources.Contains(r.Source));
        for (var q = 0; q < questions.Count; q++)
            foreach (var s in sources)
            {
                if (have.Contains((q + 1, s))) continue;
                if (ct.IsCancellationRequested) return (results, true);
                progress?.Invoke(new(count, total, q + 1, questions.Count, s), null);
                Result r;
                try
                {
                    var (ok, text, sec) = await ask(questions[q], s, ct);
                    r = ok ? new(q + 1, questions[q], s, true, text, Math.Round(Math.Max(0, sec), 2), "")
                           : new(q + 1, questions[q], s, false, "", Math.Round(Math.Max(0, sec), 2), string.IsNullOrWhiteSpace(text) ? "keine Antwort" : text.Trim());
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { return (results, true); }
                catch (Exception ex) when (ex is not OperationCanceledException) { r = new(q + 1, questions[q], s, false, "", 0, ex.Message); }
                results.Add(r); have.Add((q + 1, s)); count++;
                progress?.Invoke(new(count, total, q + 1, questions.Count, s), r);
            }
        return (results, false);
    }

    public static IReadOnlyList<Summary> Summarize(IEnumerable<Result> results) =>
        results.GroupBy(r => r.Source).Select(g =>
        {
            var ok = g.Where(r => r.Ok).ToList();
            return new Summary(g.Key, g.Count(), ok.Count, g.Count() - ok.Count, ok.Count == 0 ? 0 : Math.Round(ok.Average(r => r.Seconds), 2),
                ok.Count == 0 ? 0 : Math.Round(ok.Average(r => (double)r.Chars), 1), ok.Count == 0 || ok.Sum(r => r.Seconds) <= 0 ? 0 : Math.Round(ok.Sum(r => r.Chars) / ok.Sum(r => r.Seconds), 1));
        }).ToList();

    static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");
    static string CsvCell(string s) => KreativSafe.CsvCell(s); // Gegenprüfung T-0739: Formeln entschärft, reine Zahlen bleiben
    static string MdCell(string s) => s.Replace("|", "\\|").Replace("\r", "").Replace("\n", " ");

    /// <summary>Eine Zeile je Frage und Modell, Semikolon, Dezimalpunkt (für Tabellenprogramme und Skripte gleich lesbar). Zeilenende „\n“.</summary>
    public static string Csv(IEnumerable<Result> results)
    {
        var sb = new StringBuilder("nr;frage;modell;ok;sekunden;zeichen;woerter;zeichen_je_sekunde;antwort;fehler\n");
        foreach (var r in results.OrderBy(r => r.Question))
            sb.Append(CultureInfo.InvariantCulture, $"{r.Question};{CsvCell(r.Text)};{CsvCell(r.Source)};{(r.Ok ? "ja" : "nein")};{r.Seconds:0.00};{r.Chars};{r.Words};{r.CharsPerSecond:0.0};{CsvCell(r.Answer.Trim())};{CsvCell(r.Error)}\n");
        return sb.ToString();
    }

    /// <summary>Bericht: Übersicht je Modell, dann je Frage alle Antworten mit Zeit, Zeichen und Fehler.</summary>
    public static string Markdown(IReadOnlyList<Result> results, string title, int questionCount, bool cancelled)
    {
        var sources = results.Select(r => r.Source).Distinct().ToList();
        var sb = new StringBuilder($"# {title}\n\n");
        sb.Append(De, $"Fragenreihe: {questionCount} Fragen · {sources.Count} Modelle · {results.Count} Antworten{(cancelled ? " · **abgebrochen**" : "")}.\n\n");
        sb.Append("| Modell | Gefragt | Antworten | Fehler | Dauer (Schnitt) | Zeichen (Schnitt) | Zeichen/s |\n|---|---:|---:|---:|---:|---:|---:|\n");
        foreach (var s in Summarize(results))
            sb.Append(De, $"| {MdCell(s.Source)} | {s.Asked} | {s.Ok} | {s.Failed} | {s.AvgSeconds:0.00} s | {s.AvgChars:0} | {s.CharsPerSecond:0.0} |\n");
        foreach (var g in results.GroupBy(r => r.Question).OrderBy(g => g.Key))
        {
            sb.Append(De, $"\n## Frage {g.Key}\n\n{g.First().Text}\n\n| Modell | Zeit | Zeichen | Antwort / Fehler |\n|---|---:|---:|---|\n");
            foreach (var r in g)
                sb.Append(De, $"| {MdCell(r.Source)} | {r.Seconds:0.00} s | {r.Chars} | {(r.Ok ? MdCell(r.Answer.Trim()) : "⚠ " + MdCell(r.Error))} |\n");
        }
        return sb.ToString();
    }
}
