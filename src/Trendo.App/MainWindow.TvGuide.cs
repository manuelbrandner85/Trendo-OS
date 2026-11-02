using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core.Tv;

namespace Trendo.App;

/// <summary>Programmführer als Raster (Masterprompt 131): Sender × Zeit mit festem Senderrand, Tage, früher/später, 20:15, Sparten in Farbe,
/// Suche über alle Tage, Jetzt-Linie, Merker für Aufnahme und Erinnerung, Überschneidungen von Aufnahmen.</summary>
public partial class MainWindow
{
    int _tvGuideDay; double? _tvGuideHour; string _tvGuideKind = "Alle", _tvGuideFind = "";
    /// <summary>Gewählter Bereich der TV-Seite (live, guide, rec …) – für Vorschaubilder und Tests.</summary>
    public string TvTab { get => _tvTab; set => _tvTab = value; }
    /// <summary>Sender und Programmführer neu einlesen (nach Änderungen von außen).</summary>
    public void TvReloadData() => TvReload();
    const double GuidePerMin = 4, GuideHours = 6, GuideName = 180, GuideRow = 48;

    Control TvGuide()
    {
        var sp = new StackPanel { Spacing = 8, Name = "tv-guide" };
        var now = DateTime.UtcNow; var progs = TvProgs(); var recs = Tv.Recordings;
        var from = Trendo.Core.Tv.TvGuide.Start(now, _tvGuideDay, _tvGuideHour); var to = from.AddHours(GuideHours);
        var today = now.ToLocalTime().Date; var fromHour = (from.ToLocalTime() - today.AddDays(_tvGuideDay)).TotalHours;
        void Go(int day, double? hour) { _tvGuideDay = day; _tvGuideHour = hour; Show("tv"); }

        Button Chip(string name, string label, bool on, Action a, string? dot = null)
        {
            var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { new TextBlock { Text = label, FontSize = 12, Foreground = on ? Brush.Parse("#1B1406") : Text, VerticalAlignment = VerticalAlignment.Center } } };
            if (dot is not null) content.Children.Insert(0, new Border { Width = 9, Height = 9, CornerRadius = new CornerRadius(5), Background = Brush.Parse(dot), VerticalAlignment = VerticalAlignment.Center });
            var b = new Button { Name = name, Content = content, Padding = new Thickness(11, 5), Margin = new Thickness(0, 0, 6, 6), CornerRadius = new CornerRadius(10),
                Background = on ? Brush.Parse("#D4A84B") : Brush.Parse("#1AFFFFFF"), BorderBrush = Brush.Parse(on ? "#F6D98C" : "#26FFFFFF"), BorderThickness = new Thickness(1) };
            b.Click += (_, _) => a(); Hover(b); return b;
        }

        // Tage
        var days = new WrapPanel { Name = "tv-guide-days" };
        for (var d = 0; d < 7; d++)
        {
            var dd = d; var date = today.AddDays(d);
            days.Children.Add(Chip($"tv-guide-day-{d}", d switch { 0 => "Heute", 1 => "Morgen", _ => date.ToString("ddd dd.MM.", De) }, _tvGuideDay == d, () => Go(dd, dd == 0 ? null : _tvGuideHour)));
        }
        sp.Children.Add(days);
        // Zeit, Gruppe, Suche
        var groups = new List<string> { "Alle", "Favoriten" }; groups.AddRange(TvChannels().Select(c => c.Group).Distinct().OrderBy(g => g));
        var gsel = new ComboBox { Name = "tv-guide-group", ItemsSource = groups, SelectedItem = groups.Contains(_tvGroup) ? _tvGroup : "Alle", Width = 170, CornerRadius = new CornerRadius(10), Margin = new Thickness(0, 0, 6, 6) };
        gsel.SelectionChanged += (_, _) => { if (gsel.SelectedItem is string g && g != _tvGroup) { _tvGroup = g; Show("tv"); } };
        var find = new TextBox { Name = "tv-guide-find", Text = _tvGuideFind, PlaceholderText = "🔎  Sendung suchen (alle Tage) …", Width = 260, CornerRadius = new CornerRadius(10), Margin = new Thickness(0, 0, 6, 6) };
        find.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Enter) { _tvGuideFind = find.Text ?? ""; Show("tv"); } };
        sp.Children.Add(new WrapPanel { Children = {
            Chip("tv-guide-earlier", "◀ 2 Std. früher", false, () => Go(_tvGuideDay, Math.Max(-6, fromHour - 2))),
            Chip("tv-guide-now", "Jetzt", _tvGuideDay == 0 && _tvGuideHour is null, () => Go(0, null)),
            Chip("tv-guide-prime", "20:15", _tvGuideHour == 19.5, () => Go(_tvGuideDay, 19.5)),
            Chip("tv-guide-later", "2 Std. später ▶", false, () => Go(_tvGuideDay, Math.Min(26, fromHour + 2))),
            gsel, find } });
        // Sparten
        var kinds = new WrapPanel { Name = "tv-guide-kinds" };
        kinds.Children.Add(Chip("tv-guide-kind-alle", "Alle Sparten", _tvGuideKind == "Alle", () => { _tvGuideKind = "Alle"; Show("tv"); }));
        foreach (var k in Trendo.Core.Tv.TvGuide.Kinds) { var kk = k; kinds.Children.Add(Chip("tv-guide-kind-" + k.ToLowerInvariant(), k, _tvGuideKind == k, () => { _tvGuideKind = kk; Show("tv"); }, Trendo.Core.Tv.TvGuide.Color(k))); }
        sp.Children.Add(kinds);

        if (progs.Count == 0) sp.Children.Add(P("Noch kein Programmführer geladen. Unter „Quellen“ eine EPG-Adresse (XMLTV) eintragen – bei Xtream-Zugängen geht das von selbst.", Text, 12.5));

        // Suchergebnisse
        if (_tvGuideFind.Trim().Length >= 2)
        {
            var hits = Trendo.Core.Tv.TvGuide.Search(progs, TvChannels(), _tvGuideFind, now);
            var list = new StackPanel { Name = "tv-guide-hits", Spacing = 2 };
            list.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { H(hits.Count == 0 ? $"Nichts gefunden für „{_tvGuideFind}“" : $"{hits.Count} Termine für „{_tvGuideFind}“", 13),
                Pos(Chip("tv-guide-find-clear", "✕ Suche schließen", false, () => { _tvGuideFind = ""; Show("tv"); }), 1) } });
            for (var i = 0; i < hits.Count; i++)
            {
                var (c, p) = hits[i]; var key = $"{c.Id}|{p.Start:O}"; var local = p.Start.ToLocalTime();
                var b = new Button { Name = $"tv-guide-hit-{i}", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(8, 4), Background = Brushes.Transparent, CornerRadius = new CornerRadius(8),
                    Content = new Grid { ColumnDefinitions = new ColumnDefinitions("150,160,*"), Children = {
                        new TextBlock { Text = local.ToString("ddd dd.MM. HH:mm", De), Foreground = p.IsOn(now) ? GoldText() : Muted, FontSize = 12 },
                        Pos(new TextBlock { Text = c.Name, Foreground = Muted, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis }, 1),
                        Pos(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { new Border { Width = 8, Height = 8, CornerRadius = new CornerRadius(4), Background = Brush.Parse(Trendo.Core.Tv.TvGuide.Color(Trendo.Core.Tv.TvGuide.Kind(p))), VerticalAlignment = VerticalAlignment.Center },
                            new TextBlock { Text = p.Title, Foreground = Text, FontSize = 12.5, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis } } }, 2) } } };
                b.Click += (_, _) => { _tvProgSel = key; _tvGuideDay = Math.Max(0, (local.Date - today).Days); _tvGuideHour = Math.Floor(local.Hour + local.Minute / 60.0) - 1; Show("tv"); };
                Hover(b); list.Children.Add(b);
            }
            sp.Children.Add(WCard(list));
        }

        // Raster: links fester Senderrand, rechts Zeit (waagerecht scrollbar), Jetzt-Linie über alle Zeilen
        var width = GuideHours * 60 * GuidePerMin;
        double X(DateTime t) => (t - from).TotalMinutes * GuidePerMin;
        var names = new StackPanel { Name = "tv-guide-channels", Width = GuideName };
        var rows = new StackPanel { Name = "tv-guide-rows" };
        var ruler = new Canvas { Height = 26, Width = width };
        for (var t = from; t < to; t = t.AddMinutes(30))
        {
            var full = t.ToLocalTime().Minute == 0;
            var tb = new TextBlock { Text = t.ToLocalTime().ToString("HH:mm"), Foreground = full ? Text : Muted, FontSize = full ? 12 : 11, FontWeight = full ? FontWeight.SemiBold : FontWeight.Normal };
            Canvas.SetLeft(tb, X(t) + 4); Canvas.SetTop(tb, 4); ruler.Children.Add(tb);
            var tick = new Border { Width = 1, Height = full ? 26 : 10, Background = Brush.Parse(full ? "#40FFFFFF" : "#22FFFFFF") }; Canvas.SetLeft(tick, X(t)); Canvas.SetTop(tick, full ? 0 : 16); ruler.Children.Add(tick);
        }
        rows.Children.Add(ruler);
        names.Children.Add(new TextBlock { Height = 26, Text = from.ToLocalTime().ToString("dddd, dd.MM.", De), Foreground = GoldText(), FontSize = 12, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        var channels = TvFiltered().Where(c => c.EpgId is not null).Take(80).ToList();
        var stripe = 0;
        foreach (var c in channels)
        {
            var bg = Brush.Parse(stripe++ % 2 == 0 ? "#0AFFFFFF" : "#04FFFFFF");
            var logo = new Image { Width = 30, Height = 30, Stretch = Stretch.Uniform }; TvImage(logo, c.Logo);
            var nameBtn = new Button { Name = "tv-guide-ch-" + c.Id.Replace(':', '-'), Height = GuideRow, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(6, 0), CornerRadius = new CornerRadius(0), Background = bg, BorderThickness = new Thickness(0),
                Content = new Grid { ColumnDefinitions = new ColumnDefinitions("34,Auto,*"), VerticalAlignment = VerticalAlignment.Center, Children = {
                    new Grid { Width = 32, Height = 32, Children = { new Border { CornerRadius = new CornerRadius(8), Background = Brush.Parse("#FF343434") }, new TextBlock { Text = c.Number.ToString(), Foreground = GoldText(), FontSize = 11, FontWeight = FontWeight.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }, logo } },
                    Pos(new Border { Width = 6 }, 1),
                    Pos(new TextBlock { Text = c.Name + (Tv.Favorites.Contains(c.Id) ? " ★" : ""), Foreground = Text, FontSize = 12.5, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center }, 2) } } };
            ToolTip.SetTip(nameBtn, $"{c.Name} jetzt ansehen");
            var cid = c.Id; nameBtn.Click += (_, _) => { _tvTab = "live"; TvPlay(cid); Show("tv"); };
            names.Children.Add(nameBtn);
            var row = new Canvas { Height = GuideRow, Width = width, Background = bg };
            foreach (var p in Trendo.Core.Tv.TvGuide.Row(progs, c, from, to))
            {
                var s = p.Start < from ? from : p.Start; var e = p.Stop > to ? to : p.Stop; var w = Math.Max(8, X(e) - X(s) - 2);
                var on = p.IsOn(now); var past = p.Stop <= now; var kind = Trendo.Core.Tv.TvGuide.Kind(p); var key = $"{c.Id}|{p.Start:O}";
                var rec = Trendo.Core.Tv.TvGuide.Planned(recs, c, p); var bell = Tv.HasReminder(c.Id, p.Start);
                var marks = (rec ? "⏺ " : "") + (bell ? "🔔 " : "");
                var title = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, Children = { new TextBlock { Text = p.Title, Foreground = Text, FontSize = 12, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis } } };
                if (marks.Length > 0) title.Children.Insert(0, new TextBlock { Text = marks, Foreground = Brush.Parse(rec ? "#F87171" : "#FACC15"), FontSize = 11 });
                var inner = new StackPanel { Spacing = 1, Margin = new Thickness(7, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center, Children = { title } };
                if (w > 80) inner.Children.Add(new TextBlock { Text = $"{p.Start.ToLocalTime():HH:mm}–{p.Stop.ToLocalTime():HH:mm}", Foreground = Muted, FontSize = 10.5 });
                var cell = new Grid { Children = { new Border { Width = 3, HorizontalAlignment = HorizontalAlignment.Left, Background = Brush.Parse(Trendo.Core.Tv.TvGuide.Color(kind)), CornerRadius = new CornerRadius(2) }, inner } };
                if (on) cell.Children.Add(new Border { Height = 3, VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Left, Width = Math.Max(0, (X(e) - X(s) - 4) * p.Progress(now)), Background = Brush.Parse("#D4A84B"), CornerRadius = new CornerRadius(2) });
                var selected = _tvProgSel == key;
                var b = new Button { Name = "tv-prog-" + c.Id.Replace(':', '-') + "-" + p.Start.ToString("HHmm"), Width = w, Height = GuideRow - 6, Padding = new Thickness(0), CornerRadius = new CornerRadius(7), HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch, ClipToBounds = true,
                    Background = on ? new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative), GradientStops = { new GradientStop(Color.Parse("#55C9A227"), 0), new GradientStop(Color.Parse("#22D4A84B"), 1) } } : Brush.Parse(past ? "#0DFFFFFF" : "#1CFFFFFF"),
                    BorderBrush = selected ? Brush.Parse("#D4A84B") : Brush.Parse("#18FFFFFF"), BorderThickness = new Thickness(selected ? 2 : 1),
                    Opacity = _tvGuideKind != "Alle" && kind != _tvGuideKind ? 0.22 : past ? 0.55 : 1, Content = cell };
                ToolTip.SetTip(b, $"{p.Title}\n{p.Start.ToLocalTime():HH:mm}–{p.Stop.ToLocalTime():HH:mm} · {kind}" + (p.Description.Length > 0 ? "\n" + (p.Description.Length > 160 ? p.Description[..160] + " …" : p.Description) : ""));
                b.Click += (_, _) => { _tvProgSel = key; Show("tv"); };
                Canvas.SetLeft(b, X(s) + 1); Canvas.SetTop(b, 3); row.Children.Add(b);
            }
            rows.Children.Add(row);
        }
        if (channels.Count == 0 && progs.Count > 0) rows.Children.Add(P("In dieser Gruppe hat kein Sender Programminfos.", Muted, 12));
        var area = new Grid { Children = { rows } };
        if (now > from && now < to)
        {
            var overlay = new Canvas { IsHitTestVisible = false, Name = "tv-guide-nowline" };
            var line = new Border { Width = 2, Height = 26 + channels.Count * GuideRow, Background = Brush.Parse("#D4A84B"), BoxShadow = BoxShadows.Parse("0 0 6 0 #AAD4A84B") };
            Canvas.SetLeft(line, X(now)); overlay.Children.Add(line);
            var tag = new Border { Background = Brush.Parse("#D4A84B"), CornerRadius = new CornerRadius(4), Padding = new Thickness(4, 0), Child = new TextBlock { Text = now.ToLocalTime().ToString("HH:mm"), FontSize = 10, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#1B1406") } };
            Canvas.SetLeft(tag, X(now) - 16); Canvas.SetTop(tag, 0); overlay.Children.Add(tag);
            area.Children.Add(overlay);
        }
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions($"{GuideName},*"), Children = { names, Pos(new ScrollViewer { Name = "tv-guide-time", Content = area, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled }, 1) } };
        sp.Children.Add(Glass2(new ScrollViewer { Content = grid, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 560 }, 6));
        if (channels.Count == 80) sp.Children.Add(P("Es werden die ersten 80 Sender gezeigt – oben eine Gruppe wählen für die übrigen.", Muted, 11.5));

        // Details zur gewählten Sendung
        if (_tvProgSel?.Split('|') is [var selId, var st] && TvChannels().FirstOrDefault(x => x.Id == selId) is { } ch
            && progs.FirstOrDefault(p => string.Equals(p.Channel, ch.EpgId, StringComparison.OrdinalIgnoreCase) && p.Start.ToString("O") == st) is { } sel)
            sp.Children.Add(WCard(TvGuideDetail(ch, sel, now)));
        return sp;
    }

    Control TvGuideDetail(TvChannel ch, TvProgramme sel, DateTime now)
    {
        var kind = Trendo.Core.Tv.TvGuide.Kind(sel);
        var d = new StackPanel { Name = "tv-prog-detail", Spacing = 4 };
        d.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { new Border { Background = Brush.Parse(Trendo.Core.Tv.TvGuide.Color(kind)), CornerRadius = new CornerRadius(6), Padding = new Thickness(7, 1), VerticalAlignment = VerticalAlignment.Center, Child = new TextBlock { Text = kind, FontSize = 11, Foreground = Brushes.White, FontWeight = FontWeight.SemiBold } }, H(sel.Title, 15) } });
        d.Children.Add(P($"{ch.Name} · {sel.Start.ToLocalTime():dddd dd.MM. HH:mm}–{sel.Stop.ToLocalTime():HH:mm} ({(int)(sel.Stop - sel.Start).TotalMinutes} Min.){(sel.Category.Length > 0 ? " · " + sel.Category : "")}", null, 12));
        if (sel.IsOn(now)) d.Children.Add(new ProgressBar { Value = sel.Progress(now) * 100, Height = 4, MinHeight = 4, Foreground = Brush.Parse("#D4A84B"), Background = Brush.Parse("#22FFFFFF"), Margin = new Thickness(0, 2) });
        if (sel.Description.Length > 0) d.Children.Add(P(sel.Description, Text, 12.5));
        var acts = new WrapPanel();
        if (sel.IsOn(now)) acts.Children.Add(TvButton("tv-prog-watch", "▶ Ansehen", () => { _tvTab = "live"; TvPlay(ch.Id); Show("tv"); }, true));
        var planned = Tv.Recordings.FirstOrDefault(r => r.ChannelId == ch.Id && r.State is "geplant" or "läuft" && r.Start <= sel.Start && r.Stop >= sel.Stop);
        if (sel.Stop > now)
        {
            if (planned is null) acts.Children.Add(TvButton("tv-prog-rec", "⏺ Aufnehmen", () =>
            {
                var r = TvPlanRecording(ch, sel.Title, sel.Start, sel.Stop);
                var clash = Trendo.Core.Tv.TvGuide.Overlaps(Tv.Recordings, sel.Start, sel.Stop, r?.Id);
                _tvMsg = $"✓ Aufnahme geplant: {sel.Title} ({sel.Start.ToLocalTime():dd.MM. HH:mm})." + (clash.Count > 0 ? $" Hinweis: überschneidet sich mit „{clash[0].Title}“ ({clash[0].ChannelName}) – beide laufen gleichzeitig, wenn dein Anbieter das erlaubt." : "");
                Show("tv");
            }));
            else if (planned.State == "geplant") acts.Children.Add(TvButton("tv-prog-unrec", "⏺ Aufnahme geplant – entfernen", () => { Tv.RemoveRecording(planned.Id); _tvMsg = "Aufnahme entfernt."; Show("tv"); }));
            if (sel.Start > now)
            {
                var on = Tv.HasReminder(ch.Id, sel.Start);
                acts.Children.Add(TvButton("tv-prog-remind", on ? "🔔 Erinnerung aus" : "🔔 Erinnern", () =>
                {
                    var set = Tv.ToggleReminder(ch, sel);
                    _tvMsg = set ? $"🔔 Trendo erinnert dich kurz vor {sel.Start.ToLocalTime():HH:mm} an „{sel.Title}“." : "Erinnerung entfernt.";
                    if (set) TvRecWatch();
                    Show("tv");
                }));
            }
        }
        else if (Catchup.UrlFor(ch, sel) is { } cu) acts.Children.Add(TvButton("tv-prog-catchup", "⟲ Zeitversetzt ansehen", () => { _tvTab = "live"; Show("tv"); TvPlayUrl(cu, "main", ch); }, true));
        else acts.Children.Add(P("Diese Sendung ist vorbei. Dein Anbieter bietet für diesen Sender kein Nachschauen an.", null, 11.5));
        acts.Children.Add(TvButton("tv-prog-more", "Weitere Termine", () => { _tvGuideFind = sel.Title; Show("tv"); }));
        d.Children.Add(acts);
        return d;
    }

    /// <summary>Fällige Erinnerungen als Hinweis mit „Einschalten“ (läuft mit dem Aufnahme-Takt).</summary>
    public void TvReminderTick()
    {
        foreach (var r in Trendo.Core.Tv.TvGuide.Due(Tv.Reminders, DateTime.UtcNow, TimeSpan.FromMinutes(3)))
        {
            Tv.ReminderShown(r.ChannelId, r.Start);
            var id = r.ChannelId;
            Toast($"🔔 {(r.Start > DateTime.UtcNow ? "Gleich" : "Jetzt")}: „{r.Title}“ auf {r.ChannelName} ({r.Start.ToLocalTime():HH:mm})", ("Einschalten", new Action(() => { _tvTab = "live"; TvPlay(id); Show("tv"); })));
        }
    }
}
