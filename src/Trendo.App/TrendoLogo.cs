using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Trendo.App;

/// <summary>Trendo-Logo C „Gold-Orbit“ (gewählt am 27.10.2026 vom Projektinhaber: „Dieses T soll im gesamten Trendo OS das neue Logo sein“, „viel realistischer,
/// fotorealistisch“). Ersetzt Logo B „Saphir-Kristall“. Fotorealistisches goldenes T mit blauen Innenkanten und Orbit-Ring, ausgeschnitten aus der Vorlage
/// docs/design/logo-vorschlaege/gold-orbit-vorlage.png (<see cref="SourceCrop"/>), weich in eine dunkle Navy-Kachel eingebettet.
/// Einzige Quelle für Programm-Logo, PNGs und App-Symbol (tools/Trendo.Render mit TRENDO_LOGO).</summary>
public static class TrendoLogo
{
    public const string TileTop = "#0E1B3A", TileBottom = "#050B18", TileStroke = "#6E5422", Glow = "#F5C451", BlueGlow = "#3B82F6";

    /// <summary>Ausschnitt des Emblems (T und innerer Teil des Rings) in der Vorlage (1672 × 941 px).</summary>
    public static readonly PixelRect SourceCrop = new(616, 44, 468, 276);
    public const string EmblemAsset = "avares://TrendoOS/Assets/logo/trendo-emblem.png";

    static Bitmap? _emblem;
    /// <summary>Das ausgeschnittene Emblem (oder null, falls die Datei fehlt – dann zeichnet <see cref="Build"/> nur die Kachel mit Leuchten).</summary>
    public static Bitmap? Emblem()
    {
        if (_emblem is not null) return _emblem;
        try { using var s = AssetLoader.Open(new Uri(EmblemAsset)); return _emblem = new Bitmap(s); }
        catch (Exception e) when (e is FileNotFoundException or IOException or InvalidOperationException or ArgumentException) { return null; }
    }

    /// <summary>Weicher Rand: das Emblem läuft zur Kachel hin elliptisch aus, damit der Himmel der Vorlage nahtlos in die Kachel übergeht.</summary>
    static IBrush Feather() => new RadialGradientBrush
    {
        GradientStops = { new GradientStop(Colors.White, 0), new GradientStop(Colors.White, 0.74), new GradientStop(Color.FromArgb(0, 255, 255, 255), 1) },
    };

    /// <summary>Emblem aus der Vorlage ausschneiden (für das Render-Werkzeug).</summary>
    public static Control EmblemFrom(Bitmap source) => new Image { Source = new CroppedBitmap(source, SourceCrop), Width = SourceCrop.Width, Height = SourceCrop.Height, Stretch = Stretch.Fill };

    /// <summary>Das Logo (64×64): Kachel, Gold-Blau-Leuchten, fotorealistisches Emblem mit weichem Rand, feine Goldkante.</summary>
    public static Canvas Build(bool tile = true, Bitmap? emblem = null)
    {
        var canvas = new Canvas { Width = 64, Height = 64 };
        var rel = RelativeUnit.Relative;
        if (tile)
        {
            var bg = new Border
            {
                Width = 62, Height = 62, CornerRadius = new CornerRadius(15), BorderThickness = new Thickness(0.75), BorderBrush = Brush.Parse(TileStroke),
                Background = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, rel), EndPoint = new RelativePoint(0, 1, rel), GradientStops = { new GradientStop(Color.Parse(TileTop), 0), new GradientStop(Color.Parse(TileBottom), 1) } },
            };
            Canvas.SetLeft(bg, 1); Canvas.SetTop(bg, 1); canvas.Children.Add(bg);
        }
        var glow = new Ellipse
        {
            Width = 58, Height = 58,
            Fill = new RadialGradientBrush { GradientStops = { new GradientStop(Color.Parse("#40F5C451"), 0), new GradientStop(Color.Parse("#263B82F6"), 0.55), new GradientStop(Color.Parse("#003B82F6"), 1) } },
        };
        Canvas.SetLeft(glow, 3); Canvas.SetTop(glow, 3); canvas.Children.Add(glow);
        if ((emblem ?? Emblem()) is { } e)
        {
            // Seitenverhältnis des Ausschnitts erhalten, so groß wie möglich in der Kachel
            double w = 62, h = w * e.PixelSize.Height / e.PixelSize.Width;
            if (h > 58) { h = 58; w = h * e.PixelSize.Width / e.PixelSize.Height; }
            var img = new Image { Source = e, Width = w, Height = h, Stretch = Stretch.Fill, OpacityMask = Feather() };
            RenderOptions.SetBitmapInterpolationMode(img, BitmapInterpolationMode.HighQuality);
            Canvas.SetLeft(img, (64 - w) / 2); Canvas.SetTop(img, (64 - h) / 2 + 1); canvas.Children.Add(img);
        }
        return canvas;
    }
}
