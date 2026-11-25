using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using Trendo.Core.Sys;
using Xunit;

namespace Trendo.Core.Tests;

/// T-0763 Fix 1 (Gegenprüfung): Paket genau zuordnen, keine Schreibwege über Verknüpfungen, ehrliche Grenzen, eindeutige Sicherungen, Prozess per Startzeit.
public class T0763Fix1Tests
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-t0763f-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

    /// <summary>Echte Verzeichnis-Verknüpfung (Junction) – braucht unter Windows keine Administratorrechte.</summary>
    static bool Junction(string link, string target)
    {
        if (!OperatingSystem.IsWindows()) return false;
        using var p = Process.Start(new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{link}\" \"{target}\"") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true })!;
        p.WaitForExit(10000);
        return Directory.Exists(link) && new DirectoryInfo(link).Attributes.HasFlag(FileAttributes.ReparsePoint);
    }

    static void AddPackage(string root, string file, string id, string ver, Dictionary<string, string> files)
    {
        using var z = ZipFile.Open(Path.Combine(root, "packages", file), ZipArchiveMode.Create);
        using (var n = z.CreateEntry(id + ".nuspec").Open()) n.Write(Encoding.UTF8.GetBytes($"<?xml version=\"1.0\"?><package><metadata><id>{id}</id><version>{ver}</version></metadata></package>"));
        foreach (var (k, v) in files) { using var s = z.CreateEntry("lib/app/" + k).Open(); s.Write(Encoding.UTF8.GetBytes(v)); }
    }

    [Fact(DisplayName = "T-0763 M1: nur das Paket „{Kennung}-{Version}-full.nupkg“ mit passender .nuspec – fremdes Paket gleicher Version wird nie zur Reparatur benutzt")]
    public void M1_exact_package()
    {
        var root = Tmp(); var app = T0763SystemAlltagTests.FakeInstall(root);
        Assert.NotNull(InstallIntegrity.FindPackage(app, "TrendoOS", "1.2.3"));
        var own = Directory.GetFiles(Path.Combine(root, "packages"))[0]; File.Delete(own);
        AddPackage(root, "Fremd-1.2.3-full.nupkg", "Fremd", "1.2.3", new() { ["Trendo.Core.dll"] = "BOESE" });
        var r = InstallIntegrity.Check(app);
        Assert.Null(r.Package); Assert.Contains("Kein passendes Installationspaket (TrendoOS 1.2.3)", r.Note);
        // richtiger Dateiname, aber .nuspec nennt eine andere Kennung → ebenfalls kein Paket
        AddPackage(root, "TrendoOS-1.2.3-full.nupkg", "Fremd", "1.2.3", new() { ["Trendo.Core.dll"] = "BOESE" });
        Assert.Null(InstallIntegrity.Check(app).Package);
        File.Delete(Path.Combine(root, "packages", "TrendoOS-1.2.3-full.nupkg"));
        AddPackage(root, "TrendoOS-1.2.3-full.nupkg", "TrendoOS", "9.9.9", new() { ["Trendo.Core.dll"] = "BOESE" });
        Assert.Null(InstallIntegrity.Check(app).Package);
    }

    [Fact(DisplayName = "T-0763 M2: Reparatur schreibt nie über eine Junction im Programmordner nach außen (echte Junction)")]
    public void M2_no_write_through_junction()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = Tmp(); var app = T0763SystemAlltagTests.FakeInstall(root);
        var outside = Path.Combine(Tmp(), "draussen"); Directory.CreateDirectory(outside);
        Directory.Delete(Path.Combine(app, "Assets"), true);
        Assert.True(Junction(Path.Combine(app, "Assets"), outside), "Junction ließ sich nicht anlegen");
        Assert.NotNull(PathGuard.LinkOnWay(app, Path.Combine(app, "Assets", "logo.png")));
        var r = InstallIntegrity.Check(app);
        Assert.Contains(r.Findings, f => f.File == "Assets/logo.png");
        var (rep, failed) = InstallIntegrity.Repair(app, r.Package!, r.Findings.Select(f => f.File));
        Assert.Contains("Assets/logo.png", failed); Assert.DoesNotContain("Assets/logo.png", rep);
        Assert.Empty(Directory.GetFiles(outside)); // draußen ist nichts angekommen
        Assert.Null(PathGuard.LinkOnWay(app, Path.Combine(app, "Trendo.Core.dll")));
    }

    [Fact(DisplayName = "T-0763 M3: grüner Befund nennt die Grenze (nicht code-signiert); Datei, die nicht aus dem Paket stammt, ist ein gelber Hinweis")]
    public void M3_limit_and_extra_files()
    {
        var root = Tmp(); var app = T0763SystemAlltagTests.FakeInstall(root);
        var ok = InstallIntegrity.Check(app);
        Assert.Empty(ok.Extra); // sq.version ist Velopack-eigen
        var g = Checkup.Installation(ok);
        Assert.Equal(Checkup.Light.Gruen, g.Light); Assert.Contains("nicht code-signiert", g.Finding); Assert.Contains("schützt nicht vor gezielter Manipulation", g.Finding);
        File.WriteAllText(Path.Combine(app, "Zusatz.dll"), "x");
        var r = InstallIntegrity.Check(app);
        Assert.True(r.Ok); Assert.Equal(["Zusatz.dll"], r.Extra);
        var a = Checkup.Installation(r);
        Assert.Equal(Checkup.Light.Gelb, a.Light); Assert.Contains("nicht aus dem Paket", a.Finding); Assert.Contains("Zusatz.dll", a.Finding);
    }

    [Fact(DisplayName = "T-0763 N1: reiner Prüflauf schreibt keine „letzte Prüfung“-Datei, nur den Protokolleintrag")]
    public void N1_check_only_writes_no_files()
    {
        var root = Tmp(); var audit = new AuditLog(Path.Combine(root, "audit", "audit.jsonl"));
        foreach (var f in SelfHealer.Folders) Directory.CreateDirectory(Path.Combine(root, f));
        File.WriteAllText(Path.Combine(root, "kaputt.json"), "{x");
        new SelfHealer(root, audit) { FreeSpace = _ => 100L << 30 }.Run(false, DateTime.Now);
        Assert.False(File.Exists(Path.Combine(root, "logs", "selbstheilung-letzte.json")));
        Assert.True(File.Exists(Path.Combine(root, "kaputt.json")));
        Assert.Contains(audit.ReadAll(), e => e.Action == "heal.check");
        new SelfHealer(root, audit) { FreeSpace = _ => 100L << 30 }.Run(true, DateTime.Now);
        Assert.True(File.Exists(Path.Combine(root, "logs", "selbstheilung-letzte.json")));
    }

    [Fact(DisplayName = "T-0763 N3: zweimal Zurückholen in derselben Sekunde überschreibt keine Sicherung (.bak eindeutig)")]
    public void N3_unique_backup_names()
    {
        var root = Tmp(); var audit = new AuditLog(Path.Combine(root, "audit", "audit.jsonl"));
        var now = new DateTime(2026, 11, 24, 10, 0, 0);
        var target = Path.Combine(root, "settings.json");
        var h = new SelfHealer(root, audit) { FreeSpace = _ => 100L << 30 };
        File.WriteAllText(target, "{ kaputt 1"); h.Run(true, now);
        File.WriteAllText(target, "{ kaputt 2"); h.Run(true, now.AddSeconds(1));
        var items = SelfHealer.Quarantined(root); Assert.Equal(2, items.Count);
        File.WriteAllText(target, "{\"nutzer\":1}");
        SelfHealer.Restore(root, items[0], audit, now);
        File.WriteAllText(target, "{\"nutzer\":2}"); // dazwischen neu gespeichert
        SelfHealer.Restore(root, items[1], audit, now); // gleiche Sekunde
        var baks = Directory.GetFiles(root, "settings.json.vor-rueckholung-*.bak").Select(File.ReadAllText).OrderBy(x => x).ToList();
        Assert.Equal(2, baks.Count); // beide Nutzerdateien als eigene Sicherung (zweite mit „-2“) – nichts überschrieben
        Assert.Single(Directory.GetFiles(root, "settings.json.vor-rueckholung-*-2.bak"));
        Assert.Contains("{\"nutzer\":1}", baks); Assert.Contains("{\"nutzer\":2}", baks);
    }

    [Fact(DisplayName = "T-0763 N4: Zurückholen legt nichts über eine Junction im Datenordner nach außen (echte Junction)")]
    public void N4_restore_not_through_junction()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = Tmp(); var audit = new AuditLog(Path.Combine(root, "audit", "audit.jsonl"));
        Directory.CreateDirectory(Path.Combine(root, "ki")); File.WriteAllText(Path.Combine(root, "ki", "m.json"), "{x");
        new SelfHealer(root, audit) { FreeSpace = _ => 100L << 30 }.Run(true, DateTime.Now);
        var item = SelfHealer.Quarantined(root).Single();
        Directory.Delete(Path.Combine(root, "ki"), true);
        var outside = Path.Combine(Tmp(), "draussen"); Directory.CreateDirectory(outside);
        Assert.True(Junction(Path.Combine(root, "ki"), outside));
        var ex = Assert.Throws<InvalidOperationException>(() => SelfHealer.Restore(root, item, audit, DateTime.Now));
        Assert.Contains("Verknüpfung", ex.Message);
        Assert.Empty(Directory.GetFiles(outside)); Assert.True(File.Exists(item.File));
    }

    [Fact(DisplayName = "T-0763 N5: Beenden nur bei gleicher Startzeit und gleichem Pfad; Junction aus dem Werkzeugordner auf ein fremdes Programm ist nicht „eigen“")]
    public void N5_same_process_and_junction()
    {
        var own = Tmp(); var exe = Path.Combine(own, "werkzeug.exe"); File.WriteAllText(exe, "x");
        var start = new DateTime(2026, 11, 24, 9, 0, 0);
        var row = new TaskManager.Proc(7, "werkzeug", 0, 1, exe, true, start);
        Assert.True(TaskManager.SameProcess(row, exe, start, [own]));
        Assert.False(TaskManager.SameProcess(row, exe, start.AddSeconds(3), [own])); // Kennung wiederverwendet
        Assert.False(TaskManager.SameProcess(row, @"C:\Windows\System32\ping.exe", start, [own]));
        if (!OperatingSystem.IsWindows()) return;
        var link = Path.Combine(own, "verknuepft");
        Assert.True(Junction(link, Environment.SystemDirectory));
        var foreign = Path.Combine(link, "PING.EXE");
        Assert.True(File.Exists(foreign));
        Assert.False(TaskManager.IsOwn(foreign, [own]));
        Assert.True(TaskManager.IsOwn(exe, [own]));
        Directory.Delete(link); // nur die Verknüpfung, nicht System32
    }
}
