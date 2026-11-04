using Avalonia.LogicalTree;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Trendo.App;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Prüfung 04.11.2026 (Wunsch „nichts zu verschachtelt, einfach verständlich, ohne Funktionsverlust“): jede Seite öffnen und messen,
/// wie viele Reiterleisten gleichzeitig übereinander stehen (Navigationsebenen), wie tief Aufklapper und Scrollbereiche ineinander liegen
/// und wie viele Knöpfe auf einmal sichtbar sind. Bericht in %TEMP%\trendo-einfachheit.txt (nicht im Repo).</summary>
public class Einfachheit_Ui(ITestOutputHelper log)
{
    public sealed record Measure(string Key, int NavBars, int ExpanderDepth, int ScrollDepth, int Buttons, int TreeDepth, string Bars)
    {
        /// <summary>Grob: je Navigationsebene über zwei, je Aufklapper-Ebene über eins, je Scroll-Ebene über eins ein Punkt; viele Knöpfe kosten auch.</summary>
        public int Score => Math.Max(0, NavBars - 2) * 3 + Math.Max(0, ExpanderDepth - 1) * 2 + Math.Max(0, ScrollDepth - 1) * 2 + Math.Max(0, Buttons - 60) / 20;
    }

    static string? Bg(Button b) => b.Background is ISolidColorBrush s ? s.Color.ToString() : b.Background?.GetType().Name;

    /// <summary>Reiterleiste: Leiste mit mindestens drei Knöpfen, von denen genau einer anders hinterlegt ist (der gewählte).</summary>
    static bool IsNavBar(Panel p)
    {
        var bs = p.Children.OfType<Button>().Where(b => b.IsVisible).ToList();
        if (bs.Count < 3 || bs.Count > 16) return false;
        var groups = bs.GroupBy(Bg).ToList();
        if (!(groups.Count == 2 && groups.Any(g => g.Count() == 1) && groups.Any(g => g.Count() >= 2))) return false;
        // nur Navigation (Reiter, Abschnitte, Gruppen), keine Befehlsgruppe mit hervorgehobenem Hauptknopf
        return bs.Count(b => b.Name is { } n && (n.Contains("-tab-") || n.Contains("-sec-") || n.Contains("-nav-") || n.Contains("-g-") || n.Contains("-mode") || n.Contains("-section"))) >= 3;
    }

    /// <summary>Eigene Scrollbereiche der Seite – nicht die eingebauten von Eingabefeldern, Listen oder dem Text-Editor.</summary>
    /// <summary>Waagerechte Streifen (Zeitleiste, Schnittlage) zählen nicht: sie scrollen quer zur Seite und stören nicht.</summary>
    static bool Own(Visual v) => v is not ScrollViewer sv || (sv.TemplatedParent is null && sv.VerticalScrollBarVisibility != ScrollBarVisibility.Disabled);
    static int Depth<T>(Visual v, int d = 0) where T : Visual
    {
        var here = v is T && Own(v) ? d + 1 : d; var max = here;
        foreach (var c in v.GetVisualChildren()) max = Math.Max(max, Depth<T>(c, here));
        return max;
    }
    static int TreeDepth(Visual v) => 1 + v.GetVisualChildren().Select(TreeDepth).DefaultIfEmpty(0).Max();

    public static Measure Of(string key, Control root)
    {
        var all = root.GetVisualDescendants().OfType<Control>().Where(c => c.IsEffectivelyVisible).ToList();
        var bars = all.OfType<Panel>().Where(IsNavBar).ToList();
        var names = string.Join(", ", bars.Select(b => b.Name ?? b.Children.OfType<Button>().FirstOrDefault()?.Name ?? "?"));
        var deepScroll = all.OfType<ScrollViewer>().Where(sv => Own(sv) && sv.GetVisualAncestors().OfType<ScrollViewer>().Count(Own) >= 2).Select(sv => string.Join(">", sv.GetVisualAncestors().OfType<ScrollViewer>().Where(Own).Reverse().Append(sv).Select(x => x.Name ?? (x.Parent as Control)?.Name ?? x.GetVisualAncestors().OfType<Control>().FirstOrDefault(c => c.Name is { Length: > 0 })?.Name ?? "?"))).Distinct().Take(3);
        if (deepScroll.Any()) names += " | Scroll: " + string.Join("; ", deepScroll);
        return new(key, bars.Count, Depth<Expander>(root), Depth<ScrollViewer>(root), all.OfType<Button>().Count(), TreeDepth(root), names);
    }

    [AvaloniaFact(DisplayName = "Einfachheit: jede Seite höchstens drei Reiterleisten auf einmal, Aufklapper und Scrollbereiche höchstens zwei Ebenen tief; Bericht der verschachteltsten Seiten")]
    public void Every_page_is_simple()
    {
        var (w, _, _) = Ui.Open(); w.Width = 1600; w.Height = 1000;
        var rows = new List<Measure>();
        try
        {
            foreach (var key in w.PageKeys.OrderBy(k => k).ToList())
            {
                foreach (var old in w.Windows.Stack.Reverse().Skip(3).ToList()) w.CloseWindow(old.Id);
                try
                {
                    w.Show(key); Dispatcher.UIThread.RunJobs(); w.UpdateLayout();
                    if (w.ActivePageRoot is { } r) rows.Add(Of(key, r));
                    // Programme der Programm-Hülle: auch mit offenem Dokument messen (Arbeitsfläche)
                    if (MainWindow.Programs.TryGetValue(key, out var spec) && spec.Templates.Count > 0)
                    {
                        w.ProgramNew(key, spec.Templates[0].Id); w.Show(key); Dispatcher.UIThread.RunJobs(); w.UpdateLayout();
                        if (w.ActivePageRoot is { } r2) rows.Add(Of(key + " (Dokument)", r2));
                    }
                }
                catch (Exception e) when (e is InvalidOperationException or ArgumentException or IOException or NotSupportedException) { }
            }
        }
        finally { w.Close(); }
        var worst = rows.OrderByDescending(r => r.Score).ThenByDescending(r => r.NavBars).ThenByDescending(r => r.Buttons).ToList();
        var report = string.Join("\n", worst.Select(r => $"{r.Key,-34} Punkte {r.Score,2} · Reiterleisten {r.NavBars} · Aufklapper {r.ExpanderDepth} · Scroll {r.ScrollDepth} · Knöpfe {r.Buttons,3} · Tiefe {r.TreeDepth,3}  [{r.Bars}]"));
        File.WriteAllText(Path.Combine(Path.GetTempPath(), "trendo-einfachheit.txt"), $"{rows.Count} Ansichten\n" + report);
        log.WriteLine(report);
        Assert.True(rows.Count > 40, $"nur {rows.Count} Ansichten");
        Assert.Empty(rows.Where(r => r.NavBars > 3 || r.ExpanderDepth > 2 || r.ScrollDepth > 2).Select(r => $"{r.Key}: {r.NavBars} Reiterleisten, Aufklapper {r.ExpanderDepth}, Scroll {r.ScrollDepth} [{r.Bars}]"));
    }
    [AvaloniaFact(DisplayName = "Einfach zuerst: neue Nutzer öffnen Programme in der Stufe „Einfach“ (weniger Werkzeuge, alle Funktionen über „Erweitert“/„Professionell“ erreichbar)")]
    public void New_users_start_simple()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-ui-" + Guid.NewGuid().ToString("N"));
        var core = new Trendo.Core.TrendoCore(root, new Trendo.Core.InMemorySecretStore(), new FakeLauncher());
        var w = new MainWindow(core, new AppOptions(root, null, 0, false, Isolated: true), new Probe("browser")); w.Show(); Dispatcher.UIThread.RunJobs();
        try
        {
            Assert.Equal(Trendo.Core.Programs.UserLevel.Einfach, w.ProgramLevel);
            w.Show("ton"); w.ProgramNew("ton"); w.Show("ton"); Dispatcher.UIThread.RunJobs();
            var simple = w.GetLogicalDescendants().OfType<Button>().Count(b => b.Name?.StartsWith("prog-cmd-") == true);
            Assert.Null(w.TryFind<Button>("prog-cmd-au-compress")); // Profi-Werkzeug erst später
            w.ProgramSetLevel(Trendo.Core.Programs.UserLevel.Profi, "ton"); Dispatcher.UIThread.RunJobs();
            Assert.NotNull(w.TryFind<Button>("prog-cmd-au-compress"));
            Assert.True(w.GetLogicalDescendants().OfType<Button>().Count(b => b.Name?.StartsWith("prog-cmd-") == true) > simple);
        }
        finally { w.Close(); }
    }
}
