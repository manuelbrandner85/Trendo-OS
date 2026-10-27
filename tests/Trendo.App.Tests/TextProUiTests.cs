using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core.Office;
using Xunit;
namespace Trendo.App.Tests;

/// <summary>T-0230: Menüband von Trendo Text (W1–W9).</summary>
public class T0230_TrendoTextProUi
{
    static void Pump() { for (var i = 0; i < 5; i++) Dispatcher.UIThread.RunJobs(); }
    static void Menu(MainWindow w, string button, string item) =>
        w.Find<Button>(button).ContextMenu!.Items.Cast<MenuItem>().First(m => m.Name == item).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

    [AvaloniaFact(DisplayName = "T-0230 W1/W2/W5: Formatvorlagen-Galerie, Format übertragen, Link/Kommentar/Fußnote über das Eingabefeld, Verzeichnis, Navigation")]
    public void Insert_and_styles()
    {
        var (w, _, _) = Ui.Open();
        w.TextNew(); w.Show("text"); Pump();
        w.Find<TextBox>("text-edit").Text = "Einleitung"; Pump();
        w.Find<Button>("text-gallery-heading1").Press(); Pump();
        Assert.Equal(ParaStyle.Heading1, ((Para)w.TextDocument.Blocks[0]).Style);
        w.TextDocument.Blocks.Add(new Para("Mehr bei den Stadtwerken.")); w.TextSelect(1, 9, 15); w.Show("text"); Pump();
        w.Find<Button>("text-rib-einfuegen").Press(); Pump();
        w.Find<Button>("text-link").Press(); Pump();
        w.Find<TextBox>("text-prompt").Text = "https://example.org"; w.Find<Button>("text-prompt-ok").Press(); Pump();
        var p = (Para)w.TextDocument.Blocks[1];
        Assert.Equal((9, 15, "https://example.org"), (p.Marks[0].Start, p.Marks[0].Length, p.Marks[0].Value));
        w.TextSelect(1, 0, 4); w.Find<Button>("text-comment").Press(); Pump();
        w.Find<TextBox>("text-prompt").Text = "Stimmt das?"; w.Find<Button>("text-prompt-ok").Press(); Pump();
        Assert.Contains(p.Marks, m => m.Kind == MarkKind.Comment && m.Value == "Stimmt das?");
        w.TextSelect(1, 25, 0);
        w.Find<Button>("text-rib-referenzen").Press(); Pump();
        w.Find<Button>("text-footnote").Press(); Pump();
        w.Find<TextBox>("text-prompt").Text = "Laut Webseite"; w.Find<Button>("text-prompt-ok").Press(); Pump();
        Assert.Contains(p.Marks, m => m.Kind == MarkKind.Footnote && m.Start == 25);
        w.Find<Button>("text-toc2").Press(); Pump();
        Assert.Single(w.TextDocument.Blocks.OfType<TocBlock>());
        w.Find<Button>("text-rib-ansicht").Press(); Pump();
        w.Find<Button>("text-nav").Press(); Pump();
        Assert.NotNull(w.TryFind<Button>("text-nav-0"));
        // Format übertragen: Überschrift-Absatzart auf einen anderen Absatz
        w.Find<Button>("text-rib-start").Press(); Pump();
        w.TextSelect(0, 0, 0); w.TextFormatPainter(); w.TextSelect(w.TextDocument.Blocks.Count - 1, 0, 3); w.TextFormatPainter();
        Assert.Equal(ParaStyle.Heading1, ((Para)w.TextDocument.Blocks[^1]).Style);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "T-0230 W3/W4: Design, Wasserzeichen, Ränder, Querformat, Spalten, Einzug, Seitenzahlen – im Blatt sichtbar und im Dokument gespeichert")]
    public void Design_and_layout()
    {
        var (w, _, root) = Ui.Open();
        w.TextNew(); w.Show("text"); Pump();
        w.Find<Button>("text-rib-entwurf").Press(); Pump();
        w.Find<Button>("text-design-modern").Press(); Pump();
        Menu(w, "text-watermark", "text-watermark-1"); Pump();
        Assert.Equal("modern", w.TextDocument.Page.Design); Assert.Equal("ENTWURF", w.TextDocument.Page.Watermark);
        Assert.NotNull(w.TryFind<TextBlock>("text-watermark-view"));
        w.Find<Button>("text-rib-layout").Press(); Pump();
        Menu(w, "text-margins", "text-margins-1"); w.Find<Button>("text-orient").Press(); Menu(w, "text-columns", "text-columns-1");
        w.Find<Button>("text-indent-more").Press(); Pump();
        var pg = w.TextDocument.Page;
        Assert.Equal(1.27, pg.Left); Assert.True(pg.Landscape); Assert.Equal(2, pg.Columns);
        Assert.Equal(0.5, ((Para)w.TextDocument.Blocks[0]).IndentCm);
        Assert.True(w.Find<Border>("text-sheet").Width > 900);
        w.Find<Button>("text-rib-einfuegen").Press(); Pump();
        w.Find<Button>("text-pagenumbers").Press(); Pump();
        Assert.Contains("Seite 1", w.Find<TextBlock>("text-footer-view").Text);
        var f = Path.Combine(root, "layout.docx"); w.TextSaveAs(f);
        var back = TextFiles.Open(f);
        Assert.True(back.Page.Landscape); Assert.Equal("ENTWURF", back.Page.Watermark); Assert.True(back.Page.PageNumbers);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "T-0230 W6/W7: Serienbrief aus Tabelle mit Feldern und Etiketten; Vergleichen, annehmen; Barrierefreiheit und Kommentare in der Seitenleiste")]
    public void Merge_review()
    {
        var (w, _, root) = Ui.Open();
        var csv = Path.Combine(root, "adressen.csv");
        File.WriteAllText(csv, "Vorname;Nachname;Straße;PLZ;Ort\nErika;Muster;Hauptstraße 1;10115;Berlin\nMax;Beispiel;Ringweg 2;20095;Hamburg\n");
        w.TextNew(); w.Show("text"); Pump();
        w.Find<TextBox>("text-edit").Text = "Hallo "; Pump();
        w.TextMergeSource(csv); w.TextSelect(0, 6, 0);
        w.TextRibbon = "sendungen"; w.Show("text"); Pump();
        Menu(w, "text-merge-field", "text-merge-field-0"); Pump();
        Assert.Equal("Hallo «Vorname»", ((Para)w.TextDocument.Blocks[0]).Text);
        Assert.NotNull(w.TryFind<StackPanel>("text-merge-panel"));
        var docx = w.TextMergeCreate()!;
        var merged = TextFiles.Open(docx);
        Assert.Equal(["Hallo Erika", "Hallo Max"], merged.Paras.Select(p => p.Text).Where(t => t.Length > 0));
        Assert.True(File.Exists(Path.ChangeExtension(docx, ".pdf")));
        Assert.True(File.Exists(w.TextLabels()!));
        // Vergleichen mit älterer Fassung
        var old = Path.Combine(root, "alt.docx"); TextFiles.Save(new TextDoc { Blocks = [new Para("Hallo «Vorname»"), new Para("Alter Satz.")] }, old);
        w.TextCompareWith(old);
        Assert.True(TextTools.HasChanges(w.TextDocument));
        w.TextRibbon = "pruefen"; w.Show("text"); Pump();
        w.Find<Button>("text-accept").Press(); Pump();
        Assert.False(TextTools.HasChanges(w.TextDocument));
        Assert.DoesNotContain(w.TextDocument.Paras, p => p.Text == "Alter Satz.");
        // Barrierefreiheit
        w.TextDocument.Blocks.Add(new Picture { Data = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==") });
        w.Find<Button>("text-access").Press(); Pump();
        Assert.NotNull(w.TryFind<StackPanel>("text-access-panel"));
        w.Find<Button>("text-comments").Press(); Pump();
        Assert.NotNull(w.TryFind<StackPanel>("text-comments-panel"));
        w.Close();
    }
}
