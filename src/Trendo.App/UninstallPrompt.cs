using System.Diagnostics;
using System.Runtime.InteropServices;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Beim Deinstallieren (Velopack-Hook, höchstens 30 s): fragen, ob auch die eigenen Trendo-Daten gelöscht werden.
/// Standard ist „Behalten“. Ohne Antwort nach 25 s, bei stiller Deinstallation (--silent) und außerhalb von Windows bleibt alles.</summary>
public static class UninstallPrompt
{
    /// <summary>Gesamtbudget des Hooks bis zur Antwort; Velopack bricht nach 30 s ab.</summary>
    public const int TimeoutMs = 22_000;

    public static string Question(string dataRoot, string? projects) =>
        "Trendo OS wird deinstalliert.\n\nSollen auch deine Trendo-Daten gelöscht werden?\n" +
        $"Das sind Einstellungen, Protokolle und Sicherungen in:\n{dataRoot}\n\n" +
        (string.IsNullOrWhiteSpace(projects) ? "" : $"Deine Projekte und Dokumente bleiben in jedem Fall erhalten:\n{projects}\n\n") +
        "Projekte im Trendo-Ordner (projects, projekte) bleiben ebenfalls.\n\n" +
        "Ja = Daten löschen · Nein = Daten behalten (empfohlen)";

    /// <summary>Stille Deinstallation erkennt man am Aufruf von Update.exe (uninstall --silent / -s).</summary>
    public static bool IsSilent(string? parentCommandLine)
    {
        if (string.IsNullOrWhiteSpace(parentCommandLine)) return false;
        var parts = parentCommandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Select(p => p.Trim('"', '\'')).Any(p => p.Equals("--silent", StringComparison.OrdinalIgnoreCase) || p.Equals("-s", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Protokoll der Deinstallation in %TEMP%	rendo-deinstallation.log (Programmordner ist danach weg).</summary>
    public static void LogToTemp(string line)
    {
        try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "trendo-deinstallation.log"), $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {line}{Environment.NewLine}"); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    public static void Run(Action<string>? log = null)
    {
        var clock = Stopwatch.StartNew(); // Velopack beendet den Hook nach 30 s
        log?.Invoke("Deinstallation: Frage nach den Daten.");
        if (!OperatingSystem.IsWindows()) return;
        var root = DataLocation.Default(); // fest: nie ein anderer Ordner
        if (!Directory.Exists(root)) return;
        if (IsSilent(ParentCommandLine())) { log?.Invoke("Stille Deinstallation: Daten bleiben."); return; }
        string? projects = null;
        try { projects = UserSettings.Load(root).ProjectsFolder; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { log?.Invoke("Einstellungen nicht lesbar: Daten bleiben."); return; }
        // MB_YESNO | MB_ICONQUESTION | MB_DEFBUTTON2 (Nein vorausgewählt) | MB_TOPMOST | MB_SETFOREGROUND
        log?.Invoke("Frage angezeigt.");
        var answer = MessageBoxTimeoutW(IntPtr.Zero, Question(root, projects), "Trendo OS deinstallieren", 0x4 | 0x20 | 0x100 | 0x40000 | 0x10000, 0, Math.Max(3000, TimeoutMs - (int)clock.ElapsedMilliseconds));
        if (answer != 6) { log?.Invoke(answer == 32000 ? "Keine Antwort: Daten bleiben." : "Daten bleiben."); return; } // 6 = Ja, 32000 = Zeit abgelaufen
        try { var n = DataLocation.RemoveUserData(root, projects); log?.Invoke(n < 0 ? "Daten bleiben (geschützter Ordner)." : $"Daten gelöscht ({n} Einträge), Projekte behalten."); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { log?.Invoke("Daten konnten nicht vollständig gelöscht werden: " + ex.Message); }
    }

    static string? ParentCommandLine()
    {
        try
        {
            var pid = ParentId(Process.GetCurrentProcess().Handle);
            if (pid <= 0) return null;
            var shell = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
            if (!File.Exists(shell)) return null;
            using var ps = Process.Start(new ProcessStartInfo(shell, $"-NoProfile -NonInteractive -Command \"(Get-CimInstance Win32_Process -Filter 'ProcessId={pid}').CommandLine\"")
            { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true });
            if (ps is null) return null;
            var output = ps.StandardOutput.ReadToEndAsync();
            if (!ps.WaitForExit(2000)) { try { ps.Kill(); } catch (InvalidOperationException) { } return null; }
            return output.Result.Trim();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { return null; }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct ProcessBasicInformation { public IntPtr Reserved1, PebBaseAddress, Reserved2a, Reserved2b, UniqueProcessId, InheritedFromUniqueProcessId; }

    [DllImport("ntdll.dll")] static extern int NtQueryInformationProcess(IntPtr process, int cls, ref ProcessBasicInformation info, int size, out int returned);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int MessageBoxTimeoutW(IntPtr hWnd, string text, string caption, uint type, ushort language, int milliseconds);

    static int ParentId(IntPtr handle)
    {
        var info = new ProcessBasicInformation();
        return NtQueryInformationProcess(handle, 0, ref info, Marshal.SizeOf(info), out _) == 0 ? info.InheritedFromUniqueProcessId.ToInt32() : 0;
    }
}
