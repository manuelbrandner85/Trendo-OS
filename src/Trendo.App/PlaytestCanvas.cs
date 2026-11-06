using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>Spiel-Editor: Probespielen im Trendo-Fenster. Pfeiltasten (oder A/D) laufen, Leertaste (oder ↑/W) springt, Esc beendet.
/// 60 Bilder je Sekunde, die Kamera folgt der Figur; das Level wird auf die Höhe des Fensters eingepasst.</summary>
public sealed class PlaytestCanvas : Control
{
    public required PlatformSim Sim { get; set; }
    public Action? Ticked { get; set; }
    public Action? Ended { get; set; }
    public Action? Stop { get; set; }
    /// <summary>Hintergrund-Ebene je Level (nur Deko).</summary>
    public IReadOnlyList<IReadOnlyList<string>> Decor { get; set; } = [];
    /// <summary>Animation je Zustand: Bildstreifen, Anzahl Bilder, Bilder je Sekunde.</summary>
    public Dictionary<string, (Avalonia.Media.Imaging.Bitmap Img, int Frames, int Fps)> Anims { get; set; } = [];
    /// <summary>Bild der Figur im aktuellen Spielschritt: Zustand und Bildnummer im Streifen.</summary>
    public (string State, int Frame) CurrentFrame()
    {
        var st = Sim.State;
        return Anims.TryGetValue(st, out var a) || Anims.TryGetValue("stehen", out a) ? (st, (int)(Sim.Frames * a.Fps / 60) % Math.Max(1, a.Frames)) : (st, 0);
    }
    readonly HashSet<Key> _down = [];
    DispatcherTimer? _timer; bool _wasOver;

    public PlaytestCanvas() { Focusable = true; ClipToBounds = true; }
    protected override Size MeasureOverride(Size available) => new(double.IsInfinity(available.Width) ? 900 : available.Width, 360);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, (_, _) => Tick());
        _timer.Start(); Dispatcher.UIThread.Post(() => Focus(), DispatcherPriority.Input);
    }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e) { base.OnDetachedFromVisualTree(e); _timer?.Stop(); _timer = null; }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape) { Stop?.Invoke(); e.Handled = true; return; }
        if (e.Key is Key.Left or Key.Right or Key.Up or Key.Space or Key.A or Key.D or Key.W or Key.LeftShift or Key.RightShift or Key.X or Key.J) { _down.Add(e.Key); e.Handled = true; }
    }
    protected override void OnKeyUp(KeyEventArgs e) { base.OnKeyUp(e); _down.Remove(e.Key); }
    protected override void OnPointerPressed(PointerPressedEventArgs e) { base.OnPointerPressed(e); Focus(); }

    /// <summary>Ein Bild weiter mit den gehaltenen Tasten (öffentlich für Tests).</summary>
    public void Tick(bool? left = null, bool? right = null, bool? jump = null, bool? sprint = null, bool? attack = null)
    {
        Sim.Step(left ?? (_down.Contains(Key.Left) || _down.Contains(Key.A)), right ?? (_down.Contains(Key.Right) || _down.Contains(Key.D)),
            jump ?? (_down.Contains(Key.Space) || _down.Contains(Key.Up) || _down.Contains(Key.W)), sprint ?? (_down.Contains(Key.LeftShift) || _down.Contains(Key.RightShift)), attack ?? (_down.Contains(Key.X) || _down.Contains(Key.J)));
        InvalidateVisual(); Ticked?.Invoke();
        if (Sim.Over && !_wasOver) { _wasOver = true; Ended?.Invoke(); }
        _wasOver = Sim.Over;
    }

    static readonly IBrush HpBar = new SolidColorBrush(Color.Parse("#FF22C55E"));
    static readonly IBrush Hero = new SolidColorBrush(Color.Parse("#FF4FB3FF")), Shade = new SolidColorBrush(Color.Parse("#AA000000"));

    public override void Render(DrawingContext ctx)
    {
        var s = Sim; const int T = PlatformSim.T;
        var k = Math.Min(1.2, Bounds.Height / Math.Max(1, s.Rows * T)); var c = T * k;
        ctx.FillRectangle(LevelCanvas.Sky, new Rect(Bounds.Size));
        var cam = Math.Max(0, Math.Min(s.Cols * c - Bounds.Width, s.Player.X * k - Bounds.Width / 3));
        var oy = Math.Max(0, Bounds.Height - s.Rows * c);
        using (ctx.PushTransform(Matrix.CreateTranslation(-cam, oy)))
        {
            int x0 = (int)Math.Floor(cam / c), x1 = (int)Math.Ceiling((cam + Bounds.Width) / c);
            if (s.LevelIndex < Decor.Count)
            {
                var dec = Decor[s.LevelIndex];
                for (var y = 0; y < dec.Count; y++)
                    for (var x = Math.Max(0, x0); x <= x1 && x < dec[y].Length; x++) LevelCanvas.DrawTile(ctx, dec[y][x], new Rect(x * c, y * c, c, c), false);
            }
            for (var y = 0; y < s.Rows; y++)
                for (var x = Math.Max(0, x0); x <= x1 && x < s.Cols; x++)
                {
                    var ch = s.At(x, y); if (ch is 'o' or 'E' or 'S' or '.' || (ch == 'D' && s.DoorsOpen)) continue; // offene Türen verschwinden // Münzen und Gegner bewegen sich bzw. verschwinden
                    LevelCanvas.DrawTile(ctx, ch, new Rect(x * c, y * c, c, c), y == 0 || s.At(x, y - 1) != '#');
                }
            foreach (var q in s.Coins.Where(q => !q.Taken)) LevelCanvas.DrawTile(ctx, 'o', new Rect(q.Col * c, q.Row * c, c, c), false);
            foreach (var f in s.Foes.Where(f => f.Alive))
            {
                if (!(f.Hurt > 0 && f.Hurt % 4 < 2)) LevelCanvas.DrawTile(ctx, 'E', new Rect(f.X * k - 4 * k, f.Y * k - 6 * k, c, c), false);
                if (f.MaxHp > 1) { ctx.FillRectangle(Brushes.Black, new Rect((f.X - 2) * k, (f.Y - 8) * k, 28 * k, 4 * k)); ctx.FillRectangle(HpBar, new Rect((f.X - 2) * k, (f.Y - 8) * k, 28 * k * f.Hp / f.MaxHp, 4 * k)); }
            }
            foreach (var sh in s.Shots) ctx.DrawEllipse(LevelCanvas.Coin, null, new Point(sh.X * k, sh.Y * k), 4 * k, 4 * k);
            if (s.Swing > 0) ctx.DrawGeometry(null, new Pen(Brushes.WhiteSmoke, 3 * k), new PolylineGeometry([new Point((s.Player.X + 12 + s.Player.Facing * 8) * k, (s.Player.Y - 4) * k), new Point((s.Player.X + 12 + s.Player.Facing * 34) * k, (s.Player.Y + 14) * k), new Point((s.Player.X + 12 + s.Player.Facing * 8) * k, (s.Player.Y + 32) * k)], false));
            if (!(s.Flash > 0 && s.Flash % 4 < 2))
            {
                var pr = new Rect(s.Player.X * k, s.Player.Y * k, s.Player.W * k, s.Player.H * k);
                var st = s.State;
                if (Anims.TryGetValue(st, out var a) || Anims.TryGetValue("stehen", out a))
                {
                    var n = CurrentFrame().Frame;
                    var fw = a.Img.PixelSize.Width / (double)Math.Max(1, a.Frames);
                    var src = new Rect(n * fw, 0, fw, a.Img.PixelSize.Height);
                    if (s.Player.Facing < 0)
                        using (ctx.PushTransform(Matrix.CreateScale(-1, 1) * Matrix.CreateTranslation(2 * pr.X + pr.Width, 0))) ctx.DrawImage(a.Img, src, pr);
                    else ctx.DrawImage(a.Img, src, pr);
                }
                else ctx.DrawRectangle(Hero, null, new RoundedRect(pr, 4 * k));
            }
        }
        var hud = new FormattedText($"Punkte {s.Score}   Leben {Math.Max(0, s.Lives)}" + (s.LevelCount > 1 ? $"   Level {s.LevelIndex + 1}/{s.LevelCount}" : ""), System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Inter", FontStyle.Normal, FontWeight.Bold), 15, Brushes.White);
        ctx.FillRectangle(Shade, new Rect(8, 8, hud.Width + 16, hud.Height + 8)); ctx.DrawText(hud, new Point(16, 12));
        if (s.Over)
        {
            var t = new FormattedText(s.Won ? "Geschafft!" : "Verloren", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Inter", FontStyle.Normal, FontWeight.Bold), 34, Brushes.White);
            ctx.FillRectangle(Shade, new Rect(0, 0, Bounds.Width, Bounds.Height)); ctx.DrawText(t, new Point((Bounds.Width - t.Width) / 2, (Bounds.Height - t.Height) / 2));
        }
        else if (!IsFocused)
        {
            var t = new FormattedText("Hier klicken, dann spielen: ← → laufen · Leertaste springt · Esc beendet", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Inter"), 13, Brushes.White);
            ctx.FillRectangle(Shade, new Rect((Bounds.Width - t.Width) / 2 - 10, Bounds.Height - t.Height - 22, t.Width + 20, t.Height + 10)); ctx.DrawText(t, new Point((Bounds.Width - t.Width) / 2, Bounds.Height - t.Height - 17));
        }
    }
}
