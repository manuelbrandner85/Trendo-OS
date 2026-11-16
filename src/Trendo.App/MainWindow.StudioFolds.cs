using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Trendo.App;

/// <summary>Zusammengelegte Studios: Das aufnehmende Studio und die Einstellungen führen die eingegliederten Bereiche als „Verwandte Programme“.
/// So bleibt jede Funktion ohne Suche erreichbar.
/// V-13 (T-0684, freigegeben 16.11.2026): Die Zeile „Gehört hierher:“ über jeder Seite ist weg – die Einträge stehen im Menü „Mehr“ des Seitenkopfs.
/// Seiten ohne Seitenkopf (Programme, die das ganze Fenster füllen) bekommen einen einzelnen Knopf „Verwandte Programme“.</summary>
public partial class MainWindow
{
    /// <summary>Hat der Seitenkopf der gerade gebauten Seite die verwandten Programme schon aufgenommen?</summary>
    bool _relatedPlaced;

    /// <summary>Knöpfe zu den Bereichen, die in das Studio dieser Seite eingegliedert sind (ohne die Seite selbst).</summary>
    private List<Button> RelatedPrograms(string key)
    {
        var list = new List<Button>();
        var host = key == StudioCatalog.SettingsHost ? StudioCatalog.SettingsHost : StudioKeyFor(key);
        if (host is null) return list;
        foreach (var id in StudioCatalog.FoldedFor(host).OrderBy(i => StudioCatalog.For(i)?.Name, StringComparer.CurrentCulture))
        {
            if (PageForStudio(id) is not { } page || page == key) continue;
            var b = new Button { Name = "fold-open-" + id, Content = StudioCatalog.For(id)?.Name ?? id, Padding = new Thickness(10, 5), FontSize = 12.5, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
            b.Click += (_, _) => Show(page);
            list.Add(b);
        }
        return list;
    }

    /// <summary>Abschnitt „Verwandte Programme“ für ein Klappmenü; ein Klick auf einen Eintrag schließt das Menü.</summary>
    private Control RelatedList(IReadOnlyList<Button> related, Button owner)
    {
        var sp = new StackPanel { Name = "fold-list", Spacing = 1, MinWidth = 220, Children = { new TextBlock { Name = "fold-title", Text = "Verwandte Programme", Foreground = Muted, FontSize = 11.5, FontWeight = FontWeight.SemiBold, Margin = new Thickness(10, 2, 0, 3) } } };
        foreach (var b in related) { b.Click += (_, _) => owner.Flyout?.Hide(); sp.Children.Add(b); }
        return sp;
    }

    /// <summary>Seiten ohne Seitenkopf: ein Knopf „Verwandte Programme“ rechts über der Seite, wenn in dieses Studio etwas eingegliedert ist.</summary>
    private Control WithFoldedStrip(string key, Control content)
    {
        if (_relatedPlaced) return content;
        var related = RelatedPrograms(key);
        if (related.Count == 0) return content;
        var open = new Button { Name = "fold-menu", Content = "Verwandte Programme ▾", FontSize = 12, Padding = new Thickness(9, 2), CornerRadius = new CornerRadius(10), Background = Brush.Parse("#22FFFFFF"), Foreground = Text, BorderThickness = new Thickness(0), HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(10, 3, 12, 0) };
        open.Flyout = new Flyout { Content = RelatedList(related, open) };
        return new DockPanel { Name = "fold-strip", Children = { Dk(open, Avalonia.Controls.Dock.Top), content } };
    }
}
