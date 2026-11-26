using System.Net;
using System.Net.Sockets;
using System.Text.Json.Nodes;
using Trendo.Core;
using Trendo.Core.Lernen;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0782: Kern von „Nachhilfe mit KI“ (Lernpfad, Prüfung, sokratischer Modus, Beherrschung, FSRS-Plan, CSV, Arbeitsblatt).</summary>
public class T0782_Nachhilfe
{
    static ProgramDocument Doc(string fach = "mathe", int klasse = 6) => new("STUDIO-TUTOR-01", "Test", Nachhilfe.Neu(fach, klasse));

    [Fact(DisplayName = "Nachhilfe: Lernpfad je Fach und Klasse – Mathe 6 hat Brüche, Deutsch 3 hat Wortarten, nichts Fremdes")]
    public void Path_per_subject_and_grade()
    {
        Assert.Contains(Nachhilfe.Pfad("mathe", 6), t => t.Id == "m-kuerzen");
        Assert.DoesNotContain(Nachhilfe.Pfad("mathe", 6), t => t.Id == "m-pythagoras");
        Assert.Contains(Nachhilfe.Pfad("deutsch", 3), t => t.Id == "d-wortarten");
        Assert.All(Nachhilfe.Pfad("englisch", 5), t => Assert.Equal("englisch", t.Fach));
        Assert.All(Nachhilfe.Themen, t => Assert.True(t.Generator is not null || t.Items is { Count: >= 4 }));
    }

    [Fact(DisplayName = "Nachhilfe: Prüfung – Zahl mit Komma, Bruch nicht ganz gekürzt zählt nicht, Rest-Schreibweise, Text ohne Groß/klein")]
    public void Checking()
    {
        Assert.True(Nachhilfe.Vergleiche(new("x", ["2,5"], "", [], "zahl"), "2.5").Richtig);
        Assert.True(Nachhilfe.Vergleiche(new("x", ["2,5"], "", [], "zahl"), " 2,5 ").Richtig);
        var b = Nachhilfe.Vergleiche(new("x", ["3/4"], "", [], "bruch"), "6/8");
        Assert.False(b.Richtig); Assert.Contains("weiter kürzen", b.Rueckmeldung);
        Assert.True(Nachhilfe.Vergleiche(new("x", ["3/4"], "", [], "bruch"), "3 / 4").Richtig);
        Assert.True(Nachhilfe.Vergleiche(new("x", ["3 R 2"], "", [], "rest"), "3 Rest 2").Richtig);
        Assert.True(Nachhilfe.Vergleiche(new("x", ["die Bäume", "Bäume"], "", []), "baeume").Richtig);
        Assert.False(Nachhilfe.Vergleiche(new("x", ["Verb"], "", [], "wahl"), "Nomen").Richtig);
    }

    [Fact(DisplayName = "Nachhilfe sokratisch: Lösungsweg erst nach einem Versuch, Hinweise jederzeit, gelöst geht in den Fortschritt")]
    public void Socratic_mode()
    {
        var d = Doc(); Nachhilfe.ThemaWaehlen(d, "m-kuerzen");
        var u = Nachhilfe.NaechsteUebung(d, new Random(4));
        var soll = u["antworten"]![0]!.GetValue<string>();
        var e = Assert.Throws<InvalidOperationException>(() => Nachhilfe.LoesungZeigen(d));
        Assert.Contains("zuerst selbst", e.Message);
        Assert.NotNull(Nachhilfe.Hinweis(d)); Assert.NotNull(Nachhilfe.Hinweis(d)); Assert.Null(Nachhilfe.Hinweis(d));
        Assert.False(Nachhilfe.Pruefen(d, "99/100").Richtig);
        Assert.Contains("Lösung: " + soll, Nachhilfe.LoesungZeigen(d));
        Assert.Throws<InvalidOperationException>(() => Nachhilfe.Pruefen(d, soll)); // abgeschlossen
        var st = Nachhilfe.Stand(d, Nachhilfe.ThemaById("m-kuerzen")!);
        Assert.Equal(1, st.Falsch); Assert.Equal(0, st.Richtig);
        // Undo nimmt Schritt für Schritt zurück
        Assert.True(d.Undo()); Assert.False(Nachhilfe.Uebung(d)!["loesungGezeigt"].AsBool());
        // Ohne sokratischen Modus geht die Lösung sofort
        d.Set("sokratisch", false); Nachhilfe.NaechsteUebung(d, new Random(5));
        Assert.StartsWith("Lösung:", Nachhilfe.LoesungZeigen(d));
    }

    [Fact(DisplayName = "Nachhilfe: Runde von 5 Übungen → FSRS-Termin; Beherrschung steigt (sicher, gemeistert an zwei Tagen) und fällt wieder")]
    public void Mastery_and_schedule()
    {
        var d = Doc(); Nachhilfe.ThemaWaehlen(d, "m-prozent");
        var day1 = new DateTime(2026, 11, 2);
        void Runde(DateTime tag, int richtig)
        {
            for (var i = 0; i < Nachhilfe.RundeLaenge; i++)
            {
                var u = Nachhilfe.NaechsteUebung(d, new Random(i + tag.Day));
                var soll = u["antworten"]![0]!.GetValue<string>();
                if (i < richtig) Assert.True(Nachhilfe.Pruefen(d, soll, tag).Richtig);
                else { Nachhilfe.Pruefen(d, "-1", tag); Nachhilfe.LoesungZeigen(d, tag); }
            }
        }
        var t = Nachhilfe.ThemaById("m-prozent")!;
        Assert.Equal("neu", Nachhilfe.Stand(d, t).Stufe);
        Runde(day1, 5);
        var s1 = Nachhilfe.Stand(d, t); Assert.Equal("sicher", s1.Stufe); Assert.NotNull(s1.Faellig); Assert.True(s1.Faellig > day1);
        Runde(s1.Faellig!.Value, 5);
        var s2 = Nachhilfe.Stand(d, t); Assert.Equal("gemeistert", s2.Stufe);
        Assert.True((s2.Faellig!.Value - s1.Faellig!.Value).TotalDays > (s1.Faellig.Value - day1).TotalDays); // Abstand wächst
        Runde(s2.Faellig!.Value, 1);
        var s3 = Nachhilfe.Stand(d, t); Assert.Equal("versucht", s3.Stufe); Assert.True((s3.Faellig!.Value - s2.Faellig!.Value).TotalDays <= 2); // falsch → bald wieder
        Assert.Equal(t.Id, Nachhilfe.Wiederholungsplan(d)[0].Thema.Id);
    }

    [Fact(DisplayName = "Nachhilfe: KI-Antwort ohne Links; verrät sie die Lösung einer offenen Übung, wird sie zurückgehalten; die Frage an die KI enthält die Lösung nicht")]
    public void Ki_filter()
    {
        var d = Doc(); Nachhilfe.ThemaWaehlen(d, "m-einmaleins");
        var u = Nachhilfe.NaechsteUebung(d, new Random(1)); var soll = u["antworten"]![0]!.GetValue<string>();
        var q = Nachhilfe.KiFrage(d, "Wie geht das? Schau auf www.example.com");
        Assert.DoesNotContain("Lösung", q); Assert.DoesNotContain("example.com", q);
        Assert.DoesNotContain(u["erklaerung"]!.GetValue<string>(), q);
        var (t1, held) = Nachhilfe.KiAntwortFiltern(d, $"Das Ergebnis ist {soll}.");
        Assert.True(held); Assert.DoesNotContain(soll, t1);
        var (t2, held2) = Nachhilfe.KiAntwortFiltern(d, "<think>intern</think>Denk an Gruppen. Mehr unter https://example.org/x oder info@example.org");
        Assert.False(held2); Assert.DoesNotContain("http", t2); Assert.DoesNotContain("@", t2); Assert.DoesNotContain("intern", t2);
    }

    [Fact(DisplayName = "Nachhilfe: eigene/KI-Übung angelegt (Links entfernt, verratender Hinweis fällt weg), CSV hin und zurück, Arbeitsblatt als ODT lesbar")]
    public void Own_items_csv_sheet()
    {
        var d = Doc(); Nachhilfe.ThemaWaehlen(d, "m-prozent");
        Nachhilfe.UebungAnlegen(d, "m-prozent", "Wie viel sind 10 % von 80? Siehe www.x.de", "8", "80 : 10 = 8", ["Teile durch 10.", "Es ist 8."], "ki");
        var e = Assert.Single(Nachhilfe.Eigene(d, "m-prozent"));
        Assert.DoesNotContain("www", e.Item.Frage); Assert.Equal(["Teile durch 10."], e.Item.Hinweise); Assert.Equal("zahl", e.Item.Art);
        Assert.Equal("ki", e.Herkunft);
        Assert.Throws<ArgumentException>(() => Nachhilfe.UebungAnlegen(d, "m-prozent", "Frage ohne Antwort", "", "", [], "ki"));
        var csv = Nachhilfe.CsvExport(d);
        var d2 = Doc(); Nachhilfe.ThemaWaehlen(d2, "m-prozent");
        var (n, skip) = Nachhilfe.CsvImport(d2, csv + "Prozent berechnen;Mit Link;1;;https://böse.example\r\nGibtsNicht;F?;1\r\n");
        Assert.Equal(1, n); Assert.Equal(2, skip.Count);
        Assert.Equal(e.Item.Frage, Nachhilfe.Eigene(d2, "m-prozent")[0].Item.Frage);
        // Echtlauf-Befund: lokale Modelle schreiben LaTeX und zählen ungekürzte Brüche als richtig auf – Schreibweise wird einfach, geprüft wird gegen die erste (gekürzte) Antwort
        Nachhilfe.UebungAnlegen(d, "m-kuerzen", @"Kürze den Bruch \(\frac{12}{18}\).", @"\(\frac{2}{3}\)|\(\frac{6}{9}\)", "ggT ist 6", [], "ki", aktivieren: false);
        var li = Nachhilfe.Eigene(d, "m-kuerzen").Single().Item;
        Assert.Equal("Kürze den Bruch 12/18.", li.Frage); Assert.Equal(["2/3", "6/9"], li.Antworten); Assert.Equal("bruch", li.Art);
        Assert.False(Nachhilfe.Vergleiche(li, "6/9").Richtig); Assert.True(Nachhilfe.Vergleiche(li, "2/3").Richtig);
        var sheet = Nachhilfe.Arbeitsblatt(d, 6);
        var f = Path.Combine(Path.GetTempPath(), "trendo-t0782-" + Guid.NewGuid().ToString("N")[..6] + ".odt");
        try
        {
            Trendo.Core.Office.OdtFormat.Write(sheet, f);
            var back = Trendo.Core.Office.OdtFormat.Read(f).PlainText();
            Assert.Contains("Arbeitsblatt: Prozent berechnen", back); Assert.Contains("Lösungen", back); Assert.Contains("10 % von 80", back);
        }
        finally { File.Delete(f); }
    }
}

/// <summary>T-0782: Kern von „Programmieren lernen“ – echte Tests im AppContainer (ohne Netz, Zeitgrenze, nur Temp-Ordner beschreibbar). Nichts ersetzt;
/// übersprungen nur, wenn das mitgelieferte Python fehlt.</summary>
[Collection("T0782-Sandbox")]
public class T0782_CodeLernen
{
    static string? Python(out string root)
    {
        root = Path.Combine(Path.GetTempPath(), "trendo-t0782-core-" + Guid.NewGuid().ToString("N")[..6]);
        if (!OperatingSystem.IsWindows()) return null;
        return LernSandbox.PrepareRuntime(root).Exe;
    }
    /// <summary>Echt-Test nur mit dem mitgelieferten Python (sonst übersprungen mit Grund).</summary>
    public sealed class PyFactAttribute : FactAttribute { public PyFactAttribute() { if (!OperatingSystem.IsWindows() || LernSandbox.BundledPython() is null) Skip = "Übersprungen – mitgeliefertes Python (TrendoWerkzeuge, Ordner python) fehlt auf diesem PC."; } }
    static string Work(string root) => Path.Combine(root, "lauf-" + Guid.NewGuid().ToString("N")[..6]);

    [Fact(DisplayName = "Programmieren lernen: Lernpfad – jede Aufgabe hat Tests und Hinweise, Freischaltung über die Voraussetzung")]
    public void Track_unlock()
    {
        Assert.All(CodeLernen.Track, a => { Assert.Contains("unittest", a.Tests); Assert.True(a.Hinweise.Count >= 2); Assert.True(a.Braucht is null || CodeLernen.ById(a.Braucht) is not null); });
        var d = new ProgramDocument("STUDIO-CODELEARN-01", "T", CodeLernen.Neu());
        Assert.Throws<InvalidOperationException>(() => CodeLernen.Waehlen(d, "summe"));
        CodeLernen.Eintragen(d, new("pass", [new("t", "pass", "", "")], "", 0.1));
        CodeLernen.Waehlen(d, "summe");
        Assert.Equal("summe", CodeLernen.Aktuell(d).Id);
        Assert.True(CodeLernen.Fortschritt(d) > 0);
    }

    [Fact(DisplayName = "Programmieren lernen: KI-Hinweis mit fertigem Code oder Link abgelehnt, Denkanstoß angenommen")]
    public void Ki_hint_rules()
    {
        var a = CodeLernen.Track[0];
        Assert.NotNull(CodeLernen.HinweisProblem(a, "```python\ndef gruss(name):\n    return 'x'\n```"));
        Assert.NotNull(CodeLernen.HinweisProblem(a, "def gruss(name):\n    return f'Hallo, {name}!'"));
        Assert.NotNull(CodeLernen.HinweisProblem(a, "Lies mal auf https://docs.python.org nach"));
        Assert.Null(CodeLernen.HinweisProblem(a, "Was passiert, wenn du statt print das Wort return nimmst?"));
    }

    [PyFact(DisplayName = "Programmieren lernen ECHT: Startcode fällt durch, richtige Lösung besteht, Syntaxfehler = error – im AppContainer mit dem mitgelieferten Python")]
    public void Real_tests_pass_and_fail()
    {
        var py = Python(out var root)!;
        try
        {
            var a = CodeLernen.Track[0];
            var r0 = CodeLernen.Testen(a, a.Start, py, Work(root));
            Assert.True(r0.Status == "fail", r0.Status + ": " + r0.Meldung); Assert.Equal(3, r0.Tests.Count); Assert.Equal(0, r0.Bestanden);
            var r1 = CodeLernen.Testen(a, "def gruss(name):\n    print('Test-Ausgabe')\n    return f'Hallo, {name}!'\n", py, Work(root));
            Assert.Equal("pass", r1.Status); Assert.Equal(3, r1.Bestanden); Assert.Contains(r1.Tests, t => t.Ausgabe.Contains("Test-Ausgabe"));
            var r2 = CodeLernen.Testen(a, "def gruss(name)\n    return name\n", py, Work(root));
            Assert.Equal("error", r2.Status); Assert.Contains("Syntax", r2.Meldung); Assert.DoesNotContain(root, r2.Meldung);
            var note = CodeLernen.Track.First(x => x.Id == "note");
            var r3 = CodeLernen.Testen(note, "def note(p):\n    return 1 if p >= 90 else 2 if p >= 75 else 3 if p >= 60 else 4 if p >= 45 else 5 if p > 25 else 6\n", py, Work(root));
            Assert.Equal("fail", r3.Status); Assert.Contains(r3.Tests, t => t.Name.Contains("punkte=25") && t.Status == "fail");
        }
        finally { try { Directory.Delete(root, true); } catch (IOException) { } }
    }

    [PyFact(DisplayName = "Programmieren lernen ECHT feindlich: Endlosschleife → Zeitgrenze, kein Netz (auch nicht Loopback), kein Schreiben/Lesen außerhalb, kein weiterer Prozess")]
    public void Real_sandbox_hostile()
    {
        var py = Python(out var root)!;
        var outside = Path.Combine(root, "draussen"); Directory.CreateDirectory(outside);
        var secret = Path.Combine(outside, "geheim.txt"); File.WriteAllText(secret, "geheim");
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        try
        {
            var a = CodeLernen.Track[0];
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var loop = CodeLernen.Testen(a, "while True:\n    pass\n", py, Work(root), timeoutMs: 3000);
            Assert.Equal("timeout", loop.Status); Assert.True(sw.Elapsed.TotalSeconds < 15);

            string Probe(string body, bool frei = false)
            {
                var code = "def gruss(name):\n    return f'Hallo, {name}!'\n" + "def probe():\n" + string.Join("\n", body.Split('\n').Select(l => "    " + l)) + "\nprint('PROBE:' + str(probe()))\n";
                var w = Work(root); Directory.CreateDirectory(w);
                File.WriteAllText(Path.Combine(w, "p.py"), code);
                string text;
                if (frei)
                {   // Gegenprobe ohne Container: dasselbe Python, derselbe Code
                    var psi = new System.Diagnostics.ProcessStartInfo(py, "-I -B p.py") { WorkingDirectory = w, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
                    using var p = System.Diagnostics.Process.Start(psi)!; text = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd(); p.WaitForExit(8000);
                }
                else { var r = LernSandbox.Run(py, ["-I", "-B", "p.py"], w, 8000); text = r.Out + r.Err; }
                try { Directory.Delete(w, true); } catch (IOException) { }
                return text;
            }
            var netBody = $"import socket\ntry:\n    s = socket.create_connection(('127.0.0.1', {port}), timeout=3)\n    return 'VERBUNDEN'\nexcept OSError as e:\n    return 'GESPERRT ' + type(e).__name__";
            Assert.Contains("PROBE:VERBUNDEN", Probe(netBody, frei: true)); // Gegenprobe: ohne Container kommt die Verbindung zustande
            var net = Probe(netBody);
            Assert.Contains("PROBE:GESPERRT", net);
            Assert.Contains("PROBE:" + File.ReadAllText(secret), Probe($"return open(r'{secret}').read()", frei: true)); // Gegenprobe Lesen
            var dns = Probe("import socket\ntry:\n    socket.getaddrinfo('example.org', 443)\n    s = socket.create_connection(('93.184.215.14', 443), timeout=3)\n    return 'VERBUNDEN'\nexcept OSError as e:\n    return 'GESPERRT'");
            Assert.Contains("PROBE:GESPERRT", dns);
            var write = Probe($"try:\n    open(r'{Path.Combine(outside, "neu.txt")}', 'w').write('x')\n    return 'GESCHRIEBEN'\nexcept OSError:\n    return 'GESPERRT'");
            Assert.Contains("PROBE:GESPERRT", write); Assert.False(File.Exists(Path.Combine(outside, "neu.txt")));
            var read = Probe($"try:\n    return open(r'{secret}').read()\nexcept OSError:\n    return 'GESPERRT'");
            Assert.Contains("PROBE:GESPERRT", read);
            var home = Probe($"import os\ntry:\n    open(os.path.join(r'{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}', 'trendo-t0782-probe.txt'), 'w').write('x')\n    return 'GESCHRIEBEN'\nexcept OSError:\n    return 'GESPERRT'");
            Assert.Contains("PROBE:GESPERRT", home);
            var proc = Probe("import subprocess\ntry:\n    subprocess.run(['cmd', '/c', 'echo hi'], capture_output=True, timeout=3)\n    return 'GESTARTET'\nexcept Exception as e:\n    return 'GESPERRT'");
            Assert.Contains("PROBE:GESPERRT", proc);
            var own = Probe("open('eigen.txt', 'w').write('ok')\nreturn open('eigen.txt').read()");
            Assert.Contains("PROBE:ok", own); // Gegenprobe: im eigenen Arbeitsordner geht es
        }
        finally { listener.Stop(); try { Directory.Delete(root, true); } catch (IOException) { } }
    }
}

/// <summary>T-0782: Exportdateien für die Fremdprogramm-Belege (LibreOffice, System-Python) – schreibt nur mit TRENDO_T0782_BELEG=&lt;Ordner&gt;.</summary>
public class T0782_Belege
{
    public sealed class BelegFactAttribute : FactAttribute { public BelegFactAttribute() { if (Environment.GetEnvironmentVariable("TRENDO_T0782_BELEG") is not { Length: > 0 }) Skip = "Übersprungen – nur mit TRENDO_T0782_BELEG=<Ordner>."; } }

    [BelegFact(DisplayName = "T-0782 Belege: Arbeitsblatt ODT/DOCX, Übungen CSV, Lösung mit Tests .py")]
    public void Write_exports()
    {
        var dir = Directory.CreateDirectory(Environment.GetEnvironmentVariable("TRENDO_T0782_BELEG")!).FullName;
        var d = new ProgramDocument("STUDIO-TUTOR-01", "Beleg", Nachhilfe.Neu("mathe", 6));
        Nachhilfe.ThemaWaehlen(d, "m-kuerzen");
        Nachhilfe.UebungAnlegen(d, "m-kuerzen", "Kürze 12/18 so weit wie möglich.", "2/3", "12 und 18 sind durch 6 teilbar: 2/3.", ["Welche Zahl teilt 12 und 18?"], "eigen");
        Trendo.Core.Office.OdtFormat.Write(Nachhilfe.Arbeitsblatt(d, 8, 20261126), Path.Combine(dir, "arbeitsblatt-brueche.odt"));
        Trendo.Core.Office.DocxFormat.Write(Nachhilfe.Arbeitsblatt(d, 8, 20261126), Path.Combine(dir, "arbeitsblatt-brueche.docx"));
        File.WriteAllText(Path.Combine(dir, "eigene-uebungen.csv"), Nachhilfe.CsvExport(d), new System.Text.UTF8Encoding(true));
        var c = new ProgramDocument("STUDIO-CODELEARN-01", "Beleg", CodeLernen.Neu());
        CodeLernen.CodeSetzen(c, "def gruss(name):\n    return f'Hallo, {name}!'\n");
        File.WriteAllText(Path.Combine(dir, "begruessung.py"), CodeLernen.Export(c), new System.Text.UTF8Encoding(false));
        // Rundreise: die CSV wieder einlesen
        var back = new ProgramDocument("STUDIO-TUTOR-01", "Zurück", Nachhilfe.Neu("mathe", 6));
        Assert.Equal(1, Nachhilfe.CsvImport(back, File.ReadAllText(Path.Combine(dir, "eigene-uebungen.csv"))).Uebernommen);
    }
}
