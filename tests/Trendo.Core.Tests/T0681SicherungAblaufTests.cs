using System.IO.Compression;
using System.Text.Json;
using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// T-0681 (B1b): Sicherung ohne halbe ZIP, Abläufe ohne falsches „zuletzt gelaufen“, Speichern übersteht einen Absturz.
public class T0681_Sicherung
{
    static bool Lesbar(string zip) { try { using var z = ZipFile.OpenRead(zip); foreach (var e in z.Entries) { using var s = e.Open(); s.CopyTo(Stream.Null); } return true; } catch (InvalidDataException) { return false; } }

    [Fact(DisplayName = "T-0681: Eine gesperrte Datei bricht die Sicherung nicht ab – der Rest ist gesichert, keine halbe ZIP bleibt liegen")]
    public void Gesperrte_Datei_bricht_nicht_ab()
    {
        using var f = new Fixture(); var root = f.Core.DataRoot;
        var b = new Backup(root, Path.Combine(root, "backups"), f.Core.Audit);
        File.WriteAllText(Path.Combine(root, "a-wichtig.txt"), "Inhalt A");
        File.WriteAllText(Path.Combine(root, "z-spaeter.txt"), "Inhalt Z");
        var gesperrt = Path.Combine(root, "m-gesperrt.dat"); File.WriteAllText(gesperrt, "belegt");
        string zip;
        using (new FileStream(gesperrt, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) // wie von einem anderen Programm belegt
            zip = b.Create(new DateTime(2026, 11, 16, 10, 0, 0));
        Assert.All(b.List(), x => Assert.True(Lesbar(x.FullName), x.Name + " ist beschädigt"));
        using var z = ZipFile.OpenRead(zip);
        Assert.NotNull(z.GetEntry("a-wichtig.txt")); Assert.NotNull(z.GetEntry("z-spaeter.txt")); // auch Dateien NACH der gesperrten
        Assert.Null(z.GetEntry("m-gesperrt.dat"));
    }

    [Fact(DisplayName = "T-0681: Absturz mitten im Sichern (echter Prozessabbruch) – keine halbe ZIP wird als Sicherung geführt")]
    public void Absturz_beim_Sichern_laesst_keine_halbe_Sicherung()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-t0681-sich-" + Guid.NewGuid().ToString("N"));
        try
        {
            var daten = Path.Combine(root, "daten"); Directory.CreateDirectory(daten);
            var rnd = new Random(681); var buf = new byte[4 * 1024 * 1024];
            for (var i = 0; i < 6; i++) { rnd.NextBytes(buf); File.WriteAllBytes(Path.Combine(daten, $"gross-{i}.bin"), buf); } // nicht komprimierbar: das Schreiben dauert
            using var core = new Fixture();
            var b = new Backup(daten, Path.Combine(root, "sicherungen"), core.Core.Audit);
            for (var runde = 0; runde < 4; runde++)
            {
                var p = AbsturzKind.Start(root, "sicherung", root);
                Thread.Sleep(250 + runde * 130);
                AbsturzKind.Kill(p);
                Assert.All(b.List(), x => Assert.True(Lesbar(x.FullName), $"Runde {runde}: {x.Name} ist eine halbe Sicherung"));
            }
        }
        finally { try { Directory.Delete(root, true); } catch (IOException) { } }
    }
}

public class T0681_Ablaeufe
{
    static (Fixture F, WorkflowEngine E, string Home) Make()
    {
        var f = new Fixture(); var home = Path.Combine(f.Root, "home"); Directory.CreateDirectory(home);
        return (f, new WorkflowEngine(f.Root, f.Core.Audit, f.Core.Approvals, [home]), home);
    }

    [Fact(DisplayName = "T-0681: Ein fehlgeschlagener Lauf gilt nicht als „zuletzt gelaufen“ – er ist beim nächsten Takt wieder fällig")]
    public async Task Fehlschlag_setzt_LastRun_nicht()
    {
        var (f, e, home) = Make(); using var _ = f;
        var ziel = Path.Combine(home, "ziel");
        var w = e.Add(new Workflow { Name = "täglich", Trigger = TriggerKind.Taeglich, At = "08:00", Steps = [new() { Kind = StepKind.Kopieren, From = Path.Combine(home, "quelle"), To = ziel, Pattern = "*.txt" }] });
        Directory.CreateDirectory(Path.Combine(home, "quelle")); File.WriteAllText(Path.Combine(home, "quelle", "a.txt"), "x");
        File.WriteAllText(ziel, "hier liegt eine Datei statt eines Ordners"); // Kopieren schlägt fehl
        var tag = new DateTimeOffset(2026, 11, 16, 9, 0, 0, TimeSpan.Zero);
        var lauf = Assert.Single(await e.RunDueAsync(tag));
        Assert.False(lauf.Ok);
        Assert.Null(w.LastRun);
        Assert.Contains(e.Due(tag.AddMinutes(1)), x => x.W.Id == w.Id); // wird erneut versucht
        File.Delete(ziel); // Ursache behoben
        Assert.True(Assert.Single(await e.RunDueAsync(tag.AddMinutes(1))).Ok);
        Assert.True(File.Exists(Path.Combine(ziel, "a.txt")));
        Assert.Empty(e.Due(tag.AddMinutes(2)));
    }

    [Fact(DisplayName = "T-0681: Beschädigte Ablaufdatei wird nicht still als leer gelesen, sondern aufgehoben (.beschaedigt-<zeit>)")]
    public void Beschaedigte_Datei_wird_aufgehoben()
    {
        using var f = new Fixture(); var home = Path.Combine(f.Root, "home"); Directory.CreateDirectory(home);
        var dir = Path.Combine(f.Root, "ablaeufe"); Directory.CreateDirectory(dir);
        const string kaputt = "[{\"Id\":\"abc\",\"Name\":\"Wichtiger Abl";
        File.WriteAllText(Path.Combine(dir, "ablaeufe.json"), kaputt);
        var e = new WorkflowEngine(f.Root, f.Core.Audit, f.Core.Approvals, [home]);
        e.Add(new Workflow { Name = "neu" }); // früher: überschreibt die beschädigte Datei endgültig
        var kopie = Assert.Single(Directory.GetFiles(dir, "ablaeufe.json.beschaedigt-*"));
        Assert.Equal(kaputt, File.ReadAllText(kopie));
    }

    [Fact(DisplayName = "T-0681: Absturz mitten im Speichern der Abläufe (echter Prozessabbruch) – die Datei bleibt vollständig lesbar")]
    public void Absturz_beim_Speichern_laesst_Datei_ganz()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-t0681-abl-" + Guid.NewGuid().ToString("N"));
        try
        {
            var home = Path.Combine(root, "home"); Directory.CreateDirectory(home);
            var datei = Path.Combine(root, "ablaeufe", "ablaeufe.json");
            for (var runde = 0; runde < 10; runde++)
            {
                var p = AbsturzKind.Start(root, "ablauf-speichern", root, home);
                Thread.Sleep(40 + runde * 23);
                AbsturzKind.Kill(p);
                List<Workflow>? gelesen = null;
                var ex = Record.Exception(() => gelesen = JsonSerializer.Deserialize<List<Workflow>>(File.ReadAllText(datei)));
                Assert.True(ex is null, $"Runde {runde}: Ablaufdatei nach dem Absturz beschädigt ({ex?.Message}, {new FileInfo(datei).Length} Bytes)");
                Assert.Equal(AbsturzKind.Ablaeufe, gelesen!.Count);
                Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(datei)!, "*.beschaedigt-*"));
            }
        }
        finally { try { Directory.Delete(root, true); } catch (IOException) { } }
    }
}
