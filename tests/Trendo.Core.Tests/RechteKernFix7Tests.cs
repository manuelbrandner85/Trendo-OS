using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Trendo.Core;
using Trendo.Core.Programs;
using Trendo.Core.Sys;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0764, 7. Prüfrunde: Kinderprofil übergibt nie eine Datei an Windows (H), Erlaubnisliste leer (M), .pth und Konfigurationen in cache/logs,
/// geprüfter Startausdruck für alle Blender-Hintergrundläufe (N). Erwachsene unverändert. Startet nichts echt (FakeLauncher).</summary>
public class RechteKernFix7
{
    static (Fixture F, string Tools) Setup()
    {
        var f = new Fixture();
        var tools = Path.Combine(f.Root, "werkzeuge"); Directory.CreateDirectory(tools);
        ProgramTrust.RegisterStore(tools, Path.Combine(f.Root, "daten", "sicherheit"));
        f.Core.Integrations.TrustedProgramPath = p => ProgramTrust.Verified(p, [tools]);
        return (f, tools);
    }
    static string Sha(string p) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(p)));
    static string Repo(string rel)
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "Trendo.sln"))) return Path.Combine(d.FullName, rel);
        throw new FileNotFoundException(rel);
    }

    [Theory(DisplayName = "T-0764 H: Kinderprofil übergibt keine Datei an Windows (Standardprogramm) – Dokumente, Archive, Abbilder, Skripte, file:-Adressen; Adressen ja; Erwachsene wie bisher")]
    [InlineData("brief.pdf")] [InlineData("notiz.txt")] [InlineData("bild.png")] [InlineData("spiel.zip")] [InlineData("abbild.iso")] [InlineData("platte.vhdx")]
    [InlineData("paket.cab")] [InlineData("skript.py")] [InlineData("bild.kra")] [InlineData("szene.blend")] [InlineData("archiv.7z")]
    public void Kind_nie_Standardprogramm(string name)
    {
        var (f, _) = Setup(); using var _f = f;
        var file = Path.Combine(f.Root, "dokumente", name); Directory.CreateDirectory(Path.GetDirectoryName(file)!); File.WriteAllText(file, "x");
        var e = Assert.Throws<UnauthorizedAccessException>(() => f.Core.Integrations.LaunchDirect(Principal.ChildProfile, file, f.Core.Audit));
        Assert.Contains("Erwachsene", e.Message);
        Assert.Throws<UnauthorizedAccessException>(() => f.Core.Integrations.LaunchDirect(Principal.ChildProfile, new Uri(file).AbsoluteUri, f.Core.Audit));
        Assert.Empty(f.Launcher.Opened);
        f.Core.Integrations.LaunchDirect(Principal.ChildProfile, "https://example.org/", f.Core.Audit); // Adressen: wie bisher
        f.Core.Integrations.LaunchDirect(Principal.LocalOwner, file, f.Core.Audit); // Erwachsene: wie bisher
        Assert.Equal(2, f.Launcher.Opened.Count);
    }

    [Fact(DisplayName = "T-0764 M: SumatraPDF nicht mehr in der Erlaubnisliste – auch eingerichtet und geprüft startet es im Kinderprofil nicht; Liste leer")]
    public void Sumatra_raus()
    {
        var (f, tools) = Setup(); using var _f = f;
        var src = Path.Combine(f.Root, "q"); Directory.CreateDirectory(src); var zip = Path.Combine(src, "s.zip");
        using (var z = ZipFile.Open(zip, ZipArchiveMode.Create)) using (var w = new StreamWriter(z.CreateEntry("SumatraPDF.exe").Open())) w.Write("MZ-sumatra");
        var exe = Path.Combine(Software.InstallPortable(Principal.LocalOwner, zip, Sha(zip), tools, "sumatra", f.Core.Audit), "SumatraPDF.exe");
        Assert.True(ProgramTrust.Verified(exe, [tools]));
        Assert.Empty(ProgramTrust.ChildPrograms);
        Assert.Throws<UnauthorizedAccessException>(() => f.Core.Integrations.LaunchDirect(Principal.ChildProfile, exe, f.Core.Audit));
        Assert.Empty(f.Launcher.Opened);
        f.Core.Integrations.LaunchDirect(Principal.LocalOwner, exe, f.Core.Audit);
        Assert.Single(f.Launcher.Opened);
    }

    [Fact(DisplayName = "T-0764 M: Mechanismus der Erlaubnisliste bleibt – Abgleich an Komponente und Pfad (geprüft mit einer Testliste, ohne die Liste zu ändern)")]
    public void Mechanismus_bleibt()
    {
        var (f, tools) = Setup(); using var _f = f;
        (string, string)[] list = [("betrachter", "ansicht.exe")];
        Assert.True(ProgramTrust.InAllowList(Path.Combine(tools, "betrachter", "ansicht.exe"), list));
        Assert.False(ProgramTrust.InAllowList(Path.Combine(tools, "anderes", "ansicht.exe"), list));
        Assert.False(ProgramTrust.InAllowList(Path.Combine(tools, "betrachter", "bin", "ansicht.exe"), list));
        Assert.False(ProgramTrust.InChildAllowList(Path.Combine(tools, "sumatra", "SumatraPDF.exe")));
    }

    [Theory(DisplayName = "T-0764 N: .pth ist Code; in cache\\/logs\\ nur *.log und reine Daten (Bilder, .tmp) ausgenommen – .json, .ini, .txt, .bin nicht mehr")]
    [InlineData(@"cache\thumb.png", true)]
    [InlineData(@"logs\start.log", true)]
    [InlineData(@"cache\x.tmp", true)]
    [InlineData(@"cache\einstellungen.json", false)]
    [InlineData(@"logs\konfig.ini", false)]
    [InlineData(@"logs\start.txt", false)]
    [InlineData(@"cache\snapshot_blob.bin", false)]
    [InlineData(@"cache\site.pth", false)]
    [InlineData(@"lib\site-packages\boese.pth", false)]
    public void Daten_und_pth(string rel, bool ignored)
    {
        var folder = Path.Combine(Path.GetTempPath(), "prog");
        Assert.Equal(ignored, ProgramTrust.IgnoredInFolderCheck(folder, Path.Combine(folder, rel)));
        Assert.Contains(".pth", ProgramTrust.CodeExtensions);
    }

    [Fact(DisplayName = "T-0764 N: kein Blender-Lauf mit Skriptpfad – jeder Aufruf mit „--factory-startup“ nutzt den geprüften Startausdruck (Quelltext durchsucht)")]
    public void Blender_nur_geprueft()
    {
        var bad = new List<string>();
        foreach (var f in Directory.EnumerateFiles(Repo("src"), "*.cs", SearchOption.AllDirectories).Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")))
        {
            var lines = File.ReadAllLines(f);
            if (!lines.Any(l => l.Contains("trendo_") && l.Contains(".py") || l.Contains("--factory-startup"))) continue;
            for (var i = 0; i < lines.Length; i++)
                if (Regex.IsMatch(lines[i], "\"--python\",\\s*(script|s\\b|Path\\.Combine\\(scriptDir|KreaturScript)")) bad.Add($"{Path.GetFileName(f)}:{i + 1}");
        }
        Assert.True(bad.Count == 0, string.Join(", ", bad));
        // die beiden öffentlichen Argument-Bauer: Startausdruck mit der Prüfsumme genau des eingebauten Skripts
        var a = Studio3DEngines.Args("s.py", "in.blend", "unity", "o.fbx", "r.json");
        Assert.DoesNotContain("--python", a);
        Assert.Contains(BlenderScript.Sha(Studio3DEngines.Script), BlenderScript.Decode(a[a.IndexOf("--python-expr") + 1]));
        var m = MixamoBlender.Args("m.py", ["x"]);
        Assert.DoesNotContain("--python", m);
        Assert.Contains(BlenderScript.Sha(MixamoBlender.Script), BlenderScript.Decode(m[m.IndexOf("--python-expr") + 1]));
    }

    [Fact(DisplayName = "T-0764 N: Startausdruck prüft auch mitgeladene Module (trendo_moebel) und lehnt ungültige Modulnamen und Prüfsummen ab")]
    public void Startausdruck_Module()
    {
        var s = new string('b', 64);
        var e = BlenderScript.Decode(BlenderScript.Expr(Path.Combine(Path.GetTempPath(), "foto.py"), new string('a', 64), [("trendo_moebel", Path.Combine(Path.GetTempPath(), "m'oebel.py"), s)]));
        Assert.Contains($"'{s}'", e);
        Assert.Contains("sys.modules['trendo_moebel']", e);
        Assert.Contains("TRENDO-SKRIPT-VERAENDERT", e);
        Assert.Throws<ArgumentException>(() => BlenderScript.Expr("a.py", new string('a', 64), [("x;import os", "m.py", s)]));
        Assert.Throws<ArgumentException>(() => BlenderScript.Expr("a.py", new string('a', 64), [("m", "m.py", "x'")]));
    }

    [Fact(DisplayName = "T-0764 H: Archive im Kinderprofil – nur ZIP ohne ausführbaren Inhalt; mit Programm, Skript, Abbild oder Datei ohne Endung abgelehnt; Erwachsene wie bisher")]
    public void Archive_Kind()
    {
        using var f = new Fixture();
        string Zip(string name, params string[] entries)
        {
            var z = Path.Combine(f.Root, name); Directory.CreateDirectory(f.Root);
            using var a = ZipFile.Open(z, ZipArchiveMode.Create); foreach (var e in entries) using (var w = new StreamWriter(a.CreateEntry(e).Open())) w.Write("x");
            return z;
        }
        Assert.Equal(1, Archives.UnpackAs(Principal.ChildProfile, Zip("bilder.zip", "bild.png"), Path.Combine(f.Root, "a"), f.Core.Audit));
        foreach (var bad in new[] { "spiel.exe", "tools/run.ps1", "lib/x.pth", "abbild.iso", "LIESMICH" })
        {
            var z = Zip("p-" + Guid.NewGuid().ToString("N")[..6] + ".zip", "bild.png", bad);
            var e = Assert.Throws<UnauthorizedAccessException>(() => Archives.UnpackAs(Principal.ChildProfile, z, Path.Combine(f.Root, "b-" + Path.GetFileNameWithoutExtension(z)), f.Core.Audit));
            Assert.Equal(Rbac.NurErwachseneArchiv, e.Message);
            Assert.False(Directory.Exists(Path.Combine(f.Root, "b-" + Path.GetFileNameWithoutExtension(z))));
            Assert.Equal(2, Archives.UnpackAs(Principal.LocalOwner, z, Path.Combine(f.Root, "c-" + Path.GetFileNameWithoutExtension(z)), f.Core.Audit)); // Erwachsene: wie bisher
        }
    }
}
