using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Trendo.App;

/// <summary>Modernisierung K3 (07.11.2026): schmale Wellenform hinter dem Abspielregler – gespielter Teil in Gold, der Rest gedämpft.
/// Werte kommen aus der echten Tondatei (<see cref="Trendo.Core.Media.AudioEdit.Peaks"/>).</summary>
public sealed class WaveStrip : Control
{
    public (float Min, float Max)[] Peaks { get; set; } = [];
    /// <summary>Abgespielter Anteil 0–1.</summary>
    public double Progress { get; set; }
    public IBrush Played { get; set; } = Brushes.Goldenrod;
    public IBrush Rest { get; set; } = new SolidColorBrush(Color.Parse("#40FFFFFF"));

    public WaveStrip() { IsHitTestVisible = false; ClipToBounds = true; }

    public void SetProgress(double p) { Progress = Math.Clamp(p, 0, 1); InvalidateVisual(); }

    public override void Render(DrawingContext ctx)
    {
        var w = Bounds.Width; var h = Bounds.Height; if (Peaks.Length == 0 || w <= 0) return;
        var mid = h / 2; var bar = 2.0; var gap = 1.5; var n = (int)(w / (bar + gap)); if (n <= 0) return;
        var played = Progress * w;
        for (var i = 0; i < n; i++)
        {
            int a = i * Peaks.Length / n, b = Math.Max(a + 1, (i + 1) * Peaks.Length / n); float hi = 0;
            for (var k = a; k < b && k < Peaks.Length; k++) hi = Math.Max(hi, Math.Max(Peaks[k].Max, -Peaks[k].Min));
            var x = i * (bar + gap); var amp = Math.Max(1, Math.Min(1, hi) * (mid - 1));
            ctx.FillRectangle(x < played ? Played : Rest, new Rect(x, mid - amp, bar, amp * 2), 1);
        }
    }
}
