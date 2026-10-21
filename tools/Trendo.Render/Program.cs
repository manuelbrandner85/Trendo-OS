// Rendert die Oberfläche headless mit Skia in eine PNG (Design-Vergleich ohne Windows-PC).
using Avalonia;
using Avalonia.Headless;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;

var outPath = args.Length > 0 ? args[0] : "render.png";
int w = args.Length > 1 ? int.Parse(args[1]) : 1536, h = args.Length > 2 ? int.Parse(args[2]) : 1024;
AppBuilder.Configure<App>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).WithInterFont().SetupWithoutStarting();
var root = Path.Combine(Path.GetTempPath(), "trendo-render");
if (Directory.Exists(root)) Directory.Delete(root, true);
var core = new TrendoCore(root, new InMemorySecretStore(), new NoopL());
if (args.Length > 4) new UserSettings { Theme = args[4], SetupDone = true, Wallpaper = args.Length > 5 ? args[5] : "bergsee-nacht" }.Save(root);
var win = new MainWindow(core, new AppOptions(root, null, 0, Page: args.Length > 3 ? args[3] : null, Query: Environment.GetEnvironmentVariable("TRENDO_QUERY")), new DefaultToolProbe()) { Width = w, Height = h, WindowState = Avalonia.Controls.WindowState.Normal };
win.Show();
for (var i = 0; i < 30; i++) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(50); }
var frame = win.CaptureRenderedFrame();
frame!.Save(outPath);
Console.WriteLine($"OK {outPath}");
Directory.Delete(root, true);
sealed class NoopL : IProcessLauncher { public void Open(string t) { } }
