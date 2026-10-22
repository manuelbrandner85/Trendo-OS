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
        sp.Children.Add(FachGroup("car-tax", "Steuer & Erstattung", false,
            P($"Steuerwerte geprüft am {LegalRates.Checked} an amtlichen Quellen (Liste: docs/rechtswerte.md), ohne Gewähr – im Zweifel Finanzamt oder Steuerberatung fragen.", null, 11),
            FachTool("car-commute", "Pendlerpauschale", "Einfache Strecke Wohnung–Arbeit (volle km) und Arbeitstage im Jahr.", [("km", "Entfernung (km)", "30"), ("days", "Arbeitstage", "220"), ("year", "Steuerjahr", DateTime.Today.Year.ToString())],
                v => $"Entfernungspauschale {Fach.F(VehiclePlus.Commute(Fach.Num(v("km")), (int)Fach.Num(v("days")), (int)Fach.Num(v("year"))))} € im Jahr", "Berechnen"),
            FachTool("car-company", "Dienstwagen: 1-%-Regel oder Fahrtenbuch?", "Den Antrieb wählst du in der Liste. Gesamtkosten sind alle Kosten des Wagens im Jahr.",
                [("lp", "Bruttolistenpreis (€)", "40000"), ("kind", "Antrieb", "verbrenner"), ("km", "Weg zur Arbeit (km)", "20"), ("cost", "Gesamtkosten im Jahr (€)", "8000"), ("priv", "Privatanteil (%)", "30"), ("acq", "Angeschafft am (TT.MM.JJJJ)", "01.01.2026")],
                v => { var c = VehiclePlus.CompanyCar(Fach.Num(v("lp")), v("kind"), Fach.Num(v("km")), Fach.Num(v("cost")), Fach.Num(v("priv")), DateOnly.FromDateTime(DateOr(v("acq")))); return $"1-%-Regel: {Fach.F(c.OnePercent)} € · Fahrtenbuch: {Fach.F(c.Logbook)} € geldwerter Vorteil im Jahr\n→ günstiger: {c.Better}"; }, "Vergleichen"),
            FachTool("car-mile", "Kilometergeld", "Dienstfahrten mit dem eigenen Fahrzeug: steuerliche Pauschale (§ 9 EStG) oder – im öffentlichen Dienst des Bundes – nach Bundesreisekostengesetz.", [("km", "Dienstliche km", "125"), ("moto", "Motorrad/Roller? (j/n)", "n"), ("brkg", "Öffentlicher Dienst (BRKG)? (j/n)", "n"), ("int", "Erhebliches dienstliches Interesse? (j/n)", "n")],
                v => $"{Fach.F(VehiclePlus.Mileage(Fach.Num(v("km")), v("moto").Trim().StartsWith("j", StringComparison.OrdinalIgnoreCase), brkg: v("brkg").Trim().StartsWith("j", StringComparison.OrdinalIgnoreCase), significantInterest: v("int").Trim().StartsWith("j", StringComparison.OrdinalIgnoreCase)))} € Erstattung", "Berechnen")));
        sp.Children.Add(FachGroup("car-calc", "Rechner", false,
            FachTool("car-ev", "E-Auto: Verbrauch & Ersparnis", "Aus den Ladungen im Tankbuch (kWh). Vergleich mit einem Verbrenner.", [("car", "Fahrzeug", Cars.Garage.Vehicles.FirstOrDefault(x => x.Electric)?.Name ?? ""), ("ice", "Verbrenner l/100 km", "6"), ("fp", "Spritpreis (€/l)", "1,79")],
                v => { var e = VehiclePlus.Ev(Cars.Get(v("car")), Fach.Num(v("ice")), Fach.Num(v("fp"))); return $"{Fach.F(e.KwhPer100, 1)} kWh/100 km (mit Ladeverlust) · {Fach.F(e.EvCost100)} € je 100 km · Verbrenner {Fach.F(e.IceCost100)} € → {Fach.F(e.Saving100)} € gespart je 100 km"; }, "Berechnen"),
            FachTool("car-charge", "Ladezeit", "Ab 80 % lädt das Auto langsamer – Trendo rechnet dort mit halber Leistung.", [("cap", "Akku (kWh)", "60"), ("from", "Von (%)", "10"), ("to", "Bis (%)", "80"), ("kw", "Ladesäule/Wallbox (kW)", "11"), ("max", "Auto lädt max. (kW)", "11")],
                v => { var c = VehiclePlus.ChargeTime(Fach.Num(v("cap")), Fach.Num(v("from")), Fach.Num(v("to")), Fach.Num(v("kw")), Fach.Num(v("max"))); return $"{Fach.F(c.Kwh, 1)} kWh aus dem Netz · ca. {(int)c.Time.TotalHours}:{c.Time.Minutes:00} h"; }, "Berechnen"),
            FachTool("car-tire", "Reifen prüfen", "DOT-Nummer: die letzten 4 Ziffern an der Reifenflanke (Woche + Jahr). Profiltiefe in mm.", [("dot", "DOT", "2319"), ("mm", "Profil (mm)", "4"), ("w", "Winterreifen? (j/n)", "n")],
                v => { var t = VehiclePlus.Tire(v("dot"), Fach.Num(v("mm")), v("w").Trim().StartsWith("j", StringComparison.OrdinalIgnoreCase), DateTime.Today); return $"Hergestellt {t.Made:MM/yyyy}, {Fach.F(t.AgeYears, 1)} Jahre alt\n" + string.Join("\n", t.Advice); }, "Prüfen"),
            FachTool("car-share", "Fahrtkosten teilen", "Sprit oder Strom plus Maut/Parken auf alle Mitfahrenden aufteilen.", [("km", "Strecke (km)", "500"), ("c", "Verbrauch je 100 km", "6"), ("p", "Preis je l/kWh (€)", "1,79"), ("n", "Personen", "4"), ("x", "Maut/Parken (€)", "0")],
                v => { var s = VehiclePlus.Share(Fach.Num(v("km")), Fach.Num(v("c")), Fach.Num(v("p")), (int)Fach.Num(v("n")), Fach.Num(v("x"))); return $"Gesamt {Fach.F(s.Total)} € → {Fach.F(s.PerPerson)} € pro Person"; }, "Aufteilen"),
            FachTool("car-year", "Jahreskosten", "Was kostet das Auto wirklich?", [("km", "km im Jahr", "15000"), ("c", "Verbrauch je 100 km", "6"), ("p", "Preis je l/kWh (€)", "1,79"), ("ins", "Versicherung (€/Jahr)", "600"), ("tax", "Kfz-Steuer (€/Jahr)", "140"), ("m", "Wartung & Reifen (€/Jahr)", "900"), ("dep", "Wertverlust (€/Jahr)", "0")],
                v => { var y = VehiclePlus.YearCost(Fach.Num(v("km")), Fach.Num(v("c")), Fach.Num(v("p")), Fach.Num(v("ins")), Fach.Num(v("tax")), Fach.Num(v("m")), Fach.Num(v("dep"))); return $"{Fach.F(y.Year)} € im Jahr · {Fach.F(y.Month)} € im Monat · {Fach.F(y.PerKm * 100, 1)} ct je km"; }, "Berechnen")));
        return FachPage("Fahrzeug", sp);
    }
}
