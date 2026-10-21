using Avalonia;
using Trendo.Core;

namespace Trendo.App;

public sealed record AppOptions(string? DataRoot, string? ScreenshotPath, int AutoExitSeconds, bool Offline = false, int? ApiPort = null, bool FirstRun = false, string? Page = null, string? KiAsk = null, string? Query = null);

class Program
{
    public static AppOptions Options { get; private set; } = new(null, null, 0);

    [STAThread]
    public static int Main(string[] args)
    {
        // Velopack: verarbeitet Installations-/Deinstallations-Hooks des Setup.exe (muss zuerst laufen)
        Velopack.VelopackApp.Build().Run();
        // Arbeitsverzeichnis aus dem Programmordner herausnehmen: gestartete Programme (winget, Ollama) würden sonst Updates blockieren
        try { Directory.SetCurrentDirectory(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)); } catch { }
        string? Arg(string n) { var i = Array.IndexOf(args, n); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
        Options = new AppOptions(Arg("--data-root"), Arg("--screenshot"),
            int.TryParse(Arg("--auto-exit"), out var s) ? s : (args.Contains("--smoke") ? 5 : 0),
            args.Contains("--offline"),
            args.Contains("--api") ? (int.TryParse(Arg("--api-port"), out var ap) ? ap : 47800) : null,
            FirstRun: !args.Contains("--no-first-run"), Page: Arg("--page"), KiAsk: Arg("--ki-ask"), Query: Arg("--query"));

        var crashRoot = Options.DataRoot ?? TrendoCore.DefaultDataRoot();
        AppDomain.CurrentDomain.UnhandledException += (_, e) => { if (e.ExceptionObject is Exception ex) try { CrashLog.Write(crashRoot, ex, "Programm"); } catch { } };
        TaskScheduler.UnobservedTaskException += (_, e) => { try { CrashLog.Write(crashRoot, e.Exception, "Hintergrundaufgabe"); } catch { } e.SetObserved(); };
        if (args.Contains("--selftest-crashlog")) { Console.WriteLine(CrashLog.Write(crashRoot, new InvalidOperationException("Selbsttest Absturzprotokoll"), "Selbsttest")); return 0; }
        if (args.Contains("--version")) { Console.WriteLine(CrashLog.AppVersion()); return 0; }
        if (args.Contains("--selftest-open-explorer"))
        {
            var core = new TrendoCore(Options.DataRoot ?? TrendoCore.DefaultDataRoot(), offline: Options.Offline);
            return SelfTest.OpenExplorer(core, Arg("--approver") ?? "operator-cli", Console.Out);
        }
        if (args.Contains("--deps-status") || Arg("--install-deps") is not null)
        {
            // Konsolenweg für dieselbe Komponenten-Logik wie in der UI (z. B. für Tests/stille Erstinstallation).
            var core = new TrendoCore(Options.DataRoot ?? TrendoCore.DefaultDataRoot(), offline: Options.Offline);
            var dm = new DependencyManager(core.Audit, new RealProcessRunner()) { ConsentFile = Path.Combine(core.DataRoot, "deps-consent.txt"), ToolsFile = Path.Combine(core.DataRoot, "tools.json") };
            dm.OllamaUp = new OllamaClient().IsUpAsync;
            var rc = 0;
            if (Arg("--install-deps") is { } ids)
            {
                if (!dm.HasConsent)
                {
                    if (!args.Contains("--accept-licenses")) { Console.WriteLine("Lizenz-Zustimmung fehlt (--accept-licenses)"); return 3; }
                    dm.GiveConsent(Principal.LocalOwner);
                }
                var list = ids == "auto" ? DependencyCatalog.All.Where(c => c.Mode == DeliveryMode.AutoUser).ToList() : ids.Split(',').Select(DependencyCatalog.Get).ToList();
                foreach (var c in list)
                {
                    var req = core.Approvals.Request(Principal.LocalOwner, "deps.install", $"{c.Name} installieren", c.Id);
                    core.Approvals.Approve(req.Id, new Principal(Arg("--approver") ?? "operator-cli", Role.Owner));
                    Console.WriteLine($"Freigabe {req.Id[..8]} erteilt für {c.Id}");
                    var ok = dm.InstallAsync(Principal.LocalOwner, c, l => { if (l.Trim().Length > 0 && !l.Contains('\r') && l.Length < 200) Console.WriteLine($"  {c.Id}: {l.Trim()}"); }).GetAwaiter().GetResult();
                    Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  Installation {c.Name}"); if (!ok) rc = 1;
                }
            }
            foreach (var c in DependencyCatalog.All)
            {
                var ch = dm.CheckAsync(c).GetAwaiter().GetResult();
                Console.WriteLine($"{c.Id,-12} {c.Mode,-12} {ch.State,-10} {ch.Version ?? "",-40} {c.License}");
            }
            core.Shutdown();
            return rc;
        }
        if (args.Contains("--api-selftest"))
        {
            var core = new TrendoCore(Options.DataRoot ?? TrendoCore.DefaultDataRoot(), launcher: new NoopLauncher(), offline: Options.Offline);
            var req = TrendoCore.FindRequirements(AppContext.BaseDirectory) ?? throw new FileNotFoundException("requirements.yaml");
            var rc = Trendo.Api.ApiSelfTest.RunAsync(core, req, Console.Out).GetAwaiter().GetResult();
            core.Shutdown();
            return rc;
        }
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().LogToTrace();
}

/// <summary>Launcher für den API-Selbsttest: führt nichts aus (der Test lehnt Aktionen ab).</summary>
sealed class NoopLauncher : IProcessLauncher { public void Open(string target) { } }
