using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>S3 (Modernisierung 07.11.2026): VR-Raumvorschau mit Brillen-Ansicht – beide Augen mit Augenabstand und Sichtfeld des Zielgeräts.</summary>
public class XrHeadset_Ui
{
    static void Pump() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    [AvaloniaFact(DisplayName = "S3 VR: Brillen-Ansicht zeigt linkes und rechtes Auge (±3,15 cm, Sichtfeld der Brille), Ziehen dreht beide; Handy-AR hat keine Brillen-Ansicht")]
    public void Headset_view()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.Show("xr"); Pump();
            w.Find<Button>("xr-sample").Press(); Pump();
            w.XrDevice = 0; w.Show("xr"); Pump();
            Assert.Null(w.TryFind<MeshView>("xr-eye-left"));
            w.Find<Button>("xr-hmd").Press(); Pump();
            var l = w.Find<MeshView>("xr-eye-left"); var r = w.Find<MeshView>("xr-eye-right");
            Assert.Equal(-3.15, l.EyeOffsetCm, 2); Assert.Equal(3.15, r.EyeOffsetCm, 2);
            Assert.Equal(96, l.FovDeg); Assert.Equal(XrPro.EyeCm, l.EyeHeightCm);
            Assert.Contains("110° × 96°", w.Find<TextBlock>("xr-hmd-info").Text);
            Assert.NotEqual(l.Screen(new Vec3(0, 0, 50)).X, r.Screen(new Vec3(0, 0, 50)).X); // die Augen sehen leicht versetzt
            w.XrDevice = 1; w.Show("xr"); Pump();
            Assert.Equal(93, w.Find<MeshView>("xr-eye-left").FovDeg); // Sichtfeld folgt dem Zielgerät
            w.XrDevice = 2; w.Show("xr"); Pump();
            Assert.Null(w.TryFind<Button>("xr-hmd")); Assert.Null(w.TryFind<MeshView>("xr-eye-left")); // Handy-AR: keine Brille
            Assert.NotNull(w.Find<MeshView>("xr-view"));
        }
        finally { w.XrHeadset = false; w.XrDevice = 0; w.Close(); }
    }
}
