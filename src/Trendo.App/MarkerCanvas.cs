using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Stoffbahn des Schnittlagebilds: Teile ziehen (1-cm-Raster), Doppelklick dreht, Überlappung/Stoffrand rot, Verbrauchslinie.</summary>
public sealed class MarkerCanvas : Control
{
    public IReadOnlyList<Marker.Piece> Pieces { get; set; } = [];
    public List<Marker.Placed> Layout { get; set; } = [];
    public double FabricW { get; set; } = 140;
    public double Scale { get; set; } = 3;
    public int Selected { get; set; } = -1;
    public Action<int, double, double>? Moved { get; set; }
    public Action<int>? Rotated { get; set; }
    public Action<int>? Clicked { get; set; }
    static readonly string[] Cols = ["#2F86F6", "#22B35E", "#F08A3C", "#7C4DFF", "#E5484D"];
    static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");
    int _drag = -1; Point _from, _at;

    public MarkerCanvas() { ClipToBounds = true; Cursor = new Cursor(StandardCursorType.Hand); }
    double Len => Math.Max(60, Marker.Length(Pieces, Layout) + 30);
    protected override Size MeasureOverride(Size a) => new(FabricW * Scale + 50, Len * Scale + 20);
    const double Ox = 40, Oy = 10;
    public Point CenterOf(int i) { var b = Marker.Box(Pieces, Layout[i]); return new(Ox + (b.X + b.W / 2) * Scale, Oy + (b.Y + b.H / 2) * Scale); }
    int Hit(Point p) { for (var i = Layout.Count - 1; i >= 0; i--) { var b = Marker.Box(Pieces, Layout[i]); if (new Rect(Ox + b.X * Scale, Oy + b.Y * Scale, b.W * Scale, b.H * Scale).Contains(p)) return i; } return -1; }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e); var p = e.GetPosition(this); var i = Hit(p);
        if (i >= 0 && e.ClickCount == 2) { Rotated?.Invoke(i); return; }
        _drag = i; _from = _at = p; if (i >= 0) e.Pointer.Capture(this); e.Handled = true;
    }
    protected override void OnPointerMoved(PointerEventArgs e) { base.OnPointerMoved(e); if (_drag < 0) return; _at = e.GetPosition(this); InvalidateVisual(); }
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e); e.Pointer.Capture(null); var i = _drag; _drag = -1; if (i < 0) { InvalidateVisual(); return; }
        var dx = Math.Round((_at.X - _from.X) / Scale); var dy = Math.Round((_at.Y - _from.Y) / Scale);
        if (Math.Abs(dx) + Math.Abs(dy) >= 1) Moved?.Invoke(i, dx, dy); else Clicked?.Invoke(i);
    }

    public override void Render(DrawingContext ctx)
    {
        var len = Len; var s = Scale;
        ctx.FillRectangle(new SolidColorBrush(Color.Parse("#FF0B1424")), new Rect(Bounds.Size));
        var fabric = new Rect(Ox, Oy, FabricW * s, len * s);
        ctx.FillRectangle(new SolidColorBrush(Color.Parse("#FFE9E4D6")), fabric);
        var grid = new Pen(new SolidColorBrush(Color.Parse("#22000000")), 1); var muted = new SolidColorBrush(Color.Parse("#8AA0BD"));
        for (var x = 10.0; x < FabricW; x += 10) ctx.DrawLine(grid, new Point(Ox + x * s, Oy), new Point(Ox + x * s, Oy + len * s));
        for (var y = 10.0; y < len; y += 10)
        {
            ctx.DrawLine(grid, new Point(Ox, Oy + y * s), new Point(Ox + FabricW * s, Oy + y * s));
            if ((int)y % 50 == 0) ctx.DrawText(new FormattedText($"{y:0}", De, FlowDirection.LeftToRight, new Typeface("Inter"), 10, muted), new Point(4, Oy + y * s - 6));
        }
        // Webkanten
        var selv = new Pen(new SolidColorBrush(Color.Parse("#FF8B7355")), 3);
        ctx.DrawLine(selv, new Point(Ox, Oy), new Point(Ox, Oy + len * s)); ctx.DrawLine(selv, new Point(Ox + FabricW * s, Oy), new Point(Ox + FabricW * s, Oy + len * s));
        var bad = Marker.Problems(Pieces, Layout, FabricW);
        for (var i = 0; i < Layout.Count; i++)
        {
            var b = Marker.Box(Pieces, Layout[i]); var off = i == _drag ? (_at - _from) : new Vector();
            var r = new Rect(Ox + b.X * s + off.X, Oy + b.Y * s + off.Y, b.W * s, b.H * s);
            var c = Color.Parse(Cols[Layout[i].Piece % Cols.Length]);
            ctx.DrawRectangle(new SolidColorBrush(c, 0.55), new Pen(new SolidColorBrush(bad.Contains(i) ? Colors.Red : i == Selected ? Color.Parse("#FACC15") : c), bad.Contains(i) || i == Selected ? 2.5 : 1.2), r);
            var inner = r.Deflate(Marker.Seam * s);
            if (inner.Width > 2 && inner.Height > 2) ctx.DrawRectangle(null, new Pen(new SolidColorBrush(Color.Parse("#AA1F2937")), 1, DashStyle.Dash), inner);
            var p = Pieces[Layout[i].Piece];
            using (ctx.PushClip(r.Deflate(3)))
            {
                ctx.DrawText(new FormattedText(p.Name + (Layout[i].Rot ? " ↻" : ""), De, FlowDirection.LeftToRight, new Typeface("Inter", FontStyle.Normal, FontWeight.SemiBold), 11, Brushes.Black), new Point(r.X + 5, r.Y + 4));
                ctx.DrawText(new FormattedText($"{p.W:0.#} × {p.H:0.#}", De, FlowDirection.LeftToRight, new Typeface("Inter"), 10, new SolidColorBrush(Color.Parse("#CC000000"))), new Point(r.X + 5, r.Y + 18));
            }
        }
        // Verbrauchslinie
        var used = Marker.Length(Pieces, Layout); var red = new SolidColorBrush(Color.Parse("#EF4444"));
        ctx.DrawLine(new Pen(red, 2, DashStyle.Dash), new Point(Ox - 6, Oy + used * s), new Point(Ox + FabricW * s + 6, Oy + used * s));
        ctx.DrawText(new FormattedText($"{used / 100:0.00} m", De, FlowDirection.LeftToRight, new Typeface("Inter", FontStyle.Normal, FontWeight.Bold), 11, red), new Point(Ox + FabricW * s - 50, Oy + used * s + 3));
    }
}
