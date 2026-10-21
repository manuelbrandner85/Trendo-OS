using Avalonia.Controls;
using Trendo.Core;

namespace Trendo.App;

/// <summary>SPEC-13: Karten & Geodaten.</summary>
public partial class MainWindow
{
    private void InitSpec13() => RegisterTab(FachTabs, "karten", "Karten & GPS", "SPEC-13", GeoView, "karte", "gps", "gpx", "wandern", "route", "strecke", "koordinaten", "utm", "geojson", "höhenmeter");

    private Control GeoView()
    {
        var sp = new StackPanel { Spacing = 8 };
        sp.Children.Add(FachTool("geo-gpx", "GPS-Strecke auswerten", "GPX-Datei aus Uhr, Handy-App (Komoot, Strava, OsmAnd) oder Navi. Trendo speichert daneben GeoJSON (für QGIS, uMap) und ein Streckenbild.",
            [("file", "GPX-Datei", "")],
            v =>
            {
                var f = v("file").Trim().Trim('"'); var (trk, wpt) = Geo.ReadGpx(f);
                var lines = new List<string>();
                if (trk.Count > 0)
                {
                    var s = Geo.Analyse(trk);
                    lines.Add($"{Fach.F(s.DistanceKm, 2)} km · ↑ {Fach.F(s.AscentM, 0)} m · ↓ {Fach.F(s.DescentM, 0)} m · Höhe {Fach.F(s.MinEle, 0)}–{Fach.F(s.MaxEle, 0)} m · {s.Points} Punkte");
                    if (s.Duration is { } d) lines.Add($"Dauer {(int)d.TotalHours}:{d.Minutes:00} h · Schnitt {Fach.F(s.AvgKmh ?? 0, 1)} km/h");
                    File.WriteAllText(Path.ChangeExtension(f, ".svg"), Geo.Svg(trk));
                }
                if (wpt.Count > 0) lines.Add($"{wpt.Count} Wegpunkt(e): {string.Join(", ", wpt.Take(5).Select(w => w.Name ?? "?"))}");
                var gj = Path.ChangeExtension(f, ".geojson"); File.WriteAllText(gj, Geo.GeoJson(trk, wpt, Path.GetFileNameWithoutExtension(f)));
                lines.Add("Gespeichert: " + gj); return string.Join("\n", lines);
            }, "Auswerten"));
        sp.Children.Add(FachTool("geo-coord", "Koordinaten umrechnen", "Dezimalgrad („48,1372 11,5756“) oder Grad/Minuten/Sekunden („48°8'14\"N 11°34'32\"E“).",
            [("c", "Koordinate", "48,1372 11,5756")],
            v => { var (lat, lon) = Geo.ParseCoord(v("c")); var u = Geo.Utm(lat, lon); return $"Dezimal: {lat.ToString("0.000000", System.Globalization.CultureInfo.InvariantCulture)}, {lon.ToString("0.000000", System.Globalization.CultureInfo.InvariantCulture)}\nGrad/Min/Sek: {Geo.Dms(lat, lon)}\nUTM: {u.Zone}{u.Band} {Fach.F(u.Easting, 0)} O / {Fach.F(u.Northing, 0)} N"; }, "Umrechnen"));
        sp.Children.Add(FachTool("geo-dist", "Entfernung (Luftlinie)", "Zwei Koordinaten.",
            [("a", "Von", "52,5200 13,4050"), ("b", "Nach", "48,1372 11,5756")],
            v => { var (la, oa) = Geo.ParseCoord(v("a")); var (lb, ob) = Geo.ParseCoord(v("b")); return $"Luftlinie: {Fach.F(Geo.Haversine(new(la, oa), new(lb, ob)), 1)} km"; }, "Messen"));
        return FachPage("Karten & GPS", sp);
    }
}
