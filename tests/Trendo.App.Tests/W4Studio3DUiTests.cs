using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

public class W4_Core05_Ui
{
    static string Msg(MainWindow w) => ((TextBlock)w.Find<Border>("w45-msg").Child!).Text ?? "";

    static async Task Until(Func<bool> done)
    {
        for (var i = 0; i < 50 && !done(); i++) { await Task.Delay(30); Dispatcher.UIThread.RunJobs(); }
    }

    [AvaloniaFact(DisplayName = "3D: Teil hinzufügen, ändern, Vorschau, löschen, rückgängig")]
    public void Build_part_preview_undo()
    {
        var (w, _, _) = Ui.Open();
        w.Show("3d");
        Assert.NotNull(w.TryFind<TextBlock>("3d-ai-reason"));
        Assert.False(w.Find<Button>("3d-ai").IsEnabled);
        w.Find<Button>("3d-add").Press();
        Assert.Contains("ist dabei", Msg(w));
        Assert.NotNull(w.TryFind<Image>("3d-preview-image"));
        Assert.Equal(new byte[] { 137, 80, 78, 71 }, w.CurrentPreview![..4]);
        w.Find<TextBox>("3d-sx").Text = "30";
        w.Find<Button>("3d-apply").Press();
        Assert.Contains("geändert", Msg(w));
        var front = Convert.ToHexString(w.CurrentPreview!);
        w.Find<Button>("3d-view-top").Press();
        Assert.Contains("oben", Msg(w));
        Assert.NotEqual(front, Convert.ToHexString(w.CurrentPreview!));
        w.Find<Button>("3d-hole").Press();
        Assert.Contains("Loch", Msg(w));
        w.Find<Button>("3d-bevel").Press();
        Assert.Contains("gerundet", Msg(w));
        w.Find<Button>("3d-del-0").Press();
        Assert.Contains("entfernt", Msg(w));
        w.Find<Button>("3d-undo").Press();
        Assert.Contains("Rückgängig", Msg(w));
        Assert.Contains("Teil", Msg(w) + string.Join(" ", w.GetLogicalDescendants().OfType<Button>().Select(b => b.Content as string)));
        w.Find<Button>("3d-smooth").Press();
        Assert.Contains("geglättet", Msg(w));
        w.Find<Button>("3d-mirror").Press();
        Assert.Contains("gespiegelt", Msg(w));
        w.Find<Button>("3d-shell").Press();
        Assert.Contains("ausgehöhlt", Msg(w));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "3D: OBJ, STL und GLB speichern, Datei öffnen, Projekt wieder öffnen")]
    public async Task Export_import_and_project()
    {
        var (w, l, root) = Ui.Open();
        w.Show("3d");
        w.Find<TextBox>("3d-name").Text = "Tisch";
        w.Find<Button>("3d-add").Press();
        w.Find<Button>("3d-export-obj").Press();
        Assert.Contains(".obj", Msg(w));
        var obj = Directory.GetFiles(w.Folder3D, "*.obj").Single();
        Assert.Contains("v ", File.ReadAllText(obj));
        w.Find<Button>("3d-export-stl").Press();
        Assert.True(Directory.GetFiles(w.Folder3D, "*.stl").Length >= 1);
        w.Find<Button>("3d-export-glb").Press();
        Assert.True(Directory.GetFiles(w.Folder3D, "*.glb").Length >= 1);
        w.Find<Button>("3d-folder").Press();
        Assert.Contains(l.Opened, p => p.Contains(w.Folder3D));
        var extra = Path.Combine(root, "kugel.obj");
        File.WriteAllText(extra, "v 0 0 0\nv 2 0 0\nv 0 2 0\nf 1 2 3\n");
        w.Pick3DFile = () => Task.FromResult<string?>(extra);
        w.Find<Button>("3d-open-file").Press();
        await Until(() => Msg(w).Contains("Eingelesen"));
        Assert.Contains("Eingelesen", Msg(w));
        w.Find<Button>("3d-save").Press();
        Assert.Contains("Gespeichert im Projekt", Msg(w));
        w.Show("projects");
        w.Find<Button>("btn-open-3d-tisch").Press();
        Assert.Equal("3d", w.CurrentView);
        Assert.Contains("geöffnet", Msg(w));
        Assert.Contains(w.GetLogicalDescendants().OfType<Button>(), b => (b.Content as string)?.Contains("Würfel") == true);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "3D: Satz wird zum Vorschlag und erst nach Übernehmen gebaut")]
    public async Task Ai_proposes_parts()
    {
        var (w, _, _) = Ui.Open();
        w.SceneAi = _ => Task.FromResult("{\"parts\":[{\"name\":\"Platte\",\"shape\":\"cube\",\"sx\":120,\"sy\":80,\"sz\":4,\"z\":40,\"color\":\"#8B5A2B\"},{\"name\":\"Bein\",\"shape\":\"cylinder\",\"sx\":6,\"sy\":6,\"sz\":40}]}");
        w.Show("3d");
        Assert.True(w.Find<Button>("3d-ai").IsEnabled);
        w.Find<TextBox>("3d-ai-text").Text = "ein Tisch 120 mal 80";
        w.Find<Button>("3d-ai").Press();
        await Until(() => Msg(w).Contains("So würde ich"));
        Assert.Contains("Platte", string.Join("\n", w.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text)));
        w.Find<Button>("3d-ai-use").Press();
        Assert.Contains("übernommen", Msg(w));
        Assert.Contains(w.GetLogicalDescendants().OfType<Button>(), b => (b.Content as string)?.Contains("Platte") == true);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "3D: ohne Blender einmal fragen, mit Blender das aktuelle Objekt öffnen")]
    public async Task Blender_asks_then_opens_current()
    {
        var (w, l, _) = Ui.Open();
        w.Show("3d");
        w.Find<Button>("3d-setup-blender").Press();
        await Until(() => w.CurrentView == "studio-deps");
        Assert.Equal("studio-deps", w.CurrentView);
        Assert.Equal("3D-Modelle erstellen braucht Blender. Jetzt installieren?", w.Find<TextBlock>("ask-blender").Text);
        Assert.Empty(l.Opened);
        w.Find<Button>("btn-later-blender").Press();
        Assert.Equal("3d", w.CurrentView);
        Assert.Contains(w.Core.Audit.ReadAll(), e => e.Action == "deps.declined");

        var (w2, l2, root) = Ui.Open();
        var script = Path.Combine(root, "blender");
        await File.WriteAllTextAsync(script, "#!/bin/sh\nlast=\nfor a in \"$@\"; do\n  last=\"$a\"\ndone\nif [ -n \"$last\" ]; then printf BLENDER > \"$last\"; fi\nexit 0\n");
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(script, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        await File.WriteAllTextAsync(Path.Combine(root, "tools.json"), JsonSerializer.Serialize(new Dictionary<string, string> { ["blender"] = script }));
        w2.Show("3d");
        w2.Find<TextBox>("3d-name").Text = "Stuhl";
        w2.Find<Button>("3d-add").Press();
        w2.Find<Button>("3d-open-blender").Press();
        await Until(() => l2.Opened.Count > 0);
        Assert.Contains(l2.Opened, p => p.Contains("Stuhl") && p.Contains(".blend"));
        Assert.Contains("öffnet", Msg(w2));
        w2.Close();
        w.Close();
    }
}
