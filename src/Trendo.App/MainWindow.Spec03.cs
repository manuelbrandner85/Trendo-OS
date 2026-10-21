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
        return FachPage("Technik & Statik", sp);
    }
}
