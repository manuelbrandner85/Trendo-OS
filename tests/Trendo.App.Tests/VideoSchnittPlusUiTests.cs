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
}
