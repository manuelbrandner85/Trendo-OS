using Avalonia;
using Trendo.Core;

namespace Trendo.App;

public sealed record AppOptions(string? DataRoot, string? ScreenshotPath, int AutoExitSeconds, bool Offline = false, int? ApiPort = null, bool FirstRun = false, string? Page = null, string? KiAsk = null, string? Query = null, bool Isolated = false);

class Program
{
    public static AppOptions Options { get; private set; } = new(null, null, 0);

    [STAThread]
    public static int Main(string[] args)
    {
        // Velopack: verarbeitet Installations-/Deinstallations-Hooks des Setup.exe (muss zuerst laufen)
        StartupTrace.Mark("main");
        var uiArgs = AppStartup.WithoutVelopack(args);
        // Fast-Hooks (--veloapp-install usw.) beendet Velopack selbst. OnFirstRun beendet nicht: danach muss das Fenster erscheinen.
        var velo = Velopack.VelopackApp.Build().OnFirstRun(_ => AppStartup.NoteFirstRun());
        // Deinstallieren (nur Windows): fragen, ob auch die eigenen Daten weg sollen – Standard: behalten
        if (OperatingSystem.IsWindows()) velo = velo.OnBeforeUninstallFastCallback(_ => UninstallPrompt.Run(UninstallPrompt.LogToTemp));
        // T-0222: Ordner des Vollpakets merken, solange Setup.exe noch läuft (beim ersten Start ist es schon beendet)
        if (OperatingSystem.IsWindows()) velo = velo.OnAfterInstallFastCallback(_ => VollpaketStart.RememberFromInstaller(log: UninstallPrompt.LogToTemp));
        velo.Run();
        // Arbeitsverzeichnis aus dem Programmordner herausnehmen: gestartete Programme (winget, Ollama) würden sonst Updates blockieren
        try { Directory.SetCurrentDirectory(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)); } catch { }
        string? Arg(string n) { var i = Array.IndexOf(uiArgs, n); return i >= 0 && i + 1 < uiArgs.Length ? uiArgs[i + 1] : null; }
        Options = new AppOptions(Arg("--data-root"), Arg("--screenshot"),
            int.TryParse(Arg("--auto-exit"), out var s) ? s : (uiArgs.Contains("--smoke") ? 5 : 0),
            uiArgs.Contains("--offline"),
            uiArgs.Contains("--api") ? (int.TryParse(Arg("--api-port"), out var ap) ? ap : 47800) : null,
            FirstRun: !uiArgs.Contains("--no-first-run"), Page: Arg("--page"), KiAsk: Arg("--ki-ask"), Query: Arg("--query"));

        var crashRoot = Options.DataRoot ?? TrendoCore.DefaultDataRoot();
        AppDomain.CurrentDomain.UnhandledException += (_, e) => { if (e.ExceptionObject is Exception ex) try { CrashLog.Write(crashRoot, ex, "Programm"); } catch { } };
        TaskScheduler.UnobservedTaskException += (_, e) => { try { CrashLog.Write(crashRoot, e.Exception, "Hintergrundaufgabe"); } catch { } e.SetObserved(); };
        if (uiArgs.Contains("--selftest-crashlog")) { Console.WriteLine(CrashLog.Write(crashRoot, new InvalidOperationException("Selbsttest Absturzprotokoll"), "Selbsttest")); return 0; }
        if (uiArgs.Contains("--version")) { Console.WriteLine(CrashLog.AppVersion()); return 0; }
        if (uiArgs.Contains("--selftest-open-explorer"))
        {
            var core = new TrendoCore(Options.DataRoot ?? TrendoCore.DefaultDataRoot(), offline: Options.Offline);
            return SelfTest.OpenExplorer(core, Arg("--approver") ?? "operator-cli", Console.Out);
        }
        if (uiArgs.Contains("--deps-status") || Arg("--install-deps") is not null)
        {
            // Konsolenweg für dieselbe Komponenten-Logik wie in der UI (z. B. für Tests/stille Erstinstallation).
            var core = new TrendoCore(Options.DataRoot ?? TrendoCore.DefaultDataRoot(), offline: Options.Offline);
            var dm = new DependencyManager(core.Audit, new RealProcessRunner()) { ConsentFile = Path.Combine(core.DataRoot, "deps-consent.txt"), ToolsFile = Path.Combine(core.DataRoot, "tools.json") };
            dm.OllamaUp = new OllamaClient().IsUpAsync;
            // T-0222: Vollpaket ausdrücklich angeben oder suchen lassen; --werkzeuge für Prüfläufe in einem eigenen Ordner
            if (Arg("--werkzeuge") is { } wz) dm.ToolsRoot = Path.GetFullPath(wz);
            if (Arg("--vollpaket") is { } vp) dm.VollpaketDir = Vollpaket.Locate([vp]) ?? throw new InvalidOperationException("Kein gültiges Vollpaket in " + vp);
            else dm.LocateVollpaket = VollpaketStart.Find;
            var rc = 0;
            if (Arg("--install-deps") == "vollpaket")
            {
                // genau der Weg nach dem Setup (SetupVollpaket): eigene, geprüfte Kopien im Werkzeugordner, schon installierte Programme zählen nicht
                if (dm.VollpaketDir is null && dm.LocateVollpaket?.Invoke() is { } found) dm.VollpaketDir = found;
                if (dm.VollpaketDir is null) { Console.WriteLine("FAIL  kein Vollpaket gefunden"); return 1; }
                try { Console.WriteLine($"PASS  {Vollpaket.InstallMissing(dm.VollpaketDir, dm.ToolsRoot, core.Audit, Console.WriteLine)} Pakete eingerichtet"); }
                catch (InvalidOperationException e) { Console.WriteLine("FAIL  " + e.Message); rc = 1; }
            }
            else if (Arg("--install-deps") is { } ids)
            {
                if (!dm.HasConsent)
                {
                    if (!uiArgs.Contains("--accept-licenses")) { Console.WriteLine("Lizenz-Zustimmung fehlt (--accept-licenses)"); return 3; }
                    dm.GiveConsent(Principal.LocalOwner);
                }
                var list = ids == "auto" ? DependencyCatalog.All.Where(c => c.Mode is DeliveryMode.AutoUser or DeliveryMode.Included).ToList()
                    : ids == "vollpaket" ? DependencyCatalog.All.Where(c => c.Mode == DeliveryMode.Included).ToList() : ids.Split(',').Select(DependencyCatalog.Get).ToList();
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
        if (uiArgs.Contains("--api-selftest"))
        {
            var core = new TrendoCore(Options.DataRoot ?? TrendoCore.DefaultDataRoot(), launcher: new NoopLauncher(), offline: Options.Offline);
            var req = TrendoCore.FindRequirements(AppContext.BaseDirectory) ?? throw new FileNotFoundException("requirements.yaml");
            var rc = Trendo.Api.ApiSelfTest.RunAsync(core, req, Console.Out).GetAwaiter().GetResult();
            core.Shutdown();
            return rc;
        }
        StartupTrace.LogPath = Arg("--startup-log");
        StartupTrace.Mark("avalonia.start");
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(uiArgs);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().LogToTrace();
}

/// <summary>Start nach dem Installer: Velopack-Schalter bleiben bei Velopack, die Oberfläche bekommt sie nicht.</summary>
public static class AppStartup
{
    public static bool VelopackFirstRun { get; private set; }
    public static void NoteFirstRun() => VelopackFirstRun = true;

    public static string[] WithoutVelopack(string[] args)
    {
        var list = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (a.StartsWith("--veloapp-", StringComparison.OrdinalIgnoreCase) || a.StartsWith("--velopack-", StringComparison.OrdinalIgnoreCase)
                || a.StartsWith("--squirrel-", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length && !args[i + 1].StartsWith('-')) i++;
                continue;
            }
            list.Add(a);
        }
        return list.ToArray();
    }
}

/// <summary>Launcher für den API-Selbsttest: führt nichts aus (der Test lehnt Aktionen ab).</summary>
sealed class NoopLauncher : IProcessLauncher { public void Open(string target) { } }
