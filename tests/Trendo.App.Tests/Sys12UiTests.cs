using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Trendo.App;
using Trendo.Core.Sys;
using Xunit;
namespace Trendo.App.Tests;

public class Sys12_Bedienung_Ui
{
    [AvaloniaFact(DisplayName = "SYS-12 (UI): Mindestschrift greift; Knöpfe haben Namen und sind per Tab erreichbar; Bewegung aus = keine Übergänge")]
    public void Apply()
    {
        var (w, l, root) = Ui.Open();
        w.Show("bedien-sehen"); w.Find<Button>("font-14").Press();
        w.Show("prog-pruefen");
        var all = w.GetLogicalDescendants().OfType<Control>().ToList();
        var help = all.OfType<TextBlock>().Where(t => t.Text?.StartsWith("Prüft Arbeitsspeicher") == true).ToList(); Assert.NotEmpty(help); Assert.All(help, t => Assert.True(t.FontSize >= 14));
        var btns = all.OfType<Button>().Where(b => b.Name?.StartsWith("int-") == true || b.Name == "req-check").ToList(); Assert.NotEmpty(btns);
        Assert.All(btns, b => { Assert.True(b.IsTabStop && b.Focusable); Assert.False(string.IsNullOrEmpty(AutomationProperties.GetName(b))); });
        w.Settings.ReduceMotion = true; w.Show("start");
        Assert.DoesNotContain(w.Find<Control>("desktop-area").GetLogicalDescendants().OfType<Control>(), c => c.Transitions is { Count: > 0 });
        w.Close();
    }

    [AvaloniaFact(DisplayName = "SYS-12 (UI): Bericht über alle Seiten; keine Seite tiefer als 2 Ebenen; Prüfseite hat keine unbenannten Knöpfe")]
    public void Report()
    {
        var (w, l, root) = Ui.Open();
        var c = w.CheckPage("bedien-hilfen"); Assert.Empty(c.Unnamed); Assert.Empty(c.EnglishLabels); Assert.Equal(2, c.Depth);
        Assert.Equal(3, w.NavDepth("gibt-es-nicht"));
        w.Show("bedien-pruefen"); w.Find<Button>("a11y-report").Press();
        var f = Directory.GetFiles(Path.Combine(root, "berichte"), "barrierefreiheit-*.md").Single();
        Assert.Contains("| start |", File.ReadAllText(f)); Assert.DoesNotMatch(@"\| 3 \|\n", File.ReadAllText(f));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "SYS-12 (UI): Leise Benachrichtigung; Einstellung bleibt gespeichert; einfacher Modus zeigt weniger Kacheln")]
    public void Settings()
    {
        var (w, l, root) = Ui.Open();
        w.Show("bedien-hilfen"); w.Find<Button>("notify-leise").Press();
        Assert.Equal("leise", Trendo.Core.UserSettings.Load(root).NotifyStyle);
        w.Settings.SimpleMode = true; w.Show("start");
        Assert.Null(w.TryFind<Control>("tile-CORE-05")); 
        w.Close();
    }
}
