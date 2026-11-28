using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using Trendo.Core.Wissen;
using Xunit;
using Xunit.Abstractions;

namespace Trendo.Core.Tests;

/// <summary>T-0788 Gegenprüfung (Funde 2–9): Laufzeit großer Stammbäume, CONC linear, „lebt noch?“ im Zweifel ja, Private() ohne Datensätze Lebender,
/// doppelte und überlange Kennungen, KI-Prüfung strenger, Datumsgrenzfälle, CSV-Formeln, Zeilenumbruch an Kombinationszeichen.</summary>
public class T0788GegenpruefungTests(ITestOutputHelper output)
{
    static readonly DateTime Now = T0788Fixtures.Now;

    static FamPerson Person(string id, string given, string sur, string sex, string birth = "", string death = "")
    {
        var p = new FamPerson { Id = id, Sex = sex }; FamilyKit.SetName(p, given, sur);
        if (birth.Length > 0) p.Events.Add(new FamEvent { Tag = "BIRT", Date = birth, Place = "Ort" + id });
        if (death.Length > 0) p.Events.Add(new FamEvent { Tag = "DEAT", Date = death });
        return p;
    }

    /// <summary>50 000 Personen: 25 000 Paare, jedes Paar hat zwei Kinder, die das nächste Paar bilden (tiefe Linie) – Worst case für Suchen über Listen.</summary>
    static FamilyTree Big(int n)
    {
        var t = new FamilyTree();
        for (var i = 0; i < n; i++) t.Persons.Add(Person("I" + i, "Vorname" + i, "Muster", i % 2 == 0 ? "M" : "F", (1000 + i / 2 % 900).ToString(), (1080 + i / 2 % 900).ToString()));
        for (var k = 0; k + 1 < n / 2; k++)
            t.Families.Add(new FamFamily { Id = "F" + k, Husband = "I" + (2 * k), Wife = "I" + (2 * k + 1), Children = [.. new[] { 2 * k + 2, 2 * k + 3 }.Where(c => c < n).Select(c => "I" + c)] });
        return t;
    }

    [Fact(DisplayName = "T-0788 G2: Prüfung, Lebende und Baum für 50 000 Personen in deutlich unter einer Sekunde (gemessen wird nur die Arbeit, Grenze großzügig 5 s)")]
    public void Big_tree_fast()
    {
        var t = Big(50_000);
        var sw = Stopwatch.StartNew(); var issues = FamilyKit.Check(t); var check = sw.Elapsed;
        sw.Restart(); var priv = FamilyKit.Private(t, Now); var p = sw.Elapsed;
        sw.Restart(); var anc = FamilyKit.Ancestors(t, "I49999", 6); var desc = FamilyKit.Descendants(t, "I0", 6); var lay = sw.Elapsed;
        sw.Restart(); var ged = FamilyKit.ToGedcom(t, false, Now); var back = FamilyKit.FromGedcom(Encoding.UTF8.GetBytes(ged)); var io = sw.Elapsed;
        output.WriteLine($"Prüfung {check.TotalMilliseconds:0} ms, Private {p.TotalMilliseconds:0} ms, Bäume {lay.TotalMilliseconds:0} ms, GEDCOM hin und zurück {io.TotalMilliseconds:0} ms");
        Assert.True(check < TimeSpan.FromSeconds(5), $"Prüfung {check}");
        Assert.True(p < TimeSpan.FromSeconds(5), $"Private {p}");
        Assert.True(lay < TimeSpan.FromSeconds(5), $"Bäume {lay}");
        Assert.True(io < TimeSpan.FromSeconds(20), $"GEDCOM {io}");
        Assert.Equal(50_000, back.Persons.Count); Assert.Equal(50_000, priv.Persons.Count);
        Assert.Contains("I49996", anc.Hits.Keys); Assert.Contains("I49997", anc.Hits.Keys); Assert.Contains("I2", desc.Hits.Keys);
        Assert.DoesNotContain(issues, i => i.Text.Contains("Vorfahre"));
    }

    [Fact(DisplayName = "T-0788 G2: Kreis in den Verknüpfungen wird einmal erkannt (alle Personen auf dem Kreis), auch in einer Linie mit 5 000 Generationen schnell")]
    public void Cycle_once()
    {
        var t = Big(10_000);
        t.Families.Add(new FamFamily { Id = "FX", Wife = "I9999", Children = ["I0"] }); // die letzte Person wird Mutter der ersten
        var sw = Stopwatch.StartNew(); var issues = FamilyKit.Check(t); sw.Stop();
        output.WriteLine($"Prüfung mit Kreis: {sw.ElapsedMilliseconds} ms");
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5), sw.Elapsed.ToString());
        var loop = issues.Where(i => i.Text.Contains("eigener Vorfahre")).Select(i => i.Person).ToHashSet();
        Assert.Contains("I0", loop); Assert.Contains("I9999", loop); Assert.Contains("I5000", loop);
    }

    [Fact(DisplayName = "T-0788 G3: CONC wird linear zusammengesetzt (30 000 Fortsetzungszeilen, Grenze großzügig 3 s)")]
    public void Conc_linear()
    {
        var sb = new StringBuilder("0 HEAD\r\n1 CHAR UTF-8\r\n0 @I1@ INDI\r\n1 NAME Erika /Muster/\r\n1 NOTE Anfang\r\n");
        for (var i = 0; i < 30_000; i++) sb.Append("2 CONC ").Append('x', 40).Append(i % 10).Append("\r\n");
        sb.Append("0 TRLR\r\n");
        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var sw = Stopwatch.StartNew(); var t = FamilyKit.FromGedcom(bytes); sw.Stop();
        output.WriteLine($"30 000 CONC: {sw.ElapsedMilliseconds} ms");
        Assert.Equal(6 + 30_000 * 41, t.P("I1")!.Note.Length);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(3), sw.Elapsed.ToString());
    }

    [Fact(DisplayName = "T-0788 G4: „lebt noch?“ – AFT/FROM ohne Ende und Schätzung über Eltern gelten im Zweifel als lebend; sichere Schlüsse über Kinder und Heirat")]
    public void Living_conservative()
    {
        var t = new FamilyTree();
        t.Persons.AddRange([Person("I1", "Nach", "Muster", "F", "AFT 1900"), Person("I2", "Ab", "Muster", "M", "FROM 1890"), Person("I3", "Vater", "Muster", "M", "1890"), Person("I4", "Kind", "Muster", "F"),
            Person("I5", "Ur", "Muster", "M", "1800"), Person("I6", "Sohn", "Muster", "M"), Person("I7", "Alt", "Muster", "F"), Person("I8", "Jung", "Muster", "M", "1900"), Person("I9", "Ehe", "Muster", "F")]);
        t.Families.AddRange([new FamFamily { Id = "F1", Husband = "I3", Children = ["I4"] }, new FamFamily { Id = "F2", Husband = "I5", Children = ["I6"] },
            new FamFamily { Id = "F3", Wife = "I7", Children = ["I8"] }, new FamFamily { Id = "F4", Wife = "I9", Events = [new FamEvent { Tag = "MARR", Date = "1890" }] }]);
        bool L(string id) => FamilyKit.IsLiving(t, t.P(id)!, Now);
        Assert.True(L("I1")); // „nach 1900“ geboren – kann jung sein
        Assert.True(L("I2")); // „ab 1890“ ohne Ende
        Assert.True(L("I4")); // Vater 1890 – das Kind kann bis 1950 geboren sein
        Assert.False(L("I6")); // Vater 1800 – Kind spätestens 1860
        Assert.False(L("I7")); // Kind 1900 – Mutter spätestens 1888 geboren
        Assert.False(L("I9")); // Heirat 1890 – spätestens 1878 geboren
    }

    [Fact(DisplayName = "T-0788 G5: Ausblenden entfernt NOTE-, OBJE- und Quellen-Datensätze, die nur Lebende benutzen; andere bleiben")]
    public void Private_drops_records()
    {
        const string ged = "0 HEAD\r\n1 CHAR UTF-8\r\n0 @I1@ INDI\r\n1 NAME Erika /Muster/\r\n1 BIRT\r\n2 DATE 1990\r\n2 SOUR @S2@\r\n1 NOTE @N1@\r\n1 OBJE @M1@\r\n"
            + "0 @I2@ INDI\r\n1 NAME Max /Muster/\r\n1 BIRT\r\n2 DATE 1850\r\n1 DEAT\r\n2 DATE 1920\r\n1 NOTE @N2@\r\n2 SOUR @S1@\r\n"
            + "0 @N1@ NOTE Erika wohnt in der Gartenstraße 5\r\n0 @M1@ OBJE\r\n1 FILE C:\\Fotos\\erika-2024.jpg\r\n0 @N2@ NOTE Max war Bäcker\r\n"
            + "0 @S1@ SOUR\r\n1 TITL Kirchenbuch\r\n0 @S2@ SOUR\r\n1 TITL Geburtsurkunde Erika Muster\r\n0 _EIGEN Bleibt\r\n0 TRLR\r\n";
        var t = FamilyKit.FromGedcom(Encoding.UTF8.GetBytes(ged));
        var outp = FamilyKit.ToGedcom(t, hideLiving: true, now: Now);
        foreach (var gone in new[] { "Gartenstraße", "erika-2024.jpg", "@M1@", "@N1@", "Geburtsurkunde", "@S2@", "1990" }) Assert.DoesNotContain(gone, outp);
        foreach (var stays in new[] { "0 @N2@ NOTE Max war Bäcker", "0 @S1@ SOUR", "1 TITL Kirchenbuch", "0 _EIGEN Bleibt", "1 NAME Lebende Person /Muster/" }) Assert.Contains(stays, outp);
        Assert.Contains("Gartenstraße", FamilyKit.ToGedcom(t, hideLiving: false, now: Now));
    }

    [Fact(DisplayName = "T-0788 G6: doppelte Kennungen werden gemeldet und eindeutig gemacht – beide Personen bleiben, die zweite wird nicht übersehen")]
    public void Duplicate_xrefs()
    {
        const string ged = "0 HEAD\r\n1 CHAR UTF-8\r\n0 @I1@ INDI\r\n1 NAME Max /Muster/\r\n1 BIRT\r\n2 DATE 1850\r\n1 DEAT\r\n2 DATE 1920\r\n0 @I1@ INDI\r\n1 NAME Erika /Muster/\r\n1 BIRT\r\n2 DATE 1990\r\n"
            + "0 @F1@ FAM\r\n1 HUSB @I1@\r\n0 @F1@ FAM\r\n1 WIFE @I1@\r\n0 TRLR\r\n";
        var t = FamilyKit.FromGedcom(Encoding.UTF8.GetBytes(ged));
        Assert.Equal(2, t.Persons.Count); Assert.Equal(2, t.Persons.Select(p => p.Id).Distinct().Count());
        Assert.Equal(2, t.Families.Select(f => f.Id).Distinct().Count());
        Assert.Contains("doppelt", t.ImportNote);
        var outp = FamilyKit.ToGedcom(t, hideLiving: true, now: Now);
        Assert.DoesNotContain("Erika", outp); Assert.Contains("Max /Muster/", outp); // die zweite (lebende) Person wird nicht mit dem Urteil der ersten durchgelassen
    }

    [Fact(DisplayName = "T-0788 G9: überlange Kennungen werden beim Einlesen gekürzt, Verweise mitgeführt, Export hält 22 Zeichen ein")]
    public void Long_xrefs()
    {
        const string ged = "0 HEAD\r\n1 CHAR UTF-8\r\n0 @PERSON_MIT_SEHR_LANGER_KENNUNG_123@ INDI\r\n1 NAME Max /Muster/\r\n1 FAMS @F1@\r\n0 @F1@ FAM\r\n1 HUSB @PERSON_MIT_SEHR_LANGER_KENNUNG_123@\r\n0 TRLR\r\n";
        var t = FamilyKit.FromGedcom(Encoding.UTF8.GetBytes(ged));
        var p = t.Persons.Single();
        Assert.True(p.Id.Length <= 20, p.Id);
        Assert.Equal(p.Id, t.Families.Single().Husband);
        Assert.Contains("Kennung", t.ImportNote);
        foreach (var l in FamilyKit.ToGedcom(t, false, Now).Split("\r\n")) if (l.Split(' ') is [_, var x, ..] && x.StartsWith('@')) Assert.True(x.Length <= 22, l);
    }

    [Fact(DisplayName = "T-0788 G6: KI-Prüfung lehnt vorhandene Kennung, geänderte Datensätze/Kopf/Titel/Mitte und neue Kreise ab")]
    public void Ki_check_strict()
    {
        var t = FamilyKit.FromGedcom(T0788Fixtures.Bytes); var before = FamilyKit.ToJson(t);
        JsonObject Changed(Action<FamilyTree> a) { var x = FamilyKit.FromJson(before); a(x); return FamilyKit.ToJson(x); }
        Assert.NotNull(FamilyKit.KiProblem(before, Changed(x => x.Persons.Add(new FamPerson { Id = "I1", Given = "Doppelt" }))));
        Assert.NotNull(FamilyKit.KiProblem(before, Changed(x => x.Records.Clear())));
        Assert.NotNull(FamilyKit.KiProblem(before, Changed(x => x.Head.Clear())));
        Assert.NotNull(FamilyKit.KiProblem(before, Changed(x => x.Title = "Anders")));
        Assert.NotNull(FamilyKit.KiProblem(before, Changed(x => x.Root = "I2")));
        // neue Familie, in der Erika (Kind von I1) Mutter ihres eigenen Vaters wird
        Assert.Contains("Kreis", FamilyKit.KiProblem(before, Changed(x => x.Families.Add(new FamFamily { Id = "F9", Wife = "I3", Children = ["I1"] }))));
        // erlaubt: neue Person und neue Familie ohne Kreis
        Assert.Null(FamilyKit.KiProblem(before, Changed(x => { x.Persons.Add(Person("I9", "Neu", "Muster", "M", "1900")); x.Families.Add(new FamFamily { Id = "F9", Husband = "I9", Children = ["I3"] }); })));
    }

    [Theory(DisplayName = "T-0788 G7: Datumsgrenzfälle – Februar v. Chr. (julianisch), julianischer 29. Februar 1700, Doppeljahr, verkehrte Spanne, „ca.1850“")]
    [InlineData("29 FEB 44 B.C.", false)]
    [InlineData("31 FEB 44 B.C.", false)]
    [InlineData("29 FEB 45 B.C.", true)]
    [InlineData("@#DJULIAN@ 29 FEB 1700", true)]
    [InlineData("29 FEB 1700", false)]
    [InlineData("1699/00", true)]
    [InlineData("1699/45", false)]
    public void Date_edges(string raw, bool ok) => Assert.Equal(ok, GenDate.Parse(raw).Kind != GenDateKind.Unreadable);

    [Fact(DisplayName = "T-0788 G7: verkehrte Spanne wird gemeldet (Eingabe abgelehnt, Prüfung nennt sie), „ca.1850“ ohne Leerzeichen lesbar")]
    public void Date_reversed_and_ca()
    {
        Assert.True(GenDate.Parse("BET 1900 AND 1850").Reversed);
        Assert.True(GenDate.Parse("FROM 1900 TO 1850").Reversed);
        Assert.False(GenDate.Parse("BET 1850 AND 1900").Reversed);
        Assert.Contains("verkehrt", Assert.Throws<ArgumentException>(() => GenDate.FromInput("zwischen 1900 und 1850")).Message);
        var t = new FamilyTree(); t.Persons.Add(Person("I1", "Max", "Muster", "M", "BET 1900 AND 1850"));
        Assert.Contains(FamilyKit.Check(t), i => i.Text.Contains("Spanne verkehrt"));
        Assert.Equal("ABT 1850", GenDate.FromInput("ca.1850"));
        Assert.Equal("ABT 1850", GenDate.FromInput("um1850".Insert(2, " ")));
    }

    [Fact(DisplayName = "T-0788 G8: CSV entschärft Zellen, die mit = + - @ Tab oder Zeilenumbruch beginnen")]
    public void Csv_formula()
    {
        var t = new FamilyTree { HideLiving = false };
        t.Persons.Add(Person("I1", "=HYPERLINK(\"http://x\")", "+Muster", "M", "1850", "1920"));
        t.Persons[0].Events[0].Place = "@Ort";
        var csv = FamilyKit.ToCsv(t, Now);
        Assert.Contains(";\"'=HYPERLINK(", csv); Assert.Contains(";'+Muster;", csv); Assert.Contains(";'@Ort;", csv);
        foreach (var bad in new[] { ";=", ";+", ";@", ";\"=", ";\"+", ";\"@" }) Assert.DoesNotContain(bad, csv);
    }

    [Fact(DisplayName = "T-0788 G9: lange Werte werden nie zwischen Buchstabe und Kombinationszeichen getrennt")]
    public void Chunks_keep_combining()
    {
        var v = string.Concat(Enumerable.Repeat("Zoe\u0308 ", 1)) + string.Concat(Enumerable.Repeat("e\u0301", 400));
        var parts = Gedcom.Chunks(v);
        Assert.True(parts.Count > 1);
        Assert.All(parts.Skip(1), c => Assert.NotEqual(System.Globalization.UnicodeCategory.NonSpacingMark, char.GetUnicodeCategory(c[0])));
        Assert.Equal(v, string.Concat(parts));
    }
}
