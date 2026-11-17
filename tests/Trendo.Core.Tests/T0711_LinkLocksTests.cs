using System.Text.Json.Nodes;
using Trendo.Core;
using Trendo.Core.Programs;
using Xunit;
using Xunit.Abstractions;

namespace Trendo.Core.Tests;

/// <summary>T-0711 (B6 Etappe 1b): Sperre gegen ein zweites Fenster auch für Sicherungen angeschlossener Programme (<see cref="ProgramLink"/>),
/// alte Dateien der neu angeschlossenen Programme bleiben lesbar, Speichern atomar, Abbruchtest mit dem Stand des Editors.</summary>
public class T0711_LinkLocksTests(ITestOutputHelper output)
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-t0711-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }
    static JsonObject S(string text) => new() { ["text"] = text };

    [Fact(DisplayName = "T-0711 Zwei Fenster: eine Sicherung, die ein anderes laufendes Fenster hält, wird nicht angeboten und nicht übernommen; nach dessen Ende (oder Absturz) schon")]
    public void Second_window_does_not_offer_held_backup()
    {
        var dir = Tmp(); var bak = Path.Combine(dir, "sicherung"); var locksDir = Path.Combine(dir, "sperren");
        var locksA = new ProgramLocks(locksDir); var locksB = new ProgramLocks(locksDir);
        try
        {
            var a = new ProgramLink("LINK-MAIL", "mail", S("")) { Locks = locksA };
            a.Commit("t", S("Hallo Team"));
            var f = a.Autosave(bak); Assert.NotNull(f);
            Assert.NotNull(locksB.HeldByOther(f!)); // gesperrt für Fenster A

            var b = new ProgramLink("LINK-MAIL", "mail", S("")) { Locks = locksB };
            Assert.Empty(b.Pending(bak)); // Fenster B bietet die Sicherung von A nicht an
            Assert.Throws<IOException>(() => b.Restore(f!, bak)); // und kann sie nicht übernehmen
            Assert.Single(new ProgramLink("LINK-MAIL", "mail").Pending(bak)); // ohne Sperren (wie bisher) wäre sie angeboten worden – das war der offene Punkt

            locksA.Abandon(); // Fenster A stürzt ab: die Sperrdatei bleibt liegen, gilt aber als verwaist
            var p = Assert.Single(b.Pending(bak));
            Assert.Equal("Hallo Team", b.Restore(p.File, bak)!["text"]!.GetValue<string>());
            Assert.NotNull(locksA.HeldByOther(f!)); // jetzt hält B die Sicherung …
            var c = new ProgramLink("LINK-MAIL", "mail", S("")) { Locks = new ProgramLocks(locksDir) };
            Assert.Empty(c.Pending(bak)); // … und ein drittes Fenster bietet sie nicht an

            b.Saved(bak); // gespeichert: Sicherung und Sperre weg
            Assert.False(File.Exists(f)); Assert.Null(c.Locks!.HeldByOther(f!));
            c.Locks!.Dispose();
        }
        finally { locksA.Dispose(); locksB.Dispose(); Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "T-0711 Zwei Fenster: Schließen eines Fensters gibt seine Sicherungen frei; Verwerfen löst die eigene Sperre")]
    public void Close_releases_backup()
    {
        var dir = Tmp(); var bak = Path.Combine(dir, "sicherung"); var locksDir = Path.Combine(dir, "sperren");
        try
        {
            var locksA = new ProgramLocks(locksDir);
            var a = new ProgramLink("LINK-WIKI", "wiki", S("")) { Locks = locksA };
            a.Commit("t", S("Kundennummer 4711")); var f = a.Autosave(bak)!;
            using var locksB = new ProgramLocks(locksDir);
            var b = new ProgramLink("LINK-WIKI", "wiki", S("")) { Locks = locksB };
            Assert.Empty(b.Pending(bak));
            locksA.Dispose(); // Fenster A geschlossen (die Hülle sichert vorher, die Sperre geht beim Schließen)
            var p = Assert.Single(b.Pending(bak));
            ProgramLink.Discard(p.File, locksB);
            Assert.False(File.Exists(f)); Assert.Empty(Directory.GetFiles(locksDir));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "T-0711 Dateiformate: alte Dateien der neu angeschlossenen Programme bleiben lesbar (Spiel-Einstellungen, Wissen, Vorlagen/Skills), Speichern atomar")]
    public void Old_files_stay_readable()
    {
        var dir = Tmp();
        try
        {
            // Spiele-Werkstatt: spiel.json im Aufbau von T-0204 (nur die ersten Felder, ohne die späteren Ergänzungen)
            var game = Path.Combine(dir, "spiel"); Directory.CreateDirectory(game);
            File.WriteAllText(Path.Combine(game, "spiel.json"), """{ "Speed": 7, "Lives": 2, "WinScore": 30, "Player": "#FF0000", "Accent": "#00FF00", "Background": "#000000", "Sound": false, "Level": ["#..#", "...."] }""");
            var s = GameStudio.ReadSettings(game);
            Assert.Equal(7, s.Speed); Assert.Equal(2, s.Lives); Assert.Equal("#..#", s.Level[0]); Assert.True(s.Menu); // neue Felder mit Vorgabe
            var saved = GameStudio.Save(game, s with { Speed = 8 });
            Assert.Equal(8, GameStudio.ReadSettings(game).Speed); Assert.Empty(Directory.GetFiles(game, "*.tmp*"));
            Assert.Contains("\"Speed\": 8", File.ReadAllText(Path.Combine(game, "spiel.json")));

            // Wissen: wissen.json im Aufbau von CORE-10 (System.Text.Json, nicht eingerückt) – von Hand geschrieben wie eine alte Datei
            var audit = new AuditLog(Path.Combine(dir, "audit.jsonl"));
            Directory.CreateDirectory(Path.Combine(dir, "wissen"));
            File.WriteAllText(Path.Combine(dir, "wissen", "wissen.json"), """[{"Id":"k1","Title":"Stadtwerke","Text":"Kundennummer 4711","Tags":[],"Source":"eigene Notiz","Created":"2026-10-20T10:00:00+02:00"}]""");
            var kb = new KnowledgeStore(dir, audit);
            Assert.Equal("Kundennummer 4711", kb.ByTitle("Stadtwerke")!.Text);
            kb.Update("k1", "Stadtwerke", "Kundennummer 4712");
            Assert.Equal("Kundennummer 4712", new KnowledgeStore(dir, audit).ByTitle("Stadtwerke")!.Text);
            Assert.Empty(Directory.GetFiles(Path.Combine(dir, "wissen"), "*.tmp*"));

            // Vorlagen und Skills (ein Speicher): vorlagen.json im Aufbau von CORE-12 bleibt lesbar, Speichern legt eine neue Version an
            Directory.CreateDirectory(Path.Combine(dir, "prompts"));
            File.WriteAllText(Path.Combine(dir, "prompts", "vorlagen.json"), """[{"Id":"brief-kuerzen","Name":"Brief kürzen","Category":"Briefe","BuiltIn":false,"Versions":[{"Version":1,"Text":"Kürze diesen Text auf {{Anzahl|3}} Sätze.","At":"2026-10-20T10:00:00+02:00","Note":"angelegt"}]}]""");
            var lib = new PromptLibrary(dir, audit);
            Assert.Equal("Kürze diesen Text auf {{Anzahl|3}} Sätze.", lib.Get("brief-kuerzen")!.Text);
            lib.Save("brief-kuerzen", "Brief kürzen", "Briefe", "Kürze auf {{Anzahl|2}} Sätze.");
            Assert.Equal(2, new PromptLibrary(dir, audit).Get("brief-kuerzen")!.Version);
            Assert.Empty(Directory.GetFiles(Path.Combine(dir, "prompts"), "*.tmp*"));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "T-0711 Absturz: Editor der Übersicht über ProgramLink mit Sperre – 12-mal hart beendet, die Sicherung ist jedes Mal lesbar und wird trotz liegengebliebener Sperre zurückgeholt")]
    public void Kill_while_autosaving_editor_link()
    {
        var dir = Tmp(); var rnd = new Random(711); int during = 0, worst = 0;
        try
        {
            for (var round = 1; round <= 3 * 12 && (round <= 12 || during < 6); round++) // unter Last weiter abbrechen, bis genug mitten im Schreiben lagen (wie T0708)
            {
                if (ProgramCrashTests.RunAndKill("link-editor", dir, 2, rnd)) during++;
                var (started, done, _) = ProgramCrashTests.ReadLog(dir);
                var bak = Path.Combine(dir, "sicherung");
                Assert.Empty(ProgramDocument.OrphanScan(bak, "LINK-EDITOR").Broken); // nie eine halbe Sicherung
                Assert.NotEmpty(Directory.GetFiles(Path.Combine(dir, "sperren"), "*.lock")); // die Sperre des abgestürzten Prozesses liegt noch da …
                using var locks = new ProgramLocks(Path.Combine(dir, "sperren"));
                var link = new ProgramLink("LINK-EDITOR", "editor") { Locks = locks };
                var pending = link.Pending(bak); // … gilt aber als verwaist
                Assert.True(pending.Count == 1, $"Runde {round}: {pending.Count} lesbare Sicherungen statt einer");
                var state = link.Restore(pending[0].File, bak)!;
                var n = (int)ProgramDocument.ToNumber(state["n"]);
                Assert.EndsWith("Äpfel\r\n" + n, state["docs"]![0]!["text"]!.GetValue<string>());
                Assert.True(n >= done, $"Runde {round}: Sicherung hat Stand {n}, fertig gesichert war {done}");
                Assert.True(n <= started, $"Runde {round}: Stand {n} > begonnen {started}");
                worst = Math.Max(worst, started - n);
            }
            Assert.True(worst <= 1, "Mehr als der eine laufende Schritt ging verloren: " + worst);
            Assert.True(during >= 6, $"Nur {during} von 12 Abbrüchen fielen in einen laufenden Schreibvorgang – der Test taugt so nicht.");
            output.WriteLine($"link-editor: 12 harte Abbrüche, {during} mitten im Schreiben, höchstens {worst} Schritt verloren.");
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }
}
