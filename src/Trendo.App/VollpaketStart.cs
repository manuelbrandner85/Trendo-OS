using System.Diagnostics;
using Trendo.Core;

namespace Trendo.App;

/// <summary>T-0222: findet den Ordner „Programme“ des Vollpakets. Das Setup liegt meist daneben – darum zuerst die Ordner der aufrufenden Programme.</summary>
public static class VollpaketStart
{
    /// <summary>Ordner der Eltern-Prozesse (bis vier Ebenen), z. B. Setup.exe vom USB-Stick.</summary>
    public static IEnumerable<string> CallerDirs()
    {
        if (!OperatingSystem.IsWindows()) yield break;
        var pid = Environment.ProcessId;
        for (var i = 0; i < 4; i++)
        {
            string? dir = null;
            try
            {
                using var p = Process.GetProcessById(pid);
                pid = UninstallPrompt.ParentId(p.Handle);
                if (pid <= 0) yield break;
                using var parent = Process.GetProcessById(pid);
                dir = Path.GetDirectoryName(parent.MainModule?.FileName);
            }
            catch (Exception e) when (e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { yield break; }
            if (dir is not null) yield return dir;
        }
    }

    public static string? Find() => Vollpaket.Locate(Vollpaket.Candidates(CallerDirs()));
}
