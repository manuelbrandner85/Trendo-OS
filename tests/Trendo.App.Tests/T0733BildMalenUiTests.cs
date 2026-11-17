using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SkiaSharp;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Media;
using Trendo.Core.Programs;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0733 Bildbearbeitung + Künstlerstudio: WebP-Export/-Import (SkiaSharp), TIFF-Import (über das echte FFmpeg), Werkzeug-Tasten mit Zoom (Z),
/// Auswahl im Künstlerstudio (Rechteck/Lasso) verschieben und skalieren mit Rückgängig. Alles über echte Maus- und Tastenereignisse des Headless-Fensters;
/// die Fremdprüfung der WebP-Datei macht das echte ffprobe.</summary>
public class T0733_BildMalen_Ui
{
    static void Pump() { for (var i = 0; i < 8; i++) Dispatcher.UIThread.RunJobs(); Settle(); }
    static T V<T>(MainWindow w, string name) where T : Control { Pump(); return w.GetVisualDescendants().OfType<T>().Last(c => c.Name == name && c.IsEffectivelyVisible && c.Bounds.Width > 0); }
    static void Click(MainWindow w, string name) { var b = V<Button>(w, name); b.BringIntoView(); Pump(); RealClick(w, V<Button>(w, name)); Pump(); }
    static void Key_(MainWindow w, Key k, PhysicalKey p) { w.KeyPress(k, RawInputModifiers.None, p, null); Pump(); w.KeyRelease(k, RawInputModifiers.None, p, null); Pump(); }
    static void Drag(MainWindow w, Point a, Point b, int steps = 8) { w.MouseDown(a, MouseButton.Left); for (var i = 1; i <= steps; i++) { w.MouseMove(a + (b - a) * (i / (double)steps)); Pump(); } w.MouseUp(b, MouseButton.Left); Pump(); }
    static Menu Bar(MainWindow w) => w.ActiveMenuBar() ?? throw new Xunit.Sdk.XunitException("keine Menüleiste auf " + w.CurrentView);
    static List<MenuTop> Model(MainWindow w) => (List<MenuTop>)Bar(w).Tag!;
    static MenuCmd Cmd(MainWindow w, string id) => Model(w).SelectMany(m => ProgramMenu.Flat(m.Items)).First(c => c.Id == id);
    static Point ImgP(MainWindow w, double x, double y) { Pump(); var c = w.ImgCanvasForTests!; return c.TranslatePoint(c.ToScreen(new Point(x, y)), w)!.Value; }
    static Point ArtP(MainWindow w, double x, double y) { Pump(); var c = w.ArtCanvasForTests!; return c.TranslatePoint(c.ToScreen(x, y), w)!.Value; }
    /// <summary>Bild der Seite nur auf Wunsch (Umgebungsvariable T0733_SHOT = Ordner) – zum Nachprüfen der Oberfläche.</summary>
    static void Shot(MainWindow w, string name) { if (Environment.GetEnvironmentVariable("T0733_SHOT") is { Length: > 0 } dir) { Directory.CreateDirectory(dir); w.CaptureRenderedFrame()?.Save(Path.Combine(dir, name)); } }
    /// <summary>Wartet, bis die TIFF-Umwandlung im Hintergrund fertig ist und ihre Rückmeldung auf dem Oberflächen-Faden lief (höchstens 60 s).</summary>
    static void WaitTiff(MainWindow w) { for (var i = 0; i < 1200 && w.ImgTiffBusy; i++) { Thread.Sleep(50); Pump(); } Assert.False(w.ImgTiffBusy, "TIFF-Umwandlung nach 60 s nicht fertig"); Pump(); }
    static SKColor Px(MainWindow w, int x, int y) { using var b = ImageRender.Render(w.ProgramActive("bild")!.Data); return b.GetPixel(x, y); }

    [AvaloniaFact(DisplayName = "T-0733 Bild: TIFF (echtes FFmpeg) als Ebene öffnen, malen, als WebP exportieren – ffprobe liest webp 400×200, WebP wieder als Ebene")]
    public void Tiff_in_webp_out()
    {
        var ff = Cli.Find("ffmpeg"); var probe = Cli.Find("ffprobe");
        if (ff is null || probe is null) { Assert.Skip("BLOCKIERT – FFmpeg oder ffprobe fehlt auf diesem PC (TIFF-Import und Fremdprüfung brauchen sie)."); return; }
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000; Directory.CreateDirectory(root);
        try
        {
            // Fremddatei: PNG 400 × 200 (links rot, rechts blau) → TIFF mit dem echten FFmpeg
            var png = Path.Combine(root, "quelle.png");
            using (var bmp = new SKBitmap(400, 200)) { using (var c = new SKCanvas(bmp)) { c.Clear(SKColors.Red); c.DrawRect(200, 0, 200, 200, new SKPaint { Color = SKColors.Blue }); } using var img = SKImage.FromBitmap(bmp); using var data = img.Encode(SKEncodedImageFormat.Png, 100); File.WriteAllBytes(png, data.ToArray()); }
            var tif = Path.Combine(root, "scan.tif");
            Assert.True(Cli.Run(ff, ["-v", "error", "-y", "-i", png, tif]).Ok, "FFmpeg konnte das TIFF nicht schreiben");
            Assert.Null(SKBitmap.Decode(tif)); // SkiaSharp kann TIFF nicht – darum der Weg über FFmpeg

            w.ProgramSetLevel(UserLevel.Profi);
            w.Show("bild"); Pump(); RealClick(w, V<Button>(w, "prog-tpl-leer")); Pump();
            V<TextBox>(w, "img-file").Text = tif; Click(w, "img-add");
            WaitTiff(w);
            // Gegenprüfung T-0733: FFmpeg lief nicht auf dem Oberflächen-Faden
            Assert.NotEqual(0, w.ImgTiffThreadForTests); Assert.NotEqual(Environment.CurrentManagedThreadId, w.ImgTiffThreadForTests);
            var d = w.ProgramActive("bild")!;
            Assert.Equal((400, 200), ((int)d.Number("width"), (int)d.Number("height")));
            Assert.Contains("TIFF", w.ProgramMessage("bild"));
            var layerFile = ImageDoc.S(ImageDoc.Layers(d.Data).OfType<System.Text.Json.Nodes.JsonObject>().Single()["file"]);
            Assert.EndsWith(".png", layerFile); Assert.True(File.Exists(layerFile));
            Assert.Equal(SKColors.Red, Px(w, 50, 100)); Assert.Equal(SKColors.Blue, Px(w, 300, 100));

            // Malen mit der Maus (Pinsel B), dann Export über den Knopf „WebP“
            Click(w, "prog-cmd-img-tool-brush"); Click(w, "img-color-22c55e");
            Drag(w, ImgP(w, 20, 100), ImgP(w, 180, 100));
            Click(w, "prog-export-webp");
            var webp = w.ProgramMessage("bild")["Exportiert: ".Length..];
            Assert.EndsWith(".webp", webp);
            using (var codec = SKCodec.Create(webp)) Assert.Equal(SKEncodedImageFormat.Webp, codec.EncodedFormat);
            using (var back = SKBitmap.Decode(webp)) { Assert.Equal((400, 200), (back.Width, back.Height)); var g = back.GetPixel(100, 100); Assert.True(g.Green > 150 && g.Red < 100, $"kein Grün: {g}"); }
            // Fremdwerkzeug: ffprobe erkennt WebP mit 400 × 200
            var pr = Cli.Run(probe, ["-v", "error", "-show_entries", "stream=codec_name,width,height", "-of", "csv=p=0", webp]);
            Assert.True(pr.Ok, pr.Err); Assert.Equal("webp,400,200", pr.Out.Trim());
            // WebP wieder als Ebene
            var before = ImageDoc.Layers(w.ProgramActive("bild")!.Data).Count;
            w.ImgAddImage(webp); Assert.Equal(before + 1, ImageDoc.Layers(w.ProgramActive("bild")!.Data).Count);

            // Gegenprüfung T-0733: zweites TIFF (anderer Inhalt) – die Umwandlung läuft im Hintergrund, „wird umgewandelt …“ steht sofort da,
            // ein zweiter Start während der Umwandlung wird abgelehnt (kein Doppelstart), danach genau eine neue Ebene
            var tif2 = Path.Combine(root, "scan2.tif");
            Assert.True(Cli.Run(ff, ["-v", "error", "-y", "-f", "lavfi", "-i", "color=c=yellow:s=320x160", "-frames:v", "1", tif2]).Ok, "FFmpeg konnte das zweite TIFF nicht schreiben");
            var n0 = ImageDoc.Layers(w.ProgramActive("bild")!.Data).Count;
            w.ImgAddImage(tif2); // kein Pumpen: die Rückmeldung der Umwandlung kann noch nicht da sein
            Assert.True(w.ImgTiffBusy); Assert.Contains("wird umgewandelt", w.ProgramMessage("bild"));
            Assert.Equal(n0, ImageDoc.Layers(w.ProgramActive("bild")!.Data).Count);
            var twice = Assert.Throws<InvalidOperationException>(() => w.ImgAddImage(tif2)); Assert.Contains("gerade umgewandelt", twice.Message);
            WaitTiff(w);
            Assert.Equal(n0 + 1, ImageDoc.Layers(w.ProgramActive("bild")!.Data).Count);
            var last = ImageDoc.S(ImageDoc.Layers(w.ProgramActive("bild")!.Data).OfType<System.Text.Json.Nodes.JsonObject>().Last()["file"]);
            Assert.EndsWith(".png", last); Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(last)!, "*.tmp.png")); // keine halbe Zwischendatei liegen geblieben
            using (var y = SKBitmap.Decode(last)) { Assert.Equal((320, 160), (y.Width, y.Height)); var c = y.GetPixel(10, 10); Assert.True(c.Red > 200 && c.Green > 200 && c.Blue < 80, $"kein Gelb: {c}"); }

            // Sammelstand 0.9.104: Wird das Bild während der Umwandlung geschlossen, landet die Ebene in einem neuen Bild – nie still in einem anderen offenen
            var other = w.ProgramNew("bild", "leer"); var target = w.ProgramActive("bild")!; Assert.Same(other, target);
            var otherLayers = ImageDoc.Layers(other.Data).Count;
            var keep = w.ProgramDocs("bild").First(x => !ReferenceEquals(x, other)); // ein weiteres offenes Bild, das nicht Ziel ist
            var keepLayers = ImageDoc.Layers(keep.Data).Count;
            w.ImgAddImage(tif2); Assert.True(w.ImgTiffBusy);
            w.ProgramClose("bild", w.ProgramDocs("bild").IndexOf(other)); // Ziel schließen, während FFmpeg noch läuft
            WaitTiff(w);
            Assert.Equal(keepLayers, ImageDoc.Layers(keep.Data).Count); // das andere offene Bild bleibt unverändert
            var fresh = w.ProgramActive("bild")!; Assert.NotSame(keep, fresh); Assert.Single(ImageDoc.Layers(fresh.Data));
            Assert.Contains("schon geschlossen", w.ProgramMessage("bild"));
            if (Environment.GetEnvironmentVariable("T0733_BELEG") is { Length: > 0 } beleg)
            {
                Directory.CreateDirectory(beleg);
                File.Copy(webp, Path.Combine(beleg, "export.webp"), true); File.Copy(tif, Path.Combine(beleg, "quelle.tif"), true);
            }
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0733 Tasten: Bildbearbeitung V B E M T Z im Menü „Werkzeuge“ und im Tooltip, ohne Konflikt; Z + Klick zoomt an der Stelle, Alt+Klick zurück; Künstlerstudio M = Auswahl")]
    public void Tool_keys_and_zoom()
    {
        var (w, _, _) = Ui.Open(); w.Width = 1600; w.Height = 1000;
        try
        {
            w.ProgramSetLevel(UserLevel.Profi);
            while (w.ProgramDocs("bild").Count > 0) w.ProgramClose("bild", 0);
            w.Show("bild"); var d = w.ProgramNew("bild", "leer");
            d.Edit("Rechteck", x => ImageDoc.AddShape(x, "rect", 100, 100, 400, 300, "#E0475B")); // mit Inhalt – der erste Schritt „Foto öffnen“ liegt dann nicht über der Fläche
            w.Show("bild"); Pump(); w.UpdateLayout(); Pump();
            var steps = d.History.Count;
            foreach (var (tool, key) in new[] { ("move", "V"), ("brush", "B"), ("eraser", "E"), ("select", "M"), ("text", "T"), ("zoom", "Z") })
            {
                Assert.Equal(key, Cmd(w, "tool-img-tool-" + tool).Shortcut);
                Assert.Contains($"({key})", ToolTip.GetTip(V<Button>(w, "prog-cmd-img-tool-" + tool))?.ToString() ?? "");
            }
            Assert.Empty(ProgramMenu.Conflicts(Model(w)));
            V<Button>(w, "prog-undo").Focus(); Pump();
            Key_(w, Key.Z, PhysicalKey.Z); Assert.Equal("zoom", w.ImgTool);
            Key_(w, Key.M, PhysicalKey.M); Assert.Equal("select", w.ImgTool);
            Key_(w, Key.Z, PhysicalKey.Z); Assert.Equal("zoom", w.ImgTool);
            // Zoom: Klick auf (1400, 300) – die Stelle bleibt unter der Maus, Maßstab steigt eine Stufe
            var fit = w.ImgCanvasForTests!.CurrentScale;
            var at = ImgP(w, 1400, 300); w.MouseDown(at, MouseButton.Left); w.MouseUp(at, MouseButton.Left); Pump();
            Assert.True(w.ImgZoom > fit, $"kein Zoom: {w.ImgZoom} ≤ {fit} · Werkzeug der Fläche {w.ImgCanvasForTests!.Tool} · Meldung {w.ProgramMessage("bild")} · bei {at} Fläche {w.ImgCanvasForTests.Bounds}");
            var again = ImgP(w, 1400, 300); Assert.True(Math.Abs(again.X - at.X) < 1.5 && Math.Abs(again.Y - at.Y) < 1.5, $"Stelle gewandert: {at} → {again}");
            var z1 = w.ImgZoom;
            Shot(w, "bild-zoom.png");
            at = ImgP(w, 1400, 300); w.MouseDown(at, MouseButton.Left); w.MouseUp(at, MouseButton.Left); Pump();
            Assert.True(w.ImgZoom > z1);
            at = ImgP(w, 1400, 300); w.MouseDown(at, MouseButton.Left, RawInputModifiers.Alt); w.MouseUp(at, MouseButton.Left, RawInputModifiers.Alt); Pump();
            Assert.Equal(z1, w.ImgZoom, 3);
            Assert.Equal(steps, d.History.Count); // Zoomen ist kein Bearbeitungsschritt
            Click(w, "prog-cmd-img-zoom-fit"); Assert.Equal(0, w.ImgZoom);

            // Künstlerstudio: M wählt die Auswahl, ebenfalls ohne Konflikt
            while (w.ProgramDocs(MainWindow.ArtKey).Count > 0) w.ProgramClose(MainWindow.ArtKey, 0);
            w.Show(MainWindow.ArtKey); w.ProgramNew(MainWindow.ArtKey, "quer"); w.Show(MainWindow.ArtKey); Pump(); w.UpdateLayout(); Pump();
            Assert.Equal("M", Cmd(w, "tool-art-tool-select").Shortcut);
            Assert.Empty(ProgramMenu.Conflicts(Model(w)));
            V<Button>(w, "prog-undo").Focus(); Pump();
            Key_(w, Key.M, PhysicalKey.M); Assert.Equal("select", w.ArtMode);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0733 Künstlerstudio: Rechteck aufziehen, Inhalt mit der Maus verschieben und an der Ecke auf 200 % ziehen, Rückgängig je Schritt; Lasso und Skalieren per Zahl")]
    public void Art_select_move_scale()
    {
        var (w, _, _) = Ui.Open(); w.Width = 1600; w.Height = 1000;
        try
        {
            w.ProgramSetLevel(UserLevel.Profi);
            while (w.ProgramDocs(MainWindow.ArtKey).Count > 0) w.ProgramClose(MainWindow.ArtKey, 0);
            w.Show(MainWindow.ArtKey); Pump(); RealClick(w, V<Button>(w, "prog-tpl-quer")); Pump();
            var d = w.ProgramActive(MainWindow.ArtKey)!;
            // schwarzes Quadrat 100 × 100 bei (400, 300) malen
            w.ArtBrush = "tusche"; w.ArtSize = 30; w.ArtColor = "#000000"; w.ArtSmooth = 0;
            for (var y = 310; y <= 390; y += 10) w.ArtPaint(d, [(410, y, 1), (490, y, 1)]);
            (byte R, byte A) At(int x, int y) { var c = w.ArtModel(d).Comp.At(x, y); return (c.R, c.A); }
            Assert.True(At(450, 350).R < 40);
            var strokes = d.History.Count;

            V<Button>(w, "prog-undo").Focus(); Pump(); Key_(w, Key.M, PhysicalKey.M); Assert.Equal("select", w.ArtMode); // Taste M wie in Krita/Photoshop
            Drag(w, ArtP(w, 380, 280), ArtP(w, 520, 420));
            var sel = w.ArtSelection(d); Assert.NotNull(sel); Assert.Equal(4, sel!.Count);
            var sb = Trendo.Core.Kreativ.PaintSelection.Bounds(sel); Assert.True(Math.Abs(sb.X - 380) < 3 && Math.Abs(sb.W - 140) < 3, $"Auswahl {sb}, Maßstab {w.ArtCanvasForTests!.Scale}");
            Assert.Equal(strokes, d.History.Count); // Auswahl selbst ist kein Schritt
            // Inhalt verschieben: hineinklicken und ziehen (+600, +200)
            Drag(w, ArtP(w, 450, 350), ArtP(w, 1050, 550));
            Assert.True(d.UndoLabel == "Auswahl verschieben", $"Verlauf {string.Join(" | ", d.History.Take(4))} · Meldung {w.ProgramMessage(MainWindow.ArtKey)} · Auswahl jetzt {Trendo.Core.Kreativ.PaintSelection.Bounds(w.ArtSelection(d)!)} · Maßstab {w.ArtCanvasForTests!.Scale} · Fläche {w.ArtCanvasForTests.Bounds}");
            Assert.True(At(450, 350).R > 240, "alte Stelle nicht frei"); Assert.True(At(1050, 550).R < 40, "Inhalt nicht am Ziel");
            var b = Trendo.Core.Kreativ.PaintSelection.Bounds(w.ArtSelection(d)!);
            Assert.InRange(b.X, 975, 985); Assert.InRange(b.Y, 475, 485);
            // Ecke unten rechts ziehen: 140 × 140 → 280 × 280 (Anker oben links)
            Drag(w, ArtP(w, b.X + b.W, b.Y + b.H), ArtP(w, b.X + 2 * b.W, b.Y + 2 * b.H));
            Assert.Equal("Auswahl skalieren", d.UndoLabel);
            var b2 = Trendo.Core.Kreativ.PaintSelection.Bounds(w.ArtSelection(d)!);
            Assert.InRange(b2.W, 270, 290); Assert.InRange(b2.H, 270, 290);
            Shot(w, "kuenstler-auswahl.png");
            // Quadrat lag 20..120 px innerhalb der Auswahl → jetzt 40..240
            Assert.True(At((int)(b2.X + 140), (int)(b2.Y + 140)).R < 40); Assert.True(At((int)(b2.X + 230), (int)(b2.Y + 230)).R < 40);
            Assert.True(At((int)(b2.X + 260), (int)(b2.Y + 140)).R > 240);
            // Rückgängig: erst Skalieren, dann Verschieben – Auswahl wandert mit zurück
            RealClick(w, V<Button>(w, "prog-undo")); Pump();
            Assert.Equal("Auswahl verschieben", d.UndoLabel); Assert.True(At((int)(b2.X + 230), (int)(b2.Y + 230)).R > 240);
            RealClick(w, V<Button>(w, "prog-undo")); Pump();
            Assert.True(At(450, 350).R < 40); Assert.True(At(1050, 550).R > 240);
            Assert.InRange(Trendo.Core.Kreativ.PaintSelection.Bounds(w.ArtSelection(d)!).X, 375, 385);
            // Skalieren per Zahl (50 %) über die Optionsleiste
            V<TextBox>(w, "art-sel-scale").Text = "50"; Click(w, "art-sel-scale-ok");
            Assert.Equal("Auswahl skalieren", d.UndoLabel); Assert.True(At(470, 370).R > 240); Assert.True(At(415, 330).R < 40);
            // Lasso: Dreieck umfahren, Inhalt löschen
            Click(w, "art-sel-mode-lasso"); Assert.Equal("lasso", w.ArtSelMode);
            var p0 = ArtP(w, 1300, 200); var p1 = ArtP(w, 1600, 200); var p2 = ArtP(w, 1300, 500);
            w.MouseDown(p0, MouseButton.Left);
            for (var i = 1; i <= 6; i++) { w.MouseMove(p0 + (p1 - p0) * (i / 6.0)); Pump(); }
            for (var i = 1; i <= 6; i++) { w.MouseMove(p1 + (p2 - p1) * (i / 6.0)); Pump(); }
            w.MouseUp(p2, MouseButton.Left); Pump();
            Assert.True(w.ArtSelection(d)!.Count > 4);
            Click(w, "art-sel-none"); Assert.Null(w.ArtSelection(d));
        }
        finally { w.Close(); }
    }

    /// <summary>Schwarzes Quadrat 100 × 100 bei (400, 300) und ein Strich außerhalb bei (700, 600) – neues Querformat-Bild im Künstlerstudio, Werkzeug „Auswahl“ per Taste M.</summary>
    static ProgramDocument ArtWithSquare(MainWindow w)
    {
        w.ProgramSetLevel(UserLevel.Profi);
        while (w.ProgramDocs(MainWindow.ArtKey).Count > 0) w.ProgramClose(MainWindow.ArtKey, 0);
        w.Show(MainWindow.ArtKey); Pump(); RealClick(w, V<Button>(w, "prog-tpl-quer")); Pump();
        var d = w.ProgramActive(MainWindow.ArtKey)!;
        w.ArtBrush = "tusche"; w.ArtSize = 30; w.ArtColor = "#000000"; w.ArtSmooth = 0;
        for (var y = 310; y <= 390; y += 10) w.ArtPaint(d, [(410, y, 1), (490, y, 1)]);
        w.ArtPaint(d, [(680, 600, 1), (720, 600, 1)]);
        w.Show(MainWindow.ArtKey); Pump();
        V<Button>(w, "prog-undo").Focus(); Pump(); Key_(w, Key.M, PhysicalKey.M); Assert.Equal("select", w.ArtMode);
        return d;
    }
    /// <summary>Anzahl dunkler Bildpunkte (Rot &lt; 60) im Gesamtbild innerhalb eines Rechtecks.</summary>
    static int Dark(MainWindow w, ProgramDocument d, int x0, int y0, int x1, int y1)
    {
        var comp = w.ArtModel(d).Comp; var n = 0;
        for (var y = y0; y < y1; y++) for (var x = x0; x < x1; x++) if (comp.At(x, y).R < 60) n++;
        return n;
    }

    [AvaloniaFact(DisplayName = "T-0733 Gegenprüfung: Künstlerstudio – Lasso umfahren und Inhalt per Maus verschieben, „Inhalt löschen“ per Klick; echte Pixel vorher/nachher, Rückgängig je Schritt")]
    public void Art_lasso_move_and_clear_by_click()
    {
        var (w, _, _) = Ui.Open(); w.Width = 1600; w.Height = 1000;
        try
        {
            var d = ArtWithSquare(w);
            var square = Dark(w, d, 395, 295, 506, 406); var stroke = Dark(w, d, 660, 580, 740, 620);
            Assert.True(square > 9000, $"Quadrat zu klein: {square}"); Assert.True(stroke > 500, $"Strich fehlt: {stroke}");
            Assert.Equal(0, Dark(w, d, 795, 295, 906, 406));

            // Lasso: Achteck um das Quadrat herum mit gedrückter Maustaste umfahren
            Click(w, "art-sel-mode-lasso"); Assert.Equal("lasso", w.ArtSelMode);
            (double X, double Y)[] ring = [(370, 300), (400, 265), (500, 265), (535, 300), (535, 400), (500, 435), (400, 435), (370, 400)];
            var start = ArtP(w, ring[0].X, ring[0].Y); w.MouseDown(start, MouseButton.Left);
            for (var i = 1; i <= ring.Length; i++)
            {
                var (ax, ay) = ring[i - 1]; var (bx, by) = ring[i % ring.Length];
                for (var k = 1; k <= 3; k++) { w.MouseMove(ArtP(w, ax + (bx - ax) * k / 3.0, ay + (by - ay) * k / 3.0)); Pump(); }
            }
            w.MouseUp(start, MouseButton.Left); Pump();
            var sel = w.ArtSelection(d); Assert.NotNull(sel); Assert.True(sel!.Count > 8, $"Lasso hat nur {sel.Count} Punkte");
            var steps = d.History.Count;

            // Inhalt der Lasso-Auswahl hineinklicken und um +400 px nach rechts ziehen
            Drag(w, ArtP(w, 450, 350), ArtP(w, 850, 350));
            Assert.Equal("Auswahl verschieben", d.UndoLabel); Assert.Equal(steps + 1, d.History.Count);
            Assert.True(Dark(w, d, 395, 295, 506, 406) < 50, "alte Stelle nicht frei");
            Assert.InRange(Dark(w, d, 795, 295, 906, 406), square - 300, square + 300); // dasselbe Quadrat am Ziel
            Assert.Equal(stroke, Dark(w, d, 660, 580, 740, 620)); // außerhalb der Auswahl unverändert
            Assert.InRange(PaintSelBoundsX(w, d), 765, 775); // Auswahl wandert mit

            // Rückgängig per Knopf: Inhalt und Auswahl zurück
            RealClick(w, V<Button>(w, "prog-undo")); Pump();
            Assert.Equal(square, Dark(w, d, 395, 295, 506, 406)); Assert.Equal(0, Dark(w, d, 795, 295, 906, 406));
            Assert.InRange(PaintSelBoundsX(w, d), 365, 375);

            // „Inhalt löschen“ per Klick in der Optionsleiste
            Click(w, "art-sel-clear");
            Assert.Equal("Auswahl löschen", d.UndoLabel);
            Assert.Equal(0, Dark(w, d, 395, 295, 506, 406));
            Assert.Equal(stroke, Dark(w, d, 660, 580, 740, 620));
            var layer = w.ArtModel(d).Doc.Layers.Single(l => l.Id == d.Text("active"));
            Assert.Equal(0, layer.Px.At(450, 350).A); // wirklich durchsichtig, nicht weiß übermalt
            RealClick(w, V<Button>(w, "prog-undo")); Pump();
            Assert.Equal(square, Dark(w, d, 395, 295, 506, 406));
        }
        finally { w.Close(); }
    }

    static double PaintSelBoundsX(MainWindow w, ProgramDocument d) => Trendo.Core.Kreativ.PaintSelection.Bounds(w.ArtSelection(d)!).X;

    [AvaloniaFact(DisplayName = "T-0733 Gegenprüfung: geht die Mauserfassung mitten im Verschieben der Auswahl verloren, ist der Zug abgebrochen – späteres Loslassen verschiebt nichts")]
    public void Art_capture_lost_cancels_selection_drag()
    {
        var (w, _, _) = Ui.Open(); w.Width = 1600; w.Height = 1000;
        try
        {
            var d = ArtWithSquare(w);
            Drag(w, ArtP(w, 380, 280), ArtP(w, 520, 420)); Assert.NotNull(w.ArtSelection(d));
            var steps = d.History.Count; var square = Dark(w, d, 395, 295, 506, 406);
            // Zeiger beim Drücken merken, dann die Erfassung entziehen (wie bei einem Fenster, das sich davorschiebt)
            IPointer? ptr = null; var canvas = w.ArtCanvasForTests!;
            canvas.AddHandler(InputElement.PointerPressedEvent, (_, e) => ptr = e.Pointer, Avalonia.Interactivity.RoutingStrategies.Tunnel | Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
            var from = ArtP(w, 450, 350); w.MouseDown(from, MouseButton.Left); Pump();
            Assert.NotNull(ptr); Assert.Same(canvas, ptr!.Captured);
            Assert.True(canvas.SelDragging);
            ptr.Capture(null); Pump();
            Assert.False(canvas.SelDragging);
            // weiter ziehen und loslassen: kein Verschieben, kein neuer Schritt, Quadrat bleibt
            for (var i = 1; i <= 6; i++) { w.MouseMove(from + new Vector(60 * i, 30 * i)); Pump(); }
            w.MouseUp(from + new Vector(360, 180), MouseButton.Left); Pump();
            Assert.Equal(steps, d.History.Count);
            Assert.Equal(square, Dark(w, d, 395, 295, 506, 406));
            Assert.InRange(PaintSelBoundsX(w, d), 375, 385);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0733 Gegenprüfung: Zoom-Werkzeug in der Leiste – „Einfach“ mit Symbol und Wort, Hinweis mit Taste Z, gewählt = goldener Rand (nicht golden gefüllt, kein Blau)")]
    public void Zoom_tool_in_rail()
    {
        var (w, _, _) = Ui.Open(); w.Width = 1600; w.Height = 1000;
        try
        {
            w.ProgramSetLevel(UserLevel.Einfach);
            while (w.ProgramDocs("bild").Count > 0) w.ProgramClose("bild", 0);
            w.Show("bild"); w.ProgramNew("bild", "leer"); w.Show("bild"); Pump();
            var b = V<Button>(w, "prog-cmd-img-tool-zoom");
            var words = b.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
            Assert.Contains("⌕", words); Assert.Contains("Zoom", words); // Symbol und Wort
            Assert.StartsWith("Zoom (Z) – ", ToolTip.GetTip(b)?.ToString());
            Assert.False(b.BorderBrush is Avalonia.Media.ISolidColorBrush { Color: var c0 } && c0.ToHsv().S > 0.5 && c0.ToHsv().H is > 35 and < 55); // noch nicht gewählt: kein goldener Rand
            Click(w, "prog-cmd-img-tool-zoom"); Assert.Equal("zoom", w.ImgTool); // in „Einfach“ ist die Leiste lang – erst hinscrollen
            b = V<Button>(w, "prog-cmd-img-tool-zoom");
            var edge = Assert.IsAssignableFrom<Avalonia.Media.ISolidColorBrush>(b.BorderBrush).Color;
            var hsv = edge.ToHsv(); Assert.InRange(hsv.H, 35, 55); Assert.True(hsv.S > 0.5, $"kein kräftiges Gold: {edge}"); // Goldton (Akzent je Thema, z. B. #C9A227 oder #B38B22), kein Blau
            Assert.False(MainWindow.IsGold(b)); // golden gefüllt ist nur die Hauptaktion
            Shot(w, "bild-zoom-einfach.png");
        }
        finally { w.Close(); }
    }
}
