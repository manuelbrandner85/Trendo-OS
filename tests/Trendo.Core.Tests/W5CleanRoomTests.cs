using Trendo.Core;
using Xunit;

public class W5_X10_CleanRoom
{
    const string Orig = """
// Original: summiert Bestellungen
public static decimal Total(List<Order> orders) {
    decimal sum = 0;
    foreach (var o in orders) { if (o.Paid && !o.Cancelled) { sum += o.Price * o.Qty; } }
    return sum > 1000 ? sum * 0.95m : sum;
}
""";
    const string Renamed = """
/* meine Version */
public static decimal Gesamt(List<Auftrag> liste) {
    decimal s = 0;
    foreach (var a in liste) { if (a.Bezahlt && !a.Storniert) { s += a.Preis * a.Menge; } }
    return s > 5000 ? s * 0.90m : s;
}
""";
    const string Own = """
def gesamt(auftraege):
    bezahlt = [a for a in auftraege if a["bezahlt"]]
    summe = sum(a["preis"] * a["menge"] for a in bezahlt)
    return summe * 0.95 if summe > 1000 else summe
""";

    [Fact(DisplayName = "X-10: Ähnlichkeit erkennt kopierten Code trotz Umbenennen, eigener Code bleibt unauffällig")]
    public void Similarity()
    {
        Assert.True(CleanRoom.Similarity(Orig, Renamed) > 0.8);
        Assert.True(CleanRoom.Similarity(Orig, Own) < 0.1);
        Assert.Equal(0, CleanRoom.Similarity("", Orig));
        Assert.DoesNotContain(CleanRoom.Tokens("// Kommentar\nx = \"text\""), t => t.Contains("Kommentar") || t.Contains("text"));
    }

    [Fact(DisplayName = "X-10: Projekt mit Rollen-Ordnern, Tagebuch, Vergleich zweier Ordner und Prüfbericht")]
    public void Project()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-cr-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        var d = CleanRoom.Create(root, "Rechner", "Ein Kassenprogramm");
        Assert.True(File.Exists(Path.Combine(d, "1-Beschreibung", "BESCHREIBUNG.md")));
        CleanRoom.Log(d, "Beschreiben", "Verhalten beschrieben"); CleanRoom.Log(d, "Umsetzen", "Nur Beschreibung gelesen");
        Assert.Throws<ArgumentException>(() => CleanRoom.Log(d, "Chef", "x"));
        Assert.Equal(3, CleanRoom.Journal(d).Count);
        var theirs = Path.Combine(root, "original"); Directory.CreateDirectory(theirs); File.WriteAllText(Path.Combine(theirs, "Orders.cs"), Orig);
        File.WriteAllText(Path.Combine(d, "2-Umsetzung", "kopie.cs"), Renamed); File.WriteAllText(Path.Combine(d, "2-Umsetzung", "eigen.py"), Own);
        var s = CleanRoom.Compare(Path.Combine(d, "2-Umsetzung"), theirs);
        Assert.Single(s); Assert.Equal("kopie.cs", s[0].Ours);
        var rep = File.ReadAllText(CleanRoom.Report(d, s, theirs));
        Assert.Contains("kopie.cs", rep); Assert.Contains("Beide Rollen", rep); Assert.Contains("keine Rechtsberatung", rep);
    }
}
