using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Vorgabe 2026-10-27: Auf dem Briefblatt steht nur, was der Nutzer angibt – kein Trendo-Logo, kein „Trendo OS“.</summary>
public class LetterNoBrandingUiTests
{
    [AvaloniaFact(DisplayName = "Briefvorschau: kein Trendo-Logo und kein „Trendo OS“ auf dem Blatt")]
    public void Letter_sheet_has_no_trendo_branding()
    {
        var (w, _, _) = Ui.Open();
        w.Show("brief");
        var sheet = w.GetLogicalDescendants().OfType<Border>().First(b => b.Name == "letter-preview");
        Assert.DoesNotContain(sheet.GetLogicalDescendants(), c => c is Viewbox { Name: "trendo-logo" });
        Assert.DoesNotContain(sheet.GetLogicalDescendants().OfType<TextBlock>(), t => (t.Text ?? "").Contains("Trendo", StringComparison.OrdinalIgnoreCase));
        w.Close();
    }
}
