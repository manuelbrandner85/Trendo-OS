using System.IO.Compression;
using System.Text.RegularExpressions;
using Trendo.Core;
using Trendo.Core.Sys;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0764, 8. Prüfrunde: mpv nie mit Einstellungen/Skripten aus %APPDATA%\mpv (H), Datei-Dialoge nur über Trendos Helfer (M1), keine
/// beschreibbaren Ordner in Blenders sys.path (M2), sicheres Entpacken für Kinder (N). Startet nichts echt.</summary>
public class RechteKernFix8
{
    static string Repo(string rel)
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "Trendo.sln"))) return Path.Combine(d.FullName, rel);
        throw new FileNotFoundException(rel);
    }
    static IEnumerable<string> Sources() => Directory.EnumerateFiles(Repo("src"), "*.cs", SearchOption.AllDirectories)
        .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

    [Fact(DisplayName = "T-0764 H: mpv im eigenen Player-Fenster ohne Einstellungen, Skripte und Standardtasten, mit Trendos sicherer Tastenbelegung; VLC ohne Lua; Versionsprüfung ohne Einstellungen")]
    public void Mpv_Argumente()
    {
        var (exe, args) = MediaLibrary.Player("a.mp3", "mpv", null)!.Value;
        Assert.Equal("mpv", exe);
        foreach (var a in new[] { "--no-config", "--load-scripts=no", "--input-default-bindings=no" }) Assert.Contains(a, args);
        var conf = args.Single(a => a.StartsWith("--input-conf=", StringComparison.Ordinal))["--input-conf=".Length..];
        var keys = File.ReadAllText(conf);
        Assert.Contains("cycle pause", keys);
        Assert.DoesNotMatch(new Regex(@"\b(run|subprocess|load-script|script-binding|script-message|keybind)\b"), keys);
        Assert.Equal("a.mp3", args[^1]);
        Assert.Contains("--no-lua", MediaLibrary.Player("a.mp3", null, "vlc")!.Value.Args);
        Assert.Contains("--no-config", StudioTools.Verify["mpv"].VerifyArgs);
    }

    [Fact(DisplayName = "T-0764 H: sichere Tastenbelegung – nur Pause, Spulen, Lautstärke, Vollbild, Beenden; run, subprocess, script-binding, Befehlsketten abgelehnt")]
    public void Mpv_Tastenbelegung()
    {
        Assert.True(MpvSafe.IsSafeInputConf(MpvSafe.SafeKeys));
        Assert.True(MpvSafe.IsSafeInputConf(""));
        foreach (var bad in new[] { "x run calc.exe", "SPACE cycle pause; run calc", "` script-binding console/enable", "y subprocess [\"cmd\"]", "z load-script C:/x.lua", "k keybind a run" })
            Assert.False(MpvSafe.IsSafeInputConf(bad), bad);
        var empty = MpvSafe.Args();
        Assert.Equal("", File.ReadAllText(empty.Single(a => a.StartsWith("--input-conf=", StringComparison.Ordinal))["--input-conf=".Length..]));
    }

    [Fact(DisplayName = "T-0764 H: jeder mpv-Start im Quelltext nutzt die festen Argumente (MpvSafe, „--no-config“)")]
    public void Mpv_ueberall()
    {
        var bad = new List<string>();
        foreach (var f in Sources())
        {
            var t = File.ReadAllText(f);
            var startsMpv = Regex.IsMatch(t, @"ProcessStartInfo\(\s*(MpvPath|mpvPath|mpv\b|MpvExe)") || Regex.IsMatch(t, @"\(mpv,\s*\[");
            if (startsMpv && !t.Contains("--no-config", StringComparison.Ordinal) && !t.Contains("MpvSafe.Args", StringComparison.Ordinal)) bad.Add(Path.GetFileName(f));
        }
        Assert.True(bad.Count == 0, string.Join(", ", bad));
    }

    [Fact(DisplayName = "T-0764 M1: Datei- und Ordner-Dialoge des Systems (StorageProvider) nur noch im zentralen Helfer – Kinder bekommen Trendos Auswahl")]
    public void Dialoge_nur_im_Helfer()
    {
        var hits = Sources().SelectMany(f => File.ReadAllLines(f).Select((l, i) => (File: f, Line: l, No: i + 1)))
            .Where(x => Regex.IsMatch(x.Line, @"StorageProvider\s*\.\s*(OpenFilePickerAsync|SaveFilePickerAsync|OpenFolderPickerAsync)\s*\(") && !x.Line.TrimStart().StartsWith("///", StringComparison.Ordinal))
            .Select(x => $"{Path.GetFileName(x.File)}:{x.No}").ToList();
        Assert.All(hits, h => Assert.StartsWith("MainWindow.KidPicker.cs:", h));
        Assert.Equal(3, hits.Count);
    }

    [Fact(DisplayName = "T-0764 M2: Blender – kein sys.path-Trick in Trendos Skripten; der Startausdruck entfernt Skriptordner, Temp und %APPDATA% aus sys.path")]
    public void Blender_sys_path()
    {
        var scripts = Directory.EnumerateFiles(Repo("src/Trendo.App/Assets/blender"), "*.py").Append(Repo("src/Trendo.App/Assets/ki/trendo_anprobe.py"));
        foreach (var s in scripts)
            Assert.DoesNotMatch(new Regex(@"sys\.path\.(insert|append)|importlib|__import__|runpy"), File.ReadAllText(s));
        var dir = Path.Combine(Path.GetTempPath(), "cache", "blender");
        var e = BlenderScript.Decode(BlenderScript.Expr(Path.Combine(dir, "trendo_foto.py"), new string('a', 64), [("trendo_moebel", Path.Combine(dir, "trendo_moebel.py"), new string('b', 64))]));
        Assert.Contains("sys.path[:]=", e);
        Assert.Contains(System.Text.Json.JsonSerializer.Serialize(Path.GetFullPath(dir)), e);
        Assert.Contains("tempfile.gettempdir()", e);
        Assert.Contains("APPDATA", e);
        Assert.True(e.IndexOf("sys.path[:]=", StringComparison.Ordinal) < e.IndexOf("_m=types.ModuleType", StringComparison.Ordinal)); // vor jedem Trendo-Skript
    }

    [Theory(DisplayName = "T-0764 N: Entpacken im Kinderprofil – Punkte/Leerzeichen am Ende, Datenströme und Gerätenamen gelten als unsicher")]
    [InlineData("spiel.exe.")] [InlineData("spiel.exe ")] [InlineData("spiel.exe. .")] [InlineData("bild.png:boese.exe")] [InlineData("CON.txt")] [InlineData("ordner/nul")]
    [InlineData("aux.png")] [InlineData("COM1.jpg")] [InlineData("lpt9")] [InlineData("ordner./bild.png:x")]
    public void Entpacken_Namen(string entry)
    {
        using var f = new Fixture(); Directory.CreateDirectory(f.Root); ChildWrite.AllowInside(f.Root); // 11. Prüfrunde: freigegebener Ort – geprüft wird der Name
        var zip = Path.Combine(f.Root, "a.zip");
        using (var a = ZipFile.Open(zip, ZipArchiveMode.Create)) { using (var w = new StreamWriter(a.CreateEntry("bild.png").Open())) w.Write("x"); using (var w = new StreamWriter(a.CreateEntry(entry).Open())) w.Write("x"); }
        var e = Assert.Throws<UnauthorizedAccessException>(() => Archives.UnpackAs(Principal.ChildProfile, zip, Path.Combine(f.Root, "ziel"), f.Core.Audit));
        Assert.Contains("Erwachsener", e.Message);
        Assert.False(Directory.Exists(Path.Combine(f.Root, "ziel")));
    }

    [Fact(DisplayName = "T-0764 N: Entpacken im Kinderprofil – verdächtiges Packverhältnis und mehr als 10 000 Einträge abgelehnt (vorab, ohne zu entpacken)")]
    public void Entpacken_Grenzen()
    {
        using var f = new Fixture(); Directory.CreateDirectory(f.Root); ChildWrite.AllowInside(f.Root); // 11. Prüfrunde: freigegebener Ort – geprüft werden die Grenzen
        var bomb = Path.Combine(f.Root, "bombe.zip");
        using (var a = ZipFile.Open(bomb, ZipArchiveMode.Create)) using (var s = a.CreateEntry("leer.png", CompressionLevel.SmallestSize).Open()) { var buf = new byte[1 << 20]; for (var i = 0; i < 20; i++) s.Write(buf); }
        Assert.Throws<UnauthorizedAccessException>(() => Archives.UnpackAs(Principal.ChildProfile, bomb, Path.Combine(f.Root, "b"), f.Core.Audit));
        Assert.False(Directory.Exists(Path.Combine(f.Root, "b")));
        var many = Path.Combine(f.Root, "viele.zip");
        using (var a = ZipFile.Open(many, ZipArchiveMode.Create)) for (var i = 0; i <= 10_000; i++) a.CreateEntry($"b{i}.png");
        Assert.Throws<UnauthorizedAccessException>(() => Archives.UnpackAs(Principal.ChildProfile, many, Path.Combine(f.Root, "v"), f.Core.Audit));
        Assert.False(Directory.Exists(Path.Combine(f.Root, "v")));
        Assert.Equal(1, Archives.UnpackAs(Principal.LocalOwner, bomb, Path.Combine(f.Root, "e"), f.Core.Audit)); // Erwachsene: wie bisher
    }
}
