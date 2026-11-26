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
    static readonly object Gate = new(), RunGate = new();

    /// <param name="OutputLimit">T-0782 (Gegenprüfung M3/M4): Ausgabe oder Dateien im Arbeitsordner/AC-Temp wurden zu groß – der Lauf wurde abgebrochen.</param>
    public sealed record Result(int Exit, bool TimedOut, bool MemoryLimit, double Seconds, string Out, string Err, string Text, bool OutputLimit = false)
    {
        public bool Ok => Exit == 0 && !TimedOut && !OutputLimit && !MemoryLimit;
    }

    /// <summary>Grenzen eines Laufs. <paramref name="OutputBytes"/>: stdout + stderr zusammen; <paramref name="DiskBytes"/>: alles, was der Lauf in seinem
    /// Arbeitsordner und im Temp-Ordner des Containers anlegt; <paramref name="ReadTailBytes"/>: so viel wird am Ende je Ausgabe gelesen (nur das Ende).</summary>
    public sealed record Limits(int TimeoutMs = 10_000, long MemoryBytes = 512L << 20, long OutputBytes = 2L << 20, long DiskBytes = 50L << 20, int ReadTailBytes = 256 * 1024);

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
    public static string Sid(string name = ContainerName)
    {
        var hr = CreateAppContainerProfile(name, "Trendo – Code von Lernenden", "Abgeschotteter Lauf ohne Netz für „Programmieren lernen“", IntPtr.Zero, 0, out var sid);
        if (hr == unchecked((int)0x800700B7)) hr = DeriveAppContainerSidFromAppContainerName(name, out sid); // gibt es schon
        if (hr != 0) throw new Win32Exception(hr, "Der abgeschottete Bereich für Lernende ließ sich nicht anlegen.");
        try { if (!ConvertSidToStringSidW(sid, out var s)) throw new Win32Exception(); try { return Marshal.PtrToStringUni(s)!; } finally { LocalFree(s); } }
        finally { FreeSid(sid); }
    }

    /// <summary>T-0782 (Nutzerentscheidung 26.11.2026): Gibt es das Windows-App-Profil des Containers? (Ordner unter %LOCALAPPDATA%\Packages)</summary>
    [SupportedOSPlatform("windows")]
    public static bool ProfileExists(string name = ContainerName) =>
        Directory.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages", name.ToLowerInvariant()));

    /// <summary>T-0782 (Nutzerentscheidung 26.11.2026): Profil beim Deinstallieren wieder entfernen (offizielle API, ohne Admin). Gibt eine Zeile fürs Protokoll zurück.</summary>
    [SupportedOSPlatform("windows")]
    public static string RemoveProfile(string name = ContainerName)
    {
        if (!ProfileExists(name)) return $"App-Profil „{name}“: nicht vorhanden.";
        var hr = DeleteAppContainerProfile(name);
        return hr == 0 ? $"App-Profil „{name}“ entfernt." : $"App-Profil „{name}“ nicht entfernt (0x{hr:X8}).";
    }

    /// <summary>Ordner des Containers (…\Packages\&lt;name&gt;\AC) oder null.</summary>
    [SupportedOSPlatform("windows")]
    public static string? ContainerFolder(string sidText)
    {
        if (GetAppContainerFolderPath(sidText, out var p) != 0 || p == IntPtr.Zero) return null;
        try { return Marshal.PtrToStringUni(p); } finally { Marshal.FreeCoTaskMem(p); }
    }

    /// <summary>T-0782 (M4): Temp-Ordner des Containers leeren (nur dessen Inhalt; der Ordner gehört Trendos eigenem App-Profil).</summary>
    [SupportedOSPlatform("windows")]
    public static int CleanContainerTemp(string sidText)
    {
        if (ContainerFolder(sidText) is not { } ac) return 0;
        var temp = new DirectoryInfo(Path.Combine(ac, "Temp"));
        if (!temp.Exists || temp.Attributes.HasFlag(FileAttributes.ReparsePoint)) return 0;
        var n = 0;
        foreach (var e in temp.EnumerateFileSystemInfos())
            try { if (e is DirectoryInfo d && !d.Attributes.HasFlag(FileAttributes.ReparsePoint)) d.Delete(true); else e.Delete(); n++; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        return n;
    }

    static long TreeBytes(string? dir)
    {
        if (dir is null || !Directory.Exists(dir)) return 0;
        try { return new DirectoryInfo(dir).EnumerateFiles("*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint }).Sum(f => { try { return f.Length; } catch (IOException) { return 0L; } }); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return 0; }
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
    public static Result Run(string exe, IReadOnlyList<string> args, string workDir, int timeoutMs = 10_000, long memoryBytes = 512L << 20) =>
        Run(exe, args, workDir, new Limits(timeoutMs, memoryBytes));

    public static Result Run(string exe, IReadOnlyList<string> args, string workDir, Limits lim, string container = ContainerName)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Der abgeschottete Lauf für Lernende ist bisher nur unter Windows gebaut – ohne ihn startet Trendo keinen Code.");
        lock (RunGate) return RunWindows(exe, args, workDir, lim, container); // nacheinander: der Temp-Ordner des Containers wird je Lauf geleert
    }

    /// <summary>Nur das Ende einer Datei lesen (T-0782 M3: eine riesige Ausgabe wird nie ganz eingelesen).</summary>
    public static string Tail(string file, int maxBytes)
    {
        try
        {
            using var s = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var cut = s.Length > maxBytes;
            if (cut) s.Seek(-maxBytes, SeekOrigin.End);
            var buf = new byte[Math.Min(s.Length, maxBytes)]; var n = 0;
            while (n < buf.Length) { var r = s.Read(buf, n, buf.Length - n); if (r <= 0) break; n += r; }
            var t = Encoding.UTF8.GetString(buf, 0, n);
            return cut ? "[… Anfang abgeschnitten …]\n" + t.TrimStart('\uFFFD') : t;
        }
        catch (IOException) { return ""; }
    }

    [SupportedOSPlatform("windows")]
    static Result RunWindows(string exe, IReadOnlyList<string> args, string workDir, Limits lim, string container)
    {
        Directory.CreateDirectory(workDir); Directory.CreateDirectory(Path.Combine(workDir, "tmp"));
        var sidText = Sid(container);
        Grant(workDir, sidText, FileSystemRights.Modify);
        var acFolder = ContainerFolder(sidText); var acTemp = acFolder is null ? null : Path.Combine(acFolder, "Temp");
        CleanContainerTemp(sidText);
        var sw = Stopwatch.StartNew();
        IntPtr sid = IntPtr.Zero, job = IntPtr.Zero, attr = IntPtr.Zero, env = IntPtr.Zero, handles = IntPtr.Zero, caps = IntPtr.Zero, desk = IntPtr.Zero, deskSd = IntPtr.Zero;
        var pi = new PROCESS_INFORMATION();
        // T-0782 (M3): Ausgabe über Pipes – Trendo liest mit und behält nur das Ende. Liest Trendo nicht weiter, blockiert der Lerncode (kann nichts ungebremst auf die Platte schreiben).
        var pout = new System.IO.Pipes.AnonymousPipeServerStream(System.IO.Pipes.PipeDirection.In, HandleInheritability.Inheritable);
        var perr = new System.IO.Pipes.AnonymousPipeServerStream(System.IO.Pipes.PipeDirection.In, HandleInheritability.Inheritable);
        var tail = new TailReader[] { new(pout, lim.ReadTailBytes), new(perr, lim.ReadTailBytes) };
        try
        {
            if (!ConvertStringSidToSidW(sidText, out sid)) throw new Win32Exception();
            job = CreateJobObjectW(IntPtr.Zero, null); if (job == IntPtr.Zero) throw new Win32Exception();
            var jl = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
            jl.BasicLimitInformation.LimitFlags = 0x2000 | 0x0008 | 0x0100 | 0x0400; // KILL_ON_JOB_CLOSE | ACTIVE_PROCESS | PROCESS_MEMORY | DIE_ON_UNHANDLED_EXCEPTION
            jl.BasicLimitInformation.ActiveProcessLimit = 1;
            jl.ProcessMemoryLimit = (UIntPtr)(ulong)lim.MemoryBytes;
            if (!SetInformationJobObject(job, 9, ref jl, (uint)Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>())) throw new Win32Exception();
            var ui = new JOBOBJECT_BASIC_UI_RESTRICTIONS { UIRestrictionsClass = 0x01 | 0x02 | 0x04 | 0x08 | 0x10 | 0x20 | 0x40 | 0x80 };
            if (!SetInformationJobObject(job, 4, ref ui, (uint)Marshal.SizeOf<JOBOBJECT_BASIC_UI_RESTRICTIONS>())) throw new Win32Exception();

            // T-0782 (Gegenprüfung M1): eigener, unsichtbarer Desktop – GetDC(0)/GetPixel sehen nicht den echten Bildschirm, keine Fenster der Person
            var deskName = "TrendoLernen-" + Guid.NewGuid().ToString("N")[..12];
            var me = WindowsIdentity.GetCurrent().User!.Value;
            if (!ConvertStringSecurityDescriptorToSecurityDescriptorW($"D:P(A;;GA;;;{sidText})(A;;GA;;;{me})(A;;GA;;;SY)", 1, out deskSd, out _)) throw new Win32Exception();
            var sa = new SECURITY_ATTRIBUTES { nLength = Marshal.SizeOf<SECURITY_ATTRIBUTES>(), lpSecurityDescriptor = deskSd, bInheritHandle = false };
            desk = CreateDesktopW(deskName, IntPtr.Zero, IntPtr.Zero, 0, 0x01FF /*DESKTOP_ALL*/, ref sa);
            if (desk == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "Der eigene Desktop für den Lauf ließ sich nicht anlegen.");
            var station = WindowStationName() ?? "WinSta0";

            // Attribute: Container ohne Fähigkeiten (kein internetClient, kein privateNetwork) + nur die beiden Ausgabedateien vererben
            var hOut = pout.ClientSafePipeHandle.DangerousGetHandle(); var hErr = perr.ClientSafePipeHandle.DangerousGetHandle();
            IntPtr size = IntPtr.Zero; InitializeProcThreadAttributeList(IntPtr.Zero, 2, 0, ref size);
            attr = Marshal.AllocHGlobal(size); if (!InitializeProcThreadAttributeList(attr, 2, 0, ref size)) throw new Win32Exception();
            var sc = new SECURITY_CAPABILITIES { AppContainerSid = sid, Capabilities = IntPtr.Zero, CapabilityCount = 0, Reserved = 0 };
            caps = Marshal.AllocHGlobal(Marshal.SizeOf<SECURITY_CAPABILITIES>()); Marshal.StructureToPtr(sc, caps, false);
            if (!UpdateProcThreadAttribute(attr, 0, (IntPtr)0x20009 /*SECURITY_CAPABILITIES*/, caps, (IntPtr)Marshal.SizeOf<SECURITY_CAPABILITIES>(), IntPtr.Zero, IntPtr.Zero)) throw new Win32Exception();
            handles = Marshal.AllocHGlobal(IntPtr.Size * 2); Marshal.WriteIntPtr(handles, hOut); Marshal.WriteIntPtr(handles, IntPtr.Size, hErr);
            if (!UpdateProcThreadAttribute(attr, 0, (IntPtr)0x20002 /*HANDLE_LIST*/, handles, (IntPtr)(IntPtr.Size * 2), IntPtr.Zero, IntPtr.Zero)) throw new Win32Exception();
            var si = new STARTUPINFOEX(); si.StartupInfo.cb = Marshal.SizeOf<STARTUPINFOEX>(); si.StartupInfo.dwFlags = 0x100 | 0x1;
            si.StartupInfo.hStdOutput = hOut; si.StartupInfo.hStdError = hErr; si.lpAttributeList = attr;
            si.StartupInfo.lpDesktop = station + "\\" + deskName;

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
            pout.DisposeLocalCopyOfClientHandle(); perr.DisposeLocalCopyOfClientHandle(); // nur das Kind hält die Schreib-Enden
            foreach (var t in tail) t.Start();
            if (!AssignProcessToJobObject(job, pi.hProcess)) { TerminateProcess(pi.hProcess, 1); throw new Win32Exception(); }
            ResumeThread(pi.hThread);

            // T-0782 (M3/M4): während des Laufs Ausgabe und Platten-Nutzung überwachen – beim Überschreiten sofort abbrechen
            var timedOut = false; var outLimit = false; var lastDisk = 0L; var lastDiskCheck = sw.ElapsedMilliseconds;
            while (true)
            {
                if (WaitForSingleObject(pi.hProcess, 25) == 0) break;
                if (sw.ElapsedMilliseconds >= lim.TimeoutMs) { timedOut = true; break; }
                if (tail[0].Total + tail[1].Total > lim.OutputBytes) { outLimit = true; break; }
                if (sw.ElapsedMilliseconds - lastDiskCheck >= 200)
                {
                    lastDiskCheck = sw.ElapsedMilliseconds;
                    lastDisk = TreeBytes(workDir) + TreeBytes(acTemp);
                    if (lastDisk > lim.DiskBytes) { outLimit = true; break; }
                }
            }
            if (timedOut || outLimit) { TerminateJobObject(job, 0xEE); WaitForSingleObject(pi.hProcess, 5000); }
            GetExitCodeProcess(pi.hProcess, out var code);
            var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
            QueryInformationJobObject(job, 9, ref info, (uint)Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>(), IntPtr.Zero);
            foreach (var t in tail) t.Wait(3000);
            sw.Stop();
            // M4: auch nach dem Ende prüfen – ein schneller Lauf kann zwischen zwei Messungen fertig sein
            if (!timedOut && !outLimit && (tail[0].Total + tail[1].Total > lim.OutputBytes || TreeBytes(workDir) + TreeBytes(acTemp) > lim.DiskBytes)) outLimit = true;
            var outText = tail[0].Text; var errText = tail[1].Text;
            // N2: die Speichergrenze des Jobs lässt Anforderungen scheitern – Python meldet dann MemoryError, ohne die Grenze ganz zu erreichen
            var memHit = !timedOut && !outLimit && ((long)(ulong)info.PeakProcessMemoryUsed >= lim.MemoryBytes * 95 / 100 || errText.Contains("MemoryError", StringComparison.Ordinal));
            var exit = unchecked((int)code);
            var text = timedOut ? $"abgebrochen: Zeitgrenze von {lim.TimeoutMs / 1000.0:0.#} s überschritten"
                : outLimit ? $"abgebrochen: zu viel Ausgabe oder zu große Dateien (Grenze {lim.OutputBytes >> 20} MB Ausgabe, {lim.DiskBytes >> 20} MB Dateien)"
                : memHit ? $"abgebrochen: Speichergrenze ({lim.MemoryBytes >> 20} MB) erreicht"
                : exit == 0 ? "ok" : Sys.Sandbox.CrashName(exit) is { } crash ? $"abgestürzt ({crash})" : $"beendet mit Fehlercode {exit}";
            return new Result(exit, timedOut, memHit, sw.Elapsed.TotalSeconds, outText, errText, text, outLimit);
        }
        finally
        {
            pout.Dispose(); perr.Dispose();
            if (pi.hThread != IntPtr.Zero) CloseHandle(pi.hThread);
            if (pi.hProcess != IntPtr.Zero) CloseHandle(pi.hProcess);
            if (job != IntPtr.Zero) CloseHandle(job);
            if (desk != IntPtr.Zero) CloseDesktop(desk);
            if (deskSd != IntPtr.Zero) LocalFree(deskSd);
            if (attr != IntPtr.Zero) { DeleteProcThreadAttributeList(attr); Marshal.FreeHGlobal(attr); }
            if (handles != IntPtr.Zero) Marshal.FreeHGlobal(handles);
            if (caps != IntPtr.Zero) Marshal.FreeHGlobal(caps);
            if (env != IntPtr.Zero) Marshal.FreeHGlobal(env);
            if (sid != IntPtr.Zero) LocalFree(sid);
            CleanContainerTemp(sidText); // M4: nach jedem Lauf leer
        }
    }

    /// <summary>Liest eine Pipe bis zum Ende und behält nur die letzten Bytes (T-0782 M3).</summary>
    sealed class TailReader(Stream s, int keep)
    {
        readonly byte[] _ring = new byte[Math.Max(1024, keep)]; long _total; Task? _t;
        public long Total => Interlocked.Read(ref _total);
        public void Start() => _t = Task.Run(() =>
        {
            var buf = new byte[65536];
            try { int n; while ((n = s.Read(buf, 0, buf.Length)) > 0) lock (_ring) { for (var i = 0; i < n; i++) _ring[(int)((_total + i) % _ring.Length)] = buf[i]; Interlocked.Add(ref _total, n); } }
            catch (Exception e) when (e is IOException or ObjectDisposedException) { }
        });
        public void Wait(int ms) { try { _t?.Wait(ms); } catch (AggregateException) { } }
        public string Text
        {
            get
            {
                lock (_ring)
                {
                    var n = (int)Math.Min(_total, _ring.Length); var start = _total - n; var bytes = new byte[n];
                    for (var i = 0; i < n; i++) bytes[i] = _ring[(int)((start + i) % _ring.Length)];
                    var t = Encoding.UTF8.GetString(bytes);
                    return _total > _ring.Length ? "[… Anfang abgeschnitten …]\n" + t.TrimStart('\uFFFD') : t;
                }
            }
        }
    }

    [SupportedOSPlatform("windows")]
    static string? WindowStationName()
    {
        var ws = GetProcessWindowStation(); if (ws == IntPtr.Zero) return null;
        var buf = Marshal.AllocHGlobal(512);
        try { return GetUserObjectInformationW(ws, 2 /*UOI_NAME*/, buf, 512, out _) ? Marshal.PtrToStringUni(buf) : null; }
        finally { Marshal.FreeHGlobal(buf); }
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
    [StructLayout(LayoutKind.Sequential)] struct SECURITY_ATTRIBUTES { public int nLength; public IntPtr lpSecurityDescriptor; public bool bInheritHandle; }
    [StructLayout(LayoutKind.Sequential)]
    struct JOBOBJECT_BASIC_LIMIT_INFORMATION { public long PerProcessUserTimeLimit, PerJobUserTimeLimit; public uint LimitFlags; public UIntPtr MinimumWorkingSetSize, MaximumWorkingSetSize; public uint ActiveProcessLimit; public UIntPtr Affinity; public uint PriorityClass, SchedulingClass; }
    [StructLayout(LayoutKind.Sequential)] struct IO_COUNTERS { public ulong ReadOperationCount, WriteOperationCount, OtherOperationCount, ReadTransferCount, WriteTransferCount, OtherTransferCount; }
    [StructLayout(LayoutKind.Sequential)]
    struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION { public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation; public IO_COUNTERS IoInfo; public UIntPtr ProcessMemoryLimit, JobMemoryLimit, PeakProcessMemoryUsed, PeakJobMemoryUsed; }
    [StructLayout(LayoutKind.Sequential)] struct JOBOBJECT_BASIC_UI_RESTRICTIONS { public uint UIRestrictionsClass; }

    [DllImport("userenv.dll", CharSet = CharSet.Unicode)] static extern int CreateAppContainerProfile(string name, string display, string desc, IntPtr caps, uint count, out IntPtr sid);
    [DllImport("userenv.dll", CharSet = CharSet.Unicode)] static extern int DeriveAppContainerSidFromAppContainerName(string name, out IntPtr sid);
    [DllImport("userenv.dll", CharSet = CharSet.Unicode)] static extern int DeleteAppContainerProfile(string name);
    [DllImport("userenv.dll", CharSet = CharSet.Unicode)] static extern int GetAppContainerFolderPath(string sid, out IntPtr path);
    [DllImport("advapi32.dll")] static extern IntPtr FreeSid(IntPtr sid);
    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)] static extern bool ConvertSidToStringSidW(IntPtr sid, out IntPtr s);
    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)] static extern bool ConvertStringSidToSidW(string s, out IntPtr sid);
    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)] static extern bool ConvertStringSecurityDescriptorToSecurityDescriptorW(string s, uint rev, out IntPtr sd, out uint size);
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)] static extern IntPtr CreateDesktopW(string name, IntPtr device, IntPtr devmode, uint flags, uint access, ref SECURITY_ATTRIBUTES sa);
    [DllImport("user32.dll", SetLastError = true)] static extern bool CloseDesktop(IntPtr desk);
    [DllImport("user32.dll", SetLastError = true)] static extern IntPtr GetProcessWindowStation();
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)] static extern bool GetUserObjectInformationW(IntPtr obj, int index, IntPtr info, int len, out int needed);
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
