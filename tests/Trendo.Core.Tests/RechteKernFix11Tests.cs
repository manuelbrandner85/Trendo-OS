using Trendo.Core;
using Trendo.Core.Shell;
using Trendo.Core.Sys;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0764, 11. Prüfrunde: Freigabeliste statt Sperrliste – Kinder schreiben nur in freigegebene Orte und legen nie ausführbare oder
/// auslösende Dateien an (M); frischer mpv-Arbeitsordner je Start, Umleitungspunkte werden nie verfolgt (N). Schreibt NIE in echte Systemorte –
/// geprüft wird nur die Entscheidung. Startet nichts echt.</summary>
public class RechteKernFix11
{
    static string Sys(Environment.SpecialFolder f, params string[] rest) => Path.Combine([Environment.GetFolderPath(f), .. rest]);

    public static TheoryData<string> Systemorte() => new()
    {
        Sys(Environment.SpecialFolder.Startup, "setup.exe"), Sys(Environment.SpecialFolder.Startup, "spiel.lnk"), Sys(Environment.SpecialFolder.Startup, "seite.url"),
        Sys(Environment.SpecialFolder.Startup, "bild.png"), Sys(Environment.SpecialFolder.ApplicationData, "x.txt"), Sys(Environment.SpecialFolder.LocalApplicationData, "x.txt"),
        Sys(Environment.SpecialFolder.StartMenu, "x.txt"), Sys(Environment.SpecialFolder.SendTo, "x.txt"), Sys(Environment.SpecialFolder.CommonApplicationData, "x.txt"),
        Sys(Environment.SpecialFolder.ProgramFiles, "x.txt"), Sys(Environment.SpecialFolder.Windows, "x.txt"), Path.Combine(Path.GetTempPath(), "x.txt"),
        Path.Combine(Path.GetPathRoot(Environment.SystemDirectory)!, "x.txt"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "x.txt"),
        Path.Combine(Path.GetDirectoryName(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))!, "Anderer", "Documents", "x.txt"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "x.txt"),
    };

    [Theory(DisplayName = "T-0764 M1: Kinder schreiben nicht in Autostart, AppData, Startmenü, SendTo, ProgramData, Programme, Windows, Temp, Laufwerk, Profilwurzel, andere Benutzer; Erwachsene wie bisher")]
    [MemberData(nameof(Systemorte))]
    public void Systemorte_gesperrt(string path)
    {
        Assert.NotNull(ChildWrite.Problem(Principal.ChildProfile, path));
        Assert.Null(ChildWrite.Problem(Principal.LocalOwner, path));
    }

    [Fact(DisplayName = "T-0764 M1: Freigabe – Dokumente, Bilder, Musik, Videos, Downloads, Desktop des Kontos (ohne zu schreiben geprüft); nie eine Systemwurzel als Freigabe")]
    public void Freigabe()
    {
        foreach (var f in new[] { Environment.SpecialFolder.MyDocuments, Environment.SpecialFolder.MyPictures, Environment.SpecialFolder.MyMusic, Environment.SpecialFolder.MyVideos, Environment.SpecialFolder.DesktopDirectory })
            Assert.Null(ChildWrite.Problem(Principal.ChildProfile, Sys(f, "Trendo-Test", "bild.png")));
        Assert.Null(ChildWrite.Problem(Principal.ChildProfile, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "musik.mp3")));
        ChildWrite.Allow(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)); // Profilwurzel enthält AppData – wird nie Freigabe
        ChildWrite.Allow(Path.GetPathRoot(Environment.SystemDirectory));
        ChildWrite.AllowInside(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)); // enthält den Autostart – nie
        Assert.NotNull(ChildWrite.Problem(Principal.ChildProfile, Sys(Environment.SpecialFolder.Startup, "setup.exe")));
        Assert.NotNull(ChildWrite.Problem(Principal.ChildProfile, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "x.txt")));
    }

    [Theory(DisplayName = "T-0764 M2: Kinder legen keine ausführbaren oder auslösenden Dateien an – auch Doppelendung, Punkt/Leerzeichen am Ende, Datenstrom, Gerätename")]
    [InlineData("setup.exe", true)] [InlineData("spiel.lnk", true)] [InlineData("seite.url", true)] [InlineData("x.scf", true)] [InlineData("x.library-ms", true)]
    [InlineData("x.settingcontent-ms", true)] [InlineData("x.appref-ms", true)] [InlineData("x.hta", true)] [InlineData("x.chm", true)] [InlineData("x.reg", true)]
    [InlineData("x.ps1", true)] [InlineData("x.vbs", true)] [InlineData("x.js", true)] [InlineData("x.jse", true)] [InlineData("x.wsf", true)] [InlineData("x.bat", true)]
    [InlineData("x.cmd", true)] [InlineData("x.com", true)] [InlineData("x.pif", true)] [InlineData("x.scr", true)] [InlineData("x.msi", true)] [InlineData("x.msix", true)]
    [InlineData("x.xll", true)] [InlineData("x.application", true)] [InlineData("x.inf", true)] [InlineData("x.cpl", true)] [InlineData("x.jar", true)] [InlineData("x.msc", true)]
    [InlineData("a.exe.", true)] [InlineData("a.exe ", true)] [InlineData("a.EXE . .", true)] [InlineData("bild.exe.png", true)] [InlineData("a.txt:b.exe", true)] [InlineData("CON.txt", true)]
    [InlineData("bild.png", false)] [InlineData("brief.txt", false)] [InlineData("musik.mp3", false)] [InlineData("foto.2024.jpg", false)] [InlineData("LIESMICH", false)] [InlineData("projekt.zip", false)]
    public void Endungen(string name, bool blocked) => Assert.Equal(blocked, ChildWrite.BlockedName(name));

    static (Fixture F, string Root, FileBrowser Kid) Setup()
    {
        var f = new Fixture();
        var root = Path.Combine(f.Root, "eigene"); Directory.CreateDirectory(root);
        ChildWrite.AllowInside(root); // steht für Dokumente & Co. (der Test schreibt nie in echte Ordner)
        return (f, root, new FileBrowser { WriteGuard = (p, n) => ChildWrite.WriteProblem(Principal.ChildProfile, p, f.Core.Audit, n) });
    }

    [Fact(DisplayName = "T-0764 M2: Dateimanager im Kinderprofil – Umbenennen in .exe, Kopieren einer .lnk und eines Ordners mit .exe darin abgelehnt; Bilder und Musik weiter")]
    public void Dateimanager_Endungen()
    {
        var (f, root, kid) = Setup(); using var _f = f;
        var txt = Path.Combine(root, "a.txt"); File.WriteAllText(txt, "x");
        Assert.Equal(Rbac.NurErwachseneEndung, Assert.Throws<UnauthorizedAccessException>(() => kid.Rename(txt, "a.exe")).Message);
        Assert.Throws<UnauthorizedAccessException>(() => kid.Rename(txt, "a.exe ."));
        var src = Path.Combine(f.Root, "quelle"); Directory.CreateDirectory(Path.Combine(src, "spiel"));
        File.WriteAllText(Path.Combine(src, "verknuepfung.lnk"), "x"); File.WriteAllText(Path.Combine(src, "spiel", "start.exe"), "MZ"); File.WriteAllText(Path.Combine(src, "spiel", "bild.png"), "x");
        Assert.Throws<UnauthorizedAccessException>(() => kid.Paste(Path.Combine(src, "verknuepfung.lnk"), root, move: false));
        Assert.Throws<UnauthorizedAccessException>(() => kid.Paste(Path.Combine(src, "spiel"), root, move: false));
        Assert.False(Directory.Exists(Path.Combine(root, "spiel")));
        Assert.True(File.Exists(kid.Paste(Path.Combine(src, "spiel", "bild.png"), root, move: false)));
        Assert.True(Directory.Exists(kid.NewFolder(root)));
        Assert.Throws<UnauthorizedAccessException>(() => kid.NewFolder(f.Root)); // außerhalb der Freigabe
        var adult = new FileBrowser { WriteGuard = (p, n) => ChildWrite.WriteProblem(Principal.LocalOwner, p, f.Core.Audit, n) };
        Assert.True(File.Exists(adult.Rename(Path.Combine(root, "bild.png"), "bild.exe"))); // Erwachsene: wie bisher
    }

    [Fact(DisplayName = "T-0764 M1: Junction/Symlink in der Freigabe mit Ziel draußen gilt als draußen; Löschen entfernt nur den Link, nie das Ziel")]
    public void Umleitung()
    {
        var (f, root, _) = Setup(); using var _f = f;
        var outside = Path.Combine(f.Root, "draussen"); Directory.CreateDirectory(outside); File.WriteAllText(Path.Combine(outside, "wichtig.txt"), "x");
        var link = Path.Combine(root, "abkuerzung");
        try { Directory.CreateSymbolicLink(link, outside); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return; } // ohne Recht für Symlinks nicht prüfbar
        Assert.False(ChildWrite.InAllowedRoot(Path.Combine(link, "neu.txt")));
        Assert.NotNull(ChildWrite.Problem(Principal.ChildProfile, Path.Combine(link, "neu.txt")));
        MpvSafe.SafeDelete(link);
        Assert.False(Directory.Exists(link));
        Assert.True(File.Exists(Path.Combine(outside, "wichtig.txt")));
    }

    [Fact(DisplayName = "T-0764 M1: Entpacken im Kinderprofil nur in freigegebene Orte; ZIP ohne Code dort weiter möglich")]
    public void Entpacken_Ort()
    {
        var (f, root, _) = Setup(); using var _f = f;
        var zip = Path.Combine(root, "bilder.zip");
        using (var a = System.IO.Compression.ZipFile.Open(zip, System.IO.Compression.ZipArchiveMode.Create)) using (var w = new StreamWriter(a.CreateEntry("bild.png").Open())) w.Write("x");
        Assert.Equal(1, Archives.UnpackAs(Principal.ChildProfile, zip, Path.Combine(root, "bilder"), f.Core.Audit));
        Assert.Equal(Rbac.NurErwachseneOrt, Assert.Throws<UnauthorizedAccessException>(() => Archives.UnpackAs(Principal.ChildProfile, zip, Path.Combine(f.Root, "anderswo"), f.Core.Audit)).Message);
        using (var a = System.IO.Compression.ZipFile.Open(zip, System.IO.Compression.ZipArchiveMode.Update)) using (var w = new StreamWriter(a.CreateEntry("Autostart.lnk").Open())) w.Write("x");
        Assert.Throws<UnauthorizedAccessException>(() => Archives.UnpackAs(Principal.ChildProfile, zip, Path.Combine(root, "mit-link"), f.Core.Audit));
    }

    [Fact(DisplayName = "T-0764 N5: mpv-Arbeitsordner je Start frisch mit Zufallsnamen; ein umgeleiteter Player-Ordner bricht ab")]
    public void Arbeitsordner_frisch()
    {
        var a = MpvSafe.WorkDir(); var b = MpvSafe.WorkDir();
        Assert.NotEqual(a, b);
        Assert.Matches(@"leer-[0-9a-f]{32}$", a);
        Assert.False(new DirectoryInfo(a).Attributes.HasFlag(FileAttributes.ReparsePoint));
        MpvSafe.SafeDelete(a); MpvSafe.SafeDelete(b);
        Assert.False(Directory.Exists(a));
    }
}
