using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Xunit;
namespace Trendo.App.Tests;

public class W4_Core05_Ui
{
    [AvaloniaFact(DisplayName = "CORE-05 (UI): Modell anlegen und Vorschau anzeigen (echtes Blender)")]
    public void Create_and_preview()
    {
        var (w, _, _) = Ui.Open();
        w.Show("3d");
        if (w.BlenderPath is null) { Assert.NotNull(w.TryFind<Button>("kreativ-tab-3d")); w.Close(); return; }
        w.Find<Button>("3d-create").Press();
        Assert.True(File.Exists(Path.Combine(w.Folder3D, "Mein-Modell.blend")), ((TextBlock)w.Find<Border>("w45-msg").Child!).Text + " / " + w.Folder3D);
        w.Find<Button>("3d-preview-0").Press();
        Assert.NotNull(w.TryFind<Image>("3d-preview-image"));
        w.Close();
    }
}
