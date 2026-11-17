using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
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
/// Läuft im Hintergrund mit Fortschritt und „Abbrechen“; die ganze Ergebnistabelle liegt als Datei im Datenordner (<see cref="TrainBatchDir"/>, atomar geschrieben, Name = Prüfsumme),
/// im Dokument stehen nur Verweis, Kennzahlen und eine Vorschau – so bleibt der Rückgängig-Stapel klein, und Rückgängig nimmt den Verweis zurück.
/// „Vorhersagen (.csv)“ bringt die Tabelle als Datei hinaus. Nutzt den vorhandenen Klassifikator (<see cref="TextClassifier"/>) über <see cref="ClassifierBatch"/> – kein zweites Lernsystem.</summary>
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
    /// <summary>So viele Zeilen der Ergebnistabelle stehen als Vorschau im Dokument.</summary>
    public const int TrainPreviewRows = 12;
    /// <summary>Ablage der Ergebnistabellen (eine Datei je Ergebnis, Name aus der Prüfsumme – gleiche Ergebnisse teilen sich eine Datei, Rückgängig/Wiederholen finden ihre Datei wieder).</summary>
    public string TrainBatchDir => Path.Combine(_core.DataRoot, "ki-training", "vorhersagen");
    static readonly Regex TrainBatchName = new("^[0-9a-f]{24}\\.csv$", RegexOptions.CultureInvariant);

    /// <summary>Modell auf eine CSV-Datei (<paramref name="folder"/> = false) oder alle Textdateien eines Ordners anwenden.
    /// <paramref name="header"/>: null = erste Zeile selbst erkennen, sonst „Erste Zeile ist Überschrift“ wie gewählt.</summary>
    public async Task<ClassifierBatch.Result?> TrainApply(ProgramDocument d, string path, bool folder, bool? header = null)
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
        var modelHash = TrainModelHash(d); var dir = TrainBatchDir;
        try
        {
            // Einordnen, Tabelle bauen und Datei schreiben im Hintergrund – auf dem UI-Faden bleibt nur der kleine Verweis
            var (r, file, preview) = await Task.Run(() =>
            {
                var res = folder ? ClassifierBatch.ApplyFolder(m, path, report, ct) : ClassifierBatch.ApplyCsvFile(m, path, report, ct, header);
                ct.ThrowIfCancellationRequested();
                var (f, p) = TrainStoreBatch(dir, res);
                return (res, f, p);
            }, ct);
            var sum = ClassifierBatch.Summary(r);
            var inv = CultureInfo.InvariantCulture;
            d.Edit("Vorhersagen erstellt", x =>
            {
                x.Remove("batch"); // alte Dokumente hatten die ganze Tabelle im Dokument
                x["batchFile"] = file; x["batchPreview"] = preview;
                x["batchSource"] = Path.GetFileName(path.TrimEnd('\\', '/')); x["batchPath"] = path; x["batchKind"] = r.Kind;
                x["batchTextCol"] = r.TextColumn.ToString(inv); x["batchRows"] = r.Rows.Count.ToString(inv); x["batchTotal"] = r.Total.ToString(inv);
                x["batchTruncated"] = r.Truncated ? "ja" : ""; x["batchHeader"] = r.HasHeader ? "ja" : "nein"; x["batchModel"] = modelHash;
            });
            _core.Audit.Append(Principal.LocalOwner.Name, "ki-training.anwenden", $"{r.Rows.Count} {(folder ? "Dateien" : "Zeilen")}" + (r.Truncated ? $" von {r.Total}" : ""));
            var what = folder ? (r.Rows.Count == 1 ? "Datei" : "Dateien") : (r.Rows.Count == 1 ? "Zeile" : "Zeilen");
            var cats = ": " + string.Join(", ", sum.Take(5).Select(s => $"{s.Label} {s.Count}")) + (r.Skipped > 0 ? $" ({r.Skipped} übersprungen)" : "");
            _progMsg[TrainKey] = r.Truncated
                ? $"⚠ Nur die ersten {ClassifierBatch.MaxItems.ToString("N0", De)} von {r.Total.ToString("N0", De)} {(folder ? "Dateien" : "Zeilen")} eingeordnet (mehr geht in einem Lauf nicht){cats}. Teile die Datei auf, um den Rest einzuordnen."
                : $"✓ {r.Rows.Count} {what} eingeordnet{cats}. „Vorhersagen speichern …“ legt die Tabelle als CSV-Datei ab.";
            return r;
        }
        catch (OperationCanceledException) { _progMsg[TrainKey] = $"Abgebrochen nach {_trProg.Done} von {_trProg.Total} – es wurde nichts übernommen."; return null; }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
        { _progMsg[TrainKey] = "Einordnen ging nicht: " + ex.Message; return null; }
        finally { _trBusy = false; _trCancel.Dispose(); _trCancel = null; _trBar = null; _trBarText = null; Show(TrainKey); }
    }

    /// <summary>Ganze Tabelle als Datei (UTF-8 mit BOM, atomar) ablegen; zurück kommen der Dateiname und die Vorschau (Kopf + <see cref="TrainPreviewRows"/> Zeilen) für das Dokument.</summary>
    static (string File, string Preview) TrainStoreBatch(string dir, ClassifierBatch.Result r)
    {
        byte[] bytes = [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(ClassifierBatch.ToCsv(r))];
        var name = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes))[..24].ToLowerInvariant() + ".csv";
        Directory.CreateDirectory(dir);
        var full = Path.Combine(dir, name);
        if (!File.Exists(full) || new FileInfo(full).Length != bytes.Length) AtomicFile.WriteAllBytes(full, bytes);
        return (name, ClassifierBatch.ToCsv(r with { Rows = [.. r.Rows.Take(TrainPreviewRows)] }));
    }

    /// <summary>Fingerabdruck des Modells – steht bei den Vorhersagen, damit ein späteres Training als „älteres Modell“ auffällt.</summary>
    static string TrainModelHash(ProgramDocument d) => Sha256Text(d.Text("model"))[..16];

    /// <summary>Gibt es Vorhersagen (als Datei-Verweis oder – alte Dokumente – im Dokument)?</summary>
    static bool TrainHasBatch(ProgramDocument d) => d.Text("batch").Length > 0 || d.Text("batchFile").Length > 0;

    /// <summary>Pfad der Ergebnisdatei – nur Namen aus <see cref="TrainStoreBatch"/> (Prüfsumme), nie ein Pfad aus einem fremden Dokument.</summary>
    string? TrainBatchFile(ProgramDocument d) => d.Text("batchFile") is var n && TrainBatchName.IsMatch(n) ? Path.Combine(TrainBatchDir, n) : null;

    /// <summary>Die ganze Ergebnistabelle als CSV-Text ("" = keine oder Datei fehlt).</summary>
    public string TrainBatchCsv(ProgramDocument d)
    {
        if (d.Text("batch") is { Length: > 0 } legacy) return legacy;
        return TrainBatchFile(d) is { } f && File.Exists(f) ? File.ReadAllText(f, Encoding.UTF8) : "";
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
        if (!TrainHasBatch(d)) { _progMsg[TrainKey] = "Noch keine Vorhersagen – zuerst eine CSV-Datei oder einen Ordner einordnen."; Show(TrainKey); return; }
        var name = Path.GetFileNameWithoutExtension(d.Text("batchSource")); if (name.Length == 0) name = "vorhersagen";
        string? f;
        if (PickOverride is { } fake) f = (await fake(new PickRequest("train-batch-save", false, true, [".csv"]))) is [var x, ..] ? x : null;
        else f = (await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title = "Vorhersagen speichern", SuggestedFileName = name + "-vorhersagen.csv", DefaultExtension = "csv",
            FileTypeChoices = [new FilePickerFileType("CSV-Tabelle") { Patterns = ["*.csv"] }] }))?.TryGetLocalPath();
        if (f is null) return;
        ProgramRun(TrainKey, () => { TrainWriteBatch(d, f); _progMsg[TrainKey] = $"✓ Gespeichert: {Path.GetFileName(f)}"; });
    }

    /// <summary>Vorhersagen als CSV (UTF-8 mit BOM, Semikolon) – atomar, damit bei einem Absturz keine halbe Datei liegen bleibt.</summary>
    string TrainWriteBatch(ProgramDocument d, string f)
    {
        if (d.Text("batch") is { Length: > 0 } legacy) { AtomicFile.WriteAllBytes(f, [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(legacy)]); return f; }
        if (TrainBatchFile(d) is not { } src) throw new InvalidOperationException("Noch keine Vorhersagen – zuerst eine CSV-Datei oder einen Ordner einordnen.");
        if (!File.Exists(src)) throw new InvalidOperationException("Die Datei mit allen Vorhersagen fehlt auf diesem Rechner (z. B. weil das Dokument von einem anderen Rechner kommt) – bitte die CSV-Datei oder den Ordner noch einmal einordnen.");
        AtomicFile.WriteAllBytes(f, File.ReadAllBytes(src));
        return f;
    }

    /// <summary>„Erste Zeile ist Überschrift“ umgeschaltet: dieselbe CSV-Datei noch einmal einordnen.</summary>
    void TrainHeaderToggled(ProgramDocument d, bool header)
    {
        if (_trBusy || header == (d.Text("batchHeader") == "ja")) return;
        var path = d.Text("batchPath");
        if (path.Length == 0 || !File.Exists(path)) { _progMsg[TrainKey] = "Die CSV-Datei gibt es an der alten Stelle nicht mehr – bitte über „CSV-Datei einordnen …“ neu wählen."; Show(TrainKey); return; }
        _ = TrainApply(d, path, false, header);
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
        if (TrainHasBatch(d))
        {
            var legacy = d.Text("batch");
            var rows = Trendo.Core.Sys.Csv.Parse(legacy.Length > 0 ? legacy : d.Text("batchPreview"), TrainPreviewRows + 1);
            var total = int.TryParse(d.Text("batchRows"), out var tr) ? tr : 0;
            var csvKind = d.Text("batchKind") != "ordner";
            sp.Children.Add(H($"Vorhersagen aus „{d.Text("batchSource")}“", 13));
            // ehrliche Hinweise: gekürzt, älteres Modell, Datei fehlt
            if (d.Text("batchTruncated") == "ja")
            {
                var all = int.TryParse(d.Text("batchTotal"), out var tt) ? tt : 0;
                var t = $"⚠ Nur die ersten {ClassifierBatch.MaxItems.ToString("N0", De)} von {all.ToString("N0", De)} {(csvKind ? "Zeilen" : "Dateien")} sind eingeordnet – mehr geht in einem Lauf nicht. Teile die Datei auf, um den Rest einzuordnen.";
                var tb = P(t, StatusBrush(t), 11); tb.Name = "train-batch-truncated"; sp.Children.Add(tb);
            }
            if (d.Text("batchModel") is { Length: > 0 } bm && TrainModel(d) is not null && bm != TrainModelHash(d))
            {
                const string t = "⚠ Mit einem älteren Modell erstellt – seitdem wurde neu trainiert oder ein anderes Modell geladen. Noch einmal einordnen, um das aktuelle Modell zu nutzen.";
                var tb = P(t, StatusBrush(t), 11); tb.Name = "train-batch-oldmodel"; sp.Children.Add(tb);
            }
            if (legacy.Length == 0 && TrainBatchFile(d) is { } bf && !File.Exists(bf))
            {
                const string t = "⚠ Die Datei mit allen Vorhersagen fehlt auf diesem Rechner – hier steht nur die Vorschau. Bitte noch einmal einordnen.";
                var tb = P(t, StatusBrush(t), 11); tb.Name = "train-batch-missing"; sp.Children.Add(tb);
            }
            if (csvKind && d.Text("batchHeader").Length > 0)
            {
                var hb = new CheckBox { Name = "train-batch-header", Content = "Erste Zeile ist Überschrift", IsChecked = d.Text("batchHeader") == "ja", IsEnabled = !_trBusy, FontSize = 12 };
                ToolTip.SetTip(hb, "Trendo erkennt die Überschrift selbst. Stimmt das nicht, hier umschalten – die Datei wird dann noch einmal eingeordnet.");
                hb.IsCheckedChanged += (_, _) => TrainHeaderToggled(d, hb.IsChecked == true);
                sp.Children.Add(hb);
            }
            if (rows.Count > 0)
            {
                var head = rows[0];
                // Ordner: Datei, Vorhersage, Sicherheit; CSV: Textspalte(n) gekürzt, Vorhersage, Sicherheit
                var show = !csvKind ? new[] { 0, 1, 2 } : new[] { int.TryParse(d.Text("batchTextCol"), out var tc) ? tc : 0, head.Count - 3, head.Count - 2 };
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
        var has = TrainHasBatch(d); var model = TrainModel(d) is not null;
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
            new("train-key-save", "Vorhersagen speichern …", () => _ = TrainSaveBatch(d), "", () => TrainHasBatch(d)),
            new("train-key-cancel", "Einordnen abbrechen", TrainBatchCancel, "", () => _trBusy),
        ]),
    ];
}
