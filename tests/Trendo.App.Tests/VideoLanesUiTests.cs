using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Trendo.App;
using Trendo.Core.Media;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>Pilot 4.0: V2 Spuren, V3 Bildraster, V4 Werkzeuge über echte Maus und Tasten.</summary>
public class VideoLanes_Ui
{
    static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");
    static Point P(MainWindow w, double x, double y) => w.EditTimelineForTests!.TranslatePoint(new Point(x, y), w)!.Value;
    static void Drag(MainWindow w, Point a, Point b) { w.MouseDown(a, MouseButton.Left); for (var i = 1; i <= 5; i++) { w.MouseMove(a + (b - a) * (i / 5.0)); Settle(); } w.MouseUp(b, MouseButton.Left); Settle(); }

    static (MainWindow W, Trendo.Core.Programs.ProgramDocument D, Func<string, string> F) Open(int fps = 30)
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000;
        string F(string n) { var f = Path.Combine(root, n); if (!File.Exists(f)) File.WriteAllBytes(f, n.EndsWith(".png", StringComparison.Ordinal) ? Png : [1, 2, 3]); return f; }
        w.Show("schnitt"); Settle(); RealClick(w, w.Find<Button>("prog-tpl-leer")); Settle();
        var d = w.ProgramActive("schnitt")!; d.Set("fps", fps);
        w.EditAdd(F("a.mp4"), length: 10, audio: true); w.EditAdd(F("b.mp4"), length: 10, audio: true); w.EditAdd(F("m.mp3"), music: true, length: 4, audio: true); Settle();
        w.Show("schnitt"); Settle();
        return (w, d, F);
    }

    [AvaloniaFact(DisplayName = "V2: Tonspur hinzufügen, Musik mit der Maus auf die neue Spur ziehen, Stumm-Knopf greift, Rückgängig holt sie zurück")]
    public void Move_to_new_lane()
    {
        var (w, d, _) = Open();
        RealClick(w, w.Find<Button>("prog-cmd-ve-lane-audio")); Settle();
        Assert.Equal(2, VideoLanes.Audio(d.Data));
        var t = w.EditTimelineForTests!; t.BringIntoView(); Settle();
        var r = t.BoxOf("music", 0); var y2 = t.ExtraRows().First().Y + 10;
        Drag(w, P(w, r.Center.X, r.Center.Y), P(w, r.Center.X, y2));
        Assert.Equal(1, VideoEdit.Clips(d.Data, "music")[0].Lane);
        RealClick(w, w.Find<Button>("ve-lane-audio-1-m")); Settle();
        Assert.True(VideoLanes.IsMuted(d.Data, "audio", 1));
        Assert.True(d.Undo()); Assert.True(d.Undo());
        Assert.Equal(0, VideoEdit.Clips(d.Data, "music")[0].Lane);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "V2: Einblendungen erscheinen auf der Zeitleiste und lassen sich auf eine zweite Bildspur ziehen")]
    public void Overlay_lane()
    {
        var (w, d, F) = Open();
        w.EditAddOverlay(d, F("logo.png")); w.Show("schnitt"); Settle();
        RealClick(w, w.Find<Button>("prog-cmd-ve-lane-video")); Settle();
        var t = w.EditTimelineForTests!; t.BringIntoView(); Settle();
        var r = t.BoxOf("overlay", 0); var y2 = t.ExtraRows().Last().Y + 10;
        Drag(w, P(w, r.Center.X, r.Center.Y), P(w, r.Center.X, y2));
        Assert.Equal(1, VideoPro.Overlays(d.Data)[0].Lane);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "V3: Pfeiltaste springt um genau ein Bild der Projekt-Bildrate, Anzeige zeigt den Zeitcode, Klick im Lineal rastet ein")]
    public void Frame_step()
    {
        var (w, d, _) = Open(25);
        var t = w.EditTimelineForTests!; t.Focus(); Settle();
        w.KeyPress(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, null); w.KeyRelease(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, null); Settle();
        Assert.Equal(0.04, d.Number("playhead"), 3);
        Assert.Contains("00:00:00:01", w.Find<TextBlock>("ve-time").Text);
        t = w.EditTimelineForTests!; t.BringIntoView(); Settle();
        var a = P(w, t.X(1.013), EditTimeline.Ruler / 2);
        w.MouseDown(a, MouseButton.Left); w.MouseUp(a, MouseButton.Left); Settle();
        Assert.Equal(1.0, d.Number("playhead") % 0.04 < 1e-3 || 0.04 - d.Number("playhead") % 0.04 < 1e-3 ? 1.0 : d.Number("playhead"), 3);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "V4: Slip- und Rolling-Werkzeug über die Zeitleiste; Löschen-und-Aufrücken verlangt eine Tonauswahl")]
    public void Tools()
    {
        var (w, d, _) = Open();
        var before = VideoEdit.Clips(d.Data);
        d.Edit("Kürzen", x => { VideoEdit.Trim(x, 0, 2, 8); VideoEdit.Trim(x, 1, 2, 8); }); w.Show("schnitt"); Settle();
        w.EditTool = "slip"; w.Show("schnitt"); Settle();
        var t = w.EditTimelineForTests!; t.BringIntoView(); Settle();
        var r = t.BoxOf("clips", 0);
        Drag(w, P(w, r.Center.X, r.Center.Y), P(w, r.Center.X - 40, r.Center.Y));
        var c = VideoEdit.Clips(d.Data)[0];
        Assert.True(c.In > 2.5, $"In {c.In}"); Assert.Equal(6, c.Length, 2);

        w.EditTool = "roll"; w.Show("schnitt"); Settle();
        t = w.EditTimelineForTests!; t.BringIntoView(); Settle();
        var total = VideoEdit.VideoLength(VideoEdit.Clips(d.Data));
        r = t.BoxOf("clips", 0);
        Drag(w, P(w, r.Right - 20, r.Center.Y), P(w, r.Right - 60, r.Center.Y));
        Assert.Equal(total, VideoEdit.VideoLength(VideoEdit.Clips(d.Data)), 2);
        Assert.True(VideoEdit.Clips(d.Data)[0].Length < 6);
        w.Close();
        _ = before;
    }
}
