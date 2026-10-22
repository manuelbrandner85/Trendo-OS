using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Trendo.App.Tests;

/// T-0209: Kleine Helfer & Dateien – leere Felder führen zu klarer Meldung, nie zu Absturz oder Stille.
public class HelferFeedbackTests
{
    [AvaloniaTheory(DisplayName = "T-0209: Jeder Knopf mit leeren Feldern zeigt eine neue Meldung")]
    [InlineData("alltag-helfer", "cur-run", "den Betrag")]
    [InlineData("alltag-helfer", "color-pick", "ein Bild für die Pipette")]
    [InlineData("alltag-helfer", "clip-add", "Text zum Merken")]
    [InlineData("alltag-dateien", "ren-apply", "den Ordner mit den Dateien")]
    [InlineData("alltag-dateien", "du-run", "Speicherplatz")]
    [InlineData("alltag-dateien", "photo-apply", "den Ordner mit den Fotos")]
    [InlineData("alltag-dateien", "trash-empty", "Nichts zu leeren")]
    [InlineData("alltag-bilder", "img-left", "zum Drehen")]
    [InlineData("alltag-bilder", "img-resize", "zum Verkleinern")]
    public void Empty_input_gives_message(string page, string button, string expected)
    {
        var (w, _, _) = Ui.Open();
        w.Show(page);
        var before = w.GetLogicalTexts();
        w.Find<Button>(button).Press();
        Assert.Contains(w.GetLogicalTexts().Except(before), t => t.Contains(expected));
    }

    [AvaloniaFact(DisplayName = "T-0209: Geld umrechnen mit Text statt Zahl → Hinweis, kein Absturz")]
    public void Currency_bad_number()
    {
        var (w, _, _) = Ui.Open();
        w.Show("alltag-helfer");
        w.Find<TextBox>("cur-amount").Text = "abc"; w.Find<TextBox>("cur-rate").Text = "1,08";
        w.Find<Button>("cur-run").Press();
        Assert.Contains(w.GetLogicalTexts(), t => t.Contains("keine gültige Zahl"));
    }

    [AvaloniaFact(DisplayName = "T-0209: Papierkorb anzeigen meldet Inhalt oder „leer“")]
    public void Trash_show_reports()
    {
        var (w, _, _) = Ui.Open();
        w.Show("alltag-dateien");
        w.Find<Button>("trash-show").Press();
        Assert.Contains(w.GetLogicalTexts(), t => t.StartsWith("Der Papierkorb ist leer") || t.StartsWith("Papierkorb gelesen"));
    }
}
