using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core.Media;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>Paket 01 – Studio 4, S4-13 (06.11.2026): Modus „Beat“ in Trendo Musik.</summary>
public class MusikBeat_Ui
{
    [AvaloniaFact(DisplayName = "S4-13 Trendo Musik: Beat-Modus – Raster klicken (an → betont → aus), Tempo 140, Arpeggio ab; als Lied gespeichert (WAV 24 Bit + Muster); 600 BPM wird abgelehnt, nichts gespeichert")]
    public void Beat_mode()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000; Directory.CreateDirectory(root);
        try
        {
            w.Show("musik-studio"); w.MsPage("studio"); Settle(); // Suno V1: Beat liegt im Studio
            var mode = w.Find<Button>("ms-mode-beat"); mode.Press(); Settle();
            Assert.Equal("x...x...x...x...", w.MsBeatPattern.Tracks.Single(t => t.Id == "kick").Steps); // Vorlage EDM
            var cell = w.Find<Button>("ms-beat-kick-1"); cell.BringIntoView(); Settle(); cell.Press(); Settle();
            w.Find<Button>("ms-beat-kick-1").Press(); Settle();
            Assert.Equal("xX..x...x...x...", w.MsBeatPattern.Tracks.Single(t => t.Id == "kick").Steps);
            w.Find<Button>("ms-beat-kick-1").Press(); Settle();
            Assert.Equal('.', w.MsBeatPattern.Tracks.Single(t => t.Id == "kick").Steps[1]);
            // ungültiges Tempo: nichts speichern
            w.Find<NumericUpDown>("ms-beat-bpm").Maximum = 1000; w.Find<NumericUpDown>("ms-beat-bpm").Value = 600;
            var save = w.Find<Button>("ms-beat-save"); save.BringIntoView(); Settle(); save.Press(); Settle();
            Assert.Empty(w.Songs.All().Where(s => s.Kind == "beat"));
            Assert.Contains(w.GetLogicalDescendantsOfType<TextBlock>(), t => (t.Text ?? "").Contains("Beat nicht gespeichert") && t.Text!.Contains("300"));
            // gültig: 140 BPM, Arpeggio ab
            w.Find<NumericUpDown>("ms-beat-bpm").Value = 140; w.Find<ComboBox>("ms-beat-arp-mode").SelectedIndex = 1; w.Find<TextBox>("ms-beat-title").Text = "Mein Beat";
            save = w.Find<Button>("ms-beat-save"); save.BringIntoView(); Settle(); save.Press(); Settle();
            var song = w.Songs.All().Single(s => s.Kind == "beat");
            Assert.Equal(("Mein Beat", 140), (song.Title, song.Bpm)); Assert.True(File.Exists(song.File));
            Assert.Equal(24, AudioQa.WavBits(File.ReadAllBytes(song.File)));
            var json = File.ReadAllText(Path.ChangeExtension(song.File, ".beat.json"));
            var p = StepSequencer.FromJson(System.Text.Json.Nodes.JsonNode.Parse(json)!.AsObject());
            Assert.Equal((140.0, "down"), (p.Bpm, p.Arp.Mode));
        }
        finally { w.Close(); }
    }
}
