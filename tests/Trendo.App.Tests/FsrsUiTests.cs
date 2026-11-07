using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Alle Studios, Runde 4 – Karteikarten: Planer im Lernplan auf FSRS umschalten, Bewertungsknöpfe zeigen die FSRS-Abstände.</summary>
public class FsrsUi
{
    static string Days(MainWindow w, string key) => ((StackPanel)w.Find<Button>("learn-grade-" + key).Content!).Children.OfType<TextBlock>().Last().Text ?? "";

    [AvaloniaFact(DisplayName = "FSRS-Oberfläche: Planer umschalten → Meldung, Behaltensrate wählbar, „Leicht“ zeigt 16 Tage statt 4, zurück auf SM-2")]
    public void Switch_to_fsrs()
    {
        var (w, _, _) = Ui.Open();
        w.Learn.Add("Englisch", "Baum", "tree");
        w.LearnDeck = "Englisch"; w.LearnMode = "lernen"; w.Show("lernen"); w.LearnReveal();
        Assert.Equal("in 4 T.", Days(w, "4")); // SM-2: neue Karte „Leicht“ = 4 Tage

        w.LearnMode = "plan"; w.Show("lernen");
        Assert.False(w.Find<ComboBox>("learn-retention").IsEnabled);
        w.Find<ComboBox>("learn-scheduler").SelectedIndex = 1;
        Assert.True(w.Learn.UsesFsrs);
        Assert.Contains("FSRS aktiv", w.Find<TextBlock>("learn-plan-msg").Text);
        Assert.True(w.Find<ComboBox>("learn-retention").IsEnabled);
        w.Find<ComboBox>("learn-retention").SelectedIndex = 1; // 85 %
        Assert.Equal(0.85, w.Learn.Store.Retention, 3);

        w.LearnMode = "lernen"; w.Show("lernen"); w.LearnReveal();
        Assert.Equal($"in {w.Learn.PreviewDays(w.Learn.Store.Cards[0].Id, Grade.Leicht)} T.", Days(w, "4"));
        Assert.NotEqual("in 4 T.", Days(w, "4"));

        w.LearnMode = "plan"; w.Show("lernen");
        w.Find<ComboBox>("learn-scheduler").SelectedIndex = 0;
        Assert.False(w.Learn.UsesFsrs); Assert.Contains("SM-2 aktiv", w.Find<TextBlock>("learn-plan-msg").Text);
    }
}
