using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

public class Programme_Ui
{
    [AvaloniaFact(DisplayName = "T-0203: Blender, Unreal, VS Code, LibreOffice, OBS und mpv öffnen die aktuelle Datei")]
    public void Open_passes_program_and_file()
    {
        var (w, l, root) = Ui.Open();
        w.EngineFinder = () => [];

        var blender = Fake(root, "blender");
        var modell = Path.Combine(w.Folder3D, "kiste.blend");
        File.WriteAllText(modell, "x");
        w.Deps.Integrate("blender", blender);
        w.Show("3d");
        w.Find<Button>("prog-open-blender").Press();
        Assert.Contains(l.With, x => x.Exe == blender && x.Arg == modell);

        var unreal = Fake(root, "UnrealEditor");
        var spiel = Path.Combine(w.GameFolder, "Demo");
        Directory.CreateDirectory(spiel);
        var up = Path.Combine(spiel, "Demo.uproject");
        File.WriteAllText(up, "{}");
        w.Deps.Integrate("unreal", unreal);
        w.Show("spiele");
        Assert.Equal("In Unreal öffnen", w.Find<Button>("prog-open-unreal").Content);
        w.Find<Button>("prog-open-unreal").Press();
        Assert.Contains(l.With, x => x.Exe == unreal && x.Arg == up);

        var code = Fake(root, "code");
        var projekt = Path.Combine(w.DevFolder, "Web");
        Directory.CreateDirectory(projekt);
        w.Deps.Integrate("vscode", code);
        w.Show("dev");
        Assert.Equal("In VS Code öffnen", w.Find<Button>("prog-open-vscode").Content);
        w.Find<Button>("prog-open-vscode").Press();
        Assert.Contains(l.With, x => x.Exe == code && x.Arg == projekt);

        var office = Fake(root, "soffice");
        var brief = Path.Combine(root, "Projekte", "brief.docx");
        Directory.CreateDirectory(Path.GetDirectoryName(brief)!);
        File.WriteAllText(brief, "x");
        w.Deps.Integrate("libreoffice", office);
        w.Show("office");
        Assert.Equal("In LibreOffice öffnen", w.Find<Button>("prog-open-libreoffice").Content);
        w.Find<Button>("prog-open-libreoffice").Press();
        Assert.Contains(l.With, x => x.Exe == office && x.Arg == brief);

        var obs = Fake(root, "obs64");
        w.Deps.Integrate("obs", obs);
        w.Show("sendung");
        Assert.Equal("In OBS öffnen", w.Find<Button>("prog-open-obs").Content);
        w.Find<Button>("prog-open-obs").Press();
        Assert.Contains(l.With, x => x.Exe == obs && x.Arg.Contains("sendung"));

        var mpv = Fake(root, "mpv");
        var clip = Path.Combine(root, "clip.mp4");
        File.WriteAllText(clip, "x");
        w.Deps.Integrate("mpv", mpv);
        w.MediaPick(clip);
        Assert.Equal("In mpv öffnen", w.Find<Button>("prog-open-mpv").Content);
        w.Find<Button>("prog-open-mpv").Press();
        Assert.Contains(l.With, x => x.Exe == mpv && x.Arg == clip);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "T-0203: Einrichten fragt einmal; vor Ja passiert nichts; danach ist Öffnen aktiv")]
    public async Task Setup_asks_once_then_open_works()
    {
        var (w, l, root) = Ui.Open();
        var runner = new MerkRunner();
        w.Runner = runner;
        w.Show("sendung");
        Assert.Equal("Einrichten", w.Find<Button>("prog-setup-obs").Content);
        Assert.Null(w.TryFind<Button>("prog-open-obs"));
        w.Find<Button>("prog-setup-obs").Press();
        Assert.NotNull(w.Find<Button>("prog-yes-obs"));
        Assert.Empty(runner.Calls);
        w.Find<Button>("prog-no-obs").Press();
        Assert.Empty(runner.Calls);
        Assert.Contains(w.Core.Audit.ReadAll(), e => e.Action == "deps.declined" && e.Detail == "obs");
        Assert.Equal("Einrichten", w.Find<Button>("prog-setup-obs").Content);

        var exe = Path.Combine(root, "obs64");
        runner.BeiWinget = () => { File.WriteAllText(exe, ""); w.Deps.Integrate("obs", exe); };
        w.Find<Button>("prog-setup-obs").Press();
        w.Find<Button>("prog-yes-obs").Press();
        for (var i = 0; i < 50 && w.TryFind<Button>("prog-open-obs") is null; i++)
        {
            await Task.Delay(20);
            Dispatcher.UIThread.RunJobs();
        }
        Assert.NotNull(w.TryFind<Button>("prog-open-obs"));
        Assert.Contains(runner.Calls, c => c.Contains("winget") && c.Contains("OBSProject.OBSStudio"));
        w.Find<Button>("prog-open-obs").Press();
        Assert.Contains(l.With, x => x.Exe == exe && (File.Exists(x.Arg) || Directory.Exists(x.Arg)));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "T-0203: Programme-Liste startet das erkannte Programm mit der aktuellen Datei")]
    public void Apps_list_starts_with_file()
    {
        var (w, l, root) = Ui.Open();
        var blender = Fake(root, "blender");
        w.Deps.Integrate("blender", blender);
        var modell = Path.Combine(w.Folder3D, "kiste.glb");
        File.WriteAllText(modell, "x");
        w.Show("apps");
        var knopf = w.Find<Button>("app-blender");
        Assert.Equal("Starten", knopf.Content);
        knopf.Press();
        Assert.Contains(l.With, x => x.Exe == blender && x.Arg == modell);
        w.Close();
    }

    private static string Fake(string root, string name)
    {
        var p = Path.Combine(root, name);
        File.WriteAllText(p, "");
        return p;
    }

    private sealed class MerkRunner : IProcessRunner
    {
        public List<string> Calls { get; } = [];
        public Action? BeiWinget { get; set; }
        public Task<(int Exit, string Output)> RunAsync(string file, string args, Action<string>? line = null, CancellationToken ct = default)
        {
            Calls.Add(file + " " + args);
            if (file == "winget") BeiWinget?.Invoke();
            return Task.FromResult((0, ""));
        }
    }
}
