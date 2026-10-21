using System.Globalization;

namespace Trendo.Core;

public enum TwinLevel { Ok, Warnung, Alarm, Unbekannt }
public sealed class TwinSensor { public string Name { get; set; } = ""; public string Unit { get; set; } = ""; public double WarnAbove { get; set; } = double.NaN; public double AlarmAbove { get; set; } = double.NaN; public double WarnBelow { get; set; } = double.NaN; public double AlarmBelow { get; set; } = double.NaN; }
public sealed class TwinAsset { public string Name { get; set; } = ""; public string Kind { get; set; } = ""; public List<TwinSensor> Sensors { get; set; } = []; }
public sealed record Reading(DateTime Time, string Asset, string Sensor, double Value);
public sealed record SensorState(string Asset, string Sensor, string Unit, double? Last, DateTime? At, TwinLevel Level, double? TrendPerHour, double? HoursToAlarm);
public sealed class TwinModel { public List<TwinAsset> Assets { get; set; } = []; public List<Reading> Readings { get; set; } = []; }

/// <summary>SPEC-10: Digitaler Zwilling – Anlagen mit Sensoren, Messwerte (CSV oder live), Zustand, Trend, Vorhersage.</summary>
public sealed class TwinHub(string dataRoot)
{
    public const int MaxReadings = 200_000;
    public TwinModel Model { get; } = Fach.Load<TwinModel>(dataRoot, "zwilling", "modell");
    public void Save() { if (Model.Readings.Count > MaxReadings) Model.Readings.RemoveRange(0, Model.Readings.Count - MaxReadings); Fach.Save(dataRoot, "zwilling", "modell", Model); }

    public TwinSensor Define(string asset, string sensor, string unit, double warnAbove = double.NaN, double alarmAbove = double.NaN, double warnBelow = double.NaN, double alarmBelow = double.NaN, string kind = "")
    {
        if (string.IsNullOrWhiteSpace(asset) || string.IsNullOrWhiteSpace(sensor)) throw new ArgumentException("Bitte Anlage und Sensor benennen.");
        if (!double.IsNaN(warnAbove) && !double.IsNaN(alarmAbove) && alarmAbove < warnAbove) throw new ArgumentException("Die Alarm-Grenze oben muss über der Warn-Grenze liegen.");
        var a = Model.Assets.FirstOrDefault(x => x.Name.Equals(asset.Trim(), StringComparison.OrdinalIgnoreCase)); if (a is null) { a = new TwinAsset { Name = asset.Trim(), Kind = kind }; Model.Assets.Add(a); }
        var s = a.Sensors.FirstOrDefault(x => x.Name.Equals(sensor.Trim(), StringComparison.OrdinalIgnoreCase)); if (s is null) { s = new TwinSensor { Name = sensor.Trim() }; a.Sensors.Add(s); }
        s.Unit = unit; s.WarnAbove = warnAbove; s.AlarmAbove = alarmAbove; s.WarnBelow = warnBelow; s.AlarmBelow = alarmBelow; Save(); return s;
    }

    public void Record(DateTime t, string asset, string sensor, double v, bool save = true)
    {
        if (!Model.Assets.Any(a => a.Name.Equals(asset, StringComparison.OrdinalIgnoreCase) && a.Sensors.Any(s => s.Name.Equals(sensor, StringComparison.OrdinalIgnoreCase)))) Define(asset, sensor, "");
        Model.Readings.Add(new(t, asset, sensor, v)); if (save) Save();
    }

    /// <summary>CSV „Zeit;Anlage;Sensor;Wert“ (Zeit als 21.10.2026 14:00 oder ISO). Unbekannte Sensoren werden angelegt.</summary>
    public (int Ok, int Skipped) Import(string csv)
    {
        int ok = 0, bad = 0;
        foreach (var l in csv.Replace("\r", "").Split('\n').Skip(1))
        {
            if (l.Trim() == "") continue;
            var c = l.Split(l.Contains(';') ? ';' : ',');
            if (c.Length < 4 || !(DateTime.TryParse(c[0].Trim(), Fach.De, DateTimeStyles.AssumeLocal, out var t) || DateTime.TryParse(c[0].Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out t))) { bad++; continue; }
            try { Record(t, c[1].Trim(), c[2].Trim(), Fach.Num(c[3]), false); ok++; } catch (FormatException) { bad++; }
        }
        Save(); return (ok, bad);
    }

    public static TwinLevel Level(TwinSensor s, double v) =>
        (!double.IsNaN(s.AlarmAbove) && v >= s.AlarmAbove) || (!double.IsNaN(s.AlarmBelow) && v <= s.AlarmBelow) ? TwinLevel.Alarm
        : (!double.IsNaN(s.WarnAbove) && v >= s.WarnAbove) || (!double.IsNaN(s.WarnBelow) && v <= s.WarnBelow) ? TwinLevel.Warnung : TwinLevel.Ok;

    /// <summary>Zustand je Sensor: letzter Wert, Ampel, Trend (lineare Regression der letzten 24 h), Stunden bis zum Alarm.</summary>
    public IReadOnlyList<SensorState> States()
    {
        var res = new List<SensorState>();
        foreach (var a in Model.Assets) foreach (var s in a.Sensors)
        {
            var rs = Model.Readings.Where(r => r.Asset.Equals(a.Name, StringComparison.OrdinalIgnoreCase) && r.Sensor.Equals(s.Name, StringComparison.OrdinalIgnoreCase)).OrderBy(r => r.Time).ToList();
            if (rs.Count == 0) { res.Add(new(a.Name, s.Name, s.Unit, null, null, TwinLevel.Unbekannt, null, null)); continue; }
            var last = rs[^1]; var win = rs.Where(r => r.Time >= last.Time.AddHours(-24)).ToList();
            double? trend = null, hours = null;
            if (win.Count >= 3 && (win[^1].Time - win[0].Time).TotalHours > 0)
            {
                var t0 = win[0].Time; var reg = Stats.Linear(win.Select(r => (r.Time - t0).TotalHours).ToArray(), win.Select(r => r.Value).ToArray()); trend = reg.Slope;
                if (reg.Slope > 1e-9 && !double.IsNaN(s.AlarmAbove) && last.Value < s.AlarmAbove) hours = (s.AlarmAbove - last.Value) / reg.Slope;
                else if (reg.Slope < -1e-9 && !double.IsNaN(s.AlarmBelow) && last.Value > s.AlarmBelow) hours = (last.Value - s.AlarmBelow) / -reg.Slope;
            }
            res.Add(new(a.Name, s.Name, s.Unit, last.Value, last.Time, Level(s, last.Value), trend, hours));
        }
        return res;
    }
}
