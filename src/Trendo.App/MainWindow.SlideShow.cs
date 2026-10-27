using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Trendo.Core;
using Trendo.Core.Office;

namespace Trendo.App;

/// <summary>T-0229 P4/P5: Bildschirmpräsentation – Vollbild, Übergänge, Klick-Animationen, Laserpointer, schwarzer Bildschirm,
/// Referentenansicht mit Notizen, Uhr und nächster Folie. Video/Audio öffnet der Standard-Player per Klick.</summary>
public partial class MainWindow
{
    public Window? ShowWindow { get; private set; }
    public int ShowIndex { get; private set; }
    /// <summary>Eingeblendete animierte Objekte der aktuellen Folie.</summary>
    public int ShowRevealed { get; private set; }
    public bool ShowBlack { get; private set; }
    public bool ShowLaser { get; private set; }
    private SlideView? _showView, _showNext; private TextBlock? _showNotes, _showClock, _showInfo; private Border? _showBlackLayer;
    private DateTime _showStart; private DispatcherTimer? _showTimer, _showAnim;

    public void StartShow(int from, bool presenter = false)
    {
        CloseDeckEditor(true); EndShow();
        ShowIndex = Math.Clamp(from, 0, Deck.Slides.Count - 1); ShowRevealed = 0; ShowBlack = false; ShowLaser = false; _showStart = DateTime.Now;
        _showView = new SlideView { Name = "show-slide", Doc = Deck, Slide = Deck.Slides[ShowIndex], Revealed = 0, Focusable = true };
        _showBlackLayer = new Border { Name = "show-black", Background = Brushes.Black, IsVisible = false };
        Control content = new Panel { Background = Brushes.Black, Children = { _showView, _showBlackLayer } };
        if (presenter)
        {
            _showNext = new SlideView { Name = "show-next", Doc = Deck, Slide = Deck.Slides[Math.Min(ShowIndex + 1, Deck.Slides.Count - 1)], Height = 200, IsHitTestVisible = false };
            _showNotes = new TextBlock { Name = "show-notes", Foreground = Brushes.White, FontSize = 22, TextWrapping = TextWrapping.Wrap };
            _showClock = new TextBlock { Name = "show-clock", Foreground = Brush.Parse("#93C5FD"), FontSize = 28, FontWeight = FontWeight.Bold };
            _showInfo = new TextBlock { Foreground = Brush.Parse("#CBD5E1"), FontSize = 16 };
            var right = new StackPanel { Spacing = 12, Margin = new Thickness(16), Children = { _showClock, _showInfo, new TextBlock { Text = "Nächste Folie", Foreground = Brush.Parse("#94A3B8"), FontSize = 14 }, _showNext,
                new TextBlock { Text = "Notizen", Foreground = Brush.Parse("#94A3B8"), FontSize = 14 }, new ScrollViewer { Content = _showNotes, MaxHeight = 420 } } };
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions("2*,*"), Background = Brush.Parse("#0B1220") };
            g.Children.Add(content); Grid.SetColumn(right, 1); g.Children.Add(right);
            content = g;
        }
        var w = new Window { Title = "Vorführung", Content = content, Background = Brushes.Black, WindowDecorations = Avalonia.Controls.WindowDecorations.None, WindowState = WindowState.FullScreen, Topmost = true };
        w.KeyDown += (_, e) => { ShowKey(e.Key, e.KeyModifiers); e.Handled = true; };
        _showView.PointerPressed += (_, e) =>
        {
            var pt = e.GetCurrentPoint(_showView);
            if (MediaAt(e.GetPosition(_showView)) is { } media) { OpenMedia(media); return; }
            if (pt.Properties.IsRightButtonPressed) ShowPrev(); else ShowNext();
        };
        _showView.PointerMoved += (_, e) =>
        {
            if (!ShowLaser) { _showView.Laser = null; return; }
            var (a, k) = _showView.Frame(); var p = e.GetPosition(_showView);
            _showView.Laser = new Point((p.X - a.X) / k, (p.Y - a.Y) / k); _showView.InvalidateVisual();
        };
        w.Closed += (_, _) => { if (ShowWindow == w) EndShow(); };
        ShowWindow = w;
        _showTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _showTimer.Tick += (_, _) => UpdatePresenter();
        _showTimer.Start();
        UpdatePresenter();
        _core.Audit.Append(Principal.LocalOwner.Name, "praesentation.show", $"{Deck.Slides.Count} Folien{(presenter ? " · Referentenansicht" : "")}");
        w.Show();
        Dispatcher.UIThread.Post(() => _showView?.Focus());
    }

    public void EndShow()
    {
        _showTimer?.Stop(); _showAnim?.Stop();
        var w = ShowWindow; ShowWindow = null;
        w?.Close();
        _showView = _showNext = null;
        if (w is not null) { DeckIndex = ShowIndex; if (DeckStartVisible == false) Show("praesentation"); }
    }

    int AnimatedCount(Slide s) => s.Shapes.Count(x => x.Animation != Animation.Keine);

    /// <summary>Weiter: erst die nächste Animation, dann die nächste Folie; nach der letzten endet die Vorführung.</summary>
    public void ShowNext()
    {
        if (ShowWindow is null || _showView is null) return;
        if (ShowBlack) { SetBlack(false); return; }
        if (ShowRevealed < AnimatedCount(Deck.Slides[ShowIndex])) { ShowRevealed++; RunAnim(_ => { }); return; }
        if (ShowIndex >= Deck.Slides.Count - 1) { EndShow(); return; }
        GoShow(ShowIndex + 1);
    }

    public void ShowPrev()
    {
        if (ShowWindow is null || _showView is null) return;
        if (ShowRevealed > 0) { ShowRevealed--; _showView.Revealed = ShowRevealed; _showView.InvalidateVisual(); return; }
        if (ShowIndex > 0) { GoShow(ShowIndex - 1, revealAll: true); }
    }

    void GoShow(int index, bool revealAll = false)
    {
        if (_showView is null) return;
        ShowIndex = index; var slide = Deck.Slides[index];
        ShowRevealed = revealAll ? AnimatedCount(slide) : 0;
        _showView.Slide = slide; _showView.Revealed = ShowRevealed; _showView.AnimProgress = 1;
        // Übergang: Einblenden über die Deckkraft, Schieben von unten, Wischen von links
        var t = slide.Transition;
        if (t == Transition.Keine || revealAll) { _showView.Opacity = 1; _showView.RenderTransform = null; _showView.Clip = null; _showView.InvalidateVisual(); }
        else RunAnim(p =>
        {
            if (t == Transition.Ueberblenden) _showView.Opacity = p;
            else if (t == Transition.Schieben) _showView.RenderTransform = new TranslateTransform(0, (1 - p) * _showView.Bounds.Height);
            else _showView.Clip = new RectangleGeometry(new Rect(0, 0, Math.Max(1, _showView.Bounds.Width * p), Math.Max(1, _showView.Bounds.Height)));
        }, transition: true);
        UpdatePresenter();
    }

    /// <summary>Kurze Animation (0,4 s). In Tests ohne Fenster sofort fertig.</summary>
    void RunAnim(Action<double> step, bool transition = false)
    {
        if (_showView is null) return;
        _showAnim?.Stop();
        var t0 = DateTime.Now; var v = _showView;
        void Apply(double p)
        {
            if (transition) step(p);
            else { v.Revealed = ShowRevealed; v.AnimProgress = p; }
            v.InvalidateVisual();
        }
        if (ReduceMotionActive) { Apply(1); if (transition) { v.Opacity = 1; v.RenderTransform = null; v.Clip = null; } return; }
        Apply(0);
        _showAnim = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _showAnim.Tick += (_, _) =>
        {
            var p = Math.Min(1, (DateTime.Now - t0).TotalMilliseconds / 400);
            Apply(1 - Math.Pow(1 - p, 3));
            if (p >= 1) { _showAnim?.Stop(); if (transition) { v.Opacity = 1; v.RenderTransform = null; v.Clip = null; } }
        };
        _showAnim.Start();
    }

    bool ReduceMotionActive => _settings.ReduceMotion;

    void SetBlack(bool on) { ShowBlack = on; if (_showBlackLayer is not null) _showBlackLayer.IsVisible = on; }

    public void ShowKey(Key key, KeyModifiers mods = KeyModifiers.None)
    {
        switch (key)
        {
            case Key.Right or Key.Space or Key.Down or Key.PageDown or Key.Enter or Key.N: ShowNext(); break;
            case Key.Left or Key.Up or Key.PageUp or Key.Back or Key.P: ShowPrev(); break;
            case Key.Home: GoShow(0); break;
            case Key.End: GoShow(Deck.Slides.Count - 1, revealAll: true); break;
            case Key.Escape: EndShow(); break;
            case Key.S or Key.B or Key.OemPeriod: SetBlack(!ShowBlack); break;
            case Key.L: ShowLaser = !ShowLaser; if (!ShowLaser && _showView is not null) { _showView.Laser = null; _showView.InvalidateVisual(); } break;
        }
    }

    string? MediaAt(Point p)
    {
        if (_showView is null) return null;
        foreach (var s in _showView.Slide.Shapes.Where(x => x.Kind == ShapeKind.Medien && x.MediaPath is not null))
            if (_showView.ShapeRect(s).Contains(p)) return s.MediaPath;
        return null;
    }

    void OpenMedia(string path)
    {
        if (!File.Exists(path)) { Toast($"Datei nicht gefunden: {Path.GetFileName(path)}"); return; }
        if (_opt.Isolated) return;
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException) { Toast("Abspielen ging nicht: " + e.Message); }
    }

    void UpdatePresenter()
    {
        if (_showNotes is not null) _showNotes.Text = Deck.Slides[ShowIndex].Notes.Length > 0 ? Deck.Slides[ShowIndex].Notes : "(keine Notizen)";
        if (_showNext is not null) { _showNext.Slide = Deck.Slides[Math.Min(ShowIndex + 1, Deck.Slides.Count - 1)]; _showNext.InvalidateVisual(); }
        if (_showClock is not null) _showClock.Text = (DateTime.Now - _showStart).ToString(@"mm\:ss") + "   " + DateTime.Now.ToString("HH:mm");
        if (_showInfo is not null) _showInfo.Text = $"Folie {ShowIndex + 1} von {Deck.Slides.Count}";
    }
}
