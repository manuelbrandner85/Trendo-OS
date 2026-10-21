using Avalonia.Controls;
using Trendo.Core;

namespace Trendo.App;

/// <summary>SPEC-14: Fertigung & CNC.</summary>
public partial class MainWindow
{
    private void InitSpec14() => RegisterTab(FachTabs, "cnc", "CNC & Fertigung", "SPEC-14", CncView, "cnc", "fräse", "fräsen", "g-code", "gcode", "drehzahl", "vorschub", "laser", "fertigung");

    private Control CncView()
    {
        var sp = new StackPanel { Spacing = 8 };
        sp.Children.Add(P("Prüfe ein Fräs- oder Laserprogramm, bevor die Maschine läuft: Wege, Zeit, Größe, Werkzeuge und typische Fehler. Die Vorschau zeigt die Bahnen von oben.", Text));
        sp.Children.Add(FachTool("cnc-check", "G-Code prüfen", "Datei .nc, .gcode, .tap oder .ngc. Arbeitsraum der Maschine in mm.",
            [("file", "Datei", ""), ("x", "Arbeitsraum X", "300"), ("y", "Arbeitsraum Y", "300"), ("z", "Arbeitsraum Z", "60"), ("rapid", "Eilgang (mm/min)", "3000")],
            v =>
            {
                var f = v("file").Trim().Trim('"'); if (!File.Exists(f)) throw new IOException("Diese Datei gibt es nicht.");
                var r = Cnc.Analyse(File.ReadAllText(f), Fach.Num(v("rapid"), "Eilgang"), (Fach.Num(v("x")), Fach.Num(v("y")), Fach.Num(v("z"))));
                var svg = Path.ChangeExtension(f, ".vorschau.svg"); File.WriteAllText(svg, r.Svg);
                return $"{(r.Warnings.Count == 0 ? "✓ Keine Auffälligkeiten." : "⚠ " + string.Join("\n⚠ ", r.Warnings))}\nDauer ca. {(int)r.Time.TotalHours}:{r.Time.Minutes:00}:{r.Time.Seconds:00} · Fräsweg {Fach.F(r.CutMm / 1000, 2)} m · Eilgang {Fach.F(r.RapidMm / 1000, 2)} m · {r.Moves} Bewegungen\nGröße {Fach.F(r.Box.X1 - r.Box.X0, 1)} × {Fach.F(r.Box.Y1 - r.Box.Y0, 1)} mm, Z {Fach.F(r.Box.Z0, 2)} bis {Fach.F(r.Box.Z1, 2)} · Werkzeuge: {(r.Tools.Count == 0 ? "–" : string.Join(", ", r.Tools.Select(t => "T" + t)))} · Spindel max. {Fach.F(r.MaxSpindle, 0)} U/min\nVorschau: {svg}";
            }, "Prüfen"));
        sp.Children.Add(FachTool("cnc-feeds", "Drehzahl & Vorschub", "Startwerte für Schaftfräser. Werkstoffe: " + string.Join(", ", Cnc.Materials.Keys) + ".",
            [("mat", "Werkstoff", "Aluminium"), ("d", "Fräser-Ø (mm)", "6"), ("z", "Schneiden", "2"), ("max", "max. Drehzahl", "24000")],
            v => { var r = Cnc.Feeds(v("mat"), Fach.Num(v("d")), (int)Fach.Num(v("z")), Fach.Num(v("max"))); return $"Drehzahl {Fach.F(r.Rpm, 0)} U/min · Vorschub {Fach.F(r.FeedMmMin, 0)} mm/min (fz {Fach.F(r.Fz, 3)} mm, Vc {Fach.F(r.Vc, 0)} m/min){(r.Limited ? "\nDie Spindel ist am Limit – Vorschub ist entsprechend kleiner." : "")}"; }));
        return FachPage("CNC & Fertigung", sp);
    }
}
