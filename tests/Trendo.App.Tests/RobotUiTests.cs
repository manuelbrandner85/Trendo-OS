using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Trendo.Core;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0270: Programm „Roboter-Werkstatt“ über den echten Eingabeweg – Ziel per Klick, Wegpunkte, Simulation, Greifbahn, CSV, Bild prüfen mit Fundstellen, Ziel aus dem Bild.</summary>
public class CORE09_Ui
{
    static T Get<T>(MainWindow w, string name) where T : Control { Settle(); return w.GetVisualDescendants().OfType<T>().Last(c => c.Name == name && c.IsEffectivelyVisible && c.Bounds.Width > 0); }
    static T? Try<T>(MainWindow w, string name) where T : Control { Settle(); return w.GetVisualDescendants().OfType<T>().LastOrDefault(c => c.Name == name && c.IsEffectivelyVisible && c.Bounds.Width > 0); }
    static void Press(MainWindow w, string name) { var b = Get<Button>(w, name); b.BringIntoView(); Settle(); RealClick(w, Get<Button>(w, name)); }
    static void CloseToasts(MainWindow w) { while (Try<Button>(w, "toast-close") is { } c) RealClick(w, c); }

    [AvaloniaFact(DisplayName = "CORE-09 UI: Klick in die Draufsicht setzt das Ziel, Wegpunkt hinzufügen/verschieben/löschen, Weg simulieren, Greifbahn, Programm-CSV")]
    public void Arm_plan()
    {
        var (w, _, _) = Ui.Open();
        w.Show(MainWindow.StudioPages["CORE-09"]); Settle();
        Assert.Null(Try<Button>(w, "rob-ik-run")); // kein Formular mehr
        var view = Get<ArmView>(w, "rob-arm-view");
        var at = view.TranslatePoint(view.TopPoint(100, 100), w)!.Value;
        w.MouseDown(at, MouseButton.Left); w.MouseUp(at, MouseButton.Left); Settle();
        Assert.InRange(double.Parse(w.Bench.X), 97, 103); Assert.InRange(double.Parse(w.Bench.Y), 97, 103);
        Assert.Contains("Ziel", Get<TextBlock>(w, "rob-msg").Text);
        Assert.Equal(double.Parse(w.Bench.X), Get<ArmView>(w, "rob-arm-view").Target!.Value.X);
        Press(w, "rob-pt-add"); Assert.Equal(4, w.Bench.Points.Split('|').Length);
        Press(w, "rob-pt-up-3"); Assert.StartsWith("1", w.Bench.Points.Split('|')[2].Trim()); // nach oben gerückt
        Press(w, "rob-pt-del-0"); Assert.Equal(3, w.Bench.Points.Split('|').Length);
        // außer Reichweite klicken → Meldung, Ziel bleibt
        view = Get<ArmView>(w, "rob-arm-view"); var far = view.TranslatePoint(view.TopPoint(180, 180), w)!.Value; var before = w.Bench.X;
        w.MouseDown(far, MouseButton.Left); w.MouseUp(far, MouseButton.Left); Settle();
        Assert.Contains("außer Reichweite", Get<TextBlock>(w, "rob-msg").Text); Assert.Equal(before, w.Bench.X);
        Press(w, "rob-sim"); Assert.True(w.RobSimulating);
        Press(w, "rob-sim"); Assert.False(w.RobSimulating);
        Press(w, "rob-pick"); Assert.True(w.Bench.Points.Split('|').Length >= 4); Assert.Contains("Greifbahn", Get<TextBlock>(w, "rob-msg").Text);
        Press(w, "rob-csv"); var f = Get<TextBlock>(w, "rob-msg").Text!.Split("gespeichert: ")[1]; Assert.StartsWith("Schritt;", File.ReadAllText(f));
        CloseToasts(w);
        Assert.EndsWith("µs", Get<SelectableTextBlock>(w, "rob-servo-out").Text);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "CORE-09 UI: Bild prüfen – zwei rote Teile werden markiert, Maske, Ziel aus dem Bild, Bewegung zwischen zwei Bildern")]
    public void Image_check()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        static RgbImage Img(int ox) { var rgb = new byte[200 * 150 * 3]; for (var i = 0; i < rgb.Length; i++) rgb[i] = 200; void Sq(int x0, int y0) { for (var y = y0; y < y0 + 20; y++) for (var x = x0; x < x0 + 20; x++) { var k = (y * 200 + x) * 3; rgb[k] = 220; rgb[k + 1] = 30; rgb[k + 2] = 30; } } Sq(30 + ox, 40); Sq(130, 90); return new(200, 150, rgb); }
        var a = Path.Combine(root, "tisch.png"); File.WriteAllBytes(a, VisionPlus.EncodePng(Img(0)));
        var b = Path.Combine(root, "tisch2.png"); File.WriteAllBytes(b, VisionPlus.EncodePng(Img(25)));
        w.Show("roboter"); Settle();
        Press(w, "rob-mode-bild");
        Get<TextBox>(w, "rob-img-path").Text = a; Press(w, "rob-img-open");
        Assert.StartsWith("2 ", Get<TextBlock>(w, "rob-img-found").Text);
        Assert.Equal(2, Get<Border>(w, "rob-img-view").GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Rectangle>().Count());
        RealClick(w, Get<CheckBox>(w, "rob-img-mask")); Assert.NotNull(Try<Border>(w, "rob-img-view"));
        Get<TextBox>(w, "rob-img2-path").Text = b; Press(w, "rob-img-motion"); Assert.StartsWith("Bewegung", Get<TextBlock>(w, "rob-msg").Text);
        Press(w, "rob-img-target");
        Assert.Equal("arm", w.RobMode); Assert.Contains("Ziel aus dem Bild", Get<TextBlock>(w, "rob-msg").Text);
        w.Close();
    }
}
