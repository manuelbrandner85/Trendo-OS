using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Trendo.Core.Shell;

namespace Trendo.App;

/// <summary>Fenster auf dem Trendo-Schreibtisch: jedes Studio in einem eigenen Fenster mit eigenem Taskleisten-Eintrag.
/// Der Startbildschirm bleibt immer dahinter. Reiter desselben Studios teilen sich ein Fenster. Die Windows-Taskleiste und die Windows-Shell bleiben unangetastet.</summary>
public partial class MainWindow
{
    public WindowManager Windows { get; } = new();
    private readonly Panel _deskHost = new() { Name = "desk-host" };
    private readonly Canvas _winCanvas = new() { Name = "window-area" };
    private readonly StackPanel _taskbar = new() { Name = "taskbar-windows", Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 0) };
    private readonly Dictionary<string, (Border Frame, ContentControl Body, TextBlock Title)> _frames = [];
    private readonly Dictionary<string, string> _windowGroups = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _windowStudio = new(StringComparer.OrdinalIgnoreCase);
    private bool _windowsReady;

    /// <summary>Wurzel der gerade sichtbaren Seite (aktives Fenster oder Startbildschirm).</summary>
    public Control? ActivePageRoot => Windows.Active is { } a && _frames.TryGetValue(a.Id, out var f) ? f.Body.Content as Control : _deskHost.Children.FirstOrDefault();

    public string WindowGroupOf(string key) => _windowGroups.TryGetValue(key, out var g) ? g : key;

    private void EnsureWindowLayer()
    {
        if (_windowsReady) return; _windowsReady = true;
        _center.Children.Clear();
        _deskHost.ZIndex = 0;
        _center.Children.Add(_deskHost);
        // Fenster liegen über der ganzen Arbeitsfläche (Leiste und Status bleiben sichtbar, solange kein Fenster darüber liegt).
        // Die leere Fläche fängt keine Klicks ab, der Startbildschirm darunter bleibt erreichbar.
        _winCanvas.ZIndex = 2; _winCanvas.Background = null;
        if (_center.Parent is Grid body)
        {
            Grid.SetColumnSpan(_winCanvas, 1);
            body.Children.Add(_winCanvas);
        }
        else _center.Children.Add(_winCanvas);
        _winCanvas.SizeChanged += (_, e) => { Windows.Resize(e.NewSize.Width, e.NewSize.Height); LayoutWindows(); };
    }

    /// <summary>Wird von Show aufgerufen: Startbildschirm zeigen (alle Fenster minimiert) oder Seite im passenden Fenster.</summary>
    private void PresentPage(string key)
    {
        EnsureWindowLayer();
        if (key == "start")
        {
            Windows.MinimizeAll();
            _deskHost.Children.Clear(); _deskHost.Children.Add(SafeView(key));
            RenderWindows(); return;
        }
        if (_deskHost.Children.Count == 0) _deskHost.Children.Add(SafeView("start"));
        var content = SafeView(key);
        var (win, closed) = Windows.Open(PresentGroup(key), key, PresentTitle(key));
        if (closed is not null && _frames.Remove(closed.Id, out var old)) { _winCanvas.Children.Remove(old.Frame); _windowStudio.Remove(closed.Id); }
        if (StudioKeyFor(key) is { } sid) _windowStudio[win.Id] = sid;
        if (!_frames.TryGetValue(win.Id, out var fr)) { fr = MakeFrame(win); _frames[win.Id] = fr; _winCanvas.Children.Add(fr.Frame); }
        fr.Title.Text = win.Title; fr.Body.Content = content;
        RenderWindows();
    }

    /// <summary>Programm-Fragen liegen im Fenster des Studios, das sie ausgelöst hat – nicht in einem gemeinsamen Fenster, das andere Studios verdrängt.</summary>
    private string PresentGroup(string key)
    {
        if (key == "studio-deps" && _depStudio.Length > 0 && HasStudioWorkspace(_depStudio))
            return WindowGroupOf(PageForStudio(_depStudio)!);
        return WindowGroupOf(key);
    }

    private string PresentTitle(string key)
    {
        if (key == "studio-deps" && _depStudio.Length > 0)
            return StudioCatalog.NameOf(_depStudio, string.IsNullOrWhiteSpace(_depStudioTitle) ? _depStudio : _depStudioTitle);
        return PageTitle(key);
    }

    private string? StudioKeyFor(string key)
    {
        if (key == "studio-deps" && _depStudio.Length > 0) return _depStudio;
        if (key.StartsWith("studio-status-", StringComparison.OrdinalIgnoreCase)) return key["studio-status-".Length..];
        return IsStudioWorkspace(key) ? ModuleOf(key) : null;
    }

    private (Border, ContentControl, TextBlock) MakeFrame(DeskWindow w)
    {
        var id = w.Id;
        var titleIcon = new ContentControl { Name = "win-icon-" + id, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Content = IconFor(_windowStudio.TryGetValue(id, out var sid) ? sid : ModuleOf(w.Page), 16) };
        var title = new TextBlock { Name = "win-titletext-" + id, Foreground = Text, FontSize = 12.5, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0), TextTrimming = TextTrimming.CharacterEllipsis };
        Button Cap(string name, string glyph, string tip, Action a)
        {
            var b = new Button { Name = name + "-" + id, Content = glyph, Width = 42, Height = 28, Padding = new Thickness(0), Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = Text,
                HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center, FontSize = 13, CornerRadius = new CornerRadius(name == "win-close" ? 6 : 4), Margin = new Thickness(0, 0, name == "win-close" ? 6 : 0, 0) };
            ToolTip.SetTip(b, tip); Avalonia.Automation.AutomationProperties.SetName(b, tip); b.Click += (_, _) => a(); return b;
        }
        var caps = new StackPanel { Orientation = Orientation.Horizontal, Children = {
            Cap("win-min", "—", "Minimieren", () => { Windows.Minimize(id); AfterWindowChange(); }),
            Cap("win-max", "☐", "Maximieren / Wiederherstellen", () => { Windows.ToggleMaximize(id); AfterWindowChange(); }),
            Cap("win-close", "✕", "Schließen", () => CloseWindow(id)) } };
        var bar = new Border { Name = "win-title-" + id, Height = WindowManager.TitleBar, Background = Brush.Parse("#331C3058"), CornerRadius = new CornerRadius(0), Cursor = new Cursor(StandardCursorType.SizeAll),
            Child = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Children = { titleIcon, Pos(title, 1), Pos(caps, 2) } } };
        // Verschieben mit der Maus an der Titelleiste; Doppelklick maximiert
        Point? from = null;
        bar.PointerPressed += (_, e) =>
        {
            if (e.Source is Visual v && v.FindAncestorOfType<Button>(true) is not null) return;
            if (e.ClickCount == 2) { Windows.ToggleMaximize(id); AfterWindowChange(); return; }
            from = e.GetPosition(_winCanvas); e.Pointer.Capture(bar); FocusWindow(id);
        };
        var dragged = false;
        bar.PointerMoved += (_, e) =>
        {
            if (from is not { } f) return; var p = e.GetPosition(_winCanvas);
            if (Windows.Get(id) is { Maximized: true } mx) { mx.Maximized = false; mx.X = p.X - mx.W / 2; mx.Y = 0; } // aus maximiert herausziehen
            Windows.Move(id, p.X - f.X, p.Y - f.Y); from = p; dragged = true; LayoutWindows();
        };
        bar.PointerReleased += (_, e) =>
        {
            if (from is not null && dragged) { Windows.SnapAt(id, e.GetPosition(_winCanvas).X); LayoutWindows(); }
            from = null; dragged = false; e.Pointer.Capture(null);
        };
        var body = new ContentControl { Name = "win-body-" + id, Margin = new Thickness(6, 4, 6, 6) };
        var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,*") }; grid.Children.Add(bar); Grid.SetRow(body, 1); grid.Children.Add(body);
        var layers = new Panel { Children = { grid } };
        foreach (var h in ResizeHandles(id)) layers.Children.Add(h);
        var frame = new Border { Name = "win-" + id, Background = Brush.Parse(C("#F012182C", "#F7F4F7FC")), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Child = layers, ClipToBounds = true, BoxShadow = BoxShadows.Parse("0 16 40 0 #99000000, 0 2 8 0 #55000000") };
        // Klick ins Fenster holt es nach vorn (nach dem Klick, damit Knöpfe ihren Klick behalten)
        frame.AddHandler(PointerPressedEvent, (_, _) => { if (Windows.Active?.Id != id) Dispatcher.UIThread.Post(() => FocusWindow(id), DispatcherPriority.Background); }, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        return (frame, body, title);
    }

    /// <summary>Unsichtbare Griffe an den vier Rändern und Ecken (6 px breit) zum Größe-Ändern wie bei Windows.</summary>
    private IEnumerable<Control> ResizeHandles(string id)
    {
        const double T = 6, C = 12;
        (string Edge, HorizontalAlignment H, VerticalAlignment V, double W, double Ht, StandardCursorType Cur)[] spec =
        [
            ("L", HorizontalAlignment.Left, VerticalAlignment.Stretch, T, double.NaN, StandardCursorType.LeftSide),
            ("R", HorizontalAlignment.Right, VerticalAlignment.Stretch, T, double.NaN, StandardCursorType.RightSide),
            ("T", HorizontalAlignment.Stretch, VerticalAlignment.Top, double.NaN, T, StandardCursorType.TopSide),
            ("B", HorizontalAlignment.Stretch, VerticalAlignment.Bottom, double.NaN, T, StandardCursorType.BottomSide),
            ("TL", HorizontalAlignment.Left, VerticalAlignment.Top, C, C, StandardCursorType.TopLeftCorner),
            ("TR", HorizontalAlignment.Right, VerticalAlignment.Top, C, C, StandardCursorType.TopRightCorner),
            ("BL", HorizontalAlignment.Left, VerticalAlignment.Bottom, C, C, StandardCursorType.BottomLeftCorner),
            ("BR", HorizontalAlignment.Right, VerticalAlignment.Bottom, C, C, StandardCursorType.BottomRightCorner),
        ];
        foreach (var s in spec)
        {
            var h = new Border { Name = $"win-size-{s.Edge}-{id}", Background = Brushes.Transparent, HorizontalAlignment = s.H, VerticalAlignment = s.V, Width = s.W, Height = s.Ht, Cursor = new Cursor(s.Cur) };
            Point? from = null; var edge = s.Edge;
            h.PointerPressed += (_, e) => { from = e.GetPosition(_winCanvas); e.Pointer.Capture(h); e.Handled = true; FocusWindow(id); };
            h.PointerMoved += (_, e) => { if (from is not { } f) return; var p = e.GetPosition(_winCanvas); Windows.ResizeEdge(id, edge, p.X - f.X, p.Y - f.Y); from = p; LayoutWindows(); };
            h.PointerReleased += (_, e) => { from = null; e.Pointer.Capture(null); };
            yield return h;
        }
    }

    /// <summary>Nach dem Neuaufbau (Einstellungen übernehmen): dieselben Fenster wieder öffnen, mit Lage, Größe, Zustand und Reihenfolge. Titel kommen neu aus der Namenskarte.</summary>
    public void RestoreWindows((List<WindowManager.Saved> Windows, List<string> Stack) s, string? activePage)
    {
        foreach (var x in s.Windows) if (_showMap.ContainsKey(x.Page) && Windows.Get(x.Id) is null) PresentPage(x.Page);
        Windows.Apply(s);
        if (activePage is not null && Windows.Get(WindowGroupOf(activePage)) is { } a && !a.Minimized) Windows.Activate(a.Id);
        AfterWindowChange();
    }

    public void FocusWindow(string id) { if (Windows.Get(id) is null || Windows.Active?.Id == id) return; Windows.Activate(id); AfterWindowChange(); }
    public void CloseWindow(string id)
    {
        Windows.Close(id);
        _windowStudio.Remove(id);
        if (_frames.Remove(id, out var f)) _winCanvas.Children.Remove(f.Frame);
        AfterWindowChange();
    }
    public void TaskbarClick(string id) { Windows.TaskbarClick(id); AfterWindowChange(); }

    /// <summary>Nächstes oder vorheriges Fenster (Ctrl+Tab / Ctrl+Umschalt+Tab). Minimierte Fenster werden dabei geöffnet.</summary>
    public void CycleWindows(int direction)
    {
        var ids = Windows.TaskOrder.Select(w => w.Id).ToList();
        if (ids.Count == 0 || direction == 0) return;
        var i = Windows.Active is { } a ? ids.IndexOf(a.Id) : -1;
        var n = i < 0 ? 0 : (i + direction) % ids.Count;
        if (n < 0) n += ids.Count;
        Windows.Activate(ids[n]);
        AfterWindowChange();
    }

    /// <summary>Nach Fensterwechsel: aktuelle Seite, Markierungen und Taskleiste nachziehen.</summary>
    private void AfterWindowChange()
    {
        var a = Windows.Active;
        CurrentView = a?.Page ?? "start"; MarkTab(CurrentView); MarkSide(CurrentView);
        RenderWindows();
    }

    private void RenderWindows()
    {
        var active = Windows.Active;
        // Aktives Fenster zuerst in der Liste (wird zuerst gefunden), Zeichenreihenfolge über ZIndex
        var order = Windows.Stack.Reverse().Select(w => _frames[w.Id].Frame).ToList();
        if (!_winCanvas.Children.SequenceEqual(order)) { _winCanvas.Children.Clear(); foreach (var f in order) _winCanvas.Children.Add(f); }
        var z = 0;
        foreach (var w in Windows.Stack)
        {
            var f = _frames[w.Id].Frame; f.ZIndex = ++z; f.IsVisible = !w.Minimized;
            var on = active?.Id == w.Id;
            f.BorderBrush = on ? Accent : Edge; f.Opacity = on ? 1 : 0.9;
            f.BoxShadow = on ? BoxShadows.Parse("0 22 50 0 #BB000000, 0 0 0 1 #6651C5FF") : BoxShadows.Parse("0 8 18 0 #66000000");
            _frames[w.Id].Title.Foreground = on ? Text : Muted;
            if (f.Child is Panel { Children: [Grid g, ..] } && g.Children[0] is Border bar) bar.Background = on ? Brush.Parse("#55305078") : Brush.Parse("#22101830");
            if (f.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.Name == "win-close-" + w.Id) is { } close)
            { close.Background = on ? Brush.Parse("#E23B4A") : Brushes.Transparent; close.Foreground = on ? Brushes.White : Text; }
        }
        _deskHost.IsHitTestVisible = true;
        LayoutWindows(); RenderTaskbar();
        Rescale?.Invoke();
    }

    private void LayoutWindows()
    {
        foreach (var w in Windows.Stack)
        {
            if (!_frames.TryGetValue(w.Id, out var fr)) continue;
            var (x, y, ww, hh) = Windows.Rect(w);
            Canvas.SetLeft(fr.Frame, x); Canvas.SetTop(fr.Frame, y); fr.Frame.Width = ww; fr.Frame.Height = hh;
            fr.Frame.CornerRadius = new CornerRadius(w.Maximized ? 4 : 12);
        }
    }

    private void RenderTaskbar()
    {
        _taskbar.Children.Clear();
        var active = Windows.Active?.Id;
        if (Windows.Notice is { } note)
        {
            var hint = new TextBlock { Name = "windows-notice", Text = note, Foreground = Brush.Parse("#FACC15"), FontSize = 11, VerticalAlignment = VerticalAlignment.Center, MaxWidth = 220, TextWrapping = TextWrapping.Wrap };
            ToolTip.SetTip(hint, note); Avalonia.Automation.AutomationProperties.SetName(hint, note);
            _taskbar.Children.Add(hint);
        }
        foreach (var w in Windows.TaskOrder)
        {
            var on = w.Id == active;
            var t = w.Title.Length > 22 ? w.Title[..21] + "…" : w.Title;
            var iconId = _windowStudio.TryGetValue(w.Id, out var sid) ? sid : ModuleOf(w.Page);
            var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            if (IconFor(iconId, 15) is { } ic) content.Children.Add(ic);
            content.Children.Add(new TextBlock { Text = t, VerticalAlignment = VerticalAlignment.Center });
            var b = new Button { Name = "task-" + w.Id, Content = content, FontSize = 11.5, Padding = new Thickness(12, 4), CornerRadius = new CornerRadius(8),
                Background = on ? Accent : Brush.Parse(w.Minimized ? "#330A1630" : "#660A1630"), Foreground = on ? Brushes.White : Text, BorderBrush = on ? Brushes.White : Edge, BorderThickness = new Thickness(on ? 1 : 1) };
            ToolTip.SetTip(b, w.Title + (w.Minimized ? " (minimiert)" : on ? " (aktiv)" : "")); Avalonia.Automation.AutomationProperties.SetName(b, w.Title);
            var id = w.Id; b.Click += (_, _) => TaskbarClick(id);
            _taskbar.Children.Add(b);
        }
        foreach (var pin in _settings.PinnedStudios)
        {
            if (Windows.TaskOrder.Any(w => string.Equals(_windowStudio.GetValueOrDefault(w.Id), pin, StringComparison.OrdinalIgnoreCase))) continue;
            var name = StudioCatalog.NameOf(pin, _studios.FirstOrDefault(s => string.Equals(s.Id, pin, StringComparison.OrdinalIgnoreCase))?.Name ?? pin);
            var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            if (IconFor(pin, 15) is { } ic) content.Children.Add(ic);
            var shown = name.Length > 22 ? name[..21] + "…" : name;
            content.Children.Add(new TextBlock { Text = shown, VerticalAlignment = VerticalAlignment.Center, Opacity = 0.85 });
            var b = new Button { Name = "pinbar-" + pin, Content = content, FontSize = 11.5, Padding = new Thickness(12, 4), CornerRadius = new CornerRadius(8),
                Background = Brush.Parse("#220A1630"), Foreground = Text, BorderBrush = Edge, BorderThickness = new Thickness(1) };
            ToolTip.SetTip(b, name + " (angeheftet)"); Avalonia.Automation.AutomationProperties.SetName(b, name);
            var id = pin; b.Click += async (_, _) => await ActivateOrOpenStudio(id, name);
            _taskbar.Children.Add(b);
        }
        _taskbar.IsVisible = _taskbar.Children.Count > 0;
    }
}
