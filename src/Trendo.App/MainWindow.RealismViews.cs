using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Trendo.Core;
using Trendo.Core.Programs;
using Trendo.Core.Statik;
using Path = System.IO.Path;

namespace Trendo.App;

/// <summary>Freigabe 09.11.2026: Fräsen mit Abtrag in 3D samt Foto, Statik als Körper.</summary>
public partial class MainWindow
{
    private bool _cncStock, _frame3d;
    public string CncStockMessage { get; private set; } = "";
    public string? CncStockImage { get; private set; }
    private bool _cncPhotoBusy;
    /// <summary>Für Tests: Blender-Lauf nachbilden (Auftrag, Zielbild) → erfolgreich.</summary>
    public Func<string, string, Task<bool>>? CncPhotoOverride { get; set; }

    private static string CncMaterialId(string material) => material.ToLowerInvariant() switch { var m when m.Contains("holz") => "holz", var m when m.Contains("kunst") || m.Contains("acryl") || m.Contains("pom") => "kunststoff-matt", var m when m.Contains("alu") => "alu-gefraest", _ => "metall" };

    private static string CncPhotoColor(string material) => material.ToLowerInvariant() switch { var m when m.Contains("holz") => "#B07A48", var m when m.Contains("kunst") || m.Contains("acryl") || m.Contains("pom") => "#E8E8E2", var m when m.Contains("messing") => "#E6C27A", var m when m.Contains("stahl") => "#C4C7CC", _ => "#F0F1F2" };

    private static (byte, byte, byte) CncColor(string material) => CncMaterialId(material) switch { "holz" => ((byte)176, (byte)132, (byte)82), "kunststoff-matt" => ((byte)232, (byte)232, (byte)226), _ => ((byte)184, (byte)188, (byte)193) };

    private int CncCurrentStep(IReadOnlyList<CncSeg> segs) => _cncT < 0 ? -1 : CncSim.AtTime(segs, _cncT);

    private Control CncStockView(ProgramDocument d, List<CncSeg> segs, CncJob j)
    {
        CncStock stock;
        try { stock = CncStock.Cut(segs, j.ToolD, CncCurrentStep(segs)); }
        catch (Exception e) when (e is InvalidOperationException or ArgumentException) { return new TextBlock { Name = "cnc-stock-empty", Text = e.Message, Foreground = Text, Margin = new Thickness(14), TextWrapping = TextWrapping.Wrap }; }
        var volume = stock.RemovedVolume; var grams = volume * CncStock.Density(j.Material);
        var facts = new TextBlock { Name = "cnc-stock-facts", Text = $"{j.Material}: {volume:0.0} cm³ abgetragen, das sind {grams:0.#} g Späne", Foreground = Text, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
        var run = Act("cnc-photo", _cncPhotoBusy ? "Rechnet …" : "Fertiges Teil als Foto", () => _ = CncPhoto(segs, j), true); run.IsEnabled = !_cncPhotoBusy;
        var bar = new DockPanel { Margin = new Thickness(0, 0, 0, 6), Children = { Dk(run, Avalonia.Controls.Dock.Right), facts } };
        var view = new MeshView { Name = "cnc-stock", Model = stock.ToMesh(CncColor(j.Material)), Yaw = -35, Pitch = 35 };
        var dock = new DockPanel { Children = { Dk(bar, Avalonia.Controls.Dock.Top) } };
        if (CncStockMessage.Length > 0) dock.Children.Add(Dk(new TextBlock { Name = "cnc-stock-msg", Text = CncStockMessage, Foreground = Text, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6) }, Avalonia.Controls.Dock.Top));
        if (CncStockImage is { } img && File.Exists(img))
            try { dock.Children.Add(Dk(new Image { Name = "cnc-stock-image", Source = new Bitmap(img), MaxHeight = 280, Margin = new Thickness(0, 0, 0, 6) }, Avalonia.Controls.Dock.Top)); } catch (Exception e) when (e is IOException or ArgumentException or InvalidOperationException) { }
        dock.Children.Add(view);
        return dock;
    }

    /// <summary>Fertiges Teil mit echter Oberfläche rendern (öffentlich für Tests).</summary>
    public async Task CncPhoto(IReadOnlyList<CncSeg> segs, CncJob j)
    {
        if (_cncPhotoBusy) return;
        CncStock stock;
        try { stock = CncStock.Cut(segs, j.ToolD, cells: CncStock.PhotoCells); }
        catch (Exception e) when (e is InvalidOperationException or ArgumentException) { CncStockMessage = e.Message; Show(CK); return; }
        _cncPhotoBusy = true; CncStockMessage = "Das Bild wird gerechnet."; CncStockImage = null; Show(CK);
        try
        {
            var dir = Path.Combine(WorkFolder("Fertigung"), "Foto"); Directory.CreateDirectory(dir);
            var material = CncMaterialId(j.Material);
            var scene = new Scene3D { Name = "Werkstück", Light = "produkt" }; scene.Parts.Add(stock.ToPart(material, CncPhotoColor(j.Material)));
            var textures = new Dictionary<string, string>();
            if (CncPhotoOverride is null && Studio3DPro.MaterialOf(material).TextureId is { } tid)
                try { if (await AmbientTexture(tid) is { } t) textures[tid] = t; }
                catch (Exception e) when (e is HttpRequestException or IOException or InvalidOperationException or TaskCanceledException) { CncStockMessage = "Die echte Oberfläche ließ sich nicht laden, es wird mit Farbe gerendert."; }
            var spec = scene.WriteBlenderSpec(Path.Combine(dir, "blender"), textures);
            var png = Path.Combine(dir, $"teil-{DateTime.Now:yyyyMMdd-HHmmss}.png");
            bool ok;
            if (CncPhotoOverride is { } fake) ok = await fake(spec, png);
            else
            {
                var blender = BlenderPath;
                if (blender is null) { CncStockMessage = "Für das Foto wird Blender gebraucht. Es lässt sich im Studio „3D-Modelle erstellen“ einrichten."; return; }
                var (r, _) = await Task.Run(() => Studio3DPro.Render(blender, spec, png, 200));
                ok = r.Ok && File.Exists(png);
                if (!ok) { CncStockMessage = "Blender konnte nicht rendern: " + (r.Err.Length > 0 ? r.Err : r.Out).Trim().Split('\n').LastOrDefault(); return; }
            }
            CncStockImage = ok && File.Exists(png) ? png : null;
            CncStockMessage = CncStockImage is null ? "Blender hat kein Bild geliefert." : "Fertig: " + png;
            _core.Audit.Append(Principal.LocalOwner.Name, "cnc.foto", Path.GetFileName(png));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException) { CncStockMessage = "Foto ging nicht: " + Trendo.Core.FriendlyError.Say(e); }
        finally { _cncPhotoBusy = false; Show(CK); }
    }

    private Control Frame3DView(FrameModel model, FrameResult result)
    {
        var view = new MeshView { Name = "tk-3d-view", Model = Frame3D.Build(model, result), Yaw = -30, Pitch = 18 };
        var note = new TextBlock { Name = "tk-3d-note", Text = $"Biegung {Frame3D.Exaggeration(model, result):0}-fach überhöht. Rot zeigt die stärkste Belastung.", Foreground = Text, FontSize = 12, Margin = new Thickness(10, 8) };
        return new DockPanel { Children = { Dk(note, Avalonia.Controls.Dock.Top), view } };
    }
}
