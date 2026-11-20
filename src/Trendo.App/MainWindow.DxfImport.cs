using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Trendo.Core;
using Trendo.Core.Arch;
using Trendo.Core.Cad;
using Trendo.Core.Programs;
using Path = System.IO.Path;

namespace Trendo.App;

/// <summary>T-0747: DXF in den Grundriss (SPEC-01) und DXF/SVG zu Fräsbahnen (SPEC-14). Beide lesen über den gemeinsamen Leser
/// <see cref="CadDxf"/> im Kern; jeder Import ist ein Rückgängig-Schritt im offenen Dokument. Dazu Dateiauswahl und Ziehen und Ablegen.
/// T-0747-fix/-fix2: Einlesen und Fräsbahn-Berechnung laufen immer im Hintergrund – die Oberfläche bleibt bedienbar, eine Karte
/// zeigt den Fortschritt mit „Abbrechen“; erst das fertige Ergebnis wird in einem Schritt ins Dokument übernommen.</summary>
public partial class MainWindow
{
    public static readonly string[] ArchImportExts = ["dxf"];
    public static readonly string[] CncImportExts = ["dxf", "svg"];

    CancellationTokenSource? _drawCts; string? _drawKey, _drawWhat;
    /// <summary>T-0747-fix: läuft gerade ein Einlesen oder eine Bahnberechnung im Hintergrund?</summary>
    public bool DrawingImportBusy => _drawCts is not null;
    /// <summary>T-0747-fix: Arbeit im Hintergrund abbrechen – das Dokument bleibt unverändert.</summary>
    public void DrawingImportCancel() => _drawCts?.Cancel();
    /// <summary>T-0747-fix (Tests): ist das gesetzt, wartet die Arbeit im Hintergrund vor dem Start darauf – so lässt sich „Abbrechen“ sicher prüfen.</summary>
    public ManualResetEventSlim? DrawingImportHold { get; set; }

    /// <summary>T-0747-fix2: Arbeit im Hintergrund (Einlesen, Fräsbahnen) mit Karte und „Abbrechen“. <paramref name="work"/> rechnet ohne die Oberfläche
    /// und liefert, was danach auf der Oberfläche übernommen wird (ein Rückgängig-Schritt). Fehler werden zur Meldung, Abbrechen ändert nichts.</summary>
    void RunBackground(string key, string what, Func<CancellationToken, Action> work, Func<Exception, bool>? onError = null)
    {
        if (_drawCts is not null) throw new InvalidOperationException($"Es läuft noch: {_drawWhat} – bitte warten oder „Abbrechen“ drücken.");
        var cts = new CancellationTokenSource(); var ct = cts.Token;
        _drawCts = cts; _drawKey = key; _drawWhat = what;
        _progMsg[key] = what + " … Trendo bleibt bedienbar."; _progErr.Remove(key);
        var hold = DrawingImportHold;
        _ = Task.Run(() => { hold?.Wait(ct); return work(ct); }, ct).ContinueWith(t => Dispatcher.UIThread.Post(() =>
        {
            _drawCts = null; _drawKey = null; _drawWhat = null; cts.Dispose();
            if (t.IsCanceled || t.Exception?.InnerException is OperationCanceledException) { _camQueue.Clear(); _importQueue.Clear(); _progMsg[key] = $"Vorgang abgebrochen – nichts geändert ({what})."; _progErr.Remove(key); Show(key); return; }
            if (t.Exception?.InnerException is { } ex && onError is not null && onError(ex)) { Show(key); if (key == CK) CncRunQueued(); else CncRunQueuedImport(); return; }
            ProgramRun(key, () => { if (t.Exception?.InnerException is { } e2) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(e2); t.Result(); });
            if (key == CK) CncRunQueued(); else CncRunQueuedImport(); // T-0747-fix3/-fix4: zwischendurch geänderte Werte bzw. gewählte Dateien nachholen
        }), TaskScheduler.Default);
    }

    /// <summary>DXF-Grundriss in das aktive Geschoss übernehmen (ein Rückgängig-Schritt). Einzellinien bekommen die eingestellte Wandstärke.</summary>
    public string ArchImportDxf(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Diese DXF-Datei gibt es nicht.");
        var dxf = Readable(() => CadDxf.Read(path));
        var d = ProgramActive(AK) ?? throw new InvalidOperationException("Es ist kein Grundriss offen.");
        PlanDxfReport? rep = null;
        var opts = ArchDxfOptions();
        Readable(() => d.Edit($"DXF „{Path.GetFileName(path)}“ einlesen", x => { var p = FloorPlan.From(x); rep = PlanDxfImport.Import(p, dxf, opts); p.WriteTo(x); }));
        return ArchDxfDone(path, rep!);
    }

    PlanDxfOptions ArchDxfOptions() => new() { SingleWallCm = ArchState.WallThickness is >= 5 and <= 100 ? ArchState.WallThickness : 24 };

    string ArchDxfDone(string path, PlanDxfReport rep)
    {
        _archIssues = null; ArchState.Selected = null; ArchState.Fitted = false; ArchState.UserView = false;
        var msg = $"DXF „{Path.GetFileName(path)}“ eingelesen: {rep.Text} Strg+Z nimmt es zurück.";
        _progMsg[AK] = msg;
        _core.Audit.Append(Principal.LocalOwner.Name, "arch.dxf.import", Path.GetFileName(path));
        return msg;
    }

    /// <summary>Zeichnung (DXF oder SVG) für Fräsbahnen einlesen: Konturen ins Dokument, G-Code neu (ein Rückgängig-Schritt).</summary>
    public string CncImportDrawing(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Diese Datei gibt es nicht.");
        var d = ProgramActive(CK) ?? throw new InvalidOperationException("Es ist kein CNC-Programm offen.");
        var j = CncJob.From(d.Data); var r = Readable(() => j.ImportDrawing(path)); Cnc.Analyse(r.Gcode);
        return CncDrawingApply(d, path, j, r);
    }

    string CncDrawingApply(ProgramDocument d, string path, CncJob j, CamResult r, string? snapshot = null)
    {
        if (snapshot is not null && !DocOpen(CK, d)) return _progMsg[CK] = $"„{Path.GetFileName(path)}“: das Dokument wurde inzwischen geschlossen – nichts eingelesen."; // T-0747-fix4
        if (snapshot is not null && d.Data.ToJsonString() != snapshot) // T-0747-fix3: Dokument hat sich während des Einlesens geändert
            return _progMsg[CK] = $"⚠ Während „{Path.GetFileName(path)}“ eingelesen wurde, hat sich das Dokument geändert – das Ergebnis wurde verworfen, damit nichts überschrieben wird. Bitte noch einmal einlesen.";
        var key = j.CamSettingsKey();
        CncEdit(d, $"Zeichnung „{Path.GetFileName(path)}“ einlesen", x => { x.Contours = j.Contours; x.CamSource = j.CamSource; x.SetCamGcode(r.Gcode, key); });
        CncStop(); _cncT = -1;
        var msg = CncCamMessage(j, r);
        _progMsg[CK] = msg;
        _core.Audit.Append(Principal.LocalOwner.Name, "cnc.cam.import", Path.GetFileName(path));
        return msg;
    }

    /// <summary>T-0747-fix2: Datei aus Dateiauswahl oder Ziehen und Ablegen einlesen – immer im Hintergrund mit „Abbrechen“ (auch kleine Dateien
    /// können lange rechnen, z. B. ein Zickzack mit vielen Punkten).</summary>
    public void ImportDrawingFile(string key, string path) => ImportDrawingFile(key, path, null);

    void ImportDrawingFile(string key, string path, ProgramDocument? target)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Diese Datei gibt es nicht.");
        var d = target ?? ProgramActive(key) ?? throw new InvalidOperationException(key == AK ? "Es ist kein Grundriss offen." : "Es ist kein CNC-Programm offen.");
        if (_drawCts is not null) // T-0747-fix4: läuft schon etwas, wird die Datei danach eingelesen statt mit Fehler abgewiesen
        {
            _importQueue.Add((key, path, d));
            _progMsg[key] = $"„{Path.GetFileName(path)}“ wird eingelesen, sobald die laufende Arbeit fertig ist.";
            return;
        }
        var name = Path.GetFileName(path);
        if (key == AK)
        {
            var snapshot = d.Data.ToJsonString(); var snap = (System.Text.Json.Nodes.JsonObject)System.Text.Json.Nodes.JsonNode.Parse(snapshot)!; var opts = ArchDxfOptions();
            RunBackground(key, $"„{name}“ wird eingelesen", ct =>
            {
                var dxf = Readable(() => CadDxf.Read(path, ct));
                var p = FloorPlan.From(snap); var rep = Readable(() => PlanDxfImport.Import(p, dxf, opts, ct));
                return () =>
                {
                    // Ergebnis gilt nur für den Stand beim Start; wurde inzwischen etwas geändert, wird auf dem neuen Stand noch einmal eingesetzt
                    if (!DocOpen(AK, d)) { _progMsg[AK] = $"„{name}“: der Grundriss wurde inzwischen geschlossen – nichts eingelesen."; return; } // T-0747-fix4
                    PlanDxfReport? rep2 = null;
                    Readable(() => d.Edit($"DXF „{name}“ einlesen", x => { if (x.ToJsonString() == snapshot) p.WriteTo(x); else { var q = FloorPlan.From(x); rep2 = PlanDxfImport.Import(q, dxf, opts); q.WriteTo(x); } }));
                    ArchDxfDone(path, rep2 ?? rep);
                };
            });
        }
        else
        {
            var j = CncJob.From(d.Data); var snap = d.Data.ToJsonString();
            RunBackground(key, $"„{name}“ wird eingelesen", ct => { var r = Readable(() => j.ImportDrawing(path, ct)); Cnc.Analyse(r.Gcode); return () => CncDrawingApply(d, path, j, r, snap); });
        }
    }

    /// <summary>T-0747-fix: Karte „wird eingelesen … Abbrechen“ über der Arbeitsfläche, solange das Einlesen im Hintergrund läuft.</summary>
    Control? DrawingImportCard(string key)
    {
        if (_drawCts is null || _drawKey != key) return null;
        var stop = new Button { Name = "draw-import-cancel", Content = "Abbrechen", Padding = new Thickness(12, 4), VerticalAlignment = VerticalAlignment.Center };
        stop.Click += (_, _) => { DrawingImportCancel(); _progMsg[key] = $"{_drawWhat}: wird abgebrochen …"; Show(key); };
        var text = new TextBlock { Text = $"{_drawWhat} … Du kannst weiterarbeiten; das Ergebnis kommt als ein Schritt (Strg+Z nimmt es zurück).", Foreground = Text, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
        var row = new DockPanel { LastChildFill = true }; DockPanel.SetDock(stop, Avalonia.Controls.Dock.Right); row.Children.Add(stop); row.Children.Add(text);
        return new Border { Name = "draw-import-card", Background = TileBg, BorderBrush = Accent, BorderThickness = new Thickness(4, 0, 0, 0), CornerRadius = new CornerRadius(8), Padding = new Thickness(14, 10), Child = row };
    }

    /// <summary>Fräsbahnen aus den eingelesenen Konturen mit den aktuellen Werten neu berechnen (ein Rückgängig-Schritt, im Hintergrund).</summary>
    public string CncCamRegenerate(ProgramDocument d) => CncCamApply(d, "Fräsbahnen neu berechnen", _ => { });

    /// <summary>CAM-Wert ändern und die Bahnen neu berechnen – zusammen ein Rückgängig-Schritt; geht die Berechnung nicht, bleibt alles wie es war.
    /// T-0747-fix2: die Rechnung läuft im Hintergrund (mit „Abbrechen“), übernommen wird erst das fertige Ergebnis.</summary>
    public string CncCamApply(ProgramDocument d, string label, Action<CncJob> change) => CncCamApply(d, label, change, null);

    string CncCamApply(ProgramDocument d, string label, Action<CncJob> change, Func<Exception, bool>? onError, bool viaSetting = false)
    {
        if (_drawCts is not null && _drawKey == CK) // T-0747-fix3: läuft schon eine Rechnung, wird die Änderung danach übernommen statt verworfen
        {
            _camQueue.Add((d, label, change, viaSetting)); // T-0747-fix4: an das Dokument gebunden
            return _progMsg[CK] = $"„{label}“ wird übernommen, sobald die laufende Rechnung fertig ist.";
        }
        var j = CncJob.From(d.Data); change(j);
        if (j.Contours.Count == 0) throw new InvalidOperationException("Es ist noch keine Zeichnung eingelesen – unter „Erzeugen“ auf „Aus DXF/SVG …“ klicken.");
        var settings = j.CamSettings(); var contours = j.Contours; var key = j.CamSettingsKey();
        var snap = d.Data.ToJsonString(); // T-0747-fix3: Stand beim Start – nur auf genau diesen Stand wird das Ergebnis übernommen
        RunBackground(CK, "Fräsbahnen werden berechnet", ct =>
        {
            var r = CncCam.Generate(contours, settings, ct); Cnc.Analyse(r.Gcode);
            return () =>
            {
                if (!DocOpen(CK, d)) return; // T-0747-fix4: Dokument inzwischen geschlossen – Ergebnis verfällt
                if (d.Data.ToJsonString() != snap) { _progMsg[CK] = StaleResultMessage; return; }
                CncEdit(d, label, x => { change(x); x.SetCamGcode(r.Gcode, key); });
                CncStop(); _cncT = -1;
                _progMsg[CK] = CncCamMessage(j, r);
            };
        }, onError is null ? null : ex => { if (!DocOpen(CK, d)) return true; if (d.Data.ToJsonString() != snap) { _progMsg[CK] = StaleResultMessage; return true; } return onError(ex); });
        return _progMsg[CK];
    }

    const string StaleResultMessage = "⚠ Während der Rechnung wurde die Zeichnung, der G-Code oder ein Wert geändert (z. B. Rückgängig oder von Hand) – das Ergebnis passt nicht mehr und wurde verworfen. Bei Bedarf „Bahnen neu“ drücken.";

    /// <summary>T-0747-fix3: Änderungen, die während einer laufenden Bahnberechnung kommen; nach dem Ende als ein Schritt nachgerechnet.</summary>
    readonly List<(ProgramDocument Doc, string Label, Action<CncJob> Change, bool ViaSetting)> _camQueue = [];
    /// <summary>T-0747-fix4: Dateien, die während eines laufenden Einlesens gewählt wurden – danach in ihr Dokument eingelesen.</summary>
    readonly List<(string Key, string Path, ProgramDocument Doc)> _importQueue = [];
    bool DocOpen(string key, ProgramDocument d) => ProgramDocs(key).Contains(d);

    /// <summary>T-0747-fix4: nächste gemerkte Datei einlesen (nur, wenn ihr Dokument noch offen ist).</summary>
    void CncRunQueuedImport()
    {
        if (_drawCts is not null) return;
        _importQueue.RemoveAll(it => !DocOpen(it.Key, it.Doc));
        if (_importQueue.Count == 0) return;
        var (key, path, doc) = _importQueue[0]; _importQueue.RemoveAt(0);
        ProgramRun(key, () => ImportDrawingFile(key, path, doc));
    }

    void CncRunQueued()
    {
        if (_drawCts is not null) return;
        _camQueue.RemoveAll(it => !DocOpen(CK, it.Doc)); // T-0747-fix4: geschlossene Dokumente – ihre Änderungen verfallen
        if (_camQueue.Count == 0) { CncRunQueuedImport(); return; }
        var d = _camQueue[0].Doc; // T-0747-fix4: nur das Dokument, zu dem die Änderungen gehören – nicht das gerade aktive
        var q = _camQueue.Where(it => it.Doc == d).ToList(); _camQueue.RemoveAll(it => it.Doc == d);
        void All(CncJob x) { foreach (var it in q) it.Change(x); }
        var label = string.Join(", ", q.Select(it => it.Label).Distinct());
        ProgramRun(CK, () => { if (q.All(it => it.ViaSetting)) CncSettingChanged(d, label, All, true); else CncCamApply(d, label, All); });
    }

    /// <summary>T-0747-fix: Fräser-Ø, Tiefe, Zustellung oder Vorschub geändert. Stammt der G-Code aus der Zeichnung, werden die Bahnen neu berechnet
    /// (im Hintergrund, ein Rückgängig-Schritt). Geht das nicht (z. B. Fräser zu groß), wird nur der Wert übernommen; der G-Code gilt dann als veraltet und
    /// der Export „G-Code (.nc)“ ist gesperrt, bis „Bahnen neu“ klappt.</summary>
    public void CncSettingChanged(ProgramDocument d, string label, Action<CncJob> change, bool cam)
    {
        if (cam && CncJob.From(d.Data).IsCamGcode)
        {
            CncCamApply(d, label, change, ex =>
            {
                if (ex is not (InvalidOperationException or ArgumentException)) return false;
                CncEdit(d, label, change);
                _progMsg[CK] = $"⚠ Bahnen nicht neu berechnet: {ex.Message} Der G-Code passt nicht mehr zu den Werten – der Export „G-Code (.nc)“ ist gesperrt, bis „Bahnen neu“ klappt.";
                return true;
            }, viaSetting: true);
            return;
        }
        CncEdit(d, label, change);
    }

    /// <summary>T-0747-fix: Export des G-Codes – gesperrt, wenn die Bahnen aus der Zeichnung nicht mehr zu Fräser-Ø, Tiefe, Zustellung … passen.</summary>
    static string CncExportNc(ProgramDocument d, string f)
    {
        var j = CncJob.From(d.Data);
        if (j.CamStale) throw new InvalidOperationException("Der G-Code passt nicht mehr zu Fräser-Ø, Tiefe, Zustellung oder Vorschub – erst „Bahnen neu“ drücken, dann exportieren.");
        File.WriteAllText(f, j.Gcode); return f;
    }

    static string CncCamMessage(CncJob j, CamResult r)
    {
        var kinds = r.Paths.GroupBy(p => p.Kind).Select(g => $"{g.Count()} {(g.Key == "aussen" ? "außen" : g.Key == "innen" ? "innen" : "auf der Linie")}");
        var size = r.Contours.Count == 0 ? "" : $", Teil {Fach.F(r.Contours.Max(c => c.Box.MaxX), 1)} × {Fach.F(r.Contours.Max(c => c.Box.MaxY), 1)} mm";
        return $"„{j.CamSource}“: {r.Paths.Count} Fräsbahnen ({string.Join(", ", kinds)}){size}, Fräser Ø {Fach.F(j.ToolD, 1)} mm, Tiefe {Fach.F(j.Depth, 1)} mm in {Math.Ceiling(j.Depth / j.StepDown - 1e-9):0} Stufen{(j.CamTabs > 0 ? $", {j.CamTabs} Haltestege" : "")}."
            + (r.Warnings.Count == 0 ? "" : " ⚠ " + string.Join(" ", r.Warnings));
    }

    Task ArchDxfPick() => ProgramPickThen(AK, "arch-dxf-file", "DXF-Grundriss", ArchImportExts, false, f => ImportDrawingFile(AK, f[0]));
    Task CncDrawingPick() => ProgramPickThen(CK, "cnc-drawing-file", "Zeichnung (DXF, SVG)", CncImportExts, false, f => ImportDrawingFile(CK, f[0]));

    /// <summary>Dateien auf die Arbeitsfläche ziehen: <paramref name="use"/> bekommt jede passende Datei (Fehler werden zur Meldung).</summary>
    void FileDrop(Control target, string key, Func<string, bool> accepts, Action<string> use)
    {
        DragDrop.SetAllowDrop(target, true);
        target.AddHandler(DragDrop.DragOverEvent, (_, e) => { e.DragEffects = DroppedFiles(e).Any(accepts) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; });
        target.AddHandler(DragDrop.DropEvent, (_, e) =>
        {
            var files = DroppedFiles(e).Where(accepts).ToList(); e.Handled = true;
            if (files.Count > 0) ProgramRun(key, () => { foreach (var f in files) use(f); });
        });
        static IEnumerable<string> DroppedFiles(DragEventArgs e) => e.DataTransfer.TryGetFiles()?.Select(f => f.TryGetLocalPath()).OfType<string>() ?? [];
    }

    /// <summary>Lesefehler der Zeichnung als „Datei unlesbar“ melden (wird in der Programm-Hülle zur Meldung mit Knopf).</summary>
    static T Readable<T>(Func<T> f) { try { return f(); } catch (CadError e) { throw new InvalidDataException(e.Message, e); } }
    static void Readable(Action f) => Readable(() => { f(); return 0; });

    static bool HasExt(string f, params string[] exts) => exts.Any(x => Path.GetExtension(f).Equals("." + x, StringComparison.OrdinalIgnoreCase));
}
