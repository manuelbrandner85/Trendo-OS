using Avalonia.Controls;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>SPEC-16: Verkehr & Fahrzeug.</summary>
public partial class MainWindow
{
    private VehicleBook? _cars;
    public VehicleBook Cars => _cars ??= new VehicleBook(_core.DataRoot);

    private void InitSpec16() => RegisterTab(FachTabs, "fahrzeug", "Fahrzeug", "SPEC-16", VehicleView, "auto", "fahrzeug", "tanken", "verbrauch", "fahrtenbuch", "tüv", "hu", "service", "inspektion", "e-auto", "laden");

    private static DateTime DateOr(string s) => s.Trim() == "" ? DateTime.Today : DateTime.TryParse(s, Fach.De, System.Globalization.DateTimeStyles.None, out var d) ? d : throw new FormatException("Datum bitte als TT.MM.JJJJ.");

    private Control VehicleView()
    {
        var sp = new StackPanel { Spacing = 8 };
        foreach (var v in Cars.Garage.Vehicles)
        {
            var s = VehicleBook.Stats(v); var (b, p, share) = VehicleBook.TripShare(v); var unit = v.Electric ? "kWh" : "l"; var gaps = VehicleBook.Gaps(v);
            var body = new StackPanel { Spacing = 2, Children = { H($"{v.Name} {v.Plate}".Trim(), 14),
                new TextBlock { Name = $"car-stats-{v.Name}", Text = $"{Fach.F(v.Odometer, 0)} km · Verbrauch {(s.AvgPer100 is { } a ? Fach.F(a, 1) + $" {unit}/100 km" : "– (mind. 2× voll tanken)")}{(s.LastPer100 is { } l ? $" (zuletzt {Fach.F(l, 1)})" : "")} · {(s.CostPerKm is { } c ? Fach.F(c * 100, 1) + " ct/km" : "")}", Foreground = Text, FontSize = 12 },
                P($"Fahrten: {Fach.F(b, 0)} km dienstlich, {Fach.F(p, 0)} km privat ({Fach.F(share, 1)} % dienstlich){(gaps.Count > 0 ? $" · ⚠ {gaps.Count} Lücke(n) im Fahrtenbuch" : "")}", null, 11) } };
            foreach (var r in VehicleBook.Reminders(v, DateTime.Today)) body.Children.Add(P((r.Urgent ? "⚠ " : "• ") + r.Text, r.Urgent ? Brush.Parse("#FACC15") : null, 11));
            sp.Children.Add(WCard(body));
        }
        sp.Children.Add(FachTool("car-add", "Fahrzeug anlegen", "HU-Monat (MM.JJJJ) und Service-km stehen im Fahrzeugschein bzw. Serviceheft.",
            [("name", "Name", "Golf"), ("plate", "Kennzeichen", ""), ("ev", "Elektro? (j/n)", "n"), ("hu", "HU fällig (MM.JJJJ)", ""), ("skm", "Service bei km", "")],
            v => { var c = Cars.Add(v("name"), v("plate"), v("ev").Trim().StartsWith("j", StringComparison.OrdinalIgnoreCase)); if (v("hu").Trim() != "") c.InspectionDue = DateOr("01." + v("hu").Trim()); if (v("skm").Trim() != "") c.ServiceDueKm = Fach.Num(v("skm"), "Service-km"); Cars.Save(); Show("fahrzeug"); return "Angelegt."; }, "Anlegen"));
        if (Cars.Garage.Vehicles.Count > 0)
        {
            var first = Cars.Garage.Vehicles[0].Name;
            sp.Children.Add(FachTool("car-fuel", "Getankt / geladen", "Menge in Litern (E-Auto: kWh), Preis pro Liter bzw. kWh. „voll“ = bis zum Abschalten getankt.",
                [("car", "Fahrzeug", first), ("date", "Datum", DateTime.Today.ToString("dd.MM.yyyy")), ("odo", "km-Stand", ""), ("amt", "Menge", ""), ("price", "Preis je Einheit (€)", "1,79"), ("full", "voll? (j/n)", "j")],
                v => { Cars.Refuel(v("car"), DateOr(v("date")), Fach.Num(v("odo"), "km-Stand"), Fach.Num(v("amt"), "Menge"), Fach.Num(v("price"), "Preis"), !v("full").Trim().StartsWith("n", StringComparison.OrdinalIgnoreCase)); Show("fahrzeug"); return "Eingetragen."; }, "Eintragen"));
            sp.Children.Add(FachTool("car-trip", "Fahrt eintragen", "Dienstlich: Zweck und Ziel angeben (Pflicht fürs Finanzamt).",
                [("car", "Fahrzeug", first), ("date", "Datum", DateTime.Today.ToString("dd.MM.yyyy")), ("s", "km Start", ""), ("e", "km Ende", ""), ("biz", "dienstlich? (j/n)", "j"), ("why", "Zweck / Ziel", "")],
                v => { Cars.Drive(v("car"), DateOr(v("date")), Fach.Num(v("s"), "km Start"), Fach.Num(v("e"), "km Ende"), v("biz").Trim().StartsWith("j", StringComparison.OrdinalIgnoreCase), v("why")); Show("fahrzeug"); return "Eingetragen."; }, "Eintragen"));
            sp.Children.Add(FachTool("car-export", "Fahrtenbuch speichern", "Als CSV für Steuerberater oder Excel.",
                [("car", "Fahrzeug", first)],
                v => { var c = Cars.Get(v("car")); var p = Path.Combine(Fach.StoreDir(_core.DataRoot, "fahrzeug"), $"fahrtenbuch-{string.Concat(c.Name.Where(char.IsLetterOrDigit))}.csv"); File.WriteAllText(p, VehicleBook.LogCsv(c)); Launch(p); return "Gespeichert: " + p; }, "Speichern"));
        }
        return FachPage("Fahrzeug", sp);
    }
}
