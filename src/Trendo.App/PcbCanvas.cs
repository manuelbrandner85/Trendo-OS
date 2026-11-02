using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Trendo.Core.Elektro;

namespace Trendo.App;

/// <summary>T-0277: Platinen-Ansicht – grüne Platine mit Lötstopplack, goldene Durchkontaktierungen, Bestückungsdruck, Leiterbahnen
/// (Oberseite kupferrot, Unterseite blau), Luftlinien gestrichelt. Bauteil mit der Maus ziehen (Raster 1,27 mm), R dreht um 90°.</summary>
public sealed class PcbCanvas : Control
{
    public Circuit? Model { get; set; }
    public string? Selected { get; set; }
    public Action<string, double, double>? Moved;
    public Action<string>? Rotate;
    public Action<string?>? Select;
    string? _drag; Point _start; double _ox, _oy;

    public PcbCanvas() { ClipToBounds = true; Focusable = true; }

    public double Scale => Model is null ? 1 : Math.Min((Bounds.Width - 40) / Model.Pcb.W, (Bounds.Height - 40) / Model.Pcb.H);
    public Point ToScreen(double x, double y) { var s = Scale; return new(20 + x * s, 20 + y * s); }
    (double X, double Y) ToBoard(Point p) { var s = Scale; return ((p.X - 20) / s, (p.Y - 20) / s); }

    public override void Render(DrawingContext ctx)
    {
        ctx.FillRectangle(new SolidColorBrush(Color.Parse("#212121")), new Rect(Bounds.Size));
        if (Model is not { } c) return;
        var s = Scale; var b = new Rect(ToScreen(0, 0), ToScreen(c.Pcb.W, c.Pcb.H));
        ctx.DrawRectangle(new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative), GradientStops = { new GradientStop(Color.Parse("#1F6B3A"), 0), new GradientStop(Color.Parse("#14522B"), 1) } },
            new Pen(Brush.Parse("#C9B37A"), 1.5), b, 3 * s / 10, 3 * s / 10);
        foreach (var (x, y) in new[] { (3.0, 3.0), (c.Pcb.W - 3, 3.0), (3.0, c.Pcb.H - 3), (c.Pcb.W - 3, c.Pcb.H - 3) }) ctx.DrawEllipse(new SolidColorBrush(Color.Parse("#212121")), new Pen(Brush.Parse("#C9A54A"), 0.6 * s), ToScreen(x, y), 1.6 * s, 1.6 * s); // Befestigungslöcher
        // Leiterbahnen: erst Unterseite (durchscheinend), dann Oberseite
        foreach (var bottom in new[] { true, false })
            foreach (var t in c.Pcb.Traces.Where(t => t.Bottom == bottom))
            {
                var pen = new Pen(new SolidColorBrush(bottom ? Color.FromArgb(170, 70, 130, 230) : Color.FromRgb(214, 120, 70)), c.Pcb.TraceWidth * s, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
                for (var i = 1; i < t.Pts.Count; i++) ctx.DrawLine(pen, ToScreen(t.Pts[i - 1][0], t.Pts[i - 1][1]), ToScreen(t.Pts[i][0], t.Pts[i][1]));
            }
        // Bauteile: Bestückungsdruck und Lötaugen
        var silk = new Pen(new SolidColorBrush(Color.FromArgb(230, 245, 245, 240)), Math.Max(1, 0.15 * s));
        foreach (var p in Pcb.Placeable(c))
        {
            if (!c.Pcb.Places.TryGetValue(p.Id, out var pl)) continue; var fp = Footprints.For(p.Kind)!; var (w, h) = pl.Rot % 180 == 0 ? (fp.W, fp.H) : (fp.H, fp.W);
            var r = new Rect(ToScreen(pl.X - w / 2, pl.Y - h / 2), ToScreen(pl.X + w / 2, pl.Y + h / 2));
            if (fp.Round) ctx.DrawEllipse(null, silk, r.Center, r.Width / 2, r.Height / 2); else ctx.DrawRectangle(null, silk, r);
            if (p.Id == Selected) ctx.DrawRectangle(null, new Pen(Brush.Parse("#F5A524"), 2, new DashStyle([4, 3], 0)), r.Inflate(4));
            ctx.DrawText(new FormattedText(p.Name, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, Math.Max(9, 1.4 * s), Brushes.White), new Point(r.X, r.Y - Math.Max(11, 1.8 * s)));
        }
        foreach (var pad in Pcb.Pads(c))
        {
            var cpt = ToScreen(pad.X, pad.Y); var rr = pad.Def.Size / 2 * s;
            var gold = new RadialGradientBrush { GradientStops = { new GradientStop(Color.Parse("#F7E2A0"), 0), new GradientStop(Color.Parse("#C9A54A"), 0.7), new GradientStop(Color.Parse("#8C6E2A"), 1) } };
            if (pad.Def.Square) ctx.DrawRectangle(gold, null, new Rect(cpt.X - rr, cpt.Y - rr, 2 * rr, 2 * rr)); else ctx.DrawEllipse(gold, null, cpt, rr, rr);
            ctx.DrawEllipse(new SolidColorBrush(Color.Parse("#0B1220")), null, cpt, pad.Def.Drill / 2 * s, pad.Def.Drill / 2 * s); // Bohrung
        }
        // Luftlinien
        var air = new Pen(new SolidColorBrush(Color.FromArgb(220, 250, 204, 21)), 1.2, new DashStyle([4, 3], 0));
        foreach (var a in Pcb.Ratsnest(c)) ctx.DrawLine(air, ToScreen(a.X1, a.Y1), ToScreen(a.X2, a.Y2));
    }

    string? HitPart(Point at)
    {
        if (Model is not { } c) return null; var (x, y) = ToBoard(at);
        foreach (var p in Pcb.Placeable(c).Reverse())
            if (c.Pcb.Places.TryGetValue(p.Id, out var pl)) { var fp = Footprints.For(p.Kind)!; var (w, h) = pl.Rot % 180 == 0 ? (fp.W, fp.H) : (fp.H, fp.W); if (Math.Abs(x - pl.X) <= w / 2 + 1 && Math.Abs(y - pl.Y) <= h / 2 + 1) return p.Id; }
        return null;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e); Focus(); _start = e.GetPosition(this); _drag = HitPart(_start); Selected = _drag; Select?.Invoke(_drag);
        if (_drag is not null && Model!.Pcb.Places.TryGetValue(_drag, out var pl)) { (_ox, _oy) = (pl.X, pl.Y); e.Pointer.Capture(this); }
        InvalidateVisual();
    }
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e); if (_drag is null || Model is null) return;
        var p = e.GetPosition(this); var s = Scale; var pl = Model.Pcb.Places[_drag];
        pl.X = Pcb.Snap(_ox + (p.X - _start.X) / s); pl.Y = Pcb.Snap(_oy + (p.Y - _start.Y) / s); InvalidateVisual();
    }
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e); if (_drag is null || Model is null) return;
        var pl = Model.Pcb.Places[_drag]; var id = _drag; _drag = null; e.Pointer.Capture(null);
        if (Math.Abs(pl.X - _ox) > 1e-9 || Math.Abs(pl.Y - _oy) > 1e-9) { var (x, y) = (pl.X, pl.Y); pl.X = _ox; pl.Y = _oy; Moved?.Invoke(id, x, y); } // als Bearbeitungsschritt (Rückgängig)
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e); if (e.Key == Key.R && Selected is { } id) { Rotate?.Invoke(id); e.Handled = true; }
    }
}
