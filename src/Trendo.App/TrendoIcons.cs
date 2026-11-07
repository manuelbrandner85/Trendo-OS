using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;

namespace Trendo.App;

/// <summary>Modernisierung G7 (freigegeben 07.11.2026): eigener Linien-Symbolsatz statt Emojis – selbst gezeichnet auf einem 24er-Raster
/// (keine fremden Grafiken), 1,8 px Strich, runde Enden. Gleich aussehend auf jedem System (Emojis sehen je Schrift anders aus).</summary>
public static class TrendoIcons
{
    /// <summary>Pfade im 24 × 24-Raster (SVG-Pfadsyntax, nur Striche).</summary>
    public static readonly IReadOnlyDictionary<string, string> Paths = new Dictionary<string, string>
    {
        ["start"] = "M4 11 L12 4 L20 11 M6 9.5 V20 H18 V9.5 M10 20 V14 H14 V20",
        ["erstellen"] = "M12 3 L13.8 9.2 L20 11 L13.8 12.8 L12 19 L10.2 12.8 L4 11 L10.2 9.2 Z M19 3 V7 M17 5 H21",
        ["regler"] = "M6 4 V20 M12 4 V20 M18 4 V20 M4 14 H8 M10 8 H14 M16 16 H20",
        ["bibliothek"] = "M4 4 V20 M8 4 V20 M12 5 L16 20 M17 4 H20 V20 H17 Z",
        ["mikrofon"] = "M9 4 H15 V13 A3 3 0 0 1 9 13 Z M6 11 A6 6 0 0 0 18 11 M12 17 V21 M8 21 H16",
        ["musik"] = "M9 18 V5 L20 3 V16 M9 18 A3 3 0 1 1 3 18 A3 3 0 1 1 9 18 M20 16 A3 3 0 1 1 14 16 A3 3 0 1 1 20 16",
        ["plus"] = "M12 5 V19 M5 12 H19",
        ["suche"] = "M10.5 4 A6.5 6.5 0 1 1 10.5 17 A6.5 6.5 0 1 1 10.5 4 M15.5 15.5 L20 20",
        ["abspielen"] = "M7 4 L19 12 L7 20 Z",
        ["pause"] = "M8 5 V19 M16 5 V19",
        ["stern"] = "M12 3 L14.6 8.8 L21 9.4 L16.2 13.6 L17.6 20 L12 16.7 L6.4 20 L7.8 13.6 L3 9.4 L9.4 8.8 Z",
        ["ordner"] = "M3 6 H9 L11 8 H21 V19 H3 Z",
        ["zahnrad"] = "M12 9 A3 3 0 1 1 12 15 A3 3 0 1 1 12 9 M12 2 V5 M12 19 V22 M2 12 H5 M19 12 H22 M4.9 4.9 L7 7 M17 17 L19.1 19.1 M4.9 19.1 L7 17 M17 7 L19.1 4.9",
        ["bild"] = "M3 5 H21 V19 H3 Z M3 16 L9 10 L14 15 L17 12 L21 16 M15.5 7.5 A1.5 1.5 0 1 1 15.5 10.5 A1.5 1.5 0 1 1 15.5 7.5",
        ["video"] = "M3 6 H15 V18 H3 Z M15 10 L21 7 V17 L15 14",
        ["papierkorb"] = "M4 7 H20 M9 7 V4 H15 V7 M6 7 L7 20 H17 L18 7 M10 11 V16 M14 11 V16",
        ["herunterladen"] = "M12 4 V15 M7 10 L12 15 L17 10 M5 20 H19",
        ["teilen"] = "M18 5 A2.5 2.5 0 1 1 18 10 A2.5 2.5 0 1 1 18 5 M6 9.5 A2.5 2.5 0 1 1 6 14.5 A2.5 2.5 0 1 1 6 9.5 M18 14 A2.5 2.5 0 1 1 18 19 A2.5 2.5 0 1 1 18 14 M8.2 10.8 L15.8 8.2 M8.2 13.2 L15.8 15.8",
        ["person"] = "M12 4 A4 4 0 1 1 12 12 A4 4 0 1 1 12 4 M4 21 A8 8 0 0 1 20 21",
        ["kalender"] = "M4 6 H20 V20 H4 Z M4 10 H20 M8 3 V7 M16 3 V7",
        ["diagramm"] = "M4 20 H20 M7 20 V12 M12 20 V6 M17 20 V14",
        ["haken"] = "M5 12.5 L10 17.5 L19 7",
        ["warnung"] = "M12 3 L22 20 H2 Z M12 9 V14 M12 17 V17.5",
        ["info"] = "M12 3 A9 9 0 1 1 12 21 A9 9 0 1 1 12 3 M12 11 V16.5 M12 7.5 V8",
        ["schliessen"] = "M6 6 L18 18 M18 6 L6 18",
        ["mehr"] = "M5 12 H5.2 M12 12 H12.2 M19 12 H19.2",
        ["neu-laden"] = "M20 12 A8 8 0 1 1 17.7 6.3 M20 4 V8 H16",
        ["bearbeiten"] = "M4 20 L4.8 16 L16 4.8 L19.2 8 L8 19.2 Z M14 6.8 L17.2 10",
        ["schloss"] = "M6 11 H18 V20 H6 Z M8.5 11 V8 A3.5 3.5 0 0 1 15.5 8 V11",
        ["ki"] = "M12 3 L13.4 8.6 L19 10 L13.4 11.4 L12 17 L10.6 11.4 L5 10 L10.6 8.6 Z M18.5 15 L19.2 17.3 L21.5 18 L19.2 18.7 L18.5 21 L17.8 18.7 L15.5 18 L17.8 17.3 Z",
        ["welle"] = "M3 12 H5 M7 8 V16 M11 5 V19 M15 9 V15 M19 7 V17 M21 12 H21",
    };

    /// <summary>Symbol als Vektor-Pfad (skaliert ohne Unschärfe).</summary>
    public static Control Icon(string name, double size = 20, IBrush? brush = null)
    {
        if (!Paths.TryGetValue(name, out var d)) throw new ArgumentException("Unbekanntes Symbol: " + name);
        var path = new Avalonia.Controls.Shapes.Path { Data = Geometry.Parse(d), Stroke = brush ?? Brushes.White, StrokeThickness = 1.8, StrokeLineCap = PenLineCap.Round, StrokeJoin = PenLineJoin.Round, Stretch = Stretch.None };
        return new Viewbox { Name = "icon-" + name, Width = size, Height = size, Child = new Canvas { Width = 24, Height = 24, Children = { path } } };
    }
}
