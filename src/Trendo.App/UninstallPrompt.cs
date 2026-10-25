using System.Diagnostics;
using System.Runtime.InteropServices;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Beim Deinstallieren (Velopack-Hook, höchstens 30 s): fragen, ob auch die eigenen Trendo-Daten und danach die von Trendo
/// geladenen Zusatzprogramme gelöscht werden. Standard ist jeweils „Behalten“. Ohne Antwort, bei stiller Deinstallation (--silent)
/// und außerhalb von Windows bleibt alles.</summary>
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

    /// <summary>Protokoll der Deinstallation in %TEMP%\trendo-deinstallation.log (Programmordner ist danach weg).</summary>
    public static void LogToTemp(string line)
    {
        try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "trendo-deinstallation.log"), $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {line}{Environment.NewLine}"); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>Zweite Frage: die von Trendo geladenen Zusatzprogramme (z. B. COLMAP, qpdf) im Werkzeugordner.</summary>
    public static string ToolsQuestion(string toolsRoot, IReadOnlyList<string> programs, long bytes) =>
        "Sollen auch die Zusatzprogramme gelöscht werden, die Trendo für dich geladen hat?\n" +
        $"{(programs.Count > 0 ? string.Join(", ", programs) : "Werkzeuge")} – zusammen {Size(bytes)} in:\n{toolsRoot}\n\n" +
        "Programme, die du selbst installiert hast (z. B. Blender, Unreal, Office), bleiben unberührt.\n\n" +
        "Ja = Zusatzprogramme löschen · Nein = behalten (empfohlen)";

    static string Size(long b) => b >= 1L << 30 ? $"{b / (double)(1L << 30):0.0} GB" : b >= 1L << 20 ? $"{b / (double)(1L << 20):0} MB" : $"{Math.Max(1, b / 1024)} KB";

    public static void Run(Action<string>? log = null)
    {
        var clock = Stopwatch.StartNew(); // Velopack beendet den Hook nach 30 s – beide Fragen teilen sich TimeoutMs
        log?.Invoke("Deinstallation: Fragen nach Daten und Zusatzprogrammen.");
        if (!OperatingSystem.IsWindows()) return;
        var root = DataLocation.Default(); // fest: nie ein anderer Ordner
        var tools = DataLocation.ToolsRoot();
        var toolInfo = DataLocation.ToolsSummary(tools);
        var hasData = Directory.Exists(root);
        if (!hasData && toolInfo is null) return;
        if (IsSilent(ParentCommandLine())) { log?.Invoke("Stille Deinstallation: Daten und Zusatzprogramme bleiben."); return; }
        int Remaining() => TimeoutMs - (int)clock.ElapsedMilliseconds;
        // MB_YESNO | MB_ICONQUESTION | MB_DEFBUTTON2 (Nein vorausgewählt) | MB_TOPMOST | MB_SETFOREGROUND; 6 = Ja, 32000 = Zeit abgelaufen
        int Ask(string text, int ms) => MessageBoxTimeoutW(IntPtr.Zero, text, "Trendo OS deinstallieren", 0x4 | 0x20 | 0x100 | 0x40000 | 0x10000, 0, ms);
        if (hasData)
        {
            string? projects;
            try { projects = UserSettings.Load(root).ProjectsFolder; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { log?.Invoke("Einstellungen nicht lesbar: Daten und Zusatzprogramme bleiben."); return; }
            log?.Invoke("Frage angezeigt.");
            var answer = Ask(Question(root, projects), Math.Max(3000, Remaining()));
            if (answer == 32000) { log?.Invoke("Keine Antwort: Daten und Zusatzprogramme bleiben."); return; } // niemand da: nicht weiter fragen
            if (answer != 6) log?.Invoke("Daten bleiben.");
            else
            {
                try { var n = DataLocation.RemoveUserData(root, projects); log?.Invoke(n < 0 ? "Daten bleiben (geschützter Ordner)." : $"Daten gelöscht ({n} Einträge), Projekte behalten."); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { log?.Invoke("Daten konnten nicht vollständig gelöscht werden: " + ex.Message); }
            }
        }
        if (toolInfo is not { } t) return;
        if (Remaining() < 3000) { log?.Invoke("Keine Zeit mehr für die zweite Frage: Zusatzprogramme bleiben."); return; }
        log?.Invoke("Frage nach Zusatzprogrammen angezeigt.");
        var toolsAnswer = Ask(ToolsQuestion(tools, t.Programs, t.Bytes), Remaining());
        if (toolsAnswer != 6) { log?.Invoke(toolsAnswer == 32000 ? "Keine Antwort: Zusatzprogramme bleiben." : "Zusatzprogramme bleiben."); return; }
        try { var n = DataLocation.RemoveTools(tools); log?.Invoke(n < 0 ? "Zusatzprogramme bleiben (geschützter Ordner)." : $"Zusatzprogramme gelöscht ({n} Einträge)."); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { log?.Invoke("Zusatzprogramme konnten nicht vollständig gelöscht werden: " + ex.Message); }
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
