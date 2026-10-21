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
        return FachPage("Elektronik", sp);
    }
}
