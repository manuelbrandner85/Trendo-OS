using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using Trendo.Core.Kreativ;

namespace Trendo.App;

/// <summary>T-0696: Zeitleiste der Animation – je Form eine Zeile, die gewählte Form aufgeklappt mit Position, Größe, Drehung, Deckkraft, Farbe.
/// Schlüsselbilder (Rauten) lassen sich ziehen; Klick ins Lineal oder die Spur setzt die Zeit, Doppelklick in eine Spur setzt einen Schlüssel,
/// Rechtsklick auf eine Raute wählt die Kurve (gleichmäßig, weich, federnd …) oder löscht sie.</summary>
public sealed class KeyTimeline : Control
{
    public const double LabelW = 130, RulerH = 20, HeadH = 22, RowH = 18;
    readonly VDoc _doc; readonly VectorEditorState _st;
    public double Span { get; }
    public double Time { get; set; }
    public Action<double>? Scrub { get; set; }
    public Action<string, string[], double, double>? KeysMoved { get; set; }
    public Action<string, string[], double, string>? KeyCommand { get; set; }
    public Action<string, string[], double>? AddKey { get; set; }
    public Action<string>? SelectShape { get; set; }

    enum Mode { None, Scrub, Key }
    Mode _mode; (string Id, string[] Props, double T)? _key; double _dragT; bool _moved; Point _down;

    public KeyTimeline(VDoc doc, VectorEditorState st, double span)
    {
        _doc = doc; _st = st; Span = Math.Max(1, span);
        Height = RulerH + Rows().Sum(r => r.H) + 4;
        Focusable = true; ClipToBounds = true;
        Cursor = new Cursor(StandardCursorType.Arrow);
    }

    /// <summary>Zeilen von oben: (Form, Zeilen-Kennung oder leer für die Kopfzeile, Eigenschaften, Höhe).</summary>
    public List<(VShape Shape, string Row, string[] Props, double H)> Rows()
    {
        var list = new List<(VShape, string, string[], double)>();
        foreach (var s in _doc.Shapes)
        {
            list.Add((s, "", MotionKit.Props.ToArray(), HeadH));
            if (_st.Selected.Contains(s.Id)) foreach (var (id, _, props) in MotionKit.Rows) list.Add((s, id, props, RowH));
        }
        return list;
    }

    public double X(double t) => LabelW + (Bounds.Width - LabelW - 10) * t / Span;
    public double T(double x) => Math.Clamp((x - LabelW) / Math.Max(1, Bounds.Width - LabelW - 10) * Span, 0, Span);

    /// <summary>Mitte der Raute (für Tests und Hilfen): Form, Zeile („“ = Kopfzeile), Zeit.</summary>
    public Point? PointOf(string id, string row, double t)
    {
        var y = RulerH;
        foreach (var r in Rows()) { if (r.Shape.Id == id && r.Row == row) return new Point(X(t), y + r.H / 2); y += r.H; }
        return null;
    }

    (VShape Shape, string Row, string[] Props, double H, double Y)? RowAt(double py)
    {
        var y = RulerH;
        foreach (var r in Rows()) { if (py >= y && py < y + r.H) return (r.Shape, r.Row, r.Props, r.H, y); y += r.H; }
        return null;
    }

    static readonly IBrush Gold = new SolidColorBrush(Color.Parse("#F5C542"));
    static readonly IBrush GoldDim = new SolidColorBrush(Color.Parse("#8C7A3A"));
    static readonly IBrush Txt = new SolidColorBrush(Color.Parse("#E5E7EB"));
    static readonly IBrush Muted = new SolidColorBrush(Color.Parse("#9CA3AF"));
    static readonly IBrush Lane = new SolidColorBrush(Color.Parse("#141B28"));
    static readonly IBrush LaneSel = new SolidColorBrush(Color.Parse("#1D2638"));
    static readonly Typeface Face = new("Segoe UI");

    static FormattedText Ft(string s, double size, IBrush b) => new(s, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Face, size, b);

    public override void Render(DrawingContext g)
    {
        var w = Bounds.Width;
        g.FillRectangle(new SolidColorBrush(Color.Parse("#0E1420")), new Rect(Bounds.Size));
        // Lineal
        var step = Span <= 4 ? 0.5 : Span <= 12 ? 1 : 2;
        for (var t = 0.0; t <= Span + 1e-9; t += step)
        {
            var x = X(t);
            g.DrawLine(new Pen(Muted, 1), new Point(x, RulerH - 6), new Point(x, RulerH));
            g.DrawText(Ft(t.ToString("0.#", System.Globalization.CultureInfo.GetCultureInfo("de-DE")) + " s", 10, Muted), new Point(x + 2, 2));
        }
        var y = RulerH;
        foreach (var r in Rows())
        {
            var sel = _st.Selected.Contains(r.Shape.Id);
            g.FillRectangle(r.Row.Length == 0 && sel ? LaneSel : Lane, new Rect(0, y, w, r.H - 1));
            var label = r.Row.Length == 0 ? (r.Shape.Name.Length > 0 ? r.Shape.Name : r.Shape.Kind == "text" ? "„" + Cut(r.Shape.Text, 14) + "“" : VectorKit.Label(r.Shape.Kind) + " " + r.Shape.Id.Split('-').Last()) : "   " + MotionKit.Rows.First(x => x.Id == r.Row).Label;
            g.DrawText(Ft(label, r.Row.Length == 0 ? 12 : 11, r.Row.Length == 0 ? Txt : Muted), new Point(6, y + (r.H - (r.Row.Length == 0 ? 15 : 13)) / 2));
            var times = MotionKit.KeyTimes(r.Shape, r.Props);
            if (r.Row.Length > 0 && times.Count > 1) g.DrawLine(new Pen(GoldDim, 1.5), new Point(X(times[0]), y + r.H / 2), new Point(X(times[^1]), y + r.H / 2));
            foreach (var t in times)
            {
                var drag = _mode == Mode.Key && _moved && _key is { } k && k.Id == r.Shape.Id && k.Props.SequenceEqual(r.Props) && Math.Abs(k.T - t) < 0.005;
                var cx = X(drag ? _dragT : t); var cy = y + r.H / 2; var rad = r.Row.Length == 0 ? 4.5 : 5.5;
                var g2 = new StreamGeometry();
                using (var c = g2.Open()) { c.BeginFigure(new Point(cx, cy - rad), true); c.LineTo(new Point(cx + rad, cy)); c.LineTo(new Point(cx, cy + rad)); c.LineTo(new Point(cx - rad, cy)); c.EndFigure(true); }
                var ease = r.Row.Length > 0 ? r.Props.Select(p => r.Shape.Tracks.GetValueOrDefault(p)?.FirstOrDefault(x => Math.Abs(x.T - t) < 0.005)?.Ease).FirstOrDefault(e => e is not null) : null;
                g.DrawGeometry(r.Row.Length == 0 ? GoldDim : Gold, new Pen(drag ? Brushes.White : new SolidColorBrush(Color.Parse("#111827")), 1), g2);
                if (ease == "federnd") g.DrawText(Ft("~", 10, Muted), new Point(cx + 6, cy - 8)); else if (ease == "linear") g.DrawText(Ft("/", 10, Muted), new Point(cx + 6, cy - 8));
            }
            y += r.H;
        }
        var px = X(Time);
        g.DrawLine(new Pen(Gold, 2), new Point(px, 0), new Point(px, Bounds.Height));
        g.FillRectangle(Gold, new Rect(px - 5, 0, 10, 6));
    }

    static string Cut(string s, int n) => s.Length <= n ? s : s[..n] + "…";

    (string Id, string[] Props, double T)? KeyAt(Point p)
    {
        if (RowAt(p.Y) is not { } r || p.X < LabelW - 6) return null;
        foreach (var t in MotionKit.KeyTimes(r.Shape, r.Props)) if (Math.Abs(X(t) - p.X) <= 7) return (r.Shape.Id, r.Props, t);
        return null;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var pt = e.GetCurrentPoint(this); var p = pt.Position; _down = p; _moved = false;
        Focus();
        if (pt.Properties.IsRightButtonPressed)
        {
            // T-0730: kein altes Menü stehen lassen – Rechtsklick neben eine Raute öffnet kein Menü eines früheren Schlüssels
            ContextMenu = KeyAt(p) is { } k ? KeyMenu(k) : null;
            return;
        }
        if (!pt.Properties.IsLeftButtonPressed) return;
        e.Handled = true; e.Pointer.Capture(this);
        var row = RowAt(p.Y);
        if (p.X < LabelW && row is { } lr) { _mode = Mode.None; SelectShape?.Invoke(lr.Shape.Id); return; }
        if (KeyAt(p) is { } key) { _mode = Mode.Key; _key = key; _dragT = key.T; return; }
        if (e.ClickCount >= 2 && row is { Row.Length: > 0 } r2) { _mode = Mode.None; AddKey?.Invoke(r2.Shape.Id, r2.Props, Frame(T(p.X))); return; }
        _mode = Mode.Scrub; Time = T(p.X); InvalidateVisual(); Scrub?.Invoke(Time);
    }

    static double Frame(double t) => Math.Round(t * 30) / 30;

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var p = e.GetPosition(this);
        switch (_mode)
        {
            case Mode.Scrub: Time = T(p.X); InvalidateVisual(); Scrub?.Invoke(Time); break;
            case Mode.Key:
                if (!_moved && Math.Abs(p.X - _down.X) < 3) return;
                _moved = true; _dragT = Frame(T(p.X)); InvalidateVisual(); break;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var mode = _mode; _mode = Mode.None; e.Pointer.Capture(null);
        if (mode == Mode.Key && _key is { } k)
        {
            if (_moved && Math.Abs(_dragT - k.T) > 0.004) KeysMoved?.Invoke(k.Id, k.Props, k.T, _dragT);
            else { Time = k.T; InvalidateVisual(); Scrub?.Invoke(k.T); }
        }
        _key = null; _moved = false;
    }

    ContextMenu KeyMenu((string Id, string[] Props, double T) k)
    {
        var m = new ContextMenu { Name = "anim-key-menu" };
        // T-0730: Der Schlüssel wird über Form, Eigenschaften und Zeit festgehalten (nicht über eine Position in einer Liste) und nur benutzt,
        // wenn es ihn beim Klick noch gibt und diese Zeitleiste noch angezeigt wird – ein veraltetes Menü löscht nie einen anderen Schlüssel.
        void Run(string cmd) { m.Close(); if (StillThere(k)) KeyCommand?.Invoke(k.Id, k.Props, k.T, cmd); }
        foreach (var (id, label) in AnimKit.Eases)
        {
            var i = new MenuItem { Header = "Kurve: " + label, Name = "anim-key-ease-" + id };
            i.Click += (_, _) => Run("ease:" + id);
            m.Items.Add(i);
        }
        m.Items.Add(new Separator());
        var del = new MenuItem { Header = "Schlüssel löschen", Name = "anim-key-delete" };
        del.Click += (_, _) => Run("delete");
        m.Items.Add(del);
        return m;
    }

    /// <summary>T-0730: Gibt es den beim Öffnen des Menüs gemerkten Schlüssel noch (gleiche Form, gleiche Eigenschaften, gleiche Zeit) und ist die Zeitleiste noch sichtbar?</summary>
    public bool StillThere((string Id, string[] Props, double T) k) =>
        TopLevel.GetTopLevel(this) is not null && _doc.Find(k.Id) is { } s && MotionKit.KeyTimes(s, k.Props).Any(t => Math.Abs(t - k.T) < 0.005);
}
