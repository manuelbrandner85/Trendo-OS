using System.Diagnostics;
using System.Runtime.InteropServices;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Beim Deinstallieren (Velopack-Hook, höchstens 30 s): Die mit Trendo installierten Programme (Werkzeugordner „TrendoWerkzeuge“:
/// Vollpaket, KI-Laufzeiten, Programme von GitHub) werden immer mit entfernt (Vorgabe 2026-10-26) – in einem eigenen Prozess, weil
/// mehrere GB länger dauern als der Hook darf. Danach Frage, ob auch die eigenen Trendo-Daten gelöscht werden; Standard „Behalten“.
/// Ohne Antwort, bei stiller Deinstallation (--silent) und außerhalb von Windows bleiben die Daten.</summary>
public static class UninstallPrompt
{
    /// <summary>Gesamtbudget des Hooks bis zur Antwort; Velopack bricht nach 30 s ab.</summary>
    public const int TimeoutMs = 22_000;

    public static string Question(string dataRoot, string? projects) =>
        "Trendo OS wird deinstalliert.\n\nSollen auch deine Trendo-Daten gelöscht werden?\n" +
        $"Das sind Einstellungen, Protokolle und Sicherungen in:\n{dataRoot}\n\n" +
        (string.IsNullOrWhiteSpace(projects) ? "" : $"Deine Projekte und Dokumente bleiben in jedem Fall erhalten:\n{projects}\n\n") +
        "Projekte im Trendo-Ordner (projects, projekte) bleiben ebenfalls.\n" +
        "Die mit Trendo installierten Programme werden in jedem Fall mit entfernt; selbst installierte (z. B. Blender, Unreal, Office) bleiben.\n\n" +
        "Ja = Daten löschen · Nein = Daten behalten (empfohlen)";

    /// <summary>Stille Deinstallation erkennt man am Aufruf von Update.exe (uninstall --silent / -s).</summary>
    public static bool IsSilent(string? parentCommandLine)
    {
        if (string.IsNullOrWhiteSpace(parentCommandLine)) return false;
        var parts = parentCommandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Select(p => p.Trim('"', '\'')).Any(p => p.Equals("--silent", StringComparison.OrdinalIgnoreCase) || p.Equals("-s", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Protokoll der Deinstallation in %TEMP%\trendo-deinstallation.log (Programmordner ist danach weg).</summary>
    public static void LogToTemp(string line)
    {
        try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "trendo-deinstallation.log"), $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {line}{Environment.NewLine}"); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>Befehl, der den Werkzeugordner nach dem Ende von Velopack löscht (cmd: kurz warten, dann rd). Nur für geprüfte Pfade.</summary>
    public static string ToolsCleanupArguments(string dir)
    {
        if (dir.IndexOfAny(['"', '&', '|', '<', '>', '^', '%']) >= 0) throw new ArgumentException("Ungültiger Ordnername.", nameof(dir));
        return $"/d /c ping -n 4 127.0.0.1 >nul & rd /s /q \"{dir}\"";
    }

    /// <summary>Programme aus dem Werkzeugordner beenden (nur Prozesse, deren Datei dort liegt) und den Ordner im Hintergrund löschen.</summary>
    public static void RemoveTools(string tools, (IReadOnlyList<string> Programs, long Bytes) info, Action<string>? log)
    {
        if (DataLocation.RemovableTools(tools) is not { } dir) { log?.Invoke("Programme bleiben (geschützter Ordner)."); return; }
        var prefix = dir.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        foreach (var p in Process.GetProcesses())
        {
            try { if (p.MainModule?.FileName is { } f && f.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) { p.Kill(true); log?.Invoke("Beendet: " + Path.GetFileName(f)); } }
            catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException) { }
            finally { p.Dispose(); }
        }
        try
        {
            using var cmd = Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"), ToolsCleanupArguments(dir))
            { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Path.GetTempPath() });
            log?.Invoke($"Programme werden entfernt ({string.Join(", ", info.Programs)}, {Size(info.Bytes)}): {dir}");
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or ArgumentException)
        {
            log?.Invoke("Löschen im Hintergrund nicht möglich, lösche direkt: " + e.Message);
            try { DataLocation.RemoveTools(dir); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { log?.Invoke("Programme nicht vollständig gelöscht: " + ex.Message); }
        }
    }

    static string Size(long b) => b >= 1L << 30 ? $"{b / (double)(1L << 30):0.0} GB" : b >= 1L << 20 ? $"{b / (double)(1L << 20):0} MB" : $"{Math.Max(1, b / 1024)} KB";

    public static void Run(Action<string>? log = null)
    {
        var clock = Stopwatch.StartNew(); // Velopack beendet den Hook nach 30 s
        log?.Invoke("Deinstallation: Programme entfernen, Frage nach Daten.");
        if (!OperatingSystem.IsWindows()) return;
        var root = DataLocation.Default(); // fest: nie ein anderer Ordner
        var tools = DataLocation.ToolsRoot();
        if (DataLocation.ToolsSummary(tools) is { } toolInfo) RemoveTools(tools, toolInfo, log);
        if (!Directory.Exists(root)) return;
        if (IsSilent(ParentCommandLine())) { log?.Invoke("Stille Deinstallation: Daten bleiben."); return; }
        string? projects;
        try { projects = UserSettings.Load(root).ProjectsFolder; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { log?.Invoke("Einstellungen nicht lesbar: Daten bleiben."); return; }
        log?.Invoke("Frage angezeigt.");
        // MB_YESNO | MB_ICONQUESTION | MB_DEFBUTTON2 (Nein vorausgewählt) | MB_TOPMOST | MB_SETFOREGROUND; 6 = Ja, 32000 = Zeit abgelaufen
        var answer = MessageBoxTimeoutW(IntPtr.Zero, Question(root, projects), "Trendo OS deinstallieren", 0x4 | 0x20 | 0x100 | 0x40000 | 0x10000, 0,
            Math.Max(3000, TimeoutMs - (int)clock.ElapsedMilliseconds));
        if (answer != 6) { log?.Invoke(answer == 32000 ? "Keine Antwort: Daten bleiben." : "Daten bleiben."); return; }
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

    internal static int ParentId(IntPtr handle)
    {
        var info = new ProcessBasicInformation();
        return NtQueryInformationProcess(handle, 0, ref info, Marshal.SizeOf(info), out _) == 0 ? info.InheritedFromUniqueProcessId.ToInt32() : 0;
    }
}
