using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Zeitplan (Gantt): Balken ziehen = verschieben, Kante ziehen = Beginn/Ende ändern, Punkt am Balkenende auf einen anderen Balken ziehen = Abhängigkeit.
/// Wochenenden schattiert, Heute-Linie, Pfeile für Abhängigkeiten, kritischer Pfad rot umrandet, Meilensteine als Raute.</summary>
public sealed class GanttCanvas : Control
{
    public IReadOnlyList<PlanBar> Bars { get; set; } = [];
    public DateTime From { get; set; } = DateTime.Today;
    public int DaysShown { get; set; } = 30;
    public double DayW { get; set; } = 28;
    public const double RowH = 34, Head = 40;
    public int Selected { get; set; } = -1;
    public Action<int, int>? Moved { get; set; }
    public Action<int, int, bool>? Resized { get; set; }
    public Action<int, int>? Linked { get; set; }
    public Action<int>? Clicked { get; set; }
    static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");
    enum Mode { None, Move, Start, End, Link }
    Mode _mode; int _bar = -1; Point _from, _at;

    public GanttCanvas() { ClipToBounds = true; }
    protected override Size MeasureOverride(Size a) => new(DaysShown * DayW, Head + Math.Max(1, Bars.Count) * RowH + 8);
    public double X(DateTime d) => (d.Date - From.Date).TotalDays * DayW;
    double Y(int row) => Head + row * RowH;
    /// <summary>Rechteck eines Balkens (Zeile = Reihenfolge in <see cref="Bars"/>).</summary>
    public Rect BarRect(int row) { var b = Bars[row]; return b.Step.Milestone ? new Rect(X(b.End) + DayW / 2 - 9, Y(row) + 8, 18, 18) : new Rect(X(b.Start) + 1, Y(row) + 7, b.Days * DayW - 2, RowH - 14); }
    public Point LinkDot(int row) { var r = BarRect(row); return new(r.Right + 8, r.Center.Y); }

    int RowAt(Point p) { var i = (int)Math.Floor((p.Y - Head) / RowH); return i >= 0 && i < Bars.Count ? i : -1; }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e); var p = e.GetPosition(this); _from = _at = p; _mode = Mode.None; _bar = -1;
        for (var i = 0; i < Bars.Count; i++)
        {
            var d = LinkDot(i); if (Math.Abs(p.X - d.X) <= 7 && Math.Abs(p.Y - d.Y) <= 7) { _bar = i; _mode = Mode.Link; break; }
            var r = BarRect(i); if (!r.Inflate(2).Contains(p)) continue;
            _bar = i; _mode = Bars[i].Step.Milestone ? Mode.Move : p.X <= r.Left + 6 ? Mode.Start : p.X >= r.Right - 6 ? Mode.End : Mode.Move; break;
        }
        if (_bar >= 0) { e.Pointer.Capture(this); e.Handled = true; }
    }
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e); var p = e.GetPosition(this);
        if (_mode == Mode.None)
        {
            var c = StandardCursorType.Arrow;
            for (var i = 0; i < Bars.Count; i++) { var r = BarRect(i); var d = LinkDot(i); if (Math.Abs(p.X - d.X) <= 7 && Math.Abs(p.Y - d.Y) <= 7) { c = StandardCursorType.Cross; break; } if (r.Contains(p)) { c = !Bars[i].Step.Milestone && (p.X <= r.Left + 6 || p.X >= r.Right - 6) ? StandardCursorType.SizeWestEast : StandardCursorType.Hand; break; } }
            Cursor = new Cursor(c); return;
        }
        _at = p; InvalidateVisual();
    }
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e); e.Pointer.Capture(null); var mode = _mode; var bar = _bar; _mode = Mode.None; _bar = -1;
        if (bar < 0) { InvalidateVisual(); return; }
        var days = (int)Math.Round((_at.X - _from.X) / DayW);
        switch (mode)
        {
            case Mode.Link: { var t = RowAt(_at); if (t >= 0 && t != bar) Linked?.Invoke(Bars[bar].No, Bars[t].No); else InvalidateVisual(); break; }
            case Mode.Move when days != 0: Moved?.Invoke(Bars[bar].No, days); break;
            case Mode.Start when days != 0: Resized?.Invoke(Bars[bar].No, days, true); break;
            case Mode.End when days != 0: Resized?.Invoke(Bars[bar].No, days, false); break;
            default: Clicked?.Invoke(bar); break;
        }
    }

    public override void Render(DrawingContext g)
    {
        var w = DaysShown * DayW; var h = Head + Bars.Count * RowH;
        g.FillRectangle(new SolidColorBrush(Color.Parse("#FF0B1424")), new Rect(Bounds.Size));
        var muted = new SolidColorBrush(Color.Parse("#8AA0BD")); var text = new SolidColorBrush(Color.Parse("#E6EDF7"));
        var tf = new Typeface("Inter"); var tfb = new Typeface("Inter", FontStyle.Normal, FontWeight.SemiBold);
        // Zeilenbänder
        for (var r = 0; r < Bars.Count; r++) if (r % 2 == 0) g.FillRectangle(new SolidColorBrush(Color.Parse("#0AFFFFFF")), new Rect(0, Y(r), w, RowH));
        // Tage, Wochenenden, Monate
        for (var i = 0; i < DaysShown; i++)
        {
            var d = From.Date.AddDays(i); var x = i * DayW;
            if (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) g.FillRectangle(new SolidColorBrush(Color.Parse("#14FFFFFF")), new Rect(x, 20, DayW, h - 20));
            g.DrawLine(new Pen(new SolidColorBrush(Color.Parse(d.DayOfWeek == DayOfWeek.Monday ? "#30FFFFFF" : "#12FFFFFF")), 1), new Point(x, 20), new Point(x, h));
            if (DayW >= 18) { var t = new FormattedText(d.Day.ToString(De), De, FlowDirection.LeftToRight, tf, 10.5, d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? muted : text); g.DrawText(t, new Point(x + (DayW - t.Width) / 2, 22)); }
            if (d.Day == 1 || i == 0 && (DateTime.DaysInMonth(d.Year, d.Month) - d.Day + 1) * DayW > 110) g.DrawText(new FormattedText(d.ToString("MMMM yyyy", De), De, FlowDirection.LeftToRight, tfb, 12, new SolidColorBrush(Color.Parse("#F6D98C"))), new Point(x + 4, 2));
        }
        // Heute
        var today = X(DateTime.Today) + DayW / 2;
        if (today >= 0 && today <= w)
        {
            g.DrawLine(new Pen(new SolidColorBrush(Color.Parse("#D4A84B")), 2), new Point(today, 18), new Point(today, h));
            g.DrawText(new FormattedText("heute", De, FlowDirection.LeftToRight, tfb, 9.5, new SolidColorBrush(Color.Parse("#D4A84B"))), new Point(today + 3, h - 14));
        }
        // Abhängigkeiten
        var row = Bars.Select((b, i) => (b.No, i)).ToDictionary(x => x.No, x => x.i);
        var arrow = new Pen(new SolidColorBrush(Color.Parse("#B0C4DE")), 1.4);
        for (var i = 0; i < Bars.Count; i++)
            foreach (var pre in Bars[i].Step.After ?? [])
                if (row.TryGetValue(pre, out var pi))
                {
                    var a = BarRect(pi); var b = BarRect(i); var crit = Bars[i].Critical && Bars[pi].Critical; var pen = crit ? new Pen(new SolidColorBrush(Color.Parse("#F87171")), 1.6) : arrow;
                    var p0 = new Point(a.Right, a.Center.Y); var p1 = new Point(Math.Max(a.Right + 8, b.Left - 8), a.Center.Y); var p2 = new Point(p1.X, b.Center.Y); var p3 = new Point(b.Left - 2, b.Center.Y);
                    g.DrawLine(pen, p0, p1); g.DrawLine(pen, p1, p2); g.DrawLine(pen, p2, p3);
                    var head = new StreamGeometry(); using (var c = head.Open()) { c.BeginFigure(p3, true); c.LineTo(p3 + new Vector(-6, -4)); c.LineTo(p3 + new Vector(-6, 4)); c.EndFigure(true); }
                    g.DrawGeometry(pen.Brush, null, head);
                }
        // Balken
        for (var i = 0; i < Bars.Count; i++)
        {
            var b = Bars[i]; var r = BarRect(i);
            if (i == _bar && _mode is Mode.Move or Mode.Start or Mode.End)
            {
                var dx = Math.Round((_at.X - _from.X) / DayW) * DayW;
                r = _mode switch { Mode.Move => r.Translate(new Vector(dx, 0)), Mode.Start => new Rect(Math.Min(r.Right - DayW + 2, r.X + dx), r.Y, Math.Max(DayW - 2, r.Width - dx), r.Height), _ => new Rect(r.X, r.Y, Math.Max(DayW - 2, r.Width + dx), r.Height) };
            }
            var col = Color.Parse(b.Step.Done ? "#22B35E" : b.Step.State == 1 ? "#F08A3C" : b.End < DateTime.Today ? "#E5484D" : "#2F86F6");
            var fill = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative), GradientStops = { new GradientStop(Color.FromArgb(255, (byte)Math.Min(255, col.R + 40), (byte)Math.Min(255, col.G + 40), (byte)Math.Min(255, col.B + 40)), 0), new GradientStop(col, 1) } };
            var edge = new Pen(new SolidColorBrush(b.Critical ? Color.Parse("#F87171") : i == Selected ? Color.Parse("#FACC15") : Color.FromArgb(90, 255, 255, 255)), b.Critical || i == Selected ? 2 : 1);
            if (b.Step.Milestone)
            {
                var geo = new StreamGeometry(); using (var c = geo.Open()) { c.BeginFigure(new Point(r.Center.X, r.Top), true); c.LineTo(new Point(r.Right, r.Center.Y)); c.LineTo(new Point(r.Center.X, r.Bottom)); c.LineTo(new Point(r.Left, r.Center.Y)); c.EndFigure(true); }
                g.DrawGeometry(fill, edge, geo);
            }
            else
            {
                g.DrawRectangle(new SolidColorBrush(Color.FromArgb(70, 0, 0, 0)), null, r.Translate(new Vector(0, 2)), 5, 5);
                g.DrawRectangle(fill, edge, r, 5, 5);
                using (g.PushClip(r.Deflate(2)))
                    g.DrawText(new FormattedText(b.Step.Text, De, FlowDirection.LeftToRight, tfb, 11, Brushes.White), new Point(r.X + 6, r.Y + (r.Height - 14) / 2));
            }
            if (i == Selected || i == _bar) { var d = LinkDot(i); g.DrawEllipse(new SolidColorBrush(Color.Parse("#FACC15")), new Pen(Brushes.White, 1), d, 5, 5); }
        }
        if (_mode == Mode.Link && _bar >= 0)
            g.DrawLine(new Pen(new SolidColorBrush(Color.Parse("#FACC15")), 2, DashStyle.Dash), LinkDot(_bar), _at);
    }
}
