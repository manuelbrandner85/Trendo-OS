using System.Text.Json.Nodes;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0680: Programm-Hülle absturzsicher – atomar schreiben, Formatversion, beschädigte Dateien, Sperre, Änderung von außen.
/// (Der echte Prozessabbruch steht in <see cref="ProgramCrashTests"/>.)</summary>
public class ProgramSafetyTests
{
    const string Studio = "SPEC-08";
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-sicher-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }
    static ProgramDocument Doc(string name = "A") { var d = new ProgramDocument(Studio, "Kleid"); d.Set("name", name); return d; }

    [Fact(DisplayName = "T-0680 Atomar schreiben: neue Datei, Ersetzen, Ordner wird angelegt, keine Zwischendatei bleibt liegen, alte Reste werden aufgeräumt")]
    public void Atomic_write()
    {
        var dir = Tmp();
        try
        {
            var f = Path.Combine(dir, "unter", "datei.json");
            AtomicFile.WriteAllText(f, "eins"); Assert.Equal("eins", File.ReadAllText(f));
            AtomicFile.WriteAllText(f, "zwei – größer"); Assert.Equal("zwei – größer", File.ReadAllText(f));
            Assert.Equal([f], Directory.GetFiles(Path.GetDirectoryName(f)!));
            Assert.NotEqual(0xEF, File.ReadAllBytes(f)[0]); // ohne BOM, wie bisher
            // Rest eines früher abgebrochenen Schreibvorgangs: alt → wird entfernt; frisch (anderes Fenster schreibt gerade) → bleibt
            var old = f + ".0000aaaa.tmp"; var young = f + ".0000bbbb.tmp";
            File.WriteAllText(old, "halb"); File.SetLastWriteTimeUtc(old, DateTime.UtcNow - AtomicFile.StaleAfter - TimeSpan.FromMinutes(1));
            File.WriteAllText(young, "halb");
            AtomicFile.WriteAllText(f, "drei");
            Assert.False(File.Exists(old)); Assert.True(File.Exists(young)); Assert.Equal("drei", File.ReadAllText(f));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "T-0680 Formatversion: wird gelesen; neuere Fassung wird mit Hinweis abgelehnt und nicht angefasst; fehlende Angabe gilt als Fassung 1")]
    public void Format_version()
    {
        var dir = Tmp();
        try
        {
            var f = Path.Combine(dir, "a.tdoc"); Doc().Save(f);
            var root = JsonNode.Parse(File.ReadAllText(f))!.AsObject();
            Assert.Equal(ProgramDocument.FormatVersion, (int)root["version"]!);
            // neuere Fassung mit einem Feld, das dieses Programm nicht kennt
            root["version"] = ProgramDocument.FormatVersion + 1; root["neu"] = "unbekannt";
            var newer = Path.Combine(dir, "neuer.tdoc"); File.WriteAllText(newer, root.ToJsonString()); var before = File.ReadAllText(newer);
            var e = Assert.Throws<InvalidDataException>(() => ProgramDocument.Load(newer, Studio));
            Assert.Equal(ProgramDocument.LoadProblem.Newer, ProgramDocument.ProblemOf(e));
            Assert.Contains("neueren", e.Message); Assert.Contains("aktualisiere", e.Message);
            Assert.Equal(before, File.ReadAllText(newer)); // unverändert
            Assert.False(ProgramDocument.Readable(newer));
            // ohne Angabe (alte Dateien) und mit der bekannten Fassung: lesbar
            root.Remove("version"); root.Remove("neu"); File.WriteAllText(newer, root.ToJsonString());
            Assert.Equal("A", ProgramDocument.Load(newer, Studio).Text("name"));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "T-0680 Beschädigte Datei: klare Ablehnung statt leerem Dokument; angeboten werden nur lesbare Stände (Notsicherung, Versionen), neueste zuerst")]
    public void Damaged_file_offers_recoveries()
    {
        var dir = Tmp();
        try
        {
            var f = Path.Combine(dir, "a.tdoc");
            var d = Doc("eins"); d.Save(f);
            File.SetLastWriteTime(f, DateTime.Now.AddMinutes(-30)); d = ProgramDocument.Load(f, Studio);
            d.Set("name", "zwei"); d.Save();                    // „eins“ wird Version
            d.Set("name", "drei"); var side = d.Autosave(dir)!; // Notsicherung neben der Datei
            var full = File.ReadAllText(f);
            foreach (var broken in new[] { full[..(full.Length / 2)], "", "null", "[1,2]", "{\"format\":\"tdoc\",\"data\":[1]}", "{\"format\":\"tdoc\",\"title\":5}" })
            {
                File.WriteAllText(f, broken);
                var e = Assert.Throws<InvalidDataException>(() => ProgramDocument.Load(f, Studio));
                Assert.Equal(ProgramDocument.LoadProblem.Damaged, ProgramDocument.ProblemOf(e));
            }
            File.WriteAllText(f, "{\"format\":\"anderes\"}");
            Assert.Equal(ProgramDocument.LoadProblem.Foreign, ProgramDocument.ProblemOf(Assert.Throws<InvalidDataException>(() => ProgramDocument.Load(f))));
            File.WriteAllText(f, full[..(full.Length / 2)]);
            var rec = ProgramDocument.Recoveries(f, Studio);
            Assert.Equal(["sicherung", "version"], rec.Select(r => r.Kind));
            Assert.Equal("drei", ProgramDocument.Load(rec[0].File).Text("name"));
            Assert.Equal("eins", ProgramDocument.Load(rec[1].File).Text("name"));
            // eine kaputte Notsicherung wird übersprungen, nicht angeboten
            File.WriteAllText(side, "{ halb");
            Assert.Equal(["version"], ProgramDocument.Recoveries(f, Studio).Select(r => r.Kind));
            // Zurückholen: gehört zur beschädigten Datei, gilt als geändert; Speichern ersetzt sie – die kaputte Fassung wird keine „Version“
            var back = ProgramDocument.Restore(rec[1].File, f);
            Assert.Equal((f, true, "eins"), (back.Path, back.Dirty, back.Text("name")));
            var versions = ProgramDocument.Versions(f).Count;
            back.Save();
            Assert.Equal("eins", ProgramDocument.Load(f, Studio).Text("name"));
            Assert.Equal(versions, ProgramDocument.Versions(f).Count);
            Assert.All(ProgramDocument.Versions(f), v => Assert.True(ProgramDocument.Readable(v.File)));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "T-0680 Von außen geändert: Speichern überschreibt nicht still, sondern meldet es; nach Rückfrage bleibt die fremde Fassung als Version; „laden“ übernimmt sie")]
    public void Changed_on_disk()
    {
        var dir = Tmp();
        try
        {
            var f = Path.Combine(dir, "a.tdoc"); Doc("eins").Save(f);
            var mine = ProgramDocument.Load(f, Studio); var other = ProgramDocument.Load(f, Studio);
            Assert.False(mine.ChangedOnDisk);
            other.Set("name", "von außen"); other.Set("mehr", "damit die Größe sicher anders ist"); other.Save();
            Assert.True(mine.ChangedOnDisk);
            mine.Set("name", "meine");
            Assert.Throws<FileChangedOutsideException>(() => mine.Save());
            Assert.Throws<FileChangedOutsideException>(() => mine.Save(keepVersion: false)); // auch das automatische Speichern
            Assert.Equal("von außen", ProgramDocument.Load(f).Text("name")); Assert.True(mine.Dirty); // nichts überschrieben
            // nach Rückfrage: meine Fassung gewinnt, die fremde ist als Version da – auch wenn gerade keine Version fällig wäre
            mine.Save(keepVersion: false, overwriteChanged: true);
            Assert.Equal("meine", ProgramDocument.Load(f).Text("name")); Assert.False(mine.ChangedOnDisk);
            Assert.Contains(ProgramDocument.Versions(f), v => ProgramDocument.Load(v.File).Text("name") == "von außen");
            mine.Set("name", "weiter"); mine.Save(); // danach wieder ohne Rückfrage
            // die andere Seite lädt neu: Inhalt übernommen, gilt als gespeichert, Rückgängig holt die eigene Fassung
            Assert.True(other.ChangedOnDisk);
            other.Set("name", "eigene"); other.ReloadFromDisk();
            Assert.Equal(("weiter", false, false), (other.Text("name"), other.Dirty, other.ChangedOnDisk));
            Assert.True(other.Undo()); Assert.Equal("eigene", other.Text("name"));
            // „Speichern unter“ auf eine andere Datei fragt nicht nach
            other.Save(Path.Combine(dir, "b.tdoc")); Assert.Equal("eigene", ProgramDocument.Load(Path.Combine(dir, "b.tdoc")).Text("name"));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "T-0680 Nur lesen: Speichern über dieselbe Datei ist gesperrt, die Notsicherung geht in den Sicherungsordner, „Speichern unter“ und „als Kopie“ gehen")]
    public void Read_only()
    {
        var dir = Tmp();
        try
        {
            var f = Path.Combine(dir, "a.tdoc"); Doc("eins").Save(f); var backup = Path.Combine(dir, "sicherung");
            var d = ProgramDocument.Load(f, Studio); d.ReadOnly = true; d.Set("name", "geändert");
            Assert.Contains("nur zum Lesen", Assert.Throws<InvalidOperationException>(() => d.Save()).Message);
            var b = d.Autosave(backup)!;
            Assert.Equal(d.BackupFile(backup), b); Assert.False(File.Exists(ProgramDocument.AutosaveFile(f))); // die Notsicherung des anderen Fensters bleibt unberührt
            d.Save(Path.Combine(dir, "b.tdoc")); Assert.False(d.ReadOnly); d.Set("name", "weiter"); d.Save();
            Assert.Equal("eins", ProgramDocument.Load(f).Text("name"));
            var c = ProgramDocument.Load(f, Studio); c.ReadOnly = true; c.Detach("Kleid (Kopie)");
            Assert.Equal(((string?)null, false, true, "Kleid (Kopie)"), (c.Path, c.ReadOnly, c.Dirty, c.Title));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "T-0680 Sperre im selben Prozess: zweites Fenster bekommt die Datei nicht; nach Freigabe oder „Absturz“ des ersten schon; unlesbare alte Sperre und fremde, beendete Prozesse werden übernommen")]
    public void Locks()
    {
        var dir = Tmp();
        try
        {
            var lockDir = Path.Combine(dir, "sperren"); var f = Path.Combine(dir, "a.tdoc");
            using var a = new ProgramLocks(lockDir); using var b = new ProgramLocks(lockDir);
            Assert.Null(a.Acquire(f)); Assert.Null(a.Acquire(f)); // mehrfach: bleibt meine
            Assert.Null(a.Acquire(f.ToUpperInvariant()));          // gleiche Datei, andere Schreibweise
            var h = b.Acquire(f);
            Assert.NotNull(h); Assert.Equal(Environment.ProcessId, h!.Pid); Assert.True((DateTime.Now - h.Since).Duration() < TimeSpan.FromMinutes(1));
            Assert.NotNull(b.HeldByOther(f)); Assert.Null(a.HeldByOther(f));
            b.Release(f); Assert.NotNull(b.Acquire(f)); // fremde Sperre lässt sich nicht freigeben
            Assert.Null(b.Acquire(Path.Combine(dir, "andere.tdoc"))); // andere Datei: frei
            a.Release(f); Assert.Null(b.Acquire(f));
            b.Abandon(); // Fenster „abgestürzt“: Sperrdatei liegt noch, gilt aber als verwaist
            Assert.True(File.Exists(a.FileFor(f))); Assert.Null(a.HeldByOther(f)); Assert.Null(a.Acquire(f));
            a.Release(f);
            // Sperrdatei eines Prozesses, den es nicht mehr gibt
            File.WriteAllText(a.FileFor(f), new JsonObject { ["pid"] = 0x3FFFFFFC, ["start"] = DateTime.UtcNow.AddHours(-1).ToString("O"), ["fenster"] = "weg", ["seit"] = DateTime.Now.ToString("O") }.ToJsonString());
            Assert.Null(a.Acquire(f)); a.Release(f);
            // gleiche Prozessnummer wie ein laufender Prozess, aber anderer Start (die Nummer wurde neu vergeben): verwaist
            using var self = System.Diagnostics.Process.GetCurrentProcess();
            var parent = System.Diagnostics.Process.GetProcesses().First(p => { try { return p.Id != self.Id && p.Id != 0 && p.StartTime < DateTime.Now.AddSeconds(-10); } catch { return false; } });
            File.WriteAllText(a.FileFor(f), new JsonObject { ["pid"] = parent.Id, ["start"] = parent.StartTime.ToUniversalTime().AddHours(-5).ToString("O"), ["fenster"] = "alt", ["seit"] = DateTime.Now.ToString("O") }.ToJsonString());
            Assert.Null(a.Acquire(f)); a.Release(f);
            // … mit passendem Start: lebt, also besetzt
            File.WriteAllText(a.FileFor(f), new JsonObject { ["pid"] = parent.Id, ["start"] = parent.StartTime.ToUniversalTime().ToString("O"), ["fenster"] = "lebt", ["seit"] = DateTime.Now.ToString("O") }.ToJsonString());
            Assert.Equal(parent.Id, a.Acquire(f)?.Pid); File.Delete(a.FileFor(f));
            // unlesbare Sperrdatei, älter als ein paar Sekunden: Rest eines Absturzes
            File.WriteAllText(a.FileFor(f), "{ halb"); File.SetLastWriteTimeUtc(a.FileFor(f), DateTime.UtcNow.AddMinutes(-1));
            Assert.Null(a.Acquire(f));
            a.Dispose(); Assert.False(File.Exists(a.FileFor(f))); // Fenster zu: Sperren weg
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "T-0680 Sicherungsordner: kaputte Sicherungen werden getrennt gemeldet statt geladen; Wiederherstellen behält die Kennung, die Sicherung bleibt bis zum Speichern")]
    public void Orphans_checked()
    {
        var dir = Tmp();
        try
        {
            var backup = Path.Combine(dir, "sicherung");
            var d = new ProgramDocument(Studio, "Entwurf"); d.Set("name", "x"); var f = d.Autosave(backup)!;
            File.WriteAllText(Path.Combine(backup, Studio + "-kaputt0000.tdoc"), "{\"format\":\"tdoc\",\"da");
            File.WriteAllText(Path.Combine(backup, Studio + "-leer000000.tdoc"), "");
            var (good, broken) = ProgramDocument.OrphanScan(backup, Studio);
            Assert.Equal([(f, "Entwurf")], good.Select(g => (g.File, g.Title))); Assert.Equal(2, broken.Count);
            var r = ProgramDocument.Restore(f);
            Assert.Equal((d.Id, (string?)null, true), (r.Id, r.Path, r.Dirty)); Assert.Equal(f, r.BackupFile(backup));
            Assert.True(File.Exists(f)); // Wiederherstellen löscht nichts
            r.Set("name", "y"); Assert.Equal(f, r.Autosave(backup)); // weiteres Sichern führt dieselbe Sicherung fort
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "T-0680 Notsicherung: wird erst nach erfolgreichem Speichern entfernt; eine angebotene (….wiederherstellen) gehört zur selben Datei; „Zuletzt“ übersteht eine kaputte Liste")]
    public void Autosave_removed_after_save_and_recent()
    {
        var dir = Tmp();
        try
        {
            var f = Path.Combine(dir, "a.tdoc"); var d = Doc("eins"); d.Save(f);
            d.Set("name", "zwei"); var side = d.Autosave(dir)!;
            Assert.Equal(ProgramDocument.AutosaveFile(f), side);
            Assert.Equal([side], Directory.GetFiles(dir, "*.autosave")); Assert.Empty(Directory.GetFiles(dir, "*.tmp"));
            // Speichern scheitert (nur lesen): die Notsicherung bleibt
            d.ReadOnly = true; Assert.Throws<InvalidOperationException>(() => d.Save()); Assert.True(File.Exists(side));
            d.ReadOnly = false; d.Save(); Assert.False(File.Exists(side));
            // angebotene Sicherung unter eigenem Namen
            d.Set("name", "drei"); side = d.Autosave(dir)!; var keep = ProgramDocument.RecoveryFile(f); File.Move(side, keep);
            var r = ProgramDocument.Restore(keep); Assert.Equal((f, "drei"), (r.Path, r.Text("name")));
            Assert.Contains(ProgramDocument.Recoveries(f, Studio), x => x.File == keep);
            // „Zuletzt“
            var recent = new ProgramRecent(dir); recent.Add(Studio, f);
            var list = Path.Combine(dir, "programme", "zuletzt.json");
            Assert.Equal([list], Directory.GetFiles(Path.GetDirectoryName(list)!));
            File.WriteAllText(list, "{\"SPEC-08\":[\"C:"); Assert.Empty(recent.Get(Studio));
            recent.Add(Studio, f); Assert.Equal([f], recent.Get(Studio));
        }
        finally { Directory.Delete(dir, true); }
    }
}
