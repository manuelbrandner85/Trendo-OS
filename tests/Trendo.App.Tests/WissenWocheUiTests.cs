using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>Vorschlag 32: Wochenrückblick im Wissen-Studio.</summary>
public class WissenWoche_Ui
{
    [AvaloniaFact(DisplayName = "Wochenrückblick UI: Knopf öffnet den Rückblick mit Kacheln und Lerntagen, KI-Zusammenfassung (ersetzt) erscheint, „Als Seite speichern“ legt die Seite an")]
    public async Task Week()
    {
        var (w, _, _) = Ui.Open(); w.Width = 1600; w.Height = 1000;
        try
        {
            w.WeekTodayOverride = () => DateTime.Now;
            w.Wissen.Add("Photosynthese", "Pflanzen wandeln Licht um. Siehe [[Chlorophyll]].");
            w.Learn.Store.ReviewLog[DateTime.Now.ToString("yyyy-MM-dd")] = 7;
            w.Show("ki-wissen"); Settle();
            RealClick(w, V<Button>(w, "wissen-week")); Settle();
            Assert.NotNull(w.TryFind<StackPanel>("week-panel"));
            Assert.Equal(7, w.WeekReviewNow().Reviews);
            string? asked = null; w.WeekSummaryOverride = p => { asked = p; return Task.FromResult<string?>("Diese Woche ging es um Pflanzen."); };
            await w.WeekSummarize(); Settle();
            Assert.Contains("Photosynthese", asked); Assert.NotNull(w.TryFind<Border>("week-summary"));
            V<Button>(w, "week-save").BringIntoView(); Settle(); RealClick(w, V<Button>(w, "week-save")); Settle();
            var page = w.Wissen.All().First(i => i.Title.StartsWith("Wochenrückblick KW"));
            Assert.Contains("Diese Woche ging es um Pflanzen.", page.Text); Assert.Contains("[[Photosynthese]]", page.Text);
            w.WeekSave(); // gleiche Woche: aktualisieren statt doppelt
            Assert.Single(w.Wissen.All(), i => i.Title.StartsWith("Wochenrückblick KW"));
        }
        finally { w.Close(); }
    }
}
