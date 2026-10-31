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

/// <summary>T-0264: Programm „Punktwolke“ über den echten Eingabeweg – Drehen/Zoomen mit der Maus, Messen per Klick, Schritte im Menüband, Schritt entfernen, Datei laden, Export.</summary>
public class SPEC09_Ui
{
    const string PK = "punktwolke";
    static T Get<T>(MainWindow w, string name) where T : Control { Settle(); return w.GetVisualDescendants().OfType<T>().Last(c => c.Name == name && c.IsEffectivelyVisible && c.Bounds.Width > 0); }
    static T? Try<T>(MainWindow w, string name) where T : Control { Settle(); return w.GetVisualDescendants().OfType<T>().LastOrDefault(c => c.Name == name && c.IsEffectivelyVisible && c.Bounds.Width > 0); }
    static void Press(MainWindow w, string name) { var b = Get<Button>(w, name); b.BringIntoView(); Settle(); RealClick(w, Get<Button>(w, name)); }
    static ScanDoc Doc(MainWindow w) => ScanDoc.From(w.ProgramActive(PK)!.Data);
    static int Points(MainWindow w) => w.ScanCloud(w.ProgramActive(PK)!).Points.Count;

    [AvaloniaFact(DisplayName = "SPEC-09 UI: Beispielraum – Maus dreht und zoomt, Störpunkte/Boden entfernen als Schritte, Schritt wieder entfernen, Rückgängig")]
    public void Orbit_and_steps()
    {
        var (w, _, _) = Ui.Open();
        Assert.Equal(PK, MainWindow.StudioPages["SPEC-09"]);
        w.Show(PK); Settle();
        RealClick(w, Get<Button>(w, "prog-tpl-beispiel"));
        Assert.Null(Try<Button>(w, "scan-info-run")); // keine Formularseite mehr
        var view = Get<PointCloudView>(w, "pc-view"); var yaw = view.Yaw;
        var c = view.TranslatePoint(new Point(view.Bounds.Width / 2, view.Bounds.Height / 2), w)!.Value;
        w.MouseDown(c, MouseButton.Left); w.MouseMove(c + new Point(60, 20)); w.MouseMove(c + new Point(120, 30)); w.MouseUp(c + new Point(120, 30), MouseButton.Left); Settle();
        Assert.NotEqual(yaw, view.Yaw); Assert.Equal(view.Yaw, Doc(w).Yaw, 6); // Ansicht wird im Dokument gemerkt
        w.MouseWheel(c, new Vector(0, 1)); Settle(); Assert.True(Doc(w).Zoom > 1);
        var raw = Points(w);
        Press(w, "prog-cmd-pc-outliers");
        Assert.InRange(raw - Points(w), 40, 48); Assert.DoesNotContain(w.ScanCloud(w.ProgramActive(PK)!).Colors!, c => c == (255, 60, 60));
        Assert.Contains("Störpunkte entfernt", Get<TextBlock>(w, "pc-op-0").Text);
        Press(w, "prog-cmd-pc-nofloor");
        Assert.Equal(2, Doc(w).Ops.Count); Assert.True(Points(w) < raw - 7000);
        Press(w, "pc-op-del-0"); // ersten Schritt herausnehmen, zweiter bleibt
        Assert.Single(Doc(w).Ops); Assert.Equal("nofloor", Doc(w).Ops[0].Kind);
        Press(w, "prog-undo"); Assert.Equal(2, Doc(w).Ops.Count);
        Press(w, "prog-cmd-pc-v-top"); Assert.Equal(89, Doc(w).Pitch);
        Press(w, "prog-cmd-pc-color"); Assert.Equal("hoehe", Doc(w).ColorBy); Assert.True(Get<PointCloudView>(w, "pc-view").ByHeight);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "SPEC-09 UI: Messen – zwei Punkte anklicken zeigt den Abstand; eigene Datei laden, Einheit umrechnen, Analyse, Export PLY/XYZ/Bericht")]
    public void Measure_load_export()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        w.Show(PK); Settle(); RealClick(w, Get<Button>(w, "prog-tpl-beispiel"));
        Press(w, "prog-cmd-pc-v-front");
        Press(w, "prog-cmd-pc-measure");
        Assert.Contains("ersten Punkt", Get<TextBlock>(w, "pc-measure-out").Text);
        // zwei Punkte der Wand: links und rechts oben (Wand bei y = 3, x 0…4, z bis 1,2)
        var view = Get<PointCloudView>(w, "pc-view");
        Point Screen(P3 p) { var (x, y, _) = view.Project(p); return view.TranslatePoint(new Point(x, y), w)!.Value; }
        var a = Screen(new(0.4, 3, 1.0)); var b = Screen(new(3.6, 3, 1.0));
        w.MouseDown(a, MouseButton.Left); w.MouseUp(a, MouseButton.Left); Settle();
        Assert.Single(w.ScanMarks);
        view = Get<PointCloudView>(w, "pc-view"); b = Screen(new(3.6, 3, 1.0));
        w.MouseDown(b, MouseButton.Left); w.MouseUp(b, MouseButton.Left); Settle();
        Assert.Equal(2, w.ScanMarks.Count);
        var dist = Math.Abs(w.ScanMarks[1].X - w.ScanMarks[0].X); Assert.InRange(dist, 3.0, 3.4);
        Assert.Contains("Abstand 3,", Get<TextBlock>(w, "pc-measure-out").Text);
        // Export
        foreach (var x in new[] { "ply", "xyz", "report" })
        {
            Press(w, "prog-export-" + x);
            var file = w.ProgramMessage(PK)["Exportiert: ".Length..].Trim();
            Assert.True(File.Exists(file) && new FileInfo(file).Length > 50, x);
            if (x == "report") Assert.Contains("Gemessener Abstand", File.ReadAllText(file));
        }
        // eigene Datei in mm laden und auf Meter umrechnen
        var f = Path.Combine(root, "bauteil.xyz"); File.WriteAllText(f, string.Join("\n", Enumerable.Range(0, 50).Select(i => $"{i * 20} {i % 5 * 30} {i % 7 * 10}")));
        Get<TextBox>(w, "pc-file-path").Text = f; Press(w, "pc-load");
        Assert.Equal(f, Doc(w).Source); Assert.Empty(Doc(w).Ops); Assert.Equal(50, Points(w));
        Assert.Contains("980", Get<SelectableTextBlock>(w, "pc-info").Text);
        Press(w, "prog-cmd-pc-scale");
        Assert.Contains("0,98", Get<SelectableTextBlock>(w, "pc-info").Text);
        Press(w, "pc-a-dens"); Assert.Contains("Punktabstand", Get<SelectableTextBlock>(w, "pc-analysis").Text);
        Get<TextBox>(w, "pc-cmp-path").Text = f; Press(w, "pc-a-merge");
        Assert.Equal(100, Points(w));
        Get<TextBox>(w, "pc-file-path").Text = Path.Combine(root, "fehlt.ply"); Press(w, "pc-load");
        Assert.Contains("gibt es nicht", w.ProgramMessage(PK)); Assert.Equal(f, Doc(w).Source);
        w.Close();
    }
}
