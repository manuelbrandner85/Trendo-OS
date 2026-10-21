using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>SPEC-14 Ausbau: Programme versetzen und erzeugen, Zerspanleistung, Kosten, Kernloch.</summary>
public static partial class CncPlus
{
    static string I(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
    [GeneratedRegex(@"([XYZ])\s*([-+]?\d*\.?\d+)", RegexOptions.IgnoreCase)] private static partial Regex Axis();

    /// <summary>F14: absolute X/Y/Z-Werte verschieben (relative Abschnitte G91 und Kommentare bleiben unverändert).</summary>
    public static string Offset(string gcode, double dx, double dy, double dz)
    {
        var abs = true; var sb = new StringBuilder();
        foreach (var line in gcode.Replace("\r", "").Split('\n'))
        {
            var sc = line.IndexOf(';'); var code = sc >= 0 ? line[..sc] : line; var rest = sc >= 0 ? line[sc..] : "";
            var parts = Regex.Split(code, @"(\(.*?\))");
            for (var i = 0; i < parts.Length; i += 2)
            {
                var u = parts[i].ToUpperInvariant();
                if (Regex.IsMatch(u, @"G0*91(?!\d)")) abs = false; if (Regex.IsMatch(u, @"G0*90(?!\d)")) abs = true;
                if (abs && !Regex.IsMatch(u, @"G0*(53|92)(?!\d)")) parts[i] = Axis().Replace(parts[i], m => { var v = double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture); var d = char.ToUpperInvariant(m.Groups[1].Value[0]) switch { 'X' => dx, 'Y' => dy, _ => dz }; return m.Groups[1].Value + I(v + d); });
            }
            sb.Append(string.Concat(parts)).Append(rest).Append('\n');
        }
        return sb.ToString().TrimEnd('\n') + "\n";
    }

    static int Passes(double depth, double step) { if (depth <= 0 || step <= 0) throw new ArgumentException("Tiefe und Zustellung müssen größer als 0 sein."); return (int)Math.Ceiling(depth / step - 1e-9); }
    static StringBuilder Head(string title, double safe) => new($"({title} – Trendo OS)\nG21 G90 G17\nG0 Z{I(safe)}\n");

    /// <summary>F15: Rechtecktasche ab (0,0) im Zickzack, Ecke links unten. Fräser bleibt innerhalb.</summary>
    public static string Pocket(double w, double h, double depth, double toolD, double stepoverPct, double stepdown, double feed, double plunge, double safe = 5)
    {
        var r = toolD / 2; if (toolD <= 0 || w < toolD || h < toolD) throw new ArgumentException("Die Tasche muss mindestens so groß wie der Fräser sein.");
        if (stepoverPct <= 0 || stepoverPct > 100) throw new ArgumentException("Bahnüberlappung zwischen 1 und 100 %.");
        var step = toolD * stepoverPct / 100; var sb = Head($"Tasche {I(w)}×{I(h)}×{I(depth)}", safe); var n = Passes(depth, stepdown);
        for (var p = 1; p <= n; p++)
        {
            var z = -Math.Min(depth, p * stepdown); sb.Append($"G0 X{I(r)} Y{I(r)}\nG1 Z{I(z)} F{I(plunge)}\n"); var y = r; var right = true;
            while (true)
            {
                sb.Append($"G1 X{I(right ? w - r : r)} Y{I(y)} F{I(feed)}\n"); right = !right;
                if (y >= h - r - 1e-9) break; y = Math.Min(h - r, y + step); sb.Append($"G1 Y{I(y)}\n");
            }
            sb.Append($"G1 X{I(r)} Y{I(h - r)}\nG1 X{I(r)} Y{I(r)}\nG1 X{I(w - r)}\nG1 Y{I(h - r)}\nG1 X{I(r)}\nG0 Z{I(safe)}\n");
        }
        return sb.Append("M5\nM30\n").ToString();
    }

    /// <summary>F16: Lochkreis mit Tieflochbohren (Spanbrechen per Rückzug).</summary>
    public static (string Gcode, List<(double X, double Y)> Holes) BoltCircle(double cx, double cy, double diameter, int n, double startDeg, double depth, double peck, double feed, double safe = 5)
    {
        if (n < 1 || diameter <= 0) throw new ArgumentException("Mindestens 1 Loch und ein Lochkreis größer als 0.");
        var holes = Enumerable.Range(0, n).Select(i => { var a = (startDeg + 360.0 * i / n) * Math.PI / 180; return (Math.Round(cx + diameter / 2 * Math.Cos(a), 4), Math.Round(cy + diameter / 2 * Math.Sin(a), 4)); }).ToList();
        var sb = Head($"Lochkreis {n}× auf Ø{I(diameter)}", safe); var k = Passes(depth, peck <= 0 ? depth : peck);
        foreach (var (x, y) in holes)
        {
            sb.Append($"G0 X{I(x)} Y{I(y)}\nG0 Z1\n");
            for (var p = 1; p <= k; p++) sb.Append($"G1 Z{I(-Math.Min(depth, p * (peck <= 0 ? depth : peck)))} F{I(feed)}\nG0 Z1\n");
            sb.Append($"G0 Z{I(safe)}\n");
        }
        return (sb.Append("M5\nM30\n").ToString(), holes);
    }

    /// <summary>F17: Außenkontur eines Rechtecks, Fräsermitte um den Radius nach außen versetzt, Gleichlauf (im Uhrzeigersinn).</summary>
    public static string Outline(double w, double h, double depth, double toolD, double stepdown, double feed, double plunge, double safe = 5)
    {
        if (w <= 0 || h <= 0 || toolD <= 0) throw new ArgumentException("Maße und Fräser müssen größer als 0 sein.");
        var r = toolD / 2; var sb = Head($"Außenkontur {I(w)}×{I(h)}", safe); var n = Passes(depth, stepdown);
        sb.Append($"G0 X{I(-r)} Y{I(-r)}\n");
        for (var p = 1; p <= n; p++) sb.Append($"G1 Z{I(-Math.Min(depth, p * stepdown))} F{I(plunge)}\nG1 Y{I(h + r)} F{I(feed)}\nG1 X{I(w + r)}\nG1 Y{I(-r)}\nG1 X{I(-r)}\n");
        return sb.Append($"G0 Z{I(safe)}\nM5\nM30\n").ToString();
    }

    public static readonly IReadOnlyDictionary<string, double> Kc = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["Aluminium"] = 800, ["Messing"] = 1100, ["Stahl"] = 2000, ["Edelstahl"] = 2500, ["Kunststoff"] = 300, ["Holz"] = 100 };

    /// <summary>F18: Spanvolumen Q = ae·ap·vf (cm³/min), Schnittleistung P = Q·kc/60 000 000 … (kW), Antrieb mit Wirkungsgrad.</summary>
    public static (double Qcm3, double CutKw, double SpindleKw) Mrr(double ae, double ap, double vf, string material, double efficiency = 0.8)
    {
        if (!Kc.TryGetValue(material.Trim(), out var kc)) throw new ArgumentException("Werkstoffe: " + string.Join(", ", Kc.Keys));
        var q = ae * ap * vf / 1000; var p = ae * ap * vf * kc / 60e6; return (Math.Round(q, 3), Math.Round(p, 3), Math.Round(p / efficiency, 3));
    }

    /// <summary>F19: Kosten je Teil aus Maschinenzeit, Rüstzeit (auf die Menge verteilt), Material und Werkzeugverschleiß.</summary>
    public static (double PerPart, double Total) PartCost(TimeSpan cycle, int qty, double ratePerHour, double setupMin, double materialPerPart, double toolPerPart = 0)
    {
        if (qty < 1) throw new ArgumentException("Mindestens 1 Teil.");
        var per = cycle.TotalHours * ratePerHour + setupMin / 60 * ratePerHour / qty + materialPerPart + toolPerPart;
        return (Math.Round(per, 2), Math.Round(per * qty, 2));
    }

    static readonly Dictionary<double, double> Coarse = new() { [1] = 0.25, [1.2] = 0.25, [1.6] = 0.35, [2] = 0.4, [2.5] = 0.45, [3] = 0.5, [4] = 0.7, [5] = 0.8, [6] = 1, [8] = 1.25, [10] = 1.5, [12] = 1.75, [14] = 2, [16] = 2, [18] = 2.5, [20] = 2.5, [22] = 2.5, [24] = 3, [27] = 3, [30] = 3.5, [36] = 4 };

    /// <summary>F20: Kernlochbohrer = Nenndurchmesser − Steigung („M8“ oder „M8x1“).</summary>
    public static (double D, double Pitch, double Drill) TapDrill(string thread)
    {
        var m = Regex.Match(thread.Trim().Replace(',', '.'), @"^M\s*(\d+(?:\.\d+)?)\s*(?:[x×]\s*(\d+(?:\.\d+)?))?$", RegexOptions.IgnoreCase);
        if (!m.Success) throw new FormatException("Bitte so eingeben: M8 oder M8x1.");
        var d = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        var p = m.Groups[2].Success ? double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) : Coarse.TryGetValue(d, out var c) ? c : throw new ArgumentException($"M{I(d)} ist kein gängiges Regelgewinde – Steigung angeben, z. B. M{I(d)}x1.");
        return (d, p, Math.Round(d - p, 2));
    }
}
