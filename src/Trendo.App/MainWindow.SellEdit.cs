using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Trendo.Core;

namespace Trendo.App;

/// <summary>T-0753 (STUDIO-ONLINE-BUSINESS-01): Arbeitsplatz für Artikel, Kunden und Bestellungen in „Online verkaufen“ – ändern, löschen,
/// Bild, Varianten, CSV im Shopify-Format ein/aus, Rückgängig (Knopf und Strg+Z), Rechtsklick und Ziehen und Ablegen.
/// Alles wird sofort und atomar gespeichert (<see cref="AtomicFile"/> über <see cref="IdeaList{T}"/>).</summary>
public partial class MainWindow
{
    const string SellV = "verkaufen";
    string? _sellEditProd, _sellEditCust, _sellEditOrder;
    string _sellNewImage = "", _sellEditImage = "";

    /// <summary>Für Tests und die KI: Varianten-Text „Name; Preis; Bestand; Art.-Nr.“ je Zeile.</summary>
    public static List<ItemVariant>? SellParseVariants(string text)
    {
        var list = new List<ItemVariant>();
        foreach (var raw in text.Replace("\r", "").Split('\n'))
        {
            var line = raw.Trim(); if (line.Length == 0) continue;
            var p = line.Split(';').Select(x => x.Trim()).ToArray();
            if (p[0].Length == 0) throw new ArgumentException($"Variante „{line}“: der Name fehlt.");
            if (p.Length < 2 || p[1].Length == 0) throw new ArgumentException($"Variante „{p[0]}“: bitte den Preis angeben (z. B. „M; 12,50; 5“).");
            list.Add(new(p[0], (double)Finance.ParseAmount(p[1]), p.Length > 2 && p[2].Length > 0 ? (int.TryParse(p[2], out var s) && s >= 0 ? s : throw new ArgumentException($"Variante „{p[0]}“: „{p[2]}“ ist kein Bestand.")) : 0, p.Length > 3 ? p[3] : ""));
        }
        return list.Count == 0 ? null : list;
    }

    static string SellVariantsText(Product p) => p.Variants is { } vs ? string.Join("\n", vs.Select(v => $"{v.Name}; {Eu((decimal)v.Price)}; {v.Stock}{(v.Sku.Length > 0 ? "; " + v.Sku : "")}")) : "";
    static string SellPrice(Product p, bool small) => p.Variants is { Count: > 0 } vs && vs.Any(v => v.Price != vs[0].Price)
        ? "ab " + Eu(SellGross(vs.Min(v => v.Price), small)) + " €" : Eu(SellGross(p.Price, small)) + " €";
    static decimal SellGross(double net, bool small) => small ? (decimal)net : Math.Round((decimal)net * (1 + LegalRates.VatStandard / 100m), 2);

    /// <summary>Rechtsklick-Menü für eine Zeile (Ändern, Löschen …).</summary>
    static ContextMenu SellMenu(params (string Name, string Text, Action Run)[] items)
    {
        var m = new ContextMenu();
        foreach (var (n, t, run) in items) { var mi = new MenuItem { Name = n, Header = t }; mi.Click += (_, _) => run(); m.Items.Add(mi); }
        return m;
    }

    void SellUndo() => SellTry(SellV, () => { var what = BizShop.Undo(); _core.Audit.Append(Principal.LocalOwner.Name, "shop.undo", what); _sellEditProd = _sellEditCust = _sellEditOrder = null; Say($"Rückgängig: {what}.", SellV); });

    /// <summary>T-0753 M-B: Storno sagt sichtbar, wenn der Bestand eines gelöschten Artikels nicht zurückgebucht werden konnte.</summary>
    static string SellCancelText(Shop shop) => shop.LastNotRestocked.Count == 0 ? "Bestellung storniert."
        : $"Bestellung storniert. Bestand nicht zurückgebucht für {string.Join(", ", shop.LastNotRestocked.Select(x => "„" + x + "“"))} – den Artikel gibt es nicht mehr.";

    /// <summary>Strg+Z auf der Seite nimmt die letzte Änderung an Artikeln, Kunden oder Bestellungen zurück (nicht in Textfeldern).</summary>
    void SellKeys(Control page) => page.KeyDown += (_, e) =>
    {
        if (e.Key == Key.Z && e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Source is not TextBox && BizShop.UndoLabel is not null) { SellUndo(); e.Handled = true; }
    };

    /// <summary>Ziehen und Ablegen: CSV-Dateien werden nach Kopfzeile als Artikel- oder Kundenliste gelesen, ein Bild auf einer Artikelkarte wird dessen Bild.
    /// Öffentlich für Tests.</summary>
    public string SellDropFiles(IEnumerable<string> files, string? product = null)
    {
        var shop = BizShop; var said = new List<string>();
        foreach (var f in files)
        {
            var ext = Path.GetExtension(f).ToLowerInvariant();
            if (ext == ".csv")
            {
                var head = File.ReadLines(f).FirstOrDefault() ?? "";
                var r = head.Contains("First Name", StringComparison.OrdinalIgnoreCase) ? shop.ImportCustomersCsv(f) : shop.ImportProductsCsv(f);
                said.Add($"{Path.GetFileName(f)}: {r.Added} neu, {r.Updated} aktualisiert" + (r.Warnings.Count > 0 ? " · " + string.Join(" · ", r.Warnings.Take(3)) : ""));
            }
            else if (Shop.ImageTypes.Contains(ext) && product is not null && shop.Products.All().FirstOrDefault(p => p.Name == product) is { } p)
            {
                shop.UpdateProduct(p.Name, p with { Image = shop.ImportImage(f) }); said.Add($"Bild für „{p.Name}“ übernommen");
            }
        }
        return said.Count == 0 ? "Hier lassen sich CSV-Dateien (Shopify-Format) und auf einer Artikelkarte Bilder ablegen." : string.Join(". ", said) + ".";
    }

    void SellDropTarget(Control c, string? product)
    {
        DragDrop.SetAllowDrop(c, true);
        c.AddHandler(DragDrop.DragOverEvent, (_, e) => { e.DragEffects = e.DataTransfer.TryGetFiles() is { Length: > 0 } ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; });
        c.AddHandler(DragDrop.DropEvent, (_, e) =>
        {
            var files = (e.DataTransfer.TryGetFiles() ?? []).Select(x => x.TryGetLocalPath()).OfType<string>().ToList();
            e.Handled = true;
            SellTry(SellV, () => Say(SellDropFiles(files, product), SellV));
        });
    }

    string SellCsvFile(string kind) => Path.Combine(BusinessFolder, DevStudio.SafeName(BizIdea.Name) + "-" + kind + "-shopify.csv");

    /// <summary>CSV-Austausch im Shopify-Spaltensatz.</summary>
    Control SellCsvCard()
    {
        var shop = BizShop;
        async Task Import(string field, bool customers)
        {
            if (await PickMediaFile(field, customers ? "Kundenliste (CSV)" : "Artikelliste (CSV)", ["*.csv"]) is not { } f) return;
            SellTry(SellV, () =>
            {
                var r = customers ? shop.ImportCustomersCsv(f) : shop.ImportProductsCsv(f);
                _core.Audit.Append(Principal.LocalOwner.Name, customers ? "shop.csv.kunden" : "shop.csv.artikel", $"{r.Added} neu, {r.Updated} aktualisiert");
                Say($"{Path.GetFileName(f)}: {r.Added} neu, {r.Updated} aktualisiert." + (r.Warnings.Count > 0 ? " Hinweise: " + string.Join(" · ", r.Warnings.Take(5)) : ""), SellV);
            });
        }
        void Export(string kind, Func<string, string> write) => SellTry(SellV, () => { var f = write(SellCsvFile(kind)); Say("Gespeichert: " + f, SellV); });
        var body = Section("Austausch mit anderen Shops (CSV)", "Spalten wie bei Shopify – so lassen sich Artikel und Kunden mit Shopify, Tabellen und anderen Shop-Programmen tauschen. Du kannst CSV-Dateien auch hier ablegen. Bilder: Shopify nimmt nur Internetadressen, lokale Bilder liegen beim Export im Ordner „bilder“ daneben. Texte, die mit = + - @ beginnen, bekommen zum Schutz vor Formeln ein Hochkomma – Shopify zeigt es sichtbar an.",
            Line(Act("sell-csv-prod-out", "Artikel als CSV", () => Export("Artikel", shop.ExportProductsCsv)),
                Act("sell-csv-prod-in", "Artikel aus CSV laden …", () => { _ = Import("sell-csv-prod", false); }),
                Act("sell-csv-cust-out", "Kunden als CSV", () => Export("Kunden", shop.ExportCustomersCsv)),
                Act("sell-csv-cust-in", "Kunden aus CSV laden …", () => { _ = Import("sell-csv-cust", true); }),
                Act("sell-csv-orders-out", "Bestellungen als CSV", () => Export("Bestellungen", shop.ExportOrdersCsv))));
        var card = Card(body); card.Name = "sell-csv";
        SellDropTarget(card, null);
        return card;
    }

    /// <summary>Kunden: anlegen, Liste mit Ändern und Löschen (auch per Rechtsklick).</summary>
    Control SellCustomersCard(Shop shop, List<BizContact> contacts)
    {
        var cn = SellField("sell-cust-name", "Name", 160); var ca = SellField("sell-cust-address", "Straße, PLZ Ort", 220); var ce = SellField("sell-cust-email", "E-Mail (optional)", 180);
        var list = new StackPanel { Name = "sell-customers", Spacing = 4 };
        var i = 0;
        foreach (var c in contacts)
        {
            var name = c.Name; var k = i;
            if (_sellEditCust == name)
            {
                var en = Field("sell-ce-name", "Name", 160, c.Name); var ef = Field("sell-ce-company", "Firma", 140, c.Company); var ea = Field("sell-ce-address", "Straße, PLZ Ort", 220, c.Address); var em = Field("sell-ce-email", "E-Mail", 180, c.Email);
                var es = new ComboBox { Name = "sell-ce-stage", ItemsSource = new[] { "Interessent", "Angebot", "Kunde" }, SelectedItem = c.Stage is "Interessent" or "Angebot" ? c.Stage : "Kunde", MinWidth = 120 };
                var notes = Field("sell-ce-notes", "Notiz", 220, c.Notes);
                list.Children.Add(Card(new StackPanel { Spacing = 4, Children = { H("Kunde ändern", 13), Line(en, ef, ea, em, es, notes,
                    Act("sell-ce-save", "Speichern", () => SellTry(SellV, () =>
                    {
                        shop.UpdateCustomer(name, c with { Name = Need(en, "den Namen"), Company = Clean(ef), Address = Clean(ea), Email = Clean(em), Stage = es.SelectedItem as string ?? "Kunde", Notes = Clean(notes) });
                        _sellEditCust = null; Say($"Kunde „{Clean(en)}“ gespeichert.", SellV);
                    })),
                    Act("sell-ce-cancel", "Abbrechen", () => { _sellEditCust = null; Show(SellV); })) } }));
            }
            else
            {
                Action edit = () => { _sellEditCust = name; Show(SellV); };
                Action del = () => SellTry(SellV, () => { shop.RemoveCustomer(name); Say($"Kunde „{name}“ gelöscht. „Rückgängig“ holt ihn zurück.", SellV); });
                var row = Card(Row(new StackPanel { Children = { new TextBlock { Text = c.Name + (c.Company.Length > 0 ? " · " + c.Company : ""), Foreground = Text, FontWeight = FontWeight.SemiBold },
                    P(string.Join(" · ", new[] { c.Stage, c.Address, c.Email }.Where(x => x.Length > 0)), null, 11) } },
                    new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { Act($"sell-cust-edit-{k}", "Ändern", edit), Act($"sell-cust-del-{k}", "Löschen", del) } }));
                row.Name = $"sell-cust-row-{k}";
                row.ContextMenu = SellMenu(($"sell-cust-menu-edit-{k}", "Ändern", edit), ($"sell-cust-menu-del-{k}", "Löschen", del));
                list.Children.Add(row);
            }
            i++;
        }
        if (i == 0) list.Children.Add(P("Noch keine Kunden. Lege den ersten oben an oder lade eine Kundenliste als CSV.", Text));
        return Card(Section("Kunden", "Dieselbe Liste wie unter „Betrieb“. Ändern oder Löschen über die Knöpfe oder per Rechtsklick; ein neuer Name wird in die Bestellungen übernommen.",
            Line(cn, ca, ce, Act("sell-cust-add", "Kunde anlegen", () => SellTry(SellV, () =>
            {
                var name = Need(cn, "den Namen");
                shop.AddCustomer(new BizContact(name, "", ce.Text?.Trim() ?? "", "Kunde", null, 0, "", ca.Text?.Trim() ?? "")); Say($"Kunde „{name}“ angelegt.", SellV);
            }))), list));
    }

    Image? SellImage(Shop shop, Product p, double w, double h)
    {
        if (shop.ImagePath(p) is not { } f) return null;
        try { return new Image { Source = new Bitmap(f), Width = w, Height = h, Stretch = Stretch.UniformToFill }; }
        catch (Exception e) when (e is IOException or ArgumentException or InvalidOperationException or NotSupportedException) { return null; }
    }

    async Task SellPickImage(bool forEdit)
    {
        if (await PickMediaFile(forEdit ? "sell-pe-image" : "sell-prod-image", "Artikelbild", ["*.png", "*.jpg", "*.jpeg", "*.webp"]) is not { } f) return;
        Try(SellV, () => { var name = BizShop.ImportImage(f); if (forEdit) _sellEditImage = name; else _sellNewImage = name; Say("Bild übernommen – jetzt speichern.", SellV); }); // Eingaben bleiben stehen
    }

    /// <summary>Artikel: anlegen (mit Beschreibung, Varianten, Bild), Vorschau-Karten mit dem echten Datenstand, Ändern und Löschen.</summary>
    Control SellProductsCard(Shop shop, List<Product> products)
    {
        var small = _settings.SmallBusiness;
        var pn = SellField("sell-prod-name", "Artikel", 180); var pp = SellField("sell-prod-price", "Preis netto €", 110); var ps = SellField("sell-prod-stock", "Bestand", 80, "0"); var psku = SellField("sell-prod-sku", "Artikelnr./EAN", 150);
        var pd = SellField("sell-prod-desc", "Beschreibung (optional)", 300);
        var pv = SellField("sell-prod-variants", "Varianten: je Zeile Name; Preis; Bestand", 340); pv.AcceptsReturn = true; pv.MinHeight = 54;
        var grid = new WrapPanel { Name = "sell-shop" };
        var i = 0;
        foreach (var p in products)
        {
            var name = p.Name; var k = i;
            Action edit = () => { _sellEditProd = name; _sellEditImage = p.Image; Show(SellV); };
            Action del = () => SellTry(SellV, () => { shop.RemoveProduct(name); if (_sellEditProd == name) _sellEditProd = null; Say($"Artikel „{name}“ gelöscht. „Rückgängig“ holt ihn zurück.", SellV); });
            var inner = new StackPanel { Spacing = 4 };
            if (SellImage(shop, p, 166, 120) is { } img) inner.Children.Add(new Border { CornerRadius = new CornerRadius(8), ClipToBounds = true, Child = img });
            else if (p.Image.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) inner.Children.Add(new TextBlock { Text = "Bild im Netz: " + new Uri(p.Image).Host, Foreground = Brush.Parse("#667085"), FontSize = 10.5 });
            inner.Children.Add(new TextBlock { Text = p.Name, Foreground = Brush.Parse("#1F2933"), FontSize = 14, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
            if (p.Description.Length > 0) inner.Children.Add(new TextBlock { Text = p.Description, Foreground = Brush.Parse("#475467"), FontSize = 11, TextWrapping = TextWrapping.Wrap, MaxLines = 3 });
            inner.Children.Add(new TextBlock { Text = SellPrice(p, small), Foreground = Brush.Parse("#7A5A10"), FontSize = 18, FontWeight = FontWeight.Bold });
            if (p.Variants is { Count: > 0 } vs)
                foreach (var v in vs) inner.Children.Add(new TextBlock { Text = $"{v.Name} · {Eu(SellGross(v.Price, small))} € · {(Shop.MadeToOrder(p) ? "auf Bestellung" : v.Stock > 0 ? $"{v.Stock} vorrätig" : "nicht vorrätig")}", Foreground = Brush.Parse("#344054"), FontSize = 11 });
            else inner.Children.Add(new TextBlock { Text = (Shop.MadeToOrder(p) ? "wird auf Bestellung gefertigt" : p.Stock > 0 ? $"{p.Stock} vorrätig" : "nicht vorrätig") + (shop.Reserved(p.Name) is var rs && rs > 0 ? $" · {rs:0.##} vorgemerkt" : ""), Foreground = Brush.Parse(p.Stock > 0 || Shop.MadeToOrder(p) ? "#167A45" : "#B2361F"), FontSize = 11 });
            if (p.Variants is { Count: > 0 } && shop.Reserved(p.Name) is var rv && rv > 0) inner.Children.Add(new TextBlock { Text = $"{rv:0.##} vorgemerkt", Foreground = Brush.Parse("#475467"), FontSize = 11 });
            var e = new Button { Name = $"sell-prod-edit-{k}", Content = "Ändern", FontSize = 11, Padding = new Thickness(8, 2), Foreground = Brush.Parse("#1F2933"), Background = Brush.Parse("#EEF0F3"), BorderBrush = Brush.Parse("#D0D5DD"), BorderThickness = new Thickness(1) };
            e.Click += (_, _) => edit();
            var d = new Button { Name = $"sell-prod-del-{k}", Content = "Löschen", FontSize = 11, Padding = new Thickness(8, 2), Foreground = Brush.Parse("#1F2933"), Background = Brush.Parse("#EEF0F3"), BorderBrush = Brush.Parse("#D0D5DD"), BorderThickness = new Thickness(1) };
            d.Click += (_, _) => del();
            inner.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { e, d } });
            var card = new Border { Name = $"sell-prod-card-{k}", Width = 190, Margin = new Thickness(0, 0, 10, 10), Padding = new Thickness(12), CornerRadius = new CornerRadius(14), Background = Brushes.White, BoxShadow = BoxShadows.Parse("0 6 16 -4 #55000000"),
                BorderBrush = _sellEditProd == name ? Accent : null, BorderThickness = new Thickness(_sellEditProd == name ? 2 : 0), Child = inner };
            card.ContextMenu = SellMenu(($"sell-prod-menu-edit-{k}", "Ändern", edit), ($"sell-prod-menu-del-{k}", "Löschen", del));
            SellDropTarget(card, name);
            grid.Children.Add(card);
            i++;
        }
        if (products.Count == 0) grid.Children.Add(P("Noch keine Artikel. Lege den ersten oben an oder lade eine Artikelliste als CSV.", Text));

        var parts = new List<Control>
        {
            Line(pn, pp, ps, psku, Act("sell-prod-add", "Artikel anlegen", () => SellTry(SellV, () =>
            {
                var name = Need(pn, "den Artikelnamen");
                var price = (double)Finance.ParseAmount(Need(pp, "den Preis")); if (price < 0) throw new ArgumentException("Der Preis darf nicht negativ sein.");
                var sku = SalesStats.CheckSku(psku.Text ?? "");
                shop.AddProduct(new Product(name, "", 0, price, int.TryParse(ps.Text, out var st) ? Math.Max(0, st) : 0, 0, 0, sku, pd.Text?.Trim() ?? "", _sellNewImage, SellParseVariants(pv.Text ?? "")));
                _sellNewImage = ""; Say($"Artikel „{name}“ angelegt.", SellV);
            })), Act("sell-shop-save", "Als Webseite speichern", () => SellTry(SellV, () =>
            {
                var f = Path.Combine(BusinessFolder, DevStudio.SafeName(BizIdea.Name) + "-Shop.html");
                AtomicFile.WriteAllText(f, Shop.ShopHtml(BizIdea.Name, shop.Products.All(), _settings.SmallBusiness, shop.ImageDir)); Launch(f); Say("Gespeichert: " + f, SellV);
            }))),
            Line(pd, pv, Act("sell-prod-image", _sellNewImage.Length > 0 ? "Bild gewählt ✓" : "Bild wählen …", () => { _ = SellPickImage(false); })),
        };
        if (_sellEditProd is { } en && products.FirstOrDefault(x => x.Name == en) is { } ep) parts.Add(SellProductEditor(shop, ep));
        parts.Add(grid);
        return Card(Section("Artikel und Webshop-Vorschau", "So sieht dein Shop mit dem aktuellen Stand aus. Ändern, Löschen per Knopf oder Rechtsklick; ein Bild lässt sich auch auf die Karte ziehen. „Als Webseite speichern“ legt eine HTML-Seite an – ohne Bezahlfunktion. Eigene Bilder stecken in der Seite, Bilder aus Print on Demand lädt sie von der Adresse des Anbieters.", [.. parts]));
    }

    Control SellProductEditor(Shop shop, Product p)
    {
        var name = p.Name;
        var en = Field("sell-pe-name", "Artikel", 180, p.Name); var ep = Field("sell-pe-price", "Preis netto €", 110, Eu((decimal)p.Price)); var es = Field("sell-pe-stock", "Bestand", 80, p.Stock.ToString());
        var ek = Field("sell-pe-sku", "Artikelnr./EAN", 150, p.Sku); var ed = Field("sell-pe-desc", "Beschreibung", 340, p.Description);
        var ev = Field("sell-pe-variants", "Varianten, je Zeile: Name; Preis; Bestand; Art.-Nr.", 340, SellVariantsText(p)); ev.AcceptsReturn = true; ev.MinHeight = 70;
        var hasVariants = p.Variants is { Count: > 0 };
        if (hasVariants) { ep.IsEnabled = false; es.IsEnabled = false; ep.PlaceholderText = es.PlaceholderText = "aus den Varianten"; }
        var preview = SellImage(shop, p with { Image = _sellEditImage }, 120, 90);
        var sp = new StackPanel { Name = "sell-prod-editor", Spacing = 4, Children = { H($"„{name}“ ändern", 13), Line(en, ep, es, ek), Line(ed, ev) } };
        var imgRow = Line(Act("sell-pe-image", "Anderes Bild …", () => { _ = SellPickImage(true); }), Act("sell-pe-noimage", "Bild entfernen", () => { _sellEditImage = ""; Show(SellV); }));
        if (preview is not null) imgRow.Children.Insert(0, new Border { CornerRadius = new CornerRadius(6), ClipToBounds = true, Child = preview });
        sp.Children.Add(imgRow);
        sp.Children.Add(Line(Act("sell-pe-save", "Speichern", () => SellTry(SellV, () =>
        {
            var vars = SellParseVariants(ev.Text ?? "");
            var changed = p with { Name = Need(en, "den Artikelnamen"), Sku = SalesStats.CheckSku(ek.Text ?? ""), Description = ed.Text?.Trim() ?? "", Image = _sellEditImage, Variants = vars,
                Price = vars is null ? (double)Finance.ParseAmount(Need(ep, "den Preis")) : p.Price, Stock = vars is null ? (int.TryParse(es.Text, out var s) ? s : throw new ArgumentException("Bitte den Bestand als ganze Zahl eintragen.")) : p.Stock };
            shop.UpdateProduct(name, changed); _sellEditProd = null;
            _core.Audit.Append(Principal.LocalOwner.Name, "shop.product.edit", changed.Name);
            Say($"Artikel „{changed.Name}“ gespeichert. Alte Bestellungen behalten ihren Preis.", SellV);
        })), Act("sell-pe-cancel", "Abbrechen", () => { _sellEditProd = null; Show(SellV); })));
        return Card(sp);
    }

    /// <summary>Knopfzeile „Rückgängig“ (nur wenn es etwas zurückzunehmen gibt).</summary>
    Control? SellUndoBar() => BizShop.UndoLabel is { } u
        ? Line(Act("sell-undo", "Rückgängig: " + u, SellUndo), P("Strg+Z · nimmt die letzte Änderung an Artikeln, Kunden oder Bestellungen zurück", null, 11))
        : null;
}
