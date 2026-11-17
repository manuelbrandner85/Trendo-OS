using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Kreativ;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>T-0739 (STUDIO-MODELS-01): Fragenreihe – mehrere Fragen (Liste oder CSV/TXT) laufen in einem Durchgang gegen alle gewählten Modelle.
/// Gefragt wird über den gemeinsamen KI-Kern (<see cref="IntelligenceCore.AskAsync"/>), auf je eine Quelle beschränkt – wie bei der Einzelfrage.
/// Fortschritt je Modell und Frage, „Abbrechen“ (erhaltene Antworten bleiben, „Fortsetzen“ macht weiter), Tabelle, Export als CSV und Markdown.</summary>
public partial class MainWindow
{
    CancellationTokenSource? _modelsCts;
    ModelBatch.Progress? _modelsBatchProgress;

    static JsonObject BatchJson(ModelBatch.Result r) => new()
    {
        ["n"] = r.Question, ["q"] = r.Text, ["source"] = r.Source, ["ok"] = r.Ok, ["text"] = r.Answer, ["sec"] = r.Seconds, ["error"] = r.Error,
    };

    /// <summary>Ergebnisse der Fragenreihe aus dem Dokument (beschädigte Einträge fallen weg).</summary>
    internal static List<ModelBatch.Result> ModelsBatchResults(ProgramDocument d) =>
        d.List("batch").OfType<JsonObject>().Select(o => new ModelBatch.Result((int)ProgramDocument.ToNumber(o["n"], 0), Js(o["q"]), Js(o["source"]),
            o["ok"]?.GetValueKind() == System.Text.Json.JsonValueKind.True, Js(o["text"]), Math.Max(0, ProgramDocument.ToNumber(o["sec"], 0)), Js(o["error"])))
        .Where(r => r.Question > 0 && r.Source.Length > 0).ToList();

    /// <summary>Fragen aus einer CSV- oder TXT-Datei in die Fragenreihe übernehmen (vorhandene Liste wird ersetzt, ein Rückgängig-Schritt).</summary>
    public async Task ModelsBatchImport(ProgramDocument d)
    {
        if (await PickMediaFile("models-fragen", "Fragen (CSV oder TXT)", ["*.csv", "*.txt"]) is not { } f) return;
        ProgramRun(ModelsKey, () =>
        {
            if (!KreativSafe.LocalFile(f, ModelBatch.MaxFileBytes)) throw new ArgumentException("Bitte eine CSV- oder TXT-Datei auf diesem Rechner (höchstens 1 MB).");
            var qs = ModelBatch.ParseQuestions(File.ReadAllText(f), f);
            if (qs.Count == 0) throw new InvalidDataException("In der Datei stehen keine Fragen. Erwartet: eine Frage je Zeile, bei CSV die Spalte „Frage“ oder die erste Spalte.");
            d.Set("series", string.Join("\n", qs.Select(q => q.Replace("\r", " ").Replace("\n", " "))), "Fragen importiert");
            _progMsg[ModelsKey] = $"{qs.Count} Fragen aus „{Path.GetFileName(f)}“ übernommen. Modelle anhaken, dann „Fragenreihe starten“.";
        });
    }

    /// <summary>„Abbrechen“: hält den laufenden Durchgang sofort an; schon erhaltene Antworten bleiben im Dokument.</summary>
    public void ModelsBatchCancel() => _modelsCts?.Cancel();

    /// <summary>Fragenreihe starten oder – nach einem Abbruch mit unveränderter Liste – fortsetzen. Jede Frage geht nacheinander an jedes gewählte Modell.
    /// Ist keines der gewählten Modelle erreichbar, meldet das Programm BLOCKIERT und fragt nichts.</summary>
    public async Task<bool> ModelsBatchRun(ProgramDocument d)
    {
        if (_modelsBusy) { _progMsg[ModelsKey] = "Es läuft schon eine Abfrage – einen Moment."; Show(ModelsKey); return false; }
        IReadOnlyList<string> questions;
        try { questions = ModelBatch.ParseQuestions(d.Text("series")); }
        catch (InvalidOperationException ex) { _progMsg[ModelsKey] = ex.Message; Show(ModelsKey); return false; }
        if (questions.Count == 0) { _progMsg[ModelsKey] = "Trag zuerst Fragen ein (eine je Zeile) oder importiere eine CSV-/TXT-Datei."; Show(ModelsKey); return false; }
        var picked = d.List("sources").Select(Js).Where(s => s.Contains('|')).Distinct().ToList();
        if (picked.Count == 0) { _progMsg[ModelsKey] = "Wähle zuerst die Modelle aus (oben „Quellen suchen“ und anhaken)."; Show(ModelsKey); return false; }
        var backends = ModelsBackends();
        var system = d.Text("system").Trim();
        _modelsBusy = true; _modelsCts = new CancellationTokenSource();
        _modelsBatchProgress = new(0, questions.Count * picked.Count, 1, questions.Count, picked[0]);
        _progMsg[ModelsKey] = "Modelle werden geprüft …"; Show(ModelsKey);
        try
        {
            // Erreichbarkeit einmal vorab: ein fehlendes Modell steht dann je Frage als Fehler in der Tabelle, statt jedes Mal neu zu warten
            var reach = new Dictionary<string, (IKiBackend? B, string? Why)>();
            foreach (var key in picked)
            {
                var (name, model) = (key[..key.IndexOf('|')], key[(key.IndexOf('|') + 1)..]);
                var b = backends.FirstOrDefault(x => x.Name == name);
                if (b is null) { reach[key] = (null, "Diese Quelle ist nicht mehr eingerichtet."); continue; }
                try { reach[key] = await b.IsUpAsync(_modelsCts.Token) && (await b.ModelsAsync(_modelsCts.Token)).Contains(model) ? (b, null) : (null, "Quelle oder Modell nicht erreichbar."); }
                catch (Exception ex) when (ex is not OperationCanceledException) { reach[key] = (null, KreativSafe.Cut(Trendo.Core.FriendlyError.Say(ex), 200)); }
            }
            if (reach.Values.All(v => v.B is null))
            {
                _progMsg[ModelsKey] = "BLOCKIERT: Keines der gewählten Modelle ist erreichbar. Voraussetzung: eine KI auf diesem PC (z. B. Ollama mit geladenem Modell) oder Claude über deinen eigenen Zugang – unter „KI-Quellen“ einrichten.";
                return false;
            }
            var seriesText = d.Text("series");
            var resume = d.Flag("batchCancelled") && d.Text("batchSeries") == seriesText;
            var before = resume ? ModelsBatchResults(d).Where(r => r.Question <= questions.Count && r.Text == questions[r.Question - 1] && picked.Contains(r.Source)).ToList() : [];
            // EIN Rückgängig-Schritt für den ganzen Durchgang: „Rückgängig“ bringt den Stand vor dem Start zurück. Die Antworten kommen ohne eigenen
            // Schritt dazu (Track), damit ein langer Durchgang die früheren Rückgängig-Schritte nicht aus dem Stapel drängt.
            d.Edit(resume ? "Fragenreihe fortgesetzt" : "Fragenreihe", x =>
            {
                x["batch"] = new JsonArray(before.Select(r => (JsonNode)BatchJson(r)).ToArray());
                x["batchSeries"] = seriesText; x["batchCancelled"] = false; x["batchQuestions"] = questions.Count;
            });
            async Task<(bool, string, double)> Ask(string q, string key, CancellationToken ct)
            {
                var (b, why) = reach[key];
                if (b is null) return (false, why ?? "nicht erreichbar", 0);
                var model = key[(key.IndexOf('|') + 1)..];
                var core = new IntelligenceCore(_core.DataRoot, _core.Audit, () => [b]); // derselbe Kern wie überall, auf diese eine Quelle beschränkt
                var a = await core.AskAsync(new KiRequest(q, system.Length > 0 ? system : IntelligenceCore.DefaultSystem, model, UseKnowledge: false, AllowRemote: true), ct);
                if (a.Ok && a.Model is { } used && used != model) return (false, $"Es hat ein anderes Modell geantwortet ({used}).", a.Took.TotalSeconds);
                return (a.Ok, a.Text, a.Took.TotalSeconds);
            }
            var (results, cancelled) = await ModelBatch.RunAsync(questions, picked, Ask, (p, r) =>
            {
                _modelsBatchProgress = p;
                if (r is not null) d.Track(x => { if (x["batch"] is not JsonArray a) x["batch"] = a = []; a.Add(BatchJson(r)); }); // sofort im Dokument: Notsicherung verliert höchstens die laufende Frage
                _progMsg[ModelsKey] = r is null ? $"Frage {p.Question} von {p.Questions} geht an {p.Source.Replace("|", " · ")} ({p.Done + 1} von {p.Total}) …"
                    : $"{p.Done} von {p.Total} erledigt.";
                if (CurrentView == ModelsKey) Show(ModelsKey);
            }, before, _modelsCts.Token);
            d.Track(x => x["batchCancelled"] = cancelled);
            var ok = results.Count(r => r.Ok); var err = results.Count - ok;
            _progMsg[ModelsKey] = cancelled
                ? $"Abgebrochen nach {results.Count} von {questions.Count * picked.Count} Antworten – sie bleiben erhalten. „Fragenreihe starten“ setzt fort."
                : $"Fragenreihe fertig: {ok} Antworten{(err > 0 ? $", {err} Fehler" : "")}. Export als CSV oder Markdown oben unter „Exportieren“.";
            _core.Audit.Append(CurrentPrincipal.Name, "models.batch", $"{questions.Count} Fragen, {picked.Count} Modelle, {ok} ok, {err} Fehler{(cancelled ? ", abgebrochen" : "")}");
            return !cancelled && ok > 0;
        }
        catch (OperationCanceledException) { _progMsg[ModelsKey] = "Abgebrochen, bevor die erste Frage gestellt wurde."; return false; }
        finally { _modelsBusy = false; _modelsCts?.Dispose(); _modelsCts = null; _modelsBatchProgress = null; Show(ModelsKey); }
    }

    /// <summary>Karte „Fragenreihe“: Fragenliste, Datei-Import, Start/Fortsetzen, Abbrechen, Fortschritt, Ergebnistabelle je Frage und Modell.</summary>
    Control ModelsBatchCard(ProgramDocument d)
    {
        var sp = new StackPanel { Name = "models-batch", Spacing = 8 };
        var questions = 0; string? parseError = null;
        try { questions = ModelBatch.ParseQuestions(d.Text("series")).Count; } catch (InvalidOperationException ex) { parseError = ex.Message; }
        var head = new DockPanel();
        var import = new Button { Name = "models-batch-import", Content = "Fragen aus Datei …", Padding = new Thickness(12, 5), IsEnabled = !_modelsBusy, [DockPanel.DockProperty] = Avalonia.Controls.Dock.Right };
        ToolTip.SetTip(import, "CSV (Spalte „Frage“ oder erste Spalte) oder TXT (eine Frage je Zeile).");
        import.Click += (_, _) => _ = ModelsBatchImport(d);
        head.Children.Add(import);
        head.Children.Add(H("Fragenreihe – alle Fragen an alle gewählten Modelle", 14));
        sp.Children.Add(head);
        var box = KrBox(d, "series", "models-batch-series", "Eine Frage je Zeile. Zeilen mit # am Anfang sind Notizen.", true); box.MinHeight = 90;
        sp.Children.Add(box);
        var resume = d.Flag("batchCancelled") && d.Text("batchSeries") == d.Text("series") && d.List("batch").Count > 0;
        var picked = d.List("sources").Count;
        var run = new Button { Name = "models-batch-run", Content = _modelsBusy && _modelsCts is not null ? "Läuft …" : resume ? "Fragenreihe fortsetzen" : "Fragenreihe starten", Padding = new Thickness(14, 6), IsEnabled = !_modelsBusy && questions > 0 && picked > 0 };
        run.Click += (_, _) => _ = ModelsBatchRun(d);
        var cancel = new Button { Name = "models-batch-cancel", Content = "Abbrechen", Padding = new Thickness(12, 6), IsEnabled = _modelsCts is not null };
        cancel.Click += (_, _) => ModelsBatchCancel();
        sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { run, cancel,
            new TextBlock { Name = "models-batch-count", Text = parseError ?? $"{questions} Fragen × {picked} Modelle = {questions * picked} Antworten", Foreground = parseError is null ? Muted : WarnBrush, FontSize = 12, VerticalAlignment = VerticalAlignment.Center } } });
        if (_modelsBatchProgress is { } p && _modelsCts is not null)
        {
            sp.Children.Add(new ProgressBar { Name = "models-batch-progress", Minimum = 0, Maximum = Math.Max(1, p.Total), Value = p.Done, Height = 6 });
            sp.Children.Add(P($"Frage {p.Question} von {p.Questions} · {p.Source.Replace("|", " · ")} · {p.Done} von {p.Total} erledigt", Muted, 11));
        }
        var results = ModelsBatchResults(d);
        if (results.Count > 0)
        {
            // Übersicht je Modell
            var sum = ModelBatch.Summarize(results);
            var g = new Grid { Name = "models-batch-summary", ColumnDefinitions = new ColumnDefinitions("2*,*,*,*,*,*") };
            string[] sh = ["Modell", "Antworten", "Fehler", "Dauer (Schnitt)", "Zeichen (Schnitt)", "Zeichen/s"];
            for (var i = 0; i <= sum.Count; i++) g.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            for (var c = 0; c < sh.Length; c++) { var tb = new TextBlock { Text = sh[c], Foreground = Muted, FontSize = 12 }; Grid.SetColumn(tb, c); g.Children.Add(tb); }
            for (var i = 0; i < sum.Count; i++)
            {
                var s = sum[i];
                string[] cells = [s.Source.Replace("|", " · "), s.Ok.ToString(De), s.Failed.ToString(De), s.AvgSeconds.ToString("0.0", De) + " s", s.AvgChars.ToString("0", De), s.CharsPerSecond.ToString("0.0", De)];
                for (var c = 0; c < cells.Length; c++) { var tb = new TextBlock { Text = cells[c], Foreground = c == 2 && s.Failed > 0 ? WarnBrush : Text, FontSize = 13, Margin = new Thickness(0, 3, 6, 3), TextTrimming = TextTrimming.CharacterEllipsis }; Grid.SetColumn(tb, c); Grid.SetRow(tb, i + 1); g.Children.Add(tb); }
            }
            sp.Children.Add(g);
            // Tabelle je Frage und Modell
            var t = new Grid { Name = "models-batch-table", ColumnDefinitions = new ColumnDefinitions("40,2*,2*,4*,70,70") };
            string[] th = ["Nr.", "Frage", "Modell", "Antwort / Fehler", "Zeit", "Zeichen"];
            var shown = results.OrderBy(r => r.Question).Take(400).ToList();
            for (var i = 0; i <= shown.Count; i++) t.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            for (var c = 0; c < th.Length; c++) { var tb = new TextBlock { Text = th[c], Foreground = Muted, FontSize = 12 }; Grid.SetColumn(tb, c); t.Children.Add(tb); }
            for (var i = 0; i < shown.Count; i++)
            {
                var r = shown[i];
                string[] cells = [r.Question.ToString(De), KreativSafe.Cut(r.Text, 120), r.Source.Replace("|", " · "), r.Ok ? KreativSafe.Cut(r.Answer.Trim().Replace('\n', ' '), 300) : "⚠ " + KreativSafe.Cut(r.Error, 200), r.Seconds.ToString("0.0", De) + " s", r.Chars.ToString(De)];
                for (var c = 0; c < cells.Length; c++)
                {
                    var tb = new TextBlock { Name = c == 3 ? $"models-batch-cell-{i}" : null, Text = cells[c], Foreground = c == 3 && !r.Ok ? WarnBrush : Text, FontSize = 12, Margin = new Thickness(0, 3, 8, 3), TextWrapping = c is 1 or 3 ? TextWrapping.Wrap : TextWrapping.NoWrap };
                    Grid.SetColumn(tb, c); Grid.SetRow(tb, i + 1); t.Children.Add(tb);
                }
            }
            sp.Children.Add(t);
            if (results.Count > shown.Count) sp.Children.Add(P($"Hier stehen die ersten {shown.Count} von {results.Count} Antworten – vollständig im Export.", Muted, 11));
            if (d.Flag("batchCancelled")) sp.Children.Add(P("⚠ Abgebrochen – die erhaltenen Antworten bleiben. „Fragenreihe fortsetzen“ fragt nur, was noch fehlt.", WarnBrush, 11));
        }
        return KrCard(sp, "models-batch-card");
    }
}
