using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using SkiaSharp;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0205: Bilder bearbeiten, Zeitleiste mit Start/Ende, Ausschnitt, Clips aneinanderhängen.</summary>
public class MediaEditTests
{
    static string Png(int w = 200, int h = 100)
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-img-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        var p = Path.Combine(dir, "bild.png");
        using var bmp = new SKBitmap(w, h);
        using (var c = new SKCanvas(bmp)) c.Clear(new SKColor(100, 100, 100));
        using var img = SKImage.FromBitmap(bmp); using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        using (var fs = File.Create(p)) data.SaveTo(fs);
        return p;
    }

    [Fact(DisplayName = "T-0205: Zuschneiden + Drehen ergibt richtige Größe, Helligkeit ändert Pixel, Text wird gezeichnet, JPG/PNG")]
    public void Image_edits()
    {
        var src = Png();
        var o = ImageTools.Apply(src, new ImageTools.Edit(CropX: 10, CropY: 10, CropW: 50, CropH: 40, Rotate: 90, Format: ".png"));
        Assert.Equal((40, 50), ImageTools.Size(o));
        var hell = ImageTools.Apply(src, new ImageTools.Edit(Brightness: 50, Format: ".png"));
        using (var b = SKBitmap.Decode(hell)) Assert.True(b.GetPixel(100, 50).Red > 150);
        var mitText = ImageTools.Apply(src, new ImageTools.Edit(Text: "Hallo", TextSize: 40, Format: ".png"));
        using (var b = SKBitmap.Decode(mitText))
            Assert.Contains(Enumerable.Range(0, 200).SelectMany(x => Enumerable.Range(0, 100).Select(y => b.GetPixel(x, y))), p => p.Red > 200);
        var jpg = ImageTools.Apply(src, new ImageTools.Edit());
        Assert.EndsWith(".jpg", jpg);
        Assert.Throws<ArgumentException>(() => ImageTools.Apply(src, new ImageTools.Edit(CropX: 190, CropW: 50)));
    }

    [AvaloniaFact(DisplayName = "T-0205 (UI): Bild wählen → Bearbeiten-Bereich, Speichern zeigt Ergebnis")]
    public void Image_ui()
    {
        var src = Png();
        var (w, _, _) = Ui.Open();
        w.MediaPick(src); w.Show("kreativ-medien");
        Assert.NotNull(w.TryFind<StackPanel>("media-image"));
        w.Find<TextBox>("img-text").Text = "Urlaub";
        w.Find<Button>("img-save").Press();
        Assert.True(File.Exists(w.MediaLastOutput));
        Assert.NotNull(w.TryFind<StackPanel>("img-result"));
        Assert.NotNull(w.TryFind<Button>("media-play"));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "T-0205 (UI): Video → Länge, Zeitleiste mit Bildern, Ausschnitt Start–Ende, zwei Clips aneinanderhängen")]
    public void Video_timeline_cut_concat()
    {
        var src = W4_Core08_Media_Ui.Sample(); if (src is null) return; // ohne FFmpeg nicht prüfbar
        var (w, _, _) = Ui.Open();
        w.MediaPick(src); w.Show("kreativ-medien");
        Assert.Equal(6, w.Find<StackPanel>("media-timeline").Children.Count);
        w.MediaSetRange(0.5, 1.5);
        w.Find<Button>("media-cut").Press();
        var cut = w.MediaLastOutput!;
        Assert.True(File.Exists(cut));
        var d = MediaStudio.Duration(w.FfmpegPath!, cut);
        Assert.InRange(d ?? 0, 0.7, 1.4);
        w.Find<Button>("media-clip-add").Press();
        w.MediaPick(cut); w.Show("kreativ-medien");
        w.Find<Button>("media-clip-add").Press();
        Assert.Equal(2, w.MediaClips.Count);
        w.Find<Button>("media-concat").Press();
        Assert.Contains("zusammen", w.MediaLastOutput);
        Assert.InRange(MediaStudio.Duration(w.FfmpegPath!, w.MediaLastOutput!) ?? 0, 2.5, 3.5);
        w.Close();
    }

    [Fact(DisplayName = "T-0205: Clipliste maskiert Anführungszeichen, Vorschaubild-Aufruf ist korrekt")]
    public void Concat_list_and_frame_args()
    {
        var list = MediaStudio.ConcatList([@"C:\a\it's.mp4"]);
        Assert.Equal("file 'C:/a/it'\\''s.mp4'\n", list);
        var a = MediaStudio.FrameArgs("in.mp4", 2.5, "out.png");
        Assert.Equal(["-ss", "2.5"], a.SkipWhile(x => x != "-ss").Take(2));
        Assert.Contains("scale=160:-2", a);
    }
}
