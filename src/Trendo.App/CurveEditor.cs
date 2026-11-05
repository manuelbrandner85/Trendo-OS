using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Trendo.App;

/// <summary>Gradationskurve wie in Photoshop/GIMP/Lightroom: Punkte frei ziehen, Klick auf die Fläche setzt einen neuen Punkt,
/// Rechtsklick oder Doppelklick entfernt ihn. Die Endpunkte bleiben links und rechts, lassen sich aber hoch/runter ziehen.
/// Werte 0–255 (x = Eingang, y = Ausgang); dahinter das Helligkeits-Histogramm des Bildes.</summary>
public sealed class CurveEditor : Control
{
    readonly List<Point> _pts = [new(0, 0), new(255, 255)];
    int _drag = -1;

    /// <summary>Helligkeitsverteilung 0–255 (optional) für den Hintergrund.</summary>
    public int[]? Histogram { get; set; }
    /// <summary>Wird nach jeder Änderung gerufen (Punkte x,y,x,y …).</summary>
    public Action<double[]>? Changed { get; set; }

    public CurveEditor() { Width = 220; Height = 220; Cursor = new Cursor(StandardCursorType.Cross); ClipToBounds = true; }

    /// <summary>Punkte als flache Liste x,y,x,y … (leer = Gerade).</summary>
    public double[] Points
    {
        get => IsIdentity ? [] : [.. _pts.SelectMany(p => new[] { Math.Round(p.X), Math.Round(p.Y) })];
        set
        {
            _pts.Clear();
            for (var i = 0; i + 1 < value.Length; i += 2) _pts.Add(new Point(Math.Clamp(value[i], 0, 255), Math.Clamp(value[i + 1], 0, 255)));
            if (_pts.Count < 2) { _pts.Clear(); _pts.Add(new(0, 0)); _pts.Add(new(255, 255)); }
            _pts.Sort((a, b) => a.X.CompareTo(b.X)); InvalidateVisual();
        }
    }
    public bool IsIdentity => _pts.Count == 2 && _pts[0] == new Point(0, 0) && _pts[1] == new Point(255, 255);
    public int Count => _pts.Count;

    Point ToVal(Point s) => new(Math.Clamp(s.X / Bounds.Width * 255, 0, 255), Math.Clamp(255 - s.Y / Bounds.Height * 255, 0, 255));
    Point ToScr(Point v) => new(v.X / 255 * Bounds.Width, (255 - v.Y) / 255 * Bounds.Height);

    int Hit(Point screen) { for (var i = 0; i < _pts.Count; i++) if (Vector.Distance(ToScr(_pts[i]), screen) < 9) return i; return -1; }

    /// <summary>Punkt bei (x, y) in Werten 0–255 setzen – für Tastatur, Tests und Claude.</summary>
    public int AddPoint(double x, double y)
    {
        x = Math.Clamp(x, 1, 254); y = Math.Clamp(y, 0, 255);
        var i = _pts.FindIndex(p => p.X > x); if (i < 0) i = _pts.Count - 1;
        if (_pts.Any(p => Math.Abs(p.X - x) < 3)) { var k = _pts.FindIndex(p => Math.Abs(p.X - x) < 3); MovePoint(k, x, y); return k; }
        _pts.Insert(i, new Point(x, y)); Notify(); return i;
    }
    public void MovePoint(int i, double x, double y)
    {
        if (i < 0 || i >= _pts.Count) return;
        // Endpunkte bleiben am Rand, innere Punkte zwischen ihren Nachbarn
        x = i == 0 ? 0 : i == _pts.Count - 1 ? 255 : Math.Clamp(x, _pts[i - 1].X + 2, _pts[i + 1].X - 2);
        _pts[i] = new Point(x, Math.Clamp(y, 0, 255)); Notify();
    }
    public void RemovePoint(int i) { if (i <= 0 || i >= _pts.Count - 1) return; _pts.RemoveAt(i); Notify(); }
    void Notify() { InvalidateVisual(); Changed?.Invoke(Points); }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e); var s = e.GetPosition(this); var hit = Hit(s);
        var right = e.GetCurrentPoint(this).Properties.IsRightButtonPressed;
        if (hit >= 0 && (right || e.ClickCount >= 2)) { RemovePoint(hit); e.Handled = true; return; }
        if (right) return;
        if (hit < 0) { var v = ToVal(s); hit = AddPoint(v.X, v.Y); }
        _drag = hit; e.Pointer.Capture(this); e.Handled = true;
    }
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e); if (_drag < 0) return;
        var v = ToVal(e.GetPosition(this)); MovePoint(_drag, v.X, v.Y);
    }
    protected override void OnPointerReleased(PointerReleasedEventArgs e) { base.OnPointerReleased(e); _drag = -1; e.Pointer.Capture(null); }

    public override void Render(DrawingContext ctx)
    {
        var r = new Rect(Bounds.Size);
        ctx.FillRectangle(new SolidColorBrush(Color.Parse("#FF0E1726")), r);
        if (Histogram is { Length: 256 } hist && hist.Max() is var mx and > 0)
        {
            var hb = new SolidColorBrush(Color.Parse("#33FFFFFF")); var bw = r.Width / 256;
            for (var i = 0; i < 256; i++) { var hh = Math.Sqrt(hist[i] / (double)mx) * r.Height; ctx.FillRectangle(hb, new Rect(i * bw, r.Height - hh, bw + 0.5, hh)); }
        }
        var grid = new Pen(new SolidColorBrush(Color.Parse("#22FFFFFF")), 1);
        for (var i = 1; i < 4; i++) { ctx.DrawLine(grid, new Point(r.Width * i / 4, 0), new Point(r.Width * i / 4, r.Height)); ctx.DrawLine(grid, new Point(0, r.Height * i / 4), new Point(r.Width, r.Height * i / 4)); }
        ctx.DrawLine(new Pen(new SolidColorBrush(Color.Parse("#33FFFFFF")), 1, DashStyle.Dash), new Point(0, r.Height), new Point(r.Width, 0));
        // Kurve wie sie gerechnet wird (Trendo.Core.Media.ImageOps.Lut)
        var lut = Trendo.Core.Media.ImageOps.Lut(new System.Text.Json.Nodes.JsonObject { ["curve"] = new System.Text.Json.Nodes.JsonArray([.. _pts.SelectMany(p => new System.Text.Json.Nodes.JsonNode?[] { p.X, p.Y })]) });
        var g = new StreamGeometry();
        using (var c = g.Open()) { c.BeginFigure(ToScr(new Point(0, lut[0])), false); for (var i = 1; i < 256; i++) c.LineTo(ToScr(new Point(i, lut[i]))); c.EndFigure(false); }
        ctx.DrawGeometry(null, new Pen(new SolidColorBrush(Color.Parse("#FFFACC15")), 2), g);
        foreach (var p in _pts) ctx.DrawEllipse(new SolidColorBrush(Color.Parse("#FF0E1726")), new Pen(Brushes.White, 2), ToScr(p), 5, 5);
        ctx.DrawRectangle(null, new Pen(new SolidColorBrush(Color.Parse("#44FFFFFF")), 1), r);
    }
}
