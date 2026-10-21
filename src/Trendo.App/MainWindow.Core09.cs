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
        return FachPage("Sehen & Roboter", sp);
    }
}
