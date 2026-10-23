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
        sp.Children.Add(FachTool("cnc-feeds", "Drehzahl & Vorschub", "Startwerte für Schaftfräser. Den Werkstoff wählst du in der Liste.",
            [("mat", "Werkstoff", "Aluminium"), ("d", "Fräser-Ø (mm)", "6"), ("z", "Schneiden", "2"), ("max", "max. Drehzahl", "24000")],
            v => { var r = Cnc.Feeds(v("mat"), Fach.Num(v("d")), (int)Fach.Num(v("z")), Fach.Num(v("max"))); return $"Drehzahl {Fach.F(r.Rpm, 0)} U/min · Vorschub {Fach.F(r.FeedMmMin, 0)} mm/min (fz {Fach.F(r.Fz, 3)} mm, Vc {Fach.F(r.Vc, 0)} m/min){(r.Limited ? "\nDie Spindel ist am Limit – Vorschub ist entsprechend kleiner." : "")}"; }));
        string Prog(string name, string g) { var p = Path.Combine(Fach.StoreDir(_core.DataRoot, "cnc"), name + ".nc"); File.WriteAllText(p, g); var r = Cnc.Analyse(g); return $"Dauer ca. {(int)r.Time.TotalHours}:{r.Time.Minutes:00}:{r.Time.Seconds:00} · Fräsweg {Fach.F(r.CutMm / 1000, 2)} m\nGespeichert: {p}\nVor dem Fräsen bitte mit „G-Code prüfen“ und an der Maschine kontrollieren."; }
        sp.Children.Add(FachGroup("cnc-gen", "Programm erzeugen", false,
            FachTool("cnc-pocket", "Rechtecktasche", "Nullpunkt = Ecke links unten, Oberkante Werkstück. Maße in mm.",
                [("w", "Breite", "40"), ("h", "Höhe", "20"), ("dep", "Tiefe", "3"), ("d", "Fräser-Ø", "6"), ("so", "Bahnabstand (% vom Ø)", "50"), ("sd", "Zustellung", "1"), ("f", "Vorschub", "600"), ("pl", "Eintauchen", "200")],
                v => Prog("tasche", CncPlus.Pocket(Fach.Num(v("w")), Fach.Num(v("h")), Fach.Num(v("dep")), Fach.Num(v("d")), Fach.Num(v("so")), Fach.Num(v("sd")), Fach.Num(v("f")), Fach.Num(v("pl")))), "Erzeugen"),
            FachTool("cnc-outline", "Außenkontur", "Rechteck ausfräsen; der Fräser fährt um seinen Radius außen herum.",
                [("w", "Breite", "50"), ("h", "Höhe", "30"), ("dep", "Tiefe", "4"), ("d", "Fräser-Ø", "6"), ("sd", "Zustellung", "2"), ("f", "Vorschub", "800"), ("pl", "Eintauchen", "200")],
                v => Prog("kontur", CncPlus.Outline(Fach.Num(v("w")), Fach.Num(v("h")), Fach.Num(v("dep")), Fach.Num(v("d")), Fach.Num(v("sd")), Fach.Num(v("f")), Fach.Num(v("pl")))), "Erzeugen"),
            FachTool("cnc-bolt", "Lochkreis bohren", "Bohrungen gleichmäßig auf einem Kreis. Spanbruch: alle X mm zurückziehen (0 = durchbohren).",
                [("cx", "Mitte X", "0"), ("cy", "Mitte Y", "0"), ("dia", "Lochkreis-Ø", "100"), ("n", "Anzahl", "6"), ("a", "Startwinkel (°)", "0"), ("dep", "Tiefe", "10"), ("peck", "Spanbruch", "3"), ("f", "Vorschub", "100")],
                v => { var (g, h) = CncPlus.BoltCircle(Fach.Num(v("cx")), Fach.Num(v("cy")), Fach.Num(v("dia")), (int)Fach.Num(v("n")), Fach.Num(v("a")), Fach.Num(v("dep")), Fach.Num(v("peck")), Fach.Num(v("f"))); return string.Join(" · ", h.Select(p => $"({Fach.F(p.X, 2)} | {Fach.F(p.Y, 2)})")) + "\n" + Prog("lochkreis", g); }, "Erzeugen"),
            FachTool("cnc-offset", "Nullpunkt verschieben", "Verschiebt alle absoluten Koordinaten. Speichert eine Kopie mit „.versetzt“.",
                [("file", "Datei", ""), ("dx", "X um", "0"), ("dy", "Y um", "0"), ("dz", "Z um", "0")],
                v => { var f = v("file").Trim().Trim('"'); if (!File.Exists(f)) throw new IOException("Diese Datei gibt es nicht."); var o = Path.Combine(Path.GetDirectoryName(f) ?? ".", Path.GetFileNameWithoutExtension(f) + ".versetzt" + Path.GetExtension(f)); File.WriteAllText(o, CncPlus.Offset(File.ReadAllText(f), Fach.Num(v("dx")), Fach.Num(v("dy")), Fach.Num(v("dz")))); return "Gespeichert: " + o; }, "Verschieben")));
        sp.Children.Add(FachGroup("cnc-calc", "Werkstatt-Rechner", false,
            FachTool("cnc-mrr", "Zerspanleistung", "Reicht die Spindel? Den Werkstoff wählst du in der Liste.",
                [("ae", "Eingriffsbreite (mm)", "3"), ("ap", "Schnitttiefe (mm)", "6"), ("vf", "Vorschub (mm/min)", "1000"), ("mat", "Werkstoff", "Aluminium")],
                v => { var m = CncPlus.Mrr(Fach.Num(v("ae")), Fach.Num(v("ap")), Fach.Num(v("vf")), v("mat")); return $"Spanvolumen {Fach.F(m.Qcm3, 1)} cm³/min · Schnittleistung {Fach.F(m.CutKw, 2)} kW · Spindel braucht ca. {Fach.F(m.SpindleKw, 2)} kW"; }),
            FachTool("cnc-cost", "Teilekosten", "Maschinenzeit je Teil, Rüstzeit wird auf die Menge verteilt.",
                [("min", "Zeit je Teil (min)", "12"), ("qty", "Stückzahl", "10"), ("rate", "Stundensatz (€)", "60"), ("setup", "Rüstzeit (min)", "30"), ("mat", "Material je Teil (€)", "2,50"), ("tool", "Werkzeug je Teil (€)", "0")],
                v => { var c = CncPlus.PartCost(TimeSpan.FromMinutes(Fach.Num(v("min"))), (int)Fach.Num(v("qty")), Fach.Num(v("rate")), Fach.Num(v("setup")), Fach.Num(v("mat")), Fach.Num(v("tool"))); return $"{Fach.F(c.PerPart)} € je Teil · {Fach.F(c.Total)} € gesamt"; }),
            FachTool("cnc-tap", "Kernloch", "Bohrer für metrisches Gewinde, z. B. M8 oder M10x1.", [("t", "Gewinde", "M8")],
                v => { var t = CncPlus.TapDrill(v("t")); return $"M{Fach.F(t.D, 1)} × {Fach.F(t.Pitch, 2)}: Kernlochbohrer Ø {Fach.F(t.Drill, 2)} mm"; }, "Finden")));
        return FachPage("CNC & Fertigung", sp);
    }
}
