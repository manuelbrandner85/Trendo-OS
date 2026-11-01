using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Trendo.Core;

/// <summary>Windows-Job-Objekt mit „beim Schließen alles beenden“: Ein hineingelegter Prozess und alle seine Unterprozesse (z. B. Chrome-Hilfsprozesse)
/// enden spätestens, wenn das Job-Objekt geschlossen wird – auch wenn Trendo abstürzt. Auf anderen Systemen tut es nichts.</summary>
public sealed class ProcessJob : IDisposable
{
    nint _handle;

    public ProcessJob()
    {
        if (!OperatingSystem.IsWindows()) return;
        _handle = CreateJobObjectW(0, null);
        if (_handle == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
        var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION { BasicLimitInformation = new() { LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE } };
        if (!SetInformationJobObject(_handle, JobObjectExtendedLimitInformation, ref info, (uint)Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>()))
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    /// <summary>Prozess hineinlegen; false, wenn es nicht ging (dann bleibt nur das normale Beenden).</summary>
    public bool Add(Process p)
    {
        if (_handle == 0) return false;
        try { return AssignProcessToJobObject(_handle, p.Handle); } catch (Exception e) when (e is InvalidOperationException or Win32Exception) { return false; }
    }

    public void Dispose() { if (_handle != 0) { CloseHandle(_handle); _handle = 0; } }

    const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x2000;
    const int JobObjectExtendedLimitInformation = 9;

    [StructLayout(LayoutKind.Sequential)]
    struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public long PerProcessUserTimeLimit, PerJobUserTimeLimit;
        public uint LimitFlags;
        public nuint MinimumWorkingSetSize, MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public nuint Affinity;
        public uint PriorityClass, SchedulingClass;
    }
    [StructLayout(LayoutKind.Sequential)]
    struct IO_COUNTERS { public ulong ReadOperationCount, WriteOperationCount, OtherOperationCount, ReadTransferCount, WriteTransferCount, OtherTransferCount; }
    [StructLayout(LayoutKind.Sequential)]
    struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public nuint ProcessMemoryLimit, JobMemoryLimit, PeakProcessMemoryUsed, PeakJobMemoryUsed;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)] private static extern nint CreateJobObjectW(nint attributes, string? name);
    [DllImport("kernel32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetInformationJobObject(nint job, int infoClass, ref JOBOBJECT_EXTENDED_LIMIT_INFORMATION info, uint length);
    [DllImport("kernel32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool AssignProcessToJobObject(nint job, nint process);
    [DllImport("kernel32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseHandle(nint handle);
}
