using System.IO.Compression;
using System.Security.Cryptography;
using Trendo.Core;
using Trendo.Core.Sys;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0764, 5. Prüfrunde: Konsolen-Programme nie für Kinder (H1), strenge Signaturprüfung (H2, N1), ganzer Programmordner (M2).
/// Erwachsene unverändert. Startet nichts echt (FakeLauncher).</summary>
public class RechteKernFix5
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

    [Theory(DisplayName = "T-0764 H1: Konsolen-Programme (VS Code, Blender, Unreal, Godot, LibreOffice …) im Kinderprofil nie – auch bei festen Aufrufen; Erwachsene wie bisher")]
    [InlineData(@"Microsoft VS Code\Code.exe")]
    [InlineData(@"Blender Foundation\Blender 4.2\blender.exe")]
    [InlineData(@"Epic Games\UE_5.4\Engine\Binaries\Win64\UnrealEditor.exe")]
    [InlineData(@"Godot\Godot_v4.3-stable_win64.exe")]
    [InlineData(@"LibreOffice\program\soffice.exe")]
    public void Konsolen_Programme(string rel)
    {
        var (f, _) = Setup(); using var _f = f;
        var exe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), rel);
        var doc = Path.Combine(f.Root, "a.txt"); File.WriteAllText(doc, "x");
        var e = Assert.Throws<UnauthorizedAccessException>(() => f.Core.Integrations.LaunchDirect(Principal.ChildProfile, exe, f.Core.Audit, doc));
        Assert.Equal(Rbac.NurErwachseneKonsole, e.Message);
        Assert.Throws<UnauthorizedAccessException>(() => f.Core.Integrations.LaunchArgs(Principal.ChildProfile, exe, f.Core.Audit, [doc], internalCall: true));
        Assert.Empty(f.Launcher.Opened);
        f.Core.Integrations.LaunchArgs(Principal.LocalOwner, exe, f.Core.Audit, [doc], internalCall: true);
        Assert.Single(f.Launcher.Opened);
    }

    [Fact(DisplayName = "T-0764 H2/N1 echt: kopierte cmd.exe als Code.exe zählt nicht (nur Katalog-Signatur, falsche Versionsinfos); echtes VS Code mit eingebetteter Signatur und Sperrlisten-Prüfung schon")]
    public void Signatur_streng()
    {
        if (!OperatingSystem.IsWindows()) return; // nicht anwendbar
        using var f = new Fixture();
        var copy = Path.Combine(f.Root, "Code.exe"); Directory.CreateDirectory(f.Root); File.Copy(Path.Combine(Environment.SystemDirectory, "cmd.exe"), copy);
        Assert.True(Authenticode.Check(copy).ViaCatalog); // der alte Weg hätte sie über den Windows-Katalog als „Microsoft“ gezählt
        Assert.NotEqual(SigState.Gueltig, Authenticode.CheckEmbeddedStrict(copy).State);
        Assert.False(ProgramTrust.SignedByExpected(copy));
        var code = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Microsoft VS Code", "Code.exe");
        if (!File.Exists(code)) return; // VS Code nicht installiert – dieser Teil ist hier nicht prüfbar
        var s = Authenticode.CheckEmbeddedStrict(code);
        Assert.True(ProgramTrust.SignedByExpected(code), s.State + " " + s.Detail);
    }

    [Theory(DisplayName = "T-0764 M2: ganzer Programmordner (auch Unterordner) – neue oder ausgetauschte Dateien führen im Kinderprofil zur Ablehnung")]
    [InlineData(@"scripts\startup\boese.py", false)]
    [InlineData(@"resources\app.asar", true)]
    [InlineData(@"daten\einstellungen.ini", false)]
    public void Ganzer_Ordner(string rel, bool replace)
    {
        var (f, tools) = Setup(); using var _f = f;
        var dir = Install(f, tools, "sumatra", ("SumatraPDF.exe", "MZ-app"), ("resources/app.asar", "ASAR-echt")); // 6. Prüfrunde (A): Komponente der Erlaubnisliste
        var exe = Path.Combine(dir, "SumatraPDF.exe");
        f.Core.Integrations.LaunchDirect(Principal.ChildProfile, exe, f.Core.Audit);
        Assert.Single(f.Launcher.Opened);
        var p = Path.Combine(dir, rel); Directory.CreateDirectory(Path.GetDirectoryName(p)!);
        File.WriteAllText(p, replace ? "ASAR-boese" : "neu");
        Assert.Throws<UnauthorizedAccessException>(() => f.Core.Integrations.LaunchDirect(Principal.ChildProfile, exe, f.Core.Audit));
        f.Core.Integrations.LaunchDirect(Principal.LocalOwner, exe, f.Core.Audit); // Erwachsene: wie bisher
        Assert.Equal(2, f.Launcher.Opened.Count);
    }
}
