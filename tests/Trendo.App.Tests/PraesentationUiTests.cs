using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core.Office;
using Xunit;
namespace Trendo.App.Tests;

/// <summary>T-0229: Oberfläche von Trendo Präsentation.</summary>
public class T0229_TrendoPraesentationUi
{
    static void Pump() { for (var i = 0; i < 5; i++) Dispatcher.UIThread.RunJobs(); }
    static void Key(Control c, Key k, KeyModifiers m = KeyModifiers.None) => c.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = k, KeyModifiers = m });

    [AvaloniaFact(DisplayName = "T-0229 G1/P7: Startseite mit Vorlagen; Folien aus Stichpunkten; Folienleiste zeigt alle Folien")]
    public void Start_and_outline()
    {
        var (w, _, _) = Ui.Open();
        w.Show("praesentation"); Pump();
        Assert.True(w.DeckStartVisible);
        foreach (var id in new[] { "leer", "vortrag", "projekt", "fotoalbum" }) Assert.NotNull(w.TryFind<Button>("praesentation-tpl-" + id));
        w.Find<TextBox>("praesentation-outline").Text = "Sommerfest\n  - für alle\nAblauf\n  - Begrüßung\n  - Kuchen\nDanke";
        w.Find<Button>("praesentation-from-outline").Press(); Pump();
        Assert.False(w.DeckStartVisible);
        Assert.Equal(3, w.Deck.Slides.Count);
        Assert.NotNull(w.TryFind<Button>("praesentation-slide-2"));
        Assert.Equal("Begrüßung\nKuchen", w.Deck.Slides[1].ByRole("body")!.Text);
        Assert.Contains("Folie 1 von 3", w.Find<TextBlock>("praesentation-status").Text);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "T-0229 P1/P2/P3: Neue Folie mit Layout, Text bearbeiten, Formen/Tabelle einfügen, mit Pfeiltasten schieben, Design, Sortieren, Rückgängig")]
    public void Edit_slides()
    {
        var (w, _, _) = Ui.Open();
        w.DeckNew(); w.Show("praesentation"); Pump();
        w.DeckType(0, "Mein Vortrag"); Pump();
        Assert.Equal("Mein Vortrag", w.Deck.Slides[0].Title);
        var menu = w.Find<Button>("praesentation-new").ContextMenu!;
        menu.Items.Cast<MenuItem>().First(m => m.Name == "praesentation-new-zweispalten").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent)); Pump();
        Assert.Equal(2, w.Deck.Slides.Count); Assert.Equal(1, w.DeckIndex); Assert.Equal(SlideLayout.ZweiSpalten, w.CurrentSlide.Layout);
        w.Find<Button>("praesentation-rib-einfuegen").Press(); Pump();
        w.Find<Button>("praesentation-ins-ellipse").Press(); Pump();
        var ell = w.DeckSelected!; Assert.Equal(ShapeKind.Ellipse, ell.Kind);
        var x0 = ell.X;
        Key(w.Find<SlideView>("praesentation-canvas"), Avalonia.Input.Key.Right, KeyModifiers.Shift); Pump();
        Assert.Equal(x0 + 1, w.DeckSelected!.X, 3);
        w.Find<Button>("praesentation-ins-table").Press(); Pump();
        var ti = w.CurrentSlide.Shapes.Count - 1;
        w.DeckType(ti, "Name | Wert\nA | 1"); Pump();
        Assert.Equal(["A", "1"], w.CurrentSlide.Shapes[ti].Table![1]);
        w.Find<Button>("praesentation-rib-entwurf").Press(); Pump();
        w.Find<Button>("praesentation-theme-wald").Press(); Pump();
        Assert.Equal("wald", w.Deck.Theme.Id);
        w.Find<Button>("praesentation-up").Press(); Pump();
        Assert.Equal(SlideLayout.ZweiSpalten, w.Deck.Slides[0].Layout); Assert.Equal(0, w.DeckIndex);
        w.Find<Button>("praesentation-rib-start").Press(); Pump();
        w.Find<Button>("praesentation-undo").Press(); Pump();
        Assert.Equal(SlideLayout.Titel, w.Deck.Slides[0].Layout);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "T-0229 P4/P5: Vorführung – Animationen per Klick, dann nächste Folie, schwarz, Laserpointer, Referentenansicht mit Notizen, Esc beendet")]
    public void Slideshow()
    {
        var (w, _, _) = Ui.Open();
        w.DeckFromOutline("Titel\n  - Untertitel\nFolie zwei\n  - Punkt\n> Meine Notiz\nEnde");
        w.Show("praesentation"); Pump();
        w.DeckGo(1); w.DeckSelect(1);
        w.Find<Button>("praesentation-rib-animationen").Press(); Pump();
        w.Find<Button>("praesentation-an-einblenden").Press(); Pump();
        Assert.Equal(Animation.Einblenden, w.Deck.Slides[1].Shapes[1].Animation);
        w.Find<Button>("praesentation-rib-vorfuehren").Press(); Pump();
        w.Find<Button>("praesentation-show-presenter").Press(); Pump();
        Assert.NotNull(w.ShowWindow);
        Assert.Equal(1, w.ShowIndex);
        Assert.Equal(0, w.ShowRevealed);
        w.ShowKey(Avalonia.Input.Key.Space); Assert.Equal(1, w.ShowRevealed); Assert.Equal(1, w.ShowIndex);   // erst die Animation
        w.ShowKey(Avalonia.Input.Key.Right); Assert.Equal(2, w.ShowIndex);                                   // dann die Folie
        w.ShowKey(Avalonia.Input.Key.Left); Assert.Equal(1, w.ShowIndex); Assert.Equal(1, w.ShowRevealed);
        var notes = w.ShowWindow!.GetLogicalDescendants().OfType<TextBlock>().First(t => t.Name == "show-notes");
        Assert.Equal("Meine Notiz", notes.Text);
        w.ShowKey(Avalonia.Input.Key.S); Assert.True(w.ShowBlack);
        w.ShowKey(Avalonia.Input.Key.Space); Assert.False(w.ShowBlack); Assert.Equal(1, w.ShowIndex);
        w.ShowKey(Avalonia.Input.Key.L); Assert.True(w.ShowLaser);
        w.ShowKey(Avalonia.Input.Key.Escape); Pump();
        Assert.Null(w.ShowWindow);
        Assert.Equal(1, w.DeckIndex);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "T-0229 P6: Speichern als PPTX und wieder öffnen, Zuletzt geöffnet, Als PDF, Diagramm aus Trendo Tabelle")]
    public void Save_pdf_chart()
    {
        var (w, _, root) = Ui.Open();
        w.SheetNew("haushalt"); w.SheetSelect("A1:C7"); w.SheetInsertChart(ChartKind.Balken);
        w.DeckNew("vortrag"); w.Show("praesentation"); Pump();
        w.DeckGo(1); var ch = w.DeckInsert(ShapeKind.Diagramm);
        Assert.Equal("Betrag", ch.Text); Assert.Equal(2450, ch.ChartValues![0]);
        var f = Path.Combine(root, "vortrag.pptx"); w.DeckSaveAs(f);
        Assert.False(w.DeckDirty);
        w.DeckNew(); w.DeckOpen(f);
        Assert.Equal(5, w.Deck.Slides.Count);
        Assert.Contains(w.Deck.Slides[1].Shapes, s => s.Kind == ShapeKind.Diagramm);
        var pdf = w.DeckExportPdf();
        Assert.True(File.Exists(pdf));
        w.Find<Button>("praesentation-file").ContextMenu!.Items.Cast<MenuItem>().First(m => m.Name == "praesentation-m-start").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent)); Pump();
        Assert.NotNull(w.TryFind<Button>("praesentation-recent-0"));
        w.Close();
    }
}
