using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Alle Studios, Runde 7 – CORE-09: „Objekte erkennen“ im Kamera-Bereich (YOLOX nachgebildet; Echtlauf erst nach Freigabe).</summary>
public class CamObjects_Ui
{
    [AvaloniaFact(DisplayName = "Objekterkennung: ohne Einrichtung Einrichten-Knopf; ohne Bild klarer Hinweis; mit Bild → markiertes Bild in der Vorschau, „2 Personen, 1 Hund“ im Status")]
    public async Task Detect()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            MainWindow.ObjectsRunOverride = null;
            w.Show(MainWindow.StudioPages["CORE-09"]);
            Assert.NotNull(w.Find<Button>("cam-objects-setup"));
            var marked = VisionPlus.EncodePng(new RgbImage(2, 2, [245, 165, 36, 245, 165, 36, 245, 165, 36, 245, 165, 36]));
            IReadOnlyList<string>? seen = null;
            MainWindow.ObjectsRunOverride = a =>
            {
                seen = a; File.WriteAllBytes(a[2], marked);
                return new Cli.Result(0, """{"breite":2,"hoehe":2,"ms":40,"objekte":[{"klasse":"person","sicherheit":0.9,"rahmen":[0,0,1,2]},{"klasse":"dog","sicherheit":0.8,"rahmen":[1,1,2,2]},{"klasse":"person","sicherheit":0.7,"rahmen":[0,0,2,2]}]}""", "");
            };
            w.Show(MainWindow.StudioPages["CORE-09"]);
            await w.CamDetectObjects();
            Assert.Equal("Zuerst ein Bild holen (Livebild oder Foto).", w.Bench.CamStatus);
            w.Bench.Frame = VisionPlus.EncodePng(new RgbImage(2, 2, [10, 20, 30, 40, 50, 60, 70, 80, 90, 100, 110, 120]));
            w.Find<Button>("cam-objects").Press();
            for (var i = 0; i < 50 && !w.Bench.CamStatus.StartsWith("Erkannt", StringComparison.Ordinal); i++) { await Task.Delay(20); Avalonia.Threading.Dispatcher.UIThread.RunJobs(); }
            Assert.Equal("Erkannt: 2 Personen, 1 Hund (40 ms, YOLOX-S, lokal).", w.Bench.CamStatus);
            Assert.Equal(marked, w.Bench.Frame); Assert.Equal(3, w.ObjectsLast!.Hits.Count);
            Assert.EndsWith(".png", seen![1]); Assert.Equal("0.35", seen[3]);
            Assert.Equal("Erkannt: 2 Personen, 1 Hund (40 ms, YOLOX-S, lokal).", w.Find<TextBlock>("cam-status").Text);
            MainWindow.ObjectsRunOverride = _ => new Cli.Result(1, "", "Bild nicht lesbar: kaputt");
            await w.CamDetectObjects();
            Assert.StartsWith("⚠ Objekterkennung: Bild nicht lesbar", w.Bench.CamStatus);
        }
        finally { MainWindow.ObjectsRunOverride = null; w.Close(); }
    }
}
