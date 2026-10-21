namespace Trendo.Core;

/// <summary>SPEC-02 Ausbau: Reihe/parallel, Kondensator- und SMD-Codes, RC, 555, Leiterbahn, Akku, Kabel, Regler, Präfixe.</summary>
public static class ElectronicsPlus
{
    /// <summary>F16: „4k7“, „100n“, „2,2µ“, „1M“, „470p“, „3R3“ → Zahl.</summary>
    public static double Si(string s)
    {
        var t = s.Trim().Replace(" ", "").Replace("Ω", "").Replace("F", "").Replace("Hz", "").Replace("V", "").Replace("A", "");
        var pre = new Dictionary<char, double> { ['p'] = 1e-12, ['n'] = 1e-9, ['u'] = 1e-6, ['µ'] = 1e-6, ['m'] = 1e-3, ['R'] = 1, ['r'] = 1, ['k'] = 1e3, ['K'] = 1e3, ['M'] = 1e6, ['G'] = 1e9 };
        var i = t.IndexOfAny(pre.Keys.ToArray());
        if (i < 0) return Fach.Num(t, "Wert");
        var head = t[..i]; var tail = t[(i + 1)..];
        if (tail != "" && !tail.All(char.IsDigit)) throw new FormatException($"„{s}“ ist kein gültiger Wert.");
        var num = Fach.Num((head == "" ? "0" : head) + (tail == "" ? "" : "," + tail), "Wert");
        return num * pre[t[i]];
    }

    public static string Fmt(double v, string unit)
    {
        var a = Math.Abs(v); (double F, string P) p = a >= 1e9 ? (1e9, "G") : a >= 1e6 ? (1e6, "M") : a >= 1e3 ? (1e3, "k") : a >= 1 || a == 0 ? (1, "") : a >= 1e-3 ? (1e-3, "m") : a >= 1e-6 ? (1e-6, "µ") : a >= 1e-9 ? (1e-9, "n") : (1e-12, "p");
        return $"{Fach.F(v / p.F, 3).TrimEnd('0').TrimEnd(',')} {p.P}{unit}";
    }

    /// <summary>F07: Gesamtwert in Reihe/parallel. Kondensatoren: umgekehrt (cap = true).</summary>
    public static double Combine(IEnumerable<double> values, bool parallel, bool cap = false)
    {
        var v = values.ToList(); if (v.Count == 0 || v.Any(x => x <= 0)) throw new ArgumentException("Bitte positive Werte angeben.");
        var sum = parallel != cap ? 1 / v.Sum(x => 1 / x) : v.Sum(); return sum;
    }

    /// <summary>F08: Kondensator-Code „104“ → 100 nF; Rückweg: pF → 3-stelliger Code.</summary>
    public static double CapCode(string code)
    {
        var c = code.Trim(); if (c.Length != 3 || !c.All(char.IsDigit)) throw new FormatException("Code bitte dreistellig, z. B. 104.");
        return int.Parse(c[..2]) * Math.Pow(10, c[2] - '0') * 1e-12;
    }
    public static string ToCapCode(double farad)
    {
        var pf = farad / 1e-12; if (pf < 10) throw new ArgumentException("Unter 10 pF gibt es keinen 3-stelligen Code.");
        var e = (int)Math.Floor(Math.Log10(pf)) - 1; var two = (int)Math.Round(pf / Math.Pow(10, e)); if (two >= 100) { two /= 10; e++; }
        return $"{two}{e}";
    }

    /// <summary>F09: RC: τ = R·C, fg = 1/(2πRC), 99 % geladen nach 5τ.</summary>
    public static (double Tau, double Cutoff, double Full) Rc(double r, double c) => r <= 0 || c <= 0 ? throw new ArgumentException("R und C müssen größer als 0 sein.") : (r * c, 1 / (2 * Math.PI * r * c), 5 * r * c);

    /// <summary>F10: NE555 astabil: f = 1/(0,693·(R1+2R2)·C), Tastgrad = (R1+R2)/(R1+2R2).</summary>
    public static (double Hz, double DutyPct, double High, double Low) Timer555(double r1, double r2, double c)
    {
        if (r1 <= 0 || r2 <= 0 || c <= 0) throw new ArgumentException("R1, R2 und C müssen größer als 0 sein.");
        var hi = 0.693 * (r1 + r2) * c; var lo = 0.693 * r2 * c; return (1 / (hi + lo), (r1 + r2) / (r1 + 2 * r2) * 100, hi, lo);
    }

    /// <summary>F11: IPC-2221: I = k·ΔT^0,44·A^0,725 (A in mil², außen k=0,048, innen 0,024) → Breite in mm bei Kupferdicke (µm).</summary>
    public static double TraceWidthMm(double amps, double riseC = 10, double copperUm = 35, bool outer = true)
    {
        if (amps <= 0 || riseC <= 0 || copperUm <= 0) throw new ArgumentException("Strom, Erwärmung und Kupferdicke müssen größer als 0 sein.");
        var k = outer ? 0.048 : 0.024; var area = Math.Pow(amps / (k * Math.Pow(riseC, 0.44)), 1 / 0.725); // mil²
        var thickMil = copperUm / 25.4; return Math.Round(area / thickMil * 0.0254, 3);
    }

    /// <summary>F12: Akku-Laufzeit (h) = mAh · Wirkungsgrad / mA.</summary>
    public static double BatteryHours(double mAh, double loadmA, double efficiency = 0.85) => mAh <= 0 || loadmA <= 0 ? throw new ArgumentException("Kapazität und Verbrauch müssen größer als 0 sein.") : Math.Round(mAh * efficiency / loadmA, 2);

    /// <summary>F13: Spannungsabfall Kupfer (hin + zurück): ΔU = 2·L·I·ρ/A, ρ = 0,0178 Ω·mm²/m.</summary>
    public static (double DropV, double DropPct, double LossW) CableDrop(double lengthM, double amps, double mm2, double volts)
    {
        if (lengthM <= 0 || amps <= 0 || mm2 <= 0 || volts <= 0) throw new ArgumentException("Alle Werte müssen größer als 0 sein.");
        var r = 2 * lengthM * 0.0178 / mm2; var du = r * amps; return (Math.Round(du, 3), Math.Round(du / volts * 100, 2), Math.Round(du * amps, 2));
    }

    private static readonly int[] E96 = [100,102,105,107,110,113,115,118,121,124,127,130,133,137,140,143,147,150,154,158,162,165,169,174,178,182,187,191,196,200,205,210,215,221,226,232,237,243,249,255,261,267,274,280,287,294,301,309,316,324,332,340,348,357,365,374,383,392,402,412,422,432,442,453,464,475,487,499,511,523,536,549,562,576,590,604,619,634,649,665,681,698,715,732,750,768,787,806,825,845,866,887,909,931,953,976];

    /// <summary>F14: SMD-Codes: „472“ (3-stellig), „4702“ (4-stellig), „4R7“, EIA-96 „01C“.</summary>
    public static double SmdCode(string code)
    {
        var c = code.Trim().ToUpperInvariant();
        if (c.Contains('R')) return Fach.Num(c.Replace('R', ','), "Code");
        if (c.Length == 3 && char.IsLetter(c[2]) && int.TryParse(c[..2], out var idx) && idx is >= 1 and <= 96)
        {
            var m = c[2] switch { 'Z' => 0.001, 'Y' or 'R' => 0.01, 'X' or 'S' => 0.1, 'A' => 1, 'B' or 'H' => 10, 'C' => 100, 'D' => 1000, 'E' => 10000, 'F' => 100000, _ => throw new FormatException("Unbekannter EIA-96-Buchstabe.") };
            return E96[idx - 1] * m;
        }
        if (c.Length is 3 or 4 && c.All(char.IsDigit)) return int.Parse(c[..^1]) * Math.Pow(10, c[^1] - '0');
        throw new FormatException("SMD-Code z. B. 472, 4702, 4R7 oder 01C.");
    }

    /// <summary>F15: Linearregler: P = (Uin − Uout)·I; Kühlkörper-Wärmewiderstand für max. Sperrschicht 125 °C.</summary>
    public static (double Watt, double TjNoSink, bool NeedsSink, double SinkKPerW) Regulator(double vin, double vout, double amps, double ambient = 25, double rthJa = 50, double rthJc = 5)
    {
        if (vin <= vout) throw new ArgumentException("Die Eingangsspannung muss über der Ausgangsspannung liegen (Dropout beachten).");
        var p = (vin - vout) * amps; var tj = ambient + p * rthJa; var need = tj > 125;
        return (Math.Round(p, 3), Math.Round(tj, 1), need, need ? Math.Round((125 - ambient) / p - rthJc - 0.5, 1) : double.NaN);
    }
}
