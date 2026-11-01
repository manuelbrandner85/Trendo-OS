using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// T-0309: Recherche mit Markierungen – auswählen und markieren, farbig im Text, Notiz, Farbe ändern, Zitat kopieren, Literaturliste (APA), Bericht, Sitzungsende.
public class ResearchMarks_Ui
{
    [AvaloniaFact(DisplayName = "T-0309: Recherche – Auswahl markieren (farbig im Text), Notiz, Farbe, Zitat mit Quelle kopieren, Literaturliste einfach/APA, Bericht mit Zitaten, Sitzungsende löscht")]
    public async Task Ui_marks()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1100;
        void Click(string name) { var b = V<Control>(w, name); b.BringIntoView(); Settle(); RealClick(w, V<Control>(w, name)); Settle(); }
        w.ReaderFetchOverride = u => Task.FromResult(("text/html", "<html><head><title>Bienen im Garten</title></head><body><p>Wildbienen bestäuben viele Pflanzen. Sie brauchen offene Bodenstellen zum Nisten.</p></body></html>", u));
        w.ResearchReportFolder = Path.Combine(root, "berichte");
        await w.ReadUrlAsync("https://www.example.org/bienen"); w.Show("recherche"); Settle();
        var text = w.Find<SelectableTextBlock>("rs-text"); var full = text.Text!;
        var start = full.IndexOf("Wildbienen bestäuben"); text.SelectionStart = start; text.SelectionEnd = start + "Wildbienen bestäuben viele Pflanzen.".Length;
        w.Find<TextBox>("rs-mark-note").Text = "Kernaussage";
        Click("rs-mark-gelb");
        var h = Assert.Single(w.Desk.Highlights()); Assert.Equal(("Wildbienen bestäuben viele Pflanzen.", "gelb", "Kernaussage"), (h.Text, h.Color, h.Note));
        var shown = w.Find<SelectableTextBlock>("rs-text"); Assert.True(shown.Inlines!.Count >= 2); // markierter Abschnitt eigenes Stück
        Assert.NotNull(w.Find<Border>("rs-hl-0"));
        // Farbe ändern, Notiz per Enter
        Click("rs-hl-color-0-gruen"); Assert.Equal("grün", w.Desk.Highlights()[0].Color);
        var note = w.Find<TextBox>("rs-hl-note-0"); note.Text = "Für den Vortrag";
        note.RaiseEvent(new Avalonia.Input.KeyEventArgs { RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Enter }); Settle();
        Assert.Equal("Für den Vortrag", w.Desk.Highlights()[0].Note);
        // zweite Stelle, ohne Auswahl → Meldung
        var t2 = w.Find<SelectableTextBlock>("rs-text"); t2.SelectionStart = t2.SelectionEnd = 0; Click("rs-mark-blau");
        Assert.Single(w.Desk.Highlights()); Assert.Contains("auswählen", w.Find<TextBlock>("rs-msg").Text);
        // Zitat kopieren
        Click("rs-hl-copy-0"); Assert.StartsWith("„Wildbienen bestäuben viele Pflanzen.“ (Bienen im Garten. In: example.org.", w.LastClipboardText);
        // Literaturliste: einfach, dann APA
        Click("rs-bib-copy"); Assert.Contains("Online unter: https://www.example.org/bienen", w.LastClipboardText);
        w.Find<ComboBox>("rs-cite-style").SelectedIndex = 1; Settle(); Click("rs-bib-copy"); Assert.Contains("Abgerufen am", w.LastClipboardText);
        // Bericht
        w.Find<TextBox>("rs-note").Text = "Notiz zur Recherche"; Click("rs-export");
        var md = File.ReadAllText(Assert.Single(Directory.GetFiles(w.ResearchReportFolder, "recherche-*.md")));
        Assert.Contains("Zitate und Markierungen", md); Assert.Contains("Für den Vortrag", md); Assert.Contains("Abgerufen am", md);
        // Sitzungsende löscht Markierungen
        Click("rs-end"); Assert.Empty(w.Desk.Highlights());
    }
}
