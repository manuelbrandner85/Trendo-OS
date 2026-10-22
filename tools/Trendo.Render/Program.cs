// Rendert die Oberfläche headless mit Skia in eine PNG (Design-Vergleich ohne Windows-PC).
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
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
void Pump() { for (var n = 0; n < 8; n++) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(20); } }
if (Environment.GetEnvironmentVariable("TRENDO_BUSINESS_PROMPT") == "1")
{
    core.Projects.Create("ideeprobe", "Ideeprobe");
    win.Show("business");
    Pump();
    T Find<T>(string name) where T : Control => win.GetLogicalDescendants().OfType<T>().First(c => c.Name == name);
    Find<TextBox>("auftrag-prompt").Text = "Name: Kerzen\nProblem: Kerzen riechen im Flur zu schwach und halten nicht lange.\nZielgruppe: Haushalte\nLoesung: Duftkerzen aus Sojawachs mit langer Brenndauer im Glas.\nModell: Shop\nPreis: 20\nKosten: 5\nFix: 100\nMenge: 30\nStunden: 10\nStartgeld: 3000\nMitbewerber: 4\nKoennen: 4";
    Find<Button>("auftrag-pruefen").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    for (var i = 0; i < 40 && Find<TextBlock>("auftrag-status").Text != "Plan steht"; i++) Pump();
    Find<Button>("auftrag-los").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    for (var i = 0; i < 40 && Find<TextBlock>("auftrag-status").Text != "QA bestanden"; i++) Pump();
    if (Find<TextBlock>("auftrag-status").Text != "QA bestanden")
        throw new InvalidOperationException("Geschäftsidee steht auf " + Find<TextBlock>("auftrag-status").Text + " / " + Find<TextBlock>("auftrag-vorschau").Text);
}
if (Environment.GetEnvironmentVariable("TRENDO_MONEY_PROMPT") == "1")
{
    core.Projects.Create("geldprobe", "Geldprobe");
    win.Show("finanzen");
    Pump();
    T Find<T>(string name) where T : Control => win.GetLogicalDescendants().OfType<T>().First(c => c.Name == name);
    Find<TextBox>("auftrag-prompt").Text = "Verkaeufer: Anna Beispiel\nKaeufer: Bruno Beispiel\nPosition: Beratung | 2 | 10\nPosition: Fahrt | 1 | 5,50\nMwSt: 19\nDatum: 2026-10-22";
    Find<Button>("auftrag-pruefen").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    for (var i = 0; i < 40 && Find<TextBlock>("auftrag-status").Text != "Plan steht"; i++) Pump();
    Find<Button>("auftrag-los").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    for (var i = 0; i < 40 && Find<TextBlock>("auftrag-status").Text != "QA bestanden"; i++) Pump();
    if (Find<TextBlock>("auftrag-status").Text != "QA bestanden")
        throw new InvalidOperationException("Rechnung steht auf " + Find<TextBlock>("auftrag-status").Text + " / " + Find<TextBlock>("auftrag-vorschau").Text);
}
if (Environment.GetEnvironmentVariable("TRENDO_MAIL_PROMPT") == "1")
{
    core.Projects.Create("postprobe", "Postprobe");
    win.Show("post");
    Pump();
    T Find<T>(string name) where T : Control => win.GetLogicalDescendants().OfType<T>().First(c => c.Name == name);
    Find<TextBox>("auftrag-prompt").Text = "An: a@b.de\nBetreff: Hallo\nText: Bitte um Rückruf.";
    Find<Button>("auftrag-pruefen").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    for (var i = 0; i < 40 && Find<TextBlock>("auftrag-status").Text != "Plan steht"; i++) Pump();
    Find<Button>("auftrag-los").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    for (var i = 0; i < 40 && Find<TextBlock>("auftrag-status").Text != "QA bestanden"; i++) Pump();
    if (Find<TextBlock>("auftrag-status").Text != "QA bestanden")
        throw new InvalidOperationException("E-Mail steht auf " + Find<TextBlock>("auftrag-status").Text + " / " + Find<TextBlock>("auftrag-vorschau").Text);
}
if (Environment.GetEnvironmentVariable("TRENDO_OFFICE_PROMPT") == "1")
{
    core.Projects.Create("tabellenprobe", "Tabellenprobe");
    win.Show("office");
    Pump();
    T Find<T>(string name) where T : Control => win.GetLogicalDescendants().OfType<T>().First(c => c.Name == name);
    Find<TextBox>("auftrag-prompt").Text = "Zelle: A1=Posten\nZelle: B1=Betrag\nZelle: A2=Papier\nZelle: B2=12,50\nZelle: A3=Stift\nZelle: B3=3,50\nZelle: A4=Summe\nZelle: B4=SUMME(B2:B3)";
    Find<Button>("auftrag-pruefen").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    for (var i = 0; i < 40 && Find<TextBlock>("auftrag-status").Text != "Plan steht"; i++) Pump();
    Find<Button>("auftrag-los").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    for (var i = 0; i < 40 && Find<TextBlock>("auftrag-status").Text != "QA bestanden"; i++) Pump();
    if (Find<TextBlock>("auftrag-status").Text != "QA bestanden")
        throw new InvalidOperationException("Tabelle steht auf " + Find<TextBlock>("auftrag-status").Text + " / " + Find<TextBlock>("auftrag-detail").Text);
}
if (Environment.GetEnvironmentVariable("TRENDO_BRIEF_PROMPT") == "1")
{
    core.Projects.Create("briefprobe", "Briefprobe");
    win.Show("brief");
    Pump();
    T Find<T>(string name) where T : Control => win.GetLogicalDescendants().OfType<T>().First(c => c.Name == name);
    Find<TextBox>("auftrag-prompt").Text = "Absender: Anna Beispiel\nStrasse: Beispielweg 1\nOrt: 10115 Berlin\nEmpfaenger: Amt für Beispiel\nMusterstraße 2\n10115 Berlin\nBetreff: Auskunft zu den Unterlagen\nText: bitte senden sie mir die unterlagen zu.\nDatum: 2026-10-22";
    Find<Button>("auftrag-pruefen").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    for (var i = 0; i < 40 && Find<TextBlock>("auftrag-status").Text != "Plan steht"; i++) Pump();
    Find<Button>("auftrag-los").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    for (var i = 0; i < 40 && Find<TextBlock>("auftrag-status").Text != "QA bestanden"; i++) Pump();
    if (Find<TextBlock>("auftrag-status").Text != "QA bestanden")
        throw new InvalidOperationException("Briefvorschau steht auf " + Find<TextBlock>("auftrag-status").Text);
}
for (var i = 0; i < 30; i++) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(50); }
var frame = win.CaptureRenderedFrame();
frame!.Save(outPath);
Console.WriteLine($"OK {outPath}");
Directory.Delete(root, true);
sealed class NoopL : IProcessLauncher { public void Open(string t) { } }
