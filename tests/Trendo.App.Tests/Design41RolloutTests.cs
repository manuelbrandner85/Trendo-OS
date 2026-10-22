using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Trendo.App;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Design-41-Übertragung: Studiofilter, Suche und Einstellungsabschnitte schalten wirklich um.</summary>
public class Design41RolloutTests
{
    static void Press(Button b) { b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Dispatcher.UIThread.RunJobs(); }
    static void Type(TextBox box, string text) { box.Text = text; Dispatcher.UIThread.RunJobs(); }

    [AvaloniaFact(DisplayName = "Alle Studios: Bereich und Suche filtern die Karten, Alle zeigt wieder alles")]
    public void Studio_filter_and_search()
    {
        var (w, _, _) = Ui.Open();
        w.Show("studios");
        var all = w.GetLogicalDescendants().OfType<Button>().Count(b => b.Name?.StartsWith("studio-card-") == true);
        Assert.True(all >= 54, $"nur {all} Karten");
        Press(w.Find<Button>("studio-filter-business")!);
        var biz = w.GetLogicalDescendants().OfType<Button>().Where(b => b.Name?.StartsWith("studio-card-") == true).Select(b => b.Name).ToList();
        Assert.Contains("studio-card-STUDIO-ONLINE-BUSINESS-01", biz);
        Assert.DoesNotContain("studio-card-CORE-05", biz);
        Assert.True(biz.Count < all);
        Type(w.Find<TextBox>("txt-studio-search")!, "Rechnung");
        var narrowed = w.GetLogicalDescendants().OfType<Button>().Where(b => b.Name?.StartsWith("studio-card-") == true).Select(b => b.Name).ToList();
        Assert.Contains("studio-card-SPEC-11", narrowed);
        Assert.DoesNotContain("studio-card-STUDIO-ONLINE-BUSINESS-01", narrowed);
        Press(w.Find<Button>("studio-filter-alle")!);
        Type(w.Find<TextBox>("txt-studio-search")!, "Brief");
        var found = w.GetLogicalDescendants().OfType<Button>().Where(b => b.Name?.StartsWith("studio-card-") == true).Select(b => b.Name).ToList();
        Assert.Contains("studio-card-STUDIO-LETTER-01", found);
        Assert.DoesNotContain("studio-card-CORE-06", found);
        Press(w.Find<Button>("studio-filter-alle")!);
        Assert.True(w.GetLogicalDescendants().OfType<Button>().Count(b => b.Name?.StartsWith("studio-card-") == true) >= 54);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Einstellungen: linker Abschnitt schaltet um, Aussehen bleibt zuerst klickbar")]
    public void Settings_sections()
    {
        var (w, _, _) = Ui.Open();
        w.Show("settings");
        Assert.True(w.Find<Button>("theme-dunkel")!.IsVisible);
        Assert.False(w.Find<StackPanel>("settings-section-bedienung")!.IsVisible);
        Press(w.Find<Button>("settings-nav-bedienung")!);
        Assert.True(w.Find<StackPanel>("settings-section-bedienung")!.IsVisible);
        Assert.True(w.Find<Button>("size-100")!.IsVisible);
        Assert.False(w.Find<StackPanel>("settings-section-aussehen")!.IsVisible);
        Press(w.Find<Button>("settings-nav-hintergrund")!);
        Assert.True(w.Find<Button>("wall-bergsee-nacht")!.IsVisible);
        Press(w.Find<Button>("settings-nav-aussehen")!);
        Assert.True(w.Find<Button>("theme-dunkel")!.IsVisible);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "KI: die Seite lässt sich erneut öffnen, Status bleibt „Nicht verbunden“")]
    public void Ki_reopen_keeps_status()
    {
        var (w, _, _) = Ui.Open();
        w.Show("ki"); w.Show("settings"); w.Show("ki");
        Assert.Equal("Nicht verbunden", w.Find<TextBlock>("ki-status")!.Text);
        Assert.False(w.Find<TextBox>("ki-input")!.IsEnabled);
        w.Close();
    }
}
