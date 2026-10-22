using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

public class W4_Core05_Ui
{
    static string Msg(MainWindow w) => ((TextBlock)w.Find<Border>("w45-msg").Child!).Text ?? "";

    static void Shot(MainWindow w, string name)
    {
        w.WindowState = WindowState.Normal;
        w.Width = 1600;
        w.Height = 1200;
        Dispatcher.UIThread.RunJobs();
        var max = w.GetLogicalDescendants().OfType<Button>().FirstOrDefault(b => b.Name?.StartsWith("win-max-") == true);
        max?.Press();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(3);
        var bmp = w.CaptureRenderedFrame() ?? w.GetLastRenderedFrame();
        if (bmp is null) throw new InvalidOperationException($"kein Bild. Bounds={w.Bounds} Client={w.ClientSize} State={w.WindowState}");
        var dir = Path.Combine(Path.GetTempPath(), "trendo-3d-shots");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, name);
        bmp.Save(path, new PngBitmapEncoderOptions());
        var keep = "/opt/cursor/artifacts/screenshots";
        if (Directory.Exists("/opt/cursor/artifacts"))
        {
            Directory.CreateDirectory(keep);
            File.Copy(path, Path.Combine(keep, name), true);
        }
        var bytes = File.ReadAllBytes(path);
        Assert.Equal(new byte[] { 137, 80, 78, 71 }, bytes[..4]);
        Assert.True(bytes.Length > 20_000, "Bild ist leer: " + bytes.Length);
    }

    [AvaloniaFact(DisplayName = "CORE-05: Studio öffnet ohne Blender und zeigt den Namen aus dem Katalog")]
    public async Task Studio_oeffnet()
    {
        var (w, _, _) = Ui.Open();
        await w.OpenStudio("CORE-05", "3D-Modelle erstellen");
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("3d", w.CurrentView);
        Assert.Equal("3D-Modelle erstellen", w.Find<TextBlock>("page-title").Text);
        Assert.NotNull(w.TryFind<Button>("3d-add"));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "CORE-05: leere Szene – jeder Knopf sagt, was fehlt")]
    public void Leere_Szene()
    {
        var (w, l, _) = Ui.Open();
        w.Show("3d");
        Assert.False(w.Find<Button>("3d-ai").IsEnabled);
        Assert.Contains("nicht verbunden", w.Find<TextBlock>("3d-ai-reason").Text);
        w.Find<Button>("3d-export-obj").Press();
        Assert.Contains("kein Objekt", Msg(w));
        w.Find<Button>("3d-undo").Press();
        Assert.Contains("Nichts", Msg(w));
        w.Find<Button>("3d-tool-hole").Press();
        Assert.Contains("Wähle zuerst", Msg(w));
        w.Find<Button>("3d-open-blender").Press();
        Assert.Contains("kein Objekt", Msg(w));
        Assert.Null(w.TryFind<TextBlock>("3d-blender-ask"));
        w.Find<Button>("3d-project-save").Press();
        Assert.Contains("kein Objekt", Msg(w));
        w.Find<Button>("3d-refresh").Press();
        Assert.Contains("Noch kein Teil", Msg(w));
        w.Find<Button>("3d-folder").Press();
        Assert.Contains(w.Folder3D, l.Opened);
        Assert.Contains("Ordner geöffnet", Msg(w));
        w.Find<Button>("btn-help").Press();
        Assert.NotNull(w.TryFind<Border>("hint"));
        w.Find<Button>("hint-ok").Press();
        w.Close();
    }

    [AvaloniaFact(DisplayName = "CORE-05: Form bauen, ändern, Werkzeuge, Vorschau, Export, Projekt")]
    public void Bauen_und_speichern()
    {
        var (w, l, root) = Ui.Open();
        w.Show("kreativ-medien");
        w.Find<Button>("kreativ-tab-3d").Press();
        Assert.Equal("3d", w.CurrentView);
        w.Find<TextBox>("3d-name").Text = "Tisch";
        w.Find<Button>("3d-add").Press();
        Assert.Contains("hinzugefügt", Msg(w));
        Assert.NotNull(w.TryFind<Image>("3d-preview-image"));
        w.Find<TextBox>("3d-part-name").Text = "Platte";
        w.Find<TextBox>("3d-sx").Text = "16";
        w.Find<TextBox>("3d-sy").Text = "8";
        w.Find<TextBox>("3d-sz").Text = "4";
        w.Find<TextBox>("3d-px").Text = "18";
        w.Find<TextBox>("3d-color").Text = "#C4A574";
        w.Find<Button>("3d-apply").Press();
        Assert.Contains("übernommen", Msg(w));
        Assert.Contains("Platte", w.Find<Button>("3d-part-0").Content?.ToString());
        Assert.Equal("16", w.Find<TextBox>("3d-sx").Text);

        w.Find<ComboBox>("3d-shape").SelectedIndex = 2;
        w.Find<Button>("3d-add").Press();
        Assert.Contains("Zylinder", w.Find<Button>("3d-part-1").Content?.ToString());
        w.Find<Button>("3d-part-0").Press();
        Assert.Contains("ausgewählt", Msg(w));

        foreach (var (id, wort) in new[] { ("front", "Vorne"), ("back", "Hinten"), ("left", "Links"), ("right", "Rechts"), ("top", "Oben"), ("persp", "Schräg") })
        {
            w.Find<Button>("3d-view-" + id).Press();
            Assert.Contains(wort, Msg(w));
            Assert.NotNull(w.TryFind<Image>("3d-preview-image"));
        }
        w.Find<Button>("3d-refresh").Press();
        Assert.Contains("aktualisiert", Msg(w));
        Shot(w, "3d-studio.png");

        w.Find<Button>("3d-tool-hole").Press();
        Assert.Contains("Loch", Msg(w));
        w.Find<Button>("3d-undo").Press();
        Assert.Contains("rückgängig", Msg(w));
        w.Find<Button>("3d-tool-bevel").Press();
        Assert.Contains("abgerundet", Msg(w));
        w.Find<Button>("3d-tool-smooth").Press();
        Assert.Contains("Geglättet", Msg(w));
        w.Find<Button>("3d-tool-mirror").Press();
        Assert.Contains("Gespiegelt", Msg(w));
        Assert.NotNull(w.TryFind<Button>("3d-part-2"));

        w.Find<ComboBox>("3d-shape").SelectedIndex = 5;
        w.Find<Button>("3d-add").Press();
        w.Find<Button>("3d-tool-solid").Press();
        Assert.Contains("Wandstärke", Msg(w));
        w.Find<ComboBox>("3d-shape").SelectedIndex = 6;
        w.Find<Button>("3d-add").Press();
        Assert.True(w.Find<TextBox>("3d-text").IsVisible);
        w.Find<TextBox>("3d-text").Text = "Hallo";
        w.Find<Button>("3d-apply").Press();
        Assert.Contains("übernommen", Msg(w));

        w.Find<Button>("3d-del-1").Press();
        Assert.Contains("gelöscht", Msg(w));

        w.Find<Button>("3d-export-obj").Press();
        Assert.Contains("Gespeichert", Msg(w));
        Assert.True(File.Exists(Path.Combine(w.Folder3D, "Tisch.obj")));
        w.Find<Button>("3d-export-stl").Press();
        var stl = Path.Combine(w.Folder3D, "Tisch.stl");
        Assert.True(new FileInfo(stl).Length > 84);
        w.Find<Button>("3d-export-glb").Press();
        var glb = File.ReadAllBytes(Path.Combine(w.Folder3D, "Tisch.glb"));
        Assert.Equal("glTF"u8.ToArray(), glb[..4]);
        w.Find<Button>("3d-export-fbx").Press();
        Assert.Contains("FBX", File.ReadAllText(Path.Combine(w.Folder3D, "Tisch.fbx")));
        Assert.Contains("Tisch.fbx", w.Find<TextBlock>("3d-marked").Text);
        w.Find<Button>("3d-folder").Press();
        Assert.Contains(w.Folder3D, l.Opened);
        Assert.Contains("Tisch.fbx", Msg(w));

        w.Find<Button>("3d-export-blend").Press();
        Assert.Contains("Blender", Msg(w));
        Assert.NotNull(w.TryFind<TextBlock>("3d-blender-ask"));
        Shot(w, "3d-blender-frage.png");
        w.Find<Button>("3d-blender-no").Press();
        Assert.Contains("ungefragt", Msg(w));
        Assert.Null(w.TryFind<TextBlock>("3d-blender-ask"));
        w.Find<Button>("3d-blender-setup").Press();
        Assert.NotNull(w.TryFind<TextBlock>("3d-blender-ask"));
        w.Find<Button>("3d-blender-no").Press();
        w.Find<Button>("3d-open-blender").Press();
        Assert.Contains("dieses Objekt", Msg(w));
        w.Find<Button>("3d-blender-no").Press();

        w.Find<TextBox>("3d-project-id").Text = "tisch";
        w.Find<Button>("3d-project-save").Press();
        Assert.Contains("gespeichert", Msg(w));
        Assert.True(File.Exists(Path.Combine(root, "projects", "tisch", "files", "3d", "szene.json")));
        w.Find<Button>("3d-project-load").Press();
        Assert.Contains("geöffnet", Msg(w));
        w.Show("projects");
        w.Find<Button>("btn-open-3d-tisch").Press();
        Assert.Equal("3d", w.CurrentView);
        Assert.Contains("Platte", w.Find<Button>("3d-part-0").Content?.ToString());
        w.Close();
    }

    [AvaloniaFact(DisplayName = "CORE-05: Datei öffnen lädt OBJ, STL und GLB in die Teileliste")]
    public void Datei_oeffnen()
    {
        var (w, _, root) = Ui.Open();
        var dir = Path.Combine(root, "eingang");
        Directory.CreateDirectory(dir);
        var szene = new Scene3D();
        szene.Hinzu("kugel");
        var obj = Path.Combine(dir, "kugel.obj");
        var stl = Path.Combine(dir, "kugel.stl");
        var glb = Path.Combine(dir, "kugel.glb");
        var mesh = szene.Welt();
        MeshIo.WriteObj(obj, mesh);
        MeshIo.WriteStl(stl, mesh);
        MeshIo.WriteGlb(glb, mesh);
        w.Pick3DFiles = () => Task.FromResult<IReadOnlyList<string>>([obj, stl, glb]);
        w.Show("3d");
        w.Find<ComboBox>("3d-unit").SelectedIndex = 1;
        w.Find<Button>("3d-open").Press();
        Assert.Contains("geladen", Msg(w));
        Assert.NotNull(w.TryFind<Button>("3d-part-2"));
        Assert.NotNull(w.TryFind<Image>("3d-preview-image"));
        Assert.NotNull(w.TryFind<Border>("3d-drop"));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "CORE-05: KI-Vorschlag wird gezeigt, geändert und erst dann gebaut")]
    public void Ki_vorschlag()
    {
        var json = """
        {"teile":[
          {"form":"wuerfel","name":"Platte","bx":120,"by":80,"bz":4,"x":0,"y":0,"z":40,"farbe":"#C4A574"},
          {"form":"zylinder","name":"Bein","bx":5,"by":5,"bz":38,"x":52,"y":32,"z":19,"farbe":"#C4A574"},
          {"form":"zylinder","name":"Bein","bx":5,"by":5,"bz":38,"x":-52,"y":32,"z":19,"farbe":"#C4A574"},
          {"form":"zylinder","name":"Bein","bx":5,"by":5,"bz":38,"x":52,"y":-32,"z":19,"farbe":"#C4A574"},
          {"form":"zylinder","name":"Bein","bx":5,"by":5,"bz":38,"x":-52,"y":-32,"z":19,"farbe":"#C4A574"}
        ]}
        """;
        var (w, _, _) = Ui.Open();
        w.Ki3DForceReady = true;
        w.Ki3DOverride = auftrag =>
        {
            Assert.Contains("Zentimeter", auftrag);
            return Task.FromResult(json);
        };
        w.Show("3d");
        Assert.True(w.Find<Button>("3d-ai").IsEnabled);
        w.Find<TextBox>("3d-prompt").Text = "ein Tisch 120 × 80 cm mit vier Beinen";
        w.Find<Button>("3d-ai").Press();
        Assert.Contains("5 Teile", Msg(w));
        Assert.NotNull(w.TryFind<TextBlock>("3d-proposal"));
        w.Find<TextBox>("3d-prop-bx-0").Text = "100";
        w.Find<Button>("3d-ai-apply").Press();
        Assert.Contains("gebaut", Msg(w));
        Assert.Contains("5 Teile", w.Find<TextBlock>("3d-info").Text);
        Assert.Equal("100", w.Find<TextBox>("3d-sx").Text);
        Assert.Null(w.TryFind<TextBlock>("3d-proposal"));
        w.Find<Button>("3d-undo").Press();
        Assert.NotNull(w.TryFind<TextBlock>("3d-empty"));

        w.Ki3DOverride = _ => Task.FromResult(json);
        w.Find<TextBox>("3d-prompt").Text = "noch ein Tisch";
        w.Find<Button>("3d-ai").Press();
        w.Find<Button>("3d-ai-drop").Press();
        Assert.Contains("verworfen", Msg(w));
        Assert.Null(w.TryFind<TextBlock>("3d-proposal"));
        w.Find<Button>("3d-ai-check").Press();
        Assert.Contains("ist verbunden", Msg(w));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "CORE-05: offline bleibt die Installation aus, der Bau bleibt an")]
    public void Offline_kein_Install()
    {
        var (w, _, _) = Ui.Open(offline: true);
        w.Show("3d");
        Assert.Contains("Offline", w.Find<TextBlock>("3d-ai-reason").Text);
        Assert.False(w.Find<Button>("3d-ai").IsEnabled);
        w.Find<Button>("3d-add").Press();
        w.Find<Button>("3d-open-blender").Press();
        Assert.False(w.Find<Button>("3d-blender-yes").IsEnabled);
        w.Find<Button>("3d-blender-no").Press();
        Assert.Contains("ungefragt", Msg(w));
        Assert.NotNull(w.TryFind<Image>("3d-preview-image"));
        w.Close();
    }
}
