using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Media;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>Videoschnitt X1/X2/X11/X16 (freigegeben 04.11.2026): Export-Vorlage wählen, Grafikkarten-Encoder anzeigen, nur einen Bereich,
/// Lautheit −14 LUFS – und das Projekt als EDL oder FCPXML an Resolve, Premiere oder Final Cut übergeben.</summary>
public partial class MainWindow
{
    /// <summary>Gewählte Vorlage (öffentlich für Tests und die KI).</summary>
    public string EditPreset { get; set; } = "projekt";
    public bool EditLoudness { get; set; }
    public bool EditDucking { get; set; }
    public bool EditBurnSubs { get; set; }
    /// <summary>Für Tests: Ersatz für Whisper (Ton-WAV, Ziel-SRT).</summary>
    public Func<string, string, Task<bool>>? SubsOverride { get; set; }
    bool _veSubsBusy;
    public (double From, double To) EditRange { get; set; }
    IReadOnlySet<string>? _veGpu;

    public VideoExport.Options EditExportOptions(ProgramDocument d) => new(VideoExport.Get(EditPreset), _veGpu, EditRange.From, EditRange.To, EditLoudness, EditDucking, EditBurnSubs && d.Text("subs") is { Length: > 0 } sb && File.Exists(sb) ? sb : null);

    /// <summary>Übergabe-Datei schreiben (EDL oder FCPXML) – neben die Exporte.</summary>
    public string EditInterchange(ProgramDocument d, string kind)
    {
        if (VideoEdit.Clips(d.Data).Count == 0) throw new InvalidOperationException("Die Bildspur ist leer – zuerst Videos oder Bilder hinzufügen.");
        var file = Path.Combine(WorkFolder(Programs[VK].Folder), DevStudio.SafeName(d.Title) + (kind == "edl" ? ".edl" : ".fcpxml"));
        File.WriteAllText(file, kind == "edl" ? VideoExport.Edl(d.Data, d.Title) : VideoExport.Fcpxml(d.Data, d.Title));
        _core.Audit.Append(Principal.LocalOwner.Name, "video.interchange", Path.GetFileName(file));
        _progMsg[VK] = $"Gespeichert: {file} – öffnen mit DaVinci Resolve, Premiere oder Final Cut.";
        return file;
    }

    /// <summary>X10: Untertitel aus dem Ton des Projekts (Whisper, lokal). Die SRT liegt neben den Exporten und kommt ins Projekt (rückgängig machbar).</summary>
    public async Task EditSubtitles(ProgramDocument d)
    {
        if (_veSubsBusy) return;
        if (VideoEdit.Clips(d.Data).Count == 0) { _progMsg[VK] = "Die Bildspur ist leer – zuerst Videos hinzufügen."; Show(VK); return; }
        var srt = Path.Combine(WorkFolder(Programs[VK].Folder), DevStudio.SafeName(d.Title) + ".srt");
        var tmp = Path.Combine(Path.GetTempPath(), "trendo-untertitel-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(tmp);
        _veSubsBusy = true; _progMsg[VK] = "Untertitel: Trendo hört den Ton ab (Whisper, auf diesem PC) …"; Show(VK);
        try
        {
            var wav = Path.Combine(tmp, "ton.wav"); bool ok;
            if (SubsOverride is { } fake) ok = await fake(wav, srt);
            else
            {
                if (_opt.Isolated) throw new InvalidOperationException("Im abgeschotteten Testmodus startet Trendo kein FFmpeg und kein Whisper.");
                var ff = FfmpegPath ?? throw new InvalidOperationException("Für Untertitel braucht Trendo FFmpeg – unter Zusatzprogramme einrichten.");
                var wm = WhisperModel() ?? throw new InvalidOperationException("Für Untertitel fehlt ein Whisper-Modell – in der KI-Bibliothek unter „Sprache zu Text“ mit einem Klick laden.");
                var data = (JsonObject)d.Data.DeepClone(); var m4a = Path.Combine(tmp, "ton.m4a");
                await Task.Run(() => VideoEdit.Export(ff, data, m4a, o: new VideoExport.Options(VideoExport.Get("ton"))));
                var c = await Task.Run(() => Cli.Run(ff, ["-y", "-i", m4a, "-ar", "16000", "-ac", "1", "-c:a", "pcm_s16le", wav], timeoutMs: 600_000));
                if (!c.Ok) throw new InvalidOperationException("Ton konnte nicht vorbereitet werden.");
                using var gpu = await GpuSlot("sprache", "Untertitel");
                var b = Path.Combine(tmp, "untertitel");
                var r = await Task.Run(() => Cli.Run(KiStore.RuntimeExe(KiRuntime.WhisperCpp), ["-m", KiStore.ModelFile(wm, wm.Files[0]), "-f", wav, "-l", "auto", "-ml", "42", "-osrt", "-of", b], timeoutMs: 1_800_000));
                ok = r.Ok && File.Exists(b + ".srt");
                if (ok) File.Copy(b + ".srt", srt, true);
            }
            if (!ok || !File.Exists(srt)) throw new InvalidOperationException("Whisper hat keine Untertitel geliefert.");
            var n = VideoExport.ReadSrt(await File.ReadAllTextAsync(srt)).Count;
            d.Edit("Untertitel", x => x["subs"] = srt);
            _core.Audit.Append(Principal.LocalOwner.Name, "video.untertitel", $"{n} Zeilen");
            _progMsg[VK] = $"✓ {n} Untertitel erkannt – gespeichert als {Path.GetFileName(srt)}. Beim Export einbrennen oder als SRT mitgeben.";
        }
        catch (Exception e) when (e is InvalidOperationException or IOException or FileNotFoundException) { _progMsg[VK] = e.Message; }
        finally { _veSubsBusy = false; try { Directory.Delete(tmp, true); } catch (IOException) { } Show(VK); }
    }

    /// <summary>Nach dem Export: SRT gleichnamig neben das Video legen (z. B. für YouTube).</summary>
    void EditCopySubs(JsonObject data, string video)
    {
        if (data["subs"]?.GetValue<string>() is { Length: > 0 } s && File.Exists(s) && !video.EndsWith(".m4a", StringComparison.OrdinalIgnoreCase))
            try
            {
                var srtOut = Path.ChangeExtension(video, ".srt");
                if (!string.Equals(Path.GetFullPath(s), Path.GetFullPath(srtOut), StringComparison.OrdinalIgnoreCase)) File.Copy(s, srtOut, true); // gleicher Name im gleichen Ordner: liegt schon da
                File.WriteAllText(Path.ChangeExtension(video, ".vtt"), TimedText.ToVtt(TimedText.FromSrt(File.ReadAllText(s)), "de")); // Paket 01 S3-16: auch WebVTT
                foreach (var (lang, node) in data["subsLang"] as JsonObject ?? [])
                    if (node?.GetValue<string>() is { } ls && File.Exists(ls))
                    {
                        var lsOut = Path.ChangeExtension(video, $".{lang}.srt");
                        if (!string.Equals(Path.GetFullPath(ls), Path.GetFullPath(lsOut), StringComparison.OrdinalIgnoreCase)) File.Copy(ls, lsOut, true);
                        File.WriteAllText(Path.ChangeExtension(video, $".{lang}.vtt"), TimedText.ToVtt(TimedText.FromSrt(File.ReadAllText(ls)), lang));
                    }
            }
            catch (Exception e) when (e is IOException or ArgumentException) { }
    }

    /// <summary>Paket 01 S3-16: Sprachfassung der Untertitel – eine übersetzte Zeile je Untertitel, Zeiten exakt vom Original (sonst Fehler).</summary>
    public string EditSubsLanguage(ProgramDocument d, string lang, string text)
    {
        var src = d.Text("subs"); if (src.Length == 0 || !File.Exists(src)) throw new InvalidOperationException("Erst Untertitel erzeugen.");
        if (!TimedText.Languages.Any(l => l.Code == lang)) throw new ArgumentException("Sprache: de, it oder en.");
        var orig = TimedText.FromSrt(File.ReadAllText(src));
        var lines = text.Replace("\r\n", "\n").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        var tr = TimedText.Translate(orig, lines);
        var f = Path.Combine(Path.GetDirectoryName(src)!, Path.GetFileNameWithoutExtension(src) + $".{lang}.srt");
        File.WriteAllText(f, TimedText.ToSrt(tr));
        d.Edit("Sprachfassung", x => { var o = x["subsLang"] as JsonObject ?? (JsonObject)(x["subsLang"] = new JsonObject()); o[lang] = f; });
        _progMsg[VK] = $"Sprachfassung {TimedText.Languages.First(l => l.Code == lang).Label}: {tr.Count} Untertitel, Zeiten wie im Original – kommt als .{lang}.srt und .{lang}.vtt neben jedes Video.";
        return f;
    }

    Control EditExportSettings(ProgramDocument d)
    {
        var sp = new StackPanel { Name = "ve-export-settings", Spacing = 6 };
        sp.Children.Add(H("Exportieren", 13));
        // Vorlagen als Kacheln: ein Klick wählt
        var grid = new WrapPanel();
        foreach (var p in VideoExport.Presets)
        {
            var on = p.Id == EditPreset; var id = p.Id;
            var b = new Button { Name = "ve-preset-" + p.Id, Width = 150, Margin = new Thickness(0, 0, 6, 6), Padding = new Thickness(8, 6), CornerRadius = new CornerRadius(8),
                Background = on ? Brush.Parse("#33F5B83D") : TileBg, BorderBrush = on ? Brush.Parse("#F5B83D") : Edge, BorderThickness = new Thickness(on ? 2 : 1), HorizontalContentAlignment = HorizontalAlignment.Left,
                Content = new StackPanel { Children = { new TextBlock { Text = p.Label, FontSize = 12.5, FontWeight = FontWeight.SemiBold, Foreground = Text }, new TextBlock { Text = p.Hint, FontSize = 10.5, Foreground = Muted, TextWrapping = TextWrapping.Wrap } } } };
            b.Click += (_, _) => { EditPreset = id; Show(VK); };
            grid.Children.Add(b);
        }
        sp.Children.Add(grid);
        var loud = new CheckBox { Name = "ve-loud", Content = "Lautheit angleichen (−14 LUFS, wie YouTube und Spotify)", IsChecked = EditLoudness, FontSize = 12 };
        loud.IsCheckedChanged += (_, _) => EditLoudness = loud.IsChecked == true;
        sp.Children.Add(loud);
        if (VideoEdit.Clips(d.Data, "music").Count > 0)
        {
            var duck = new CheckBox { Name = "ve-duck", Content = "Musik leiser, wenn gesprochen wird (Ducking)", IsChecked = EditDucking, FontSize = 12 };
            duck.IsCheckedChanged += (_, _) => EditDucking = duck.IsChecked == true;
            sp.Children.Add(duck);
        }
        var len = VideoEdit.VideoLength(VideoEdit.Clips(d.Data));
        var range = new CheckBox { Name = "ve-range", Content = "Nur einen Bereich exportieren", IsChecked = EditRange.To > EditRange.From, FontSize = 12 };
        var ra = new NumericUpDown { Name = "ve-range-a", Value = (decimal)Math.Round(EditRange.From, 1), Minimum = 0, Maximum = (decimal)Math.Max(1, Math.Ceiling(len)), Increment = 0.5m, FormatString = "0.0", Width = 120, FontSize = 12 };
        var rb = new NumericUpDown { Name = "ve-range-b", Value = (decimal)Math.Round(EditRange.To > 0 ? EditRange.To : len, 1), Minimum = 0, Maximum = (decimal)Math.Max(1, Math.Ceiling(len)), Increment = 0.5m, FormatString = "0.0", Width = 120, FontSize = 12 };
        void SetRange() => EditRange = range.IsChecked == true ? ((double)(ra.Value ?? 0), (double)(rb.Value ?? 0)) : default;
        range.IsCheckedChanged += (_, _) => SetRange(); ra.ValueChanged += (_, _) => SetRange(); rb.ValueChanged += (_, _) => SetRange();
        sp.Children.Add(range);
        sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { new TextBlock { Text = "von", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center }, ra, new TextBlock { Text = "bis", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center }, rb, new TextBlock { Text = "Sekunden", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center } } });
        // X10: Untertitel
        var subs = d.Text("subs");
        var subsBtn = new Button { Name = "ve-subs", Content = _veSubsBusy ? "Untertitel werden erstellt …" : subs.Length > 0 ? "Untertitel neu erzeugen" : "💬 Untertitel erzeugen (Whisper)", FontSize = 12, IsEnabled = !_veSubsBusy };
        subsBtn.Click += async (_, _) => await EditSubtitles(d);
        sp.Children.Add(subsBtn);
        if (subs.Length > 0 && File.Exists(subs))
        {
            var cues = VideoExport.ReadSrt(File.ReadAllText(subs));
            sp.Children.Add(P($"{cues.Count} Untertitel – z. B. „{(cues.Count > 0 ? cues[0].Text : "")}“. Die SRT kommt neben jedes exportierte Video.", Muted, 11.5));
            var burn = new CheckBox { Name = "ve-burn", Content = "Untertitel ins Bild einbrennen", IsChecked = EditBurnSubs, FontSize = 12 };
            burn.IsCheckedChanged += (_, _) => EditBurnSubs = burn.IsChecked == true;
            sp.Children.Add(burn);
            // Paket 01 S3-16: Sprachfassungen (Zeiten bleiben exakt gleich)
            var langs = TimedText.Languages.Where(l => l.Code != "de").ToArray();
            var lang = new ComboBox { Name = "ve-subs-lang", ItemsSource = langs.Select(l => l.Label).ToList(), SelectedIndex = 0, MinWidth = 120, FontSize = 12 };
            var trans = new TextBox { Name = "ve-subs-trans", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 60, FontSize = 12, Watermark = $"Übersetzung: {cues.Count} Zeilen, eine je Untertitel" };
            var save = new Button { Name = "ve-subs-lang-save", Content = "Sprachfassung übernehmen", FontSize = 12 };
            save.Click += (_, _) => ProgramRun(VK, () => EditSubsLanguage(d, langs[Math.Max(0, lang.SelectedIndex)].Code, trans.Text ?? ""));
            var have = (d.Data["subsLang"] as JsonObject)?.Select(x => x.Key).ToList() ?? [];
            sp.Children.Add(new WrapPanel { Children = { lang, new Border { Width = 6 }, save } }); sp.Children.Add(trans);
            if (have.Count > 0) sp.Children.Add(P("Sprachfassungen: " + string.Join(", ", have.Select(h => TimedText.Languages.FirstOrDefault(l => l.Code == h).Label ?? h)) + " – plus WebVTT (.vtt) für Browser und YouTube.", Muted, 11));
        }
        sp.Children.Add(EditCreditsPanel(d)); // Paket 01 S3-17: Abspann
        // Grafikkarte: einmal messen, ehrlich anzeigen
        var gpuText = _veGpu is null ? "Grafikkarte: wird beim ersten Export geprüft." : _veGpu.Count == 0 ? "Grafikkarte: kein NVENC – Trendo kodiert mit dem Prozessor (gleiche Qualität, langsamer)."
            : "Grafikkarte: " + string.Join(", ", _veGpu.Select(e => e.Replace("_nvenc", "").ToUpperInvariant().Replace("H264", "H.264").Replace("HEVC", "H.265"))) + " über NVENC – schnell.";
        var pr = VideoExport.Get(EditPreset);
        var (enc, _) = VideoExport.Encoder(pr, _veGpu, Math.Max(1, len));
        sp.Children.Add(P(gpuText + (pr.AudioOnly ? "" : $" Encoder für diese Vorlage: {enc}."), Muted, 11.5));
        var go = new Button { Name = "ve-export-go", Content = pr.AudioOnly ? "⬇ Ton exportieren" : "⬇ Exportieren", Background = Accent, Foreground = Brushes.White, FontSize = 13, Padding = new Thickness(16, 7), CornerRadius = new CornerRadius(8), IsEnabled = _veExport is null };
        go.Click += async (_, _) => await EditExportBackground(d);
        sp.Children.Add(go);
        // X16: Übergabe an andere Schnittprogramme
        sp.Children.Add(H("An anderes Schnittprogramm geben", 12.5));
        var edl = new Button { Name = "ve-edl", Content = "EDL (Resolve, Premiere, Avid)", FontSize = 12, Margin = new Thickness(0, 0, 6, 0) };
        edl.Click += (_, _) => ProgramRun(VK, () => EditInterchange(d, "edl"));
        var fcp = new Button { Name = "ve-fcpxml", Content = "FCPXML (Final Cut, Resolve)", FontSize = 12 };
        fcp.Click += (_, _) => ProgramRun(VK, () => EditInterchange(d, "fcpxml"));
        sp.Children.Add(new WrapPanel { Children = { edl, fcp } });
        return sp;
    }
}
