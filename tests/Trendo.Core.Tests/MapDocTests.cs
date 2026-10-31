using System.Text.Json.Nodes;
using Trendo.Core.Karten;
using Xunit;
namespace Trendo.Core.Tests;

/// <summary>T-0260: Karten & Touren – Projektion, GPX laden, Route/Fläche, Export GPX/GeoJSON/KML.</summary>
public class T0260_Map
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-map-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

    [Fact(DisplayName = "T-0260: Web-Mercator – Hin und zurück, Kachel von München bei Zoom 10, Meter je Pixel")]
    public void Projection()
    {
        var (x, y) = WebMercator.ToPixel(48.1372, 11.5756, 10);
        Assert.Equal(544, (int)(x / 256)); Assert.Equal(355, (int)(y / 256)); // OSM-Kachel 10/544/355
        var (lat, lon) = WebMercator.ToLatLon(x, y, 10);
        Assert.Equal(48.1372, lat, 6); Assert.Equal(11.5756, lon, 6);
        Assert.Equal(156543.03, WebMercator.MetersPerPixel(0, 0), 1);
        Assert.Equal(156543.03392 * 0.5, WebMercator.MetersPerPixel(60, 0), 2);
    }

    [Fact(DisplayName = "T-0260: GPX laden (Spur + Wegpunkt), Kennzahlen, Ansicht passt sich an; Route und Fläche zeichnen")]
    public void Import_route_area()
    {
        var d = Tmp();
        try
        {
            var f = Path.Combine(d, "tour.gpx");
            File.WriteAllText(f, "<gpx xmlns=\"http://www.topografix.com/GPX/1/1\"><wpt lat=\"47.405\" lon=\"10.98\"><name>Hütte</name></wpt><trk><trkseg><trkpt lat=\"47.40\" lon=\"10.98\"><ele>1000</ele></trkpt><trkpt lat=\"47.41\" lon=\"10.98\"><ele>1100</ele></trkpt></trkseg></trk></gpx>");
            var m = new MapDoc(); var t = m.ImportGpx(f)!;
            Assert.Equal("tour", t.Name); Assert.Single(m.Waypoints); Assert.Equal("Hütte", m.Waypoints[0].Name);
            Assert.Equal(1.11, t.Stats().DistanceKm, 2); Assert.Equal(100, t.Stats().AscentM, 0);
            Assert.InRange(m.CenterLat, 47.40, 47.41); Assert.True(m.Zoom > 12);
            var r = m.AddRoute([(48.0, 11.0), (48.0, 11.01), (48.01, 11.01)]);
            Assert.True(r.Planned); Assert.InRange(r.Stats().DistanceKm, 0.74 + 1.11 - 0.05, 0.74 + 1.11 + 0.05);
            var a = m.AddArea([(48.0, 11.0), (48.0, 11.001), (48.001, 11.001), (48.001, 11.0)]);
            Assert.InRange(a.AreaM2, 74.4 * 111.2 * 0.97, 74.4 * 111.2 * 1.03); // rund 8.270 m²
            Assert.Throws<ArgumentException>(() => m.AddRoute([(48.0, 11.0)]));
            Assert.True(m.Remove(a.Id)); Assert.Empty(m.Areas);
            File.WriteAllText(f, "<gpx xmlns=\"http://www.topografix.com/GPX/1/1\"></gpx>");
            Assert.ThrowsAny<Exception>(() => new MapDoc().ImportGpx(f)); // leere Datei: verständliche Meldung statt Absturz
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0260: Export GPX (Spur als trk, Route als rte) wieder lesbar, GeoJSON und KML; Speichern im Dokument")]
    public void Exports()
    {
        var d = Tmp();
        try
        {
            var m = MapDoc.Template("tour"); m.AddRoute([(47.5, 11.0), (47.51, 11.02)], "Abkürzung");
            var gpx = m.Gpx(); Assert.Contains("<trk><name>Rundweg</name>", gpx); Assert.Contains("<rte><name>Abkürzung</name>", gpx); Assert.Contains("<wpt ", gpx);
            var f = Path.Combine(d, "x.gpx"); File.WriteAllText(f, gpx);
            var (trk, wpt) = Geo.ReadGpx(f);
            Assert.True(trk.Count >= 121); Assert.Equal(2, wpt.Count);
            var gj = System.Text.Json.JsonDocument.Parse(m.GeoJson());
            Assert.Equal(4, gj.RootElement.GetProperty("features").GetArrayLength());
            var kml = m.Kml(); Assert.Contains("<LineString><coordinates>", kml); Assert.Contains("<name>Gipfelkreuz</name>", kml);
            System.Xml.Linq.XDocument.Parse(kml); System.Xml.Linq.XDocument.Parse(gpx);
            var data = new JsonObject(); m.WriteTo(data);
            var back = MapDoc.From(data); Assert.Equal(2, back.Tracks.Count); Assert.Equal(m.Tracks[0].Stats().DistanceKm, back.Tracks[0].Stats().DistanceKm, 6);
        }
        finally { Directory.Delete(d, true); }
    }
}
