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
}
