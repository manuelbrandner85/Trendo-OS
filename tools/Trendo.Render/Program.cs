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
if (Environment.GetEnvironmentVariable("TRENDO_STYLE") is { Length: > 0 } stilWahl) { var us0 = UserSettings.Load(root); us0.SetupDone = true; us0.Style = stilWahl; us0.Save(root); } // Bedienstil (trendo, windows, mac, linux) zeigen
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
// Vorschaubilder und Katalog-Lauf schreiben nie in die echten Dokumente: Projektordner liegt im eigenen Temp-Ordner
{ var us = UserSettings.Load(root); if (string.IsNullOrEmpty(us.ProjectsFolder)) { us.ProjectsFolder = Path.Combine(root, "Projekte"); us.Save(root); } }
// Immer Testmodus: Katalog und Bilder landen im öffentlichen Repository – nie echte Ordner des Nutzers lesen (z. B. „Zuletzt benutzt“ aus Dokumente).
var win = new MainWindow(core, new AppOptions(root, null, 0, Page: args.Length > 3 ? args[3] : null, Query: Environment.GetEnvironmentVariable("TRENDO_QUERY"), Isolated: true), new DefaultToolProbe()) { Width = w, Height = h, WindowState = Avalonia.Controls.WindowState.Normal };
if (Environment.GetEnvironmentVariable("TRENDO_TV_TAB") is { Length: > 0 } tvTab) { win.TvTabForTests = tvTab; win.Show("tv"); }
win.Show();
if (Environment.GetEnvironmentVariable("TRENDO_KATALOG") is { Length: > 0 } repoArg) // Masterprompt 131: Programmkatalog und Produktionspakete aus dem Repository erzeugen
{
    var repo = Path.GetFullPath(repoArg); var outDir = Path.Combine(repo, "docs", "programme");
    var trxDir = Environment.GetEnvironmentVariable("TRENDO_KATALOG_TRX");
    var trx = trxDir is { Length: > 0 } && Directory.Exists(trxDir) ? Directory.GetFiles(trxDir, "*.trx", SearchOption.AllDirectories) : [];
    var outcomes = trx.Length > 0 ? Trendo.Core.Programs.ProgramCatalog.ReadTrx(trx) : null;
    var req = File.ReadAllLines(Path.Combine(repo, "requirements.yaml")); var types = Trendo.Core.Programs.ProgramCatalog.CoreTypes(repo);
    var entries = new List<Trendo.Core.Programs.ProgramEntry>();
    foreach (var e in win.ProgramCatalogRuntime())
    {
        var (ui, tools) = win.ProgramCatalogInspect(e.Id);
        var full = Trendo.Core.Programs.ProgramCatalog.Enrich(e with { Type = ui, Tools = e.Tools.Count > 0 ? e.Tools : tools, Ui = ui.Concat(e.Shell ? ["Menüband", "Eigenschaften", "Statusleiste", "Dokument-Reiter"] : []).ToList() }, repo, req, types);
        entries.Add(full);
        var dir = Path.Combine(outDir, full.Id); Directory.CreateDirectory(dir);
        foreach (var (name, text) in Trendo.Core.Programs.ProgramCatalog.Docs(full, outcomes, trx.Length > 0 ? string.Join(", ", trx.Select(Path.GetFileName)) : null)) // nur Dateinamen: keine Pfade mit Benutzer- oder Rechnernamen
            File.WriteAllText(Path.Combine(dir, name), text.Replace("\r\n", "\n"));
    }
    foreach (var stale in Directory.GetDirectories(outDir).Where(d => !entries.Any(x => x.Id == Path.GetFileName(d)))) Directory.Delete(stale, true);
    File.WriteAllText(Path.Combine(outDir, "katalog.json"), Trendo.Core.Programs.ProgramCatalog.ToJson(entries));
    File.WriteAllText(Path.Combine(outDir, "KATALOG.md"), Trendo.Core.Programs.ProgramCatalog.Markdown(entries));
    Console.WriteLine($"Programmkatalog: {entries.Count} Programme → {outDir}" + (outcomes is null ? "" : $" ({outcomes.Count} Testergebnisse)"));
    return;
}
if (Environment.GetEnvironmentVariable("TRENDO_PDF") is { Length: > 0 } pdf) { var copy = Path.Combine(root, Path.GetFileName(pdf)); File.Copy(pdf, copy, true); win.OpenPdf(copy); if (Environment.GetEnvironmentVariable("TRENDO_PDF_ORDNEN") is { Length: > 0 }) { win.PdfOrgStart(); if (win.PdfPages > 1) { win.PdfOrgSelect(1); win.PdfOrgMove([1], 0); } } win.Show("pdf"); for (var i = 0; i < 40; i++) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(25); } win.Show("pdf"); for (var i = 0; i < 20; i++) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(25); } } // T-0223/T-0303: Trendo PDF rendern (optional Seiten ordnen)
if (Environment.GetEnvironmentVariable("TRENDO_SHEET") is { Length: > 0 } mappe) { win.SheetNew(mappe); if (Enum.TryParse<Trendo.Core.Office.ChartKind>(Environment.GetEnvironmentVariable("TRENDO_SHEET_CHART"), out var chartKind)) { win.SheetSelect("A1:C7"); win.SheetInsertChart(chartKind); } if (Environment.GetEnvironmentVariable("TRENDO_SHEET_EXTRAS") == "1") { win.SheetSelect("C2:C7"); win.SheetAddCond(Trendo.Core.Office.CondKind.Datenbalken); win.SheetSelect("C9:C11"); win.SheetAddCond(Trendo.Core.Office.CondKind.Farbskala); win.SheetSelect("B2:B7"); win.SheetAddCond(Trendo.Core.Office.CondKind.Regel, Trendo.Core.Office.CondOp.Enthaelt, text: "Einnahme"); win.SheetSetValidation("Einnahme; Ausgabe"); win.SheetSelect("A2"); win.SheetSetNote("Netto, ohne Weihnachtsgeld"); } win.SheetSelect("C2:C7"); if (Environment.GetEnvironmentVariable("TRENDO_SHEET_PDF") is { Length: > 0 } sheetPdf) win.SheetExportPdf(sheetPdf); win.Show("tabelle"); } // T-0228: Trendo Tabelle mit Vorlage rendern
if (Environment.GetEnvironmentVariable("TRENDO_DECK") is { Length: > 0 } deck) { win.DeckNew(deck); if (Environment.GetEnvironmentVariable("TRENDO_DECK_SLIDE") is { } ds && int.TryParse(ds, out var dsi)) win.DeckGo(dsi); if (Environment.GetEnvironmentVariable("TRENDO_DECK_PNG") is { Length: > 0 } pngDir) win.DeckExportImages(pngDir); win.Show("praesentation"); } // T-0229: Trendo Präsentation mit Vorlage rendern
if (Environment.GetEnvironmentVariable("TRENDO_TEXT") is { Length: > 0 } vorlage) { win.TextNew(vorlage); if (Environment.GetEnvironmentVariable("TRENDO_TEXT_RIBBON") is { Length: > 0 } rib) { win.TextRibbon = rib; win.TextSetPage(pg => { pg.Watermark = "ENTWURF"; pg.Header = "Erika Muster"; pg.PageNumbers = true; pg.Design = "klassisch"; }); win.TextSelect(1, 0, 5); win.TextAddMark(Trendo.Core.Office.MarkKind.Comment, "Bitte prüfen"); win.TextSelect(1, 6, 0); win.TextAddMark(Trendo.Core.Office.MarkKind.Footnote, "Quelle"); win.TextInsertToc(); }
    if (Environment.GetEnvironmentVariable("TRENDO_TEXT_LISTE") is { Length: > 0 }) // F3: mehrstufige Liste (erfundene Einkaufsliste)
    {
        Trendo.Core.Office.Para L(string t, Trendo.Core.Office.ListKind k, int lvl = 0) => new(t) { List = k, ListLevel = lvl };
        var (nb, bu) = (Trendo.Core.Office.ListKind.Number, Trendo.Core.Office.ListKind.Bullet);
        win.TextDocument.Blocks.Clear();
        win.TextDocument.Blocks.AddRange([new Trendo.Core.Office.Para("Einkauf fürs Wochenende", Trendo.Core.Office.ParaStyle.Heading1),
            L("Obst", nb), L("Äpfel", bu, 1), L("Birnen", bu, 1), L("die roten", bu, 2), L("Gemüse", nb), L("Möhren", nb, 1), L("Lauch", nb, 1), L("dünn", nb, 2), L("dick", nb, 2), L("Brot vom Bäcker", nb),
            new Trendo.Core.Office.Para("Mit Tab rückt ein Punkt eine Ebene tiefer, mit Umschalt+Tab wieder höher.")]);
        win.TextSelect(7, 2, 0);
    }
    if (Environment.GetEnvironmentVariable("TRENDO_TEXT_LOOK") is { Length: > 0 }) // Pilot 0.9.47: Schrift, Farbe, Hervorhebung, Zeilenabstand sichtbar
    {
        var paras = win.TextDocument.Blocks.Select((b, i) => (b, i)).Where(x => x.b is Trendo.Core.Office.Para { Text.Length: > 12 }).Select(x => x.i).Take(3).ToList();
        if (paras.Count > 0) { win.TextSelect(paras[0], 0, 6); win.TextSetInk(Trendo.Core.Office.MarkKind.Color, "#B8860B"); win.TextSelect(paras[0], 0, 6); win.TextSetInk(Trendo.Core.Office.MarkKind.Font, "Georgia"); }
        if (paras.Count > 1) { win.TextSelect(paras[1], 0, 8); win.TextSetInk(Trendo.Core.Office.MarkKind.Highlight, "#FFF59D"); win.TextSetParagraph(lineSpacing: 1.5, firstLineCm: 1.25); }
        if (paras.Count > 2) { win.TextSelect(paras[2], 0, 10); win.TextSetInk(Trendo.Core.Office.MarkKind.Color, "#C00000"); }
    }
    win.TextSelect(1, 0, 0); win.Show("text"); } // T-0223: Trendo Text mit Vorlage rendern
if (shot == "claude") // Claude im Studio-Fenster (Testmodus: claude.ai wird nicht geladen)
{
    win.Show("baukasten"); win.ClaudeDockToggle("X-03", null);
}
else if (shot == "claude-chat") // 0.9.37: Claude-Chat mit schöner Antwort, Gedächtnis und Gutachten (Claude nachgebildet, nichts wird gestartet)
{
    static string Jc(object o) => System.Text.Json.JsonSerializer.Serialize(o);
    MainWindow.ClaudeStatusOverride = () => Task.FromResult(new ClaudeCode.Status(true, true, "claude.ai", "max", "claude"));
    MainWindow.ClaudeRunOverride = (_, dir, line, _) =>
    {
        File.WriteAllText(Path.Combine(dir, "index.html"), "<!doctype html><html lang=\"de\"><head><meta name=\"viewport\" content=\"width=device-width\"><title>Bäckerei</title></head><body><h1>Bäckerei Muster</h1></body></html>");
        line(Jc(new { type = "assistant", message = new { content = new object[] { new { type = "tool_use", name = "Write", input = new { file_path = Path.Combine(dir, "index.html"), content = "…" } } } } }));
        line(Jc(new { type = "user", message = new { content = new object[] { new { type = "tool_result", content = "ok", is_error = false } } } }));
        line(Jc(new { type = "assistant", message = new { content = new object[] { new { type = "text", text = "## Deine Startseite steht\n\nIch habe eine **klare Startseite** gebaut – mit Kopfbereich, Angebot und Öffnungszeiten.\n\n| Bereich | Inhalt |\n|---|---|\n| Kopf | Logo und Menü |\n| Angebot | 6 Karten mit Bildern |\n| Kontakt | Karte und Zeiten |\n\n```css\n/* Leitfarben */\n:root { --gold: #e8c26a; --blau: #5b8fe0; }\nh1 { font-size: 2.4rem; }\n```\n\n> Tipp: Bilder kannst du einfach in den Chat ziehen.\n\nMerken: Farben sind Dunkelblau und Gold" } } } }));
        line(Jc(new { type = "result", subtype = "success", is_error = false, result = "ok", session_id = "s1", num_turns = 1, usage = new { input_tokens = 2100, output_tokens = 640 } }));
        return Task.FromResult(0);
    };
    MainWindow.ReviewOverride = _ => Task.FromResult("Note: 8/10\nStärken:\n- Klarer Aufbau, gut lesbar\nSchwächen:\n- Öffnungszeiten auf dem Handy zu klein\nVerbesserungen:\n- Schrift der Zeiten auf 1,1 rem");
    void PumpChat(Task t) { while (!t.IsCompleted) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(20); } }
    PumpChat(win.ClaudeCheck(true));
    var chatStudio = Environment.GetEnvironmentVariable("TRENDO_CLAUDE_STUDIO") is { Length: > 0 } cst ? cst : "CORE-07"; // Studio wählbar (z. B. CORE-06: Claude arbeitet im Programm „Spiele bauen“)
    win.Show(win.PageForStudio(chatStudio) ?? "dev"); win.ClaudeDockToggle(chatStudio, null); Dispatcher.UIThread.RunJobs();
    PumpChat(win.ClaudeChatSend("Baue eine Startseite für meine Bäckerei"));
    PumpChat(win.ClaudeReviewRun());
    for (var i = 0; i < 30; i++) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(20); }
    if (Environment.GetEnvironmentVariable("TRENDO_CLAUDE_GROSS") == "1")
        foreach (var b in win.GetLogicalDescendants().OfType<Button>().Where(b => b.Name is "claude-dock-max" or "claude-preview-close").ToList()) b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    if (Environment.GetEnvironmentVariable("TRENDO_CLAUDE_PANEL") is { Length: > 0 } panel)
        foreach (var b in win.GetLogicalDescendants().OfType<Button>().Where(b => b.Name == panel).ToList()) b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    for (var i = 0; i < 30; i++) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(20); }
}
else if (shot == "claude-programm") // 0.9.38: Claude hat ein Studio-Dokument geändert – Leiste zum Vergleichen, Behalten, Verwerfen (Claude nachgebildet)
{
    MainWindow.ClaudeStatusOverride = () => Task.FromResult(new ClaudeCode.Status(true, true, "claude.ai", "max", "claude"));
    MainWindow.ClaudeRunOverride = (_, dir, line, _) =>
    {
        if (Environment.GetEnvironmentVariable("TRENDO_CLAUDE_PLAN") == "1") // Claude bedient das Programm: Plan wartet auf „Ausführen“
        {
            line(System.Text.Json.JsonSerializer.Serialize(new { type = "result", subtype = "success", is_error = false, result = "Ich lege ein zweites Geschoss an und exportiere den Plan.\n[[trendo:klick | prog-new]]\n[[trendo:klick | prog-save]]\n[[trendo:klick | prog-close]]", session_id = "s1", num_turns = 1 }));
            return Task.FromResult(0);
        }
        var tdoc = Directory.GetFiles(dir, "*.tdoc").Single();
        var o = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(tdoc))!.AsObject();
        o["data"]!.AsObject()["claude"] = "hell";
        File.WriteAllText(tdoc, o.ToJsonString());
        line(System.Text.Json.JsonSerializer.Serialize(new { type = "result", subtype = "success", is_error = false, result = "Erledigt.\nVorschläge: Fenster größer | Boden in Eiche", session_id = "s1", num_turns = 1 }));
        return Task.FromResult(0);
    };
    void PumpProg(Task t) { while (!t.IsCompleted) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(20); } }
    PumpProg(win.ClaudeCheck(true));
    var progPage = MainWindow.Programs.Keys.First(k => MainWindow.Programs[k].Studio == "SPEC-01");
    win.Show(progPage); win.ProgramNew(progPage);
    win.ClaudeDockToggle("SPEC-01", null); Dispatcher.UIThread.RunJobs();
    if (win.Windows.Active?.Id is { } pwid && win.ClaudeDockIsMax(pwid)) foreach (var mb in win.GetLogicalDescendants().OfType<Button>().Where(b => b.Name == "claude-dock-max").ToList()) mb.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    win.Show(progPage);
    PumpProg(win.ClaudeChatSend("Mach das Wohnzimmer hell"));
    win.Show(progPage);
    for (var i = 0; i < 30; i++) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(20); }
}
else if (shot?.StartsWith("neuerungen", StringComparison.Ordinal) == true) // Vollbild „Das ist neu“ nach einem Update (Folie als Zahl dahinter, sonst Begrüßung)
{
    win.NewsManualClock = true; win.ShowNews("0.9.28", File.ReadAllText(Path.Combine("installer", "neuerungen", "0.9.28.md")));
    if (int.TryParse(shot["neuerungen".Length..], out var folie)) win.NewsGo(folie);
}
else if (shot == "einrichtung") win.ShowSetupOverlay("Trendo OS richtet deine Programme ein", 12, 20, "Krita 5.2.9 wird eingerichtet …"); // Vollbild-Einrichtung
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
    if (Environment.GetEnvironmentVariable("TRENDO_TEAM_STUDIO") is { Length: > 0 } teamStudio) win.AgentsForStudio(teamStudio); // Profi-Team eines Studios unter „KI einsetzen“
    if (Environment.GetEnvironmentVariable("TRENDO_MUSIK_DEMO") is { Length: > 0 }) // Trendo Musik mit erfundenen Beispielliedern (leere Dateien, nur zum Zeigen)
    {
        var lib = new SongLibrary(root); var dir = Path.Combine(root, "musik-demo"); Directory.CreateDirectory(dir);
        foreach (var (t, st, ly, sec, lang, sk, fav) in new[] {
            ("Sommer, nimm uns mit", "fröhlicher Pop, Akustikgitarre, Frauenstimme", "[Strophe]\nDie Straße glänzt im Abendlicht\nwir fahren los und halten nicht\n[Refrain]\nSommer, Sommer, nimm uns mit\nweit hinaus im gleichen Schritt", 184.0, "de", "neu", true),
            ("Sommer, nimm uns mit – Cover", "ruhige Jazz-Ballade, Klavier, Kontrabass", "[Strophe]\nDie Straße glänzt im Abendlicht", 184.0, "de", "cover", false),
            ("Bajo la luna", "Flamenco-Pop, spanische Gitarre", "[Verse]\nBajo la luna canto yo", 142.0, "es", "neu", false),
            ("Nachtfahrt", "Synthwave, instrumental, 110 BPM", "[Instrumental]", 600.0, "de", "neu", true) })
        {
            var f = Path.Combine(dir, t + ".mp3"); File.WriteAllBytes(f, [0x49, 0x44, 0x33]);
            var s = lib.Add(new MusicSong { Title = t, Style = st, Lyrics = ly, Seconds = sec, Language = lang, File = f, Kind = sk, Favorite = fav });
            win.MusicStudioSelect(s.Id);
        }
        if (Environment.GetEnvironmentVariable("TRENDO_MUSIK_DEMO") == "profi") win.MusicStudioProMode(true);
        if (Environment.GetEnvironmentVariable("TRENDO_MUSIK_DEMO") == "genre") win.MusicStudioPickGenres(true, "Jazz-Ballade", "Smooth Jazz");
        win.Show(page);
    }
    if (Environment.GetEnvironmentVariable("TRENDO_SETUP_DEMO") is { Length: > 0 } sd) // Ersteinrichtung im Vollbild (Begrüßung mit Claude und Nutzungsart)
    {
        win.DrivesOverride = () => [("E:", "SanDisk Ultra", 64.0, true), ("F:", "Intenso", 16.0, true)];
        win.EnterSetupFullscreen(); if (sd is "stick" or "abbild" or "bereit") win.SetupUse("stick");
        if (sd is "abbild" or "bereit")
        {
            win.ImageManifest = new Trendo.Core.TrendoImage.Manifest(1, "0.9.71", 1_847_721_984, new string('0', 64), 32, [new("TrendoOS-Abbild.part00", new string('0', 64), 1_847_721_984)]);
            win.SetupPickDrive("E:");
            if (sd == "abbild") win.Show("einrichtung-abbild");
        }
    }
    if (Environment.GetEnvironmentVariable("TRENDO_BRIEF_DEMO") is { Length: > 0 }) // Brief mit Beispieldaten, Tippfehler und gezeichneter Unterschrift (Schritt 3)
    {
        var l = win.Letter; l.SenderName = "Erika Muster"; l.SenderStreet = "Musterweg 1"; l.SenderCity = "12345 Musterstadt"; l.Recipient = "Beispiel Versicherung AG\nKundenservice\nHauptstraße 2\n54321 Beispielort";
        l.Subject = "Kündigung meines Vertrags 4711"; l.Body = "hiermit kündige ich meinen Vertrag fristgerecht zum 31.12.2026.\n\nBitte bestätigen Sie mir die Kündigung und das Vertragsende schriftlich. Vielen Dankee für Ihre Mühe.";
        var sig = Path.Combine(root, "unterschrift.png");
        File.WriteAllBytes(sig, Trendo.Core.LetterSignature.ToPng([[(10, 50), (25, 20), (35, 52), (50, 18), (62, 48), (80, 30), (95, 44), (110, 26), (130, 40), (150, 32)], [(20, 60), (160, 56)]])!);
        var dict = Path.Combine(Environment.CurrentDirectory, "artifacts", "vollpaket", "staging", "libreoffice", "App", "libreoffice", "share", "extensions", "dict-de");
        var dst = Path.Combine(root, "werkzeuge", "libreoffice", "App", "libreoffice", "share", "extensions", "dict-de");
        if (File.Exists(Path.Combine(dict, "de_DE_frami.dic"))) { Directory.CreateDirectory(dst); foreach (var e in new[] { ".dic", ".aff" }) File.Copy(Path.Combine(dict, "de_DE_frami" + e), Path.Combine(dst, "de_DE_frami" + e), true); }
        l.SignatureFile = sig;
        var briefDesign = Environment.GetEnvironmentVariable("TRENDO_BRIEF_DEMO") ?? "";
        if (briefDesign.Length > 1) win.LetterSetDesign(briefDesign, tagline: "Kauffrau für Büromanagement"); // z. B. navy-gold
        win.LetterGoStep(3); win.LetterSpellNow();
        if (briefDesign.Length > 1) { for (var i = 0; i < 10; i++) Dispatcher.UIThread.RunJobs(); Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(win).OfType<Avalonia.Controls.Control>().FirstOrDefault(c => c.Name == "letter-design-gallery")?.BringIntoView(); }
    }
    if (Environment.GetEnvironmentVariable("TRENDO_TABELLE_DEMO") is { Length: > 0 } tabDemo) // Ausgabenliste mit Autofilter bzw. Pivot (erfundene Beispielwerte)
    {
        win.SheetNew(); win.Show("tabelle");
        string[][] rows = [["Monat", "Kategorie", "Betrag"], ["Jan", "Miete", "800"], ["Jan", "Essen", "250,5"], ["Jan", "Auto", "95"], ["Feb", "Miete", "800"], ["Feb", "Essen", "199,5"], ["Feb", "Auto", "120"], ["Mär", "Miete", "800"], ["Mär", "Essen", "300"], ["Mär", "Freizeit", "60"]];
        for (var r = 0; r < rows.Length; r++) for (var c = 0; c < 3; c++) win.SheetSetCell($"{(char)('A' + c)}{r + 1}", rows[r][c]);
        win.SheetSelect("C2:C10"); win.SheetFormatSelection(Trendo.Core.Office.NumFmt.Euro); win.SheetSelect("B2");
        if (tabDemo == "af") { win.SheetToggleAutoFilter(); win.SheetSetColumnFilter(1, ["Essen", "Freizeit"]); }
        else if (tabDemo == "pivot") win.SheetShowPivotPanel();
        else win.SheetCreatePivot(1, 0, 2, Trendo.Core.Office.SheetPivot.Agg.Summe);
        win.Show("tabelle");
    }
    if (Environment.GetEnvironmentVariable("TRENDO_DOCK_APPS") is { Length: > 0 } dockApps) // Leiste mit hinzugefügten Apps und ihren Original-Symbolen (holt sie echt vom Dienst)
    {
        foreach (var id in dockApps.Split(',')) { win.InstallWebApp(id); Task.Run(() => win.FetchWebAppIcon(id)).Wait(TimeSpan.FromSeconds(40)); }
        for (var i = 0; i < 20; i++) Dispatcher.UIThread.RunJobs();
        win.RefreshDockWebApps(); win.Show(page);
    }
    if (Environment.GetEnvironmentVariable("TRENDO_CAL") is { Length: > 0 }) // Kalender-Woche mit erfundenen Beispielterminen
    {
        var mo = new DateTime(2026, 11, 2);
        foreach (var (d, hr, len, t, loc) in new[] { (0, 9.0, 1.0, "Teambesprechung", "Raum 2"), (0, 12.5, 1.0, "Mittagessen mit Erika", ""), (1, 10.0, 1.5, "Zahnarzt", "Praxis Muster"), (2, 8.5, 0.5, "Telefonat Vermieter", ""), (2, 14.0, 2.0, "Projekt-Workshop", "Online"), (2, 15.0, 1.0, "Paket abholen", "Post"), (3, 18.0, 1.5, "Sport", "Halle"), (4, 16.0, 1.0, "Kinder abholen", "Schule") })
            win.Cal.Add(new Trendo.Core.Sys.CalEvent(Guid.NewGuid().ToString("N"), mo.AddDays(d).AddHours(hr), mo.AddDays(d).AddHours(hr + len), t, loc));
        win.Cal.Add(new Trendo.Core.Sys.CalEvent("ganz", mo.AddDays(5), mo.AddDays(6), "Ausflug", AllDay: true));
        win.Cal.AddTodo(new Trendo.Core.Sys.TodoItem("st", "Steuererklärung abgeben", mo.AddDays(3), Priority: 1)); win.SuggestNow = () => mo.AddHours(8);
        win.CalDay = mo; win.CalMode = "woche"; win.CalSelected = win.Cal.All().First(e => e.Title == "Zahnarzt").Uid; win.Show("kalender");
    }
    if (Environment.GetEnvironmentVariable("TRENDO_TODO") is { Length: > 0 }) // Aufgaben-Tafel mit erfundenen Beispielaufgaben
    {
        var td = new DateTime(2026, 11, 2);
        foreach (var (t, l, pr, due, st, n) in new[] { ("Steuererklärung abgeben", "Haushalt", 1, (DateTime?)td.AddDays(13), 0, "Belege im Ordner 2025"), ("Fahrrad zur Inspektion", "Haushalt", 2, null, 0, ""), ("Noten kopieren", "Chor", 2, td.AddDays(-2), 0, ""), ("Präsentation vorbereiten", "Arbeit", 1, td.AddDays(3), 1, "Folien 1–12, Zahlen vom Oktober"), ("Angebot vergleichen", "Arbeit", 2, td.AddDays(5), 1, ""), ("Geschenk für Erika", "Familie", 3, null, 2, ""), ("Arzttermin ausmachen", "Familie", 2, null, 2, "") })
            win.Cal.AddTodo(new Trendo.Core.Sys.TodoItem(Guid.NewGuid().ToString("N"), t, due, st == 2, pr, l, st == 1, n));
        win.TodoSelected = win.Cal.Todos().First(x => x.Title.StartsWith("Präsentation")).Uid; win.Show("aufgaben");
    }
    if (Environment.GetEnvironmentVariable("TRENDO_SCHNITT") is { Length: > 0 } && Cli.Find("ffmpeg") is { } ffm) // Videoschnitt mit erzeugten Beispielmedien
    {
        var md = Path.Combine(root, "medien-beispiel"); Directory.CreateDirectory(md);
        string M(string n) => Path.Combine(md, n);
        Cli.Run(ffm, ["-y", "-loglevel", "error", "-f", "lavfi", "-i", "testsrc2=size=1280x720:rate=30:duration=8", "-f", "lavfi", "-i", "sine=frequency=330:duration=8", "-shortest", "-c:v", "libx264", "-pix_fmt", "yuv420p", "-c:a", "aac", M("ausflug.mp4")], md, 120000);
        Cli.Run(ffm, ["-y", "-loglevel", "error", "-f", "lavfi", "-i", "mandelbrot=size=1280x720:rate=30", "-t", "6", "-c:v", "libx264", "-pix_fmt", "yuv420p", M("abendhimmel.mp4")], md, 120000);
        Cli.Run(ffm, ["-y", "-loglevel", "error", "-f", "lavfi", "-i", "gradients=size=1280x720:seed=7", "-frames:v", "1", M("titelbild.png")], md, 120000);
        Cli.Run(ffm, ["-y", "-loglevel", "error", "-f", "lavfi", "-i", "sine=frequency=220:duration=20", M("musik.mp3")], md, 120000);
        win.ProgramNew("schnitt"); win.EditAdd(M("titelbild.png")); win.EditAdd(M("ausflug.mp4")); win.EditAdd(M("abendhimmel.mp4")); win.EditAdd(M("musik.mp3"), music: true);
        var doc = win.ProgramActive("schnitt")!; doc.Edit("Überblenden/Text", x => { Trendo.Core.Media.VideoEdit.SetClip(x, 0, 1, "Unser Ausflug"); Trendo.Core.Media.VideoEdit.SetClip(x, 1, 1.5, ""); }); doc.Set("playhead", 2.5); win.EditSelected = ("clips", 0); win.Show("schnitt");
    }
    if (Environment.GetEnvironmentVariable("TRENDO_WF") is { Length: > 0 }) // Ablaufplan mit Vorlage
    {
        MainWindow.WorkflowHome = Path.Combine(root, "heim"); Directory.CreateDirectory(MainWindow.WorkflowHome);
        var wfl = win.Workflows.Add(WorkflowEngine.Templates[0].Make(MainWindow.WorkflowHome)); wfl.Steps.Add(new WfStep { Kind = StepKind.Zippen, From = Path.Combine(MainWindow.WorkflowHome, "Downloads"), To = Path.Combine(MainWindow.WorkflowHome, "archiv-{datum}.zip") }); win.Workflows.Update(wfl);
        win.WfSelected = wfl.Id; win.WfNode = 0; win.Show("ablaeufe");
    }
    if (Environment.GetEnvironmentVariable("TRENDO_BILD") is { Length: > 0 } bildSrc && File.Exists(bildSrc)) // Bildbearbeitung mit Ebenen
    {
        win.ProgramNew("bild", "quadrat"); win.ImgAddImage(bildSrc);
        var bd = win.ProgramActive("bild")!; var bw = bd.Number("width"); var bh = bd.Number("height");
        bd.Edit("Beispiel", x =>
        {
            Trendo.Core.Media.ImageDoc.AddShape(x, "rect", bw * 0.06, bh * 0.74, bw * 0.88, bh * 0.18, "#CC0B1424");
            Trendo.Core.Media.ImageDoc.AddText(x, "Trendo OS 0.9.28", bw * 0.1, bh * 0.76, bh / 14, "#FACC15");
            var pl = Trendo.Core.Media.ImageDoc.AddPaint(x);
            Trendo.Core.Media.ImageDoc.AddStroke(pl, [.. Enumerable.Range(0, 40).Select(i => (bw * (0.1 + i * 0.02), bh * (0.12 + 0.03 * Math.Sin(i / 4.0))))], "#22C55E", bh / 90, false);
            x["selected"] = (string?)((System.Text.Json.Nodes.JsonObject)Trendo.Core.Media.ImageDoc.Layers(x)[0]!)["id"];
        });
        win.Show("bild");
    }
    if (Environment.GetEnvironmentVariable("TRENDO_DRUCK") is { Length: > 0 }) // 3D-Druck-Vorschau mit T-Stück (Überhang)
    {
        var tm = new Mesh(); Mesh3D.AddBox(tm, -1, 1, -1, 1, 0, 6); Mesh3D.AddBox(tm, -4.5, 4.5, -1, 1, 6, 7.5); // cm – STL wird in mm geschrieben
        var stl = Path.Combine(root, "t-stueck.stl"); Mesh3D.WriteStl(tm, stl);
        win.Open3dPrint(stl); if (Environment.GetEnvironmentVariable("TRENDO_DRUCK") == "auf") win.PrintAutoOrient(); win.Show("3d-druck");
    }
    if (Environment.GetEnvironmentVariable("TRENDO_DISK") is { Length: > 0 } diskDir) // Speicherplatz-Karte
    {
        var task = win.DiskScanAsync(diskDir); while (!task.IsCompleted) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(20); }
        if (win.DiskAt?.Children.FirstOrDefault(c => c.IsDir) is { } firstDir) win.DiskSelect(firstDir);
        win.Show("ressourcen");
    }
    if (Environment.GetEnvironmentVariable("TRENDO_BRIEF") is { Length: > 0 }) // Brief nach DIN 5008, zweiseitig, Empfänger aus dem Adressbuch
    {
        win.Mail.UpdateContact(new Trendo.Core.Contact("demo-e", "Max Beispiel", "", Street: "Ring 5", City: "80331 München", Company: "Beispiel AG"));
        var L = win.Letter; L.SenderName = "Erika Muster"; L.SenderStreet = "Hauptstraße 1"; L.SenderCity = "10115 Berlin"; L.SenderContact = "Tel. 030 123456 · erika@example.de";
        L.Recipient = Trendo.Core.Letters.RecipientOf(win.Mail.Contacts()[0]); L.Salutation = Trendo.Core.Letters.SalutationFor(win.Mail.Contacts()[0]); L.Place = "Berlin";
        L.Reference = "KD-4711"; L.YourMessage = "28.10.2026"; L.OurReference = "em/26"; L.Subject = "Angebot für die Gartengestaltung";
        L.Body = string.Join("\n\n", Enumerable.Range(1, 14).Select(i => $"Punkt {i}: Wir bedanken uns für Ihre Anfrage und bieten Ihnen die Leistung wie besprochen an. Die Arbeiten umfassen Planung, Material und Ausführung."));
        L.Attachments = "Leistungsbeschreibung\nPreisliste 2026"; L.Category = "anfrage";
        win.Show("brief");
    }
    if (Environment.GetEnvironmentVariable("TRENDO_RECHERCHE") is { Length: > 0 }) // Recherche mit Markierungen (Beispielartikel, ohne Netz)
    {
        const string article = "<html><head><title>Wildbienen im Garten</title></head><body><h1>Wildbienen im Garten</h1>"
            + "<p>In Deutschland leben rund 560 Wildbienenarten. Anders als die Honigbiene leben die meisten von ihnen allein und bilden keine Völker.</p>"
            + "<p>Wildbienen bestäuben viele Wild- und Kulturpflanzen. Manche Arten sind auf wenige Pflanzen spezialisiert und verschwinden, wenn diese fehlen.</p>"
            + "<h2>Was hilft</h2><p>Etwa drei Viertel der Arten nisten im Boden. Sie brauchen offene, sonnige Bodenstellen ohne dichten Bewuchs.</p>"
            + "<p>Heimische Blühpflanzen von März bis Oktober sichern Nahrung über die ganze Saison. Ungefüllte Blüten sind besser als gefüllte, weil sie Pollen und Nektar bieten.</p>"
            + "<p>Totholz, markhaltige Stängel und Trockenmauern sind wertvolle Nistplätze. Ein aufgeräumter Garten ist für viele Arten dagegen ein Problem.</p></body></html>";
        win.ReaderFetchOverride = u => Task.FromResult(("text/html", article, u));
        var rt = win.ReadUrlAsync("https://www.example.org/wildbienen"); while (!rt.IsCompleted) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(10); }
        var txt = win.ReaderShown!.Text; var url = win.ReaderUrl;
        void Mark(string part, string color, string note) { var i = txt.IndexOf(part, StringComparison.Ordinal); if (i >= 0) win.Desk.AddHighlight(url, win.ReaderShown!.Title, txt, i, part.Length, color, note); }
        Mark("Wildbienen bestäuben viele Wild- und Kulturpflanzen.", "gelb", "Kernaussage für den Vortrag");
        Mark("Etwa drei Viertel der Arten nisten im Boden.", "grün", "Zahl prüfen (zweite Quelle)");
        Mark("Ein aufgeräumter Garten ist für viele Arten dagegen ein Problem.", "rot", "");
        win.Show("recherche");
    }
    if (Environment.GetEnvironmentVariable("TRENDO_PLAN") is { Length: > 0 }) // Vorhaben als Zeitplan mit Abhängigkeiten
    {
        var pdir = CreationSuite.Create(win.SuiteFolder, "event", "Sommerfest im Verein", DateTime.Today.AddDays(-4));
        var st = CreationSuite.Steps(pdir);
        CreationSuite.SetState(pdir, st[0].Line, 2); CreationSuite.SetState(pdir, st[1].Line, 1);
        CreationSuite.SetAfter(pdir, 3, [2]); CreationSuite.SetAfter(pdir, 5, [3, 4]); CreationSuite.SetAfter(pdir, 6, [5]); CreationSuite.Reschedule(pdir);
        win.SuiteOpenForPreview(pdir, "zeitplan", 2);
        win.Show("vorhaben");
    }
    if (Environment.GetEnvironmentVariable("TRENDO_KONTAKTE") is { Length: > 0 } abMode) // Adressbuch mit Beispielkontakten
    {
        var t = DateOnly.FromDateTime(DateTime.Today);
        (string N, string E, string M, string C, string City, int BdayIn, string[] G, bool Fav)[] people = [
            ("Erika Muster", "erika@example.de", "0160 1234567", "Muster GmbH", "10115 Berlin", 4, ["Familie"], true), ("Max Beispiel", "max@example.de", "0171 7654321", "Beispiel AG", "80331 München", 40, ["Arbeit"], false),
            ("Anna Schmidt", "anna@example.org", "0152 111222", "", "50667 Köln", 12, ["Verein"], true), ("Bernd Neumann", "", "0176 999888", "Neumann Holzbau", "20095 Hamburg", 200, ["Arbeit"], false),
            ("Clara Weber", "clara@example.net", "", "", "01067 Dresden", 25, ["Familie", "Verein"], false), ("David Fischer", "david@example.de", "0151 333444", "Praxis Fischer", "70173 Stuttgart", 90, [], false),
            ("Eva Hoffmann", "eva@example.de", "", "", "04109 Leipzig", 300, ["Verein"], false), ("Felix Wagner", "", "0172 555666", "", "90402 Nürnberg", 150, [], false),
            ("Greta Becker", "greta@example.de", "0163 777", "Café Becker", "28195 Bremen", 60, ["Arbeit"], false), ("Max Beispiel", "", "+49 171 7654321", "", "", 400, [], false) ];
        var k = 0;
        foreach (var x in people) { var b = t.AddDays(x.BdayIn); win.Mail.UpdateContact(new Trendo.Core.Contact($"demo{k++}", x.N, x.E, "", x.N.StartsWith("Erika") ? "Mag Rosen; ruft lieber abends an." : "", x.M, x.C, x.N.StartsWith("Erika") ? "Hauptstraße 1" : "", x.City, new DateOnly(1960 + k * 3, b.Month, b.Day), x.G.Length > 0 ? [.. x.G] : null, "", x.Fav, x.N.StartsWith("Erika") ? "https://example.de" : "")); }
        win.AddressSelectForPreview("demo0", abMode == "bearbeiten");
        win.Show("kontakte");
    }
    if (Environment.GetEnvironmentVariable("TRENDO_MUSIK") is { Length: > 0 } musikMode) // Musikbibliothek mit Beispiel-Alben (eingebettete Cover), ein Titel läuft
    {
        static byte[] Syn(int n) => [(byte)(n >> 21 & 0x7F), (byte)(n >> 14 & 0x7F), (byte)(n >> 7 & 0x7F), (byte)(n & 0x7F)];
        static byte[] Fr(string id, byte[] d) => [.. System.Text.Encoding.ASCII.GetBytes(id), (byte)(d.Length >> 24), (byte)(d.Length >> 16), (byte)(d.Length >> 8), (byte)d.Length, 0, 0, .. d];
        static byte[] Cov(SkiaSharp.SKColor a, SkiaSharp.SKColor b, int seed)
        {
            using var bmp = new SkiaSharp.SKBitmap(300, 300); using (var cv = new SkiaSharp.SKCanvas(bmp))
            {
                using var bg = new SkiaSharp.SKPaint { Shader = SkiaSharp.SKShader.CreateLinearGradient(new(0, 0), new(300, 300), [a, b], SkiaSharp.SKShaderTileMode.Clamp) }; cv.DrawRect(0, 0, 300, 300, bg);
                var r = new Random(seed); using var ring = new SkiaSharp.SKPaint { Color = new SkiaSharp.SKColor(255, 255, 255, 70), IsStroke = true, StrokeWidth = 6, IsAntialias = true };
                for (var k = 0; k < 5; k++) cv.DrawCircle(r.Next(40, 260), r.Next(40, 260), r.Next(20, 120), ring);
            }
            using var ms = new MemoryStream(); bmp.Encode(ms, SkiaSharp.SKEncodedImageFormat.Png, 90); return ms.ToArray();
        }
        var mdir = Path.Combine(root, "beispielmusik");
        (string Artist, string Album, string[] Titles, SkiaSharp.SKColor A, SkiaSharp.SKColor B)[] albums = [
            ("Erika Muster", "Morgenlicht", ["Aufbruch", "Über den Feldern", "Kaffee am Fenster", "Lichtspiel"], new(0xf7, 0x9d, 0x5c), new(0x6a, 0x1b, 0x4d)),
            ("Das Hafenquartett", "Nordwind", ["Möwen", "Leuchtturm", "Ebbe"], new(0x13, 0x4e, 0x5e), new(0x71, 0xb2, 0x80)),
            ("Klangwerk", "Stadt bei Nacht", ["Neon", "Letzte Bahn", "Dächer", "Regen auf Glas", "Halb vier"], new(0x41, 0x29, 0x5a), new(0x2f, 0x80, 0xed)),
            ("Max Beispiel", "Holz & Saiten", ["Werkstatt", "Erste Saite"], new(0x8e, 0x5a, 0x2b), new(0x2b, 0x1a, 0x0e)),
            ("Klangwerk", "Frühe Aufnahmen", ["Probe 1", "Probe 2", "Garage"], new(0x37, 0x47, 0x4f), new(0xcf, 0xd8, 0xdc)) ];
        var seedN = 1;
        foreach (var al in albums)
        {
            var d = Directory.CreateDirectory(Path.Combine(mdir, al.Artist, al.Album)).FullName; var cover = Cov(al.A, al.B, seedN++);
            for (var t = 0; t < al.Titles.Length; t++)
            {
                byte[] frames = [.. Fr("TIT2", [3, .. System.Text.Encoding.UTF8.GetBytes(al.Titles[t])]), .. Fr("TPE1", [3, .. System.Text.Encoding.UTF8.GetBytes(al.Artist)]), .. Fr("TALB", [3, .. System.Text.Encoding.UTF8.GetBytes(al.Album)]),
                    .. Fr("TRCK", [0, .. System.Text.Encoding.ASCII.GetBytes((t + 1).ToString())]), .. Fr("TYER", [0, .. "2025"u8.ToArray()]), .. Fr("APIC", [0, .. "image/png"u8.ToArray(), 0, 3, 0, .. cover])];
                File.WriteAllBytes(Path.Combine(d, $"{t + 1:00}.mp3"), [.. "ID3"u8.ToArray(), 3, 0, 0, .. Syn(frames.Length), .. frames, .. new byte[200]]);
            }
        }
        win.MusicFoldersOverride = [mdir]; win.AudioOverride = () => new PreviewAudio();
        win.Show("medien"); win.MediaTabForPreview("musik");
        var mt = win.MusicLoadAsync(); while (!mt.IsCompleted || win.MusicTracks.Count == 0) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(20); }
        var morgen = win.MusicTracks.Where(x => x.Album == "Morgenlicht").OrderBy(x => x.No).Select(x => x.Path).ToList();
        if (musikMode == "album") win.MusicViewForPreview("album:Erika Muster|Morgenlicht");
        win.MusicPlay(morgen, 1); win.MusicProgressForPreview(83, 214);
        for (var i = 0; i < 50; i++) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(20); } win.Show("medien"); win.MusicProgressForPreview(83, 214); for (var i = 0; i < 20; i++) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(20); }
    }
    if (Environment.GetEnvironmentVariable("TRENDO_KASSE") is { Length: > 0 }) // Kasse mit Beispielartikeln, zwei Verkäufen und gefülltem Warenkorb
    {
        var k = win.Kasse;
        foreach (var (n, pr, st) in new[] { ("Filterkaffee", 2.2, 40), ("Cappuccino", 3.1, 40), ("Croissant", 1.9, 12), ("Apfelkuchen", 3.4, 8), ("Bio-Honig 500 g", 7.5, 6), ("Tee", 2.0, 30), ("Brezel", 1.2, 0), ("Kaffeebohnen 1 kg", 18.9, 4) })
            k.Shop.Products.Add(new Trendo.Core.Product(n, "", 0, pr, st, 0, 0));
        k.Checkout([("Cappuccino", 2), ("Croissant", 2)], "bar", 20, DateTime.Now.AddHours(-2), win.Money, true);
        k.Checkout([("Bio-Honig 500 g", 1), ("Filterkaffee", 1)], "karte", 0, DateTime.Now.AddHours(-1), win.Money, true);
        win.Show("verkaufen"); win.KasseRenderCart([("Cappuccino", 2), ("Apfelkuchen", 1)], "10");
        win.Show("verkaufen");
    }
    if (Environment.GetEnvironmentVariable("TRENDO_FOTOS") is { Length: > 0 } fotoMode) // Fotoalbum mit erzeugten Beispielbildern (Verläufe, Horizont, Sonne)
    {
        var fdir = Path.Combine(root, "beispielfotos"); Directory.CreateDirectory(fdir); var rnd = new Random(11);
        (SkiaSharp.SKColor, SkiaSharp.SKColor)[] skies = [(new(0x1e, 0x3c, 0x72), new(0xf7, 0x9d, 0x5c)), (new(0x5b, 0x9b, 0xd5), new(0xe8, 0xf1, 0xf8)), (new(0x2c, 0x3e, 0x50), new(0xfd, 0x74, 0x6c)), (new(0x13, 0x4e, 0x5e), new(0x71, 0xb2, 0x80)), (new(0x41, 0x29, 0x5a), new(0x2f, 0x07, 0x43))];
        for (var k = 0; k < 26; k++)
        {
            using var bmp = new SkiaSharp.SKBitmap(640, 480); using (var cv = new SkiaSharp.SKCanvas(bmp))
            {
                var (top, bottom) = skies[rnd.Next(skies.Length)];
                using var sky = new SkiaSharp.SKPaint { Shader = SkiaSharp.SKShader.CreateLinearGradient(new(0, 0), new(0, 480), [top, bottom], SkiaSharp.SKShaderTileMode.Clamp) }; cv.DrawRect(0, 0, 640, 480, sky);
                using var sun = new SkiaSharp.SKPaint { Color = new SkiaSharp.SKColor(255, 236, 180, 220), IsAntialias = true }; cv.DrawCircle(rnd.Next(80, 560), rnd.Next(90, 230), rnd.Next(25, 60), sun);
                var hz = rnd.Next(260, 360); using var land = new SkiaSharp.SKPath(); land.MoveTo(0, hz);
                for (var x = 0; x <= 640; x += 40) land.LineTo(x, hz + (float)(Math.Sin(x / 90.0 + k) * 30 + rnd.Next(-12, 12)));
                land.LineTo(640, 480); land.LineTo(0, 480); land.Close();
                using var lp = new SkiaSharp.SKPaint { Shader = SkiaSharp.SKShader.CreateLinearGradient(new(0, hz), new(0, 480), [new SkiaSharp.SKColor(0x1b, 0x2a, 0x1f), new SkiaSharp.SKColor(0x0b, 0x12, 0x0e)], SkiaSharp.SKShaderTileMode.Clamp), IsAntialias = true }; cv.DrawPath(land, lp);
            }
            var file = Path.Combine(fdir, $"IMG_{2000 + k}.png"); using (var fs = File.Create(file)) bmp.Encode(fs, SkiaSharp.SKEncodedImageFormat.Png, 90);
            File.SetLastWriteTime(file, DateTime.Now.AddDays(-k * 3.3));
        }
        win.PhotoFoldersOverride = [fdir];
        var t = win.PhotosLoadAsync(); while (!t.IsCompleted || win.PhotosLoaded.Count == 0) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(20); }
        win.Albums.Create("Urlaub am See"); win.Albums.Add("Urlaub am See", win.PhotosLoaded.Take(6).Select(p => p.Path)); win.Albums.Add("Favoriten", win.PhotosLoaded.Skip(2).Take(3).Select(p => p.Path));
        if (fotoMode == "gross") win.PhotoOpenForPreview(win.PhotosLoaded[3].Path);
        win.Show("medien"); for (var i = 0; i < 60; i++) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(20); } win.Show("medien"); for (var i = 0; i < 30; i++) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(20); }
    }
    if (Environment.GetEnvironmentVariable("TRENDO_GELD") is { Length: > 0 }) // Haushaltsbuch mit Beispielbuchungen (drei Monate)
    {
        var m0 = new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1); var rnd = new Random(3);
        for (var k = 0; k < 3; k++)
        {
            var m = m0.AddMonths(-k);
            win.Money.Add(new(m, "Arbeitgeber – Gehalt", 2850, "Einnahmen")); win.Money.Add(new(m, "Hausverwaltung – Miete", -890, "Wohnen"));
            win.Money.Add(new(m.AddDays(2), "Stadtwerke – Abschlag Strom", -78, "Wohnen")); win.Money.Add(new(m.AddDays(14), "Streamingdienst – Abo", -13.99m, "Freizeit"));
            win.Money.Add(new(m.AddDays(4), "Versicherung – Haftpflicht", -7.50m, "Versicherung")); win.Money.Add(new(m.AddDays(9), "Deutschlandticket", -58, "Mobilität"));
            foreach (var shop in new[] { "Rewe", "Edeka", "Wochenmarkt", "Bäckerei" }) win.Money.Add(new(m.AddDays(rnd.Next(0, 27)), shop + " – Einkauf", -Math.Round((decimal)(rnd.NextDouble() * 60 + 12), 2), "Lebensmittel"));
            win.Money.Add(new(m.AddDays(rnd.Next(0, 27)), "Apotheke am Markt", -14.90m, "Gesundheit")); win.Money.Add(new(m.AddDays(rnd.Next(0, 27)), "Kino – Erika Muster", -24, "Freizeit"));
        }
        win.Budgets.Set("Lebensmittel", 250); win.Budgets.Set("Freizeit", 60);
        win.Show("finanzen");
    }
    if (Environment.GetEnvironmentVariable("TRENDO_TV") is { Length: > 0 }) // Programmführer als Raster mit Beispielsendern
    {
        var src = win.Tv.AddSource("Beispiel", "datei", "beispiel.m3u");
        string[] names = ["Trendo Eins", "Trendo Zwei", "Kino Plus", "Sport Live", "Kinderwelt", "Wissen TV", "Nachrichten 24", "Show Kanal"];
        win.Tv.StorePlaylist(src.Id, "#EXTM3U\n" + string.Concat(names.Select((n, i) => $"#EXTINF:-1 tvg-id=\"k{i}\" tvg-chno=\"{i + 1}\" group-title=\"Beispiel\",{n}\nhttps://example.invalid/{i}.ts\n")));
        var t0 = Trendo.Core.Tv.TvGuide.Start(DateTime.UtcNow, 0).AddHours(-1); var rnd = new Random(7); var progs = new List<Trendo.Core.Tv.TvProgramme>();
        string[][] shows = [["Morgenmagazin", "Nachrichten"], ["Der Fall Muster", "Krimi"], ["Abenteuer Alpen", "Dokumentation"], ["Großes Kino", "Spielfilm"], ["Bundesliga live", "Sport"], ["Sandmann", "Kinder"], ["Quiz am Abend", "Quiz"], ["Tagesschau", "Nachrichten"], ["Familie Muster", "Serie"], ["Talk um acht", "Talk"]];
        for (var i = 0; i < names.Length; i++) for (var t = t0.AddMinutes(-rnd.Next(0, 40)); t < t0.AddHours(30);) { var s = shows[rnd.Next(shows.Length)]; var len = new[] { 15, 30, 45, 60, 90, 105 }[rnd.Next(6)]; progs.Add(new($"k{i}", t, t.AddMinutes(len), s[0], "Beispielsendung mit Erika Muster.", s[1])); t = t.AddMinutes(len); }
        win.Tv.StoreEpg(src.Id, progs);
        var ch = win.Tv.Channels(); var pick = progs.First(p => p.Channel == "k1" && p.Start > DateTime.UtcNow);
        win.TvPlanRecording(ch[1], pick.Title, pick.Start, pick.Stop);
        win.TvReloadData(); win.TvTab = "guide"; win.Show("tv");
    }
    if (Environment.GetEnvironmentVariable("TRENDO_BACKUPDEMO") is { Length: > 0 }) // Sicherung mit Zeitleiste
    {
        var b = win.BackupStore; var broot = Path.GetDirectoryName(b.Folder)!; Directory.CreateDirectory(Path.Combine(broot, "Projekte"));
        File.WriteAllText(Path.Combine(broot, "Projekte", "Brief an Erika Muster.txt"), "Liebe Erika,\nhier die erste Fassung.");
        File.WriteAllText(Path.Combine(broot, "Projekte", "Einkauf.txt"), "Milch, Brot");
        string Mk(DateTime t) { var f = b.Create(t); File.SetLastWriteTime(f, t); return f; }
        Mk(DateTime.Now.AddDays(-3)); Mk(DateTime.Now.AddDays(-1)); var z = Mk(DateTime.Now.AddHours(-2));
        File.WriteAllText(Path.Combine(broot, "Projekte", "Brief an Erika Muster.txt"), "Liebe Erika,\nhier die überarbeitete Fassung.");
        File.Delete(Path.Combine(broot, "Projekte", "Einkauf.txt")); File.WriteAllText(Path.Combine(broot, "Projekte", "Neu.txt"), "neu");
        win.BackupSelected = z; win.BackupFile = "Projekte/Brief an Erika Muster.txt"; win.Show("sicherung");
    }
    if (Environment.GetEnvironmentVariable("TRENDO_3D") is { Length: > 0 }) { win.Show("3d"); foreach (var sh in new[] { "cube", "sphere", "cylinder", "torus" }) win.Studio3DAddShape(sh); win.Studio3DSelect(1); } // 3D-Modelle erstellen mit Arbeitsfläche
    if (Environment.GetEnvironmentVariable("TRENDO_PROGTPL") is { Length: > 0 } tpl) { if (Environment.GetEnvironmentVariable("TRENDO_LEVEL") is { Length: > 0 } lv) win.ProgramSetLevel(Trendo.Core.Programs.UserLevel.Of(lv)); win.ProgramNew(page, tpl); if (Environment.GetEnvironmentVariable("TRENDO_GAMESEC") is { Length: > 0 } gs) win.GameSection = gs; win.Show(page); } // Programm mit Vorlage öffnen
    if (page == "architektur" && Environment.GetEnvironmentVariable("TRENDO_ARCH") is { Length: > 0 } arch) // Grundriss: Auswahl/3D/Werkzeug zeigen
    {
        var plan = Trendo.Core.Arch.FloorPlan.From(win.ProgramActive(page)!.Data);
        if (arch.Contains("raum")) win.ArchState.Selected = plan.Rooms[0].Id;
        if (arch.Contains("moebel")) win.ArchState.Tool = "moebel";
        if (arch.Contains("plus")) win.ArchEdit("Beispiel", pl => { pl.AddDimension(-12, 736, 884, 736, 70); pl.AddLabel(250, 300, "Fernsehecke"); });
        if (arch.Contains("bogen")) win.ArchEdit("Beispiel", pl => { pl.North = 30; var g = pl.AddArcWall(860, 100, 860, 600, -150); win.ArchState.Selected = pl.ArcWalls(g)[0].Id; });
        if (arch.Contains("3d")) win.ArchToggle3D();
        win.Show(page);
        if (arch.Contains("foto")) // Foto-Ansicht: echtes Blender (Cycles) im Hintergrund
        {
            win.ArchPhotoToggle(); win.ArchPhotoMode = arch.Contains("raum") ? "raum" : "vogel"; win.ArchPhotoFinal = arch.Contains("fertig");
            var t = win.ArchPhotoRender(); for (var i = 0; i < 6000 && !t.IsCompleted; i++) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(50); }
            win.Show(page);
        }
        if (arch.Contains("nah")) // Nahansicht der 3D-Möbel
        {
            Pump(); var cv = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(win).OfType<CadView>().FirstOrDefault(c => c.Name == "arch-3d");
            if (cv is not null) { cv.Zoom = 2.4; cv.Pitch = 48; cv.Yaw = 25; cv.InvalidateVisual(); }
        }
    }
    if (page == "elektronik" && Environment.GetEnvironmentVariable("TRENDO_EK") is { Length: > 0 } ek) // Schaltung: Bauteil wählen, Simulation laufen lassen
    {
        var circ = Trendo.Core.Elektro.Circuit.From(win.ProgramActive(page)!.Data);
        win.CircuitState.Selected = circ.Parts.FirstOrDefault(x => x.Kind == ek)?.Id;
        for (var k = 0; k < 10; k++) win.CircuitAdvance(0.02);
        if (ek == "pcb") { win.CircuitState.Pcb = true; win.CircuitEdit("Platine", x => { Trendo.Core.Elektro.Pcb.AutoPlace(x); Trendo.Core.Elektro.Pcb.AutoRoute(x); }); }
        win.Show(page);
    }
    if (page == "karten" && Environment.GetEnvironmentVariable("TRENDO_MK") is { Length: > 0 }) // Karte: erste Spur auswählen
    {
        var md = Trendo.Core.Karten.MapDoc.From(win.ProgramActive(page)!.Data); win.MapSelect(md.Tracks.FirstOrDefault()?.Id);
    }
    if (page == "lernen" && Environment.GetEnvironmentVariable("TRENDO_LEARN") is { Length: > 0 } lm) // Karteikarten: Beispielstapel, Modus, aufgedeckt
    {
        win.Learn.Import("Englisch", "Hund;dog\nKatze;cat\nHaus;house\nBaum;tree\nApfel;apple\nBuch;book"); win.LearnMode = lm == "auf" ? "lernen" : lm; win.LearnDeck = "Englisch";
        if (lm == "auf") win.LearnReveal(); else win.Show(page);
    }
    if (page == "sendung" && Environment.GetEnvironmentVariable("TRENDO_BC") is { Length: > 0 } bcMode) // Sendeablauf mit Beispielablauf
    {
        win.Broadcast.Plan.Show = "Abendmagazin"; win.Broadcast.Plan.Start = DateTime.Now.AddMinutes(-3); win.Broadcast.Plan.SlotMinutes = 30;
        foreach (var (t, sec, n) in new[] { ("Begrüßung", 90, "Kamera 1"), ("Nachrichten", 300, "Einspieler"), ("Gast im Studio: Erika Muster", 600, "Kamera 2, Ansteckmikro"), ("Musik", 240, ""), ("Wetter", 120, "Grafik"), ("Verabschiedung", 60, "") }) win.Broadcast.Add(t, sec, n);
        win.BcMode = bcMode; win.Show(page);
    }
    if (page == "fahrzeug" && Environment.GetEnvironmentVariable("TRENDO_CAR") is { Length: > 0 } carTab) // Fahrzeugbuch mit Beispielfahrzeug
    {
        var demo = Trendo.Core.VehiclePlus.SeedDemo(win.Cars, DateTime.Today); win.CarSel = demo.Name; win.CarTab = carTab; win.Show(page);
    }
    if (page == "bib3d" && Environment.GetEnvironmentVariable("TRENDO_BIB") is { Length: > 0 } bibQ) // 3D-Bibliothek: echte Live-Suche (Internet)
    {
        var parts = bibQ.Split(':'); if (parts.Length > 1) win.BibKind = parts[0]; win.BibQuery = parts[^1];
        var t = win.BibSearch(); for (var i = 0; i < 600 && (!t.IsCompleted); i++) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(50); }
    }
    if (page == "xr" && Environment.GetEnvironmentVariable("TRENDO_XR") is { Length: > 0 } xrArg) // Modell-Prüfer mit Beispielmodell oder Datei
    {
        win.XrFile = File.Exists(xrArg) ? xrArg : Trendo.Core.XrSample.Write(Path.Combine(root, "beispiel-leseecke.glb")); win.Show(page);
    }
    if (page == "zwilling" && Environment.GetEnvironmentVariable("TRENDO_TWIN") is { Length: > 0 }) // Anlagen-Monitor mit Beispielanlagen
    {
        Trendo.Core.TwinDemo.Seed(win.Twin, DateTime.Now); win.TwinSelect("Kühlraum", "Temperatur");
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
    if (Environment.GetEnvironmentVariable("TRENDO_BRIEF_DESIGN") is { Length: > 0 } design) // 0.9.43: Bewerbung mit Design
    {
        d.Recipient = "Zahnarztpraxis Dr. Muster\nLindenweg 5\n10115 Berlin"; d.Subject = "Bewerbung als Zahnmedizinische Fachangestellte";
        d.Body = "mit großem Interesse habe ich Ihre Stellenanzeige gelesen. Als ausgebildete Zahnmedizinische Fachangestellte mit drei Jahren Erfahrung in Prophylaxe und Abrechnung möchte ich Ihr Team verstärken.\n\nÜber eine Einladung zu einem persönlichen Gespräch freue ich mich sehr.";
        win.LetterSetDesign(design, Environment.GetEnvironmentVariable("TRENDO_BRIEF_FARBE") ?? "#1F5FA8", "Zahnmedizinische Fachangestellte");
    }
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
for (var i = 0; i < (Environment.GetEnvironmentVariable("TRENDO_TILES") == "1" ? 200 : 40); i++) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(50); } // Kartenkacheln brauchen etwas länger
var frame = win.CaptureRenderedFrame();
if (Environment.GetEnvironmentVariable("TRENDO_DUMP") is { Length: > 0 } dump) // Lage benannter Bausteine ausgeben (Layout-Fehlersuche)
    foreach (var c in Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(win).OfType<Control>().Where(c => c.Name is { } n && dump.Split(',').Any(n.StartsWith)))
        Console.WriteLine($"{c.Name}: {c.Bounds} desired={c.DesiredSize} @ {c.TranslatePoint(new Point(0, 0), win)}");
frame!.Save(outPath);
Console.WriteLine($"OK {outPath}");
foreach (var crash in CrashLog.List(root).Take(1)) Console.Error.WriteLine("ABSTURZ beim Rendern:" + Environment.NewLine + File.ReadAllText(crash));
Directory.Delete(root, true);
sealed class NoopL : IProcessLauncher { public void Open(string t) { } }

/// <summary>Stumme Tonausgabe für Vorschaubilder.</summary>
sealed class PreviewAudio : IAudioOut
{
    public event Action<double, double>? Progress { add { } remove { } }
    public event Action? Ended { add { } remove { } }
    public void Load(string file) { } public void SetPause(bool pause) { } public void Seek(double seconds) { } public void SetVolume(int percent) { } public void Stop() { } public void Dispose() { }
}
