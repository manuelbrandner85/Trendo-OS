using System.Diagnostics;
using System.Globalization;
using System.Text;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.Core;
using Trendo.Core.Office;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0724 (Audit Phase E, Punkt 4): grobe Leistungsmessung „großes Projekt öffnen“ über die echte Oberfläche (Avalonia Headless, kein
/// sichtbares Fenster): Tabelle mit 100 000 Zellen (XLSX) und 3D-Szene mit 40 000 Dreiecken (OBJ). Es gibt bewusst KEIN Zeitziel – geprüft wird nur,
/// dass alles vollständig ankommt; die gemessenen Zeiten landen in der Testausgabe und, wenn TRENDO_LEISTUNG_OUT gesetzt ist, in dieser Datei.</summary>
public class LeistungT0724Ui(ITestOutputHelper output)
{
    static void Pump() { for (var i = 0; i < 6; i++) Dispatcher.UIThread.RunJobs(); }

    void Report(string line)
    {
        output.WriteLine(line);
        if (Environment.GetEnvironmentVariable("TRENDO_LEISTUNG_OUT") is { Length: > 0 } f) File.AppendAllText(f, line + Environment.NewLine, new UTF8Encoding(false));
    }

    [AvaloniaFact(DisplayName = "T-0724 Leistung: Tabelle mit 100 000 Zellen öffnen – alle Werte kommen an (Zeit wird nur gemessen)")]
    public void Tabelle_100000_Zellen()
    {
        var (w, _, root) = Ui.Open();
        try
        {
            var src = new SheetDoc(); var s = src.Sheets[0];
            for (var r = 0; r < 1000; r++) for (var c = 0; c < 100; c++) s.Set(r, c, ((r * 100) + c).ToString(CultureInfo.InvariantCulture));
            var f = Path.Combine(root, "gross-tabelle.xlsx"); SheetFiles.Save(src, f);
            var sw = Stopwatch.StartNew();
            w.SheetOpen(f); var opened = sw.Elapsed.TotalMilliseconds;
            w.Show("tabelle"); Pump(); var shown = sw.Elapsed.TotalMilliseconds;
            var sheet = w.SheetDocument.Sheets[0];
            Assert.Equal("0", sheet.Get("A1").Raw);
            Assert.Equal("99999", sheet.Get("CV1000").Raw); // Zeile 1000, Spalte 100
            Report(FormattableString.Invariant($"Tabelle 1000 x 100 = 100000 Zellen, XLSX {new FileInfo(f).Length / 1024} KB: Datei lesen {opened:0} ms, bis Ansicht aufgebaut {shown:0} ms"));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0724 Leistung: 3D-Szene mit 40 000 Dreiecken öffnen – alle Dreiecke kommen an (Zeit wird nur gemessen)")]
    public void Szene_40000_Dreiecke()
    {
        var (w, _, root) = Ui.Open();
        try
        {
            // Gitter 101 x 201 Punkte = 100 x 200 Felder x 2 Dreiecke = 40 000 Dreiecke, leicht gewellt
            var f = Path.Combine(root, "gross-szene.obj");
            var sb = new StringBuilder();
            for (var y = 0; y <= 200; y++) for (var x = 0; x <= 100; x++) sb.Append(FormattableString.Invariant($"v {x} {y} {Math.Sin(x * 0.3) * Math.Cos(y * 0.2):0.0000}\n"));
            int Id(int x, int y) => (y * 101) + x + 1;
            for (var y = 0; y < 200; y++) for (var x = 0; x < 100; x++) sb.Append(FormattableString.Invariant($"f {Id(x, y)} {Id(x + 1, y)} {Id(x + 1, y + 1)}\nf {Id(x, y)} {Id(x + 1, y + 1)} {Id(x, y + 1)}\n"));
            File.WriteAllText(f, sb.ToString());
            w.Show("3d"); Pump();
            var sw = Stopwatch.StartNew();
            var import = w.Import3DFileAsync(f); while (!import.IsCompleted) Dispatcher.UIThread.RunJobs(); import.GetAwaiter().GetResult();
            var read = sw.Elapsed.TotalMilliseconds;
            w.Show("3d"); Pump(); var shown = sw.Elapsed.TotalMilliseconds;
            var part = Assert.Single(w.Studio3DParts);
            Assert.Equal(40_000, Mesh3D.FromStored(part).Triangles);
            Report(FormattableString.Invariant($"3D-Szene 40000 Dreiecke, OBJ {new FileInfo(f).Length / 1024} KB: Import fertig {read:0} ms, bis Ansicht aufgebaut {shown:0} ms"));
        }
        finally { w.Close(); }
    }
}
