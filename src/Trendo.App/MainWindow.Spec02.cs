using Avalonia.Controls;
using Trendo.Core;

namespace Trendo.App;

/// <summary>SPEC-02: Elektronik & Platinen – Rechner und Stückliste.</summary>
public partial class MainWindow
{
    private void InitSpec02() => RegisterTab(FachTabs, "elektronik", "Elektronik", "SPEC-02", ElectronicsView, "elektronik", "widerstand", "led", "platine", "pcb", "farbcode", "ohm", "spannung", "stückliste");

    private Control ElectronicsView()
    {
        var sp = new StackPanel { Spacing = 8 };
        sp.Children.Add(FachTool("el-code", "Widerstand: Farbringe lesen", "Farben von links nach rechts, z. B. „gelb violett rot gold“. Oder einen Wert wie „4700“ eingeben, dann zeigt Trendo die Farben.",
            [("bands", "Farben oder Wert in Ω", "gelb violett rot gold")],
            v => { var s = v("bands"); if (s.Any(char.IsDigit)) { var o = Fach.Num(s, "Wert"); return $"{Electronics.Ohm(o)} = {Electronics.EncodeBands(o)}"; } var (ohm, t) = Electronics.DecodeBands(s); return $"{Electronics.Ohm(ohm)} ± {Fach.F(t, 2)} %"; }, "Umrechnen"));
        sp.Children.Add(FachTool("el-ohm", "Ohmsches Gesetz", "Zwei Werte eintragen, den dritten auf 0 lassen.",
            [("u", "Spannung (V)", "12"), ("i", "Strom (A)", "0"), ("r", "Widerstand (Ω)", "220")],
            v => { var (u, i, r, p) = Electronics.OhmsLaw(Fach.Num(v("u")), Fach.Num(v("i")), Fach.Num(v("r"))); return $"U = {Fach.F(u, 3)} V · I = {Fach.F(i * 1000, 2)} mA · R = {Electronics.Ohm(r)} · Leistung = {Fach.F(p, 3)} W"; }));
        sp.Children.Add(FachTool("el-led", "LED-Vorwiderstand", "Typisch: rote LED 2 V, weiße/blaue 3,2 V, 20 mA.",
            [("vs", "Versorgung (V)", "5"), ("vf", "LED-Spannung (V)", "2"), ("ma", "Strom (mA)", "20"), ("n", "LEDs in Reihe", "1")],
            v => { var x = Electronics.LedResistor(Fach.Num(v("vs")), Fach.Num(v("vf")), Fach.Num(v("ma")), (int)Fach.Num(v("n"))); return $"Rechnerisch {Electronics.Ohm(x.Exact)} → nimm {Electronics.Ohm(x.E12)} ({Electronics.EncodeBands(x.E12)}). Strom {Fach.F(x.Current, 1)} mA, Widerstand braucht {Fach.F(x.Power * 1000, 0)} mW{(x.Power > 0.25 ? " – mehr als 1/4 W, größeren Widerstand nehmen!" : ".")}"; }));
        sp.Children.Add(FachTool("el-div", "Spannungsteiler", "Findet passende Normwerte (E24) für die Wunschspannung.",
            [("vin", "Eingang (V)", "12"), ("vout", "Wunsch (V)", "3,3"), ("r2", "R2 (Ω)", "10000")],
            v => { var d = Electronics.Divider(Fach.Num(v("vin")), Fach.Num(v("vout")), Fach.Num(v("r2"))); return $"R1 = {Electronics.Ohm(d.R1)}, R2 = {Electronics.Ohm(d.R2)} → {Fach.F(d.Vout, 3)} V (Abweichung {Fach.F(d.ErrorPct, 2)} %)"; }));
        sp.Children.Add(FachTool("el-bom", "Stückliste", "Eine Zeile pro Bauteil: „R1, 10k, 0805, 1, 0,02“. Gleiche Teile werden zusammengefasst; die Liste wird als CSV gespeichert.",
            [("text", "Zeilen (mit | trennen)", "R1, 10k, 0805, 1, 0,02 | R2, 10k, 0805, 1, 0,02 | C1, 100n, 0603, 1, 0,03")],
            v => { var bom = Electronics.ParseBom(v("text").Replace('|', '\n')); var p = Path.Combine(Fach.StoreDir(_core.DataRoot, "elektronik"), "stueckliste.csv"); File.WriteAllText(p, Electronics.BomCsv(bom)); return string.Join("\n", bom.Select(b => $"{b.Qty}× {b.Value} {b.Part} ({b.Ref})")) + $"\nSumme: {Fach.F(bom.Sum(b => b.Qty * b.UnitPrice))} €\nGespeichert: {p}"; }, "Zusammenfassen"));
        sp.Children.Add(FachGroup("el-calc", "Bauteile & Schaltungen", false,
            FachTool("el-comb", "Reihe & parallel", "Werte mit Komma oder Leerzeichen trennen, z. B. „4k7 10k 2k2“. Art: r = Widerstände, c = Kondensatoren.",
                [("vals", "Werte", "1k 1k 2k"), ("mode", "reihe/parallel", "parallel"), ("kind", "r oder c", "r")],
                v => { var vals = v("vals").Split([' ', ';'], StringSplitOptions.RemoveEmptyEntries).Select(ElectronicsPlus.Si); var cap = v("kind").Trim().StartsWith("c", StringComparison.OrdinalIgnoreCase); var x = ElectronicsPlus.Combine(vals, v("mode").Trim().StartsWith("p", StringComparison.OrdinalIgnoreCase), cap); return "Gesamt: " + ElectronicsPlus.Fmt(x, cap ? "F" : "Ω"); }),
            FachTool("el-cap", "Kondensator-Aufdruck", "Code wie „104“ oder Wert wie „100n“.",
                [("c", "Code oder Wert", "104")],
                v => { var t = v("c").Trim(); return t.Length == 3 && t.All(char.IsDigit) ? $"{t} = {ElectronicsPlus.Fmt(ElectronicsPlus.CapCode(t), "F")}" : $"{ElectronicsPlus.Fmt(ElectronicsPlus.Si(t), "F")} = Code {ElectronicsPlus.ToCapCode(ElectronicsPlus.Si(t))}"; }, "Umrechnen"),
            FachTool("el-smd", "SMD-Widerstand", "Aufdruck wie 472, 4702, 4R7 oder 01C.",
                [("c", "Aufdruck", "472")], v => $"{v("c")} = {Electronics.Ohm(ElectronicsPlus.SmdCode(v("c")))}", "Lesen"),
            FachTool("el-rc", "RC-Glied", "Zeitkonstante und Grenzfrequenz (Tiefpass).",
                [("r", "R", "10k"), ("c", "C", "100n")],
                v => { var (t, f, full) = ElectronicsPlus.Rc(ElectronicsPlus.Si(v("r")), ElectronicsPlus.Si(v("c"))); return $"τ = {ElectronicsPlus.Fmt(t, "s")} · Grenzfrequenz {ElectronicsPlus.Fmt(f, "Hz")} · voll geladen nach {ElectronicsPlus.Fmt(full, "s")}"; }),
            FachTool("el-555", "Blinker mit NE555", "Astabile Schaltung.",
                [("r1", "R1", "1k"), ("r2", "R2", "10k"), ("c", "C", "10u")],
                v => { var x = ElectronicsPlus.Timer555(ElectronicsPlus.Si(v("r1")), ElectronicsPlus.Si(v("r2")), ElectronicsPlus.Si(v("c"))); return $"{ElectronicsPlus.Fmt(x.Hz, "Hz")} · an {Fach.F(x.DutyPct, 1)} % ({ElectronicsPlus.Fmt(x.High, "s")} an, {ElectronicsPlus.Fmt(x.Low, "s")} aus)"; })));
        sp.Children.Add(FachGroup("el-power", "Platine & Stromversorgung", false,
            FachTool("el-trace", "Leiterbahnbreite", "Nach IPC-2221, Außenlage, 35 µm Kupfer.",
                [("a", "Strom (A)", "2"), ("dt", "Erwärmung (°C)", "10"), ("cu", "Kupfer (µm)", "35")],
                v => $"Mindestens {Fach.F(ElectronicsPlus.TraceWidthMm(Fach.Num(v("a")), Fach.Num(v("dt")), Fach.Num(v("cu"))), 2)} mm breit (Außenlage)"),
            FachTool("el-bat", "Akku-Laufzeit", "Wirkungsgrad 85 % eingerechnet.",
                [("mah", "Kapazität (mAh)", "2000"), ("ma", "Verbrauch (mA)", "50")],
                v => { var h = ElectronicsPlus.BatteryHours(Fach.Num(v("mah")), Fach.Num(v("ma"))); return $"ca. {Fach.F(h, 1)} Stunden ({Fach.F(h / 24, 1)} Tage)"; }),
            FachTool("el-cable", "Kabel: Spannungsverlust", "Kupfer, Hin- und Rückleiter.",
                [("l", "Länge (m)", "5"), ("a", "Strom (A)", "10"), ("q", "Querschnitt (mm²)", "1,5"), ("u", "Spannung (V)", "12")],
                v => { var d = ElectronicsPlus.CableDrop(Fach.Num(v("l")), Fach.Num(v("a")), Fach.Num(v("q")), Fach.Num(v("u"))); return $"{(d.DropPct > 3 ? "⚠" : "✓")} Verlust {Fach.F(d.DropV, 2)} V ({Fach.F(d.DropPct, 1)} %), {Fach.F(d.LossW, 1)} W Wärme{(d.DropPct > 3 ? " – dickeres Kabel nehmen (Richtwert max. 3 %)." : "")}"; }),
            FachTool("el-reg", "Spannungsregler (z. B. 7805)", "Wie warm wird er? Ohne Kühlkörper ca. 50 K/W.",
                [("vin", "Eingang (V)", "12"), ("vout", "Ausgang (V)", "5"), ("a", "Strom (A)", "0,5")],
                v => { var r = ElectronicsPlus.Regulator(Fach.Num(v("vin")), Fach.Num(v("vout")), Fach.Num(v("a"))); return r.NeedsSink ? $"⚠ {Fach.F(r.Watt, 2)} W Verlust – ohne Kühlkörper {Fach.F(r.TjNoSink, 0)} °C. Kühlkörper mit höchstens {Fach.F(r.SinkKPerW, 1)} K/W nötig, oder Schaltregler nehmen." : $"✓ {Fach.F(r.Watt, 2)} W Verlust, ca. {Fach.F(r.TjNoSink, 0)} °C – kein Kühlkörper nötig."; }),
            FachTool("el-si", "Einheiten-Präfixe", "Wert mit Präfix (p, n, µ/u, m, k, M, G) lesen.",
                [("v", "Wert", "4k7")], v => $"{v("v")} = {Fach.F(ElectronicsPlus.Si(v("v")), 12).TrimEnd('0').TrimEnd(',')}", "Lesen")));
        return FachPage("Elektronik", sp);
    }
}
