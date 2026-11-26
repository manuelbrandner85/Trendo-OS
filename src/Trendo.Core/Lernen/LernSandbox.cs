using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;

namespace Trendo.Core.Lernen;

/// <summary>T-0782 (STUDIO-CODELEARN-01): abgeschotteter Lauf für den Code einer lernenden Person – ohne Adminrechte.
/// Windows: Prozess in einem eigenen <b>AppContainer</b> ohne jede Fähigkeit (keine Netzwerk-Fähigkeit → das Betriebssystem sperrt Netz und Loopback;
/// Lesen und Schreiben nur dort, wo der Container ausdrücklich Zugriff hat: Laufzeit nur lesen, eigener Arbeitsordner im Temp-Ordner schreiben),
/// in einem <b>Job-Objekt</b> (genau ein Prozess, Speichergrenze, wird beim Ende mit beendet, keine Zwischenablage) und mit <b>Zeitgrenze</b>.
/// Die Laufzeit ist eine Kopie des mitgelieferten Python (Trendo-Werkzeugordner) in Trendos Datenordner – der Werkzeugordner selbst wird nie verändert.
/// Vorbild der Grenzen: Exercism-Test-Runner (ohne Netz, Zeitgrenze, Ergebnis je Test); eigener Code, kein fremder übernommen.</summary>
public static class LernSandbox
{
    /// <summary>Name des AppContainer-Profils (je Windows-Benutzer, ohne Admin angelegt).</summary>
    public const string ContainerName = "Trendo.Lernen.Code";
    static readonly object Gate = new();

    public sealed record Result(int Exit, bool TimedOut, bool MemoryLimit, double Seconds, string Out, string Err, string Text)
    {
        public bool Ok => Exit == 0 && !TimedOut;
    }

    /// <summary>Mitgeliefertes Python (eingebettete Fassung) im Werkzeugordner – oder null.</summary>
    public static string? BundledPython(string? toolsRoot = null)
    {
        var exe = Path.Combine(toolsRoot ?? DataLocation.ToolsRoot(), "python", "python.exe");
        return File.Exists(exe) && File.Exists(Path.Combine(Path.GetDirectoryName(exe)!, "python313.zip")) ? exe : null;
    }

    /// <summary>Lern-Laufzeit vorbereiten: Kopie des mitgelieferten Python unter <paramref name="dataRoot"/>/lernen/laufzeit/python, mit Lesezugriff für den Container.
    /// Null + Grund, wenn es kein mitgeliefertes Python gibt.</summary>
    [SupportedOSPlatform("windows")]
    public static (string? Exe, string? Why) PrepareRuntime(string dataRoot, string? toolsRoot = null)
    {
        if (BundledPython(toolsRoot) is not { } src)
            return (null, "BLOCKIERT: Das mitgelieferte Python fehlt (Trendo-Werkzeugordner „python“). Ohne es startet Trendo keinen Code von Lernenden – ein Python aus dem System läuft nicht abgeschottet. Einrichten: unter „Zusatzprogramme“ das Trendo-Vollpaket ergänzen.");
        var dir = Path.Combine(dataRoot, "lernen", "laufzeit", "python");
        lock (Gate)
        {
            var srcDir = Path.GetDirectoryName(src)!;
            var stamp = Path.Combine(dir, ".trendo-quelle");
            var want = $"{new FileInfo(src).Length}|{File.GetLastWriteTimeUtc(Path.Combine(srcDir, "python313.zip")).Ticks}";
            if (!File.Exists(stamp) || File.ReadAllText(stamp) != want)
            {
                Directory.CreateDirectory(dir);
                foreach (var f in Directory.GetFiles(srcDir)) File.Copy(f, Path.Combine(dir, Path.GetFileName(f)), true);
                File.WriteAllText(stamp, want);
            }
            Grant(dir, Sid(), FileSystemRights.ReadAndExecute);
        }
        return (Path.Combine(dir, "python.exe"), null);
    }

    /// <summary>SID des Containers als Text (legt das Profil bei Bedarf an).</summary>
    [SupportedOSPlatform("windows")]
    public static string Sid()
    {
        var hr = CreateAppContainerProfile(ContainerName, "Trendo – Code von Lernenden", "Abgeschotteter Lauf ohne Netz für „Programmieren lernen“", IntPtr.Zero, 0, out var sid);
        if (hr == unchecked((int)0x800700B7)) hr = DeriveAppContainerSidFromAppContainerName(ContainerName, out sid); // gibt es schon
        if (hr != 0) throw new Win32Exception(hr, "Der abgeschottete Bereich für Lernende ließ sich nicht anlegen.");
        try { if (!ConvertSidToStringSidW(sid, out var s)) throw new Win32Exception(); try { return Marshal.PtrToStringUni(s)!; } finally { LocalFree(s); } }
        finally { FreeSid(sid); }
    }

    [SupportedOSPlatform("windows")]
    static void Grant(string dir, string sid, FileSystemRights rights)
    {
        var di = new DirectoryInfo(dir);
        var acl = di.GetAccessControl();
        var id = new SecurityIdentifier(sid);
        if (acl.GetAccessRules(true, false, typeof(SecurityIdentifier)).OfType<FileSystemAccessRule>().Any(r => r.IdentityReference == id && (r.FileSystemRights & rights) == rights && r.AccessControlType == AccessControlType.Allow)) return;
        acl.AddAccessRule(new FileSystemAccessRule(id, rights, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        di.SetAccessControl(acl);
    }

    /// <summary>Programm im Container ausführen. Nur <paramref name="workDir"/> ist beschreibbar; Ausgaben landen dort in „ausgabe.txt“/„fehler.txt“.</summary>
    public static Result Run(string exe, IReadOnlyList<string> args, string workDir, int timeoutMs = 10_000, long memoryBytes = 512L << 20)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Der abgeschottete Lauf für Lernende ist bisher nur unter Windows gebaut – ohne ihn startet Trendo keinen Code.");
        return RunWindows(exe, args, workDir, timeoutMs, memoryBytes);
    }

    [SupportedOSPlatform("windows")]
    static Result RunWindows(string exe, IReadOnlyList<string> args, string workDir, int timeoutMs, long memoryBytes)
    {
        Directory.CreateDirectory(workDir); Directory.CreateDirectory(Path.Combine(workDir, "tmp"));
        var sidText = Sid();
        Grant(workDir, sidText, FileSystemRights.Modify);
        var outFile = Path.Combine(workDir, "ausgabe.txt"); var errFile = Path.Combine(workDir, "fehler.txt");
        var sw = Stopwatch.StartNew();
        IntPtr sid = IntPtr.Zero, job = IntPtr.Zero, attr = IntPtr.Zero, env = IntPtr.Zero, handles = IntPtr.Zero, caps = IntPtr.Zero;
        var pi = new PROCESS_INFORMATION();
        using var fout = new FileStream(outFile, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
        using var ferr = new FileStream(errFile, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
        try
        {
            if (!ConvertStringSidToSidW(sidText, out sid)) throw new Win32Exception();
            job = CreateJobObjectW(IntPtr.Zero, null); if (job == IntPtr.Zero) throw new Win32Exception();
            var lim = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
            lim.BasicLimitInformation.LimitFlags = 0x2000 | 0x0008 | 0x0100 | 0x0400; // KILL_ON_JOB_CLOSE | ACTIVE_PROCESS | PROCESS_MEMORY | DIE_ON_UNHANDLED_EXCEPTION
            lim.BasicLimitInformation.ActiveProcessLimit = 1;
            lim.ProcessMemoryLimit = (UIntPtr)(ulong)memoryBytes;
            if (!SetInformationJobObject(job, 9, ref lim, (uint)Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>())) throw new Win32Exception();
            var ui = new JOBOBJECT_BASIC_UI_RESTRICTIONS { UIRestrictionsClass = 0x01 | 0x02 | 0x04 | 0x08 | 0x10 | 0x20 | 0x40 | 0x80 };
            if (!SetInformationJobObject(job, 4, ref ui, (uint)Marshal.SizeOf<JOBOBJECT_BASIC_UI_RESTRICTIONS>())) throw new Win32Exception();

            // Attribute: Container ohne Fähigkeiten (kein internetClient, kein privateNetwork) + nur die beiden Ausgabedateien vererben
            var hOut = fout.SafeFileHandle.DangerousGetHandle(); var hErr = ferr.SafeFileHandle.DangerousGetHandle();
            IntPtr size = IntPtr.Zero; InitializeProcThreadAttributeList(IntPtr.Zero, 2, 0, ref size);
            attr = Marshal.AllocHGlobal(size); if (!InitializeProcThreadAttributeList(attr, 2, 0, ref size)) throw new Win32Exception();
            var sc = new SECURITY_CAPABILITIES { AppContainerSid = sid, Capabilities = IntPtr.Zero, CapabilityCount = 0, Reserved = 0 };
            caps = Marshal.AllocHGlobal(Marshal.SizeOf<SECURITY_CAPABILITIES>()); Marshal.StructureToPtr(sc, caps, false);
            if (!UpdateProcThreadAttribute(attr, 0, (IntPtr)0x20009 /*SECURITY_CAPABILITIES*/, caps, (IntPtr)Marshal.SizeOf<SECURITY_CAPABILITIES>(), IntPtr.Zero, IntPtr.Zero)) throw new Win32Exception();
            handles = Marshal.AllocHGlobal(IntPtr.Size * 2); Marshal.WriteIntPtr(handles, hOut); Marshal.WriteIntPtr(handles, IntPtr.Size, hErr);
            if (!UpdateProcThreadAttribute(attr, 0, (IntPtr)0x20002 /*HANDLE_LIST*/, handles, (IntPtr)(IntPtr.Size * 2), IntPtr.Zero, IntPtr.Zero)) throw new Win32Exception();
            var si = new STARTUPINFOEX(); si.StartupInfo.cb = Marshal.SizeOf<STARTUPINFOEX>(); si.StartupInfo.dwFlags = 0x100 | 0x1;
            si.StartupInfo.hStdOutput = hOut; si.StartupInfo.hStdError = hErr; si.lpAttributeList = attr;

            var tmp = Path.Combine(workDir, "tmp");
            var e = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["SystemRoot"] = Environment.GetEnvironmentVariable("SystemRoot") ?? @"C:\Windows",
                ["windir"] = Environment.GetEnvironmentVariable("windir") ?? @"C:\Windows",
                ["PATH"] = Environment.GetFolderPath(Environment.SpecialFolder.System) + ";" + Path.GetDirectoryName(Path.GetFullPath(exe)),
                ["TEMP"] = tmp, ["TMP"] = tmp, ["USERPROFILE"] = workDir, ["APPDATA"] = tmp, ["LOCALAPPDATA"] = tmp,
                ["PYTHONIOENCODING"] = "utf-8", ["PYTHONDONTWRITEBYTECODE"] = "1", ["PYTHONUTF8"] = "1",
            };
            var block = new StringBuilder(); foreach (var (k, v) in e) block.Append(k).Append('=').Append(v).Append('\0'); block.Append('\0');
            env = Marshal.StringToHGlobalUni(block.ToString());

            var cmd = new StringBuilder(Sys.Sandbox.Quote(exe)); foreach (var a in args) cmd.Append(' ').Append(Sys.Sandbox.Quote(a));
            const uint flags = 0x00000004 | 0x08000000 | 0x00000400 | 0x00080000; // SUSPENDED | NO_WINDOW | UNICODE_ENVIRONMENT | EXTENDED_STARTUPINFO
            lock (Gate)
            {
                SetHandleInformation(hOut, 1, 1); SetHandleInformation(hErr, 1, 1);
                var ok = CreateProcessW(null, cmd, IntPtr.Zero, IntPtr.Zero, true, flags, env, workDir, ref si, out pi);
                var err = Marshal.GetLastWin32Error();
                SetHandleInformation(hOut, 1, 0); SetHandleInformation(hErr, 1, 0);
                if (!ok) throw new Win32Exception(err, "Der Code ließ sich im abgeschotteten Bereich nicht starten.");
            }
            if (!AssignProcessToJobObject(job, pi.hProcess)) { TerminateProcess(pi.hProcess, 1); throw new Win32Exception(); }
            ResumeThread(pi.hThread);
            var timedOut = WaitForSingleObject(pi.hProcess, (uint)timeoutMs) == 0x102;
            if (timedOut) { TerminateJobObject(job, 0xEE); WaitForSingleObject(pi.hProcess, 5000); }
            GetExitCodeProcess(pi.hProcess, out var code);
            var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
            QueryInformationJobObject(job, 9, ref info, (uint)Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>(), IntPtr.Zero);
            var memHit = (long)(ulong)info.PeakProcessMemoryUsed >= memoryBytes * 95 / 100;
            fout.Flush(); ferr.Flush(); sw.Stop();
            string Read(string f) { try { using var s = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite); using var r = new StreamReader(s, Encoding.UTF8); var t = r.ReadToEnd(); return t.Length > 20000 ? t[^20000..] : t; } catch (IOException) { return ""; } }
            var exit = unchecked((int)code);
            var text = timedOut ? $"abgebrochen: Zeitgrenze von {timeoutMs / 1000.0:0.#} s überschritten"
                : memHit ? $"abgebrochen: Speichergrenze ({memoryBytes >> 20} MB) erreicht"
                : exit == 0 ? "ok" : Sys.Sandbox.CrashName(exit) is { } crash ? $"abgestürzt ({crash})" : $"beendet mit Fehlercode {exit}";
            return new Result(exit, timedOut, memHit, sw.Elapsed.TotalSeconds, Read(outFile), Read(errFile), text);
        }
        finally
        {
            if (pi.hThread != IntPtr.Zero) CloseHandle(pi.hThread);
            if (pi.hProcess != IntPtr.Zero) CloseHandle(pi.hProcess);
            if (job != IntPtr.Zero) CloseHandle(job);
            if (attr != IntPtr.Zero) { DeleteProcThreadAttributeList(attr); Marshal.FreeHGlobal(attr); }
            if (handles != IntPtr.Zero) Marshal.FreeHGlobal(handles);
            if (caps != IntPtr.Zero) Marshal.FreeHGlobal(caps);
            if (env != IntPtr.Zero) Marshal.FreeHGlobal(env);
            if (sid != IntPtr.Zero) LocalFree(sid);
        }
    }

    // ───── Windows-API ─────
    [StructLayout(LayoutKind.Sequential)] struct PROCESS_INFORMATION { public IntPtr hProcess, hThread; public int dwProcessId, dwThreadId; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct STARTUPINFO
    {
        public int cb; public string? lpReserved, lpDesktop, lpTitle; public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
        public short wShowWindow, cbReserved2; public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
    }
    [StructLayout(LayoutKind.Sequential)] struct STARTUPINFOEX { public STARTUPINFO StartupInfo; public IntPtr lpAttributeList; }
    [StructLayout(LayoutKind.Sequential)] struct SECURITY_CAPABILITIES { public IntPtr AppContainerSid; public IntPtr Capabilities; public uint CapabilityCount; public uint Reserved; }
    [StructLayout(LayoutKind.Sequential)]
    struct JOBOBJECT_BASIC_LIMIT_INFORMATION { public long PerProcessUserTimeLimit, PerJobUserTimeLimit; public uint LimitFlags; public UIntPtr MinimumWorkingSetSize, MaximumWorkingSetSize; public uint ActiveProcessLimit; public UIntPtr Affinity; public uint PriorityClass, SchedulingClass; }
    [StructLayout(LayoutKind.Sequential)] struct IO_COUNTERS { public ulong ReadOperationCount, WriteOperationCount, OtherOperationCount, ReadTransferCount, WriteTransferCount, OtherTransferCount; }
    [StructLayout(LayoutKind.Sequential)]
    struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION { public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation; public IO_COUNTERS IoInfo; public UIntPtr ProcessMemoryLimit, JobMemoryLimit, PeakProcessMemoryUsed, PeakJobMemoryUsed; }
    [StructLayout(LayoutKind.Sequential)] struct JOBOBJECT_BASIC_UI_RESTRICTIONS { public uint UIRestrictionsClass; }

    [DllImport("userenv.dll", CharSet = CharSet.Unicode)] static extern int CreateAppContainerProfile(string name, string display, string desc, IntPtr caps, uint count, out IntPtr sid);
    [DllImport("userenv.dll", CharSet = CharSet.Unicode)] static extern int DeriveAppContainerSidFromAppContainerName(string name, out IntPtr sid);
    [DllImport("advapi32.dll")] static extern IntPtr FreeSid(IntPtr sid);
    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)] static extern bool ConvertSidToStringSidW(IntPtr sid, out IntPtr s);
    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)] static extern bool ConvertStringSidToSidW(string s, out IntPtr sid);
    [DllImport("kernel32.dll")] static extern IntPtr LocalFree(IntPtr h);
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)] static extern IntPtr CreateJobObjectW(IntPtr sa, string? name);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool SetInformationJobObject(IntPtr job, int cls, ref JOBOBJECT_EXTENDED_LIMIT_INFORMATION info, uint len);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool SetInformationJobObject(IntPtr job, int cls, ref JOBOBJECT_BASIC_UI_RESTRICTIONS info, uint len);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool QueryInformationJobObject(IntPtr job, int cls, ref JOBOBJECT_EXTENDED_LIMIT_INFORMATION info, uint len, IntPtr ret);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool AssignProcessToJobObject(IntPtr job, IntPtr proc);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool TerminateJobObject(IntPtr job, uint code);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool SetHandleInformation(IntPtr h, uint mask, uint flags);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool InitializeProcThreadAttributeList(IntPtr list, int count, int flags, ref IntPtr size);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool UpdateProcThreadAttribute(IntPtr list, uint flags, IntPtr attr, IntPtr value, IntPtr size, IntPtr prev, IntPtr ret);
    [DllImport("kernel32.dll")] static extern void DeleteProcThreadAttributeList(IntPtr list);
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool CreateProcessW(string? app, StringBuilder cmd, IntPtr pa, IntPtr ta, bool inherit, uint flags, IntPtr env, string dir, ref STARTUPINFOEX si, out PROCESS_INFORMATION pi);
    [DllImport("kernel32.dll", SetLastError = true)] static extern uint ResumeThread(IntPtr t);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool TerminateProcess(IntPtr p, uint code);
    [DllImport("kernel32.dll")] static extern uint WaitForSingleObject(IntPtr h, uint ms);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool GetExitCodeProcess(IntPtr p, out uint code);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
}
