using System.Xml.Linq;
using Trendo.Core.Karten;
using Xunit;
namespace Trendo.Core.Tests;

/// <summary>T-0719: GPX fremder Programme – jede Spur und jede Route einzeln (vorher in eine Linie zusammengelegt); GPX-Ausgabe in der Reihenfolge, die GPX 1.1 verlangt.</summary>
public class T0719_MapGpx
{
    [Fact(DisplayName = "T-0719: GPX mit zwei Spuren (eine mit zwei Segmenten) und einer Route → drei Linien mit Namen, Höhen und Zeiten; ohne Namen der Dateiname")]
    public void Import_each_track_and_route()
    {
        var d = Path.Combine(Path.GetTempPath(), "trendo-t0719-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d);
        try
        {
            var f = Path.Combine(d, "fremd.gpx");
            File.WriteAllText(f, """
                <gpx version="1.1" creator="x" xmlns="http://www.topografix.com/GPX/1/1" xmlns:e="urn:ext">
                <rte><name>Hinweg</name><rtept lat="48.0" lon="11.0"/><rtept lat="48.01" lon="11.01"/></rte>
                <trk><name>Teil A</name><trkseg><trkpt lat="48.0" lon="11.0"><ele>500</ele><time>2026-09-12T08:00:00Z</time><extensions><e:hr>99</e:hr></extensions></trkpt><trkpt lat="48.001" lon="11.0"><ele>505</ele></trkpt></trkseg>
                  <trkseg><trkpt lat="48.002" lon="11.0"/></trkseg></trk>
                <trk><trkseg><trkpt lat="48.1" lon="11.1"/><trkpt lat="48.11" lon="11.1"/></trkseg></trk>
                </gpx>
                """);
            var m = new MapDoc(); var first = m.ImportGpx(f)!;
            Assert.Equal(3, m.Tracks.Count);
            Assert.Equal("Hinweg", first.Name); Assert.True(first.Planned);
            var a = m.Tracks[1]; Assert.Equal("Teil A", a.Name); Assert.False(a.Planned); Assert.Equal(3, a.Points.Count); Assert.Equal(500, a.Points[0].Ele);
            Assert.Equal(new DateTime(2026, 9, 12, 8, 0, 0, DateTimeKind.Utc), a.Points[0].Time!.Value.ToUniversalTime());
            Assert.Equal("fremd (3)", m.Tracks[2].Name);
            // Ausgabe: wpt → rte → trk (GPX 1.1, xsd:sequence), auch wenn die Spur vor der Route in der Liste steht
            var x = XDocument.Parse(new MapDoc { Tracks = [a, first], Waypoints = [new MapWaypoint { Name = "P", Lat = 48, Lon = 11 }] }.Gpx());
            Assert.Equal(["metadata", "wpt", "rte", "trk"], x.Root!.Elements().Select(e => e.Name.LocalName).ToArray());
        }
        finally { Directory.Delete(d, true); }
    }
}
