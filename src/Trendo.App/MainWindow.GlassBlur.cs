using Avalonia.Media.Imaging;

namespace Trendo.App;

public partial class MainWindow
{
    private static Bitmap? BlurredWallpaperBitmap(string name) => GlassBlur.Get(name, () =>
    {
        foreach (var uri in new[] { $"avares://TrendoOS/Assets/wallpapers/{name}.jpg", "avares://TrendoOS/Assets/wallpaper.jpg" })
            try { return Avalonia.Platform.AssetLoader.Open(new System.Uri(uri)); } catch { }
        return null;
    });

    /// <summary>Bildwechsel: Wallpaper setzen und Glas-Cache für das neue Bild verwenden (einmal berechnet).</summary>
    private void SetWallpaper(string name)
    {
        _wallName = name; _wallImg.Source = LoadWallpaper(name);
        if (!LowPower) _blurImg.Source = BlurredWallpaperBitmap(name);
    }
}
