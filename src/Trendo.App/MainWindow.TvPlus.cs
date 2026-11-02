using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Trendo.Core;
using Trendo.Core.Tv;

namespace Trendo.App;

/// <summary>TV & Live auf Highend (Wunsch 02.11.2026, alle 12 Vorschläge): Weiterschauen, nächste Folge mit „Intro/Abspann überspringen“,
/// Startseite mit großen Bild und Reihen, Kinder-Profil mit PIN, Bild im Bild, Sprache für Ton und Untertitel, Bild- und Tonregler,
/// Senderlogo und Jetzt/Danach im Vollbild, Aufnahme-Regeln, Handy als Fernbedienung, Trailer und Bewertungen, doppelte Sender zusammengefasst.</summary>
public partial class MainWindow
{
    /// <summary>Gerade laufende Folge (für „Nächste Folge“).</summary>
    sealed record TvEpCtx(string SeriesId, string SeriesName, int Season, int Episode, IReadOnlyList<TvEpisode> Eps, string? Cover);
    TvEpCtx? _tvEp;
    string? _tvVodUrl;
    DateTime _tvResumeSaved;
    bool _tvNextCancelled, _tvKidsAsk;
    Window? _tvPip;
    Dictionary<string, List<TvChannel>> _tvAlternatives = [];
    TvVodInfo? _tvVodInfo; string? _tvVodInfoFor;
    public bool TvPipOpen => _tvPip is not null;
    public TvVodInfo? TvVodInfoForTests => _tvVodInfo;

    // ---------- 1/2 Weiterschauen, nächste Folge ----------
    /// <summary>Nach dem Start eines Films oder einer Folge: an die gemerkte Stelle springen (mpv braucht einen Moment zum Öffnen).</summary>
    void TvAfterPlay(string url)
    {
        _tvVodUrl = url; _tvNextCancelled = false;
        if (Tv.ResumeFor(url) is { Open: true } r)
        {
            _tvMsg = $"✓ Weiter ab {TvClock(r.Pos)}.";
            _ = Task.Delay(1500).ContinueWith(_ => _ui.Post(() => _tvPlayer?.Send("seek", Math.Round(r.Pos), "absolute")));
        }
    }

    /// <summary>Jede Sekunde (aus der Zeitleiste): Stelle merken, Knöpfe „Intro überspringen“/„Nächste Folge“ zeigen, am Ende automatisch weiter.</summary>
    void TvPlusTick(double pos, double dur)
    {
        if (!_tvVodPlaying || _tvVodUrl is not { } url || _tvPlayingTitle is not { } title) return;
        if ((DateTime.UtcNow - _tvResumeSaved).TotalSeconds >= 10 && pos > 0)
        {
            _tvResumeSaved = DateTime.UtcNow;
            Tv.SaveResume(new TvResume(TvPlus.ResumeKey(url), title, url, pos, dur, DateTime.UtcNow, _tvEp is null ? "film" : "serie", _tvEp?.SeriesId, _tvEp?.Season ?? 0, _tvEp?.Episode ?? 0, _tvEp?.Cover));
        }
        var (intro, credits, countdown) = TvPlus.EpisodeMoments(pos, dur);
        var next = _tvEp is { } ep ? TvPlus.NextEpisode(ep.Eps, ep.Season, ep.Episode) : null;
        TvSetVisible("tv-vod-skip-intro", intro && _tvEp is not null);
        TvSetVisible("tv-vod-next", (credits || countdown) && next is not null && !_tvNextCancelled);
        if (TvFindName<TextBlock>("tv-vod-countdown") is { } cd)
        {
            cd.IsVisible = countdown && next is not null && !_tvNextCancelled;
            cd.Text = next is null ? "" : $"Nächste Folge in {Math.Max(0, (int)Math.Ceiling(dur - pos))} s: S{next.Season:00}E{next.Episode:00} {next.Title}";
        }
        if (countdown && next is not null && !_tvNextCancelled && pos >= dur - 1.2) TvPlayNextEpisode();
    }

    T? TvFindName<T>(string name) where T : Control => this.GetLogicalDescendantsOf<T>().FirstOrDefault(c => c.Name == name);
    void TvSetVisible(string name, bool on) { if (TvFindName<Control>(name) is { } c) c.IsVisible = on; }

    /// <summary>Nächste Folge der laufenden Serie abspielen.</summary>
    public bool TvPlayNextEpisode()
    {
        if (_tvEp is not { } ep || TvPlus.NextEpisode(ep.Eps, ep.Season, ep.Episode) is not { } n) return false;
        if (_tvVodUrl is { } done) Tv.ForgetResume(TvPlus.ResumeKey(done)); // fertig gesehen
        _tvEp = ep with { Season = n.Season, Episode = n.Episode };
        _tvSeason = n.Season;
        return TvPlayVideo(n.Url, $"{ep.SeriesName} – S{n.Season:00}E{n.Episode:00} {n.Title}");
    }

    /// <summary>Folge mit ihrer Serie abspielen (damit „Nächste Folge“ weiß, wie es weitergeht).</summary>
    public bool TvPlayEpisode(TvSeries ser, TvEpisode e, IReadOnlyList<TvEpisode> eps)
    {
        _tvEp = new TvEpCtx(ser.Id, ser.Name, e.Season, e.Episode, eps, ser.Cover);
        return TvPlayVideo(e.Url, $"{ser.Name} – S{e.Season:00}E{e.Episode:00} {e.Title}");
    }

    /// <summary>Knöpfe unter dem Film/der Folge: Intro überspringen, Nächste Folge, Countdown, Bild im Bild, Bild & Ton.</summary>
    Control TvVodExtras()
    {
        var skip = TvButton("tv-vod-skip-intro", "⏭ Intro überspringen", () => _tvPlayer?.Send("seek", 85)); skip.IsVisible = false;
        var next = TvButton("tv-vod-next", "⏭ Nächste Folge", () => TvPlayNextEpisode(), true); next.IsVisible = false;
        var cd = new TextBlock { Name = "tv-vod-countdown", Foreground = GoldText(), FontSize = 13, VerticalAlignment = VerticalAlignment.Center, IsVisible = false };
        var cancel = TvButton("tv-vod-next-cancel", "Abbrechen", () => { _tvNextCancelled = true; TvSetVisible("tv-vod-next", false); TvSetVisible("tv-vod-countdown", false); });
        cancel.Bind(IsVisibleProperty, cd.GetObservable(IsVisibleProperty));
        return new WrapPanel { Name = "tv-vod-extras", Children = { skip, next, cd, cancel, TvPipButton("tv-vod-pip"), TvTuneButton("tv-vod-tune") } };
    }

    /// <summary>„Weiterschauen“ als Reihe: Bild, Titel, Fortschritt; Klick spielt an der gemerkten Stelle weiter.</summary>
    Control? TvContinueRow(string kind)
    {
        var items = Tv.Resume.Where(r => r.Kind == kind && r.Open).Take(14).ToList();
        if (items.Count == 0) return null;
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        for (var i = 0; i < items.Count; i++)
        {
            var r = items[i];
            var img = new Avalonia.Controls.Image { Width = 180, Height = 100, Stretch = Stretch.UniformToFill }; TvImage(img, r.Cover ?? TvVodList().FirstOrDefault(v => v.Url == r.Url)?.Cover);
            var card = new StackPanel { Width = 180, Spacing = 4, Children = {
                new Border { CornerRadius = new CornerRadius(10), ClipToBounds = true, Height = 100, Background = Brush.Parse("#262626"), Child = img },
                new ProgressBar { Value = r.Progress * 100, Height = 4, MinHeight = 4, Foreground = Brush.Parse("#D4A84B"), Background = Brush.Parse("#22FFFFFF") },
                new TextBlock { Text = r.Title, Foreground = Text, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis },
                new TextBlock { Text = $"noch {TvClock(Math.Max(0, r.Dur - r.Pos))}", Foreground = Muted, FontSize = 10.5 } } };
            var b = new Button { Name = $"tv-continue-{kind}-{i}", Content = card, Padding = new Thickness(0), Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
            b.Click += (_, _) =>
            {
                if (r.Kind == "serie" && r.SeriesId is { } sid && TvSeriesList().FirstOrDefault(s => s.Id == sid) is { } ser)
                    _tvEp = new TvEpCtx(sid, ser.Name, r.Season, r.Episode, _tvEpsFor == sid ? _tvEps ?? [] : Tv.Episodes(sid), ser.Cover);
                else _tvEp = null;
                TvPlayVideo(r.Url, r.Title);
            };
            row.Children.Add(b);
        }
        return new StackPanel { Name = "tv-continue-" + kind, Spacing = 6, Children = { H("▶ Weiterschauen", 15), new ScrollViewer { Content = row, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled } } };
    }

    // ---------- 3 Startseite wie bei Streaming-Diensten ----------
    Control TvFilmHome(IReadOnlyList<TvVod> all)
    {
        var sp = new StackPanel { Name = "tv-film-home", Spacing = 14 };
        if (TvPlus.Hero(all) is { } hero)
        {
            var img = new Avalonia.Controls.Image { Stretch = Stretch.UniformToFill }; TvImage(img, hero.Cover);
            var info = new StackPanel { Spacing = 6, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(26, 0, 26, 22), MaxWidth = 560, HorizontalAlignment = HorizontalAlignment.Left, Children = {
                new TextBlock { Text = "EMPFEHLUNG", Foreground = GoldText(), FontSize = 11, LetterSpacing = 2 },
                new TextBlock { Text = hero.Name, Foreground = Brushes.White, FontSize = 30, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap },
                new TextBlock { Text = string.Join(" · ", new[] { hero.Year, hero.Rating is { Length: > 0 } r ? "★ " + r : null, hero.Category }.Where(x => !string.IsNullOrEmpty(x))), Foreground = Brush.Parse("#E7E1D5"), FontSize = 13 },
                new TextBlock { Text = hero.Plot ?? "", Foreground = Brush.Parse("#D8D2C6"), FontSize = 13, TextWrapping = TextWrapping.Wrap, MaxHeight = 60, TextTrimming = TextTrimming.CharacterEllipsis },
                new WrapPanel { Children = { TvButton("tv-hero-play", "▶ Abspielen", () => { _tvEp = null; TvPlayVideo(hero.Url, hero.Name); }, true), TvButton("tv-hero-info", "ⓘ Mehr Infos", () => { _tvVodSel = hero.Id; Show("tv"); }) } } } };
            sp.Children.Add(new Border { Name = "tv-film-hero", Height = 340, CornerRadius = new CornerRadius(18), ClipToBounds = true, Background = Brush.Parse("#1A1A1A"),
                Child = new Grid { Children = { img,
                    new Border { Background = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative), GradientStops = { new GradientStop(Color.Parse("#F00B0B0C"), 0), new GradientStop(Color.Parse("#990B0B0C"), 0.5), new GradientStop(Color.Parse("#000B0B0C"), 1) } } },
                    info } } });
        }
        if (TvContinueRow("film") is { } cont) sp.Children.Add(cont);
        foreach (var row in TvPlus.Rows(all, Tv.Resume))
        {
            var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            foreach (var v in row.Items)
            {
                var vv = v;
                line.Children.Add(TvPoster($"tv-row-{TvSafeId(row.Id)}-{Safe(v.Id)}", v.Name, v.Rating is { Length: > 0 } r ? "★ " + r : v.Year ?? "", v.Cover, () => { _tvVodSel = vv.Id; Show("tv"); }, false));
            }
            sp.Children.Add(new StackPanel { Name = "tv-row-" + TvSafeId(row.Id), Spacing = 6, Children = { H(row.Title, 15), new ScrollViewer { Content = line, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled } } });
        }
        return sp;
    }
    static string TvSafeId(string s) => new(s.Select(ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '-').ToArray());

    // ---------- 4 Kinder-Profil ----------
    /// <summary>Kinder-Profil an/aus. Aus geht mit PIN (wenn eine eingerichtet ist).</summary>
    public bool TvSetKids(bool on, string? pin = null)
    {
        if (on) { Tv.SetKidsMode(true); _tvChannels = null; _tvKidsAsk = false; _tvMsg = "✓ Kinder-Profil an – nur Kindersender und Kinderfilme."; _core.Audit.Append(Principal.LocalOwner.Name, "tv.kinder", "an"); return true; }
        if (Lock.Enabled && !Lock.Verify(pin ?? "")) { _tvKidsAsk = true; _tvMsg = pin is null ? "Zum Beenden des Kinder-Profils die PIN eingeben." : "Die PIN stimmt nicht."; return false; }
        Tv.SetKidsMode(false); _tvChannels = null; _tvKidsAsk = false;
        _tvMsg = Lock.Enabled ? "✓ Kinder-Profil aus." : "✓ Kinder-Profil aus. Tipp: unter Einstellungen → Sicherheit eine PIN setzen, dann können Kinder es nicht selbst beenden.";
        _core.Audit.Append(Principal.LocalOwner.Name, "tv.kinder", "aus");
        return true;
    }

    Control TvKidsBar()
    {
        var kids = Tv.KidsMode;
        var b = TvButton("tv-kids", kids ? "🧸 Kinder-Profil beenden" : "🧸 Kinder-Profil", () => { if (Tv.KidsMode) TvSetKids(false); else TvSetKids(true); Show("tv"); }, kids);
        var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { b } };
        if (_tvKidsAsk)
        {
            var pin = new TextBox { Name = "tv-kids-pin", PasswordChar = '•', Width = 110, PlaceholderText = "PIN" };
            sp.Children.Add(pin); sp.Children.Add(TvButton("tv-kids-pin-ok", "Beenden", () => { TvSetKids(false, pin.Text ?? ""); Show("tv"); }, true));
        }
        return sp;
    }

    // ---------- 5 Bild im Bild ----------
    Button TvPipButton(string name) => TvButton(name, _tvPip is null ? "⧉ Bild im Bild" : "⧉ Zurückholen", () => TvPip(_tvPip is null));

    /// <summary>Bild im Bild: das laufende Bild in ein kleines Fenster, das über allem liegt – Trendo bleibt frei bedienbar.</summary>
    public void TvPip(bool on)
    {
        _tvPlayer ??= new MpvView("main");
        if (on)
        {
            if (_tvPip is not null) return;
            if (TvIsFullscreen) TvFullscreen(false);
            Detach(_tvPlayer);
            _tvPip = new Window { Title = "Trendo – Bild im Bild", Width = 480, Height = 270, Topmost = true, CanResize = true, Background = Brushes.Black, ShowInTaskbar = false, Content = _tvPlayer,
                WindowStartupLocation = WindowStartupLocation.Manual, Position = new PixelPoint(Math.Max(0, (int)(Screens.Primary?.WorkingArea.Right ?? 1920) - 500), Math.Max(0, (int)(Screens.Primary?.WorkingArea.Bottom ?? 1080) - 300)) };
            _tvPip.Closed += (_, _) => { var p = _tvPip; _tvPip = null; if (p?.Content == _tvPlayer) p!.Content = null; Show("tv"); };
            if (!_opt.Isolated || TvPlayOverride is not null) _tvPip.Show();
            _core.Audit.Append(Principal.LocalOwner.Name, "tv.pip", "an");
        }
        else if (_tvPip is { } p) { p.Content = null; _tvPip = null; p.Close(); }
        Show("tv");
    }

    // ---------- 6/7 Sprache, Bild & Ton ----------
    /// <summary>Bild & Ton: Helligkeit, Kontrast, Farbe, Nachtmodus (leise Stellen lauter, laute leiser), Ton-Verzögerung.</summary>
    Button TvTuneButton(string name)
    {
        var b = TvButton(name, "🎚 Bild & Ton", () => { });
        var panel = new StackPanel { Name = name + "-panel", Spacing = 6, Width = 280, Margin = new Thickness(6) };
        Slider S(string id, string label, string prop)
        {
            var s = new Slider { Name = $"{name}-{id}", Minimum = -50, Maximum = 50, Value = 0 };
            s.ValueChanged += (_, _) => _tvPlayer?.Send("set_property", prop, (int)Math.Round(s.Value));
            panel.Children.Add(new TextBlock { Text = label, FontSize = 12 }); panel.Children.Add(s); return s;
        }
        S("hell", "Helligkeit", "brightness"); S("kontrast", "Kontrast", "contrast"); S("farbe", "Farbe", "saturation");
        var night = new CheckBox { Name = name + "-nacht", Content = "Nachtmodus (gleichmäßige Lautstärke)" };
        night.IsCheckedChanged += (_, _) => _tvPlayer?.Send("af", "toggle", "@nacht:lavfi=[dynaudnorm=f=200:g=15]");
        panel.Children.Add(night);
        panel.Children.Add(new TextBlock { Text = "Ton-Verzögerung", FontSize = 12 });
        panel.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = {
            TvButton(name + "-ton-minus", "− 0,1 s", () => _tvPlayer?.Send("add", "audio-delay", -0.1)),
            TvButton(name + "-ton-plus", "+ 0,1 s", () => _tvPlayer?.Send("add", "audio-delay", 0.1)),
            TvButton(name + "-zurueck", "Zurücksetzen", () => { foreach (var p in new[] { "brightness", "contrast", "saturation" }) _tvPlayer?.Send("set_property", p, 0); _tvPlayer?.Send("set_property", "audio-delay", 0); }) } });
        b.Flyout = new Flyout { Content = panel, Placement = PlacementMode.Top };
        return b;
    }

    /// <summary>Sprache für Ton und Untertitel an den Player geben (Deutsch zuerst; Untertitel nur bei fremder Sprache).</summary>
    void TvApplyLanguage() => MpvView.ExtraArgs = TvPlus.LanguageArgs(Tv.Language);

    // ---------- 9 Aufnahme-Regeln ----------
    /// <summary>Programmführer der nächsten 24 Stunden mit den Aufnahme-Regeln abgleichen und passende Sendungen einplanen (je Sendung einmal).</summary>
    public int TvRuleTick()
    {
        var rules = Tv.RecordRules; if (rules.Count == 0) return 0;
        var now = DateTime.UtcNow; var n = 0;
        var chans = TvChannels(); var planned = Tv.Recordings.Select(r => (r.ChannelId, r.Start)).ToHashSet();
        foreach (var p in TvProgs().Where(p => p.Stop > now && p.Start < now.AddHours(24)))
        {
            if (chans.FirstOrDefault(c => string.Equals(c.EpgId, p.Channel, StringComparison.OrdinalIgnoreCase)) is not { } ch) continue;
            if (!rules.Any(r => TvPlus.Matches(r, ch.Id, p.Title)) || planned.Contains((ch.Id, p.Start))) continue;
            TvPlanRecording(ch, p.Title, p.Start, p.Stop); planned.Add((ch.Id, p.Start)); n++;
        }
        return n;
    }

    // ---------- 10 Handy als Fernbedienung ----------
    /// <summary>Befehl vom Handy (Fernbedienung): pause, zurueck, vor, lauter, leiser, stumm, sender+, sender-, vollbild, naechste.</summary>
    public string TvRemote(string cmd)
    {
        if (CurrentView != "tv") Show("tv");
        switch (cmd)
        {
            case "pause": _tvPlayer?.Send("cycle", "pause"); return "⏯";
            case "zurueck": _tvPlayer?.Send("seek", -30); return "⏪ 30 s";
            case "vor": _tvPlayer?.Send("seek", 30); return "⏩ 30 s";
            case "lauter": _tvPlayer?.Send("add", "volume", 10); return "🔊 +";
            case "leiser": _tvPlayer?.Send("add", "volume", -10); return "🔉 −";
            case "stumm": _tvPlayer?.Send("cycle", "mute"); return "🔇";
            case "sender+": TvZap(1); return "Sender ▶";
            case "sender-": TvZap(-1); return "◀ Sender";
            case "vollbild": TvFullscreen(!TvIsFullscreen); return TvIsFullscreen ? "⛶ Vollbild" : "⤡ Fenster";
            case "naechste": return TvPlayNextEpisode() ? "⏭ Nächste Folge" : "Keine nächste Folge";
            default: return "Unbekannt";
        }
    }

    // ---------- 11 Trailer und Bewertungen ----------
    /// <summary>Film-Infos vom Anbieter holen (Bewertung, Regie, Darsteller, Genre, Länge, Trailer). Nur bei Xtream-Zugängen.</summary>
    public async Task TvLoadVodInfo(TvVod film)
    {
        if (_tvVodInfoFor == film.Id) return;
        _tvVodInfoFor = film.Id; _tvVodInfo = null;
        if (TvPlus.StreamIdOf(Tv.Reveal(film.Url) ?? film.Url) is not { } sid) return;
        var src = Tv.Sources.FirstOrDefault(s => s.Kind == "xtream");
        if (src is null || Xtream.FromPlaylistUrl(Tv.Reveal(src.Url) ?? "") is not { } x) return;
        try
        {
            var json = System.Text.Encoding.UTF8.GetString(await TvFetch(Xtream.Api(x.Server, x.User, x.Pass, "get_vod_info", "&vod_id=" + Uri.EscapeDataString(sid))));
            _tvVodInfo = TvPlus.ParseVodInfo(json);
            if (_tvVodSel == film.Id) _ui.Post(() => Show("tv")); // nach dem laufenden Seitenaufbau neu zeichnen
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or InvalidOperationException) { }
    }

    Control? TvVodInfoBlock()
    {
        if (_tvVodInfo is not { } i) return null;
        var sp = new StackPanel { Name = "tv-film-info", Spacing = 3 };
        void L(string label, string? v) { if (!string.IsNullOrWhiteSpace(v)) sp.Children.Add(new TextBlock { Text = $"{label}: {v}", Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap }); }
        L("Bewertung", i.Rating is { } r ? "★ " + r : null); L("Genre", i.Genre); L("Länge", i.Duration); L("Regie", i.Director); L("Darsteller", i.Cast);
        if (i.TrailerUrl is { } t) sp.Children.Add(TvButton("tv-film-trailer", "▶ Trailer ansehen", () => Launch(t)));
        return sp;
    }

    // ---------- 12 Doppelte Sender ----------
    /// <summary>Andere Qualitäten des laufenden Senders (z. B. HD statt FHD, wenn das Netz knapp ist).</summary>
    Control? TvQualityButton()
    {
        if (_tvChannel is not { } id || !_tvAlternatives.TryGetValue(id, out var alts) || alts.Count < 2) return null;
        var b = TvButton("tv-quality", "Qualität ▾", () => { });
        var menu = new StackPanel { Spacing = 4, Margin = new Thickness(6) };
        foreach (var c in alts) { var cid = c.Id; menu.Children.Add(TvButton("tv-quality-" + Safe(c.Id), c.Name, () => TvPlay(cid))); }
        b.Flyout = new Flyout { Content = menu, Placement = PlacementMode.Top };
        return b;
    }
}

public partial class MainWindow
{
    static Control DockRight(Control c) { DockPanel.SetDock(c, Avalonia.Controls.Dock.Right); return c; }
}
