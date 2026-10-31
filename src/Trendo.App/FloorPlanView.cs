using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Trendo.Core.Arch;

namespace Trendo.App;

/// <summary>Zustand der Zeichenfläche, der ein Neuzeichnen der Seite überlebt (Werkzeug, Auswahl, Ansicht, angefangene Wandkette).</summary>
public sealed class PlanViewState
{
    public string Tool = "auswahl";
    public string? Selected;
    public string ItemType = "sofa";
    public double Zoom = 0.9, PanX = -100, PanY = -100;
    public bool Fitted, Grid = true, Dims = true, Focus, UserView;
    public (double X, double Y)? Chain;
    public double WallThickness = 24;
    public string Hint = "";
}

/// <summary>T-0256: Die Zeichenfläche des Grundriss-Programms. Ziehen, Klicken, Doppelklick wie in einem Planungsprogramm:
/// Auswahl verschiebt Möbel, Türen, Fenster, Wände (mit angeschlossenen Ecken) und Raumecken; Wand zeichnet Wandzüge mit Fang und Längeneingabe;
/// Raum zieht ein Rechteck auf oder erkennt per Doppelklick die umschlossene Fläche; Tür/Fenster setzen in die nächste Wand; Möbel aus dem Katalog.
/// Mausrad zoomt am Zeiger, mittlere/rechte Maustaste oder Leertaste + Ziehen verschiebt.</summary>
public sealed class FloorPlanView : Control
{
    readonly FloorPlan _plan;
    readonly PlanViewState _s;
    readonly Action<string, Action<FloorPlan>> _commit;
    readonly Action<string?> _select;
    FloorPlan? _work; string _workLabel = "";
    (double X, double Y) _mouse; (double X, double Y)? _press; (double X, double Y) _snap; bool _snapped;
    Point? _panFrom; bool _space;
    string? _dragKind; int _dragHandle = -1;
    string _typed = "";

    public FloorPlanView(FloorPlan plan, PlanViewState state, Action<string, Action<FloorPlan>> commit, Action<string?> select)
    {
        _plan = plan; _s = state; _commit = commit; _select = select;
        ClipToBounds = true; Focusable = true; Cursor = new Cursor(StandardCursorType.Cross);
    }

    FloorPlan Shown => _work ?? _plan;
    (double X, double Y) World(Point p) => (p.X / _s.Zoom + _s.PanX, p.Y / _s.Zoom + _s.PanY);
    Point Screen(double x, double y) => new((x - _s.PanX) * _s.Zoom, (y - _s.PanY) * _s.Zoom);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_s.Focus) Avalonia.Threading.Dispatcher.UIThread.Post(() => Focus());
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if ((!_s.Fitted || !_s.UserView) && Bounds.Width > 10) { Fit(); _s.Fitted = true; } // bis man selbst zoomt/verschiebt, passt sich der Plan an
    }

    /// <summary>Ganzen Plan ins Bild holen.</summary>
    public void Fit()
    {
        var b = _plan.Bounds() ?? (0, 0, 800, 600);
        double w = Math.Max(200, b.MaxX - b.MinX + 160), h = Math.Max(200, b.MaxY - b.MinY + 160);
        var bw = Bounds.Width > 10 ? Bounds.Width : 900; var bh = Bounds.Height > 10 ? Bounds.Height : 600;
        _s.Zoom = Math.Clamp(Math.Min(bw / w, bh / h), 0.05, 8);
        _s.PanX = (b.MinX + b.MaxX) / 2 - bw / 2 / _s.Zoom; _s.PanY = (b.MinY + b.MaxY) / 2 - bh / 2 / _s.Zoom;
        InvalidateVisual();
    }

    // ---------- Eingabe ----------
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        var p = e.GetPosition(this); var (wx, wy) = World(p);
        _s.Zoom = Math.Clamp(_s.Zoom * (e.Delta.Y > 0 ? 1.15 : 1 / 1.15), 0.05, 8);
        _s.PanX = wx - p.X / _s.Zoom; _s.PanY = wy - p.Y / _s.Zoom; _s.UserView = true;
        InvalidateVisual(); e.Handled = true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus(); _s.Focus = true;
        var pt = e.GetCurrentPoint(this); var p = pt.Position; var w = World(p);
        if (pt.Properties.IsMiddleButtonPressed || _space || pt.Properties.IsRightButtonPressed && _s.Chain is null && _s.Tool != "wand")
        { _panFrom = p; e.Pointer.Capture(this); return; }
        if (pt.Properties.IsRightButtonPressed) { EndChain(); return; }
        Track(w, e.KeyModifiers);
        var dbl = e.ClickCount == 2;
        switch (_s.Tool)
        {
            case "wand":
                if (dbl) { EndChain(); return; }
                if (_s.Chain is { } c)
                {
                    var (ex, ey) = _snap; if (Dist(c, (ex, ey)) < 5) return;
                    Commit("Wand zeichnen", pl => pl.AddWall(c.X, c.Y, ex, ey, _s.WallThickness));
                    _s.Chain = (ex, ey); _typed = "";
                }
                else _s.Chain = _snap;
                InvalidateVisual(); return;
            case "raum":
                if (dbl)
                {
                    _press = null;
                    var name = NextRoomName(); var hit = false;
                    Commit("Raum erkennen", pl => hit = pl.DetectRoom(w.X, w.Y, name) is not null);
                    if (!hit) Hint("Hier ist keine geschlossene Fläche – Wände müssen den Raum ganz umschließen. Oder ein Rechteck aufziehen.");
                    return;
                }
                _press = _snap; e.Pointer.Capture(this); return;
            case "tuer": case "fenster":
                var kind = _s.Tool;
                Commit(kind == "tuer" ? "Tür einsetzen" : "Fenster einsetzen", pl => { if (pl.AddOpening(kind, w.X, w.Y) is { } o) _s.Selected = o.Id; else throw new InvalidOperationException("Bitte auf oder neben eine Wand klicken."); });
                return;
            case "moebel":
                var (ix, iy) = (Math.Round(w.X / 5) * 5, Math.Round(w.Y / 5) * 5); var type = _s.ItemType;
                Commit("Möbel stellen", pl => _s.Selected = pl.AddItem(type, ix, iy).Id);
                return;
        }
        // Auswahl: zuerst Griffe der aktuellen Auswahl, dann was darunter liegt
        _press = w; _work = null; _dragKind = null; _dragHandle = -1;
        if (_s.Selected is { } sel && Handles(_plan, sel).Select((h, i) => (h, i)).FirstOrDefault(x => Dist(x.h, w) <= 7 / _s.Zoom + 2) is { h: var hh, i: var hi } && Dist(hh, w) <= 7 / _s.Zoom + 2)
        { _dragKind = "handle"; _dragHandle = hi; e.Pointer.Capture(this); return; }
        var hitTest = _plan.HitTest(w.X, w.Y, 6 / _s.Zoom);
        var id = hitTest?.Id;
        if (id != _s.Selected) { _s.Selected = id; _select(id); }
        if (id is not null) { _dragKind = hitTest!.Value.Kind; e.Pointer.Capture(this); }
        InvalidateVisual();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var p = e.GetPosition(this);
        if (_panFrom is { } f) { _s.PanX -= (p.X - f.X) / _s.Zoom; _s.PanY -= (p.Y - f.Y) / _s.Zoom; _panFrom = p; _s.UserView = true; InvalidateVisual(); return; }
        var w = World(p); Track(w, e.KeyModifiers);
        if (_s.Tool == "auswahl" && _press is { } st && _s.Selected is { } sel && _dragKind is not null)
        {
            if (_work is null && Dist(st, w) * _s.Zoom < 3) return; // Klick, kein Ziehen
            _work = _plan.Clone();
            var (dx, dy) = (Snap5(w.X - st.X), Snap5(w.Y - st.Y));
            switch (_dragKind)
            {
                case "handle": DragHandle(_work, sel, _dragHandle); _workLabel = "Ecke verschieben"; break;
                case "item": if (_work.Items.First(i => i.Id == sel) is var it) { var o = _plan.Items.First(i => i.Id == sel); it.X = o.X + dx; it.Y = o.Y + dy; } _workLabel = "Möbel verschieben"; break;
                case "opening":
                    var op = _work.Openings.First(o => o.Id == sel); var wall = _work.WallOf(op.WallId)!;
                    op.At = Math.Clamp(Math.Round(wall.Project(w.X, w.Y).T), op.Width / 2 + 5, wall.Length - op.Width / 2 - 5); _workLabel = "Öffnung verschieben"; break;
                case "wall": _work.MoveWall(sel, dx, dy); _workLabel = "Wand verschieben"; break;
                case "room":
                    var r = _work.Rooms.First(x => x.Id == sel); var r0 = _plan.Rooms.First(x => x.Id == sel);
                    r.Points = r0.Points.Select(q => new PlanPoint(q.X + dx, q.Y + dy)).ToList(); _workLabel = "Raum verschieben"; break;
            }
        }
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        e.Pointer.Capture(null);
        if (_panFrom is not null) { _panFrom = null; return; }
        if (_s.Tool == "raum" && _press is { } a)
        {
            _press = null; var (bx, by) = _snap;
            if (Math.Abs(bx - a.X) * _s.Zoom < 4 && Math.Abs(by - a.Y) * _s.Zoom < 4) { InvalidateVisual(); return; }
            var name = NextRoomName(); var t = _s.WallThickness;
            Commit("Raum aufziehen", pl => _s.Selected = pl.AddRectRoom(a.X, a.Y, bx, by, name, "wohnen", t).Id);
            return;
        }
        _press = null;
        if (_work is { } done)
        {
            _work = null;
            Commit(_workLabel, pl => { pl.Walls = done.Walls; pl.Rooms = done.Rooms; pl.Items = done.Items; pl.Openings = done.Openings; });
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Space) { _space = true; e.Handled = true; return; }
        if (e.Key == Key.Escape) { if (_s.Chain is not null) EndChain(); else { _s.Tool = "auswahl"; _s.Selected = null; _select(null); } e.Handled = true; return; }
        // Längeneingabe beim Wandzeichnen: Zahl tippen (cm, oder m mit Komma), Enter
        if (_s.Tool == "wand" && _s.Chain is { } c)
        {
            var ch = e.Key switch { >= Key.D0 and <= Key.D9 => (char)('0' + (e.Key - Key.D0)), >= Key.NumPad0 and <= Key.NumPad9 => (char)('0' + (e.Key - Key.NumPad0)), Key.OemComma or Key.Decimal or Key.OemPeriod => ',', _ => '\0' };
            if (ch != '\0') { _typed += ch; InvalidateVisual(); e.Handled = true; return; }
            if (e.Key == Key.Back && _typed.Length > 0) { _typed = _typed[..^1]; InvalidateVisual(); e.Handled = true; return; }
            if (e.Key == Key.Enter && TypedLength() is { } len)
            {
                var (dx, dy) = (_snap.X - c.X, _snap.Y - c.Y); var l = Math.Sqrt(dx * dx + dy * dy); if (l < 1) (dx, dy, l) = (1, 0, 1);
                var end = (c.X + dx / l * len, c.Y + dy / l * len);
                Commit("Wand zeichnen", pl => pl.AddWall(c.X, c.Y, end.Item1, end.Item2, _s.WallThickness));
                _s.Chain = end; _typed = ""; e.Handled = true; return;
            }
        }
        if (_s.Selected is not { } sel) return;
        if (e.Key is Key.Delete or Key.Back) { _s.Selected = null; Commit("Löschen", pl => pl.Remove(sel)); e.Handled = true; return; }
        if (_plan.Items.FirstOrDefault(i => i.Id == sel) is { } item)
        {
            if (e.Key == Key.R) { Commit("Drehen", pl => { var i = pl.Items.First(x => x.Id == sel); i.Rot = (i.Rot + 90) % 360; }); e.Handled = true; return; }
            var step = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 10 : 1;
            var (mx, my) = e.Key switch { Key.Left => (-step, 0), Key.Right => (step, 0), Key.Up => (0, -step), Key.Down => (0, step), _ => (0, 0) };
            if (mx != 0 || my != 0) { Commit("Möbel verschieben", pl => { var i = pl.Items.First(x => x.Id == sel); i.X += mx; i.Y += my; }); e.Handled = true; }
        }
    }

    protected override void OnKeyUp(KeyEventArgs e) { base.OnKeyUp(e); if (e.Key == Key.Space) _space = false; }

    double? TypedLength() => double.TryParse(_typed, NumberStyles.Float, CultureInfo.GetCultureInfo("de-DE"), out var v) && v > 0 ? (_typed.Contains(',') ? v * 100 : v) : null;

    void EndChain() { _s.Chain = null; _typed = ""; InvalidateVisual(); }
    void Hint(string h) { _s.Hint = h; InvalidateVisual(); }
    void Commit(string label, Action<FloorPlan> a) => _commit(label, a);
    string NextRoomName() => $"Raum {_plan.Rooms.Count + 1}";
    static double Snap5(double v) => Math.Round(v / 5) * 5;
    static double Dist((double X, double Y) a, (double X, double Y) b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    bool _moved;
    void Track((double X, double Y) w, KeyModifiers mods)
    {
        _mouse = w; _moved = true;
        var grid = _s.Grid ? 10 : 0;
        var (x, y, snapped) = _plan.Snap(w.X, w.Y, grid, 10 / _s.Zoom + 3);
        if (_s.Tool == "wand" && _s.Chain is { } c && !snapped && !mods.HasFlag(KeyModifiers.Shift)) (x, y) = FloorPlan.AxisLock(c.X, c.Y, x, y);
        _snap = (x, y); _snapped = snapped;
    }

    /// <summary>Griffe der Auswahl: Wandenden bzw. Raumecken.</summary>
    static List<(double X, double Y)> Handles(FloorPlan p, string id)
    {
        if (p.WallOf(id) is { } w) return [(w.X1, w.Y1), (w.X2, w.Y2)];
        if (p.Rooms.FirstOrDefault(r => r.Id == id) is { } r) return r.Points.Select(q => (q.X, q.Y)).ToList();
        return [];
    }

    void DragHandle(FloorPlan p, string id, int k)
    {
        var (x, y) = _snap;
        if (p.WallOf(id) is not null) p.MoveWallEnd(id, k == 0, x, y);
        else if (p.Rooms.FirstOrDefault(r => r.Id == id) is { } r && k < r.Points.Count) r.Points[k] = new PlanPoint(x, y);
    }

    // ---------- Zeichnen ----------
    static readonly Typeface Face = new("Inter");
    static IBrush B(string hex, byte alpha = 255) { var c = Color.Parse(hex); return new SolidColorBrush(Color.FromArgb(alpha, c.R, c.G, c.B)); }

    public override void Render(DrawingContext ctx)
    {
        var size = Bounds.Size; if (size.Width < 2) return;
        var t = PlanTheme.Screen;
        ctx.FillRectangle(new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Color.Parse("#FF102038"), 0), new GradientStop(Color.Parse("#FF0A1628"), 1) } }, new Rect(size));
        if (_s.Grid) DrawGrid(ctx, size);
        var plan = Shown;
        foreach (var sh in PlanDrawing.Build(plan, t, _s.Dims)) Draw(ctx, sh, sh.Layer == "RAEUME" ? (byte)200 : (byte)255);
        // Auswahl
        if (_s.Selected is { } sel) DrawSelection(ctx, plan, sel);
        DrawToolPreview(ctx, plan);
        // Fadenkreuz-Fang
        if (_moved)
        {
            var sp = Screen(_snap.X, _snap.Y);
            var snapPen = new Pen(B("#F5A524"), 1.2);
            if (_snapped) ctx.DrawRectangle(null, snapPen, new Rect(sp.X - 5, sp.Y - 5, 10, 10));
            else ctx.DrawEllipse(null, snapPen, sp, 3, 3);
        }
        // Kopfzeile: Koordinaten, Maßstab, Hinweis
        var info = $"x {_snap.X / 100:0.00} m · y {_snap.Y / 100:0.00} m · {_s.Zoom * 100 / 1:0} px/m";
        Label(ctx, info, new Point(10, size.Height - 24), "#C9D6EA", 11, "#AA0A1628");
        var hint = _s.Hint.Length > 0 ? _s.Hint : ToolHint();
        if (hint.Length > 0) Label(ctx, hint, new Point(10, 10), "#F2F6FC", 12, "#CC14233D");
        if (plan.Walls.Count == 0 && plan.Rooms.Count == 0 && _s.Tool == "auswahl")
        {
            var ft = new FormattedText("Leerer Plan – links „Raum“ wählen und ein Rechteck aufziehen, oder „Wand“ für einzelne Wände.", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Face, 15, B("#9FB4D3"));
            ctx.DrawText(ft, new Point((size.Width - ft.Width) / 2, size.Height / 2 - 10));
        }
    }

    string ToolHint() => _s.Tool switch
    {
        "wand" => _s.Chain is null ? "Wand: Startpunkt klicken (fängt an Ecken und am 10-cm-Raster)." : $"Nächster Punkt klicken · Länge tippen + Enter{(_typed.Length > 0 ? $": {_typed}" : "")} · Doppelklick, Esc oder rechte Maustaste beendet · Umschalt: freier Winkel",
        "raum" => "Raum: Rechteck aufziehen (lichtes Innenmaß, Wände entstehen außen) · Doppelklick in umschlossene Fläche erkennt den Raum",
        "tuer" => "Tür: auf eine Wand klicken – Breite, Anschlag und Richtung rechts einstellen",
        "fenster" => "Fenster: auf eine Wand klicken – Maße rechts einstellen",
        "moebel" => $"Möbel: „{FurnitureCatalog.Get(_s.ItemType)?.Name}“ hinklicken · R dreht, Pfeiltasten schieben",
        _ => "",
    };

    void DrawGrid(DrawingContext ctx, Size size)
    {
        var (x0, y0) = World(new Point(0, 0)); var (x1, y1) = World(new Point(size.Width, size.Height));
        var minor = _s.Zoom * 10 >= 6 ? 10.0 : _s.Zoom * 50 >= 6 ? 50 : 100;
        var pm = new Pen(B("#FFFFFF", 14), 1); var pM = new Pen(B("#7FD3FF", 34), 1);
        for (var x = Math.Floor(x0 / minor) * minor; x <= x1; x += minor) { var a = Screen(x, 0).X; ctx.DrawLine(Math.Abs(x % 100) < 0.01 ? pM : pm, new Point(a, 0), new Point(a, size.Height)); }
        for (var y = Math.Floor(y0 / minor) * minor; y <= y1; y += minor) { var a = Screen(0, y).Y; ctx.DrawLine(Math.Abs(y % 100) < 0.01 ? pM : pm, new Point(0, a), new Point(size.Width, a)); }
    }

    void Draw(DrawingContext ctx, Shape sh, byte alpha = 255)
    {
        switch (sh)
        {
            case DPoly q:
                var pts = q.Pts.Select(v => Screen(v.X, v.Y)).ToList();
                ctx.DrawGeometry(q.Fill is null ? null : B(q.Fill, alpha), q.Stroke is null ? null : new Pen(B(q.Stroke), Math.Max(1, q.Width * Math.Min(1.6, _s.Zoom))), new PolylineGeometry(pts, q.Closed));
                break;
            case DLine l: ctx.DrawLine(new Pen(B(l.Stroke), Math.Max(1, l.Width * Math.Min(1.6, _s.Zoom))), Screen(l.X1, l.Y1), Screen(l.X2, l.Y2)); break;
            case DArc a:
                var n = 24; var arc = Enumerable.Range(0, n + 1).Select(k => { var ang = (a.StartDeg + a.SweepDeg * k / n) * Math.PI / 180; return Screen(a.Cx + a.R * Math.Cos(ang), a.Cy + a.R * Math.Sin(ang)); }).ToList();
                ctx.DrawGeometry(null, new Pen(B(a.Stroke, 200), 1, new DashStyle([4, 3], 0)), new PolylineGeometry(arc, false)); break;
            case DText x:
                var px = x.Size * _s.Zoom;
                if (x.Layer is "TEXT" or "MASSE") px = Math.Max(px, x.Layer == "TEXT" ? 11 : 10); // Raumnamen und Maße bleiben lesbar
                else if (px < 7) return;
                var ft = new FormattedText(x.Text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, x.Bold ? new Typeface("Inter", FontStyle.Normal, FontWeight.SemiBold) : Face, Math.Min(px, 22), B(x.Color));
                var c = Screen(x.X, x.Y) + new Point(0, x.Line * Math.Min(px, 22) * 0.62);
                using (ctx.PushTransform(Matrix.CreateTranslation(-ft.Width / 2, -ft.Height / 2) * Matrix.CreateRotation(x.AngleDeg * Math.PI / 180) * Matrix.CreateTranslation(c.X, c.Y)))
                    ctx.DrawText(ft, new Point(0, 0));
                break;
        }
    }

    void DrawSelection(DrawingContext ctx, FloorPlan p, string sel)
    {
        var gold = new Pen(B("#F5A524"), 2); var fill = B("#F5A524", 40);
        List<(double X, double Y)>? outline = null;
        if (p.WallOf(sel) is { } w) outline = w.Outline();
        else if (p.Items.FirstOrDefault(i => i.Id == sel) is { } it) outline = it.Corners();
        else if (p.Rooms.FirstOrDefault(r => r.Id == sel) is { } r) outline = r.Points.Select(q => (q.X, q.Y)).ToList();
        else if (p.Openings.FirstOrDefault(o => o.Id == sel) is { } o && p.WallOf(o.WallId) is { } ow)
        {
            var (ux, uy) = ow.Dir; var (nx, ny) = (-uy * (ow.Thickness / 2 + 4), ux * (ow.Thickness / 2 + 4)); var (cx, cy) = ow.PointAt(o.At);
            var (ax, ay, bx, by) = (cx - ux * o.Width / 2, cy - uy * o.Width / 2, cx + ux * o.Width / 2, cy + uy * o.Width / 2);
            outline = [(ax + nx, ay + ny), (bx + nx, by + ny), (bx - nx, by - ny), (ax - nx, ay - ny)];
        }
        if (outline is not null) ctx.DrawGeometry(fill, gold, new PolylineGeometry(outline.Select(v => Screen(v.X, v.Y)).ToList(), true));
        foreach (var (hx, hy) in Handles(p, sel)) { var s = Screen(hx, hy); ctx.DrawRectangle(Brushes.White, gold, new Rect(s.X - 5, s.Y - 5, 10, 10), 2, 2); }
        if (p.WallOf(sel) is { } sw) { var m = sw.PointAt(sw.Length / 2); Label(ctx, FloorPlan.Cm(sw.Length), Screen(m.X, m.Y) + new Point(8, -26), "#0A1628", 12, "#F5A524"); }
    }

    void DrawToolPreview(DrawingContext ctx, FloorPlan p)
    {
        var gold = new Pen(B("#F5A524"), 2, new DashStyle([6, 4], 0));
        switch (_s.Tool)
        {
            case "wand" when _s.Chain is { } c:
                var end = _snap;
                if (TypedLength() is { } len) { var (dx, dy) = (end.X - c.X, end.Y - c.Y); var l = Math.Max(1, Math.Sqrt(dx * dx + dy * dy)); end = (c.X + dx / l * len, c.Y + dy / l * len); }
                var prev = new Wall { X1 = c.X, Y1 = c.Y, X2 = end.X, Y2 = end.Y, Thickness = _s.WallThickness };
                if (prev.Length >= 1)
                {
                    ctx.DrawGeometry(B("#F5A524", 70), gold, new PolylineGeometry(prev.Outline().Select(v => Screen(v.X, v.Y)).ToList(), true));
                    var ang = Math.Atan2(-(end.Y - c.Y), end.X - c.X) * 180 / Math.PI; if (ang < 0) ang += 360;
                    Label(ctx, $"{FloorPlan.Cm(prev.Length)} · {ang:0}°{(_typed.Length > 0 ? $"  ⌨ {_typed}" : "")}", Screen(end.X, end.Y) + new Point(12, 10), "#0A1628", 12, "#F5A524");
                }
                break;
            case "raum" when _press is { } a:
                var (bx, by) = _snap;
                var r = new[] { (a.X, a.Y), (bx, a.Y), (bx, by), (a.X, by) };
                ctx.DrawGeometry(B("#7FD3FF", 50), gold, new PolylineGeometry(r.Select(v => Screen(v.Item1, v.Item2)).ToList(), true));
                Label(ctx, $"{FloorPlan.Cm(Math.Abs(bx - a.X))} × {FloorPlan.Cm(Math.Abs(by - a.Y))} = {Math.Abs(bx - a.X) * Math.Abs(by - a.Y) / 10000:0.00} m²", Screen(bx, by) + new Point(12, 10), "#0A1628", 12, "#F5A524");
                break;
            case "tuer": case "fenster":
                var near = p.Walls.Select(x => (W: x, P: x.Project(_mouse.X, _mouse.Y))).Where(x => x.P.Dist <= 60).OrderBy(x => x.P.Dist).FirstOrDefault();
                if (near.W is { } nw)
                {
                    var width = _s.Tool == "fenster" ? 120 : 88.5; var at = Math.Clamp(near.P.T, width / 2 + 5, Math.Max(width / 2 + 5, nw.Length - width / 2 - 5));
                    var (ux, uy) = nw.Dir; var (cx, cy) = nw.PointAt(at); var h = nw.Thickness / 2 + 3; var (nx, ny) = (-uy * h, ux * h);
                    var (ax, ay, ex, ey) = (cx - ux * width / 2, cy - uy * width / 2, cx + ux * width / 2, cy + uy * width / 2);
                    ctx.DrawGeometry(B("#F5A524", 90), gold, new PolylineGeometry(new[] { (ax + nx, ay + ny), (ex + nx, ey + ny), (ex - nx, ey - ny), (ax - nx, ay - ny) }.Select(v => Screen(v.Item1, v.Item2)).ToList(), true));
                }
                break;
            case "moebel" when FurnitureCatalog.Get(_s.ItemType) is { } f:
                var ghost = new Item { Type = f.Type, Name = f.Name, X = Math.Round(_mouse.X / 5) * 5, Y = Math.Round(_mouse.Y / 5) * 5, W = f.W, D = f.D };
                ctx.DrawGeometry(B("#8CC8FF", 60), gold, new PolylineGeometry(ghost.Corners().Select(v => Screen(v.X, v.Y)).ToList(), true));
                break;
        }
    }

    static void Label(DrawingContext ctx, string text, Point at, string color, double size, string bg)
    {
        var ft = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Face, size, B(color));
        ctx.DrawRectangle(B(bg[..1] == "#" && bg.Length == 9 ? "#" + bg[3..] : bg, bg.Length == 9 ? Convert.ToByte(bg[1..3], 16) : (byte)255), null, new Rect(at.X - 6, at.Y - 3, ft.Width + 12, ft.Height + 6), 6, 6);
        ctx.DrawText(ft, at);
    }
}
