using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Online verkaufen (STUDIO-ONLINE-BUSINESS-01), Teil 4: U1 Übersicht mit Kennzahlen und Umsatzdiagramm, F1 Kunden und Artikel
/// (dieselben Listen wie „Betrieb“), F2 Rechnung aus Bestellung, F3 Webshop-Vorschau.</summary>
public partial class MainWindow
{
    public Shop BizShop => new(_core.DataRoot, BizIdea.Id);
    private static readonly string[] MonthShort = ["Jan", "Feb", "Mär", "Apr", "Mai", "Jun", "Jul", "Aug", "Sep", "Okt", "Nov", "Dez"];

    /// <summary>Kennzahl-Karte mit Verlauf, Lichtkante und Schatten.</summary>
    private Border Kpi(string name, string title, string value, string sub, string c1, string c2)
    {
        var v = new TextBlock { Name = name, Text = value, Foreground = Brushes.White, FontSize = 24, FontWeight = FontWeight.Bold };
        return new Border
        {
            Margin = new Thickness(0, 0, 10, 10), MinWidth = 190, Padding = new Thickness(16, 12), CornerRadius = new CornerRadius(16),
            Background = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative), GradientStops = { new GradientStop(Color.Parse(c1), 0), new GradientStop(Color.Parse(c2), 1) } },
            BorderBrush = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative), GradientStops = { new GradientStop(Color.Parse("#66FFFFFF"), 0), new GradientStop(Color.Parse("#00FFFFFF"), 1) } },
            BorderThickness = new Thickness(1), BoxShadow = BoxShadows.Parse("0 10 24 -6 #66000000"),
            Child = new StackPanel { Spacing = 2, Children = { new TextBlock { Text = title, Foreground = Brush.Parse("#E6FFFFFF"), FontSize = 12, FontWeight = FontWeight.SemiBold }, v, new TextBlock { Text = sub, Foreground = Brush.Parse("#CCFFFFFF"), FontSize = 11 } } },
        };
    }

    private Control SellView()
    {
        const string V = "verkaufen";
        var shop = BizShop; var today = DateOnly.FromDateTime(DateTime.Today);
        var (month, year, customers, open) = shop.Figures(today);
        var y = Finance.Year(Money.All(), today.Year);
        var sp = new StackPanel { Spacing = 10, Children = { IdeaHeader(V) } };
        sp.Children.Add(new WrapPanel { Name = "sell-kpis", Children = {
            Kpi("sell-kpi-month", "Umsatz " + MonthShort[today.Month - 1], Eu(month) + " €", "netto, aus Bestellungen", "#3D8BFF", "#1E4FD8"),
            Kpi("sell-kpi-year", "Umsatz " + today.Year, Eu(year) + " €", "netto", "#7C4DFF", "#4B2BC2"),
            Kpi("sell-kpi-profit", "Gewinn " + today.Year, Eu(y.IncomeNet + y.ExpenseNet) + " €", "aus „Geld“, netto", y.IncomeNet + y.ExpenseNet >= 0 ? "#2FBF71" : "#F0644B", y.IncomeNet + y.ExpenseNet >= 0 ? "#167A45" : "#B2361F"),
            Kpi("sell-kpi-customers", "Kunden", customers.ToString(), $"{open} Bestellung(en) offen", "#F5A524", "#C46A0A") } });
        var chart = new BarChart { Name = "sell-chart", Height = 200, Labels = MonthShort, Foreground = Muted };
        chart.Series.Add(("Umsatz", "#3D8BFF", shop.MonthRevenue(today.Year).Select(d => (double)d).ToArray()));
        sp.Children.Add(Card(new StackPanel { Spacing = 6, Children = { H($"Umsatz je Monat {today.Year}", 14), chart } }));

        // Bestellungen
        var contacts = shop.Customers.All(); var products = shop.Products.All();
        var cust = new ComboBox { Name = "sell-order-customer", ItemsSource = contacts.Select(c => c.Name).ToList(), PlaceholderText = "Kunde", MinWidth = 180 };
        var prod = new ComboBox { Name = "sell-order-product", ItemsSource = products.Select(p => p.Name).ToList(), PlaceholderText = "Artikel", MinWidth = 180 };
        var qty = Field("sell-order-qty", "Menge", 70, "1");
        var orders = new StackPanel { Name = "sell-orders", Spacing = 4 };
        var i = 0;
        foreach (var o in shop.Orders().Take(20))
        {
            var oid = o.Id;
            var right = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = {
                new TextBlock { Text = Eu(o.Net) + " €", Foreground = Text, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center },
                new Border { CornerRadius = new CornerRadius(10), Padding = new Thickness(8, 2), VerticalAlignment = VerticalAlignment.Center, Background = Brush.Parse(o.Status switch { "offen" => "#33F5A524", "bezahlt" => "#332FBF71", _ => "#333D8BFF" }),
                    Child = new TextBlock { Text = o.Status, Foreground = Text, FontSize = 11 } } } };
            if (o.InvoiceNumber.Length == 0) right.Children.Add(Act($"sell-invoice-{i}", "Rechnung", () => Try(V, () =>
            {
                var (inv, file) = shop.Invoice(oid, Money, _settings.Name.Trim().Length > 0 ? _settings.Name + "\n" + _settings.SenderStreet + "\n" + _settings.SenderCity : "Dein Name und Adresse", _settings.SmallBusiness, _settings.InvoiceLogo);
                _core.Audit.Append(Principal.LocalOwner.Name, "shop.invoice", inv.Number); Launch(file); Say($"Rechnung {inv.Number} erstellt und unter „Geld“ als Einnahme gebucht.", V);
            }), true));
            else right.Children.Add(P(o.InvoiceNumber, null, 11));
            if (o.Status != "bezahlt") right.Children.Add(Act($"sell-paid-{i}", "Bezahlt", () => { shop.SetStatus(oid, "bezahlt"); Show(V); }));
            orders.Children.Add(Card(Row(new StackPanel { Children = { new TextBlock { Text = o.Customer, Foreground = Text, FontWeight = FontWeight.SemiBold },
                P($"{o.Date:dd.MM.yyyy} · {string.Join(", ", o.Lines.Select(l => $"{l.Qty:0.##} × {l.Product}"))}", null, 11) } }, right)));
            i++;
        }
        if (i == 0) orders.Children.Add(P("Noch keine Bestellungen.", Text));
        sp.Children.Add(Card(Section("Bestellungen", "Bestellung eintragen, dann mit einem Klick die Rechnung schreiben – sie wird automatisch als Einnahme gebucht.",
            Line(cust, prod, qty, Act("sell-order-add", "Bestellung eintragen", () => Try(V, () =>
            {
                if (cust.SelectedItem is not string c) throw new ArgumentException("Bitte einen Kunden wählen (oder unten anlegen).");
                if (prod.SelectedItem is not string p) throw new ArgumentException("Bitte einen Artikel wählen (oder unten anlegen).");
                shop.AddOrder(c, DateOnly.FromDateTime(DateTime.Today), (p, Finance.ParseAmount(Need(qty, "die Menge")))); Say("Bestellung eingetragen.", V);
            }), true)), orders)));

        // Kunden und Artikel (gemeinsam mit „Betrieb“)
        var cn = Field("sell-cust-name", "Name", 160); var ca = Field("sell-cust-address", "Straße, PLZ Ort", 220); var ce = Field("sell-cust-email", "E-Mail (optional)", 180);
        var custList = new WrapPanel();
        foreach (var c in contacts.Take(30)) custList.Children.Add(new Border { Margin = new Thickness(0, 0, 6, 6), Padding = new Thickness(10, 6), CornerRadius = new CornerRadius(10), Background = TileBg, BorderBrush = Edge, BorderThickness = new Thickness(1),
            Child = new StackPanel { Children = { new TextBlock { Text = c.Name, Foreground = Text, FontSize = 12.5 }, P($"{c.Stage}{(c.Address.Length > 0 ? " · " + c.Address : "")}", null, 10.5) } } });
        var pn = Field("sell-prod-name", "Artikel", 180); var pp = Field("sell-prod-price", "Preis netto €", 110); var ps = Field("sell-prod-stock", "Bestand", 80, "0");
        sp.Children.Add(Card(Section("Kunden", "Dieselbe Liste wie unter „Betrieb“.",
            Line(cn, ca, ce, Act("sell-cust-add", "Kunde anlegen", () => Try(V, () =>
            {
                var name = Need(cn, "den Namen"); if (contacts.Any(x => x.Name == name)) throw new ArgumentException("Diesen Kunden gibt es schon.");
                shop.Customers.Add(new BizContact(name, "", ce.Text?.Trim() ?? "", "Kunde", null, 0, "", ca.Text?.Trim() ?? "")); Say($"Kunde „{name}“ angelegt.", V);
            }))), custList)));
        var shopGrid = new WrapPanel { Name = "sell-shop" };
        foreach (var p in products)
            shopGrid.Children.Add(new Border { Width = 190, Margin = new Thickness(0, 0, 10, 10), Padding = new Thickness(12), CornerRadius = new CornerRadius(14), Background = Brushes.White, BoxShadow = BoxShadows.Parse("0 6 16 -4 #55000000"),
                Child = new StackPanel { Spacing = 4, Children = {
                    new TextBlock { Text = p.Name, Foreground = Brush.Parse("#1F2933"), FontSize = 14, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap },
                    new TextBlock { Text = Eu(_settings.SmallBusiness ? (decimal)p.Price : Math.Round((decimal)p.Price * (1 + LegalRates.VatStandard / 100m), 2)) + " €", Foreground = Brush.Parse("#1E4FD8"), FontSize = 18, FontWeight = FontWeight.Bold },
                    new TextBlock { Text = p.Stock > 0 ? $"{p.Stock} vorrätig" : "nicht vorrätig", Foreground = Brush.Parse(p.Stock > 0 ? "#167A45" : "#B2361F"), FontSize = 11 } } } });
        if (products.Count == 0) shopGrid.Children.Add(P("Noch keine Artikel.", Text));
        sp.Children.Add(Card(Section("Artikel und Webshop-Vorschau", "So sähe dein Shop aus. „Als Webseite speichern“ legt eine HTML-Seite an – ohne Bezahlfunktion und ohne fremde Dienste.",
            Line(pn, pp, ps, Act("sell-prod-add", "Artikel anlegen", () => Try(V, () =>
            {
                var name = Need(pn, "den Artikelnamen"); if (products.Any(x => x.Name == name)) throw new ArgumentException("Diesen Artikel gibt es schon.");
                var price = (double)Finance.ParseAmount(Need(pp, "den Preis")); if (price < 0) throw new ArgumentException("Der Preis darf nicht negativ sein.");
                shop.Products.Add(new Product(name, "", 0, price, int.TryParse(ps.Text, out var st) ? Math.Max(0, st) : 0, 0, 0)); Say($"Artikel „{name}“ angelegt.", V);
            })), Act("sell-shop-save", "Als Webseite speichern", () => Try(V, () =>
            {
                var f = Path.Combine(BusinessFolder, DevStudio.SafeName(BizIdea.Name) + "-Shop.html");
                File.WriteAllText(f, Shop.ShopHtml(BizIdea.Name, shop.Products.All(), _settings.SmallBusiness)); Launch(f); Say("Gespeichert: " + f, V);
            }))), shopGrid)));
        return BusinessPage("Online verkaufen", sp);
    }
}
