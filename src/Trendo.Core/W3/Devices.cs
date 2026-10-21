using System.Text.Json;

namespace Trendo.Core;

public sealed record DeviceInfo(string Category, string Name, string Status, string Detail = "")
{
    public bool Problem => Status is not ("OK" or "");
}

/// <summary>SYS-07 Hardware & Device Recognition: erkennt Laufwerke, Geräte (Windows: PnP, Linux: /sys), ordnet sie verständlichen Gruppen zu,
/// meldet Geräte mit Problemen und was seit dem letzten Mal neu dazugekommen oder verschwunden ist.</summary>
public sealed class DeviceScanner(IProcessRunner run, string dataRoot, string sysRoot = "/sys")
{
    public static string Group(string cls) => cls.ToLowerInvariant() switch
    {
        "display" or "monitor" or "drm" => "Anzeige", "media" or "audioendpoint" or "sound" => "Ton", "usb" or "usbdevice" => "USB",
        "net" => "Netzwerk", "camera" or "image" or "video4linux" => "Kamera", "printer" or "printqueue" => "Drucker",
        "keyboard" or "mouse" or "hidclass" or "input" => "Eingabe", "diskdrive" or "volume" or "cdrom" or "scsiadapter" or "hdc" or "block" or "drive" => "Speicher",
        "bluetooth" => "Bluetooth", "processor" or "cpu" => "Prozessor", "battery" or "power_supply" => "Akku", _ => "Sonstiges",
    };

    public async Task<IReadOnlyList<DeviceInfo>> ScanAsync(CancellationToken ct = default)
    {
        var l = new List<DeviceInfo>();
        foreach (var d in DriveInfo.GetDrives())
        {
            try { if (!d.IsReady || d.TotalSize == 0 || d.DriveType == DriveType.Ram) continue; if (!OperatingSystem.IsWindows() && d.DriveType != DriveType.Fixed && d.DriveType != DriveType.Removable) continue;
                l.Add(new("Speicher", $"Laufwerk {d.Name}{(d.VolumeLabel is { Length: > 0 } v && v != d.Name ? $" ({v})" : "")}", "OK", $"{d.DriveType switch { DriveType.Removable => "Wechseldatenträger", DriveType.Network => "Netzlaufwerk", _ => "fest" }} · {d.TotalFreeSpace / 1_000_000_000} von {d.TotalSize / 1_000_000_000} GB frei · {d.DriveFormat}")); }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        if (OperatingSystem.IsWindows() && sysRoot == "/sys") // eigener sysRoot = bewusst sysfs lesen (Tests, Linux-Abbild)
        {
            var (e, o) = await run.RunAsync("powershell", "-NoProfile -Command \"Get-PnpDevice -PresentOnly | Select-Object Class,FriendlyName,Status | ConvertTo-Csv -NoTypeInformation\"", null, ct);
            if (e == 0) l.AddRange(ParsePnpCsv(o));
        }
        else l.AddRange(ScanSys());
        return l.DistinctBy(d => (d.Category, d.Name)).OrderBy(d => d.Category).ThenBy(d => d.Name).ToList();
    }

    public static IReadOnlyList<DeviceInfo> ParsePnpCsv(string csv)
    {
        var res = new List<DeviceInfo>();
        foreach (var line in csv.Replace("\r", "").Split('\n').Skip(1).Where(x => x.Length > 2))
        {
            var c = SplitCsv(line); if (c.Count < 3 || c[1] == "") continue;
            if (c[0] is "System" or "SoftwareDevice" or "SoftwareComponent" or "Computer" or "Volume" or "VolumeSnapshot" or "LegacyDriver" or "Firmware" or "SecurityDevices") continue;
            res.Add(new(Group(c[0]), c[1], c[2] == "OK" ? "OK" : c[2] switch { "Error" => "Fehler", "Degraded" => "eingeschränkt", "Unknown" => "unbekannt", _ => c[2] }));
        }
        return res;
    }

    private static List<string> SplitCsv(string l)
    {
        var r = new List<string>(); var sb = new System.Text.StringBuilder(); var q = false;
        for (var i = 0; i < l.Length; i++)
        {
            var ch = l[i];
            if (ch == '"') { if (q && i + 1 < l.Length && l[i + 1] == '"') { sb.Append('"'); i++; } else q = !q; }
            else if (ch == ',' && !q) { r.Add(sb.ToString()); sb.Clear(); }
            else sb.Append(ch);
        }
        r.Add(sb.ToString()); return r;
    }

    /// <summary>Linux: liest /sys (USB-Geräte mit Namen, Netzwerkkarten, Grafik, Ton, Akku, Blockgeräte).</summary>
    public IReadOnlyList<DeviceInfo> ScanSys()
    {
        var res = new List<DeviceInfo>();
        string? R(string p) { try { return File.Exists(p) ? File.ReadAllText(p).Trim() : null; } catch { return null; } }
        var usb = Path.Combine(sysRoot, "bus", "usb", "devices");
        if (Directory.Exists(usb))
            foreach (var d in Directory.GetDirectories(usb))
            {
                var prod = R(Path.Combine(d, "product")); if (prod is null || Path.GetFileName(d).StartsWith("usb")) continue;
                var man = R(Path.Combine(d, "manufacturer"));
                var cls = R(Path.Combine(d, "bDeviceClass"));
                res.Add(new(cls switch { "0e" => "Kamera", "07" => "Drucker", "e0" => "Bluetooth", "01" => "Ton", "03" => "Eingabe", "08" => "Speicher", _ => "USB" }, man is null ? prod : $"{prod} ({man})", "OK", "USB"));
            }
        var net = Path.Combine(sysRoot, "class", "net");
        if (Directory.Exists(net))
            foreach (var d in Directory.GetDirectories(net).Concat(Directory.GetFiles(net)))
            {
                var n = Path.GetFileName(d); if (n == "lo") continue;
                var st = R(Path.Combine(d, "operstate"));
                res.Add(new("Netzwerk", n.StartsWith("wl") ? $"WLAN {n}" : $"Netzwerk {n}", "OK", st == "up" ? "verbunden" : "nicht verbunden"));
            }
        foreach (var (dir, cat) in new[] { ("drm", "Anzeige"), ("sound", "Ton"), ("power_supply", "Akku") })
        {
            var p = Path.Combine(sysRoot, "class", dir);
            if (!Directory.Exists(p)) continue;
            foreach (var d in Directory.GetDirectories(p).Concat(Directory.GetFiles(p)))
            {
                var n = Path.GetFileName(d);
                if (dir == "drm" && !System.Text.RegularExpressions.Regex.IsMatch(n, @"^card\d+$")) continue;
                if (dir == "sound" && !n.StartsWith("card")) continue;
                var label = dir == "sound" ? R(Path.Combine(d, "id")) ?? n : dir == "power_supply" ? $"{n} {R(Path.Combine(d, "capacity")) ?? "?"} %" : $"Grafik {n}";
                res.Add(new(cat, label, "OK"));
            }
        }
        return res;
    }

    /// <summary>Vergleicht mit dem letzten Stand und speichert den neuen.</summary>
    public (IReadOnlyList<DeviceInfo> Added, IReadOnlyList<DeviceInfo> Removed) Diff(IReadOnlyList<DeviceInfo> now)
    {
        var f = Path.Combine(dataRoot, "geraete.json");
        List<DeviceInfo>? before = null;
        try { if (File.Exists(f)) before = JsonSerializer.Deserialize<List<DeviceInfo>>(File.ReadAllText(f)); } catch (JsonException) { }
        File.WriteAllText(f, JsonSerializer.Serialize(now));
        if (before is null) return ([], []);
        static string K(DeviceInfo d) => d.Category + "|" + d.Name;
        var bk = before.Select(K).ToHashSet(); var nk = now.Select(K).ToHashSet();
        return (now.Where(d => !bk.Contains(K(d))).ToList(), before.Where(d => !nk.Contains(K(d))).ToList());
    }
}
