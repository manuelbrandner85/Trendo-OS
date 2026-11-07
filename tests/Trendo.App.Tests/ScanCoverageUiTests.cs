using System.Globalization;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>S4 (Modernisierung 07.11.2026): Aufnahme-Assistent und Abdeckungsanzeige beim Einscannen.</summary>
public class ScanCoverage_Ui
{
    static void Pump() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    [AvaloniaFact(DisplayName = "S4 Scannen: Aufnahme-Assistent vor der Rechnung; nach der Rechnung Abdeckungsscheibe mit Prozent und Lücken aus den Kamerapositionen")]
    public void Assistant_and_coverage()
    {
        var (w, _, _) = Ui.Open();
        var dir = Path.Combine(Path.GetTempPath(), "trendo-scan-" + Guid.NewGuid().ToString("N"));
        try
        {
            w.Show("scan3d"); Pump();
            Assert.Equal(5, w.Find<StackPanel>("scan-assistant").Children.Count(c => c is DockPanel) + 1); // vier Schritte plus Überschrift
            Assert.Equal(0, w.Find<ProgressBar>("scan-assistant-progress").Value);
            Assert.Null(w.TryFind<Border>("scan-coverage")); // ohne Modell keine Abdeckung
            // Modell (Würfel, 1 m) und Kameras rundum schräg, aber mit Lücke 150°–180° und ohne Runde von oben
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "modell.obj"), "v -50 -50 0\nv 50 -50 0\nv 50 50 0\nv -50 50 0\nv -50 -50 100\nv 50 -50 100\nv 50 50 100\nv -50 50 100\nf 1 2 3\nf 1 3 4\nf 5 6 7\nf 5 7 8\nf 1 2 6\nf 1 6 5\n");
            var cams = Enumerable.Range(0, 12).Select(i => i * 30).Where(d => d is not (150 or 180))
                .Select(d => $"{{\"name\":\"f{d}.jpg\",\"x\":{(200 * Math.Cos(d * Math.PI / 180)).ToString(CultureInfo.InvariantCulture)},\"y\":{(200 * Math.Sin(d * Math.PI / 180)).ToString(CultureInfo.InvariantCulture)},\"z\":{(50 + 150).ToString(CultureInfo.InvariantCulture)}}}");
            File.WriteAllText(Path.Combine(dir, "kameras.json"), "[" + string.Join(",", cams) + "]");
            w.ScanSetModel(Path.Combine(dir, "modell.obj")); Pump();
            Assert.NotNull(w.Find<Border>("scan-coverage")); Assert.NotNull(w.Find<ScanCoverageView>("scan-coverage-disc").Result);
            var r = w.ScanCoverageResult!;
            Assert.Equal(10, r.Cameras);
            Assert.Contains(r.Gaps, g => g.Contains("150°–180° fehlt"));
            Assert.Contains(r.Gaps, g => g.StartsWith("von oben"));
        }
        finally { w.Close(); if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
}
