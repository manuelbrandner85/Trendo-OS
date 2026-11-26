using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Trendo.Core.Kreativ;
using Path = Avalonia.Controls.Shapes.Path;

namespace Trendo.App;

/// <summary>T-0696: zeichnet Formen des Vektor-Kerns (<see cref="VectorKit"/>) als Avalonia-Steuerelemente – dieselbe Darstellung für Editor, Vorschau, PNG und Videobilder.</summary>
public static class VectorRender
{
    static readonly Dictionary<string, Bitmap> Bitmaps = [];

    static IBrush? Paint(VShape s, string fill)
    {
        if (fill.Length == 0 || !VectorKit.IsColor(fill)) return null;
        if (s.Fill2.Length > 0 && VectorKit.IsColor(s.Fill2) && fill == s.Fill)
        {
            var (x1, y1, x2, y2) = VectorKit.GradientLine(s.GradAngle);
            return new LinearGradientBrush
            {
                StartPoint = new RelativePoint(x1, y1, RelativeUnit.Relative), EndPoint = new RelativePoint(x2, y2, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Color.Parse(s.Fill), 0), new GradientStop(Color.Parse(s.Fill2), 1) },
            };
        }
        return new SolidColorBrush(Color.Parse(fill));
    }

    public static Typeface Face(VShape s) => new(new FontFamily(s.Font.Length > 0 ? s.Font : "Segoe UI"), FontStyle.Normal, s.Bold ? FontWeight.Bold : FontWeight.Normal);

    static FormattedText Ft(VShape s, string line) => new(line.Length == 0 ? " " : line, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Face(s), s.FontSize, Brushes.Black);

    /// <summary>Breite des Textes (längste Zeile) in Dokument-Einheiten.</summary>
    public static double TextWidth(VShape s) => Math.Max(4, VectorKit.Lines(s).Max(l => Ft(s, l).WidthIncludingTrailingWhitespace));

    /// <summary>Textkasten an die echte Breite anpassen (Ausrichtung bleibt stehen: links, Mitte oder rechts).</summary>
    public static void FitText(VShape s)
    {
        if (s.Kind != "text") return;
        var w = TextWidth(s); var h = s.FontSize * VectorKit.TextLine * VectorKit.Lines(s).Length;
        var a = VectorKit.ToDoc(s, s.Anchor == "middle" ? s.W / 2 : s.Anchor == "end" ? s.W : 0, 0);
        var keepX = s.Anchor == "middle" ? w / 2 : s.Anchor == "end" ? w : 0;
        s.W = w; s.H = h;
        // Ankerpunkt (oben) bleibt, wo er war – auch bei gedrehtem Text
        var now = VectorKit.ToDoc(s, keepX, 0);
        s.X += a.X - now.X; s.Y += a.Y - now.Y;
    }

    static Bitmap? Bmp(string uri)
    {
        if (uri.Length == 0) return null;
        var key = uri.Length + ":" + uri.GetHashCode();
        if (Bitmaps.TryGetValue(key, out var b)) return b;
        if (VectorKit.DataUri(uri) is not { } bytes) return null;
        try { using var ms = new MemoryStream(bytes); b = new Bitmap(ms); }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or NotSupportedException or IOException) { return null; }
        if (Bitmaps.Count > 64) Bitmaps.Clear();
        return Bitmaps[key] = b;
    }

    /// <summary>Eine Form als Steuerelement (Kasten W×H an X/Y, Drehung und Größe um die Mitte).</summary>
    public static Control Build(VShape s)
    {
        var box = new Canvas { Width = s.W, Height = s.H, Opacity = Math.Clamp(s.Opacity, 0, 1), RenderTransformOrigin = RelativePoint.Center, Tag = s.Id };
        Canvas.SetLeft(box, s.X); Canvas.SetTop(box, s.Y);
        var tf = new TransformGroup();
        if (Math.Abs(s.Scale - 1) > 1e-9) tf.Children.Add(new ScaleTransform(s.Scale, s.Scale));
        if (Math.Abs(s.Rot) > 1e-9) tf.Children.Add(new RotateTransform(s.Rot));
        if (tf.Children.Count > 0) box.RenderTransform = tf;
        switch (s.Kind)
        {
            case "text":
            {
                var lines = VectorKit.Lines(s); var brush = Paint(s, s.Fill) ?? Brushes.Transparent;
                for (var i = 0; i < lines.Length; i++)
                {
                    var ft = Ft(s, lines[i]); var w = ft.WidthIncludingTrailingWhitespace;
                    var t = new TextBlock { Text = lines[i], FontFamily = Face(s).FontFamily, FontSize = s.FontSize, FontWeight = s.Bold ? FontWeight.Bold : FontWeight.Normal, Foreground = brush };
                    Canvas.SetLeft(t, s.Anchor == "middle" ? s.W / 2 - w / 2 : s.Anchor == "end" ? s.W - w : 0);
                    Canvas.SetTop(t, s.FontSize * (VectorKit.TextBase + i * VectorKit.TextLine) - ft.Baseline);
                    box.Children.Add(t);
                }
                break;
            }
            case "image":
                if (Bmp(s.Image) is { } bmp) box.Children.Add(new Image { Source = bmp, Width = s.W, Height = s.H, Stretch = Stretch.Fill });
                else box.Children.Add(new Rectangle { Width = s.W, Height = s.H, Stroke = Brushes.Gray, StrokeThickness = 1, StrokeDashArray = [4, 3] });
                break;
            default:
            {
                var (n, closed) = VectorKit.ToNodes(s);
                var data = VectorKit.PathData(n, closed);
                if (data.Length == 0) break;
                var p = new Path { Data = StreamGeometry.Parse(data), Fill = closed ? Paint(s, s.Fill) : null, StrokeJoin = PenLineJoin.Round, StrokeLineCap = PenLineCap.Round };
                if (s.Stroke.Length > 0 && s.StrokeW > 0 && VectorKit.IsColor(s.Stroke)) { p.Stroke = new SolidColorBrush(Color.Parse(s.Stroke)); p.StrokeThickness = s.StrokeW; }
                box.Children.Add(p);
                break;
            }
        }
        return box;
    }

    /// <summary>Ganze Zeichenfläche (für Vorschau, PNG, Videobild). <paramref name="background"/> überschreibt den Hintergrund des Dokuments.</summary>
    public static Canvas Stage(VDoc d, string? background = null)
    {
        var bg = background ?? d.Background;
        var c = new Canvas { Width = d.W, Height = d.H, ClipToBounds = true, Background = VectorKit.IsColor(bg) ? new SolidColorBrush(Color.Parse(bg)) : Brushes.Transparent };
        foreach (var s in d.Shapes) c.Children.Add(Build(s));
        return c;
    }

    /// <summary>PNG in <paramref name="pixelWidth"/> Pixel Breite (Vektor wird scharf in dieser Größe gezeichnet, nicht hochgerechnet).</summary>
    public static byte[] Png(VDoc d, int pixelWidth, string? background = null, int? pixelHeight = null) => VectorSkia.Png(d, pixelWidth, background, pixelHeight);
}

/// <summary>T-0696: Zustand des Editors je Dokument – Werkzeug, Auswahl, Raster. Bleibt beim Neuzeichnen der Seite erhalten.</summary>
public sealed class VectorEditorState
{
    public string Tool { get; set; } = "select";
    public HashSet<string> Selected { get; } = [];
    public bool ShowGrid { get; set; }
    public bool SnapOn { get; set; } = true;
    public double Grid { get; set; } = 10;
    public int Node { get; set; } = -1;
    public bool WantFocus { get; set; }
    /// <summary>Nach dem Neuzeichnen das Textfeld der Leiste fokussieren (neuer Text).</summary>
    public bool FocusText { get; set; }
    /// <summary>Animation: Ziehen auf der Bühne setzt Schlüsselbilder zur aktuellen Zeit.</summary>
    public bool AutoKey { get; set; } = true;
    public static List<VShape> Clipboard { get; } = [];

    public static readonly IReadOnlyList<(string Id, string Icon, string Label, string Tip, Key Key)> Tools =
    [
        ("select", "↖", "Auswahl", "Anklicken wählt, Ziehen verschiebt; Griffe am Rahmen ändern Größe und Drehung (V)", Avalonia.Input.Key.V),
        ("nodes", "◇", "Punkte", "Punkte und Bézier-Griffe eines Pfads ziehen, Doppelklick macht Ecke ↔ Rundung (A)", Avalonia.Input.Key.A),
        ("rect", "▭", "Rechteck", "Ziehen zeichnet ein Rechteck, mit Umschalt ein Quadrat (R)", Avalonia.Input.Key.R),
        ("ellipse", "◯", "Ellipse", "Ziehen zeichnet eine Ellipse, mit Umschalt einen Kreis (E)", Avalonia.Input.Key.E),
        ("polygon", "⬡", "Vieleck", "Ziehen zeichnet ein Vieleck (Ecken oben einstellen)", Avalonia.Input.Key.Y),
        ("star", "★", "Stern", "Ziehen zeichnet einen Stern (S)", Avalonia.Input.Key.S),
        ("line", "╱", "Linie", "Ziehen zeichnet eine gerade Linie (L)", Avalonia.Input.Key.L),
        ("pen", "✒", "Zeichenstift", "Klicken setzt Ecken, Ziehen setzt Rundungen; Klick auf den ersten Punkt schließt, Enter beendet (P)", Avalonia.Input.Key.P),
        ("text", "T", "Text", "Klick setzt einen Text, oben eintippen (T)", Avalonia.Input.Key.T),
    ];
}

/// <summary>T-0696: gemeinsamer kleiner Vektor-Editor für Logos &amp; Marken und die Animations-Bühne. Formen und Bézier-Pfade direkt auf der Fläche zeichnen,
/// wählen, verschieben, drehen, skalieren, Punkte und Griffe ziehen; Raster und Einrasten; Tastenkürzel, Rechtsklick-Menü, SVG/Bild hineinziehen.
/// Jede fertige Änderung geht als ein Schritt über <see cref="Commit"/> ins Programm-Dokument (Rückgängig über die Hülle).</summary>
public sealed class VectorEditor : DockPanel
{
    public VDoc Doc { get; private set; }
    public VectorEditorState State { get; }
    readonly string _p;
    /// <summary>Neuer Stand und Bezeichnung des Schritts.</summary>
    public Action<VDoc, string> Commit { get; set; }
    /// <summary>Animation: Lage, Größe und Drehung geändert – vorher/nachher je Form (statt <see cref="Commit"/>).</summary>
    public Action<IReadOnlyList<(VShape Before, VShape After)>, string>? TransformCommit { get; set; }
    /// <summary>Bild/SVG einsetzen (Dateiauswahl der Hülle).</summary>
    public Action? PickImport { get; set; }
    /// <summary>Abgelegte Dateien einsetzen (Pfad, Mitte in Dokument-Koordinaten).</summary>
    public Action<IReadOnlyList<string>, double, double>? DropFiles { get; set; }
    /// <summary>Animation: Größe über die Eigenschaft „Skalierung“ statt über Breite/Höhe.</summary>
    public bool ScaleMode { get; init; }
    public Panel Surface { get; }
    public Viewbox View { get; }
    public Control ContextBar { get; private set; } = new Panel();
    readonly Canvas _shapes; readonly VectorOverlay _overlay; readonly Canvas _grid;

    // laufendes Ziehen
    enum DragKind { None, Move, Resize, Rotate, Band, Node, Create, PenHandle }
    DragKind _drag; Point _start, _last; int _handle; string _part = ""; bool _moved;
    Dictionary<string, VShape> _before = [];
    (double Cx, double Cy, double W, double H, double Rot) _frame0;
    VShape? _creating;
    readonly List<VNode> _pen = [];

    public VectorEditor(VDoc doc, VectorEditorState state, Action<VDoc, string> commit, string namePrefix = "vec")
    {
        Doc = doc.Clone(); State = state; Commit = commit; _p = namePrefix;
        foreach (var t in Doc.Shapes.Where(s => s.Kind == "text")) VectorRender.FitText(t);
        State.Selected.RemoveWhere(id => Doc.Find(id) is null);
        _grid = new Canvas { Width = Doc.W, Height = Doc.H, IsHitTestVisible = false };
        _shapes = new Canvas { Width = Doc.W, Height = Doc.H, IsHitTestVisible = false, ClipToBounds = false };
        _overlay = new VectorOverlay(this) { Width = Doc.W, Height = Doc.H, IsHitTestVisible = false };
        var bg = VectorKit.IsColor(Doc.Background) ? (IBrush)new SolidColorBrush(Color.Parse(Doc.Background)) : new SolidColorBrush(Color.Parse("#F4F4F5")); // durchsichtig: auf hellem Grund gestalten (die Vorschau unten zeigt auch dunkel)
        Surface = new Panel { Name = _p + "-surface", Width = Doc.W, Height = Doc.H, Background = bg, Focusable = true, Cursor = new Cursor(StandardCursorType.Cross), Children = { _grid, _shapes, _overlay } };
        View = new Viewbox { Name = _p + "-view", Stretch = Stretch.Uniform, Child = Surface, Margin = new Thickness(8) };
        _overlay.View = View;
        RedrawShapes(); RedrawGrid();
        Surface.PointerPressed += OnPress; Surface.PointerMoved += OnMove; Surface.PointerReleased += OnRelease;
        Surface.KeyDown += OnKey;
        DragDrop.SetAllowDrop(Surface, true);
        Surface.AddHandler(DragDrop.DragOverEvent, (_, e) => { e.DragEffects = e.DataTransfer.Contains(DataFormat.File) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; });
        Surface.AddHandler(DragDrop.DropEvent, (_, e) =>
        {
            var files = e.DataTransfer.TryGetFiles()?.Select(f => f.TryGetLocalPath()).OfType<string>().Where(File.Exists).ToList() ?? [];
            if (files.Count == 0 || DropFiles is null) return;
            var p = e.GetPosition(Surface); DropFiles(files, p.X, p.Y); e.Handled = true;
        });
        ContextBar = BuildBar();
        DockPanel.SetDock(ContextBar, Avalonia.Controls.Dock.Top);
        Children.Add(ContextBar);
        Children.Add(new Border { Name = _p + "-stage", MinHeight = 120, Background = Brush.Parse("#0B111C"), CornerRadius = new CornerRadius(8), ClipToBounds = true, Child = View });
        if (State.FocusText) { State.FocusText = false; State.WantFocus = false; AttachedToVisualTree += (_, _) => Trendo.App.UiThread.Post(FocusText); }
        else if (State.WantFocus) AttachedToVisualTree += (_, _) => Trendo.App.UiThread.Post(() => Surface.Focus());
    }

    static IBrush Checker() => new ConicGradientBrush { GradientStops = { new GradientStop(Color.Parse("#2A2F3A"), 0), new GradientStop(Color.Parse("#2A2F3A"), 0.25), new GradientStop(Color.Parse("#323846"), 0.25), new GradientStop(Color.Parse("#323846"), 0.5), new GradientStop(Color.Parse("#2A2F3A"), 0.5), new GradientStop(Color.Parse("#2A2F3A"), 0.75), new GradientStop(Color.Parse("#323846"), 0.75), new GradientStop(Color.Parse("#323846"), 1) }, SpreadMethod = GradientSpreadMethod.Repeat };

    public List<VShape> Selection => Doc.Shapes.Where(s => State.Selected.Contains(s.Id)).ToList();
    /// <summary>Bildschirm-Pixel je Dokument-Einheit.</summary>
    public double ScreenScale => View.Bounds.Width > 1 ? Math.Min((View.Bounds.Width - 16) / Doc.W, (View.Bounds.Height - 16) / Doc.H) : 1;
    double Tol => 6 / Math.Max(0.05, ScreenScale);

    public void RedrawShapes()
    {
        _shapes.Children.Clear();
        foreach (var s in Doc.Shapes) _shapes.Children.Add(VectorRender.Build(s));
        if (_creating is not null) _shapes.Children.Add(VectorRender.Build(_creating));
        _overlay.InvalidateVisual();
    }

    void RedrawGrid()
    {
        _grid.Children.Clear();
        if (!State.ShowGrid) return;
        var pen = new SolidColorBrush(Color.Parse("#30FFFFFF"));
        var step = Math.Max(5, State.Grid * (Doc.W / State.Grid > 200 ? 5 : 1));
        for (var x = step; x < Doc.W; x += step) _grid.Children.Add(new Line { StartPoint = new Point(x, 0), EndPoint = new Point(x, Doc.H), Stroke = pen, StrokeThickness = 1 });
        for (var y = step; y < Doc.H; y += step) _grid.Children.Add(new Line { StartPoint = new Point(0, y), EndPoint = new Point(Doc.W, y), Stroke = pen, StrokeThickness = 1 });
    }

    double Sn(double v, bool snap = true) => snap && State.SnapOn ? VectorKit.Snap(v, State.Grid) : v;

    /// <summary>Rahmen der Auswahl: eine Form (oder Gruppe aus einer Form) mit ihrer Drehung, sonst achsenparallel.</summary>
    public (double Cx, double Cy, double W, double H, double Rot)? Frame()
    {
        var sel = Selection;
        if (sel.Count == 0) return null;
        if (sel.Count == 1) { var s = sel[0]; return (s.Cx, s.Cy, s.W * s.Scale, s.H * s.Scale, s.Rot); }
        var b = VectorKit.Bounds(sel);
        return (b.X + b.W / 2, b.Y + b.H / 2, b.W, b.H, 0);
    }

    static (double X, double Y) Rot((double X, double Y) p, double cx, double cy, double deg)
    {
        var a = deg * Math.PI / 180; double c = Math.Cos(a), s = Math.Sin(a); double x = p.X - cx, y = p.Y - cy;
        return (cx + x * c - y * s, cy + x * s + y * c);
    }

    /// <summary>Griffe des Rahmens in Dokument-Koordinaten: 0–7 Größe (im Uhrzeigersinn ab oben links), 8 Drehen.</summary>
    public List<(double X, double Y)> Handles((double Cx, double Cy, double W, double H, double Rot) f)
    {
        var u = new (double, double)[] { (0, 0), (0.5, 0), (1, 0), (1, 0.5), (1, 1), (0.5, 1), (0, 1), (0, 0.5) };
        var list = u.Select(p => Rot((f.Cx - f.W / 2 + p.Item1 * f.W, f.Cy - f.H / 2 + p.Item2 * f.H), f.Cx, f.Cy, f.Rot)).ToList();
        list.Add(Rot((f.Cx, f.Cy - f.H / 2 - 28 / Math.Max(0.05, ScreenScale)), f.Cx, f.Cy, f.Rot));
        return list;
    }

    // ───────────── Zeiger ─────────────
    void OnPress(object? sender, PointerPressedEventArgs e)
    {
        Surface.Focus(); State.WantFocus = true;
        var pt = e.GetCurrentPoint(Surface); var p = pt.Position; _start = _last = p; _moved = false;
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift); var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        if (pt.Properties.IsRightButtonPressed)
        {
            var hit = VectorKit.HitTop(Doc, p.X, p.Y, Tol);
            if (hit >= 0 && !State.Selected.Contains(Doc.Shapes[hit].Id)) Select([Doc.Shapes[hit].Id], false, ctrl);
            _overlay.InvalidateVisual();
            Surface.ContextMenu = Menu();
            return;
        }
        if (!pt.Properties.IsLeftButtonPressed) return;
        e.Pointer.Capture(Surface); e.Handled = true;
        _before = Doc.Shapes.ToDictionary(s => s.Id, s => s.Clone());
        switch (State.Tool)
        {
            case "select":
            {
                if (Frame() is { } f)
                {
                    var hs = Handles(f);
                    for (var i = 8; i >= 0; i--)
                        if (Math.Abs(hs[i].X - p.X) <= Tol && Math.Abs(hs[i].Y - p.Y) <= Tol) { _drag = i == 8 ? DragKind.Rotate : DragKind.Resize; _handle = i; _frame0 = f; return; }
                }
                var hit = VectorKit.HitTop(Doc, p.X, p.Y, Tol);
                if (hit < 0) { if (!shift) State.Selected.Clear(); _drag = DragKind.Band; _overlay.Band = new Rect(p, p); _overlay.InvalidateVisual(); return; }
                var s = Doc.Shapes[hit];
                if (e.ClickCount >= 2)
                {
                    Select([s.Id], false, true);
                    if (s.Kind == "text") { _drag = DragKind.None; FocusText(); return; }
                    if (s.Kind is not "image") { SetTool("nodes"); return; }
                }
                if (shift && State.Selected.Contains(s.Id)) { foreach (var id in ctrl ? [s.Id] : VectorKit.WithGroups(Doc, [s.Id])) State.Selected.Remove(id); _drag = DragKind.None; _overlay.InvalidateVisual(); RefreshBar(); return; }
                if (!State.Selected.Contains(s.Id)) Select([s.Id], shift, ctrl);
                _drag = DragKind.Move; _overlay.InvalidateVisual();
                return;
            }
            case "nodes":
            {
                var sel = Selection;
                if (sel.Count == 1 && sel[0].Kind is not ("text" or "image"))
                {
                    var s = sel[0]; var (n, closed) = VectorKit.ToNodes(s);
                    for (var i = n.Count - 1; i >= 0; i--)
                    {
                        foreach (var (part, lx, ly) in new[] { ("a", n[i].X, n[i].Y), ("in", n[i].InX, n[i].InY), ("out", n[i].OutX, n[i].OutY) })
                        {
                            if (part != "a" && (i != State.Node || n[i].IsCorner)) continue; // Griffe nur am gewählten Punkt
                            var d = VectorKit.ToDoc(s, lx, ly);
                            if (Math.Abs(d.X - p.X) <= Tol && Math.Abs(d.Y - p.Y) <= Tol)
                            {
                                if (e.ClickCount >= 2 && part == "a")
                                {
                                    VectorKit.ToPath(s); s.Nodes[i] = VectorKit.ToggleSmooth(s.Nodes, i, s.Closed); VectorKit.Normalize(s);
                                    State.Node = i; Done(s.Nodes[i].IsCorner ? "Punkt zur Ecke" : "Punkt gerundet"); return;
                                }
                                State.Node = i; _part = part; _drag = DragKind.Node; _overlay.InvalidateVisual(); return;
                            }
                        }
                    }
                }
                var hit = VectorKit.HitTop(Doc, p.X, p.Y, Tol);
                State.Node = -1;
                if (hit >= 0) Select([Doc.Shapes[hit].Id], false, true); else State.Selected.Clear();
                _drag = DragKind.None; _overlay.InvalidateVisual();
                return;
            }
            case "pen":
            {
                if (_pen.Count >= 2)
                {
                    var first = _pen[0];
                    if (Math.Abs(first.X - p.X) <= Tol && Math.Abs(first.Y - p.Y) <= Tol) { FinishPen(true); _drag = DragKind.None; return; }
                }
                if (e.ClickCount >= 2 && _pen.Count >= 2) { FinishPen(false); _drag = DragKind.None; return; }
                _pen.Add(VNode.Corner(Sn(p.X), Sn(p.Y))); _drag = DragKind.PenHandle; _overlay.InvalidateVisual();
                return;
            }
            default:
            {
                var kind = State.Tool;
                _creating = VectorKit.New(kind, Sn(p.X), Sn(p.Y), 1, 1, VectorKit.NextId(Doc, kind), Doc.Shapes.LastOrDefault(s => s.Kind != "text" && s.Fill.Length > 0)?.Fill ?? "#D4A017");
                if (kind == "text") { _creating.Fill = VectorKit.IsColor(Doc.Background) && LogoKit.Contrast(Doc.Background, "#FFFFFF") > 3 ? "#FFFFFF" : "#1F2937"; }
                _drag = DragKind.Create;
                return;
            }
        }
    }

    void OnMove(object? sender, PointerEventArgs e)
    {
        if (_drag == DragKind.None) return;
        var p = e.GetPosition(Surface);
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift); var alt = e.KeyModifiers.HasFlag(KeyModifiers.Alt);
        if (!_moved && Math.Abs(p.X - _start.X) + Math.Abs(p.Y - _start.Y) < Tol / 3) return;
        _moved = true; _last = p;
        double dx = p.X - _start.X, dy = p.Y - _start.Y;
        switch (_drag)
        {
            case DragKind.Move:
            {
                var sel = Selection; if (sel.Count == 0) break;
                var b0 = VectorKit.Bounds(sel.Select(s => _before[s.Id]));
                if (State.SnapOn) { dx = Sn(b0.X + dx) - b0.X; dy = Sn(b0.Y + dy) - b0.Y; }
                if (shift) { if (Math.Abs(dx) > Math.Abs(dy)) dy = 0; else dx = 0; }
                foreach (var s in sel) { s.X = _before[s.Id].X + dx; s.Y = _before[s.Id].Y + dy; }
                RedrawShapes(); break;
            }
            case DragKind.Resize: ResizeTo(p, shift, alt); RedrawShapes(); break;
            case DragKind.Rotate:
            {
                var f = _frame0;
                var a = (Math.Atan2(p.Y - f.Cy, p.X - f.Cx) - Math.Atan2(_start.Y - f.Cy, _start.X - f.Cx)) * 180 / Math.PI;
                if (shift || State.SnapOn) a = Math.Round(a / 15) * 15;
                foreach (var s in Selection)
                {
                    var b = _before[s.Id]; var c = Rot((b.Cx, b.Cy), f.Cx, f.Cy, a);
                    s.Rot = Norm(b.Rot + a); s.X = c.X - s.W / 2; s.Y = c.Y - s.H / 2;
                }
                RedrawShapes(); break;
            }
            case DragKind.Band:
                _overlay.Band = new Rect(new Point(Math.Min(_start.X, p.X), Math.Min(_start.Y, p.Y)), new Point(Math.Max(_start.X, p.X), Math.Max(_start.Y, p.Y)));
                _overlay.InvalidateVisual(); break;
            case DragKind.Node:
            {
                var s = Selection.FirstOrDefault(); if (s is null || State.Node < 0) break;
                if (s.Kind != "path" && s.Kind != "line") VectorKit.ToPath(s);
                var (lx, ly) = VectorKit.ToLocal(s, Sn(p.X), Sn(p.Y));
                var n = s.Nodes[State.Node];
                s.Nodes[State.Node] = _part switch
                {
                    "a" => n.Offset(lx - n.X, ly - n.Y),
                    "in" => alt ? n with { InX = lx, InY = ly } : n with { InX = lx, InY = ly, OutX = 2 * n.X - lx, OutY = 2 * n.Y - ly },
                    _ => alt ? n with { OutX = lx, OutY = ly } : n with { OutX = lx, OutY = ly, InX = 2 * n.X - lx, InY = 2 * n.Y - ly },
                };
                RedrawShapes(); break;
            }
            case DragKind.PenHandle:
            {
                var n = _pen[^1]; var ox = Sn(p.X); var oy = Sn(p.Y);
                _pen[^1] = new VNode(n.X, n.Y, 2 * n.X - ox, 2 * n.Y - oy, ox, oy);
                _overlay.InvalidateVisual(); break;
            }
            case DragKind.Create when _creating is { } c:
            {
                double x0 = Sn(_start.X), y0 = Sn(_start.Y), x1 = Sn(p.X), y1 = Sn(p.Y);
                if (shift && c.Kind != "line") { var m = Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0)); x1 = x0 + Math.Sign(x1 - x0 + 1e-9) * m; y1 = y0 + Math.Sign(y1 - y0 + 1e-9) * m; }
                if (c.Kind == "line")
                {
                    if (shift) { var a = Math.Round(Math.Atan2(y1 - y0, x1 - x0) / (Math.PI / 4)) * Math.PI / 4; var l = Math.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0)); x1 = x0 + l * Math.Cos(a); y1 = y0 + l * Math.Sin(a); }
                    c.X = 0; c.Y = 0; c.W = 1; c.H = 1; c.Nodes = [VNode.Corner(x0, y0), VNode.Corner(x1, y1)]; VectorKit.Normalize(c);
                }
                else if (c.Kind == "text") { c.FontSize = Math.Max(8, Math.Abs(y1 - y0) / VectorKit.TextLine); c.X = Math.Min(x0, x1); c.Y = Math.Min(y0, y1); VectorRender.FitText(c); }
                else { c.X = Math.Min(x0, x1); c.Y = Math.Min(y0, y1); c.W = Math.Max(1, Math.Abs(x1 - x0)); c.H = Math.Max(1, Math.Abs(y1 - y0)); }
                RedrawShapes(); break;
            }
        }
    }

    static double Norm(double a) { a %= 360; return a > 180 ? a - 360 : a <= -180 ? a + 360 : a; }

    void ResizeTo(Point p, bool keepRatio, bool fromCenter)
    {
        var f = _frame0; var h = _handle;
        var (lx, ly) = Rot((p.X, p.Y), f.Cx, f.Cy, -f.Rot); // in den ungedrehten Rahmen
        lx = Sn(lx); ly = Sn(ly);
        double l = f.Cx - f.W / 2, t = f.Cy - f.H / 2, r = f.Cx + f.W / 2, b = f.Cy + f.H / 2;
        if (h is 0 or 6 or 7) l = lx; if (h is 2 or 3 or 4) r = lx;
        if (h is 0 or 1 or 2) t = ly; if (h is 4 or 5 or 6) b = ly;
        if (fromCenter) { if (h is 0 or 6 or 7) r = 2 * f.Cx - l; if (h is 2 or 3 or 4) l = 2 * f.Cx - r; if (h is 0 or 1 or 2) b = 2 * f.Cy - t; if (h is 4 or 5 or 6) t = 2 * f.Cy - b; }
        double nw = Math.Max(1, Math.Abs(r - l)), nh = Math.Max(1, Math.Abs(b - t));
        var corner = h is 0 or 2 or 4 or 6;
        if ((keepRatio || ScaleMode || Selection.Any(s => s.Kind is "text")) && corner)
        {
            var k = Math.Max(nw / Math.Max(1, f.W), nh / Math.Max(1, f.H)); nw = f.W * k; nh = f.H * k;
            if (!fromCenter) { if (h is 0 or 6) l = r - nw; else r = l + nw; if (h is 0 or 2) t = b - nh; else b = t + nh; }
        }
        double sx = nw / Math.Max(1e-6, f.W), sy = nh / Math.Max(1e-6, f.H);
        if (ScaleMode) { var k = h is 1 or 5 ? sy : h is 3 or 7 ? sx : Math.Max(sx, sy); sx = sy = k; }
        var ncx = (Math.Min(l, r) + Math.Max(l, r)) / 2; var ncy = (Math.Min(t, b) + Math.Max(t, b)) / 2;
        if (ScaleMode && h is 1 or 5) ncx = f.Cx; if (ScaleMode && h is 3 or 7) ncy = f.Cy;
        var c = Rot((ncx, ncy), f.Cx, f.Cy, f.Rot);
        foreach (var s in Selection)
        {
            var b0 = _before[s.Id];
            var (ox, oy) = Rot((b0.Cx, b0.Cy), f.Cx, f.Cy, -f.Rot);
            var (px, py) = Rot((c.X + (ox - f.Cx) * sx, c.Y + (oy - f.Cy) * sy), c.X, c.Y, f.Rot);
            if (ScaleMode) { s.Scale = Math.Max(0.01, b0.Scale * sx); s.X = px - s.W / 2; s.Y = py - s.H / 2; continue; }
            var copy = b0.Clone();
            var (ssx, ssy) = Math.Abs(Norm(b0.Rot - f.Rot)) % 180 is > 45 and < 135 ? (sy, sx) : (sx, sy);
            VectorKit.Resize(copy, 0, 0, b0.W * ssx, b0.H * ssy);
            s.W = copy.W; s.H = copy.H; s.Nodes = copy.Nodes; s.FontSize = copy.FontSize; s.X = px - s.W / 2; s.Y = py - s.H / 2;
            if (s.Kind == "text") VectorRender.FitText(s);
        }
    }

    void OnRelease(object? sender, PointerReleasedEventArgs e)
    {
        var drag = _drag; _drag = DragKind.None; e.Pointer.Capture(null);
        switch (drag)
        {
            case DragKind.Move when _moved: Transformed("Verschoben"); break;
            case DragKind.Resize when _moved: Transformed(ScaleMode ? "Größe geändert" : "Größe geändert"); break;
            case DragKind.Rotate when _moved: Transformed("Gedreht"); break;
            case DragKind.Band:
            {
                var r = _overlay.Band; _overlay.Band = null;
                if (r is { } band && band.Width + band.Height > Tol)
                    foreach (var s in Doc.Shapes) { var b = VectorKit.Bounds(s); if (band.Contains(new Rect(b.X, b.Y, b.W, b.H))) State.Selected.Add(s.Id); }
                _overlay.InvalidateVisual(); RefreshBar(); break;
            }
            case DragKind.Node when _moved: { if (Selection.FirstOrDefault() is { } s) VectorKit.Normalize(s); Done("Punkt verschoben"); break; }
            case DragKind.Create when _creating is { } c:
            {
                _creating = null;
                if (!_moved)
                {
                    if (c.Kind == "text") { c.FontSize = 48; c.X = Sn(_start.X); c.Y = Sn(_start.Y); VectorRender.FitText(c); }
                    else if (c.Kind == "line") { c.X = 0; c.Y = 0; c.W = 1; c.H = 1; c.Nodes = [VNode.Corner(_start.X - 80, _start.Y), VNode.Corner(_start.X + 80, _start.Y)]; VectorKit.Normalize(c); c.H = Math.Max(c.H, 1); }
                    else { c.W = c.H = Math.Min(160, Math.Min(Doc.W, Doc.H) / 3); c.X = Sn(_start.X - c.W / 2); c.Y = Sn(_start.Y - c.H / 2); }
                }
                Doc.Shapes.Add(c); State.Selected.Clear(); State.Selected.Add(c.Id); State.Tool = "select";
                if (c.Kind == "text") FocusTextLater(); // vor dem Neuzeichnen merken – das neue Textfeld bekommt den Fokus
                Done(VectorKit.Label(c.Kind) + " gezeichnet");
                break;
            }
        }
    }

    /// <summary>Schritt abschließen: Animation bekommt vorher/nachher, sonst geht das ganze Dokument ins Programm.</summary>
    void Transformed(string label)
    {
        if (TransformCommit is { } tc)
        {
            var list = Selection.Select(s => (_before[s.Id], s.Clone())).ToList();
            tc(list, label); return;
        }
        Done(label);
    }

    public void Done(string label) => Commit(Doc.Clone(), label);

    /// <summary>T-0730: letzter Hinweis an den Nutzer (z. B. „mindestens zwei Formen“) – kein Rückgängig-Schritt, das Dokument bleibt unverändert.
    /// Das Rechtsklick-Menü graut „Gruppieren“ unter zwei Formen aus; Kürzel Strg+G und der Knopf unter „Anordnen“ landen hier.</summary>
    public string LastHint { get; private set; } = "";
    void Hint(string text) => LastHint = text;

    /// <summary>Anderen Stand zeigen (Animation: Bühne zu einer anderen Zeit), ohne neu aufzubauen.</summary>
    public void ShowDoc(VDoc d)
    {
        if (_drag != DragKind.None) return;
        Doc = d.Clone();
        foreach (var t in Doc.Shapes.Where(s => s.Kind == "text")) VectorRender.FitText(t);
        RedrawShapes();
    }

    void FinishPen(bool close)
    {
        if (_pen.Count < 2) { _pen.Clear(); _overlay.InvalidateVisual(); return; }
        var s = new VShape { Id = VectorKit.NextId(Doc, "pfad"), Kind = "path", Closed = close, Nodes = [.. _pen], X = 0, Y = 0, W = 1, H = 1, Fill = close ? (Doc.Shapes.LastOrDefault(x => x.Fill.Length > 0)?.Fill ?? "#D4A017") : "", Stroke = close ? "" : "#F5C542", StrokeW = close ? 0 : 6 };
        VectorKit.Normalize(s);
        _pen.Clear(); Doc.Shapes.Add(s); State.Selected.Clear(); State.Selected.Add(s.Id); State.Tool = "select";
        Done(close ? "Pfad geschlossen" : "Pfad gezeichnet");
    }

    public IReadOnlyList<VNode> PenNodes => _pen;
    public VShape? Creating => _creating;

    void Select(IEnumerable<string> ids, bool add, bool exact)
    {
        if (!add) State.Selected.Clear();
        foreach (var id in exact ? ids.ToHashSet() : VectorKit.WithGroups(Doc, ids)) State.Selected.Add(id);
        State.Node = -1;
        _overlay.InvalidateVisual();
        RefreshBar();
        SelectionChanged?.Invoke();
    }

    public event Action? SelectionChanged;

    /// <summary>Seite neu zeichnen ohne Änderung am Dokument (Werkzeug gewechselt).</summary>
    public Action? Rerender { get; set; }

    public void SetTool(string tool) { State.Tool = tool; _pen.Clear(); if (Rerender is { } r) r(); else { RefreshBar(); _overlay.InvalidateVisual(); } }

    /// <summary>Leiste über der Fläche passend zur Auswahl neu aufbauen (ohne die Seite neu zu zeichnen – laufendes Ziehen bleibt).</summary>
    public void RefreshBar()
    {
        Children.Remove(ContextBar);
        ContextBar = BuildBar(); DockPanel.SetDock(ContextBar, Avalonia.Controls.Dock.Top);
        Children.Insert(0, ContextBar);
    }

    // ───────────── Tastatur ─────────────
    void OnKey(object? sender, KeyEventArgs e)
    {
        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control); var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var sel = Selection; var step = shift ? 10 : 1;
        void Nudge(double dx, double dy)
        {
            if (sel.Count == 0) return;
            _before = Doc.Shapes.ToDictionary(s => s.Id, s => s.Clone());
            foreach (var s in sel) { s.X += dx; s.Y += dy; }
            Transformed("Verschoben");
        }
        switch (e.Key)
        {
            case Key.Delete or Key.Back when sel.Count > 0: Act("delete"); break;
            case Key.Left: Nudge(-step, 0); break;
            case Key.Right: Nudge(step, 0); break;
            case Key.Up: Nudge(0, -step); break;
            case Key.Down: Nudge(0, step); break;
            case Key.Escape:
                if (_pen.Count > 0) { _pen.Clear(); _overlay.InvalidateVisual(); }
                else { State.Selected.Clear(); State.Node = -1; _overlay.InvalidateVisual(); RefreshBar(); }
                break;
            case Key.Enter when _pen.Count >= 2: FinishPen(false); break;
            case Key.A when ctrl: State.Selected.Clear(); foreach (var s in Doc.Shapes) State.Selected.Add(s.Id); _overlay.InvalidateVisual(); RefreshBar(); break;
            case Key.D when ctrl: Act("duplicate"); break;
            case Key.C when ctrl: Act("copy"); break;
            case Key.X when ctrl: Act("cut"); break;
            case Key.V when ctrl: Act("paste"); break;
            case Key.G when ctrl && shift: Act("ungroup"); break;
            case Key.G when ctrl: Act("group"); break;
            case Key.OemCloseBrackets: Act(ctrl ? "order-vorne" : "order-ganz-vorne"); break;
            case Key.OemOpenBrackets: Act(ctrl ? "order-hinten" : "order-ganz-hinten"); break;
            default:
                if (ctrl || e.KeyModifiers.HasFlag(KeyModifiers.Alt)) return;
                var t = VectorEditorState.Tools.FirstOrDefault(x => x.Key == e.Key);
                if (t.Id is null) return;
                SetTool(t.Id); break;
        }
        e.Handled = true;
    }

    /// <summary>Befehle für Menü, Kürzel und Leiste.</summary>
    public void Act(string cmd)
    {
        var ids = State.Selected.ToList(); var sel = Selection;
        switch (cmd)
        {
            case "delete": if (sel.Count == 0) return; Doc.Shapes.RemoveAll(s => State.Selected.Contains(s.Id)); State.Selected.Clear(); Done(sel.Count == 1 ? "Form gelöscht" : $"{sel.Count} Formen gelöscht"); return;
            case "duplicate": { if (sel.Count == 0) return; var n = VectorKit.Duplicate(Doc, sel); State.Selected.Clear(); State.Selected.UnionWith(n); Done("Dupliziert"); return; }
            case "copy": VectorEditorState.Clipboard.Clear(); VectorEditorState.Clipboard.AddRange(sel.Select(s => s.Clone())); return;
            case "cut": VectorEditorState.Clipboard.Clear(); VectorEditorState.Clipboard.AddRange(sel.Select(s => s.Clone())); Act("delete"); return;
            case "paste": { if (VectorEditorState.Clipboard.Count == 0) return; var n = VectorKit.Duplicate(Doc, VectorEditorState.Clipboard); State.Selected.Clear(); State.Selected.UnionWith(n); Done("Eingefügt"); return; }
            case "group":
                // T-0730: Gruppieren braucht mindestens zwei Formen – sonst nur ein Hinweis, nie eine Ausnahme im Bedien-Handler
                if (sel.Count < 2) { Hint("Zum Gruppieren mindestens zwei Formen wählen."); return; }
                VectorKit.Group(Doc, ids); Done("Gruppiert"); return;
            case "ungroup": VectorKit.Ungroup(Doc, ids); Done("Gruppe gelöst"); return;
            case "topath": foreach (var s in sel) VectorKit.ToPath(s); State.Tool = "nodes"; Done("In Pfad umgewandelt"); return;
            case "dist-h": VectorKit.Distribute(Doc, ids, "h"); Done("Waagrecht verteilt"); return;
            case "dist-v": VectorKit.Distribute(Doc, ids, "v"); Done("Senkrecht verteilt"); return;
        }
        if (cmd.StartsWith("align-", StringComparison.Ordinal)) { if (sel.Count == 0) return; _before = Doc.Shapes.ToDictionary(s => s.Id, s => s.Clone()); VectorKit.Align(Doc, ids, cmd[6..]); Transformed("Ausgerichtet"); return; }
        if (cmd.StartsWith("order-", StringComparison.Ordinal)) { if (sel.Count == 0) return; VectorKit.Order(Doc, ids, cmd[6..]); Done(VectorKit.OrderModes.First(o => o.Id == cmd[6..]).Label); return; }
    }

    ContextMenu Menu()
    {
        var sel = Selection; var m = new ContextMenu { Name = _p + "-menu" };
        MenuItem I(string header, string cmd, string gesture = "", bool on = true) { var i = new MenuItem { Header = header, InputGesture = gesture.Length > 0 ? KeyGesture.Parse(gesture) : null, IsEnabled = on, Name = _p + "-menu-" + cmd }; i.Click += (_, _) => Act(cmd); return i; }
        var any = sel.Count > 0;
        m.Items.Add(I("Ausschneiden", "cut", "Ctrl+X", any)); m.Items.Add(I("Kopieren", "copy", "Ctrl+C", any)); m.Items.Add(I("Einfügen", "paste", "Ctrl+V", VectorEditorState.Clipboard.Count > 0));
        m.Items.Add(I("Duplizieren", "duplicate", "Ctrl+D", any)); m.Items.Add(I("Löschen", "delete", "Delete", any));
        m.Items.Add(new Separator());
        m.Items.Add(I("Gruppieren", "group", "Ctrl+G", sel.Count > 1)); m.Items.Add(I("Gruppe lösen", "ungroup", "Ctrl+Shift+G", sel.Any(s => s.Group.Length > 0)));
        var order = new MenuItem { Header = "Anordnen", IsEnabled = any };
        foreach (var (id, label) in VectorKit.OrderModes) order.Items.Add(I(label, "order-" + id));
        m.Items.Add(order);
        var align = new MenuItem { Header = "Ausrichten", IsEnabled = any };
        foreach (var (id, label) in VectorKit.AlignModes) align.Items.Add(I(label, "align-" + id));
        align.Items.Add(I("Waagrecht verteilen", "dist-h", "", sel.Count > 2)); align.Items.Add(I("Senkrecht verteilen", "dist-v", "", sel.Count > 2));
        m.Items.Add(align);
        m.Items.Add(I("In Pfad umwandeln (Punkte bearbeiten)", "topath", "", sel.Any(s => s.Kind is "rect" or "ellipse" or "polygon" or "star" or "line")));
        return m;
    }

    // ───────────── Leiste über der Fläche ─────────────
    TextBox? _textBox;
    void FocusText() { _textBox?.Focus(); _textBox?.SelectAll(); }
    void FocusTextLater() => State.FocusText = true;

    static readonly string[] Swatches = ["#D4A017", "#F5C542", "#FFFFFF", "#111827", "#1E3A8A", "#0F4C5C", "#5FA8D3", "#7A2E1F", "#E0A458", "#16A34A", "#DC2626", "#9333EA"];

    Control ColorPicker(string name, string label, string value, bool allowNone, Action<string> apply)
    {
        var sw = new Border { Width = 18, Height = 18, CornerRadius = new CornerRadius(4), BorderBrush = Brush.Parse("#66FFFFFF"), BorderThickness = new Thickness(1), Background = VectorKit.IsColor(value) ? new SolidColorBrush(Color.Parse(value)) : Checker() };
        var b = new Button { Name = name, Padding = new Thickness(6, 3), Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { sw, new TextBlock { Text = label, FontSize = 12, VerticalAlignment = VerticalAlignment.Center } } } };
        ToolTip.SetTip(b, label + ": " + (value.Length > 0 ? value : "keine"));
        var grid = new WrapPanel { Width = 196 };
        var own = VectorKit.Palette(Doc).Take(6).ToList();
        foreach (var c in own.Concat(Swatches).Distinct())
        {
            var cb = new Button { Width = 26, Height = 26, Margin = new Thickness(2), Padding = new Thickness(0), Background = new SolidColorBrush(Color.Parse(c)), BorderThickness = new Thickness(c == value ? 2 : 0), BorderBrush = Brush.Parse("#FFD54F") };
            ToolTip.SetTip(cb, c); var cc = c;
            cb.Click += (_, _) => { b.Flyout?.Hide(); apply(cc); };
            grid.Children.Add(cb);
        }
        var hex = new TextBox { Name = name + "-hex", Text = value, Width = 110, PlaceholderText = "#RRGGBB" };
        void Use() { var v = (hex.Text ?? "").Trim(); if (v.Length == 7 && !v.StartsWith('#')) v = "#" + v; if (v.Length > 0 && v != value) { b.Flyout?.Hide(); apply(v); } }
        hex.KeyDown += (_, e) => { if (e.Key == Key.Enter) { Use(); e.Handled = true; } };
        var panel = new StackPanel { Spacing = 6, Children = { grid, new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { hex, Btn(name + "-ok", "Übernehmen", Use) } } } };
        if (allowNone) panel.Children.Add(Btn(name + "-none", "Keine", () => { b.Flyout?.Hide(); apply(""); }));
        b.Flyout = new Flyout { Content = panel, Placement = PlacementMode.BottomEdgeAlignedLeft };
        return b;
    }

    static Button Btn(string name, string label, Action click, string tip = "")
    {
        var b = new Button { Name = name, Content = label, Padding = new Thickness(8, 3), FontSize = 12 };
        if (tip.Length > 0) ToolTip.SetTip(b, tip);
        b.Click += (_, _) => click();
        return b;
    }

    void Change(string label, Action<VShape> f)
    {
        var sel = Selection; if (sel.Count == 0) return;
        foreach (var s in sel) { f(s); if (s.Kind == "text") VectorRender.FitText(s); }
        State.WantFocus = true; Done(label);
    }

    /// <summary>Farbe ändern – Animation fängt das ab (Schlüssel „Farbe“).</summary>
    public Action<IReadOnlyList<VShape>, string>? FillCommit { get; set; }

    Control BuildBar()
    {
        var bar = new WrapPanel { Name = _p + "-bar", Margin = new Thickness(0, 0, 0, 6) };
        void Add(Control c) { c.Margin = new Thickness(0, 0, 6, 4); c.VerticalAlignment = VerticalAlignment.Center; bar.Children.Add(c); }
        var sel = Selection; var s = sel.FirstOrDefault();
        if (s is null)
        {
            Add(new TextBlock { Name = _p + "-hint", Text = State.Tool switch { "select" => "Form anklicken oder links ein Werkzeug wählen und auf der Fläche ziehen.", "pen" => "Klicken setzt Ecken, Ziehen setzt Rundungen. Enter beendet.", _ => "Auf der Fläche ziehen." }, Foreground = Brush.Parse("#9CA3AF"), FontSize = 12 });
        }
        else
        {
            Add(ColorPicker(_p + "-fill", "Füllung", s.Fill, true, c => { if (FillCommit is { } fc && c.Length > 0) { fc(sel, c); return; } Change("Füllung", x => { x.Fill = c; if (c.Length == 0) x.Fill2 = ""; }); }));
            if (s.Kind is not ("text" or "image" or "line"))
            {
                var grad = new CheckBox { Name = _p + "-grad", Content = "Verlauf", IsChecked = s.Fill2.Length > 0, FontSize = 12 };
                ToolTip.SetTip(grad, "Füllung als Verlauf von der ersten zur zweiten Farbe");
                grad.Click += (_, _) => Change(grad.IsChecked == true ? "Verlauf an" : "Verlauf aus", x => x.Fill2 = grad.IsChecked == true ? (x.Fill2.Length > 0 ? x.Fill2 : "#111827") : "");
                Add(grad);
                if (s.Fill2.Length > 0)
                {
                    Add(ColorPicker(_p + "-fill2", "Zweite Farbe", s.Fill2, false, c => Change("Verlauf", x => x.Fill2 = c)));
                    var ang = new NumericUpDown { Name = _p + "-grad-angle", Value = (decimal)s.GradAngle, Minimum = -360, Maximum = 360, Increment = 15, Width = 118, FormatString = "0", FontSize = 12 };
                    ToolTip.SetTip(ang, "Richtung des Verlaufs in Grad (90 = oben nach unten)");
                    ang.ValueChanged += (_, e) => { if (e.NewValue is { } v) Change("Verlaufsrichtung", x => x.GradAngle = (double)v); };
                    Add(ang);
                }
            }
            if (s.Kind is not ("text" or "image"))
            {
                Add(ColorPicker(_p + "-stroke", "Kontur", s.Stroke, true, c => Change("Kontur", x => { x.Stroke = c; if (c.Length > 0 && x.StrokeW <= 0) x.StrokeW = 4; })));
                var sw = new NumericUpDown { Name = _p + "-stroke-w", Value = (decimal)s.StrokeW, Minimum = 0, Maximum = 200, Increment = 1, Width = 118, FormatString = "0.#", FontSize = 12 };
                ToolTip.SetTip(sw, "Breite der Kontur");
                sw.ValueChanged += (_, e) => { if (e.NewValue is { } v) Change("Konturbreite", x => x.StrokeW = (double)v); };
                Add(sw);
            }
            if (s.Kind is "polygon" or "star")
            {
                var n = new NumericUpDown { Name = _p + "-sides", Value = s.Sides, Minimum = 3, Maximum = 32, Increment = 1, Width = 118, FormatString = "0", FontSize = 12 };
                ToolTip.SetTip(n, s.Kind == "star" ? "Zacken" : "Ecken");
                n.ValueChanged += (_, e) => { if (e.NewValue is { } v) Change("Ecken", x => { if (x.Kind is "polygon" or "star") x.Sides = (int)v; }); };
                Add(n);
            }
            if (s.Kind == "rect")
            {
                var r = new NumericUpDown { Name = _p + "-radius", Value = (decimal)s.Radius, Minimum = 0, Maximum = 2000, Increment = 2, Width = 118, FormatString = "0", FontSize = 12 };
                ToolTip.SetTip(r, "Abgerundete Ecken");
                r.ValueChanged += (_, e) => { if (e.NewValue is { } v) Change("Ecken gerundet", x => { if (x.Kind == "rect") x.Radius = (double)v; }); };
                Add(r);
            }
            if (s.Kind == "text")
            {
                _textBox = new TextBox { Name = _p + "-text", Text = s.Text, MinWidth = 160, MaxWidth = 280, FontSize = 12, AcceptsReturn = false };
                var shown = s.Text;
                void CommitText() { var v = _textBox.Text ?? ""; if (v != shown && v.Length > 0) { shown = v; Change("Text geändert", x => { if (x.Kind == "text") x.Text = v; }); } }
                _textBox.KeyDown += (_, e) => { if (e.Key == Key.Enter) { CommitText(); e.Handled = true; } };
                _textBox.LostFocus += (_, _) => CommitText();
                _textBox.GotFocus += (_, _) => State.WantFocus = false;
                Add(_textBox);
                var font = new ComboBox { Name = _p + "-font", ItemsSource = LogoKit.Fonts.Concat(["Arial", "Times New Roman", "Impact", "Bahnschrift"]).Distinct().ToList(), SelectedItem = s.Font, Width = 140, FontSize = 12 };
                font.SelectionChanged += (_, _) => { if (font.SelectedItem is string f && f != s.Font) Change("Schrift " + f, x => { if (x.Kind == "text") x.Font = f; }); };
                Add(font);
                var size = new NumericUpDown { Name = _p + "-size", Value = (decimal)Math.Round(s.FontSize), Minimum = 4, Maximum = 1000, Increment = 2, Width = 118, FormatString = "0", FontSize = 12 };
                size.ValueChanged += (_, e) => { if (e.NewValue is { } v) Change("Schriftgröße", x => { if (x.Kind == "text") { x.FontSize = (double)v; } }); };
                Add(size);
                var bold = new CheckBox { Name = _p + "-bold", Content = "Fett", IsChecked = s.Bold, FontSize = 12 };
                ToolTip.SetTip(bold, "Fett");
                bold.Click += (_, _) => Change("Fett", x => x.Bold = bold.IsChecked == true);
                Add(bold);
            }
            var arrange = new Button { Name = _p + "-arrange", Content = "Anordnen ▾", Padding = new Thickness(8, 3), FontSize = 12 };
            var ap = new StackPanel { Spacing = 4 };
            var row1 = new WrapPanel(); foreach (var (id, label) in VectorKit.AlignModes) row1.Children.Add(Btn(_p + "-align-" + id, label, () => { arrange.Flyout?.Hide(); Act("align-" + id); }, sel.Count == 1 ? "An der Zeichenfläche ausrichten" : "An der Auswahl ausrichten"));
            var row2 = new WrapPanel(); foreach (var (id, label) in VectorKit.OrderModes) row2.Children.Add(Btn(_p + "-order-" + id, label, () => { arrange.Flyout?.Hide(); Act("order-" + id); }));
            var row3 = new WrapPanel { Children = {
                Btn(_p + "-group", "Gruppieren (Strg+G)", () => { arrange.Flyout?.Hide(); Act("group"); }),
                Btn(_p + "-ungroup", "Gruppe lösen", () => { arrange.Flyout?.Hide(); Act("ungroup"); }),
                Btn(_p + "-dist-h", "Waagrecht verteilen", () => { arrange.Flyout?.Hide(); Act("dist-h"); }),
                Btn(_p + "-topath", "In Pfad umwandeln", () => { arrange.Flyout?.Hide(); Act("topath"); }) } };
            foreach (var w in new[] { row1, row2, row3 }) foreach (var c in w.Children) c.Margin = new Thickness(0, 0, 4, 4);
            ap.Children.Add(new TextBlock { Text = "Ausrichten", FontSize = 11, Foreground = Brush.Parse("#9CA3AF") }); ap.Children.Add(row1);
            ap.Children.Add(new TextBlock { Text = "Reihenfolge", FontSize = 11, Foreground = Brush.Parse("#9CA3AF") }); ap.Children.Add(row2);
            ap.Children.Add(row3);
            arrange.Flyout = new Flyout { Content = new Border { MaxWidth = 460, Child = ap }, Placement = PlacementMode.BottomEdgeAlignedLeft };
            Add(arrange);
            Add(Btn(_p + "-delete", "Löschen", () => Act("delete"), "Entf"));
        }
        var grid = new CheckBox { Name = _p + "-grid", Content = "Raster", IsChecked = State.ShowGrid, FontSize = 12 };
        grid.Click += (_, _) => { State.ShowGrid = grid.IsChecked == true; RedrawGrid(); };
        var snap = new CheckBox { Name = _p + "-snap", Content = "Einrasten", IsChecked = State.SnapOn, FontSize = 12 };
        ToolTip.SetTip(snap, $"Beim Ziehen auf das Raster ({State.Grid:0} Punkte) und 15°-Schritte einrasten");
        snap.Click += (_, _) => State.SnapOn = snap.IsChecked == true;
        Add(grid); Add(snap);
        if (PickImportVisible) Add(Btn(_p + "-import", "Bild oder SVG einsetzen", () => PickImport?.Invoke(), "PNG, JPEG oder SVG – oder einfach auf die Fläche ziehen"));
        return bar;
    }

    /// <summary>Knopf „Bild oder SVG einsetzen“ zeigen (wird vor dem Aufbau gesetzt).</summary>
    public static bool PickImportVisible { get; set; } = true;
}

/// <summary>T-0696: Auswahlrahmen, Griffe, Pfadpunkte, Zeichenstift-Vorschau und Auswahlrechteck über der Fläche – in Bildschirm-Pixeln gleich groß.</summary>
public sealed class VectorOverlay(VectorEditor ed) : Control
{
    public Viewbox? View { get; set; }
    public Rect? Band { get; set; }
    static readonly IBrush Gold = new SolidColorBrush(Color.Parse("#FFD54F"));
    static readonly IBrush Dark = new SolidColorBrush(Color.Parse("#111827"));

    public override void Render(DrawingContext g)
    {
        var k = 1 / Math.Max(0.05, ed.ScreenScale);
        var thin = new Pen(Gold, 1.2 * k); var dash = new Pen(Gold, 1 * k, new DashStyle([4, 3], 0)); var handlePen = new Pen(Dark, 1 * k);
        var st = ed.State;
        foreach (var s in ed.Selection)
        {
            var c = new[] { VectorKit.ToDoc(s, 0, 0), VectorKit.ToDoc(s, s.W, 0), VectorKit.ToDoc(s, s.W, s.H), VectorKit.ToDoc(s, 0, s.H) };
            for (var i = 0; i < 4; i++) g.DrawLine(dash, new Point(c[i].X, c[i].Y), new Point(c[(i + 1) % 4].X, c[(i + 1) % 4].Y));
        }
        if (st.Tool == "select" && ed.Frame() is { } f && ed.Selection.Count > 0)
        {
            var hs = ed.Handles(f); var r = 4.5 * k;
            var tc = ed.Handles(f)[1];
            g.DrawLine(thin, new Point(tc.X, tc.Y), new Point(hs[8].X, hs[8].Y));
            if (ed.Selection.Count > 1) { var cs = hs.Take(8).Where((_, i) => i % 2 == 0).ToList(); for (var i = 0; i < 4; i++) g.DrawLine(thin, new Point(cs[i].X, cs[i].Y), new Point(cs[(i + 1) % 4].X, cs[(i + 1) % 4].Y)); }
            for (var i = 0; i < 8; i++) g.DrawRectangle(Gold, handlePen, new Rect(hs[i].X - r, hs[i].Y - r, 2 * r, 2 * r));
            g.DrawEllipse(Gold, handlePen, new Point(hs[8].X, hs[8].Y), r * 1.2, r * 1.2);
        }
        if (st.Tool == "nodes" && ed.Selection is [var p] && p.Kind is not ("text" or "image"))
        {
            var (n, _) = VectorKit.ToNodes(p); var r = 4 * k;
            for (var i = 0; i < n.Count; i++)
            {
                var a = VectorKit.ToDoc(p, n[i].X, n[i].Y);
                if (i == st.Node && !n[i].IsCorner)
                {
                    foreach (var (hx, hy) in new[] { (n[i].InX, n[i].InY), (n[i].OutX, n[i].OutY) })
                    {
                        var h = VectorKit.ToDoc(p, hx, hy);
                        g.DrawLine(thin, new Point(a.X, a.Y), new Point(h.X, h.Y)); g.DrawEllipse(Gold, handlePen, new Point(h.X, h.Y), r, r);
                    }
                }
                g.DrawRectangle(i == st.Node ? Gold : Dark, new Pen(Gold, 1.2 * k), new Rect(a.X - r, a.Y - r, 2 * r, 2 * r));
            }
        }
        if (ed.PenNodes.Count > 0)
        {
            var pts = VectorKit.Sample(ed.PenNodes.ToList(), false);
            for (var i = 0; i + 1 < pts.Count; i++) g.DrawLine(new Pen(Gold, 2 * k), new Point(pts[i].X, pts[i].Y), new Point(pts[i + 1].X, pts[i + 1].Y));
            foreach (var n in ed.PenNodes) g.DrawRectangle(Gold, handlePen, new Rect(n.X - 4 * k, n.Y - 4 * k, 8 * k, 8 * k));
            var last = ed.PenNodes[^1];
            if (!last.IsCorner) { g.DrawLine(thin, new Point(last.InX, last.InY), new Point(last.OutX, last.OutY)); }
        }
        if (Band is { } b) g.DrawRectangle(new SolidColorBrush(Color.Parse("#22FFD54F")), dash, b);
    }
}
