namespace Trendo.Core;

/// <summary>SPEC-03 Ausbau: Hohlprofil, Knicken, Schrauben, Zug/Druck, Torsion, Feder, Zahnräder, U-Wert, Gewicht.</summary>
public static class EngineeringPlus
{
    /// <summary>F08: Rechteck-Hohlprofil b×h, Wand t (mm).</summary>
    public static (double A, double I, double W) HollowRect(double b, double h, double t)
    {
        if (b <= 0 || h <= 0 || t <= 0 || 2 * t >= Math.Min(b, h)) throw new ArgumentException("Wand muss dünner als die halbe Seite sein.");
        double bi = b - 2 * t, hi = h - 2 * t, i = (b * h * h * h - bi * hi * hi * hi) / 12; return (b * h - bi * hi, i, i / (h / 2));
    }

    /// <summary>F09: Euler-Knicklast F = π²EI/(βL)², Fälle 1–4 (β = 2; 1; 0,699; 0,5). E in GPa, I mm⁴, L m → kN.</summary>
    public static double EulerKn(int fall, double eGPa, double iMm4, double lM)
    {
        var beta = fall switch { 1 => 2.0, 2 => 1.0, 3 => 0.699, 4 => 0.5, _ => throw new ArgumentException("Euler-Fall 1 bis 4.") };
        if (lM <= 0) throw new ArgumentException("Länge muss größer als 0 sein.");
        var lk = beta * lM * 1000; return Math.Round(Math.PI * Math.PI * eGPa * 1000 * iMm4 / (lk * lk) / 1000, 2);
    }

    // Festigkeitsklasse 8.8, µ = 0,12: Anzugsmoment (Nm), Vorspannkraft (kN) – Richtwerte (VDI 2230, gängige Tabellen)
    public static IEnumerable<string> BoltSizes => Bolts88.Keys;
    private static readonly Dictionary<string, (double Nm, double KN)> Bolts88 = new(StringComparer.OrdinalIgnoreCase)
    { ["M3"] = (1.3, 2.3), ["M4"] = (3.0, 4.0), ["M5"] = (5.9, 6.6), ["M6"] = (10, 9.3), ["M8"] = (25, 17), ["M10"] = (49, 27), ["M12"] = (85, 40), ["M16"] = (210, 75), ["M20"] = (410, 120), ["M24"] = (710, 170) };

    /// <summary>F10: Anzugsmoment/Vorspannkraft für Klasse 8.8, 10.9 (×1,47), 12.9 (×1,72), A2-70 (×0,7).</summary>
    public static (double Nm, double KN) Bolt(string size, string grade = "8.8")
    {
        if (!Bolts88.TryGetValue(size.Trim(), out var b)) throw new ArgumentException("Größen: " + string.Join(", ", Bolts88.Keys));
        var f = grade.Trim() switch { "8.8" => 1.0, "10.9" => 1.47, "12.9" => 1.72, "A2-70" or "a2" or "A2" => 0.7, _ => throw new ArgumentException("Klassen: 8.8, 10.9, 12.9, A2-70.") };
        return (Math.Round(b.Nm * f, 1), Math.Round(b.KN * f, 1));
    }

    /// <summary>F11: σ = F/A, ε = σ/E, ΔL = ε·L. Kraft kN, A mm², E GPa, L m → MPa, ‰, mm.</summary>
    public static (double Sigma, double StrainPermille, double DeltaMm) Axial(double fKn, double aMm2, double eGPa, double lM)
    {
        if (aMm2 <= 0 || eGPa <= 0) throw new ArgumentException("Fläche und E-Modul müssen größer als 0 sein.");
        var s = fKn * 1000 / aMm2; var e = s / (eGPa * 1000); return (Math.Round(s, 2), Math.Round(e * 1000, 4), Math.Round(e * lM * 1000, 3));
    }

    /// <summary>F12: Welle: τ = 16T/(πd³), φ = TL/(GJ) mit G = E/2,6. T Nm, d mm, L m.</summary>
    public static (double Tau, double PhiDeg) Torsion(double tNm, double dMm, double lM, double eGPa = 210)
    {
        if (dMm <= 0) throw new ArgumentException("Durchmesser muss größer als 0 sein.");
        var t = tNm * 1000; var tau = 16 * t / (Math.PI * Math.Pow(dMm, 3)); var j = Math.PI * Math.Pow(dMm, 4) / 32; var g = eGPa * 1000 / 2.6;
        return (Math.Round(tau, 2), Math.Round(t * lM * 1000 / (g * j) * 180 / Math.PI, 3));
    }

    /// <summary>F13: Druckfeder: c = G d⁴ / (8 D³ n), G Stahl 81,5 GPa. d Draht, D mittlerer Ø (mm).</summary>
    public static (double RateNmm, double TravelMm) Spring(double dMm, double dMeanMm, double nActive, double forceN, double gGPa = 81.5)
    {
        if (dMm <= 0 || dMeanMm <= dMm || nActive <= 0) throw new ArgumentException("Drahtdurchmesser < mittlerer Durchmesser, Windungen > 0.");
        var c = gGPa * 1000 * Math.Pow(dMm, 4) / (8 * Math.Pow(dMeanMm, 3) * nActive); return (Math.Round(c, 3), Math.Round(forceN / c, 2));
    }

    /// <summary>F14: Zahnräder z1 → z2: i = z2/z1, n2 = n1/i, M2 = M1·i·η.</summary>
    public static (double Ratio, double Rpm2, double Torque2) Gears(int z1, int z2, double rpm1, double torque1, double efficiency = 0.98)
    {
        if (z1 < 6 || z2 < 6) throw new ArgumentException("Zahnräder brauchen mindestens 6 Zähne.");
        var i = (double)z2 / z1; return (Math.Round(i, 4), Math.Round(rpm1 / i, 2), Math.Round(torque1 * i * efficiency, 3));
    }

    /// <summary>F15: U = 1/(Rsi + Σ d/λ + Rse), Wärmestrom Q = U·A·ΔT. Schichten (Dicke m, λ W/mK).</summary>
    public static (double U, double Watt) UValue(IEnumerable<(double D, double Lambda)> layers, double areaM2, double dT, double rsi = 0.13, double rse = 0.04)
    {
        var l = layers.ToList(); if (l.Count == 0 || l.Any(x => x.D <= 0 || x.Lambda <= 0)) throw new ArgumentException("Jede Schicht braucht Dicke und λ größer 0.");
        var u = 1 / (rsi + l.Sum(x => x.D / x.Lambda) + rse); return (Math.Round(u, 3), Math.Round(u * areaM2 * dT, 1));
    }

    /// <summary>F16: Masse = Volumen × Dichte; Volumen aus Quader (mm) oder Querschnitt × Länge.</summary>
    public static double MassKg(Material m, double volumeMm3) => volumeMm3 < 0 ? throw new ArgumentException("Volumen darf nicht negativ sein.") : Math.Round(volumeMm3 / 1e9 * m.Density, 3);
}
