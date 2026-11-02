using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Xunit;
namespace Trendo.App.Tests;

/// Wunsch 02.11.2026: „Briefe schreiben viel einfacher“ – Assistent in 3 Schritten, alles löschbar mit Papierkorb und Rückgängig.
public class LetterWizard_Ui
{
    static void Pump() { for (var i = 0; i < 5; i++) Avalonia.Threading.Dispatcher.UIThread.RunJobs(); }

    [AvaloniaFact(DisplayName = "Brief-Assistent: 1 Was? → Vorlage springt zu 2 An wen? → 3 Text & Design → Fertig speichert; „Alles leeren“ mit Rückgängig")]
    public void Wizard()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.Show("brief"); Pump();
            Assert.Equal(1, w.LetterStep);
            Assert.True(w.Find<StackPanel>("letter-wiz-1").IsVisible); Assert.False(w.Find<StackPanel>("letter-wiz-3").IsVisible);
            w.Find<Button>("letter-gallery-widerruf").Press(); Pump();
            Assert.Equal(2, w.LetterStep); // Vorlage gewählt → weiter zu „An wen?“
            Assert.True(w.Find<StackPanel>("letter-wiz-2").IsVisible);
            w.Find<TextBox>("letter-sender-name").Text = "Erika Muster"; w.Find<TextBox>("letter-sender-street").Text = "Hauptstraße 1"; w.Find<TextBox>("letter-sender-city").Text = "10115 Berlin";
            w.Find<TextBox>("letter-recipient").Text = "Versand GmbH\nPostfach 1\n20095 Hamburg"; Pump();
            w.Find<Button>("letter-wiz-next-2").Press(); Pump();
            Assert.Equal(3, w.LetterStep);
            Assert.True(w.Find<StackPanel>("letter-wiz-3").IsVisible);
            Assert.NotNull(w.TryFind<Button>("letter-wiz-done"));
            Assert.NotNull(w.TryFind<Expander>("letter-more")); // Profi-Funktionen unter „Mehr“
            // „Alles auf einer Seite“ zeigt alle Schritte
            w.Find<CheckBox>("letter-wiz-all").IsChecked = true; Pump();
            Assert.True(w.Find<StackPanel>("letter-wiz-1").IsVisible && w.Find<StackPanel>("letter-wiz-2").IsVisible && w.Find<StackPanel>("letter-wiz-3").IsVisible);
            w.Find<CheckBox>("letter-wiz-all").IsChecked = false; Pump();
            // Alles leeren – und zurück
            var subject = w.Letter.Subject;
            w.Find<Button>("letter-clear").Press(); Pump();
            Assert.Equal("", w.Letter.SenderName); Assert.Equal(1, w.LetterStep);
            Assert.Contains(w.GetLogicalTexts(), t => t == "Alle Eingaben geleert.");
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Löschen überall: Brief, Akte und Briefkopf in den Papierkorb, zurückholen, Papierkorb leeren geht an den Windows-Papierkorb")]
    public void Delete_everywhere()
    {
        var (w, _, _) = Ui.Open();
        var recycled = new List<string>(); MainWindow.LetterRecycleOverride = recycled.Add;
        try
        {
            // Brief speichern und unter „Zuletzt geschrieben“ löschen
            w.Show("brief"); Pump();
            w.Letter.SenderName = "Erika Muster"; w.Letter.SenderStreet = "Hauptstraße 1"; w.Letter.SenderCity = "10115 Berlin";
            w.Letter.Recipient = "Versand GmbH\nPostfach 1\n20095 Hamburg"; w.Letter.Subject = "Kündigung"; w.Letter.Body = "Hiermit kündige ich."; w.Letter.Place = "Berlin";
            w.Show("brief"); Pump();
            w.Find<Button>("letter-save").Press();
            for (var i = 0; i < 40 && w.TryFind<Button>("letter-del-0") is null; i++) { Pump(); w.Show("brief"); Pump(); }
            w.Find<Button>("letter-del-0").Press(); Pump();
            Assert.Null(w.TryFind<Button>("letter-del-0"));
            // Akte anlegen, löschen, zurückholen
            w.Show("akten"); Pump();
            w.Find<TextBox>("case-title").Text = "Widerspruch Bescheid"; w.Find<Button>("case-create").Press(); Pump();
            w.Show("akten"); Pump();
            w.Find<Button>("case-del-0").Press(); Pump();
            Assert.Null(w.TryFind<Button>("case-open-0"));
            Assert.NotNull(w.TryFind<StackPanel>("letter-trash"));
            w.Find<Button>("trash-restore-0").Press(); Pump(); // neuester Eintrag = die Akte
            Assert.NotNull(w.TryFind<Button>("case-open-0"));
            // Briefkopf anlegen und löschen
            w.Show("briefkoepfe"); Pump();
            w.Find<TextBox>("head-name").Text = "Erika Muster"; w.Find<TextBox>("head-city").Text = "10115 Berlin"; w.Find<Button>("head-save").Press(); Pump();
            var del = w.GetLogicalDescendantsOf<Button>().First(b => b.Name?.StartsWith("head-del-") == true);
            del.Press(); Pump();
            Assert.DoesNotContain(w.GetLogicalDescendantsOf<Button>(), b => b.Name?.StartsWith("head-del-") == true);
            // Papierkorb leeren: alles an den Windows-Papierkorb (hier: Test-Ersatz)
            w.Show("akten"); Pump();
            w.Find<Button>("trash-empty").Press(); Pump();
            Assert.Equal(2, recycled.Count); // Brief + Briefkopf
        }
        finally { MainWindow.LetterRecycleOverride = null; w.Close(); }
    }
}
