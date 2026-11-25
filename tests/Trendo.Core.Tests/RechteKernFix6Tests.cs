using System.IO.Compression;
using System.Security.Cryptography;
using Trendo.Core;
using Trendo.Core.Sys;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0764, 6. Prüfrunde: Erlaubnisliste statt Sperrliste im Kinderprofil (A), Zwischenspeicher und Protokolle in der Ordnerprüfung (C).
/// Erwachsene unverändert. Startet nichts echt (FakeLauncher); die Programmdateien sind Platzhalter.</summary>
public class RechteKernFix6
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
    static string Install(Fixture f, string tools, string name, params (string N, string T)[] files)
    {
        var d = Path.Combine(f.Root, "q-" + name + Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(d); var zip = Path.Combine(d, "p.zip");
        using (var z = ZipFile.Open(zip, ZipArchiveMode.Create)) foreach (var (n, t) in files) using (var w = new StreamWriter(z.CreateEntry(n).Open())) w.Write(t);
        return Software.InstallPortable(Principal.LocalOwner, zip, Sha(zip), tools, name, f.Core.Audit);
    }

    [Theory(DisplayName = "T-0764 A: Erlaubnisliste – eingerichtete, unveränderte Kreativ- und Fremdprogramme (Krita, OBS, Inkscape, Audacity, GIMP, eigenes Programm) startet das Kinderprofil nicht; Erwachsene wie bisher")]
    [InlineData("krita", @"bin\krita.exe")]
    [InlineData("obs", @"bin\64bit\obs64.exe")]
    [InlineData("inkscape", @"bin\inkscape.exe")]
    [InlineData("audacity", @"bin\Audacity4.exe")]
    [InlineData("gimp", @"bin\gimp-2.10.exe")]
    [InlineData("notiz", "notiz.exe")]
    public void Erlaubnisliste_nicht_drin(string name, string rel)
    {
        var (f, tools) = Setup(); using var _f = f;
        var dir = Install(f, tools, name, (rel.Replace('\\', '/'), "MZ-" + name));
        var exe = Path.Combine(dir, rel);
        Assert.True(ProgramTrust.Verified(exe, [tools])); // eingerichtet und unverändert – trotzdem nicht für Kinder
        var e = Assert.Throws<UnauthorizedAccessException>(() => f.Core.Integrations.LaunchDirect(Principal.ChildProfile, exe, f.Core.Audit));
        Assert.Contains("Erwachsenen-Profil", e.Message);
        Assert.Throws<UnauthorizedAccessException>(() => f.Core.Integrations.LaunchArgs(Principal.ChildProfile, exe, f.Core.Audit, [], internalCall: true));
        Assert.Empty(f.Launcher.Opened);
        f.Core.Integrations.LaunchDirect(Principal.LocalOwner, exe, f.Core.Audit); // Erwachsene: wie bisher
        Assert.Single(f.Launcher.Opened);
    }

    [Fact(DisplayName = "T-0764 A: Erlaubnisliste – SumatraPDF nur als Vollpaket-Komponente „sumatra\\SumatraPDF.exe“ und geprüft; gleicher Name in anderem Ordner oder unter „Programme“ nicht")]
    public void Erlaubnisliste_Sumatra()
    {
        var (f, tools) = Setup(); using var _f = f;
        var pdf = Path.Combine(f.Root, "a.pdf"); File.WriteAllText(pdf, "%PDF");
        var ok = Path.Combine(Install(f, tools, "sumatra", ("SumatraPDF.exe", "MZ-sumatra")), "SumatraPDF.exe");
        f.Core.Integrations.LaunchDirect(Principal.ChildProfile, ok, f.Core.Audit, pdf);
        Assert.Single(f.Launcher.Opened);
        var falsch = Path.Combine(Install(f, tools, "pdf", ("SumatraPDF.exe", "MZ-anderes")), "SumatraPDF.exe");
        Assert.Throws<UnauthorizedAccessException>(() => f.Core.Integrations.LaunchDirect(Principal.ChildProfile, falsch, f.Core.Audit, pdf));
        var tief = Path.Combine(Install(f, tools, "sumatra2", ("bin/SumatraPDF.exe", "MZ-tief")), "bin", "SumatraPDF.exe");
        Assert.Throws<UnauthorizedAccessException>(() => f.Core.Integrations.LaunchDirect(Principal.ChildProfile, tief, f.Core.Audit, pdf));
        var pf = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Krita (x64)", "bin", "krita.exe");
        Assert.Throws<UnauthorizedAccessException>(() => f.Core.Integrations.LaunchArgs(Principal.ChildProfile, pf, f.Core.Audit, [pdf], internalCall: true));
        File.WriteAllText(ok, "MZ-veraendert"); // geprüft: ausgetauschte Datei nicht
        Assert.Throws<UnauthorizedAccessException>(() => f.Core.Integrations.LaunchDirect(Principal.ChildProfile, ok, f.Core.Audit, pdf));
        Assert.Single(f.Launcher.Opened);
    }

    [Fact(DisplayName = "T-0764 E: Blender-Startausdruck – Pfad sicher eingesetzt (Anführungszeichen, Umlaute), SHA-256 geprüft, sonst Abbruch; ungültige Prüfsumme abgelehnt")]
    public void Skript_Startausdruck()
    {
        var path = Path.Combine(Path.GetTempPath(), "Ordner mit ' und \" und ä", "trendo_kreatur.py");
        var sha = new string('a', 64);
        var e = Studio3D.VerifiedScriptExpr(path, sha);
        Assert.StartsWith("import hashlib,sys;p=\"", e);
        Assert.Contains($"hexdigest()=='{sha}' or sys.exit('TRENDO-SKRIPT-VERAENDERT')", e);
        Assert.Contains("exec(compile(d,p,'exec')", e);
        Assert.DoesNotContain("'", System.Text.Json.JsonSerializer.Serialize(Path.GetFullPath(path))); // ' und " sind maskiert – kein Ausbruch aus dem Literal
        Assert.Equal(Path.GetFullPath(path), System.Text.Json.JsonSerializer.Deserialize<string>(e[(e.IndexOf("p=", StringComparison.Ordinal) + 2)..e.IndexOf(";d=", StringComparison.Ordinal)]));
        Assert.Throws<ArgumentException>(() => Studio3D.VerifiedScriptExpr(path, "x'); import os; os.system('calc'); ('"));
    }

    [Theory(DisplayName = "T-0764 C: Zwischenspeicher und Protokolle (cache\\, logs\\, *.log) sperren ein Programm nicht mehr; Ausführbares darin, .pyc und Dateien ohne Endung bleiben streng")]
    [InlineData(@"cache\thumb-0001.bin", true)]
    [InlineData(@"logs\start.txt", true)]
    [InlineData(@"sumatra.log", true)]
    [InlineData(@"daten\cache\a.png", true)]
    [InlineData(@"cache\evil.dll", false)]
    [InlineData(@"logs\run.ps1", false)]
    [InlineData(@"cache\noext", false)]
    [InlineData(@"cache\x.js", false)]
    [InlineData(@"__pycache__\plugin.cpython-313.pyc", false)]
    [InlineData(@"einstellungen.ini", false)]
    public void Zwischenspeicher(string rel, bool allowed)
    {
        var (f, tools) = Setup(); using var _f = f;
        var dir = Install(f, tools, "sumatra", ("SumatraPDF.exe", "MZ-sumatra"), ("plugin.py", "x = 1"));
        var exe = Path.Combine(dir, "SumatraPDF.exe");
        var p = Path.Combine(dir, rel); Directory.CreateDirectory(Path.GetDirectoryName(p)!); File.WriteAllText(p, "neu");
        if (allowed) { f.Core.Integrations.LaunchDirect(Principal.ChildProfile, exe, f.Core.Audit); Assert.Single(f.Launcher.Opened); }
        else Assert.Throws<UnauthorizedAccessException>(() => f.Core.Integrations.LaunchDirect(Principal.ChildProfile, exe, f.Core.Audit));
    }
}
