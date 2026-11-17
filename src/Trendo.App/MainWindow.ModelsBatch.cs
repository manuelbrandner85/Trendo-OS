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
    /// <summary>Rückfrage vor „Neu starten“ (Test-Ersatz; sonst ein eigenes Fenster mit „Abbrechen“ vorbelegt).</summary>
    public Func<string, Task<bool>>? ModelsRestartConfirm { get; set; }

    /// <summary>Gegenprüfung T-0739: Während ein Durchgang läuft, hängt er jede Antwort an das Dokument an. Ein Rückgängig mitten im Lauf würde den
    /// zurückgeholten Stand still weiterbeschreiben – darum ist Rückgängig/Wiederholen in diesem Programm bis zum Ende oder „Abbrechen“ gesperrt.</summary>
    void ModelsUndoGuard(string key)
    {
        if (key == ModelsKey && _modelsCts is not null)
            throw new InvalidOperationException("Rückgängig ist gesperrt, solange die Fragenreihe läuft – erst „Abbrechen“ drücken, dann geht es wieder.");
    }

    /// <summary>Gegenprüfung T-0739: Was von einem früheren Durchgang noch passt (gleiche Fragenliste, gewähltes Modell, gleiche Frage) und wie viele
    /// Paare noch fehlen. Gilt auch ohne den Merker „abgebrochen“ – nach einem Absturz steht er nie im Dokument, die Antworten aber schon.</summary>
    static (List<ModelBatch.Result> Kept, int Missing) ModelsBatchKept(ProgramDocument d, IReadOnlyList<string> questions, IReadOnlyList<string> picked)
    {
        var total = questions.Count * picked.Count;
        if (d.Text("batchSeries") != d.Text("series")) return ([], total);
        var kept = ModelsBatchResults(d).Where(r => r.Question <= questions.Count && r.Text == questions[r.Question - 1] && picked.Contains(r.Source))
            .GroupBy(r => (r.Question, r.Source)).Select(g => g.First()).ToList();
        return (kept, total - kept.Count);
    }

    static List<string> ModelsPicked(ProgramDocument d) => d.List("sources").Select(Js).Where(s => s.Contains('|')).Distinct().ToList();

    /// <summary>„Neu starten“: verwirft vorhandene Antworten erst nach Rückfrage und fragt dann alle Paare neu (ein Rückgängig-Schritt holt sie zurück).</summary>
    public async Task<bool> ModelsBatchRestart(ProgramDocument d)
    {
        if (_modelsBusy) { _progMsg[ModelsKey] = "Es läuft schon eine Abfrage – einen Moment."; Show(ModelsKey); return false; }
        var n = ModelsBatchResults(d).Count;
        if (n > 0)
        {
            var text = $"Die Fragenreihe hat schon {n} vorhandene Antworten. „Neu starten“ verwirft sie und fragt alle Fragen noch einmal an alle gewählten Modelle. "
                + "„Rückgängig“ holt sie danach zurück.";
            bool yes;
            try { yes = await (ModelsRestartConfirm ?? ModelsRestartDialog)(text); }
            catch (Exception ex) when (ex is InvalidOperationException or OperationCanceledException) { yes = false; }
            if (!yes) { _progMsg[ModelsKey] = "Nicht neu gestartet – die vorhandenen Antworten bleiben. „Fragenreihe fortsetzen“ fragt nur, was fehlt."; Show(ModelsKey); return false; }
        }
        return await ModelsBatchRun(d, restart: true);
    }

    async Task<bool> ModelsRestartDialog(string text)
    {
        const string title = "Fragenreihe neu starten?";
        var dlg = new Window { Title = title, Width = 480, SizeToContent = SizeToContent.Height, CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var yes = new Button { Name = "models-restart-yes", Content = "Neu starten" };
        var no = new Button { Name = "models-restart-no", Content = "Abbrechen", IsDefault = true, IsCancel = true };
        yes.Click += (_, _) => dlg.Close(true);
        no.Click += (_, _) => dlg.Close(false);
        dlg.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Escape) { e.Handled = true; dlg.Close(false); } };
        dlg.Content = new StackPanel { Margin = new Thickness(16), Spacing = 10, Children = {
            new TextBlock { Text = title, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap },
            new TextBlock { Name = "models-restart-text", Text = text, TextWrapping = TextWrapping.Wrap },
            new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { no, yes } } } };
        dlg.Opened += (_, _) => no.Focus();
        return await dlg.ShowDialog<bool>(this);
    }

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
    public async Task<bool> ModelsBatchRun(ProgramDocument d, bool restart = false)
    {
        if (_modelsBusy) { _progMsg[ModelsKey] = "Es läuft schon eine Abfrage – einen Moment."; Show(ModelsKey); return false; }
        IReadOnlyList<string> questions;
        try { questions = ModelBatch.ParseQuestions(d.Text("series")); }
        catch (InvalidOperationException ex) { _progMsg[ModelsKey] = ex.Message; Show(ModelsKey); return false; }
        if (questions.Count == 0) { _progMsg[ModelsKey] = "Trag zuerst Fragen ein (eine je Zeile) oder importiere eine CSV-/TXT-Datei."; Show(ModelsKey); return false; }
        var picked = ModelsPicked(d);
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
            // Gegenprüfung T-0739: Fortsetzen hängt nicht am Merker „abgebrochen“ – nach einem Absturz fehlt er, die Teilergebnisse sind aber da.
            // Fortgesetzt wird, sobald zur unveränderten Liste Antworten vorliegen und noch Paare fehlen; „Neu starten“ (nach Rückfrage) verwirft sie.
            var (kept, missing) = ModelsBatchKept(d, questions, picked);
            var resume = !restart && kept.Count > 0 && missing > 0;
            List<ModelBatch.Result> before = resume ? kept : [];
            // EIN Rückgängig-Schritt für den ganzen Durchgang: „Rückgängig“ bringt den Stand vor dem Start zurück. Die Antworten kommen ohne eigenen
            // Schritt dazu (Track), damit ein langer Durchgang die früheren Rückgängig-Schritte nicht aus dem Stapel drängt.
            var runId = Guid.NewGuid().ToString("N");
            d.Edit(resume ? "Fragenreihe fortgesetzt" : "Fragenreihe", x =>
            {
                x["batch"] = new JsonArray(before.Select(r => (JsonNode)BatchJson(r)).ToArray());
                x["batchSeries"] = seriesText; x["batchCancelled"] = false; x["batchQuestions"] = questions.Count; x["batchRun"] = runId;
            });
            // Zweite Sicherung neben der Sperre: Hat ein anderer Weg (Version zurückholen, Projektdaten ersetzen) den Stand dieses Laufs verdrängt,
            // hält der Lauf an und schreibt nichts mehr in den fremden Stand
            var cts = _modelsCts!;
            bool Mine() { if (d.Text("batchRun") == runId) return true; cts.Cancel(); return false; }
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
                if (r is not null && Mine()) d.Track(x => { if (x["batch"] is not JsonArray a) x["batch"] = a = []; a.Add(BatchJson(r)); }); // sofort im Dokument: Notsicherung verliert höchstens die laufende Frage
                _progMsg[ModelsKey] = r is null ? $"Frage {p.Question} von {p.Questions} geht an {p.Source.Replace("|", " · ")} ({p.Done + 1} von {p.Total}) …"
                    : $"{p.Done} von {p.Total} erledigt.";
                if (CurrentView == ModelsKey) Show(ModelsKey);
            }, before, _modelsCts.Token);
            if (!Mine())
            {
                _progMsg[ModelsKey] = "Angehalten: Der Stand der Fragenreihe wurde während des Laufs ersetzt – die weiteren Antworten wurden nicht eingetragen.";
                return false;
            }
            d.Track(x => x["batchCancelled"] = cancelled);
            var ok = results.Count(r => r.Ok); var err = results.Count - ok;
            _progMsg[ModelsKey] = cancelled
                ? $"Abgebrochen nach {results.Count} von {questions.Count * picked.Count} Antworten – sie bleiben erhalten. „Fragenreihe fortsetzen“ fragt nur, was noch fehlt."
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
        IReadOnlyList<string> questionList = [];
        try { questionList = ModelBatch.ParseQuestions(d.Text("series")); questions = questionList.Count; } catch (InvalidOperationException ex) { parseError = ex.Message; }
        var head = new DockPanel();
        var import = new Button { Name = "models-batch-import", Content = "Fragen aus Datei …", Padding = new Thickness(12, 5), IsEnabled = !_modelsBusy, [DockPanel.DockProperty] = Avalonia.Controls.Dock.Right };
        ToolTip.SetTip(import, "CSV (Spalte „Frage“ oder erste Spalte) oder TXT (eine Frage je Zeile).");
        import.Click += (_, _) => _ = ModelsBatchImport(d);
        head.Children.Add(import);
        head.Children.Add(H("Fragenreihe – alle Fragen an alle gewählten Modelle", 14));
        sp.Children.Add(head);
        var box = KrBox(d, "series", "models-batch-series", "Eine Frage je Zeile. Zeilen mit # am Anfang sind Notizen.", true); box.MinHeight = 90;
        sp.Children.Add(box);
        var picked = d.List("sources").Count;
        // Gegenprüfung T-0739: Fortsetzen anbieten, sobald zur unveränderten Liste Teilergebnisse vorliegen – auch ohne Merker (nach einem Absturz)
        var (kept, missing) = questionList.Count > 0 ? ModelsBatchKept(d, questionList, ModelsPicked(d)) : (new List<ModelBatch.Result>(), 0);
        var resume = kept.Count > 0 && missing > 0;
        var run = new Button { Name = "models-batch-run", Content = _modelsBusy && _modelsCts is not null ? "Läuft …" : resume ? "Fragenreihe fortsetzen" : "Fragenreihe starten", Padding = new Thickness(14, 6), IsEnabled = !_modelsBusy && questions > 0 && picked > 0 };
        run.Click += (_, _) => _ = ModelsBatchRun(d);
        var restart = new Button { Name = "models-batch-restart", Content = "Neu starten", Padding = new Thickness(12, 6), IsVisible = resume, IsEnabled = !_modelsBusy };
        ToolTip.SetTip(restart, "Verwirft die vorhandenen Antworten (nach Rückfrage) und fragt alles neu.");
        restart.Click += (_, _) => _ = ModelsBatchRestart(d);
        var cancel = new Button { Name = "models-batch-cancel", Content = "Abbrechen", Padding = new Thickness(12, 6), IsEnabled = _modelsCts is not null };
        cancel.Click += (_, _) => ModelsBatchCancel();
        sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { run, restart, cancel,
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
            if (resume && _modelsCts is null)
                sp.Children.Add(P($"⚠ {(d.Flag("batchCancelled") ? "Abgebrochen" : "Unterbrochen")} – {kept.Count} Antworten liegen vor, {missing} fehlen noch. „Fragenreihe fortsetzen“ fragt nur, was noch fehlt; „Neu starten“ fragt alles neu.", WarnBrush, 11));
        }
        return KrCard(sp, "models-batch-card");
    }
}
