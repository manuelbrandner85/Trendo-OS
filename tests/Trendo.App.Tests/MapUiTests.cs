using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Trendo.Core.Karten;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0260: Programm „Karten & Touren“ über den echten Eingabeweg – GPX laden, Spur wählen, Route zeichnen, Wegpunkt, Fläche, Export, Rechner.</summary>
public class SPEC13_Ui
{
    const string MK = "karten";
    static MapDoc Model(MainWindow w) => MapDoc.From(w.ProgramActive(MK)!.Data);
    static T Get<T>(MainWindow w, string name) where T : Control { Settle(); return w.GetVisualDescendants().OfType<T>().Last(c => c.Name == name && c.IsEffectivelyVisible && c.Bounds.Width > 0); }
    static void Press(MainWindow w, string name) { var b = Get<Button>(w, name); b.BringIntoView(); Settle(); RealClick(w, Get<Button>(w, name)); }
    static Point At(MainWindow w, double lat, double lon) { Settle(); w.UpdateLayout(); Settle(); var c = Get<MapCanvas>(w, "mk-canvas"); return c.TranslatePoint(c.Screen(lat, lon), w)!.Value; }
    static void Click(MainWindow w, Point p) { w.MouseMove(p); w.MouseDown(p, MouseButton.Left); w.MouseUp(p, MouseButton.Left); Settle(); }
    static void Enter(MainWindow w) { w.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null); w.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null); Settle(); }

    [AvaloniaFact(DisplayName = "SPEC-13 UI: GPX laden – Spur mit Höhenprofil und Kennzahlen; Spur auf der Karte anklicken, umbenennen, exportieren")]
    public void Gpx_profile_export()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        var f = Path.Combine(root, "tour.gpx"); File.WriteAllText(f, "<gpx xmlns=\"http://www.topografix.com/GPX/1/1\"><trk><trkseg><trkpt lat=\"47.40\" lon=\"10.98\"><ele>1000</ele></trkpt><trkpt lat=\"47.405\" lon=\"10.985\"><ele>1060</ele></trkpt><trkpt lat=\"47.41\" lon=\"10.98\"><ele>1100</ele></trkpt></trkseg></trk></gpx>");
        w.Show(MainWindow.StudioPages["SPEC-13"]); Settle();
        RealClick(w, Get<Button>(w, "prog-tpl-leer"));
        Assert.Null(w.TryFind<Button>("geo-gpx-run")); // kein Formular mehr
        Get<TextBox>(w, "mk-gpx-path").Text = f; Press(w, "mk-gpx-load");
        var t = Assert.Single(Model(w).Tracks);
        Assert.Equal(t.Id, w.MapState.Selected);
        Assert.Contains("↑ 100 m", string.Join(" ", w.GetVisualDescendants().OfType<TextBlock>().Select(x => x.Text)));
        Assert.NotNull(Get<Border>(w, "mk-profile"));
        Click(w, At(w, 47.403, 10.995)); Assert.Null(w.MapState.Selected); // ins Leere: abwählen
        Click(w, At(w, 47.4025, 10.9825)); Assert.Equal(t.Id, w.MapState.Selected); // Spur treffen
        var name = Get<TextBox>(w, "mk-p-name"); name.Focus(); name.Text = "Hausrunde"; Enter(w);
        Assert.Equal("Hausrunde", Model(w).Tracks[0].Name);
        foreach (var x in new[] { "gpx", "geojson", "kml" })
        {
            Press(w, "prog-export-" + x);
            var file = w.ProgramMessage(MK)["Exportiert: ".Length..].Trim();
            Assert.True(File.Exists(file) && File.ReadAllText(file).Contains("Hausrunde"), x);
        }
        w.Close();
    }

    [AvaloniaFact(DisplayName = "SPEC-13 UI: Route mit der Maus zeichnen (Doppelklick beendet), Wegpunkt setzen, Fläche messen (Enter), Rechner rechnen sofort")]
    public void Draw_route_waypoint_area()
    {
        var (w, _, _) = Ui.Open();
        w.Show(MainWindow.StudioPages["SPEC-13"]); Settle();
        RealClick(w, Get<Button>(w, "prog-tpl-tour"));
        Press(w, "mk-tool-route");
        var a = At(w, 47.47, 11.06); var b = At(w, 47.475, 11.07); var c = At(w, 47.48, 11.07);
        Click(w, a); Click(w, b); Click(w, c);
        w.MouseDown(c, MouseButton.Left); w.MouseUp(c, MouseButton.Left); w.MouseDown(c, MouseButton.Left); w.MouseUp(c, MouseButton.Left); Settle();
        var route = Model(w).Tracks.FirstOrDefault(t => t.Planned);
        Assert.NotNull(route); Assert.Equal(3, route!.Points.Count);
        Assert.InRange(route.Stats().DistanceKm, 0.9, 1.6);
        Press(w, "mk-tool-wegpunkt"); Click(w, At(w, 47.46, 11.05));
        Assert.Equal(3, Model(w).Waypoints.Count);
        Assert.Contains("UTM 32T", Get<SelectableTextBlock>(w, "mk-coord").Text);
        Press(w, "mk-tool-flaeche");
        foreach (var (la, lo) in new[] { (47.465, 11.04), (47.465, 11.05), (47.47, 11.05) }) Click(w, At(w, la, lo));
        Get<MapCanvas>(w, "mk-canvas").Focus(); Settle(); Enter(w);
        var area = Assert.Single(Model(w).Areas); Assert.True(area.AreaM2 > 10000);
        Get<MapCanvas>(w, "mk-canvas").Focus(); Settle();
        w.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null); w.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null); Settle(); // Esc: Auswahl aufheben → Übersicht
        Assert.Null(w.MapState.Selected);
        Get<Expander>(w, "mk-calcs").IsExpanded = true; Settle();
        Assert.Contains("UTM 32U", Get<SelectableTextBlock>(w, "geo-coord-out").Text);
        Assert.Contains("504", Get<SelectableTextBlock>(w, "geo-dist-out").Text);
        Assert.Contains("(S", Get<SelectableTextBlock>(w, "geo-dist-out").Text);
        Assert.Contains("48.1", Get<SelectableTextBlock>(w, "geo-utm-out").Text);
        Assert.Contains("Ziel: 48.13", Get<SelectableTextBlock>(w, "geo-dest-out").Text);
        Assert.Contains("5:46 h", Get<SelectableTextBlock>(w, "geo-time-out").Text);
        w.Close();
    }
}
