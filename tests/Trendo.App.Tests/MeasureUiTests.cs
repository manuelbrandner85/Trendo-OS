using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Trendo.Core.Messdaten;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0261: Programm „Messdaten & Auswertung“ über den echten Eingabeweg – Werte eintippen, Spalte wählen, Diagramm, Vergleich, Export, Rechner.</summary>
public class SPEC05_Ui
{
    const string SK = "wissenschaft";
    static MeasureSheet Model(MainWindow w) => MeasureSheet.From(w.ProgramActive(SK)!.Data);
    static T Get<T>(MainWindow w, string name) where T : Control { Settle(); return w.GetVisualDescendants().OfType<T>().Last(c => c.Name == name && c.IsEffectivelyVisible && c.Bounds.Width > 0); }
    static void Press(MainWindow w, string name) { var b = Get<Button>(w, name); b.BringIntoView(); Settle(); RealClick(w, Get<Button>(w, name)); }
    static void Key1(MainWindow w, Key k, PhysicalKey ph) { w.KeyPress(k, RawInputModifiers.None, ph, null); w.KeyRelease(k, RawInputModifiers.None, ph, null); Settle(); }
    static void Type(MainWindow w, string text) { foreach (var ch in text) { w.KeyTextInput(ch.ToString()); Settle(); } }

    [AvaloniaFact(DisplayName = "SPEC-05 UI: Werte in die Tabelle tippen (Enter/Tab), Spalte per Kopf wählen, Kennzahlen, Diagrammart, Ausgleich, Export")]
    public void Type_values_chart_export()
    {
        var (w, _, _) = Ui.Open();
        w.Show(MainWindow.StudioPages["SPEC-05"]); Settle();
        RealClick(w, Get<Button>(w, "prog-tpl-leer"));
        Assert.Null(w.TryFind<Button>("sci-desc-run")); // kein Formular mehr
        var grid = Get<MeasureGrid>(w, "ms-grid"); grid.Focus(); Settle();
        // x-Spalte: 1, 2, 3, 4 – y-Spalte: 2,1 4,0 5,9 8,1 (Tippen, Enter)
        foreach (var v in new[] { "1", "2", "3", "4" }) { Type(w, v); Key1(w, Key.Enter, PhysicalKey.Enter); }
        Assert.Equal([1.0, 2, 3, 4], Model(w).Columns[0].Values.Select(x => x!.Value));
        grid = Get<MeasureGrid>(w, "ms-grid"); grid.Focus(); Settle();
        for (var i = 0; i < 4; i++) Key1(w, Key.Up, PhysicalKey.ArrowUp);
        Key1(w, Key.Right, PhysicalKey.ArrowRight);
        foreach (var v in new[] { "2,1", "4", "5,9", "8,1" }) { Type(w, v); Key1(w, Key.Enter, PhysicalKey.Enter); }
        var m = Model(w); Assert.Equal(4.0, m.Get(1, 1)); Assert.Equal(8.1, m.Get(1, 3));
        // Ausgleichsgerade
        Press(w, "prog-cmd-ms-f-linear");
        Assert.Contains("R² = 0,99", Get<SelectableTextBlock>(w, "ms-fit").Text);
        // Spaltenkopf anklicken → Kennzahlen der Spalte
        var g = Get<MeasureGrid>(w, "ms-grid"); var head = g.TranslatePoint(new Point(44 + 104 + 50, 20), w)!.Value;
        w.MouseDown(head, MouseButton.Left); w.MouseUp(head, MouseButton.Left); Settle();
        Assert.Equal(1, w.SheetCursor.Col);
        Assert.Contains("5,025", string.Join(" ", w.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text))); // Mittelwert y
        Press(w, "prog-cmd-ms-k-balken"); Assert.Equal("balken", Model(w).Chart.Kind);
        foreach (var x in new[] { "csv", "report", "svg" })
        {
            Press(w, "prog-export-" + x);
            var file = w.ProgramMessage(SK)["Exportiert: ".Length..].Trim();
            Assert.True(File.Exists(file) && new FileInfo(file).Length > 15, x);
        }
        w.Close();
    }

    [AvaloniaFact(DisplayName = "SPEC-05 UI: Zwei Gruppen vergleichen (t-Test), neue Spalte aus Glättung, CSV laden, Rechner rechnen sofort")]
    public void Compare_csv_calcs()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        w.Show(MainWindow.StudioPages["SPEC-05"]); Settle();
        RealClick(w, Get<Button>(w, "prog-tpl-duenger"));
        Assert.Contains("signifikant", Get<StackPanel>(w, "ms-cmp-res").Children.OfType<TextBlock>().First().Text);
        Press(w, "ms-smooth"); Assert.Equal(3, Model(w).Columns.Count); Assert.Contains("geglättet", Model(w).Columns[2].Name);
        var f = Path.Combine(root, "mess.csv"); File.WriteAllText(f, "Zeit [s];Spannung [V]\n0;0\n1;3,2\n2;4,4\n");
        Get<TextBox>(w, "ms-csv-path").Text = f; Press(w, "ms-csv-load");
        Assert.Equal("Spannung", Model(w).Columns[1].Name); Assert.Equal("V", Model(w).Columns[1].Unit); Assert.Equal(3, Model(w).Rows);
        Get<Expander>(w, "ms-calcs").IsExpanded = true; Settle();
        Assert.Contains("Zusammenhang signifikant", Get<SelectableTextBlock>(w, "sci-chi-out").Text);
        Assert.Contains("97 Messungen", Get<SelectableTextBlock>(w, "sci-n-out").Text);
        Assert.Contains("Ergebnis", Get<SelectableTextBlock>(w, "sci-err-out").Text);
        w.Close();
    }
}
