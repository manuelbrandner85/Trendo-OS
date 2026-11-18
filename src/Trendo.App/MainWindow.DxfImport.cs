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
/// T-0747-fix: große Dateien (ab <see cref="DrawingBackgroundBytes"/>) werden im Hintergrund gelesen – die Oberfläche bleibt bedienbar, eine Karte
/// zeigt den Fortschritt mit „Abbrechen“; erst das fertige Ergebnis wird in einem Schritt ins Dokument übernommen.</summary>
public partial class MainWindow
{
    public static readonly string[] ArchImportExts = ["dxf"];
    public static readonly string[] CncImportExts = ["dxf", "svg"];

    /// <summary>T-0747-fix: ab dieser Dateigröße liest Trendo die Zeichnung im Hintergrund.</summary>
    public const long DrawingBackgroundBytes = 512 * 1024;
    CancellationTokenSource? _drawCts; string? _drawKey, _drawFile;
    /// <summary>T-0747-fix: läuft gerade ein Einlesen im Hintergrund?</summary>
    public bool DrawingImportBusy => _drawCts is not null;
    /// <summary>T-0747-fix: Einlesen im Hintergrund abbrechen – das Dokument bleibt unverändert.</summary>
    public void DrawingImportCancel() => _drawCts?.Cancel();
    /// <summary>T-0747-fix (Tests): ist das gesetzt, wartet das Einlesen im Hintergrund vor dem Start darauf – so lässt sich „Abbrechen“ sicher prüfen.</summary>
    public ManualResetEventSlim? DrawingImportHold { get; set; }

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

    string CncDrawingApply(ProgramDocument d, string path, CncJob j, CamResult r)
    {
        var key = j.CamSettingsKey();
        CncEdit(d, $"Zeichnung „{Path.GetFileName(path)}“ einlesen", x => { x.Contours = j.Contours; x.CamSource = j.CamSource; x.SetCamGcode(r.Gcode, key); });
        CncStop(); _cncT = -1;
        var msg = CncCamMessage(j, r);
        _progMsg[CK] = msg;
        _core.Audit.Append(Principal.LocalOwner.Name, "cnc.cam.import", Path.GetFileName(path));
        return msg;
    }

    /// <summary>T-0747-fix: Datei aus Dateiauswahl oder Ziehen und Ablegen einlesen – kleine sofort, große im Hintergrund mit „Abbrechen“.</summary>
    public void ImportDrawingFile(string key, string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Diese Datei gibt es nicht.");
        if (new FileInfo(path).Length < DrawingBackgroundBytes) { if (key == AK) ArchImportDxf(path); else CncImportDrawing(path); return; }
        if (_drawCts is not null) throw new InvalidOperationException($"Es wird schon „{_drawFile}“ eingelesen – bitte warten oder „Abbrechen“ drücken.");
        var d = ProgramActive(key) ?? throw new InvalidOperationException(key == AK ? "Es ist kein Grundriss offen." : "Es ist kein CNC-Programm offen.");
        var cts = new CancellationTokenSource(); var ct = cts.Token; var name = Path.GetFileName(path);
        Func<Action> work;
        if (key == AK)
        {
            var snapshot = d.Data.ToJsonString(); var snap = (System.Text.Json.Nodes.JsonObject)System.Text.Json.Nodes.JsonNode.Parse(snapshot)!; var opts = ArchDxfOptions();
            work = () =>
            {
                var dxf = Readable(() => CadDxf.Read(path, ct));
                var p = FloorPlan.From(snap); var rep = Readable(() => PlanDxfImport.Import(p, dxf, opts, ct));
                return () =>
                {
                    // Ergebnis gilt nur für den Stand beim Start; wurde inzwischen etwas geändert, wird auf dem neuen Stand noch einmal eingesetzt
                    PlanDxfReport? rep2 = null;
                    Readable(() => d.Edit($"DXF „{name}“ einlesen", x => { if (x.ToJsonString() == snapshot) p.WriteTo(x); else { var q = FloorPlan.From(x); rep2 = PlanDxfImport.Import(q, dxf, opts); q.WriteTo(x); } }));
                    ArchDxfDone(path, rep2 ?? rep);
                };
            };
        }
        else
        {
            var j = CncJob.From(d.Data);
            work = () => { var r = Readable(() => j.ImportDrawing(path, ct)); Cnc.Analyse(r.Gcode); return () => CncDrawingApply(d, path, j, r); };
        }
        _drawCts = cts; _drawKey = key; _drawFile = name;
        _progMsg[key] = $"„{name}“ wird eingelesen ({new FileInfo(path).Length / 1024.0 / 1024:0.0} MB) – Trendo bleibt bedienbar.";
        var hold = DrawingImportHold;
        _ = Task.Run(() => { hold?.Wait(ct); return work(); }, ct).ContinueWith(t => Dispatcher.UIThread.Post(() =>
        {
            _drawCts = null; _drawKey = null; _drawFile = null; cts.Dispose();
            if (t.IsCanceled || t.Exception?.InnerException is OperationCanceledException) { _progMsg[key] = $"Einlesen von „{name}“ abgebrochen – nichts geändert."; _progErr.Remove(key); Show(key); return; }
            ProgramRun(key, () => { if (t.Exception?.InnerException is { } ex) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(ex); t.Result(); });
        }), TaskScheduler.Default);
    }

    /// <summary>T-0747-fix: Karte „wird eingelesen … Abbrechen“ über der Arbeitsfläche, solange das Einlesen im Hintergrund läuft.</summary>
    Control? DrawingImportCard(string key)
    {
        if (_drawCts is null || _drawKey != key) return null;
        var stop = new Button { Name = "draw-import-cancel", Content = "Abbrechen", Padding = new Thickness(12, 4), VerticalAlignment = VerticalAlignment.Center };
        stop.Click += (_, _) => { DrawingImportCancel(); _progMsg[key] = $"„{_drawFile}“: wird abgebrochen …"; Show(key); };
        var text = new TextBlock { Text = $"„{_drawFile}“ wird eingelesen … Du kannst weiterarbeiten; das Ergebnis kommt als ein Schritt (Strg+Z nimmt es zurück).", Foreground = Text, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
        var row = new DockPanel { LastChildFill = true }; DockPanel.SetDock(stop, Avalonia.Controls.Dock.Right); row.Children.Add(stop); row.Children.Add(text);
        return new Border { Name = "draw-import-card", Background = TileBg, BorderBrush = Accent, BorderThickness = new Thickness(4, 0, 0, 0), CornerRadius = new CornerRadius(8), Padding = new Thickness(14, 10), Child = row };
    }

    /// <summary>Fräsbahnen aus den eingelesenen Konturen mit den aktuellen Werten neu berechnen (ein Rückgängig-Schritt).</summary>
    public string CncCamRegenerate(ProgramDocument d) => CncCamApply(d, "Fräsbahnen neu berechnen", _ => { });

    /// <summary>CAM-Wert ändern und die Bahnen gleich neu berechnen – zusammen ein Rückgängig-Schritt; geht die Berechnung nicht, bleibt alles wie es war.</summary>
    public string CncCamApply(ProgramDocument d, string label, Action<CncJob> change)
    {
        var j = CncJob.From(d.Data); change(j); var r = j.Cam(); Cnc.Analyse(r.Gcode); var key = j.CamSettingsKey();
        CncEdit(d, label, x => { change(x); x.SetCamGcode(r.Gcode, key); });
        CncStop(); _cncT = -1;
        return _progMsg[CK] = CncCamMessage(j, r);
    }

    /// <summary>T-0747-fix: Fräser-Ø, Tiefe, Zustellung oder Vorschub geändert. Stammt der G-Code aus der Zeichnung, werden die Bahnen gleich neu
    /// berechnet (ein Rückgängig-Schritt). Geht das nicht (z. B. Fräser zu groß), wird nur der Wert übernommen; der G-Code gilt dann als veraltet und
    /// der Export „G-Code (.nc)“ ist gesperrt, bis „Bahnen neu“ klappt.</summary>
    public void CncSettingChanged(ProgramDocument d, string label, Action<CncJob> change, bool cam)
    {
        if (cam && CncJob.From(d.Data).IsCamGcode)
        {
            try { CncCamApply(d, label, change); return; }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                CncEdit(d, label, change);
                _progMsg[CK] = $"⚠ Bahnen nicht neu berechnet: {ex.Message} Der G-Code passt nicht mehr zu den Werten – der Export „G-Code (.nc)“ ist gesperrt, bis „Bahnen neu“ klappt.";
                return;
            }
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
