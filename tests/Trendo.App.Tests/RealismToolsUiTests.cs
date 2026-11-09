using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Trendo.Core;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>Freigabe 09.11.2026: Werkstück in 3D samt Foto, Tragwerk als Körper, Sonnenstand im Wohnungsfoto – über den echten Eingabeweg.</summary>
public class RealismTools_Ui
{
    static T Get<T>(MainWindow w, string name) where T : Control { Settle(); return w.GetVisualDescendants().OfType<T>().Last(c => c.Name == name && c.IsEffectivelyVisible); }
    static T? Try<T>(MainWindow w, string name) where T : Control { Settle(); return w.GetVisualDescendants().OfType<T>().LastOrDefault(c => c.Name == name && c.IsEffectivelyVisible); }
    static void Press(MainWindow w, string name) { var b = Get<Button>(w, name); b.BringIntoView(); Settle(); RealClick(w, Get<Button>(w, name)); }
    static async Task Wait(Func<bool> done) { for (var i = 0; i < 300 && !done(); i++) { await Task.Delay(10); Settle(); } }
    static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");

    [AvaloniaFact(DisplayName = "Fräsen UI: Werkstück in 3D zeigt den Abtrag mit Volumen, das Foto entsteht und erscheint, Zurück zur Bahnansicht geht")]
    public async Task Cnc_stock()
    {
        var (w, _, _) = Ui.Open();
        string? spec = null;
        w.CncPhotoOverride = async (s, png) => { spec = s; await File.WriteAllBytesAsync(png, Png); return true; };
        w.Show("cnc"); Settle(); RealClick(w, Get<Button>(w, "prog-tpl-leer"));
        Press(w, "prog-cmd-cnc-v-stock");
        Assert.True(Get<MeshView>(w, "cnc-stock").Model!.Triangles > 1000);
        Assert.Contains("cm³", Get<TextBlock>(w, "cnc-stock-facts").Text);
        Assert.Null(Try<CncPathView>(w, "cnc-view"));
        Press(w, "cnc-photo");
        await Wait(() => w.CncStockImage is not null);
        Assert.NotNull(spec); Assert.True(File.Exists(spec));
        Assert.NotNull(Try<Image>(w, "cnc-stock-image"));
        Press(w, "prog-cmd-cnc-v-stock");
        Assert.NotNull(Try<CncPathView>(w, "cnc-view"));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Statik UI: 3D-Ansicht zeigt das Tragwerk als Körper mit Überhöhung, zweiter Klick führt zur Zeichnung zurück")]
    public void Frame_3d()
    {
        var (w, _, _) = Ui.Open();
        w.Show("technik"); Settle(); RealClick(w, Get<Button>(w, "prog-tpl-einfeld"));
        Press(w, "prog-cmd-tk-3d");
        Assert.True(Get<MeshView>(w, "tk-3d-view").Model!.Triangles > 20);
        Assert.Contains("überhöht", Get<TextBlock>(w, "tk-3d-note").Text);
        Press(w, "prog-cmd-tk-3d");
        Assert.NotNull(Try<FrameCanvas>(w, "tk-canvas"));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Wohnung UI: Uhrzeit und Monat gehen als echter Sonnenstand in die Szene, ohne Wahl bleibt das Licht wie bisher")]
    public async Task Sun_choice()
    {
        var (w, _, _) = Ui.Open();
        System.Text.Json.Nodes.JsonObject? seen = null;
        w.ArchPhotoRenderer = async (scene, file, _, ct) => { seen = scene; Directory.CreateDirectory(Path.GetDirectoryName(file)!); await File.WriteAllBytesAsync(file, Png, ct); return file; };
        w.Show(MainWindow.StudioPages["SPEC-01"]); Settle(); RealClick(w, Get<Button>(w, "prog-tpl-wohnung"));
        Press(w, "prog-cmd-arch-photo");
        Assert.Null(Try<ComboBox>(w, "arch-photo-month"));
        Press(w, "arch-photo-go"); await Wait(() => seen is not null && !w.ArchPhotoBusy);
        Assert.Null(seen!["sun"]);
        Get<ComboBox>(w, "arch-photo-hour").SelectedIndex = 8; Settle(); // 13 Uhr
        Get<ComboBox>(w, "arch-photo-month").SelectedIndex = 6; Settle(); // Juli
        seen = null;
        Press(w, "arch-photo-go"); await Wait(() => seen is not null && !w.ArchPhotoBusy);
        Assert.InRange((double)seen!["sun"]!["elevation"]!, 50, 65);
        Assert.Equal(13, w.ArchPhotoSunHour); Assert.Equal(7, w.ArchPhotoSunMonth);
        Get<ComboBox>(w, "arch-photo-hour").SelectedIndex = 16; Settle(); // 21 Uhr
        Get<ComboBox>(w, "arch-photo-month").SelectedIndex = 0; Settle(); // Januar: längst dunkel
        Assert.NotNull(Try<TextBlock>(w, "arch-photo-night"));
        w.Close();
    }
}
