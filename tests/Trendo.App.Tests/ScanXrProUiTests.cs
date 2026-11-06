using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>VR & AR (Profi 3 + Claude 3) und Einscannen (Profi 4 + Claude 4) in der Oberfläche.</summary>
public class ScanXrPro_Ui
{
    static void Pump() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    [AvaloniaFact(DisplayName = "SPEC-04 VR: Vorschau aus Augenhöhe schaltbar (1,60 m), Anzeige der größten Textur, Karte „Für die Brille reparieren“; passt das Modell, gibt es nichts zu reparieren")]
    public async Task Xr_eye_and_fix()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.Show("xr"); Pump();
            w.Find<Button>("xr-sample").Press(); Pump();
            Assert.Null(w.Find<MeshView>("xr-view").EyeHeightCm);
            w.Find<Button>("xr-eye").Press(); Pump();
            Assert.Equal(160, w.Find<MeshView>("xr-view").EyeHeightCm);
            Assert.NotNull(w.TryFind<StackPanel>("xr-fix-card"));
            var called = false; w.XrFixOverride = (_, _, _) => { called = true; return Task.FromResult(true); };
            await w.XrFix(); Pump();
            Assert.False(called); // das Beispiel passt schon – Blender wird nicht bemüht
            Assert.Contains("Nichts zu reparieren", w.Find<TextBlock>("xr-msg").Text);
            w.Find<Button>("xr-eye").Press(); Pump(); Assert.Null(w.Find<MeshView>("xr-view").EyeHeightCm);
        }
        finally { w.XrFixOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "SPEC-09 Scan: Kameras als Marker, beschneiden, Reste entfernen, Löcher schließen, echte Größe direkt und über Kreditkarte, Rückgängig; gute Fotos auswählen und als Kopie verwenden; Texturqualität wählbar")]
    public void Scan_tools()
    {
        var (w, _, root) = Ui.Open();
        try
        {
            // Ergebnis eines Scans: Würfel mit Krümel und ein fehlendes Bodenquadrat, dazu zwei Kameras
            var work = Path.Combine(root, "scan-test"); Directory.CreateDirectory(work);
            var m = new Mesh(); Vec3 P(double x, double y, double z) => new(x * 20, y * 20, z * 20);
            void Q(Vec3 a, Vec3 b, Vec3 c, Vec3 d) { m.Add(a, b, c); m.Add(a, c, d); }
            Q(P(0, 0, 1), P(1, 0, 1), P(1, 1, 1), P(0, 1, 1)); Q(P(0, 0, 0), P(1, 0, 0), P(1, 0, 1), P(0, 0, 1)); Q(P(0, 1, 0), P(0, 1, 1), P(1, 1, 1), P(1, 1, 0));
            Q(P(0, 0, 0), P(0, 0, 1), P(0, 1, 1), P(0, 1, 0)); Q(P(1, 0, 0), P(1, 1, 0), P(1, 1, 1), P(1, 0, 1));
            m.Add(new(60, 60, 60), new(61, 60, 60), new(60, 61, 60)); // Krümel
            var obj = Path.Combine(work, "modell.obj"); Mesh3D.WriteObj(m, obj);
            File.WriteAllText(Path.Combine(work, "kameras.json"), """[{"name":"a.jpg","x":0,"y":-40,"z":10},{"name":"b.jpg","x":40,"y":0,"z":10}]""");
            w.Show("scan3d"); w.ScanSetModel(obj); Pump();
            Assert.Equal(2, w.Find<MeshView>("scan-view").Markers!.Count);
            w.Find<Button>("scan-largest").Press(); Pump();
            Assert.Equal(10, Mesh3D.ReadMeshFile(obj).Triangles);
            w.Find<Button>("scan-holes").Press(); Pump();
            Assert.Equal(14, Mesh3D.ReadMeshFile(obj).Triangles); // Boden mit vier Dreiecken geschlossen
            w.Find<Button>("scan-undo").Press(); Pump(); Assert.Equal(10, Mesh3D.ReadMeshFile(obj).Triangles);
            w.Find<TextBox>("scan-size-cm").Text = "45"; w.Find<ComboBox>("scan-size-axis").SelectedIndex = 3; w.Find<Button>("scan-size-set").Press(); Pump();
            var (lo, hi) = ScanPro.Bounds(Mesh3D.ReadMeshFile(obj)); Assert.Equal(45, hi.Z - lo.Z, 3);
            Assert.Null(w.Find<MeshView>("scan-view").Markers); // Kameras im alten Maßstab werden ausgeblendet
            w.Find<TextBox>("scan-ref-measured").Text = "4"; w.Find<Button>("scan-ref-set").Press(); Pump(); // Kreditkarte: 4 cm im Modell = 8,56 cm echt
            (lo, hi) = ScanPro.Bounds(Mesh3D.ReadMeshFile(obj)); Assert.Equal(45 * 8.56 / 4, hi.Z - lo.Z, 2);
            w.Find<Slider>("scan-crop-bottom").Value = 40; w.Find<Button>("scan-crop").Press(); Pump();
            Assert.True(Mesh3D.ReadMeshFile(obj).Triangles < 10);
            Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "scan.bearbeitet");
            // Fotos: ein doppeltes wird aussortiert, die guten werden als Kopie verwendet
            var photos = Path.Combine(root, "fotos"); Directory.CreateDirectory(photos);
            for (var i = 0; i < 5; i++) File.WriteAllBytes(Path.Combine(photos, $"f{i}.jpg"), new byte[200_000 + i * 30_000]);
            File.WriteAllBytes(Path.Combine(photos, "f5.jpg"), new byte[320_000 + 10]); // fast gleich wie f4
            w.ScanLoad(photos); Pump();
            w.Find<Button>("scan-rank").Press(); Pump();
            Assert.Single(w.ScanPhotoRanking!, r => !r.Keep);
            w.Find<Button>("scan-use-good").Press(); Pump();
            Assert.Equal(5, w.PhotoReport!.Files.Count); Assert.True(File.Exists(Path.Combine(photos, "f5.jpg"))); // Original bleibt
            w.Find<ComboBox>("scan-texture").SelectedIndex = 3; Assert.Equal(8192, w.ScanTextureSide);
        }
        finally { w.Close(); }
    }
}
