using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Trendo.Core;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0266: Programm „CNC-Werkstatt“ über den echten Eingabeweg – G-Code tippen, Simulation, Hinweis springt zur Zeile, Erzeugen, Laden, Nullpunkt, Export, Rechner.</summary>
public class SPEC14_Ui
{
    const string CK = "cnc";
    static T Get<T>(MainWindow w, string name) where T : Control { Settle(); return w.GetVisualDescendants().OfType<T>().Last(c => c.Name == name && c.IsEffectivelyVisible && c.Bounds.Width > 0); }
    static T? Try<T>(MainWindow w, string name) where T : Control { Settle(); return w.GetVisualDescendants().OfType<T>().LastOrDefault(c => c.Name == name && c.IsEffectivelyVisible && c.Bounds.Width > 0); }
    static void Press(MainWindow w, string name) { var b = Get<Button>(w, name); b.BringIntoView(); Settle(); RealClick(w, Get<Button>(w, name)); }
    static void CloseToasts(MainWindow w) { while (Try<Button>(w, "toast-close") is { } c) RealClick(w, c); }
    static void Key1(MainWindow w, Key k, PhysicalKey ph) { w.KeyPress(k, RawInputModifiers.None, ph, null); w.KeyRelease(k, RawInputModifiers.None, ph, null); Settle(); }
    static void Retype(MainWindow w, string name, string text) { var tb = Get<TextBox>(w, name); tb.BringIntoView(); RealClick(w, tb); tb.Focus(); tb.SelectAll(); Settle(); Key1(w, Key.Back, PhysicalKey.Backspace); foreach (var ch in text) { w.KeyTextInput(ch.ToString()); Settle(); } Key1(w, Key.Enter, PhysicalKey.Enter); }
    static CncJob Job(MainWindow w) => CncJob.From(w.ProgramActive(CK)!.Data);

    [AvaloniaFact(DisplayName = "SPEC-14 UI: G-Code eintippen → Prüfung findet Fehler, Klick auf den Hinweis markiert die Zeile; Simulation über Regler und Abspielen")]
    public void Type_check_simulate()
    {
        var (w, _, _) = Ui.Open();
        Assert.Equal(CK, MainWindow.StudioPages["SPEC-14"]);
        w.Show(CK); Settle(); RealClick(w, Get<Button>(w, "prog-tpl-leer"));
        Assert.Null(Try<Button>(w, "cnc-check-run")); // kein Formular mehr
        Assert.NotNull(Try<TextBlock>(w, "cnc-ok"));
        // ans Ende eine gefährliche Zeile anhängen: Eilgang schräg ins Material
        var ed = Get<TextBox>(w, "cnc-editor"); RealClick(w, ed); ed.Focus(); ed.CaretIndex = ed.Text!.Length; Settle();
        foreach (var ch in "G0 X60 Y30 Z-2") { w.KeyTextInput(ch.ToString()); Settle(); }
        Key1(w, Key.Enter, PhysicalKey.Enter);
        RealClick(w, Get<CncPathView>(w, "cnc-view")); // Klick in die Vorschau übernimmt
        Assert.EndsWith("G0 X60 Y30 Z-2\n", Job(w).Gcode.Replace("\r", ""));
        var warn = Get<Button>(w, "cnc-warn-0"); Assert.Contains("Kollisionsgefahr", warn.Content?.ToString());
        var line = Job(w).Gcode.Replace("\r", "").Split('\n').ToList().FindIndex(l => l.StartsWith("G0 X60")) + 1;
        Press(w, "cnc-warn-0");
        Assert.Equal(line, w.CncSelectedLine);
        Assert.Contains($"Zeile {line}", Get<TextBlock>(w, "cnc-now").Text);
        // Regler an den Anfang → Fräser bei der ersten Bewegung
        var sl = Get<Slider>(w, "cnc-sim"); sl.Value = 0; Settle();
        Assert.Equal(0, Get<CncPathView>(w, "cnc-view").Pos); Assert.StartsWith("Zeile 3", Get<TextBlock>(w, "cnc-now").Text);
        Press(w, "cnc-sim-play"); Assert.True(w.CncPlaying);
        Press(w, "cnc-sim-play"); Assert.False(w.CncPlaying);
        Press(w, "prog-cmd-cnc-end"); Assert.Equal(-1, w.CncTime);
        Press(w, "prog-cmd-cnc-v-iso"); Assert.True(Get<CncPathView>(w, "cnc-view").Iso);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "SPEC-14 UI: Maße eintippen und Kontur erzeugen, Datei laden, Nullpunkt verschieben, Export .nc/SVG/Bericht, Rechner rechnen sofort")]
    public void Generate_load_export()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        w.Show(CK); Settle(); RealClick(w, Get<Button>(w, "prog-tpl-tasche"));
        Retype(w, "cnc-p-w", "80"); Retype(w, "cnc-p-h", "50");
        Press(w, "prog-cmd-cnc-g-outline");
        var r = Cnc.Analyse(Job(w).Gcode); Assert.InRange(r.Box.X1 - r.Box.X0, 85, 87); // 80 + Fräser 6
        Press(w, "prog-undo"); Assert.InRange(Cnc.Analyse(Job(w).Gcode).Box.X1, 30, 41); // wieder die Tasche
        var f = Path.Combine(root, "teil.nc"); File.WriteAllText(f, "G21 G90\nG0 Z5\nG0 X10 Y10\nG1 Z-1 F100\nG1 X20 F300\nG0 Z5\n");
        Get<TextBox>(w, "cnc-file-path").Text = f; Press(w, "cnc-load");
        Assert.Contains("G1 X20 F300", Job(w).Gcode);
        Get<TextBox>(w, "cnc-offset").Text = "5 0 0"; Press(w, "cnc-offset-run");
        Assert.Equal(25, Cnc.Analyse(Job(w).Gcode).Box.X1, 6);
        foreach (var x in new[] { "nc", "svg", "report" })
        {
            Press(w, "prog-export-" + x);
            var file = w.ProgramMessage(CK)["Exportiert: ".Length..].Trim();
            Assert.True(File.Exists(file) && new FileInfo(file).Length > 30, x);
            if (x == "svg") System.Xml.Linq.XDocument.Load(file);
        }
        CloseToasts(w);
        Assert.StartsWith("Drehzahl", Get<SelectableTextBlock>(w, "cnc-feeds-out").Text);
        var mat = Get<ComboBox>(w, "cnc-feeds-mat"); mat.SelectedItem = "Holz"; Settle();
        Assert.Contains("U/min", Get<SelectableTextBlock>(w, "cnc-feeds-out").Text);
        Assert.Contains("€ je Teil", Get<SelectableTextBlock>(w, "cnc-cost-out").Text);
        Retype(w, "cnc-tap-t", "M10x1"); Assert.Contains("Ø 9", Get<SelectableTextBlock>(w, "cnc-tap-out").Text);
        w.Close();
    }
}
