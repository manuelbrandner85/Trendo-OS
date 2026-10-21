namespace Trendo.Core;

public sealed record Material(string Name, double E_GPa, double Yield_MPa, double Density, double Alpha_perK);
public enum BeamCase { EinfeldMitte, EinfeldStrecke, KragarmEnde, KragarmStrecke }
public sealed record BeamResult(double MaxMoment_kNm, double Stress_MPa, double Deflection_mm, double Safety, double Weight_kg, bool Ok, string Verdict);

/// <summary>SPEC-03: Technik & Simulation – Balkenbiegung, Querschnitte, Wärmedehnung, Einheiten.</summary>
public static class Engineering
{
    public static readonly IReadOnlyList<Material> Materials =
    [
        new("Baustahl S235", 210, 235, 7850, 12e-6), new("Edelstahl 1.4301", 200, 190, 7900, 16e-6),
        new("Aluminium 6060", 70, 150, 2700, 23e-6), new("Fichte/Nadelholz C24", 11, 24, 420, 5e-6), new("Beton C25", 31, 25, 2400, 10e-6),
    ];

    public static Material Mat(string name) => Materials.FirstOrDefault(m => m.Name.StartsWith(name, StringComparison.OrdinalIgnoreCase) || m.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException("Unbekannter Werkstoff. Möglich: " + string.Join(", ", Materials.Select(m => m.Name)));

    /// <summary>Rechteck b×h (mm) oder Rohr/Vollkreis (d außen, t Wand; t=0 → voll). Ergebnis: Fläche mm², I mm⁴, W mm³.</summary>
    public static (double A, double I, double W) Rect(double b, double h) => b <= 0 || h <= 0 ? throw new ArgumentException("Breite und Höhe müssen größer als 0 sein.") : (b * h, b * h * h * h / 12, b * h * h / 6);
    public static (double A, double I, double W) Tube(double d, double t = 0)
    {
        if (d <= 0 || t < 0 || 2 * t >= d && t > 0) throw new ArgumentException("Durchmesser muss größer als 0 und die Wand dünner als der halbe Durchmesser sein.");
        var di = t == 0 ? 0 : d - 2 * t;
        var i = Math.PI / 64 * (Math.Pow(d, 4) - Math.Pow(di, 4));
        return (Math.PI / 4 * (d * d - di * di), i, i / (d / 2));
    }

    /// <summary>Balken: Länge m, Last kN (Einzellast) bzw. kN/m (Streckenlast), Querschnitt aus I/W in mm.</summary>
    public static BeamResult Beam(BeamCase c, double L_m, double load, (double A, double I, double W) s, Material m, double limitRatio = 300)
    {
        if (L_m <= 0) throw new ArgumentException("Die Länge muss größer als 0 sein.");
        double L = L_m * 1000, E = m.E_GPa * 1000; // mm, N/mm²
        double M, f; // N·mm, mm
        switch (c)
        {
            case BeamCase.EinfeldMitte: { var F = load * 1000; M = F * L / 4; f = F * L * L * L / (48 * E * s.I); break; }
            case BeamCase.EinfeldStrecke: { var q = load; M = q * L * L / 8; f = 5 * q * Math.Pow(L, 4) / (384 * E * s.I); break; } // kN/m = N/mm
            case BeamCase.KragarmEnde: { var F = load * 1000; M = F * L; f = F * L * L * L / (3 * E * s.I); break; }
            default: { var q = load; M = q * L * L / 2; f = q * Math.Pow(L, 4) / (8 * E * s.I); break; }
        }
        var sigma = M / s.W; var safety = sigma <= 0 ? double.PositiveInfinity : m.Yield_MPa / sigma;
        var limit = L / (c is BeamCase.KragarmEnde or BeamCase.KragarmStrecke ? limitRatio / 2 : limitRatio);
        var weight = s.A / 1e6 * L_m * m.Density;
        var ok = safety >= 1.5 && f <= limit;
        var verdict = safety < 1 ? "Bricht bzw. verformt sich bleibend – viel zu schwach." : safety < 1.5 ? "Zu knapp: Sicherheit unter 1,5." : f > limit ? $"Hält, biegt sich aber zu stark (mehr als L/{(int)(L / limit)})." : "Hält mit ausreichender Reserve.";
        return new(Math.Round(M / 1e6, 3), Math.Round(sigma, 2), Math.Round(f, 2), Math.Round(safety, 2), Math.Round(weight, 2), ok, verdict);
    }

    /// <summary>Längenänderung durch Temperatur: ΔL = α·L·ΔT (mm).</summary>
    public static double ThermalExpansion(Material m, double L_m, double dT) => Math.Round(m.Alpha_perK * L_m * 1000 * dT, 3);

    private static readonly Dictionary<string, (string Dim, double F)> Units = new(StringComparer.OrdinalIgnoreCase)
    {
        ["mm"] = ("L", 0.001), ["cm"] = ("L", 0.01), ["m"] = ("L", 1), ["km"] = ("L", 1000), ["in"] = ("L", 0.0254), ["zoll"] = ("L", 0.0254), ["ft"] = ("L", 0.3048),
        ["n"] = ("F", 1), ["kn"] = ("F", 1000), ["kg"] = ("F", 9.80665), ["lbf"] = ("F", 4.448222),
        ["pa"] = ("P", 1), ["kpa"] = ("P", 1000), ["mpa"] = ("P", 1e6), ["bar"] = ("P", 1e5), ["psi"] = ("P", 6894.757),
        ["nm"] = ("T", 1), ["knm"] = ("T", 1000), ["w"] = ("W", 1), ["kw"] = ("W", 1000), ["ps"] = ("W", 735.49875),
    };

    public static double Convert(double v, string from, string to)
    {
        if (!Units.TryGetValue(from.Trim(), out var a) || !Units.TryGetValue(to.Trim(), out var b)) throw new ArgumentException("Unbekannte Einheit. Möglich: " + string.Join(", ", Units.Keys));
        if (a.Dim != b.Dim) throw new ArgumentException("Diese Einheiten passen nicht zusammen.");
        return v * a.F / b.F;
    }
}
