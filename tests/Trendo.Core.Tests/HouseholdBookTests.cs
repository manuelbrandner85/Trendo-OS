using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// T-0301: Geld als Haushaltsbuch – Monat, Fixkosten/Abos, Import-Vorschau, Ändern, Regeln.
public class HouseholdBookTests
{
    static readonly DateOnly Today = new(2026, 11, 1);
    static Entry E(int y, int m, int d, string t, decimal a, string c = "Sonstiges") => new(new DateOnly(y, m, d), t, a, c);

    [Fact(DisplayName = "T-0301: Monatsauswahl mit Index, Summen, Sparquote, Ausgaben je Kategorie")]
    public void Month_and_sums()
    {
        List<Entry> all = [E(2026, 11, 1, "Gehalt", 2500, "Einnahmen"), E(2026, 11, 1, "Miete", -800, "Wohnen"), E(2026, 10, 30, "Rewe", -40, "Lebensmittel"), E(2026, 11, 1, "Rewe", -60, "Lebensmittel"), E(2026, 11, 1, "Edeka", -40, "Lebensmittel")];
        var m = HouseholdBook.Month(all, 2026, 11);
        Assert.Equal(4, m.Count); Assert.DoesNotContain(m, x => x.Index == 2); Assert.Equal("Miete", all[m.First(x => x.Entry.Text == "Miete").Index].Text);
        var (inc, exp, left, rate) = HouseholdBook.Sum(m.Select(x => x.Entry));
        Assert.Equal(2500, inc); Assert.Equal(900, exp); Assert.Equal(1600, left); Assert.Equal(0.64, rate, 3);
        Assert.Equal([("Wohnen", 800m), ("Lebensmittel", 100m)], HouseholdBook.ByCategory(m.Select(x => x.Entry)));
        Assert.Equal(0, HouseholdBook.Sum([E(2026, 11, 1, "x", -5)]).SaveRate);
    }

    [Fact(DisplayName = "T-0301: Fixkosten erkannt – monatlich (auch bei kleiner Preiserhöhung), jährlich; schwankende Einkäufe und gekündigte Abos nicht")]
    public void Recurring()
    {
        List<Entry> all = [
            E(2026, 8, 1, "Miete Wohnung", -800, "Wohnen"), E(2026, 9, 1, "Miete Wohnung", -800, "Wohnen"), E(2026, 10, 1, "Miete Wohnung", -840, "Wohnen"),
            E(2026, 9, 15, "Netflix Abo", -13.99m, "Freizeit"), E(2026, 10, 15, "Netflix Abo", -13.99m, "Freizeit"),
            E(2026, 9, 3, "Rewe Markt", -40, "Lebensmittel"), E(2026, 9, 21, "Rewe Markt", -85, "Lebensmittel"), E(2026, 10, 2, "Rewe Markt", -23, "Lebensmittel"),
            E(2026, 1, 5, "Fitness Studio", -30, "Freizeit"), E(2026, 2, 5, "Fitness Studio", -30, "Freizeit"), E(2026, 3, 5, "Fitness Studio", -30, "Freizeit"),
            E(2024, 11, 14, "Haftpflicht Versicherung", -240, "Versicherung"), E(2025, 11, 15, "Haftpflicht Versicherung", -240, "Versicherung"),
            E(2026, 9, 10, "Bäckerei Einkauf", -20, "Lebensmittel"), E(2026, 10, 10, "Bäckerei Einkauf", -21, "Lebensmittel"), E(2026, 10, 25, "Gehalt", 2500, "Einnahmen"), E(2026, 9, 25, "Gehalt", 2500, "Einnahmen")];
        var r = HouseholdBook.FindRecurring(all, Today);
        Assert.Equal(["Miete Wohnung", "Haftpflicht Versicherung", "Netflix Abo"], r.Select(x => x.Name));
        var miete = r[0]; Assert.Equal("monatlich", miete.Rhythm); Assert.Equal(840, miete.Amount); Assert.Equal(new DateOnly(2026, 11, 1), miete.Next); Assert.Equal(3, miete.Count);
        var vers = r[1]; Assert.Equal("jährlich", vers.Rhythm); Assert.Equal(20, vers.PerMonth); Assert.Equal(new DateOnly(2026, 11, 15), vers.Next);
        Assert.Empty(HouseholdBook.FindRecurring([E(2026, 10, 1, "Storno: Miete", -800), E(2026, 9, 1, "Storno: Miete", -800)], Today));
    }

    [Fact(DisplayName = "T-0301: Import-Vorschau – Kategorie geraten (Gelerntes zuerst), schon vorhandene und doppelte Zeilen markiert")]
    public void Preview()
    {
        using var f = new Fixture(); var mem = new CategoryMemory(f.Core.DataRoot); mem.Learn("Wochenmarkt Samstag", "Lebensmittel");
        List<Entry> existing = [E(2026, 10, 1, "Miete", -800, "Wohnen")];
        var p = HouseholdBook.Preview(existing, [E(2026, 10, 1, "Miete", -800), E(2026, 10, 3, "Wochenmarkt Samstag", -12), E(2026, 10, 3, "Wochenmarkt Samstag", -12), E(2026, 10, 4, "Rewe", -30)], mem);
        Assert.Equal([true, false, true, false], p.Select(x => x.Duplicate));
        Assert.Equal("Lebensmittel", p[1].Entry.Category); Assert.Equal("Lebensmittel", p[3].Entry.Category);
    }

    [Fact(DisplayName = "T-0301: Buchung ändern gibt alte Fassung zurück; Regeln auflisten und vergessen")]
    public void Replace_and_rules()
    {
        using var f = new Fixture(); var root = f.Core.DataRoot;
        var l = new Ledger(root); l.Add(E(2026, 10, 1, "Miete", -800, "Wohnen")); l.Add(E(2026, 10, 2, "Rewe", -50, "Lebensmittel"));
        var i = l.All().FindIndex(e => e.Text == "Rewe");
        var old = l.Replace(i, l.All()[i] with { Amount = -55.10m });
        Assert.Equal(-50, old.Amount); Assert.Contains(l.All(), e => e.Text == "Rewe" && e.Amount == -55.10m);
        Assert.Throws<ArgumentException>(() => l.Replace(9, old));
        var mem = new CategoryMemory(root); mem.Learn("Stadtwerke Abschlag", "Wohnen"); mem.Learn("Apotheke am Markt", "Gesundheit");
        Assert.Equal([("apotheke markt", "Gesundheit"), ("stadtwerke abschlag", "Wohnen")], mem.All());
        mem.Forget("apotheke markt"); Assert.Single(mem.All()); Assert.Null(mem.Lookup("Apotheke am Markt"));
    }
}
