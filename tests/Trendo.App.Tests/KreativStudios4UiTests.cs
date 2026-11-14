using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using AvaloniaEdit;
using SkiaSharp;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Sys;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>Etappe „Kreativ &amp; KI“ Teil 4 (T-0615): Code mit KI, Bilder erkennen, Recherche mit Quellen, Tagesplaner.</summary>
public class KreativStudios4_Ui
{
    static string Export(MainWindow w, string key, string name, string root)
    {
        var e = MainWindow.Programs[key].Exports.Single(x => x.Name == name);
        return e.Write(w.ProgramActive(key)!, Path.Combine(root, "export-" + key + "-" + name + e.Extension));
    }

    static void Tap(MainWindow w, string name) { var b = w.Find<Button>(name); b.BringIntoView(); Settle(); b.Press(); Settle(); }
    static void WaitFor(Func<bool> done) { for (var i = 0; i < 600 && !done(); i++) { Thread.Sleep(15); Settle(); } }
    static List<string> Texts(Control c) => c.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? "").Where(t => t.Length > 0).ToList();

    [AvaloniaFact(DisplayName = "Kreativ & KI Teil 4: alle vier Studios haben eine eigene Seite und öffnen eine Vorlage")]
    public void Pages()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            foreach (var (studio, key, tpl) in new[] { ("STUDIO-CODEAI-01", "code-ki-programm", "leer"), ("STUDIO-VISION-01", "bilder-erkennen-programm", "leer"), ("STUDIO-CITE-01", "quellen-programm", "beispiel"), ("STUDIO-DAYPLAN-01", "tagesplaner-programm", "beispiel") })
            {
                Assert.Equal(key, MainWindow.StudioPages[studio]);
                w.Show(key); Settle();
                RealClick(w, w.Find<Button>("prog-tpl-" + tpl)); Settle();
                Assert.NotNull(w.ProgramActive(key));
            }
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Code mit KI: Ordner öffnen, Datei bearbeiten und speichern, Schnappschuss und Zeilen-Diff, Tests erst nach zweitem Klick, Exporte")]
    public void Code()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        var proj = Path.Combine(root, "projekt"); Directory.CreateDirectory(proj);
        File.WriteAllText(Path.Combine(proj, "rechner.py"), "def addiere(a, b):\n    return a + b\n");
        File.WriteAllText(Path.Combine(proj, "test_rechner.py"), "import unittest\n");
        var calls = new List<(string Exe, IReadOnlyList<string> Args, string Cwd, int Ms)>();
        MainWindow.FolderPickOverride = _ => proj;
        MainWindow.CodeAiRunOverride = (exe, args, cwd, ms) => { calls.Add((exe, args, cwd, ms)); return new Cli.Result(0, "Ran 1 test\nOK", ""); };
        try
        {
            const string K = "code-ki-programm";
            w.Show(K); Settle(); RealClick(w, w.Find<Button>("prog-tpl-leer")); Settle();
            Tap(w, "ca-open-big"); WaitFor(() => w.ProgramActive(K)!.Text("root") == proj);
            var d = w.ProgramActive(K)!;
            Assert.Contains("Python", w.ProgramMessage(K));
            var node = w.Find<StackPanel>("ca-tree").Children.OfType<Button>().First(b => (b.Content as TextBlock)?.Text?.Trim() == "rechner.py");
            node.Press(); Settle();
            var ed = w.Find<TextEditor>("ca-editor");
            Assert.Contains("addiere", ed.Document.Text);
            Tap(w, "ca-snap-now");
            ed = w.Find<TextEditor>("ca-editor");
            ed.Document.Text = "def addiere(a, b):\n    \"\"\"Summe.\"\"\"\n    return a + b\n";
            Tap(w, "ca-save-file");
            Assert.Contains("Summe", File.ReadAllText(Path.Combine(proj, "rechner.py")));
            // Datei wird von außen geändert (z. B. von Claude): Speichern überschreibt erst nach Rückfrage
            var py = File.ReadAllText(Path.Combine(proj, "rechner.py"));
            File.WriteAllText(Path.Combine(proj, "rechner.py"), py + "# von außen\n");
            w.Find<TextEditor>("ca-editor").Document.Insert(0, "# lokal\n");
            Tap(w, "ca-save-file");
            Assert.StartsWith("⚠", w.ProgramMessage(K));
            Assert.Contains("# von außen", File.ReadAllText(Path.Combine(proj, "rechner.py")));
            Tap(w, "ca-save-file");
            Assert.StartsWith("# lokal", File.ReadAllText(Path.Combine(proj, "rechner.py")));
            File.WriteAllText(Path.Combine(proj, "rechner.py"), py); // für den Vergleich unten
            w.Find<TextEditor>("ca-editor").Document.Text = py; Tap(w, "ca-save-file"); Tap(w, "ca-save-file");
            File.WriteAllText(Path.Combine(proj, "neu.py"), "x = 1\n");
            Tap(w, "ca-changes-now");
            Assert.Equal(2, w.Find<WrapPanel>("ca-changes").Children.Count);
            w.Find<WrapPanel>("ca-changes").Children.OfType<Button>().First(b => b.Content?.ToString()?.StartsWith("rechner.py") == true).Press(); Settle();
            Assert.Contains(Texts(w.Find<StackPanel>("ca-diff")), t => t.Contains("+     \"\"\"Summe.\"\"\""));

            Tap(w, "ca-test-run");
            Assert.Empty(calls); // erst fragen
            Assert.Contains("Nochmal klicken", w.ProgramMessage(K));
            Assert.Equal("Ja, jetzt ausführen", w.Find<Button>("ca-test-run").Content);
            Tap(w, "ca-test-run");
            WaitFor(() => calls.Count == 1 && w.ProgramMessage(K).StartsWith('✓'));
            Assert.Equal(proj, calls[0].Cwd); Assert.Contains("unittest", calls[0].Args); Assert.Equal(300_000, calls[0].Ms);
            WaitFor(() => w.TryFind<SelectableTextBlock>("ca-output") is not null);
            Assert.StartsWith("✓ Tests bestanden", w.Find<SelectableTextBlock>("ca-output").Text);

            var patch = File.ReadAllText(Export(w, K, "patch", root));
            Assert.Contains("--- a/rechner.py", patch); Assert.Contains("+++ b/neu.py", patch); Assert.Contains("+x = 1", patch);
            Assert.Contains("- Python (erkannt an", File.ReadAllText(Export(w, K, "md", root)));

            Tap(w, "prog-cmd-ca-explain");
            Assert.Contains("Claude ist nicht verbunden", w.ProgramMessage(K)); // ohne eigenen Zugang klarer Hinweis
            Assert.Throws<InvalidOperationException>(() => w.CodeAiOpenFile(d, Path.Combine(root, "fremd.py")));
        }
        finally { MainWindow.FolderPickOverride = null; MainWindow.CodeAiRunOverride = null; w.Close(); }
    }

    static void Png(string file, Action<SKBitmap> paint, int w = 256, int h = 256)
    {
        using var b = new SKBitmap(w, h); paint(b);
        using var img = SKImage.FromBitmap(b); using var data = img.Encode(SKEncodedImageFormat.Png, 90);
        using var fs = File.Create(file); data.SaveTo(fs);
    }

    [AvaloniaFact(DisplayName = "Bilder erkennen: Bild öffnen, Fakten und Hauptfarben, Ordner prüfen (unscharf, zu dunkel, unlesbar), OCR ohne Tesseract mit Hinweis, Exporte")]
    public void Vision()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        var dir = Path.Combine(root, "bilder"); Directory.CreateDirectory(dir);
        var sharp = Path.Combine(dir, "scharf.png");
        Png(sharp, b => { for (var y = 0; y < 256; y++) for (var x = 0; x < 256; x++) b.SetPixel(x, y, ((x / 16) + (y / 16)) % 2 == 0 ? new SKColor(240, 240, 240) : new SKColor(20, 40, 160)); });
        Png(Path.Combine(dir, "weich.png"), b => { for (var y = 0; y < 256; y++) for (var x = 0; x < 256; x++) { var v = (byte)(40 + x * 180 / 255); b.SetPixel(x, y, new SKColor(v, v, v)); } });
        Png(Path.Combine(dir, "dunkel.png"), b => b.Erase(new SKColor(12, 14, 18)));
        File.WriteAllBytes(Path.Combine(dir, "kaputt.jpg"), [0xFF, 0xD8, 0xFF, 1, 2, 3]);
        w.PickOverride = _ => Task.FromResult<string[]?>([sharp]);
        MainWindow.FolderPickOverride = _ => dir;
        MainWindow.VisionTesseractOverride = () => null;
        try
        {
            const string K = "bilder-erkennen-programm";
            w.Show(K); Settle(); RealClick(w, w.Find<Button>("prog-tpl-leer")); Settle();
            RealClick(w, w.Find<Button>("prog-cmd-vi-open")); WaitFor(() => w.ProgramMessage(K).StartsWith("scharf.png"));
            Assert.Equal("scharf.png: in Ordnung, scharf.", w.ProgramMessage(K));
            var facts = Texts(w.Find<StackPanel>("vi-facts"));
            Assert.Contains("PNG, 256 × 256 Bildpunkte", facts);
            Assert.Contains("keine Aufnahmedaten", facts);
            Assert.Equal(2, w.Find<WrapPanel>("vi-colors").Children.Count);
            Assert.Contains(facts, t => t.StartsWith("✓") && t.Contains("scharf"));

            Tap(w, "vi-ocr-now");
            Assert.Contains("Tesseract", w.ProgramMessage(K));
            Assert.StartsWith("⚠", w.ProgramMessage(K));

            RealClick(w, w.Find<Button>("prog-cmd-vi-folder")); WaitFor(() => w.ProgramMessage(K).Contains("geprüft"));
            Assert.Equal("3 Bilder geprüft, 2 auffällig, 1 nicht lesbar.", w.ProgramMessage(K));
            var list = Texts(w.Find<StackPanel>("vi-folder-list"));
            Assert.Contains("⚠ dunkel.png – zu dunkel", list);
            Assert.Contains("⚠ weich.png – unscharf", list);
            Assert.Contains("✓ scharf.png", list);

            var md = File.ReadAllText(Export(w, K, "md", root));
            Assert.Contains("## scharf.png", md); Assert.Contains("zu dunkel", md);
            var csv = File.ReadAllLines(Export(w, K, "csv", root));
            Assert.Equal(4, csv.Length); Assert.StartsWith("Datei;Format", csv[0]);
            Assert.Equal([0xEF, 0xBB, 0xBF], File.ReadAllBytes(Path.Combine(root, $"export-{K}-csv.csv"))[..3]); // mit BOM, damit Tabellenprogramme Umlaute richtig lesen
            RealClick(w, w.Find<Button>("prog-cmd-vi-claude")); Settle();
            Assert.Contains("Claude ist nicht verbunden", w.ProgramMessage(K));
        }
        finally { w.PickOverride = null; MainWindow.FolderPickOverride = null; MainWindow.VisionTesseractOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Recherche mit Quellen: Beispiel, Stil wechseln, Quelle anlegen und ausfüllen, Doppelte, Zitat mit Seite, BibTeX hin und zurück, aus der Recherche übernehmen")]
    public void Cite()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        try
        {
            const string K = "quellen-programm";
            w.Show(K); Settle(); RealClick(w, w.Find<Button>("prog-tpl-beispiel")); Settle();
            var d = w.ProgramActive(K)!;
            Assert.Equal(3, w.Find<StackPanel>("cite-list").Children.OfType<Button>().Count());
            RealClick(w, w.Find<Button>("prog-cmd-cite-style-harvard")); Settle();
            Assert.Equal("harvard", d.Text("style"));
            Assert.Contains("(2024) Gärten für Einsteiger. 2. Aufl. Musterstadt: Grünverlag.", (string)w.Find<SelectableTextBlock>("cite-preview").Tag!);

            RealClick(w, w.Find<Button>("prog-cmd-cite-add-buch")); Settle();
            Assert.Equal(4, MainWindow.CiteSources(d).Count);
            Assert.StartsWith("⚠ Fehlt für diesen Stil: Titel", w.Find<TextBlock>("cite-missing").Text);
            void Fill(string field, string value)
            {
                var box = w.Find<TextBox>("cite-f-" + field); box.Text = value;
                box.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter }); Settle();
            }
            Fill("title", "Gärten für Einsteiger"); Fill("year", "2024"); Fill("authors", "Muster, Erika"); Fill("publisher", "Grünverlag"); Fill("place", "Musterstadt");
            Assert.Equal("✓ Vollständig für diesen Stil.", w.Find<TextBlock>("cite-missing").Text);
            Assert.Equal("Muster, E. (2024) Gärten für Einsteiger. Musterstadt: Grünverlag.", (string)w.Find<SelectableTextBlock>("cite-preview").Tag!);
            RealClick(w, w.Find<Button>("prog-cmd-cite-dups")); Settle();
            Assert.Equal("⚠ 1× doppelt – oben markiert.", w.ProgramMessage(K));
            Assert.Contains(Texts(w.Find<StackPanel>("cite-list")), t => t.StartsWith("⚠ Doppelt mit: Gärten für Einsteiger"));

            w.Find<TextBox>("cite-q-text").Text = "Wer gießt, gewinnt."; w.Find<TextBox>("cite-q-page").Text = "7";
            Tap(w, "cite-q-add");
            Assert.Equal(2, MainWindow.CiteQuotes(d).Count);
            Assert.Equal("✓ Zitat gespeichert.", w.ProgramMessage(K));

            var md = File.ReadAllText(Export(w, K, "md", root));
            Assert.Contains("# Literaturverzeichnis", md); Assert.Contains("Zitierstil: Harvard", md); Assert.Contains("„Wer gießt, gewinnt.“ (Muster 2024, S. 7)", md);
            var bib = Export(w, K, "bib", root);
            Assert.Contains("@online{", File.ReadAllText(bib));

            var d2 = w.ProgramNew(K, "leer"); w.Show(K); Settle(); // zweites Dokument
            Assert.Equal(4, w.CiteImportBib(d2, bib));
            Assert.Equal(MainWindow.CiteSources(d).Select(s => s.Title).Order(), MainWindow.CiteSources(d2).Select(s => s.Title).Order());
            Assert.Contains("doppelt", w.ProgramMessage(K));

            w.Desk.Record(new Uri("https://example.org/artikel"), new ReaderPage("Ein Artikel", "Text", []), "Text");
            Assert.Equal(1, w.CiteFromResearch(d2));
            Assert.Equal(0, w.CiteFromResearch(d2)); // nicht doppelt
            Assert.Equal("webseite", MainWindow.CiteSources(d2).Last().Kind);
            Assert.Equal(DateTime.Today.ToString("yyyy-MM-dd"), MainWindow.CiteSources(d2).Last().Accessed);
            Assert.Throws<InvalidOperationException>(() => w.CiteImportBib(d2, @"\\rechner\quellen.bib"));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Tagesplaner: Beispieltag planen, Pause und Morgen-Liste, abhaken, Kalenderdatei, Termine und Aufgaben aus dem Kalender, auf morgen schieben")]
    public void Day()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        MainWindow.DayNowOverride = () => new DateTime(2026, 11, 16, 7, 0, 0);
        try
        {
            const string K = "tagesplaner-programm";
            w.Show(K); Settle(); RealClick(w, w.Find<Button>("prog-tpl-beispiel")); Settle();
            var d = w.ProgramActive(K)!;
            Assert.Equal("2026-11-16", d.Text("day"));
            RealClick(w, w.Find<Button>("prog-cmd-dp-plan")); Settle();
            Assert.Equal("3 Aufgaben eingeplant, 1 passen nicht mehr – stehen unter „Morgen“.", w.ProgramMessage(K));
            var txt = File.ReadAllText(Export(w, K, "txt", root));
            Assert.Contains("08:00–09:30  Angebot für Erika Muster schreiben", txt);
            Assert.Contains("09:30–09:40  Pause", txt);
            Assert.Contains("09:40–09:55  Rückruf beim Kundendienst", txt);
            Assert.Contains("10:30–11:15  Rechnungen prüfen", txt);
            Assert.Contains("- Steuerunterlagen sortieren (120 min, niedrig) – passt heute nicht mehr", txt);
            Assert.Contains(Texts(w.Find<StackPanel>("dp-later")), t => t.StartsWith("⚠ Steuerunterlagen sortieren"));
            var ics = File.ReadAllText(Export(w, K, "ics", root));
            Assert.Equal(4, ics.Split("\r\n").Count(l => l == "BEGIN:VEVENT"));
            Assert.Equal(4, Ical.Net.Calendar.Load(ics)!.Events.Count);

            w.Find<CheckBox>("dp-block-done-0").IsChecked = true; Settle();
            Assert.True(MainWindowDay(d).Single(i => i.Title.StartsWith("Angebot")).Done);

            w.Find<TextBox>("dp-quick").Text = "Blumen gießen, 10 min"; Tap(w, "dp-add");
            Assert.Contains("„Blumen gießen“ (10 min)", w.ProgramMessage(K));

            RealClick(w, w.Find<Button>("prog-cmd-dp-carry")); Settle();
            Assert.Equal("2026-11-17", d.Text("day"));
            Assert.Equal(4, d.List("tasks").Count); // erledigte fallen weg
            Assert.Empty(d.List("plan")); Assert.Empty(d.List("appts"));

            w.Cal.Add(new CalEvent("u1", new DateTime(2026, 11, 17, 9, 0, 0), new DateTime(2026, 11, 17, 10, 0, 0), "Werkstatt"));
            w.Cal.Add(new CalEvent("u2", new DateTime(2026, 11, 17), new DateTime(2026, 11, 18), "Feiertag", AllDay: true));
            w.Cal.AddTodo(new TodoItem("x1", "Paket abholen", Priority: 1));
            RealClick(w, w.Find<Button>("prog-cmd-dp-cal")); Settle();
            Assert.Equal("1 Termine aus dem Kalender übernommen.", w.ProgramMessage(K));
            RealClick(w, w.Find<Button>("prog-cmd-dp-cal")); Settle();
            Assert.Single(d.List("appts")); // nicht doppelt
            RealClick(w, w.Find<Button>("prog-cmd-dp-todos")); Settle();
            Assert.Equal(5, d.List("tasks").Count);
            RealClick(w, w.Find<Button>("prog-cmd-dp-plan")); Settle();
            var plan = File.ReadAllText(Export(w, K, "txt", root));
            Assert.Contains("09:00–10:00  Termin: Werkstatt", plan);
            Assert.DoesNotContain("Feiertag", plan);
        }
        finally { MainWindow.DayNowOverride = null; w.Close(); }
    }

    static List<(string Title, bool Done)> MainWindowDayRaw(Trendo.Core.Programs.ProgramDocument d) =>
        d.List("tasks").OfType<System.Text.Json.Nodes.JsonObject>().Select(o => ((string?)o["title"] ?? "", (string?)o["done"] == "ja")).ToList();
    static List<(string Title, bool Done)> MainWindowDay(Trendo.Core.Programs.ProgramDocument d) => MainWindowDayRaw(d);
}
