using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Media;

namespace Trendo.App;

/// <summary>KI-Musikvideo C, G, D, F (freigegeben 04.11.2026): aus dem Drehbuch echte Filmszenen rendern.
/// Ablauf je Einstellung: Szene (Wan 2.2 TI2V) oder singende Figur (Wan 2.2 S2V, zur Gesangsspur) → auf Länge, Format (16:9 oder 9:16) und
/// Filmlook bringen (D2/D3) → prüfen (G: Körper, Hände, Gesichter, Bildsprünge, Lippen) → bei Fehlern bis zu 3-mal neu mit strengerer
/// Beschreibung (G6) → sonst rot markiert (nie still übernommen). F1: zuerst eine schnelle Vorschau des ganzen Videos, dann die Endfassung.
/// F2: Fortschritt mit Restzeit, abbrechbar; bereits fertige Einstellungen bleiben (weiter nach Neustart). Danach (D1) liegt alles als
/// verknüpftes Projekt im Videoschnitt – jede Einstellung genau auf ihrem Takt, Ton vom Lied.</summary>
public partial class MainWindow
{
    /// <summary>Tests: ersetzt jeden Aufruf von sd-cli/ffmpeg (Programm, Argumente) – schreibt die Ausgabe selbst. Nie echte KIs im Test.</summary>
    public Func<string, IReadOnlyList<string>, Task<bool>>? VideoKiOverride { get; set; }
    /// <summary>Tests: ersetzt die Artefakt-Prüfung (Video, Versuch) → JSON von trendo_pruefung.py.</summary>
    public Func<string, int, Task<string>>? VideoCheckOverride { get; set; }

    /// <summary>Tests: ersetzt den zweiten Blick der Bild-KI (Clip → Antwort „OK“ oder „ERROR: …“).</summary>
    public Func<string, Task<string?>>? VisionCheckOverride { get; set; }
    const string VisionModel = "qwen2.5vl:7b";

    /// <summary>G2: drei Standbilder (¼, ½, ¾) an Qwen2.5-VL – nur wenn das Modell schon eingerichtet ist.</summary>
    async Task<MusicVideoRender.Issue?> VisionCheck(string clip, MusicScript.Shot x, MusicScript.Script sc)
    {
        string? answer;
        if (VisionCheckOverride is { } o) answer = await o(clip);
        else
        {
            if (_opt.Isolated || FfmpegPath is not { } ff || !await _ollama.HasModelAsync(VisionModel)) return null;
            var imgs = new List<byte[]>();
            var len = x.End - x.Start;
            foreach (var k in new[] { 0.25, 0.5, 0.75 })
            {
                var png = clip + $".blick{(int)(k * 100)}.png";
                if (Cli.Run(ff, ["-hide_banner", "-loglevel", "error", "-y", "-ss", (len * k).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture), "-i", clip, "-frames:v", "1", "-vf", "scale=768:-2", png], timeoutMs: 60_000).Ok && File.Exists(png))
                { imgs.Add(await File.ReadAllBytesAsync(png)); TryDelete(png); }
            }
            if (imgs.Count == 0) return null;
            using var gpu = await GpuSlot("bild-pruefung", "Bild-KI prüft Einstellung " + (x.Index + 1));
            answer = await _ollama.AskImagesAsync(VisionModel, MusicVideoRender.VisionQuestion(MusicVideoRender.ExpectedPeople(x, sc.Figures)), imgs);
        }
        return MusicVideoRender.ParseVision(answer);
    }

    public sealed record ShotClip(int Index, string File, bool Ok, string Report, string Quality);
    string _mvFmt = "16:9"; CancellationTokenSource? _mvKiCts; string _mvKiStatus = "";
    public bool VideoKiBusy => _mvKiCts is not null;

    static string ClipsFile(MusicSong s, string q) => s.File + $".clips-{q}.json";
    public List<ShotClip> ShotClips(MusicSong s, string q)
    {
        try { return File.Exists(ClipsFile(s, q)) ? (System.Text.Json.JsonSerializer.Deserialize<List<ShotClip>>(File.ReadAllText(ClipsFile(s, q))) ?? []).Where(c => File.Exists(c.File)).ToList() : []; }
        catch (System.Text.Json.JsonException) { return []; }
    }
    void SaveClips(MusicSong s, string q, List<ShotClip> l) => File.WriteAllText(ClipsFile(s, q), System.Text.Json.JsonSerializer.Serialize(l.OrderBy(c => c.Index)));

    KiLibraryModel? Model(string id) => KiLibrary.Models.FirstOrDefault(m => m.Id == id) is { } m && KiStore.ModelReady(m) && KiStore.RuntimeReady(m.Runtime) ? m : null;
    bool ScenesReady => VideoKiOverride is not null || Model("wan2.2-szenen") is not null;
    bool SingReady => VideoKiOverride is not null || (Model("wan2.2-singen") is not null && Model("wan2.2-szenen") is not null);
    bool CheckReady => VideoCheckOverride is not null || Model("video-pruefung") is not null;

    (int W, int H) MvSize(bool preview) => (_mvFmt == "9:16") ? (preview ? (360, 640) : (1080, 1920)) : (preview ? (640, 360) : (1920, 1080));

    async Task<bool> Tool(string exe, IReadOnlyList<string> args, CancellationToken ct)
    {
        if (VideoKiOverride is { } o) return await o(exe, args);
        if (_opt.Isolated) return false;
        ct.ThrowIfCancellationRequested();
        var r = await Task.Run(() => Cli.Run(exe, args, timeoutMs: 3_600_000), ct);
        return r.Ok;
    }

    /// <summary>B3: Figurenbild je Figur (einmal; danach Bezug für jede Einstellung).</summary>
    async Task<string?> FigureImage(MusicSong s, MusicScript.Figure f, CancellationToken ct)
    {
        var png = s.File + $".figur-{f.Id}.png";
        if (File.Exists(png)) return png;
        var exe = VideoKiOverride is not null ? "sd-cli" : CoverModel() is { } cm ? cm.Exe : null;
        if (exe is null) return null;
        var model = VideoKiOverride is not null ? "bild.safetensors" : KiStore.ModelFile(CoverModel()!.Value.Model, CoverModel()!.Value.Model.Files[0]);
        using var gpu = await GpuSlot("bild", "Figurenbild " + f.Role, ct);
        var (fw, fh) = MusicVideoRender.KeyframeSize(_mvFmt == "9:16");
        var ok = await Tool(exe, ["-m", model, "-p", MusicVideoRender.FigurePrompt(f, s.Style), "-n", MusicScript.Negative, "-o", png, "--steps", "6", "--cfg-scale", "1.5", "-W", fw.ToString(), "-H", fh.ToString(), "--seed", (4242 + f.Id.Sum(ch => ch)).ToString(), "--vae-tiling", "--offload-to-cpu"], ct);
        return ok && File.Exists(png) ? png : null;
    }

    MusicScript.Script sc0(MusicSong s) => Script(s) ?? throw new InvalidOperationException("Kein Drehbuch.");

    /// <summary>Startbild einer Szene: Figur am Drehort in Einstellungsgröße und Licht (statt des Studio-Porträts).</summary>
    async Task<string?> Keyframe(MusicSong s, MusicScript.Shot x, string prompt, string dir, CancellationToken ct)
    {
        var png = Path.Combine(dir, $"start{x.Index:000}-{x.Seed}.png");
        if (File.Exists(png)) return png;
        var exe = VideoKiOverride is not null ? "sd-cli" : CoverModel() is { } cm ? cm.Exe : null;
        if (exe is null) return null;
        var model = VideoKiOverride is not null ? "bild.safetensors" : KiStore.ModelFile(CoverModel()!.Value.Model, CoverModel()!.Value.Model.Files[0]);
        var (w, h) = MusicVideoRender.KeyframeSize(_mvFmt == "9:16");
        using var gpu = await GpuSlot("bild", "Startbild " + (x.Index + 1), ct);
        var people = MusicVideoRender.ExpectedPeople(x, sc0(s).Figures);
        // G vorab: das Startbild wird geprüft, bevor es bewegt wird – bis zu 3 Zufallswerte (z. B. gegen eine verdoppelte Figur)
        for (var k = 0; k < MusicVideoRender.MaxTries; k++)
        {
            var seed = x.Seed + k * 977;
            var ok = await Tool(exe, ["-m", model, "-p", MusicVideoRender.KeyframePrompt(x, prompt, people), "-n", MusicVideoRender.KeyframeNegative(people), "-o", png, "--steps", "6", "--cfg-scale", "1.5", "-W", w.ToString(), "-H", h.ToString(), "--seed", seed.ToString(), "--vae-tiling", "--offload-to-cpu"], ct);
            if (!ok || !File.Exists(png)) return null;
            if (!CheckReady) return png;
            var chk = await CheckClip(png, x, sc0(s), null, null, k + 1);
            if (chk is null || chk.Ok) return png;
            _core.Audit.Append(Principal.LocalOwner.Name, "musik.video.startbild", $"#{x.Index} neu: {string.Join("; ", chk.Issues.Select(i => i.Text))}");
            TryDelete(png);
        }
        return null;
    }

    async Task<MusicVideoRender.Check?> CheckClip(string clip, MusicScript.Shot x, MusicScript.Script sc, string? figure, string? vocalClip, int attempt)
    {
        string? json = null;
        var expect = MusicVideoRender.ExpectedPeople(x, sc.Figures);
        if (VideoCheckOverride is { } o) json = await o(clip, attempt);
        else if (Model("video-pruefung") is { } m)
        {
            var outFile = clip + ".pruefung.json";
            var args = new List<string> { clip, outFile, "--modelle", KiStore.ModelDir(m), "--personen", expect.ToString() };
            if (x.Singing && vocalClip is not null) { args.Add("--singt"); args.Add("--gesang"); args.Add(vocalClip); }
            if (figure is not null && expect == 1) { args.Add("--referenz"); args.Add(figure); }
            var r = await KiPyRun(KiRuntime.VideoPruefung, args);
            json = r.Ok && File.Exists(outFile) ? await File.ReadAllTextAsync(outFile) : null;
        }
        var chk = json is null ? null : MusicVideoRender.ParseCheck(json);
        if (chk is { Ok: false }) return chk; // grober Fehler schon gefunden – kein zweiter Blick nötig
        if (await VisionCheck(clip, x, sc) is { } vi) return new MusicVideoRender.Check(false, [.. chk?.Issues ?? [], vi]);
        return chk ?? (VisionCheckOverride is not null ? new MusicVideoRender.Check(true, []) : null);
    }

    /// <summary>Einstellungen rendern (alle offenen oder nur <paramref name="only"/>). Gibt die Zahl fertiger, geprüfter Einstellungen zurück.</summary>
    public async Task<int> MsRenderVideo(string songId, bool preview, int? only = null)
    {
        if (Songs.Get(songId) is not { } s || Script(s) is not { } sc) { _msMsg = "Zuerst „Lied verstehen“ und „Drehbuch erstellen“."; Show(MS); return 0; }
        if (!ScenesReady) { _msMsg = "Für echte Filmszenen bitte in der KI-Bibliothek „Wan 2.2 – Filmszenen“ einrichten (ca. 11,7 GB)."; Show(MS); return 0; }
        if (_mvKiCts is not null) return 0;
        var cts = _mvKiCts = new CancellationTokenSource(); var ct = cts.Token;
        var q = preview ? "vorschau" : "final";
        var (w, h) = MvSize(preview);
        var dir = Path.Combine(Path.GetDirectoryName(s.File)!, DevStudio.SafeName(s.Title) + " – Musikvideo", q); Directory.CreateDirectory(dir);
        var done = ShotClips(s, q).ToDictionary(c => c.Index);
        var todo = sc.Shots.Where(x => only is null ? !(done.TryGetValue(x.Index, out var c) && c.Ok) : x.Index == only).ToList();
        var vocal = VocalStemOf(s)?.File;
        var lut = Path.Combine(dir, "look.cube"); await File.WriteAllTextAsync(lut, MusicVideo.Cube(sc.Look), ct);
        var started = DateTime.Now; var n = 0; var okCount = 0;
        void Say(string t) { _mvKiStatus = t; _msMsg = t; if (CurrentView == MS) Show(MS); }
        try
        {
            var figs = new Dictionary<string, string?>();
            foreach (var f in sc.Figures) figs[f.Id] = await FigureImage(s, f, ct);
            var sdcli = VideoKiOverride is not null ? "sd-cli" : KiStore.RuntimeExe(KiRuntime.StableDiffusionCpp);
            var ff = VideoKiOverride is not null ? "ffmpeg" : FfmpegPath ?? throw new InvalidOperationException("Für das Musikvideo braucht Trendo FFmpeg (unter „Programme“ einrichten).");
            foreach (var shot in todo)
            {
                ct.ThrowIfCancellationRequested();
                n++;
                var per = n > 1 ? (DateTime.Now - started).TotalMinutes / (n - 1) : 0;
                var rest = per > 0 ? $" – noch ca. {Math.Ceiling(per * (todo.Count - n + 1))} min" : "";
                var fig = sc.Figures.FirstOrDefault(f => f.Role == shot.Who || shot.Who.StartsWith(f.Role)) ?? (shot.Who == "beide" ? sc.Figures.FirstOrDefault() : null);
                var figImg = fig is null ? null : figs.GetValueOrDefault(fig.Id);
                var x = shot; var prompt = shot.Prompt; var tries = new List<MusicVideoRender.Check>(); string? clip = null; var ok = false;
                for (var attempt = 1; attempt <= MusicVideoRender.MaxTries; attempt++)
                {
                    Say($"{(preview ? "Vorschau" : "Endfassung")}: Einstellung {n} von {todo.Count}" + (attempt > 1 ? $" (Versuch {attempt} – Fehler wird behoben)" : "") + rest);
                    var raw = Path.Combine(dir, $"roh{x.Index:000}-{attempt}.webm");
                    string? vclip = null;
                    var sing = x.Singing && figImg is not null && vocal is not null && SingReady && x.Who != "beide";
                    using (var gpu = await GpuSlot("video", $"Einstellung {x.Index + 1}", ct))
                    {
                        bool made;
                        if (sing)
                        {
                            vclip = Path.Combine(dir, $"gesang{x.Index:000}.wav");
                            if (!File.Exists(vclip)) await Tool(ff, MusicVideoRender.VocalClipArgs(vocal!, x, vclip), ct);
                            var m = Model("wan2.2-singen"); var sm = Model("wan2.2-szenen");
                            var models = m is null || sm is null ? new MusicVideoRender.SingModels("s2v.gguf", "vae21", "t5", "w2v") : new(KiStore.ModelFile(m, m.Files[0]), KiStore.ModelFile(m, m.Files[2]), KiStore.ModelFile(sm, sm.Files[1]), KiStore.ModelFile(m, m.Files[1]));
                            gpu.Dispose(); // auch beim Singen: Startbild am Drehort statt Studio-Porträt (Befund 04.11.2026)
                            var key = await Keyframe(s, x, prompt, dir, ct);
                            using var gpu2 = await GpuSlot("video", $"Einstellung {x.Index + 1}", ct);
                            made = await Tool(sdcli, MusicVideoRender.SingArgs(models, x, prompt, key ?? figImg!, vclip, raw, preview ? MusicVideoRender.SingPreview : MusicVideoRender.SingFinal, x.Seed), ct);
                        }
                        else
                        {
                            var m = Model("wan2.2-szenen");
                            var models = m is null ? new MusicVideoRender.SceneModels("ti2v.gguf", "vae22", "t5") : new(KiStore.ModelFile(m, m.Files[0]), KiStore.ModelFile(m, m.Files[2]), KiStore.ModelFile(m, m.Files[1]));
                            gpu.Dispose(); // Startbild braucht die Grafikkarte selbst
                            var key = await Keyframe(s, x, prompt, dir, ct); // Storyboard-Bild: Figur am Drehort
                            using var gpu2 = await GpuSlot("video", $"Einstellung {x.Index + 1}", ct);
                            made = await Tool(sdcli, MusicVideoRender.SceneArgs(models, x, prompt, key ?? (MusicVideoRender.ExpectedPeople(x, sc.Figures) > 0 ? figImg : null), raw, preview ? MusicVideoRender.Preview : MusicVideoRender.Final, x.Seed), ct);
                        }
                        if (!made || !File.Exists(raw)) { tries.Add(new(false, [new("render", "Die Video-KI hat keinen Clip geliefert.", 0)])); continue; }
                    }
                    // D2/D3: genaue Länge, Format, Filmlook (Farbe, leichtes Korn, Breitbild-Balken nur bei 16:9)
                    clip = Path.Combine(dir, $"einstellung{x.Index:000}.mp4");
                    var filter = MusicVideo.Filter(w, h, lut, _mvBars && _mvFmt == "16:9" && !preview, preview ? 0 : 4) + $",tpad=stop_mode=clone:stop_duration=1";
                    if (!await Tool(ff, MusicVideo.ClipArgs(raw, 0, x.End - x.Start, filter, clip), ct) || !File.Exists(clip)) { tries.Add(new(false, [new("format", "Der Clip ließ sich nicht aufbereiten.", 0)])); continue; }
                    TryDelete(raw);
                    var chk = CheckReady ? await CheckClip(clip, x, sc, figImg, vclip, attempt) : null;
                    if (chk is null) { tries.Add(new(true, [])); ok = true; break; } // ohne Prüfprogramm: ehrlich als „nicht geprüft“ vermerkt (siehe unten)
                    tries.Add(chk);
                    if (chk.Ok) { ok = true; break; }
                    (x, prompt) = MusicVideoRender.Retry(x, prompt, chk, attempt);
                }
                var report = CheckReady ? MusicVideoRender.Report(tries) : ok ? "erzeugt, nicht geprüft (Artefakt-Prüfung in der KI-Bibliothek einrichten)" : MusicVideoRender.Report(tries);
                done[shot.Index] = new ShotClip(shot.Index, clip ?? "", ok, report, q);
                SaveClips(s, q, done.Values.Where(c => File.Exists(c.File)).ToList());
                if (ok) okCount++;
                _core.Audit.Append(Principal.LocalOwner.Name, "musik.video.einstellung", $"{s.Id} #{shot.Index} {(ok ? "ok" : "rot")}: {report}");
            }
            var red = done.Values.Count(c => !c.Ok);
            Say($"✓ {(preview ? "Vorschau" : "Endfassung")}: {done.Values.Count(c => c.Ok)} von {sc.Shots.Count} Einstellungen fertig und geprüft" + (red > 0 ? $", {red} rot – bitte ansehen: neu würfeln, ändern oder ersetzen." : ".") + " „Im Videoschnitt zusammensetzen“ legt alles auf den Takt.");
        }
        catch (OperationCanceledException) { Say("Abgebrochen – fertige Einstellungen bleiben, beim nächsten Start geht es dort weiter."); }
        catch (Exception e) when (e is InvalidOperationException or IOException) { Say("Musikvideo ging nicht: " + Trendo.Core.FriendlyError.Say(e)); }
        finally { _mvKiCts = null; cts.Dispose(); if (CurrentView == MS) Show(MS); }
        return okCount;
    }

    public void MsRenderCancel() => _mvKiCts?.Cancel();

    /// <summary>D1: fertige Einstellungen in Reihenfolge auf die Bildspur des verknüpften Projekts legen (Ton vom Lied, Marken aus dem Drehbuch).
    /// Rote oder fehlende Einstellungen werden durch die vorige ersetzt verlängert – und so gemeldet.</summary>
    public int MsAssembleVideo(string songId, bool preview)
    {
        if (Songs.Get(songId) is not { } s || Script(s) is not { } sc) return 0;
        var clips = ShotClips(s, preview ? "vorschau" : "final").ToDictionary(c => c.Index);
        if (clips.Count == 0) { _msMsg = "Noch keine fertigen Einstellungen."; Show(MS); return 0; }
        MsToVideo(s);
        var d = ProgramActive("schnitt"); if (d is null) return 0;
        var (w, h) = MvSize(preview);
        d.Edit("Musikvideo zusammensetzen", p =>
        {
            VideoEdit.Store(p, []);
            p["width"] = w; p["height"] = h;
            foreach (var x in sc.Shots)
            {
                var c = clips.GetValueOrDefault(x.Index);
                if (c is null || !c.Ok)
                {
                    var l = VideoEdit.Clips(p);
                    if (l.Count > 0) { var last = l[^1]; l[^1] = last with { Out = last.Out + (x.End - x.Start), SourceLength = Math.Max(last.SourceLength, last.Out + (x.End - x.Start)) }; VideoEdit.Store(p, l); }
                    continue;
                }
                VideoEdit.Add(p, VideoEdit.NewClip(c.File, x.End - x.Start, false));
            }
        });
        var missing = sc.Shots.Count(x => clips.GetValueOrDefault(x.Index) is not { Ok: true });
        _progMsg["schnitt"] = $"Musikvideo zusammengesetzt: {sc.Shots.Count - missing} Einstellungen auf dem Takt" + (missing > 0 ? $", {missing} fehlen oder sind rot – dort läuft die vorige weiter." : ".") + " Jetzt „Video exportieren“.";
        Show("schnitt");
        return sc.Shots.Count - missing;
    }

    Control MsVideoKiPanel(MusicSong s)
    {
        var sp = new StackPanel { Name = "ms-videoki-panel", Spacing = 6 };
        sp.Children.Add(H("Echtes Musikvideo (KI-Filmszenen)", 13));
        var sc = Script(s);
        if (sc is null) { sp.Children.Add(P("Zuerst „Lied verstehen“ und „Drehbuch erstellen“.", Muted, 11.5)); return sp; }
        if (!ScenesReady)
        {
            sp.Children.Add(P("Dafür braucht Trendo die freien Video-KIs: „Wan 2.2 – Filmszenen“ (ca. 11,7 GB) und für singende Figuren zusätzlich „Wan 2.2 S2V“ (ca. 13,7 GB), dazu die „Artefakt-Prüfung“ (0,4 GB). Alles kostenlos, läuft auf deinem PC.", Muted, 11.5));
            sp.Children.Add(Nav2("KI-Bibliothek öffnen", "ki-bibliothek"));
            return sp;
        }
        var fmt = new WrapPanel();
        foreach (var f in new[] { "16:9", "9:16" })
        {
            var on = _mvFmt == f; var ff = f;
            var b = new Button { Name = "ms-vk-fmt-" + f.Replace(':', 'x'), Content = f == "16:9" ? "16:9 YouTube" : "9:16 TikTok/Reels", FontSize = 11.5, Padding = new Thickness(9, 3), Margin = new Thickness(0, 0, 4, 4),
                Background = on ? Brush.Parse("#33D4A84B") : Brush.Parse("#10FFFFFF"), BorderBrush = Brush.Parse(on ? "#D4A84B" : "#22FFFFFF"), BorderThickness = new Thickness(1) };
            b.Click += (_, _) => { _mvFmt = ff; Show(MS); };
            fmt.Children.Add(b);
        }
        sp.Children.Add(fmt);
        if (!SingReady) sp.Children.Add(P("Hinweis: Ohne „Wan 2.2 S2V“ werden Gesangs-Einstellungen als Szene ohne lippensynchrones Singen erzeugt.", Brush.Parse("#FACC15"), 11));
        if (!CheckReady) sp.Children.Add(P("Hinweis: Ohne „Artefakt-Prüfung“ prüft Trendo die Einstellungen nicht auf Fehlbildungen.", Brush.Parse("#FACC15"), 11));
        var prev = ShotClips(s, "vorschau"); var fin = ShotClips(s, "final");
        if (_mvKiCts is not null)
        {
            sp.Children.Add(new TextBlock { Name = "ms-vk-status", Text = _mvKiStatus, Foreground = Text, FontSize = 12, TextWrapping = TextWrapping.Wrap });
            sp.Children.Add(Act("ms-vk-cancel", "Abbrechen", MsRenderCancel));
            return sp;
        }
        var finBtn = Act("ms-vk-final", fin.Count == 0 ? "2. Endfassung rendern" : $"Endfassung ({fin.Count(c => c.Ok)}/{sc.Shots.Count}) weiter", () => _ = MsRenderVideo(s.Id, false), prev.Count > 0 && fin.Count == 0);
        finBtn.IsEnabled = prev.Count > 0; // F1: erst die Vorschau ansehen
        if (!finBtn.IsEnabled) ToolTip.SetTip(finBtn, "Zuerst die schnelle Vorschau rendern und ansehen.");
        sp.Children.Add(new WrapPanel { Children = {
            Act("ms-vk-preview", prev.Count == 0 ? "1. Schnelle Vorschau rendern" : $"Vorschau ({prev.Count(c => c.Ok)}/{sc.Shots.Count}) weiter", () => _ = MsRenderVideo(s.Id, true), prev.Count == 0), finBtn } });
        if (prev.Count > 0 || fin.Count > 0)
            sp.Children.Add(Act("ms-vk-assemble", "Im Videoschnitt zusammensetzen", () => MsAssembleVideo(s.Id, fin.Count == 0)));
        var show = fin.Count > 0 ? fin : prev;
        if (show.Count > 0)
        {
            var list = new StackPanel { Name = "ms-vk-report", Spacing = 1 };
            foreach (var c in show.OrderBy(c => c.Index).Take(80))
            {
                var ci = c.Index;
                list.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("26,*,Auto"), Children = {
                    new TextBlock { Text = (ci + 1).ToString(), Foreground = Muted, FontSize = 11 },
                    Pos(new TextBlock { Text = c.Report, Foreground = c.Ok ? Brush.Parse("#4ADE80") : Brush.Parse("#F87171"), FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis }, 1),
                    Pos(Act($"ms-vk-redo-{ci}", "↻", () => { MsShotReroll(s.Id, ci); _ = MsRenderVideo(s.Id, fin.Count == 0, ci); }), 2) } });
            }
            sp.Children.Add(new Border { Background = Brush.Parse("#0FFFFFFF"), CornerRadius = new CornerRadius(10), Padding = new Thickness(6), MaxHeight = 180, Child = new ScrollViewer { Content = list } });
        }
        return sp;
    }
}
