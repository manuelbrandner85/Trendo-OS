using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core.Media;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>Farbrad: Punkt im Kreis ziehen, Doppelklick stellt zurück.</summary>
public sealed class VideoColorWheel : Control
{
    public double ValueX { get; set; }
    public double ValueY { get; set; }
    public Action<double, double>? Changed { get; set; }
    public VideoColorWheel() { Width = 92; Height = 92; ClipToBounds = false; }

    (double, double) FromPoint(Point p)
    {
        var r = Bounds.Width / 2 - 4; var (x, y) = ((p.X - Bounds.Width / 2) / r, (p.Y - Bounds.Height / 2) / r);
        var len = Math.Sqrt(x * x + y * y);
        return len > 1 ? (x / len, y / len) : (x, y);
    }
    protected override void OnPointerPressed(PointerPressedEventArgs e) { base.OnPointerPressed(e); e.Pointer.Capture(this); (ValueX, ValueY) = FromPoint(e.GetPosition(this)); InvalidateVisual(); e.Handled = true; }
    protected override void OnPointerMoved(PointerEventArgs e) { base.OnPointerMoved(e); if (!ReferenceEquals(e.Pointer.Captured, this)) return; (ValueX, ValueY) = FromPoint(e.GetPosition(this)); InvalidateVisual(); }
    protected override void OnPointerReleased(PointerReleasedEventArgs e) { base.OnPointerReleased(e); if (!ReferenceEquals(e.Pointer.Captured, this)) return; e.Pointer.Capture(null); Changed?.Invoke(ValueX, ValueY); }
    protected override void OnDoubleTapped(Avalonia.Input.TappedEventArgs e) { base.OnDoubleTapped(e); ValueX = ValueY = 0; InvalidateVisual(); Changed?.Invoke(0, 0); }

    public override void Render(DrawingContext ctx)
    {
        var c = new Point(Bounds.Width / 2, Bounds.Height / 2); var r = Bounds.Width / 2 - 4;
        for (var d = 0; d < 360; d += 10)
        {
            var (x, y) = (Math.Cos(d * Math.PI / 180), Math.Sin(d * Math.PI / 180));
            var (cr, cg, cb) = VideoGrade.Rgb(x, y);
            byte B(double v) => (byte)Math.Clamp(128 + v * 240, 0, 255);
            ctx.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(B(cr), B(cg), B(cb))), 5), c + new Vector(x, y) * r, c + new Vector(Math.Cos((d + 10) * Math.PI / 180), Math.Sin((d + 10) * Math.PI / 180)) * r);
        }
        ctx.DrawEllipse(new SolidColorBrush(Color.Parse("#1B2230")), null, c, r - 3, r - 3);
        ctx.DrawLine(new Pen(new SolidColorBrush(Color.Parse("#33FFFFFF")), 1), c - new Vector(r, 0), c + new Vector(r, 0));
        ctx.DrawLine(new Pen(new SolidColorBrush(Color.Parse("#33FFFFFF")), 1), c - new Vector(0, r), c + new Vector(0, r));
        ctx.DrawEllipse(new SolidColorBrush(Color.Parse("#F5B83D")), new Pen(Brushes.Black, 1), c + new Vector(ValueX, ValueY) * (r - 4), 5, 5);
    }
}

/// <summary>Pilot 4.0: V7 Farbräder, V8 Mischpult, V9 Rechtsklick auf der Zeitleiste.</summary>
public partial class MainWindow
{
    public List<VideoColorWheel> PreviewWheelsForTests { get; } = [];

    Control EditGradePanel(ProgramDocument d, string clip)
    {
        var sp = new StackPanel { Name = "ve-grade", Spacing = 4, Margin = new Thickness(0, 8, 0, 0) };
        sp.Children.Add(H("Farbräder", 13));
        sp.Children.Add(P("Punkt zum gewünschten Farbton ziehen. Doppelklick stellt zurück. Sichtbar wird es im Export und in den Scopes.", Muted, 11));
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        PreviewWheelsForTests.Clear();
        foreach (var (id, label, _) in VideoGrade.Wheels)
        {
            var (x, y) = VideoGrade.Get(d.Data, clip, id);
            var wheel = new VideoColorWheel { Name = "ve-wheel-" + id, ValueX = x, ValueY = y };
            var wid = id;
            wheel.Changed = (nx, ny) => ProgramRun(VK, () => d.Edit("Farbrad", z => VideoGrade.Set(z, clip, wid, nx, ny)));
            PreviewWheelsForTests.Add(wheel);
            row.Children.Add(new StackPanel { Spacing = 2, Children = { wheel, new TextBlock { Text = label, Foreground = Muted, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center } } });
        }
        sp.Children.Add(row);
        var reset = new Button { Name = "ve-grade-reset", Content = "Farbräder zurücksetzen", Padding = new Thickness(10, 4) };
        reset.Click += (_, _) => ProgramRun(VK, () => d.Edit("Farbräder zurücksetzen", z => VideoGrade.Reset(z, clip)));
        sp.Children.Add(reset);
        return sp;
    }

    readonly List<(ProgressBar Bar, int Lane)> _veMeters = [];

    /// <summary>Lautester Wert der Spur an der Stelle t (0…1). Null, wenn dort kein Stück mit lesbarer Wellenform liegt.</summary>
    public double? EditLevelAt(ProgramDocument d, int lane, double t)
    {
        var hit = VideoEdit.Clips(d.Data, "music").Where(m => m.Lane == lane && t >= m.Start && t < m.Start + m.Length).ToList();
        double? best = null;
        foreach (var m in hit)
        {
            if (EditWave(m.File) is not { Length: > 0 } wave) continue;
            var k = (int)((m.In + t - m.Start) * EditTimeline.WaveRate);
            if (k < 0 || k >= wave.Length) continue;
            var v = Math.Max(Math.Abs(wave[k].Min), Math.Abs(wave[k].Max)) * m.Volume * VideoLanes.Gain(d.Data, lane);
            best = Math.Max(best ?? 0, v);
        }
        return best;
    }

    void EditUpdateMeters(ProgramDocument d, double t)
    {
        foreach (var (bar, lane) in _veMeters) bar.Value = Math.Min(1, EditLevelAt(d, lane, t) ?? 0);
    }

    Control EditMixerPanel(ProgramDocument d)
    {
        var sp = new StackPanel { Name = "ve-mixer", Spacing = 8 };
        sp.Children.Add(H("Mischpult", 14));
        sp.Children.Add(P("Der Regler gilt für die ganze Spur. Stumm und Solo stehen auch links an der Zeitleiste.", Muted, 11));
        _veMeters.Clear();
        void Lane(string title, int lane)
        {
            var fader = new Slider { Name = $"ve-mix-gain-{lane}", Minimum = 0, Maximum = 2, Value = VideoLanes.Gain(d.Data, lane), HorizontalAlignment = HorizontalAlignment.Stretch };
            var label = new TextBlock { Name = $"ve-mix-value-{lane}", Text = $"{fader.Value * 100:0} %", Foreground = Text, FontSize = 12, Width = 46, VerticalAlignment = VerticalAlignment.Center };
            fader.PropertyChanged += (_, e) => { if (e.Property == Avalonia.Controls.Primitives.RangeBase.ValueProperty) label.Text = $"{fader.Value * 100:0} %"; };
            var ok = new Button { Name = $"ve-mix-set-{lane}", Content = "OK", Padding = new Thickness(8, 2) };
            ok.Click += (_, _) => ProgramRun(VK, () => d.Edit("Spurpegel", x => VideoLanes.SetGain(x, lane, fader.Value)));
            Button Sw(string id, string text, bool on, Action<System.Text.Json.Nodes.JsonObject> a) { var b = new Button { Name = $"ve-mix-{id}-{lane}", Content = text, Padding = new Thickness(7, 2) }; if (on) b.Classes.Add("accent"); b.Click += (_, _) => ProgramRun(VK, () => d.Edit(text == "M" ? "Stumm" : "Solo", a)); return b; }
            var head = new DockPanel { Children = { Dk(Sw("m", "M", VideoLanes.IsMuted(d.Data, "audio", lane), x => VideoLanes.ToggleMute(x, "audio", lane)), Avalonia.Controls.Dock.Right), Dk(Sw("s", "S", VideoLanes.IsSolo(d.Data, "audio", lane), x => VideoLanes.ToggleSolo(x, "audio", lane)), Avalonia.Controls.Dock.Right), new TextBlock { Text = title, Foreground = Text, FontSize = 12.5, VerticalAlignment = VerticalAlignment.Center } } };
            var body = new DockPanel { Children = { Dk(label, Avalonia.Controls.Dock.Right), Dk(ok, Avalonia.Controls.Dock.Right), fader } };
            sp.Children.Add(head); sp.Children.Add(body);
            if (lane >= 0)
            {
                var bar = new ProgressBar { Name = $"ve-mix-level-{lane}", Minimum = 0, Maximum = 1, Height = 6, Value = Math.Min(1, EditLevelAt(d, lane, d.Number("playhead")) ?? 0), Foreground = Brush.Parse("#3DDC84") };
                sp.Children.Add(bar); _veMeters.Add((bar, lane));
            }
        }
        Lane("Ton der Bildspur", -1);
        for (var a = 0; a < VideoLanes.Audio(d.Data); a++) Lane(a == 0 ? "Musik" : $"Ton {a + 1}", a);
        return sp;
    }

    /// <summary>Rechtsklick-Menü für ein Stück der Zeitleiste.</summary>
    public MenuFlyout EditContextMenu(ProgramDocument d, string track, int index)
    {
        (string, string, Action) Act(string id, string label, Action a) => (id, label, () => ProgramRun(VK, a));
        void Sel() => EditSelected = (track, index);
        return track switch
        {
            "clips" => ContextMenuOf("ve-ctx",
                [Act("teilen", "Am Abspielkopf teilen", () => { if (!VideoEdit.Split(d.Data, d.Number("playhead"))) throw new InvalidOperationException("Am Abspielkopf liegt kein Stück."); d.Edit("Teilen", _ => { }); }), Act("loeschen", "Löschen", () => { Sel(); EditDelete(d); })],
                [Act("vor", "Eine Stelle nach vorn", () => { Sel(); EditShift(d, -1); }), Act("nach", "Eine Stelle nach hinten", () => { Sel(); EditShift(d, 1); })]),
            "music" => ContextMenuOf("ve-ctx", [Act("loeschen", "Löschen", () => { Sel(); EditDelete(d); }), Act("aufruecken", "Löschen und aufrücken", () => { Sel(); EditRippleDelete(d); })]),
            _ => ContextMenuOf("ve-ctx", [Act("entfernen", "Einblendung entfernen", () => { var o = VideoPro.Overlays(d.Data)[index]; d.Edit("Einblendung entfernen", x => VideoPro.RemoveOverlay(x, o.Id)); })]),
        };
    }
}
