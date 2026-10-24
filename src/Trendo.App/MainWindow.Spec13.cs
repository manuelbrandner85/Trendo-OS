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
                    File.WriteAllText(Path.ChangeExtension(f, ".svg"), Geo.Svg(trk)); lines.Add("Streckenbild: " + Path.ChangeExtension(f, ".svg"));
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
        sp.Children.Add(FachGroup("geo-nav", "Navigieren", false,
            FachTool("geo-utm", "UTM → Grad", "Zone, Ost- und Nordwert, z. B. von einer Karte oder einem Vermessungsplan.", [("z", "Zone", "32"), ("e", "Ost (m)", "691607"), ("n", "Nord (m)", "5334760"), ("h", "Halbkugel (N/S)", "N")],
                v => { var (la, lo) = GeoPlus.UtmToLatLon((int)Fach.Num(v("z")), Fach.Num(v("e")), Fach.Num(v("n")), !v("h").Trim().StartsWith("S", StringComparison.OrdinalIgnoreCase)); return $"{la.ToString("0.000000", System.Globalization.CultureInfo.InvariantCulture)}, {lo.ToString("0.000000", System.Globalization.CultureInfo.InvariantCulture)} · {Geo.Dms(la, lo)}"; }, "Umrechnen"),
            FachTool("geo-bear", "Richtung", "In welche Himmelsrichtung liegt das Ziel?", [("a", "Von", "52,5200 13,4050"), ("b", "Nach", "48,1372 11,5756")],
                v => { var (la, oa) = Geo.ParseCoord(v("a")); var (lb, ob) = Geo.ParseCoord(v("b")); var d = GeoPlus.Bearing(new(la, oa), new(lb, ob)); return $"Richtung {Fach.F(d, 1)}° ({GeoPlus.Compass(d)}), {Fach.F(Geo.Haversine(new(la, oa), new(lb, ob)), 1)} km"; }, "Peilen"),
            FachTool("geo-dest", "Zielpunkt", "Wo lande ich, wenn ich X km in Richtung Y gehe?", [("a", "Start", "48,1372 11,5756"), ("deg", "Richtung (°)", "90"), ("km", "Entfernung (km)", "10")],
                v => { var (la, oa) = Geo.ParseCoord(v("a")); var p = GeoPlus.Destination(new(la, oa), Fach.Num(v("deg")), Fach.Num(v("km"))); return $"Ziel: {p.Lat.ToString("0.000000", System.Globalization.CultureInfo.InvariantCulture)}, {p.Lon.ToString("0.000000", System.Globalization.CultureInfo.InvariantCulture)}"; }, "Berechnen"),
            FachTool("geo-area", "Fläche", "Eckpunkte des Grundstücks, je Punkt eine Koordinate, getrennt mit „/“.", [("p", "Eckpunkte", "48,0000 11,0000 / 48,0000 11,0010 / 48,0010 11,0010 / 48,0010 11,0000")],
                v => { var pts = v("p").Split('/').Select(x => Geo.ParseCoord(x.Trim())).Select(c => new GeoPoint(c.Lat, c.Lon)).ToList(); var a = GeoPlus.AreaM2(pts); return $"Fläche: {Fach.F(a, 0)} m² = {Fach.F(a / 10000, 3)} ha"; }, "Messen")));
        sp.Children.Add(FachGroup("geo-tour", "Tour planen", false,
            FachTool("geo-time", "Wanderzeit", "Richtzeit nach DIN 33466 (ohne Pausen).", [("km", "Strecke (km)", "12"), ("up", "Aufstieg (m)", "800"), ("down", "Abstieg (m)", "800")],
                v => { var t = GeoPlus.HikingTime(Fach.Num(v("km")), Fach.Num(v("up")), Fach.Num(v("down"))); return $"Gehzeit ca. {(int)t.TotalHours}:{t.Minutes:00} h (ohne Pausen)"; }, "Berechnen"),
            FachTool("geo-prof", "Höhenprofil & Gehzeit", "GPX-Datei mit Höhen. Speichert ein Höhenprofil als Bild daneben.", [("file", "GPX-Datei", "")],
                v => { var f = v("file").Trim().Trim('"'); var (t, _) = Geo.ReadGpx(f); var s = Geo.Analyse(t); var o = Path.ChangeExtension(f, ".profil.svg"); File.WriteAllText(o, GeoPlus.ProfileSvg(t)); var h = GeoPlus.HikingTime(s.DistanceKm, s.AscentM, s.DescentM); return $"Gehzeit nach DIN ca. {(int)h.TotalHours}:{h.Minutes:00} h\nGespeichert: {o}"; }, "Erstellen"),
            FachTool("geo-simp", "Vereinfachen & GPX speichern", "Weniger Punkte für Navis und Uhren. Toleranz in Metern.", [("file", "GPX-Datei", ""), ("tol", "Toleranz (m)", "5")],
                v => { var f = v("file").Trim().Trim('"'); var (t, w) = Geo.ReadGpx(f); var s = GeoPlus.Simplify(t, Fach.Num(v("tol"))); var o = Path.ChangeExtension(f, ".einfach.gpx"); File.WriteAllText(o, GeoPlus.WriteGpx(s, w, Path.GetFileNameWithoutExtension(f))); return $"{t.Count} → {s.Count} Punkte\nGespeichert: {o}"; }, "Vereinfachen")));
        return FachPage("Karten & GPS", sp);
    }
}
