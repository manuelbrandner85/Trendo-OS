using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using Trendo.Core.Sys;
using Xunit;

namespace Trendo.Core.Tests;

/// T-0763: SYS-13 „Alles prüfen“ (Ampel je Bereich), X-02 Installation gegen das Paket prüfen und ersetzen, Quarantäne zurückholen,
/// X-09 Grafiklast je Programm und Lastprobe gegen die Windows-Leistungsindikatoren.
/// <remarks>T-0771: nicht parallel zu anderen Tests. Die Lastprobe X-09 vergleicht die Prozessorlast DIESES Testprozesses (Trendo gegen die
/// Windows-Leistungsindikatoren). Liefen andere Testklassen im selben Prozess mit, schwankte dessen Last von Sekunde zu Sekunde; die beiden Messungen
/// lesen nicht im selben Augenblick und wichen im Volltest um mehr als 5 Punkte ab (46,4 % gegen 39,6 %). Allein gemessen ist die Last des
/// Testprozesses die, die der Test selbst erzeugt. Grenze (5 Punkte) und Messung unverändert.</remarks>
[Collection("T0763 Lastprobe")]
public class T0763SystemAlltagTests
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-t0763-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

    /// <summary>Nachbau des Velopack-Aufbaus: …\current (Programmordner mit sq.version) und …\packages\App-&lt;ver&gt;-full.nupkg (lib/app/…).</summary>
    public static string FakeInstall(string root, string ver = "1.2.3")
    {
        var app = Path.Combine(root, "current"); Directory.CreateDirectory(Path.Combine(app, "Assets"));
        var files = new Dictionary<string, string> { ["TrendoOS.exe"] = "PROGRAMM-" + new string('x', 5000), ["Trendo.Core.dll"] = "KERN", ["Assets/logo.png"] = "BILD" };
        foreach (var (k, v) in files) File.WriteAllText(Path.Combine(app, k.Replace('/', Path.DirectorySeparatorChar)), v);
        File.WriteAllText(Path.Combine(app, "sq.version"), $"<?xml version=\"1.0\"?><package><metadata><id>TrendoOS</id><version>{ver}</version></metadata></package>");
        Directory.CreateDirectory(Path.Combine(root, "packages"));
        using (var z = ZipFile.Open(Path.Combine(root, "packages", $"TrendoOS-{ver}-full.nupkg"), ZipArchiveMode.Create))
        {
            using (var n = z.CreateEntry("TrendoOS.nuspec").Open()) n.Write(Encoding.UTF8.GetBytes($"<?xml version=\"1.0\"?><package><metadata><id>TrendoOS</id><version>{ver}</version></metadata></package>"));
            foreach (var (k, v) in files) { using var s = z.CreateEntry("lib/app/" + k).Open(); s.Write(Encoding.UTF8.GetBytes(v)); }
        }
        return app;
    }

    [Fact(DisplayName = "X-02 (T-0763): gelöschte und veränderte Programmdatei werden gegen das Installationspaket erkannt und daraus ersetzt")]
    public void Install_check_and_repair()
    {
        var root = Tmp(); var app = FakeInstall(root);
        var ok = InstallIntegrity.Check(app);
        Assert.True(ok.Ok, ok.Note); Assert.Equal(3, ok.Checked); Assert.Equal("1.2.3", ok.Version);
        File.Delete(Path.Combine(app, "Trendo.Core.dll"));
        File.WriteAllText(Path.Combine(app, "Assets", "logo.png"), "BILX"); // gleiche Größe, anderer Inhalt → nur die Prüfsumme merkt es
        var bad = InstallIntegrity.Check(app);
        Assert.False(bad.Ok);
        Assert.Equal(["Assets/logo.png:verändert", "Trendo.Core.dll:fehlt"], bad.Findings.Select(f => $"{f.File}:{f.Problem}").OrderBy(x => x, StringComparer.Ordinal));
        var audit = new AuditLog(Path.Combine(root, "audit.jsonl"));
        var (rep, failed) = InstallIntegrity.Repair(app, bad.Package!, bad.Findings.Select(f => f.File), audit);
        Assert.Equal(2, rep.Count); Assert.Empty(failed);
        Assert.True(InstallIntegrity.Check(app).Ok);
        Assert.Equal("KERN", File.ReadAllText(Path.Combine(app, "Trendo.Core.dll")));
        Assert.Contains(audit.ReadAll(), e => e.Action == "installation.reparatur");
    }

    [Fact(DisplayName = "X-02 (T-0763): ohne Installation ehrlich „nur installierte Version“, ohne Paket „Setup erneut ausführen“; Pfade aus dem Paket bleiben im Programmordner")]
    public void Install_honest_limits()
    {
        var dev = InstallIntegrity.Check(Tmp());
        Assert.False(dev.Installed); Assert.Contains("ohne Installation", dev.Note);
        Assert.Equal(Checkup.Light.Gruen, Checkup.Installation(dev).Light); Assert.Contains("Nur für Entwickler", Checkup.Installation(dev).Finding);
        var root = Tmp(); var app = FakeInstall(root); File.Delete(Directory.GetFiles(Path.Combine(root, "packages"))[0]);
        var nopkg = InstallIntegrity.Check(app);
        Assert.True(nopkg.Installed); Assert.Null(nopkg.Package); Assert.Contains("Setup erneut", nopkg.Note);
        Assert.Equal(Checkup.Light.Gelb, Checkup.Installation(nopkg).Light);
        // Eintrag mit „../“ im Paket wird nie außerhalb des Programmordners geschrieben
        var root2 = Tmp(); var app2 = FakeInstall(root2);
        var pkg = Directory.GetFiles(Path.Combine(root2, "packages"))[0];
        using (var z = ZipFile.Open(pkg, ZipArchiveMode.Update)) { using var s = z.CreateEntry("lib/app/../boese.txt").Open(); s.Write("x"u8); }
        var (rep, failed) = InstallIntegrity.Repair(app2, pkg, ["../boese.txt"]);
        Assert.Empty(rep); Assert.Single(failed); Assert.False(File.Exists(Path.Combine(root2, "boese.txt")));
    }

    [Fact(DisplayName = "X-02 (T-0763): beiseitegelegte Datei zurückholen – an den alten Ort, bisherige Datei bleibt als .bak, wird nicht erneut beiseitegelegt; alte Quarantäne ohne Herkunft ehrlich")]
    public void Quarantine_restore()
    {
        var root = Tmp(); var audit = new AuditLog(Path.Combine(root, "audit", "audit.jsonl"));
        Directory.CreateDirectory(Path.Combine(root, "ki"));
        var broken = Path.Combine(root, "ki", "modell.json"); File.WriteAllText(broken, "{ kaputt");
        var now = new DateTime(2026, 11, 24, 10, 0, 0);
        var h = new SelfHealer(root, audit) { FreeSpace = _ => 100L << 30 };
        Assert.Equal(HealState.Repaired, h.Run(true, now).Single(r => r.Id == "json").State);
        Assert.False(File.Exists(broken));
        var q = Assert.Single(SelfHealer.Quarantined(root));
        Assert.Equal("ki/modell.json", q.Original); Assert.Equal("20261124-100000", q.Stamp);
        File.WriteAllText(broken, "{\"neu\":1}"); // inzwischen hat Trendo Standardwerte angelegt
        var msg = SelfHealer.Restore(root, q, audit, now.AddMinutes(5));
        Assert.Contains("wieder an ihrem Platz", msg);
        Assert.Equal("{ kaputt", File.ReadAllText(broken));
        Assert.Equal("{\"neu\":1}", File.ReadAllText(Assert.Single(Directory.GetFiles(Path.Combine(root, "ki"), "modell.json.vor-rueckholung-*.bak"))));
        Assert.Empty(SelfHealer.Quarantined(root));
        Assert.Contains(audit.ReadAll(), e => e.Action == "heal.restore");
        // nächste Prüfung: bleibt liegen, aber ehrlich als „braucht dich“
        var again = h.Run(true, now.AddMinutes(6)).Single(r => r.Id == "json");
        // Fix N2: Hinweis statt dauerhaft Gelb, mit passendem nächsten Schritt
        Assert.Equal(HealState.Ok, again.State); Assert.Contains(SelfHealer.KeptPrefix, again.Detail); Assert.True(File.Exists(broken));
        var data = Checkup.Data(h.Run(false, now.AddMinutes(6)));
        Assert.Equal(Checkup.Light.Gruen, data.Light); Assert.Contains("modell.json", data.Finding); Assert.Contains("Texteditor", data.Next);
        // verändert sich die Datei (immer noch kaputt), wird sie wieder beiseitegelegt
        File.WriteAllText(broken, "{ noch kaputter");
        Assert.Equal(HealState.Repaired, h.Run(true, now.AddMinutes(7)).Single(r => r.Id == "json").State);
        // alter Quarantäne-Ordner (vor T-0763) ohne herkunft.json: Datei direkt im Datenordner bekannt, mit „_“ unbekannt
        var old = Path.Combine(root, "quarantaene", "20250101-000000"); Directory.CreateDirectory(old);
        File.WriteAllText(Path.Combine(old, "settings.json"), "{x"); File.WriteAllText(Path.Combine(old, "ki_alt.json"), "{x");
        var legacy = SelfHealer.Quarantined(root).Where(i => i.Stamp == "20250101-000000").ToDictionary(i => Path.GetFileName(i.File));
        Assert.Equal("settings.json", legacy["settings.json"].Original); Assert.Null(legacy["ki_alt.json"].Original);
        Assert.Throws<InvalidOperationException>(() => SelfHealer.Restore(root, legacy["ki_alt.json"], audit, now));
        Assert.Throws<InvalidOperationException>(() => SelfHealer.Restore(root, new("x", Path.Combine(old, "settings.json"), "../../ausserhalb.json"), audit, now));
    }

    [Fact(DisplayName = "SYS-13 (T-0763): veränderte Protokollzeile wird mit Stelle gemeldet – Ampel rot im Bereich Protokoll")]
    public void Audit_line_changed_reported_with_position()
    {
        var root = Tmp(); var f = Path.Combine(root, "audit.jsonl"); var audit = new AuditLog(f);
        for (var i = 1; i <= 4; i++) audit.Append("test", "eintrag", "Zeile " + i);
        Assert.Equal(Checkup.Light.Gruen, Checkup.Audit(audit.Check().Broken, audit.ReadAll().Count).Light);
        var lines = File.ReadAllLines(f); lines[2] = lines[2].Replace("Zeile 3", "Zeile 9"); File.WriteAllLines(f, lines);
        var a = Checkup.Audit(new AuditLog(f).Check().Broken, 4);
        Assert.Equal(Checkup.Light.Rot, a.Light); Assert.Contains("Eintrag 3", a.Finding);
        // gelöschte Zeile: ab dort unterbrochen
        File.WriteAllLines(f, File.ReadAllLines(f).Where((_, i) => i != 1).Select(l => l.Replace("Zeile 9", "Zeile 3")));
        Assert.Contains("Eintrag 2", Checkup.Audit(new AuditLog(f).Check().Broken, 3).Finding);
    }

    [Fact(DisplayName = "SYS-13 (T-0763): Ampel je Bereich mit nächstem Schritt; Gesamtsatz; rot vor gelb vor grün")]
    public void Checkup_lights()
    {
        var now = new DateTime(2026, 11, 24);
        Assert.Equal(Checkup.Light.Gelb, Checkup.Backup(null, true, now).Light);
        Assert.Equal(Checkup.Light.Gelb, Checkup.Backup(now.AddDays(-9), true, now).Light);
        Assert.Equal(Checkup.Light.Gelb, Checkup.Backup(now.AddDays(-1), false, now).Light);
        Assert.Equal(Checkup.Light.Gruen, Checkup.Backup(now.AddDays(-1), true, now).Light);
        Assert.Equal(Checkup.Light.Rot, Checkup.Disk(1L << 30).Light); Assert.Equal(Checkup.Light.Gelb, Checkup.Disk(5L << 30).Light); Assert.Equal(Checkup.Light.Gruen, Checkup.Disk(50L << 30).Light);
        Assert.Equal(Checkup.Light.Gelb, Checkup.Programs(3, ["FFmpeg"], []).Light); Assert.Equal("heilung", Checkup.Programs(3, ["FFmpeg"], []).Page);
        Assert.Equal(Checkup.Light.Gruen, Checkup.Programs(0, [], []).Light);
        Assert.Equal(Checkup.Light.Gelb, Checkup.Crashes([now.AddDays(-2)], now).Light); Assert.Equal(Checkup.Light.Gruen, Checkup.Crashes([now.AddDays(-20)], now).Light);
        var data = Checkup.Data([new("folders", "Ordner", HealState.Ok, ""), new("json", "Einstellungsdateien", HealState.NeedsYou, "1 Datei beschädigt."), new("audit", "Protokoll", HealState.NeedsYou, "x")]);
        Assert.Equal(Checkup.Light.Gelb, data.Light); Assert.DoesNotContain("Protokoll", data.Finding);
        var areas = new[] { Checkup.Disk(50L << 30), Checkup.Programs(3, ["FFmpeg"], []), Checkup.Audit(7, 9) };
        Assert.Equal(Checkup.Light.Rot, Checkup.Overall(areas));
        Assert.Equal("1 Bereich braucht jetzt deine Hilfe, 1 weiterer einen Blick.", Checkup.Sentence(areas));
        Assert.Equal("Alles in Ordnung: 1 von 1 Bereichen grün.", Checkup.Sentence([Checkup.Disk(50L << 30)]));
        Assert.All(areas.Where(a => a.Light != Checkup.Light.Gruen), a => Assert.False(string.IsNullOrWhiteSpace(a.Next)));
        Assert.Contains("[rot] Protokoll: Eintrag 7", Checkup.Text(areas, now));
    }

    [Fact(DisplayName = "SYS-13 (T-0763): Telemetrie-Prüfung der installierten Version über die Paketlisten – findet Analyse-Bibliotheken, Trendo selbst ist frei")]
    public void Telemetry_in_installed_folder()
    {
        var d = Tmp();
        Assert.Equal(0, Quality.DepsFiles(d));
        File.WriteAllText(Path.Combine(d, "App.deps.json"), "{\"libraries\":{\"Sentry/4.0.0\":{}}}");
        Assert.Contains("App.deps.json: Sentry", Quality.TelemetryHitsInstalled(d));
        // echte Paketliste dieses Testlaufs (enthält Trendo.Core und alle Abhängigkeiten)
        Assert.True(Quality.DepsFiles(AppContext.BaseDirectory) > 0);
        Assert.Empty(Quality.TelemetryHitsInstalled(AppContext.BaseDirectory));
    }

    [Fact(DisplayName = "X-09 (T-0763): Grafiklast je Programm aus „GPU Engine“-Instanzen – je Prozess die höchste Grafik-Einheit, wie im Windows-Task-Manager")]
    public void Gpu_per_process()
    {
        var g = GpuLoad.PerProcess([
            ("pid_100_luid_0x00000000_0x0000F72D_phys_0_eng_0_engtype_3D", 30), ("pid_100_luid_0x00000000_0x0000F72D_phys_0_eng_0_engtype_3D", 15),
            ("pid_100_luid_0x00000000_0x0000F72D_phys_0_eng_3_engtype_VideoDecode", 20), ("pid_200_luid_0x00000000_0x0000F72D_phys_0_eng_0_engtype_3D", 250),
            ("_Total", 99), ("pid_x_kaputt", 5)]);
        Assert.Equal(45, g[100]); Assert.Equal(100, g[200]); Assert.Equal(2, g.Count);
        Assert.Contains("Administratorrechte", GpuLoad.NetNote);
    }

    [Fact(DisplayName = "X-09 (T-0763, echt): Lastprobe – Prozessorlast eines ausgelasteten Prozesses weicht höchstens 5 Prozentpunkte von den Windows-Leistungsindikatoren ab")]
    public void Load_probe_against_performance_counters()
    {
        if (!OperatingSystem.IsWindows()) return;
        var cores = Environment.ProcessorCount;
        // Last: eine PowerShell, die einen Kern voll auslastet, bis dieser Test sie beendet (T-0768: ohne feste Laufzeit – unter Last dauern die drei Messfenster länger)
        var psi = new ProcessStartInfo("powershell.exe", "-NoProfile -NonInteractive -Command \"while($true){}\"") { UseShellExecute = false, CreateNoWindow = true };
        using var load = Process.Start(psi)!;
        try
        {
            using var cpu = new PdhArray(@"\Process(*)\% Processor Time", @"\Process(*)\ID Process"); // beide Zähler aus einer Messung
            Assert.True(cpu.Available, "Leistungsindikator \\Process(*)\\% Processor Time fehlt");
            // zweite, schwerere Last: halb so viele Rechenfäden wie Kerne in diesem Testprozess
            var stop = false; var me = Environment.ProcessId; var threads = Math.Max(1, cores / 2);
            var busy = Enumerable.Range(0, threads).Select(_ => new Thread(() => { var x = 0L; while (!Volatile.Read(ref stop)) x++; }) { IsBackground = true }).ToList();
            busy.ForEach(t => t.Start());
            Thread.Sleep(1500);
            // T-0768: drei Messfenster, gewertet wird das mittlere (Median) – ein einzelnes Fenster kippt, wenn parallel andere Läufe die Kerne
            // belegen und die beiden Messungen (Trendo, PDH) nicht im selben Augenblick lesen. Die Grenze von 5 Prozentpunkten bleibt.
            var probes = new List<(double Own, double Counter, double Diff, double Own2, double Counter2, double Diff2, double Secs, string Inst, string Inst2)>();
            for (var round = 0; round < 3; round++)
            {
                cpu.ReadPair(); // Bezug
                var s0 = TaskManager.Measure(); var t0 = Stopwatch.GetTimestamp();
                Thread.Sleep(4000);
                var pair = cpu.ReadPair();
                var s1 = TaskManager.Measure(); var secsR = Stopwatch.GetElapsedTime(t0).TotalSeconds;
                var rows = TaskManager.Rows(s0, s1, secsR, cores, []);
                // PDH liefert gleichnamige Instanzen (mehrere „powershell“) ohne Nummer – Zuordnung über die Kennung aus derselben Messung
                (string Instance, double Value) Pick(int pid) { var m = Assert.Single(pair!, x => (int)x.Second == pid); return (m.Instance, m.Value); }
                var ownR = rows.Single(r => r.Pid == load.Id).CpuPercent; var (instR, cvalR) = Pick(load.Id);
                var own2R = rows.Single(r => r.Pid == me).CpuPercent; var (inst2R, cval2R) = Pick(me);
                probes.Add((ownR, cvalR / cores, Math.Abs(ownR - cvalR / cores), own2R, cval2R / cores, Math.Abs(own2R - cval2R / cores), secsR, instR, inst2R));
            }
            Volatile.Write(ref stop, true); busy.ForEach(t => t.Join());
            var (own, counter, diff, own2, counter2, diff2, secs, inst, inst2) = probes.OrderBy(x => x.Diff + x.Diff2).ElementAt(1);
            var log = $"Lastprobe T-0763 ({DateTime.Now:yyyy-MM-dd HH:mm:ss})\nKerne: {cores}\nLastprozess: powershell (Kennung {load.Id}), Endlosschleife auf einem Kern\nMessfenster Trendo: {secs:0.00} s\n" +
                $"Trendo (TaskManager.Rows, Prozessorzeit/Zeit/Kerne): {own:0.00} %\nWindows-Leistungsindikator \\Process({inst})\\% Processor Time / Kerne: {counter:0.00} %\nErwartet (ein Kern voll): {100.0 / cores:0.00} %\nAbweichung: {diff:0.00} Prozentpunkte\n" +
                $"\nZweite Last: Testprozess (Kennung {me}) mit {threads} Rechenfäden\nTrendo: {own2:0.00} %\nWindows-Leistungsindikator \\Process({inst2})\\% Processor Time / Kerne: {counter2:0.00} %\nErwartet (etwa): {100.0 * threads / cores:0.00} %\nAbweichung: {diff2:0.00} Prozentpunkte\n" +
                $"\nMessfenster je 4 s, Abweichung Last/Testprozess: " + string.Join(" | ", probes.Select(x => $"{x.Diff:0.00}/{x.Diff2:0.00}")) + " (gewertet: das mittlere)" +
                $"\nGrenze: 5 Prozentpunkte\nErgebnis: {(diff <= 5 && diff2 <= 5 ? "BESTANDEN" : "NICHT BESTANDEN")}\n";
            if (Environment.GetEnvironmentVariable("TRENDO_BELEG_DIR") is { Length: > 0 } beleg) { Directory.CreateDirectory(beleg); File.WriteAllText(Path.Combine(beleg, "lastprobe.txt"), log); }
            Assert.True(own > 100.0 / cores / 2, log); // die Last ist wirklich gemessen
            Assert.True(diff <= 5, log); Assert.True(diff2 <= 5, log);
            Assert.True(own2 > 100.0 * threads / cores / 2, log);
        }
        finally { try { load.Kill(); } catch (InvalidOperationException) { } }
    }

    [Fact(DisplayName = "X-02 (T-0763, echt): installiertes Trendo dieses PCs gegen sein Paket prüfen; auf einer Kopie Datei löschen, verändern und ersetzen")]
    public void Real_installation_copy()
    {
        var real = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TrendoOS");
        var app = Path.Combine(real, "current");
        if (InstallIntegrity.Installed(app) is not var (id, ver) || InstallIntegrity.FindPackage(app, id, ver) is not { } pkg) { Ueberspringen.Weil("kein installiertes Trendo mit Paket auf diesem PC."); return; } // kein installiertes Trendo: nichts zu prüfen
        var sw = Stopwatch.StartNew();
        var r = InstallIntegrity.Check(app); // nur lesen – an der echten Installation ändert der Test nichts
        Assert.Empty(r.Extra); Assert.Empty(r.Links); // Fix M3/F4: die echte Installation darf nicht fälschlich „nicht aus dem Paket“ oder „Verknüpfung“ melden
        Assert.Equal(Checkup.Light.Gruen, Checkup.Installation(r).Light);
        var checkSecs = sw.Elapsed.TotalSeconds;
        var root = Tmp();
        try
        {
            var copy = Path.Combine(root, "current"); Directory.CreateDirectory(Path.Combine(root, "packages"));
            foreach (var f in Directory.EnumerateFiles(app, "*", SearchOption.AllDirectories))
            { var t = Path.Combine(copy, Path.GetRelativePath(app, f)); Directory.CreateDirectory(Path.GetDirectoryName(t)!); File.Copy(f, t); }
            File.Copy(pkg, Path.Combine(root, "packages", Path.GetFileName(pkg)));
            var before = InstallIntegrity.Check(copy);
            File.Delete(Path.Combine(copy, "Trendo.Core.dll"));
            var json = Directory.GetFiles(copy, "*.deps.json")[0]; var bytes = File.ReadAllBytes(json); bytes[^2] ^= 0x01; File.WriteAllBytes(json, bytes);
            var bad = InstallIntegrity.Check(copy);
            var (rep, failed) = InstallIntegrity.Repair(copy, bad.Package!, bad.Findings.Select(f => f.File));
            var after = InstallIntegrity.Check(copy);
            var log = $"Echte Installation (Velopack) – T-0763 ({DateTime.Now:yyyy-MM-dd HH:mm:ss})\nVersion: {ver}\nPaket: {Path.GetFileName(pkg)} ({new FileInfo(pkg).Length:N0} Bytes)\n" +
                $"Prüfung der echten Installation (nur lesen): {r.Checked} Dateien, {r.Findings.Count} Abweichungen, {checkSecs:0.0} s – {r.Note}\n" +
                (r.Findings.Count > 0 ? "  " + string.Join("\n  ", r.Findings.Take(20).Select(f => $"{f.File}: {f.Problem}")) + "\n" : "") +
                $"Kopie vorher: {before.Findings.Count} Abweichungen\nKopie nach Löschen von Trendo.Core.dll und Ändern eines Bytes in {Path.GetFileName(json)}: {string.Join(", ", bad.Findings.Select(f => $"{f.File} ({f.Problem})"))}\n" +
                $"Ersetzt: {string.Join(", ", rep)}; nicht ersetzt: {failed.Count}\nKopie danach: {after.Findings.Count} Abweichungen – {after.Note}\n";
            if (Environment.GetEnvironmentVariable("TRENDO_BELEG_DIR") is { Length: > 0 } beleg) { Directory.CreateDirectory(beleg); File.WriteAllText(Path.Combine(beleg, "installation-echt.txt"), log); }
            Assert.True(before.Ok, log);
            Assert.Equal(2, bad.Findings.Count);
            Assert.Empty(failed); Assert.True(after.Ok, log);
        }
        finally { try { Directory.Delete(root, true); } catch (IOException) { } }
    }
}

/// <summary>T-0771: Sammlung für die Lastprobe – läuft allein, nicht parallel zu anderen Testklassen.</summary>
[CollectionDefinition("T0763 Lastprobe", DisableParallelization = true)]
public sealed class T0763LastprobeSammlung;
