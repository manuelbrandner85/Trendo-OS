using System.Text.Json.Nodes;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Trendo.Core;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>Alles live mitverfolgen (Wunsch 02.11.2026, alle Studios): Trendo beobachtet den Projektordner, solange Claude arbeitet, und zeigt jede Änderung sofort im passenden
/// Programm – egal, wie Claude sie macht. Kommen in einem Programm-Dokument viele neue Teile auf einmal (z. B. alle Wände einer Wohnung), erscheinen sie nacheinander;
/// Rückgängig bleibt ein einziger Schritt.</summary>
public partial class MainWindow
{
    /// <summary>Pause zwischen zwei Live-Zwischenständen (Tests: 0 = sofort der Endstand).</summary>
    public static int LiveStepDelay { get; set; } = 160;
    /// <summary>Für Tests: wird bei jeder Live-Übernahme aus dem Projektordner gezählt.</summary>
    public static int LiveSyncs;
    /// <summary>Für Tests: Anzahl gezeigter Live-Zwischenstände.</summary>
    public static int LiveSteps;
    readonly Dictionary<string, (CancellationTokenSource Cts, Task Run, string Target)> _liveAnim = [];

    /// <summary>Endstand, der gerade live eingespielt wird (oder die Daten des Dokuments).</summary>
    string LiveTarget(string page, ProgramDocument doc) => _liveAnim.TryGetValue(page, out var a) ? a.Target : doc.Data.ToJsonString();

    /// <summary>Neue Daten im Programm-Dokument zeigen – mit vielen neuen Teilen nacheinander, sonst sofort.</summary>
    void ClaudeApplyLive(ClaudeDock dock, ProgramDocument doc, JsonObject fresh)
    {
        var page = dock.Page; var final = fresh.ToJsonString();
        Interlocked.Increment(ref LiveSyncs);
        if (_liveAnim.Remove(page, out var running)) { running.Cts.Cancel(); } // vorige Wiedergabe springt an ihr Ende (ihr „finally“)
        ClaudeChangeBegin(page, doc);
        var steps = LiveStepDelay > 0 ? LiveReplay.Steps(doc.Data, fresh) : [];
        if (steps.Count == 0) { doc.ReplaceData(final); ClaudeChangeAfter(page, doc); ClaudeLiveShown(page); return; }
        var cts = new CancellationTokenSource();
        var task = Animate();
        _liveAnim[page] = (cts, task, final);

        async Task Animate()
        {
            var mark = doc.Remember();
            var delay = Math.Clamp(2500 / steps.Count, 40, LiveStepDelay);
            try
            {
                foreach (var s in steps)
                {
                    if (cts.IsCancellationRequested) break;
                    doc.ReplaceData(s.ToJsonString()); Interlocked.Increment(ref LiveSteps);
                    if (CurrentView == page) Show(page);
                    await Task.Delay(delay, cts.Token);
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                doc.Rollback(mark); doc.ReplaceData(final); // ein einziger Rückgängig-Schritt für die ganze Änderung
                ClaudeChangeAfter(page, doc);
                if (_liveAnim.TryGetValue(page, out var cur) && cur.Cts == cts) _liveAnim.Remove(page);
                ClaudeLiveShown(page);
            }
        }
    }

    void ClaudeLiveShown(string page)
    {
        _progMsg[page] = "✳ Claude hat das Dokument geändert – oben vergleichen, behalten oder verwerfen.";
        if (CurrentView == page) Show(page);
        Dispatcher.UIThread.Post(() => { if (ActivePageRoot?.GetLogicalDescendants().OfType<Avalonia.Controls.Control>().FirstOrDefault(c => c.Name == "prog-workspace") is { } ws) ClaudeFlash(ws); }, DispatcherPriority.Background); // zeigt, wo Claude gearbeitet hat
    }

    /// <summary>Für Tests: läuft gerade eine Live-Wiedergabe in diesem Programm?</summary>
    public bool ClaudeLiveAnimating(string page) => _liveAnim.ContainsKey(page);

    /// <summary>Projektordner beobachten, solange Claude arbeitet: jede geänderte Datei geht (kurz gebündelt) an die Live-Anzeige des Programms.</summary>
    IDisposable? ClaudeWatch(ClaudeDock dock, string folder, Func<string?> workFile)
    {
        FileSystemWatcher w;
        try { w = new FileSystemWatcher(folder) { IncludeSubdirectories = true, NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size }; }
        catch (Exception e) when (e is ArgumentException or IOException or PlatformNotSupportedException) { return null; }
        var pending = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void On(string path)
        {
            var rel = Path.GetRelativePath(folder, path);
            if (rel.Split(Path.DirectorySeparatorChar).Any(p => p is ".trendo" or ".claude" or "anhang" or ".git" or "node_modules") || path.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)) return;
            lock (pending) if (!pending.Add(path)) return;
            _ = Task.Delay(220).ContinueWith(_ => Dispatcher.UIThread.Post(() => // kurz bündeln: mehrere Schreibvorgänge an derselben Datei → eine Anzeige
            {
                lock (pending) pending.Remove(path);
                if (!File.Exists(path) || !_chatUis.ContainsKey(dock)) return;
                ClaudeLiveChanged(dock, path, workFile(), null);
                ClaudeLiveProgram(dock, folder, path);
            }), TaskScheduler.Default);
        }
        w.Changed += (_, e) => On(e.FullPath); w.Created += (_, e) => On(e.FullPath); w.Renamed += (_, e) => On(e.FullPath);
        w.EnableRaisingEvents = true;
        return w;
    }

    /// <summary>Live in den Programmen ohne Studio-Dokument: Text und Tabelle öffnen die Datei (nie über Ungespeichertes), Webseite, 3D und Editor zeigen sie.</summary>
    void ClaudeLiveProgram(ClaudeDock dock, string folder, string file)
    {
        var ext = Path.GetExtension(file).ToLowerInvariant();
        switch (dock.Page)
        {
            case "text" when ext is ".md" or ".txt":
                if (TextDirty && !string.Equals(TextPath, file, StringComparison.OrdinalIgnoreCase)) return;
                try { TextOpen(file); Interlocked.Increment(ref LiveSyncs); if (CurrentView == "text") Show("text"); } catch (Exception e) when (e is IOException or InvalidDataException) { }
                break;
            case "tabelle" when ext == ".csv":
                if (SheetDirty && !string.Equals(SheetPath, file, StringComparison.OrdinalIgnoreCase)) return;
                try { SheetOpen(file); Interlocked.Increment(ref LiveSyncs); if (CurrentView == "tabelle") Show("tabelle"); } catch (Exception e) when (e is IOException or InvalidDataException) { }
                break;
            case "dev" or "3d" or "editor":
                _ = ClaudeShowInStudio(dock, folder, [Path.GetRelativePath(folder, file).Replace('\\', '/')]);
                Interlocked.Increment(ref LiveSyncs);
                break;
        }
    }
}
