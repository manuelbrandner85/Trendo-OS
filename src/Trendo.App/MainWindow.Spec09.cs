using Avalonia.Controls;
using Trendo.Core;

namespace Trendo.App;

/// <summary>SPEC-09: 3D-Scan & Rekonstruktion – Punktwolken aufbereiten.</summary>
public partial class MainWindow
{
    private void InitSpec09() => RegisterTab(FachTabs, "scan3d", "3D-Scan", "SPEC-09", ScanView, "3d-scan", "scan", "punktwolke", "ply", "xyz", "lidar", "photogrammetrie", "rekonstruktion");

    private Control ScanView()
    {
        var sp = new StackPanel { Spacing = 8 };
        sp.Children.Add(P("Bereite Scans vom Handy (LiDAR, Polycam, Scaniverse) oder Scanner auf: Punktwolke ansehen, ausdünnen, Störpunkte entfernen und als PLY für Blender oder CloudCompare speichern.", Text));
        sp.Children.Add(FachTool("scan-info", "Scan ansehen", "Datei: .ply, .xyz, .pts, .csv oder .obj.",
            [("file", "Datei", "")],
            v => { var i = PointCloud.Load(v("file").Trim().Trim('"')).Info(); return $"{Fach.F(i.Points, 0)} Punkte{(i.HasColor ? " mit Farbe" : "")}\nGröße: {Fach.F(i.SizeX, 3)} × {Fach.F(i.SizeY, 3)} × {Fach.F(i.SizeZ, 3)} (Einheit der Datei, meist Meter)\nMitte: {Fach.F(i.Center.X, 3)} / {Fach.F(i.Center.Y, 3)} / {Fach.F(i.Center.Z, 3)}"; }, "Lesen"));
        sp.Children.Add(FachTool("scan-clean", "Aufräumen & speichern", "Würfelgröße fürs Ausdünnen (z. B. 0,005 = 5 mm) und Umkreis für Störpunkte. 0 = Schritt überspringen. Das Original bleibt unverändert.",
            [("file", "Datei", ""), ("voxel", "Würfelgröße", "0,005"), ("radius", "Störpunkt-Umkreis", "0,02"), ("n", "min. Nachbarn", "3")],
            v =>
            {
                var f = v("file").Trim().Trim('"'); var pc = PointCloud.Load(f); var before = pc.Points.Count;
                var r = Fach.Num(v("radius"), "Umkreis"); if (r > 0) pc = pc.RemoveOutliers(r, (int)Fach.Num(v("n"), "Nachbarn"));
                var mid = pc.Points.Count; var vx = Fach.Num(v("voxel"), "Würfelgröße"); if (vx > 0) pc = pc.Downsample(vx);
                if (pc.Points.Count == 0) throw new InvalidOperationException("Danach bleibt kein Punkt übrig – Umkreis größer oder Nachbarn kleiner wählen.");
                var outp = Path.Combine(Path.GetDirectoryName(f) ?? ".", Path.GetFileNameWithoutExtension(f) + ".bereinigt.ply"); pc.SavePly(outp);
                return $"{Fach.F(before, 0)} → {Fach.F(before - mid, 0)} Störpunkte entfernt → {Fach.F(pc.Points.Count, 0)} Punkte nach dem Ausdünnen.\nGespeichert: {outp}";
            }, "Aufräumen"));
        return FachPage("3D-Scan", sp);
    }
}
