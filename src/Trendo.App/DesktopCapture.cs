using System.Runtime.InteropServices;
using System.Text;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Sucht Bildschirme, Fenster, Kameras und Mikrofone auf diesem PC. Installiert nichts.</summary>
public static class DesktopCapture
{
    public static IReadOnlyList<WindowGrab> Windows()
    {
        if (OperatingSystem.IsWindows()) return WinTitles();
        var wm = Cli.Run("wmctrl", ["-lG"], timeoutMs: 4000);
        if (wm.Out.Length > 0) return LiveDevices.ParseWmctrl(wm.Out);
        var xw = Cli.Run("xwininfo", ["-root", "-tree"], timeoutMs: 4000);
        if (xw.Out.Length > 0) return LiveDevices.ParseXwininfo(xw.Out);
        return [];
    }

    public static CaptureDevices Devices(string ffmpeg)
    {
        if (OperatingSystem.IsWindows())
        {
            var r = Cli.Run(ffmpeg, ["-hide_banner", "-list_devices", "true", "-f", "dshow", "-i", "dummy"], timeoutMs: 8000);
            var (c, m) = LiveDevices.ParseDshow(r.Err + "\n" + r.Out);
            return new(c, m);
        }
        if (OperatingSystem.IsMacOS())
        {
            var r = Cli.Run(ffmpeg, ["-hide_banner", "-f", "avfoundation", "-list_devices", "true", "-i", ""], timeoutMs: 8000);
            var (c, m) = LiveDevices.ParseAvfoundation(r.Err + "\n" + r.Out);
            return new(c, m);
        }
        var cams = Directory.Exists("/dev")
            ? Directory.GetFiles("/dev", "video*").OrderBy(x => x, StringComparer.Ordinal).Select(x => new DeviceItem(x, x)).ToList()
            : [];
        var pulse = Cli.Run(ffmpeg, ["-hide_banner", "-sources", "pulse"], timeoutMs: 8000);
        var mics = LiveDevices.ParsePulse(pulse.Out + "\n" + pulse.Err);
        if (mics.Count == 0)
        {
            var p = Cli.Run("pactl", ["list", "short", "sources"], timeoutMs: 4000);
            mics = LiveDevices.ParsePactl(p.Out);
        }
        return new(cams, mics);
    }

    private static List<WindowGrab> WinTitles()
    {
        var list = new List<string>();
        EnumWindows((h, _) =>
        {
            if (!IsWindowVisible(h)) return true;
            var len = GetWindowTextLength(h);
            if (len <= 0) return true;
            var sb = new StringBuilder(len + 1);
            GetWindowText(h, sb, sb.Capacity);
            var t = sb.ToString().Trim();
            if (t.Length > 0) list.Add(t);
            return true;
        }, 0);
        return list.Distinct().OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase).Select(t => new WindowGrab(t, 0, 0, 0, 0, true)).ToList();
    }

    private delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowTextLength(IntPtr h);
}
