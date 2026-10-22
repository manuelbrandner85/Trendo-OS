// Rendert die Oberfläche headless mit Skia in eine PNG (Design-Vergleich ohne Windows-PC).
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Rebuild;

var outPath = args.Length > 0 ? args[0] : "render.png";
int w = args.Length > 1 ? int.Parse(args[1]) : 1536, h = args.Length > 2 ? int.Parse(args[2]) : 1024;
AppBuilder.Configure<App>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).WithInterFont().SetupWithoutStarting();
var root = Path.Combine(Path.GetTempPath(), "trendo-render");
if (Directory.Exists(root)) Directory.Delete(root, true);
var core = new TrendoCore(root, new InMemorySecretStore(), new NoopL());
if (args.Length > 4) new UserSettings { Theme = args[4], SetupDone = true, Wallpaper = args.Length > 5 ? args[5] : "bergsee-nacht" }.Save(root);
if (Environment.GetEnvironmentVariable("TRENDO_DEMO_CASE") is not null)
{
    var us = UserSettings.Load(root); us.SetupDone = true; us.ProjectsFolder = Path.Combine(root, "projekte"); us.Theme = args.Length > 4 ? args[4] : "dunkel"; us.Save(root);
}
var win = new MainWindow(core, new AppOptions(root, null, 0, Page: args.Length > 3 ? args[3] : null, Query: Environment.GetEnvironmentVariable("TRENDO_QUERY")), new DefaultToolProbe()) { Width = w, Height = h, WindowState = Avalonia.Controls.WindowState.Normal };
win.Show();
if (Environment.GetEnvironmentVariable("TRENDO_DEMO_CASE") is not null)
{
    var store = new CaseStore(Path.Combine(root, "projekte"), core.Audit);
    var lh = store.SaveLetterhead(new Letterhead("", "", "privat", "Erika Muster", "Hauptstraße 1", "10115 Berlin", "erika@example.de"), Principal.LocalOwner);
    var c = store.Create("Widerspruch Rentenbescheid", "behoerde", Principal.LocalOwner); c.LetterheadId = lh.Id; c.Procedure = "Widerspruchsverfahren"; c.ProcedureConfirmed = true;
    c.Facts.Add(new("Bescheid vom 03.09.2026 liegt vor.", FactKind.Tatsache, "Bescheid")); c.Facts.Add(new("Die Beitragszeiten 2019 fehlen vermutlich.", FactKind.Vermutung));
    c.Timeline.Add(new(new DateTime(2026, 9, 5), "Bescheid erhalten"));
    c.Draft.Recipient = "Deutsche Rentenversicherung\nRuhrstraße 2\n10709 Berlin"; c.Draft.Reference = "R-123/26"; c.Draft.Place = "Berlin";
    c.Draft.Subject = "Widerspruch gegen den Bescheid vom 03.09.2026, Az. R-123/26"; c.Draft.Body = "gegen den oben genannten Bescheid lege ich Widerspruch ein. Nach § 84 SGG ist die Frist gewahrt.\n\n[Sachverhalt]";
    c.Deadlines.AddRange(LetterKit.FindDeadlines("Gegen diesen Bescheid kann innerhalb eines Monats Widerspruch erhoben werden.", "e01", new DateTime(2026, 9, 5)));
    LetterKit.Compose(c, lh); store.Save(c, Principal.LocalOwner);
    win.OpenCase(c); win.Show(args.Length > 3 ? args[3] : "akte");
}
if (Environment.GetEnvironmentVariable("TRENDO_REBUILD") is not null)
{
    win.RebuildOverride = new RebuildPorts { Web = new RenderWeb(), Author = new NoAuthor(), Build = new RenderBuild(), Packer = new RenderPack() };
    win.Show("nachbau");
    Pump(win);
    Find<TextBox>(win, "rebuild-prompt").Text = "ein Notizprogramm wie Notepad++";
    Press(Find<Button>(win, "rebuild-plan"));
    Pump(win);
    SaveFrame(win, Path.Combine(Path.GetDirectoryName(outPath) ?? ".", "nachbau-plan.png"));
    Find<CheckBox>(win, "rebuild-feat-suchen").IsChecked = false;
    Press(Find<Button>(win, "rebuild-go"));
    Pump(win);
    Press(Find<Button>(win, "rebuild-open"));
    Pump(win);
    SaveFrame(win, Path.Combine(Path.GetDirectoryName(outPath) ?? ".", "nachbau-fenster.png"));
    win.OpenStartMenu();
    Pump(win);
    SaveFrame(win, outPath);
}
else
{
    for (var i = 0; i < 30; i++) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(50); }
    SaveFrame(win, outPath);
}
Console.WriteLine($"OK {outPath}");
try { Directory.Delete(root, true); } catch (IOException) { }

static T Find<T>(Window w, string name) where T : Control =>
    w.GetLogicalDescendants().OfType<T>().First(c => c.Name == name);

static void Press(Button b)
{
    b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    Dispatcher.UIThread.RunJobs();
}

static void Pump(MainWindow w)
{
    var until = DateTime.UtcNow.AddSeconds(8);
    while (w.RebuildBusy && DateTime.UtcNow < until)
    {
        Dispatcher.UIThread.RunJobs();
        Thread.Sleep(20);
    }
    AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    Dispatcher.UIThread.RunJobs();
}

static void SaveFrame(Window w, string path)
{
    AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    Dispatcher.UIThread.RunJobs();
    var frame = w.CaptureRenderedFrame();
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    frame!.Save(path);
    Console.WriteLine("Bild " + path);
}

sealed class NoopL : IProcessLauncher { public void Open(string t) { } }

sealed class RenderWeb : IPublicWeb
{
    public bool Allowed => false;
    public IReadOnlyList<PublicPage> Research(string query) => [];
}

sealed class RenderBuild : IBuildRunner
{
    public ToolResult Run(string exe, IReadOnlyList<string> args, string cwd, int timeoutMs = 180000)
    {
        if (args.Contains("build"))
        {
            var dll = Path.Combine(cwd, "Kern", "bin", "Release", "net10.0", "RebuildKern.dll");
            Directory.CreateDirectory(Path.GetDirectoryName(dll)!);
            File.WriteAllText(dll, "platzhalter");
        }
        return new ToolResult(0, "Passed! - 3");
    }
}

sealed class RenderPack : ISetupPacker
{
    public SetupResult Pack(string root, AppBlueprint blueprint, string destDir, bool selfContained, int appVersion) =>
        new(false, null, "", false, "Die Setup-Datei ist nicht signiert.");
}
