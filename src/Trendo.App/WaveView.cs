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
    /// <summary>Spektrogramm statt Wellenform (Spalten × Zeilen, 0–1).</summary>
    public float[,]? Spectro { get; set; }
    Avalonia.Media.Imaging.WriteableBitmap? _specBmp; float[,]? _specFor;
    /// <summary>Neue Auswahl (Sekunden). Ein einfacher Klick setzt nur die Marke (a == b).</summary>
    public Action<double, double>? Selected { get; set; }
    /// <summary>Abtastrate der Spur (für die Frequenzachse des Spektrogramms).</summary>
    public int Rate { get; set; } = 44100;
    /// <summary>Gewählter Frequenzbereich im Spektrogramm (Hz); FreqHi ≤ FreqLo = keiner.</summary>
    public double FreqLo { get; set; }
    public double FreqHi { get; set; }
    /// <summary>Rechteck im Spektrogramm aufgezogen: Zeit von–bis und Frequenz von–bis (wie RX „Zeit-Frequenz-Auswahl“).</summary>
    public Action<double, double, double, double>? SelectedBox { get; set; }

    double? _down; double _now, _downY, _nowY;
    double FMax => Math.Min(16000, Rate / 2.0);
    const double FMin = 40;
    /// <summary>Höhe → Frequenz (logarithmisch, tief unten) – dieselbe Achse wie das Spektrogramm.</summary>
    public double YToHz(double y) { var h = Bounds.Height - Ruler; var t = Math.Clamp(1 - (y - Ruler) / Math.Max(1, h), 0, 1); return FMin * Math.Pow(FMax / FMin, t); }
    public double HzToY(double hz) { var h = Bounds.Height - Ruler; var t = Math.Log(Math.Clamp(hz, FMin, FMax) / FMin) / Math.Log(FMax / FMin); return Ruler + (1 - t) * h; }
    const double Ruler = 22;

    public WaveView() { ClipToBounds = true; Cursor = new Cursor(StandardCursorType.Ibeam); MinHeight = 200; }

    public double ToTime(double x) => Bounds.Width <= 0 || Seconds <= 0 ? 0 : Math.Clamp(x / Bounds.Width, 0, 1) * Seconds;
    public double ToX(double t) => Seconds <= 0 ? 0 : t / Seconds * Bounds.Width;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e); _down = _now = ToTime(e.GetPosition(this).X); _downY = _nowY = e.GetPosition(this).Y; e.Pointer.Capture(this); e.Handled = true; InvalidateVisual();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e); if (_down is null) return; _now = ToTime(e.GetPosition(this).X); _nowY = e.GetPosition(this).Y; InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e); if (_down is not { } a) return; _down = null; e.Pointer.Capture(null);
        var b = _now; if (Math.Abs(b - a) * Bounds.Width / Math.Max(Seconds, 1e-6) < 3) b = a; // kleine Bewegung = Klick
        if (Spectro is not null && SelectedBox is not null && b != a && Math.Abs(_nowY - _downY) >= 6)
        {
            double f1 = YToHz(_downY), f2 = YToHz(_nowY);
            SelectedBox(Math.Min(a, b), Math.Max(a, b), Math.Min(f1, f2), Math.Max(f1, f2)); return;
        }
        Selected?.Invoke(Math.Min(a, b), Math.Max(a, b));
    }

    /// <summary>Farben wie im Studio: dunkelblau (leise) über violett und rot zu gold-weiß (laut).</summary>
    static Avalonia.Media.Imaging.WriteableBitmap SpecBitmap(float[,] sp)
    {
        int cols = sp.GetLength(0), rows = sp.GetLength(1);
        var bmp = new Avalonia.Media.Imaging.WriteableBitmap(new PixelSize(cols, rows), new Vector(96, 96), Avalonia.Platform.PixelFormat.Bgra8888, Avalonia.Platform.AlphaFormat.Premul);
        using var fb = bmp.Lock();
        var px = new byte[cols * rows * 4];
        for (var y = 0; y < rows; y++)
            for (var x = 0; x < cols; x++)
            {
                var v = Math.Clamp(sp[x, y], 0f, 1f);
                (double r, double gg, double b) = v < 0.33 ? (v * 3 * 0.35, 0.02, 0.18 + v * 1.2) : v < 0.66 ? (0.35 + (v - 0.33) * 1.9, 0.05, 0.58 - (v - 0.33) * 1.5) : (0.98, 0.15 + (v - 0.66) * 2.4, (v - 0.66) * 1.6);
                var i = (y * cols + x) * 4; px[i] = (byte)(Math.Clamp(b, 0, 1) * 255); px[i + 1] = (byte)(Math.Clamp(gg, 0, 1) * 255); px[i + 2] = (byte)(Math.Clamp(r, 0, 1) * 255); px[i + 3] = 255;
            }
        for (var y = 0; y < rows; y++) System.Runtime.InteropServices.Marshal.Copy(px, y * cols * 4, fb.Address + y * fb.RowBytes, cols * 4); // Zeilenabstand des Bildes beachten
        return bmp;
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
        if (Spectro is { } sp)
        {
            if (!ReferenceEquals(_specFor, sp)) { _specBmp = SpecBitmap(sp); _specFor = sp; }
            if (_specBmp is not null) g.DrawImage(_specBmp, new Rect(0, top - 6, w, h - top + 6));
            // Auswahl über dem Spektrogramm: ganze Höhe oder Zeit-Frequenz-Rechteck
            double y1 = Ruler, y2 = h;
            if (_down is not null && Math.Abs(_nowY - _downY) >= 6) { y1 = Math.Min(_downY, _nowY); y2 = Math.Max(_downY, _nowY); }
            else if (_down is null && FreqHi > FreqLo) { y1 = HzToY(FreqHi); y2 = HzToY(FreqLo); }
            if (b > a)
            {
                var box = new Rect(ToX(a), y1, Math.Max(1, ToX(b) - ToX(a)), Math.Max(1, y2 - y1));
                g.FillRectangle(new SolidColorBrush(Color.Parse("#40FFFFFF")), box);
                g.DrawRectangle(null, new Pen(Brushes.White, 1, new DashStyle([4, 3], 0)), box);
            }
            // Frequenzachse links
            foreach (var hz in new[] { 100, 300, 1000, 3000, 10000 })
            {
                if (hz > FMax) continue; var y = HzToY(hz);
                g.DrawLine(new Pen(new SolidColorBrush(Color.Parse("#30FFFFFF")), 1), new Point(0, y), new Point(w, y));
                var ft = new FormattedText(hz >= 1000 ? $"{hz / 1000} kHz" : $"{hz} Hz", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, 10, new SolidColorBrush(Color.Parse("#CCFFFFFF")));
                g.DrawText(ft, new Point(4, y - 13));
            }
        }
        // Wellenform in Gold (Leitfarbe), innen heller
        else if (Peaks.Length > 0)
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
