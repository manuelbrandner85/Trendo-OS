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
    [AvaloniaFact(DisplayName = "CORE-04 UI: Code erzeugen, Pi koppeln, Erreichbarkeit prüfen, Aufgabe zuordnen, entfernen")]
    public async Task Verbund()
    {
        var (w, _, _) = Ui.Open();
        w.Show("verbund"); Assert.NotNull(w.TryFind<Button>("sys-tab-verbund"));
        w.Find<Button>("fus-newcode").Press();
        Assert.Contains(w.LastPairCode!.Code, w.Find<TextBlock>("fus-code").Text);
        w.Find<TextBox>("fus-name").Text = "Pi Wohnzimmer"; w.Find<TextBox>("fus-addr").Text = "192.168.178.40";
        w.Find<TextBox>("fus-caps").Text = "ki, sensor"; w.Find<TextBox>("fus-pcode").Text = w.LastPairCode.Code;
        Dispatcher.UIThread.RunJobs(); w.Find<Button>("fus-pair").Press();
        Assert.Contains("gekoppelt", w.W7Msg);
        Assert.Contains("Pi Wohnzimmer", w.Find<TextBlock>("fus-node-0").Text);
        w.HealthOverride = u => Task.FromResult(u.Host == "192.168.178.40");
        await w.CheckNodesAsync(); Assert.Contains("1 von 1", w.W7Msg); Assert.StartsWith("🟢", w.Find<TextBlock>("fus-node-0").Text);
        w.Find<TextBox>("fus-task").Text = "sensor"; Dispatcher.UIThread.RunJobs(); w.Find<Button>("fus-route").Press();
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
        w.AdbOverride = null; w.Close();
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
