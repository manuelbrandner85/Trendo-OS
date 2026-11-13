using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Live streamen wie OBS (Profi 27, 05.11.2026): Szenen links, Programmbild groß, Quellen der Szene, Tonmischer mit Pegeln,
/// Probelauf vor dem Senden und Sendeplan. Ton-Wache: Aussetzer und Übersteuern werden sofort gemeldet.</summary>
public partial class MainWindow
{
    LiveShow? _liveShow; DateTime? _liveStarted; LiveMode _liveLastMode = LiveMode.Idle;
    string? _liveWarn; List<LiveProbe.Check>? _liveProbe; bool _liveProbing; string _liveOverlayDraft = "", _liveOverlayKind = "text";
    readonly Dictionary<string, (Border Bar, TextBlock Value)> _liveMeters = [];
    const string LiveMeterDir = "trendo-live-pegel";

    string LiveDir => Fach.StoreDir(_core.DataRoot, "sendung");
    /// <summary>Szenen, Tonmischer und Sendeplan (öffentlich für Tests und Claude).</summary>
    public LiveShow LiveShowState => _liveShow ??= LiveShow.Load(LiveDir);
    /// <summary>Tests: Dauer des Probelaufs in Sekunden (0 = sofort auswerten).</summary>
    public static int LiveProbeSeconds { get; set; } = 8;
    /// <summary>Tests: Auswertung der Probeaufnahme ersetzen (Datei → FFmpeg-Ausgabe).</summary>
    public Func<string, string>? ProbeAnalyzeOverride { get; set; }
    public IReadOnlyList<LiveProbe.Check>? LiveProbeResult => _liveProbe;
    public string? LiveAudioWarning => _liveWarn;
    string MeterPath(string ch) => Path.Combine(Path.GetTempPath(), LiveCompose.MeterFile(LiveMeterDir, ch).Replace('/', Path.DirectorySeparatorChar));

    void LiveSave() { LiveShowState.Save(LiveDir); }

    /// <summary>Szene wählen: Hauptquelle folgt der Szene. Läuft eine Aufnahme, startet sie mit der neuen Szene neu; bei einer Sendung braucht der Wechsel ein „Ja“.</summary>
    public void LiveSelectScene(string id)
    {
        var sc = LiveShowState.Scenes.FirstOrDefault(s => s.Id == id) ?? throw new InvalidOperationException("Diese Szene gibt es nicht.");
        LiveShowState.Active = id; _liveVideo = sc.Main; LiveSave();
        _core.Audit.Append(Principal.LocalOwner.Name, "live.szene", sc.Name);
        switch (Live.Mode)
        {
            case LiveMode.Preview when _ffmpegResolved is { } ff: Live.Stop(); try { Live.StartPreview(ff, CurrentChoice(), PreviewPath); } catch (InvalidOperationException e) { _liveMsg = e.Message; } break;
            case LiveMode.Recording when _ffmpegResolved is { } ff2:
                Live.Stop(); try { Live.StartRecording(ff2, CurrentChoice(), PreviewPath, NewRecording()); _liveMsg = $"Szene „{sc.Name}“ – die Aufnahme läuft in einer neuen Datei weiter."; } catch (InvalidOperationException e) { _liveMsg = e.Message; }
                break;
            case LiveMode.Broadcasting:
                AskLive(); _liveMsg = $"Szenenwechsel auf „{sc.Name}“: „Ja, jetzt senden“ schaltet um – der Stream startet dabei 1–2 Sekunden neu."; break;
            default: _liveMsg = $"Szene „{sc.Name}“ gewählt."; break;
        }
        Show("sendung");
    }

    public void LiveAddScene(string name) { var s = LiveShowState.Add(name, _liveVideo); LiveSave(); LiveSelectScene(s.Id); }

    public void LiveAddOverlay(string kind, string value)
    {
        var sc = LiveShowState.Current ?? throw new InvalidOperationException("Erst eine Szene anlegen.");
        LiveShowState.AddOverlay(sc.Id, kind, value); LiveSave(); _liveOverlayDraft = "";
        _liveMsg = "Einblendung hinzugefügt – „Vorschau starten“ zeigt sie im Bild.";
    }

    public void LiveSetMix(LiveMix mix) { LiveShowState.Mix = mix; LiveSave(); }

    /// <summary>Sendeplan setzen („mm:ss Szene – Notiz“, Zeilen oder „ / “).</summary>
    public void LiveSetPlan(string text)
    {
        var (plan, err) = LiveProbe.ParsePlan(text, LiveShowState.Scenes);
        if (plan is null) throw new InvalidOperationException("Sendeplan: " + err + ".");
        LiveShowState.Plan = plan; LiveSave();
    }

    /// <summary>Sendeplan aus dem Ablauf (Programmpunkte mit Beginn) – Szene nach Namen im Titel oder Hinweis, sonst die aktive Szene.</summary>
    public void LivePlanFromRundown()
    {
        var rows = Broadcast.Rows(); if (rows.Count == 0) throw new InvalidOperationException("Der Ablauf ist noch leer – erst Programmpunkte anlegen.");
        var show = LiveShowState; var t0 = rows[0].Start; var plan = new List<LiveCue>();
        foreach (var r in rows)
        {
            var seg = Broadcast.Plan.Segments[r.Index];
            var sc = show.Scenes.FirstOrDefault(s => seg.Title.Contains(s.Name, StringComparison.OrdinalIgnoreCase) || seg.Note.Contains(s.Name, StringComparison.OrdinalIgnoreCase)) ?? show.Current!;
            plan.Add(new LiveCue(r.Start - t0, sc.Id, seg.Title));
        }
        show.Plan = plan; LiveSave(); _liveMsg = $"Sendeplan aus dem Ablauf: {plan.Count} Punkte.";
    }

    void ResetMeters()
    {
        var d = Path.Combine(Path.GetTempPath(), LiveMeterDir); Directory.CreateDirectory(d);
        foreach (var f in new[] { "mikrofon", "musik" }) { try { File.Delete(MeterPath(f)); } catch (IOException) { } }
        _liveWarn = null;
    }

    static List<(double T, double Lufs)> ReadMeter(string file)
    {
        try
        {
            if (!File.Exists(file)) return [];
            using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (fs.Length > 16000) fs.Seek(-16000, SeekOrigin.End);
            using var r = new StreamReader(fs); var text = r.ReadToEnd();
            var cut = text.IndexOf("frame:", StringComparison.Ordinal); // erste angeschnittene Zeile weglassen
            return LiveMeter.Read(cut > 0 ? text[cut..] : text);
        }
        catch (IOException) { return []; }
    }

    /// <summary>Pegel aktualisieren und Ton-Wache (öffentlich für Tests): zeigt Balken, meldet Aussetzer und Übersteuern.</summary>
    public void LiveMeterTick()
    {
        if (Live.Mode != _liveLastMode) { _liveStarted = Live.Mode is LiveMode.Recording or LiveMode.Broadcasting ? DateTime.Now : null; _liveLastMode = Live.Mode; }
        var mix = LiveShowState.Mix; string? warn = null;
        foreach (var (ch, label, muted, on) in new[] { ("mikrofon", "Mikrofon", mix.MicMute, _liveMic), ("musik", "Musik", mix.MusicMute, mix.MusicFile is { Length: > 0 }) })
        {
            var v = Live.Mode is LiveMode.Recording or LiveMode.Broadcasting && on ? ReadMeter(MeterPath(ch)) : [];
            if (_liveMeters.TryGetValue(ch, out var m))
            {
                var now = v.Count > 0 ? v[^1].Lufs : -120;
                m.Bar.Width = Math.Max(2, 180 * LiveMeter.Bar(now));
                m.Bar.Background = Brush.Parse(now > -6 ? "#EF4444" : now > -14 ? "#FACC15" : "#4ADE80");
                m.Value.Text = v.Count == 0 ? "–" : now <= -100 ? "still" : $"{now:0} LUFS";
            }
            if (ch == "mikrofon") warn ??= LiveMeter.Warn(v, label, muted);
        }
        if (warn != _liveWarn)
        {
            _liveWarn = warn;
            if (warn is not null) { _core.Audit.Append(Principal.LocalOwner.Name, "live.tonwarnung", warn); Toast("" + warn); }
            if (LiveCtrl<TextBlock>("live-audio-warn") is { } t) { t.Text = warn ?? ""; t.IsVisible = warn is not null; }
        }
        if (LiveCtrl<TextBlock>("live-plan-now") is { } pn && _liveStarted is { } st && LiveShowState.Plan.Count > 0)
        {
            var (cur, next) = LiveProbe.At(LiveShowState.Plan, DateTime.Now - st);
            string N(LiveCue c) => LiveShowState.Scenes.FirstOrDefault(s => s.Id == c.Scene)?.Name ?? c.Scene;
            pn.Text = (cur is null ? "" : $"Jetzt: {N(cur)}" + (cur.Note.Length > 0 ? $" – {cur.Note}" : "")) + (next is null ? "" : $"   ·   Als Nächstes {(int)next.At.TotalMinutes:00}:{next.At.Seconds:00}: {N(next)}");
        }
    }

    /// <summary>Probelauf (öffentlich für Tests): kurze Probeaufnahme mit Szene und Tonmischer, dann prüfen – Bild, Ton, Datenrate.</summary>
    public async Task LiveProbeRun()
    {
        if (Live.Mode != LiveMode.Idle) throw new InvalidOperationException("Erst auf Stopp tippen – der Probelauf braucht Kamera und Mikrofon für sich.");
        var ff = await EnsureFfmpeg(); if (ff is null) { Show("sendung"); return; }
        ReadLiveForm();
        var choice = CurrentChoice();
        var file = Path.Combine(LiveDir, "probelauf.mp4"); try { File.Delete(file); } catch (IOException) { }
        _liveProbing = true; _liveProbe = null; _liveMsg = $"Probelauf: {LiveProbeSeconds} Sekunden – sprich ein paar Sätze wie in der Sendung.";
        Live.StartRecording(ff, choice, PreviewPath, file); Show("sendung");
        if (LiveProbeSeconds > 0) await Task.Delay(TimeSpan.FromSeconds(LiveProbeSeconds));
        Live.Stop();
        string err;
        if (ProbeAnalyzeOverride is { } o) err = o(file);
        else { var r = await Task.Run(() => Cli.Run(ff, LiveProbe.AnalyzeArgs(file), null, 120_000)); err = r.Err; }
        var dur = System.Text.RegularExpressions.Regex.Match(err, @"time=(\d+):(\d+):(\d+\.?\d*)") is { Success: true } m ? int.Parse(m.Groups[1].Value) * 3600 + int.Parse(m.Groups[2].Value) * 60 + double.Parse(m.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture) : Math.Max(1, LiveProbeSeconds);
        var bytes = File.Exists(file) ? new FileInfo(file).Length : 0;
        _liveProbe = LiveProbe.Evaluate(err, dur, bytes, choice.VideoKbps + choice.AudioKbps, _liveMic || LiveShowState.Mix.MusicFile is { Length: > 0 });
        _liveProbing = false;
        _liveMsg = _liveProbe.Any(c => c.Light == "rot") ? "Probelauf: bitte die roten Punkte beheben, dann noch einmal." : _liveProbe.Any(c => c.Light == "gelb") ? "Probelauf: fast bereit – gelbe Punkte ansehen." : "Probelauf bestanden – bereit zum Senden.";
        _core.Audit.Append(Principal.LocalOwner.Name, "live.probelauf", string.Join(",", _liveProbe.Select(c => c.Light)));
        Show("sendung");
    }

    // ───────────── Oberfläche ─────────────
    Control LiveScenesCard()
    {
        var sp = new StackPanel { Name = "live-scenes", Spacing = 4 };
        sp.Children.Add(H("Szenen", 14));
        foreach (var s in LiveShowState.Scenes)
        {
            var on = s.Id == LiveShowState.Current?.Id; var id = s.Id;
            var b = new Button { Name = "live-scene-" + s.Id, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(10, 6), CornerRadius = new CornerRadius(8),
                Background = on ? Brush.Parse("#33D4A84B") : Brush.Parse("#10FFFFFF"), BorderBrush = Brush.Parse(on ? "#D4A84B" : "#22FFFFFF"), BorderThickness = new Thickness(1),
                Content = new StackPanel { Children = { new TextBlock { Text = (on ? "• " : "") + s.Name, Foreground = Text, FontWeight = on ? FontWeight.SemiBold : FontWeight.Normal, FontSize = 13 },
                    new TextBlock { Text = (s.Main switch { VideoKind.Camera => "Kamera", VideoKind.Window => "Fenster", _ => "Bildschirm" }) + (s.Overlays.Count > 0 ? $" + {s.Overlays.Count} Einblendung{(s.Overlays.Count == 1 ? "" : "en")}" : ""), Foreground = Muted, FontSize = 11 } } } };
            b.Click += (_, _) => { try { LiveSelectScene(id); } catch (InvalidOperationException e) { _liveMsg = e.Message; Show("sendung"); } };
            sp.Children.Add(b);
        }
        var name = new TextBox { Name = "live-scene-name", PlaceholderText = "Neue Szene, z. B. Interview", MinWidth = 140 };
        var add = new Button { Name = "live-scene-add", Content = "＋", Padding = new Thickness(10, 3) };
        add.Click += (_, _) => { try { LiveAddScene(name.Text ?? ""); } catch (Exception e) when (e is ArgumentException or InvalidOperationException) { _liveMsg = e.Message; Show("sendung"); } };
        sp.Children.Add(new DockPanel { Margin = new Thickness(0, 4, 0, 0), Children = { Dk(add, Avalonia.Controls.Dock.Right), name } });
        return WCard(sp);
    }

    /// <summary>Einblendungen der aktiven Szene (unter den Quellen).</summary>
    Control LiveOverlaysPanel()
    {
        var sp = new StackPanel { Name = "live-overlays", Spacing = 4, Margin = new Thickness(0, 6, 0, 0) };
        var sc = LiveShowState.Current;
        sp.Children.Add(new TextBlock { Text = $"Einblendungen in „{sc?.Name}“", Foreground = Text, FontSize = 12.5, FontWeight = FontWeight.SemiBold });
        foreach (var o in sc?.Overlays ?? [])
        {
            var oid = o.Id;
            var vis = new CheckBox { Name = "live-overlay-vis-" + o.Id, IsChecked = o.Visible, Content = o.Label, Foreground = Text, FontSize = 12 };
            vis.IsCheckedChanged += (_, _) => { var i = sc!.Overlays.FindIndex(x => x.Id == oid); if (i >= 0) { sc.Overlays[i] = sc.Overlays[i] with { Visible = vis.IsChecked == true }; LiveSave(); } };
            var del = new Button { Name = "live-overlay-del-" + o.Id, Content = "✕", Padding = new Thickness(6, 0), Background = Brushes.Transparent, Foreground = Muted };
            del.Click += (_, _) => { sc!.Overlays.RemoveAll(x => x.Id == oid); LiveSave(); Show("sendung"); };
            sp.Children.Add(new DockPanel { Children = { Dk(del, Avalonia.Controls.Dock.Right), vis } });
        }
        var kinds = new[] { ("text", "Bauchbinde (Text)"), ("bild", "Bild/Logo (Datei)"), ("kamera", "Zweite Kamera (Bild im Bild)") };
        var kind = new ComboBox { Name = "live-overlay-kind", ItemsSource = kinds.Select(k => k.Item2).ToList(), SelectedIndex = Math.Max(0, Array.FindIndex(kinds, k => k.Item1 == _liveOverlayKind)), MinWidth = 170 };
        var val = new TextBox { Name = "live-overlay-value", Text = _liveOverlayDraft, PlaceholderText = "Text, Bilddatei oder Kamera", MinWidth = 170 };
        kind.SelectionChanged += (_, _) => { if (kind.SelectedIndex >= 0) _liveOverlayKind = kinds[kind.SelectedIndex].Item1; };
        val.TextChanged += (_, _) => _liveOverlayDraft = val.Text ?? "";
        var add = new Button { Name = "live-overlay-add", Content = "Einblenden" };
        add.Click += (_, _) => { try { LiveAddOverlay(_liveOverlayKind, val.Text ?? ""); } catch (Exception e) when (e is ArgumentException or InvalidOperationException or FileNotFoundException) { _liveMsg = e.Message; } Show("sendung"); };
        sp.Children.Add(new WrapPanel { Children = { kind, new Border { Width = 4 }, val, new Border { Width = 4 }, add } });
        return sp;
    }

    Control LiveMixerCard()
    {
        _liveMeters.Clear();
        var mix = LiveShowState.Mix;
        var sp = new StackPanel { Name = "live-mixer", Spacing = 6 };
        sp.Children.Add(H("Tonmischer", 14));
        var warn = new TextBlock { Name = "live-audio-warn", Text = _liveWarn ?? "", IsVisible = _liveWarn is not null, Foreground = Brush.Parse("#FFF8B26A"), FontSize = 12, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap };
        sp.Children.Add(warn);
        Control Channel(string ch, string label, double db, bool muted, bool active, Action<double> setDb, Action<bool> setMute)
        {
            var bar = new Border { Width = 2, Height = 10, CornerRadius = new CornerRadius(3), Background = Brush.Parse("#4ADE80"), HorizontalAlignment = HorizontalAlignment.Left };
            var val = new TextBlock { Text = "–", Foreground = Muted, FontSize = 11, Width = 64 };
            _liveMeters[ch] = (bar, val);
            var slider = new Slider { Name = $"live-mix-{ch}-db", Minimum = -30, Maximum = 12, Value = db, Width = 150, Margin = new Thickness(0, -10, 0, -10) };
            slider.PointerCaptureLost += (_, _) => setDb(Math.Round(slider.Value, 1));
            ToolTip.SetTip(slider, "Lautstärke in dB");
            var mute = new Button { Name = $"live-mix-{ch}-mute", Content = muted ? "🔇" : "🔊", Width = 34, Padding = new Thickness(0), HorizontalContentAlignment = HorizontalAlignment.Center, Background = muted ? Brush.Parse("#55EF4444") : Brush.Parse("#22FFFFFF") };
            ToolTip.SetTip(mute, muted ? "Stumm – antippen zum Einschalten" : "Stumm schalten");
            mute.Click += (_, _) => { setMute(!muted); Show("sendung"); };
            return new Border { CornerRadius = new CornerRadius(8), Background = Brush.Parse("#10FFFFFF"), Padding = new Thickness(8, 6), Opacity = active ? 1 : 0.55,
                Child = new StackPanel { Spacing = 3, Children = {
                    new DockPanel { Children = { Dk(new TextBlock { Text = $"{db:+0.0;−0.0;0} dB", Foreground = Muted, FontSize = 11 }, Avalonia.Controls.Dock.Right), new TextBlock { Text = label + (active ? "" : " (aus)"), Foreground = Text, FontSize = 12.5, FontWeight = FontWeight.SemiBold } } },
                    new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { mute, slider } },
                    new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { new Border { Width = 180, Height = 10, CornerRadius = new CornerRadius(3), Background = Brush.Parse("#22FFFFFF"), Child = bar }, val } } } } };
        }
        sp.Children.Add(Channel("mikrofon", "Mikrofon", mix.MicDb, mix.MicMute, _liveMic, d => LiveSetMix(LiveShowState.Mix with { MicDb = d }), m => LiveSetMix(LiveShowState.Mix with { MicMute = m })));
        sp.Children.Add(Channel("musik", "Musik", mix.MusicDb, mix.MusicMute, mix.MusicFile is { Length: > 0 }, d => LiveSetMix(LiveShowState.Mix with { MusicDb = d }), m => LiveSetMix(LiveShowState.Mix with { MusicMute = m })));
        var music = new TextBox { Name = "live-music-file", Text = mix.MusicFile ?? "", PlaceholderText = "Musik im Hintergrund (MP3/WAV, läuft in Schleife)", MinWidth = 220 };
        var take = new Button { Name = "live-music-set", Content = "Übernehmen" };
        take.Click += (_, _) =>
        {
            var f = (music.Text ?? "").Trim().Trim('"');
            if (f.Length > 0 && !File.Exists(f)) { _liveMsg = "Musikdatei nicht gefunden."; Show("sendung"); return; }
            LiveSetMix(LiveShowState.Mix with { MusicFile = f.Length == 0 ? null : f }); _liveMsg = f.Length == 0 ? "Musik aus." : $"Musik „{Path.GetFileName(f)}“ läuft bei Aufnahme und Sendung mit."; Show("sendung");
        };
        sp.Children.Add(new WrapPanel { Children = { music, new Border { Width = 4 }, take } });
        sp.Children.Add(P("Pegel zeigen die Lautheit während Probelauf, Aufnahme und Sendung: grün gut, gelb kräftig, rot zu laut.", Muted, 11));
        return WCard(sp);
    }

    Control LiveProbeCard()
    {
        var sp = new StackPanel { Name = "live-probe", Spacing = 4 };
        sp.Children.Add(H("Probelauf", 14));
        sp.Children.Add(P("Vor dem Senden: kurze Probeaufnahme mit Szene und Ton, dann prüft Trendo Bild, Ton und Datenrate.", Muted, 11));
        var go = LiveBtn("live-probe-run", _liveProbing ? "Probelauf läuft …" : "Probelauf starten", LiveProbeRun, true); go.IsEnabled = !_liveProbing;
        sp.Children.Add(go);
        foreach (var c in _liveProbe ?? [])
            sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = {
                new Border { Width = 10, Height = 10, CornerRadius = new CornerRadius(5), VerticalAlignment = VerticalAlignment.Center, Background = Brush.Parse(c.Light switch { "rot" => "#EF4444", "gelb" => "#FACC15", _ => "#4ADE80" }) },
                new TextBlock { Text = c.Text, Foreground = Text, FontSize = 12, TextWrapping = TextWrapping.Wrap, MaxWidth = 380 } } });
        return WCard(sp);
    }

    Control LivePlanCard()
    {
        var sp = new StackPanel { Name = "live-plan", Spacing = 4 };
        sp.Children.Add(H("Sendeplan", 14));
        sp.Children.Add(new TextBlock { Name = "live-plan-now", Text = "", Foreground = Brush.Parse("#F1C870"), FontSize = 12.5, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
        var show = LiveShowState;
        if (show.Plan.Count == 0) sp.Children.Add(P("Noch kein Sendeplan. Zeilen wie „00:00 Gleich geht's los – Begrüßung“ – oder Claude fragen: „Bau mir Szenen und einen Sendeplan für …“.", Muted, 11.5));
        foreach (var c in show.Plan)
        {
            var sid = c.Scene; var name = show.Scenes.FirstOrDefault(s => s.Id == c.Scene)?.Name ?? c.Scene;
            var go = new Button { Name = $"live-plan-go-{(int)c.At.TotalSeconds}", Content = "Umschalten", FontSize = 11, Padding = new Thickness(6, 1) };
            go.Click += (_, _) => { try { LiveSelectScene(sid); } catch (InvalidOperationException e) { _liveMsg = e.Message; Show("sendung"); } };
            sp.Children.Add(new DockPanel { Children = { Dk(go, Avalonia.Controls.Dock.Right),
                new TextBlock { Text = $"{(int)c.At.TotalMinutes:00}:{c.At.Seconds:00}  {name}" + (c.Note.Length > 0 ? " – " + c.Note : ""), Foreground = Text, FontSize = 12, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center } } });
        }
        var box = new TextBox { Name = "live-plan-text", Text = LiveProbe.PlanText(show.Plan, show.Scenes).Replace(" / ", "\n"), AcceptsReturn = true, MinHeight = 60, PlaceholderText = "00:00 Gleich geht's los – Begrüßung\n02:00 Kamera – Thema vorstellen" };
        var fromRundown = new Button { Name = "live-plan-from-ablauf", Content = "Aus dem Ablauf" };
        ToolTip.SetTip(fromRundown, "Programmpunkte aus „Ablauf“ übernehmen – eine Szene, deren Name im Titel oder Hinweis steht, wird zugeordnet");
        fromRundown.Click += (_, _) => { try { LivePlanFromRundown(); } catch (InvalidOperationException e) { _liveMsg = e.Message; } Show("sendung"); };
        var set = new Button { Name = "live-plan-set", Content = "Sendeplan übernehmen" };
        set.Click += (_, _) => { try { LiveSetPlan(box.Text ?? ""); _liveMsg = $"Sendeplan mit {LiveShowState.Plan.Count} Punkten."; } catch (InvalidOperationException e) { _liveMsg = e.Message; } Show("sendung"); };
        sp.Children.Add(box); sp.Children.Add(new WrapPanel { Children = { set, new Border { Width = 6 }, fromRundown } });
        return WCard(sp);
    }

    /// <summary>Anordnung wie OBS: oben Programmbild und Steuerung, darunter Szenen · Quellen · Tonmischer, dann Sendeplan und Werkzeuge.</summary>
    Control LiveLayout(Control controls, Control sources, Control picture, Control live, Control tools)
    {
        Control Col(int c, Control x) { Grid.SetColumn(x, c); return x; }
        var top = new Grid { ColumnDefinitions = new ColumnDefinitions("3*,12,2*"), Children = { Col(0, picture), Col(2, new StackPanel { Spacing = 8, Children = { controls, LiveProbeCard(), live } }) } };
        var mid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,12,*,12,*"), Children = { Col(0, LiveScenesCard()), Col(2, sources), Col(4, LiveMixerCard()) } };
        var bottom = new Grid { ColumnDefinitions = new ColumnDefinitions("3*,12,2*"), Children = { Col(0, LivePlanCard()), Col(2, tools) } };
        LiveMeterTick();
        return new StackPanel { Name = "live-studio", Spacing = 10, Children = { top, mid, bottom } };
    }
}
