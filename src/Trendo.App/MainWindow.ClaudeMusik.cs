using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Trendo Musik Claude 26 (05.11.2026): Claude arbeitet als Produzent – Liedstruktur, Text-Rhythmus, Stilwahl, Mastering-Ziel.
/// Trendo gibt Claude das gewählte Lied samt Prüfung (Silben, Reime, Aufbau), Claude schlägt Text, Stil oder Mastering-Ziel vor, die Plan-Karte zeigt
/// die Wirkung als Vorschau (Rhythmus vorher/nachher), erst „Ausführen“ übernimmt: Text und Stil werden der Entwurf einer neuen Fassung (Profi-Formular,
/// „Erstellen“ erzeugt sie, dann A/B neben dem Original), das Mastering-Ziel mastert das Lied.</summary>
public partial class MainWindow
{
    const string ProdLyrics = "ms-prod-lyrics", ProdStyle = "ms-prod-style", ProdMaster = "ms-prod-master";
    string? _msProdParent;
    /// <summary>Lied, an das die nächste erzeugte Fassung gehängt wird (Produzenten-Entwurf von Claude).</summary>
    public string? MsProducerParent => _msProdParent;

    MusicSong? MsSelected => _msSel is null ? null : Songs.Get(_msSel);

    /// <summary>Felder für Claude: Liedtext und Stil der nächsten Fassung, Mastering-Ziel des gewählten Lieds.</summary>
    IEnumerable<ClaudeActions.Field> ClaudeMusicFields(string page)
    {
        if (page == "sendung") { foreach (var f in ClaudeLiveFields()) yield return f; yield break; } // Live streamen: Claude als Regie
        if (page == MB) { foreach (var f in ClaudeMediaFields()) yield return f; yield break; } // Freie Bilder & Klänge: Claude als Rechercheur
        if (page != MS || MsSelected is not { } s) yield break;
        yield return new(ProdLyrics, $"Liedtext für eine neue Fassung von „{s.Title}“", s.Lyrics.Replace("\r", "").Replace("\n", " / "));
        yield return new(ProdStyle, "Stil für eine neue Fassung", s.Style);
        yield return new(ProdMaster, "Mastering-Ziel (" + string.Join(", ", SongProducer.Targets.Select(t => t.Id)) + ")", s.MasterTarget ?? "");
    }

    string? ClaudeMusicFocus(string page) => page == "sendung" ? ClaudeLiveFocus() : page == MB ? ClaudeMediaFocus() : page == MS && MsSelected is { } s ? SongProducer.Briefing(s, SongFollow.Versions(Songs.All(), s)) : null;

    /// <summary>Vorschau in der Plan-Karte: Rhythmus-Prüfung vorher/nachher, Stil-Bausteine, Mastering-Ziel.</summary>
    Control? ClaudeMusicPreview(string page, ClaudeActions.Step s)
    {
        if (page == "sendung" && s.Kind == "feld") return ClaudeLivePreview(s);
        if (page == MB) return ClaudeMediaPreview(s);
        if (page != MS || s.Kind != "feld" || s.Value is not { Length: > 0 } v || MsSelected is not { } song) return null;
        TextBlock Warn(string t) => new() { Name = "claude-plan-warn", Text = "⚠ So nicht möglich: " + t, Foreground = Brush.Parse("#FFF8B26A"), FontSize = 12, TextWrapping = TextWrapping.Wrap };
        var bpm = song.Analysis?.Bpm ?? song.Bpm ?? 0; var meter = song.Analysis?.Meter ?? song.TimeSignature;
        switch (s.Target)
        {
            case ProdLyrics:
            {
                var (text, err) = SongProducer.ParseLyrics(v); if (text is null) return Warn(err!);
                var before = SongProducer.Check(song.Lyrics, bpm, meter); var after = SongProducer.Check(text, bpm, meter);
                var sp = new StackPanel { Name = "claude-plan-music", Spacing = 2 };
                sp.Children.Add(new TextBlock { Text = $"Vorher: {SongProducer.Summary(before)}", Foreground = Muted, FontSize = 11.5, TextWrapping = TextWrapping.Wrap });
                sp.Children.Add(new TextBlock { Text = $"Nachher: {SongProducer.Summary(after)}", Foreground = after.Issues <= before.Issues ? Brush.Parse("#4ADE80") : Brush.Parse("#FFF8B26A"), FontSize = 11.5, TextWrapping = TextWrapping.Wrap });
                foreach (var sec in after.Sections.Take(6))
                {
                    sp.Children.Add(new TextBlock { Text = $"[{sec.Name}]", Foreground = Brush.Parse("#D4A84B"), FontSize = 11, Margin = new Thickness(0, 3, 0, 0) });
                    foreach (var l in sec.Lines.Take(8))
                        sp.Children.Add(new TextBlock { Text = $"{l.Syllables,2} {l.Rhyme}  {l.Text}" + (l.Hint is { } h ? $"  ← {h}" : ""), Foreground = l.Hint is null ? ClaudeFg : Brush.Parse("#FFF8B26A"), FontSize = 11.5, FontFamily = new FontFamily("Consolas, monospace"), TextWrapping = TextWrapping.Wrap });
                }
                return new Border { CornerRadius = new CornerRadius(8), Background = Brush.Parse("#14FFFFFF"), Padding = new Thickness(8, 6), Child = sp };
            }
            case ProdStyle:
            {
                var (text, err) = SongProducer.ParseStyle(v); if (text is null) return Warn(err!);
                var wp = new WrapPanel { Name = "claude-plan-music" };
                foreach (var p in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    wp.Children.Add(new Border { Background = Brush.Parse("#22D4A84B"), CornerRadius = new CornerRadius(8), Padding = new Thickness(7, 1), Margin = new Thickness(0, 0, 4, 4), Child = new TextBlock { Text = p, Foreground = ClaudeFg, FontSize = 11.5 } });
                return wp;
            }
            case ProdMaster:
            {
                if (!SongProducer.Targets.Any(t => t.Id == v.Trim().ToLowerInvariant())) return Warn($"unbekanntes Ziel „{v}“ – {string.Join(", ", SongProducer.Targets.Select(t => t.Id))}");
                var t = SongProducer.TargetOf(v.Trim().ToLowerInvariant());
                return new TextBlock { Name = "claude-plan-music", Text = $"{t.Label}: {t.Lufs:0} LUFS, Spitzen {t.TruePeak:0.0} dBTP – {t.Note}".Replace("-", "−"), Foreground = ClaudeFg, FontSize = 12, TextWrapping = TextWrapping.Wrap };
            }
        }
        return null;
    }

    /// <summary>Claudes Vorschlag übernehmen: Text/Stil → Entwurf der neuen Fassung im Profi-Formular; Mastering-Ziel → Lied mastern.</summary>
    bool ClaudeMusicApply(string page, ClaudeActions.Step s)
    {
        if (page == "sendung" && s.Kind == "feld") return ClaudeLiveApply(s);
        if (page == MB) return ClaudeMediaApply(s);
        if (page != MS || s.Kind != "feld" || s.Value is not { Length: > 0 } v || MsSelected is not { } song) return false;
        void Draft()
        {
            if (_msProdParent != song.Id)
            {
                _msTitle = song.Title + " – Produzenten-Fassung"; _msLyrics = song.Lyrics; _msStyle = song.Style; _msLang = Math.Max(0, Array.FindIndex(AceStepClient.Languages, l => l.Code == song.Language));
                var bpm = song.Analysis?.Bpm ?? song.Bpm; _msBpm = bpm is > 0 ? Math.Round(bpm.Value).ToString(System.Globalization.CultureInfo.InvariantCulture) : "";
                var key = song.Analysis?.Key ?? song.Key; _msKey = key is not null && AceStepClient.Keys.Contains(key) ? key : "";
                _msTime = (song.Analysis?.Meter ?? song.TimeSignature ?? "").Split('/')[0];
                _msLen = Enumerable.Range(0, MusicLengths.Length).MinBy(i => Math.Abs(MusicLengths[i].Seconds - song.Seconds));
            }
            _msPro = true; _msProdParent = song.Id;
        }
        switch (s.Target)
        {
            case ProdLyrics:
            {
                var (text, err) = SongProducer.ParseLyrics(v); if (text is null) { _msMsg = "Claudes Liedtext nicht übernommen: " + err + "."; return false; }
                Draft(); _msLyrics = text; _msInstr = false;
                _msMsg = "Claudes Fassung steht links im Profi-Formular – „Erstellen“ erzeugt sie, danach unter „Fassungen vergleichen“ A/B neben dem Original.";
                return true;
            }
            case ProdStyle:
            {
                var (text, err) = SongProducer.ParseStyle(v); if (text is null) { _msMsg = "Claudes Stil nicht übernommen: " + err + "."; return false; }
                Draft(); _msStyle = text; _msGenres.Clear();
                _msMsg = "Claudes Stil steht links im Profi-Formular – „Erstellen“ erzeugt die neue Fassung.";
                return true;
            }
            case ProdMaster:
            {
                var id = v.Trim().ToLowerInvariant(); if (!SongProducer.Targets.Any(t => t.Id == id)) return false;
                Songs.Update(song.Id, x => x.MasterTarget = id);
                _ = MsMaster(song.Id, target: id);
                return true;
            }
        }
        return false;
    }

    /// <summary>Nach „Erstellen“: die neuen Lieder als Produzenten-Fassung an das Ausgangslied hängen (für den A/B-Vergleich).</summary>
    List<MusicSong> MsLinkProducerDraft(List<MusicSong> made)
    {
        if (_msProdParent is not { } parent || made.Count == 0) return made;
        foreach (var m in made) Songs.Update(m.Id, x => { x.ParentId = parent; x.Kind = "produzent"; });
        _msProdParent = null;
        return [.. made.Select(m => Songs.Get(m.Id) ?? m)];
    }

    /// <summary>Hinweis über dem Profi-Formular, solange ein Produzenten-Entwurf von Claude offen ist.</summary>
    Control? MsProducerDraftBanner()
    {
        if (_msProdParent is not { } pid || Songs.Get(pid) is not { } parent) return null;
        var drop = new Button { Name = "ms-prod-drop", Content = "Entwurf verwerfen", FontSize = 11.5, Padding = new Thickness(8, 2) };
        drop.Click += (_, _) => { _msProdParent = null; _msTitle = ""; _msMsg = "Entwurf verworfen."; Show(MS); };
        return new Border { Name = "ms-prod-banner", CornerRadius = new CornerRadius(10), Background = Brush.Parse("#22D4A84B"), BorderBrush = Brush.Parse("#66D4A84B"), BorderThickness = new Thickness(1), Padding = new Thickness(10, 6), Margin = new Thickness(0, 0, 0, 6),
            Child = new DockPanel { Children = { Dk(drop, Avalonia.Controls.Dock.Right),
                new TextBlock { Text = $"Produzenten-Fassung von „{parent.Title}“ – „Erstellen“ erzeugt sie als neue Fassung zum Vergleichen.", Foreground = Text, FontSize = 12, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center } } } };
    }
}
