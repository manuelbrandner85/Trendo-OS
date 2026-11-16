using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Trendo.Core.Office;

namespace Trendo.App;

/// <summary>T-0692: Das Schreibfeld von Trendo Text – ein zusammenhängendes Steuerelement über das ganze Dokument. Es zeigt die Seiten des Seitensatzes
/// (<see cref="FlowDoc"/>, derselbe Umbruch wie im PDF), führt Cursor und Auswahl über Absatz- und Seitengrenzen, nimmt Tastatur und Maus an und ändert
/// das Dokument nur über <see cref="TextEdit"/>. Gezeichnet werden nur die sichtbaren Seiten.</summary>
public sealed class TextPageView : Control
{
    public const double Pad = 16, Gap = 18;
    /// <summary>Woher das Dokument kommt – das Schreibfeld hält keine eigene Kopie.</summary>
    public Func<TextDoc> DocSource { get; set; } = () => Fallback;
    static readonly TextDoc Fallback = TextDoc.Empty();
    TextDoc _doc => DocSource();
    FlowDoc? _flow; TextDoc? _flowOf;
    TextPos _anchor, _caret;
    double _wantX = double.NaN;
    double _zoom = 1;
    bool _drag; int _resizeBlock = -1, _resizeCol; double _resizeStartX; double[]? _resizeFrom; double _resizeTableW;
    readonly CaretLayer _caretLayer;
    readonly DispatcherTimer _blink = new() { Interval = TimeSpan.FromMilliseconds(530) };
    readonly Dictionary<FlowItem, FormattedText> _texts = new(ReferenceEqualityComparer.Instance);
    readonly System.Runtime.CompilerServices.ConditionalWeakTable<Picture, Bitmap> _bitmaps = [];
    ScrollViewer? _scroll;

    /// <summary>Zuletzt in Trendo kopierte Bausteine (mit Format) und ihr reiner Text – stimmt die Zwischenablage damit überein, wird mit Format eingefügt.</summary>
    public static List<Trendo.Core.Office.Block>? ClipBlocks { get; private set; }
    public static string ClipText { get; private set; } = "";

    /// <summary>Vor einer Änderung (Art: „tippen“, „loeschen“, „einfuegen“, „absatz“, „spalte“) – der Gastgeber merkt sich den Stand für Rückgängig.</summary>
    public event Action<string>? Changing;
    public event Action? Changed;
    /// <summary>Cursor oder Auswahl haben sich bewegt.</summary>
    public event Action? Moved;
    /// <summary>Tastenkürzel, die der Gastgeber ausführt (Strg+Z/Y/F/S/B/I/U, Tab in Listen). Liefert true, wenn erledigt.</summary>
    public Func<Key, KeyModifiers, bool>? Shortcut;
    /// <summary>Rechtsklick: der Gastgeber setzt das passende Menü.</summary>
    public event Action? MenuWanted;
    /// <summary>Rot unterstrichene Stellen (Rechtschreibung): Baustein, Anfang, Länge.</summary>
    public Func<IEnumerable<(int Block, int Start, int Length)>>? Mistakes;
    /// <summary>Zeichenformate, die für das nächste Tippen vorgemerkt sind (Fett an ohne Auswahl): ein- bzw. auszuschalten.</summary>
    public Fmt PendingOn { get; set; }
    public Fmt PendingOff { get; set; }

    public TextPageView()
    {
        Focusable = true; ClipToBounds = true; Cursor = new Cursor(StandardCursorType.Ibeam);
        _caretLayer = new CaretLayer(this) { IsHitTestVisible = false };
        VisualChildren.Add(_caretLayer); LogicalChildren.Add(_caretLayer);
        _blink.Tick += (_, _) => { _caretLayer.On = !_caretLayer.On; _caretLayer.InvalidateVisual(); };
        Avalonia.Automation.AutomationProperties.SetName(this, "Dokument bearbeiten");
    }

    // ---------- Zustand ----------
    public TextDoc Document => _doc;
    public FlowDoc Flow { get { var d = _doc; if (_flow is null || !ReferenceEquals(_flowOf, d)) { _flow = FlowDoc.Build(d); _flowOf = d; _texts.Clear(); } return _flow; } }
    public TextPos Caret => _caret;
    public TextPos Anchor => _anchor;
    public bool HasSelection => _anchor != _caret;
    public (TextPos A, TextPos B) Range => TextEdit.Order(_anchor, _caret);
    public double Scale => 96.0 / 72 * _zoom;
    public double Zoom { get => _zoom; set { if (Math.Abs(_zoom - value) < 0.001) return; _zoom = Math.Clamp(value, 0.25, 4); InvalidateMeasure(); InvalidateVisual(); } }
    public int PageCount => Flow.Pages.Count;
    /// <summary>Seite (ab 1), auf der der Cursor steht.</summary>
    public int CaretPage => (Flow.Caret(_caret)?.Page ?? Flow.Boxes.FirstOrDefault(b => b.Block == _caret.Block)?.Page ?? 0) + 1;
    public Size PageSize => new(Flow.PageW * Scale, Flow.PageH * Scale);
    public bool IsEmptyDocument => _doc.Blocks.Count == 1 && _doc.Blocks[0] is Para { Text.Length: 0 };

    /// <summary>Cursor und Auswahl wieder gültig machen (das Dokument wurde von außen geändert).</summary>
    public void ClampSelection()
    {
        _anchor = TextEdit.Clamp(_doc, _anchor); _caret = TextEdit.Clamp(_doc, _caret);
        if (!Valid(_anchor, _caret)) _anchor = _caret;
    }

    /// <summary>Auswahl setzen, ohne zu rollen und ohne Meldung an den Gastgeber.</summary>
    public void SetQuiet(TextPos anchor, TextPos caret)
    {
        _anchor = TextEdit.Clamp(_doc, anchor); _caret = TextEdit.Clamp(_doc, caret);
        if (!Valid(_anchor, _caret)) _anchor = _caret;
        _wantX = double.NaN; PendingOn = PendingOff = Fmt.None;
        InvalidateVisual(); _caretLayer.InvalidateVisual();
    }

    /// <summary>Seitensatz verwerfen und neu zeichnen (das Dokument wurde geändert).</summary>
    public void Refresh() { _flow = null; _texts.Clear(); InvalidateMeasure(); InvalidateVisual(); _caretLayer.InvalidateVisual(); }

    static bool Valid(TextPos a, TextPos b) => a.SameBox(b) || !a.InCell && !b.InCell;

    public void Select(TextPos anchor, TextPos caret, bool reveal = true)
    {
        _anchor = TextEdit.Clamp(_doc, anchor); _caret = TextEdit.Clamp(_doc, caret);
        if (!Valid(_anchor, _caret)) _anchor = _caret;
        _wantX = double.NaN; PendingOn = PendingOff = Fmt.None;
        AfterMove(reveal);
    }

    void AfterMove(bool reveal = true)
    {
        _caretLayer.On = true; InvalidateVisual(); _caretLayer.InvalidateVisual();
        if (reveal) Reveal();
        Moved?.Invoke();
    }

    /// <summary>Listennummer bzw. -zeichen, das vor dem Absatz auf der Seite steht (leer ohne Liste).</summary>
    public string ListLabel(int block)
    {
        var line = Flow.LinesOf(new TextPos(block, 0)).FirstOrDefault(); if (line is null) return "";
        return Flow.Pages[line.Page].Items.OfType<FlowText>().Where(t => t.Y >= line.Y && t.Y <= line.Y + line.H && t.X < line.Left).Select(t => t.Text).FirstOrDefault() ?? "";
    }

    /// <summary>Text der Auswahl (ohne Format).</summary>
    public string SelectedText() => HasSelection ? TextEdit.Plain(TextEdit.Copy(_doc, _anchor, _caret)) : "";

    // ---------- Maße ----------
    double PageWpx => Flow.PageW * Scale;
    double PageHpx => Flow.PageH * Scale;
    double PageLeft => Math.Max(Pad, (Bounds.Width - PageWpx) / 2);
    double PageTop(int page) => Pad + page * (PageHpx + Gap);

    protected override Size MeasureOverride(Size availableSize)
    {
        _caretLayer.Measure(availableSize);
        return new Size(PageWpx + 2 * Pad, Pad + Flow.Pages.Count * (PageHpx + Gap));
    }

    protected override Size ArrangeOverride(Size finalSize) { _caretLayer.Arrange(new Rect(finalSize)); return finalSize; }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _scroll = this.FindAncestorOfTypeSafe();
        if (_scroll is not null) _scroll.ScrollChanged += OnScroll;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_scroll is not null) _scroll.ScrollChanged -= OnScroll;
        _scroll = null; _blink.Stop();
        base.OnDetachedFromVisualTree(e);
    }

    void OnScroll(object? s, ScrollChangedEventArgs e) => InvalidateVisual();

    protected override void OnGotFocus(FocusChangedEventArgs e) { base.OnGotFocus(e); _caretLayer.On = true; _blink.Start(); _caretLayer.InvalidateVisual(); }
    protected override void OnLostFocus(FocusChangedEventArgs e) { base.OnLostFocus(e); _blink.Stop(); _caretLayer.On = false; _caretLayer.InvalidateVisual(); }

    /// <summary>Punkt des Steuerelements → (Seite, x, y) in Punkt auf der Seite.</summary>
    (int Page, double X, double Y) ToPage(Point p)
    {
        var page = Math.Clamp((int)Math.Floor((p.Y - Pad + Gap / 2) / (PageHpx + Gap)), 0, Math.Max(0, Flow.Pages.Count - 1));
        return (page, (p.X - PageLeft) / Scale, (p.Y - PageTop(page)) / Scale);
    }

    Rect ToView(int page, double x, double y, double w, double h) => new(PageLeft + x * Scale, PageTop(page) + y * Scale, w * Scale, h * Scale);

    /// <summary>Stelle unter einem Punkt des Steuerelements.</summary>
    public TextPos? PosAt(Point p) { var (page, x, y) = ToPage(p); return Flow.Hit(page, x, y); }

    /// <summary>Rechteck des Cursors im Steuerelement (für Tests und zum Hineinrollen).</summary>
    public Rect? CaretRect(TextPos pos)
    {
        if (Flow.Caret(pos) is { } c) return ToView(c.Page, c.X, c.Y, 0.8, c.H);
        return Flow.Boxes.FirstOrDefault(b => b.Block == pos.Block && b.Kind is FlowBoxKind.Picture or FlowBoxKind.Break or FlowBoxKind.Toc) is { } box ? ToView(box.Page, box.X, box.Y, box.W, box.H) : null;
    }

    /// <summary>Mitte eines Zeichens im Steuerelement – für Mausklicks in Tests.</summary>
    public Point PointOf(TextPos pos) { var r = CaretRect(pos) ?? default; return new Point(r.X, r.Y + r.Height / 2); }

    void Reveal()
    {
        if (CaretRect(_caret) is { } r && _scroll is not null) this.BringIntoView(r.Inflate(new Thickness(24, 30)));
    }

    // ---------- Zeichnen ----------
    static readonly IBrush Paper = Brushes.White, Sel = new SolidColorBrush(Color.Parse("#66C9A227")), Hint = new SolidColorBrush(Color.Parse("#9AA5B1"));
    static readonly IPen Edge = new Pen(new SolidColorBrush(Color.Parse("#33000000")), 1), Squiggle = new Pen(Brushes.Red, 1.2), BreakPen = new Pen(new SolidColorBrush(Color.Parse("#9AA5B1")), 1, new DashStyle([4, 3], 0)), ObjPen = new Pen(new SolidColorBrush(Color.Parse("#C9A227")), 1.5);
    static IBrush B(uint argb) => new SolidColorBrush(Color.FromUInt32(argb));
    static readonly Dictionary<uint, IBrush> Brs = [];
    static IBrush Br(uint argb) => Brs.TryGetValue(argb, out var b) ? b : Brs[argb] = B(argb);

    FormattedText Ft(FlowItem key, string text, FlowFont f, uint color)
    {
        if (_texts.TryGetValue(key, out var ft)) return ft;
        var tf = new Typeface(new FontFamily(f.Family), f.Italic ? FontStyle.Italic : FontStyle.Normal, f.Bold ? FontWeight.Bold : FontWeight.Normal);
        return _texts[key] = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, tf, f.Size, Br(color));
    }

    public override void Render(DrawingContext ctx)
    {
        var flow = Flow;
        double top = 0, bottom = double.MaxValue;
        if (_scroll is not null && _scroll.Viewport.Height > 0) { top = _scroll.Offset.Y - 40; bottom = _scroll.Offset.Y + _scroll.Viewport.Height + 40; }
        var (selA, selB) = Range;
        var mistakes = Mistakes?.Invoke().ToList() ?? [];
        for (var i = 0; i < flow.Pages.Count; i++)
        {
            var py = PageTop(i);
            if (py + PageHpx < top || py > bottom) continue; // nur sichtbare Seiten
            var rect = new Rect(PageLeft, py, PageWpx, PageHpx);
            ctx.FillRectangle(Br(0x33000000), rect.Translate(new Vector(2, 3)));
            ctx.DrawRectangle(Paper, Edge, rect);
            using (ctx.PushTransform(Matrix.CreateScale(Scale, Scale) * Matrix.CreateTranslation(PageLeft, py)))
            {
                DrawSelection(ctx, flow, i, selA, selB);
                foreach (var item in flow.Pages[i].Items) DrawItem(ctx, flow, item);
                foreach (var b in flow.Boxes)
                {
                    if (b.Page != i) continue;
                    if (b.Kind == FlowBoxKind.Break)
                    {
                        ctx.DrawLine(BreakPen, new Point(b.X, b.Y + 7), new Point(b.X + b.W, b.Y + 7));
                        var ft = new FormattedText("Seitenumbruch", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface("Arial"), 7.5, Hint);
                        ctx.FillRectangle(Paper, new Rect(b.X + b.W / 2 - 34, b.Y + 1, 68, 12)); ctx.DrawText(ft, new Point(b.X + b.W / 2 - 26, b.Y + 2));
                    }
                    if (!HasSelection && b.Block == _caret.Block && b.Kind is FlowBoxKind.Picture or FlowBoxKind.Break or FlowBoxKind.Toc && !_caret.InCell && _doc.Blocks.ElementAtOrDefault(b.Block) is not (Para or Table))
                        ctx.DrawRectangle(null, ObjPen, new Rect(b.X - 2, b.Y - 2, b.W + 4, Math.Min(b.H, b.Kind == FlowBoxKind.Break ? 14 : b.H) + 4));
                }
                foreach (var (block, start, length) in mistakes)
                    foreach (var r in RangeRects(flow, new TextPos(block, start), new TextPos(block, start + length)))
                        if (r.Page == i) ctx.DrawLine(Squiggle, new Point(r.X, r.Y + r.H * 0.9), new Point(r.X + r.W, r.Y + r.H * 0.9));
                if (i == 0 && IsEmptyDocument && flow.Lines.Count > 0)
                {
                    var l = flow.Lines[0];
                    var ft = new FormattedText("Hier schreiben …", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface("Arial"), 11, Hint);
                    ctx.DrawText(ft, new Point(l.X + 2, l.Base - ft.Baseline));
                }
            }
        }
    }

    void DrawItem(DrawingContext ctx, FlowDoc flow, FlowItem item)
    {
        switch (item)
        {
            case FlowFill f: ctx.FillRectangle(Br(f.Color), new Rect(f.X, f.Y, f.W, f.H)); break;
            case FlowStroke s: ctx.DrawRectangle(null, new Pen(Br(s.Color), s.Width), new Rect(s.X, s.Y, s.W, s.H)); break;
            case FlowRule r: ctx.DrawLine(new Pen(Br(r.Color), r.Width), new Point(r.X1, r.Y1), new Point(r.X2, r.Y2)); break;
            case FlowText t:
                if (t.Text.Length > 0) { var ft = Ft(t, t.Text, t.Font, t.Color); ctx.DrawText(ft, new Point(t.X, t.Y - ft.Baseline)); }
                if (t.Underline) ctx.DrawLine(new Pen(Br(t.Pen), 0.6), new Point(t.X, t.Y + 1.5), new Point(t.X + t.W, t.Y + 1.5));
                if (t.Strike) ctx.DrawLine(new Pen(Br(t.Pen), 0.6), new Point(t.X, t.Y - t.Font.Size * 0.3), new Point(t.X + t.W, t.Y - t.Font.Size * 0.3));
                break;
            case FlowAligned a:
                var aw = FlowMeasure.Width(a.Text, a.Font); var ax = a.Align switch { 1 => a.X + (a.W - aw) / 2, 2 => a.X + a.W - aw, _ => a.X };
                ctx.DrawText(Ft(a, a.Text, a.Font, a.Color), new Point(ax, a.Y));
                break;
            case FlowImage im:
                if (BitmapOf(im.Pic) is { } bmp) ctx.DrawImage(bmp, new Rect(im.X, im.Y, im.W, im.H));
                else ctx.DrawRectangle(Br(0xFFF1F3F5), new Pen(Br(0xFF9AA5B1), 0.5), new Rect(im.X, im.Y, im.W, im.H));
                break;
            case FlowComment c:
                ctx.DrawRectangle(Br(0xFFFFE680), new Pen(Br(0xFFB45309), 0.6), new Rect(c.X, c.Y + 2, 10, 8), 2, 2);
                break;
            case FlowWatermark wm:
                using (ctx.PushTransform(Matrix.CreateRotation(-Math.PI / 4) * Matrix.CreateTranslation(flow.PageW / 2, flow.PageH / 2)))
                {
                    var ft = Ft(wm, wm.Text, new FlowFont(wm.Family, wm.Size, true, false), 0x30808080);
                    var w = FlowMeasure.Width(wm.Text, new FlowFont(wm.Family, wm.Size, true, false));
                    ctx.DrawText(ft, new Point(-w / 2, -wm.Size * 0.6));
                }
                break;
        }
    }

    Bitmap? BitmapOf(Picture pic)
    {
        if (_bitmaps.TryGetValue(pic, out var b)) return b;
        try { using var ms = new MemoryStream(pic.Data); b = new Bitmap(ms); _bitmaps.Add(pic, b); return b; }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException) { return null; }
    }

    /// <summary>Rechtecke (in Punkt, je Seite) für einen Bereich – Auswahl und rote Linien.</summary>
    public static List<(int Page, double X, double Y, double W, double H)> RangeRects(FlowDoc flow, TextPos a, TextPos b)
    {
        var res = new List<(int, double, double, double, double)>();
        (a, b) = TextEdit.Order(a, b); if (a == b) return res;
        var same = a.SameBox(b);
        foreach (var l in flow.Lines)
        {
            int s, e;
            if (same) { if (!l.Box.SameBox(a)) continue; s = a.Offset; e = b.Offset; }
            else
            {
                if (l.Block < a.Block || l.Block > b.Block) continue;
                s = l.Block == a.Block && l.Row < 0 ? a.Offset : 0; e = l.Block == b.Block && l.Row < 0 ? b.Offset : int.MaxValue; // Tabellen dazwischen: ganz
            }
            var from = Math.Max(s, l.Start); var to = Math.Min(e, l.End);
            if (to < from || s > l.End || e < l.Start) continue;
            var x1 = flow.XAt(l, from); var x2 = flow.XAt(l, to);
            var past = e > l.End; // Zeilen-/Absatzende mitmarkiert: ein Stück weiter
            if (past && to == l.End) x2 += 4;
            if (x2 - x1 < 0.01 && !past) continue;
            res.Add((l.Page, x1, l.Y, Math.Max(2, x2 - x1), l.H));
        }
        return res;
    }

    void DrawSelection(DrawingContext ctx, FlowDoc flow, int page, TextPos a, TextPos b)
    {
        if (a == b) return;
        foreach (var r in RangeRects(flow, a, b)) if (r.Page == page) ctx.FillRectangle(Sel, new Rect(r.X, r.Y, r.W, r.H));
        if (!a.SameBox(b))
            foreach (var box in flow.Boxes) if (box.Page == page && box.Block > a.Block && box.Block < b.Block && box.Kind is FlowBoxKind.Picture or FlowBoxKind.Toc) ctx.FillRectangle(Sel, new Rect(box.X, box.Y, box.W, box.H));
    }

    sealed class CaretLayer(TextPageView owner) : Control
    {
        public bool On;
        public override void Render(DrawingContext ctx)
        {
            if (!On || !owner.IsFocused || TextEdit.IsObject(owner._doc, owner._caret)) return;
            if (owner.CaretRect(owner._caret) is { } r) ctx.FillRectangle(Brushes.Black, new Rect(r.X, r.Y + r.Height * 0.1, Math.Max(1.2, owner.Scale), r.Height * 0.86));
        }
    }

    // ---------- Maus ----------
    FlowBox? ColumnEdge(Point p)
    {
        var (page, x, y) = ToPage(p);
        return Flow.Boxes.FirstOrDefault(b => b.Kind == FlowBoxKind.Cell && b.Page == page && y >= b.Y && y <= b.Y + b.H && Math.Abs(x - (b.X + b.W)) < 2.5 && _doc.Blocks[b.Block] is Table t && b.Col + t.SpanOf(b.Row, b.Col).ColSpan < t.Cols);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        var pt = e.GetPosition(this); var props = e.GetCurrentPoint(this).Properties;
        if (props.IsLeftButtonPressed && ColumnEdge(pt) is { } edge && _doc.Blocks[edge.Block] is Table tbl)
        {
            _resizeBlock = edge.Block; _resizeCol = edge.Col + tbl.SpanOf(edge.Row, edge.Col).ColSpan - 1; _resizeStartX = pt.X; _resizeFrom = tbl.Fractions();
            _resizeTableW = Flow.Boxes.Where(b => b.Kind == FlowBoxKind.Cell && b.Block == edge.Block && b.Row == edge.Row).Sum(b => b.W) * Scale;
            Changing?.Invoke("spalte"); e.Pointer.Capture(this); e.Handled = true; return;
        }
        if (PosAt(pt) is not { } pos) return;
        var (page, x, y) = ToPage(pt);
        if (props.IsLeftButtonPressed && Flow.Boxes.FirstOrDefault(b => b.Kind == FlowBoxKind.TocEntry && b.Page == page && x >= b.X && x <= b.X + b.W && y >= b.Y && y <= b.Y + b.H) is { Target: >= 0 } entry && e.ClickCount == 2)
        { Select(new TextPos(entry.Target, 0), new TextPos(entry.Target, 0)); e.Handled = true; return; } // Doppelklick im Verzeichnis springt zur Überschrift
        if (props.IsRightButtonPressed)
        {
            var (a, b) = Range;
            if (!HasSelection || pos < a || pos > b) Select(pos, pos, false);
            MenuWanted?.Invoke(); return;
        }
        if (!props.IsLeftButtonPressed) return;
        if (e.ClickCount == 2 && !TextEdit.IsObject(_doc, pos)) { var (a, b) = TextEdit.WordAt(_doc, pos); Select(a, b, false); }
        else if (e.ClickCount >= 3 && !TextEdit.IsObject(_doc, pos)) Select(pos with { Offset = 0 }, pos with { Offset = TextEdit.Length(_doc, pos) }, false);
        else if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) Select(_anchor, Snap(_anchor, pos), false);
        else { Select(pos, pos, false); _drag = true; e.Pointer.Capture(this); }
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var pt = e.GetPosition(this);
        if (_resizeBlock >= 0 && _resizeFrom is { } from && _doc.Blocks.ElementAtOrDefault(_resizeBlock) is Table t && _resizeCol + 1 < from.Length)
        {
            var d = (pt.X - _resizeStartX) / Math.Max(1, _resizeTableW); var w = (double[])from.Clone();
            d = Math.Clamp(d, 0.05 - w[_resizeCol], w[_resizeCol + 1] - 0.05);
            w[_resizeCol] += d; w[_resizeCol + 1] -= d;
            t.SetWidths(w); Refresh(); Changed?.Invoke();
            return;
        }
        if (_drag) { if (PosAt(pt) is { } pos) { var c = Snap(_anchor, pos); if (c != _caret) { _caret = c; AfterMove(); } } return; }
        Cursor = ColumnEdge(pt) is not null ? new Cursor(StandardCursorType.SizeWestEast) : new Cursor(StandardCursorType.Ibeam);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_drag || _resizeBlock >= 0) e.Pointer.Capture(null);
        _drag = false; _resizeBlock = -1; _resizeFrom = null;
    }

    /// <summary>Ende einer Auswahl so setzen, dass sie gültig bleibt: in einer Zelle begonnen bleibt sie in der Zelle; aus einem Absatz heraus springt das Ende über Tabellen und Bilder zum nächsten Absatz.</summary>
    TextPos Snap(TextPos anchor, TextPos pos)
    {
        pos = TextEdit.Clamp(_doc, pos);
        if (pos.SameBox(anchor)) return pos;
        if (anchor.InCell) return anchor with { Offset = pos > anchor ? TextEdit.Length(_doc, anchor) : 0 };
        if (TextEdit.IsObject(_doc, anchor)) return pos.InCell || TextEdit.IsObject(_doc, pos) ? anchor : pos;
        if (!pos.InCell && !TextEdit.IsObject(_doc, pos)) return pos;
        var dir = pos > anchor ? 1 : -1;
        for (var b = pos.Block + dir; b >= 0 && b < _doc.Blocks.Count; b += dir) if (_doc.Blocks[b] is Para p) return new TextPos(b, dir > 0 ? 0 : p.Text.Length);
        for (var b = pos.Block - dir; b >= 0 && b < _doc.Blocks.Count && b != anchor.Block - dir; b -= dir) if (_doc.Blocks[b] is Para p) return new TextPos(b, dir > 0 ? p.Text.Length : 0);
        return anchor;
    }

    // ---------- Tastatur ----------
    void Move(TextPos to, bool extend, bool keepX = false)
    {
        var x = _wantX;
        if (extend) { _caret = Snap(_anchor, to); PendingOn = PendingOff = Fmt.None; }
        else { _caret = _anchor = TextEdit.Clamp(_doc, to); PendingOn = PendingOff = Fmt.None; }
        _wantX = keepX ? x : double.NaN;
        AfterMove();
    }

    TextPos LineEdge(TextPos p, bool end)
    {
        if (Flow.LineAt(p) is not { } l) return p;
        if (!end) return p with { Offset = l.Start };
        var ls = Flow.LinesOf(l.Box); var lastLine = ReferenceEquals(ls[^1], l);
        var text = TextEdit.ParaAt(_doc, p)?.Text ?? "";
        return p with { Offset = lastLine || l.End <= l.Start || l.End > text.Length || text[l.End - 1] != ' ' ? Math.Min(l.End, text.Length) : l.End - 1 };
    }

    TextPos Vertical(TextPos p, int dir, double? jump = null)
    {
        if (Flow.Caret(p) is not { } c) return TextEdit.Step(_doc, p, dir);
        if (double.IsNaN(_wantX)) _wantX = c.X;
        if (jump is { } dy)
        {
            // Bild auf/ab: dieselbe Stelle eine Fensterhöhe weiter
            var view = ToView(c.Page, _wantX, c.Y + c.H / 2, 0, 0);
            var target = new Point(view.X, Math.Clamp(view.Y + dir * dy, Pad + 1, Math.Max(Pad + 1, Bounds.Height - Pad)));
            return PosAt(target) ?? p;
        }
        return Flow.Vertical(p, dir, _wantX) ?? (dir < 0 ? p with { Offset = 0 } : p with { Offset = TextEdit.Length(_doc, p) });
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled) return;
        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta); var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var obj = TextEdit.IsObject(_doc, _caret);
        e.Handled = true;
        switch (e.Key)
        {
            case Key.Left: if (!shift && HasSelection && !ctrl) Move(Range.A, false); else Move(ctrl ? TextEdit.WordStep(_doc, _caret, -1) : TextEdit.Step(_doc, _caret, -1), shift); break;
            case Key.Right: if (!shift && HasSelection && !ctrl) Move(Range.B, false); else Move(ctrl ? TextEdit.WordStep(_doc, _caret, 1) : TextEdit.Step(_doc, _caret, 1), shift); break;
            case Key.Up: Move(Vertical(_caret, -1), shift, true); break;
            case Key.Down: Move(Vertical(_caret, 1), shift, true); break;
            case Key.PageUp: Move(Vertical(_caret, -1, Math.Max(100, _scroll?.Viewport.Height ?? 600) - 40), shift, true); break;
            case Key.PageDown: Move(Vertical(_caret, 1, Math.Max(100, _scroll?.Viewport.Height ?? 600) - 40), shift, true); break;
            case Key.Home: Move(ctrl ? TextEdit.DocStart(_doc) : LineEdge(_caret, false), shift); break;
            case Key.End: Move(ctrl ? TextEdit.DocEnd(_doc) : LineEdge(_caret, true), shift); break;
            case Key.A when ctrl: SelectAll(); break;
            case Key.C when ctrl: Copy(); break;
            case Key.X when ctrl: Cut(); break;
            case Key.V when ctrl: Paste(); break;
            case Key.Insert when ctrl: Copy(); break;
            case Key.Insert when shift: Paste(); break;
            case Key.Delete when shift: Cut(); break;
            case Key.Back: DeleteKey(false, ctrl); break;
            case Key.Delete: DeleteKey(true, ctrl); break;
            case Key.Enter:
                if (Shortcut?.Invoke(e.Key, e.KeyModifiers) == true) break;
                if (ctrl && !_caret.InCell && !obj) { InsertBlockAfterCaret(new PageBreak()); break; }
                if (obj) { Changing?.Invoke("absatz"); _doc.Blocks.Insert(_caret.Block + 1, new Para()); Done(new TextPos(_caret.Block + 1, 0)); break; }
                if (shift || _caret.InCell) { Type("\n", "absatz"); break; }
                Changing?.Invoke("absatz");
                var at = HasSelection ? TextEdit.Delete(_doc, _anchor, _caret) : _caret;
                var wasEmptyListItem = _doc.Blocks[at.Block] is Para { List: not ListKind.None, Text.Length: 0 };
                _doc.SplitAt(at.Block, at.Offset);
                if (wasEmptyListItem) { _doc.Blocks.RemoveAt(at.Block + 1); Done(at); } // leerer Listenpunkt + Enter beendet die Liste
                else Done(new TextPos(at.Block + 1, 0));
                break;
            case Key.Tab:
                if (Shortcut?.Invoke(e.Key, e.KeyModifiers) == true) break;
                if (_caret.InCell) { Changing?.Invoke("absatz"); var n = TextEdit.NextCell(_doc, _caret, shift ? -1 : 1); Refresh(); Changed?.Invoke(); Select(n with { Offset = 0 }, n); break; }
                if (!shift && !obj) Type("\t", "tippen"); else e.Handled = false;
                break;
            case Key.Escape: if (HasSelection) Move(_caret, false); else e.Handled = false; break;
            default:
                e.Handled = ctrl && Shortcut?.Invoke(e.Key, e.KeyModifiers) == true;
                break;
        }
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (e.Handled || string.IsNullOrEmpty(e.Text)) return;
        var text = new string(e.Text.Where(c => !char.IsControl(c)).ToArray());
        if (text.Length == 0) return;
        Type(text, "tippen"); e.Handled = true;
    }

    void Done(TextPos caret)
    {
        Refresh(); _anchor = _caret = TextEdit.Clamp(_doc, caret); _wantX = double.NaN;
        Changed?.Invoke(); AfterMove();
    }

    /// <summary>Text an der Cursorstelle eintippen (ersetzt die Auswahl) – wie über die Tastatur.</summary>
    public void Type(string text, string kind = "tippen")
    {
        if (TextEdit.IsObject(_doc, _caret) && !HasSelection) return;
        Changing?.Invoke(kind);
        var at = HasSelection ? TextEdit.Delete(_doc, _anchor, _caret) : _caret;
        var end = TextEdit.InsertText(_doc, at, text);
        if ((PendingOn | PendingOff) != Fmt.None && end.SameBox(at) && TextEdit.ParaAt(_doc, at) is { } p)
        {
            if (PendingOn != Fmt.None) TextDoc.Format(p, at.Offset, end.Offset - at.Offset, PendingOn, true);
            if (PendingOff != Fmt.None) TextDoc.Format(p, at.Offset, end.Offset - at.Offset, PendingOff, false);
            TextEdit.Commit(_doc, at, p);
        }
        var (on, off) = (PendingOn, PendingOff);
        Done(end);
        (PendingOn, PendingOff) = (on, off); // vorgemerktes Format gilt weiter, bis der Cursor wandert
    }

    void DeleteKey(bool forward, bool word)
    {
        if (TextEdit.IsObject(_doc, _caret) && !HasSelection)
        {
            Changing?.Invoke("loeschen");
            _doc.Blocks.RemoveAt(_caret.Block); if (_doc.Blocks.Count == 0) _doc.Blocks.Add(new Para());
            Done(TextEdit.Clamp(_doc, new TextPos(Math.Max(0, _caret.Block - (forward ? 0 : 1)), int.MaxValue)));
            return;
        }
        Changing?.Invoke(HasSelection || word ? "loeschen" : forward ? "entf" : "rueck");
        TextPos at;
        if (HasSelection) at = TextEdit.Delete(_doc, _anchor, _caret);
        else if (word) { var to = TextEdit.WordStep(_doc, _caret, forward ? 1 : -1); at = to.SameBox(_caret) ? TextEdit.Delete(_doc, _caret, to) : forward ? TextEdit.DeleteForward(_doc, _caret) : TextEdit.Backspace(_doc, _caret); }
        else at = forward ? TextEdit.DeleteForward(_doc, _caret) : TextEdit.Backspace(_doc, _caret);
        Done(at);
    }

    void InsertBlockAfterCaret(Trendo.Core.Office.Block b)
    {
        Changing?.Invoke("absatz");
        var at = HasSelection ? TextEdit.Delete(_doc, _anchor, _caret) : _caret;
        _doc.SplitAt(at.Block, at.Offset); _doc.Blocks.Insert(at.Block + 1, b);
        Done(new TextPos(at.Block + 2, 0));
    }

    /// <summary>Alles markieren: in einer Tabellenzelle die Zelle, sonst das ganze Dokument.</summary>
    public void SelectAll()
    {
        if (_caret.InCell) { Select(_caret with { Offset = 0 }, _caret with { Offset = TextEdit.Length(_doc, _caret) }, false); return; }
        var first = Enumerable.Range(0, _doc.Blocks.Count).FirstOrDefault(i => _doc.Blocks[i] is Para, -1); var last = Enumerable.Range(0, _doc.Blocks.Count).LastOrDefault(i => _doc.Blocks[i] is Para, -1);
        if (first < 0) return;
        Select(new TextPos(first, 0), new TextPos(last, ((Para)_doc.Blocks[last]).Text.Length), false);
    }

    // ---------- Zwischenablage ----------
    Avalonia.Input.Platform.IClipboard? Clip => TopLevel.GetTopLevel(this)?.Clipboard;

    public void Copy()
    {
        if (!HasSelection) return;
        ClipBlocks = TextEdit.Copy(_doc, _anchor, _caret); ClipText = TextEdit.Plain(ClipBlocks);
        try { _ = Clip?.SetTextAsync(ClipText.Replace("\n", Environment.NewLine)); } catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException) { }
    }

    public void Cut()
    {
        if (!HasSelection) return;
        Copy(); Changing?.Invoke("loeschen");
        Done(TextEdit.Delete(_doc, _anchor, _caret));
    }

    /// <summary>Einfügen: stammt der Text aus Trendo Text, mit Format – sonst als reiner Text.</summary>
    public async void Paste()
    {
        string? text = null;
        try { if (Clip is { } cb) text = await cb.TryGetTextAsync(); } catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException or TimeoutException) { }
        PasteText(text);
    }

    /// <summary>Einfügen mit dem Text der Zwischenablage (null = nur Trendos eigener Zwischenspeicher).</summary>
    public void PasteText(string? clipboardText)
    {
        static string N(string s) => s.Replace("\r\n", "\n").Replace('\r', '\n');
        var own = ClipBlocks is { Count: > 0 } && (clipboardText is null || N(clipboardText) == ClipText);
        if (!own && string.IsNullOrEmpty(clipboardText)) return;
        if (TextEdit.IsObject(_doc, _caret) && !HasSelection) return;
        Changing?.Invoke("einfuegen");
        var at = HasSelection ? TextEdit.Delete(_doc, _anchor, _caret) : _caret;
        Done(own ? TextEdit.Paste(_doc, at, ClipBlocks!) : TextEdit.InsertText(_doc, at, N(clipboardText!)));
    }
}

static class TextPageViewExtensions
{
    public static ScrollViewer? FindAncestorOfTypeSafe(this Visual v)
    {
        for (var p = Avalonia.VisualTree.VisualExtensions.GetVisualParent(v); p is not null; p = Avalonia.VisualTree.VisualExtensions.GetVisualParent(p)) if (p is ScrollViewer s) return s;
        return null;
    }
}
