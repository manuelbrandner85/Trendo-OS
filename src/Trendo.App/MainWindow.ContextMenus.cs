using Avalonia.Controls;

namespace Trendo.App;

/// <summary>Modernisierung G12 (freigegeben 07.11.2026): Rechtsklick-Menü an Listenzeilen und Karten – dieselben Aktionen wie im „⋯“-Menü,
/// gruppiert mit Trennlinien. Einheitlich gebaut, damit jedes Studio es gleich anbietet.</summary>
public partial class MainWindow
{
    /// <summary>Menü aus Gruppen von Aktionen (zwischen den Gruppen eine Trennlinie). Namen: „{prefix}-{id}“.</summary>
    internal static MenuFlyout ContextMenuOf(string prefix, params IEnumerable<(string Id, string Label, Action Run)>[] groups)
    {
        var f = new MenuFlyout();
        foreach (var g in groups)
        {
            var items = g.ToList(); if (items.Count == 0) continue;
            if (f.Items.Count > 0) f.Items.Add(new Separator());
            foreach (var (id, label, run) in items) { var mi = new MenuItem { Name = $"{prefix}-{id}", Header = label }; var r = run; mi.Click += (_, _) => r(); f.Items.Add(mi); }
        }
        return f;
    }

    /// <summary>Rechtsklick-Menü an ein Element hängen.</summary>
    internal static T WithContext<T>(T c, MenuFlyout menu) where T : Control { c.ContextFlyout = menu; return c; }
}
