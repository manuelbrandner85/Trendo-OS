using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core.Media;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>Videoschnitt-Ausbau: Überblenden und Text am Stück, Abspielen im Programm, Export im Hintergrund mit Fortschritt und Abbrechen.</summary>
public class VideoSchnittPlus_Ui
{
    static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");
    static void Click(MainWindow w, string name) { var b = V<Button>(w, name); b.BringIntoView(); Settle(); RealClick(w, V<Button>(w, name)); }

    [AvaloniaFact(DisplayName = "Videoschnitt UI: Überblenden und Titel setzen (Länge kürzer, Titel in der Vorschau), Abspielen zeigt Bilder und Titel, Hintergrund-Export mit Fortschritt, Abbrechen")]
    public async Task Fade_title_play_export()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000; Directory.CreateDirectory(root);
        string F(string n, byte[]? b = null) { var f = Path.Combine(root, n); File.WriteAllBytes(f, b ?? [1]); return f; }
        w.Show("schnitt"); Settle(); RealClick(w, V<Button>(w, "prog-tpl-leer"));
        w.EditAdd(F("a.mp4"), length: 4, audio: true); w.EditAdd(F("b.mp4"), length: 4, audio: true);
        w.EditSelected = ("clips", 0); w.Show("schnitt"); Settle();
        w.Find<TextBox>("ve-fade").Text = "1"; w.Find<TextBox>("ve-title").Text = "Sommer 2026";
        Click(w, "ve-clip-apply");
        var d = w.ProgramActive("schnitt")!; var c0 = VideoEdit.Clips(d.Data)[0];
        Assert.Equal((1.0, "Sommer 2026"), (c0.Fade, c0.Title)); Assert.Equal(7, VideoEdit.Duration(d.Data), 3);
        Assert.Equal("Überblenden/Text", d.UndoLabel);
        // Abspielen: Bilder je Stück (FFmpeg ersetzt), Titel sichtbar im ersten Stück, nicht im zweiten
        var fa = new[] { F("fa1.png", Png), F("fa2.png", Png) }; var fb = new[] { F("fb1.png", Png) };
        w.PlayFramesOverride = c => c.File.EndsWith("a.mp4") ? fa : fb;
        w.EditShowFrame(d, 0.5); Settle();
        Assert.True(w.Find<TextBlock>("ve-title-overlay").IsVisible); Assert.Equal("Sommer 2026", w.Find<TextBlock>("ve-title-overlay").Text);
        w.EditShowFrame(d, 5.5); Settle();
        Assert.False(w.Find<TextBlock>("ve-title-overlay").IsVisible); Assert.Contains("0:05.5", w.Find<TextBlock>("ve-time").Text);
        w.EditTogglePlay(d); Assert.True(w.EditPlaying); w.EditTogglePlay(d); Assert.False(w.EditPlaying);
        // Hintergrund-Export: Fortschritt sichtbar, danach Meldung
        var gate = new TaskCompletionSource();
        w.ExportAsyncOverride = async (p, f, prog, ct) => { prog.Report(0.4); await gate.Task.WaitAsync(ct); File.WriteAllText(f, "mp4"); prog.Report(1); return f; };
        Click(w, "prog-cmd-ve-export-bg");
        for (var i = 0; i < 100 && w.TryFind<ProgressBar>("ve-export-progress") is null; i++) { await Task.Delay(10); Settle(); }
        Assert.True(w.EditExporting); Assert.Equal(40, w.Find<ProgressBar>("ve-export-progress").Value, 0);
        gate.SetResult();
        for (var i = 0; i < 100 && w.EditExporting; i++) { await Task.Delay(10); Settle(); }
        Assert.Contains("Exportiert", w.ProgramMessage("schnitt"));
        // Abbrechen
        w.ExportAsyncOverride = async (p, f, prog, ct) => { await Task.Delay(-1, ct); return f; };
        Click(w, "prog-cmd-ve-export-bg");
        for (var i = 0; i < 100 && w.TryFind<Button>("ve-export-cancel") is null; i++) { await Task.Delay(10); Settle(); }
        Click(w, "ve-export-cancel");
        for (var i = 0; i < 100 && w.EditExporting; i++) { await Task.Delay(10); Settle(); }
        Assert.Contains("abgebrochen", w.ProgramMessage("schnitt"));
    }

    [AvaloniaFact(DisplayName = "Videoschnitt „Meine Exporte“: fertiges Video bleibt in der Liste, Abspielen startet es, Ordner öffnen markiert die Datei im Explorer, Löschen in den Papierkorb")]
    public async Task Exports_list()
    {
        var (w, l, root) = Ui.Open(); w.Width = 1600; w.Height = 1000; Directory.CreateDirectory(root);
        var src = Path.Combine(root, "a.mp4"); File.WriteAllBytes(src, [1]);
        var recycled = new List<string>(); w.ExportRecycleOverride = recycled.Add;
        w.Show("schnitt"); Settle(); RealClick(w, V<Button>(w, "prog-tpl-leer"));
        w.EditAdd(src, length: 4, audio: true); w.Show("schnitt"); Settle();
        Assert.Contains("Noch nichts exportiert", string.Join("|", w.GetLogicalTexts()));
        w.ExportAsyncOverride = (p, f, prog, ct) => { File.WriteAllText(f, "mp4"); return Task.FromResult(f); };
        Click(w, "prog-cmd-ve-export-bg");
        for (var i = 0; i < 100 && w.EditExporting; i++) { await Task.Delay(10); Settle(); }
        w.Show("schnitt"); Settle();
        var file = w.EditExports().Single().FullName;
        Assert.Equal(Path.GetFileName(file), w.Find<TextBlock>("ve-exp-name-0").Text);
        Click(w, "ve-exp-play-0"); Assert.Contains(file, l.Opened);
        Click(w, "ve-exp-folder-0");
        Assert.Contains(l.Opened, o => o.Contains("explorer.exe") && o.Contains("/select," + file));
        Click(w, "ve-exp-del-0"); Settle();
        Assert.Equal([file], recycled);
    }

    [AvaloniaFact(DisplayName = "Videoschnitt übersichtlich: Spurnamen, Abspielleiste unter der Vorschau, rechts Medien · Auswahl · Export – Auswahl öffnet sich beim Anklicken eines Stücks")]
    public void Clear_layout()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000; Directory.CreateDirectory(root);
        string F(string n) { var f = Path.Combine(root, n); File.WriteAllBytes(f, Png); return f; }
        w.Show("schnitt"); Settle(); RealClick(w, V<Button>(w, "prog-tpl-leer"));
        Assert.NotNull(w.Find<Canvas>("ve-track-labels"));
        Assert.True(w.Find<StackPanel>("ve-tab-body-medien").IsVisible);
        Assert.Contains(w.GetLogicalTexts(), t => t.Contains("Noch nichts ausgewählt"));
        w.EditAdd(F("a.png")); w.EditAdd(F("b.png")); w.Show("schnitt"); Settle();
        Assert.True(w.Find<StackPanel>("ve-tab-body-auswahl").IsVisible); // neues Stück: seine Einstellungen
        Assert.True(V<Button>(w, "ve-trim-apply").IsEffectivelyVisible);
        Click(w, "ve-tab-medien");
        Assert.True(V<Button>(w, "ve-add").IsEffectivelyVisible);
        var d = w.ProgramActive("schnitt")!;
        Click(w, "ve-tp-end"); Assert.Equal(VideoEdit.Duration(d.Data), d.Number("playhead"), 3);
        Click(w, "ve-tp-start"); Assert.Equal(0, d.Number("playhead"), 3);
        Click(w, "ve-tp-fwd"); Assert.Equal(1, d.Number("playhead"), 3);
        Click(w, "ve-tp-play"); Assert.True(w.EditPlaying); Click(w, "ve-tp-play"); Assert.False(w.EditPlaying);
        Click(w, "ve-tab-export");
        Assert.True(w.Find<StackPanel>("ve-tab-body-export").IsVisible);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Videoschnitt Export X1/X11/X16: Vorlage Hochformat per Klick, Bereich und Lautheit gehen an den Export, Dateiname mit Vorlage, EDL und FCPXML werden geschrieben")]
    public async Task Export_presets_and_interchange()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000; Directory.CreateDirectory(root);
        string F(string n) { var f = Path.Combine(root, n); File.WriteAllBytes(f, [1]); return f; }
        w.Show("schnitt"); Settle(); RealClick(w, V<Button>(w, "prog-tpl-leer"));
        w.EditAdd(F("a.mp4"), length: 6, audio: true); w.Show("schnitt"); Settle();
        w.EditTab = "export"; w.Show("schnitt"); Settle();
        Click(w, "ve-preset-hoch");
        Assert.Equal("hoch", w.EditPreset);
        w.Find<CheckBox>("ve-loud").IsChecked = true; w.Find<CheckBox>("ve-range").IsChecked = true;
        w.Find<NumericUpDown>("ve-range-a").Value = 1; w.Find<NumericUpDown>("ve-range-b").Value = 4; Settle();
        var o = w.EditExportOptions(w.ProgramActive("schnitt")!);
        Assert.Equal(("hoch", true, 1.0, 4.0), (o.Preset.Id, o.Loudness, o.From, o.To));
        string? target = null;
        w.ExportAsyncOverride = (p, f, prog, ct) => { target = f; File.WriteAllText(f, "mp4"); return Task.FromResult(f); };
        Click(w, "ve-export-go");
        for (var i = 0; i < 100 && (target is null || w.EditExporting); i++) { await Task.Delay(10); Settle(); }
        Assert.EndsWith("-hoch.mp4", target);
        // nur Ton: Endung .m4a
        w.Show("schnitt"); Settle(); Click(w, "ve-preset-ton"); target = null;
        Click(w, "ve-export-go");
        for (var i = 0; i < 100 && (target is null || w.EditExporting); i++) { await Task.Delay(10); Settle(); }
        Assert.EndsWith(".m4a", target);
        // Übergabe
        w.Show("schnitt"); Settle(); Click(w, "ve-edl"); Settle();
        var edl = Directory.GetFiles(root, "*.edl", SearchOption.AllDirectories).Single();
        Assert.Contains("FROM CLIP NAME: a.mp4", File.ReadAllText(edl));
        w.Show("schnitt"); Settle(); Click(w, "ve-fcpxml"); Settle();
        Assert.Contains("<fcpxml version=\"1.10\"", File.ReadAllText(Directory.GetFiles(root, "*.fcpxml", SearchOption.AllDirectories).Single()));
        Assert.Contains("Resolve", w.ProgramMessage("schnitt"));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Videoschnitt X15: verschobene Datei wird gemeldet und per Ordnerwahl neu verknüpft (Rückgängig möglich)")]
    public void Relink_missing()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000; Directory.CreateDirectory(root);
        var a = Path.Combine(root, "alt", "strand.mp4"); Directory.CreateDirectory(Path.GetDirectoryName(a)!); File.WriteAllBytes(a, [1]);
        w.Show("schnitt"); Settle(); RealClick(w, V<Button>(w, "prog-tpl-leer"));
        w.EditAdd(a, length: 4, audio: true); w.Show("schnitt"); Settle();
        Assert.Null(w.TryFind<Border>("ve-missing"));
        var neu = Path.Combine(root, "neu", "urlaub"); Directory.CreateDirectory(neu); File.Move(a, Path.Combine(neu, "strand.mp4"));
        w.Show("schnitt"); Settle();
        Assert.NotNull(w.TryFind<Border>("ve-missing"));
        w.PickOverride = _ => Task.FromResult<string[]?>([Path.Combine(root, "neu")]);
        Click(w, "ve-relink"); Settle(); Settle();
        var d = w.ProgramActive("schnitt")!;
        Assert.Equal(Path.Combine(neu, "strand.mp4"), VideoEdit.Clips(d.Data)[0].File);
        Assert.Contains("1 von 1", w.ProgramMessage("schnitt")); Assert.Equal("Neu verknüpfen", d.UndoLabel);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Videoschnitt X5: L spielt (zweimal = 2×), J rückwärts, K hält an, I/O setzen den Export-Bereich, M setzt eine Marke, Pos1/Ende springen")]
    public void Jkl_keys()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000; Directory.CreateDirectory(root);
        string F(string n) { var f = Path.Combine(root, n); File.WriteAllBytes(f, [1]); return f; }
        w.Show("schnitt"); Settle(); RealClick(w, V<Button>(w, "prog-tpl-leer"));
        w.EditAdd(F("a.mp4"), length: 10, audio: true); w.Show("schnitt"); Settle();
        var d = w.ProgramActive("schnitt")!;
        w.EditKey(d, Avalonia.Input.Key.L); Assert.True(w.EditPlaying); Assert.Equal(1, w.EditRate);
        w.EditKey(d, Avalonia.Input.Key.L); Assert.Equal(2, w.EditRate);
        w.EditKey(d, Avalonia.Input.Key.K); Assert.False(w.EditPlaying); Assert.Equal(1, w.EditRate);
        w.EditKey(d, Avalonia.Input.Key.End); Assert.Equal(10, d.Number("playhead"), 1);
        w.EditKey(d, Avalonia.Input.Key.J); Assert.True(w.EditPlaying); Assert.Equal(-1, w.EditRate);
        w.EditKey(d, Avalonia.Input.Key.K);
        d.Set("playhead", 2, "Abspielkopf"); w.EditKey(d, Avalonia.Input.Key.I);
        d.Set("playhead", 6, "Abspielkopf"); w.EditKey(d, Avalonia.Input.Key.O);
        Assert.Equal((2.0, 6.0), w.EditRange);
        w.EditKey(d, Avalonia.Input.Key.M);
        Assert.Contains(VideoEdit.Markers(d.Data), m => m.Kind == "user" && Math.Abs(m.Start - 6) < 0.01);
        Assert.Equal("Marke", d.UndoLabel);
        w.EditKey(d, Avalonia.Input.Key.Home); Assert.Equal(0, d.Number("playhead"));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Videoschnitt X10: Untertitel erzeugen (Whisper ersetzt), Anzahl und Beispiel sichtbar, Einbrennen geht an den Export, SRT liegt neben dem Video")]
    public async Task Subtitles()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000; Directory.CreateDirectory(root);
        string F(string n) { var f = Path.Combine(root, n); File.WriteAllBytes(f, [1]); return f; }
        w.Show("schnitt"); Settle(); RealClick(w, V<Button>(w, "prog-tpl-leer"));
        w.EditAdd(F("a.mp4"), length: 6, audio: true); w.Show("schnitt"); Settle();
        w.EditTab = "export"; w.Show("schnitt"); Settle();
        const string srtText = """
            1
            00:00:00,500 --> 00:00:02,000
            Hallo Welt

            2
            00:00:02,000 --> 00:00:03,000
            Tschüss
            """;
        w.SubsOverride = (wav, srt) => { File.WriteAllText(srt, srtText); return Task.FromResult(true); };
        Click(w, "ve-subs");
        for (var i = 0; i < 100 && w.TryFind<CheckBox>("ve-burn") is null; i++) { await Task.Delay(10); Settle(); }
        Assert.Contains("2 Untertitel", w.ProgramMessage("schnitt"));
        var d = w.ProgramActive("schnitt")!; Assert.Equal("Untertitel", d.UndoLabel);
        w.Find<CheckBox>("ve-burn").IsChecked = true; Settle();
        Assert.Equal(d.Text("subs"), w.EditExportOptions(d).Subtitles);
        string? target = null;
        w.ExportAsyncOverride = (p, f, prog, ct) => { target = f; File.WriteAllText(f, "mp4"); return Task.FromResult(f); };
        Click(w, "ve-export-go");
        for (var i = 0; i < 100 && (target is null || w.EditExporting); i++) { await Task.Delay(10); Settle(); }
        Assert.True(File.Exists(Path.ChangeExtension(target!, ".srt")));
        w.Close();
    }
}
