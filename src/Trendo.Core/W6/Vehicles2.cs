using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>SPEC-16 Ausbau: E-Auto, Steuer (Pendlerpauschale, 1-%-Regel), Kilometergeld, Reifen, Kosten.
/// Steuerwerte mit Quellen in LegalRates (geprüft 21.10.2026), ohne Gewähr – Auskunft gibt das Finanzamt oder die Steuerberatung.</summary>
public static partial class VehiclePlus
{
    /// <summary>F05: Verbrauch eines E-Autos (Ladung inkl. Ladeverlust) und Vergleich mit einem Verbrenner je 100 km.</summary>
    public static (double KwhPer100, double EvCost100, double IceCost100, double Saving100) Ev(Vehicle v, double icePer100, double fuelPrice)
    {
        if (!v.Electric) throw new ArgumentException("Das Fahrzeug ist nicht als Elektro angelegt.");
        var s = VehicleBook.Stats(v); if (s.AvgPer100 is not { } k) throw new InvalidOperationException("Mindestens zweimal voll laden, dann lässt sich der Verbrauch berechnen.");
        var f = v.Fuel.OrderBy(x => x.Odometer).Skip(1).ToList(); var price = f.Sum(x => x.Amount) > 0 ? f.Sum(x => x.Amount * x.Price) / f.Sum(x => x.Amount) : 0;
        var ev = Math.Round(k * price, 2); var ice = Math.Round(icePer100 * fuelPrice, 2); return (k, ev, ice, Math.Round(ice - ev, 2));
    }

    /// <summary>F14: Entfernungspauschale für ein Jahr nach § 9 Abs. 1 Satz 3 Nr. 4 EStG – Sätze und Quellen in <see cref="LegalRates.CommuteRates"/>.</summary>
    public static double Commute(double oneWayKm, int days, int year = 2026, bool ownCar = true)
    {
        if (oneWayKm < 0 || days < 0) throw new ArgumentException("Entfernung und Tage dürfen nicht negativ sein.");
        var km = Math.Floor(oneWayKm); var (r1, r2) = LegalRates.CommuteRates(year);
        var total = (Math.Min(km, 20) * r1 + Math.Max(0, km - 20) * r2) * days; return Math.Round(ownCar ? total : Math.Min(total, LegalRates.CommuteCap), 2);
    }

    /// <summary>F15: geldwerter Vorteil je Jahr nach 1-%-Regel gegen Fahrtenbuch (§ 6 Abs. 1 Nr. 4 EStG, Quellen in <see cref="LegalRates"/>).
    /// kind: verbrenner = voller, hybrid = halber (nur wenn die Reichweiten-/CO₂-Bedingungen erfüllt sind), elektro = ¼ bis zur Preisgrenze je Anschaffungsdatum, sonst ½ Listenpreis.
    /// Vereinfachung: Beim Fahrtenbuch wird der Privatanteil der eingegebenen Gesamtkosten genommen; die Kürzung der AfA bei E-/Hybridautos muss dort schon berücksichtigt sein.</summary>
    public static (double OnePercent, double Logbook, string Better) CompanyCar(double listPrice, string kind, double commuteKm, double yearCost, double privateSharePct, DateOnly? acquired = null)
    {
        var f = kind.Trim().ToLowerInvariant() switch { "verbrenner" or "benzin" or "diesel" => 1.0, "hybrid" => 0.5, "elektro" or "e" => listPrice <= LegalRates.EvPriceLimit(acquired ?? new DateOnly(2026, 1, 1)) ? 0.25 : 0.5, _ => throw new ArgumentException("Antrieb: verbrenner, hybrid oder elektro.") };
        var basis = Math.Floor(listPrice * f / 100) * 100; // erst Bruchteil, dann auf volle 100 € abrunden (BMF 05.11.2021 Rz. 12)
        var one = Math.Round(12 * (basis * 0.01 + basis * 0.0003 * commuteKm), 2);
        var log = Math.Round(yearCost * privateSharePct / 100, 2);
        return (one, log, one <= log ? "1-%-Regel" : "Fahrtenbuch");
    }

    /// <summary>F16: Kilometergeld. Steuerfrei erstattbar/absetzbar nach § 9 Abs. 1 Satz 3 Nr. 4a EStG: Auto 0,30 €/km, Motorrad/Roller 0,20 €/km.
    /// <paramref name="brkg"/> = Bundesreisekostengesetz § 5 (öffentlicher Dienst): 0,20 €/km bis 130 € je Reise, 0,30 €/km bei erheblichem dienstlichen Interesse.</summary>
    public static double Mileage(double km, bool motorbike = false, double? rate = null, bool brkg = false, bool significantInterest = false)
    {
        if (rate is { } r) return Math.Round(km * r, 2);
        if (brkg) return significantInterest && !motorbike ? Math.Round(km * LegalRates.BrkgLarge, 2) : Math.Round(Math.Min(km * LegalRates.BrkgSmall, LegalRates.BrkgSmallCap), 2);
        return Math.Round(km * (motorbike ? LegalRates.TravelOtherMotor : LegalRates.TravelCar), 2);
    }

    [GeneratedRegex(@"(\d{2})(\d{2})\s*$")] private static partial Regex Dot();
    /// <summary>F17: Reifenalter aus der DOT-Nummer (letzte 4 Ziffern = Woche, Jahr) und Profiltiefe.</summary>
    public static (DateTime Made, double AgeYears, List<string> Advice) Tire(string dot, double treadMm, bool winter, DateTime today)
    {
        var m = Dot().Match(dot.Trim()); if (!m.Success) throw new FormatException("Bitte die letzten 4 Ziffern der DOT-Nummer eingeben, z. B. 2319.");
        int wk = int.Parse(m.Groups[1].Value), yr = 2000 + int.Parse(m.Groups[2].Value); if (wk < 1 || wk > 53) throw new FormatException("Die Woche muss zwischen 01 und 53 liegen.");
        var made = System.Globalization.ISOWeek.ToDateTime(yr, Math.Min(wk, System.Globalization.ISOWeek.GetWeeksInYear(yr)), DayOfWeek.Monday);
        if (made > today) throw new FormatException("Dieses Herstelldatum liegt in der Zukunft.");
        var age = Math.Round((today - made).TotalDays / 365.25, 1); var a = new List<string>();
        if (treadMm < 1.6) a.Add("Unter 1,6 mm – gesetzlich nicht mehr erlaubt, sofort wechseln.");
        else if (treadMm < (winter ? 4 : 3)) a.Add($"Unter {(winter ? 4 : 3)} mm – bald wechseln, Bremsweg wird länger.");
        if (age >= 10) a.Add("Älter als 10 Jahre – austauschen, auch bei gutem Profil.");
        else if (age >= 6) a.Add("Älter als 6 Jahre – Gummi härtet aus, jährlich prüfen lassen.");
        if (a.Count == 0) a.Add("In Ordnung.");
        return (made, age, a);
    }

    /// <summary>F18: Fahrtkosten auf Mitfahrer aufteilen.</summary>
    public static (double Total, double PerPerson) Share(double km, double per100, double price, int persons, double extras = 0)
    {
        if (persons < 1) throw new ArgumentException("Mindestens 1 Person.");
        var t = Math.Round(km * per100 / 100 * price + extras, 2); return (t, Math.Round(t / persons, 2, MidpointRounding.AwayFromZero));
    }

    /// <summary>F19: Ladezeit von <paramref name="fromPct"/> bis <paramref name="toPct"/> % (Wirkungsgrad AC ≈ 90 %).</summary>
    public static (double Kwh, TimeSpan Time) ChargeTime(double batteryKwh, double fromPct, double toPct, double chargerKw, double carMaxKw, double efficiency = 0.9)
    {
        if (batteryKwh <= 0 || chargerKw <= 0 || carMaxKw <= 0 || toPct <= fromPct || fromPct < 0 || toPct > 100) throw new ArgumentException("Bitte gültige Werte (von < bis, 0–100 %).");
        var e = batteryKwh * (toPct - fromPct) / 100 / efficiency; var p = Math.Min(chargerKw, carMaxKw);
        if (toPct > 80) { var hi = batteryKwh * (toPct - Math.Max(80, fromPct)) / 100 / efficiency; var lo = e - hi; return (Math.Round(e, 1), TimeSpan.FromHours(lo / p + hi / (p / 2))); }
        return (Math.Round(e, 1), TimeSpan.FromHours(e / p));
    }

    /// <summary>F20: Jahreskosten aus Fahrleistung, Verbrauch und Fixkosten.</summary>
    public static (double Year, double Month, double PerKm) YearCost(double kmYear, double per100, double price, double insurance, double tax, double maintenance, double depreciation = 0)
    {
        if (kmYear <= 0) throw new ArgumentException("Die Jahresfahrleistung muss größer als 0 sein.");
        var y = kmYear * per100 / 100 * price + insurance + tax + maintenance + depreciation;
        return (Math.Round(y, 2), Math.Round(y / 12, 2), Math.Round(y / kmYear, 3));
    }
}
