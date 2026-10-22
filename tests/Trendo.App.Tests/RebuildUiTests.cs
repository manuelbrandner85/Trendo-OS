using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Trendo.Core.Rebuild;
using Xunit;

namespace Trendo.App.Tests;

public class RebuildUiTests
{
    [AvaloniaFact(DisplayName = "Offline lässt die Recherche aus")]
    public void Offline_schaltet_aus()
    {
        var (w, _, _) = Ui.Open(offline: true);
        w.Show("nachbau");
        Dispatcher.UIThread.RunJobs();
        var web = w.Find<ToggleSwitch>("rebuild-web");
        Assert.False(web.IsEnabled);
        Assert.NotEqual(true, web.IsChecked);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Plan, Los, Fenster, Startmenü, größere Knöpfe, unsignierte Setup-Datei", Timeout = 60000)]
    public void Ablauf()
    {
        var (w, _, root) = Ui.Open();
        w.Width = 1440;
        w.Height = 900;
        var web = new CountingWeb();
        var launcher = new RecordingLauncher();
        w.RebuildOverride = new RebuildPorts { Web = web, Author = new NoAuthor(), Build = new OkBuild(), Packer = new FakePacker() };
        w.SetupLauncher = launcher;
        w.Show("nachbau");
        Dispatcher.UIThread.RunJobs();
        Assert.NotEqual(true, w.Find<ToggleSwitch>("rebuild-web").IsChecked);
        w.Find<TextBox>("rebuild-prompt").Text = "ein Notizprogramm wie Notepad++";
        w.Find<Button>("rebuild-plan").Press();
        Wait(w);
        Assert.Equal(0, web.Calls);
        Assert.Contains("Merkzettel", w.Find<TextBlock>("rebuild-reason").Text);
        Assert.Contains("Notepad", w.Find<TextBlock>("rebuild-reason").Text);
        Shot(w, "nachbau-plan.png");
        w.Find<CheckBox>("rebuild-feat-suchen").IsChecked = false;
        w.Find<Button>("rebuild-go").Press();
        Wait(w);
        Assert.DoesNotContain("suchen", w.Find<Border>("rebuild-preview").GetLogicalTextsSafe(), StringComparison.OrdinalIgnoreCase);
        w.Find<Button>("rebuild-open").Press();
        Dispatcher.UIThread.RunJobs();
        Assert.NotNull(w.TryFind<Button>("task-mein-"));
        Shot(w, "nachbau-fenster.png");
        Assert.Equal(36, w.Find<Button>("prog-action").MinHeight);
        w.OpenStartMenu();
        Dispatcher.UIThread.RunJobs();
        Assert.NotNull(w.TryFind<Button>("sm-mein-"));
        w.CloseStartMenu();
        w.Show("nachbau");
        var dest = Path.Combine(root, "setup");
        Directory.CreateDirectory(dest);
        w.Find<TextBox>("rebuild-setup-dir").Text = dest;
        w.Find<Button>("rebuild-setup").Press();
        Wait(w);
        Assert.Contains("nicht signiert", w.Find<TextBlock>("rebuild-unsigned").Text);
        Assert.True(File.Exists(Directory.GetFiles(dest, "*Setup.exe").First()));
        w.Find<Button>("rebuild-install").Press();
        Assert.Contains("Haken", string.Join("\n", w.GetLogicalTexts()));
        w.Find<CheckBox>("rebuild-install-ok").IsChecked = true;
        w.Find<Button>("rebuild-install").Press();
        Assert.Contains("Windows", string.Join("\n", w.GetLogicalTexts()));
        Assert.Empty(launcher.Started);
        w.Find<TextBox>("rebuild-change").Text = "mach die Knöpfe größer";
        w.Find<Button>("rebuild-revise").Press();
        Wait(w);
        Assert.Contains("48", w.Find<Border>("rebuild-preview").GetLogicalTextsSafe());
        w.Find<Button>("rebuild-open").Press();
        Assert.Equal(48, w.Find<Button>("prog-action").MinHeight);
        w.Show("nachbau");
        w.Find<Button>("rebuild-undo").Press();
        w.Find<Button>("rebuild-open").Press();
        Assert.Equal(36, w.Find<Button>("prog-action").MinHeight);
        w.Close();
    }

    static void Wait(MainWindow w)
    {
        var until = DateTime.UtcNow.AddSeconds(8);
        while (w.RebuildBusy && DateTime.UtcNow < until)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(20);
        }
        Dispatcher.UIThread.RunJobs();
        Assert.False(w.RebuildBusy);
    }

    static void Shot(MainWindow w, string name)
    {
        try
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            var frame = w.CaptureRenderedFrame();
            if (frame is null) return;
            Directory.CreateDirectory("/opt/cursor/artifacts");
            frame.Save(Path.Combine("/opt/cursor/artifacts", name));
        }
        catch (Exception) { }
    }

    sealed class CountingWeb : IPublicWeb
    {
        public int Calls;
        public bool Allowed => true;
        public IReadOnlyList<PublicPage> Research(string query) { Calls++; return []; }
    }

    sealed class OkBuild : IBuildRunner
    {
        public ToolResult Run(string exe, IReadOnlyList<string> args, string cwd, int timeoutMs = 180000)
        {
            if (args.Contains("build"))
            {
                var dll = Path.Combine(cwd, "Kern", "bin", "Release", "net10.0", "RebuildKern.dll");
                Directory.CreateDirectory(Path.GetDirectoryName(dll)!);
                File.WriteAllText(dll, "platzhalter");
            }
            return new ToolResult(0, "Passed! - 3");
        }
    }

    sealed class FakePacker : ISetupPacker
    {
        public SetupResult Pack(string root, AppBlueprint blueprint, string destDir, bool selfContained, int appVersion)
        {
            Directory.CreateDirectory(destDir);
            var path = Path.Combine(destDir, "Merkzettel-Setup.exe");
            File.WriteAllBytes(path, [0x4D, 0x5A, 0, 0]);
            File.WriteAllText(Path.Combine(destDir, "HINWEIS-UNSIGNIERT.txt"), "Diese Setup-Datei ist nicht signiert.\n");
            return new SetupResult(true, path, "vpk", false, "Die Setup-Datei ist nicht signiert. Windows fragt beim Installieren selbst nach. Trendo installiert sie nicht von allein.");
        }
    }

    sealed class RecordingLauncher : ISetupLauncher
    {
        public List<string> Started { get; } = [];
        public void Start(string path) => Started.Add(path);
    }
}

static class TextExt
{
    public static string GetLogicalTextsSafe(this Control c) =>
        string.Join("\n", c.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text));
}
