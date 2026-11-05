using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Trendo.App;

/// <summary>Arbeitsfläche der Bildbearbeitung: zeigt das fertig gerechnete Bild eingepasst (oder gezoomt) über einem Karomuster,
/// rechnet Mauspositionen in Leinwand-Pixel um und zeigt während des Ziehens eine Live-Vorschau (Strich, Form, Ausschnitt, Verschieben).</summary>
public sealed class ImageCanvas : Control
{
    public Bitmap? Image { get; set; }
    public int DocW { get; set; } = 800;
    public int DocH { get; set; } = 600;
    public double Zoom { get; set; } = 0; // 0 = einpassen
    public string Tool { get; set; } = "move";
    public Color BrushColor { get; set; } = Colors.Black;
    public double BrushSize { get; set; } = 12;
    /// <summary>Rahmen der gewählten Ebene (Leinwand-Pixel).</summary>
    public Rect? Selection { get; set; }
    public Action<List<(double X, double Y)>, bool>? Stroke { get; set; }
    public Action<Rect>? Dragged { get; set; }
    public Action<Point>? Clicked { get; set; }
    public Action<Point, Vector>? Moved { get; set; }
    /// <summary>Gezogene Linie (Verlauf): Start und Ende in Leinwand-Pixeln.</summary>
    public Action<Point, Point>? Line { get; set; }
    /// <summary>Aktive Auswahl (gestrichelt, Leinwand-Pixel).</summary>
    public Rect? Marquee { get; set; }
    /// <summary>Lasso-Auswahl (Leinwand-Pixel), gestrichelt geschlossen gezeichnet.</summary>
    public IReadOnlyList<Point>? LassoPath { get; set; }
    /// <summary>Zauberstab-Auswahl als halbdurchsichtige Fläche über der ganzen Leinwand.</summary>
    public Bitmap? SelectionOverlay { get; set; }
    /// <summary>Freihändig umfahrene Fläche (Lasso).</summary>
    public Action<List<(double X, double Y)>>? Lassoed { get; set; }
    /// <summary>Quelle des Kopierstempels.</summary>
    public Point? CloneSource { get; set; }

    Point? _from; Point _at; readonly List<Point> _pts = [];

    public ImageCanvas() { ClipToBounds = true; Cursor = new Cursor(StandardCursorType.Cross); }

    double Scale => Zoom > 0 ? Zoom : Math.Max(0.02, Math.Min((Bounds.Width - 24) / DocW, (Bounds.Height - 24) / DocH));
    Point Origin => new((Bounds.Width - DocW * Scale) / 2, (Bounds.Height - DocH * Scale) / 2);
    public Point ToDoc(Point p) => new((p.X - Origin.X) / Scale, (p.Y - Origin.Y) / Scale);
    public Point ToScreen(Point d) => new(Origin.X + d.X * Scale, Origin.Y + d.Y * Scale);

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _from = _at = ToDoc(e.GetPosition(this)); _pts.Clear(); _pts.Add(_from.Value);
        e.Pointer.Capture(this); e.Handled = true; InvalidateVisual();
    }
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e); if (_from is null) return;
        _at = ToDoc(e.GetPosition(this)); if (Tool is "brush" or "eraser" or "vector" or "heal" or "clone" or "lasso") _pts.Add(_at); InvalidateVisual();
    }
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e); e.Pointer.Capture(null); if (_from is not { } a) return; _from = null;
        var moved = Math.Abs(_at.X - a.X) + Math.Abs(_at.Y - a.Y) > 2 / Scale;
        switch (Tool)
        {
            case "brush" or "eraser" or "vector" or "heal" or "clone": Stroke?.Invoke(_pts.Select(p => (p.X, p.Y)).ToList(), Tool == "eraser"); break;
            case "lasso" when _pts.Count >= 3: Lassoed?.Invoke(_pts.Select(p => (p.X, p.Y)).ToList()); break;
            case "rect" or "ellipse" or "crop" or "select" when moved: Dragged?.Invoke(new Rect(a, _at).Normalize()); break;
            case "gradient" when moved: Line?.Invoke(a, _at); break;
            case "move" when moved: Moved?.Invoke(a, _at - a); break;
            default: Clicked?.Invoke(a); break;
        }
        _pts.Clear(); InvalidateVisual();
    }

    void DrawPoly(DrawingContext ctx, IPen pen, IReadOnlyList<Point> pts, bool closed)
    {
        var g = new StreamGeometry();
        using (var c = g.Open()) { c.BeginFigure(ToScreen(pts[0]), false); for (var i = 1; i < pts.Count; i++) c.LineTo(ToScreen(pts[i])); c.EndFigure(closed); }
        ctx.DrawGeometry(null, new Pen(new SolidColorBrush(Color.Parse("#99000000")), 2), g); ctx.DrawGeometry(null, pen, g);
    }

    public override void Render(DrawingContext ctx)
    {
        ctx.FillRectangle(new SolidColorBrush(Color.Parse("#FF0B1424")), new Rect(Bounds.Size));
        var s = Scale; var o = Origin; var r = new Rect(o.X, o.Y, DocW * s, DocH * s);
        // Karomuster für durchsichtige Stellen
        using (ctx.PushClip(r))
        {
            var light = new SolidColorBrush(Color.Parse("#FFE5E7EB")); var dark = new SolidColorBrush(Color.Parse("#FFC9CDD3"));
            ctx.FillRectangle(light, r);
            for (var yy = 0; yy * 12 < r.Height; yy++) for (var xx = 0; xx * 12 < r.Width; xx++) if ((xx + yy) % 2 == 0) ctx.FillRectangle(dark, new Rect(r.X + xx * 12, r.Y + yy * 12, 12, 12));
        }
        if (Image is { } img) ctx.DrawImage(img, new Rect(0, 0, img.PixelSize.Width, img.PixelSize.Height), r);
        ctx.DrawRectangle(null, new Pen(new SolidColorBrush(Color.Parse("#55FFFFFF")), 1), r);
        var gold = new Pen(new SolidColorBrush(Color.Parse("#FFFACC15")), 1.5, DashStyle.Dash);
        if (Selection is { } sel)
        {
            var sr = sel; if (_from is { } f0 && Tool == "move") sr = sr.Translate(_at - f0);
            ctx.DrawRectangle(null, gold, new Rect(ToScreen(sr.TopLeft), ToScreen(sr.BottomRight)));
        }
        var ants = new Pen(Brushes.White, 1, new DashStyle([4, 4], 0));
        if (SelectionOverlay is { } so) ctx.DrawImage(so, new Rect(0, 0, so.PixelSize.Width, so.PixelSize.Height), r);
        if (LassoPath is { Count: >= 3 } lp) DrawPoly(ctx, ants, lp, true);
        else if (Marquee is { } mq) ctx.DrawRectangle(null, ants, new Rect(ToScreen(mq.TopLeft), ToScreen(mq.BottomRight)));
        if (CloneSource is { } cs && Tool == "clone") { var sp = ToScreen(cs); ctx.DrawEllipse(null, new Pen(Brushes.White, 1.5), sp, 8, 8); ctx.DrawLine(new Pen(Brushes.White, 1), sp - new Vector(12, 0), sp + new Vector(12, 0)); ctx.DrawLine(new Pen(Brushes.White, 1), sp - new Vector(0, 12), sp + new Vector(0, 12)); }
        if (_from is not { } a) return;
        switch (Tool)
        {
            case "brush" or "eraser" or "vector" or "heal" or "clone" when _pts.Count > 0:
                var pen = new Pen(Tool is "eraser" or "heal" or "clone" ? new SolidColorBrush(Color.Parse("#AAFFFFFF")) : new SolidColorBrush(BrushColor), BrushSize * s, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
                for (var i = 1; i < _pts.Count; i++) ctx.DrawLine(pen, ToScreen(_pts[i - 1]), ToScreen(_pts[i]));
                if (_pts.Count == 1) ctx.DrawEllipse(pen.Brush, null, ToScreen(_pts[0]), BrushSize * s / 2, BrushSize * s / 2);
                break;
            case "rect" or "crop":
                ctx.DrawRectangle(Tool == "crop" ? new SolidColorBrush(Color.Parse("#22FFFFFF")) : new SolidColorBrush(BrushColor, 0.6), gold, new Rect(ToScreen(a), ToScreen(_at)).Normalize()); break;
            case "lasso" when _pts.Count > 1:
                DrawPoly(ctx, ants, _pts, false); break;
            case "select":
                ctx.DrawRectangle(null, ants, new Rect(ToScreen(a), ToScreen(_at)).Normalize()); break;
            case "gradient":
                ctx.DrawLine(new Pen(Brushes.White, 2), ToScreen(a), ToScreen(_at)); ctx.DrawEllipse(new SolidColorBrush(BrushColor), new Pen(Brushes.White, 1), ToScreen(a), 5, 5); break;
            case "ellipse":
                var er = new Rect(ToScreen(a), ToScreen(_at)).Normalize(); ctx.DrawEllipse(new SolidColorBrush(BrushColor, 0.6), gold, er.Center, er.Width / 2, er.Height / 2); break;
        }
    }
}
