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

/// <summary>T-0272: Transistor, OPV und Frequenzgang über die Oberfläche.</summary>
public class SPEC02_UiPlus
{
    const string EK = "elektronik";
    static Trendo.Core.Elektro.Circuit Model(MainWindow w) => Trendo.Core.Elektro.Circuit.From(w.ProgramActive(EK)!.Data);
    static T Get<T>(MainWindow w, string name) where T : Control { Settle(); return w.GetVisualDescendants().OfType<T>().Last(c => c.Name == name && c.IsEffectivelyVisible && c.Bounds.Width > 0); }
    static void Press(MainWindow w, string name) { var b = Get<Button>(w, name); b.BringIntoView(); Settle(); RealClick(w, Get<Button>(w, name)); }

    [AvaloniaFact(DisplayName = "SPEC-02 UI: Vorlage Transistor schaltet die LED ein; OPV-Vorlage – Bode-Diagramm am Ausgang zeigt Verstärkung 20 dB und die Grenzfrequenz des Tiefpasses")]
    public void Transistor_opamp_bode()
    {
        var (w, _, _) = Ui.Open();
        w.Show(EK); Settle(); { var tpl = Get<Button>(w, "prog-tpl-transistor"); tpl.BringIntoView(); Settle(); RealClick(w, tpl); };
        Assert.NotNull(Get<Button>(w, "ek-tool-npn"));
        w.CircuitAdvance(0.01);
        var c = Model(w); var led = c.Parts.Single(p => p.Kind == "led");
        Assert.InRange(w.CircuitSim()!.Last!.Parts[led.Id].I, 0.010, 0.020); // ≈ (9 − 2 − 0,1) V / 470 Ω
        w.ProgramNew(EK, "verstaerker"); w.Show(EK); Settle(); // zweites Dokument aus der Vorlage
        var op = Model(w).Parts.Single(p => p.Kind == "opv");
        w.CircuitSelect(op.Id);
        Assert.Equal("12", Get<TextBox>(w, "ek-p-value").Text);
        Press(w, "ek-bode");
        var fc = Get<TextBlock>(w, "ek-bode-fc").Text!;
        Assert.Contains("Grenzfrequenz 1,", fc); Assert.Contains("kHz", fc); // 1/(2π·1 kΩ·100 nF) ≈ 1,59 kHz
        Assert.Contains("Verstärkung bei 10 Hz 20", fc);
        Assert.NotNull(Get<Border>(w, "ek-bode"));
        w.Close();
    }
}

/// <summary>T-0277: Platinen-Layout über die Oberfläche – umschalten, verlegen, Bauteil ziehen und drehen, Fertigungsdaten.</summary>
public class SPEC02_UiPcb
{
    const string EK = "elektronik";
    static Trendo.Core.Elektro.Circuit Model(MainWindow w) => Trendo.Core.Elektro.Circuit.From(w.ProgramActive(EK)!.Data);
    static T Get<T>(MainWindow w, string name) where T : Control { Settle(); return w.GetVisualDescendants().OfType<T>().Last(c => c.Name == name && c.IsEffectivelyVisible && c.Bounds.Width > 0); }
    static void Press(MainWindow w, string name) { var b = Get<Button>(w, name); b.BringIntoView(); Settle(); RealClick(w, Get<Button>(w, name)); }

    [AvaloniaFact(DisplayName = "SPEC-02 UI: Platine – Umschalten platziert automatisch, Leiterbahnen verlegen ohne Konflikt, Bauteil mit der Maus ziehen (Rückgängig), R dreht, Gerber-ZIP")]
    public void Pcb_flow()
    {
        var (w, _, _) = Ui.Open();
        w.Show(EK); Settle(); { var tpl = Get<Button>(w, "prog-tpl-transistor"); tpl.BringIntoView(); Settle(); RealClick(w, tpl); };
        Press(w, "prog-cmd-ek-pcb");
        Assert.True(w.CircuitState.Pcb); Assert.Equal(6, Model(w).Pcb.Places.Count); // Batterie, 2 Widerstände, LED, Transistor, Schalter
        Assert.Contains("noch nicht verlegt", Get<TextBlock>(w, "ek-pcb-info").Text);
        Press(w, "prog-cmd-ek-route");
        Assert.Contains("Prüfung: keine Konflikte", Get<TextBlock>(w, "ek-pcb-info").Text);
        var led = Model(w).Parts.Single(p => p.Kind == "led"); var pl = Model(w).Pcb.Places[led.Id];
        var cv = Get<PcbCanvas>(w, "ek-pcb-canvas"); var from = cv.TranslatePoint(cv.ToScreen(pl.X, pl.Y), w)!.Value; var dx = 10 * cv.Scale;
        w.MouseDown(from, MouseButton.Left); w.MouseMove(from + new Point(dx / 2, 0)); w.MouseMove(from + new Point(dx, 0)); w.MouseUp(from + new Point(dx, 0), MouseButton.Left); Settle();
        Assert.InRange(Model(w).Pcb.Places[led.Id].X - pl.X, 8.8, 11.2); // ≈ 10 mm im Raster 1,27
        w.KeyPress(Key.R, RawInputModifiers.None, PhysicalKey.R, "r"); w.KeyRelease(Key.R, RawInputModifiers.None, PhysicalKey.R, "r"); Settle();
        Assert.Equal(90, Model(w).Pcb.Places[led.Id].Rot);
        Press(w, "prog-undo"); Press(w, "prog-undo"); Assert.Equal(pl.X, Model(w).Pcb.Places[led.Id].X, 6);
        Press(w, "prog-export-fab");
        var zip = w.ProgramMessage(EK)["Exportiert: ".Length..].Trim();
        using (var z = System.IO.Compression.ZipFile.OpenRead(zip)) Assert.Equal(5, z.Entries.Count);
        Press(w, "prog-cmd-ek-pcb"); Assert.False(w.CircuitState.Pcb); Assert.NotNull(Get<CircuitCanvas>(w, "ek-canvas"));
        w.Close();
    }
}
