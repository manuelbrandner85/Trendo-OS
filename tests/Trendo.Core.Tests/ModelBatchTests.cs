using Trendo.Core.Kreativ;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0739 (STUDIO-MODELS-01): Fragenreihe – Fragen lesen (Liste, TXT, CSV), Durchgang mit Fortschritt, Fehler je Frage, Abbrechen, Fortsetzen, Export.
/// Die Quelle ist hier eine Funktion im Test (ersetzt); der echte Lauf gegen Ollama steht in <c>ModelsBatchLive_Ui</c>.</summary>
public class ModelBatchTests
{
    [Fact(DisplayName = "Fragenreihe: TXT-Liste – leere Zeilen und #-Notizen fallen weg, Reihenfolge bleibt")]
    public void Parse_txt()
    {
        var q = ModelBatch.ParseQuestions("﻿# Notiz\r\nErste Frage?\r\n\r\n  Zweite Frage?  \nDritte;mit Semikolon\n");
        Assert.Equal(["Erste Frage?", "Zweite Frage?", "Dritte;mit Semikolon"], q);
    }

    [Fact(DisplayName = "Fragenreihe: CSV mit Kopfzeile „Frage“, Anführungszeichen, Zeilenumbruch im Feld und Komma-Trenner")]
    public void Parse_csv()
    {
        var semi = ModelBatch.ParseQuestions("nr;Frage;Soll\n1;\"Was ist \"\"Trendo\"\"?\";x\n2;\"Zwei\nZeilen\";y\n3;;z\n", "fragen.csv");
        Assert.Equal(["Was ist \"Trendo\"?", "Zwei\nZeilen"], semi);
        var comma = ModelBatch.ParseQuestions("question,expected\r\n\"Hallo, Welt?\",a\r\nB?,b\r\n", "q.CSV");
        Assert.Equal(["Hallo, Welt?", "B?"], comma);
        var noHead = ModelBatch.ParseQuestions("Wie spät ist es?;egal\nWo bin ich?;egal", "x.csv");
        Assert.Equal(["Wie spät ist es?", "Wo bin ich?"], noHead);
    }

    [Fact(DisplayName = "Fragenreihe: mehr als 200 Fragen oder eine zu lange Frage gibt eine klare Meldung")]
    public void Parse_limits()
    {
        var many = string.Join("\n", Enumerable.Range(1, ModelBatch.MaxQuestions + 1).Select(i => $"Frage {i}"));
        Assert.Contains("Zu viele Fragen", Assert.Throws<InvalidOperationException>(() => ModelBatch.ParseQuestions(many)).Message);
        Assert.Contains("länger als", Assert.Throws<InvalidOperationException>(() => ModelBatch.ParseQuestions("ok\n" + new string('x', ModelBatch.MaxQuestionChars + 1))).Message);
    }

    [Fact(DisplayName = "Fragenreihe: jede Frage an jedes Modell, Fortschritt je Schritt, Fehler einer Quelle steht als Zeile und der Rest läuft weiter")]
    public async Task Run_all_with_failure()
    {
        var calls = new List<string>();
        var steps = new List<ModelBatch.Progress>();
        Task<(bool, string, double)> Ask(string q, string s, CancellationToken ct)
        {
            calls.Add($"{q}@{s}");
            if (s == "B" && q == "q2") throw new HttpRequestException("Verbindung weg");
            if (s == "B" && q == "q3") return Task.FromResult((false, "Modell lädt nicht", 0.5));
            return Task.FromResult((true, $"Antwort {q} von {s}", 2.0));
        }
        var (res, cancelled) = await ModelBatch.RunAsync(["q1", "q2", "q3"], ["A", "B"], Ask, (p, r) => steps.Add(p));
        Assert.False(cancelled);
        Assert.Equal(["q1@A", "q1@B", "q2@A", "q2@B", "q3@A", "q3@B"], calls);
        Assert.Equal(6, res.Count);
        Assert.Equal(12, steps.Count); // vor und nach jeder Einzelfrage
        Assert.Equal(6, steps[^1].Done); Assert.Equal(100, steps[^1].Percent);
        var e1 = res.Single(r => r.Question == 2 && r.Source == "B");
        Assert.False(e1.Ok); Assert.Equal("Verbindung weg", e1.Error); Assert.Equal(0, e1.Chars);
        Assert.Equal("Modell lädt nicht", res.Single(r => r.Question == 3 && r.Source == "B").Error);
        var a = res.Single(r => r.Question == 1 && r.Source == "A");
        Assert.True(a.Ok); Assert.Equal("Antwort q1 von A".Length, a.Chars); Assert.Equal(4, a.Words); Assert.Equal(Math.Round(a.Chars / 2.0, 1), a.CharsPerSecond);
        var sum = ModelBatch.Summarize(res);
        Assert.Equal((3, 0), (sum.Single(s => s.Source == "A").Ok, sum.Single(s => s.Source == "A").Failed));
        Assert.Equal((1, 2), (sum.Single(s => s.Source == "B").Ok, sum.Single(s => s.Source == "B").Failed));
    }

    [Fact(DisplayName = "Fragenreihe: Abbrechen mitten im Lauf behält erhaltene Antworten, Fortsetzen fragt nur, was fehlt")]
    public async Task Cancel_and_resume()
    {
        using var cts = new CancellationTokenSource();
        var n = 0;
        async Task<(bool, string, double)> Ask(string q, string s, CancellationToken ct)
        {
            if (++n == 3) { cts.Cancel(); await Task.Delay(Timeout.Infinite, ct); } // läuft gerade, als abgebrochen wird
            return (true, q + s, 1);
        }
        var (part, cancelled) = await ModelBatch.RunAsync(["q1", "q2"], ["A", "B"], Ask, ct: cts.Token);
        Assert.True(cancelled);
        Assert.Equal(["q1A", "q1B"], part.Select(r => r.Answer));
        var asked = new List<string>();
        var (all, c2) = await ModelBatch.RunAsync(["q1", "q2"], ["A", "B"], (q, s, ct) => { asked.Add(q + s); return Task.FromResult((true, q + s, 1.0)); }, done: part);
        Assert.False(c2);
        Assert.Equal(["q2A", "q2B"], asked);
        Assert.Equal(4, all.Count);
    }

    [Fact(DisplayName = "Fragenreihe: leere Liste oder keine Modelle wird abgelehnt")]
    public async Task Run_guards()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => ModelBatch.RunAsync([], ["A"], (q, s, ct) => Task.FromResult((true, "", 0.0))));
        await Assert.ThrowsAsync<InvalidOperationException>(() => ModelBatch.RunAsync(["q"], [], (q, s, ct) => Task.FromResult((true, "", 0.0))));
    }

    [Fact(DisplayName = "Fragenreihe: CSV (Semikolon, Felder gequotet) und Markdown mit Übersicht, Fehlern und Abbruch-Vermerk")]
    public void Export_csv_md()
    {
        ModelBatch.Result[] res =
        [
            new(1, "Was ist 2+2?", "Ollama|qwen2.5:3b", true, "Das ist \"vier\";\nalso 4.", 1.25, ""),
            new(1, "Was ist 2+2?", "Ollama|qwen2.5:1.5b", false, "", 0, "Quelle oder Modell nicht erreichbar."),
        ];
        var csv = ModelBatch.Csv(res);
        var lines = csv.Split('\n');
        Assert.Equal("nr;frage;modell;ok;sekunden;zeichen;woerter;zeichen_je_sekunde;antwort;fehler", lines[0]);
        Assert.StartsWith("1;Was ist 2+2?;Ollama|qwen2.5:3b;ja;1.25;23;", lines[1]);
        Assert.Contains("\"Das ist \"\"vier\"\";", csv);
        Assert.Contains(";nein;0.00;0;0;0.0;;Quelle oder Modell nicht erreichbar.", csv);
        Assert.DoesNotContain("\r", csv);
        var md = ModelBatch.Markdown(res, "Test", 1, cancelled: true);
        Assert.Contains("1 Fragen · 2 Modelle · 2 Antworten · **abgebrochen**", md);
        Assert.Contains("| Ollama\\|qwen2.5:1.5b | 1 | 0 | 1 |", md);
        Assert.Contains("⚠ Quelle oder Modell nicht erreichbar.", md);
        Assert.Contains("Das ist \"vier\"; also 4.", md);
    }

    [Fact(DisplayName = "Fragenreihe (Gegenprüfung T-0739): CSV entschärft Formeln – „- Karotte“ und „=1+1“ bekommen ein Hochkomma, reine Zahlen bleiben")]
    public void Export_csv_formula_guard()
    {
        ModelBatch.Result[] res =
        [
            new(1, "=1+1", "Ollama|a", true, "- Karotte", 1, ""),
            new(2, "@SUMME(A1)", "+Quelle|b", false, "", 0, "-5"),
            new(3, "Zahl?", "Ollama|a", true, "+3,2", 1, ""),
            new(4, "\tTab?", "Ollama|a", true, "x", 1, ""), // die Antwort wird getrimmt, die Frage nicht
        ];
        var lines = ModelBatch.Csv(res).Split('\n');
        Assert.StartsWith("1;'=1+1;Ollama|a;ja;", lines[1]);
        Assert.EndsWith(";'- Karotte;", lines[1]);
        Assert.StartsWith("2;'@SUMME(A1);'+Quelle|b;nein;", lines[2]);
        Assert.EndsWith(";;-5", lines[2]); // reine Zahl bleibt unverändert
        Assert.EndsWith(";+3,2;", lines[3]);
        Assert.StartsWith("4;'\tTab?;", lines[4]);
        // derselbe Schutz im alten Einzelvergleich (ModelCompare.Csv)
        var mc = ModelCompare.Csv([ModelCompare.NewRound("=1+1", [new("-Quelle", "x", 1, true)], 1) with { Revealed = true }]).Split('\n');
        Assert.StartsWith("'=1+1;'-Quelle;", mc[1]);
        Assert.StartsWith("-5;", ModelCompare.Csv([ModelCompare.NewRound("-5", [new("Q", "x", 1, true)], 1)]).Split('\n')[1]);
    }
}
