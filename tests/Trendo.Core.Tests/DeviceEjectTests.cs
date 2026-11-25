using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0764 (SYS-07): Auswerfen nur für Wechseldatenträger – nie das Systemlaufwerk, nie Trendos eigenes Laufwerk; Problemcodes mit nächstem Schritt.</summary>
public class SYS07_Auswerfen
{
    sealed class FakeEjector(bool ok) : IDriveEjector
    {
        public List<string> Calls { get; } = [];
        public EjectResult Eject(string driveRoot) { Calls.Add(driveRoot); return new(ok, ok ? "weg" : "veto"); }
    }

    [Fact(DisplayName = "SYS-07 T-0764: Systemlaufwerk, Trendo-Laufwerk und feste Platten werden nie ausgeworfen – das Betriebssystem wird gar nicht gefragt")]
    public void Nie_System()
    {
        using var f = new Fixture();
        var sys = Path.GetPathRoot(Environment.SystemDirectory)!;
        var e = new FakeEjector(true);
        // selbst wenn ein Laufwerk fälschlich als „Wechseldatenträger“ gemeldet würde
        var r = DeviceEject.Eject(sys, DriveType.Removable, [Environment.SystemDirectory], e, f.Core.Audit);
        Assert.False(r.Ok); Assert.Contains("Windows oder Trendo", r.Message);
        r = DeviceEject.Eject("X:\\", DriveType.Removable, [Environment.SystemDirectory, @"X:\Trendo\daten"], e, f.Core.Audit);
        Assert.False(r.Ok);
        r = DeviceEject.Eject("Y:\\", DriveType.Fixed, [Environment.SystemDirectory], e, f.Core.Audit);
        Assert.False(r.Ok); Assert.Contains("kein Wechseldatenträger", r.Message);
        Assert.False(DeviceEject.Eject("nicht", DriveType.Removable, [], e, f.Core.Audit).Ok);
        Assert.Empty(e.Calls);
        Assert.Equal(4, f.Core.Audit.ReadAll().Count(a => a.Action == "device.eject.refused"));
        // ein echter Stick: genau ein Versuch, im Protokoll
        r = DeviceEject.Eject("e:", DriveType.Removable, [Environment.SystemDirectory], e, f.Core.Audit);
        Assert.True(r.Ok);
        Assert.Equal(["E:\\"], e.Calls);
        Assert.Contains(f.Core.Audit.ReadAll(), a => a.Action == "device.eject" && a.Detail.StartsWith("E:\\"));
    }

    [Fact(DisplayName = "SYS-07 T-0764: Windows-Problemcode → verständlicher nächster Schritt (Name oder Zahl); ohne Problem kein Hinweis")]
    public void Naechster_Schritt()
    {
        Assert.Equal("", DeviceEject.NextStep("CM_PROB_NONE", "OK"));
        Assert.Equal("", DeviceEject.NextStep(null, "OK"));
        Assert.Contains("Treiber fehlt", DeviceEject.NextStep("CM_PROB_FAILED_INSTALL", "Error"));
        Assert.Contains("Treiber fehlt", DeviceEject.NextStep("28", "Error"));
        Assert.Contains("ausgeschaltet", DeviceEject.NextStep("CM_PROB_DISABLED", "Error"));
        Assert.Contains("angehalten", DeviceEject.NextStep("CM_PROB_FAILED_POST_START", "Error"));
        Assert.Contains("neu gestartet", DeviceEject.NextStep("CM_PROB_NEED_RESTART", "Error"));
        Assert.Contains("CM_PROB_IRGENDWAS", DeviceEject.NextStep("CM_PROB_IRGENDWAS", "Error"));
        Assert.Contains("geöffnet", DeviceEject.VetoText(5, ""));
    }

    [Fact(DisplayName = "SYS-07 T-0764: Geräteliste liest den Problemcode (5. Spalte) und nennt den nächsten Schritt; ältere Ausgabe ohne Spalte geht weiter")]
    public void PnP_Problemspalte()
    {
        var csv = "\"Class\",\"FriendlyName\",\"Status\",\"InstanceId\",\"Problem\"\n"
            + "\"Camera\",\"USB-Kamera\",\"Error\",\"USB\\VID_1\\1\",\"CM_PROB_FAILED_INSTALL\"\n"
            + "\"Net\",\"Intel Ethernet\",\"OK\",\"PCI\\VEN_8086&DEV_1533\\9\",\"CM_PROB_NONE\"\n";
        var l = DeviceScanner.ParsePnpCsv(csv);
        var cam = l.Single(d => d.Name == "USB-Kamera");
        Assert.True(cam.Problem); Assert.Contains("Treiber fehlt", cam.Hint);
        var net = l.Single(d => d.Name == "Intel Ethernet");
        Assert.False(net.Problem); Assert.Equal("", net.Hint);
        var old = DeviceScanner.ParsePnpCsv("\"Class\",\"FriendlyName\",\"Status\",\"InstanceId\"\n\"Net\",\"Intel Ethernet\",\"OK\",\"PCI\\X\\9\"\n");
        Assert.False(Assert.Single(old).Problem);
    }

    [Fact(DisplayName = "SYS-07 T-0764: nur Wechseldatenträger bekommen „Auswerfen“; neu eingesteckte Sticks sind als Menge erkennbar")]
    public void Laufwerke()
    {
        DriveSnapshot[] drives = [new("C:\\", DriveType.Fixed, "System", 500_000_000_000, 100_000_000_000, "NTFS"), new("E:\\", DriveType.Removable, "STICK", 16_000_000_000, 8_000_000_000, "FAT32")];
        var l = DeviceScanner.DriveEntries(drives);
        Assert.Null(l.Single(d => d.Name.StartsWith("Laufwerk C:")).EjectRoot);
        Assert.Equal("E:\\", l.Single(d => d.Name.StartsWith("Laufwerk E:")).EjectRoot);
        Assert.Equal(["E:\\"], DeviceScanner.RemovableRoots(drives));
    }

    [Fact(DisplayName = "SYS-07 T-0764 echt: Windows-Auswurf (cfgmgr32) für ein nicht vorhandenes Laufwerk meldet „nicht gefunden“ und wirft nichts aus")]
    public void Echt_nicht_vorhanden()
    {
        if (!OperatingSystem.IsWindows()) return; // nicht anwendbar (kein Windows bzw. kein freier Buchstabe)
        var used = DriveInfo.GetDrives().Select(d => char.ToUpperInvariant(d.Name[0])).ToHashSet();
        var free = "QRSTUVWXYZ".FirstOrDefault(c => !used.Contains(c));
        if (free == default) return; // nicht anwendbar (kein Windows bzw. kein freier Buchstabe)
        var before = DriveInfo.GetDrives().Length;
        var r = new WindowsDriveEjector().Eject(free + ":\\");
        Assert.False(r.Ok);
        Assert.Contains("nicht gefunden", r.Message);
        Assert.Equal(before, DriveInfo.GetDrives().Length);
    }

    [Fact(DisplayName = "SYS-07 T-0764 echt (nur lesen): Zuordnung Laufwerk → Gerät über SetupAPI findet den Datenträger des Systemlaufwerks – ohne Auswurf")]
    public void Echt_Zuordnung_nur_lesen()
    {
        if (!OperatingSystem.IsWindows()) return; // nicht anwendbar
        var sys = Path.GetPathRoot(Environment.SystemDirectory)!;
        Assert.NotEqual(0u, new WindowsDriveEjector().ResolveDeviceInstance(sys));
        Assert.True(new DriveInfo(sys).IsReady);
    }
}
