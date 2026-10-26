using System.Diagnostics;
using Trendo.Core;

namespace Trendo.App;

/// <summary>T-0222: findet den Ordner „Programme“ des Vollpakets. Das Setup liegt meist daneben – darum zuerst die Ordner der aufrufenden Programme.
/// Beim ersten Start ist Setup.exe schon beendet (am PC so beobachtet, 0.9.10): Deshalb merkt sich Trendo den Ordner bereits während der
/// Installation (Velopack-Hook, Setup.exe läuft da noch) in <see cref="RememberFile"/>.</summary>
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

    /// <summary>Gemerkter Ordner des Vollpakets (im Datenordner, überlebt Neuinstallationen und Updates).</summary>
    public static string RememberFile => Path.Combine(DataLocation.Default(), "vollpaket-quelle.txt");

    /// <summary>Während der Installation aufgerufen: Ordner des laufenden Setup.exe prüfen und merken. Schnell, kein Laufwerkssuchen.</summary>
    public static string? RememberFromInstaller(IEnumerable<string>? callers = null, string? file = null, Action<string>? log = null)
    {
        try
        {
            var dir = Vollpaket.Locate(callers ?? CallerDirs());
            if (dir is null) { log?.Invoke("Installation ohne Vollpaket (kein Ordner „Programme“ neben dem Setup)."); return null; }
            var f = file ?? RememberFile;
            Directory.CreateDirectory(Path.GetDirectoryName(f)!);
            File.WriteAllText(f, dir);
            log?.Invoke("Vollpaket gefunden und gemerkt: " + dir);
            return dir;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { log?.Invoke("Vollpaket nicht gemerkt: " + e.Message); return null; }
    }

    public static IEnumerable<string> Remembered(string? file = null)
    {
        var f = file ?? RememberFile;
        try { return File.Exists(f) ? File.ReadAllLines(f).Where(l => l.Trim().Length > 0).Select(l => l.Trim()).ToList() : []; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return []; }
    }

    public static string? Find() => Vollpaket.Locate(Vollpaket.Candidates(Remembered().Concat(CallerDirs())));
}
