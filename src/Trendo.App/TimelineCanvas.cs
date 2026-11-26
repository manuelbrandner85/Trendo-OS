using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using SkiaSharp;
using Trendo.Core.Wissen;
using static Trendo.Core.Wissen.TimelineKit;

namespace Trendo.App;

/// <summary>T-0780: Arbeitsfläche des Studios „Zeitleisten“. Zeichnet dieselben Bausteine wie SVG, PNG und PDF (<see cref="TimelineKit.Layout"/>).
/// Mausrad = vergrößern/verkleinern um die Mausstelle, Ziehen auf freier Fläche = verschieben, Eintrag anklicken = wählen, Eintrag ziehen = in der Zeit verschieben,
/// Doppelklick auf freie Fläche = neues Ereignis an dieser Stelle. Tasten: + / − vergrößern, ← / → verschieben, Entf löscht den gewählten Eintrag.</summary>
public sealed class TimelineCanvas : Control
{
    public Timeline Data { get; set; } = new();
    public double From { get; set; } = 1900;
    public double To { get; set; } = 2030;
    public string? Selected { get; set; }
    /// <summary>Ansicht geändert (Zoom/Verschieben) – von, bis.</summary>
    public Action<double, double>? ViewChanged { get; set; }
    public Action<string?>? Clicked { get; set; }
    /// <summary>Eintrag um so viele Jahre verschoben (astronomisch, auch Bruchteile).</summary>
    public Action<string, double>? Moved { get; set; }
    /// <summary>Doppelklick auf freie Fläche: neues Ereignis bei diesem Jahr (astronomisch).</summary>
    public Action<double>? AddAt { get; set; }
    public Action? DeleteSelected { get; set; }

    Scene? _scene; double _sceneW = -1; int _sceneKey;
    enum Mode { None, Pan, Move }
    Mode _mode; Point _down, _at; string? _item; double _from0, _to0;
    static readonly ConditionalWeakTable<byte[], Bitmap> Bitmaps = new();

    public TimelineCanvas() { ClipToBounds = true; Focusable = true; }

    public Scene SceneFor(double width)
    {
        var key = HashCode.Combine(Data.Items.Count, Data.Tracks.Count, From, To, Selected, Data.Title, Data.Items.Sum(i => (long)i.GetHashCode()));
        if (_scene is null || Math.Abs(width - _sceneW) > 0.5 || key != _sceneKey) { _scene = Layout(Data, From, To, width, Selected); _sceneW = width; _sceneKey = key; }
        return _scene;
    }

    /// <summary>Zeitpunkt (astronomisches Dezimaljahr) an der Stelle x.</summary>
    public double YearAt(double x) => From + (x - TimelineKit.Left) / Math.Max(1, (Bounds.Width - RightPad) - TimelineKit.Left) * (To - From);

    /// <summary>Welcher Eintrag liegt an der Stelle? (oberster zuerst)</summary>
    public string? ItemAt(Point p)
    {
        var s = SceneFor(Math.Max(500, Bounds.Width));
        foreach (var (id, (x, y, w, h)) in s.Hits.Reverse()) if (p.X >= x && p.X <= x + w && p.Y >= y && p.Y <= y + h) return id;
        return null;
    }

    protected override Size MeasureOverride(Size a)
    {
        var w = double.IsInfinity(a.Width) ? 1100 : Math.Max(500, a.Width);
        return new Size(w, SceneFor(w).Height);
    }

    /// <summary>Vergrößern (Faktor &lt; 1) oder verkleinern um die Stelle <paramref name="atYear"/>.</summary>
    public void Zoom(double factor, double? atYear = null)
    {
        var c = atYear ?? (From + To) / 2; var span = Math.Clamp((To - From) * factor, 1 / 12.0, 200_000);
        var rel = (c - From) / Math.Max(1e-9, To - From);
        From = c - span * rel; To = From + span; InvalidateMeasure(); InvalidateVisual(); ViewChanged?.Invoke(From, To);
    }

    public void Pan(double fraction) { var d = (To - From) * fraction; From += d; To += d; InvalidateVisual(); ViewChanged?.Invoke(From, To); }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) Pan(-e.Delta.Y * 0.1);
        else Zoom(e.Delta.Y > 0 ? 0.8 : 1.25, YearAt(e.GetPosition(this).X));
        e.Handled = true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e); Focus();
        var p = e.GetPosition(this); var pt = e.GetCurrentPoint(this);
        _down = _at = p; _from0 = From; _to0 = To; _item = ItemAt(p);
        if (pt.Properties.IsRightButtonPressed) { if (_item is not null && _item != Selected) { Selected = _item; InvalidateVisual(); Clicked?.Invoke(_item); } _mode = Mode.None; return; }
        if (!pt.Properties.IsLeftButtonPressed) return;
        if (e.ClickCount == 2 && _item is null) { AddAt?.Invoke(YearAt(p.X)); e.Handled = true; return; }
        _mode = _item is null ? Mode.Pan : Mode.Move;
        e.Pointer.Capture(this); e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e); var p = e.GetPosition(this);
        if (_mode == Mode.None) { Cursor = new Cursor(ItemAt(p) is null ? StandardCursorType.Arrow : StandardCursorType.Hand); return; }
        _at = p;
        if (_mode == Mode.Pan)
        {
            var perPx = (_to0 - _from0) / Math.Max(1, Bounds.Width - RightPad - TimelineKit.Left);
            From = _from0 - (p.X - _down.X) * perPx; To = _to0 - (p.X - _down.X) * perPx;
        }
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e); e.Pointer.Capture(null);
        var mode = _mode; _mode = Mode.None; var dx = _at.X - _down.X;
        if (mode == Mode.Pan) { if (Math.Abs(dx) >= 3) ViewChanged?.Invoke(From, To); else if (Selected is not null) { Selected = null; Clicked?.Invoke(null); } InvalidateVisual(); return; }
        if (mode == Mode.Move && _item is { } id)
        {
            var perPx = (To - From) / Math.Max(1, Bounds.Width - RightPad - TimelineKit.Left);
            if (Math.Abs(dx) >= 4) Moved?.Invoke(id, dx * perPx);
            else { Selected = id; Clicked?.Invoke(id); }
            InvalidateVisual();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.Key)
        {
            case Key.Add or Key.OemPlus: Zoom(0.8); e.Handled = true; break;
            case Key.Subtract or Key.OemMinus: Zoom(1.25); e.Handled = true; break;
            case Key.Left: Pan(-0.1); e.Handled = true; break;
            case Key.Right: Pan(0.1); e.Handled = true; break;
            case Key.Delete when Selected is not null: DeleteSelected?.Invoke(); e.Handled = true; break;
        }
    }

    static Color C(string hex) => Color.Parse(hex.StartsWith('#') ? hex : "#" + hex);

    public override void Render(DrawingContext g)
    {
        var s = SceneFor(Math.Max(500, Bounds.Width));
        var moveDx = _mode == Mode.Move ? _at.X - _down.X : 0;
        var de = CultureInfo.GetCultureInfo("de-DE");
        foreach (var p in s.Prims)
        {
            var off = moveDx != 0 && p switch { PRect r => r.ItemId == _item, PText t => t.ItemId == _item, PCircle c => c.ItemId == _item, PImage i => i.ItemId == _item, _ => false } ? moveDx : 0;
            switch (p)
            {
                case PRect r:
                {
                    var fill = C(r.Fill); var brush = new SolidColorBrush(fill, r.Opacity);
                    IPen? pen = r.Stroke is { } st ? new Pen(new SolidColorBrush(C(st)), 1.5, r.Dashed ? new DashStyle([5, 3], 0) : null) : null;
                    g.DrawRectangle(brush, pen, new Rect(r.X + off, r.Y, Math.Max(0.5, r.W), Math.Max(0.5, r.H)), r.Radius, r.Radius);
                    break;
                }
                case PLine l:
                    g.DrawLine(new Pen(new SolidColorBrush(C(l.Color)), l.Width, l.Dashed ? new DashStyle([4, 3], 0) : null), new Point(l.X1, l.Y1), new Point(l.X2, l.Y2)); break;
                case PCircle c:
                    g.DrawEllipse(new SolidColorBrush(C(c.Fill)), c.Stroke is { } cs ? new Pen(new SolidColorBrush(C(cs)), 1.5, c.Dashed ? new DashStyle([2, 2], 0) : null) : null, new Point(c.Cx + off, c.Cy), c.R, c.R); break;
                case PText t:
                {
                    var ft = new FormattedText(t.Text, de, FlowDirection.LeftToRight, new Typeface("Inter", FontStyle.Normal, t.Bold ? FontWeight.SemiBold : FontWeight.Normal), t.Size, new SolidColorBrush(C(t.Color)));
                    var x = t.Anchor < 0 ? t.X : t.Anchor > 0 ? t.X - ft.Width : t.X - ft.Width / 2;
                    g.DrawText(ft, new Point(x + off, t.Y - ft.Baseline)); break;
                }
                case PImage im:
                    if (BitmapOf(im.Data) is { } bmp) g.DrawImage(bmp, new Rect(0, 0, bmp.Size.Width, bmp.Size.Height), new Rect(im.X + off, im.Y, im.W, im.H));
                    break;
            }
        }
    }

    static Bitmap? BitmapOf(byte[] data)
    {
        if (Bitmaps.TryGetValue(data, out var b)) return b;
        try { using var ms = new MemoryStream(data); b = new Bitmap(ms); Bitmaps.Add(data, b); return b; }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or NotSupportedException or IOException) { return null; }
    }

    // ---------------- PNG (SkiaSharp) ----------------

    static SKColor Sk(string hex, double opacity = 1)
    {
        var h = hex.TrimStart('#'); byte a = 255;
        if (h.Length == 8) { a = Convert.ToByte(h[..2], 16); h = h[2..]; }
        return new SKColor(Convert.ToByte(h[..2], 16), Convert.ToByte(h[2..4], 16), Convert.ToByte(h[4..6], 16), (byte)Math.Round(a * Math.Clamp(opacity, 0, 1)));
    }

    /// <summary>Zeitleiste als PNG in doppelter Auflösung (für Folien und Druck) – dieselben Bausteine wie auf dem Bildschirm.</summary>
    public static string SavePng(Scene s, string path, float scale = 2)
    {
        var w = (int)Math.Ceiling(s.Width * scale); var h = (int)Math.Ceiling(s.Height * scale);
        using var bmp = new SKBitmap(w, h);
        using (var c = new SKCanvas(bmp))
        {
            c.Scale(scale);
            var face = SKTypeface.FromFamilyName("Segoe UI") ?? SKTypeface.Default; var bold = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold) ?? face;
            foreach (var p in s.Prims)
            {
                switch (p)
                {
                    case PRect r:
                    {
                        using var fill = new SKPaint { Color = Sk(r.Fill, r.Opacity), IsAntialias = true };
                        var rr = new SKRect((float)r.X, (float)r.Y, (float)(r.X + Math.Max(0.5, r.W)), (float)(r.Y + Math.Max(0.5, r.H)));
                        c.DrawRoundRect(rr, (float)r.Radius, (float)r.Radius, fill);
                        if (r.Stroke is { } st) { using var sp = new SKPaint { Color = Sk(st), IsStroke = true, StrokeWidth = 1.5f, IsAntialias = true, PathEffect = r.Dashed ? SKPathEffect.CreateDash([5, 3], 0) : null }; c.DrawRoundRect(rr, (float)r.Radius, (float)r.Radius, sp); }
                        break;
                    }
                    case PLine l:
                    {
                        using var sp = new SKPaint { Color = Sk(l.Color), IsStroke = true, StrokeWidth = (float)l.Width, IsAntialias = true, PathEffect = l.Dashed ? SKPathEffect.CreateDash([4, 3], 0) : null };
                        c.DrawLine((float)l.X1, (float)l.Y1, (float)l.X2, (float)l.Y2, sp); break;
                    }
                    case PCircle ci:
                    {
                        using var fp = new SKPaint { Color = Sk(ci.Fill), IsAntialias = true };
                        c.DrawCircle((float)ci.Cx, (float)ci.Cy, (float)ci.R, fp);
                        if (ci.Stroke is { } st) { using var sp = new SKPaint { Color = Sk(st), IsStroke = true, StrokeWidth = 1.5f, IsAntialias = true }; c.DrawCircle((float)ci.Cx, (float)ci.Cy, (float)ci.R, sp); }
                        break;
                    }
                    case PText t:
                    {
                        using var font = new SKFont(t.Bold ? bold : face, (float)t.Size);
                        using var paint = new SKPaint { Color = Sk(t.Color), IsAntialias = true };
                        var tw = font.MeasureText(t.Text);
                        var x = t.Anchor < 0 ? t.X : t.Anchor > 0 ? t.X - tw : t.X - tw / 2;
                        c.DrawText(t.Text, (float)x, (float)t.Y, SKTextAlign.Left, font, paint); break;
                    }
                    case PImage im:
                    {
                        using var img = SKBitmap.Decode(im.Data);
                        if (img is not null) c.DrawBitmap(img, new SKRect((float)im.X, (float)im.Y, (float)(im.X + im.W), (float)(im.Y + im.H)));
                        break;
                    }
                }
            }
        }
        using var data = bmp.Encode(SKEncodedImageFormat.Png, 100);
        var tmp = path + ".tmp";
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using (var f = File.Create(tmp)) data.SaveTo(f);
        Trendo.Core.SafeFile.Move(tmp, path);
        return path;
    }

    /// <summary>Bild für einen Eintrag verkleinern (längste Seite höchstens <paramref name="max"/> px, JPEG) – so bleibt das Dokument klein.</summary>
    public static byte[] Thumb(string file, int max = 480)
    {
        using var src = SKBitmap.Decode(file) ?? throw new InvalidDataException("Das Bild lässt sich nicht lesen (PNG, JPEG, WebP oder BMP).");
        var k = Math.Min(1.0, max / (double)Math.Max(src.Width, src.Height));
        using var small = k < 1 ? src.Resize(new SKImageInfo(Math.Max(1, (int)(src.Width * k)), Math.Max(1, (int)(src.Height * k))), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear)) : src.Copy();
        using var img = SKImage.FromBitmap(small);
        using var data = img.Encode(SKEncodedImageFormat.Jpeg, 85);
        var bytes = data.ToArray();
        if (bytes.Length > MaxImageBytes) throw new InvalidDataException("Das Bild ist auch verkleinert noch zu groß.");
        return bytes;
    }
}
