using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Trendo.App;
using Trendo.Core.Programs;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0740 Gegenprüfung: große Ergebnisse liegen als Datei neben dem Dokument (kleiner Rückgängig-Stapel), Windows-1252-CSV, ehrliche Kürzung über 50 000,
/// Schalter „Erste Zeile ist Überschrift“, Hinweis „älteres Modell“, Export „Vorhersagen (.csv)“ in „Einfach“ über Datei ▸ Exportieren, eine Regel für Lücken.
/// Training und Einordnen laufen echt; ersetzt ist nur der Dateidialog (Pfad statt Fenster).</summary>
public class T0740Gegenpruefung_Ui
{
    const string K = "ki-trainieren-programm";
    static void WaitFor(Func<bool> done, int ms = 60000) { var until = DateTime.Now.AddMilliseconds(ms); while (!done() && DateTime.Now < until) { Thread.Sleep(15); Settle(); } Assert.True(done(), "Zeit abgelaufen"); }
    static bool Has(MainWindow w, string name) { Settle(); return w.GetLogicalDescendants().OfType<Control>().Any(c => c.Name == name); }
    static T Get<T>(MainWindow w, string name) where T : Control { Settle(); return w.GetLogicalDescendants().OfType<T>().Last(c => c.Name == name); }

    static ProgramDocument Trained(MainWindow w)
    {
        w.ProgramNew(K, "beispiel"); w.Show(K); Settle();
        RealClick(w, w.Find<Button>("prog-cmd-train-run")); Settle();
        Assert.StartsWith("✓ Trainiert", w.ProgramMessage(K));
        return w.ProgramActive(K)!;
    }

    /// <summary>Auf das Einordnen warten, ohne den UI-Faden zu blockieren (sonst verklemmt sich das await in TrainApply).</summary>
    static T Run<T>(MainWindow w, Task<T> t) { WaitFor(() => t.IsCompleted, 120000); return t.Result; }

    static string Export(ProgramDocument d, string file) => MainWindow.Programs[K].Exports.Single(e => e.Name == "pred").Write(d, file);

    [AvaloniaFact(DisplayName = "T-0740 Gegenprüfung: große CSV einordnen, danach kleine Änderung – Dokument und Rückgängig-Schnappschuss bleiben klein, Rückgängig nimmt den Verweis zurück, Export hat alle Zeilen")]
    public void Big_result_stays_out_of_document()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        try
        {
            var d = Trained(w);
            var big = Path.Combine(root, "gross.csv");
            var sb = new StringBuilder("text;notiz\n");
            for (var i = 0; i < 20000; i++) sb.Append("Bitte zahlen Sie die offene Rechnung Nummer ").Append(i).Append(" bis Ende des Monats auf unser Konto ein;Notiz ").Append(i).Append('\n');
            File.WriteAllText(big, sb.ToString());
            Assert.NotNull(Run(w, w.TrainApply(d, big, false)));
            WaitFor(() => !w.TrainBatchBusy);
            var size = d.Data.ToJsonString().Length;
            Assert.True(size < 64 * 1024, $"Dokument nach dem Einordnen {size:N0} Zeichen groß");
            d.Edit("Kleine Änderung", x => x["probe"] = "Rechnung");
            Assert.True(d.Remember().Data.Length < 64 * 1024, "Schnappschuss für Rückgängig ist groß");
            // die ganze Tabelle geht als Datei hinaus
            var out1 = Export(d, Path.Combine(root, "alle.csv"));
            Assert.Equal(20001, File.ReadAllText(out1, Encoding.UTF8).Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
            // Rückgängig: erst die kleine Änderung, dann die Vorhersagen (Verweis weg), Wiederholen bringt sie wieder
            Assert.True(d.Undo()); Assert.True(d.Undo());
            Assert.Equal("", d.Text("batch")); Assert.Equal("", d.Text("batchFile"));
            Assert.True(d.Redo());
            Assert.Equal(File.ReadAllBytes(out1), File.ReadAllBytes(Export(d, Path.Combine(root, "wieder.csv"))));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0740 Gegenprüfung: CSV aus deutschem Excel (Windows-1252) – Umlaute kommen richtig an, kein Ersatzzeichen")]
    public void Csv_windows1252()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        try
        {
            var d = Trained(w);
            var csv = Path.Combine(root, "excel.csv");
            File.WriteAllBytes(csv, Encoding.GetEncoding(1252).GetBytes("Text;Absender\r\nBitte überweisen Sie den Betrag für März;Müller GmbH\r\nEinladung zur Besprechung am Dienstag;Bürgerbüro\r\n"));
            Assert.NotNull(Run(w, w.TrainApply(d, csv, false)));
            var all = File.ReadAllText(Export(d, Path.Combine(root, "aus.csv")), Encoding.UTF8);
            Assert.Contains("Müller GmbH", all); Assert.Contains("Bürgerbüro", all); Assert.DoesNotContain('�', all);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0740 Gegenprüfung: über 50 000 Zeilen – Meldung und Vorschau sagen „nur die ersten 50.000 von …“")]
    public void Truncation_is_told()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        try
        {
            var d = Trained(w);
            var big = Path.Combine(root, "riesig.csv");
            var sb = new StringBuilder("text\n"); for (var i = 0; i < 50002; i++) sb.Append("Rechnung Betrag ").Append(i).Append('\n');
            File.WriteAllText(big, sb.ToString());
            Assert.NotNull(Run(w, w.TrainApply(d, big, false)));
            Assert.StartsWith("⚠ Nur die ersten 50.000 von 50.002 Zeilen eingeordnet", w.ProgramMessage(K));
            w.Show(K);
            Assert.True(Has(w, "train-batch-truncated"));
            Assert.Contains("50.002", Get<TextBlock>(w, "train-batch-truncated").Text);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0740 Gegenprüfung: CSV ohne Kopf, erstes Wort „Rechnung“ – bleibt Datenzeile; Schalter „Erste Zeile ist Überschrift“ ordnet neu ein")]
    public void Header_switch()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        try
        {
            var d = Trained(w);
            var csv = Path.Combine(root, "ohne-kopf.csv");
            File.WriteAllText(csv, "Rechnung\nEinladung zur Besprechung am Montag um zehn Uhr\nBitte den offenen Betrag bis Freitag überweisen\n");
            Assert.NotNull(Run(w, w.TrainApply(d, csv, false)));
            Assert.Equal("3", d.Text("batchRows"));
            w.Show(K);
            var hb = Get<CheckBox>(w, "train-batch-header");
            Assert.False(hb.IsChecked);
            hb.IsChecked = true;
            WaitFor(() => !w.TrainBatchBusy && d.Text("batchRows") == "2");
            Assert.Equal("ja", d.Text("batchHeader"));
            Assert.True(Get<CheckBox>(w, "train-batch-header").IsChecked);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0740 Gegenprüfung: nach erneutem Training sagen die alten Vorhersagen „mit älterem Modell erstellt“")]
    public void Old_model_hint()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        try
        {
            var d = Trained(w);
            var csv = Path.Combine(root, "kurz.csv");
            File.WriteAllText(csv, "text\nBitte den offenen Betrag überweisen\nTermin am Montag\n");
            Assert.NotNull(Run(w, w.TrainApply(d, csv, false)));
            w.Show(K);
            Assert.False(Has(w, "train-batch-oldmodel"));
            d.Edit("Beispiel", x => ((System.Text.Json.Nodes.JsonArray)x["examples"]!).Add(new System.Text.Json.Nodes.JsonObject { ["text"] = "Zahlungserinnerung offener Betrag", ["label"] = "Rechnung" }));
            RealClick(w, w.Find<Button>("prog-cmd-train-run")); Settle();
            w.Show(K);
            Assert.True(Has(w, "train-batch-oldmodel"));
            Assert.Contains("älteren Modell", Get<TextBlock>(w, "train-batch-oldmodel").Text);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0740 Gegenprüfung: in „Einfach“ speichert Datei ▸ Exportieren ▸ „Vorhersagen (.csv)“ die echte Datei (Ersatzweg für den Knopf)")]
    public void Simple_export_via_menu()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        try
        {
            w.ProgramSetLevel(UserLevel.Einfach);
            var d = Trained(w);
            var csv = Path.Combine(root, "kurz.csv");
            File.WriteAllText(csv, "text\nBitte den offenen Betrag überweisen\nTermin am Montag\n");
            Assert.NotNull(Run(w, w.TrainApply(d, csv, false)));
            w.Show(K); Settle();
            Assert.Equal(UserLevel.Einfach, w.ProgramLevel);
            Assert.False(Has(w, "prog-export-pred")); // kein eigener Knopf in „Einfach“ …
            var mi = w.ActiveMenuBar()!.GetLogicalDescendants().OfType<MenuItem>().Single(m => m.Name == "menu-prog-export-pred"); // … aber in der Menüleiste
            mi.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent)); Settle();
            Assert.StartsWith("Exportiert: ", w.ProgramMessage(K));
            var file = w.ProgramMessage(K)["Exportiert: ".Length..].Trim();
            Assert.EndsWith(".csv", file);
            var bytes = File.ReadAllBytes(file);
            Assert.Equal(Encoding.UTF8.GetPreamble(), bytes[..3]);
            Assert.Contains("Vorhersage", Encoding.UTF8.GetString(bytes));
        }
        finally { w.ProgramSetLevel(UserLevel.Erweitert); w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0740 Gegenprüfung: Hervorhebung und Ausfüllen nutzen dieselbe Lücken-Regel")]
    public void Placeholder_rule_is_shared()
    {
        const string text = "{{a.b}} {{Thema|x}} {{offen";
        Assert.Equal([(8, 11)], MainWindow.PromptPlaceholderSpans(text));
        Assert.Equal(["Thema"], Trendo.Core.PromptLibrary.Variables(text).Select(v => v.Name));
        Assert.Equal([(0, 2, false), (8, 11, true), (20, 2, false)], MainWindow.PromptHighlightSpans(text));
    }
}
