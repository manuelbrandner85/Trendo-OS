using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Trendo.App;
using Trendo.Core.Sys;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>Speicherplatz-Karte: Treemap-Kern und Oberfläche (einlesen, auswählen, hinein, Papierkorb nur nach Ja).</summary>
public class DiskMap_Ui
{
    [Fact(DisplayName = "Kachelkarte: Flächen proportional, lückenlos in der Fläche, Einlesen zählt Größen, Entfernen zieht Eltern nach")]
    public void Core()
    {
        var r = DiskMap.Squarify([600, 300, 100], new DiskMap.Rect(0, 0, 100, 100));
        Assert.Equal(6000, r[0].W * r[0].H, 1); Assert.Equal(3000, r[1].W * r[1].H, 1); Assert.Equal(1000, r[2].W * r[2].H, 1);
        Assert.All(r, x => Assert.True(x.X >= -1e-9 && x.Y >= -1e-9 && x.X + x.W <= 100 + 1e-6 && x.Y + x.H <= 100 + 1e-6));
        var dir = Path.Combine(Path.GetTempPath(), "trendo-disk-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(dir, "Videos")); File.WriteAllBytes(Path.Combine(dir, "Videos", "film.mp4"), new byte[50_000]); File.WriteAllBytes(Path.Combine(dir, "brief.pdf"), new byte[1_000]);
            var root = DiskMap.Scan(dir);
            Assert.Equal(51_000, root.Bytes); Assert.Equal(2, root.Files); Assert.Equal("Videos", root.Children[0].Name); // größte zuerst
            Assert.Equal("film.mp4", DiskMap.Largest(root, 1)[0].Name); Assert.Equal("Video", DiskMap.Kind("film.mp4"));
            DiskMap.Remove(root.Children[0]); Assert.Equal(1_000, root.Bytes); Assert.Equal(1, root.Files);
        }
        finally { Directory.Delete(dir, true); }
    }

    [AvaloniaFact(DisplayName = "Speicherplatz-Karte UI: Ordner einlesen, Kachel anklicken wählt, zweiter Klick geht hinein, Papierkorb erst nach Ja")]
    public async Task Map_page()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1300;
        var dir = Path.Combine(root, "platte"); Directory.CreateDirectory(Path.Combine(dir, "Fotos")); Directory.CreateDirectory(Path.Combine(dir, "Alt"));
        for (var i = 0; i < 3; i++) File.WriteAllBytes(Path.Combine(dir, "Fotos", $"bild{i}.jpg"), new byte[40_000]);
        File.WriteAllBytes(Path.Combine(dir, "Alt", "backup.zip"), new byte[30_000]);
        var trashed = new List<string>(); w.TrashOverride = trashed.Add;
        await w.DiskScanAsync(dir); w.Show("ressourcen"); Settle();
        var map = w.DiskViewForTests!; map.BringIntoView(); Settle();
        var fotos = w.DiskAt!.Children.First(c => c.Name == "Fotos");
        var r = map.BoxOf(fotos)!.Value; var p = map.TranslatePoint(r.Center, w)!.Value;
        w.MouseDown(p, MouseButton.Left); w.MouseUp(p, MouseButton.Left); Settle();
        Assert.Equal("Fotos", w.DiskSelected!.Name);
        map = w.DiskViewForTests!; map.BringIntoView(); Settle(); p = map.TranslatePoint(map.BoxOf(w.DiskSelected!)!.Value.Center, w)!.Value;
        w.MouseDown(p, MouseButton.Left); w.MouseUp(p, MouseButton.Left); Settle();
        Assert.Equal("Fotos", w.DiskAt!.Name); // hinein
        // zurück über den Pfad, „Alt“ wählen, Papierkorb: erst fragen, dann Ja
        var up = V<Button>(w, "disk-up-0"); up.BringIntoView(); Settle(); RealClick(w, V<Button>(w, "disk-up-0"));
        w.DiskSelect(w.DiskAt!.Children.First(c => c.Name == "Alt")); Settle();
        var t = V<Button>(w, "disk-trash"); t.BringIntoView(); Settle(); RealClick(w, V<Button>(w, "disk-trash"));
        Assert.Empty(trashed); Assert.NotNull(w.TryFind<Border>("disk-confirm"));
        var yes = w.Find<Button>("disk-trash-yes"); yes.BringIntoView(); Settle(); RealClick(w, w.Find<Button>("disk-trash-yes"));
        Assert.Equal([Path.Combine(dir, "Alt")], trashed);
        Assert.Equal(120_000, w.DiskAt!.Bytes); Assert.Contains("Papierkorb", w.Find<TextBlock>("disk-msg").Text);
    }
}
