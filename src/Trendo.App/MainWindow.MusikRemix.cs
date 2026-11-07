using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Media;

namespace Trendo.App;

/// <summary>Trendo Musik V5 wie Suno (07.11.2026): „Remix“-Menü je Lied – Verlängern, Cover, Abschnitt ersetzen, Zuschneiden, Abschnitt entfernen,
/// Remaster mit Variation, Beschreibung wiederverwenden, Spuren holen, MIDI holen, als Klangvorbild merken, Teil per Sprache ändern.
/// Neu gebaut: Zuschneiden/Entfernen (FFmpeg, mit kurzem Übergang), Remaster (ACE-Step „cover“ nah am Original), Beschreibung wiederverwenden.</summary>
public partial class MainWindow
{

    public IReadOnlyList<(string Id, string Label, Action Run)> MsRemixActions(MusicSong s)
    {
        void Tab(string t) { _msSel = s.Id; _msDetailTab = t; Show(MS); }
        return [
            ("extend", "↦ Verlängern", () => Tab("bearbeiten")),
            ("cover", "♫ Cover in anderem Stil", () => Tab("bearbeiten")),
            ("replace", "✎ Abschnitt ersetzen", () => Tab("bearbeiten")),
            ("crop", "✂ Zuschneiden oder Abschnitt entfernen", () => Tab("bearbeiten")),
            ("remaster", "✨ Remaster (neu, nah am Original)", () => _ = MsRemaster(s.Id)),
            ("reuse", "↺ Beschreibung wiederverwenden", () => MsReusePrompt(s.Id)),
            ("mashup", "⇄ Mashup mit einem anderen Lied", () => Tab("bearbeiten")),
            ("sample", "◧ Als Sample verwenden", () => Tab("bearbeiten")),
            ("stems", "🎚 Spuren holen", () => Tab("spuren")),
            ("midi", "🎹 MIDI holen", () => _ = MsMidi(s.Id)),
            ("persona", "👤 Persona aus diesem Lied", () => Tab("bearbeiten")),
            ("inspo", "🎧 Als Klangvorbild für ein neues Lied", () => { _msRefFile = s.File; _msMsg = $"Klangvorbild: „{s.Title}“ – jetzt beschreiben und „Erstellen“."; MsPage("erstellen"); }),
            ("clip", "📱 Kurzvideo 9:16 (Reels, Shorts)", () => _ = MsShortVideo(s.Id)),
            ("speak", "💬 Teil per Sprache ändern (Produzent)", () => Tab("profi")),
        ];
    }

    /// <summary>Knopf „Remix“ je Zeile (wie Suno) mit allen Bearbeitungen.</summary>
    Button MsRemixButton(MusicSong s, int n)
    {
        var b = new Button { Name = $"ms-remix-{n}", Content = "Remix", FontSize = 11.5, Padding = new Thickness(10, 3), CornerRadius = new CornerRadius(12), Background = Brush.Parse("#22FFFFFF"), BorderThickness = new Thickness(0), VerticalAlignment = VerticalAlignment.Center };
        ToolTip.SetTip(b, "Verlängern, Cover, Abschnitt ersetzen, Zuschneiden, Remaster, Spuren, MIDI …");
        var f = new MenuFlyout();
        foreach (var (id, label, run) in MsRemixActions(s)) { var mi = new MenuItem { Name = $"ms-remix-{n}-{id}", Header = label }; var r = run; mi.Click += (_, _) => r(); f.Items.Add(mi); }
        b.Flyout = f;
        return b;
    }

    /// <summary>Beschreibung, Liedtext und Eckdaten des Lieds in „Erstellen → Erweitert“ übernehmen (Suno „Reuse Prompt“).</summary>
    public void MsReusePrompt(string songId)
    {
        var s = Songs.Get(songId) ?? throw new InvalidOperationException("Dieses Lied gibt es nicht mehr.");
        _msPro = true; _msStyle = s.Style; _msTitle = s.Title; _msInstr = s.Lyrics.Trim() == "[Instrumental]";
        _msLyrics = _msInstr ? "" : s.Lyrics;
        _msLang = Math.Max(0, Array.FindIndex(AceStepClient.Languages, l => l.Code == s.Language));
        _msBpm = s.Bpm?.ToString(Inv) ?? ""; _msKey = s.Key ?? ""; _msTime = s.TimeSignature ?? "";
        _msMsg = $"Beschreibung von „{s.Title}“ übernommen – ändern und „Erstellen“.";
        MsPage("erstellen");
    }

    /// <summary>Remaster: dasselbe Lied neu, sehr nah am Original (ACE-Step „cover“, Stärke 0,85) und gemastert – als eigene Fassung.</summary>
    public async Task<MusicSong?> MsRemaster(string songId)
    {
        var s = Songs.Get(songId) ?? throw new InvalidOperationException("Dieses Lied gibt es nicht mehr.");
        var made = await MusicStudioCreate(new AceStepClient.Job(s.Style.Length > 0 ? s.Style : "same style", s.Lyrics, (int)Math.Round(s.Seconds), s.Language, 1, s.Bpm, s.Key, s.TimeSignature, Task: "cover", CoverStrength: 0.85), s.Title, s.Id);
        if (made.FirstOrDefault() is not { } m) return null;
        Songs.Update(m.Id, x => { x.Title = s.Title + " – Remaster"; x.Kind = "remaster"; });
        _msMsg = $"✓ Remaster von „{s.Title}“ fertig – mit dem Original vergleichen (Reiter Lied → Fassungen).";
        Show(MS);
        return Songs.Get(m.Id);
    }

    /// <summary>Für Tests: FFmpeg-Lauf beim Zuschneiden ersetzen.</summary>
    public Func<IReadOnlyList<string>, Cli.Result>? MsCropRunOverride { get; set; }

    /// <summary>FFmpeg-Argumente: Bereich behalten (<paramref name="keep"/>) oder herausschneiden (Teile mit 30 ms Übergang zusammenfügen).</summary>
    public static List<string> CropArgs(string input, string output, double from, double to, double total, bool keep)
    {
        string F(double v) => v.ToString("0.###", Inv);
        if (keep) return ["-hide_banner", "-loglevel", "error", "-y", "-ss", F(from), "-to", F(to), "-i", input, "-af", "afade=t=in:d=0.01,areverse,afade=t=in:d=0.03,areverse", output];
        if (from <= 0.05) return ["-hide_banner", "-loglevel", "error", "-y", "-ss", F(to), "-i", input, "-af", "afade=t=in:d=0.02", output];
        if (to >= total - 0.05) return ["-hide_banner", "-loglevel", "error", "-y", "-to", F(from), "-i", input, "-af", "areverse,afade=t=in:d=0.03,areverse", output];
        return ["-hide_banner", "-loglevel", "error", "-y", "-i", input, "-filter_complex",
            $"[0:a]atrim=0:{F(from)},asetpts=PTS-STARTPTS[a];[0:a]atrim={F(to)},asetpts=PTS-STARTPTS[b];[a][b]acrossfade=d=0.03:c1=tri:c2=tri[o]", "-map", "[o]", output];
    }

    /// <summary>Zuschneiden (nur von–bis behalten) oder Abschnitt entfernen – neue Fassung „… – gekürzt“, das Original bleibt.</summary>
    public async Task<MusicSong?> MsCrop(string songId, double from, double to, bool keep)
    {
        var s = Songs.Get(songId) ?? throw new InvalidOperationException("Dieses Lied gibt es nicht mehr.");
        if (to - from < 0.5) throw new ArgumentException("Der Bereich muss mindestens eine halbe Sekunde lang sein.");
        if (from < 0 || to > s.Seconds + 0.5) throw new ArgumentException($"Der Bereich liegt außerhalb des Lieds (0–{s.Seconds:0} s).");
        if (keep && from <= 0.05 && to >= s.Seconds - 0.05) throw new ArgumentException("Das wäre das ganze Lied – nichts zu schneiden.");
        if (!keep && from <= 0.05 && to >= s.Seconds - 0.05) throw new ArgumentException("Dann bliebe nichts übrig.");
        var ext = Path.GetExtension(s.File).ToLowerInvariant() is ".wav" or ".flac" ? Path.GetExtension(s.File) : ".mp3";
        var outFile = Path.Combine(Path.GetDirectoryName(s.File)!, Path.GetFileNameWithoutExtension(s.File) + " – gekürzt" + ext);
        for (var i = 2; File.Exists(outFile); i++) outFile = Path.Combine(Path.GetDirectoryName(s.File)!, Path.GetFileNameWithoutExtension(s.File) + $" – gekürzt {i}" + ext);
        var args = CropArgs(s.File, outFile, from, to, s.Seconds, keep);
        Cli.Result r;
        if (MsCropRunOverride is { } o) r = o(args);
        else if (FfmpegPath is { } ff) r = await Task.Run(() => Cli.Run(ff, args, Path.GetDirectoryName(s.File), 600_000));
        else { _msMsg = "Zum Schneiden fehlt FFmpeg – unter Zusatzprogramme einrichten."; Show(MS); return null; }
        if (!r.Ok || !File.Exists(outFile)) { _msMsg = "Schneiden ging nicht: " + (r.Err.Trim().Split('\n').LastOrDefault() ?? ""); Show(MS); return null; }
        var len = keep ? to - from : s.Seconds - (to - from);
        var m = Songs.Add(new MusicSong { Title = s.Title + " – gekürzt", Style = s.Style, Lyrics = s.Lyrics, Language = s.Language, Seconds = Math.Round(len, 2), Bpm = s.Bpm, Key = s.Key,
            TimeSignature = s.TimeSignature, File = outFile, Kind = "gekuerzt", ParentId = s.Id, Mastered = s.Mastered, CoverFile = s.CoverFile });
        _core.Audit.Append(Principal.LocalOwner.Name, "musik.studio.gekuerzt", $"{s.Id} {(keep ? "behalten" : "entfernt")} {from:0.0}-{to:0.0}");
        _msSel = m.Id; _msMsg = keep ? $"✓ Zugeschnitten auf {from:0.0}–{to:0.0} s ({SongTime(len)})." : $"✓ {from:0.0}–{to:0.0} s entfernt, weich übergeblendet ({SongTime(len)}).";
        Show(MS);
        return m;
    }

    /// <summary>Feld „Zuschneiden / Abschnitt entfernen“ im Reiter Bearbeiten.</summary>
    Control MsCropPanel(MusicSong s)
    {
        var from = new NumericUpDown { Name = "ms-crop-from", Minimum = 0, Maximum = (decimal)Math.Max(1, s.Seconds), Value = 0, Increment = 1, FormatString = "0.0", Width = 100 };
        var to = new NumericUpDown { Name = "ms-crop-to", Minimum = 0, Maximum = (decimal)Math.Max(1, s.Seconds), Value = (decimal)Math.Min(30, s.Seconds), Increment = 1, FormatString = "0.0", Width = 100 };
        async Task Run(bool keep) { try { await MsCrop(s.Id, (double)(from.Value ?? 0), (double)(to.Value ?? 0), keep); } catch (ArgumentException e) { _msMsg = e.Message; Show(MS); } }
        return new StackPanel { Name = "ms-crop", Spacing = 4, Children = { H("Zuschneiden / Abschnitt entfernen", 13),
            P("Ohne neue KI: schneidet die Datei, mit weichem Übergang. Das Original bleibt erhalten.", Muted, 11.5),
            new WrapPanel { Children = { new TextBlock { Text = "von", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) }, from,
                new TextBlock { Text = "bis", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 6, 0) }, to, new TextBlock { Text = "s", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0) } } },
            new WrapPanel { Children = { Act("ms-crop-keep", "✂ Nur das behalten", () => _ = Run(true)), new Border { Width = 6 }, Act("ms-crop-remove", "Diesen Teil entfernen", () => _ = Run(false)) } } } };
    }
}
