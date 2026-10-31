using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core.Office;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>108-49 (Entscheidung 31.10.2026): KI-Umschreiben zeigt erst einen Vorschlag – der Text ändert sich nur nach „Übernehmen“.</summary>
public class TextAiProposalTests
{
    static void Pump() { for (var i = 0; i < 5; i++) Avalonia.Threading.Dispatcher.UIThread.RunJobs(); }

    [AvaloniaFact(DisplayName = "108-49: KI-Vorschlag mit Änderungen – erst „Übernehmen“ ersetzt, „Verwerfen“ lässt alles, geänderter Text wird nicht überschrieben")]
    public async Task Proposal_first()
    {
        var (w, _, _) = Ui.Open();
        w.KiBackendsOverride = () => [new FakeKi("Wir freuen uns sehr auf Ihren Besuch.")];
        w.TextNew(); w.Show("text"); Pump();
        w.TextDocument.Blocks[0] = new Para("Wir freuen uns auf Ihren Besuch.");
        w.TextSelect(0, 0, 0); w.Show("text"); Pump();

        await w.TextAiRewrite("freundlich"); Pump();
        Assert.True(w.TextAiHasProposal);
        Assert.Equal("Wir freuen uns auf Ihren Besuch.", ((Para)w.TextDocument.Blocks[0]).Text); // noch nichts geändert
        Assert.NotNull(w.TryFind<Button>("text-ai-accept"));
        Assert.Contains(w.GetLogicalTexts(), t => t.Contains("Vorschlag der KI"));

        w.Find<Button>("text-ai-reject").Press(); Pump();
        Assert.False(w.TextAiHasProposal);
        Assert.Equal("Wir freuen uns auf Ihren Besuch.", ((Para)w.TextDocument.Blocks[0]).Text);

        await w.TextAiRewrite("freundlich"); Pump();
        w.Find<Button>("text-ai-accept").Press(); Pump();
        Assert.Equal("Wir freuen uns sehr auf Ihren Besuch.", ((Para)w.TextDocument.Blocks[0]).Text);
        w.TextUndo();
        Assert.Equal("Wir freuen uns auf Ihren Besuch.", ((Para)w.TextDocument.Blocks[0]).Text);

        // Text inzwischen selbst geändert → Vorschlag wird nicht übernommen
        await w.TextAiRewrite("freundlich"); Pump();
        ((Para)w.TextDocument.Blocks[0]).Text = "Ganz anderer Text.";
        w.TextAiDecide(true);
        Assert.Equal("Ganz anderer Text.", ((Para)w.TextDocument.Blocks[0]).Text);
        w.Close();
    }
}
