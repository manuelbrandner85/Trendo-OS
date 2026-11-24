using System.IO.Compression;
using System.Security.Cryptography;
using Trendo.Core;
using Trendo.Core.Sys;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0764 (SYS-11): Von Trendo eingerichtete Programme an einem Ort – Entfernen ohne Windows-Einstellungen, nur Trendos eigener Ordner,
/// in die Sicherung (wiederherstellbar), endgültig löschen erst als eigener Schritt; Kinderprofil darf nichts davon.</summary>
public class SYS11_TrendoProgramme
{
    static (string Portable, string Tools) Roots(Fixture f)
    {
        var p = Path.Combine(f.Root, "werkzeuge"); var t = Path.Combine(f.Root, "tools");
        Directory.CreateDirectory(p); Directory.CreateDirectory(t);
        return (p, t);
    }

    static string PortableZip(string dir)
    {
        Directory.CreateDirectory(dir);
        var zip = Path.Combine(dir, "x.zip");
        using (var z = ZipFile.Open(zip, ZipArchiveMode.Create))
        {
            using (var s = new StreamWriter(z.CreateEntry("notiz/notiz.exe").Open())) s.Write("MZ-Programm");
            using (var s = new StreamWriter(z.CreateEntry("notiz/liesmich.txt").Open())) s.Write("Hallo");
        }
        return zip;
    }

    [Fact(DisplayName = "SYS-11 T-0764: Portable, Vollpaket und GitHub erscheinen an einem Ort – mit Programmdatei, Herkunft und Größe")]
    public void Liste()
    {
        using var f = new Fixture();
        var (p, t) = Roots(f);
        var zip = PortableZip(Path.Combine(f.Root, "q"));
        Software.InstallPortable(Principal.LocalOwner, zip, Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(zip))), p, "notiz", f.Core.Audit);
        Directory.CreateDirectory(Path.Combine(t, "7zip")); File.WriteAllText(Path.Combine(t, "7zip", "7z.exe"), "MZ");
        var list = TrendoPrograms.List(p, t);
        var port = Assert.Single(list, e => e.Art == TrendoPrograms.Herkunft.Portable);
        Assert.Equal("notiz", port.Id);
        Assert.EndsWith("notiz.exe", port.Exe);
        Assert.True(port.Bytes > 0);
        var voll = Assert.Single(list, e => e.Art == TrendoPrograms.Herkunft.Vollpaket);
        Assert.Equal("7zip", voll.Id);
        Assert.EndsWith("7z.exe", voll.Exe);
    }

    [Fact(DisplayName = "SYS-11 T-0764: Entfernen verschiebt in die Sicherung (nichts gelöscht), Wiederherstellen bringt es zurück, „Endgültig löschen“ gibt den Platz frei")]
    public void Entfernen_Sicherung_Wiederherstellen()
    {
        using var f = new Fixture();
        var (p, t) = Roots(f);
        var dir = Path.Combine(p, "notiz"); Directory.CreateDirectory(dir); File.WriteAllText(Path.Combine(dir, "notiz.exe"), "MZ-Programm");
        var e = Assert.Single(TrendoPrograms.List(p, t));
        var s = TrendoPrograms.Remove(Principal.LocalOwner, e, p, t, f.Core.Audit);
        Assert.False(Directory.Exists(dir));
        Assert.Equal("MZ-Programm", File.ReadAllText(Path.Combine(s.Ordner, "notiz.exe")));
        Assert.Empty(TrendoPrograms.List(p, t));
        var b = Assert.Single(TrendoPrograms.Backups(p, t));
        Assert.Equal("notiz", b.Id);
        Assert.Contains(f.Core.Audit.ReadAll(), a => a.Action == "programm.entfernt");
        Assert.Empty(f.Launcher.Opened); // keine Windows-Einstellungen

        TrendoPrograms.Restore(Principal.LocalOwner, b, p, t, f.Core.Audit);
        Assert.Equal("MZ-Programm", File.ReadAllText(Path.Combine(dir, "notiz.exe")));
        Assert.Empty(TrendoPrograms.Backups(p, t));

        var s2 = TrendoPrograms.Remove(Principal.LocalOwner, Assert.Single(TrendoPrograms.List(p, t)), p, t, f.Core.Audit);
        // gleicher Name wieder eingerichtet: Wiederherstellen überschreibt nichts
        Directory.CreateDirectory(dir); File.WriteAllText(Path.Combine(dir, "notiz.exe"), "neu");
        Assert.Throws<InvalidOperationException>(() => TrendoPrograms.Restore(Principal.LocalOwner, s2, p, t, f.Core.Audit));
        Assert.Equal("neu", File.ReadAllText(Path.Combine(dir, "notiz.exe")));
        TrendoPrograms.Purge(Principal.LocalOwner, s2, p, t, f.Core.Audit);
        Assert.False(Directory.Exists(s2.Ordner));
        Assert.Empty(TrendoPrograms.Backups(p, t));
    }

    [Fact(DisplayName = "SYS-11 T-0764: GitHub-Programm entfernen nimmt es aus der Liste, Wiederherstellen trägt es wieder ein")]
    public void GitHub_Eintrag()
    {
        using var f = new Fixture();
        var (p, t) = Roots(f);
        var zip = Path.Combine(f.Root, "tool.zip");
        using (var z = ZipFile.Open(zip, ZipArchiveMode.Create)) using (var w = new StreamWriter(z.CreateEntry("tool.exe").Open())) w.Write("MZ");
        var sha = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(zip)));
        GitHubApps.Install(Principal.LocalOwner, t, "o/tool", new GitHubApps.Release("v1", "", []), new GitHubApps.Asset("tool-windows-x64.zip", "https://github.com/o/tool/x", new FileInfo(zip).Length, sha), zip, "MIT", "", f.Core.Audit);
        var e = Assert.Single(TrendoPrograms.List(p, t));
        Assert.Equal(TrendoPrograms.Herkunft.GitHub, e.Art);
        var s = TrendoPrograms.Remove(Principal.LocalOwner, e, p, t, f.Core.Audit);
        Assert.Empty(GitHubApps.List(t));
        TrendoPrograms.Restore(Principal.LocalOwner, s, p, t, f.Core.Audit);
        Assert.Equal("v1", Assert.Single(GitHubApps.List(t)).Version);
        Assert.NotNull(TrendoPrograms.List(p, t).Single().Exe);
    }

    [Fact(DisplayName = "SYS-11 T-0764: Nur Trendos eigener Ordner – Pfadtricks, fremde Sicherungen und Verknüpfungen werden abgelehnt; Kinderprofil darf nichts")]
    public void Grenzen()
    {
        using var f = new Fixture();
        var (p, t) = Roots(f);
        var outside = Path.Combine(f.Root, "fremd"); Directory.CreateDirectory(outside); File.WriteAllText(Path.Combine(outside, "wichtig.txt"), "x");
        foreach (var id in new[] { "..", "../fremd", "..\\fremd", ".entfernt", "", "C:\\Windows" })
            Assert.ThrowsAny<Exception>(() => TrendoPrograms.Remove(Principal.LocalOwner, new(id, TrendoPrograms.Herkunft.Portable, id, outside, null, "", "", 0), p, t, f.Core.Audit));
        Assert.True(File.Exists(Path.Combine(outside, "wichtig.txt")));
        Assert.Throws<ArgumentException>(() => TrendoPrograms.Purge(Principal.LocalOwner, new(outside, "fremd", TrendoPrograms.Herkunft.Portable, "fremd", "", DateTime.Now, 0), p, t, f.Core.Audit));
        Assert.True(File.Exists(Path.Combine(outside, "wichtig.txt")));
        // ein fremdes Paket im Werkzeugordner ist kein „mitgeliefertes Programm“
        Directory.CreateDirectory(Path.Combine(t, "fremdpaket"));
        Assert.Throws<InvalidOperationException>(() => TrendoPrograms.Remove(Principal.LocalOwner, new("fremdpaket", TrendoPrograms.Herkunft.Vollpaket, "x", "", null, "", "", 0), p, t, f.Core.Audit));

        var dir = Path.Combine(p, "notiz"); Directory.CreateDirectory(dir); File.WriteAllText(Path.Combine(dir, "notiz.exe"), "MZ");
        var e = Assert.Single(TrendoPrograms.List(p, t));
        Assert.Throws<UnauthorizedAccessException>(() => TrendoPrograms.Remove(Principal.ChildProfile, e, p, t, f.Core.Audit));
        Assert.True(Directory.Exists(dir));
        var s = TrendoPrograms.Remove(Principal.LocalOwner, e, p, t, f.Core.Audit);
        Assert.Throws<UnauthorizedAccessException>(() => TrendoPrograms.Restore(Principal.ChildProfile, s, p, t, f.Core.Audit));
        Assert.Throws<UnauthorizedAccessException>(() => TrendoPrograms.Purge(Principal.ChildProfile, s, p, t, f.Core.Audit));
        Assert.True(Directory.Exists(s.Ordner));
    }

    [Fact(DisplayName = "SYS-11 T-0764: Läuft das Programm noch (Datei offen), wird nichts verschoben – ganz oder gar nicht")]
    public void Gesperrt_bleibt_ganz()
    {
        using var f = new Fixture();
        var (p, t) = Roots(f);
        var dir = Path.Combine(p, "notiz"); Directory.CreateDirectory(dir); var exe = Path.Combine(dir, "notiz.exe"); File.WriteAllText(exe, "MZ");
        var e = Assert.Single(TrendoPrograms.List(p, t));
        using (new FileStream(exe, FileMode.Open, FileAccess.Read, FileShare.Read)) // wie ein laufendes Programm: ohne Freigabe zum Löschen/Umbenennen
        {
            if (OperatingSystem.IsWindows()) Assert.ThrowsAny<IOException>(() => TrendoPrograms.Remove(Principal.LocalOwner, e, p, t, f.Core.Audit));
            else return; // unter Linux darf ein offener Ordner umbenannt werden – dort gibt es diese Sperre nicht
        }
        Assert.True(File.Exists(exe));
        Assert.Empty(TrendoPrograms.Backups(p, t));
    }
}
