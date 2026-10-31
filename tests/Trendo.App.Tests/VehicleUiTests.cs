using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Trendo.Core;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0268: Programm „Fahrzeugbuch“ über den echten Eingabeweg – Fahrzeug anlegen, tanken, Fahrten, Tabelle ändern, Übersicht, Export, Rechner.</summary>
public class SPEC16_Ui
{
    static T Get<T>(MainWindow w, string name) where T : Control { Settle(); return w.GetVisualDescendants().OfType<T>().Last(c => c.Name == name && c.IsEffectivelyVisible && c.Bounds.Width > 0); }
    static T? Try<T>(MainWindow w, string name) where T : Control { Settle(); return w.GetVisualDescendants().OfType<T>().LastOrDefault(c => c.Name == name && c.IsEffectivelyVisible && c.Bounds.Width > 0); }
    static void Press(MainWindow w, string name) { var b = Get<Button>(w, name); b.BringIntoView(); Settle(); RealClick(w, Get<Button>(w, name)); }
    static void Key1(MainWindow w, Key k, PhysicalKey ph) { w.KeyPress(k, RawInputModifiers.None, ph, null); w.KeyRelease(k, RawInputModifiers.None, ph, null); Settle(); }
    static void Retype(MainWindow w, string name, string text, bool enter = true) { var tb = Get<TextBox>(w, name); tb.BringIntoView(); RealClick(w, tb); tb.Focus(); tb.SelectAll(); Settle(); Key1(w, Key.Back, PhysicalKey.Backspace); foreach (var ch in text) { w.KeyTextInput(ch.ToString()); Settle(); } if (enter) Key1(w, Key.Enter, PhysicalKey.Enter); }
    static void CloseToasts(MainWindow w) { while (Try<Button>(w, "toast-close") is { } c) RealClick(w, c); }

    [AvaloniaFact(DisplayName = "SPEC-16 UI: Fahrzeug anlegen, dreimal voll tanken (Enter), Verbrauch und ct/km in der Übersicht, Tankung in der Tabelle korrigieren")]
    public void Add_refuel_overview()
    {
        var (w, _, _) = Ui.Open();
        w.Show(MainWindow.StudioPages["SPEC-16"]); Settle();
        Assert.Null(Try<Button>(w, "car-add-run")); // kein Formular mehr
        Retype(w, "car-new-name", "Golf", false); Retype(w, "car-new-plate", "B-EM 123", false);
        Press(w, "car-add"); Assert.Equal("Golf", w.CarSel);
        Retype(w, "car-p-hu", "03.2027"); Assert.Equal(new DateTime(2027, 3, 1), w.Cars.Get("Golf").InspectionDue);
        Press(w, "car-tab-tank");
        foreach (var (odo, amt) in new[] { ("10000", "40"), ("10500", "30"), ("11100", "36") })
        {
            Retype(w, "car-f-new-odo", odo, false); Retype(w, "car-f-new-amt", amt, false); Retype(w, "car-f-new-price", "1,80");
        }
        Assert.Equal(3, w.Cars.Get("Golf").Fuel.Count);
        Press(w, "car-tab-uebersicht");
        Assert.Equal(6, VehicleBook.Stats(w.Cars.Get("Golf")).AvgPer100); // (30+36)/1100*100
        Assert.Contains(Fach.F(6.0, 1) + " l", string.Join(" ", Get<Border>(w, "car-kpi-avg").GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text)));
        Assert.Contains("10,8 ct", string.Join(" ", Get<Border>(w, "car-kpi-km").GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text))); // 66 l × 1,80 € / 1100 km
        Press(w, "car-tab-tank");
        Retype(w, "car-f-amt-0", "44"); // neueste Tankung (oben) korrigieren
        Assert.Equal(44, w.Cars.Get("Golf").Fuel.OrderBy(f => f.Odometer).Last().Amount);
        Retype(w, "car-f-odo-0", "abc"); Assert.StartsWith("⚠", Get<TextBlock>(w, "car-msg").Text);
        Press(w, "car-f-del-0"); Assert.Equal(2, w.Cars.Get("Golf").Fuel.Count);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "SPEC-16 UI: Fahrten eintragen (dienstlich mit Zweck), Lücke wird gemeldet, Export CSV, Rechner rechnen sofort mit Werten des Fahrzeugs, Löschen nur mit Bestätigung")]
    public void Trips_export_calcs()
    {
        var (w, _, _) = Ui.Open();
        w.Show("fahrzeug"); Settle();
        Press(w, "car-demo"); Assert.Equal("Familienauto", w.CarSel);
        Press(w, "car-tab-fahrten");
        var start = w.Cars.Get("Familienauto").Odometer;
        Retype(w, "car-t-new-start", (start + 10).ToString("0"), false); Retype(w, "car-t-new-end", (start + 52).ToString("0"), false);
        RealClick(w, Get<CheckBox>(w, "car-t-new-biz")); Retype(w, "car-t-new-why", "Kundentermin Freising");
        var t = w.Cars.Get("Familienauto").Trips.Last(); Assert.True(t.Business); Assert.Equal(42, t.EndKm - t.StartKm); Assert.Equal("Kundentermin Freising", t.Purpose);
        Assert.Contains("Lücke im Fahrtenbuch", string.Join(" ", w.GetVisualDescendants().OfType<TextBlock>().Select(x => x.Text)));
        Press(w, "car-export"); var csv = Get<TextBlock>(w, "car-msg").Text!["Gespeichert: ".Length..]; Assert.Contains("Kundentermin Freising", File.ReadAllText(csv));
        CloseToasts(w);
        Press(w, "car-export-fuel"); Assert.True(File.Exists(Get<TextBlock>(w, "car-msg").Text!["Gespeichert: ".Length..]));
        CloseToasts(w);
        Assert.Contains("ct je km", Get<SelectableTextBlock>(w, "car-year-out").Text);
        Assert.Equal(VehicleBook.Stats(w.Cars.Get("Familienauto")).AvgPer100!.Value.ToString("0.#", Fach.De), Get<TextBox>(w, "car-year-c").Text); // vorbelegt mit dem echten Verbrauch
        Assert.Contains("Entfernungspauschale", Get<SelectableTextBlock>(w, "car-commute-out").Text);
        Get<ComboBox>(w, "car-company-kind").SelectedItem = "elektro"; Settle(); Assert.Contains("günstiger", Get<SelectableTextBlock>(w, "car-company-out").Text);
        Press(w, "car-del"); Assert.StartsWith("⚠", Get<TextBlock>(w, "car-msg").Text); Assert.Single(w.Cars.Garage.Vehicles);
        Get<TextBox>(w, "car-del-confirm").Text = "Familienauto"; Press(w, "car-del"); Assert.Empty(w.Cars.Garage.Vehicles);
        w.Close();
    }
}
