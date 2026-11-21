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

/// <summary>T-0747-fix4 (vierte Prüfung, M2 + N4): Während einer Bahnberechnung gesammelte Änderungen gehören zu ihrem Dokument – nicht zu dem,
/// das gerade vorn ist; ist es geschlossen, verfallen sie. Eine zweite Datei während eines Einlesens wird danach eingelesen.</summary>
public class T0747_Fix4RaceUi
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
    static CncJob JobOf(Trendo.Core.Programs.ProgramDocument d) => CncJob.From(d.Data);

    /// <summary>Dokumente A und B mit eingelesener Zeichnung; A ist vorn. A rechnet (angehalten), danach wird in A die Fräsart geändert (gemerkt).</summary>
    static (MainWindow W, Trendo.Core.Programs.ProgramDocument A, Trendo.Core.Programs.ProgramDocument B, ManualResetEventSlim Hold) Setup()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        var svg = Path.Combine(root, "teil.svg"); File.Copy(Fixture("teil-fremd.svg"), svg);
        w.Show(CK); Settle(); RealClick(w, Get<Button>(w, "prog-tpl-leer")); Settle();
        w.PickOverride = _ => Task.FromResult<string[]?>([svg]); Click(w, "prog-cmd-cnc-g-import"); Wait(w);
        var a = w.ProgramActive(CK)!;
        var b = w.ProgramNew(CK, "leer"); w.Show(CK); Settle();
        Click(w, "prog-cmd-cnc-g-import"); Wait(w); Assert.Equal(1, JobOf(b).Contours.Count > 0 ? 1 : 0); // B hat auch eine Zeichnung – sonst fiele eine falsch zugeordnete Änderung nicht auf
        Click(w, "prog-tab-0"); Assert.Same(a, w.ProgramActive(CK));
        var hold = new ManualResetEventSlim(false); w.DrawingImportHold = hold;
        Type(w, "cnc-p-tool", "3"); Assert.True(w.DrawingImportBusy);
        Get<ComboBox>(w, "cnc-p-cammode").SelectedIndex = 3; Settle(); // „auf der Linie“ – wird gemerkt
        return (w, a, b, hold);
    }

    [AvaloniaFact(DisplayName = "T-0747-fix4 P2b: Fräsart in A gemerkt, dann Reiter B gewählt – die Änderung landet in A, B bleibt unverändert")]
    public void P2b_switch_tab()
    {
        var (w, a, b, hold) = Setup();
        var bBefore = b.Data.ToJsonString();
        Click(w, "prog-tab-1"); Assert.Same(b, w.ProgramActive(CK));
        hold.Set(); Wait(w); w.DrawingImportHold = null; Wait(w);
        Assert.Equal(bBefore, b.Data.ToJsonString());
        var ja = JobOf(a); Assert.Equal(3, ja.ToolD); Assert.Equal("auf", ja.CamMode); Assert.True(ja.IsCamGcode); Assert.False(ja.CamStale);
        hold.Dispose(); w.Close();
    }

    [AvaloniaFact(DisplayName = "T-0747-fix4 P2a: Fräsart in A gemerkt, dann A geschlossen – B bekommt nichts, keine Ausnahme")]
    public void P2a_close_doc()
    {
        var (w, a, b, hold) = Setup();
        var bBefore = b.Data.ToJsonString();
        w.ProgramClose(CK, 0); w.Show(CK); Settle(); Assert.Same(b, w.ProgramActive(CK));
        hold.Set(); Wait(w); w.DrawingImportHold = null; Wait(w);
        Assert.Equal(bBefore, b.Data.ToJsonString());
        Assert.DoesNotContain(w.ProgramDocs(CK), d => d == a);
        hold.Dispose(); w.Close();
    }

    [AvaloniaFact(DisplayName = "T-0747-fix4 N4: zweite Datei während eines laufenden Einlesens wird gemerkt und danach eingelesen")]
    public void Second_file_is_queued()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        var a = Path.Combine(root, "a.svg"); File.Copy(Fixture("teil-fremd.svg"), a);
        var b = Path.Combine(root, "b.dxf"); File.Copy(Fixture("teil-fremd.dxf"), b);
        w.Show(CK); Settle(); RealClick(w, Get<Button>(w, "prog-tpl-leer")); Settle();
        using var hold = new ManualResetEventSlim(false); w.DrawingImportHold = hold;
        w.PickOverride = _ => Task.FromResult<string[]?>([a]); Click(w, "prog-cmd-cnc-g-import"); Assert.True(w.DrawingImportBusy);
        w.PickOverride = _ => Task.FromResult<string[]?>([b]); Click(w, "prog-cmd-cnc-g-import");
        Assert.Contains("sobald die laufende Arbeit fertig ist", w.ProgramMessage(CK));
        hold.Set(); Wait(w); w.DrawingImportHold = null; Wait(w);
        var j = CncJob.From(w.ProgramActive(CK)!.Data);
        Assert.Equal("b.dxf", j.CamSource); Assert.True(j.IsCamGcode); Assert.False(j.CamStale);
        w.Close();
    }
}
