using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0263: Studio Mode & Produkt ist nur noch das Programm „Kleidung & Produkte“ – Schnittteile direkt bearbeiten, Etikett-Prüfung, Größensprung, eingebaute Rechner (sofort).</summary>
public class SPEC08_Ui
{
    const string SK = "mode-programm";
    static T Get<T>(MainWindow w, string name) where T : Control { Settle(); return w.GetVisualDescendants().OfType<T>().Last(c => c.Name == name && c.IsEffectivelyVisible && c.Bounds.Width > 0); }
    static T? Try<T>(MainWindow w, string name) where T : Control { Settle(); return w.GetVisualDescendants().OfType<T>().LastOrDefault(c => c.Name == name && c.IsEffectivelyVisible && c.Bounds.Width > 0); }
    static void Press(MainWindow w, string name) { var b = Get<Button>(w, name); b.BringIntoView(); Settle(); RealClick(w, Get<Button>(w, name)); }
    static void Key1(MainWindow w, Key k, PhysicalKey ph) { w.KeyPress(k, RawInputModifiers.None, ph, null); w.KeyRelease(k, RawInputModifiers.None, ph, null); Settle(); }
    static void Retype(MainWindow w, string name, string text) { var tb = Get<TextBox>(w, name); tb.BringIntoView(); RealClick(w, tb); tb.Focus(); tb.SelectAll(); Settle(); Key1(w, Key.Back, PhysicalKey.Backspace); foreach (var ch in text) { w.KeyTextInput(ch.ToString()); Settle(); } Key1(w, Key.Enter, PhysicalKey.Enter); }
    static string Status(MainWindow w) => string.Join(" ", w.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text));

    [AvaloniaFact(DisplayName = "SPEC-08 UI: kein Rechner-Formular mehr; Schnittteil umbenennen/vergrößern/entfernen ändert Zuschnitt und Stoffbedarf; Rückgängig")]
    public void Edit_pieces()
    {
        var (w, _, _) = Ui.Open();
        Assert.Equal(SK, MainWindow.StudioPages["SPEC-08"]);
        w.Show(SK); Settle();
        RealClick(w, Get<Button>(w, "prog-tpl-tshirt"));
        Assert.Null(Try<Control>(w, "fa-body")); // die alte Formularseite gibt es nicht mehr
        Assert.Contains("Stoff 1,2 m", Status(w));
        Retype(w, "mode-pc-name-0", "Front");
        Retype(w, "mode-pc-h-0", "120");
        var pcs = w.ProgramActive(SK)!.Data["pieces"]!.AsArray();
        Assert.Equal("Front", (string?)pcs[0]!["name"]); Assert.Equal(120, (double)pcs[0]!["h"]!);
        Assert.Contains("Stoff 1,7 m", Status(w));
        Press(w, "mode-pc-del-2");
        Assert.Equal(2, w.ProgramActive(SK)!.Data["pieces"]!.AsArray().Count);
        Press(w, "prog-undo");
        Assert.Equal(3, w.ProgramActive(SK)!.Data["pieces"]!.AsArray().Count);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "SPEC-08 UI: Etikett sofort geprüft, Größensprung ändert die Größentabelle, Rechner (Schuh, Kind, BH, Ring, EAN, Staffel vom Ladenpreis) rechnen beim Tippen")]
    public void Label_grade_calcs()
    {
        var (w, _, _) = Ui.Open();
        w.Show(SK); Settle(); RealClick(w, Get<Button>(w, "prog-tpl-tshirt"));
        Assert.Contains("✓", Get<TextBlock>(w, "mode-label-check").Text);
        Retype(w, "mode-p-material", "90 % Baumwolle, 5 % Elasthan");
        Assert.StartsWith("⚠", Get<TextBlock>(w, "mode-label-check").Text);
        Retype(w, "mode-p-gradeStep", "6");
        Assert.Contains(" 88 ", " " + Status(w) + " "); // Brust S bei Basis L (100) und 6 cm Sprung: 100 − 2 × 6
        Get<Expander>(w, "mode-calcs").IsExpanded = true; Settle();
        Assert.Contains("UK 8", Get<SelectableTextBlock>(w, "mode-c-shoe-out").Text);
        Retype(w, "mode-c-shoe-eu", "26,5 cm"); Assert.Contains("EU 42", Get<SelectableTextBlock>(w, "mode-c-shoe-out").Text);
        Assert.Contains("Größe 98", Get<SelectableTextBlock>(w, "mode-c-kid-out").Text);
        Assert.Contains("75C", Get<SelectableTextBlock>(w, "mode-c-bra-out").Text);
        Assert.Contains("US 7", Get<SelectableTextBlock>(w, "mode-c-ring-out").Text);
        Assert.Contains("4006381333931", Get<SelectableTextBlock>(w, "mode-c-ean-out").Text);
        Assert.Contains("10 % Rabatt", Get<SelectableTextBlock>(w, "mode-c-tier-out").Text);
        Assert.Contains("XL:", Get<SelectableTextBlock>(w, "mode-c-grade-out").Text);
        Assert.Contains("Knäuel", Get<SelectableTextBlock>(w, "mode-c-yarn-out").Text);
        Retype(w, "mode-c-ean-e", "4006381333932"); Assert.StartsWith("⚠ Prüfziffer falsch", Get<SelectableTextBlock>(w, "mode-c-ean-out").Text);
        w.Close();
    }
}
