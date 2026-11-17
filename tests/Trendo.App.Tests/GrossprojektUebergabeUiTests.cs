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

    // ───── Gegenprüfung T-0743 ─────

    /// <summary>Übergaben der Großprojekte (Absender oder Empfänger X-01) im Projekt.</summary>
    static List<HandoffItem> BoardHandoffs(MainWindow w, string pid) =>
        [.. BigBoardHandoff.Handoffs(w.Core.Projects, pid).All().Where(x => x.From == BigBoardHandoff.Board || x.To == BigBoardHandoff.Board)];
    static string[] FilesIn(MainWindow w, string pid, string rel)
    {
        var d = Path.Combine(w.Core.Projects.ProjectFilesRoot(pid), rel);
        return Directory.Exists(d) ? [.. Directory.GetFiles(d).Select(Path.GetFileName).Order()!] : [];
    }

    [AvaloniaFact(DisplayName = "Gegenprüfung 1: übergeben → Ergebnis → Rückgängig → Rückgängig → Wiederholen ×2 – Tafel, uebergaben.json und Dateien stimmen bei jedem Schritt überein")]
    public void Undo_keeps_board_and_register_in_step()
    {
        var (w, _) = Open();
        NewProject(w, "Rueckgaengig", "");
        AddCard(w, "Kapitel", "Ein Absatz.", "Bücher schreiben");
        var cid = Card(w, "Kapitel").Id; var pid = w.BigProject!;
        Click(w, $"big-card-{cid}"); Click(w, "big-handover");
        Click(w, "prog-export-pdf");
        var done = Card2(w, cid);
        Assert.Equal(BigBoardHandoff.Done, done.Status);
        var resultFile = done.Result!; var orderFile = done.Order!; var docFile = done.Doc!; var order = done.Handoff!;
        Assert.Equal(2, BoardHandoffs(w, pid).Count);

        // „Rückgängig“ an der Tafel: das Ergebnis geht zurück, die Karte ist wieder „übergeben“
        w.Show("produktion"); Settle();
        Click(w, "big-undo");
        var c = Card2(w, cid);
        Assert.Equal((BigBoardHandoff.Handed, (string?)null, (string?)order), (c.Status, c.Result, c.Handoff));
        var a = Assert.Single(BoardHandoffs(w, pid)); // nur noch der Auftrag – das Ergebnis ist aus dem Register
        Assert.Equal((order, "auftrag", (DateTimeOffset?)null), (a.Id, a.Kind, a.Received)); // Auftrag wieder offen
        Assert.Single(BigBoardHandoff.Handoffs(w.Core.Projects, pid).OpenFor("STUDIO-BOOK-01"));
        Assert.Empty(FilesIn(w, pid, "grossprojekt/belege")); // keine verwaiste Ergebnisdatei
        Assert.False(File.Exists(w.Core.Projects.ResolveInProject(pid, resultFile)));

        // noch einmal: die Übergabe geht zurück
        w.BigUndo(); Settle();
        c = Card2(w, cid);
        Assert.Equal((BigBoardHandoff.Open, (string?)null, (string?)null), (c.Status, c.Handoff, c.Doc));
        Assert.Empty(BoardHandoffs(w, pid));
        Assert.Empty(FilesIn(w, pid, "grossprojekt/auftraege"));

        // Wiederholen bringt Übergabe und Ergebnis mit denselben Dateien und Einträgen zurück
        w.BigRedo(); Settle();
        c = Card2(w, cid);
        Assert.Equal((BigBoardHandoff.Handed, (string?)order), (c.Status, c.Handoff));
        Assert.True(File.Exists(w.Core.Projects.ResolveInProject(pid, orderFile)));
        Assert.True(File.Exists(w.Core.Projects.ResolveInProject(pid, docFile)));
        Assert.Null(Assert.Single(BoardHandoffs(w, pid)).Received);
        w.BigRedo(); Settle();
        c = Card2(w, cid);
        Assert.Equal((BigBoardHandoff.Done, (string?)resultFile), (c.Status, c.Result));
        Assert.True(BigBoardHandoff.ResultIntact(w.Core.Projects, pid, c));
        var reg = BoardHandoffs(w, pid);
        Assert.Equal(2, reg.Count); Assert.All(reg, x => Assert.NotNull(x.Received));
        Assert.Equal(resultFile, reg.Single(x => x.Kind == "ergebnis").File);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Gegenprüfung 2: eine kaputte Tafel in einem anderen Projekt stört den Export eines normalen Dokuments nicht")]
    public void Broken_board_elsewhere_does_not_break_export()
    {
        var (w, _) = Open();
        var (bad, _) = BigBoards.Create(w.Core.Projects, "Kaputt", "");
        w.Core.Projects.WriteFile(bad.Id, BigBoards.File, "{\"Goal\":\"\",\"Cards\":[{\"Id\":\"aa11bb22\",\"Title\":\"T\",\"Doc\":\"../../x\"},{\"Id\":\"cc33dd44\",\"Title\":\"U\",\"Doc\":\"C:/x\"}],\"Trash\":[]}");
        var d = w.ProgramNew(BookKey); d.Title = "Normaler Brief"; d.Set("text", "Hallo", "Text"); w.Show(BookKey); Settle();
        Click(w, "prog-export-docx");
        Assert.StartsWith("Exportiert: ", w.ProgramMessage(BookKey));
        Assert.True(File.Exists(w.ProgramMessage(BookKey)["Exportiert: ".Length..].Trim()));

        // auch ein Dokument, das im kaputten Projekt bei den Aufträgen liegt: Export klappt, keine Ausnahme
        Directory.CreateDirectory(w.Core.Projects.ResolveInProject(bad.Id, "grossprojekt/auftraege"));
        w.ProgramSave(BookKey, w.Core.Projects.ResolveInProject(bad.Id, "grossprojekt/auftraege/aa11bb22-T" + ProgramDocument.Extension));
        Click(w, "prog-export-docx");
        Assert.StartsWith("Exportiert: ", w.ProgramMessage(BookKey));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Gegenprüfung 3: erneuter Export wird neue Fassung (alte bleibt als Beleg), „Karte wieder öffnen“ übergibt erneut")]
    public void Second_export_is_new_version_and_reopen()
    {
        var (w, _) = Open();
        NewProject(w, "Fassungen", "");
        AddCard(w, "Kapitel", "Ein Absatz.", "Bücher schreiben");
        var cid = Card(w, "Kapitel").Id; var pid = w.BigProject!;
        Click(w, $"big-card-{cid}"); Click(w, "big-handover");
        var book = w.ProgramActive(BookKey)!;
        Click(w, "prog-export-pdf");
        var first = Card2(w, cid).Result!;

        // im Studio weiter, nochmal exportieren: neue Fassung an der Karte
        book.Set("text", book.Text("text") + "\nZweite Fassung.\n", "Text ergänzt");
        Click(w, "prog-export-pdf");
        Assert.Contains("neue Fassung", w.ProgramMessage(BookKey));
        var c = Card2(w, cid);
        Assert.NotEqual(first, c.Result);
        Assert.Contains("Zweite Fassung", AllText(w.Core.Projects.ResolveInProject(pid, c.Result!)));
        Assert.True(File.Exists(w.Core.Projects.ResolveInProject(pid, first))); // die alte bleibt als Beleg
        Assert.Contains(c.Evidence, e => e.Path == first);
        Assert.Equal(2, c.Evidence.Count);
        Assert.True(BigBoardHandoff.ResultIntact(w.Core.Projects, pid, c));
        Assert.Equal(2, BoardHandoffs(w, pid).Count(x => x.Kind == "ergebnis"));

        // Großprojekte: „Karte wieder öffnen“ – Studio öffnet das Dokument, Karte läuft wieder
        w.Show("produktion"); Settle(); Click(w, $"big-card-{cid}");
        Assert.Contains("Fassung 2", w.Find<TextBlock>("big-c-status").Text);
        Click(w, "big-reopen");
        Assert.Equal(BookKey, w.CurrentView);
        Assert.Contains("Zweite Fassung", w.ProgramActive(BookKey)!.Text("text")); // dasselbe Dokument, nichts verloren
        c = Card2(w, cid);
        Assert.Equal((BigBoardHandoff.Handed, 1), (c.Status, c.Column));
        Assert.Single(BigBoardHandoff.Handoffs(w.Core.Projects, pid).OpenFor("STUDIO-BOOK-01")); // neuer Auftrag
        Click(w, "prog-export-docx");
        c = Card2(w, cid);
        Assert.Equal(BigBoardHandoff.Done, c.Status); Assert.EndsWith(".docx", c.Result);
        Assert.Equal(3, c.Evidence.Count);
        Assert.Empty(BigBoardHandoff.Handoffs(w.Core.Projects, pid).OpenFor(null));

        // Rückgängig nimmt die letzte Fassung zurück, Register bleibt passend
        w.BigUndo(); Settle();
        c = Card2(w, cid);
        Assert.Equal(BigBoardHandoff.Handed, c.Status);
        Assert.Single(BigBoardHandoff.Handoffs(w.Core.Projects, pid).OpenFor("STUDIO-BOOK-01"));
        Assert.Equal(2, BoardHandoffs(w, pid).Count(x => x.Kind == "ergebnis"));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Gegenprüfung 5: Alt+↓ in der aufgeklappten Studio-Auswahl sortiert die Kette nicht um")]
    public void Alt_arrow_in_open_combobox_keeps_chain()
    {
        var (w, _) = Open();
        NewProject(w, "Auswahl", "");
        AddCard(w, "Eins", "a", "Bücher schreiben");
        AddCard(w, "Zwei", "b", "PDF-Werkstatt");
        var first = Card(w, "Eins").Id;
        Click(w, $"big-card-{first}");
        var cb = V<ComboBox>(w, "big-c-program");
        cb.Focus(); cb.IsDropDownOpen = true; Settle();
        w.KeyPress(Key.Down, RawInputModifiers.Alt, PhysicalKey.ArrowDown, null); w.KeyRelease(Key.Down, RawInputModifiers.Alt, PhysicalKey.ArrowDown, null); Settle();
        Assert.Equal(first, w.BigBoardNow!.Cards[0].Id);
        // an der zugeklappten Auswahl ebenso: Alt+↓ gehört der Auswahl, die Kette bleibt
        cb = V<ComboBox>(w, "big-c-program"); cb.IsDropDownOpen = false; Settle(); cb.Focus(); Settle();
        w.KeyPress(Key.Down, RawInputModifiers.Alt, PhysicalKey.ArrowDown, null); w.KeyRelease(Key.Down, RawInputModifiers.Alt, PhysicalKey.ArrowDown, null); Settle();
        Assert.Equal(first, w.BigBoardNow!.Cards[0].Id);
        w.Close();
    }
}
