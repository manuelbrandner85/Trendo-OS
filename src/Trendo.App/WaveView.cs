using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Trendo.App;

/// <summary>Wellenform mit Zeitlineal, Auswahl (Ziehen) und Abspielmarke – Arbeitsfläche des Audio-Editors.</summary>
public sealed class WaveView : Control
{
    public (float Min, float Max)[] Peaks { get; set; } = [];
    public double Seconds { get; set; }
    public double SelA { get; set; }
    public double SelB { get; set; }
    public double Playhead { get; set; } = -1;
    /// <summary>Neue Auswahl (Sekunden). Ein einfacher Klick setzt nur die Marke (a == b).</summary>
    public Action<double, double>? Selected { get; set; }

    double? _down; double _now;
    const double Ruler = 22;

    public WaveView() { ClipToBounds = true; Cursor = new Cursor(StandardCursorType.Ibeam); MinHeight = 200; }

    public double ToTime(double x) => Bounds.Width <= 0 || Seconds <= 0 ? 0 : Math.Clamp(x / Bounds.Width, 0, 1) * Seconds;
    public double ToX(double t) => Seconds <= 0 ? 0 : t / Seconds * Bounds.Width;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e); _down = _now = ToTime(e.GetPosition(this).X); e.Pointer.Capture(this); e.Handled = true; InvalidateVisual();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e); if (_down is null) return; _now = ToTime(e.GetPosition(this).X); InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e); if (_down is not { } a) return; _down = null; e.Pointer.Capture(null);
        var b = _now; if (Math.Abs(b - a) * Bounds.Width / Math.Max(Seconds, 1e-6) < 3) b = a; // kleine Bewegung = Klick
        Selected?.Invoke(Math.Min(a, b), Math.Max(a, b));
    }

    public override void Render(DrawingContext g)
    {
        var w = Bounds.Width; var h = Bounds.Height;
        g.FillRectangle(new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Color.Parse("#151A24"), 0), new GradientStop(Color.Parse("#0B0E14"), 1) } }, new Rect(0, 0, w, h));
        // Zeitlineal
        g.FillRectangle(new SolidColorBrush(Color.Parse("#1E2533")), new Rect(0, 0, w, Ruler));
        if (Seconds > 0)
        {
            var step = new[] { 0.1, 0.25, 0.5, 1, 2, 5, 10, 15, 30, 60, 120, 300 }.FirstOrDefault(s => w / (Seconds / s) >= 70, 600);
            var tick = new Pen(new SolidColorBrush(Color.Parse("#55FFFFFF")), 1);
            for (var t = 0.0; t <= Seconds + 1e-9; t += step)
            {
                var x = ToX(t); g.DrawLine(tick, new Point(x, Ruler - 6), new Point(x, Ruler));
                var ft = new FormattedText(Trendo.Core.Media.AudioEdit.Time(t), System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, 10, new SolidColorBrush(Color.Parse("#AAB4C3")));
                g.DrawText(ft, new Point(x + 3, 3));
            }
        }
        var top = Ruler + 6; var mid = top + (h - top - 6) / 2; var amp = (h - top - 6) / 2;
        g.DrawLine(new Pen(new SolidColorBrush(Color.Parse("#30FFFFFF")), 1), new Point(0, mid), new Point(w, mid));
        // Auswahl
        double a = SelA, b = SelB;
        if (_down is { } d) { a = Math.Min(d, _now); b = Math.Max(d, _now); }
        if (b > a) g.FillRectangle(new SolidColorBrush(Color.Parse("#40F5B83D")), new Rect(ToX(a), Ruler, Math.Max(1, ToX(b) - ToX(a)), h - Ruler));
        // Wellenform in Gold (Leitfarbe), innen heller
        if (Peaks.Length > 0)
        {
            var fill = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Color.Parse("#B8860B"), 0), new GradientStop(Color.Parse("#FFE08A"), 0.5), new GradientStop(Color.Parse("#B8860B"), 1) } };
            var pen = new Pen(fill, Math.Max(1, w / Peaks.Length));
            for (var i = 0; i < Peaks.Length; i++)
            {
                var x = (i + 0.5) * w / Peaks.Length; var (lo, hi) = Peaks[i];
                g.DrawLine(pen, new Point(x, mid - Math.Max(0.5, hi * amp)), new Point(x, mid - Math.Min(-0.5, lo * amp)));
            }
        }
        else
        {
            var ft = new FormattedText("Noch kein Ton – rechts eine Datei öffnen", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, 14, new SolidColorBrush(Color.Parse("#AAB4C3")));
            g.DrawText(ft, new Point((w - ft.Width) / 2, mid - 10));
        }
        // Marke / Abspielmarke
        var gold = new Pen(new SolidColorBrush(Color.Parse("#F5B83D")), 1.5);
        if (b <= a && a > 0) g.DrawLine(gold, new Point(ToX(a), Ruler), new Point(ToX(a), h));
        if (Playhead >= 0) g.DrawLine(new Pen(Brushes.White, 1.5), new Point(ToX(Playhead), Ruler), new Point(ToX(Playhead), h));
    }
}
