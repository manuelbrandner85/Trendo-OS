using System.Globalization;
using System.Text;

namespace Trendo.Core;

/// <summary>T-0268: Auswertungen für das Fahrzeugbuch – Verbrauch je Tankfüllung, Kosten je Monat, Tankbuch als CSV, Beispielfahrzeug.</summary>
public static partial class VehiclePlus
{
    /// <summary>Verbrauch zwischen zwei Volltankungen (Teilbetankungen dazwischen zählen mit).</summary>
    public static List<(DateTime Date, double Odometer, double Per100)> Consumption(Vehicle v)
    {
        var f = v.Fuel.OrderBy(x => x.Odometer).ToList(); var res = new List<(DateTime, double, double)>();
        int? prev = null;
        for (var i = 0; i < f.Count; i++)
        {
            if (!f[i].Full) continue;
            if (prev is { } p && f[i].Odometer > f[p].Odometer) res.Add((f[i].Date, f[i].Odometer, Math.Round(f.Skip(p + 1).Take(i - p).Sum(x => x.Amount) / (f[i].Odometer - f[p].Odometer) * 100, 2)));
            prev = i;
        }
        return res;
    }

    /// <summary>Tank-/Ladekosten der letzten <paramref name="months"/> Monate bis <paramref name="today"/> (älteste zuerst).</summary>
    public static List<(DateTime Month, double Cost)> MonthlyCost(Vehicle v, DateTime today, int months = 12)
    {
        var start = new DateTime(today.Year, today.Month, 1).AddMonths(-(months - 1));
        return Enumerable.Range(0, months).Select(k => start.AddMonths(k)).Select(m => (m, Math.Round(v.Fuel.Where(x => x.Date >= m && x.Date < m.AddMonths(1)).Sum(x => x.Amount * x.Price), 2))).ToList();
    }

    public static string FuelCsv(Vehicle v)
    {
        var de = CultureInfo.GetCultureInfo("de-DE"); var unit = v.Electric ? "kWh" : "l";
        var sb = new StringBuilder($"Datum;km-Stand;Menge ({unit});Preis je {unit} (€);Betrag (€);voll\n");
        foreach (var x in v.Fuel.OrderBy(x => x.Odometer)) sb.Append($"{x.Date:dd.MM.yyyy};{x.Odometer.ToString("0", de)};{x.Amount.ToString("0.00", de)};{x.Price.ToString("0.000", de)};{(x.Amount * x.Price).ToString("0.00", de)};{(x.Full ? "ja" : "nein")}\n");
        return sb.ToString();
    }

    /// <summary>Beispielfahrzeug (erfunden): ein Jahr Tankbuch alle zwei bis drei Wochen und Fahrten mit dienstlichem Anteil.</summary>
    public static Vehicle SeedDemo(VehicleBook book, DateTime today, int seed = 5)
    {
        var r = new Random(seed); var name = "Familienauto";
        var v = book.Garage.Vehicles.FirstOrDefault(x => x.Name == name) ?? book.Add(name, "M-EM 2026");
        v.InspectionDue = new DateTime(today.Year, today.Month, 1).AddMonths(1); v.ServiceDueKm = 60_000;
        double odo = 41_200; var d = today.AddDays(-360);
        while (d < today)
        {
            var km = 380 + r.Next(260); odo += km; d = d.AddDays(14 + r.Next(8)); if (d > today) break;
            var per100 = 5.6 + r.NextDouble() * 1.4 + (d.Month is 12 or 1 or 2 ? 0.6 : 0);
            v.Fuel.Add(new FuelEntry { Date = d, Odometer = Math.Round(odo), Amount = Math.Round(km * per100 / 100, 2), Price = Math.Round(1.69 + r.NextDouble() * 0.2, 3), Full = true });
        }
        double tkm = odo - 900; var td = today.AddDays(-20);
        foreach (var (len, biz, why) in new[] { (64.0, true, "Kundentermin Augsburg"), (18.0, false, ""), (122.0, true, "Messe Nürnberg, Rückfahrt"), (35.0, false, ""), (41.0, true, "Lieferant Freising") })
        { v.Trips.Add(new Trip { Date = td, StartKm = tkm, EndKm = tkm + len, Business = biz, Purpose = why }); tkm += len; td = td.AddDays(3); }
        book.Save(); return v;
    }
}
