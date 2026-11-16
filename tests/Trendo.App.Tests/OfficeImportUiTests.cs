using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Trendo.App;
using Trendo.Core.Office;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0682: Fremde Office-Dateien mit Inhalten, die Trendo nicht hält, werden nicht mehr still überschrieben.
/// Geprüft wird der echte Weg: Datei öffnen, Strg+S im Programm, Knöpfe der Hinweisleiste – und danach die Bytes auf der Platte.
/// Die Fremddateien liegen unter tests/Daten/office (handgeschriebenes Roh-OOXML/-ODF, nicht aus Trendos Schreibern).</summary>
public class OfficeImportUiTests
{
    static void Pump() { for (var i = 0; i < 5; i++) Avalonia.Threading.Dispatcher.UIThread.RunJobs(); }
    static void Key(Control c, Key k, KeyModifiers m = KeyModifiers.None) { c.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = k, KeyModifiers = m }); Pump(); }

    /// <summary>Kopie der Fremddatei in einem eigenen Arbeitsordner – die eingecheckte Vorlage wird nie angefasst.</summary>
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

    static string BannerText(MainWindow w, string kind) => w.Find<TextBlock>($"office-import-text-{kind}").Text ?? "";

    [AvaloniaFact(DisplayName = "T-0682 Text: fremde DOCX – Hinweisleiste nennt, was fehlt; Strg+S und „Speichern“ lassen das Original bytegleich; „Als Kopie speichern“ schreibt „Name (Trendo).docx“")]
    public void Text_Strg_S_schuetzt_Original_und_Kopie()
    {
        var (w, _, root) = Ui.Open();
        var f = Fremd(root, "fremd-text.docx"); var before = File.ReadAllBytes(f);
        w.TextOpen(f); w.Show("text"); Pump();
        Assert.Equal(["docx.kopf.weitere", "docx.kopf.zeilen", "docx.textfeld"], w.OfficeImportReport("text")!.Verluste.Select(v => v.Id).Order()); // T-0692: verbundene Zellen bleiben erhalten – nicht mehr im Bericht
        var banner = BannerText(w, "text");
        Assert.StartsWith("Diese Datei enthält Dinge, die Trendo nicht darstellt:", banner);
        Assert.DoesNotContain("Verbundene Zellen", banner); Assert.Contains("Textfelder", banner); Assert.Contains("Beim Speichern in diese Datei gingen sie verloren.", banner);
        w.Find<Button>("office-import-details-text").Press(); Pump();
        Assert.Contains("Kopfzeile", w.Find<TextBlock>("office-import-list-text").Text); Assert.Contains("Hinweis im Textfeld", w.Find<TextBlock>("office-import-list-text").Text);

        // Der Nutzer ändert etwas und drückt Strg+S – nichts wird geschrieben
        w.TextSelect(0, 0, 5); w.TextToggle(Fmt.Bold); w.Show("text"); Pump();
        Assert.True(w.TextDirty);
        Key(w.Find<TextPageView>("text-edit"), Avalonia.Input.Key.S, KeyModifiers.Control);
        Assert.Equal(before, File.ReadAllBytes(f));
        Assert.True(w.TextDirty); Assert.Contains("Nicht gespeichert", w.Find<TextBlock>("text-status").Text);
        w.Find<Button>("text-save").Press(); Pump();                       // der Knopf „Speichern“
        Assert.False(w.TextSaveAs(f));                                     // und der Weg, den auch die KI nimmt
        Assert.Equal(before, File.ReadAllBytes(f));
        Assert.Equal([Path.GetFileName(f)], Directory.GetFiles(Path.GetDirectoryName(f)!).Select(Path.GetFileName)); // weder Kopie noch Sicherung entstanden
        Assert.DoesNotContain(w.Core.Audit.ReadAll(), e => e.Action == "text.save");

        // „Als Kopie speichern“ (Standardknopf)
        w.Find<Button>("office-import-copy-text").Press(); Pump();
        var copy = Path.Combine(Path.GetDirectoryName(f)!, "fremd-text (Trendo).docx");
        Assert.True(File.Exists(copy)); Assert.Equal(before, File.ReadAllBytes(f));
        Assert.Equal(copy, w.TextPath); Assert.False(w.TextDirty);
        Assert.Null(w.TryFind<Border>("office-import-text")); Assert.Null(w.OfficeImportReport("text"));
        Assert.Contains("ist unverändert", w.Find<TextBlock>("text-status").Text);
        Assert.Contains("Umsatz nach Quartal", TextFiles.Open(copy).PlainText());
        // Danach speichert Strg+S ganz normal – in die Kopie
        w.TextSelect(0, 0, 5); w.TextToggle(Fmt.Italic); w.Show("text"); Pump();
        var copyBefore = File.ReadAllBytes(copy);
        Key(w.Find<TextPageView>("text-edit"), Avalonia.Input.Key.S, KeyModifiers.Control);
        Assert.False(w.TextDirty); Assert.NotEqual(copyBefore, File.ReadAllBytes(copy)); Assert.Equal(before, File.ReadAllBytes(f));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "T-0682 Text: „Trotzdem überschreiben“ fragt erst nach; nach „Ja“ liegt das Original bytegleich als „Name.original.docx“ daneben – einmalig")]
    public void Text_Trotzdem_ueberschreiben_sichert_Original()
    {
        var (w, _, root) = Ui.Open();
        var f = Fremd(root, "fremd-text.docx"); var before = File.ReadAllBytes(f); var dir = Path.GetDirectoryName(f)!;
        w.TextOpen(f); w.Show("text"); Pump();
        w.Find<Button>("office-import-overwrite-text").Press(); Pump();
        Assert.Equal(before, File.ReadAllBytes(f)); Assert.Single(Directory.GetFiles(dir)); // der erste Klick schreibt noch nichts
        Assert.StartsWith("Wirklich überschreiben?", BannerText(w, "text")); Assert.Contains("fremd-text.original.docx", BannerText(w, "text"));
        w.Find<Button>("office-import-no-text").Press(); Pump();                           // Abbrechen: zurück zur Leiste, nichts geschrieben
        Assert.StartsWith("Diese Datei enthält", BannerText(w, "text")); Assert.Equal(before, File.ReadAllBytes(f)); Assert.Single(Directory.GetFiles(dir));

        w.Find<Button>("office-import-overwrite-text").Press(); Pump();
        w.Find<Button>("office-import-yes-text").Press(); Pump();
        var backup = Path.Combine(dir, "fremd-text.original.docx");
        Assert.Equal(before, File.ReadAllBytes(backup));                                    // das Original ist gerettet
        Assert.NotEqual(before, File.ReadAllBytes(f)); Assert.False(ImportCheck.For(f).HasLoss); // und die Datei ist jetzt Trendos eigene
        Assert.Equal(f, w.TextPath); Assert.Null(w.TryFind<Border>("office-import-text"));
        Assert.Contains("fremd-text.original.docx", w.Find<TextBlock>("text-status").Text);
        Assert.Contains(w.Core.Audit.ReadAll(), e => e.Action == "office.import.ueberschrieben");

        // Weiteres Speichern: keine Rückfrage, keine zweite Sicherung, die erste bleibt unverändert
        w.TextSelect(0, 0, 5); w.TextToggle(Fmt.Bold); w.Show("text"); Pump();
        Key(w.Find<TextPageView>("text-edit"), Avalonia.Input.Key.S, KeyModifiers.Control);
        Assert.False(w.TextDirty); Assert.Equal(2, Directory.GetFiles(dir).Length); Assert.Equal(before, File.ReadAllBytes(backup));

        // Eigene Datei wieder öffnen: kein Hinweis, Speichern wie bisher
        w.TextOpen(f); w.Show("text"); Pump();
        Assert.Null(w.TryFind<Border>("office-import-text")); Assert.True(w.TextSaveAs(f));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "T-0682 Tabelle: fremde XLSX – Strg+S lässt das Original bytegleich; Kopie und „Trotzdem“ mit Sicherung „Name.original.xlsx“")]
    public void Tabelle_schuetzt_Original()
    {
        var (w, _, root) = Ui.Open();
        var f = Fremd(root, "fremd-tabelle.xlsx"); var before = File.ReadAllBytes(f); var dir = Path.GetDirectoryName(f)!;
        w.SheetOpen(f); w.Show("tabelle"); Pump();
        Assert.Equal(["xlsx.diagramm", "xlsx.verbunden", "xlsx.zahlenformat"], w.OfficeImportReport("tabelle")!.Verluste.Select(v => v.Id).Order());
        Assert.Contains("Verbundene Zellen", BannerText(w, "tabelle")); Assert.Contains("Zahlenformate", BannerText(w, "tabelle"));
        w.SheetSetCell("D1", "neu"); Assert.True(w.SheetDirty);
        w.Show("tabelle"); Pump();
        Key(w.Find<Control>("tabelle-grid"), Avalonia.Input.Key.S, KeyModifiers.Control);
        Assert.Equal(before, File.ReadAllBytes(f)); Assert.True(w.SheetDirty); Assert.Single(Directory.GetFiles(dir));
        Assert.False(w.SheetSaveAs(f)); Assert.Equal(before, File.ReadAllBytes(f));

        w.Find<Button>("office-import-copy-tabelle").Press(); Pump();
        var copy = Path.Combine(dir, "fremd-tabelle (Trendo).xlsx");
        Assert.True(File.Exists(copy)); Assert.Equal(before, File.ReadAllBytes(f)); Assert.Equal(copy, w.SheetPath); Assert.False(w.SheetDirty);
        Assert.Equal("neu", SheetFiles.Open(copy).Sheets[0].Get("D1").Raw);
        Assert.Null(w.TryFind<Border>("office-import-tabelle"));

        // Zweiter Durchgang: trotzdem überschreiben
        w.SheetOpen(f); w.Show("tabelle"); Pump();
        w.Find<Button>("office-import-overwrite-tabelle").Press(); Pump();
        Assert.Equal(before, File.ReadAllBytes(f));
        w.Find<Button>("office-import-yes-tabelle").Press(); Pump();
        Assert.Equal(before, File.ReadAllBytes(Path.Combine(dir, "fremd-tabelle.original.xlsx")));
        Assert.NotEqual(before, File.ReadAllBytes(f)); Assert.False(ImportCheck.For(f).HasLoss);
        Assert.Null(w.TryFind<Border>("office-import-tabelle"));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "T-0682 Präsentation: fremde PPTX – Strg+S lässt das Original bytegleich; Kopie und „Trotzdem“ mit Sicherung „Name.original.pptx“")]
    public void Praesentation_schuetzt_Original()
    {
        var (w, _, root) = Ui.Open();
        var f = Fremd(root, "fremd-praesentation.pptx"); var before = File.ReadAllBytes(f); var dir = Path.GetDirectoryName(f)!;
        w.DeckOpen(f); w.Show("praesentation"); Pump();
        Assert.Equal(["pptx.animation", "pptx.design", "pptx.textformat"], w.OfficeImportReport("praesentation")!.Verluste.Select(v => v.Id).Order());
        Assert.Contains("Mehrere Textformate in einem Textfeld", BannerText(w, "praesentation")); Assert.Contains("Animationen", BannerText(w, "praesentation"));
        Key(w.Find<Control>("praesentation-canvas"), Avalonia.Input.Key.S, KeyModifiers.Control);
        Assert.Equal(before, File.ReadAllBytes(f)); Assert.Single(Directory.GetFiles(dir));
        Assert.False(w.DeckSaveAs(f)); Assert.Equal(before, File.ReadAllBytes(f));

        w.Find<Button>("office-import-copy-praesentation").Press(); Pump();
        var copy = Path.Combine(dir, "fremd-praesentation (Trendo).pptx");
        Assert.True(File.Exists(copy)); Assert.Equal(before, File.ReadAllBytes(f)); Assert.Equal(copy, w.DeckPath);
        Assert.Contains(SlideFiles.Open(copy).Slides[0].Shapes, s => s.Text.Contains("12 Prozent"));

        w.DeckOpen(f); w.Show("praesentation"); Pump();
        w.Find<Button>("office-import-overwrite-praesentation").Press(); Pump();
        Assert.Equal(before, File.ReadAllBytes(f));
        w.Find<Button>("office-import-yes-praesentation").Press(); Pump();
        Assert.Equal(before, File.ReadAllBytes(Path.Combine(dir, "fremd-praesentation.original.pptx")));
        Assert.NotEqual(before, File.ReadAllBytes(f)); Assert.False(ImportCheck.For(f).HasLoss);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "T-0682: ODT wie DOCX geschützt; ODS und ODP kann Trendo nicht zurückschreiben – nur „Als Kopie speichern“ (XLSX/PPTX), Strg+S lässt das Original; ältere Sicherung wird nicht überschrieben")]
    public void Odf_und_vorhandene_Sicherung()
    {
        var (w, _, root) = Ui.Open();
        // ODT: schreibbar, also mit „Trotzdem überschreiben“; liegt schon eine andere „.original“-Datei da, bleibt sie erhalten
        var odt = Fremd(root, "fremd-text.odt"); var odtBefore = File.ReadAllBytes(odt); var dir = Path.GetDirectoryName(odt)!;
        File.WriteAllText(Path.Combine(dir, "fremd-text.original.odt"), "ältere Sicherung");
        w.TextOpen(odt); w.Show("text"); Pump();
        Assert.Contains("Textfelder", BannerText(w, "text"));
        Assert.False(w.TextSaveAs(odt)); Assert.Equal(odtBefore, File.ReadAllBytes(odt));
        w.Find<Button>("office-import-overwrite-text").Press(); Pump();
        w.Find<Button>("office-import-yes-text").Press(); Pump();
        Assert.Equal("ältere Sicherung", File.ReadAllText(Path.Combine(dir, "fremd-text.original.odt")));
        Assert.Equal(odtBefore, File.ReadAllBytes(Path.Combine(dir, "fremd-text.original 2.odt")));
        Assert.NotEqual(odtBefore, File.ReadAllBytes(odt));

        // ODS
        var ods = Fremd(root, "fremd-tabelle.ods"); var odsBefore = File.ReadAllBytes(ods);
        w.SheetOpen(ods); w.Show("tabelle"); Pump();
        Assert.Contains("Trendo kann dieses Format nicht speichern", BannerText(w, "tabelle"));
        Assert.Null(w.TryFind<Button>("office-import-overwrite-tabelle"));
        Key(w.Find<Control>("tabelle-grid"), Avalonia.Input.Key.S, KeyModifiers.Control);
        Assert.Equal(odsBefore, File.ReadAllBytes(ods)); Assert.Null(w.OfficeOverwriteOriginal("tabelle"));
        w.Find<Button>("office-import-copy-tabelle").Press(); Pump();
        var xlsx = Path.Combine(Path.GetDirectoryName(ods)!, "fremd-tabelle (Trendo).xlsx");
        Assert.True(File.Exists(xlsx)); Assert.Equal(xlsx, w.SheetPath); Assert.Equal(odsBefore, File.ReadAllBytes(ods));

        // ODP
        var odp = Fremd(root, "fremd-praesentation.odp"); var odpBefore = File.ReadAllBytes(odp);
        w.DeckOpen(odp); w.Show("praesentation"); Pump();
        Assert.Null(w.TryFind<Button>("office-import-overwrite-praesentation"));
        w.Find<Button>("office-import-copy-praesentation").Press(); Pump();
        Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(odp)!, "fremd-praesentation (Trendo).pptx"))); Assert.Equal(odpBefore, File.ReadAllBytes(odp));

        // „Neu“ räumt den Hinweis weg
        w.TextOpen(Fremd(root, "fremd-text.docx")); w.TextNew(); w.Show("text"); Pump();
        Assert.Null(w.TryFind<Border>("office-import-text")); Assert.Null(w.OfficeImportReport("text"));
        w.Close();
    }
}
