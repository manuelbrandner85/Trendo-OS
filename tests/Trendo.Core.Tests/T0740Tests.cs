using System.Text;
using Trendo.Core.Kreativ;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0740: Modell im Stapel anwenden (STUDIO-TRAIN-01) und KI-Anweisungen als Datei (CORE-12).</summary>
public class T0740Tests
{
    static TextClassifier Model() => TextClassifier.Train(
    [
        new("Rechnung bitte bezahlen Betrag überweisen", "Rechnung"), new("Mahnung offene Rechnung Betrag", "Rechnung"), new("Abbuchung vom Konto Rechnung", "Rechnung"),
        new("Termin Besprechung Montag Uhr", "Termin"), new("Treffen verschieben Dienstag Termin", "Termin"), new("Einladung Sitzung Uhr", "Termin"),
    ]);

    static string Temp() { var d = Path.Combine(Path.GetTempPath(), "trendo-t0740-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }

    [Fact(DisplayName = "T-0740 Stapel CSV: Kopfzeile erkannt, Textspalte per Namen, alle Spalten bleiben, je Zeile Vorhersage + Sicherheit, leere Zeile übersprungen")]
    public void Csv_rows()
    {
        var csv = "nr;text;notiz\n1;Bitte den offenen Betrag überweisen;a\n;;\n2;Können wir den Termin am Montag verschieben;b\n3;\"Mahnung; Rechnung noch offen\";c\n";
        var r = ClassifierBatch.ApplyCsv(Model(), csv, "x.csv");
        Assert.Equal(["nr", "text", "notiz", "Vorhersage", "Sicherheit", "Bekannte Wörter"], r.Header);
        Assert.Equal(1, r.TextColumn);
        Assert.Equal(3, r.Rows.Count); Assert.Equal(1, r.Skipped);
        Assert.Equal(["Rechnung", "Termin", "Rechnung"], r.Rows.Select(x => x[3]));
        Assert.All(r.Rows, x => Assert.EndsWith(" %", x[4]));
        Assert.Equal("c", r.Rows[2][2]);
        var outCsv = ClassifierBatch.ToCsv(r);
        var back = Trendo.Core.Sys.Csv.Parse(outCsv);
        Assert.Equal("Mahnung; Rechnung noch offen", back[3][1]); // Semikolon im Text bleibt (Anführungszeichen)
        Assert.Equal(("Rechnung", 2), (ClassifierBatch.Summary(r)[0].Label, ClassifierBatch.Summary(r)[0].Count));
    }

    [Fact(DisplayName = "T-0740 Stapel CSV ohne Kopfzeile: längste Spalte ist der Text; Formel-Zellen werden entschärft")]
    public void Csv_without_header()
    {
        var r = ClassifierBatch.ApplyCsv(Model(), "7;Einladung zur Sitzung um 10 Uhr\n8;=SUMME(A1) Rechnung Betrag\n", "y.csv");
        Assert.Equal(["Spalte 1", "Text", "Vorhersage", "Sicherheit", "Bekannte Wörter"], r.Header);
        Assert.Equal(2, r.Rows.Count);
        Assert.Equal("Termin", r.Rows[0][2]);
        Assert.Contains("'=SUMME(A1) Rechnung Betrag", ClassifierBatch.ToCsv(r));
    }

    [Fact(DisplayName = "T-0740 Stapel: Text ohne bekannte Wörter bleibt ohne Vorhersage (nicht geraten)")]
    public void Csv_unknown_text()
    {
        var r = ClassifierBatch.ApplyCsv(Model(), "text\n!!!\n", "z.csv");
        Assert.Equal(["", "", "0"], r.Rows[0].Skip(1));
        Assert.Equal("(nicht einzuordnen)", ClassifierBatch.Summary(r)[0].Label);
    }

    [Fact(DisplayName = "T-0740 Stapel Ordner: je Textdatei eine Zeile (mit Unterordnern), andere Dateiarten nicht, Fortschritt bis zum Ende")]
    public void Folder()
    {
        var d = Temp();
        try
        {
            File.WriteAllText(Path.Combine(d, "a.txt"), "Mahnung: Rechnung bitte bezahlen");
            Directory.CreateDirectory(Path.Combine(d, "unter"));
            File.WriteAllText(Path.Combine(d, "unter", "b.md"), "# Einladung\nBesprechung am Montag um 10 Uhr");
            File.WriteAllBytes(Path.Combine(d, "bild.png"), [1, 2, 3]);
            var seen = new List<(int, int)>();
            var r = ClassifierBatch.ApplyFolder(Model(), d, new SyncProgress(seen));
            Assert.Equal(["Datei", "Vorhersage", "Sicherheit", "Bekannte Wörter", "Hinweis"], r.Header);
            Assert.Equal(2, r.Rows.Count);
            Assert.Equal(("a.txt", "Rechnung"), (r.Rows[0][0], r.Rows[0][1]));
            Assert.Equal((Path.Combine("unter", "b.md"), "Termin"), (r.Rows[1][0], r.Rows[1][1]));
            Assert.Equal((2, 2), seen[^1]);
            var empty = Path.Combine(d, "leer"); Directory.CreateDirectory(empty);
            Assert.Throws<InvalidDataException>(() => ClassifierBatch.ApplyFolder(Model(), empty));
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0740 Stapel: Abbrechen wirft OperationCanceledException und liefert kein Teilergebnis")]
    public void Cancel()
    {
        var sb = new StringBuilder("text\n"); for (var i = 0; i < 5000; i++) sb.Append("Rechnung bitte bezahlen ").Append(i).Append('\n');
        using var cts = new CancellationTokenSource();
        var p = new CancelAt(cts, 100);
        Assert.Throws<OperationCanceledException>(() => ClassifierBatch.ApplyCsv(Model(), sb.ToString(), "gross.csv", p, cts.Token));
        Assert.InRange(p.Last, 1, 200);
    }

    // ---------------- Gegenprüfung T-0740 ----------------

    [Fact(DisplayName = "T-0740 Gegenprüfung: Ordnerdatei in Windows-1252 (deutsches Excel/Editor) – Umlaute bleiben, kein U+FFFD")]
    public void Folder_windows1252()
    {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        var d = Temp();
        try
        {
            File.WriteAllBytes(Path.Combine(d, "brief.txt"), System.Text.Encoding.GetEncoding(1252).GetBytes("Rechnung für März: Betrag überweisen, Größe egal"));
            var r = ClassifierBatch.ApplyFolder(Model(), d);
            Assert.Equal("Rechnung", r.Rows[0][1]);
            Assert.True(int.Parse(r.Rows[0][3]) >= 2, "Umlaut-Wörter wurden nicht erkannt: " + r.Rows[0][3]);
            var csv = Path.Combine(d, "liste.csv");
            File.WriteAllBytes(csv, System.Text.Encoding.GetEncoding(1252).GetBytes("text;notiz\nBitte Betrag überweisen;Grüße aus Köln\n"));
            var c = ClassifierBatch.ApplyCsvFile(Model(), csv);
            Assert.Equal("Grüße aus Köln", c.Rows[0][1]);
            Assert.DoesNotContain('�', ClassifierBatch.ToCsv(c));
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0740 Gegenprüfung: CSV-Entschärfung – Zahlen wie -5 und +3,2 bleiben, Formeln, Tab und CR am Anfang bekommen ein Hochkomma")]
    public void Csv_formula_guard()
    {
        var r = new ClassifierBatch.Result(["a", "b", "c", "d", "e", "f", "g"], [["-5", "+3,2", "=1+1", "-1+A1", "\tx", "\rx", "@SUMME(A1)"]], 0, "x", "csv");
        var line = Trendo.Core.Sys.Csv.Parse(ClassifierBatch.ToCsv(r))[1];
        Assert.Equal(["-5", "+3,2", "'=1+1", "'-1+A1", "'\tx", "'\rx", "'@SUMME(A1)"], line.Take(7));
    }

    [Fact(DisplayName = "T-0740 Gegenprüfung: CSV ohne Kopfzeile, erste Zelle ein kurzes Wort, das das Modell kennt – die erste Zeile bleibt Daten")]
    public void Csv_short_first_word()
    {
        var r = ClassifierBatch.ApplyCsv(Model(), "Rechnung\nTermin am Montag um zehn Uhr\nBitte den Betrag überweisen\n", "k.csv");
        Assert.Equal(["Text", "Vorhersage", "Sicherheit", "Bekannte Wörter"], r.Header);
        Assert.Equal(3, r.Rows.Count);
        Assert.Equal("Rechnung", r.Rows[0][0]);
        // echte Kopfzeile mit einem Wort, das das Modell nicht kennt, wird weiter erkannt
        var h = ClassifierBatch.ApplyCsv(Model(), "Anliegen\nTermin am Montag um zehn Uhr\nBitte den Betrag überweisen\n", "k.csv");
        Assert.Equal("Anliegen", h.Header[0]); Assert.Equal(2, h.Rows.Count);
        Assert.False(r.HasHeader); Assert.True(h.HasHeader);
        // Schalter „Erste Zeile ist Überschrift“ in der Oberfläche: setzt die Erkennung außer Kraft
        Assert.Equal(2, ClassifierBatch.ApplyCsv(Model(), "Rechnung\nTermin am Montag um zehn Uhr\nBitte den Betrag überweisen\n", "k.csv", header: true).Rows.Count);
        Assert.Equal(3, ClassifierBatch.ApplyCsv(Model(), "Anliegen\nTermin am Montag um zehn Uhr\nBitte den Betrag überweisen\n", "k.csv", header: false).Rows.Count);
    }

    [Fact(DisplayName = "T-0740 Gegenprüfung: Über 50 000 Zeilen wird nicht still gekürzt – Ergebnis sagt „gekürzt“ und nennt die Gesamtzahl")]
    public void Csv_truncated()
    {
        var n = ClassifierBatch.MaxItems + 3;
        var sb = new StringBuilder("text\n"); for (var i = 0; i < n; i++) sb.Append("Rechnung Betrag ").Append(i).Append('\n');
        var r = ClassifierBatch.ApplyCsv(Model(), sb.ToString(), "gross.csv");
        Assert.True(r.Truncated); Assert.Equal(n, r.Total); Assert.Equal(ClassifierBatch.MaxItems, r.Rows.Count);
        var small = ClassifierBatch.ApplyCsv(Model(), "text\nRechnung Betrag\n", "k.csv");
        Assert.False(small.Truncated); Assert.Equal(1, small.Total);
    }

    sealed class SyncProgress(List<(int, int)> seen) : IProgress<(int Done, int Total)> { public void Report((int Done, int Total) v) => seen.Add(v); }
    sealed class CancelAt(CancellationTokenSource cts, int at) : IProgress<(int Done, int Total)>
    {
        public int Last;
        public void Report((int Done, int Total) v) { Last = v.Done; if (v.Done >= at) cts.Cancel(); }
    }

    // ---------------- CORE-12: KI-Anweisungen als Datei ----------------

    static PromptLibrary Lib(string root) => new(root, new AuditLog(Path.Combine(root, "audit.log")));

    [Fact(DisplayName = "T-0740 Vorlage als Markdown mit Kopfzeilen: Rundreise Name, Bereich, Text (mehrzeilig, Lücken) unverändert")]
    public void Markdown_roundtrip()
    {
        var root = Temp();
        try
        {
            var lib = Lib(root);
            var t = lib.Save(null, "Kündigung: Fitness", "Briefe", "Schreibe eine Kündigung an {{Studio}}.\n\n---\nFrist: {{Frist|3 Monate}}");
            var md = PromptLibrary.ToMarkdown(t);
            Assert.StartsWith("---\nformat: trendo-ki-anweisung\nname: Kündigung: Fitness\nbereich: Briefe\nversion: 1\n---\n", md);
            var e = Assert.Single(PromptLibrary.ParseFile("x.md", md.Replace("\n", "\r\n")));
            Assert.Equal(("Kündigung: Fitness", "Briefe", t.Text), (e.Name, e.Category, e.Text));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "T-0740 Import: .txt ohne Kopf nimmt den Dateinamen, .json als Sammlung, fremdes Format und leere Datei werden abgelehnt")]
    public void Parse_variants()
    {
        var e = Assert.Single(PromptLibrary.ParseFile("Kurz_fassen.txt", "Fasse {{Text}} zusammen."));
        Assert.Equal(("Kurz fassen", "Allgemein"), (e.Name, e.Category));
        var json = PromptLibrary.ToJson([new PromptTemplate { Name = "A", Category = "X", Versions = [new(1, "Text A", DateTimeOffset.Now, "")] }, new PromptTemplate { Name = "B", Versions = [new(2, "Text B", DateTimeOffset.Now, "")] }]);
        Assert.Equal(["A", "B"], PromptLibrary.ParseFile("sammlung.json", json).Select(x => x.Name));
        Assert.Equal("Text B", PromptLibrary.ParseFile("s.json", json)[1].Text);
        Assert.Throws<InvalidDataException>(() => PromptLibrary.ParseFile("f.md", "---\nformat: trendo-skill\nname: X\n---\nText"));
        Assert.Throws<InvalidDataException>(() => PromptLibrary.ParseFile("f.json", "{\"format\":\"trendo-textklassifikator\"}"));
        Assert.Throws<InvalidDataException>(() => PromptLibrary.ParseFile("f.json", "{kaputt"));
        Assert.Throws<InvalidDataException>(() => PromptLibrary.ParseFile("leer.md", "---\nname: Leer\n---\n"));
    }

    [Fact(DisplayName = "T-0740 Import gleicher Name: ohne Antwort Fehler, „Ersetzen“ = neue Version (alte bleibt), „Als Kopie“ = neue Vorlage; bestehende Datei bleibt lesbar")]
    public void Import_conflict()
    {
        var root = Temp();
        try
        {
            var lib = Lib(root);
            var t = lib.Save(null, "Gedicht", "Kreativ", "Schreibe ein Gedicht über {{Tier}}.");
            var e = new PromptFileEntry("gedicht", "Kreativ", "Schreibe ein kurzes Gedicht über {{Tier}} in 4 Zeilen.");
            Assert.Throws<InvalidOperationException>(() => lib.Import(e));
            var r = lib.Import(e, PromptLibrary.ImportMode.Replace);
            Assert.Equal(t.Id, r.Id); Assert.Equal(2, r.Version); Assert.Equal("Schreibe ein Gedicht über {{Tier}}.", r.Versions[0].Text);
            var c = lib.Import(e, PromptLibrary.ImportMode.Copy);
            Assert.Equal("gedicht (Kopie)", c.Name); Assert.NotEqual(t.Id, c.Id);
            Assert.Equal("gedicht (Kopie 2)", lib.Import(e, PromptLibrary.ImportMode.Copy).Name);
            var n = lib.Import(new("Neu", "", "Erkläre {{Thema}}."));
            Assert.Equal(("Neu", "Allgemein", 1), (n.Name, n.Category, n.Version));
            // dieselbe Datei mit einer neuen Bibliothek lesen: alles da, Mitgelieferte auch
            var again = Lib(root);
            Assert.Equal(2, again.FindByName("Gedicht")!.Version);
            Assert.NotNull(again.Get("einfach-erklaeren"));
        }
        finally { Directory.Delete(root, true); }
    }
}
