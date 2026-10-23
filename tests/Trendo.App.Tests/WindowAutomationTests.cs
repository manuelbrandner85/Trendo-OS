using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>Bildschirmleser und Testautomatisierung lesen den AutomationPeer-Baum.
/// Ein maximiertes Fenster darf den Baum nicht abreißen – auch die anderen Fenster bleiben mit Namen erreichbar.</summary>
public class Fenster_Automation_Ui
{
    [AvaloniaFact(DisplayName = "Fenster: maximiert und daneben geöffnet bleiben im AutomationPeer-Baum, mit Namen und endlichem Rechteck")]
    public void Maximized_windows_stay_in_automation_tree()
    {
        var (w, _, _) = Ui.Open();
        w.WindowState = WindowState.Maximized;
        w.Show("settings");
        w.Show("werkzeuge");
        Settle();
        w.UpdateLayout();
        Settle();

        var before = Inspect(w);
        AssertReachable(before, w, "settings", "werkzeuge");

        w.Windows.ToggleMaximize("settings");
        Settle();
        w.UpdateLayout();
        Settle();
        Assert.True(w.Windows.Get("settings")!.Maximized);

        var maximized = Inspect(w);
        AssertReachable(maximized, w, "settings", "werkzeuge");
        var max = maximized.ById["win-settings"];
        Assert.Equal(AutomationControlType.Window, max.Type);
        Assert.True(max.Bounds.Width > 50 && max.Bounds.Height > 50, $"maximiertes Fenster ohne Fläche: {max.Bounds}");
        Assert.True(maximized.ById.TryGetValue("work-area", out var area));
        Assert.Equal("Arbeitsfläche", area.Name);
        Assert.NotEqual(max.Name, area.Name);

        w.Windows.ToggleMaximize("settings");
        Settle();
        w.UpdateLayout();
        Settle();
        Assert.False(w.Windows.Get("settings")!.Maximized);
        AssertReachable(Inspect(w), w, "settings", "werkzeuge");
        w.Close();
    }

    static void AssertReachable(TreeReport tree, MainWindow w, params string[] ids)
    {
        Assert.True(tree.Issues.Count == 0, string.Join("\n", tree.Issues));
        foreach (var id in ids)
        {
            var title = w.Windows.Get(id)!.Title;
            Assert.True(tree.ById.ContainsKey("win-" + id), $"win-{id} fehlt im Baum ({title})");
            var node = tree.ById["win-" + id];
            Assert.Equal(title, node.Name);
            Assert.Equal(AutomationControlType.Window, node.Type);
            Assert.True(IsFinite(node.Bounds) && node.Bounds.Width > 0 && node.Bounds.Height > 0, $"win-{id} Rechteck {node.Bounds}");
            Assert.True(node.Screen is { } s && IsFinite(s) && s.Width > 0 && s.Height > 0, $"win-{id} Bildschirmrechteck {node.Screen}");
        }
    }

    static bool IsFinite(Rect r) =>
        double.IsFinite(r.X) && double.IsFinite(r.Y) && double.IsFinite(r.Width) && double.IsFinite(r.Height);

    static TreeReport Inspect(Window w)
    {
        var report = new TreeReport();
        Walk(ControlAutomationPeer.CreatePeerForElement(w), null, [], report, 0);
        return report;
    }

    static void Walk(AutomationPeer peer, AutomationPeer? expectedParent, HashSet<AutomationPeer> seen, TreeReport report, int depth)
    {
        if (depth > 80) { report.Issues.Add("Baum tiefer als 80"); return; }
        if (!seen.Add(peer)) { report.Issues.Add("Kreis bei " + Id(peer)); return; }
        var id = Safe(() => peer.GetAutomationId(), report);
        var name = Safe(() => peer.GetName(), report);
        Rect bounds = default;
        try { bounds = peer.GetBoundingRectangle(); }
        catch (Exception ex) { report.Issues.Add($"GetBoundingRectangle wirft bei {id}/{name}: {ex.GetType().Name}: {ex.Message}"); }
        if (!IsFinite(bounds) || bounds.Width < 0 || bounds.Height < 0)
            report.Issues.Add($"Rechteck ungültig bei {id}/{name}: {bounds}");
        Rect? screen = null;
        try { screen = peer.ToScreen(bounds); }
        catch (Exception ex) { report.Issues.Add($"ToScreen wirft bei {id}/{name} bounds={bounds}: {ex.GetType().Name}: {ex.Message}"); }
        if (screen is { } s && (!IsFinite(s) || s.Width < 0 || s.Height < 0))
            report.Issues.Add($"Bildschirmrechteck ungültig bei {id}/{name}: {s}");
        AutomationControlType type = default;
        try { type = peer.GetAutomationControlType(); }
        catch (Exception ex) { report.Issues.Add($"ControlType wirft bei {id}/{name}: {ex.GetType().Name}: {ex.Message}"); }
        if (!string.IsNullOrEmpty(id) && !report.ById.ContainsKey(id))
            report.ById[id] = new Node(name ?? "", bounds, screen, type);
        try
        {
            if (peer.GetProvider<IScrollProvider>() is { } scroll)
            {
                double[] nums = [scroll.HorizontalScrollPercent, scroll.VerticalScrollPercent, scroll.HorizontalViewSize, scroll.VerticalViewSize];
                if (nums.Any(n => !double.IsFinite(n)))
                    report.Issues.Add($"Scroll nicht endlich bei {id}/{name}");
            }
            if (peer.GetProvider<IRangeValueProvider>() is { } range &&
                (!double.IsFinite(range.Value) || !double.IsFinite(range.Minimum) || !double.IsFinite(range.Maximum)))
                report.Issues.Add($"Bereich nicht endlich bei {id}/{name}");
        }
        catch (Exception ex) { report.Issues.Add($"Muster wirft bei {id}/{name}: {ex.GetType().Name}: {ex.Message}"); }
        AutomationPeer? parent = null;
        try { parent = peer.GetParent(); }
        catch (Exception ex) { report.Issues.Add($"GetParent wirft bei {id}/{name}: {ex.GetType().Name}: {ex.Message}"); }
        if (expectedParent is not null && !ReferenceEquals(parent, expectedParent))
            report.Issues.Add($"Elternzeiger stimmt nicht bei {id}/{name}: gemeldet {Id(parent)}, erwartet {Id(expectedParent)}");
        IReadOnlyList<AutomationPeer> children;
        try { children = peer.GetChildren(); }
        catch (Exception ex) { report.Issues.Add($"GetChildren wirft bei {id}/{name}: {ex.GetType().Name}: {ex.Message}"); return; }
        foreach (var child in children)
        {
            if (child is null) { report.Issues.Add($"null-Kind bei {id}/{name}"); continue; }
            Walk(child, peer, seen, report, depth + 1);
        }
    }

    static string Id(AutomationPeer? peer)
    {
        if (peer is null) return "(keins)";
        try { return peer.GetAutomationId() ?? peer.GetClassName(); }
        catch { return "?"; }
    }

    static string? Safe(Func<string?> read, TreeReport report)
    {
        try { return read(); }
        catch (Exception ex) { report.Issues.Add($"{ex.GetType().Name}: {ex.Message}"); return null; }
    }

    sealed class TreeReport
    {
        public List<string> Issues { get; } = [];
        public Dictionary<string, Node> ById { get; } = [];
    }

    sealed record Node(string Name, Rect Bounds, Rect? Screen, AutomationControlType Type);
}
