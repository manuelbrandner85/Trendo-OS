using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

public class W7PhoneLinkTests
{
    const string Wifi = "WIFI:T:ADB;S:studio-AbCdEfGh12;P:1234567890;;";

    [Fact(DisplayName = "X-08: QR folgt dem Android-Text, Maske 0 hat die Format-Bits 0x5412, der Code ist fest und wurde gelesen")]
    public void Qr_payload_and_matrix()
    {
        Assert.Equal(0x5412, QrMatrix.FormatBits(0));
        Assert.Matches(@"^WIFI:T:ADB;S:studio-[A-Za-z0-9]{8,16};P:\d{10};;$", WirelessAdb.QrPayload("studio-AbCdEfGh12", "1234567890"));
        Assert.Throws<ArgumentException>(() => WirelessAdb.QrPayload("studio-kurz", "1234567890"));
        Assert.Throws<ArgumentException>(() => WirelessAdb.QrPayload("studio-AbCdEfGh12", "123456"));
        var a = WirelessAdb.CreateSession(new Random(1));
        var b = WirelessAdb.CreateSession(new Random(2));
        Assert.NotEqual(a.Password, b.Password);
        Assert.Matches(@"^studio-[A-Za-z0-9]{10}$", a.ServiceName);
        Assert.Equal(4, QrMatrix.VersionFor(Encoding.UTF8.GetByteCount(Wifi)));
        var m = QrMatrix.Encode(Wifi);
        Assert.Equal(33, m.GetLength(0));
        Assert.True(m[0, 0]); Assert.False(m[1, 1]); Assert.True(m[3, 3]);
        var bits = new StringBuilder();
        for (var y = 0; y < 33; y++)
            for (var x = 0; x < 33; x++) bits.Append(m[y, x] ? '1' : '0');
        Assert.Equal("3b5cac31b233fcb701fb12086285eaeb90ef40667dd7f055ce309b8f12531436", Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(bits.ToString()))).ToLowerInvariant());
        var src = File.ReadAllText(RepoFile("src/Trendo.Core/W7/WirelessAdb.cs"));
        Assert.DoesNotMatch(@"P:\d{10}", src);
    }

    [Fact(DisplayName = "X-08: Kabel, WLAN-Adresse und TLS-Name, mDNS, Koppeln nur mit Zustimmung und nur im Heimnetz")]
    public void Wlan_rules()
    {
        Assert.Equal(PhoneLink.Kabel, MobileBridge.LinkOf("R58", " usb:1-2 product:x"));
        Assert.Equal(PhoneLink.Wlan, MobileBridge.LinkOf("192.168.1.20:44000", " model:Pixel_8"));
        Assert.Equal(PhoneLink.Wlan, MobileBridge.LinkOf("adb-0123456789ABCDEF._adb-tls-connect._tcp", ""));
        Assert.Equal("WLAN", MobileBridge.LinkText(PhoneLink.Wlan));
        Assert.Equal("Kabel", MobileBridge.LinkText(PhoneLink.Kabel));
        Assert.Contains("WLAN-Freigabe", MobileBridge.StateText("unauthorized", PhoneLink.Wlan));
        var mdns = """
            List of discovered mdns services
            adb-0123456789ABCDEF _adb-tls-connect._tcp 192.168.1.20:44000
            studio-AbCdEfGh12 _adb-tls-pairing._tcp 192.168.1.20:37123
            adb-alt _adb._tcp 192.168.1.20:5555
            ignore-me _adb-tls-connect._tcp [fe80::1]:44000
            """;
        var services = WirelessAdb.ParseMdns(mdns);
        Assert.Equal(3, services.Count);
        Assert.Equal(WirelessAdb.ConnectType, services[0].Type);
        Assert.Equal(37123, services[1].Port);
        Assert.Equal("0123456789ABCDEF", WirelessAdb.WifiGuid(services[0].Name));

        Assert.False(WirelessAdb.PairArgs("192.168.1.20", 37123, "123456", false).Ok);
        Assert.False(WirelessAdb.PairArgs("8.8.8.8", 37123, "123456", true).Ok);
        Assert.False(WirelessAdb.PairArgs("192.168.1.20", 80, "123456", true).Ok);
        Assert.False(WirelessAdb.PairArgs("192.168.1.20", 37123, "12", true).Ok);
        var pair = WirelessAdb.PairArgs("192.168.1.20", 37123, "123456", true);
        Assert.Equal("pair 192.168.1.20:37123 123456", pair.Args);
        Assert.DoesNotContain("123456", pair.Message);
        Assert.True(WirelessAdb.PairOk(0, "Successfully paired to 192.168.1.20:37123"));
        Assert.False(WirelessAdb.PairOk(0, "Failed"));
        var connect = WirelessAdb.ConnectArgs("192.168.1.20", 44000, true);
        Assert.Equal("connect 192.168.1.20:44000", connect.Args);
        Assert.False(WirelessAdb.ConnectArgs("1.1.1.1", 44000, true).Ok);
        Assert.True(WirelessAdb.ConnectOk(0, "already connected to 192.168.1.20:44000"));
        Assert.False(WirelessAdb.ConnectOk(0, "failed to connect to 192.168.1.20:44000"));
        Assert.False(WirelessAdb.ConnectOk(1, "connected"));
    }

    [Fact(DisplayName = "X-08: bekanntes Handy nur nach Zustimmung, mDNS vor der alten Adresse, ohne Code in der Datei")]
    public void Known_and_reconnect()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-known-" + Guid.NewGuid().ToString("N"));
        var store = new KnownPhones(root);
        store.Remember("Pixel 8", "8.8.8.8", 44000, null, DateTime.Now);
        Assert.Empty(store.All());
        store.Remember("Pixel 8", "192.168.1.20", 44000, "0123456789ABCDEF", new DateTime(2026, 10, 22, 12, 0, 0));
        var json = File.ReadAllText(Path.Combine(root, "handy", "bekannte.json"));
        Assert.Contains("Pixel 8", json);
        Assert.DoesNotContain("WIFI:", json);
        Assert.DoesNotContain("Password", json);
        Assert.DoesNotContain("pair ", json);
        var phone = store.All().Single();
        Assert.False(WirelessAdb.PickReconnect(phone, [], false).Ok);
        var services = WirelessAdb.ParseMdns("adb-0123456789ABCDEF _adb-tls-connect._tcp 192.168.1.21:45555\nadb-0123456789ABCDEF _adb._tcp 192.168.1.21:5555\n");
        var hit = WirelessAdb.PickReconnect(phone, services, true);
        Assert.True(hit.Ok);
        Assert.Equal(("192.168.1.21", 45555), (hit.Host, hit.Port));
        var last = WirelessAdb.PickReconnect(phone, [], true);
        Assert.Equal(("192.168.1.20", 44000), (last.Host, last.Port));
        Assert.Contains("Port", last.Message);
        Assert.False(WirelessAdb.PickReconnect(phone, [new MdnsService("adb-0123456789ABCDEF", WirelessAdb.ConnectType, "8.8.8.8", 44000)], true).Ok);
    }

    [Fact(DisplayName = "X-08: Datei, Bildschirm, Apps und Überschriften nutzen dieselbe Seriennummer für Kabel und WLAN")]
    public void Same_commands()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-act-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var file = Path.Combine(root, "brief.pdf");
        File.WriteAllText(file, "x");
        foreach (var serial in new[] { "R58N123ABC", "192.168.1.20:44000", "adb-0123456789ABCDEF._adb-tls-connect._tcp" })
        {
            var dev = new PhoneDevice(serial, "device", "Pixel", serial.Contains(':') || serial.Contains("tls") ? PhoneLink.Wlan : PhoneLink.Kabel);
            Assert.Contains("-s " + serial + " ", MobileBridge.PushArgs(dev, file, true).Args);
            Assert.Equal($"-s {serial} --window-title Handy", PhoneActions.MirrorArgs(serial, true).Args);
            Assert.Contains($"-s {serial} shell pm list packages -3", PhoneActions.PackageArgs(serial, true).Args);
            Assert.Contains("dumpsys notification", PhoneActions.NoticeArgs(serial, true).Args);
            Assert.False(PhoneActions.MirrorArgs(serial, false).Ok);
        }
        Assert.False(PhoneActions.PackageArgs("a;rm", true).Ok);
        Assert.False(MobileBridge.SafeSerial("a;rm -rf"));
        var dumpsys = "NotificationRecord pkg=com.example id=1\n        android.title=String (Neue Nachricht)\n        android.text=String (Geheimtext)\n";
        var notes = PhoneActions.ParseNotifications(dumpsys);
        Assert.Equal(new PhoneNotice("com.example", "Neue Nachricht"), Assert.Single(notes));
        Assert.DoesNotContain("Geheimtext", notes[0].Title);
        Assert.Equal(["com.example.app"], PhoneActions.ParsePackages("package:com.example.app\npackage:a;rm\n"));
        Assert.Contains("Bluetooth", string.Join(" ", MobileBridge.Guide));
        Assert.Contains("Per WLAN verbinden", string.Join(" ", MobileBridge.Guide));
        Assert.Contains("Bildschirm", BluetoothLink.LimitHint);
        Assert.Contains("Kabel oder WLAN", BluetoothLink.LimitHint);
    }

    [LinuxFact(DisplayName = "X-08: BlueZ sucht ohne zu koppeln, koppelt nur nach Zustimmung, OBEX und Akku")]
    public async Task Bluez()
    {
        var calls = new List<string>();
        var run = new Rec((file, args) =>
        {
            calls.Add(args);
            if (args.Contains("scan on")) return (0, "[NEW] Device AA:BB:CC:DD:EE:FF Pixel 8\n[CHG] Device AA:BB:CC:DD:EE:FF Paired: yes\n");
            if (args.StartsWith("info ")) return (0, "Battery Percentage: 0x50 (80)\n");
            if (args.Contains(" pair ")) return (0, "Pairing successful\n");
            return (0, "");
        });
        bool Exists(string p) => p is "/usr/bin/bluetoothctl" or "/usr/bin/obexftp" or "/usr/bin/obexd";
        var gw = new BlueZGateway(run, Exists, null);
        var scan = await gw.SearchAsync();
        var dev = Assert.Single(scan.Devices);
        Assert.Equal("AA:BB:CC:DD:EE:FF", dev.Id);
        Assert.True(dev.Paired);
        Assert.Equal("Pixel 8", dev.Name);
        Assert.DoesNotContain(calls, c => c.Contains(" pair "));
        var denied = await gw.PairAsync(dev.Id, false);
        Assert.False(denied.Ok);
        Assert.DoesNotContain(calls, c => c.Contains(" pair "));
        Assert.True((await gw.PairAsync(dev.Id, true)).Ok);
        Assert.Contains(calls, c => c.Contains("pair AA:BB:CC:DD:EE:FF"));
        Assert.Equal(80, (await gw.BatteryAsync(dev.Id, true)).Percent);
        Assert.Null((await gw.BatteryAsync(dev.Id, false)).Percent);
        var file = Path.Combine(Path.GetTempPath(), "trendo-bt-" + Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(file, "hallo");
        Assert.False((await gw.SendAsync(dev.Id, file, false)).Ok);
        Assert.DoesNotContain(calls, c => c.Contains("obex") || c.StartsWith("-b"));
        Assert.True((await gw.SendAsync(dev.Id, file, true)).Ok);
        Assert.Contains(calls, c => c.Contains("-b AA:BB:CC:DD:EE:FF") && c.Contains(file));
        var folder = Path.Combine(Path.GetTempPath(), "trendo-in-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        Assert.False((await gw.ReceiveAsync(folder, false)).Ok);
        var recv = await gw.ReceiveAsync(folder, true);
        Assert.False(recv.Ok);
        Assert.Contains(calls, c => c.Contains("--nodaemon") && c.Contains(folder));
        var missing = new BlueZGateway(run, _ => false, null);
        Assert.Contains("keine Treiber", (await missing.SearchAsync()).Message);
    }

    [Fact(DisplayName = "X-08: Windows-Suche nutzt WinRT und koppelt nicht, fsquirt erst nach Zustimmung, böse Kennung fliegt raus")]
    public async Task Windows_gateway()
    {
        Assert.Contains("Windows.Devices.Bluetooth", BluetoothLink.DiscoveryScript);
        Assert.Contains("DeviceInformation", BluetoothLink.DiscoveryScript);
        Assert.Contains("CreateWatcher", BluetoothLink.DiscoveryScript);
        Assert.DoesNotContain("PairAsync", BluetoothLink.DiscoveryScript);
        var id = @"BluetoothLE#BluetoothLEdevice-1";
        var pair = BluetoothLink.PairScript(id);
        Assert.Contains("PairAsync", pair);
        Assert.Contains(id, pair);
        Assert.Contains("0000180f-0000-1000-8000-00805f9b34fb", BluetoothLink.BatteryScript(id));
        Assert.Contains("00002a19-0000-1000-8000-00805f9b34fb", BluetoothLink.BatteryScript(id));
        Assert.Throws<ArgumentException>(() => BluetoothLink.PairScript("AA:BB:CC:DD:EE:FF\"; Remove-Item"));
        Assert.False(BluetoothLink.SafeId("AA:BB:CC:DD:EE:FF$(calc)"));
        Assert.False(BluetoothLink.WinRtPairOk("NotPaired"));
        Assert.True(BluetoothLink.WinRtPairOk("AlreadyPaired"));
        var round = BluetoothLink.PowerShell("Write-Output hallo");
        Assert.Equal("Write-Output hallo", BluetoothLink.DecodePowerShell(round.Args));

        var calls = new List<(string File, string Args)>();
        var run = new Rec((file, args) =>
        {
            calls.Add((file, args));
            var script = BluetoothLink.DecodePowerShell(args);
            if (script.Contains("CreateWatcher")) return (0, "{\"id\":\"BluetoothLE#BluetoothLEdevice-1\",\"name\":\"Pixel\",\"paired\":false}\n");
            if (script.Contains("PairAsync")) return (0, "Paired\n");
            if (script.Contains("BATTERY") || script.Contains("180f")) return (0, "BATTERY 64\n");
            return (0, "");
        });
        var sys = Path.Combine(Path.GetTempPath(), "trendo-sys-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sys);
        var fsquirt = Path.Combine(sys, "fsquirt.exe");
        File.WriteAllText(fsquirt, "x");
        var gw = new WindowsBtGateway(run, File.Exists, sys, null);
        var found = Assert.Single((await gw.SearchAsync()).Devices);
        Assert.Equal("Pixel", found.Name);
        Assert.DoesNotContain(calls, c => BluetoothLink.DecodePowerShell(c.Args).Contains("PairAsync"));
        Assert.False((await gw.PairAsync(id, false)).Ok);
        Assert.DoesNotContain(calls, c => BluetoothLink.DecodePowerShell(c.Args).Contains("PairAsync"));
        Assert.True((await gw.PairAsync(id, true)).Ok);
        Assert.Equal(64, (await gw.BatteryAsync(id, true)).Percent);
        var file = Path.Combine(sys, "brief.txt");
        File.WriteAllText(file, "x");
        Assert.False((await gw.SendAsync(id, file, false)).Ok);
        Assert.DoesNotContain(calls, c => c.Args == "-send" || c.Args == "-receive");
        Assert.True((await gw.SendAsync(id, file, true)).Ok);
        Assert.Contains(calls, c => c.File == fsquirt && c.Args == "-send");
        var folder = Path.Combine(sys, "in");
        Directory.CreateDirectory(folder);
        Assert.False((await gw.ReceiveAsync(folder, false)).Ok);
        Assert.True((await gw.ReceiveAsync(folder, true)).Ok);
        Assert.Contains(calls, c => c.File == fsquirt && c.Args == "-receive");
    }

    [Fact(DisplayName = "X-08: adb und scrcpy stehen im Katalog, winget ohne --scope user, Prüfung version bzw. --version")]
    public async Task Tools()
    {
        Assert.Equal(DeliveryMode.Included, DependencyCatalog.Get("adb").Mode); // T-0222: adb steckt im scrcpy-Paket des Vollpakets
        var adb = DependencyCatalog.Get("adb") with { Mode = DeliveryMode.AutoUser };
        var scrcpy = DependencyCatalog.Get("scrcpy") with { Mode = DeliveryMode.AutoUser };
        Assert.Equal("Google.PlatformTools", adb.WingetId);
        Assert.Equal("Genymobile.scrcpy", scrcpy.WingetId);
        Assert.Equal(("adb.exe", "version"), StudioTools.Verify["adb"]);
        Assert.Equal(("scrcpy.exe", "--version"), StudioTools.Verify["scrcpy"]);
        var root = Path.Combine(Path.GetTempPath(), "trendo-dep-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var calls = new List<string>();
        var run = new Rec((_, args) => { calls.Add(args); return (0, ""); });
        var m = new DependencyManager(new AuditLog(Path.Combine(root, "a.jsonl")), run, p => p, _ => false)
        { ConsentFile = Path.Combine(root, "c.txt") };
        m.GiveConsent(Principal.LocalOwner);
        Assert.False(await m.InstallAsync(Principal.LocalOwner, adb));
        Assert.Contains("Google.PlatformTools", calls[0]);
        Assert.DoesNotContain("--scope user", calls[0]);
        Assert.False(await m.InstallAsync(Principal.LocalOwner, scrcpy));
        Assert.Contains("Genymobile.scrcpy", calls[^1]);
        Assert.DoesNotContain("--scope user", calls[^1]);
    }

    static string RepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, relative))) dir = dir.Parent;
        return Path.Combine(dir!.FullName, relative);
    }

    sealed class Rec(Func<string, string, (int Exit, string Output)> f) : IProcessRunner
    {
        public Task<(int Exit, string Output)> RunAsync(string file, string args, Action<string>? line = null, CancellationToken ct = default)
            => Task.FromResult(f(file, args));
    }
}
