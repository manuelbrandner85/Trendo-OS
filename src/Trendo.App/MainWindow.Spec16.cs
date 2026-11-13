using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Arch;

namespace Trendo.App;

/// <summary>SPEC-16 / T-0268: Verkehr & Fahrzeug als Programm „Fahrzeugbuch“ (Vorbild Spritmonitor/Fuelio, nur Funktionsumfang):
/// Fahrzeugliste, Übersicht mit Verbrauchs- und Kostendiagramm und Erinnerungen, Tankbuch und Fahrtenbuch als bearbeitbare Tabellen,
/// Fahrzeugdaten, Export und Rechner (Steuer, E-Auto, Reifen, Kosten), die sofort rechnen. Ersetzt die Formularseite.</summary>
public partial class MainWindow
{
    private VehicleBook? _cars;
    public VehicleBook Cars => _cars ??= new VehicleBook(_core.DataRoot);
    public string? CarSel;
    public string CarTab = "uebersicht";
    private string _carMsg = "";
    private string? _carCalcFor;

    private void InitSpec16() => RegisterTab(FachTabs, "fahrzeug", "Fahrzeugbuch", "SPEC-16", VehicleView, "auto", "fahrzeug", "tanken", "verbrauch", "fahrtenbuch", "tankbuch", "tüv", "hu", "service", "inspektion", "e-auto", "laden", "spritmonitor");

    private static DateTime DateOr(string s) => s.Trim() == "" ? DateTime.Today : DateTime.TryParse(s, Fach.De, System.Globalization.DateTimeStyles.None, out var d) ? d : throw new FormatException("Datum bitte als TT.MM.JJJJ.");

    void CarRun(Action a) { try { a(); } catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException or FormatException or UnauthorizedAccessException) { _carMsg = "" + ex.Message; } Show("fahrzeug"); }

    private Control VehicleView()
    {
        if (CarSel is null || !Cars.Garage.Vehicles.Any(v => v.Name == CarSel)) CarSel = Cars.Garage.Vehicles.FirstOrDefault()?.Name;
        var car = CarSel is null ? null : Cars.Get(CarSel);
        if (_carCalcFor != CarSel) // Rechner neu mit den Werten des gewählten Fahrzeugs vorbelegen
        {
            foreach (var k in _calcIn.Keys.Where(k => k is "car-year-c" or "car-year-p" or "car-share-c" or "car-share-p" or "car-company-priv" or "car-mile-km").ToList()) _calcIn.Remove(k);
            _carCalcFor = CarSel;
        }
        Button Btn(string name, string label, Action go, bool accent = false) { var b = new Button { Name = name, Content = label, Padding = new Thickness(10, 4), Margin = new Thickness(0, 0, 4, 4) }; if (accent) { b.Background = Accent; b.Foreground = Brushes.White; } b.Click += (_, _) => go(); return b; }

        // Links: Fahrzeuge
        var list = new StackPanel { Name = "car-list", Spacing = 6 };
        foreach (var v in Cars.Garage.Vehicles)
        {
            var rem = VehicleBook.Reminders(v, DateTime.Today); var on = v.Name == CarSel; var nm = v.Name;
            var b = new Button { Name = $"car-pick-{v.Name}".Replace(' ', '_'), HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(10, 8),
                Background = on ? Brush.Parse("#C9A22733") : Brush.Parse("#2E2E2E"), BorderBrush = on ? Accent : Edge, BorderThickness = new Thickness(on ? 2 : 1), CornerRadius = new CornerRadius(10),
                Content = new StackPanel { Spacing = 1, Children = {
                    new TextBlock { Text = (v.Electric ? "• " : "") + v.Name, Foreground = Text, FontWeight = FontWeight.SemiBold, FontSize = 14 },
                    new TextBlock { Text = $"{v.Plate} · {Fach.F(v.Odometer, 0)} km".Trim(' ', '·'), Foreground = Muted, FontSize = 11 },
                    new TextBlock { Text = rem.Any(r => r.Urgent) ? "" + rem.First(r => r.Urgent).Text : rem.Count > 0 ? "• " + rem[0].Text : "", Foreground = Brush.Parse(rem.Any(r => r.Urgent) ? "#FACC15" : "#8FA3BF"), FontSize = 11, TextWrapping = TextWrapping.Wrap } } } };
            b.Click += (_, _) => { CarSel = nm; _carMsg = ""; Show("fahrzeug"); };
            list.Children.Add(b);
        }
        var nName = new TextBox { Name = "car-new-name", PlaceholderText = "Name, z. B. Golf" }; var nPlate = new TextBox { Name = "car-new-plate", PlaceholderText = "Kennzeichen" }; var nEv = new CheckBox { Name = "car-new-ev", Content = "Elektro" };
        list.Children.Add(H("Fahrzeug anlegen", 12)); list.Children.Add(nName); list.Children.Add(nPlate);
        list.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { nEv, Btn("car-add", "＋ Anlegen", () => CarRun(() => { var c = Cars.Add(nName.Text ?? "", nPlate.Text ?? "", nEv.IsChecked == true); CarSel = c.Name; _carMsg = "Angelegt – rechts HU und Service eintragen."; })) } });
        if (Cars.Garage.Vehicles.Count == 0) list.Children.Add(Btn("car-demo", "Beispielfahrzeug laden", () => CarRun(() => { var v = VehiclePlus.SeedDemo(Cars, DateTime.Today); CarSel = v.Name; _carMsg = "Beispielfahrzeug mit erfundenen Werten geladen."; })));
        var left = new Border { Name = "car-left", Width = 230, CornerRadius = new CornerRadius(14), Background = TileBg, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), Padding = new Thickness(10), Child = new ScrollViewer { Content = list } };

        // Mitte: Reiter
        var tabs = new WrapPanel { Name = "car-tabs" };
        foreach (var (id, label) in new[] { ("uebersicht", "Übersicht"), ("tank", car?.Electric == true ? "Ladebuch" : "Tankbuch"), ("fahrten", "Fahrtenbuch") })
        {
            var tid = id; var b = Btn("car-tab-" + id, label, () => { CarTab = tid; Show("fahrzeug"); }); if (CarTab == id) { b.Background = Accent; b.Foreground = Brushes.White; }
            tabs.Children.Add(b);
        }
        if (_carMsg.Length > 0) tabs.Children.Add(new TextBlock { Name = "car-msg", Text = _carMsg, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 4), TextWrapping = TextWrapping.Wrap, MaxWidth = 560, FontSize = 12, Foreground = Brush.Parse(_carMsg.StartsWith('⚠') ? "#F87171" : "#4ADE80") });
        Control content = car is null ? EmptyState("car-empty", "diagramm", "Noch kein Fahrzeug", // T7 (07.11.2026)
                "Links ein Fahrzeug anlegen – dann siehst du hier Verbrauch, Kosten je Monat, Tankbuch und Fahrtenbuch.",
                Act("car-empty-demo", "Beispielfahrzeug laden", () => CarRun(() => { var v = VehiclePlus.SeedDemo(Cars, DateTime.Today); CarSel = v.Name; _carMsg = "Beispielfahrzeug mit erfundenen Werten geladen."; }), true))
            : CarTab switch { "tank" => CarFuel(car), "fahrten" => CarTrips(car), _ => CarOverview(car) };
        var center = new DockPanel(); DockPanel.SetDock(tabs, Avalonia.Controls.Dock.Top); center.Children.Add(tabs); center.Children.Add(new ScrollViewer { Content = content });
        var mid = new Border { CornerRadius = new CornerRadius(14), Background = TileBg, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), Padding = new Thickness(12), Child = center };
        var right = new Border { Name = "car-side", Width = 320, CornerRadius = new CornerRadius(14), Background = TileBg, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), Padding = new Thickness(12), Child = new ScrollViewer { Content = CarSide(car) } };
        var body = new Grid { Name = "car-root", ColumnDefinitions = new ColumnDefinitions("Auto,12,*,12,Auto"), Children = { left, Pos(mid, 2), Pos(right, 4) } };
        return FitPage("Fahrzeugbuch", new Panel(), body);
    }

    Control CarOverview(Vehicle v)
    {
        var sp = new StackPanel { Spacing = 10 }; var s = VehicleBook.Stats(v); var unit = v.Electric ? "kWh" : "l"; var (biz, priv, share) = VehicleBook.TripShare(v);
        Border Kpi(string name, string label, string value, string sub) => new() { Name = name, Width = 170, Margin = new Thickness(0, 0, 8, 8), Padding = new Thickness(12, 8), CornerRadius = new CornerRadius(12), Background = Brush.Parse("#2E2E2E"), BorderBrush = Edge, BorderThickness = new Thickness(1),
            Child = new StackPanel { Children = { new TextBlock { Text = label, Foreground = Muted, FontSize = 11 }, new TextBlock { Text = value, Foreground = Text, FontSize = 22, FontWeight = FontWeight.Bold }, new TextBlock { Text = sub, Foreground = Muted, FontSize = 11 } } } };
        sp.Children.Add(new WrapPanel { Name = "car-kpis", Children = {
            Kpi("car-kpi-avg", "Ø Verbrauch", s.AvgPer100 is { } a ? $"{Fach.F(a, 1)} {unit}" : "–", s.LastPer100 is { } l ? $"zuletzt {Fach.F(l, 1)} {unit}/100 km" : "mind. 2× voll tanken"),
            Kpi("car-kpi-km", "Kosten je km", s.CostPerKm is { } c ? $"{Fach.F(c * 100, 1)} ct" : "–", $"über {Fach.F(s.Km, 0)} km"),
            Kpi("car-kpi-cost", v.Electric ? "Strom gesamt" : "Sprit gesamt", $"{Fach.F(s.TotalCost, 0)} €", $"{Fach.F(s.TotalAmount, 0)} {unit}"),
            Kpi("car-kpi-odo", "km-Stand", Fach.F(v.Odometer, 0), $"{Fach.F(share, 0)} % dienstlich") } });
        foreach (var r in VehicleBook.Reminders(v, DateTime.Today)) sp.Children.Add(new TextBlock { Text = (r.Urgent ? "" : "• ") + r.Text, Foreground = Brush.Parse(r.Urgent ? "#FACC15" : "#C9D6EA"), FontSize = 13 });
        var cons = VehiclePlus.Consumption(v);
        sp.Children.Add(H($"Verbrauch je Tankfüllung ({unit}/100 km)", 13));
        sp.Children.Add(new Border { Name = "car-chart-cons", Height = 180, CornerRadius = new CornerRadius(10), ClipToBounds = true, Child = new ShapeView((w, h) => CarLineChart(cons.Select(x => (x.Date, x.Per100)).ToList(), w, h, "#4FA3FF", s.AvgPer100), "#212121") });
        var month = VehiclePlus.MonthlyCost(v, DateTime.Today);
        sp.Children.Add(H("Kosten je Monat (€)", 13));
        sp.Children.Add(new Border { Name = "car-chart-cost", Height = 150, CornerRadius = new CornerRadius(10), ClipToBounds = true, Child = new ShapeView((w, h) =>
        {
            var sh = new List<Shape>(); var max = Math.Max(1, month.Max(m => m.Cost)); var bw = w / month.Count;
            for (var i = 0; i < month.Count; i++)
            {
                var bh = (h - 34) * month[i].Cost / max; var x = i * bw;
                if (month[i].Cost > 0) sh.Add(new DPoly([(x + 4, h - 18 - bh), (x + bw - 4, h - 18 - bh), (x + bw - 4, h - 18), (x + 4, h - 18)], i == month.Count - 1 ? "#F5A524" : "#22B35E", null, 0, "DATEN"));
                sh.Add(new DText(x + bw / 2, h - 5, month[i].Month.ToString("MMM", De), 10, "#8FA3BF", "TEXT"));
                if (month[i].Cost > 0) sh.Add(new DText(x + bw / 2, h - 23 - bh, Fach.F(month[i].Cost, 0), 10, "#FFFFFF", "TEXT"));
            }
            return sh;
        }, "#212121") });
        return sp;
    }

    static List<Shape> CarLineChart(List<(DateTime X, double Y)> pts, double w, double h, string col, double? avg)
    {
        var s = new List<Shape>();
        if (pts.Count == 0) { s.Add(new DText(w / 2, h / 2, "Noch zu wenig Volltankungen", 13, "#8FA3BF", "TEXT")); return s; }
        double lo = pts.Min(p => p.Y), hi = pts.Max(p => p.Y); if (hi - lo < 0.5) { lo -= 0.5; hi += 0.5; } var pad = (hi - lo) * 0.15; lo -= pad; hi += pad;
        const double L = 40, T = 10, B = 22; var pw = w - L - 10; var ph = h - T - B;
        var t0 = pts[0].X; var span = Math.Max(1, (pts[^1].X - t0).TotalSeconds);
        double X(DateTime t) => L + (pts.Count == 1 ? pw / 2 : (t - t0).TotalSeconds / span * pw); double Y(double v) => T + (hi - v) / (hi - lo) * ph;
        for (var k = 0; k <= 3; k++) { var v = lo + (hi - lo) * k / 3; s.Add(new DLine(L, Y(v), L + pw, Y(v), "#FFFFFF14", 1, "RASTER")); s.Add(new DText(L - 18, Y(v) + 4, v.ToString("0.0", De), 10, "#8FA3BF", "ACHSE")); }
        if (avg is { } a) { s.Add(new DLine(L, Y(a), L + pw, Y(a), "#F5A524", 1.2, "MITTEL", true)); s.Add(new DText(L + pw - 30, Y(a) - 5, "Ø " + a.ToString("0.0", De), 10, "#F5A524", "MITTEL")); }
        s.Add(new DPoly(pts.Select(p => (X(p.X), Y(p.Y))).ToList(), null, col, 2, "VERLAUF", Closed: false));
        foreach (var p in pts) s.Add(new DPoly([(X(p.X) - 3, Y(p.Y) - 3), (X(p.X) + 3, Y(p.Y) - 3), (X(p.X) + 3, Y(p.Y) + 3), (X(p.X) - 3, Y(p.Y) + 3)], col, null, 0, "PUNKT"));
        s.Add(new DText(L + 20, h - 5, pts[0].X.ToString("dd.MM.yy", De), 10, "#8FA3BF", "ACHSE")); s.Add(new DText(L + pw - 20, h - 5, pts[^1].X.ToString("dd.MM.yy", De), 10, "#8FA3BF", "ACHSE"));
        return s;
    }

    Control CarFuel(Vehicle v)
    {
        var sp = new StackPanel { Name = "car-fuel-table", Spacing = 3 }; var unit = v.Electric ? "kWh" : "l";
        var cols = "100,90,80,80,70,50,Auto";
        var hdr = new Grid { ColumnDefinitions = new ColumnDefinitions(cols) };
        foreach (var (t, c) in new[] { ("Datum", 0), ("km-Stand", 1), ($"Menge ({unit})", 2), ($"€/{unit}", 3), ("Betrag", 4), ("voll", 5) }) hdr.Children.Add(Pos(new TextBlock { Text = t, Foreground = Muted, FontSize = 11 }, c));
        sp.Children.Add(hdr);
        var rows = v.Fuel.OrderByDescending(x => x.Odometer).ToList();
        for (var k = 0; k < rows.Count; k++)
        {
            var e = rows[k]; var i = k;
            TextBox Cell(string key, string val, Action<string> set) { var t = new TextBox { Name = $"car-f-{key}-{i}", Text = val, Margin = new Thickness(1, 0) }; void C() { if ((t.Text ?? "") != val) CarRun(() => { set(t.Text ?? ""); Cars.Save(); }); } t.LostFocus += (_, _) => C(); OnEnter(t, C); return t; }
            var full = new CheckBox { Name = $"car-f-full-{i}", IsChecked = e.Full }; full.IsCheckedChanged += (_, _) => { if ((full.IsChecked == true) != e.Full) CarRun(() => { e.Full = full.IsChecked == true; Cars.Save(); }); };
            var del = new Button { Name = $"car-f-del-{i}", Content = "✕", Padding = new Thickness(6, 2) }; ToolTip.SetTip(del, "Eintrag löschen"); del.Click += (_, _) => CarRun(() => { v.Fuel.Remove(e); Cars.Save(); });
            sp.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions(cols), Children = {
                Cell("date", e.Date.ToString("dd.MM.yyyy"), x => e.Date = DateOr(x)), Pos(Cell("odo", e.Odometer.ToString("0", De), x => e.Odometer = Fach.Num(x, "km-Stand")), 1),
                Pos(Cell("amt", e.Amount.ToString("0.##", De), x => e.Amount = Fach.Num(x, "Menge")), 2), Pos(Cell("price", e.Price.ToString("0.###", De), x => e.Price = Fach.Num(x, "Preis")), 3),
                Pos(new TextBlock { Text = Fach.F(e.Amount * e.Price) + " €", Foreground = Text, VerticalAlignment = VerticalAlignment.Center, FontSize = 12 }, 4), Pos(full, 5), Pos(del, 6) } });
        }
        // Neue Tankung (oben über der Liste als Eingabezeile)
        var nd = new TextBox { Name = "car-f-new-date", Text = DateTime.Today.ToString("dd.MM.yyyy"), Margin = new Thickness(1, 0) }; var no = new TextBox { Name = "car-f-new-odo", PlaceholderText = "km", Margin = new Thickness(1, 0) };
        var na = new TextBox { Name = "car-f-new-amt", PlaceholderText = unit, Margin = new Thickness(1, 0) }; var np = new TextBox { Name = "car-f-new-price", Text = (rows.FirstOrDefault()?.Price ?? (v.Electric ? 0.39 : 1.79)).ToString("0.###", De), Margin = new Thickness(1, 0) };
        var nf = new CheckBox { Name = "car-f-new-full", IsChecked = true };
        void Add() => CarRun(() => { Cars.Refuel(v.Name, DateOr(nd.Text ?? ""), Fach.Num(no.Text ?? "", "km-Stand"), Fach.Num(na.Text ?? "", "Menge"), Fach.Num(np.Text ?? "", "Preis"), nf.IsChecked == true); _carMsg = ""; });
        foreach (var t in new[] { nd, no, na, np }) OnEnter(t, Add);
        var add = new Button { Name = "car-f-add", Content = "＋", Padding = new Thickness(10, 2), Background = Accent, Foreground = Brushes.White }; add.Click += (_, _) => Add();
        sp.Children.Insert(1, new Grid { ColumnDefinitions = new ColumnDefinitions(cols), Margin = new Thickness(0, 0, 0, 6), Children = { nd, Pos(no, 1), Pos(na, 2), Pos(np, 3), Pos(nf, 5), Pos(add, 6) } });
        if (rows.Count == 0) sp.Children.Add(P("Noch keine Einträge – oben km-Stand und Menge eintragen und Enter drücken.", null, 12));
        return sp;
    }

    int _carYear = DateTime.Today.Year; Trip? _carFix, _carVoid;
    public int CarTripYear { get => _carYear; set => _carYear = value; }

    /// <summary>Fahrtenbuch (Masterprompt 131): Schnelleingabe mit Art, Ziel, Zweck, Partner und häufigen Fahrten; Monate mit Summen und Festschreiben;
    /// Korrigieren und Stornieren nur mit Grund (bleibt sichtbar); Jahr blättern; PDF fürs Finanzamt.</summary>
    Control CarTrips(Vehicle v)
    {
        var sp = new StackPanel { Name = "car-trip-table", Spacing = 6 }; var (biz, priv, share) = VehicleBook.TripShare(v, _carYear); var gaps = VehicleBook.Gaps(v);
        var commute = v.Trips.Where(t => !t.Voided && t.Date.Year == _carYear && t.Art == "arbeitsweg").Sum(t => t.Km);
        // Kopf: Jahr, Summen, Anteil als Balken, PDF
        var total = Math.Max(1, biz + priv);
        var bar = new Grid { Height = 12, ColumnDefinitions = new ColumnDefinitions($"{Math.Max(0.001, biz)}*,{Math.Max(0.001, priv - commute)}*,{Math.Max(0.001, commute)}*"), Children = {
            new Border { Background = Brush.Parse("#2F86F6"), CornerRadius = new CornerRadius(6, 0, 0, 6) }, Pos(new Border { Background = Brush.Parse("#64748B") }, 1), Pos(new Border { Background = Brush.Parse("#F5A524"), CornerRadius = new CornerRadius(0, 6, 6, 0) }, 2) } };
        sp.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Children = {
            new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { Btn("car-t-year-prev", "◀", () => { _carYear--; Show("fahrzeug"); }), new TextBlock { Name = "car-t-year", Text = _carYear.ToString(), Foreground = GoldText(), FontSize = 20, FontWeight = FontWeight.Bold, VerticalAlignment = VerticalAlignment.Center }, Btn("car-t-year-next", "▶", () => { _carYear++; Show("fahrzeug"); }) } },
            Pos(new StackPanel { Margin = new Thickness(16, 0), Spacing = 4, VerticalAlignment = VerticalAlignment.Center, Children = {
                new TextBlock { Name = "car-trip-sum", Text = $"{Fach.F(biz, 0)} km dienstlich · {Fach.F(priv - commute, 0)} km privat · {Fach.F(commute, 0)} km Arbeitsweg · {Fach.F(share, 1)} % dienstlich", Foreground = Text, FontSize = 13, FontWeight = FontWeight.SemiBold },
                bar } }, 1),
            Pos(Btn("car-t-pdf", "Fahrtenbuch als PDF", () => CarRun(() => { var f = Path.Combine(WorkFolder("Fahrzeug"), $"Fahrtenbuch {_carYear} {DevStudio.SafeName(v.Name)}.pdf"); Directory.CreateDirectory(Path.GetDirectoryName(f)!); VehicleBook.LogPdf(f, v, _carYear); _carMsg = "Gespeichert: " + f; NotifyFileResult(_carMsg); })), 2) } });
        foreach (var (a, b) in gaps) sp.Children.Add(new TextBlock { Text = $"Lücke im Fahrtenbuch: {Fach.F(a, 0)} → {Fach.F(b, 0)} km ({Fach.F(b - a, 0)} km fehlen)", Foreground = Brush.Parse("#FACC15"), FontSize = 12 });

        // Schnelleingabe
        var nd = new TextBox { Name = "car-t-new-date", Text = DateTime.Today.ToString("dd.MM.yyyy"), Width = 100 }; var ns = new TextBox { Name = "car-t-new-start", Text = v.Odometer > 0 ? v.Odometer.ToString("0", De) : "", Width = 90 };
        var ne = new TextBox { Name = "car-t-new-end", PlaceholderText = "km Ende", Width = 90 };
        var nb = new CheckBox { Name = "car-t-new-biz", Content = new TextBlock { Text = "dienstlich", Foreground = Text } }; var nc = new CheckBox { Name = "car-t-new-commute", Content = new TextBlock { Text = "Arbeitsweg", Foreground = Text } };
        nb.IsCheckedChanged += (_, _) => { if (nb.IsChecked == true) nc.IsChecked = false; }; nc.IsCheckedChanged += (_, _) => { if (nc.IsChecked == true) nb.IsChecked = false; };
        var nt = new TextBox { Name = "car-t-new-dest", PlaceholderText = "Ziel (Ort, Adresse)", Width = 190 }; var nw = new TextBox { Name = "car-t-new-why", PlaceholderText = "Zweck, z. B. Kundentermin", Width = 200 };
        var np = new TextBox { Name = "car-t-new-partner", PlaceholderText = "Geschäftspartner", Width = 160 };
        void Add() => CarRun(() =>
        {
            var kind = nb.IsChecked == true ? "dienstlich" : nc.IsChecked == true ? "arbeitsweg" : "privat";
            var t = Cars.Log(v.Name, DateOr(nd.Text ?? ""), Fach.Num(ns.Text ?? "", "km Start"), Fach.Num(ne.Text ?? "", "km Ende"), kind, nt.Text ?? "", nw.Text ?? "", np.Text ?? "");
            _carMsg = kind == "dienstlich" && t.Destination.Length == 0 ? "Eingetragen. Tipp: Das Finanzamt erwartet bei dienstlichen Fahrten auch das Ziel." : ""; _carYear = t.Date.Year;
        });
        foreach (var t in new[] { nd, ns, ne, nt, nw, np }) OnEnter(t, Add);
        var add = new Button { Name = "car-t-add", Content = "＋ Eintragen", Padding = new Thickness(12, 4), Background = Accent, Foreground = Brushes.White }; add.Click += (_, _) => Add();
        var entry = new WrapPanel { Children = { nd, ns, ne, nb, nc, nt, nw, np, add } };
        foreach (var c in entry.Children.OfType<Control>()) { c.Margin = new Thickness(0, 0, 6, 6); c.VerticalAlignment = VerticalAlignment.Center; }
        var freq = VehicleBook.Frequent(v);
        var chips = new WrapPanel();
        if (freq.Count > 0) chips.Children.Add(new TextBlock { Text = "Häufig:", Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
        for (var i = 0; i < freq.Count; i++)
        {
            var fr = freq[i];
            var chip = Btn($"car-t-freq-{i}", $"{(fr.Destination.Length > 0 ? fr.Destination : fr.Purpose)} · {Fach.F(fr.Km, 0)} km", () =>
            {
                nt.Text = fr.Destination; nw.Text = fr.Purpose; np.Text = fr.Partner; nb.IsChecked = fr.Kind == "dienstlich"; nc.IsChecked = fr.Kind == "arbeitsweg";
                if (double.TryParse(ns.Text, NumberStyles.Number, De, out var st)) ne.Text = (st + fr.Km).ToString("0", De);
            });
            chip.Margin = new Thickness(0, 0, 6, 6); chips.Children.Add(chip);
        }
        sp.Children.Add(new Border { Background = Brush.Parse("#2E2E2E"), CornerRadius = new CornerRadius(12), Padding = new Thickness(12, 10), BorderBrush = Edge, BorderThickness = new Thickness(1), Child = new StackPanel { Spacing = 4, Children = { H("Neue Fahrt", 13), entry, chips } } });

        // Monate
        var months = VehicleBook.Months(v, _carYear); var all = v.Trips.Where(t => t.Date.Year == _carYear).OrderByDescending(t => t.StartKm).ToList(); var idx = 0;
        if (all.Count == 0) sp.Children.Add(P($"Im Jahr {_carYear} noch keine Fahrten.", Muted, 12));
        foreach (var grp in all.GroupBy(t => t.Date.Month))
        {
            var m = months.FirstOrDefault(x => x.Month == grp.Key); var mon = grp.Key;
            var head = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 8, 0, 2), Children = {
                new TextBlock { Text = $"{new DateTime(_carYear, mon, 1).ToString("MMMM", De)} · {Fach.F(m?.Business ?? 0, 0)} km dienstlich · {Fach.F(m?.Private ?? 0, 0)} km privat · {Fach.F(m?.Commute ?? 0, 0)} km Arbeitsweg", Foreground = GoldText(), FontSize = 13, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center } } };
            head.Children.Add(Pos(m?.Locked == true ? new TextBlock { Name = $"car-t-locked-{mon}", Text = "festgeschrieben", Foreground = Muted, FontSize = 12 } : Btn($"car-t-lock-{mon}", "Monat festschreiben", () => CarRun(() => { var n = Cars.LockMonth(v.Name, _carYear, mon); _carMsg = $"{n} Fahrten im {new DateTime(_carYear, mon, 1).ToString("MMMM", De)} festgeschrieben – Änderungen sind nicht mehr möglich."; })), 1));
            sp.Children.Add(head);
            foreach (var t in grp)
            {
                var i = idx++; var tt = t;
                var col = t.Art switch { "dienstlich" => "#2F86F6", "arbeitsweg" => "#F5A524", _ => "#64748B" };
                var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = {
                    new TextBlock { Name = $"car-t-why-{i}", Text = t.Art == "dienstlich" ? string.Join(" · ", new[] { t.Destination, t.Purpose, t.Partner }.Where(x => x.Length > 0)) : t.Art == "arbeitsweg" ? "Wohnung – Arbeitsstätte" : "Privatfahrt", Foreground = Text, FontSize = 12.5, TextTrimming = TextTrimming.CharacterEllipsis, TextDecorations = t.Voided ? TextDecorations.Strikethrough : null },
                    new TextBlock { Text = t.Voided ? "storniert: " + t.VoidReason : t.Corrections is { Count: > 0 } c ? "korrigiert: " + c[^1] : "", Foreground = Brush.Parse(t.Voided ? "#F87171" : "#FACC15"), FontSize = 10.5, IsVisible = t.Voided || t.Corrections is { Count: > 0 }, TextTrimming = TextTrimming.CharacterEllipsis } } };
                var acts = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
                if (!t.Locked && !t.Voided)
                {
                    acts.Children.Add(Btn($"car-t-fix-{i}", "Korrigieren", () => { _carFix = tt; _carVoid = null; Show("fahrzeug"); }));
                    acts.Children.Add(Btn($"car-t-void-{i}", "Stornieren …", () => { _carVoid = tt; _carFix = null; Show("fahrzeug"); }));
                }
                else if (t.Locked) acts.Children.Add(new TextBlock { Text = "🔒", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center });
                sp.Children.Add(new Border { Name = $"car-t-row-{i}", Background = Brush.Parse(i % 2 == 0 ? "#0CFFFFFF" : "#00000000"), CornerRadius = new CornerRadius(6), Padding = new Thickness(6, 3), Opacity = t.Voided ? 0.6 : 1,
                    Child = new Grid { ColumnDefinitions = new ColumnDefinitions("6,90,120,70,90,*,Auto"), Children = {
                        new Border { Background = Brush.Parse(col), CornerRadius = new CornerRadius(3) },
                        Pos(new TextBlock { Text = t.Date.ToString("dd.MM.yyyy"), Foreground = Text, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) }, 1),
                        Pos(new TextBlock { Text = $"{Fach.F(t.StartKm, 0)} – {Fach.F(t.EndKm, 0)}", Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center }, 2),
                        Pos(new TextBlock { Text = $"{Fach.F(t.Km, 0)} km", Foreground = Text, FontSize = 12.5, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center }, 3),
                        Pos(new TextBlock { Text = t.Art, Foreground = Brush.Parse(col), FontSize = 12, VerticalAlignment = VerticalAlignment.Center }, 4), Pos(info, 5), Pos(acts, 6) } } });
                if (ReferenceEquals(_carFix, t)) sp.Children.Add(CarFixPanel(v, t));
                if (ReferenceEquals(_carVoid, t))
                {
                    var why = new TextBox { Name = "car-t-void-reason", PlaceholderText = "Grund, z. B. doppelt eingetragen", Width = 280 };
                    sp.Children.Add(new Border { Name = "car-t-void-ask", Background = Brush.Parse("#33F59E0B"), CornerRadius = new CornerRadius(8), Padding = new Thickness(10), Child = new WrapPanel { Children = {
                        new TextBlock { Text = "Fahrt stornieren? Sie bleibt mit Grund sichtbar, zählt aber nicht mehr.", Foreground = Text, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) }, why,
                        Btn("car-t-void-yes", "Ja, stornieren", () => CarRun(() => { Cars.Void(v.Name, tt, why.Text ?? ""); _carVoid = null; _carMsg = "Storniert."; })), Btn("car-t-void-no", "Nein", () => { _carVoid = null; Show("fahrzeug"); }) } } });
                }
            }
        }
        return sp;
    }

    Control CarFixPanel(Vehicle v, Trip t)
    {
        var d = new TextBox { Name = "car-t-fix-date", Text = t.Date.ToString("dd.MM.yyyy"), Width = 100 }; var s = new TextBox { Name = "car-t-fix-start", Text = t.StartKm.ToString("0", De), Width = 90 }; var e = new TextBox { Name = "car-t-fix-end", Text = t.EndKm.ToString("0", De), Width = 90 };
        var kind = new ComboBox { Name = "car-t-fix-kind", ItemsSource = VehicleBook.Kinds, SelectedItem = t.Art, Width = 130 };
        var dest = new TextBox { Name = "car-t-fix-dest", Text = t.Destination, PlaceholderText = "Ziel", Width = 170 }; var why = new TextBox { Name = "car-t-fix-why", Text = t.Purpose, PlaceholderText = "Zweck", Width = 180 }; var part = new TextBox { Name = "car-t-fix-partner", Text = t.Partner, PlaceholderText = "Partner", Width = 150 };
        var reason = new TextBox { Name = "car-t-fix-reason", PlaceholderText = "Grund der Korrektur (Pflicht)", Width = 240 };
        var save = Btn("car-t-fix-save", "Korrektur speichern", () => CarRun(() =>
        {
            Cars.Correct(v.Name, t, reason.Text ?? "", DateOr(d.Text ?? ""), Fach.Num(s.Text ?? "", "km Start"), Fach.Num(e.Text ?? "", "km Ende"), (string)kind.SelectedItem!, dest.Text ?? "", why.Text ?? "", part.Text ?? "");
            _carFix = null; _carMsg = "Korrektur gespeichert und an der Fahrt vermerkt.";
        }));
        var cancel = Btn("car-t-fix-cancel", "Abbrechen", () => { _carFix = null; Show("fahrzeug"); });
        var w = new WrapPanel { Children = { d, s, e, kind, dest, why, part, reason, save, cancel } };
        foreach (var c in w.Children.OfType<Control>()) { c.Margin = new Thickness(0, 0, 6, 6); c.VerticalAlignment = VerticalAlignment.Center; }
        return new Border { Name = "car-t-fix", Background = Brush.Parse("#22C9A227"), BorderBrush = Brush.Parse("#D4A84B"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(10), Child = w };
    }

    Control CarSide(Vehicle? car)
    {
        var sp = new StackPanel { Spacing = 5 };
        Button B(string name, string label, Action go) { var b = new Button { Name = name, Content = label, Padding = new Thickness(10, 4), Margin = new Thickness(0, 0, 4, 4) }; b.Click += (_, _) => CarRun(go); return b; }
        if (car is not null)
        {
            sp.Children.Add(H(car.Name, 13));
            TextBox F(string name, string label, string val, Action<string> set) { var t = new TextBox { Name = name, Text = val, Width = 130 }; void C() { if ((t.Text ?? "") != val) CarRun(() => { set(t.Text ?? ""); Cars.Save(); }); } t.LostFocus += (_, _) => C(); OnEnter(t, C); sp.Children.Add(Row(P(label, Text, 12), t)); return t; }
            F("car-p-plate", "Kennzeichen", car.Plate, x => car.Plate = x.Trim());
            F("car-p-hu", "HU fällig (MM.JJJJ)", car.InspectionDue?.ToString("MM.yyyy") ?? "", x => car.InspectionDue = x.Trim() == "" ? null : DateOr("01." + x.Trim()));
            F("car-p-skm", "Service bei km", car.ServiceDueKm?.ToString("0", De) ?? "", x => car.ServiceDueKm = x.Trim() == "" ? null : Fach.Num(x, "Service-km"));
            F("car-p-sdate", "Service bis (TT.MM.JJJJ)", car.ServiceDueDate?.ToString("dd.MM.yyyy") ?? "", x => car.ServiceDueDate = x.Trim() == "" ? null : DateOr(x));
            var ev = new CheckBox { Name = "car-p-ev", Content = "Elektroauto (Laden statt Tanken)", IsChecked = car.Electric }; ev.IsCheckedChanged += (_, _) => { if ((ev.IsChecked == true) != car.Electric) CarRun(() => { car.Electric = ev.IsChecked == true; Cars.Save(); }); };
            sp.Children.Add(ev);
            var d = Fach.StoreDir(_core.DataRoot, "fahrzeug"); var safe = string.Concat(car.Name.Where(char.IsLetterOrDigit));
            sp.Children.Add(new WrapPanel { Children = {
                B("car-export", "Fahrtenbuch (CSV)", () => { var p = Path.Combine(d, $"fahrtenbuch-{safe}.csv"); File.WriteAllText(p, VehicleBook.LogCsv(car)); _carMsg = "Gespeichert: " + p; NotifyFileResult(_carMsg); }),
                B("car-export-fuel", car.Electric ? "Ladebuch (CSV)" : "Tankbuch (CSV)", () => { var p = Path.Combine(d, $"tankbuch-{safe}.csv"); File.WriteAllText(p, VehiclePlus.FuelCsv(car)); _carMsg = "Gespeichert: " + p; NotifyFileResult(_carMsg); }) } });
            var conf = new TextBox { Name = "car-del-confirm", PlaceholderText = $"„{car.Name}“ zum Löschen" };
            sp.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,4,Auto"), Children = { conf, Pos(B("car-del", "Löschen", () => { if (!(conf.Text ?? "").Trim().Equals(car.Name, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Zum Löschen bitte den Namen des Fahrzeugs eintippen."); Cars.Garage.Vehicles.Remove(car); Cars.Save(); CarSel = null; _carMsg = "Fahrzeug gelöscht."; }), 2) } });
        }
        // Rechner (sofort) – mit Werten des gewählten Fahrzeugs vorbelegt
        var s = car is null ? null : VehicleBook.Stats(car); var per100 = (s?.AvgPer100 ?? (car?.Electric == true ? 17 : 6)).ToString("0.#", De);
        var price = (car?.Fuel.OrderBy(x => x.Date).LastOrDefault()?.Price ?? (car?.Electric == true ? 0.39 : 1.79)).ToString("0.###", De);
        var calcs = new StackPanel { Spacing = 6 };
        LiveCalc(calcs, "car-year", "Jahreskosten", [("km", "km im Jahr", "15000", null), ("c", "Verbrauch je 100 km", per100, null), ("p", "Preis je l/kWh (€)", price, null), ("ins", "Versicherung (€/Jahr)", "600", null), ("tax", "Kfz-Steuer (€/Jahr)", "140", null), ("m", "Wartung & Reifen (€/Jahr)", "900", null), ("dep", "Wertverlust (€/Jahr)", "0", null)],
            v => { var y = VehiclePlus.YearCost(Fach.Num(v("km")), Fach.Num(v("c")), Fach.Num(v("p")), Fach.Num(v("ins")), Fach.Num(v("tax")), Fach.Num(v("m")), Fach.Num(v("dep"))); return $"{Fach.F(y.Year)} € im Jahr · {Fach.F(y.Month)} € im Monat · {Fach.F(y.PerKm * 100, 1)} ct je km"; });
        LiveCalc(calcs, "car-share", "Fahrtkosten teilen", [("km", "Strecke (km)", "500", null), ("c", "Verbrauch je 100 km", per100, null), ("p", "Preis je l/kWh (€)", price, null), ("n", "Personen", "4", null), ("x", "Maut/Parken (€)", "0", null)],
            v => { var r = VehiclePlus.Share(Fach.Num(v("km")), Fach.Num(v("c")), Fach.Num(v("p")), (int)Fach.Num(v("n")), Fach.Num(v("x"))); return $"Gesamt {Fach.F(r.Total)} € → {Fach.F(r.PerPerson)} € pro Person"; });
        LiveCalc(calcs, "car-tire", "Reifen prüfen", [("dot", "DOT (Woche+Jahr)", "2319", null), ("mm", "Profil (mm)", "4", null), ("w", "Winterreifen", "nein", ["nein", "ja"])],
            v => { var t = VehiclePlus.Tire(v("dot"), Fach.Num(v("mm")), v("w") == "ja", DateTime.Today); return $"Hergestellt {t.Made:MM/yyyy}, {Fach.F(t.AgeYears, 1)} Jahre alt · " + string.Join(" · ", t.Advice); });
        LiveCalc(calcs, "car-charge", "Ladezeit", [("cap", "Akku (kWh)", "60", null), ("from", "Von (%)", "10", null), ("to", "Bis (%)", "80", null), ("kw", "Ladesäule/Wallbox (kW)", "11", null), ("max", "Auto lädt max. (kW)", "11", null)],
            v => { var c = VehiclePlus.ChargeTime(Fach.Num(v("cap")), Fach.Num(v("from")), Fach.Num(v("to")), Fach.Num(v("kw")), Fach.Num(v("max"))); return $"{Fach.F(c.Kwh, 1)} kWh aus dem Netz · ca. {(int)c.Time.TotalHours}:{c.Time.Minutes:00} h"; });
        if (car is { Electric: true }) LiveCalc(calcs, "car-ev", "E-Auto: Ersparnis", [("ice", "Verbrenner l/100 km", "6", null), ("fp", "Spritpreis (€/l)", "1,79", null)],
            v => { var e = VehiclePlus.Ev(car, Fach.Num(v("ice")), Fach.Num(v("fp"))); return $"{Fach.F(e.KwhPer100, 1)} kWh/100 km · {Fach.F(e.EvCost100)} € je 100 km · Verbrenner {Fach.F(e.IceCost100)} € → {Fach.F(e.Saving100)} € gespart je 100 km"; });
        sp.Children.Add(H("Rechner", 12)); sp.Children.Add(calcs);
        var tax = new StackPanel { Spacing = 6 };
        tax.Children.Add(P($"Steuerwerte geprüft am {LegalRates.Checked} an amtlichen Quellen (docs/rechtswerte.md), ohne Gewähr.", null, 11));
        LiveCalc(tax, "car-commute", "Pendlerpauschale", [("km", "Entfernung (km)", "30", null), ("days", "Arbeitstage", "220", null), ("year", "Steuerjahr", DateTime.Today.Year.ToString(), null)],
            v => $"Entfernungspauschale {Fach.F(VehiclePlus.Commute(Fach.Num(v("km")), (int)Fach.Num(v("days")), (int)Fach.Num(v("year"))))} € im Jahr");
        LiveCalc(tax, "car-company", "Dienstwagen: 1-%-Regel oder Fahrtenbuch?", [("lp", "Bruttolistenpreis (€)", "40000", null), ("kind", "Antrieb", "verbrenner", ["verbrenner", "hybrid", "elektro"]), ("km", "Weg zur Arbeit (km)", "20", null), ("cost", "Gesamtkosten im Jahr (€)", "8000", null), ("priv", "Privatanteil (%)", car is null ? "30" : Fach.F(100 - VehicleBook.TripShare(car).SharePct, 0), null), ("acq", "Angeschafft am", "01.01.2026", null)],
            v => { var c = VehiclePlus.CompanyCar(Fach.Num(v("lp")), v("kind"), Fach.Num(v("km")), Fach.Num(v("cost")), Fach.Num(v("priv")), DateOnly.FromDateTime(DateOr(v("acq")))); return $"1-%-Regel: {Fach.F(c.OnePercent)} € · Fahrtenbuch: {Fach.F(c.Logbook)} € im Jahr → günstiger: {c.Better}"; });
        LiveCalc(tax, "car-mile", "Kilometergeld", [("km", "Dienstliche km", car is null ? "125" : Fach.F(VehicleBook.TripShare(car).Business, 0), null), ("moto", "Fahrzeug", "Auto", ["Auto", "Motorrad/Roller"]), ("brkg", "Öffentlicher Dienst (BRKG)", "nein", ["nein", "ja"]), ("int", "Erhebliches dienstl. Interesse", "nein", ["nein", "ja"])],
            v => $"{Fach.F(VehiclePlus.Mileage(Fach.Num(v("km")), v("moto") != "Auto", brkg: v("brkg") == "ja", significantInterest: v("int") == "ja"))} € Erstattung");
        sp.Children.Add(H("Steuer & Erstattung", 12)); sp.Children.Add(tax);
        return sp;
    }
}
