using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Trendo.Core.Elektro;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0259: Programm „Schaltung & Simulation“ über den echten Eingabeweg – Bauteile ziehen, Schalter umlegen, Simulation, Prüfung, Export, Rechner.</summary>
public class SPEC02_Ui
{
    const string EK = "elektronik";
    static Circuit Model(MainWindow w) => Circuit.From(w.ProgramActive(EK)!.Data);
    static T Get<T>(MainWindow w, string name) where T : Control { Settle(); return w.GetVisualDescendants().OfType<T>().Last(c => c.Name == name && c.IsEffectivelyVisible && c.Bounds.Width > 0); }
    static void Press(MainWindow w, string name) { var b = Get<Button>(w, name); b.BringIntoView(); Settle(); RealClick(w, Get<Button>(w, name)); }
    static Point At(MainWindow w, int gx, int gy)
    {
        Settle(); w.UpdateLayout(); Settle(); var s = w.CircuitState; var c = Get<CircuitCanvas>(w, "ek-canvas");
        return c.TranslatePoint(new Point((gx * CircuitDrawing.Grid - s.PanX) * s.Zoom, (gy * CircuitDrawing.Grid - s.PanY) * s.Zoom), w) ?? throw new InvalidOperationException("Zeichenfläche nicht sichtbar");
    }
    static void Drag(MainWindow w, Point a, Point b) { w.MouseMove(a); w.MouseDown(a, MouseButton.Left); Settle(); w.MouseMove(new Point((a.X + b.X) / 2, (a.Y + b.Y) / 2)); w.MouseMove(b); Settle(); w.MouseUp(b, MouseButton.Left); Settle(); }
    static void Click(MainWindow w, Point p) { w.MouseMove(p); w.MouseDown(p, MouseButton.Left); w.MouseUp(p, MouseButton.Left); Settle(); }

    [AvaloniaFact(DisplayName = "SPEC-02 UI: Schaltung mit der Maus aufbauen (Batterie, Widerstand, LED, Drähte, Masse) – Simulation zeigt 20 mA, Prüfung ok")]
    public void Build_with_mouse()
    {
        var (w, _, _) = Ui.Open();
        w.Show(MainWindow.StudioPages["SPEC-02"]); Settle();
        RealClick(w, Get<Button>(w, "prog-tpl-leer"));
        Assert.Null(w.TryFind<Button>("el-code-run")); // kein Formular mehr
        w.CircuitState.UserView = true; w.CircuitState.Running = false;
        Press(w, "ek-tool-spannung"); Drag(w, At(w, 0, 6), At(w, 0, 0));
        Press(w, "ek-tool-widerstand"); Click(w, At(w, 0, 0)); // Klick: 4 Raster lang → (0,0)…(4,0)
        Press(w, "ek-tool-led"); Drag(w, At(w, 4, 0), At(w, 4, 6));
        Press(w, "ek-tool-draht"); Drag(w, At(w, 4, 6), At(w, 0, 6));
        Press(w, "ek-tool-masse"); Click(w, At(w, 0, 6));
        var c = Model(w);
        Assert.Equal(["spannung", "widerstand", "led", "draht", "masse"], c.Parts.Select(p => p.Kind));
        var r = c.Parts.First(p => p.Kind == "widerstand"); Assert.Equal((0, 0, 4, 0), (r.X1, r.Y1, r.X2, r.Y2));
        // Wert ändern: 9 V an 1 kΩ → zu wenig Strom; 330 Ω (Eingabe „330“) → ~21 mA
        Press(w, "ek-tool-auswahl"); Click(w, At(w, 10, 10)); // ins Leere: Auswahl aufheben → Übersicht
        Assert.Null(w.CircuitState.Selected);
        Press(w, "ek-part-1");
        var t = Get<TextBox>(w, "ek-p-value"); t.Focus(); t.Text = "330"; w.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null); w.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null); Settle();
        Assert.Equal(330, Model(w).Parts.First(p => p.Kind == "widerstand").Value, 3);
        w.CircuitAdvance(0.01);
        var led = Model(w).Parts.First(p => p.Kind == "led"); var st = w.CircuitSim()!.Last!;
        Assert.InRange(st.Parts[led.Id].I, 0.019, 0.023);
        Assert.Empty(Simulator.Check(Model(w), st));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "SPEC-02 UI: RC-Vorlage – Schalter per Klick umlegen, Kondensator lädt, Oszilloskop sammelt Werte, Export SPICE/Stückliste/SVG")]
    public void Switch_scope_export()
    {
        var (w, _, _) = Ui.Open();
        w.Show(MainWindow.StudioPages["SPEC-02"]); Settle();
        RealClick(w, Get<Button>(w, "prog-tpl-rc"));
        w.CircuitState.UserView = true; w.CircuitState.Running = false;
        var sw = Model(w).Parts.First(p => p.Kind == "schalter");
        Click(w, At(w, 2, 0)); Assert.Equal(sw.Id, w.CircuitState.Selected); // erster Klick wählt
        Click(w, At(w, 2, 0)); Assert.False(Model(w).Parts.First(p => p.Kind == "schalter").Closed); // zweiter Klick schaltet
        Press(w, "ek-toggle"); Assert.True(Model(w).Parts.First(p => p.Kind == "schalter").Closed);
        var cap = Model(w).Parts.First(p => p.Kind == "kondensator");
        w.CircuitSelect(cap.Id);
        for (var i = 0; i < 10; i++) w.CircuitAdvance(0.05);
        Assert.InRange(Math.Abs(w.CircuitSim()!.Last!.Parts[cap.Id].V), 4.9, 5.0);
        Assert.True(w.CircuitState.Scope.Count > 50);
        foreach (var x in new[] { "spice", "bom", "svg" })
        {
            Press(w, "prog-export-" + x);
            var file = w.ProgramMessage(EK)["Exportiert: ".Length..].Trim();
            Assert.True(File.Exists(file) && new FileInfo(file).Length > 50, x);
        }
        Assert.Contains("Kondensator;100 µF;1;C1", File.ReadAllText(Path.ChangeExtension(w.ProgramMessage(EK)["Exportiert: ".Length..].Trim(), ".csv")));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "SPEC-02 UI: LED ohne Vorwiderstand meldet „brennt durch“; Rechner rechnen sofort (Farbcode, LED, Teiler, SMD, 555, Akku, Kabel, Regler, Präfixe)")]
    public void Check_and_calculators()
    {
        var (w, _, _) = Ui.Open();
        w.Show(MainWindow.StudioPages["SPEC-02"]); Settle();
        RealClick(w, Get<Button>(w, "prog-tpl-led"));
        w.CircuitState.Running = false;
        w.CircuitEdit("Test", c => c.Parts.First(p => p.Kind == "widerstand").Value = 10);
        w.CircuitAdvance(0.01); w.CircuitSelect(null);
        Assert.Contains("brennt durch", Get<TextBlock>(w, "ek-check").Text);
        Get<Expander>(w, "ek-calcs").IsExpanded = true; Settle();
        string Out(string id) => Get<SelectableTextBlock>(w, id + "-out").Text ?? "";
        Assert.Contains("4,70 kΩ", Out("el-code"));
        Assert.Contains("150", Out("el-led"));
        Assert.Contains("R1 =", Out("el-div"));
        Assert.Contains("400 Ω", Out("el-comb"));
        Assert.Contains("100 nF", Out("el-cap"));
        Assert.Contains("4,70 kΩ", Out("el-smd"));
        Assert.Contains("Hz", Out("el-555"));
        Assert.Contains("mm breit", Out("el-trace"));
        Assert.Contains("34", Out("el-bat"));
        Assert.StartsWith("⚠", Out("el-cable"));
        Assert.Contains("Kühlkörper", Out("el-reg"));
        Assert.Contains("4.700", Out("el-si"));
        Get<TextBox>(w, "el-code-bands").Text = "1000"; Settle();
        Assert.Contains("1,00 kΩ", Out("el-code"));
        w.Close();
    }
}
