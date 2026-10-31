using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Trendo.Core;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0265: Programm „Anlagen-Monitor“ über den echten Eingabeweg – Beispielanlagen, Baum und Kacheln, Zeitraum, Grenzen eintippen, Sensor anlegen, CSV einlesen, verwalten, Live.</summary>
public class SPEC10_Ui
{
    static T Get<T>(MainWindow w, string name) where T : Control { Settle(); return w.GetVisualDescendants().OfType<T>().Last(c => c.Name == name && c.IsEffectivelyVisible && c.Bounds.Width > 0); }
    static T? Try<T>(MainWindow w, string name) where T : Control { Settle(); return w.GetVisualDescendants().OfType<T>().LastOrDefault(c => c.Name == name && c.IsEffectivelyVisible && c.Bounds.Width > 0); }
    static void Press(MainWindow w, string name) { var b = Get<Button>(w, name); b.BringIntoView(); Settle(); RealClick(w, Get<Button>(w, name)); }
    static void CloseToasts(MainWindow w) { while (Try<Button>(w, "toast-close") is { } c) RealClick(w, c); }
    static void Key1(MainWindow w, Key k, PhysicalKey ph) { w.KeyPress(k, RawInputModifiers.None, ph, null); w.KeyRelease(k, RawInputModifiers.None, ph, null); Settle(); }
    static void Retype(MainWindow w, string name, string text) { var tb = Get<TextBox>(w, name); tb.BringIntoView(); RealClick(w, tb); tb.Focus(); tb.SelectAll(); Settle(); Key1(w, Key.Back, PhysicalKey.Backspace); foreach (var ch in text) { w.KeyTextInput(ch.ToString()); Settle(); } Key1(w, Key.Enter, PhysicalKey.Enter); }

    [AvaloniaFact(DisplayName = "SPEC-10 UI: Beispielanlagen laden, im Baum wählen, Kachel zeigt Zustand, Zeitraum 24 h, Grenze eintippen ändert die Ampel, Prognose")]
    public void Demo_select_limits()
    {
        var (w, _, _) = Ui.Open();
        w.Show(MainWindow.StudioPages["SPEC-10"]); Settle();
        Assert.Null(Try<Button>(w, "twin-def-run")); // kein Formular mehr
        Press(w, "twin-demo");
        Assert.Contains("Beispielwerte geladen", Get<TextBlock>(w, "twin-msg").Text);
        Assert.Equal(("Kühlraum", "Temperatur"), (w.TwinAsset, w.TwinSensor));
        Assert.Contains("Temperatur: Warnung", Get<TextBlock>(w, "twin-Kühlraum-Temperatur").Text);
        Assert.Contains("Alarm in etwa", string.Join(" ", Get<Border>(w, "twin-tile-Kühlraum-Temperatur").GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text)));
        Assert.NotNull(Try<Border>(w, "twin-chart"));
        Press(w, "twin-node-Heizung-Vorlauf");
        Assert.Equal(("Heizung", "Vorlauf"), (w.TwinAsset, w.TwinSensor));
        Assert.Equal(3, Get<WrapPanel>(w, "twin-tiles").Children.Count);
        Press(w, "twin-range-24h"); Assert.Equal("24h", w.TwinRange);
        Assert.StartsWith("9", Get<SelectableTextBlock>(w, "twin-stats").Text); // 97 Werte in 24 h (alle 15 min)
        // Warnung ab 40 eintippen → Vorlauf geht auf Warnung oder Alarm
        Retype(w, "twin-p-wa", "40");
        Assert.Equal(40, w.Twin.Model.Assets.First(a => a.Name == "Heizung").Sensors.First(s => s.Name == "Vorlauf").WarnAbove);
        Assert.Contains("Warnung", Get<TextBlock>(w, "twin-Heizung-Vorlauf").Text);
        Retype(w, "twin-p-wa", ""); Assert.True(double.IsNaN(w.Twin.Model.Assets.First(a => a.Name == "Heizung").Sensors.First(s => s.Name == "Vorlauf").WarnAbove));
        Retype(w, "twin-p-fc", "12"); Assert.Contains("In 12 h etwa", Get<TextBlock>(w, "twin-fc-out").Text);
        // Kachel anklicken wählt den Sensor
        RealClick(w, Get<Border>(w, "twin-tile-Heizung-Brenner")); Assert.Equal("Brenner", w.TwinSensor);
        Assert.Contains("Energie", Get<SelectableTextBlock>(w, "twin-stats").Text); // kW → kWh
        w.Close();
    }

    [AvaloniaFact(DisplayName = "SPEC-10 UI: Sensor anlegen, CSV einlesen, ableiten, umbenennen, CSV/SVG speichern, löschen nur mit Bestätigung; PC messen und Live")]
    public void Manage_and_live()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        w.PcSampleSource = () => new MainWindow.PcSample(50, 400);
        w.Show("zwilling"); Settle();
        var a = Get<TextBox>(w, "twin-add-asset"); a.Text = "";
        Retype(w, "twin-add-asset", "Werkstatt"); Retype(w, "twin-add-sensor", "Druck"); Retype(w, "twin-add-unit", "bar");
        Press(w, "twin-add");
        Assert.Equal(("Werkstatt", "Druck"), (w.TwinAsset, w.TwinSensor));
        var f = Path.Combine(root, "druck.csv");
        File.WriteAllText(f, "Zeit;Anlage;Sensor;Wert\n" + string.Join("\n", Enumerable.Range(0, 12).Select(i => $"{new DateTime(2026, 10, 30, 8, 0, 0).AddHours(i):dd.MM.yyyy HH:mm};Werkstatt;Druck;{6 + i * 0.1:0.0}".Replace('.', ','))));
        Get<TextBox>(w, "twin-csv-path").Text = f; Press(w, "twin-csv-load");
        Assert.Contains("12 Messwerte übernommen", Get<TextBlock>(w, "twin-msg").Text);
        Get<TextBox>(w, "twin-derive-name").Text = "Druck psi"; Get<TextBox>(w, "twin-derive-a").Text = "14,5"; Get<TextBox>(w, "twin-derive-b").Text = "0";
        Press(w, "twin-derive"); Assert.Equal("Druck psi", w.TwinSensor);
        Assert.Equal(87, TwinPlus.Series(w.Twin.Model, "Werkstatt", "Druck psi")[0].Value, 6);
        Get<TextBox>(w, "twin-rename-to").Text = "Druck (psi)"; Press(w, "twin-rename"); Assert.Equal("Druck (psi)", w.TwinSensor);
        Press(w, "twin-export"); var csv = Get<TextBlock>(w, "twin-msg").Text!["Gespeichert: ".Length..]; Assert.True(File.Exists(csv));
        Press(w, "twin-svg"); System.Xml.Linq.XDocument.Load(Get<TextBlock>(w, "twin-msg").Text!["Gespeichert: ".Length..]);
        CloseToasts(w); // Hinweise „Ordner öffnen“ wie ein Nutzer wegklicken
        Press(w, "twin-del"); Assert.StartsWith("⚠ Bitte den Sensornamen", Get<TextBlock>(w, "twin-msg").Text);
        Get<TextBox>(w, "twin-del-confirm").Text = "Druck (psi)"; Press(w, "twin-del");
        Assert.DoesNotContain(w.Twin.Model.Assets.First(x => x.Name == "Werkstatt").Sensors, s => s.Name == "Druck (psi)");
        // Diesen PC messen und Live
        Press(w, "twin-pc"); Assert.Equal("Dieser PC", w.TwinAsset);
        Assert.Contains("Trendo-Arbeitsspeicher: Ok", Get<TextBlock>(w, "twin-Dieser_PC-Trendo-Arbeitsspeicher").Text); // 400 MB < Warnung 1500
        Press(w, "twin-live"); Assert.True(w.TwinLive); Assert.Contains("stoppen", Get<Button>(w, "twin-live").Content?.ToString());
        Press(w, "twin-live"); Assert.False(w.TwinLive);
        w.Close();
    }
}
