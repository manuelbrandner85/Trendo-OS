using System.Diagnostics;
using System.Text;
using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0753 Viertprüfung M-D: Rückgängig bleibt bei 10 000 Bons klein und schnell.</summary>
public class ShopEditPerfTests
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-t0753p-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }

    static long UndoBytes(Shop s)
    {
        var dir = Path.GetDirectoryName(s.Products.FilePath)!;
        return Directory.EnumerateFileSystemEntries(dir, "rueckgaengig*").Sum(e => Directory.Exists(e) ? Directory.EnumerateFiles(e, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length) : new FileInfo(e).Length);
    }

    [Fact(DisplayName = "M-D: 100 Artikel, 10 000 Bons – 20× Anlegen je unter 200 ms, Import 100 Artikel in wenigen Sekunden, Rückgängig nur wenige MB")]
    public void Undo_scales_with_many_bons()
    {
        var d = Tmp();
        try
        {
            var s = new Shop(d, "i");
            for (var i = 0; i < 100; i++) s.AddProduct(new Product($"Artikel {i}", "Lieferant", 1, 2.5, 50, 0, 0, $"A-{i}", "Beschreibung"));
            var prods = s.Products.All();
            var rnd = new Random(7);
            var sales = Enumerable.Range(0, 10_000).Select(n =>
            {
                var p = prods[rnd.Next(prods.Count)];
                return new CashSale(n.ToString("x8"), $"BON-{n:00000}", new DateTime(2026, 11, 1).AddMinutes(n), [new CashLine(p.Name, 1, 2.5m, p.Id), new CashLine(prods[rnd.Next(prods.Count)].Name, 2, 2.5m, prods[rnd.Next(prods.Count)].Id)], "bar", 10, 0);
            }).ToList();
            s.CashSales.Save(sales);
            var kasseMb = new FileInfo(s.CashSales.FilePath).Length / 1024.0 / 1024.0;

            var sw = Stopwatch.StartNew();
            for (var i = 0; i < 20; i++) s.AddProduct(new Product($"Neu {i}", "", 0, 3, 5, 0, 0));
            sw.Stop();
            var perStep = sw.Elapsed.TotalMilliseconds / 20;

            var csv = new StringBuilder("Title,Price,Inventory quantity\n");
            for (var i = 0; i < 100; i++) csv.Append($"Artikel {i},{i % 9 + 1}.50,{i % 7}\n");
            var f = Path.Combine(d, "a.csv"); File.WriteAllText(f, csv.ToString());
            var sw2 = Stopwatch.StartNew();
            var r = s.ImportProductsCsv(f);
            sw2.Stop();
            var undoMb = UndoBytes(s) / 1024.0 / 1024.0;
            Assert.Equal(100, r.Updated);
            Assert.Equal("100 Artikel aus CSV laden", s.UndoLabel);
            Assert.Equal("100 Artikel aus CSV laden", s.Undo());
            Assert.Equal(2.5, s.Products.All()[0].Price);

            var report = $"Bons: 10000, kasse.json: {kasseMb:0.0} MB | 20× Artikel anlegen: {sw.Elapsed.TotalMilliseconds:0} ms ({perStep:0} ms je Schritt) | Import 100 Artikel: {sw2.Elapsed.TotalMilliseconds:0} ms | Rückgängig-Daten: {undoMb:0.00} MB";
            if (Environment.GetEnvironmentVariable("TRENDO_BELEG_DIR") is { Length: > 0 } dir) { Directory.CreateDirectory(dir); File.AppendAllText(Path.Combine(dir, "leistung-rueckgaengig.txt"), (Environment.GetEnvironmentVariable("TRENDO_BELEG_TAG") ?? "") + " " + report + "\n"); }
            Assert.True(perStep < 200, report);
            Assert.True(sw2.Elapsed < TimeSpan.FromSeconds(5), report);
            Assert.True(undoMb < 10, report);
        }
        finally { Directory.Delete(d, true); }
    }
}
