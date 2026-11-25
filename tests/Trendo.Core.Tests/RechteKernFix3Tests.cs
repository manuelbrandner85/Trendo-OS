using System.IO.Compression;
using System.Security.Cryptography;
using Trendo.Core;
using Trendo.Core.Sys;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0764, 3. Prüfrunde: Vertrauen nach Herkunft (P3-1), keine freien Interpreter/Parameter (P3-2), Schemata (P3-3),
/// vom Code gewählte Programme nur von festen Orten (P3-4), weitere Dateiarten (P3-5). Startet nichts echt (FakeLauncher).</summary>
public class RechteKernFix3
{
    static (Fixture F, string Tools) Setup()
    {
        var f = new Fixture();
        var tools = Path.Combine(f.Root, "werkzeuge"); Directory.CreateDirectory(tools);
        ProgramTrust.RegisterStore(tools, Path.Combine(f.Root, "sicherheit")); // 4. Prüfrunde: Vertrauensliste mit HMAC im Datenbereich
        f.Core.Integrations.TrustedProgramPath = p => ProgramTrust.Verified(p, [tools]); // wie die App: nach Herkunft
        return (f, tools);
    }

    static string Zip(string dir, params (string Name, string Text)[] files)
    {
        Directory.CreateDirectory(dir);
        var zip = Path.Combine(dir, Guid.NewGuid().ToString("N") + ".zip");
        using (var z = ZipFile.Open(zip, ZipArchiveMode.Create)) foreach (var (n, t) in files) using (var w = new StreamWriter(z.CreateEntry(n).Open())) w.Write(t);
        return zip;
    }
    static string ShaOf(string f) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(f)));

    [Fact(DisplayName = "T-0764 P3-1: Nur von Trendo eingerichtete, unveränderte Programme gelten – hineinkopierte, ausgetauschte oder Altbestand ohne Prüfsumme nicht")]
    public void Herkunft()
    {
        var (f, tools) = Setup(); using var _ = f;
        // 6. Prüfrunde (A): Kinder starten nur Programme der Erlaubnisliste – daher hier die Komponente „sumatra“ (Herkunftsprüfung unverändert)
        var zip = Zip(Path.Combine(f.Root, "q"), ("SumatraPDF.exe", "MZ-echt"));
        var dir = Software.InstallPortable(Principal.LocalOwner, zip, ShaOf(zip), tools, "sumatra", f.Core.Audit);
        var exe = Path.Combine(dir, "SumatraPDF.exe");
        f.Core.Integrations.LaunchDirect(Principal.ChildProfile, exe, f.Core.Audit);
        Assert.Single(f.Launcher.Opened);
        // fremde exe in einen Ordner mit Kennung kopiert, eigener Ordner ohne Kennung, Altbestand ohne Prüfsummen
        var copied = Path.Combine(dir, "spiel.exe"); File.WriteAllText(copied, "MZ-fremd");
        var own = Path.Combine(tools, "meins", "spiel.exe"); Directory.CreateDirectory(Path.GetDirectoryName(own)!); File.WriteAllText(own, "MZ-fremd");
        var old = Path.Combine(tools, "alt", "alt.exe"); Directory.CreateDirectory(Path.GetDirectoryName(old)!); File.WriteAllText(old, "MZ");
        File.WriteAllText(Path.Combine(tools, "alt", Software.PortableMarker), "{\"name\":\"alt\",\"sha256\":\"x\"}");
        foreach (var e in new[] { copied, own, old })
            Assert.Throws<UnauthorizedAccessException>(() => f.Core.Integrations.LaunchDirect(Principal.ChildProfile, e, f.Core.Audit));
        // ausgetauschter Inhalt (andere Größe → neuer Eintrag im Zwischenspeicher) wird erkannt
        File.WriteAllText(exe, "MZ-ausgetauscht-und-laenger");
        Assert.Throws<UnauthorizedAccessException>(() => f.Core.Integrations.LaunchDirect(Principal.ChildProfile, exe, f.Core.Audit));
        Assert.Single(f.Launcher.Opened);
        // GitHub-Programm: Kennung mit Prüfsumme
        var gz = Zip(Path.Combine(f.Root, "g"), ("tool.exe", "MZ-gh"));
        var gh = GitHubApps.Install(Principal.LocalOwner, tools, "o/tool", new GitHubApps.Release("v1", "", []), new GitHubApps.Asset("tool-windows-x64.zip", "https://github.com/o/tool/x", new FileInfo(gz).Length, ShaOf(gz)), gz, "MIT", "", f.Core.Audit);
        Assert.True(ProgramTrust.Verified(GitHubApps.ExePath(tools, gh), [tools]));
        // Erwachsene: wie bisher
        f.Core.Integrations.LaunchDirect(Principal.LocalOwner, own, f.Core.Audit);
        Assert.Equal(2, f.Launcher.Opened.Count);
    }

    [Fact(DisplayName = "T-0764 P3-2: Interpreter (python, git, cmd …) und freie Parameter – im Kinderprofil nie, auch wenn von Trendo eingerichtet; feste Trendo-Aufrufe ja")]
    public void Interpreter_und_Parameter()
    {
        var (f, tools) = Setup(); using var _ = f;
        var zip = Zip(Path.Combine(f.Root, "q"), ("python.exe", "MZ-py"), ("spiel.exe", "MZ-spiel"));
        var dir = Software.InstallPortable(Principal.LocalOwner, zip, ShaOf(zip), tools, "python", f.Core.Audit);
        var py = Path.Combine(dir, "python.exe");
        // 6. Prüfrunde (A): Kinder starten nur Programme der Erlaubnisliste – das geprüfte Programm ist hier die Komponente „sumatra“
        var zs = Zip(Path.Combine(f.Root, "s"), ("SumatraPDF.exe", "MZ-spiel"));
        var game = Path.Combine(Software.InstallPortable(Principal.LocalOwner, zs, ShaOf(zs), tools, "sumatra", f.Core.Audit), "SumatraPDF.exe");
        Assert.Throws<UnauthorizedAccessException>(() => f.Core.Integrations.LaunchArgs(Principal.ChildProfile, Path.Combine(dir, "spiel.exe"), f.Core.Audit, ["--fest"], internalCall: true));
        var doc = Path.Combine(f.Root, "brief.txt"); File.WriteAllText(doc, "x");
        Assert.Throws<UnauthorizedAccessException>(() => f.Core.Integrations.LaunchDirect(Principal.ChildProfile, py, f.Core.Audit));
        Assert.Throws<UnauthorizedAccessException>(() => f.Core.Integrations.LaunchDirect(Principal.ChildProfile, py, f.Core.Audit, "-m pip install x", userExe: true));
        Assert.Throws<UnauthorizedAccessException>(() => f.Core.Integrations.LaunchDirect(Principal.ChildProfile, game, f.Core.Audit, "--irgendwas", userExe: true));
        Assert.Throws<UnauthorizedAccessException>(() => f.Core.Integrations.LaunchArgs(Principal.ChildProfile, game, f.Core.Audit, ["--irgendwas"]));
        Assert.Throws<UnauthorizedAccessException>(() => f.Core.Integrations.LaunchArgs(Principal.ChildProfile, py, f.Core.Audit, [doc]));
        Assert.Empty(f.Launcher.Opened);
        f.Core.Integrations.LaunchDirect(Principal.ChildProfile, game, f.Core.Audit, doc, userExe: true); // geprüftes Programm mit einer Datei
        f.Core.Integrations.LaunchArgs(Principal.ChildProfile, game, f.Core.Audit, ["--fest", doc], internalCall: true); // fester Trendo-Aufruf
        Assert.Equal(2, f.Launcher.Opened.Count);
        f.Core.Integrations.LaunchDirect(Principal.LocalOwner, py, f.Core.Audit, "-c 1", userExe: true); // Erwachsene: wie bisher
        Assert.Equal(3, f.Launcher.Opened.Count);
    }

    [Theory(DisplayName = "T-0764 P3-3: Adressen im Kinderprofil – nur http(s), mailto, genutzte ms-settings-Seiten, Papierkorb; file: wie ein Pfad")]
    [InlineData("https://example.org/", false)]
    [InlineData("http://example.org/", false)]
    [InlineData("mailto:info@example.org", false)]
    [InlineData("ms-settings:appsfeatures", false)]
    [InlineData("shell:RecycleBinFolder", true)] // 4. Prüfrunde M3: Kinder öffnen keinen Windows-Explorer (auch nicht den Papierkorb)
    [InlineData("ms-appinstaller:?source=https://example.org/x.appinstaller", true)]
    [InlineData("search-ms:query=x", true)]
    [InlineData("ms-msdt:/id x", true)]
    [InlineData("ms-settings:developers", true)]
    [InlineData("steam://run/1", true)]
    [InlineData("file:///C:/Temp/setup.exe", true)]
    [InlineData("file:///C:/Temp/brief.pdf", false)]
    public void Schemata(string target, bool needs) => Assert.Equal(needs, Software.NeedsSoftwareRight(target));

    [Fact(DisplayName = "T-0764 P3-4: Vom Code gewähltes Programm im Kinderprofil nur von festen Orten (Programme, LocalAppData Programs) oder geprüft")]
    public void Feste_Orte()
    {
        var (f, _) = Setup(); using var _f = f;
        var foreign = Path.Combine(f.Root, "fremd", "tool.exe"); Directory.CreateDirectory(Path.GetDirectoryName(foreign)!); File.WriteAllText(foreign, "MZ");
        Assert.Throws<UnauthorizedAccessException>(() => f.Core.Integrations.LaunchDirect(Principal.ChildProfile, foreign, f.Core.Audit, ""));
        var pf = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Beispiel GmbH", "werkzeug.exe"); // 5. Prüfrunde: Blender ist jetzt ein Konsolen-Programm (nie für Kinder) – hier ein gewöhnliches Programm
        Assert.True(ProgramTrust.InFixedLocation(pf));
        Assert.False(ProgramTrust.InFixedLocation(foreign));
        // 6. Prüfrunde (A): Ein fester Ort allein reicht für einen Start im Kinderprofil nicht mehr – nur die Erlaubnisliste (InFixedLocation gilt weiter
        // für Trendos eigene Hintergrund-Aufrufe, z. B. Blender -b mit Trendo-Skript)
        var e = Assert.Throws<UnauthorizedAccessException>(() => f.Core.Integrations.LaunchDirect(Principal.ChildProfile, pf, f.Core.Audit, ""));
        Assert.Equal(Rbac.NurErwachseneProgramm, e.Message);
        Assert.Empty(f.Launcher.Opened);
        f.Core.Integrations.LaunchDirect(Principal.LocalOwner, pf, f.Core.Audit, ""); // Erwachsene: wie bisher (FakeLauncher: nichts wird wirklich gestartet)
        Assert.Single(f.Launcher.Opened);
    }

    [Theory(DisplayName = "T-0764 P3-5: Hilfe-, Skript-, Add-in- und Makro-Dateien brauchen das Recht")]
    [InlineData("a.chm")] [InlineData("a.sct")] [InlineData("a.wsc")] [InlineData("a.xll")] [InlineData("a.docm")] [InlineData("a.xlsm")] [InlineData("a.pptm")]
    public void Weitere_Dateiarten(string n) => Assert.True(Software.NeedsSoftwareRight(Path.Combine(@"D:\", n)));
}
