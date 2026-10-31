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

/// <summary>CORE-08 / Masterprompt 131: Videoschnitt als Programm – Zeitleiste mit echter Maus und Tastatur, Rückgängig, Export, Bedienstufe.</summary>
public class VideoSchnitt_Ui
{
    static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");
    static EditTimeline Tl(MainWindow w) { Settle(); return w.EditTimelineForTests!; }
    static Point P(MainWindow w, double x, double y) { var t = Tl(w); return t.TranslatePoint(new Point(x, y), w)!.Value; }
    static void Drag(MainWindow w, Point a, Point b) { w.MouseDown(a, MouseButton.Left); for (var i = 1; i <= 5; i++) { w.MouseMove(a + (b - a) * (i / 5.0)); Settle(); } w.MouseUp(b, MouseButton.Left); Settle(); }
    static void Key1(MainWindow w, Key k, PhysicalKey ph, RawInputModifiers m = RawInputModifiers.None) { w.KeyPress(k, m, ph, null); w.KeyRelease(k, m, ph, null); Settle(); }

    [AvaloniaFact(DisplayName = "Videoschnitt UI: Medien auf die Spuren, Stück ziehen ordnet um, Kante ziehen kürzt, Lineal setzt Abspielkopf, S teilt, Strg+Z, Entf, Export, Einfach-Stufe")]
    public void Timeline_edit()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000; Directory.CreateDirectory(root);
        string F(string n, byte[]? b = null) { var f = Path.Combine(root, n); File.WriteAllBytes(f, b ?? [1, 2, 3]); return f; }
        w.Show("schnitt"); Settle(); RealClick(w, V<Button>(w, "prog-tpl-leer"));
        w.EditAdd(F("titel.png", Png)); w.EditAdd(F("urlaub.mp4"), length: 10, audio: true); w.EditAdd(F("ende.png", Png)); w.EditAdd(F("musik.mp3"), music: true, length: 30, audio: true);
        w.Show("schnitt"); var d = w.ProgramActive("schnitt")!;
        Assert.Equal(new[] { "titel.png", "urlaub.mp4", "ende.png" }, VideoEdit.Clips(d.Data).Select(c => Path.GetFileName(c.File)));
        Assert.Equal(30, VideoEdit.Duration(d.Data), 3); // Bildspur 20 s, Musik 30 s
        // Titelbild hinter das Video ziehen
        var t = Tl(w); var box = t.BoxOf("clips", 0);
        Drag(w, P(w, box.Center.X, box.Center.Y), P(w, t.BoxOf("clips", 1).Right - 10, box.Center.Y));
        d = w.ProgramActive("schnitt")!; Assert.Equal(new[] { "urlaub.mp4", "titel.png", "ende.png" }, VideoEdit.Clips(d.Data).Select(c => Path.GetFileName(c.File)));
        // rechte Kante des Videos 1 s nach links (40 px je Sekunde)
        t = Tl(w); box = t.BoxOf("clips", 0);
        Drag(w, P(w, box.Right - 3, box.Center.Y), P(w, box.Right - 3 - 40, box.Center.Y));
        Assert.Equal(9, VideoEdit.Clips(w.ProgramActive("schnitt")!.Data)[0].Out, 1);
        // Abspielkopf per Klick ins Lineal auf 3 s, S teilt das Video
        var ruler = P(w, Tl(w).X(3), 10); w.MouseDown(ruler, MouseButton.Left); w.MouseUp(ruler, MouseButton.Left); Settle();
        Assert.Equal(3, w.ProgramActive("schnitt")!.Number("playhead"), 1);
        Tl(w).Focus(); Key1(w, Key.S, PhysicalKey.S);
        var clips = VideoEdit.Clips(w.ProgramActive("schnitt")!.Data); Assert.Equal(4, clips.Count); Assert.Equal((0, 3), ((int)clips[0].In, (int)Math.Round(clips[0].Out)));
        // Strg+Z nimmt das Teilen zurück
        Tl(w).Focus(); Key1(w, Key.Z, PhysicalKey.Z, RawInputModifiers.Control);
        Assert.Equal(3, VideoEdit.Clips(w.ProgramActive("schnitt")!.Data).Count);
        // Stück anklicken und mit Entf löschen
        t = Tl(w); box = t.BoxOf("clips", 2); var c2 = P(w, box.Center.X, box.Center.Y); w.MouseDown(c2, MouseButton.Left); w.MouseUp(c2, MouseButton.Left); Settle();
        Assert.Equal(("clips", 2), w.EditSelected!.Value);
        Tl(w).Focus(); Key1(w, Key.Delete, PhysicalKey.Delete);
        Assert.Equal(2, VideoEdit.Clips(w.ProgramActive("schnitt")!.Data).Count);
        // Musik verschieben: 2 s nach rechts
        t = Tl(w); box = t.BoxOf("music", 0);
        Drag(w, P(w, box.X + 60, box.Center.Y), P(w, box.X + 60 + 80, box.Center.Y));
        Assert.Equal(2, VideoEdit.Clips(w.ProgramActive("schnitt")!.Data, "music")[0].Start, 1);
        // Export (FFmpeg ersetzt) – Aufruf mit allen Stücken
        string? args = null; w.ExportOverride = (p, f) => { args = string.Join(" ", VideoEdit.ExportArgs(p, f)); File.WriteAllText(f, "mp4"); return f; };
        var ex = V<Button>(w, "prog-export-mp4"); ex.BringIntoView(); Settle(); RealClick(w, ex);
        Assert.Contains("Exportiert", w.ProgramMessage("schnitt")); Assert.Contains("concat=n=2", args); Assert.Contains("adelay=2000|2000", args);
        // Einfach: Zoom-Gruppe weg, Schneiden bleibt
        w.ProgramSetLevel(Trendo.Core.Programs.UserLevel.Einfach, "schnitt"); Settle();
        Assert.Null(w.TryFind<Button>("prog-cmd-ve-zoom-in")); Assert.NotNull(w.TryFind<Button>("prog-cmd-ve-split"));
        w.ProgramSetLevel(Trendo.Core.Programs.UserLevel.Erweitert);
    }

    [AvaloniaFact(DisplayName = "Videoschnitt UI: falsche Dateien und leere Bildspur geben klare Meldungen")]
    public void Errors_are_clear()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        w.Show("schnitt"); Settle(); RealClick(w, V<Button>(w, "prog-tpl-leer"));
        Assert.Throws<FileNotFoundException>(() => w.EditAdd(Path.Combine(root, "fehlt.mp4"), length: 3));
        var txt = Path.Combine(root, "notiz.txt"); File.WriteAllText(txt, "x"); Assert.Throws<InvalidOperationException>(() => w.EditAdd(txt));
        w.ExportOverride = (p, f) => { VideoEdit.ExportArgs(p, f); return f; };
        var ex = V<Button>(w, "prog-export-mp4"); ex.BringIntoView(); Settle(); RealClick(w, ex);
        Assert.Contains("Bildspur ist leer", w.ProgramMessage("schnitt"));
    }
}
