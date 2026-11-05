using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Trendo.App;

/// <summary>Eine Spur der Mehrspur-Ansicht (wie Audition Multitrack): Clip mit Wellenform auf der gemeinsamen Zeitachse.
/// Clip ziehen verschiebt ihn (Raster 0,05 s), Klick daneben setzt die Marke.</summary>
public sealed class ClipLane : Control
{
    public double Total { get; set; } = 10;
    public double Start { get; set; }
    public double Length { get; set; }
    public (float Min, float Max)[] Peaks { get; set; } = [];
    public string Label { get; set; } = "";
    public Color Tint { get; set; } = Color.Parse("#F5B83D");
    public bool Dimmed { get; set; }
    public double Marker { get; set; } = -1;
    public double Playhead { get; set; } = -1;
    /// <summary>Clip wurde auf eine neue Startzeit gezogen (Sekunden).</summary>
    public Action<double>? Moved { get; set; }
    /// <summary>Klick auf die Spur (Sekunden) – setzt die Marke.</summary>
    public Action<double>? Clicked { get; set; }

    double? _grab; double _dx;

    public ClipLane() { Height = 84; ClipToBounds = true; Cursor = new Cursor(StandardCursorType.Hand); }

    public double ToX(double t) => Total <= 0 ? 0 : t / Total * Bounds.Width;
    public double ToTime(double x) => Bounds.Width <= 0 ? 0 : Math.Clamp(x / Bounds.Width, 0, 1) * Total;
    Rect ClipRect(double start) => new(ToX(start), 4, Math.Max(6, ToX(start + Length) - ToX(start)), Bounds.Height - 8);

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e); var p = e.GetPosition(this);
        if (ClipRect(Start).Contains(p)) { _grab = p.X; _dx = 0; e.Pointer.Capture(this); }
        else Clicked?.Invoke(ToTime(p.X));
        e.Handled = true;
    }
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e); if (_grab is not { } g) return; _dx = e.GetPosition(this).X - g; InvalidateVisual();
    }
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e); if (_grab is null) return; _grab = null; e.Pointer.Capture(null);
        if (Math.Abs(_dx) < 3) { Clicked?.Invoke(ToTime(e.GetPosition(this).X)); _dx = 0; return; }
        var t = Math.Max(0, Math.Round((Start + _dx / Math.Max(1, Bounds.Width) * Total) * 20) / 20); _dx = 0;
        Moved?.Invoke(t);
    }

    public override void Render(DrawingContext g)
    {
        var w = Bounds.Width; var h = Bounds.Height;
        g.FillRectangle(new SolidColorBrush(Color.Parse("#0E131C")), new Rect(0, 0, w, h));
        // Raster jede Sekunde-Stufe wie im Lineal
        var step = new[] { 0.5, 1, 2, 5, 10, 15, 30, 60, 120 }.FirstOrDefault(s => w / Math.Max(1e-6, Total / s) >= 70, 300);
        var grid = new Pen(new SolidColorBrush(Color.Parse("#14FFFFFF")), 1);
        for (var t = step; t < Total; t += step) g.DrawLine(grid, new Point(ToX(t), 0), new Point(ToX(t), h));
        var start = _grab is null ? Start : Math.Max(0, Start + _dx / Math.Max(1, w) * Total);
        var r = ClipRect(start);
        var alpha = Dimmed ? (byte)70 : (byte)255;
        g.DrawRectangle(new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Color.FromArgb((byte)(alpha * 0.5), Tint.R, Tint.G, Tint.B), 0), new GradientStop(Color.FromArgb((byte)(alpha * 0.22), Tint.R, Tint.G, Tint.B), 1) } },
            new Pen(new SolidColorBrush(Color.FromArgb(alpha, Tint.R, Tint.G, Tint.B)), _grab is null ? 1 : 2), r, 6, 6);
        if (Peaks.Length > 0)
        {
            var mid = r.Y + r.Height / 2 + 6; var amp = (r.Height - 16) / 2;
            var pen = new Pen(new SolidColorBrush(Color.FromArgb(alpha, 255, 236, 190)), Math.Max(1, r.Width / Peaks.Length));
            for (var i = 0; i < Peaks.Length; i++)
            {
                var x = r.X + (i + 0.5) * r.Width / Peaks.Length; var (lo, hi) = Peaks[i];
                g.DrawLine(pen, new Point(x, mid - Math.Max(0.5, hi * amp)), new Point(x, mid - Math.Min(-0.5, lo * amp)));
            }
        }
        var ft = new FormattedText(Label, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold), 11, Brushes.White);
        using (g.PushClip(r)) g.DrawText(ft, new Point(r.X + 6, r.Y + 3));
        if (Marker >= 0) g.DrawLine(new Pen(new SolidColorBrush(Color.Parse("#F5B83D")), 1.5), new Point(ToX(Marker), 0), new Point(ToX(Marker), h));
        if (Playhead >= 0) g.DrawLine(new Pen(Brushes.White, 1.5), new Point(ToX(Playhead), 0), new Point(ToX(Playhead), h));
    }
}

/// <summary>Zeitlineal über den Spuren.</summary>
public sealed class TimeRuler : Control
{
    public double Total { get; set; } = 10;
    public TimeRuler() { Height = 22; }
    public override void Render(DrawingContext g)
    {
        var w = Bounds.Width; g.FillRectangle(new SolidColorBrush(Color.Parse("#1E2533")), new Rect(0, 0, w, Bounds.Height));
        if (Total <= 0) return;
        var step = new[] { 0.5, 1, 2, 5, 10, 15, 30, 60, 120 }.FirstOrDefault(s => w / (Total / s) >= 70, 300);
        var tick = new Pen(new SolidColorBrush(Color.Parse("#55FFFFFF")), 1);
        for (var t = 0.0; t <= Total + 1e-9; t += step)
        {
            var x = t / Total * w; g.DrawLine(tick, new Point(x, Bounds.Height - 6), new Point(x, Bounds.Height));
            g.DrawText(new FormattedText(Trendo.Core.Media.AudioEdit.Time(t), System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, 10, new SolidColorBrush(Color.Parse("#AAB4C3"))), new Point(x + 3, 3));
        }
    }
}
