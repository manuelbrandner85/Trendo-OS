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
var shot = Environment.GetEnvironmentVariable("TRENDO_SHOT");
AppBuilder.Configure<App>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).WithInterFont().SetupWithoutStarting();
if (Environment.GetEnvironmentVariable("TRENDO_LOGO") is { Length: > 0 } logoDir) // Logo C „Gold-Orbit“: Emblem aus der Vorlage, PNGs und trendo.ico
{
    Directory.CreateDirectory(logoDir);
    var emblemFile = Path.Combine(logoDir, "trendo-emblem.png");
    // 1. Emblem aus der fotorealistischen Vorlage ausschneiden (TRENDO_LOGO_SRC, sonst die Vorlage im Repo)
    var src = Environment.GetEnvironmentVariable("TRENDO_LOGO_SRC") is { Length: > 0 } s0 ? s0 : Path.Combine(logoDir, "..", "..", "..", "..", "docs", "design", "logo-vorschlaege", "gold-orbit-vorlage.png");
    if (File.Exists(src))
    {
        using var source = new Avalonia.Media.Imaging.Bitmap(src);
        var cut = TrendoLogo.EmblemFrom(source); var cw = TrendoLogo.SourceCrop.Width; var chh = TrendoLogo.SourceCrop.Height;
        cut.Measure(new Avalonia.Size(cw, chh)); cut.Arrange(new Avalonia.Rect(0, 0, cw, chh));
        using var eb = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(cw, chh)); eb.Render(cut); eb.Save(emblemFile);
    }
    using var emblem = File.Exists(emblemFile) ? new Avalonia.Media.Imaging.Bitmap(emblemFile) : null;
    byte[] Png(int px)
    {
        var box = new Avalonia.Controls.Viewbox { Width = px, Height = px, Child = TrendoLogo.Build(emblem: emblem) };
        box.Measure(new Avalonia.Size(px, px)); box.Arrange(new Avalonia.Rect(0, 0, px, px));
        using var bmp = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(px, px));
        bmp.Render(box);
        using var ms = new MemoryStream(); bmp.Save(ms); return ms.ToArray();
    }
    foreach (var px in new[] { 32, 256, 512 }) File.WriteAllBytes(Path.Combine(logoDir, $"trendo-{px}.png"), Png(px));
    int[] icoSizes = [16, 24, 32, 48, 64, 128, 256];
    var pngs = icoSizes.Select(Png).ToList();
    using (var ico = new BinaryWriter(File.Create(Path.Combine(logoDir, "trendo.ico"))))
    {
        ico.Write((ushort)0); ico.Write((ushort)1); ico.Write((ushort)icoSizes.Length);
        var offset = 6 + 16 * icoSizes.Length;
        for (var i = 0; i < icoSizes.Length; i++)
        {
            var s = icoSizes[i] >= 256 ? (byte)0 : (byte)icoSizes[i];
            ico.Write(s); ico.Write(s); ico.Write((byte)0); ico.Write((byte)0); ico.Write((ushort)1); ico.Write((ushort)32);
            ico.Write(pngs[i].Length); ico.Write(offset); offset += pngs[i].Length;
        }
        foreach (var p in pngs) ico.Write(p);
    }
    Console.WriteLine("Logo geschrieben: " + logoDir);
    return;
}
var root = Path.Combine(Path.GetTempPath(), "trendo-render");
if (Directory.Exists(root)) Directory.Delete(root, true);
if (shot is "brief" or "fenster" or "seite")
{
    Directory.CreateDirectory(root);
    var prepared = UserSettings.Load(root);
    prepared.SetupDone = true;
    foreach (var hint in MainWindow.HelpTexts.Keys) if (!prepared.SeenHints.Contains(hint)) prepared.SeenHints.Add(hint);
    if (shot == "seite") prepared.ReduceMotion = true;
    if (shot == "brief")
    {
        prepared.Name = "Erika Muster"; prepared.SenderStreet = "Hauptstraße 1"; prepared.SenderCity = "10115 Berlin"; prepared.SenderContact = "erika@example.de";
        prepared.ProjectsFolder = Path.Combine(root, "projekte");
    }
    prepared.Save(root);
}
var core = new TrendoCore(root, new InMemorySecretStore(), new NoopL());
if (args.Length > 4) new UserSettings { Theme = args[4], SetupDone = true, Wallpaper = args.Length > 5 ? args[5] : "abendrot" }.Save(root);
if (Environment.GetEnvironmentVariable("TRENDO_DEMO_CASE") is not null)
{
    var us = UserSettings.Load(root); us.SetupDone = true; us.ProjectsFolder = Path.Combine(root, "projekte"); us.Theme = args.Length > 4 ? args[4] : "dunkel"; us.Save(root);
}
if (Environment.GetEnvironmentVariable("TRENDO_TV_DEMO") is { Length: > 0 }) // TV & Live mit erfundenen Beispiel-Sendern rendern
{
    var lib = new Trendo.Core.Tv.TvLibrary(root);
    var src = lib.AddSource("Beispiel", "url", "https://tv.example/liste.m3u");
    var sb = new System.Text.StringBuilder("#EXTM3U\n");
    string[] groups = ["Nachrichten", "Unterhaltung", "Sport", "Kinder", "Doku"];
    for (var i = 1; i <= 24; i++) sb.Append($"#EXTINF:-1 tvg-id=\"k{i}\" tvg-chno=\"{i}\" group-title=\"{groups[i % 5]}\",Kanal {i} {groups[i % 5]}\nhttps://tv.example/{i}.m3u8\n");
    foreach (var (f, g) in new[] { ("Der lange Weg", "Abenteuer"), ("Sternenstaub", "Science-Fiction"), ("Kleine Freunde", "Familie"), ("Nordlicht", "Drama"), ("Die Brücke", "Thriller"), ("Sommerwind", "Komödie"), ("Tiefsee", "Doku"), ("Gipfelsturm", "Abenteuer") })
        sb.Append($"#EXTINF:-1 group-title=\"{g}\",{f}\nhttps://tv.example/movie/u/p/{f.Length}.mkv\n");
    foreach (var s in new[] { "Hafenkommissarin", "Bergdoktorin", "Zeitreisende" }) for (var st = 1; st <= 2; st++) for (var e = 1; e <= 4; e++)
        sb.Append($"#EXTINF:-1 group-title=\"Serien\",{s} S0{st} E0{e}\nhttps://tv.example/series/u/p/{s.Length}{st}{e}.mkv\n");
    lib.StorePlaylist(src.Id, sb.ToString());
    var now = DateTime.UtcNow; var progs = new List<Trendo.Core.Tv.TvProgramme>();
    string[] titles = ["Tagesbericht", "Quizabend", "Fußball live", "Abenteuer im Wald", "Wildes Afrika", "Wetter", "Talk um acht", "Spätnachrichten"];
    for (var i = 1; i <= 24; i++) { var t = now.Date.AddHours(now.Hour - 1).AddMinutes(-(i * 7 % 30)); for (var k = 0; k < 6; k++) { progs.Add(new($"k{i}", t, t.AddMinutes(45 + i % 4 * 15), titles[(i + k) % titles.Length], "Beispielsendung")); t = t.AddMinutes(45 + i % 4 * 15); } }
    lib.StoreEpg(src.Id, progs);
}
var win = new MainWindow(core, new AppOptions(root, null, 0, Page: args.Length > 3 ? args[3] : null, Query: Environment.GetEnvironmentVariable("TRENDO_QUERY")), new DefaultToolProbe()) { Width = w, Height = h, WindowState = Avalonia.Controls.WindowState.Normal };
if (Environment.GetEnvironmentVariable("TRENDO_TV_TAB") is { Length: > 0 } tvTab) { win.TvTabForTests = tvTab; win.Show("tv"); }
win.Show();
if (Environment.GetEnvironmentVariable("TRENDO_PDF") is { Length: > 0 } pdf) { var copy = Path.Combine(root, Path.GetFileName(pdf)); File.Copy(pdf, copy, true); win.OpenPdf(copy); win.Show("pdf"); } // T-0223: Trendo PDF rendern
if (Environment.GetEnvironmentVariable("TRENDO_SHEET") is { Length: > 0 } mappe) { win.SheetNew(mappe); if (Enum.TryParse<Trendo.Core.Office.ChartKind>(Environment.GetEnvironmentVariable("TRENDO_SHEET_CHART"), out var chartKind)) { win.SheetSelect("A1:C7"); win.SheetInsertChart(chartKind); } if (Environment.GetEnvironmentVariable("TRENDO_SHEET_EXTRAS") == "1") { win.SheetSelect("C2:C7"); win.SheetAddCond(Trendo.Core.Office.CondKind.Datenbalken); win.SheetSelect("C9:C11"); win.SheetAddCond(Trendo.Core.Office.CondKind.Farbskala); win.SheetSelect("B2:B7"); win.SheetAddCond(Trendo.Core.Office.CondKind.Regel, Trendo.Core.Office.CondOp.Enthaelt, text: "Einnahme"); win.SheetSetValidation("Einnahme; Ausgabe"); win.SheetSelect("A2"); win.SheetSetNote("Netto, ohne Weihnachtsgeld"); } win.SheetSelect("C2:C7"); if (Environment.GetEnvironmentVariable("TRENDO_SHEET_PDF") is { Length: > 0 } sheetPdf) win.SheetExportPdf(sheetPdf); win.Show("tabelle"); } // T-0228: Trendo Tabelle mit Vorlage rendern
if (Environment.GetEnvironmentVariable("TRENDO_DECK") is { Length: > 0 } deck) { win.DeckNew(deck); if (Environment.GetEnvironmentVariable("TRENDO_DECK_SLIDE") is { } ds && int.TryParse(ds, out var dsi)) win.DeckGo(dsi); if (Environment.GetEnvironmentVariable("TRENDO_DECK_PNG") is { Length: > 0 } pngDir) win.DeckExportImages(pngDir); win.Show("praesentation"); } // T-0229: Trendo Präsentation mit Vorlage rendern
if (Environment.GetEnvironmentVariable("TRENDO_TEXT") is { Length: > 0 } vorlage) { win.TextNew(vorlage); if (Environment.GetEnvironmentVariable("TRENDO_TEXT_RIBBON") is { Length: > 0 } rib) { win.TextRibbon = rib; win.TextSetPage(pg => { pg.Watermark = "ENTWURF"; pg.Header = "Erika Muster"; pg.PageNumbers = true; pg.Design = "klassisch"; }); win.TextSelect(1, 0, 5); win.TextAddMark(Trendo.Core.Office.MarkKind.Comment, "Bitte prüfen"); win.TextSelect(1, 6, 0); win.TextAddMark(Trendo.Core.Office.MarkKind.Footnote, "Quelle"); win.TextInsertToc(); } win.TextSelect(1, 0, 0); win.Show("text"); } // T-0223: Trendo Text mit Vorlage rendern
if (shot == "einrichtung") win.ShowSetupOverlay("Trendo OS richtet deine Programme ein", 12, 20, "Krita 5.2.9 wird eingerichtet …"); // Vollbild-Einrichtung
else if (shot == "startmenu")
{
    foreach (var id in new[] { "STUDIO-LETTER-01", "CORE-05", "CORE-08", "CORE-06", "CORE-02" }) win.PinStudio(id);
    win.Show("start"); win.OpenStartMenu();
}
else if (shot == "fenster")
{
    win.Show("brief"); var brief = win.Windows.Active!.Id;
    win.Show("spiele"); var spiele = win.Windows.Active!.Id;
    win.Show("3d"); var d3 = win.Windows.Active!.Id;
    for (var i = 0; i < 10; i++) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(20); }
    win.UpdateLayout();
    var aw = win.Windows.AreaW; var ah = win.Windows.AreaH;
    if (aw > 400 && ah > 300)
    {
        void Place(string id, double x, double y, double ww, double hh)
        {
            var dw = win.Windows.Get(id); if (dw is null) return;
            dw.Maximized = false; dw.Moved = true; dw.X = x; dw.Y = y; dw.W = ww; dw.H = hh; win.Windows.Clamp(dw);
        }
        var ww = Math.Max(360, aw * 0.72); var hh = Math.Max(240, ah * 0.72);
        Place(brief, 12, 8, ww, hh);
        Place(spiele, aw * 0.16, ah * 0.12, ww, hh);
        Place(d3, aw * 0.30, ah * 0.22, ww, hh);
        win.Show(win.Windows.Get(d3)!.Page);
    }
}
else if (shot == "seite")
{
    var page = Environment.GetEnvironmentVariable("TRENDO_STUDIO") is { Length: > 0 } sid ? win.PageForStudio(sid) ?? "studios" : args.Length > 3 ? args[3] : "studios"; // Studio per Katalog-Id
    win.Show(page);
    if (win.Windows.Active is { Maximized: false } pageWin) win.Windows.ToggleMaximize(pageWin.Id);
    win.Show(page);
    if (Environment.GetEnvironmentVariable("TRENDO_PROGTPL") is { Length: > 0 } tpl) { win.ProgramNew(page, tpl); win.Show(page); } // Programm mit Vorlage öffnen
    if (page == "architektur" && Environment.GetEnvironmentVariable("TRENDO_ARCH") is { Length: > 0 } arch) // Grundriss: Auswahl/3D/Werkzeug zeigen
    {
        var plan = Trendo.Core.Arch.FloorPlan.From(win.ProgramActive(page)!.Data);
        if (arch.Contains("raum")) win.ArchState.Selected = plan.Rooms[0].Id;
        if (arch.Contains("moebel")) win.ArchState.Tool = "moebel";
        if (arch.Contains("plus")) win.ArchEdit("Beispiel", pl => { pl.AddDimension(-12, 736, 884, 736, 70); pl.AddLabel(250, 300, "Fernsehecke"); });
        if (arch.Contains("3d")) win.ArchToggle3D();
        win.Show(page);
    }
    if (page == "ki-bibliothek" && Enum.TryParse<KiKind>(Environment.GetEnvironmentVariable("TRENDO_KIKIND"), out var kind)) win.KiLibraryShow(kind); // Art der KI-Bibliothek
}
else if (shot == "brief")
{
    var us = UserSettings.Load(root);
    us.SetupDone = true; us.Name = "Erika Muster"; us.SenderStreet = "Hauptstraße 1"; us.SenderCity = "10115 Berlin"; us.SenderContact = "erika@example.de";
    us.ProjectsFolder = Path.Combine(root, "projekte"); us.Save(root);
    win.Show("brief");
    var d = win.Letter;
    d.SenderName = "Erika Muster"; d.SenderStreet = "Hauptstraße 1"; d.SenderCity = "10115 Berlin"; d.SenderContact = "erika@example.de";
    d.Place = "Berlin"; d.Recipient = "Fitness AG\nAm Park 2\n50667 Köln";
    d.Subject = "Kündigung der Mitgliedschaft 123"; d.Salutation = "Sehr geehrte Damen und Herren,";
    d.Body = "hiermit kündige ich die Mitgliedschaft 123 zum nächstmöglichen Zeitpunkt.\n\nBitte bestätigen Sie mir den Eingang und das Ende der Mitgliedschaft schriftlich.";
    d.Closing = "Mit freundlichen Grüßen";
    win.Show("brief");
    if (win.Windows.Active is { Maximized: false } briefWin) win.Windows.ToggleMaximize(briefWin.Id);
    win.Show("brief");
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
void Pump() { for (var n = 0; n < 8; n++) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(20); } }
if (Environment.GetEnvironmentVariable("TRENDO_PAGE_PROMPT") == "1")
{
    core.Projects.Create("seitenprobe", "Seitenprobe");
    win.Show("baukasten");
    Pump();
    T Find<T>(string name) where T : Control => win.GetLogicalDescendants().OfType<T>().First(c => c.Name == name);
    Find<TextBox>("auftrag-prompt").Text = "Titel: Hofseite\nText: Willkommen im Hof";
    Find<Button>("auftrag-pruefen").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    for (var i = 0; i < 40 && Find<TextBlock>("auftrag-status").Text != "Plan steht"; i++) Pump();
    Find<Button>("auftrag-los").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    for (var i = 0; i < 40 && Find<TextBlock>("auftrag-status").Text != "QA bestanden"; i++) Pump();
    if (Find<TextBlock>("auftrag-status").Text != "QA bestanden")
        throw new InvalidOperationException("Baukasten steht auf " + Find<TextBlock>("auftrag-status").Text + " / " + Find<TextBlock>("auftrag-vorschau").Text);
    if (Find<TextBox>("builder-title").Text != "Hofseite")
        throw new InvalidOperationException("Das Formular zeigt „" + Find<TextBox>("builder-title").Text + "“ statt Hofseite.");
}
if (Environment.GetEnvironmentVariable("TRENDO_LEARN_PROMPT") == "1")
{
    core.Projects.Create("lernprobe", "Lernprobe");
    win.Show("lernen");
    Pump();
    T Find<T>(string name) where T : Control => win.GetLogicalDescendants().OfType<T>().First(c => c.Name == name);
    Find<TextBox>("auftrag-prompt").Text = "Stapel: Geografie\nFrage: Hauptstadt von Frankreich\nAntwort: Paris";
    Find<Button>("auftrag-pruefen").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    for (var i = 0; i < 40 && Find<TextBlock>("auftrag-status").Text != "Plan steht"; i++) Pump();
    Find<Button>("auftrag-los").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    for (var i = 0; i < 40 && Find<TextBlock>("auftrag-status").Text != "QA bestanden"; i++) Pump();
    if (Find<TextBlock>("auftrag-status").Text != "QA bestanden")
        throw new InvalidOperationException("Lernen steht auf " + Find<TextBlock>("auftrag-status").Text + " / " + Find<TextBlock>("auftrag-vorschau").Text);
}
if (Environment.GetEnvironmentVariable("TRENDO_ROOM_PROMPT") == "1")
{
    core.Projects.Create("raumprobe", "Raumprobe");
    win.Show("architektur");
    Pump();
    T Find<T>(string name) where T : Control => win.GetLogicalDescendants().OfType<T>().First(c => c.Name == name);
    Find<TextBox>("auftrag-prompt").Text = "Name: Wohnzimmer\nLaenge: 4\nBreite: 3";
    Find<Button>("auftrag-pruefen").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    for (var i = 0; i < 40 && Find<TextBlock>("auftrag-status").Text != "Plan steht"; i++) Pump();
    Find<Button>("auftrag-los").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    for (var i = 0; i < 40 && Find<TextBlock>("auftrag-status").Text != "QA bestanden"; i++) Pump();
    if (Find<TextBlock>("auftrag-status").Text != "QA bestanden")
        throw new InvalidOperationException("Wohnung steht auf " + Find<TextBlock>("auftrag-status").Text + " / " + Find<TextBlock>("auftrag-vorschau").Text);
}
if (Environment.GetEnvironmentVariable("TRENDO_MEDIA_PROMPT") == "1")
{
    var project = core.Projects.Create("medienprobe", "Medienprobe");
    var png = Mesh3D.RenderPng(new Scene3D { Name = "Quelle", Parts = [new Part3D { Shape = "cube", Sx = 8, Sy = 8, Sz = 8, Color = "#2F86F6" }] }, "front", 64, 48)
        ?? throw new InvalidOperationException("Keine Bildquelle.");
    var source = core.Projects.ResolveInProject(project.Id, "quelle.png");
    Directory.CreateDirectory(Path.GetDirectoryName(source)!);
    File.WriteAllBytes(source, png);
    win.Show("kreativ-medien");
    Pump();
    T Find<T>(string name) where T : Control => win.GetLogicalDescendants().OfType<T>().First(c => c.Name == name);
    Find<TextBox>("auftrag-prompt").Text = "Datei: quelle.png\nAufgabe: Bild";
    Find<Button>("auftrag-pruefen").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    for (var i = 0; i < 40 && Find<TextBlock>("auftrag-status").Text != "Plan steht"; i++) Pump();
    Find<Button>("auftrag-los").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    for (var i = 0; i < 80 && Find<TextBlock>("auftrag-status").Text != "QA bestanden"; i++) Pump();
    if (Find<TextBlock>("auftrag-status").Text != "QA bestanden")
        throw new InvalidOperationException("Medien steht auf " + Find<TextBlock>("auftrag-status").Text + " / " + Find<TextBlock>("auftrag-detail").Text);
}
if (Environment.GetEnvironmentVariable("TRENDO_GAME_PROMPT") == "1")
{
    core.Projects.Create("spielprobe", "Spielprobe");
    win.Show("spiele");
    Pump();
    T Find<T>(string name) where T : Control => win.GetLogicalDescendants().OfType<T>().First(c => c.Name == name);
    Find<TextBox>("auftrag-prompt").Text = "Vorlage: Schlange\nName: Hofspiel";
    Find<Button>("auftrag-pruefen").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    for (var i = 0; i < 40 && Find<TextBlock>("auftrag-status").Text != "Plan steht"; i++) Pump();
    Find<Button>("auftrag-los").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    for (var i = 0; i < 40 && Find<TextBlock>("auftrag-status").Text != "QA bestanden"; i++) Pump();
    if (Find<TextBlock>("auftrag-status").Text != "QA bestanden")
        throw new InvalidOperationException("Spiel steht auf " + Find<TextBlock>("auftrag-status").Text + " / " + Find<TextBlock>("auftrag-vorschau").Text);
}
if (Environment.GetEnvironmentVariable("TRENDO_3D_PROMPT") == "1")
{
    core.Projects.Create("modellprobe", "Modellprobe");
    win.Show("3d");
    Pump();
    T Find<T>(string name) where T : Control => win.GetLogicalDescendants().OfType<T>().First(c => c.Name == name);
    Find<TextBox>("auftrag-prompt").Text = "Name: Klotz\nForm: Wuerfel\nBreite: 10\nTiefe: 10\nHoehe: 10\nFarbe: Blau";
    Find<Button>("auftrag-pruefen").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    for (var i = 0; i < 40 && Find<TextBlock>("auftrag-status").Text != "Plan steht"; i++) Pump();
    Find<Button>("auftrag-los").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    for (var i = 0; i < 40 && Find<TextBlock>("auftrag-status").Text != "QA bestanden"; i++) Pump();
    if (Find<TextBlock>("auftrag-status").Text != "QA bestanden")
        throw new InvalidOperationException("3D steht auf " + Find<TextBlock>("auftrag-status").Text + " / " + Find<TextBlock>("auftrag-vorschau").Text);
}
if (Environment.GetEnvironmentVariable("TRENDO_SOFTWARE_PROMPT") == "1")
{
    core.Projects.Create("codeprobe", "Codeprobe");
    win.Show("dev");
    Pump();
    T Find<T>(string name) where T : Control => win.GetLogicalDescendants().OfType<T>().First(c => c.Name == name);
    Find<TextBox>("auftrag-prompt").Text = "Vorlage: Webseite\nName: Kerzenladen";
    Find<Button>("auftrag-pruefen").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    for (var i = 0; i < 40 && Find<TextBlock>("auftrag-status").Text != "Plan steht"; i++) Pump();
    Find<Button>("auftrag-los").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    for (var i = 0; i < 40 && Find<TextBlock>("auftrag-status").Text != "QA bestanden"; i++) Pump();
    if (Find<TextBlock>("auftrag-status").Text != "QA bestanden")
        throw new InvalidOperationException("Software steht auf " + Find<TextBlock>("auftrag-status").Text + " / " + Find<TextBlock>("auftrag-vorschau").Text);
}
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
if (Environment.GetEnvironmentVariable("TRENDO_PHONE_AUDIT_ALL") == "1") // jede App auf schmalem und großem Handy prüfen (echte Schrift)
{
    for (var i = 0; i < 10; i++) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(30); }
    var lines = new List<string>();
    foreach (var app in win.PhoneApps())
    {
        var pg = app.Id.StartsWith("page:") ? app.Id[5..] : win.StudioLaunchPage(app.Id);
        foreach (var pw in new[] { 360, 430 })
        {
            win.PresentPhoneForTests(pg);
            for (var i = 0; i < 3; i++) { Dispatcher.UIThread.RunJobs(); win.PhoneScreen(pw, 780, 1, false); }
            foreach (var b in win.PhoneOverflow(pw).Distinct().Take(5)) lines.Add($"{app.Name} [{pg}] @{pw}: {b}");
        }
    }
    File.WriteAllLines(outPath, lines);
    Console.WriteLine($"OK {lines.Count} Stellen → {outPath}");
    Directory.Delete(root, true);
    return;
}
if (Environment.GetEnvironmentVariable("TRENDO_PHONE") is { Length: > 0 } phoneApp) // Handy-App: genau das Bild, das das Handy bekommt (412 × 780, 2,6-fach)
{
    for (var i = 0; i < 10; i++) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(30); }
    win.PresentPhoneForTests(phoneApp); // Seitenschlüssel, z. B. „brief“
    for (var i = 0; i < 20; i++) { Dispatcher.UIThread.RunJobs(); win.PhoneScreen(412, 780, 2.6, false); Thread.Sleep(30); }
    File.WriteAllBytes(outPath, win.PhoneScreen(412, 780, double.TryParse(Environment.GetEnvironmentVariable("TRENDO_PHONE_SCALE"), System.Globalization.CultureInfo.InvariantCulture, out var psc) ? psc : 2.6, false)!.Png);
    if (Environment.GetEnvironmentVariable("TRENDO_PHONE_AUDIT") == "1") foreach (var b in win.PhoneOverflow(412)) Console.WriteLine("ÜBERLAUF " + b);
    if (Environment.GetEnvironmentVariable("TRENDO_PHONE_DEBUG") is { Length: > 0 } dbg)
    {
        var surf = win.GetLogicalDescendants().OfType<Control>().First(c => c.Name == "phone-surface");
        var target = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(surf).OfType<Control>().First(c => c.Name == dbg);
        foreach (var v in Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(target).OfType<Control>())
            Console.WriteLine($"  KIND {v.GetType().Name} {v.Name} {v.Bounds} {(v is TextBlock tt ? tt.Text + " natural=" + MainWindow.NaturalWidth(tt) + " wrap=" + tt.TextWrapping + " trim=" + tt.TextTrimming + " desired=" + tt.DesiredSize : "")}");
        foreach (var v in Avalonia.VisualTree.VisualExtensions.GetSelfAndVisualAncestors(target).OfType<Control>().TakeWhile(c => c != surf))
            Console.WriteLine($"{v.GetType().Name} {v.Name} {v.Bounds} RT={v.RenderTransform} {(v is LayoutTransformControl l ? "LT=" + l.LayoutTransform : "")}");
    }
    Console.WriteLine($"OK {outPath}");
    Directory.Delete(root, true);
    return;
}
for (var i = 0; i < 40; i++) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(50); }
var frame = win.CaptureRenderedFrame();
if (Environment.GetEnvironmentVariable("TRENDO_DUMP") is { Length: > 0 } dump) // Lage benannter Bausteine ausgeben (Layout-Fehlersuche)
    foreach (var c in Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(win).OfType<Control>().Where(c => c.Name is { } n && dump.Split(',').Any(n.StartsWith)))
        Console.WriteLine($"{c.Name}: {c.Bounds} desired={c.DesiredSize} @ {c.TranslatePoint(new Point(0, 0), win)}");
frame!.Save(outPath);
Console.WriteLine($"OK {outPath}");
foreach (var crash in CrashLog.List(root).Take(1)) Console.Error.WriteLine("ABSTURZ beim Rendern:" + Environment.NewLine + File.ReadAllText(crash));
Directory.Delete(root, true);
sealed class NoopL : IProcessLauncher { public void Open(string t) { } }
