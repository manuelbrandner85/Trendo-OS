using System.Runtime.InteropServices;
using Avalonia.Controls;

namespace Trendo.App;

/// <summary>Holt das Hauptfenster nach vorn. Der Installer startet den Prozess unter Windows manchmal ohne sichtbares Fenster.</summary>
public static class WindowReveal
{
    public static void Force(Window window)
    {
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            var handle = window.TryGetPlatformHandle()?.Handle ?? 0;
            if (handle == 0) return;
            ShowWindow(handle, 3); // SW_SHOWMAXIMIZED: Fenster erzeugen und maximiert zeigen
            SetForegroundWindow(handle);
        }
        catch (Exception) { /* Anzeige bleibt über Avalonia Show/Activate */ }
    }

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint hWnd);
}
