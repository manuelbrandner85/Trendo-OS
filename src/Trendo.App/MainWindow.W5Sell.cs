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
    private string? _sellPayFor; // Bestellung, für die gerade eine Zahlung erfasst wird
    private string? _sellRefundFor; // Bestellung, für die gerade eine Erstattung erfasst wird
    private static readonly string[] MonthShort = ["Jan", "Feb", "Mär", "Apr", "Mai", "Jun", "Jul", "Aug", "Sep", "Okt", "Nov", "Dez"];

    /// <summary>Kennzahl-Karte. V-17 (T-0700): dunkle Fläche wie alle Karten, Zahl in Gold statt bunter Verlaufskacheln; nur eine Warnung (rot übergeben) bleibt rot.</summary>
    private Border Kpi(string name, string title, string value, string sub, string c1, string c2)
    {
        var warn = KpiWarn(c1);
        var v = new TextBlock { Name = name, Text = value, Foreground = warn ? Brush.Parse("#F0644B") : Accent, FontSize = 24, FontWeight = FontWeight.Bold };
        return new Border
        {
            Name = name + "-kachel", Classes = { "kennzahl" },
            Margin = new Thickness(0, 0, 10, 10), MinWidth = 190, Padding = new Thickness(16, 12), CornerRadius = new CornerRadius(12),
            Background = TileBg, BorderBrush = Edge, BorderThickness = new Thickness(1),
            Child = new StackPanel { Spacing = 2, Children = { new TextBlock { Text = title, Foreground = Muted, FontSize = 12, FontWeight = FontWeight.SemiBold }, v, new TextBlock { Text = sub, Foreground = Muted, FontSize = 11 } } },
        };
    }

    /// <summary>Rote Töne der bisherigen Kacheln bedeuten „Achtung“ (Minus, Überzug) – nur sie behalten eine eigene Farbe.</summary>
    internal static bool KpiWarn(string color) => Color.TryParse(color, out var c) && c.R > 0xC0 && c.G < 0x80 && c.B < 0x70;

    /// <summary>Eingaben der Verkaufen-Formulare bleiben nach einer Fehlermeldung stehen (vorher war nach jeder Meldung alles leer);
    /// nach erfolgreichem Anlegen werden sie wie gewohnt geleert.</summary>
    readonly Dictionary<string, string> _sellDrafts = [];
    TextBox SellField(string name, string hint, double w = 340, string? text = null)
    {
        var t = Field(name, hint, w, _sellDrafts.TryGetValue(name, out var d) ? d : text);
        t.TextChanged += (_, _) => { if (Avalonia.VisualTree.VisualExtensions.IsAttachedToVisualTree(t)) _sellDrafts[name] = t.Text ?? ""; }; // T-0753: abgelöste alte Felder schreiben keinen Entwurf zurück
        return t;
    }
    void SellTry(string view, Action a) => Try(view, () => { a(); if (_sellDrafts.Count > 0) { _sellDrafts.Clear(); Show(view); } }); // erst leeren, dann neu zeichnen (die Meldung bleibt)

    private Control SellView()
    {
        const string V = "verkaufen";
        if (_sellTab == "kasse") return SellPage("Verkaufen", new StackPanel { Spacing = 10, Children = { IdeaHeader(V), SellTabs(), KasseView() } });
        if (_sellTab == "auswertung") return SellPage("Verkaufen", new StackPanel { Spacing = 10, Children = { IdeaHeader(V), SellTabs(), SalesStatsView() } });
        var shop = BizShop; var today = DateOnly.FromDateTime(DateTime.Today);
        var (month, year, customers, open) = shop.Figures(today);
        var y = Finance.Year(Money.All(), today.Year);
        var sp = new StackPanel { Spacing = 10, Children = { IdeaHeader(V), SellTabs() } };
        if (SellUndoBar() is { } undo) sp.Children.Add(undo);
        SellKeys(sp);
        sp.Children.Add(new WrapPanel { Name = "sell-kpis", Children = {
            Kpi("sell-kpi-month", "Umsatz " + MonthShort[today.Month - 1], Eu(month) + " €", "netto, nach Zahlungseingang", "#3D8BFF", "#1E4FD8"),
            Kpi("sell-kpi-year", "Umsatz " + today.Year, Eu(year) + " €", "netto", "#7C4DFF", "#4B2BC2"),
            Kpi("sell-kpi-profit", "Gewinn " + today.Year, Eu(y.IncomeNet + y.ExpenseNet) + " €", "aus „Geld“, netto", y.IncomeNet + y.ExpenseNet >= 0 ? "#2FBF71" : "#F0644B", y.IncomeNet + y.ExpenseNet >= 0 ? "#167A45" : "#B2361F"),
            Kpi("sell-kpi-customers", "Kunden", customers.ToString(), $"{open} Bestellung(en) offen", "#F5A524", "#C46A0A"),
            Kpi("sell-kpi-open", "Offene Rechnungen", Eu(shop.OpenReceivables()) + " €", "brutto, noch nicht bezahlt", "#5BC0EB", "#1D7FA8") } });
        var chart = new BarChart { Name = "sell-chart", Height = 200, Labels = MonthShort, Foreground = Muted };
        chart.Series.Add(("Umsatz", "#3D8BFF", shop.MonthRevenue(today.Year).Select(d => (double)d).ToArray()));
        sp.Children.Add(Card(new StackPanel { Spacing = 6, Children = { H($"Umsatz je Monat {today.Year}", 14), chart } }));

        // Bestellungen
        var contacts = shop.Customers.All(); var products = shop.Products.All();
        var cust = new ComboBox { Name = "sell-order-customer", ItemsSource = contacts.Select(c => c.Name).ToList(), PlaceholderText = "Kunde", MinWidth = 180 };
        var prod = new ComboBox { Name = "sell-order-product", ItemsSource = shop.OrderableItems(), PlaceholderText = "Artikel", MinWidth = 180 }; // T-0753: Varianten einzeln
        var qty = SellField("sell-order-qty", "Menge", 70, "1");
        var orders = new StackPanel { Name = "sell-orders", Spacing = 4 };
        var i = 0;
        foreach (var o in shop.Orders().Take(20))
        {
            var oid = o.Id;
            var right = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = {
                new TextBlock { Text = Eu(o.Net) + " €", Foreground = Text, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center },
                new Border { CornerRadius = new CornerRadius(10), Padding = new Thickness(8, 2), VerticalAlignment = VerticalAlignment.Center, Background = Brush.Parse(o.Status switch { "offen" => "#33F5A524", "bezahlt" => "#332FBF71", "storniert" => "#33F0644B", _ => "#333D8BFF" }),
                    Child = new TextBlock { Text = o.Status, Foreground = Text, FontSize = 11 } } } };
            var live = o.Status != "storniert";
            // 105 (Entscheidung 31.10.2026): Bestand sinkt erst bei „Geliefert“
            if (live && o.StockOut == false) right.Children.Add(Act($"sell-deliver-{i}", "Geliefert", () => SellTry(V, () => { shop.Deliver(oid, today); _core.Audit.Append(Principal.LocalOwner.Name, "shop.deliver", oid); Say("Als geliefert eingetragen – der Bestand ist gesunken.", V); })));
            if (live && o.InvoiceNumber.Length == 0) right.Children.Add(Act($"sell-invoice-{i}", "Rechnung", () => SellTry(V, () =>
            {
                var (inv, file) = shop.Invoice(oid, Money, _settings.Name.Trim().Length > 0 ? _settings.Name + "\n" + _settings.SenderStreet + "\n" + _settings.SenderCity : "Dein Name und Adresse", _settings.SmallBusiness, _settings.InvoiceLogo);
                _core.Audit.Append(Principal.LocalOwner.Name, "shop.invoice", inv.Number); Launch(file); Say($"Rechnung {inv.Number} erstellt. Die Einnahme wird gebucht, sobald du die Zahlung erfasst.", V);
            }), true));
            else if (o.InvoiceNumber.Length > 0) right.Children.Add(P(o.InvoiceNumber + (o.Open > 0 ? $" · offen {Eu(o.Open)} €" : ""), null, 11));
            // Umsatz zählt erst bei Zahlung (105-05): Betrag und Referenz, gleiche Referenz bucht nichts doppelt
            if (live && o.Payments is not null && o.InvoiceNumber.Length > 0 && o.Open > 0)
            {
                if (_sellPayFor == oid)
                {
                    var amt = SellField("sell-pay-amount", "Betrag €", 90, o.Open.ToString("0.00", System.Globalization.CultureInfo.GetCultureInfo("de-DE")));
                    var refBox = SellField("sell-pay-ref", "Referenz (Kontoauszug)", 170, $"{o.InvoiceNumber}-{o.Payments.Count + 1}");
                    right.Children.Add(amt); right.Children.Add(refBox);
                    right.Children.Add(Act($"sell-pay-book-{i}", "Buchen", () => SellTry(V, () =>
                    {
                        var after = shop.Pay(oid, Finance.ParseAmount(Need(amt, "den Betrag")), today, Need(refBox, "die Referenz"), Money, _settings.SmallBusiness);
                        _sellPayFor = null; _core.Audit.Append(Principal.LocalOwner.Name, "shop.payment", $"{o.InvoiceNumber} {after.Paid:0.00}");
                        Say(after.Open > 0 ? $"Zahlung gebucht – noch offen {Eu(after.Open)} €." : "Zahlung gebucht – Rechnung vollständig bezahlt.", V);
                    }), true));
                }
                else right.Children.Add(Act($"sell-pay-{i}", "Zahlung erfassen", () => { _sellPayFor = oid; Show(V); }));
            }
            // T-0252: Erstatten (höchstens das Gezahlte), danach ist ein Storno möglich
            if (live && o.Payments is not null && o.Paid > 0)
            {
                if (_sellRefundFor == oid)
                {
                    var amt = SellField("sell-refund-amount", "Betrag €", 90, o.Paid.ToString("0.00", System.Globalization.CultureInfo.GetCultureInfo("de-DE")));
                    var refBox = SellField("sell-refund-ref", "Referenz", 150, $"{o.InvoiceNumber}-E{o.Payments.Count(p => p.Amount < 0) + 1}");
                    right.Children.Add(amt); right.Children.Add(refBox);
                    right.Children.Add(Act($"sell-refund-book-{i}", "Erstattung buchen", () => SellTry(V, () =>
                    {
                        var after = shop.Refund(oid, Finance.ParseAmount(Need(amt, "den Betrag")), today, Need(refBox, "die Referenz"), Money, _settings.SmallBusiness);
                        _sellRefundFor = null; _core.Audit.Append(Principal.LocalOwner.Name, "shop.refund", $"{o.InvoiceNumber} {after.Paid:0.00}");
                        Say(after.Paid > 0 ? $"Erstattung gebucht – noch {Eu(after.Paid)} € bezahlt." : "Erstattung gebucht – die Bestellung kann jetzt storniert werden.", V);
                    })));
                }
                else right.Children.Add(Act($"sell-refund-{i}", "Erstatten", () => { _sellRefundFor = oid; Show(V); }));
            }
            if (o.Payments is null && o.Status != "bezahlt") right.Children.Add(Act($"sell-paid-{i}", "Bezahlt", () => { shop.SetStatus(oid, "bezahlt"); Show(V); })); // Altdaten
            // T-0753: offene Bestellung (nicht geliefert, ohne Rechnung und Zahlung) lässt sich ändern
            var editable = o.Status == "offen" && o.StockOut == false && o.InvoiceNumber.Length == 0 && o.Payments is { Count: 0 } && o.Lines.Count == 1;
            if (editable && _sellEditOrder != oid) right.Children.Add(Act($"sell-order-edit-{i}", "Ändern", () => { _sellEditOrder = oid; Show(V); }));
            if (live && o.Paid == 0 && o.Status != "bezahlt") right.Children.Add(Act($"sell-cancel-{i}", "Stornieren", () => SellTry(V, () => { shop.Cancel(oid, today); _core.Audit.Append(Principal.LocalOwner.Name, "shop.cancel", oid); Say("Bestellung storniert.", V); })));
            var orderCard = Card(Row(new StackPanel { Children = { new TextBlock { Text = o.Customer, Foreground = Text, FontWeight = FontWeight.SemiBold },
                P($"{o.Date:dd.MM.yyyy} · {string.Join(", ", o.Lines.Select(l => $"{l.Qty:0.##} × {l.Product}"))}", null, 11) } }, right));
            if (editable && _sellEditOrder == oid)
            {
                var ec = new ComboBox { Name = "sell-oe-customer", ItemsSource = contacts.Select(c => c.Name).ToList(), SelectedItem = o.Customer, MinWidth = 180 };
                var ep = new ComboBox { Name = "sell-oe-product", ItemsSource = shop.OrderableItems(), SelectedItem = o.Lines[0].Product, MinWidth = 180 };
                var eq = Field("sell-oe-qty", "Menge", 70, o.Lines[0].Qty.ToString("0.##", System.Globalization.CultureInfo.GetCultureInfo("de-DE")));
                orderCard = Card(new StackPanel { Spacing = 4, Children = { H("Bestellung ändern", 13), Line(ec, ep, eq,
                    Act("sell-oe-save", "Speichern", () => SellTry(V, () =>
                    {
                        if (ec.SelectedItem is not string c) throw new ArgumentException("Bitte einen Kunden wählen.");
                        if (ep.SelectedItem is not string p) throw new ArgumentException("Bitte einen Artikel wählen.");
                        shop.UpdateOrder(oid, c, (p, Finance.ParseAmount(Need(eq, "die Menge")))); _sellEditOrder = null; Say("Bestellung geändert.", V);
                    })), Act("sell-oe-cancel", "Abbrechen", () => { _sellEditOrder = null; Show(V); })) } });
            }
            if (editable) { var eid = oid; orderCard.ContextMenu = SellMenu(($"sell-order-menu-edit-{i}", "Ändern", () => { _sellEditOrder = eid; Show(V); }), ($"sell-order-menu-cancel-{i}", "Stornieren", () => SellTry(V, () => { shop.Cancel(eid, today); Say("Bestellung storniert.", V); }))); }
            orders.Children.Add(orderCard);
            i++;
        }
        if (i == 0) orders.Children.Add(P("Noch keine Bestellungen.", Text));
        sp.Children.Add(Card(Section("Bestellungen", "Bestellung eintragen (die Ware wird vorgemerkt), bei Versand „Geliefert“ (der Bestand sinkt), Rechnung schreiben und bei Eingang die Zahlung erfassen – erst dann zählt sie als Einnahme.",
            Line(cust, prod, qty, Act("sell-order-add", "Bestellung eintragen", () => SellTry(V, () =>
            {
                if (cust.SelectedItem is not string c) throw new ArgumentException("Bitte einen Kunden wählen (oder unten anlegen).");
                if (prod.SelectedItem is not string p) throw new ArgumentException("Bitte einen Artikel wählen (oder unten anlegen).");
                shop.PlaceOrder(c, DateOnly.FromDateTime(DateTime.Today), (p, Finance.ParseAmount(Need(qty, "die Menge")))); Say("Bestellung eingetragen.", V);
            }), true)), orders)));

        // Kunden und Artikel (gemeinsam mit „Betrieb“) – T-0753: ändern, löschen, Bilder, Varianten, CSV
        sp.Children.Add(SellCustomersCard(shop, contacts));
        sp.Children.Add(SellProductsCard(shop, products));
        sp.Children.Add(SellCsvCard());
        sp.Children.Add(Card(Section("Print on Demand im Shop", "Produkte aus Printful, Bezahlung über dein eigenes Stripe-Konto. Bestellungen werden zuerst Entwürfe, Kosten entstehen erst nach deiner Freigabe.", PodShopCard())));
        return SellPage("Online verkaufen", sp);
    }
}
