using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Alle Studios, Runde 9 – SYS-07: Geräte ohne Namen sichtbar, Namen über die echte PCI-Liste nachschlagen.</summary>
public class DevicesPci_Ui
{
    sealed class Pnp : IProcessRunner
    {
        public Task<(int Exit, string Output)> RunAsync(string file, string args, Action<string>? line = null, CancellationToken ct = default) =>
            Task.FromResult((0, "\"Class\",\"FriendlyName\",\"Status\",\"InstanceId\"\n\"\",\"\",\"Error\",\"PCI\\VEN_10DE&DEV_2204&SUBSYS_1\\4\"\n\"Net\",\"Intel Ethernet\",\"OK\",\"PCI\\VEN_8086&DEV_1533\\9\"\n"));
    }
    static string Real => Path.Combine(Path.GetTempPath(), "trendo-tools-b", "pciids", "pci.ids");

    [AvaloniaFact(DisplayName = "Geräte: namenloses PCI-Gerät erscheint mit Kennung und Angebot; „Namen nachschlagen“ lädt die Liste (Prüfsumme) und zeigt Hersteller und Modell; danach kein Angebot mehr")]
    public async Task Names()
    {
        if (!OperatingSystem.IsWindows() || !File.Exists(Real)) return; // braucht Windows-Weg und pci.ids im Prüfordner
        var (w, _, _) = Ui.Open();
        try
        {
            w.DeviceRunnerOverride = new Pnp(); MainWindow.PciFetchOverride = _ => Task.FromResult<Stream>(File.OpenRead(Real));
            await w.ScanDevicesAsync(); w.Show("geraete");
            Assert.Contains(w.Devices, d => d.Name == "Unbekanntes PCI-Gerät (10DE:2204)" && d.Problem);
            Assert.NotNull(w.Find<Button>("dev-pci-load"));
            await w.DevicesLoadPciList();
            Assert.Contains(w.Devices, d => d.Name == "GA102 [GeForce RTX 3090] (NVIDIA Corporation)" && d.Detail.Contains("Treiber fehlt") && d.Problem);
            Assert.Null(w.TryFind<Button>("dev-pci-load"));
        }
        finally { MainWindow.PciFetchOverride = null; w.Close(); }
    }
}
