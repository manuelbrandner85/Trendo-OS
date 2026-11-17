using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>T-0745: Auswahl im Schnitt-Editor – bleibt beim Neuzeichnen der Seite erhalten (je Dokument).</summary>
public sealed class GarmentEditState
{
    public int Part { get; set; } = -1;
    public int Node { get; set; } = -1;
    /// <summary>Gewähltes Kantenstück (vom Punkt <see cref="Seg"/> zum nächsten).</summary>
    public int Seg { get; set; } = -1;
    public bool WantFocus { get; set; }
    public void Clear() { Part = Node = Seg = -1; }
}

/// <summary>T-0745 (SPEC-08): Schnittteile auf der Arbeitsfläche frei bearbeiten. Teil anklicken wählt es; Punkte ziehen; Doppelklick auf eine Kante setzt
/// einen Punkt; Griffe der Kurven ziehen; Rechtsklick-Menü und Tastenkürzel (Entf, Einfg, K, Pfeile, Tab, Esc). Jede fertige Änderung geht als EIN Schritt
/// ins Programm-Dokument (Rückgängig mit Strg+Z über die Hülle). Rechnet mit <see cref="GarmentEdit"/> – kein eigener Geometrie-Kern.</summary>
public sealed class GarmentPatternEditor : Canvas
{
    public const double Scale = 3.4, Gap = 14, Top = 6;
    /// <summary>Fangradius in Bildschirmpunkten.</summary>
    const double TolPx = 7;

    readonly Garment _g;
    readonly GarmentEditState _st;
    readonly Func<int, PatPart, List<PatNode>> _nodesOf;
    /// <summary>Teil, neue Punkte (null = auf die Vorlage zurücksetzen), Bezeichnung des Schritts.</summary>
    readonly Action<int, List<PatNode>?, string> _commit;
    readonly Action<string> _hint;
    readonly IBrush _text, _muted;
    static readonly IBrush Gold = Brush.Parse("#C9A227"), GoldFill = Brush.Parse("#22C9A227"), GoldSel = Brush.Parse("#33C9A227"), Dark = Brush.Parse("#0B111C"), Handle = Brush.Parse("#E8D9A8");
    readonly List<(double Ox, double MaxY)> _frames = [];
    readonly Dictionary<int, List<PatNode>> _nodes = [];

    enum DragKind { None, Node, HandleIn, HandleOut }
    DragKind _drag; bool _moved; Point _start;
    List<PatNode>? _live;
    (int Seg, double T)? _menuAt;

    public GarmentEditState State => _st;
    /// <summary>Auswahl oder Vorschau hat sich geändert (Leiste und Maße auffrischen).</summary>
    public event Action? Changed;
    public Garment Garment => _g;

    public GarmentPatternEditor(Garment g, GarmentEditState st, Func<int, PatPart, List<PatNode>> nodesOf, Action<int, List<PatNode>?, string> commit, Action<string> hint, IBrush text, IBrush muted)
    {
        _g = g; _st = st; _nodesOf = nodesOf; _commit = commit; _hint = hint; _text = text; _muted = muted;
        Name = "garment-pattern"; HorizontalAlignment = HorizontalAlignment.Left; Focusable = true; Background = Brushes.Transparent;
        Cursor = new Cursor(StandardCursorType.Cross);
        if (_st.Part >= g.Parts.Count) _st.Clear();
        // Lage der Teile nebeneinander (bleibt während des Ziehens fest)
        var x = 6.0; var maxH = 0.0;
        foreach (var p in g.Parts)
        {
            var cut = GarmentProduction.WithAllowance(p);
            double minX = cut.Min(q => q.X), maxX = cut.Max(q => q.X), maxY = cut.Max(q => q.Y), minY = cut.Min(q => q.Y);
            _frames.Add((x - minX * Scale, maxY + 2));
            x += (maxX - minX) * Scale + Gap; maxH = Math.Max(maxH, (maxY + 2 - minY) * Scale + 20);
        }
        Width = x; Height = maxH;
        if (_st.Part >= 0 && _st.Node >= Nodes(_st.Part).Count) _st.Node = -1;
        if (_st.Part >= 0 && _st.Seg >= Nodes(_st.Part).Count) _st.Seg = -1;
        Redraw();
        ContextMenu = Menu(); // Rechtsklick-Menü gibt es immer (auch in „Einfach“, wo die Leiste fehlt)
        PointerPressed += OnPress; PointerMoved += OnMove; PointerReleased += OnRelease; KeyDown += OnKey;
        if (_st.WantFocus) AttachedToVisualTree += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(() => Focus());
    }

    /// <summary>Punkte des Teils (bearbeitet oder aus der Vorlage zerlegt).</summary>
    public List<PatNode> Nodes(int part) => _nodes.TryGetValue(part, out var n) ? n : _nodes[part] = _nodesOf(part, _g.Parts[part]);

    /// <summary>Lage eines Schnittpunkts (cm) auf der Fläche – für Tests und Hilfen.</summary>
    public Point MapPoint(int part, double x, double y) => Map(part, x, y);
    Point Map(int part, double x, double y) { var (ox, maxY) = _frames[part]; return new(ox + x * Scale, Top + (maxY - y) * Scale); }
    (double X, double Y) Unmap(int part, Point p) { var (ox, maxY) = _frames[part]; return ((p.X - ox) / Scale, maxY - (p.Y - Top) / Scale); }

    // ───────────── Zeichnen ─────────────
    public void Redraw()
    {
        Children.Clear();
        for (var i = 0; i < _g.Parts.Count; i++) DrawPart(i);
        if (_drag == DragKind.None) ContextMenu = Menu(); // Menü passt immer zur Auswahl
        Changed?.Invoke();
    }

    PatPart Shown(int i)
    {
        if (i != _st.Part || _live is null) return _g.Parts[i];
        try { return GarmentEdit.FromNodes(_g.Parts[i], _live); } catch (ArgumentException) { return _g.Parts[i]; }
    }

    void DrawPart(int i)
    {
        var p = Shown(i); var sel = i == _st.Part;
        Point M(PatPt q) => Map(i, q.X, q.Y);
        var cut = GarmentProduction.WithAllowance(p);
        Children.Add(new Polygon { Name = $"garment-part-{i}", Points = new Points(cut.Select(M)), Fill = sel ? GoldSel : GoldFill, Stroke = Gold, StrokeThickness = sel ? 2 : 1.4 });
        Children.Add(new Polygon { Name = $"garment-seam-{i}", Points = new Points(p.Outline.Select(M)), Stroke = sel ? _text : _muted, StrokeThickness = 0.9, StrokeDashArray = [4, 3] });
        double yLow = p.Outline.Min(q => q.Y) + 6, yHigh = p.Outline.Max(q => q.Y) - 6;
        if (yHigh > yLow)
        {
            Children.Add(new Line { StartPoint = M(new PatPt(0, yLow)), EndPoint = M(new PatPt(0, yHigh)), Stroke = _text, StrokeThickness = 1 });
            var head = M(new PatPt(0, yHigh));
            Children.Add(new Polygon { Points = new Points([head, head + new Point(-4, 8), head + new Point(4, 8)]), Fill = _text });
        }
        var mid = M(new PatPt(0, (p.Outline.Min(q => q.Y) + p.Outline.Max(q => q.Y)) / 2));
        Children.Add(new TextBlock { Text = $"{p.Name} ({p.Qty}×)", Foreground = _text, FontSize = 11.5, [Canvas.LeftProperty] = mid.X - 34, [Canvas.TopProperty] = mid.Y });
        if (!sel) return;

        var n = _live ?? Nodes(i);
        // Länge jeder Kante an ihrer Mitte
        foreach (var e in p.Edges)
        {
            var m = e.Points[e.Points.Count / 2]; var at = M(m);
            Children.Add(new TextBlock { Text = GarmentEdit.Cm(e.Length), Foreground = Gold, FontSize = 10, IsHitTestVisible = false, [Canvas.LeftProperty] = at.X + 3, [Canvas.TopProperty] = at.Y - 14 });
        }
        if (_st.Seg >= 0 && _st.Seg < n.Count)
            Children.Add(new Polyline { Name = "garment-edit-seg", Points = new Points(GarmentEdit.SegmentPoints(n, _st.Seg).Select(M)), Stroke = Gold, StrokeThickness = 3.2, IsHitTestVisible = false });
        for (var k = 0; k < n.Count; k++)
        {
            var c = M(new PatPt(n[k].X, n[k].Y)); var on = k == _st.Node; var s = on ? 10.0 : 7.0;
            Shape mark = n[k].Edge is not null ? new Rectangle { Width = s, Height = s } : new Ellipse { Width = s, Height = s };
            mark.Name = $"garment-node-{k}"; mark.Fill = on ? Gold : Dark; mark.Stroke = Gold; mark.StrokeThickness = 1.4; mark.IsHitTestVisible = false;
            SetLeft(mark, c.X - s / 2); SetTop(mark, c.Y - s / 2);
            Children.Add(mark);
        }
        if (_st.Node >= 0 && _st.Node < n.Count)
        {
            var q = n[_st.Node].N; var c = M(new PatPt(q.X, q.Y));
            void H(double hx, double hy, string name)
            {
                if (Math.Abs(hx - q.X) + Math.Abs(hy - q.Y) < 1e-9) return;
                var h = M(new PatPt(hx, hy));
                Children.Add(new Line { StartPoint = c, EndPoint = h, Stroke = Handle, StrokeThickness = 1, IsHitTestVisible = false });
                var dot = new Ellipse { Name = name, Width = 8, Height = 8, Fill = Handle, IsHitTestVisible = false };
                SetLeft(dot, h.X - 4); SetTop(dot, h.Y - 4); Children.Add(dot);
            }
            H(q.InX, q.InY, "garment-handle-in"); H(q.OutX, q.OutY, "garment-handle-out");
        }
    }

    // ───────────── Zeiger ─────────────
    /// <summary>Teil unter einem Punkt der Fläche (Schnittkante inklusive Nahtzugabe).</summary>
    int PartAt(Point pt)
    {
        for (var i = _g.Parts.Count - 1; i >= 0; i--)
        {
            var (x, y) = Unmap(i, pt);
            var cut = GarmentProduction.WithAllowance(_g.Parts[i]); var hit = false;
            for (int a = 0, b = cut.Count - 1; a < cut.Count; b = a++)
                if (cut[a].Y > y != cut[b].Y > y && x < (cut[b].X - cut[a].X) * (y - cut[a].Y) / (cut[b].Y - cut[a].Y) + cut[a].X) hit = !hit;
            if (hit) return i;
        }
        return -1;
    }

    void OnPress(object? sender, PointerPressedEventArgs e)
    {
        Focus(); _st.WantFocus = true;
        var pt = e.GetCurrentPoint(this); var pos = pt.Position; _start = pos; _moved = false; _menuAt = null;
        var tol = TolPx / Scale;
        if (_st.Part >= 0)
        {
            var i = _st.Part; var n = Nodes(i); var (x, y) = Unmap(i, pos);
            // 1. Griffe des gewählten Punkts
            if (_st.Node >= 0 && pt.Properties.IsLeftButtonPressed)
            {
                var q = n[_st.Node].N;
                bool Near(double hx, double hy) => Math.Abs(hx - q.X) + Math.Abs(hy - q.Y) > 1e-9 && Math.Sqrt((hx - x) * (hx - x) + (hy - y) * (hy - y)) <= tol;
                if (Near(q.OutX, q.OutY)) { Begin(DragKind.HandleOut, e); return; }
                if (Near(q.InX, q.InY)) { Begin(DragKind.HandleIn, e); return; }
            }
            // 2. Punkte
            var hit = GarmentEdit.HitNode(n, x, y, tol);
            if (hit >= 0)
            {
                _st.Node = hit; _st.Seg = -1; Redraw();
                if (pt.Properties.IsLeftButtonPressed) Begin(DragKind.Node, e); else ContextMenu = Menu();
                return;
            }
            // 3. Kanten
            var (seg, t, d) = GarmentEdit.Nearest(n, x, y);
            if (seg >= 0 && d <= tol)
            {
                _menuAt = (seg, t);
                if (pt.Properties.IsLeftButtonPressed && e.ClickCount >= 2) { Insert(seg, t); e.Handled = true; return; }
                _st.Seg = seg; _st.Node = -1; Redraw();
                if (!pt.Properties.IsLeftButtonPressed) ContextMenu = Menu();
                e.Handled = true; return;
            }
        }
        var part = PartAt(pos);
        if (part != _st.Part) { _st.Part = part; _st.Node = _st.Seg = -1; Redraw(); }
        else if (part >= 0) { _st.Node = _st.Seg = -1; Redraw(); }
        ContextMenu = Menu();
        _hint(part >= 0 ? HintFor(part) : "Klicke ein Schnittteil an, um es zu bearbeiten.");
    }

    void Begin(DragKind k, PointerPressedEventArgs e) { _drag = k; _live = null; e.Pointer.Capture(this); e.Handled = true; }

    void OnMove(object? sender, PointerEventArgs e)
    {
        if (_drag == DragKind.None || _st.Part < 0) return;
        var pos = e.GetPosition(this);
        if (!_moved && Math.Abs(pos.X - _start.X) + Math.Abs(pos.Y - _start.Y) < 2) return;
        _moved = true;
        var (x, y) = Unmap(_st.Part, pos);
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) { x = Math.Round(x * 2) / 2; y = Math.Round(y * 2) / 2; } // Umschalt: auf 5 mm einrasten
        var n = Nodes(_st.Part);
        _live = _drag switch
        {
            DragKind.Node => GarmentEdit.MoveNode(n, _st.Node, x, y),
            DragKind.HandleOut => GarmentEdit.MoveHandle(n, _st.Node, true, x, y),
            _ => GarmentEdit.MoveHandle(n, _st.Node, false, x, y),
        };
        Redraw();
        var p = Shown(_st.Part);
        _hint($"Punkt bei {GarmentEdit.Cm(x)} / {GarmentEdit.Cm(y)} cm · Fläche {p.Area:0} cm²");
    }

    void OnRelease(object? sender, PointerReleasedEventArgs e)
    {
        if (_drag == DragKind.None) return;
        var kind = _drag; _drag = DragKind.None; e.Pointer.Capture(null);
        if (!_moved || _live is null) { _live = null; return; }
        var n = _live; _live = null;
        Commit(n, kind == DragKind.Node ? "Punkt verschoben" : "Kurve geformt");
    }

    void Commit(List<PatNode> n, string label)
    {
        _nodes[_st.Part] = n; _st.WantFocus = true;
        _commit(_st.Part, n, label);
    }

    // ───────────── Befehle (Menü, Kürzel, Leiste) ─────────────
    /// <summary>Befehle: insert, delete, curve, line, toggle, reset, next, prev, clear. Rückgabe: true, wenn etwas passiert ist.</summary>
    public bool Act(string cmd)
    {
        var i = _st.Part;
        if (cmd == "clear") { _st.Clear(); Redraw(); _hint("Klicke ein Schnittteil an, um es zu bearbeiten."); return true; }
        if (i < 0) { _hint("Erst ein Schnittteil anklicken."); return false; }
        var n = Nodes(i);
        switch (cmd)
        {
            case "insert":
            {
                var seg = _menuAt?.Seg ?? (_st.Seg >= 0 ? _st.Seg : _st.Node >= 0 ? _st.Node : -1);
                if (seg < 0) { _hint("Erst eine Kante oder einen Punkt wählen – der neue Punkt kommt auf die Kante danach."); return false; }
                Insert(seg, _menuAt?.Seg == seg ? _menuAt.Value.T : 0.5); return true;
            }
            case "delete":
            {
                if (_st.Node < 0) { _hint("Erst einen Punkt anklicken."); return false; }
                var (r, problem) = GarmentEdit.Delete(n, _st.Node);
                if (r is null) { _hint(problem!); return false; }
                _st.Node = -1; _st.Seg = -1;
                Commit(r, "Punkt gelöscht"); return true;
            }
            case "curve" or "line" or "toggle":
            {
                var seg = _st.Seg >= 0 ? _st.Seg : _st.Node;
                if (seg < 0) { _hint("Erst eine Kante anklicken."); return false; }
                var curve = cmd == "curve" || (cmd == "toggle" && !GarmentEdit.IsCurve(n, seg));
                if (curve == GarmentEdit.IsCurve(n, seg)) { _hint(curve ? "Diese Kante ist schon eine Kurve." : "Diese Kante ist schon gerade."); return false; }
                _st.Seg = seg; if (curve) _st.Node = seg;
                Commit(GarmentEdit.SetCurve(n, seg, curve), curve ? "Kante als Kurve" : "Kante als Gerade"); return true;
            }
            case "reset":
                _st.Node = _st.Seg = -1; _nodes.Remove(i); _st.WantFocus = true;
                _commit(i, null, "Teil zurückgesetzt"); return true;
            case "next" or "prev":
                _st.Node = _st.Node < 0 ? 0 : (_st.Node + (cmd == "next" ? 1 : n.Count - 1)) % n.Count; _st.Seg = -1; Redraw(); _hint(HintFor(i)); return true;
        }
        return false;
    }

    /// <summary>Gewählten Punkt um dx/dy (cm) verschieben.</summary>
    public bool Nudge(double dx, double dy)
    {
        if (_st.Part < 0 || _st.Node < 0) return false;
        var n = Nodes(_st.Part); var p = n[_st.Node];
        Commit(GarmentEdit.MoveNode(n, _st.Node, p.X + dx, p.Y + dy), "Punkt verschoben"); return true;
    }

    /// <summary>Gewählten Punkt genau an eine Lage (cm) setzen.</summary>
    public bool MoveTo(double x, double y)
    {
        if (_st.Part < 0 || _st.Node < 0) return false;
        Commit(GarmentEdit.MoveNode(Nodes(_st.Part), _st.Node, x, y), "Punkt gesetzt"); return true;
    }

    void Insert(int seg, double t)
    {
        var (r, at) = GarmentEdit.Insert(Nodes(_st.Part), seg, t);
        _st.Node = at; _st.Seg = -1;
        Commit(r, "Punkt eingefügt");
    }

    string HintFor(int part)
    {
        var n = Nodes(part);
        if (_st.Node >= 0 && _st.Node < n.Count)
        {
            var q = n[_st.Node];
            return $"Punkt {_st.Node + 1} von {n.Count} bei {GarmentEdit.Cm(q.X)} / {GarmentEdit.Cm(q.Y)} cm" + (q.Edge is { } e ? $" · Beginn der Kante „{GarmentEdit.EdgeLabel(e)}“" : "") + " · ziehen, Pfeiltasten, Entf";
        }
        if (_st.Seg >= 0 && _st.Seg < n.Count) return $"Kante „{GarmentEdit.EdgeLabel(GarmentEdit.EdgeOf(n, _st.Seg))}“ · {(GarmentEdit.IsCurve(n, _st.Seg) ? "Kurve" : "Gerade")} · K wechselt, Doppelklick setzt einen Punkt";
        return $"{_g.Parts[part].Name}: Punkt anklicken und ziehen · Doppelklick auf eine Kante setzt einen Punkt · Rechtsklick für alle Befehle";
    }

    ContextMenu Menu()
    {
        var m = new ContextMenu { Name = "garment-edit-menu" };
        var n = _st.Part >= 0 ? Nodes(_st.Part) : [];
        var seg = _st.Seg >= 0 ? _st.Seg : _st.Node;
        MenuItem I(string header, string cmd, string gesture, bool on)
        {
            var it = new MenuItem { Name = "garment-edit-menu-" + cmd, Header = header, InputGesture = gesture.Length > 0 ? KeyGesture.Parse(gesture) : null, IsEnabled = on };
            it.Click += (_, _) => Act(cmd);
            return it;
        }
        m.Items.Add(I("Punkt hier einfügen", "insert", "Insert", _menuAt is not null || seg >= 0));
        m.Items.Add(I("Punkt löschen", "delete", "Delete", _st.Node >= 0));
        m.Items.Add(new Separator());
        m.Items.Add(I("Kante als Kurve", "curve", "K", seg >= 0 && seg < n.Count && !GarmentEdit.IsCurve(n, seg)));
        m.Items.Add(I("Kante als Gerade", "line", "K", seg >= 0 && seg < n.Count && GarmentEdit.IsCurve(n, seg)));
        m.Items.Add(new Separator());
        m.Items.Add(I("Nächster Punkt", "next", "Tab", n.Count > 0));
        m.Items.Add(I("Teil auf die Vorlage zurücksetzen", "reset", "", _st.Part >= 0));
        m.Items.Add(I("Auswahl aufheben", "clear", "Escape", _st.Part >= 0));
        return m;
    }

    void OnKey(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Alt)) return; // Strg+Z/Y/S gehen an die Hülle
        var step = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 1.0 : 0.1;
        var done = e.Key switch
        {
            Key.Delete or Key.Back => Act("delete"),
            Key.Insert => Act("insert"),
            Key.K => Act("toggle"),
            Key.Left => Nudge(-step, 0),
            Key.Right => Nudge(step, 0),
            Key.Up => Nudge(0, step),
            Key.Down => Nudge(0, -step),
            Key.Tab => Act(e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? "prev" : "next"),
            Key.Escape => Act("clear"),
            _ => (bool?)null,
        };
        if (done is not null) e.Handled = true;
    }
}
