using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Trendo.App;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>CORE-05 / Masterprompt 131: 3D-Modelle erstellen hat eine echte Arbeitsfläche – Formen einsetzen, mit der Maus auswählen und verschieben, Tastatur, Rückgängig.</summary>
public class Studio3DViewport_Ui
{
    static CadView Vp(MainWindow w) { Settle(); return V<CadView>(w, "3d-viewport"); }
    static Point At(MainWindow w, int part) { var vp = Vp(w); vp.InvalidateVisual(); Settle(); var p = vp.ScreenOf(part) ?? throw new Xunit.Sdk.XunitException($"Teil {part} nicht sichtbar"); return vp.TranslatePoint(p, w)!.Value; }
    static void Key1(MainWindow w, Key k, PhysicalKey ph, RawInputModifiers m = RawInputModifiers.None) { w.KeyPress(k, m, ph, null); w.KeyRelease(k, m, ph, null); Settle(); }

    [AvaloniaFact(DisplayName = "3D-Arbeitsfläche: Formen einsetzen (auf dem Boden), Teil anklicken, mit der Maus verschieben (Raster), Pfeiltasten, Verdoppeln, Entf, Rückgängig")]
    public void Viewport_edit()
    {
        var (w, _, _) = Ui.Open();
        w.Show("3d"); Settle();
        RealClick(w, V<Button>(w, "3d-new-cube")); RealClick(w, V<Button>(w, "3d-new-sphere"));
        Assert.Equal(2, w.Studio3DParts.Count); Assert.Equal(2, Vp(w).Parts.Count);
        Assert.Equal(w.Studio3DParts[1].Id, w.Studio3DSelected); // neu eingesetzt = ausgewählt
        Assert.True(w.Studio3DParts[1].X > w.Studio3DParts[0].X); // daneben, nicht übereinander
        // Würfel anklicken → ausgewählt
        var cube = At(w, 0); w.MouseDown(cube, MouseButton.Left); w.MouseUp(cube, MouseButton.Left); Settle();
        Assert.Equal(w.Studio3DParts[0].Id, w.Studio3DSelected); Assert.True(Vp(w).Parts[0].Selected);
        Assert.Contains("Würfel", V<TextBlock>(w, "3d-vp-info").Text);
        // ziehen verschiebt den Würfel (auf 0,5 cm gerundet), die Kugel bleibt
        var (x0, y0, sphereX) = (w.Studio3DParts[0].X, w.Studio3DParts[0].Y, w.Studio3DParts[1].X);
        cube = At(w, 0); w.MouseDown(cube, MouseButton.Left);
        for (var i = 1; i <= 6; i++) { w.MouseMove(cube + new Point(-12 * i, 0)); Settle(); }
        w.MouseUp(cube + new Point(-72, 0), MouseButton.Left); Settle();
        var moved = w.Studio3DParts[0];
        Assert.True(Math.Abs(moved.X - x0) + Math.Abs(moved.Y - y0) > 3, $"nicht bewegt: {moved.X}/{moved.Y}");
        Assert.Equal(0, moved.X * 2 % 1, 6); Assert.Equal(sphereX, w.Studio3DParts[1].X);
        Assert.Contains("verschoben", ((TextBlock)w.Find<Border>("w45-msg").Child!).Text);
        // ziehen daneben dreht nur die Ansicht
        var yaw = Vp(w).Yaw; var empty = new Point(Vp(w).TranslatePoint(new Point(15, 0), w)!.Value.X, At(w, 0).Y); // links am Rand, auf Höhe der Teile
        var vp0 = Vp(w); var hitTest = w.InputHitTest(empty);
        w.MouseDown(empty, MouseButton.Left); w.MouseMove(empty + new Point(30, 0)); w.MouseMove(empty + new Point(60, 0)); w.MouseUp(empty + new Point(60, 0), MouseButton.Left); Settle();
        Assert.True(yaw != Vp(w).Yaw, $"Ansicht nicht gedreht – Treffer: {(hitTest as Control)?.GetType().Name} {(hitTest as Control)?.Name}, gleiche Ansicht: {ReferenceEquals(vp0, Vp(w))}, yaw {vp0.Yaw}"); Assert.Equal(moved.X, w.Studio3DParts[0].X);
        // Tastatur: Pfeil rechts = +1 cm, Umschalt = 10 cm, Strg+D verdoppelt, Entf löscht
        Vp(w).Focus(); var x1 = w.Studio3DParts[0].X;
        Key1(w, Key.Right, PhysicalKey.ArrowRight); Assert.Equal(x1 + 1, w.Studio3DParts[0].X, 6);
        Vp(w).Focus(); Key1(w, Key.Right, PhysicalKey.ArrowRight, RawInputModifiers.Shift); Assert.Equal(x1 + 11, w.Studio3DParts[0].X, 6);
        Vp(w).Focus(); Key1(w, Key.D, PhysicalKey.D, RawInputModifiers.Control); Assert.Equal(3, w.Studio3DParts.Count); Assert.Contains("Kopie", w.Studio3DParts[2].Name);
        Vp(w).Focus(); Key1(w, Key.Delete, PhysicalKey.Delete); Assert.Equal(2, w.Studio3DParts.Count);
        RealClick(w, V<Button>(w, "3d-undo")); Assert.Equal(3, w.Studio3DParts.Count);
        // Ins Leere klicken hebt die Auswahl auf
        var none = new Point(Vp(w).TranslatePoint(new Point(15, 0), w)!.Value.X, At(w, 0).Y); w.MouseDown(none, MouseButton.Left); w.MouseUp(none, MouseButton.Left); Settle();
        Assert.Null(w.Studio3DSelected);
    }

    [AvaloniaFact(DisplayName = "3D-Arbeitsfläche: „Auf den Boden“ setzt die Unterkante auf 0, Text und Ebene stehen auf dem Boden")]
    public void Drop_to_floor()
    {
        var (w, _, _) = Ui.Open();
        w.Show("3d"); Settle();
        foreach (var s in new[] { "text", "plane", "cone" }) RealClick(w, V<Button>(w, "3d-new-" + s));
        foreach (var p in Vp(w).Parts) Assert.InRange(p.Solid.Bounds().Min.Z, -0.01, 0.01);
        w.Studio3DMove(2, new Trendo.Core.Vec3(0, 0, 12)); Assert.InRange(Vp(w).Parts[2].Solid.Bounds().Min.Z, 11.9, 12.1);
        RealClick(w, V<Button>(w, "3d-drop-floor")); Assert.InRange(Vp(w).Parts[2].Solid.Bounds().Min.Z, -0.01, 0.01);
    }
}
