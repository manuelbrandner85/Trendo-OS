using Trendo.Core;
using Trendo.Core.Arch;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>Alle Studios, Runde 3 – SPEC-01 „Wohnung planen“: Grundriss als IFC4 (BIM) mit IfcOpenShell (LGPL-3.0, eigener Prozess, eigene Python-Umgebung).
/// Einrichtung erst nach Klick (= Zustimmung) über die vorhandene Kette für Python-Werkzeuge (uv, feste Fassung, Audit-Log).</summary>
public partial class MainWindow
{
    /// <summary>Für Tests: IfcOpenShell nachbilden (Modus „export“/„info“, Eingabe, Ausgabe) – schreibt die Ausgabe selbst.</summary>
    public static Func<string, string, string, Cli.Result>? IfcRunOverride { get; set; }
    public string? ArchIfcLastInfo { get; private set; }

    static readonly KiLibraryModel IfcModel = new("wohnung-ifc", KiKind.Modell3D, KiRuntime.Ifc, "IFC-Austausch (IfcOpenShell)", "", 0.1, 2, "LGPL-3.0",
        "Grundrisse für Architekten- und CAD-Programme (Revit, ArchiCAD, BlenderBIM, FreeCAD)", "Im Wohnungsplaner: Exportieren → „IFC (BIM)“.", []);

    bool IfcReady => IfcRunOverride is not null || KiStore.RuntimeReady(KiRuntime.Ifc);

    Cli.Result IfcRun(string mode, string input, string output)
    {
        if (IfcRunOverride is { } o) return o(mode, input, output);
        var app = KiPyApps.For(KiRuntime.Ifc)!; var p = KiStore.PyPaths(KiRuntime.Ifc);
        if (_opt.Isolated) return new Cli.Result(-1, "", "Testmodus: IfcOpenShell wird nicht gestartet.");
        CopyKiScript(app, p);
        return Cli.Run(p.PythonExe, [Path.Combine(p.Dir, app.Script), p.Repo, mode, input, output], p.Dir, 120_000, KiPyApps.UvEnv(p.UvHome));
    }

    /// <summary>Export „IFC (BIM)“: aktives Geschoss → IFC4 (Wände, Türen, Fenster, Räume); danach zur Kontrolle wieder eingelesen.</summary>
    string ArchIfcExport(ProgramDocument d, string file)
    {
        if (!IfcReady) throw new InvalidOperationException("Für IFC zuerst im Menüband „BIM → IFC einrichten“ klicken (Stufe „Professionell“; einmalig, kostenlos, LGPL-3.0).");
        var plan = FloorPlan.From(d.Data);
        if (plan.Walls.Count == 0) throw new InvalidOperationException("Der Grundriss hat noch keine Wände – nichts zu exportieren.");
        var json = Path.ChangeExtension(file, ".plan.json");
        File.WriteAllText(json, IfcExchange.ToJson(plan, d.Title));
        try
        {
            var r = IfcRun("export", json, file);
            if (!r.Ok || !File.Exists(file)) throw new InvalidOperationException("IFC-Export hat nicht geklappt: " + Short(r.Err.Length > 0 ? r.Err : r.Out));
            var infoFile = Path.ChangeExtension(file, ".info.json");
            var i = IfcRun("info", file, infoFile);
            ArchIfcLastInfo = i.Ok && File.Exists(infoFile) ? IfcExchange.ParseInfo(File.ReadAllText(infoFile)).Text : null;
            TryDelete(infoFile);
            _core.Audit.Append(Principal.LocalOwner.Name, "arch.ifc.export", ArchIfcLastInfo ?? Path.GetFileName(file));
            return file;
        }
        finally { TryDelete(json); }
    }

    async Task ArchIfcSetup()
    {
        if (KiStore.RuntimeReady(KiRuntime.Ifc)) { _progMsg[AK] = "✓ IFC-Austausch ist schon eingerichtet."; Show(AK); return; }
        try { await KiPyInstall(IfcModel, KiPyApps.For(KiRuntime.Ifc)!); _progMsg[AK] = "✓ IFC-Austausch ist eingerichtet – jetzt unter Exportieren „IFC (BIM)“."; }
        catch (Exception e) when (e is InvalidOperationException or IOException or HttpRequestException or OperationCanceledException) { _progMsg[AK] = "Einrichten ging nicht: " + Trendo.Core.FriendlyError.Say(e); }
        Show(AK);
    }
}
