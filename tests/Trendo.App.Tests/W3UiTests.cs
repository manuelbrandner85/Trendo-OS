using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

public class CORE11_Ui
{
    [AvaloniaFact(DisplayName = "CORE-11 UI: Schutz-Seite über System erreichbar, Sichern, Ordner prüfen")]
    public void Security_page()
    {
        var (w, _, root) = Ui.Open();
        w.Show("system");
        Assert.Equal("CORE-11", MainWindow.StudioPages.First(kv => kv.Value == "schutz").Key);
        w.Show("schutz");
        var s1 = w.Find<TextBlock>("sec-score").Text;
        w.Find<Button>("sec-fix-backup").Press();
        Assert.NotEqual(s1, w.Find<TextBlock>("sec-score").Text);
        var d = Path.Combine(root, "pr"); Directory.CreateDirectory(d); File.WriteAllText(Path.Combine(d, "a.txt"), "password=geheimnis");
        w.Find<TextBox>("sec-folder").Text = d;
        w.Find<Button>("sec-scan").Press();
        for (var i = 0; i < 50 && w.LastPrivacy.Count == 0; i++) { Thread.Sleep(20); W2Ui.Settle(); }
        Assert.Single(w.LastPrivacy);
        Assert.Contains("1 Fundstelle", w.Find<TextBlock>("sec-msg").Text);
    }
}

public class X02_Ui
{
    [AvaloniaFact(DisplayName = "X-02 UI: Beim Start automatisch geprüft, Seite zeigt Ergebnis, erneut prüfen")]
    public void Heal_page()
    {
        var (w, _, _) = Ui.Open();
        w.StartupHeal!.Wait(5000); W2Ui.Settle();
        Assert.NotEmpty(w.LastHeal);
        w.Show("heilung");
        Assert.NotEqual("Noch nicht geprüft.", w.Find<TextBlock>("heal-summary").Text);
        w.Find<Button>("heal-run").Press();
        Assert.NotNull(w.TryFind<Button>("sys-tab-schutz"));
    }
}

public sealed class FakePkg : IPackageBackend
{
    public List<string> Done { get; } = [];
    public string Name => "Test-Paketverwaltung"; public string Exe => "x";
    public Task<IReadOnlyList<PackageInfo>> SearchAsync(string q, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<PackageInfo>>([new("VLC", "VideoLAN.VLC", "3.0")]);
    public Task<IReadOnlyList<PackageInfo>> InstalledAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<PackageInfo>>([new("Git", "Git.Git", "2.45")]);
    public Task<IReadOnlyList<PackageInfo>> UpgradesAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<PackageInfo>>([new("Git", "Git.Git", "2.45", "2.47")]);
    public Task<(bool Ok, string Log)> InstallAsync(string id, Action<string>? l = null, CancellationToken ct = default) { Done.Add("i " + id); return Task.FromResult((true, "")); }
    public Task<(bool Ok, string Log)> UpgradeAsync(string id, Action<string>? l = null, CancellationToken ct = default) { Done.Add("u " + id); return Task.FromResult((true, "")); }
    public Task<(bool Ok, string Log)> UninstallAsync(string id, Action<string>? l = null, CancellationToken ct = default) { Done.Add("x " + id); return Task.FromResult((true, "")); }
}

public class SYS04_Ui
{
    [AvaloniaFact(DisplayName = "SYS-04 UI: Suchen, installieren, Updates anzeigen und aktualisieren, entfernen")]
    public async Task AppCenter_page()
    {
        var (w, _, _) = Ui.Open();
        var pkg = new FakePkg(); w.AppBackendOverride = pkg;
        w.Show("appcenter");
        await w.AppSearchAsync("vlc"); w.Show("appcenter");
        w.Find<Button>("appc-hit-install-0").Press(); W2Ui.Settle();
        Assert.Contains("VLC: installiert", w.Find<TextBlock>("appc-msg").Text);
        await w.AppLoadAsync(); w.Show("appcenter");
        w.Find<Button>("appc-upd-upgrade-0").Press(); W2Ui.Settle();
        w.Find<Button>("appc-inst-uninstall-0").Press(); W2Ui.Settle();
        Assert.Equal(["i VideoLAN.VLC", "u Git.Git", "x Git.Git"], pkg.Done);
    }
}

public class SYS07_Ui
{
    static string FakeSys(string root)
    {
        var s = Path.Combine(root, "sys");
        void W(string p, string v) { var f = Path.Combine(s, p); Directory.CreateDirectory(Path.GetDirectoryName(f)!); File.WriteAllText(f, v); }
        W("bus/usb/devices/1-2/product", "HD Webcam"); W("bus/usb/devices/1-2/bDeviceClass", "0e"); W("class/net/eth0/operstate", "up");
        return s;
    }
    [AvaloniaFact(DisplayName = "SYS-07 UI: Geräte suchen zeigt Gruppen")]
    public async Task Devices_page()
    {
        var (w, _, root) = Ui.Open();
        w.DeviceSysRootOverride = FakeSys(root);
        w.Show("geraete");
        await w.ScanDevicesAsync(); w.Show("geraete");
        Assert.Contains("Geräte gefunden", w.Find<TextBlock>("dev-msg").Text);
        Assert.NotNull(w.TryFind<StackPanel>("dev-group-Kamera"));
    }
}

public class SYS06_Ui
{
    [AvaloniaFact(DisplayName = "SYS-06 UI: Ordner teilen, Datei echt per HTTP laden, falsches Token abgelehnt, beenden")]
    public async Task Share_page()
    {
        var (w, _, root) = Ui.Open();
        var d = Path.Combine(root, "teilen"); Directory.CreateDirectory(d); File.WriteAllText(Path.Combine(d, "hallo.txt"), "Hallo Handy");
        w.ShareAddressOverride = System.Net.IPAddress.Loopback;
        w.Show("teilen");
        await w.StartShareAsync(d, TimeSpan.FromHours(1), System.Net.IPAddress.Loopback); w.Show("teilen");
        var url = w.Find<SelectableTextBlock>("share-url").Text!;
        using var http = new HttpClient();
        Assert.Contains("hallo.txt", await http.GetStringAsync(url));
        Assert.Equal("Hallo Handy", await http.GetStringAsync(url + "hallo.txt"));
        Assert.Equal(System.Net.HttpStatusCode.NotFound, (await http.GetAsync(url.Replace(w.ActiveShare!.Share.Token, "0000"))).StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.MethodNotAllowed, (await http.PostAsync(url + "hallo.txt", new StringContent("x"))).StatusCode);
        Assert.Equal(1, w.ActiveShare.Share.Downloads);
        w.Find<Button>("share-stop").Press(); await Task.Delay(100); W2Ui.Settle();
        Assert.Null(w.ActiveShare);
    }
}
