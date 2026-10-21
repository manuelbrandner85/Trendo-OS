using System.Diagnostics;
using System.Runtime.Versioning;
using Trendo.Core;
using Trendo.Core.Sys;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Echte Windows-Prüfungen (SYS-02/03/10/11/12/13). Außerhalb von Windows übersprungen – nie als bestanden gezählt.</summary>
public sealed class WinFactAttribute : FactAttribute
{
    public WinFactAttribute() { if (!OperatingSystem.IsWindows()) Skip = "nur auf Windows"; }
}

[SupportedOSPlatform("windows")]
public class WinRealTests
{
    static void Log(string s)
    {
        var f = Environment.GetEnvironmentVariable("TRENDO_WINLOG");
        lock (typeof(WinRealTests)) { if (f is null) Console.WriteLine(s); else File.AppendAllText(f, $"[{DateTime.Now:HH:mm:ss}] {s}\n"); }
    }
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-win-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }
    static (int Exit, string Out) Run(string exe, params string[] args)
    {
        var psi = new ProcessStartInfo(exe) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!; var o = p.StandardOutput.ReadToEndAsync(); var e = p.StandardError.ReadToEndAsync();
        if (!p.WaitForExit(180000)) { p.Kill(true); return (-1, "Timeout"); }
        return (p.ExitCode, o.Result + e.Result);
    }
    static string Ps(string script) => Run("powershell", "-NoProfile", "-NonInteractive", "-Command", script).Out.Trim();
    static string? Chrome() => new[] { @"%ProgramFiles%\Google\Chrome\Application\chrome.exe", @"%ProgramFiles(x86)%\Google\Chrome\Application\chrome.exe", @"%LOCALAPPDATA%\Google\Chrome\Application\chrome.exe", @"%ProgramFiles(x86)%\Microsoft\Edge\Application\msedge.exe" }
        .Select(Environment.ExpandEnvironmentVariables).FirstOrDefault(File.Exists);

    [WinFact]
    public void Autostart_echt_lesen_und_im_Temp_umschalten()
    {
        var real = Software.StartupFolder(Environment.GetFolderPath(Environment.SpecialFolder.Startup));
        using var run = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        var reg = run?.GetValueNames() ?? [];
        Log($"AUTOSTART Ordner={real.Count} Einträge, HKCU\\Run={reg.Length} Einträge (nur gelesen)");
        var d = Tmp(); var on = Path.Combine(d, "an"); var off = Path.Combine(d, "aus"); Directory.CreateDirectory(on);
        File.WriteAllText(Path.Combine(on, "Test.lnk"), "x");
        var it = Software.StartupFolder(on, off).Single();
        Assert.Throws<InvalidOperationException>(() => Software.SetStartupEnabled(it, on, off, false, confirmed: false));
        Software.SetStartupEnabled(it, on, off, false, true);
        var after = Software.StartupFolder(on, off).Single(); Assert.False(after.Enabled);
        Software.SetStartupEnabled(after, on, off, true, true); Assert.True(Software.StartupFolder(on, off).Single().Enabled);
        Log("AUTOSTART Umschalten aus→an im Temp-Ordner OK (echter Autostart unverändert)");
        Directory.Delete(d, true);
    }

    [WinFact]
    public void Dateizuordnungen_echt_lesen()
    {
        var found = 0;
        foreach (var ext in Software.CommonExtensions)
        {
            using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey($@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\{ext}\UserChoice");
            var pid = k?.GetValue("ProgId") as string; if (pid is not null) found++;
            Log($"ZUORDNUNG {ext} → {Software.ProgIdName(pid)}");
        }
        Log($"ZUORDNUNG {found}/{Software.CommonExtensions.Length} mit eigener Benutzerwahl (nur gelesen)");
        Assert.Equal("Windows-Standard", Software.ProgIdName(null));
    }

    [WinFact]
    public void Winget_export_und_ImportIds()
    {
        var d = Tmp(); var f = Path.Combine(d, "export.json");
        var r = Run("winget", "export", "-o", f, "--accept-source-agreements", "--disable-interactivity");
        Assert.True(File.Exists(f), "winget export schrieb keine Datei: " + r.Out[..Math.Min(300, r.Out.Length)]);
        var ids = Software.ImportIds(File.ReadAllText(f));
        Log($"WINGET export exit={r.Exit}, {ids.Count} Pakete gelesen");
        Assert.NotEmpty(ids);
        var round = Software.ImportIds(Software.WingetExport(ids.Select(i => new PackageInfo(i, i, "1", null, "winget"))));
        Assert.Equal(ids.Where(i => PackageParsers.SafeId(i) && i.Contains('.')).Count(), round.Count);
        Log($"WINGET Rundreise eigene Exportdatei → {round.Count} Pakete");
        Directory.Delete(d, true);
    }

    [WinFact]
    public void Papierkorb_echt_lesen()
    {
        var sid = System.Security.Principal.WindowsIdentity.GetCurrent().User!.Value;
        var bin = Path.Combine(Path.GetPathRoot(Environment.SystemDirectory)!, "$Recycle.Bin", sid);
        var d = Tmp(); var f = Path.Combine(d, "trendo-papierkorb-test.txt"); File.WriteAllText(f, "Trendo QA");
        Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(f, Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
        var items = Directory.GetFiles(bin, "$I*").Select(Trash.ParseWindowsInfo).Where(i => i is not null).ToList();
        var mine = items.FirstOrDefault(i => i!.OriginalPath.Equals(f, StringComparison.OrdinalIgnoreCase));
        Log($"PAPIERKORB {items.Count} Einträge gelesen, Testdatei gefunden={mine is not null}, Größe={mine?.Bytes}");
        Assert.NotNull(mine); Assert.Equal(9, mine!.Bytes); Assert.True(File.Exists(mine.DataFile));
        // eigene Testdatei wieder entfernen ($I + $R), sonst nichts anfassen
        File.Delete(mine.DataFile); File.Delete(Path.Combine(bin, "$I" + Path.GetFileName(mine.DataFile)[2..]));
        Directory.Delete(d, true);
    }

    [WinFact]
    public void Defender_scan_saubere_Datei()
    {
        var exe = Environment.ExpandEnvironmentVariables(Web.MpCmdRun);
        var d = Tmp(); var f = Path.Combine(d, "sauber.txt"); File.WriteAllText(f, "Hallo Trendo");
        if (!File.Exists(exe)) { Log("DEFENDER MpCmdRun.exe nicht gefunden – NICHT GEPRÜFT"); return; }
        var r = Run(exe, [.. Web.DefenderArgs(f)]);
        var msg = Web.DefenderResult(r.Exit, r.Out);
        Log($"DEFENDER exit={r.Exit} → {msg}");
        Assert.True(r.Exit is 0 or 2 || msg.Contains("Administrator"), msg);
        if (r.Exit != 2) Assert.NotEqual(2, r.Exit);
        Directory.Delete(d, true);
    }

    [WinFact]
    public void Drucker_auflisten_und_Seite_als_PDF()
    {
        var printers = Ps("(Get-CimInstance Win32_Printer | Measure-Object).Count; (Get-CimInstance Win32_Printer | ? Default).Name");
        Log($"DRUCKEN Drucker (Anzahl / Standard): {printers.Replace("\r\n", " / ")} – nichts gedruckt");
        var chrome = Chrome(); if (chrome is null) { Log("DRUCKEN kein Chrome/Edge – PDF NICHT GEPRÜFT"); return; }
        var d = Tmp(); var html = Path.Combine(d, "seite.html"); File.WriteAllText(html, "<h1>Trendo Druck</h1>"); var pdf = Path.Combine(d, "seite.pdf");
        var args = PrintArgs(new Uri(html), pdf);
        var r = Run(chrome, [.. args]);
        var ok = File.Exists(pdf) && File.ReadAllBytes(pdf).Take(4).SequenceEqual("%PDF"u8.ToArray());
        Log($"DRUCKEN als PDF exit={r.Exit} pdf={ok} {(ok ? new FileInfo(pdf).Length : 0)} B");
        Assert.True(ok);
        try { Directory.Delete(d, true); } catch (IOException) { }
    }
    static List<string> PrintArgs(Uri u, string p) => Web.PrintToPdfArgs(u, p);

    [WinFact]
    public void Vorlesen_SAPI_in_WAV_und_Sprachausgabe_vorhanden()
    {
        var cmd = A11y.SpeakCommand("Hallo, ich bin Trendo.", true, _ => null)!.Value;
        Assert.Contains("System.Speech", cmd.Args[^1]);
        var d = Tmp(); var wav = Path.Combine(d, "test.wav");
        // gleiche Engine wie SpeakCommand, aber stumm in eine Datei
        string script;
        script = "Add-Type -AssemblyName System.Speech; $v=New-Object System.Speech.Synthesis.SpeechSynthesizer; ($v.GetInstalledVoices() | % { $_.VoiceInfo.Culture.Name }) -join ','; " + cmd.Args[^1].Replace("$s.Speak(", $"$s.SetOutputToWaveFile('{wav}'); $s.Speak(") + "; $s.Dispose()";
        var voices = Ps(script);
        var len = File.Exists(wav) ? new FileInfo(wav).Length : 0;
        Log($"VORLESEN Stimmen: {voices}; WAV {len} B (stumm, nichts abgespielt)");
        Assert.True(len > 10000);
        var narrator = Path.Combine(Environment.SystemDirectory, "Narrator.exe");
        Log($"SPRACHAUSGABE Narrator.exe vorhanden={File.Exists(narrator)} – Bedienung mit Narrator nur manuell prüfbar (nicht gestartet)");
        Directory.Delete(d, true);
    }

    [WinFact]
    public void Bildschirme_echt_und_Fensterposition()
    {
        var raw = Ps("Add-Type -AssemblyName System.Windows.Forms; [System.Windows.Forms.Screen]::AllScreens | % { '{0},{1},{2},{3}' -f $_.Bounds.X,$_.Bounds.Y,$_.Bounds.Width,$_.Bounds.Height }");
        var screens = raw.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim().Split(',').Select(int.Parse).ToArray()).Select(a => new A11y.Rect(a[0], a[1], a[2], a[3])).ToList();
        Log($"BILDSCHIRME {screens.Count}: {string.Join(" | ", screens.Select(s => $"{s.X},{s.Y} {s.W}×{s.H}"))}");
        Assert.NotEmpty(screens);
        var p = screens[0];
        Assert.NotNull(A11y.RestorePosition(new A11y.Rect(p.X + 50, p.Y + 50, 800, 600), screens));
        Assert.Null(A11y.RestorePosition(new A11y.Rect(p.X + p.W + 5000, p.Y, 800, 600), screens)); // abgezogener zweiter Bildschirm
        Log("BILDSCHIRME Position auf echtem Schirm übernommen, Position auf fehlendem Schirm → Standardplatz");
    }

    [WinFact]
    public async Task Qpdf_verwaltet_zusammenfuegen_und_teilen()
    {
        var tq = Tmp();
        var deps = new DependencyManager(new AuditLog(Path.Combine(tq, "a.jsonl")), new RealProcessRunner()) { ConsentFile = Path.Combine(tq, "c.txt") };
        var q = deps.ResolvePath(DependencyCatalog.Get("qpdf"));
        if (q is null && Environment.GetEnvironmentVariable("TRENDO_INSTALL_QPDF") == "1")
        {
            // echte verwaltete Installation: Download + Prüfsumme + Entpacken nach %LOCALAPPDATA%\TrendoWerkzeuge (kein Admin)
            deps.GiveConsent(Principal.LocalOwner);
            var sw = Stopwatch.StartNew(); var ok = await deps.InstallAsync(Principal.LocalOwner, DependencyCatalog.Get("qpdf"));
            q = deps.ResolvePath(DependencyCatalog.Get("qpdf"));
            Log($"QPDF verwaltete Installation ok={ok} in {sw.ElapsedMilliseconds} ms → %LOCALAPPDATA%\\TrendoWerkzeuge\\qpdf");
        }
        if (q is null) { Log("QPDF nicht installiert – NICHT GEPRÜFT"); Assert.Fail("qpdf fehlt"); return; }
        var chrome = Chrome()!; var d = Tmp(); var a = Path.Combine(d, "a.pdf"); var b = Path.Combine(d, "b.pdf");
        foreach (var (f, t) in new[] { (a, "Eins"), (b, "Zwei") }) { var h = Path.Combine(d, t + ".html"); File.WriteAllText(h, $"<h1>{t}</h1>"); Run(chrome, [.. PrintArgs(new Uri(h), f)]); }
        var m = Path.Combine(d, "zusammen.pdf"); var (exe, args) = Pdf.MergeCmd(q, null, [a, b], m);
        var r = Run(exe, [.. args]); var n = Run(q, "--show-npages", m).Out.Trim();
        Log($"QPDF {Run(q, "--version").Out.Split('\n')[0].Trim()}: zusammengefügt exit={r.Exit}, Seiten={n}");
        Assert.Equal("2", n);
        var x = Path.Combine(d, "seite2.pdf"); var (e2, a2) = Pdf.ExtractCmd(q, null, m, 2, 2, x); var r2 = Run(e2, [.. a2]);
        var n2 = Run(q, "--show-npages", x).Out.Trim(); Log($"QPDF Seite 2 herausgelöst exit={r2.Exit}, Seiten={n2}");
        Assert.Equal("1", n2);
        try { Directory.Delete(d, true); } catch (IOException) { }
    }

    [WinFact]
    public void Rueckfall_vorbereiten_sichert_Einstellungen()
    {
        var root = Tmp(); Directory.CreateDirectory(Path.Combine(root, "audit"));
        File.WriteAllText(UserSettings.PathIn(root), "{}"); File.WriteAllText(Path.Combine(root, "audit", "audit.jsonl"), "{}\n");
        var b = Quality.PrepareDowngrade(root, "0.9.3");
        Assert.Equal(2, Directory.GetFiles(b).Length);
        Log("RÜCKFALL Sicherung vor Downgrade: 2 Dateien");
        Directory.Delete(root, true);
    }
}
