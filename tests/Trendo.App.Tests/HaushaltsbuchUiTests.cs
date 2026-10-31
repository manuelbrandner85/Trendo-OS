using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Trendo.Core;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// T-0301: Geld als Haushaltsbuch – Ring filtert, Buchung in der Zeile ändern mit Rückgängig, Monat blättern, Fixkosten, Import-Vorschau, Regeln.
public class Haushaltsbuch_Ui
{
    [AvaloniaFact(DisplayName = "T-0301: Ring-Klick filtert, Suche, Buchung ändern + Rückgängig, Monat blättern, Fixkosten, Kontoauszug erst Vorschau dann Ja, Regel anlegen")]
    public void Ui_household()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1100;
        void Click(string name) { var b = V<Button>(w, name); b.BringIntoView(); Settle(); RealClick(w, V<Button>(w, name)); Settle(); }
        var t = DateOnly.FromDateTime(DateTime.Today); var m0 = new DateOnly(t.Year, t.Month, 1);
        foreach (var e in new Entry[] { new(m0, "Gehalt", 2500, "Einnahmen"), new(m0, "Miete Wohnung", -800, "Wohnen"), new(m0, "Rewe Markt", -50, "Lebensmittel"),
            new(m0.AddMonths(-1), "Miete Wohnung", -800, "Wohnen"), new(m0.AddMonths(-2), "Miete Wohnung", -800, "Wohnen"), new(m0.AddMonths(-1), "Kino", -20, "Freizeit") }) w.Money.Add(e);
        w.Show("finanzen"); Settle();
        Assert.Contains(w.GetLogicalTexts(), x => x == "850,00 €");
        // Ring: Klick auf das Wohnen-Segment filtert die Liste
        var d = w.HouseholdDonutForTests!; d.BringIntoView(); Settle();
        var i = d.Slices.FindIndex(s => s.Name == "Wohnen"); var p = d.TranslatePoint(d.SliceCenter(i), w)!.Value;
        w.MouseDown(p, MouseButton.Left); w.MouseUp(p, MouseButton.Left); Settle();
        Assert.Equal("Wohnen", w.HouseholdCategory);
        Assert.Contains("1 Buchungen", w.Find<TextBlock>("hb-sum").Text);
        Click("hb-cat-clear"); Assert.Null(w.HouseholdCategory);
        // Suche + in der Zeile ändern, Rückgängig
        var find = w.Find<TextBox>("hb-find"); find.Text = "rewe"; find.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter }); Settle();
        Assert.Contains("1 Buchungen", w.Find<TextBlock>("hb-sum").Text);
        Click("hb-edit-0");
        w.Find<TextBox>("hb-edit-amount").Text = "-55,10"; Click("hb-edit-save");
        Assert.Contains(w.Money.All(), e => e.Text == "Rewe Markt" && e.Amount == -55.10m);
        Assert.Equal("Buchung ändern", w.HouseholdUndoLabel);
        Click("hb-undo");
        Assert.Contains(w.Money.All(), e => e.Text == "Rewe Markt" && e.Amount == -50m);
        Click("hb-find-clear");
        // Monat zurück: Kino im Vormonat
        Click("hb-prev"); Assert.Contains("Kino", w.GetLogicalTexts()); Click("hb-today");
        // Fixkosten erkannt
        Assert.Contains(w.GetLogicalTexts(), x => x.StartsWith("Fixkosten & Abos – etwa 800,00"));
        // Kontoauszug: erst Vorschau, Kategorie ändern, dann Ja
        var csv = Path.Combine(root, "auszug.csv");
        File.WriteAllText(csv, $"Buchungstag;Valuta;Auftraggeber/Empfänger;Verwendungszweck;Betrag\n{m0:dd.MM.yyyy};{m0:dd.MM.yyyy};Wochenmarkt;Samstag;-12,00\n{m0:dd.MM.yyyy};{m0:dd.MM.yyyy};Stadtwerke;Abschlag;-90,00\n");
        w.HouseholdPreviewImport(csv); Settle();
        Assert.Equal(2, w.Find<StackPanel>("hb-import-rows").Children.Count);
        Assert.DoesNotContain(w.Money.All(), e => e.Text.Contains("Wochenmarkt"));
        w.Find<ComboBox>("hb-imp-cat-0").SelectedItem = "Lebensmittel"; Settle();
        Click("hb-import-yes");
        Assert.Contains(w.Money.All(), e => e.Text.Contains("Wochenmarkt") && e.Category == "Lebensmittel");
        Assert.Equal("Lebensmittel", w.MoneyMemory.Lookup(w.Money.All().First(e => e.Text.Contains("Wochenmarkt")).Text));
        // Nochmal derselbe Auszug: alles „schon da“, Ja gesperrt
        w.HouseholdPreviewImport(csv); Settle();
        Assert.False(w.Find<Button>("hb-import-yes").IsEnabled); Click("hb-import-no");
        Assert.Null(w.TryFind<StackPanel>("hb-import"));
        // Regel anlegen und auf alle anwenden
        w.Find<TextBox>("hb-rule-text").Text = "Stadtwerke"; w.Find<ComboBox>("hb-rule-cat").SelectedItem = "Wohnen"; Click("hb-rule-add");
        Click("hb-rule-apply");
        Assert.Contains(w.Money.All(), e => e.Text.Contains("Stadtwerke") && e.Category == "Wohnen");
    }
}
