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

/// <summary>T-0269 / T-0722: Programm „Modell-Prüfer“ unter „3D-Modelle ansehen“ über den echten Eingabeweg – Beispielmodell, Drehen mit der Maus, Maße, Fehler im Modell, Bericht, Rechner.
/// T-0722: Zielgeräte, Bildzeit und Komfort-Rechner (VR) sind entfernt; die Tests prüfen, dass sie nicht mehr erscheinen.</summary>
public class SPEC04_Ui
{
    static T Get<T>(MainWindow w, string name) where T : Control { Settle(); return w.GetVisualDescendants().OfType<T>().Last(c => c.Name == name && c.IsEffectivelyVisible && c.Bounds.Width > 0); }
    static T? Try<T>(MainWindow w, string name) where T : Control { Settle(); return w.GetVisualDescendants().OfType<T>().LastOrDefault(c => c.Name == name && c.IsEffectivelyVisible && c.Bounds.Width > 0); }
    static void Press(MainWindow w, string name) { var b = Get<Button>(w, name); b.BringIntoView(); Settle(); RealClick(w, Get<Button>(w, name)); }
    static void Key1(MainWindow w, Key k, PhysicalKey ph) { w.KeyPress(k, RawInputModifiers.None, ph, null); w.KeyRelease(k, RawInputModifiers.None, ph, null); Settle(); }
    static void Retype(MainWindow w, string name, string text) { var tb = Get<TextBox>(w, name); tb.BringIntoView(); RealClick(w, tb); tb.Focus(); tb.SelectAll(); Settle(); Key1(w, Key.Back, PhysicalKey.Backspace); foreach (var ch in text) { w.KeyTextInput(ch.ToString()); Settle(); } }
    static void CloseToasts(MainWindow w) { while (Try<Button>(w, "toast-close") is { } c) RealClick(w, c); }

    [AvaloniaFact(DisplayName = "SPEC-04 UI: Beispielmodell laden, mit der Maus drehen, Ansichten, Maßstab plausibel, Maße und keine Fehler, Bericht speichern")]
    public void Sample_orbit_report()
    {
        var (w, _, _) = Ui.Open();
        w.Show(MainWindow.StudioPages["SPEC-04"]); Settle();
        Assert.Null(Try<Button>(w, "xr-check-run")); // kein Formular mehr
        // T-0722: neutral benannt und über die Suche auffindbar, ohne VR-Suchwörter
        var page = MainWindow.ExtraPages.Single(p => p.Key == "xr");
        Assert.Equal("3D-Modelle ansehen", page.Title); Assert.Contains("modell ansehen", page.Words);
        Assert.DoesNotContain(page.Words, x => x is "vr" or "ar" or "brille" or "quest");
        Assert.Equal("3D-Modelle ansehen", Get<TextBlock>(w, "page-title").Text);
        Press(w, "xr-sample");
        var view = Get<MeshView>(w, "xr-view"); Assert.Equal(XrSample.Build().Triangles, view.Model!.Triangles); Assert.True(view.Model.Triangles > 3000); // echte Möbel statt Kisten
        var c = view.TranslatePoint(new Point(view.Bounds.Width / 2, view.Bounds.Height / 2), w)!.Value; var yaw = view.Yaw;
        w.MouseDown(c, MouseButton.Left); w.MouseMove(c + new Point(80, 10)); w.MouseUp(c + new Point(80, 10), MouseButton.Left); Settle();
        Assert.NotEqual(yaw, view.Yaw);
        Press(w, "xr-v-oben"); Assert.Equal(89, Get<MeshView>(w, "xr-view").Pitch);
        Press(w, "xr-wire"); Assert.True(Get<MeshView>(w, "xr-view").Wire);
        Assert.Contains("plausibel", Get<TextBlock>(w, "xr-size").Text); // 2,00 × 1,15 × 1,40 m
        Assert.StartsWith("✓", Get<TextBlock>(w, "xr-problem-0").Text); // keine Fehler im Modell
        Assert.Contains("cm", Get<TextBlock>(w, "xr-m-dims-val").Text);
        Assert.Null(Try<TextBlock>(w, "xr-check-0")); Assert.Null(Try<Button>(w, "xr-dev-0")); Assert.Null(Try<Button>(w, "xr-hmd")); // T-0722: keine Zielbrillen mehr
        Press(w, "xr-report"); var rep = Get<TextBlock>(w, "xr-msg").Text!["Gespeichert: ".Length..]; Assert.Contains("Dreiecke", File.ReadAllText(rep)); Assert.DoesNotContain("Quest", File.ReadAllText(rep));
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

    [AvaloniaFact(DisplayName = "SPEC-04 UI: feines Modell über „Datei wählen“ öffnen (ein Klick liest ein), Dreiecke und Maße stehen da; fehlende Datei meldet einen Fehler; keine Bildzeit und kein Komfort-Rechner mehr")]
    public void Heavy_model_and_calcs()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        var f = Path.Combine(root, "fein.glb"); Mesh3D.WriteGlb(FineSphere(600), f); // Kugel mit 360 000 Dreiecken
        // T-0722, Ursache des roten Tests: seit T-0700 (V-07) sind Pfadfeld und „Öffnen“ außerhalb von „Professionell“ ausgeblendet – man wählt
        // die Datei mit „Datei wählen …“, und der Folgeknopf liest sofort ein. Der Test tippte noch ins verborgene Feld.
        var pick = f; w.PickOverride = _ => Task.FromResult<string[]?>([pick]);
        w.Show("xr"); Settle();
        Press(w, "xr-file-path-pick");
        Assert.Equal(f, w.XrFile);
        var tris = XrInspector.Inspect(f).Triangles; Assert.True(tris > 300_000); Assert.Equal(Fach.F(tris, 0), Get<TextBlock>(w, "xr-m-tris-val").Text);
        Assert.Equal("30,0 × 30,0 × 30,0 cm", Get<TextBlock>(w, "xr-m-dims-val").Text);
        pick = Path.Combine(root, "fehlt.glb"); Press(w, "xr-file-path-pick");
        Assert.StartsWith("⚠", Get<TextBlock>(w, "xr-msg").Text); Assert.Equal(f, w.XrFile);
        Assert.Null(Try<SelectableTextBlock>(w, "xr-move-out")); Assert.Null(Try<SelectableTextBlock>(w, "xr-frame-out")); // T-0722: VR-Rechner entfernt
        Retype(w, "xr-lod-size", "4"); Assert.Contains("Stufe 3", Get<SelectableTextBlock>(w, "xr-lod-out").Text);
        w.PickOverride = null; w.Close();
    }
}
