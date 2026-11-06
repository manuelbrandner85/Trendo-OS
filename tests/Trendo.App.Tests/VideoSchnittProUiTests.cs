using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Trendo.App;
using Trendo.Core.Media;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>CORE-08 Videoschnitt wie Premiere/DaVinci (Profi 23, 05.11.2026): Farbe, Ton, Übergang, Titellage je Stück und zweite Bildspur in der Oberfläche.</summary>
public class VideoSchnittPro_Ui
{
    static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");

    [AvaloniaFact(DisplayName = "CORE-08 Videoschnitt Profi: Look Schwarzweiß, Ton 50 %, Übergang Wischen, Titel oben – als ein Schritt rückgängig; Logo auf die zweite Bildspur, Ecke unten links, entfernen")]
    public void Clip_pro_and_overlay()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000; Directory.CreateDirectory(root);
        try
        {
            string F(string n, byte[]? b = null) { var f = Path.Combine(root, n); File.WriteAllBytes(f, b ?? [1, 2, 3]); return f; }
            w.Show("schnitt"); Settle(); RealClick(w, V<Button>(w, "prog-tpl-leer"));
            w.EditAdd(F("a.mp4"), length: 6, audio: true); w.EditAdd(F("b.mp4"), length: 6, audio: true);
            var d = w.ProgramActive("schnitt")!; var first = VideoEdit.Clips(d.Data)[0];
            w.EditSelected = ("clips", 0); w.EditTab = "auswahl"; w.Show("schnitt"); Settle();
            w.Find<ComboBox>("ve-look").SelectedIndex = Array.FindIndex(VideoPro.Looks, l => l.Id == "sw"); Settle();
            Assert.Equal(0, w.Find<Slider>("ve-color-s").Value); // Look setzt die Regler
            w.Find<Slider>("ve-clip-vol").Value = 0.5;
            w.Find<ComboBox>("ve-transition").SelectedIndex = Array.FindIndex(VideoPro.Transitions, t => t.Id == "wipeleft");
            w.Find<ComboBox>("ve-title-pos").SelectedIndex = 2;
            var b = w.Find<Button>("ve-pro-apply"); b.BringIntoView(); Settle(); b.Press(); Settle();
            d = w.ProgramActive("schnitt")!;
            var fx = VideoPro.Fx(d.Data, first.Id);
            Assert.Equal((0.0, "wipeleft", "oben"), (fx.Saturation, fx.Transition, fx.TitlePos));
            Assert.Equal(0.5, VideoEdit.Clips(d.Data)[0].Volume);
            Assert.Equal("Stück einstellen", d.UndoLabel);
            d.Undo(); Assert.Equal(1.0, VideoPro.Fx(d.Data, first.Id).Saturation); Assert.Equal(1.0, VideoEdit.Clips(d.Data)[0].Volume);
            // zweite Bildspur
            w.EditTab = "medien"; w.Show("schnitt"); Settle();
            w.Find<TextBox>("ve-file").Text = F("logo.png", Png);
            var add = w.Find<Button>("ve-add-overlay"); add.BringIntoView(); Settle(); add.Press(); Settle();
            d = w.ProgramActive("schnitt")!;
            Assert.Single(VideoPro.Overlays(d.Data)); Assert.Contains(w.GetVisualDescendants().OfType<Border>(), x => x.Name == "ve-overlay-0" || x.Child is StackPanel { Name: "ve-overlay-0" });
            w.Find<ComboBox>("ve-overlay-corner-0").SelectedIndex = 3; w.Find<Slider>("ve-overlay-size-0").Value = 0.2;
            w.Find<Button>("ve-overlay-apply-0").Press(); Settle();
            var o = VideoPro.Overlays(w.ProgramActive("schnitt")!.Data)[0];
            Assert.Equal(0.03, o.X, 2); Assert.True(o.Y > 0.5, $"nicht unten: {o.Y}"); Assert.Equal(0.2, o.Width, 2);
            // Paket 01 S3-05: Greenscreen blau mit Maske rechts
            w.Find<CheckBox>("ve-overlay-key-0").IsChecked = true; w.Find<ComboBox>("ve-overlay-keycolor-0").SelectedIndex = 1; w.Find<ComboBox>("ve-overlay-mask-0").SelectedIndex = 2;
            w.Find<Button>("ve-overlay-apply-0").Press(); Settle();
            o = VideoPro.Overlays(w.ProgramActive("schnitt")!.Data)[0];
            Assert.Equal(("#0000FF", (0.5, 0.0, 0.5, 1.0)), (o.Key, o.Mask!.Value)); Assert.Equal(0.2, o.Width, 2);
            Assert.True(w.Find<CheckBox>("ve-overlay-key-0").IsChecked); Assert.Equal(2, w.Find<ComboBox>("ve-overlay-mask-0").SelectedIndex); // Karte zeigt den Stand
            w.Find<Button>("ve-overlay-del-0").Press(); Settle();
            Assert.Empty(VideoPro.Overlays(w.ProgramActive("schnitt")!.Data));
            // Ton auf die zweite Bildspur geht nicht
            w.Find<TextBox>("ve-file").Text = F("ton.mp3"); w.Find<Button>("ve-add-overlay").Press(); Settle();
            Assert.Contains("Bilder und Videos", w.ProgramMessage("schnitt"));
        }
        finally { w.Close(); }
    }
}
