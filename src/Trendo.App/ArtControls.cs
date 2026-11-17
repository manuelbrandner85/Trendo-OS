using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Trendo.Core.Kreativ;

namespace Trendo.App;

/// <summary>Künstlerstudio: Zustand eines Bildes im Speicher (Ebenen entpackt, Gesamtbild, Anzeige-Bitmap). Wird nur bei Änderungen neu gerechnet.</summary>
public sealed class ArtState
{
    public PaintDoc Doc { get; set; } = new(1, 1);
    public Dictionary<string, (string Png, Pixmap Px)> Cache { get; } = [];
    public string Sig { get; set; } = "";
    public Pixmap Comp { get; set; } = new(1, 1);
    public WriteableBitmap? Bmp { get; set; }

    /// <summary>Bereich des Gesamtbilds neu rechnen und in die Anzeige kopieren (x1/y1 einschließlich).</summary>
    public void Refresh(int x0 = 0, int y0 = 0, int x1 = int.MaxValue, int y1 = int.MaxValue)
    {
        x0 = Math.Max(0, x0); y0 = Math.Max(0, y0); x1 = Math.Min(Doc.W - 1, x1); y1 = Math.Min(Doc.H - 1, y1);
        if (x1 < x0 || y1 < y0) return;
        Doc.Composite(true, Comp, x0, y0, x1 + 1, y1 + 1);
        if (Bmp is null || Bmp.PixelSize.Width != Doc.W || Bmp.PixelSize.Height != Doc.H)
        {
            Bmp?.Dispose();
            Bmp = new WriteableBitmap(new PixelSize(Doc.W, Doc.H), new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Unpremul);
            x0 = 0; y0 = 0; x1 = Doc.W - 1; y1 = Doc.H - 1;
        }
        using var fb = Bmp.Lock();
        var bytes = (x1 - x0 + 1) * 4;
        for (var y = y0; y <= y1; y++) Marshal.Copy(Comp.Px, (y * Doc.W + x0) * 4, fb.Address + y * fb.RowBytes + x0 * 4, bytes);
    }
}

/// <summary>Malfläche: zeigt das Gesamtbild, nimmt Stift (mit Druck), Maus und Berührung an, zoomt mit Strg+Mausrad, verschiebt mit mittlerer Maustaste oder Leertaste.
/// Zeigt Raster, Spiegelachsen und Fluchtpunkte als Hilfslinien.</summary>
public sealed class PaintCanvas : Control
{
    public ArtState? State { get; set; }
    /// <summary>0 = einpassen.</summary>
    public double Zoom { get; set; }
    public Vector Pan { get; set; }
    public string Mode { get; set; } = "paint"; // paint, pick, points, select
    /// <summary>T-0733: aktuelle Auswahl als Vieleck (Leinwand-Pixel) und Art des Aufziehens (rect, lasso).</summary>
    public IReadOnlyList<(double X, double Y)>? SelPoly { get; set; }
    public string SelShape { get; set; } = "rect";
    /// <summary>Neue Auswahl aufgezogen bzw. umfahren (leer = Auswahl aufheben).</summary>
    public Action<List<(double X, double Y)>>? Selected { get; set; }
    /// <summary>Auswahl verschoben (dx, dy) und/oder skaliert (sx, sy, Anker oben links der Auswahl).</summary>
    public Action<double, double, double, double>? SelTransformed { get; set; }
    public bool ShowGrid { get; set; }
    public int GridStep { get; set; } = 100;
    public PaintSymmetry Sym { get; set; } = new();
    public DrawAssist Assist { get; set; } = new();
    public Func<StrokeEngine?>? BeginStroke { get; set; }
    public Action<StrokeEngine>? EndStroke { get; set; }
    public Action<double, double>? Picked { get; set; }
    public Action<double, double>? PointSet { get; set; }
    public Action<double>? ZoomChanged { get; set; }
    public double BrushRadius { get; set; } = 3;

    StrokeEngine? _stroke; Point? _panFrom; Point _hover; bool _space;
    // Auswahl: 1 = neu aufziehen, 2 = verschieben, 3 = Ecke ziehen (skalieren); Ecke 0..3 = oben links, oben rechts, unten rechts, unten links
    int _selDrag, _selCorner; (double X, double Y) _selFrom, _selAt; readonly List<(double X, double Y)> _selPts = [];
    static readonly IPen AntsWhite = new Pen(Brushes.White, 1, new DashStyle([4, 4], 0));
    static readonly IPen AntsBlack = new Pen(Brushes.Black, 1, new DashStyle([4, 4], 4));
    const double HandleSize = 9;

    (double X, double Y, double W, double H)? SelBox => SelPoly is { Count: >= 3 } p ? Trendo.Core.Kreativ.PaintSelection.Bounds(p) : null;

    /// <summary>Zielrechteck während Verschieben/Skalieren (Leinwand-Pixel).</summary>
    (double X, double Y, double W, double H) SelTarget()
    {
        var (x, y, w, h) = SelBox!.Value; double mx = _selAt.X - _selFrom.X, my = _selAt.Y - _selFrom.Y;
        if (_selDrag == 2) return (x + mx, y + my, w, h);
        double l = x, t = y, r = x + w, b = y + h;
        if (_selCorner is 0 or 3) l += mx; else r += mx;
        if (_selCorner is 0 or 1) t += my; else b += my;
        if (r - l < 2) { if (_selCorner is 0 or 3) l = r - 2; else r = l + 2; }
        if (b - t < 2) { if (_selCorner is 0 or 1) t = b - 2; else b = t + 2; }
        return (l, t, r - l, b - t);
    }

    int CornerAt(Point screen)
    {
        if (SelBox is not { } bx) return -1;
        Point[] c = [ToScreen(bx.X, bx.Y), ToScreen(bx.X + bx.W, bx.Y), ToScreen(bx.X + bx.W, bx.Y + bx.H), ToScreen(bx.X, bx.Y + bx.H)];
        // Bei kleiner Auswahl auf dem Bildschirm schrumpft der Griff, damit die Mitte zum Verschieben frei bleibt
        var grip = Math.Max(3, Math.Min(HandleSize, Math.Min(Math.Abs(c[1].X - c[0].X), Math.Abs(c[3].Y - c[0].Y)) / 4));
        for (var i = 0; i < 4; i++) if (Math.Abs(screen.X - c[i].X) <= grip && Math.Abs(screen.Y - c[i].Y) <= grip) return i;
        return -1;
    }

    void DrawAnts(DrawingContext ctx, IReadOnlyList<(double X, double Y)> pts, bool closed)
    {
        if (pts.Count < 2) return;
        var g = new StreamGeometry();
        using (var c = g.Open()) { c.BeginFigure(ToScreen(pts[0].X, pts[0].Y), false); for (var i = 1; i < pts.Count; i++) c.LineTo(ToScreen(pts[i].X, pts[i].Y)); c.EndFigure(closed); }
        ctx.DrawGeometry(null, AntsBlack, g); ctx.DrawGeometry(null, AntsWhite, g);
    }
    static readonly IPen GridPen = new Pen(new SolidColorBrush(Color.Parse("#40808080")), 1);
    static readonly IPen GuidePen = new Pen(new SolidColorBrush(Color.Parse("#B0E0A030")), 1.2, new DashStyle([6, 4], 0));
    static readonly IPen PointPen = new Pen(new SolidColorBrush(Color.Parse("#FFE0A030")), 2);
    static readonly IPen CursorPen = new Pen(new SolidColorBrush(Color.Parse("#C0FFFFFF")), 1);
    static readonly IPen CursorPen2 = new Pen(new SolidColorBrush(Color.Parse("#80000000")), 1);

    public PaintCanvas() { Focusable = true; ClipToBounds = true; Cursor = new Cursor(StandardCursorType.Cross); }

    public double Scale => State is null ? 1 : Zoom > 0 ? Zoom : Math.Max(0.02, Math.Min((Bounds.Width - 16) / State.Doc.W, (Bounds.Height - 16) / State.Doc.H));
    Point Origin => State is null ? default : new Point((Bounds.Width - State.Doc.W * Scale) / 2 + Pan.X, (Bounds.Height - State.Doc.H * Scale) / 2 + Pan.Y);
    public (double X, double Y) ToDoc(Point p) { var o = Origin; var s = Scale; return ((p.X - o.X) / s, (p.Y - o.Y) / s); }
    public Point ToScreen(double x, double y) { var o = Origin; var s = Scale; return new Point(o.X + x * s, o.Y + y * s); }

    public override void Render(DrawingContext ctx)
    {
        ctx.FillRectangle(new SolidColorBrush(Color.Parse("#FF1B1C1F")), new Rect(Bounds.Size));
        if (State?.Bmp is not { } bmp) return;
        var o = Origin; var s = Scale; var r = new Rect(o.X, o.Y, State.Doc.W * s, State.Doc.H * s);
        ctx.FillRectangle(Brushes.Black, r.Inflate(1));
        using (ctx.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = s >= 2 ? BitmapInterpolationMode.None : BitmapInterpolationMode.HighQuality }))
            ctx.DrawImage(bmp, new Rect(0, 0, State.Doc.W, State.Doc.H), r);
        using (ctx.PushClip(r))
        {
            if (ShowGrid && GridStep > 0)
            {
                for (var x = GridStep; x < State.Doc.W; x += GridStep) ctx.DrawLine(GridPen, ToScreen(x, 0), ToScreen(x, State.Doc.H));
                for (var y = GridStep; y < State.Doc.H; y += GridStep) ctx.DrawLine(GridPen, ToScreen(0, y), ToScreen(State.Doc.W, y));
            }
            switch (Sym.Mode)
            {
                case "senkrecht": ctx.DrawLine(GuidePen, ToScreen(Sym.Cx, 0), ToScreen(Sym.Cx, State.Doc.H)); break;
                case "waagrecht": ctx.DrawLine(GuidePen, ToScreen(0, Sym.Cy), ToScreen(State.Doc.W, Sym.Cy)); break;
                case "beide": ctx.DrawLine(GuidePen, ToScreen(Sym.Cx, 0), ToScreen(Sym.Cx, State.Doc.H)); ctx.DrawLine(GuidePen, ToScreen(0, Sym.Cy), ToScreen(State.Doc.W, Sym.Cy)); break;
                case "radial":
                    var len = Math.Max(State.Doc.W, State.Doc.H);
                    for (var k = 0; k < Math.Clamp(Sym.Parts, 2, 32); k++) { var a = 2 * Math.PI * k / Math.Clamp(Sym.Parts, 2, 32) - Math.PI / 2; ctx.DrawLine(GuidePen, ToScreen(Sym.Cx, Sym.Cy), ToScreen(Sym.Cx + Math.Cos(a) * len, Sym.Cy + Math.Sin(a) * len)); }
                    break;
            }
        }
        // Zeichenhilfen: Fluchtpunkte mit Strahlen, Lineal
        foreach (var (px, py) in Assist.Pts)
        {
            var c = ToScreen(px, py); ctx.DrawEllipse(null, PointPen, c, 6, 6);
            if (Assist.Mode.StartsWith("perspektive", StringComparison.Ordinal))
                for (var k = 0; k < 16; k++) { var a = Math.PI * 2 * k / 16; ctx.DrawLine(GuidePen, c, new Point(c.X + Math.Cos(a) * 4000, c.Y + Math.Sin(a) * 4000)); }
        }
        // T-0733: Auswahl – gestrichelt, Griffe an den Ecken; beim Verschieben/Skalieren Vorschau des Inhalts am Ziel
        if (Mode == "select")
        {
            if (_selDrag == 1 && _selPts.Count > 0)
            {
                if (SelShape == "lasso") DrawAnts(ctx, _selPts, false);
                else DrawAnts(ctx, Trendo.Core.Kreativ.PaintSelection.Rect(_selFrom.X, _selFrom.Y, _selAt.X, _selAt.Y), true);
            }
            else if (SelBox is { } bx && SelPoly is { } sp)
            {
                if (_selDrag is 2 or 3)
                {
                    var (tx, ty, tw, th) = SelTarget();
                    var dst = new Rect(ToScreen(tx, ty), ToScreen(tx + tw, ty + th));
                    using (ctx.PushClip(r)) ctx.DrawImage(bmp, new Rect(bx.X, bx.Y, Math.Max(1, bx.W), Math.Max(1, bx.H)), dst);
                    ctx.DrawRectangle(null, AntsWhite, dst);
                }
                else
                {
                    DrawAnts(ctx, sp, true);
                    Point[] c = [ToScreen(bx.X, bx.Y), ToScreen(bx.X + bx.W, bx.Y), ToScreen(bx.X + bx.W, bx.Y + bx.H), ToScreen(bx.X, bx.Y + bx.H)];
                    foreach (var p in c) ctx.DrawRectangle(Brushes.White, new Pen(Brushes.Black, 1), new Rect(p.X - HandleSize / 2, p.Y - HandleSize / 2, HandleSize, HandleSize));
                }
            }
        }
        if (Assist.Mode == "lineal" && Assist.Pts.Count >= 2) ctx.DrawLine(PointPen, ToScreen(Assist.Pts[0].X, Assist.Pts[0].Y), ToScreen(Assist.Pts[1].X, Assist.Pts[1].Y));
        // Pinselumriss
        if (Mode == "paint" && IsPointerOver) { var rr = BrushRadius * s; ctx.DrawEllipse(null, CursorPen2, _hover, rr + 1, rr + 1); ctx.DrawEllipse(null, CursorPen, _hover, rr, rr); }
    }

    static double PressureOf(PointerPoint p, PointerType t) => t == PointerType.Pen && p.Properties.Pressure > 0 ? p.Properties.Pressure : 1;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e); Focus();
        var pt = e.GetCurrentPoint(this);
        if (pt.Properties.IsMiddleButtonPressed || _space) { _panFrom = pt.Position; e.Pointer.Capture(this); e.Handled = true; return; }
        if (!pt.Properties.IsLeftButtonPressed && e.Pointer.Type == PointerType.Mouse) return;
        var (x, y) = ToDoc(pt.Position);
        switch (Mode)
        {
            case "pick": Picked?.Invoke(x, y); e.Handled = true; return;
            case "points": PointSet?.Invoke(x, y); e.Handled = true; return;
            case "select":
                _selFrom = _selAt = (x, y); _selPts.Clear();
                var corner = CornerAt(pt.Position);
                if (corner >= 0) { _selDrag = 3; _selCorner = corner; }
                else if (SelBox is { } bx && x >= bx.X && x <= bx.X + bx.W && y >= bx.Y && y <= bx.Y + bx.H) _selDrag = 2;
                else { _selDrag = 1; _selPts.Add((x, y)); }
                e.Pointer.Capture(this); e.Handled = true; InvalidateVisual(); return;
        }
        _stroke = BeginStroke?.Invoke(); if (_stroke is null) return;
        e.Pointer.Capture(this); _stroke.Add(x, y, PressureOf(pt, e.Pointer.Type)); Flush(); e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var pt = e.GetCurrentPoint(this); _hover = pt.Position;
        if (_panFrom is { } from) { Pan += pt.Position - from; _panFrom = pt.Position; InvalidateVisual(); return; }
        if (_selDrag != 0) { _selAt = ToDoc(pt.Position); if (_selDrag == 1 && SelShape == "lasso") _selPts.Add(_selAt); InvalidateVisual(); return; }
        if (_stroke is null) { if (Mode == "paint") InvalidateVisual(); return; }
        foreach (var ip in e.GetIntermediatePoints(this)) { var (x, y) = ToDoc(ip.Position); _stroke.Add(x, y, PressureOf(ip, e.Pointer.Type)); }
        Flush();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_panFrom is not null) { _panFrom = null; e.Pointer.Capture(null); return; }
        if (_selDrag != 0) { _selAt = ToDoc(e.GetPosition(this)); e.Pointer.Capture(null); FinishSelection(); return; }
        if (_stroke is not { } s) return;
        _stroke = null; e.Pointer.Capture(null); Flush(s); EndStroke?.Invoke(s);
    }

    void FinishSelection()
    {
        var kind = _selDrag; var target = kind is 2 or 3 && SelBox is not null ? SelTarget() : default; // vor dem Zurücksetzen: SelTarget hängt an der Zugart
        _selDrag = 0; var moved = Math.Abs(_selAt.X - _selFrom.X) + Math.Abs(_selAt.Y - _selFrom.Y) > 1 / Scale;
        InvalidateVisual();
        if (kind == 1)
        {
            if (SelShape == "lasso") { _selPts.Add(_selAt); Selected?.Invoke(_selPts.Count >= 3 && moved ? [.. _selPts] : []); }
            else Selected?.Invoke(moved ? Trendo.Core.Kreativ.PaintSelection.Rect(_selFrom.X, _selFrom.Y, _selAt.X, _selAt.Y) : []);
            _selPts.Clear(); return;
        }
        if (!moved || SelBox is not { } bx) return;
        var (tx, ty, tw, th) = target;
        SelTransformed?.Invoke(tx - bx.X, ty - bx.Y, bx.W > 0 ? tw / bx.W : 1, bx.H > 0 ? th / bx.H : 1);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (_stroke is { } s) { _stroke = null; Flush(s); EndStroke?.Invoke(s); }
        _panFrom = null;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (State is null || (e.KeyModifiers & KeyModifiers.Control) == 0) return;
        var before = ToDoc(e.GetPosition(this)); var z = Math.Clamp(Scale * (e.Delta.Y > 0 ? 1.2 : 1 / 1.2), 0.05, 16);
        Zoom = z; var after = ToScreen(before.X, before.Y); Pan += e.GetPosition(this) - after;
        ZoomChanged?.Invoke(z); InvalidateVisual(); e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e) { base.OnKeyDown(e); if (e.Key == Key.Space) { _space = true; Cursor = new Cursor(StandardCursorType.Hand); e.Handled = true; } }
    protected override void OnKeyUp(KeyEventArgs e) { base.OnKeyUp(e); if (e.Key == Key.Space) { _space = false; Cursor = new Cursor(StandardCursorType.Cross); } }

    /// <summary>Gemalten Bereich in die Anzeige übernehmen.</summary>
    public void Flush(StrokeEngine? s = null)
    {
        s ??= _stroke; if (s is null || State is null) return;
        if (s.TakeDirty() is { } r) State.Refresh(r.X0, r.Y0, r.X1, r.Y1);
        InvalidateVisual();
    }
}

/// <summary>Farbrad: Farbton-Ring außen, Sättigung (waagrecht) und Helligkeit (senkrecht) im Quadrat innen – wie in Malprogrammen üblich.</summary>
public sealed class HsvWheel : Control
{
    public double H { get; set; }
    public double S { get; set; } = 1;
    public double V { get; set; } = 1;
    public Action<string>? Changed { get; set; }
    int _drag; // 1 = Ring, 2 = Quadrat

    public HsvWheel() { Width = 190; Height = 190; Cursor = new Cursor(StandardCursorType.Hand); }
    public string Hex => PaintColors.FromHsv(H, S, V);
    public void Set(string hex) { if (!PaintColors.IsHex(hex)) return; var (h, s, v) = PaintColors.Hsv(hex); if (s > 0 && v > 0) H = h; S = s; V = v; InvalidateVisual(); }

    double R => Math.Min(Bounds.Width, Bounds.Height) / 2;
    Point C => new(Bounds.Width / 2, Bounds.Height / 2);
    Rect Square { get { var inner = R * 0.78 * 0.7; return new Rect(C.X - inner, C.Y - inner, inner * 2, inner * 2); } }

    public override void Render(DrawingContext ctx)
    {
        var c = C; var r = R; var thick = r * 0.2;
        for (var a = 0; a < 360; a += 2)
        {
            var pen = new Pen(new SolidColorBrush(Color.Parse(PaintColors.FromHsv(a, 1, 1))), thick + 1);
            double a0 = (a - 90) * Math.PI / 180, a1 = (a + 2.5 - 90) * Math.PI / 180, rm = r - thick / 2;
            ctx.DrawLine(pen, new Point(c.X + Math.Cos(a0) * rm, c.Y + Math.Sin(a0) * rm), new Point(c.X + Math.Cos(a1) * rm, c.Y + Math.Sin(a1) * rm));
        }
        var sq = Square;
        ctx.FillRectangle(new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Colors.White, 0), new GradientStop(Color.Parse(PaintColors.FromHsv(H, 1, 1)), 1) } }, sq);
        ctx.FillRectangle(new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Color.FromArgb(0, 0, 0, 0), 0), new GradientStop(Colors.Black, 1) } }, sq);
        // Marken
        var ha = (H - 90) * Math.PI / 180; var hp = new Point(c.X + Math.Cos(ha) * (r - thick / 2), c.Y + Math.Sin(ha) * (r - thick / 2));
        ctx.DrawEllipse(null, new Pen(Brushes.White, 2), hp, thick / 2, thick / 2);
        var sp = new Point(sq.X + S * sq.Width, sq.Y + (1 - V) * sq.Height);
        ctx.DrawEllipse(null, new Pen(Brushes.Black, 2.5), sp, 6, 6); ctx.DrawEllipse(null, new Pen(Brushes.White, 1.5), sp, 6, 6);
    }

    void Apply(Point p, bool start)
    {
        var d = p - C; var dist = Math.Sqrt(d.X * d.X + d.Y * d.Y);
        if (start) _drag = Square.Contains(p) ? 2 : dist >= R * 0.7 && dist <= R ? 1 : 0;
        if (_drag == 1) H = (Math.Atan2(d.Y, d.X) * 180 / Math.PI + 90 + 360) % 360;
        else if (_drag == 2) { var sq = Square; S = Math.Clamp((p.X - sq.X) / sq.Width, 0, 1); V = Math.Clamp(1 - (p.Y - sq.Y) / sq.Height, 0, 1); }
        else return;
        InvalidateVisual();
    }
    protected override void OnPointerPressed(PointerPressedEventArgs e) { base.OnPointerPressed(e); Apply(e.GetPosition(this), true); if (_drag != 0) e.Pointer.Capture(this); }
    protected override void OnPointerMoved(PointerEventArgs e) { base.OnPointerMoved(e); if (_drag != 0) Apply(e.GetPosition(this), false); }
    protected override void OnPointerReleased(PointerReleasedEventArgs e) { base.OnPointerReleased(e); if (_drag != 0) { _drag = 0; e.Pointer.Capture(null); Changed?.Invoke(Hex); } }
}
