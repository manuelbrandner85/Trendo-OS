using Avalonia.Controls;
using Avalonia.Media;

namespace Trendo.App;

/// <summary>Polish: Rechtsklick auf den freien Startbildschirm → Hintergrund und Farbe ändern (wie bei Windows).</summary>
public partial class MainWindow
{
    private void DesktopMenu(Grid g)
    {
        g.Name ??= "desktop-area"; g.Background = Brushes.Transparent;
        var item = new MenuItem { Name = "menu-desktop-look", Header = "Hintergrund und Farbe ändern" };
        item.Click += (_, _) => Show("settings");
        var items = new MenuItem { Name = "menu-desktop-tiles", Header = "Startbildschirm anpassen" };
        items.Click += (_, _) => Show("settings");
        var add = new MenuItem { Name = "menu-desktop-add", Header = "Symbol hinzufügen …" };
        add.Click += (_, _) => Show("desktop-symbole");
        g.ContextMenu = new ContextMenu { Name = "desktop-menu", Items = { add, item, items } };
    }
}
