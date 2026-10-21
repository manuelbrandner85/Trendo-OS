using System.Globalization;
using System.Text;

namespace Trendo.Core;

public sealed record TwinEvent(DateTime Time, TwinLevel From, TwinLevel To, double Value);
public sealed record TwinSpan(int Count, double Min, double Max, double Mean);

/// <summary>SPEC-10 Ausbau: Verlauf auswerten, Ereignisse, Betriebsstunden, Wartung, Energie.</summary>
public static class TwinPlus
{
    static bool Is(Reading r, string a, string s) => r.Asset.Equals(a, StringComparison.OrdinalIgnoreCase) && r.Sensor.Equals(s, StringComparison.OrdinalIgnoreCase);
    public static List<Reading> Series(TwinModel m, string asset, string sensor) => m.Readings.Where(r => Is(r, asset, sensor)).OrderBy(r => r.Time).ToList();
    static TwinSensor Sensor(TwinModel m, string asset, string sensor) => m.Assets.FirstOrDefault(a => a.Name.Equals(asset, StringComparison.OrdinalIgnoreCase))?.Sensors.FirstOrDefault(s => s.Name.Equals(sensor, StringComparison.OrdinalIgnoreCase)) ?? throw new ArgumentException($"Sensor „{asset} – {sensor}“ gibt es nicht.");

    /// <summary>F09: nur die neuesten <paramref name="max"/> Werte behalten. Gibt die Zahl der verworfenen zurück.</summary>
    public static int Trim(TwinModel m, int max)
    {
        if (max < 1) throw new ArgumentException("Mindestens 1 Wert behalten.");
        var n = m.Readings.Count - max; if (n <= 0) return 0;
        var keep = m.Readings.OrderBy(r => r.Time).Skip(n).ToList(); m.Readings.Clear(); m.Readings.AddRange(keep); return n;
    }

    /// <summary>F11: Ampel mit Hysterese – zurück auf eine bessere Stufe erst, wenn der Wert die Grenze um das Band unterschreitet (bzw. überschreitet).</summary>
    public static TwinLevel HystLevel(TwinSensor s, double v, TwinLevel prev, double band)
    {
        var raw = TwinHub.Level(s, v); if (band <= 0 || raw >= prev || prev == TwinLevel.Unbekannt) return raw;
        var relaxed = new TwinSensor { WarnAbove = s.WarnAbove - band, AlarmAbove = s.AlarmAbove - band, WarnBelow = s.WarnBelow + band, AlarmBelow = s.AlarmBelow + band };
        var r2 = TwinHub.Level(relaxed, v); return r2 > raw ? (TwinLevel)Math.Min((int)r2, (int)prev) : raw;
    }

    /// <summary>F10: Wechsel der Ampel im Verlauf (mit Hysterese-Band).</summary>
    public static List<TwinEvent> Events(TwinModel m, string asset, string sensor, double band = 0)
    {
        var s = Sensor(m, asset, sensor); var res = new List<TwinEvent>(); var prev = TwinLevel.Unbekannt;
        foreach (var r in Series(m, asset, sensor)) { var l = HystLevel(s, r.Value, prev, band); if (l != prev) { if (prev != TwinLevel.Unbekannt) res.Add(new(r.Time, prev, l, r.Value)); prev = l; } }
        return res;
    }

    /// <summary>F12</summary>
    public static TwinSpan Span(IEnumerable<Reading> rs, DateTime? from = null, DateTime? to = null)
    {
        var v = rs.Where(r => (from is null || r.Time >= from) && (to is null || r.Time <= to)).Select(r => r.Value).ToList();
        if (v.Count == 0) throw new InvalidOperationException("In diesem Zeitraum gibt es keine Werte.");
        return new(v.Count, v.Min(), v.Max(), v.Average());
    }

    /// <summary>F13: Stunden, in denen der Wert ≥ Schwelle war (jeder Wert gilt bis zum nächsten).</summary>
    public static double HoursAbove(IReadOnlyList<Reading> rs, double threshold)
    {
        var h = 0.0; for (var i = 0; i + 1 < rs.Count; i++) if (rs[i].Value >= threshold) h += (rs[i + 1].Time - rs[i].Time).TotalHours; return h;
    }

    /// <summary>F14: Sensoren ohne Wert seit <paramref name="minutes"/> Minuten.</summary>
    public static List<(string Asset, string Sensor, DateTime? Last)> Stale(TwinModel m, DateTime now, double minutes)
    {
        var res = new List<(string, string, DateTime?)>();
        foreach (var a in m.Assets) foreach (var s in a.Sensors)
        {
            DateTime? last = m.Readings.Where(r => Is(r, a.Name, s.Name)).Select(r => (DateTime?)r.Time).Max();
            if (last is null || (now - last.Value).TotalMinutes > minutes) res.Add((a.Name, s.Name, last));
        }
        return res;
    }

    /// <summary>F15: neuer Sensor aus einem vorhandenen: y = a·x + b (z. B. °C → °F).</summary>
    public static int Derive(TwinHub hub, string asset, string source, string name, string unit, double a, double b)
    {
        var src = Series(hub.Model, asset, source); if (src.Count == 0) throw new InvalidOperationException("Der Quellsensor hat noch keine Werte.");
        hub.Define(asset, name, unit);
        hub.Model.Readings.RemoveAll(r => Is(r, asset, name));
        foreach (var r in src) hub.Model.Readings.Add(new(r.Time, r.Asset, name, a * r.Value + b));
        hub.Save(); return src.Count;
    }

    /// <summary>F16</summary>
    public static string ExportCsv(TwinModel m, string asset, string sensor)
    {
        var sb = new StringBuilder("Zeit;Anlage;Sensor;Wert\n");
        foreach (var r in Series(m, asset, sensor)) sb.Append($"{r.Time.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)};{r.Asset};{r.Sensor};{r.Value.ToString(Fach.De)}\n");
        return sb.ToString();
    }

    /// <summary>F17</summary>
    public static void Rename(TwinHub hub, string asset, string sensor, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName)) throw new ArgumentException("Bitte einen neuen Namen eingeben.");
        var s = Sensor(hub.Model, asset, sensor); var a = hub.Model.Assets.First(x => x.Sensors.Contains(s));
        if (a.Sensors.Any(x => x != s && x.Name.Equals(newName.Trim(), StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("Diesen Namen gibt es schon.");
        s.Name = newName.Trim();
        for (var i = 0; i < hub.Model.Readings.Count; i++) if (Is(hub.Model.Readings[i], asset, sensor)) hub.Model.Readings[i] = hub.Model.Readings[i] with { Sensor = s.Name };
        hub.Save();
    }

    public static int Delete(TwinHub hub, string asset, string sensor)
    {
        var s = Sensor(hub.Model, asset, sensor); var a = hub.Model.Assets.First(x => x.Sensors.Contains(s)); a.Sensors.Remove(s);
        if (a.Sensors.Count == 0) hub.Model.Assets.Remove(a);
        var n = hub.Model.Readings.RemoveAll(r => Is(r, asset, sensor)); hub.Save(); return n;
    }

    /// <summary>F18: nächste Wartung nach Betriebsstunden.</summary>
    public static (double NextAt, double Remaining, bool Due) Maintenance(double hoursRun, double interval, double lastServiceAt = 0)
    {
        if (interval <= 0) throw new ArgumentException("Das Intervall muss größer als 0 sein.");
        var next = lastServiceAt + interval; return (next, next - hoursRun, hoursRun >= next);
    }

    /// <summary>F19: Trend der letzten 24 h um <paramref name="hours"/> fortschreiben.</summary>
    public static double Forecast(IReadOnlyList<Reading> rs, double hours)
    {
        if (rs.Count == 0) throw new InvalidOperationException("Keine Werte.");
        var last = rs[^1]; var w = rs.Where(r => r.Time >= last.Time.AddHours(-24)).ToList();
        if (w.Count < 2 || (w[^1].Time - w[0].Time).TotalHours <= 0) return last.Value;
        var t0 = w[0].Time; var reg = Stats.Linear(w.Select(r => (r.Time - t0).TotalHours).ToArray(), w.Select(r => r.Value).ToArray());
        return reg.Slope * ((last.Time - t0).TotalHours + hours) + reg.Intercept;
    }

    /// <summary>F20: Leistung (kW) über die Zeit → Energie (kWh), Trapezregel.</summary>
    public static double EnergyKwh(IReadOnlyList<Reading> rs)
    {
        var e = 0.0; for (var i = 0; i + 1 < rs.Count; i++) e += (rs[i].Value + rs[i + 1].Value) / 2 * (rs[i + 1].Time - rs[i].Time).TotalHours; return e;
    }
}
