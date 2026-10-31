using System.Globalization;
using Trendo.Core.Arch;

namespace Trendo.Core;

/// <summary>T-0265: Zeitverlauf eines Sensors als Zeichnung (Bildschirm und SVG) – Grenzlinien, Verlauf, Ampelwechsel und Prognose.</summary>
public static class TwinChart
{
    static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");
    public static (DateTime From, DateTime To) Range(IReadOnlyList<Reading> rs, string range)
    {
        if (rs.Count == 0) return (DateTime.Now.AddDays(-1), DateTime.Now);
        var to = rs[^1].Time;
        var from = range switch { "24h" => to.AddHours(-24), "7d" => to.AddDays(-7), "30d" => to.AddDays(-30), _ => rs[0].Time };
        if (from < rs[0].Time) from = rs[0].Time;
        if (to <= from) to = from.AddHours(1);
        return (from, to);
    }

    public static List<Shape> Build(TwinModel m, string asset, string sensor, double w, double h, string range = "7d", double forecastHours = 0)
    {
        var s = new List<Shape>();
        var def = m.Assets.FirstOrDefault(a => a.Name.Equals(asset, StringComparison.OrdinalIgnoreCase))?.Sensors.FirstOrDefault(x => x.Name.Equals(sensor, StringComparison.OrdinalIgnoreCase));
        var all = TwinPlus.Series(m, asset, sensor);
        if (def is null || all.Count == 0) { s.Add(new DText(w / 2, h / 2, "Noch keine Werte", 14, "#8FA3BF", "TEXT")); return s; }
        var (from, to) = Range(all, range);
        var rs = all.Where(r => r.Time >= from && r.Time <= to).ToList();
        var fcAt = forecastHours > 0 ? to.AddHours(forecastHours) : to;
        double? fcVal = forecastHours > 0 && all.Count >= 3 ? TwinPlus.Forecast(all, forecastHours) : null;
        var vals = rs.Select(r => r.Value).ToList(); if (fcVal is { } fv) vals.Add(fv);
        foreach (var lim in new[] { def.WarnAbove, def.AlarmAbove, def.WarnBelow, def.AlarmBelow }) if (!double.IsNaN(lim)) vals.Add(lim);
        double lo = vals.Min(), hi = vals.Max(); if (hi - lo < 1e-9) { lo -= 1; hi += 1; } var pad = (hi - lo) * 0.08; lo -= pad; hi += pad;
        const double L = 56, T = 14, R = 14, B = 26; double pw = Math.Max(10, w - L - R), ph = Math.Max(10, h - T - B);
        var t1 = fcVal is null ? to : fcAt; var span = Math.Max(1, (t1 - from).TotalSeconds);
        double X(DateTime t) => L + (t - from).TotalSeconds / span * pw;
        double Y(double v) => T + (hi - v) / (hi - lo) * ph;
        // Hintergrundbänder für Warnung/Alarm
        void Band(double a, double b, string col) { var y1 = Y(Math.Min(hi, Math.Max(a, b))); var y2 = Y(Math.Max(lo, Math.Min(a, b))); if (y2 > y1) s.Add(new DPoly([(L, y1), (L + pw, y1), (L + pw, y2), (L, y2)], col, null, 0, "BAND")); }
        if (!double.IsNaN(def.AlarmAbove)) Band(def.AlarmAbove, hi, "#F8717122");
        if (!double.IsNaN(def.WarnAbove)) Band(def.WarnAbove, double.IsNaN(def.AlarmAbove) ? hi : def.AlarmAbove, "#FACC1518");
        if (!double.IsNaN(def.AlarmBelow)) Band(lo, def.AlarmBelow, "#F8717122");
        if (!double.IsNaN(def.WarnBelow)) Band(double.IsNaN(def.AlarmBelow) ? lo : def.AlarmBelow, def.WarnBelow, "#FACC1518");
        // Raster und Achsen
        for (var k = 0; k <= 4; k++)
        {
            var v = lo + (hi - lo) * k / 4; var y = Y(v);
            s.Add(new DLine(L, y, L + pw, y, "#FFFFFF14", 1, "RASTER"));
            s.Add(new DText(L - 24, y + 4, v.ToString(Math.Abs(hi - lo) < 10 ? "0.0" : "0", De), 10, "#8FA3BF", "ACHSE"));
        }
        var days = (t1 - from).TotalDays; var fmt = days <= 2 ? "HH:mm" : "dd.MM.";
        for (var k = 0; k <= 5; k++) { var t = from.AddSeconds(span * k / 5); s.Add(new DText(X(t), T + ph + 16, t.ToString(fmt, De), 10, "#8FA3BF", "ACHSE")); }
        foreach (var (lim, col, lab) in new[] { (def.WarnAbove, "#FACC15", "Warnung"), (def.AlarmAbove, "#F87171", "Alarm"), (def.WarnBelow, "#FACC15", "Warnung"), (def.AlarmBelow, "#F87171", "Alarm") })
            if (!double.IsNaN(lim)) { s.Add(new DLine(L, Y(lim), L + pw, Y(lim), col, 1.2, "GRENZE", true)); s.Add(new DText(L + pw - 48, Y(lim) - 5, $"{lab} {lim.ToString("0.##", De)}", 10, col, "GRENZE")); }
        // Verlauf (auf höchstens 2 Punkte je Pixel verdichtet: Min und Max je Spalte)
        var pts = new List<(double, double)>();
        foreach (var g in rs.GroupBy(r => (int)X(r.Time)))
        {
            var mn = g.MinBy(r => r.Value)!; var mx = g.MaxBy(r => r.Value)!;
            foreach (var r in mn.Time <= mx.Time ? new[] { mn, mx } : [mx, mn]) pts.Add((X(r.Time), Y(r.Value)));
        }
        if (pts.Count >= 2) { var area = new List<(double, double)>(pts) { (pts[^1].Item1, T + ph), (pts[0].Item1, T + ph) }; s.Add(new DPoly(area, "#2F86F622", null, 0, "FLAECHE")); s.Add(new DPoly(pts, null, "#4FA3FF", 2, "VERLAUF", Closed: false)); }
        else if (pts.Count == 1) s.Add(new DPoly([(pts[0].Item1 - 3, pts[0].Item2 - 3), (pts[0].Item1 + 3, pts[0].Item2 - 3), (pts[0].Item1 + 3, pts[0].Item2 + 3), (pts[0].Item1 - 3, pts[0].Item2 + 3)], "#4FA3FF", null, 0, "VERLAUF"));
        // Ampelwechsel
        foreach (var e in TwinPlus.Events(m, asset, sensor, EventBand(all)).Where(e => e.Time >= from && e.Time <= to))
        {
            var col = e.To switch { TwinLevel.Alarm => "#F87171", TwinLevel.Warnung => "#FACC15", _ => "#4ADE80" }; var x = X(e.Time); var y = Y(e.Value);
            s.Add(new DPoly([(x, y - 5), (x + 5, y), (x, y + 5), (x - 5, y)], col, "#0A1322", 1, "EREIGNIS"));
        }
        // Prognose
        if (fcVal is { } f && rs.Count > 0) { s.Add(new DLine(X(rs[^1].Time), Y(rs[^1].Value), X(fcAt), Y(f), "#C084FC", 2, "PROGNOSE", true)); s.Add(new DText(X(fcAt) - 44, Y(f) - 8, $"in {forecastHours:0} h: {f.ToString("0.#", De)}", 10, "#C084FC", "PROGNOSE")); }
        return s;
    }

    /// <summary>Hysterese-Band gegen Flattern an einer Grenze: 3 % der Wertespanne.</summary>
    public static double EventBand(IReadOnlyList<Reading> rs) => rs.Count < 2 ? 0 : (rs.Max(r => r.Value) - rs.Min(r => r.Value)) * 0.03;

    /// <summary>Mini-Verlauf der letzten 24 h für eine Kachel.</summary>
    public static List<Shape> Spark(TwinModel m, string asset, string sensor, double w, double h, string color)
    {
        var all = TwinPlus.Series(m, asset, sensor); var s = new List<Shape>();
        if (all.Count < 2) return s;
        var to = all[^1].Time; var rs = all.Where(r => r.Time >= to.AddHours(-24)).ToList(); if (rs.Count < 2) rs = all.TakeLast(2).ToList();
        double lo = rs.Min(r => r.Value), hi = rs.Max(r => r.Value); if (hi - lo < 1e-9) { lo -= 1; hi += 1; }
        var t0 = rs[0].Time; var span = Math.Max(1, (to - t0).TotalSeconds);
        var step = Math.Max(1, rs.Count / (int)Math.Max(2, w));
        s.Add(new DPoly(rs.Where((_, i) => i % step == 0 || i == rs.Count - 1).Select(r => ((r.Time - t0).TotalSeconds / span * w, 2 + (hi - r.Value) / (hi - lo) * (h - 4))).ToList(), null, color, 1.5, "VERLAUF", Closed: false));
        return s;
    }

    public static string Svg(TwinModel m, string asset, string sensor, string range = "7d", double forecastHours = 0)
    {
        const double W = 900, H = 360; var sb = new System.Text.StringBuilder($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{W}\" height=\"{H}\" viewBox=\"0 0 {W} {H}\"><rect width=\"100%\" height=\"100%\" fill=\"#0E1A2E\"/>");
        string N(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
        static (string Col, string Op) C(string hex) => hex.Length == 9 ? ("#" + hex[1..7], (Convert.ToInt32(hex[7..], 16) / 255.0).ToString("0.##", CultureInfo.InvariantCulture)) : (hex, "1");
        foreach (var sh in Build(m, asset, sensor, W, H, range, forecastHours))
            switch (sh)
            {
                case DPoly p: { var (fc, fo) = p.Fill is null ? ("none", "1") : C(p.Fill); var (sc, so) = p.Stroke is null ? ("none", "1") : C(p.Stroke); sb.Append($"<{(p.Closed ? "polygon" : "polyline")} points=\"{string.Join(" ", p.Pts.Select(q => N(q.X) + "," + N(q.Y)))}\" fill=\"{fc}\" fill-opacity=\"{fo}\" stroke=\"{sc}\" stroke-opacity=\"{so}\" stroke-width=\"{N(p.Width)}\"/>"); break; }
                case DLine l: { var (sc, so) = C(l.Stroke); sb.Append($"<line x1=\"{N(l.X1)}\" y1=\"{N(l.Y1)}\" x2=\"{N(l.X2)}\" y2=\"{N(l.Y2)}\" stroke=\"{sc}\" stroke-opacity=\"{so}\" stroke-width=\"{N(l.Width)}\"{(l.Dashed ? " stroke-dasharray=\"5 4\"" : "")}/>"); break; }
                case DText t: sb.Append($"<text x=\"{N(t.X)}\" y=\"{N(t.Y)}\" font-family=\"sans-serif\" font-size=\"{N(t.Size)}\" fill=\"{t.Color}\" text-anchor=\"middle\">{System.Security.SecurityElement.Escape(t.Text)}</text>"); break;
            }
        return sb.Append("</svg>").ToString();
    }
}

/// <summary>T-0265: Beispielanlagen (erfunden) mit sieben Tagen Messwerten alle 15 Minuten – Heizung, Solaranlage, Kühlraum mit steigender Temperatur.</summary>
public static class TwinDemo
{
    public static int Seed(TwinHub hub, DateTime now, int seed = 3)
    {
        var r = new Random(seed); var n = 0;
        hub.Define("Heizung", "Vorlauf", "°C", 70, 80, kind: "Heizung"); hub.Define("Heizung", "Rücklauf", "°C", kind: "Heizung"); hub.Define("Heizung", "Brenner", "kW", kind: "Heizung");
        hub.Define("Solaranlage", "Leistung", "kW", kind: "Solar");
        hub.Define("Kühlraum", "Temperatur", "°C", 6, 8, double.NaN, -2, kind: "Kühlung");
        var start = now.AddDays(-7);
        for (var t = start; t <= now; t = t.AddMinutes(15))
        {
            var hour = t.Hour + t.Minute / 60.0; var day = Math.Max(0, Math.Sin((hour - 6) / 12 * Math.PI));
            var burner = hour is >= 5 and < 22 ? 8 + 6 * Math.Max(0, Math.Cos((hour - 7) / 24 * 2 * Math.PI)) + r.NextDouble() : 0;
            var vl = 45 + burner * 2.2 + r.NextDouble() * 1.5 + (t.Day % 3 == 0 && hour is > 6 and < 7.5 ? 12 : 0); // morgens kurze Spitze
            hub.Record(t, "Heizung", "Vorlauf", Math.Round(vl, 1), false); hub.Record(t, "Heizung", "Rücklauf", Math.Round(vl - 10 - r.NextDouble() * 2, 1), false); hub.Record(t, "Heizung", "Brenner", Math.Round(burner, 2), false);
            hub.Record(t, "Solaranlage", "Leistung", Math.Round(6.5 * day * (0.7 + 0.3 * r.NextDouble()), 2), false);
            var drift = (t - start).TotalDays > 6 ? ((t - start).TotalDays - 6) * 3.5 : 0; // letzter Tag: Kühlraum wird wärmer (Tür offen?)
            hub.Record(t, "Kühlraum", "Temperatur", Math.Round(3.2 + 0.6 * Math.Sin(hour / 24 * 2 * Math.PI) + r.NextDouble() * 0.3 + drift, 2), false);
            n += 5;
        }
        hub.Save(); return n;
    }
}
