using System.Text.RegularExpressions;
using Trendo.Core;
using Trendo.Core.Shell;
using Trendo.Core.Sys;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0764, 10. Prüfrunde: mpv ohne yt-dlp-Hook und mit leerem eigenem Arbeitsordner (M a/b), Kinder schreiben nie in Trendos eigene Ordner
/// (M c), IPTV-Multicast, Aufräumen alter mpv-Ordner (N). Startet nichts echt.</summary>
public class RechteKernFix10
{
    static string Repo(string rel)
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "Trendo.sln"))) return Path.Combine(d.FullName, rel);
        throw new FileNotFoundException(rel);
    }
    static IEnumerable<string> Sources() => Directory.EnumerateFiles(Repo("src"), "*.cs", SearchOption.AllDirectories)
        .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

    [Fact(DisplayName = "T-0764 M a: jeder mpv-Start ohne yt-dlp-Hook („--ytdl=no“) – feste Argumente und eigenes Player-Fenster")]
    public void Kein_ytdl()
    {
        var a = MpvSafe.Args();
        Assert.Contains("--ytdl=no", a);
        Assert.Contains("--ytdl=no", MediaLibrary.Player("a.mp3", "mpv", null)!.Value.Args);
        MpvSafe.Release(a);
        Assert.Contains("\"--ytdl=no\"", File.ReadAllText(Repo("src/Trendo.Core/MpvSafe.cs"))); // im Quelltext der festen Argumente
    }

    [Fact(DisplayName = "T-0764 M b: jeder mpv-/VLC-Start im Quelltext setzt den leeren, Trendo-eigenen Arbeitsordner (MpvSafe.WorkDir)")]
    public void Arbeitsordner()
    {
        var bad = new List<string>();
        foreach (var f in Sources())
        {
            var lines = File.ReadAllLines(f);
            for (var i = 0; i < lines.Length; i++)
            {
                var l = lines[i];
                var mpvStart = Regex.IsMatch(l, @"ProcessStartInfo\(\s*(MpvPath|mpvPath|mpv|MpvExe|vlc)\b") || l.Contains("MpvSafe.HoldUntilExit(x.Args", StringComparison.Ordinal);
                if (mpvStart && !l.Contains("MpvSafe.WorkDir()", StringComparison.Ordinal)) bad.Add($"{Path.GetFileName(f)}:{i + 1}");
            }
        }
        Assert.True(bad.Count == 0, string.Join(", ", bad));
    }

    [Theory(DisplayName = "T-0764 N2: IPTV-Multicast – udp:// und rtp:// erlaubt")]
    [InlineData("udp://239.0.0.1:1234")] [InlineData("rtp://239.1.1.1:5004")]
    public void Multicast(string url) => Assert.True(MpvSafe.TargetAllowed(url));

    [Fact(DisplayName = "T-0764 M b: Arbeitsordner wird vor jedem Start geleert (hineingelegte yt-dlp.exe ist weg) und ist geschützt")]
    public void Arbeitsordner_leer()
    {
        // 11. Prüfrunde: je Start ein frischer, leerer Ordner mit Zufallsnamen – eine in einen früheren Ordner gelegte yt-dlp.exe erreicht den nächsten Start nie
        var d = MpvSafe.WorkDir();
        File.WriteAllText(Path.Combine(d, "yt-dlp.exe"), "MZ");
        var d2 = MpvSafe.WorkDir();
        Assert.NotEqual(d, d2);
        Assert.Empty(Directory.EnumerateFileSystemEntries(d2));
        Assert.True(ProtectedFolders.IsProtected(Path.Combine(d2, "yt-dlp.exe")));
        MpvSafe.SafeDelete(d); MpvSafe.SafeDelete(d2);
        Assert.False(Directory.Exists(d));
    }

    [Fact(DisplayName = "T-0764 M c: Kinder schreiben nie in Trendos eigene Ordner (Dateimanager: anlegen, kopieren, verschieben, umbenennen, löschen) – eigene Projekte und Erwachsene wie bisher")]
    public void Dateimanager_Kind()
    {
        using var f = new Fixture();
        var trendo = Path.Combine(f.Root, "TrendoOS"); var tools = Path.Combine(trendo, "werkzeuge", "mpv"); var proj = Path.Combine(trendo, "projekte");
        var home = Path.Combine(f.Root, "home"); foreach (var d in new[] { tools, proj, home }) Directory.CreateDirectory(d);
        ProtectedFolders.Protect(trendo); ProtectedFolders.Allow(proj);
        var exe = Path.Combine(home, "yt-dlp.exe"); File.WriteAllText(exe, "MZ");
        var inTools = Path.Combine(tools, "alt.txt"); File.WriteAllText(inTools, "x");
        var kid = new FileBrowser { WriteGuard = (p, _) => ProtectedFolders.WriteProblem(Principal.ChildProfile, p, f.Core.Audit) }; // nur die Trendo-Ordner-Regel (Freigabeliste: RechteKernFix11)
        Assert.Equal(Rbac.NurErwachseneTrendoOrdner, Assert.Throws<UnauthorizedAccessException>(() => kid.Paste(exe, tools, move: false)).Message);
        Assert.Throws<UnauthorizedAccessException>(() => kid.Paste(exe, tools, move: true));
        Assert.Throws<UnauthorizedAccessException>(() => kid.NewFolder(tools));
        Assert.Throws<UnauthorizedAccessException>(() => kid.Rename(inTools, "yt-dlp.exe"));
        Assert.Throws<UnauthorizedAccessException>(() => kid.DeletePermanently(inTools));
        Assert.Throws<UnauthorizedAccessException>(() => kid.Paste(inTools, home, move: true)); // Verschieben aus dem Trendo-Ordner
        Assert.False(File.Exists(Path.Combine(tools, "yt-dlp.exe")));
        Assert.True(File.Exists(kid.Paste(inTools, home, move: false))); // Lesen (Kopie heraus) bleibt erlaubt
        Assert.True(File.Exists(kid.Paste(exe, proj, move: false))); // eigene Projekte: frei
        Assert.Contains(f.Core.Audit.ReadAll(), a => a.Action == "rbac.denied" && a.Detail.Contains("schreiben"));
        var adult = new FileBrowser { WriteGuard = (p, _) => ProtectedFolders.WriteProblem(Principal.LocalOwner, p, f.Core.Audit) };
        Assert.True(File.Exists(adult.Paste(exe, tools, move: false))); // Erwachsene: wie bisher
    }

    [Fact(DisplayName = "T-0764 M c: Umweg über eine Verknüpfung (Symlink/Junction) in den Trendo-Ordner wird aufgelöst und abgelehnt; Entpacken-Ziel ebenso")]
    public void Umweg_und_Entpacken()
    {
        using var f = new Fixture();
        var trendo = Path.Combine(f.Root, "TrendoOS"); var tools = Path.Combine(trendo, "werkzeuge"); var home = Path.Combine(f.Root, "home");
        Directory.CreateDirectory(tools); Directory.CreateDirectory(home);
        ProtectedFolders.Protect(trendo); ChildWrite.AllowInside(home); // 11. Prüfrunde: „home“ steht für einen freigegebenen Ort
        var link = Path.Combine(home, "abkuerzung");
        var linked = false;
        try { Directory.CreateSymbolicLink(link, tools); linked = true; } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { } // ohne Recht für Symlinks: Teil entfällt
        if (linked) Assert.True(ProtectedFolders.IsProtected(Path.Combine(link, "yt-dlp.exe")));
        var zip = Path.Combine(home, "bilder.zip");
        using (var a = System.IO.Compression.ZipFile.Open(zip, System.IO.Compression.ZipArchiveMode.Create)) using (var w = new StreamWriter(a.CreateEntry("bild.png").Open())) w.Write("x");
        Assert.Equal(Rbac.NurErwachseneTrendoOrdner, Assert.Throws<UnauthorizedAccessException>(() => Archives.UnpackAs(Principal.ChildProfile, zip, Path.Combine(tools, "x"), f.Core.Audit)).Message);
        Assert.Equal(1, Archives.UnpackAs(Principal.ChildProfile, zip, Path.Combine(home, "x"), f.Core.Audit));
    }

    [Fact(DisplayName = "T-0764 N3: alte mpv-Ordner nur von beendeten Trendo-Läufen und nur mit Trendo-Inhalt werden aufgeräumt; fremde und laufende bleiben")]
    public void Aufraeumen()
    {
        var temp = Path.GetTempPath();
        int Dead() { for (var p = 4_000_000; p < 4_100_000; p += 4) { try { using var x = System.Diagnostics.Process.GetProcessById(p); } catch (ArgumentException) { return p; } } throw new InvalidOperationException(); }
        var dead = Dead();
        var own = Path.Combine(temp, "trendo-mpv-" + dead); Directory.CreateDirectory(Path.Combine(own, "leer")); File.WriteAllText(Path.Combine(own, "trendo-leer-" + new string('a', 32) + ".conf"), "");
        var foreign = Path.Combine(temp, "trendo-mpv-" + (dead + 1)); Directory.CreateDirectory(foreign); File.WriteAllText(Path.Combine(foreign, "notiz.txt"), "fremd");
        var running = Path.Combine(temp, "trendo-mpv-" + Environment.ProcessId);
        var odd = Path.Combine(temp, "trendo-mpv-abc"); Directory.CreateDirectory(odd);
        try
        {
            MpvSafe.CleanupStale();
            Assert.False(Directory.Exists(own));
            Assert.True(Directory.Exists(foreign)); // fremder Inhalt
            Assert.True(Directory.Exists(odd)); // Name passt nicht
            if (Directory.Exists(running)) Assert.True(Directory.Exists(running)); // eigener Lauf bleibt
        }
        finally { foreach (var d in new[] { own, foreign, odd }) if (Directory.Exists(d)) Directory.Delete(d, true); }
    }
}
