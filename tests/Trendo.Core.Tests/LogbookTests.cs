using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// T-0311: Auto als Fahrtenbuch – Fahrtart, Ziel/Partner, Korrektur und Storno mit Grund, Festschreiben, Monatssummen, häufige Fahrten, PDF.
public class LogbookTests
{
    static VehicleBook Book(Fixture f) { var b = new VehicleBook(f.Core.DataRoot); b.Add("Golf", "B-EM 26"); return b; }
    static readonly DateTime Okt = new(2026, 10, 5), Nov = new(2026, 11, 3);

    [Fact(DisplayName = "T-0311: Eintragen mit Art, Ziel, Zweck, Partner; dienstlich ohne Zweck abgewiesen; alte Einträge ohne Art bleiben lesbar; Monatssummen")]
    public void Log_and_months()
    {
        using var f = new Fixture(); var b = Book(f);
        b.Log("Golf", Okt, 1000, 1042, "dienstlich", "Augsburg, Ring 5", "Kundentermin", "Beispiel AG");
        b.Log("Golf", Okt.AddDays(1), 1042, 1060, "arbeitsweg");
        b.Log("Golf", Nov, 1060, 1100, "privat");
        b.Drive("Golf", Nov.AddDays(1), 1100, 1110, true, "Altes Format");
        Assert.Throws<ArgumentException>(() => b.Log("Golf", Nov, 1200, 1210, "dienstlich"));
        Assert.Throws<ArgumentException>(() => b.Log("Golf", Nov, 1200, 1210, "urlaub"));
        var v = b.Get("Golf"); Assert.Equal("dienstlich", v.Trips[^1].Art); Assert.Equal("Beispiel AG", v.Trips[0].Partner);
        var m = VehicleBook.Months(v, 2026);
        Assert.Equal([(11, 10.0, 40.0, 0.0), (10, 42.0, 0.0, 18.0)], m.Select(x => (x.Month, x.Business, x.Private, x.Commute)));
        Assert.Equal((52.0, 58.0), (VehicleBook.TripShare(v, 2026).Business, VehicleBook.TripShare(v, 2026).Private));
        Assert.Contains("Augsburg, Ring 5;Beispiel AG", VehicleBook.LogCsv(v));
    }

    [Fact(DisplayName = "T-0311: Korrektur nur mit Grund und mit Vermerk alt → neu; Storno bleibt sichtbar, zählt nicht und gibt die km frei; Festschreiben sperrt Änderungen und Nachträge")]
    public void Correct_void_lock()
    {
        using var f = new Fixture(); var b = Book(f);
        var t = b.Log("Golf", Okt, 1000, 1042, "dienstlich", "Augsburg", "Kundentermin");
        Assert.Throws<ArgumentException>(() => b.Correct("Golf", t, " ", end: 1045));
        b.Correct("Golf", t, "Tacho falsch abgelesen", end: 1045, partner: "Beispiel AG");
        Assert.Equal(45, t.Km); var note = Assert.Single(t.Corrections!); Assert.Contains("km: 1000–1042 → 1000–1045", note); Assert.Contains("Partner:  → Beispiel AG", note); Assert.Contains("Grund: Tacho falsch abgelesen", note);
        var p = b.Log("Golf", Okt.AddDays(2), 1045, 1080, "privat");
        b.Void("Golf", p, "doppelt eingetragen");
        Assert.True(p.Voided); Assert.Equal(0, VehicleBook.TripShare(b.Get("Golf"), 2026).Private);
        b.Log("Golf", Okt.AddDays(2), 1045, 1070, "privat"); // stornierte km sind wieder frei
        Assert.Equal(3, b.LockMonth("Golf", 2026, 10)); // auch die stornierte Fahrt wird mit festgeschrieben
        Assert.Throws<InvalidOperationException>(() => b.Correct("Golf", t, "zu spät", end: 1050));
        Assert.Throws<InvalidOperationException>(() => b.Void("Golf", t, "zu spät"));
        Assert.Throws<InvalidOperationException>(() => b.Log("Golf", Okt.AddDays(20), 1070, 1080, "privat"));
        Assert.True(VehicleBook.Months(b.Get("Golf"), 2026).Single().Locked);
        Assert.Contains("storniert: doppelt eingetragen", VehicleBook.LogCsv(new VehicleBook(f.Core.DataRoot).Get("Golf")));
    }

    [Fact(DisplayName = "T-0311: Häufige Fahrten mit üblicher Strecke; Fahrtenbuch-PDF mit allen Seiten")]
    public void Frequent_and_pdf()
    {
        using var f = new Fixture(); var b = Book(f); var km = 1000.0;
        for (var i = 0; i < 3; i++) { b.Log("Golf", Okt.AddDays(i), km, km + 42, "dienstlich", "Büro Augsburg", "Teamtreffen", "Beispiel AG"); km += 42; }
        for (var i = 0; i < 2; i++) { b.Log("Golf", Okt.AddDays(5 + i), km, km + 18, "arbeitsweg", "Werk Süd"); km += 18; }
        b.Log("Golf", Okt.AddDays(9), km, km + 5, "privat", "Bäcker"); km += 5;
        var fr = VehicleBook.Frequent(b.Get("Golf"));
        Assert.Equal([("Büro Augsburg", 42.0, 3, "dienstlich"), ("Werk Süd", 18.0, 2, "arbeitsweg")], fr.Select(x => (x.Destination, x.Km, x.Count, x.Kind)));
        for (var i = 0; i < 60; i++) { b.Log("Golf", Nov.AddDays(i % 25), km, km + 3, "privat"); km += 3; }
        var pdf = Path.Combine(f.Core.DataRoot, "fahrtenbuch.pdf"); VehicleBook.LogPdf(pdf, b.Get("Golf"), 2026);
        Assert.StartsWith("%PDF", File.ReadAllText(pdf)[..4]); Assert.True(Trendo.Core.Sys.PdfTools.Read(pdf).Pages >= 2);
    }
}
