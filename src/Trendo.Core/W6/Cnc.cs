using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Trendo.Core;

public sealed record GcodeReport(int Lines, int Moves, double CutMm, double RapidMm, TimeSpan Time, (double X0, double Y0, double Z0, double X1, double Y1, double Z1) Box, IReadOnlyList<int> Tools, double MaxSpindle, IReadOnlyList<string> Warnings, string Svg);

/// <summary>SPEC-14: Fertigung & CNC – G-Code prüfen (Weg, Zeit, Grenzen, Werkzeuge, Vorschau), Schnittwerte.</summary>
public static partial class Cnc
{
    [GeneratedRegex(@"([A-Z])\s*([-+]?\d*\.?\d+)", RegexOptions.IgnoreCase)] private static partial Regex Word();

    public static GcodeReport Analyse(string gcode, double rapidMmMin = 3000, (double X, double Y, double Z)? envelope = null)
    {
        double x = 0, y = 0, z = 0, feed = 0, maxS = 0; bool abs = true, inch = false; int motion = 0, lineNo = 0, moves = 0;
        double cut = 0, rapid = 0, minutes = 0;
        double x0 = double.MaxValue, y0 = double.MaxValue, z0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue, z1 = double.MinValue;
        var tools = new List<int>(); var warn = new List<string>(); var svgCut = new StringBuilder(); var svgRapid = new StringBuilder(); var noFeedWarned = false;
        foreach (var raw in gcode.Replace("\r", "").Split('\n'))
        {
            lineNo++;
            var l = Regex.Replace(raw, @"\(.*?\)", ""); var sc = l.IndexOf(';'); if (sc >= 0) l = l[..sc]; l = l.Trim().ToUpperInvariant();
            if (l == "" || l.StartsWith('%')) continue;
            var w = Word().Matches(l).Select(m => (C: m.Groups[1].Value[0], V: double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture))).ToList();
            double? nx = null, ny = null, nz = null, ci = null, cj = null, cr = null;
            foreach (var (c, v) in w)
                switch (c)
                {
                    case 'G':
                        switch ((int)Math.Round(v * 10)) { case 0: motion = 0; break; case 10: motion = 1; break; case 20: motion = 2; break; case 30: motion = 3; break; case 200: inch = true; break; case 210: inch = false; break; case 900: abs = true; break; case 910: abs = false; break; }
                        break;
                    case 'X': nx = v; break; case 'Y': ny = v; break; case 'Z': nz = v; break; case 'I': ci = v; break; case 'J': cj = v; break; case 'R': cr = v; break;
                    case 'F': feed = inch ? v * 25.4 : v; break;
                    case 'S': maxS = Math.Max(maxS, v); break;
                    case 'T': if (!tools.Contains((int)v)) tools.Add((int)v); break;
                }
            if (nx is null && ny is null && nz is null) continue;
            var k = inch ? 25.4 : 1;
            double tx = nx is { } a ? (abs ? a * k : x + a * k) : x, ty = ny is { } b ? (abs ? b * k : y + b * k) : y, tz = nz is { } c2 ? (abs ? c2 * k : z + c2 * k) : z;
            double len;
            if (motion is 2 or 3 && (ci.HasValue || cj.HasValue || cr.HasValue))
            {
                double cx, cy, rad;
                if (cr is { } rr) { rad = Math.Abs(rr * k); var d = Math.Sqrt((tx - x) * (tx - x) + (ty - y) * (ty - y)); var h = Math.Sqrt(Math.Max(0, rad * rad - d * d / 4)); var mx = (x + tx) / 2; var my = (y + ty) / 2; var sgn = (motion == 2) == (rr > 0) ? 1 : -1; cx = mx - sgn * h * (ty - y) / Math.Max(d, 1e-12); cy = my + sgn * h * (tx - x) / Math.Max(d, 1e-12); }
                else { cx = x + (ci ?? 0) * k; cy = y + (cj ?? 0) * k; rad = Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)); }
                double a0 = Math.Atan2(y - cy, x - cx), a1 = Math.Atan2(ty - cy, tx - cx), sweep = a1 - a0;
                if (motion == 2) { if (sweep >= -1e-9) sweep -= 2 * Math.PI; } else if (sweep <= 1e-9) sweep += 2 * Math.PI;
                var arc = Math.Abs(sweep) * rad; len = Math.Sqrt(arc * arc + (tz - z) * (tz - z));
                const int seg = 24; for (var i = 1; i <= seg; i++) { var an = a0 + sweep * i / seg; Bound(cx + rad * Math.Cos(an), cy + rad * Math.Sin(an), z + (tz - z) * i / seg); svgCut.Append(FormattableString.Invariant($"L{cx + rad * Math.Cos(an):0.###} {-(cy + rad * Math.Sin(an)):0.###} ")); }
            }
            else
            {
                len = Math.Sqrt((tx - x) * (tx - x) + (ty - y) * (ty - y) + (tz - z) * (tz - z));
                (motion == 0 ? svgRapid : svgCut).Append(FormattableString.Invariant($"M{x:0.###} {-y:0.###} L{tx:0.###} {-ty:0.###} "));
                if (motion != 0) { } else svgCut.Append(FormattableString.Invariant($"M{tx:0.###} {-ty:0.###} "));
            }
            if (motion == 0) { rapid += len; minutes += len / rapidMmMin; }
            else
            {
                cut += len;
                if (feed <= 0) { if (!noFeedWarned) warn.Add($"Zeile {lineNo}: Fräsbewegung ohne Vorschub (F fehlt)."); noFeedWarned = true; }
                else minutes += len / feed;
            }
            if (motion == 0 && tz < z && tz < 0 && Math.Abs(tx - x) + Math.Abs(ty - y) > 0) warn.Add($"Zeile {lineNo}: Eilgang schräg ins Material (Z < 0) – Kollisionsgefahr.");
            x = tx; y = ty; z = tz; moves++; Bound(x, y, z);
        }
        if (moves == 0) throw new InvalidOperationException("Keine Fahrbewegungen gefunden – ist das G-Code?");
        if (envelope is { } e && (x0 < 0 || y0 < 0 || x1 > e.X || y1 > e.Y || z1 - z0 > e.Z)) warn.Add($"Das Programm ({x1 - x0:0.#} × {y1 - y0:0.#} mm) passt nicht in den Arbeitsraum {e.X:0} × {e.Y:0} mm bzw. liegt außerhalb.");
        if (z0 < -50) warn.Add($"Sehr tiefe Zustellung: Z bis {z0:0.##} mm.");
        double pad = 5, w2 = x1 - x0 + 2 * pad, h2 = y1 - y0 + 2 * pad;
        var svg = FormattableString.Invariant($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"{x0 - pad:0.###} {-(y1 + pad):0.###} {w2:0.###} {h2:0.###}\" width=\"600\" height=\"{600 * h2 / Math.Max(w2, 1e-9):0}\"><path d=\"{svgRapid}\" stroke=\"#9ca3af\" stroke-dasharray=\"2,2\" fill=\"none\" stroke-width=\"{w2 / 400:0.###}\"/><path d=\"{svgCut}\" stroke=\"#2563eb\" fill=\"none\" stroke-width=\"{w2 / 250:0.###}\"/></svg>");
        return new(lineNo, moves, Math.Round(cut, 1), Math.Round(rapid, 1), TimeSpan.FromMinutes(minutes), (x0, y0, z0, x1, y1, z1), tools, maxS, warn, svg);

        void Bound(double px, double py, double pz) { x0 = Math.Min(x0, px); y0 = Math.Min(y0, py); z0 = Math.Min(z0, pz); x1 = Math.Max(x1, px); y1 = Math.Max(y1, py); z1 = Math.Max(z1, pz); }
    }

    public static readonly IReadOnlyDictionary<string, (double Vc, double Fz)> Materials = new Dictionary<string, (double, double)>(StringComparer.OrdinalIgnoreCase)
    {
        ["Holz"] = (500, 0.10), ["MDF"] = (450, 0.10), ["Acryl"] = (250, 0.05), ["Aluminium"] = (250, 0.04), ["Messing"] = (150, 0.04), ["Stahl"] = (80, 0.03), ["Edelstahl"] = (50, 0.02),
    };

    /// <summary>Schnittwerte: Drehzahl n = Vc·1000/(π·D), Vorschub = n · Schneiden · fz; begrenzt auf die maximale Spindeldrehzahl.</summary>
    public static (double Rpm, double FeedMmMin, double Vc, double Fz, bool Limited) Feeds(string material, double diameterMm, int flutes, double maxRpm = 24000)
    {
        if (!Materials.TryGetValue(material.Trim(), out var m)) throw new ArgumentException("Werkstoff unbekannt. Möglich: " + string.Join(", ", Materials.Keys));
        if (diameterMm <= 0 || flutes <= 0) throw new ArgumentException("Durchmesser und Schneidenzahl müssen größer als 0 sein.");
        var fz = m.Fz * Math.Clamp(diameterMm / 6, 0.3, 2); var n = m.Vc * 1000 / (Math.PI * diameterMm); var lim = n > maxRpm; if (lim) n = maxRpm;
        return (Math.Round(n), Math.Round(n * flutes * fz), Math.Round(Math.PI * diameterMm * n / 1000), Math.Round(fz, 3), lim);
    }
}
