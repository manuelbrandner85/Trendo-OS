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

/// <summary>T-0274: Höhen und Routing aus dem Internet – nur nach Zustimmung, mit nachgebautem Dienst.</summary>
public class SPEC13_UiNet
{
    const string MK = "karten";
    static T Get<T>(MainWindow w, string name) where T : Control { Settle(); return w.GetVisualDescendants().OfType<T>().Last(c => c.Name == name && c.IsEffectivelyVisible && c.Bounds.Width > 0); }
    static T? Try<T>(MainWindow w, string name) where T : Control { Settle(); return w.GetVisualDescendants().OfType<T>().LastOrDefault(c => c.Name == name && c.IsEffectivelyVisible && c.Bounds.Width > 0); }
    static void Press(MainWindow w, string name) { var b = Get<Button>(w, name); b.BringIntoView(); Settle(); RealClick(w, Get<Button>(w, name)); }
    static async Task Wait(Func<bool> done) { for (var i = 0; i < 200 && !done(); i++) { await Task.Delay(10); Settle(); } }

    sealed class Fake : HttpMessageHandler
    {
        public List<string> Urls { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        {
            var u = r.RequestUri!.ToString(); Urls.Add(u);
            string json;
            if (u.Contains("opentopodata")) { var n = Uri.UnescapeDataString(r.RequestUri.Query).Split("locations=")[1].Split('|').Length; json = "{\"status\":\"OK\",\"results\":[" + string.Join(",", Enumerable.Range(0, n).Select(i => $"{{\"elevation\":{600 + i}}}")) + "]}"; }
            else json = "{\"code\":\"Ok\",\"routes\":[{\"distance\":2500,\"duration\":1800,\"geometry\":{\"coordinates\":[[11.0,48.0],[11.005,48.004],[11.01,48.01]]}}]}";
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(json) });
        }
    }

    [AvaloniaFact(DisplayName = "SPEC-13 UI: Höhen ergänzen fragt erst – „Nein“ sendet nichts, „Ja“ holt die Höhen; Route entlang von Wegen über den eingetragenen Server")]
    public async Task Elevation_and_route_with_consent()
    {
        var (w, _, _) = Ui.Open(); var fake = new Fake(); w.MapHttpOverride = fake;
        w.Show(MK); Settle(); RealClick(w, Get<Button>(w, "prog-tpl-leer"));
        string id = ""; w.MapEdit("Route", x => id = x.AddRoute([(48.0, 11.0), (48.01, 11.01)], "Testroute").Id); w.MapSelect(id);
        Press(w, "mk-ele"); Assert.NotNull(Try<Border>(w, "mk-consent"));
        Press(w, "mk-consent-no"); Assert.Empty(fake.Urls); Assert.Equal("Nichts gesendet.", Get<TextBlock>(w, "mk-net-msg").Text);
        Press(w, "mk-ele"); Press(w, "mk-consent-yes");
        await Wait(() => Try<TextBlock>(w, "mk-net-msg")?.Text?.StartsWith('✓') == true);
        Assert.Single(fake.Urls); Assert.Contains("api.opentopodata.org/v1/eudem25m", fake.Urls[0]);
        var tr = MapDoc.From(w.ProgramActive(MK)!.Data).Tracks.Single(); Assert.True(tr.Points.Count > 10); Assert.All(tr.Points, p => Assert.NotNull(p.Ele));
        Assert.NotNull(Try<Border>(w, "mk-profile"));
        // Routing: ohne Adresse klare Meldung, mit Adresse neue Linie
        Press(w, "mk-route"); Press(w, "mk-consent-yes");
        await Wait(() => Try<TextBlock>(w, "mk-net-msg")?.Text?.StartsWith('⚠') == true);
        Assert.Contains("Routing-Servers", Get<TextBlock>(w, "mk-net-msg").Text);
        Get<TextBox>(w, "mk-route-server").Text = "https://osrm.example"; Press(w, "mk-route"); Press(w, "mk-consent-yes");
        await Wait(() => Try<TextBlock>(w, "mk-net-msg")?.Text?.Contains("entlang von Wegen") == true);
        Assert.StartsWith("https://osrm.example/route/v1/foot/", fake.Urls[^1]);
        Assert.Equal(3, MapDoc.From(w.ProgramActive(MK)!.Data).Tracks.Single().Points.Count);
        Assert.Contains("2,5 km", Get<TextBlock>(w, "mk-net-msg").Text);
        w.Close();
    }
}
