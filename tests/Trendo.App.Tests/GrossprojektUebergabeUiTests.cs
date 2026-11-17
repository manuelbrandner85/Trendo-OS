using System.Security.Cryptography;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Programs;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0743 (Audit X-01): Karte eines Großprojekts → Studio → Ergebnisdatei zurück. Echte Studios, echte Exporte (Bücher schreiben → Druck-PDF über TextPdf,
/// PDF-Werkstatt → Seitenzahlen über PdfTools), echte Klicks; ersetzt ist nichts außer dem Öffnen von Dateien im System (FakeLauncher des Testfensters).
/// Mit <c>TRENDO_BELEG_T0743=&lt;Ordner&gt;</c> legt der Ende-zu-Ende-Test Ergebnisdateien, Übergaben und Ablaufprotokoll dort ab (Beleg unter reports/T-0743).</summary>
public class GrossprojektUebergabe_Ui
{
    const string BookKey = "buch-programm";
    static T V<T>(MainWindow w, string name) where T : Control { Settle(); return w.GetVisualDescendants().OfType<T>().Last(c => c.Name == name && c.IsEffectivelyVisible && c.Bounds.Width > 0); }
    static void Click(MainWindow w, string name) { var c = V<Control>(w, name); c.BringIntoView(); Settle(); RealClick(w, V<Control>(w, name)); Settle(); }
    static void Enter(TextBox t, string v) { t.Text = v; t.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter }); Settle(); }
    static string AllText(string pdf) => string.Join(" ", Enumerable.Range(0, PdfView.PageCount(pdf)).Select(i => PdfView.Text(pdf, i)));
    static string Sha(string f) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f))).ToLowerInvariant();

    static (MainWindow W, string Root) Open(string? root = null)
    {
        root ??= Path.Combine(Path.GetTempPath(), "trendo-ui-" + Guid.NewGuid().ToString("N"));
        var core = new TrendoCore(root, new InMemorySecretStore(), new FakeLauncher(), offline: true);
        var w = new MainWindow(core, new AppOptions(root, null, 0, true, Isolated: true), new Probe("browser")) { Width = 1700, Height = 1200 };
        w.ProgramSetLevel(UserLevel.Erweitert); w.FreigabeConfirm = _ => Task.FromResult(true);
        w.Show(); Dispatcher.UIThread.RunJobs();
        return (w, root);
    }

    static BoardCard Card(MainWindow w, string title) => w.BigBoardNow!.Cards.Single(c => c.Title == title);

    static void NewProject(MainWindow w, string name, string goal, int recipe = 0)
    {
        w.Show("produktion"); Settle();
        w.Find<TextBox>("big-new-name").Text = name; w.Find<TextBox>("big-new-goal").Text = goal;
        w.Find<ComboBox>("big-new-recipe").SelectedIndex = recipe; w.Find<Button>("big-new-create").Press(); Settle();
    }

    static void AddCard(MainWindow w, string title, string detail, string studio)
    {
        Enter(w.Find<TextBox>("big-add-text"), title);
        Click(w, "big-c-detail"); w.KeyTextInput(detail); Settle(); Click(w, "big-c-title"); // tippen, dann woanders hin: gespeichert
        w.Find<ComboBox>("big-c-program").SelectedItem = studio; Settle();
        Assert.Equal(studio, MainWindow.Programs[Card(w, title).Program!].Title);
    }

    [AvaloniaFact(DisplayName = "Großprojekt Ende-zu-Ende: Karte → Bücher schreiben → Druck-PDF zurück, nächste Karte → PDF-Werkstatt mit dieser PDF → Seitenzahlen-PDF zurück, mit Vorschau und Prüfsumme")]
    public void Text_to_pdf_chain()
    {
        var (w, _) = Open(); var log = new List<string>();
        void L(string s) => log.Add($"{DateTime.Now:HH:mm:ss.fff} {s}");
        NewProject(w, "Hofladen Flyer", "Flyer für den Wochenmarkt");
        var pid = w.BigProject!; L("Großprojekt angelegt: " + pid);
        AddCard(w, "Flyer-Text schreiben", "Hofladen am Feldrand: frische Eier, Kartoffeln und Honig. Geöffnet Freitag und Samstag.", "Bücher schreiben");
        AddCard(w, "Flyer als PDF mit Seitenzahlen", "Seitenzahlen unten in die Mitte setzen.", "PDF-Werkstatt");
        Assert.Contains("1. Flyer-Text schreiben (Bücher schreiben)", w.Find<TextBlock>("big-chain").Text);
        L(w.Find<TextBlock>("big-chain").Text);

        // 1. Karte übergeben: Bücher schreiben öffnet sich mit vorbereitetem Dokument
        Click(w, $"big-card-{Card(w, "Flyer-Text schreiben").Id}");
        Click(w, "big-handover");
        Assert.Equal(BookKey, w.CurrentView);
        var book = w.ProgramActive(BookKey)!;
        Assert.Equal("Flyer-Text schreiben", book.Title);
        Assert.Contains("frische Eier, Kartoffeln und Honig", book.Text("text"));
        Assert.StartsWith(w.Core.Projects.ProjectFilesRoot(pid), book.Path!); // vorbereitetes Dokument liegt im Projekt
        var c1 = Card(w, "Flyer-Text schreiben");
        Assert.Equal((BigBoardHandoff.Handed, 1), (c1.Status, c1.Column));
        var h = BigBoardHandoff.Handoffs(w.Core.Projects, pid);
        Assert.Equal("auftrag", Assert.Single(h.OpenFor("STUDIO-BOOK-01")).Kind);
        L($"Karte 1 übergeben an Bücher schreiben, Dokument {Path.GetFileName(book.Path)}, Auftrag {c1.Order}");

        // im Studio weiterschreiben, dann Druck-PDF exportieren – die Datei geht von selbst zurück
        book.Set("text", book.Text("text") + "\nKommen Sie vorbei!\n", "Text ergänzt");
        Click(w, "prog-export-pdf");
        Assert.Contains("Zurück an die Karte „Flyer-Text schreiben“", w.ProgramMessage(BookKey));
        c1 = Card(w, "Flyer-Text schreiben");
        Assert.Equal((BigBoardHandoff.Done, 2), (c1.Status, c1.Column));
        var r1 = w.Core.Projects.ResolveInProject(pid, c1.Result!);
        Assert.Contains("Kommen Sie vorbei", AllText(r1));
        Assert.True(BigBoardHandoff.ResultIntact(w.Core.Projects, pid, c1));
        L($"Karte 1 erledigt: {c1.Result} ({new FileInfo(r1).Length} Bytes, SHA-256 {Sha(r1)})");

        // 2. Karte: PDF-Werkstatt bekommt das Ergebnis der Karte davor als Eingabe
        w.Show("produktion"); Settle();
        Click(w, $"big-card-{Card(w, "Flyer als PDF mit Seitenzahlen").Id}");
        Click(w, "big-handover");
        Assert.Equal(MainWindow.PwKey, w.CurrentView);
        var pw = w.ProgramActive(MainWindow.PwKey)!;
        Assert.Equal(r1, Assert.Single(pw.List("files")).AsText());
        Click(w, "prog-cmd-pw-tool-seitenzahlen");
        Assert.Equal("seitenzahlen", pw.Text("tool"));
        L("Karte 2 übergeben an PDF-Werkstatt, Eingabe " + Path.GetFileName(r1) + ", Werkzeug Seitenzahlen");
        Click(w, "prog-export-pdf");
        Assert.Contains("Zurück an die Karte „Flyer als PDF mit Seitenzahlen“", w.ProgramMessage(MainWindow.PwKey));
        var c2 = Card(w, "Flyer als PDF mit Seitenzahlen");
        Assert.Equal(BigBoardHandoff.Done, c2.Status);
        var r2 = w.Core.Projects.ResolveInProject(pid, c2.Result!);
        var t2 = AllText(r2);
        Assert.Contains("Seite 1 von", t2); Assert.Contains("Kommen Sie vorbei", t2);
        Assert.NotEqual(Sha(r1), Sha(r2));
        Assert.Empty(h.OpenFor(null)); // alle Übergaben angenommen
        Assert.Equal(4, h.All().Count);
        L($"Karte 2 erledigt: {c2.Result} ({new FileInfo(r2).Length} Bytes, SHA-256 {Sha(r2)})");

        // Großprojekte: Status „erledigt“ und Vorschau an der Karte
        w.Show("produktion"); Settle();
        Click(w, $"big-card-{c2.Id}");
        Assert.StartsWith("✓ erledigt", w.Find<TextBlock>("big-c-status").Text);
        Assert.NotNull(V<Border>(w, "big-result-preview"));
        Assert.Contains("Prüfsumme stimmt", w.Find<TextBlock>("big-result-name").Text);
        for (var i = 0; i < 200 && w.Find<Image>("big-result-image").Source is null; i++) { Thread.Sleep(20); Settle(); }
        Assert.NotNull(w.Find<Image>("big-result-image").Source); // erste Seite gerendert
        Assert.Equal(100, w.BigBoardNow!.Percent);

        if (Environment.GetEnvironmentVariable("TRENDO_BELEG_T0743") is { Length: > 0 } dir)
        {
            Directory.CreateDirectory(dir);
            File.Copy(r1, Path.Combine(dir, "karte1-ergebnis.pdf"), true); File.Copy(r2, Path.Combine(dir, "karte2-ergebnis.pdf"), true);
            File.WriteAllText(Path.Combine(dir, "uebergaben.json"), File.ReadAllText(Path.Combine(w.Core.Projects.ProjectFilesRoot(pid), ".trendo", ProjectHandoff.FileName)).Replace("\r\n", "\n"));
            File.WriteAllText(Path.Combine(dir, "tafel.json"), File.ReadAllText(w.Core.Projects.ResolveInProject(pid, BigBoards.File)).Replace("\r\n", "\n"));
            File.WriteAllText(Path.Combine(dir, "auftrag-karte1.md"), w.Core.Projects.ReadFile(pid, c1.Order!).Replace("\r\n", "\n"));
            File.WriteAllText(Path.Combine(dir, "ablauf.log"), string.Join("\n", log) + "\n");
        }
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Großprojekt: nach einem Neustart kommt der Export des wieder geöffneten Dokuments an die Karte zurück")]
    public void Return_after_restart()
    {
        var (w, root) = Open();
        NewProject(w, "Neustart", "");
        AddCard(w, "Kapitel", "Ein Absatz.", "Bücher schreiben");
        var cid = Card(w, "Kapitel").Id; var pid = w.BigProject!;
        Click(w, $"big-card-{cid}"); Click(w, "big-handover");
        w.Close();

        var (w2, _) = Open(root);
        w2.Show("produktion"); Settle();
        w2.BigProject = pid; w2.Show("produktion"); Settle();
        Click(w2, $"big-card-{cid}");
        Assert.StartsWith("→ übergeben an Bücher schreiben", w2.Find<TextBlock>("big-c-status").Text);
        Click(w2, "big-open-studio");
        Assert.Equal(BookKey, w2.CurrentView);
        Click(w2, "prog-export-docx");
        var c = w2.BigBoardNow!.Cards.Single(x => x.Id == cid);
        Assert.Equal(BigBoardHandoff.Done, c.Status);
        Assert.EndsWith(".docx", c.Result);
        w2.Close();
    }

    [AvaloniaFact(DisplayName = "Großprojekt-Kette frei: Vorlage bekommt Studios, Rechtsklick und Alt+↑ sortieren um, Strg+Enter übergibt, abgelegte Datei wird Ergebnis")]
    public void Free_chain_and_handling()
    {
        var (w, _) = Open();
        var recipe = Fusion.Recipes.ToList().FindIndex(r => r.Id == "figur-spiel");
        NewProject(w, "Spiel", "Kleines Spiel", recipe + 1);
        var cards = w.BigBoardNow!.Cards;
        Assert.Equal(["Bildbearbeitung", "Spiele bauen", "Spiele bauen"], cards.Take(3).Select(c => MainWindow.Programs[c.Program!].Title));
        Assert.Null(cards[3].Program); // „Qualität“ ist kein Programm – die Person wählt selbst
        var last = cards[^1].Id; var first = cards[0].Id;

        // Rechtsklick: früher in der Kette
        Click(w, $"big-card-{last}");
        var card = V<Border>(w, $"big-card-{last}"); card.BringIntoView(); Settle();
        var p = card.TranslatePoint(new Point(card.Bounds.Width / 2, 12), w)!.Value;
        w.MouseDown(p, MouseButton.Right); w.MouseUp(p, MouseButton.Right); Settle();
        var menu = V<Border>(w, $"big-card-{last}").ContextMenu!;
        Assert.True(menu.IsOpen);
        menu.Items.OfType<MenuItem>().Single(m => m.Name == "big-m-up").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); Settle(); menu.Close(); Settle();
        Assert.True(last == w.BigBoardNow!.Cards[^2].Id, string.Join(",", w.BigBoardNow!.Cards.Select(c => c.Id)) + " last=" + last);

        // Alt+↑ an der Tafel
        V<Grid>(w, "big-window").Focus(); Settle();
        w.KeyPress(Key.Up, RawInputModifiers.Alt, PhysicalKey.ArrowUp, null); w.KeyRelease(Key.Up, RawInputModifiers.Alt, PhysicalKey.ArrowUp, null); Settle();
        Assert.Equal(last, w.BigBoardNow!.Cards[^3].Id);

        // Strg+Enter übergibt die erste Karte an die Bildbearbeitung
        w.Show("produktion"); Settle(); Click(w, $"big-card-{first}");
        V<Grid>(w, "big-window").Focus(); Settle();
        w.KeyPress(Key.Enter, RawInputModifiers.Control, PhysicalKey.Enter, null); w.KeyRelease(Key.Enter, RawInputModifiers.Control, PhysicalKey.Enter, null); Settle();
        Assert.Equal(BigBoardHandoff.Handed, Card2(w, first).Status);

        // eine fertige Datei auf die übergebene Karte ziehen: sie wird das Ergebnis
        w.Show("produktion"); Settle();
        var png = Path.Combine(Path.GetTempPath(), "trendo-figur-" + Guid.NewGuid().ToString("N")[..6] + ".png");
        using (var bmp = new SkiaSharp.SKBitmap(16, 16)) { bmp.Erase(SkiaSharp.SKColors.Gold); using var data = bmp.Encode(SkiaSharp.SKEncodedImageFormat.Png, 90); File.WriteAllBytes(png, data.ToArray()); }
        BueroAusbau_Ui.Drop(w, $"big-card-{first}", png);
        var done = w.BigBoardNow!.Cards.Single(c => c.Id == first);
        Assert.Equal(BigBoardHandoff.Done, done.Status);
        Assert.Equal(Sha(png), done.ResultSha!.ToLowerInvariant());
        w.Close();
    }

    static BoardCard Card2(MainWindow w, string id) => BigBoards.Load(w.Core.Projects, w.BigProject!).Cards.Single(c => c.Id == id);
}
