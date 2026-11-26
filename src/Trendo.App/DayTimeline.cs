using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Trendo.App;

/// <summary>T-0719: ein Block der Tages-Zeitleiste – Termin („a:&lt;id&gt;“), Aufgabe oder Pause aus dem Plan („p:&lt;index&gt;“). Zeiten in Minuten ab Mitternacht.</summary>
public sealed class TimelineBlock
{
    public required string Key { get; init; }
    public required string Title { get; set; }
    /// <summary><c>DayPlanner.Work</c>, <c>Fixed</c> oder <c>Pause</c>.</summary>
    public required string Kind { get; init; }
    public int Start { get; set; }
    public int End { get; set; }
    public bool Done { get; init; }
    /// <summary>Aufgabe mit Eintrag in der Aufgabenliste (dann gibt es „Erledigt“).</summary>
    public bool HasTask { get; init; }
    /// <summary>T-0728: von Hand gesetzt oder verschoben – „Tag planen“ lässt den Block, wo er ist.</summary>
    public bool Pinned { get; init; }
}

/// <summary>T-0719 (Audit D4 Tagesplaner): Zeitleiste eines Tages zum Anfassen wie in einem Kalenderprogramm – Block ziehen verschiebt,
/// an der Unterkante ziehen ändert die Dauer (Raster 5 Minuten), Doppelklick ins Leere legt eine Aufgabe an, Doppelklick auf einen Block benennt ihn um,
/// Rechtsklick öffnet ein Menü, Entf löscht, Pfeil hoch/runter verschiebt um 5 Minuten. Erst beim Loslassen wird das Dokument geändert (ein Rückgängig-Schritt).</summary>
public sealed class DayTimeline : Panel
{
    public const int Snap = 5;
    public const double Gutter = 54;
    public double PxPerMin { get; init; } = 1.2;
    readonly List<TimelineBlock> _blocks;
    readonly int _from, _to, _workFrom, _workTo, _now;
    readonly Dictionary<string, Border> _views = [];
    readonly GridLayer _layer;
    public string? Selected { get; private set; }

    /// <summary>Block verschoben oder Dauer geändert: (Schlüssel, Beginn, Ende).</summary>
    public Action<string, int, int>? Changed { get; set; }
    public Action<int>? CreateAt { get; set; }
    public Action<string?>? Select { get; set; }
    public Action<string>? Rename { get; set; }
    public Action<string>? Delete { get; set; }
    public Func<string?, int, ContextMenu>? MenuAt { get; set; }
    public Action<string, bool>? Toggle { get; set; }
    /// <summary>Name direkt im Block geändert (Enter oder Fokus weg).</summary>
    public Action<string, string>? Renamed { get; set; }
    public Action? CancelEdit { get; set; }
    readonly string? _editing;

    (string Key, bool Resize, Point From, int S0, int E0)? _drag; bool _moved;

    /// <param name="workFrom">Arbeitsbeginn (Minuten) – heller hinterlegt.</param>
    /// <param name="now">Jetzt in Minuten, wenn der Tag heute ist (sonst -1) – goldene Linie.</param>
    public DayTimeline(IEnumerable<TimelineBlock> blocks, int workFrom, int workTo, int now, string? selected, string? editing = null, string namePrefix = "dp")
    {
        _editing = editing;
        _blocks = blocks.OrderBy(b => b.Start).ThenBy(b => b.Kind == Trendo.Core.Kreativ.DayPlanner.Fixed ? 0 : 1).ToList();
        _workFrom = workFrom; _workTo = workTo; _now = now; Selected = selected;
        var first = _blocks.Select(b => b.Start).DefaultIfEmpty(workFrom).Min(); var last = _blocks.Select(b => b.End).DefaultIfEmpty(workTo).Max();
        _from = Math.Clamp(Math.Min(workFrom, first) / 60 * 60, 0, 23 * 60);
        _to = Math.Clamp((Math.Max(workTo, last) + 59) / 60 * 60, _from + 60, 24 * 60);
        Background = Brushes.Transparent; Focusable = true; ClipToBounds = true; Name = namePrefix + "-timeline";
        // Fokus beim Klicken darf die Arbeitsfläche nicht verschieben (sonst springt der Block beim Ziehen um den Bildlauf)
        AddHandler(RequestBringIntoViewEvent, (_, e) => { if (ReferenceEquals(e.TargetObject, this)) e.Handled = true; });
        _layer = new GridLayer(this) { IsHitTestVisible = false }; Children.Add(_layer);
        var i = 0;
        foreach (var b in _blocks) { var v = View(b, i++, namePrefix); _views[b.Key] = v; Children.Add(v); }
    }

    public IReadOnlyList<TimelineBlock> Blocks => _blocks;
    public double Y(int minute) => 8 + (minute - _from) * PxPerMin;
    public int Minute(double y) => (int)Math.Round((y - 8) / PxPerMin) + _from;
    static int Round(int m) => (int)Math.Round(m / (double)Snap) * Snap;
    public static string Hm(int m) => m >= 24 * 60 ? "24:00" : $"{m / 60:00}:{m % 60:00}";
    /// <summary>Lage eines Blocks auf der Leiste (für Tests und Menüs).</summary>
    public Rect BlockRect(string key) => _views[key].Bounds;

    static readonly IBrush WorkFill = new SolidColorBrush(Color.Parse("#C9A227"), 0.26), WorkEdge = Brush.Parse("#D4A84B");
    static readonly IBrush FixedFill = new SolidColorBrush(Color.Parse("#E5E7EB"), 0.13), FixedEdge = Brush.Parse("#9CA3AF");
    static readonly IBrush PauseFill = new SolidColorBrush(Color.Parse("#6B7280"), 0.14), PauseEdge = Brush.Parse("#6B7280");
    static readonly IBrush Ink = Brush.Parse("#F3F4F6"), Soft = Brush.Parse("#B8BEC8");

    Border View(TimelineBlock b, int i, string p)
    {
        var (fill, edge) = b.Kind switch { Trendo.Core.Kreativ.DayPlanner.Fixed => (FixedFill, FixedEdge), Trendo.Core.Kreativ.DayPlanner.Pause => (PauseFill, PauseEdge), _ => (WorkFill, WorkEdge) };
        var title = new TextBlock { Name = $"{p}-blk-title-{i}", Text = b.Kind == Trendo.Core.Kreativ.DayPlanner.Pause ? "Pause" : b.Kind == Trendo.Core.Kreativ.DayPlanner.Fixed ? "Termin: " + b.Title : b.Title,
            Foreground = b.Done ? Soft : Ink, FontSize = 12.5, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis, TextDecorations = b.Done ? TextDecorations.Strikethrough : null };
        var time = new TextBlock { Name = $"{p}-blk-time-{i}", Text = $"{Hm(b.Start)}–{Hm(b.End)}" + (b.Pinned ? " · festgehalten" : ""), Foreground = Soft, FontSize = 11 };
        Control head = title;
        if (b.Key == _editing)
        {
            var box = new TextBox { Name = $"{p}-blk-edit", Text = b.Title, FontSize = 12.5, Padding = new Thickness(4, 1), MinHeight = 0 };
            var done = false;
            void Commit(bool keep) { if (done) return; done = true; if (keep) Renamed?.Invoke(b.Key, box.Text ?? ""); else CancelEdit?.Invoke(); }
            box.KeyDown += (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; Commit(true); } else if (e.Key == Key.Escape) { e.Handled = true; Commit(false); } };
            box.LostFocus += (_, _) => Commit(true);
            box.AttachedToVisualTree += (_, _) => Trendo.App.UiThread.Post(() => { box.Focus(); box.SelectAll(); });
            head = box;
        }
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { new StackPanel { Spacing = 0, Children = { head, time } } } };
        if (b.HasTask)
        {
            var cb = new CheckBox { Name = $"{p}-block-done-{i}", IsChecked = b.Done, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(4, -4, 0, 0) };
            ToolTip.SetTip(cb, "Erledigt");
            cb.IsCheckedChanged += (_, _) => { if ((cb.IsChecked == true) != b.Done) Toggle?.Invoke(b.Key, cb.IsChecked == true); };
            Grid.SetColumn(cb, 1); grid.Children.Add(cb);
        }
        var sel = b.Key == Selected;
        var v = new Border { Name = $"{p}-blk-{i}", Tag = b.Key, Background = fill, BorderBrush = sel ? Brushes.White : edge, BorderThickness = new Thickness(sel ? 2 : 0, sel ? 2 : 0, sel ? 2 : 0, sel ? 2 : 0),
            CornerRadius = new CornerRadius(6), Padding = new Thickness(8, 3, 6, 3), ClipToBounds = true, Cursor = new Cursor(StandardCursorType.SizeAll) };
        // linker Farbstreifen wie in Kalenderprogrammen
        v.Child = new DockPanel { Children = { new Border { Width = 3, Background = edge, CornerRadius = new CornerRadius(2), Margin = new Thickness(-6, -1, 6, -1), [DockPanel.DockProperty] = Dock.Left }, grid } };
        ToolTip.SetTip(v, "Ziehen verschiebt · Unterkante ziehen ändert die Dauer · Doppelklick benennt um · Rechtsklick für mehr");
        return v;
    }

    // ---------- Anordnung (überlappende Blöcke nebeneinander) ----------
    protected override Size MeasureOverride(Size available)
    {
        foreach (var c in Children) c.Measure(new Size(Math.Max(0, (double.IsInfinity(available.Width) ? 600 : available.Width) - Gutter - 8), double.PositiveInfinity));
        return new Size(double.IsInfinity(available.Width) ? 600 : available.Width, Y(_to) + 8);
    }

    protected override Size ArrangeOverride(Size final)
    {
        _layer.Arrange(new Rect(final));
        var lanes = Lanes();
        var w = Math.Max(40, final.Width - Gutter - 10);
        foreach (var b in _blocks)
        {
            var (lane, count) = lanes[b.Key];
            var lw = w / count; var y = Y(b.Start); var h = Math.Max(16, (b.End - b.Start) * PxPerMin - 2);
            _views[b.Key].Arrange(new Rect(Gutter + 4 + lane * lw, y + 1, lw - 3, h));
        }
        return final;
    }

    Dictionary<string, (int Lane, int Count)> Lanes()
    {
        var res = new Dictionary<string, (int, int)>(); var group = new List<(TimelineBlock B, int Lane)>(); var ends = new List<int>(); var groupEnd = -1;
        void Flush() { var n = Math.Max(1, ends.Count); foreach (var (b, l) in group) res[b.Key] = (l, n); group.Clear(); ends.Clear(); }
        foreach (var b in _blocks.OrderBy(x => x.Start).ThenBy(x => x.End))
        {
            if (b.Start >= groupEnd) Flush();
            var lane = ends.FindIndex(e => e <= b.Start); if (lane < 0) { lane = ends.Count; ends.Add(b.End); } else ends[lane] = b.End;
            group.Add((b, lane)); groupEnd = Math.Max(groupEnd, b.End);
        }
        Flush();
        return res;
    }

    // ---------- Zeichnen: Stunden, Arbeitszeit, Jetzt ----------
    static readonly Typeface Face = new("Inter");
    /// <summary>Hintergrund der Leiste (Panel.Render ist versiegelt): Stundenlinien, Arbeitszeit, Jetzt-Linie.</summary>
    sealed class GridLayer(DayTimeline t) : Control
    {
        public override void Render(DrawingContext ctx) => t.DrawGrid(ctx, Bounds.Width);
    }

    void DrawGrid(DrawingContext ctx, double w)
    {
        ctx.FillRectangle(new SolidColorBrush(Color.Parse("#C9A227"), 0.05), new Rect(Gutter, Y(_workFrom), w - Gutter, Math.Max(0, Y(_workTo) - Y(_workFrom))));
        var line = new Pen(new SolidColorBrush(Color.Parse("#FFFFFF"), 0.10), 1); var half = new Pen(new SolidColorBrush(Color.Parse("#FFFFFF"), 0.05), 1);
        for (var m = _from; m <= _to; m += 30)
        {
            var y = Math.Round(Y(m)) + 0.5;
            ctx.DrawLine(m % 60 == 0 ? line : half, new Point(Gutter, y), new Point(w, y));
            if (m % 60 == 0 && m < 24 * 60) ctx.DrawText(new FormattedText(Hm(m), CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Face, 11, Soft), new Point(8, y - 7));
        }
        if (_now >= _from && _now <= _to)
        {
            var y = Y(_now); var gold = Brush.Parse("#F5C542");
            ctx.DrawLine(new Pen(gold, 1.5), new Point(Gutter - 4, y), new Point(w, y)); ctx.DrawEllipse(gold, null, new Point(Gutter - 4, y), 4, 4);
        }
    }

    // ---------- Eingabe ----------
    string? KeyAt(Point p)
    {
        foreach (var b in _blocks.AsEnumerable().Reverse()) if (_views[b.Key].Bounds.Contains(p)) return b.Key;
        return null;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e); Focus();
        var pt = e.GetCurrentPoint(this); var p = pt.Position; var key = KeyAt(p); var min = Math.Clamp(Round(Minute(p.Y)), 0, 24 * 60 - Snap);
        if (pt.Properties.IsRightButtonPressed)
        {
            if (key is not null && key != Selected) Pick(key);
            if (MenuAt is { } menu) ContextMenu = menu(key, min);
            return;
        }
        if (!pt.Properties.IsLeftButtonPressed) return;
        e.Handled = true;
        if (e.ClickCount == 2) { _drag = null; if (key is null) CreateAt?.Invoke((int)Math.Floor(Minute(p.Y) / 15.0) * 15); else Rename?.Invoke(key); return; }
        if (key is null) { if (Selected is not null) Pick(null); return; }
        var r = _views[key].Bounds; var b = _blocks.First(x => x.Key == key);
        _drag = (key, p.Y >= r.Bottom - 7, p, b.Start, b.End); _moved = false;
        e.Pointer.Capture(this);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e); var p = e.GetPosition(this);
        if (_drag is not { } d)
        {
            var key = KeyAt(p); Cursor = key is not null && p.Y >= _views[key].Bounds.Bottom - 7 ? new Cursor(StandardCursorType.SizeNorthSouth) : null;
            return;
        }
        if (!_moved && Math.Abs(p.Y - d.From.Y) < 3) return;
        _moved = true;
        var delta = Round((int)Math.Round((p.Y - d.From.Y) / PxPerMin));
        var b = _blocks.First(x => x.Key == d.Key);
        if (d.Resize) { b.Start = d.S0; b.End = Math.Clamp(d.E0 + delta, d.S0 + Snap, 24 * 60); }
        else { var len = d.E0 - d.S0; b.Start = Math.Clamp(d.S0 + delta, 0, 24 * 60 - len); b.End = b.Start + len; }
        if (_views[d.Key].Child is DockPanel { Children: [_, Grid { Children: [StackPanel { Children: [_, TextBlock t] }, ..] }] }) t.Text = $"{Hm(b.Start)}–{Hm(b.End)}";
        InvalidateArrange();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_drag is not { } d) return;
        _drag = null; e.Pointer.Capture(null);
        var b = _blocks.First(x => x.Key == d.Key);
        if (_moved && (b.Start != d.S0 || b.End != d.E0)) { Selected = d.Key; Changed?.Invoke(d.Key, b.Start, b.End); }
        else if (!_moved) Pick(d.Key);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        // T-0730: Während im Block umbenannt wird (Textfeld dp-blk-edit), gehören Pfeile, Entf, F2 und Enter dem Textfeld –
        // sie dürfen den Block weder verschieben noch löschen noch eine neue Umbenennung starten.
        if (e.Handled || e.Source is TextBox) return; // nur Tasten aus dem Textfeld des Blocks (nicht „wird bearbeitet“ allein: nach Enter bleibt die Zeitleiste ohne Neuaufbau stehen)
        if (Selected is not { } key || _blocks.FirstOrDefault(x => x.Key == key) is not { } b) return;
        switch (e.Key)
        {
            case Key.Delete: Delete?.Invoke(key); e.Handled = true; break;
            case Key.F2: case Key.Enter: Rename?.Invoke(key); e.Handled = true; break;
            case Key.Up when b.Start >= Snap: Changed?.Invoke(key, b.Start - Snap, b.End - Snap); e.Handled = true; break;
            case Key.Down when b.End <= 24 * 60 - Snap: Changed?.Invoke(key, b.Start + Snap, b.End + Snap); e.Handled = true; break;
        }
    }

    void Pick(string? key)
    {
        Selected = key;
        foreach (var (k, v) in _views) { var on = k == key; v.BorderBrush = on ? Brushes.White : null; v.BorderThickness = new Thickness(on ? 2 : 0); }
        Select?.Invoke(key);
    }
}
