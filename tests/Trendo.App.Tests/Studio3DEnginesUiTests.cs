using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Paket 01 / S2-17: im 3D-Studio „Für Engine“ – Unity, Unreal, Godot, Web; Bericht aus dem Rundlauf neben der Datei.</summary>
public class Studio3DEngines_Ui
{
    static string Msg(MainWindow w) => ((TextBlock)w.Find<Border>("w45-msg").Child!).Text ?? "";
    static void Pump() { for (var i = 0; i < 5; i++) Dispatcher.UIThread.RunJobs(); }

    [AvaloniaFact(DisplayName = "3D Für Engine: ohne Teil Hinweis; vier Ziele; Unity-Export mit Bericht-Datei; Verlust (Material) wird als Fehler gemeldet")]
    public void Engine_export()
    {
        var (w, _, _) = Ui.Open();
        w.Show("3d"); Pump();
        foreach (var id in new[] { "unity", "unreal", "godot", "web" }) Assert.NotNull(w.TryFind<Button>("3d-engine-" + id));
        var calls = new List<string>();
        SceneStats S(int mats) => new(1, 8, 12, mats, 1, 0, 0, 0, 0.5, 0.5, 0.5);
        w.EngineExportOverride = (src, target, dir) =>
        {
            calls.Add(target); Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, "Tisch-" + target + Studio3DEngines.Profile(target).Ext); File.WriteAllText(file, "x");
            var after = target == "web" ? S(0) : S(1);
            return new(target, file, S(1), after, Studio3DEngines.Compare(S(1), after));
        };
        w.Find<Button>("3d-engine-unity").Press(); Pump();
        Assert.Empty(calls); Assert.Contains("Noch kein Teil", Msg(w));
        w.Find<TextBox>("3d-name").Text = "Tisch";
        w.Find<Button>("3d-add").Press(); Pump();
        w.Find<Button>("3d-engine-unity").Press(); Pump();
        Assert.Equal(["unity"], calls);
        Assert.True(w.LastEngineExport!.Ok); Assert.StartsWith("Unity", Msg(w));
        Assert.True(File.Exists(Path.Combine(w.Folder3D, "Engine-Export", "Tisch-unity-bericht.txt")));
        w.Find<Button>("3d-engine-web").Press(); Pump();
        Assert.False(w.LastEngineExport!.Ok); Assert.Contains("Materialien verloren", Msg(w));
        w.Close();
    }
}
