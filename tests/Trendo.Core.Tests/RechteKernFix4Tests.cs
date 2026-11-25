using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Trendo.Core;
using Trendo.Core.Sys;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0764, 4. Prüfrunde: Vertrauensliste mit HMAC im Datenbereich (M1, N1, N2), feste Orte (M2), Ordner (M3), weitere Interpreter (N3),
/// Steuerzeichen (N4). Erwachsene: unverändert. Startet nichts echt (FakeLauncher).</summary>
public class RechteKernFix4
{
    static (Fixture F, string Tools, string Store) Setup()
    {
        var f = new Fixture();
        var tools = Path.Combine(f.Root, "werkzeuge"); Directory.CreateDirectory(tools);
        var store = Path.Combine(f.Root, "daten", "sicherheit");
        ProgramTrust.RegisterStore(tools, store);
        f.Core.Integrations.TrustedProgramPath = p => ProgramTrust.Verified(p, [tools]);
        return (f, tools, store);
    }
    static string Sha(string p) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(p)));
    static string Install(Fixture f, string tools, string name, params (string N, string T)[] files)
    {
        var dir = Path.Combine(f.Root, "q-" + name + Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(dir);
        var zip = Path.Combine(dir, "p.zip");
        using (var z = ZipFile.Open(zip, ZipArchiveMode.Create)) foreach (var (n, t) in files) using (var w = new StreamWriter(z.CreateEntry(n).Open())) w.Write(t);
        return Software.InstallPortable(Principal.LocalOwner, zip, Sha(zip), tools, name, f.Core.Audit);
    }
    static void Kind(Fixture f, string target) => Assert.Throws<UnauthorizedAccessException>(() => f.Core.Integrations.LaunchDirect(Principal.ChildProfile, target, f.Core.Audit));

    [Fact(DisplayName = "T-0764 M1: Vertrauensliste mit HMAC im Datenbereich – eingerichtete Programme ja; gefälschte Kennungsdatei, veränderte Liste, fremder Schlüssel nein")]
    public void Liste_mit_HMAC()
    {
        var (f, tools, store) = Setup(); using var _ = f;
        var dir = Install(f, tools, "sumatra", ("SumatraPDF.exe", "MZ-echt")); // 6. Prüfrunde (A): Komponente der Erlaubnisliste
        var exe = Path.Combine(dir, "SumatraPDF.exe");
        Assert.True(ProgramTrust.Verified(exe, [tools])); // 7. Prüfrunde: Erlaubnisliste leer – Kinder starten kein Fremdprogramm; die Prüfung selbst zeigt Verified
        Kind(f, exe);
        Assert.True(File.Exists(Path.Combine(store, "programm-vertrauen.json")));
        Assert.True(File.Exists(Path.Combine(store, "vertrauen.key")));
        if (OperatingSystem.IsWindows()) Assert.NotEqual(32, new FileInfo(Path.Combine(store, "vertrauen.key")).Length); // per DPAPI geschützt, nicht roh
        // gefälschte Kennungsdatei im Programmordner reicht nicht
        var fake = Path.Combine(tools, "spiel"); Directory.CreateDirectory(fake);
        var fakeExe = Path.Combine(fake, "spiel.exe"); File.WriteAllText(fakeExe, "MZ-fremd");
        File.WriteAllText(Path.Combine(fake, Software.PortableMarker), JsonSerializer.Serialize(new { Name = "spiel", Sha256 = "x", Am = "", Dateien = new Dictionary<string, string> { ["spiel.exe"] = Sha(fakeExe) } }));
        Kind(f, fakeExe); Assert.False(ProgramTrust.Verified(fakeExe, [tools]));
        // Eintrag in der Liste ohne gültigen HMAC (z. B. von Hand ergänzt)
        var list = Path.Combine(store, "programm-vertrauen.json");
        var json = File.ReadAllText(list);
        File.WriteAllText(list, json.Replace("\"Entries\":[", "\"Entries\":[{\"Path\":" + JsonSerializer.Serialize(Path.GetFullPath(fakeExe)) + ",\"Sha256\":\"" + Sha(fakeExe) + "\",\"Mac\":\"00\"},"));
        Kind(f, fakeExe); Assert.False(ProgramTrust.Verified(fakeExe, [tools]));
        // Prüfsumme in der Liste verändert → HMAC passt nicht mehr
        File.WriteAllText(list, json.Replace(Sha(exe), new string('a', 64)));
        Kind(f, exe); Assert.False(ProgramTrust.Verified(exe, [tools]));
        Assert.Empty(f.Launcher.Opened);
        // Erwachsene: wie bisher
        f.Core.Integrations.LaunchDirect(Principal.LocalOwner, fakeExe, f.Core.Audit);
        Assert.Single(f.Launcher.Opened);
    }

    [Fact(DisplayName = "T-0764 M1 (5. Prüfrunde): keine Übernahme alter Kennungen – älterer Stand im Kinderprofil abgelehnt mit „Bitte einmal als Erwachsener neu einrichten“; Erwachsene wie bisher")]
    public void Keine_Uebernahme()
    {
        var (f, tools, _) = Setup(); using var _f = f;
        var dir = Path.Combine(tools, "alt"); Directory.CreateDirectory(dir);
        var a = Path.Combine(dir, "alt.exe"); File.WriteAllText(a, "MZ-alt");
        File.WriteAllText(Path.Combine(dir, Software.PortableMarker), JsonSerializer.Serialize(new { Name = "alt", Sha256 = "x", Am = "", Dateien = new Dictionary<string, string> { ["alt.exe"] = Sha(a) } }));
        Assert.Null(typeof(ProgramTrust).GetMethod("MigrateMarkers")); // ersatzlos gestrichen
        var e = Assert.Throws<UnauthorizedAccessException>(() => f.Core.Integrations.LaunchDirect(Principal.ChildProfile, a, f.Core.Audit));
        Assert.Equal(Rbac.NurErwachseneNeuEinrichten, e.Message);
        f.Core.Integrations.LaunchDirect(Principal.LocalOwner, a, f.Core.Audit);
        Assert.Single(f.Launcher.Opened);
    }

    [Fact(DisplayName = "T-0764 N1/N2: gleich große, gleich datierte ausgetauschte exe und hineingelegte DLL werden erkannt (vor jedem Start neu gerechnet)")]
    public void Neu_rechnen_und_DLL()
    {
        var (f, tools, _) = Setup(); using var _f = f;
        var dir = Install(f, tools, "sumatra", ("SumatraPDF.exe", "MZ-echt-AAAA"), ("helfer.dll", "MZ-dll")); // 6. Prüfrunde (A): Komponente der Erlaubnisliste
        var exe = Path.Combine(dir, "SumatraPDF.exe");
        Assert.True(ProgramTrust.Verified(exe, [tools])); // 7. Prüfrunde: Erlaubnisliste leer – Kinder starten kein Fremdprogramm; die Prüfung selbst zeigt Verified
        var t = File.GetLastWriteTimeUtc(exe);
        File.WriteAllText(exe, "MZ-boes-BBBB"); File.SetLastWriteTimeUtc(exe, t);
        Kind(f, exe);
        Assert.False(ProgramTrust.Verified(exe, [tools]));
        // 6. Prüfrunde: „zwei“ und „drei“ stehen nicht in der Erlaubnisliste – die Prüfung selbst (Verified) zeigt, dass die DLL erkannt wird
        var dir2 = Install(f, tools, "zwei", ("zwei.exe", "MZ-zwei"));
        Assert.True(ProgramTrust.Verified(Path.Combine(dir2, "zwei.exe"), [tools]));
        File.WriteAllText(Path.Combine(dir2, "version.dll"), "MZ-boese-dll");
        Kind(f, Path.Combine(dir2, "zwei.exe"));
        Assert.False(ProgramTrust.Verified(Path.Combine(dir2, "zwei.exe"), [tools]));
        var dir3 = Install(f, tools, "drei", ("drei.exe", "MZ-drei"), ("helfer.dll", "MZ-dll"));
        Assert.True(ProgramTrust.Verified(Path.Combine(dir3, "drei.exe"), [tools]));
        File.WriteAllText(Path.Combine(dir3, "helfer.dll"), "MZ-dll-ausgetauscht");
        Kind(f, Path.Combine(dir3, "drei.exe"));
        Assert.False(ProgramTrust.Verified(Path.Combine(dir3, "drei.exe"), [tools]));
        Assert.Empty(f.Launcher.Opened);
    }

    [Fact(DisplayName = "T-0764 M2: Für Kinder fester Ort nur „Programme“; %LOCALAPPDATA%\\Programs nur mit Listeneintrag oder Signatur des erwarteten Herausgebers")]
    public void Feste_Orte_streng()
    {
        var (f, _, _) = Setup(); using var _f = f;
        var pf = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Beispiel GmbH", "werkzeug.exe"); // 5. Prüfrunde: Blender ist jetzt ein Konsolen-Programm (nie für Kinder) – hier ein gewöhnliches Programm
        Assert.True(ProgramTrust.InFixedLocation(pf));
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var lp = Path.Combine(local, "Programs", "Irgendwas", "irgendwas.exe");
        Assert.False(ProgramTrust.InFixedLocation(lp));
        Assert.Throws<UnauthorizedAccessException>(() => f.Core.Integrations.LaunchDirect(Principal.ChildProfile, lp, f.Core.Audit, ""));
        Assert.False(ProgramTrust.InFixedLocation(Path.Combine(local, "Microsoft", "WinGet", "Packages", "X", "x.exe")));
        f.Core.Integrations.LaunchDirect(Principal.LocalOwner, lp, f.Core.Audit, ""); // Erwachsene: wie bisher
        Assert.Single(f.Launcher.Opened);
    }

    [Fact(DisplayName = "T-0764 M2 echt (nur lesen): Signaturprüfung mit Herausgeber-Abgleich – nur echte, passend signierte Datei zählt")]
    public void Signatur_Herausgeber()
    {
        if (!OperatingSystem.IsWindows()) return;
        var code = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Microsoft VS Code", "Code.exe");
        if (!File.Exists(code)) return; // VS Code nicht im Benutzerordner installiert – dieser Teil ist hier nicht prüfbar
        Assert.True(ProgramTrust.InFixedLocation(code)); // echte Authenticode-Prüfung über WinVerifyTrust, Herausgeber Microsoft Corporation
    }

    [Fact(DisplayName = "T-0764 M3: Kind öffnet keinen Ordner im Windows-Explorer (Kern)")]
    public void Ordner()
    {
        var (f, _, _) = Setup(); using var _f = f;
        Kind(f, f.Root);
        Kind(f, "shell:RecycleBinFolder");
        f.Core.Integrations.LaunchDirect(Principal.LocalOwner, f.Root, f.Core.Audit);
        Assert.Single(f.Launcher.Opened);
    }

    [Theory(DisplayName = "T-0764 N3: weitere Interpreter sind nie frei wählbar, auch wenn von Trendo eingerichtet")]
    [InlineData("msbuild")] [InlineData("hh")] [InlineData("mmc")] [InlineData("dotnet")] [InlineData("explorer")] [InlineData("wsl")] [InlineData("blender")] [InlineData("soffice")]
    [InlineData("installutil")] [InlineData("regasm")] [InlineData("forfiles")] [InlineData("cmstp")] [InlineData("Rscript")]
    public void Interpreter(string name)
    {
        var (f, tools, _) = Setup(); using var _f = f;
        var dir = Install(f, tools, name, (name + ".exe", "MZ-" + name));
        Kind(f, Path.Combine(dir, name + ".exe"));
        Assert.Throws<UnauthorizedAccessException>(() => f.Core.Integrations.LaunchArgs(Principal.ChildProfile, Path.Combine(dir, name + ".exe"), f.Core.Audit, ["--python-expr", "x"]));
    }

    [Fact(DisplayName = "T-0764 N4: Ziele mit Steuerzeichen brauchen das Recht")]
    public void Steuerzeichen()
    {
        Assert.True(Software.NeedsSoftwareRight("D:\\brief\u0007.pdf"));
        Assert.True(Software.NeedsSoftwareRight("https://example.org/\u0000x"));
        Assert.False(Software.NeedsSoftwareRight("D:\\brief.pdf"));
    }
}
