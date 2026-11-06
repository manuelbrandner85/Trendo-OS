using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Trendo.Core.Media;

namespace Trendo.App;

/// <summary>Paket 01 – Studio 4, S4-04 (06.11.2026): Piano-Roll wie in Bitwig/Studio One – Tasten links, Raster 1/16, Takte betont,
/// Noten als goldene Balken (Deckkraft = Anschlag). Klick setzt oder löscht eine Note.</summary>
public sealed class PianoRollView : Control
{
    public IReadOnlyList<PianoRoll.Note> Notes { get; set; } = [];
    public int Bars { get; set; } = 4;
    public int Numerator { get; set; } = 4;
    public int Low { get; set; } = 48;   // C3
    public int High { get; set; } = 84;  // C6
    public int Grid { get; set; } = 120; // 1/16
    public const double KeyWidth = 44;
    /// <summary>Klick: Tonhöhe und Tick (auf das Raster gezogen).</summary>
    public Action<int, int>? Clicked { get; set; }

    int Ticks => Bars * Numerator * PianoRoll.Ppq;
    double RowH => Bounds.Height / Math.Max(1, High - Low + 1);
    double TickW => (Bounds.Width - KeyWidth) / Math.Max(1, Ticks);

    public (int Pitch, int Tick) HitTest(Point p)
    {
        var pitch = High - (int)Math.Floor(p.Y / RowH);
        var tick = (int)Math.Floor(Math.Max(0, p.X - KeyWidth) / TickW / Grid) * Grid;
        return (Math.Clamp(pitch, Low, High), Math.Clamp(tick, 0, Math.Max(0, Ticks - Grid)));
    }

    public Point PointOf(int pitch, int tick) => new(KeyWidth + (tick + Grid / 2.0) * TickW, (High - pitch + 0.5) * RowH);

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var p = e.GetPosition(this); if (p.X < KeyWidth) return;
        var (pitch, tick) = HitTest(p); Clicked?.Invoke(pitch, tick); e.Handled = true;
    }

    static bool Black(int pitch) => (pitch % 12) is 1 or 3 or 6 or 8 or 10;

    public override void Render(DrawingContext ctx)
    {
        var w = Bounds.Width; var h = Bounds.Height; var rh = RowH; var tw = TickW;
        ctx.FillRectangle(new SolidColorBrush(Color.Parse("#11151C")), new Rect(0, 0, w, h));
        for (var p = High; p >= Low; p--)
        {
            var y = (High - p) * rh;
            ctx.FillRectangle(new SolidColorBrush(Color.Parse(Black(p) ? "#161B24" : "#1C222C")), new Rect(KeyWidth, y, w - KeyWidth, rh));
            ctx.FillRectangle(new SolidColorBrush(Color.Parse(Black(p) ? "#2A2F38" : "#D9DDE3")), new Rect(0, y, KeyWidth - 2, rh - 0.5));
            if (p % 12 == 0)
            {
                ctx.DrawText(new FormattedText(PianoRoll.NoteName(p), System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), Math.Min(10, rh * 0.8), Brushes.Black), new Point(4, y + Math.Max(0, (rh - 11) / 2)));
                ctx.DrawLine(new Pen(new SolidColorBrush(Color.Parse("#3A4250")), 1), new Point(KeyWidth, y + rh), new Point(w, y + rh));
            }
        }
        for (var t = 0; t <= Ticks; t += Grid)
        {
            var x = KeyWidth + t * tw; var bar = t % (Numerator * PianoRoll.Ppq) == 0; var beat = t % PianoRoll.Ppq == 0;
            ctx.DrawLine(new Pen(new SolidColorBrush(Color.Parse(bar ? "#5A6475" : beat ? "#343B47" : "#232934")), bar ? 1.4 : 1), new Point(x, 0), new Point(x, h));
        }
        foreach (var n in Notes)
        {
            if (n.Pitch < Low || n.Pitch > High) continue;
            var r = new Rect(KeyWidth + n.Start * tw + 0.5, (High - n.Pitch) * rh + 1, Math.Max(3, n.Length * tw - 1), Math.Max(2, rh - 2));
            var c = Color.Parse("#F5B83D"); var a = (byte)(110 + n.Velocity);
            ctx.DrawRectangle(new SolidColorBrush(Color.FromArgb(a, c.R, c.G, c.B)), new Pen(new SolidColorBrush(Color.Parse("#FFE2A0")), 1), r, 2, 2);
        }
    }
}
