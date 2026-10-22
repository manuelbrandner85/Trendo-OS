using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Xunit;
namespace Trendo.App.Tests;

sealed class AdbRunner(string devices) : IProcessRunner
{
    public List<string> Calls { get; } = [];
    public Task<(int Exit, string Output)> RunAsync(string file, string args, Action<string>? line = null, CancellationToken ct = default)
    { Calls.Add(args); return Task.FromResult((0, args.StartsWith("devices") ? devices : "1 file pushed")); }
}

public class W7_Ui
{
    [AvaloniaFact(DisplayName = "CORE-04 UI: mit echtem Begleiter koppeln (Code vom Gerät), Details holen, Erreichbarkeit, Aufgabe zuordnen, entfernen")]
    public async Task Verbund()
    {
        Assert.SkipUnless(!OperatingSystem.IsWindows(), "Übersprungen: der Begleiter-Server läuft in diesem Test nur unter Linux.");
        var (w, _, root) = Ui.Open();
        var port = 20000 + Random.Shared.Next(2000); var stateFile = Path.Combine(root, "begleiter", "zustand.json");
        using var srv = new Trendo.Companion.CompanionServer(port, stateFile, loopbackOnly: true); srv.Start();
        var st = CompanionState.Load(stateFile); st.Name = "Pi Wohnzimmer"; var code = st.NewCode(DateTime.Now); st.Save(stateFile);
        w.AllowLoopbackForTests = true;
        w.Show("verbund"); Assert.NotNull(w.TryFind<Button>("sys-tab-verbund"));
        await w.PairRemoteAsync("127.0.0.1", port, "000000" == code ? "111111" : "000000"); Assert.Contains("lehnt ab", w.W7Msg);
        await w.PairRemoteAsync("8.8.8.8", port, code); Assert.Contains("Heimnetz", w.W7Msg);
        await w.PairRemoteAsync("127.0.0.1", port, code);
        Assert.Contains("gekoppelt", w.W7Msg);
        Assert.Contains("Pi Wohnzimmer", w.Find<TextBlock>("fus-node-0").Text);
        var node = w.Verbund.Nodes()[0];
        await w.NodeInfoAsync(node); Assert.Contains("Kerne", w.W7Msg);
        await w.CheckNodesAsync(); Assert.Contains("1 von 1", w.W7Msg);
        w.Find<TextBox>("fus-task").Text = "sysinfo"; Dispatcher.UIThread.RunJobs(); w.Find<Button>("fus-route").Press();
        Assert.Contains("Pi Wohnzimmer", w.W7Msg);
        w.Find<Button>("fus-del-0").Press(); Assert.Null(w.TryFind<TextBlock>("fus-node-"));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "X-08 UI: Handy suchen (adb), Freigabe-Hinweis, Senden erst nach Zustimmung")]
    public async Task Handy()
    {
        var (w, _, root) = Ui.Open();
        var r = new AdbRunner("List of devices attached\nR58N1 device model:Pixel_8\nX2 unauthorized\n"); w.Runner = r; w.AdbOverride = "/fake/adb";
        w.Show("handy"); await w.ScanPhonesAsync();
        Assert.Contains("2 Handy", w.W7Msg); Assert.Contains("Pixel 8", w.Find<TextBlock>("phone-dev-0").Text); Assert.Contains("zulassen", w.Find<TextBlock>("phone-dev-1").Text);
        var f = Path.Combine(root, "brief.pdf"); File.WriteAllText(f, "%PDF");
        await w.PushToPhoneAsync(w.Phones[0], f, false); Assert.Contains("zustimmen", w.W7Msg); Assert.DoesNotContain(r.Calls, c => c.Contains("push"));
        await w.PushToPhoneAsync(w.Phones[0], f, true); Assert.Contains("✓", w.W7Msg); Assert.Contains(r.Calls, c => c.Contains("push"));
        await w.PhoneShotAsync(w.Phones[0], false); Assert.Contains("zustimmen", w.W7Msg); Assert.DoesNotContain(r.Calls, c => c.Contains("screencap"));
        w.AdbOverride = null; w.Close();
    }

    [AvaloniaFact(DisplayName = "X-08 UI: Verbindungsart, WLAN nur nach Haken, QR ohne gespeicherten Code, Wiederverbinden fragt")]
    public async Task Wlan()
    {
        var (w, _, root) = Ui.Open();
        var run = new PhoneScript();
        w.Runner = run; w.AdbOverride = "/fake/adb"; w.AdbLocator = null;
        run.Handle = (_, args) => args.StartsWith("devices")
            ? (0, "List of devices attached\nR58N1 device usb:1-1 model:Pixel_8\n192.168.1.20:44000 device model:Pixel_8\n")
            : (0, "");
        w.Show("handy");
        Assert.Equal("Per WLAN verbinden", w.Find<Button>("phone-wlan").Content);
        Assert.StartsWith("1. ", w.Find<TextBlock>("phone-step-0").Text!);
        Assert.Contains("Bildschirm", w.Find<TextBlock>("phone-bt-hint").Text);
        w.Find<Button>("phone-wlan").Press();
        Assert.Null(w.WlanSession);
        Assert.Contains("zustimmen", w.W7Msg);
        Assert.Empty(run.Calls);
        await w.ScanPhonesAsync();
        Assert.Contains("Kabel", w.Find<TextBlock>("phone-dev-0").Text);
        Assert.Contains("WLAN", w.Find<TextBlock>("phone-dev-1").Text);
        run.Calls.Clear();
        w.PrepareWlan(true);
        var session = w.WlanSession!;
        Assert.Matches(@"^WIFI:T:ADB;S:studio-[A-Za-z0-9]{8,16};P:\d{10};;$", session.QrPayload);
        Assert.NotNull(w.Find<Canvas>("phone-qr"));
        Assert.DoesNotContain(session.Password, string.Join("\n", w.GetLogicalTexts()));
        Assert.DoesNotContain(run.Calls, c => c.Args.Contains("pair") || c.Args.Contains("mdns"));
        run.Handle = (_, args) =>
        {
            if (args == "mdns services") return (0, $"{session.ServiceName} _adb-tls-pairing._tcp 192.168.1.20:37123\nadb-0123456789ABCDEF _adb-tls-connect._tcp 192.168.1.20:44000\n");
            if (args.StartsWith("pair ")) return (0, "Successfully paired to 192.168.1.20:37123\n");
            if (args.StartsWith("connect ")) return (0, "connected to 192.168.1.20:44000\n");
            if (args.StartsWith("devices")) return (0, "List of devices attached\n192.168.1.20:44000 device model:Pixel_8\n");
            return (0, "");
        };
        await w.FinishWlanAsync(false);
        Assert.Contains("zustimmen", w.W7Msg);
        Assert.DoesNotContain(run.Calls, c => c.Args.StartsWith("pair"));
        await w.FinishWlanAsync(true);
        Assert.Null(w.WlanSession);
        Assert.Contains("WLAN", w.W7Msg);
        Assert.Contains("WLAN", w.Find<TextBlock>("phone-dev-0").Text);
        var json = File.ReadAllText(Path.Combine(root, "handy", "bekannte.json"));
        Assert.DoesNotContain(session.Password, json);
        Assert.DoesNotContain("WIFI:", json);
        Assert.DoesNotContain(w.Core.Audit.ReadAll(), e => e.Detail.Contains(session.Password));
        Assert.Contains(run.Calls, c => c.Args == $"pair 192.168.1.20:37123 {session.Password}");
        run.Calls.Clear();
        await w.ReconnectPhoneAsync("0123456789ABCDEF", false);
        Assert.Empty(run.Calls);
        Assert.Contains("zustimmen", w.W7Msg);
        await w.ReconnectPhoneAsync("0123456789ABCDEF", true);
        Assert.Contains(run.Calls, c => c.Args.StartsWith("connect 192.168.1.20:"));
        run.Calls.Clear();
        await w.PairWithCodeAsync("8.8.8.8", "37123", "123456", "44000", true);
        Assert.Contains("Heimnetz", w.W7Msg);
        Assert.DoesNotContain(run.Calls, c => c.Args.StartsWith("pair"));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "X-08 UI: Bildschirm, Apps und Überschriften über die WLAN-Adresse, scrcpy nur nach Frage")]
    public async Task Ueber_Wlan()
    {
        var (w, _, root) = Ui.Open();
        var run = new PhoneScript();
        w.Runner = run; w.AdbOverride = "/fake/adb"; w.ScrcpyLocator = () => null;
        var dev = new PhoneDevice("192.168.1.20:44000", "device", "Pixel", PhoneLink.Wlan);
        await w.PhoneMirrorAsync(dev, true);
        Assert.True(w.PhoneAskScrcpy);
        Assert.NotNull(w.Find<Button>("phone-scrcpy-yes"));
        Assert.DoesNotContain(run.Calls, c => c.File.Contains("scrcpy"));
        w.ScrcpyOverride = "/fake/scrcpy";
        await w.PhoneMirrorAsync(dev, false);
        Assert.DoesNotContain(run.Calls, c => c.File.Contains("scrcpy"));
        await w.PhoneMirrorAsync(dev, true);
        Assert.Contains(run.Calls, c => c.File == "/fake/scrcpy" && c.Args == "-s 192.168.1.20:44000 --window-title Handy");
        run.Handle = (_, args) => args.Contains("pm list packages")
            ? (0, "package:com.example.app\n")
            : args.Contains("dumpsys notification")
                ? (0, "NotificationRecord pkg=com.example id=1\n        android.title=String (Ping)\n        android.text=String (Geheimtext)\n")
                : (0, "");
        await w.PhoneAppsAsync(dev, false);
        Assert.DoesNotContain(run.Calls, c => c.Args.Contains("pm list"));
        await w.PhoneAppsAsync(dev, true);
        Assert.Contains("com.example.app", w.PhoneDetail);
        await w.PhoneNotesAsync(dev, true);
        Assert.Contains("Ping", w.PhoneDetail);
        Assert.DoesNotContain("Geheimtext", w.PhoneDetail);
        var f = Path.Combine(root, "brief.pdf"); File.WriteAllText(f, "%PDF");
        await w.PushToPhoneAsync(dev, f, true);
        Assert.Contains(run.Calls, c => c.Args.Contains("-s 192.168.1.20:44000 push"));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "X-08 UI: Bluetooth sucht ohne zu koppeln, adb-Einrichtung erst nach Ja")]
    public async Task Bluetooth_und_Werkzeug()
    {
        var (w, _, _) = Ui.Open();
        var run = new PhoneScript();
        var bt = new BtFake();
        w.Runner = run; w.AdbLocator = () => null; w.BluetoothOverride = bt;
        w.Show("handy");
        await w.ScanPhonesAsync();
        Assert.True(w.PhoneAskAdb);
        Assert.NotNull(w.Find<Button>("phone-adb-yes"));
        Assert.DoesNotContain(run.Calls, c => c.File == "winget");
        await w.InstallPhoneToolAsync("adb");
        Assert.Contains(run.Calls, c => c.File == "winget" && c.Args.Contains("Google.PlatformTools") && !c.Args.Contains("--scope user"));
        await w.SearchBluetoothAsync();
        Assert.Contains("Pixel", w.Find<TextBlock>("phone-bt-0").Text);
        Assert.Equal(["search"], bt.Calls);
        w.Find<Button>("phone-bt-pair-0").Press();
        Assert.DoesNotContain(bt.Calls, c => c.StartsWith("pair"));
        Assert.Contains("zustimmen", w.W7Msg);
        await w.PairBluetoothAsync("AA:BB:CC:DD:EE:FF", true);
        Assert.Contains("pair AA:BB:CC:DD:EE:FF", bt.Calls);
        bt.ReceiveThrows = true;
        await w.ReceiveBluetoothAsync(false);
        Assert.DoesNotContain(bt.Calls, c => c == "receive");
        await w.ReceiveBluetoothAsync(true);
        Assert.Contains("Empfang beendet", w.W7Msg);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "SYS-01 UI: Betriebsmodus zeigt aktuellen Modus und Fähigkeiten")]
    public void Modus()
    {
        var (w, _, _) = Ui.Open();
        w.Show("modus");
        Assert.Contains(OperatingSystem.IsWindows() ? "Windows-App" : "Linux", w.Find<TextBlock>("mode-now").Text);
        Assert.NotNull(w.TryFind<TextBlock>("mode-cap-7"));
        w.Close();
    }
}

sealed class PhoneScript : IProcessRunner
{
    public List<(string File, string Args)> Calls { get; } = [];
    public Func<string, string, (int Exit, string Output)> Handle = (_, _) => (0, "");
    public Task<(int Exit, string Output)> RunAsync(string file, string args, Action<string>? line = null, CancellationToken ct = default)
    {
        Calls.Add((file, args));
        return Task.FromResult(Handle(file, args));
    }
}

sealed class BtFake : IBluetoothGateway
{
    public List<string> Calls { get; } = [];
    public bool ReceiveThrows { get; set; }
    public string Backend => "Fake";
    public Task<BtScan> SearchAsync(CancellationToken ct = default)
    {
        Calls.Add("search");
        return Task.FromResult(new BtScan([new BtDevice("AA:BB:CC:DD:EE:FF", "Pixel", false, null)], "1 Gerät gefunden."));
    }
    public Task<(bool Ok, string Message)> PairAsync(string id, bool consent, CancellationToken ct = default)
    {
        Calls.Add("pair " + id);
        return Task.FromResult((true, "Gekoppelt."));
    }
    public Task<(bool Ok, string Message)> SendAsync(string id, string file, bool consent, CancellationToken ct = default)
    {
        Calls.Add("send");
        return Task.FromResult((true, "gesendet"));
    }
    public Task<(bool Ok, string Message)> ReceiveAsync(string folder, bool consent, CancellationToken ct = default)
    {
        Calls.Add("receive");
        if (ReceiveThrows) throw new OperationCanceledException();
        return Task.FromResult((false, "keine Datei"));
    }
    public Task<(int? Percent, string Message)> BatteryAsync(string id, bool consent, CancellationToken ct = default)
    {
        Calls.Add("bat");
        return Task.FromResult<(int?, string)>((40, "Akku 40 %"));
    }
}
