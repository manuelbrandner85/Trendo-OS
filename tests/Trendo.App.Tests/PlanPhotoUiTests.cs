using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Trendo.Core.Arch;
using Trendo.Core.Bibliothek;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0280: Grundriss mit echten Möbelmodellen, Modelle aus der 3D-Bibliothek stellen, Foto-Ansicht (Blender durch Ersatz nachgestellt).</summary>
public class PlanPhoto_Ui
{
    const string AK = "architektur";
    static T Get<T>(MainWindow w, string name) where T : Control { Settle(); return w.GetVisualDescendants().OfType<T>().Last(c => c.Name == name && c.IsEffectivelyVisible && c.Bounds.Width > 0); }
    static T? Try<T>(MainWindow w, string name) where T : Control { Settle(); return w.GetVisualDescendants().OfType<T>().LastOrDefault(c => c.Name == name && c.IsEffectivelyVisible && c.Bounds.Width > 0); }
    static void Press(MainWindow w, string name) { var b = Get<Button>(w, name); b.BringIntoView(); Settle(); RealClick(w, Get<Button>(w, name)); }
    static async Task Wait(Func<bool> done) { for (var i = 0; i < 300 && !done(); i++) { await Task.Delay(10); Settle(); } }
    static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");

    [AvaloniaFact(DisplayName = "T-0280 UI: 3D-Ansicht zeigt echte Möbelmodelle statt Kisten")]
    public void View3D_uses_models()
    {
        var (w, _, _) = Ui.Open();
        w.Show(MainWindow.StudioPages["SPEC-01"]); Settle(); RealClick(w, Get<Button>(w, "prog-tpl-wohnung"));
        Press(w, "prog-cmd-arch-2d3d");
        var cv = Get<CadView>(w, "arch-3d");
        var sofaId = FloorPlan.From(w.ProgramActive(AK)!.Data).Items.First(i => i.Type == "sofa").Id;
        Assert.Contains(cv.Parts, p => p.Solid.Smooth); // Modellteile
        Assert.True(cv.Parts.Count(p => p.Solid.Smooth) > 10);
    }

    [AvaloniaFact(DisplayName = "T-0280 UI: Modell aus der 3D-Bibliothek im Möbelkatalog wählen und stellen – Maße aus dem Modell, in 3D als Modell")]
    public void Library_model_in_plan()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        var glb = Trendo.Core.XrSample.Write(Path.Combine(root, "lesesessel.glb"));
        new AssetLibrary(new HttpFetch(new HttpClient()), Path.Combine(root, "bibliothek3d")).AddLocal(glb, "lesesessel.glb", "lesesessel.glb");
        w.Show(MainWindow.StudioPages["SPEC-01"]); Settle(); RealClick(w, Get<Button>(w, "prog-tpl-leer"));
        Press(w, "arch-tool-moebel");
        Press(w, "arch-bib-0");
        Assert.Equal("bib:Eigene Datei/lesesessel", w.ArchState.ItemType);
        var ft = FurnitureCatalog.Get(w.ArchState.ItemType)!; Assert.Equal("lesesessel", ft.Name); Assert.InRange(ft.W, 20, 200);
        w.ArchEdit("Möbel stellen", pl => pl.AddItem(w.ArchState.ItemType, 300, 300));
        Press(w, "prog-cmd-arch-2d3d");
        Assert.True(Get<CadView>(w, "arch-3d").Parts.Count(p => p.Solid.Smooth) > 0);
    }

    [AvaloniaFact(DisplayName = "T-0280 UI: Foto-Ansicht – Raumansicht mit gewähltem Raum, Fortschritt, Abbrechen, Bild zeigen und speichern")]
    public async Task Photo_flow()
    {
        var (w, _, _) = Ui.Open();
        System.Text.Json.Nodes.JsonObject? seen = null; var gate = new TaskCompletionSource();
        w.ArchPhotoRenderer = async (scene, file, progress, ct) =>
        {
            seen = scene; progress.Report(0.5);
            await gate.Task.WaitAsync(ct);
            Directory.CreateDirectory(Path.GetDirectoryName(file)!); await File.WriteAllBytesAsync(file, Png, ct); return file;
        };
        w.Show(MainWindow.StudioPages["SPEC-01"]); Settle(); RealClick(w, Get<Button>(w, "prog-tpl-wohnung"));
        Press(w, "prog-cmd-arch-photo");
        Assert.NotNull(Try<TextBlock>(w, "arch-photo-empty"));
        Press(w, "arch-photo-raum");
        var plan = FloorPlan.From(w.ProgramActive(AK)!.Data);
        Get<ComboBox>(w, "arch-photo-room").SelectedIndex = plan.Rooms.FindIndex(r => r.Name == "Küche");
        // 1. Lauf abbrechen
        Press(w, "arch-photo-go");
        await Wait(() => Try<ProgressBar>(w, "arch-photo-progress")?.Value >= 50);
        Assert.Equal("raum", (string)seen!["mode"]!); Assert.Equal(plan.Rooms.First(r => r.Name == "Küche").Id, (string)seen["room"]!);
        Assert.Equal(960, (int)seen["width"]!); // Vorschau
        Press(w, "arch-photo-cancel");
        await Wait(() => !w.ArchPhotoBusy);
        Assert.StartsWith("⚠", Get<TextBlock>(w, "arch-photo-msg").Text); Assert.Null(w.ArchPhotoFile);
        // 2. fertige Qualität durchlaufen lassen
        Press(w, "arch-photo-q-final"); Press(w, "arch-photo-go");
        await Wait(() => w.ArchPhotoBusy); gate.SetResult();
        await Wait(() => Try<Image>(w, "arch-photo-image") is not null && Try<TextBlock>(w, "arch-photo-msg")?.Text?.StartsWith('✓') == true);
        Assert.Equal(1600, (int)seen["width"]!); Assert.Equal(256, (int)seen["samples"]!);
        Assert.StartsWith("✓", Get<TextBlock>(w, "arch-photo-msg").Text);
        Press(w, "arch-photo-save");
        Assert.Contains("Gespeichert", Get<TextBlock>(w, "arch-photo-msg").Text);
        Press(w, "arch-photo-back"); Assert.NotNull(Try<FloorPlanView>(w, "arch-canvas"));
    }

    [AvaloniaFact(DisplayName = "T-0280 UI: Ohne Blender klare Meldung statt Absturz")]
    public async Task Photo_without_blender()
    {
        var old = Environment.GetEnvironmentVariable("TRENDO_BLENDER");
        var (w, _, _) = Ui.Open();
        w.ArchPhotoRenderer = (_, _, _, _) => throw new InvalidOperationException("Blender wurde nicht gefunden – die Foto-Ansicht braucht Blender (unter Programme einrichten).");
        w.Show(MainWindow.StudioPages["SPEC-01"]); Settle(); RealClick(w, Get<Button>(w, "prog-tpl-wohnung"));
        Press(w, "prog-cmd-arch-photo"); Press(w, "arch-photo-go");
        await Wait(() => !w.ArchPhotoBusy && Try<TextBlock>(w, "arch-photo-msg")?.Text?.StartsWith('⚠') == true);
        Assert.Contains("Blender", Get<TextBlock>(w, "arch-photo-msg").Text);
        Assert.Equal(old, Environment.GetEnvironmentVariable("TRENDO_BLENDER"));
    }
}
