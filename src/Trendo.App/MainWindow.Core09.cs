using Avalonia.Controls;
using Trendo.Core;

namespace Trendo.App;

/// <summary>CORE-09: Sehen & Roboter – Farbobjekte im Bild finden, Roboterarm rechnen, Fahrprogramm erzeugen.</summary>
public partial class MainWindow
{
    private void InitCore09() => RegisterTab(FachTabs, "roboter", "Sehen & Roboter", "CORE-09", RobotView, "roboter", "robotik", "roboterarm", "kamera", "bilderkennung", "vision", "servo", "kinematik", "greifer");

    private ArmConfig ArmFrom(Func<string, string> v) => new(Fach.Num(v("base"), "Sockelhöhe"), Fach.Num(v("l1"), "Oberarm"), Fach.Num(v("l2"), "Unterarm"));

    private Control RobotView()
    {
        var sp = new StackPanel { Spacing = 8 };
        sp.Children.Add(P("Kameras siehst du hier live. Der Roboter rechnet und probiert, bewegt sich aber erst nach deiner Freigabe. Not-Aus bleibt immer sichtbar."));
        sp.Children.Add(CameraRobotPanel());
        sp.Children.Add(H("Auswerten und planen", 14));
        sp.Children.Add(FachTool("rob-see", "Objekt im Bild finden", "Kamerabild (PNG oder BMP), Kamera senkrecht von oben. Trendo sucht das größte Objekt der Farbe und rechnet die Lage auf dem Tisch aus. Farben: " + string.Join(", ", Vision.Hues.Keys) + ".",
            [("file", "Bild", ""), ("color", "Farbe", "rot"), ("tw", "Tisch-Breite im Bild (mm)", "400"), ("th", "Tisch-Höhe im Bild (mm)", "300")],
            v =>
            {
                var img = Vision.Load(v("file").Trim().Trim('"')); var b = Vision.Find(img, v("color"));
                if (b is null || b.Pixels < 20) return $"Kein {v("color")}es Objekt gefunden ({img.Width} × {img.Height} Pixel durchsucht).";
                var (x, y) = Vision.PixelToTable(b.CenterX, b.CenterY, img.Width, img.Height, Fach.Num(v("tw"), "Breite"), Fach.Num(v("th"), "Höhe"));
                return $"Gefunden: Mitte bei Pixel {Fach.F(b.CenterX, 0)} / {Fach.F(b.CenterY, 0)}, Größe {b.MaxX - b.MinX + 1} × {b.MaxY - b.MinY + 1} Pixel ({Fach.F(b.SharePct, 1)} % des Bildes)\nAuf dem Tisch: x = {Fach.F(x, 1)} mm, y = {Fach.F(y, 1)} mm (von links unten)";
            }, "Suchen"));
        sp.Children.Add(FachTool("rob-ik", "Arm-Stellung berechnen", "Wohin muss der Arm fahren? 3 Achsen: Drehsockel, Schulter, Ellbogen. Maße in mm.",
            [("base", "Sockelhöhe", "60"), ("l1", "Oberarm", "120"), ("l2", "Unterarm", "120"), ("x", "x", "150"), ("y", "y", "50"), ("z", "z", "40")],
            v => { var c = ArmFrom(v); var j = RobotArm.Inverse(c, Fach.Num(v("x")), Fach.Num(v("y")), Fach.Num(v("z"))); var (fx, fy, fz) = RobotArm.Forward(c, j); return $"Sockel {Fach.F(j.Base, 1)}° · Schulter {Fach.F(j.Shoulder, 1)}° · Ellbogen {Fach.F(j.Elbow, 1)}°\nKontrolle: Greifer landet bei {Fach.F(fx, 1)} / {Fach.F(fy, 1)} / {Fach.F(fz, 1)} mm"; }));
        sp.Children.Add(FachTool("rob-prog", "Fahrprogramm erzeugen", "Wegpunkte „x y z“, mit | getrennt. Ergebnis: Winkel je Achse und Dauer, als CSV für Arduino, ESP32 oder ROS.",
            [("base", "Sockelhöhe", "60"), ("l1", "Oberarm", "120"), ("l2", "Unterarm", "120"), ("pts", "Wegpunkte", "150 0 100 | 150 0 40 | 0 150 40 | 0 150 100")],
            v => { var p = RobotArm.Program(ArmFrom(v), v("pts").Replace('|', '\n')); var f = Path.Combine(Fach.StoreDir(_core.DataRoot, "roboter"), "programm.csv"); File.WriteAllText(f, RobotArm.Csv(p)); return string.Join("\n", p.Select(s => $"{s.Index}: {Fach.F(s.J.Base, 1)}° / {Fach.F(s.J.Shoulder, 1)}° / {Fach.F(s.J.Elbow, 1)}° in {Fach.F(s.Seconds, 2)} s")) + $"\nGesamt {Fach.F(p.Sum(s => s.Seconds), 2)} s · Gespeichert: {f}"; }, "Erzeugen"));
        static string Pic(Func<string, string> v, string k = "file") => v(k).Trim().Trim('"');
        (string, string, string)[] ARM = [("base", "Sockelhöhe", "60"), ("l1", "Oberarm", "120"), ("l2", "Unterarm", "120")];
        sp.Children.Add(FachGroup("rob-img", "Bild prüfen", false,
            FachTool("rob-count", "Objekte zählen", "Alle Objekte einer Farbe ab einer Mindestgröße (Pixel).", [("file", "Bild", ""), ("color", "Farbe", "rot"), ("min", "Mindestgröße", "20")],
                v => { var b = VisionPlus.FindAll(Vision.Load(Pic(v)), v("color"), (int)Fach.Num(v("min"))); return b.Count == 0 ? "Keine Objekte gefunden." : $"{b.Count} Objekt(e):\n" + string.Join("\n", b.Take(20).Select((x, i) => $"{i + 1}. Mitte {Fach.F(x.CenterX, 0)} / {Fach.F(x.CenterY, 0)} · {x.Pixels} Pixel")); }, "Zählen"),
            FachTool("rob-exp", "Belichtung & Schärfe", "Ist das Kamerabild brauchbar für die Erkennung?", [("file", "Bild", "")],
                v => { var img = Vision.Load(Pic(v)); var e = VisionPlus.CheckExposure(img); var s = VisionPlus.Sharpness(img); return $"{e.Verdict}\nHelligkeit {Fach.F(e.MeanPct, 0)} % · zu dunkel {Fach.F(e.DarkPct, 1)} % · überstrahlt {Fach.F(e.ClippedPct, 1)} %\nSchärfe {Fach.F(s, 0)} {(s < 100 ? "– wahrscheinlich unscharf, Fokus prüfen" : "– scharf genug")}"; }, "Prüfen"),
            FachTool("rob-motion", "Bewegung erkennen", "Zwei Bilder derselben Kamera vergleichen.", [("file", "Bild vorher", ""), ("file2", "Bild nachher", ""), ("thr", "Empfindlichkeit (0–255)", "30")],
                v => { var m = VisionPlus.Motion(Vision.Load(Pic(v)), Vision.Load(Pic(v, "file2")), Fach.Num(v("thr"))); return m.Moved ? $"Bewegung: {Fach.F(m.ChangedPct, 1)} % des Bildes geändert, Bereich {m.MinX}/{m.MinY} bis {m.MaxX}/{m.MaxY}" : $"Keine nennenswerte Bewegung ({Fach.F(m.ChangedPct, 2)} % geändert)."; }, "Vergleichen"),
            FachTool("rob-mask", "Maske speichern", "Zeigt als Schwarz-Weiß-Bild, was als Farbe erkannt wird – gut zum Einstellen des Lichts.", [("file", "Bild", ""), ("color", "Farbe", "rot")],
                v => { var f = Pic(v); var o = Path.Combine(Path.GetDirectoryName(f) ?? ".", $"{Path.GetFileNameWithoutExtension(f)}.maske-{v("color").Trim()}.png"); File.WriteAllBytes(o, VisionPlus.EncodePng(VisionPlus.MaskImage(Vision.Load(f), v("color")))); return "Gespeichert: " + o; }, "Speichern"),
            FachTool("rob-cal", "Kalibrieren (schräge Kamera)", "Vier Ecken im Bild (Pixel) und dieselben Ecken auf dem Tisch (mm), jeweils „x y“ mit „/“ getrennt, dazu ein Bildpunkt zum Umrechnen.",
                [("src", "Ecken im Bild", "100 50 / 500 60 / 520 400 / 80 380"), ("dst", "Ecken auf dem Tisch", "0 300 / 400 300 / 400 0 / 0 0"), ("pt", "Bildpunkt", "300 220")],
                v => { (double, double)[] P(string k) => v(k).Split('/').Select(x => x.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)).Select(x => x.Length == 2 ? (Fach.Num(x[0]), Fach.Num(x[1])) : throw new FormatException("Jeder Punkt als „x y“.")).ToArray(); var h = VisionPlus.Homography(P("src"), P("dst")); var q = P("pt"); var (x, y) = VisionPlus.Apply(h, q[0].Item1, q[0].Item2); return $"Auf dem Tisch: x = {Fach.F(x, 1)} mm, y = {Fach.F(y, 1)} mm"; }, "Umrechnen")));
        sp.Children.Add(FachGroup("rob-arm", "Arm planen", false,
            FachTool("rob-pick", "Greifbahn (Pick & Place)", "Teil aufnehmen und ablegen. Erzeugt das Fahrprogramm mit Greifer-Befehlen.", [.. ARM, ("pick", "Aufnehmen bei x y z", "150 0 20"), ("place", "Ablegen bei x y z", "0 150 20"), ("lift", "Anfahrhöhe (mm)", "40")],
                v => { (double, double, double) T(string k) { var p = v(k).Split(' ', StringSplitOptions.RemoveEmptyEntries); return p.Length == 3 ? (Fach.Num(p[0]), Fach.Num(p[1]), Fach.Num(p[2])) : throw new FormatException("Bitte „x y z“."); } var path = ArmPlus.PickPlace(T("pick"), T("place"), Fach.Num(v("lift"))); var prog = RobotArm.Program(ArmFrom(v), ArmPlus.ProgramText(path)); var f = Path.Combine(Fach.StoreDir(_core.DataRoot, "roboter"), "greifbahn.csv"); File.WriteAllText(f, RobotArm.Csv(prog).TrimEnd('\n').Split('\n').Select((l, i) => l + ";" + (i == 0 ? "Aktion" : path[i - 1].Action)).Aggregate((a, b) => a + "\n" + b) + "\n"); return string.Join("\n", prog.Select((s, i) => $"{s.Index}. {path[i].Action}: {Fach.F(s.J.Base, 1)}° / {Fach.F(s.J.Shoulder, 1)}° / {Fach.F(s.J.Elbow, 1)}°")) + $"\nGesamt {Fach.F(prog.Sum(s => s.Seconds), 2)} s · Gespeichert: {f}"; }, "Planen"),
            FachTool("rob-reach", "Reichweite", "Wohin kommt der Arm in dieser Höhe? Speichert eine Karte von oben (# = erreichbar, O = Sockel).", [.. ARM, ("z", "Höhe z (mm)", "40"), ("step", "Raster (mm)", "20")],
                v => { var r = ArmPlus.Reach(ArmFrom(v), Fach.Num(v("z")), Fach.Num(v("step"))); var f = Path.Combine(Fach.StoreDir(_core.DataRoot, "roboter"), "reichweite.txt"); File.WriteAllText(f, r.Map); return $"{Fach.F(r.ReachablePct, 1)} % des Quadrats erreichbar, bis {Fach.F(r.MaxRadius, 0)} mm vom Sockel\nKarte: {f}"; }, "Berechnen"),
            FachTool("rob-servo", "Servo-Pulse", "Winkel → Pulsbreite für Hobby-Servos (meist 500–2500 µs).", [("a", "Winkel (°)", "90"), ("amin", "Winkel min", "0"), ("amax", "Winkel max", "180"), ("umin", "Puls min (µs)", "500"), ("umax", "Puls max (µs)", "2500")],
                v => $"{ArmPlus.ServoPulse(Fach.Num(v("a")), Fach.Num(v("amin")), Fach.Num(v("amax")), (int)Fach.Num(v("umin")), (int)Fach.Num(v("umax")))} µs", "Umrechnen"),
            FachTool("rob-prof", "Fahrprofil", "Sanft anfahren und bremsen: Weg (mm oder °), Höchstgeschwindigkeit (je s), Beschleunigung (je s²).", [("d", "Weg", "100"), ("v", "Höchstgeschwindigkeit", "50"), ("a", "Beschleunigung", "100")],
                v => { var p = ArmPlus.Profile(Fach.Num(v("d")), Fach.Num(v("v")), Fach.Num(v("a"))); return p.Triangle ? $"Kurzer Weg – Höchstgeschwindigkeit wird nicht erreicht (Spitze {Fach.F(p.PeakSpeed, 1)}). Dauer {Fach.F(p.Total, 2)} s" : $"Beschleunigen {Fach.F(p.AccelTime, 2)} s · gleichmäßig {Fach.F(p.CruiseTime, 2)} s · Bremsen {Fach.F(p.AccelTime, 2)} s → {Fach.F(p.Total, 2)} s"; }, "Berechnen")));
        return FachPage("Sehen & Roboter", sp);
    }
}
