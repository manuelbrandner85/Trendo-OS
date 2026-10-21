using System.IO.Compression;
using System.Security.Cryptography;
using Trendo.Core;
using Trendo.Core.Sys;
using Xunit;
namespace Trendo.Core.Tests;

public class Sys11_Programme
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-s11-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }
    static string Sha(string f) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f))).ToLowerInvariant();

    [Fact(DisplayName = "SYS-11-P07: Portable-ZIP nur mit richtiger Prüfsumme entpacken")]
    public void Portable()
    {
        var d = Tmp(); var z = Path.Combine(d, "t.zip");
        using (var a = ZipFile.Open(z, ZipArchiveMode.Create)) using (var w = new StreamWriter(a.CreateEntry("bin/tool.txt").Open())) w.Write("hi");
        var audit = new AuditLog(Path.Combine(d, "a.jsonl"));
        Assert.Throws<InvalidOperationException>(() => Software.InstallPortable(z, new string('0', 64), Path.Combine(d, "w"), "tool", audit));
        Assert.False(Directory.Exists(Path.Combine(d, "w", "tool")));
        var dest = Software.InstallPortable(z, Sha(z).ToUpperInvariant(), Path.Combine(d, "w"), "to/ol", audit);
        Assert.True(File.Exists(Path.Combine(dest, "bin", "tool.txt"))); Assert.EndsWith("tool", dest);
        Assert.Contains(audit.ReadAll(), e => e.Action == "portable.install");
    }

    [Fact(DisplayName = "SYS-11-P10: Verknüpfungen mit Parametern (Anführungszeichen) speichern und entfernen")]
    public void Shortcuts()
    {
        var d = Tmp(); var exe = Path.Combine(d, "p.exe"); File.WriteAllText(exe, "");
        Assert.Equal(["--neu", "C:\\Mein Ordner", "-x"], Software.SplitArgs("--neu \"C:\\Mein Ordner\" -x"));
        Software.AddShortcut(d, "Mein", exe, "-a \"b c\""); Assert.Equal(["-a", "b c"], Software.Shortcuts(d).Single().Args);
        Assert.Throws<ArgumentException>(() => Software.AddShortcut(d, "X", Path.Combine(d, "fehlt.exe"), ""));
        Software.RemoveShortcut(d, "Mein"); Assert.Empty(Software.Shortcuts(d));
    }

    [Fact(DisplayName = "SYS-11-P11: Autostart lesen; ein/aus nur mit Bestätigung, nichts gelöscht")]
    public void Autostart()
    {
        var d = Tmp(); var on = Path.Combine(d, "on"); var off = Path.Combine(d, "off"); Directory.CreateDirectory(on);
        File.WriteAllText(Path.Combine(on, "Chat.lnk"), ""); File.WriteAllText(Path.Combine(on, "desktop.ini"), "");
        var it = Software.StartupFolder(on, off).Single(); Assert.True(it.Enabled);
        Assert.Throws<InvalidOperationException>(() => Software.SetStartupEnabled(it, on, off, false, false));
        Software.SetStartupEnabled(it, on, off, false, true);
        var x = Software.StartupFolder(on, off).Single(); Assert.False(x.Enabled);
        Software.SetStartupEnabled(x, on, off, true, true); Assert.True(Software.StartupFolder(on, off).Single().Enabled);
    }

    [Fact(DisplayName = "SYS-11-P14: Offline-Installer – Prüfsumme aus .sha256 und SHA256SUMS, falsche/fehlende erkannt")]
    public void Offline()
    {
        var d = Tmp(); string F(string n, string c) { var p = Path.Combine(d, n); File.WriteAllText(p, c); return p; }
        var a = F("a.exe", "A"); var b = F("b.msi", "B"); F("c.exe", "C"); var e = F("e.zip", "E");
        File.WriteAllText(a + ".sha256", Sha(a) + "  a.exe"); File.WriteAllText(Path.Combine(d, "SHA256SUMS"), $"{Sha(b)} *b.msi\n{new string('1', 64)}  e.zip\n");
        var r = Software.OfflinePackages(d).ToDictionary(p => Path.GetFileName(p.File));
        Assert.True(r["a.exe"].Ok); Assert.True(r["b.msi"].Ok); Assert.Null(r["c.exe"].ExpectedSha); Assert.False(r["e.zip"].Ok); Assert.NotNull(r["e.zip"].ExpectedSha);
    }

    [Fact(DisplayName = "SYS-11-P15/P20: Systemvoraussetzungen blockieren mit Grund; Kompatibilitätshinweise")]
    public void Requirements()
    {
        var blender = Software.Known.First(k => k.Name == "Blender");
        Assert.Empty(Software.Check(blender, 16L << 30, 100L << 30, 22631));
        var why = Software.Check(blender, 4L << 30, 1L << 30, 10240);
        Assert.Equal(3, why.Count); Assert.Contains("Arbeitsspeicher", why[0]);
        Assert.Contains("OpenGL", Software.CompatFor("blender")); Assert.Equal("Keine bekannten Probleme.", Software.CompatFor("unbekannt"));
    }

    [Fact(DisplayName = "SYS-11-P18: Zurücksetzen legt Sicherung an und lässt sich wiederherstellen")]
    public void Reset()
    {
        var d = Tmp(); File.WriteAllText(UserSettings.PathIn(d), "{\"Accent\":\"#123456\"}");
        var b = Software.ResetSettings(d); Assert.False(File.Exists(UserSettings.PathIn(d))); Assert.Contains("#123456", File.ReadAllText(b));
        Software.RestoreSettings(d, b); Assert.Contains("#123456", File.ReadAllText(UserSettings.PathIn(d)));
    }

    [Fact(DisplayName = "SYS-11-P19/P06: Programmnamen zu Zuordnungen; Update-Text")]
    public void Names()
    {
        Assert.Equal("Adobe Acrobat", Software.ProgIdName("AcroExch.Document.DC")); Assert.Equal("Windows-Standard", Software.ProgIdName(null));
        Assert.Equal("Alles aktuell.", Software.UpdateSummary([]));
        Assert.StartsWith("2 Aktualisierungen", Software.UpdateSummary([new("A", "a.a", "1", "2"), new("B", "b.b", "1", "3")]));
    }

    [Fact(DisplayName = "SYS-11-P21: Integrationstest startet Programm und protokolliert Version/Fehler")]
    public void Integration()
    {
        var ok = Software.Test("dotnet", Cli.Find("dotnet") ?? "dotnet"); Assert.True(ok.Ok); Assert.Matches(@"\d+\.\d+", ok.Version);
        var bad = Software.Test("fehlt", "/gibt/es/nicht"); Assert.False(bad.Ok);
        var log = Software.TestLog([ok, bad]); Assert.Contains("OK", log); Assert.Contains("FEHLER", log);
    }

    [Fact(DisplayName = "SYS-11-P22/P23: CSV-Export und winget-Wiederherstellungsliste (Rundreise)")]
    public void Export()
    {
        PackageInfo[] l = [new("Mozilla Firefox", "Mozilla.Firefox", "131.0", Source: "winget"), new("Eigen; App", "ARP\\Machine\\X", "1"), new("7-Zip", "7zip.7zip", "24.08")];
        var csv = Software.ToCsv(l); Assert.StartsWith("\uFEFFName;Kennung", csv); Assert.Contains("\"Eigen; App\"", csv);
        var ids = Software.ImportIds(Software.WingetExport(l)); Assert.Equal(["Mozilla.Firefox", "7zip.7zip"], ids);
    }

    [Fact(DisplayName = "SYS-11-P24/P25: Platz je Werkzeug; Selbsttest-Bericht ohne private Pfade")]
    public void SizesAndReport()
    {
        var d = Tmp(); var t = Path.Combine(d, "tools"); Directory.CreateDirectory(Path.Combine(t, "big")); Directory.CreateDirectory(Path.Combine(t, "small"));
        File.WriteAllBytes(Path.Combine(t, "big", "x"), new byte[5000]); File.WriteAllBytes(Path.Combine(t, "small", "x"), new byte[10]);
        var s = Software.ToolSizes([t, Path.Combine(d, "fehlt")]); Assert.Equal("big", s[0].Name); Assert.Equal(5000, s[0].Bytes);
        Assert.Equal("C:\\Users\\<BENUTZER>\\x <E-MAIL>", Software.Redact("C:\\Users\\max\\x max.muster@web.de"));
        var core = new TrendoCore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".trendo-s11-" + Guid.NewGuid().ToString("N")));
        var rep = Software.SelfTestReport(core, [], core.Version);
        Assert.Contains("Protokoll-Kette unverändert: ja", rep); Assert.DoesNotContain(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), rep);
        Directory.Delete(core.DataRoot, true);
    }
}
