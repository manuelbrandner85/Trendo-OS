using System.IO.Compression;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Trendo.App;
using Trendo.Core.Office;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0701: Trendo Tabelle und Trendo Präsentation im Programmfenster (ohne Bildschirm) – fremde Datei öffnen, bearbeiten, mit Strg+S speichern,
/// und danach die Bytes der Datei prüfen. Fremddateien aus tests/Daten/office (handgeschriebenes OOXML, nicht aus Trendos Schreibern).</summary>
public class T0701OfficeUiTests
{
    static void Pump() { for (var i = 0; i < 5; i++) Avalonia.Threading.Dispatcher.UIThread.RunJobs(); }
    static void Key(Control c, Key k, KeyModifiers m = KeyModifiers.None) { c.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = k, KeyModifiers = m }); Pump(); }

    static string Fremd(string root, string name)
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "Trendo.sln")))
            {
                var dir = Path.Combine(root, "fremd-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(dir);
                var f = Path.Combine(dir, name); File.Copy(Path.Combine(d.FullName, "tests", "Daten", "office", name), f); return f;
            }
        throw new FileNotFoundException(name);
    }

    static string Part(string file, string part) { using var z = ZipFile.OpenRead(file); using var s = new StreamReader(z.GetEntry(part)!.Open()); return s.ReadToEnd(); }

    [AvaloniaFact(DisplayName = "T-0701 Tabelle: fremde XLSX öffnen – kein Verlust-Hinweis, verbundene Zelle wird als Ganzes gewählt; verbinden, kursiv (Strg+I), eigenes Zahlenformat, Rechtsklick-Menü; Strg+S schreibt alles in die Datei, Formatcode und Diagramm-Einzelheiten bleiben")]
    public void Tabelle_fremd_bearbeiten_und_speichern()
    {
        var (w, _, root) = Ui.Open();
        var f = Fremd(root, "fremd-tabelle.xlsx");
        w.SheetOpen(f); w.Show("tabelle"); Pump();
        Assert.Null(w.OfficeImportReport("tabelle")); Assert.Null(w.TryFind<Border>("office-import-tabelle")); // nichts geht verloren, also kein Hinweis
        var grid = w.Find<SheetGridView>("tabelle-grid");
        grid.Select(1, 1); Key(grid, Avalonia.Input.Key.Up); // in die verbundene Zelle A1:C1 hinein
        Assert.Equal((0, 0), grid.Active); Assert.Equal((0, 2), grid.Anchor);
        w.Show("tabelle"); Pump();
        Assert.Contains("Verbund lösen", string.Join(" ", w.GetLogicalTexts()));
        Assert.NotNull(grid.ContextMenu); Assert.Contains(grid.ContextMenu!.Items.OfType<MenuItem>(), m => m.Name == "tabelle-k-merge");

        // D2:E2 verbinden (Knopf im Menüband), kursiv per Strg+I, eigenes Zahlenformat für C3
        w.SheetSelect("D2:E2"); w.SheetSetCell("D2", "Hinweis"); w.SheetSelect("D2:E2"); w.Show("tabelle"); Pump();
        w.Find<Button>("tabelle-merge").Press(); Pump();
        Assert.Contains(new MergeRange(1, 3, 1, 4), w.ActiveSheet.Merges);
        Key(w.Find<SheetGridView>("tabelle-grid"), Avalonia.Input.Key.I, KeyModifiers.Control);
        Assert.True(w.ActiveSheet.Get("D2").Italic);
        w.SheetSelect("C3"); w.SheetSetNumCode("0.0\" cm\"");
        Assert.Equal("10,5 cm", new SheetCalc(w.SheetDocument).Display(w.ActiveSheet, 2, 2));
        w.SheetUndo(); Assert.Equal("0.000\" kg\"", w.ActiveSheet.Get("C3")!.NumCode); // rückgängig: wieder der Code aus der Datei
        w.SheetRedo(); Assert.Equal("0.0\" cm\"", w.ActiveSheet.Get("C3")!.NumCode);

        w.Show("tabelle"); Pump();
        Key(w.Find<SheetGridView>("tabelle-grid"), Avalonia.Input.Key.S, KeyModifiers.Control);
        Assert.Equal("0.0\" cm\"", w.ActiveSheet.Get("C3")!.NumCode);
        Assert.False(w.SheetDirty);
        var sheet = Part(f, "xl/worksheets/sheet1.xml"); var styles = Part(f, "xl/styles.xml"); var chart = Part(f, "xl/charts/chart1.xml");
        Assert.Contains("<mergeCell ref=\"A1:C1\"/>", sheet); Assert.Contains("<mergeCell ref=\"D2:E2\"/>", sheet);
        Assert.Contains("formatCode=\"0.000&quot; kg&quot;\"", styles); Assert.Contains("formatCode=\"0.0&quot; cm&quot;\"", styles); Assert.Contains("<i/>", styles);
        Assert.Contains(">Probe<", chart); Assert.Contains("C0504D", chart);
        var back = SheetFiles.Open(f);
        Assert.False(back.Import!.HasLoss); Assert.True(back.Sheets[0].Get("D2").Italic);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "T-0701 Präsentation: fremde PPTX – Design der Datei als eigene Kachel; im Textfeld auf der Folie ein Wort markieren und Strg+B/Strg+I nur dort; Strg+S behält Design, Animation und alle Textformate")]
    public void Praesentation_fremd_teilformat_und_speichern()
    {
        var (w, _, root) = Ui.Open();
        var f = Fremd(root, "fremd-praesentation.pptx");
        w.DeckOpen(f); w.Show("praesentation"); Pump();
        Assert.Null(w.OfficeImportReport("praesentation"));
        Assert.True(w.Deck.KeepsDesign);
        w.Find<Button>("praesentation-rib-entwurf").Press(); Pump();
        Assert.NotNull(w.TryFind<Button>("praesentation-theme-datei"));
        w.Find<Button>("praesentation-rib-start").Press(); Pump();

        var idx = w.Deck.Slides[0].Shapes.FindIndex(s => s.Text.Contains("12 Prozent"));
        w.DeckSelect(idx); w.Show("praesentation"); Pump();
        var canvas = w.Find<SlideView>("praesentation-canvas");
        Key(canvas, Avalonia.Input.Key.F2); // Textfeld direkt auf der Folie öffnen
        var tb = w.Find<TextBox>("praesentation-editor");
        tb.Text = "Der Umsatz ist um 12 Prozent kräftig gestiegen.";
        var start = tb.Text.IndexOf("kräftig", StringComparison.Ordinal); tb.SelectionStart = start; tb.SelectionEnd = start + 7;
        Key(tb, Avalonia.Input.Key.I, KeyModifiers.Control);
        Key(tb, Avalonia.Input.Key.B, KeyModifiers.Control);
        var shape = w.Deck.Slides[0].Shapes[idx];
        Assert.Equal("Der Umsatz ist um 12 Prozent kräftig gestiegen.", shape.Text);
        Assert.Equal(new TextRun("kräftig", 18, true, true), shape.Runs!.Single(r => r.Text == "kräftig")); // neu Getipptes übernimmt Größe 18 der Stelle davor (wie PowerPoint)
        Assert.Equal(new TextRun("12 Prozent", 28, true, Color: "#C00000"), shape.Runs!.Single(r => r.Text == "12 Prozent")); // das alte Format bleibt
        w.DeckUndo(); Assert.DoesNotContain(w.Deck.Slides[0].Shapes[idx].Runs!, r => r.Text == "kräftig" && r.Bold == true); // rückgängig machbar
        w.DeckRedo();

        w.Show("praesentation"); Pump();
        Key(w.Find<SlideView>("praesentation-canvas"), Avalonia.Input.Key.S, KeyModifiers.Control);
        var xml = Part(f, "ppt/slides/slide1.xml");
        Assert.Contains("b=\"1\" i=\"1\"", xml); Assert.Contains(">kräftig</a:t>", xml); Assert.Contains("sz=\"2800\" b=\"1\"", xml);
        Assert.Contains("animRot", xml); Assert.Contains("Office Theme", Part(f, "ppt/theme/theme1.xml"));
        Assert.False(SlideFiles.Open(f).Import!.HasLoss);
        w.Close();
    }
}
