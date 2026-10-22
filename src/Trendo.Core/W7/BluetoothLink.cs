using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Trendo.Core;

public sealed record BtDevice(string Id, string Name, bool Paired, int? BatteryPercent);

public sealed record BtScan(IReadOnlyList<BtDevice> Devices, string Message);

public interface IBluetoothGateway
{
    string Backend { get; }
    Task<BtScan> SearchAsync(CancellationToken ct = default);
    Task<(bool Ok, string Message)> PairAsync(string id, bool consent, CancellationToken ct = default);
    Task<(bool Ok, string Message)> SendAsync(string id, string file, bool consent, CancellationToken ct = default);
    Task<(bool Ok, string Message)> ReceiveAsync(string folder, bool consent, CancellationToken ct = default);
    Task<(int? Percent, string Message)> BatteryAsync(string id, bool consent, CancellationToken ct = default);
}

/// <summary>Bluetooth: unter Windows die WinRT-Schnittstelle, unter Linux BlueZ. Dateien über OBEX. Kein Bildschirm.</summary>
public static class BluetoothLink
{
    public const string LimitHint = "Per Bluetooth kannst du Dateien schicken und den Akkustand sehen. Den Bildschirm zeigen geht so nicht. Dafür das Handy per Kabel oder WLAN verbinden.";

    public static bool IsMac(string id) => Regex.IsMatch(id, @"^(?:[0-9A-Fa-f]{2}:){5}[0-9A-Fa-f]{2}$");

    public static bool SafeId(string id) => IsMac(id) || Regex.IsMatch(id, @"^[A-Za-z0-9:_\\.#-]{8,220}$");

    public static string? FindTool(string name, Func<string, bool> exists, string? path)
    {
        var file = OperatingSystem.IsWindows() && !name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name + ".exe" : name;
        if (exists(file)) return file;
        foreach (var dir in new[] { "/usr/bin", "/usr/local/bin", "/bin" })
        {
            var p = Path.Combine(dir, file);
            if (exists(p)) return p;
        }
        foreach (var dir in (path ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var p = Path.Combine(dir, file);
            if (exists(p)) return p;
        }
        return null;
    }

    public static string? FindFsquirt(Func<string, bool> exists, string? systemDir)
    {
        if (string.IsNullOrEmpty(systemDir)) return null;
        var p = Path.Combine(systemDir, "fsquirt.exe");
        return exists(p) ? p : null;
    }

    public static (string File, string Args) PowerShell(string script) =>
        ("powershell.exe", "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + Convert.ToBase64String(Encoding.Unicode.GetBytes(script)));

    public static string DecodePowerShell(string args)
    {
        var mark = "-EncodedCommand ";
        var i = args.IndexOf(mark, StringComparison.Ordinal);
        if (i < 0) return "";
        return Encoding.Unicode.GetString(Convert.FromBase64String(args[(i + mark.Length)..].Trim()));
    }

    public const string DiscoveryScript =
        """
        $ErrorActionPreference = 'Stop'
        $null = [Windows.Devices.Enumeration.DeviceInformation, Windows.Devices.Enumeration, ContentType = WindowsRuntime]
        $null = [Windows.Devices.Enumeration.DeviceInformationKind, Windows.Devices.Enumeration, ContentType = WindowsRuntime]
        $null = [Windows.Devices.Bluetooth.BluetoothDevice, Windows.Devices.Bluetooth, ContentType = WindowsRuntime]
        $null = [Windows.Devices.Bluetooth.BluetoothLEDevice, Windows.Devices.Bluetooth, ContentType = WindowsRuntime]
        $seen = @{}
        function Esc([string]$s) { if ([string]::IsNullOrEmpty($s)) { return '' }; return (($s -replace '\\','\\') -replace '"','\"') }
        function AddDev($d) {
          if ($null -eq $d -or $seen.ContainsKey($d.Id)) { return }
          $seen[$d.Id] = $true
          $paired = $false
          try { $paired = [bool]$d.Pairing.IsPaired } catch { }
          $name = $d.Name
          Write-Output ('{"id":"' + (Esc $d.Id) + '","name":"' + (Esc $name) + '","paired":' + $(if ($paired) { 'true' } else { 'false' }) + '}')
        }
        try {
          $sels = @(
            'System.Devices.Aep.ProtocolId:="{e0cbf06c-cd8b-4647-bb8a-263b43f0f974}"',
            'System.Devices.Aep.ProtocolId:="{bb7bb05e-5972-42b5-94fc-76eaa7084d49}"'
          )
          foreach ($sel in $sels) {
            $watcher = [Windows.Devices.Enumeration.DeviceInformation]::CreateWatcher($sel, $null, [Windows.Devices.Enumeration.DeviceInformationKind]::AssociationEndpoint)
            $handler = { param($s, $dev) AddDev $dev }
            $watcher.add_Added($handler)
            $watcher.Start()
            Start-Sleep -Seconds 4
            $watcher.Stop()
          }
        } catch {
          Write-Output ('ERR ' + $_.Exception.Message)
        }
        function Await($op, $type) {
          $asTask = ([System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object { $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' })[0]
          $task = $asTask.MakeGenericMethod($type).Invoke($null, @($op))
          $task.Wait() | Out-Null
          $task.Result
        }
        foreach ($sel in @([Windows.Devices.Bluetooth.BluetoothDevice]::GetDeviceSelector(), [Windows.Devices.Bluetooth.BluetoothLEDevice]::GetDeviceSelector())) {
          $all = Await ([Windows.Devices.Enumeration.DeviceInformation]::FindAllAsync($sel)) ([Windows.Devices.Enumeration.DeviceInformationCollection])
          foreach ($d in $all) { AddDev $d }
        }
        """;

    public static string PairScript(string id)
    {
        if (!SafeId(id)) throw new ArgumentException("Gerät ungültig.");
        return PairTemplate.Replace("##ID##", id, StringComparison.Ordinal);
    }

    public static string BatteryScript(string id)
    {
        if (!SafeId(id)) throw new ArgumentException("Gerät ungültig.");
        return BatteryTemplate.Replace("##ID##", id, StringComparison.Ordinal);
    }

    const string PairTemplate =
        """
        $ErrorActionPreference = 'Stop'
        $null = [Windows.Devices.Enumeration.DeviceInformation, Windows.Devices.Enumeration, ContentType = WindowsRuntime]
        $null = [Windows.Devices.Enumeration.DevicePairingResult, Windows.Devices.Enumeration, ContentType = WindowsRuntime]
        $id = '##ID##'
        function Await($op, $type) {
          $asTask = ([System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object { $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' })[0]
          $task = $asTask.MakeGenericMethod($type).Invoke($null, @($op))
          $task.Wait() | Out-Null
          $task.Result
        }
        $info = Await ([Windows.Devices.Enumeration.DeviceInformation]::CreateFromIdAsync($id)) ([Windows.Devices.Enumeration.DeviceInformation])
        $result = Await ($info.Pairing.PairAsync()) ([Windows.Devices.Enumeration.DevicePairingResult])
        Write-Output ([string]$result.Status)
        """;

    const string BatteryTemplate =
        """
        $ErrorActionPreference = 'Stop'
        $null = [Windows.Devices.Enumeration.DeviceInformation, Windows.Devices.Enumeration, ContentType = WindowsRuntime]
        $null = [Windows.Devices.Bluetooth.BluetoothLEDevice, Windows.Devices.Bluetooth, ContentType = WindowsRuntime]
        $null = [Windows.Devices.Bluetooth.GenericAttributeProfile.GattDeviceService, Windows.Devices.Bluetooth.GenericAttributeProfile, ContentType = WindowsRuntime]
        $null = [Windows.Storage.Streams.DataReader, Windows.Storage.Streams, ContentType = WindowsRuntime]
        $id = '##ID##'
        function Await($op, $type) {
          $asTask = ([System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object { $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' })[0]
          $task = $asTask.MakeGenericMethod($type).Invoke($null, @($op))
          $task.Wait() | Out-Null
          $task.Result
        }
        try {
          $info = Await ([Windows.Devices.Enumeration.DeviceInformation]::CreateFromIdAsync($id)) ([Windows.Devices.Enumeration.DeviceInformation])
          $addr = [string]$info.Properties['System.Devices.Aep.DeviceAddress']
          if ([string]::IsNullOrWhiteSpace($addr)) { Write-Output 'NONE'; exit 0 }
          $n = [uint64]0
          foreach ($part in $addr.Split(':')) { $n = ($n -shl 8) -bor [convert]::ToByte($part, 16) }
          $ble = Await ([Windows.Devices.Bluetooth.BluetoothLEDevice]::FromBluetoothAddressAsync($n)) ([Windows.Devices.Bluetooth.BluetoothLEDevice])
          if ($null -eq $ble) { Write-Output 'NONE'; exit 0 }
          $svcId = [guid]'0000180f-0000-1000-8000-00805f9b34fb'
          $svcs = Await ($ble.GetGattServicesForUuidAsync($svcId)) ([Windows.Devices.Bluetooth.GenericAttributeProfile.GattDeviceServicesResult])
          if ($svcs.Services.Count -lt 1) { Write-Output 'NONE'; exit 0 }
          $chrId = [guid]'00002a19-0000-1000-8000-00805f9b34fb'
          $chars = Await ($svcs.Services[0].GetCharacteristicsForUuidAsync($chrId)) ([Windows.Devices.Bluetooth.GenericAttributeProfile.GattCharacteristicsResult])
          if ($chars.Characteristics.Count -lt 1) { Write-Output 'NONE'; exit 0 }
          $val = Await ($chars.Characteristics[0].ReadValueAsync()) ([Windows.Devices.Bluetooth.GenericAttributeProfile.GattReadResult])
          $reader = [Windows.Storage.Streams.DataReader]::FromBuffer($val.Value)
          Write-Output ('BATTERY ' + $reader.ReadByte())
        } catch {
          Write-Output 'NONE'
        }
        """;

    public static IReadOnlyList<BtDevice> ParseWinRtScan(string output)
    {
        var list = new List<BtDevice>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var raw in output.Split('\n'))
        {
            var t = raw.Trim();
            if (!t.StartsWith('{')) continue;
            try
            {
                using var doc = JsonDocument.Parse(t);
                var id = doc.RootElement.GetProperty("id").GetString() ?? "";
                if (!SafeId(id) || !seen.Add(id)) continue;
                var name = doc.RootElement.TryGetProperty("name", out var n) ? n.GetString() : null;
                if (string.IsNullOrWhiteSpace(name)) name = "Bluetooth-Gerät";
                var paired = doc.RootElement.TryGetProperty("paired", out var p) && p.ValueKind == JsonValueKind.True;
                list.Add(new BtDevice(id, name.Trim(), paired, null));
            }
            catch (JsonException) { }
        }
        return list;
    }

    public static bool WinRtPairOk(string output) =>
        output.Split('\n').Select(l => l.Trim()).Any(l => l is "Paired" or "AlreadyPaired");

    public static int? ParseBatteryLine(string output)
    {
        var m = Regex.Match(output, @"BATTERY\s+(\d{1,3})");
        if (!m.Success) return null;
        var n = int.Parse(m.Groups[1].Value);
        return n is >= 0 and <= 100 ? n : null;
    }

    public static IReadOnlyList<BtDevice> ParseBlueZScan(string output)
    {
        var map = new Dictionary<string, (string Name, bool Paired)>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in output.Split('\n'))
        {
            var m = Regex.Match(raw, @"Device\s+((?:[0-9A-Fa-f]{2}:){5}[0-9A-Fa-f]{2})\s*(.*)$");
            if (!m.Success) continue;
            var addr = m.Groups[1].Value.ToUpperInvariant();
            var rest = m.Groups[2].Value.Trim();
            if (rest.Length == 0 || Regex.IsMatch(rest, @"^[A-Za-z][A-Za-z0-9 ]*:\s")) continue;
            map[addr] = (rest, map.TryGetValue(addr, out var old) && old.Paired);
        }
        string? cur = null;
        foreach (var raw in output.Split('\n'))
        {
            var dm = Regex.Match(raw, @"Device\s+((?:[0-9A-Fa-f]{2}:){5}[0-9A-Fa-f]{2})");
            if (dm.Success) cur = dm.Groups[1].Value.ToUpperInvariant();
            if (cur is not null && map.ContainsKey(cur) && Regex.IsMatch(raw, @"Paired:\s*yes", RegexOptions.IgnoreCase))
                map[cur] = (map[cur].Name, true);
        }
        return map.Select(kv => new BtDevice(kv.Key, kv.Value.Name, kv.Value.Paired, null)).ToList();
    }

    public static int? ParseBlueZBattery(string info)
    {
        var m = Regex.Match(info, @"Battery Percentage:\s*(?:0x[0-9A-Fa-f]+\s*)?(?:\((\d{1,3})\)|(\d{1,3}))", RegexOptions.IgnoreCase);
        if (!m.Success) return null;
        var raw = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
        var n = int.Parse(raw);
        return n is >= 0 and <= 100 ? n : null;
    }

    public static bool BlueZPairOk(int exit, string output) =>
        exit == 0 && !output.Contains("Failed", StringComparison.OrdinalIgnoreCase)
        && (output.Contains("Pairing successful", StringComparison.OrdinalIgnoreCase) || output.Contains("AlreadyExists", StringComparison.OrdinalIgnoreCase));

    public static IBluetoothGateway Create(IProcessRunner run, Func<string, bool>? exists = null, string? systemDir = null, string? pathEnv = null)
    {
        exists ??= File.Exists;
        pathEnv ??= Environment.GetEnvironmentVariable("PATH");
        if (OperatingSystem.IsWindows())
            return new WindowsBtGateway(run, exists, systemDir ?? Environment.GetFolderPath(Environment.SpecialFolder.System), pathEnv);
        return new BlueZGateway(run, exists, pathEnv);
    }
}

public sealed class BlueZGateway(IProcessRunner run, Func<string, bool> exists, string? path) : IBluetoothGateway
{
    public string Backend => "Linux";

    public async Task<BtScan> SearchAsync(CancellationToken ct = default)
    {
        var tool = BluetoothLink.FindTool("bluetoothctl", exists, path);
        if (tool is null) return new([], "Bluetooth-Suche braucht BlueZ (bluetoothctl). Trendo installiert keine Treiber.");
        var (e, o) = await run.RunAsync(tool, "--timeout 8 scan on", null, ct);
        var devices = BluetoothLink.ParseBlueZScan(o);
        if (devices.Count == 0)
            return new([], e == 0 ? "Kein Bluetooth-Gerät gefunden. Mach das Handy sichtbar und tippe noch einmal auf „Geräte suchen“." : "Bluetooth-Suche fehlgeschlagen. Ist Bluetooth am PC eingeschaltet?");
        return new(devices, devices.Count == 1 ? "1 Gerät gefunden." : $"{devices.Count} Geräte gefunden.");
    }

    public async Task<(bool Ok, string Message)> PairAsync(string id, bool consent, CancellationToken ct = default)
    {
        if (!consent) return (false, "Erst zustimmen.");
        if (!BluetoothLink.IsMac(id)) return (false, "Gerät ungültig.");
        var tool = BluetoothLink.FindTool("bluetoothctl", exists, path);
        if (tool is null) return (false, "Koppeln braucht BlueZ (bluetoothctl). Trendo installiert keine Treiber.");
        var (e, o) = await run.RunAsync(tool, $"--timeout 30 pair {id.ToUpperInvariant()}", null, ct);
        return BluetoothLink.BlueZPairOk(e, o)
            ? (true, "Gekoppelt. Falls das Handy fragt, dort auch bestätigen.")
            : (false, "Koppeln fehlgeschlagen. Am Handy die Anfrage bestätigen und es noch einmal versuchen.");
    }

    public Task<(bool Ok, string Message)> SendAsync(string id, string file, bool consent, CancellationToken ct = default) =>
        SendFile(run, exists, path, windows: false, systemDir: null, id, file, consent, ct);

    public Task<(bool Ok, string Message)> ReceiveAsync(string folder, bool consent, CancellationToken ct = default) =>
        ReceiveFile(run, exists, path, windows: false, systemDir: null, folder, consent, ct);

    public async Task<(int? Percent, string Message)> BatteryAsync(string id, bool consent, CancellationToken ct = default)
    {
        if (!consent) return (null, "Erst zustimmen.");
        if (!BluetoothLink.IsMac(id)) return (null, "Gerät ungültig.");
        var tool = BluetoothLink.FindTool("bluetoothctl", exists, path);
        if (tool is null) return (null, "Akkustand braucht BlueZ (bluetoothctl).");
        var (_, o) = await run.RunAsync(tool, $"info {id.ToUpperInvariant()}", null, ct);
        var level = BluetoothLink.ParseBlueZBattery(o);
        return level is { } n ? (n, $"Akku {n} %") : (null, "Dieses Gerät meldet keinen Akkustand.");
    }

    internal static async Task<(bool Ok, string Message)> SendFile(IProcessRunner run, Func<string, bool> exists, string? path, bool windows, string? systemDir, string id, string file, bool consent, CancellationToken ct)
    {
        if (!consent) return (false, "Erst zustimmen.");
        if (!BluetoothLink.SafeId(id)) return (false, "Gerät ungültig.");
        if (file.Contains('"') || file.Contains('\n')) return (false, "Dateiname ungültig.");
        if (!File.Exists(file)) return (false, "Datei nicht gefunden.");
        var name = Path.GetFileName(file);
        if (BluetoothLink.IsMac(id))
        {
            var obex = BluetoothLink.FindTool("obexftp", exists, path);
            if (obex is not null)
            {
                var (e, o) = await run.RunAsync(obex, $"-b {id.ToUpperInvariant()} -p \"{file}\"", null, ct);
                return e == 0 ? (true, $"„{name}“ wurde gesendet.") : (false, "Senden fehlgeschlagen. " + PhoneActions.FirstLine(o));
            }
        }
        if (windows)
        {
            var fs = BluetoothLink.FindFsquirt(exists, systemDir);
            if (fs is null) return (false, "Der Windows-Dialog zum Senden (fsquirt) fehlt.");
            var (e, _) = await run.RunAsync(fs, "-send", null, ct);
            return e == 0
                ? (true, $"Das Windows-Fenster ist offen. Wähle dort dein Handy und die Datei „{name}“.")
                : (false, "Der Windows-Dialog konnte nicht geöffnet werden.");
        }
        return (false, "Zum Senden fehlt das Programm obexftp. Trendo installiert keine Bluetooth-Treiber.");
    }

    internal static async Task<(bool Ok, string Message)> ReceiveFile(IProcessRunner run, Func<string, bool> exists, string? path, bool windows, string? systemDir, string folder, bool consent, CancellationToken ct)
    {
        if (!consent) return (false, "Erst zustimmen.");
        if (folder.Contains('"') || folder.Contains('\n') || !Directory.Exists(folder)) return (false, "Ordner nicht gefunden.");
        if (windows)
        {
            var fs = BluetoothLink.FindFsquirt(exists, systemDir);
            if (fs is null) return (false, "Der Windows-Dialog zum Empfangen (fsquirt) fehlt.");
            var (e, _) = await run.RunAsync(fs, "-receive", null, ct);
            return e == 0 ? (true, "Das Windows-Fenster zum Empfangen ist offen. Nimm die Datei dort an.") : (false, "Empfangen fehlgeschlagen.");
        }
        var obexd = BluetoothLink.FindTool("obexd", exists, path);
        if (obexd is null) return (false, "Zum Empfangen fehlt obexd (Paket bluez-obexd). Trendo startet keinen Empfang im Hintergrund und ändert keine Treiber.");
        var before = Directory.GetFiles(folder).ToHashSet(StringComparer.Ordinal);
        var (exit, output) = await run.RunAsync(obexd, $"--nodaemon --root \"{folder}\"", null, ct);
        var neu = Directory.GetFiles(folder).FirstOrDefault(f => !before.Contains(f));
        if (neu is not null) return (true, $"Datei empfangen: {Path.GetFileName(neu)}");
        return (false, exit == 0 ? "Keine Datei angekommen." : "Empfang fehlgeschlagen. " + PhoneActions.FirstLine(output));
    }
}

public sealed class WindowsBtGateway(IProcessRunner run, Func<string, bool> exists, string? systemDir, string? path) : IBluetoothGateway
{
    public string Backend => "Windows";

    public async Task<BtScan> SearchAsync(CancellationToken ct = default)
    {
        var (file, args) = BluetoothLink.PowerShell(BluetoothLink.DiscoveryScript);
        var (e, o) = await run.RunAsync(file, args, null, ct);
        var devices = BluetoothLink.ParseWinRtScan(o);
        if (devices.Count == 0)
            return new([], e == 0 ? "Kein Bluetooth-Gerät gefunden. Mach das Handy sichtbar und tippe noch einmal auf „Geräte suchen“." : "Bluetooth-Suche fehlgeschlagen. Ist Bluetooth am PC eingeschaltet?");
        return new(devices, devices.Count == 1 ? "1 Gerät gefunden." : $"{devices.Count} Geräte gefunden.");
    }

    public async Task<(bool Ok, string Message)> PairAsync(string id, bool consent, CancellationToken ct = default)
    {
        if (!consent) return (false, "Erst zustimmen.");
        if (!BluetoothLink.SafeId(id)) return (false, "Gerät ungültig.");
        var (file, args) = BluetoothLink.PowerShell(BluetoothLink.PairScript(id));
        var (_, o) = await run.RunAsync(file, args, null, ct);
        return BluetoothLink.WinRtPairOk(o)
            ? (true, "Gekoppelt. Falls Windows oder das Handy fragt, dort auch bestätigen.")
            : (false, "Koppeln fehlgeschlagen. Am Handy und am PC die Anfrage bestätigen.");
    }

    public Task<(bool Ok, string Message)> SendAsync(string id, string file, bool consent, CancellationToken ct = default) =>
        BlueZGateway.SendFile(run, exists, path, windows: true, systemDir, id, file, consent, ct);

    public Task<(bool Ok, string Message)> ReceiveAsync(string folder, bool consent, CancellationToken ct = default) =>
        BlueZGateway.ReceiveFile(run, exists, path, windows: true, systemDir, folder, consent, ct);

    public async Task<(int? Percent, string Message)> BatteryAsync(string id, bool consent, CancellationToken ct = default)
    {
        if (!consent) return (null, "Erst zustimmen.");
        if (!BluetoothLink.SafeId(id)) return (null, "Gerät ungültig.");
        var (file, args) = BluetoothLink.PowerShell(BluetoothLink.BatteryScript(id));
        var (_, o) = await run.RunAsync(file, args, null, ct);
        var level = BluetoothLink.ParseBatteryLine(o);
        return level is { } n ? (n, $"Akku {n} %") : (null, "Dieses Gerät meldet keinen Akkustand.");
    }
}
