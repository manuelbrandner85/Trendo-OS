using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Trendo.App;
using Trendo.Core;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0739 (STUDIO-MODELS-01): Fragenreihe im Programm „KI-Modelle vergleichen“ über die Oberfläche.
/// <see cref="ModelsBatch_Ui"/>: Quellen ersetzt (Ablauf, Fehler mitten im Lauf, Abbrechen/Fortsetzen, BLOCKIERT, Export, Rückgängig).
/// <see cref="ModelsBatchLive_Ui"/>: ECHT gegen Ollama auf diesem PC – ersetzt ist nur die Dateiauswahl des Systems.</summary>
public class ModelsBatch_Ui
{
    const string K = "modelle-vergleichen-programm";

    static void Tap(MainWindow w, string name) { var b = w.Find<Button>(name); b.BringIntoView(); Settle(); RealClick(w, b); Settle(); }
    static void WaitFor(Func<bool> done, int ms = 8000) { var sw = Stopwatch.StartNew(); while (!done() && sw.ElapsedMilliseconds < ms) { Thread.Sleep(10); Settle(); } Settle(); }

    /// <summary>Ersatzquelle: antwortet sofort; <see cref="FailOn"/> wirft beim n-ten Aufruf, <see cref="HoldOn"/> hängt beim n-ten Aufruf, bis abgebrochen wird.</summary>
    sealed class Ki(string name, bool up, params string[] models) : IKiBackend
    {
        public int Calls, FailOn = -1, HoldOn = -1;
        public readonly List<string> Asked = [];
        public string Name => name;
        public string BaseUrl => "http://127.0.0.1:1";
        public Task<bool> IsUpAsync(CancellationToken ct = default) => Task.FromResult(up);
        public Task<IReadOnlyList<string>> ModelsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>(models);
        public async Task<string> ChatAsync(string model, IReadOnlyList<KiMessage> messages, CancellationToken ct = default)
        {
            var n = ++Calls; var q = messages[^1].Content; Asked.Add(q);
            if (n == FailOn) throw new HttpRequestException("Verbindung zur Quelle abgerissen");
            if (n == HoldOn) await Task.Delay(Timeout.Infinite, ct);
            return $"{name}/{model} sagt: {q.Length} Zeichen gelesen.";
        }
    }

    [AvaloniaFact(DisplayName = "T-0739 Fragenreihe: CSV importieren, alle Fragen an zwei Modelle, Fehler mitten im Lauf steht je Frage, Tabelle, CSV/MD-Export, ein Rückgängig-Schritt")]
    public void Batch_runs_table_export_undo()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        var a = new Ki("Quelle A", true, "a1"); var b = new Ki("Quelle B", true, "b1") { FailOn = 2 }; var c = new Ki("Quelle C", false, "c1");
        w.KiBackendsOverride = () => [a, b, c];
        try
        {
            var csv = Path.Combine(root, "fragen.csv");
            File.WriteAllText(csv, "Nr;Frage;Soll\n1;Was ist 2+2?;4\n2;\"Nenne eine Farbe; kurz\";rot\n3;Wie heißt die Hauptstadt von Frankreich?;Paris\n", new UTF8Encoding(true));
            w.PickOverride = _ => Task.FromResult<string[]?>([csv]);
            w.Show(K); Settle(); RealClick(w, w.Find<Button>("prog-tpl-leer")); Settle();
            var d = w.ProgramActive(K)!;
            Assert.False(w.Find<Button>("models-batch-run").IsEnabled); // leer: erst Fragen und Modelle
            Tap(w, "models-batch-import"); WaitFor(() => d.Text("series").Length > 0);
            Assert.Equal("Was ist 2+2?\nNenne eine Farbe; kurz\nWie heißt die Hauptstadt von Frankreich?", d.Text("series"));
            Assert.Contains("3 Fragen", w.ProgramMessage(K));
            var tf = w.ModelsFind(); WaitFor(() => tf.IsCompleted);
            w.Find<CheckBox>("models-src-0").IsChecked = true; Settle(); // Quelle A · a1
            w.Find<CheckBox>("models-src-1").IsChecked = true; Settle(); // Quelle B · b1
            Assert.Equal("3 Fragen × 2 Modelle = 6 Antworten", w.Find<TextBlock>("models-batch-count").Text);
            var undoBefore = d.History.Count;
            Tap(w, "models-batch-run");
            WaitFor(() => w.ProgramMessage(K).StartsWith("Fragenreihe fertig"));
            Assert.Equal("Fragenreihe fertig: 5 Antworten, 1 Fehler. Export als CSV oder Markdown oben unter „Exportieren“.", w.ProgramMessage(K));
            Assert.Equal(["Was ist 2+2?", "Was ist 2+2?", "Nenne eine Farbe; kurz", "Nenne eine Farbe; kurz", "Wie heißt die Hauptstadt von Frankreich?", "Wie heißt die Hauptstadt von Frankreich?"],
                a.Asked.Zip(b.Asked).SelectMany(x => new[] { x.First, x.Second }));
            var res = MainWindow.ModelsBatchResults(d);
            Assert.Equal(6, res.Count);
            var broken = res.Single(r => !r.Ok);
            Assert.Equal((2, "Quelle B|b1"), (broken.Question, broken.Source)); // Fehler genau bei Frage 2, Modell B – Frage 3 lief trotzdem
            Assert.True(res.Single(r => r.Question == 3 && r.Source == "Quelle B|b1").Ok);
            // Tabelle auf der Arbeitsfläche
            w.Show(K); Settle();
            var cells = w.Find<Grid>("models-batch-table").Children.OfType<TextBlock>().Select(t => t.Text!).ToList();
            Assert.Contains("Quelle A · a1", cells); Assert.Contains(cells, t => t.StartsWith("⚠ "));
            var sum = w.Find<Grid>("models-batch-summary").Children.OfType<TextBlock>().Select(t => t.Text!).ToList();
            Assert.Equal(["Modell", "Antworten", "Fehler"], sum.Take(3));
            Assert.Equal(["Quelle A · a1", "3", "0"], sum.Skip(6).Take(3));
            Assert.Equal(["Quelle B · b1", "2", "1"], sum.Skip(12).Take(3));
            // Export
            var e = MainWindow.Programs[K].Exports;
            var outCsv = e.Single(x => x.Name == "reihe-csv").Write(d, Path.Combine(root, "reihe.csv"));
            var lines = File.ReadAllLines(outCsv);
            Assert.Equal(7, lines.Length);
            Assert.Contains(lines, l => l.StartsWith("2;\"Nenne eine Farbe; kurz\";Quelle B|b1;nein;"));
            var md = File.ReadAllText(e.Single(x => x.Name == "reihe-md").Write(d, Path.Combine(root, "reihe.md")));
            Assert.Contains("3 Fragen · 2 Modelle · 6 Antworten", md);
            Assert.Contains("| Quelle B\\|b1 | 3 | 2 | 1 |", md);
            // ganzer Durchgang = ein Rückgängig-Schritt
            Assert.Equal(undoBefore + 1, d.History.Count);
            Assert.Equal("Fragenreihe", d.UndoLabel);
            Assert.True(d.Undo()); Assert.Empty(MainWindow.ModelsBatchResults(d));
            Assert.True(d.Redo()); Assert.Equal(6, MainWindow.ModelsBatchResults(d).Count);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0739 Fragenreihe: „Abbrechen“ mitten in einer Antwort behält das Erhaltene, „Fortsetzen“ fragt nur das Fehlende; ohne erreichbares Modell BLOCKIERT")]
    public void Batch_cancel_resume_blocked()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        var a = new Ki("Quelle A", true, "a1") { HoldOn = 3 }; var c = new Ki("Quelle C", false, "c1");
        w.KiBackendsOverride = () => [a, c];
        try
        {
            w.Show(K); Settle(); RealClick(w, w.Find<Button>("prog-tpl-reihe")); Settle();
            var d = w.ProgramActive(K)!;
            Assert.Equal(5, d.Text("series").Split('\n').Length);
            // nur die nicht erreichbare Quelle gewählt: BLOCKIERT, nichts gefragt
            d.Edit("Quelle", x => x["sources"] = new System.Text.Json.Nodes.JsonArray("Quelle C|c1"));
            w.Show(K); Settle();
            Tap(w, "models-batch-run"); WaitFor(() => w.ProgramMessage(K).StartsWith("BLOCKIERT"));
            Assert.StartsWith("BLOCKIERT: Keines der gewählten Modelle ist erreichbar.", w.ProgramMessage(K));
            Assert.Equal(0, a.Calls + c.Calls); Assert.Empty(d.List("batch"));
            // eine erreichbare Quelle: Frage 3 hängt, bis abgebrochen wird
            d.Edit("Quelle", x => x["sources"] = new System.Text.Json.Nodes.JsonArray("Quelle A|a1"));
            w.Show(K); Settle();
            Tap(w, "models-batch-run");
            WaitFor(() => a.Calls == 3);
            w.Show(K); Settle();
            Assert.True(w.Find<Button>("models-batch-cancel").IsEnabled);
            Assert.Equal(2, (int)w.Find<ProgressBar>("models-batch-progress").Value);
            var sw = Stopwatch.StartNew();
            Tap(w, "models-batch-cancel");
            WaitFor(() => w.ProgramMessage(K).StartsWith("Abgebrochen"), 3000);
            Assert.True(sw.ElapsedMilliseconds < 3000);
            Assert.StartsWith("Abgebrochen nach 2 von 5 Antworten", w.ProgramMessage(K));
            Assert.Equal(2, MainWindow.ModelsBatchResults(d).Count);
            Assert.True(d.Flag("batchCancelled"));
            w.Show(K); Settle();
            Assert.Equal("Fragenreihe fortsetzen", w.Find<Button>("models-batch-run").Content);
            Tap(w, "models-batch-run");
            WaitFor(() => w.ProgramMessage(K).StartsWith("Fragenreihe fertig"));
            Assert.Equal(5, MainWindow.ModelsBatchResults(d).Count);
            Assert.Equal(6, a.Calls); // 2 vorher + 1 abgebrochen + 3 fehlende – die ersten beiden wurden nicht noch einmal gefragt
            Assert.Equal([1, 2, 3, 4, 5], MainWindow.ModelsBatchResults(d).Select(r => r.Question));
            Assert.False(d.Flag("batchCancelled"));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0739 Gegenprüfung: nach Absturz (Teilergebnisse ohne Abbruch-Merker) bietet „Fortsetzen“ nur die fehlenden Paare an; „Neu starten“ fragt vorher")]
    public void Batch_resume_after_crash_and_restart_asks()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        var a = new Ki("Quelle A", true, "a1");
        w.KiBackendsOverride = () => [a];
        try
        {
            w.Show(K); Settle(); RealClick(w, w.Find<Button>("prog-tpl-leer")); Settle();
            var d = w.ProgramActive(K)!;
            // Stand wie nach einem Absturz mitten im Lauf: Notsicherung hat zwei Antworten, der Merker „abgebrochen“ wurde nie geschrieben
            d.Edit("Stand nach Absturz", x =>
            {
                x["series"] = "Frage eins?\nFrage zwei?\nFrage drei?"; x["sources"] = new JsonArray("Quelle A|a1");
                x["batchSeries"] = "Frage eins?\nFrage zwei?\nFrage drei?"; x["batchCancelled"] = false; x["batchQuestions"] = 3;
                x["batch"] = new JsonArray(
                    new JsonObject { ["n"] = 1, ["q"] = "Frage eins?", ["source"] = "Quelle A|a1", ["ok"] = true, ["text"] = "alt 1", ["sec"] = 1.0, ["error"] = "" },
                    new JsonObject { ["n"] = 2, ["q"] = "Frage zwei?", ["source"] = "Quelle A|a1", ["ok"] = true, ["text"] = "alt 2", ["sec"] = 1.0, ["error"] = "" });
            });
            w.Show(K); Settle();
            Assert.Equal("Fragenreihe fortsetzen", w.Find<Button>("models-batch-run").Content);
            Tap(w, "models-batch-run");
            WaitFor(() => w.ProgramMessage(K).StartsWith("Fragenreihe fertig"));
            Assert.Equal(["Frage drei?"], a.Asked); // nur das fehlende Paar
            var res = MainWindow.ModelsBatchResults(d);
            Assert.Equal([1, 2, 3], res.Select(r => r.Question));
            Assert.Equal(["alt 1", "alt 2"], res.Take(2).Select(r => r.Answer)); // Teilergebnisse blieben

            // Nochmal ein Absturz-Stand, dann „Neu starten“: erst die Rückfrage – „Nein“ ändert nichts, „Ja“ fragt alles neu
            d.Edit("Stand nach Absturz", x => ((JsonArray)x["batch"]!).RemoveAt(2));
            w.Show(K); Settle();
            Assert.Equal("Fragenreihe fortsetzen", w.Find<Button>("models-batch-run").Content);
            string? asked = null; var answer = false;
            w.ModelsRestartConfirm = t => { asked = t; return Task.FromResult(answer); };
            Tap(w, "models-batch-restart"); Settle();
            Assert.Contains("2 vorhandene Antworten", asked);
            Assert.Single(a.Asked); Assert.Equal(2, MainWindow.ModelsBatchResults(d).Count);
            answer = true;
            Tap(w, "models-batch-restart");
            WaitFor(() => w.ProgramMessage(K).StartsWith("Fragenreihe fertig"));
            Assert.Equal(["Frage drei?", "Frage eins?", "Frage zwei?", "Frage drei?"], a.Asked);
            Assert.DoesNotContain(MainWindow.ModelsBatchResults(d), r => r.Answer.StartsWith("alt"));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0739 Gegenprüfung: Rückgängig während eines laufenden Durchgangs ist gesperrt, mit Hinweis; danach bleibt der Stand sauber")]
    public void Batch_undo_blocked_while_running()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        var a = new Ki("Quelle A", true, "a1") { HoldOn = 2 };
        w.KiBackendsOverride = () => [a];
        try
        {
            w.Show(K); Settle(); RealClick(w, w.Find<Button>("prog-tpl-leer")); Settle();
            var d = w.ProgramActive(K)!;
            d.Edit("Fragen", x => { x["series"] = "Eins?\nZwei?\nDrei?"; x["sources"] = new JsonArray("Quelle A|a1"); });
            w.Show(K); Settle();
            Tap(w, "models-batch-run");
            WaitFor(() => a.Calls == 2);
            Assert.Single(MainWindow.ModelsBatchResults(d));
            w.Show(K); Settle();
            Tap(w, "prog-undo");
            Assert.Contains("Rückgängig ist gesperrt", w.ProgramMessage(K));
            Assert.Equal("Fragenreihe", d.UndoLabel); // nichts zurückgeholt
            Assert.Single(MainWindow.ModelsBatchResults(d));
            Tap(w, "models-batch-cancel");
            WaitFor(() => w.ProgramMessage(K).StartsWith("Abgebrochen"), 3000);
            // nach dem Lauf greift Rückgängig wieder – und nichts hängt danach am zurückgeholten Stand
            Tap(w, "prog-undo"); Settle();
            Assert.Empty(MainWindow.ModelsBatchResults(d));
            Assert.Equal("Fragen", d.UndoLabel);
        }
        finally { w.Close(); }
    }
}

/// <summary>T-0739 ECHT (nur mit <c>TRENDO_ECHT_MODELLE</c> = Ordner für die Belege): zehn Fragen aus einer TXT-Datei laufen über die Oberfläche
/// gegen zwei lokale Ollama-Modelle; danach ein zweiter Durchgang, der mitten im Lauf abgebrochen und fortgesetzt wird. Echte Quelle über den
/// gemeinsamen KI-Kern; ersetzt ist nur die Dateiauswahl des Systems (liefert die echte TXT-Datei). Ohne Ollama oder ohne zwei Modelle: BLOCKIERT.</summary>
public class ModelsBatchLive_Ui
{
    const string K = "modelle-vergleichen-programm";

    static void Click(MainWindow w, string name) { var b = w.Find<Button>(name); b.BringIntoView(); Settle(); RealClick(w, b); Settle(); }
    static async Task Until(Func<bool> ok, int ms) { var sw = Stopwatch.StartNew(); while (!ok() && sw.ElapsedMilliseconds < ms) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(50); } Avalonia.Threading.Dispatcher.UIThread.RunJobs(); Assert.True(ok()); }

    [AvaloniaFact(DisplayName = "T-0739 ECHT: zehn Fragen aus Datei gegen zwei Ollama-Modelle in einem Durchgang, Tabelle, CSV/MD; Abbrechen und Fortsetzen echt (nur mit TRENDO_ECHT_MODELLE)")]
    public async Task Real_ollama_batch()
    {
        if (Environment.GetEnvironmentVariable("TRENDO_ECHT_MODELLE") is not { Length: > 0 } dirEnv) { Assert.Skip("Übersprungen – Echtlauf nur mit TRENDO_ECHT_MODELLE=<Belegordner> und laufendem Ollama."); return; }
        var dir = Directory.CreateDirectory(dirEnv).FullName;
        var ollama = new OllamaBackend();
        if (!await ollama.IsUpAsync()) { Assert.Skip("BLOCKIERT – Ollama läuft nicht auf 127.0.0.1:11434."); return; }
        var tags = await ollama.ModelsAsync();
        var pick = new[] { "qwen2.5:1.5b", "qwen2.5:3b", "qwen2.5:0.5b", "llama3.2:3b", "qwen3:4b", "gemma3:4b" }.Where(tags.Contains).Take(2).ToList();
        if (pick.Count < 2) { Assert.Skip("BLOCKIERT – es braucht zwei kleine Textmodelle in Ollama (z. B. qwen2.5:1.5b und qwen2.5:3b); nichts wird geladen."); return; }
        var log = new StringBuilder();
        void Log(string s) => log.Append($"{DateTime.Now:HH:mm:ss} {s}\n");
        var (w, _, _) = Ui.Open();
        try
        {
            w.KiBackendsOverride = () => [ollama]; // echte Quelle; Claude und Internet-KIs bleiben bei diesem Beleg außen vor
            string[] questions =
            [
                "Was ist die Hauptstadt von Österreich?", "Wie viel sind 17 mal 23?", "Nenne drei Gemüsesorten, die im Herbst Saison haben.",
                "Formuliere höflich: „Die Heizung ist schon wieder kaputt.“", "Erkläre in einem Satz, was ein Backup ist.", "Wie viele Tage hat ein Schaltjahr?",
                "Übersetze ins Englische: „Guten Morgen, wie geht es dir?“", "Was ist schwerer: ein Kilo Federn oder ein Kilo Eisen?",
                "Schreibe einen Betreff für eine E-Mail, die einen Termin absagt.", "Welche Farbe entsteht, wenn man Blau und Gelb mischt?",
            ];
            var txt = Path.Combine(dir, "fragen.txt");
            File.WriteAllText(txt, "# T-0739 Echtlauf: zehn Fragen, eine je Zeile\n" + string.Join("\n", questions) + "\n", new UTF8Encoding(false));
            w.PickOverride = _ => Task.FromResult<string[]?>([txt]);
            w.Show(K); Settle(); Click(w, "prog-tpl-leer"); Settle();
            var d = w.ProgramActive(K)!;
            d.Set("system", "Antworte auf Deutsch in höchstens zwei Sätzen.", "Anweisung");
            w.Show(K); Settle();
            Click(w, "models-batch-import"); await Until(() => d.Text("series").Length > 0, 5000);
            Log($"Import: {w.ProgramMessage(K)}");
            Click(w, "models-go-find"); await Until(() => w.ProgramMessage(K).Contains("gefunden") || w.ProgramMessage(K).StartsWith("Keine"), 30000);
            Log($"Quellen: {w.ProgramMessage(K)}");
            foreach (var m in pick)
            {
                var box = w.Find<StackPanel>("models-sources").GetLogicalDescendants().OfType<CheckBox>().Single(cb => (cb.Content as string) == "Ollama · " + m);
                box.IsChecked = true; Settle();
            }
            Log($"Gewählt: {string.Join(", ", d.List("sources").Select(n => n!.ToString()))} (Ollama 127.0.0.1:11434)");
            Assert.Equal("10 Fragen × 2 Modelle = 20 Antworten", w.Find<TextBlock>("models-batch-count").Text);
            var sw = Stopwatch.StartNew(); var lastMsg = "";
            Click(w, "models-batch-run");
            await Until(() =>
            {
                var msg = w.ProgramMessage(K);
                if (msg != lastMsg && (msg.Contains("geht an") || msg.StartsWith("Fragenreihe"))) { lastMsg = msg; Log("Fortschritt: " + msg); }
                return msg.StartsWith("Fragenreihe fertig") || msg.StartsWith("BLOCKIERT");
            }, 900_000);
            Log($"Durchgang 1 fertig nach {sw.Elapsed.TotalSeconds:0.0} s: {w.ProgramMessage(K)}");
            var res = MainWindow.ModelsBatchResults(d);
            Assert.Equal(20, res.Count);
            foreach (var m in pick) Assert.True(res.Count(r => r.Source == "Ollama|" + m && r.Ok) >= 8, m + " hat zu oft nicht geantwortet");
            foreach (var r in res) Log($"  F{r.Question} {r.Source} ok={(r.Ok ? "ja" : "nein")} {r.Seconds:0.00} s {r.Chars} Z: {(r.Ok ? r.Answer.Replace("\n", " ") : r.Error)}");
            var e = MainWindow.Programs[K].Exports;
            e.Single(x => x.Name == "reihe-csv").Write(d, Path.Combine(dir, "durchgang1.csv"));
            e.Single(x => x.Name == "reihe-md").Write(d, Path.Combine(dir, "durchgang1.md"));
            w.Show(K); Settle();
            Assert.Equal(20 + 1, w.Find<Grid>("models-batch-table").RowDefinitions.Count);

            // Durchgang 2: drei neue Fragen, nach der ersten Antwort „Abbrechen“, dann fortsetzen
            d.Set("series", "Nenne ein Haustier.\nWie viele Beine hat eine Spinne?\nWas ist das Gegenteil von warm?", "Fragen");
            w.Show(K); Settle();
            Click(w, "models-batch-run");
            await Until(() => MainWindow.ModelsBatchResults(d).Any(r => r.Text == "Nenne ein Haustier."), 300_000); // erste Antwort des neuen Durchgangs
            w.Show(K); Settle();
            var stop = Stopwatch.StartNew();
            Click(w, "models-batch-cancel");
            await Until(() => w.ProgramMessage(K).StartsWith("Abgebrochen"), 10_000);
            Log($"Abbrechen nach {stop.ElapsedMilliseconds} ms: {w.ProgramMessage(K)}");
            var kept = MainWindow.ModelsBatchResults(d).Count;
            Assert.InRange(kept, 1, 5); Assert.True(d.Flag("batchCancelled"));
            w.Show(K); Settle();
            Assert.Equal("Fragenreihe fortsetzen", w.Find<Button>("models-batch-run").Content);
            Click(w, "models-batch-run");
            await Until(() => w.ProgramMessage(K).StartsWith("Fragenreihe fertig"), 600_000);
            var res2 = MainWindow.ModelsBatchResults(d);
            Log($"Fortgesetzt: {w.ProgramMessage(K)} – {kept} Antworten behalten, insgesamt {res2.Count}");
            Assert.Equal(6, res2.Count);
            Assert.Equal(6, res2.Select(r => (r.Question, r.Source)).Distinct().Count()); // nichts doppelt gefragt
            e.Single(x => x.Name == "reihe-csv").Write(d, Path.Combine(dir, "durchgang2-abgebrochen-fortgesetzt.csv"));
        }
        finally
        {
            w.Close();
            File.WriteAllText(Path.Combine(dir, "t0739-echtlauf.log"), log.ToString(), new UTF8Encoding(false));
        }
    }
}
