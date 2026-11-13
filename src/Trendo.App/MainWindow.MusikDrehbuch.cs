using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>KI-Musikvideo, Baustein B (freigegeben 04.11.2026): Drehbuch im Reiter „Musikvideo“. Konzept wählen, Drehbuch erstellen,
/// jede Einstellung anklicken und ändern (Einstellungsgröße, Kamera, wer im Bild, Gesang ja/nein, Ort, Beschreibung), löschen oder neu würfeln.
/// Figuren (erfunden) lassen sich beschreiben – die Beschreibung gilt für jede Einstellung, damit die Person überall gleich aussieht.</summary>
public partial class MainWindow
{
    string _msConcept = "gemischt"; int _msShotSel = -1;

    // ───────────── E2: fertige Musikvideos am Lied ─────────────
    static string SongVideosFile(MusicSong s) => s.File + ".videos.json";
    public List<string> SongVideos(MusicSong s)
    {
        try { return File.Exists(SongVideosFile(s)) ? (System.Text.Json.JsonSerializer.Deserialize<List<string>>(File.ReadAllText(SongVideosFile(s))) ?? []).Where(File.Exists).ToList() : []; }
        catch (System.Text.Json.JsonException) { return []; }
    }
    void SongVideoAdd(MusicSong s, string video)
    {
        var l = SongVideos(s); if (!l.Contains(video, StringComparer.OrdinalIgnoreCase)) l.Insert(0, video);
        File.WriteAllText(SongVideosFile(s), System.Text.Json.JsonSerializer.Serialize(l));
    }

    /// <summary>Tests: Ton ersetzen ohne FFmpeg (Video, Lied, Ziel) → ok.</summary>
    public Func<string, string, string, bool>? ReplaceAudioOverride { get; set; }

    /// <summary>E2: Lied geändert (z. B. neu gemastert)? Nur den Ton im Video ersetzen – das Bild bleibt, nichts wird neu gerechnet.</summary>
    public string? MsVideoNewAudio(string songId, string video)
    {
        if (Songs.Get(songId) is not { } s || !File.Exists(video)) return null;
        var o = Path.Combine(Path.GetDirectoryName(video)!, Path.GetFileNameWithoutExtension(video) + " – neuer Ton.mp4");
        for (var n = 2; File.Exists(o); n++) o = Path.Combine(Path.GetDirectoryName(video)!, Path.GetFileNameWithoutExtension(video) + $" – neuer Ton {n}.mp4");
        bool ok;
        if (ReplaceAudioOverride is { } f) ok = f(video, s.File, o);
        else if (FfmpegPath is { } ff && !_opt.Isolated) ok = Cli.Run(ff, ["-y", "-i", video, "-i", s.File, "-map", "0:v:0", "-map", "1:a:0", "-c:v", "copy", "-c:a", "aac", "-b:a", "320k", "-shortest", o], timeoutMs: 600_000).Ok && File.Exists(o);
        else { _msMsg = "Zum Ersetzen des Tons braucht Trendo FFmpeg (unter „Programme“ einrichten)."; Show(MS); return null; }
        if (!ok) { _msMsg = "Ton ersetzen ging nicht."; Show(MS); return null; }
        SongVideoAdd(s, o);
        _msMsg = $"Neuer Ton im Video: {Path.GetFileName(o)}"; _core.Audit.Append(Principal.LocalOwner.Name, "musik.video.ton", Path.GetFileName(o));
        if (CurrentView == MS) Show(MS);
        return o;
    }

    Control MsSongVideosPanel(MusicSong s)
    {
        var sp = new StackPanel { Name = "ms-song-videos", Spacing = 4 };
        var vids = SongVideos(s);
        sp.Children.Add(H("Videos zu diesem Lied", 13));
        sp.Children.Add(Act("ms-open-schnitt", "Im Videoschnitt öffnen (mit Abschnitten, Zeilen und Drehbuch)", () => MsToVideo(s)));
        if (vids.Count == 0) { sp.Children.Add(P("Noch keins. Fertige Videos aus dem Videoschnitt erscheinen hier und unter „Meine Exporte“.", Muted, 11)); return sp; }
        var i = 0;
        foreach (var v in vids.Take(6))
        {
            var f = v; var n = i++;
            var newer = File.GetLastWriteTimeUtc(s.File) > File.GetLastWriteTimeUtc(f);
            sp.Children.Add(new StackPanel { Spacing = 2, Children = {
                new TextBlock { Text = "" + Path.GetFileName(f) + (newer ? "  · Lied wurde seitdem geändert" : ""), Foreground = newer ? Brush.Parse("#FACC15") : Text, FontSize = 11.5, TextTrimming = TextTrimming.CharacterEllipsis },
                new WrapPanel { Children = { Act($"ms-vid-play-{n}", "Abspielen", () => Launch(f)), Act($"ms-vid-folder-{n}", "Ordner öffnen", () => RevealInFolder(f)),
                    Act($"ms-vid-audio-{n}", "Ton erneuern", () => MsVideoNewAudio(s.Id, f), newer) } } } });
        }
        return sp;
    }

    static string ScriptFile(MusicSong s) => s.File + ".drehbuch.json";
    public MusicScript.Script? Script(MusicSong s) => File.Exists(ScriptFile(s)) ? MusicScript.Load(File.ReadAllText(ScriptFile(s))) : null;
    void SaveScript(MusicSong s, MusicScript.Script sc) { File.WriteAllText(ScriptFile(s), MusicScript.Save(sc)); }

    /// <summary>B1: Drehbuch aus dem Liedverständnis erstellen (ohne Verständnis: erst verstehen).</summary>
    public async Task<MusicScript.Script?> MsScriptPlan(string songId, string? concept = null)
    {
        if (Songs.Get(songId) is not { } s) return null;
        var u = Understanding(s) ?? await MsUnderstand(songId);
        if (u is null) return null;
        var old = Script(s);
        var sc = MusicScript.Plan(u, s.Style, concept ?? _msConcept, seed: old is null ? 0 : old.Shots.Count + 1);
        if (old is not null && old.Figures.Count == sc.Figures.Count) sc = sc with { Figures = old.Figures }; // eigene Figurenbeschreibungen behalten
        sc = sc with { Shots = sc.Shots.Select(x => x with { Prompt = MusicScript.Prompt(x, sc.Figures, s.Style, u.Theme) }).ToList() };
        SaveScript(s, sc); _msShotSel = -1;
        _msMsg = "Drehbuch: " + MusicScript.Summary(sc) + ". Einstellung anklicken zum Ändern.";
        _core.Audit.Append(Principal.LocalOwner.Name, "musik.drehbuch", $"{s.Id}: {sc.Shots.Count} Einstellungen");
        if (CurrentView == MS) Show(MS);
        return sc;
    }

    /// <summary>B2: eine Einstellung ändern (Felder → Beschreibung wird neu gebaut, außer sie wurde selbst geschrieben).</summary>
    public void MsShotEdit(string songId, int index, Func<MusicScript.Shot, MusicScript.Shot> change)
    {
        if (Songs.Get(songId) is not { } s || Script(s) is not { } sc || index < 0 || index >= sc.Shots.Count) return;
        SaveScript(s, MusicScript.Edit(sc, index, change, s.Style, Understanding(s)?.Theme)); _msShotSel = index;
        if (CurrentView == MS) Show(MS);
    }

    public void MsShotRemove(string songId, int index)
    {
        if (Songs.Get(songId) is not { } s || Script(s) is not { } sc) return;
        SaveScript(s, MusicScript.Remove(sc, index)); _msShotSel = Math.Min(index, sc.Shots.Count - 2);
        if (CurrentView == MS) Show(MS);
    }

    public void MsShotReroll(string songId, int index)
    {
        if (Songs.Get(songId) is not { } s || Script(s) is not { } sc) return;
        SaveScript(s, MusicScript.Reroll(sc, index, s.Style, Understanding(s)?.Theme)); _msShotSel = index;
        if (CurrentView == MS) Show(MS);
    }

    /// <summary>B3: Figur beschreiben – alle Einstellungen bekommen die neue Beschreibung.</summary>
    public void MsFigureEdit(string songId, string figId, string look)
    {
        if (Songs.Get(songId) is not { } s || Script(s) is not { } sc || string.IsNullOrWhiteSpace(look)) return;
        var figs = sc.Figures.Select(f => f.Id == figId ? f with { Look = look.Trim() } : f).ToList();
        var theme = Understanding(s)?.Theme;
        sc = sc with { Figures = figs, Shots = sc.Shots.Select(x => x with { Prompt = MusicScript.Prompt(x, figs, s.Style, theme), Status = "geplant" }).ToList() };
        SaveScript(s, sc);
        if (CurrentView == MS) Show(MS);
    }

    Control MsScriptPanel(MusicSong s)
    {
        var sp = new StackPanel { Name = "ms-script-panel", Spacing = 6 };
        sp.Children.Add(H("Drehbuch", 13));
        var concepts = new WrapPanel();
        foreach (var (id, name) in MusicScript.Concepts)
        {
            var on = id == _msConcept; var cid = id;
            var b = new Button { Name = "ms-concept-" + id, Content = name, FontSize = 11.5, Padding = new Thickness(9, 3), Margin = new Thickness(0, 0, 4, 4), CornerRadius = new CornerRadius(9),
                Background = on ? Brush.Parse("#33D4A84B") : Brush.Parse("#10FFFFFF"), BorderBrush = Brush.Parse(on ? "#D4A84B" : "#22FFFFFF"), BorderThickness = new Thickness(1) };
            b.Click += (_, _) => { _msConcept = cid; Show(MS); };
            concepts.Children.Add(b);
        }
        sp.Children.Add(concepts);
        var sc = Script(s);
        sp.Children.Add(Act("ms-script-plan", sc is null ? "Drehbuch erstellen" : "Drehbuch neu erstellen", () => _ = MsScriptPlan(s.Id), sc is null));
        if (sc is null) { sp.Children.Add(P("Trendo plant Einstellung für Einstellung wie bei einem echten Dreh: Größe, Kamerafahrt, Licht, Ort – und wer gerade singt, ist im Bild.", Muted, 11.5)); return sp; }
        sp.Children.Add(P(sc.Logline, Text, 11.5));
        sp.Children.Add(P(MusicScript.Summary(sc), Muted, 11));
        // Figuren
        foreach (var f in sc.Figures)
        {
            var fid = f.Id;
            var box = new TextBox { Name = "ms-figure-" + f.Id, Text = f.Look, TextWrapping = TextWrapping.Wrap, FontSize = 11, AcceptsReturn = false };
            box.LostFocus += (_, _) => { if (box.Text != f.Look) MsFigureEdit(s.Id, fid, box.Text ?? ""); };
            sp.Children.Add(new StackPanel { Spacing = 2, Children = { new TextBlock { Text = (f.Gender == "w" ? "• " : "") + f.Role + " (erfunden – so sieht sie/er in jeder Einstellung aus)", Foreground = Text, FontSize = 11.5, FontWeight = FontWeight.SemiBold }, box } });
        }
        // Einstellungen: kompakte Liste, Klick öffnet den Editor
        var list = new StackPanel { Name = "ms-shots", Spacing = 1 };
        foreach (var x in sc.Shots)
        {
            var i = x.Index; var on = i == _msShotSel;
            var row = new Button { Name = $"ms-shot-{i}", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(6, 3),
                Background = on ? Brush.Parse("#26C9A227") : Brushes.Transparent, BorderThickness = new Thickness(0),
                Content = new Grid { ColumnDefinitions = new ColumnDefinitions("40,62,*"), Children = {
                    new TextBlock { Text = SongTime(x.Start), Foreground = Muted, FontSize = 11 },
                    Pos(new TextBlock { Text = x.Size, Foreground = Text, FontSize = 11 }, 1),
                    Pos(new TextBlock { Text = (x.Singing ? "" + x.Who : "" + x.Place) + " · " + x.Move, Foreground = x.Singing ? Text : Muted, FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis }, 2) } } };
            row.Click += (_, _) => { _msShotSel = _msShotSel == i ? -1 : i; Show(MS); };
            list.Children.Add(row);
        }
        sp.Children.Add(new Border { Background = Brush.Parse("#0FFFFFFF"), CornerRadius = new CornerRadius(10), Padding = new Thickness(4), MaxHeight = 220, Child = new ScrollViewer { Content = list } });
        if (_msShotSel >= 0 && _msShotSel < sc.Shots.Count) sp.Children.Add(MsShotEditor(s, sc, sc.Shots[_msShotSel]));
        return sp;
    }

    Control MsShotEditor(MusicSong s, MusicScript.Script sc, MusicScript.Shot x)
    {
        var i = x.Index;
        var sp = new StackPanel { Name = "ms-shot-editor", Spacing = 4 };
        sp.Children.Add(new TextBlock { Text = $"Einstellung {i + 1} · {SongTime(x.Start)}–{SongTime(x.End)} · {x.Part}" + (x.Line.Length > 0 ? $" · „{x.Line}“" : ""), Foreground = Text, FontSize = 12, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
        var size = new ComboBox { Name = "ms-shot-size", ItemsSource = MusicScript.SizeNames, SelectedItem = x.Size, Width = 120 };
        size.SelectionChanged += (_, _) => { if (size.SelectedItem is string v && v != x.Size) MsShotEdit(s.Id, i, y => y with { Size = v }); };
        var move = new ComboBox { Name = "ms-shot-move", ItemsSource = MusicScript.MoveNames.ToList(), SelectedItem = x.Move, Width = 130 };
        move.SelectionChanged += (_, _) => { if (move.SelectedItem is string v && v != x.Move) MsShotEdit(s.Id, i, y => y with { Move = v }); };
        var whoList = sc.Figures.Select(f => f.Role).Append("beide").Distinct().ToList();
        var who = new ComboBox { Name = "ms-shot-who", ItemsSource = whoList, SelectedItem = whoList.Contains(x.Who) ? x.Who : whoList[0], Width = 120 };
        who.SelectionChanged += (_, _) => { if (who.SelectedItem is string v && v != x.Who) MsShotEdit(s.Id, i, y => y with { Who = v }); };
        var sing = new CheckBox { Name = "ms-shot-sing", Content = "singt im Bild", IsChecked = x.Singing, Foreground = Text };
        sing.IsCheckedChanged += (_, _) => { var v = sing.IsChecked == true; if (v != x.Singing) MsShotEdit(s.Id, i, y => y with { Singing = v, Size = v && y.Size is "Totale" or "Halbtotale" ? "Halbnah" : y.Size }); };
        sp.Children.Add(new WrapPanel { Children = { size, Gap(move), Gap(who), Gap(sing) } });
        var place = new TextBox { Name = "ms-shot-place", Text = x.Place, Watermark = "Ort", FontSize = 11.5 };
        place.LostFocus += (_, _) => { if ((place.Text ?? "") != x.Place && (place.Text ?? "").Trim().Length > 0) MsShotEdit(s.Id, i, y => y with { Place = place.Text!.Trim() }); };
        sp.Children.Add(place);
        var prompt = new TextBox { Name = "ms-shot-prompt", Text = x.Prompt, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, FontSize = 11, MinHeight = 60 };
        ToolTip.SetTip(prompt, "Beschreibung für die Video-KI (englisch). Eigene Änderungen bleiben erhalten.");
        prompt.LostFocus += (_, _) => { if ((prompt.Text ?? "") != x.Prompt) MsShotEdit(s.Id, i, y => y with { Prompt = prompt.Text ?? "" }); };
        sp.Children.Add(prompt);
        sp.Children.Add(new WrapPanel { Children = {
            Act("ms-shot-reroll", "Neu würfeln", () => MsShotReroll(s.Id, i)), Gap(Act("ms-shot-delete", "Einstellung löschen", () => MsShotRemove(s.Id, i))) } });
        return new Border { Background = Brush.Parse("#14FFFFFF"), CornerRadius = new CornerRadius(10), Padding = new Thickness(10, 8), Child = sp };

        static Control Gap(Control c) { c.Margin = new Thickness(6, 0, 0, 0); return c; }
    }
}
