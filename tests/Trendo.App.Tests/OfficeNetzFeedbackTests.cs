using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Trendo.App.Tests;

/// T-0211: PDF, Druckvorschau, Downloads, Medien – jeder Knopf meldet sich, kein Absturz bei leerem Feld.
public class OfficeNetzFeedbackTests
{
    [AvaloniaTheory(DisplayName = "T-0211: Leere Eingabe / Offline → eigene Meldung statt Stille")]
    [InlineData("doku-pdf", "form-read", "PDF-Datei mit dem Formular")]
    [InlineData("doku-pdf", "form-save", "Erst „Felder lesen“")]
    [InlineData("doku-pdf", "print-preview", "PDF-Datei für die Vorschau")]
    [InlineData("netz-laden", "dl-stop", "kein Download")]
    [InlineData("netz-laden", "page-offline", "Offline speichern")]
    [InlineData("netz-laden", "page-pdf", "Als PDF")]
    [InlineData("kreativ-medien", "media-use", "Video- oder Audiodatei")]
    public void Empty_input_gives_message(string page, string button, string expected)
    {
        var (w, _, _) = Ui.Open();
        w.Show(page);
        var before = w.GetLogicalTexts();
        w.Find<Button>(button).Press();
        Assert.Contains(w.GetLogicalTexts().Except(before), t => t.Contains(expected));
    }

    [AvaloniaFact(DisplayName = "T-0211: Laden ohne Adresse/offline meldet sich")]
    public void Download_start_reports()
    {
        var (w, _, _) = Ui.Open();
        w.Show("netz-laden");
        w.Find<Button>("dl-start").Press();
        Assert.Contains(w.GetLogicalTexts(), t => t.StartsWith("Offline-Modus: Downloads") || t.StartsWith("Bitte eine http"));
    }

    [AvaloniaFact(DisplayName = "T-0211: Sortieren in leerem Ordner meldet „nichts zu sortieren“")]
    public void Sort_empty_folder()
    {
        var (w, _, root) = Ui.Open();
        var d = Path.Combine(root, "leer"); Directory.CreateDirectory(d);
        w.Show("netz-laden");
        w.Find<TextBox>("dlsort-folder").Text = d;
        w.Find<Button>("dlsort-apply").Press();
        Assert.Contains(w.GetLogicalTexts(), t => t.StartsWith("Nichts zu sortieren"));
    }
}
