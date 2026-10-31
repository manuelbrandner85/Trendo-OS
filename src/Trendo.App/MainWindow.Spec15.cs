using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Threading;
using Trendo.Core;

namespace Trendo.App;

/// <summary>SPEC-15 / T-0267: Sendung & Streaming als Programm „Sendeablauf“ – bearbeitbare Ablauftabelle mit Zeitleiste, On-Air-Uhr mit Countdown,
/// Live senden (ffmpeg), Weitergabe (CSV, Kapitel, Moderationskarten, OBS) und Rechner, die sofort rechnen. Ersetzt die Formularseite.</summary>
public partial class MainWindow
{
    private BroadcastDesk? _bc;
    public BroadcastDesk Broadcast => _bc ??= new BroadcastDesk(_core.DataRoot);

    private void InitSpec15()
    {
        RegisterTab(FachTabs, "sendung", "Sendeablauf", "SPEC-15", BroadcastView, "sendeablauf", "rundown", "on air", "streaming", "stream", "livestream", "obs", "sendung", "ablauf", "podcast", "youtube", "twitch", "bitrate", "kapitel");
        StartLiveTimer();
    }

    // ---------- T-0267: Programm „Sendeablauf“ (Vorbild Rundown-Planer und OBS, nur Funktionsumfang) ----------
    public string BcMode = "ablauf";
    private string _bcMsg = "";
    private DispatcherTimer? _bcClock;
    private TextBlock? _bcNowTitle, _bcNowLeft, _bcNowNext, _bcNowShow; private ProgressBar? _bcNowBar;
    /// <summary>Für Tests: Uhrzeit der On-Air-Uhr (null = jetzt).</summary>
    public DateTime? BcClockOverride { get; set; }

    void BcRun(Action a) { try { a(); } catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException or FormatException or UnauthorizedAccessException) { _bcMsg = "⚠ " + ex.Message; } Show("sendung"); }

    /// <summary>On-Air-Uhr nachziehen: was läuft, wie lange noch, was kommt.</summary>
    public void BcTick()
    {
        if (_bcNowTitle is null) return;
        var t = BcClockOverride ?? DateTime.Now; var n = BroadcastPlus.NowPlaying(Broadcast, t); var rows = Broadcast.Rows();
        if (n is null)
        {
            _bcNowTitle.Text = rows.Count == 0 ? "Noch kein Ablauf" : t < Broadcast.Plan.Start ? "Noch nicht auf Sendung" : "Sendung beendet";
            _bcNowLeft!.Text = rows.Count > 0 && t < Broadcast.Plan.Start ? "−" + BroadcastDesk.Clock(Broadcast.Plan.Start - t) : "–";
            _bcNowNext!.Text = rows.Count > 0 && t < Broadcast.Plan.Start ? $"Als Erstes: {rows[0].Title}" : ""; _bcNowShow!.Text = ""; _bcNowBar!.Value = 0; return;
        }
        var seg = rows[n.Index]; var len = Math.Max(1, seg.Seconds);
        _bcNowTitle.Text = $"{n.Index + 1}. {n.Title}"; _bcNowLeft!.Text = BroadcastDesk.Clock(n.LeftInSegment);
        _bcNowLeft.Foreground = Brush.Parse(n.LeftInSegment.TotalSeconds <= 10 ? "#F87171" : n.LeftInSegment.TotalSeconds <= 30 ? "#FACC15" : "#FFFFFF");
        _bcNowNext!.Text = n.Next is null ? "Danach: Ende der Sendung" : $"Danach: {n.Next}"; _bcNowShow!.Text = $"Sendung noch {BroadcastDesk.Clock(n.LeftInShow)}";
        _bcNowBar!.Value = 100 * (1 - n.LeftInSegment.TotalSeconds / len);
    }

    private Control BroadcastView()
    {
        Button Btn(string name, string label, Action go, bool accent = false) { var b = new Button { Name = name, Content = label, Padding = new Thickness(10, 4), Margin = new Thickness(0, 0, 4, 4) }; if (accent) { b.Background = Accent; b.Foreground = Brushes.White; } b.Click += (_, _) => go(); return b; }
        // Kopf: Modus und Sendung
        var bar = new WrapPanel { Name = "bc-bar" };
        foreach (var (id, label) in new[] { ("ablauf", "Ablauf"), ("onair", "On-Air-Uhr") })
        {
            var mid = id; var b = Btn("bc-mode-" + id, label, () => { BcMode = mid; Show("sendung"); }); if (BcMode == id) { b.Background = Accent; b.Foreground = Brushes.White; }
            bar.Children.Add(b);
        }
        TextBox Head(string name, string text, double w, Action<string> set) { var t = new TextBox { Name = name, Text = text, Width = w, Margin = new Thickness(6, 0, 0, 4) }; void C() { if ((t.Text ?? "") != text) BcRun(() => { set(t.Text ?? ""); Broadcast.Save(); }); } t.LostFocus += (_, _) => C(); OnEnter(t, C); return t; }
        bar.Children.Add(new TextBlock { Text = "Sendung", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 4) });
        bar.Children.Add(Head("bc-show", Broadcast.Plan.Show, 180, v => Broadcast.Plan.Show = v.Trim()));
        bar.Children.Add(new TextBlock { Text = "Beginn", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 4) });
        bar.Children.Add(Head("bc-start", Broadcast.Plan.Start.ToString("dd.MM.yyyy HH:mm"), 140, v => Broadcast.Plan.Start = DateTime.TryParseExact(v.Trim(), "dd.MM.yyyy HH:mm", Fach.De, System.Globalization.DateTimeStyles.None, out var st) ? st : throw new FormatException("Beginn bitte als „21.10.2026 20:00“.")));
        bar.Children.Add(new TextBlock { Text = "Sendeplatz (min)", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 4) });
        bar.Children.Add(Head("bc-slot", Broadcast.Plan.SlotMinutes.ToString(), 60, v => Broadcast.Plan.SlotMinutes = int.TryParse(v.Trim(), out var m) && m > 0 ? m : throw new FormatException("Sendeplatz bitte in ganzen Minuten.")));
        bar.Children.Add(Btn("bc-start-now", "Beginn = jetzt", () => BcRun(() => { var n = DateTime.Now; Broadcast.Plan.Start = new DateTime(n.Year, n.Month, n.Day, n.Hour, n.Minute, 0); Broadcast.Save(); BcMode = "onair"; })));
        if (_bcMsg.Length == 0 && _w45Msg.Length > 0) bar.Children.Add(new TextBlock { Name = "bc-app-msg", Text = _w45Msg, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 4), TextWrapping = TextWrapping.Wrap, MaxWidth = 520, FontSize = 12, Foreground = Brush.Parse(_w45Msg.StartsWith('⚠') ? "#F87171" : "#7FD3FF") }); // Meldungen von „In OBS öffnen“
        if (_bcMsg.Length > 0) bar.Children.Add(new TextBlock { Name = "bc-msg", Text = _bcMsg, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 4), TextWrapping = TextWrapping.Wrap, MaxWidth = 520, FontSize = 12, Foreground = Brush.Parse(_bcMsg.StartsWith('⚠') ? "#F87171" : "#4ADE80") });

        Control main = BcMode == "onair" ? BcOnAir() : BcRundown();
        var mid2 = new Border { Name = "bc-main", CornerRadius = new CornerRadius(14), Background = TileBg, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), Padding = new Thickness(14), Child = main };
        var right = new Border { Name = "bc-side", Width = 360, CornerRadius = new CornerRadius(14), Background = TileBg, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), Padding = new Thickness(12), Child = new ScrollViewer { Content = BcSide() } };
        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("*,12,Auto"), Children = { mid2, Pos(right, 2) } };
        var root = new DockPanel { Name = "bc-root" };
        DockPanel.SetDock(bar, Avalonia.Controls.Dock.Top); root.Children.Add(bar); root.Children.Add(body);
        if (BcMode == "onair")
        {
            _bcClock ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _bcClock.Tick -= BcClockTick; _bcClock.Tick += BcClockTick; _bcClock.Start();
        }
        else _bcClock?.Stop();
        return FitPage("Sendeablauf", new Panel(), root);
    }
    void BcClockTick(object? s, EventArgs e) { if (CurrentView != "sendung" || BcMode != "onair") { _bcClock?.Stop(); return; } BcTick(); }

    Control BcRundown()
    {
        var sp = new DockPanel();
        var over = Broadcast.Over; var rows = Broadcast.Rows();
        var head = new StackPanel { Spacing = 6 };
        head.Children.Add(new TextBlock { Name = "bc-total", Text = $"{Broadcast.Plan.Show}: {BroadcastDesk.Clock(Broadcast.Total)} von {Broadcast.Plan.SlotMinutes} min" + (over > TimeSpan.Zero ? $" – {BroadcastDesk.Clock(over)} zu lang!" : $" – noch {BroadcastDesk.Clock(-over)} frei"), Foreground = over > TimeSpan.Zero ? Brush.Parse("#F87171") : Brush.Parse("#4ADE80"), FontSize = 14, FontWeight = FontWeight.SemiBold });
        // Zeitleiste: Programmpunkte nebeneinander, Ende des Sendeplatzes als Linie
        var slot = Broadcast.Plan.SlotMinutes * 60.0; var total = Broadcast.Total.TotalSeconds; var span = Math.Max(slot, total);
        string[] cols = ["#2F86F6", "#22B35E", "#F08A3C", "#7C4DFF", "#E5484D", "#14B8A6", "#EAB308"];
        head.Children.Add(new Border { Name = "bc-timeline", Height = 40, CornerRadius = new CornerRadius(8), ClipToBounds = true, Child = new ShapeView((w, h) =>
        {
            var s = new List<Trendo.Core.Arch.Shape>(); double x = 0;
            for (var i = 0; i < rows.Count; i++)
            {
                var ww = rows[i].Seconds / Math.Max(1, span) * w; var col = cols[i % cols.Length];
                s.Add(new Trendo.Core.Arch.DPoly([(x + 1, 4), (x + ww - 1, 4), (x + ww - 1, h - 4), (x + 1, h - 4)], col, null, 0, "TEIL"));
                if (ww > 40) s.Add(new Trendo.Core.Arch.DText(x + ww / 2, h / 2 + 4, rows[i].Title.Length > (int)(ww / 7) ? rows[i].Title[..Math.Max(1, (int)(ww / 7) - 1)] + "…" : rows[i].Title, 11, "#FFFFFF", "TEXT"));
                x += ww;
            }
            var sx = slot / Math.Max(1, span) * w; s.Add(new Trendo.Core.Arch.DLine(sx - 1, 0, sx - 1, h, total > slot ? "#F87171" : "#FFFFFF", 2, "ENDE", true));
            return s;
        }, "#0E1A2E") });
        DockPanel.SetDock(head, Avalonia.Controls.Dock.Top); sp.Children.Add(head);
        // Tabelle
        var table = new StackPanel { Name = "bc-table", Spacing = 3, Margin = new Thickness(0, 8, 0, 0) };
        var hdr = new Grid { ColumnDefinitions = new ColumnDefinitions("30,80,*,70,*,Auto") };
        foreach (var (t, c) in new[] { ("#", 0), ("Beginn", 1), ("Titel", 2), ("Dauer", 3), ("Hinweis (Kamera, Gast, Einspieler)", 4) }) hdr.Children.Add(Pos(new TextBlock { Text = t, Foreground = Muted, FontSize = 11 }, c));
        table.Children.Add(hdr);
        foreach (var r in rows)
        {
            var i = r.Index; var seg = Broadcast.Plan.Segments[i];
            TextBox Cell(string key, string val, Action<string> set) { var t = new TextBox { Name = $"bc-{key}-{i}", Text = val, Margin = new Thickness(2, 0) }; void C() { if ((t.Text ?? "") != val) BcRun(() => { set(t.Text ?? ""); Broadcast.Save(); }); } t.LostFocus += (_, _) => C(); OnEnter(t, C); return t; }
            Button Small(string name, string label, string tip, Action go) { var b = new Button { Name = name, Content = label, Padding = new Thickness(6, 2), Margin = new Thickness(1, 0) }; ToolTip.SetTip(b, tip); b.Click += (_, _) => BcRun(go); return b; }
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions("30,80,*,70,*,Auto"), Children = {
                new TextBlock { Text = (i + 1).ToString(), Foreground = Muted, VerticalAlignment = VerticalAlignment.Center },
                Pos(new TextBlock { Name = $"bc-row-{i}", Text = r.Start.ToString("HH:mm:ss"), Foreground = Text, VerticalAlignment = VerticalAlignment.Center, FontFamily = new FontFamily("Consolas, monospace") }, 1),
                Pos(Cell("title", seg.Title, v => seg.Title = v.Trim().Length > 0 ? v.Trim() : throw new ArgumentException("Der Titel darf nicht leer sein.")), 2),
                Pos(Cell("dur", BroadcastDesk.Clock(TimeSpan.FromSeconds(seg.Seconds)), v => seg.Seconds = BroadcastDesk.ParseDuration(v)), 3),
                Pos(Cell("note", seg.Note, v => seg.Note = v.Trim()), 4),
                Pos(new StackPanel { Orientation = Orientation.Horizontal, Children = { Small($"bc-up-{i}", "↑", "nach oben", () => Broadcast.Move(i, -1)), Small($"bc-down-{i}", "↓", "nach unten", () => Broadcast.Move(i, 1)), Small($"bc-del-{i}", "✕", "entfernen", () => Broadcast.Remove(i)) } }, 5) } };
            table.Children.Add(g);
        }
        // Neue Zeile
        var nt = new TextBox { Name = "bc-new-title", PlaceholderText = "Neuer Programmpunkt", Margin = new Thickness(2, 0) }; var nd = new TextBox { Name = "bc-new-dur", PlaceholderText = "2:00", Margin = new Thickness(2, 0) }; var nn = new TextBox { Name = "bc-new-note", PlaceholderText = "Hinweis", Margin = new Thickness(2, 0) };
        void Add() => BcRun(() => { var title = string.IsNullOrWhiteSpace(nt.Text) ? throw new ArgumentException("Bitte einen Titel eintragen.") : nt.Text.Trim(); Broadcast.Add(title, BroadcastDesk.ParseDuration(string.IsNullOrWhiteSpace(nd.Text) ? "2:00" : nd.Text), nn.Text?.Trim() ?? ""); _bcMsg = ""; });
        OnEnter(nt, Add); OnEnter(nd, Add); OnEnter(nn, Add);
        var add = new Button { Name = "bc-new-add", Content = "＋", Padding = new Thickness(10, 2), Background = Accent, Foreground = Brushes.White }; add.Click += (_, _) => Add();
        table.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("30,80,*,70,*,Auto"), Margin = new Thickness(0, 6, 0, 0), Children = { Pos(nt, 2), Pos(nd, 3), Pos(nn, 4), Pos(add, 5) } });
        if (rows.Count == 0) table.Children.Add(P("Noch kein Ablauf – oben den ersten Programmpunkt eintragen und Enter drücken.", null, 12));
        sp.Children.Add(new ScrollViewer { Content = table });
        return sp;
    }

    Control BcOnAir()
    {
        _bcNowTitle = new TextBlock { Name = "bc-now-title", Foreground = Text, FontSize = 30, FontWeight = FontWeight.Bold, HorizontalAlignment = HorizontalAlignment.Center, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center };
        _bcNowLeft = new TextBlock { Name = "bc-now-left", Foreground = Brushes.White, FontSize = 96, FontWeight = FontWeight.Bold, FontFamily = new FontFamily("Consolas, monospace"), HorizontalAlignment = HorizontalAlignment.Center };
        _bcNowBar = new ProgressBar { Name = "bc-now-bar", Minimum = 0, Maximum = 100, Height = 10, MaxWidth = 700 };
        _bcNowNext = new TextBlock { Name = "bc-now-next", Foreground = Brush.Parse("#7FD3FF"), FontSize = 20, HorizontalAlignment = HorizontalAlignment.Center };
        _bcNowShow = new TextBlock { Name = "bc-now-show", Foreground = Muted, FontSize = 14, HorizontalAlignment = HorizontalAlignment.Center };
        BcTick();
        return new StackPanel { Spacing = 14, VerticalAlignment = VerticalAlignment.Center, Children = { new TextBlock { Text = "● ON AIR", Foreground = Brush.Parse("#F87171"), FontWeight = FontWeight.Bold, HorizontalAlignment = HorizontalAlignment.Center }, _bcNowTitle, _bcNowLeft, _bcNowBar, _bcNowNext, _bcNowShow } };
    }

    Control BcSide()
    {
        var sp = new StackPanel { Spacing = 6 };
        Button B(string name, string label, Action go) { var b = new Button { Name = name, Content = label, Padding = new Thickness(10, 4), Margin = new Thickness(0, 0, 4, 4) }; b.Click += (_, _) => BcRun(go); return b; }
        sp.Children.Add(LiveSection());
        sp.Children.Add(H("Weitergeben", 12));
        var d = Fach.StoreDir(_core.DataRoot, "sendung");
        sp.Children.Add(new WrapPanel { Children = {
            B("bc-export", "Ablaufplan (CSV)", () => { if (Broadcast.Plan.Segments.Count == 0) throw new InvalidOperationException("Bitte zuerst Programmpunkte anlegen."); var p = Path.Combine(d, "ablaufplan.csv"); File.WriteAllText(p, Broadcast.Sheet()); _bcMsg = "Gespeichert: " + p; NotifyFileResult(_bcMsg); }),
            B("bc-chapters", "YouTube-Kapitel", () => { var ch = Broadcast.Chapters(); var p = Path.Combine(d, "kapitel.txt"); File.WriteAllText(p, ch); _ = Clipboard?.SetTextAsync(ch); _bcMsg = "Kapitelmarken kopiert und gespeichert: " + p; }),
            B("bc-cards", "Moderationskarten", () => { if (Broadcast.Plan.Segments.Count == 0) throw new InvalidOperationException("Bitte zuerst Programmpunkte anlegen."); var p = Path.Combine(d, "moderationskarten.md"); File.WriteAllText(p, BroadcastPlus.CueCards(Broadcast)); _bcMsg = "Gespeichert: " + p; NotifyFileResult(_bcMsg); }),
        } });
        sp.Children.Add(P("OBS-Szenen (eine je Programmpunkt):", null, 11)); sp.Children.Add(new WrapPanel { Children = { OpenInButton("obs", () => ObsCollectionFile(), "sendung") } });
        // Rechner (sofort)
        var calcs = new StackPanel { Spacing = 6 };
        LiveCalc(calcs, "bc-rate", "Reicht meine Leitung?", [("pf", "Plattform", "YouTube", ["YouTube", "Twitch", "Facebook", "Kick"]), ("q", "Qualität", "1080p60", ["720p30", "720p60", "1080p30", "1080p60", "1440p60", "2160p30"]), ("up", "Upload (Mbit/s)", "10", null), ("min", "Aufnahme (min)", Broadcast.Plan.SlotMinutes.ToString(), null)],
            v => { var p = BroadcastDesk.PlanStream(v("pf"), v("q"), Fach.Num(v("up"), "Upload")); return $"{(p.Fits ? "✓" : "⚠")} Video {Fach.F(p.VideoKbps, 0)} + Ton {p.AudioKbps} kbit/s → ca. {Fach.F(p.NeededUploadMbps, 1)} Mbit/s Upload nötig. {p.Advice} Aufnahme ca. {Fach.F(BroadcastDesk.StorageGb(p.VideoKbps + p.AudioKbps, Fach.Num(v("min"), "Minuten")), 1)} GB"; });
        LiveCalc(calcs, "bc-speak", "Sprechzeit", [("t", "Moderationstext", "", null), ("wpm", "Wörter pro Minute", "130", ["110", "130", "160"])],
            v => { var (w, t) = BroadcastPlus.SpeakingTime(v("t"), Fach.Num(v("wpm"))); return $"{w} Wörter → etwa {BroadcastDesk.Clock(t)}"; });
        LiveCalc(calcs, "bc-loud", "Lautheit", [("pf", "Plattform", "YouTube", [.. BroadcastPlus.LoudTargets.Keys]), ("lufs", "Gemessen (LUFS)", "-18", null), ("tp", "Spitze (dBTP)", "-6", null)],
            v => { var l = BroadcastPlus.Loudness(v("pf"), Fach.Num(v("lufs")), Fach.Num(v("tp"))); return $"Ziel {Fach.F(l.Target, 0)} LUFS → {(l.Gain >= 0 ? "+" : "")}{Fach.F(l.Gain, 1)} dB · Spitze danach {Fach.F(l.PeakAfter, 1)} dBTP" + (l.Clips ? " · ⚠ vorher Limiter einsetzen" : ""); });
        LiveCalc(calcs, "bc-crop", "Hochkant-Ausschnitt", [("w", "Breite", "1920", null), ("h", "Höhe", "1080", null), ("ratio", "Ziel", "9:16", ["9:16", "4:5", "1:1"]), ("c", "Lage (%)", "50", null)],
            v => { var r = v("ratio").Split(':'); var c = BroadcastPlus.Crop((int)Fach.Num(v("w")), (int)Fach.Num(v("h")), (int)Fach.Num(r[0]), (int)Fach.Num(r[1]), Fach.Num(v("c"))); return $"Ausschnitt {c.W} × {c.H} px ab x = {c.X}, y = {c.Y} · ffmpeg/OBS: crop={c.W}:{c.H}:{c.X}:{c.Y}"; });
        LiveCalc(calcs, "bc-aspect", "Seitenverhältnis", [("w", "Breite", "1920", null), ("h", "Höhe", "1080", null)], v => { var a = BroadcastPlus.Aspect((int)Fach.Num(v("w")), (int)Fach.Num(v("h"))); return $"{a.W}:{a.H} – {a.Name}"; });
        LiveCalc(calcs, "bc-tc", "Timecode", [("v", "Frames oder hh:mm:ss:ff", "90012", null), ("fps", "Bilder pro Sekunde", "25", ["24", "25", "30", "50", "60"])],
            v => { var f = (int)Fach.Num(v("fps")); var x = v("v").Trim(); return x.Contains(':') ? $"{Fach.F(BroadcastPlus.FromTimecode(x, f), 0)} Frames" : $"Timecode {BroadcastPlus.ToTimecode((long)Fach.Num(x), f)}"; });
        sp.Children.Add(H("Rechner", 12)); sp.Children.Add(calcs);
        // Wochenplan und Untertitel
        sp.Children.Add(H("Wochenplan", 12));
        var week = new TextBox { Name = "bc-week-spec", AcceptsReturn = true, MinHeight = 54, Text = "Mo 20:00 60 Talk\nFr 18:00 45 Musik", TextWrapping = TextWrapping.Wrap };
        sp.Children.Add(P("Eine Sendung pro Zeile: Tag, Beginn, Minuten, Titel", null, 11)); sp.Children.Add(week);
        sp.Children.Add(B("bc-week", "Zwei Wochen planen", () => { var (sl, c) = BroadcastPlus.Weekly(week.Text ?? "", DateTime.Today, 2); var p = Path.Combine(d, "wochenplan.csv"); File.WriteAllText(p, "Datum;Beginn;Ende;Sendung\n" + string.Concat(sl.Select(x => $"{x.Start:dd.MM.yyyy};{x.Start:HH:mm};{x.End:HH:mm};{x.Title}\n"))); _bcMsg = $"{sl.Count} Termine" + (c.Count == 0 ? " ohne Überschneidung" : " · ⚠ " + string.Join(" · ", c)) + $" – gespeichert: {p}"; }));
        sp.Children.Add(H("Untertitel verschieben", 12));
        var srt = Field("bc-srt-path", "SRT-Datei (.srt)", 150); sp.Children.Add(srt);
        var shift = new TextBox { Name = "bc-srt-shift", Text = "0", Width = 70 };
        sp.Children.Add(Row(P("Versatz in Sekunden (− = früher)", Text, 12), shift));
        sp.Children.Add(B("bc-srt", "Kopie verschieben", () => { var f = Need(srt, "die SRT-Datei"); if (!File.Exists(f)) throw new IOException("Diese Datei gibt es nicht."); var (s2, n, p) = BroadcastPlus.ShiftSrt(File.ReadAllText(f), TimeSpan.FromSeconds(Fach.Num(shift.Text ?? "0"))); var o = Path.Combine(Path.GetDirectoryName(f) ?? ".", Path.GetFileNameWithoutExtension(f) + ".versetzt.srt"); File.WriteAllText(o, s2); _bcMsg = $"{n} Untertitel" + (p.Count == 0 ? "" : " · ⚠ " + string.Join(" · ", p)) + $" – gespeichert: {o}"; }));
        return sp;
    }
    /// <summary>T-0203: schreibt die OBS-Szenensammlung zur aktuellen Sendung.</summary>
    private string? ObsCollectionFile()
    {
        if (Broadcast.Plan.Segments.Count == 0) throw new InvalidOperationException("Bitte zuerst Programmpunkte anlegen.");
        var name = "Trendo " + (string.IsNullOrWhiteSpace(Broadcast.Plan.Show) ? "Sendung" : Broadcast.Plan.Show.Trim());
        var f = Path.Combine(Fach.StoreDir(_core.DataRoot, "sendung"), string.Concat(name.Split(Path.GetInvalidFileNameChars())) + ".json");
        File.WriteAllText(f, ObsScenes.Collection(name, Broadcast.Plan.Segments.Select(s => s.Title)));
        return f;
    }
}
