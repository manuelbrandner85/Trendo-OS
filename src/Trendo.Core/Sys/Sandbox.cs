using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Trendo.Core.Sys;

/// <summary>Paket 01 – Studio 4, S4-06 (07.11.2026): abgeschottete Laufumgebung für fremden Code (z. B. Audio-Plugins), ohne Adminrechte.
/// Windows: eigener Prozess mit <b>niedriger Integritätsstufe</b> (darf keine Dateien des Nutzers verändern, keine Programme mit normaler Stufe
/// steuern), in einem <b>Job-Objekt</b> (genau ein Prozess – keine weiteren Programme –, Speichergrenze, kein Fehlerdialog, wird beim Ende mit
/// beendet, keine Zwischenablage/Systemeinstellungen/Abmelden), mit <b>Zeitgrenze</b> und eigenem Arbeitsordner (nur dort Schreibrecht).
/// Umgebung minimal (TEMP, APPDATA, LOCALAPPDATA im Arbeitsordner). Ehrlich: Lesen bleibt möglich, das Netz ist nicht gesperrt
/// (bräuchte eine Firewall-Regel = Systemänderung).</summary>
public static class Sandbox
{
    static readonly object InheritLock = new();

    public sealed record Options(int TimeoutMs = 120_000, long MemoryBytes = 2L << 30, IReadOnlyDictionary<string, string>? Env = null);

    public sealed record Result(int Exit, bool TimedOut, bool Crashed, bool MemoryLimit, long PeakMemory, double Seconds, string Out, string Err, string Text)
    {
        public bool Ok => Exit == 0 && !TimedOut && !Crashed;
    }

    /// <summary>Windows-Ausnahmecodes, die einen Absturz bedeuten (NTSTATUS mit Schweregrad „Fehler“).</summary>
    public static string? CrashName(int exit) => unchecked((uint)exit) switch
    {
        0xC0000005 => "Zugriffsverletzung",
        0xC0000409 => "Stapelüberlauf/Schutzverletzung (Fast-Fail)",
        0xC00000FD => "Stapelüberlauf",
        0xC000001D => "ungültiger Befehl",
        0xC0000094 => "Division durch null",
        0xC0000017 => "kein Speicher mehr",
        0xC0000135 => "fehlende DLL",
        0xC0000142 => "Start der DLL fehlgeschlagen",
        0xC0000374 => "Heap beschädigt",
        0x80131623 => "Notabbruch der .NET-Laufzeit",
        0x40010004 => "von außen beendet",
        var u when u >= 0xC0000000 => $"Ausnahme 0x{u:X8}",
        _ => null,
    };

    /// <summary>Arbeitsordner vorbereiten: anlegen und mit „niedriger Integrität“ kennzeichnen, damit nur hier geschrieben werden darf.</summary>
    [SupportedOSPlatform("windows")]
    public static void PrepareFolder(string dir)
    {
        Directory.CreateDirectory(dir);
        foreach (var sub in new[] { "tmp", "appdata", "localappdata" }) Directory.CreateDirectory(Path.Combine(dir, sub));
        if (!ConvertStringSecurityDescriptorToSecurityDescriptorW("S:(ML;OICI;NW;;;LW)", 1, out var sd, out _)) throw new Win32Exception();
        try
        {
            if (!GetSecurityDescriptorSacl(sd, out _, out var sacl, out _)) throw new Win32Exception();
            var err = SetNamedSecurityInfoW(dir, 1 /*SE_FILE_OBJECT*/, 0x10 /*LABEL_SECURITY_INFORMATION*/, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, sacl);
            if (err != 0) throw new Win32Exception((int)err);
        }
        finally { LocalFree(sd); }
    }

    /// <summary>Programm abgeschottet ausführen. Ausgaben (stdout/stderr) landen in Dateien im Arbeitsordner.</summary>
    public static Result Run(string exe, IReadOnlyList<string> args, string workDir, Options? opt = null)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Die abgeschottete Laufumgebung gibt es bisher nur unter Windows – ohne sie lädt Trendo keinen fremden Code.");
        return RunWindows(exe, args, workDir, opt ?? new Options());
    }

    [SupportedOSPlatform("windows")]
    static Result RunWindows(string exe, IReadOnlyList<string> args, string workDir, Options opt)
    {
        PrepareFolder(workDir);
        var outFile = Path.Combine(workDir, "ausgabe.txt"); var errFile = Path.Combine(workDir, "fehler.txt");
        var sw = Stopwatch.StartNew();
        IntPtr token = IntPtr.Zero, low = IntPtr.Zero, sid = IntPtr.Zero, job = IntPtr.Zero, attr = IntPtr.Zero, env = IntPtr.Zero, tml = IntPtr.Zero, handles = IntPtr.Zero;
        var pi = new PROCESS_INFORMATION();
        using var fout = new FileStream(outFile, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
        using var ferr = new FileStream(errFile, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
        try
        {
            // 1) Kopie des eigenen Zugangs mit niedriger Integritätsstufe
            if (!OpenProcessToken(GetCurrentProcess(), 0x0002 | 0x0008 | 0x0080 | 0x0001 /*DUPLICATE|QUERY|ADJUST_DEFAULT|ASSIGN_PRIMARY*/, out token)) throw new Win32Exception();
            if (!DuplicateTokenEx(token, 0x02000000 /*MAXIMUM_ALLOWED*/, IntPtr.Zero, 2 /*SecurityImpersonation*/, 1 /*TokenPrimary*/, out low)) throw new Win32Exception();
            if (!ConvertStringSidToSidW("S-1-16-4096", out sid)) throw new Win32Exception();
            var sidLen = GetLengthSid(sid); tml = Marshal.AllocHGlobal(16 + sidLen);
            Marshal.WriteIntPtr(tml, sid); Marshal.WriteInt32(tml, IntPtr.Size, 0x20 /*SE_GROUP_INTEGRITY*/);
            if (!SetTokenInformation(low, 25 /*TokenIntegrityLevel*/, tml, (uint)(16 + sidLen))) throw new Win32Exception();

            // 2) Job-Objekt: ein Prozess, Speichergrenze, kein Fehlerdialog, beim Schließen beenden; Oberflächen-Grenzen
            job = CreateJobObjectW(IntPtr.Zero, null); if (job == IntPtr.Zero) throw new Win32Exception();
            var lim = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
            lim.BasicLimitInformation.LimitFlags = 0x2000 | 0x0008 | 0x0100 | 0x0400; // KILL_ON_JOB_CLOSE | ACTIVE_PROCESS | PROCESS_MEMORY | DIE_ON_UNHANDLED_EXCEPTION
            lim.BasicLimitInformation.ActiveProcessLimit = 1;
            lim.ProcessMemoryLimit = (UIntPtr)(ulong)opt.MemoryBytes;
            if (!SetInformationJobObject(job, 9, ref lim, (uint)Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>())) throw new Win32Exception();
            var ui = new JOBOBJECT_BASIC_UI_RESTRICTIONS { UIRestrictionsClass = 0x02 | 0x04 | 0x08 | 0x10 | 0x20 | 0x80 }; // Zwischenablage, Systemparameter, Anzeige, globale Atome, Abmelden
            if (!SetInformationJobObject(job, 4, ref ui, (uint)Marshal.SizeOf<JOBOBJECT_BASIC_UI_RESTRICTIONS>())) throw new Win32Exception();

            // 3) Nur die beiden Ausgabedateien werden vererbt (Handle-Liste), sonst nichts
            // Befund 07.11.2026 (Gesamttestlauf): vererbbare Handles erbt auch jeder andere Prozess, den Trendo gleichzeitig startet – dann
            // bleiben die Dateien offen. Darum vererbbar nur unmittelbar um CreateProcessAsUser (unten) und sofort danach wieder zurück.
            var hOut = fout.SafeFileHandle.DangerousGetHandle(); var hErr = ferr.SafeFileHandle.DangerousGetHandle();
            IntPtr size = IntPtr.Zero; InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref size);
            attr = Marshal.AllocHGlobal(size); if (!InitializeProcThreadAttributeList(attr, 1, 0, ref size)) throw new Win32Exception();
            handles = Marshal.AllocHGlobal(IntPtr.Size * 2); Marshal.WriteIntPtr(handles, hOut); Marshal.WriteIntPtr(handles, IntPtr.Size, hErr);
            if (!UpdateProcThreadAttribute(attr, 0, (IntPtr)0x20002 /*HANDLE_LIST*/, handles, (IntPtr)(IntPtr.Size * 2), IntPtr.Zero, IntPtr.Zero)) throw new Win32Exception();
            var si = new STARTUPINFOEX(); si.StartupInfo.cb = Marshal.SizeOf<STARTUPINFOEX>(); si.StartupInfo.dwFlags = 0x100 | 0x1 /*USESTDHANDLES|USESHOWWINDOW*/;
            si.StartupInfo.hStdOutput = hOut; si.StartupInfo.hStdError = hErr; si.lpAttributeList = attr;

            // 4) Umgebung: nur das Nötigste, Schreiborte im Arbeitsordner
            var e = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["SystemRoot"] = Environment.GetEnvironmentVariable("SystemRoot") ?? @"C:\Windows",
                ["windir"] = Environment.GetEnvironmentVariable("windir") ?? @"C:\Windows",
                ["SystemDrive"] = Environment.GetEnvironmentVariable("SystemDrive") ?? "C:",
                ["PATH"] = Environment.GetFolderPath(Environment.SpecialFolder.System) + ";" + Path.GetDirectoryName(Path.GetFullPath(exe)),
                ["PATHEXT"] = ".COM;.EXE;.BAT;.CMD",
                ["COMSPEC"] = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe"),
                ["TEMP"] = Path.Combine(workDir, "tmp"), ["TMP"] = Path.Combine(workDir, "tmp"),
                ["APPDATA"] = Path.Combine(workDir, "appdata"), ["LOCALAPPDATA"] = Path.Combine(workDir, "localappdata"),
                ["USERPROFILE"] = workDir, ["HOMEDRIVE"] = Path.GetPathRoot(workDir)!.TrimEnd('\\'), ["HOMEPATH"] = workDir[2..],
                ["NUMBER_OF_PROCESSORS"] = Environment.ProcessorCount.ToString(),
            };
            if (opt.Env is not null) foreach (var (k, v) in opt.Env) e[k] = v;
            var block = new StringBuilder(); foreach (var (k, v) in e) block.Append(k).Append('=').Append(v).Append('\0'); block.Append('\0');
            env = Marshal.StringToHGlobalUni(block.ToString());

            // 5) Starten (angehalten), in den Job, dann weiterlaufen lassen
            var cmd = new StringBuilder(Quote(exe)); foreach (var a in args) cmd.Append(' ').Append(Quote(a));
            const uint flags = 0x00000004 | 0x08000000 | 0x00000400 | 0x00080000; // SUSPENDED | NO_WINDOW | UNICODE_ENVIRONMENT | EXTENDED_STARTUPINFO
            bool started;
            lock (InheritLock)
            {
                SetHandleInformation(hOut, 1, 1); SetHandleInformation(hErr, 1, 1);
                started = CreateProcessAsUserW(low, null, cmd, IntPtr.Zero, IntPtr.Zero, true, flags, env, workDir, ref si, out pi);
                var err = Marshal.GetLastWin32Error();
                SetHandleInformation(hOut, 1, 0); SetHandleInformation(hErr, 1, 0);
                if (!started) throw new Win32Exception(err);
            }
            if (!AssignProcessToJobObject(job, pi.hProcess)) { TerminateProcess(pi.hProcess, 1); throw new Win32Exception(); }
            ResumeThread(pi.hThread);
            var waited = WaitForSingleObject(pi.hProcess, (uint)opt.TimeoutMs);
            var timedOut = waited == 0x102; // WAIT_TIMEOUT
            if (timedOut) { TerminateJobObject(job, 0xEE); WaitForSingleObject(pi.hProcess, 5000); }
            GetExitCodeProcess(pi.hProcess, out var code);
            var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
            QueryInformationJobObject(job, 9, ref info, (uint)Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>(), IntPtr.Zero);
            var peak = (long)(ulong)info.PeakProcessMemoryUsed;
            fout.Flush(); ferr.Flush();
            sw.Stop();
            var exit = unchecked((int)code); var crash = !timedOut ? CrashName(exit) : null;
            var memHit = peak >= opt.MemoryBytes * 95 / 100;
            string Read(string f) { try { using var s = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite); using var r = new StreamReader(s, Encoding.UTF8); var t = r.ReadToEnd(); return t.Length > 20000 ? t[^20000..] : t; } catch (IOException) { return ""; } }
            var text = timedOut ? $"abgebrochen: Zeitgrenze von {opt.TimeoutMs / 1000.0:0.#} s überschritten"
                : memHit ? $"abgebrochen: Speichergrenze ({opt.MemoryBytes >> 20} MB) erreicht" + (crash is not null ? $" ({crash})" : "")
                : crash is not null ? $"abgestürzt ({crash}, Code 0x{unchecked((uint)exit):X8}) – nur der abgeschottete Prozess ist betroffen"
                : exit == 0 ? "ok" : $"beendet mit Fehlercode {exit}";
            return new Result(exit, timedOut, crash is not null || memHit, memHit, peak, sw.Elapsed.TotalSeconds, Read(outFile), Read(errFile), text);
        }
        finally
        {
            if (pi.hThread != IntPtr.Zero) CloseHandle(pi.hThread);
            if (pi.hProcess != IntPtr.Zero) CloseHandle(pi.hProcess);
            if (job != IntPtr.Zero) CloseHandle(job); // beendet alles, was noch läuft
            if (attr != IntPtr.Zero) { DeleteProcThreadAttributeList(attr); Marshal.FreeHGlobal(attr); }
            if (handles != IntPtr.Zero) Marshal.FreeHGlobal(handles);
            if (env != IntPtr.Zero) Marshal.FreeHGlobal(env);
            if (tml != IntPtr.Zero) Marshal.FreeHGlobal(tml);
            if (sid != IntPtr.Zero) LocalFree(sid);
            if (low != IntPtr.Zero) CloseHandle(low);
            if (token != IntPtr.Zero) CloseHandle(token);
        }
    }

    /// <summary>Argument nach den Windows-Regeln (CommandLineToArgvW) in Anführungszeichen setzen.</summary>
    public static string Quote(string a)
    {
        if (a.Length > 0 && a.IndexOfAny([' ', '\t', '"']) < 0) return a;
        var sb = new StringBuilder("\""); var bs = 0;
        foreach (var c in a)
        {
            if (c == '\\') { bs++; continue; }
            if (c == '"') { sb.Append('\\', bs * 2 + 1).Append('"'); bs = 0; continue; }
            sb.Append('\\', bs).Append(c); bs = 0;
        }
        return sb.Append('\\', bs * 2).Append('"').ToString();
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
    [StructLayout(LayoutKind.Sequential)]
    struct JOBOBJECT_BASIC_LIMIT_INFORMATION { public long PerProcessUserTimeLimit, PerJobUserTimeLimit; public uint LimitFlags; public UIntPtr MinimumWorkingSetSize, MaximumWorkingSetSize; public uint ActiveProcessLimit; public UIntPtr Affinity; public uint PriorityClass, SchedulingClass; }
    [StructLayout(LayoutKind.Sequential)] struct IO_COUNTERS { public ulong ReadOperationCount, WriteOperationCount, OtherOperationCount, ReadTransferCount, WriteTransferCount, OtherTransferCount; }
    [StructLayout(LayoutKind.Sequential)]
    struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION { public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation; public IO_COUNTERS IoInfo; public UIntPtr ProcessMemoryLimit, JobMemoryLimit, PeakProcessMemoryUsed, PeakJobMemoryUsed; }
    [StructLayout(LayoutKind.Sequential)] struct JOBOBJECT_BASIC_UI_RESTRICTIONS { public uint UIRestrictionsClass; }

    [DllImport("kernel32.dll")] static extern IntPtr GetCurrentProcess();
    [DllImport("advapi32.dll", SetLastError = true)] static extern bool OpenProcessToken(IntPtr h, uint access, out IntPtr token);
    [DllImport("advapi32.dll", SetLastError = true)] static extern bool DuplicateTokenEx(IntPtr t, uint access, IntPtr sa, int level, int type, out IntPtr nt);
    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)] static extern bool ConvertStringSidToSidW(string s, out IntPtr sid);
    [DllImport("advapi32.dll")] static extern int GetLengthSid(IntPtr sid);
    [DllImport("advapi32.dll", SetLastError = true)] static extern bool SetTokenInformation(IntPtr t, int cls, IntPtr info, uint len);
    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)] static extern bool ConvertStringSecurityDescriptorToSecurityDescriptorW(string s, uint rev, out IntPtr sd, out uint size);
    [DllImport("advapi32.dll", SetLastError = true)] static extern bool GetSecurityDescriptorSacl(IntPtr sd, out bool present, out IntPtr sacl, out bool defaulted);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)] static extern uint SetNamedSecurityInfoW(string name, int type, uint info, IntPtr owner, IntPtr group, IntPtr dacl, IntPtr sacl);
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
    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool CreateProcessAsUserW(IntPtr token, string? app, StringBuilder cmd, IntPtr pa, IntPtr ta, bool inherit, uint flags, IntPtr env, string dir, ref STARTUPINFOEX si, out PROCESS_INFORMATION pi);
    [DllImport("kernel32.dll", SetLastError = true)] static extern uint ResumeThread(IntPtr t);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool TerminateProcess(IntPtr p, uint code);
    [DllImport("kernel32.dll")] static extern uint WaitForSingleObject(IntPtr h, uint ms);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool GetExitCodeProcess(IntPtr p, out uint code);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
}
