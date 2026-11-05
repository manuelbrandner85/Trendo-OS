using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Verkaufen › Kasse (Masterprompt 131): Artikel als Kacheln, Warenkorb mit Mengen, bar mit Rückgeld oder Karte, Bon als PDF,
/// Storno nach „Ja“, Tagesabschluss. Gleiche Artikel und gleicher Bestand wie „Online & Artikel“, Einnahmen in „Geld“.</summary>
public partial class MainWindow
{
    public CashRegister Kasse => new(_core.DataRoot, BizIdea.Id);
    string _sellTab = "kasse", _kassePay = "bar", _kasseGiven = "", _kasseFind = "", _kasseDiscount = ""; string? _kasseCancelAsk;
    int _statDays = 30;

    /// <summary>Rabatt-Eingabe lesen: „10 %“ = Prozent, „5“ oder „5 €“ = Betrag.</summary>
    public static (decimal Percent, decimal Euro) ParseDiscount(string text)
    {
        var t = text.Trim(); if (t.Length == 0) return (0, 0);
        var pct = t.EndsWith('%'); var num = t.TrimEnd('%', '€', ' ').Trim();
        if (!decimal.TryParse(num, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.GetCultureInfo("de-DE"), out var v) || v < 0) throw new ArgumentException("Rabatt bitte als „10 %“ oder „5 €“.");
        return pct ? (v, 0) : (0, v);
    }
    readonly List<(string Product, decimal Qty)> _cart = [];
    CashSale? _kasseLast;
    public IReadOnlyList<(string Product, decimal Qty)> KasseCart => _cart;
    public CashSale? KasseLast => _kasseLast;
    /// <summary>Warenkorb füllen (Vorschaubilder, KI).</summary>
    public void KasseRenderCart(IEnumerable<(string Product, decimal Qty)> items, string given = "") { _cart.Clear(); foreach (var (p, q) in items) CartAdd(p, q); _kasseGiven = given; }

    Control SellTabs()
    {
        var bar = new WrapPanel { Name = "sell-tabs" };
        foreach (var (id, label) in new[] { ("kasse", "🧾 Kasse"), ("online", "🛒 Online & Artikel"), ("auswertung", "📊 Auswertung") })
        { var b = Act("sell-tab-" + id, label, () => { _sellTab = id; Show("verkaufen"); }, _sellTab == id); b.Margin = new Thickness(0, 0, 6, 6); bar.Children.Add(b); }
        return bar;
    }

    void CartAdd(string product, decimal qty)
    {
        var i = _cart.FindIndex(c => c.Product == product);
        if (i < 0) { if (qty > 0) _cart.Add((product, qty)); }
        else { var q = _cart[i].Qty + qty; if (q <= 0) _cart.RemoveAt(i); else _cart[i] = (product, q); }
        _kasseLast = null;
    }

    /// <summary>Kassieren (öffentlich für Tests und die KI).</summary>
    public CashSale? KasseCheckout()
    {
        var given = _kassePay == "bar" ? (decimal.TryParse(_kasseGiven.Replace("€", "").Trim(), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.GetCultureInfo("de-DE"), out var g) ? g : 0m) : 0m;
        try
        {
            var (dp, de) = ParseDiscount(_kasseDiscount);
            var s = Kasse.Checkout(_cart, _kassePay, given, DateTime.Now, Money, _settings.SmallBusiness, dp, de);
            _core.Audit.Append(Principal.LocalOwner.Name, "kasse.verkauf", $"{s.No} {s.Gross:0.00}");
            _cart.Clear(); _kasseGiven = ""; _kasseDiscount = ""; _kasseLast = s; BudgetWarn(); return s;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException) { Say(ex.Message, "verkaufen"); return null; }
    }

    string Seller => _settings.Name.Trim().Length > 0 ? string.Join("\n", new[] { BizIdea.Name, _settings.Name, _settings.SenderStreet, _settings.SenderCity }.Where(x => x.Trim().Length > 0)) : BizIdea.Name;

    Control KasseView()
    {
        const string V = "verkaufen";
        var kasse = Kasse; var products = kasse.Shop.Products.All(); var small = _settings.SmallBusiness;
        // Links: Artikel
        var find = new TextBox { Name = "kasse-find", Text = _kasseFind, PlaceholderText = "🔎  Artikel suchen …", Width = 260, HorizontalAlignment = HorizontalAlignment.Left, CornerRadius = new CornerRadius(8) };
        // Scanner oder Artikelnummer: genauer Treffer landet sofort im Korb (wie an jeder Ladenkasse), sonst wird gefiltert
        find.KeyDown += (_, e) => { if (e.Key != Avalonia.Input.Key.Enter) return; var hit = SalesStats.Find(products, find.Text ?? ""); if (hit is not null) { CartAdd(hit.Name, 1); _kasseFind = ""; } else _kasseFind = find.Text ?? ""; Show(V); };
        find.PlaceholderText = "🔎  Artikel suchen oder Strichcode scannen …";
        var tiles = new WrapPanel { Name = "kasse-articles" };
        var shown = products.Where(p => _kasseFind.Length == 0 || p.Name.Contains(_kasseFind, StringComparison.CurrentCultureIgnoreCase)).ToList();
        for (var i = 0; i < shown.Count; i++)
        {
            var p = shown[i]; var inCart = _cart.Where(c => c.Product == p.Name).Sum(c => c.Qty); var free = kasse.Shop.Available(p.Name) - inCart; var name = p.Name;
            var b = new Button { Name = $"kasse-art-{i}", Width = 170, Height = 104, Padding = new Thickness(12, 10), CornerRadius = new CornerRadius(14), IsEnabled = free > 0,
                HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch,
                Background = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative), GradientStops = { new GradientStop(Color.Parse("#FF4A4A4A"), 0), new GradientStop(Color.Parse("#FF303030"), 1) } },
                BorderBrush = Brush.Parse(inCart > 0 ? "#D4A84B" : "#33FFFFFF"), BorderThickness = new Thickness(inCart > 0 ? 2 : 1),
                Content = new Grid { RowDefinitions = new RowDefinitions("*,Auto,Auto"), Children = {
                    new TextBlock { Text = p.Name, Foreground = Brushes.White, FontSize = 14, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap, MaxLines = 2 },
                    Row1(new TextBlock { Text = Eu(CashRegister.GrossPrice(p, small)) + " €", Foreground = GoldText(), FontSize = 18, FontWeight = FontWeight.Bold }, 1),
                    Row1(new TextBlock { Text = free > 0 ? $"{free:0.##} vorrätig" + (inCart > 0 ? $" · {inCart:0.##} im Korb" : "") : "nicht vorrätig", Foreground = Brush.Parse(free > 0 ? "#9FE3BC" : "#F0A08F"), FontSize = 11 }, 2) } } };
            b.Click += (_, _) => { CartAdd(name, 1); Show(V); }; Hover(b);
            tiles.Children.Add(new Border { Margin = new Thickness(0, 0, 10, 10), CornerRadius = new CornerRadius(14), BoxShadow = BoxShadows.Parse("0 6 16 -6 #88000000"), Child = b });
        }
        if (products.Count == 0) tiles.Children.Add(new StackPanel { Spacing = 6, Children = { P("Noch keine Artikel. Lege sie einmal unter „Online & Artikel“ an – sie gelten für Kasse und Shop.", Text, 13),
            Act("kasse-to-articles", "Artikel anlegen →", () => { _sellTab = "online"; Show(V); }, true) } });
        var left = new StackPanel { Spacing = 8, Children = { find, new ScrollViewer { Content = tiles, MaxHeight = 520 } } };

        // Rechts: Warenkorb und Bezahlen
        var cart = new StackPanel { Name = "kasse-cart", Spacing = 4 };
        var total = 0m;
        for (var i = 0; i < _cart.Count; i++)
        {
            var (prod, qty) = _cart[i]; var pr = products.FirstOrDefault(x => x.Name == prod); var price = pr is null ? 0 : CashRegister.GrossPrice(pr, small); var sum = Math.Round(qty * price, 2); total += sum; var pn = prod;
            cart.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto,Auto,90,Auto"), Children = {
                new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { new TextBlock { Text = prod, Foreground = Text, FontSize = 13, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis }, new TextBlock { Text = Eu(price) + " € je Stück", Foreground = Muted, FontSize = 11 } } },
                Pos(Act($"kasse-minus-{i}", "−", () => { CartAdd(pn, -1); Show(V); }), 1),
                Pos(new TextBlock { Name = $"kasse-qty-{i}", Text = qty.ToString("0.##"), Foreground = Text, FontSize = 15, FontWeight = FontWeight.Bold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0), MinWidth = 22, TextAlignment = TextAlignment.Center }, 2),
                Pos(Act($"kasse-plus-{i}", "+", () => { CartAdd(pn, 1); Show(V); }), 3),
                Pos(new TextBlock { Text = Eu(sum) + " €", Foreground = Text, FontSize = 14, FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center }, 4),
                Pos(Act($"kasse-del-{i}", "✕", () => { CartAdd(pn, -1000); Show(V); }), 5) } });
        }
        if (_cart.Count == 0) cart.Children.Add(P(_kasseLast is null ? "Artikel links antippen – sie landen hier." : "Bereit für den nächsten Verkauf.", Muted, 12.5));
        // Rabatt wie bei SumUp/Square: Prozent oder Betrag auf den ganzen Bon
        var discount = 0m; string? discountErr = null;
        try { var (dp, de) = ParseDiscount(_kasseDiscount); discount = -(CashRegister.DiscountLine(total, dp, de)?.Sum ?? 0); }
        catch (ArgumentException ex) { discountErr = ex.Message; }
        total -= discount;
        var pay = new StackPanel { Spacing = 8 };
        if (_cart.Count > 0)
        {
            var disc = new TextBox { Name = "kasse-discount", Text = _kasseDiscount, PlaceholderText = "Rabatt: 10 % oder 5 €", Width = 170 };
            disc.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Enter) { _kasseDiscount = disc.Text ?? ""; Show(V); } };
            disc.LostFocus += (_, _) => { if ((disc.Text ?? "") != _kasseDiscount) { _kasseDiscount = disc.Text ?? ""; Show(V); } };
            pay.Children.Add(new WrapPanel { Children = { disc, new TextBlock { Name = "kasse-discount-info", Text = discountErr ?? (discount > 0 ? $"  − {Eu(discount)} €" : ""), Foreground = discountErr is null ? GoldText() : Brush.Parse("#F87171"), FontSize = 13, VerticalAlignment = VerticalAlignment.Center } } });
        }
        pay.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { new TextBlock { Text = "Summe", Foreground = Text, FontSize = 18, VerticalAlignment = VerticalAlignment.Center },
            Pos(new TextBlock { Name = "kasse-total", Text = Eu(total) + " €", Foreground = GoldText(), FontSize = 30, FontWeight = FontWeight.Bold }, 1) } });
        if (!small && total > 0) pay.Children.Add(new TextBlock { Text = $"enthält {Eu(Math.Round(total - total / (1 + LegalRates.VatStandard / 100m), 2))} € MwSt ({LegalRates.VatStandard:0} %)", Foreground = Muted, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Right });
        var kinds = new WrapPanel();
        foreach (var (id, label) in new[] { ("bar", "💶 Bar"), ("karte", "💳 Karte") }) { var b = Act("kasse-pay-" + id, label, () => { _kassePay = id; Show(V); }, _kassePay == id); b.Margin = new Thickness(0, 0, 6, 0); kinds.Children.Add(b); }
        pay.Children.Add(kinds);
        if (_kassePay == "bar")
        {
            var given = new TextBox { Name = "kasse-given", Text = _kasseGiven, PlaceholderText = "gegeben €", Width = 120, FontSize = 16 };
            var change = new TextBlock { Name = "kasse-change", Foreground = Text, FontSize = 16, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
            void Upd() { var ok = decimal.TryParse((given.Text ?? "").Replace("€", "").Trim(), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.GetCultureInfo("de-DE"), out var gv); _kasseGiven = given.Text ?? ""; change.Text = !ok || total == 0 ? "" : gv >= total ? $"Rückgeld {Eu(gv - total)} €" : $"Es fehlen {Eu(total - gv)} €"; change.Foreground = Brush.Parse(ok && gv < total ? "#F0644B" : "#2FBF71"); }
            given.TextChanged += (_, _) => Upd(); Upd();
            var quick = new WrapPanel();
            foreach (var v in new[] { total, 5m, 10m, 20m, 50m, 100m }.Where(v => v >= total && total > 0).Distinct().Take(5))
            { var vv = v; var q = Act($"kasse-quick-{(vv == total ? "passend" : vv.ToString("0"))}", vv == total ? "passend" : Eu(vv).Replace(",00", "") + " €", () => { given.Text = vv.ToString("0.00", System.Globalization.CultureInfo.GetCultureInfo("de-DE")); }); q.Margin = new Thickness(0, 0, 6, 6); quick.Children.Add(q); }
            pay.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Children = { given, change } });
            pay.Children.Add(quick);
        }
        var go = Act("kasse-checkout", "Kassieren", () => { if (KasseCheckout() is not null) Show(V); }, true); go.IsEnabled = _cart.Count > 0; go.FontSize = 16; go.Padding = new Thickness(18, 8); go.HorizontalAlignment = HorizontalAlignment.Stretch; go.HorizontalContentAlignment = HorizontalAlignment.Center;
        pay.Children.Add(go);
        if (_cart.Count > 0) pay.Children.Add(Act("kasse-clear", "Warenkorb leeren", () => { _cart.Clear(); Show(V); }));
        if (_kasseLast is { } last)
            pay.Children.Add(new Border { Name = "kasse-done", Background = Brush.Parse("#332FBF71"), BorderBrush = Brush.Parse("#2FBF71"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(12), Child = new StackPanel { Spacing = 6, Children = {
                new TextBlock { Text = $"✓ {last.No} · {Eu(last.Gross)} € {(last.Payment == "bar" ? "bar" : "mit Karte")}", Foreground = Text, FontWeight = FontWeight.SemiBold },
                last.Payment == "bar" ? new TextBlock { Name = "kasse-done-change", Text = $"Rückgeld: {Eu(last.Change)} €", Foreground = GoldText(), FontSize = 22, FontWeight = FontWeight.Bold } : new TextBlock { Text = "Bezahlt.", Foreground = Muted },
                Act("kasse-receipt", "Bon als PDF", () => Try(V, () => { var f = Path.Combine(BusinessFolder, "Kasse", last.No + ".pdf"); Directory.CreateDirectory(Path.GetDirectoryName(f)!); CashRegister.ReceiptPdf(f, last, Seller); NotifyFileResult("Gespeichert: " + f); Say("Bon gespeichert: " + Path.GetFileName(f), V); })) } } });
        var right = Glass2(new StackPanel { Spacing = 10, Width = 400, Children = { H("Warenkorb", 15), new ScrollViewer { Content = cart, MaxHeight = 300 }, pay } }, 14);

        // Unten: heute
        var today = DateOnly.FromDateTime(DateTime.Today); var day = kasse.Day(today); var sales = kasse.Of(today);
        var list = new StackPanel { Name = "kasse-today", Spacing = 2 };
        for (var i = 0; i < sales.Count && i < 60; i++)
        {
            var s = sales[i]; var id = s.Id;
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("60,170,*,Auto,Auto"), Opacity = s.Cancelled ? 0.5 : 1, Children = {
                new TextBlock { Text = s.At.ToString("HH:mm"), Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center },
                Pos(new TextBlock { Text = s.No, Foreground = Text, FontSize = 12, VerticalAlignment = VerticalAlignment.Center }, 1),
                Pos(new TextBlock { Text = string.Join(", ", s.Lines.Select(l => $"{l.Qty:0.##}× {l.Product}")) + (s.Cancelled ? " · storniert" : ""), Foreground = Muted, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center }, 2),
                Pos(new TextBlock { Text = $"{Eu(s.Gross)} € {(s.Payment == "bar" ? "bar" : "Karte")}", Foreground = Text, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0) }, 3) } };
            if (!s.Cancelled)
            {
                if (_kasseCancelAsk != id) row.Children.Add(Pos(Act($"kasse-storno-{i}", "Storno …", () => { _kasseCancelAsk = id; Show(V); }), 4));
                else row.Children.Add(Pos(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = {
                    new TextBlock { Text = "Wirklich stornieren?", Foreground = Brush.Parse("#F5A524"), VerticalAlignment = VerticalAlignment.Center },
                    Act($"kasse-storno-yes-{i}", "Ja", () => Try(V, () => { Kasse.Cancel(id, DateTime.Now, Money); _kasseCancelAsk = null; _core.Audit.Append(Principal.LocalOwner.Name, "kasse.storno", s.No); Say($"{s.No} storniert – Bestand und Geld sind zurückgebucht.", V); }), true),
                    Act($"kasse-storno-no-{i}", "Nein", () => { _kasseCancelAsk = null; Show(V); }) } }, 4));
            }
            list.Children.Add(row);
        }
        if (sales.Count == 0) list.Children.Add(P("Heute noch keine Verkäufe.", Muted, 12));
        var close = Act("kasse-day-pdf", "Tagesabschluss als PDF", () => Try(V, () => { var f = Path.Combine(BusinessFolder, "Kasse", $"Tagesabschluss-{today:yyyy-MM-dd}.pdf"); Directory.CreateDirectory(Path.GetDirectoryName(f)!); CashRegister.DayPdf(f, Kasse.Day(today), Kasse.Of(today), Seller); NotifyFileResult("Gespeichert: " + f); Say("Tagesabschluss gespeichert: " + Path.GetFileName(f), V); }));
        var dayCard = Card(new StackPanel { Spacing = 6, Children = {
            new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { H($"Heute, {DateTime.Today:dddd dd.MM.}", 14), Pos(close, 1) } },
            new WrapPanel { Name = "kasse-day", Children = {
                Kpi("kasse-day-gross", "Umsatz heute", Eu(day.Gross) + " €", $"{day.Count} Bons" + (day.Cancelled > 0 ? $" · {day.Cancelled} storniert" : ""), "#2FBF71", "#167A45"),
                Kpi("kasse-day-cash", "Bar", Eu(day.Cash) + " €", "in der Kasse", "#F5A524", "#C46A0A"),
                Kpi("kasse-day-card", "Karte", Eu(day.Card) + " €", "aufs Konto", "#3D8BFF", "#1E4FD8") } },
            day.ByProduct.Count > 0 ? new TextBlock { Text = "Meistverkauft: " + string.Join(" · ", day.ByProduct.Take(5).Select(x => $"{x.Product} {x.Qty:0.##}×")), Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap } : new Border(),
            list } });
        return new StackPanel { Spacing = 10, Children = {
            new Grid { ColumnDefinitions = new ColumnDefinitions("*,14,Auto"), Children = { left, Pos(right, 2) } },
            dayCard,
            P("Die Kasse ist ein einfaches Kassenbuch für kleine Verkäufe. Sie ersetzt keine zertifizierte Registrierkasse mit TSE.", Muted, 11) } };
    }

    static Control Row1(Control c, int row) { Grid.SetRow(c, row); return c; }
}
