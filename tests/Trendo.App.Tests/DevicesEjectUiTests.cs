using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0764 (SYS-07): Stick einstecken → erscheint beim nächsten Nachsehen (alle 2 s) mit „Auswerfen“; Auswerfen meldet Erfolg und der Stick
/// verschwindet; ein Gerät mit Problemcode zeigt den nächsten Schritt. Laufwerke und Auswurf sind ersetzt (kein echter Stick an diesem PC).</summary>
public class SYS07_Auswerfen_Ui
{
    static void Settle() { for (var i = 0; i < 8; i++) Dispatcher.UIThread.RunJobs(); }

    sealed class Pnp : IProcessRunner
    {
        public Task<(int Exit, string Output)> RunAsync(string file, string args, Action<string>? line = null, CancellationToken ct = default) =>
            Task.FromResult((0, "\"Class\",\"FriendlyName\",\"Status\",\"InstanceId\",\"Problem\"\n\"Camera\",\"USB-Kamera\",\"Error\",\"USB\\VID_1\\1\",\"CM_PROB_FAILED_INSTALL\"\n\"Net\",\"Intel Ethernet\",\"OK\",\"PCI\\VEN_8086&DEV_1533\\9\",\"CM_PROB_NONE\"\n"));
    }

    sealed class Ejector(List<DriveSnapshot> drives) : IDriveEjector
    {
        public List<string> Calls { get; } = [];
        public EjectResult Eject(string root) { Calls.Add(root); drives.RemoveAll(d => d.Root == root); return new(true, $"Laufwerk {root} kann jetzt abgezogen werden."); }
    }

    [AvaloniaFact(DisplayName = "SYS-07 T-0764 UI: Stick einstecken → „Neu eingesteckt“ mit Auswerfen; Auswerfen → abziehbar, Eintrag weg; Systemlaufwerk ohne Knopf; Problemgerät mit nächstem Schritt")]
    public async Task Stick_einstecken_und_auswerfen()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            var drives = new List<DriveSnapshot> { new("C:\\", DriveType.Fixed, "System", 500_000_000_000, 100_000_000_000, "NTFS") };
            var ej = new Ejector(drives);
            w.DriveSourceOverride = () => drives.ToList();
            w.EjectorOverride = ej;
            if (OperatingSystem.IsWindows()) w.DeviceRunnerOverride = new Pnp(); else w.DeviceSysRootOverride = Path.Combine(Path.GetTempPath(), "leer-" + Guid.NewGuid().ToString("N"));
            w.Show("geraete"); Settle();
            await w.ScanDevicesAsync(); w.Show("geraete"); Settle();
            Assert.Null(w.TryFind<Button>("dev-eject-"));
            if (OperatingSystem.IsWindows())
                Assert.Contains("Treiber fehlt", w.Find<TextBlock>("dev-hint-0").Text);

            Assert.False(w.DevicesPollNow()); // nichts geändert
            drives.Add(new("E:\\", DriveType.Removable, "STICK", 16_000_000_000, 8_000_000_000, "FAT32"));
            Assert.True(w.DevicesPollNow()); Settle();
            Assert.Contains("Neu eingesteckt: Laufwerk E:\\ (STICK)", w.Find<TextBlock>("dev-msg").Text);
            w.Find<Button>("dev-eject-E").Press();
            for (var i = 0; i < 50 && ej.Calls.Count == 0; i++) { await Task.Delay(20); Settle(); }
            for (var i = 0; i < 50 && w.TryFind<Button>("dev-eject-E") is not null; i++) { await Task.Delay(20); Settle(); }
            Assert.Equal(["E:\\"], ej.Calls);
            Assert.Contains("kann jetzt abgezogen werden", w.Find<TextBlock>("dev-msg").Text);
            Assert.Null(w.TryFind<Button>("dev-eject-E"));
            Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "device.eject");
        }
        finally { w.Close(); }
    }
}
