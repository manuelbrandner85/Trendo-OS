using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Trendo Musik – Ausbau M1/M2/M6/M8 (freigegeben 03.11.2026): über 180 Genres in Gruppen mit Suche und Tempo-Vorschlag, Gesangsarten,
/// Tonlage und Instrumente, „Im Stil von“ (Stilmerkmale – nie Stimme oder Name echter Musiker). Gilt im Einfach- und im Profi-Modus.
/// Tonart, Takt und Tempo hält ACE-Step ein (am 03.11.2026 gemessen: A-Moll/3-4 und E-Dur/4-4 genau, 140 BPM → 143,6).</summary>
public partial class MainWindow
{
    readonly List<string> _msGenres = [];
    string _msVoice = "", _msRange = "", _msLike = "", _msGenreQuery = "", _msGenreGroup = "";
    readonly HashSet<string> _msInstruments = [];
    bool _msGenreOpen;

    static readonly (string Name, string Tag)[] MsRanges = [("hoch", "high-pitched vocals"), ("mittel", "mid-range vocals"), ("tief", "deep low vocals")];
    static readonly (string Name, string Tag)[] MsInstrumentChoices =
    [
        ("Klavier", "piano"), ("Akustikgitarre", "acoustic guitar"), ("E-Gitarre", "electric guitar"), ("Bass", "bass guitar"), ("Schlagzeug", "drums"),
        ("Streicher", "strings"), ("Bläser", "brass section"), ("Saxophon", "saxophone"), ("Synthesizer", "synthesizer"), ("808-Bass", "808 bass"),
        ("Orgel", "hammond organ"), ("Akkordeon", "accordion"), ("Violine", "violin"), ("Cello", "cello"), ("Flöte", "flute"), ("Ukulele", "ukulele"),
    ];

    /// <summary>Gewählte Genres, Stimme, Tonlage und Instrumente als Begriffe für ACE-Step (kommen zur Beschreibung bzw. zum Stil dazu).</summary>
    string MsStyleExtras()
    {
        var parts = new List<string>();
        parts.AddRange(_msGenres.Select(n => MusicGenres.Find(n)?.Tag).OfType<string>());
        if (!_msInstr)
        {
            if (MusicGenres.Voices.FirstOrDefault(v => v.Name == _msVoice).Tag is { } vt) parts.Add(vt);
            if (MsRanges.FirstOrDefault(r => r.Name == _msRange).Tag is { } rt) parts.Add(rt);
        }
        parts.AddRange(MsInstrumentChoices.Where(i => _msInstruments.Contains(i.Name)).Select(i => i.Tag));
        return string.Join(", ", parts.Distinct());
    }

    /// <summary>Beschreibung bzw. Stil mit den Zusätzen.</summary>
    string MsWithExtras(string text) => MsStyleExtras() is { Length: > 0 } x ? (text.Trim().TrimEnd(',') + ", " + x).Trim(' ', ',') : text.Trim();

    /// <summary>Bereich „Genre, Stimme, Klang“ im Erstellen-Feld.</summary>
    Control MsStylePanel(Action sync)
    {
        var sp = new StackPanel { Name = "ms-style-panel", Spacing = 6 };
        // M1 Genres: gewählte als Chips, dazu „Genre wählen …“
        var chosen = new WrapPanel { Name = "ms-genres-chosen" };
        foreach (var n in _msGenres.ToList())
        {
            var g = MusicGenres.Find(n);
            var b = new Button { Name = "ms-genre-x-" + Slug(n), Content = "✓ " + n + "  ×", FontSize = 11.5, Padding = new Thickness(8, 2), Margin = new Thickness(0, 0, 4, 4), CornerRadius = new CornerRadius(10), Background = Accent, Foreground = Brushes.White, BorderThickness = new Thickness(0) };
            if (g is not null) ToolTip.SetTip(b, $"{g.Group} · üblich {g.BpmLow}–{g.BpmHigh} BPM · {g.Instruments}");
            var nn = n; b.Click += (_, _) => { sync(); _msGenres.Remove(nn); Show(MS); };
            chosen.Children.Add(b);
        }
        var open = new Button { Name = "ms-genre-open", Content = _msGenreOpen ? "Genres schließen" : $"🎼 Genre wählen ({MusicGenres.All.Length})", FontSize = 12, Padding = new Thickness(10, 3), Margin = new Thickness(0, 0, 4, 4) };
        open.Click += (_, _) => { sync(); _msGenreOpen = !_msGenreOpen; Show(MS); };
        chosen.Children.Add(open);
        sp.Children.Add(chosen);
        if (_msGenres.Count > 0 && MusicGenres.Find(_msGenres[0]) is { } first)
        {
            var hint = new TextBlock { Name = "ms-genre-hint", Text = $"Passt meist: {first.BpmLow}–{first.BpmHigh} BPM, {(first.Minor ? "Moll" : "Dur")}, {first.Instruments}", Foreground = Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap };
            sp.Children.Add(hint);
            if (_msPro && _msBpm.Length == 0)
            {
                var take = new Button { Name = "ms-genre-bpm", Content = $"Tempo {first.Bpm} übernehmen", FontSize = 11.5, Padding = new Thickness(8, 2) };
                take.Click += (_, _) => { sync(); _msBpm = first.Bpm.ToString(System.Globalization.CultureInfo.InvariantCulture); Show(MS); };
                sp.Children.Add(take);
            }
        }
        if (_msGenreOpen)
        {
            var box = new StackPanel { Name = "ms-genres", Spacing = 4 };
            var list = new WrapPanel { Name = "ms-genre-list" };
            var q = new TextBox { Name = "ms-genre-search", Watermark = "Genre suchen, z. B. Jazz, Techno, Schlager", Text = _msGenreQuery };
            q.KeyUp += (_, _) => { if ((q.Text ?? "") != _msGenreQuery) { _msGenreQuery = q.Text ?? ""; FillList(); } };
            var groups = new WrapPanel();
            foreach (var gname in MusicGenres.Groups.Prepend(""))
            {
                var on = _msGenreGroup == gname; var gn = gname;
                var gb = new Button { Name = "ms-genre-group-" + (gname.Length == 0 ? "alle" : Slug(gname)), Content = gname.Length == 0 ? "Alle" : gname, FontSize = 11, Padding = new Thickness(7, 1), Margin = new Thickness(0, 0, 3, 3), CornerRadius = new CornerRadius(9),
                    Background = on ? Accent : Brush.Parse("#18FFFFFF"), Foreground = on ? Brushes.White : Text, BorderThickness = new Thickness(0) };
                gb.Click += (_, _) => { sync(); _msGenreGroup = gn; Show(MS); };
                groups.Children.Add(gb);
            }
            void FillList()
            {
                list.Children.Clear();
                foreach (var g in MusicGenres.Search(_msGenreQuery).Where(g => _msGenreGroup.Length == 0 || g.Group == _msGenreGroup).Take(80))
                {
                    var on = _msGenres.Contains(g.Name); var gg = g;
                    var b = new Button { Name = "ms-genre-" + Slug(g.Name), Content = g.Name, FontSize = 11.5, Padding = new Thickness(8, 2), Margin = new Thickness(0, 0, 4, 4), CornerRadius = new CornerRadius(10),
                        Background = on ? Accent : Brush.Parse("#14FFFFFF"), Foreground = on ? Brushes.White : Text, BorderThickness = new Thickness(0) };
                    ToolTip.SetTip(b, $"{g.Group} · {g.BpmLow}–{g.BpmHigh} BPM · {g.Instruments}");
                    b.Click += (_, _) =>
                    {
                        sync();
                        if (_msGenres.Contains(gg.Name)) _msGenres.Remove(gg.Name);
                        else if (_msGenres.Count < 3) _msGenres.Add(gg.Name);
                        else { _msMsg = "Höchstens drei Genres zusammen – sonst wird es beliebig."; }
                        Show(MS);
                    };
                    list.Children.Add(b);
                }
                if (list.Children.Count == 0) list.Children.Add(new TextBlock { Text = "Kein Genre gefunden.", Foreground = Muted, FontSize = 11.5 });
            }
            FillList();
            box.Children.Add(q); box.Children.Add(groups); box.Children.Add(new ScrollViewer { MaxHeight = 190, Content = list });
            sp.Children.Add(new Border { Background = Brush.Parse("#10FFFFFF"), CornerRadius = new CornerRadius(10), Padding = new Thickness(8), Child = box });
        }

        // M8 Stimme, M6 Tonlage
        if (!_msInstr)
        {
            var voice = new ComboBox { Name = "ms-voice", Width = 160, ItemsSource = MusicGenres.Voices.Select(v => v.Name).Prepend("Stimme: KI wählt").ToArray(), SelectedIndex = 1 + Array.FindIndex(MusicGenres.Voices, v => v.Name == _msVoice) };
            voice.SelectionChanged += (_, _) => _msVoice = voice.SelectedIndex <= 0 ? "" : MusicGenres.Voices[voice.SelectedIndex - 1].Name;
            var range = new ComboBox { Name = "ms-range", Width = 150, ItemsSource = MsRanges.Select(r => "Tonlage " + r.Name).Prepend("Tonlage: KI wählt").ToArray(), SelectedIndex = 1 + Array.FindIndex(MsRanges, r => r.Name == _msRange) };
            range.SelectionChanged += (_, _) => _msRange = range.SelectedIndex <= 0 ? "" : MsRanges[range.SelectedIndex - 1].Name;
            sp.Children.Add(new WrapPanel { Children = { voice, new Border { Width = 6 }, range } });
        }
        // M6 Instrumente
        var ins = new WrapPanel { Name = "ms-instruments" };
        foreach (var (name, _) in MsInstrumentChoices)
        {
            var on = _msInstruments.Contains(name); var nm = name;
            var b = new Button { Name = "ms-instr-" + Slug(name), Content = name, FontSize = 11, Padding = new Thickness(7, 1), Margin = new Thickness(0, 0, 3, 3), CornerRadius = new CornerRadius(9),
                Background = on ? Accent : Brush.Parse("#12FFFFFF"), Foreground = on ? Brushes.White : Text, BorderThickness = new Thickness(0) };
            b.Click += (_, _) => { sync(); if (!_msInstruments.Remove(nm)) _msInstruments.Add(nm); Show(MS); };
            ins.Children.Add(b);
        }
        sp.Children.Add(new Expander { Name = "ms-instruments-box", Header = _msInstruments.Count == 0 ? "Instrumente (optional)" : $"Instrumente: {string.Join(", ", _msInstruments)}", IsExpanded = _msInstruments.Count > 0, Content = ins, HorizontalAlignment = HorizontalAlignment.Stretch });

        // M2 Im Stil von
        var like = new TextBox { Name = "ms-like", Watermark = "Im Stil von … (Musiker oder Band)", Text = _msLike, MinWidth = 200 };
        like.TextChanged += (_, _) => _msLike = like.Text ?? "";
        var likeGo = Act("ms-like-go", "Stil übernehmen", () => { sync(); _msLike = like.Text ?? ""; _ = MsStyleLike(_msLike); });
        ToolTip.SetTip(likeGo, "Die KI beschreibt den Stil in Merkmalen (Jahrzehnt, Instrumente, Tempo, Gesang). Kein Name und keine nachgemachte Stimme einer echten Person.");
        sp.Children.Add(new WrapPanel { Children = { like, new Border { Width = 6 }, likeGo } });
        return sp;
    }

    /// <summary>Genres wählen und die Auswahl öffnen (Tests, Bildschirmfotos).</summary>
    public void MusicStudioPickGenres(bool open, params string[] names) { _msGenres.Clear(); _msGenres.AddRange(names.Where(n => MusicGenres.Find(n) is not null).Take(3)); _msGenreOpen = open; }

    static string Slug(string s) => System.Text.RegularExpressions.Regex.Replace(new string(s.ToLowerInvariant().Replace('ä', 'a').Replace('ö', 'o').Replace('ü', 'u').Replace("ß", "ss").Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()), "-+", "-").Trim('-');

    /// <summary>M2: Stil eines Musikers als Merkmale – ohne Namen von Personen, Bands oder Liedern (Persönlichkeitsrecht; ACE-Step soll niemanden nachahmen).</summary>
    public async Task<string?> MsStyleLike(string artist)
    {
        var a = artist.Trim();
        if (a.Length < 2) { _msMsg = "Gib zuerst einen Musiker oder eine Band ein."; Show(MS); return null; }
        var prompt = $"Beschreibe den typischen musikalischen Stil von „{a}“ als englische Stichworte für eine Musik-KI, durch Kommas getrennt: Genre, Jahrzehnt, Instrumente, Tempo, Gesangsart, Produktion. " +
                     "Nenne KEINE Namen von Personen, Bands, Alben oder Liedern. Höchstens 25 Wörter, nur die Stichworte.";
        _msMsg = "Die KI beschreibt den Stil …"; Show(MS);
        string? text;
        try { text = MusicLyricsOverride is { } o ? await o(prompt) : await Intel.AskAsync(new KiRequest(prompt, PreferredModel: KiModelName, UseKnowledge: false, AllowRemote: true)) is { Ok: true } r ? r.Text : null; }
        catch (Exception e) when (e is InvalidOperationException or HttpRequestException or TaskCanceledException) { text = null; }
        if (string.IsNullOrWhiteSpace(text)) { _msMsg = "Dafür braucht es eine Text-KI (unter „KI einsetzen“ einrichten) – oder wähle oben Genres und Stimme selbst."; Show(MS); return null; }
        // Sicherheit: Namen entfernen, falls die KI sie doch nennt
        var traits = System.Text.RegularExpressions.Regex.Replace(text.Replace("\n", " "), System.Text.RegularExpressions.Regex.Escape(a), "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        traits = string.Join(", ", traits.Split(',', ';').Select(t => t.Trim(' ', '.', '"', '„', '“')).Where(t => t.Length > 1).Take(12));
        if (_msPro) _msStyle = (_msStyle.Trim().Length > 0 ? _msStyle.TrimEnd(' ', ',') + ", " : "") + traits; else _msDesc = (_msDesc.Trim().Length > 0 ? _msDesc.TrimEnd(' ', ',') + ", " : "") + traits;
        _msMsg = "✓ Stilmerkmale eingesetzt – ohne Namen, die Musik-KI ahmt niemanden nach.";
        _core.Audit.Append(Principal.LocalOwner.Name, "musik.studio.stil", "merkmale");
        Show(MS);
        return traits;
    }
}
