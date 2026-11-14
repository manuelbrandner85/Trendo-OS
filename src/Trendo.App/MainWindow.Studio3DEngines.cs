using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Paket 01 / S2-17 „Blender–Unreal–Web Fusion“ (Unity zusätzlich): Export für Unity, Unreal, Godot und Web über Blender
/// mit Rundlauf-Prüfung (Export → Wieder-Import → Vergleich) und Bericht neben der Datei.</summary>
public partial class MainWindow
{
    /// <summary>Für Tests: statt Blender (Quelle, Ziel, Ordner) → Bericht.</summary>
    public Func<string, string, string, EngineExportReport>? EngineExportOverride { get; set; }
    public EngineExportReport? LastEngineExport { get; private set; }

    public async Task ExportForEngine(TextBox objName, string target)
    {
        Keep(objName);
        if (_scene.Parts.Count == 0) { Say("Noch kein Teil. Füge zuerst eines hinzu.", "3d"); return; }
        var profile = Studio3DEngines.Profile(target);
        string? blender = null;
        if (EngineExportOverride is null) { blender = await ReadyBlender(); if (blender is null) return; }
        var outDir = Path.Combine(Folder3D, "Engine-Export");
        var name = DevStudio.SafeName(_scene.Name);
        Say($"Export für {profile.Label} läuft – Blender prüft danach die Datei …", "3d");
        try
        {
            EngineExportReport rep;
            if (EngineExportOverride is { } o) rep = o(Path.Combine(Folder3D, name + ".blend"), target, outDir);
            else
            {
                var spec = _scene.WriteBlenderSpec(Path.Combine(Folder3D, "blender"));
                var blend = Path.Combine(outDir, name + ".blend"); Directory.CreateDirectory(outDir);
                var (r, _) = await Task.Run(() => Studio3D.ExportBlend(blender!, spec, blend));
                if (!r.Ok || !File.Exists(blend)) { Say("Blender konnte die Szene nicht anlegen: " + Short(r.Err.Length > 0 ? r.Err : r.Out), "3d"); return; }
                rep = await Task.Run(() => Studio3DEngines.Run(blender!, blend, target, outDir));
            }
            LastEngineExport = rep; _last3dFile = rep.File;
            var text = Studio3DEngines.Text(rep);
            Directory.CreateDirectory(outDir);
            File.WriteAllText(Path.Combine(outDir, Path.GetFileNameWithoutExtension(rep.File) + "-bericht.txt"), $"{DateTime.Now:yyyy-MM-dd HH:mm}\n{profile.Why}\n{text}\n");
            _core.Audit.Append(Principal.LocalOwner.Name, "3d.engine.export", $"{target}: {Path.GetFileName(rep.File)} ({(rep.Ok ? "ok" : rep.Findings.Count(f => f.Error) + " Fehler")})");
            Say(text, "3d");
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or System.Text.Json.JsonException or KeyNotFoundException) { Say("Export nicht möglich: " + Trendo.Core.FriendlyError.Say(ex), "3d"); }
    }

    /// <summary>Karte „Für Engine“ in der linken Seitenleiste: Unity, Unreal, Godot, Web.</summary>
    private Control EngineExportCard(TextBox objName)
    {
        var row = new WrapPanel(); foreach (var c in EngineExportButtons(objName).Skip(1)) { c.Margin = new Thickness(0, 0, 4, 4); row.Children.Add(c); }
        return new StackPanel { Name = "3d-engine-export", Spacing = 6, Children = { H("Für Engine", 14), P("Export mit Prüfung: Blender liest die Datei wieder ein und vergleicht alles.", Muted, 11.5), row } };
    }

    /// <summary>Knöpfe „Für Engine“ (Unity, Unreal, Godot, Web).</summary>
    private List<Control> EngineExportButtons(TextBox objName)
    {
        var list = new List<Control> { new TextBlock { Name = "3d-engine-export", Text = "FÜR ENGINE", Foreground = Muted, FontSize = 10.5, FontWeight = FontWeight.SemiBold, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Margin = new Thickness(8, 0, 4, 0) } };
        foreach (var p in Studio3DEngines.Profiles)
        {
            var id = p.Id;
            var b = Act("3d-engine-" + p.Id, p.Label, () => _ = ExportForEngine(objName, id));
            ToolTip.SetTip(b, $"{p.Ext.TrimStart('.').ToUpperInvariant()}: {p.Why} Blender liest die Datei danach wieder ein und vergleicht Teile, Materialien, UV, Knochen, Animationen und Maße.");
            list.Add(b);
        }
        return list;
    }
}
