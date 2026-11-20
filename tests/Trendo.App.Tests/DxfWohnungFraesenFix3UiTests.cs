using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Trendo.Core;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0747-fix3 (dritte Prüfung, M4 + N3): Ein Ergebnis aus dem Hintergrund wird nur auf genau den Stand übernommen, auf dem es gerechnet wurde.
/// Rückgängig oder ein von Hand geänderter G-Code während der Rechnung → Ergebnis verworfen; eine zweite Wertänderung geht nicht verloren.</summary>
public class T0747_Fix3RaceUi
{
    const string CK = "cnc";
    static T Get<T>(MainWindow w, string name) where T : Control { Settle(); return w.GetVisualDescendants().OfType<T>().Last(c => c.Name == name && c.IsEffectivelyVisible && c.Bounds.Width > 0); }
    static void Wait(MainWindow w) { var sw = Stopwatch.StartNew(); while (w.DrawingImportBusy && sw.Elapsed < TimeSpan.FromSeconds(120)) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(10); } Assert.False(w.DrawingImportBusy); Settle(); }
    static void Click(MainWindow w, string name) { var b = Get<Button>(w, name); b.BringIntoView(); Settle(); RealClick(w, Get<Button>(w, name)); Settle(); }
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
    static CncJob Job(MainWindow w) => CncJob.From(w.ProgramActive(CK)!.Data);
    static (MainWindow W, string Root) Start()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        w.Show(CK); Settle(); RealClick(w, Get<Button>(w, "prog-tpl-leer")); Settle();
        return (w, root);
    }
    static void Import(MainWindow w, string file) { w.PickOverride = _ => Task.FromResult<string[]?>([file]); Click(w, "prog-cmd-cnc-g-import"); Wait(w); }

    [AvaloniaFact(DisplayName = "T-0747-fix3 R1/N3: Wert während der Rechnung noch einmal geändert – keine Ausnahme, der letzte Wert gilt und die Bahnen passen dazu")]
    public void R1_second_change_is_kept()
    {
        var (w, root) = Start();
        var svg = Path.Combine(root, "teil.svg"); File.Copy(Fixture("teil-fremd.svg"), svg); Import(w, svg);
        using var hold = new ManualResetEventSlim(false); w.DrawingImportHold = hold;
        Type(w, "cnc-p-tool", "3"); Assert.True(w.DrawingImportBusy);
        Type(w, "cnc-p-tool", "2"); // darf keine Ausnahme werfen
        Assert.Contains("sobald die laufende Rechnung fertig ist", w.ProgramMessage(CK));
        hold.Set(); Wait(w); w.DrawingImportHold = null; Wait(w);
        var j = Job(w);
        Assert.Equal(2, j.ToolD); Assert.True(j.IsCamGcode); Assert.False(j.CamStale);
        Assert.Equal(CncCam.Generate(j.Contours, j.CamSettings()).Gcode, j.Gcode);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "T-0747-fix3 R2: A einlesen, B einlesen, Ø ändern, Strg+Z während der Rechnung – G-Code passt danach zur Zeichnung im Dokument")]
    public void R2_undo_during_background()
    {
        var (w, root) = Start();
        var a = Path.Combine(root, "a.svg"); File.Copy(Fixture("teil-fremd.svg"), a);
        var b = Path.Combine(root, "b.dxf"); File.Copy(Fixture("teil-fremd.dxf"), b);
        Import(w, a); Import(w, b); Assert.Equal("b.dxf", Job(w).CamSource);
        using var hold = new ManualResetEventSlim(false); w.DrawingImportHold = hold;
        Type(w, "cnc-p-tool", "3"); Assert.True(w.DrawingImportBusy);
        Click(w, "prog-undo"); // zurück zu A, während B mit Ø 3 rechnet
        hold.Set(); Wait(w); w.DrawingImportHold = null;
        var j = Job(w);
        Assert.Equal("a.svg", j.CamSource);
        Assert.True(!j.IsCamGcode || !j.CamStale);
        Assert.Equal(CncCam.Generate(j.Contours, j.CamSettings()).Gcode, j.Gcode); // G-Code gehört zu den Konturen im Dokument
        Assert.Contains("verworfen", w.ProgramMessage(CK));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "T-0747-fix3 R3: G-Code von Hand geändert, während „Bahnen neu“ rechnet – die Handänderung bleibt")]
    public void R3_hand_edit_during_background()
    {
        var (w, root) = Start();
        var a = Path.Combine(root, "a.svg"); File.Copy(Fixture("teil-fremd.svg"), a); Import(w, a);
        using var hold = new ManualResetEventSlim(false); w.DrawingImportHold = hold;
        Click(w, "prog-cmd-cnc-g-cam"); Assert.True(w.DrawingImportBusy);
        w.ProgramActive(CK)!.Edit("Hand", x => { var jj = CncJob.From(x); jj.Gcode = "G21\nG0 Z5\n(von Hand)\nM30\n"; jj.WriteTo(x); }); Settle();
        hold.Set(); Wait(w); w.DrawingImportHold = null;
        Assert.Contains("(von Hand)", Job(w).Gcode);
        Assert.Contains("verworfen", w.ProgramMessage(CK));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "T-0747-fix3 N4: Trendo CAD meldet eine DXF ohne Einheit („als mm gelesen“)")]
    public void Cad_missing_units()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        var f = Path.Combine(root, "ohne.dxf");
        File.WriteAllText(f, "0\nSECTION\n2\nENTITIES\n0\nLINE\n8\n0\n10\n0\n20\n0\n11\n2\n21\n0\n0\nENDSEC\n0\nEOF\n");
        Assert.Contains("Einheit nicht angegeben – als mm gelesen", w.CadImport(f));
        w.Close();
    }
}
