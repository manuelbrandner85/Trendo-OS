using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// T-0299: Sicherung mit Zeitleiste – Inhalt, Vergleich mit heute, einzelne Datei zurückholen.
public class BackupTimelineTests
{
    [Fact(DisplayName = "T-0299: Vergleich erkennt geändert, gelöscht, neu und gleich; Vorschau liest den alten Inhalt")]
    public void Compare_with_now()
    {
        using var f = new Fixture(); var root = f.Core.DataRoot;
        var b = new Backup(root, Path.Combine(root, "backups"), f.Core.Audit);
        Directory.CreateDirectory(Path.Combine(root, "Projekte"));
        File.WriteAllText(Path.Combine(root, "Projekte", "a.txt"), "alt");
        File.WriteAllText(Path.Combine(root, "Projekte", "b.txt"), "bleibt");
        File.WriteAllText(Path.Combine(root, "Projekte", "c.txt"), "weg");
        var z = b.Create(new DateTime(2026, 10, 30, 8, 0, 0));
        Assert.Contains(b.Entries(z), e => e.Path == "Projekte/a.txt" && e.Bytes == 3);
        Assert.DoesNotContain(b.Entries(z), e => e.Path.StartsWith("audit/"));
        File.WriteAllText(Path.Combine(root, "Projekte", "a.txt"), "neuer");
        File.Delete(Path.Combine(root, "Projekte", "c.txt"));
        File.WriteAllText(Path.Combine(root, "Projekte", "d.txt"), "frisch");
        var d = b.CompareWithNow(z).ToDictionary(x => x.Path, x => x.Kind);
        Assert.Equal(Backup.Change.Geaendert, d["Projekte/a.txt"]);
        Assert.Equal(Backup.Change.Gleich, d["Projekte/b.txt"]);
        Assert.Equal(Backup.Change.Geloescht, d["Projekte/c.txt"]);
        Assert.Equal(Backup.Change.Neu, d["Projekte/d.txt"]);
        Assert.DoesNotContain(d.Keys, k => k.StartsWith("backups") || k.StartsWith("audit/"));
        Assert.Equal("alt", System.Text.Encoding.UTF8.GetString(b.Read(z, "Projekte/a.txt")));
    }

    [Fact(DisplayName = "T-0299: Einzelne Datei zurückholen hebt die heutige Fassung auf, holt Gelöschtes zurück, Protokoll ist geschützt")]
    public void Restore_one_keeps_current()
    {
        using var f = new Fixture(); var root = f.Core.DataRoot;
        var b = new Backup(root, Path.Combine(root, "backups"), f.Core.Audit);
        var a = Path.Combine(root, "Projekte", "a.txt"); var c = Path.Combine(root, "Projekte", "c.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(a)!); File.WriteAllText(a, "alt"); File.WriteAllText(c, "weg");
        var t = new DateTime(2026, 10, 30, 8, 0, 0); var z = b.Create(t);
        File.WriteAllText(a, "heute"); File.Delete(c);
        var kept = b.RestoreOne(z, "Projekte/a.txt", t.AddHours(1));
        Assert.Equal("alt", File.ReadAllText(a));
        Assert.NotNull(kept); Assert.Equal("heute", File.ReadAllText(kept!));
        Assert.StartsWith(Path.Combine(root, "backups", "einzeln"), kept);
        Assert.Null(b.RestoreOne(z, "Projekte/c.txt", t.AddHours(1)));
        Assert.Equal("weg", File.ReadAllText(c));
        Assert.Throws<InvalidOperationException>(() => b.RestoreOne(z, "audit/log.jsonl", t));
        Assert.Throws<InvalidOperationException>(() => b.RestoreOne(z, "../draussen.txt", t));
        Assert.Throws<FileNotFoundException>(() => b.RestoreOne(z, "Projekte/gibtsnicht.txt", t));
        Assert.Equal(Backup.Change.Gleich, b.CompareWithNow(z).Single(x => x.Path == "Projekte/a.txt").Kind);
        Assert.True(f.Core.Audit.Verify());
    }
}
