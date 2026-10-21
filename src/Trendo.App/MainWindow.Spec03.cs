using Avalonia.Controls;
using Trendo.Core;

namespace Trendo.App;

/// <summary>SPEC-03: Technik & Simulation – Balken, Wärmedehnung, Einheiten.</summary>
public partial class MainWindow
{
    private void InitSpec03() => RegisterTab(FachTabs, "technik", "Technik & Statik", "SPEC-03", EngineeringView, "technik", "statik", "balken", "träger", "simulation", "biegung", "festigkeit", "einheiten", "umrechnen");

    private static BeamCase BeamFrom(string s) => s.Trim().ToLowerInvariant() switch
    {
        "1" or "mitte" => BeamCase.EinfeldMitte, "2" or "strecke" => BeamCase.EinfeldStrecke, "3" or "kragarm" => BeamCase.KragarmEnde, "4" => BeamCase.KragarmStrecke,
        _ => throw new ArgumentException("Lagerung: 1 = zwei Auflager, Last in der Mitte · 2 = zwei Auflager, gleichmäßige Last · 3 = einseitig eingespannt, Last am Ende · 4 = einseitig, gleichmäßig."),
    };

    private Control EngineeringView()
    {
        var sp = new StackPanel { Spacing = 8 };
        sp.Children.Add(P("Schnelle Abschätzung für Regal, Träger oder Halterung. Ersetzt keine Statik vom Fachmann.", Text));
        sp.Children.Add(FachTool("eng-beam", "Hält der Balken?", "Lagerung: 1 = zwei Auflager, Last Mitte · 2 = zwei Auflager, gleichmäßig · 3 = einseitig eingespannt, Last am Ende · 4 = einseitig, gleichmäßig. Last in kN (bei 2/4 in kN pro Meter). 1 kN ≈ 100 kg.",
            [("case", "Lagerung (1–4)", "1"), ("len", "Länge (m)", "2"), ("load", "Last (kN)", "1"), ("b", "Breite (mm)", "60"), ("h", "Höhe (mm)", "120"), ("mat", "Werkstoff", "Fichte")],
            v => { var m = Engineering.Mat(v("mat")); var r = Engineering.Beam(BeamFrom(v("case")), Fach.Num(v("len"), "Länge"), Fach.Num(v("load"), "Last"), Engineering.Rect(Fach.Num(v("b"), "Breite"), Fach.Num(v("h"), "Höhe")), m);
                   return $"{(r.Ok ? "✓" : "⚠")} {r.Verdict}\nWerkstoff {m.Name} · Biegemoment {Fach.F(r.MaxMoment_kNm, 2)} kNm · Spannung {Fach.F(r.Stress_MPa, 1)} N/mm² · Sicherheit {Fach.F(r.Safety, 2)} · Durchbiegung {Fach.F(r.Deflection_mm, 1)} mm · Gewicht {Fach.F(r.Weight_kg, 1)} kg"; }, "Prüfen"));
        sp.Children.Add(FachTool("eng-heat", "Wärmedehnung", "Wie stark wird ein Bauteil bei Erwärmung länger?",
            [("mat", "Werkstoff", "Aluminium"), ("len", "Länge (m)", "3"), ("dt", "Temperaturänderung (°C)", "40")],
            v => { var m = Engineering.Mat(v("mat")); return $"{m.Name}: {Fach.F(Engineering.ThermalExpansion(m, Fach.Num(v("len")), Fach.Num(v("dt"))), 2)} mm länger"; }));
        sp.Children.Add(FachTool("eng-unit", "Einheiten umrechnen", "Länge (mm, m, in, ft), Kraft (N, kN, kg, lbf), Druck (bar, psi, MPa), Leistung (kW, PS).",
            [("v", "Wert", "100"), ("from", "von", "psi"), ("to", "nach", "bar")],
            v => $"{v("v")} {v("from")} = {Fach.F(Engineering.Convert(Fach.Num(v("v")), v("from"), v("to")), 4)} {v("to")}", "Umrechnen"));
        sp.Children.Add(FachGroup("eng-parts", "Stäbe, Wellen, Profile", false,
            FachTool("eng-hollow", "Hohlprofil", "Rechteckrohr (mm) als Balken prüfen – Werte gehen in „Hält der Balken?“ als Vergleich.",
                [("b", "Breite", "60"), ("h", "Höhe", "40"), ("t", "Wand", "3"), ("len", "Länge (m)", "2"), ("load", "Last Mitte (kN)", "1"), ("mat", "Werkstoff", "Baustahl")],
                v => { var s = EngineeringPlus.HollowRect(Fach.Num(v("b")), Fach.Num(v("h")), Fach.Num(v("t"))); var r = Engineering.Beam(BeamCase.EinfeldMitte, Fach.Num(v("len")), Fach.Num(v("load")), s, Engineering.Mat(v("mat"))); return $"Fläche {Fach.F(s.A, 0)} mm², I {Fach.F(s.I, 0)} mm⁴, W {Fach.F(s.W, 0)} mm³\n{(r.Ok ? "✓" : "⚠")} {r.Verdict} Durchbiegung {Fach.F(r.Deflection_mm, 1)} mm, Gewicht {Fach.F(r.Weight_kg, 1)} kg"; }),
            FachTool("eng-buckle", "Knickt die Stütze?", "Euler-Fall: 1 = unten eingespannt, oben frei · 2 = beide Enden gelenkig · 3 = eingespannt/gelenkig · 4 = beidseitig eingespannt. Rundrohr-Ø und Wand in mm.",
                [("fall", "Fall (1–4)", "2"), ("d", "Ø außen", "40"), ("t", "Wand", "2"), ("len", "Länge (m)", "2,5"), ("mat", "Werkstoff", "Baustahl"), ("f", "Last (kN)", "10")],
                v => { var m = Engineering.Mat(v("mat")); var s = Engineering.Tube(Fach.Num(v("d")), Fach.Num(v("t"))); var fk = EngineeringPlus.EulerKn((int)Fach.Num(v("fall")), m.E_GPa, s.I, Fach.Num(v("len"))); var f = Fach.Num(v("f")); return $"Knicklast {Fach.F(fk, 1)} kN → Sicherheit {Fach.F(fk / f, 2)} {(fk / f >= 3 ? "✓ ausreichend (≥ 3)" : "⚠ zu knapp")}"; }),
            FachTool("eng-axial", "Zug & Druck", "Kraft in kN auf einen Stab mit Querschnitt (mm²).",
                [("f", "Kraft (kN)", "20"), ("a", "Fläche (mm²)", "200"), ("mat", "Werkstoff", "Baustahl"), ("len", "Länge (m)", "1")],
                v => { var m = Engineering.Mat(v("mat")); var r = EngineeringPlus.Axial(Fach.Num(v("f")), Fach.Num(v("a")), m.E_GPa, Fach.Num(v("len"))); return $"Spannung {Fach.F(r.Sigma, 1)} N/mm² (Grenze {Fach.F(m.Yield_MPa, 0)}) · Dehnung {Fach.F(r.StrainPermille, 3)} ‰ · Längenänderung {Fach.F(r.DeltaMm, 3)} mm"; }),
            FachTool("eng-torsion", "Welle verdrehen", "Drehmoment auf eine Vollwelle aus Stahl.",
                [("t", "Drehmoment (Nm)", "50"), ("d", "Ø (mm)", "20"), ("len", "Länge (m)", "0,5")],
                v => { var r = EngineeringPlus.Torsion(Fach.Num(v("t")), Fach.Num(v("d")), Fach.Num(v("len"))); return $"Schubspannung {Fach.F(r.Tau, 1)} N/mm² · Verdrehung {Fach.F(r.PhiDeg, 2)}°"; })));
        sp.Children.Add(FachGroup("eng-mach", "Maschinenelemente & Wärme", false,
            FachTool("eng-bolt", "Schraube anziehen", "Richtwerte für leicht geölte Schrauben (µ = 0,12). Klassen: 8.8, 10.9, 12.9, A2-70.",
                [("size", "Größe", "M8"), ("grade", "Klasse", "8.8")],
                v => { var b = EngineeringPlus.Bolt(v("size"), v("grade")); return $"Anzugsmoment {Fach.F(b.Nm, 1)} Nm · Vorspannkraft ca. {Fach.F(b.KN, 1)} kN"; }, "Nachsehen"),
            FachTool("eng-spring", "Druckfeder", "Federstahl. Maße in mm.",
                [("d", "Draht-Ø", "2"), ("dm", "mittlerer Ø", "20"), ("n", "federnde Windungen", "8"), ("f", "Kraft (N)", "50")],
                v => { var r = EngineeringPlus.Spring(Fach.Num(v("d")), Fach.Num(v("dm")), Fach.Num(v("n")), Fach.Num(v("f"))); return $"Federrate {Fach.F(r.RateNmm, 2)} N/mm → Weg {Fach.F(r.TravelMm, 1)} mm bei der Kraft"; }),
            FachTool("eng-gear", "Zahnräder", "Antrieb z1, Abtrieb z2.",
                [("z1", "Zähne Antrieb", "12"), ("z2", "Zähne Abtrieb", "36"), ("n", "Drehzahl (U/min)", "1500"), ("m", "Drehmoment (Nm)", "2")],
                v => { var g = EngineeringPlus.Gears((int)Fach.Num(v("z1")), (int)Fach.Num(v("z2")), Fach.Num(v("n")), Fach.Num(v("m"))); return $"Übersetzung {Fach.F(g.Ratio, 2)} : 1 → {Fach.F(g.Rpm2, 0)} U/min, {Fach.F(g.Torque2, 2)} Nm"; }),
            FachTool("eng-u", "Wärmeverlust einer Wand", "Schichten als „Dicke m × λ“, mit | getrennt. λ: Ziegel 0,5, Beton 2,1, Mineralwolle 0,035, Putz 0,7.",
                [("layers", "Schichten", "0,015x0,7 | 0,24x0,5 | 0,12x0,035"), ("a", "Fläche (m²)", "10"), ("dt", "Temperaturunterschied (K)", "30")],
                v => { var l = v("layers").Split('|').Select(x => x.ToLowerInvariant().Replace('×', 'x').Split('x')).Select(x => x.Length == 2 ? (Fach.Num(x[0], "Dicke"), Fach.Num(x[1], "λ")) : throw new FormatException("Schicht bitte als „0,24x0,5“.")); var r = EngineeringPlus.UValue(l, Fach.Num(v("a")), Fach.Num(v("dt"))); return $"U-Wert {Fach.F(r.U, 3)} W/(m²K) → {Fach.F(r.Watt, 0)} W Wärmeverlust{(r.U <= 0.24 ? " ✓ (Neubau-Niveau)" : "")}"; }),
            FachTool("eng-mass", "Wie schwer?", "Quader in mm.",
                [("x", "Länge", "1000"), ("y", "Breite", "100"), ("z", "Dicke", "10"), ("mat", "Werkstoff", "Aluminium")],
                v => $"{Fach.F(EngineeringPlus.MassKg(Engineering.Mat(v("mat")), Fach.Num(v("x")) * Fach.Num(v("y")) * Fach.Num(v("z"))), 2)} kg")));
        return FachPage("Technik & Statik", sp);
    }
}
