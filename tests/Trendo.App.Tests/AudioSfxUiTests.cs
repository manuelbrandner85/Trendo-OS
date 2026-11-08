using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.Core.Media;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>Alle Studios, Runde 7 – CORE-06: Spiel-Effekte in der Geräusch-Werkstatt.</summary>
public class AudioSfx_Ui
{
    [AvaloniaFact(DisplayName = "Spiel-Effekte: Laser würfeln → öffnet sich mit Lizenz „selbst erzeugt“ und Tag Spiel.Schuss; neue Variante ist anders; in die Bibliothek und ins Paket")]
    public void Make_variants_and_pack()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000; Directory.CreateDirectory(root);
        try
        {
            w.Show("ton"); Settle(); RealClick(w, V<Button>(w, "prog-tpl-leer")); Settle();
            w.Find<ComboBox>("au-sfx-kind").SelectedIndex = Array.FindIndex(Sfxr.Presets, k => k.Id == "laser");
            var make = w.Find<Button>("au-sfx-make"); make.BringIntoView(); Settle(); make.Press(); Settle();
            var d = w.ProgramActive("ton")!; var first = d.Text("source");
            Assert.Equal(Sfxr.License, d.Text("license")); Assert.Equal("laser", d.Text("sfxPreset"));
            Assert.Equal("Spiel.Schuss", w.Find<TextBox>("au-foley-tags").Text);
            Assert.Contains("Variante 1", w.ProgramMessage("ton"));
            make = w.Find<Button>("au-sfx-make"); make.BringIntoView(); Settle(); make.Press(); Settle();
            var second = w.ProgramActive("ton")!.Text("source");
            Assert.NotEqual(first, second); Assert.NotEqual(File.ReadAllBytes(first), File.ReadAllBytes(second));
            w.Find<TextBox>("au-foley-name").Text = "Laser 2";
            var add = w.Find<Button>("au-foley-add"); add.BringIntoView(); Settle(); add.Press(); Settle();
            var pack = w.Find<Button>("au-foley-pack"); pack.BringIntoView(); Settle(); pack.Press(); Settle();
            var r = w.AuFoleyLastPack!;
            Assert.Equal(["Laser 2"], r.Included.Select(e => e.Name)); Assert.Contains("Spiel.Schuss", r.Included[0].Tags);
        }
        finally { w.Close(); }
    }
}
