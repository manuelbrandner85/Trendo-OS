using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace Trendo.Core;

/// <summary>Echte Messwerte. Nicht messbare Werte sind null (UI zeigt "n. v.") – niemals geschätzt.</summary>
public sealed record MetricsSnapshot(double? CpuPercent, double? RamPercent, double? DiskPercent, double? GpuPercent, string? GpuSource);

public sealed class SystemMetrics
{
    private (ulong idle, ulong total)? _lastCpu;
    private DateTime _lastGpu = DateTime.MinValue; private double? _gpu; private string? _gpuSrc;
    private (long rx, long tx, DateTime t)? _net;

    public MetricsSnapshot Sample()
    {
        return new MetricsSnapshot(Cpu(), Ram(), Disk(), Gpu(), _gpuSrc);
    }

    private double? Cpu()
    {
        (ulong idle, ulong total)? now = null;
        if (OperatingSystem.IsWindows())
        {
            if (GetSystemTimes(out var i, out var k, out var u)) now = (i, k + u); // Kernelzeit enthält Idle
        }
        else if (File.Exists("/proc/stat"))
        {
            var p = File.ReadLines("/proc/stat").First().Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1).Select(ulong.Parse).ToArray();
            now = (p[3] + (p.Length > 4 ? p[4] : 0), (ulong)p.Aggregate(0UL, (a, b) => a + b));
        }
        if (now is null) return null;
        var prev = _lastCpu; _lastCpu = now;
        if (prev is null) return null; // erster Messpunkt: noch kein Intervall
        var dt = now.Value.total - prev.Value.total; var di = now.Value.idle - prev.Value.idle;
        return dt == 0 ? null : Math.Round(100.0 * (dt - di) / dt, 0);
    }

    private static double? Ram()
    {
        if (OperatingSystem.IsWindows())
        {
            var m = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
            return GlobalMemoryStatusEx(ref m) ? m.dwMemoryLoad : null;
        }
        if (!File.Exists("/proc/meminfo")) return null;
        var d = File.ReadLines("/proc/meminfo").Select(l => l.Split(':')).ToDictionary(a => a[0], a => double.Parse(a[1].Trim().Split(' ')[0]));
        return d.TryGetValue("MemTotal", out var t) && d.TryGetValue("MemAvailable", out var a) ? Math.Round(100 * (1 - a / t), 0) : null;
    }

    private static double? Disk()
    {
        try
        {
            var root = Path.GetPathRoot(Environment.SystemDirectory is { Length: > 0 } s ? s : "/") ?? "/";
            var di = new DriveInfo(root);
            return Math.Round(100.0 * (di.TotalSize - di.AvailableFreeSpace) / di.TotalSize, 0);
        }
        catch { return null; }
    }

    /// <summary>Belegter und gesamter Arbeitsspeicher in GiB. Null, wenn das System die Zahlen nicht hergibt.</summary>
    public (double? UsedGb, double? TotalGb) MemorySize()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                var m = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
                if (!GlobalMemoryStatusEx(ref m) || m.a == 0) return (null, null);
                return (Math.Round((m.a - m.b) / 1024d / 1024 / 1024, 1), Math.Round(m.a / 1024d / 1024 / 1024, 1));
            }
            if (!File.Exists("/proc/meminfo")) return (null, null);
            var d = File.ReadLines("/proc/meminfo").Select(l => l.Split(':')).ToDictionary(a => a[0], a => double.Parse(a[1].Trim().Split(' ')[0], System.Globalization.CultureInfo.InvariantCulture));
            if (!d.TryGetValue("MemTotal", out var t) || !d.TryGetValue("MemAvailable", out var a)) return (null, null);
            return (Math.Round((t - a) / 1024 / 1024, 1), Math.Round(t / 1024 / 1024, 1));
        }
        catch { return (null, null); }
    }

    /// <summary>Belegter und gesamter Speicher des Systemlaufwerks in GiB.</summary>
    public (double? UsedGb, double? TotalGb) DiskSize()
    {
        try
        {
            var root = Path.GetPathRoot(Environment.SystemDirectory is { Length: > 0 } s ? s : "/") ?? "/";
            var di = new DriveInfo(root);
            return (Math.Round((di.TotalSize - di.AvailableFreeSpace) / 1024d / 1024 / 1024, 0), Math.Round(di.TotalSize / 1024d / 1024 / 1024, 0));
        }
        catch { return (null, null); }
    }

    /// <summary>Durchsatz seit dem letzten Aufruf, Bytes pro Sekunde. Der erste Aufruf hat noch kein Intervall und liefert null.</summary>
    public (double? DownBps, double? UpBps) NetworkSpeed()
    {
        try
        {
            long rx = 0, tx = 0; var any = false;
            foreach (var n in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (n.OperationalStatus != OperationalStatus.Up || n.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                var s = n.GetIPStatistics(); rx += s.BytesReceived; tx += s.BytesSent; any = true;
            }
            if (!any) return (null, null);
            var now = DateTime.UtcNow; var prev = _net; _net = (rx, tx, now);
            if (prev is null) return (null, null);
            var dt = (now - prev.Value.t).TotalSeconds;
            if (dt < 0.2) return (null, null);
            return (Math.Max(0, (rx - prev.Value.rx) / dt), Math.Max(0, (tx - prev.Value.tx) / dt));
        }
        catch { return (null, null); }
    }

    /// <summary>GPU-Auslastung nur über nvidia-smi (falls vorhanden), höchstens alle 5 s; sonst null.</summary>
    private double? Gpu()
    {
        if ((DateTime.Now - _lastGpu).TotalSeconds < 5) return _gpu;
        _lastGpu = DateTime.Now;
        var exe = OperatingSystem.IsWindows() ? Path.Combine(Environment.SystemDirectory, "nvidia-smi.exe") : "/usr/bin/nvidia-smi";
        if (!File.Exists(exe)) { _gpu = null; _gpuSrc = "keine Messquelle"; return null; }
        try
        {
            using var p = Process.Start(new ProcessStartInfo(exe, "--query-gpu=utilization.gpu --format=csv,noheader,nounits")
            { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true })!;
            // Erst mit Zeitlimit warten, dann lesen: ein hängendes nvidia-smi (Treiber unter Last) darf die Oberfläche nie blockieren
            var read = p.StandardOutput.ReadToEndAsync();
            if (!p.WaitForExit(2000) || !read.Wait(500))
            {
                try { p.Kill(true); } catch (InvalidOperationException) { }
                _gpu = null; _gpuSrc = "Messung zu langsam"; return null;
            }
            _gpu = double.TryParse(read.Result.Split('\n')[0].Trim(), out var v) ? v : null; _gpuSrc = "nvidia-smi";
        }
        catch { _gpu = null; _gpuSrc = "Messung fehlgeschlagen"; }
        return _gpu;
    }

    /// <summary>Freier und gesamter Grafikspeicher in GB (NVIDIA, über nvidia-smi); null, wenn nicht messbar. Höchstens 3 s.</summary>
    public static (double FreeGb, double TotalGb)? GpuMemory()
    {
        var exe = OperatingSystem.IsWindows() ? Path.Combine(Environment.SystemDirectory, "nvidia-smi.exe") : "/usr/bin/nvidia-smi";
        if (!File.Exists(exe)) return null;
        try
        {
            using var p = Process.Start(new ProcessStartInfo(exe, "--query-gpu=memory.free,memory.total --format=csv,noheader,nounits")
            { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true })!;
            var read = p.StandardOutput.ReadToEndAsync();
            if (!p.WaitForExit(2500) || !read.Wait(500)) { try { p.Kill(true); } catch (InvalidOperationException) { } return null; }
            return ParseGpuMemory(read.Result);
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or IOException) { return null; }
    }

    /// <summary>Erste Zeile „frei, gesamt“ in MiB → GB.</summary>
    public static (double FreeGb, double TotalGb)? ParseGpuMemory(string csv)
    {
        var parts = csv.Split('\n')[0].Split(',');
        if (parts.Length < 2 || !double.TryParse(parts[0].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var free)
            || !double.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var total)) return null;
        return (Math.Round(free / 1024, 1), Math.Round(total / 1024, 1));
    }

    [DllImport("kernel32.dll")] private static extern bool GetSystemTimes(out ulong idle, out ulong kernel, out ulong user);
    [StructLayout(LayoutKind.Sequential)] private struct MEMORYSTATUSEX { public uint dwLength, dwMemoryLoad; public ulong a, b, c, d, e, f, g; }
    [DllImport("kernel32.dll")] private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX m);
}
