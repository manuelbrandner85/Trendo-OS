using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Trendo.Core;
using Trendo.Core.Media;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>CORE-08 (Masterprompt 131): „Videoschnitt“ als Programm – Medien auf Bild- und Musikspur legen, Stücke umordnen, an den Kanten kürzen,
/// am Abspielkopf teilen, Musik mit Startzeit und Lautstärke; Vorschau des Bildes am Abspielkopf; Export als MP4 über FFmpeg.
/// Projektdatei, Rückgängig, Autosave und Bedienstufen kommen aus der Programm-Hülle.</summary>
public partial class MainWindow
{
    const string VK = "schnitt";
    private readonly Dictionary<string, IReadOnlyList<Bitmap>> _thumbs = [];
    private readonly Dictionary<string, Bitmap> _veFrames = [];
    public (string Track, int Index)? EditSelected { get; set; }
    /// <summary>Rechte Seite in Registerkarten (Wunsch 04.11.2026 „viel übersichtlicher“): medien, auswahl, export.</summary>
    public string EditTab { get; set; } = "medien";
    (string Track, int Index)? _veSeenSel;
    public EditTimeline? EditTimelineForTests { get; private set; }
    public double EditZoom { get; set; } = 40;

    private void InitVideoSchnitt() => RegisterProgram(KreativTabs, new ProgramSpec(VK, "CORE-08", "Videoschnitt")
    {
        Sentence = "Videos, Fotos und Musik auf die Zeitleiste legen, schneiden und als MP4 speichern.",
        FillHeight = true,
        Folder = "Medien",
        Templates =
        [
            new("leer", "Neues Video (16:9, HD)", "1280 × 720, 30 Bilder je Sekunde", () => VideoEdit.NewProject()),
            new("fullhd", "Full HD (1920 × 1080)", "Für große Bildschirme und YouTube", () => VideoEdit.NewProject(1920, 1080)),
            new("hoch", "Hochformat (1080 × 1920)", "Für Handy, Reels und Stories", () => VideoEdit.NewProject(1080, 1920)),
        ],
        Ribbon =
        [
            ("Schneiden", [
                new ProgramCmd("ve-split", "Teilen (S)", d => d.Edit("Teilen", x => { if (!VideoEdit.Split(x, d.Number("playhead"))) throw new InvalidOperationException("Am Abspielkopf liegt kein Stück – oder er steht zu nah an einer Schnittkante."); }), true, "Stück am Abspielkopf in zwei teilen"),
                new ProgramCmd("ve-delete", "Löschen (Entf)", d => EditDelete(d), false, "Ausgewähltes Stück entfernen"),
                new ProgramCmd("ve-play", "▶ Abspielen (Leertaste)", d => EditTogglePlay(d), false, "Im Programm abspielen (Bild, ohne Ton) – noch einmal: anhalten"),
                new ProgramCmd("ve-silence", "✨ Stille finden", d => EditFindSilences(d), false, "Stille Stellen (mind. 1 s) in Videos mit Ton suchen – Trendo schlägt vor, sie herauszuschneiden", Level: 1),
                new ProgramCmd("ve-left", "◀ Nach vorn", d => EditShift(d, -1), false, "Ausgewähltes Stück eine Stelle nach vorn"),
                new ProgramCmd("ve-right", "Nach hinten ▶", d => EditShift(d, 1), false, "Ausgewähltes Stück eine Stelle nach hinten"),
            ]),
            ("Ausgabe", [
                new ProgramCmd("ve-export-bg", "⬇ Video exportieren", d => _ = EditExportBackground(d), true, "Als MP4 im Hintergrund – mit Fortschritt und Abbrechen; du kannst weiterarbeiten"),
                new ProgramCmd("ve-deliver", "📦 Film ausliefern", d => _ = FilmDeliver(d), false, "Wie im Profi-Schnitt: Film, Ton-Stems (Dialog, Musik), Schnittliste (EDL, OTIO), Cue-Liste und Prüfbericht mit Prüfsummen in einem Ordner"),
            ]),
            ("Ansicht", [
                new ProgramCmd("ve-zoom-in", "＋ Zoom", _ => EditZoom = Math.Min(200, EditZoom * 1.5)),
                new ProgramCmd("ve-zoom-out", "－ Zoom", _ => EditZoom = Math.Max(4, EditZoom / 1.5)),
                new ProgramCmd("ve-start", "⏮ Anfang", d => d.Set("playhead", 0, "Abspielkopf")),
            ]),
        ],
        GroupLevels = new() { ["Ansicht"] = 1 },
        Exports = [new("mp4", "Video (MP4) sofort", ".mp4", (d, f) => EditExport(d, f))],
        Workspace = EditWorkspace,
        Properties = EditProperties,
        Status = d => { var p = d.Data; return $"{VideoEdit.Clips(p).Count} Stücke · Musik {VideoEdit.Clips(p, "music").Count} · Länge {Clock(VideoEdit.Duration(p))} · {(int)d.Number("width")} × {(int)d.Number("height")}" + (FfmpegPath is null ? " · FFmpeg fehlt (Vorschau und Export)" : ""); },
        Help = "Rechts Medien hinzufügen. Unten auf der Zeitleiste: Stück ziehen ordnet um, Kanten ziehen kürzt, Klick ins Lineal setzt den Abspielkopf, S teilt, Entf löscht. Export unter „Exportieren“.",
    }, "videoschnitt", "video schneiden", "zeitleiste", "timeline", "film", "clip", "musik unterlegen", "mp4", "schnitt");

    // ---------- Bearbeiten (öffentlich für Tests und die KI) ----------
    /// <summary>Datei auf die Bild- oder Musikspur legen; Länge und Ton werden mit FFmpeg ermittelt (oder angegeben).</summary>
    public void EditAdd(string file, bool music = false, double? length = null, bool? audio = null)
    {
        var d = ProgramActive(VK) ?? ProgramNew(VK);
        if (!File.Exists(file)) throw new FileNotFoundException("Datei nicht gefunden: " + Path.GetFileName(file));
        if (!MediaStudio.Inputs.Contains(Path.GetExtension(file).ToLowerInvariant())) throw new InvalidOperationException("Diese Datei ist kein Video, Bild oder Ton.");
        if (music && (MediaStudio.IsImage(file))) throw new InvalidOperationException("Auf die Musikspur gehören Ton- oder Videodateien.");
        double len; bool hasAudio;
        if (MediaStudio.IsImage(file)) { len = VideoEdit.ImageSeconds; hasAudio = false; }
        else
        {
            var ff = FfmpegPath;
            if (length is null && ff is null) throw new InvalidOperationException("Für Videos und Ton fehlt FFmpeg – unter Zusatzprogramme einrichten.");
            len = length ?? MediaStudio.Duration(ff!, file) ?? throw new InvalidOperationException("Die Länge der Datei lässt sich nicht lesen.");
            hasAudio = audio ?? (ff is not null && VideoEdit.HasAudioStream(ff, file));
        }
        var clip = VideoEdit.NewClip(file, len, hasAudio);
        d.Edit(music ? "Musik hinzufügen" : "Hinzufügen", x => VideoEdit.Add(x, clip, music ? "music" : "clips"));
        EditSelected = (music ? "music" : "clips", VideoEdit.Clips(d.Data, music ? "music" : "clips").Count - 1);
        _progMsg[VK] = $"„{Path.GetFileName(file)}“ liegt auf der {(music ? "Musikspur" : "Bildspur")}.";
    }

    void EditDelete(ProgramDocument d)
    {
        if (EditSelected is not { } s) throw new InvalidOperationException("Erst ein Stück auf der Zeitleiste anklicken.");
        d.Edit("Löschen", x => VideoEdit.Remove(x, s.Index, s.Track)); EditSelected = null;
    }
    void EditShift(ProgramDocument d, int by)
    {
        if (EditSelected is not ("clips", var i)) throw new InvalidOperationException("Erst ein Stück auf der Bildspur anklicken.");
        var to = Math.Clamp(i + by, 0, VideoEdit.Clips(d.Data).Count - 1); if (to == i) return;
        d.Edit("Verschieben", x => VideoEdit.Move(x, i, to)); EditSelected = ("clips", to);
    }

    string EditExport(ProgramDocument d, string file)
    {
        var ff = FfmpegPath ?? throw new InvalidOperationException("Für den Export fehlt FFmpeg – unter Zusatzprogramme einrichten.");
        if (_opt.Isolated && ExportOverride is null) throw new InvalidOperationException("Im abgeschotteten Testmodus startet Trendo kein FFmpeg.");
        var o = ExportOverride?.Invoke(d.Data, file) ?? VideoEdit.Export(ff, d.Data, file);
        _core.Audit.Append(Principal.LocalOwner.Name, "video.export", Path.GetFileName(o));
        return o;
    }
    /// <summary>Für Tests: Ersatz für den FFmpeg-Export.</summary>
    public Func<JsonObject, string, string>? ExportOverride { get; set; }

    IReadOnlyList<Bitmap> EditThumbs(EditClip c)
    {
        var key = c.Id + "|" + c.In + "|" + c.Out;
        if (_thumbs.TryGetValue(key, out var l)) return l;
        var list = new List<Bitmap>();
        try
        {
            if (c.IsImage) { using var s = File.OpenRead(c.File); list.Add(Bitmap.DecodeToWidth(s, 160)); }
            else if (FfmpegPath is { } ff && !_opt.Isolated && !MediaStudio.IsAudio(c.File))
            {
                var dir = Path.Combine(MediaFolder, ".vorschau", Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(System.Text.Encoding.UTF8.GetBytes(c.File + File.GetLastWriteTimeUtc(c.File).Ticks + c.In + c.Out)))[..12]);
                Directory.CreateDirectory(dir);
                for (var i = 0; i < 4; i++)
                {
                    var png = Path.Combine(dir, $"b{i}.png");
                    if (!File.Exists(png)) Cli.Run(ff, MediaStudio.FrameArgs(c.File, c.In + c.Length * (i + 0.5) / 4, png), dir, 30000);
                    if (File.Exists(png)) { using var s = File.OpenRead(png); list.Add(new Bitmap(s)); }
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException) { }
        return _thumbs[key] = list;
    }

    /// <summary>Bild am Abspielkopf (Standbild direkt, Video über FFmpeg, zwischengespeichert).</summary>
    Bitmap? EditFrame(ProgramDocument d)
    {
        if (VideoEdit.At(d.Data, d.Number("playhead")) is not { } at) return null;
        var c = VideoEdit.Clips(d.Data)[at.Index];
        try
        {
            if (c.IsImage) { using var s = File.OpenRead(c.File); return new Bitmap(s); }
            if (FfmpegPath is not { } ff || _opt.Isolated) return null;
            var src = VideoProxy.ForPreview(d.Data, c.File); // Paket 01 S3-02: Proxy für die Vorschau
            var t = Math.Round(at.SourceTime, 1); var key = src + "|" + t;
            if (_veFrames.TryGetValue(key, out var b)) return b;
            var dir = Path.Combine(MediaFolder, ".vorschau", "kopf"); Directory.CreateDirectory(dir);
            var png = Path.Combine(dir, Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(System.Text.Encoding.UTF8.GetBytes(key)))[..16] + ".png");
            if (!File.Exists(png)) Cli.Run(ff, ["-hide_banner", "-loglevel", "error", "-y", "-ss", t.ToString(System.Globalization.CultureInfo.InvariantCulture), "-i", src, "-frames:v", "1", "-vf", "scale=640:-2", png], dir, 30000);
            if (!File.Exists(png)) return null;
            using var fs = File.OpenRead(png); if (_veFrames.Count > 60) _veFrames.Clear(); return _veFrames[key] = new Bitmap(fs);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException) { return null; }
    }

    private Control EditWorkspace(ProgramDocument d)
    {
        var p = d.Data; var clips = VideoEdit.Clips(p); var music = VideoEdit.Clips(p, "music");
        if (EditSelected is { } s && s.Index >= (s.Track == "music" ? music.Count : clips.Count)) EditSelected = null;
        // Vorschau im Seitenverhältnis des Projekts
        var (pw, ph) = (Math.Max(16, d.Number("width")), Math.Max(16, d.Number("height")));
        var frame = EditFrame(d);
        Control screen = frame is not null ? new Image { Name = "ve-preview", Source = frame, Stretch = Stretch.Uniform }
            : new TextBlock { Name = "ve-preview-empty", Text = clips.Count == 0 ? "Rechts Videos, Fotos oder Musik hinzufügen." : FfmpegPath is null ? "Vorschau von Videos braucht FFmpeg." : "Abspielkopf ins Video setzen.", Foreground = Muted, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center };
        var titleAt = VideoEdit.At(p, d.Number("playhead")) is { } at0 ? clips[at0.Index].Title : "";
        var overlay = new TextBlock { Name = "ve-title-overlay", Text = titleAt, Foreground = Brushes.White, FontSize = ph / 14, FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, ph / 10), IsVisible = titleAt.Length > 0,
            Effect = new DropShadowEffect { BlurRadius = 6, OffsetX = 0, OffsetY = 2, Color = Colors.Black, Opacity = 0.9 } };
        if (screen is Image im0) _vePreview = im0;
        var frameBox = new Grid { Width = pw, Height = ph, Background = Brushes.Black, Children = { new Viewbox { Stretch = Stretch.Uniform, Child = screen is Image ? screen : new Border { Width = 640, Height = 360, Child = screen } }, overlay } };
        _veOverlay = overlay;
        var view = new Viewbox { Stretch = Stretch.Uniform, Child = frameBox };
        if (screen is not Image) { var img = new Image { Name = "ve-play-image", Stretch = Stretch.Uniform, IsVisible = false }; frameBox.Children.Insert(1, img); _vePreview = img; }
        var time = new TextBlock { Name = "ve-time", Text = $"{ClockMs(d.Number("playhead"))} / {ClockMs(VideoEdit.Duration(p))}", Foreground = Text, FontSize = 13, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0) };
        // Abspielleiste unter der Vorschau wie in jedem Schnittprogramm
        Button Tp(string name, string label, string tip, Action a, bool main = false)
        {
            var b = new Button { Name = name, Content = label, FontSize = main ? 16 : 13, Width = main ? 44 : 36, Height = main ? 40 : 32, Padding = new Thickness(0), CornerRadius = new CornerRadius(main ? 20 : 8),
                HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center, Background = main ? Accent : Brush.Parse("#18FFFFFF"), Foreground = main ? Brushes.White : Text, BorderThickness = new Thickness(0), Margin = new Thickness(2, 0) };
            ToolTip.SetTip(b, tip); b.Click += (_, _) => ProgramRun(VK, a); return b;
        }
        void Seek(double t) => d.Set("playhead", Math.Clamp(Math.Round(t, 3), 0, VideoEdit.Duration(d.Data)), "Abspielkopf");
        var transport = new DockPanel { Margin = new Thickness(0, 6), LastChildFill = false };
        var tpMid = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Children = {
            Tp("ve-tp-start", "⏮", "Zum Anfang", () => Seek(0)), Tp("ve-tp-back", "⏪", "1 Sekunde zurück (Umschalt+←)", () => Seek(d.Number("playhead") - 1)),
            Tp("ve-tp-play", _vePlay is not null ? "⏸" : "▶", "Abspielen / Anhalten (Leertaste)", () => EditTogglePlay(d), true),
            Tp("ve-tp-fwd", "⏩", "1 Sekunde vor (Umschalt+→)", () => Seek(d.Number("playhead") + 1)), Tp("ve-tp-end", "⏭", "Zum Ende", () => Seek(VideoEdit.Duration(d.Data))), time } };
        var zoom = new StackPanel { Orientation = Orientation.Horizontal, Children = {
            new TextBlock { Text = "Zoom", Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) },
            Tp("ve-tp-zoom-out", "－", "Zeitleiste verkleinern", () => EditZoom = Math.Max(4, EditZoom / 1.5)), Tp("ve-tp-zoom-in", "＋", "Zeitleiste vergrößern", () => EditZoom = Math.Min(200, EditZoom * 1.5)) } };
        DockPanel.SetDock(zoom, Avalonia.Controls.Dock.Right); transport.Children.Add(zoom);
        transport.Children.Add(new Border { Width = 1 }); // Platzhalter links
        var tpWrap = new Grid { Children = { transport, tpMid } };
        var tl = new EditTimeline { Name = "ve-timeline", Clips = clips, Music = music, PxPerSec = EditZoom, Playhead = d.Number("playhead"), Selected = EditSelected, Markers = VideoEdit.Markers(p),
            Thumbs = clips.ToDictionary(c => c.Id, EditThumbs) };
        EditTimelineForTests = tl;
        tl.PlayheadChanged = t => ProgramRun(VK, () => d.Set("playhead", Math.Min(t, VideoEdit.Duration(d.Data)), "Abspielkopf"));
        tl.ClipSelected = (track, i) => { EditSelected = (track, i); EditTab = "auswahl"; Show(VK); };
        tl.ClipMoved = (a, b) => ProgramRun(VK, () => { d.Edit("Umordnen", x => VideoEdit.Move(x, a, b)); EditSelected = ("clips", b); });
        tl.MusicMoved = (i, start) => ProgramRun(VK, () => { var m = VideoEdit.Clips(d.Data, "music")[i]; d.Edit("Musik verschieben", x => VideoEdit.SetMusic(x, i, start, m.Volume)); EditSelected = ("music", i); });
        tl.ClipTrimmed = (track, i, a, b) => ProgramRun(VK, () =>
        {
            var before = VideoEdit.Clips(d.Data, track)[i];
            d.Edit("Kürzen", x =>
            {
                VideoEdit.Trim(x, i, a, b, track);
                if (track == "music") { var after = VideoEdit.Clips(x, "music")[i]; VideoEdit.SetMusic(x, i, before.Start + (after.In - before.In), after.Volume); } // Ende bleibt stehen
            });
            EditSelected = (track, i);
        });
        tl.KeyDown += (_, e) =>
        {
            var spec = Programs[VK];
            ProgramCmd? Cmd(string n) => spec.Ribbon.SelectMany(g => g.Cmds).First(c => c.Name == n);
            switch (e.Key)
            {
                case Key.S when e.KeyModifiers == KeyModifiers.None: ProgramRun(VK, () => Cmd("ve-split")!.Run(d)); e.Handled = true; break;
                case Key.Delete: ProgramRun(VK, () => EditDelete(d)); e.Handled = true; break;
                case Key.Space: EditTogglePlay(d); e.Handled = true; break;
                case Key.Left: ProgramRun(VK, () => d.Set("playhead", Math.Max(0, Math.Round(d.Number("playhead") - (e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 1 : 1.0 / 30), 3)), "Abspielkopf")); e.Handled = true; break;
                case Key.Right: ProgramRun(VK, () => d.Set("playhead", Math.Min(VideoEdit.Duration(d.Data), Math.Round(d.Number("playhead") + (e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 1 : 1.0 / 30), 3)), "Abspielkopf")); e.Handled = true; break;
                case Key.J or Key.K or Key.L or Key.I or Key.O or Key.M or Key.Home or Key.End when e.KeyModifiers == KeyModifiers.None: EditKey(d, e.Key); e.Handled = true; break; // X5: wie in Profi-Schnittprogrammen
            }
        };
        var scroll = new ScrollViewer { Content = tl, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        // Spurnamen links neben der Zeitleiste
        var labels = new Canvas { Width = 92, Height = tl.Height, Background = Brush.Parse("#0E131C") };
        void Lab(string text, double top, double h) { var t = new Border { Width = 92, Height = h, Child = new TextBlock { Text = text, Foreground = Muted, FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0) } }; Canvas.SetTop(t, top); labels.Children.Add(t); }
        Lab("Zeit", 0, EditTimeline.Ruler); Lab("🎬 Bild", EditTimeline.VideoTop, EditTimeline.VideoH); Lab("♪ Musik", EditTimeline.MusicTop, EditTimeline.MusicH);
        labels.Name = "ve-track-labels";
        var tlBox = new Border { CornerRadius = new CornerRadius(10), ClipToBounds = true, BorderBrush = Edge, BorderThickness = new Thickness(1),
            Child = new DockPanel { Children = { Dk(labels, Avalonia.Controls.Dock.Left), scroll } } };
        var hint = new TextBlock { Text = "Stück ziehen: umordnen · Kanten ziehen: kürzen · Lineal: Abspielkopf · S teilen · Entf löschen · ←/→ Bild für Bild (Umschalt: Sekunde)", Foreground = Muted, FontSize = 11.5, Margin = new Thickness(2, 4, 0, 0), TextWrapping = TextWrapping.Wrap };
        var g = new Grid { RowDefinitions = new RowDefinitions("*,Auto,Auto,Auto") };
        g.Children.Add(new Border { Background = Brush.Parse("#05070C"), CornerRadius = new CornerRadius(10), Padding = new Thickness(6), Child = view, MinHeight = 90 });
        _veTime = time;
        Grid.SetRow(tpWrap, 1); g.Children.Add(tpWrap); Grid.SetRow(tlBox, 2); g.Children.Add(tlBox); Grid.SetRow(hint, 3); g.Children.Add(hint);
        return g;
    }

    static string ClockMs(double s) => TimeSpan.FromSeconds(Math.Max(0, s)).ToString(s >= 3600 ? @"h\:mm\:ss\.f" : @"m\:ss\.f");

    private Control EditProperties(ProgramDocument d)
    {
        var outer = new StackPanel { Spacing = 8 };
        if (SuggestionCard(VK, EditSuggestions(d)) is { } sugg) outer.Children.Add(sugg);
        // Registerkarten: Medien · Auswahl · Export – alles bleibt da, nur geordnet
        if (EditSelected != _veSeenSel) { _veSeenSel = EditSelected; if (EditSelected is not null) EditTab = "auswahl"; } // neue Auswahl: gleich ihre Einstellungen zeigen
        var tabs = new (string Id, string Label)[] { ("medien", "Medien"), ("auswahl", EditSelected is { } es ? (es.Track == "music" ? "Auswahl: Musik" : "Auswahl: Stück") : "Auswahl"), ("export", _veExport is not null ? "Export ⏳" : "Export") };
        if (!tabs.Any(t => t.Id == EditTab)) EditTab = "medien";
        var tabBar = new WrapPanel { Name = "ve-tabs" };
        var bodies = new Dictionary<string, StackPanel>();
        foreach (var (id, label) in tabs)
        {
            var on = id == EditTab; var tid = id;
            var b = new Button { Name = "ve-tab-" + id, Content = label, Padding = new Thickness(12, 5), Margin = new Thickness(0, 0, 6, 6), FontSize = 12.5, CornerRadius = new CornerRadius(10),
                Background = on ? Brush.Parse("#33D4A84B") : Brush.Parse("#10FFFFFF"), BorderBrush = Brush.Parse(on ? "#D4A84B" : "#22FFFFFF"), BorderThickness = new Thickness(1) };
            b.Click += (_, _) => { EditTab = tid; Show(VK); };
            tabBar.Children.Add(b);
            bodies[id] = new StackPanel { Name = "ve-tab-body-" + id, Spacing = 8, IsVisible = on };
        }
        outer.Children.Add(tabBar);
        foreach (var t in tabs) outer.Children.Add(bodies[t.Id]);
        var sp = bodies["medien"];
        sp.Children.Add(EditProxyPanel(d)); // Paket 01 S3-02: Proxy-Dateien
        // Medien hinzufügen
        var path = new TextBox { Name = "ve-file", PlaceholderText = "Video-, Bild- oder Tondatei", HorizontalAlignment = HorizontalAlignment.Stretch };
        Button B(string name, string label, Action a, bool primary = false) { var b = new Button { Name = name, Content = label, Margin = new Thickness(0, 0, 4, 4), FontSize = 12 }; if (primary) { b.Background = Accent; b.Foreground = Brushes.White; } b.Click += (_, _) => ProgramRun(VK, a); return b; }
        string Need() => (path.Text ?? "").Trim().Trim('"') is { Length: > 0 } f ? f : throw new InvalidOperationException("Bitte eine Datei wählen.");
        sp.Children.Add(H("Medien", 13));
        sp.Children.Add(path);
        sp.Children.Add(new WrapPanel { Children = { B("ve-add", "Auf die Bildspur", () => EditAdd(Need()), true), B("ve-add-music", "Als Musik", () => EditAdd(Need(), music: true)) } });
        var recent = FileShelf.Recent([MediaFolder, UserFolder(Environment.SpecialFolder.MyVideos), UserFolder(Environment.SpecialFolder.MyPictures), UserFolder(Environment.SpecialFolder.MyMusic)], MediaStudio.Inputs).Take(6).ToList();
        if (recent.Count > 0)
        {
            sp.Children.Add(P("Zuletzt im Medienordner:", null, 11.5));
            var k = 0;
            foreach (var f in recent) { var fp = f.FullName; var b = B($"ve-recent-{k++}", (MediaStudio.IsImage(fp) ? "🖼 " : MediaStudio.IsAudio(fp) ? "♪ " : "🎬 ") + f.Name, () => EditAdd(fp, music: MediaStudio.IsAudio(fp))); b.HorizontalAlignment = HorizontalAlignment.Stretch; b.HorizontalContentAlignment = HorizontalAlignment.Left; ToolTip.SetTip(b, fp); sp.Children.Add(b); }
        }
        sp.Children.Add(new WrapPanel { Children = { B("ve-add-overlay", "Auf die zweite Bildspur (Bild im Bild, Logo)", () => EditAddOverlay(d, Need())) } });
        sp.Children.Add(EditOverlayPanel(d));
        sp.Children.Add(P("Tipp: Unten in der Zeitleiste ein Stück anklicken – seine Einstellungen stehen dann unter „Auswahl“.", Muted, 11.5));
        if (VideoEdit.Song(d.Data) is { } linked && Songs.Get(linked.Id) is { } ls)
        {
            var mk = VideoEdit.Markers(d.Data);
            sp.Children.Add(new Border { Name = "ve-song-link", Background = Brush.Parse("#22C9A227"), CornerRadius = new CornerRadius(10), Padding = new Thickness(10, 8), Child = new StackPanel { Spacing = 4, Children = {
                new TextBlock { Text = $"♪ Verknüpft mit „{ls.Title}“ aus Trendo Musik", Foreground = Text, FontSize = 12, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap },
                P($"{mk.Count(m => m.Kind == "section")} Abschnitte · {mk.Count(m => m.Kind == "line")} Liedzeilen (rosa Sängerin, blau Sänger, gold beide) · {mk.Count(m => m.Kind == "shot")} Einstellungen aus dem Drehbuch", Muted, 11),
                B("ve-song-open", "Zum Lied in Trendo Musik", () => { _msSel = ls.Id; _msDetailTab = "video"; Show(MS); }) } } });
        }
        // Ausgewähltes Stück
        sp = bodies["auswahl"];
        if (EditSelected is null) sp.Children.Add(P("Noch nichts ausgewählt. Klicke unten in der Zeitleiste auf ein Stück oder die Musik – dann kannst du hier kürzen, überblenden, Text einblenden oder die Lautstärke ändern.", Muted, 12));
        if (EditSelected is { } s)
        {
            var list = VideoEdit.Clips(d.Data, s.Track);
            if (s.Index < list.Count)
            {
                var c = list[s.Index];
                var inBox = new TextBox { Name = "ve-in", Text = c.In.ToString("0.##", De), Width = 70 }; var outBox = new TextBox { Name = "ve-out", Text = c.Out.ToString("0.##", De), Width = 70 };
                sp.Children.Add(H(s.Track == "music" ? "Musik" : "Stück", 13));
                sp.Children.Add(P($"{Path.GetFileName(c.File)} · {c.Length:0.0} s" + (c.IsImage ? " (Standbild)" : $" von {c.SourceLength:0.0} s"), Text, 12));
                sp.Children.Add(new WrapPanel { Children = { new TextBlock { Text = "von", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) }, inBox, new TextBlock { Text = " bis ", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center }, outBox, new TextBlock { Text = " s", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center } } });
                double Num(TextBox t) => double.TryParse((t.Text ?? "").Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : throw new InvalidOperationException("Bitte Sekunden als Zahl eingeben.");
                var row = new WrapPanel { Children = { B("ve-trim-apply", "Übernehmen", () => d.Edit("Kürzen", x => VideoEdit.Trim(x, s.Index, Num(inBox), Num(outBox), s.Track)), true) } };
                sp.Children.Add(row);
                if (s.Track == "clips")
                {
                    var fade = new TextBox { Name = "ve-fade", Text = c.Fade.ToString("0.#", De), Width = 60 };
                    var title = new TextBox { Name = "ve-title", Text = c.Title, PlaceholderText = "Text, der eingeblendet wird", HorizontalAlignment = HorizontalAlignment.Stretch };
                    sp.Children.Add(new WrapPanel { Children = { new TextBlock { Text = "Überblenden ins nächste ", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center }, fade, new TextBlock { Text = " s", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center } } });
                    sp.Children.Add(title);
                    sp.Children.Add(B("ve-clip-apply", "Überblenden und Text übernehmen", () => d.Edit("Überblenden/Text", x => VideoEdit.SetClip(x, s.Index, Num(fade), title.Text ?? ""))));
                    sp.Children.Add(EditClipPro(d, s.Index, c)); // Profi 23: Farbe, Ton, Übergang, Titellage
                }
                if (s.Track == "music")
                {
                    var start = new TextBox { Name = "ve-music-start", Text = c.Start.ToString("0.##", De), Width = 70 };
                    var vol = new Slider { Name = "ve-music-vol", Minimum = 0, Maximum = 2, Value = c.Volume, Width = 180 };
                    sp.Children.Add(new WrapPanel { Children = { new TextBlock { Text = "Start bei ", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center }, start, new TextBlock { Text = " s", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center } } });
                    sp.Children.Add(new StackPanel { Children = { new TextBlock { Text = $"Lautstärke {c.Volume * 100:0} %", Foreground = Muted, FontSize = 11.5 }, vol } });
                    sp.Children.Add(B("ve-music-apply", "Musik übernehmen", () => d.Edit("Musik", x => VideoEdit.SetMusic(x, s.Index, Num(start), vol.Value))));
                }
                // E3: KI-Einstellung aus dem Musikvideo – nur diese neu erzeugen (Rest bleibt)
                if (VideoEdit.Song(d.Data) is { } lk && Songs.Get(lk.Id) is { } lsong && System.Text.RegularExpressions.Regex.Match(Path.GetFileName(c.File), @"^einstellung(\d{3})\.mp4$") is { Success: true } mm)
                {
                    var idx = int.Parse(mm.Groups[1].Value); var prev = c.File.Contains(Path.DirectorySeparatorChar + "vorschau" + Path.DirectorySeparatorChar);
                    sp.Children.Add(B("ve-shot-redo", $"🎬 Einstellung {idx + 1} neu erzeugen (KI)", () => { MsShotReroll(lsong.Id, idx); _ = MsRenderVideo(lsong.Id, prev, idx).ContinueWith(_ => _ui.Post(() => { _thumbs.Clear(); _veFrames.Clear(); Show(VK); })); }));
                }
                sp.Children.Add(B("ve-sel-delete", "Stück löschen", () => EditDelete(d)));
            }
        }
        sp = bodies["export"];
        sp.Children.Add(EditExportSettings(d)); // X1/X2/X11/X16: Vorlage, Grafikkarte, Bereich, Lautheit, Übergabe an andere Programme
        if (_veExport is { } job)
        {
            var bar = new ProgressBar { Name = "ve-export-progress", Minimum = 0, Maximum = 100, Value = _veExportProgress * 100, Height = 8 };
            _veExportBar = bar;
            var cancel = new Button { Name = "ve-export-cancel", Content = "Export abbrechen" }; cancel.Click += (_, _) => job.Cancel();
            sp.Children.Add(H("Export läuft", 13)); sp.Children.Add(bar); sp.Children.Add(cancel);
        }
        sp.Children.Add(EditExportsPanel()); // „Meine Exporte“ mit Abspielen und Ordner öffnen
        sp.Children.Add(H("Projekt", 13));
        sp.Children.Add(P($"{(int)d.Number("width")} × {(int)d.Number("height")} · {(int)d.Number("fps", 30)} Bilder/s · Länge {Clock(VideoEdit.Duration(d.Data))}", Muted, 12));
        if (FfmpegPath is null) outer.Children.Add(P("FFmpeg fehlt: Videos und Ton brauchen es für Länge, Vorschau und Export. Fotos gehen trotzdem.", Brush.Parse("#FACC15"), 12));
        // X15: verschobene Quelldateien wiederfinden
        if (VideoEdit.Missing(d.Data) is { Count: > 0 } miss)
        {
            var relink = new Button { Name = "ve-relink", Content = "Ordner wählen und neu verknüpfen …", FontSize = 12, Background = Accent, Foreground = Brushes.White };
            relink.Click += async (_, _) =>
            {
                string? dir;
                if (PickOverride is { } fake) dir = (await fake(new PickRequest("ve-relink", true, false, []))) is [var f0, ..] ? f0 : null;
                else dir = (await StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions { Title = "Wo liegen die Dateien jetzt?" })).Select(x => Avalonia.Platform.Storage.StorageProviderExtensions.TryGetLocalPath(x)).OfType<string>().FirstOrDefault();
                if (dir is null) return;
                var n = 0; d.Edit("Neu verknüpfen", x => n = VideoEdit.Relink(x, dir));
                _thumbs.Clear(); _progMsg[VK] = n == 0 ? "In diesem Ordner wurden die fehlenden Dateien nicht gefunden." : $"{n} von {miss.Count} Dateien wieder verknüpft."; Show(VK);
            };
            outer.Children.Insert(0, new Border { Name = "ve-missing", Background = Brush.Parse("#33F59E0B"), BorderBrush = Brush.Parse("#F59E0B"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 8), Margin = new Thickness(0, 0, 0, 8),
                Child = new StackPanel { Spacing = 6, Children = { P($"⚠ {miss.Count} Datei(en) nicht gefunden – verschoben oder umbenannt? z. B. „{Path.GetFileName(miss[0])}“.", Text, 12), relink } } });
        }
        return outer;
    }

    // ---------- Abspielen im Programm ----------
    private Image? _vePreview; private TextBlock? _veTime, _veOverlay;
    private Avalonia.Threading.DispatcherTimer? _vePlay;
    public bool EditPlaying => _vePlay is not null;
    /// <summary>Für Tests: Abspielbilder ersetzen (Stück → Bilddateien).</summary>
    public Func<EditClip, IReadOnlyList<string>>? PlayFramesOverride { get; set; }
    private readonly Dictionary<string, IReadOnlyList<string>> _vePlayFrames = [];
    const int PlayFps = 6;

    public void EditTogglePlay(ProgramDocument d)
    {
        if (_vePlay is not null) { _vePlay.Stop(); _vePlay = null; _progMsg[VK] = "Angehalten bei " + ClockMs(d.Number("playhead")) + "."; Show(VK); return; }
        if (VideoEdit.Clips(d.Data).Count == 0) { _progMsg[VK] = "Nichts zum Abspielen – zuerst Medien auf die Bildspur legen."; Show(VK); return; }
        if (d.Number("playhead") >= VideoEdit.VideoLength(VideoEdit.Clips(d.Data)) - 0.05) d.Set("playhead", 0, "Abspielkopf");
        var t = d.Number("playhead");
        _vePlay = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000.0 / PlayFps) };
        _vePlay.Tick += (_, _) =>
        {
            var clips = VideoEdit.Clips(d.Data); var len = VideoEdit.VideoLength(clips);
            t += EditRate / PlayFps; _vePos = t;
            if (t <= 0 && EditRate < 0) { _vePlay?.Stop(); _vePlay = null; EditRate = 1; d.Set("playhead", 0, "Abspielkopf"); _progMsg[VK] = "Anfang erreicht."; Show(VK); return; }
            if (t >= len) { _vePlay?.Stop(); _vePlay = null; d.Set("playhead", Math.Round(len, 2), "Abspielkopf"); _progMsg[VK] = "Ende erreicht."; Show(VK); return; }
            EditShowFrame(d, t);
        };
        _vePlay.Start(); _progMsg[VK] = "Spielt … (Leertaste hält an)";
    }

    /// <summary>X5: Abspieltempo (J/K/L): 1, 2, 4 vorwärts, −1, −2, −4 rückwärts.</summary>
    public double EditRate { get; private set; } = 1;
    double _vePos = -1;

    /// <summary>X5 Tastatur wie in Resolve/Premiere: J rückwärts (mehrfach = schneller), K Halt, L vorwärts (mehrfach = schneller),
    /// I/O Anfang/Ende des Export-Bereichs, M Marke setzen, Pos1/Ende springen. Öffentlich für Tests.</summary>
    public void EditKey(ProgramDocument d, Key key)
    {
        var len = VideoEdit.Duration(d.Data);
        var at = _vePlay is not null && _vePos >= 0 ? _vePos : d.Number("playhead");
        switch (key)
        {
            case Key.K:
                if (_vePlay is not null) { EditTogglePlay(d); d.Set("playhead", Math.Round(Math.Clamp(at, 0, len), 3), "Abspielkopf"); }
                EditRate = 1; break;
            case Key.L or Key.J:
            {
                var dir = key == Key.L ? 1 : -1;
                EditRate = _vePlay is not null && Math.Sign(EditRate) == dir ? Math.Clamp(EditRate * 2, -4, 4) : dir;
                if (_vePlay is null) { if (dir < 0 && at <= 0.05) { EditRate = 1; break; } EditTogglePlay(d); }
                _progMsg[VK] = (EditRate > 0 ? "▶ " : "◀ ") + $"{Math.Abs(EditRate):0}× (K hält an)"; break;
            }
            case Key.I: EditRange = (Math.Round(at, 2), EditRange.To > at ? EditRange.To : len); _progMsg[VK] = $"Bereich ab {ClockMs(at)} (Export → „Nur einen Bereich“)."; Show(VK); break;
            case Key.O: EditRange = (Math.Min(EditRange.From, at), Math.Round(at, 2)); _progMsg[VK] = $"Bereich {ClockMs(EditRange.From)}–{ClockMs(at)}."; Show(VK); break;
            case Key.M:
            {
                var t = Math.Round(at, 2);
                d.Edit("Marke", x => VideoEdit.SetMarkers(x, VideoEdit.Markers(x).Append(new EditMarker(t, t, "user", $"Marke {VideoEdit.Markers(x).Count(m => m.Kind == "user") + 1}"))));
                _progMsg[VK] = $"Marke bei {ClockMs(t)}."; Show(VK); break;
            }
            case Key.Home: d.Set("playhead", 0, "Abspielkopf"); Show(VK); break;
            case Key.End: d.Set("playhead", Math.Round(len, 2), "Abspielkopf"); Show(VK); break;
        }
    }

    /// <summary>Bild zur Zeit t zeigen, ohne die Seite neu aufzubauen (Abspielen).</summary>
    public void EditShowFrame(ProgramDocument d, double t)
    {
        if (VideoEdit.At(d.Data, t) is not { } at) return;
        var c = VideoEdit.Clips(d.Data)[at.Index];
        if (EditTimelineForTests is { } tl) { tl.Playhead = t; tl.InvalidateVisual(); }
        if (_veTime is not null) _veTime.Text = $"{ClockMs(t)} / {ClockMs(VideoEdit.Duration(d.Data))}";
        if (_veOverlay is not null) { _veOverlay.Text = c.Title; _veOverlay.IsVisible = c.Title.Length > 0; }
        if (_vePreview is null) return;
        try
        {
            string? file = null;
            if (c.IsImage) file = c.File;
            else
            {
                var key = c.Id + "|" + c.In + "|" + c.Out;
                if (!_vePlayFrames.TryGetValue(key, out var frames))
                    frames = _vePlayFrames[key] = PlayFramesOverride?.Invoke(c) ?? (FfmpegPath is { } ff && !_opt.Isolated
                        ? VideoEdit.PlayFrames(ff, c with { File = VideoProxy.ForPreview(d.Data, c.File) }, Path.Combine(MediaFolder, ".vorschau", "abspielen", Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(System.Text.Encoding.UTF8.GetBytes(c.File + File.GetLastWriteTimeUtc(c.File).Ticks + key)))[..12]), PlayFps) : []);
                if (frames.Count > 0) file = frames[Math.Clamp((int)((at.SourceTime - c.In) * PlayFps), 0, frames.Count - 1)];
            }
            if (file is null) return;
            using var s = File.OpenRead(file); _vePreview.Source = new Bitmap(s); _vePreview.IsVisible = true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException) { }
    }

    // ---------- Export im Hintergrund ----------
    private CancellationTokenSource? _veExport; private double _veExportProgress; private ProgressBar? _veExportBar;
    public bool EditExporting => _veExport is not null;
    /// <summary>Für Tests: Ersatz für den Hintergrund-Export (Projekt, Ziel, Fortschritt, Abbruch).</summary>
    public Func<JsonObject, string, IProgress<double>, CancellationToken, Task<string>>? ExportAsyncOverride { get; set; }

    /// <summary>Für Tests: Encoder-Liste des FFmpeg (sonst „ffmpeg -encoders“).</summary>
    public IReadOnlySet<string>? EncodersOverride { get; set; }

    /// <summary>Paket 01 S3-18 (07.11.2026): Render-Warteschlange – weitere Fassungen warten und starten nacheinander von selbst.</summary>
    public sealed record QueuedExport(string Title, JsonObject Data, VideoExport.Options Options);
    readonly List<QueuedExport> _veQueue = [];
    public IReadOnlyList<QueuedExport> EditQueue => _veQueue;
    /// <summary>Ergebnisse der Warteschlange (Datei oder Fehlertext) in der Reihenfolge der Fertigstellung.</summary>
    public List<string> EditQueueResults { get; } = [];

    /// <summary>Aktuelle Einstellungen als weitere Fassung einreihen; läuft nichts, startet sie sofort.</summary>
    public async Task EditQueueAdd(ProgramDocument d)
    {
        var job = new QueuedExport(d.Title, (JsonObject)d.Data.DeepClone(), EditExportOptions(d));
        if (_veExport is null) { await EditExportRun(job); return; }
        _veQueue.Add(job);
        _progMsg[VK] = $"„{job.Options.Preset.Label}“ wartet in der Warteschlange (Platz {_veQueue.Count}).";
        Show(VK);
    }

    public void EditQueueRemove(int index) { if (index >= 0 && index < _veQueue.Count) { _veQueue.RemoveAt(index); Show(VK); } }

    public async Task EditExportBackground(ProgramDocument d)
    {
        if (_veExport is not null) { await EditQueueAdd(d); return; } // läuft schon: einreihen statt abweisen
        await EditExportRun(new QueuedExport(d.Title, (JsonObject)d.Data.DeepClone(), EditExportOptions(d)));
    }

    async Task EditExportRun(QueuedExport job)
    {
        var opts = job.Options; var title = job.Title; var ext = opts.Preset.Sequence ? "" : opts.Preset.Ext; var tag = opts.Preset.Id == "projekt" ? "" : "-" + opts.Preset.Id; // Sequenz: Ordner
        var file = Path.Combine(WorkFolder(Programs[VK].Folder), DevStudio.SafeName(title) + tag + ext);
        for (var i = 2; File.Exists(file) || Directory.Exists(file); i++) file = Path.Combine(WorkFolder(Programs[VK].Folder), $"{DevStudio.SafeName(title)}{tag}-{i}{ext}");
        // Paket 01 S3-18-N: fehlt der Encoder, nichts starten und eine Alternative nennen
        var encs = EncodersOverride ?? (!_opt.Isolated && FfmpegPath is { } ffe ? await Task.Run(() => VideoExport.Encoders(ffe)) : null);
        if (encs is not null && VideoExport.Check(opts.Preset, encs) is { Ok: false } chk) { _progMsg[VK] = chk.Text; EditQueueResults.Add(chk.Text); Show(VK); await EditQueueNext(); return; }
        var data = job.Data;
        _veExport = new CancellationTokenSource(); _veExportProgress = 0; EditTab = "export"; _progMsg[VK] = "Export läuft im Hintergrund – du kannst weiterarbeiten."; Show(VK);
        var progress = new Progress<double>(v => { _veExportProgress = v; if (_veExportBar is { } b) b.Value = v * 100; });
        try
        {
            string o;
            if (ExportAsyncOverride is { } fake) o = await fake(data, file, progress, _veExport.Token);
            else
            {
                var ff = FfmpegPath ?? throw new InvalidOperationException("Für den Export fehlt FFmpeg – unter Zusatzprogramme einrichten.");
                if (_opt.Isolated) throw new InvalidOperationException("Im abgeschotteten Testmodus startet Trendo kein FFmpeg.");
                if (opts.Gpu is null && !opts.Preset.AudioOnly) { _veGpu = await Task.Run(() => VideoExport.GpuEncoders(ff)); opts = opts with { Gpu = _veGpu }; }
                o = await VideoEdit.ExportAsync(ff, data, file, progress, _veExport.Token, opts);
            }
            _core.Audit.Append(Principal.LocalOwner.Name, "video.export", Path.GetFileName(o));
            EditCopySubs(data, o); // X10: SRT neben das Video
            if (!opts.Preset.Sequence && VideoEdit.Song(data) is { } linked && Songs.Get(linked.Id) is { } ls) SongVideoAdd(ls, o); // E2: fertiges Video auch beim Lied
            _progMsg[VK] = "Exportiert: " + o; NotifyFileResult(_progMsg[VK]); EditQueueResults.Add(o);
        }
        catch (OperationCanceledException) { _progMsg[VK] = "Export abgebrochen – die halbe Datei wurde gelöscht."; EditQueueResults.Add("abgebrochen"); }
        catch (Exception ex) when (ex is InvalidOperationException or FileNotFoundException or IOException or System.ComponentModel.Win32Exception) { _progMsg[VK] = ex.Message; EditQueueResults.Add(ex.Message); }
        finally { _veExport.Dispose(); _veExport = null; _veExportBar = null; }
        Show(VK);
        await EditQueueNext();
    }

    async Task EditQueueNext()
    {
        if (_veExport is not null || _veQueue.Count == 0) return;
        var next = _veQueue[0]; _veQueue.RemoveAt(0);
        await EditExportRun(next);
    }
}
