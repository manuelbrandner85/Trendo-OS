using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Trendo.App;

/// <summary>Zusammengelegte Studios: Das aufnehmende Studio und die Einstellungen zeigen oben eine Leiste mit den eingegliederten Bereichen. So bleibt jede Funktion ohne Suche erreichbar.</summary>
public partial class MainWindow
{
    /// <summary>Stellt der Seite die Leiste voran, wenn in dieses Studio etwas eingegliedert ist.</summary>
    private Control WithFoldedStrip(string key, Control content)
    {
        var host = key == StudioCatalog.SettingsHost ? StudioCatalog.SettingsHost : StudioKeyFor(key);
        if (host is null) return content;
        var row = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
        foreach (var id in StudioCatalog.FoldedFor(host).OrderBy(i => StudioCatalog.For(i)?.Name, StringComparer.CurrentCulture))
        {
            if (PageForStudio(id) is not { } page || page == key) continue;
            var b = new Button { Name = "fold-open-" + id, Content = StudioCatalog.For(id)?.Name ?? id, Padding = new Thickness(10, 3), Margin = new Thickness(0, 0, 6, 4), FontSize = 12 };
            b.Click += (_, _) => Show(page);
            row.Children.Add(b);
        }
        if (row.Children.Count == 0) return content;
        var strip = new DockPanel { Name = "fold-strip", Margin = new Thickness(10, 6, 10, 4) };
        strip.Children.Add(Dk(new TextBlock { Text = "Gehört hierher:", Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 4, 10, 0) }, Avalonia.Controls.Dock.Left));
        strip.Children.Add(row);
        return new DockPanel { Children = { Dk(strip, Avalonia.Controls.Dock.Top), content } };
    }
}
