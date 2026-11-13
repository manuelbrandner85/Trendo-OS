using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Trendo.Core;
using Trendo.Core.Pod;
using Trendo.Core.Programs;
using IoPath = System.IO.Path;

namespace Trendo.App;

/// <summary>Print on Demand: Design-Editor. Bedienung nach öffentlich beschriebenen Abläufen von Kittl (docs/nachbau/kittl/anforderungen.md), eigene Umsetzung im Trendo-Stil.
/// Dazu die Anbindung an ein eigenes Kittl-Konto über den offiziellen MCP-Zugang.</summary>
public partial class MainWindow
{
    public double PodZoom { get; private set; } = 0.55;
    public HashSet<string> PodSelected { get; } = [];
    public bool PodGrid { get; private set; }
    public bool PodRulers { get; private set; }
    Point _podPan = new(40, 20);
    List<PodItem> _podClip = [];
    Canvas? _podBoard;
    Border? _podView;
    string _podFitted = "";

    static readonly string[] PodFonts = ["Inter", "Arial", "Georgia", "Times New Roman", "Courier New", "Impact", "Verdana", "Trebuchet MS"];

    static (double W, double H) PodBoardSize(ProgramDocument d) => PodDesign.Board(PodProductOf(d));
    void PodStore(ProgramDocument d, string label, Func<List<PodItem>, IEnumerable<PodItem>> change) => ProgramRun(PodKey, () => d.Edit(label, x => PodDesign.Store(x, change(PodDesign.Items(x)))));

    public void PodAdd(ProgramDocument d, string kind, string src = "")
    {
        var (w, h) = PodBoardSize(d);
        var item = PodDesign.New(kind, w, h, src: src);
        PodSelected.Clear(); PodSelected.Add(item.Id);
        PodStore(d, "Element hinzufügen", l => [.. l, item]);
    }

    static Control PodVisual(PodItem i)
    {
        Control c;
        IBrush Fill() => PodDesign.IsColor(i.Fill) ? Brush.Parse(i.Fill) : Brushes.Black;
        IBrush? Stroke() => i.StrokeW > 0 && PodDesign.IsColor(i.Stroke) ? Brush.Parse(i.Stroke) : null;
        switch (i.Kind)
        {
            case "rect": c = new Rectangle { Fill = Fill(), Stroke = Stroke(), StrokeThickness = i.StrokeW }; break;
            case "ellipse": c = new Ellipse { Fill = Fill(), Stroke = Stroke(), StrokeThickness = i.StrokeW }; break;
            case "image":
                try { c = new Image { Source = new Bitmap(i.Src), Stretch = Stretch.Fill }; }
                catch (Exception e) when (e is IOException or ArgumentException or InvalidOperationException or NullReferenceException or NotSupportedException) { c = new Border { Background = Brush.Parse("#555555"), Child = new TextBlock { Text = "Bild fehlt", Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontSize = 40 } }; }
                break;
            default:
                c = new TextBlock { Text = i.Text, Foreground = Fill(), FontFamily = new FontFamily(i.Font), FontSize = i.Size, FontWeight = i.Bold ? FontWeight.Bold : FontWeight.Normal, TextWrapping = TextWrapping.Wrap };
                break;
        }
        c.Width = Math.Max(1, i.W); c.Height = Math.Max(1, i.H);
        if (i.Shadow) c.Effect = new DropShadowEffect { BlurRadius = 18, OffsetX = 8, OffsetY = 8, Color = Colors.Black, Opacity = 0.55 };
        c.RenderTransform = new RotateTransform(i.Rot);
        Canvas.SetLeft(c, i.X); Canvas.SetTop(c, i.Y);
        c.IsHitTestVisible = false;
        return c;
    }

    /// <summary>Zeichnet alle sichtbaren Elemente; dieselbe Fläche dient auch für die Druckdatei.</summary>
    static Canvas PodCanvas(IEnumerable<PodItem> items, double w, double h, IBrush? background)
    {
        var c = new Canvas { Width = w, Height = h, Background = background, ClipToBounds = true };
        foreach (var i in items.Where(i => !i.Hidden)) c.Children.Add(PodVisual(i));
        return c;
    }

    /// <summary>Entwurf als PNG mit Transparenz in Druckgröße (300 DPI).</summary>
    public string PodExportPng(ProgramDocument d, string file)
    {
        var (w, h) = PodBoardSize(d);
        var items = PodDesign.Items(d.Data);
        if (items.All(i => i.Hidden)) throw new InvalidOperationException("Der Entwurf ist leer.");
        var c = PodCanvas(items, w, h, null);
        c.Measure(new Size(w, h)); c.Arrange(new Rect(0, 0, w, h));
        var scale = PodDesign.PixelsPerUnit;
        using var bmp = new RenderTargetBitmap(new PixelSize((int)Math.Round(w * scale), (int)Math.Round(h * scale)), new Vector(96 * scale, 96 * scale));
        bmp.Render(c);
        Directory.CreateDirectory(IoPath.GetDirectoryName(IoPath.GetFullPath(file))!);
        bmp.Save(file);
        return file;
    }

    /// <summary>Entwurf wird zum Motiv: Druckprüfung und Produktvorschau arbeiten damit weiter.</summary>
    public void PodUseAsMotif(ProgramDocument d)
    {
        var file = IoPath.Combine(_core.DataRoot, "print-on-demand", d.Id + ".png");
        PodExportPng(d, file);
        d.Set("motif", file, "Entwurf als Motiv");
        _podStep = "motiv";
    }

    Control PodDesigner(ProgramDocument d)
    {
        var items = PodDesign.Items(d.Data);
        var (bw, bh) = PodBoardSize(d);
        PodSelected.RemoveWhere(id => items.All(i => i.Id != id));

        var board = PodCanvas(items, bw, bh, Brushes.White);
        board.Name = "pod-board"; board.Focusable = false;
        var world = new Canvas { Name = "pod-world", RenderTransformOrigin = RelativePoint.TopLeft, RenderTransform = new MatrixTransform(new Matrix(PodZoom, 0, 0, PodZoom, _podPan.X, _podPan.Y)), Children = { board } };
        _podBoard = board;
        var overlay = new Canvas { IsHitTestVisible = false };
        void DrawOverlay(IReadOnlyList<PodItem> now)
        {
            overlay.Children.Clear();
            if (PodGrid) { for (var x = 60d; x < bw; x += 60) overlay.Children.Add(new Line { StartPoint = new Point(x, 0), EndPoint = new Point(x, bh), Stroke = Brush.Parse("#332F6BF6"), StrokeThickness = 1 / PodZoom }); for (var y = 60d; y < bh; y += 60) overlay.Children.Add(new Line { StartPoint = new Point(0, y), EndPoint = new Point(bw, y), Stroke = Brush.Parse("#332F6BF6"), StrokeThickness = 1 / PodZoom }); }
            if (PodRulers)
            {
                for (var x = 0d; x <= bw; x += PodDesign.UnitsPerCm * 5) { var t = new TextBlock { Text = (x / PodDesign.UnitsPerCm).ToString("0"), FontSize = 11 / PodZoom, Foreground = Muted }; Canvas.SetLeft(t, x); Canvas.SetTop(t, -18 / PodZoom); overlay.Children.Add(t); }
                for (var y = 0d; y <= bh; y += PodDesign.UnitsPerCm * 5) { var t = new TextBlock { Text = (y / PodDesign.UnitsPerCm).ToString("0"), FontSize = 11 / PodZoom, Foreground = Muted }; Canvas.SetLeft(t, -22 / PodZoom); Canvas.SetTop(t, y); overlay.Children.Add(t); }
            }
            foreach (var i in now.Where(i => PodSelected.Contains(i.Id) && !i.Hidden))
            {
                var r = new Rectangle { Width = i.W, Height = i.H, Stroke = Brush.Parse("#F5B83D"), StrokeThickness = 2 / PodZoom, RenderTransform = new RotateTransform(i.Rot) };
                Canvas.SetLeft(r, i.X); Canvas.SetTop(r, i.Y); overlay.Children.Add(r);
                if (PodSelected.Count == 1)
                    foreach (var (px, py) in new[] { (i.Right, i.Bottom), (i.CenterX, i.Y - 40 / PodZoom) })
                    {
                        var size = 12 / PodZoom;
                        var h = new Ellipse { Width = size, Height = size, Fill = Brush.Parse("#F5B83D") };
                        var (hx, hy) = PodRotate(px, py, i);
                        Canvas.SetLeft(h, hx - size / 2); Canvas.SetTop(h, hy - size / 2); overlay.Children.Add(h);
                    }
            }
        }
        world.Children.Add(overlay);
        DrawOverlay(items);

        var view = new Border { Name = "pod-view", Background = Brush.Parse("#0E131C"), ClipToBounds = true, Focusable = true, CornerRadius = new CornerRadius(8), Child = world };
        string mode = ""; Point start = default; var snapshot = new List<PodItem>(); var moved = false;
        void ApplyTransform() => world.RenderTransform = new MatrixTransform(new Matrix(PodZoom, 0, 0, PodZoom, _podPan.X, _podPan.Y));

        void Redraw(List<PodItem> now)
        {
            for (var n = board.Children.Count - 1; n >= 0; n--) board.Children.RemoveAt(n);
            foreach (var i in now.Where(i => !i.Hidden)) board.Children.Add(PodVisual(i));
            DrawOverlay(now);
        }

        view.PointerPressed += (_, e) =>
        {
            view.Focus(); var p = e.GetPosition(board); start = p; moved = false;
            var props = e.GetCurrentPoint(view).Properties;
            if (props.IsMiddleButtonPressed) { mode = "pan"; start = e.GetPosition(view); e.Pointer.Capture(view); e.Handled = true; return; }
            snapshot = PodDesign.Items(d.Data);
            if (PodSelected.Count == 1 && snapshot.FirstOrDefault(i => PodSelected.Contains(i.Id)) is { } one)
            {
                var near = 14 / PodZoom;
                var (cx, cy) = PodRotate(one.Right, one.Bottom, one);
                if (Math.Abs(p.X - cx) < near && Math.Abs(p.Y - cy) < near) { mode = "scale"; e.Pointer.Capture(view); e.Handled = true; return; }
                var (rx, ry) = PodRotate(one.CenterX, one.Y - 40 / PodZoom, one);
                if (Math.Abs(p.X - rx) < near && Math.Abs(p.Y - ry) < near) { mode = "rotate"; e.Pointer.Capture(view); e.Handled = true; return; }
            }
            var hit = snapshot.LastOrDefault(i => !i.Hidden && PodDesign.Hit(i, p.X, p.Y));
            var multi = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
            if (hit is null) { if (!multi) { PodSelected.Clear(); Show(PodKey); } mode = ""; e.Handled = true; return; }
            if (!PodSelected.Contains(hit.Id))
            {
                if (!multi) PodSelected.Clear();
                foreach (var id in PodDesign.WithGroups(snapshot, [hit.Id])) PodSelected.Add(id);
                DrawOverlay(snapshot);
            }
            mode = "move"; e.Pointer.Capture(view); e.Handled = true;
        };
        view.PointerMoved += (_, e) =>
        {
            if (mode.Length == 0) return;
            if (mode == "pan") { var v = e.GetPosition(view); _podPan += v - start; start = v; ApplyTransform(); return; }
            var p = e.GetPosition(board); var (dx, dy) = (p.X - start.X, p.Y - start.Y);
            if (!moved && Math.Abs(dx) + Math.Abs(dy) < 3 / PodZoom) return;
            moved = true;
            var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control); var alt = e.KeyModifiers.HasFlag(KeyModifiers.Alt);
            var now = snapshot.ToList();
            if (mode == "move")
            {
                var chosen = now.Where(i => PodSelected.Contains(i.Id)).ToList();
                if (!ctrl && chosen.Count > 0)
                {
                    var (x0, y0) = (chosen.Min(i => i.X) + dx, chosen.Min(i => i.Y) + dy);
                    var (sx, sy) = PodDesign.Snap(x0, y0, chosen.Max(i => i.Right) + dx - x0, chosen.Max(i => i.Bottom) + dy - y0, now.Where(i => !PodSelected.Contains(i.Id)), bw, bh, 8 / PodZoom);
                    dx += sx; dy += sy;
                }
                now = now.Select(i => PodSelected.Contains(i.Id) ? i with { X = i.X + dx, Y = i.Y + dy } : i).ToList();
            }
            else
            {
                var i0 = now.Find(i => PodSelected.Contains(i.Id))!; var k = now.IndexOf(i0);
                if (mode == "rotate") now[k] = i0 with { Rot = Math.Round(Math.Atan2(p.Y - i0.CenterY, p.X - i0.CenterX) * 180 / Math.PI + 90, 1) };
                else
                {
                    var f = alt ? 2 : 1; var w = Math.Max(10, i0.W + dx * f); var h = Math.Max(10, i0.H + dy * f);
                    if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) h = w * i0.H / i0.W;
                    now[k] = alt ? i0 with { X = i0.CenterX - w / 2, Y = i0.CenterY - h / 2, W = w, H = h } : i0 with { W = w, H = h };
                }
            }
            Redraw(now);
            _podLive = now;
        };
        view.PointerReleased += (_, e) =>
        {
            var was = mode; mode = ""; e.Pointer.Capture(null);
            if (was == "pan") return;
            if (moved && _podLive is { } done) { _podLive = null; var label = was == "move" ? "Verschieben" : was == "rotate" ? "Drehen" : "Skalieren"; ProgramRun(PodKey, () => d.Edit(label, x => PodDesign.Store(x, done))); }
            else if (was.Length > 0) Show(PodKey);
        };
        view.PointerWheelChanged += (_, e) =>
        {
            var at = e.GetPosition(view); var b = (at - _podPan) / PodZoom;
            PodZoom = Math.Clamp(PodZoom * (e.Delta.Y > 0 ? 1.15 : 1 / 1.15), 0.05, 8);
            _podPan = at - b * PodZoom; ApplyTransform(); DrawOverlay(PodDesign.Items(d.Data)); e.Handled = true;
        };
        view.KeyDown += (_, e) => { if (PodKey_(d, e.Key, e.KeyModifiers, view)) e.Handled = true; };
        view.AttachedToVisualTree += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(() => view.Focus());

        _podView = view;
        TextBlock? zoomLabel = null;
        if (_podFitted != d.Id)
        {
            EventHandler? first = null;
            first = (_, _) =>
            {
                if (view.Bounds.Width < 10 || view.Bounds.Height < 10) return;
                view.LayoutUpdated -= first; _podFitted = d.Id;
                PodFitTo(0, 0, bw, bh); ApplyTransform(); DrawOverlay(items);
                if (zoomLabel is not null) zoomLabel.Text = $"{PodZoom * 100:0} %";
            };
            view.LayoutUpdated += first;
        }

        var top = new WrapPanel { Margin = new Thickness(0, 0, 0, 6), [DockPanel.DockProperty] = Avalonia.Controls.Dock.Top };
        Button T(string name, string label, Action a, bool accent = false) { var b = new Button { Name = name, Content = label, Padding = new Thickness(10, 4), Margin = new Thickness(0, 0, 4, 4) }; if (accent) b.Classes.Add("accent"); b.Click += (_, _) => a(); return b; }
        top.Children.Add(T("pod-zoom-out", "−", () => PodZoomBy(1 / 1.25)));
        zoomLabel = new TextBlock { Name = "pod-zoom", Text = $"{PodZoom * 100:0} %", Foreground = Text, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 8, 4) };
        top.Children.Add(zoomLabel);
        top.Children.Add(T("pod-zoom-in", "+", () => PodZoomBy(1.25)));
        top.Children.Add(T("pod-fit", "Einpassen", () => { _podFitted = ""; Show(PodKey); }));
        top.Children.Add(T("pod-grid", PodGrid ? "Raster aus" : "Raster", () => { PodGrid = !PodGrid; Show(PodKey); }));
        top.Children.Add(T("pod-rulers", PodRulers ? "Lineale aus" : "Lineale", () => { PodRulers = !PodRulers; Show(PodKey); }));
        top.Children.Add(T("pod-use", "Als Motiv übernehmen", () => ProgramRun(PodKey, () => PodUseAsMotif(d)), true));

        var left = PodLeftPanel(d, items);
        var right = PodRightPanel(d, items);
        var g = new Grid { ColumnDefinitions = new ColumnDefinitions("210,8,*,8,260"), Children = { left, Pos(view, 2), Pos(right, 4) } };
        return new DockPanel { Name = "pod-designer", Margin = new Thickness(4, 0, 10, 4), Children = { top, g } };
    }

    List<PodItem>? _podLive;

    static (double X, double Y) PodRotate(double px, double py, PodItem i)
    {
        var a = i.Rot * Math.PI / 180; var (dx, dy) = (px - i.CenterX, py - i.CenterY);
        return (i.CenterX + dx * Math.Cos(a) - dy * Math.Sin(a), i.CenterY + dx * Math.Sin(a) + dy * Math.Cos(a));
    }

    void PodZoomBy(double f) { PodZoom = Math.Clamp(PodZoom * f, 0.05, 8); Show(PodKey); }

    /// <summary>Tastatur des Editors; öffentlich für Tests. true: Taste verarbeitet.</summary>
    public bool PodKey_(ProgramDocument d, Key key, KeyModifiers mods, Control? view = null)
    {
        var ctrl = mods.HasFlag(KeyModifiers.Control); var shift = mods.HasFlag(KeyModifiers.Shift);
        var items = PodDesign.Items(d.Data); var sel = PodDesign.WithGroups(items, PodSelected);
        void Arr(string how) { if (sel.Count > 0) PodStore(d, "Reihenfolge", l => PodDesign.Arrange(l, sel, how)); }
        void Nudge(double x, double y) { if (sel.Count > 0) PodStore(d, "Verschieben", l => l.Select(i => sel.Contains(i.Id) ? i with { X = i.X + x, Y = i.Y + y } : i)); }
        var step = shift ? 10 : 1;
        switch (key)
        {
            case Key.OemCloseBrackets: Arr(shift ? "front" : "up"); return true;
            case Key.OemOpenBrackets: Arr(shift ? "back" : "down"); return true;
            case Key.Delete or Key.Back: if (sel.Count > 0) { PodStore(d, "Löschen", l => l.Where(i => !sel.Contains(i.Id))); PodSelected.Clear(); } return true;
            case Key.G when ctrl: PodStore(d, shift ? "Gruppe lösen" : "Gruppieren", l => shift ? PodDesign.Ungroup(l, sel) : PodDesign.Group(l, sel)); return true;
            case Key.C when ctrl: _podClip = items.Where(i => sel.Contains(i.Id)).ToList(); return true;
            case Key.X when ctrl: _podClip = items.Where(i => sel.Contains(i.Id)).ToList(); PodStore(d, "Ausschneiden", l => l.Where(i => !sel.Contains(i.Id))); PodSelected.Clear(); return true;
            case Key.V when ctrl:
                if (_podClip.Count == 0) return true;
                var copies = PodDesign.Duplicates(_podClip); PodSelected.Clear(); foreach (var c in copies) PodSelected.Add(c.Id);
                PodStore(d, "Einfügen", l => [.. l, .. copies]); return true;
            case Key.OemQuotes when ctrl: PodGrid = !PodGrid; Show(PodKey); return true;
            case Key.R when shift: PodRulers = !PodRulers; Show(PodKey); return true;
            case Key.OemPlus or Key.Add: PodZoomBy(1.25); return true;
            case Key.OemMinus or Key.Subtract: PodZoomBy(1 / 1.25); return true;
            case Key.D1 or Key.D2 or Key.D3 when shift: PodFit(d, items, sel, key); return true;
            case Key.Left: Nudge(-step, 0); return true;
            case Key.Right: Nudge(step, 0); return true;
            case Key.Up: Nudge(0, -step); return true;
            case Key.Down: Nudge(0, step); return true;
        }
        return false;
    }

    /// <summary>Umschalt+1 Inhalt, 2 Arbeitsfläche, 3 Auswahl einpassen.</summary>
    void PodFit(ProgramDocument d, List<PodItem> items, HashSet<string> sel, Key key)
    {
        var (bw, bh) = PodBoardSize(d);
        var list = key == Key.D1 ? items.Where(i => !i.Hidden).ToList() : key == Key.D3 ? items.Where(i => sel.Contains(i.Id)).ToList() : [];
        var (x, y, w, h) = list.Count == 0 ? (0d, 0d, bw, bh) : (list.Min(i => i.X), list.Min(i => i.Y), list.Max(i => i.Right) - list.Min(i => i.X), list.Max(i => i.Bottom) - list.Min(i => i.Y));
        PodFitTo(x, y, w, h);
        Show(PodKey);
    }

    void PodFitTo(double x, double y, double w, double h)
    {
        var (vw, vh) = _podView is { Bounds: { Width: > 10, Height: > 10 } b } ? (b.Width, b.Height) : (800d, 620d);
        PodZoom = Math.Clamp(Math.Min(vw / Math.Max(1, w), vh / Math.Max(1, h)) * 0.9, 0.05, 8);
        _podPan = new Point((vw - w * PodZoom) / 2 - x * PodZoom, (vh - h * PodZoom) / 2 - y * PodZoom);
    }

    Control PodLeftPanel(ProgramDocument d, List<PodItem> items)
    {
        var sp = new StackPanel { Name = "pod-left", Spacing = 6, Margin = new Thickness(0, 0, 14, 0) };
        Button A(string name, string label, Action a) { var b = new Button { Name = name, Content = label, HorizontalAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(10, 6) }; b.Click += (_, _) => a(); return b; }
        sp.Children.Add(H("Hinzufügen", 13));
        sp.Children.Add(A("pod-add-text", "Text", () => PodAdd(d, "text")));
        sp.Children.Add(A("pod-add-rect", "Rechteck", () => PodAdd(d, "rect")));
        sp.Children.Add(A("pod-add-ellipse", "Kreis", () => PodAdd(d, "ellipse")));
        sp.Children.Add(A("pod-add-image", "Bild …", async () => { if (await PickMediaFile("pod-bild", "Bild (PNG oder JPEG)", ["*.png", "*.jpg", "*.jpeg"]) is { } f) PodAdd(d, "image", f); }));
        sp.Children.Add(H("Ebenen", 13));
        foreach (var i in Enumerable.Reverse(items))
        {
            var label = i.Kind switch { "text" => i.Text.Length > 18 ? i.Text[..18] + "…" : i.Text, "rect" => "Rechteck", "ellipse" => "Kreis", _ => "Bild" };
            var row = new Button { Name = "pod-layer-" + i.Id, Content = (i.Group.Length > 0 ? "• " : "") + label, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(8, 3) };
            if (PodSelected.Contains(i.Id)) row.Classes.Add("accent");
            row.Click += (_, _) => { PodSelected.Clear(); foreach (var x in PodDesign.WithGroups(items, [i.Id])) PodSelected.Add(x); Show(PodKey); };
            var eye = new Button { Name = "pod-hide-" + i.Id, Content = i.Hidden ? "○" : "●", Padding = new Thickness(6, 3) };
            ToolTip.SetTip(eye, i.Hidden ? "Einblenden" : "Ausblenden");
            eye.Click += (_, _) => PodStore(d, "Ebene ausblenden", l => l.Select(x => x.Id == i.Id ? x with { Hidden = !x.Hidden } : x));
            sp.Children.Add(new DockPanel { Children = { Dk(eye, Avalonia.Controls.Dock.Right), row } });
        }
        if (items.Count == 0) sp.Children.Add(P("Noch leer. Füge Text, Formen oder ein Bild hinzu.", Muted, 11));
        return new ScrollViewer { Content = sp };
    }

    Control PodRightPanel(ProgramDocument d, List<PodItem> items)
    {
        var sp = new StackPanel { Name = "pod-right", Spacing = 5, Margin = new Thickness(0, 0, 14, 0) };
        var chosen = items.Where(i => PodSelected.Contains(i.Id)).ToList();
        var palette = d.List("palette").Select(x => x?.ToString() ?? "").Where(PodDesign.IsColor).ToList();
        if (chosen.Count != 1)
        {
            sp.Children.Add(H(chosen.Count > 1 ? $"{chosen.Count} Elemente" : "Nichts ausgewählt", 13));
            if (chosen.Count > 1) sp.Children.Add(P("Strg+G gruppiert. Ziehen bewegt alle gemeinsam.", Muted, 11));
            sp.Children.Add(P("Tasten: ] und [ ordnen, Strg+C, V, X, Plus und Minus zoomen, Strg+' Raster, Umschalt+R Lineale, Alt skaliert von der Mitte, Strg beim Ziehen schaltet das Einrasten aus.", Muted, 11));
        }
        else
        {
            var i = chosen[0];
            void Up(string label, Func<PodItem, PodItem> f) => PodStore(d, label, l => l.Select(x => x.Id == i.Id ? f(x) : x));
            void Num(string key, string label, double value, Func<PodItem, double, PodItem> f)
            {
                var t = new TextBox { Name = "pod-prop-" + key, Text = value.ToString("0.#", De), Width = 90 };
                void Commit() { if (double.TryParse(t.Text, System.Globalization.NumberStyles.Float, De, out var v) && Math.Abs(v - value) > 0.04) Up(label, x => f(x, v)); }
                t.LostFocus += (_, _) => Commit(); OnEnter(t, Commit);
                sp.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,96"), Children = { new TextBlock { Text = label, Foreground = Text, VerticalAlignment = VerticalAlignment.Center, FontSize = 12 }, Pos(t, 1) } });
            }
            sp.Children.Add(H("Lage", 13));
            Num("x", "X", i.X, (x, v) => x with { X = v }); Num("y", "Y", i.Y, (x, v) => x with { Y = v });
            Num("w", "Breite", i.W, (x, v) => x with { W = Math.Max(1, v) }); Num("h", "Höhe", i.H, (x, v) => x with { H = Math.Max(1, v) });
            Num("rot", "Drehung", i.Rot, (x, v) => x with { Rot = v });
            if (i.Kind == "text")
            {
                sp.Children.Add(H("Text", 13));
                var t = new TextBox { Name = "pod-prop-text", Text = i.Text, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 60 };
                t.LostFocus += (_, _) => { if ((t.Text ?? "") != i.Text) Up("Text", x => x with { Text = t.Text ?? "" }); };
                sp.Children.Add(t);
                var font = new ComboBox { Name = "pod-prop-font", ItemsSource = PodFonts, SelectedItem = PodFonts.Contains(i.Font) ? i.Font : PodFonts[0], HorizontalAlignment = HorizontalAlignment.Stretch };
                font.SelectionChanged += (_, _) => { if (font.SelectedItem is string f && f != i.Font) Up("Schrift", x => x with { Font = f }); };
                sp.Children.Add(font);
                Num("size", "Schriftgröße", i.Size, (x, v) => x with { Size = Math.Max(4, v) });
                var bold = new CheckBox { Name = "pod-prop-bold", Content = "Fett", IsChecked = i.Bold };
                bold.IsCheckedChanged += (_, _) => { if (bold.IsChecked != i.Bold) Up("Fett", x => x with { Bold = bold.IsChecked == true }); };
                sp.Children.Add(bold);
            }
            var shadow = new CheckBox { Name = "pod-prop-shadow", Content = "Schatten", IsChecked = i.Shadow };
            shadow.IsCheckedChanged += (_, _) => { if (shadow.IsChecked != i.Shadow) Up("Schatten", x => x with { Shadow = shadow.IsChecked == true }); };
            sp.Children.Add(shadow);
            if (i.Kind != "image")
            {
                sp.Children.Add(H("Farbe", 13));
                var fill = new TextBox { Name = "pod-prop-fill", Text = i.Fill };
                void C() { var v = (fill.Text ?? "").Trim(); if (PodDesign.IsColor(v) && !v.Equals(i.Fill, StringComparison.OrdinalIgnoreCase)) Up("Farbe", x => x with { Fill = v }); }
                fill.LostFocus += (_, _) => C(); OnEnter(fill, C);
                sp.Children.Add(fill);
                var swatches = new WrapPanel { Name = "pod-palette" };
                for (var n = 0; n < palette.Count; n++)
                {
                    var col = palette[n];
                    var b = new Button { Name = "pod-color-" + n, Width = 24, Height = 24, Padding = new Thickness(0), Margin = new Thickness(0, 0, 4, 4), Background = Brush.Parse(col) };
                    b.Click += (_, _) => Up("Farbe", x => x with { Fill = col });
                    swatches.Children.Add(b);
                }
                sp.Children.Add(swatches);
                var keep = new Button { Name = "pod-color-keep", Content = "Farbe in die Palette", Padding = new Thickness(8, 3) };
                keep.Click += (_, _) => ProgramRun(PodKey, () => d.Set("palette", new JsonArray([.. PodDesign.AddColor(palette, i.Fill).Select(c => (JsonNode)c)]), "Palette"));
                sp.Children.Add(keep);
                sp.Children.Add(H("Umriss", 13));
                Num("stroke", "Stärke", i.StrokeW, (x, v) => x with { StrokeW = Math.Max(0, v), Stroke = x.Stroke.Length > 0 ? x.Stroke : "#000000" });
                var outline = new TextBox { Name = "pod-prop-outline", Text = i.Stroke, PlaceholderText = "#000000" };
                void O() { var v = (outline.Text ?? "").Trim(); if (PodDesign.IsColor(v) && v != i.Stroke) Up("Umrissfarbe", x => x with { Stroke = v }); }
                outline.LostFocus += (_, _) => O(); OnEnter(outline, O);
                sp.Children.Add(outline);
            }
        }
        sp.Children.Add(PodKittlPanel(d));
        sp.Children.Add(PodPrintfulPanel(d));
        sp.Children.Add(PodPrintifyPanel(d));
        return new ScrollViewer { Content = sp };
    }

    string PodKittlFile => IoPath.Combine(_core.DataRoot, "print-on-demand", "kittl-verbunden.txt");
    public bool PodKittlOn => File.Exists(PodKittlFile);
    public void PodKittlSet(bool on) { if (on) { Directory.CreateDirectory(IoPath.GetDirectoryName(PodKittlFile)!); File.WriteAllText(PodKittlFile, "ja"); } else if (File.Exists(PodKittlFile)) File.Delete(PodKittlFile); _core.Audit.Append(Principal.LocalOwner.Name, "pod.kittl", on ? "an" : "aus"); }

    string PodKittlDir => IoPath.Combine(_core.DataRoot, "print-on-demand", "kittl-zugang");
    KittlConnect.State _kittlState = KittlConnect.State.Unbekannt;
    bool _kittlChecked;
    /// <summary>Für Tests: Aufruf von Claude nachbilden.</summary>
    public Func<IReadOnlyList<string>, Cli.Result>? KittlCliOverride { get; set; }
    /// <summary>Für Tests: Anmeldefenster nachbilden.</summary>
    public Func<IReadOnlyList<string>, bool>? KittlConsoleOverride { get; set; }
    public KittlConnect.State PodKittlState => _kittlState;

    async Task<Cli.Result?> KittlCli(IReadOnlyList<string> args)
    {
        if (KittlCliOverride is { } fake) return fake(args);
        if (_opt.Isolated || ClaudeExe() is not { } exe) return null;
        Directory.CreateDirectory(PodKittlDir);
        return await Task.Run(() => Cli.Run(exe, args, PodKittlDir, 60000));
    }

    /// <summary>Fragt ab, ob das eigene Kittl-Konto verbunden ist.</summary>
    public async Task<KittlConnect.State> PodKittlCheck()
    {
        _kittlChecked = true;
        var r = await KittlCli(KittlConnect.GetArgs);
        _kittlState = r is null ? KittlConnect.State.Unbekannt : KittlConnect.Parse(r.Out + "\n" + r.Err);
        if (_kittlState == KittlConnect.State.NichtEingerichtet) _kittlState = KittlConnect.State.AnmeldungNoetig;
        Show(PodKey);
        return _kittlState;
    }

    /// <summary>Öffnet ein Konsolenfenster, in dem Claude die Anmeldung im Browser startet. Ohne Terminal bricht die Anmeldung ab.</summary>
    bool KittlConsole(string exe, IReadOnlyList<string> args)
    {
        try
        {
            ProcessStartInfo psi;
            if (OperatingSystem.IsWindows()) psi = new ProcessStartInfo(exe) { UseShellExecute = true };
            else if (Cli.Find("x-terminal-emulator") is { } term) { psi = new ProcessStartInfo(term) { UseShellExecute = false }; psi.ArgumentList.Add("-e"); psi.ArgumentList.Add(exe); }
            else return false;
            psi.WorkingDirectory = PodKittlDir;
            foreach (var a in args) psi.ArgumentList.Add(a);
            using var p = Process.Start(psi);
            if (p is null) return false;
            if (p.WaitForExit(300000)) return true;
            p.Kill(true);
            return false;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { return false; }
    }

    /// <summary>Anmeldung am eigenen Kittl-Konto. Der Hinweis nennt den Grund, wenn es nicht geklappt hat.</summary>
    public async Task<bool> PodKittlLogin()
    {
        var exe = ClaudeExe();
        if (KittlConsoleOverride is null && (_opt.Isolated || exe is null)) { _progMsg[PodKey] = "Dafür muss Claude Code installiert sein. Das richtest du unter „Mit Claude verbinden“ ein."; Show(PodKey); return false; }
        Directory.CreateDirectory(PodKittlDir);
        await KittlCli(KittlConnect.AddArgs); // meldet einen Fehler, wenn der Eintrag schon da ist; das ist in Ordnung
        _progMsg[PodKey] = "Im neuen Fenster öffnet sich die Anmeldung. Bestätige sie im Browser mit deinem Kittl-Konto."; Show(PodKey);
        var opened = KittlConsoleOverride is { } fake ? fake(KittlConnect.LoginArgs) : await Task.Run(() => KittlConsole(exe!, KittlConnect.LoginArgs));
        _core.Audit.Append(Principal.LocalOwner.Name, "pod.kittl", "anmelden");
        var state = await PodKittlCheck();
        _progMsg[PodKey] = state == KittlConnect.State.Verbunden ? "Kittl ist verbunden." : opened ? "Die Anmeldung wurde nicht abgeschlossen. Versuche es noch einmal." : "Das Anmeldefenster ließ sich nicht öffnen.";
        Show(PodKey);
        return state == KittlConnect.State.Verbunden;
    }

    /// <summary>Meldet das Kittl-Konto bei Claude Code ab.</summary>
    public async Task PodKittlLogout()
    {
        await KittlCli(KittlConnect.LogoutArgs);
        _core.Audit.Append(Principal.LocalOwner.Name, "pod.kittl", "abmelden");
        await PodKittlCheck();
    }

    Control PodKittlPanel(ProgramDocument d)
    {
        var sp = new StackPanel { Name = "pod-kittl", Spacing = 4, Margin = new Thickness(0, 12, 0, 0) };
        sp.Children.Add(H("Kittl anbinden", 13));
        sp.Children.Add(P(PodKittlOn ? "Eingeschaltet: Claude darf in diesem Studio deine Kittl-Werkzeuge nutzen. Gestartete Erzeugungen verbrauchen Credits deines Kittl-Kontos." : "Mit einem eigenen Kittl-Konto nutzt Claude hier Kittls offiziellen Zugang: Mockups, Vektorisieren, Hintergrund entfernen. Trendo speichert keine Zugangsdaten.", Muted, 11));
        var toggle = new Button { Name = "pod-kittl-toggle", Content = PodKittlOn ? "Ausschalten" : "Einschalten", Padding = new Thickness(10, 4) };
        toggle.Click += (_, _) => { PodKittlSet(!PodKittlOn); Show(PodKey); };
        var open = new Button { Name = "pod-kittl-open", Content = "kittl.com öffnen", Padding = new Thickness(10, 4), Margin = new Thickness(6, 0, 0, 0) };
        open.Click += (_, _) => Launch("https://www.kittl.com");
        sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Children = { toggle, open } });
        if (PodKittlOn)
        {
            if (!_kittlChecked) _ = PodKittlCheck();
            sp.Children.Add(new TextBlock { Name = "pod-kittl-state", Text = KittlConnect.Text(_kittlState), Foreground = _kittlState == KittlConnect.State.Verbunden ? Brush.Parse("#3DDC84") : Muted, FontSize = 12, Margin = new Thickness(0, 4, 0, 0) });
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            if (_kittlState != KittlConnect.State.Verbunden)
            {
                var login = new Button { Name = "pod-kittl-login", Content = "Bei Kittl anmelden", Padding = new Thickness(10, 4) };
                login.Classes.Add("accent");
                login.Click += async (_, _) => await PodKittlLogin();
                row.Children.Add(login);
            }
            var check = new Button { Name = "pod-kittl-check", Content = "Verbindung prüfen", Padding = new Thickness(10, 4) };
            check.Click += async (_, _) => await PodKittlCheck();
            row.Children.Add(check);
            if (_kittlState == KittlConnect.State.Verbunden)
            {
                var logout = new Button { Name = "pod-kittl-logout", Content = "Abmelden", Padding = new Thickness(10, 4) };
                logout.Click += async (_, _) => await PodKittlLogout();
                row.Children.Add(logout);
            }
            sp.Children.Add(row);
            sp.Children.Add(P("Jede Person meldet sich mit dem eigenen Konto an. Läuft die Anmeldung ab, erscheint der Knopf wieder.", Muted, 11));
        }
        return sp;
    }
}
