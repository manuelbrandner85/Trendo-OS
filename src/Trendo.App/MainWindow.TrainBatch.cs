using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Trendo.Core;
using Trendo.Core.Kreativ;
using Trendo.Core.Programs;
using Path = System.IO.Path;

namespace Trendo.App;

/// <summary>T-0740 (STUDIO-TRAIN-01, Restliste Rang 13): Das trainierte Modell wird benutzt – im Stapel auf eine CSV-Datei (je Zeile) oder einen Ordner mit Textdateien (je Datei).
/// Läuft im Hintergrund mit Fortschritt und „Abbrechen“; das Ergebnis liegt im Dokument (rückgängig machbar, wird mitgesichert) und geht über „Vorhersagen (.csv)“ als Datei hinaus.
/// Nutzt den vorhandenen Klassifikator (<see cref="TextClassifier"/>) über <see cref="ClassifierBatch"/> – kein zweites Lernsystem.</summary>
public partial class MainWindow
{
    bool _trBusy; CancellationTokenSource? _trCancel; (int Done, int Total) _trProg;
    ProgressBar? _trBar; TextBlock? _trBarText;

    /// <summary>Läuft gerade ein Stapel?</summary>
    public bool TrainBatchBusy => _trBusy;
    /// <summary>Laufenden Stapel abbrechen (wie der Knopf „Abbrechen“).</summary>
    public void TrainBatchCancel() => _trCancel?.Cancel();
    /// <summary>Tests: nach so vielen Zeilen/Dateien kurz warten, damit „Abbrechen“ sicher mitten im Lauf greift (0 = nicht warten).</summary>
    public static int TrainBatchTestDelayMs { get; set; }

    const long TrainBatchMaxCsvBytes = 20L * 1024 * 1024;

    /// <summary>Modell auf eine CSV-Datei (<paramref name="folder"/> = false) oder alle Textdateien eines Ordners anwenden.</summary>
    public async Task<ClassifierBatch.Result?> TrainApply(ProgramDocument d, string path, bool folder)
    {
        if (_trBusy) return null;
        if (TrainModel(d) is not { } m) { _progMsg[TrainKey] = "Erst trainieren oder ein Modell laden – dann kann die KI Dateien einordnen."; Show(TrainKey); return null; }
        if (folder ? !KreativSafe.LocalDir(path) : !KreativSafe.LocalFile(path, TrainBatchMaxCsvBytes))
        { _progMsg[TrainKey] = folder ? "Bitte einen Ordner auf diesem Rechner wählen." : "Bitte eine CSV-Datei auf diesem Rechner wählen (höchstens 20 MB)."; Show(TrainKey); return null; }
        _trBusy = true; _trCancel = new CancellationTokenSource(); _trProg = (0, 0);
        _progMsg[TrainKey] = $"Ordne „{Path.GetFileName(path.TrimEnd('\\', '/'))}“ ein …"; Show(TrainKey);
        var ct = _trCancel.Token; var delay = TrainBatchTestDelayMs;
        var prog = new Progress<(int Done, int Total)>(p => { _trProg = p; TrainProgressRefresh(); });
        IProgress<(int Done, int Total)> report = delay > 0 ? new SyncWait(prog, delay) : prog;
        try
        {
            var r = await Task.Run(() => folder ? ClassifierBatch.ApplyFolder(m, path, report, ct) : ClassifierBatch.ApplyCsv(m, File.ReadAllText(path), path, report, ct), ct);
            var csv = ClassifierBatch.ToCsv(r);
            var sum = ClassifierBatch.Summary(r);
            d.Edit("Vorhersagen erstellt", x => { x["batch"] = csv; x["batchSource"] = Path.GetFileName(path.TrimEnd('\\', '/')); x["batchKind"] = r.Kind; x["batchTextCol"] = r.TextColumn.ToString(CultureInfo.InvariantCulture); x["batchRows"] = r.Rows.Count.ToString(CultureInfo.InvariantCulture); });
            _core.Audit.Append(Principal.LocalOwner.Name, "ki-training.anwenden", $"{r.Rows.Count} {(folder ? "Dateien" : "Zeilen")}");
            var what = folder ? (r.Rows.Count == 1 ? "Datei" : "Dateien") : (r.Rows.Count == 1 ? "Zeile" : "Zeilen");
            _progMsg[TrainKey] = $"✓ {r.Rows.Count} {what} eingeordnet: " + string.Join(", ", sum.Take(5).Select(s => $"{s.Label} {s.Count}"))
                + (r.Skipped > 0 ? $" ({r.Skipped} übersprungen)" : "") + ". „Vorhersagen speichern …“ legt die Tabelle als CSV-Datei ab.";
            return r;
        }
        catch (OperationCanceledException) { _progMsg[TrainKey] = $"Abgebrochen nach {_trProg.Done} von {_trProg.Total} – es wurde nichts übernommen."; return null; }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
        { _progMsg[TrainKey] = "Einordnen ging nicht: " + ex.Message; return null; }
        finally { _trBusy = false; _trCancel.Dispose(); _trCancel = null; _trBar = null; _trBarText = null; Show(TrainKey); }
    }

    /// <summary>Fortschritt melden und (nur in Tests) kurz warten – so greift „Abbrechen“ sicher mitten im Lauf.</summary>
    sealed class SyncWait(IProgress<(int, int)> inner, int ms) : IProgress<(int, int)>
    {
        public void Report((int, int) v) { inner.Report(v); Thread.Sleep(ms); }
    }

    void TrainProgressRefresh()
    {
        if (_trBar is { } b) { b.Maximum = Math.Max(1, _trProg.Total); b.Value = _trProg.Done; }
        if (_trBarText is { } t) t.Text = _trProg.Total == 0 ? "Liest ein …" : $"{_trProg.Done.ToString("N0", De)} von {_trProg.Total.ToString("N0", De)}";
    }

    async Task TrainPickCsv(ProgramDocument d)
    {
        if (await PickMediaFile("train-apply-csv", "Texte zum Einordnen (CSV)", ["*.csv", "*.txt"]) is { } f) await TrainApply(d, f, false);
    }

    async Task TrainPickFolder(ProgramDocument d)
    {
        if (await PickFolderPath("Ordner mit Textdateien einordnen") is { } f) await TrainApply(d, f, true);
    }

    /// <summary>Vorhersagen als Datei speichern – derselbe Weg wie Datei ▸ Exportieren ▸ „Vorhersagen (.csv)“.</summary>
    async Task TrainSaveBatch(ProgramDocument d)
    {
        if (d.Text("batch").Length == 0) { _progMsg[TrainKey] = "Noch keine Vorhersagen – zuerst eine CSV-Datei oder einen Ordner einordnen."; Show(TrainKey); return; }
        var name = Path.GetFileNameWithoutExtension(d.Text("batchSource")); if (name.Length == 0) name = "vorhersagen";
        string? f;
        if (PickOverride is { } fake) f = (await fake(new PickRequest("train-batch-save", false, true, [".csv"]))) is [var x, ..] ? x : null;
        else f = (await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title = "Vorhersagen speichern", SuggestedFileName = name + "-vorhersagen.csv", DefaultExtension = "csv",
            FileTypeChoices = [new FilePickerFileType("CSV-Tabelle") { Patterns = ["*.csv"] }] }))?.TryGetLocalPath();
        if (f is null) return;
        ProgramRun(TrainKey, () => { TrainWriteBatch(d, f); _progMsg[TrainKey] = $"✓ Gespeichert: {Path.GetFileName(f)}"; });
    }

    /// <summary>Vorhersagen als CSV (UTF-8 mit BOM, Semikolon) – atomar, damit bei einem Absturz keine halbe Datei liegen bleibt.</summary>
    static string TrainWriteBatch(ProgramDocument d, string f)
    {
        var csv = d.Text("batch");
        if (csv.Length == 0) throw new InvalidOperationException("Noch keine Vorhersagen – zuerst eine CSV-Datei oder einen Ordner einordnen.");
        AtomicFile.WriteAllBytes(f, [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(csv)]);
        return f;
    }

    /// <summary>Karte „Auf Dateien anwenden“: Knöpfe, Fortschritt mit Abbrechen, Vorschau der Vorhersagen.</summary>
    Control TrainBatchCard(ProgramDocument d)
    {
        var sp = new StackPanel { Name = "train-batch", Spacing = 6 };
        sp.Children.Add(H("Auf Dateien anwenden", 14));
        sp.Children.Add(P("Die KI ordnet jede Zeile einer CSV-Datei oder jede Textdatei eines Ordners ein. Du kannst Dateien auch hierher ziehen.", Muted, 11));
        var csvBtn = new Button { Name = "train-apply-csv-big", Content = "CSV-Datei einordnen …", Padding = new Thickness(12, 5), IsEnabled = !_trBusy };
        csvBtn.Click += (_, _) => _ = TrainPickCsv(d);
        ToolTip.SetTip(csvBtn, "Jede Zeile bekommt eine Kategorie und eine Sicherheit (Strg+E).");
        var dirBtn = new Button { Name = "train-apply-folder-big", Content = "Ordner einordnen …", Padding = new Thickness(12, 5), Margin = new Thickness(6, 0, 0, 0), IsEnabled = !_trBusy };
        dirBtn.Click += (_, _) => _ = TrainPickFolder(d);
        ToolTip.SetTip(dirBtn, "Jede Textdatei (.txt, .md, .eml, .csv …) bekommt eine Kategorie (Strg+Umschalt+E).");
        sp.Children.Add(new WrapPanel { Children = { csvBtn, dirBtn } });
        if (_trBusy)
        {
            _trBar = new ProgressBar { Name = "train-batch-progress", Minimum = 0, Maximum = 1, Height = 8, Margin = new Thickness(0, 4, 0, 0) };
            _trBarText = new TextBlock { Name = "train-batch-progress-text", Foreground = Muted, FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
            var stop = new Button { Name = "train-batch-cancel", Content = "Abbrechen", Padding = new Thickness(10, 3), Margin = new Thickness(8, 0, 0, 0) };
            stop.Click += (_, _) => TrainBatchCancel();
            TrainProgressRefresh();
            sp.Children.Add(_trBar);
            sp.Children.Add(new WrapPanel { Children = { _trBarText, stop } });
        }
        var csv = d.Text("batch");
        if (csv.Length > 0)
        {
            var rows = Trendo.Core.Sys.Csv.Parse(csv, 13);
            var total = int.TryParse(d.Text("batchRows"), out var tr) ? tr : 0;
            sp.Children.Add(H($"Vorhersagen aus „{d.Text("batchSource")}“", 13));
            if (rows.Count > 0)
            {
                var head = rows[0];
                // Ordner: Datei, Vorhersage, Sicherheit; CSV: Textspalte(n) gekürzt, Vorhersage, Sicherheit
                var show = d.Text("batchKind") == "ordner" ? new[] { 0, 1, 2 } : new[] { int.TryParse(d.Text("batchTextCol"), out var tc) ? tc : 0, head.Count - 3, head.Count - 2 };
                var g = new Grid { Name = "train-batch-table", ColumnDefinitions = new ColumnDefinitions("*,110,70") };
                for (var r = 0; r < rows.Count; r++)
                {
                    g.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                    for (var c = 0; c < 3; c++)
                    {
                        var v = show[c] >= 0 && show[c] < rows[r].Count ? rows[r][show[c]] : "";
                        var tb = new TextBlock { Text = v.ReplaceLineEndings(" "), FontSize = 11.5, Foreground = r == 0 ? Muted : c == 1 ? Accent : Text, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 1, 8, 1),
                            FontWeight = r == 0 ? FontWeight.SemiBold : FontWeight.Normal };
                        Grid.SetRow(tb, r); Grid.SetColumn(tb, c); g.Children.Add(tb);
                    }
                }
                sp.Children.Add(g);
                if (total > rows.Count - 1) sp.Children.Add(P($"… und {(total - (rows.Count - 1)).ToString("N0", De)} weitere – alle stehen in der gespeicherten Datei.", Muted, 11));
            }
            var save = new Button { Name = "train-batch-save", Content = "Vorhersagen speichern …", Padding = new Thickness(12, 5), Margin = new Thickness(0, 4, 0, 0), IsEnabled = !_trBusy };
            save.Click += (_, _) => _ = TrainSaveBatch(d);
            sp.Children.Add(save);
        }
        return sp;
    }

    /// <summary>Ziehen und Ablegen auf der Arbeitsfläche: eine .csv-Datei → einordnen je Zeile; ein Ordner → je Datei.</summary>
    void TrainDropTarget(Control c, ProgramDocument d)
    {
        DragDrop.SetAllowDrop(c, true);
        c.AddHandler(DragDrop.DragOverEvent, (_, e) => { e.DragEffects = e.DataTransfer.Contains(DataFormat.File) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; });
        c.AddHandler(DragDrop.DropEvent, (_, e) =>
        {
            var item = e.DataTransfer.TryGetFiles()?.Select(f => f.TryGetLocalPath()).OfType<string>().FirstOrDefault();
            if (item is null) return;
            e.Handled = true;
            _ = TrainDropped(d, item);
        });
    }

    /// <summary>Abgelegte Datei oder abgelegten Ordner einordnen (auch für Tests).</summary>
    public Task<ClassifierBatch.Result?> TrainDropped(ProgramDocument d, string path) =>
        Directory.Exists(path) ? TrainApply(d, path, true)
        : path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ? TrainApply(d, path, false)
        : Task.FromResult<ClassifierBatch.Result?>(TrainDropRefused());

    ClassifierBatch.Result? TrainDropRefused() { _progMsg[TrainKey] = "Bitte eine CSV-Datei oder einen Ordner ablegen."; Show(TrainKey); return null; }

    /// <summary>Rechtsklick auf die Vorschau: dieselben Befehle wie die Knöpfe.</summary>
    ContextMenu TrainBatchMenu(ProgramDocument d)
    {
        MenuItem I(string name, string label, Action a, bool on = true) { var m = new MenuItem { Name = name, Header = label, IsEnabled = on }; m.Click += (_, _) => a(); return m; }
        var has = d.Text("batch").Length > 0; var model = TrainModel(d) is not null;
        return new ContextMenu { Items = {
            I("train-ctx-csv", "CSV-Datei einordnen …", () => _ = TrainPickCsv(d), model && !_trBusy),
            I("train-ctx-folder", "Ordner einordnen …", () => _ = TrainPickFolder(d), model && !_trBusy),
            I("train-ctx-save", "Vorhersagen speichern …", () => _ = TrainSaveBatch(d), has && !_trBusy),
            I("train-ctx-cancel", "Einordnen abbrechen", TrainBatchCancel, _trBusy) } };
    }

    /// <summary>Menüleiste „Modell“ mit Tastenkürzeln (dieselben Handler wie die Knöpfe).</summary>
    IEnumerable<MenuTop> TrainMenus(ProgramDocument d) =>
    [
        new("Modell",
        [
            new("train-key-run", "Trainieren", () => ProgramRun(TrainKey, () => TrainRun(d)), "F5"),
            MenuCmd.Separator,
            new("train-key-csv", "CSV-Datei einordnen …", () => _ = TrainPickCsv(d), "Strg+E", () => !_trBusy && TrainModel(d) is not null),
            new("train-key-folder", "Ordner einordnen …", () => _ = TrainPickFolder(d), "Strg+Umschalt+E", () => !_trBusy && TrainModel(d) is not null),
            new("train-key-save", "Vorhersagen speichern …", () => _ = TrainSaveBatch(d), "", () => d.Text("batch").Length > 0),
            new("train-key-cancel", "Einordnen abbrechen", TrainBatchCancel, "", () => _trBusy),
        ]),
    ];
}
