using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Trendo.App;
using Trendo.Core;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(Trendo.App.Tests.TestApp))]

namespace Trendo.App.Tests;

public static class TestApp
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}

public sealed class FakeLauncher : IProcessLauncher
{
    public List<string> Opened { get; } = [];
    public void Open(string target) => Opened.Add(target);
}
public sealed class Probe(params string[] t) : IToolProbe { public bool IsAvailable(string x) => t.Contains(x); }

public static class Ui
{
    public static (MainWindow W, FakeLauncher L, string Root) Open(bool offline = false)
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-ui-" + Guid.NewGuid().ToString("N"));
        var l = new FakeLauncher();
        var core = new TrendoCore(root, new InMemorySecretStore(), l, offline: offline);
        var w = new MainWindow(core, new AppOptions(root, null, 0, offline), new Probe("browser"));
        w.Show();
        Dispatcher.UIThread.RunJobs();
        return (w, l, root);
    }
    public static T Find<T>(this Window w, string name) where T : Control =>
        w.GetLogicalDescendants().OfType<T>().First(c => c.Name == name);
    public static T? TryFind<T>(this Window w, string prefix) where T : Control =>
        w.GetLogicalDescendants().OfType<T>().FirstOrDefault(c => c.Name?.StartsWith(prefix) == true);
    public static void Press(this Button b) { b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Dispatcher.UIThread.RunJobs(); }
    public static void Key(this Window w, PhysicalKey k, RawInputModifiers m = RawInputModifiers.None)
    { w.KeyPressQwerty(k, m); w.KeyReleaseQwerty(k, m); Dispatcher.UIThread.RunJobs(); }
}

/// QA-017 (Teil Tastatur): Navigation ohne Maus
public class QA017_Keyboard
{
    [AvaloniaFact(DisplayName = "QA-017: Strg+1..5 schaltet alle Hauptbereiche per Tastatur")]
    public void Ctrl_shortcuts_switch_views()
    {
        var (w, _, _) = Ui.Open();
        var expected = new[] { "start", "studios", "projects", "approvals", "system" };
        var keys = new[] { PhysicalKey.Digit1, PhysicalKey.Digit2, PhysicalKey.Digit3, PhysicalKey.Digit4, PhysicalKey.Digit5 };
        for (var i = 4; i >= 0; i--)
        {
            w.Key(keys[i], RawInputModifiers.Control);
            Assert.Equal(expected[i], w.CurrentView);
        }
        w.Close();
    }

    [AvaloniaFact(DisplayName = "QA-017: Tab bewegt Fokus durch die Navigation, Enter aktiviert")]
    public void Tab_and_enter()
    {
        var (w, _, _) = Ui.Open();
        var start = w.Find<Button>("nav-start");
        start.Focus(NavigationMethod.Tab);
        Dispatcher.UIThread.RunJobs();
        w.Key(PhysicalKey.Tab);
        var focused = w.FocusManager?.GetFocusedElement() as Button;
        Assert.Equal("nav-studios", focused?.Name);
        w.Key(PhysicalKey.Enter);
        Assert.Equal("studios", w.CurrentView);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "QA-017: Navigationsschaltflächen haben zugängliche Namen")]
    public void Accessible_names()
    {
        var (w, _, _) = Ui.Open();
        foreach (var n in new[] { "nav-start", "nav-studios", "nav-projects", "nav-approvals", "nav-system" })
            Assert.False(string.IsNullOrWhiteSpace(Avalonia.Automation.AutomationProperties.GetName(w.Find<Button>(n))));
        w.Close();
    }
}

/// QA-004 (UI): Aktionen nur über das Approval-Gate
public class QA004_UiApproval
{
    [AvaloniaFact(DisplayName = "QA-004 UI: Explorer-Aktion → Freigabe-Ansicht; Ausführung erst nach Klick auf Freigeben")]
    public void Explorer_through_gate()
    {
        var (w, l, _) = Ui.Open();
        w.Core.Projects.Create("ui-test", "UI-Test");
        w.Show("projects");
        w.Find<Button>("btn-open-folder-ui-test").Press();
        Assert.Equal("approvals", w.CurrentView);
        Assert.Empty(l.Opened);
        w.TryFind<Button>("btn-approve-")!.Press();
        Assert.Single(l.Opened);
        Assert.EndsWith(Path.Combine("ui-test", "files"), l.Opened[0]);
        Assert.Contains(w.Core.Audit.ReadAll(), e => e.Action == "execution.done");
        Assert.Null(w.TryFind<Button>("btn-approve-")); // keine erneute Ausführung möglich
        w.Close();
    }

    [AvaloniaFact(DisplayName = "QA-004 UI: Browser-Aktion abgelehnt → nichts geöffnet")]
    public void Browser_denied()
    {
        var (w, l, _) = Ui.Open();
        w.Show("projects");
        w.Find<Button>("btn-open-url").Press();
        Assert.Equal("approvals", w.CurrentView);
        w.TryFind<Button>("btn-deny-")!.Press();
        Assert.Empty(l.Opened);
        Assert.Contains(w.Core.Audit.ReadAll(), e => e.Action == "approval.denied");
        w.Close();
    }

    [AvaloniaFact(DisplayName = "QA-004 UI: Browser-Aktion freigegeben → genau einmal geöffnet")]
    public void Browser_approved_once()
    {
        var (w, l, _) = Ui.Open();
        w.Show("projects");
        w.Find<Button>("btn-open-url").Press();
        w.TryFind<Button>("btn-approve-")!.Press();
        Assert.Equal(["https://github.com/manuelbrandner85/Trendo-OS"], l.Opened);
        w.Close();
    }
}

/// QA-019 (simuliert): Offline-Modus
public class QA019_OfflineUi
{
    [AvaloniaFact(DisplayName = "QA-019 (simuliert): Offline-Modus – Browser-Aktion deaktiviert, lokale Funktionen laufen")]
    public void Offline_mode()
    {
        var (w, l, _) = Ui.Open(offline: true);
        w.Core.Projects.Create("offline-ui", "Offline");
        w.Show("projects");
        Assert.False(w.Find<Button>("btn-open-url").IsEnabled);
        Assert.Throws<OfflineException>(() => w.Core.Integrations.PlanOpenUrl(Principal.LocalOwner, "https://example.org"));
        w.Find<Button>("btn-open-folder-offline-ui").Press();
        w.TryFind<Button>("btn-approve-")!.Press();
        Assert.Single(l.Opened);
        w.Close();
    }
}
