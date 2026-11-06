using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Trendo Musik im DAW-Stil (Wunsch 06.11.2026 „Design je Programm“, Vorbilder Bitwig und Studio One, Clean-Room):
/// Transportleiste oben mit Position in Takten, Tempo, Takt und Tonart; Arrangement des gewählten Lieds mit Abschnitts-Clips, Text-Spur,
/// getrennten Spuren und Abspielmarke; „Im Mischpult öffnen“ legt die Spuren als Mehrspur-Projekt an.</summary>
public partial class MainWindow
{
    ArrangeView? _msArrange; TextBlock? _msBars; string? _msArrangeFile, _msDawFile; double _msDawBpm; string _msDawMeter = "4/4";
    static readonly IBrush DawPanel = Brush.Parse("#FF151A22"), DawEdge = Brush.Parse("#33FFFFFF"), DawLcd = Brush.Parse("#FF0C1220");
    static readonly IBrush DawDigits = Brush.Parse("#FF7FC8FF");

    /// <summary>DAW-Look der Programm-Hülle (Wunsch 06.11.2026 „alle Programme im DAW-Stil“): nur im dunklen Trendo-Stil; hell, Kontrast und die OS-Stile behalten ihr Aussehen.</summary>
    bool DawLook => !OsStyle && _theme is not ("hell" or "kontrast");
    IBrush PaneBg => DawLook ? DawPanel : TileBg;
    IBrush PaneEdge => DawLook ? DawEdge : EdgeLit;
    IBrush GroupBg => DawLook ? DawGroup : TileBg;
    static readonly IBrush DawGroup = Brush.Parse("#FF1C2330"), DawButton = Brush.Parse("#FF232B39");

    /// <summary>Position in Takten „Takt.Schlag“ beim Tempo und Takt des Lieds.</summary>
    static string BarsBeats(double sec, double bpm, string meter)
    {
        if (bpm <= 0) return "–";
        var per = int.TryParse(meter.Split('/')[0], out var m) && m > 0 ? m : 4; var beat = sec * bpm / 60;
        return $"{(int)(beat / per) + 1}.{(int)(beat % per) + 1}";
    }

    /// <summary>Transportleiste wie in einer DAW – ersetzt die Abspielleiste unten (gleiche Knöpfe und Namen, mehr Anzeige).</summary>
    Control MsTransportBar()
    {
        var cur = _queue.Current;
        var song = (cur is null ? null : Songs.All().FirstOrDefault(x => string.Equals(x.File, cur, StringComparison.OrdinalIgnoreCase))) ?? (_msSel is null ? null : Songs.Get(_msSel));
        var bpm = song?.Analysis?.Bpm ?? song?.Bpm ?? Understanding0(song)?.Bpm ?? 0; var meter = song?.Analysis?.Meter ?? song?.TimeSignature ?? "4/4"; var key = song?.Analysis?.Key ?? song?.Key;
        _msDawFile = song?.File; _msDawBpm = bpm; _msDawMeter = meter;
        Button T(string name, string icon, string tip, Action a, bool hot = false)
        {
            var b = new Button { Name = name, Content = icon, Width = 38, Height = 34, Padding = new Thickness(0), FontSize = 15, CornerRadius = new CornerRadius(6), HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
                Background = hot ? Accent : Brush.Parse("#22FFFFFF"), Foreground = hot ? Brushes.White : Text, BorderThickness = new Thickness(0), IsEnabled = cur is not null || name == "ms-player-play" && _msSel is not null };
            ToolTip.SetTip(b, tip); b.Click += (_, _) => a(); return b;
        }
        var pp = T("ms-player-play", _mPaused || cur is null ? "▶" : "⏸", "Abspielen / Pause (Leertaste)", () => { _mPage = MS; if (cur is null && _msSel is not null && Songs.Get(_msSel) is { } s) MsPlay(s); else MusicTogglePause(); }, true);
        var stop = T("ms-transport-stop", "⏹", "Stopp – zurück an den Anfang", () => { _audio?.Seek(0); _mPos = 0; if (!_mPaused) MusicTogglePause(); });
        var back = T("ms-transport-back", "⏮", "An den Anfang", () => { _audio?.Seek(0); _mPos = 0; MusicTick(); });
        Border Lcd(string name, string label, Control value, double w) => new() { Name = name, Width = w, Height = 40, CornerRadius = new CornerRadius(6), Background = DawLcd, BorderBrush = DawEdge, BorderThickness = new Thickness(1), Padding = new Thickness(8, 2),
            Child = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { new TextBlock { Text = label, Foreground = Muted, FontSize = 9.5 }, value } } };
        TextBlock Dig(string name, string text, double size = 15) => new() { Name = name, Text = text, Foreground = DawDigits, FontSize = size, FontFamily = new FontFamily("Consolas, monospace"), FontWeight = FontWeight.SemiBold };
        _mTimeL = Dig("ms-time", Trendo.Core.Media.MusicLibrary.Time(_mPos), 13);
        _mTimeR = new TextBlock { Name = "ms-duration", Text = Trendo.Core.Media.MusicLibrary.Time(_mDur), Foreground = Muted, FontSize = 11, FontFamily = new FontFamily("Consolas, monospace"), VerticalAlignment = VerticalAlignment.Center };
        _mTimeL.VerticalAlignment = VerticalAlignment.Center;
        _msBars = Dig("ms-transport-bars", BarsBeats(_mPos, bpm, meter), 17);
        var pos = Lcd("ms-transport-pos", "TAKT · ZEIT", new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children = { _msBars, _mTimeL, new TextBlock { Text = "/", Foreground = Muted, FontSize = 11, VerticalAlignment = VerticalAlignment.Center }, _mTimeR } }, 210);
        var tempo = Lcd("ms-transport-bpm", "TEMPO", Dig("ms-transport-bpm-v", bpm > 0 ? $"{bpm:0.00}" : "–"), 84);
        var takt = Lcd("ms-transport-meter", "TAKT", Dig("ms-transport-meter-v", meter), 60);
        var ton = Lcd("ms-transport-key", "TONART", Dig("ms-transport-key-v", string.IsNullOrEmpty(key) ? "–" : SongTools.KeyDe(key), 13.5), 104);
        var seek = _mSeek = new Slider { Name = "ms-seek", Minimum = 0, Maximum = double.IsNaN(_mDur) || _mDur <= 0 ? 1 : _mDur, Value = Math.Max(0, _mPos), Margin = new Thickness(8, 0), IsEnabled = cur is not null, VerticalAlignment = VerticalAlignment.Center };
        seek.AddHandler(PointerPressedEvent, (_, _) => _mSeeking = true, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        seek.AddHandler(PointerReleasedEvent, (_, _) => { _mSeeking = false; _mPos = seek.Value; _audio?.Seek(seek.Value); }, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        var title = new TextBlock { Name = "ms-player-title", Text = song?.Title ?? "Nichts ausgewählt", Foreground = Text, FontSize = 13, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center, MaxWidth = 220, TextTrimming = TextTrimming.CharacterEllipsis };
        var master = new TextBlock { Text = song is null ? "" : song.Mastered ? $"● Master {SongProducer.TargetOf(song.MasterTarget).Lufs:0} LUFS".Replace("-", "−") : "○ nicht gemastert", Foreground = song?.Mastered == true ? Brush.Parse("#4ADE80") : Muted, FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto"), ColumnSpacing = 10, Children = {
            new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center, Children = { back, pp, stop } },
            Pos(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { pos, tempo, takt, ton } }, 1),
            Pos(new DockPanel { VerticalAlignment = VerticalAlignment.Center, Children = { Dk(title, Avalonia.Controls.Dock.Left), seek } }, 2),
            Pos(master, 3) } };
        return new Border { Name = "ms-player", CornerRadius = new CornerRadius(12), Background = DawPanel, BorderBrush = DawEdge, BorderThickness = new Thickness(1), BoxShadow = CardShadow, Padding = new Thickness(10, 6), Margin = new Thickness(0, 0, 0, 10), Child = grid };
    }

    SongUnderstanding.Result? Understanding0(MusicSong? s) => s is null ? null : Understanding(s);

    /// <summary>Arrangement des gewählten Lieds (über der Liederliste).</summary>
    Control? MsArrangePanel()
    {
        var s = _msSel is null ? null : Songs.Get(_msSel);
        if (s is null || !File.Exists(s.File)) { _msArrange = null; return null; }
        var u = Understanding(s); var dur = s.Seconds > 0 ? s.Seconds : s.Analysis?.Duration ?? u?.Duration ?? 0;
        if (dur <= 0) { _msArrange = null; return null; }
        var bpm = s.Analysis?.Bpm ?? s.Bpm ?? u?.Bpm ?? 0; var meter = s.Analysis?.Meter ?? s.TimeSignature ?? "4/4";
        var lanes = new List<ArrangeView.Lane>();
        // Abschnitte: gemessen aus „Lied verstehen“, sonst aus dem Liedtext geschätzt
        var plan = s.Lyrics.Trim().Length > 0 && s.Lyrics != "[Instrumental]" ? SongFollow.Plan(s.Lyrics, dur, u) : [];
        var secs = u?.Sections.Select(x => (x.Start, x.End, x.Part)).ToList() ?? [];
        if (secs.Count == 0 && plan.Count > 0)
            foreach (var g in plan.GroupBy(l => l.Part).SelectMany(g => g.Select(l => l)).Aggregate(new List<(double Start, double End, string Part)>(), (acc, l) => { if (acc.Count > 0 && acc[^1].Part == l.Part) acc[^1] = (acc[^1].Start, l.End, l.Part); else acc.Add((l.Start, l.End, l.Part.Length > 0 ? l.Part : "Teil")); return acc; }).OrderBy(x => x.Start))
                secs.Add(g);
        if (secs.Count > 0) lanes.Add(new("Abschnitte", "#F59E0B", [.. secs.Select(x => new ArrangeView.Clip(x.Start, x.End, x.Part))]));
        if (plan.Count > 0) lanes.Add(new("Text" + (plan.All(l => l.Timed) ? "" : " (geschätzt)"), "#38BDF8", [.. plan.Select(l => new ArrangeView.Clip(l.Start, l.End, l.Text))]));
        var (acc, voc) = StemsOf(s);
        if (voc is not null) lanes.Add(new("Gesang", "#F472B6", [new(0, dur, voc.Title)]));
        if (acc is not null) lanes.Add(new("Begleitung", "#A78BFA", [new(0, dur, acc.Title)]));
        lanes.Add(new("Mix", "#34D399", [new(0, dur, s.Title + (s.Mastered ? " · gemastert" : ""))]));
        _msArrangeFile = s.File;
        var view = _msArrange = new ArrangeView { Name = "ms-arrange", Seconds = dur, Bars = SongFollow.Bars(bpm, meter, dur, u?.Beats), Lanes = lanes, Playhead = string.Equals(_queue.Current, s.File, StringComparison.OrdinalIgnoreCase) ? _mPos : -1 };
        view.Seek = t => MsPlayAt(s, t);
        var mix = new Button { Name = "ms-to-mixer", Content = "☰ Im Mischpult öffnen", FontSize = 11.5, Padding = new Thickness(8, 2) };
        ToolTip.SetTip(mix, acc is not null ? "Gesang und Begleitung als Spuren mit Lautstärke, Panorama, Stumm und Solo" : "Das Lied als Spur im Mehrspur-Mischpult – mit „Spuren trennen“ kommen Gesang und Begleitung einzeln");
        mix.Click += (_, _) => { try { MsOpenInMixer(s); } catch (Exception e) when (e is IOException or InvalidOperationException) { _msMsg = e.Message; Show(MS); } };
        return new Border { Name = "ms-arrange-panel", CornerRadius = new CornerRadius(12), Background = DawPanel, BorderBrush = DawEdge, BorderThickness = new Thickness(1), Padding = new Thickness(8), Margin = new Thickness(0, 0, 0, 8),
            Child = new StackPanel { Spacing = 6, Children = {
                new DockPanel { Children = { Dk(mix, Avalonia.Controls.Dock.Right), new TextBlock { Text = $"ARRANGEMENT · {s.Title}", Foreground = Muted, FontSize = 11, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center } } },
                view } } };
    }

    /// <summary>Lied (oder seine getrennten Spuren) als Mehrspur-Projekt im Audio-Programm öffnen.</summary>
    public void MsOpenInMixer(MusicSong s)
    {
        var (acc, voc) = StemsOf(s);
        var files = new[] { voc, acc }.Where(x => x is not null && File.Exists(x.File)).Select(x => x!.File).ToList();
        if (files.Count == 0) files.Add(s.File);
        var d = ProgramNew(AUK, "mehrspur");
        d.Edit("Spuren aus Trendo Musik", x => { foreach (var f in files) Trendo.Core.Media.AudioMulti.AddTrack(x, f, 0); });
        d.Title = s.Title + " – Mischpult";
        _progMsg[AUK] = $"„{s.Title}“ liegt mit {files.Count} Spur{(files.Count == 1 ? "" : "en")} im Mischpult – Lautstärke, Panorama, Stumm und Solo je Spur, dann „Abmischen“.";
        Show(AUK);
    }

    /// <summary>Abspielmarke im Arrangement und Takt-Anzeige nachziehen (aus dem Takt des Players).</summary>
    void MsDawTick()
    {
        if (_msArrange is not null) { _msArrange.Playhead = string.Equals(_queue.Current, _msArrangeFile, StringComparison.OrdinalIgnoreCase) ? _mPos : -1; _msArrange.InvalidateVisual(); }
        if (_msBars is not null && string.Equals(_queue.Current, _msDawFile, StringComparison.OrdinalIgnoreCase)) _msBars.Text = BarsBeats(_mPos, _msDawBpm, _msDawMeter);
    }
}

/// <summary>Arrangement wie in einer DAW: Taktlineal, Spuren mit farbigen Clips, Abspielmarke; Klick setzt die Position.</summary>
public sealed class ArrangeView : Control
{
    public sealed record Clip(double Start, double End, string Label);
    public sealed record Lane(string Name, string Color, IReadOnlyList<Clip> Clips);
    public IReadOnlyList<Lane> Lanes { get; set; } = [];
    public IReadOnlyList<SongFollow.Bar> Bars { get; set; } = [];
    public double Seconds { get; set; }
    public double Playhead { get; set; } = -1;
    public Action<double>? Seek { get; set; }
    const double Head = 96, Ruler = 20, LaneH = 30;

    public ArrangeView() { ClipToBounds = true; Cursor = new Cursor(StandardCursorType.Hand); }
    protected override Size MeasureOverride(Size available) => new(double.IsInfinity(available.Width) ? 600 : available.Width, Ruler + Lanes.Count * (LaneH + 2) + 4);
    double X(double t) => Head + (Seconds <= 0 ? 0 : t / Seconds * Math.Max(1, Bounds.Width - Head));

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e); var p = e.GetPosition(this); if (p.X < Head || Seconds <= 0) return;
        Seek?.Invoke(Math.Clamp((p.X - Head) / Math.Max(1, Bounds.Width - Head), 0, 1) * Seconds); e.Handled = true;
    }

    public override void Render(DrawingContext g)
    {
        var w = Bounds.Width; var h = Bounds.Height;
        g.FillRectangle(new SolidColorBrush(Color.Parse("#0E131B")), new Rect(0, 0, w, h));
        g.FillRectangle(new SolidColorBrush(Color.Parse("#1B2230")), new Rect(Head, 0, w - Head, Ruler));
        var every = Bars.Count > 0 && (w - Head) / Bars.Count < 18 ? 8 : 4;
        foreach (var b in Bars)
        {
            var x = X(b.Start); var big = (b.Number - 1) % every == 0;
            g.DrawLine(new Pen(new SolidColorBrush(Color.Parse(big ? "#33FFFFFF" : "#14FFFFFF")), 1), new Point(x, big ? 0 : Ruler - 5), new Point(x, h));
            if (big) g.DrawText(new FormattedText(b.Number.ToString(), System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, 10, new SolidColorBrush(Color.Parse("#AAB4C3"))), new Point(x + 3, 3));
        }
        for (var i = 0; i < Lanes.Count; i++)
        {
            var ln = Lanes[i]; var y = Ruler + 2 + i * (LaneH + 2); var col = Color.Parse(ln.Color);
            g.FillRectangle(new SolidColorBrush(Color.Parse("#1A202B")), new Rect(0, y, Head - 4, LaneH));
            g.FillRectangle(new SolidColorBrush(col), new Rect(0, y, 3, LaneH));
            g.DrawText(new FormattedText(ln.Name, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold), 11, Brushes.White), new Point(8, y + 8));
            foreach (var c in ln.Clips)
            {
                var r = new Rect(X(c.Start) + 1, y + 2, Math.Max(3, X(c.End) - X(c.Start) - 2), LaneH - 4);
                g.DrawRectangle(new SolidColorBrush(Color.FromArgb(150, col.R, col.G, col.B)), new Pen(new SolidColorBrush(col), 1), r, 4, 4);
                if (r.Width > 30) using (g.PushClip(r)) g.DrawText(new FormattedText(c.Label, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, 10.5, Brushes.White), new Point(r.X + 4, r.Y + 6));
            }
        }
        if (Playhead >= 0) { var x = X(Playhead); g.DrawLine(new Pen(Brushes.White, 1.5), new Point(x, 0), new Point(x, h)); g.FillRectangle(Brushes.White, new Rect(x - 4, 0, 8, 6)); }
    }
}
