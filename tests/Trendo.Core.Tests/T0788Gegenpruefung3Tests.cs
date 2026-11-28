using System.Text;
using System.Text.Json.Nodes;
using Trendo.Core.Wissen;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0788, 2. Prüfrunde (Kern): transitive Verweise beim Ausblenden, Einsender ohne Kontaktdaten, v. Chr. im deutschen Datum,
/// Generationen für die KI gesperrt, doppelte Kennung ohne stille Familienverknüpfung, keine neue Kennung, die schon als Verweis vorkommt.</summary>
public class T0788Gegenpruefung3Tests
{
    static readonly DateTime Now = T0788Fixtures.Now;
    static FamilyTree Read(string ged) => FamilyKit.FromGedcom(Encoding.UTF8.GetBytes(ged));

    [Fact(DisplayName = "T-0788 R2 MITTEL: Ausblenden entfernt auch, was nur über ausgeblendete Datensätze erreichbar war (Lebende → NOTE → SOUR „Meldeakte Erika“)")]
    public void Private_transitive()
    {
        var t = Read("0 HEAD\r\n1 CHAR UTF-8\r\n0 @I1@ INDI\r\n1 NAME Erika /Muster/\r\n1 BIRT\r\n2 DATE 1990\r\n1 NOTE @N1@\r\n"
            + "0 @I2@ INDI\r\n1 NAME Max /Muster/\r\n1 BIRT\r\n2 DATE 1850\r\n1 DEAT\r\n2 DATE 1920\r\n1 SOUR @S1@\r\n"
            + "0 @N1@ NOTE Erika ist umgezogen\r\n1 SOUR @S9@\r\n2 PAGE Blatt 4\r\n0 @S9@ SOUR\r\n1 TITL Meldeakte Erika\r\n1 REPO @R9@\r\n0 @R9@ REPO\r\n1 NAME Einwohnermeldeamt Musterstadt\r\n"
            + "0 @S1@ SOUR\r\n1 TITL Kirchenbuch\r\n0 TRLR\r\n");
        var outp = FamilyKit.ToGedcom(t, hideLiving: true, now: Now);
        foreach (var gone in new[] { "Meldeakte", "@S9@", "@N1@", "umgezogen", "Einwohnermeldeamt", "@R9@" }) Assert.DoesNotContain(gone, outp);
        Assert.Contains("1 TITL Kirchenbuch", outp);
    }

    [Fact(DisplayName = "T-0788 R2: Einsender (SUBM) – bei „Lebende ausblenden“ ohne Adresse, Telefon, E-Mail; Name bleibt")]
    public void Subm_contact_removed()
    {
        var t = Read("0 HEAD\r\n1 CHAR UTF-8\r\n1 SUBM @U1@\r\n0 @U1@ SUBM\r\n1 NAME Erika Muster\r\n1 ADDR Gartenstraße 5\r\n2 CITY Musterstadt\r\n1 PHON 0123 456789\r\n1 EMAIL erika@example.org\r\n"
            + "0 @I1@ INDI\r\n1 NAME Max /Muster/\r\n1 DEAT Y\r\n0 TRLR\r\n");
        var outp = FamilyKit.ToGedcom(t, hideLiving: true, now: Now);
        Assert.Contains("0 @U1@ SUBM\r\n1 NAME Erika Muster\r\n", outp);
        foreach (var gone in new[] { "Gartenstraße", "0123", "erika@example.org", "Musterstadt" }) Assert.DoesNotContain(gone, outp);
        Assert.Contains("erika@example.org", FamilyKit.ToGedcom(t, hideLiving: false, now: Now));
        Assert.DoesNotContain("Gartenstraße", FamilyKit.ToJson(FamilyKit.Private(t, Now)).ToJsonString()); // dieselbe Kürzung in der KI-Sicht
    }

    [Theory(DisplayName = "T-0788 R2: deutsches Datum v. Chr. – Tag erst nach v. Chr. (julianisch) prüfen")]
    [InlineData("29.02.45 v. Chr.", "29 FEB 45 B.C.")]
    [InlineData("29. Februar 45 v. Chr.", "29 FEB 45 B.C.")]
    [InlineData("28.02.44 v. Chr.", "28 FEB 44 B.C.")]
    public void German_bc_ok(string input, string ged) => Assert.Equal(ged, GenDate.FromInput(input));

    [Theory(DisplayName = "T-0788 R2: deutsches Datum v. Chr. – ungültige Tage werden abgelehnt")]
    [InlineData("29.02.44 v. Chr.")]
    [InlineData("31.02.45 v. Chr.")]
    public void German_bc_bad(string input) => Assert.Throws<ArgumentException>(() => GenDate.FromInput(input));

    [Fact(DisplayName = "T-0788 R2: KI darf die Zahl der Generationen nicht ändern")]
    public void Ki_gens_locked()
    {
        var before = FamilyKit.ToJson(FamilyKit.FromGedcom(T0788Fixtures.Bytes));
        var after = (JsonObject)before.DeepClone(); after["gens"] = 6;
        Assert.NotNull(FamilyKit.KiProblem(before, after));
    }

    [Fact(DisplayName = "T-0788 R2: doppelte Kennung – der umbenannte zweite Datensatz hängt nicht still in der Familie des ersten; neue Kennung kollidiert nicht mit verwaisten Verweisen")]
    public void Duplicate_not_linked()
    {
        var t = Read("0 HEAD\r\n1 CHAR UTF-8\r\n0 @I1@ INDI\r\n1 NAME Max /Muster/\r\n1 SEX M\r\n1 FAMS @F1@\r\n0 @I1@ INDI\r\n1 NAME Erika /Beispiel/\r\n1 SEX F\r\n1 FAMS @F1@\r\n"
            + "0 @F1@ FAM\r\n1 HUSB @I1@\r\n1 CHIL @I3@\r\n0 TRLR\r\n"); // @I3@ wird nur verwiesen (verwaist)
        var erika = t.Persons.Single(p => p.Given == "Erika");
        Assert.NotEqual("I3", erika.Id); Assert.NotEqual("I1", erika.Id);
        var f = t.Families.Single();
        Assert.Equal("I1", f.Husband); Assert.Null(f.Wife); // nicht still als Ehefrau eingehängt
        Assert.Contains("gelöst", t.ImportNote + string.Join(" ", FamilyKit.Check(t).Select(i => i.Text)) + ImportProblems(t));
    }

    static string ImportProblems(FamilyTree t) => t.ImportNote;
}
