using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Shell;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>UI41-SM-01: jedes Katalog-Studio startet aus dem Startmenü im eigenen Fenster, mit Taskleiste.</summary>
public class StartMenu_StudioFenster
{
    static async Task WaitOpen(MainWindow w, string id)
    {
        if (id.Equals("CORE-05", StringComparison.OrdinalIgnoreCase) || !StudioTools.Required.ContainsKey(id))
        {
            for (var i = 0; i < 8; i++) { Dispatcher.UIThread.RunJobs(); await Task.Delay(10); }
            return;
        }
        for (var i = 0; i < 150; i++)
        {
            Dispatcher.UIThread.RunJobs();
            if (w.Core.Audit.ReadAll().Any(e => e.Action == "deps.check" && e.Detail.StartsWith(id + ":", StringComparison.OrdinalIgnoreCase)))
            { Dispatcher.UIThread.RunJobs(); return; }
            await Task.Delay(30);
        }
        Assert.Fail(id + ": der Start aus dem Menü wurde nicht fertig");
    }

    static void Prepare(MainWindow w, double width, double height)
    {
        w.WindowState = WindowState.Normal; w.Width = width; w.Height = height;
        Settle(); w.UpdateLayout(); Settle();
    }

    static Button MenuButton(MainWindow w, string name)
    {
        var b = w.GetVisualDescendants().OfType<Button>().First(c => c.Name == name);
        b.BringIntoView(); Settle();
        return V<Button>(w, name);
    }

    [AvaloniaFact(DisplayName = "Startmenü: jedes Katalog-Studio öffnet das eigene Fenster, Taskleiste, Minimieren, Wiederherstellen, Schließen", Timeout = 300000)]
    public async Task Jedes_Studio()
    {
        var (w, _, _) = Ui.Open();
        Prepare(w, 1440, 900);
        w.Show("start"); Settle();
        Assert.True(StudioCatalog.ById.Count >= 54, "Katalog kleiner als 54");
        foreach (var id in StudioCatalog.ById.Keys)
            Assert.Contains(w.Studios, s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));

        foreach (var (id, info) in StudioCatalog.ById)
        {
            RealClick(w, V<Button>(w, "start-button"));
            Assert.True(w.StartMenuOpen, id);
            var item = MenuButton(w, "sm-az-" + id);
            Assert.Equal(info.Name, Avalonia.Automation.AutomationProperties.GetName(item));
            if (!w.HasStudioWorkspace(id) && w.StudioStatusLabel(id) == "Noch nicht verfügbar")
                Assert.Contains("Noch nicht verfügbar", item.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text));
            RealClick(w, item);
            await WaitOpen(w, id);

            Assert.NotEqual("studios", w.CurrentView);
            Assert.False(w.StartMenuOpen, id);
            var win = w.Windows.Active;
            Assert.NotNull(win);
            Assert.Equal(info.Name, win!.Title);
            Assert.Equal(info.Name, V<TextBlock>(w, "win-titletext-" + win.Id).Text);
            var task = V<Button>(w, "task-" + win.Id);
            Assert.Equal(info.Name, Avalonia.Automation.AutomationProperties.GetName(task));
            if (StudioIcons.For(id) is not null)
                Assert.NotEmpty(task.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>());
            Assert.NotNull(w.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.Name == "win-max-" + win.Id));
            Assert.NotNull(w.GetVisualDescendants().OfType<Border>().FirstOrDefault(b => b.Name == "win-size-BR-" + win.Id));

            if (!w.HasStudioWorkspace(id))
            {
                Assert.StartsWith("studio-status-", w.CurrentView);
                var label = w.Find<TextBlock>("studio-status-label");
                Assert.Equal(w.StudioStatusLabel(id), label.Text);
                var st = w.Studios.First(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));
                if (st.Capability is Capability.SpecOnly or Capability.Rejected or Capability.NotConnected)
                    Assert.Equal("Noch nicht verfügbar", label.Text);
                Assert.Equal(info.Sentence, w.Find<TextBlock>("studio-status-sentence").Text);
                Assert.Equal(st.Reason, w.Find<TextBlock>("studio-status-reason").Text);
                Assert.DoesNotContain(w.ActivePageRoot!.GetVisualDescendants().OfType<Button>(), b => b.Name?.StartsWith("studio-card-") == true);
            }

            RealClick(w, V<Button>(w, "win-min-" + win.Id));
            Assert.True(w.Windows.Get(win.Id)!.Minimized, id);
            RealClick(w, V<Button>(w, "task-" + win.Id));
            Assert.False(w.Windows.Get(win.Id)!.Minimized, id);
            Assert.Equal(win.Id, w.Windows.Active!.Id);
            RealClick(w, V<Button>(w, "win-close-" + win.Id));
            Assert.Null(w.Windows.Get(win.Id));
            Assert.Null(w.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.Name == "task-" + win.Id));
        }

        RealClick(w, V<Button>(w, "start-button"));
        Assert.True(w.StartMenuOpen);
        var letters = w.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Name?.StartsWith("sm-letter-") == true).Select(t => t.Text).ToList();
        Assert.Equal(letters.OrderBy(x => x, StringComparer.Create(new System.Globalization.CultureInfo("de-DE"), true)), letters);
        Assert.Equal(StudioCatalog.ById.Count, w.GetVisualDescendants().OfType<Button>().Count(b => b.Name?.StartsWith("sm-az-") == true));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Startmenü: Suche und Enter starten das Studio, nicht die Übersicht")]
    public async Task Suche_Enter()
    {
        var (w, _, _) = Ui.Open();
        Prepare(w, 1440, 900);
        w.Show("start"); Settle();
        RealClick(w, V<Button>(w, "start-button"));
        var box = V<TextBox>(w, "startmenu-search");
        box.Text = "Prüfungen"; Settle();
        Assert.NotNull(w.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.Name == "sm-hit-0"));
        box.Focus(); Settle();
        w.Key(PhysicalKey.Enter); Settle();
        await WaitOpen(w, "SPEC-06");
        Assert.NotEqual("studios", w.CurrentView);
        Assert.Equal("Lernen mit Karteikarten", w.Windows.Active!.Title);
        Assert.False(w.StartMenuOpen);

        w.Show("start"); Settle();
        RealClick(w, V<Button>(w, "start-button"));
        V<TextBox>(w, "startmenu-search").Text = "3d"; Settle();
        box = V<TextBox>(w, "startmenu-search"); box.Focus(); Settle();
        w.Key(PhysicalKey.Enter); Settle();
        Assert.NotEqual("studios", w.CurrentView);
        Assert.Equal("3D-Modelle erstellen", w.Windows.Active!.Title);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Drei Studios gleichzeitig bei 1366×768: Briefe, Spiele, 3D – Taskleiste, Snap, Eingabe bleibt")]
    public async Task Drei_gleichzeitig()
    {
        var (w, l, _) = Ui.Open();
        Prepare(w, 1366, 768);
        Assert.InRange(w.Bounds.Width, 1300, 1450);
        Assert.InRange(w.Bounds.Height, 720, 820);
        w.Show("start"); Settle();

        async Task OpenMenu(string id)
        {
            RealClick(w, V<Button>(w, "start-button"));
            RealClick(w, MenuButton(w, "sm-az-" + id));
            await WaitOpen(w, id);
        }

        await OpenMenu("STUDIO-LETTER-01");
        if (w.CurrentView == "studio-deps")
        {
            var later = w.GetVisualDescendants().OfType<Button>().First(b => b.Name?.StartsWith("btn-later-") == true);
            later.BringIntoView(); Settle(); RealClick(w, V<Button>(w, later.Name!));
            for (var i = 0; i < 20; i++) { Dispatcher.UIThread.RunJobs(); await Task.Delay(15); }
        }
        var subject = w.Find<TextBox>("letter-subject");
        subject.Text = "Betreff bleibt stehen";
        var letterId = w.Windows.Active!.Id;

        await OpenMenu("CORE-06");
        var gameId = w.Windows.Active!.Id;
        await OpenMenu("CORE-05");
        var d3 = w.Windows.Active!.Id;

        Assert.Equal(3, w.Windows.TaskOrder.Count);
        Assert.NotEqual(letterId, gameId); Assert.NotEqual(letterId, d3); Assert.NotEqual(gameId, d3);
        Assert.Equal("Briefe schreiben", w.Windows.Get(letterId)!.Title);
        Assert.Equal("Spiele bauen", w.Windows.Get(gameId)!.Title);
        Assert.Equal("3D-Modelle erstellen", w.Windows.Get(d3)!.Title);
        Assert.NotNull(V<Button>(w, "task-" + letterId));
        Assert.NotNull(V<Button>(w, "task-" + gameId));
        Assert.NotNull(V<Button>(w, "task-" + d3));
        Assert.Equal("Betreff bleibt stehen", w.Find<TextBox>("letter-subject").Text);
        Assert.DoesNotContain(l.Opened, o => o.Contains("Unreal", StringComparison.OrdinalIgnoreCase) || o.Contains("blender", StringComparison.OrdinalIgnoreCase));

        RealClick(w, V<Button>(w, "task-" + letterId));
        Assert.Equal(letterId, w.Windows.Active!.Id);
        Assert.Equal("Betreff bleibt stehen", w.Find<TextBox>("letter-subject").Text);

        RealClick(w, V<Button>(w, "task-" + d3));
        var win = w.Windows.Get(d3)!;
        var area = V<Canvas>(w, "window-area");
        var bar = V<Border>(w, "win-title-" + d3);
        var p = bar.TranslatePoint(new Point(80, 15), w)!.Value;
        var edge = area.TranslatePoint(new Point(2, Math.Min(200, area.Bounds.Height / 2)), w)!.Value;
        w.MouseDown(p, MouseButton.Left);
        w.MouseMove(p + (edge - p) / 2, RawInputModifiers.LeftMouseButton);
        w.MouseMove(edge, RawInputModifiers.LeftMouseButton);
        w.MouseUp(edge, MouseButton.Left); Settle();
        Assert.Equal(0, win.X, 1); Assert.Equal(w.Windows.AreaW / 2, win.W, 1);

        RealClick(w, V<Button>(w, "task-" + letterId));
        Assert.Equal("Betreff bleibt stehen", w.Find<TextBox>("letter-subject").Text);

        await OpenMenu("STUDIO-LETTER-01");
        Assert.Equal(3, w.Windows.TaskOrder.Count);
        Assert.Equal(letterId, w.Windows.Active!.Id);
        Assert.Equal("Betreff bleibt stehen", w.Find<TextBox>("letter-subject").Text);

        var before = w.Windows.Active!.Id;
        w.Key(PhysicalKey.Tab, RawInputModifiers.Control); Settle();
        Assert.NotEqual(before, w.Windows.Active!.Id);
        Assert.Equal(3, w.Windows.TaskOrder.Count);
        Assert.NotNull(w.Windows.Get(letterId));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Startmenü: Anheften bleibt nach erneutem Öffnen, angeheftetes Studio sitzt in der Taskleiste")]
    public void Anheften_bleibt()
    {
        var (w, _, root) = Ui.Open();
        Prepare(w, 1440, 900);
        w.Show("start"); Settle();
        RealClick(w, V<Button>(w, "start-button"));
        var toggle = MenuButton(w, "sm-pintoggle-CORE-05");
        RealClick(w, toggle);
        Assert.Contains("CORE-05", UserSettings.Load(root).PinnedStudios);
        w.Close();

        var core = new TrendoCore(root, new InMemorySecretStore(), new FakeLauncher());
        var w2 = new MainWindow(core, new AppOptions(root, null, 0, Isolated: true), new Probe("browser"));
        w2.Show(); Settle();
        Prepare(w2, 1440, 900);
        w2.Show("start"); Settle();
        RealClick(w2, V<Button>(w2, "start-button"));
        Assert.NotNull(w2.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.Name == "sm-pin-CORE-05"));
        Assert.NotNull(w2.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.Name == "pinbar-CORE-05" && b.IsEffectivelyVisible));
        w2.Close();
    }

    [AvaloniaFact(DisplayName = "Viele Studiofenster: das erste bleibt, Hinweis statt stillem Schließen")]
    public void Viele_Fenster_bleiben()
    {
        var (w, _, _) = Ui.Open();
        w.Show("start"); Settle();
        string? first = null;
        foreach (var id in StudioCatalog.ById.Keys.Take(26))
        {
            w.Show(w.StudioLaunchPage(id));
            first ??= w.Windows.Active!.Id;
        }
        Assert.True(w.Windows.TaskOrder.Count >= 24, "nur " + w.Windows.TaskOrder.Count);
        Assert.NotNull(w.Windows.Get(first!));
        Assert.Equal(WindowManager.ManyWindowsNotice, w.Windows.Notice);
        Assert.Equal(WindowManager.ManyWindowsNotice, w.Find<TextBlock>("windows-notice").Text);
        w.Close();
    }
}
