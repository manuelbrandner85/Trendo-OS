using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Trendo.App;

/// <summary>D1/P3 (Vorgabe 13.11.2026 „nicht KI-generiert“): nachträglicher Ruhe-Durchgang über die sichtbare Oberfläche.
/// Farbverläufe auf Flächen werden zu einer ruhigen Einzelfarbe (Mittel der Verlaufsfarben, leicht entsättigt), Leucht-Effekte entfallen,
/// Kartenschatten verschwinden. Unangetastet bleiben: Bilder und Verläufe, die über einem Bild liegen (Lesbarkeit), Logo und Hintergrund,
/// eigene Zeichenflächen (3D, Wellenform, Platine, Karte – die zeichnen selbst) sowie Fensterrahmen (dürfen einen weichen Schatten haben).</summary>
public static class CalmLook
{
    static readonly string[] KeepNames = ["wall", "backdrop", "trendo-logo", "splash"];

    static bool Kept(Visual v)
    {
        for (var x = v; x is not null; x = x.GetVisualParent())
            if (x is Control c && c.Name is { Length: > 0 } n && KeepNames.Any(k => n.StartsWith(k, StringComparison.Ordinal))) return true;
        return false;
    }

    static bool OverImage(Control c) =>
        c.GetVisualParent() is Panel p && p.Children.Any(s => !ReferenceEquals(s, c) && (s is Image || s.GetVisualDescendants().OfType<Image>().Any()));

    public static IBrush Flatten(GradientBrush g)
    {
        var stops = g.GradientStops;
        if (stops.Count == 0) return Brushes.Transparent;
        double a = 0, r = 0, gr = 0, b = 0;
        foreach (var s in stops) { a += s.Color.A; r += s.Color.R; gr += s.Color.G; b += s.Color.B; }
        var n = stops.Count;
        var c = Color.FromArgb((byte)(a / n), (byte)(r / n), (byte)(gr / n), (byte)(b / n));
        var grey = c.R * 0.3 + c.G * 0.59 + c.B * 0.11;
        byte M(byte v) => (byte)Math.Clamp(grey + (v - grey) * 0.6, 0, 255);
        return new SolidColorBrush(Color.FromArgb(c.A, M(c.R), M(c.G), M(c.B)));
    }

    /// <summary>Einmal über einen Teilbaum gehen. Kostet wenige Millisekunden je Seite.</summary>
    public static void Apply(Visual root)
    {
        foreach (var v in root.GetSelfAndVisualDescendants().ToList())
        {
            if (v is not Control c || Kept(c)) continue;
            if (c.Effect is DropShadowEffect) c.Effect = null; // kein Leuchten
            switch (c)
            {
                case Border bd:
                    if (bd.Background is GradientBrush bg && !OverImage(bd)) bd.Background = Flatten(bg);
                    if (bd.BorderBrush is GradientBrush bb) bd.BorderBrush = Flatten(bb);
                    if (bd.BoxShadow.Count > 0 && bd.GetType().Name != "DeskFrame") bd.BoxShadow = default; // Fensterrahmen behalten ihren weichen Schatten
                    break;
                case Panel p when p.Background is GradientBrush pg && !OverImage(p):
                    p.Background = Flatten(pg); break;
            }
            if (c is Avalonia.Controls.Primitives.TemplatedControl tc)
            {
                if (tc.Background is GradientBrush tb) tc.Background = Flatten(tb);
                if (tc.BorderBrush is GradientBrush tbb) tc.BorderBrush = Flatten(tbb);
                if (tc.Foreground is GradientBrush tf) tc.Foreground = Flatten(tf);
            }
            if (c is TextBlock tx && tx.Foreground is GradientBrush txf) tx.Foreground = Flatten(txf);
        }
    }
}
