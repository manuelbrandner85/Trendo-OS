using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>Alle Studios, Runde 9 – SYS-07 „Geräte erkennen“: Hersteller und Modell zu PCI-Kennungen aus pci.ids (PCI ID Project, BSD-3-Clause gewählt, Doppellizenz GPL-2.0+/BSD-3).
/// Reine Datendatei, fester Stand mit SHA-256, wird erst auf Klick geladen. Hilft bei Geräten ohne Treiber: Windows nennt sie gar nicht oder nur „Unbekanntes Gerät“.</summary>
public sealed partial class PciIds
{
    public const string Commit = "32bce8c6bc068e0349d8762ff7a4a5cd92114f2c";
    public const string Url = "https://raw.githubusercontent.com/pciutils/pciids/" + Commit + "/pci.ids";
    public const string Sha256 = "dcc29adc900c441e7d26d83ce15dc212ff3294b94e4bb1d46a71d7337287580a";
    public static string FileIn(string dataRoot) => Path.Combine(dataRoot, "geraete", "pci.ids");

    readonly Dictionary<string, (string Name, Dictionary<string, string> Devices)> _vendors = new(StringComparer.OrdinalIgnoreCase);
    public int VendorCount => _vendors.Count;
    public int DeviceCount => _vendors.Values.Sum(v => v.Devices.Count);

    /// <summary>pci.ids lesen: „vvvv  Hersteller“, eingerückt „\tdddd  Gerät“; Untergeräte (zwei Tabs) entfallen, die Klassenliste am Ende („C xx“) ebenso.</summary>
    public static PciIds Parse(string text)
    {
        var p = new PciIds(); Dictionary<string, string>? cur = null;
        foreach (var line in text.Replace("\r", "").Split('\n'))
        {
            if (line.Length < 7 || line[0] == '#') continue;
            if (line.StartsWith("C ", StringComparison.Ordinal)) break; // ab hier nur noch Geräteklassen
            if (line.StartsWith("\t\t", StringComparison.Ordinal)) continue;
            if (line[0] == '\t') { if (cur is not null && IsHex(line.AsSpan(1, 4)) && line[5] == ' ') cur[line.Substring(1, 4)] = line[7..].Trim(); }
            else if (IsHex(line.AsSpan(0, 4)) && line[4] == ' ') { cur = new(StringComparer.OrdinalIgnoreCase); p._vendors[line[..4]] = (line[6..].Trim(), cur); }
        }
        return p;
    }

    static bool IsHex(ReadOnlySpan<char> s) { foreach (var c in s) if (!Uri.IsHexDigit(c)) return false; return true; }

    /// <summary>Name zu Hersteller und Gerät: „Modell (Hersteller)“, nur Hersteller bekannt: „Unbekanntes Modell (Hersteller)“, sonst null.</summary>
    public string? Name(string vendor, string device)
    {
        if (!_vendors.TryGetValue(vendor, out var v)) return null;
        return v.Devices.TryGetValue(device, out var d) ? $"{d} ({v.Name})" : $"Unbekanntes Modell ({v.Name})";
    }

    [GeneratedRegex(@"^PCI\\VEN_([0-9A-F]{4})&DEV_([0-9A-F]{4})", RegexOptions.IgnoreCase)] private static partial Regex InstanceRx();
    /// <summary>Windows-Kennung „PCI\VEN_10DE&amp;DEV_2204&amp;…“ zerlegen; andere Busse ergeben null.</summary>
    public static (string Vendor, string Device)? FromInstanceId(string instanceId) =>
        InstanceRx().Match(instanceId ?? "") is { Success: true } m ? (m.Groups[1].Value.ToLowerInvariant(), m.Groups[2].Value.ToLowerInvariant()) : null;

    /// <summary>Datei nach Prüfung übernehmen; falsche Prüfsumme → verworfen.</summary>
    public static async Task<(bool Ok, string Message)> DownloadAsync(Func<string, Task<Stream>> fetch, string dataRoot, string url = Url, string sha = Sha256)
    {
        try
        {
            await using var s = await fetch(url); using var ms = new MemoryStream(); await s.CopyToAsync(ms); var data = ms.ToArray();
            if (Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(data)) != sha) return (false, "Download verworfen: Prüfsumme stimmt nicht.");
            var f = FileIn(dataRoot); Directory.CreateDirectory(Path.GetDirectoryName(f)!); await File.WriteAllBytesAsync(f, data);
            return (true, "Geräteliste geladen.");
        }
        catch (Exception e) when (e is not OutOfMemoryException) { return (false, "Download fehlgeschlagen: " + e.Message); }
    }
}
