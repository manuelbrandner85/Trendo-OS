using System.Text.Json.Nodes;
using Trendo.Core;
using Trendo.Core.Lernen;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0782 Gegenprüfung: Regressionstests für M1–M8, N2, N5 und das App-Profil (Kern). Die ECHT-Fälle laufen mit dem mitgelieferten Python im AppContainer.</summary>
[Collection("T0782-Sandbox")]
public class T0782_Gegenpruefung : T0782ProfilTest
{
    static ProgramDocument Doc(string thema)
    {
        var d = new ProgramDocument("STUDIO-TUTOR-01", "T", Nachhilfe.Neu("mathe", 6));
        Nachhilfe.ThemaWaehlen(d, thema);
        return d;
    }
    static Nachhilfe.Item Z(string a, string? einheit = null) => new("x", [a], "", [], "zahl", einheit);

    // ───── M8 Antwortprüfung ─────
    [Fact(DisplayName = "M8: Einheit passend, „c = 5“, „x = −3“, Unicode-Minus, „4 R 0“, „3, Rest 2“, e-Schreibweise abgelehnt, −3/−4 = 3/4, 0,75 als Bruchfrage gemeldet, Überlauf ohne Absturz")]
    public void M8_answer_checks()
    {
        Assert.True(Nachhilfe.Vergleiche(Z("5", "cm"), "5 cm").Richtig);
        Assert.True(Nachhilfe.Vergleiche(Z("5", "cm"), "5cm").Richtig);
        var falscheEinheit = Nachhilfe.Vergleiche(Z("5", "cm"), "5 m"); Assert.False(falscheEinheit.Richtig); Assert.False(falscheEinheit.Zaehlt); Assert.Contains("cm", falscheEinheit.Rueckmeldung);
        Assert.True(Nachhilfe.Vergleiche(Z("5", "cm"), "c = 5").Richtig);
        Assert.True(Nachhilfe.Vergleiche(Z("-3"), "x = -3").Richtig);
        Assert.True(Nachhilfe.Vergleiche(Z("-3"), "\u22123").Richtig);
        Assert.True(Nachhilfe.Vergleiche(new("x", ["4"], "", [], "rest"), "4 R 0").Richtig);
        Assert.True(Nachhilfe.Vergleiche(new("x", ["3 R 2"], "", [], "rest"), "3, Rest 2").Richtig);
        var exp = Nachhilfe.Vergleiche(Z("21"), "2,1e1"); Assert.False(exp.Richtig); Assert.False(exp.Zaehlt);
        Assert.True(Nachhilfe.Vergleiche(new("x", ["3/4"], "", [], "bruch"), "-3/-4").Richtig);
        var dez = Nachhilfe.Vergleiche(new("x", ["3/4"], "", [], "bruch"), "0,75"); Assert.False(dez.Richtig); Assert.False(dez.Zaehlt); Assert.Contains("Bruch", dez.Rueckmeldung);
        var big = Nachhilfe.Vergleiche(new("x", ["3/4"], "", [], "bruch"), "99999999999999999999999/4"); Assert.False(big.Richtig); Assert.False(big.Zaehlt);
        Assert.False(Nachhilfe.Vergleiche(Z("21"), "9999999999999999999999999999999999999999").Richtig);
        // Import: Soll-Antworten vereinheitlicht
        Assert.Equal("2/3", Nachhilfe.NormSoll(" 2 / 3 ")); Assert.Equal("-3/4", Nachhilfe.NormSoll("3/-4")); Assert.Equal("-5", Nachhilfe.NormSoll("\u22125")); Assert.Equal("2.5", Nachhilfe.NormSoll("2,5"));
        var d = Doc("m-kuerzen");
        var (n, _) = Nachhilfe.CsvImport(d, "Thema;Frage;Antwort\r\nBrüche kürzen;Kürze 6/8;3 / 4\r\n");
        Assert.Equal(1, n); Assert.Equal(["3/4"], Nachhilfe.Eigene(d, "m-kuerzen")[0].Item.Antworten);
    }

    // ───── N5 Großschreibung ─────
    [Fact(DisplayName = "N5: Deutsch – „bäume“ ist richtig, aber mit Hinweis auf die Großschreibung; korrekt geschrieben ohne Hinweis")]
    public void N5_case_hint()
    {
        var i = new Nachhilfe.Item("Mehrzahl von der Baum?", ["die Bäume", "Bäume"], "", []);
        var low = Nachhilfe.Vergleiche(i, "bäume"); Assert.True(low.Richtig); Assert.Contains("Groß- und Kleinschreibung", low.Rueckmeldung);
        var ok = Nachhilfe.Vergleiche(i, "Bäume"); Assert.True(ok.Richtig); Assert.DoesNotContain("Kleinschreibung", ok.Rueckmeldung);
    }

    // ───── M6 Sokratischer Modus ─────
    [Fact(DisplayName = "M6: Strg+Z setzt Versuch und „Lösung gesehen“ nicht zurück; nach gesehener Lösung zählt richtig nicht; leere/Unsinns-Eingaben sind kein Versuch")]
    public void M6_undo_cannot_reset()
    {
        var d = Doc("m-kuerzen");
        var u = Nachhilfe.NaechsteUebung(d, new Random(3));
        var soll = u["antworten"]![0]!.GetValue<string>();
        Assert.False(Nachhilfe.Pruefen(d, "   ").Zaehlt);
        Assert.False(Nachhilfe.Pruefen(d, "???").Zaehlt);
        Assert.False(Nachhilfe.Pruefen(d, "0,5").Zaehlt); // Bruch gefragt
        Assert.Equal(0, Nachhilfe.VersucheEcht(d));
        Assert.Throws<InvalidOperationException>(() => Nachhilfe.LoesungZeigen(d));
        Assert.False(Nachhilfe.Pruefen(d, "99/100").Richtig);
        Assert.True(d.Undo()); // Versuch zurücknehmen …
        Assert.Equal(1, Nachhilfe.VersucheEcht(d)); // … bleibt gezählt
        Nachhilfe.LoesungZeigen(d); // erlaubt
        Assert.True(d.Undo()); // „Lösungsweg gezeigt“ zurücknehmen
        Assert.True(Nachhilfe.LoesungGesehen(d));
        var p = Nachhilfe.Pruefen(d, soll);
        Assert.True(p.Richtig); Assert.Contains("zählt aber nicht", p.Rueckmeldung);
        var st = Nachhilfe.Stand(d, Nachhilfe.ThemaById("m-kuerzen")!);
        Assert.Equal(0, st.Richtig); Assert.Equal(1, st.Falsch);
        // Speichern und Laden: die Zähler stehen im Dokument (Meta), nicht in der Rückgängig-Liste
        var f = Path.Combine(Path.GetTempPath(), "t0782-m6-" + Guid.NewGuid().ToString("N")[..6] + ProgramDocument.Extension);
        try { d.Save(f); var back = ProgramDocument.Load(f, "STUDIO-TUTOR-01"); Assert.True(Nachhilfe.LoesungGesehen(back)); }
        finally { File.Delete(f); }
    }

    [Fact(DisplayName = "M6: KI-Sicht ohne Antworten, Lösungsweg, Hinweise, Eingaben und Verlauf – auch bei eigenen Übungen")]
    public void M6_ki_view()
    {
        var d = Doc("m-prozent");
        Nachhilfe.UebungAnlegen(d, "m-prozent", "Wie viel sind 10 % von 70?", "7", "70 : 10 = 7", ["Teile durch zehn."], "eigen");
        Nachhilfe.Pruefen(d, "8");
        var json = Nachhilfe.KiSicht(d.Data).ToJsonString();
        Assert.Contains("Wie viel sind 10 % von 70?", json);
        Assert.DoesNotContain("antworten", json); Assert.DoesNotContain("70 : 10", json); Assert.DoesNotContain("Teile durch", json); Assert.DoesNotContain("eingaben", json); Assert.DoesNotContain("verlauf", json);
        Assert.Contains("antworten", d.Data.ToJsonString()); // das Dokument selbst bleibt vollständig
    }

    // ───── M7 Lösung zurückhalten ─────
    [Fact(DisplayName = "M7: verratene Lösung erkannt – Rest, Bruch mit Leerzeichen, Zahlwort, Unicode-Minus, Punkt/Komma, w-e-n-t, ae statt ä, Rechnung „20 + 1“; harmlose Texte bleiben")]
    public void M7_reveal_variants()
    {
        Assert.True(Nachhilfe.VerraetLoesung("Das macht 3 Rest 2.", ["3 R 2"]));
        Assert.True(Nachhilfe.VerraetLoesung("Ergebnis: 3, Rest 2", ["3 R 2"]));
        Assert.True(Nachhilfe.VerraetLoesung("Gekürzt ist das 2 / 3.", ["2/3"]));
        Assert.True(Nachhilfe.VerraetLoesung("Das sind einundzwanzig.", ["21"]));
        Assert.True(Nachhilfe.VerraetLoesung("x ist \u22123.", ["-3"]));
        Assert.True(Nachhilfe.VerraetLoesung("Das macht 2.5 Stück", ["2,5"]));
        Assert.True(Nachhilfe.VerraetLoesung("Das macht 2,5 Stück", ["2.5"]));
        Assert.True(Nachhilfe.VerraetLoesung("Schreib w-e-n-t.", ["went"]));
        Assert.True(Nachhilfe.VerraetLoesung("Schreib w e n t.", ["went"]));
        Assert.True(Nachhilfe.VerraetLoesung("Die Antwort ist Baeume.", ["Bäume"]));
        Assert.True(Nachhilfe.VerraetLoesung("Rechne einfach 20 + 1.", ["21"]));
        Assert.True(Nachhilfe.VerraetLoesung("Das ist 3 · 7.", ["21"]));
        Assert.False(Nachhilfe.VerraetLoesung("Zähle in Siebenerschritten.", ["21"]));
        Assert.False(Nachhilfe.VerraetLoesung("Teile Zähler und Nenner durch 6.", ["2/3"]));
        Assert.False(Nachhilfe.VerraetLoesung("Denk an die Vergangenheit von go.", ["went"]));
        Assert.Equal(21m, Nachhilfe.Rechne("20+1")); Assert.Equal(23m, Nachhilfe.Rechne("2+3*7")); Assert.Null(Nachhilfe.Rechne("1/0"));
    }

    // ───── M5 Gefälschte Ergebnisse, kaputte Zeilen ─────
    [Fact(DisplayName = "M5: Auswertung nimmt keine Datei des Lerncodes; kaputtes JSON und gefälschte Zeilen ohne erwartete Werte bestehen nicht; Meldungen gekürzt")]
    public void M5_parse_robust()
    {
        var a = CodeLernen.Track[0];
        var lines = string.Join("\n",
            CodeLernen.Marke + "{kaputt",
            CodeLernen.Marke + "{\"name\": 5}",
            CodeLernen.Marke + "{\"status\":\"pass\"}",
            CodeLernen.Marke + "{\"name\":\"lea\",\"wert\":\"falsch\",\"ausgabe\":\"" + new string('x', 5000) + "\"}",
            CodeLernen.Marke + "{\"name\":\"tom\",\"ausnahme\":\"" + new string('y', 5000) + "\"}");
        var r = CodeLernen.Auswerten(a, new LernSandbox.Result(0, false, false, 0.1, lines, "", "ok"));
        Assert.Equal("fail", r.Status);
        Assert.Equal(0, r.Bestanden);
        Assert.All(r.Tests, t => { Assert.True(t.Meldung.Length <= 802); Assert.True(t.Ausgabe.Length <= 502); });
        Assert.Contains(r.Tests, t => t.Name == "gibt_text_zurueck" && t.Status == "error"); // keine Rückmeldung = nicht bestanden
    }

    public sealed class PyFactAttribute : FactAttribute { public PyFactAttribute() { if (!OperatingSystem.IsWindows() || LernSandbox.BundledPython() is null) Skip = "Übersprungen – mitgeliefertes Python (Trendo-Werkzeugordner) fehlt auf diesem PC."; } }
    static string Py(out string root) { root = Path.Combine(Path.GetTempPath(), "trendo-t0782-gp-" + Guid.NewGuid().ToString("N")[..6]); return LernSandbox.PrepareRuntime(root).Exe!; }
    static string Work(string root) => Path.Combine(root, "lauf-" + Guid.NewGuid().ToString("N")[..6]);
    static void Clean(string root) { try { Directory.Delete(root, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }

    [PyFact(DisplayName = "M5 ECHT: Lerncode schreibt „ergebnis.json: pass“, fälscht Zeilen und verbiegt unittest/json – trotzdem nicht bestanden; richtige Lösung besteht")]
    public void M5_forgery_real()
    {
        var py = Py(out var root);
        try
        {
            var a = CodeLernen.Track[0];
            var boese = "import os, json, unittest\n"
                + "open('ergebnis.json','w').write('{\"status\":\"pass\",\"tests\":[]}')\n"
                + "for n in ['lea','tom','gibt_text_zurueck']:\n    os.write(1, ('@@T0782@@' + json.dumps({'name': n, 'wert': 'bestanden', 'typ': 'str'}) + '\\n').encode())\n"
                + "unittest.TestCase.assertEqual = lambda *a, **k: None\n"
                + "json.dumps = lambda *a, **k: '{\"name\":\"lea\",\"wert\":\"x\"}'\n"
                + "def gruss(name):\n    return 'nein'\n";
            var r = CodeLernen.Testen(a, boese, py, Work(root));
            Assert.NotEqual("pass", r.Status);
            Assert.True(r.Tests.Count(t => t.Name != "gibt_text_zurueck" && t.Status == "pass") == 0, string.Join("|", r.Tests.Select(t => t.Name + "=" + t.Status)));
            var gut = CodeLernen.Testen(a, "def gruss(name):\n    return f'Hallo, {name}!'\n", py, Work(root));
            Assert.Equal("pass", gut.Status); Assert.Equal(3, gut.Bestanden);
        }
        finally { Clean(root); }
    }

    [PyFact(DisplayName = "M3 ECHT: Ausgabeflut → Abbruch „limit“, gelesen wird nur das Ende (höchstens 256 KB)")]
    public void M3_output_flood()
    {
        var py = Py(out var root);
        try
        {
            var w = Work(root); Directory.CreateDirectory(w);
            File.WriteAllText(Path.Combine(w, "p.py"), "import os\nwhile True:\n    os.write(1, b'x' * 65536)\n");
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var r = LernSandbox.Run(py, ["-I", "-B", "p.py"], w, new LernSandbox.Limits(TimeoutMs: 20_000, OutputBytes: 4L << 20));
            Assert.True(r.OutputLimit, r.Text); Assert.False(r.TimedOut); Assert.True(sw.Elapsed.TotalSeconds < 15);
            Assert.True(r.Out.Length <= 300 * 1024, r.Out.Length.ToString());
            Assert.False(File.Exists(Path.Combine(w, "ausgabe.txt"))); // keine Ausgabedatei mehr: die Ausgabe läuft durch eine Pipe, Trendo behält nur das Ende
            var t = CodeLernen.Testen(CodeLernen.Track[0], "import sys\nwhile True:\n    sys.__stdout__.write('y' * 65536)\n", py, Work(root), new LernSandbox.Limits(TimeoutMs: 20_000, OutputBytes: 2L << 20));
            Assert.Equal("limit", t.Status);
        }
        finally { Clean(root); }
    }

    [PyFact(DisplayName = "M4 ECHT: Dateien über der Grenze → Abbruch; Temp-Ordner des Containers ist nach jedem Lauf leer")]
    public void M4_disk_and_actemp()
    {
        var py = Py(out var root);
        try
        {
            var sid = LernSandbox.Sid();
            var acTemp = Path.Combine(LernSandbox.ContainerFolder(sid)!, "Temp");
            Directory.CreateDirectory(acTemp);
            var marker = Path.Combine(acTemp, "t0782-rest.txt"); File.WriteAllText(marker, "alt");
            var w = Work(root); Directory.CreateDirectory(w);
            File.WriteAllText(Path.Combine(w, "p.py"), "import sys\np = sys.argv[1]\nwith open(p + '/gross.bin', 'wb') as f:\n    for i in range(200):\n        f.write(b'z' * (1 << 20))\n");
            var r = LernSandbox.Run(py, ["-I", "-B", "p.py", acTemp], w, new LernSandbox.Limits(TimeoutMs: 30_000, DiskBytes: 20L << 20));
            Assert.True(r.OutputLimit, r.Text + " | " + r.Err);
            Assert.False(File.Exists(marker));
            Assert.Empty(Directory.EnumerateFileSystemEntries(acTemp));
            var w2 = Work(root); Directory.CreateDirectory(w2);
            File.WriteAllText(Path.Combine(w2, "p.py"), "with open('tmp/gross.bin', 'wb') as f:\n    for i in range(200):\n        f.write(b'z' * (1 << 20))\n");
            Assert.True(LernSandbox.Run(py, ["-I", "-B", "p.py"], w2, new LernSandbox.Limits(TimeoutMs: 30_000, DiskBytes: 20L << 20)).OutputLimit);
        }
        finally { Clean(root); }
    }

    [PyFact(DisplayName = "M1 ECHT: eigener Desktop – GetDC(0)/GetPixel liefern nicht den echten Bildschirm, keine fremden Fenster (Gegenprobe ohne Container liest Pixel)")]
    public void M1_own_desktop()
    {
        var py = Py(out var root);
        try
        {
            const string probe = "import ctypes\nu = ctypes.windll.user32\ng = ctypes.windll.gdi32\ndc = u.GetDC(0)\npx = [g.GetPixel(dc, x, y) & 0xFFFFFFFF for x, y in ((5, 5), (100, 100), (400, 300), (800, 600))]\n"
                + "n = [0]\nCB = ctypes.WINFUNCTYPE(ctypes.c_bool, ctypes.c_void_p, ctypes.c_void_p)\ndef cb(h, l):\n    if u.IsWindowVisible(h): n[0] += 1\n    return True\nu.EnumWindows(CB(cb), 0)\n"
                + "print('PIXEL', px)\nprint('UNGUELTIG', all(p == 0xFFFFFFFF for p in px))\nprint('FENSTER', n[0])\n";
            var w = Work(root); Directory.CreateDirectory(w); File.WriteAllText(Path.Combine(w, "p.py"), probe);
            var r = LernSandbox.Run(py, ["-I", "-B", "p.py"], w, 15_000);
            Assert.Contains("UNGUELTIG True", r.Out + r.Err);
            var fenster = int.Parse(System.Text.RegularExpressions.Regex.Match(r.Out, @"FENSTER (\d+)").Groups[1].Value);
            // Gegenprobe ohne Container: derselbe Code sieht den echten Bildschirm
            var w2 = Work(root); Directory.CreateDirectory(w2); File.WriteAllText(Path.Combine(w2, "p.py"), probe);
            var psi = new System.Diagnostics.ProcessStartInfo(py, "-I -B p.py") { WorkingDirectory = w2, RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
            using var p = System.Diagnostics.Process.Start(psi)!; var outside = p.StandardOutput.ReadToEnd(); p.WaitForExit(15_000);
            Assert.Contains("UNGUELTIG False", outside);
            var fensterDraussen = int.Parse(System.Text.RegularExpressions.Regex.Match(outside, @"FENSTER (\d+)").Groups[1].Value);
            Assert.True(fenster < fensterDraussen, $"im Container {fenster}, draußen {fensterDraussen}");
            Assert.Equal(0, fenster);
        }
        finally { Clean(root); }
    }

    [PyFact(DisplayName = "N2 ECHT: Speichergrenze wird als Speichergrenze gemeldet (MemoryError erkannt)")]
    public void N2_memory()
    {
        var py = Py(out var root);
        try
        {
            var w = Work(root); Directory.CreateDirectory(w);
            File.WriteAllText(Path.Combine(w, "p.py"), "x = bytearray(700 * 1024 * 1024)\n");
            var r = LernSandbox.Run(py, ["-I", "-B", "p.py"], w, new LernSandbox.Limits(TimeoutMs: 20_000, MemoryBytes: 256L << 20));
            Assert.True(r.MemoryLimit, r.Text + " | " + r.Err); Assert.Contains("Speichergrenze", r.Text);
            var t = CodeLernen.Testen(CodeLernen.Track[0], "x = bytearray(700 * 1024 * 1024)\n", py, Work(root), new LernSandbox.Limits(MemoryBytes: 256L << 20));
            Assert.Contains("Speichergrenze", t.Meldung);
        }
        finally { Clean(root); }
    }

    [PyFact(DisplayName = "App-Profil: RemoveProfile entfernt ein (eigenes Test-)Profil; das Profil der echten Installation wird hier nicht angefasst")]
    public void Profile_remove()
    {
        if (!OperatingSystem.IsWindows()) return;
        var name = "Trendo.Lernen.Test" + Guid.NewGuid().ToString("N")[..8];
        LernSandbox.Sid(name);
        Assert.True(LernSandbox.ProfileExists(name));
        Assert.Contains("entfernt", LernSandbox.RemoveProfile(name));
        Assert.False(LernSandbox.ProfileExists(name));
        Assert.Contains("nicht vorhanden", LernSandbox.RemoveProfile(name));
    }

    [Fact(DisplayName = "M2: Frage an die KI enthält ohne Freigabe keine Programmausgabe und keine Testmeldungen")]
    public void M2_no_output_to_ki()
    {
        var d = new ProgramDocument("STUDIO-CODELEARN-01", "T", CodeLernen.Neu());
        CodeLernen.Eintragen(d, new("fail", [new("lea", "fail", "gruss(\"Lea\") ergab 'PC-GEHEIM', erwartet 'Hallo, Lea!'.", "AUSGABE-GEHEIM")], "MELDUNG-GEHEIM", 0.1));
        var q = CodeLernen.KiFrage(d, "Warum?");
        Assert.DoesNotContain("GEHEIM", q); Assert.Contains("lea", q);
        Assert.Contains("PC-GEHEIM", CodeLernen.KiFrage(d, "Warum?", mitAusgabe: true));
    }
}
