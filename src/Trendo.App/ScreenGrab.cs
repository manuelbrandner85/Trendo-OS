using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SkiaSharp;

namespace Trendo.App;

/// <summary>Bildschirmfoto ohne ffmpeg (T-0731): liest die Pixel des ganzen Desktops über die Windows-Grafikschnittstelle (GDI, BitBlt) –
/// gehört zu jedem Windows, ohne Admin, enthält auch fremde Fenster.</summary>
public static class ScreenGrab
{
    /// <summary>Aufnahme: Pixel (BGRA, Zeile für Zeile), Größe und linke obere Ecke des Desktops.</summary>
    public sealed record Shot(byte[] Bgra, int Width, int Height, int Left, int Top)
    {
        /// <summary>Farbe an (x, y) im Bild als (R, G, B).</summary>
        public (byte R, byte G, byte B) At(int x, int y) { var o = (y * Width + x) * 4; return (Bgra[o + 2], Bgra[o + 1], Bgra[o]); }
    }

    public static bool Available => OperatingSystem.IsWindows();

    /// <summary>Ganzer Desktop über alle Bildschirme.</summary>
    public static Shot Desktop()
    {
        if (!Available) throw new InvalidOperationException("Das eingebaute Bildschirmfoto gibt es bisher nur unter Windows – unter Linux braucht es ffmpeg.");
        var old = Native.SetThreadDpiAwarenessContext(Native.PerMonitorV2);
        try { return Area(new PixelRect(Native.GetSystemMetrics(76), Native.GetSystemMetrics(77), Native.GetSystemMetrics(78), Native.GetSystemMetrics(79))); }
        finally { if (old != IntPtr.Zero) Native.SetThreadDpiAwarenessContext(old); }
    }

    /// <summary>Bereich in Bildschirmpixeln.</summary>
    public static Shot Area(PixelRect r)
    {
        if (!Available) throw new InvalidOperationException("Das eingebaute Bildschirmfoto gibt es bisher nur unter Windows – unter Linux braucht es ffmpeg.");
        if (r.Width <= 0 || r.Height <= 0) throw new ArgumentException("Der Bereich ist leer.");
        // Echte Bildschirmpixel, auch wenn der Prozess nicht DPI-bewusst ist (sonst skaliert Windows die Koordinaten um)
        var dpiOld = Native.SetThreadDpiAwarenessContext(Native.PerMonitorV2);
        try { return Grab(r); } finally { if (dpiOld != IntPtr.Zero) Native.SetThreadDpiAwarenessContext(dpiOld); }
    }

    /// <summary>Rahmen eines Fensters in echten Bildschirmpixeln (für Tests und „Fenster aufnehmen“).</summary>
    public static PixelRect WindowRect(IntPtr hwnd)
    {
        var dpiOld = Native.SetThreadDpiAwarenessContext(Native.PerMonitorV2);
        try { return Native.GetWindowRect(hwnd, out var r) ? new PixelRect(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top) : default; }
        finally { if (dpiOld != IntPtr.Zero) Native.SetThreadDpiAwarenessContext(dpiOld); }
    }

    static Shot Grab(PixelRect r)
    {
        var screen = Native.GetDC(IntPtr.Zero); var mem = Native.CreateCompatibleDC(screen); var bmp = Native.CreateCompatibleBitmap(screen, r.Width, r.Height);
        try
        {
            if (screen == IntPtr.Zero || mem == IntPtr.Zero || bmp == IntPtr.Zero) throw new InvalidOperationException("Windows gibt den Bildschirm gerade nicht frei (gesperrt?).");
            var old = Native.SelectObject(mem, bmp);
            var ok = Native.BitBlt(mem, 0, 0, r.Width, r.Height, screen, r.X, r.Y, 0x00CC0020 | 0x40000000); // SRCCOPY | CAPTUREBLT (auch überlagernde Fenster)
            Native.SelectObject(mem, old);
            if (!ok) throw new InvalidOperationException("Der Bildschirm ließ sich nicht lesen (gesperrt oder geschützt?).");
            var bi = new Native.BITMAPINFOHEADER { biSize = (uint)Marshal.SizeOf<Native.BITMAPINFOHEADER>(), biWidth = r.Width, biHeight = -r.Height, biPlanes = 1, biBitCount = 32 };
            var px = new byte[r.Width * r.Height * 4];
            if (Native.GetDIBits(mem, bmp, 0, (uint)r.Height, px, ref bi, 0) != r.Height) throw new InvalidOperationException("Die Bildpunkte ließen sich nicht übernehmen.");
            for (var i = 3; i < px.Length; i += 4) px[i] = 255; // GDI lässt den Deckkanal leer
            return new Shot(px, r.Width, r.Height, r.X, r.Y);
        }
        finally { if (bmp != IntPtr.Zero) Native.DeleteObject(bmp); if (mem != IntPtr.Zero) Native.DeleteDC(mem); if (screen != IntPtr.Zero) Native.ReleaseDC(IntPtr.Zero, screen); }
    }

    /// <summary>Ausschnitt (in Bildpixeln) als PNG speichern; ohne Ausschnitt das ganze Bild.</summary>
    public static string SavePng(Shot s, string path, PixelRect? crop = null)
    {
        var c = crop ?? new PixelRect(0, 0, s.Width, s.Height);
        c = c.Intersect(new PixelRect(0, 0, s.Width, s.Height));
        if (c.Width <= 0 || c.Height <= 0) throw new ArgumentException("Der gewählte Bereich liegt außerhalb des Bildschirms.");
        using var bmp = new SKBitmap(new SKImageInfo(c.Width, c.Height, SKColorType.Bgra8888, SKAlphaType.Opaque));
        var dst = bmp.GetPixels(); var row = c.Width * 4;
        for (var y = 0; y < c.Height; y++) Marshal.Copy(s.Bgra, ((c.Y + y) * s.Width + c.X) * 4, dst + y * bmp.RowBytes, row);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path))!);
        using var img = SKImage.FromBitmap(bmp); using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        var tmp = path + ".tmp"; File.WriteAllBytes(tmp, data.ToArray()); File.Move(tmp, path, true);
        return path;
    }

    /// <summary>Aufnahme als Avalonia-Bild (für die Bereichsauswahl).</summary>
    public static WriteableBitmap ToBitmap(Shot s)
    {
        var wb = new WriteableBitmap(new PixelSize(s.Width, s.Height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
        using var fb = wb.Lock();
        for (var y = 0; y < s.Height; y++) Marshal.Copy(s.Bgra, y * s.Width * 4, fb.Address + y * fb.RowBytes, s.Width * 4);
        return wb;
    }

    static class Native
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAPINFOHEADER { public uint biSize; public int biWidth, biHeight; public ushort biPlanes, biBitCount; public uint biCompression, biSizeImage; public int biXPelsPerMeter, biYPelsPerMeter; public uint biClrUsed, biClrImportant; }
        public static readonly IntPtr PerMonitorV2 = new(-4);
        [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
        [DllImport("user32.dll")] public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr ctx);
        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] public static extern bool GetWindowRect(IntPtr hwnd, out RECT r);
        [DllImport("user32.dll")] public static extern int GetSystemMetrics(int i);
        [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);
        [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleBitmap(IntPtr dc, int w, int h);
        [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
        [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] public static extern bool BitBlt(IntPtr dst, int x, int y, int w, int h, IntPtr src, int sx, int sy, uint rop);
        [DllImport("gdi32.dll")] public static extern int GetDIBits(IntPtr dc, IntPtr bmp, uint start, uint lines, byte[] bits, ref BITMAPINFOHEADER bi, uint usage);
        [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr obj);
        [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr dc);
    }
}

/// <summary>Bereich mit der Maus aufziehen (wie das Snipping Tool): zeigt das eingefrorene Desktopbild über alle Bildschirme,
/// Ziehen wählt, Loslassen übernimmt, Esc bricht ab. Ergebnis in Bildpixeln.</summary>
public sealed class ScreenAreaPicker : Window
{
    readonly PixelSize _img; readonly Canvas _overlay = new() { Name = "pick-overlay", Background = Brushes.Transparent };
    readonly Rectangle _sel = new() { Name = "pick-rect", Stroke = Brush.Parse("#E8B04B"), StrokeThickness = 2, Fill = Brush.Parse("#22E8B04B"), IsVisible = false };
    readonly TextBlock _size = new() { Name = "pick-size", Foreground = Brushes.White, FontSize = 12, Background = Brush.Parse("#CC111111"), Padding = new Thickness(6, 2), IsVisible = false };
    Point? _start; readonly TaskCompletionSource<PixelRect?> _done = new();
    public Task<PixelRect?> Result => _done.Task;

    public ScreenAreaPicker(IImage shot, PixelSize imagePixels, PixelPoint desktopTopLeft)
    {
        _img = imagePixels; Name = "screen-picker"; Title = "Bereich wählen";
        WindowDecorations = WindowDecorations.None; Topmost = true; ShowInTaskbar = false; CanResize = false; Cursor = new Cursor(StandardCursorType.Cross);
        WindowStartupLocation = WindowStartupLocation.Manual; Position = desktopTopLeft; Width = imagePixels.Width; Height = imagePixels.Height; Background = Brushes.Black;
        _overlay.Children.Add(_sel); _overlay.Children.Add(_size);
        Content = new Grid { Children = {
            new Image { Source = shot, Stretch = Stretch.Fill },
            new Border { Background = Brush.Parse("#55000000"), IsHitTestVisible = false },
            _overlay,
            new Border { Name = "pick-hint", Background = Brush.Parse("#DD1C1C1C"), BorderBrush = Brush.Parse("#66E8B04B"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(14, 8),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top, Margin = new Thickness(0, 24, 0, 0), IsHitTestVisible = false,
                Child = new TextBlock { Text = "Bereich mit der Maus aufziehen · Esc bricht ab", Foreground = Brushes.White, FontSize = 14 } } } };
        Opened += (_, _) => { var s = RenderScaling > 0 ? RenderScaling : 1; Width = imagePixels.Width / s; Height = imagePixels.Height / s; Position = desktopTopLeft; };
        _overlay.PointerPressed += (_, e) => { _start = e.GetPosition(_overlay); e.Pointer.Capture(_overlay); Draw(_start.Value); };
        _overlay.PointerMoved += (_, e) => { if (_start is not null) Draw(e.GetPosition(_overlay)); };
        _overlay.PointerReleased += (_, e) =>
        {
            if (_start is not { } a) return;
            e.Pointer.Capture(null); _start = null;
            var r = Map(a, e.GetPosition(_overlay), _overlay.Bounds.Size, _img);
            if (r is null) { _sel.IsVisible = _size.IsVisible = false; return; } // Klick ohne Ziehen: weiter wählen
            Finish(r);
        };
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Finish(null); };
        Closed += (_, _) => _done.TrySetResult(null);
    }

    void Finish(PixelRect? r) { _done.TrySetResult(r); Close(); }

    void Draw(Point b)
    {
        if (_start is not { } a) return;
        var x = Math.Min(a.X, b.X); var y = Math.Min(a.Y, b.Y);
        Canvas.SetLeft(_sel, x); Canvas.SetTop(_sel, y); _sel.Width = Math.Abs(b.X - a.X); _sel.Height = Math.Abs(b.Y - a.Y); _sel.IsVisible = true;
        var r = Map(a, b, _overlay.Bounds.Size, _img);
        _size.Text = r is { } p ? $"{p.Width} × {p.Height} Pixel" : ""; _size.IsVisible = r is not null;
        Canvas.SetLeft(_size, x); Canvas.SetTop(_size, Math.Max(0, y - 24));
    }

    /// <summary>Gezogene Strecke (Fensterkoordinaten) in Bildpixel umrechnen – unabhängig von der Anzeigeskalierung. null bei weniger als 3 Pixeln.</summary>
    public static PixelRect? Map(Point a, Point b, Size view, PixelSize img)
    {
        if (view.Width <= 0 || view.Height <= 0) return null;
        double sx = img.Width / view.Width, sy = img.Height / view.Height;
        int x0 = (int)Math.Floor(Math.Clamp(Math.Min(a.X, b.X), 0, view.Width) * sx), y0 = (int)Math.Floor(Math.Clamp(Math.Min(a.Y, b.Y), 0, view.Height) * sy);
        int x1 = (int)Math.Ceiling(Math.Clamp(Math.Max(a.X, b.X), 0, view.Width) * sx), y1 = (int)Math.Ceiling(Math.Clamp(Math.Max(a.Y, b.Y), 0, view.Height) * sy);
        x1 = Math.Min(x1, img.Width); y1 = Math.Min(y1, img.Height);
        return x1 - x0 < 3 || y1 - y0 < 3 ? null : new PixelRect(x0, y0, x1 - x0, y1 - y0);
    }
}
