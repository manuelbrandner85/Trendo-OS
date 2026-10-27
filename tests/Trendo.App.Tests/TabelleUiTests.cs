using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core.Office;
using Xunit;
namespace Trendo.App.Tests;

/// <summary>T-0228 Stufe C: Oberfläche von Trendo Tabelle.</summary>
public class T0228_TrendoTabelleUi
{
    static void Pump() { for (var i = 0; i < 5; i++) Dispatcher.UIThread.RunJobs(); }
    static void Key(Control c, Key k, KeyModifiers m = KeyModifiers.None) => c.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = k, KeyModifiers = m });

    [AvaloniaFact(DisplayName = "T-0228 G1: Startseite mit Vorlagen; Vorlage Haushaltsbuch rechnet; Statusleiste zeigt Summe/Mittelwert/Anzahl (G5)")]
    public void Start_template_and_stats()
    {
        var (w, _, _) = Ui.Open();
        w.Show("tabelle"); Pump();
        Assert.True(w.SheetStartVisible);
        foreach (var t in SheetTemplates.Builtin) Assert.NotNull(w.TryFind<Button>("tabelle-tpl-" + t.Id));
        w.Find<Button>("tabelle-tpl-haushalt").Press(); Pump();
        Assert.False(w.SheetStartVisible);
        Assert.NotNull(w.TryFind<SheetGridView>("tabelle-grid"));
        Assert.Equal("1.160,00 €", w.SheetDisplay("C11"));
        w.SheetSelect("C2:C7");
        var st = w.SheetSelectionStats();
        Assert.Equal(3740, st.Sum); Assert.Equal(6, st.Count);
        var status = w.Find<TextBlock>("tabelle-status").Text!;
        Assert.Contains("Summe: 3.740,00 €", status);
        Assert.Contains("Anzahl: 6", status);
        Assert.Equal("C2:C7", w.Find<TextBlock>("tabelle-namebox").Text);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "T-0228 E1: tippen ins Raster, Enter geht nach unten, Formel rechnet, Formelleiste zeigt Inhalt, Entf/Strg+Z/Strg+Y")]
    public void Type_formula_undo()
    {
        var (w, _, _) = Ui.Open();
        w.SheetNew(); w.Show("tabelle"); Pump();
        var grid = w.Find<SheetGridView>("tabelle-grid");
        w.SheetType("12,5"); w.SheetType("7,5"); w.SheetType("=SUMME(A1:A2)");
        Assert.Equal((3, 0), grid.Active);
        Assert.Equal("20", w.SheetDisplay("A3"));
        w.SheetSelect("A3"); Pump();
        Assert.Equal("=SUMME(A1:A2)", w.Find<TextBox>("tabelle-formula").Text);
        // Formelleiste ändern
        var f = w.Find<TextBox>("tabelle-formula"); f.Text = "=A1*2"; Key(f, Avalonia.Input.Key.Enter);
        Assert.Equal("25", w.SheetDisplay("A3"));
        // Pfeil, Entf, Rückgängig, Wiederholen
        w.SheetSelect("A1"); Key(grid, Avalonia.Input.Key.Down); Assert.Equal((1, 0), grid.Active);
        Key(grid, Avalonia.Input.Key.Delete); Assert.Equal("", w.ActiveSheet.Raw(1, 0));
        Key(grid, Avalonia.Input.Key.Z, KeyModifiers.Control); Assert.Equal("7,5", w.ActiveSheet.Raw(1, 0));
        Key(grid, Avalonia.Input.Key.Y, KeyModifiers.Control); Assert.Equal("", w.ActiveSheet.Raw(1, 0));
        // Shift+Pfeil erweitert die Markierung
        w.SheetSelect("A1"); Key(grid, Avalonia.Input.Key.Right, KeyModifiers.Shift); Key(grid, Avalonia.Input.Key.Down, KeyModifiers.Shift);
        Assert.Equal((0, 0, 1, 1), grid.Selection);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "T-0228 Menüband: Σ-Summe, Fett, Euro, Sortieren, Ausfüllen (Mo, Di …), Kopieren/Einfügen mit angepasster Formel, Blätter")]
    public void Ribbon_commands()
    {
        var (w, _, _) = Ui.Open();
        w.SheetNew(); w.Show("tabelle"); Pump();
        w.SheetSetCell("A1", "Name"); w.SheetSetCell("B1", "Betrag");
        w.SheetSetCell("A2", "Zora"); w.SheetSetCell("B2", "30");
        w.SheetSetCell("A3", "Anna"); w.SheetSetCell("B3", "10");
        w.SheetSetCell("A4", "Max"); w.SheetSetCell("B4", "20");
        w.SheetSelect("B5"); w.Find<Button>("tabelle-sum").Press(); Pump();
        Assert.Equal("=SUMME(B2:B4)", w.ActiveSheet.Raw(4, 1));
        Assert.Equal("60", w.SheetDisplay("B5"));
        w.SheetSelect("B2:B5"); w.Find<Button>("tabelle-bold").Press(); w.Find<Button>("tabelle-euro").Press(); Pump();
        Assert.True(w.ActiveSheet.Get(1, 1)!.Bold); Assert.Equal(NumFmt.Euro, w.ActiveSheet.Get(4, 1)!.Format);
        // Sortieren nach Spalte A: Kopfzeile und Summenzeile bleiben stehen
        w.SheetSelect("A2"); w.SheetSort(true);
        Assert.Equal(["Anna", "Max", "Zora"], new[] { w.ActiveSheet.Raw(1, 0), w.ActiveSheet.Raw(2, 0), w.ActiveSheet.Raw(3, 0) });
        Assert.Equal("Name", w.ActiveSheet.Raw(0, 0));
        // Ausfüllen
        w.SheetSetCell("D1", "Mo"); w.SheetSelect("D1:D5"); w.SheetFill();
        Assert.Equal(["Mo", "Di", "Mi", "Do", "Fr"], Enumerable.Range(0, 5).Select(r => w.ActiveSheet.Raw(r, 3)));
        // Kopieren/Einfügen verschiebt Bezüge
        w.SheetSetCell("C2", "=B2*2"); w.SheetSelect("C2");
        var grid = w.Find<SheetGridView>("tabelle-grid");
        Key(grid, Avalonia.Input.Key.C, KeyModifiers.Control); w.SheetSelect("C3"); Key(grid, Avalonia.Input.Key.V, KeyModifiers.Control); Pump();
        Assert.Equal("=B3*2", w.ActiveSheet.Raw(2, 2));
        // Blätter
        w.Find<Button>("tabelle-sheet-add").Press(); Pump();
        Assert.Equal(2, w.SheetDocument.Sheets.Count); Assert.Equal(1, w.SheetIndex);
        w.SheetRename(1, "Auswertung"); w.Show("tabelle"); Pump();
        Assert.Equal("Auswertung", w.Find<Button>("tabelle-sheet-1").Content);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "T-0228: Speichern als XLSX und wieder öffnen; erscheint unter „Zuletzt geöffnet“")]
    public void Save_and_reopen()
    {
        var (w, _, root) = Ui.Open();
        w.SheetNew("rechnung"); w.Show("tabelle"); Pump();
        var path = Path.Combine(root, "rechnung-test.xlsx");
        w.SheetSaveAs(path);
        Assert.True(File.Exists(path)); Assert.False(w.SheetDirty);
        w.SheetNew(); w.SheetOpen(path);
        Assert.Equal("=E6+E7", w.ActiveSheet.Raw(7, 4));
        Assert.Equal("392,58 €", w.SheetDisplay("E8"));
        w.SheetShowStart();
        w.Show("tabelle"); Pump();
        Assert.NotNull(w.TryFind<Button>("tabelle-recent-0"));
        w.Close();
    }
}
