using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Trendo.App;

/// <summary>P4 (13.11.2026): Dichte „Kompakt“ wie in Explorer oder Outlook. Viele Knöpfe und Felder setzen ihren Innenabstand im Code –
/// den verkleinert dieser Durchgang auf etwa zwei Drittel und merkt sich den Ursprung, damit „Normal“ ihn genau zurückholt.</summary>
public static class Density
{
    static readonly AttachedProperty<Thickness?> OriginalProperty = AvaloniaProperty.RegisterAttached<Control, Thickness?>("DensityOriginal", typeof(Density));

    public const double Factor = 0.65;

    static double R(double v) => Math.Round(v * Factor, MidpointRounding.AwayFromZero);
    public static Thickness Shrink(Thickness t) => new(R(t.Left), R(t.Top), R(t.Right), R(t.Bottom));

    public static void Apply(Visual root, bool compact)
    {
        foreach (var v in root.GetSelfAndVisualDescendants())
        {
            if (v is not (Button or TextBox or ComboBox) || v is not Avalonia.Controls.Primitives.TemplatedControl c) continue;
            var orig = c.GetValue(OriginalProperty);
            if (compact)
            {
                if (orig is not null || !c.IsSet(Avalonia.Controls.Primitives.TemplatedControl.PaddingProperty)) continue; // schon verkleinert oder Stil-Wert (den regelt App.axaml)
                c.SetValue(OriginalProperty, c.Padding);
                c.Padding = Shrink(c.Padding);
            }
            else if (orig is { } o)
            {
                c.Padding = o; c.SetValue(OriginalProperty, null);
            }
        }
    }
}
