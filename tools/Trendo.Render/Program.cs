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
if (Environment.GetEnvironmentVariable("TRENDO_DEMO_CASE") is not null)
{
    var us = UserSettings.Load(root); us.SetupDone = true; us.ProjectsFolder = Path.Combine(root, "projekte"); us.Theme = args.Length > 4 ? args[4] : "dunkel"; us.Save(root);
}
var win = new MainWindow(core, new AppOptions(root, null, 0, Page: args.Length > 3 ? args[3] : null, Query: Environment.GetEnvironmentVariable("TRENDO_QUERY")), new DefaultToolProbe()) { Width = w, Height = h, WindowState = Avalonia.Controls.WindowState.Normal };
win.Show();
var shot = Environment.GetEnvironmentVariable("TRENDO_SHOT");
if (shot == "startmenu") { win.Show("start"); win.OpenStartMenu(); }
else if (shot == "fenster")
{
    win.Show("brief"); var brief = win.Windows.Active!.Id;
    win.Show("spiele"); var spiele = win.Windows.Active!.Id;
    win.Show("3d"); var d3 = win.Windows.Active!.Id;
    for (var i = 0; i < 10; i++) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(20); }
    win.UpdateLayout();
    var aw = win.Windows.AreaW; var ah = win.Windows.AreaH;
    if (aw > 700 && ah > 500)
    {
        var col = (aw - 28) / 2; var row = (ah - 20) / 2;
        void Place(string id, double x, double y, double ww, double hh)
        {
            var dw = win.Windows.Get(id); if (dw is null) return;
            dw.Maximized = false; dw.Moved = true; dw.X = x; dw.Y = y; dw.W = ww; dw.H = hh; win.Windows.Clamp(dw);
        }
        Place(brief, 8, 8, col, row); Place(spiele, col + 16, 8, col, row); Place(d3, 8, row + 12, col, row);
        win.Show(win.Windows.Get(d3)!.Page);
    }
}
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
for (var i = 0; i < 30; i++) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(50); }
var frame = win.CaptureRenderedFrame();
frame!.Save(outPath);
Console.WriteLine($"OK {outPath}");
Directory.Delete(root, true);
sealed class NoopL : IProcessLauncher { public void Open(string t) { } }
