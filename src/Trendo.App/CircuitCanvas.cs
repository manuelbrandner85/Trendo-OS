using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Trendo.Core.Arch;
using Trendo.Core.Elektro;

namespace Trendo.App;

/// <summary>Zustand der Schaltungs-Zeichenfläche (überlebt das Neuzeichnen der Seite).</summary>
public sealed class CircuitViewState
{
    public string Tool = "auswahl";
    public string? Selected;
    public double Zoom = 1.2, PanX = -60, PanY = -60;
    public bool Fitted, UserView, Focus;
    public bool Running = true;
    public double Speed = 1; // 1 = Echtzeit, 0,1 = zehnfach langsamer …
    public readonly Dictionary<string, double> Phase = [];
    public readonly List<(double T, double V, double I)> Scope = [];
    public string Hint = "";
}

/// <summary>T-0259: Zeichenfläche des Programms „Schaltung & Simulation“ – Bauteile durch Ziehen zwischen Rasterpunkten setzen (Klick = Standardlänge),
/// Schalter per Klick umlegen, Bauteile verschieben, R dreht; Leitungen nach Spannung gefärbt, Strompunkte laufen mit, Oszilloskop für die Auswahl.</summary>
public sealed class CircuitCanvas : Control
{
    readonly Circuit _c; readonly Func<SimState?> _state; readonly string? _problem;
    readonly CircuitViewState _s; readonly Action<string, Action<Circuit>> _commit; readonly Action<string?> _select;
    (int X, int Y) _grid; (double X, double Y) _mouse; (int X, int Y)? _press; Point? _panFrom; Circuit? _work; int _handle = -1; bool _moved;
    const double G = CircuitDrawing.Grid;

    public CircuitCanvas(Circuit c, Func<SimState?> state, string? problem, CircuitViewState s, Action<string, Action<Circuit>> commit, Action<string?> select)
    {
        _c = c; _state = state; _problem = problem; _s = s; _commit = commit; _select = select;
        ClipToBounds = true; Focusable = true; Cursor = new Cursor(StandardCursorType.Cross);
    }

    (double X, double Y) World(Point p) => (p.X / _s.Zoom + _s.PanX, p.Y / _s.Zoom + _s.PanY);
    Point Screen(double x, double y) => new((x - _s.PanX) * _s.Zoom, (y - _s.PanY) * _s.Zoom);
    static (int X, int Y) Snap((double X, double Y) w) => ((int)Math.Round(w.X / G), (int)Math.Round(w.Y / G));

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e) { base.OnAttachedToVisualTree(e); if (_s.Focus) Avalonia.Threading.Dispatcher.UIThread.Post(() => Focus()); }
    protected override void OnSizeChanged(SizeChangedEventArgs e) { base.OnSizeChanged(e); if ((!_s.Fitted || !_s.UserView) && Bounds.Width > 10) { Fit(); _s.Fitted = true; } }

    public void Fit()
    {
        var shapes = CircuitDrawing.Build(_c, null, CircuitDrawing.Screen);
        var (x0, y0, x1, y1) = _c.Parts.Count == 0 ? (0.0, 0.0, 400.0, 240.0) : Trendo.Core.Statik.FrameDrawing.Extent(shapes);
        var bw = Bounds.Width > 10 ? Bounds.Width : 900; var bh = (Bounds.Height > 10 ? Bounds.Height : 600) - ScopeHeight;
        _s.Zoom = Math.Clamp(Math.Min(bw / (x1 - x0 + 160), bh / (y1 - y0 + 160)), 0.2, 4);
        _s.PanX = (x0 + x1) / 2 - bw / 2 / _s.Zoom; _s.PanY = (y0 + y1) / 2 - bh / 2 / _s.Zoom;
        InvalidateVisual();
    }

    double ScopeHeight => _s.Selected is not null && _s.Scope.Count > 1 ? 120 : 0;

    (string Id, int Handle)? Hit((double X, double Y) w)
    {
        var tol = 8 / _s.Zoom;
        if (_s.Selected is { } sel && _c.Part(sel) is { } sp)
            foreach (var (k, hx, hy) in new[] { (0, sp.X1 * G, sp.Y1 * G), (1, sp.X2 * G, sp.Y2 * G) })
                if (Math.Abs(w.X - hx) < tol && Math.Abs(w.Y - hy) < tol && sp.Kind != "masse") return (sel, k);
        foreach (var p in _c.Parts.AsEnumerable().Reverse())
        {
            if (p.Kind == "masse") { if (Math.Abs(w.X - p.X1 * G) < 14 && w.Y >= p.Y1 * G - tol && w.Y <= p.Y1 * G + 14) return (p.Id, -1); continue; }
            var d = Geo.SegDist(w.X, w.Y, p.X1 * G, p.Y1 * G, p.X2 * G, p.Y2 * G);
            if (d < (p.Kind == "draht" ? tol : Math.Max(tol, 12))) return (p.Id, -1);
        }
        return null;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e); var p = e.GetPosition(this); var (wx, wy) = World(p);
        _s.Zoom = Math.Clamp(_s.Zoom * (e.Delta.Y > 0 ? 1.15 : 1 / 1.15), 0.2, 6); _s.PanX = wx - p.X / _s.Zoom; _s.PanY = wy - p.Y / _s.Zoom; _s.UserView = true;
        InvalidateVisual(); e.Handled = true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e); Focus(); _s.Focus = true;
        var pt = e.GetCurrentPoint(this); var p = pt.Position;
        if (pt.Properties.IsMiddleButtonPressed || pt.Properties.IsRightButtonPressed) { _panFrom = p; e.Pointer.Capture(this); return; }
        var w = World(p); _mouse = w; _grid = Snap(w); _moved = true;
        if (_s.Tool == "auswahl")
        {
            var hit = Hit(w);
            if (hit is { } h && _c.Part(h.Id) is { Kind: "schalter" } sw && h.Handle < 0 && _s.Selected == sw.Id) { Commit("Schalter umlegen", c => { var x = c.Part(sw.Id)!; x.Closed = !x.Closed; }); return; }
            if (hit?.Id != _s.Selected) { _s.Selected = hit?.Id; _s.Scope.Clear(); _select(hit?.Id); }
            if (hit is { } hh) { _press = _grid; _handle = hh.Handle; e.Pointer.Capture(this); }
            InvalidateVisual(); return;
        }
        _press = _grid; e.Pointer.Capture(this); InvalidateVisual();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e); var p = e.GetPosition(this);
        if (_panFrom is { } f) { _s.PanX -= (p.X - f.X) / _s.Zoom; _s.PanY -= (p.Y - f.Y) / _s.Zoom; _panFrom = p; _s.UserView = true; InvalidateVisual(); return; }
        _mouse = World(p); _grid = Snap(_mouse); _moved = true;
        if (_s.Tool == "auswahl" && _press is { } a && _s.Selected is { } sel && _c.Part(sel) is not null)
        {
            var (dx, dy) = (_grid.X - a.X, _grid.Y - a.Y);
            if (dx != 0 || dy != 0 || _work is not null)
            {
                _work = _c.Clone(); var w0 = _c.Part(sel)!; var w = _work.Part(sel)!;
                if (_handle == 0) { w.X1 = _grid.X; w.Y1 = _grid.Y; }
                else if (_handle == 1) { w.X2 = _grid.X; w.Y2 = _grid.Y; }
                else { w.X1 = w0.X1 + dx; w.Y1 = w0.Y1 + dy; w.X2 = w0.X2 + dx; w.Y2 = w0.Y2 + dy; }
            }
        }
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e); e.Pointer.Capture(null);
        if (_panFrom is not null) { _panFrom = null; return; }
        if (_press is not { } a) return; _press = null;
        if (_s.Tool == "auswahl")
        {
            if (_work is { } w && _s.Selected is { } sel) { var np = w.Part(sel)!; _work = null; var (x1, y1, x2, y2) = (np.X1, np.Y1, np.X2, np.Y2); if (x1 == x2 && y1 == y2 && np.Kind != "masse") return; Commit("Verschieben", c => { var q = c.Part(sel)!; (q.X1, q.Y1, q.X2, q.Y2) = (x1, y1, x2, y2); }); }
            return;
        }
        var kind = _s.Tool; var b = _grid;
        if (kind == "masse") b = (a.X, a.Y + 1);
        else if (b == a) { if (kind == "draht") { Hint("Draht: vom einen zum anderen Punkt ziehen."); return; } b = (a.X + 4, a.Y); } // Klick: Bauteil 4 Raster lang
        Commit(PartTypes.Get(kind).Label + " einsetzen", c => _s.Selected = c.Add(kind, a.X, a.Y, b.X, b.Y).Id);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape) { _s.Tool = "auswahl"; _s.Selected = null; _select(null); e.Handled = true; return; }
        if (_s.Selected is not { } sel || _c.Part(sel) is not { } p) return;
        if (e.Key is Key.Delete or Key.Back) { _s.Selected = null; Commit("Löschen", c => c.Remove(sel)); e.Handled = true; }
        else if (e.Key == Key.R && p.Kind != "masse")
        {
            Commit("Drehen", c => { var q = c.Part(sel)!; var (dx, dy) = (q.X2 - q.X1, q.Y2 - q.Y1); (q.X2, q.Y2) = (q.X1 - dy, q.Y1 + dx); });
            e.Handled = true;
        }
    }

    void Commit(string label, Action<Circuit> a) => _commit(label, a);
    void Hint(string h) { _s.Hint = h; InvalidateVisual(); }

    // ---------- Zeichnen ----------
    static readonly Typeface Face = new("Inter");
    static IBrush B(string hex) => new SolidColorBrush(Color.Parse(hex.Length == 9 ? "#" + hex[7..] + hex[1..7] : hex));

    public override void Render(DrawingContext ctx)
    {
        var size = Bounds.Size; if (size.Width < 2) return;
        ctx.FillRectangle(new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Color.Parse("#FF0E1B30"), 0), new GradientStop(Color.Parse("#FF081120"), 1) } }, new Rect(size));
        // Rasterpunkte
        if (_s.Zoom * G >= 8)
        {
            var dot = new SolidColorBrush(Color.FromArgb(40, 160, 190, 230));
            var (x0, y0) = World(new Point(0, 0)); var (x1, y1) = World(new Point(size.Width, size.Height));
            for (var gx = Math.Floor(x0 / G) * G; gx <= x1; gx += G) for (var gy = Math.Floor(y0 / G) * G; gy <= y1; gy += G) { var sp = Screen(gx, gy); ctx.FillRectangle(dot, new Rect(sp.X - 1, sp.Y - 1, 2, 2)); }
        }
        var c = _work ?? _c; var st = _work is null ? _state() : null;
        foreach (var sh in CircuitDrawing.Build(c, st, CircuitDrawing.Screen, true, _s.Selected)) Draw(ctx, sh);
        // Strompunkte (laufen mit dem Strom)
        if (st is not null)
            foreach (var p in c.Parts.Where(p => p.Kind is not ("masse" or "draht")))
            {
                if (!st.Parts.TryGetValue(p.Id, out var r) || Math.Abs(r.I) < 1e-6) continue;
                var ph = _s.Phase.GetValueOrDefault(p.Id); var len = Math.Sqrt(Math.Pow(p.X2 - p.X1, 2) + Math.Pow(p.Y2 - p.Y1, 2)) * G;
                for (var d = ph % 16; d < len; d += 16)
                { var t = d / len; var sp = Screen(p.X1 * G + (p.X2 - p.X1) * G * t, p.Y1 * G + (p.Y2 - p.Y1) * G * t); ctx.FillRectangle(B("#FACC15"), new Rect(sp.X - 2, sp.Y - 2, 4, 4)); }
            }
        // Auswahl
        if (_s.Selected is { } sel && c.Part(sel) is { } ps)
        {
            var gold = new Pen(B("#F5A524"), 2);
            var a = Screen(ps.X1 * G, ps.Y1 * G); var b = Screen(ps.X2 * G, ps.Y2 * G);
            var ux = b.X - a.X; var uy = b.Y - a.Y; var l = Math.Max(1, Math.Sqrt(ux * ux + uy * uy)); var nx = -uy / l * 16 * _s.Zoom; var ny = ux / l * 16 * _s.Zoom;
            ctx.DrawGeometry(B("#F5A52422"), gold, new PolylineGeometry([new Point(a.X + nx, a.Y + ny), new Point(b.X + nx, b.Y + ny), new Point(b.X - nx, b.Y - ny), new Point(a.X - nx, a.Y - ny)], true));
            if (ps.Kind != "masse") foreach (var q in new[] { a, b }) ctx.DrawRectangle(Brushes.White, gold, new Rect(q.X - 4, q.Y - 4, 8, 8), 2, 2);
        }
        // Vorschau beim Setzen
        if (_s.Tool != "auswahl" && _press is { } pa)
        {
            var pb = _grid == pa && _s.Tool != "draht" ? (pa.X + 4, pa.Y) : _grid; if (_s.Tool == "masse") pb = (pa.X, pa.Y + 1);
            var tmp = new Circuit(); try { tmp.Add(_s.Tool, pa.X, pa.Y, pb.Item1, pb.Item2); foreach (var sh in CircuitDrawing.Build(tmp, null, CircuitDrawing.Screen, false)) Draw(ctx, sh, 0.6); } catch (ArgumentException) { }
        }
        if (_moved) { var g = Screen(_grid.X * G, _grid.Y * G); ctx.DrawEllipse(null, new Pen(B("#F5A524"), 1.4), g, 5, 5); }
        // Oszilloskop
        if (ScopeHeight > 0 && _s.Selected is { } sid && c.Part(sid) is { } sp2) DrawScope(ctx, size, sp2);
        var info = st is null ? "" : $"t = {st.Time:0.000} s · {(_s.Running ? (_s.Speed >= 1 ? "läuft (Echtzeit)" : $"läuft ({1 / _s.Speed:0}× langsamer)") : "angehalten")}";
        if (info.Length > 0) Label(ctx, info, new Point(10, size.Height - 24 - ScopeHeight), "#C9D6EA", "#AA0A1628");
        var hint = _problem is { } pr ? "⚠ " + pr : _s.Hint.Length > 0 ? _s.Hint : ToolHint();
        if (hint.Length > 0) Label(ctx, hint, new Point(10, 10), "#FFFFFF", _problem is null ? "#CC14233D" : "#DDB91C1C");
        if (c.Parts.Count == 0 && _s.Tool == "auswahl")
        {
            var ft = new FormattedText("Leere Schaltung – links ein Bauteil wählen und von Punkt zu Punkt ziehen (Klick = Standardlänge).", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Face, 15, B("#9FB4D3"));
            ctx.DrawText(ft, new Point((size.Width - ft.Width) / 2, size.Height / 2 - 10));
        }
    }

    string ToolHint() => _s.Tool switch
    {
        "auswahl" => _s.Selected is not null && _c.Part(_s.Selected)?.Kind == "schalter" ? "Schalter nochmal anklicken schaltet um · R dreht · Entf löscht" : "",
        "draht" => "Draht: von Punkt zu Punkt ziehen – Punkte, die sich treffen, sind verbunden.",
        "masse" => "Masse (0 V): auf den Punkt klicken.",
        _ => $"{PartTypes.Get(_s.Tool).Label}: von Punkt zu Punkt ziehen oder klicken (4 Raster lang) · R dreht danach",
    };

    void DrawScope(DrawingContext ctx, Size size, Part p)
    {
        var h = ScopeHeight; var top = size.Height - h; var rect = new Rect(0, top, size.Width, h);
        ctx.FillRectangle(B("#E6050B16"), rect); ctx.DrawLine(new Pen(B("#335B7BA8"), 1), new Point(0, top), new Point(size.Width, top));
        var data = _s.Scope; var t0 = data[0].T; var t1 = data[^1].T; var span = Math.Max(1e-9, t1 - t0);
        var vmax = Math.Max(1e-9, data.Max(d => Math.Abs(d.V))); var imax = Math.Max(1e-12, data.Max(d => Math.Abs(d.I)));
        var mid = top + h / 2; ctx.DrawLine(new Pen(B("#22FFFFFF"), 1), new Point(0, mid), new Point(size.Width, mid));
        IEnumerable<Point> Pts(Func<(double T, double V, double I), double> f, double max) => data.Select(d => new Point(60 + (d.T - t0) / span * (size.Width - 70), mid - f(d) / max * (h / 2 - 12)));
        ctx.DrawGeometry(null, new Pen(B("#4ADE80"), 1.6), new PolylineGeometry(Pts(d => d.V, vmax).ToList(), false));
        ctx.DrawGeometry(null, new Pen(B("#FACC15"), 1.6), new PolylineGeometry(Pts(d => d.I, imax).ToList(), false));
        var last = data[^1];
        ctx.DrawText(new FormattedText($"{p.Name}  U {Trendo.Core.ElectronicsPlus.Fmt(last.V, "V")} (max {Trendo.Core.ElectronicsPlus.Fmt(vmax, "V")})", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Face, 11, B("#4ADE80")), new Point(8, top + 6));
        ctx.DrawText(new FormattedText($"I {Trendo.Core.ElectronicsPlus.Fmt(last.I, "A")} (max {Trendo.Core.ElectronicsPlus.Fmt(imax, "A")}) · {span * 1000:0.#} ms", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Face, 11, B("#FACC15")), new Point(8, top + h - 20));
    }

    void Draw(DrawingContext ctx, Shape sh, double alpha = 1)
    {
        using var _ = ctx.PushOpacity(alpha);
        switch (sh)
        {
            case DPoly q:
                if (q.Fill is null && q.Stroke is null) return;
                ctx.DrawGeometry(q.Fill is null ? null : B(q.Fill), q.Stroke is null ? null : new Pen(B(q.Stroke), Math.Max(1, q.Width * Math.Min(1.5, _s.Zoom))) { LineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round }, new PolylineGeometry(q.Pts.Select(v => Screen(v.X, v.Y)).ToList(), q.Closed));
                break;
            case DLine l: ctx.DrawLine(new Pen(B(l.Stroke), Math.Max(1, l.Width * Math.Min(1.5, _s.Zoom))) { LineCap = PenLineCap.Round }, Screen(l.X1, l.Y1), Screen(l.X2, l.Y2)); break;
            case DText t:
                var px = Math.Clamp(t.Size * _s.Zoom, 9, 15);
                var ft = new FormattedText(t.Text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, t.Bold ? new Typeface("Inter", FontStyle.Normal, FontWeight.SemiBold) : Face, px, B(t.Color));
                var c = Screen(t.X, t.Y); ctx.DrawText(ft, new Point(c.X - ft.Width / 2, c.Y - ft.Height / 2)); break;
        }
    }

    static void Label(DrawingContext ctx, string text, Point at, string color, string bg)
    {
        var ft = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Face, 12, B(color));
        ctx.DrawRectangle(B(bg), null, new Rect(at.X - 6, at.Y - 3, ft.Width + 12, ft.Height + 6), 6, 6); ctx.DrawText(ft, at);
    }
}
