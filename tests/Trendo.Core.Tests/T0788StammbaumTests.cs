using System.Text;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Trendo.Core.Office;
using Trendo.Core.PromptAction;
using Trendo.Core.Wissen;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0788: Eingangsdatei in der Form, wie verbreitete Genealogie-Programme sie schreiben (eigene Beispieldaten, neutrale Namen):
/// Kopf mit Fremdprogramm, Einsender, Namen mit GIVN/SURN, Koordinaten, Notizen über mehrere Zeilen, Änderungsdatum (CHAN), eigene Tags (_UID, _FREL, _STAT),
/// Notiz- und Archiv-Datensätze, Medienverweis, Umlaute.</summary>
public static class T0788Fixtures
{
    public const string Foreign = """
0 HEAD
1 SOUR FREMDPROGRAMM
2 NAME Genealogie-Programm
2 VERS 5.2
1 DATE 3 OCT 2026
1 SUBM @U1@
1 GEDC
2 VERS 5.5.1
2 FORM LINEAGE-LINKED
1 CHAR UTF-8
1 LANG German
1 PLAC
2 FORM Ort, Kreis, Land
0 @U1@ SUBM
1 NAME Erika Muster
0 @I1@ INDI
1 NAME Jürgen /Größer/
2 GIVN Jürgen
2 SURN Größer
1 SEX M
1 BIRT
2 DATE 12 MAR 1850
2 PLAC Köln, Nordrhein-Westfalen, Deutschland
3 MAP
4 LATI N50.9375
4 LONG E6.9603
2 SOUR @S1@
3 PAGE Taufbuch 1850, Nr. 34
3 QUAY 3
1 DEAT
2 DATE ABT 1920
2 PLAC Düsseldorf
1 OCCU Bäcker
1 NOTE Erste Zeile der Notiz
2 CONT zweite Zeile mit Umlauten: äöü ÄÖÜ ß
2 CONC  – fortgesetzt
1 _UID 4F2A11C0E1B44C7A9F0A
1 OBJE @M1@
1 CHAN
2 DATE 3 OCT 2026
3 TIME 10:15:00
1 FAMS @F1@
0 @I2@ INDI
1 NAME Anna /Muster/
1 SEX F
1 BIRT
2 DATE BET 1852 AND 1854
1 DEAT
2 DATE 4 JUN 1931
1 NOTE @N1@
1 FAMS @F1@
0 @I3@ INDI
1 NAME Erika /Größer/
1 SEX F
1 BIRT
2 DATE 1 FEB 1880
2 PLAC Köln
1 DEAT Y
1 FAMC @F1@
2 PEDI birth
0 @F1@ FAM
1 HUSB @I1@
1 WIFE @I2@
1 CHIL @I3@
2 _FREL Natural
2 _MREL Natural
1 MARR
2 DATE 5 MAY 1878
2 PLAC Köln
1 _STAT verheiratet
0 @S1@ SOUR
1 TITL Kirchenbuch St. Beispiel 1840–1860
1 AUTH Pfarramt Musterstadt
1 REPO @R1@
0 @R1@ REPO
1 NAME Archiv Musterstadt
0 @N1@ NOTE Notiz als eigener Datensatz
0 @M1@ OBJE
1 FILE bild.jpg
2 FORM jpg
0 _PLAC Köln
1 _GOV KOELNJO30RW
0 TRLR
""";

    public static byte[] Bytes => Encoding.UTF8.GetBytes(Foreign.Replace("\r\n", "\n").Replace("\n", "\r\n"));
    public static readonly DateTime Now = new(2026, 11, 28);
}

public class T0788StammbaumTests
{
    static FamilyTree Foreign() => FamilyKit.FromGedcom(T0788Fixtures.Bytes);

    [Fact(DisplayName = "T-0788 GEDCOM: Fremddatei mit Umlauten gelesen – Personen, Familie, Quelle, Fundstelle, Koordinaten, mehrzeilige Notiz")]
    public void Read_foreign()
    {
        var t = Foreign();
        Assert.Equal(3, t.Persons.Count); Assert.Single(t.Families); Assert.Single(t.Sources);
        var j = t.P("I1")!;
        Assert.Equal(("Jürgen", "Größer", "M"), (j.Given, j.Surname, j.Sex));
        var b = j.Ev("BIRT")!;
        Assert.Equal(("12 MAR 1850", "Köln, Nordrhein-Westfalen, Deutschland", "N50.9375", "E6.9603"), (b.Date, b.Place, b.Lat, b.Long));
        Assert.Equal(("S1", "Taufbuch 1850, Nr. 34"), (b.Cites[0].Source, b.Cites[0].Page));
        Assert.Equal("Erste Zeile der Notiz\nzweite Zeile mit Umlauten: äöü ÄÖÜ ß – fortgesetzt", j.Note);
        Assert.Equal("Bäcker", j.Ev("OCCU")!.Value);
        Assert.Equal(("I1", "I2", "I3"), (t.Families[0].Husband, t.Families[0].Wife, t.Families[0].Children.Single()));
        Assert.Equal("Kirchenbuch St. Beispiel 1840–1860", t.Sources[0].Title);
        Assert.Contains("UTF-8", t.ImportNote);
        Assert.Equal(["SUBM", "REPO", "NOTE", "OBJE", "_PLAC"], t.Records.Select(r => r.Tag)); // unverändert übernommen
    }

    [Fact(DisplayName = "T-0788 GEDCOM: Rundreise ohne Datenverlust – Inhalt nach Export und erneutem Einlesen gleich, unbekannte Tags und Datensätze erhalten, zweiter Export zeichengleich")]
    public void Roundtrip_lossless()
    {
        var t = Foreign();
        var ged = FamilyKit.ToGedcom(t, hideLiving: false, now: T0788Fixtures.Now);
        var back = FamilyKit.FromGedcom(Encoding.UTF8.GetBytes(ged));
        Assert.Equal(FamilyKit.Content(t), FamilyKit.Content(back));
        Assert.Equal(ged, FamilyKit.ToGedcom(back, hideLiving: false, now: T0788Fixtures.Now));
        foreach (var s in new[] { "1 _UID 4F2A11C0E1B44C7A9F0A", "2 _FREL Natural", "2 _MREL Natural", "1 _STAT verheiratet", "3 QUAY 3", "1 OBJE @M1@", "3 TIME 10:15:00", "2 PEDI birth",
                     "0 @R1@ REPO", "1 REPO @R1@", "0 @N1@ NOTE Notiz als eigener Datensatz", "1 NOTE @N1@", "0 _PLAC Köln", "1 _GOV KOELNJO30RW", "1 LANG German", "2 FORM Ort, Kreis, Land",
                     "1 SUBM @U1@", "0 @U1@ SUBM", "1 CHAR UTF-8", "2 VERS 5.5.1", "4 LATI N50.9375", "1 DEAT Y" })
            Assert.Contains(s + "\r\n", ged);
        Assert.StartsWith("0 HEAD\r\n", ged); Assert.EndsWith("0 TRLR\r\n", ged);
        Assert.Contains("2 CONT zweite Zeile mit Umlauten: äöü ÄÖÜ ß – fortgesetzt", ged);
        Assert.All(ged.Split("\r\n"), l => Assert.True(Encoding.UTF8.GetByteCount(l) <= 255, l));
    }

    [Fact(DisplayName = "T-0788 GEDCOM: lange Notiz wird mit CONC geteilt (≤ 255 Byte je Zeile, nie an einem Leerzeichen) und kommt gleich zurück")]
    public void Long_values()
    {
        var t = new FamilyTree();
        var p = new FamPerson { Id = "I1", Sex = "F" }; FamilyKit.SetName(p, "Erika", "Muster");
        p.Note = string.Join(" ", Enumerable.Range(1, 300).Select(i => "Wort" + i + "äö")) + "\nZweiter Absatz";
        t.Persons.Add(p);
        var ged = FamilyKit.ToGedcom(t, false, T0788Fixtures.Now);
        var lines = ged.Split("\r\n");
        Assert.All(lines, l => Assert.True(Encoding.UTF8.GetByteCount(l) <= 255));
        var conc = lines.Where(l => l.StartsWith("2 CONC ", StringComparison.Ordinal)).ToList();
        Assert.True(conc.Count >= 5);
        Assert.All(conc, l => { Assert.NotEqual(' ', l[7]); Assert.False(l.EndsWith(' ')); });
        Assert.Equal(p.Note, FamilyKit.FromGedcom(Encoding.UTF8.GetBytes(ged)).P("I1")!.Note);
    }

    [Fact(DisplayName = "T-0788 GEDCOM: ANSEL wird erkannt, Umlaute und ß werden richtig umgerechnet, unbekannte Zeichen ehrlich gezählt")]
    public void Ansel()
    {
        var text = "0 HEAD\r\n1 CHAR ANSEL\r\n0 @I1@ INDI\r\n1 NAME Jürgen /Größer/\r\n1 BIRT\r\n2 PLAC Köln\r\n0 @I2@ INDI\r\n1 NAME Zoë /Çelik/\r\n0 TRLR\r\n";
        var bytes = Gedcom.EncodeAnsel(text);
        Assert.Contains((byte)0xE8, bytes); Assert.Contains((byte)0xCF, bytes); // Trema vor dem Buchstaben, ß
        Assert.DoesNotContain(bytes, b => b == 0xC3); // kein UTF-8
        var t = FamilyKit.FromGedcom(bytes);
        Assert.Equal(("Jürgen", "Größer"), (t.P("I1")!.Given, t.P("I1")!.Surname));
        Assert.Equal("Köln", t.P("I1")!.Ev("BIRT")!.Place);
        Assert.Equal(("Zoë", "Çelik"), (t.P("I2")!.Given, t.P("I2")!.Surname));
        Assert.Contains("ANSEL", t.ImportNote);
        // unbekanntes ANSEL-Zeichen (0xD5 ist nicht belegt)
        var broken = bytes.ToList(); broken.InsertRange(broken.IndexOf((byte)'Z'), [0xD5]);
        var r = Gedcom.Read([.. broken]);
        Assert.Equal(1, r.BadChars); Assert.Contains("1 Zeichen unbekannt", r.Encoding);
        // Export ist immer UTF-8
        var ged = FamilyKit.ToGedcom(t, false, T0788Fixtures.Now);
        Assert.Contains("1 CHAR UTF-8", ged); Assert.Contains("Jürgen /Größer/", ged);
    }

    [Fact(DisplayName = "T-0788 GEDCOM: Kodierungen UTF-16, Windows-1252 (ANSI) und UTF-8 mit falscher ASCII-Angabe")]
    public void Encodings()
    {
        var text = "0 HEAD\r\n1 CHAR {0}\r\n0 @I1@ INDI\r\n1 NAME Jürgen /Größer/\r\n0 TRLR\r\n";
        var u16 = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(string.Format(text, "UNICODE"))).ToArray();
        Assert.Equal("Größer", FamilyKit.FromGedcom(u16).P("I1")!.Surname);
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var ansi = Encoding.GetEncoding(1252).GetBytes(string.Format(text, "ANSI"));
        var a = Gedcom.Read(ansi); Assert.Contains("Windows-1252", a.Encoding);
        Assert.Equal("Größer", FamilyKit.FromRecords(a.Records).P("I1")!.Surname);
        var lie = Encoding.UTF8.GetBytes(string.Format(text, "ASCII"));
        var l = Gedcom.Read(lie); Assert.Contains("UTF-8", l.Encoding);
        Assert.Equal("Jürgen", FamilyKit.FromRecords(l.Records).P("I1")!.Given);
    }

    [Fact(DisplayName = "T-0788 GEDCOM: kaputte Zeilen werden mit Zeilennummer gemeldet, nicht geraten")]
    public void Broken_lines()
    {
        var r = Gedcom.Read(Encoding.UTF8.GetBytes("0 HEAD\n1 CHAR UTF-8\ndas ist keine Zeile\n0 @I1@ INDI\n3 NAME Sprung /Ebene/\n"));
        Assert.Contains(r.Problems, p => p.StartsWith("Zeile 3 nicht lesbar"));
        Assert.Contains(r.Problems, p => p.Contains("Ebene 3 springt"));
        Assert.Contains(r.Problems, p => p.Contains("TRLR"));
    }

    [Theory(DisplayName = "T-0788 Datum: GEDCOM-Formen lesen und deutsch anzeigen")]
    [InlineData("12 MAR 1850", "12. März 1850", GenDateKind.Exact)]
    [InlineData("MAR 1850", "März 1850", GenDateKind.Exact)]
    [InlineData("abt 1850", "um 1850", GenDateKind.About)]
    [InlineData("CAL 1850", "errechnet 1850", GenDateKind.About)]
    [InlineData("BEF 1 JAN 1900", "vor 1. Januar 1900", GenDateKind.Before)]
    [InlineData("AFT 1900", "nach 1900", GenDateKind.After)]
    [InlineData("BET 1852 AND 1854", "zwischen 1852 und 1854", GenDateKind.Between)]
    [InlineData("FROM 1914 TO 1918", "von 1914 bis 1918", GenDateKind.Period)]
    [InlineData("44 B.C.", "44 v. Chr.", GenDateKind.Exact)]
    [InlineData("(im Krieg verschollen)", "im Krieg verschollen", GenDateKind.Phrase)]
    [InlineData("INT 1850 (laut Grabstein)", "um 1850 (laut Grabstein)", GenDateKind.About)]
    [InlineData("12.03.1850", "12. März 1850", GenDateKind.Exact)]
    [InlineData("irgendwann", "irgendwann", GenDateKind.Unreadable)]
    [InlineData("31 FEB 1850", "31 FEB 1850", GenDateKind.Unreadable)]
    public void Dates(string raw, string shown, GenDateKind kind)
    {
        var g = GenDate.Parse(raw);
        Assert.Equal(kind, g.Kind); Assert.Equal(shown, g.Show()); Assert.Equal(raw, g.Raw);
        Assert.Equal(raw == "12.03.1850", g.NonStandard);
    }

    [Theory(DisplayName = "T-0788 Datum: deutsche Eingabe → GEDCOM")]
    [InlineData("12.03.1850", "12 MAR 1850")]
    [InlineData("März 1850", "MAR 1850")]
    [InlineData("3.1850", "MAR 1850")]
    [InlineData("ca. 1850", "ABT 1850")]
    [InlineData("um 1850", "ABT 1850")]
    [InlineData("vor 1900", "BEF 1900")]
    [InlineData("nach 12.5.1900", "AFT 12 MAY 1900")]
    [InlineData("zwischen 1850 und 1860", "BET 1850 AND 1860")]
    [InlineData("1850–1860", "BET 1850 AND 1860")]
    [InlineData("von 1914 bis 1918", "FROM 1914 TO 1918")]
    [InlineData("44 v. Chr.", "44 B.C.")]
    [InlineData("12 mar 1850", "12 MAR 1850")]
    [InlineData("(unbekannt)", "(unbekannt)")]
    [InlineData("", "")]
    public void Input(string input, string ged) => Assert.Equal(ged, GenDate.FromInput(input));

    [Fact(DisplayName = "T-0788 Datum: unlesbare Eingabe wird mit Beispiel abgelehnt")]
    public void Input_rejected() => Assert.Contains("nicht lesbar", Assert.Throws<ArgumentException>(() => GenDate.FromInput("Ostern")).Message);

    static FamilyTree Tree(params (string Id, string Name, string Sex, string Birth, string Death)[] ps)
    {
        var t = new FamilyTree();
        foreach (var (id, name, sex, b, d) in ps)
        {
            var p = new FamPerson { Id = id, Sex = sex }; var (g, s) = FamilyKit.NameOf(name); FamilyKit.SetName(p, g, s);
            if (b.Length > 0) p.Events.Add(new() { Tag = "BIRT", Date = b });
            if (d.Length > 0) p.Events.Add(new() { Tag = "DEAT", Date = d });
            t.Persons.Add(p);
        }
        return t;
    }

    [Fact(DisplayName = "T-0788 Prüfung: Geburt nach Tod, Eltern jünger als Kind, Geburt nach Tod der Mutter, Heirat vor Geburt, eigener Vorfahre – mit Namen")]
    public void Checks_find_contradictions()
    {
        var t = Tree(("I1", "Max Muster", "M", "1900", "1880"), ("I2", "Erika Muster", "F", "1950", "1940"), ("I3", "Kind Muster", "F", "1930", ""), ("I4", "Klein Muster", "M", "1945", ""));
        t.Families.Add(new() { Id = "F1", Husband = "I1", Wife = "I2", Children = ["I3", "I4"], Events = [new() { Tag = "MARR", Date = "1890" }] });
        var issues = FamilyKit.Check(t);
        Assert.Contains(issues, i => i.Error && i.Text == "Max Muster: Geburt (1900) nach dem Tod (1880)");
        Assert.Contains(issues, i => i.Error && i.Person == "I3" && i.Text.Contains("Mutter Erika Muster ist jünger als das Kind"));
        Assert.Contains(issues, i => i.Error && i.Person == "I3" && i.Text.Contains("nach dem Tod des Vaters"));
        Assert.Contains(issues, i => i.Error && i.Person == "I4" && i.Text.Contains("nach dem Tod der Mutter"));
        Assert.Contains(issues, i => i.Error && i.Text.Contains("Heirat (1890) vor der eigenen Geburt"));
        // Kreis: I1 wird Kind von I3
        t.Families.Add(new() { Id = "F2", Wife = "I3", Children = ["I1"] });
        Assert.Contains(FamilyKit.Check(t), i => i.Error && i.Text.Contains("eigener Vorfahre"));
    }

    [Fact(DisplayName = "T-0788 Prüfung: keine Fehlalarme bei stimmigen und bei ungefähren Daten (±10 Jahre)")]
    public void Checks_no_false_alarm()
    {
        var t = Tree(("I1", "Max Muster", "M", "ABT 1850", "1920"), ("I2", "Erika Muster", "F", "BET 1852 AND 1854", "1931"), ("I3", "Kind Muster", "F", "1880", ""));
        t.Families.Add(new() { Id = "F1", Husband = "I1", Wife = "I2", Children = ["I3"], Events = [new() { Tag = "MARR", Date = "1878" }] });
        Assert.DoesNotContain(FamilyKit.Check(t), i => i.Error);
        // „um 1862“ ist für ein Kind von 1856 Geborener möglich (ungefähr) – kein Fehler, höchstens Hinweis
        t.P("I3")!.Events[0].Date = "ABT 1862";
        Assert.DoesNotContain(FamilyKit.Check(t), i => i.Error);
        Assert.Empty(FamilyKit.Check(Foreign()).Where(i => i.Error));
    }

    [Fact(DisplayName = "T-0788 Lebende: Erkennung (Tod, Alter, Schätzung über Kinder) und Ausblenden im GEDCOM-Export")]
    public void Living_hidden()
    {
        var t = Tree(("I1", "Max Muster", "M", "1900", "1980"), ("I2", "Erika Muster", "F", "1985", ""), ("I3", "Alt Muster", "M", "1890", ""), ("I4", "Ohne Daten", "F", "", ""), ("I5", "Enkel Muster", "M", "2010", ""));
        t.P("I2")!.Note = "privat";
        t.Families.Add(new() { Id = "F1", Husband = "I3", Children = ["I4"] });
        t.Families.Add(new() { Id = "F2", Wife = "I2", Children = ["I5"], Events = [new() { Tag = "MARR", Date = "2008", Place = "Musterstadt" }] });
        var now = T0788Fixtures.Now;
        Assert.False(FamilyKit.IsLiving(t, t.P("I1")!, now)); Assert.True(FamilyKit.IsLiving(t, t.P("I2")!, now)); Assert.False(FamilyKit.IsLiving(t, t.P("I3")!, now));
        Assert.True(FamilyKit.IsLiving(t, t.P("I4")!, now)); // T-0788 G4: Vater 1890 – das Kind kann bis 1970 geboren sein, im Zweifel lebend
        var ged = FamilyKit.ToGedcom(t, hideLiving: true, now: now);
        Assert.DoesNotContain("Erika", ged); Assert.DoesNotContain("1985", ged); Assert.DoesNotContain("privat", ged); Assert.DoesNotContain("Musterstadt", ged); Assert.DoesNotContain("2010", ged);
        Assert.Contains("1 NAME Lebende Person /Muster/", ged); Assert.Contains("1 NAME Max /Muster/", ged); Assert.Contains("2 DATE 1980", ged);
        var back = FamilyKit.FromGedcom(Encoding.UTF8.GetBytes(ged));
        Assert.Equal(["I5"], back.F("F2")!.Children); // Verknüpfungen bleiben
        Assert.Contains("Erika", FamilyKit.ToGedcom(t, hideLiving: false, now: now));
        // Bericht und Tabelle halten sich an dieselbe Einstellung
        Assert.DoesNotContain("Erika", FamilyKit.ToCsv(t, now));
        Assert.DoesNotContain(FamilyKit.BuildReport(t, now).Blocks.OfType<Para>(), p => p.Text.Contains("Erika"));
    }

    [Fact(DisplayName = "T-0788 Baum: Vorfahren mit Platzhaltern für fehlende Eltern, Nachkommen mit Partnern; SVG ist gültiges XML")]
    public void Layouts()
    {
        var t = Foreign();
        var anc = FamilyKit.Ancestors(t, "I3", 3);
        Assert.Contains("I3", anc.Hits.Keys); Assert.Contains("I1", anc.Hits.Keys); Assert.Contains("I2", anc.Hits.Keys);
        Assert.Contains("+vater:I1", anc.Hits.Keys); Assert.Contains("+mutter:I2", anc.Hits.Keys);
        Assert.True(anc.Hits["I1"].X > anc.Hits["I3"].X);
        Assert.DoesNotContain(FamilyKit.Ancestors(t, "I3", 3, paper: true).Hits.Keys, k => k.StartsWith('+')); // Platzhalter nicht in Ausgaben
        var desc = FamilyKit.Descendants(t, "I1", 3);
        Assert.True(desc.Hits["I3"].Y > desc.Hits["I1"].Y);
        Assert.Contains(desc.Prims.OfType<TimelineKit.PText>(), p => p.Text == "⚭ Anna Muster");
        var svg = XDocument.Parse(TimelineKit.ToSvg(FamilyKit.Ancestors(t, "I3", 4, paper: true), "Stammbaum"));
        var texts = svg.Descendants().Where(e => e.Name.LocalName == "text").Select(e => e.Value).ToList();
        Assert.Contains("Jürgen Größer", texts); Assert.Contains("Stammbaum", svg.Descendants().First(e => e.Name.LocalName == "title").Value);
    }

    [Fact(DisplayName = "T-0788 Bericht: Personen, Eltern, Partner mit Heirat, Kinder, Quellen mit Fundstelle, Prüfung und Ortsverzeichnis – als DOCX, ODT und PDF")]
    public void Report()
    {
        var t = Foreign(); t.HideLiving = false; t.Title = "Familie Größer";
        var doc = FamilyKit.BuildReport(t, T0788Fixtures.Now);
        var texts = doc.Blocks.OfType<Para>().Select(p => p.Text).ToList();
        Assert.Contains("Familie Größer", texts);
        Assert.Contains("Jürgen Größer (männlich)", texts);
        Assert.Contains("Geburt: 12. März 1850 in Köln, Nordrhein-Westfalen, Deutschland – Quelle [1] Taufbuch 1850, Nr. 34", texts);
        Assert.Contains("Partner: Anna Muster, Heirat 5. Mai 1878 in Köln; Kinder: Erika Größer", texts);
        Assert.Contains("Eltern: Jürgen Größer und Anna Muster", texts);
        Assert.Contains("[1] Pfarramt Musterstadt. Kirchenbuch St. Beispiel 1840–1860", texts);
        Assert.Contains("Keine Widersprüche gefunden.", texts);
        Assert.Contains(doc.Blocks.OfType<Table>(), x => x.Rows.Any(r => r[0] == "Köln" && r[1] == "2"));
        Assert.IsType<Para>(doc.Blocks[^1]); // nie mit einer Tabelle enden (LibreOffice hängt sonst beim Einlesen des DOCX)
        var dir = Directory.CreateTempSubdirectory("trendo-t0788-").FullName;
        try
        {
            var docx = Path.Combine(dir, "b.docx"); DocxFormat.Write(doc, docx);
            Assert.Contains(DocxFormat.Read(docx).Paras, p => p.Text == "Jürgen Größer (männlich)");
            var odt = Path.Combine(dir, "b.odt"); OdtFormat.Write(doc, odt);
            Assert.Contains(OdtFormat.Read(odt).Paras, p => p.Text == "Prüfung");
            var pdf = Path.Combine(dir, "b.pdf"); TextPdf.Export(doc, pdf);
            using var pd = PdfSharp.Pdf.IO.PdfReader.Open(pdf, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import); Assert.True(pd.PageCount >= 1);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "T-0788 Zeilen: Personen und Ehen aus Zeilen, vorhandene Personen bleiben unverändert, unlesbare Daten werden genannt")]
    public void Lines()
    {
        var t = Foreign(); var before = FamilyKit.PersonJson(t.P("I1")!).ToJsonString();
        var (ps, ms, bad) = FamilyKit.ParseLines("Name | Geschlecht | Geburt\nMax Größer | m | 3.4.1905 | Köln | | | Jürgen Größer | Anna Muster\nJürgen Größer | m | 1999\nEhe: Erika Größer + Paul Beispiel | 1901 | Bonn\nIrgendwer | w | Ostern");
        Assert.Equal(["Irgendwer | w | Ostern"], bad);
        Assert.Equal(2, ps.Count); Assert.Single(ms);
        var (np, nl) = FamilyKit.AddLines(t, ps, ms);
        Assert.Equal(2, np); // Max Größer, Paul Beispiel
        Assert.Equal(before, FamilyKit.PersonJson(t.P("I1")!).ToJsonString()); // Jürgen: kein neues Geburtsdatum
        var max = t.Persons.Single(p => p.Name == "Max Größer");
        Assert.Equal("3 APR 1905", max.Birth!.Date); Assert.Contains("Max Größer", t.Children("I1").Select(c => c.Name));
        var paul = t.Persons.Single(p => p.Name == "Paul Beispiel");
        Assert.Equal("1901", t.OwnFamilies(paul.Id).Single().Marriage!.Date);
        Assert.True(nl >= 2);
    }

    [Fact(DisplayName = "T-0788 Zeilen (Befund Echtlauf): wer zuerst als Elternteil genannt wird und danach eine eigene Zeile hat, bekommt deren Daten")]
    public void Lines_parent_first()
    {
        var t = new FamilyTree();
        var (ps, ms, bad) = FamilyKit.ParseLines("Erika Muster | w | 15.08.1952 | Neustadt | | | Hans Muster | Marie Sommer\nHans Muster | m | 1921 | Musterstadt | 1999 | Neustadt | |\nMarie Sommer | w | um 1925 | Talheim | | | |");
        Assert.Empty(bad);
        FamilyKit.AddLines(t, ps, ms);
        Assert.Equal(3, t.Persons.Count);
        var hans = t.Persons.Single(p => p.Name == "Hans Muster");
        Assert.Equal(("1921", "Musterstadt", "1999", "Neustadt", "M"), (hans.Birth!.Date, hans.Birth.Place, hans.Death!.Date, hans.Death.Place, hans.Sex));
        Assert.Equal(("ABT 1925", "Talheim"), (t.Persons.Single(p => p.Name == "Marie Sommer").Birth!.Date, t.Persons.Single(p => p.Name == "Marie Sommer").Birth!.Place));
        // eine zweite Zeile zur selben (nun vorhandenen) Person in einem späteren Aufruf ändert nichts
        var (ps2, _, _) = FamilyKit.ParseLines("Hans Muster | m | 1930 | Anderswo");
        FamilyKit.AddLines(t, ps2, []);
        Assert.Equal("1921", t.Persons.Single(p => p.Name == "Hans Muster").Birth!.Date);
    }

    [Fact(DisplayName = "T-0788 KI-Prüfung: Änderung vorhandener Personen, Familien, Quellen oder des Schutzes wird abgelehnt; Ergänzen ist erlaubt")]
    public void Ki_check()
    {
        var t = Foreign(); var before = FamilyKit.ToJson(t);
        var ok = FamilyKit.FromJson(before);
        FamilyKit.AddLines(ok, [new("Max", "Größer", "M", "1905", "", "", "", "Jürgen Größer", "Anna Muster")], []);
        Assert.Null(FamilyKit.KiProblem(before, FamilyKit.ToJson(ok)));
        JsonObject Changed(Action<FamilyTree> a) { var x = FamilyKit.FromJson(before); a(x); return FamilyKit.ToJson(x); }
        Assert.Contains("„Jürgen Größer“ geändert", FamilyKit.KiProblem(before, Changed(x => x.P("I1")!.Ev("BIRT")!.Date = "1851")));
        Assert.Contains("entfernt", FamilyKit.KiProblem(before, Changed(x => x.Persons.RemoveAt(2))));
        Assert.Contains("Quellen", FamilyKit.KiProblem(before, Changed(x => x.Sources.Add(new() { Id = "S9", Title = "Erfunden" }))));
        Assert.Contains("Familie F1", FamilyKit.KiProblem(before, Changed(x => x.Families[0].Children.Clear())));
        Assert.Contains("Schutz", FamilyKit.KiProblem(before, Changed(x => x.HideLiving = !x.HideLiving)));
        Assert.Contains("unlesbares Datum", FamilyKit.KiProblem(before, Changed(x => x.Persons.Add(new() { Id = "I9", Given = "Neu", Events = [new() { Tag = "BIRT", Date = "Ostern" }] }))));
    }

    [Fact(DisplayName = "T-0788 Auftrag außerhalb des Studios: Zeilen → geprüfte GEDCOM-Datei im Projekt, ohne Seiteneffekt")]
    public void Adapter()
    {
        var all = PromptActions.CreateDefault();
        var ad = all.Single(a => a.StudioId == "STUDIO-FAMILY-01");
        Assert.False(ad.CanPerformSideEffect); Assert.Equal(SideEffectClass.None, ad.Effect);
        Assert.NotEmpty(ad.Missing(new Dictionary<string, string>()));
        Assert.Equal(CapabilityLevel.Partial, CapabilityCatalog.Level("STUDIO-FAMILY-01", all));
        Assert.NotEmpty(ad.Validate(new Dictionary<string, string> { ["personen"] = "| nur Striche" }));
        var root = Directory.CreateTempSubdirectory("trendo-t0788-").FullName;
        try
        {
            var core = new TrendoCore(Path.Combine(root, "daten"), new InMemorySecretStore(), offline: true);
            var project = core.Projects.Create("probe", "Probe").Id;
            var wf = new StudioWorkflow(core, all);
            var ctx = new StudioExecutionContext(project, Principal.LocalOwner, CancellationToken.None, ResourceBudget.Default);
            var req = new StudioPromptRequest("fam-1", project, "STUDIO-FAMILY-01", "Max Muster | m | 12.03.1850 | Musterstadt | 1920\nAnna Muster | w | 1880 | | | | Max Muster |", [], "de", null, PrivacyMode.Local, new Provenance("test", null, null));
            var done = wf.Los(project, wf.Start(req, ctx).RunId, ctx);
            Assert.Equal(RunStatus.Verified, done.Status);
            var ged = File.ReadAllText(core.Projects.ResolveInProject(project, done.Artifacts.Single(a => a.RelativePath.EndsWith("stammbaum.ged")).RelativePath));
            var t = FamilyKit.FromGedcom(Encoding.UTF8.GetBytes(ged));
            Assert.Equal("Max Muster", t.Parents(t.Persons.Single(p => p.Given == "Anna").Id).Father!.Name);
            Assert.Contains("2 DATE 12 MAR 1850", ged);
        }
        finally { try { Directory.Delete(root, true); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { } }
    }
}
