using System.Net;
using System.Security.Cryptography;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Sys;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0755 (Rang 33–35 der Restliste): Programme installieren, Downloads & Links, System auf Gefahren prüfen.</summary>
public class T0755_Ui
{
    static void Settle() { for (var i = 0; i < 8; i++) Dispatcher.UIThread.RunJobs(); }
    static async Task Until(Func<bool> ok, int ms = 60000) { for (var i = 0; i < ms / 20 && !ok(); i++) { Settle(); await Task.Delay(20); } Settle(); Assert.True(ok()); }

    /// <summary>ERSETZTES winget: eigenes Testprogramm (Batch-Datei), das winget-Ausgaben nachahmt und nur eine Zustandsdatei schreibt. Kein echter winget-Lauf.</summary>
    static string FakeWinget(string dir)
    {
        Directory.CreateDirectory(dir);
        string Table(string id) => "Name               Id                   Version  Source\r\n" + new string('-', 56) + "\r\n" + $"VLC media player   {id.PadRight(21)}3.0.21   winget\r\n";
        foreach (var id in new[] { "VideoLAN.VLC", "Langsam.Paket" }) File.WriteAllText(Path.Combine(dir, $"list-{id}.txt"), Table(id));
        File.WriteAllText(Path.Combine(dir, "search.txt"), Table("VideoLAN.VLC") + $"Langsames Paket    {"Langsam.Paket".PadRight(21)}1.0      winget\r\n");
        var cmd = Path.Combine(dir, "winget-ersatz.cmd");
        File.WriteAllText(cmd, string.Join("\r\n",
            "@echo off", "set \"STATE=%~dp0installiert-%3.txt\"",
            "if /I \"%1\"==\"search\" (type \"%~dp0search.txt\" & exit /b 0)",
            "if /I \"%1\"==\"list\" goto list", "if /I \"%1\"==\"install\" goto install", "exit /b 1",
            ":install",
            "echo Found [%3] Version 3.0.21", "echo Downloading https://example.invalid/setup.exe", "echo   ##########            10.0 MB / 40.0 MB",
            "if /I \"%3\"==\"Langsam.Paket\" ping -n 90 127.0.0.1 >nul",
            "ping -n 2 127.0.0.1 >nul", "echo   ####################  40.0 MB / 40.0 MB", "echo Starting package install...", "echo ok> \"%STATE%\"", "echo Successfully installed", "exit /b 0",
            ":list",
            "if not exist \"%STATE%\" (echo No installed package found matching input criteria. & exit /b -1978335212)",
            "type \"%~dp0list-%3.txt\"", "exit /b 0", ""));
        return cmd;
    }

    [AvaloniaFact(DisplayName = "T-0755 SYS-04 UI (ERSETZT: Testprogramm statt winget): Rückfrage zeigt den Befehl; Lauf zeigt Befehl, Fortschritt und Ausgabe live, schreibt ein Protokoll und prüft nach; Abbrechen beendet den Lauf")]
    public async Task AppCenter_run_visible()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (w, l, root) = Ui.Open();
        try
        {
            var exe = FakeWinget(Path.Combine(root, "winget-ersatz"));
            w.AppBackendOverride = new WingetBackend(new RealProcessRunner(), exe);
            var asked = new List<MainWindow.AppChange>();
            w.AppConfirm = c => { asked.Add(c); return Task.FromResult(true); };
            w.Show("appcenter");
            w.Find<TextBox>("appc-query").Text = "vlc"; w.Find<Button>("appc-search").Press();
            await Until(() => w.TryFind<Button>("appc-hit-install-0") is not null);
            w.Find<Button>("appc-hit-install-0").Press(); Settle();
            Assert.Contains("Befehl: " + exe + " install --id VideoLAN.VLC -e --scope user", Assert.Single(asked).Text);
            // läuft: Befehl und Abbrechen sichtbar, die Oberfläche bleibt bedienbar
            await Until(() => w.TryFind<SelectableTextBlock>("appc-run-cmd") is not null);
            Assert.StartsWith(exe + " install --id VideoLAN.VLC", w.Find<SelectableTextBlock>("appc-run-cmd").Text);
            await Until(() => w.AppRun?.Result is not null);
            Settle();
            var run = w.AppRun!; var res = run.Result!;
            Assert.True(res.Verified); Assert.Equal(1.0, run.Progress);
            Assert.Contains("Successfully installed", w.Find<TextBox>("appc-run-output").Text);
            Assert.Contains("10.0 MB / 40.0 MB", w.Find<TextBox>("appc-run-output").Text);
            Assert.Equal(100, w.Find<ProgressBar>("appc-run-progress").Value);
            Assert.Contains("VLC media player: installiert – nachgeprüft", w.Find<TextBlock>("appc-msg").Text);
            Assert.Contains("Fertig – nachgeprüft", w.Find<TextBlock>("appc-run-status").Text);
            Assert.StartsWith(w.AppRunLogFolder, res.LogFile); Assert.Contains("Ziel erreicht: ja", File.ReadAllText(res.LogFile));
            w.Find<Button>("appc-run-log").Press(); Assert.Equal(res.LogFile, l.Opened[^1]);

            // Abbrechen: das Testprogramm wartet 90 s – „Abbrechen“ beendet es sofort
            w.Find<TextBox>("appc-query").Text = "langsam"; w.Find<Button>("appc-search").Press();
            await Until(() => w.TryFind<Button>("appc-hit-install-1") is not null);
            w.Find<Button>("appc-hit-install-1").Press(); Settle();
            await Until(() => w.TryFind<TextBox>("appc-run-output")?.Text?.Contains("10.0 MB / 40.0 MB") == true && w.AppRun?.Package.Id == "Langsam.Paket");
            Assert.Equal(25, w.Find<ProgressBar>("appc-run-progress").Value, 1);
            Assert.Contains("25 %", w.Find<TextBlock>("appc-run-status").Text);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            w.Find<Button>("appc-run-cancel").Press(); Settle();
            await Until(() => w.AppRun?.Result is not null, 45000);
            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(40));
            Assert.True(w.AppRun!.Result!.Cancelled);
            Assert.Contains("abgebrochen", w.Find<TextBlock>("appc-msg").Text); Assert.Contains("nicht installiert", w.Find<TextBlock>("appc-msg").Text);
            Assert.Contains("Ergebnis: abgebrochen", File.ReadAllText(w.AppRun.Result.LogFile));
            Assert.False(File.Exists(Path.Combine(root, "winget-ersatz", "installiert-Langsam.Paket.txt")));
            w.Find<Button>("appc-run-close").Press(); Assert.Null(w.TryFind<StackPanel>("appc-run"));
        }
        finally { w.AppRunCancel(); w.Close(); }
    }

    static HttpListener Server(out int port)
    {
        for (var i = 0; i < 8; i++)
        {
            port = WebsitePreview.FreePort(); var l = new HttpListener(); l.Prefixes.Add($"http://127.0.0.1:{port}/");
            try { l.Start(); return l; } catch (HttpListenerException) { l.Close(); }
        }
        throw new IOException("Kein freier Port.");
    }

    [AvaloniaFact(DisplayName = "T-0755 SYS-03 UI: „Downloads & Links“ – echter HTTP-Download (lokaler Testserver) zeigt SHA-256, vergleicht mit der Angabe, Anhalten/Fortsetzen, Verlauf bleibt; Links sammeln und im Standard-Browser öffnen")]
    public async Task Downloads_and_links()
    {
        var data = new byte[2_500_000]; new Random(11).NextBytes(data);
        var sha = Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
        using var srv = Server(out var port); var slow = true;
        _ = Task.Run(async () =>
        {
            while (srv.IsListening)
            {
                HttpListenerContext c; try { c = await srv.GetContextAsync(); } catch { break; }
                _ = Task.Run(async () =>
                {
                    var r = c.Request.Headers["Range"]; long from = 0;
                    if (r is not null) { from = long.Parse(r.Replace("bytes=", "").TrimEnd('-')); c.Response.StatusCode = 206; c.Response.AddHeader("Content-Range", $"bytes {from}-{data.Length - 1}/{data.Length}"); }
                    c.Response.ContentLength64 = data.Length - from;
                    try
                    {
                        for (var i = (int)from; i < data.Length; i += 50_000)
                        { await c.Response.OutputStream.WriteAsync(data.AsMemory(i, Math.Min(50_000, data.Length - i))); if (slow && c.Request.Url!.AbsolutePath.Contains("langsam")) await Task.Delay(60); }
                        c.Response.Close();
                    }
                    catch { }
                });
            }
        });
        var (w, l, _) = Ui.Open();
        MainWindow.DownloadRunOverride = (j, p, ct) => WebTools.DownloadAsync(j.Url, j.Target, p, ct); // echter Downloader (im Testmodus sonst gesperrt)
        try
        {
            // ehrlicher Name überall
            Assert.Equal("Downloads & Links", StudioCatalog.For("SYS-03")!.Name);
            Assert.DoesNotContain("surf", StudioCatalog.For("SYS-03")!.Sentence, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("Downloads & Links", Trendo.Core.PromptAction.CapabilityCatalog.Profiles.First(s => s.Id == "SYS-03").Title);
            w.Show("downloads"); Settle();
            Assert.Contains(Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(w).OfType<TextBlock>(), t => t.Text == "Downloads & Links");

            // 1) Download mit richtiger Prüfsumme
            w.Find<TextBox>("dlm-url").Text = $"http://127.0.0.1:{port}/datei.bin"; w.Find<TextBox>("dlm-hash").Text = sha.ToUpperInvariant();
            w.Find<Button>("dlm-add").Press(); Settle();
            await Until(() => w.Downloads.Jobs.All(j => j.Status is DownloadQueue.State.Fertig or DownloadQueue.State.Fehler));
            w.Show("downloads"); Settle();
            Assert.Equal("SHA-256: " + sha, w.Find<SelectableTextBlock>("dlm-sha-0").Text);
            Assert.StartsWith("✓ Stimmt", w.Find<SelectableTextBlock>("dlm-sha-0-vergleich").Text);
            Assert.Equal(sha, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(w.Downloads.Jobs[0].Target))).ToLowerInvariant());

            // 2) falsche Prüfsumme → Fehler, Datei nicht öffnen
            w.Find<TextBox>("dlm-url").Text = $"http://127.0.0.1:{port}/zweite.bin"; w.Find<TextBox>("dlm-hash").Text = new string('a', 64);
            w.Find<Button>("dlm-add").Press(); Settle();
            await Until(() => w.Downloads.Jobs.Count == 2 && w.Downloads.Jobs.All(j => j.Status is DownloadQueue.State.Fertig or DownloadQueue.State.Fehler));
            w.Show("downloads"); Settle();
            Assert.Equal(DownloadQueue.State.Fehler, w.Downloads.Jobs[1].Status);
            Assert.StartsWith("✗ Stimmt NICHT", w.Find<SelectableTextBlock>("dlm-sha-1-vergleich").Text);

            // 3) Anhalten und Fortsetzen (langsamer Server), danach dieselbe Prüfsumme
            w.Find<TextBox>("dlm-url").Text = $"http://127.0.0.1:{port}/langsam.bin"; w.Find<TextBox>("dlm-hash").Text = sha;
            w.Find<Button>("dlm-add").Press(); Settle();
            var job = w.Downloads.Jobs[2];
            await Until(() => job.Done > 200_000);
            w.DownloadPause(job.Id); Settle();
            await Until(() => job.Status == DownloadQueue.State.Angehalten);
            await Task.Delay(300);
            var part = new FileInfo(Web.PartFile(job.Target)).Length; Assert.InRange(part, 1, data.Length - 1);
            slow = false;
            w.Show("downloads"); Settle();
            w.Find<Button>("dlm-resume-2").Press(); Settle();
            await Until(() => job.Status is DownloadQueue.State.Fertig or DownloadQueue.State.Fehler);
            Assert.Equal(DownloadQueue.State.Fertig, job.Status); Assert.Equal(sha, job.Sha256);

            // 4) Verlauf bleibt, auch wenn Fertige aus der Liste genommen werden
            w.Show("downloads"); Settle();
            w.Find<Button>("dlm-clear").Press(); Settle();
            w.Find<Button>("dlm-tab-verlauf").Press(); Settle();
            Assert.NotNull(w.TryFind<Border>("dlm-hist-2") ?? (Control?)w.TryFind<StackPanel>("dlm-hist-2"));
            Assert.Equal(3, w.Downloads.History.Load().Count);
            Assert.Contains(w.Downloads.History.Load(), h => !h.Ok && h.Message.Contains("stimmt nicht"));
            w.Find<Button>("dlm-tab-alle").Press(); Settle();

            // 5) Links sammeln, im Standard-Browser öffnen, entfernen
            w.Find<TextBox>("links-text").Text = "Siehe https://example.org/a.pdf und https://example.org/a.pdf, außerdem http://example.net/seite – kein Link: javascript:alert(1)";
            w.Find<Button>("links-collect").Press(); Settle();
            Assert.Equal("2 Links gesammelt.", w.Find<TextBlock>("links-msg").Text);
            w.Find<TextBox>("links-text").Text = "nochmal: https://example.org/a.pdf";
            w.Find<Button>("links-collect").Press(); Settle();
            Assert.Equal("Diese Links sind schon gesammelt.", w.Find<TextBlock>("links-msg").Text);
            w.Find<Button>("links-open-0").Press();
            Assert.Equal("http://example.net/seite", l.Opened[^1]);
            w.Find<Button>("links-remove-0").Press(); Settle();
            Assert.Single(Bookmarks.Load(w.Core.DataRoot), b => b.Folder == Bookmarks.Collected);
        }
        finally { MainWindow.DownloadRunOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0755 SPEC-12 UI (echte Dateien, echte Signaturprüfung): „System prüfen“ zeigt Ampel, Datei, Herausgeber, Erklärung; Filter; Bericht als HTML/CSV; nichts wird verändert")]
    public async Task System_scan_page()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (w, l, root) = Ui.Open();
        var start = Path.Combine(root, "Autostart"); Directory.CreateDirectory(start);
        var unsigned = Path.Combine(start, "testprogramm.exe"); File.Copy(typeof(SystemScan).Assembly.Location, unsigned);
        var signedCopy = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
        try
        {
            w.ScanSourcesOverride = () => new SystemScan.Sources([start], RunKeys: true, Tasks: false, Programs: false); // Run-Schlüssel dieses PCs: nur gelesen
            w.Show("forensik"); Settle();
            Assert.Equal("System prüfen", w.Find<Button>("for-scan").Content);
            w.Find<Button>("for-scan").Press(); Settle();
            await w.SystemScanTask!.WaitAsync(TimeSpan.FromSeconds(60)); Settle();
            var r = w.LastSystemScan!;
            var t = r.Findings.Single(f => f.Entry.File == unsigned);
            Assert.Equal(Ampel.Rot, t.Light); Assert.Equal(SigState.Unsigniert, t.Signature.State);
            Assert.Contains("Fertig:", w.Find<TextBlock>("for-scan-msg").Text);
            var row = r.Findings.Where(f => f.Light != Ampel.Gruen).ToList().IndexOf(t);
            Assert.Contains("%TEMP%", w.Find<TextBlock>($"for-scan-text-{row}").Text);
            w.Find<Button>("for-scan-f-alle").Press(); Settle();
            Assert.NotNull(w.TryFind<Grid>("for-scan-row-0"));
            Assert.Equal(Authenticode.Check(signedCopy).State, SigState.Gueltig);
            w.Find<Button>("for-scan-export").Press(); Settle();
            var html = l.Opened[^1]; Assert.EndsWith(".html", html);
            Assert.Contains("testprogramm.exe", File.ReadAllText(html)); Assert.True(File.Exists(Path.ChangeExtension(html, ".csv")));
            Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "gefahren.pruefung");
            Assert.True(File.Exists(unsigned)); // nichts verändert
        }
        finally { w.Close(); }
    }
}
