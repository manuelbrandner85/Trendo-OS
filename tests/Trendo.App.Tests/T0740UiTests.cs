using System.Text;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using AvaloniaEdit;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Kreativ;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0740: „Eigene KI trainieren“ wendet das Modell im Stapel an (CSV/Ordner, Fortschritt, Abbrechen, Export, Ziehen und Ablegen, Menü),
/// „KI-Anweisungen“ lassen sich als Datei speichern und einlesen (Rückfrage bei gleichem Namen) und haben einen echten Texteditor.
/// Alles vom Klick bis zur echten Datei; nur der Dateidialog ist ersetzt (Pfad statt Fenster), Training und Einordnen laufen echt.</summary>
public class T0740_Ui
{
    const string K = "ki-trainieren-programm";
    static string RepoRoot() { for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent) if (File.Exists(Path.Combine(d.FullName, "Trendo.sln"))) return d.FullName; throw new DirectoryNotFoundException(); }
    static string Daten => Path.Combine(RepoRoot(), "reports", "T-0740", "daten");
    static void WaitFor(Func<bool> done, int ms = 20000) { var until = DateTime.Now.AddMilliseconds(ms); while (!done() && DateTime.Now < until) { Thread.Sleep(15); Settle(); } Assert.True(done(), "Zeit abgelaufen"); }
    static T Get<T>(MainWindow w, string name) where T : Control { Settle(); return w.GetLogicalDescendants().OfType<T>().Last(c => c.Name == name); }
    static bool Has(MainWindow w, string name) { Settle(); return w.GetLogicalDescendants().OfType<Control>().Any(c => c.Name == name); }
    static void Click(MainWindow w, string name) { var b = Get<Button>(w, name); b.BringIntoView(); Settle(); RealClick(w, Get<Button>(w, name)); Settle(); }
    /// <summary>Beleg-Lauf: mit TRENDO_T0740_BELEG=&lt;Ordner&gt; werden die erzeugten Dateien dorthin kopiert.</summary>
    /// <summary>Bild zum Nachprüfen der Oberfläche: mit TRENDO_T0740_SHOT=&lt;Ordner&gt;.</summary>
    static void Shot(MainWindow w, string name) { if (Environment.GetEnvironmentVariable("TRENDO_T0740_SHOT") is { Length: > 0 } dir) { Directory.CreateDirectory(dir); w.Width = 1600; w.Height = 1000; Settle(); w.CaptureRenderedFrame()?.Save(Path.Combine(dir, name)); } }
    static void Keep(string file, string name) { if (Environment.GetEnvironmentVariable("TRENDO_T0740_BELEG") is { Length: > 0 } dir) { Directory.CreateDirectory(dir); File.Copy(file, Path.Combine(dir, name), true); } }

    [AvaloniaFact(DisplayName = "T-0740 Eigene KI: Beispiel trainieren, CSV aus dem Repo einordnen (Knopf), Vorschau, Speichern als CSV mit BOM, Rückgängig, Ordner einordnen, Ablegen, Menü und Rechtsklick")]
    public void Train_apply()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        try
        {
            w.Show(K); Settle(); RealClick(w, w.Find<Button>("prog-tpl-beispiel")); Settle();
            var d = w.ProgramActive(K)!;
            Assert.False(Has(w, "train-batch")); // ohne Modell nichts anzuwenden
            RealClick(w, w.Find<Button>("prog-cmd-train-run")); Settle();
            Assert.StartsWith("✓ Trainiert", w.ProgramMessage(K));
            Assert.True(Has(w, "train-apply-csv-big"));

            // CSV mit 30 ungesehenen Texten (reports/T-0740/daten/neue-texte.csv) – über den Knopf im Menüband
            var input = Path.Combine(Daten, "neue-texte.csv");
            w.PickOverride = _ => Task.FromResult<string[]?>([input]);
            Click(w, "prog-cmd-train-apply-csv");
            WaitFor(() => !w.TrainBatchBusy && w.TrainBatchCsv(d).Length > 0);
            Assert.StartsWith("✓ 30 Zeilen eingeordnet", w.ProgramMessage(K));
            var rows = Trendo.Core.Sys.Csv.Parse(w.TrainBatchCsv(d));
            Assert.Equal(["nr", "text", "erwartet", "Vorhersage", "Sicherheit", "Bekannte Wörter"], rows[0]);
            Assert.Equal(31, rows.Count(r => r.Count > 1));
            var right = rows.Skip(1).Count(r => r.Count > 3 && r[2] == r[3]);
            Assert.True(right >= 24, $"nur {right} von 30 richtig");
            w.Show(K); Settle();
            var table = Get<Grid>(w, "train-batch-table");
            Assert.Equal(13 * 3, table.Children.Count); // Kopf + 12 Zeilen, je 3 Spalten
            Assert.NotNull(Get<StackPanel>(w, "train-batch").ContextMenu);
            if (Environment.GetEnvironmentVariable("TRENDO_T0740_SHOT") is { Length: > 0 } sd) { Directory.CreateDirectory(sd); File.WriteAllText(Path.Combine(sd, "train-dokument.json"), d.Data.ToJsonString()); } // für tools/Trendo.Render (TRENDO_PROGDATA)

            // Speichern über den Knopf: echte Datei, UTF-8 mit BOM, Semikolon
            var outFile = Path.Combine(root, "vorhersagen.csv");
            w.PickOverride = _ => Task.FromResult<string[]?>([outFile]);
            Click(w, "train-batch-save");
            WaitFor(() => File.Exists(outFile));
            var bytes = File.ReadAllBytes(outFile);
            Assert.Equal(Encoding.UTF8.GetPreamble(), bytes[..3]);
            Assert.Equal(w.TrainBatchCsv(d), Encoding.UTF8.GetString(bytes[3..]));
            Keep(input, "eingabe-neue-texte.csv"); Keep(outFile, "ausgabe-neue-texte-vorhersagen.csv");
            // derselbe Inhalt über Datei ▸ Exportieren
            var viaExport = MainWindow.Programs[K].Exports.Single(e => e.Name == "pred").Write(d, Path.Combine(root, "export.csv"));
            Assert.Equal(bytes, File.ReadAllBytes(viaExport));

            // Rückgängig nimmt die Vorhersagen zurück, Wiederholen bringt sie wieder
            Assert.True(d.Undo()); Assert.Equal("", w.TrainBatchCsv(d));
            Assert.True(d.Redo()); Assert.NotEqual("", w.TrainBatchCsv(d));

            // Ordner einordnen (Menüband) – je Datei eine Zeile
            var folder = Path.Combine(Daten, "ordner");
            var keepPick = MainWindow.FolderPickOverride;
            MainWindow.FolderPickOverride = _ => folder;
            try { w.Show(K); Settle(); Click(w, "prog-cmd-train-apply-folder"); WaitFor(() => !w.TrainBatchBusy && d.Text("batchKind") == "ordner"); }
            finally { MainWindow.FolderPickOverride = keepPick; }
            var f = Trendo.Core.Sys.Csv.Parse(w.TrainBatchCsv(d)).Where(r => r.Count > 1).ToDictionary(r => r[0], r => r[1]);
            Assert.Equal("Rechnung", f["rechnung-strom.txt"]); Assert.Equal("Rechnung", f["mahnung.txt"]);
            Assert.Equal("Termin", f["einladung-besprechung.txt"]); Assert.Equal("Termin", f["arzttermin.md"]);
            Assert.Equal("Werbung", f["newsletter.txt"]); Assert.Equal("Werbung", f["gewinnspiel.txt"]);
            var folderOut = Path.Combine(root, "ordner-vorhersagen.csv");
            MainWindow.Programs[K].Exports.Single(e => e.Name == "pred").Write(d, folderOut);
            Keep(folderOut, "ausgabe-ordner-vorhersagen.csv");

            // Ziehen und Ablegen: CSV-Datei auf die Arbeitsfläche (gleicher Weg wie der Drop-Handler); fremde Dateiart wird abgelehnt
            Assert.Null(w.TrainDropped(d, Path.Combine(root, "bild.png")).Result);
            Assert.Contains("CSV-Datei oder einen Ordner", w.ProgramMessage(K));
            _ = w.TrainDropped(d, input); WaitFor(() => !w.TrainBatchBusy && d.Text("batchKind") == "csv");

            // Menüleiste „Modell“ mit Kürzeln
            w.Show(K); Settle();
            var names = w.ActiveMenuBar()!.GetLogicalDescendants().OfType<MenuItem>().Select(m => m.Name).ToList();
            Assert.Contains("menu-train-key-csv", names); Assert.Contains("menu-train-key-folder", names);
            Assert.Empty(Trendo.Core.Programs.ProgramMenu.Conflicts([.. MainWindow.Programs[K].Menus!(d)]));
        }
        finally { w.PickOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0740 Eigene KI: „Abbrechen“ hält einen großen Stapel mitten im Lauf an, Fortschritt sichtbar, nichts wird übernommen")]
    public void Train_cancel()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        try
        {
            w.ProgramNew(K, "beispiel"); w.Show(K); Settle();
            var d = w.ProgramActive(K)!;
            RealClick(w, w.Find<Button>("prog-cmd-train-run")); Settle();
            var big = Path.Combine(root, "gross.csv");
            var sb = new StringBuilder("text\n"); for (var i = 0; i < 3000; i++) sb.Append("Bitte zahlen Sie die Rechnung Nummer ").Append(i).Append('\n');
            File.WriteAllText(big, sb.ToString());
            MainWindow.TrainBatchTestDelayMs = 30;
            w.PickOverride = _ => Task.FromResult<string[]?>([big]);
            Click(w, "train-apply-csv-big");
            WaitFor(() => w.TrainBatchBusy && Has(w, "train-batch-cancel"));
            WaitFor(() => Get<ProgressBar>(w, "train-batch-progress").Value > 0);
            Assert.Matches(@"^\d[\d.]* von 3\.000$", Get<TextBlock>(w, "train-batch-progress-text").Text);
            Shot(w, "train-fortschritt.png");
            Click(w, "train-batch-cancel");
            WaitFor(() => !w.TrainBatchBusy);
            Assert.StartsWith("Abgebrochen nach ", w.ProgramMessage(K));
            Assert.Equal("", w.TrainBatchCsv(d));
            Assert.False(Has(w, "train-batch-progress"));
        }
        finally { MainWindow.TrainBatchTestDelayMs = 0; w.PickOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0740 Eigene KI ohne Modell: Einordnen sagt, was fehlt, statt etwas zu raten")]
    public void Train_no_model()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        try
        {
            w.ProgramNew(K, "leer"); w.Show(K); Settle();
            Assert.Null(w.TrainApply(w.ProgramActive(K)!, Path.Combine(Daten, "neue-texte.csv"), false).Result);
            Assert.StartsWith("Erst trainieren", w.ProgramMessage(K));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0740 KI-Anweisungen: Vorlage als .md speichern, ändern, einlesen → Rückfrage → Ersetzen (neue Version) und Als Kopie; Alle als .json; Ablegen")]
    public void Prompt_files()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        try
        {
            w.Show("ki-vorlagen"); Settle();
            Click(w, "prompt-brief-formulieren");
            var md = Path.Combine(root, "brief.md");
            w.PickOverride = _ => Task.FromResult<string[]?>([md]);
            Click(w, "prompt-export"); WaitFor(() => File.Exists(md));
            var text = File.ReadAllText(md);
            Assert.StartsWith("---\nformat: trendo-ki-anweisung\nname: Brief formulieren\nbereich: Briefe\nversion: 1\n---\nSchreibe einen höflichen", text);
            Keep(md, "vorlage-export.md");

            // in einem anderen Programm geändert (hier: Text ersetzt) und wieder eingelesen
            File.WriteAllText(md, text.Replace("3 kurze Absätze", "2 kurze Absätze"));
            Keep(md, "vorlage-geaendert.md");
            Click(w, "prompt-import");
            WaitFor(() => Has(w, "prompt-import-ask"));
            Assert.Contains("Brief formulieren", Get<TextBlock>(w, "prompt-msg").Text);
            Shot(w, "vorlagen-rueckfrage.png");
            Click(w, "prompt-import-replace");
            var t = w.Prompts.FindByName("Brief formulieren")!;
            Assert.Equal(2, t.Version); Assert.Contains("2 kurze Absätze", t.Text); Assert.Contains("3 kurze Absätze", t.Versions[0].Text);
            Assert.False(Has(w, "prompt-import-ask"));

            Click(w, "prompt-import"); WaitFor(() => Has(w, "prompt-import-ask"));
            Click(w, "prompt-import-copy");
            Assert.NotNull(w.Prompts.FindByName("Brief formulieren (Kopie)"));
            Click(w, "prompt-import"); WaitFor(() => Has(w, "prompt-import-ask"));
            var before = w.Prompts.All().Count;
            Click(w, "prompt-import-skip");
            Assert.Equal(before, w.Prompts.All().Count);

            // alle als .json, in einen frischen Datenordner eingelesen
            var json = Path.Combine(root, "alle.json");
            w.PickOverride = _ => Task.FromResult<string[]?>([json]);
            Click(w, "prompt-export-all"); WaitFor(() => File.Exists(json));
            Keep(json, "alle-vorlagen.json");
            var other = new PromptLibrary(Path.Combine(root, "frisch"), new AuditLog(Path.Combine(root, "frisch-audit.jsonl")));
            var entries = PromptLibrary.ParseFile(json, File.ReadAllText(json));
            Assert.Equal(w.Prompts.All().Count, entries.Count);
            foreach (var e in entries) other.Import(e, PromptLibrary.ImportMode.Replace);
            Assert.Contains("2 kurze Absätze", other.FindByName("Brief formulieren")!.Text);

            // fremde Markdown-Datei mit anderem Kopf (title/category/tags, wie in Notiz-Programmen) über den Knopf
            var foreign = Path.Combine(Daten, "fremde-vorlage.md");
            w.PickOverride = _ => Task.FromResult<string[]?>([foreign]);
            Click(w, "prompt-import");
            var p = w.Prompts.FindByName("Protokoll zusammenfassen")!;
            Assert.Equal("Büro", p.Category);
            Assert.Equal(["Anzahl", "Text"], PromptLibrary.Variables(p.Text).Select(v => v.Name));
            Assert.EndsWith("{{Text}}", p.Text);

            // Ablegen einer .txt-Datei auf der Seite (gleicher Weg wie der Drop-Handler)
            var txt = Path.Combine(root, "Witz erzählen.txt"); File.WriteAllText(txt, "Erzähle einen kurzen Witz über {{Thema}} in 2 Sätzen.");
            w.PromptImportFile(txt);
            Assert.Equal("Erzähle einen kurzen Witz über {{Thema}} in 2 Sätzen.", w.Prompts.FindByName("Witz erzählen")!.Text);
            Assert.StartsWith("✓ 1 Vorlage übernommen", Get<TextBlock>(w, "prompt-msg").Text);

            // Sammelstand 0.9.105: .txt aus dem deutschen Windows-Editor (Windows-1252) – Umlaute kommen richtig an
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            var ansi = Path.Combine(root, "Grüße schreiben.txt");
            File.WriteAllBytes(ansi, System.Text.Encoding.GetEncoding(1252).GetBytes("Schreibe Grüße an {{Name}} – mit Größe und Stil."));
            w.PromptImportFile(ansi);
            Assert.Equal("Schreibe Grüße an {{Name}} – mit Größe und Stil.", w.Prompts.FindByName("Grüße schreiben")!.Text);
        }
        finally { w.PickOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0740 KI-Anweisungen: Texteditor – Tippen, Rückgängig im Feld (Knopf und Rechtsklick), „Lücke einfügen“ als ein Schritt, Lücken hervorgehoben, Speichern")]
    public void Prompt_editor()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.Show("ki-vorlagen"); Settle();
            Click(w, "prompt-new");
            var ed = Get<TextEditor>(w, "pedit-text");
            Assert.Contains(ed.TextArea.TextView.LineTransformers, x => x.GetType().Name == "PlaceholderColorizer");
            ed.TextArea.Focus(); Settle();
            foreach (var ch in "Erkläre Strom") { w.KeyTextInput(ch.ToString()); Settle(); }
            Assert.Equal("Erkläre Strom", ed.Text);
            ed.Select(8, 5); Settle();
            Click(w, "pedit-mark-placeholder");
            Assert.Equal("Erkläre {{Strom}}", ed.Text);
            Assert.Equal([(8, 9)], MainWindow.PromptPlaceholderSpans(ed.Text));
            Shot(w, "vorlagen-editor.png");
            Click(w, "pedit-text-undo");
            Assert.Equal("Erkläre Strom", ed.Text); // die Lücke war genau ein Schritt
            Click(w, "pedit-text-redo");
            Assert.Equal("Erkläre {{Strom}}", ed.Text);
            var undo = ed.ContextMenu!.Items.OfType<MenuItem>().Single(m => m.Name == "pedit-ctx-undo");
            undo.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent)); Settle();
            Assert.Equal("Erkläre Strom", ed.Text);
            ed.Document.Text = "Erkläre {{Thema}} in 3 Sätzen."; Settle();
            Get<TextBox>(w, "pedit-name").Text = "Erklären kurz";
            Click(w, "pedit-save");
            Assert.Equal("Erkläre {{Thema}} in 3 Sätzen.", w.Prompts.FindByName("Erklären kurz")!.Text);
            Assert.True(Has(w, "pvar-Thema"));
        }
        finally { w.Close(); }
    }
}
