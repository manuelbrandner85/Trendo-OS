using Avalonia;

namespace Trendo.App;

public sealed record AppOptions(string? DataRoot, string? ScreenshotPath, int AutoExitSeconds);

class Program
{
    public static AppOptions Options { get; private set; } = new(null, null, 0);

    [STAThread]
    public static int Main(string[] args)
    {
        string? Arg(string n) { var i = Array.IndexOf(args, n); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
        Options = new AppOptions(Arg("--data-root"), Arg("--screenshot"),
            int.TryParse(Arg("--auto-exit"), out var s) ? s : (args.Contains("--smoke") ? 5 : 0));
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().LogToTrace();
}
