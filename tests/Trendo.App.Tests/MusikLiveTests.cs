using System.Diagnostics;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Echter Probelauf der Musik-KI am PC (nur auf Anforderung): TRENDO_ACE_LIVE = Pfad zum heruntergeladenen ACE-Step-1.5.7z,
/// TRENDO_7Z = 7z.exe. Prüft und entpackt mit Trendos Code nach %TEMP%\trendo-ace-live, startet den Server wie Trendo, erzeugt 30 s Musik.
/// Ergebnis und Server-Protokoll bleiben in %TEMP%\trendo-ace-live\ergebnis (zum Anhören), der Rest wird gelöscht.</summary>
public class MusikLiveTests
{
    public static bool Live => File.Exists(Environment.GetEnvironmentVariable("TRENDO_ACE_LIVE") ?? "") && File.Exists(Environment.GetEnvironmentVariable("TRENDO_7Z") ?? "");

    [Fact(DisplayName = "Musik-KI live: Paket prüfen, entpacken, Server starten, 30 s Lied erzeugen", Skip = "Echter Lauf: TRENDO_ACE_LIVE und TRENDO_7Z setzen", SkipUnless = nameof(Live), Timeout = 7_200_000)]
    public async Task Echt_ein_Lied()
    {
        var ct = TestContext.Current.CancellationToken;
        var archive = Environment.GetEnvironmentVariable("TRENDO_ACE_LIVE")!;
        var seven = Environment.GetEnvironmentVariable("TRENDO_7Z")!;
        var root = Path.Combine(Path.GetTempPath(), "trendo-ace-live");
        var outDir = Path.Combine(root, "ergebnis"); Directory.CreateDirectory(outDir);
        var tools = Path.Combine(root, "TrendoWerkzeuge");
        var log = Path.Combine(outDir, "ablauf.log");
        void Say(string s) => File.AppendAllText(log, $"{DateTime.Now:HH:mm:ss} {s}{Environment.NewLine}");
        var store = new KiLibraryStore(tools);
        var audit = new AuditLog(Path.Combine(outDir, "audit.jsonl"));
        if (!store.RuntimeReady(KiRuntime.AceStep))
        {
            Say("Prüfen und entpacken …");
            store.InstallRuntime(KiRuntime.AceStep, archive, audit, (a, t) =>
            {
                var r = Cli.Run(seven, ["x", a, "-o" + t, "-y", "-bso0", "-bsp0", "-snl-"], timeoutMs: 3_600_000);
                Assert.True(r.Ok, r.Err);
            });
        }
        Say("Laufzeit bereit: " + store.RuntimeReady(KiRuntime.AceStep));
        Assert.True(store.RuntimeReady(KiRuntime.AceStep));

        var dir = store.RuntimeDir(KiRuntime.AceStep);
        var (file, args) = AceStepClient.ServerCommand(dir);
        var psi = new ProcessStartInfo(file) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = dir, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var a in args) psi.ArgumentList.Add(a);
        foreach (var (k, v) in AceStepClient.ServerEnvironment()) psi.Environment[k] = v;
        var serverLog = Path.Combine(outDir, "server.log");
        using var p = Process.Start(psi)!;
        p.OutputDataReceived += (_, e) => { if (e.Data is { } l) lock (serverLog) File.AppendAllText(serverLog, l + Environment.NewLine); };
        p.ErrorDataReceived += (_, e) => { if (e.Data is { } l) lock (serverLog) File.AppendAllText(serverLog, l + Environment.NewLine); };
        p.BeginOutputReadLine(); p.BeginErrorReadLine();
        Say($"Server gestartet, PID {p.Id}");
        try
        {
            var c = new AceStepClient(new HttpClient { Timeout = TimeSpan.FromMinutes(2) });
            var t0 = DateTime.UtcNow;
            while (!await c.HealthyAsync(ct))
            {
                Assert.False(p.HasExited, "Server beendet, siehe server.log");
                Assert.True(DateTime.UtcNow - t0 < TimeSpan.FromMinutes(60), "Server antwortet nicht");
                await Task.Delay(5000, ct);
            }
            Say($"Server bereit nach {(int)(DateTime.UtcNow - t0).TotalSeconds} s");
            var t1 = DateTime.UtcNow;
            var id = await c.SubmitAsync(new("fröhlicher Pop mit Klavier und Gitarre, warme Stimme", "[Verse]\nHeute scheint die Sonne über unsrer Stadt\n[Chorus]\nWir singen laut, weil jeder Tag ein Anfang hat", 30, "de"), ct);
            AceStepClient.State st;
            while ((st = await c.QueryAsync(id, ct)).Status == 0)
            {
                Assert.False(p.HasExited, "Server beendet, siehe server.log");
                Assert.True(DateTime.UtcNow - t1 < TimeSpan.FromMinutes(60), "Erzeugen dauert zu lange");
                await Task.Delay(3000, ct);
            }
            Assert.True(st.Status == 1, st.Error);
            var song = Path.Combine(outDir, "trendo-musik-probe.mp3");
            await c.DownloadAsync(st.AudioUrl!, song, ct);
            var len = new FileInfo(song).Length;
            Say($"Lied fertig nach {(int)(DateTime.UtcNow - t1).TotalSeconds} s: {len} Bytes");
            Assert.True(len > 100_000, "Lied zu klein: " + len);
        }
        finally
        {
            try { if (!p.HasExited) p.Kill(true); } catch (InvalidOperationException) { }
        }
    }
}
