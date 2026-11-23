using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Trendo.Core;

// T-0753 (STUDIO-ONLINE-BUSINESS-01): Artikel, Kunden und Bestellungen ändern und löschen, Artikelbilder, Varianten,
// Rückgängig für diese Stammdaten und CSV im Shopify-Spaltensatz (Artikel und Kunden ein/aus, Bestellungen aus).

/// <summary>Ergebnis eines CSV-Imports: neu angelegt, aktualisiert und Hinweise (z. B. doppelte Artikelnummern).</summary>
public sealed record ShopImport(int Added, int Updated, IReadOnlyList<string> Warnings);

public sealed partial class Shop
{
    /// <summary>Trenner zwischen Artikel und Variante in Bestellzeilen: „Shirt – M“.</summary>
    public const string VariantSep = " – ";
    public static readonly string[] ImageTypes = [".png", ".jpg", ".jpeg", ".webp"];
    const long MaxImageBytes = 10 * 1024 * 1024;
    const int UndoDepth = 20;

    string IdeaDir => Path.GetDirectoryName(Products.FilePath)!;
    /// <summary>Ordner der Artikelbilder dieser Geschäftsidee.</summary>
    public string ImageDir => Path.Combine(IdeaDir, "bilder");
    string UndoFile => Path.Combine(IdeaDir, "rueckgaengig.json");

    public static string Label(Product p, ItemVariant v) => p.Name + VariantSep + v.Name;

    /// <summary>Artikel (und Variante, sonst −1) zu einer Bezeichnung aus einer Bestellzeile.</summary>
    public static (int Product, int Variant) FindItem(List<Product> prods, string label)
    {
        var i = prods.FindIndex(p => p.Name == label);
        if (i >= 0) return (i, -1);
        for (var k = 0; k < prods.Count; k++)
            if (prods[k].Variants is { } vs && label.StartsWith(prods[k].Name + VariantSep, StringComparison.Ordinal))
                for (var j = 0; j < vs.Count; j++) if (Label(prods[k], vs[j]) == label) return (k, j);
        return (-1, -1);
    }

    /// <summary>Bestand ändern (nicht unter 0); bei einer Variante wird die Summe des Artikels nachgeführt.</summary>
    static void AddStock(List<Product> prods, string label, int delta)
    {
        var (i, v) = FindItem(prods, label);
        if (i < 0) return;
        if (v < 0) { prods[i] = prods[i] with { Stock = Math.Max(0, prods[i].Stock + delta) }; return; }
        var vs = new List<ItemVariant>(prods[i].Variants!);
        vs[v] = vs[v] with { Stock = Math.Max(0, vs[v].Stock + delta) };
        prods[i] = prods[i] with { Variants = vs, Stock = vs.Sum(x => x.Stock) };
    }

    /// <summary>Was sich bestellen lässt: Artikel ohne Varianten mit Namen, sonst jede Variante einzeln.</summary>
    public List<string> OrderableItems() => Products.All().SelectMany(p => p.Variants is { Count: > 0 } vs ? vs.Select(v => Label(p, v)) : [p.Name]).ToList();

    /// <summary>Pfad des lokalen Artikelbilds oder null (kein Bild, Adresse im Netz oder Datei fehlt).</summary>
    public string? ImagePath(Product p)
    {
        if (p.Image.Length == 0 || p.Image.Contains("://", StringComparison.Ordinal)) return null;
        var f = Path.Combine(ImageDir, Path.GetFileName(p.Image));
        return File.Exists(f) ? f : null;
    }

    /// <summary>Kopiert ein Bild in den Bilderordner der Idee und liefert den Dateinamen, der im Artikel steht.
    /// So bleibt das Bild erhalten, auch wenn die Ursprungsdatei verschoben wird.</summary>
    public string ImportImage(string source)
    {
        if (!File.Exists(source)) throw new ArgumentException("Die Bilddatei gibt es nicht.");
        var ext = Path.GetExtension(source).ToLowerInvariant();
        if (!ImageTypes.Contains(ext)) throw new ArgumentException("Bitte ein Bild als PNG, JPEG oder WebP wählen.");
        if (new FileInfo(source).Length > MaxImageBytes) throw new ArgumentException("Das Bild ist größer als 10 MB.");
        var bytes = File.ReadAllBytes(source);
        if (bytes.Length == 0) throw new ArgumentException("Die Bilddatei ist leer.");
        var name = Convert.ToHexString(SHA256.HashData(bytes))[..12].ToLowerInvariant() + "-" + DevStudio.SafeName(Path.GetFileNameWithoutExtension(source)) + ext;
        var target = Path.Combine(ImageDir, name);
        if (!File.Exists(target)) AtomicFile.WriteAllBytes(target, bytes);
        return name;
    }

    // ---------- Rückgängig: Stand vorher plus Prüfsumme danach; dazwischen verkauft/geliefert/bezahlt → nicht mehr möglich ----------
    sealed record UndoStep(string What, Dictionary<string, string?> Before, string After);
    string[] Files => [Products.FilePath, Customers.FilePath, OrderList.FilePath];
    static string? ReadOrNull(string f) => File.Exists(f) ? File.ReadAllText(f) : null;
    string Hash() => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\u0001", Files.Select(f => ReadOrNull(f) ?? "∅")))));
    List<UndoStep> LoadUndo() { try { return File.Exists(UndoFile) ? JsonSerializer.Deserialize<List<UndoStep>>(File.ReadAllText(UndoFile)) ?? [] : []; } catch (JsonException) { return []; } }

    /// <summary>Führt eine Änderung der Stammdaten aus und merkt den Stand davor.</summary>
    T Undoable<T>(string what, Func<T> change)
    {
        var before = Files.ToDictionary(f => Path.GetFileName(f), ReadOrNull);
        var r = change();
        var l = LoadUndo(); l.Add(new(what, before, Hash()));
        if (l.Count > UndoDepth) l.RemoveRange(0, l.Count - UndoDepth);
        AtomicFile.WriteAllText(UndoFile, JsonSerializer.Serialize(l));
        return r;
    }

    /// <summary>Was „Rückgängig“ zurücknehmen würde, oder null.</summary>
    public string? UndoLabel => LoadUndo() is { Count: > 0 } l ? l[^1].What : null;

    /// <summary>Nimmt die letzte Änderung an Artikeln, Kunden oder Bestellungen zurück. Wurde seitdem verkauft, geliefert oder bezahlt,
    /// lehnt es ab – sonst passten Bestand, Bons und Buchungen nicht mehr zusammen.</summary>
    public string Undo()
    {
        var l = LoadUndo();
        if (l.Count == 0) throw new InvalidOperationException("Es gibt nichts rückgängig zu machen.");
        var step = l[^1];
        if (step.After != Hash()) { AtomicFile.WriteAllText(UndoFile, "[]"); throw new InvalidOperationException("Seitdem wurde verkauft, geliefert oder bezahlt – „Rückgängig“ ist dafür nicht mehr möglich."); }
        foreach (var f in Files)
            if (step.Before.TryGetValue(Path.GetFileName(f), out var text) && text is not null) AtomicFile.WriteAllText(f, text);
            else if (File.Exists(f)) File.Delete(f);
        l.RemoveAt(l.Count - 1);
        AtomicFile.WriteAllText(UndoFile, JsonSerializer.Serialize(l));
        return step.What;
    }

    // ---------- Artikel ----------
    static void CheckProduct(Product p, IEnumerable<Product> others)
    {
        if (string.IsNullOrWhiteSpace(p.Name)) throw new ArgumentException("Bitte einen Artikelnamen eintragen.");
        if (p.Name.Contains(VariantSep, StringComparison.Ordinal)) throw new ArgumentException($"Der Name darf „{VariantSep.Trim()}“ nicht enthalten, das trennt die Variante.");
        if (p.Price < 0 || p.Variants?.Any(v => v.Price < 0) == true) throw new ArgumentException("Der Preis darf nicht negativ sein.");
        if (p.Stock < 0 || p.Variants?.Any(v => v.Stock < 0) == true) throw new ArgumentException("Der Bestand darf nicht negativ sein.");
        var list = others.ToList();
        if (list.Any(x => x.Name == p.Name)) throw new ArgumentException($"Den Artikel „{p.Name}“ gibt es schon.");
        if (p.Variants is { } vs)
        {
            if (vs.Any(v => string.IsNullOrWhiteSpace(v.Name))) throw new ArgumentException("Jede Variante braucht einen Namen.");
            if (vs.GroupBy(v => v.Name, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1) is { } dup) throw new ArgumentException($"Die Variante „{dup.Key}“ steht doppelt da.");
        }
        var skus = list.SelectMany(Skus).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var own = Skus(p).ToList();
        if (own.GroupBy(s => s, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1) is { } d2) throw new ArgumentException($"Die Artikelnummer „{d2.Key}“ steht doppelt da.");
        if (own.FirstOrDefault(skus.Contains) is { } taken) throw new ArgumentException($"Die Artikelnummer „{taken}“ hat schon ein anderer Artikel.");
    }
    static IEnumerable<string> Skus(Product p) => new[] { p.Sku }.Concat(p.Variants?.Select(v => v.Sku) ?? []).Where(s => s.Length > 0);

    /// <summary>Hat ein Artikel Varianten, ist sein Bestand die Summe und sein Preis der der ersten Variante (für Kasse und Listen).</summary>
    static Product Normalize(Product p)
    {
        p = p with { Name = p.Name.Trim(), Sku = SalesStats.CheckSku(p.Sku), Description = p.Description.Trim() };
        if (p.Variants is not { Count: > 0 } vs) return p with { Variants = null };
        vs = vs.Select(v => v with { Name = v.Name.Trim(), Sku = SalesStats.CheckSku(v.Sku) }).ToList();
        return p with { Variants = vs, Stock = vs.Sum(v => v.Stock), Price = vs[0].Price };
    }

    public Product AddProduct(Product p) => Undoable($"Artikel „{p.Name.Trim()}“ anlegen", () =>
    {
        p = Normalize(p); var all = Products.All(); CheckProduct(p, all);
        all.Add(p); Products.Save(all); return p;
    });

    /// <summary>Ändert einen Artikel. Bei neuem Namen tragen offene und gelieferte Bestellungen den neuen Namen,
    /// ihr Preis bleibt der beim Bestellen festgehaltene. Eine Variante, die noch vorgemerkt ist, lässt sich nicht entfernen.</summary>
    public Product UpdateProduct(string name, Product changed) => Undoable($"Artikel „{name}“ ändern", () =>
    {
        var all = Products.All(); var i = all.FindIndex(p => p.Name == name);
        if (i < 0) throw new ArgumentException($"Den Artikel „{name}“ gibt es nicht.");
        changed = Normalize(changed);
        CheckProduct(changed, all.Where((_, k) => k != i));
        var oldLabels = all[i].Variants is { } ov ? ov.Select(v => Label(all[i], v)).ToList() : [name];
        var newLabels = changed.Variants is { } nv ? nv.Select(v => Label(changed, v)).ToHashSet() : [changed.Name];
        var orders = OrderList.All();
        string Map(string label) => label == name ? changed.Name : label.StartsWith(name + VariantSep, StringComparison.Ordinal) ? changed.Name + label[name.Length..] : label;
        foreach (var o in orders.Where(o => o.StockOut == false && o.Status != "storniert"))
            foreach (var l in o.Lines.Where(l => oldLabels.Contains(l.Product) || l.Product == name))
                if (!newLabels.Contains(Map(l.Product))) throw new InvalidOperationException($"„{l.Product}“ ist noch für eine offene Bestellung vorgemerkt – erst liefern oder stornieren.");
        if (changed.Name != name)
        {
            for (var k = 0; k < orders.Count; k++)
                if (orders[k].Status != "storniert" && orders[k].StockOut is not null && orders[k].Lines.Any(l => Map(l.Product) != l.Product))
                    orders[k] = orders[k] with { Lines = orders[k].Lines.Select(l => l with { Product = Map(l.Product) }).ToList() };
            OrderList.Save(orders);
        }
        all[i] = changed; Products.Save(all); return changed;
    });

    public void RemoveProduct(string name) => Undoable($"Artikel „{name}“ löschen", () =>
    {
        var all = Products.All(); var i = all.FindIndex(p => p.Name == name);
        if (i < 0) throw new ArgumentException($"Den Artikel „{name}“ gibt es nicht.");
        if (Reserved(name) > 0) throw new InvalidOperationException($"„{name}“ ist noch für eine offene Bestellung vorgemerkt – erst liefern oder stornieren.");
        all.RemoveAt(i); Products.Save(all); return true;
    });

    // ---------- Kunden ----------
    public BizContact AddCustomer(BizContact c) => Undoable($"Kunde „{c.Name.Trim()}“ anlegen", () =>
    {
        c = c with { Name = c.Name.Trim() }; var all = Customers.All();
        if (c.Name.Length == 0) throw new ArgumentException("Bitte den Namen eintragen.");
        if (all.Any(x => x.Name == c.Name)) throw new ArgumentException("Diesen Kunden gibt es schon.");
        all.Add(c); Customers.Save(all); return c;
    });

    /// <summary>Ändert einen Kunden; ein neuer Name wird in seine Bestellungen übernommen (geschriebene Rechnungen bleiben unverändert).</summary>
    public BizContact UpdateCustomer(string name, BizContact changed) => Undoable($"Kunde „{name}“ ändern", () =>
    {
        changed = changed with { Name = changed.Name.Trim() };
        var all = Customers.All(); var i = all.FindIndex(c => c.Name == name);
        if (i < 0) throw new ArgumentException($"Den Kunden „{name}“ gibt es nicht.");
        if (changed.Name.Length == 0) throw new ArgumentException("Bitte den Namen eintragen.");
        if (changed.Name != name && all.Any(c => c.Name == changed.Name)) throw new ArgumentException($"Den Kunden „{changed.Name}“ gibt es schon.");
        if (changed.Name != name)
        {
            var orders = OrderList.All();
            if (orders.Any(o => o.Customer == name)) OrderList.Save(orders.Select(o => o.Customer == name ? o with { Customer = changed.Name } : o).ToList());
        }
        all[i] = changed; Customers.Save(all); return changed;
    });

    public void RemoveCustomer(string name) => Undoable($"Kunde „{name}“ löschen", () =>
    {
        var all = Customers.All(); var i = all.FindIndex(c => c.Name == name);
        if (i < 0) throw new ArgumentException($"Den Kunden „{name}“ gibt es nicht.");
        if (OrderList.All().Any(o => o.Customer == name)) throw new InvalidOperationException($"„{name}“ hat Bestellungen – Kunden mit Bestellungen bleiben für die Buchhaltung erhalten.");
        all.RemoveAt(i); Customers.Save(all); return true;
    });

    // ---------- Bestellungen ----------
    public ShopOrder PlaceOrder(string customer, DateOnly date, params (string Product, decimal Qty)[] lines) => Undoable($"Bestellung für „{customer}“ eintragen", () => AddOrder(customer, date, lines));

    /// <summary>Ändert eine Bestellung, solange sie offen ist (nicht geliefert, ohne Rechnung, ohne Zahlung). Preise werden neu festgehalten.</summary>
    public ShopOrder UpdateOrder(string orderId, string customer, params (string Product, decimal Qty)[] lines) => Undoable("Bestellung ändern", () =>
    {
        var orders = OrderList.All(); var i = orders.FindIndex(o => o.Id == orderId);
        if (i < 0) throw new InvalidOperationException("Diese Bestellung gibt es nicht.");
        var o = orders[i];
        if (o.Status != "offen" || o.StockOut != false || o.InvoiceNumber.Length > 0 || o.Payments is not { Count: 0 })
            throw new InvalidOperationException("Nur offene Bestellungen ohne Lieferung, Rechnung und Zahlung lassen sich ändern.");
        if (Customers.All().All(c => c.Name != customer)) throw new ArgumentException("Bitte einen Kunden wählen.");
        if (lines.Length == 0 || lines.Any(l => l.Qty <= 0)) throw new ArgumentException("Bitte mindestens einen Artikel mit Menge eintragen.");
        var prods = Products.All(); var res = new List<OrderLine>();
        foreach (var (name, qty) in lines)
        {
            var (pi, v) = FindItem(prods, name); if (pi < 0) throw new ArgumentException($"Den Artikel „{name}“ gibt es nicht.");
            res.Add(new(name, qty, (decimal)(v >= 0 ? prods[pi].Variants![v].Price : prods[pi].Price)));
        }
        orders[i] = o with { Customer = customer, Lines = res }; OrderList.Save(orders); return orders[i];
    });

    // ---------- CSV im Shopify-Format ----------
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Spalten des Shopify-Produkt-CSV (Namen laut Shopify-Hilfe „Using CSV files to import and export products“, Stand November 2026).</summary>
    public static readonly string[] ProductColumns = ["Title", "URL handle", "Description", "Vendor", "Type", "Tags", "Published on online store", "Status", "SKU",
        "Option1 name", "Option1 value", "Price", "Cost per item", "Charge tax", "Inventory tracker", "Inventory quantity", "Continue selling when out of stock",
        "Requires shipping", "Fulfillment service", "Product image URL", "Image position"];
    /// <summary>Spalten des Shopify-Kunden-CSV (Vorlage laut Shopify-Hilfe „Importing and exporting customers“).</summary>
    public static readonly string[] CustomerColumns = ["First Name", "Last Name", "Email", "Accepts Email Marketing", "Default Address Company", "Default Address Address1",
        "Default Address Address2", "Default Address City", "Default Address Province Code", "Default Address Country Code", "Default Address Zip", "Default Address Phone",
        "Phone", "Accepts SMS Marketing", "Accepts WhatsApp Marketing", "Total Spent", "Total Orders", "Note", "Tax Exempt", "Tags"];
    /// <summary>Spalten wie im Shopify-Bestellexport (Auswahl), eine Zeile je Bestellposition.</summary>
    public static readonly string[] OrderColumns = ["Name", "Email", "Financial Status", "Fulfillment Status", "Currency", "Subtotal", "Total", "Created at",
        "Lineitem quantity", "Lineitem name", "Lineitem price", "Lineitem sku", "Billing Name", "Id"];

    static string Cell(string s) => s.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
    static string Row(IEnumerable<string> cells) => string.Join(',', cells.Select(Cell)) + "\n";
    static string Num(double d) => d.ToString("0.00", Inv);

    /// <summary>„Kaffee Bohne süß“ → „kaffee-bohne-suess“ (Shopify-Handle).</summary>
    public static string Handle(string name)
    {
        var s = name.ToLowerInvariant().Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue").Replace("ß", "ss");
        s = Regex.Replace(s, "[^a-z0-9]+", "-").Trim('-');
        return s.Length == 0 ? "artikel" : s;
    }

    /// <summary>Artikel als Shopify-CSV (UTF-8, Komma, LF). Lokale Bilder werden in <c>bilder/</c> neben die Datei kopiert und relativ eingetragen –
    /// Shopify selbst nimmt nur öffentliche https-Adressen, diese Bilder musst du dort hochladen.</summary>
    public string ExportProductsCsv(string file)
    {
        var sb = new StringBuilder(Row(ProductColumns));
        var used = new HashSet<string>();
        var dir = Path.GetDirectoryName(Path.GetFullPath(file))!;
        foreach (var p in Products.All())
        {
            var h = Handle(p.Name); var n = 2; while (!used.Add(h)) h = Handle(p.Name) + "-" + n++;
            var img = p.Image.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ? p.Image : "";
            if (img.Length == 0 && ImagePath(p) is { } local)
            {
                Directory.CreateDirectory(Path.Combine(dir, "bilder"));
                File.Copy(local, Path.Combine(dir, "bilder", Path.GetFileName(local)), true);
                img = "bilder/" + Path.GetFileName(local);
            }
            var body = WebUtility.HtmlEncode(p.Description).Replace("\r\n", "\n").Replace("\n", "<br>");
            var variants = p.Variants is { Count: > 0 } vs ? vs.Select(v => (Opt: "Variante", Val: v.Name, v.Sku, v.Price, v.Stock)).ToList() : [("Title", "Default Title", p.Sku, p.Price, p.Stock)];
            for (var k = 0; k < variants.Count; k++)
            {
                var (opt, val, sku, price, stock) = variants[k];
                var first = k == 0;
                sb.Append(Row([first ? p.Name : "", h, first ? body : "", first ? p.Supplier : "", "", "", first ? "true" : "", first ? "active" : "", sku,
                    first ? opt : "", val, Num(price), first && p.Cost > 0 ? Num(p.Cost) : "", "true", "shopify", stock.ToString(Inv), "false", "true", "manual",
                    first ? img : "", first && img.Length > 0 ? "1" : ""]));
            }
        }
        AtomicFile.WriteAllText(file, sb.ToString());
        return file;
    }

    /// <summary>Liest CSV nach RFC 4180 (Komma oder – bei deutschen Tabellen – Semikolon, Felder in Anführungszeichen mit Zeilenumbruch).</summary>
    public static List<string[]> ParseCsv(string text)
    {
        if (text.Length > 0 && text[0] == '﻿') text = text[1..];
        var firstLine = text.Split('\n', 2)[0];
        var sep = firstLine.Count(c => c == ';') > firstLine.Count(c => c == ',') ? ';' : ',';
        var rows = new List<string[]>(); var row = new List<string>(); var cell = new StringBuilder(); var q = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (q)
            {
                if (c == '"') { if (i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; } else q = false; }
                else cell.Append(c);
            }
            else if (c == '"') q = true;
            else if (c == sep) { row.Add(cell.ToString()); cell.Clear(); }
            else if (c == '\n' || c == '\r')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                row.Add(cell.ToString()); cell.Clear();
                if (row.Count > 1 || row[0].Length > 0) rows.Add([.. row]);
                row.Clear();
            }
            else cell.Append(c);
        }
        if (cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString()); if (row.Count > 1 || row[0].Length > 0) rows.Add([.. row]); }
        return rows;
    }

    /// <summary>Spaltenzugriff mit den neuen und den älteren Shopify-Namen (z. B. „Price“ und „Variant Price“).</summary>
    sealed class Cols(string[] header)
    {
        readonly Dictionary<string, int> _i = header.Select((h, i) => (h.Trim(), i)).GroupBy(x => x.Item1, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First().i, StringComparer.OrdinalIgnoreCase);
        public bool Has(params string[] names) => names.Any(_i.ContainsKey);
        public string Get(string[] row, params string[] names)
        {
            foreach (var n in names) if (_i.TryGetValue(n, out var i) && i < row.Length) return row[i].Trim();
            return "";
        }
    }

    static double ParseNum(string s, string what, int line)
    {
        if (s.Length == 0) return 0;
        var t = s.Replace("€", "").Trim();
        if (t.Contains(',') && !t.Contains('.')) t = t.Replace(',', '.'); // deutsche Tabelle: 12,50
        if (double.TryParse(t, NumberStyles.Number, Inv, out var d)) return d;
        throw new ArgumentException($"Zeile {line}: „{s}“ ist keine Zahl ({what}).");
    }

    static string PlainText(string html) => WebUtility.HtmlDecode(Regex.Replace(Regex.Replace(html, "<br\\s*/?>|</p>", "\n", RegexOptions.IgnoreCase), "<[^>]+>", "")).Trim();

    /// <summary>Artikel aus einem Shopify-CSV (neue oder ältere Spaltennamen). Gleicher Name → der Artikel wird aktualisiert, sonst neu angelegt.
    /// Doppelte Artikelnummern werden gemeldet und beim zweiten Vorkommen weggelassen. Alles wird in einem Schritt gespeichert und lässt sich rückgängig machen.</summary>
    public ShopImport ImportProductsCsv(string file)
    {
        var rows = ParseCsv(File.ReadAllText(file, Encoding.UTF8));
        if (rows.Count == 0) throw new ArgumentException("Die Datei ist leer.");
        var c = new Cols(rows[0]);
        if (!c.Has("Title")) throw new ArgumentException("Das ist keine Artikelliste im Shopify-Format: die Spalte „Title“ fehlt.");
        var dir = Path.GetDirectoryName(Path.GetFullPath(file))!;
        var groups = new List<(string Handle, List<(string[] Row, int Line)> Rows)>();
        for (var r = 1; r < rows.Count; r++)
        {
            var h = c.Get(rows[r], "URL handle", "Handle"); var title = c.Get(rows[r], "Title");
            if (h.Length == 0) h = title.Length > 0 ? "#" + r : "";
            if (h.Length == 0) continue;
            if (groups.Count > 0 && groups[^1].Handle == h) groups[^1].Rows.Add((rows[r], r + 1));
            else if (groups.FindIndex(g => g.Handle == h) is var gi and >= 0) groups[gi].Rows.Add((rows[r], r + 1));
            else groups.Add((h, [(rows[r], r + 1)]));
        }
        var warnings = new List<string>();
        var parsed = new List<Product>();
        var seenSku = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string Sku(string s, int line) { try { s = SalesStats.CheckSku(s); } catch (ArgumentException e) { warnings.Add($"Zeile {line}: {e.Message} – ohne Nummer übernommen."); return ""; } if (s.Length == 0) return ""; if (seenSku.Add(s)) return s; warnings.Add($"Zeile {line}: Artikelnummer „{s}“ kommt doppelt vor – beim zweiten Mal weggelassen."); return ""; }
        foreach (var (_, g) in groups)
        {
            var (head, line0) = g.FirstOrDefault(x => c.Get(x.Row, "Title").Length > 0);
            if (head is null) { warnings.Add($"Zeile {g[0].Line}: Variante ohne Artikelzeile mit „Title“ – übersprungen."); continue; }
            var name = c.Get(head, "Title").Replace(VariantSep, " - ");
            var vars = new List<ItemVariant>();
            var image = "";
            foreach (var (row, line) in g)
            {
                var img = c.Get(row, "Product image URL", "Image Src");
                if (image.Length == 0 && img.Length > 0)
                {
                    if (img.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) image = img;
                    else if (!img.Contains("://", StringComparison.Ordinal) && Path.Combine(dir, img.Replace('/', Path.DirectorySeparatorChar)) is var local && File.Exists(local)) image = ImportImage(local);
                    else warnings.Add($"Zeile {line}: Bild „{img}“ nicht gefunden – ohne Bild übernommen.");
                }
                var val = c.Get(row, "Option1 value", "Option1 Value");
                if (val.Length == 0 && c.Get(row, "Price", "Variant Price").Length == 0) continue; // reine Bildzeile
                vars.Add(new(val.Length == 0 ? "Default Title" : val, ParseNum(c.Get(row, "Price", "Variant Price"), "Preis", line),
                    (int)Math.Max(0, ParseNum(c.Get(row, "Inventory quantity", "Variant Inventory Qty"), "Bestand", line)), Sku(c.Get(row, "SKU", "Variant SKU"), line)));
            }
            if (vars.Count == 0) { warnings.Add($"Zeile {line0}: „{name}“ ohne Preis – übersprungen."); continue; }
            var single = vars.Count == 1 && vars[0].Name == "Default Title";
            parsed.Add(new Product(name, c.Get(head, "Vendor"), ParseNum(c.Get(head, "Cost per item"), "Einkaufspreis", line0), vars[0].Price, single ? vars[0].Stock : vars.Sum(v => v.Stock), 0, 0,
                single ? vars[0].Sku : "", PlainText(c.Get(head, "Description", "Body (HTML)")), image, single ? null : vars));
        }
        var added = 0; var updated = 0;
        Undoable($"{parsed.Count} Artikel aus CSV laden", () =>
        {
            var all = Products.All();
            foreach (var raw in parsed)
            {
                var p = Normalize(raw); var i = all.FindIndex(x => x.Name == p.Name);
                var keep = i >= 0 ? all[i] : null;
                if (keep is not null) p = p with { LeadDays = keep.LeadDays, DailySales = keep.DailySales, Image = p.Image.Length > 0 ? p.Image : keep.Image };
                var others = all.Where((_, k) => k != i).ToList();
                var taken = others.SelectMany(Skus).ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (Skus(p).FirstOrDefault(taken.Contains) is { } t) { warnings.Add($"„{p.Name}“: Artikelnummer „{t}“ hat schon „{others.First(o => Skus(o).Contains(t, StringComparer.OrdinalIgnoreCase)).Name}“ – ohne Nummer übernommen."); p = p with { Sku = taken.Contains(p.Sku) ? "" : p.Sku, Variants = p.Variants?.Select(v => taken.Contains(v.Sku) ? v with { Sku = "" } : v).ToList() }; }
                if (i >= 0) { all[i] = p; updated++; } else { all.Add(p); added++; }
            }
            Products.Save(all); return true;
        });
        return new(added, updated, warnings);
    }

    /// <summary>Kunden als Shopify-Kunden-CSV. „Straße, PLZ Ort“ wird in Adresse, PLZ und Ort aufgeteilt, das Land ist DE.</summary>
    public string ExportCustomersCsv(string file)
    {
        var sb = new StringBuilder(Row(CustomerColumns));
        var orders = Orders();
        foreach (var k in Customers.All())
        {
            var sp = k.Name.LastIndexOf(' ');
            var (first, last) = sp > 0 ? (k.Name[..sp], k.Name[(sp + 1)..]) : (k.Name, "");
            var (street, zip, city) = SplitAddress(k.Address);
            var mine = orders.Where(o => o.Customer == k.Name && o.Status != "storniert").ToList();
            sb.Append(Row([first, last, k.Email, "no", k.Company, street, "", city, "", street.Length + city.Length > 0 ? "DE" : "", zip, "", "", "no", "no",
                mine.Sum(o => o.Paid).ToString("0.00", Inv), mine.Count.ToString(Inv), k.Notes, "no", k.Stage]));
        }
        AtomicFile.WriteAllText(file, sb.ToString());
        return file;
    }

    /// <summary>„Hauptstraße 1, 10115 Berlin“ → (Hauptstraße 1, 10115, Berlin).</summary>
    public static (string Street, string Zip, string City) SplitAddress(string a)
    {
        var parts = a.Split(',', 2, StringSplitOptions.TrimEntries);
        if (parts.Length < 2) return (a.Trim(), "", "");
        var m = Regex.Match(parts[1], "^(\\d{4,5})\\s+(.+)$");
        return m.Success ? (parts[0], m.Groups[1].Value, m.Groups[2].Value) : (parts[0], "", parts[1]);
    }

    /// <summary>Kunden aus einem Shopify-Kunden-CSV (auch ältere Spalten „Address1“, „City“, „Zip“, „Company“). Gleicher Name → aktualisiert.</summary>
    public ShopImport ImportCustomersCsv(string file)
    {
        var rows = ParseCsv(File.ReadAllText(file, Encoding.UTF8));
        if (rows.Count == 0) throw new ArgumentException("Die Datei ist leer.");
        var c = new Cols(rows[0]);
        if (!c.Has("First Name", "Last Name", "Email")) throw new ArgumentException("Das ist keine Kundenliste im Shopify-Format: „First Name“, „Last Name“ und „Email“ fehlen.");
        var warnings = new List<string>(); var list = new List<BizContact>();
        for (var r = 1; r < rows.Count; r++)
        {
            var row = rows[r];
            var name = $"{c.Get(row, "First Name")} {c.Get(row, "Last Name")}".Trim();
            var mail = c.Get(row, "Email");
            if (name.Length == 0) name = mail;
            if (name.Length == 0) { warnings.Add($"Zeile {r + 1}: ohne Namen und E-Mail – übersprungen."); continue; }
            var street = string.Join(" ", new[] { c.Get(row, "Default Address Address1", "Address1"), c.Get(row, "Default Address Address2", "Address2") }.Where(x => x.Length > 0));
            var zipCity = $"{c.Get(row, "Default Address Zip", "Zip")} {c.Get(row, "Default Address City", "City")}".Trim();
            var addr = string.Join(", ", new[] { street, zipCity }.Where(x => x.Length > 0));
            var stage = c.Get(row, "Tags") is var t && t is "Interessent" or "Angebot" or "Kunde" ? t : "Kunde";
            list.Add(new BizContact(name, c.Get(row, "Default Address Company", "Company"), mail, stage, null, 0, c.Get(row, "Note"), addr));
        }
        var added = 0; var updated = 0;
        Undoable($"{list.Count} Kunden aus CSV laden", () =>
        {
            var all = Customers.All();
            foreach (var k in list)
            {
                var i = all.FindIndex(x => x.Name == k.Name);
                if (i >= 0) { all[i] = k with { Next = all[i].Next, Value = all[i].Value }; updated++; } else { all.Add(k); added++; }
            }
            Customers.Save(all); return true;
        });
        return new(added, updated, warnings);
    }

    /// <summary>Bestellungen wie im Shopify-Bestellexport, eine Zeile je Position; Beträge brutto laut Rechnung, sonst netto.</summary>
    public string ExportOrdersCsv(string file)
    {
        var sb = new StringBuilder(Row(OrderColumns));
        var cust = Customers.All(); var prods = Products.All();
        foreach (var o in Orders().OrderBy(o => o.Date))
        {
            var mail = cust.FirstOrDefault(k => k.Name == o.Customer)?.Email ?? "";
            var fin = o.Status switch { "storniert" => "voided", "erstattet" => "refunded", "bezahlt" => "paid", "teilbezahlt" => "partially_paid", _ => "pending" };
            var ful = o.StockOut == true ? "fulfilled" : "unfulfilled";
            var total = o.InvoiceGross > 0 ? o.InvoiceGross : o.Net;
            for (var k = 0; k < o.Lines.Count; k++)
            {
                var l = o.Lines[k]; var (pi, v) = FindItem(prods, l.Product);
                var sku = pi < 0 ? "" : v >= 0 ? prods[pi].Variants![v].Sku : prods[pi].Sku;
                var first = k == 0;
                sb.Append(Row(["#" + o.Id, first ? mail : "", first ? fin : "", first ? ful : "", "EUR", first ? o.Net.ToString("0.00", Inv) : "", first ? total.ToString("0.00", Inv) : "",
                    o.Date.ToString("yyyy-MM-dd", Inv) + " 00:00:00 +0100", l.Qty.ToString("0.##", Inv), l.Product, l.PriceNet.ToString("0.00", Inv), sku, o.Customer, o.Id]));
            }
        }
        AtomicFile.WriteAllText(file, sb.ToString());
        return file;
    }
}

public sealed partial class Shop
{
    /// <summary>Artikel, die erst nach der Bestellung gedruckt werden (aus „Print on Demand“): kein Lagerbestand nötig.</summary>
    public static bool MadeToOrder(Product p) => p.Supplier.StartsWith("Print on Demand", StringComparison.Ordinal);

    static double Net(decimal gross, bool smallBusiness) => (double)(smallBusiness ? gross : Math.Round(gross / (1 + LegalRates.VatStandard / 100m), 2, MidpointRounding.AwayFromZero));

    /// <summary>T-0753: Ein Produkt aus dem POD-Shop (Printful/Printify) als Artikel übernehmen – mit Varianten, Bild-Adresse und Preis
    /// (der POD-Preis ist brutto inkl. Versand, der Artikel speichert netto). Gibt es den Artikel schon, wird er aktualisiert.</summary>
    public Product TakeOverPod(Pod.PodShopItem item, bool smallBusiness)
    {
        var net = Net(item.Price, smallBusiness);
        var vars = item.Variants.Count > 1 || item.Variants is [{ Name.Length: > 0 }] ? item.Variants.Select(v => new ItemVariant(v.Name, net, 0)).ToList() : null;
        var p = new Product(item.Name, "Print on Demand · " + (item.Provider == "printify" ? "Printify" : "Printful"), 0, net, 0, 0, 0, "",
            "Wird nach deiner Bestellung gedruckt und versandt.", item.Image.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ? item.Image : "", vars);
        return Products.All().Any(x => x.Name == p.Name) ? UpdateProduct(p.Name, p) : AddProduct(p);
    }

    /// <summary>T-0753: Einen eigenen Entwurf aus dem Studio „Print on Demand“ als Artikel übernehmen: die Druckdatei wird das Artikelbild.</summary>
    public Product TakeOverDesign(string title, decimal gross, string description, string? pngFile, bool smallBusiness)
    {
        var name = title.Trim().Replace(VariantSep, " - ");
        var img = pngFile is not null && File.Exists(pngFile) ? ImportImage(pngFile) : "";
        var old = Products.All().FirstOrDefault(x => x.Name == name);
        var p = new Product(name, "Print on Demand · eigener Entwurf", old?.Cost ?? 0, Net(gross, smallBusiness), 0, 0, 0, old?.Sku ?? "",
            description.Trim().Length > 0 ? description.Trim() : "Wird nach deiner Bestellung gedruckt und versandt.", img.Length > 0 ? img : old?.Image ?? "", old?.Variants);
        return old is null ? AddProduct(p) : UpdateProduct(name, p);
    }
}
