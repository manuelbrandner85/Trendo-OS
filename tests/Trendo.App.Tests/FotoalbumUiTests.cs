using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using SkiaSharp;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// T-0302: Medien als Fotoalbum – Auswahl mit der Maus, Album und Favoriten, Vollbild mit Pfeiltasten, Papierkorb nur nach Ja, Doppelte.
public class Fotoalbum_Ui
{
    static void Png(string file, SKColor c, DateTime when)
    {
        using var bmp = new SKBitmap(64, 48); using (var cv = new SKCanvas(bmp)) { cv.Clear(c); cv.DrawCircle(20, 20, 10, new SKPaint { Color = SKColors.White }); }
        using (var fs = File.Create(file)) bmp.Encode(fs, SKEncodedImageFormat.Png, 90);
        File.SetLastWriteTime(file, when);
    }

    [AvaloniaFact(DisplayName = "T-0302: Fotos nach Monaten, Klick/Strg-Klick wählt, Album + Favoriten, Doppelklick groß mit ← →/Esc, Papierkorb nur nach Ja, Doppelte gefunden")]
    public void Ui_photos()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1100;
        void Click(string name) { var b = V<Control>(w, name); b.BringIntoView(); Settle(); RealClick(w, V<Control>(w, name)); Settle(); }
        var dir = Path.Combine(root, "Fotos"); Directory.CreateDirectory(Path.Combine(dir, "Kopien"));
        Png(Path.Combine(dir, "rot.png"), SKColors.IndianRed, new DateTime(2026, 10, 20)); Png(Path.Combine(dir, "gruen.png"), SKColors.SeaGreen, new DateTime(2026, 10, 12));
        Png(Path.Combine(dir, "blau.png"), SKColors.SteelBlue, new DateTime(2026, 10, 2)); Png(Path.Combine(dir, "gelb.png"), SKColors.Goldenrod, new DateTime(2026, 9, 18));
        File.Copy(Path.Combine(dir, "rot.png"), Path.Combine(dir, "Kopien", "rot.png")); File.SetLastWriteTime(Path.Combine(dir, "Kopien", "rot.png"), new DateTime(2026, 9, 1));
        var trashed = new List<string>(); w.TrashOverride = trashed.Add;
        w.PhotoFoldersOverride = [dir];
        w.Show("medien"); Settle();
        for (var i = 0; i < 200 && w.PhotosLoaded.Count == 0; i++) { Thread.Sleep(15); Settle(); }
        Assert.Equal(5, w.PhotosLoaded.Count);
        Assert.StartsWith("5 Fotos", w.Find<TextBlock>("photo-count").Text);
        Assert.Contains(w.GetLogicalTexts(), t => t.StartsWith("Oktober 2026"));
        // Album anlegen
        w.Find<TextBox>("photo-album-name").Text = "Herbst"; Click("photo-album-create");
        Assert.Contains(w.Albums.Albums(), a => a.Name == "Herbst");
        Click("photo-nav-alle");
        // Klick wählt eins, Strg-Klick ein zweites
        Click("photo-0"); Assert.Single(w.PhotoSelection);
        var t1 = V<Control>(w, "photo-1"); t1.BringIntoView(); Settle(); var p1 = t1.TranslatePoint(new Point(t1.Bounds.Width / 2, t1.Bounds.Height / 2), w)!.Value;
        w.MouseDown(p1, MouseButton.Left, RawInputModifiers.Control); w.MouseUp(p1, MouseButton.Left, RawInputModifiers.Control); Settle();
        Assert.Equal(2, w.PhotoSelection.Count);
        w.Find<ComboBox>("photo-sel-album").SelectedItem = "Herbst"; Click("photo-sel-add");
        Assert.Equal(2, w.Albums.In("Herbst").Count);
        Click("photo-sel-fav"); Assert.Equal(2, w.Albums.In("Favoriten").Count);
        Click("photo-sel-none"); Assert.Empty(w.PhotoSelection);
        // Doppelte: die Kopie in „Kopien“ wird gefunden; Nein ändert nichts, Ja verschiebt nur die Kopie
        Click("photo-nav-dups"); Click("photo-dups-find");
        Assert.Contains("1 Kopien", w.Find<TextBlock>("photo-dups-sum").Text);
        Click("photo-dups-trash"); Click("photo-dups-no"); Assert.Empty(trashed);
        Click("photo-dups-trash"); Click("photo-dups-yes");
        Assert.Equal([Path.Combine(dir, "Kopien", "rot.png")], trashed); Assert.Equal(4, w.PhotosLoaded.Count);
        Click("photo-nav-alle");
        // Doppelklick öffnet groß, Pfeiltasten blättern, Esc schließt
        var t0 = V<Control>(w, "photo-0"); t0.BringIntoView(); Settle(); var p0 = t0.TranslatePoint(new Point(t0.Bounds.Width / 2, t0.Bounds.Height / 2), w)!.Value;
        w.MouseDown(p0, MouseButton.Left); w.MouseUp(p0, MouseButton.Left); w.MouseDown(p0, MouseButton.Left); w.MouseUp(p0, MouseButton.Left); Settle();
        Assert.Equal(w.PhotosLoaded[0].Path, w.PhotoOpen);
        Assert.NotNull(w.Find<Image>("photo-big").Source);
        w.Find<Grid>("photo-viewer").Focus(); w.KeyPress(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, null); Settle();
        Assert.Equal(w.PhotosLoaded[1].Path, w.PhotoOpen);
        w.Find<Grid>("photo-viewer").Focus(); w.KeyPress(Key.Left, RawInputModifiers.None, PhysicalKey.ArrowLeft, null); Settle();
        Assert.Equal(w.PhotosLoaded[0].Path, w.PhotoOpen);
        // Papierkorb: Nein ändert nichts, Ja verschiebt
        Click("photo-trash"); Click("photo-trash-no"); Assert.Single(trashed);
        var first = w.PhotoOpen!;
        Click("photo-trash"); Click("photo-trash-yes");
        Assert.Equal(first, trashed[^1]); Assert.Equal(3, w.PhotosLoaded.Count); Assert.NotEqual(first, w.PhotoOpen);
        Assert.DoesNotContain(first, w.Albums.In("Favoriten"));
        w.Find<Grid>("photo-viewer").Focus(); w.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null); Settle();
        Assert.Null(w.PhotoOpen);
    }
}
