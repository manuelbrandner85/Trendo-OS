using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Trendo.Core.Statik;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0258: Programm „Tragwerk & Mechanik“ über den echten Eingabeweg – Stäbe zeichnen, Lager/Lasten klicken, Nachweis, Vorschlag, Export, Rechner.</summary>
public class SPEC03_Ui
{
    const string TK = "technik";
    static FrameModel Model(MainWindow w) => FrameModel.From(w.ProgramActive(TK)!.Data);
    static T Get<T>(MainWindow w, string name) where T : Control { Settle(); return w.GetVisualDescendants().OfType<T>().Last(c => c.Name == name && c.IsEffectivelyVisible && c.Bounds.Width > 0); }
    static void Press(MainWindow w, string name) { var b = Get<Button>(w, name); b.BringIntoView(); Settle(); RealClick(w, Get<Button>(w, name)); }
    static FrameCanvas Canvas(MainWindow w) => Get<FrameCanvas>(w, "tk-canvas");
    static Point At(MainWindow w, double x, double y)
    {
        Settle(); w.UpdateLayout(); Settle(); var s = w.FrameState;
        return Canvas(w).TranslatePoint(new Point((x * 100 - s.PanX) * s.Zoom, (-y * 100 - s.PanY) * s.Zoom), w) ?? throw new InvalidOperationException("Zeichenfläche nicht sichtbar");
    }
    static void Click(MainWindow w, Point p) { w.MouseMove(p); w.MouseDown(p, MouseButton.Left); w.MouseUp(p, MouseButton.Left); Settle(); }
    static void Key1(MainWindow w, Key k, PhysicalKey ph) { w.KeyPress(k, RawInputModifiers.None, ph, null); w.KeyRelease(k, RawInputModifiers.None, ph, null); Settle(); }
    static string Texts(MainWindow w) => string.Join(" ", w.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text));

    [AvaloniaFact(DisplayName = "SPEC-03 UI: Tragwerk – Stab mit der Maus zeichnen (Länge tippen), Lager und Streckenlast klicken, Nachweis, Vorschlag übernehmen")]
    public void Draw_support_load_check()
    {
        var (w, _, _) = Ui.Open();
        w.Show(MainWindow.StudioPages["SPEC-03"]); Settle();
        RealClick(w, Get<Button>(w, "prog-tpl-leer"));
        Assert.Null(w.TryFind<Button>("eng-beam-run")); // kein Formular mehr
        w.FrameState.UserView = true;
        Press(w, "tk-tool-stab");
        Click(w, At(w, 0, 0));
        Canvas(w).Focus(); Settle(); Key1(w, Key.D5, PhysicalKey.Digit5); w.MouseMove(At(w, 3, 0.05)); Settle(); Key1(w, Key.Enter, PhysicalKey.Enter);
        var m = Model(w); var bar = Assert.Single(m.Members);
        Assert.Equal(5, m.Node(bar.B)!.X, 3); Assert.Equal(0, m.Node(bar.B)!.Y, 3);
        Key1(w, Key.Escape, PhysicalKey.Escape);

        Press(w, "tk-tool-lager"); Press(w, "tk-opt-support-fest"); Click(w, At(w, 0, 0));
        Press(w, "tk-opt-support-los"); Click(w, At(w, 5, 0));
        Assert.Equal(["fest", "los"], Model(w).Nodes.Select(n => n.Support));
        Press(w, "tk-tool-last"); Press(w, "tk-opt-load-strecke");
        Click(w, At(w, 2.5, 0));
        var load = Assert.Single(Model(w).Loads); Assert.Equal("strecke", load.Kind); Assert.Equal(10, load.Value);

        // Nachweis: IPE 200 auf 5 m mit 10 kN/m biegt sich zu stark → Vorschlag übernimmt das kleinste passende IPE
        Press(w, "tk-tool-auswahl"); Click(w, At(w, 2.5, 2.5)); // ins Leere klicken: Auswahl aufheben → Übersicht
        Assert.Null(w.FrameState.Selected);
        Press(w, "tk-member-0");
        Assert.Contains("biegt sich zu stark", Get<TextBlock>(w, "tk-verdict").Text);
        Press(w, "tk-suggest");
        Assert.Equal("IPE 220", Model(w).Members[0].Section); // IPE 220: f = 14,0 mm ≤ L/300, σ = 124 N/mm²
        Assert.StartsWith("✓", Get<TextBlock>(w, "tk-verdict").Text);
        Assert.Contains("31,25", Texts(w)); // max. Moment qL²/8
        w.Close();
    }

    [AvaloniaFact(DisplayName = "SPEC-03 UI: bewegliches Tragwerk wird gemeldet; Rahmen-Vorlage, Ergebnisanzeigen, PDF/SVG/CSV-Export")]
    public void Mechanism_template_exports()
    {
        var (w, _, _) = Ui.Open();
        w.Show(MainWindow.StudioPages["SPEC-03"]); Settle();
        RealClick(w, Get<Button>(w, "prog-tpl-leer"));
        w.FrameEdit("Test", f => { var b = f.AddMember(0, 0, 4, 0); f.Node(b.A)!.Support = "los"; f.Node(b.B)!.Support = "los"; f.AddLineLoad(b.Id, 5); });
        Assert.Contains("beweglich", Get<Border>(w, "tk-error").Child is TextBlock t ? t.Text : "");
        RealClick(w, Get<Button>(w, "prog-tab-new"));
        w.ProgramClose(TK, 1); w.ProgramNew(TK, "rahmen"); w.Show(TK); Settle();
        Assert.Contains("alle Stäbe halten", Get<TextBlock>(w, "prog-status-text").Text);
        Press(w, "prog-cmd-tk-v"); Assert.True(w.FrameState.Show.Shear);
        Press(w, "prog-cmd-tk-def"); Assert.False(w.FrameState.Show.Deformed);
        foreach (var x in new[] { "pdf", "svg", "csv" })
        {
            Press(w, "prog-export-" + x);
            var file = w.ProgramMessage(TK)["Exportiert: ".Length..].Trim();
            Assert.True(File.Exists(file) && new FileInfo(file).Length > 200, x);
        }
        Assert.Contains("HEA 200", File.ReadAllText(w.ProgramMessage(TK)["Exportiert: ".Length..].Trim()));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "SPEC-03 UI: Rechner rechnen sofort bei der Eingabe (Wärmedehnung, Einheiten, Schraube, Zahnrad, Gewicht)")]
    public void Live_calculators()
    {
        var (w, _, _) = Ui.Open();
        w.Show(MainWindow.StudioPages["SPEC-03"]); Settle();
        RealClick(w, Get<Button>(w, "prog-tpl-einfeld"));
        Get<Expander>(w, "tk-calcs").IsExpanded = true; Settle();
        Assert.Contains("2,76 mm", Get<SelectableTextBlock>(w, "eng-heat-out").Text);
        Assert.Contains("6,8948 bar", Get<SelectableTextBlock>(w, "eng-unit-out").Text);
        Get<TextBox>(w, "eng-unit-v").Text = "200"; Settle();
        Assert.Contains("13,7895 bar", Get<SelectableTextBlock>(w, "eng-unit-out").Text);
        Get<TextBox>(w, "eng-unit-v").Text = "abc"; Settle();
        Assert.StartsWith("⚠", Get<SelectableTextBlock>(w, "eng-unit-out").Text);
        Assert.Contains("25,0 Nm", Get<SelectableTextBlock>(w, "eng-bolt-out").Text);
        Assert.Contains("500 U/min", Get<SelectableTextBlock>(w, "eng-gear-out").Text);
        Assert.Contains("2,70 kg", Get<SelectableTextBlock>(w, "eng-mass-out").Text);
        Assert.Contains("Federrate", Get<SelectableTextBlock>(w, "eng-spring-out").Text);
        Assert.Contains("U-Wert", Get<SelectableTextBlock>(w, "eng-u-out").Text);
        Assert.Contains("Verdrehung", Get<SelectableTextBlock>(w, "eng-torsion-out").Text);
        w.Close();
    }
}

/// <summary>T-0271: Trapezlast, Temperatur, Setzung und schräges Loslager über die Eigenschaften eintippen.</summary>
public class SPEC03_UiPlus
{
    const string TK = "technik";
    static FrameModel Model(MainWindow w) => FrameModel.From(w.ProgramActive(TK)!.Data);
    static T Get<T>(MainWindow w, string name) where T : Control { Settle(); return w.GetVisualDescendants().OfType<T>().Last(c => c.Name == name && c.IsEffectivelyVisible && c.Bounds.Width > 0); }
    static void Press(MainWindow w, string name) { var b = Get<Button>(w, name); b.BringIntoView(); Settle(); RealClick(w, Get<Button>(w, name)); }
    static void Key1(MainWindow w, Key k, PhysicalKey ph) { w.KeyPress(k, RawInputModifiers.None, ph, null); w.KeyRelease(k, RawInputModifiers.None, ph, null); Settle(); }
    static void Retype(MainWindow w, string name, string text) { var tb = Get<TextBox>(w, name); tb.BringIntoView(); RealClick(w, tb); tb.Focus(); tb.SelectAll(); Settle(); Key1(w, Key.Back, PhysicalKey.Backspace); foreach (var ch in text) { w.KeyTextInput(ch.ToString()); Settle(); } Key1(w, Key.Enter, PhysicalKey.Enter); }

    [AvaloniaFact(DisplayName = "SPEC-03 UI: Streckenlast zum Trapez machen, Temperatur am Stab, Setzung und Neigung am Lager – Ergebnis rechnet mit")]
    public void Trapez_temp_settle_angle()
    {
        var (w, _, _) = Ui.Open();
        w.Show(TK); Settle(); RealClick(w, Get<Button>(w, "prog-tpl-einfeld"));
        var m = Model(w); var load = m.Loads.Single(l => l.Kind == "strecke"); var mem = m.Members.Single();
        w.FrameSelect(load.Id);
        Retype(w, "tk-p-value2", "0");
        Assert.Equal(0, Model(w).Loads.Single(l => l.Kind == "strecke").Value2);
        var r = FrameSolver.Solve(Model(w)); Assert.Equal(10 * 5 / 3.0, r.Reactions[mem.A].Ry, 3); // Dreieck 10 → 0: links qL/3
        w.FrameSelect(mem.Id); Press(w, "tk-temp");
        var t = Model(w).Loads.Single(l => l.Kind == "temperatur"); Assert.Equal(30, t.Value);
        Retype(w, "tk-p-grad", "15"); Assert.Equal(15, Model(w).Loads.Single(l => l.Kind == "temperatur").M);
        var right = Model(w).Nodes.Single(n => n.Support == "los"); w.FrameSelect(right.Id);
        Retype(w, "tk-p-settle", "5"); Retype(w, "tk-p-angle", "20");
        var n2 = Model(w).Node(right.Id)!; Assert.Equal(5, n2.Settle); Assert.Equal(20, n2.Angle);
        Assert.NotEqual(0, FrameSolver.Solve(Model(w)).Reactions[mem.A].Rx, 3); // schräge Ebene erzeugt waagerechte Kraft
        Press(w, "prog-undo"); Assert.Equal(0, Model(w).Node(right.Id)!.Angle);
        w.Close();
    }
}

/// <summary>T-0275: Eurocode-Nachweis über die Oberfläche.</summary>
public class SPEC03_UiEc
{
    const string TK = "technik";
    static FrameModel Model(MainWindow w) => FrameModel.From(w.ProgramActive(TK)!.Data);
    static T Get<T>(MainWindow w, string name) where T : Control { Settle(); return w.GetVisualDescendants().OfType<T>().Last(c => c.Name == name && c.IsEffectivelyVisible && c.Bounds.Width > 0); }
    static void Press(MainWindow w, string name) { var b = Get<Button>(w, name); b.BringIntoView(); Settle(); RealClick(w, Get<Button>(w, name)); }

    [AvaloniaFact(DisplayName = "SPEC-03 UI: Eurocode einschalten – Ausnutzung am Stab, Last als ständig markieren senkt η, Nachweis als Bericht")]
    public void Eurocode_toggle()
    {
        var (w, _, _) = Ui.Open();
        w.Show(TK); Settle(); RealClick(w, Get<Button>(w, "prog-tpl-einfeld"));
        var m = Model(w); var mem = m.Members.Single(); var load = m.Loads.Single(l => l.Kind == "strecke");
        Press(w, "prog-cmd-tk-ec"); Assert.True(w.FrameState.Eurocode);
        w.FrameSelect(mem.Id);
        Assert.Contains("EC 3", Get<TextBlock>(w, "tk-ec-verdict").Text);
        var q = Eurocode.Check(Model(w)).Of(mem.Id)!.EtaStress;
        w.FrameSelect(load.Id); RealClick(w, Get<CheckBox>(w, "tk-p-permanent"));
        Assert.True(Model(w).Loads.Single(l => l.Kind == "strecke").Permanent);
        Assert.Equal(q * 1.35 / 1.5, Eurocode.Check(Model(w)).Of(mem.Id)!.EtaStress, 3);
        Press(w, "prog-export-ec");
        var file = w.ProgramMessage(TK)["Exportiert: ".Length..].Trim(); Assert.Contains("Nachweis nach Eurocode", File.ReadAllText(file));
        w.Close();
    }
}
