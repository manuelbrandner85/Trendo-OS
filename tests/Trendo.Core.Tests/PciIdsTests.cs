using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Alle Studios, Runde 9 – SYS-07: Gerätenamen aus pci.ids.</summary>
public class PciIdsTests
{
    const string Sample = "#\tList of PCI IDs\n\n10de  NVIDIA Corporation\n\t2204  GA102 [GeForce RTX 3090]\n\t\t10de 147d  GeForce RTX 3090 Founders Edition\n\t2484  GA104 [GeForce RTX 3070]\n8086  Intel Corporation\n\t1533  I210 Gigabit Network Connection\n\n# List of known device classes\nC 00  Unclassified device\n\t00  Non-VGA unclassified device\n";

    [Fact(DisplayName = "PCI +1: Datei lesen – Hersteller und Geräte, Untergeräte und Klassenliste entfallen; Name als „Modell (Hersteller)“")]
    public void Parse()
    {
        var p = PciIds.Parse(Sample);
        Assert.Equal((2, 3), (p.VendorCount, p.DeviceCount));
        Assert.Equal("GA102 [GeForce RTX 3090] (NVIDIA Corporation)", p.Name("10DE", "2204"));
        Assert.Equal("Unbekanntes Modell (Intel Corporation)", p.Name("8086", "ffff")); Assert.Null(p.Name("dead", "beef"));
    }

    [Fact(DisplayName = "PCI +2: Windows-Kennung zerlegen – nur PCI, USB und Sonstiges ergeben nichts")]
    public void Instance()
    {
        Assert.Equal(("10de", "2204"), PciIds.FromInstanceId(@"PCI\VEN_10DE&DEV_2204&SUBSYS_147D10DE&REV_A1\4&1F2E"));
        Assert.Null(PciIds.FromInstanceId(@"USB\VID_046D&PID_C52B\5")); Assert.Null(PciIds.FromInstanceId("")); Assert.Null(PciIds.FromInstanceId(null!));
    }

    const string Header = "\"Class\",\"FriendlyName\",\"Status\",\"InstanceId\"\n";

    [Fact(DisplayName = "PCI +3: Gerät ohne Namen – bisher verschwunden, jetzt sichtbar: ohne Liste mit Kennung, mit Liste mit Hersteller und Modell, Status bleibt")]
    public void Unnamed_devices()
    {
        var csv = Header + "\"\",\"\",\"Error\",\"PCI\\VEN_10DE&DEV_2204&SUBSYS_1\\4\"\n\"Display\",\"Unbekanntes Gerät\",\"Unknown\",\"PCI\\VEN_8086&DEV_1533\\3\"\n\"Net\",\"Intel Ethernet\",\"OK\",\"PCI\\VEN_8086&DEV_1533\\9\"\n\"\",\"\",\"OK\",\"USB\\VID_1\\2\"\n";
        var without = DeviceScanner.ParsePnpCsv(csv);
        Assert.Equal(3, without.Count);
        Assert.Equal(("Sonstiges", "Unbekanntes PCI-Gerät (10DE:2204)", "Fehler"), (without[0].Category, without[0].Name, without[0].Status));
        var with = DeviceScanner.ParsePnpCsv(csv, PciIds.Parse(Sample));
        Assert.Equal("GA102 [GeForce RTX 3090] (NVIDIA Corporation)", with[0].Name); Assert.Contains("Treiber fehlt", with[0].Detail); Assert.True(with[0].Problem);
        Assert.Equal(("Anzeige", "I210 Gigabit Network Connection (Intel Corporation)"), (with[1].Category, with[1].Name));
        Assert.Equal("Intel Ethernet", with[2].Name); // Namen von Windows bleiben
    }

    [Fact(DisplayName = "PCI +4: alte Ausgabe mit drei Spalten funktioniert weiter; Systemgeräte bleiben ausgeblendet")]
    public void Old_format()
    {
        var d = DeviceScanner.ParsePnpCsv("\"Class\",\"FriendlyName\",\"Status\"\n\"Net\",\"WLAN\",\"OK\"\n\"System\",\"Zeitgeber\",\"OK\"\n\"Net\",\"\",\"OK\"\n");
        Assert.Equal("WLAN", Assert.Single(d).Name);
    }

    [Fact(DisplayName = "PCI −1: Download nur mit passender Prüfsumme; fester Stand gepinnt")]
    public async Task Download()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-pci-" + Guid.NewGuid().ToString("N"));
        try
        {
            var (bad, m) = await PciIds.DownloadAsync(_ => Task.FromResult<Stream>(new MemoryStream([1, 2])), root);
            Assert.False(bad); Assert.Contains("Prüfsumme", m); Assert.False(File.Exists(PciIds.FileIn(root)));
            var data = "x"u8.ToArray(); var sha = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(data));
            Assert.True((await PciIds.DownloadAsync(_ => Task.FromResult<Stream>(new MemoryStream(data)), root, "u", sha)).Ok); Assert.True(File.Exists(PciIds.FileIn(root)));
            Assert.Contains(PciIds.Commit, PciIds.Url); Assert.Equal(64, PciIds.Sha256.Length);
        }
        finally { try { Directory.Delete(root, true); } catch (IOException) { } }
    }
}

/// <summary>Echte Daten: pci.ids vom gepinnten Stand aus dem Prüfordner – nur gelesen.</summary>
public class PciIdsRealTests
{
    static string F => Path.Combine(Path.GetTempPath(), "trendo-tools-b", "pciids", "pci.ids");
    public sealed class PciFactAttribute : FactAttribute { public PciFactAttribute() { if (!File.Exists(F)) Skip = "Übersprungen – pci.ids nicht im Prüfordner."; } }

    [PciFact(DisplayName = "PCI echt: gepinnte Prüfsumme; 2524 Hersteller, 21 739 Geräte; bekannte Grafikkarte und Netzwerkkarte gefunden; Lesen unter 100 ms")]
    public void Real_file()
    {
        var b = File.ReadAllBytes(F); Assert.Equal(PciIds.Sha256, Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(b)));
        var sw = System.Diagnostics.Stopwatch.StartNew(); var p = PciIds.Parse(System.Text.Encoding.UTF8.GetString(b)); sw.Stop();
        Assert.Equal(2524, p.VendorCount); Assert.Equal(21739, p.DeviceCount);
        Assert.Equal("GA102 [GeForce RTX 3090] (NVIDIA Corporation)", p.Name("10de", "2204"));
        Assert.Contains("I210", p.Name("8086", "1533")); Assert.InRange(sw.ElapsedMilliseconds, 0, 100);
    }
}
