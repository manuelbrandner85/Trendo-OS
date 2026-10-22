using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Trendo.Core;

namespace Trendo.App;

/// <summary>CORE-05: 3D-Studio über Blender.</summary>
public partial class MainWindow
{
    public string Folder3D => WorkFolder("3D");
    public string? BlenderPath => ToolPath("blender") is { } p && File.Exists(p) ? p : Cli.Find("blender");
    private string? _3dPreview;

    private void InitW4Studio3D() => RegisterTab(KreativTabs, "3d", "3D", "CORE-05", Studio3DView, "blender", "3d", "modell", "stl", "gltf", "rendern");

    public string? Run3D(Func<string, (Cli.Result R, string Out)> job, string what)
    {
        if (BlenderPath is not { } bl) { Say("Dafür fehlt Blender. Unter Zusatzprogramme kannst du es einrichten.", "3d"); return null; }
        var (r, o) = job(bl);
        _core.Audit.Append(Principal.LocalOwner.Name, "3d." + what, $"exit={r.Exit}");
        if (r.Ok && o.EndsWith(".png")) _3dPreview = o;
        Say(r.Ok ? $"Fertig: {Path.GetFileName(o)}" : "Blender meldet einen Fehler: " + Short(r.Err.Length > 0 ? r.Err : r.Out), "3d");
        return r.Ok ? o : null;
    }

    private Control Studio3DView()
    {
        var sp = new StackPanel { Spacing = 8, Children = { P("Trendo steuert Blender im Hintergrund. Du brauchst dafür kein Blender-Wissen.") } };
        sp.Children.Add(Card(ProgrammLeiste("blender", "Blender", AktuelleModellDatei)));

        var name = new TextBox { Name = "3d-name", PlaceholderText = "Name", Text = "Mein Modell", Width = 180 };
        var shape = new ComboBox { Name = "3d-shape", ItemsSource = Studio3D.Shapes.Select(s => s.Label).ToList(), SelectedIndex = 0, Width = 150 };
        var text = new TextBox { Name = "3d-text", PlaceholderText = "Text für 3D-Schrift", Text = "Trendo", Width = 160 };
        var fmt = new ComboBox { Name = "3d-format", ItemsSource = Studio3D.Formats.Select(f => f.Label).ToList(), SelectedIndex = 4, Width = 170 };
        sp.Children.Add(Card(new StackPanel { Spacing = 6, Children = { H("Neues Modell", 14), new WrapPanel { Children = { name, shape, text, fmt } },
            Act("3d-create", "Anlegen", () => Run3D(bl => Studio3D.Create(bl, Folder3D, Studio3D.Shapes[Math.Max(0, shape.SelectedIndex)].Id, name.Text ?? "Modell", Studio3D.Formats[Math.Max(0, fmt.SelectedIndex)].Ext, "#2F86F6", text.Text ?? "Trendo"), "create"), true) } }));

        sp.Children.Add(H("Deine Modelle", 14));
        var files = Directory.GetFiles(Folder3D).Where(f => Studio3D.Imports.Contains(Path.GetExtension(f).ToLowerInvariant())).OrderByDescending(File.GetLastWriteTime).Take(15).ToList();
        if (files.Count == 0) sp.Children.Add(P("Noch keine Modelle. Lege oben eins an oder kopiere Dateien in den 3D-Ordner."));
        var i = 0;
        foreach (var f in files)
        {
            var file = f; var conv = new ComboBox { Name = $"3d-conv-fmt-{i}", ItemsSource = Studio3D.Formats.Select(x => x.Label).ToList(), SelectedIndex = 0, Width = 150 };
            sp.Children.Add(Card(Row(P(Path.GetFileName(f), Text), new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = {
                Act($"3d-preview-{i}", "Vorschau", () => Run3D(bl => Studio3D.Preview(bl, file), "preview")),
                conv, Act($"3d-convert-{i}", "Umwandeln", () => Run3D(bl => Studio3D.Convert(bl, file, Studio3D.Formats[Math.Max(0, conv.SelectedIndex)].Ext), "convert")) } })));
            i++;
        }
        sp.Children.Add(Act("3d-folder", "3D-Ordner öffnen", () => Launch(Folder3D)));
        if (_3dPreview is { } pv && File.Exists(pv))
            try { sp.Children.Add(new Image { Name = "3d-preview-image", Source = new Bitmap(pv), MaxWidth = 480, HorizontalAlignment = HorizontalAlignment.Left }); } catch { }
        return KreativPage("3D-Studio", sp);
    }
}
