using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Trendo.Core;
using Trendo.Core.Arch;
using Trendo.Core.Cad;
using Trendo.Core.Programs;
using Path = System.IO.Path;

namespace Trendo.App;

/// <summary>T-0747: DXF in den Grundriss (SPEC-01) und DXF/SVG zu Fräsbahnen (SPEC-14). Beide lesen über den gemeinsamen Leser
/// <see cref="CadDxf"/> im Kern; jeder Import ist ein Rückgängig-Schritt im offenen Dokument. Dazu Dateiauswahl und Ziehen und Ablegen.</summary>
public partial class MainWindow
{
    public static readonly string[] ArchImportExts = ["dxf"];
    public static readonly string[] CncImportExts = ["dxf", "svg"];

    /// <summary>DXF-Grundriss in das aktive Geschoss übernehmen (ein Rückgängig-Schritt). Einzellinien bekommen die eingestellte Wandstärke.</summary>
    public string ArchImportDxf(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Diese DXF-Datei gibt es nicht.");
        var dxf = Readable(() => CadDxf.Read(path));
        var d = ProgramActive(AK) ?? throw new InvalidOperationException("Es ist kein Grundriss offen.");
        PlanDxfReport? rep = null;
        var thick = ArchState.WallThickness is >= 5 and <= 100 ? ArchState.WallThickness : 24;
        Readable(() => d.Edit($"DXF „{Path.GetFileName(path)}“ einlesen", x => { var p = FloorPlan.From(x); rep = PlanDxfImport.Import(p, dxf, new PlanDxfOptions { SingleWallCm = thick }); p.WriteTo(x); }));
        _archIssues = null; ArchState.Selected = null; ArchState.Fitted = false; ArchState.UserView = false;
        var msg = $"DXF „{Path.GetFileName(path)}“ eingelesen: {rep!.Text} Strg+Z nimmt es zurück.";
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
        CncEdit(d, $"Zeichnung „{Path.GetFileName(path)}“ einlesen", x => { x.Contours = j.Contours; x.CamSource = j.CamSource; x.Gcode = r.Gcode; });
        CncStop(); _cncT = -1;
        var msg = CncCamMessage(j, r);
        _progMsg[CK] = msg;
        _core.Audit.Append(Principal.LocalOwner.Name, "cnc.cam.import", Path.GetFileName(path));
        return msg;
    }

    /// <summary>Fräsbahnen aus den eingelesenen Konturen mit den aktuellen Werten neu berechnen (ein Rückgängig-Schritt).</summary>
    public string CncCamRegenerate(ProgramDocument d) => CncCamApply(d, "Fräsbahnen neu berechnen", _ => { });

    /// <summary>CAM-Wert ändern und die Bahnen gleich neu berechnen – zusammen ein Rückgängig-Schritt; geht die Berechnung nicht, bleibt alles wie es war.</summary>
    public string CncCamApply(ProgramDocument d, string label, Action<CncJob> change)
    {
        var j = CncJob.From(d.Data); change(j); var r = j.Cam(); Cnc.Analyse(r.Gcode);
        CncEdit(d, label, x => { change(x); x.Gcode = r.Gcode; });
        CncStop(); _cncT = -1;
        return _progMsg[CK] = CncCamMessage(j, r);
    }

    static string CncCamMessage(CncJob j, CamResult r)
    {
        var kinds = r.Paths.GroupBy(p => p.Kind).Select(g => $"{g.Count()} {(g.Key == "aussen" ? "außen" : g.Key == "innen" ? "innen" : "auf der Linie")}");
        var size = r.Contours.Count == 0 ? "" : $", Teil {Fach.F(r.Contours.Max(c => c.Box.MaxX), 1)} × {Fach.F(r.Contours.Max(c => c.Box.MaxY), 1)} mm";
        return $"„{j.CamSource}“: {r.Paths.Count} Fräsbahnen ({string.Join(", ", kinds)}){size}, Fräser Ø {Fach.F(j.ToolD, 1)} mm, Tiefe {Fach.F(j.Depth, 1)} mm in {Math.Ceiling(j.Depth / j.StepDown - 1e-9):0} Stufen{(j.CamTabs > 0 ? $", {j.CamTabs} Haltestege" : "")}."
            + (r.Warnings.Count == 0 ? "" : " ⚠ " + string.Join(" ", r.Warnings));
    }

    Task ArchDxfPick() => ProgramPickThen(AK, "arch-dxf-file", "DXF-Grundriss", ArchImportExts, false, f => ArchImportDxf(f[0]));
    Task CncDrawingPick() => ProgramPickThen(CK, "cnc-drawing-file", "Zeichnung (DXF, SVG)", CncImportExts, false, f => CncImportDrawing(f[0]));

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
