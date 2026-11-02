using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Trendo.Core.Sys;

namespace Trendo.App;

/// <summary>Kalender-Zeitachse (Tag oder Woche) wie in Kalenderprogrammen: Stundenraster, Termine als Blöcke an ihrer Zeit (Überschneidungen nebeneinander),
/// Linie für „jetzt“. Termin ziehen verschiebt ihn (15-Minuten-Raster, auch auf andere Tage), Unterkante ziehen ändert die Dauer,
/// in freier Zeit ziehen legt einen neuen Termin an, Klick wählt aus. Ganztägige Termine stehen im Kopf darüber (eigene Leiste).</summary>
public sealed class CalTimeGrid : Control
{
    public DateTime FirstDay { get; set; } = DateTime.Today;
    public int Days { get; set; } = 7;
    public double HourHeight { get; set; } = 48;
    public IReadOnlyList<(CalEvent Event, DateTime Start, DateTime End)> Items { get; set; } = [];
    public string? SelectedUid { get; set; }
    public Action<string?>? EventClicked { get; set; }
    public Action<string, TimeSpan>? EventMoved { get; set; }
    public Action<string, DateTime>? EventResized { get; set; }
    public Action<DateTime, DateTime>? RangeCreated { get; set; }
    public Func<DateTime> Now { get; set; } = () => DateTime.Now;

    const double Gutter = 52;
    static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");
    readonly List<(string Uid, DateTime Start, DateTime End, Rect R)> _boxes = [];
    enum Mode { None, Move, Resize, Create }
    Mode _mode; string? _uid; Point _from; Point _at; bool _dragged; DateTime _origStart, _origEnd;

    public CalTimeGrid() { ClipToBounds = true; Focusable = true; Height = 24 * 48; }

    double ColW => Math.Max(20, (Bounds.Width - Gutter) / Days);
    public DateTime TimeAt(Point p)
    {
        var day = Math.Clamp((int)Math.Floor((p.X - Gutter) / ColW), 0, Days - 1);
        var minutes = Math.Clamp(p.Y / HourHeight * 60, 0, 24 * 60 - 15);
        return Trendo.Core.Sys.Calendar.Snap(FirstDay.Date.AddDays(day).AddMinutes(minutes));
    }
    public Point PointOf(DateTime t) => new(Gutter + (t.Date - FirstDay.Date).Days * ColW + ColW / 2, (t - t.Date).TotalHours * HourHeight);
    /// <summary>Kasten eines Termins (nach der letzten Darstellung).</summary>
    public Rect? BoxOf(string uid) => _boxes.FirstOrDefault(b => b.Uid == uid) is { Uid: not null } b ? b.R : null;

    protected override Size MeasureOverride(Size available) => new(double.IsInfinity(available.Width) ? 600 : available.Width, 24 * HourHeight);

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e); Focus();
        var p = e.GetPosition(this); _from = _at = p; _dragged = false; _mode = Mode.None; _uid = null;
        if (p.X < Gutter) return;
        foreach (var b in Enumerable.Reverse(_boxes))
            if (b.R.Contains(p)) { _uid = b.Uid; _origStart = b.Start; _origEnd = b.End; _mode = p.Y > b.R.Bottom - 7 ? Mode.Resize : Mode.Move; break; }
        if (_mode == Mode.None) _mode = Mode.Create;
        e.Pointer.Capture(this); e.Handled = true;
    }
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_mode == Mode.None) { Cursor = _boxes.Any(b => b.R.Contains(e.GetPosition(this)) && e.GetPosition(this).Y > b.R.Bottom - 7) ? new Cursor(StandardCursorType.SizeNorthSouth) : Cursor.Default; return; }
        _at = e.GetPosition(this);
        if (!_dragged && Math.Abs(_at.X - _from.X) + Math.Abs(_at.Y - _from.Y) < 5) return;
        _dragged = true; InvalidateVisual();
    }
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e); e.Pointer.Capture(null);
        var mode = _mode; _mode = Mode.None;
        if (!_dragged) { if (mode != Mode.None) EventClicked?.Invoke(_uid); InvalidateVisual(); return; }
        switch (mode)
        {
            case Mode.Move when _uid is not null:
                var by = TimeAt(_at) - TimeAt(_from); if (by != TimeSpan.Zero) EventMoved?.Invoke(_uid, by); break;
            case Mode.Resize when _uid is not null:
                EventResized?.Invoke(_uid, Trendo.Core.Sys.Calendar.Snap(_origEnd + (TimeAt(_at) - TimeAt(_from)))); break;
            case Mode.Create:
                var (a, b) = (TimeAt(_from), TimeAt(_at)); if (b < a) (a, b) = (b, a);
                if (a.Date != b.Date) b = a.Date.AddDays(1).AddMinutes(-15);
                RangeCreated?.Invoke(a, b > a ? b : a.AddMinutes(60)); break;
        }
        InvalidateVisual();
    }

    public override void Render(DrawingContext ctx)
    {
        var w = Bounds.Width; var h = Bounds.Height; if (w < Gutter + 20) return;
        var muted = new SolidColorBrush(Color.Parse("#8AA0BD")); var line = new Pen(new SolidColorBrush(Color.Parse("#22FFFFFF")), 1); var half = new Pen(new SolidColorBrush(Color.Parse("#11FFFFFF")), 1);
        ctx.FillRectangle(Brushes.Transparent, new Rect(0, 0, w, h));
        var cw = ColW;
        for (var d = 0; d < Days; d++)
        {
            var day = FirstDay.Date.AddDays(d);
            if (day == Now().Date) ctx.FillRectangle(new SolidColorBrush(Color.Parse("#14C9A227")), new Rect(Gutter + d * cw, 0, cw, h));
            else if (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) ctx.FillRectangle(new SolidColorBrush(Color.Parse("#0AFFFFFF")), new Rect(Gutter + d * cw, 0, cw, h));
            ctx.DrawLine(line, new Point(Gutter + d * cw, 0), new Point(Gutter + d * cw, h));
        }
        for (var hr = 0; hr < 24; hr++)
        {
            var y = hr * HourHeight;
            ctx.DrawLine(line, new Point(Gutter - 6, y), new Point(w, y)); ctx.DrawLine(half, new Point(Gutter, y + HourHeight / 2), new Point(w, y + HourHeight / 2));
            if (hr > 0) ctx.DrawText(new FormattedText($"{hr:00}:00", De, FlowDirection.LeftToRight, new Typeface("Inter"), 11, muted), new Point(6, y - 7));
        }
        // Termine je Tag, Überschneidungen nebeneinander
        _boxes.Clear();
        for (var d = 0; d < Days; d++)
        {
            var day = FirstDay.Date.AddDays(d);
            var list = Items.Where(o => !o.Event.AllDay && o.Start < day.AddDays(1) && o.End > day).OrderBy(o => o.Start).ThenByDescending(o => o.End).ToList();
            var lanes = new List<DateTime>(); var lane = new int[list.Count];
            for (var i = 0; i < list.Count; i++)
            {
                var k = lanes.FindIndex(end => end <= list[i].Start); if (k < 0) { lanes.Add(list[i].End); k = lanes.Count - 1; } else lanes[k] = list[i].End;
                lane[i] = k;
            }
            for (var i = 0; i < list.Count; i++)
            {
                var o = list[i]; var lw = (cw - 6) / Math.Max(1, lanes.Count);
                var s = o.Start < day ? day : o.Start; var en = o.End > day.AddDays(1) ? day.AddDays(1) : o.End;
                var r = new Rect(Gutter + d * cw + 3 + lane[i] * lw, (s - day).TotalHours * HourHeight + 1, lw - 2, Math.Max(18, (en - s).TotalHours * HourHeight - 2));
                _boxes.Add((o.Event.Uid, o.Start, o.End, r));
            }
        }
        var font = new Typeface("Inter"); var bold = new Typeface("Inter", FontStyle.Normal, FontWeight.SemiBold);
        foreach (var (uid, start, end, r0) in _boxes)
        {
            var ev = Items.First(x => x.Event.Uid == uid).Event; var sel = uid == SelectedUid; var r = r0;
            if (_dragged && uid == _uid && _mode is Mode.Move or Mode.Resize)
            {
                var by = TimeAt(_at) - TimeAt(_from);
                if (_mode == Mode.Move) { var p = PointOf(start + by); r = new Rect(p.X - cw / 2 + 3, p.Y + 1, cw - 8, r.Height); }
                else r = r.WithHeight(Math.Max(HourHeight / 4, r.Height + by.TotalHours * HourHeight));
            }
            var fill = new SolidColorBrush(Color.Parse(sel ? "#E6C9A227" : "#B3C9A227"));
            ctx.DrawRectangle(fill, new Pen(new SolidColorBrush(Color.Parse(sel ? "#FFFACC15" : "#FFE8C26A")), sel ? 2 : 1), new RoundedRect(r, 6));
            ctx.FillRectangle(new SolidColorBrush(Color.Parse("#FF93C5FD")), new Rect(r.X, r.Y + 3, 3, Math.Max(4, r.Height - 6)));
            using (ctx.PushClip(r.Deflate(new Thickness(7, 2, 3, 2))))
            {
                ctx.DrawText(new FormattedText(ev.Title + (ev.Repeat != Repeat.Keine ? " ↻" : ""), De, FlowDirection.LeftToRight, bold, 12, Brushes.White), new Point(r.X + 8, r.Y + 2));
                if (r.Height > 30) ctx.DrawText(new FormattedText($"{start:HH:mm}–{end:HH:mm}{(ev.Location.Length > 0 ? " · " + ev.Location : "")}", De, FlowDirection.LeftToRight, font, 11, new SolidColorBrush(Color.Parse("#DDEAF2FF"))), new Point(r.X + 8, r.Y + 17));
            }
        }
        if (_dragged && _mode == Mode.Create)
        {
            var (a, b) = (TimeAt(_from), TimeAt(_at)); if (b < a) (a, b) = (b, a); if (b == a) b = a.AddMinutes(15);
            var pa = PointOf(a); var r = new Rect(pa.X - cw / 2 + 3, pa.Y, cw - 6, Math.Max(HourHeight / 4, (b - a).TotalHours * HourHeight));
            ctx.DrawRectangle(new SolidColorBrush(Color.Parse("#55C9A227")), new Pen(new SolidColorBrush(Color.Parse("#FFE8C26A")), 1.5, DashStyle.Dash), new RoundedRect(r, 6));
            ctx.DrawText(new FormattedText($"{a:HH:mm}–{b:HH:mm}", De, FlowDirection.LeftToRight, font, 11, Brushes.White), new Point(r.X + 6, r.Y + 2));
        }
        // jetzt
        var now = Now();
        if (now.Date >= FirstDay.Date && now.Date < FirstDay.Date.AddDays(Days))
        {
            var p = PointOf(now); var red = new SolidColorBrush(Color.Parse("#EF4444"));
            ctx.DrawLine(new Pen(red, 2), new Point(p.X - cw / 2, p.Y), new Point(p.X + cw / 2, p.Y)); ctx.DrawEllipse(red, null, new Point(p.X - cw / 2, p.Y), 4, 4);
        }
    }
}
