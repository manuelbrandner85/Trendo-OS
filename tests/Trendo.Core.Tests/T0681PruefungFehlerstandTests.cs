using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// T-0681 (B1b): Prüflesen und Vermerk der Sicherung, Wiederherstellen überspringt Beschädigtes; Fehlerstand und Obergrenze der Abläufe.
public class T0681_Sicherung_Pruefung
{
    static (Fixture F, Backup B, string Root) Make()
    {
        var f = new Fixture(); var root = f.Core.DataRoot;
        File.WriteAllText(Path.Combine(root, "a.txt"), "Inhalt A"); File.WriteAllText(Path.Combine(root, "b.txt"), string.Concat(Enumerable.Repeat("Zeile mit Inhalt B. ", 400)));
        return (f, new Backup(root, Path.Combine(root, "backups"), f.Core.Audit), root);
    }

    [Fact(DisplayName = "T-0681: Nicht gesicherte Dateien stehen im Ergebnis und in der Sicherung selbst – sie gilt als unvollständig; nach dem Sichern liegen keine Reste herum")]
    public void Unvollstaendig_wird_gefuehrt()
    {
        var (f, b, root) = Make(); using var _ = f;
        var gesperrt = Path.Combine(root, "belegt.dat"); File.WriteAllText(gesperrt, "x");
        Backup.Result r;
        using (new FileStream(gesperrt, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) r = b.CreateChecked(new DateTime(2026, 11, 16, 10, 0, 0));
        Assert.False(r.Complete); Assert.Equal(["belegt.dat"], r.NotSaved);
        Assert.Contains("Unvollständig", r.Say()); Assert.Contains("belegt.dat", r.Say());
        var note = b.Describe(r.Zip); Assert.NotNull(note); Assert.False(note!.Complete); Assert.Equal(["belegt.dat"], note.NotSaved); Assert.Equal(r.Saved, note.Saved);
        Assert.Contains(f.Core.Audit.ReadAll(), a => a.Action == "backup.incomplete");
        Assert.True(b.Verify(r.Zip).Ok); // was drin ist, ist heil
        var voll = b.CreateChecked(new DateTime(2026, 11, 16, 11, 0, 0));
        Assert.True(voll.Complete); Assert.True(b.Describe(voll.Zip)!.Complete); Assert.Contains("geprüft", voll.Say());
        Assert.Equal(voll.Saved, b.Verify(voll.Zip).Files);
        Assert.DoesNotContain(Directory.GetFiles(b.Folder), x => !x.EndsWith(".zip"));
    }

    [Fact(DisplayName = "T-0681: Reste einer abgebrochenen Sicherung werden beim nächsten Sichern aufgeräumt; eine beschädigte ZIP zählt nicht als letzte Sicherung")]
    public void Reste_und_beschaedigte_zaehlen_nicht()
    {
        var (f, b, root) = Make(); using var _ = f;
        Directory.CreateDirectory(b.Folder);
        var rest = Path.Combine(b.Folder, "sicherung-0123456789abcdef0123456789abcdef.teil"); File.WriteAllBytes(rest, new byte[1000]);
        var halb = Path.Combine(b.Folder, "trendo-sicherung-20261116-235959.zip"); File.WriteAllBytes(halb, [0x50, 0x4B, 0x03, 0x04, 1, 2, 3, 4, 5, 6]); // wie von einer älteren Version nach einem Abbruch
        var jetzt = new DateTime(2026, 11, 17, 0, 30, 0);
        Assert.Null(b.Latest()); Assert.True(b.Due(jetzt, 6)); // die halbe Datei verhindert keine neue Sicherung
        var zip = b.Create(new DateTime(2026, 11, 16, 12, 0, 0));
        Assert.False(File.Exists(rest));
        Assert.Equal(zip, b.Latest()!.FullName);
        Assert.False(Backup.Readable(halb)); Assert.False(b.Verify(halb).Ok); Assert.Null(b.Describe(halb));
    }

    [Fact(DisplayName = "T-0681: Wiederherstellen prüft die Sicherung vorher – eine beschädigte wird mit Meldung übersprungen, nichts wird geändert")]
    public void Beschaedigte_Sicherung_wird_uebersprungen()
    {
        var (f, b, root) = Make(); using var _ = f;
        var zip = b.Create(new DateTime(2026, 11, 16, 10, 0, 0));
        File.WriteAllText(Path.Combine(root, "a.txt"), "neuer Stand");
        // 1) abgeschnitten (Inhaltsverzeichnis fehlt)
        var ganz = File.ReadAllBytes(zip);
        File.WriteAllBytes(zip, ganz[..(ganz.Length / 2)]);
        var ex = Assert.Throws<InvalidDataException>(() => b.Restore(zip, new DateTime(2026, 11, 16, 11, 0, 0)));
        Assert.Contains("übersprungen", ex.Message); Assert.Contains("nichts geändert", ex.Message);
        Assert.Equal("neuer Stand", File.ReadAllText(Path.Combine(root, "a.txt")));
        Assert.Single(b.List()); // auch keine „Vorher“-Sicherung angelegt
        Assert.Contains(f.Core.Audit.ReadAll(), a => a.Action == "backup.restore.skipped");
        // 2) Bytes im gepackten Inhalt verfälscht (Verzeichnis heil, Inhalt falsch)
        var kipp = (byte[])ganz.Clone();
        var stelle = FirstDataOffset(kipp); for (var i = 0; i < 4; i++) kipp[stelle + 3 + i] ^= 0xFF;
        File.WriteAllBytes(zip, kipp);
        Assert.True(Backup.Readable(zip)); Assert.False(b.Verify(zip).Ok);
        Assert.Throws<InvalidDataException>(() => b.Restore(zip, new DateTime(2026, 11, 16, 11, 5, 0)));
        Assert.Equal("neuer Stand", File.ReadAllText(Path.Combine(root, "a.txt")));
        // 3) heil: klappt wie bisher
        File.WriteAllBytes(zip, ganz);
        Assert.True(b.Verify(zip).Ok);
        Assert.True(b.Restore(zip, new DateTime(2026, 11, 16, 11, 10, 0)) >= 2);
        Assert.Equal("Inhalt A", File.ReadAllText(Path.Combine(root, "a.txt")));
        Assert.True(f.Core.Audit.Verify());
    }

    /// <summary>Beginn der Nutzdaten des ersten ZIP-Eintrags (lokaler Kopf: 30 Bytes + Name + Zusatzfeld).</summary>
    static int FirstDataOffset(byte[] zip) => 30 + BitConverter.ToUInt16(zip, 26) + BitConverter.ToUInt16(zip, 28);
}

public class T0681_Ablaeufe_Fehlerstand
{
    static (Fixture F, WorkflowEngine E, string Home) Make()
    {
        var f = new Fixture(); var home = Path.Combine(f.Root, "home"); Directory.CreateDirectory(home);
        return (f, new WorkflowEngine(f.Root, f.Core.Audit, f.Core.Approvals, [home]), home);
    }

    static Workflow Kaputt(string home, TriggerKind trigger, out string ziel)
    {
        ziel = Path.Combine(home, "ziel"); Directory.CreateDirectory(Path.Combine(home, "quelle")); File.WriteAllText(Path.Combine(home, "quelle", "a.txt"), "x");
        File.WriteAllText(ziel, "Datei statt Ordner"); // Kopieren schlägt fehl
        return new Workflow { Name = "kopie", Trigger = trigger, At = "08:00", EveryMinutes = 60, Steps = [new() { Kind = StepKind.Kopieren, From = Path.Combine(home, "quelle"), To = ziel, Pattern = "*.txt" }] };
    }

    [Fact(DisplayName = "T-0681: Fehlversuche haben eine Obergrenze – danach erst zum nächsten regulären Zeitpunkt; der Fehlerstand steht am Ablauf und bleibt nach Neustart")]
    public async Task Obergrenze_und_Fehlerstand()
    {
        var (f, e, home) = Make(); using var _ = f;
        var w = e.Add(Kaputt(home, TriggerKind.Taeglich, out var ziel));
        var t = new DateTimeOffset(2026, 11, 16, 9, 0, 0, TimeSpan.Zero);
        for (var i = 0; i < WorkflowEngine.MaxRetries; i++) Assert.False(Assert.Single(await e.RunDueAsync(t.AddMinutes(i))).Ok);
        Assert.Equal(WorkflowEngine.MaxRetries, w.FailCount); Assert.Null(w.LastRun); Assert.NotNull(w.LastError);
        Assert.Empty(await e.RunDueAsync(t.AddMinutes(10))); Assert.Empty(e.Due(t.AddHours(10))); // heute kein weiterer Versuch
        var text = WorkflowEngine.FailText(w); Assert.NotNull(text); Assert.Contains("3 Versuche", text); Assert.Contains("nächsten regulären Zeitpunkt", text);
        // Neustart: Fehlerstand ist gespeichert
        var neu = new WorkflowEngine(f.Root, f.Core.Audit, f.Core.Approvals, [home]).Get(w.Id)!;
        Assert.Equal(WorkflowEngine.MaxRetries, neu.FailCount); Assert.Equal(w.LastError, neu.LastError); Assert.Null(neu.LastRun);
        // morgen wieder ein regulärer Versuch; klappt er, ist der Fehlerstand weg
        Assert.Single(e.Due(t.AddDays(1)));
        File.Delete(ziel);
        Assert.True(Assert.Single(await e.RunDueAsync(t.AddDays(1))).Ok);
        Assert.Equal(0, w.FailCount); Assert.Null(w.LastError); Assert.Null(WorkflowEngine.FailText(w)); Assert.Equal(t.AddDays(1), w.LastRun);
        Assert.Equal(WorkflowEngine.MaxRetries, e.History(w.Id).Count(h => !h.Ok)); // jeder Fehlversuch steht im Protokoll
    }

    [Fact(DisplayName = "T-0681: Intervall-Ablauf – nach der Obergrenze erst nach dem nächsten Intervall wieder; Ändern des Ablaufs erlaubt sofort einen neuen Versuch")]
    public async Task Intervall_Obergrenze()
    {
        var (f, e, home) = Make(); using var _ = f;
        var w = e.Add(Kaputt(home, TriggerKind.Intervall, out var _z));
        var t = new DateTimeOffset(2026, 11, 16, 9, 0, 0, TimeSpan.Zero);
        for (var i = 0; i < WorkflowEngine.MaxRetries; i++) Assert.Single(await e.RunDueAsync(t.AddMinutes(i)));
        Assert.Empty(e.Due(t.AddMinutes(30)));
        Assert.Single(e.Due(t.AddMinutes(2 + 60)));
        e.Update(w); // Nutzer hat etwas geändert
        Assert.Single(e.Due(t.AddMinutes(30)));
    }

    [Fact(DisplayName = "T-0681: Neue-Datei-Auslöser – eine Datei, deren Lauf fehlschlug, wird beim nächsten Takt erneut versucht (früher: nie wieder)")]
    public async Task NeueDatei_wird_erneut_versucht()
    {
        var (f, e, home) = Make(); using var _ = f;
        var watch = Path.Combine(home, "Eingang"); Directory.CreateDirectory(watch);
        var w = e.Add(new Workflow { Name = "neu", Trigger = TriggerKind.NeueDatei, WatchFolder = watch, WatchPattern = "*.txt", Steps = [new() { Kind = StepKind.KiZusammenfassen, From = watch, To = Path.Combine(home, "Sum") }] });
        var t = new DateTimeOffset(2026, 11, 16, 9, 0, 0, TimeSpan.Zero);
        Assert.Empty(e.Due(t));
        File.WriteAllText(Path.Combine(watch, "brief.txt"), "Inhalt");
        e.Summarize = _ => Task.FromResult<string?>(null); // KI nicht erreichbar
        Assert.False(Assert.Single(await e.RunDueAsync(t.AddMinutes(1))).Ok);
        e.Summarize = x => Task.FromResult<string?>("KURZ: " + x);
        Assert.True(Assert.Single(await e.RunDueAsync(t.AddMinutes(2))).Ok);
        Assert.Equal("KURZ: Inhalt", File.ReadAllText(Path.Combine(home, "Sum", "brief.zusammenfassung.txt")));
        Assert.Empty(await e.RunDueAsync(t.AddMinutes(3)));
    }

    [Fact(DisplayName = "T-0681: Beschädigte Ablaufdatei wird gemeldet (Meldung + Protokoll); Speichern hinterlässt keine vorläufigen Dateien")]
    public void Beschaedigt_wird_gemeldet()
    {
        using var f = new Fixture(); var home = Path.Combine(f.Root, "home"); Directory.CreateDirectory(home);
        var dir = Path.Combine(f.Root, "ablaeufe"); Directory.CreateDirectory(dir);
        Assert.Null(new WorkflowEngine(f.Root, f.Core.Audit, f.Core.Approvals, [home]).LoadProblem);
        File.WriteAllText(Path.Combine(dir, "ablaeufe.json"), "");
        var e = new WorkflowEngine(f.Root, f.Core.Audit, f.Core.Approvals, [home]);
        Assert.NotNull(e.LoadProblem); Assert.Contains("beschaedigt-", e.LoadProblem); Assert.Contains("nichts gelöscht", e.LoadProblem);
        Assert.Contains(f.Core.Audit.ReadAll(), a => a.Action == "workflow.datei.beschaedigt");
        e.Add(new Workflow { Name = "eins" }); e.Add(new Workflow { Name = "zwei" });
        Assert.Equal(["ablaeufe.json"], Directory.GetFiles(dir).Select(Path.GetFileName).Where(n => !n!.Contains("beschaedigt")));
        var wieder = new WorkflowEngine(f.Root, f.Core.Audit, f.Core.Approvals, [home]);
        Assert.Null(wieder.LoadProblem); Assert.Equal(2, wieder.All().Count);
    }
}
