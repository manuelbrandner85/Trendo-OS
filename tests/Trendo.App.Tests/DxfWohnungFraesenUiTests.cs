using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Trendo.Core;
using Trendo.Core.Arch;
using Trendo.Core.Cad;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0747: Über die Oberfläche – DXF in den Grundriss einlesen, bearbeiten, zurücknehmen, als DXF ausgeben (SPEC-01);
/// DXF/SVG in der CNC-Werkstatt zu Fräsbahnen, Haltestege eintippen, zurücknehmen, als G-Code ausgeben (SPEC-14).</summary>
public class T0747_DxfUi
{
    const string AK = "architektur", CK = "cnc";
    static T Get<T>(MainWindow w, string name) where T : Control { Settle(); return w.GetVisualDescendants().OfType<T>().Last(c => c.Name == name && c.IsEffectivelyVisible && c.Bounds.Width > 0); }
    static void Press(MainWindow w, string name) { var b = Get<Button>(w, name); b.BringIntoView(); Settle(); RealClick(w, Get<Button>(w, name)); }
    static void Type(MainWindow w, string box, string text)
    {
        var t = Get<TextBox>(w, box); t.BringIntoView(); t.Focus(); t.Text = text; Settle();
        w.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null); w.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null); Settle();
    }
    static string Fixture(string name)
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "Trendo.sln"))) d = d.Parent;
        return Path.Combine(d!.FullName, "tests", "fixtures", "t0747", name);
    }
    static FloorPlan Plan(MainWindow w) => FloorPlan.From(w.ProgramActive(AK)!.Data);
    static CncJob Job(MainWindow w) => CncJob.From(w.ProgramActive(CK)!.Data);

    [AvaloniaFact(DisplayName = "T-0747 UI: Grundriss – „DXF einlesen …“ → Wände, Tür, Räume; Rückgängig; Wand ändern; Export DXF liest sich wieder")]
    public void Plan_import_edit_export()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        var f = Path.Combine(root, "fremd.dxf"); File.Copy(Fixture("wohnung-fremd.dxf"), f);
        w.Show(AK); Settle(); RealClick(w, Get<Button>(w, "prog-tpl-leer"));
        Assert.Empty(Plan(w).Walls);
        w.PickOverride = _ => Task.FromResult<string[]?>([f]);
        Press(w, "prog-cmd-arch-dxf-import");
        var p = Plan(w);
        Assert.True(p.Walls.Count >= 7, w.ProgramMessage(AK));
        Assert.Single(p.Openings); Assert.Equal(2, p.Rooms.Count);
        Assert.Contains("DXF „fremd.dxf“ eingelesen", w.ProgramMessage(AK)); Assert.Contains("Strg+Z", w.ProgramMessage(AK));
        // ein Schritt zurück: Plan wieder leer, und wieder her
        Press(w, "prog-undo"); Assert.Empty(Plan(w).Walls);
        Press(w, "prog-redo"); Assert.Equal(p.Walls.Count, Plan(w).Walls.Count);
        // Innenwand anwählen und Stärke in den Eigenschaften ändern
        var inner = Plan(w).Walls.Single(x => Math.Abs(x.Thickness - 11.5) < 0.01);
        w.ArchSelect(inner.Id); Settle();
        Type(w, "arch-p-thickness", "17,5");
        Assert.Equal(17.5, Plan(w).WallOf(inner.Id)!.Thickness);
        // Export DXF → Datei; eingelesen mit dem gemeinsamen Leser ergibt dieselben Wände (17,5 cm Innenwand mit Tür)
        Press(w, "prog-export-dxf");
        var file = w.ProgramMessage(AK)["Exportiert: ".Length..].Trim();
        Assert.True(File.Exists(file), file);
        var back = new FloorPlan(); var rep = PlanDxfImport.Import(back, CadDxf.Read(file));
        Assert.Equal("cm", rep.Units);
        Assert.Contains(back.Walls, x => Math.Abs(x.Thickness - 17.5) < 0.01);
        Assert.Single(back.Openings); Assert.Equal(["Schlafen", "Wohnen"], back.Rooms.Select(r => r.Name).Order());
        w.Close();
    }

    [AvaloniaFact(DisplayName = "T-0747 UI: CNC – „Aus DXF/SVG …“ → Bahnen mit Versatz; Haltestege eintippen; Rückgängig; Rechtsklick-Menü; Export .nc")]
    public void Cnc_import_tabs_export()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        var svg = Path.Combine(root, "teil.svg"); File.Copy(Fixture("teil-fremd.svg"), svg);
        w.Show(CK); Settle(); RealClick(w, Get<Button>(w, "prog-tpl-leer"));
        Type(w, "cnc-p-tool", "4"); Type(w, "cnc-p-depth", "6"); Type(w, "cnc-p-step", "2");
        w.PickOverride = _ => Task.FromResult<string[]?>([svg]);
        Press(w, "prog-cmd-cnc-g-import");
        var j = Job(w);
        Assert.Equal(3, j.Contours.Count); Assert.Equal("teil.svg", j.CamSource);
        Assert.StartsWith("(Konturen aus Zeichnung", j.Gcode);
        Assert.Contains("3 Fräsbahnen (2 innen, 1 außen)", w.ProgramMessage(CK));
        var view = Get<CncPathView>(w, "cnc-view"); Assert.Equal(3, view.Outlines.Count); Assert.True(view.Segs.Count > 100);
        Assert.Equal(-6, view.Segs.Min(s => s.Z1), 9);
        // Rechtsklick-Menü der Vorschau
        Assert.Contains(view.ContextMenu!.Items.OfType<MenuItem>(), m => m.Name == "cnc-ctx-import");
        Assert.True(view.ContextMenu.Items.OfType<MenuItem>().Single(m => m.Name == "cnc-ctx-cam").IsEnabled);
        // Haltestege eintippen → Bahnen neu, mit Stegen; zurücknehmen
        Assert.DoesNotContain("G1 Z-5\n", j.Gcode);
        Type(w, "cnc-p-tabs", "4");
        Assert.True(Job(w).CamTabs == 4, w.ProgramMessage(CK));
        Assert.Equal(4, System.Text.RegularExpressions.Regex.Matches(Job(w).Gcode, @"\nG1 Z-5\n").Count); // Steghöhe 1 mm über −6
        Press(w, "prog-undo"); // ein Schritt: Wert und Bahnen zusammen
        Assert.Equal(0, Job(w).CamTabs); Assert.DoesNotContain("G1 Z-5\n", Job(w).Gcode);
        // Fräsart: alles auf der Linie
        Get<ComboBox>(w, "cnc-p-cammode").SelectedIndex = 3; Settle();
        Assert.Equal("auf", Job(w).CamMode); Assert.Contains("auf der Linie", Job(w).Gcode);
        // Export als G-Code-Datei
        Press(w, "prog-export-nc");
        var file = w.ProgramMessage(CK)["Exportiert: ".Length..].Trim();
        Assert.True(File.Exists(file)); Assert.Equal(Job(w).Gcode.Replace("\r", ""), File.ReadAllText(file).Replace("\r", ""));
        // kaputte Datei: Meldung statt Absturz, Dokument bleibt
        var bad = Path.Combine(root, "kaputt.dxf"); File.WriteAllText(bad, "0\nSECTION\n2\nENTITIES\n0\nENDSEC\n0\nEOF\n");
        var before = Job(w).Gcode;
        w.PickOverride = _ => Task.FromResult<string[]?>([bad]); Press(w, "prog-cmd-cnc-g-import");
        Assert.Equal(before, Job(w).Gcode); Assert.NotEmpty(w.ProgramMessage(CK));
        w.Close();
    }
}
