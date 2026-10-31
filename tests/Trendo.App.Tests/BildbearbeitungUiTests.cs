using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using SkiaSharp;
using Trendo.App;
using Trendo.Core.Media;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>Masterprompt 131 (Beispiel Bildbearbeitung): Bild öffnen, auf der Fläche bearbeiten, Ebenen, Filter, speichern – mit Pixelprüfung.</summary>
public class Bildbearbeitung_Ui
{
    const string BK = "bild";
    static Point P(MainWindow w, double x, double y) { Settle(); var c = w.ImgCanvasForTests!; return c.TranslatePoint(c.ToScreen(new Point(x, y)), w)!.Value; }
    static void Drag(MainWindow w, Point a, Point b) { w.MouseDown(a, MouseButton.Left); for (var i = 1; i <= 6; i++) { w.MouseMove(a + (b - a) * (i / 6.0)); Settle(); } w.MouseUp(b, MouseButton.Left); Settle(); }
    static void Click(MainWindow w, string name) { var b = V<Button>(w, name); b.BringIntoView(); Settle(); RealClick(w, V<Button>(w, name)); }
    static SKColor Px(MainWindow w, int x, int y) { using var b = ImageRender.Render(w.ProgramActive(BK)!.Data); return b.GetPixel(x, y); }

    [AvaloniaFact(DisplayName = "Bildbearbeitung UI: Bild als Ebene, Pinsel malt (Pixel), Rechteck aufziehen, Text setzen, Ebene verschieben, Filter Schwarzweiß, Zuschneiden, Rückgängig, Export PNG")]
    public void Edit_image()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000; Directory.CreateDirectory(root);
        // Testbild 400 × 200: links rot, rechts blau
        var src = Path.Combine(root, "foto.png");
        using (var bmp = new SKBitmap(400, 200)) { using (var c = new SKCanvas(bmp)) { c.Clear(SKColors.Red); c.DrawRect(200, 0, 200, 200, new SKPaint { Color = SKColors.Blue }); } using var img = SKImage.FromBitmap(bmp); using var data = img.Encode(SKEncodedImageFormat.Png, 100); File.WriteAllBytes(src, data.ToArray()); }
        w.Show(BK); Settle(); RealClick(w, V<Button>(w, "prog-tpl-leer"));
        w.ImgAddImage(src); w.Show(BK); Settle();
        var d = w.ProgramActive(BK)!; Assert.Equal((400, 200), ((int)d.Number("width"), (int)d.Number("height"))); // erstes Bild bestimmt die Leinwand
        Assert.Equal(SKColors.Red, Px(w, 50, 100));
        // Pinsel: grüner Strich quer über die rote Hälfte
        Click(w, "prog-cmd-img-tool-brush"); Click(w, "img-color-22c55e");
        Drag(w, P(w, 20, 100), P(w, 180, 100));
        var g = Px(w, 100, 100); Assert.True(g.Green > 150 && g.Red < 100, $"kein Grün: {g}");
        Assert.Equal("Malen", w.ProgramActive(BK)!.UndoLabel);
        // Rechteck oben rechts aufziehen (gelb)
        Click(w, "prog-cmd-img-tool-rect"); Click(w, "img-color-facc15");
        Drag(w, P(w, 300, 20), P(w, 380, 80));
        var y = Px(w, 340, 50); Assert.True(y.Red > 200 && y.Green > 180 && y.Blue < 80, $"kein Gelb: {y}");
        // Text per Klick
        Click(w, "prog-cmd-img-tool-text"); var tp = P(w, 220, 120); w.MouseDown(tp, MouseButton.Left); w.MouseUp(tp, MouseButton.Left); Settle();
        Assert.Contains(ImageDoc.Layers(w.ProgramActive(BK)!.Data).OfType<System.Text.Json.Nodes.JsonObject>(), l => ImageDoc.S(l["kind"]) == "text");
        // Rechteck verschieben (anklicken + ziehen)
        Click(w, "prog-cmd-img-tool-move");
        Drag(w, P(w, 340, 50), P(w, 340, 150));
        var rect = ImageDoc.Layers(w.ProgramActive(BK)!.Data).OfType<System.Text.Json.Nodes.JsonObject>().First(l => ImageDoc.S(l["kind"]) == "shape");
        Assert.InRange(ImageDoc.N(rect["y"]), 115, 125);
        // Bild-Ebene auswählen, Schwarzweiß
        var imgIdx = 0; Click(w, $"img-layer-{imgIdx}");
        w.Find<CheckBox>("img-f-grayscale").IsChecked = true; Click(w, "img-apply");
        var gr = Px(w, 300, 190); Assert.True(Math.Abs(gr.Red - gr.Blue) < 6 && Math.Abs(gr.Green - gr.Blue) < 6, $"nicht grau: {gr}");
        // Zuschneiden: rechte Hälfte
        Click(w, "prog-cmd-img-tool-crop"); Drag(w, P(w, 200, 0), P(w, 400, 200));
        d = w.ProgramActive(BK)!; Assert.Equal((200, 200), ((int)d.Number("width"), (int)d.Number("height")));
        // Rückgängig: Zuschnitt zurück
        RealClick(w, V<Button>(w, "prog-undo")); Assert.Equal(400, (int)w.ProgramActive(BK)!.Number("width"));
        // Export PNG: echte Datei, gleiche Größe
        Click(w, "prog-export-png");
        var outFile = w.ProgramMessage(BK)["Exportiert: ".Length..];
        using var exported = SKBitmap.Decode(outFile); Assert.Equal((400, 200), (exported.Width, exported.Height));
    }

    [Fact(DisplayName = "Bild-Dokument: Ebenen-Reihenfolge, Treffer oberste Ebene, Zuschnitt verschiebt Ebenen und Striche, Filter-Grenzen, Farbmatrix Invertieren")]
    public void Doc_ops()
    {
        var d = ImageDoc.New(100, 100);
        var a = ImageDoc.AddShape(d, "rect", 10, 10, 50, 50); var b = ImageDoc.AddShape(d, "ellipse", 30, 30, 50, 50);
        Assert.Equal((string?)b["id"], ImageDoc.HitTest(d, 40, 40)); Assert.Equal((string?)a["id"], ImageDoc.HitTest(d, 15, 15)); Assert.Null(ImageDoc.HitTest(d, 95, 5));
        ImageDoc.Reorder(d, (string)b["id"]!, -1); Assert.Equal((string?)a["id"], ImageDoc.HitTest(d, 40, 40));
        var p = ImageDoc.AddPaint(d); ImageDoc.AddStroke(p, [(20, 20), (60, 60)], "#000000", 4, false);
        ImageDoc.Crop(d, 10, 10, 60, 60);
        Assert.Equal((60, 60), ((int)ImageDoc.N(d["width"]), (int)ImageDoc.N(d["height"])));
        Assert.Equal((0.0, 0.0), (ImageDoc.N(a["x"]), ImageDoc.N(a["y"]))); Assert.Equal(10, ImageDoc.N(((System.Text.Json.Nodes.JsonArray)((System.Text.Json.Nodes.JsonObject)((System.Text.Json.Nodes.JsonArray)p["strokes"]!)[0]!)["points"]!)[0]));
        ImageDoc.SetFilter(a, "blur", 999); Assert.Equal(50, ImageDoc.N(((System.Text.Json.Nodes.JsonObject)a["filter"]!)["blur"]));
        Assert.Throws<ArgumentException>(() => ImageDoc.SetFilter(a, "zauber", 1));
        Assert.Throws<InvalidOperationException>(() => ImageDoc.Crop(d, 0, 0, 2, 2));
        var inv = ImageDoc.ColorMatrix(new System.Text.Json.Nodes.JsonObject { ["invert"] = 1 });
        Assert.Equal((-1f, 255f), (inv[0], inv[4]));
    }
}
