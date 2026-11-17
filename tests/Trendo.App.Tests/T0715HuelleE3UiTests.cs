using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Media;
using Trendo.Core.Programs;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0715 (B6 „Programm-Hülle für alle“, Etappe E3 Kreativ/Medien): Medien bearbeiten (Bild und Video, echtes FFmpeg), Spuren-Mischer von Trendo Musik
/// und Live streamen hängen über ProgramLink an Rückgängig (100 Schritte) und – wo nicht sofort gespeichert wird – an der Notsicherung der Hülle.
/// Je Programm echt über die Oberfläche: ändern → mehrere Schritte Rückgängig und Wiederholen mit den Knöpfen → Takt der Hülle → zweites Fenster bietet nichts an →
/// Fenster ohne Speichern schließen → neues Fenster: Karte → Wiederherstellen bringt exakt den Stand → Speichern räumt die Sicherung weg.</summary>
public class T0715_HuelleE3_Ui
{
    static T Get<T>(MainWindow w, string name) where T : Control { Settle(); return w.GetVisualDescendants().OfType<T>().Last(c => c.Name == name && c.IsEffectivelyVisible && c.Bounds.Width > 0); }
    static bool Has(MainWindow w, string name) { Settle(); return w.GetVisualDescendants().OfType<Control>().Any(c => c.Name == name && c.IsEffectivelyVisible); }
    static void Press(MainWindow w, string name) { var b = Get<Button>(w, name); b.BringIntoView(); Settle(); RealClick(w, Get<Button>(w, name)); Settle(); }
    static void Type(MainWindow w, string name, string text) { var tb = Get<TextBox>(w, name); tb.BringIntoView(); Settle(); RealClick(w, Get<TextBox>(w, name)); tb = Get<TextBox>(w, name); tb.Focus(); tb.CaretIndex = tb.Text?.Length ?? 0; Settle(); foreach (var ch in text) { w.KeyTextInput(ch.ToString()); Settle(); } }
    static void Pause() { var until = DateTime.Now + MainWindow.LinkTypingDelay + TimeSpan.FromMilliseconds(150); while (DateTime.Now < until) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); Thread.Sleep(20); } Settle(); }

    static MainWindow Reopen(string root)
    {
        var w = new MainWindow(new TrendoCore(root, new InMemorySecretStore(), new FakeLauncher()), new AppOptions(root, null, 0, Isolated: true), new Probe("browser"));
        w.ProgramSetLevel(UserLevel.Erweitert); w.FreigabeConfirm = _ => Task.FromResult(true); w.Width = 1600; w.Height = 1100; w.Show(); Settle(); return w;
    }

    static string[] Backups(string root, string studio) { var d = Path.Combine(root, "programme", "sicherung"); return Directory.Exists(d) ? Directory.GetFiles(d, studio + "-*.tdoc") : []; }

    /// <summary>Der ganze Ablauf für ein Programm mit Notsicherung (wie T-0711). <paramref name="also"/> läuft im neuen Fenster nach dem Zurückholen (z. B. Speichern).</summary>
    static void Cycle(string page, string prefix, string studio, Action<MainWindow, string> open, Action<MainWindow> step1, Action<MainWindow> step2, Func<MainWindow, string> state,
        Action<MainWindow> reopen, Action<MainWindow, string, string>? also = null)
    {
        MainWindow.LinkTypingDelay = TimeSpan.FromMilliseconds(300);
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1100;
        try
        {
            open(w, root); Settle();
            var s0 = state(w);
            Assert.False(Get<Button>(w, prefix + "-undo").IsEnabled, page + ": Rückgängig ist am Anfang schon an");
            step1(w); Pause(); var s1 = state(w); Assert.NotEqual(s0, s1);
            step2(w); Pause(); var s2 = state(w); Assert.NotEqual(s1, s2);
            Press(w, prefix + "-undo"); Assert.Equal(s1, state(w));
            Press(w, prefix + "-undo"); Assert.Equal(s0, state(w)); // mehrere Schritte zurück
            Press(w, prefix + "-redo"); Assert.Equal(s1, state(w));
            Press(w, prefix + "-redo"); Assert.Equal(s2, state(w));
            w.ProgramAutosaveAll(); // Takt der Hülle (alle 20 s)
            var file = Assert.Single(Backups(root, studio));
            Assert.True(ProgramDocument.Readable(file));

            var other = Reopen(root); // zweites Fenster, solange das erste läuft: bietet die Sicherung NICHT an (Sperre)
            try { reopen(other); Settle(); Assert.False(Has(other, prefix + "-recovery"), page + ": zweites Fenster bietet die Sicherung des ersten an"); }
            finally { other.Close(); }

            w.Close(); // ohne Speichern
            var w2 = Reopen(root);
            try
            {
                reopen(w2); Settle();
                Assert.True(Has(w2, prefix + "-recovery"), page + ": keine Wiederherstellen-Karte");
                Press(w2, prefix + "-restore-0");
                Assert.Equal(s2, state(w2)); // exakt der Stand
                Assert.False(Has(w2, prefix + "-recovery"), page + ": zurückgeholte Sicherung wird erneut angeboten");
                Assert.True(File.Exists(file)); // bleibt, bis gespeichert ist
                also?.Invoke(w2, root, file);
            }
            finally { if (w2.IsVisible) w2.Close(); }
        }
        finally { MainWindow.LinkTypingDelay = TimeSpan.FromSeconds(1); if (w.IsVisible) w.Close(); }
    }

    static string Png(string root)
    {
        var f = Path.Combine(root, "Foto.png");
        using var bmp = new SkiaSharp.SKBitmap(320, 200);
        using (var c = new SkiaSharp.SKCanvas(bmp)) { c.Clear(new SkiaSharp.SKColor(0x30, 0x60, 0x90)); using var p = new SkiaSharp.SKPaint { Color = SkiaSharp.SKColors.Gold }; c.DrawCircle(160, 100, 60, p); }
        using var img = SkiaSharp.SKImage.FromBitmap(bmp); using var data = img.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(f, data.ToArray()); return f;
    }

    [AvaloniaFact(DisplayName = "T-0715 Medien bearbeiten (Bild): Helligkeit und Text – Rückgängig mehrere Schritte, Notsicherung, zweites Fenster nichts, Karte bringt exakt den Stand, „Bild speichern“ schreibt das echte Bild und räumt weg")]
    public void Media_image()
    {
        Cycle("kreativ-medien", "media", "LINK-MEDIEN", (w, root) => w.MediaPick(Png(root)),
            w => { Get<Slider>(w, "img-bright").Value = 30; Settle(); }, w => Type(w, "img-text", "Urlaub"),
            w => w.MediaState().ToJsonString(), w => w.Show("kreativ-medien"),
            also: (w, root, file) =>
            {
                Assert.Equal(30, (int)Get<Slider>(w, "img-bright").Value); Assert.Equal("Urlaub", Get<TextBox>(w, "img-text").Text); // Felder zeigen den Stand
                Press(w, "img-save");
                var o = w.MediaLastOutput!; Assert.True(File.Exists(o)); Assert.EndsWith(".jpg", o);
                using (var bmp = SkiaSharp.SKBitmap.Decode(o)) Assert.Equal(320, bmp.Width); // echtes Bild
                Assert.False(File.Exists(file)); // Ergebnis entstanden – Sicherung weg
                Assert.Empty(Directory.GetFiles(root, "*.tmp", SearchOption.AllDirectories));
            });
    }

    [AvaloniaFact(DisplayName = "T-0715 Medien bearbeiten (Video, echtes FFmpeg): Clipliste und Ausschnitt – Rückgängig, Notsicherung, Karte, „Ausschnitt speichern“ schneidet echt und atomar")]
    public void Media_video()
    {
        var src = W4_Core08_Media_Ui.Sample(); if (src is null) { Assert.Skip("BLOCKIERT – FFmpeg fehlt auf diesem PC."); return; }
        try
        {
            Cycle("kreativ-medien", "media", "LINK-MEDIEN", (w, _) => w.MediaPick(src),
                w => Press(w, "media-clip-add"), w => { Get<Slider>(w, "media-from").Value = 0.5; Settle(); },
                w => w.MediaState().ToJsonString(), w => w.Show("kreativ-medien"),
                also: (w, _, file) =>
                {
                    Assert.Single(w.MediaClips);
                    Press(w, "media-cut");
                    var o = w.MediaLastOutput!; Assert.True(File.Exists(o), "kein Ausschnitt");
                    var d = MediaStudio.Duration(w.FfmpegPath!, o); Assert.NotNull(d); Assert.InRange(d!.Value, 1.2, 1.8); // 0,5 s bis 2 s
                    Assert.False(File.Exists(file));
                    Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(src)!, "*.teil*"));
                });
        }
        finally { try { Directory.Delete(Path.GetDirectoryName(src)!, true); } catch (IOException) { } }
    }

    sealed class FakeAudio : IAudioOut
    {
        public event Action<double, double>? Progress { add { } remove { } }
        public event Action? Ended { add { } remove { } }
        public void Load(string file) { }
        public void SetPause(bool pause) { } public void Seek(double seconds) { } public void SetVolume(int percent) { } public void Stop() { } public void Dispose() { }
    }

    const int Rate = 44100;
    static float[] Tone(double hz, double amp) => Enumerable.Range(0, Rate).Select(i => (float)(amp * Math.Sin(2 * Math.PI * hz * i / Rate))).ToArray();
    static void Wav(string f, float[] x) => AudioFile.WriteWav(new AudioBuf(Rate, [x, (float[])x.Clone()]), f);

    [AvaloniaFact(DisplayName = "T-0715 Trendo Musik Spuren-Mischer: Stumm/Solo – Rückgängig mehrere Schritte, Notsicherung, zweites Fenster nichts, Karte bringt die Mischung zurück, „Als Lied speichern“ räumt weg")]
    public async Task Music_stem_mixer()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1100;
        try
        {
            var stems = new Dictionary<string, float[]> { ["gesang"] = Tone(440, 0.2), ["schlagzeug"] = Tone(80, 0.15), ["bass"] = Tone(110, 0.15), ["rest"] = Tone(660, 0.1) };
            var src = Path.Combine(root, "Mein Lied.wav"); Wav(src, Enumerable.Range(0, Rate).Select(i => stems.Values.Sum(s => s[i])).ToArray());
            w.StemSplitOverride = (file, dir) => { Directory.CreateDirectory(dir); foreach (var (n, x) in stems) Wav(Path.Combine(dir, n + ".wav"), x); Wav(Path.Combine(dir, "begleitung.wav"), Enumerable.Range(0, Rate).Select(i => stems["schlagzeug"][i] + stems["bass"][i] + stems["rest"][i]).ToArray()); return Task.FromResult(true); };
            w.AudioOverride = () => new FakeAudio();
            w.Show("musik-studio"); Settle();
            w.PickOverride = _ => Task.FromResult<string[]?>([src]);
            w.Find<Button>("ms-import").Press(); // „＋ Eigene Aufnahme“ → gleich trennen
            for (var i = 0; i < 200 && w.Songs.All().Count < 3; i++) { await Task.Delay(20); Settle(); }
            var song = w.Songs.All().First(x => x.Kind == "eigen");
            w.MusicStudioSelect(song.Id); w.Show("musik-studio"); Press(w, "ms-dtab-spuren");
            string State() => w.MsStemLink.Get()?.ToJsonString() ?? "-";
            var s0 = State();
            Assert.False(Get<Button>(w, "ms-stem-undo").IsEnabled);
            Press(w, "ms-stem-mute-gesang"); var s1 = State(); Assert.NotEqual(s0, s1);
            Press(w, "ms-stem-solo-bass"); var s2 = State(); Assert.NotEqual(s1, s2);
            Press(w, "ms-stem-undo"); Assert.Equal(s1, State());
            Press(w, "ms-stem-undo"); Assert.Equal(s0, State());
            Press(w, "ms-stem-redo"); Assert.Equal(s1, State());
            Press(w, "ms-stem-redo"); Assert.Equal(s2, State());
            w.ProgramAutosaveAll();
            var file = Assert.Single(Backups(root, "LINK-MUSIK-SPUREN"));

            var other = Reopen(root);
            try { other.Show("musik-studio"); Settle(); Assert.False(Has(other, "ms-stem-recovery"), "zweites Fenster bietet die Mischung des ersten an"); }
            finally { other.Close(); }

            w.Close(); // ohne Speichern
            var w2 = Reopen(root);
            try
            {
                w2.AudioOverride = () => new FakeAudio(); w2.StemSplitOverride = (_, _) => Task.FromResult(true); // „Spuren trennen“ gilt als eingerichtet (wie im ersten Fenster) – sonst zeigt das Lied statt des Mischers „Einrichten“
                w2.Show("musik-studio"); Settle();
                Assert.True(Has(w2, "ms-stem-recovery"), "keine Wiederherstellen-Karte");
                Press(w2, "ms-stem-restore-0");
                Assert.Equal(s2, w2.MsStemLink.Get()?.ToJsonString());
                Assert.True(Has(w2, "ms-stem-mixer"), "Mischer nicht zu sehen");
                Assert.False(Has(w2, "ms-stem-recovery"));
                Assert.True(File.Exists(file));
                var saved = w2.MsStemSave(w2.Songs.Get(song.Id)!)!;
                Assert.True(File.Exists(saved.File)); Assert.Equal("mischung", saved.Kind);
                Assert.True(AudioFile.Read(saved.File).Length > 0); // echte WAV
                Assert.False(File.Exists(file)); // als Lied gespeichert – Sicherung weg
                Assert.Empty(Directory.GetFiles(root, "*.teil*", SearchOption.AllDirectories));
            }
            finally { if (w2.IsVisible) w2.Close(); }
        }
        finally { if (w.IsVisible) w.Close(); }
    }

    static string Bc(MainWindow w) => string.Join("|", w.Broadcast.Plan.Segments.Select(s => s.Title)) + " / " + string.Join(",", w.LiveShowState.Scenes.Select(s => s.Name));

    [AvaloniaFact(DisplayName = "T-0715 Live streamen: alte ablauf.json lesbar; Programmpunkte und Szene – Rückgängig/Wiederholen mehrere Schritte (auch über das Menü), sofort gespeichert (keine Karte nötig, zweites Fenster sieht den Stand), Export atomar")]
    public void Broadcast_undo()
    {
        MainWindow.LinkTypingDelay = TimeSpan.FromMilliseconds(300);
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1100;
        try
        {
            // alte Datei (vor T-0715, ohne Version)
            File.WriteAllText(Path.Combine(Fach.StoreDir(root, "sendung"), "ablauf.json"), "{\n  \"Show\": \"Abendshow\",\n  \"Start\": \"2026-10-21T20:00:00\",\n  \"SlotMinutes\": 30,\n  \"Segments\": [ { \"Title\": \"Intro\", \"Seconds\": 60, \"Note\": \"\" } ]\n}");
            w.Show("sendung"); Settle();
            Assert.Equal("Abendshow", Get<TextBox>(w, "bc-show").Text);
            var s0 = Bc(w);
            Assert.False(Get<Button>(w, "bc-undo").IsEnabled);
            Type(w, "bc-new-title", "Begrüßung"); Press(w, "bc-new-add"); var s1 = Bc(w); Assert.Contains("Begrüßung", s1);
            Type(w, "bc-new-title", "Interview"); Press(w, "bc-new-add"); var s2 = Bc(w); Assert.Contains("Interview", s2);
            w.LiveAddScene("Gast"); w.Show("sendung"); Settle(); var s3 = Bc(w); Assert.Contains("Gast", s3); // Studio gehört dazu
            Press(w, "bc-undo"); Assert.Equal(s2, Bc(w));
            Press(w, "bc-undo"); Assert.Equal(s1, Bc(w));
            Press(w, "bc-undo"); Assert.Equal(s0, Bc(w));
            var peek = Reopen(root); try { peek.Show("sendung"); Settle(); Assert.Equal(s0, Bc(peek)); } finally { peek.Close(); } // Rückgängig ist auch sofort gespeichert
            Press(w, "bc-redo"); Press(w, "bc-redo"); Assert.Equal(s2, Bc(w));
            // „Bearbeiten › Rückgängig“ der Menüleiste
            var bar = w.ActiveMenuBar() ?? throw new Xunit.Sdk.XunitException("keine Menüleiste");
            var names = bar.GetLogicalDescendants().OfType<MenuItem>().Select(m => m.Name).ToList();
            Assert.Contains("menu-bc-undo", names); Assert.Contains("menu-bc-export", names);
            bar.GetLogicalDescendants().OfType<MenuItem>().First(m => m.Name == "menu-bc-undo").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); Settle();
            Assert.Equal(s1, Bc(w));
            Press(w, "bc-redo"); Assert.Equal(s2, Bc(w));

            w.ProgramAutosaveAll();
            Assert.Empty(Backups(root, "LINK-SENDUNG")); // sofort gespeichert – keine Notsicherung nötig
            Assert.Contains("\"Version\": 1", File.ReadAllText(Path.Combine(Fach.StoreDir(root, "sendung"), "ablauf.json")));
            Press(w, "bc-export");
            var csv = Path.Combine(Fach.StoreDir(root, "sendung"), "ablaufplan.csv");
            Assert.Contains("Interview", File.ReadAllText(csv));
            Assert.Empty(Directory.GetFiles(Fach.StoreDir(root, "sendung"), "*.tmp"));

            w.Close(); // „ohne Speichern“ – es gibt nichts Ungespeichertes
            var w2 = Reopen(root);
            try
            {
                w2.Show("sendung"); Settle();
                Assert.Equal(s2, Bc(w2)); // nichts verloren
                Assert.False(Has(w2, "bc-recovery"));
                Assert.False(Get<Button>(w2, "bc-undo").IsEnabled); // neues Fenster, neuer Verlauf
            }
            finally { w2.Close(); }
        }
        finally { MainWindow.LinkTypingDelay = TimeSpan.FromSeconds(1); if (w.IsVisible) w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0715 Menüleiste: Live streamen und Medien bearbeiten haben „Datei“ und „Bearbeiten › Rückgängig“, Trendo Musik zusätzlich die Spuren-Mischung")]
    public void Menu_bar()
    {
        var (w, _, _) = Ui.Open(); w.Width = 3000; w.Height = 1000;
        try
        {
            foreach (var (page, must) in new[] { ("sendung", new[] { "menu-bc-undo", "menu-bc-redo", "menu-bc-export" }), ("kreativ-medien", new[] { "menu-media-undo", "menu-media-redo", "menu-media-folder" }), ("musik-studio", new[] { "menu-ms-stem-undo" }) })
            {
                foreach (var old in w.Windows.Stack.Reverse().Skip(3).ToList()) w.CloseWindow(old.Id);
                w.Show(page); Settle();
                var bar = w.ActiveMenuBar() ?? throw new Xunit.Sdk.XunitException("keine Menüleiste auf " + page);
                var names = bar.GetLogicalDescendants().OfType<MenuItem>().Select(m => m.Name).ToList();
                foreach (var n in must) Assert.True(names.Contains(n), $"{page}: {n} fehlt ({string.Join(" ", names)})");
            }
        }
        finally { w.Close(); }
    }
}

