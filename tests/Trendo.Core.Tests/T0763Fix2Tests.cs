using System.Diagnostics;
using System.Runtime.InteropServices;
using Trendo.Core.Sys;
using Xunit;

namespace Trendo.Core.Tests;

/// T-0763 Fix 2 (zweite Prüfrunde): keine Schreibwege über Hardlinks oder Verknüpfungen in Programm- und Datenordner, XML ohne DTD,
/// Verknüpfungen im Programmordner als Hinweis.
public class T0763Fix2Tests
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-t0763g-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool CreateHardLinkW(string newName, string existing, IntPtr security);

    static bool Junction(string link, string target)
    {
        if (!OperatingSystem.IsWindows()) return false;
        using var p = Process.Start(new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{link}\" \"{target}\"") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true })!;
        p.WaitForExit(10000);
        return Directory.Exists(link) && new DirectoryInfo(link).Attributes.HasFlag(FileAttributes.ReparsePoint);
    }

    [Fact(DisplayName = "T-0763 F1: vorab gelegte Zwischendatei als Hardlink nach außen – Reparatur schreibt nicht hinein, Ziel mit zweitem Hardlink wird ersetzt statt überschrieben")]
    public void F1_hardlink_temp_and_target()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = Tmp(); var app = T0763SystemAlltagTests.FakeInstall(root);
        var outside = Path.Combine(Tmp(), "fremd.txt"); File.WriteAllText(outside, "ORIGINAL");
        var target = Path.Combine(app, "Trendo.Core.dll"); File.Delete(target);
        Assert.True(CreateHardLinkW(target + ".trendo-reparatur", outside, IntPtr.Zero), "Hardlink ließ sich nicht anlegen");
        var r = InstallIntegrity.Check(app);
        var (rep, failed) = InstallIntegrity.Repair(app, r.Package!, r.Findings.Select(f => f.File));
        Assert.Contains("Trendo.Core.dll", rep); Assert.Empty(failed);
        Assert.Equal("ORIGINAL", File.ReadAllText(outside)); // die fremde Datei ist unverändert
        Assert.Equal("KERN", File.ReadAllText(target));
        Assert.False(File.Exists(target + ".trendo-reparatur")); // alte Zwischendatei entfernt (nur der Verweis)
        // Zieldatei hat einen zweiten Hardlink nach außen: ersetzen, nicht hineinschreiben
        var outside2 = Path.Combine(Tmp(), "fremd2.txt"); File.WriteAllText(outside2, "ANDERS");
        File.Delete(target); Assert.True(CreateHardLinkW(target, outside2, IntPtr.Zero));
        var r2 = InstallIntegrity.Check(app);
        Assert.Contains(r2.Findings, f => f.File == "Trendo.Core.dll");
        InstallIntegrity.Repair(app, r2.Package!, ["Trendo.Core.dll"]);
        Assert.Equal("ANDERS", File.ReadAllText(outside2)); Assert.Equal("KERN", File.ReadAllText(target));
    }

    [Fact(DisplayName = "T-0763 F1: liegengebliebene *.trendo-reparatur wird als Hinweis gemeldet, nicht ausgeblendet")]
    public void F1_stale_temp_is_reported()
    {
        var root = Tmp(); var app = T0763SystemAlltagTests.FakeInstall(root);
        File.WriteAllText(Path.Combine(app, "Trendo.Core.dll.trendo-reparatur"), "x");
        var a = Checkup.Installation(InstallIntegrity.Check(app));
        Assert.Equal(Checkup.Light.Gelb, a.Light); Assert.Contains("Trendo.Core.dll.trendo-reparatur", a.Finding);
    }

    [Fact(DisplayName = "T-0763 F2: Ordner „quarantaene“ und „logs“ als Junction nach außen – Selbstheilung und Zurückholen schreiben dort nichts hin")]
    public void F2_data_folders_as_junction()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = Tmp(); var audit = new AuditLog(Path.Combine(root, "audit", "audit.jsonl"));
        var h = new SelfHealer(root, audit) { FreeSpace = _ => 100L << 30 };
        h.Run(true, DateTime.Now); // Ordner anlegen
        var outQ = Path.Combine(Tmp(), "q"); var outL = Path.Combine(Tmp(), "l"); Directory.CreateDirectory(outQ); Directory.CreateDirectory(outL);
        Directory.Delete(Path.Combine(root, "logs"), true);
        Assert.True(Junction(Path.Combine(root, "quarantaene"), outQ)); Assert.True(Junction(Path.Combine(root, "logs"), outL));
        File.WriteAllText(Path.Combine(root, "settings.json"), "{kaputt");
        var res = h.Run(true, DateTime.Now.AddMinutes(1));
        Assert.Empty(Directory.EnumerateFileSystemEntries(outQ)); Assert.Empty(Directory.EnumerateFileSystemEntries(outL));
        Assert.True(File.Exists(Path.Combine(root, "settings.json"))); // nicht verschoben
        Assert.Contains(res, x => x.Id == "json" && x.State != HealState.Repaired && x.Detail.Contains("Verknüpfung"));
        // „tmp“ als Junction: alte Dateien draußen werden nicht gelöscht
        var outT = Path.Combine(Tmp(), "t"); Directory.CreateDirectory(outT); var old = Path.Combine(outT, "alt.bin"); File.WriteAllText(old, "x"); File.SetLastWriteTime(old, DateTime.Now.AddDays(-30));
        Directory.Delete(Path.Combine(root, "tmp"), true); Assert.True(Junction(Path.Combine(root, "tmp"), outT));
        h.Run(true, DateTime.Now.AddMinutes(2));
        Assert.True(File.Exists(old));
    }

    [Fact(DisplayName = "T-0763 F2: Zurückholen schreibt die Listen nicht über eine Junction „logs“ nach außen")]
    public void F2_restore_logs_junction()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = Tmp(); var audit = new AuditLog(Path.Combine(root, "audit", "audit.jsonl"));
        File.WriteAllText(Path.Combine(root, "settings.json"), "{kaputt");
        new SelfHealer(root, audit) { FreeSpace = _ => 100L << 30 }.Run(true, DateTime.Now);
        var item = SelfHealer.Quarantined(root).Single();
        var outL = Path.Combine(Tmp(), "l"); Directory.CreateDirectory(outL);
        Directory.Delete(Path.Combine(root, "logs"), true); Assert.True(Junction(Path.Combine(root, "logs"), outL));
        Assert.Throws<InvalidOperationException>(() => SelfHealer.Restore(root, item, audit, DateTime.Now));
        Assert.Empty(Directory.EnumerateFileSystemEntries(outL)); Assert.True(File.Exists(item.File));
    }

    [Fact(DisplayName = "T-0763 F3: sq.version und .nuspec mit DOCTYPE werden abgelehnt (keine DTD, keine externen Verweise)")]
    public void F3_no_dtd()
    {
        var root = Tmp(); var app = T0763SystemAlltagTests.FakeInstall(root);
        var outside = Path.Combine(Tmp(), "geheim.txt"); File.WriteAllText(outside, "9.9.9");
        File.WriteAllText(Path.Combine(app, "sq.version"), $"<?xml version=\"1.0\"?><!DOCTYPE p [<!ENTITY x SYSTEM \"file:///{outside.Replace('\\', '/')}\">]><package><metadata><id>TrendoOS</id><version>&x;</version></metadata></package>");
        Assert.Null(InstallIntegrity.InstalledVersion(app));
        var r = InstallIntegrity.Check(app);
        Assert.Null(r.Package); Assert.Contains("sq.version", r.Note);
        // .nuspec mit DOCTYPE
        var root2 = Tmp(); var app2 = T0763SystemAlltagTests.FakeInstall(root2);
        var pkg = Directory.GetFiles(Path.Combine(root2, "packages"))[0];
        using (var z = System.IO.Compression.ZipFile.Open(pkg, System.IO.Compression.ZipArchiveMode.Update))
        {
            z.GetEntry("TrendoOS.nuspec")!.Delete();
            using var s = z.CreateEntry("TrendoOS.nuspec").Open();
            s.Write("<?xml version=\"1.0\"?><!DOCTYPE p [<!ENTITY a \"TrendoOS\">]><package><metadata><id>&a;</id><version>1.2.3</version></metadata></package>"u8);
        }
        Assert.Null(InstallIntegrity.Check(app2).Package);
    }

    [Fact(DisplayName = "T-0763 F4: Verknüpfung (Junction) im Programmordner ist ein eigener gelber Hinweis")]
    public void F4_links_in_app_folder()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = Tmp(); var app = T0763SystemAlltagTests.FakeInstall(root);
        Assert.True(Junction(Path.Combine(app, "plugins"), Tmp()));
        var a = Checkup.Installation(InstallIntegrity.Check(app));
        Assert.Equal(Checkup.Light.Gelb, a.Light); Assert.Contains("Verknüpfung", a.Finding); Assert.Contains("plugins", a.Finding);
    }
}
