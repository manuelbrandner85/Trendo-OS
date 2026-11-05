using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>CORE-05 Profi-Ausbau 2: Arbeitsschritte, Anfasser, Szenenbaum mit Gruppen, PBR-Material, Licht, Kamera, Render mit Vorher/Nachher.</summary>
public class Studio3DPro_Ui
{
    static CadView Vp(MainWindow w) { Settle(); var v = V<CadView>(w, "3d-viewport"); v.InvalidateVisual(); Settle(); return v; }

    [AvaloniaFact(DisplayName = "CORE-05: Anfasser – Verschieben, Drehen, Skalieren entlang einer Achse (Raster), Tasten G/R/S wechseln die Art, ein Schritt im Rückgängig")]
    public void Gizmo()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.Show("3d"); Settle();
            RealClick(w, V<Button>(w, "3d-new-cube"));
            var p = w.Studio3DParts[0]; var (x0, sx0) = (p.X, p.Sx);
            var vp = Vp(w);
            Assert.Equal("move", vp.GizmoMode); Assert.NotNull(vp.GizmoAt);
            Assert.NotNull(vp.HandleEnd('x')); Assert.Equal('x', vp.HandleAt(vp.HandleEnd('x')!.Value));
            vp.DragHandle('x', new Point(vp.HandleEnd('x')!.Value.X - vp.GizmoAtScreen().X, vp.HandleEnd('x')!.Value.Y - vp.GizmoAtScreen().Y)); // eine Grifflänge entlang X
            Assert.True(w.Studio3DParts[0].X > x0 + 1, $"{x0} → {w.Studio3DParts[0].X}"); Assert.Equal(0, w.Studio3DParts[0].X * 2 % 1, 6); // 0,5-cm-Raster
            Assert.Equal(p.Y, w.Studio3DParts[0].Y); // nur entlang X
            w.Find<Button>("3d-gizmo-rotate").Press(); Settle(); vp = Vp(w); Assert.Equal("rotate", vp.GizmoMode);
            vp.DragHandle('z', new Point(50, 0)); Assert.Equal(30, w.Studio3DParts[0].Rz); // 50 px × 0,6 = 30°
            w.Find<Button>("3d-gizmo-scale").Press(); Settle(); vp = Vp(w);
            vp.DragHandle('x', new Point(vp.HandleEnd('x')!.Value.X - vp.GizmoAtScreen().X, vp.HandleEnd('x')!.Value.Y - vp.GizmoAtScreen().Y));
            Assert.Equal(sx0 * 2, w.Studio3DParts[0].Sx, 1); // doppelte Grifflänge = × 2
            w.Find<Button>("3d-undo").Press(); Settle(); Assert.Equal(sx0, w.Studio3DParts[0].Sx, 1);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "CORE-05: Szenenbaum – Teile in Gruppen ordnen, Gruppe auswählen bewegt alle ihre Teile, Ausblenden nimmt Teile aus Ansicht und Render, Material für die ganze Gruppe")]
    public void Tree_groups_material()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.Show("3d"); Settle();
            RealClick(w, V<Button>(w, "3d-new-cube")); RealClick(w, V<Button>(w, "3d-new-cylinder")); RealClick(w, V<Button>(w, "3d-new-sphere"));
            w.Studio3DSelect(0); w.Studio3DSetGroup("Lampe"); w.Studio3DSelect(1); w.Studio3DSetGroup("Lampe");
            Settle(); Assert.NotNull(w.TryFind<Button>("3d-group-lampe"));
            w.Find<Button>("3d-group-lampe").Press(); Settle(); Assert.Equal("Lampe", w.Studio3DGroup);
            var (a, b, c) = (w.Studio3DParts[0].Z, w.Studio3DParts[1].Z, w.Studio3DParts[2].Z);
            w.Studio3DGizmo('z', "move", 5);
            Assert.Equal((a + 5, b + 5, c), (w.Studio3DParts[0].Z, w.Studio3DParts[1].Z, w.Studio3DParts[2].Z));
            w.Studio3DStep = "material"; w.Show("3d"); Settle();
            w.Find<Button>("3d-mat-chrom").Press(); Settle();
            Assert.Equal(("chrom", "chrom", null), (w.Studio3DParts[0].Material, w.Studio3DParts[1].Material, w.Studio3DParts[2].Material));
            Assert.Equal(1, Vp(w).Looks![0].Metallic);
            w.Find<Button>("3d-eye-2").Press(); Settle();
            Assert.True(w.Studio3DParts[2].Hidden); Assert.Equal(2, Vp(w).Parts.Count);
            Assert.True(Studio3DPro.Progress(new Scene3D { Parts = [.. w.Studio3DParts] }, 0)[2].Done); // die ausgeblendete Kugel ohne Material zählt nicht
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "CORE-05: Arbeitsschritte – Licht und Kamera wählen, Render (nachgebildet) landet im Verlauf, zwei Renders zeigen Vorher/Nachher mit Regler; alle Schritte erledigt")]
    public async Task Steps_light_camera_render()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.Show("3d"); Settle();
            RealClick(w, V<Button>(w, "3d-new-cube"));
            Assert.NotNull(w.TryFind<WrapPanel>("3d-steps"));
            w.Find<Button>("3d-step-licht").Press(); Settle(); Assert.Equal("licht", w.Studio3DStep);
            w.Find<Button>("3d-light-abend").Press(); Settle();
            w.Find<Button>("3d-step-kamera").Press(); Settle(); w.Find<Button>("3d-lens-85").Press(); Settle();
            w.Find<Button>("3d-step-material").Press(); Settle(); w.Studio3DSelect(0); w.Find<Button>("3d-mat-holz").Press(); Settle();
            var specs = new List<string>();
            w.Render3DOverride = (spec, png) => { specs.Add(File.ReadAllText(spec)); using (var bmp = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(64, 48))) bmp.Save(png); return Task.FromResult(true); };
            await w.Studio3DRender(); Settle();
            Assert.Contains("\"light\":\"abend\"", specs[0]); Assert.Contains("\"cameraLens\":85", specs[0]); Assert.Contains("\"material\":\"holz\"", specs[0]);
            Assert.NotNull(w.TryFind<Image>("3d-render-now"));
            await Task.Delay(1100); await w.Studio3DRender(); w.Show("3d"); Settle();
            Assert.NotNull(w.TryFind<Grid>("3d-compare"));
            var slider = V<Slider>(w, "3d-compare-slider"); slider.Value = 0.8; Settle();
            Assert.Equal(0.8, w.Studio3DCompare, 3);
            Assert.All(new[] { "modell", "uv", "material", "licht", "kamera", "render" }, s => Assert.StartsWith("✓", (string)V<Button>(w, "3d-step-" + s).Content!));
            Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "3d.render");
        }
        finally { w.Render3DOverride = null; w.Close(); }
    }
}
