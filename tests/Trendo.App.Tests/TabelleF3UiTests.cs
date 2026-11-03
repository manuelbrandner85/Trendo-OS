using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>F3 Tabelle (Vorbild-Analyse „Nächste Schritte“): Autofilter mit Werteliste je Spalte und Pivot-Tabelle in der Oberfläche.</summary>
public class TabelleF3UiTests
{
    static void Pump() { for (var i = 0; i < 5; i++) Dispatcher.UIThread.RunJobs(); }

    static MainWindow Data()
    {
        var (w, _, _) = Ui.Open();
        w.SheetNew(); w.Show("tabelle"); Pump();
        string[][] rows = [["Monat", "Kategorie", "Betrag"], ["Jan", "Miete", "800"], ["Jan", "Essen", "250"], ["Feb", "Miete", "800"], ["Feb", "Essen", "200"], ["Feb", "Auto", "120"]];
        for (var r = 0; r < rows.Length; r++) for (var c = 0; c < 3; c++) w.SheetSetCell($"{(char)('A' + c)}{r + 1}", rows[r][c]);
        w.SheetSelect("B2"); w.Show("tabelle"); Pump();
        return w;
    }

    [AvaloniaFact(DisplayName = "Tabelle F3: Autofilter – Knopf unter „Daten“, rechts je Spalte Werte mit Anzahl, Haken weg blendet Zeilen aus, „Alle“ und „Autofilter aus“")]
    public void Auto_filter_ui()
    {
        var w = Data();
        try
        {
            w.Find<Button>("tabelle-rib-daten").Press(); Pump();
            w.Find<Button>("tabelle-autofilter").Press(); Pump();
            Assert.NotNull(w.ActiveSheet.AutoFilter);
            var miete = w.Find<CheckBox>("tabelle-af-1-2");
            Assert.Equal("Miete  (2)", miete.Content);
            miete.IsChecked = false; Pump();
            Assert.Equal([1, 3], w.ActiveSheet.HiddenRows.Order());
            Assert.Contains("2 Zeile(n) ausgeblendet", string.Join("|", w.GetLogicalTexts()));
            w.Find<CheckBox>("tabelle-af-0-0").IsChecked = false; Pump(); // Feb weg (Werte: Feb, Jan)
            Assert.Equal([1, 3, 4, 5], w.ActiveSheet.HiddenRows.Order()); // nur „Jan Essen“ bleibt
            w.Find<Button>("tabelle-af-all-1").Press(); Pump();
            Assert.Equal([3, 4, 5], w.ActiveSheet.HiddenRows.Order());
            w.Find<Button>("tabelle-af-off").Press(); Pump();
            Assert.Null(w.ActiveSheet.AutoFilter); Assert.Empty(w.ActiveSheet.HiddenRows); Assert.Null(w.TryFind<Border>("tabelle-af"));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Tabelle F3: Pivot – „⊞ Pivot“ öffnet den Bauer rechts mit Vorschlag (Kategorie/Betrag), erstellt ein neues Blatt mit Formeln, das mitrechnet")]
    public void Pivot_ui()
    {
        var w = Data();
        try
        {
            w.Find<Button>("tabelle-rib-daten").Press(); Pump();
            w.Find<Button>("tabelle-pivot").Press(); Pump();
            Assert.Equal("Monat", w.Find<ComboBox>("tabelle-pivot-rows").SelectedItem); // erstes Textfeld
            Assert.Equal("Betrag", w.Find<ComboBox>("tabelle-pivot-values").SelectedItem); // letztes Zahlenfeld
            w.Find<ComboBox>("tabelle-pivot-rows").SelectedIndex = 1; // Kategorie
            w.Find<ComboBox>("tabelle-pivot-cols").SelectedIndex = 1; // Monat
            w.Find<Button>("tabelle-pivot-go").Press(); Pump();
            Assert.Equal("Pivot1", w.ActiveSheet.Name);
            Assert.Equal(2, w.SheetDocument.Sheets.Count);
            Assert.Equal("1.600", w.SheetDisplay("D7").Replace(",00", "")); // Miete gesamt (Zeilen Auto, Essen, Miete; Spalten Feb, Jan, Gesamt)
            Assert.Equal("1.120", w.SheetDisplay("B8").Replace(",00", "")); // Februar gesamt
            Assert.Null(w.TryFind<Border>("tabelle-pivot")); // Bauer schließt
            Assert.Contains("rechnen mit", w.Find<TextBlock>("tabelle-status").Text);
        }
        finally { w.Close(); }
    }
}
