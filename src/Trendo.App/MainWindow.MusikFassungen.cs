using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Trendo Musik Profi 26 (05.11.2026): mitlaufender Liedtext beim Abspielen, Tonart- und Tempo-Raster, Fassungen nebeneinander vergleichen (A/B an derselben Stelle).</summary>
public partial class MainWindow
{
    string? _msFollowFile; List<SongFollow.FollowLine> _msFollowPlan = []; List<TextBlock> _msFollowRows = []; int _msFollowIdx = -2; BeatGrid? _msGrid;
    /// <summary>Nach dem Laden an diese Stelle springen (A/B-Vergleich).</summary>
    double? _mPendingSeek;
    public int MsFollowIndex => _msFollowIdx;
    public IReadOnlyList<SongFollow.FollowLine> MsFollowPlan => _msFollowPlan;

    /// <summary>Wird bei jedem Fortschritt des Players gerufen: hebt die laufende Zeile hervor und bewegt die Abspielmarke im Raster.</summary>
    void MsFollowTick()
    {
        if (_mPendingSeek is { } ps && !double.IsNaN(_mDur) && _mDur > 0) { _mPendingSeek = null; _audio?.Seek(ps); _mPos = ps; }
        var playing = _msFollowFile is not null && _queue.Current is { } cur && string.Equals(cur, _msFollowFile, StringComparison.OrdinalIgnoreCase);
        if (_msGrid is not null) { _msGrid.Playhead = playing ? _mPos : -1; _msGrid.InvalidateVisual(); }
        var idx = playing ? SongFollow.At(_msFollowPlan, _mPos) : -1;
        if (idx == _msFollowIdx || _msFollowRows.Count != _msFollowPlan.Count) return;
        _msFollowIdx = idx;
        for (var i = 0; i < _msFollowRows.Count; i++)
        {
            var r = _msFollowRows[i]; var now = i == idx;
            r.Foreground = now ? Brush.Parse("#FFF1C870") : i < idx ? Brush.Parse("#66FFFFFF") : Text;
            r.FontSize = now ? 17 : 13.5; r.FontWeight = now ? FontWeight.Bold : FontWeight.Normal;
        }
        if (idx >= 0 && idx < _msFollowRows.Count) _msFollowRows[idx].BringIntoView();
    }

    /// <summary>Lied an einer Stelle abspielen (auch, wenn gerade ein anderes läuft).</summary>
    public void MsPlayAt(MusicSong s, double seconds)
    {
        if (_queue.Current is { } cur && string.Equals(cur, s.File, StringComparison.OrdinalIgnoreCase)) { _audio?.Seek(seconds); _mPos = seconds; if (_mPaused) MusicTogglePause(); MsFollowTick(); return; }
        MusicPlay([s.File], 0, MS); _mPendingSeek = seconds > 0.05 ? seconds : null;
    }

    /// <summary>A/B-Vergleich wie beim Mastering: die andere Fassung an derselben Stelle weiterhören.</summary>
    public void MsCompareSwitch(MusicSong to)
    {
        var at = _queue.Current is not null && !_mPaused ? _mPos : 0;
        MsPlayAt(to, Math.Min(at, Math.Max(0, (to.Seconds > 0 ? to.Seconds : at + 1) - 0.5)));
        _msMsg = at > 0 ? $"Jetzt „{to.Title}“ ab {SongTime(at)} – gleiche Stelle zum Vergleichen." : $"Spielt „{to.Title}“.";
    }

    Control? MsFollowPanel(MusicSong s)
    {
        _msFollowFile = s.File; _msFollowRows = []; _msFollowIdx = -2;
        _msFollowPlan = string.IsNullOrWhiteSpace(s.Lyrics) || s.Lyrics == "[Instrumental]" ? [] : SongFollow.Plan(s.Lyrics, s.Seconds > 0 ? s.Seconds : s.Analysis?.Duration ?? 0, Understanding(s));
        if (_msFollowPlan.Count == 0) return null;
        var list = new StackPanel { Name = "ms-follow-lines", Spacing = 4 };
        var part = "";
        for (var i = 0; i < _msFollowPlan.Count; i++)
        {
            var l = _msFollowPlan[i];
            if (l.Part.Length > 0 && l.Part != part) { part = l.Part; list.Children.Add(new TextBlock { Text = part.ToUpperInvariant(), Foreground = Brush.Parse("#99D4A84B"), FontSize = 10.5, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 6, 0, 0) }); }
            var row = new TextBlock { Name = "ms-follow-line-" + i, Text = l.Text, Foreground = Text, FontSize = 13.5, TextWrapping = TextWrapping.Wrap, Cursor = new Cursor(StandardCursorType.Hand) };
            ToolTip.SetTip(row, $"Ab {SongTime(l.Start)} abspielen");
            var at = l.Start; row.PointerPressed += (_, _) => MsPlayAt(s, at);
            _msFollowRows.Add(row); list.Children.Add(row);
        }
        var timed = _msFollowPlan.All(l => l.Timed);
        var sp = new StackPanel { Spacing = 6, Children = {
            new DockPanel { Children = {
                Dk(new TextBlock { Text = timed ? "Zeiten gemessen" : "Zeiten geschätzt", Foreground = timed ? Brush.Parse("#4ADE80") : Muted, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) }, Avalonia.Controls.Dock.Right),
                H("Mitlaufender Liedtext", 13) } },
            new Border { Name = "ms-follow", CornerRadius = new CornerRadius(12), Padding = new Thickness(14, 10), MaxHeight = 260,
                Background = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative), GradientStops = { new GradientStop(Color.Parse("#26D4A84B"), 0), new GradientStop(Color.Parse("#0AFFFFFF"), 1) } },
                BorderBrush = Brush.Parse("#33D4A84B"), BorderThickness = new Thickness(1), Child = new ScrollViewer { Content = list } } } };
        if (!timed) sp.Children.Add(P("Genaue Zeiten je Zeile gibt es nach „Lied verstehen“ (Registerkarte Musikvideo). Eine Zeile anklicken spielt ab dort.", Muted, 11));
        MsFollowTick();
        return sp;
    }

    Control MsGridPanel(MusicSong s)
    {
        _msFollowFile = s.File; var a = s.Analysis; var u = Understanding(s);
        var bpm = a?.Bpm ?? u?.Bpm ?? s.Bpm ?? 0; var meter = a?.Meter ?? s.TimeSignature ?? "4/4"; var key = a?.Key ?? s.Key;
        var dur = s.Seconds > 0 ? s.Seconds : a?.Duration ?? u?.Duration ?? 0;
        var sp = new StackPanel { Name = "ms-grid-panel", Spacing = 6 };
        sp.Children.Add(H("Tonart und Tempo", 13));
        if (bpm <= 0 && string.IsNullOrEmpty(key))
        {
            sp.Children.Add(P("Noch kein Tempo und keine Tonart bekannt – unten „Lied analysieren“ misst beides (Tonart, Tempo, Takt, Akkorde).", Muted, 12));
            _msGrid = null; return sp;
        }
        var scale = SongFollow.Scale(key); var rel = SongFollow.Relative(key);
        var facts = new WrapPanel { Name = "ms-grid-facts" };
        void Fact(string name, string label, string value)
        {
            facts.Children.Add(new Border { Name = name, CornerRadius = new CornerRadius(10), Background = Brush.Parse("#14FFFFFF"), BorderBrush = Brush.Parse("#22FFFFFF"), BorderThickness = new Thickness(1), Padding = new Thickness(10, 5), Margin = new Thickness(0, 0, 6, 6),
                Child = new StackPanel { Children = { new TextBlock { Text = label, Foreground = Muted, FontSize = 10.5 }, new TextBlock { Text = value, Foreground = Text, FontSize = 14, FontWeight = FontWeight.SemiBold } } } });
        }
        if (!string.IsNullOrEmpty(key)) Fact("ms-grid-key", "Tonart", SongTools.KeyDe(key));
        if (bpm > 0) Fact("ms-grid-bpm", "Tempo", $"{bpm:0} BPM");
        Fact("ms-grid-meter", "Takt", meter);
        if (bpm > 0) { var per = int.TryParse(meter.Split('/')[0], out var m) ? m : 4; Fact("ms-grid-barlen", "Ein Takt", $"{per * 60 / bpm:0.00} s".Replace('.', ',')); }
        sp.Children.Add(facts);
        if (scale.Count > 0)
            sp.Children.Add(new TextBlock { Name = "ms-grid-scale", Text = $"Tonleiter: {string.Join(" · ", scale)}" + (rel is not null ? $"   ·   Parallele: {SongTools.KeyDe(rel)}" : ""), Foreground = Text, FontSize = 12 });
        var bars = SongFollow.Bars(bpm, meter, dur, u?.Beats);
        var grid = _msGrid = new BeatGrid { Name = "ms-grid", Bars = bars, Seconds = dur, Sections = u?.Sections.Select(x => (x.Start, x.End, x.Part)).ToList() ?? [], Height = 64 };
        grid.Seek = t => MsPlayAt(s, t);
        ToolTip.SetTip(grid, "Takte des Lieds – klicken spielt ab diesem Takt");
        sp.Children.Add(new Border { CornerRadius = new CornerRadius(10), ClipToBounds = true, BorderBrush = Brush.Parse("#33D4A84B"), BorderThickness = new Thickness(1), Child = grid });
        if (a?.Chords is { Count: > 0 } ch) sp.Children.Add(new TextBlock { Name = "ms-grid-chords", Text = "Akkorde: " + string.Join(" – ", ch.Take(16)), Foreground = Muted, FontSize = 11.5, TextWrapping = TextWrapping.Wrap });
        sp.Children.Add(P($"{bars.Count} Takte" + (u?.Beats is { Count: > 4 } ? " aus den gemessenen Schlägen" : " aus dem Tempo gerechnet"), Muted, 11));
        MsFollowTick();
        return sp;
    }

    Control MsComparePanel(MusicSong s)
    {
        var all = Songs.All(); var versions = SongFollow.Versions(all, s).Where(v => File.Exists(v.File)).ToList();
        var sp = new StackPanel { Name = "ms-compare", Spacing = 6 };
        sp.Children.Add(H($"Fassungen vergleichen ({versions.Count})", 13));
        if (versions.Count < 2) { sp.Children.Add(P("Noch keine zweite Fassung. „In anderem Stil (Cover)“, „Abschnitt neu“ oder „Verlängern“ (Bearbeiten) machen eine – dann liegen sie hier nebeneinander.", Muted, 12)); return sp; }
        var cur = _queue.Current;
        var row = new WrapPanel { Name = "ms-compare-row" };
        for (var i = 0; i < versions.Count && i < 6; i++)
        {
            var v = versions[i]; var vv = v; var isSel = v.Id == s.Id;
            var playingThis = cur is not null && string.Equals(cur, v.File, StringComparison.OrdinalIgnoreCase) && !_mPaused;
            var letter = ((char)('A' + i)).ToString();
            var info = new List<string> { SongFollow.KindLabel(v.Kind), SongTime(v.Seconds) };
            var bpm = v.Analysis?.Bpm ?? v.Bpm; if (bpm is > 0) info.Add($"{bpm:0} BPM");
            var key = v.Analysis?.Key ?? v.Key; if (!string.IsNullOrEmpty(key)) info.Add(SongTools.KeyDe(key));
            if (v.Mastered) info.Add("gemastert");
            var play = new Button { Name = "ms-compare-play-" + i, Content = playingThis ? "läuft" : $"{letter} hören", FontSize = 12, Background = playingThis ? Accent : Brush.Parse("#22FFFFFF"), Foreground = playingThis ? Brushes.White : Text, Margin = new Thickness(0, 0, 4, 0) };
            ToolTip.SetTip(play, "An derselben Stelle weiterhören wie die laufende Fassung");
            play.Click += (_, _) => { MsCompareSwitch(vv); Show(MS); };
            var open = new Button { Name = "ms-compare-open-" + i, Content = isSel ? "gewählt" : "Wählen", FontSize = 12, IsEnabled = !isSel };
            open.Click += (_, _) => { _msSel = vv.Id; Show(MS); };
            row.Children.Add(new Border
            {
                Name = "ms-compare-item-" + i, Width = 196, Margin = new Thickness(0, 0, 8, 8), CornerRadius = new CornerRadius(12), Padding = new Thickness(10, 8),
                Background = Brush.Parse(isSel ? "#22D4A84B" : "#10FFFFFF"), BorderBrush = Brush.Parse(playingThis ? "#F1C870" : isSel ? "#66D4A84B" : "#22FFFFFF"), BorderThickness = new Thickness(playingThis ? 2 : 1),
                Child = new StackPanel { Spacing = 4, Children = {
                    new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = {
                        new Border { Width = 24, Height = 24, CornerRadius = new CornerRadius(12), Background = Brush.Parse("#D4A84B"), Child = new TextBlock { Text = letter, Foreground = Brushes.Black, FontWeight = FontWeight.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } },
                        new TextBlock { Text = v.Title, Foreground = Text, FontSize = 12.5, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis, Width = 140, VerticalAlignment = VerticalAlignment.Center } } },
                    new TextBlock { Text = string.Join(" · ", info), Foreground = Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap },
                    new TextBlock { Text = v.Style, Foreground = Muted, FontSize = 10.5, TextWrapping = TextWrapping.Wrap, MaxHeight = 30, TextTrimming = TextTrimming.CharacterEllipsis },
                    new StackPanel { Orientation = Orientation.Horizontal, Children = { play, open } } } }
            });
        }
        sp.Children.Add(row);
        sp.Children.Add(P("Tipp wie beim Mastering: während ein Lied läuft, die andere Fassung antippen – sie spielt an derselben Stelle weiter, so hörst du den Unterschied sofort.", Muted, 11));
        return sp;
    }
}

/// <summary>Taktraster eines Lieds: Takte (jeder vierte nummeriert), Abschnitte farbig darunter, Abspielmarke; Klick springt in den Takt.</summary>
public sealed class BeatGrid : Control
{
    public IReadOnlyList<SongFollow.Bar> Bars { get; set; } = [];
    public IReadOnlyList<(double Start, double End, string Part)> Sections { get; set; } = [];
    public double Seconds { get; set; }
    public double Playhead { get; set; } = -1;
    public Action<double>? Seek { get; set; }
    static readonly string[] PartColors = ["#38BDF8", "#F472B6", "#A78BFA", "#34D399", "#FB923C", "#FACC15"];

    public BeatGrid() { ClipToBounds = true; Cursor = new Cursor(StandardCursorType.Hand); }
    public double ToX(double t) => Seconds <= 0 ? 0 : t / Seconds * Bounds.Width;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e); if (Seconds <= 0) return;
        var t = Math.Clamp(e.GetPosition(this).X / Math.Max(1, Bounds.Width), 0, 1) * Seconds;
        var bar = Bars.LastOrDefault(b => b.Start <= t); Seek?.Invoke(bar?.Start ?? t); e.Handled = true;
    }

    public override void Render(DrawingContext g)
    {
        var w = Bounds.Width; var h = Bounds.Height;
        g.FillRectangle(new SolidColorBrush(Color.Parse("#0E131C")), new Rect(0, 0, w, h));
        var parts = Sections.Select(x => x.Part).Distinct().ToList();
        foreach (var (a, b, p) in Sections)
        {
            var c = Color.Parse(PartColors[Math.Max(0, parts.IndexOf(p)) % PartColors.Length]);
            g.FillRectangle(new SolidColorBrush(Color.FromArgb(70, c.R, c.G, c.B)), new Rect(ToX(a), h - 18, Math.Max(1, ToX(b) - ToX(a)), 18));
            g.DrawText(new FormattedText(p, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, 10, Brushes.White), new Point(ToX(a) + 3, h - 16));
        }
        var thin = new Pen(new SolidColorBrush(Color.Parse("#26FFFFFF")), 1); var strong = new Pen(new SolidColorBrush(Color.Parse("#99D4A84B")), 1.2);
        var every = Bars.Count > 0 && w / Bars.Count < 14 ? 8 : 4;
        foreach (var bar in Bars)
        {
            var x = ToX(bar.Start); var big = (bar.Number - 1) % every == 0;
            g.DrawLine(big ? strong : thin, new Point(x, 0), new Point(x, h - 18));
            if (big) g.DrawText(new FormattedText(bar.Number.ToString(), System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, 10, new SolidColorBrush(Color.Parse("#CCD4A84B"))), new Point(x + 3, 2));
        }
        if (Playhead >= 0)
        {
            var x = ToX(Playhead); g.DrawLine(new Pen(Brushes.White, 2), new Point(x, 0), new Point(x, h));
            if (Bars.LastOrDefault(b => b.Start <= Playhead) is { } now) g.FillRectangle(new SolidColorBrush(Color.Parse("#33F1C870")), new Rect(ToX(now.Start), 0, Math.Max(1, ToX(now.End) - ToX(now.Start)), h - 18));
        }
    }
}
