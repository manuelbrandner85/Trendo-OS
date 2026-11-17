using System.Globalization;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Trendo.Core.Kreativ;

namespace Trendo.App;

/// <summary>T-0697: Zeichnet eine Comicseite (Bildfelder mit Bildausschnitt, Mal-Ebenen, Rahmen, Storyboard-Zeilen, Sprechblasen mit Dorn und Zeilenumbruch).
/// Dieselbe Zeichnung dient der Arbeitsfläche und dem Export (PNG, PDF, CBZ) – was man sieht, kommt heraus.</summary>
public static class ComicDraw
{
    static readonly IPen Frame = new Pen(Brushes.Black, 4, lineJoin: PenLineJoin.Round);
    static readonly IPen Ink = new Pen(Brushes.Black, 3, lineJoin: PenLineJoin.Round);
    static readonly IBrush Paper = Brushes.White, BoxFill = new SolidColorBrush(Color.Parse("#FFF3B0")), Empty = new SolidColorBrush(Color.Parse("#F7F7F8")), Hint = new SolidColorBrush(Color.Parse("#B0B4BC"));

    public static Geometry Poly(IEnumerable<Point> pts)
    {
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            var first = true;
            foreach (var p in pts) { if (first) { c.BeginFigure(p, true); first = false; } else c.LineTo(p); }
            if (!first) c.EndFigure(true);
        }
        return g;
    }

    public static Geometry Quad(ComicPanel p) => Poly(Enumerable.Range(0, 4).Select(k => new Point(p.P[k * 2], p.P[k * 2 + 1])));

    /// <summary>Zielrechteck eines Bildes im Feld: füllt das Feld, Zoom und Versatz wählen den Ausschnitt.</summary>
    public static Rect ImageRect(ComicPanel p, Size img)
    {
        var (x, y, w, h) = p.Bounds();
        var s = Math.Max(w / Math.Max(1, img.Width), h / Math.Max(1, img.Height)) * Math.Max(1, p.Zoom);
        double dw = img.Width * s, dh = img.Height * s;
        return new Rect(x + (w - dw) / 2 + p.Ox * w, y + (h - dh) / 2 + p.Oy * h, dw, dh);
    }

    public static void Page(DrawingContext ctx, ComicBook book, int index, Func<string, Bitmap?> image, IImage? art, bool screen = false)
    {
        var page = book.Pages[index];
        ctx.FillRectangle(Paper, new Rect(0, 0, ComicBook.PageW, ComicBook.PageH));
        if (index == 0 && book.Title.Trim().Length > 0)
            ctx.DrawText(Text(book.Title.Trim(), 24, FontWeight.Bold, "Segoe UI", ComicBook.PageW - 80), new Point(ComicBook.Margin, 2));
        for (var i = 0; i < page.Panels.Count; i++)
        {
            var p = page.Panels[i]; var q = Quad(p);
            ctx.DrawGeometry(p.Image.Length > 0 ? Paper : screen ? Empty : Paper, null, q);
            if (p.Image.Length > 0 && image(p.Image) is { } bmp)
                using (ctx.PushGeometryClip(q)) ctx.DrawImage(bmp, new Rect(bmp.Size), ImageRect(p, bmp.Size));
            else if (screen)
            {
                var (x, y, w, h) = p.Bounds();
                var t = Text(book.Storyboard ? $"Einstellung {i + 1}" : $"Bild {i + 1}", 26, FontWeight.Normal, "Segoe UI", w, Hint, TextAlignment.Center);
                ctx.DrawText(t, new Point(x, y + h / 2 - t.Height / 2));
            }
        }
        if (art is not null) ctx.DrawImage(art, new Rect(0, 0, ComicBook.PageW, ComicBook.PageH));
        for (var i = 0; i < page.Panels.Count; i++)
        {
            var p = page.Panels[i];
            ctx.DrawGeometry(null, Frame, Quad(p));
            if (!book.Storyboard) continue;
            var (x, y, w, h) = p.Bounds();
            var head = Text(p.ShotHead(i), 18, FontWeight.Bold, "Segoe UI", w); ctx.DrawText(head, new Point(x, y + h + 6));
            if (p.Shot.Trim().Length > 0) { var body = Text(p.Shot.Trim(), 16, FontWeight.Normal, "Segoe UI", w); body.MaxLineCount = 4; ctx.DrawText(body, new Point(x, y + h + 8 + head.Height)); }
        }
        foreach (var b in page.Balloons) Balloon(ctx, b);
    }

    public static FormattedText Text(string s, double size, FontWeight weight, string font, double maxWidth, IBrush? fg = null, TextAlignment align = TextAlignment.Left) =>
        new(s, CultureInfo.GetCultureInfo("de-DE"), FlowDirection.LeftToRight, new Typeface(new FontFamily(font), FontStyle.Normal, weight), size, fg ?? Brushes.Black)
        { MaxTextWidth = Math.Max(10, maxWidth), TextAlignment = align };

    public static void Balloon(DrawingContext ctx, ComicBalloon b)
    {
        var fill = b.Kind == "kasten" ? BoxFill : Paper;
        Geometry shape = b.Kind switch
        {
            "kasten" => new RectangleGeometry(new Rect(b.X, b.Y, b.W, b.H)),
            "schrei" => Poly(b.Burst().Select(p => new Point(p.X, p.Y))),
            _ => new EllipseGeometry(new Rect(b.X, b.Y, b.W, b.H)),
        };
        if (b.Kind == "gedanke")
        {
            double cx = b.X + b.W / 2, cy = b.Y + b.H / 2;
            for (var k = 1; k <= 3; k++) { var t = 0.45 + k * 0.17; var r = Math.Max(4, 16 - k * 4); ctx.DrawEllipse(fill, Ink, new Point(cx + (b.TailX - cx) * t, cy + (b.TailY - cy) * t), r, r); }
            ctx.DrawGeometry(fill, Ink, shape);
        }
        else
        {
            Geometry? tail = null;
            if (b.HasTail) { var (a, c) = b.TailBase(); tail = Poly([new Point(a.X, a.Y), new Point(b.TailX, b.TailY), new Point(c.X, c.Y)]); }
            // erst mit Rand, dann nur Füllung: die Linie zwischen Dorn und Blase verschwindet
            if (tail is not null) ctx.DrawGeometry(fill, Ink, tail);
            ctx.DrawGeometry(fill, Ink, shape);
            if (tail is not null) { ctx.DrawGeometry(fill, null, tail); ctx.DrawGeometry(fill, null, shape); }
        }
        var font = b.Kind == "kasten" ? "Segoe UI" : "Comic Sans MS, Segoe UI";
        var text = Text(b.Text, b.Size, b.Kind == "schrei" ? FontWeight.Bold : FontWeight.Normal, font, b.W - 2 * b.Pad, null, b.Kind == "kasten" ? TextAlignment.Left : TextAlignment.Center);
        text.MaxTextHeight = Math.Max(b.Size * 1.3, b.H - b.Pad);
        ctx.DrawText(text, new Point(b.X + b.Pad, b.Y + Math.Max(4, (b.H - text.Height) / 2)));
    }

    /// <summary>Mal-Ebenen einer Seite als Bild (durchsichtig, ohne Hintergrund).</summary>
    public static WriteableBitmap? Art(PaintDoc? d)
    {
        if (d is null || d.Layers.All(l => !l.Visible || l.Px.IsEmpty())) return null;
        var bmp = new WriteableBitmap(new PixelSize(d.W, d.H), new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Unpremul);
        Copy(bmp, d.Composite(false), 0, 0, d.W - 1, d.H - 1);
        return bmp;
    }

    public static void Copy(WriteableBitmap bmp, Pixmap src, int x0, int y0, int x1, int y1)
    {
        x0 = Math.Max(0, x0); y0 = Math.Max(0, y0); x1 = Math.Min(src.W - 1, x1); y1 = Math.Min(src.H - 1, y1);
        if (x1 < x0 || y1 < y0) return;
        using var fb = bmp.Lock(); var bytes = (x1 - x0 + 1) * 4;
        for (var y = y0; y <= y1; y++) Marshal.Copy(src.Px, (y * src.W + x0) * 4, fb.Address + y * fb.RowBytes + x0 * 4, bytes);
    }
}

/// <summary>T-0697: Arbeitsfläche einer Comicseite. Werkzeuge: Auswahl (Bildfelder und Blasen ziehen, Ecken verformen, Dorn richten, Bildausschnitt mit Alt),
/// Bildfeld aufziehen, Pinsel und Radierer (Mal-Kern des Künstlerstudios, Strich bleibt im Bildfeld, in dem er beginnt), Sprechblase setzen.</summary>
public sealed class ComicPageView : Control
{
    public ComicBook? Book { get; set; }
    public int PageIndex { get; set; }
    public string Tool { get; set; } = "auswahl";
    public (string Kind, string Id)? Selected { get; set; }
    public Func<string, Bitmap?> Image { get; set; } = _ => null;
    /// <summary>Mal-Ebenen der Seite und die aktive Ebene.</summary>
    public PaintDoc? Paint { get; set; }
    public string? ActiveLayer { get; set; }
    public WriteableBitmap? ArtBmp { get; set; }
    public string BrushId { get; set; } = "tusche";
    public string BrushColor { get; set; } = "#111111";
    public double BrushSize { get; set; } = 6;

    /// <summary>Das Modell wurde während eines Ziehens geändert – jetzt als ein Schritt übernehmen.</summary>
    public Action<string>? Changed { get; set; }
    public Action<PaintLayer>? Stroked { get; set; }
    public Action<double, double, double, double>? PanelDrawn { get; set; }
    public Action<double, double>? BalloonAt { get; set; }
    public Action<(string Kind, string Id)?>? SelectionChanged { get; set; }
    public Action<ComicBalloon>? EditText { get; set; }
    public Func<double, double, ContextMenu?>? MenuAt { get; set; }

    enum Drag { None, Panel, Corner, Balloon, Resize, Tail, Crop, NewPanel, Stroke }
    Drag _drag; int _corner; Point _from; (double X, double Y) _start, _now; bool _moved;
    StrokeEngine? _stroke; Pixmap? _before, _comp; int _strokePanel = -1;
    static readonly IPen SelPen = new Pen(new SolidColorBrush(Color.Parse("#E0A030")), 2, new DashStyle([5, 3], 0));
    static readonly IBrush HandleFill = new SolidColorBrush(Color.Parse("#E0A030"));
    static readonly IPen HandlePen = new Pen(Brushes.Black, 1);

    public ComicPageView() { Focusable = true; ClipToBounds = true; DragDrop.SetAllowDrop(this, true); }

    public ComicPage? Page => Book is null || Book.Pages.Count == 0 ? null : Book.Pages[Math.Clamp(PageIndex, 0, Book.Pages.Count - 1)];
    public double Scale => Math.Max(0.05, Math.Min((Bounds.Width - 32) / ComicBook.PageW, (Bounds.Height - 32) / ComicBook.PageH));
    Point Origin => new((Bounds.Width - ComicBook.PageW * Scale) / 2, (Bounds.Height - ComicBook.PageH * Scale) / 2);
    public (double X, double Y) ToPage(Point p) { var o = Origin; return ((p.X - o.X) / Scale, (p.Y - o.Y) / Scale); }
    double Grab => 9 / Scale;

    public ComicPanel? SelPanel => Selected is ("panel", var id) ? Page?.Panels.FirstOrDefault(p => p.Id == id) : null;
    public ComicBalloon? SelBalloon => Selected is ("balloon", var id) ? Page?.Balloons.FirstOrDefault(b => b.Id == id) : null;

    public override void Render(DrawingContext ctx)
    {
        ctx.FillRectangle(new SolidColorBrush(Color.Parse("#0E1420")), new Rect(Bounds.Size));
        if (Book is null || Page is null) return;
        var o = Origin; var s = Scale;
        ctx.DrawRectangle(null, new Pen(new SolidColorBrush(Color.Parse("#66000000")), 8), new Rect(o.X - 2, o.Y - 2, ComicBook.PageW * s + 4, ComicBook.PageH * s + 4));
        using (ctx.PushTransform(Matrix.CreateScale(s, s) * Matrix.CreateTranslation(o.X, o.Y)))
        {
            ComicDraw.Page(ctx, Book, Math.Clamp(PageIndex, 0, Book.Pages.Count - 1), Image, ArtBmp, screen: true);
            var hp = new Pen(HandlePen.Brush, 1 / s); var sel = new Pen(SelPen.Brush, 2 / s, SelPen.DashStyle); var h = 5 / s;
            if (SelPanel is { } p)
            {
                ctx.DrawGeometry(null, sel, ComicDraw.Quad(p));
                for (var k = 0; k < 4; k++) ctx.DrawRectangle(HandleFill, hp, new Rect(p.P[k * 2] - h, p.P[k * 2 + 1] - h, 2 * h, 2 * h));
            }
            if (SelBalloon is { } b)
            {
                ctx.DrawRectangle(null, sel, new Rect(b.X, b.Y, b.W, b.H));
                ctx.DrawRectangle(HandleFill, hp, new Rect(b.X + b.W - h, b.Y + b.H - h, 2 * h, 2 * h));
                if (b.HasTail) ctx.DrawEllipse(HandleFill, hp, new Point(b.TailX, b.TailY), h * 1.2, h * 1.2);
            }
            if (_drag == Drag.NewPanel)
                ctx.DrawRectangle(null, sel, new Rect(new Point(_start.X, _start.Y), new Point(_now.X, _now.Y)));
        }
    }

    bool _selChanged;
    /// <summary>Auswahl ändern; gemeldet wird erst beim Loslassen, damit ein Neuaufbau das Ziehen nicht unterbricht.</summary>
    void Select((string Kind, string Id)? s) { if (Selected != s) { Selected = s; _selChanged = true; } InvalidateVisual(); }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e); Focus();
        if (Page is not { } page) return;
        var pt = e.GetCurrentPoint(this); var (x, y) = ToPage(pt.Position); _from = pt.Position; _start = _now = (x, y); _moved = false;
        if (pt.Properties.IsRightButtonPressed)
        {
            var hb = ComicBook.HitBalloon(page, x, y); var hp = ComicBook.HitPanel(page, x, y);
            if (hb >= 0) Select(("balloon", page.Balloons[hb].Id)); else if (hp >= 0) Select(("panel", page.Panels[hp].Id));
            if (MenuAt?.Invoke(x, y) is { } m) { m.Open(this); e.Handled = true; }
            return;
        }
        if (!pt.Properties.IsLeftButtonPressed) return;
        e.Pointer.Capture(this); e.Handled = true;
        switch (Tool)
        {
            case "pinsel" or "radierer": BeginStroke(page, x, y, e.Pointer.Type == PointerType.Pen ? pt.Properties.Pressure : 1); return;
            case "feld": _drag = Drag.NewPanel; return;
            case "blase": BalloonAt?.Invoke(x, y); return;
        }
        if (e.ClickCount == 2 && ComicBook.HitBalloon(page, x, y) is var dbl and >= 0) { Select(("balloon", page.Balloons[dbl].Id)); EditText?.Invoke(page.Balloons[dbl]); return; }
        // Griffe der Auswahl zuerst
        if (SelBalloon is { } sb)
        {
            if (sb.HasTail && Math.Abs(x - sb.TailX) <= Grab * 1.4 && Math.Abs(y - sb.TailY) <= Grab * 1.4) { _drag = Drag.Tail; return; }
            if (Math.Abs(x - (sb.X + sb.W)) <= Grab && Math.Abs(y - (sb.Y + sb.H)) <= Grab) { _drag = Drag.Resize; return; }
        }
        if (SelPanel is { } sp && sp.Corner(x, y, Grab) is var k and >= 0) { _drag = Drag.Corner; _corner = k; return; }
        var bi = ComicBook.HitBalloon(page, x, y);
        if (bi >= 0) { Select(("balloon", page.Balloons[bi].Id)); _drag = Drag.Balloon; return; }
        var pi = ComicBook.HitPanel(page, x, y);
        if (pi >= 0)
        {
            Select(("panel", page.Panels[pi].Id));
            _drag = e.KeyModifiers.HasFlag(KeyModifiers.Alt) && page.Panels[pi].Image.Length > 0 ? Drag.Crop : Drag.Panel; return;
        }
        Select(null); _drag = Drag.None;
    }

    void BeginStroke(ComicPage page, double x, double y, float pressure)
    {
        if (Paint is null) return;
        var layer = Paint.Layers.FirstOrDefault(l => l.Id == ActiveLayer) ?? Paint.Layers[^1];
        if (!layer.Visible || layer.Locked) return;
        _before = layer.Px.Clone(); _strokePanel = ComicBook.HitPanel(page, x, y);
        var brush = Tool == "radierer" ? BrushPreset.Get("radierer") : BrushPreset.Get(BrushId);
        _stroke = new StrokeEngine(layer.Px, brush, PaintColors.IsHex(BrushColor) ? BrushColor : "#111111", Tool == "radierer" ? Math.Max(BrushSize * 3, 12) : BrushSize);
        _drag = Drag.Stroke; StrokeTo(x, y, pressure);
    }

    void StrokeTo(double x, double y, float pressure)
    {
        if (_stroke is null) return;
        _stroke.Add(x, y, pressure > 0 ? pressure : 1);
        if (_stroke.TakeDirty() is { } r && Paint is not null)
        {
            if (ArtBmp is null) ArtBmp = ComicDraw.Art(Paint) ?? new WriteableBitmap(new PixelSize(Paint.W, Paint.H), new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Unpremul);
            if (_comp is null || _comp.W != Paint.W || _comp.H != Paint.H) _comp = new Pixmap(Paint.W, Paint.H);
            var comp = Paint.Composite(false, _comp, r.X0, r.Y0, r.X1 + 1, r.Y1 + 1);
            ComicDraw.Copy(ArtBmp, comp, r.X0, r.Y0, r.X1, r.Y1);
        }
        InvalidateVisual();
    }

    /// <summary>Strich auf das Bildfeld begrenzen, in dem er begonnen hat (außerhalb bleibt der Stand vor dem Strich).</summary>
    void ClipStroke(PaintLayer layer)
    {
        if (_before is null || _stroke is null || _strokePanel < 0 || Page is not { } page || _strokePanel >= page.Panels.Count) return;
        var q = page.Panels[_strokePanel].P;
        for (var py = Math.Max(0, _stroke.DirtyY0); py <= Math.Min(layer.Px.H - 1, _stroke.DirtyY1); py++)
            for (var px = Math.Max(0, _stroke.DirtyX0); px <= Math.Min(layer.Px.W - 1, _stroke.DirtyX1); px++)
                if (!ComicBook.InQuad(q, px + 0.5, py + 0.5)) { var i = (py * layer.Px.W + px) * 4; Buffer.BlockCopy(_before.Px, i, layer.Px.Px, i, 4); }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_drag == Drag.None || Page is not { } page) return;
        var pt = e.GetCurrentPoint(this); var (x, y) = ToPage(pt.Position); var (dx, dy) = (x - _now.X, y - _now.Y);
        if (Math.Abs(pt.Position.X - _from.X) + Math.Abs(pt.Position.Y - _from.Y) > 2) _moved = true;
        _now = (x, y);
        switch (_drag)
        {
            case Drag.Stroke: StrokeTo(x, y, e.Pointer.Type == PointerType.Pen ? pt.Properties.Pressure : 1); return;
            case Drag.Panel: SelPanel?.Move(dx, dy); break;
            case Drag.Corner: SelPanel?.MoveCorner(_corner, x, y); break;
            case Drag.Crop when SelPanel is { } cp: { var (_, _, w, h) = cp.Bounds(); cp.Ox += dx / w; cp.Oy += dy / h; break; }
            case Drag.Balloon: SelBalloon?.Move(dx, dy); break;
            case Drag.Resize when SelBalloon is { } rb: rb.Resize(x - rb.X, y - rb.Y); break;
            case Drag.Tail when SelBalloon is { } tb: (tb.TailX, tb.TailY) = ComicBook.Clamp(x, y); break;
        }
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e); var d = _drag; _drag = Drag.None; e.Pointer.Capture(null);
        if (_selChanged) { _selChanged = false; SelectionChanged?.Invoke(Selected); }
        switch (d)
        {
            case Drag.Stroke when Paint is not null:
            {
                var layer = Paint.Layers.FirstOrDefault(l => l.Id == ActiveLayer) ?? Paint.Layers[^1];
                ClipStroke(layer); _stroke = null; _before = null;
                Stroked?.Invoke(layer); break;
            }
            case Drag.NewPanel:
            {
                double x0 = Math.Min(_start.X, _now.X), y0 = Math.Min(_start.Y, _now.Y), w = Math.Abs(_now.X - _start.X), h = Math.Abs(_now.Y - _start.Y);
                if (w >= 30 && h >= 30) PanelDrawn?.Invoke(x0, y0, w, h);
                InvalidateVisual(); break;
            }
            case Drag.Panel or Drag.Corner or Drag.Crop or Drag.Balloon or Drag.Resize or Drag.Tail when _moved:
                Changed?.Invoke(d switch { Drag.Panel => "Bildfeld verschoben", Drag.Corner => "Bildfeld verformt", Drag.Crop => "Bildausschnitt verschoben", Drag.Balloon => "Blase verschoben", Drag.Resize => "Blasengröße geändert", _ => "Dorn gerichtet" });
                break;
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Alt) || SelPanel is not { Image.Length: > 0 } p) return;
        p.Zoom = Math.Clamp(p.Zoom * (e.Delta.Y > 0 ? 1.1 : 1 / 1.1), 1, 8); e.Handled = true; InvalidateVisual();
        Changed?.Invoke("Bildausschnitt gezoomt");
    }
}
