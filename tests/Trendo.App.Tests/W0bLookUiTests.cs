using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Xunit;
namespace Trendo.App.Tests;

public class W0b_Hintergrund_Farbe_Ui
{
    [AvaloniaFact(DisplayName = "W0B-02/03 (UI): Rechtsklick-Menü öffnet Einstellungen; Hintergrund wählen, Farbe passend zum Hintergrund")]
    public void Look()
    {
        var (w, _, root) = Ui.Open();
        w.Show("start");
        var menu = w.Find<Grid>("desktop-area").ContextMenu!;
        var item = menu.Items.OfType<MenuItem>().First(i => i.Name == "menu-desktop-look");
        Assert.Equal("Hintergrund und Farbe ändern", item.Header);
        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); Dispatcher.UIThread.RunJobs();
        Assert.Equal("settings", w.CurrentView);
        w.Find<Button>("wall-polarlicht").Press();
        Assert.Equal("polarlicht", UserSettings.Load(root).Wallpaper);
        w.Find<CheckBox>("accent-auto").IsChecked = true; Dispatcher.UIThread.RunJobs();
        var s = UserSettings.Load(root);
        Assert.True(s.AccentFromWallpaper);
        Assert.Equal("#22C3A6", s.EffectiveAccent(DateTime.Now));
    }
}
