using System.IO.Compression;
using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Teil 4, Business & Geld: Haushaltsbuch, gelernte Kategorien, Budgets, Steuerberater-Export, Logo, Shop.</summary>
public class Teil4BusinessTests
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-biz-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }

    [Fact(DisplayName = "SPEC-11 F1/F2: Haushalts-Kategorien erkennen; Zuordnung des Nutzers wird gelernt und hat Vorrang")]
    public void Categories_learned()
    {
        var d = Tmp();
        try
        {
            Assert.Equal("Lebensmittel", FinanceExtra.Guess("REWE Markt Berlin"));
            Assert.Equal("Wohnen", FinanceExtra.Guess("Miete Oktober"));
            Assert.Equal("Software", FinanceExtra.Guess("GitHub Inc."));
            var mem = new CategoryMemory(d);
            Assert.Equal("Sonstiges", FinanceExtra.Guess("Hundesalon Bello", mem));
            mem.Learn("Hundesalon Bello", "Gesundheit");
            Assert.Equal("Gesundheit", FinanceExtra.Guess("Hundesalon Bello 12.10.", mem));
            var e = FinanceExtra.Categorize([new(new DateOnly(2026, 10, 1), "Hundesalon Bello", -80m), new(new DateOnly(2026, 10, 2), "Gehalt", 2000m, "Einnahmen")], mem);
            Assert.Equal(["Gesundheit", "Einnahmen"], e.Select(x => x.Category));
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "SPEC-11 U1/F4: Monatswerte, Saldo, Budget mit Warnstufen")]
    public void Months_and_budgets()
    {
        var d = Tmp();
        try
        {
            List<Entry> all = [new(new DateOnly(2026, 10, 1), "Gehalt", 2000m, "Einnahmen"), new(new DateOnly(2026, 10, 3), "REWE", -180m, "Lebensmittel"), new(new DateOnly(2026, 10, 9), "Aldi", -40m, "Lebensmittel"), new(new DateOnly(2026, 9, 9), "Kino", -20m, "Freizeit")];
            var m = FinanceExtra.YearMonths(all, 2026);
            Assert.Equal((2000m, 220m), m[9]); Assert.Equal((0m, 20m), m[8]);
            Assert.Equal(1760m, FinanceExtra.Balance(all));
            var b = new BudgetStore(d); b.Set("Lebensmittel", 240m); b.Set("Freizeit", 50m);
            var s = b.Status(all, 2026, 10);
            Assert.Equal("knapp", s.Single(x => x.Category == "Lebensmittel").Level);
            Assert.Equal("ok", s.Single(x => x.Category == "Freizeit").Level);
            b.Set("Lebensmittel", 200m); Assert.Equal("über", b.Status(all, 2026, 10).First().Level);
            b.Set("Freizeit", 0); Assert.Single(b.All());
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "SPEC-11 F3/F5: Logo in der Rechnung, ZIP für den Steuerberater mit Buchungen, Übersicht und Rechnungen")]
    public void Logo_and_tax_export()
    {
        var d = Tmp();
        try
        {
            var logo = Path.Combine(d, "logo.png"); File.WriteAllBytes(logo, [0x89, 0x50, 0x4E, 0x47]);
            Assert.Contains("data:image/png;base64,iVBORw", FinanceExtra.WithLogo("<html><body>x</body></html>", logo));
            Assert.Equal("<body>", FinanceExtra.WithLogo("<body>", Path.Combine(d, "fehlt.png")));
            var l = new Ledger(d);
            l.Add(new(new DateOnly(2026, 3, 1), "Rechnung RE-2026-001", 119m, "Einnahmen", 19m)); l.Add(new(new DateOnly(2025, 3, 1), "Alt", -5m, "Büro"));
            l.WriteInvoice(new Invoice("RE-2026-001", new DateOnly(2026, 3, 1), "A", "B", [new("X", 1, 100)], 19m, false));
            var zip = FinanceExtra.TaxExport(l.All(), l.InvoiceDir, 2026, Path.Combine(d, "out"));
            using var z = ZipFile.OpenRead(zip);
            Assert.Equal(["Buchungen-2026.csv", "LIESMICH.txt", "Rechnungen/RE-2026-001.html", "Uebersicht-2026.csv"], z.Entries.Select(e => e.FullName).Order());
            var csv = new StreamReader(z.GetEntry("Buchungen-2026.csv")!.Open()).ReadToEnd();
            Assert.DoesNotContain("Alt", csv);
            Assert.DoesNotContain("Trendo", new StreamReader(z.GetEntry("LIESMICH.txt")!.Open()).ReadToEnd());
        }
        finally { Directory.Delete(d, true); }
    }

    // 105 (Entscheidung Manuel 31.10.2026): Bestand sinkt erst bei Lieferung, Einnahme erst bei Zahlung – Test bewusst angepasst
    [Fact(DisplayName = "Online verkaufen F1/F2/U1/F3 + 105: Bestellung merkt vor, Lieferung senkt Bestand, Rechnung ohne Buchung, Zahlung bucht Einnahme, Kennzahlen, Shop-Seite")]
    public void Shop_flow()
    {
        var d = Tmp();
        try
        {
            var s = new Shop(d, "idee1");
            s.Customers.Add(new BizContact("Erika Muster", "", "erika@example.org", "Interessent", null, 0, "", "Hauptstraße 1, 10115 Berlin"));
            s.Products.Add(new Product("Kaffee 500 g", "", 4, 9.5, 10, 0, 0));
            var o = s.AddOrder("Erika Muster", new DateOnly(2026, 10, 27), ("Kaffee 500 g", 3));
            Assert.Equal(28.5m, o.Net);
            Assert.Equal(10, s.Products.All()[0].Stock); // nur vorgemerkt
            Assert.Equal(3m, s.Reserved("Kaffee 500 g")); Assert.Equal(7m, s.Available("Kaffee 500 g"));
            Assert.Equal("Kunde", s.Customers.All()[0].Stage);
            Assert.Throws<ArgumentException>(() => s.AddOrder("Niemand", new DateOnly(2026, 10, 27), ("Kaffee 500 g", 1)));
            s.Deliver(o.Id, new DateOnly(2026, 10, 28));
            Assert.Equal(7, s.Products.All()[0].Stock); Assert.Equal(0m, s.Reserved("Kaffee 500 g"));
            Assert.Equal(-3m, Assert.Single(s.Moves.All()).Qty);
            Assert.Throws<InvalidOperationException>(() => s.Deliver(o.Id, new DateOnly(2026, 10, 28)));
            var ledger = new Ledger(d);
            var (inv, file) = s.Invoice(o.Id, ledger, "Muster Kaffee\nWeg 1\n12345 Ort", smallBusiness: true);
            Assert.Equal("RE-2026-001", inv.Number);
            Assert.Contains("10115 Berlin", File.ReadAllText(file));
            Assert.Empty(ledger.All()); // Rechnung ist noch keine Einnahme
            Assert.Equal("berechnet", s.Orders()[0].Status); Assert.Equal(28.5m, s.Orders()[0].Open);
            Assert.Throws<InvalidOperationException>(() => s.Invoice(o.Id, ledger, "x", true));
            Assert.Equal((0m, 0m, 1, 0), s.Figures(new DateOnly(2026, 10, 30)));
            s.Pay(o.Id, 28.5m, new DateOnly(2026, 10, 29), "Konto 29.10.", ledger, smallBusiness: true);
            Assert.Equal(28.5m, Assert.Single(ledger.All()).Amount);
            Assert.Equal("bezahlt", s.Orders()[0].Status);
            var f = s.Figures(new DateOnly(2026, 10, 30));
            Assert.Equal((28.5m, 28.5m, 1, 0), f);
            Assert.Equal(28.5m, s.MonthRevenue(2026)[9]);
            var html = Shop.ShopHtml("Muster Kaffee", s.Products.All(), smallBusiness: false);
            Assert.Contains("11,31 €", html); Assert.Contains("7 vorrätig", html); Assert.DoesNotContain("Trendo", html);
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "105-05/105-22: Rechnung 100 €, Zahlung 60 € → offen 40 €; gleiche Zahlung nochmal ändert nichts; Storno vor Lieferung ohne Bestand, nach Lieferung mit Gegenbuchung")]
    public void Payments_and_stock()
    {
        var d = Tmp();
        try
        {
            var s = new Shop(d, "idee1");
            s.Customers.Add(new BizContact("Erika Muster", "", "", "Kunde", null, 0, "", "Weg 1, 12345 Ort"));
            s.Products.Add(new Product("Tasche", "", 0, 100, 4, 0, 0));
            var ledger = new Ledger(d);
            var o = s.AddOrder("Erika Muster", new DateOnly(2026, 11, 2), ("Tasche", 1));
            s.Invoice(o.Id, ledger, "Händler", smallBusiness: true);
            s.Pay(o.Id, 60m, new DateOnly(2026, 11, 3), "Bank-001", ledger, true);
            Assert.Equal(40m, s.Orders().Single(x => x.Id == o.Id).Open);
            Assert.Equal("teilbezahlt", s.Orders().Single(x => x.Id == o.Id).Status);
            s.Pay(o.Id, 60m, new DateOnly(2026, 11, 3), "Bank-001", ledger, true); // gleicher Import nochmal
            Assert.Equal(40m, s.Orders().Single(x => x.Id == o.Id).Open);
            Assert.Single(ledger.All());
            Assert.Throws<ArgumentException>(() => s.Pay(o.Id, 50m, new DateOnly(2026, 11, 4), "Bank-002", ledger, true)); // mehr als offen
            Assert.Throws<InvalidOperationException>(() => s.Cancel(o.Id, new DateOnly(2026, 11, 4))); // schon Geld eingegangen
            Assert.Equal(40m, s.OpenReceivables());

            // Storno vor Lieferung: Bestand bleibt, Vormerkung weg
            var a = s.AddOrder("Erika Muster", new DateOnly(2026, 11, 5), ("Tasche", 2));
            Assert.Equal(3m, s.Reserved("Tasche"));
            s.Cancel(a.Id, new DateOnly(2026, 11, 5));
            Assert.Equal(4, s.Products.All()[0].Stock); Assert.Equal(1m, s.Reserved("Tasche"));
            Assert.Throws<InvalidOperationException>(() => s.Deliver(a.Id, new DateOnly(2026, 11, 6)));
            // Storno nach Lieferung: Gegenbewegung, Bestand wieder da
            var b = s.AddOrder("Erika Muster", new DateOnly(2026, 11, 6), ("Tasche", 2));
            s.Deliver(b.Id, new DateOnly(2026, 11, 6));
            Assert.Equal(2, s.Products.All()[0].Stock);
            s.Cancel(b.Id, new DateOnly(2026, 11, 7));
            Assert.Equal(4, s.Products.All()[0].Stock);
            Assert.Equal(0m, s.Moves.All().Where(m => m.OrderId == b.Id).Sum(m => m.Qty)); // ausgeglichen
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "105: Bestellungen aus älteren Versionen (Bestand schon gesenkt, Einnahme schon gebucht) werden nicht doppelt gebucht")]
    public void Legacy_orders()
    {
        var d = Tmp();
        try
        {
            var s = new Shop(d, "idee1");
            s.Customers.Add(new BizContact("Erika Muster", "", "", "Kunde", null, 0, "", ""));
            s.Products.Add(new Product("Tasche", "", 0, 10, 3, 0, 0));
            s.OrderList.Add(new ShopOrder("alt1", new DateOnly(2026, 10, 1), "Erika Muster", [new("Tasche", 1, 10)], "berechnet", "RE-2026-009"));
            var ledger = new Ledger(d);
            Assert.Throws<InvalidOperationException>(() => s.Deliver("alt1", new DateOnly(2026, 10, 2)));
            Assert.Throws<InvalidOperationException>(() => s.Pay("alt1", 10m, new DateOnly(2026, 10, 2), "x", ledger, true));
            Assert.Equal(3, s.Products.All()[0].Stock);
            Assert.Empty(ledger.All());
            Assert.Equal(0m, s.Reserved("Tasche"));
            Assert.Equal(10m, s.Figures(new DateOnly(2026, 10, 15)).Month); // Umsatz wie bisher zum Bestelldatum
        }
        finally { Directory.Delete(d, true); }
    }
}
