using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Trendo.Core;

namespace Trendo.App;

/// <summary>T-0732 (Audit-Restliste Rang 7): „Medien bearbeiten“ wandelt viele Dateien in einem Lauf um – Dateien wählen oder aus dem Explorer
/// auf die Werkbank ziehen, Aufgabe wählen, Ausgabeordner wählen, Start. Je Datei ein Fortschrittsbalken, „Abbrechen“ hält sofort an und
/// löscht die halbe Datei (<see cref="MediaBatch"/>). Originale bleiben unverändert; Ergebnisse landen im Ausgabeordner.</summary>
public partial class MainWindow
{
    const string MBK = "kreativ-medien";
    readonly List<MediaBatchItem> _mbItems = [];
    string _mbJob = "mp4";
    string? _mbOut;
    CancellationTokenSource? _mbCts;
    Task? _mbTask;
    string? _mbRunOut;
    readonly List<(MediaBatchItem It, ProgressBar Bar, TextBlock Label)> _mbRows = [];

    /// <summary>Dateien im Stapel (öffentlich für Tests).</summary>
    public IReadOnlyList<MediaBatchItem> MediaBatchItems => _mbItems;
    /// <summary>Gewählte Aufgabe des Stapels.</summary>
    public string MediaBatchJob { get => _mbJob; set => _mbJob = MediaStudio.Jobs.Any(j => j.Id == value) ? value : _mbJob; }
    /// <summary>Ausgabeordner (Standard: Medienordner\Stapel).</summary>
    public string MediaBatchOut { get => _mbOut ?? Path.Combine(MediaFolder, "Stapel"); set => _mbOut = string.IsNullOrWhiteSpace(value) ? null : value; }
    public bool MediaBatchRunning => _mbTask is { IsCompleted: false };
    /// <summary>Der laufende Stapel (Tests warten darauf).</summary>
    public Task? MediaBatchTask => _mbTask;

    /// <summary>Dateien in den Stapel legen; Doppelte und unbekannte Endungen werden übersprungen. Liefert die Zahl der neuen Dateien.</summary>
    public int MediaBatchAdd(IEnumerable<string> files)
    {
        if (MediaBatchRunning) { Say("Der Stapel läuft gerade – erst abwarten oder abbrechen.", MBK); return 0; }
        var n = 0; var bad = 0;
        foreach (var f in files.Select(x => x.Trim().Trim('"')).Where(x => x.Length > 0))
        {
            if (!File.Exists(f) || !MediaStudio.Inputs.Contains(Path.GetExtension(f).ToLowerInvariant())) { bad++; continue; }
            var full = Path.GetFullPath(f);
            if (_mbItems.Any(i => string.Equals(i.Input, full, StringComparison.OrdinalIgnoreCase))) continue;
            // fertige oder abgebrochene Läufe räumen nicht selbst auf – neue Dateien kommen dazu, alte Zustände gelten weiter bis „Liste leeren“
            _mbItems.Add(new MediaBatchItem(full)); n++;
        }
        Say(n == 0 ? (bad > 0 ? "Diese Dateien passen hier nicht – Video, Ton oder Bild ablegen." : "Schon im Stapel.")
            : $"{n} Datei(en) im Stapel – jetzt Aufgabe wählen und „Stapel starten“." + (bad > 0 ? $" ({bad} übersprungen – unbekanntes Format)" : ""), MBK);
        return n;
    }

    public void MediaBatchClear()
    {
        if (MediaBatchRunning) { Say("Der Stapel läuft gerade – erst abwarten oder abbrechen.", MBK); return; }
        _mbItems.Clear(); Say("Stapel geleert.", MBK);
    }

    /// <summary>Stapel starten (öffentlich für Tests). Bereits fertige Dateien werden nicht noch einmal umgewandelt.</summary>
    public Task MediaBatchStart()
    {
        if (MediaBatchRunning) return _mbTask!;
        if (FfmpegPath is not { } ff) { Say("Dafür fehlt FFmpeg. Unter Zusatzprogramme kannst du es installieren.", MBK); return Task.CompletedTask; }
        var todo = _mbItems.Where(i => i.State != MediaBatchState.Fertig).ToList();
        if (todo.Count == 0) { Say(_mbItems.Count == 0 ? "Zuerst Dateien in den Stapel legen – wählen oder hierher ziehen." : "Alles schon fertig. Für einen neuen Lauf „Liste leeren“.", MBK); return Task.CompletedTask; }
        foreach (var i in todo) i.Reset();
        var job = MediaStudio.Jobs.First(j => j.Id == _mbJob); var outDir = MediaBatchOut;
        if (File.Exists(outDir)) { Say("Der Ausgabeordner ist eine Datei – bitte unter „Ausgabeordner …“ einen Ordner wählen.", MBK); return Task.CompletedTask; }
        _mbCts = new CancellationTokenSource(); var ct = _mbCts.Token; _mbRunOut = outDir;
        _core.Audit.Append(Principal.LocalOwner.Name, "media.batch.start", $"{job.Id} n={todo.Count}");
        _w45Msg = $"Stapel läuft: {job.Label} für {todo.Count} Datei(en) …";
        _mbTask = Task.Run(() => MediaBatch.RunAsync(ff, todo, job.Id, outDir, it => Dispatcher.UIThread.Post(() => MediaBatchPaint(it)), ct, _mediaStart, Math.Max(0.1, _mediaEnd - _mediaStart)))
            .ContinueWith(t => Dispatcher.UIThread.Post(() => MediaBatchDone(job, todo, outDir, t.Exception)), TaskScheduler.Default);
        if (CurrentView == MBK) Show(MBK);
        return _mbTask;
    }

    /// <summary>Laufenden Stapel abbrechen: die Datei in Arbeit wird verworfen (keine halbe Datei), die übrigen bleiben „abgebrochen“.</summary>
    public void MediaBatchCancel() { if (MediaBatchRunning) { _mbCts?.Cancel(); _w45Msg = "Wird abgebrochen …"; if (CurrentView == MBK) Show(MBK); } }

    /// <summary>Gegenprüfung T-0732: Beim Schließen von Trendo einen laufenden Stapel abbrechen (FFmpeg wird beendet) und kurz warten, bis die
    /// Zwischendatei gelöscht ist. Hängt etwas, räumt Trendo die eigenen Zwischendateien (<c>*.~xxxxxxxx.teil.*</c>) im Ausgabeordner selbst weg.
    /// Fertige Ergebnisse bleiben unberührt.</summary>
    public void MediaBatchStopOnClose(int waitMs = 15000)
    {
        if (!MediaBatchRunning || _mbTask is not { } t) return;
        var outDir = _mbRunOut;
        try { _mbCts?.Cancel(); } catch (ObjectDisposedException) { }
        var done = false;
        try { done = t.Wait(waitMs); } catch (AggregateException) { done = true; }
        if (done || outDir is null || !Directory.Exists(outDir)) return;
        foreach (var f in Directory.GetFiles(outDir, "*.~*.teil*")) TryDelete(f);
    }

    void MediaBatchDone(MediaJob job, List<MediaBatchItem> todo, string outDir, AggregateException? ex)
    {
        int ok = todo.Count(i => i.State == MediaBatchState.Fertig), err = todo.Count(i => i.State == MediaBatchState.Fehler),
            skip = todo.Count(i => i.State == MediaBatchState.Uebersprungen), stop = todo.Count(i => i.State == MediaBatchState.Abgebrochen);
        _core.Audit.Append(Principal.LocalOwner.Name, "media.batch.end", $"{job.Id} ok={ok} fehler={err} uebersprungen={skip} abgebrochen={stop}");
        if (todo.LastOrDefault(i => i.State == MediaBatchState.Fertig)?.Output is { } last) _mediaLast = last;
        var msg = ex is not null ? "Stapel ging nicht: " + FriendlyError.Say(ex.InnerException ?? ex)
            : $"Stapel fertig: {ok} von {todo.Count} umgewandelt" + (err > 0 ? $", {err} mit Fehler" : "") + (skip > 0 ? $", {skip} übersprungen" : "") + (stop > 0 ? $", {stop} abgebrochen" : "") + $" – Ergebnisse in „{outDir}“.";
        _mbCts?.Dispose(); _mbCts = null;
        Say(msg, MBK);
    }

    void MediaBatchPaint(MediaBatchItem it)
    {
        foreach (var (i, bar, label) in _mbRows)
            if (ReferenceEquals(i, it)) { bar.Value = it.Fraction * 100; label.Text = it.StateText; label.Foreground = MediaBatchColor(it.State); }
    }

    static IBrush MediaBatchColor(MediaBatchState s) => s switch
    {
        MediaBatchState.Fertig => Brush.Parse("#4ADE80"),
        MediaBatchState.Fehler => Brush.Parse("#F87171"),
        MediaBatchState.Laeuft => Brush.Parse("#F5B83D"),
        _ => Muted,
    };

    async Task MediaBatchPick()
    {
        string[]? got;
        if (PickOverride is { } fake) got = await fake(new PickRequest("media-batch-pick", false, true, MediaStudio.Inputs));
        else got = (await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Dateien für den Stapel", AllowMultiple = true,
            FileTypeFilter = [new FilePickerFileType("Video, Ton und Bilder") { Patterns = MediaStudio.Inputs.Select(e => "*" + e).ToArray() }] })).Select(f => f.TryGetLocalPath()).OfType<string>().ToArray();
        if (got is { Length: > 0 }) MediaBatchAdd(got);
    }

    async Task MediaBatchPickOut()
    {
        string? dir;
        if (PickOverride is { } fake) dir = (await fake(new PickRequest("media-batch-out", true, false, []))) is [var f0, ..] ? f0 : null;
        else dir = (await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Wohin mit den Ergebnissen?" })).Select(x => x.TryGetLocalPath()).OfType<string>().FirstOrDefault();
        if (dir is null) return;
        if (File.Exists(dir)) { Say("Bitte einen Ordner wählen, keine Datei.", MBK); return; }
        MediaBatchOut = dir; Say("Ergebnisse kommen nach „" + dir + "“.", MBK);
    }

    /// <summary>Dateien aus dem Explorer annehmen: auf die Stapel-Karte → Stapel; auf die übrige Werkbank → eine Datei wird gewählt, mehrere kommen in den Stapel.</summary>
    void MediaWireDrop(Control c, bool batchOnly)
    {
        DragDrop.SetAllowDrop(c, true);
        c.AddHandler(DragDrop.DragOverEvent, (_, e) => { e.DragEffects = MediaDropFiles(e).Count > 0 ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; });
        c.AddHandler(DragDrop.DropEvent, (_, e) =>
        {
            e.Handled = true;
            var files = MediaDropFiles(e);
            if (files.Count == 0) { Say("Hier lassen sich nur Dateien ablegen.", MBK); return; }
            if (!batchOnly && files.Count == 1) MediaPick(files[0]); else MediaBatchAdd(files);
        });
    }

    static List<string> MediaDropFiles(DragEventArgs e) => e.DataTransfer.TryGetFiles()?.Select(f => f.TryGetLocalPath()).OfType<string>().Where(File.Exists).ToList() ?? [];

    /// <summary>Karte „Mehrere Dateien auf einmal“ oben in der Werkbank.</summary>
    private Control MediaBatchCard()
    {
        _mbRows.Clear();
        var box = new StackPanel { Name = "media-batch", Spacing = 6 };
        var running = MediaBatchRunning;
        box.Children.Add(H("Mehrere Dateien auf einmal", 13));
        var drop = new Border { Name = "media-batch-drop", BorderBrush = Brush.Parse("#66F5B83D"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(10, 8), Background = Brush.Parse("#10F5B83D"),
            Child = new TextBlock { Text = _mbItems.Count == 0 ? "Dateien hierher ziehen – Videos, Ton oder Bilder. Oder unten „Dateien wählen …“." : "Weitere Dateien hierher ziehen.", Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap } };
        box.Children.Add(drop);
        var list = new StackPanel { Name = "media-batch-list", Spacing = 3 };
        var n = 0;
        foreach (var it in _mbItems)
        {
            var bar = new ProgressBar { Name = $"media-batch-bar-{n}", Minimum = 0, Maximum = 100, Value = it.Fraction * 100, Height = 6, MinWidth = 120, VerticalAlignment = VerticalAlignment.Center, Foreground = Accent };
            var label = new TextBlock { Name = $"media-batch-state-{n}", Text = it.StateText, Foreground = MediaBatchColor(it.State), FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            var name = new TextBlock { Text = Path.GetFileName(it.Input), Foreground = Text, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            ToolTip.SetTip(name, it.Input + (it.Output is { } o ? "\n→ " + o : ""));
            var row = new Grid { Name = $"media-batch-item-{n}", ColumnDefinitions = new ColumnDefinitions("2*,120,*"), ColumnSpacing = 8, Children = { name, Pos(bar, 1), Pos(label, 2) } };
            list.Children.Add(row); _mbRows.Add((it, bar, label)); n++;
        }
        if (_mbItems.Count > 0) box.Children.Add(list);
        var jobs = new ComboBox { Name = "media-batch-job", ItemsSource = MediaStudio.Jobs.Select(j => j.Label).ToArray(), SelectedIndex = Math.Max(0, MediaStudio.Jobs.ToList().FindIndex(j => j.Id == _mbJob)), IsEnabled = !running, MinWidth = 200 };
        jobs.SelectionChanged += (_, _) => { if (jobs.SelectedIndex >= 0) _mbJob = MediaStudio.Jobs[jobs.SelectedIndex].Id; };
        ToolTip.SetTip(jobs, string.Join("\n", MediaStudio.Jobs.Select(j => $"{j.Label}: {j.Help}")));
        box.Children.Add(new WrapPanel { Children = { new TextBlock { Text = "Aufgabe", Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) }, jobs } });
        var outText = new TextBlock { Name = "media-batch-out", Text = "Ergebnisse in: " + MediaBatchOut, Foreground = Muted, FontSize = 11.5, TextWrapping = TextWrapping.Wrap };
        box.Children.Add(outText);
        var acts = new WrapPanel { ItemSpacing = 6, LineSpacing = 6 };
        if (running) acts.Children.Add(Act("media-batch-cancel", "Abbrechen", MediaBatchCancel));
        else
        {
            var go = Act("media-batch-start", _mbItems.Count > 0 ? $"Stapel starten ({_mbItems.Count(i => i.State != MediaBatchState.Fertig)})" : "Stapel starten", () => _ = MediaBatchStart());
            go.IsEnabled = _mbItems.Count > 0; acts.Children.Add(go);
            acts.Children.Add(Act("media-batch-pick", "Dateien wählen …", () => _ = MediaBatchPick()));
            acts.Children.Add(Act("media-batch-outpick", "Ausgabeordner …", () => _ = MediaBatchPickOut()));
            if (_mbItems.Count > 0) acts.Children.Add(Act("media-batch-clear", "Liste leeren", MediaBatchClear));
        }
        acts.Children.Add(Act("media-batch-open", "Ordner öffnen", () =>
        {
            try { Directory.CreateDirectory(MediaBatchOut); Launch(MediaBatchOut); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Say("Der Ausgabeordner lässt sich nicht anlegen: " + FriendlyError.Say(ex), MBK); }
        }));
        box.Children.Add(acts);
        var card = Card(box);
        MediaWireDrop(card, batchOnly: true);
        return card;
    }
}
