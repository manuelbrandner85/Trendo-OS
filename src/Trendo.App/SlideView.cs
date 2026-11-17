using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Trendo.Core.Office;

namespace Trendo.App;

/// <summary>T-0229: zeichnet eine Folie (16:9, mittig eingepasst). Im Bearbeiten-Modus: Objekt anklicken, verschieben, Größe am Griff ändern,
/// Doppelklick bearbeitet den Text, Pfeiltasten schieben, Entf löscht. In der Vorführung blendet <see cref="Revealed"/> Animationen schrittweise ein.</summary>
public sealed class SlideView : Control
{
    public SlideDoc Doc { get; set; } = SlideDoc.Empty();
    public Slide Slide { get; set; } = new();
    public bool Editable { get; set; }
    /// <summary>Ausgewähltes Objekt (Index in <see cref="Slide.Shapes"/>), -1 = keins.</summary>
    public int Selected { get; private set; } = -1;
    /// <summary>Vorführung: so viele animierte Objekte sind schon eingeblendet; null = alle zeigen.</summary>
    public int? Revealed { get; set; }
    /// <summary>Vorführung: Fortschritt der gerade laufenden Animation (0–1) für das zuletzt eingeblendete Objekt.</summary>
    public double AnimProgress { get; set; } = 1;
    /// <summary>Laserpointer-Punkt in Folienkoordinaten (cm), null = aus.</summary>
    public Point? Laser { get; set; }

    public event Action? SelectionChanged;
    /// <summary>Gleich ändert sich etwas (für Rückgängig).</summary>
    public event Action? Begin;
    public event Action? Changed;
    public event Action<int>? EditRequested;
    public event Action<KeyEventArgs>? Command;

    static readonly Dictionary<byte[], Bitmap?> Pictures = new(ReferenceEqualityComparer.Instance);
    enum Drag { None, Move, Resize }
    Drag _drag; Point _start; (double X, double Y, double W, double H) _orig; bool _begun;

    public SlideView() { Focusable = true; ClipToBounds = true; }

    /// <summary>Fläche der Folie im Steuerelement und Maßstab (Pixel je cm).</summary>
    public (Rect Area, double K) Frame()
    {
        var w = Bounds.Width; var h = Bounds.Height; if (w <= 0 || h <= 0) return (default, 1);
        var k = Math.Min(w / SlideDoc.WidthCm, h / SlideDoc.HeightCm);
        var aw = SlideDoc.WidthCm * k; var ah = SlideDoc.HeightCm * k;
        return (new Rect((w - aw) / 2, (h - ah) / 2, aw, ah), k);
    }

    public Rect ShapeRect(SlideShape s) { var (a, k) = Frame(); return new Rect(a.X + s.X * k, a.Y + s.Y * k, Math.Max(1, s.W * k), Math.Max(1, s.H * k)); }

    public void Select(int i) { Selected = i >= 0 && i < Slide.Shapes.Count ? i : -1; InvalidateVisual(); SelectionChanged?.Invoke(); }

    int HitShape(Point p) { for (var i = Slide.Shapes.Count - 1; i >= 0; i--) if (ShapeRect(Slide.Shapes[i]).Inflate(2).Contains(p)) return i; return -1; }
    /// <summary>T-0701: acht Griffe (Ecken und Kantenmitten) – Richtung je Achse: -1 = links/oben, 0 = Mitte, 1 = rechts/unten.</summary>
    static IEnumerable<(Point P, int X, int Y)> HandlePoints(Rect r)
    {
        for (var y = -1; y <= 1; y++)
            for (var x = -1; x <= 1; x++)
                if (x != 0 || y != 0) yield return (new Point(r.X + (x + 1) / 2.0 * r.Width, r.Y + (y + 1) / 2.0 * r.Height), x, y);
    }

    (int X, int Y)? HandleAt(Point p)
    {
        if (Selected < 0 || Selected >= Slide.Shapes.Count) return null;
        foreach (var h in HandlePoints(ShapeRect(Slide.Shapes[Selected]))) if (new Rect(h.P - new Point(7, 7), new Size(14, 14)).Contains(p)) return (h.X, h.Y);
        return null;
    }

    (int X, int Y) _handle = (1, 1);

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!Editable) return;
        Focus();
        var p = e.GetPosition(this);
        if (HandleAt(p) is { } hd) { _drag = Drag.Resize; _handle = hd; }
        else
        {
            var hit = HitShape(p);
            if (hit != Selected) Select(hit);
            if (hit < 0) return;
            if (e.ClickCount == 2) { EditRequested?.Invoke(hit); return; }
            _drag = Drag.Move;
        }
        var s = Slide.Shapes[Selected]; _orig = (s.X, s.Y, s.W, s.H); _start = p; _begun = false;
        e.Pointer.Capture(this);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!Editable) return;
        var p = e.GetPosition(this);
        Cursor = HandleAt(p) is { } hc ? new Cursor(hc switch { (0, _) => StandardCursorType.SizeNorthSouth, (_, 0) => StandardCursorType.SizeWestEast, (-1, -1) => StandardCursorType.TopLeftCorner, (1, -1) => StandardCursorType.TopRightCorner, (-1, 1) => StandardCursorType.BottomLeftCorner, _ => StandardCursorType.BottomRightCorner })
            : HitShape(p) >= 0 ? new Cursor(StandardCursorType.SizeAll) : Cursor.Default;
        if (_drag == Drag.None || Selected < 0) return;
        var k = Frame().K; var dx = (p.X - _start.X) / k; var dy = (p.Y - _start.Y) / k;
        if (!_begun && Math.Abs(dx) + Math.Abs(dy) < 0.05) return;
        if (!_begun) { _begun = true; Begin?.Invoke(); }
        var s = Slide.Shapes[Selected];
        static double Snap(double v) => Math.Round(v * 10) / 10;
        if (_drag == Drag.Move) { s.X = Snap(Math.Clamp(_orig.X + dx, -s.W + 0.5, SlideDoc.WidthCm - 0.5)); s.Y = Snap(Math.Clamp(_orig.Y + dy, -s.H + 0.5, SlideDoc.HeightCm - 0.5)); }
        else
        {
            // Griff links/oben verschiebt die Kante (Breite/Höhe ändern sich entgegengesetzt), rechts/unten wie bisher
            if (_handle.X == 1) s.W = Snap(Math.Max(0.5, _orig.W + dx));
            else if (_handle.X == -1) { var w = Snap(Math.Max(0.5, _orig.W - dx)); s.X = Snap(_orig.X + _orig.W - w); s.W = w; }
            if (_handle.Y == 1) s.H = Snap(Math.Max(0.5, _orig.H + dy));
            else if (_handle.Y == -1) { var h = Snap(Math.Max(0.5, _orig.H - dy)); s.Y = Snap(_orig.Y + _orig.H - h); s.H = h; }
        }
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_drag != Drag.None && _begun) Changed?.Invoke();
        _drag = Drag.None; e.Pointer.Capture(null);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!Editable) { Command?.Invoke(e); return; }
        if (Selected >= 0 && e.Key is Key.Left or Key.Right or Key.Up or Key.Down)
        {
            var step = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 1 : 0.1; var s = Slide.Shapes[Selected];
            Begin?.Invoke();
            if (e.Key == Key.Left) s.X -= step; else if (e.Key == Key.Right) s.X += step; else if (e.Key == Key.Up) s.Y -= step; else s.Y += step;
            s.X = Math.Round(s.X, 2); s.Y = Math.Round(s.Y, 2);
            InvalidateVisual(); Changed?.Invoke(); e.Handled = true; return;
        }
        if (e.Key == Key.Escape && Selected >= 0) { Select(-1); e.Handled = true; return; }
        if (e.Key is Key.F2 or Key.Enter && Selected >= 0) { EditRequested?.Invoke(Selected); e.Handled = true; return; }
        Command?.Invoke(e);
    }

    public override void Render(DrawingContext ctx)
    {
        var (area, k) = Frame(); if (area.Width <= 0) return;
        Paint(ctx, Doc, Slide, area, k, Revealed, AnimProgress);
        if (Editable && Selected >= 0 && Selected < Slide.Shapes.Count)
        {
            var r = ShapeRect(Slide.Shapes[Selected]);
            var gold = new SolidColorBrush(Color.Parse("#C9A227"));
            ctx.DrawRectangle(new Pen(gold, 1.5, new DashStyle([4, 3], 0)), r.Inflate(2));
            // T-0701: Griffe an allen Ecken und Kanten wie in PowerPoint
            foreach (var h in HandlePoints(r)) ctx.DrawRectangle(Brushes.White, new Pen(gold, 1.5), new Rect(h.P - new Point(5, 5), new Size(10, 10)));
        }
        if (Laser is { } l) { var c = new Point(area.X + l.X * k, area.Y + l.Y * k); ctx.DrawEllipse(new SolidColorBrush(Color.Parse("#E0FF2020")), new Pen(new SolidColorBrush(Color.Parse("#60FF2020")), 6), c, 6, 6); }
    }

    /// <summary>1 Punkt in Zentimetern.</summary>
    const double PtCm = 2.54 / 72;

    static IBrush B(string hex) => new SolidColorBrush(Color.TryParse(hex.Length == 7 ? hex : hex, out var c) ? c : Colors.Gray);

    /// <summary>Folie zeichnen – auch für Vorschaubilder, Vorführung und Bildexport.</summary>
    public static void Paint(DrawingContext ctx, SlideDoc doc, Slide slide, Rect area, double k, int? revealed = null, double progress = 1)
    {
        var th = doc.Theme;
        ctx.FillRectangle(B(slide.Background ?? th.Background), area);
        var font = new FontFamily(th.Font + ", Inter, Arial");
        var animIndex = 0;
        foreach (var s in slide.Shapes)
        {
            double alpha = 1, dy = 0, scale = 1;
            if (revealed is int rv && s.Animation != Animation.Keine)
            {
                var my = animIndex++;
                if (my >= rv) continue;
                if (my == rv - 1 && progress < 1)
                {
                    if (s.Animation == Animation.Einblenden) alpha = progress;
                    else if (s.Animation == Animation.Hereinfliegen) dy = (1 - progress) * (SlideDoc.HeightCm - s.Y) * k;
                    else scale = Math.Max(0.05, progress);
                }
            }
            var r = new Rect(area.X + s.X * k, area.Y + s.Y * k + dy, Math.Max(1, s.W * k), Math.Max(1, s.H * k));
            using var op = ctx.PushOpacity(alpha);
            using var tr = ctx.PushTransform(Matrix.CreateTranslation(-r.Center.X, -r.Center.Y) * Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(r.Center.X, r.Center.Y));
            switch (s.Kind)
            {
                case ShapeKind.Bild when s.Image is { Length: > 0 } img:
                {
                    if (!Pictures.TryGetValue(img, out var bmp)) { try { using var ms = new MemoryStream(img); bmp = new Bitmap(ms); } catch (Exception e) when (e is ArgumentException or InvalidOperationException or NotSupportedException or IOException) { bmp = null; } Pictures[img] = bmp; }
                    if (bmp is not null) ctx.DrawImage(bmp, r); else ctx.DrawRectangle(Brushes.LightGray, null, r);
                    break;
                }
                case ShapeKind.Tabelle when s.Table is { Count: > 0 } t:
                {
                    var cols = t.Max(x => x.Count); var cw = r.Width / Math.Max(1, cols); var rh = r.Height / t.Count;
                    for (var i = 0; i < t.Count; i++)
                        for (var c = 0; c < cols; c++)
                        {
                            var cell = new Rect(r.X + c * cw, r.Y + i * rh, cw, rh);
                            ctx.DrawRectangle(i == 0 ? B(th.Accent) : i % 2 == 0 ? B("#EEF2F7") : Brushes.White, new Pen(Brushes.White, 1), cell);
                            var ft = Text(c < t[i].Count ? t[i][c] : "", font, s.FontSize * PtCm * k, i == 0, i == 0 ? Brushes.White : B("#1F2937"), cw - 8);
                            ft.MaxLineCount = 2; ft.Trimming = TextTrimming.CharacterEllipsis;
                            ctx.DrawText(ft, new Point(cell.X + 4, cell.Y + (rh - ft.Height) / 2));
                        }
                    break;
                }
                case ShapeKind.Diagramm when s.ChartValues is { Count: > 0 }:
                {
                    using var _ = ctx.PushTransform(Matrix.CreateTranslation(r.X, r.Y));
                    SheetChartView.Paint(ctx, r.Size, new SheetChart(s.ChartKind, "A1", s.Text), new ChartData(s.ChartLabels ?? [], [(s.Text.Length > 0 ? s.Text : "Werte", s.ChartValues.ToArray())]));
                    break;
                }
                default:
                {
                    if (s.Fill is { } fill)
                    {
                        var fb = B(fill);
                        if (s.Kind == ShapeKind.Ellipse) ctx.DrawEllipse(fb, null, r.Center, r.Width / 2, r.Height / 2);
                        else if (s.Kind == ShapeKind.Pfeil)
                            ctx.DrawGeometry(fb, null, new PolylineGeometry([new(r.Left, r.Top + r.Height * 0.25), new(r.Right - r.Height * 0.5, r.Top + r.Height * 0.25), new(r.Right - r.Height * 0.5, r.Top), new(r.Right, r.Top + r.Height / 2), new(r.Right - r.Height * 0.5, r.Bottom), new(r.Right - r.Height * 0.5, r.Top + r.Height * 0.75), new(r.Left, r.Top + r.Height * 0.75)], true));
                        else ctx.FillRectangle(fb, r);
                    }
                    var text = s.Kind == ShapeKind.Medien ? "" + Path.GetFileName(s.MediaPath ?? "Video/Audio") : s.Text;
                    if (s.Kind == ShapeKind.Medien && s.Fill is null) ctx.FillRectangle(B("#111827"), r);
                    if (text.Length == 0) break;
                    var color = s.Kind == ShapeKind.Medien ? Brushes.White : B(s.Color ?? (s.Role == "title" ? th.Title : th.Text));
                    var px = s.FontSize * PtCm * k; // Punkt → cm → Pixel
                    var lines = new List<(FormattedText Ft, double Indent, bool Bullet)>();
                    // T-0701: mehrere Textformate in einem Feld – je Abschnitt eigene Größe, Fett, Kursiv, Unterstreichung, Farbe, Schrift
                    var chars = s.Kind != ShapeKind.Medien && s.Runs is { } runs ? TextRuns.Expand(runs, text) : null;
                    var pos = 0;
                    foreach (var raw in text.Split('\n'))
                    {
                        var lvl = raw.StartsWith('\t') ? 1 : 0; var indent = s.Bullets ? (0.95 + lvl * 1.27) * k : 0;
                        var line = raw.TrimStart('\t'); var lineStart = pos + raw.Length - line.Length;
                        var ft = Text(line, font, px * (lvl == 1 ? 0.85 : 1), s.Bold, color, Math.Max(1, r.Width - indent - 0.25 * k * 2));
                        ft.TextAlignment = s.Align switch { Align.Center => TextAlignment.Center, Align.Right => TextAlignment.Right, Align.Justify => TextAlignment.Justify, _ => TextAlignment.Left };
                        if (chars is not null)
                            for (var i = 0; i < line.Length;)
                            {
                                var look = chars[lineStart + i]; var j = i; while (j < line.Length && chars[lineStart + j] == look) j++;
                                if (look.Size is { } sz) ft.SetFontSize(Math.Max(1, sz * PtCm * k), i, j - i);
                                if (look.Bold is { } b) ft.SetFontWeight(b ? FontWeight.Bold : FontWeight.Normal, i, j - i);
                                if (look.Italic == true) ft.SetFontStyle(FontStyle.Italic, i, j - i);
                                if (look.Underline is not null) ft.SetTextDecorations(TextDecorations.Underline, i, j - i);
                                if (look.Color is { } col) ft.SetForegroundBrush(B(col), i, j - i);
                                if (look.Font is { Length: > 0 } fn && !fn.StartsWith('+')) ft.SetFontFamily(new FontFamily(fn + ", " + th.Font + ", Inter, Arial"), i, j - i);
                                i = j;
                            }
                        lines.Add((ft, indent, s.Bullets && raw.Trim().Length > 0));
                        pos += raw.Length + 1;
                    }
                    var total = lines.Sum(l => Math.Max(l.Ft.Height, l.Ft.Baseline)) ;
                    var y = s.Kind is ShapeKind.Text ? r.Y + 0.13 * k : r.Y + Math.Max(0, (r.Height - total) / 2);
                    using var clip = ctx.PushClip(r.Inflate(new Thickness(0, 0, 0, r.Height)));
                    foreach (var (ft, indent, bullet) in lines)
                    {
                        if (bullet) { var dot = Text("•", font, ft.Height / 1.25, false, color, 50); ctx.DrawText(dot, new Point(r.X + 0.25 * k + indent - 0.7 * k, y)); }
                        ctx.DrawText(ft, new Point(r.X + 0.25 * k + indent, y));
                        y += Math.Max(ft.Height, 1);
                    }
                    break;
                }
            }
        }
    }

    static FormattedText Text(string s, FontFamily font, double size, bool bold, IBrush brush, double maxWidth) =>
        new(s.Length == 0 ? " " : s, CultureInfo.GetCultureInfo("de-DE"), FlowDirection.LeftToRight, new Typeface(font, FontStyle.Normal, bold ? FontWeight.Bold : FontWeight.Normal), Math.Max(1, size), brush) { MaxTextWidth = Math.Max(1, maxWidth) };
}
