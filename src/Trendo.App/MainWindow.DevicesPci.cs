using Avalonia.Controls;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Alle Studios, Runde 9 – SYS-07: Sind Geräte ohne Namen dabei, bietet Trendo an, die PCI-Liste zu laden (1,7 MB, BSD-3-Clause, feste Prüfsumme), und sucht neu.</summary>
public partial class MainWindow
{
    /// <summary>Für Tests: Abruf nachbilden.</summary>
    public static Func<string, Task<Stream>>? PciFetchOverride { get; set; }

    /// <summary>Liste laden und neu suchen (öffentlich für Tests).</summary>
    public async Task DevicesLoadPciList()
    {
        _devMsg = "Geräteliste wird geladen …"; Show("geraete");
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
        var (ok, msg) = await PciIds.DownloadAsync(PciFetchOverride ?? http.GetStreamAsync, _core.DataRoot);
        _core.Audit.Append(Principal.LocalOwner.Name, "deps.download", "pci.ids " + PciIds.Commit[..8] + (ok ? "" : " (fehlgeschlagen)"));
        if (ok) await ScanDevicesAsync(); else _devMsg = "" + msg;
        Show("geraete");
    }

    Control DevicesPciCard()
    {
        var unnamed = Devices.Count(d => d.Name.StartsWith(DeviceScanner.UnnamedPrefix, StringComparison.Ordinal));
        if (unnamed == 0) return new Border { Height = 0 };
        return WCard(new StackPanel { Name = "dev-pci", Spacing = 6, Children = {
            P($"{unnamed} Gerät(e) ohne Namen – meist fehlt der Treiber. Mit der freien PCI-Liste (1,7 MB, einmalig) zeigt Trendo Hersteller und Modell.", null, 12),
            Act("dev-pci-load", "Namen nachschlagen", () => _ = DevicesLoadPciList()) } });
    }
}
