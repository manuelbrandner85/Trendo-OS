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

/// <summary>T-0269: Programm „Modell-Prüfer“ über den echten Eingabeweg – Beispielmodell, Drehen mit der Maus, Zielgerät, Ampel bei zu großem Modell, Bericht, Rechner.</summary>
public class SPEC04_Ui
{
    static T Get<T>(MainWindow w, string name) where T : Control { Settle(); return w.GetVisualDescendants().OfType<T>().Last(c => c.Name == name && c.IsEffectivelyVisible && c.Bounds.Width > 0); }
    static T? Try<T>(MainWindow w, string name) where T : Control { Settle(); return w.GetVisualDescendants().OfType<T>().LastOrDefault(c => c.Name == name && c.IsEffectivelyVisible && c.Bounds.Width > 0); }
    static void Press(MainWindow w, string name) { var b = Get<Button>(w, name); b.BringIntoView(); Settle(); RealClick(w, Get<Button>(w, name)); }
    static void Key1(MainWindow w, Key k, PhysicalKey ph) { w.KeyPress(k, RawInputModifiers.None, ph, null); w.KeyRelease(k, RawInputModifiers.None, ph, null); Settle(); }
    static void Retype(MainWindow w, string name, string text) { var tb = Get<TextBox>(w, name); tb.BringIntoView(); RealClick(w, tb); tb.Focus(); tb.SelectAll(); Settle(); Key1(w, Key.Back, PhysicalKey.Backspace); foreach (var ch in text) { w.KeyTextInput(ch.ToString()); Settle(); } }
    static void CloseToasts(MainWindow w) { while (Try<Button>(w, "toast-close") is { } c) RealClick(w, c); }

    [AvaloniaFact(DisplayName = "SPEC-04 UI: Beispielmodell laden, mit der Maus drehen, Ansichten, Maßstab plausibel, alle Geräte grün, Bericht speichern")]
    public void Sample_orbit_report()
    {
        var (w, _, _) = Ui.Open();
        w.Show(MainWindow.StudioPages["SPEC-04"]); Settle();
        Assert.Null(Try<Button>(w, "xr-check-run")); // kein Formular mehr
        Press(w, "xr-sample");
        var view = Get<MeshView>(w, "xr-view"); Assert.Equal(XrSample.Build().Triangles, view.Model!.Triangles); Assert.True(view.Model.Triangles > 3000); // echte Möbel statt Kisten
        var c = view.TranslatePoint(new Point(view.Bounds.Width / 2, view.Bounds.Height / 2), w)!.Value; var yaw = view.Yaw;
        w.MouseDown(c, MouseButton.Left); w.MouseMove(c + new Point(80, 10)); w.MouseUp(c + new Point(80, 10), MouseButton.Left); Settle();
        Assert.NotEqual(yaw, view.Yaw);
        Press(w, "xr-v-oben"); Assert.Equal(89, Get<MeshView>(w, "xr-view").Pitch);
        Press(w, "xr-wire"); Assert.True(Get<MeshView>(w, "xr-view").Wire);
        Assert.Contains("plausibel", Get<TextBlock>(w, "xr-size").Text); // 2,00 × 1,15 × 1,40 m
        for (var k = 0; k < 4; k++) Assert.StartsWith("✓", Get<TextBlock>(w, $"xr-check-{k}").Text);
        Press(w, "xr-report"); var rep = Get<TextBlock>(w, "xr-msg").Text!["Gespeichert: ".Length..]; Assert.Contains("Dreiecke", File.ReadAllText(rep));
        CloseToasts(w);
        Assert.Equal(view.Model!.Triangles.ToString(), Get<TextBox>(w, "xr-lod-tris").Text); // Rechner mit dem Modell vorbelegt
        w.Close();
    }

    static Mesh FineSphere(int seg)
    {
        var m = new Mesh(); const double r = 15;
        Vec3 S(int a, int b) { var th = Math.PI * a / (seg / 2); var ph = 2 * Math.PI * b / seg; return new(r * Math.Sin(th) * Math.Cos(ph), r * Math.Sin(th) * Math.Sin(ph), r + r * Math.Cos(th)); }
        for (var i = 0; i < seg / 2; i++) for (var j = 0; j < seg; j++) { m.Add(S(i, j), S(i, j + 1), S(i + 1, j + 1)); m.Add(S(i, j), S(i + 1, j + 1), S(i + 1, j)); }
        return m;
    }

    [AvaloniaFact(DisplayName = "SPEC-04 UI: zu feines Modell wird für Handy-AR gelb, Zielgerät färbt die Balken; Datei öffnen, fehlende Datei; Komfort-Rechner")]
    public void Heavy_model_and_calcs()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        var f = Path.Combine(root, "fein.glb"); Mesh3D.WriteGlb(FineSphere(600), f); // Kugel mit 360 000 Dreiecken
        w.Show("xr"); Settle();
        Get<TextBox>(w, "xr-file-path").Text = f; Press(w, "xr-open");
        Assert.Equal(f, w.XrFile);
        Assert.StartsWith("✓", Get<TextBlock>(w, "xr-check-0").Text); Assert.StartsWith("⚠", Get<TextBlock>(w, "xr-check-2").Text); // Handy-AR: mehr als 300 000 Dreiecke
        Press(w, "xr-dev-2"); Assert.Equal(2, w.XrDevice);
        Get<TextBox>(w, "xr-file-path").Text = Path.Combine(root, "fehlt.glb"); Press(w, "xr-open");
        Assert.StartsWith("⚠", Get<TextBlock>(w, "xr-msg").Text); Assert.Equal(f, w.XrFile);
        Assert.Contains("Komfort", Get<SelectableTextBlock>(w, "xr-move-out").Text);
        Retype(w, "xr-move-turn", "200"); Assert.Contains("Schritt-Drehen", Get<SelectableTextBlock>(w, "xr-move-out").Text);
        Get<ComboBox>(w, "xr-move-snap").SelectedItem = "ja"; Settle(); Assert.DoesNotContain("Glattes Drehen", Get<SelectableTextBlock>(w, "xr-move-out").Text);
        Retype(w, "xr-frame-ms", "20"); Assert.StartsWith("⚠ Zu langsam", Get<SelectableTextBlock>(w, "xr-frame-out").Text);
        w.Close();
    }
}
