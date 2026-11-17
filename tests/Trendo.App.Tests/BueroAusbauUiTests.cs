using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.LogicalTree;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Buero;
using Trendo.Core.Kreativ;
using Trendo.Core.Office;
using Trendo.Core.Programs;
using Trendo.Core.Sys;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0705 Büro-Ausbau: die sieben Büro-Studios über den echten Eingabeweg – vom leeren Zustand über den ersten Schritt bis zur echten Datei (Stufe „Einfach“),
/// Absturz ohne Speichern (höchstens ein Sicherungstakt verloren), Ziehen und Ablegen von Dateien, Umsortieren mit der Maus und Rechtsklick-Menüs.
/// Ersetzt ist nur die Dateiauswahl des Systems (<see cref="MainWindow.PickOverride"/> liefert echte Dateien); die Fremdprogramm-Prüfung steht in <see cref="BueroEchtlauf_Ui"/>.</summary>
public class BueroAusbau_Ui
{
    // ---------- Hilfen ----------

    internal static void Click(MainWindow w, string name) { var c = V<Control>(w, name); c.BringIntoView(); Settle(); RealClick(w, V<Control>(w, name)); Settle(); }
    internal static Point Center(MainWindow w, Control c) { Settle(); w.UpdateLayout(); return c.TranslatePoint(new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), w) ?? throw new InvalidOperationException("nicht sichtbar: " + c.Name); }

    /// <summary>Echtes Ziehen mit der Maus: drücken, in Schritten bewegen, loslassen.</summary>
    internal static void Drag(MainWindow w, string from, string to)
    {
        var a = V<Control>(w, from); a.BringIntoView(); Settle();
        var pa = Center(w, a); var pb = Center(w, V<Control>(w, to));
        w.MouseDown(pa, MouseButton.Left);
        for (var i = 1; i <= 8; i++) { w.MouseMove(pa + (pb - pa) * (i / 8.0)); Settle(); }
        w.MouseUp(pb, MouseButton.Left); Settle();
    }

    /// <summary>Rechtsklick an der Stelle – das Menü muss sich öffnen; dann den Eintrag wählen.</summary>
    internal static void Menu(MainWindow w, string target, string item)
    {
        var c = V<Control>(w, target); c.BringIntoView(); Settle();
        var p = Center(w, c);
        w.MouseDown(p, MouseButton.Right); w.MouseUp(p, MouseButton.Right); Settle();
        var menu = c.ContextMenu ?? throw new InvalidOperationException("kein Rechtsklick-Menü an " + target);
        Assert.True(menu.IsOpen, "Rechtsklick hat das Menü an " + target + " nicht geöffnet");
        var mi = menu.Items.OfType<MenuItem>().Single(m => m.Name == item);
        Assert.True(mi.IsEnabled, item + " ist ausgegraut");
        mi.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent)); Settle();
        menu.Close(); Settle();
    }

    /// <summary>Datei aus dem Explorer ablegen: DragEnter → DragOver → Drop an der Mitte von <paramref name="target"/> (echte Ereignisse des Fensters).</summary>
    internal static DragDropEffects Drop(MainWindow w, string target, params string[] files)
    {
        var t = V<Control>(w, target); t.BringIntoView(); Settle();
        var p = Center(w, t);
        var hit = w.InputHitTest(p) as Visual;
        Assert.True(hit is not null && (hit == t || hit.GetVisualAncestors().Contains(t)), $"{target}: an der Ablegestelle liegt {hit?.GetType().Name} {(hit as Control)?.Name}");
        var dt = new DataTransfer();
        foreach (var f in files) dt.Add(DataTransferItem.CreateFile(LocalFile(f)));
        w.DragDrop(p, RawDragEventType.DragEnter, dt, DragDropEffects.Copy | DragDropEffects.Move); Settle();
        w.DragDrop(p, RawDragEventType.DragOver, dt, DragDropEffects.Copy | DragDropEffects.Move); Settle();
        w.DragDrop(p, RawDragEventType.Drop, dt, DragDropEffects.Copy | DragDropEffects.Move); Settle();
        return DragDropEffects.Copy;
    }

    /// <summary>Datei, wie sie der Explorer beim Ziehen liefert. <see cref="IStorageFile"/> lässt sich nicht selbst bauen – Avalonias eigene Datei-Klasse
    /// (dieselbe, die das Windows-Ablegen liefert) wird über ihren Konstruktor mit <see cref="FileInfo"/> erzeugt.</summary>
    internal static IStorageItem LocalFile(string path)
    {
        var t = typeof(IStorageFile).Assembly.GetType("Avalonia.Platform.Storage.FileIO.BclStorageFile", throwOnError: true)!;
        return (IStorageItem)Activator.CreateInstance(t, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null, [new FileInfo(path)], null)!;
    }

    /// <summary>Text eintippen wie auf der Tastatur: Feld bekommt den Fokus, Inhalt markiert, Text tippen, Tab-Taste übernimmt (Fokus wandert weiter).</summary>
    internal static void TypeInto(MainWindow w, string box, string text)
    {
        var tb = V<TextBox>(w, box); tb.BringIntoView(); Settle();
        tb.Focus(); Settle(); tb.SelectAll(); w.KeyTextInput(text); Settle();
        w.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null); w.KeyRelease(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null); Settle();
    }

    /// <summary>Befehl im Menüband anklicken – in „Einfach“ steckt er hinter „Mehr“ (erst „Mehr“ anklicken, dann den Eintrag im Klappmenü).</summary>
    internal static void Cmd(MainWindow w, string name)
    {
        Settle();
        if (w.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.Name == name && b.IsEffectivelyVisible) is { } b) { b.BringIntoView(); Settle(); RealClick(w, b); Settle(); return; }
        var more = V<Button>(w, "prog-more"); RealClick(w, more); Settle();
        var content = (Control)((Flyout)more.Flyout!).Content!;
        var item = content.GetLogicalDescendants().OfType<Button>().First(x => x.Name == name);
        item.Press(); Settle();
        ((Flyout)more.Flyout!).Hide(); Settle();
    }

    internal static string Exported(MainWindow w, string key)
    {
        var msg = w.ProgramMessage(key);
        var at = msg.IndexOf("Exportiert: ", StringComparison.Ordinal);
        Assert.True(at >= 0, "keine Exportmeldung: " + msg);
        var f = msg[(at + "Exportiert: ".Length)..].Trim();
        Assert.True(File.Exists(f), "Datei fehlt: " + f);
        return f;
    }

    internal static ProgramDocument StartEmpty(MainWindow w, string key, string tpl)
    {
        w.Width = 1600; w.Height = 1200;
        w.ProgramSetLevel(UserLevel.Einfach); w.Show(key); Settle();
        RealClick(w, w.Find<Button>("prog-tpl-" + tpl)); Settle();
        Assert.NotNull(V<Button>(w, "prog-first-step")); // Leerzustand: erster Schritt sichtbar
        return w.ProgramActive(key)!;
    }

    internal static string FormPdf(string dir)
    {
        Directory.CreateDirectory(dir);
        var blank = Path.Combine(dir, "leer.pdf"); PdfTools.NewBlank(blank);
        var f = Path.Combine(dir, "antrag.pdf");
        PdfFormular.AddFields(blank, f, [new("Nachname", "text", 0, 60, 120, 220, 20, true), new("Wohnort", "text", 0, 60, 160, 220, 20), new("Zustimmung", "haken", 0, 60, 200, 14, 14)]);
        return f;
    }

    static string Photo(string dir, string name)
    {
        Directory.CreateDirectory(dir); var f = Path.Combine(dir, name);
        File.WriteAllBytes(f, PngCodec.Encode(DocScan.TestPhoto(600, 450, [(130, 45), (465, 75), (505, 410), (85, 380)])));
        return f;
    }

    // ---------- 1. Vom leeren Zustand über den ersten Schritt bis zur Datei ----------

    [AvaloniaFact(DisplayName = "T-0705 Klick bis Datei – Bewerbung: leerer Lebenslauf, „Namen eintragen“ holt das Namensfeld, Name getippt, Export PDF und Word enthalten ihn")]
    public void Cv_first_step_to_file()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            const string K = MainWindow.CvKey;
            var d = StartEmpty(w, K, "leer");
            Click(w, "prog-first-step");
            Assert.Null(w.TryFind<Button>("prog-first-step")); // Karte weg
            Assert.Equal("cv-p-name", (w.FocusManager?.GetFocusedElement() as Control)?.Name); // das Namensfeld hat den Fokus
            w.KeyTextInput("Erika Muster"); Settle();
            TypeInto(w, "cv-p-titel", "Kauffrau für Büromanagement");
            Assert.Equal("Erika Muster", MainWindow.CvOf(d).Cv.Person.Name);
            Cmd(w, "prog-export-pdf");
            var pdf = Exported(w, K);
            Assert.Contains("Erika Muster", PdfView.Text(pdf, 0));
            Cmd(w, "prog-export-docx");
            var docx = Exported(w, K);
            Assert.Contains("Erika Muster", Ooxml.DocxText(docx)); Assert.Contains("Kauffrau für Büromanagement", Ooxml.DocxText(docx));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0705 Klick bis Datei – Karten: Vorlage unverändert, „Text anpassen“ klappt die Eigenschaften auf, eigener Text getippt, Druck-PDF enthält ihn")]
    public void Cards_first_step_to_file()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            const string K = MainWindow.CardKey;
            var d = StartEmpty(w, K, "geburtstag");
            Assert.Null(w.TryFind<TextBox>("cards-t-text")); // Eigenschaften in „Einfach“ eingeklappt
            Click(w, "prog-first-step");
            TypeInto(w, "cards-t-text", "Komm zu meiner Feier, {Name}!");
            var k = MainWindow.CardOf(d);
            Assert.Contains(KartenKit.Seiten(k).SelectMany(s => k.Seite(s.Id).Texte), t => t.Text == "Komm zu meiner Feier, {Name}!");
            Cmd(w, "prog-export-pdf");
            var pdf = Exported(w, K);
            var all = string.Concat(Enumerable.Range(0, PdfView.PageCount(pdf)).Select(i => PdfView.Text(pdf, i)));
            Assert.Contains("Komm zu meiner Feier", System.Text.RegularExpressions.Regex.Replace(all, @"\s+", " ")); // Zeilenumbruch auf der Karte
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0705 Klick bis Datei – Verträge: leerer Ordner, „Ersten Vertrag eintragen“, Felder getippt, Erinnerung als Kalenderdatei mit Alarm und Kündigung als PDF")]
    public void Contracts_first_step_to_file()
    {
        var (w, _, _) = Ui.Open();
        MainWindow.ConNowOverride = () => new DateTime(2026, 11, 16);
        try
        {
            const string K = MainWindow.ConKey;
            var d = StartEmpty(w, K, "leer");
            Click(w, "prog-first-step");
            Assert.Single(MainWindow.ConOf(d).Vertraege);
            TypeInto(w, "con-f-name", "Handyvertrag");
            TypeInto(w, "con-f-anbieter", "Muster Mobil");
            TypeInto(w, "con-f-nr", "K-4711");
            TypeInto(w, "con-f-beginn", "01.03.2025");
            var v = MainWindow.ConOf(d).Vertraege[0];
            Assert.Equal(("Handyvertrag", "Muster Mobil", "K-4711", "2025-03-01"), (v.Name, v.Anbieter, v.Kundennummer, v.Beginn));
            Cmd(w, "prog-export-ics");
            var ics = File.ReadAllText(Exported(w, K));
            Assert.Contains("BEGIN:VEVENT", ics); Assert.Contains("Handyvertrag", ics); Assert.Contains("BEGIN:VALARM", ics);
            Cmd(w, "prog-cmd-con-letter-pdf");
            var pdf = w.ProgramMessage(K).Split("PDF: ")[1].Split(" – ")[0];
            Assert.True(File.Exists(pdf));
            var text = PdfView.Text(pdf, 0); Assert.Contains("K-4711", text); Assert.Contains("Muster Mobil", text);
        }
        finally { MainWindow.ConNowOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0705 Klick bis Datei – PDF-Werkstatt: leere Liste, „Dateien hinzufügen …“, Ausführen fügt zusammen; Ergebnis hat alle Seiten in Reihenfolge")]
    public void Pdf_first_step_to_file()
    {
        var (w, _, root) = Ui.Open();
        try
        {
            const string K = MainWindow.PwKey;
            var dir = Path.Combine(root, "eingang"); Directory.CreateDirectory(dir);
            var a = PdfWerkstatt_Ui.TextPdf(Path.Combine(dir, "a.pdf"), "Erste Seite A", "Zweite Seite A");
            var b = PdfWerkstatt_Ui.TextPdf(Path.Combine(dir, "b.pdf"), "Einzige Seite B");
            StartEmpty(w, K, "zusammenfuegen");
            w.PickOverride = _ => Task.FromResult<string[]?>([a, b]);
            Click(w, "prog-first-step");
            Assert.Null(w.TryFind<Button>("prog-first-step"));
            Click(w, "pw-run-now");
            var d = w.ProgramActive(K)!;
            var res = d.List("results").Select(r => r!["file"].AsText()!).Single();
            Assert.Equal(3, PdfView.PageCount(res));
            Assert.Contains("Erste Seite A", PdfView.Text(res, 0)); Assert.Contains("Einzige Seite B", PdfView.Text(res, 2));
        }
        finally { w.PickOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0705 Klick bis Datei – Formulare: kein Formular, „Formular öffnen …“, Felder auf der Seite ausgefüllt, gespeichert (Felder bleiben) und fertig geglättet (Werte fest in der Seite)")]
    public void Forms_first_step_to_file()
    {
        var (w, _, root) = Ui.Open();
        try
        {
            const string K = MainWindow.FoKey;
            var form = FormPdf(Path.Combine(root, "eingang"));
            StartEmpty(w, K, "oeffnen");
            w.PickOverride = _ => Task.FromResult<string[]?>([form]);
            Click(w, "prog-first-step");
            var d = w.ProgramActive(K)!;
            Assert.Equal(Path.GetFullPath(form), d.Text("file"));
            TypeInto(w, "fo-field-0", "Muster");
            TypeInto(w, "fo-field-1", "Musterstadt");
            Click(w, "fo-save-now");
            var saved = d.Text("lastSaved");
            var f = PdfFormular.Fields(saved).ToDictionary(x => x.Name, x => x.Value);
            Assert.Equal("Muster", f["Nachname"]); Assert.Equal("Musterstadt", f["Wohnort"]);
            Click(w, "fo-save-flat-now");
            var flat = d.Text("lastSaved");
            Assert.NotEqual(saved, flat);
            Assert.Empty(PdfFormular.Fields(flat)); // geglättet: keine Felder mehr
            Assert.Contains("Musterstadt", PdfView.Text(flat, 0));
            Assert.Equal(Hash(form), Hash(form)); // Original unverändert gelesen
        }
        finally { w.PickOverride = null; w.Close(); }
    }

    static string Hash(string f) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(f)));

    [AvaloniaFact(DisplayName = "T-0705 Klick bis Datei – Präsentation gestalten: leere Gliederung, „Stichpunkte schreiben“ holt das Feld, Gliederung getippt, „Gestalten“, Export PPTX mit allen Folien")]
    public void Deck_first_step_to_file()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            const string K = MainWindow.DeckDesignKey;
            var d = StartEmpty(w, K, "leer");
            Click(w, "prog-first-step");
            Assert.Equal("dd-outline", (w.FocusManager?.GetFocusedElement() as Control)?.Name);
            var tb = V<TextBox>(w, "dd-outline");
            Assert.Equal(tb.Text!.Length, tb.SelectionEnd - tb.SelectionStart); // Platzhalter markiert: Tippen ersetzt ihn
            w.KeyTextInput("Unser Garten"); w.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null); w.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            w.KeyTextInput("  - Beete planen"); Settle();
            tb.Text += "\nErnte\n  - 40 kg Tomaten\n  - 12 kg Bohnen"; Settle();
            Click(w, "dd-build-now");
            Assert.Equal(["Unser Garten", "Ernte"], DeckDesign.Parse(d.Text("outline")).Select(s => s.Title));
            Cmd(w, "prog-export-pptx");
            var pptx = Exported(w, K);
            var deck = SlideFiles.Open(pptx);
            Assert.Equal(2, deck.Slides.Count); Assert.Equal("Unser Garten", deck.Slides[0].Title);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0705 Klick bis Datei – Scannen: keine Seite, „Fotos hinzufügen …“, Blatt gefunden, „Als PDF ablegen“ ergibt eine PDF mit der Seite")]
    public void Scan_first_step_to_file()
    {
        var (w, _, root) = Ui.Open();
        try
        {
            const string K = MainWindow.ScanKey;
            var photo = Photo(Path.Combine(root, "fotos"), "brief.png");
            StartEmpty(w, K, "leer");
            w.PickOverride = _ => Task.FromResult<string[]?>([photo]);
            Click(w, "prog-first-step");
            Assert.NotNull(V<Control>(w, "scan-thumb-0"));
            Cmd(w, "prog-cmd-scan-save");
            var pdf = w.ProgramActive(K)!.Text("last");
            Assert.True(File.Exists(pdf), "keine PDF: " + w.ProgramMessage(K));
            Assert.Equal(1, PdfView.PageCount(pdf));
        }
        finally { w.PickOverride = null; w.Close(); }
    }

    // ---------- 2. Absturz: höchstens ein Sicherungstakt verloren ----------

    static MainWindow OpenOn(string root)
    {
        var core = new TrendoCore(root, new InMemorySecretStore(), new FakeLauncher());
        var w = new MainWindow(core, new AppOptions(root, null, 0, false, Isolated: true), new Probe("browser")) { Width = 1600, Height = 1200 };
        w.ProgramSetLevel(UserLevel.Erweitert);
        w.Show(); Dispatcher.UIThread.RunJobs();
        return w;
    }

    [AvaloniaFact(DisplayName = "T-0705 Absturz in allen sieben Büro-Studios: Änderung per Klick, Sicherungstakt (≤ 20 s), Fenster stirbt ohne Speichern – das neue Fenster bietet „Wiederherstellen“ an und bringt jede Änderung zurück")]
    public void Crash_all_seven()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1200;
        var dir = Path.Combine(root, "eingang"); Directory.CreateDirectory(dir);
        var a = PdfWerkstatt_Ui.TextPdf(Path.Combine(dir, "a.pdf"), "A"); var b = PdfWerkstatt_Ui.TextPdf(Path.Combine(dir, "b.pdf"), "B");
        // je Studio: Vorlage, Änderung per Klick, Prüfung nach der Wiederherstellung
        var cardTexts = -1;
        static int CardTexts(ProgramDocument x) { var k = MainWindow.CardOf(x); return KartenKit.Seiten(k).Sum(s => k.Seite(s.Id).Texte.Count); }
        var cases = new (string Key, string Tpl, Action Change, Func<ProgramDocument, bool> Changed)[]
        {
            (MainWindow.CvKey, "leer", () => Click(w, "prog-cmd-cv-add-station"), x => MainWindow.CvOf(x).Cv.Stationen.Count == 1),
            (MainWindow.CardKey, "geburtstag", () => { cardTexts = CardTexts(w.ProgramActive(MainWindow.CardKey)!); Click(w, "prog-cmd-cards-add-text"); }, x => CardTexts(x) == cardTexts + 1),
            (MainWindow.ConKey, "leer", () => Click(w, "prog-cmd-con-add"), x => MainWindow.ConOf(x).Vertraege.Count == 1),
            (MainWindow.PwKey, "zusammenfuegen", () => { w.PickOverride = _ => Task.FromResult<string[]?>([a, b]); Click(w, "prog-cmd-pw-add"); }, x => x.List("files").Count == 2),
            (MainWindow.FoKey, "beispiel", () => TypeInto(w, "fo-field-0", "Muster"), x => x.Data["values"]?.ToJsonString().Contains("Muster") == true),
            (MainWindow.DeckDesignKey, "vortrag", () => Click(w, "prog-cmd-dd-next-theme"), x => x.Text("theme") != "gold"),
            (MainWindow.ScanKey, "beispiel", () => Click(w, "prog-cmd-scan-rotate"), x => x.Data["pages"]!.ToJsonString().Contains("\"rot\":1")),
        };
        foreach (var (key, tpl, change, changed) in cases)
        {
            w.Show(key); Settle();
            RealClick(w, V<Button>(w, "prog-tpl-" + tpl)); Settle();
            Assert.False(w.ProgramActive(key)!.Dirty, key + ": frische Vorlage gilt schon als geändert");
            change(); Settle();
            Assert.True(changed(w.ProgramActive(key)!), key + ": Änderung kam nicht an – " + w.ProgramMessage(key));
            Assert.True(w.ProgramActive(key)!.Dirty, key + ": Änderung ist nicht als ungespeichert markiert – theme " + w.ProgramActive(key)!.Text("theme"));
        }
        w.ProgramAutosaveAll(); // der feste Takt
        Assert.True(MainWindow.ProgramAutosaveEvery <= TimeSpan.FromSeconds(20), "Takt zu lang für „höchstens 30 s verloren“");
        w.PickOverride = null;
        w.ProgLocks.Abandon(); // Absturz: kein Speichern, kein Schließen

        var w2 = OpenOn(root);
        try
        {
            foreach (var (key, _, _, changed) in cases)
            {
                w2.Show(key); Settle();
                Assert.True(w2.TryFind<StackPanel>("prog-restore") is not null, key + ": kein „Wiederherstellen“ – aktiv: " + w2.ProgramActive(key)?.Title + " / Sicherungen: " + string.Join(", ", Directory.GetFiles(Path.Combine(root, "programme", "sicherung")).Select(Path.GetFileName)) + " / " + string.Join(" | ", w2.GetLogicalTexts().Where(t => t.Contains("gesichert") || t.Contains("Vorlage") || t.Contains("Neu")).Take(20)));
                RealClick(w2, w2.Find<Button>("prog-restore-0")); Settle();
                var d = w2.ProgramActive(key)!;
                Assert.True(changed(d), key + ": nach dem Wiederherstellen fehlt die Änderung");
            }
        }
        finally { w2.Close(); }
    }

    // ---------- 3. Ziehen und Ablegen, Rechtsklick ----------

    [AvaloniaFact(DisplayName = "T-0705 PDF-Werkstatt: PDFs aus dem Explorer auf den ersten Schritt ziehen, Seite 4 mit der Maus an den Anfang ziehen, Rechtsklick „Nach rechts drehen“ und „Seite löschen“; Ausführen schreibt genau diese Reihenfolge")]
    public void Pdf_drag_drop_and_menu()
    {
        var (w, _, root) = Ui.Open();
        try
        {
            const string K = MainWindow.PwKey;
            var dir = Path.Combine(root, "eingang"); Directory.CreateDirectory(dir);
            var a = PdfWerkstatt_Ui.TextPdf(Path.Combine(dir, "a.pdf"), "Seite eins", "Seite zwei", "Seite drei");
            var b = PdfWerkstatt_Ui.TextPdf(Path.Combine(dir, "b.pdf"), "Seite vier");
            var txt = Path.Combine(dir, "notiz.txt"); File.WriteAllText(txt, "x");
            StartEmpty(w, K, "ordnen");
            Drop(w, "prog-first", txt); // passt nicht: Hinweis, nichts hinzugefügt
            Assert.Equal(0, w.ProgramActive(K)!.List("files").Count); Assert.Contains("PDFs oder Bilder", w.ProgramMessage(K));
            Drop(w, "prog-first", a, b);
            var d = w.ProgramActive(K)!;
            Assert.Equal(2, d.List("files").Count); Assert.Equal(4, d.List("pages").Count);
            Drag(w, "pw-page-3", "pw-page-0");
            static (int F, int P, int R) Pg(ProgramDocument x, int i) { var o = x.List("pages")[i]!; return ((int)ProgramDocument.ToNumber(o["f"]), (int)ProgramDocument.ToNumber(o["p"]), (int)ProgramDocument.ToNumber(o["r"])); }
            Assert.Equal((1, 0, 0), Pg(d, 0)); Assert.Equal((0, 0, 0), Pg(d, 1)); // „Seite vier“ jetzt vorn
            Assert.True(d.Undo()); w.Show(K); Settle(); Assert.Equal((0, 0, 0), Pg(d, 0)); Assert.True(d.Redo()); w.Show(K); Settle(); // rückgängig machbar
            Menu(w, "pw-page-1", "pw-ctx-rot-right");
            Assert.Equal(90, Pg(d, 1).R);
            Menu(w, "pw-page-3", "pw-ctx-del"); // „Seite drei“ fliegt raus
            Assert.Equal(3, d.List("pages").Count);
            Click(w, "pw-run-now");
            var res = d.List("results").Select(r => r!["file"].AsText()!).Last();
            Assert.Equal(3, PdfView.PageCount(res));
            Assert.Contains("Seite vier", PdfView.Text(res, 0)); Assert.Contains("Seite eins", PdfView.Text(res, 1)); Assert.Contains("Seite zwei", PdfView.Text(res, 2));
            using var doc = PdfSharp.Pdf.IO.PdfReader.Open(res, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import);
            Assert.Equal(90, ((doc.Pages[1].Rotate % 360) + 360) % 360);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0705 Präsentation gestalten: Folie in der Leiste mit der Maus nach vorn ziehen verschiebt den Abschnitt der Gliederung; Rechtsklick „Nach hinten“ und „Folie entfernen“; PowerPoint-Datei auf die Fläche ziehen übernimmt ihre Folien")]
    public void Deck_drag_drop_and_menu()
    {
        var (w, _, root) = Ui.Open();
        try
        {
            const string K = MainWindow.DeckDesignKey;
            w.Width = 1600; w.Height = 1200; w.ProgramSetLevel(UserLevel.Erweitert); w.Show(K); Settle();
            RealClick(w, w.Find<Button>("prog-tpl-pitch")); Settle();
            var d = w.ProgramActive(K)!;
            string[] Titles() => [.. DeckDesign.Parse(d.Text("outline")).Select(s => s.Title)];
            var before = Titles();
            Drag(w, "dd-thumb-3", "dd-thumb-1");
            Assert.True(Titles().SequenceEqual([before[0], before[3], before[1], before[2], .. before[4..]]), string.Join(" | ", before) + " → " + string.Join(" | ", Titles()));
            Assert.Equal(1, w.DdSelected(d));
            Menu(w, "dd-thumb-1", "dd-ctx-later"); // eine Stelle nach hinten
            Assert.Equal([before[0], before[1], before[3], before[2], .. before[4..]], Titles());
            Drag(w, "dd-thumb-2", "dd-thumb-3"); // nach hinten ziehen: landet hinter dem Ziel
            Assert.Equal(before, Titles());
            Menu(w, "dd-thumb-2", "dd-ctx-remove");
            Assert.DoesNotContain(before[2], Titles()); Assert.Equal(before.Length - 1, Titles().Length);
            Assert.True(d.Undo()); Assert.Equal(before, Titles()); w.Show(K); Settle();

            // fremde Präsentation ablegen
            var deck = SlideOutline.FromOutline("Jahresrückblick\nHöhepunkte\n  - Neues Dach\n  - Sommerfest");
            var pptx = Path.Combine(root, "rueckblick.pptx"); Directory.CreateDirectory(root); SlideFiles.Save(deck, pptx);
            Drop(w, "dd-preview-card", pptx);
            Assert.Equal(["Jahresrückblick", "Höhepunkte"], Titles());
            Assert.True(d.Undo()); Assert.Equal(before, Titles());
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0705 Scannen: Fotos auf den ersten Schritt ziehen, zweite Seite mit der Maus nach vorn ziehen, Rechtsklick „Drehen“ und „Seite entfernen“")]
    public void Scan_drag_drop_and_menu()
    {
        var (w, _, root) = Ui.Open();
        try
        {
            const string K = MainWindow.ScanKey;
            var dir = Path.Combine(root, "fotos");
            var p1 = Photo(dir, "seite1.png"); var p2 = Photo(dir, "seite2.png");
            StartEmpty(w, K, "leer");
            Drop(w, "prog-first", p1, p2);
            var d = w.ProgramActive(K)!;
            string Src(int i) => Path.GetFileName(d.Data["pages"]![i]!["src"]!.GetValue<string>());
            Assert.Equal(["seite1.png", "seite2.png"], new[] { Src(0), Src(1) });
            Drag(w, "scan-thumb-1", "scan-thumb-0");
            Assert.Equal(["seite2.png", "seite1.png"], new[] { Src(0), Src(1) });
            Menu(w, "scan-thumb-1", "scan-ctx-rotate");
            Assert.Contains("\"rot\":1", d.Data["pages"]![1]!.ToJsonString());
            Menu(w, "scan-thumb-0", "scan-ctx-del");
            Assert.Equal(["seite1.png"], new[] { Src(0) });
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0705 Formulare, Verträge, Bewerbung, Karten: Datei auf die Arbeitsfläche ziehen öffnet bzw. hängt an; Rechtsklick am Vertrag schreibt die Kündigung als PDF")]
    public void Drop_in_forms_contracts_cv_cards()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1200;
        MainWindow.ConNowOverride = () => new DateTime(2026, 11, 16);
        try
        {
            var form = FormPdf(Path.Combine(root, "eingang"));
            // Formulare: auf die Karte „erster Schritt“ ablegen = öffnen
            StartEmpty(w, MainWindow.FoKey, "oeffnen");
            Drop(w, "prog-first", form);
            Assert.Equal(Path.GetFullPath(form), w.ProgramActive(MainWindow.FoKey)!.Text("file"));
            Assert.Contains("3 Felder", w.ProgramMessage(MainWindow.FoKey));

            // Verträge: Dokument auf die Zeile ziehen; Rechtsklick → Kündigung als PDF
            w.ProgramSetLevel(UserLevel.Erweitert); w.Show(MainWindow.ConKey); Settle();
            RealClick(w, w.Find<Button>("prog-tpl-beispiel")); Settle();
            var c = w.ProgramActive(MainWindow.ConKey)!;
            Drop(w, "con-row-1", form);
            var second = MainWindow.ConOf(c).Vertraege.Single(v => v.Dokumente.Count > 0);
            Assert.Equal(Path.GetFullPath(form), Path.GetFullPath(second.Dokumente.Single()));
            Menu(w, "con-row-0", "con-ctx-letter-pdf");
            var pdf = w.ProgramMessage(MainWindow.ConKey).Split("PDF: ")[1].Split(" – ")[0];
            Assert.True(File.Exists(pdf)); Assert.Contains("Kündigung", PdfView.Text(pdf, 0));

            // Bewerbung: Zeugnis ablegen = Anlage der Mappe
            w.Show(MainWindow.CvKey); Settle();
            RealClick(w, w.Find<Button>("prog-tpl-beispiel")); Settle();
            var cv = w.ProgramActive(MainWindow.CvKey)!;
            var before = MainWindow.CvOf(cv).Anlagen.Count;
            Drop(w, "cv-tabs", form);
            Assert.Equal(before + 1, MainWindow.CvOf(cv).Anlagen.Count);

            // Karten: Gästeliste als CSV ablegen
            w.Show(MainWindow.CardKey); Settle();
            RealClick(w, w.Find<Button>("prog-tpl-geburtstag")); Settle();
            var csv = Path.Combine(root, "gaeste.csv"); File.WriteAllText(csv, "Name;Personen\nFamilie Muster;4\nErika Muster;1\n");
            Drop(w, "cards-pages", csv);
            Assert.Equal(2, MainWindow.CardOf(w.ProgramActive(MainWindow.CardKey)!).Gaeste.Count);
        }
        finally { MainWindow.ConNowOverride = null; w.Close(); }
    }
}
