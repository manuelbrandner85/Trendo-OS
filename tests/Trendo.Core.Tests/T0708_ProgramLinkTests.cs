using System.Text.Json.Nodes;
using Trendo.Core.Programs;
using Xunit;
using Xunit.Abstractions;

namespace Trendo.Core.Tests;

/// <summary>T-0708 (B6 Etappe 1): Programme außerhalb der Hülle hängen über <see cref="ProgramLink"/> an deren Rückgängig- und Notsicherungssystem.</summary>
public class T0708_ProgramLinkTests(ITestOutputHelper output)
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-t0708-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }
    static JsonObject S(string text) => new() { ["text"] = text };
    static string T(JsonNode? n) => n!["text"]!.GetValue<string>();

    [Fact(DisplayName = "T-0708 Rückgängig über mehrere Schritte und Wiederholen, höchstens 100 Schritte wie die Hülle")]
    public void Undo_redo_many_steps()
    {
        var l = new ProgramLink("STUDIO-LETTER-01", "brief", S("a"));
        Assert.False(l.Dirty); Assert.False(l.CanUndo);
        Assert.True(l.Commit("1", S("ab"))); Assert.True(l.Commit("2", S("abc"))); Assert.True(l.Commit("3", S("abcd")));
        Assert.False(l.Commit("gleich", S("abcd"))); // gleicher Stand ist kein Schritt
        Assert.True(l.Dirty);
        Assert.Equal("abc", T(l.Undo()));
        Assert.Equal("ab", T(l.Undo()));
        Assert.Equal("a", T(l.Undo()));
        Assert.Null(l.Undo());
        Assert.Equal("ab", T(l.Redo()));
        for (var i = 0; i < 150; i++) l.Commit("x", S("n" + i));
        var steps = 0; while (l.Undo() is not null) steps++;
        Assert.Equal(ProgramDocument.MaxUndo, steps);
    }

    [Fact(DisplayName = "T-0708 Notsicherung: nächster Start findet den ungespeicherten Stand und holt ihn zurück; nach dem Speichern ist die Sicherung weg")]
    public void Autosave_pending_restore_saved()
    {
        var dir = Tmp();
        try
        {
            var a = new ProgramLink("STUDIO-LETTER-01", "brief", S(""));
            Assert.Null(a.Autosave(dir)); // nichts geändert – keine Sicherung
            a.Commit("t", S("Kündigung zum 31.12."));
            Assert.Null(a.Autosave(dir, worth: false)); // wertloser Stand wird nicht gesichert
            var f = a.Autosave(dir); Assert.NotNull(f); Assert.True(File.Exists(f));
            var raw = JsonNode.Parse(File.ReadAllText(f!))!;
            Assert.Equal("tdoc", raw["format"]!.GetValue<string>()); Assert.Equal(ProgramDocument.FormatVersion, raw["version"]!.GetValue<int>());
            Assert.Empty(a.Pending(dir)); // die eigene Sicherung wird nicht angeboten

            var b = new ProgramLink("STUDIO-LETTER-01", "brief", S("")); // „nächster Start“
            Assert.Empty(new ProgramLink("STUDIO-LETTER-01", "anderes-format").Pending(dir)); // anderes Format: nicht anbieten
            var p = Assert.Single(b.Pending(dir));
            Assert.Equal("Kündigung zum 31.12.", T(b.Restore(p.File, dir)));
            Assert.True(b.Dirty); Assert.True(File.Exists(f)); // Sicherung bleibt, bis gespeichert ist
            b.Commit("weiter", S("Kündigung zum 31.12.2026"));
            Assert.Equal("Kündigung zum 31.12.", T(b.Undo())); // Rückgängig geht auch nach dem Zurückholen
            b.Saved(dir);
            Assert.False(b.Dirty); Assert.False(File.Exists(f));
            Assert.Empty(new ProgramLink("STUDIO-LETTER-01", "brief").Pending(dir));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "T-0708 Dateiformat: alte Projektdateien (ohne Versionsangabe, ohne meta, mit BOM) bleiben lesbar, eine neuere Fassung wird abgelehnt")]
    public void Old_files_stay_readable()
    {
        var dir = Tmp();
        try
        {
            // Aufbau wie von Trendo vor den Begleitdaten geschrieben (76d5ed0d5), hier zusätzlich ohne „version“ – gilt als Fassung 1
            var old = Path.Combine(dir, "alt.tdoc");
            File.WriteAllText(old, "﻿{\n  \"format\": \"tdoc\",\n  \"studio\": \"STUDIO-LETTER-01\",\n  \"title\": \"Alter Brief\",\n  \"created\": \"2026-10-27T10:00:00\",\n  \"changed\": \"2026-10-27T10:05:00\",\n  \"data\": { \"format\": \"brief\", \"inhalt\": { \"text\": \"alt\" } }\n}");
            var d = ProgramDocument.Load(old);
            Assert.Equal("Alter Brief", d.Title); Assert.Equal("alt", T(d.Data["inhalt"]));
            var bak = Path.Combine(dir, "sicherung"); Directory.CreateDirectory(bak);
            File.Copy(old, Path.Combine(bak, "STUDIO-LETTER-01-0123456789.tdoc"));
            var l = new ProgramLink("STUDIO-LETTER-01", "brief");
            Assert.Equal("alt", T(l.Restore(Assert.Single(l.Pending(bak)).File, bak)));
            var newer = Path.Combine(dir, "neu.tdoc");
            File.WriteAllText(newer, "{\"format\":\"tdoc\",\"version\":" + (ProgramDocument.FormatVersion + 1) + ",\"studio\":\"STUDIO-LETTER-01\",\"title\":\"x\",\"data\":{}}");
            var e = Assert.Throws<InvalidDataException>(() => ProgramDocument.Load(newer));
            Assert.Equal(ProgramDocument.LoadProblem.Newer, ProgramDocument.ProblemOf(e));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "T-0708 Briefe: ein Brief im Format der ersten Fassung des Briefstudios (21.10.2026) lässt sich weiter öffnen")]
    public void Old_letter_file_readable()
    {
        var dir = Tmp();
        try
        {
            var docx = Path.Combine(dir, "Kündigung.docx"); File.WriteAllBytes(docx, []);
            // Felder genau wie LetterDraft in 48054cd71 (System.Text.Json, eingerückt)
            File.WriteAllText(Path.ChangeExtension(docx, ".trendo-brief.json"), """
            {
              "Category": "kuendigung",
              "SenderName": "Erika Muster",
              "SenderStreet": "Musterweg 1",
              "SenderCity": "12345 Musterstadt",
              "SenderContact": "",
              "Recipient": "Beispiel GmbH\nPostfach 1\n12345 Musterstadt",
              "Place": "Musterstadt",
              "Date": "2026-10-21T00:00:00+02:00",
              "Reference": "K-77",
              "Subject": "Kündigung meines Vertrags",
              "Salutation": "Sehr geehrte Damen und Herren,",
              "Body": "hiermit kündige ich fristgerecht.",
              "Closing": "Mit freundlichen Grüßen",
              "Attachments": ""
            }
            """);
            var l = LetterArchive.Load(docx);
            Assert.NotNull(l);
            Assert.Equal("Kündigung meines Vertrags", l!.Subject); Assert.Equal("hiermit kündige ich fristgerecht.", l.Body); Assert.Equal("K-77", l.Reference);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "T-0708 Absturz: angeschlossenes Programm – 16-mal hart beendet, die Notsicherung ist jedes Mal lesbar und wird beim Neustart zurückgeholt")]
    public void Kill_while_autosaving_link()
    {
        var dir = Tmp(); var rnd = new Random(708); int during = 0, worst = 0, rounds = 0;
        try
        {
            // mindestens 16 Abbrüche; unter Last (paralleler Testlauf) fallen weniger mitten ins Schreiben – dann weiter abbrechen,
            // bis 8 davon mitten im Schreiben lagen (höchstens 48 Runden). Jede Runde prüft dasselbe wie vorher.
            for (var round = 1; round <= 48 && (round <= 16 || during < 8); round++)
            {
                rounds = round;
                if (ProgramCrashTests.RunAndKill("link", dir, 2, rnd)) during++;
                var (started, done, _) = ProgramCrashTests.ReadLog(dir);
                var bak = Path.Combine(dir, "sicherung");
                Assert.Empty(ProgramDocument.OrphanScan(bak, "STUDIO-LETTER-01").Broken); // nie eine halbe Sicherung
                var pending = new ProgramLink("STUDIO-LETTER-01", "brief").Pending(bak);
                Assert.True(pending.Count == 1, $"Runde {round}: {pending.Count} lesbare Sicherungen statt einer");
                var n = (int)ProgramDocument.ToNumber(new ProgramLink("STUDIO-LETTER-01", "brief").Restore(pending[0].File, bak)!["n"]);
                Assert.True(n >= done, $"Runde {round}: Sicherung hat Stand {n}, fertig gesichert war {done}");
                Assert.True(n <= started, $"Runde {round}: Stand {n} > begonnen {started}");
                worst = Math.Max(worst, started - n);
            }
            Assert.True(worst <= 1, "Mehr als der eine laufende Schritt ging verloren: " + worst);
            Assert.True(during >= 8, $"Nur {during} von {rounds} Abbrüchen fielen in einen laufenden Schreibvorgang – der Test taugt so nicht.");
            output.WriteLine($"link: {rounds} harte Abbrüche, {during} mitten im Schreiben, höchstens {worst} Schritt verloren.");
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }
}
