using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using SkiaSharp;
using Trendo.App;
using Xunit;
namespace Trendo.App.Tests;

public class Sys02_Alltag_Ui
{
    static string Png(string dir, int w = 40, int h = 20)
    {
        var p = Path.Combine(dir, "bild.png");
        using var b = new SKBitmap(w, h); using (var c = new SKCanvas(b)) c.Clear(new SKColor(47, 134, 246));
        using var img = SKImage.FromBitmap(b); using var data = img.Encode(SKEncodedImageFormat.Png, 100); File.WriteAllBytes(p, data.ToArray()); return p;
    }

    [Fact(DisplayName = "SYS-02-P06/P10/P18: Drehen, Verkleinern, Schwärzen (Pixel überschrieben), Pipette – Original bleibt")]
    public void Images()
    {
        var d = Path.Combine(Path.GetTempPath(), "trendo-img-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d);
        var f = Png(d); var sha = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(f)));
        Assert.Equal((20, 40), ImageTools.Size(ImageTools.Rotate(f, 90)));
        Assert.Equal((20, 10), ImageTools.Size(ImageTools.Resize(f, 20)));
        var r = ImageTools.Redact(f, [(0, 0, 10, 10)]);
        Assert.Equal((0, 0, 0), ImageTools.ColorAt(r, 5, 5)); Assert.Equal((47, 134, 246), ImageTools.ColorAt(r, 30, 15));
        Assert.Equal((47, 134, 246), ImageTools.ColorAt(f, 5, 5));
        Assert.Equal(sha, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(f))));
        Assert.Throws<ArgumentException>(() => ImageTools.ColorAt(f, 99, 0));
    }

    [AvaloniaFact(DisplayName = "SYS-02 (UI): Tabs vorhanden; Umbenennen mit Vorschau + Rückgängig; Erinnerung; Zwischenablage ohne Passwort; Einheiten")]
    public void Pages()
    {
        var (w, _, root) = Ui.Open();
        foreach (var k in new[] { "alltag-dateien", "alltag-bilder", "alltag-zeit", "alltag-helfer" }) { w.Show(k); Assert.Equal(k, w.CurrentView); }
        var dir = Path.Combine(root, "umbenennen"); Directory.CreateDirectory(dir); File.WriteAllText(Path.Combine(dir, "x.txt"), "1"); File.WriteAllText(Path.Combine(dir, "y.txt"), "2");
        w.Show("alltag-dateien");
        w.Find<TextBox>("ren-folder").Text = dir; w.Find<TextBox>("ren-pattern").Text = "Foto_{n:00}";
        w.Find<Button>("ren-preview-btn").Press(); Assert.Equal(2, w.Find<StackPanel>("ren-preview").Children.Count);
        w.Find<Button>("ren-apply").Press(); Assert.True(File.Exists(Path.Combine(dir, "Foto_01.txt")));
        w.Find<Button>("ren-undo").Press(); Assert.True(File.Exists(Path.Combine(dir, "x.txt")));

        w.Show("alltag-zeit");
        w.Find<TextBox>("rem-when").Text = "0"; w.Find<TextBox>("rem-text").Text = "Pause machen"; w.Find<Button>("rem-add").Press();
        // Der 1-Sekunden-Timer kann die fällige Erinnerung schon abgeholt haben (unter Last); gemeldet wird sie in beiden Fällen
        var due = w.CheckReminders(DateTime.Now);
        if (due.Count == 0) Assert.Contains("Pause machen", File.ReadAllText(Path.Combine(root, "audit", "audit.jsonl")));
        else Assert.Single(due);
        w.Find<Button>("focus-25").Press(); Assert.NotNull(w.Focus.Until(DateTime.Now)); w.Find<Button>("focus-stop").Press(); Assert.Null(w.Focus.Until(DateTime.Now));

        w.Show("alltag-helfer");
        w.Find<TextBox>("clip-add-text").Text = "Xy7#kLm9!pQr2"; w.Find<Button>("clip-add").Press(); Assert.Empty(w.Clips.All());
        w.Find<TextBox>("clip-add-text").Text = "Einkaufsliste: Brot"; w.Find<Button>("clip-add").Press(); Assert.Single(w.Clips.All());
        w.Find<TextBox>("unit-value").Text = "10"; w.Find<Button>("unit-run").Press(); Dispatcher.UIThread.RunJobs();
        Assert.StartsWith("= 6,21", w.Find<TextBlock>("unit-result").Text);
        w.Close();
    }
}
