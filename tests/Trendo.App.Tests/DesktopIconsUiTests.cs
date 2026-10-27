using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Wunsch 27.10.2026: Desktop standardmäßig aufgeräumt, Symbole selbst hinzufügen und wieder wegnehmen.</summary>
public class DesktopIconsUiTests
{
    [AvaloniaFact(DisplayName = "Desktop: anfangs keine Symbole; über Startmenü, Seite „Desktop-Symbole“ und Studio Manager hinzufügen; Rechtsklick entfernt; bleibt gespeichert")]
    public void Clean_desktop_add_remove()
    {
        var (w, _, root) = Ui.Open();
        w.Show("start");
        Assert.NotNull(w.TryFind<Control>("desk-empty"));
        Assert.Null(w.TryFind<Button>("tile-"));
        Assert.Null(w.TryFind<Button>("module-"));

        w.OpenStartMenu();
        w.Find<Button>("sm-desk-CORE-05").Press();
        Assert.NotNull(w.TryFind<Button>("tile-CORE-05"));
        Assert.Equal("Vom Desktop", w.Find<Button>("sm-desk-CORE-05").Content);

        w.Show("desktop-symbole");
        w.Find<Button>("desk-toggle-page-ki").Press();
        Assert.Contains("page:ki", UserSettings.Load(root).DesktopIcons);
        w.Show("start");
        Assert.NotNull(w.TryFind<Button>("module-ki"));
        Assert.Null(w.TryFind<Control>("desk-empty"));
        w.Find<Button>("module-ki").Press();
        Assert.Equal("ki", w.CurrentView);

        w.Show("start");
        var tile = w.Find<Button>("tile-CORE-05");
        var rm = tile.ContextMenu!.Items.OfType<MenuItem>().Single(m => m.Name == "desk-remove-CORE-05");
        rm.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Null(w.TryFind<Button>("tile-CORE-05"));
        Assert.Equal(["page:ki"], UserSettings.Load(root).DesktopIcons);
        w.Close();
    }
}
