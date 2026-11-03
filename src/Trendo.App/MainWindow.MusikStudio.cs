using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Studio „Trendo Musik“ (freigegeben 03.11.2026): Lieder mit ACE-Step 1.5 erzeugen – Bedienkonzept wie bei bekannten Musik-KI-Diensten
/// (Clean-Room, ohne deren Namen oder Aussehen): links Erstellen (Einfach/Profi), Mitte Bibliothek, rechts das gewählte Lied mit Liedtext und Bearbeiten, unten Player.
/// Alles lokal auf dem PC. Nur Funktionen, die am 03.11.2026 echt erprobt wurden: Varianten, 50 Sprachen, Abschnitt neu erzeugen, Cover.</summary>
public partial class MainWindow
{
    public const string MusikStudio = "STUDIO-MUSIK-01";
    const string MS = "musik-studio";
    SongLibrary Songs => new(_core.DataRoot);
    bool _msPro; string _msMsg = ""; string? _msSel; bool _msFav; string _msSearch = "";
    readonly List<(string Title, DateTime Started)> _msRunning = [];
    // Eingaben bleiben beim Neuzeichnen erhalten
    string _msDesc = "", _msStyle = "", _msLyrics = "", _msTitle = "", _msBpm = "", _msKey = "", _msTime = ""; int _msLang, _msLen = 3, _msVariants = 2; bool _msInstr;
    /// <summary>Für Tests: Liedtext-Vorschlag ohne echte KI.</summary>
    public Func<string, Task<string>>? MusicLyricsOverride { get; set; }
    public static Action<string>? SongRecycleOverride { get; set; }

    /// <summary>Lied auswählen (öffentlich für Tests und Vorschaubilder).</summary>
    public void MusicStudioSelect(string id) => _msSel = id;
    public void MusicStudioProMode(bool pro) => _msPro = pro;

    void InitMusikStudio()
    {
        RegisterTab(KreativTabs, MS, "Trendo Musik", MusikStudio, MusikStudioView, "musik", "lied", "song", "songtext", "komponieren", "musik ki", "ace-step", "gesang", "beat");
    }

    // ───────────── Erzeugen ─────────────
    /// <summary>Lied erzeugen (öffentlich für Tests und die KI): Auftrag an ACE-Step, Varianten laden, in die Bibliothek.</summary>
    public async Task<List<MusicSong>> MusicStudioCreate(AceStepClient.Job job, string title, string? fromSongId = null)
    {
        var made = new List<MusicSong>();
        var src = fromSongId is null ? null : Songs.Get(fromSongId);
        var kind = job.Task switch { "repaint" => "abschnitt", "cover" => "cover", _ => "neu" };
        _msRunning.Add((title, DateTime.Now)); _msMsg = ""; Show(MS);
        try
        {
            if (!await EnsureAceServer(t => { _msMsg = t; if (_msRunningText is { } rt0) rt0.Text = t; })) { _msMsg = string.IsNullOrEmpty(_msMsg) ? "ACE-Step ist nicht bereit – in der KI-Bibliothek unter „Musik“ einrichten." : _msMsg; return made; }
            var client = AceClient;
            var task = await client.SubmitAsync(job, src?.File);
            _core.Audit.Append(Principal.LocalOwner.Name, "musik.studio.start", $"{job.Task} {job.Seconds}s {job.Language} x{job.Variants}");
            var t0 = DateTime.UtcNow;
            while (true)
            {
                await Task.Delay(AceClientOverride is null ? 3000 : 10);
                var st = await client.QueryAsync(task);
                if (st.Status == 2) throw new InvalidOperationException(st.Error ?? "Erzeugen fehlgeschlagen.");
                if (st.Status == 1 && st.AudioUrls.Count > 0)
                {
                    var dir = WorkFolder("Musik"); var stem = DevStudio.SafeName(title);
                    for (var i = 0; i < st.AudioUrls.Count; i++)
                    {
                        var file = Path.Combine(dir, $"{stem}{(st.AudioUrls.Count > 1 ? $" ({i + 1})" : "")}{(kind == "neu" ? "" : " – " + kind)}.{job.Format}");
                        for (var n = 2; File.Exists(file); n++) file = Path.Combine(dir, $"{stem} {n}{(st.AudioUrls.Count > 1 ? $" ({i + 1})" : "")}.{job.Format}");
                        await client.DownloadAsync(st.AudioUrls[i], file);
                        made.Add(Songs.Add(new MusicSong { Title = title + (st.AudioUrls.Count > 1 ? $" ({i + 1})" : "") + (kind == "abschnitt" ? " – neuer Abschnitt" : kind == "cover" ? " – Cover" : ""),
                            Style = job.SampleQuery is null ? job.Prompt : job.SampleQuery.Split(", sung in")[0], Lyrics = job.SampleQuery is not null && st.Lyrics is { Length: > 0 } sung ? sung : job.Lyrics,
                            Language = job.Language, Seconds = job.SampleQuery is not null && st.Duration is double dur && dur > 0 ? dur : job.Seconds, Bpm = job.Bpm, Key = job.Key, TimeSignature = job.TimeSignature,
                            File = file, Kind = kind, ParentId = src?.Id }));
                    }
                    _msSel = made[0].Id;
                    if (CoverImageOverride is not null || (!_opt.Isolated && CoverModel() is not null)) _ = MsMakeCover(made[0].Id, auto: true); // gleich ein passendes Cover dazu
                    _msMsg = made.Count > 1 ? $"✓ {made.Count} Varianten fertig – anhören und die schönere behalten." : "✓ Fertig.";
                    _core.Audit.Append(Principal.LocalOwner.Name, "musik.studio.fertig", $"{kind} {made.Count}");
                    return made;
                }
                if (DateTime.UtcNow - t0 > MusicTimeout(job.SampleQuery is null ? job.Seconds : 600)) throw new InvalidOperationException("Das dauert ungewöhnlich lange – bitte erneut versuchen.");
                if (CurrentView == MS && _msRunningText is { } rt) rt.Text = $"„{title}“ entsteht … {(int)(DateTime.UtcNow - t0).TotalSeconds} s";
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or ArgumentException or IOException or TaskCanceledException or System.Text.Json.JsonException)
        {
            _msMsg = "Hat nicht geklappt: " + ex.Message;
            _core.Audit.Append(Principal.LocalOwner.Name, "musik.studio.fehler", ex.GetType().Name);
            return made;
        }
        finally { _msRunning.RemoveAll(r => r.Title == title); Show(MS); }
    }
    TextBlock? _msRunningText;

    /// <summary>Auftrag aus den Eingaben bauen. Einfach: die Beschreibung ist der Stil, Text schreibt ACE-Step selbst (oder instrumental).</summary>
    AceStepClient.Job MsJob()
    {
        var lang = AceStepClient.Languages[Math.Clamp(_msLang, 0, AceStepClient.Languages.Length - 1)].Code;
        var secs = MusicLengths[Math.Clamp(_msLen, 0, MusicLengths.Length - 1)].Seconds;
        if (!_msPro)
        {
            if (string.IsNullOrWhiteSpace(_msDesc) && _msGenres.Count == 0) throw new ArgumentException("Beschreibe zuerst dein Lied – z. B. „fröhliches Sommerlied über einen Roadtrip“.");
            return new(MsWithExtras(_msDesc), _msInstr ? "[Instrumental]" : "", secs, lang, _msVariants);
        }
        if (string.IsNullOrWhiteSpace(_msStyle) && _msGenres.Count == 0) throw new ArgumentException("Gib zuerst den Stil an – z. B. „Pop, Akustikgitarre, Frauenstimme“.");
        int? bpm = int.TryParse(_msBpm, out var b) ? b : null;
        if (_msBpm.Length > 0 && bpm is null or < 30 or > 300) throw new ArgumentException("Tempo bitte zwischen 30 und 300 BPM (oder leer für automatisch).");
        return new(MsWithExtras(_msStyle), _msInstr ? "[Instrumental]" : SongLibrary.ToAceLyrics(_msLyrics), secs, lang, _msVariants, bpm,
            _msKey.Length > 0 ? _msKey : null, _msTime.Length > 0 ? _msTime : null);
    }

    /// <summary>Wunsch 03.11.2026: Im Einfach-Modus mit Gesang (nicht nur instrumental). Zuerst schreibt die Text-KI von Trendo den Liedtext
    /// (dann gilt die gewählte Länge genau); ohne Text-KI schreibt ACE-Step im eigenen Einfach-Modus Text und Gesang selbst – die Länge wählt es dann selbst.</summary>
    public async Task<List<MusicSong>> MsCreateWithVoice(AceStepClient.Job j, string title)
    {
        if (_msPro || j.Lyrics.Trim().Length > 0 || j.Task != "text2music") return await MusicStudioCreate(j, title);
        var langName = AceStepClient.Languages.FirstOrDefault(x => x.Code == j.Language).Name ?? j.Language;
        var english = j.Language switch { "de" => "German", "en" => "English", _ => TryCulture(j.Language) };
        var prompt = $"Schreibe einen Liedtext auf {langName} für ein Lied von etwa {j.Seconds} Sekunden. Thema/Stil: {j.Prompt}. "
            + "Aufbau mit Abschnitts-Marken in eckigen Klammern auf eigener Zeile, z. B. [Strophe], [Refrain], [Strophe], [Refrain]" + (j.Seconds >= 150 ? ", [Bridge], [Refrain]" : "") + ". "
            + "Kurze, singbare Zeilen, gute Reime, keine Namen echter Künstler, keine Zeilen aus bekannten Liedern. Gib nur den Liedtext aus.";
        _msMsg = "Der Liedtext entsteht …"; Show(MS);
        string? text = null;
        try
        {
            text = MusicLyricsOverride is { } o ? await o(prompt)
                : _kiConnected || !_opt.Isolated ? (await Intel.AskAsync(new KiRequest(prompt, PreferredModel: KiModelName, UseKnowledge: false, AllowRemote: true)) is { Ok: true } a ? a.Text : null) : null;
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException) { text = null; }
        text = text?.Trim().Trim('`').Trim();
        if (text is { Length: > 20 } && text.Contains('['))
        {
            _core.Audit.Append(Principal.LocalOwner.Name, "musik.studio.einfach", "text-ki");
            return await MusicStudioCreate(j with { Lyrics = SongLibrary.ToAceLyrics(text) }, title);
        }
        // ohne Text-KI: ACE-Step schreibt und singt selbst
        _core.Audit.Append(Principal.LocalOwner.Name, "musik.studio.einfach", "ace-sample");
        var made = await MusicStudioCreate(j with { SampleQuery = $"{j.Prompt}, sung in {english}" }, title);
        if (made.Count > 0) _msMsg += " Die Musik-KI hat Text und Gesang selbst geschrieben und die Länge selbst gewählt.";
        return made;

        static string TryCulture(string code) { try { return System.Globalization.CultureInfo.GetCultureInfo(code).EnglishName; } catch (System.Globalization.CultureNotFoundException) { return code; } }
    }

    /// <summary>Liedtext mit der KI (Claude, wenn verbunden) – Aufbau mit Abschnitten, in der gewählten Sprache.</summary>
    async Task MsWriteLyrics()
    {
        var topic = (_msPro ? _msStyle : _msDesc).Trim();
        if (topic.Length == 0 && _msLyrics.Trim().Length == 0) { _msMsg = "Schreib zuerst, worum es im Lied gehen soll."; Show(MS); return; }
        var lang = AceStepClient.Languages[Math.Clamp(_msLang, 0, AceStepClient.Languages.Length - 1)].Name;
        var prompt = $"Schreibe einen Liedtext auf {lang}. Thema/Stil: {topic}. " + (_msLyrics.Trim().Length > 0 ? $"Verbessere diesen Entwurf und behalte seine Idee: {_msLyrics}. " : "")
            + "Aufbau mit Abschnitts-Marken in eckigen Klammern auf eigener Zeile: [Strophe], [Refrain], [Strophe], [Refrain], [Bridge], [Refrain]. Kurze, singbare Zeilen, gute Reime, "
            + "keine Namen echter Künstler, keine Zeilen aus bekannten Liedern. Gib nur den Liedtext aus.";
        _msMsg = "Der Liedtext entsteht …"; Show(MS);
        try
        {
            var text = MusicLyricsOverride is { } o ? await o(prompt) : await Intel.AskAsync(new KiRequest(prompt, PreferredModel: KiModelName, UseKnowledge: false, AllowRemote: true)) is { Ok: true } a ? a.Text : throw new InvalidOperationException("Die KI hat gerade keinen Text geliefert.");
            _msLyrics = text.Trim().Trim('`').Trim(); _msPro = true; _msMsg = "✓ Liedtext eingesetzt – anpassen und dann „Erstellen“.";
            _core.Audit.Append(Principal.LocalOwner.Name, "musik.studio.liedtext", lang);
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException) { _msMsg = "Liedtext ging nicht: " + ex.Message; }
        Show(MS);
    }

    // ───────────── Oberfläche ─────────────
    Control MusikStudioView()
    {
        var root = new Grid { ColumnDefinitions = new ColumnDefinitions("360,14,*,14,340"), RowDefinitions = new RowDefinitions("*,Auto") };
        root.Children.Add(MsCreatePanel());
        root.Children.Add(Pos(MsLibraryPanel(), 2));
        root.Children.Add(Pos(MsDetailPanel(), 4));
        var player = MsPlayerBar(); Grid.SetRow(player, 1); Grid.SetColumnSpan(player, 5); root.Children.Add(player);
        root.VerticalAlignment = VerticalAlignment.Stretch;
        return FitPage("Trendo Musik", new Panel(), root); // füllt das Fenster wie ein echtes Programm
    }

    Border MsCard(string name, Control child) => new() { Name = name, CornerRadius = new CornerRadius(16), Background = TileBg, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), BoxShadow = CardShadow, Padding = new Thickness(14), Child = child };

    Control MsCreatePanel()
    {
        var sp = new StackPanel { Spacing = 8 };
        // Einfach | Profi
        var modes = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        foreach (var (pro, label) in new[] { (false, "Einfach"), (true, "Profi") })
        {
            var on = _msPro == pro; var p = pro;
            var b = new Button { Name = "ms-mode-" + (pro ? "profi" : "einfach"), Content = label, Padding = new Thickness(16, 5), CornerRadius = new CornerRadius(14), Background = on ? Accent : Brush.Parse("#22FFFFFF"), Foreground = on ? Brushes.White : Text, BorderThickness = new Thickness(0) };
            b.Click += (_, _) => { _msPro = p; Show(MS); };
            modes.Children.Add(b);
        }
        sp.Children.Add(new DockPanel { Children = { Dk(modes, Avalonia.Controls.Dock.Right), H("Erstellen", 16) } });
        var sync = new List<Action>(); // beim Klick die Felder direkt auslesen – nicht auf das Änderungs-Ereignis warten
        TextBox Box(string name, string watermark, string value, Action<string> set, double minH = 0, bool multi = false)
        {
            var t = new TextBox { Name = name, Watermark = watermark, Text = value, AcceptsReturn = multi, TextWrapping = TextWrapping.Wrap, MinHeight = minH };
            t.TextChanged += (_, _) => set(t.Text ?? ""); sync.Add(() => set(t.Text ?? "")); return t;
        }
        void Sync() { foreach (var a in sync) a(); }
        if (!_msPro)
        {
            sp.Children.Add(P("Beschreibe dein Lied in einem Satz – Stil, Stimmung, Thema. Den Text schreibt die KI selbst.", Muted, 12));
            sp.Children.Add(Box("ms-desc", "z. B. fröhliches Sommerlied über einen Roadtrip mit Akustikgitarre", _msDesc, v => _msDesc = v, 90, true));
        }
        else
        {
            sp.Children.Add(Box("ms-title", "Titel (optional)", _msTitle, v => _msTitle = v));
            var style = Box("ms-style", "Stil: z. B. Pop, Akustikgitarre, Frauenstimme, fröhlich", _msStyle, v => _msStyle = v, 54, true);
            sp.Children.Add(style);
            var chips = new WrapPanel { Name = "ms-chips" };
            foreach (var c in SongLibrary.StyleChips.Take(20))
            {
                var cc = c; var b = new Button { Content = c, FontSize = 11.5, Padding = new Thickness(8, 2), Margin = new Thickness(0, 0, 4, 4), CornerRadius = new CornerRadius(10), Background = Brush.Parse("#18FFFFFF"), BorderThickness = new Thickness(0) };
                b.Click += (_, _) => { Sync(); _msStyle = string.IsNullOrWhiteSpace(_msStyle) ? cc : _msStyle.TrimEnd(' ', ',') + ", " + cc; Show(MS); };
                chips.Children.Add(b);
            }
            sp.Children.Add(chips);
            var lyr = Box("ms-lyrics", "Liedtext – leer lassen, dann schreibt die KI.\n[Strophe]\n…\n[Refrain]\n…", _msLyrics, v => _msLyrics = v, 150, true);
            lyr.IsEnabled = !_msInstr;
            var tags = new WrapPanel();
            foreach (var (label, _) in SongLibrary.Sections)
            {
                var l = label; var b = new Button { Name = "ms-tag-" + l, Content = "[" + l + "]", FontSize = 11, Padding = new Thickness(6, 1), Margin = new Thickness(0, 0, 4, 4), Background = Brushes.Transparent };
                b.Click += (_, _) => { Sync(); _msLyrics = (_msLyrics.TrimEnd() + "\n[" + l + "]\n").TrimStart(); Show(MS); };
                tags.Children.Add(b);
            }
            var claude = Act("ms-lyrics-ki", "✳ Liedtext mit KI", () => { Sync(); _ = MsWriteLyrics(); });
            sp.Children.Add(new StackPanel { Spacing = 4, Children = { new DockPanel { Children = { Dk(claude, Avalonia.Controls.Dock.Right), H("Liedtext", 13) } }, lyr, tags, MsLyricsTools(Sync) } });
            sp.Children.Add(MsFactsPanel(Sync)); // M7: Tonart und Tempo eines bekannten Songs
            // Tempo, Tonart, Takt
            var bpm = Box("ms-bpm", "BPM auto", _msBpm, v => _msBpm = v.Trim()); bpm.Width = 88; ToolTip.SetTip(bpm, "Tempo in Schlägen pro Minute (30–300). Leer = die KI wählt.");
            var key = new ComboBox { Name = "ms-key", Width = 118, ItemsSource = new[] { "Tonart" }.Concat(AceStepClient.Keys.Select(KeyDe)).ToArray(), SelectedIndex = _msKey.Length == 0 ? 0 : 1 + Array.IndexOf(AceStepClient.Keys, _msKey) };
            key.SelectionChanged += (_, _) => _msKey = key.SelectedIndex <= 0 ? "" : AceStepClient.Keys[key.SelectedIndex - 1];
            var ts = new ComboBox { Name = "ms-time", Width = 92, ItemsSource = new[] { "Takt" }.Concat(AceStepClient.TimeSignatures.Select(x => x.Label)).ToArray(), SelectedIndex = _msTime.Length == 0 ? 0 : 1 + Array.FindIndex(AceStepClient.TimeSignatures, x => x.Value == _msTime) };
            ts.SelectionChanged += (_, _) => _msTime = ts.SelectedIndex <= 0 ? "" : AceStepClient.TimeSignatures[ts.SelectedIndex - 1].Value;
            sp.Children.Add(new WrapPanel { Children = { bpm, Spacer(key), Spacer(ts) } });
        }
        sp.Children.Add(MsStylePanel(Sync)); // M1/M2/M6/M8: Genres, Stimme, Tonlage, Instrumente, „Im Stil von“
        var instr = new CheckBox { Name = "ms-instrumental", Content = "Instrumental (ohne Gesang)", IsChecked = _msInstr, Foreground = Text };
        instr.IsCheckedChanged += (_, _) => { _msInstr = instr.IsChecked == true; Show(MS); };
        var lang = new ComboBox { Name = "ms-language", Width = 150, ItemsSource = AceStepClient.Languages.Select(x => x.Name).ToArray(), SelectedIndex = _msLang };
        lang.SelectionChanged += (_, _) => _msLang = lang.SelectedIndex;
        var len = new ComboBox { Name = "ms-length", Width = 130, ItemsSource = MusicLengths.Select(x => x.Label).ToArray(), SelectedIndex = _msLen };
        len.SelectionChanged += (_, _) => _msLen = len.SelectedIndex;
        var vars = new ComboBox { Name = "ms-variants", Width = 120, ItemsSource = new[] { "1 Fassung", "2 Varianten" }, SelectedIndex = _msVariants - 1 };
        vars.SelectionChanged += (_, _) => _msVariants = vars.SelectedIndex + 1;
        sp.Children.Add(instr);
        sp.Children.Add(new WrapPanel { Children = { lang, Spacer(len), Spacer(vars) } });
        var go = new Button { Name = "ms-create", Content = "✨ Erstellen", FontSize = 15, FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center,
            Padding = new Thickness(0, 10), CornerRadius = new CornerRadius(12), Background = Accent, Foreground = Brushes.White, BorderThickness = new Thickness(0), Margin = new Thickness(0, 6, 0, 0) };
        go.Click += (_, _) =>
        {
            Sync();
            try { var j = MsJob(); var title = _msPro && _msTitle.Trim().Length > 0 ? _msTitle.Trim() : SongLibrary.TitleFrom(_msPro ? _msStyle : _msDesc, _msPro ? _msLyrics : ""); _ = MsCreateWithVoice(j, title); }
            catch (ArgumentException ex) { _msMsg = ex.Message; Show(MS); }
        };
        sp.Children.Add(go);
        sp.Children.Add(P(MusicLengths[Math.Clamp(_msLen, 0, MusicLengths.Length - 1)].Seconds > AceStepClient.ThinkingMaxSeconds
            ? "Über 8 Minuten plant die KI das Stück nicht mit – sonst würde sie es kürzen." : "Läuft auf deinem PC (ACE-Step). Ein Lied dauert meist unter einer Minute.", Muted, 11));
        if (_msMsg.Length > 0) sp.Children.Add(new TextBlock { Name = "ms-msg", Text = _msMsg, Foreground = Text, FontSize = 12.5, TextWrapping = TextWrapping.Wrap });
        return MsCard("ms-create-panel", new ScrollViewer { Content = sp });

        static Control Spacer(Control c) { c.Margin = new Thickness(6, 0, 0, 0); return c; }
    }

    static string KeyDe(string k) => k.Replace(" major", "-Dur").Replace(" minor", "-Moll").Replace("#", "♯").Replace("Eb", "E♭").Replace("Ab", "A♭").Replace("Bb", "B");

    /// <summary>Titelbild: das Cover-Bild des Lieds (siehe MusikCover), sonst ruhiger Farbverlauf je Lied mit Notenzeichen.</summary>
    Border MsCover(MusicSong s, double size)
    {
        if (File.Exists(s.CoverFile))
            try { return new Border { Width = size, Height = size, CornerRadius = new CornerRadius(size / 10), ClipToBounds = true, BoxShadow = BoxShadows.Parse("0 3 10 0 #66000000"),
                Child = new Image { Source = new Avalonia.Media.Imaging.Bitmap(s.CoverFile), Stretch = Stretch.UniformToFill } }; }
            catch (Exception e) when (e is IOException or ArgumentException or InvalidOperationException) { }
        var palette = new[] { ("#C9A227", "#5B3A00"), ("#2F86F6", "#0B1E46"), ("#E0475B", "#3B0A1A"), ("#22C55E", "#08321A"), ("#7C3AED", "#1E0B46"), ("#F59E0B", "#3D1E00"), ("#14B8A6", "#05302C"), ("#EC4899", "#3D0726") };
        var (a, b) = palette[Math.Abs(s.Id.GetHashCode() % palette.Length)];
        return new Border { Width = size, Height = size, CornerRadius = new CornerRadius(size / 8),
            Background = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative), GradientStops = { new GradientStop(Color.Parse(a), 0), new GradientStop(Color.Parse(b), 1) } },
            Child = new TextBlock { Text = s.Kind == "cover" ? "♫" : "♪", FontSize = size * 0.42, Foreground = Brush.Parse("#CCFFFFFF"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
    }

    Control MsLibraryPanel()
    {
        var sp = new StackPanel { Spacing = 6 };
        var search = new TextBox { Name = "ms-search", Watermark = "In deinen Liedern suchen", Text = _msSearch, Width = 200 };
        var import = new Button { Name = "ms-import", Content = "＋ Eigene Aufnahme" }; // M3
        ToolTip.SetTip(import, "Eine Aufnahme, die du hast (eigene oder gekaufte Datei), hinzufügen – zum Analysieren und für ein Cover in anderem Stil (nur privat)");
        import.Click += async (_, _) => await MsImport();
        search.TextChanged += (_, _) => { _msSearch = search.Text ?? ""; };
        search.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Enter) Show(MS); };
        var fav = new Avalonia.Controls.Primitives.ToggleButton { Name = "ms-fav-filter", Content = "★ Favoriten", IsChecked = _msFav };
        fav.IsCheckedChanged += (_, _) => { _msFav = fav.IsChecked == true; Show(MS); };
        sp.Children.Add(new DockPanel { Children = { Dk(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { import, search, fav } }, Avalonia.Controls.Dock.Right), H("Meine Lieder", 16) } });
        foreach (var (title, started) in _msRunning)
        {
            _msRunningText = new TextBlock { Name = "ms-running", Text = $"„{title}“ entsteht … {(int)(DateTime.Now - started).TotalSeconds} s", Foreground = Text, FontSize = 13 };
            sp.Children.Add(new Border { CornerRadius = new CornerRadius(12), Background = Brush.Parse("#22C9A227"), Padding = new Thickness(12, 10), Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { new ProgressBar { IsIndeterminate = true, Width = 60, VerticalAlignment = VerticalAlignment.Center }, _msRunningText } } });
        }
        var songs = Songs.Visible(_msFav, _msSearch);
        if (songs.Count == 0 && _msRunning.Count == 0)
            sp.Children.Add(new StackPanel { Spacing = 6, Margin = new Thickness(0, 40, 0, 0), HorizontalAlignment = HorizontalAlignment.Center, Children = {
                new TextBlock { Text = "♪", FontSize = 46, Foreground = Accent, HorizontalAlignment = HorizontalAlignment.Center },
                new TextBlock { Name = "ms-empty", Text = _msFav || _msSearch.Length > 0 ? "Nichts gefunden." : "Noch keine Lieder – links beschreiben und „Erstellen“.", Foreground = Muted, FontSize = 14, HorizontalAlignment = HorizontalAlignment.Center } } });
        var i = 0;
        foreach (var s in songs)
        {
            var song = s; var n = i++; var sel = s.Id == _msSel; var playing = _queue.Current is { } c && string.Equals(c, s.File, StringComparison.OrdinalIgnoreCase);
            var play = new Button { Name = $"ms-play-{n}", Content = playing && !_mPaused ? "⏸" : "▶", Width = 36, Height = 36, CornerRadius = new CornerRadius(18), Padding = new Thickness(0), FontSize = 14,
                HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center, Background = Accent, Foreground = Brushes.White, BorderThickness = new Thickness(0) };
            play.Click += (_, _) => MsPlay(song);
            var star = new Button { Name = $"ms-star-{n}", Content = s.Favorite ? "★" : "☆", Background = Brushes.Transparent, Foreground = s.Favorite ? Accent : Muted, FontSize = 16, BorderThickness = new Thickness(0) };
            star.Click += (_, _) => { Songs.ToggleFavorite(song.Id); Show(MS); };
            var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = {
                new TextBlock { Text = s.Title, Foreground = Text, FontSize = 14, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis },
                new TextBlock { Text = s.Style, Foreground = Muted, FontSize = 11.5, TextTrimming = TextTrimming.CharacterEllipsis },
                new TextBlock { Text = $"{SongTime(s.Seconds)} · {AceStepClient.Languages.FirstOrDefault(x => x.Code == s.Language).Name ?? s.Language}{(s.Kind == "neu" ? "" : s.Kind == "cover" ? " · Cover" : " · neuer Abschnitt")} · {s.Created:dd.MM. HH:mm}", Foreground = Muted, FontSize = 11 } } };
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto"), ColumnSpacing = 10, Children = { MsCover(s, 52), Pos(play, 1), Pos(info, 2), Pos(star, 3) } };
            play.VerticalAlignment = VerticalAlignment.Center; star.VerticalAlignment = VerticalAlignment.Center;
            var item = new Button { Name = $"ms-song-{n}", Content = row, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(8),
                CornerRadius = new CornerRadius(12), Background = sel ? Brush.Parse("#26C9A227") : Brushes.Transparent, BorderBrush = sel ? Accent : Brushes.Transparent, BorderThickness = new Thickness(1) };
            item.Click += (_, _) => { _msSel = song.Id; Show(MS); };
            sp.Children.Add(item);
        }
        return MsCard("ms-library", new ScrollViewer { Content = sp });
    }

    static string SongTime(double s) => TimeSpan.FromSeconds(s).ToString(s >= 3600 ? @"h\:mm\:ss" : @"m\:ss");

    void MsPlay(MusicSong s)
    {
        if (_queue.Current is { } c && string.Equals(c, s.File, StringComparison.OrdinalIgnoreCase)) { MusicTogglePause(); return; }
        var list = Songs.Visible(_msFav, _msSearch).Select(x => x.File).ToList();
        MusicPlay(list, Math.Max(0, list.IndexOf(s.File)), MS);
    }

    Control MsDetailPanel()
    {
        var s = _msSel is null ? null : Songs.Get(_msSel);
        if (s is null || !File.Exists(s.File))
            return MsCard("ms-detail", new StackPanel { Spacing = 8, Children = { H("Lied", 16), P("Wähle in der Mitte ein Lied – hier siehst du dann den Liedtext und kannst es weiterbearbeiten.", Muted, 12.5) } });
        var sp = new StackPanel { Spacing = 8 };
        sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children = { MsCover(s, File.Exists(s.CoverFile) ? 120 : 84),
            new StackPanel { VerticalAlignment = VerticalAlignment.Center, Width = 210, Children = { new TextBlock { Name = "ms-detail-title", Text = s.Title, Foreground = Text, FontSize = 16, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap },
                new TextBlock { Text = s.Style, Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap } } } } });
        var lyr = string.IsNullOrWhiteSpace(s.Lyrics) ? "(Text von der KI)" : s.Lyrics == "[Instrumental]" ? "Instrumental" : s.Lyrics;
        sp.Children.Add(new Border { CornerRadius = new CornerRadius(10), Background = Brush.Parse("#14FFFFFF"), Padding = new Thickness(10), MaxHeight = 220,
            Child = new ScrollViewer { Content = new SelectableTextBlock { Name = "ms-detail-lyrics", Text = lyr, Foreground = Text, FontSize = 12.5, TextWrapping = TextWrapping.Wrap } } });
        sp.Children.Add(MsCoverPanel(s)); // Wunsch 03.11.2026: Cover-Bild wie ein echtes Song-Cover
        sp.Children.Add(MsAnalysePanel(s)); // M5: Tonart, Tempo, Takt, Akkorde, Tonumfang

        // Abschnitt neu erzeugen
        var from = new NumericUpDown { Name = "ms-repaint-from", Minimum = 0, Maximum = (decimal)Math.Max(1, s.Seconds - 1), Value = 0, Increment = 1, FormatString = "0", Width = 100 };
        var to = new NumericUpDown { Name = "ms-repaint-to", Minimum = 1, Maximum = (decimal)Math.Max(1, s.Seconds), Value = (decimal)Math.Min(10, s.Seconds), Increment = 1, FormatString = "0", Width = 100 };
        var repaint = Act("ms-repaint", "Abschnitt neu erzeugen", () =>
        {
            try { _ = MusicStudioCreate(new AceStepClient.Job(s.Style, s.Lyrics, (int)s.Seconds, s.Language, 1, Task: "repaint", RepaintStart: (double)(from.Value ?? 0), RepaintEnd: (double)(to.Value ?? 0)), s.Title, s.Id); }
            catch (ArgumentException ex) { _msMsg = ex.Message; Show(MS); }
        });
        sp.Children.Add(new StackPanel { Spacing = 4, Children = { H("Abschnitt neu", 13), P("Nur diese Sekunden werden neu gemacht – der Rest bleibt gleich.", Muted, 11.5),
            new WrapPanel { Children = { new TextBlock { Text = "von", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) }, from,
                new TextBlock { Text = "bis", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 6, 0) }, to, new TextBlock { Text = "s", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0) } } },
            repaint } });

        // Cover in anderem Stil
        var coverStyle = new TextBox { Name = "ms-cover-style", Watermark = "Neuer Stil, z. B. ruhige Jazz-Ballade mit Klavier" };
        var strength = new Slider { Name = "ms-cover-strength", Minimum = 0.2, Maximum = 1.0, Value = 0.6, Width = 200 };
        ToolTip.SetTip(strength, "Links: freier, weiter weg vom Original · Rechts: näher am Original");
        var cover = Act("ms-cover", "Cover erstellen", () =>
        {
            if (string.IsNullOrWhiteSpace(coverStyle.Text)) { _msMsg = "Beschreibe zuerst den neuen Stil für das Cover."; Show(MS); return; }
            _ = MusicStudioCreate(new AceStepClient.Job(coverStyle.Text.Trim(), s.Lyrics, (int)s.Seconds, s.Language, 1, Task: "cover", CoverStrength: strength.Value), s.Title, s.Id);
        });
        sp.Children.Add(new StackPanel { Spacing = 4, Children = { H("In anderem Stil (Cover)", 13), coverStyle, new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { new TextBlock { Text = "frei", Foreground = Muted, FontSize = 11, VerticalAlignment = VerticalAlignment.Center }, strength, new TextBlock { Text = "nah", Foreground = Muted, FontSize = 11, VerticalAlignment = VerticalAlignment.Center } } }, cover } });

        // Weitergeben
        var name = new TextBox { Name = "ms-rename", Text = s.Title, HorizontalAlignment = HorizontalAlignment.Stretch };
        var actions = new WrapPanel { Children = {
            Act("ms-rename-ok", "Umbenennen", () => { Songs.Rename(s.Id, name.Text ?? ""); Show(MS); }),
            Act("ms-to-video", "In den Videoschnitt", () => MsToVideo(s)),
            Act("ms-export-mp3c", "MP3 mit Cover", () => MsExportMp3WithCover(s)),
            Act("ms-export-wav", "Als WAV", () => MsExport(s, "wav")), Act("ms-export-flac", "Als FLAC", () => MsExport(s, "flac")),
            Act("ms-folder", "Ordner öffnen", () => Launch(Path.GetDirectoryName(s.File)!)),
            Act("ms-delete", "🗑 Löschen", () => { Songs.Remove(s.Id, p => { if (SongRecycleOverride is { } o) o(p); else RecycleBin.Move(p); }); _msSel = null; _msMsg = $"„{s.Title}“ liegt im Papierkorb."; _core.Audit.Append(Principal.LocalOwner.Name, "musik.studio.geloescht", s.Id); Show(MS); }) } };
        foreach (var c in actions.Children) c.Margin = new Thickness(0, 0, 6, 6);
        sp.Children.Add(new StackPanel { Spacing = 4, Children = { H("Weitergeben", 13), name, actions } });
        return MsCard("ms-detail", new ScrollViewer { Content = sp });
    }

    void MsExport(MusicSong s, string fmt)
    {
        if (FfmpegPath is not { } ff) { _msMsg = "Zum Umwandeln braucht Trendo FFmpeg (unter „Programme“ einrichten)."; Show(MS); return; }
        var target = Path.ChangeExtension(s.File, fmt);
        if (File.Exists(target)) { _msMsg = "Gibt es schon: " + target; Show(MS); return; }
        var r = _opt.Isolated && SongExportOverride is null ? new Cli.Result(-1, "", "Testmodus") : SongExportOverride?.Invoke(s.File, target) ?? Cli.Run(ff, ["-y", "-i", s.File, target], timeoutMs: 600_000);
        _msMsg = r.Ok && File.Exists(target) ? "✓ Gespeichert: " + target : "Umwandeln ging nicht: " + Short(r.Err);
        if (r.Ok) _core.Audit.Append(Principal.LocalOwner.Name, "musik.studio.export", fmt);
        Show(MS);
    }
    public static Func<string, string, Cli.Result>? SongExportOverride { get; set; }

    void MsToVideo(MusicSong s)
    {
        Show("schnitt");
        if (ProgramActive("schnitt") is null) ProgramNew("schnitt");
        EditAdd(s.File, music: true);
        _progMsg["schnitt"] = $"„{s.Title}“ liegt als Musik auf der Zeitleiste.";
        Show("schnitt");
    }

    Control MsPlayerBar()
    {
        var cur = _queue.Current;
        var song = cur is null ? null : Songs.All().FirstOrDefault(x => string.Equals(x.File, cur, StringComparison.OrdinalIgnoreCase));
        var pp = new Button { Name = "ms-player-play", Content = _mPaused || cur is null ? "▶" : "⏸", Width = 40, Height = 40, CornerRadius = new CornerRadius(20), Padding = new Thickness(0), FontSize = 15, IsEnabled = cur is not null,
            HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center, Background = Accent, Foreground = Brushes.White, BorderThickness = new Thickness(0) };
        pp.Click += (_, _) => { _mPage = MS; MusicTogglePause(); };
        _mTimeL = new TextBlock { Name = "ms-time", Text = Trendo.Core.Media.MusicLibrary.Time(_mPos), Foreground = Muted, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Width = 44, TextAlignment = TextAlignment.Right };
        _mTimeR = new TextBlock { Name = "ms-duration", Text = Trendo.Core.Media.MusicLibrary.Time(_mDur), Foreground = Muted, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Width = 44 };
        var seek = _mSeek = new Slider { Name = "ms-seek", Minimum = 0, Maximum = double.IsNaN(_mDur) || _mDur <= 0 ? 1 : _mDur, Value = Math.Max(0, _mPos), Margin = new Thickness(8, 0), IsEnabled = cur is not null };
        seek.AddHandler(PointerPressedEvent, (_, _) => _mSeeking = true, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        seek.AddHandler(PointerReleasedEvent, (_, _) => { _mSeeking = false; _mPos = seek.Value; _audio?.Seek(seek.Value); }, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        var title = new TextBlock { Name = "ms-player-title", Text = song?.Title ?? (cur is null ? "Nichts ausgewählt" : Path.GetFileNameWithoutExtension(cur)), Foreground = Text, FontSize = 13, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center, Width = 220, TextTrimming = TextTrimming.CharacterEllipsis };
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto,*,Auto"), ColumnSpacing = 8, Children = {
            song is null ? new Border { Width = 40, Height = 40 } : MsCover(song, 40), Pos(pp, 1), Pos(title, 2), Pos(new DockPanel { VerticalAlignment = VerticalAlignment.Center, Children = { Dk(_mTimeL, Avalonia.Controls.Dock.Left), Dk(_mTimeR, Avalonia.Controls.Dock.Right), seek } }, 3) } };
        return new Border { Name = "ms-player", Margin = new Thickness(0, 12, 0, 0), CornerRadius = new CornerRadius(16), Background = TileBg, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), BoxShadow = CardShadow, Padding = new Thickness(12, 8), Child = grid };
    }
}
