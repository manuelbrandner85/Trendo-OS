using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Trendo.Core.Media;

namespace Trendo.App;

/// <summary>Zeitleiste des Videoschnitts: Zeitlineal, Bildspur (Stücke lückenlos mit Vorschaubildern), Musikspur (frei platziert), Abspielkopf.
/// Ziehen im Lineal oder auf leerer Fläche bewegt den Abspielkopf; Stück ziehen ordnet um (Bildspur) bzw. verschiebt (Musik);
/// Kanten ziehen kürzt; Klick wählt aus.</summary>
public sealed class EditTimeline : Control
{
    public IReadOnlyList<EditClip> Clips { get; set; } = [];
    public IReadOnlyList<EditClip> Music { get; set; } = [];
    public IReadOnlyDictionary<string, IReadOnlyList<Bitmap>> Thumbs { get; set; } = new Dictionary<string, IReadOnlyList<Bitmap>>();
    public double PxPerSec { get; set; } = 40;
    /// <summary>K1 (07.11.2026): echte Wellenform je Tondatei – kleinster/größter Wert, <see cref="WaveRate"/> Spalten je Sekunde. Fehlt sie, zeigt die Spur nur eine Mittellinie (nichts Erfundenes).</summary>
    public IReadOnlyDictionary<string, (float Min, float Max)[]> Waves { get; set; } = new Dictionary<string, (float, float)[]>();
    public const int WaveRate = 50;
    public double Playhead { get; set; }
    /// <summary>Marken aus Trendo Musik: Abschnitte (gold), Liedzeilen nach Stimme (Sängerin rosa, Sänger blau, beide gold), Einstellungen (weiß).</summary>
    public IReadOnlyList<EditMarker> Markers { get; set; } = [];
    public (string Track, int Index)? Selected { get; set; }
    public Action<double>? PlayheadChanged { get; set; }
    public Action<string, int>? ClipSelected { get; set; }
    public Action<int, int>? ClipMoved { get; set; }
    public Action<string, int, double, double>? ClipTrimmed { get; set; }
    public Action<int, double>? MusicMoved { get; set; }
    // Pilot 4.0, V2 und V4: weitere Spuren und Werkzeuge
    public IReadOnlyList<VideoPro.Overlay> Overlays { get; set; } = [];
    public int VideoLaneCount { get; set; } = 1;
    public int AudioLaneCount { get; set; } = 1;
    /// <summary>„auswahl“, „slip“ oder „roll“.</summary>
    public string Tool { get; set; } = "auswahl";
    public bool SnapOn { get; set; }
    public Action<int, double, int>? MusicLaneMoved { get; set; }
    public Action<int, double, int>? OverlayMoved { get; set; }
    public Action<int, double, double>? OverlayTrimmed { get; set; }
    public Action<int>? OverlaySelected { get; set; }
    public Action<int, double>? ClipSlipped { get; set; }
    public Action<int, double>? CutRolled { get; set; }

    public const double Ruler = 22, VideoTop = 28, VideoH = 64, MusicTop = 100, MusicH = 38, Left = 8;
    static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");
    public const double LaneH = 34, LaneGap = 4, RowsTop = MusicTop + MusicH + 6;
    enum Mode { None, Scrub, Move, TrimIn, TrimOut, Slip, Roll }
    Mode _mode; string _track = ""; int _index = -1; Point _from, _at; bool _dragged;

    bool ShowVideoRows => Overlays.Count > 0 || VideoLaneCount > 1;
    /// <summary>Zusätzliche Zeilen unter der Musikspur: weitere Tonspuren, dann die Einblendungsspuren.</summary>
    public IEnumerable<(string Kind, int Lane, double Y)> ExtraRows()
    {
        var y = RowsTop;
        for (var a = 1; a < AudioLaneCount; a++) { yield return ("audio", a, y); y += LaneH + LaneGap; }
        if (ShowVideoRows) for (var v = 0; v < VideoLaneCount; v++) { yield return ("video", v, y); y += LaneH + LaneGap; }
    }
    double ContentH => ExtraRows().Select(r => r.Y + LaneH + 10).DefaultIfEmpty(MusicTop + MusicH + 10).Max();
    /// <summary>Nach dem Setzen der Spuren aufrufen: passt die Höhe an.</summary>
    public void LayoutLanes() => Height = ContentH;
    double LaneY(string kind, int lane) => kind == "audio" && lane == 0 ? MusicTop : ExtraRows().Where(r => r.Kind == kind && r.Lane == lane).Select(r => r.Y).DefaultIfEmpty(MusicTop).First();
    int LaneAt(string kind, double y)
    {
        var rows = ExtraRows().Where(r => r.Kind == kind).Select(r => (r.Lane, Top: r.Y, H: LaneH)).ToList();
        if (kind == "audio") rows.Insert(0, (0, MusicTop, MusicH));
        return rows.Count == 0 ? 0 : rows.OrderBy(r => Math.Abs(r.Top + r.H / 2 - y)).First().Lane;
    }
    double SnapTime(double t, Func<double, bool> own)
    {
        if (!SnapOn) return t;
        var pts = new List<double> { 0, Playhead };
        var s = VideoEdit.Starts([.. Clips]);
        for (var i = 0; i < Clips.Count; i++) { pts.Add(s[i]); pts.Add(s[i] + Clips[i].Length); }
        foreach (var m in Music) { pts.Add(m.Start); pts.Add(m.Start + m.Length); }
        foreach (var o in Overlays) { pts.Add(o.Start); pts.Add(o.Start + o.Length); }
        pts.AddRange(Markers.Select(m => m.Start));
        return VideoTools.Snap(t, pts.Where(x => !own(x)), 8 / PxPerSec);
    }

    public EditTimeline() { ClipToBounds = true; Focusable = true; Height = MusicTop + MusicH + 10; }

    double Total => Math.Max(Math.Max(VideoEdit.VideoLength([.. Clips]), Music.Select(m => m.Start + m.Length).DefaultIfEmpty(0).Max()), Overlays.Select(o => o.Start + o.Length).DefaultIfEmpty(0).Max());
    protected override Size MeasureOverride(Size available) => new(Math.Max(double.IsInfinity(available.Width) ? 600 : available.Width, Left * 2 + (Total + 5) * PxPerSec), ContentH);
    public double X(double t) => Left + t * PxPerSec;
    public double T(double x) => Math.Max(0, (x - Left) / PxPerSec);

    /// <summary>Rechteck eines Stücks (Spur „clips“ oder „music“).</summary>
    public Rect BoxOf(string track, int i)
    {
        if (track == "music") { var m = Music[i]; return new Rect(X(m.Start), LaneY("audio", m.Lane), Math.Max(4, m.Length * PxPerSec), m.Lane == 0 ? MusicH : LaneH); }
        if (track == "overlay") { var o = Overlays[i]; return new Rect(X(o.Start), LaneY("video", o.Lane), Math.Max(4, o.Length * PxPerSec), LaneH); }
        var s = VideoEdit.Starts([.. Clips])[i]; return new Rect(X(s), VideoTop, Math.Max(4, Clips[i].Length * PxPerSec), VideoH);
    }
    (string Track, int Index, Mode Mode)? HitTest(Point p)
    {
        for (var i = 0; i < Clips.Count; i++) { var r = BoxOf("clips", i); if (r.Contains(p)) return ("clips", i, p.X < r.X + 7 ? Mode.TrimIn : p.X > r.Right - 7 ? Mode.TrimOut : Mode.Move); }
        for (var i = 0; i < Music.Count; i++) { var r = BoxOf("music", i); if (r.Contains(p)) return ("music", i, p.X < r.X + 7 ? Mode.TrimIn : p.X > r.Right - 7 ? Mode.TrimOut : Mode.Move); }
        for (var i = 0; i < Overlays.Count; i++) { var r = BoxOf("overlay", i); if (r.Contains(p)) return ("overlay", i, p.X < r.X + 7 ? Mode.TrimIn : p.X > r.Right - 7 ? Mode.TrimOut : Mode.Move); }
        return null;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e); Focus();
        var p = e.GetPosition(this); _from = _at = p; _dragged = false;
        if (p.Y > Ruler && HitTest(p) is { } h)
        {
            (_track, _index, _mode) = h;
            if (_track == "clips" && Tool == "slip") _mode = Mode.Slip;
            else if (_track == "clips" && Tool == "roll")
            {
                _mode = Mode.Roll;
                if (p.X < BoxOf("clips", _index).Center.X) _index--; // Schnitt links vom Stück
                if (_index < 0 || _index + 1 >= Clips.Count) { _mode = Mode.Scrub; Playhead = T(p.X); }
            }
        }
        else { _mode = Mode.Scrub; Playhead = T(p.X); InvalidateVisual(); }
        e.Pointer.Capture(this); e.Handled = true;
    }
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var p = e.GetPosition(this);
        if (_mode == Mode.None) { Cursor = HitTest(p) is { Mode: Mode.TrimIn or Mode.TrimOut } ? new Cursor(StandardCursorType.SizeWestEast) : Cursor.Default; return; }
        _at = p; if (Math.Abs(_at.X - _from.X) > 4 || (_track != "clips" && _mode == Mode.Move && Math.Abs(_at.Y - _from.Y) > 4)) _dragged = true;
        if (_mode == Mode.Scrub) Playhead = T(p.X);
        InvalidateVisual();
    }
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e); e.Pointer.Capture(null);
        var mode = _mode; _mode = Mode.None; var dt = (_at.X - _from.X) / PxPerSec;
        if (mode == Mode.Scrub) { PlayheadChanged?.Invoke(Math.Round(Playhead, 2)); return; }
        if (!_dragged) { if (_track == "overlay") OverlaySelected?.Invoke(_index); else ClipSelected?.Invoke(_track, Math.Max(0, _index)); return; }
        if (mode == Mode.Slip) { ClipSlipped?.Invoke(_index, -dt); return; }
        if (mode == Mode.Roll) { CutRolled?.Invoke(_index, dt); return; }
        if (_track == "overlay")
        {
            var o = Overlays[_index];
            switch (mode)
            {
                case Mode.Move: OverlayMoved?.Invoke(_index, Math.Max(0, Math.Round(SnapTime(o.Start + dt, x => x == o.Start || x == o.Start + o.Length), 2)), LaneAt("video", _at.Y)); break;
                case Mode.TrimIn: { var s = Math.Clamp(o.Start + dt, 0, o.Start + o.Length - 0.2); OverlayTrimmed?.Invoke(_index, Math.Round(s, 2), Math.Round(o.Start + o.Length - s, 2)); break; }
                case Mode.TrimOut: OverlayTrimmed?.Invoke(_index, o.Start, Math.Round(Math.Max(0.2, o.Length + dt), 2)); break;
            }
            return;
        }
        var list = _track == "music" ? Music : Clips; var c = list[_index];
        switch (mode)
        {
            case Mode.Move when _track == "clips":
                // Ziel: Stück, über dessen Mitte die Mitte des gezogenen Stücks liegt
                var center = BoxOf("clips", _index).Center.X + (_at.X - _from.X); var to = 0; var acc = 0.0;
                var starts = VideoEdit.Starts([.. Clips]);
                for (var i = 0; i < Clips.Count; i++) if (i != _index && center > X(starts[i] + Clips[i].Length / 2)) to++; // Mitten in der sichtbaren Anordnung
                if (to != _index) ClipMoved?.Invoke(_index, to); else InvalidateVisual();
                break;
            case Mode.Move:
            {
                var start = Math.Max(0, Math.Round(SnapTime(c.Start + dt, x => x == c.Start || x == c.Start + c.Length), 2));
                var lane = LaneAt("audio", _at.Y);
                if (lane != c.Lane && MusicLaneMoved is not null) MusicLaneMoved(_index, start, lane); else MusicMoved?.Invoke(_index, start);
                break;
            }
            case Mode.TrimIn:
                ClipTrimmed?.Invoke(_track, _index, Math.Round(c.In + dt, 2), c.Out); // bei Musik rückt der Start mit (im Programm), das Ende bleibt stehen
                break;
            case Mode.TrimOut: ClipTrimmed?.Invoke(_track, _index, c.In, Math.Round(c.Out + dt, 2)); break;
        }
    }

    public override void Render(DrawingContext ctx)
    {
        var w = Bounds.Width; var h = Bounds.Height;
        ctx.FillRectangle(new SolidColorBrush(Color.Parse("#FF0B1424")), new Rect(0, 0, w, h));
        var muted = new SolidColorBrush(Color.Parse("#8AA0BD")); var font = new Typeface("Inter"); var bold = new Typeface("Inter", FontStyle.Normal, FontWeight.SemiBold);
        // Lineal
        var step = PxPerSec >= 60 ? 1 : PxPerSec >= 20 ? 5 : PxPerSec >= 8 ? 10 : 30;
        for (var t = 0; X(t) < w; t += step)
        {
            ctx.DrawLine(new Pen(muted, 1), new Point(X(t), Ruler - 6), new Point(X(t), Ruler));
            ctx.DrawText(new FormattedText(TimeSpan.FromSeconds(t).ToString(@"m\:ss"), De, FlowDirection.LeftToRight, font, 10, muted), new Point(X(t) + 2, 3));
        }
        ctx.FillRectangle(new SolidColorBrush(Color.Parse("#14FFFFFF")), new Rect(0, VideoTop, w, VideoH));
        ctx.FillRectangle(new SolidColorBrush(Color.Parse("#0EFFFFFF")), new Rect(0, MusicTop, w, MusicH));
        foreach (var row in ExtraRows()) ctx.FillRectangle(new SolidColorBrush(Color.Parse(row.Kind == "audio" ? "#0EFFFFFF" : "#12FFFFFF")), new Rect(0, row.Y, w, LaneH));
        foreach (var m in Markers)
        {
            if (m.Kind == "section")
            {
                var gold = new SolidColorBrush(Color.Parse("#C9A227"));
                ctx.DrawLine(new Pen(new SolidColorBrush(Color.Parse("#66C9A227")), 1, dashStyle: DashStyle.Dash), new Point(X(m.Start), Ruler), new Point(X(m.Start), h));
                ctx.DrawText(new FormattedText(m.Label, De, FlowDirection.LeftToRight, bold, 10, gold), new Point(X(m.Start) + 3, MusicTop + MusicH - 13));
            }
            else if (m.Kind == "line")
            {
                var col = m.Who == "beide" ? "#C9A227" : m.Who.StartsWith("Sängerin") ? "#F472B6" : m.Who.StartsWith("Sänger") ? "#60A5FA" : "#94A3B8";
                ctx.FillRectangle(new SolidColorBrush(Color.Parse(col)), new Rect(X(m.Start), MusicTop - 6, Math.Max(2, X(m.End) - X(m.Start)), 4));
            }
            else if (m.Kind == "shot")
                ctx.DrawLine(new Pen(new SolidColorBrush(Color.Parse("#55FFFFFF")), 1), new Point(X(m.Start), Ruler - 4), new Point(X(m.Start), Ruler + 3));
            else if (m.Kind == "user") // X5: eigene Marke (Taste M) – Dreieck am Lineal
            {
                var x0 = X(m.Start); var geo = new StreamGeometry();
                using (var g = geo.Open()) { g.BeginFigure(new Point(x0 - 5, 0), true); g.LineTo(new Point(x0 + 5, 0)); g.LineTo(new Point(x0, 8)); g.EndFigure(true); }
                ctx.DrawGeometry(new SolidColorBrush(Color.Parse("#22D3EE")), null, geo);
                ctx.DrawLine(new Pen(new SolidColorBrush(Color.Parse("#5522D3EE")), 1), new Point(x0, 8), new Point(x0, h));
            }
        }
        // Bildspur
        for (var i = 0; i < Clips.Count; i++)
        {
            var r = BoxOf("clips", i); var c = Clips[i]; var sel = Selected is ("clips", var si) && si == i;
            if (_dragged && _track == "clips" && _index == i) r = _mode switch
            {
                Mode.Move => r.Translate(new Vector(_at.X - _from.X, -4)),
                Mode.TrimIn => new Rect(r.X + (_at.X - _from.X), r.Y, Math.Max(4, r.Width - (_at.X - _from.X)), r.Height),
                Mode.TrimOut => r.WithWidth(Math.Max(4, r.Width + (_at.X - _from.X))),
                _ => r,
            };
            using (ctx.PushClip(new RoundedRect(r.Deflate(1), 6)))
            {
                ctx.FillRectangle(new SolidColorBrush(Color.Parse(c.IsImage ? "#FF7C3AED" : "#FF1D4ED8")), r);
                if (Thumbs.TryGetValue(c.Id, out var th) && th.Count > 0)
                {
                    var tw = VideoH * 16 / 9; var n = Math.Max(1, (int)Math.Ceiling(r.Width / tw));
                    for (var k = 0; k < n; k++) { var b = th[Math.Min(th.Count - 1, k * th.Count / n)]; ctx.DrawImage(b, new Rect(r.X + k * tw, r.Y, tw, r.Height)); }
                    ctx.FillRectangle(new SolidColorBrush(Color.Parse("#66000000")), new Rect(r.X, r.Y, r.Width, 16));
                }
                ctx.DrawText(new FormattedText($"{System.IO.Path.GetFileName(c.File)} · {c.Length:0.0} s" + (c.Title.Length > 0 ? $" · T „{c.Title}“" : ""), De, FlowDirection.LeftToRight, bold, 11, Brushes.White), new Point(r.X + 6, r.Y + 1));
            }
            ctx.DrawRectangle(null, new Pen(new SolidColorBrush(Color.Parse(sel ? "#FFFACC15" : "#88FFFFFF")), sel ? 2.5 : 1), new RoundedRect(r.Deflate(1), 6));
            ctx.FillRectangle(new SolidColorBrush(Color.Parse("#CCFFFFFF")), new Rect(r.X + 2, r.Y + VideoH / 2 - 8, 3, 16)); ctx.FillRectangle(new SolidColorBrush(Color.Parse("#CCFFFFFF")), new Rect(r.Right - 5, r.Y + VideoH / 2 - 8, 3, 16));
        }
        // Überblendungen: Keil über der Überlappung
        for (var i = 0; i + 1 < Clips.Count; i++)
        {
            var fd = VideoEdit.FadeOf([.. Clips], i); if (fd <= 0) continue;
            var r2 = BoxOf("clips", i + 1); var w0 = fd * PxPerSec;
            ctx.DrawGeometry(new SolidColorBrush(Color.Parse("#99FFFFFF")), null, new PolylineGeometry([new Point(r2.X, r2.Bottom - 2), new Point(r2.X + w0, r2.Y + 18), new Point(r2.X + w0, r2.Bottom - 2)], true));
            ctx.DrawText(new FormattedText($"⤫ {fd:0.#} s", De, FlowDirection.LeftToRight, font, 10, Brushes.White), new Point(r2.X + 2, r2.Bottom - 14));
        }
        // Musikspur mit echter Wellenform (K1)
        for (var i = 0; i < Music.Count; i++)
        {
            var r = BoxOf("music", i); var m = Music[i]; var sel = Selected is ("music", var si) && si == i;
            if (_dragged && _track == "music" && _index == i) r = _mode switch
            {
                Mode.Move => r.Translate(new Vector(_at.X - _from.X, _at.Y - _from.Y)),
                Mode.TrimIn => new Rect(r.X + (_at.X - _from.X), r.Y, Math.Max(4, r.Width - (_at.X - _from.X)), r.Height),
                Mode.TrimOut => r.WithWidth(Math.Max(4, r.Width + (_at.X - _from.X))),
                _ => r,
            };
            ctx.DrawRectangle(new SolidColorBrush(Color.Parse("#FF047857")), new Pen(new SolidColorBrush(Color.Parse(sel ? "#FFFACC15" : "#8834D399")), sel ? 2.5 : 1), new RoundedRect(r.Deflate(1), 6));
            var wave = new Pen(new SolidColorBrush(Color.Parse("#CC6EE7B7")), 1.1);
            if (Waves.TryGetValue(m.File, out var pk) && pk.Length > 0)
            {
                var half = r.Height / 2 - 5;
                for (var x = r.X + 2; x < r.Right - 2; x += 2)
                {
                    var t = m.In + (x - r.X) / PxPerSec; var k = (int)(t * WaveRate); if (k < 0 || k >= pk.Length) continue;
                    var (lo, hi) = pk[k]; var vol = Math.Min(1.5, m.Volume);
                    ctx.DrawLine(wave, new Point(x, r.Center.Y - Math.Min(half, hi * half * vol)), new Point(x, r.Center.Y - Math.Max(-half, lo * half * vol)));
                }
            }
            else ctx.DrawLine(wave, new Point(r.X + 4, r.Center.Y), new Point(r.Right - 4, r.Center.Y));
            ctx.DrawText(new FormattedText($"♪ {System.IO.Path.GetFileName(m.File)} · {m.Volume * 100:0} %", De, FlowDirection.LeftToRight, bold, 11, Brushes.White), new Point(r.X + 6, r.Y + 2));
        }
        // Einblendungsspuren
        for (var i = 0; i < Overlays.Count; i++)
        {
            var r = BoxOf("overlay", i); var o = Overlays[i];
            if (_dragged && _track == "overlay" && _index == i && _mode == Mode.Move) r = r.Translate(new Vector(_at.X - _from.X, _at.Y - _from.Y));
            ctx.DrawRectangle(new SolidColorBrush(Color.Parse("#FF9333EA")), new Pen(new SolidColorBrush(Color.Parse("#88D8B4FE")), 1), new RoundedRect(r.Deflate(1), 6));
            ctx.DrawText(new FormattedText(System.IO.Path.GetFileName(o.File), De, FlowDirection.LeftToRight, bold, 11, Brushes.White), new Point(r.X + 6, r.Y + 8));
        }
        // Abspielkopf
        var px = X(Playhead); var red = new SolidColorBrush(Color.Parse("#EF4444"));
        ctx.DrawLine(new Pen(red, 2), new Point(px, 0), new Point(px, h));
        ctx.DrawGeometry(red, null, new PolylineGeometry([new Point(px - 6, 0), new Point(px + 6, 0), new Point(px, 9)], true));
    }
}
