using Avalonia;
using Trendo.Core;

namespace Trendo.App;

public sealed record AppOptions(string? DataRoot, string? ScreenshotPath, int AutoExitSeconds, bool Offline = false, int? ApiPort = null);

class Program
{
    public static AppOptions Options { get; private set; } = new(null, null, 0);

    [STAThread]
    public static int Main(string[] args)
    {
        string? Arg(string n) { var i = Array.IndexOf(args, n); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
        Options = new AppOptions(Arg("--data-root"), Arg("--screenshot"),
            int.TryParse(Arg("--auto-exit"), out var s) ? s : (args.Contains("--smoke") ? 5 : 0),
            args.Contains("--offline"),
            args.Contains("--api") ? (int.TryParse(Arg("--api-port"), out var ap) ? ap : 47800) : null);

        if (args.Contains("--selftest-open-explorer"))
        {
            var core = new TrendoCore(Options.DataRoot ?? TrendoCore.DefaultDataRoot(), offline: Options.Offline);
            return SelfTest.OpenExplorer(core, Arg("--approver") ?? "operator-cli", Console.Out);
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
