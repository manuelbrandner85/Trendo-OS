using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Trendo.Core.Programs;
using Xunit;
using Xunit.Abstractions;

namespace Trendo.Core.Tests;

/// <summary>T-0680: Absturzsicherheit mit echtem Prozessabbruch. Ein eigenes Hilfsprogramm (tests/Trendo.CrashProbe) speichert in einer Schleife
/// und wird mit <see cref="Process.Kill()"/> hart beendet – mitten im Schreiben. Danach muss alles auf der Platte gültig sein.</summary>
public class ProgramCrashTests(ITestOutputHelper output)
{
    const string Studio = "SPEC-08";

    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-absturz-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }

    /// <summary>Das gebaute Hilfsprogramm: gleiche Konfiguration (Debug/Release) wie dieser Test.</summary>
    internal static ProcessStartInfo Probe(string mode, string dir)
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var config = here.Parent!.Name; // …/bin/<Konfiguration>/net10.0
        var root = here; while (root is not null && !File.Exists(Path.Combine(root.FullName, "Trendo.sln"))) root = root.Parent;
        Assert.NotNull(root);
        var dll = Path.Combine(root!.FullName, "tests", "Trendo.CrashProbe", "bin", config, here.Name, "Trendo.CrashProbe.dll");
        Assert.True(File.Exists(dll), "Hilfsprogramm fehlt: " + dll);
        var psi = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
        psi.ArgumentList.Add(dll); psi.ArgumentList.Add(mode); psi.ArgumentList.Add(dir); psi.ArgumentList.Add(Lines.ToString());
        return psi;
    }

    /// <summary>Umfang des Testdokuments: rund 2 MB – groß genug, dass das Schreiben einige Millisekunden dauert und ein Abbruch mitten hinein fällt.</summary>
    const int Lines = 40_000;

    /// <summary>Protokoll des Hilfsprogramms: zuletzt begonnener und zuletzt fertig gemeldeter Schritt, dazu die Schreibzeiten.</summary>
    internal static (int Started, int Done, List<int> Ms) ReadLog(string dir)
    {
        var f = Path.Combine(dir, "protokoll.txt");
        if (!File.Exists(f)) return (0, 0, []);
        string text;
        using (var fs = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) using (var r = new StreamReader(fs)) text = r.ReadToEnd();
        var lines = text.Split('\n'); // die letzte Zeile kann durch den Abbruch abgeschnitten sein: nur vollständige (mit Zeilenende) zählen
        int started = 0, done = 0; var ms = new List<int>();
        foreach (var line in lines.Take(lines.Length - 1))
        {
            var p = line.Split(' ');
            if (p is ["start", var a] && int.TryParse(a, out var x)) started = x;
            else if (p is ["fertig", var b, var c] && int.TryParse(b, out var y) && int.TryParse(c, out var z)) { done = y; ms.Add(z); }
        }
        return (started, done, ms);
    }

    /// <summary>Hilfsprogramm starten, warten bis es mindestens <paramref name="more"/> weitere Schritte geschafft hat und der nächste begonnen ist,
    /// dann zu einem zufälligen Zeitpunkt innerhalb der üblichen Dauer eines Schritts hart beenden. Rückgabe: Lief beim Abbruch gerade ein Schreibvorgang?</summary>
    internal static bool RunAndKill(string mode, string dir, int more, Random rnd)
    {
        var before = ReadLog(dir).Done;
        using var p = Process.Start(Probe(mode, dir))!;
        try
        {
            var sw = Stopwatch.StartNew();
            void Alive()
            {
                if (p.HasExited) Assert.Fail("Das Hilfsprogramm ist von selbst beendet: " + p.StandardError.ReadToEnd());
                Assert.True(sw.Elapsed < TimeSpan.FromSeconds(90), "Das Hilfsprogramm kommt nicht voran.");
            }
            (int Started, int Done, List<int> Ms) log;
            while ((log = ReadLog(dir)).Done < before + more) { Alive(); Thread.Sleep(2); }
            var target = log.Done;
            while ((log = ReadLog(dir)).Started <= target) { Alive(); Thread.Sleep(0); } // der nächste Schritt hat begonnen …
            var usual = log.Ms.TakeLast(3).DefaultIfEmpty(10).Average();
            var wait = Stopwatch.StartNew(); var until = rnd.NextDouble() * usual * 1.15; // … und irgendwo darin (selten kurz danach) ist Schluss
            while (wait.Elapsed.TotalMilliseconds < until) Thread.SpinWait(200);
        }
        finally
        {
            if (!p.HasExited) p.Kill(); // harter Abbruch – kein Aufräumen, kein Leeren von Puffern
            p.WaitForExit(10_000);
        }
        var end = ReadLog(dir);
        return end.Started > end.Done;
    }

    [Fact(DisplayName = "T-0680 Absturz beim Speichern: 24-mal hart beendet – Datei, Versionen und „Zuletzt“ sind jedes Mal gültig, kein fertig gemeldeter Stand fehlt")]
    public void Kill_while_saving()
    {
        var dir = Tmp(); var rnd = new Random(680);
        try
        {
            var file = Path.Combine(dir, "arbeit.tdoc"); var worstLoss = 0; var times = new List<int>(); int during = 0, lost = 0, kept = 0;
            for (var round = 1; round <= 24; round++)
            {
                var mid = RunAndKill("speichern", dir, 2, rnd);
                var (started, done, ms) = ReadLog(dir); times = ms;
                // 1) die Datei ist eine vollständige, lesbare Projektdatei
                var d = ProgramDocument.Load(file, Studio);
                var n = (int)d.Number("n");
                if (mid) { during++; if (n == started) kept++; else lost++; }
                Assert.Equal(Lines, d.List("inhalt").Count); // nichts abgeschnitten
                // 2) kein Stand, dessen Speichern als fertig gemeldet war, ist verloren; höchstens der eine, der gerade lief
                Assert.True(n >= done, $"Runde {round}: Datei hat Stand {n}, fertig gemeldet war {done}");
                Assert.True(n <= started, $"Runde {round}: Datei hat Stand {n}, begonnen war erst {started}");
                worstLoss = Math.Max(worstLoss, started - n);
                // 3) jede Version ist lesbar, höchstens 20 bleiben
                var versions = ProgramDocument.Versions(file);
                // (eine mehr ist möglich: Abbruch zwischen „neue Version geschrieben“ und „älteste entfernt“ – das nächste Speichern räumt auf)
                Assert.True(versions.Count >= 1 && versions.Count <= ProgramDocument.MaxVersions + 1, $"Runde {round}: {versions.Count} Versionen");
                foreach (var v in versions) Assert.True(ProgramDocument.Readable(v.File, Studio), $"Runde {round}: Version unlesbar: {Path.GetFileName(v.File)}");
                // 4) „Zuletzt“ ist gültiges JSON und nennt die Datei
                var recentFile = Path.Combine(dir, "programme", "zuletzt.json");
                Assert.NotNull(JsonSerializer.Deserialize<Dictionary<string, List<string>>>(File.ReadAllText(recentFile)));
                Assert.Contains(file, new ProgramRecent(dir).Get(Studio));
            }
            Assert.True(worstLoss <= 1, "Mehr als der eine laufende Schritt ging verloren: " + worstLoss);
            // Reste abgebrochener Schreibvorgänge: höchstens eine Zwischendatei je Abbruch, und keine heißt wie eine echte Datei (Endung .tmp)
            var rests = Directory.GetFiles(dir, "*.tmp", SearchOption.AllDirectories);
            Assert.True(rests.Length <= 24, "Zu viele Zwischendateien: " + rests.Length);
            output.WriteLine($"Liegengebliebene Zwischendateien nach 24 Abbrüchen: {rests.Length} (werden beim nächsten Schreiben nach {Trendo.Core.AtomicFile.StaleAfter.TotalMinutes:0} Minuten entfernt).");
            // Der Test taugt nur, wenn die Abbrüche wirklich in laufende Speichervorgänge fallen
            Assert.True(during >= 12, $"Nur {during} von 24 Abbrüchen fielen in einen laufenden Speichervorgang.");
            output.WriteLine($"24 Abbrüche beim Speichern: {during} mitten in einem Speichervorgang (davon {kept}-mal war der neue Stand schon da, {lost}-mal galt noch der alte), größter Verlust {worstLoss} laufender Schritt.");
            output.WriteLine($"{times.Count} Speichervorgänge, Dauer im Mittel {times.DefaultIfEmpty().Average():0.0} ms, längste {times.DefaultIfEmpty().Max()} ms (Dokument {new FileInfo(file).Length / 1024} KB, je Vorgang Version + Dokument + „Zuletzt“, jeweils bis auf den Datenträger).");
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "T-0680 Absturz beim Sichern: 20-mal hart beendet – Notsicherung neben der Datei und im Sicherungsordner sind jedes Mal lesbar, Wiederherstellen bringt den Stand zurück")]
    public void Kill_while_autosaving()
    {
        var dir = Tmp(); var rnd = new Random(681);
        try
        {
            var file = Path.Combine(dir, "arbeit.tdoc"); var backup = Path.Combine(dir, "sicherung"); var worstLoss = 0; var during = 0;
            for (var round = 1; round <= 20; round++)
            {
                if (RunAndKill("sichern", dir, 2, rnd)) during++;
                var (started, done, _) = ReadLog(dir);
                // die gespeicherte Datei blieb unberührt und lesbar
                Assert.True(ProgramDocument.Readable(file, Studio));
                // Notsicherung neben der Datei: vollständig, und sie enthält mindestens den zuletzt fertig gemeldeten Stand
                var side = ProgramDocument.AutosaveFile(file);
                var restored = ProgramDocument.Restore(side);
                var n = (int)restored.Number("n");
                Assert.Equal(file, restored.Path); Assert.True(restored.Dirty);
                Assert.Equal(Lines, restored.List("inhalt").Count);
                Assert.True(n >= done && n <= started, $"Runde {round}: Sicherung hat Stand {n}, fertig {done}, begonnen {started}");
                worstLoss = Math.Max(worstLoss, started - n);
                // sie ist neuer als die Datei – genau daran erkennt die Hülle beim Öffnen, dass sie etwas anzubieten hat
                Assert.True(File.GetLastWriteTimeUtc(side) > File.GetLastWriteTimeUtc(file));
                Assert.Contains(ProgramDocument.Recoveries(file, Studio), r => r.File == side && r.Kind == "sicherung");
                // Sicherungsordner (nie gespeicherte Dokumente): keine einzige kaputte Sicherung, die jüngste trägt einen aktuellen Stand
                var (good, broken) = ProgramDocument.OrphanScan(backup, Studio);
                Assert.Empty(broken); Assert.NotEmpty(good);
                var newest = (int)ProgramDocument.Restore(good[0].File).Number("n");
                Assert.True(newest >= done && newest <= started, $"Runde {round}: unbenanntes Dokument hat Stand {newest}, fertig {done}");
            }
            Assert.True(worstLoss <= 1, "Mehr als der eine laufende Schritt ging verloren: " + worstLoss);
            Assert.True(during >= 10, $"Nur {during} von 20 Abbrüchen fielen in einen laufenden Schreibvorgang.");
            output.WriteLine($"20 Abbrüche beim Sichern: {during} mitten in einem Schreibvorgang, größter Verlust {worstLoss} laufender Schritt.");
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "T-0680 Sperre: solange der andere Prozess läuft, bekommt man die Datei nicht; nach seinem harten Ende wird die verwaiste Sperre übernommen")]
    public void Lock_survives_until_process_dies()
    {
        var dir = Tmp();
        try
        {
            var file = Path.Combine(dir, "arbeit.tdoc");
            string Wait(Process p)
            {
                var note = Path.Combine(dir, $"sperre-{p.Id}.txt"); var sw = Stopwatch.StartNew();
                while (!File.Exists(note) || new FileInfo(note).Length == 0) { Assert.False(p.HasExited, "Hilfsprogramm beendet"); Assert.True(sw.Elapsed < TimeSpan.FromSeconds(30)); Thread.Sleep(10); }
                return File.ReadAllText(note);
            }
            for (var round = 0; round < 3; round++)
            {
                using var a = Process.Start(Probe("sperre", dir))!;
                Process? b = null;
                try
                {
                    Assert.Equal("gesperrt", Wait(a));
                    using var mine = new ProgramLocks(Path.Combine(dir, "sperren"));
                    var holder = mine.Acquire(file);
                    Assert.NotNull(holder); Assert.Equal(a.Id, holder!.Pid); // besetzt – vom anderen Prozess
                    Assert.NotNull(mine.HeldByOther(file));
                    a.Kill(); a.WaitForExit(10_000);                         // harter Abbruch: die Sperrdatei bleibt liegen
                    Assert.Null(mine.HeldByOther(file));                      // als verwaist erkannt
                    b = Process.Start(Probe("sperre", dir))!;
                    Assert.Equal("gesperrt", Wait(b));                        // ein zweiter Prozess übernimmt die verwaiste Sperre
                    Assert.Equal(b.Id, mine.Acquire(file)?.Pid);              // jetzt hält er sie – wir bekommen sie nicht
                    b.Kill(); b.WaitForExit(10_000);
                    Assert.Null(mine.Acquire(file));                          // wieder verwaist – übernommen
                } // mine gibt die Sperre beim Verlassen frei
                finally { foreach (var p in new[] { a, b }) if (p is { HasExited: false }) { p.Kill(); p.WaitForExit(10_000); } b?.Dispose(); }
            }
        }
        finally { Directory.Delete(dir, true); }
    }
}
