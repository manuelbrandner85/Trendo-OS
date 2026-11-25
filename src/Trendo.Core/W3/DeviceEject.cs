using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace Trendo.Core;

/// <summary>T-0764 (SYS-07): Ergebnis von „Sicher auswerfen“. <see cref="Ok"/> nur, wenn Windows das Gerät wirklich abgemeldet hat.</summary>
public sealed record EjectResult(bool Ok, string Message);

/// <summary>T-0764 (Gegenprüfung M4): Wohin zeigt ein Pfad wirklich (Verknüpfung, Junction, subst aufgelöst), und auf welchem physischen
/// Datenträger liegt ein Laufwerk? Ausgeworfen wird das ganze Gerät – zwei Partitionen auf demselben Stick zählen also gemeinsam.</summary>
public interface IDiskResolver
{
    /// <summary>Endgültiger Pfad (bei Fehlern der Pfad selbst).</summary>
    string FinalPath(string path);
    /// <summary>Physischer Datenträger (Gerätetyp, Gerätenummer) eines Laufwerks, null = unbekannt.</summary>
    (int Type, int Number)? DiskOf(string driveRoot);
}

/// <summary>Ohne Betriebssystem-Hilfe: Pfad bleibt, Datenträger unbekannt (Vergleich nur über den Buchstaben).</summary>
public sealed class PlainDiskResolver : IDiskResolver
{
    public string FinalPath(string path) => Path.GetFullPath(path);
    public (int Type, int Number)? DiskOf(string driveRoot) => null;
}

/// <summary>Auswerfen eines Wechseldatenträgers (Tests ersetzen das Betriebssystem).</summary>
public interface IDriveEjector
{
    EjectResult Eject(string driveRoot);
}

/// <summary>T-0764 (SYS-07): Wechseldatenträger sicher auswerfen und Gerätezustände verständlich erklären.
/// Ausgeworfen wird nur ein Wechseldatenträger – nie das Systemlaufwerk und nie das Laufwerk, auf dem Trendo selbst oder seine Daten liegen.
/// Unter Windows über den Geräte-Manager-Weg <c>CM_Request_Device_EjectW</c> (cfgmgr32), ohne Administratorrechte – genau wie
/// „Hardware sicher entfernen“ in der Taskleiste. Ist noch eine Datei offen, lehnt Windows ab und Trendo nennt den Grund.</summary>
public static class DeviceEject
{
    /// <summary>Darf dieses Laufwerk ausgeworfen werden? null = ja, sonst der Grund (es wird dann nichts versucht).</summary>
    /// <param name="protectedPaths">Pfade, deren Laufwerk nie ausgeworfen wird (Windows-Ordner, Trendo-Programm, Trendo-Daten).</param>
    /// <param name="resolver">T-0764: löst Schutzpfade auf (Junction, Verknüpfung, subst) und vergleicht den physischen Datenträger –
    /// ohne Angabe unter Windows der echte, sonst nur der Laufwerksbuchstabe.</param>
    public static string? Refuse(string driveRoot, DriveType type, IEnumerable<string> protectedPaths, IDiskResolver? resolver = null)
    {
        var root = NormalizeRoot(driveRoot);
        if (root is null) return "Das ist kein Laufwerk.";
        resolver ??= DefaultResolver();
        var targetDisk = resolver.DiskOf(root);
        var why = $"Laufwerk {root} wird nicht ausgeworfen: darauf (oder auf demselben Gerät) laufen Windows oder Trendo selbst.";
        foreach (var p in protectedPaths.Where(p => !string.IsNullOrWhiteSpace(p)))
            foreach (var path in new[] { Path.GetFullPath(p), resolver.FinalPath(p) }.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var pr = NormalizeRoot(Path.GetPathRoot(path) ?? "");
                if (pr is null) continue;
                if (string.Equals(pr, root, StringComparison.OrdinalIgnoreCase)) return why;
                if (targetDisk is { } td && resolver.DiskOf(pr) is { } pd && pd == td) return why; // zwei Partitionen auf demselben Gerät
            }
        if (type != DriveType.Removable) return $"Laufwerk {root} ist kein Wechseldatenträger – Trendo wirft nur USB-Sticks und Speicherkarten aus.";
        return null;
    }

    /// <summary>„E:\“, „E:“ oder „e“ → „E:\“; sonst null.</summary>
    public static string? NormalizeRoot(string s)
    {
        s = (s ?? "").Trim();
        if (s.Length is < 1 or > 3 || !char.IsAsciiLetter(s[0])) return null;
        if (s.Length >= 2 && s[1] != ':') return null;
        if (s.Length == 3 && s[2] is not ('\\' or '/')) return null;
        return char.ToUpperInvariant(s[0]) + ":\\";
    }

    /// <summary>Prüfen und auswerfen. Ohne Freigabe für dieses Laufwerk (siehe <see cref="Refuse"/>) wird das Betriebssystem gar nicht erst gefragt.</summary>
    public static IDiskResolver DefaultResolver() => OperatingSystem.IsWindows() ? new WindowsDiskResolver() : new PlainDiskResolver();

    public static EjectResult Eject(string driveRoot, DriveType type, IEnumerable<string> protectedPaths, IDriveEjector ejector, AuditLog? audit = null, string actor = "app", IDiskResolver? resolver = null)
    {
        if (Refuse(driveRoot, type, protectedPaths, resolver) is { } why)
        {
            audit?.Append(actor, "device.eject.refused", $"{driveRoot}: {why}");
            return new(false, why);
        }
        var root = NormalizeRoot(driveRoot)!;
        var r = ejector.Eject(root);
        audit?.Append(actor, r.Ok ? "device.eject" : "device.eject.failed", $"{root} {r.Message}");
        return r;
    }

    /// <summary>Gerätezustand aus „Get-PnpDevice … Problem“ (Name wie CM_PROB_FAILED_INSTALL oder die Zahl) → verständlicher nächster Schritt.
    /// Leer, wenn alles in Ordnung ist.</summary>
    public static string NextStep(string? problem, string status = "")
    {
        var p = (problem ?? "").Trim();
        var code = int.TryParse(p, out var n) ? n : -1;
        bool Is(string name, int num) => p.Equals(name, StringComparison.OrdinalIgnoreCase) || code == num;
        if (p.Length == 0 || Is("CM_PROB_NONE", 0))
            return status is "OK" or "" ? "" : "Windows meldet ein Problem ohne nähere Angabe. Gerät abziehen und neu einstecken, sonst den PC neu starten.";
        if (Is("CM_PROB_FAILED_INSTALL", 28) || Is("CM_PROB_NOT_CONFIGURED", 1) || Is("CM_PROB_REINSTALL", 18))
            return "Der Treiber fehlt. Unter Windows-Einstellungen → Windows Update → Erweiterte Optionen → Optionale Updates nach einem Treiber suchen, sonst beim Hersteller laden.";
        if (Is("CM_PROB_DISABLED", 22) || Is("CM_PROB_HARDWARE_DISABLED", 29))
            return "Das Gerät ist ausgeschaltet. Im Geräte-Manager per Rechtsklick „Gerät aktivieren“ wählen (bei „im BIOS deaktiviert“ dort einschalten).";
        if (Is("CM_PROB_FAILED_START", 10))
            return "Das Gerät startet nicht. Abziehen und neu einstecken; hilft das nicht, den Treiber beim Hersteller aktualisieren.";
        if (Is("CM_PROB_FAILED_POST_START", 43))
            return "Windows hat das Gerät nach einem Fehler angehalten. Abziehen, an einem anderen USB-Anschluss einstecken und den PC neu starten.";
        if (Is("CM_PROB_NEED_RESTART", 14))
            return "Das Gerät ist bereit, sobald der PC neu gestartet ist.";
        if (Is("CM_PROB_PHANTOM", 45))
            return "Das Gerät ist gerade nicht angeschlossen. Einstecken, dann neu suchen.";
        if (Is("CM_PROB_DRIVER_BLOCKED", 48) || Is("CM_PROB_UNSIGNED_DRIVER", 52))
            return "Windows blockiert den Treiber, weil er unsicher oder nicht signiert ist. Einen aktuellen, signierten Treiber beim Hersteller laden.";
        if (Is("CM_PROB_FAILED_DRIVER_ENTRY", 37) || Is("CM_PROB_DRIVER_FAILED_LOAD", 39) || Is("CM_PROB_DRIVER_FAILED_PRIOR_UNLOAD", 38) || Is("CM_PROB_FAILED_ADD", 31))
            return "Der Treiber lässt sich nicht laden. Den PC neu starten; hilft das nicht, den Treiber beim Hersteller neu installieren.";
        if (Is("CM_PROB_OUT_OF_MEMORY", 3) || Is("CM_PROB_NORMAL_CONFLICT", 12))
            return "Das Gerät bekommt keine freien Ressourcen. Andere Geräte abziehen oder den PC neu starten.";
        return $"Windows meldet „{p}“. Gerät abziehen und neu einstecken; hilft das nicht, den Treiber beim Hersteller aktualisieren.";
    }

    /// <summary>Text zu einem Windows-Einspruch beim Auswerfen (PNP_VETO_TYPE).</summary>
    public static string VetoText(int vetoType, string vetoName) => vetoType switch
    {
        5 => "Auf dem Laufwerk ist noch etwas geöffnet (eine Datei, ein Ordner im Explorer oder ein Programm). Bitte schließen und noch einmal auswerfen.",
        3 or 4 => $"Ein Programm benutzt das Laufwerk noch{(vetoName.Length > 0 ? $" ({vetoName})" : "")}. Bitte schließen und noch einmal auswerfen.",
        2 => "Windows schließt gerade noch Dateien auf dem Laufwerk. Kurz warten und noch einmal versuchen.",
        6 or 7 or 1 or 11 => "Ein Treiber oder ein anderes Gerät verhindert das Auswerfen. Programme schließen, die das Gerät benutzen, und noch einmal versuchen.",
        12 => "Für dieses Gerät fehlen die Rechte zum Auswerfen. Bitte über „Hardware sicher entfernen“ in der Taskleiste auswerfen.",
        13 => "Das Gerät ist schon entfernt.",
        _ => $"Windows hat das Auswerfen abgelehnt (Grund {vetoType}{(vetoName.Length > 0 ? ": " + vetoName : "")}). Bitte Programme schließen und noch einmal versuchen.",
    };
}

/// <summary>T-0764: Echter Auswurf unter Windows über cfgmgr32 – ohne Administratorrechte. Weg: Laufwerk → Gerätenummer (IOCTL_STORAGE_GET_DEVICE_NUMBER)
/// → passender Datenträger (SetupAPI, GUID_DEVINTERFACE_DISK) → übergeordnetes USB-Gerät → CM_Request_Device_EjectW.
/// Danach wird geprüft, ob das Laufwerk wirklich weg ist.</summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsDriveEjector : IDriveEjector
{
    public EjectResult Eject(string driveRoot)
    {
        var root = DeviceEject.NormalizeRoot(driveRoot) ?? throw new ArgumentException("Kein Laufwerk.");
        if (!TryDeviceNumber(@"\\.\" + root[..2], out var vol)) return new(false, $"Laufwerk {root} wurde nicht gefunden – ist der Stick schon abgezogen?");
        var disk = FindDiskInstance(vol);
        if (disk == 0) return new(false, $"Zu Laufwerk {root} hat Windows kein auswerfbares Gerät gefunden.");
        // erst das übergeordnete Gerät (USB-Massenspeicher), sonst den Datenträger selbst; Windows lehnt beim ersten Versuch manchmal kurz ab
        var targets = CM_Get_Parent(out var parent, disk, 0) == 0 ? new[] { parent, disk } : new[] { disk };
        var lastVeto = (Type: 0, Name: "");
        for (var attempt = 0; attempt < 3; attempt++)
        {
            foreach (var t in targets)
            {
                var name = new StringBuilder(260);
                var cr = CM_Request_Device_EjectW(t, out var veto, name, name.Capacity, 0);
                if (cr == 0 && veto == 0)
                {
                    for (var i = 0; i < 20 && Exists(root); i++) Thread.Sleep(100);
                    return Exists(root) ? new(false, $"Windows hat zugestimmt, aber Laufwerk {root} ist noch da. Bitte noch einmal versuchen.") : new(true, $"Laufwerk {root} kann jetzt abgezogen werden.");
                }
                if (veto != 0) lastVeto = (veto, name.ToString());
            }
            Thread.Sleep(300);
        }
        return new(false, lastVeto.Type != 0 ? DeviceEject.VetoText(lastVeto.Type, lastVeto.Name) : $"Windows konnte Laufwerk {root} nicht auswerfen.");
    }

    /// <summary>Nur lesen: Geräteknoten (DevInst) des Datenträgers zu einem Laufwerk, 0 = nicht gefunden. Wirft nichts aus –
    /// dient dem Nachweis, dass die Zuordnung Laufwerk → Gerät auf diesem PC funktioniert.</summary>
    public uint ResolveDeviceInstance(string driveRoot)
    {
        var root = DeviceEject.NormalizeRoot(driveRoot) ?? throw new ArgumentException("Kein Laufwerk.");
        return TryDeviceNumber(@"\\.\" + root[..2], out var vol) ? FindDiskInstance(vol) : 0;
    }

    static bool Exists(string root) { try { return new DriveInfo(root).IsReady; } catch (Exception e) when (e is IOException or ArgumentException) { return false; } }

    [StructLayout(LayoutKind.Sequential)] struct STORAGE_DEVICE_NUMBER { public int DeviceType; public int DeviceNumber; public int PartitionNumber; }
    [StructLayout(LayoutKind.Sequential)] struct SP_DEVICE_INTERFACE_DATA { public int cbSize; public Guid InterfaceClassGuid; public int Flags; public IntPtr Reserved; }
    [StructLayout(LayoutKind.Sequential)] struct SP_DEVINFO_DATA { public int cbSize; public Guid ClassGuid; public uint DevInst; public IntPtr Reserved; }

    static readonly Guid DiskGuid = new("53f56307-b6bf-11d0-94f2-00a0c91efb8b");
    const uint IOCTL_STORAGE_GET_DEVICE_NUMBER = 0x2D1080;
    const int DIGCF_PRESENT = 0x2, DIGCF_DEVICEINTERFACE = 0x10;

    static bool TryDeviceNumber(string path, out STORAGE_DEVICE_NUMBER n)
    {
        n = default;
        using var h = CreateFileW(path, 0, 3 /* lesen|schreiben teilen */, IntPtr.Zero, 3 /* OPEN_EXISTING */, 0, IntPtr.Zero);
        if (h.IsInvalid) return false;
        return DeviceIoControl(h, IOCTL_STORAGE_GET_DEVICE_NUMBER, IntPtr.Zero, 0, out n, Marshal.SizeOf<STORAGE_DEVICE_NUMBER>(), out _, IntPtr.Zero);
    }

    static uint FindDiskInstance(STORAGE_DEVICE_NUMBER vol)
    {
        var guid = DiskGuid;
        var set = SetupDiGetClassDevsW(ref guid, IntPtr.Zero, IntPtr.Zero, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
        if (set == IntPtr.Zero || set == new IntPtr(-1)) return 0;
        try
        {
            for (var i = 0; ; i++)
            {
                var ifd = new SP_DEVICE_INTERFACE_DATA { cbSize = Marshal.SizeOf<SP_DEVICE_INTERFACE_DATA>() };
                if (!SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref guid, i, ref ifd)) return 0;
                var info = new SP_DEVINFO_DATA { cbSize = Marshal.SizeOf<SP_DEVINFO_DATA>() };
                SetupDiGetDeviceInterfaceDetailW(set, ref ifd, IntPtr.Zero, 0, out var need, ref info);
                if (need <= 0) continue;
                var buf = Marshal.AllocHGlobal(need);
                try
                {
                    Marshal.WriteInt32(buf, IntPtr.Size == 8 ? 8 : 6); // cbSize von SP_DEVICE_INTERFACE_DETAIL_DATA_W
                    if (!SetupDiGetDeviceInterfaceDetailW(set, ref ifd, buf, need, out _, ref info)) continue;
                    var path = Marshal.PtrToStringUni(buf + 4);
                    if (path is null || !TryDeviceNumber(path, out var dn)) continue;
                    if (dn.DeviceType == vol.DeviceType && dn.DeviceNumber == vol.DeviceNumber) return info.DevInst;
                }
                finally { Marshal.FreeHGlobal(buf); }
            }
        }
        finally { SetupDiDestroyDeviceInfoList(set); }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern Microsoft.Win32.SafeHandles.SafeFileHandle CreateFileW(string name, uint access, uint share, IntPtr sec, uint disposition, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool DeviceIoControl(Microsoft.Win32.SafeHandles.SafeFileHandle h, uint code, IntPtr inBuf, int inSize, out STORAGE_DEVICE_NUMBER outBuf, int outSize, out int returned, IntPtr overlapped);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr SetupDiGetClassDevsW(ref Guid classGuid, IntPtr enumerator, IntPtr parent, int flags);
    [DllImport("setupapi.dll", SetLastError = true)]
    static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr devInfo, ref Guid guid, int index, ref SP_DEVICE_INTERFACE_DATA data);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool SetupDiGetDeviceInterfaceDetailW(IntPtr set, ref SP_DEVICE_INTERFACE_DATA data, IntPtr detail, int size, out int required, ref SP_DEVINFO_DATA info);
    [DllImport("setupapi.dll", SetLastError = true)]
    static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
    [DllImport("cfgmgr32.dll")]
    static extern int CM_Get_Parent(out uint parent, uint devInst, int flags);
    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    static extern int CM_Request_Device_EjectW(uint devInst, out int vetoType, StringBuilder vetoName, int nameLength, int flags);
}

/// <summary>T-0764 (Gegenprüfung M4): echte Auflösung unter Windows – <c>GetFinalPathNameByHandle</c> (Junction, Verknüpfung, subst) und
/// <c>IOCTL_STORAGE_GET_DEVICE_NUMBER</c> (physischer Datenträger). Nur lesen.</summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsDiskResolver : IDiskResolver
{
    public string FinalPath(string path)
    {
        var full = Path.GetFullPath(path);
        // existiert der Pfad nicht, den nächsten vorhandenen Oberordner auflösen (der Rest hängt dran)
        var probe = full; var rest = "";
        while (!Directory.Exists(probe) && !File.Exists(probe) && Path.GetDirectoryName(probe) is { } up) { rest = rest.Length == 0 ? Path.GetFileName(probe) : Path.Combine(Path.GetFileName(probe), rest); probe = up; }
        using var h = CreateFileW(probe, 0, 7, IntPtr.Zero, 3, 0x02000000 /* FILE_FLAG_BACKUP_SEMANTICS (Ordner) */, IntPtr.Zero);
        if (h.IsInvalid) return full;
        var sb = new StringBuilder(1024);
        var n = GetFinalPathNameByHandleW(h, sb, sb.Capacity, 0);
        if (n == 0 || n >= sb.Capacity) return full;
        var fin = sb.ToString();
        if (fin.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase)) fin = @"\\" + fin[8..];
        else if (fin.StartsWith(@"\\?\", StringComparison.Ordinal)) fin = fin[4..];
        return rest.Length == 0 ? fin : Path.Combine(fin, rest);
    }

    public (int Type, int Number)? DiskOf(string driveRoot)
    {
        var root = DeviceEject.NormalizeRoot(driveRoot);
        if (root is null) return null;
        using var h = CreateFileW(@"\\.\" + root[..2], 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
        if (h.IsInvalid) return null;
        return DeviceIoControl(h, 0x2D1080, IntPtr.Zero, 0, out var n, Marshal.SizeOf<StorageNumber>(), out _, IntPtr.Zero) ? (n.DeviceType, n.DeviceNumber) : null;
    }

    [StructLayout(LayoutKind.Sequential)] struct StorageNumber { public int DeviceType; public int DeviceNumber; public int PartitionNumber; }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern Microsoft.Win32.SafeHandles.SafeFileHandle CreateFileW(string name, uint access, uint share, IntPtr sec, uint disposition, uint flags, IntPtr template);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern uint GetFinalPathNameByHandleW(Microsoft.Win32.SafeHandles.SafeFileHandle h, StringBuilder path, int len, uint flags);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool DeviceIoControl(Microsoft.Win32.SafeHandles.SafeFileHandle h, uint code, IntPtr inBuf, int inSize, out StorageNumber outBuf, int outSize, out int returned, IntPtr overlapped);
}
