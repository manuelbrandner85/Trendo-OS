using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Trendo.Core.Arch;
using Trendo.Core.Statik;

namespace Trendo.App;

/// <summary>Zustand der Tragwerks-Zeichenfläche (überlebt das Neuzeichnen der Seite).</summary>
public sealed class FrameViewState
{
    public string Tool = "auswahl";
    public string? Selected;
    public string SupportType = "fest";
    public string LoadKind = "strecke";
    public double LoadValue = 10;
    public string Section = "IPE 200";
    public string Material = "Baustahl S235";
    public double Zoom = 0.8, PanX = -100, PanY = -400; // px je cm, Ansicht in cm (y nach unten)
    public bool Fitted, UserView, Grid = true, Focus;
    public (double X, double Y)? Chain;
    public FrameView Show = new();
    public string Hint = "";
}

/// <summary>T-0258: Zeichenfläche des Programms „Tragwerk & Mechanik“ – Stäbe zeichnen (Fang am 10-cm-Raster und an Knoten, 15°-Winkel, Länge tippen),
/// Lager und Lasten per Klick, Gelenke an Stabenden, Knoten ziehen; Ergebnisse (Momente, Querkraft, Normalkraft, Biegelinie, Reaktionen) sofort.</summary>
public sealed class FrameCanvas : Control
{
    readonly FrameModel _m; readonly FrameResult? _r; readonly string? _error;
    readonly FrameViewState _s; readonly Action<string, Action<FrameModel>> _commit; readonly Action<string?> _select;
    FrameModel? _work; (double X, double Y) _mouse, _snap; bool _snapped, _moved; (double X, double Y)? _press; Point? _panFrom; string _typed = "";

    public FrameCanvas(FrameModel m, FrameResult? r, string? error, FrameViewState s, Action<string, Action<FrameModel>> commit, Action<string?> select)
    {
        _m = m; _r = r; _error = error; _s = s; _commit = commit; _select = select;
        ClipToBounds = true; Focusable = true; Cursor = new Cursor(StandardCursorType.Cross);
    }

    // Weltkoordinaten = Modell in m (y oben); Ansicht in cm (y unten)
    (double X, double Y) Model(Point p) => ((p.X / _s.Zoom + _s.PanX) / 100, -(p.Y / _s.Zoom + _s.PanY) / 100);
    Point Screen(double cmX, double cmY) => new((cmX - _s.PanX) * _s.Zoom, (cmY - _s.PanY) * _s.Zoom);
    Point ScreenM(double x, double y) => Screen(x * 100, -y * 100);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e) { base.OnAttachedToVisualTree(e); if (_s.Focus) Avalonia.Threading.Dispatcher.UIThread.Post(() => Focus()); }
    protected override void OnSizeChanged(SizeChangedEventArgs e) { base.OnSizeChanged(e); if ((!_s.Fitted || !_s.UserView) && Bounds.Width > 10) { Fit(); _s.Fitted = true; } }

    public void Fit()
    {
        var (x0, y0, x1, y1) = FrameDrawing.Extent(FrameDrawing.Build(_m, _r, _s.Show, FrameDrawing.Screen));
        if (_m.Nodes.Count == 0) (x0, y0, x1, y1) = (-100, -400, 900, 100);
        double w = Math.Max(200, x1 - x0 + 160), h = Math.Max(200, y1 - y0 + 160);
        var bw = Bounds.Width > 10 ? Bounds.Width : 900; var bh = Bounds.Height > 10 ? Bounds.Height : 600;
        _s.Zoom = Math.Clamp(Math.Min(bw / w, bh / h), 0.02, 8);
        _s.PanX = (x0 + x1) / 2 - bw / 2 / _s.Zoom; _s.PanY = (y0 + y1) / 2 - bh / 2 / _s.Zoom;
        InvalidateVisual();
    }

    void Commit(string label, Action<FrameModel> a) => _commit(label, a);

    void Track(Point p, KeyModifiers mods)
    {
        var (x, y) = Model(p); _mouse = (x, y); _moved = true;
        var tol = 12 / _s.Zoom / 100; // 12 px in m
        var near = _m.Nodes.Select(n => (n, d: Math.Sqrt((n.X - x) * (n.X - x) + (n.Y - y) * (n.Y - y)))).Where(t => t.d < tol).OrderBy(t => t.d).FirstOrDefault();
        if (near.n is { } nn) { _snap = (nn.X, nn.Y); _snapped = true; return; }
        _snapped = false;
        (x, y) = _s.Grid ? (Math.Round(x * 10) / 10, Math.Round(y * 10) / 10) : (x, y);
        if (_s.Tool == "stab" && _s.Chain is { } c && !mods.HasFlag(KeyModifiers.Shift))
        {
            var (ax, ay) = FloorPlan.AngleSnap(c.X * 100, c.Y * 100, x * 100, y * 100); (x, y) = (Math.Round(ax) / 100, Math.Round(ay) / 100);
        }
        _snap = (x, y);
    }

    /// <summary>Was liegt unter dem Zeiger: Last, Knoten, Stab.</summary>
    (string Kind, string Id)? Hit((double X, double Y) w)
    {
        var tol = 10 / _s.Zoom / 100;
        foreach (var sh in FrameDrawing.Build(_m, null, _s.Show, FrameDrawing.Screen).OfType<DPoly>().Where(p => p.Layer == "LASTEN" && p.Id is not null))
            if (sh.Pts.Any(q => Math.Abs(q.X / 100 - w.X) < tol * 2 && Math.Abs(-q.Y / 100 - w.Y) < tol * 2)) return ("last", sh.Id!);
        foreach (var n in _m.Nodes) if (Math.Abs(n.X - w.X) < tol && Math.Abs(n.Y - w.Y) < tol) return ("knoten", n.Id);
        foreach (var m in _m.Members)
        {
            var a = _m.Node(m.A)!; var b = _m.Node(m.B)!;
            if (Geo.SegDist(w.X, w.Y, a.X, a.Y, b.X, b.Y) < tol) return ("stab", m.Id);
        }
        // Streckenlast: Klick auf den Lastbalken über dem Stab
        foreach (var l in _m.Loads.Where(l => l.Kind == "strecke"))
        {
            var m = _m.Member(l.Target); if (m is null) continue; var a = _m.Node(m.A)!; var b = _m.Node(m.B)!; var h = Math.Clamp(((_m.Bounds() is { } bb ? Math.Max(bb.MaxX - bb.MinX, bb.MaxY - bb.MinY) : 5) * 100 / 40), 6, 30) * 2.2 / 100;
            if (Geo.SegDist(w.X, w.Y, a.X, a.Y + h, b.X, b.Y + h) < tol * 1.5) return ("last", l.Id);
        }
        return null;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e); var p = e.GetPosition(this);
        var cx = p.X / _s.Zoom + _s.PanX; var cy = p.Y / _s.Zoom + _s.PanY;
        _s.Zoom = Math.Clamp(_s.Zoom * (e.Delta.Y > 0 ? 1.15 : 1 / 1.15), 0.02, 8); _s.PanX = cx - p.X / _s.Zoom; _s.PanY = cy - p.Y / _s.Zoom; _s.UserView = true;
        InvalidateVisual(); e.Handled = true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e); Focus(); _s.Focus = true;
        var pt = e.GetCurrentPoint(this); var p = pt.Position;
        if (pt.Properties.IsMiddleButtonPressed || pt.Properties.IsRightButtonPressed && _s.Chain is null) { _panFrom = p; e.Pointer.Capture(this); return; }
        if (pt.Properties.IsRightButtonPressed) { _s.Chain = null; _typed = ""; InvalidateVisual(); return; }
        Track(p, e.KeyModifiers); var w = Model(p); var hit = Hit(w);
        switch (_s.Tool)
        {
            case "stab":
                if (e.ClickCount == 2) { _s.Chain = null; InvalidateVisual(); return; }
                if (_s.Chain is { } c)
                {
                    var end = _snap; if (Math.Abs(end.X - c.X) + Math.Abs(end.Y - c.Y) < 0.05) return;
                    var (sec, mat) = (_s.Section, _s.Material);
                    Commit("Stab zeichnen", f => _s.Selected = f.AddMember(c.X, c.Y, end.X, end.Y, sec, mat).Id);
                    _s.Chain = end; _typed = "";
                }
                else _s.Chain = _snap;
                InvalidateVisual(); return;
            case "lager":
                if (hit is ("knoten", var nid)) { var type = _s.SupportType; Commit("Lager setzen", f => { var n = f.Node(nid)!; n.Support = n.Support == type ? "" : type; _s.Selected = n.Id; }); }
                else if (hit is ("stab", var sid)) { var type = _s.SupportType; var pos = w; Commit("Lager setzen", f => { var m = f.Member(sid)!; var a = f.Node(m.A)!; var b = f.Node(m.B)!; var t = Proj(a, b, pos); var n = f.SplitMember(sid, t); n.Support = type; _s.Selected = n.Id; }); }
                else Hint("Lager: auf einen Knoten (oder Stab) klicken.");
                return;
            case "last":
                var v = _s.LoadValue;
                if (hit is ("knoten", var kn)) Commit("Last setzen", f => _s.Selected = f.AddNodeLoad(kn, v).Id);
                else if (hit is ("stab", var st)) { var kind = _s.LoadKind; var pos = w; Commit("Last setzen", f => { var m = f.Member(st)!; _s.Selected = kind == "einzel" ? f.AddPointLoad(st, v, Math.Round(Proj(f.Node(m.A)!, f.Node(m.B)!, pos) * 20) / 20).Id : f.AddLineLoad(st, v).Id; }); }
                else Hint("Last: auf einen Stab (Strecken- oder Einzellast) oder einen Knoten klicken.");
                return;
            case "gelenk":
                if (hit is ("stab", var gs)) { var pos = w; Commit("Gelenk", f => { var m = f.Member(gs)!; var t = Proj(f.Node(m.A)!, f.Node(m.B)!, pos); if (t < 0.5) m.HingeA = !m.HingeA; else m.HingeB = !m.HingeB; _s.Selected = m.Id; }); }
                else Hint("Gelenk: nahe am Stabende auf den Stab klicken.");
                return;
        }
        // Auswahl
        var id = hit?.Id;
        if (id != _s.Selected) { _s.Selected = id; _select(id); }
        if (hit is ("knoten", _)) { _press = _snap; e.Pointer.Capture(this); }
        InvalidateVisual();
    }

    static double Proj(FNode a, FNode b, (double X, double Y) p)
    {
        var dx = b.X - a.X; var dy = b.Y - a.Y; var l2 = dx * dx + dy * dy; if (l2 < 1e-12) return 0;
        return Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / l2, 0, 1);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e); var p = e.GetPosition(this);
        if (_panFrom is { } f) { _s.PanX -= (p.X - f.X) / _s.Zoom; _s.PanY -= (p.Y - f.Y) / _s.Zoom; _panFrom = p; _s.UserView = true; InvalidateVisual(); return; }
        Track(p, e.KeyModifiers);
        if (_s.Tool == "auswahl" && _press is not null && _s.Selected is { } sel && _m.Node(sel) is not null)
        {
            _work = _m.Clone(); var n = _work.Node(sel)!; n.X = _snap.X; n.Y = _snap.Y;
        }
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e); e.Pointer.Capture(null);
        if (_panFrom is not null) { _panFrom = null; return; }
        _press = null;
        if (_work is { } w && _s.Selected is { } sel)
        {
            var n = w.Node(sel)!; var (x, y) = (n.X, n.Y); _work = null;
            Commit("Knoten verschieben", f => { var k = f.Node(sel)!; k.X = x; k.Y = y; });
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape) { if (_s.Chain is not null) { _s.Chain = null; _typed = ""; InvalidateVisual(); } else { _s.Tool = "auswahl"; _s.Selected = null; _select(null); } e.Handled = true; return; }
        if (_s.Tool == "stab" && _s.Chain is { } c)
        {
            var ch = e.Key switch { >= Key.D0 and <= Key.D9 => (char)('0' + (e.Key - Key.D0)), >= Key.NumPad0 and <= Key.NumPad9 => (char)('0' + (e.Key - Key.NumPad0)), Key.OemComma or Key.Decimal or Key.OemPeriod => ',', _ => '\0' };
            if (ch != '\0') { _typed += ch; InvalidateVisual(); e.Handled = true; return; }
            if (e.Key == Key.Back && _typed.Length > 0) { _typed = _typed[..^1]; InvalidateVisual(); e.Handled = true; return; }
            if (e.Key == Key.Enter && double.TryParse(_typed, NumberStyles.Float, CultureInfo.GetCultureInfo("de-DE"), out var len) && len > 0)
            {
                var (dx, dy) = (_snap.X - c.X, _snap.Y - c.Y); var l = Math.Sqrt(dx * dx + dy * dy); if (l < 1e-6) (dx, dy, l) = (1, 0, 1);
                var end = (Math.Round(c.X + dx / l * len, 3), Math.Round(c.Y + dy / l * len, 3)); var (sec, mat) = (_s.Section, _s.Material);
                Commit("Stab zeichnen", f => _s.Selected = f.AddMember(c.X, c.Y, end.Item1, end.Item2, sec, mat).Id);
                _s.Chain = end; _typed = ""; e.Handled = true; return;
            }
        }
        if (_s.Selected is { } sel && e.Key is Key.Delete or Key.Back) { _s.Selected = null; Commit("Löschen", f => f.Remove(sel)); e.Handled = true; }
    }

    void Hint(string h) { _s.Hint = h; InvalidateVisual(); }

    // ---------- Zeichnen ----------
    static readonly Typeface Face = new("Inter");
    static IBrush B(string hex) => new SolidColorBrush(Color.Parse(hex.Length == 9 ? "#" + hex[7..] + hex[1..7] : hex));

    public override void Render(DrawingContext ctx)
    {
        var size = Bounds.Size; if (size.Width < 2) return;
        ctx.FillRectangle(new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Color.Parse("#FF102038"), 0), new GradientStop(Color.Parse("#FF0A1628"), 1) } }, new Rect(size));
        if (_s.Grid)
        {
            var step = _s.Zoom * 50 >= 8 ? 50.0 : _s.Zoom * 100 >= 8 ? 100 : 500;
            var x0 = Math.Floor(_s.PanX / step) * step; var y0 = Math.Floor(_s.PanY / step) * step;
            var pm = new Pen(new SolidColorBrush(Color.FromArgb(14, 255, 255, 255)), 1); var pM = new Pen(new SolidColorBrush(Color.FromArgb(38, 127, 211, 255)), 1);
            for (var x = x0; (x - _s.PanX) * _s.Zoom < size.Width; x += step) { var a = Screen(x, 0).X; ctx.DrawLine(Math.Abs(x % 100) < 0.01 ? pM : pm, new Point(a, 0), new Point(a, size.Height)); }
            for (var y = y0; (y - _s.PanY) * _s.Zoom < size.Height; y += step) { var a = Screen(0, y).Y; ctx.DrawLine(Math.Abs(y % 100) < 0.01 ? pM : pm, new Point(0, a), new Point(size.Width, a)); }
        }
        var model = _work ?? _m;
        foreach (var sh in FrameDrawing.Build(model, _work is null ? _r : null, _s.Show, FrameDrawing.Screen)) Draw(ctx, sh);
        // Auswahl
        if (_s.Selected is { } sel)
        {
            var gold = new Pen(B("#F5A524"), 3);
            if (model.Node(sel) is { } n) ctx.DrawEllipse(null, gold, ScreenM(n.X, n.Y), 9, 9);
            else if (model.Member(sel) is { } m) { var a = model.Node(m.A)!; var b = model.Node(m.B)!; ctx.DrawLine(new Pen(B("#F5A524"), 8) { LineCap = PenLineCap.Round }, ScreenM(a.X, a.Y), ScreenM(b.X, b.Y)); foreach (var sh in FrameDrawing.Build(model, _r, _s.Show, FrameDrawing.Screen).Where(s => s is DPoly { Layer: "STAEBE" } p && p.Id == m.Id)) Draw(ctx, sh); }
            else if (model.Loads.FirstOrDefault(l => l.Id == sel) is { } l)
                foreach (var sh in FrameDrawing.Build(model, null, _s.Show, FrameDrawing.Screen).OfType<DPoly>().Where(p => p.Id == l.Id)) ctx.DrawGeometry(B("#F5A524"), gold, new PolylineGeometry(sh.Pts.Select(q => Screen(q.X, q.Y)).ToList(), sh.Closed));
        }
        // Vorschau Stab
        if (_s.Tool == "stab" && _s.Chain is { } c)
        {
            var end = _snap;
            if (double.TryParse(_typed, NumberStyles.Float, CultureInfo.GetCultureInfo("de-DE"), out var len) && len > 0) { var dx = end.X - c.X; var dy = end.Y - c.Y; var l = Math.Max(1e-6, Math.Sqrt(dx * dx + dy * dy)); end = (c.X + dx / l * len, c.Y + dy / l * len); }
            ctx.DrawLine(new Pen(B("#F5A524"), 4, new DashStyle([3, 2], 0)), ScreenM(c.X, c.Y), ScreenM(end.X, end.Y));
            var ll = Math.Sqrt((end.X - c.X) * (end.X - c.X) + (end.Y - c.Y) * (end.Y - c.Y)); var ang = Math.Atan2(end.Y - c.Y, end.X - c.X) * 180 / Math.PI;
            Label(ctx, $"{ll:0.00} m · {ang:0}°{(_typed.Length > 0 ? "  ⌨ " + _typed : "")}", ScreenM(end.X, end.Y) + new Point(12, 10), "#0A1628", "#F5A524");
        }
        if (_moved) { var sp = ScreenM(_snap.X, _snap.Y); var pen = new Pen(B("#F5A524"), 1.2); if (_snapped) ctx.DrawRectangle(null, pen, new Rect(sp.X - 6, sp.Y - 6, 12, 12)); else ctx.DrawEllipse(null, pen, sp, 3, 3); }
        Label(ctx, $"x {_snap.X:0.00} m · y {_snap.Y:0.00} m", new Point(10, size.Height - 24), "#C9D6EA", "#AA0A1628");
        var hint = _error is { } err ? "⚠ " + err : _s.Hint.Length > 0 ? _s.Hint : ToolHint();
        if (hint.Length > 0) Label(ctx, hint, new Point(10, 10), _error is null ? "#F2F6FC" : "#FFFFFF", _error is null ? "#CC14233D" : "#DDB91C1C");
        if (model.Members.Count == 0 && _s.Tool == "auswahl")
        {
            var ft = new FormattedText("Leeres Tragwerk – links „Stab“ wählen und Punkt für Punkt zeichnen, oder eine Vorlage öffnen.", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Face, 15, B("#9FB4D3"));
            ctx.DrawText(ft, new Point((size.Width - ft.Width) / 2, size.Height / 2 - 10));
        }
    }

    string ToolHint() => _s.Tool switch
    {
        "stab" => _s.Chain is null ? "Stab: Anfangspunkt klicken (fängt an Knoten und am 10-cm-Raster)." : "Nächsten Punkt klicken · Länge in m tippen + Enter · Doppelklick, Esc oder rechte Maustaste beendet · Umschalt: freier Winkel",
        "lager" => $"Lager ({FrameDrawing.SupportName(_s.SupportType)}): auf einen Knoten klicken – nochmal klicken entfernt es. Art rechts wählen.",
        "last" => _s.LoadKind == "strecke" ? $"Streckenlast {_s.LoadValue:0.##} kN/m: auf einen Stab klicken (Knoten: Einzellast)." : $"Einzellast {_s.LoadValue:0.##} kN: auf Stab oder Knoten klicken.",
        "gelenk" => "Gelenk: nahe am Stabende auf den Stab klicken (überträgt dort kein Moment).",
        _ => "",
    };

    void Draw(DrawingContext ctx, Shape sh)
    {
        switch (sh)
        {
            case DPoly q:
                var pts = q.Pts.Select(v => Screen(v.X, v.Y)).ToList();
                ctx.DrawGeometry(q.Fill is null ? null : B(q.Fill), q.Stroke is null ? null : new Pen(B(q.Stroke), q.Layer == "STAEBE" && !q.Closed ? 5 : Math.Max(1, q.Width)) { LineCap = PenLineCap.Round }, new PolylineGeometry(pts, q.Closed));
                break;
            case DLine l: ctx.DrawLine(new Pen(B(l.Stroke), Math.Max(1, l.Width), l.Dashed ? new DashStyle([4, 3], 0) : null), Screen(l.X1, l.Y1), Screen(l.X2, l.Y2)); break;
            case DArc a:
                var arc = Enumerable.Range(0, 25).Select(k => { var ang = (a.StartDeg + a.SweepDeg * k / 24) * Math.PI / 180; return Screen(a.Cx + a.R * Math.Cos(ang), a.Cy + a.R * Math.Sin(ang)); }).ToList();
                ctx.DrawGeometry(null, new Pen(B(a.Stroke), 1.6), new PolylineGeometry(arc, false)); break;
            case DText t:
                var px = Math.Clamp(t.Size * _s.Zoom, 10, 16);
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
