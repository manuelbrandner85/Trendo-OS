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
        sp.Children.Add(P("Mach aus Fotos oder einem Video ein 3D-Modell. Darunter bleiben die Werkzeuge für eine fertige Punktwolke: ansehen, ausdünnen, Störpunkte entfernen und als PLY speichern.", Text));
        sp.Children.Add(PhotoScanCard());
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
        sp.Children.Add(FachGroup("scan-shape", "Umformen", false,
            FachTool("scan-scale", "Einheit umrechnen", "Von welcher Einheit in welche.", [("file", "Datei", ""), ("from", "Von", "mm"), ("to", "Nach", "m")],
                v => { var f = ScanFile(v); var pc = ScanPlus.Scale(PointCloud.Load(f), ScanPlus.UnitFactor(v("from"), v("to"))); return ScanSave(f, pc, "skaliert", $"Neue Größe: {ScanSize(pc)}"); }, "Umrechnen"),
            FachTool("scan-center", "Zentrieren", "Mitte auf 0, Unterkante auf Höhe 0.", [("file", "Datei", "")],
                v => { var f = ScanFile(v); return ScanSave(f, ScanPlus.Center(PointCloud.Load(f)), "zentriert", "Mitte liegt jetzt bei 0 / 0, Boden bei 0."); }, "Zentrieren"),
            FachTool("scan-rot", "Drehen", "Winkel um die Hochachse (Grad, gegen den Uhrzeigersinn).", [("file", "Datei", ""), ("deg", "Winkel (°)", "90")],
                v => { var f = ScanFile(v); return ScanSave(f, ScanPlus.RotateZ(PointCloud.Load(f), Fach.Num(v("deg"))), "gedreht", $"Um {Fach.F(Fach.Num(v("deg")), 1)}° gedreht."); }, "Drehen"),
            FachTool("scan-crop", "Zuschneiden", "Nur Punkte in der Box behalten: „x1 y1 z1 x2 y2 z2“.", [("file", "Datei", ""), ("box", "Box", "-1 -1 -1 1 1 1")],
                v => { var f = ScanFile(v); var b = v("box").Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(x => Fach.Num(x)).ToArray(); if (b.Length != 6) throw new FormatException("Bitte 6 Zahlen eingeben."); var pc = ScanPlus.Crop(PointCloud.Load(f), new(Math.Min(b[0], b[3]), Math.Min(b[1], b[4]), Math.Min(b[2], b[5])), new(Math.Max(b[0], b[3]), Math.Max(b[1], b[4]), Math.Max(b[2], b[5]))); if (pc.Points.Count == 0) throw new InvalidOperationException("In der Box liegt kein Punkt."); return ScanSave(f, pc, "zugeschnitten", $"{Fach.F(pc.Points.Count, 0)} Punkte behalten."); }, "Zuschneiden"),
            FachTool("scan-merge", "Zusammenführen", "Zwei Scans zu einem vereinen.", [("file", "Datei", ""), ("file2", "Zweite Datei", "")],
                v => { var f = ScanFile(v); var pc = ScanPlus.Merge(PointCloud.Load(f), PointCloud.Load(v("file2").Trim().Trim('"'))); return ScanSave(f, pc, "vereint", $"{Fach.F(pc.Points.Count, 0)} Punkte zusammen."); }, "Vereinen")));
        sp.Children.Add(FachGroup("scan-floor", "Boden", false,
            FachTool("scan-plane", "Boden finden", "Sucht die größte fast waagerechte Fläche. Toleranz in der Einheit der Datei.", [("file", "Datei", ""), ("tol", "Toleranz", "0,01")],
                v => { var pc = PointCloud.Load(ScanFile(v)); var pl = ScanPlus.FindFloor(pc, Fach.Num(v("tol"))); return $"Boden gefunden: {Fach.F(pl.Inliers, 0)} von {Fach.F(pc.Points.Count, 0)} Punkten, Neigung {Fach.F(pl.TiltDeg, 1)}°"; }, "Suchen"),
            FachTool("scan-nofloor", "Boden entfernen", "Entfernt alle Punkte nahe der Bodenfläche.", [("file", "Datei", ""), ("tol", "Toleranz", "0,01")],
                v => { var f = ScanFile(v); var pc = PointCloud.Load(f); var t = Fach.Num(v("tol")); var r = ScanPlus.RemovePlane(pc, ScanPlus.FindFloor(pc, t), t); if (r.Points.Count == 0) throw new InvalidOperationException("Ohne Boden bleibt nichts übrig."); return ScanSave(f, r, "ohne-boden", $"{Fach.F(pc.Points.Count - r.Points.Count, 0)} Bodenpunkte entfernt, {Fach.F(r.Points.Count, 0)} bleiben."); }, "Entfernen"),
            FachTool("scan-vol", "Volumen", "Rauminhalt über dem tiefsten Punkt, aus einem Höhenraster.", [("file", "Datei", ""), ("cell", "Rastergröße", "0,01")],
                v => { var pc = PointCloud.Load(ScanFile(v)); return $"Volumen ca. {Fach.F(ScanPlus.Volume(pc, Fach.Num(v("cell")), pc.Info().Min.Z), 4)} (Einheit³). Hohlräume unter Überhängen zählen mit."; }, "Berechnen")));
        sp.Children.Add(FachGroup("scan-check", "Prüfen & Export", false,
            FachTool("scan-cmp", "Scans vergleichen", "Wie weit weicht Scan A von Scan B ab?", [("file", "Scan A", ""), ("file2", "Scan B", "")],
                v => { var d = ScanPlus.Compare(PointCloud.Load(ScanFile(v)), PointCloud.Load(v("file2").Trim().Trim('"'))); return $"Mittlere Abweichung {Fach.F(d.Mean, 4)} · größte {Fach.F(d.Max, 4)} · RMS {Fach.F(d.Rms, 4)}"; }, "Vergleichen"),
            FachTool("scan-dens", "Punktdichte", "Mittlerer Abstand zum nächsten Nachbarpunkt.", [("file", "Datei", "")],
                v => $"Mittlerer Punktabstand: {Fach.F(ScanPlus.Density(PointCloud.Load(ScanFile(v))), 4)}", "Messen"),
            FachTool("scan-xyz", "Als XYZ speichern", "Einfache Textdatei: eine Zeile pro Punkt.", [("file", "Datei", "")],
                v => { var f = ScanFile(v); var o = Path.Combine(Path.GetDirectoryName(f) ?? ".", Path.GetFileNameWithoutExtension(f) + ".export.xyz"); File.WriteAllText(o, ScanPlus.ToXyz(PointCloud.Load(f))); return "Gespeichert: " + o; }, "Speichern")));
        return FachPage("3D-Scan", sp);
    }

    private static string ScanFile(Func<string, string> v) => v("file").Trim().Trim('"');
    private static string ScanSize(PointCloud pc) { var i = pc.Info(); return $"{Fach.F(i.SizeX, 3)} × {Fach.F(i.SizeY, 3)} × {Fach.F(i.SizeZ, 3)}"; }
    private static string ScanSave(string f, PointCloud pc, string tag, string msg) { var o = Path.Combine(Path.GetDirectoryName(f) ?? ".", $"{Path.GetFileNameWithoutExtension(f)}.{tag}.ply"); pc.SavePly(o); return $"{msg}\nGespeichert: {o}"; }
}
