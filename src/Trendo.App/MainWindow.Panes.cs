using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Trendo.App;

/// <summary>Modernisierung G9 (freigegeben 07.11.2026): ziehbare und einklappbare Seitenleisten. Eine Lücken-Spalte im Raster wird zum Griff:
/// Ziehen ändert die Breite der Nachbarspalte, Doppelklick klappt sie ein und wieder aus. Die Breite bleibt je Programm gespeichert („ansicht-breiten.json“).</summary>
public partial class MainWindow
{
    string PaneFile => Path.Combine(_core.DataRoot, "ansicht-breiten.json");

    /// <summary>Gespeicherte Breiten (Schlüssel „programm/leiste“ → Pixel; 0 = eingeklappt).</summary>
    public Dictionary<string, double> PaneWidths()
    {
        try { return File.Exists(PaneFile) ? JsonSerializer.Deserialize<Dictionary<string, double>>(File.ReadAllText(PaneFile)) ?? [] : []; }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { return []; }
    }

    public void SavePaneWidth(string key, double width)
    {
        var d = PaneWidths(); d[key] = Math.Round(width);
        try { Directory.CreateDirectory(_core.DataRoot); File.WriteAllText(PaneFile, JsonSerializer.Serialize(d)); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>Griff in die Lücken-Spalte <paramref name="gapCol"/> setzen; <paramref name="paneCol"/> ist die Leiste, deren Breite er ändert.
    /// Gespeicherte Breite wird sofort angewandt (zwischen <paramref name="min"/> und <paramref name="max"/>; 0 = eingeklappt).</summary>
    public GridSplitter PaneGrip(Grid grid, int gapCol, int paneCol, string key, double min = 200, double max = 640, int rowSpan = 1)
    {
        var def = grid.ColumnDefinitions[paneCol];
        var normal = def.Width.IsAbsolute ? def.Width.Value : 320;
        if (PaneWidths().TryGetValue(key, out var w)) def.Width = new GridLength(w <= 0 ? 0 : Math.Clamp(w, min, max));
        def.MinWidth = 0; def.MaxWidth = max;
        var grip = new GridSplitter
        {
            Name = "pane-grip-" + key.Replace('/', '-'), ResizeDirection = GridResizeDirection.Columns, Background = Brushes.Transparent, Cursor = new Cursor(StandardCursorType.SizeWestEast),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
        };
        ToolTip.SetTip(grip, "Ziehen ändert die Breite · Doppelklick klappt ein/aus");
        // dünne Goldlinie in der Mitte des Griffs, beim Überfahren sichtbar
        var line = new Border { Width = 2, CornerRadius = new CornerRadius(1), Background = Accent, Opacity = 0, IsHitTestVisible = false, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, Margin = new Thickness(0, 40) };
        grip.PointerEntered += (_, _) => line.Opacity = 0.7; grip.PointerExited += (_, _) => line.Opacity = 0;
        grip.DragCompleted += (_, _) => SavePaneWidth(key, grid.ColumnDefinitions[paneCol].ActualWidth);
        grip.DoubleTapped += (_, _) => TogglePane(grid, paneCol, key, normal, min);
        Grid.SetColumn(grip, gapCol); Grid.SetRowSpan(grip, rowSpan); Grid.SetColumn(line, gapCol); Grid.SetRowSpan(line, rowSpan);
        grid.Children.Add(line); grid.Children.Add(grip);
        return grip;
    }

    /// <summary>Leiste ein- oder ausklappen (gemerkt).</summary>
    public void TogglePane(Grid grid, int paneCol, string key, double normal, double min = 200)
    {
        var def = grid.ColumnDefinitions[paneCol];
        var open = def.Width.Value > 0;
        var target = open ? 0 : PaneWidths().TryGetValue(key + "#offen", out var last) && last >= min ? last : normal;
        if (open) SavePaneWidth(key + "#offen", def.Width.Value);
        def.Width = new GridLength(target);
        SavePaneWidth(key, target);
    }
}
