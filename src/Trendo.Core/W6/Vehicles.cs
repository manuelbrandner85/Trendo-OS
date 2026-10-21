using System.Text;

namespace Trendo.Core;

public sealed class FuelEntry { public DateTime Date { get; set; } public double Odometer { get; set; } public double Amount { get; set; } public double Price { get; set; } public bool Full { get; set; } = true; }
public sealed class Trip { public DateTime Date { get; set; } public double StartKm { get; set; } public double EndKm { get; set; } public string Purpose { get; set; } = ""; public bool Business { get; set; } public string Route { get; set; } = ""; }
public sealed class Vehicle
{
    public string Name { get; set; } = "";
    public string Plate { get; set; } = "";
    public bool Electric { get; set; }
    public DateTime? InspectionDue { get; set; }
    public double? ServiceDueKm { get; set; }
    public DateTime? ServiceDueDate { get; set; }
    public List<FuelEntry> Fuel { get; set; } = [];
    public List<Trip> Trips { get; set; } = [];
    public double Odometer => Math.Max(Fuel.Select(f => f.Odometer).DefaultIfEmpty(0).Max(), Trips.Select(t => t.EndKm).DefaultIfEmpty(0).Max());
}
public sealed class Garage { public List<Vehicle> Vehicles { get; set; } = []; }
public sealed record FuelStats(double? AvgPer100, double? LastPer100, double TotalCost, double TotalAmount, double? CostPerKm, double Km);
public sealed record Reminder(string Text, bool Urgent);

/// <summary>SPEC-16: Verkehr & Fahrzeug – Tankbuch (Verbrauch, Kosten), Fahrtenbuch (dienstlich/privat), Erinnerungen (HU, Service).</summary>
public sealed class VehicleBook(string dataRoot)
{
    public Garage Garage { get; } = Fach.Load<Garage>(dataRoot, "fahrzeug", "garage");
    public void Save() => Fach.Save(dataRoot, "fahrzeug", "garage", Garage);

    public Vehicle Get(string name) => Garage.Vehicles.FirstOrDefault(v => v.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase)) ?? throw new ArgumentException($"Fahrzeug „{name}“ gibt es nicht.");

    public Vehicle Add(string name, string plate = "", bool electric = false)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Bitte dem Fahrzeug einen Namen geben.");
        if (Garage.Vehicles.Any(v => v.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("Dieses Fahrzeug gibt es schon.");
        var v = new Vehicle { Name = name.Trim(), Plate = plate.Trim().ToUpperInvariant(), Electric = electric }; Garage.Vehicles.Add(v); Save(); return v;
    }

    public FuelEntry Refuel(string vehicle, DateTime date, double odo, double amount, double price, bool full = true)
    {
        var v = Get(vehicle);
        if (amount <= 0 || price < 0) throw new ArgumentException("Menge muss größer als 0 sein, der Preis darf nicht negativ sein.");
        var prev = v.Fuel.Where(f => f.Date <= date).OrderBy(f => f.Odometer).LastOrDefault();
        if (prev is not null && odo <= prev.Odometer) throw new ArgumentException($"Der Kilometerstand muss höher sein als beim letzten Tanken ({prev.Odometer:0} km).");
        var e = new FuelEntry { Date = date, Odometer = odo, Amount = amount, Price = price, Full = full }; v.Fuel.Add(e); v.Fuel.Sort((a, b) => a.Odometer.CompareTo(b.Odometer)); Save(); return e;
    }

    public Trip Drive(string vehicle, DateTime date, double start, double end, bool business, string purpose = "", string route = "")
    {
        var v = Get(vehicle);
        if (end <= start) throw new ArgumentException("Der End-Kilometerstand muss größer als der Start sein.");
        if (business && string.IsNullOrWhiteSpace(purpose)) throw new ArgumentException("Für dienstliche Fahrten verlangt das Finanzamt Zweck und Ziel.");
        if (v.Trips.Any(t => start < t.EndKm && end > t.StartKm)) throw new ArgumentException("Diese Kilometer überschneiden sich mit einer anderen Fahrt.");
        var t = new Trip { Date = date, StartKm = start, EndKm = end, Business = business, Purpose = purpose.Trim(), Route = route.Trim() }; v.Trips.Add(t); v.Trips.Sort((a, b) => a.StartKm.CompareTo(b.StartKm)); Save(); return t;
    }

    /// <summary>Verbrauch nach der Volltank-Methode: Menge seit dem letzten Volltanken / gefahrene km.</summary>
    public static FuelStats Stats(Vehicle v)
    {
        var f = v.Fuel.OrderBy(x => x.Odometer).ToList(); var cost = f.Sum(x => x.Amount * x.Price); var total = f.Sum(x => x.Amount);
        var fulls = f.Select((x, i) => (x, i)).Where(p => p.x.Full).ToList();
        double? avg = null, last = null;
        if (fulls.Count >= 2)
        {
            var a = fulls[0]; var b = fulls[^1]; var km = b.x.Odometer - a.x.Odometer;
            avg = Math.Round(f.Skip(a.i + 1).Take(b.i - a.i).Sum(x => x.Amount) / km * 100, 2);
            var p = fulls[^2]; last = Math.Round(f.Skip(p.i + 1).Take(b.i - p.i).Sum(x => x.Amount) / (b.x.Odometer - p.x.Odometer) * 100, 2);
        }
        var span = f.Count >= 2 ? f[^1].Odometer - f[0].Odometer : 0;
        var costSpan = f.Skip(1).Sum(x => x.Amount * x.Price);
        return new(avg, last, Math.Round(cost, 2), Math.Round(total, 2), span > 0 ? Math.Round(costSpan / span, 3) : null, span);
    }

    public static (double Business, double Private, double SharePct) TripShare(Vehicle v, int? year = null)
    {
        var t = v.Trips.Where(x => year is null || x.Date.Year == year).ToList(); var b = t.Where(x => x.Business).Sum(x => x.EndKm - x.StartKm); var p = t.Where(x => !x.Business).Sum(x => x.EndKm - x.StartKm);
        return (b, p, b + p == 0 ? 0 : Math.Round(b / (b + p) * 100, 1));
    }

    /// <summary>Lücken im Fahrtenbuch (km zwischen zwei Fahrten) – das Finanzamt erkennt sie.</summary>
    public static IReadOnlyList<(double From, double To)> Gaps(Vehicle v) => v.Trips.Zip(v.Trips.Skip(1)).Where(p => p.Second.StartKm > p.First.EndKm + 0.5).Select(p => (p.First.EndKm, p.Second.StartKm)).ToList();

    public static IReadOnlyList<Reminder> Reminders(Vehicle v, DateTime today)
    {
        var r = new List<Reminder>();
        if (v.InspectionDue is { } hu) r.Add(hu < today ? new($"HU/TÜV ist seit {hu:MM/yyyy} fällig!", true) : hu < today.AddDays(60) ? new($"HU/TÜV bald fällig: {hu:MM/yyyy}.", true) : new($"HU/TÜV: {hu:MM/yyyy}.", false));
        if (v.ServiceDueKm is { } km) { var left = km - v.Odometer; r.Add(left <= 0 ? new($"Service ist überfällig (seit {-left:0} km).", true) : new($"Service in {left:0} km.", left < 1500)); }
        if (v.ServiceDueDate is { } sd) r.Add(sd < today ? new($"Service-Termin {sd:dd.MM.yyyy} ist verstrichen.", true) : new($"Service bis {sd:dd.MM.yyyy}.", sd < today.AddDays(30)));
        return r;
    }

    public static string LogCsv(Vehicle v)
    {
        var sb = new StringBuilder($"Fahrtenbuch {v.Name} {v.Plate}\nDatum;km Start;km Ende;km;Art;Zweck;Strecke\n");
        foreach (var t in v.Trips) sb.Append($"{t.Date:dd.MM.yyyy};{t.StartKm:0};{t.EndKm:0};{t.EndKm - t.StartKm:0};{(t.Business ? "dienstlich" : "privat")};{t.Purpose};{t.Route}\n");
        return sb.ToString();
    }
}
