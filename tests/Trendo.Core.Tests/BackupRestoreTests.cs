using System.IO.Compression;
using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// T-0214: Wiederherstellen stürzt nicht ab.
public class BackupRestoreTests
{
    [Fact(DisplayName = "T-0214: Zwei Wiederherstellungen in derselben Sekunde + schreibgeschützte Datei – kein Absturz")]
    public void Restore_twice_same_second_and_readonly()
    {
        using var f = new Fixture(); var root = f.Core.DataRoot;
        var b = new Backup(root, Path.Combine(root, "backups"), f.Core.Audit);
        var t = new DateTime(2026, 10, 22, 3, 0, 0);
        var ro = Path.Combine(root, "Projekte", ".git", "objects", "ab", "cd");
        Directory.CreateDirectory(Path.GetDirectoryName(ro)!); File.WriteAllText(ro, "alt");
        File.SetAttributes(ro, FileAttributes.ReadOnly);
        var z = b.Create(t);
        File.SetAttributes(ro, FileAttributes.Normal); File.WriteAllText(ro, "neu"); File.SetAttributes(ro, FileAttributes.ReadOnly);
        var n1 = b.Restore(z, t); var n2 = b.Restore(z, t);
        Assert.True(n1 > 0); Assert.Equal(n1, n2);
        Assert.Equal("alt", File.ReadAllText(ro));
        Assert.Equal(b.List().Count, b.List().Select(x => x.Name).Distinct().Count());
        Assert.True(f.Core.Audit.Verify());
    }

    [Fact(DisplayName = "T-0214: Vorschau nennt überschriebene Dateien, nicht das Audit; gleicher Zeitstempel ergibt eine zweite Sicherung")]
    public void Preview_lists_files_and_same_second_is_unique()
    {
        using var f = new Fixture(); var root = f.Core.DataRoot;
        var b = new Backup(root, Path.Combine(root, "backups"), f.Core.Audit);
        var t = new DateTime(2026, 10, 22, 4, 0, 0);
        var note = Path.Combine(root, "notiz.txt");
        var obj = Path.Combine(root, "Projekte", "Demo", ".git", "objects", "03", "0f1f42");
        Directory.CreateDirectory(Path.GetDirectoryName(obj)!);
        File.WriteAllText(note, "alt");
        File.WriteAllText(obj, "blob-v1");
        File.SetAttributes(obj, FileAttributes.ReadOnly);
        File.WriteAllText(Path.Combine(root, "backups-notiz.txt"), "neben-dem-ordner");
        var z = b.Create(t);
        File.WriteAllText(note, "neu");
        var preview = b.Preview(z);
        Assert.Contains(preview.Overwrite, p => p.Equals("notiz.txt", StringComparison.Ordinal));
        Assert.Contains(preview.Overwrite, p => p.Contains(".git/objects/", StringComparison.Ordinal));
        Assert.Contains(preview.Overwrite, p => p.Equals("backups-notiz.txt", StringComparison.Ordinal));
        Assert.DoesNotContain(preview.Overwrite, p => p.StartsWith("audit/", StringComparison.Ordinal) || p.StartsWith("backups/", StringComparison.Ordinal));
        var again = b.Create(t);
        Assert.NotEqual(z, again);
        Assert.True(File.Exists(z));
        Assert.True(File.Exists(again));
        var n = b.Restore(z, t);
        Assert.True(n >= 3);
        Assert.Equal("alt", File.ReadAllText(note));
        Assert.Equal("blob-v1", File.ReadAllText(obj));
        Assert.Equal("neben-dem-ordner", File.ReadAllText(Path.Combine(root, "backups-notiz.txt")));
        Assert.True(f.Core.Audit.Verify());
    }

    [Fact(DisplayName = "T-0214: Die gewählte alte Sicherung bleibt erhalten, die Sicherheitskopie hält den Stand von davor")]
    public void Restore_keeps_chosen_zip_when_rotation_would_delete_it()
    {
        using var f = new Fixture(); var root = f.Core.DataRoot;
        var b = new Backup(root, Path.Combine(root, "backups"), f.Core.Audit);
        var t = new DateTime(2026, 10, 22, 5, 0, 0);
        var note = Path.Combine(root, "notiz.txt");
        File.WriteAllText(note, "v0");
        var target = b.Create(t, keep: 100);
        File.WriteAllText(note, "v-jetzt");
        for (var i = 1; i <= 20; i++) b.Create(t.AddMinutes(i), keep: 100);
        Assert.Equal(21, b.List().Count);
        var n = b.Restore(target, t.AddHours(2));
        Assert.True(n >= 1);
        Assert.True(File.Exists(target));
        Assert.Equal("v0", File.ReadAllText(note));
        var detail = f.Core.Audit.ReadAll().Last(e => e.Action == "backup.restored").Detail;
        const string marker = "vorher gesichert: ";
        var start = detail.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0);
        var name = detail[(start + marker.Length)..detail.IndexOf(',', start)];
        var safety = Path.Combine(b.Folder, name);
        Assert.True(File.Exists(safety));
        using var zip = ZipFile.OpenRead(safety);
        using var reader = new StreamReader(zip.GetEntry("notiz.txt")!.Open());
        Assert.Equal("v-jetzt", reader.ReadToEnd());
        Assert.True(f.Core.Audit.Verify());
        Assert.Equal(b.List().Count, b.List().Select(x => x.FullName).Distinct().Count());
    }

    [Fact(DisplayName = "T-0214: Zip-Slip und Audit-Eintrag werden nicht geschrieben, leere Ordner schon")]
    public void Restore_rejects_zip_slip_and_keeps_audit()
    {
        using var f = new Fixture(); var root = f.Core.DataRoot;
        var b = new Backup(root, Path.Combine(root, "backups"), f.Core.Audit);
        var t = new DateTime(2026, 10, 22, 6, 0, 0);
        var note = Path.Combine(root, "notiz.txt");
        File.WriteAllText(note, "vorher");
        var auditBefore = File.ReadAllText(f.Core.Audit.FilePath);
        var outside = Path.Combine(Path.GetTempPath(), "trendo-slip-" + Guid.NewGuid().ToString("N") + ".txt");
        var crafted = Path.Combine(root, "fremd.zip");
        using (var zip = ZipFile.Open(crafted, ZipArchiveMode.Create))
        {
            Write(zip, "../" + Path.GetFileName(outside), "draussen");
            Write(zip, "audit/audit.jsonl", "KAPUTT");
            Write(zip, "notiz.txt", "drin");
            zip.CreateEntry("leer/");
        }
        try
        {
            var preview = b.Preview(crafted);
            Assert.Contains(preview.Overwrite, p => p == "notiz.txt");
            Assert.DoesNotContain(preview.Overwrite, p => p.Contains("..", StringComparison.Ordinal) || p.StartsWith("audit/", StringComparison.Ordinal));
            var n = b.Restore(crafted, t);
            Assert.True(n >= 1);
            Assert.False(File.Exists(outside));
            Assert.Equal("drin", File.ReadAllText(note));
            Assert.True(Directory.Exists(Path.Combine(root, "leer")));
            Assert.DoesNotContain("KAPUTT", File.ReadAllText(f.Core.Audit.FilePath));
            Assert.True(f.Core.Audit.Verify());
            Assert.NotEqual(auditBefore, File.ReadAllText(f.Core.Audit.FilePath));
        }
        finally { if (File.Exists(outside)) File.Delete(outside); }
    }

    private static void Write(ZipArchive zip, string name, string text)
    {
        using var writer = new StreamWriter(zip.CreateEntry(name).Open());
        writer.Write(text);
    }
}
